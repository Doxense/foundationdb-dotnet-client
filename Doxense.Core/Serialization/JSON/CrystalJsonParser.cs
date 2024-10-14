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

#define ENABLE_TIMO_STRING_CONVERTER
//#define ENABLE_GRISU3_STRING_CONVERTER // work in progress

//#define DEBUG_JSON_PARSER
//#define DEBUG_JSON_BINDER

namespace Doxense.Serialization.Json
{
	using System.Globalization;
	using System.Reflection;
	using System.Text;
	using Doxense.Linq;
	using Doxense.Text;
	using Doxense.Tools;

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

		internal static readonly JsonTokenType[] TokenMap =
		[
			0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
			0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
			0, 0, JsonTokenType.String, 0, 0, 0, 0, 0, 0, 0, 0, JsonTokenType.Number, 0, JsonTokenType.Number, 0, 0,
			JsonTokenType.Number, JsonTokenType.Number, JsonTokenType.Number, JsonTokenType.Number, JsonTokenType.Number, JsonTokenType.Number, JsonTokenType.Number, JsonTokenType.Number, JsonTokenType.Number, JsonTokenType.Number, 0, 0, 0, 0, 0, 0,
			0, 0, 0, 0, 0, 0, 0, 0, 0, JsonTokenType.Special, 0, 0, 0, 0, JsonTokenType.Special, 0,
			0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, JsonTokenType.Array, 0, 0, 0, 0,
			0, 0, 0, 0, 0, 0, JsonTokenType.False, 0, 0, 0, 0, 0, 0, 0, JsonTokenType.Null, 0,
			0, 0, 0, 0, JsonTokenType.True, 0, 0, 0, 0, 0, 0, JsonTokenType.Object, 0, 0, 0, 0,
		];

#if RECOMPUTE_TOKEN_MAP

		public static JsonTokenType[] ComputeTokenTypeMap()
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
			for (int i = 0; i <= 9; i++)
			{
				map['0' + i] = JsonTokenType.Number;
			}
			map['+'] = JsonTokenType.Number; // +###
			map['-'] = JsonTokenType.Number; // -###

			var sb = new StringBuilder();
			sb.AppendLine("[").Append("\t");
			for (int i = 0; i < map.Length; i++)
			{
				if (map[i] == JsonTokenType.Invalid)
				{
					sb.Append('0');
				}
				else
				{
					sb.Append(nameof(JsonTokenType) + ".").Append(map[i].ToString("G"));
				}

				if (i % 16 == 15) sb.AppendLine(",\t"); else sb.Append(", ");
			}
			sb.AppendLine("];");
			Console.WriteLine(sb);

			return map;
		}

#endif

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
				if (c is <= '9' and >= '0')
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

				if (c is 'e' or 'E')
				{
					if (hasExponent) throw InvalidNumberFormat(literal, "duplicate exponent");
					incomplete = true;
					hasExponent = true;
					computed = false;
					continue;
				}

				if (c is '-' or '+')
				{
					if (p == 1)
					{
						negative = c == '-';
					}
					else
					{
						if (!hasExponent)
						{
							throw InvalidNumberFormat(literal, "unexpected sign at this location");
						}
						if (hasExponentSign)
						{
							throw InvalidNumberFormat(literal, "duplicate sign is exponent");
						}
						hasExponentSign = true;
					}
					incomplete = true;
					continue;
				}

				if (c == 'I')
				{ // +Infinity / -Infinity ?
					if (string.Equals(literal, "+Infinity", StringComparison.Ordinal))
					{
						return JsonNumber.PositiveInfinity;
					}
					if (string.Equals(literal, "-Infinity", StringComparison.Ordinal))
					{
						return JsonNumber.NegativeInfinity;
					}
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
						if (num <= JsonNumber.CACHED_SIGNED_MAX)
						{
							return JsonNumber.GetCachedSmallNumber((int) num);
						}
					}
					else
					{
						if (num <= -JsonNumber.CACHED_SIGNED_MIN)
						{
							return JsonNumber.GetCachedSmallNumber(-((int) num));
						}
					}
				}

				return !negative
					? JsonNumber.ParseUnsigned(num, literal, literal)
					: JsonNumber.ParseSigned(-((long) num), literal, literal); // avec seulement 16 digits, pas de risques d'overflow a cause du signe
			}

			// on a besoin de parser le nombre...
			var value = ParseNumberFromLiteral(literal, literal, negative, hasDot, hasExponent);
			if (value is null) throw InvalidNumberFormat(literal, "malformed");
			return value;
		}

		internal static JsonNumber? ParseNumberFromLiteral(ReadOnlySpan<char> literal, string? original, bool negative, bool hasDot, bool hasExponent)
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
						return JsonNumber.ParseUnsigned(u64, literal, original);
					}
				}
				else
				{ // signed
					if (long.TryParse(literal, styles, NumberFormatInfo.InvariantInfo, out var s64))
					{
						return JsonNumber.ParseSigned(s64, literal, original);
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
					return JsonNumber.Parse(dbl, literal, original);
				}
			}

			// use decimal has the last resort fallback...
			if (decimal.TryParse(literal, styles, NumberFormatInfo.InvariantInfo, out var dec))
			{
				return JsonNumber.Parse(dec, literal, original);
			}

			// no luck ...
			return null;
		}

		[Pure]
		private static FormatException InvalidNumberFormat(string literal, string reason) => new($"Invalid number '{literal}.' ({reason})");

		/// <summary>Indique si la string PEUT être une date au format ISO 8601</summary>
		/// <param name="value">Chaine candidate</param>
		/// <returns>True si la string ressemble (de loin) à une date ISO</returns>
		[Pure]
		private static bool CouldBeIso8601DateTime(ReadOnlySpan<char> value)
		{
			// cherche les marqueurs '-' 'T' et ':'
			// la fin doit etre 'Z' si UTC ou alors '+##:##' ou '-##:##'
			return value.Length >= 20 && value[4] == '-' && value[7] == '-' && value[10] == 'T' && value[13] == ':' && value[16] == ':' && (value[^1] == 'Z' || (value[^3] == ':' && (value[^6] is '+' or '-')));
		}

		[Pure, ContractAnnotation("value:null => false")]
		internal static bool TryParseIso8601DateTime(ReadOnlySpan<char> value, out DateTime result)
		{
#if DEBUG_JSON_PARSER
			Debug.WriteLine("CrystalJsonConverter.TryParseMicrosoftDateTime(" + value +")");
#endif
			result = DateTime.MinValue;

			if (value.Length == 0 || !CouldBeIso8601DateTime(value)) return false;

			// cf http://msdn.microsoft.com/en-us/library/bb882584.aspx
			return DateTime.TryParse(value, DateTimeFormatInfo.InvariantInfo, DateTimeStyles.RoundtripKind, out result);
		}

		[Pure, ContractAnnotation("value:null => false")]
		internal static bool TryParseIso8601DateTimeOffset(ReadOnlySpan<char> value, out DateTimeOffset result)
		{
#if DEBUG_JSON_PARSER
			Debug.WriteLine("CrystalJsonConverter.TryParseMicrosoftDateTime(" + value +")");
#endif
			result = DateTimeOffset.MinValue;

			if (value.Length == 0 || !CouldBeIso8601DateTime(value)) return false;

			// cf http://msdn.microsoft.com/en-us/library/bb882584.aspx
			return DateTimeOffset.TryParse(value, DateTimeFormatInfo.InvariantInfo, DateTimeStyles.RoundtripKind, out result);
		}

		[Pure]
		private static bool CouldBeJsonMicrosoftDateTime(ReadOnlySpan<char> value)
		{
			return value.Length >= 9 && value.StartsWith("/Date(") && value.EndsWith(")/");
		}

		[Pure, ContractAnnotation("value:null => false")]
		internal static bool TryParseMicrosoftDateTime(ReadOnlySpan<char> value, out DateTime result, out TimeSpan? tz)
		{
#if DEBUG_JSON_PARSER
			Debug.WriteLine("CrystalJsonConverter.TryParseMicrosoftDateTime(" + value +")");
#endif
			result = DateTime.MinValue;
			tz = null;

			if (value.Length == 0 || !CouldBeJsonMicrosoftDateTime(value)) return false;

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
			if (!long.TryParse(value.Slice(6, endOffset - 6), out var ticks)) // 6 = "/Date(".Length
				return false;

			// note: il y a un "bug" dans les sérialisateurs de Microsoft: MinValue/MaxValue sont en LocalTime, et donc
			// une fois convertis en UTC, ils sont décallés (par ex le nb de ticks est négatif si on est à l'est de GMT, ou légèrement positif si on est à l'ouest)
			// Pour contrecarrer ça, on "arrondi" à Min ou Max si les ticks sont à moins d'un jour des bornes.

			const int MILLISECONDS_PER_DAY = 86400 * 1000;
			if (ticks < -62135596800000 + MILLISECONDS_PER_DAY)
			{ // MinValue
				result = DateTime.MinValue;
			}
			else if (ticks > 253402300799999 - MILLISECONDS_PER_DAY)
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
					if (!int.TryParse(value.Slice(endOffset + 1, value.Length - endOffset - 3), out var offset))
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
						throw JsonBindingException.CannotDeserializeCustomTypeBadType(data, customClass);
					}

					#endregion

#if DEBUG_JSON_BINDER
					Debug.WriteLine("DeserializeCustomClassOrStruct(..., " + type+") : object has custom class " + customClass);
#endif
					// retrouve le type correspondant
					var customType = resolver.ResolveClassId(customClass);
					if (customType == null)
					{
						throw JsonBindingException.CannotDeserializeCustomTypeNoConcreteClassFound(data, type, customClass);
					}

					if (type.IsSealed)
					{
						// si le type attendu est sealed, alors la seule solution est que le type spécifié match EXACTEMENT celui attendu!
						if (type != customType)
						{
							throw JsonBindingException.CannotDeserializeCustomTypeIncompatibleType(data, type, customClass);
						}
					}
					
					if (type != typeof(object))
					{ 
						// si le type est donné par l'appelant, on veut vérifier que c'est compatible avec le type attendu
						if (!type.IsAssignableFrom(customType))
						{ // le type donnée par l'appelant n'est pas compatible... c'est peut être une erreur, ou alors une tentative de hack!
							throw JsonBindingException.CannotDeserializeCustomTypeIncompatibleType(data, type, customClass);
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
				throw JsonBindingException.CannotDeserializeCustomTypeNoTypeDefinition(data, type);
			}

			if (typeDef.CustomBinder != null)
			{ // il y a un custom binder, qui va se charger de créer et remplir l'objet
			  // => automatiquement le cas pour les IJsonSerializable, ou alors c'est un custom binder fourni par par un CustomTypeResolver
				return typeDef.CustomBinder(data, type, resolver);
			}

			if (typeDef.Generator == null)
			{ // sans générateur, on ne peut pas créer d'instance !
				throw JsonBindingException.CannotDeserializeCustomTypeNoBinderOrGenerator(data, type);
			}

			// crée une nouvelle instance de la class/struct
			object instance;
			try
			{
				instance = typeDef.Generator();
			}
			catch (Exception e) when (!e.IsFatalError())
			{ // problème lors de la création de l'objet? cela arrive lorsqu'on crée un objet via Reflection, et qu'il n'a pas de constructeur par défaut...
				throw JsonBindingException.FailedToConstructTypeInstanceErrorOccurred(data, type, e);
			}

			if (instance == null)
			{ // uhoh ? le générateur n'a rien retourné, ce qui peut arriver quand on essaye de faire un "new Interface()" ou un "new AbstractClass()"...
				throw JsonBindingException.FailedToConstructTypeInstanceReturnedNull(data, type);
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
				if (member.Binder == null) throw JsonBindingException.CannotDeserializeCustomTypeNoReaderForMember(child, member, type);
				object? value;
				try
				{
					value = member.Binder(child, member.Type, resolver);
				}
				catch(Exception e)
				{
					// Pour aider a tracker le path vers la valeur qui cause pb, on va re-wrap l'exception!
					if (e is TargetInvocationException tiex)
					{
						e = tiex.InnerException ?? e;
					}
					var path = JsonPath.Create(member.OriginalName);
					if (e is JsonBindingException jbex)
					{
						// we have to repeat the original reason and the original path!
						var reason = jbex.Reason ?? jbex.Message;
						if (jbex.Path != null)
						{
							path = JsonPath.Combine(path, jbex.Path.Value);
						}
						var targetType = jbex.TargetType ?? member.Type;
						throw new JsonBindingException($"Cannot bind JSON {child.Type} to member '({typeDef.Type.GetFriendlyName()}).{path}' of type '{member.Type.GetFriendlyName()}': {reason}", reason, path, jbex.Value, targetType, jbex.InnerException);
					}

					throw new JsonBindingException($"Cannot bind JSON {child.Type} to member '({typeDef.Type.GetFriendlyName()}).{path}' of type '{member.Type.GetFriendlyName()}': [{e.GetType().GetFriendlyName()}] {e.Message}", path, child, member.Type, e);
				}

				// Ecrit la valeur dans le champ correspondant
				if (member.Setter == null) throw JsonBindingException.CannotDeserializeCustomTypeNoBinderForMember(child, member, type);
				try
				{
					member.Setter(instance, value);
				}
				catch(Exception e)
				{
					var path = JsonPath.Create(member.Name);
					throw new JsonBindingException($"Cannot assign member '{instance.GetType().GetFriendlyName()}.{member.Name}' of type '{member.Type.GetFriendlyName()}' with value of type '{(value?.GetType().GetFriendlyName() ?? "<null>")}': [{e.GetType().GetFriendlyName()}] {e.Message}", path, child, member.Type, e);
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
			if (first < map.Length)
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
						reader.ReadExpectedKeyword("ull");
						return JsonNull.Null;
					}
					case JsonTokenType.True:
					{ // true
						reader.ReadExpectedKeyword("rue");
						return JsonBoolean.True;
					}
					case JsonTokenType.False:
					{ // false
						reader.ReadExpectedKeyword("alse");
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
					throw reader.FailInvalidSyntax($"Unexpected character '{first}'");
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
			// note: we have already parsed the opening double-quote (")

			const int SIZE = 128;

			Span<char> buf = stackalloc char[SIZE];
			using var sb = new ValueBuffer<char>(buf);

			int hashCode = StringTable.Hash.FNV_OFFSET_BIAS;

			while (true)
			{
				char c = reader.ReadOne();

				// From most frequent to less frequent:
				// > letters
				// > the last double-quote that ends the string
				// > spaces
				// > '\' used for escaping
				// > EOF (this would only happen if the whole document is a string, usually it is an object or array)

				if (c == '"') break; // must be evaluated BEFORE parsing '\"'
				if (c == '\\')
				{ // decode the escaped character
					c = ParseEscapedCharacter(ref reader);
				}
				if (c == CrystalJsonParser.EndOfStream)
				{
					throw reader.FailUnexpectedEndOfStream("String is incomplete");
				}

				sb.Add(c);
				hashCode = StringTable.Hash.CombineFnvHash(hashCode, c);
			}

			if (sb.Count == 0)
			{
				return string.Empty;
			}

			//TODO: table for single letter names ?
			if (table != null)
			{ // interning
				return table.Add(hashCode, sb.Span);
			}
			else
			{
				return sb.Span.ToString();
			}

		}

		private static char ParseEscapedCharacter(ref CrystalJsonTokenizer<TReader> reader)
		{
			char c = reader.ReadOne();
			return c switch
			{
				'\"' or '\\' or '/' => c,
				'b' => '\b',
				'f' => '\f',
				'n' => '\n',
				'r' => '\r',
				't' => '\t',
				'u' => ParseEscapedUnicodeCharacter(ref reader),
				CrystalJsonParser.EndOfStream => throw reader.FailUnexpectedEndOfStream("Invalid string escaping"),
				_ => throw reader.FailInvalidSyntax($@"Invalid escaped character \{c} found in string")
			};
		}

		private static char ParseEscapedUnicodeCharacter(ref CrystalJsonTokenizer<TReader> reader)
		{
			// Format: "\uXXXX" where XXXX = hexa
			int x = 0;
			for (int i = 0; i < 4; i++)
			{
				char c = reader.ReadOne();

				x <<= 4;
				if ((uint) (c - '0') <= ('9' - '0'))
				{ // c is >= '0' and <= '9'
					x |= (c - 48);
				}
				else if ((uint) (c - 'A') <= ('F' - 'A'))
				{ // c is >= 'A' and <= 'F'
					x |= (c - 55);
				}
				else if ((uint) (c - 'a') <= ('f' - 'a'))
				{ // c is >= 'a' and <= 'f'
					x |= (c - 87);
				}
				else if (c == CrystalJsonParser.EndOfStream)
				{
					throw reader.FailUnexpectedEndOfStream("Invalid Unicode character escaping");
				}
				else
				{
					throw reader.FailInvalidSyntax("Invalid Unicode character escaping");
				}
			}

			return (char) x;
		}

		private static unsafe JsonNumber ParseJsonNumber(ref CrystalJsonTokenizer<TReader> reader, char first)
		{
#if DEBUG_JSON_PARSER
			System.Diagnostics.Debug.WriteLine("CrystalJsonConverter.ParseJsonNumber(...)");
#endif
			const int MAX_NUMBER_CHARS = 64;

			Span<char> buffer = stackalloc char[MAX_NUMBER_CHARS];
			buffer[0] = first;
			int p = 1;
			bool negative = first == '-';
			bool hasDot = false;
			bool hasExponent = false;
			bool hasExponentSign = false;
			bool incomplete = first is < '0' or > '9';
			bool computed = negative || !incomplete;
			ulong num = incomplete ? 0 : (ulong)(first - '0');
			while (p < MAX_NUMBER_CHARS)
			{
				char c = reader.ReadOne();

				if ((uint) (c - '0') < 10) // "0" .. "9"
				{ // digit
					incomplete = false;
					num = (num * 10) + (ulong)(c - '0');
					//REVIEW: fail if more than 17 digits? (ulong.MaxValue) unless we want to handle BigIntegers?
				}
				else if (c == ',' || c == '}' || c == ']' || c == ' ' || c == '\n' || c == '\t' || c == '\r')
				{ // this is a valid end-of-stream character
				  // rewind this character
					reader.Push(c);
					break;
				}
				else if (c == '.')
				{
					if (hasDot)
					{
						throw reader.FailInvalidSyntax($"Invalid number '{buffer[..p].ToString()}.' (duplicate decimal point)");
					}
					incomplete = true;
					hasDot = true;
					computed = false;
				}
				else if (c == 'e' || c == 'E')
				{ // exponent (scientific form)
					if (hasExponent)
					{
						throw reader.FailInvalidSyntax($"Invalid number '{buffer[..p].ToString()}{c}' (duplicate exponent)");
					}
					incomplete = true; // must be followed by a sign or digit! ("123E" is not valid)
					hasExponent = true;
					computed = false;
				}
				else if (c == '-' || c == '+')
				{ // sign of the exponent
					if (!hasExponent)
					{
						throw reader.FailInvalidSyntax($"Invalid number '{buffer[..p].ToString()}{c}' (unexpected sign at this location)");
					}
					if (hasExponentSign)
					{
						throw reader.FailInvalidSyntax($"Invalid number '{buffer[..p].ToString()}{c}' (duplicate sign is exponent)");
					}
					incomplete = true; // must be followed by a digit! ("123E-" is not valid)
					hasExponentSign = true;
				}
				else if (c == 'I' && p == 1 && (first == '+' || first == '-'))
				{ // '+Infinity' / '-Infinity' ?
					ParseSpecialKeyword(ref reader, c);
					//HACKHACK: if this succeeds, then the keyword was "Infinity" as expected
					return negative ? JsonNumber.NegativeInfinity : JsonNumber.PositiveInfinity;
				}
				else if (c == CrystalJsonParser.EndOfStream)
				{ // end of stream => end of number
					break;
				}
				else
				{ // invalid character after a (valid) number => fail
					throw reader.FailInvalidSyntax($"Invalid number '{buffer[..p].ToString()}' (unexpected character '{c}' found)");
				}

				buffer[p++] = c;
			}

			if (incomplete)
			{ // this should always end with a digit!
				throw reader.FailInvalidSyntax("Invalid JSON number (truncated)");
			}

			// if we did not see neither '.' nor exponent, and the number of digits is <= 4, we have a valid integer that will fit in the cache!
			if (computed && p <= 4)
			{
				if (num == 0)
				{
					return JsonNumber.Zero;
				}
				if (num == 1)
				{
					return negative ? JsonNumber.MinusOne : JsonNumber.One;
				}
				if (!negative)
				{
					// le literal est-il en cache?
					if (num <= JsonNumber.CACHED_SIGNED_MAX)
					{
						return JsonNumber.GetCachedSmallNumber((int) num);
					}
				}
				else
				{
					// le literal est-il en cache?
					if (num <= -JsonNumber.CACHED_SIGNED_MIN)
					{
						return JsonNumber.GetCachedSmallNumber(-((int) num));
					}
				}
			}

			if (computed)
			{
				if (negative)
				{ // avec seulement 16 digits, pas de risques d'overflow a cause du signe
					return JsonNumber.ParseSigned(-((long) num), null, default);
				}
				else
				{
					return JsonNumber.ParseUnsigned(num, null, default);
				}
			}

			// this is either a floating pointer number, or a large interger that does not fit in the cache
			var literal = buffer.Slice(0, p);

			// we may get the literal from a string table
			var table = reader.GetStringTable(computed ? JsonLiteralKind.Integer : JsonLiteralKind.Decimal);
			string? original = table?.Add(literal);

			// complete the parsing
			var value = CrystalJsonParser.ParseNumberFromLiteral(literal, original, negative, hasDot, hasExponent);

			return value ?? throw reader.FailInvalidSyntax($"Invalid JSON number '{literal.ToString()}' (malformed)");
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

			if (sb[^1] == CrystalJsonParser.EndOfStream)
			{
				sb.Length--;
			}

			throw reader.FailInvalidSyntax($"Invalid literal '{StringBuilderCache.GetStringAndRelease(sb)}'");
		}

		private static JsonObject ParseJsonObject(ref CrystalJsonTokenizer<TReader> reader)
		{
#if DEBUG_JSON_PARSER
			System.Diagnostics.Debug.WriteLine("CrystalJsonConverter.ParseJsonObject(...) [BEGIN]");
#endif

			const int EXPECT_PROPERTY = 0; // Expect a string that contains a name of a new property, or '}' to close the object
			const int EXPECT_VALUE = 1; // Expect a ':' followed by the value of the current property
			const int EXPECT_NEXT = 2; // Expect a ',' to start next property, or '}' to close the object
			int state = EXPECT_PROPERTY;

			char c = '\0';
			string? name = null;
			var createReadOnly = reader.Settings.ReadOnly;

#if NET8_0_OR_GREATER
			var scratch = new SegmentedValueBuffer<KeyValuePair<string, JsonValue>>.Scratch();
			using var props = new SegmentedValueBuffer<KeyValuePair<string, JsonValue>>(scratch);
#else
			using var props = new ValueBuffer<KeyValuePair<string, JsonValue>>(0);
#endif

			while (true)
			{
				char prev = c;
				switch (c = reader.ReadNextToken())
				{
					case '"':
					{ // start of property name
						if (state != EXPECT_PROPERTY)
						{
							if (state == EXPECT_VALUE)
							{
								throw reader.FailInvalidSyntax($"Missing colon after field #{props.Count + 1} value");
							}
							else
							{
								throw reader.FailInvalidSyntax($"Missing comma after field #{props.Count}");
							}
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
							{
								throw reader.FailInvalidSyntax("Missing field before end of object");
							}
							else if (state == EXPECT_VALUE)
							{
								throw reader.FailInvalidSyntax($"Missing value for field #{props.Count} at the end of object definition");
							}
						}
#if DEBUG_JSON_PARSER
						System.Diagnostics.Debug.WriteLine("CrystalJsonConverter.ParseJsonObject(...) [END] read " + map.Count + " fields");
#endif

						if (props.Count == 0)
						{ // empty object
							return createReadOnly ? JsonObject.EmptyReadOnly : JsonObject.Create();
						}

						// convert into the dictionary
						var map = new Dictionary<string, JsonValue>(props.Count, reader.FieldComparer);
						foreach (var kv in props)
						{
							map[kv.Key] = kv.Value;
#if DEBUG
							if (createReadOnly && !kv.Value.IsReadOnly)
							{
								Contract.Fail("Parsed child was mutable even though the settings are set to Immutable!");
							}
#endif
						}
						var obj = new JsonObject(map, createReadOnly);
						if (obj.Count != props.Count && !reader.Settings.OverwriteDuplicateFields)
						{
							var x = new HashSet<string>(reader.FieldComparer);
							foreach (var kv in props)
							{
								if (!x.Add(kv.Key))
								{
									throw reader.FailInvalidSyntax($"Duplicate field '{kv.Key}' in JSON Object.");
								}
							}
						}
						return obj;
					}
					case ':':
					{ // start of property value
						if (state != EXPECT_VALUE)
						{
							if (state == EXPECT_PROPERTY)
							{
								throw reader.FailInvalidSyntax($"Missing field name after field #{props.Count + 1}");
							}
							else if (name != null)
							{
								throw reader.FailInvalidSyntax($"Duplicate colon after field #{props.Count + 1} '{name}'");
							}
							else
							{
								throw reader.FailInvalidSyntax($"Unexpected semicolon after field #{props.Count + 1}");
							}
						}
						// immédiatement après, on doit trouver une valeur

						props.Add(new (name!, ParseJsonValue(ref reader)!));
						// next should be ',' or '}'
						state = EXPECT_NEXT;
						name = null;
						break;
					}
					case ',':
					{ // next field
						if (state != EXPECT_NEXT)
						{
							if (name != null)
							{
								throw reader.FailInvalidSyntax($"Unexpected comma after name of field #{props.Count + 1} ");
							}
							else
							{
								throw reader.FailInvalidSyntax($"Unexpected comma after field #{props.Count + 1}");
							}
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
						if (c == CrystalJsonParser.EndOfStream)
						{
							throw reader.FailUnexpectedEndOfStream("Incomplete object definition");
						}
						else if (state == EXPECT_NEXT)
						{
							throw reader.FailInvalidSyntax($"Missing comma after field #{props.Count + 1}");
						}
						else if (state == EXPECT_VALUE)
						{
							throw reader.FailInvalidSyntax($"Missing semicolon after field '{name!}' value");
						}
						else if (c == ']')
						{
							throw reader.FailInvalidSyntax("Unexpected ']' encountered inside an object. Did you forget to close the object?");
						}
						else
						{
							throw reader.FailInvalidSyntax($"Invalid character '{c}' after field #{props.Count + 1}");
						}
					}
				}
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
					if (c == CrystalJsonParser.EndOfStream)
					{
						throw reader.FailUnexpectedEndOfStream("Incomplete comment");
					}
					else
					{
						throw reader.FailInvalidSyntax($"Invalid character '{c}' after comment start");
					}
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

			char c;
			bool commaRequired = false;
			bool valueRequired = false;
			bool readOnly = reader.Settings.ReadOnly;

#if NET8_0_OR_GREATER
			var scratch = new SegmentedValueBuffer<JsonValue>.Scratch();
			using var buffer = new SegmentedValueBuffer<JsonValue>(scratch);
#else
			using var buffer = new ValueBuffer<JsonValue>(0);
#endif

			while (true)
			{
				c = reader.ReadNextToken();
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
					if (buffer.Count == 0)
					{ // empty object
						return readOnly ? JsonArray.EmptyReadOnly : new JsonArray();
					}

					var tmp = buffer.ToArray();
					return new(tmp, tmp.Length, readOnly);
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

					var val = ParseJsonValue(ref reader) ?? throw reader.FailUnexpectedEndOfStream("Array is incomplete");
					buffer.Add(val);
					commaRequired = true;
					valueRequired = false;
				}
			}
		}

		#endregion

	}

}
