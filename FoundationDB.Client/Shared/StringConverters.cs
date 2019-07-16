#region BSD License
/* Copyright (c) 2013-2018, Doxense SAS
All rights reserved.

Redistribution and use in source and binary forms, with or without
modification, are permitted provided that the following conditions are met:
	* Redistributions of source code must retain the above copyright
	  notice, this list of conditions and the following disclaimer.
	* Redistributions in binary form must reproduce the above copyright
	  notice, this list of conditions and the following disclaimer in the
	  documentation and/or other materials provided with the distribution.
	* Neither the name of Doxense nor the
	  names of its contributors may be used to endorse or promote products
	  derived from this software without specific prior written permission.

THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS "AS IS" AND
ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED
WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE
DISCLAIMED. IN NO EVENT SHALL <COPYRIGHT HOLDER> BE LIABLE FOR ANY
DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES
(INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES;
LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND
ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT
(INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS
SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
 */
#endregion

#if !USE_SHARED_FRAMEWORK

namespace Doxense.Serialization
{
	using System;
	using System.Globalization;
	using System.Runtime.CompilerServices;
	using Doxense.Diagnostics.Contracts;
	using JetBrains.Annotations;

	internal static class StringConverters
	{
		#region Numbers...

		/// <summary>Lookup table for all small numbers from 0 to 99</summary>
		private static readonly string[] SmallNumbers = new string[100]
		{
			"0", "1", "2", "3", "4", "5", "6", "7", "8", "9",
			"10", "11", "12", "13", "14", "15", "16", "17", "18", "19",
			"20", "21", "22", "23", "24", "25", "26", "27", "28", "29",
			"30", "31", "32", "33", "34", "35", "36", "37", "38", "39",
			"40", "41", "42", "43", "44", "45", "46", "47", "48", "49",
			"50", "51", "52", "53", "54", "55", "56", "57", "58", "59",
			"60", "61", "62", "63", "64", "65", "66", "67", "68", "69",
			"70", "71", "72", "73", "74", "75", "76", "77", "78", "79",
			"80", "81", "82", "83", "84", "85", "86", "87", "88", "89",
			"90", "91", "92", "93", "94", "95", "96", "97", "98", "99",
		};

		/// <summary>Convert an integer into a string</summary>
		[Pure, NotNull]
		public static string ToString(int value)
		{
			var cache = StringConverters.SmallNumbers;
			return value >= 0 && value < cache.Length ? cache[value] : value.ToString(NumberFormatInfo.InvariantInfo);
		}

		/// <summary>Convert an integer into a string</summary>
		[Pure, NotNull]
		public static string ToString(uint value)
		{
			var cache = StringConverters.SmallNumbers;
			return value < cache.Length ? cache[value] : value.ToString(NumberFormatInfo.InvariantInfo);
		}

		/// <summary>Convert an integer into a string</summary>
		[Pure, NotNull]
		public static string ToString(long value)
		{
			var cache = StringConverters.SmallNumbers;
			return value >= 0 && value < cache.Length ? cache[(int) value] : value.ToString(NumberFormatInfo.InvariantInfo);
		}

		/// <summary>Convert an integer into a string</summary>
		[Pure, NotNull]
		public static string ToString(ulong value)
		{
			var cache = StringConverters.SmallNumbers;
			return value < (ulong) cache.Length ? cache[(int) value] : value.ToString(NumberFormatInfo.InvariantInfo);
		}

		/// <summary>Convert a float into a string</summary>
		[Pure, NotNull]
		public static string ToString(float value)
		{
			long x = unchecked((long) value);
			// ReSharper disable once CompareOfFloatsByEqualityOperator
			return x != value
					   ? value.ToString("R", CultureInfo.InvariantCulture)
					   : (x >= 0 && x < StringConverters.SmallNumbers.Length ? StringConverters.SmallNumbers[(int) x] : x.ToString(NumberFormatInfo.InvariantInfo));
		}

		/// <summary>Convert a double into a string</summary>
		[Pure, NotNull]
		public static string ToString(double value)
		{
			long x = unchecked((long)value);
			// ReSharper disable once CompareOfFloatsByEqualityOperator
			return x != value
					   ? value.ToString("R", CultureInfo.InvariantCulture)
					   : (x >= 0 && x < StringConverters.SmallNumbers.Length ? StringConverters.SmallNumbers[(int)x] : x.ToString(NumberFormatInfo.InvariantInfo));
		}

		/// <summary>Convert a string into a boolean</summary>
		/// <param name="value">Text string (ex: "true")</param>
		/// <param name="dflt">Default value if empty or invalid</param>
		/// <returns>Corresponding boolean value (ex: true) or default</returns>
		/// <remarks>The values considered 'true' are "true", "yes", "on", "1".
		/// The values considered 'false' are "no", "off", "0".
		/// The null or empty strings, or any other invalid token will return the default value.
		/// </remarks>
		[Pure]
		public static bool ToBoolean(string value, bool dflt)
		{
			if (string.IsNullOrEmpty(value)) return dflt;
			char c = value[0];
			if (c == 't' || c == 'T') return true;
			if (c == 'f' || c == 'F') return false;
			if (c == 'y' || c == 'Y') return true;
			if (c == 'n' || c == 'N') return false;
			if ((c == 'o' || c == 'O') && value.Length > 1) { c = value[1]; return c == 'n' || c == 'N'; }
			if (c == '1') return true;
			if (c == '0') return false;
			return dflt;
		}

		/// <summary>Convert a string into a boolean</summary>
		/// <param name="value">Text string (ex: "true")</param>
		/// <returns>Corresponding boolean value (ex: true) or null</returns>
		/// <remarks>The values considered 'true' are "true", "yes", "on", "1".
		/// The values considered 'false' are "no", "off", "0".
		/// The null or empty strings, or any other invalid token will return <c>null</c>.
		/// </remarks>
		[Pure]
		public static bool? ToBoolean(string value)
		{
			if (string.IsNullOrEmpty(value)) return null;
			char c = value[0];
			if (c == 't' || c == 'T') return true;
			if (c == 'f' || c == 'F') return false;
			if (c == 'y' || c == 'Y') return true;
			if (c == 'n' || c == 'N') return false;
			if ((c == 'o' || c == 'O') && value.Length > 1) { c = value[1]; return c == 'n' || c == 'N'; }
			if (c == '1') return true;
			if (c == '0') return false;
			return null;
		}

		/// <summary>Parse a string containing an integer, up to the next separator (or end of string)</summary>
		/// <param name="buffer">Text buffer</param>
		/// <param name="offset">Starting offset in the buffer</param>
		/// <param name="length">Length of the buffer</param>
		/// <param name="separator">Expected separator</param>
		/// <param name="defaultValue">Default value in case of error</param>
		/// <param name="result">Stores the conversion result</param>
		/// <param name="newpos">Stores the new position in the buffer (after the separator if found)</param>
		/// <returns>true if an integer was found; otherwise, false (no more data, malformed integer, ...)</returns>
		/// <exception cref="System.ArgumentNullException">If <paramref name="buffer"/> is null</exception>
		public static unsafe bool FastTryGetInt([NotNull] char* buffer, int offset, int length, char separator, int defaultValue, out int result, out int newpos)
		{
			Contract.PointerNotNull(buffer, nameof(buffer));
			result = defaultValue;
			newpos = offset;
			if (offset < 0 || offset >= length) return false; // already at the end !!

			char c = buffer[offset];
			if (c == separator) { newpos = offset + 1; return false; } // advance the cursor anyway
			if (!char.IsDigit(c))
			{ // this is not a number, go to the next separator
				while (offset < length)
				{
					c = buffer[offset++];
					if (c == separator) break;
				}
				newpos = offset;
				return false; // already the separator, or not a digit == WARNING: the cursor will not advance!
			}
			int res = c - 48;
			offset++;
			// there's at least 1 digit, scan for the next digits
			while (offset < length)
			{
				c = buffer[offset++];
				if (c == separator) break;
				if (!char.IsDigit(c))
				{ // go to the next separator
					while (offset < length)
					{
						c = buffer[offset++];
						if (c == separator) break;
					}
					newpos = offset;
					return false;
				}
				res = res * 10 + (c - 48);
			}

			result = res;
			newpos = offset;
			return true;
		}

		/// <summary>Parse a string containing an integer, up to the next separator (or end of string)</summary>
		/// <param name="buffer">Text buffer</param>
		/// <param name="offset">Starting offset in the buffer</param>
		/// <param name="length">Length of the buffer</param>
		/// <param name="separator">Expected separator</param>
		/// <param name="defaultValue">Default value in case of error</param>
		/// <param name="result">Stores the conversion result</param>
		/// <param name="newpos">Stores the new position in the buffer (after the separator if found)</param>
		/// <returns>true if an integer was found; otherwise, false (no more data, malformed integer, ...)</returns>
		/// <exception cref="System.ArgumentNullException">If <paramref name="buffer"/> is null</exception>
		public static unsafe bool FastTryGetLong([NotNull] char* buffer, int offset, int length, char separator, long defaultValue, out long result, out int newpos)
		{
			Contract.PointerNotNull(buffer, nameof(buffer));
			result = defaultValue;
			newpos = offset;
			if (offset < 0 || offset >= length) return false; // already at the end !!

			char c = buffer[offset];
			if (c == separator) { newpos = offset + 1; return false; } // advance the cursor anyway
			if (!char.IsDigit(c))
			{ // this is not a number, go to the next separator
				while (offset < length)
				{
					c = buffer[offset++];
					if (c == separator) break;
				}
				newpos = offset;
				return false; // already the separator, or not a digit == WARNING: the cursor will not advance!
			}
			int res = c - 48;
			offset++;
			// there's at least 1 digit, scan for the next digits
			while (offset < length)
			{
				c = buffer[offset++];
				if (c == separator) break;
				if (!char.IsDigit(c))
				{ // go to the next separator
					while (offset < length)
					{
						c = buffer[offset++];
						if (c == separator) break;
					}
					newpos = offset;
					return false;
				}
				res = res * 10 + (c - 48);
			}

			result = res;
			newpos = offset;
			return true;
		}

		/// <summary>Convert a string into an integer</summary>
		/// <param name="value">Text string (ex: "1234")</param>
		/// <param name="defaultValue">Default value, if empty or invalid</param>
		/// <returns>Corresponding integer (ex: 1234), or default value if empty or invalid</returns>
		[Pure]
		public static int ToInt32(string value, int defaultValue)
		{
			if (string.IsNullOrEmpty(value)) return defaultValue;
			char c = value[0];
			if (value.Length == 1) return char.IsDigit(c) ? c - 48 : defaultValue;
			if (!char.IsDigit(c) && c != '-' && c != '+' && c != ' ') return defaultValue;
			return int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int res) ? res : defaultValue;
		}

		/// <summary>Convert a string into an integer</summary>
		/// <param name="value">Text string (ex: "1234")</param>
		/// <returns>Corresponding integer (ex: 1234), or <c>null</c> if empty or invalid</returns>
		[Pure]
		public static int? ToInt32(string value)
		{
			if (string.IsNullOrEmpty(value)) return default;
			char c = value[0];
			if (value.Length == 1) return char.IsDigit(c) ? (c - 48) : default(int?);
			if (!char.IsDigit(c) && c != '-' && c != '+' && c != ' ') return default(int?);
			return int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int res) ? res : default(int?);
		}

		/// <summary>Convert a string into an integer</summary>
		/// <param name="value">Text string (ex: "1234")</param>
		/// <param name="defaultValue">Default value, if empty or invalid</param>
		/// <returns>Corresponding integer (ex: 1234), or default value if empty or invalid</returns>
		[Pure]
		public static long ToInt64(string value, long defaultValue)
		{
			if (string.IsNullOrEmpty(value)) return defaultValue;
			char c = value[0];
			if (value.Length == 1) return char.IsDigit(c) ? ((long) c - 48) : defaultValue;
			if (!char.IsDigit(c) && c != '-' && c != '+' && c != ' ') return defaultValue;
			return long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out long res) ? res : defaultValue;
		}

		/// <summary>Convert a string into an integer</summary>
		/// <param name="value">Text string (ex: "1234")</param>
		/// <returns>Corresponding integer (ex: 1234), or <c>null</c> if empty or invalid</returns>
		[Pure]
		public static long? ToInt64(string value)
		{
			if (string.IsNullOrEmpty(value)) return default;
			char c = value[0];
			if (value.Length == 1) return char.IsDigit(c) ? ((long) c - 48) : default(long?);
			if (!char.IsDigit(c) && c != '-' && c != '+' && c != ' ') return default(long?);
			return long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out long res) ? res : default(long?);
		}

		/// <summary>Convert a string into a double</summary>
		/// <param name="value">Text string (ex: "12.34")</param>
		/// <param name="defaultValue">Default value, if empty or invalid</param>
		/// <param name="culture">Optional culture used to decode the number (invariant by default)</param>
		/// <returns>Corresponding number (ex: 12.34d), or default value if empty or invalid</returns>
		[Pure]
		public static double ToDouble(string value, double defaultValue, IFormatProvider culture = null)
		{
			if (string.IsNullOrEmpty(value)) return defaultValue;
			char c = value[0];
			if (!char.IsDigit(c) && c != '+' && c != '-' && c != '.' && c != ' ') return defaultValue;
			culture ??= CultureInfo.InvariantCulture;
			if (culture == CultureInfo.InvariantCulture && value.IndexOf(',') >= 0) value = value.Replace(',', '.');
			return double.TryParse(value, NumberStyles.Float | NumberStyles.AllowThousands, culture, out double result) ? result : defaultValue;
		}

		/// <summary>Convert a string into an double</summary>
		/// <param name="value">Text string (ex: "12.34")</param>
		/// <param name="culture">Optional culture used to decode the number (invariant by default)</param>
		/// <returns>Corresponding number (ex: 12.34d), or <c>null</c> if empty or invalid</returns>
		[Pure]
		public static double? ToDouble(string value, IFormatProvider culture = null)
		{
			if (value == null) return default;
			double result = ToDouble(value, double.NaN, culture);
			return double.IsNaN(result) ? default(double?) : result;
		}

		/// <summary>Convert a string into a float</summary>
		/// <param name="value">Text string (ex: "12.34")</param>
		/// <param name="defaultValue">Default value, if empty or invalid</param>
		/// <param name="culture">Optional culture used to decode the number (invariant by default)</param>
		/// <returns>Corresponding number (ex: 12.34f), or default value if empty or invalid</returns>
		[Pure]
		public static float ToSingle(string value, float defaultValue, IFormatProvider culture = null)
		{
			if (string.IsNullOrEmpty(value)) return defaultValue;
			char c = value[0];
			if (!char.IsDigit(c) && c != '+' && c != '-' && c != '.' && c != ' ') return defaultValue;
			culture ??= CultureInfo.InvariantCulture;
			if (culture == CultureInfo.InvariantCulture && value.IndexOf(',') >= 0) value = value.Replace(',', '.');
			return float.TryParse(value, NumberStyles.Float | NumberStyles.AllowThousands, culture, out float result) ? result : defaultValue;
		}

		/// <summary>Convert a string into a float</summary>
		/// <param name="value">Text string (ex: "12.34")</param>
		/// <param name="culture">Optional culture used to decode the number (invariant by default)</param>
		/// <returns>Corresponding number (ex: 12.34f), or <c>null</c> if empty or invalid</returns>
		[Pure]
		public static float? ToSingle(string value, IFormatProvider culture = null)
		{
			if (value == null) return default;
			float result = ToSingle(value, float.NaN, culture);
			return double.IsNaN(result) ? default(float?) : result;
		}

		/// <summary>Convert a string into a decimal</summary>
		/// <param name="value">Text string (ex: "12.34")</param>
		/// <param name="defaultValue">Default value, if empty or invalid</param>
		/// <param name="culture">Optional culture used to decode the number (invariant by default)</param>
		/// <returns>Corresponding number (ex: 12.34m), or default value if empty or invalid</returns>
		[Pure]
		public static decimal ToDecimal(string value, decimal defaultValue, IFormatProvider culture = null)
		{
			if (string.IsNullOrEmpty(value)) return defaultValue;
			char c = value[0];
			if (!char.IsDigit(c) && c != '+' && c != '-' && c != '.' && c != ' ') return defaultValue;
			if (culture == null) culture = CultureInfo.InvariantCulture;
			if (culture == CultureInfo.InvariantCulture && value.IndexOf(',') >= 0) value = value.Replace(',', '.');
			return decimal.TryParse(value, NumberStyles.Float | NumberStyles.AllowThousands, culture, out decimal result) ? result : defaultValue;
		}

		/// <summary>Convert a string into a decimal</summary>
		/// <param name="value">Text string (ex: "12.34")</param>
		/// <param name="culture">Optional culture used to decode the number (invariant by default)</param>
		/// <returns>Corresponding number (ex: 12.34m), or <c>null</c> if empty or invalid</returns>
		[Pure]
		public static decimal? ToDecimal(string value, IFormatProvider culture = null)
		{
			if (string.IsNullOrEmpty(value)) return default(decimal?);
			char c = value[0];
			if (!char.IsDigit(c) && c != '+' && c != '-' && c != '.' && c != ' ') return default(decimal?);
			if (culture == null) culture = CultureInfo.InvariantCulture;
			if (culture == CultureInfo.InvariantCulture && value.IndexOf(',') >= 0) value = value.Replace(',', '.');
			return decimal.TryParse(value, NumberStyles.Float | NumberStyles.AllowThousands, culture, out decimal result) ? result : default(decimal?);
		}

		/// <summary>Convert a string into a <c>DateTime</c></summary>
		/// <param name="value">Text string (ex: "2019-07-14T12:34:56.789")</param>
		/// <param name="defaultValue">Default value if null or empty</param>
		/// <param name="culture">Optional culture used to decode the date (invariant by default)</param>
		/// <returns>See <see cref="ParseDateTime"/></returns>
		[Pure]
		public static DateTime ToDateTime(string value, DateTime defaultValue, CultureInfo culture = null)
		{
			return ParseDateTime(value, defaultValue, culture);
		}

		/// <summary>Convert a string into a <c>DateTime</c></summary>
		/// <param name="value">Text string (ex: "2019-07-14T12:34:56.789")</param>
		/// <param name="culture">Optional culture used to decode the date (invariant by default)</param>
		/// <returns>See <see cref="ParseDateTime"/></returns>
		[Pure]
		public static DateTime? ToDateTime(string value, CultureInfo culture = null)
		{
			if (string.IsNullOrEmpty(value)) return default(DateTime?);
			DateTime result = ParseDateTime(value, DateTime.MaxValue, culture);
			return result == DateTime.MaxValue ? default(DateTime?) : result;
		}

		/// <summary>Convert a string into a GUID</summary>
		/// <param name="value">Text string (ex: "dd69d327-8a48-4010-a849-843a43801c8b")</param>
		/// <param name="defaultValue">Default value if empty or invalid</param>
		/// <returns>Corresponding GUID, or default value empty or invalid</returns>
		[Pure]
		public static Guid ToGuid(string value, Guid defaultValue)
		{
			if (string.IsNullOrEmpty(value)) return defaultValue;
			return Guid.TryParse(value, out Guid result) ? result : defaultValue;
		}

		/// <summary>Convert a string into a GUID</summary>
		/// <param name="value">Text string (ex: "dd69d327-8a48-4010-a849-843a43801c8b")</param>
		/// <returns>Corresponding GUID, or <c>null</c> if empty or invalid</returns>
		[Pure]
		public static Guid? ToGuid(string value)
		{
			if (string.IsNullOrEmpty(value)) return default;
			return Guid.TryParse(value, out Guid result) ? result : default(Guid?);
		}

		/// <summary>Convert a string into an Enum</summary>
		/// <typeparam name="TEnum">Enum type</typeparam>
		/// <param name="value">Text string (ex: "Red", "2", ...)</param>
		/// <param name="defaultValue">Default value if empty or invalid</param>
		/// <returns>Corresponding enum value, or <paramref name="defaultValue"/> if empty or invalid</returns>
		/// <remarks>Accept both numerical and case-insensitive textual forms of the enum values</remarks>
		[Pure]
		public static TEnum ToEnum<TEnum>(string value, TEnum defaultValue)
			where TEnum : struct, IComparable, IConvertible, IFormattable
		{
			if (string.IsNullOrEmpty(value)) return defaultValue;
			return Enum.TryParse<TEnum>(value, true, out TEnum result) ? result : defaultValue;
		}

		/// <summary>Convert a string into an Enum</summary>
		/// <typeparam name="TEnum">Enum type</typeparam>
		/// <param name="value">Text string (ex: "Red", "2", ...)</param>
		/// <returns>Corresponding enum value, or <c>null</c> if empty or invalid</returns>
		/// <remarks>Accept both numerical and case-insensitive textual forms of the enum values</remarks>
		[Pure]
		public static TEnum? ToEnum<TEnum>(string value)
			where TEnum : struct, IComparable, IConvertible, IFormattable
		{
			if (string.IsNullOrEmpty(value)) return default;
			return Enum.TryParse<TEnum>(value, true, out TEnum result) ? result : default(TEnum?);
		}

		#endregion

		#region Dates...

		/// <summary>Convert a date into a string, using the "YYYYMMDDHHMMSS" format</summary>
		/// <param name="date">Date to convert</param>
		/// <returns>Formatted date with fixed length 14 and format 'YYYYMMDDHHMMSS'</returns>
		[Pure, NotNull, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static string ToDateTimeString(DateTime date)
		{
			return date.ToString("yyyyMMddHHmmss", CultureInfo.InvariantCulture);
		}

		/// <summary>Convert a date into a string, using the "YYYYMMDD" format</summary>
		/// <param name="date">Date to convert (only date will be used)</param>
		/// <returns>Formatted date width fixed length 8 and format 'YYYYMMDDHHMMSS'</returns>
		[Pure, NotNull, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static string ToDateString(DateTime date)
		{
			return date.ToString("yyyyMMdd", CultureInfo.InvariantCulture);
		}

		/// <summary>Convert a time of day into a string, using the "HHMMSS" format</summary>
		/// <param name="date">Date to convert (only time of day will be used)</param>
		/// <returns>Formatted time width fixed length 6 and format 'HHMMSS'</returns>
		[Pure, NotNull, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static string ToTimeString(DateTime date)
		{
			return date.ToString("HHmmss", CultureInfo.InvariantCulture);
		}

		/// <summary>Convert a string with any supported format ("YYYY", "YYYYMM", "YYYYMMDD", "YYYYMMDDHHMMSS", ..) into a DateTime</summary>
		/// <param name="date">Text string to convert</param>
		/// <returns>Corresponding DateTime, or an exception if invalid</returns>
		/// <exception cref="System.ArgumentException">If the date is invalid</exception>
		[Pure]
		public static DateTime ParseDateTime(string date)
		{
			return ParseDateTime(date, null);
		}

		/// <summary>Convert a string with any supported format ("YYYY", "YYYYMM", "YYYYMMDD", "YYYYMMDDHHMMSS", ..) into a DateTime</summary>
		/// <param name="date">Text string to convert</param>
		/// <param name="culture">Culture used to parse the date</param>
		/// <returns>Corresponding DateTime, or an exception if invalid</returns>
		/// <exception cref="System.ArgumentException">If the date is invalid</exception>
		[Pure]
		public static DateTime ParseDateTime(string date, CultureInfo culture)
		{
			if (!TryParseDateTime(date, culture, out DateTime result, true)) throw FailInvalidDateFormat();
			return result;
		}

		[Pure, NotNull, MethodImpl(MethodImplOptions.NoInlining)]
		private static Exception FailInvalidDateFormat()
		{
			// ReSharper disable once NotResolvedInText
			return new ArgumentException("Invalid date format", "date");
		}

		/// <summary>Convert a string with any supported format ("YYYY", "YYYYMM", "YYYYMMDD", "YYYYMMDDHHMMSS", ..) into a DateTime</summary>
		/// <param name="date">Text string to convert</param>
		/// <param name="defaultValue">Default value, if empty or invalid</param>
		/// <returns>Corresponding DateTime, or default value if empty or invalid</returns>
		[Pure]
		public static DateTime ParseDateTime(string date, DateTime defaultValue)
		{
			if (string.IsNullOrEmpty(date)) return defaultValue;
			return TryParseDateTime(date, null, out DateTime result, false) ? result : defaultValue;
		}

		/// <summary>Convert a string with any supported format ("YYYY", "YYYYMM", "YYYYMMDD", "YYYYMMDDHHMMSS", ..) into a DateTime</summary>
		/// <param name="date">Text string to convert</param>
		/// <param name="defaultValue">Default value, if empty or invalid</param>
		/// <param name="culture">Culture used to parse the date</param>
		/// <returns>Corresponding DateTime, or default value if empty or invalid</returns>
		[Pure]
		public static DateTime ParseDateTime(string date, DateTime defaultValue, CultureInfo culture)
		{
			return TryParseDateTime(date, culture, out DateTime result, false) ? result : defaultValue;
		}
		private static int ParseDateSegmentUnsafe(string source, int offset, int size)
		{
			int sum = source[offset++] - '0';
			if (sum < 0 || sum >= 10) return -1; // invalid first digit
			while (--size > 0)
			{
				int d = source[offset++] - '0';
				if (d < 0 || d >= 10) return -1; // invalid digit!
				sum = (sum * 10) + d;
			}
			return sum;
		}

		/// <summary>Try to convert a string into a <see cref="DateTime"/>, using any supported format ("YYYY", "YYYYMM", "YYYYMMDD" or "YYYYMMDDHHMMSS", ...)</summary>
		/// <param name="date">Text string to convert</param>
		/// <param name="culture">Optional culture (invariant if null)</param>
		/// <param name="result">Stores the converted date (or DateTime.MinValue if conversion failed)</param>
		/// <param name="throwsFail">If <c>false</c>, no exception is thrown and <c>false</c> is returned instead.. If <c>true<c>, re-throw all exceptions</param>
		/// <returns><c>true</c> if the date was correctly converted; otherwise, <c>false</c>.</returns>
		[Pure]
		public static bool TryParseDateTime(string date, CultureInfo culture, out DateTime result, bool throwsFail)
		{
			result = DateTime.MinValue;

			if (date == null) { if (throwsFail) throw new ArgumentNullException(nameof(date)); else return false; }
			if (date.Length < 4) { if (throwsFail) throw new FormatException("Date '" + date + "' must be at least 4 characters long"); else return false; }
			try
			{
				if (char.IsDigit(date[0]))
				{ // commence par un chiffre, c'est peut etre un timestamp?
					switch (date.Length)
					{
						case 4:
						{ // YYYY -> YYYY/01/01 00:00:00.000
							int y = ParseDateSegmentUnsafe(date, 0, 4);
							if (y < 1 || y > 9999) break;
							result = new DateTime(y, 1, 1);
							return true;
						}
						case 6:
						{ // YYYYMM -> YYYY/MM/01 00:00:00.000
							int y = ParseDateSegmentUnsafe(date, 0, 4);
							if (y < 1 || y > 9999) break;
							int m = ParseDateSegmentUnsafe(date, 4, 2);
							if (m < 1 || m > 12) break;
							result = new DateTime(y, m, 1);
							return true;
						}
						case 8:
						{ // YYYYMMDD -> YYYY/MM/DD 00:00:00.000
							int y = ParseDateSegmentUnsafe(date, 0, 4);
							if (y < 1 || y > 9999) break;
							int m = ParseDateSegmentUnsafe(date, 4, 2);
							if (m < 1 || m > 12) break;
							int d = ParseDateSegmentUnsafe(date, 6, 2);
							if (d < 1 || d > 31) break;
							result = new DateTime(y, m, d);
							return true;
						}
						case 14:
						{ // YYYYMMDDHHMMSS -> YYYY/MM/DD HH:MM:SS.000
							int y = ParseDateSegmentUnsafe(date, 0, 4);
							if (y < 1 || y > 9999) break;
							int m = ParseDateSegmentUnsafe(date, 4, 2);
							if (m < 1 || m > 12) break;
							int d = ParseDateSegmentUnsafe(date, 6, 2);
							if (d < 1 || d > 31) break;
							int h = ParseDateSegmentUnsafe(date, 8, 2);
							if (h < 0 || h > 23) break;
							int n = ParseDateSegmentUnsafe(date, 10, 2);
							if (n < 0 || n > 59) break;
							int s = ParseDateSegmentUnsafe(date, 12, 2);
							if (s < 0 || s > 59) break;
							result = new DateTime(y, m, d, h, n, s);
							return true;
						}
						case 17:
						{ // YYYYMMDDHHMMSSFFF -> YYYY/MM/DD HH:MM:SS.FFF
							int y = ParseDateSegmentUnsafe(date, 0, 4);
							if (y < 1 || y > 9999) break;
							int m = ParseDateSegmentUnsafe(date, 4, 2);
							if (m < 1 || m > 12) break;
							int d = ParseDateSegmentUnsafe(date, 6, 2);
							if (d < 1 || d > 31) break;
							int h = ParseDateSegmentUnsafe(date, 8, 2);
							if (h < 0 || h > 23) break;
							int n = ParseDateSegmentUnsafe(date, 10, 2);
							if (n < 0 || n > 59) break;
							int s = ParseDateSegmentUnsafe(date, 12, 2);
							if (s < 0 || s > 59) break;
							int f = ParseDateSegmentUnsafe(date, 14, 3);
							result = new DateTime(y, m, d, h, n, s, f);
							return true;
						}
					}
				}
				else if (char.IsLetter(date[0]))
				{ // Attempt a ParseExact even if the date is using an exotic representation ("Vendredi, 37 Trumaire 1789 à 3 heures moins le quart de l'après midi")
					result = DateTime.ParseExact(date, new[] { "D", "F", "f" }, culture ?? CultureInfo.InvariantCulture, DateTimeStyles.None);
					return true;
				}

				// Well... we may as well try our luck!
				result = DateTime.Parse(date, culture ?? CultureInfo.InvariantCulture);
				return true;
			}
			catch (FormatException)
			{ // This does not look like anything to us...
				if (throwsFail) throw;
				return false;
			}
			catch (ArgumentOutOfRangeException)
			{ // For strings that have the shape of a date, only with invalid values (ex: 31th of February, etc..)
				if (throwsFail) throw;
				return false;
			}
		}

		#endregion
	}
}

#endif
