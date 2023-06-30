#region Copyright (c) 2005-2023 Doxense SAS
//
// All rights are reserved. Reproduction or transmission in whole or in part, in
// any form or by any means, electronic, mechanical or otherwise, is prohibited
// without the prior written consent of the copyright owner.
//
#endregion

namespace Doxense.Serialization.Xml
{
	using System;
	using System.Runtime.CompilerServices;
	using System.Text;
	using Doxense.Text;
	using JetBrains.Annotations;

	/// <summary>XPATH Codec</summary>
	public static class XPathEncoder
	{
		/// <summary>Return the empty string corresponding to the specified quote character</summary>
		[Pure]
		private static string EmptyString(char quote)
		{
			switch (quote)
			{
				case '"':  return "\"\"";
				case '\'': return "''";
				case '\0': return string.Empty;
				default:   return new string(quote, 2);
			}
		}

		/// <summary>Return the number of characters required to fully encode the specified string into an XPATH string literal (including escaping characters, and the surrounding quotes)</summary>
		/// <returns>Can be used to pre-allocate the buffer capacity required to fully encode a string, before calling one of the encode helpers</returns>
		/// <remarks>
		/// Please note that the <c>null</c> string is encoded as "null" and so required 4 characters.
		/// </remarks>
		[Pure]
		public static int GetEncodedLength(string literal)
		{
			if (literal == null) return 4; // "null"
			if (literal.Length == 0) return 2;

			int count = 2;
			foreach (var c in literal)
			{
				count += (c == '\'' | c == '"' | c == '\\') ? 2 : 1;
			}
			return count;
		}

		/// <summary>Return the number of characters required to fully a string literal in the worst possible</summary>
		/// <returns>In the worst case, the string requires <code>2 + LENGTH * 2</code> characters to encode.</returns>
		public static int GetMaxEncodedLength(int count)
		{
			// worst case required each character to be escape, plus 2 surrounding quotes
			return checked(count * 2 + 2);
		}

		/// <summary>Encode a string literal for inclusion in a XPATH query</summary>
		/// <remarks>
		/// Encode les <c>'</c>, <c>"</c> et <c>\</c> en ajoutant un <c>\</c> devant chaque occurence (ie: <c>'</c> => <c>\'</c>).
		/// Rajoute optionellement le caract�re <paramref name="quote"/> en d�but et fin du token.
		/// Si <paramref name="literal"/> est null, retourne soit <code>"null"</code> (si <paramref name="quote"/> est sp�cifi�), soit chaine vide (si �gal � <c>'\0'</c>)
		/// </remarks>
		/// <example>
		/// - EncodeLiteral("Hello World") => `Hello World`
		/// - EncodeLiteral("Hello World", quote: '"') => `"Hello World"`
		/// - EncodeLiteral("Hello World", quote: '\'') => `'Hello World'`
		/// - EncodeLiteral("A'B"C&amp;D\\E", quote: '\'') => `'A\'B\"C&amp;D\\E'`
		/// </example>
		[Pure]
		public static string EncodeLiteral(string literal, char quote = '"')
		{
			if (literal == null)
			{ // null  string
				return quote != '\0' ? "null" : string.Empty;
			}
			if (literal.Length == 0)
			{ // empty string
				return EmptyString(quote);
			}

			var sb = StringBuilderCache.Acquire(literal.Length + 4);
			if (quote != '\0') sb.Append(quote);
			foreach (var c in literal)
			{
				if (c == '\'' | c == '"' | c == '\\')
				{
					sb.Append('\\');
				}
				sb.Append(c);
			}
			if (quote != '\0') sb.Append(quote);
			return StringBuilderCache.GetStringAndRelease(sb);
		}

		/// <summary>Encode a string literal for inclusion in a XPATH query</summary>
		/// <remarks>
		/// Encode les <c>'</c>, <c>"</c> et <c>\</c> en ajoutant un <c>\</c> devant chaque occurence (ie: <c>'</c> => <c>\'</c>).
		/// Rajoute optionellement le caract�re <paramref name="quote"/> en d�but et fin du token.
		/// Si <paramref name="literal"/> est null, retourne soit <code>"null"</code> (si <paramref name="quote"/> est sp�cifi�), soit chaine vide (si �gal � <c>'\0'</c>)
		/// </remarks>
		/// <example>
		/// - EncodeLiteral("Hello World") => `Hello World`
		/// - EncodeLiteral("Hello World", quote: '"') => `"Hello World"`
		/// - EncodeLiteral("Hello World", quote: '\'') => `'Hello World'`
		/// - EncodeLiteral("A'B"C&amp;D\\E", quote: '\'') => `'A\'B\"C&amp;D\\E'`
		/// </example>
		[Pure]
		public static string EncodeLiteral(ReadOnlySpan<char> literal, char quote = '"')
		{
			if (literal.Length == 0)
			{ // empty string
				return EmptyString(quote);
			}

			var sb = StringBuilderCache.Acquire(literal.Length + 4);
			if (quote != '\0') sb.Append(quote);
			foreach (var c in literal)
			{
				if (c == '\'' | c == '"' | c == '\\')
				{
					sb.Append('\\');
				}
				sb.Append(c);
			}
			if (quote != '\0') sb.Append(quote);
			return StringBuilderCache.GetStringAndRelease(sb);
		}

		/// <summary>Encode a boolean into an XPATH literal</summary>
		/// <returns>Either <code>"true"</code> or <code>"false"</code></returns>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static string EncodeLiteral(bool value)
		{
			return value ? "true" : "false";
		}

		/// <summary>Encode a number into an XPATH literal</summary>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static string EncodeLiteral(int value)
		{
			return StringConverters.ToString(value);
		}

		/// <summary>Encode a number into an XPATH literal</summary>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static string EncodeLiteral(long value)
		{
			return StringConverters.ToString(value);
		}

		/// <summary>Encode a number into an XPATH literal</summary>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static string EncodeLiteral(float value)
		{
			return StringConverters.ToString(value);
		}

		/// <summary>Encode a number into an XPATH literal</summary>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static string EncodeLiteral(double value)
		{
			return StringConverters.ToString(value);
		}

		/// <summary>Encode a <see cref="Guid"/> into an XPATH literal</summary>
		/// <remarks>The <see cref="Empty"/> Guid is encoded as the empty string!</remarks>
		[Pure]
		public static string EncodeLiteral(Guid guid, char quote = '"')
		{
			var lit = guid != Guid.Empty ? guid.ToString() : "";
			switch (quote)
			{
				case '"':  return "\"" + lit + "\"";
				case '\'': return "'" + lit + "'";
				case '\0': return lit;
				default:   return StringBuilderCache.GetStringAndRelease(StringBuilderCache.Acquire(1 + 36 + 1).Append(quote).Append(lit).Append(quote));
			}
		}

		/// <summary>Append a string literal for inclusion in a XPATH query</summary>
		public static StringBuilder AppendLiteral(StringBuilder sb, string literal, char quote = '"')
		{
			if (literal == null)
			{
				if (quote != '\0') sb.Append("null");
				return sb;
			}

			if (quote != '\0') sb.Append(quote);
			if (literal.Length > 0)
			{
				foreach (var c in literal)
				{
					if (c == '\'' | c == '"' | c == '\\')
					{
						sb.Append('\\');
					}

					sb.Append(c);
				}
			}
			if (quote != '\0') sb.Append(quote);
			return sb;
		}

		/// <summary>Append a string literal for inclusion in a XPATH query</summary>
		public static StringBuilder AppendLiteral(StringBuilder sb, ReadOnlySpan<char> literal, char quote = '"')
		{
			if (quote != '\0') sb.Append(quote);
			if (literal.Length > 0)
			{
				foreach (var c in literal)
				{
					if (c == '\'' | c == '"' | c == '\\')
					{
						sb.Append('\\');
					}

					sb.Append(c);
				}
			}
			if (quote != '\0') sb.Append(quote);
			return sb;
		}

	}
}
