
namespace Doxense
{
	using System;
	using System.Globalization;
	using System.Runtime.CompilerServices;
	using Doxense.Diagnostics.Contracts;
	using JetBrains.Annotations;

	internal static class StringConverters
	{
		#region Numbers...

		//NOTE: ces méthodes ont été importées de KTL/Sioux
		//REVIEW: je ne sais pas si c'est la meilleure place pour ce code?

		/// <summary>Table de lookup pour les nombres entre 0 et 99, afin d'éviter d'allouer une string inutilement</summary>
		//note: vu que ce sont des literals, ils sont interned automatiquement
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

		/// <summary>Convertit un entier en chaîne, de manière optimisée</summary>
		/// <param name="value">Valeure entière à convertir</param>
		/// <returns>Version chaîne</returns>
		/// <remarks>Cette fonction essaye d'évite le plus possibles des allocations mémoire</remarks>
		[Pure, NotNull]
		public static string ToString(int value)
		{
			var cache = StringConverters.SmallNumbers;
			return value >= 0 && value < cache.Length ? cache[value] : value.ToString(NumberFormatInfo.InvariantInfo);
		}

		/// <summary>Convertit un entier en chaîne, de manière optimisée</summary>
		/// <param name="value">Valeure entière à convertir</param>
		/// <returns>Version chaîne</returns>
		/// <remarks>Cette fonction essaye d'évite le plus possibles des allocations mémoire</remarks>
		[Pure, NotNull]
		public static string ToString(uint value)
		{
			var cache = StringConverters.SmallNumbers;
			return value < cache.Length ? cache[value] : value.ToString(NumberFormatInfo.InvariantInfo);
		}

		/// <summary>Convertit un entier en chaîne, de manière optimisée</summary>
		/// <param name="value">Valeure entière à convertir</param>
		/// <returns>Version chaîne</returns>
		/// <remarks>Cette fonction essaye d'évite le plus possibles des allocations mémoire</remarks>
		[Pure, NotNull]
		public static string ToString(long value)
		{
			var cache = StringConverters.SmallNumbers;
			return value >= 0 && value < cache.Length ? cache[(int) value] : value.ToString(NumberFormatInfo.InvariantInfo);
		}

		/// <summary>Convertit un entier en chaîne, de manière optimisée</summary>
		/// <param name="value">Valeure entière à convertir</param>
		/// <returns>Version chaîne</returns>
		/// <remarks>Cette fonction essaye d'évite le plus possibles des allocations mémoire</remarks>
		[Pure, NotNull]
		public static string ToString(ulong value)
		{
			var cache = StringConverters.SmallNumbers;
			return value < (ulong) cache.Length ? cache[(int) value] : value.ToString(NumberFormatInfo.InvariantInfo);
		}

		/// <summary>Convertit un décimal en chaîne, de manière optimisée</summary>
		/// <param name="value">Valeure décimale à convertir</param>
		/// <returns>Version chaîne</returns>
		/// <remarks>Cette fonction essaye d'évite le plus possibles des allocations mémoire</remarks>
		[Pure, NotNull]
		public static string ToString(float value)
		{
			long x = unchecked((long) value);
			// ReSharper disable once CompareOfFloatsByEqualityOperator
			return x != value
					   ? value.ToString("R", CultureInfo.InvariantCulture)
					   : (x >= 0 && x < StringConverters.SmallNumbers.Length ? StringConverters.SmallNumbers[(int) x] : x.ToString(NumberFormatInfo.InvariantInfo));
		}

		/// <summary>Convertit un décimal en chaîne, de manière optimisée</summary>
		/// <param name="value">Valeure décimale à convertir</param>
		/// <returns>Version chaîne</returns>
		/// <remarks>Cette fonction essaye d'évite le plus possibles des allocations mémoire</remarks>
		[Pure, NotNull]
		public static string ToString(double value)
		{
			long x = unchecked((long)value);
			// ReSharper disable once CompareOfFloatsByEqualityOperator
			return x != value
					   ? value.ToString("R", CultureInfo.InvariantCulture)
					   : (x >= 0 && x < StringConverters.SmallNumbers.Length ? StringConverters.SmallNumbers[(int)x] : x.ToString(NumberFormatInfo.InvariantInfo));
		}

		/// <summary>Convertit une chaîne en booléen</summary>
		/// <param name="value">Chaîne de texte (ex: "true")</param>
		/// <param name="dflt">Valeur par défaut si vide ou invalide</param>
		/// <returns>Valeur booléenne correspondant (ex: true) ou valeur par défaut</returns>
		/// <remarks>Les valeurs pour true sont "true", "yes", "on", "1".
		/// Les valeurs pour false sont "false", "no", "off", "0", ou tout le reste
		/// null et chaîne vide sont considérés comme false
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

		/// <summary>Convertit une chaîne en booléen</summary>
		/// <param name="value">Chaîne de texte (ex: "true")</param>
		/// <returns>Valeur booléenne correspondant (ex: true) ou null</returns>
		/// <remarks>Les valeurs pour true sont "true", "yes", "on", "1".
		/// Les valeurs pour false sont "false", "no", "off", "0"
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

		/// <summary>Convertit un entier jusqu'au prochain séparateur (ou fin de buffer). A utilisé pour simuler un Split</summary>
		/// <param name="buffer">Buffer de caractères</param>
		/// <param name="offset">Offset courant dans le buffer</param>
		/// <param name="length"></param>
		/// <param name="separator">Séparateur attendu entre les ints</param>
		/// <param name="defaultValue">Valeur par défaut retournée si erreur</param>
		/// <param name="result">Récupère le résultat de la conversion</param>
		/// <param name="newpos">Récupère la nouvelle position (après le séparateur)</param>
		/// <returns>true si int chargé, false si erreur (plus de place, incorrect, ...)</returns>
		/// <exception cref="System.ArgumentNullException">Si buffer est null</exception>
		public static unsafe bool FastTryGetInt([NotNull] char* buffer, int offset, int length, char separator, int defaultValue, out int result, out int newpos)
		{
			Contract.PointerNotNull(buffer, nameof(buffer));
			result = defaultValue;
			newpos = offset;
			if (offset < 0 || offset >= length) return false; // deja a la fin !!

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
				return false; // deja le separateur, ou pas un digit == WARNING: le curseur ne sera pas avancé!
			}
			int res = c - 48;
			offset++;
			// il y a au moins 1 digit, parcourt les suivants
			while (offset < length)
			{
				c = buffer[offset++];
				if (c == separator) break;
				if (!char.IsDigit(c))
				{ // va jusqu'au prochain séparator
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
		/// <param name="separator">Séparateur attendu entre les ints</param>
		/// <param name="defaultValue">Valeur par défaut retournée si erreur</param>
		/// <param name="result">Récupère le résultat de la conversion</param>
		/// <param name="newpos">Récupère la nouvelle position (après le séparateur)</param>
		/// <returns>true si int chargé, false si erreur (plus de place, incorrect, ...)</returns>
		/// <exception cref="System.ArgumentNullException">Si buffer est null</exception>
		public static unsafe bool FastTryGetLong([NotNull] char* buffer, int offset, int length, char separator, long defaultValue, out long result, out int newpos)
		{
			Contract.PointerNotNull(buffer, nameof(buffer));
			result = defaultValue;
			newpos = offset;
			if (offset < 0 || offset >= length) return false; // deja a la fin !!

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
				return false; // deja le separateur, ou pas un digit == WARNING: le curseur ne sera pas avancé!
			}
			int res = c - 48;
			offset++;
			// il y a au moins 1 digit, parcourt les suivants
			while (offset < length)
			{
				c = buffer[offset++];
				if (c == separator) break;
				if (!char.IsDigit(c))
				{ // va jusqu'au prochain séparator
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

		/// <summary>Convertit une chaîne en entier (int)</summary>
		/// <param name="value">Chaîne de caractère (ex: "1234")</param>
		/// <param name="defaultValue">Valeur par défaut si vide ou invalide</param>
		/// <returns>Entier correspondant ou valeur par défaut si pb (ex: 1234)</returns>
		[Pure]
		public static int ToInt32(string value, int defaultValue)
		{
			if (string.IsNullOrEmpty(value)) return defaultValue;
			// optimisation: si premier carac pas chiffre, exit
			char c = value[0];
			if (value.Length == 1) return char.IsDigit(c) ? c - 48 : defaultValue;
			if (!char.IsDigit(c) && c != '-' && c != '+' && c != ' ') return defaultValue;
			return int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int res) ? res : defaultValue;
		}

		/// <summary>Convertit une chaîne en entier (int)</summary>
		/// <param name="value">Chaîne de caractère (ex: "1234")</param>
		/// <returns>Entier correspondant ou null si pb (ex: 1234)</returns>
		[Pure]
		public static int? ToInt32(string value)
		{
			if (string.IsNullOrEmpty(value)) return default(int?);
			// optimisation: si premier carac pas chiffre, exit
			char c = value[0];
			if (value.Length == 1) return char.IsDigit(c) ? (c - 48) : default(int?);
			if (!char.IsDigit(c) && c != '-' && c != '+' && c != ' ') return default(int?);
			return int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int res) ? res : default(int?);
		}

		/// <summary>Convertit une chaîne en entier (long)</summary>
		/// <param name="value">Chaîne de caractère (ex: "1234")</param>
		/// <param name="defaultValue">Valeur par défaut si vide ou invalide</param>
		/// <returns>Entier correspondant ou valeur par défaut si pb (ex: 1234)</returns>
		[Pure]
		public static long ToInt64(string value, long defaultValue)
		{
			if (string.IsNullOrEmpty(value)) return defaultValue;
			// optimisation: si premier carac pas chiffre, exit
			char c = value[0];
			if (value.Length == 1) return char.IsDigit(c) ? ((long) c - 48) : defaultValue;
			if (!char.IsDigit(c) && c != '-' && c != '+' && c != ' ') return defaultValue;
			return long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out long res) ? res : defaultValue;
		}

		/// <summary>Convertit une chaîne en entier (long)</summary>
		/// <param name="value">Chaîne de caractère (ex: "1234")</param>
		/// <returns>Entier correspondant ou null si pb (ex: 1234)</returns>
		[Pure]
		public static long? ToInt64(string value)
		{
			if (string.IsNullOrEmpty(value)) return default(long?);
			// optimisation: si premier carac pas chiffre, exit
			char c = value[0];
			if (value.Length == 1) return char.IsDigit(c) ? ((long) c - 48) : default(long?);
			if (!char.IsDigit(c) && c != '-' && c != '+' && c != ' ') return default(long?);
			return long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out long res) ? res : default(long?);
		}

		/// <summary>Convertit une chaine de caractère en double, quelque soit la langue locale (utilise le '.' comme séparateur décimal)</summary>
		/// <param name="value">Chaine (ex: "1.0", "123.456e7")</param>
		/// <param name="defaultValue">Valeur par défaut si problème de conversion ou null</param>
		/// <param name="culture">Culture (par défaut InvariantCulture)</param>
		/// <returns>Double correspondant</returns>
		[Pure]
		public static double ToDouble(string value, double defaultValue, IFormatProvider culture = null)
		{
			if (string.IsNullOrEmpty(value)) return defaultValue;
			char c = value[0];
			if (!char.IsDigit(c) && c != '+' && c != '-' && c != '.' && c != ' ') return defaultValue;
			if (culture == null) culture = CultureInfo.InvariantCulture;
			if (culture == CultureInfo.InvariantCulture && value.IndexOf(',') >= 0) value = value.Replace(',', '.');
			return double.TryParse(value, NumberStyles.Float | NumberStyles.AllowThousands, culture, out double result) ? result : defaultValue;
		}

		[Pure]
		public static double? ToDouble(string value, IFormatProvider culture = null)
		{
			if (value == null) return default(double?);
			double result = ToDouble(value, double.NaN, culture);
			return double.IsNaN(result) ? default(double?) : result;
		}

		/// <summary>Convertit une chaine de caractère en float, quelque soit la langue locale (utilise le '.' comme séparateur décimal)</summary>
		/// <param name="value">Chaine (ex: "1.0", "123.456e7")</param>
		/// <param name="defaultValue">Valeur par défaut si problème de conversion ou null</param>
		/// <param name="culture">Culture (par défaut InvariantCulture)</param>
		/// <returns>Float correspondant</returns>
		[Pure]
		public static float ToSingle(string value, float defaultValue, IFormatProvider culture = null)
		{
			if (string.IsNullOrEmpty(value)) return defaultValue;
			char c = value[0];
			if (!char.IsDigit(c) && c != '+' && c != '-' && c != '.' && c != ' ') return defaultValue;
			if (culture == null) culture = CultureInfo.InvariantCulture;
			if (culture == CultureInfo.InvariantCulture && value.IndexOf(',') >= 0) value = value.Replace(',', '.');
			return float.TryParse(value, NumberStyles.Float | NumberStyles.AllowThousands, culture, out float result) ? result : defaultValue;
		}

		[Pure]
		public static float? ToSingle(string value, IFormatProvider culture = null)
		{
			if (value == null) return default(float?);
			float result = ToSingle(value, float.NaN, culture);
			return double.IsNaN(result) ? default(float?) : result;
		}

		/// <summary>Convertit une chaine de caractère en double, quelque soit la langue locale (utilise le '.' comme séparateur décimal)</summary>
		/// <param name="value">Chaine (ex: "1.0", "123.456e7")</param>
		/// <param name="defaultValue">Valeur par défaut si problème de conversion ou null</param>
		/// <param name="culture">Culture (par défaut InvariantCulture)</param>
		/// <returns>Décimal correspondant</returns>
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

		/// <summary>Convertit une chaine en DateTime</summary>
		/// <param name="value">Date à convertir</param>
		/// <param name="defaultValue">Valeur par défaut</param>
		/// <param name="culture"></param>
		/// <returns>Voir StringConverters.ParseDateTime()</returns>
		[Pure]
		public static DateTime ToDateTime(string value, DateTime defaultValue, CultureInfo culture = null)
		{
			return ParseDateTime(value, defaultValue, culture);
		}

		/// <summary>Convertit une chaine en DateTime</summary>
		/// <param name="value">Date à convertir</param>
		/// <param name="culture"></param>
		/// <returns>Voir StringConverters.ParseDateTime()</returns>
		[Pure]
		public static DateTime? ToDateTime(string value, CultureInfo culture = null)
		{
			if (string.IsNullOrEmpty(value)) return default(DateTime?);
			DateTime result = ParseDateTime(value, DateTime.MaxValue, culture);
			return result == DateTime.MaxValue ? default(DateTime?) : result;
		}

		/// <summary>Convertit une chaine de caractères en GUID</summary>
		/// <param name="value">Chaine (ex: "123456-789")</param>
		/// <param name="defaultValue">Valeur par défaut si problème de conversion ou null</param>
		/// <returns>GUID correspondant</returns>
		[Pure]
		public static Guid ToGuid(string value, Guid defaultValue)
		{
			if (string.IsNullOrEmpty(value)) return defaultValue;
			return Guid.TryParse(value, out Guid result) ? result : defaultValue;
		}

		[Pure]
		public static Guid? ToGuid(string value)
		{
			if (string.IsNullOrEmpty(value)) return default(Guid?);
			return Guid.TryParse(value, out Guid result) ? result : default(Guid?);
		}

		/// <summary>Convertit une chaine de caractères en Enum</summary>
		/// <typeparam name="TEnum">Type de l'Enum</typeparam>
		/// <param name="value">Chaine (ex: "Red", "2", ...)</param>
		/// <param name="defaultValue">Valeur par défaut si problème de conversion ou null</param>
		/// <returns>Valeur de l'enum correspondante</returns>
		/// <remarks>Accepte les valeures sous forme textuelle ou numérique, case insensitive</remarks>
		[Pure]
		public static TEnum ToEnum<TEnum>(string value, TEnum defaultValue)
			where TEnum : struct, IComparable, IConvertible, IFormattable
		{
			if (string.IsNullOrEmpty(value)) return defaultValue;
			return Enum.TryParse<TEnum>(value, true, out TEnum result) ? result : defaultValue;
		}

		[Pure]
		public static TEnum? ToEnum<TEnum>(string value)
			where TEnum : struct, IComparable, IConvertible, IFormattable
		{
			if (string.IsNullOrEmpty(value)) return default(TEnum?);
			return Enum.TryParse<TEnum>(value, true, out TEnum result) ? result : default(TEnum?);
		}

		#endregion

		#region Dates...

		/// <summary>Convertit une date en une chaine de caractères au format "YYYYMMDDHHMMSS"</summary>
		/// <param name="date">Date à formater</param>
		/// <returns>Date formatée sur 14 caractères au format YYYYMMDDHHMMSS</returns>
		[Pure, NotNull, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static string ToDateTimeString(DateTime date)
		{
			//REVIEW: PERF: faire une version optimisée?
			return date.ToString("yyyyMMddHHmmss", CultureInfo.InvariantCulture);
		}

		/// <summary>Convertit une date en une chaine de caractères au format "AAAAMMJJ"</summary>
		/// <param name="date">Date à formater</param>
		/// <returns>Date formatée sur 8 caractères au format AAAAMMJJ</returns>
		[Pure, NotNull, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static string ToDateString(DateTime date)
		{
			//REVIEW: PERF: faire une version optimisée?
			return date.ToString("yyyyMMdd", CultureInfo.InvariantCulture);
		}

		/// <summary>Convertit un heure en une chaine de caractères au format "HHMMSS"</summary>
		/// <param name="date">Date à formater</param>
		/// <returns>Heure formatée sur 6 caractères au format HHMMSS</returns>
		[Pure, NotNull, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static string ToTimeString(DateTime date)
		{
			//REVIEW: PERF: faire une version optimisée?
			return date.ToString("HHmmss", CultureInfo.InvariantCulture);
		}

		/// <summary>Convertit une date en une chaine de caractères au format "yyyy-MM-dd HH:mm:ss"</summary>
		/// <param name="date">Date à convertir</param>
		/// <returns>Chaine au format "yyyy-MM-dd HH:mm:ss"</returns>
		[Pure, NotNull, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static string FormatDateTime(DateTime date)
		{
			//REVIEW: PERF: faire une version optimisée?
			return date.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);
		}

		/// <summary>Convertit une date en une chaine de caractères au format "yyyy-MM-dd"</summary>
		/// <param name="date">Date à convertir</param>
		/// <returns>Chaine au format "yyyy-MM-dd"</returns>
		[Pure, NotNull]
		public static string FormatDate(DateTime date)
		{
			//REVIEW: PERF: faire une version optimisée?
			return date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
		}

		/// <summary>Convertit une heure en une chaine de caractères au format "hh:mm:ss"</summary>
		/// <param name="date">Heure à convertir</param>
		/// <returns>Chaine au format "hh:mm:ss"</returns>
		[Pure, NotNull]
		public static string FormatTime(DateTime date)
		{
			//REVIEW: PERF: faire une version optimisée?
			return date.ToString("HH:mm:ss", CultureInfo.InvariantCulture);
		}

		/// <summary>Convertit une chaine de caractère au format "YYYY", "YYYYMM", "YYYYMMDD" ou "YYYYMMDDHHMMSS" en DateTime</summary>
		/// <param name="date">Chaine de caractères à convertir</param>
		/// <returns>Objet DateTime correspondant, ou exception si incorrect</returns>
		/// <exception cref="System.ArgumentException">Si la date est incorrecte</exception>
		[Pure]
		public static DateTime ParseDateTime(string date)
		{
			return ParseDateTime(date, null);
		}

		/// <summary>Convertit une chaine de caractère au format "YYYY", "YYYYMM", "YYYYMMDD" ou "YYYYMMDDHHMMSS" en DateTime</summary>
		/// <param name="date">Chaine de caractères à convertir</param>
		/// <param name="culture">Culture (pour le format attendu) ou null</param>
		/// <returns>Objet DateTime correspondant, ou exception si incorrect</returns>
		/// <exception cref="System.ArgumentException">Si la date est incorrecte</exception>
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

		/// <summary>Convertit une chaine de caractère au format "YYYY", "YYYYMM", "YYYYMMDD" ou "YYYYMMDDHHMMSS" en DateTime</summary>
		/// <param name="date">Chaine de caractères à convertir</param>
		/// <param name="dflt">Valeur par défaut</param>
		/// <returns>Objet DateTime correspondant, ou dflt si date est null ou vide</returns>
		[Pure]
		public static DateTime ParseDateTime(string date, DateTime dflt)
		{
			if (string.IsNullOrEmpty(date)) return dflt;
			if (!TryParseDateTime(date, null, out DateTime result, false)) return dflt;
			return result;
		}

		/// <summary>Convertit une chaine de caractère au format "YYYY", "YYYYMM", "YYYYMMDD" ou "YYYYMMDDHHMMSS" en DateTime</summary>
		/// <param name="date">Chaine de caractères à convertir</param>
		/// <param name="dflt">Valeur par défaut</param>
		/// <param name="culture">Culture (pour le format attendu) ou null</param>
		/// <returns>Objet DateTime correspondant, ou dflt si date est null ou vide</returns>
		[Pure]
		public static DateTime ParseDateTime(string date, DateTime dflt, CultureInfo culture)
		{
			if (!TryParseDateTime(date, culture, out DateTime result, false)) return dflt;
			return result;
		}
		private static int ParseDateSegmentUnsafe(string source, int offset, int size)
		{
			// note: normalement le caller a déja validé les paramètres
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

		/// <summary>Essayes de convertir une chaine de caratères au format "YYYY", "YYYYMM", "YYYYMMDD" ou "YYYYMMDDHHMMSS" en DateTime</summary>
		/// <param name="date">Chaine de caractères à convertir</param>
		/// <param name="culture">Culture (pour le format attendu) ou null</param>
		/// <param name="result">Date convertie (ou DateTime.MinValue en cas de problème)</param>
		/// <param name="throwsFail">Si false, absorbe les exceptions éventuelles. Si true, laisse les s'échaper</param>
		/// <returns>True si la date est correcte, false dans les autres cas</returns>
		[Pure]
		public static bool TryParseDateTime(string date, CultureInfo culture, out DateTime result, bool throwsFail)
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
				{ // on va tenter un ParseExact ("Vendredi, 37 Trumaire 1789 à 3 heures moint le quart")
					result = DateTime.ParseExact(date, new[] { "D", "F", "f" }, culture ?? CultureInfo.InvariantCulture, DateTimeStyles.None);
					return true;
				}

				// Je vais tenter le jackpot, mon cher Julien!
				result = DateTime.Parse(date, culture ?? CultureInfo.InvariantCulture);
				return true;
			}
			catch (FormatException)
			{ // Dommage! La cagnote est remise à la fois prochaine...
				if (throwsFail) throw;
				return false;
			}
			catch (ArgumentOutOfRangeException)
			{ // Pb sur un DateTime avec des dates invalides (31 février, ...)
				if (throwsFail) throw;
				return false;
			}
		}

		/// <summary>Convertit une heure "human friendly" en DateTime: "11","11h","11h00","11:00" -> {11:00:00.000}</summary>
		/// <param name="time">Chaine contenant l'heure à convertir</param>
		/// <returns>Object DateTime contenant l'heure. La partie "date" est fixée à aujourd'hui</returns>
		[Pure]
		public static DateTime ParseTime([NotNull] string time)
		{
			Contract.NotNullOrEmpty(time, nameof(time));

			time = time.ToLowerInvariant();

			int hour;
			int minute = 0;
			int second = 0;

			int p = time.IndexOf('h');
			if (p > 0)
			{
				hour = System.Convert.ToInt16(time.Substring(0, p));
				if (p + 1 >= time.Length)
					minute = 0;
				else
					minute = System.Convert.ToInt16(time.Substring(p + 1));
			}
			else
			{
				p = time.IndexOf(':');
				if (p > 0)
				{
					hour = System.Convert.ToInt16(time.Substring(0, p));
					if (p + 1 >= time.Length)
						minute = 0;
					else
						minute = System.Convert.ToInt16(time.Substring(p + 1));
				}
				else
				{
					hour = System.Convert.ToInt16(time);
				}
			}
			var d = DateTime.Today;
			return new DateTime(d.Year, d.Month, d.Day, hour, minute, second, 0);
		}

		#endregion
	}
}
