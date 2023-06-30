#region Copyright (c) 2005-2023 Doxense SAS
// All rights reserved.
// 
// Redistribution and use in source and binary forms, with or without
// modification, are permitted provided that the following conditions are met:
// 	* Redistributions of source code must retain the above copyright
// 	  notice, this list of conditions and the following disclaimer.
// 	* Redistributions in binary form must reproduce the above copyright
// 	  notice, this list of conditions and the following disclaimer in the
// 	  documentation and/or other materials provided with the distribution.
// 	* Neither the name of Doxense nor the
// 	  names of its contributors may be used to endorse or promote products
// 	  derived from this software without specific prior written permission.
// 
// THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS "AS IS" AND
// ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED
// WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE
// DISCLAIMED. IN NO EVENT SHALL DOXENSE BE LIABLE FOR ANY
// DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES
// (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES;
// LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND
// ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT
// (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS
// SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
#endregion

namespace Doxense.Serialization.Json
{
	using System;
	using System.Globalization;
	using System.IO;
	using System.Runtime.CompilerServices;
	using System.Text;
	using Doxense.Diagnostics.Contracts;
	using Doxense.Mathematics;
	using Doxense.Web;
	using JetBrains.Annotations;

	public static class CrystalJsonFormatter
	{

		internal static readonly UTF8Encoding Utf8NoBom = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);

		/// <summary>Indique si le buffer contient un BOM UTF-8 à l'offset indiqué</summary>
		internal static bool IsUtf8Bom(byte[] buffer, int offset, int count)
		{
			return count >= 3 && buffer[offset] == 0xEF && buffer[offset + 1] == 0xBB && buffer[offset + 2] == 0xBF;
		}

		/// <summary>Décode un buffer contenant du JSON encodé en UTF-8</summary>
		/// <remarks>Skip automatiquement le BOM si présent</remarks>
		internal static string ReadUtf8String(byte[]? buffer, int offset, int count)
		{
			if (buffer == null || count == 0)
			{
				return string.Empty;
			}
			if (IsUtf8Bom(buffer, offset, count))
			{ // il faut faire sauter le prefix UTF-8 !
				return count == 3 ? string.Empty : Encoding.UTF8.GetString(buffer, offset + 3, count - 3);
			}
			return Encoding.UTF8.GetString(buffer, offset, count);
		}


		#region Formatting

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		internal static string NumberToString(byte value)
		{
			return JsonNumber.GetCachedSmallNumber(value).Literal;
		}

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		internal static string NumberToString(int value)
		{
			return value <= JsonNumber.CACHED_SIGNED_MAX & value >= JsonNumber.CACHED_SIGNED_MIN
				? JsonNumber.GetCachedSmallNumber(value).Literal
				: value.ToString(NumberFormatInfo.InvariantInfo);
		}

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		internal static string NumberToString(uint value)
		{
			return value <= JsonNumber.CACHED_SIGNED_MAX
				? JsonNumber.GetCachedSmallNumber(unchecked((int) value)).Literal
				: value.ToString(NumberFormatInfo.InvariantInfo);
		}

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		internal static string NumberToString(long value)
		{
			return value <= JsonNumber.CACHED_SIGNED_MAX & value >= JsonNumber.CACHED_SIGNED_MIN
				? JsonNumber.GetCachedSmallNumber(unchecked((int) value)).Literal
				: value.ToString(NumberFormatInfo.InvariantInfo);
		}

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		internal static string NumberToString(ulong value)
		{
			return value <= JsonNumber.CACHED_SIGNED_MAX
				? JsonNumber.GetCachedSmallNumber(unchecked((int) value)).Literal
				: value.ToString(NumberFormatInfo.InvariantInfo);
		}

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		internal static string NumberToString(float value)
		{
			return value.ToString("R", NumberFormatInfo.InvariantInfo);
		}

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		internal static string NumberToString(double value)
		{
#if ENABLE_GRISU3_STRING_CONVERTER
			return Doxense.Mathematics.FastDtoa.FormatDouble(value);
#else
			return value.ToString("R", NumberFormatInfo.InvariantInfo);
#endif
		}

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		internal static string NumberToString(decimal value)
		{
			return value.ToString(null, NumberFormatInfo.InvariantInfo);
		}

		[MethodImpl(MethodImplOptions.NoInlining)]
		public static void WriteJsonString(TextWriter writer, string? text)
		{
			if (text == null)
			{ // null -> "null"
				writer.Write(JsonTokens.Null);
			}
			else if (text.Length == 0)
			{ // chaine vide -> ""
				writer.Write(JsonTokens.EmptyString);
			}
			else if (!JsonEncoding.NeedsEscaping(text))
			{ // chaine propre
				writer.Write('"');
				writer.Write(text);
				writer.Write('"');
			}
			else
			{ // chaine qui nécessite (a priori) un encoding
				writer.Write(JsonEncoding.AppendSlow(new StringBuilder(), text, true).ToString());
			}
		}


		public static void WriteJavaScriptString(TextWriter writer, string? text)
		{
			if (text == null)
			{ // "null"
				writer.Write(JsonTokens.Null);
			}
			else if (text.Length == 0)
			{ // "''"
				writer.Write(JsonTokens.DoubleQuotes);
			}
			else if (JavaScriptEncoding.IsCleanJavaScript(text))
			{ // "'foo bar'"
				writer.Write('\'');
				writer.Write(text);
				writer.Write('\'');
			}
			else
			{ // "'foo\'bar'"
				writer.Write(JavaScriptEncoding.EncodeSlow(new StringBuilder(), text, includeQuotes: true).ToString());
			}

		}

		/// <summary>Encode une chaîne en JavaSCript</summary>
		/// <param name="text">Chaîne à encoder</param>
		/// <returns>"null", "''", "'foo'", "'\''", "'\u0000'", ...</returns>
		/// <remarks>Chaine correctement encodée, avec les quotes ou "null" si text==null</remarks>
		/// <example>EncodeJavaScriptString("foo") => "'foo'"</example>
		public static string EncodeJavaScriptString(string? text)
		{
			// on évacue tout de suite les cas faciles
			if (text == null)
			{ // => null
				return JsonTokens.Null;
			}
			if (text.Length == 0)
			{ // => ""
				return JsonTokens.EmptyString;
			} // -> premiere passe pour voir s'il y a des caratères a remplacer..
			if (JavaScriptEncoding.IsCleanJavaScript(text))
			{ // rien a modifier, retourne la chaine initiale (fast, no memory used)
				return String.Concat("'", text, "'");
			} // -> deuxieme passe: remplace les caractères invalides (slow, memory used)
			// note: on estime a 6 caracs l'overhead typique d'un encoding (ou deux ou trois \", ou un \uXXXX)
			return JavaScriptEncoding.EncodeSlow(new StringBuilder(), text, includeQuotes: true).ToString();
		}


		private static readonly int[] s_digitPairs = ConstructDigitPairs();

		private static int[] ConstructDigitPairs()
		{
			// génère un tableau de int[] où chaque int contient une paire de digits
			// ex: la pair "42" ie en ASCII '\x34' 'x30' est encodée par l'integer 0x3034

			const string DIGITS_PAIRS =
				"00010203040506070809" +
				"10111213141516171819" +
				"20212223242526272829" +
				"30313233343536373839" +
				"40414243444546474849" +
				"50515253545556575859" +
				"60616263646566676869" +
				"70717273747576777879" +
				"80818283848586878889" +
				"90919293949596979899";

			var pairs = DIGITS_PAIRS.ToCharArray();
			var map = new int[pairs.Length >> 1];
			for (int i = 0; i < pairs.Length; i += 2)
			{
				map[i >> 1] = (int)pairs[i] | ((int)pairs[i + 1] << 16);
			}
			return map;
		}

		internal static void WriteUnsignedIntegerUnsafe(TextWriter output, ulong value, char[] buf)
		{
#if ENABLE_TIMO_STRING_CONVERTER
			// Timo: au lieu d'écrire les digits un par un ... on les écrit deux par deux ! (ohhhoooooooo)
			const int BUFFER_SIZE = 20 + 4; // max size = 20 char, plus une marge de sécurité
			Contract.Debug.Requires(buf.Length >= BUFFER_SIZE); // need 24 chars

			unsafe
			{
				fixed(char* ptr = buf)
				fixed (int* dp = s_digitPairs)
				{
					char* end = ptr + BUFFER_SIZE;
					char* it = end - 2;

					var div = value / 100;
					while (div != 0)
					{
						*((int*)it) = dp[value - (div * 100)];
						value = div;
						it -= 2;
						div = value / 100;
					}
					*((int*)it) = dp[value];
					if (value < 10) it++;

					Contract.Debug.Assert(it >= ptr && it < end);
					output.Write(buf, (int)(it - ptr), (int)(end - it));
				}
			}
#else
			int p = buf.Length - 1;
			do
			{
				buf[p--] = (char)(48 + (value % 10));
				value /= 10;
			}
			while (value > 0);
			++p;
			int len = buf.Length - p;
			output.Write(buf, p, len);
#endif
		}

		internal static void WriteSignedIntegerUnsafe(TextWriter output, long value, char[] buf)
		{
#if ENABLE_TIMO_STRING_CONVERTER
			// Timo: au lieu d'écrire les digits un par un ... on les écrit deux par deux ! (ohhhoooooooo)
			const int BUFFER_SIZE = 20 + 4; // max size = 20 char, plus une marge de sécurité
			Contract.Debug.Requires(buf.Length >= BUFFER_SIZE); // need 24 chars

			unsafe
			{
				fixed(char* ptr = buf)
				fixed (int* dp = s_digitPairs)
				{
					char* end = ptr + BUFFER_SIZE;
					char* it = end - 2;

					if (value >= 0)
					{
						var div = value / 100;
						while (div != 0)
						{
							*((int*)it) = dp[value - (div * 100)];
							value = div;
							it -= 2;
							div = value / 100;
						}
						*((int*)it) = dp[value];
						if (value < 10) it++;
					}
					else
					{
						var div = value / 100;
						while (div != 0)
						{
							*((int*)it) = dp[-(value - (div * 100))];
							value = div;
							it -= 2;
							div = value / 100;
						}
						*((int*)it) = dp[-value];
						if (value <= -10) it--;
						*it = '-';
					}
					Contract.Debug.Assert(it >= ptr && it < end);
					output.Write(buf, (int)(it - ptr), (int)(end - it));
				}
			}
#else
			if (value == long.MinValue)
			{ // note: on doit gérer le cas de long.MinValue à part, car sa valeur absolue ne rentrerait pas dans un long !
				output.Write(JsonTokens.LongMinValue);
				return;
			}

			bool neg = value < 0;
			value = Math.Abs(value);
			int p = buf.Length - 1;
			do
			{
				buf[p--] = (char)(48 + (value % 10));
				value /= 10;
			}
			while (value > 0);
			if (neg) buf[p] = '-'; else ++p;
			int len = buf.Length - p;
			output.Write(buf, p, len);
#endif
		}

		internal static void WriteFixedIntegerWithDecimalPartUnsafe(TextWriter output, long integer, long decimals, int digits, char[] buf)
		{
			// on a un nombre décimale décomposé en deux partie: la partie entière, et N digits de la partie décimale:
			//
			// Le nombre X est décomposé en (INTEGER, DECIMALS, DIGITS) tel que X = INTEGER + (DECIMALS / 10^DIGITS)

			//                   <-- 'DIGITS' -->
			// [  INTEGER  ] '.' [000...DECIMALS]

			// Quelques exemples:
			// - (integer: 123, dec: 456, digits: 3) => "123.456"
			// - (integer: 123, dec: 456, digits: 4) => "123.0456"
			// - (integer: 123, dec: 456, digits: 5) => "123.00456"
			// - (integer: 123, dec:   1, digits: 3) => "123.001" // on rajoute les '0' entre le '.' et le premier digit non-0 de la partie décimale!
			// - (integer: 123, dec:  10, digits: 3) => "123.01"  // on tronque les derniers '0' de la partie décimale
			// - (integer: 123, dec:   0, digits: 3) => "123"     // ici on omet complètement la partie décimale si 0

			int len = buf.Length;
			int p = buf.Length - 1;

			// partie décimale (si nécessaire)
			long value = decimals;

			if (value != 0 && digits != 0)
			{
				bool allZero = true; // set à false dés qu'on trouve un digit non-zero
				for (int i = 0; i < digits; i++)
				{
					int d = (int) (value % 10);
					buf[p--] = (char) ('0' + d);
					value /= 10;
					if (d == 0 && allZero)
					{ // pas encore de non-0, on tronque
						len--;
					}
					else
					{ // non-zero digit
						allZero = false;
					}
				}
				buf[p--] = '.';
			}

			// partie entière
			value = integer;
			bool neg = value < 0;
			value = Math.Abs(value);
			do
			{
				buf[p--] = (char) (48 + (value % 10));
				value /= 10;
			}
			while (value > 0);

			if (neg) buf[p] = '-';
			else ++p;

			output.Write(buf, p, len - p);
		}


		private static readonly NumberFormatInfo NFINV = NumberFormatInfo.InvariantInfo;

		internal static void WriteSingleUnsafe(TextWriter output, float value, char[] buf, CrystalJsonSettings.FloatFormat format)
		{
			if (value == default)
			{ // le plus courant (objets vides)
				output.Write(JsonTokens.Zero); //.DecimalZero);
				return;
			}

			if (float.IsNaN(value))
			{ // NaN dépend de la configuration
				output.Write(GetNaNToken(format)); // "NaN"
				return;
			}

			if (float.IsInfinity(value))
			{ // cas spécial pour +/- Infinity
				output.Write(value > 0 ? GetPositiveInfinityToken(format) : GetNegativeInfinityToken(format));
				return;
			}

			long l = (long)value;
			if (l == value)
			{
				WriteSignedIntegerUnsafe(output, l, buf);
			}
			else
			{
				output.Write(value.ToString("R", NFINV));
			}
		}

		internal static void WriteDoubleUnsafe(TextWriter output, double value, char[] buf, CrystalJsonSettings.FloatFormat format)
		{
#if ENABLE_GRISU3_STRING_CONVERTER
			long l = (long)value;
			if (l == value)
			{
				WriteSignedIntegerUnsafe(output, l, buf);
			}
			else
			{
				int n = Doxense.Mathematics.FastDtoa.FormatDouble(value, buf, 0);
				output.Write(buf, 0, n);
			}
#else
			if (value == default)
			{ // le plus courant (objets vides)
				output.Write(JsonTokens.Zero); //.DecimalZero);
				return;
			}

			// Gestion des valeurs spéciales (NaN, Infinity, ...)
			var dd = new DiyDouble(value);
			if (dd.IsSpecial)
			{
				if (dd.IsNaN)
				{ // NaN dépend de la configuration
					output.Write(GetNaNToken(format)); // "NaN"
					return;
				}
				if (dd.IsInfinite)
				{ // cas spécial pour +/- Infinity
					output.Write(value > 0.0 ? GetPositiveInfinityToken(format) : GetNegativeInfinityToken(format));
					return;
				}
			}
			else
			{
				long l = (long) value;
				if (l == value)
				{ // integer shortcut
					WriteSignedIntegerUnsafe(output, l, buf);
					return;
				}
			}

			output.Write(value.ToString("R", NFINV));
#endif
		}

		internal static string GetNaNToken(CrystalJsonSettings.FloatFormat format)
		{
			switch (format)
			{
				case CrystalJsonSettings.FloatFormat.Default:
				case CrystalJsonSettings.FloatFormat.Symbol:
					return JsonTokens.SymbolNaN;
				case CrystalJsonSettings.FloatFormat.String:
					return JsonTokens.StringNaN;
				case CrystalJsonSettings.FloatFormat.Null:
					return JsonTokens.Null;
				case CrystalJsonSettings.FloatFormat.JavaScript:
					return JsonTokens.JavaScriptNaN;
				default:
					throw new ArgumentException(nameof(format));
			}
		}

		internal static string GetPositiveInfinityToken(CrystalJsonSettings.FloatFormat format)
		{
			switch (format)
			{
				case CrystalJsonSettings.FloatFormat.Default:
				case CrystalJsonSettings.FloatFormat.Symbol:
					return JsonTokens.SymbolInfinityPos;
				case CrystalJsonSettings.FloatFormat.String:
					return JsonTokens.StringInfinityPos;
				case CrystalJsonSettings.FloatFormat.Null:
					return JsonTokens.Null;
				case CrystalJsonSettings.FloatFormat.JavaScript:
					return JsonTokens.JavaScriptInfinityPos;
				default:
					throw new ArgumentException(nameof(format));
			}
		}

		internal static string GetNegativeInfinityToken(CrystalJsonSettings.FloatFormat format)
		{
			switch (format)
			{
				case CrystalJsonSettings.FloatFormat.Default:
				case CrystalJsonSettings.FloatFormat.Symbol:
					return JsonTokens.SymbolInfinityNeg;
				case CrystalJsonSettings.FloatFormat.String:
					return JsonTokens.StringInfinityNeg;
				case CrystalJsonSettings.FloatFormat.Null:
					return JsonTokens.Null;
				case CrystalJsonSettings.FloatFormat.JavaScript:
					return JsonTokens.JavaScriptInfinityNeg;
				default:
					throw new ArgumentException(nameof(format));
			}
		}

		private static readonly int[] DaysToMonth365 = new int[] { 0, 31, 59, 90, 120, 151, 181, 212, 243, 273, 304, 334, 365 };
		private static readonly int[] DaysToMonth366 = new int[] { 0, 31, 60, 91, 121, 152, 182, 213, 244, 274, 305, 335, 366 };

		private const int ISO8601_MAX_FORMATTED_SIZE = 35; // Avec quotes + TimeZone!

		internal static int FormatIso8601DateTime(char[] output, DateTime date, DateTimeKind kind, TimeSpan? utcOffset, char quotes = '\0')
		{
			// on va utiliser entre 28 et 33 (+2 avec les quotes) caractères dans le buffer
			if (output.Length < ISO8601_MAX_FORMATTED_SIZE) ThrowHelper.ThrowArgumentException(nameof(output), "Output buffer size is too small");

			GetDateParts(date.Ticks, out var year, out var month, out var day, out var hour, out var min, out var sec, out var millis);

			unsafe
			{
				fixed (char* buf = output)
				{
					char* ptr = buf;

					if (quotes != '\0') *ptr++ = quotes;

					ptr += FormatDatePart(ptr, year, month, day);
					*ptr++ = 'T';
					ptr += FormatTimePart(ptr, hour, min, sec, millis);

					if (kind == DateTimeKind.Utc)
					{ // "Z"
						*ptr++ = 'Z';
					}
					else if (utcOffset.HasValue)
					{
						ptr += FormatTimeZoneOffset(ptr, utcOffset.Value, false);
					}
					else if (kind == DateTimeKind.Local)
					{
						ptr += FormatTimeZoneOffset(ptr, TimeZoneInfo.Local.GetUtcOffset(date), true);
					}

					if (quotes != '\0') *ptr++ = quotes;

					return (int)(ptr - buf);
				}
			}
		}

		public static string ToIso8601String(DateTime date)
		{
			if (date == DateTime.MinValue) return String.Empty;

			var buf = new char[ISO8601_MAX_FORMATTED_SIZE];
			int n = FormatIso8601DateTime(buf, date, date.Kind, null, '\0');
			return new string(buf, 0, n);
		}

		public static string ToIso8601String(DateTimeOffset date)
		{
			if (date == DateTime.MinValue) return String.Empty;

			var buf = new char[ISO8601_MAX_FORMATTED_SIZE];
			int n = FormatIso8601DateTime(buf, date.DateTime, DateTimeKind.Local, date.Offset, '\0');
			return new string(buf, 0, n);
		}

		private static unsafe int FormatDatePart(char* ptr, int year, int month, int day)
		{
			Paranoid.Requires(ptr != null && year >= 0 && month >= 1 && month <= 12 && day >= 1 && day <= 31);

			// Year
			ptr[3] = (char)(48 + (year % 10)); year /= 10;
			ptr[2] = (char)(48 + (year % 10)); year /= 10;
			ptr[1] = (char)(48 + (year % 10));
			ptr[0] = (char)(48 + (year / 10));
			ptr += 4;

			// Month
			ptr[0] = '-';
			ptr[1] = (char)(48 + (month / 10));
			ptr[2] = (char)(48 + (month % 10));
			ptr += 3;

			// Day
			ptr[0] = '-';
			ptr[1] = (char)(48 + (day / 10));
			ptr[2] = (char)(48 + (day % 10));

			return 10;
		}

		private static unsafe int FormatTimePart(char* ptr, int hour, int min, int sec, int ticks)
		{
			Paranoid.Requires(ptr != null && hour >= 0 && hour < 24 && min >= 0 && min < 60 && sec >= 0 && sec < 60 && ticks >= 0 && ticks < TimeSpan.TicksPerSecond);

			ptr[0] = (char)(48 + (hour / 10));
			ptr[1] = (char)(48 + (hour % 10));

			// Minutes
			ptr[2] = ':';
			ptr[3] = (char)(48 + (min / 10));
			ptr[4] = (char)(48 + (min % 10));

			// Seconds
			ptr[5] = ':';
			ptr[6] = (char)(48 + (sec / 10));
			ptr[7] = (char)(48 + (sec % 10));

			// Milliseconds
			// (sur 7 digits)
			if (ticks > 0)
			{
				ptr += 8;
				ptr[0] = '.';
				ptr[7] = (char)(48 + (ticks % 10)); ticks /= 10;
				ptr[6] = (char)(48 + (ticks % 10)); ticks /= 10;
				ptr[5] = (char)(48 + (ticks % 10)); ticks /= 10;
				ptr[4] = (char)(48 + (ticks % 10)); ticks /= 10;
				ptr[3] = (char)(48 + (ticks % 10)); ticks /= 10;
				ptr[2] = (char)(48 + (ticks % 10));
				ptr[1] = (char)(48 + (ticks / 10));

				return 16;
			}

			return 8;
		}

		private static unsafe int FormatTimeZoneOffset(char* ptr, TimeSpan utcOffset, bool forceLocal)
		{
			Paranoid.Requires(ptr != null);

			int min = (int)(utcOffset.Ticks / TimeSpan.TicksPerMinute);

			// special case: on affiche quand meme 'Z' pour les DTO avec l'offset GMT, car on ne peut pas les distinguer de DTO version "UTC"
			// => si un serveur tourne qqpart du coté de Greenwith, on confondra les heures de type locales avec les heures UTC, mais ce n'est pas très grave au final...
			if (min == 0 && !forceLocal)
			{ // "Z"
				*ptr = 'Z';
				return 1;
			}
			else
			{ // "+HH:MM"
				ptr[0] = min >= 0 ? '+' : '-';

				min = Math.Abs(min);
				int hour = min / 60;
				min %= 60;

				ptr[1] = (char)(48 + (hour / 10));
				ptr[2] = (char)(48 + (hour % 10));
				ptr[3] = ':';
				ptr[4] = (char)(48 + (min / 10));
				ptr[5] = (char)(48 + (min % 10));
				return 6;
			}
		}

		public static void GetDateParts(long ticks, out int year, out int month, out int day, out int hour, out int minute, out int second, out int remainder)
		{
			// Version modifiée de DateTime.GetDatePart(..) qui retourne les 3 valeurs en une seule passe
			// comments et noms des variables obtenues via Rotor...

			// n = number of days since 1/1/0001
			int n = (int)(ticks / (TimeSpan.TicksPerSecond * 86400));
			// y400 = number of whole 400-year periods since 1/1/0001
			int y400 = n / 146097;
			// n = day number within 400-year period
			n -= y400 * 146097;
			// y100 = number of whole 100-year periods within 400-year period
			int y100 = n / 36524;
			// Last 100-year period has an extra day, so decrement result if 4
			if (y100 == 4) y100 = 3;

			// n = day number within 100-year period
			n -= y100 * 36524;
			// y4 = number of whole 4-year periods within 100-year period
			int y4 = n / 1461;
			// n = day number within 4-year period
			n -= y4 * 1461;
			// y1 = number of whole years within 4-year period
			int y1 = n / 365;
			// Last year has an extra day, so decrement result if 4
			if (y1 == 4) y1 = 3;

			year = y400 * 400 + y100 * 100 + y4 * 4 + y1 + 1;

			// n = day number within year
			n -= y1 * 365;
			// note: si on veut le dayOfYear, il faut utiliser "n + 1"

			// Leap year calculation looks different from IsLeapYear since y1, y4,
			// and y100 are relative to year 1, not year 0
			var days = ((y1 == 3) && ((y4 != 24) || (y100 == 3))) ? DaysToMonth366 : DaysToMonth365;
			// All months have less than 32 days, so n >> 5 is a good conservative
			// estimate for the month
			int m = (n >> 5) + 1;
			// m = 1-based month number
			while (n >= days[m]) m++;
			month = m;
			// Return 1-based day-of-month
			day = (n - days[m - 1]) + 1;

			hour = (int)((ticks / 36000000000L) % 24L);
			minute = (int)((ticks / 600000000L) % 60L);
			second = (int)((ticks / 10000000L) % 60L);
			remainder = (int)(ticks % TimeSpan.TicksPerSecond);
		}

		#endregion

	}

}
