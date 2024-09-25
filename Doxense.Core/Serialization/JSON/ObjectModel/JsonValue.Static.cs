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
	using System.Globalization;
	using System.Runtime.CompilerServices;

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
			{ // Nullable<T> => retry with the underlying value type
				// note: we will not reach here if the value is null, so we are guaranteed that it is "proper" value
				return BindValueType(value, nullableType, resolver);
			}
			throw Errors.CannotBindJsonValue(nameof(type), typeof(T), type);
		}

		internal static object? BindNative<TJson, TNative>(TJson? jsonValue, TNative nativeValue, Type? type, ICrystalJsonTypeResolver? resolver = null)
			where TJson : JsonValue
		{
			// Note: Since TNative is a ValueType, we will have many different JITed versions of this method in memory, one for each value type, which may cost a lost of first-time initialization cost?
			// in early .NET Framework versions, the JIT seemed to be in O(N^2) whith the number of generic types for the same call site, not sure if this is still an issue with modern .NET Core ?

			if (jsonValue == null)
			{
				return null;
			}

			if (type == null || type == typeof(object))
			{
				return jsonValue.ToObject();
			}

			// short circuit...
			if (type == typeof(TNative))
			{
				return nativeValue;
			}

			if (type.IsPrimitive)
			{
				// Note: some base types like decimal, DateTime or TimeSpan are not considered "primitive" types and are handled elsewhere
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
			if (type == typeof(Half)) return jsonValue.ToHalf();
			if (type == typeof(DateOnly)) return jsonValue.ToDateOnly();
			if (type == typeof(TimeOnly)) return jsonValue.ToTimeOnly();
			if (type == typeof(JsonValue)) return jsonValue;
			if (type == typeof(NodaTime.Instant)) return jsonValue.ToInstant();
			if (type == typeof(NodaTime.Duration)) return jsonValue.ToDuration();

			if (type.IsEnum)
			{ // Enumeration
				// first convert into int, since decimal=>enum is not supported...
				// note: not all enums use Int32 as a backing type, so we have to first convert into their UnderlyingType (recursively)
				return Enum.ToObject(type, jsonValue.Bind(type.GetEnumUnderlyingType(), resolver)!);
			}

			var nullableType = Nullable.GetUnderlyingType(type);
			if (nullableType != null)
			{ // if this is a JsonNumber then we already know we are not null, so we handle Nullable<T> just like a regular non-nullable T

				// bind to the non-nullable type (ex: Int32 for Nullable<Int32>)
				return jsonValue.Bind(nullableType, resolver);
			}

			// last resort for types that implement IConvertible
			if (nativeValue is IConvertible)
			{
				return Convert.ChangeType(nativeValue, type, NumberFormatInfo.InvariantInfo);
			}

			// there is no known conversion path for this type
			throw JsonValue.Errors.CannotBindJsonValue(nameof(type), typeof(TNative), type);
		}

		private static class Errors
		{

			[Pure]
			internal static Exception CannotBindJsonValue([InvokerParameterName] string paramName, Type sourceType, Type targetType)
			{
				return ThrowHelper.ArgumentException(paramName, $"Cannot convert JSON value from type {sourceType.GetFriendlyName()} to type {targetType.GetFriendlyName()}");
			}

			[Pure]
			internal static Exception JsonConversionNotSupported(JsonValue source, Type targetType)
			{
				return new JsonBindingException($"Cannot convert a {source.GetType().GetFriendlyName()} into a value of type {targetType.GetFriendlyName()}", source);
			}
		}

		/// <summary>Attempts to determine the category of a JSON value, given a CLR type</summary>
		/// <param name="type">CLR Type(ex: int)</param>
		/// <returns>Corresponding JSON categoriy (ex: JsonType.Number)</returns>
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

		/// <summary>Parses a JSON text literal, and returns the corresponding JSON value</summary>
		/// <param name="jsonText">JSON text document to parse</param>
		/// <param name="settings">Serialization settings (use default JSON settings if null)</param>
		/// <returns>Corresponding JSON value. If <paramref name="jsonText"/> is empty, will return <see cref="JsonNull.Missing"/></returns>
		/// <remarks>
		/// <para>The value may be mutable (for objects and arrays) and can be modified. If you require an immutable thread-safe value, please use <see cref="ParseReadOnly(string?,Doxense.Serialization.Json.CrystalJsonSettings?)"/> instead.</para>
		/// <para>If the result is always expected to be an Array or an Object, please call either <see cref="ParseArray(string?,Doxense.Serialization.Json.CrystalJsonSettings?)"/> or <see cref="ParseObject(string?,Doxense.Serialization.Json.CrystalJsonSettings?)"/>.</para>
		/// </remarks>
		/// <exception cref="FormatException">If the JSON document is not syntaxically correct.</exception>
		[Pure]
		public static JsonValue Parse(
#if NET8_0_OR_GREATER
			[System.Diagnostics.CodeAnalysis.StringSyntax("json")]
#endif
			string? jsonText,
			CrystalJsonSettings? settings = null
		)
		{
			return CrystalJson.Parse(jsonText, settings);
		}

		/// <summary>Parses a JSON text literal, and returns the corresponding JSON value</summary>
		/// <param name="jsonText">JSON text document to parse</param>
		/// <param name="settings">Serialization settings (use default JSON settings if null)</param>
		/// <returns>Corresponding JSON value. If <paramref name="jsonText"/> is empty, will return <see cref="JsonNull.Missing"/></returns>
		/// <remarks>
		/// <para>The value will be immutable and cannot be modified. If you require an mutable value, please use <see cref="Parse(string?,Doxense.Serialization.Json.CrystalJsonSettings?)"/> instead.</para>
		/// <para>If the result is always expected to be an Array or an Object, please call either <see cref="ParseArrayReadOnly(string?,Doxense.Serialization.Json.CrystalJsonSettings?)"/> or <see cref="ParseObjectReadOnly(string?,Doxense.Serialization.Json.CrystalJsonSettings?)"/>.</para>
		/// </remarks>
		/// <exception cref="FormatException">If the JSON document is not syntaxically correct.</exception>
		[Pure]
		public static JsonValue ParseReadOnly(
#if NET8_0_OR_GREATER
			[System.Diagnostics.CodeAnalysis.StringSyntax("json")]
#endif
			string? jsonText,
			CrystalJsonSettings? settings = null
		)
		{
			return CrystalJson.Parse(jsonText, settings?.AsReadOnly() ?? CrystalJsonSettings.JsonReadOnly);
		}

		/// <summary>Parses a JSON text literal that is expected to contain an Array</summary>
		/// <param name="jsonText">JSON text document to parse</param>
		/// <param name="settings">Serialization settings (use default JSON settings if null)</param>
		/// <returns>Corresponding JSON Array. If <paramref name="jsonText"/> is empty or not an Array, an exception will be thrown instead.</returns>
		/// <remarks>
		/// <para>The JSON Array is mutable and can be freely modified. If you require an immutable array, please use <see cref="ParseArrayReadOnly(string?,Doxense.Serialization.Json.CrystalJsonSettings?)"/> instead.</para>
		/// <para>If the JSON document can sometimes be empty of the 'null' token, you should call <see cref="Parse(string?,Doxense.Serialization.Json.CrystalJsonSettings?)"/> and then use <see cref="JsonValueExtensions.AsArrayOrDefault"/> on the result.</para>
		/// </remarks>
		/// <exception cref="FormatException">If the JSON document is not syntaxically correct.</exception>
		/// <exception cref="InvalidOperationException">If the JSON document was empty or the 'null' token.</exception>
		[Pure]
		public static JsonArray ParseArray(
#if NET8_0_OR_GREATER
			[System.Diagnostics.CodeAnalysis.StringSyntax("json")]
#endif
			string? jsonText,
			CrystalJsonSettings? settings = null
		)
		{
			return CrystalJson.Parse(jsonText, settings).AsArray();
		}

		/// <summary>Parses a JSON text literal that is expected to contain an Array, as a read-only JSON array</summary>
		/// <param name="jsonText">JSON text document to parse</param>
		/// <param name="settings">Serialization settings (use default JSON settings if null)</param>
		/// <returns>Corresponding immutable JSON Array. If <paramref name="jsonText"/> is empty or not an Array, an exception will be thrown instead.</returns>
		/// <remarks>
		/// <para>The JSON Array is immutable and cannot be modified. If you require a mutable array, please use <see cref="ParseArray(string?,Doxense.Serialization.Json.CrystalJsonSettings?)"/> instead.</para>
		/// <para>If the JSON document can sometimes be empty of the '<c>null</c>' token, you should call <see cref="Parse(string?,Doxense.Serialization.Json.CrystalJsonSettings?)"/> and then use <see cref="JsonValueExtensions.AsArrayOrDefault"/> on the result.</para>
		/// </remarks>
		/// <exception cref="FormatException">If the JSON document is not syntaxically correct.</exception>
		/// <exception cref="InvalidOperationException">If the JSON document was empty, or the '<c>null</c>' token, or not an Array.</exception>
		[Pure]
		public static JsonArray ParseArrayReadOnly(
#if NET8_0_OR_GREATER
			[System.Diagnostics.CodeAnalysis.StringSyntax("json")]
#endif
			string? jsonText,
			CrystalJsonSettings? settings = null
		)
		{
			return CrystalJson.Parse(jsonText, settings?.AsReadOnly() ?? CrystalJsonSettings.JsonReadOnly).AsArray();
		}

		/// <summary>Parses a JSON text literal that is expected to contain an Object</summary>
		/// <param name="jsonText">JSON text document to parse</param>
		/// <param name="settings">Serialization settings (use default JSON settings if null)</param>
		/// <returns>Corresponding JSON Object. If <paramref name="jsonText"/> is empty or not an object, an exception will be thrown instead.</returns>
		/// <remarks>If the JSON document can sometimes be empty of the 'null' token, you should call <see cref="Parse(string?,Doxense.Serialization.Json.CrystalJsonSettings?)"/> and then use <see cref="JsonValueExtensions.AsObjectOrDefault"/> on the result.</remarks>
		/// <exception cref="FormatException">If the JSON document is not syntaxically correct.</exception>
		/// <exception cref="InvalidOperationException">If the JSON document was empty or the 'null' token.</exception>
		[Pure]
		public static JsonObject ParseObject(
#if NET8_0_OR_GREATER
			[System.Diagnostics.CodeAnalysis.StringSyntax("json")]
#endif
			string? jsonText,
			CrystalJsonSettings? settings = null
		)
		{
			return CrystalJson.Parse(jsonText, settings).AsObject();
		}

		/// <summary>Parse a string literal containing a JSON Object</summary>
		/// <param name="jsonText">Input text to parse</param>
		/// <param name="settings">Serialization settings (use default JSON settings if null)</param>
		/// <returns>Corresponding JSON Object. If <paramref name="jsonText"/> is empty or not an object, an exception will be thrown instead.</returns>
		/// <remarks>The JSON object that is returned is immutable, and is safe for use as a singleton, a cached document, or for multithreaded operations. If you require an mutable version, please call <see cref="ParseObject(string?,Doxense.Serialization.Json.CrystalJsonSettings?)"/></remarks>
		/// <exception cref="FormatException">If there is a syntax error while parsing the JSON document</exception>
		/// <exception cref="InvalidOperationException">If the text is empty or equal to <c>"null"</c>.</exception>
		[Pure]
		public static JsonObject ParseObjectReadOnly(
#if NET8_0_OR_GREATER
			[System.Diagnostics.CodeAnalysis.StringSyntax("json")]
#endif
			string? jsonText,
			CrystalJsonSettings? settings = null
		)
		{
			return CrystalJson.Parse(jsonText, settings?.AsReadOnly() ?? CrystalJsonSettings.JsonReadOnly).AsObject();
		}

		/// <summary>Parses a buffer containing a document</summary>
		/// <param name="jsonBytes">UTF-8 encoded bytes</param>
		/// <param name="settings">Serialization settings (use default JSON settings if null)</param>
		/// <returns>Corresponding JSON value. If <paramref name="jsonBytes"/> is null or empty, will return <see cref="JsonNull.Missing"/></returns>
		/// <remarks>
		/// <para>The value may be mutable (for objects and arrays) and can be modified. If you require an immutable thread-safe value, please use <see cref="ParseReadOnly(string?,Doxense.Serialization.Json.CrystalJsonSettings?)"/> instead.</para>
		/// <para>If the result is always expected to be an Array or an Object, please call either <see cref="ParseArray(string?,Doxense.Serialization.Json.CrystalJsonSettings?)"/> or <see cref="ParseObject(System.ReadOnlySpan{byte},Doxense.Serialization.Json.CrystalJsonSettings?)"/>.</para>
		/// </remarks>
		/// <exception cref="FormatException">If the JSON document is not syntaxically correct.</exception>
		[Pure]
		public static JsonValue Parse(ReadOnlySpan<byte> jsonBytes, CrystalJsonSettings? settings = null)
		{
			return CrystalJson.Parse(jsonBytes, settings);
		}

		/// <summary>Parses a buffer containing a document</summary>
		/// <param name="jsonBytes">UTF-8 encoded bytes</param>
		/// <param name="settings">Serialization settings (use default JSON settings if null)</param>
		/// <returns>Corresponding read-only JSON value. If <paramref name="jsonBytes"/> is null or empty, will return <see cref="JsonNull.Missing"/></returns>
		[Pure]
		public static JsonValue ParseReadOnly(ReadOnlySpan<byte> jsonBytes, CrystalJsonSettings? settings = null)
		{
			return CrystalJson.Parse(jsonBytes, settings?.AsReadOnly() ?? CrystalJsonSettings.JsonReadOnly);
		}

		/// <summary>Parses a buffer containing a document that is expected to be an Array</summary>
		/// <param name="jsonBytes">UTF-8 encoded bytes</param>
		/// <param name="settings">Serialization settings (use default JSON settings if null)</param>
		/// <returns>Corresponding JSON Array. If <paramref name="jsonBytes"/> is empty or not an Array, an exception will be thrown</returns>
		/// <remarks>
		/// <para>The value may be mutable (for objects and arrays) and can be modified. If you require an immutable thread-safe value, please use <see cref="ParseArrayReadOnly(string?,Doxense.Serialization.Json.CrystalJsonSettings?)"/> instead.</para>
		/// </remarks>
		/// <exception cref="FormatException">If the JSON document is not syntaxically correct.</exception>
		/// <exception cref="InvalidOperationException">If the JSON document was empty, or the 'null' token, or not an Array.</exception>
		[Pure]
		public static JsonArray ParseArray(ReadOnlySpan<byte> jsonBytes, CrystalJsonSettings? settings = null)
		{
			return CrystalJson.Parse(jsonBytes, settings).AsArray();
		}

		/// <summary>Parses a buffer containing a document that is expected to be an Array</summary>
		/// <param name="jsonBytes">UTF-8 encoded bytes</param>
		/// <param name="settings">Serialization settings (use default JSON settings if null)</param>
		/// <returns>Corresponding JSON Array. If <paramref name="jsonBytes"/> is empty or not an Array, an exception will be thrown</returns>
		/// <remarks>
		/// <para>The resulting Array is immutable and cannot be modified. If you require a mutable array, please use <see cref="ParseArray(string?,Doxense.Serialization.Json.CrystalJsonSettings?)"/> instead.</para>
		/// <para>If the JSON document can sometimes be empty of the 'null' token, you should call <see cref="Parse(string?,Doxense.Serialization.Json.CrystalJsonSettings?)"/> and then use <see cref="JsonValueExtensions.AsArrayOrDefault"/> on the result.</para>
		/// </remarks>
		/// <exception cref="FormatException">If the JSON document is not syntaxically correct.</exception>
		/// <exception cref="InvalidOperationException">If the JSON document was empty, or the 'null' token, or not an Array.</exception>
		[Pure]
		public static JsonArray ParseArrayReadOnly(ReadOnlySpan<byte> jsonBytes, CrystalJsonSettings? settings = null)
		{
			return CrystalJson.Parse(jsonBytes, settings?.AsReadOnly() ?? CrystalJsonSettings.JsonReadOnly).AsArray();
		}

		/// <summary>Parses a buffer containing a document that is expected to be an Object</summary>
		/// <param name="jsonBytes">UTF-8 encoded bytes</param>
		/// <param name="settings">Serialization settings (use default JSON settings if null)</param>
		/// <returns>Corresponding JSON Object. If <paramref name="jsonBytes"/> is empty or not an Array, an exception will be thrown</returns>
		/// <remarks>
		/// <para>The value may be mutable (for objects and arrays) and can be modified. If you require an immutable thread-safe value, please use <see cref="ParseObjectReadOnly(System.ReadOnlySpan{byte},Doxense.Serialization.Json.CrystalJsonSettings?)"/> instead.</para>
		/// </remarks>
		/// <exception cref="FormatException">If the JSON document is not syntaxically correct.</exception>
		/// <exception cref="InvalidOperationException">If the JSON document was empty, or the 'null' token, or not an Object.</exception>
		[Pure]
		public static JsonObject ParseObject(ReadOnlySpan<byte> jsonBytes, CrystalJsonSettings? settings = null)
		{
			return CrystalJson.Parse(jsonBytes, settings).AsObject();
		}

		/// <summary>Parses a buffer containing a document that is expected to be an Object</summary>
		/// <param name="jsonBytes">UTF-8 encoded bytes</param>
		/// <param name="settings">Serialization settings (use default JSON settings if null)</param>
		/// <returns>Corresponding immutable JSON Object. If <paramref name="jsonBytes"/> is empty or not an Array, an exception will be thrown</returns>
		/// <remarks>
		/// <para>The resulting object is immutable and cannot be modified. If you require a mutable array, please use <see cref="ParseObject(System.ReadOnlySpan{byte},Doxense.Serialization.Json.CrystalJsonSettings?)"/> instead.</para>
		/// </remarks>
		/// <exception cref="FormatException">If the JSON document is not syntaxically correct.</exception>
		/// <exception cref="InvalidOperationException">If the JSON document was empty, or the 'null' token, or not an Object.</exception>
		[Pure]
		public static JsonObject ParseObjectReadOnly(ReadOnlySpan<byte> jsonBytes, CrystalJsonSettings? settings = null)
		{
			return CrystalJson.Parse(jsonBytes, settings?.AsReadOnly() ?? CrystalJsonSettings.JsonReadOnly).AsObject();
		}

		/// <summary>Parses a buffer containing a document</summary>
		/// <param name="jsonBytes">UTF-8 encoded bytes</param>
		/// <param name="settings">Serialization settings (use default JSON settings if null)</param>
		/// <returns>Corresponding JSON value. If <paramref name="jsonBytes"/> is null or empty, will return <see cref="JsonNull.Missing"/></returns>
		/// <remarks>
		/// <para>The value may be mutable (for objects and arrays) and can be modified. If you require an immutable thread-safe value, please use <see cref="ParseReadOnly(System.Slice,Doxense.Serialization.Json.CrystalJsonSettings?)"/> instead.</para>
		/// <para>If the result is always expected to be an Array or an Object, please call either <see cref="ParseArray(System.Slice,Doxense.Serialization.Json.CrystalJsonSettings?)"/> or <see cref="ParseObject(string?,Doxense.Serialization.Json.CrystalJsonSettings?)"/>.</para>
		/// </remarks>
		/// <exception cref="FormatException">If the JSON document is not syntaxically correct.</exception>
		[Pure]
		public static JsonValue Parse(Slice jsonBytes, CrystalJsonSettings? settings = null)
		{
			return CrystalJson.Parse(jsonBytes, settings);
		}

		/// <summary>Parses a buffer containing a document</summary>
		[Pure]
		public static JsonValue ParseReadOnly(Slice jsonBytes, CrystalJsonSettings? settings = null)
		{
			return CrystalJson.Parse(jsonBytes, settings?.AsReadOnly() ?? CrystalJsonSettings.JsonReadOnly);
		}

		/// <summary>Parses a buffer containing a document that is expected to be an Array</summary>
		/// <param name="jsonBytes">UTF-8 encoded bytes</param>
		/// <param name="settings">Serialization settings (use default JSON settings if null)</param>
		/// <returns>Corresponding JSON Array.</returns>
		/// <remarks>
		/// <para>The value may be mutable and can be modified. If you require an immutable thread-safe array, please use <see cref="ParseArrayReadOnly(System.Slice,Doxense.Serialization.Json.CrystalJsonSettings?)"/> instead.</para>
		/// </remarks>
		/// <exception cref="FormatException">If the JSON document is not syntaxically correct.</exception>
		[Pure]
		public static JsonArray ParseArray(Slice jsonBytes, CrystalJsonSettings? settings = null)
		{
			return CrystalJson.Parse(jsonBytes, settings).AsArray();
		}

		/// <summary>Parses a buffer containing an UTF-8 encoded JSON Array</summary>
		/// <param name="jsonBytes">UTF-8 encoded bytes</param>
		/// <param name="settings">Serialization settings (use default JSON settings if null)</param>
		/// <returns>Corresponding readonly JSON Array. If <paramref name="jsonBytes"/> is null or empty, will return <see cref="JsonNull.Missing"/></returns>
		/// <exception cref="FormatException">If the JSON document is not syntaxically correct.</exception>
		[Pure]
		public static JsonArray ParseArrayReadOnly(Slice jsonBytes, CrystalJsonSettings? settings = null)
		{
			return CrystalJson.Parse(jsonBytes, settings?.AsReadOnly() ?? CrystalJsonSettings.JsonReadOnly).AsArray();
		}

		/// <summary>Parses a buffer containing an UTF-8 JSON document, and returns the corresponding expected JSON Object</summary>
		/// <param name="jsonBytes">UTF-8 encoded bytes</param>
		/// <param name="settings">Serialization settings (use default JSON settings if null)</param>
		/// <returns>Corresponding JSON Object. If <paramref name="jsonBytes"/> is empty or not an Array, an exception will be thrown</returns>
		/// <remarks>
		/// <para>The value may be mutable (for objects and arrays) and can be modified. If you require an immutable thread-safe value, please use <see cref="ParseObjectReadOnly(System.ReadOnlySpan{byte},Doxense.Serialization.Json.CrystalJsonSettings?)"/> instead.</para>
		/// </remarks>
		/// <exception cref="FormatException">If the JSON document is not syntaxically correct.</exception>
		/// <exception cref="InvalidOperationException">If the JSON document was empty, or the 'null' token, or not an Object.</exception>
		[Pure]
		public static JsonObject ParseObject(Slice jsonBytes, CrystalJsonSettings? settings = null)
		{
			return CrystalJson.Parse(jsonBytes, settings).AsObject();
		}

		/// <summary>Parses a buffer containing an UTF-8 encoded JSON Object</summary>
		/// <param name="jsonBytes">UTF-8 encoded bytes</param>
		/// <param name="settings">Serialization settings (use default JSON settings if null)</param>
		/// <returns>Corresponding readonly JSON Object. If <paramref name="jsonBytes"/> is null or empty, will return <see cref="JsonNull.Missing"/></returns>
		/// <exception cref="FormatException">If the JSON document is not syntaxically correct.</exception>
		[Pure]
		public static JsonObject ParseObjectReadOnly(Slice jsonBytes, CrystalJsonSettings? settings = null)
		{
			return CrystalJson.Parse(jsonBytes, settings?.AsReadOnly() ?? CrystalJsonSettings.JsonReadOnly).AsObject();
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

		/// <summary>Convertit un objet CLR de type inconnu, en une valeur JSON</summary>
		/// <param name="value">Instance à convertir (primitive, classe, struct, array, ...)</param>
		/// <returns>Valeur JSON correspondante (JsonNumber, JsonObject, JsonArray, ...), ou JsonNull.Null si <paramref name="value"/> est null</returns>
		/// <remarks>Perf Hint: Utilisez <see cref="FromValue{T}(T)"/> pour des struct ou classes quand c'est possible, et les implicit cast pour des strings, numbers ou booleans</remarks>
		public static JsonValue FromValueReadOnly(object? value)
		{
			if (value is null) return JsonNull.Null;
			if (value is JsonValue jv) return jv;
			var type = value.GetType();
			//TODO: PERF: Pooling?
			return CrystalJsonDomWriter.DefaultReadOnly.ParseObject(value, type, type);
		}

		/// <summary>Convertit un objet CLR de type inconnu, en une valeur JSON, avec des paramètres de conversion spécifiques</summary>
		/// <param name="value">Instance à convertir (primitive, classe, struct, array, ...)</param>
		/// <param name="settings">Serialization settings (use default JSON settings if null)</param>
		/// <param name="resolver">Optional custom resolver used to bind the value into a managed type.</param>
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
		/// <param name="settings">Serialization settings (use default JSON settings if null)</param>
		/// <param name="resolver">Optional custom resolver used to bind the value into a managed type.</param>
		/// <returns>Valeur JSON correspondante (JsonNumber, JsonObject, JsonArray, ...), ou JsonNull.Null si <paramref name="value"/> est null</returns>
		/// <remarks>Perf Hint: Utilisez <see cref="FromValue{T}(T,CrystalJsonSettings,ICrystalJsonTypeResolver)"/> pour des struct ou classes quand c'est possible, et les implicit cast pour des strings, numbers ou booleans</remarks>
		public static JsonValue FromValueReadOnly(object? value, CrystalJsonSettings settings, ICrystalJsonTypeResolver? resolver = null)
		{
			if (value is null) return JsonNull.Null;
			if (value is JsonValue jv) return jv;
			var type = value.GetType();
			return CrystalJsonDomWriter.CreateReadOnly(settings, resolver).ParseObject(value, type, type);
		}

		/// <summary>Convertit un objet CLR de type inconnu, en une valeur JSON, avec des paramètres de conversion spécifiques</summary>
		/// <param name="value">Instance à convertir (primitive, classe, struct, array, ...)</param>
		/// <param name="declaredType">Type du champ parent qui contenait la valeur de l'objet, qui peut être une interface ou une classe abstraite</param>
		/// <param name="settings">Serialization settings (use default JSON settings if null)</param>
		/// <param name="resolver">Optional custom resolver used to bind the value into a managed type.</param>
		/// <remarks>Perf Hint: Utilisez <see cref="FromValue{T}(T,CrystalJsonSettings,ICrystalJsonTypeResolver)"/> pour des struct ou classes quand c'est possible, et les implicit cast pour des strings, numbers ou booleans</remarks>
		[Pure]
		public static JsonValue FromValue(object? value, Type declaredType, CrystalJsonSettings? settings = null, ICrystalJsonTypeResolver? resolver = null)
		{
			return value != null
				? CrystalJsonDomWriter.Create(settings, resolver).ParseObject(value, declaredType, value.GetType())
				: JsonNull.Null;
		}

		/// <summary>Convertit un objet CLR de type inconnu, en une valeur JSON, avec des paramètres de conversion spécifiques</summary>
		/// <param name="value">Instance à convertir (primitive, classe, struct, array, ...)</param>
		/// <param name="declaredType">Type du champ parent qui contenait la valeur de l'objet, qui peut être une interface ou une classe abstraite</param>
		/// <param name="settings">Serialization settings (use default JSON settings if null)</param>
		/// <param name="resolver">Optional custom resolver used to bind the value into a managed type.</param>
		/// <remarks>Perf Hint: Utilisez <see cref="FromValue{T}(T,CrystalJsonSettings,ICrystalJsonTypeResolver)"/> pour des struct ou classes quand c'est possible, et les implicit cast pour des strings, numbers ou booleans</remarks>
		[Pure]
		public static JsonValue FromValueReadOnly(object? value, Type declaredType, CrystalJsonSettings? settings = null, ICrystalJsonTypeResolver? resolver = null)
		{
			return value != null
				? CrystalJsonDomWriter.CreateReadOnly(settings, resolver).ParseObject(value, declaredType, value.GetType())
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

#if !DEBUG
			if (typeof (T) == typeof (bool)) return JsonBoolean.Return((bool) (object) value!);
			if (typeof (T) == typeof (char)) return JsonString.Return((char) (object) value!);
			if (typeof (T) == typeof (byte)) return JsonNumber.Return((byte) (object) value!);
			if (typeof (T) == typeof (sbyte)) return JsonNumber.Return((sbyte) (object) value!);
			if (typeof (T) == typeof (short)) return JsonNumber.Return((short) (object) value!);
			if (typeof (T) == typeof (ushort)) return JsonNumber.Return((ushort) (object) value!);
			if (typeof (T) == typeof (int)) return JsonNumber.Return((int) (object) value!);
			if (typeof (T) == typeof (uint)) return JsonNumber.Return((uint) (object) value!);
			if (typeof (T) == typeof (long)) return JsonNumber.Return((long) (object) value!);
			if (typeof (T) == typeof (ulong)) return JsonNumber.Return((ulong) (object) value!);
			if (typeof (T) == typeof (float)) return JsonNumber.Return((float) (object) value!);
			if (typeof (T) == typeof (double)) return JsonNumber.Return((double) (object) value!);
			if (typeof (T) == typeof (decimal)) return JsonNumber.Return((decimal) (object) value!);
			if (typeof (T) == typeof (Guid)) return JsonString.Return((Guid) (object) value!);
			if (typeof (T) == typeof (Uuid128)) return JsonString.Return((Uuid128) (object) value!);
			if (typeof (T) == typeof (Uuid96)) return JsonString.Return((Uuid96) (object) value!);
			if (typeof (T) == typeof (Uuid80)) return JsonString.Return((Uuid80) (object) value!);
			if (typeof (T) == typeof (Uuid64)) return JsonString.Return((Uuid64) (object) value!);
			if (typeof (T) == typeof (TimeSpan)) return JsonNumber.Return((TimeSpan) (object) value!);
			if (typeof (T) == typeof (DateTime)) return JsonDateTime.Return((DateTime) (object) value!);
			if (typeof (T) == typeof (DateTimeOffset)) return JsonDateTime.Return((DateTimeOffset) (object) value!);
			if (typeof (T) == typeof (DateOnly)) return JsonDateTime.Return((DateOnly) (object) value!);
			if (typeof (T) == typeof (TimeOnly)) return JsonNumber.Return((TimeOnly) (object) value!);
			if (typeof (T) == typeof (NodaTime.Instant)) return JsonString.Return((NodaTime.Instant) (object) value!);
			if (typeof (T) == typeof (NodaTime.Duration)) return JsonNumber.Return((NodaTime.Duration) (object) value!);
			if (typeof (T) == typeof (NodaTime.LocalDateTime)) return JsonString.Return((NodaTime.LocalDateTime) (object) value!);
			if (typeof (T) == typeof (NodaTime.LocalDate)) return JsonString.Return((NodaTime.LocalDate) (object) value!);
			if (typeof (T) == typeof (NodaTime.LocalTime)) return JsonString.Return((NodaTime.LocalTime) (object) value!);
			if (typeof (T) == typeof (NodaTime.ZonedDateTime)) return JsonString.Return((NodaTime.ZonedDateTime) (object) value!);
			// nullable types
			if (typeof (T) == typeof (bool?)) return JsonBoolean.Return((bool?) (object?) value);
			if (typeof (T) == typeof (char?)) return JsonString.Return((char?) (object?) value);
			if (typeof (T) == typeof (byte?)) return JsonNumber.Return((byte?) (object?) value);
			if (typeof (T) == typeof (sbyte?)) return JsonNumber.Return((sbyte?) (object?) value);
			if (typeof (T) == typeof (short?)) return JsonNumber.Return((short?) (object?) value);
			if (typeof (T) == typeof (ushort?)) return JsonNumber.Return((ushort?) (object?) value);
			if (typeof (T) == typeof (int?)) return JsonNumber.Return((int?) (object?) value);
			if (typeof (T) == typeof (uint?)) return JsonNumber.Return((uint?) (object?) value);
			if (typeof (T) == typeof (long?)) return JsonNumber.Return((long?) (object?) value);
			if (typeof (T) == typeof (ulong?)) return JsonNumber.Return((ulong?) (object?) value);
			if (typeof (T) == typeof (float?)) return JsonNumber.Return((float?) (object?) value);
			if (typeof (T) == typeof (double?)) return JsonNumber.Return((double?) (object?) value);
			if (typeof (T) == typeof (decimal?)) return JsonNumber.Return((decimal?) (object?) value);
			if (typeof (T) == typeof (Guid?)) return JsonString.Return((Guid?) (object?) value);
			if (typeof (T) == typeof (Uuid128?)) return JsonString.Return((Uuid128?) (object?) value);
			if (typeof (T) == typeof (Uuid96?)) return JsonString.Return((Uuid96?) (object?) value);
			if (typeof (T) == typeof (Uuid80?)) return JsonString.Return((Uuid80?) (object?) value);
			if (typeof (T) == typeof (Uuid64?)) return JsonString.Return((Uuid64?) (object?) value);
			if (typeof (T) == typeof (TimeSpan?)) return JsonNumber.Return((TimeSpan?) (object?) value);
			if (typeof (T) == typeof (DateTime?)) return JsonDateTime.Return((DateTime?) (object?) value);
			if (typeof (T) == typeof (DateTimeOffset?)) return JsonDateTime.Return((DateTimeOffset?) (object?) value);
			if (typeof (T) == typeof (DateOnly?)) return JsonDateTime.Return((DateOnly?) (object?) value);
			if (typeof (T) == typeof (TimeOnly?)) return JsonNumber.Return((TimeOnly?) (object?) value);
			if (typeof (T) == typeof (NodaTime.Instant?)) return JsonString.Return((NodaTime.Instant?) (object?) value);
			if (typeof (T) == typeof (NodaTime.Duration?)) return JsonNumber.Return((NodaTime.Duration?) (object?) value);
			if (typeof (T) == typeof (NodaTime.LocalDateTime?)) return JsonString.Return((NodaTime.LocalDateTime?) (object?) value);
			if (typeof (T) == typeof (NodaTime.LocalDate?)) return JsonString.Return((NodaTime.LocalDate?) (object?) value);
			if (typeof (T) == typeof (NodaTime.LocalTime?)) return JsonString.Return((NodaTime.LocalTime?) (object?) value);
			if (typeof (T) == typeof (NodaTime.ZonedDateTime?)) return JsonString.Return((NodaTime.ZonedDateTime?) (object?) value);
#endif
			#endregion </JIT_HACK>

			return CrystalJsonDomWriter.Default.ParseObject(value, typeof (T));
		}

		[Pure]
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static JsonValue FromValueReadOnly<T>(T? value)
		{
			#region <JIT_HACK>

			// En mode RELEASE, le JIT reconnaît les patterns "if (typeof(T) == typeof(VALUETYPE)) { ... }" dans une méthode générique Foo<T> quand T est un ValueType,
			// et les remplace par des "if (true) { ...}" ce qui permet d'éliminer le reste du code (très efficace si le if contient un return!)
			// Egalement, le JIT optimise le "(VALUE_TYPE)(object)value" si T == VALUE_TYPE pour éviter le boxing inutile (le cast intermédiaire en object est pour faire taire le compilateur)
			// => pour le vérifier, il faut inspecter l'asm généré par le JIT au runtime (en mode release, en dehors du debugger, etc...) ce qui n'est pas facile...
			// => vérifié avec .NET 4.6.1 + RyuJIT x64, la méthode FromValue<int> est directement inlinée en l'appel à JsonNumber.Return(...) !

#if !DEBUG
			if (typeof (T) == typeof (bool)) return JsonBoolean.Return((bool) (object) value!);
			if (typeof (T) == typeof (char)) return JsonString.Return((char) (object) value!);
			if (typeof (T) == typeof (byte)) return JsonNumber.Return((byte) (object) value!);
			if (typeof (T) == typeof (sbyte)) return JsonNumber.Return((sbyte) (object) value!);
			if (typeof (T) == typeof (short)) return JsonNumber.Return((short) (object) value!);
			if (typeof (T) == typeof (ushort)) return JsonNumber.Return((ushort) (object) value!);
			if (typeof (T) == typeof (int)) return JsonNumber.Return((int) (object) value!);
			if (typeof (T) == typeof (uint)) return JsonNumber.Return((uint) (object) value!);
			if (typeof (T) == typeof (long)) return JsonNumber.Return((long) (object) value!);
			if (typeof (T) == typeof (ulong)) return JsonNumber.Return((ulong) (object) value!);
			if (typeof (T) == typeof (float)) return JsonNumber.Return((float) (object) value!);
			if (typeof (T) == typeof (double)) return JsonNumber.Return((double) (object) value!);
			if (typeof (T) == typeof (decimal)) return JsonNumber.Return((decimal) (object) value!);
			if (typeof (T) == typeof (Guid)) return JsonString.Return((Guid) (object) value!);
			if (typeof (T) == typeof (Uuid128)) return JsonString.Return((Uuid128) (object) value!);
			if (typeof (T) == typeof (Uuid96)) return JsonString.Return((Uuid96) (object) value!);
			if (typeof (T) == typeof (Uuid80)) return JsonString.Return((Uuid80) (object) value!);
			if (typeof (T) == typeof (Uuid64)) return JsonString.Return((Uuid64) (object) value!);
			if (typeof (T) == typeof (TimeSpan)) return JsonNumber.Return((TimeSpan) (object) value!);
			if (typeof (T) == typeof (DateTime)) return JsonDateTime.Return((DateTime) (object) value!);
			if (typeof (T) == typeof (DateTimeOffset)) return JsonDateTime.Return((DateTimeOffset) (object) value!);
			if (typeof (T) == typeof (NodaTime.Instant)) return JsonString.Return((NodaTime.Instant) (object) value!);
			if (typeof (T) == typeof (NodaTime.Duration)) return JsonNumber.Return((NodaTime.Duration) (object) value!);
			if (typeof (T) == typeof (NodaTime.LocalDateTime)) return JsonString.Return((NodaTime.LocalDateTime) (object) value!);
			if (typeof (T) == typeof (NodaTime.LocalDate)) return JsonString.Return((NodaTime.LocalDate) (object) value!);
			if (typeof (T) == typeof (NodaTime.LocalTime)) return JsonString.Return((NodaTime.LocalTime) (object) value!);
			if (typeof (T) == typeof (NodaTime.ZonedDateTime)) return JsonString.Return((NodaTime.ZonedDateTime) (object) value!);
			// nullable types
			if (typeof (T) == typeof (bool?)) return JsonBoolean.Return((bool?) (object?) value);
			if (typeof (T) == typeof (char?)) return JsonString.Return((char?) (object?) value);
			if (typeof (T) == typeof (byte?)) return JsonNumber.Return((byte?) (object?) value);
			if (typeof (T) == typeof (sbyte?)) return JsonNumber.Return((sbyte?) (object?) value);
			if (typeof (T) == typeof (short?)) return JsonNumber.Return((short?) (object?) value);
			if (typeof (T) == typeof (ushort?)) return JsonNumber.Return((ushort?) (object?) value);
			if (typeof (T) == typeof (int?)) return JsonNumber.Return((int?) (object?) value);
			if (typeof (T) == typeof (uint?)) return JsonNumber.Return((uint?) (object?) value);
			if (typeof (T) == typeof (long?)) return JsonNumber.Return((long?) (object?) value);
			if (typeof (T) == typeof (ulong?)) return JsonNumber.Return((ulong?) (object?) value);
			if (typeof (T) == typeof (float?)) return JsonNumber.Return((float?) (object?) value);
			if (typeof (T) == typeof (double?)) return JsonNumber.Return((double?) (object?) value);
			if (typeof (T) == typeof (decimal?)) return JsonNumber.Return((decimal?) (object?) value);
			if (typeof (T) == typeof (Guid?)) return JsonString.Return((Guid?) (object?) value);
			if (typeof (T) == typeof (Uuid128?)) return JsonString.Return((Uuid128?) (object?) value);
			if (typeof (T) == typeof (Uuid96?)) return JsonString.Return((Uuid96?) (object?) value);
			if (typeof (T) == typeof (Uuid80?)) return JsonString.Return((Uuid80?) (object?) value);
			if (typeof (T) == typeof (Uuid64?)) return JsonString.Return((Uuid64?) (object?) value);
			if (typeof (T) == typeof (TimeSpan?)) return JsonNumber.Return((TimeSpan?) (object?) value);
			if (typeof (T) == typeof (DateTime?)) return JsonDateTime.Return((DateTime?) (object?) value);
			if (typeof (T) == typeof (DateTimeOffset?)) return JsonDateTime.Return((DateTimeOffset?) (object?) value);
			if (typeof (T) == typeof (NodaTime.Instant?)) return JsonString.Return((NodaTime.Instant?) (object?) value);
			if (typeof (T) == typeof (NodaTime.Duration?)) return JsonNumber.Return((NodaTime.Duration?) (object?) value);
			if (typeof (T) == typeof (NodaTime.LocalDateTime?)) return JsonString.Return((NodaTime.LocalDateTime?) (object?) value);
			if (typeof (T) == typeof (NodaTime.LocalDate?)) return JsonString.Return((NodaTime.LocalDate?) (object?) value);
			if (typeof (T) == typeof (NodaTime.LocalTime?)) return JsonString.Return((NodaTime.LocalTime?) (object?) value);
			if (typeof (T) == typeof (NodaTime.ZonedDateTime?)) return JsonString.Return((NodaTime.ZonedDateTime?) (object?) value);
#endif
			#endregion </JIT_HACK>

			return CrystalJsonDomWriter.DefaultReadOnly.ParseObject(value, typeof(T));
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

#if !DEBUG
			if (typeof (T) == typeof (bool)) return JsonBoolean.Return((bool) (object) value!);
			if (typeof (T) == typeof (char)) return JsonString.Return((char) (object) value!);
			if (typeof (T) == typeof (byte)) return JsonNumber.Return((byte) (object) value!);
			if (typeof (T) == typeof (sbyte)) return JsonNumber.Return((sbyte) (object) value!);
			if (typeof (T) == typeof (short)) return JsonNumber.Return((short) (object) value!);
			if (typeof (T) == typeof (ushort)) return JsonNumber.Return((ushort) (object) value!);
			if (typeof (T) == typeof (int)) return JsonNumber.Return((int) (object) value!);
			if (typeof (T) == typeof (uint)) return JsonNumber.Return((uint) (object) value!);
			if (typeof (T) == typeof (long)) return JsonNumber.Return((long) (object) value!);
			if (typeof (T) == typeof (ulong)) return JsonNumber.Return((ulong) (object) value!);
			if (typeof (T) == typeof (float)) return JsonNumber.Return((float) (object) value!);
			if (typeof (T) == typeof (double)) return JsonNumber.Return((double) (object) value!);
			if (typeof (T) == typeof (decimal)) return JsonNumber.Return((decimal) (object) value!);
			if (typeof (T) == typeof (Guid)) return JsonString.Return((Guid) (object) value!);
			if (typeof (T) == typeof (Uuid128)) return JsonString.Return((Uuid128) (object) value!);
			if (typeof (T) == typeof (Uuid96)) return JsonString.Return((Uuid96) (object) value!);
			if (typeof (T) == typeof (Uuid80)) return JsonString.Return((Uuid80) (object) value!);
			if (typeof (T) == typeof (Uuid64)) return JsonString.Return((Uuid64) (object) value!);
			if (typeof (T) == typeof (TimeSpan)) return JsonNumber.Return((TimeSpan) (object) value!);
			if (typeof (T) == typeof (DateTime)) return JsonDateTime.Return((DateTime) (object) value!);
			if (typeof (T) == typeof (DateTimeOffset)) return JsonDateTime.Return((DateTimeOffset) (object) value!);
			if (typeof (T) == typeof (NodaTime.Instant)) return JsonString.Return((NodaTime.Instant) (object) value!);
			if (typeof (T) == typeof (NodaTime.Duration)) return JsonNumber.Return((NodaTime.Duration) (object) value!);
			if (typeof (T) == typeof (NodaTime.LocalDateTime)) return JsonString.Return((NodaTime.LocalDateTime) (object) value!);
			if (typeof (T) == typeof (NodaTime.LocalDate)) return JsonString.Return((NodaTime.LocalDate) (object) value!);
			if (typeof (T) == typeof (NodaTime.LocalTime)) return JsonString.Return((NodaTime.LocalTime) (object) value!);
			if (typeof (T) == typeof (NodaTime.ZonedDateTime)) return JsonString.Return((NodaTime.ZonedDateTime) (object) value!);
			// nullable types
			if (typeof (T) == typeof (bool?)) return JsonBoolean.Return((bool?) (object?) value);
			if (typeof (T) == typeof (char?)) return JsonString.Return((char?) (object?) value);
			if (typeof (T) == typeof (byte?)) return JsonNumber.Return((byte?) (object?) value);
			if (typeof (T) == typeof (sbyte?)) return JsonNumber.Return((sbyte?) (object?) value);
			if (typeof (T) == typeof (short?)) return JsonNumber.Return((short?) (object?) value);
			if (typeof (T) == typeof (ushort?)) return JsonNumber.Return((ushort?) (object?) value);
			if (typeof (T) == typeof (int?)) return JsonNumber.Return((int?) (object?) value);
			if (typeof (T) == typeof (uint?)) return JsonNumber.Return((uint?) (object?) value);
			if (typeof (T) == typeof (long?)) return JsonNumber.Return((long?) (object?) value);
			if (typeof (T) == typeof (ulong?)) return JsonNumber.Return((ulong?) (object?) value);
			if (typeof (T) == typeof (float?)) return JsonNumber.Return((float?) (object?) value);
			if (typeof (T) == typeof (double?)) return JsonNumber.Return((double?) (object?) value);
			if (typeof (T) == typeof (decimal?)) return JsonNumber.Return((decimal?) (object?) value);
			if (typeof (T) == typeof (Guid?)) return JsonString.Return((Guid?) (object?) value);
			if (typeof (T) == typeof (Uuid128?)) return JsonString.Return((Uuid128?) (object?) value);
			if (typeof (T) == typeof (Uuid96?)) return JsonString.Return((Uuid96?) (object?) value);
			if (typeof (T) == typeof (Uuid80?)) return JsonString.Return((Uuid80?) (object?) value);
			if (typeof (T) == typeof (Uuid64?)) return JsonString.Return((Uuid64?) (object?) value);
			if (typeof (T) == typeof (TimeSpan?)) return JsonNumber.Return((TimeSpan?) (object?) value);
			if (typeof (T) == typeof (DateTime?)) return JsonDateTime.Return((DateTime?) (object?) value);
			if (typeof (T) == typeof (DateTimeOffset?)) return JsonDateTime.Return((DateTimeOffset?) (object?) value);
			if (typeof (T) == typeof (NodaTime.Instant?)) return JsonString.Return((NodaTime.Instant?) (object?) value);
			if (typeof (T) == typeof (NodaTime.Duration?)) return JsonNumber.Return((NodaTime.Duration?) (object?) value);
			if (typeof (T) == typeof (NodaTime.LocalDateTime?)) return JsonString.Return((NodaTime.LocalDateTime?) (object?) value);
			if (typeof (T) == typeof (NodaTime.LocalDate?)) return JsonString.Return((NodaTime.LocalDate?) (object?) value);
			if (typeof (T) == typeof (NodaTime.LocalTime?)) return JsonString.Return((NodaTime.LocalTime?) (object?) value);
			if (typeof (T) == typeof (NodaTime.ZonedDateTime?)) return JsonString.Return((NodaTime.ZonedDateTime?) (object?) value);
#endif
			#endregion </JIT_HACK>

			//TODO: PERF: Pooling?
			return writer.ParseObjectInternal(ref context, value, typeof (T), null);
		}

		/// <summary>Convertit un objet CLR de type bien déterminé, en une valeur JSON, avec des paramètres de conversion spécifiques</summary>
		/// <typeparam name="T">Type déclaré de la valeur à convertir</typeparam>
		/// <param name="value">Valeur à convertir (primitive, classe, struct, array, ...)</param>
		/// <param name="settings">Serialization settings (use default JSON settings if null)</param>
		/// <param name="resolver">Optional custom resolver used to bind the value into a managed type.</param>
		/// <returns>Valeur JSON correspondante (string, number, object, array, ...), ou JsonNull.Null si <paramref name="value"/> est null</returns>
		/// <remarks>Perf Hint: pour des strings, numbers ou bools, utilisez plutôt le cast implicit!</remarks>
		[Pure]
		public static JsonValue FromValue<T>(T? value, CrystalJsonSettings? settings, ICrystalJsonTypeResolver? resolver = null)
		{
			return value != null ? CrystalJsonDomWriter.Create(settings, resolver).ParseObject(value, typeof(T)) : JsonNull.Null;
		}

		/// <summary>Convertit un objet CLR de type bien déterminé, en une valeur JSON, avec des paramètres de conversion spécifiques</summary>
		/// <typeparam name="T">Type déclaré de la valeur à convertir</typeparam>
		/// <param name="value">Valeur à convertir (primitive, classe, struct, array, ...)</param>
		/// <param name="settings">Serialization settings (use default JSON settings if null)</param>
		/// <param name="resolver">Optional custom resolver used to bind the value into a managed type.</param>
		/// <returns>Valeur JSON correspondante (string, number, object, array, ...), ou JsonNull.Null si <paramref name="value"/> est null</returns>
		/// <remarks>Perf Hint: pour des strings, numbers ou bools, utilisez plutôt le cast implicit!</remarks>
		[Pure]
		public static JsonValue FromValueReadOnly<T>(T? value, CrystalJsonSettings? settings, ICrystalJsonTypeResolver? resolver = null)
		{
			return value != null ? CrystalJsonDomWriter.CreateReadOnly(settings, resolver).ParseObject(value, typeof(T)) : JsonNull.Null;
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

		#endregion

	}

}
