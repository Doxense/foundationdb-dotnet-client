#region Copyright (c) 2005-2023 Doxense SAS
//
// All rights are reserved. Reproduction or transmission in whole or in part, in
// any form or by any means, electronic, mechanical or otherwise, is prohibited
// without the prior written consent of the copyright owner.
//
#endregion

namespace Doxense.Web
{
	using System;
	using System.Collections.Generic;
	using System.Globalization;
	using System.Text;
	using Doxense.Diagnostics.Contracts;

	public static class JavaScriptEncoding
	{
		public static class Tokens
		{
			public const string Null = "null";
			public const string True = "true";
			public const string False = "false";
			public const string Quote = "'";
			public const string CloseParens = ")";
			public const string BeginDate = "new Date(";
			public const string EmptyString = "''";
			public const string Zero = "0";
			public const string DecimalZero = "0.0";
			public const string DotZero = ".0";
			public const string NaN = "Number.NaN";
			public const string InfinityPos = "Number.POSITIVE_INFINITY";
			public const string InfinityNeg = "Number.NEGATIVE_INFINITY";
		}

		/// <summary>D�termine si le javascript est "clean" (ie: ne n�cessite pas d'encodage)</summary>
		/// <param name="s">Cha�ne � v�rifier</param>
		/// <returns>True si tt les caract�res sont valides</returns>
		public static unsafe bool IsCleanJavaScript(string s)
		{
			int n = s.Length;
			fixed (char* p = s)
			{
				char* ptr = p;
				while (n > 0)
				{
					char c = *ptr++;
					if (!((c >= 'a' && c <= 'z') || c == ' ' || (c >= 'A' && c <= 'Z') || (c >= '0' && c <= '9') || c == '.' || c == ',' || c == '-' || c == '_' || c == ':' || c == '/' || (c >= 880 && c <= 2047) || (c >= 12352 && c <= 12591)))
					{ // pas autoris�!
						return false;
					}
					--n;
				}
			}
			return true;
		}

		/// <summary>Encode un texte Javascript (en traitant chaque caract�re)</summary>
		/// <param name="sb">Buffer o� �crire le r�sultat</param>
		/// <param name="s">Cha�ne � encoder</param>
		/// <param name="includeQuotes"></param>
		public static unsafe StringBuilder EncodeSlow(StringBuilder sb, string s, bool includeQuotes)
		{
			int n = s.Length;
			if (includeQuotes) sb.Append('\'');
#if MONO
			fixed(char* p=s.ToCharArray())
#else
			fixed (char* p = s) // JustCode warning (compile sous MS.NET)
#endif
			{
				char* ptr = p;
				while (n-- > 0)
				{
					char c = *ptr++;
					if ((c >= 'a' && c <= 'z') || c == ' ' || (c >= 'A' && c <= 'Z') || (c >= '0' && c <= '9') || c == '.' || c == ',' || c == '-' || c == '_' || c == ':' || c == '/' || (c >= 880 && c <= 2047) || (c >= 12352 && c <= 12591))
						sb.Append(c);
					else if (c == '\n')
						sb.Append(@"\n");
					else if (c == '\r')
						sb.Append(@"\r");
					else if (c == '\t')
						sb.Append(@"\t");
					else if (c > 127)
						sb.Append(@"\u").Append(((int) c).ToString("x4", CultureInfo.InvariantCulture));
					else // pas clean!
						sb.Append(@"\x").Append(((int) c).ToString("x2", CultureInfo.InvariantCulture));
				}
			}
			if (includeQuotes) sb.Append('\'');
			return sb;
		}

		/// <summary>Encode une cha�ne en JavaScript</summary>
		/// <param name="text">Cha�ne � encoder</param>
		/// <param name="includeQuotes">Si true, ajout automatiquement des apostrophes ('...')</param>
		/// <returns>Cha�ne encod�e correctement</returns>
		/// <remarks>Cette m�thode n'alloue pas de m�moire si la cha�ne d'origine est clean</remarks>
		public static string Encode(string text, bool includeQuotes)
		{
			if (text == null) return includeQuotes ? Tokens.Null : String.Empty;
			if (text.Length == 0) return includeQuotes ? Tokens.EmptyString : String.Empty;
			// premiere passe pour voir s'il y a des caract�res a remplacer..
			if (JavaScriptEncoding.IsCleanJavaScript(text)) return includeQuotes ? String.Concat(Tokens.Quote, text, Tokens.Quote) : text; // rien a modifier, retourne la chaine initiale (fast, no memory used)
			// deuxi�me passe: remplace les caract�res invalides (slow, memory used)
			return JavaScriptEncoding.EncodeSlow(new StringBuilder(text.Length + 16), text, includeQuotes).ToString();
		}

		/// <summary>Encode le nom d'une propri�t� d'un objet</summary>
		/// <param name="name">Nom de la propri�t� d'un objet</param>
		/// <returns>Si le nom est "clean", il est �crit tel quel, sinon il est encod�</returns>
		/// <exception cref="System.ArgumentNullException">Si 'name' est null</exception>
		/// <example>
		/// EncodePropertyName("foo") => "foo"
		/// EncodePropertyName("foo bar") => "'foo bar'"
		/// EncodePropertyName("foo'bar") => "'foo\'bar'"
		/// EncodePropertyName("") => "''"
		/// EncodePropertyName(null) => ArgumentNullException
		/// </example>
		public static string EncodePropertyName(string name)
		{
			Contract.NotNull(name);

			if (IsValidIdentifier(name))
				return name;
			else
				return Encode(name, true);
		}

		/// <summary>Ensemble des mots cl�s r�serv�s en JavaScript</summary>
		private static readonly HashSet<string> s_reservedKeywords = new HashSet<string>(
			new[] { "instanceof", "typeof", "break", "do", "new", "var", "case", "else", "return", "void", "catch", "finally", "continue", "for", "switch", "while", "this", "with", "debugger", "function", "throw", "default", "if", "try", "delete", "in" },
			StringComparer.Ordinal // case sensitive! "Typeof" n'est pas r�serv�, mais "typeof" l'est.
		);

		/// <summary>D�termine si un caract�re est valide comme premier caract�re d'un identifiant JavaScript ("identifierStart")</summary>
		private static bool IsValidIdentifierStart(char c)
		{
			/* identifierStart:unicodeLetter | DOLLAR | UNDERSCORE | unicodeEscapeSequence  */

			// ASCII Letter, $ ou '_'
			if ((c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z') || c == '$' || c == '_')
				return true;

			// Sinon, cela doit etre un "Unicode Letter"
			if (c >= 0x80)
			{
				/* any character in the unicode categories:
				   "uppercase letter (Lu)",
				   "lowercase letter (Li)",
				   "titlecase letter (Lt)",
				   "modifier letter (Lm)",
				   "other letter (lo)",
				   "letter number (NI)" */
				switch (char.GetUnicodeCategory(c))
				{
					case UnicodeCategory.LowercaseLetter:
					case UnicodeCategory.UppercaseLetter:
					case UnicodeCategory.TitlecaseLetter:
					case UnicodeCategory.ModifierLetter:
					case UnicodeCategory.OtherLetter:
					case UnicodeCategory.LetterNumber:
						{
							return true;
						}
				}
			}
			return false;
		}

		/// <summary>D�termine si un caract�re est valide comme second caract�re ou suivant d'un identifiant JavaScript ("identifierPart")</summary>
		private static bool IsValidIdentifierPart(char c)
		{
			// ASCII Letter, Digit, $ ou '_'
			if ((c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z') || (c >= '0' && c <= '9') || c == '$' || c == '_')
				return true;

			if (c >= 0x80)
			{
				switch (char.GetUnicodeCategory(c))
				{
					// unicodeLetter
					case UnicodeCategory.LowercaseLetter:
					case UnicodeCategory.UppercaseLetter:
					case UnicodeCategory.TitlecaseLetter:
					case UnicodeCategory.ModifierLetter:
					case UnicodeCategory.OtherLetter:
					case UnicodeCategory.LetterNumber:
					// unicodeDigit
					case UnicodeCategory.DecimalDigitNumber:
					// uniceeCombiningMark
					case UnicodeCategory.NonSpacingMark:
					case UnicodeCategory.SpacingCombiningMark:
					// unicodeConnectorPunctuation
					case UnicodeCategory.ConnectorPunctuation:
						{
							return true;
						}
				}
			}
			return false;
		}

		/// <summary>D�termine rapidement si un nom peut �tre utilis� directement comme nom de propri�t� d'un objet JavaScript, ou s'il doit d'abord �tre encod�</summary>
		/// <param name="name">Nom d'une propri�t� d'un objet JavaScript</param>
		/// <returns>Retourne true si le nom est valide comme identifiant et qu'il ne contient que de l'ASCII, ou false s'il est invalide ou s'il contient de l'Unicode</returns>
		/// <remarks>Cette fonction peut retourner 'false' pour des identifiants tout � fait valide ! Le but ici est de d�terminer RAPIDEMENT s'il est n�cessaire d'encoder un identifiant, pas de le rejeter !</remarks>
		public static bool IsValidIdentifier(string name)
		{
			/* D'apr�s ECMA-262 "ECMAScript Language Specification" section 7.6 :

				Identifier :: 
					IdentifierName but not ReservedWord

				IdentifierName :: 
					IdentifierStart 
					IdentifierName IdentifierPart 

				IdentifierStart :: 
					UnicodeLetter 
					$ 
					_ 
					\ UnicodeEscapeSequence 

				IdentifierPart :: 
					IdentifierStart 
					UnicodeCombiningMark 
					UnicodeDigit 
					UnicodeConnectorPunctuation 
					\ UnicodeEscapeSequence 

				UnicodeLetter 
					any character in the Unicode categories �Uppercase letter (Lu)�, �Lowercase letter (Ll)�, �Titlecase letter (Lt)�, 
					�Modifier letter (Lm)�, �Other letter (Lo)�, or �Letter number (Nl)�. 

				UnicodeCombiningMark 
					any character in the Unicode categories �Non-spacing mark (Mn)� or �Combining spacing mark (Mc)� 

				UnicodeDigit 
					any character in the Unicode category �Decimal number (Nd)� 

				UnicodeConnectorPunctuation 
					any character in the Unicode category �Connector punctuation (Pc)� 

				UnicodeEscapeSequence 
					see 7.8.4. 

				HexDigit :: one of 
					0 1 2 3 4 5 6 7 8 9 a b c d e f A B C D E F
			*/

			if (string.IsNullOrEmpty(name)) return false;

			// Le premier caract�re doit �tre une lettre, un '$' ou un '_'
			if (!IsValidIdentifierStart(name[0]))
				return false;

			// Les autres caract�res autorisent les chiffres et d'autres types de symboles unicode
			int n = name.Length - 1;
			if (n > 0)
			{
				unsafe
				{
					fixed (char* p = name + 1)
					{
						char* ptr = p;
						while (n > 0)
						{
							char c = *ptr++;
							if (!IsValidIdentifierPart(c))
							{ // pas autoris�!
								return false;
							}
							--n;
						}
					}
				}
			}

			// L'identifiant ne doit pas �tre un mot cl� r�serv� ("if", "delete", ...)
			return !s_reservedKeywords.Contains(name);
		}


	}
}
