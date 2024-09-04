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

namespace Doxense.Serialization
{
	using System.Globalization;
	using System.Runtime.CompilerServices;
	using NodaTime;

	[PublicAPI]
	public static class StringConverters
	{
		#region Numbers...

		/// <summary>Converts an integer into a decimal string literal, using the Invariant culture</summary>
		/// <param name="value">Value to convert</param>
		/// <returns>Corresponding string literal</returns>
		[Pure]
		public static string ToString(int value)
		{
			//perf: as of .NET 8, Int32.ToString(null) calls Number.UInt32ToDecStr(...) which already manage a cache for small numbers (less than 300)
			if (value >= 0)
			{
				return value.ToString(default(IFormatProvider));
			}
			// for negative numbers, we have to pass an invariant culture, otherwise it will use the NegativeSign of the current culture
			return value.ToString(NumberFormatInfo.InvariantInfo);
		}

		/// <summary>Converts an integer into a decimal string literal, using the Invariant culture</summary>
		/// <param name="value">Value to convert</param>
		/// <returns>Corresponding string literal</returns>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static string ToString(uint value)
		{
			//perf: as of .NET 8, UInt32.ToString(null) calls Number.UInt32ToDecStr(...) which already manage a cache for small numbers (less than 300)
			return value.ToString(default(IFormatProvider));
		}

		/// <summary>Converts an integer into a decimal string literal, using the Invariant culture</summary>
		/// <param name="value">Value to convert</param>
		/// <returns>Corresponding string literal</returns>
		[Pure]
		public static string ToString(long value)
		{
			//perf: as of .NET 8, Int64.ToString(null) calls Number.UInt64ToDecStr(...) which already manage a cache for small numbers (less than 300)
			if (value >= 0)
			{
				return value.ToString(default(IFormatProvider));
			}

			// for negative numbers, we have to pass an invariant culture, otherwise it will use the NegativeSign of the current culture
			return value.ToString(NumberFormatInfo.InvariantInfo);
		}

		/// <summary>Converts an integer into a decimal string literal, using the Invariant culture</summary>
		/// <param name="value">Value to convert</param>
		/// <returns>Corresponding string literal</returns>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static string ToString(ulong value)
		{
			//perf: as of .NET 8, UInt64.ToString(null) calls Number.UInt64ToDecStr(...) which already manage a cache for small numbers (less than 300)
			return value.ToString(default(IFormatProvider));
		}

		/// <summary>Converts an integer into a decimal string literal, using the Invariant culture</summary>
		/// <param name="value">Value to convert</param>
		/// <returns>Corresponding string literal</returns>
		[Pure]
		public static string ToString(float value)
		{
			long x = unchecked((long) value);
			// ReSharper disable once CompareOfFloatsByEqualityOperator
			return x != value
				? value.ToString("R", NumberFormatInfo.InvariantInfo)
				: x >= 0 ? x.ToString(default(IFormatProvider))
				: x.ToString(NumberFormatInfo.InvariantInfo);
		}

		/// <summary>Converts an integer into a decimal string literal, using the Invariant culture</summary>
		/// <param name="value">Value to convert</param>
		/// <returns>Corresponding string literal</returns>
		[Pure]
		public static string ToString(double value)
		{
			long x = unchecked((long) value);
			// ReSharper disable once CompareOfFloatsByEqualityOperator
			return x != value
				? value.ToString("R", NumberFormatInfo.InvariantInfo)
				: x >= 0 ? x.ToString(default(IFormatProvider))
				: x.ToString(NumberFormatInfo.InvariantInfo);
		}

		/// <summary>Converts a string literal into a boolean, using relaxed rules</summary>
		/// <param name="value">String literal containing either a "truthy" or "falsy" boolean (ex: "true", "on", "1" vs "false", "off", "0", ...)</param>
		/// <param name="dflt">Default value, if the string is empty or not recognized</param>
		/// <returns>Corresponding boolean value if it matches the set of recognized tokens; otherwise, <paramref name="dflt"/>.</returns>
		/// <remarks>
		/// <para>The recognized "truthy" literals are: <c>"true"</c>, <c>"yes"</c>, <c>"on"</c>, <c>"1"</c>.</para>
		/// <para>The recognized "falsy" literals are: <c>"false"</c>, <c>"no"</c>, <c>"off"</c>, <c>"0"</c>, the null or empty string.</para>
		/// </remarks>
		[Pure]
		public static bool ToBoolean(string? value, bool dflt)
		{
			if (string.IsNullOrEmpty(value))
			{
				return dflt;
			}

			return char.ToLowerInvariant(value[0]) switch
			{
				't' => true,  // "true"
				'f' => false, // "false"
				'y' => true,  // "yes"
				'n' => false, // "no"
				'o' => value.Length switch
				{
					2 => (char.ToLowerInvariant(value[1]) == 'n' || dflt), // "on"
					3 => ((char.ToLowerInvariant(value[1]) != 'f' || char.ToLowerInvariant(value[2]) != 'f') && dflt), // "off"
					_ => dflt
				},
				'1' => true, // "1"
				'0' => false, // "0"
				_ => dflt
			};
		}

		/// <summary>Converts a string literal into its boolean equivalent, using relaxed rules</summary>
		/// <param name="value">String literal containing either a "truthy" or "falsy" boolean (ex: "true", "on", "1" vs "false", "off", "0", ...)</param>
		/// <returns>Corresponding boolean value if it matches the set of recognized tokens; otherwise, <see langword="null"/>.</returns>
		/// <remarks>
		/// <para>The recognized "truthy" literals are: <c>"true"</c>, <c>"yes"</c>, <c>"on"</c>, <c>"1"</c>.</para>
		/// <para>The recognized "falsy" literals are: <c>"false"</c>, <c>"no"</c>, <c>"off"</c>, <c>"0"</c>.</para>
		/// <para>The null and empty strings will return <see langword="null"/>.</para>
		/// </remarks>
		/// <example>
		/// <code>StringConverters.ToBoolean("true", false) == true</code>
		/// <code>StringConverters.ToBoolean("false", true) == false</code>
		/// <code>StringConverters.ToBoolean("hello", false) == false</code>
		/// </example>
		[Pure]
		public static bool? ToBoolean(string? value)
		{
			if (string.IsNullOrEmpty(value)) return null;
			char c = value[0];
			return char.ToLowerInvariant(c) switch
			{
				't' => true,  // "true"
				'f' => false, // "false"
				'y' => true,  // "yes"
				'n' => false, // "no"
				'o' => value.Length switch
				{
					2 => (char.ToLowerInvariant(value[1]) == 'n' ? true : null), // "on"
					3 => (char.ToLowerInvariant(value[1]) == 'f' && char.ToLowerInvariant(value[2]) == 'f' ? false : null), // "off"
					_ => null
				},
				'1' => true,
				'0' => false,
				_ => null
			};
		}

		/// <summary>Convertit un entier jusqu'au prochain séparateur (ou fin de buffer). A utilisé pour simuler un Split</summary>
		/// <param name="buffer">Buffer de caractères</param>
		/// <param name="offset">Offset courant dans le buffer</param>
		/// <param name="length"></param>
		/// <param name="separator">Séparateur attendu entre les entiers</param>
		/// <param name="defaultValue">Valeur par défaut retournée si erreur</param>
		/// <param name="result">Récupère le résultat de la conversion</param>
		/// <param name="newpos">Récupère la nouvelle position (après le séparateur)</param>
		/// <returns>true si int chargé, false si erreur (plus de place, incorrect, ...)</returns>
		/// <exception cref="System.ArgumentNullException">Si buffer est null</exception>
		[Obsolete("Use int.TryParse(ReadOnlySpan<char>, ...) instead!")]
		public static unsafe bool FastTryGetInt(char* buffer, int offset, int length, char separator, int defaultValue, out int result, out int newpos)
		{
			Contract.PointerNotNull(buffer);
			result = defaultValue;
			newpos = offset;
			if (offset < 0 || offset >= length) return false; // déjà a la fin !!

			char c = buffer[offset];
			if (c == separator) { newpos = offset + 1; return false; } // avance quand même le curseur
			if (!char.IsDigit(c))
			{ // c'est pas un nombre, va jusqu'au prochain séparateur
				while (offset < length)
				{
					c = buffer[offset++];
					if (c == separator) break;
				}
				newpos = offset;
				return false; // déjà le séparateur, ou pas un digit == WARNING: le curseur ne sera pas avancé!
			}
			int res = c - 48;
			offset++;
			// il y a au moins 1 digit, parcourt les suivants
			while (offset < length)
			{
				c = buffer[offset++];
				if (c == separator) break;
				if (!char.IsDigit(c))
				{ // va jusqu'au prochain séparateur
					while (offset < length)
					{
						c = buffer[offset++];
						if (c == separator) break;
					}
					newpos = offset;
					return false;
				}
				// accumule le digit
				res = res * 10 + (c - 48);
			}

			result = res;
			newpos = offset;
			return true;
		}

		/// <summary>Convertit un entier jusqu'au prochain séparateur (ou fin de buffer). A utilisé pour simuler un Split</summary>
		/// <param name="buffer">Buffer de caractères</param>
		/// <param name="offset">Offset courant dans le buffer</param>
		/// <param name="length"></param>
		/// <param name="separator">Séparateur attendu entre les entiers</param>
		/// <param name="defaultValue">Valeur par défaut retournée si erreur</param>
		/// <param name="result">Récupère le résultat de la conversion</param>
		/// <param name="newpos">Récupère la nouvelle position (après le séparateur)</param>
		/// <returns>true si int chargé, false si erreur (plus de place, incorrect, ...)</returns>
		/// <exception cref="System.ArgumentNullException">Si buffer est null</exception>
		[Obsolete("Use long.TryParse(ReadOnlySpan<char>, ...) instead!")]
		public static unsafe bool FastTryGetLong(char* buffer, int offset, int length, char separator, long defaultValue, out long result, out int newpos)
		{
			Contract.PointerNotNull(buffer);
			result = defaultValue;
			newpos = offset;
			if (offset < 0 || offset >= length) return false; // déjà a la fin !!

			char c = buffer[offset];
			if (c == separator) { newpos = offset + 1; return false; } // avance quand même le curseur
			if (!char.IsDigit(c))
			{ // c'est pas un nombre, va jusqu'au prochain séparateur
				while (offset < length)
				{
					c = buffer[offset++];
					if (c == separator) break;
				}
				newpos = offset;
				return false; // déjà le séparateur, ou pas un digit == WARNING: le curseur ne sera pas avancé!
			}
			int res = c - 48;
			offset++;
			// il y a au moins 1 digit, parcourt les suivants
			while (offset < length)
			{
				c = buffer[offset++];
				if (c == separator) break;
				if (!char.IsDigit(c))
				{ // va jusqu'au prochain séparateur
					while (offset < length)
					{
						c = buffer[offset++];
						if (c == separator) break;
					}
					newpos = offset;
					return false;
				}
				// accumule le digit
				res = res * 10 + (c - 48);
			}

			result = res;
			newpos = offset;
			return true;
		}

		/// <summary>Converts a string literal into its 32-bit signed integer equivalent, using invariant-culture format</summary>
		/// <param name="value">string literal to convert (ex: "1234" or "-5")</param>
		/// <param name="defaultValue">Fallback value returned if the string literal is empty or not a valid integer</param>
		/// <returns>Corresponding integer value, or <paramref name="defaultValue"/> if could not be decoded</returns>
		/// <example>
		/// <code>StringConverters.ToInt32("1234", 0) == 1234</code>
		/// <code>StringConverters.ToInt32("hello", 0) == 0</code>
		/// </example>
		[Pure]
		public static int ToInt32(string? value, int defaultValue)
		{
			if (string.IsNullOrEmpty(value))
			{
				return defaultValue;
			}

			char c = value[0];
			if (value.Length == 1)
			{
				return char.IsDigit(c) ? c - 48 : defaultValue;
			}

			if (!char.IsDigit(c) && c != '-' && c != '+' && c != ' ')
			{
				return defaultValue;
			}
			
			return int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int res) ? res : defaultValue;
		}

		/// <summary>Converts a string literal into its 32-bit signed integer equivalent, using invariant-culture format</summary>
		/// <param name="value">string literal to convert (ex: "1234" or "-5")</param>
		/// <returns>Corresponding integer value, or <see langword="null"/> if could not be decoded</returns>
		/// <example>
		/// <code>StringConverters.ToInt32("1234") == 1234</code>
		/// <code>StringConverters.ToInt32("hello") == null</code>
		/// </example>
		[Pure]
		public static int? ToInt32(string? value)
		{
			if (string.IsNullOrEmpty(value)) return default;
			// optimisation: si premier caractère pas chiffre, exit
			char c = value[0];
			if (value.Length == 1) return char.IsDigit(c) ? (c - 48) : default(int?);
			if (!char.IsDigit(c) && c != '-' && c != '+' && c != ' ') return default(int?);
			return int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int res) ? res : default(int?);
		}

		/// <summary>Converts a string literal into its 64-bit signed integer equivalent, using invariant-culture format</summary>
		/// <param name="value">string literal to convert (ex: "1234" or "-5")</param>
		/// <param name="defaultValue">Fallback value returned if the string literal is empty or not a valid integer</param>
		/// <returns>Corresponding integer value, or <paramref name="defaultValue"/> if could not be decoded</returns>
		/// <example>
		/// <code>StringConverters.ToInt64("1234", 0) == 1234</code>
		/// <code>StringConverters.ToInt64("hello", 0) == 0</code>
		/// </example>
		[Pure]
		public static long ToInt64(string? value, long defaultValue)
		{
			if (string.IsNullOrEmpty(value)) return defaultValue;
			// optimisation: si premier caractère pas chiffre, exit
			char c = value[0];
			if (value.Length == 1) return char.IsDigit(c) ? ((long) c - 48) : defaultValue;
			if (!char.IsDigit(c) && c != '-' && c != '+' && c != ' ') return defaultValue;
			return long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out long res) ? res : defaultValue;
		}

		/// <summary>Converts a string literal into its 64-bit signed integer equivalent, using invariant-culture format</summary>
		/// <param name="value">string literal to convert (ex: "1234" or "-5")</param>
		/// <returns>Corresponding integer value, or <see langword="null"/> if could not be decoded</returns>
		/// <example>
		/// <code>StringConverters.ToInt64("1234") == 1234</code>
		/// <code>StringConverters.ToInt64("hello") == null</code>
		/// </example>
		[Pure]
		public static long? ToInt64(string? value)
		{
			if (string.IsNullOrEmpty(value))
			{
				return default;
			}

			char c = value[0];
			if (value.Length == 1)
			{
				return char.IsDigit(c) ? ((long) c - 48) : default(long?);
			}

			if (!char.IsDigit(c) && c != '-' && c != '+' && c != ' ')
			{
				return default;
			}

			return long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out long res) ? res : default(long?);
		}

		/// <summary>Converts a string literal into its 64-bit floating-point number equivalent, using invariant-culture format</summary>
		/// <param name="value">string literal to convert (ex: "1.0" or "123" or "123.456e7")</param>
		/// <param name="defaultValue">Fallback value returned if the string literal is empty or not a valid decimal number</param>
		/// <returns>Corresponding double value, or <paramref name="defaultValue"/> if could not be decoded</returns>
		/// <example>
		/// <code>StringConverters.ToDouble("1.23", 0) => 1.23d</code>
		/// <code>StringConverters.ToDouble("hello", 0) => 0</code>
		/// <code>StringConverters.ToDouble("NaN", 0)) => double.NaN</code>
		/// <code>StringConverters.ToDouble("∞", 0)) => double.PositiveInfinity</code>
		/// <code>StringConverters.ToDouble("-∞", 0)) => double.NegativeInfinity</code>
		/// </example>
		[Pure]
		public static double ToDouble(string? value, double defaultValue)
		{
			if (string.IsNullOrEmpty(value))
			{ // empty
				return defaultValue;
			}

			char c = value[0];
			if (value.Length == 1)
			{ // single-digit number
				return char.IsDigit(c) ? c - '0' : c == '∞' ? double.PositiveInfinity : defaultValue;
			}

			// note: TryParse with InvariantCulture will handle "NaN" but not "∞", "+∞", "-∞"
			return double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out double result)
				? result
				: value == "-∞" ? double.NegativeInfinity
				: defaultValue;
		}

		/// <summary>Converts a string literal into its 64-bit floating-point number equivalent, using invariant-culture format</summary>
		/// <param name="value">string literal to convert (ex: "1.0" or "123" or "123.456e7")</param>
		/// <returns>Corresponding double value, or <see langword="null"/> if could not be decoded</returns>
		/// <example>
		/// <code>StringConverters.ToDouble("1.23") => 1.23d</code>
		/// <code>StringConverters.ToDouble("hello") => null</code>
		/// <code>StringConverters.ToDouble("NaN")) => double.NaN</code>
		/// <code>StringConverters.ToDouble("∞")) => double.PositiveInfinity</code>
		/// <code>StringConverters.ToDouble("-∞")) => double.NegativeInfinity</code>
		/// </example>
		[Pure]
		public static double? ToDouble(string? value)
		{
			if (string.IsNullOrEmpty(value))
			{ // empty
				return null;
			}

			char c = value[0];
			if (value.Length == 1)
			{ // single-digit number
				return char.IsDigit(c) ? c - '0' : c == '∞' ? double.PositiveInfinity : null;
			}

			// note: TryParse with InvariantCulture will handle "NaN" but not "∞", "+∞", "-∞"
			return double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out double result)
				? result
				: value == "-∞" ? double.NegativeInfinity
				: null;
		}

		/// <summary>Converts a string literal into its 32-bit floating-point number equivalent, using invariant-culture format</summary>
		/// <param name="value">string literal to convert (ex: "1.0" or "123" or "123.456e7")</param>
		/// <param name="defaultValue">Fallback value returned if the string literal is empty or not a valid decimal number</param>
		/// <returns>Corresponding single value, or <paramref name="defaultValue"/> if could not be decoded</returns>
		/// <example>
		/// <code>StringConverters.ToSingle("1.23", 0) => 1.23d</code>
		/// <code>StringConverters.ToSingle("hello", 0) => 0</code>
		/// <code>StringConverters.ToSingle("NaN", 0)) => float.NaN</code>
		/// <code>StringConverters.ToSingle("∞", 0)) => float.PositiveInfinity</code>
		/// <code>StringConverters.ToSingle("-∞", 0)) => float.NegativeInfinity</code>
		/// </example>
		[Pure]
		public static float ToSingle(string? value, float defaultValue)
		{
			if (string.IsNullOrEmpty(value))
			{ // empty
				return defaultValue;
			}

			char c = value[0];
			if (value.Length == 1)
			{ // single-digit number
				return char.IsDigit(c) ? c - '0' : c == '∞' ? float.PositiveInfinity : defaultValue;
			}

			// note: TryParse with InvariantCulture will handle "NaN" but not "∞", "+∞", "-∞"
			return float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out float result)
				? result
				: value == "-∞" ? float.NegativeInfinity
				: defaultValue;
		}

		/// <summary>Converts a string literal into its 32-bit floating-point number equivalent, using invariant-culture format</summary>
		/// <param name="value">string literal to convert (ex: "1.0" or "123" or "123.456e7")</param>
		/// <returns>Corresponding single value, or <see langword="null"/> if could not be decoded</returns>
		/// <example>
		/// <code>StringConverters.ToSingle("1.23") => 1.23d</code>
		/// <code>StringConverters.ToSingle("hello") => null</code>
		/// <code>StringConverters.ToSingle("NaN")) => float.NaN</code>
		/// <code>StringConverters.ToSingle("∞")) => float.PositiveInfinity</code>
		/// <code>StringConverters.ToSingle("-∞")) => float.NegativeInfinity</code>
		/// </example>
		[Pure]
		public static float? ToSingle(string? value)
		{
			if (string.IsNullOrEmpty(value))
			{ // empty
				return null;
			}

			char c = value[0];
			if (value.Length == 1)
			{ // single-digit number
				return char.IsDigit(c) ? c - '0' : c == '∞' ? float.PositiveInfinity : null;
			}

			// note: TryParse with InvariantCulture will handle "NaN" but not "∞", "+∞", "-∞"
			return float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out float result)
				? result
				: value == "-∞" ? float.NegativeInfinity
				: null;
		}

		/// <summary>Convertit une chaîne de caractère en double, quelque soit la langue locale (utilise le '.' comme séparateur décimal)</summary>
		/// <param name="value">Chaîne (ex: "1.0", "123.456e7")</param>
		/// <param name="defaultValue">Valeur par défaut si problème de conversion ou null</param>
		/// <param name="culture">Culture (par défaut InvariantCulture)</param>
		/// <returns>Décimal correspondant</returns>
		[Pure]
		public static decimal ToDecimal(string? value, decimal defaultValue, IFormatProvider? culture = null)
		{
			if (string.IsNullOrEmpty(value)) return defaultValue;
			char c = value[0];
			if (!char.IsDigit(c) && c != '+' && c != '-' && c != '.' && c != ' ') return defaultValue;
			culture ??= CultureInfo.InvariantCulture;
			if (culture.Equals(CultureInfo.InvariantCulture) && value.IndexOf(',') >= 0) value = value.Replace(',', '.');
			return decimal.TryParse(value, NumberStyles.Float | NumberStyles.AllowThousands, culture, out decimal result) ? result : defaultValue;
		}

		[Pure]
		public static decimal? ToDecimal(string? value, IFormatProvider? culture = null)
		{
			if (string.IsNullOrEmpty(value)) return default;
			char c = value[0];
			if (!char.IsDigit(c) && c != '+' && c != '-' && c != '.' && c != ' ') return default(decimal?);
			culture ??= CultureInfo.InvariantCulture;
			if (culture.Equals(CultureInfo.InvariantCulture) && value.IndexOf(',') >= 0) value = value.Replace(',', '.');
			return decimal.TryParse(value, NumberStyles.Float | NumberStyles.AllowThousands, culture, out decimal result) ? result : default(decimal?);
		}

		/// <summary>Convertit une chaîne en DateTime</summary>
		/// <param name="value">Date à convertir</param>
		/// <param name="defaultValue">Valeur par défaut</param>
		/// <param name="culture"></param>
		/// <returns>Voir StringConverters.ParseDateTime()</returns>
		[Pure]
		public static DateTime ToDateTime(string value, DateTime defaultValue, CultureInfo? culture = null)
		{
			return ParseDateTime(value, defaultValue, culture);
		}

		/// <summary>Convertit une chaîne en DateTime</summary>
		/// <param name="value">Date à convertir</param>
		/// <param name="culture"></param>
		/// <returns>Voir StringConverters.ParseDateTime()</returns>
		[Pure]
		public static DateTime? ToDateTime(string? value, CultureInfo? culture = null)
		{
			if (string.IsNullOrEmpty(value)) return default;
			DateTime result = ParseDateTime(value, DateTime.MaxValue, culture);
			return result == DateTime.MaxValue ? default(DateTime?) : result;
		}

		[Pure]
		public static Instant? ToInstant(string? date, CultureInfo? culture = null)
		{
			if (string.IsNullOrEmpty(date)) return default;
			if (!TryParseInstant(date, culture, out Instant res, false)) return default(Instant?);
			return res;
		}

		[Pure]
		public static Instant ToInstant(string? date, Instant dflt, CultureInfo? culture = null)
		{
			if (string.IsNullOrEmpty(date)) return dflt;
			if (!TryParseInstant(date, culture, out Instant res, false)) return dflt;
			return res;
		}

		/// <summary>Convertit une chaîne de caractères en GUID</summary>
		/// <param name="value">Chaîne (ex: "123456-789")</param>
		/// <param name="defaultValue">Valeur par défaut si problème de conversion ou null</param>
		/// <returns>GUID correspondant</returns>
		[Pure]
		public static Guid ToGuid(string? value, Guid defaultValue)
		{
			if (string.IsNullOrEmpty(value)) return defaultValue;
			return Guid.TryParse(value, out Guid result) ? result : defaultValue;
		}

		[Pure]
		public static Guid? ToGuid(string? value)
		{
			if (string.IsNullOrEmpty(value)) return default;
			return Guid.TryParse(value, out Guid result) ? result : default(Guid?);
		}

		/// <summary>Convertit une chaîne de caractères en Enum</summary>
		/// <typeparam name="TEnum">Type de l'Enum</typeparam>
		/// <param name="value">Chaîne (ex: "Red", "2", ...)</param>
		/// <param name="defaultValue">Valeur par défaut si problème de conversion ou null</param>
		/// <returns>Valeur de l'enum correspondante</returns>
		/// <remarks>Accepte les valeurs sous forme textuelle ou numérique, case insensitive</remarks>
		[Pure]
		public static TEnum ToEnum<TEnum>(string? value, TEnum defaultValue)
			where TEnum : struct, Enum
		{
			if (string.IsNullOrEmpty(value)) return defaultValue;
			return Enum.TryParse(value, true, out TEnum result) ? result : defaultValue;
		}

		[Pure]
		public static TEnum? ToEnum<TEnum>(string? value)
			where TEnum : struct, Enum
		{
			if (string.IsNullOrEmpty(value)) return default(TEnum?);
			return Enum.TryParse(value, true, out TEnum result) ? result : default(TEnum?);
		}

		#endregion

		#region Dates...

		/// <summary>Convertit une date en une chaîne de caractères au format "YYYYMMDDHHMMSS"</summary>
		/// <param name="date">Date à formater</param>
		/// <returns>Date formatée sur 14 caractères au format YYYYMMDDHHMMSS</returns>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static string ToDateTimeString(DateTime date)
		{
			//REVIEW: PERF: faire une version optimisée?
			return date.ToString("yyyyMMddHHmmss", CultureInfo.InvariantCulture);
		}

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static string ToDateTimeString(Instant date)
		{
			//REVIEW: PERF: faire une version optimisée?
			return date.ToString("yyyyMMddHHmmss", CultureInfo.InvariantCulture);
		}

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static string ToDateTimeString(ZonedDateTime date)
		{
			//REVIEW: PERF: faire une version optimisée?
			return date.ToString("yyyyMMddHHmmss", CultureInfo.InvariantCulture);
		}

		/// <summary>Convertit une date en une chaîne de caractères au format "AAAAMMJJ"</summary>
		/// <param name="date">Date à formater</param>
		/// <returns>Date formatée sur 8 caractères au format AAAAMMJJ</returns>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static string ToDateString(DateTime date)
		{
			//REVIEW: PERF: faire une version optimisée?
			return date.ToString("yyyyMMdd", CultureInfo.InvariantCulture);
		}

		/// <summary>Convertit un heure en une chaîne de caractères au format "HHMMSS"</summary>
		/// <param name="date">Date à formater</param>
		/// <returns>Heure formatée sur 6 caractères au format HHMMSS</returns>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static string ToTimeString(DateTime date)
		{
			//REVIEW: PERF: faire une version optimisée?
			return date.ToString("HHmmss", CultureInfo.InvariantCulture);
		}

		/// <summary>Convertit une date en une chaîne de caractères au format "yyyy-MM-dd HH:mm:ss"</summary>
		/// <param name="date">Date à convertir</param>
		/// <returns>Chaîne au format "yyyy-MM-dd HH:mm:ss"</returns>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static string FormatDateTime(DateTime date)
		{
			//REVIEW: PERF: faire une version optimisée?
			return date.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);
		}

		/// <summary>Convertit une date en une chaîne de caractères au format "yyyy-MM-dd"</summary>
		/// <param name="date">Date à convertir</param>
		/// <returns>Chaîne au format "yyyy-MM-dd"</returns>
		[Pure]
		public static string FormatDate(DateTime date)
		{
			//REVIEW: PERF: faire une version optimisée?
			return date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
		}

		/// <summary>Convertit une heure en une chaîne de caractères au format "hh:mm:ss"</summary>
		/// <param name="date">Heure à convertir</param>
		/// <returns>Chaîne au format "hh:mm:ss"</returns>
		[Pure]
		public static string FormatTime(DateTime date)
		{
			//REVIEW: PERF: faire une version optimisée?
			return date.ToString("HH:mm:ss", CultureInfo.InvariantCulture);
		}

		/// <summary>Convertit une chaîne de caractère au format "YYYY", "YYYYMM", "YYYYMMDD" ou "YYYYMMDDHHMMSS" en DateTime</summary>
		/// <param name="date">Chaîne de caractères à convertir</param>
		/// <returns>Objet DateTime correspondant, ou exception si incorrect</returns>
		/// <exception cref="System.ArgumentException">Si la date est incorrecte</exception>
		[Pure]
		public static DateTime ParseDateTime(string? date)
		{
			return ParseDateTime(date, null);
		}

		/// <summary>Convertit une chaîne de caractère au format "YYYY", "YYYYMM", "YYYYMMDD" ou "YYYYMMDDHHMMSS" en DateTime</summary>
		/// <param name="date">Chaîne de caractères à convertir</param>
		/// <param name="culture">Culture (pour le format attendu) ou null</param>
		/// <returns>Objet DateTime correspondant, ou exception si incorrect</returns>
		/// <exception cref="System.ArgumentException">Si la date est incorrecte</exception>
		[Pure]
		public static DateTime ParseDateTime(string? date, CultureInfo? culture)
		{
			if (!TryParseDateTime(date, culture, out DateTime result, true)) throw FailInvalidDateFormat();
			return result;
		}

		[Pure, MethodImpl(MethodImplOptions.NoInlining)]
		private static Exception FailInvalidDateFormat()
		{
			// ReSharper disable once NotResolvedInText
			return new ArgumentException("Invalid date format", "date");
		}

		/// <summary>Convertit une chaîne de caractère au format "YYYY", "YYYYMM", "YYYYMMDD" ou "YYYYMMDDHHMMSS" en DateTime</summary>
		/// <param name="date">Chaîne de caractères à convertir</param>
		/// <param name="dflt">Valeur par défaut</param>
		/// <returns>Objet DateTime correspondant, ou dflt si date est null ou vide</returns>
		[Pure]
		public static DateTime ParseDateTime(string? date, DateTime dflt)
		{
			if (string.IsNullOrEmpty(date)) return dflt;
			if (!TryParseDateTime(date, null, out DateTime result, false)) return dflt;
			return result;
		}

		/// <summary>Convertit une chaîne de caractère au format "YYYY", "YYYYMM", "YYYYMMDD" ou "YYYYMMDDHHMMSS" en DateTime</summary>
		/// <param name="date">Chaîne de caractères à convertir</param>
		/// <param name="dflt">Valeur par défaut</param>
		/// <param name="culture">Culture (pour le format attendu) ou null</param>
		/// <returns>Objet DateTime correspondant, ou dflt si date est null ou vide</returns>
		[Pure]
		public static DateTime ParseDateTime(string? date, DateTime dflt, CultureInfo? culture)
		{
			if (!TryParseDateTime(date, culture, out DateTime result, false)) return dflt;
			return result;
		}
		private static int ParseDateSegmentUnsafe(string source, int offset, int size)
		{
			// note: normalement le caller a déjà validé les paramètres
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

		/// <summary>Essayes de convertir une chaîne de caractères au format "YYYY", "YYYYMM", "YYYYMMDD" ou "YYYYMMDDHHMMSS" en DateTime</summary>
		/// <param name="date">Chaîne de caractères à convertir</param>
		/// <param name="culture">Culture (pour le format attendu) ou null</param>
		/// <param name="result">Date convertie (ou DateTime.MinValue en cas de problème)</param>
		/// <param name="throwsFail">Si false, absorbe les exceptions éventuelles. Si true, laisse les s'échaper</param>
		/// <returns>True si la date est correcte, false dans les autres cas</returns>
		[Pure]
		public static bool TryParseDateTime(string? date, CultureInfo? culture, out DateTime result, bool throwsFail)
		{
			result = DateTime.MinValue;

			if (date == null) { if (throwsFail) throw new ArgumentNullException(nameof(date)); else return false; }
			if (date.Length < 4) { if (throwsFail) throw new FormatException("Date '" + date + "' must be at least 4 characters long"); else return false; }
			//if (throwsFail) throw new FormatException("Date '"+date+"' must contains only digits"); else return false;
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
				{ // on va tenter un ParseExact ("Vendredi, 37 Trumaire 1789 à 3 heures moins le quart")
					result = DateTime.ParseExact(date, [ "D", "F", "f" ], culture ?? CultureInfo.InvariantCulture, DateTimeStyles.None);
					return true;
				}

				// Je vais tenter le jackpot, mon cher Julien!
				result = DateTime.Parse(date, culture ?? CultureInfo.InvariantCulture);
				return true;
			}
			catch (FormatException)
			{ // Dommage! La cagnotte est remise à la fois prochaine...
				if (throwsFail) throw;
				return false;
			}
			catch (ArgumentOutOfRangeException)
			{ // Pb sur un DateTime avec des dates invalides (31 février, ...)
				if (throwsFail) throw;
				return false;
			}
		}

		/// <summary>Essayes de convertir une chaîne de caractères au format "YYYY", "YYYYMM", "YYYYMMDD" ou "YYYYMMDDHHMMSS" en DateTime</summary>
		/// <param name="date">Chaîne de caractères à convertir</param>
		/// <param name="culture">Culture (pour le format attendu) ou null</param>
		/// <param name="result">Date convertie (ou DateTime.MinValue en cas de problème)</param>
		/// <param name="throwsFail">Si false, absorbe les exceptions éventuelles. Si true, laisse les s'échapper</param>
		/// <returns>True si la date est correcte, false dans les autres cas</returns>
		[Pure]
		public static bool TryParseInstant(string? date, CultureInfo? culture, out Instant result, bool throwsFail)
		{
			result = default(Instant);

			if (date == null)
			{
				if (throwsFail) throw new ArgumentNullException(nameof(date));
				return false;
			}
			if (date.Length < 4)
			{
				if (throwsFail) throw new FormatException("Date must be at least 4 characters long");
				return false;
			}
			if (!char.IsDigit(date[0]))
			{
				if (throwsFail) throw new FormatException("Date must contains only digits");
				return false;
			}
			try
			{
				switch (date.Length)
				{
					case 4:
					{ // YYYY -> YYYY/01/01 00:00:00.000
						int y = ParseDateSegmentUnsafe(date, 0, 4);
						if (y < 1 || y > 9999) break;
						result = Instant.FromUtc(y, 1, 1, 0, 0);
						return true;
					}
					case 6:
					{ // YYYYMM -> YYYY/MM/01 00:00:00.000
						int y = ParseDateSegmentUnsafe(date, 0, 4);
						if (y < 1 || y > 9999) break;
						int m = ParseDateSegmentUnsafe(date, 4, 2);
						if (m < 1 || m > 12) break;
						result = Instant.FromUtc(y, m, 1, 0, 0);
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
						result = Instant.FromUtc(y, m, d, 0, 0);
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
						result = Instant.FromUtc(y, m, d, h, n, s);
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
						result = Instant.FromUtc(y, m, d, h, n, s) + Duration.FromMilliseconds(f);
						return true;
					}
				}
			}
			catch (FormatException)
			{ // Dommage! La cagnotte est remise à la fois prochaine...
				if (throwsFail) throw;
				return false;
			}
			catch (ArgumentOutOfRangeException)
			{ // Pb sur un DateTime avec des dates invalides (31 février, ...)
				if (throwsFail) throw;
				return false;
			}
			if (throwsFail) throw new FormatException("Date must contains only digits");
			return false;
		}

		/// <summary>Convertit une heure "human friendly" en DateTime: "11","11h","11h00","11:00" -> {11:00:00.000}</summary>
		/// <param name="time">Chaîne contenant l'heure à convertir</param>
		/// <returns>Object DateTime contenant l'heure. La partie "date" est fixée à aujourd'hui</returns>
		[Pure]
		public static DateTime ParseTime(string time)
		{
			Contract.NotNullOrEmpty(time);

			time = time.ToLowerInvariant();

			int hour;
			int minute = 0;
			int second = 0;

			int p = time.IndexOf('h');
			if (p > 0)
			{
				hour = short.Parse(time.AsSpan(0, p));
				if (p + 1 >= time.Length)
				{
					minute = 0;
				}
				else
				{
					minute = short.Parse(time.AsSpan(p + 1));
				}
			}
			else
			{
				p = time.IndexOf(':');
				if (p > 0)
				{
					hour = short.Parse(time.AsSpan(0, p));
					if (p + 1 >= time.Length)
					{
						minute = 0;
					}
					else
					{
						minute = short.Parse(time.AsSpan(p + 1));
					}
				}
				else
				{
					hour = short.Parse(time);
				}
			}
			var d = DateTime.Today;
			return new DateTime(d.Year, d.Month, d.Day, hour, minute, second, 0);
		}

		#endregion

		#region Log Helpers...

		/// <summary>Conversion rapide d'une l'heure courante</summary>
		/// <param name="time">Heure à convertir</param>
		/// <returns>"hh:mm:ss.fff"</returns>
		[Pure]
		public static string FastFormatTime(DateTime time)
		{
			unsafe
			{
				// on alloue notre buffer sur la stack
				char* buffer = stackalloc char[16];
				FastFormatTimeUnsafe(buffer, time);
				return new string(buffer, 0, 12);
			}
		}

		/// <summary>Conversion rapide d'une l'heure courante (<c>hh:mm:ss.fff</c>)</summary>
		/// <param name="time">Heure à convertir</param>
		/// <param name="buffer">Buffer a utiliser pour le formattage</param>
		/// <param name="result">Si la fonction retourne <see langword="true"/>, contient le literal <c>hh:mm:ss.fff</c> formatté</param>
		/// <returns>Retourne <see langword="true"/> si le buffer était assez grand, <see langword="false"/> s'il fait moins de 12 chars</returns>
		public static bool TryFastFormatTime(DateTime time, Span<char> buffer, out ReadOnlySpan<char> result)
		{
			if (buffer.Length < 12)
			{
				result = default;
				return false;
			}

			unsafe
			{
				fixed (char* ptr = buffer)
				{
					FastFormatShortTimeUnsafe(ptr, time);
				}
				result = buffer.Slice(0, 12);
				return true;
			}
		}

		/// <summary>Conversion rapide d'une l'heure courante</summary>
		/// <param name="ptr">Pointer vers un buffer d'au moins 12 caractères</param>
		/// <param name="time">Heure à convertir</param>
		/// <returns>"hh:mm:ss.fff"</returns>
		public static unsafe void FastFormatTimeUnsafe(char* ptr, DateTime time)
		{
			// cf: http://geekswithblogs.net/akraus1/archive/2006/04/23/76146.aspx
			// cf: http://blogs.extremeoptimization.com/jeffrey/archive/2006/04/26/13824.aspx

			long ticks = time.Ticks;
			
			// Calculate values by getting the ms values first and do then
			// shave off the hour minute and second values with multiplications
			// and bit shifts instead of simple but expensive divisions.

			int ms = (int) ((ticks / 10000) % 86400000); // Get daytime in ms which does fit into an int
			int hour = (int) (Math.BigMul(ms >> 7, 9773437) >> 38); // well ... it works
			ms -= 3600000 * hour;
			int minute = (int) ((Math.BigMul(ms >> 5, 2290650)) >> 32);
			ms -= 60000 * minute;
			int second = ((ms >> 3) * 67109) >> 23;
			ms -= 1000 * second;

			// Hour
			int temp = (hour * 13) >> 7;  // 13/128 is nearly the same as /10 for values up to 65
			*ptr++ = (char) (temp + '0');
			*ptr++ = (char) (hour - 10 * temp + '0'); // Do subtract to get remainder instead of doing % 10
			*ptr++ = ':';

			// Minute
			temp = (minute * 13) >> 7;   // 13/128 is nearly the same as /10 for values up to 65
			*ptr++ = (char) (temp + '0');
			*ptr++ = (char) (minute - 10 * temp + '0'); // Do subtract to get remainder instead of doing % 10
			*ptr++ = ':';

			// Second
			temp = (second * 13) >> 7; // 13/128 is nearly the same as /10 for values up to 65
			*ptr++ = (char) (temp + '0');
			*ptr++ = (char) (second - 10 * temp + '0');
			*ptr++ = '.';

			// Millisecond
			temp = (ms * 41) >> 12;   // 41/4096 is nearly the same as /100
			*ptr++ = (char) (temp + '0');

			ms -= 100 * temp;
			temp = (ms * 205) >> 11;  // 205/2048 is nearly the same as /10
			*ptr++ = (char) (temp + '0');

			ms -= 10 * temp;
			*ptr = (char) (ms + '0');
		}

		/// <summary>Conversion rapide d'une l'heure courante</summary>
		/// <param name="time">Heure à convertir</param>
		/// <returns>"hh:mm:ss"</returns>
		[Pure]
		public static string FastFormatShortTime(DateTime time)
		{
			unsafe
			{
				// on alloue notre buffer sur la stack
				char* buffer = stackalloc char[8];
				FastFormatShortTimeUnsafe(buffer, time);
				return new string(buffer, 0, 8);
			}
		}

		/// <summary>Conversion rapide d'une l'heure courante (<c>hh:mm:ss</c>)</summary>
		/// <param name="time">Heure à convertir</param>
		/// <param name="buffer">Buffer a utiliser pour le formattage</param>
		/// <param name="result">Si la fonction retourne <see langword="true"/>, contient le literal <c>hh:mm:ss</c> formatté</param>
		/// <returns>Retourne <see langword="true"/> si le buffer était assez grand, <see langword="false"/> s'il fait moins de 8 chars</returns>
		public static bool TryFastFormatShortTime(DateTime time, Span<char> buffer, out ReadOnlySpan<char> result)
		{
			if (buffer.Length < 8)
			{
				result = default;
				return false;
			}

			unsafe
			{
				fixed (char* ptr = buffer)
				{
					FastFormatShortTimeUnsafe(ptr, time);
				}
				result = buffer.Slice(0, 8);
				return true;
			}
		}

		/// <summary>Conversion rapide d'une l'heure courante</summary>
		/// <param name="ptr">Pointer vers un buffer d'au moins 8 caractères</param>
		/// <param name="time">Heure à convertir</param>
		/// <returns>"hh:mm:ss"</returns>
		public static unsafe void FastFormatShortTimeUnsafe(char* ptr, DateTime time)
		{
			// cf: http://geekswithblogs.net/akraus1/archive/2006/04/23/76146.aspx
			// cf: http://blogs.extremeoptimization.com/jeffrey/archive/2006/04/26/13824.aspx

			long ticks = time.Ticks;
			
			// Calculate values by getting the ms values first and do then
			// shave off the hour minute and second values with multiplications
			// and bit shifts instead of simple but expensive divisions.

			int ms = (int) ((ticks / 10000) % 86400000); // Get daytime in ms which does fit into an int
			int hour = (int) (Math.BigMul(ms >> 7, 9773437) >> 38); // well ... it works
			ms -= 3600000 * hour;
			int minute = (int) ((Math.BigMul(ms >> 5, 2290650)) >> 32);
			ms -= 60000 * minute;
			int second = ((ms >> 3) * 67109) >> 23;

			// Hour
			int temp = (hour * 13) >> 7;  // 13/128 is nearly the same as /10 for values up to 65
			*ptr++ = (char) (temp + '0');
			*ptr++ = (char) (hour - 10 * temp + '0'); // Do subtract to get remainder instead of doing % 10
			*ptr++ = ':';

			// Minute
			temp = (minute * 13) >> 7;   // 13/128 is nearly the same as /10 for values up to 65
			*ptr++ = (char) (temp + '0');
			*ptr++ = (char) (minute - 10 * temp + '0'); // Do subtract to get remainder instead of doing % 10
			*ptr++ = ':';

			// Second
			temp = (second * 13) >> 7; // 13/128 is nearly the same as /10 for values up to 65
			*ptr++ = (char) (temp + '0');
			*ptr = (char) (second - 10 * temp + '0');
		}

		/// <summary>Conversion rapide de la date courante au format international (YYYY-MM-DD)</summary>
		/// <param name="date">Date à convertir</param>
		/// <returns>"YYYY-MM-DD"</returns>
		[Pure]
		public static string FastFormatDate(DateTime date)
		{
			// ATTENTION: cette fonction ne peut formatter que des années entre 1000 et 2999 !
			unsafe
			{
				char* buffer = stackalloc char[12]; // on n'utilise que 10 chars
				FastFormatDateUnsafe(buffer, date);
				return new string(buffer, 0, 10);
			}
		}

		/// <summary>Conversion rapide de la date courante au format international (YYYY-MM-DD)</summary>
		/// <param name="date">Date à convertir</param>
		/// <param name="buffer">Buffer a utiliser pour le formattage</param>
		/// <param name="result">Si la fonction retourne <see langword="true"/>, contient le literal <c>YYYY-MM-DD</c> formatté</param>
		/// <returns>Retourne <see langword="true"/> si le buffer était assez grand, <see langword="false"/> s'il fait moins de 12 chars</returns>
		public static bool TryFastFormatDate(DateTime date, Span<char> buffer, out ReadOnlySpan<char> result)
		{
			if (buffer.Length < 12)
			{
				result = default;
				return false;
			}

			unsafe
			{
				fixed (char* ptr = buffer)
				{
					FastFormatDateUnsafe(ptr, date);
				}
				result = buffer.Slice(0, 12);
				return true;
			}
		}

		/// <summary>Conversion rapide de la date courante au format international (YYYY-MM-DD)</summary>
		/// <param name="ptr">Pointer vers un buffer d'au moins 10 caractères</param>
		/// <param name="date">Date à convertir</param>
		/// <returns>"YYYY-MM-DD"</returns>
		public static unsafe void FastFormatDateUnsafe(char* ptr, DateTime date)
		{
			int y = date.Year;
			int m = date.Month;
			int d = date.Day;

			#region YEAR
			// on va d'abord afficher le 1xxx ou le 2xxx (désolé si vous êtes en l'an 3000 !)
			if (y < 2000)
			{
				ptr[0] = '1';
				y -= 1000;
			}
			else
			{
				ptr[0] = '2';
				y -= 2000; // <-- Y3K BUG HERE
			}
			// ensuite pour les centaines, on utilise la même technique que pour formatter les millisecondes
			int temp = (y * 41) >> 12;   // 41/4096 is nearly the same as /100
			ptr[1] = (char) (temp + '0');

			y -= 100 * temp;
			temp = (y * 205) >> 11;  // 205/2048 is nearly the same as /10
			ptr[2] = (char) (temp + '0');

			y -= 10 * temp;
			ptr[3] = (char) (y + '0');
			ptr[4] = '-';
			#endregion

			#region MONTH
			temp = (m * 13) >> 7;  // 13/128 is nearly the same as /10 for values up to 65
			ptr[5] = (char) (temp + '0');
			ptr[6] = (char) (m - 10 * temp + '0'); // Do subtract to get remainder instead of doing % 10
			ptr[7] = '-';
			#endregion

			#region DAY
			temp = (d * 13) >> 7;   // 13/128 is nearly the same as /10 for values up to 65
			ptr[8] = (char) (temp + '0');
			ptr[9] = (char) (d - 10 * temp + '0'); // Do subtract to get remainder instead of doing % 10
			#endregion
		}

		/// <summary>Conversion rapide de la date courante au format court (MM-DD)</summary>
		/// <param name="date">Date à convertir</param>
		/// <returns>"MM-DD"</returns>
		[Pure]
		public static string FastFormatShortDate(DateTime date)
		{
			unsafe
			{
				char* buffer = stackalloc char[8]; // on n'utilise que 5 chars
				FastFormatShortDateUnsafe(buffer, date);
				return new string(buffer, 0, 5);
			}
		}

		/// <summary>Conversion rapide de la date courante au format court (MM-DD)</summary>
		/// <param name="date">Date à convertir</param>
		/// <param name="buffer">Buffer a utiliser pour le formattage</param>
		/// <param name="result">Si la fonction retourne <see langword="true"/>, contient le literal <c>MM-DD</c> formatté</param>
		/// <returns>Retourne <see langword="true"/> si le buffer était assez grand, <see langword="false"/> s'il fait moins de 5 chars</returns>
		public static bool TryFastFormatShortDate(DateTime date, Span<char> buffer, out ReadOnlySpan<char> result)
		{
			if (buffer.Length < 5)
			{
				result = default;
				return false;
			}

			unsafe
			{
				fixed (char* ptr = buffer)
				{
					FastFormatDateUnsafe(ptr, date);
				}
				result = buffer.Slice(0, 5);
				return true;
			}
		}

		/// <summary>Conversion rapide de la date courante au format court (MM-DD)</summary>
		/// <param name="ptr">Pointer vers un buffer d'au moins 5 caractères</param>
		/// <param name="date">Date à convertir</param>
		/// <returns>"MM-DD"</returns>
		public static unsafe void FastFormatShortDateUnsafe(char* ptr, DateTime date)
		{
			int m = date.Month;
			int d = date.Day;

			int temp = (m * 13) >> 7;  // 13/128 is nearly the same as /10 for values up to 65
			*ptr++ = (char) (temp + '0');
			*ptr++ = (char) (m - 10 * temp + '0'); // Do subtract to get remainder instead of doing % 10
			*ptr++ = '-';

			temp = (d * 13) >> 7;   // 13/128 is nearly the same as /10 for values up to 65
			*ptr++ = (char) (temp + '0');
			*ptr = (char) (d - 10 * temp + '0'); // Do subtract to get remainder instead of doing % 10
		}

		/// <summary>Formate une durée en chaîne compacte ("30s", "45min", "8h32m")</summary>
		/// <param name="duration">Durée à formater</param>
		/// <returns>Forme affichable de la durée (minutes arrondies au supérieur)</returns>
		[Pure]
		public static string FormatDuration(TimeSpan duration)
		{
			long d = (long) Math.Ceiling(duration.TotalSeconds);
			if (d == 0) return "0s"; //TODO: WMLiser KTL.FormatDuration!
			if (d <= 60) return d.ToString(CultureInfo.InvariantCulture) + "s";
			if (d < 3600) return "~" + ((long) Math.Round(duration.TotalMinutes + 0.2, MidpointRounding.AwayFromZero)).ToString(CultureInfo.InvariantCulture) + "min";
			if (d <= 86400) return ((long) Math.Floor(duration.TotalHours)).ToString(CultureInfo.InvariantCulture) + "h" + ((duration.Minutes >= 1) ? (((long) Math.Ceiling((double) duration.Minutes)).ToString("D2") + "m") : String.Empty);
			if (d < 259200) return "~" + ((long) Math.Round(duration.TotalHours, MidpointRounding.AwayFromZero)).ToString(CultureInfo.InvariantCulture) + "h";
			return "~" + ((long) Math.Floor(duration.TotalDays)).ToString(CultureInfo.InvariantCulture) + "d" + (duration.Hours > 0 ? duration.Hours + "h" : "");
		}

		#endregion

	}

}
