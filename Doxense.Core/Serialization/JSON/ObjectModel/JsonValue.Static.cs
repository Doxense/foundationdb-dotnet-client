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

namespace Doxense.Serialization.Json
{
	using System;
	using System.Collections.Generic;
	using System.Diagnostics.CodeAnalysis;
	using System.Globalization;
	using System.Runtime.CompilerServices;
	using Doxense.Diagnostics.Contracts;
	using JetBrains.Annotations;

	public abstract partial class JsonValue
	{
		#region Static JsonValue...

		public static object BindValueType<T>(T value, Type type, ICrystalJsonTypeResolver resolver)
			where T : struct, IConvertible
		{
			if (type == typeof(T)) return value;
			if (type.IsPrimitive) return value.ToType(type, null);
			if (type.IsEnum) return Convert.ChangeType(value, type);
			// nullable ??
			var nullableType = Nullable.GetUnderlyingType(type);
			if (nullableType != null)
			{ // Nullable<T> => on retry avec le bon type
				// note: si c'était null, on serait dans JsonNull.Bind(...) donc pas de soucis...
				return BindValueType(value, nullableType, resolver);
			}
			throw Errors.CannotBindJsonValue(nameof(type), typeof(T), type);
		}

		internal static object? BindNative<TJson, TNative>(TJson? jsonValue, TNative nativeValue, Type? type, ICrystalJsonTypeResolver? resolver = null)
			where TJson : JsonValue
		{
			//REVIEW: vu que TNative est un valuetype, on aura autant de copie de cette méthodes en mémoire que de types! (beaucoup de boulot pour le JIT)
			// => il faudrait peut être trouver une optimisation utilisant le pattern "if (typeof(T) == typeof(...)) { ... }" pour optimiser ??

			if (jsonValue == null) return null;

			if (type == null || type == typeof(object)) return jsonValue.ToObject();

			// short circuit...
			if (type == typeof(TNative)) return nativeValue;

			if (type.IsPrimitive)
			{
				//attention: decimal et DateTime ne sont pas IsPrimitive !
				switch (System.Type.GetTypeCode(type))
				{
					case TypeCode.Boolean: return jsonValue.ToBoolean();
					case TypeCode.Char: return jsonValue.ToChar();
					case TypeCode.SByte: return jsonValue.ToSByte();
					case TypeCode.Byte: return jsonValue.ToByte();
					case TypeCode.Int16: return jsonValue.ToInt16();
					case TypeCode.UInt16: return jsonValue.ToUInt16();
					case TypeCode.Int32: return jsonValue.ToInt32();
					case TypeCode.UInt32: return jsonValue.ToUInt32();
					case TypeCode.Int64: return jsonValue.ToInt64();
					case TypeCode.UInt64: return jsonValue.ToUInt64();
					case TypeCode.Single: return jsonValue.ToSingle();
					case TypeCode.Double: return jsonValue.ToDouble();
					case TypeCode.Object:
					{
						if (type == typeof(IntPtr)) return new IntPtr(jsonValue.ToInt64());
						break;
					}
				}
			}

			if (type == typeof(string)) return jsonValue.ToString();
			if (type == typeof(DateTime)) return jsonValue.ToDateTime();
			if (type == typeof(TimeSpan)) return jsonValue.ToTimeSpan();
			if (type == typeof(Guid)) return jsonValue.ToGuid();
			if (type == typeof(decimal)) return jsonValue.ToDecimal();
			if (type == typeof(JsonValue)) return jsonValue;

			if (type.IsEnum)
			{ // Enumeration
				// on convertit en int d'abord, car decimal=>enum n'est pas supporté...
				// note: une enum n'est pas forcément un Int32, donc on est obligé d'abord de convertir vers le UnderlyingType (récursivement)
				return Enum.ToObject(type, jsonValue.Bind(type.GetEnumUnderlyingType(), resolver)!);
			}

			var nullableType = Nullable.GetUnderlyingType(type);
			if (nullableType != null)
			{ // si on est dans un JsonNumber c'est qu'on n'est pas null, donc traite les Nullable<T> comme des T
				// cas les plus fréquents...

				// rappel récursivement avec le type de base
				return jsonValue.Bind(nullableType, resolver);
			}

			// on tente notre chance
			if (nativeValue is IConvertible)
			{
				return Convert.ChangeType(nativeValue, type, NumberFormatInfo.InvariantInfo);
			}
			throw JsonValue.Errors.CannotBindJsonValue(nameof(type), typeof(TNative), type);
		}

		private static class Errors
		{

			[Pure]
			internal static Exception CannotBindJsonValue([InvokerParameterName] string paramName, Type sourceType, Type targetType)
			{
				return ThrowHelper.ArgumentException(paramName, "Cannot convert JSON value from type {0} to type {1}", sourceType.GetFriendlyName(), targetType.GetFriendlyName());
			}

			[Pure]
			internal static Exception JsonConversionNotSupported(JsonValue source, Type targetType)
			{
				return new JsonBindingException($"Cannot convert a {source.GetType().GetFriendlyName()} into a value of type {targetType.GetFriendlyName()}", source);
			}
		}

		/// <summary>Essayes de déterminer la catégorie d'une object JSON à partir d'un type CLR</summary>
		/// <param name="type">Type CLR (ex: int)</param>
		/// <returns>Catégorie JSON correspondante (ex: JsonType.Number)</returns>
		internal static JsonType GetJsonTypeFromClrType(Type type)
		{
			if (type == null) throw ThrowHelper.ArgumentNullException(nameof(type));

			switch(System.Type.GetTypeCode(type))
			{
				case TypeCode.Boolean:
					return JsonType.Boolean;

				case TypeCode.String:
				case TypeCode.Char:
					return JsonType.String;

				case TypeCode.SByte:
				case TypeCode.Byte:
				case TypeCode.Int16:
				case TypeCode.UInt16:
				case TypeCode.Int32:
				case TypeCode.UInt32:
				case TypeCode.Int64:
				case TypeCode.UInt64:
				case TypeCode.Single:
				case TypeCode.Double:
				case TypeCode.Decimal:
					return JsonType.Number;

				case TypeCode.DateTime:
					return JsonType.DateTime;

				default:
				{
					if (type == typeof(TimeSpan)) return JsonType.Number;
					if (type == typeof(DateTimeOffset)) return JsonType.Number;
					if (type == typeof(Guid)) return JsonType.String;
					if (type == typeof(IntPtr)) return JsonType.Number;

					if (type.IsArray || type.IsGenericInstanceOf(typeof(IList<>)))
					{
						return JsonType.Array;
					}

					return JsonType.Object;
				}
			}
		}

		#region ParseXYZ(...)

		// Text/Binary => JsonValue

		/// <summary>Parse une chaîne de texte JSON</summary>
		/// <param name="jsonText">Chaîne de texte JSON à parser</param>
		/// <param name="settings">Paramètres de parsing (optionnels)</param>
		/// <param name="required">Si true, throw une exception si le document JSON parsé est équivalent à null (vide, 'null', ...)</param>
		/// <returns>Valeur JSON correspondante. Si <paramref name="jsonText"/> est "vide", retourne soit <see cref="JsonNull.Missing"/> (si <paramref name="required"/> == false), ou une exception (si == true)</returns>
		/// <remarks>Si le résultat attendu est toujours un objet ou une array, préférez utiliser <see cref="ParseArray(String, CrystalJsonSettings, bool)"/> ou <see cref="ParseObject(String, CrystalJsonSettings, bool)"/></remarks>
		/// <exception cref="FormatException">En cas d'erreur de syntaxe JSON</exception>
		/// <exception cref="InvalidOperationException">Si le document JSON parsé est "null", et que <paramref name="required"/> vaut true.</exception>
		[Pure]
		public static JsonValue Parse(
#if NET8_0_OR_GREATER
			[StringSyntax("json")]
#endif
			string? jsonText,
			CrystalJsonSettings? settings = null,
			bool required = false
		)
		{
			var res = CrystalJson.Parse(jsonText, settings);
			return required ? res.Required() : res;
		}

		/// <summary>Parse une chaîne de texte contenant une array JSON</summary>
		/// <param name="jsonText">Chaîne de texte JSON à parser</param>
		/// <param name="settings">Paramètres de parsing (optionnels)</param>
		/// <param name="required">Si true, throw une exception si le document JSON parsé est équivalent à null (vide, 'null', ...)</param>
		/// <returns>Array JSON correspondante. Si <paramref name="jsonText"/> est "vide", retourne soit <see cref="JsonNull.Missing"/> (si <paramref name="required"/> == false), ou une exception (si == true)</returns>
		/// <exception cref="FormatException">En cas d'erreur de syntaxe JSON</exception>
		/// <exception cref="InvalidOperationException">Si le document JSON parsé est "null", et que <paramref name="required"/> vaut true.</exception>
		[Pure, ContractAnnotation("required:true => notnull")]
		public static JsonArray? ParseArray(
#if NET8_0_OR_GREATER
			[StringSyntax("json")]
#endif
			string? jsonText,
			CrystalJsonSettings? settings = null,
			bool required = false
		)
		{
			return CrystalJson.Parse(jsonText, settings).AsArray(required);
		}

		/// <summary>Parse une chaîne de texte contenant un object JSON</summary>
		/// <param name="jsonText">Chaîne de texte JSON à parser</param>
		/// <param name="settings">Paramètres de parsing (optionnels)</param>
		/// <param name="required">Si true, throw une exception si le document JSON parsé est équivalent à null (vide, 'null', ...)</param>
		/// <returns>Objet JSON correspondant. Si <paramref name="jsonText"/> est "vide", retourne soit <see cref="JsonNull.Missing"/> (si <paramref name="required"/> == false), ou une exception (si == true)</returns>
		/// <exception cref="FormatException">En cas d'erreur de syntaxe JSON</exception>
		/// <exception cref="InvalidOperationException">Si le document JSON parsé est "null", et que <paramref name="required"/> vaut true.</exception>
		[Pure, ContractAnnotation("required:true => notnull")]
		public static JsonObject? ParseObject(
#if NET8_0_OR_GREATER
			[StringSyntax("json")]
#endif
			string? jsonText,
			CrystalJsonSettings? settings = null,
			bool required = false
		)
		{
			return CrystalJson.Parse(jsonText, settings).AsObject(required);
		}

		/// <summary>Parse un buffer contenant du JSON (encodé en UTF8)</summary>
		/// <param name="jsonBytes">Buffer contenant le JSON à parser</param>
		/// <param name="required">Si true, throw une exception si le document JSON parsé est équivalent à null (vide, 'null', ...)</param>
		/// <param name="settings">Paramètres de parsing (optionnels)</param>
		/// <returns>Valeur JSON correspondante. Si <paramref name="jsonBytes"/> est "vide", retourne soit <see cref="JsonNull.Missing"/> (si <paramref name="required"/> == false), ou une exception (si == true)</returns>
		/// <remarks>Si le résultat attendu est toujours un objet ou une array, préférez utiliser <see cref="ParseArray(byte[], CrystalJsonSettings, bool)"/> ou <see cref="ParseObject(byte[], CrystalJsonSettings, bool)"/></remarks>
		/// <exception cref="FormatException">En cas d'erreur de syntaxe JSON</exception>
		/// <exception cref="InvalidOperationException">Si le document JSON parsé est "null", et que <paramref name="required"/> vaut true.</exception>
		[Pure]
		public static JsonValue Parse(byte[]? jsonBytes, CrystalJsonSettings? settings = null, bool required = false)
		{
			var res = CrystalJson.Parse(jsonBytes.AsSlice(), settings);
			return required ? res.Required() : res;
		}

		/// <summary>Parse un buffer contenant une array JSON (encodé en UTF8)</summary>
		/// <param name="jsonBytes">Buffer contenant le JSON à parser</param>
		/// <param name="required">Si true, throw une exception si le document JSON parsé est équivalent à null (vide, 'null', ...)</param>
		/// <param name="settings">Paramètres de parsing (optionnels)</param>
		/// <returns>Array JSON correspondante. Si <paramref name="jsonBytes"/> est "vide", retourne soit <see cref="JsonNull.Missing"/> (si <paramref name="required"/> == false), ou une exception (si == true)</returns>
		/// <exception cref="FormatException">En cas d'erreur de syntaxe JSON</exception>
		/// <exception cref="InvalidOperationException">Si le document JSON parsé est "null", et que <paramref name="required"/> vaut true.</exception>
		[Pure, ContractAnnotation("required:true => notnull")]
		public static JsonArray? ParseArray(byte[] jsonBytes, CrystalJsonSettings? settings = null, bool required = false)
		{
			return CrystalJson.Parse(jsonBytes.AsSlice(), settings).AsArray(required);
		}

		/// <summary>Parse un buffer contenant un object JSON (encodé en UTF8)</summary>
		/// <param name="jsonBytes">Buffer contenant le JSON à parser</param>
		/// <param name="required">Si true, throw une exception si le document JSON parsé est équivalent à null (vide, 'null', ...)</param>
		/// <param name="settings">Paramètres de parsing (optionnels)</param>
		/// <returns>Object JSON correspondant. Si <paramref name="jsonBytes"/> est "vide", retourne soit <see cref="JsonNull.Missing"/> (si <paramref name="required"/> == false), ou une exception (si == true)</returns>
		/// <exception cref="FormatException">En cas d'erreur de syntaxe JSON</exception>
		/// <exception cref="InvalidOperationException">Si le document JSON parsé est "null", et que <paramref name="required"/> vaut true.</exception>
		[Pure, ContractAnnotation("required:true => notnull")]
		public static JsonObject? ParseObject(byte[] jsonBytes, CrystalJsonSettings? settings = null, bool required = false)
		{
			return CrystalJson.Parse(jsonBytes.AsSlice(), settings).AsObject(required);
		}

		/// <summary>Parse un buffer contenant du JSON (encodé en UTF8)</summary>
		/// <param name="jsonBytes">Buffer contenant le JSON à parser</param>
		/// <param name="required">Si true, throw une exception si le document JSON parsé est équivalent à null (vide, 'null', ...)</param>
		/// <param name="settings">Paramètres de parsing (optionnels)</param>
		/// <returns>Valeur JSON correspondante. Si <paramref name="jsonBytes"/> est "vide", retourne soit <see cref="JsonNull.Missing"/> (si <paramref name="required"/> == false), ou une exception (si == true)</returns>
		/// <remarks>Si le résultat attendu est toujours un objet ou une array, préférez utiliser <see cref="ParseArray(Slice, CrystalJsonSettings, bool)"/> ou <see cref="ParseObject(Slice, CrystalJsonSettings, bool)"/></remarks>
		/// <exception cref="FormatException">En cas d'erreur de syntaxe JSON</exception>
		/// <exception cref="InvalidOperationException">Si le document JSON parsé est "null", et que <paramref name="required"/> vaut true.</exception>
		[Pure]
		public static JsonValue Parse(Slice jsonBytes, CrystalJsonSettings? settings = null, bool required = false)
		{
			var res = CrystalJson.Parse(jsonBytes, settings);
			return required ? res.Required() : res;
		}

		/// <summary>Parse un buffer contenant une array JSON (encodée en UTF8)</summary>
		/// <param name="jsonBytes">Buffer contenant le JSON à parser</param>
		/// <param name="required">Si true, throw une exception si le document JSON parsé est équivalent à null (vide, 'null', ...)</param>
		/// <param name="settings">Paramètres de parsing (optionnels)</param>
		/// <returns>Array JSON correspondante. Si <paramref name="jsonBytes"/> est "vide", retourne soit <see cref="JsonNull.Missing"/> (si <paramref name="required"/> == false), ou une exception (si == true)</returns>
		/// <exception cref="FormatException">En cas d'erreur de syntaxe JSON</exception>
		/// <exception cref="InvalidOperationException">Si le document JSON parsé est "null", et que <paramref name="required"/> vaut true.</exception>
		[Pure, ContractAnnotation("required:true => notnull")]
		public static JsonArray? ParseArray(Slice jsonBytes, CrystalJsonSettings? settings = null, bool required = false)
		{
			return CrystalJson.Parse(jsonBytes, settings).AsArray(required);
		}

		/// <summary>Parse un buffer contenant un object JSON (encodé en UTF8)</summary>
		/// <param name="jsonBytes">Buffer contenant le JSON à parser</param>
		/// <param name="required">Si true, throw une exception si le document JSON parsé est équivalent à null (vide, 'null', ...)</param>
		/// <param name="settings">Paramètres de parsing (optionnels)</param>
		/// <returns>Objet JSON correspondant. Si <paramref name="jsonBytes"/> est "vide", retourne soit <see cref="JsonNull.Missing"/> (si <paramref name="required"/> == false), ou une exception (si == true)</returns>
		/// <exception cref="FormatException">En cas d'erreur de syntaxe JSON</exception>
		/// <exception cref="InvalidOperationException">Si le document JSON parsé est "null", et que <paramref name="required"/> vaut true.</exception>
		[Pure, ContractAnnotation("required:true => notnull")]
		public static JsonObject? ParseObject(Slice jsonBytes, CrystalJsonSettings? settings = null, bool required = false)
		{
			return CrystalJson.Parse(jsonBytes, settings).AsObject(required);
		}

		#endregion

		#region FromValue(...)

		// CLR => JsonValue

		/// <summary>Convertit un objet CLR de type inconnu, en une valeur JSON</summary>
		/// <param name="value">Instance à convertir (primitive, classe, struct, array, ...)</param>
		/// <returns>Valeur JSON correspondante (JsonNumber, JsonObject, JsonArray, ...), ou JsonNull.Null si <paramref name="value"/> est null</returns>
		/// <remarks>Perf Hint: Utilisez <see cref="FromValue{T}(T)"/> pour des struct ou classes quand c'est possible, et les implicit cast pour des strings, numbers ou booleans</remarks>
		public static JsonValue FromValue(object? value)
		{
			if (value is null) return JsonNull.Null;
			if (value is JsonValue jv) return jv;
			var type = value.GetType();
			//TODO: PERF: Pooling?
			return CrystalJsonDomWriter.Default.ParseObject(value, type, type);
		}

		/// <summary>Convertit un objet CLR de type inconnu, en une valeur JSON, avec des paramètres de conversion spécifiques</summary>
		/// <param name="value">Instance à convertir (primitive, classe, struct, array, ...)</param>
		/// <param name="settings">Paramètre de conversion à utiliser</param>
		/// <param name="resolver">Resolver à utiliser (optionnel, utilise CrystalJson.DefaultResolver si null)</param>
		/// <returns>Valeur JSON correspondante (JsonNumber, JsonObject, JsonArray, ...), ou JsonNull.Null si <paramref name="value"/> est null</returns>
		/// <remarks>Perf Hint: Utilisez <see cref="FromValue{T}(T,CrystalJsonSettings,ICrystalJsonTypeResolver)"/> pour des struct ou classes quand c'est possible, et les implicit cast pour des strings, numbers ou booleans</remarks>
		public static JsonValue FromValue(object? value, CrystalJsonSettings settings, ICrystalJsonTypeResolver? resolver = null)
		{
			if (value is null) return JsonNull.Null;
			if (value is JsonValue jv) return jv;
			var type = value.GetType();
			return CrystalJsonDomWriter.Create(settings, resolver).ParseObject(value, type, type);
		}

		/// <summary>Convertit un objet CLR de type inconnu, en une valeur JSON, avec des paramètres de conversion spécifiques</summary>
		/// <param name="value">Instance à convertir (primitive, classe, struct, array, ...)</param>
		/// <param name="declaredType">Type du champ parent qui contenait la valeur de l'objet, qui peut être une interface ou une classe abstraite</param>
		/// <param name="settings">Paramètre de conversion à utiliser</param>
		/// <param name="resolver">Resolver à utiliser (optionnel, utilise CrystalJson.DefaultResolver si null)</param>
		/// <remarks>Perf Hint: Utilisez <see cref="FromValue{T}(T,CrystalJsonSettings,ICrystalJsonTypeResolver)"/> pour des struct ou classes quand c'est possible, et les implicit cast pour des strings, numbers ou booleans</remarks>
		[Pure]
		public static JsonValue FromValue(object? value, Type declaredType, CrystalJsonSettings? settings = null, ICrystalJsonTypeResolver? resolver = null)
		{
			return value != null
				? CrystalJsonDomWriter.Create(settings, resolver).ParseObject(value, declaredType, value.GetType())
				: JsonNull.Null;
		}

		/// <summary>Convertit un objet CLR de type bien déterminé, en une valeur JSON</summary>
		/// <typeparam name="T">Type déclaré de la valeur à convertir</typeparam>
		/// <param name="value">Valeur à convertir (primitive, classe, struct, array, ...)</param>
		/// <returns>Valeur JSON correspondante (string, number, object, array, ...), ou JsonNull.Null si <paramref name="value"/> est null</returns>
		/// <remarks>Perf Hint: pour des strings, numbers ou bool, utilisez plutôt le cast implicit!</remarks>
		[Pure]
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static JsonValue FromValue<T>(T? value)
		{
			#region <JIT_HACK>

			// En mode RELEASE, le JIT reconnaît les patterns "if (typeof(T) == typeof(VALUETYPE)) { ... }" dans une méthode générique Foo<T> quand T est un ValueType,
			// et les remplace par des "if (true) { ...}" ce qui permet d'éliminer le reste du code (très efficace si le if contient un return!)
			// Egalement, le JIT optimise le "(VALUE_TYPE)(object)value" si T == VALUE_TYPE pour éviter le boxing inutile (le cast intermédiaire en object est pour faire taire le compilateur)
			// => pour le vérifier, il faut inspecter l'asm généré par le JIT au runtime (en mode release, en dehors du debugger, etc...) ce qui n'est pas facile...
			// => vérifié avec .NET 4.6.1 + RyuJIT x64, la méthode FromValue<int> est directement inlinée en l'appel à JsonNumber.Return(...) !

#if !DEBUG // trop lent en debug !
			if (typeof (T) == typeof (bool)) return JsonBoolean.Return((bool) (object) value);
			if (typeof (T) == typeof (char)) return JsonString.Return((char) (object) value);
			if (typeof (T) == typeof (byte)) return JsonNumber.Return((byte) (object) value);
			if (typeof (T) == typeof (sbyte)) return JsonNumber.Return((sbyte) (object) value);
			if (typeof (T) == typeof (short)) return JsonNumber.Return((short) (object) value);
			if (typeof (T) == typeof (ushort)) return JsonNumber.Return((ushort) (object) value);
			if (typeof (T) == typeof (int)) return JsonNumber.Return((int) (object) value);
			if (typeof (T) == typeof (uint)) return JsonNumber.Return((uint) (object) value);
			if (typeof (T) == typeof (long)) return JsonNumber.Return((long) (object) value);
			if (typeof (T) == typeof (ulong)) return JsonNumber.Return((ulong) (object) value);
			if (typeof (T) == typeof (float)) return JsonNumber.Return((float) (object) value);
			if (typeof (T) == typeof (double)) return JsonNumber.Return((double) (object) value);
			if (typeof (T) == typeof (decimal)) return JsonNumber.Return((decimal) (object) value);
			if (typeof (T) == typeof (Guid)) return JsonString.Return((Guid) (object) value);
			if (typeof (T) == typeof (TimeSpan)) return JsonNumber.Return((TimeSpan) (object) value);
			if (typeof (T) == typeof (DateTime)) return JsonDateTime.Return((DateTime) (object) value);
			if (typeof (T) == typeof (DateTimeOffset)) return JsonDateTime.Return((DateTimeOffset) (object) value);
			if (typeof (T) == typeof (NodaTime.Instant)) return JsonString.Return((NodaTime.Instant) (object) value);
			if (typeof (T) == typeof (NodaTime.Duration)) return JsonNumber.Return((NodaTime.Duration) (object) value);
			//TODO: reste de NodaTime ?
			if (typeof (T) == typeof (bool?)) return JsonBoolean.Return((bool?) (object) value);
			if (typeof (T) == typeof (char?)) return JsonString.Return((char?) (object) value);
			if (typeof (T) == typeof (byte?)) return JsonNumber.Return((byte?) (object) value);
			if (typeof (T) == typeof (sbyte?)) return JsonNumber.Return((sbyte?) (object) value);
			if (typeof (T) == typeof (short?)) return JsonNumber.Return((short?) (object) value);
			if (typeof (T) == typeof (ushort?)) return JsonNumber.Return((ushort?) (object) value);
			if (typeof (T) == typeof (int?)) return JsonNumber.Return((int?) (object) value);
			if (typeof (T) == typeof (uint?)) return JsonNumber.Return((uint?) (object) value);
			if (typeof (T) == typeof (long?)) return JsonNumber.Return((long?) (object) value);
			if (typeof (T) == typeof (ulong?)) return JsonNumber.Return((ulong?) (object) value);
			if (typeof (T) == typeof (float?)) return JsonNumber.Return((float?) (object) value);
			if (typeof (T) == typeof (double?)) return JsonNumber.Return((double?) (object) value);
			if (typeof (T) == typeof (decimal?)) return JsonNumber.Return((decimal?) (object) value);
			if (typeof (T) == typeof (Guid?)) return JsonString.Return((Guid?) (object) value);
			if (typeof (T) == typeof (TimeSpan?)) return JsonNumber.Return((TimeSpan?) (object) value);
			if (typeof (T) == typeof (DateTime?)) return JsonDateTime.Return((DateTime?) (object) value);
			if (typeof (T) == typeof (DateTimeOffset?)) return JsonDateTime.Return((DateTimeOffset?) (object) value);
			if (typeof (T) == typeof (NodaTime.Instant?)) return JsonString.Return((NodaTime.Instant?) (object) value);
			if (typeof (T) == typeof (NodaTime.Duration?)) return JsonNumber.Return((NodaTime.Duration?) (object) value);
			//TODO: reste de NodaTime ?
#endif
			#endregion </JIT_HACK>

			//TODO: PERF: Pooling?
			return CrystalJsonDomWriter.Default.ParseObject(value, typeof (T));
		}

		[Pure]
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		internal static JsonValue FromValue<T>(CrystalJsonDomWriter writer, ref CrystalJsonDomWriter.VisitingContext context, T? value)
		{
			#region <JIT_HACK>

			// En mode RELEASE, le JIT reconnaît les patterns "if (typeof(T) == typeof(VALUETYPE)) { ... }" dans une méthode générique Foo<T> quand T est un ValueType,
			// et les remplace par des "if (true) { ...}" ce qui permet d'éliminer le reste du code (très efficace si le if contient un return!)
			// Egalement, le JIT optimise le "(VALUE_TYPE)(object)value" si T == VALUE_TYPE pour éviter le boxing inutile (le cast intermédiaire en object est pour faire taire le compilateur)
			// => pour le vérifier, il faut inspecter l'asm généré par le JIT au runtime (en mode release, en dehors du debugger, etc...) ce qui n'est pas facile...
			// => vérifié avec .NET 4.6.1 + RyuJIT x64, la méthode FromValue<int> est directement inlinée en l'appel à JsonNumber.Return(...) !

#if !DEBUG // trop lent en debug !
			if (typeof (T) == typeof (bool)) return JsonBoolean.Return((bool) (object) value);
			if (typeof (T) == typeof (char)) return JsonString.Return((char) (object) value);
			if (typeof (T) == typeof (byte)) return JsonNumber.Return((byte) (object) value);
			if (typeof (T) == typeof (sbyte)) return JsonNumber.Return((sbyte) (object) value);
			if (typeof (T) == typeof (short)) return JsonNumber.Return((short) (object) value);
			if (typeof (T) == typeof (ushort)) return JsonNumber.Return((ushort) (object) value);
			if (typeof (T) == typeof (int)) return JsonNumber.Return((int) (object) value);
			if (typeof (T) == typeof (uint)) return JsonNumber.Return((uint) (object) value);
			if (typeof (T) == typeof (long)) return JsonNumber.Return((long) (object) value);
			if (typeof (T) == typeof (ulong)) return JsonNumber.Return((ulong) (object) value);
			if (typeof (T) == typeof (float)) return JsonNumber.Return((float) (object) value);
			if (typeof (T) == typeof (double)) return JsonNumber.Return((double) (object) value);
			if (typeof (T) == typeof (decimal)) return JsonNumber.Return((decimal) (object) value);
			if (typeof (T) == typeof (Guid)) return JsonString.Return((Guid) (object) value);
			if (typeof (T) == typeof (TimeSpan)) return JsonNumber.Return((TimeSpan) (object) value);
			if (typeof (T) == typeof (DateTime)) return JsonDateTime.Return((DateTime) (object) value);
			if (typeof (T) == typeof (DateTimeOffset)) return JsonDateTime.Return((DateTimeOffset) (object) value);
			if (typeof (T) == typeof (NodaTime.Instant)) return JsonString.Return((NodaTime.Instant) (object) value);
			if (typeof (T) == typeof (NodaTime.Duration)) return JsonNumber.Return((NodaTime.Duration) (object) value);
			//TODO: reste de NodaTime ?
			if (typeof (T) == typeof (bool?)) return JsonBoolean.Return((bool?) (object) value);
			if (typeof (T) == typeof (char?)) return JsonString.Return((char?) (object) value);
			if (typeof (T) == typeof (byte?)) return JsonNumber.Return((byte?) (object) value);
			if (typeof (T) == typeof (sbyte?)) return JsonNumber.Return((sbyte?) (object) value);
			if (typeof (T) == typeof (short?)) return JsonNumber.Return((short?) (object) value);
			if (typeof (T) == typeof (ushort?)) return JsonNumber.Return((ushort?) (object) value);
			if (typeof (T) == typeof (int?)) return JsonNumber.Return((int?) (object) value);
			if (typeof (T) == typeof (uint?)) return JsonNumber.Return((uint?) (object) value);
			if (typeof (T) == typeof (long?)) return JsonNumber.Return((long?) (object) value);
			if (typeof (T) == typeof (ulong?)) return JsonNumber.Return((ulong?) (object) value);
			if (typeof (T) == typeof (float?)) return JsonNumber.Return((float?) (object) value);
			if (typeof (T) == typeof (double?)) return JsonNumber.Return((double?) (object) value);
			if (typeof (T) == typeof (decimal?)) return JsonNumber.Return((decimal?) (object) value);
			if (typeof (T) == typeof (Guid?)) return JsonString.Return((Guid?) (object) value);
			if (typeof (T) == typeof (TimeSpan?)) return JsonNumber.Return((TimeSpan?) (object) value);
			if (typeof (T) == typeof (DateTime?)) return JsonDateTime.Return((DateTime?) (object) value);
			if (typeof (T) == typeof (DateTimeOffset?)) return JsonDateTime.Return((DateTimeOffset?) (object) value);
			if (typeof (T) == typeof (NodaTime.Instant?)) return JsonString.Return((NodaTime.Instant?) (object) value);
			if (typeof (T) == typeof (NodaTime.Duration?)) return JsonNumber.Return((NodaTime.Duration?) (object) value);
			//TODO: reste de NodaTime ?
#endif
			#endregion </JIT_HACK>

			//TODO: PERF: Pooling?
			return writer.ParseObjectInternal(ref context, value, typeof (T), null);
		}

		/// <summary>Convertit un objet CLR de type bien déterminé, en une valeur JSON, avec des paramètres de conversion spécifiques</summary>
		/// <typeparam name="T">Type déclaré de la valeur à convertir</typeparam>
		/// <param name="value">Valeur à convertir (primitive, classe, struct, array, ...)</param>
		/// <param name="settings">Paramètre de conversion à utiliser</param>
		/// <param name="resolver">Resolver à utiliser (optionnel, utilise CrystalJson.DefaultResolver si null)</param>
		/// <returns>Valeur JSON correspondante (string, number, object, array, ...), ou JsonNull.Null si <paramref name="value"/> est null</returns>
		/// <remarks>Perf Hint: pour des strings, numbers ou bools, utilisez plutôt le cast implicit!</remarks>
		[Pure]
		public static JsonValue FromValue<T>(T? value, CrystalJsonSettings? settings, ICrystalJsonTypeResolver? resolver = null)
		{
			return value != null ? CrystalJsonDomWriter.Create(settings, resolver).ParseObject(value, typeof(T)) : JsonNull.Null;
		}

		#region Specialized Converters...

		// le but est que l'Intellisense bypass FromValue<T> pour des T de types connus!

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static JsonValue FromValue<T1>(ValueTuple<T1> tuple) => JsonArray.Return(tuple);

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static JsonValue FromValue<T1, T2>(ValueTuple<T1, T2> tuple) => JsonArray.Return(tuple);

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static JsonValue FromValue<T1, T2, T3>(ValueTuple<T1, T2, T3> tuple) => JsonArray.Return(tuple);

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static JsonValue FromValue<T1, T2, T3, T4>(ValueTuple<T1, T2, T3, T4> tuple) => JsonArray.Return(tuple);

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static JsonValue FromValue<T1, T2, T3, T4, T5>(ValueTuple<T1, T2, T3, T4, T5> tuple) => JsonArray.Return(tuple);

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static JsonValue FromValue<T1, T2, T3, T4, T5, T6>(ValueTuple<T1, T2, T3, T4, T5, T6> tuple) => JsonArray.Return(tuple);

		#endregion

		#endregion

		/// <summary>Indique si un objet (éventuellement dynamic) représente un élément vide ou manquant</summary>
		/// <param name="value">Valeur quelconque</param>
		/// <returns>True si value est null, JsonNull.* ou DynamicJsonNull.*</returns>
		public static bool IsJsonNull(object? value)
		{
			//TODO: utiliser value.IsNull !
			return object.ReferenceEquals(value, null) || value is JsonNull;
		}

		/// <summary>Indique si un objet (éventuellement dynamic) représente une élément manquant</summary>
		/// <param name="value">Valeur quelconque</param>
		/// <returns>True si value est soit JsonValue.Missing ou DynamicJsonValue.Missing. La référence 'null' n'est pas considérée comme manquante</returns>
		public static bool IsJsonMissing(object? value)
		{
			// note: on se repose sur le fait que JsonNull.Missing / DynamicJsonNull.Missing sont des singletons, donc on peut comparer directement les références !
			return object.ReferenceEquals(value, JsonNull.Missing);
		}

		/// <summary>Indique si un objet (éventuellement dynamic) représente une élément invalide (erreur)</summary>
		/// <param name="value">Valeur quelconque</param>
		/// <returns>True si value est soit JsonValue.Error ou DynamicJsonValue.Error. La référence 'null' n'est pas considérée comme invalide</returns>
		public static bool IsJsonError(object? value)
		{
			// note: on se repose sur le fait que JsonNull.Error / DynamicJsonNull.Error sont des singletons, donc on peut comparer directement les références !
			return object.ReferenceEquals(value, JsonNull.Error);
		}

		#endregion
	}

}
