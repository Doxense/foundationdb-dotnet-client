#region Copyright (c) 2005-2023 Doxense SAS
//
// All rights are reserved. Reproduction or transmission in whole or in part, in
// any form or by any means, electronic, mechanical or otherwise, is prohibited
// without the prior written consent of the copyright owner.
//
#endregion

//#define ENABLE_SOURCE_POSITION

namespace Doxense.Serialization.Json
{
	using System;
	using System.Buffers;
	using System.Collections.Generic;
	using System.Diagnostics;
	using System.Globalization;
	using System.Runtime.CompilerServices;
	using Doxense.Diagnostics.Contracts;
	using Doxense.Text;
	using JetBrains.Annotations;

	/// <summary>Helper utilis� pour lire et parser un texte JSON</summary>
	/// <remarks>Doit �tre allou� sur la stack de l'appelant, et pass� "by ref" !</remarks>
	public struct CrystalJsonTokenizer<TReader> : IDisposable
		where TReader : struct, IJsonReader
	{

		private const char EMPTY_TOKEN = '\0';

		// la classe supporte deux mode: String ou Stream
		// -> en mode String, on lit directement dans m_text[m_pos]
		// -> en mode Stream, on passe via m_source

		/// <summary>Source JSON (si mode Stream)</summary>
		public TReader Source;

		private char Token; // buffer utilis� pour "push back" un caract�re lu du stream

		/// <summary>Param�tres de lecture</summary>
		public readonly CrystalJsonSettings Settings;

#if ENABLE_SOURCE_POSITION
		private long m_offset;
		private int m_position;
		private int m_line;
#endif

		private readonly CrystalJsonSettings.StringInterning InternMode;

		/// <summary>Comparateur par d�faut pour les cl�s d'un JsonObject</summary>
		public readonly IEqualityComparer<string> FieldComparer;

		private StringTable? StringTable;

		private JsonValue[]? ArrayBuffer;

		private KeyValuePair<string, JsonValue>[]? ObjectBuffer;

		public CrystalJsonTokenizer(TReader source, CrystalJsonSettings settings)
			: this()
		{
			this.Source = source;
			this.Settings = settings ?? CrystalJsonSettings.Json;
			this.InternMode = this.Settings.InterningMode;
			this.FieldComparer = this.Settings.IgnoreCaseForNames ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;
		}

		public void Dispose()
		{
			this.Source = default;
			if (this.StringTable != null)
			{
				this.StringTable.Dispose();
				this.StringTable = null;
			}
			if (this.ArrayBuffer != null)
			{
				ArrayPool<JsonValue>.Shared.Return(this.ArrayBuffer);
				this.ArrayBuffer = null;
			}
			if (this.ObjectBuffer != null)
			{
				ArrayPool<KeyValuePair<string, JsonValue>>.Shared.Return(this.ObjectBuffer);
				this.ObjectBuffer = null;
			}
		}

#if DEPRECATED
		/// <summary>Retourne une estimation de la position dans le stream source, pour information</summary>
		/// <remarks>ATTENTION: cette valeur ne correspond pas � la position exacte du dernier caract�re lu!
		/// Si le reader JSON utilis� buffer les caract�res lu (la majorit� du temps), alors la position dans le stream source sera toujours en avance sur la position r�elle du reader!
		/// </remarks>
		public long? PositionHint
		{
			get
			{
				if (((object) this.Source) is StreamReader sr) return sr.BaseStream.Length;
				return null;
			}
		}
#endif

#if ENABLE_SOURCE_POSITION
		/// <summary>Nombre de charact�res lus dans la source</summary>
		public long Offset { get { return m_offset; } }

		/// <summary>Nombre de charact�res lus dans la ligne actuelle (commence � 0!)</summary>
		public long Position { get { return m_position; } }

		/// <summary>Nombre de lignes lues dans la source (commence � 0!)</summary>
		public long Line { get { return m_line; } }
#endif

		internal JsonValue[] AcquireArrayBuffer()
		{
			var buffer = this.ArrayBuffer;
			this.ArrayBuffer = null;
			return buffer ?? ArrayPool<JsonValue>.Shared.Rent(16);
		}

		internal void ResizeArrayBuffer(ref JsonValue[] buffer)
		{
			int newSize = Math.Max(buffer.Length << 1, 16);
			var tmp = ArrayPool<JsonValue>.Shared.Rent(newSize);
			Array.Copy(buffer, 0, tmp, 0, buffer.Length);
			ArrayPool<JsonValue>.Shared.Return(buffer, clearArray: true);
			buffer = tmp;
		}

		internal void ReleaseArrayBuffer(JsonValue[] buffer)
		{
			var prev = this.ArrayBuffer;
			if (prev == null)
			{ // keep it
				this.ArrayBuffer = buffer;
			}
			else if (prev.Length < buffer.Length)
			{ // discard previous
				ArrayPool<JsonValue>.Shared.Return(prev, clearArray: true);
				this.ArrayBuffer = buffer;
			}
			else
			{ // discard current
				ArrayPool<JsonValue>.Shared.Return(buffer, clearArray: true);
			}
		}

		internal KeyValuePair<string, JsonValue>[] AcquireObjectBuffer()
		{
			var buffer = this.ObjectBuffer;
			this.ObjectBuffer = null;
			return buffer ?? ArrayPool<KeyValuePair<string, JsonValue>>.Shared.Rent(16);
		}

		internal void ResizeObjectBuffer(ref KeyValuePair<string, JsonValue>[] buffer)
		{
			int newSize = Math.Max(buffer.Length << 1, 8);
			var tmp = ArrayPool<KeyValuePair<string, JsonValue>>.Shared.Rent(newSize);
			Array.Copy(buffer, 0, tmp, 0, buffer.Length);
			ArrayPool<KeyValuePair<string, JsonValue>>.Shared.Return(buffer, clearArray: true);
			buffer = tmp;
		}

		internal void ReleaseObjectBuffer(KeyValuePair<string, JsonValue>[] buffer)
		{
			var prev = this.ObjectBuffer;
			if (prev == null)
			{ // keep it
				this.ObjectBuffer = buffer;
			}
			else if (prev.Length < buffer.Length)
			{ // discard previous
				ArrayPool<KeyValuePair<string, JsonValue>>.Shared.Return(prev, clearArray: true);
				this.ObjectBuffer = buffer;
			}
			else
			{ // discard current
				ArrayPool<KeyValuePair<string, JsonValue>>.Shared.Return(buffer, clearArray: true);
			}
		}

		/// <summary>Lit le prochain caract�re significatif, qui ne soit pas un espace</summary>
		/// <returns>Prochain caract�re, ou EndOfStream si fini</returns>
		/// <remarks>Si Push(x) a �t� appel� juste avant, retourne la valeur de x. Sinon lit dans le stream</remarks>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		internal char ReadNextToken()
		{
			// il peut y avoir un caract�re "pushed back" dans le cache....
			char c = this.Token;
			if (c == EMPTY_TOKEN)
			{ // pas de token en cache, on lit le prochain caract�re dans le buffer...
				c = ReadOne();
			}
			else
			{ // on avait un token en cache, on le consomme...
				this.Token = EMPTY_TOKEN;
			}

			//REVIEW: v�rifier si '\xA0' (&#160;) est consid�r� comme un espace ou non par JSON et JS ?
			return c >= CrystalJsonParser.WHITE_CHAR_MAP ? c : ConsumeWhiteSpaces(c);
		}

		[MethodImpl(MethodImplOptions.NoInlining)]
		private char ConsumeWhiteSpaces(char token)
		{
			// skip les espaces �ventuels
			var wc = CrystalJsonParser.WhiteCharsMap;
			char c = token;
			while (wc[c])
			{
				c = ReadOne();
				if (c >= CrystalJsonParser.WHITE_CHAR_MAP) break;
			}
			// ce n'est pas un espace
			return c;

		}

		/// <summary>Replace un caract�re dans le buffer (consomm� par le prochaine ReadNextToken)</summary>
		/// <param name="c">Char � remettre dans le stream</param>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		internal void Push(char c)
		{
			Paranoid.Requires(this.Token == EMPTY_TOKEN, "Cannot push more than one character");
			this.Token = c;
		}

		/// <summary>Lit un caract�re du stream</summary>
		/// <returns>Prochain char, ou EndOfStream si stream fini</returns>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		internal char ReadOne()
		{
			char c = (char) this.Source.Read();
			UpdateSourcePosition(c);
			return c;
		}

		[Conditional("ENABLE_SOURCE_POSITION")]
		private void UpdateSourcePosition(char c)
		{
#if ENABLE_SOURCE_POSITION
			if (c == '\n')
			{
				++m_line;
				m_position = 0;
				++m_offset;
			}
			else if (c != CrystalJsonParser.EndOfStream)
			{
				++m_position;
				++m_offset;
			}
#endif
		}

		/// <summary>V�rifie que le stream contient bien les charact�res sp�cifi�s</summary>
		/// <param name="values">Charact�res d'un token a lire</param>
		/// <exception cref="System.FormatException">Si le stream contient autre chose que les charact�res, ou s'il est fini pr�matur�ment</exception>
		internal void ReadExpectedKeyword(char[] values)
		{
			Contract.Debug.Requires(values != null);

			char c;
			foreach (char value in values)
			{
				c = ReadOne();
				if (c != value)
				{
					if (c == CrystalJsonParser.EndOfStream) throw FailUnexpectedEndOfStream(null);
					throw FailInvalidSyntax("Invalid character '{0}' found while expecting '{1}'", c, value);
				}
			}
			// normalement juste derri�re on doit etre en fin de stream, ou on doit trouver un s�parateur ou terminateur
			c = ReadOne();
			if (c == CrystalJsonParser.EndOfStream) return;
			Push(c); // on le remet dans le stream pour la suite
			if (char.IsLetterOrDigit(c)) throw FailInvalidSyntax("Invalid character '{0}' found after expected keyword", c);
		}

		[Pure]
		internal JsonSyntaxException FailInvalidSyntax(string reason)
		{
#if ENABLE_SOURCE_POSITION
			return new JsonSyntaxException("Invalid JSON syntax", reason, m_offset - 1, m_line + 1, m_position);
#else
			return new JsonSyntaxException("Invalid JSON syntax", reason);
#endif
		}

		[Pure, StringFormatMethod("reason")]
		internal JsonSyntaxException FailInvalidSyntax(string reason, object arg0)
		{
#if ENABLE_SOURCE_POSITION
			return new JsonSyntaxException("Invalid JSON syntax", String.Format(CultureInfo.InvariantCulture, reason, arg0), m_offset - 1, m_line + 1, m_position);
#else
			return new JsonSyntaxException("Invalid JSON syntax", String.Format(CultureInfo.InvariantCulture, reason, arg0));
#endif
		}

		[Pure, StringFormatMethod("reason")]
		internal JsonSyntaxException FailInvalidSyntax(string reason, object arg0, object arg1)
		{
#if ENABLE_SOURCE_POSITION
			return new JsonSyntaxException("Invalid JSON syntax", String.Format(CultureInfo.InvariantCulture, reason, arg0, arg1), m_offset - 1, m_line + 1, m_position);
#else
			return new JsonSyntaxException("Invalid JSON syntax", String.Format(CultureInfo.InvariantCulture, reason, arg0, arg1));
#endif
		}

		/// <summary>G�n�re une exception correspondant � une fin de stream pr�matur�e</summary>
		[Pure]
		internal JsonSyntaxException FailUnexpectedEndOfStream(string? reason)
		{
#if ENABLE_SOURCE_POSITION
			return new JsonSyntaxException("Unexpected end of stream", reason, m_offset - 1, m_line + 1, m_position);
#else
			return new JsonSyntaxException("Unexpected end of stream", reason);
#endif
		}

		/// <summary>Retourne la StringTable pour la g�n�ration de literals, ou null si interning d�sactiv�</summary>
		/// <param name="kind">Type de literal pars� (nom de champ, string, number, ...)</param>
		/// <returns>StringTable � utiliser, ou null si pas d'interning pour ce type de literal avec le param�trage actuel</returns>
		internal StringTable? GetStringTable(JsonLiteralKind kind)
		{
			switch (this.InternMode)
			{
				case CrystalJsonSettings.StringInterning.Default:
				{ // Default: autorise les fields
					if (kind != JsonLiteralKind.Field)
					{
						return null;
					}
					break;
				}
				case CrystalJsonSettings.StringInterning.IncludeNumbers:
				{ // Numbers: autorise les fields, les petits nombres et les grand nombres
					if (kind == JsonLiteralKind.Value)
					{
						return null;
					}
					break;
				}
				case CrystalJsonSettings.StringInterning.Disabled:
				{ // Disabled: jamais!
					return null;
				}
				//other: tout!
			}
			return this.StringTable ??= StringTable.GetInstance();
		}

	}
}
