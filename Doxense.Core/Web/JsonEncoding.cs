#region Copyright Doxense 2010-2022
//
// All rights are reserved. Reproduction or transmission in whole or in part, in
// any form or by any means, electronic, mechanical or otherwise, is prohibited
// without the prior written consent of the copyright owner.
//
#endregion

namespace Doxense.Serialization.Json
{
	using System;
	using System.Globalization;
	using System.Runtime.CompilerServices;
	using System.Text;
	using Doxense.Diagnostics.Contracts;
	using Doxense.Text;
	using JetBrains.Annotations;

	public static class JsonEncoding
	{

		/// <summary>Table de lookup pour savoir si un caractère doit etre encodé</summary>
		/// <remarks>lookup[index] contient true si le charactère UNICODE correspondant doit être éncodé</remarks>
		private static readonly bool[] EscapingLookupTable = InitializeEscapingLookupTable();

		private static bool[] InitializeEscapingLookupTable()
		{
			// IMPORTANT: le tableau DOIT avoir une taille de 64K car on va l'indexer avec des caractères UNICODE!
			var table = new bool[65536];
			for (int i = 0; i < table.Length; i++)
			{
				table[i] = NeedsEscaping((char) i);
			}

			//JIT_HACK: touche la StringTable pour qu'elle soit JITée immédiatement
			StringTable.EnsureJit();

			return table;
		}

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool NeedsEscaping(char c)
		{
			// On encode la double-quote ("), l'anti-slash (\), les ASCII control codes (0..31) et les caractères UNICODE spéciaux (0xD800-0xDFFF, 0xFFFE et 0xFFFF)
			return (c < 32 | c == '"' | c == '\\') || (c >= 0xD800 && (c < 0xE000 | c >= 0xFFFE));
		}

		/// <summary>Détermine si le texte JSON est "clean" (ie: ne nécessite pas d'encodage)</summary>
		/// <param name="s">Chaîne à vérifier</param>
		/// <returns>False si tt les caractères sont valides, True si au moins un nécessite d'être encodé</returns>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool NeedsEscaping(string s)
		{
			// A string is a collection of zero or more Unicode characters, wrapped in double quotes, using backslash escapes.
			// A character is represented as a single character string. A string is very much like a C or Java string.

			// Après des de nombreux benchs :
			// * Les version "lookup" sont plus rapides (mais nécessitent un table de 64Ko)
			// * Les version "unsafe" sont toutes plus lentes, sauf avec de l'unrolling
			// * Le cross over entre lookup et unrolled est entre 6 et 12...

			return s.Length <= 6
				? NeedsEscapingShort(s)
				: NeedsEscapingLong(s);
		}

		/// <summary>Détermine si le texte JSON est "clean" (ie: ne nécessite pas d'encodage)</summary>
		/// <param name="s">Chaîne à vérifier</param>
		/// <returns>False si tt les caractères sont valides, True si au moins un nécessite d'être encodé</returns>
		public static bool NeedsEscapingShort(string s)
		{
			var lookup = EscapingLookupTable;
			foreach (var c in s)
			{
				if (lookup[c]) return true;
			}
			return false;
		}

		/// <summary>Détermine si le texte JSON est "clean" (ie: ne nécessite pas d'encodage)</summary>
		/// <param name="s">Chaîne à vérifier</param>
		/// <returns>False si tt les caractères sont valides, True si au moins un nécessite d'être encodé</returns>
		public static unsafe bool NeedsEscapingLong(string s)
		{
			Contract.Debug.Requires(s != null);

			// Unroll la boucle de test, optimisée pour les chaînes de tailles > 8

			// On part du principe que 99.99% des strings encodées sont propre, et donc que lookup[c] retournera (presque) toujours false.
			// Si on utilise un OR bitwise (|), on a un seul test/branch par boucle de 4, alors que si on utilise les OR logique (||) il y a un test à chaque step donc 4 tests/branch bar boucle de 4.

#if DISABLE_UNSAFE_CODE
			// Version "safe" (20-30% de perf en moins)
			var lookup = s_escapingLookupTable;
			int n = s.Length;
			int p = 0;

			// loop unrolling
			while (n >= 4)
			{
				if (lookup[s[p++]] | lookup[s[p++]] | lookup[s[p++]] | lookup[s[p++]]) return true;
				n -= 4;
			}
			// tail
			while (n > 0)
			{
				if (lookup[s[p++]]) return true;
				--n;
			}
			return false;
#else
			// Version "unsafe" (la plus performante)
			fixed (char* p = s)
			{
				var lookup = EscapingLookupTable;
				int n = s.Length;
				char* ptr = p;

				// loop unrolling (4 chars = 8 bytes)
				while (n >= 4)
				{
					if (lookup[*(ptr)] | lookup[*(ptr + 1)] | lookup[*(ptr + 2)] | lookup[*(ptr + 3)]) return true;
					ptr += 4;
					n -= 4;
				}
				// tail
				while (n-- > 0)
				{
					if (lookup[*ptr++]) return true;
				}
				return false;
			}
#endif
		}

		/// <summary>Encode une chaîne en JSON</summary>
		/// <param name="text">Chaîne à encoder</param>
		/// <returns>'null', '""', '"foo"', '"\""', '"\u0000"', ...</returns>
		/// <remarks>Chaîne correctement encodée, avec les guillemets ou "null" si text==null</remarks>
		/// <example>EncodeJsonString("foo") => "\"foo\""</example>
		public static string Encode(string? text)
		{
			// on évacue tout de suite les cas faciles
			if (text == null)
			{ // => null
				return "null";
			}
			if (text.Length == 0)
			{ // => ""
				return "\"\"";
			} // -> premiere passe pour voir s'il y a des caractères a remplacer..

			if (NeedsEscaping(text))
			{ // il va falloir escaper la string!
				return EncodeSlow(text);
			}

			// rien a modifier, retourne la chaîne initiale (fast, no memory used)
			return string.Concat("\"", text, "\"");
		}

		public static string EncodeSlow(string text)
		{
			// note: on estime a 6 caracs l'overhead typique d'un encoding (ou deux ou trois \", ou un \uXXXX)
			var sb = StringBuilderCache.Acquire(checked(text.Length + 2 + 6));
			return StringBuilderCache.GetStringAndRelease(AppendSlow(sb, text, true));
		}

		/// <summary>Encode un texte JSON (en traitant chaque caractère)</summary>
		/// <param name="sb">Buffer où écrire le résultat</param>
		/// <param name="text">Chaîne à encoder</param>
		/// <param name="includeQuotes">Si true, ajoutes les '"' en début et fin du buffer</param>
		/// <returns>Le StringBuilder passé en paramètre (pour chaînage)</returns>
		public static unsafe StringBuilder AppendSlow(StringBuilder sb, string? text, bool includeQuotes)
		{
			if (text == null)
			{ // bypass
				return sb.Append("null");
			}

			// On fait la détection et l'encodage en une seule passe:
			// - On a un curseur sur le dernier carac modifié (initialement à 0)
			// - Tant que tout est clean, on avance le curseur
			// - Dés qu'on trouve un caractère à encoder (ou fin de chaîne):
			//   - On dump le texte clean du curseur à la position courante
			//   - On encode le caractère actuel,
			//   - et on replace le curseur juste derrière
			//
			// Une chaîne entièrement clean arrivera a la fin du for avec le curseur toujours à 0
			//
			// note: on laisse le '/' tel quel, pour différencier entre le '/' (qui serait présent dans la chaîne d'origine), du '\/' qui serait pour les dates

			if (includeQuotes) sb.Append('"');
			int i = 0, last = 0;
			int n = text.Length;
			fixed (char* str = text)
			{
				char* ptr = str;
				while (n-- > 0)
				{
					char c = *ptr++;
					if (c <= '/')
					{ // ASCII 0..47
						if (c == '"')
						{ // " -> \"
							goto escape_backslash;
						}
						else if (c >= ' ')
						{ // ASCII 32..47 : entre l'espace et le '/'
							goto next; // => non modifié
						}
						// ASCII 0..31 : encodé
						// - on escape directement les \n, \r, \t, \b et \f
						// - le reste sera encodé en Unicode \uXXXX
						switch (c)
						{
							case '\n': c = 'n'; goto escape_backslash;
							case '\r': c = 'r'; goto escape_backslash;
							case '\t': c = 't'; goto escape_backslash;
							case '\b': c = 'b'; goto escape_backslash;
							case '\f': c = 'f'; goto escape_backslash;
						}
						// encode en \uXXXX
						goto escape_unicode;
					}
					else if (c == '\\')
					{ // \ -> \\
						goto escape_backslash;
					}
					else if (c >= 0xD800 && (c < 0xE000 || c >= 0xFFFE))
					{ // attention, la plage Unicode D800 - DFFF est utilisée pour encoder les caractères non-BMP (> 0x10000), et FFFE/FFFF correspondent aux BOM UTF-16 (LE/BE)
						goto escape_unicode;
					}
					// => skip
					goto next;

					// caractère encodé avec un backslah => \c
				escape_backslash:
					if (i > last) sb.Append(text, last, i - last);
					last = i + 1;
					sb.Append('\\').Append(c);
					goto next;

					// caractère encodé en Unicode sur 16 bits
				escape_unicode:
					if (i > last) sb.Append(text, last, i - last);
					last = i + 1;
					sb.Append(@"\u").Append(((int)c).ToString("x4", NumberFormatInfo.InvariantInfo)); //TODO: PERF: optimize this!
					goto next;

				next:
					// si on arrive ici, c'est que c'est un caractère normal.
					// on continue à checker jusqu'à la fin ou prochain caractère
					++i;

				} // while
			} // fixed

			if (last == 0)
			{ // toute la chaîne était clean
				sb.Append(text);
			}
			else if (last < text.Length)
			{ // il reste des caractères normaux dans le buffer
				sb.Append(text, last, text.Length - last);
			}
			return includeQuotes ? sb.Append('"') : sb;
		}

		/// <summary>Encode une chaîne en JSON, et append le résultat à un StringBuilder</summary>
		/// <param name="sb">Buffer où écrire le résultat</param>
		/// <param name="text">Chaîne à encoder</param>
		/// <returns>Le StringBuilder passé en paramètre (pour chaînage)</returns>
		/// <remarks>Note: Ajoute "null" si text==null && includeQuotes==true</remarks>
		public static StringBuilder Append(StringBuilder sb, string? text)
		{
			if (text == null)
			{ // null -> "null"
				return sb.Append("null");
			}
			if (text.Length == 0)
			{ // chaîne vide -> ""
				return sb.Append("\"\"");
			}
			if (!JsonEncoding.NeedsEscaping(text))
			{ // chaîne propre
				return sb.Append('"').Append(text).Append('"');
			}
			// chaîne qui nécessite (a priori) un encoding
			return AppendSlow(sb, text, true);
		}

	}

	/// <summary>Very basic (and slow!) JSON text builder</summary>
	/// <remarks>Should be used for very small and infrequent JSON needs, when you don't want to reference a full JSON serializer</remarks>
	[PublicAPI]
	public sealed class SimpleJsonBuilder
	{
		internal enum Context
		{
			Top = 0,
			Object,
			Array
		}

		public struct State
		{
			internal int Index;
			internal Context Context;
		}

		public readonly StringBuilder Buffer;
		private State Current;

		public SimpleJsonBuilder(StringBuilder? buffer = null)
		{
			this.Buffer = buffer ?? new StringBuilder();
		}

		public State BeginObject()
		{
			this.Buffer.Append('{');
			var state = this.Current;
			this.Current = new State { Context = Context.Object };
			return state;
		}

		public void EndObject(State state)
		{
			if (this.Current.Context != Context.Object) throw new InvalidOperationException("Should be inside an object");
			this.Buffer.Append(this.Current.Index == 0 ? "}" : " }");
			this.Current = state;
		}

		public void WriteField(string field)
		{
			if (this.Current.Context != Context.Object) throw new InvalidOperationException("Must be inside an object");
			this.Buffer.Append(this.Current.Index++ > 0 ? ", \"" : "\"").Append(field).Append("\": ");
		}

		public void Add(string field, string value)
		{
			var sb = this.Buffer;
			WriteField(field);
			JsonEncoding.Append(sb, value);
		}

		public void Add(string field, bool value)
		{
			WriteField(field);
			this.Buffer.Append(value ? "true" : "false");
		}

		public void Add(string field, int value)
		{
			WriteField(field);
			this.Buffer.Append(StringConverters.ToString(value));
		}

		public void Add(string field, long value)
		{
			WriteField(field);
			this.Buffer.Append(StringConverters.ToString(value));
		}

		public void Add(string field, float value)
		{
			WriteField(field);
			this.Buffer.Append(StringConverters.ToString(value));
		}

		public void Add(string field, double value)
		{
			WriteField(field);
			this.Buffer.Append(StringConverters.ToString(value));
		}

		public void Add(string field, Guid value)
		{
			WriteField(field);
			this.Buffer.Append('"').Append(value == Guid.Empty ? string.Empty : value.ToString()).Append('"');
		}

		public State BeginArray()
		{
			this.Buffer.Append('[');
			var state = this.Current;
			this.Current = new State { Context = Context.Array };
			return state;
		}

		public void EndArray(State state)
		{
			if (this.Current.Context != Context.Array) throw new InvalidOperationException("Should be inside an array");
			this.Buffer.Append(this.Current.Index == 0 ? "]" : " ]");
			this.Current = state;
		}

		public void WriteArraySeparator()
		{
			if (this.Current.Context != Context.Array) throw new InvalidOperationException("Should be inside an array");
			if (this.Current.Index++ > 0) this.Buffer.Append(", ");
		}

		public void Add(string value)
		{
			WriteArraySeparator();
			JsonEncoding.Append(this.Buffer, value);
		}

		public void Add(bool value)
		{
			WriteArraySeparator();
			this.Buffer.Append(value ? "true" : "false");
		}

		public void Add(int value)
		{
			WriteArraySeparator();
			this.Buffer.Append(StringConverters.ToString(value));
		}

		public void Add(long value)
		{
			WriteArraySeparator();
			this.Buffer.Append(StringConverters.ToString(value));
		}

		public void Add(float value)
		{
			WriteArraySeparator();
			this.Buffer.Append(StringConverters.ToString(value));
		}

		public void Add(double value)
		{
			WriteArraySeparator();
			this.Buffer.Append(StringConverters.ToString(value));
		}

		public void Add(Guid value)
		{
			WriteArraySeparator();
			this.Buffer.Append('"').Append(value == Guid.Empty ? string.Empty : value.ToString()).Append('"');
		}

		public override string ToString()
		{
			return this.Buffer.ToString();
		}
	}

}
