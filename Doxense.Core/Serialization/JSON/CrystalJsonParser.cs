#region Copyright (c) 2005-2023 Doxense SAS
//
// All rights are reserved. Reproduction or transmission in whole or in part, in
// any form or by any means, electronic, mechanical or otherwise, is prohibited
// without the prior written consent of the copyright owner.
//
#endregion

#define ENABLE_TIMO_STRING_CONVERTER
//#define ENABLE_GRISU3_STRING_CONVERTER // work in progress

//#define DEBUG_JSON_PARSER
//#define DEBUG_JSON_BINDER

namespace Doxense.Serialization.Json
{
	using System;
	using System.Collections.Generic;
	using System.Globalization;
	using System.Text;
	using Doxense.Diagnostics.Contracts;
	using Doxense.Text;
	using Doxense.Tools;
	using JetBrains.Annotations;

	internal enum JsonLiteralKind
	{
		Field,
		Value,
		Integer, // nombre entier "simple" plus grand (ie: "123", "+123" ou "-123", pas de décimales ou d'exposant)
		Decimal // nombres décimaux ("1.234"), ou en représentation scientifique ("1234E-3")
	}

	internal enum JsonTokenType
	{
		Invalid = 0,
		Object, // '{'
		Array,  // '['
		String, // '"'
		Number, // '-+0123456789'
		Null,   // 'n'
		True,   // 't'
		False,  // 'f'
		Special, // 'NI' (NaN or Infinity)
	}

	public static class CrystalJsonParser
	{
		internal const char EndOfStream = '\xFFFF'; // == -1

		internal static readonly bool[] WhiteCharsMap = ComputeWhiteCharMap();

		internal const int WHITE_CHAR_MAP = 33; // 0..32

		[Pure]
		private static bool[] ComputeWhiteCharMap()
		{
			var map = new bool[WHITE_CHAR_MAP];
			map['\t'] = true;
			map['\r'] = true;
			map['\n'] = true;
			map[' '] = true;
			return map;
		}

		internal  static readonly char[] NullToken = "ull".ToCharArray();
		internal  static readonly char[] TrueToken = "rue".ToCharArray();
		internal static readonly char[] FalseToken = "alse".ToCharArray();

		internal const int TOKEN_TYPE_LENGTH = 128;
		internal static readonly JsonTokenType[] TokenMap = ComputeTokenTypeMap();

		private static JsonTokenType[] ComputeTokenTypeMap()
		{
			var map = new JsonTokenType[TOKEN_TYPE_LENGTH];
			map['{'] = JsonTokenType.Object; // { ... }
			map['['] = JsonTokenType.Array; // [ ... ]
			map['"'] = JsonTokenType.String; // "..."
			map['n'] = JsonTokenType.Null; // null
			map['t'] = JsonTokenType.True; // true
			map['f'] = JsonTokenType.False; // false
			map['N'] = JsonTokenType.Special; // NaN
			map['I'] = JsonTokenType.Special; // Infinity
			for (int i = 0; i <= 9; i++) map['0' + i] = JsonTokenType.Number;
			map['+'] = JsonTokenType.Number; // +###
			map['-'] = JsonTokenType.Number; // -###
			return map;
		}

		internal static JsonNumber? ParseJsonNumber(string? literal)
		{
#if DEBUG_JSON_PARSER
			Debug.WriteLine("CrystalJsonParser.ParseJsonNumber('{0}')", (object)literal);
#endif
			if (string.IsNullOrEmpty(literal)) return null;

			const int MAX_NUMBER_CHARS = 64;
			if (literal.Length > MAX_NUMBER_CHARS) throw new ArgumentException("Buffer is too large for a numeric value");

			bool negative = false;
			bool hasDot = false;
			bool hasExponent = false;
			bool hasExponentSign = false;
			bool incomplete = true;
			bool computed = true;
			ulong num = 0;
			int p = 0;
			foreach (char c in literal)
			{
				++p;
				if (c <= '9' && c >= '0')
				{ // digit
					incomplete = false;
					num = (num * 10) + (ulong) (c - '0');
					continue;
				}

				if (c == '.')
				{
					if (hasDot) throw InvalidNumberFormat(literal, "duplicate decimal point");
					incomplete = true;
					hasDot = true;
					computed = false;
					continue;
				}

				if (c == 'e' || c == 'E')
				{
					if (hasExponent) throw InvalidNumberFormat(literal, "duplicate exponent");
					incomplete = true;
					hasExponent = true;
					computed = false;
					continue;
				}

				if (c == '-' || c == '+')
				{
					if (p == 1)
					{
						negative = c == '-';
					}
					else
					{
						if (!hasExponent) throw InvalidNumberFormat(literal, "unexpected sign at this location");
						if (hasExponentSign) throw InvalidNumberFormat(literal, "duplicate sign is exponent");
						hasExponentSign = true;
					}
					incomplete = true;
					continue;
				}

				if (c == 'I')
				{ // +Infinity / -Infinity ?
					if (string.Equals(literal, "+Infinity", StringComparison.Ordinal)) return JsonNumber.PositiveInfinity;
					if (string.Equals(literal, "-Infinity", StringComparison.Ordinal)) return JsonNumber.NegativeInfinity;
				}
				if (c == 'N')
				{
					if (string.Equals(literal, "NaN", StringComparison.Ordinal)) return JsonNumber.NaN;
				}
				// charactère invalide après un nombre
				throw InvalidNumberFormat(literal, $"unexpected character '{c}' found)");
			}

			if (incomplete)
			{ // normalement cela doit toujours finir par un digit !
				throw InvalidNumberFormat(literal, "truncated");
			}

			// si on n'a pas vu de points pour d'exposant, et que le nombre de digits <= 16, on est certain que c'est un entier valide, et donc on l'a déja parsé
			if (computed)
			{
				if (literal.Length < 4)
				{
					if (num == 0) return JsonNumber.Zero; //REVIEW: est-ce qu'on doit gérer "-0" ?
					if (num == 1) return negative ? JsonNumber.MinusOne : JsonNumber.One;
					if (!negative)
					{
						// le literal est-il en cache?
						if (num <= JsonNumber.CACHED_SIGNED_MAX) return JsonNumber.GetCachedSmallNumber((int) num);
					}
					else
					{
						if (num <= -JsonNumber.CACHED_SIGNED_MIN) return JsonNumber.GetCachedSmallNumber(-((int) num));
					}
				}

				return !negative
					? JsonNumber.ParseUnsigned(num, literal)
					: JsonNumber.ParseSigned(-((long) num), literal); // avec seulement 16 digits, pas de risques d'overflow a cause du signe
			}

			// on a besoin de parser le nombre...
			var value = ParseNumberFromLiteral(literal, negative, hasDot, hasExponent);
			if (value == null) throw InvalidNumberFormat(literal, "malformed");
			return value;
		}

		internal static JsonNumber? ParseNumberFromLiteral(string literal, bool negative, bool hasDot, bool hasExponent)
		{
			var styles = NumberStyles.AllowLeadingSign;
			if (hasExponent) styles |= NumberStyles.AllowExponent;

			if (!hasDot)
			{
				//on sait si c'est négatif, donc on peut directement tenter ulong
				if (!negative)
				{ // unsigned
					if (ulong.TryParse(literal, styles, NumberFormatInfo.InvariantInfo, out var u64))
					{
						return JsonNumber.ParseUnsigned(u64, literal);
					}
				}
				else
				{ // signed
					if (long.TryParse(literal, styles, NumberFormatInfo.InvariantInfo, out var s64))
					{
						return JsonNumber.ParseSigned(s64, literal);
					}
				}
			}
			else
			{ // decimal, mais pas forcément
				styles |= NumberStyles.AllowDecimalPoint;
				// maybe it fits in a double..?
				if (double.TryParse(literal, styles, NumberFormatInfo.InvariantInfo, out var dbl))
				{
					//TODO: detecter si c'est quand même un entier ?
					return JsonNumber.Parse(dbl, literal);
				}
			}

			// use decimal has the last resort fallback...
			if (decimal.TryParse(literal, styles, NumberFormatInfo.InvariantInfo, out var dec))
			{
				return JsonNumber.Parse(dec, literal);
			}

			// no luck ...
			return null;
		}

		[Pure]
		private static FormatException InvalidNumberFormat(string literal, string reason)
		{
			return new FormatException($"Invalid number '{literal}.' ({reason})");
		}

		/// <summary>Indique si la string PEUT être une date au format ISO 8601</summary>
		/// <param name="value">Chaine candidate</param>
		/// <returns>True si la string ressemble (de loin) à une date ISO</returns>
		[Pure]
		internal static bool CouldBeIso8601DateTime(string value)
		{
			// cherche les marqueurs '-' 'T' et ':'
			// la fin doit etre 'Z' si UTC ou alors '+##:##' ou '-##:##'
			return value.Length >= 20 && value[4] == '-' && value[7] == '-' && value[10] == 'T' && value[13] == ':' && value[16] == ':' && (value[value.Length - 1] == 'Z' || (value[value.Length - 3] == ':' && (value[value.Length - 6] == '+' || value[value.Length - 6] == '-')));
		}

		[Pure, ContractAnnotation("value:null => false")]
		internal static bool TryParseIso8601DateTime(string value, out DateTime result)
		{
#if DEBUG_JSON_PARSER
			Debug.WriteLine("CrystalJsonConverter.TryParseMicrosoftDateTime(" + value +")");
#endif
			result = DateTime.MinValue;

			if (string.IsNullOrEmpty(value) || !CouldBeIso8601DateTime(value)) return false;

			// cf http://msdn.microsoft.com/en-us/library/bb882584.aspx
			return DateTime.TryParse(value, DateTimeFormatInfo.InvariantInfo, DateTimeStyles.RoundtripKind, out result);
		}

		[Pure, ContractAnnotation("value:null => false")]
		internal static bool TryParseIso8601DateTimeOffset(string value, out DateTimeOffset result)
		{
#if DEBUG_JSON_PARSER
			Debug.WriteLine("CrystalJsonConverter.TryParseMicrosoftDateTime(" + value +")");
#endif
			result = DateTimeOffset.MinValue;

			if (string.IsNullOrEmpty(value) || !CouldBeIso8601DateTime(value)) return false;

			// cf http://msdn.microsoft.com/en-us/library/bb882584.aspx
			return DateTimeOffset.TryParse(value, DateTimeFormatInfo.InvariantInfo, DateTimeStyles.RoundtripKind, out result);
		}

		[Pure]
		internal static bool CouldBeJsonMicrosoftDateTime(string value)
		{
			return value.Length >= 9 && value[0] == '/' && value[1] == 'D' && value[2] == 'a' && value[3] == 't' && value[4] == 'e' && value[5] == '(' && value[value.Length - 2] == ')' && value[value.Length - 1] == '/';
		}

		[Pure, ContractAnnotation("value:null => false")]
		internal static bool TryParseMicrosoftDateTime(string value, out DateTime result, out TimeSpan? tz)
		{
#if DEBUG_JSON_PARSER
			Debug.WriteLine("CrystalJsonConverter.TryParseMicrosoftDateTime(" + value +")");
#endif
			result = DateTime.MinValue;
			tz = null;

			if (string.IsNullOrEmpty(value) || !CouldBeJsonMicrosoftDateTime(value)) return false;

			//Note: la chaine de texte est déja décodée, donc "\/Date(...)\/" devient "/Date(...)/"
			// Microsoft: "/Date(ticks)/" ou "/Date(ticks+HHMM)/"

			bool isLocal = false;
			//TODO: ajouter un Settings "ForceDateToLocalTime" ?
			int endOffset = value.Length - 2; // 2 = ")/".Length
											  // il faut regarder s'il y a une timezone
			char c = value[endOffset - 5];
			if (c == '+' || c == '-')
			{ // on dirait qu'il y a une TimeZone de spécifiée
			  // TODO: vérifier s'il y a bien 4 digits juste derrière ?
				isLocal = true;
				endOffset -= 5;
			}
			if (!long.TryParse(value.Substring(6, endOffset - 6), out var ticks)) // 6 = "/Date(".Length
				return false;

			// note: il y a un "bug" dans les sérialisateurs de Microsoft: MinValue/MaxValue sont en LocalTime, et donc
			// une fois convertis en UTC, ils sont décallés (par ex le nb de ticks est négatif si on est à l'est de GMT, ou légèrement positif si on est à l'ouest)
			// Pour contrecarrer ça, on "arrondi" à Min ou Max si les ticks sont à moins d'un jour des bornes.

			const int MillisecondsPerDay = 86400 * 1000;
			if (ticks < -62135596800000 + MillisecondsPerDay)
			{ // MinValue
				result = DateTime.MinValue;
			}
			else if (ticks > 253402300799999 - MillisecondsPerDay)
			{ // MaxValue
				result = DateTime.MaxValue;
			}
			else
			{
				DateTime date = CrystalJson.JavaScriptTicksToDate(ticks);
				if (isLocal)
				{
					// pb: Local ici sera en fct de la TZ du serveur, et non pas celle indiquée dans le JSON
					// vu que de toutes manières, les ticks sont en UTC, et qu'on ne peut pas créer un DateTime de type Local dans une autre TZ que la notre,
					// on ne peut pas faire grand chose avec la TZ spécifiée, hormis la garder en mémoire si jamais on doit binder vers un DateTimeOffset
					if (!int.TryParse(value.Substring(endOffset + 1, value.Length - endOffset - 3), out var offset))
						return false;
					// on a l'offset en "BCD", il faut retransformer en nombre de minutes
					int h = offset / 100;
					int m = offset % 100;
					if (h > 12 || m > 59) return false;
					offset = h * 60 + m;
					tz = (c == '-') ? TimeSpan.FromMinutes(-offset) : TimeSpan.FromMinutes(offset);
				}
				result = date;
			}
			return true;
		}

		#region Deserialization...

		/// <summary>Désérialise une classe ou une struct</summary>
		public static object? DeserializeCustomClassOrStruct(JsonObject data, Type type, ICrystalJsonTypeResolver resolver)
		{
			if (type == typeof(object) || type.IsInterface || type.IsClass)
			{ // il faut regarder dans les propriétés de l'objet pour obtenir le type

				string? customClass = data.CustomClassName;
				if (customClass != null)
				{
					#region Mitigation pour DOX-430
					// note: c'est n'est PAS un fix long terme qui corrige 100% le problème!
					// => le but ici est d'endiguer les payloads les plus classiques qui seraient utilisées par des attaquants qui scannent rapidement a la recherche de services vulnérable et espérer qu'ils passent leur chemin
					// => un attaquant qui ciblerait spécifiquement cette librairie en ayant le code source sous la main pourra probablement trouver un des nombres types vulnérables du .NET Framework comme proxy pour ce genre d'attaque

					// Black list de types dangereux (et qui n'ont rien a faire dans des données JSON de toutes manières!)
					if (customClass.StartsWith("System.", StringComparison.OrdinalIgnoreCase) &&
						(  customClass.StartsWith("System.Windows.Data.ObjectDataProvider", StringComparison.OrdinalIgnoreCase) // Cet objet peut exécuter n'importe quelle méthode de n'importe quel instance!
						|| customClass.StartsWith("System.Management.Automation.", StringComparison.OrdinalIgnoreCase) // techniquement c'est PSObjectXYZ mais on ban le namespace entier
						|| customClass.StartsWith("System.Security.Principal.WindowsIdentity", StringComparison.OrdinalIgnoreCase) // c'est chaud mais ce type est dangereux
						|| customClass.StartsWith("System.IdentityModel.Tokens.", StringComparison.OrdinalIgnoreCase) // vulnérabilité dans les secure session tokens
						|| customClass.StartsWith("System.Windows.ResourceDictionary", StringComparison.OrdinalIgnoreCase) // un dictionnaire de resources peut contenir des types dangereux
						|| customClass.StartsWith("System.Configuration.Install.", StringComparison.OrdinalIgnoreCase) // les Assembly loaders peuvent loader n'importe quel code dans le process!
						|| customClass.StartsWith("System.Workflow.ComponentModel.Serialization.", StringComparison.OrdinalIgnoreCase) // ActivitySurrogate* peuvent compiler n'importe quel fichier .cs ou exécuter n'importe méthode d'objets
						|| customClass.StartsWith("System.Activities.Presentation.WorkflowDesigner.", StringComparison.OrdinalIgnoreCase) // 
						|| customClass.StartsWith("System.Resources.ResXResource", StringComparison.OrdinalIgnoreCase)) // les ressources peuvent contenir des types arbitraires!
					)
					{
						throw CrystalJson.Errors.Binding_CannotDeserializeCustomTypeBadType(data, customClass);
					}

					#endregion

#if DEBUG_JSON_BINDER
					Debug.WriteLine("DeserializeCustomClassOrStruct(..., " + type+") : object has custom class " + customClass);
#endif
					// retrouve le type correspondant
					var customType = resolver.ResolveClassId(customClass);
					if (customType == null)
					{
						throw CrystalJson.Errors.Binding_CannotDeserializeCustomTypeNoConcreteClassFound(data, type, customClass);
					}

					if (type.IsSealed)
					{
						// si le type attendu est sealed, alors la seule solution est que le type spécifié match EXACTEMENT celui attendu!
						if (type != customType)
						{
							throw CrystalJson.Errors.Binding_CannotDeserializeCustomTypeIncompatibleType(data, type, customClass);
						}
					}
					
					if (type != typeof(object))
					{ 
						// si le type est donné par l'appelant, on veut vérifier que c'est compatible avec le type attendu
						if (!type.IsAssignableFrom(customType))
						{ // le type donnée par l'appelant n'est pas compatible... c'est peut être une erreur, ou alors une tentative de hack!
							throw CrystalJson.Errors.Binding_CannotDeserializeCustomTypeIncompatibleType(data, type, customClass);
						}
					}
					type = customType;
				}

				if (type == typeof(object))
				{
					return data.ToExpando();
				}
			}

			// récupère les infos sur l'objet
			var typeDef = resolver.ResolveJsonType(type);
			if (typeDef == null)
			{ // uhoh, pas normal du tout
				throw CrystalJson.Errors.Binding_CannotDeserializeCustomTypeNoTypeDefinition(data, type);
			}

			if (typeDef.CustomBinder != null)
			{ // il y a un custom binder, qui va se charger de créer et remplir l'objet
			  // => automatiquement le cas pour les IJsonSerializable, ou alors c'est un custom binder fourni par par un CustomTypeResolver
				return typeDef.CustomBinder(data, type, resolver);
			}

			if (typeDef.Generator == null)
			{ // sans générateur, on ne peut pas créer d'instance !
				throw CrystalJson.Errors.Binding_CannotDeserializeCustomTypeNoBinderOrGenerator(data, type);
			}

			// crée une nouvelle instance de la class/struct
			object instance;
			try
			{
				instance = typeDef.Generator();
			}
			catch (Exception e) when (!e.IsFatalError())
			{ // problème lors de la création de l'objet? cela arrive lorsqu'on crée un objet via Reflection, et qu'il n'a pas de constructeur par défaut...
				throw CrystalJson.Errors.Binding_FailedToConstructTypeInstanceErrorOccurred(data, type, e);
			}

			if (instance == null)
			{ // uhoh ? le générateur n'a rien retourné, ce qui peut arriver quand on essaye de faire un "new Interface()" ou un "new AbstractClass()"...
				throw CrystalJson.Errors.Binding_FailedToConstructTypeInstanceReturnedNull(data, type);
			}

			// traiement des membres
			foreach (var member in typeDef.Members)
			{
				// certains champs sont "readonly"...
				if (member.ReadOnly) continue;

				// obtient la valeur du dictionnaire
				if (!data.TryGetValue(member.Name, out var child))
					continue; // absent (donc sera égal à la valeur par défaut)

				// s'il existe mais qu'il contient la valeur par défaut, on le skip aussi
				if (child.IsNull)
					continue; // valeur par défaut ?

				// "Transcode" le type JSON vers le type réel du membre (JSON number => int, JSON string => Guid, ...)
				if (member.Binder == null) throw CrystalJson.Errors.Binding_CannotDeserializeCustomTypeNoReaderForMember(child, member, type);
				object? value;
				try
				{
					value = member.Binder(child, member.Type, resolver);
				}
				catch(Exception e)
				{
					// Pour aider a tracker le path vers la valeur qui cause pb, on va re-wrap l'exception!
					if (e is JsonBindingException jbex)
					{
						string path = jbex.Path != null ? (member.Name + "." + jbex.Path) : member.Name;
						throw new JsonBindingException($"Cannot bind member '{typeDef.Type.GetFriendlyName()}.{member.Name}': {jbex.Message}", path, jbex.Value, jbex.InnerException);
					}
					throw new JsonBindingException($"Cannot bind member '{typeDef.Type.GetFriendlyName()}.{member.Name}' of type '{member.Type.GetFriendlyName()}': [{e.GetType().GetFriendlyName()}] {e.Message}", member.Name, child, e);
				}

				// Ecrit la valeur dans le champ correspondant
				if (member.Setter == null) throw CrystalJson.Errors.Binding_CannotDeserializeCustomTypeNoBinderForMember(child, member, type);
				try
				{
					member.Setter(instance, value);
				}
				catch(Exception e)
				{
					throw new JsonBindingException($"Cannot assign member '{instance.GetType().GetFriendlyName()}.{member.Name}' of type '{member.Type.GetFriendlyName()}' with value of type '{(value?.GetType().GetFriendlyName() ?? "<null>")}': [{e.GetType().GetFriendlyName()}] {e.Message}", member.Name, child, e);
				}
			}

			return instance;
		}

		#endregion

	}

	public static class CrystalJsonParser<TReader>
		where TReader : struct, IJsonReader
	{

		#region Parsing...

		/// <summary>Parse le prochain token JSON dans le reader</summary>
		/// <returns>Token parsé, ou null si le reader est arrivé en fin de stream</returns>
		public static JsonValue? ParseJsonValue(ref CrystalJsonTokenizer<TReader> reader)
		{
#if DEBUG_JSON_PARSER
			System.Diagnostics.Debug.WriteLine("CrystalJsonConverter.ParseJsonValue(...)");
#endif
			char first = reader.ReadNextToken();

			var map = CrystalJsonParser.TokenMap;
			if (first < CrystalJsonParser.TOKEN_TYPE_LENGTH)
			{
				switch (map[first])
				{
					case JsonTokenType.Object:
					{
						return ParseJsonObject(ref reader);
					}
					case JsonTokenType.Array:
					{
						return ParseJsonArray(ref reader);
					}
					case JsonTokenType.Null:
					{ // null
						reader.ReadExpectedKeyword(CrystalJsonParser.NullToken);
						return JsonNull.Null;
					}
					case JsonTokenType.True:
					{ // true
						reader.ReadExpectedKeyword(CrystalJsonParser.TrueToken);
						return JsonBoolean.True;
					}
					case JsonTokenType.False:
					{ // false
						reader.ReadExpectedKeyword(CrystalJsonParser.FalseToken);
						return JsonBoolean.False;
					}
					case JsonTokenType.String:
					{ // string
						return ParseJsonStringOrDateTime(ref reader);
					}
					case JsonTokenType.Number:
					{ // number
						return ParseJsonNumber(ref reader, first);
					}
					case JsonTokenType.Special:
					{ // NaN ou Infinity ?
						return ParseSpecialKeyword(ref reader, first);
					}
				}
			}

			if (first == CrystalJsonParser.EndOfStream)
			{ // on considère que c'est null
				return null;
				//REVIEW: ca serait peut être mieux d'avoir un "TryParseJsonValue(...)"?
				// => on pourrait éviter de devoir retourner 'null' pour signifier "end of stream"
			}

			// c'est une erreur de syntaxe
			switch (first)
			{
				case '}':
				{ // erreur de syntaxe: fermeture d'un objet non ouvert ?
					throw reader.FailInvalidSyntax("Unexpected '}' encountered without corresponding '{'");
				}
				case ']':
				{ // erreur de syntaxe: fermeture d'une array non onverte ?
					throw reader.FailInvalidSyntax("Unexpected ']' encountered without corresponding '['");
				}
				case ',':
				{ // erreur de syntaxe: séparateur ne faisant pas partie d'un [] ou d'un {} ?
					throw reader.FailInvalidSyntax("Unexpected separator encountered outside of an array or an object");
				}
				default:
				{ // ??
					throw reader.FailInvalidSyntax("Unexpected character '{0}'", first);
				}
			}
		}

		private static JsonValue ParseJsonStringOrDateTime(ref CrystalJsonTokenizer<TReader> reader)
		{
			string value = ParseJsonStringInternal(ref reader, reader.GetStringTable(JsonLiteralKind.Value));
#if DEBUG_JSON_PARSER
			System.Diagnostics.Debug.WriteLine("CrystalJsonConverter.ParseJsonStringOrDateTime(" + value + ")");
#endif
			//TODO: gérer aussi un cache des *JsonString* elle mêmes, dans le cas où la même chaine est répétée plusieurs fois ?
			return JsonString.Return(value);
		}

		private static string ParseJsonName(ref CrystalJsonTokenizer<TReader> reader)
		{
#if DEBUG_JSON_PARSER
			System.Diagnostics.Debug.WriteLine("CrystalJsonConverter.ParseJsonName(...)");
#endif
			return ParseJsonStringInternal(ref reader, reader.GetStringTable(JsonLiteralKind.Field));
		}

		private static unsafe string ParseJsonStringInternal(ref CrystalJsonTokenizer<TReader> reader, StringTable? table)
		{
			// note: on a déja lu la quote (")

			// Optimisation: pour éviter d'allouer un StringBuilder inutilement, on fonctionne d'abord avec un buffer de 16 charactères sur la stack et on switch sur un StringBuilder si ce buffer est trop petit
			// Donc a la fin de la boucle, on est dans trois cas possibles:
			// * si p = 0, la chaine vide
			// * si sb == null, elle tient dans le buffer sur la stack et fait 'p' caractères de long
			// * sinon, elle est contenue dans le StringBuilder

			const int SIZE = 128;
			char* chunk = stackalloc char[SIZE];
			int hashCode = StringTable.Hash.FNV_OFFSET_BIAS;
			StringBuilder? sb = null;
			int p = 0;

			while (true)
			{
				char c = reader.ReadOne();

				// Probabilités décroissantes:
				// > du texte classique
				// > des espaces
				// > un \ d'escaping
				// > le " terminal
				// > EOF (cas très rare)

				if (c == '"') break; // doit être évalué AVANT de parser un '\"'
				if (c == '\\')
				{ // décode le caractère encodé
					c = ParseEscapedCharacter(ref reader);
				}
				if (c == CrystalJsonParser.EndOfStream) throw reader.FailUnexpectedEndOfStream("String is incomplete");

				if (p < SIZE & sb == null)
				{ // il y a encore de la place dans le buffer
					chunk[p++] = c;
				}
				else
				{
					if (sb == null)
					{
						// il est plein, on le dump dans le StringBuilder
						sb = new StringBuilder(SIZE * 2);
						// et on copie le chunk dans le builder
						sb.Append(new string(chunk, 0, p));
					}
					sb.Append(c);
				}
				hashCode = StringTable.Hash.CombineFnvHash(hashCode, c);
			}

			if (p == 0) return String.Empty;
			//TODO: table for single letter names ?

			if (table != null)
			{ // interning
				//REVIEW: est-qu'il faut "fermer" le hashcode? (en xoring avant la taille de la chaine, par exemple?)
				return sb == null ? table.Add(hashCode, new ReadOnlySpan<char>(chunk, p)) : table.Add(hashCode, sb);
			}
			else
			{
				return sb?.ToString() ?? new string(chunk, 0, p);
			}

		}

		private static char ParseEscapedCharacter(ref CrystalJsonTokenizer<TReader> reader)
		{
			char c = reader.ReadOne();
			switch (c)
			{
				case '\"':
				case '\\':
				case '/':
					return c; // on a deja le bon char

				case 'b': return '\b';
				case 'f': return '\f';
				case 'n': return '\n';
				case 'r': return '\r';
				case 't': return '\t';
				case 'u': return ParseEscapedUnicodeCharacter(ref reader);

				case CrystalJsonParser.EndOfStream:
					throw reader.FailUnexpectedEndOfStream("Invalid string escaping");

				default:
					throw reader.FailInvalidSyntax(@"Invalid escaped character \{0} found in string", c);
			}
		}

		private static char ParseEscapedUnicodeCharacter(ref CrystalJsonTokenizer<TReader> reader)
		{
			// Format: \uXXXX   où XXXX = hexa
			int x = 0;
			for (int i = 0; i < 4; i++)
			{
				char c = reader.ReadOne();
				if (c >= '0' && c <= '9')
					x = (x << 4) | (c - 48);
				else if (c >= 'A' && c <= 'F')
					x = (x << 4) | (c - 55);
				else if (c >= 'a' && c <= 'f')
					x = (x << 4) | (c - 87);
				else if (c == CrystalJsonParser.EndOfStream)
					throw reader.FailUnexpectedEndOfStream("Invalid Unicode character escaping");
				else
					throw reader.FailInvalidSyntax("Invalid Unicode character escaping");
			}
			return (char)x;
		}

		private static unsafe JsonNumber ParseJsonNumber(ref CrystalJsonTokenizer<TReader> reader, char first)
		{
#if DEBUG_JSON_PARSER
			System.Diagnostics.Debug.WriteLine("CrystalJsonConverter.ParseJsonNumber(...)");
#endif
			const int MAX_NUMBER_CHARS = 64;

			char* buffer = stackalloc char[MAX_NUMBER_CHARS];
			buffer[0] = first;
			int p = 1;
			bool negative = first == '-';
			bool hasDot = false;
			bool hasExponent = false;
			bool hasExponentSign = false;
			bool incomplete = first < '0' || first > '9';
			bool computed = negative || !incomplete;
			ulong num = incomplete ? 0 : (ulong)(first - '0');
			while (p < MAX_NUMBER_CHARS) // protection contre un trop grand consommation de mémoire
			{
				char c = reader.ReadOne();
				if (c <= '9' && c >= '0') //TODO: utiliser un bias (uint)
				{ // digit
					incomplete = false;
					num = (num * 10) + (ulong)(c - '0');
					//REVIEW: fail if more than 17 digits? (ulong.MaxValue) unless we want to handle BigIntegers?
				}
				else if (c == ',' || c == '}' || c == ']' || c == ' ' || c == '\n' || c == '\t' || c == '\r')
				{ // c'est un caractère valide pour une fin de stream
				  // rembobine le caractère lu
					reader.Push(c);
					break;
				}
				else if (c == '.')
				{
					if (hasDot) throw reader.FailInvalidSyntax("Invalid number '{0}.' (duplicate decimal point)", new string(buffer, 0, p));
					incomplete = true;
					hasDot = true;
					computed = false;
				}
				else if (c == 'e' || c == 'E')
				{ // exposant (forme scientifique)
					if (hasExponent) throw reader.FailInvalidSyntax("Invalid number '{0}{1}' (duplicate exponent)", new string(buffer, 0, p), c);
					incomplete = true;  // doit être suivit d'un signe ou d'un digit! ("123E" n'est pas valid)
					hasExponent = true;
					computed = false;
				}
				else if (c == '-' || c == '+')
				{ // signe de l'exposant
					if (!hasExponent) throw reader.FailInvalidSyntax("Invalid number '{0}{1}' (unexpected sign at this location)", new string(buffer, 0, p), c);
					if (hasExponentSign) throw reader.FailInvalidSyntax("Invalid number '{0}{1}' (duplicate sign is exponent)", new string(buffer, 0, p), c);
					incomplete = true; // doit être suivit d'un digit! ("123E-" n'est pas valid)
					hasExponentSign = true;
				}
				else if (c == 'I' && p == 1 && (first == '+' || first == '-'))
				{ // '+Infinity' / '-Infinity' ?
					ParseSpecialKeyword(ref reader, c);
					//HACKHACK: si ca réussi, c'est que le mot clé était bien "Infinity"
					return negative ? JsonNumber.NegativeInfinity : JsonNumber.PositiveInfinity;
				}
				else if (c == CrystalJsonParser.EndOfStream)
				{ // fin de stream
					break;
				}
				else
				{ // caractère invalide après un nombre
					throw reader.FailInvalidSyntax("Invalid number '{0}' (unexpected character '{1}' found)", new string(buffer, 0, p), c);
				}

				buffer[p++] = c;
			}

			if (incomplete)
			{ // normalement cela doit toujours finir par un digit !
				throw reader.FailInvalidSyntax("Invalid JSON number (truncated)");
			}

			// si on n'a pas vu de points pour d'exposant, et que le nombre de digits <= 16, on est certain que c'est un entier valide, et donc on l'a déja parsé
			if (computed && p <= 4)
			{
				if (num == 0) return JsonNumber.Zero;
				if (num == 1) return negative ? JsonNumber.MinusOne : JsonNumber.One;
				if (!negative)
				{
					// le literal est-il en cache?
					if (num <= JsonNumber.CACHED_SIGNED_MAX) return JsonNumber.GetCachedSmallNumber((int) num);
				}
				else
				{
					// le literal est-il en cache?
					if (num <= -JsonNumber.CACHED_SIGNED_MIN) return JsonNumber.GetCachedSmallNumber(-((int) num));
				}
			}

			// génère le literal
			// on considère qu'un nombre de 4 ou plus digits est une "valeur", alors qu'en dessous, c'est une "clé"
			var table = reader.GetStringTable(computed ? JsonLiteralKind.Integer : JsonLiteralKind.Decimal);
			string? literal = computed ? null
				: table != null ? table.Add(new ReadOnlySpan<char>(buffer, p))
				: new string(buffer, 0, p);

			if (computed)
			{
				if (negative)
				{ // avec seulement 16 digits, pas de risques d'overflow a cause du signe
					return JsonNumber.ParseSigned(-((long)num), literal);
				}
				else
				{
					return JsonNumber.ParseUnsigned(num, literal);
				}
			}

			// on a besoin de parser le nombre...
			var value = CrystalJsonParser.ParseNumberFromLiteral(literal, negative, hasDot, hasExponent);
			if (value == null) throw reader.FailInvalidSyntax("Invalid JSON number '{0}' (malformed)", literal);
			return value;
		}

		private static JsonValue ParseSpecialKeyword(ref CrystalJsonTokenizer<TReader> reader, char first)
		{
#if DEBUG_JSON_PARSER
			System.Diagnostics.Debug.WriteLine("CrystalJsonConverter.ParseJsonNumber(...)");
#endif

			// lit un literal de type "NaN", "Infinity", etc...

			char c = first;

			var sb = StringBuilderCache.Acquire();
			sb.Append(c);

			switch (first)
			{
				case 'N':
				{ // NaN
					sb.Append(c = reader.ReadOne());
					if (c != 'a') break;
					sb.Append(c = reader.ReadOne());
					if (c != 'N') break;
					return JsonNumber.NaN;
				}
				case 'I':
				{ // Infinity
					sb.Append(c = reader.ReadOne());
					if (c != 'n') break;
					sb.Append(c = reader.ReadOne());
					if (c != 'f') break;
					sb.Append(c = reader.ReadOne());
					if (c != 'i') break;
					sb.Append(c = reader.ReadOne());
					if (c != 'n') break;
					sb.Append(c = reader.ReadOne());
					if (c != 'i') break;
					sb.Append(c = reader.ReadOne());
					if (c != 't') break;
					sb.Append(c = reader.ReadOne());
					if (c != 'y') break;
					return JsonNumber.PositiveInfinity;
				}
			}

			if (sb[sb.Length - 1] == CrystalJsonParser.EndOfStream)
			{
				sb.Length = sb.Length - 1;
			}

			throw reader.FailInvalidSyntax("Invalid literal '{0}'", StringBuilderCache.GetStringAndRelease(sb));
		}

		private static JsonObject ParseJsonObject(ref CrystalJsonTokenizer<TReader> reader)
		{
#if DEBUG_JSON_PARSER
			System.Diagnostics.Debug.WriteLine("CrystalJsonConverter.ParseJsonObject(...) [BEGIN]");
#endif

			var props = reader.AcquireObjectBuffer();
			Contract.Debug.Assert(props != null);

			try
			{
				int index = 0;

				const int EXPECT_PROPERTY = 0; // Expect a string that contains a name of a new property, or '}' to close the object
				const int EXPECT_VALUE = 1; // Expect a ':' followed by the value of the current property
				const int EXPECT_NEXT = 2; // Expect a ',' to start next property, or '}' to close the object
				int state = EXPECT_PROPERTY;

				char c = '\0';
				string name = null;
				while (true)
				{
					char prev = c;
					switch (c = reader.ReadNextToken())
					{
						case '"':
						{ // start of property name
							if (state != EXPECT_PROPERTY)
							{
								if (state == EXPECT_VALUE) throw reader.FailInvalidSyntax("Missing colon after field #{0} value", index + 1);
								throw reader.FailInvalidSyntax("Missing comma after field #{0}", index);
							}

							name = ParseJsonName(ref reader);
							// next should be ':'
							state = EXPECT_VALUE;
							break;
						}
						case '}':
						{ // end of object
							if (state != EXPECT_NEXT)
							{
								if (state == EXPECT_PROPERTY && prev == ',' && reader.Settings.DenyTrailingCommas)
									throw reader.FailInvalidSyntax("Missing field before end of object");
								if (state == EXPECT_VALUE)
									throw reader.FailInvalidSyntax("Missing value for field #{0} at the end of object definition", index);
							}
#if DEBUG_JSON_PARSER
							System.Diagnostics.Debug.WriteLine("CrystalJsonConverter.ParseJsonObject(...) [END] read " + map.Count + " fields");
#endif

							var obj = new JsonObject(props, index, reader.FieldComparer);
							if (obj.Count != index && !reader.Settings.OverwriteDuplicateFields)
							{
								var x = new HashSet<string>(reader.FieldComparer);
								for (int i = 0; i < index; i++)
								{
									if (!x.Add(props[i].Key)) throw reader.FailInvalidSyntax($"Duplicate field '{props[i].Key}' in JSON Object.");
								}
							}
							return obj;
						}
						case ':':
						{ // start of property value
							if (state != EXPECT_VALUE)
							{
								if (state == EXPECT_PROPERTY) throw reader.FailInvalidSyntax("Missing field name after field #{0}", index);
								if (name != null) throw reader.FailInvalidSyntax("Duplicate colon after field #{0} '{1}'", index, name);
								throw reader.FailInvalidSyntax("Unexpected semicolon after field #{0}", index);

							}
							// immédiatement après, on doit trouver une valeur

							if (index == props.Length)
							{
								reader.ResizeObjectBuffer(ref props);
							}

							props[index] = new KeyValuePair<string, JsonValue>(name, ParseJsonValue(ref reader));
							++index;
							// next should be ',' or '}'
							state = EXPECT_NEXT;
							name = null;
							break;
						}
						case ',':
						{ // next field
							if (state != EXPECT_NEXT)
							{
								if (name != null) throw reader.FailInvalidSyntax("Unexpected comma after name of field #{0} ", index);
								throw reader.FailInvalidSyntax("Unexpected comma after field #{0}", index);
							}

							// next should be '"' or '}' if trailing commas are allowed
							state = EXPECT_PROPERTY;
							break;
						}
						case '/':
						{ // comment
							ParseComment(ref reader);
							break;
						}
						default:
						{ // object
							if (c == CrystalJsonParser.EndOfStream) throw reader.FailUnexpectedEndOfStream("Incomplete object definition");
							if (state == EXPECT_NEXT) throw reader.FailInvalidSyntax("Missing comma after field #{0}", index);
							if (state == EXPECT_VALUE) throw reader.FailInvalidSyntax("Missing semicolon after field '{0}' value", name);
							if (c == ']') throw reader.FailInvalidSyntax("Unexpected ']' encountered inside an object. Did you forget to close the object?");
							throw reader.FailInvalidSyntax("Invalid character '{0}' after field #{1}", c, index);
						}
					}
				}
			}
			finally
			{
				reader.ReleaseObjectBuffer(props);
			}
		}

		private static void ParseComment(ref CrystalJsonTokenizer<TReader> reader)
		{
#if DEBUG_JSON_PARSER
			System.Diagnostics.Debug.WriteLine("CrystalJsonConverter.ParseJsonComment(...) [BEGIN]");
#endif

			// on a déja le premier '/', donc le suivant doit soit être un '/' (single line) ou '*' (multi line)

			char c = reader.ReadOne();
			switch(c)
			{
				case '/':
				{ // read until next CRLF
					SkipSingleLineComment(ref reader);
					break;
				}
				case '*':
				{ // read until next */
					SkipMultiLineComment(ref reader);
					break;
				}
				default:
				{
					if (c == CrystalJsonParser.EndOfStream) throw reader.FailUnexpectedEndOfStream("Incomplete comment");
					throw reader.FailInvalidSyntax("Invalid character '{0}' after comment start", c);
				}
			}
		}

		/// <summary>Skip a single-line comment</summary>
		/// <remarks>The reader will be placed after the next '\n'</remarks>
		private static void SkipSingleLineComment(ref CrystalJsonTokenizer<TReader> reader)
		{
			// the reader is just after "//" and we will read until next LF or end of stream
			char c;
			do
			{
				c = reader.ReadOne();
			}
			while (c != '\n' & c != CrystalJsonParser.EndOfStream);
		}

		/// <summary>Skip a multi-line comment</summary>
		/// <remarks>The reader will be place after the final '*/'</remarks>
		private static void SkipMultiLineComment(ref CrystalJsonTokenizer<TReader> reader)
		{
			// the reader is just after "/*" and we will read until the next '*' followed by '/'
			while(true)
			{
				char c = reader.ReadOne();
				if (c == '*')
				{
					switch((c = reader.ReadOne()))
					{

						case '/': return;
						case '*': reader.Push(c); continue;
						case CrystalJsonParser.EndOfStream: throw reader.FailUnexpectedEndOfStream("Truncated multi-line comment");
					}
				}
				else if (c == CrystalJsonParser.EndOfStream)
				{
					throw reader.FailUnexpectedEndOfStream("Truncated multi-line comment");
				}
			}
		}

		private static JsonArray ParseJsonArray(ref CrystalJsonTokenizer<TReader> reader)
		{
#if DEBUG_JSON_PARSER
			System.Diagnostics.Debug.WriteLine("CrystalJsonConverter.ParseJsonArray(...) [BEGIN]");
#endif
			// on a déjà le ']'

			var buffer = reader.AcquireArrayBuffer();
			Contract.Debug.Requires(buffer != null);
			try
			{
				int index = 0;

				bool commaRequired = false;
				bool valueRequired = false;

				while (true)
				{
					char c = reader.ReadNextToken();
					if (c == CrystalJsonParser.EndOfStream)
					{
						throw reader.FailUnexpectedEndOfStream("Array is incomplete");
					}

					if (c == ']')
					{
						if (valueRequired && reader.Settings.DenyTrailingCommas) throw reader.FailInvalidSyntax("Missing value before end of array");
#if DEBUG_JSON_PARSER
					System.Diagnostics.Debug.WriteLine("CrystalJsonConverter.ParseJsonArray(...) [END] read " + list.Count + " values");
#endif
						if (index == 0) return new JsonArray();

						var tmp = new JsonValue[index];
						Array.Copy(buffer, 0, tmp, 0, index);
						return new JsonArray(tmp, index);
					}

					if (c == ',')
					{
						if (!commaRequired) throw reader.FailInvalidSyntax("Unexpected comma in array");
						commaRequired = false;
						valueRequired = true;
					}
					else
					{
						if (commaRequired) throw reader.FailInvalidSyntax("Missing comma between two items of an array");
						reader.Push(c);

						if (buffer.Length == index)
						{
							reader.ResizeArrayBuffer(ref buffer);
						}

						var val = ParseJsonValue(ref reader) ?? throw reader.FailUnexpectedEndOfStream("Array is incomplete");
						buffer[index++] = val;
						commaRequired = true;
						valueRequired = false;
					}
				}
			}
			finally
			{
				reader.ReleaseArrayBuffer(buffer);
			}
		}

		#endregion

	}

}
