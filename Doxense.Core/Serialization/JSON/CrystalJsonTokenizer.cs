#region Copyright (c) 2023-2024 SnowBank SAS, (c) 2005-2023 Doxense SAS
// All rights reserved.
// 
// Redistribution and use in source and binary forms, with or without
// modification, are permitted provided that the following conditions are met:
// 	* Redistributions of source code must retain the above copyright
// 	  notice, this list of conditions and the following disclaimer.
// 	* Redistributions in binary form must reproduce the above copyright
// 	  notice, this list of conditions and the following disclaimer in the
// 	  documentation and/or other materials provided with the distribution.
// 	* Neither the name of SnowBank nor the
// 	  names of its contributors may be used to endorse or promote products
// 	  derived from this software without specific prior written permission.
// 
// THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS "AS IS" AND
// ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED
// WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE
// DISCLAIMED. IN NO EVENT SHALL SNOWBANK SAS BE LIABLE FOR ANY
// DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES
// (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES;
// LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND
// ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT
// (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS
// SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
#endregion

//#define ENABLE_SOURCE_POSITION

namespace Doxense.Serialization.Json
{
	using System.Buffers;
	using Doxense.Text;

	/// <summary>Helper utilisé pour lire et parser un texte JSON</summary>
	/// <remarks>Doit être alloué sur la stack de l'appelant, et passé "by ref" !</remarks>
	public struct CrystalJsonTokenizer<TReader> : IDisposable
		where TReader : struct, IJsonReader
	{

		private const char EMPTY_TOKEN = '\0';

		// la classe supporte deux mode: String ou Stream
		// -> en mode String, on lit directement dans m_text[m_pos]
		// -> en mode Stream, on passe via m_source

		/// <summary>Source JSON (si mode Stream)</summary>
		public TReader Source;

		private char Token; // buffer utilisé pour "push back" un caractère lu du stream

		/// <summary>Paramètres de lecture</summary>
		public readonly CrystalJsonSettings Settings;

#if ENABLE_SOURCE_POSITION
		private long m_offset;
		private int m_position;
		private int m_line;
#endif

		private readonly CrystalJsonSettings.StringInterning InternMode;

		/// <summary>Comparateur par défaut pour les clés d'un JsonObject</summary>
		public readonly IEqualityComparer<string> FieldComparer;

		private StringTable? StringTable;

		public CrystalJsonTokenizer(TReader source, CrystalJsonSettings? settings)
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
		}

#if ENABLE_SOURCE_POSITION
		/// <summary>Nombre de charactères lus dans la source</summary>
		public long Offset => m_offset;

		/// <summary>Nombre de charactères lus dans la ligne actuelle (commence à 0!)</summary>
		public long Position => m_position;

		/// <summary>Nombre de lignes lues dans la source (commence à 0!)</summary>
		public long Line => m_line;
#endif

		/// <summary>Lit le prochain caractère significatif, qui ne soit pas un espace</summary>
		/// <returns>Prochain caractère, ou EndOfStream si fini</returns>
		/// <remarks>Si Push(x) a été appelé juste avant, retourne la valeur de x. Sinon lit dans le stream</remarks>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		internal char ReadNextToken()
		{
			// il peut y avoir un caractère "pushed back" dans le cache....
			char c = this.Token;
			if (c == EMPTY_TOKEN)
			{ // pas de token en cache, on lit le prochain caractère dans le buffer...
				c = ReadOne();
			}
			else
			{ // on avait un token en cache, on le consomme...
				this.Token = EMPTY_TOKEN;
			}

			//REVIEW: vérifier si '\xA0' (&#160;) est considéré comme un espace ou non par JSON et JS ?
			return c >= CrystalJsonParser.WHITE_CHAR_MAP ? c : ConsumeWhiteSpaces(c);
		}

		[MethodImpl(MethodImplOptions.NoInlining)]
		private char ConsumeWhiteSpaces(char token)
		{
			// skip les espaces éventuels
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

		/// <summary>Replace un caractère dans le buffer (consommé par le prochaine ReadNextToken)</summary>
		/// <param name="c">Char à remettre dans le stream</param>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		internal void Push(char c)
		{
			Paranoid.Requires(this.Token == EMPTY_TOKEN, "Cannot push more than one character");
			this.Token = c;
		}

		/// <summary>Lit un caractère du stream</summary>
		/// <returns>Prochain char, ou EndOfStream si stream fini</returns>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		internal char ReadOne()
		{
			char c = (char) this.Source.Read();
#if ENABLE_SOURCE_POSITION
			UpdateSourcePosition(c);
#endif
			return c;
		}

#if ENABLE_SOURCE_POSITION
		[MethodImpl(MethodImplOptions.NoInlining)]
		private void UpdateSourcePosition(char c)
		{
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
		}
#endif

		/// <summary>Vérifie que le stream contient bien les charactères spécifiés</summary>
		/// <param name="values">Charactères d'un token a lire</param>
		/// <exception cref="System.FormatException">Si le stream contient autre chose que les charactères, ou s'il est fini prématurément</exception>
		internal void ReadExpectedKeyword(ReadOnlySpan<char> values)
		{
			char c;
			foreach (char value in values)
			{
				c = ReadOne();
				if (c != value)
				{
					if (c == CrystalJsonParser.EndOfStream)
					{
						throw FailUnexpectedEndOfStream(null);
					}
					else
					{
						throw FailInvalidSyntax($"Invalid character '{c}' found while expecting '{value}'");
					}
				}
			}
			// normalement juste derrière on doit etre en fin de stream, ou on doit trouver un séparateur ou terminateur
			c = ReadOne();
			if (c == CrystalJsonParser.EndOfStream)
			{
				return;
			}

			// on le remet dans le stream pour la suite
			Push(c);

			if (char.IsLetterOrDigit(c))
			{
				throw FailInvalidSyntax($"Invalid character '{c}' found after expected keyword");
			}
		}

#if ENABLE_SOURCE_POSITION
		[Pure]
		internal JsonSyntaxException FailInvalidSyntax(string reason) => new("Invalid JSON syntax", reason, m_offset - 1, m_line + 1, m_position);

		[Pure]
		internal JsonSyntaxException FailInvalidSyntax(ref DefaultInterpolatedStringHandler reason) => new("Invalid JSON syntax", reason.ToStringAndClear(), m_offset - 1, m_line + 1, m_position);

		[Pure]
		internal JsonSyntaxException FailUnexpectedEndOfStream(string? reason) => new("Unexpected end of stream", reason, m_offset - 1, m_line + 1, m_position);
#else
		[Pure]
		internal JsonSyntaxException FailInvalidSyntax(string reason) => new("Invalid JSON syntax", reason);

		[Pure]
		internal JsonSyntaxException FailInvalidSyntax(ref DefaultInterpolatedStringHandler reason) => new("Invalid JSON syntax", reason.ToStringAndClear());

		[Pure]
		internal JsonSyntaxException FailUnexpectedEndOfStream(string? reason) => new("Unexpected end of stream", reason);
#endif

		/// <summary>Retourne la StringTable pour la génération de literals, ou null si interning désactivé</summary>
		/// <param name="kind">Type de literal parsé (nom de champ, string, number, ...)</param>
		/// <returns>StringTable à utiliser, ou null si pas d'interning pour ce type de literal avec le paramétrage actuel</returns>
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
