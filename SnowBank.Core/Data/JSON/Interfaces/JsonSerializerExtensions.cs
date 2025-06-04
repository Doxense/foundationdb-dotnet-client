#region Copyright (c) 2023-2025 SnowBank SAS, (c) 2005-2023 Doxense SAS
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

namespace SnowBank.Data.Json
{
	using System.Buffers;
	using System.Collections.Immutable;
	using System.Runtime.InteropServices;
	using SnowBank.Buffers;

	/// <summary>Helper methods for working with JSON converters</summary>
	[PublicAPI]
	public static class JsonSerializerExtensions
	{

		#region IJsonSerializer<T>...

		/// <summary>Serializes a value (of any type) into a string literal, using a customer serializer</summary>
		/// <param name="serializer">Custom serializer</param>
		/// <param name="value">Instance to serialize (can be null)</param>
		/// <param name="settings">Serialization settings (use default JSON settings if null)</param>
		/// <param name="resolver">Custom type resolver (use default behavior if null)</param>
		/// <returns><c>`123`</c>, <c>`true`</c>, <c>`"ABC"`</c>, <c>`{ "foo":..., "bar": ... }`</c>, <c>`[ ... ]`</c>, ...</returns>
		/// <exception cref="JsonSerializationException">If the object fails to serialize properly (non-serializable type, loop in the object graph, ...)</exception>
		public static string ToJson<T>(this IJsonSerializer<T> serializer, T? value, CrystalJsonSettings? settings = null, ICrystalJsonTypeResolver? resolver = null)
		{
			return CrystalJson.Serialize<T>(value, serializer, settings, resolver);
		}

		/// <summary>Serializes a value of type <typeparamref name="T"/> into a <see cref="Slice"/>, using a customer serializer</summary>
		/// <param name="serializer">Custom serializer</param>
		/// <param name="value">Instance to serialize (can be null)</param>
		/// <param name="settings">Serialization settings (use default JSON settings if null)</param>
		/// <param name="resolver">Custom type resolver (use default behavior if null)</param>
		/// <returns><c>`123`</c>, <c>`true`</c>, <c>`"ABC"`</c>, <c>`{ "foo":..., "bar": ... }`</c>, <c>`[ ... ]`</c>, ...</returns>
		/// <exception cref="JsonSerializationException">If the object fails to serialize properly (non-serializable type, loop in the object graph, ...)</exception>
		public static Slice ToSlice<T>(this IJsonSerializer<T> serializer, T? value, CrystalJsonSettings? settings = null, ICrystalJsonTypeResolver? resolver = null)
		{
			return CrystalJson.ToSlice<T>(value, serializer, settings, resolver);
		}

		/// <summary>Serializes a value of type <typeparamref name="T"/> into a <see cref="SliceOwner"/>, using a customer serializer, and the specified <see cref="ArrayPool{T}">pool</see></summary>
		/// <param name="serializer">Custom serializer</param>
		/// <param name="value">Instance to serialize (can be null)</param>
		/// <param name="pool">Pool used to allocate the content of the slice.</param>
		/// <param name="settings">Serialization settings (use default JSON settings if null)</param>
		/// <param name="resolver">Custom type resolver (use default behavior if null)</param>
		/// <returns><c>`123`</c>, <c>`true`</c>, <c>`"ABC"`</c>, <c>`{ "foo":..., "bar": ... }`</c>, <c>`[ ... ]`</c>, ...</returns>
		/// <exception cref="JsonSerializationException">If the object fails to serialize properly (non-serializable type, loop in the object graph, ...)</exception>
		/// <remarks>
		/// <para>The <see cref="SliceOwner"/> returned <b>MUST</b> be disposed; otherwise, the rented buffer will not be returned to the <paramref name="pool"/>.</para>
		/// </remarks>
		public static SliceOwner ToSlice<T>(this IJsonSerializer<T> serializer, T? value, ArrayPool<byte> pool, CrystalJsonSettings? settings = null, ICrystalJsonTypeResolver? resolver = null)
		{
			return CrystalJson.ToSlice<T>(value, serializer, pool, settings, resolver);
		}

		#endregion

		#region IJsonDeserializer<T>...

		/// <summary>Deserializes a JSON text literal into an instance of type <typeparamref name="T" /></summary>
		/// <param name="serializer">Deserializer instance to use</param>
		/// <param name="jsonText">JSON text document to parse</param>
		/// <param name="settings">Serialization settings (use default JSON settings if null)</param>
		/// <param name="resolver">Optional custom resolver</param>
		/// <returns>Deserialized value</returns>
		/// <exception cref="FormatException">If the JSON document is not syntactically correct.</exception>
		/// <exception cref="JsonBindingException">If the parsed JSON document cannot be bound to an instance of <typeparamref name="T"/>.</exception>
		public static T Deserialize<T>(this IJsonDeserializer<T> serializer, string jsonText, CrystalJsonSettings? settings = null, ICrystalJsonTypeResolver? resolver = null)
			where T : notnull
		{
			return serializer.Unpack(CrystalJson.Parse(jsonText, settings), resolver);
		}

		/// <summary>Deserializes a JSON text literal into an instance of type <typeparamref name="T" /></summary>
		/// <param name="serializer">Deserializer instance to use</param>
		/// <param name="jsonText">JSON text document to parse</param>
		/// <param name="settings">Serialization settings (use default JSON settings if null)</param>
		/// <param name="resolver">Optional custom resolver</param>
		/// <returns>Deserialized value</returns>
		/// <exception cref="FormatException">If the JSON document is not syntactically correct.</exception>
		/// <exception cref="JsonBindingException">If the parsed JSON document cannot be bound to an instance of <typeparamref name="T"/>.</exception>
		public static T Deserialize<T>(this IJsonDeserializer<T> serializer, ReadOnlySpan<char> jsonText, CrystalJsonSettings? settings = null, ICrystalJsonTypeResolver? resolver = null)
			where T : notnull
		{
			return serializer.Unpack(CrystalJson.Parse(jsonText, settings), resolver);
		}

		/// <summary>Deserializes a JSON text literal into an instance of type <typeparamref name="T" /></summary>
		/// <param name="serializer">Deserializer instance to use</param>
		/// <param name="jsonBytes">JSON text document to parse, encoded as UTF-8</param>
		/// <param name="settings">Serialization settings (use default JSON settings if null)</param>
		/// <param name="resolver">Optional custom resolver</param>
		/// <returns>Deserialized value</returns>
		/// <exception cref="FormatException">If the JSON document is not syntactically correct.</exception>
		/// <exception cref="JsonBindingException">If the parsed JSON document cannot be bound to an instance of <typeparamref name="T"/>.</exception>
		public static T Deserialize<T>(this IJsonDeserializer<T> serializer, Slice jsonBytes, CrystalJsonSettings? settings = null, ICrystalJsonTypeResolver? resolver = null)
			where T : notnull
		{
			return serializer.Unpack(CrystalJson.Parse(jsonBytes, settings), resolver);
		}

		/// <summary>Deserializes a JSON text literal into an instance of type <typeparamref name="T" /></summary>
		/// <param name="serializer">Deserializer instance to use</param>
		/// <param name="jsonBytes">JSON text document to parse, encoded as UTF-8</param>
		/// <param name="settings">Serialization settings (use default JSON settings if null)</param>
		/// <param name="resolver">Optional custom resolver</param>
		/// <returns>Deserialized value</returns>
		/// <exception cref="FormatException">If the JSON document is not syntactically correct.</exception>
		/// <exception cref="JsonBindingException">If the parsed JSON document cannot be bound to an instance of <typeparamref name="T"/>.</exception>
		public static T Deserialize<T>(this IJsonDeserializer<T> serializer, ReadOnlySpan<byte> jsonBytes, CrystalJsonSettings? settings = null, ICrystalJsonTypeResolver? resolver = null)
			where T : notnull
		{
			return serializer.Unpack(CrystalJson.Parse(jsonBytes, settings), resolver);
		}

		/// <summary>Deserializes a JSON value into an array of <typeparamref name="T"/></summary>
		/// <param name="serializer">Deserializer instance to use</param>
		/// <param name="destination">Destination buffer, which must be large enough to fit the deserialized elements</param>
		/// <param name="written">Number of items written into the <paramref name="destination"/></param>
		/// <param name="value">JSON value to unpack</param>
		/// <param name="resolver">Optional custom resolver</param>
		/// <returns><c>true</c> if the deserialization was successful; otherwise, <c>false</c></returns>
		public static bool TryUnpackArray<T>(this IJsonDeserializer<T> serializer, Span<T> destination, out int written, JsonValue value, ICrystalJsonTypeResolver? resolver = null)
		{
			if (value.IsNullOrMissing())
			{
				written = 0;
				return true;
			}

			var arr = value.AsArray();
			var input = arr.GetSpan();
			if (destination.Length < input.Length)
			{
				written = 0;
				return false;
			}

			try
			{
				for (int i = 0; i < input.Length; i++)
				{
					destination[i] = serializer.Unpack(input[i], resolver);
				}
			}
			catch (JsonBindingException)
			{
				destination[..input.Length].Clear();
				written = 0;
				return false;
			}

			written = input.Length;
			return true;
		}

		/// <summary>Deserializes a JSON value stored in an optional field, into an array of <typeparamref name="T"/></summary>
		/// <param name="serializer">Deserializer instance to use</param>
		/// <param name="value">JSON value to unpack</param>
		/// <param name="defaultValue">Default value to return, if <paramref name="value"/> is null or missing.</param>
		/// <param name="resolver">Optional custom resolver</param>
		/// <param name="fieldName">Name of the field that holds this value in its parent objet (used when throwing exceptions)</param>
		/// <returns>Deserialized array</returns>
		/// <exception cref="JsonBindingException">If an element of the array cannot be bound to an instance of <typeparamref name="T"/>.</exception>
		[Pure]
		[return: NotNullIfNotNull(nameof(defaultValue))]
		public static T[]? UnpackArray<T>(this IJsonDeserializer<T> serializer, JsonValue? value, T[]? defaultValue = null, ICrystalJsonTypeResolver? resolver = null, string? fieldName = null)
			=> value switch
			{
				JsonArray arr => UnpackArray(serializer, arr, resolver),
				null or JsonNull => defaultValue,
				_ => throw (fieldName != null ? CrystalJson.Errors.Parsing_CannotCastFieldToJsonArray(value, fieldName) : CrystalJson.Errors.Parsing_CannotCastToJsonArray(value))
			};

		/// <summary>Deserializes a JSON value stored in an optional field, into an array of <typeparamref name="T"/></summary>
		/// <param name="value">JSON value to unpack</param>
		/// <param name="defaultValue">Default value to return, if <paramref name="value"/> is null or missing.</param>
		/// <param name="resolver">Optional custom resolver</param>
		/// <param name="fieldName">Name of the field that holds this value in its parent objet (used when throwing exceptions)</param>
		/// <returns>Deserialized array</returns>
		/// <exception cref="JsonBindingException">If an element of the array cannot be bound to an instance of <typeparamref name="T"/>.</exception>
		[Pure]
		[return: NotNullIfNotNull(nameof(defaultValue))]
		public static T?[]? UnpackArray<T>(JsonValue? value, T[]? defaultValue = null, ICrystalJsonTypeResolver? resolver = null, string? fieldName = null)
			=> value switch
			{
				JsonArray arr => arr.ToArray<T>(resolver: resolver),
				null or JsonNull => defaultValue,
				_ => throw (fieldName != null ? CrystalJson.Errors.Parsing_CannotCastFieldToJsonArray(value, fieldName) : CrystalJson.Errors.Parsing_CannotCastToJsonArray(value))
			};

		/// <summary>Deserializes a JSON value stored in a <b>required</b> field, into an array of <typeparamref name="T"/></summary>
		/// <param name="serializer">Deserializer instance to use</param>
		/// <param name="value">JSON value to unpack</param>
		/// <param name="resolver">Optional custom resolver</param>
		/// <param name="parent">Parent JSON object that holds this field (used when throwing exceptions).</param>
		/// <param name="fieldName">Name of the field in the parent JSON object that holds this value (used when throwing exceptions).</param>
		/// <returns>Deserialized array</returns>
		/// <exception cref="JsonBindingException">If an element of the array cannot be bound to an instance of <typeparamref name="T"/>.</exception>
		[Pure]
		public static T[] UnpackRequiredArray<T>(this IJsonDeserializer<T> serializer, JsonValue? value, ICrystalJsonTypeResolver? resolver = null, JsonValue? parent = null, string? fieldName = null)
			=> value switch
			{
				JsonArray arr => UnpackArray(serializer, arr, resolver),
				null or JsonNull => throw (fieldName != null ? CrystalJson.Errors.Parsing_FieldIsNullOrMissing(parent, fieldName, null) : CrystalJson.Errors.Parsing_ValueIsNullOrMissing()),
				_ => throw (fieldName != null ? CrystalJson.Errors.Parsing_CannotCastFieldToJsonArray(value, fieldName) : CrystalJson.Errors.Parsing_CannotCastToJsonArray(value))
			};

		/// <summary>Deserializes a JSON value stored in a <b>required</b> field, into an array of <typeparamref name="T"/></summary>
		/// <param name="value">JSON value to unpack</param>
		/// <param name="resolver">Optional custom resolver</param>
		/// <param name="parent">Parent JSON object that holds this field (used when throwing exceptions).</param>
		/// <param name="fieldName">Name of the field in the parent JSON object that holds this value (used when throwing exceptions).</param>
		/// <returns>Deserialized array</returns>
		/// <exception cref="JsonBindingException">If an element of the array cannot be bound to an instance of <typeparamref name="T"/>.</exception>
		[Pure]
		public static T[] UnpackRequiredArray<T>(JsonValue? value, ICrystalJsonTypeResolver? resolver = null, JsonValue? parent = null, string? fieldName = null)
			=> value switch
			{
				JsonArray arr => (T[]) arr.ToArray<T>(resolver: resolver)!,
				null or JsonNull => throw (fieldName != null ? CrystalJson.Errors.Parsing_FieldIsNullOrMissing(parent, fieldName, null) : CrystalJson.Errors.Parsing_ValueIsNullOrMissing()),
				_ => throw (fieldName != null ? CrystalJson.Errors.Parsing_CannotCastFieldToJsonArray(value, fieldName) : CrystalJson.Errors.Parsing_CannotCastToJsonArray(value))
			};

		/// <summary>Deserializes a JSON value stored in an optional field, into an array of <typeparamref name="T"/></summary>
		/// <param name="serializer">Deserializer instance to use</param>
		/// <param name="array">JSON array to unpack</param>
		/// <param name="resolver">Optional custom resolver</param>
		/// <returns>Deserialized array</returns>
		/// <exception cref="JsonBindingException">If an element of the array cannot be bound to an instance of <typeparamref name="T"/>.</exception>
		[Pure]
		public static T[] UnpackArray<T>(this IJsonDeserializer<T> serializer, JsonArray array, ICrystalJsonTypeResolver? resolver = null)
		{
			var input = array.GetSpan();
			var result = new T[input.Length];

			for (int i = 0; i < input.Length; i++)
			{
				result[i] = serializer.Unpack(input[i], resolver);
			}

			return result;
		}

		/// <summary>Deserializes a JSON value stored in a <b>required</b> field, into an array of <see cref="string"/>.</summary>
		/// <param name="value">JSON value to unpack</param>
		/// <param name="parent">Parent JSON object that holds this field (used when throwing exceptions).</param>
		/// <param name="fieldName">Name of the field in the parent JSON object that holds this value (used when throwing exceptions).</param>
		/// <returns>Deserialized array</returns>
		/// <exception cref="JsonBindingException">If an element of the array cannot be bound to a string.</exception>
		[Pure]
		public static string[] UnpackRequiredStringArray(JsonValue? value, JsonValue? parent = null, string? fieldName = null)
			=> value switch
			{
				JsonArray arr => (string[]) arr.ToStringArray()!,
				null or JsonNull => throw (fieldName != null ? CrystalJson.Errors.Parsing_FieldIsNullOrMissing(parent, fieldName, null) : CrystalJson.Errors.Parsing_ValueIsNullOrMissing()),
				_ => throw (fieldName != null ? CrystalJson.Errors.Parsing_CannotCastFieldToJsonArray(value, fieldName) : CrystalJson.Errors.Parsing_CannotCastToJsonArray(value))
			};

		/// <summary>Deserializes a JSON value stored in an optional field, into an array of <see cref="string"/>.</summary>
		/// <param name="value">JSON value to unpack</param>
		/// <param name="defaultValue">Default value to return, if <paramref name="value"/> is null or missing.</param>
		/// <param name="fieldName">Name of the field in the parent JSON object that holds this value (used when throwing exceptions).</param>
		/// <returns>Deserialized array</returns>
		/// <exception cref="JsonBindingException">If an element of the array cannot be bound to a string.</exception>
		[Pure]
		[return: NotNullIfNotNull(nameof(defaultValue))]
		public static string?[]? UnpackStringArray(JsonValue? value, string[]? defaultValue = null, string? fieldName = null)
			=> value switch
			{
				JsonArray arr => arr.ToStringArray(),
				null or JsonNull => defaultValue,
				_ => throw (fieldName != null ? CrystalJson.Errors.Parsing_CannotCastFieldToJsonArray(value, fieldName) : CrystalJson.Errors.Parsing_CannotCastToJsonArray(value))
			};

		/// <summary>Deserializes a JSON value stored in a <b>required</b> field, into an array of <see cref="int"/>.</summary>
		/// <param name="value">JSON value to unpack</param>
		/// <param name="parent">Parent JSON object that holds this field (used when throwing exceptions).</param>
		/// <param name="fieldName">Name of the field in the parent JSON object that holds this value (used when throwing exceptions).</param>
		/// <returns>Deserialized array</returns>
		/// <exception cref="JsonBindingException">If an element of the array cannot be bound to a string.</exception>
		[Pure]
		public static int[] UnpackRequiredInt32Array(JsonValue? value, JsonValue? parent = null, string? fieldName = null)
			=> value switch
			{
				JsonArray arr => arr.ToInt32Array(),
				null or JsonNull => throw (fieldName != null ? CrystalJson.Errors.Parsing_FieldIsNullOrMissing(parent, fieldName, null) : CrystalJson.Errors.Parsing_ValueIsNullOrMissing()),
				_ => throw (fieldName != null ? CrystalJson.Errors.Parsing_CannotCastFieldToJsonArray(value, fieldName) : CrystalJson.Errors.Parsing_CannotCastToJsonArray(value))
			};

		/// <summary>Deserializes a JSON value stored in an optional field, into an array of <see cref="int"/>.</summary>
		/// <param name="value">JSON value to unpack</param>
		/// <param name="defaultValue">Default value to return, if <paramref name="value"/> is null or missing.</param>
		/// <param name="fieldName">Name of the field in the parent JSON object that holds this value (used when throwing exceptions).</param>
		/// <returns>Deserialized array</returns>
		/// <exception cref="JsonBindingException">If an element of the array cannot be bound to a string.</exception>
		[Pure]
		[return: NotNullIfNotNull(nameof(defaultValue))]
		public static int[]? UnpackInt32Array(JsonValue? value, int[]? defaultValue = null, string? fieldName = null)
			=> value switch
			{
				JsonArray arr => arr.ToInt32Array(),
				null or JsonNull => defaultValue,
				_ => throw (fieldName != null ? CrystalJson.Errors.Parsing_CannotCastFieldToJsonArray(value, fieldName) : CrystalJson.Errors.Parsing_CannotCastToJsonArray(value))
			};

		/// <summary>Deserializes a JSON value stored in a <b>required</b> field, into an array of <see cref="long"/>.</summary>
		/// <param name="value">JSON value to unpack</param>
		/// <param name="parent">Parent JSON object that holds this field (used when throwing exceptions).</param>
		/// <param name="fieldName">Name of the field in the parent JSON object that holds this value (used when throwing exceptions).</param>
		/// <returns>Deserialized array</returns>
		/// <exception cref="JsonBindingException">If an element of the array cannot be bound to a string.</exception>
		[Pure]
		public static long[] UnpackRequiredInt64Array(JsonValue? value, JsonValue? parent = null, string? fieldName = null)
			=> value switch
			{
				JsonArray arr => arr.ToInt64Array(),
				null or JsonNull => throw (fieldName != null ? CrystalJson.Errors.Parsing_FieldIsNullOrMissing(parent, fieldName, null) : CrystalJson.Errors.Parsing_ValueIsNullOrMissing()),
				_ => throw (fieldName != null ? CrystalJson.Errors.Parsing_CannotCastFieldToJsonArray(value, fieldName) : CrystalJson.Errors.Parsing_CannotCastToJsonArray(value))
			};

		/// <summary>Deserializes a JSON value stored in an optional field, into an array of <see cref="long"/>.</summary>
		/// <param name="value">JSON value to unpack</param>
		/// <param name="defaultValue">Default value to return, if <paramref name="value"/> is null or missing.</param>
		/// <param name="fieldName">Name of the field in the parent JSON object that holds this value (used when throwing exceptions).</param>
		/// <returns>Deserialized array</returns>
		/// <exception cref="JsonBindingException">If an element of the array cannot be bound to a string.</exception>
		[Pure]
		[return: NotNullIfNotNull(nameof(defaultValue))]
		public static long[]? UnpackInt64Array(JsonValue? value, long[]? defaultValue = null, string? fieldName = null)
			=> value switch
			{
				JsonArray arr => arr.ToInt64Array(),
				null or JsonNull => defaultValue,
				_ => throw (fieldName != null ? CrystalJson.Errors.Parsing_CannotCastFieldToJsonArray(value, fieldName) : CrystalJson.Errors.Parsing_CannotCastToJsonArray(value))
			};

		/// <summary>Deserializes a JSON value stored in a <b>required</b> field, into an array of <see cref="double"/>.</summary>
		/// <param name="value">JSON value to unpack</param>
		/// <param name="parent">Parent JSON object that holds this field (used when throwing exceptions).</param>
		/// <param name="fieldName">Name of the field in the parent JSON object that holds this value (used when throwing exceptions).</param>
		/// <returns>Deserialized array</returns>
		/// <exception cref="JsonBindingException">If an element of the array cannot be bound to a string.</exception>
		[Pure]
		public static double[] UnpackRequiredDoubleArray(JsonValue? value, JsonValue? parent = null, string? fieldName = null)
			=> value switch
			{
				JsonArray arr => arr.ToDoubleArray(),
				null or JsonNull => throw (fieldName != null ? CrystalJson.Errors.Parsing_FieldIsNullOrMissing(parent, fieldName, null) : CrystalJson.Errors.Parsing_ValueIsNullOrMissing()),
				_ => throw (fieldName != null ? CrystalJson.Errors.Parsing_CannotCastFieldToJsonArray(value, fieldName) : CrystalJson.Errors.Parsing_CannotCastToJsonArray(value))
			};

		/// <summary>Deserializes a JSON value stored in an optional field, into an array of <see cref="double"/>.</summary>
		/// <param name="value">JSON value to unpack</param>
		/// <param name="defaultValue">Default value to return, if <paramref name="value"/> is null or missing.</param>
		/// <param name="fieldName">Name of the field in the parent JSON object that holds this value (used when throwing exceptions).</param>
		/// <returns>Deserialized array</returns>
		/// <exception cref="JsonBindingException">If an element of the array cannot be bound to a string.</exception>
		[Pure]
		[return: NotNullIfNotNull(nameof(defaultValue))]
		public static double[]? UnpackDoubleArray(JsonValue? value, double[]? defaultValue = null, string? fieldName = null)
			=> value switch
			{
				JsonArray arr => arr.ToDoubleArray(),
				null or JsonNull => defaultValue,
				_ => throw (fieldName != null ? CrystalJson.Errors.Parsing_CannotCastFieldToJsonArray(value, fieldName) : CrystalJson.Errors.Parsing_CannotCastToJsonArray(value))
			};

		/// <summary>Deserializes a <see cref="JsonArray"/> into a <see cref="List{T}"/>, stored into an optional field of a parent object</summary>
		[Pure]
		[return: NotNullIfNotNull(nameof(defaultValue))]
		public static List<T>? UnpackList<T>(this IJsonDeserializer<T> serializer, JsonValue? value, List<T>? defaultValue = null, ICrystalJsonTypeResolver? resolver = null, string? fieldName = null)
			=> value switch
			{
				JsonArray arr => UnpackList(serializer, arr, resolver),
				null or JsonNull => defaultValue,
				_ => throw (fieldName != null ? CrystalJson.Errors.Parsing_CannotCastFieldToJsonArray(value, fieldName) : CrystalJson.Errors.Parsing_CannotCastToJsonArray(value))
			};

		/// <summary>Deserializes a <see cref="JsonArray"/> into a <see cref="List{T}"/>, stored into an optional field of a parent object</summary>
		[Pure]
		[return: NotNullIfNotNull(nameof(defaultValue))]
		public static List<T?>? UnpackList<T>(JsonValue? value, List<T?>? defaultValue = null, ICrystalJsonTypeResolver? resolver = null, string? fieldName = null)
			=> value switch
			{
				JsonArray arr => arr.ToList<T>(resolver: resolver),
				null or JsonNull => defaultValue,
				_ => throw (fieldName != null ? CrystalJson.Errors.Parsing_CannotCastFieldToJsonArray(value, fieldName) : CrystalJson.Errors.Parsing_CannotCastToJsonArray(value))
			};

		/// <summary>Deserializes a <see cref="JsonArray"/> into a <see cref="List{T}"/>, stored into a <b>required</b> field of a parent object</summary>
		[Pure]
		public static List<T> UnpackRequiredList<T>(this IJsonDeserializer<T> serializer, JsonValue? value, ICrystalJsonTypeResolver? resolver = null, JsonValue? parent = null, string? fieldName = null)
			=> value switch
			{
				JsonArray arr => UnpackList(serializer, arr, resolver),
				null or JsonNull => throw (fieldName != null ? CrystalJson.Errors.Parsing_FieldIsNullOrMissing(parent, fieldName, null) : CrystalJson.Errors.Parsing_ValueIsNullOrMissing()),
				_ => throw (fieldName != null ? CrystalJson.Errors.Parsing_CannotCastFieldToJsonArray(value, fieldName) : CrystalJson.Errors.Parsing_CannotCastToJsonArray(value))
			};

		/// <summary>Deserializes a <see cref="JsonArray"/> into a <see cref="List{T}"/> of <see cref="string"/>, stored into a <b>required</b> field of a parent object</summary>
		[Pure]
		public static List<T> UnpackRequiredList<T>(JsonValue? value, ICrystalJsonTypeResolver? resolver = null, JsonValue? parent = null, string? fieldName = null)
			=> value switch
			{
				JsonArray arr => (List<T>) arr.ToList<T>(resolver: resolver)!,
				null or JsonNull => throw (fieldName != null ? CrystalJson.Errors.Parsing_FieldIsNullOrMissing(parent, fieldName, null) : CrystalJson.Errors.Parsing_ValueIsNullOrMissing()),
				_ => throw (fieldName != null ? CrystalJson.Errors.Parsing_CannotCastFieldToJsonArray(value, fieldName) : CrystalJson.Errors.Parsing_CannotCastToJsonArray(value))
			};

		/// <summary>Deserializes a <see cref="JsonArray"/> into a <see cref="List{T}"/> of <see cref="string"/>, stored into an optional field of a parent object</summary>
		[Pure]
		public static List<string> UnpackRequiredStringList(JsonValue? value, JsonValue? parent = null, string? fieldName = null)
			=> value switch
			{
				JsonArray arr => (List<string>) arr.ToStringList()!,
				null or JsonNull => throw (fieldName != null ? CrystalJson.Errors.Parsing_FieldIsNullOrMissing(parent, fieldName, null) : CrystalJson.Errors.Parsing_ValueIsNullOrMissing()),
				_ => throw (fieldName != null ? CrystalJson.Errors.Parsing_CannotCastFieldToJsonArray(value, fieldName) : CrystalJson.Errors.Parsing_CannotCastToJsonArray(value))
			};

		/// <summary>Deserializes a <see cref="JsonArray"/> into a <see cref="List{T}"/> of <see cref="string"/>, stored into a <b>required</b> field of a parent object</summary>
		[Pure]
		[return: NotNullIfNotNull(nameof(defaultValue))]
		public static List<string?>? UnpackStringList(JsonValue? value, List<string?>? defaultValue = null, string? fieldName = null)
			=> value switch
			{
				JsonArray arr => arr.ToStringList(),
				null or JsonNull => defaultValue,
				_ => throw (fieldName != null ? CrystalJson.Errors.Parsing_CannotCastFieldToJsonArray(value, fieldName) : CrystalJson.Errors.Parsing_CannotCastToJsonArray(value))
			};

		/// <summary>Deserializes a <see cref="JsonArray"/> into a <see cref="List{T}"/> of <see cref="int"/>, stored into a <b>required</b> field of a parent object</summary>
		[Pure]
		public static List<int> UnpackRequiredInt32List(JsonValue? value, JsonValue? parent = null, string? fieldName = null)
			=> value switch
			{
				JsonArray arr => arr.ToInt32List(),
				null or JsonNull => throw (fieldName != null ? CrystalJson.Errors.Parsing_FieldIsNullOrMissing(parent, fieldName, null) : CrystalJson.Errors.Parsing_ValueIsNullOrMissing()),
				_ => throw (fieldName != null ? CrystalJson.Errors.Parsing_CannotCastFieldToJsonArray(value, fieldName) : CrystalJson.Errors.Parsing_CannotCastToJsonArray(value))
			};

		/// <summary>Deserializes a <see cref="JsonArray"/> into a <see cref="List{T}"/> of <see cref="int"/>, stored into an optional field of a parent object</summary>
		[Pure]
		[return: NotNullIfNotNull(nameof(defaultValue))]
		public static List<int>? UnpackInt32List(JsonValue? value, List<int>? defaultValue = null, string? fieldName = null)
			=> value switch
			{
				JsonArray arr => arr.ToInt32List(),
				null or JsonNull => defaultValue,
				_ => throw (fieldName != null ? CrystalJson.Errors.Parsing_CannotCastFieldToJsonArray(value, fieldName) : CrystalJson.Errors.Parsing_CannotCastToJsonArray(value))
			};

		/// <summary>Deserializes a <see cref="JsonArray"/> into a <see cref="List{T}"/> of <see cref="long"/>, stored into a <b>required</b> field of a parent object</summary>
		[Pure]
		public static List<long> UnpackRequiredInt64List(JsonValue? value, JsonValue? parent = null, string? fieldName = null)
			=> value switch
			{
				JsonArray arr => arr.ToInt64List(),
				null or JsonNull => throw (fieldName != null ? CrystalJson.Errors.Parsing_FieldIsNullOrMissing(parent, fieldName, null) : CrystalJson.Errors.Parsing_ValueIsNullOrMissing()),
				_ => throw (fieldName != null ? CrystalJson.Errors.Parsing_CannotCastFieldToJsonArray(value, fieldName) : CrystalJson.Errors.Parsing_CannotCastToJsonArray(value))
			};

		/// <summary>Deserializes a <see cref="JsonArray"/> into a <see cref="List{T}"/> of <see cref="long"/>, stored into an optional field of a parent object</summary>
		[Pure]
		[return: NotNullIfNotNull(nameof(defaultValue))]
		public static List<long>? UnpackInt64List(JsonValue? value, List<long>? defaultValue = null, string? fieldName = null)
			=> value switch
			{
				JsonArray arr => arr.ToInt64List(),
				null or JsonNull => defaultValue,
				_ => throw (fieldName != null ? CrystalJson.Errors.Parsing_CannotCastFieldToJsonArray(value, fieldName) : CrystalJson.Errors.Parsing_CannotCastToJsonArray(value))
			};

		/// <summary>Deserializes a <see cref="JsonArray"/> into a <see cref="List{T}"/> of <see cref="double"/>, stored into a <b>required</b> field of a parent object</summary>
		[Pure]
		public static List<double> UnpackRequiredDoubleList(JsonValue? value, JsonValue? parent = null, string? fieldName = null)
			=> value switch
			{
				JsonArray arr => arr.ToDoubleList(),
				null or JsonNull => throw (fieldName != null ? CrystalJson.Errors.Parsing_FieldIsNullOrMissing(parent, fieldName, null) : CrystalJson.Errors.Parsing_ValueIsNullOrMissing()),
				_ => throw (fieldName != null ? CrystalJson.Errors.Parsing_CannotCastFieldToJsonArray(value, fieldName) : CrystalJson.Errors.Parsing_CannotCastToJsonArray(value))
			};

		/// <summary>Deserializes a <see cref="JsonArray"/> into a <see cref="List{T}"/> of <see cref="double"/>, stored into an optional field of a parent object</summary>
		[Pure]
		[return: NotNullIfNotNull(nameof(defaultValue))]
		public static List<double>? UnpackDoubleList(JsonValue? value, List<double>? defaultValue = null, string? fieldName = null)
			=> value switch
			{
				JsonArray arr => arr.ToDoubleList(),
				null or JsonNull => defaultValue,
				_ => throw (fieldName != null ? CrystalJson.Errors.Parsing_CannotCastFieldToJsonArray(value, fieldName) : CrystalJson.Errors.Parsing_CannotCastToJsonArray(value))
			};

		/// <summary>Deserializes a <see cref="JsonArray"/> into a <see cref="List{T}"/></summary>
		[Pure]
		public static List<T> UnpackList<T>(this IJsonDeserializer<T> serializer, JsonArray array, ICrystalJsonTypeResolver? resolver = null)
		{
			var input = array.GetSpan();

#if NET8_0_OR_GREATER
			var result = new List<T>();
			// return a span with the correct size
			CollectionsMarshal.SetCount(result, input.Length);
			var span = CollectionsMarshal.AsSpan(result);

			for (int i = 0; i < input.Length; i++)
			{
				span[i] = serializer.Unpack(input[i], resolver);
			}
#else
			var result = new List<T>(array.Count);
			foreach (var item in array)
			{
				result.Add(serializer.Unpack(item, resolver));
			}
#endif

			return result;
		}

		/// <summary>Deserializes a <see cref="JsonArray"/> into an <see cref="ImmutableArray{T}"/>, stored into an optional field of a parent object</summary>
		[Pure]
		[return: NotNullIfNotNull(nameof(defaultValue))]
		public static ImmutableArray<T>? UnpackImmutableArray<T>(this IJsonDeserializer<T> serializer, JsonValue? value, ImmutableArray<T>? defaultValue = null, ICrystalJsonTypeResolver? resolver = null, string? fieldName = null)
			=> value switch
			{
				null or JsonNull => defaultValue,
				JsonArray arr => UnpackImmutableArray(serializer, arr, resolver),
				_ => throw (fieldName != null ? CrystalJson.Errors.Parsing_CannotCastFieldToJsonArray(value, fieldName) : CrystalJson.Errors.Parsing_CannotCastToJsonArray(value))
			};

		/// <summary>Deserializes a <see cref="JsonArray"/> into an <see cref="ImmutableArray{T}"/>, stored into an optional field of a parent object</summary>
		[Pure]
		public static ImmutableArray<T> UnpackImmutableArray<T>(this IJsonDeserializer<T> serializer, JsonArray array, ICrystalJsonTypeResolver? resolver = null)
		{
			// will wrap the array, without any copy
			return ImmutableCollectionsMarshal.AsImmutableArray<T>(UnpackArray(serializer, array, resolver));
		}

		/// <summary>Deserializes a <see cref="JsonArray"/> into an <see cref="ImmutableList{T}"/>, stored into an optional field of a parent object</summary>
		[Pure]
		[return: NotNullIfNotNull(nameof(defaultValue))]
		public static ImmutableList<T>? UnpackImmutableList<T>(this IJsonDeserializer<T> serializer, JsonValue? value, ImmutableList<T>? defaultValue = null, ICrystalJsonTypeResolver? resolver = null, string? fieldName = null)
			=> value switch
			{
				null or JsonNull => defaultValue,
				JsonArray arr => UnpackImmutableList(serializer, arr, resolver),
				_ => throw (fieldName != null ? CrystalJson.Errors.Parsing_CannotCastFieldToJsonArray(value, fieldName) : CrystalJson.Errors.Parsing_CannotCastToJsonArray(value))
			};

		/// <summary>Deserializes a <see cref="JsonArray"/> into an <see cref="ImmutableList{T}"/>, stored into an optional field of a parent object</summary>
		[Pure]
		public static ImmutableList<T> UnpackImmutableList<T>(this IJsonDeserializer<T> serializer, JsonArray array, ICrystalJsonTypeResolver? resolver = null)
		{
			// not sure if there is a way to fill the immutable list "in place"?
			return ImmutableList.Create<T>(UnpackArray(serializer, array, resolver));
		}

		/// <summary>Deserializes a <see cref="JsonArray"/> into an <see cref="Dictionary{TKey,TValue}"/> with string keys, stored into an optional field of a parent object</summary>
		[Pure]
		[return: NotNullIfNotNull(nameof(defaultValue))]
		public static Dictionary<string, TValue>? UnpackDictionary<TValue>(this IJsonDeserializer<TValue> serializer, JsonValue? value, Dictionary<string, TValue>? defaultValue = null, IEqualityComparer<string>? keyComparer = null, ICrystalJsonTypeResolver? resolver = null, string? fieldName = null)
			=> value switch
			{
				null or JsonNull => defaultValue,
				JsonObject obj => UnpackDictionary(serializer, obj, keyComparer, resolver),
				_ => throw (fieldName != null ? CrystalJson.Errors.Parsing_CannotCastFieldToJsonObject(value, fieldName) : CrystalJson.Errors.Parsing_CannotCastToJsonObject(value))
			};

		/// <summary>Deserializes a <see cref="JsonArray"/> into an <see cref="Dictionary{TKey,TValue}"/> with string keys, stored into an optional field of a parent object</summary>
		[Pure]
		public static Dictionary<string, TValue> UnpackDictionary<TValue>(this IJsonDeserializer<TValue> serializer, JsonObject obj, IEqualityComparer<string>? keyComparer = null, ICrystalJsonTypeResolver? resolver = null)
		{
			var res = new Dictionary<string, TValue>(obj.Count, keyComparer);

			foreach (var kv in obj)
			{
				res.Add(kv.Key, serializer.Unpack(kv.Value, resolver));
			}

			return res;
		}

		#endregion

		#region IJsonPacker<T>...

		/// <summary>Converts a span of items into the equivalent <see cref="JsonArray"/></summary>
		/// <param name="serializer">Custom serializer</param>
		/// <param name="items">Span of items to convert</param>
		/// <param name="settings">Serialization settings</param>
		/// <param name="resolver">Custom resolver used to bind the value into a managed type.</param>
		public static JsonArray PackSpan<TValue>(this IJsonPacker<TValue> serializer, ReadOnlySpan<TValue> items, CrystalJsonSettings? settings = null, ICrystalJsonTypeResolver? resolver = null)
		{
			var result = new JsonArray();
			var span = result.GetSpanAndSetCount(items.Length);

			for (int i = 0; i < items.Length; i++)
			{
				span[i] = items[i] is not null ? serializer.Pack(items[i], settings, resolver) : JsonNull.Null;
			}

			if (settings.IsReadOnly())
			{
				result = result.FreezeUnsafe();
			}

			return result;
		}

		/// <summary>Converts an array of items into the equivalent <see cref="JsonArray"/></summary>
		/// <param name="serializer">Custom serializer</param>
		/// <param name="items">Items to convert</param>
		/// <param name="settings">Serialization settings</param>
		/// <param name="resolver">Custom resolver used to bind the value into a managed type.</param>
		/// <returns>Packed JSON Array, or <c>null</c> if <paramref name="items"/> is <c>null</c></returns>
		[return: NotNullIfNotNull(nameof(items))]
		public static JsonArray? PackArray<TValue>(this IJsonPacker<TValue> serializer, TValue[]? items, CrystalJsonSettings? settings = null, ICrystalJsonTypeResolver? resolver = null)
		{
			if (items == null)
			{
				return null;
			}

			return PackSpan<TValue>(serializer, new ReadOnlySpan<TValue>(items), settings, resolver);
		}

		/// <summary>Converts a list of items into the equivalent <see cref="JsonArray"/></summary>
		/// <param name="serializer">Custom serializer</param>
		/// <param name="items">Items to convert</param>
		/// <param name="settings">Serialization settings</param>
		/// <param name="resolver">Custom resolver used to bind the value into a managed type.</param>
		/// <returns>Packed JSON Array, or <c>null</c> if <paramref name="items"/> is <c>null</c></returns>
		[return: NotNullIfNotNull(nameof(items))]
		public static JsonArray? PackList<TValue>(this IJsonPacker<TValue> serializer, List<TValue>? items, CrystalJsonSettings? settings = null, ICrystalJsonTypeResolver? resolver = null)
		{
			if (items == null)
			{
				return null;
			}

			return PackSpan<TValue>(serializer, CollectionsMarshal.AsSpan(items), settings, resolver);
		}

		/// <summary>Converts a sequence of items into the equivalent <see cref="JsonArray"/></summary>
		/// <param name="serializer">Custom serializer</param>
		/// <param name="items">Items to convert</param>
		/// <param name="settings">Serialization settings</param>
		/// <param name="resolver">Custom resolver used to bind the value into a managed type.</param>
		/// <returns>Packed JSON Array, or <c>null</c> if <paramref name="items"/> is <c>null</c></returns>
		[return: NotNullIfNotNull(nameof(items))]
		public static JsonArray? PackEnumerable<TValue>(this IJsonPacker<TValue> serializer, IEnumerable<TValue>? items, CrystalJsonSettings? settings = null, ICrystalJsonTypeResolver? resolver = null)
		{
			if (items == null)
			{
				return null;
			}

			if (items.TryGetSpan(out var span))
			{
				return PackSpan<TValue>(serializer, span, settings, resolver);
			}

			_ = items.TryGetNonEnumeratedCount(out var count);
			var result = new JsonArray(count);
			foreach (var item in items)
			{
				result.Add(item is not null ? serializer.Pack(item, settings, resolver) : JsonNull.Null);
			}

			if (settings.IsReadOnly())
			{
				result = result.FreezeUnsafe();
			}

			return result;
		}

		/// <summary>Converts a dictionary into the equivalent <see cref="JsonObject"/></summary>
		/// <param name="serializer">Custom serializer</param>
		/// <param name="items">Dictionary to convert</param>
		/// <param name="settings">Serialization settings</param>
		/// <param name="resolver">Custom resolver used to bind the value into a managed type.</param>
		/// <returns>Packed JSON Object, or <c>null</c> if <paramref name="items"/> is <c>null</c></returns>
		[return: NotNullIfNotNull(nameof(items))]
		public static JsonObject? PackObject<TValue>(this IJsonPacker<TValue> serializer, Dictionary<string, TValue>? items, CrystalJsonSettings? settings = null, ICrystalJsonTypeResolver? resolver = null)
		{
			if (items == null)
			{
				return null;
			}

			if (items.Count == 0)
			{
				return settings.IsReadOnly() ? JsonObject.ReadOnly.Empty : new();
			}

			var result = new JsonObject(items.Count);
			foreach (var kv in items)
			{
				result[kv.Key] = kv.Value is not null ? serializer.Pack(kv.Value, settings, resolver) : JsonNull.Null;
			}

			if (settings.IsReadOnly())
			{
				result = result.FreezeUnsafe();
			}

			return result;
		}

		/// <summary>Converts a dictionary into the equivalent <see cref="JsonObject"/></summary>
		/// <param name="serializer">Custom serializer</param>
		/// <param name="items">Dictionary to convert</param>
		/// <param name="settings">Serialization settings</param>
		/// <param name="resolver">Custom resolver used to bind the value into a managed type.</param>
		/// <returns>Packed JSON Object, or <c>null</c> if <paramref name="items"/> is <c>null</c></returns>
		[return: NotNullIfNotNull(nameof(items))]
		public static JsonObject? PackObject<TValue>(this IJsonPacker<TValue> serializer, IDictionary<string, TValue>? items, CrystalJsonSettings? settings = null, ICrystalJsonTypeResolver? resolver = null)
		{
			if (items == null)
			{
				return null;
			}

			if (items.Count == 0)
			{
				return settings.IsReadOnly() ? JsonObject.ReadOnly.Empty : new();
			}

			var result = new JsonObject(items.Count);
			if (items is Dictionary<string, TValue> dict)
			{
				// we can skip the IEnumerator<...> allocation for this type
				foreach (var kv in dict)
				{
					result[kv.Key] = kv.Value is not null ? serializer.Pack(kv.Value, settings, resolver) : JsonNull.Null;
				}
			}
			else
			{
				foreach (var kv in items)
				{
					result[kv.Key] = kv.Value is not null ? serializer.Pack(kv.Value, settings, resolver) : JsonNull.Null;
				}
			}

			if (settings.IsReadOnly())
			{
				result = result.FreezeUnsafe();
			}

			return result;
		}

		/// <summary>Converts a span of key/value pairs into the equivalent <see cref="JsonObject"/></summary>
		/// <param name="serializer">Custom serializer</param>
		/// <param name="items">Span to convert</param>
		/// <param name="settings">Serialization settings</param>
		/// <param name="resolver">Custom resolver used to bind the value into a managed type.</param>
		/// <returns>Packed JSON Object, or <c>null</c> if <paramref name="items"/> is <c>null</c></returns>
		public static JsonObject PackObject<TValue>(this IJsonPacker<TValue> serializer, ReadOnlySpan<KeyValuePair<string, TValue>> items, CrystalJsonSettings? settings = null, ICrystalJsonTypeResolver? resolver = null)
		{
			if (items.Length == 0)
			{
				return settings.IsReadOnly() ? JsonObject.ReadOnly.Empty : new();
			}

			var result = new JsonObject(items.Length);

			foreach (var kv in items)
			{
				result[kv.Key] = kv.Value is not null ? serializer.Pack(kv.Value, settings, resolver) : JsonNull.Null;
			}

			if (settings.IsReadOnly())
			{
				result = result.FreezeUnsafe();
			}

			return result;
		}

		/// <summary>Converts a sequence of key/value pairs into the equivalent <see cref="JsonObject"/></summary>
		/// <param name="serializer">Custom serializer</param>
		/// <param name="items">Sequence to convert</param>
		/// <param name="settings">Serialization settings</param>
		/// <param name="resolver">Custom resolver used to bind the value into a managed type.</param>
		/// <returns>Packed JSON Object, or <c>null</c> if <paramref name="items"/> is <c>null</c></returns>
		[return: NotNullIfNotNull(nameof(items))]
		public static JsonObject? PackObject<TValue>(this IJsonPacker<TValue> serializer, IEnumerable<KeyValuePair<string, TValue>>? items, CrystalJsonSettings? settings = null, ICrystalJsonTypeResolver? resolver = null)
		{
			if (items is null)
			{
				return null;
			}

			if (items.TryGetSpan(out var span))
			{
				return PackObject(serializer, span, settings, resolver);
			}

			JsonObject result;
			if (items.TryGetNonEnumeratedCount(out var count))
			{
				if (count == 0)
				{
					return settings.IsReadOnly() ? JsonObject.ReadOnly.Empty : new();
				}
				result = new(count);
			}
			else
			{
				result = new();
			}

			foreach (var kv in items)
			{
				result[kv.Key] = kv.Value is not null ? serializer.Pack(kv.Value, settings, resolver) : JsonNull.Null;
			}

			if (settings.IsReadOnly())
			{
				result = result.FreezeUnsafe();
			}

			return result;
		}

		#endregion

		#region CodeGen Helpers...

		// these methods are called by generated source code

		public static JsonArray PackSpan(ReadOnlySpan<string> items, CrystalJsonSettings? settings, ICrystalJsonTypeResolver? resolver)
		{
			if (items.Length == 0)
			{
				return (settings.IsReadOnly()) ? JsonArray.ReadOnly.Empty : new();
			}

			var arr = new JsonArray();
			var buf = arr.GetSpanAndSetCount(items.Length);

			for (int i = 0; i < items.Length; i++)
			{
				buf[i] = JsonString.Return(items[i]);
			}

			if (settings.IsReadOnly())
			{
				arr = arr.FreezeUnsafe();
			}
			return arr;
		}

		/// <summary>Converts a span of <see cref="bool"/> into the equivalent <see cref="JsonArray"/></summary>
		/// <param name="items">Span of items to convert</param>
		/// <param name="settings">Serialization settings</param>
		/// <param name="resolver">Custom resolver used to bind the value into a managed type.</param>
		public static JsonArray PackSpan(ReadOnlySpan<bool> items, CrystalJsonSettings? settings, ICrystalJsonTypeResolver? resolver)
		{
			if (items.Length == 0)
			{
				return (settings.IsReadOnly()) ? JsonArray.ReadOnly.Empty : new();
			}

			var arr = new JsonArray();
			var buf = arr.GetSpanAndSetCount(items.Length);

			for (int i = 0; i < items.Length; i++)
			{
				buf[i] = items[i] ? JsonBoolean.True : JsonBoolean.False;
			}

			if (settings.IsReadOnly())
			{
				arr = arr.FreezeUnsafe();
			}
			return arr;
		}

		/// <summary>Converts a span of <see cref="int"/> into the equivalent <see cref="JsonArray"/></summary>
		/// <param name="items">Span of items to convert</param>
		/// <param name="settings">Serialization settings</param>
		/// <param name="resolver">Custom resolver used to bind the value into a managed type.</param>
		public static JsonArray PackSpan(ReadOnlySpan<int> items, CrystalJsonSettings? settings, ICrystalJsonTypeResolver? resolver)
		{
			if (items.Length == 0)
			{
				return (settings.IsReadOnly()) ? JsonArray.ReadOnly.Empty : new();
			}

			var arr = new JsonArray();
			var buf = arr.GetSpanAndSetCount(items.Length);

			for (int i = 0; i < items.Length; i++)
			{
				buf[i] = JsonNumber.Return(items[i]);
			}

			if (settings.IsReadOnly())
			{
				arr = arr.FreezeUnsafe();
			}
			return arr;
		}

		/// <summary>Converts a span of <see cref="long"/> into the equivalent <see cref="JsonArray"/></summary>
		/// <param name="items">Span of items to convert</param>
		/// <param name="settings">Serialization settings</param>
		/// <param name="resolver">Custom resolver used to bind the value into a managed type.</param>
		public static JsonArray PackSpan(ReadOnlySpan<long> items, CrystalJsonSettings? settings, ICrystalJsonTypeResolver? resolver)
		{
			if (items.Length == 0)
			{
				return (settings.IsReadOnly()) ? JsonArray.ReadOnly.Empty : new();
			}

			var arr = new JsonArray();
			var buf = arr.GetSpanAndSetCount(items.Length);

			for (int i = 0; i < items.Length; i++)
			{
				buf[i] = JsonNumber.Return(items[i]);
			}

			if (settings.IsReadOnly())
			{
				arr = arr.FreezeUnsafe();
			}
			return arr;
		}

		/// <summary>Converts a span of <see cref="float"/> into the equivalent <see cref="JsonArray"/></summary>
		/// <param name="items">Span of items to convert</param>
		/// <param name="settings">Serialization settings</param>
		/// <param name="resolver">Custom resolver used to bind the value into a managed type.</param>
		public static JsonArray PackSpan(ReadOnlySpan<float> items, CrystalJsonSettings? settings, ICrystalJsonTypeResolver? resolver)
		{
			if (items.Length == 0)
			{
				return (settings.IsReadOnly()) ? JsonArray.ReadOnly.Empty : new();
			}

			var arr = new JsonArray();
			var buf = arr.GetSpanAndSetCount(items.Length);

			for (int i = 0; i < items.Length; i++)
			{
				buf[i] = JsonNumber.Return(items[i]);
			}

			if (settings.IsReadOnly())
			{
				arr = arr.FreezeUnsafe();
			}
			return arr;
		}

		/// <summary>Converts a span of <see cref="double"/> into the equivalent <see cref="JsonArray"/></summary>
		/// <param name="items">Span of items to convert</param>
		/// <param name="settings">Serialization settings</param>
		/// <param name="resolver">Custom resolver used to bind the value into a managed type.</param>
		public static JsonArray PackSpan(ReadOnlySpan<double> items, CrystalJsonSettings? settings, ICrystalJsonTypeResolver? resolver)
		{
			if (items.Length == 0)
			{
				return (settings.IsReadOnly()) ? JsonArray.ReadOnly.Empty : new();
			}

			var arr = new JsonArray();
			var buf = arr.GetSpanAndSetCount(items.Length);

			for (int i = 0; i < items.Length; i++)
			{
				buf[i] = JsonNumber.Return(items[i]);
			}

			if (settings.IsReadOnly())
			{
				arr = arr.FreezeUnsafe();
			}
			return arr;
		}

		/// <summary>Converts a span of <see cref="Guid"/> into the equivalent <see cref="JsonArray"/></summary>
		/// <param name="items">Span of items to convert</param>
		/// <param name="settings">Serialization settings</param>
		/// <param name="resolver">Custom resolver used to bind the value into a managed type.</param>
		public static JsonArray PackSpan(ReadOnlySpan<Guid> items, CrystalJsonSettings? settings, ICrystalJsonTypeResolver? resolver)
		{
			if (items.Length == 0)
			{
				return (settings.IsReadOnly()) ? JsonArray.ReadOnly.Empty : new();
			}

			var arr = new JsonArray();
			var buf = arr.GetSpanAndSetCount(items.Length);

			for (int i = 0; i < items.Length; i++)
			{
				buf[i] = JsonString.Return(items[i]);
			}

			if (settings.IsReadOnly())
			{
				arr = arr.FreezeUnsafe();
			}
			return arr;
		}

		/// <summary>Converts a span of <see cref="Uuid128"/> into the equivalent <see cref="JsonArray"/></summary>
		/// <param name="items">Span of items to convert</param>
		/// <param name="settings">Serialization settings</param>
		/// <param name="resolver">Custom resolver used to bind the value into a managed type.</param>
		public static JsonArray PackSpan(ReadOnlySpan<Uuid128> items, CrystalJsonSettings? settings, ICrystalJsonTypeResolver? resolver)
		{
			if (items.Length == 0)
			{
				return (settings.IsReadOnly()) ? JsonArray.ReadOnly.Empty : new();
			}

			var arr = new JsonArray();
			var buf = arr.GetSpanAndSetCount(items.Length);

			for (int i = 0; i < items.Length; i++)
			{
				buf[i] = JsonString.Return(items[i]);
			}

			if (settings.IsReadOnly())
			{
				arr = arr.FreezeUnsafe();
			}
			return arr;
		}

		/// <summary>Converts a span of <see cref="Uuid64"/> into the equivalent <see cref="JsonArray"/></summary>
		/// <param name="items">Span of items to convert</param>
		/// <param name="settings">Serialization settings</param>
		/// <param name="resolver">Custom resolver used to bind the value into a managed type.</param>
		public static JsonArray PackSpan(ReadOnlySpan<Uuid64> items, CrystalJsonSettings? settings, ICrystalJsonTypeResolver? resolver)
		{
			if (items.Length == 0)
			{
				return (settings.IsReadOnly()) ? JsonArray.ReadOnly.Empty : new();
			}

			var arr = new JsonArray();
			var buf = arr.GetSpanAndSetCount(items.Length);

			for (int i = 0; i < items.Length; i++)
			{
				buf[i] = JsonString.Return(items[i]);
			}

			if (settings.IsReadOnly())
			{
				arr = arr.FreezeUnsafe();
			}
			return arr;
		}

		/// <summary>Converts a sequence of <see cref="bool"/> into the equivalent <see cref="JsonArray"/></summary>
		/// <param name="items">Sequence to convert</param>
		/// <param name="settings">Serialization settings</param>
		/// <param name="resolver">Custom resolver used to bind the value into a managed type.</param>
		[return: NotNullIfNotNull(nameof(items))]
		public static JsonArray? PackEnumerable(IEnumerable<bool>? items, CrystalJsonSettings? settings, ICrystalJsonTypeResolver? resolver)
		{
			if (items == null) return null;

			if (items.TryGetSpan(out var span))
			{
				return PackSpan(span, settings, resolver);
			}

			JsonArray arr = items.TryGetNonEnumeratedCount(out var count) ? new(count) : new();

			foreach (var item in items)
			{
				arr.Add(item ? JsonBoolean.True : JsonBoolean.False);
			}

			if (settings.IsReadOnly())
			{
				arr = arr.FreezeUnsafe();
			}
			return arr;
		}

		/// <summary>Converts a sequence of <see cref="int"/> into the equivalent <see cref="JsonArray"/></summary>
		/// <param name="items">Sequence to convert</param>
		/// <param name="settings">Serialization settings</param>
		/// <param name="resolver">Custom resolver used to bind the value into a managed type.</param>
		[return: NotNullIfNotNull(nameof(items))]
		public static JsonArray? PackEnumerable(IEnumerable<int>? items, CrystalJsonSettings? settings, ICrystalJsonTypeResolver? resolver)
		{
			if (items == null) return null;

			if (items.TryGetSpan(out var span))
			{
				return PackSpan(span, settings, resolver);
			}

			JsonArray arr = items.TryGetNonEnumeratedCount(out var count) ? new(count) : new();

			foreach (var item in items)
			{
				arr.Add(JsonNumber.Return(item));
			}

			if (settings.IsReadOnly())
			{
				arr = arr.FreezeUnsafe();
			}
			return arr;
		}

		/// <summary>Converts a sequence of <see cref="long"/> into the equivalent <see cref="JsonArray"/></summary>
		/// <param name="items">Sequence to convert</param>
		/// <param name="settings">Serialization settings</param>
		/// <param name="resolver">Custom resolver used to bind the value into a managed type.</param>
		[return: NotNullIfNotNull(nameof(items))]
		public static JsonArray? PackEnumerable(IEnumerable<long>? items, CrystalJsonSettings? settings, ICrystalJsonTypeResolver? resolver)
		{
			if (items == null) return null;

			if (items.TryGetSpan(out var span))
			{
				return PackSpan(span, settings, resolver);
			}

			JsonArray arr = items.TryGetNonEnumeratedCount(out var count) ? new(count) : new();

			foreach (var item in items)
			{
				arr.Add(JsonNumber.Return(item));
			}

			if (settings.IsReadOnly())
			{
				arr = arr.FreezeUnsafe();
			}
			return arr;
		}

		/// <summary>Converts a sequence of <see cref="float"/> into the equivalent <see cref="JsonArray"/></summary>
		/// <param name="items">Sequence to convert</param>
		/// <param name="settings">Serialization settings</param>
		/// <param name="resolver">Custom resolver used to bind the value into a managed type.</param>
		[return: NotNullIfNotNull(nameof(items))]
		public static JsonArray? PackEnumerable(IEnumerable<float>? items, CrystalJsonSettings? settings, ICrystalJsonTypeResolver? resolver)
		{
			if (items == null) return null;

			if (items.TryGetSpan(out var span))
			{
				return PackSpan(span, settings, resolver);
			}

			JsonArray arr = items.TryGetNonEnumeratedCount(out var count) ? new(count) : new();

			foreach (var item in items)
			{
				arr.Add(JsonNumber.Return(item));
			}

			if (settings.IsReadOnly())
			{
				arr = arr.FreezeUnsafe();
			}
			return arr;
		}

		/// <summary>Converts a sequence of <see cref="double"/> into the equivalent <see cref="JsonArray"/></summary>
		/// <param name="items">Sequence to convert</param>
		/// <param name="settings">Serialization settings</param>
		/// <param name="resolver">Custom resolver used to bind the value into a managed type.</param>
		[return: NotNullIfNotNull(nameof(items))]
		public static JsonArray? PackEnumerable(IEnumerable<double>? items, CrystalJsonSettings? settings, ICrystalJsonTypeResolver? resolver)
		{
			if (items == null) return null;

			if (items.TryGetSpan(out var span))
			{
				return PackSpan(span, settings, resolver);
			}

			JsonArray arr = items.TryGetNonEnumeratedCount(out var count) ? new(count) : new();

			foreach (var item in items)
			{
				arr.Add(JsonNumber.Return(item));
			}

			if (settings.IsReadOnly())
			{
				arr = arr.FreezeUnsafe();
			}
			return arr;
		}

		[return: NotNullIfNotNull(nameof(items))]
		public static JsonArray? PackArray(bool[]? items, CrystalJsonSettings? settings, ICrystalJsonTypeResolver? resolver)
		{
			if (items == null) return null;
			return PackSpan(new ReadOnlySpan<bool>(items), settings, resolver);
		}

		[return: NotNullIfNotNull(nameof(items))]
		public static JsonArray? PackArray(int[]? items, CrystalJsonSettings? settings, ICrystalJsonTypeResolver? resolver)
		{
			if (items == null) return null;
			return PackSpan(new ReadOnlySpan<int>(items), settings, resolver);
		}

		[return: NotNullIfNotNull(nameof(items))]
		public static JsonArray? PackArray(long[]? items, CrystalJsonSettings? settings, ICrystalJsonTypeResolver? resolver)
		{
			if (items == null) return null;
			return PackSpan(new ReadOnlySpan<long>(items), settings, resolver);
		}

		[return: NotNullIfNotNull(nameof(items))]
		public static JsonArray? PackArray(float[]? items, CrystalJsonSettings? settings, ICrystalJsonTypeResolver? resolver)
		{
			if (items == null) return null;
			return PackSpan(new ReadOnlySpan<float>(items), settings, resolver);
		}

		[return: NotNullIfNotNull(nameof(items))]
		public static JsonArray? PackArray(double[]? items, CrystalJsonSettings? settings, ICrystalJsonTypeResolver? resolver)
		{
			if (items == null) return null;
			return PackSpan(new ReadOnlySpan<double>(items), settings, resolver);
		}

		[return: NotNullIfNotNull(nameof(items))]
		public static JsonArray? PackArray(string[]? items, CrystalJsonSettings? settings, ICrystalJsonTypeResolver? resolver)
		{
			if (items == null) return null;
			return PackSpan(new ReadOnlySpan<string>(items), settings, resolver);
		}

		[return: NotNullIfNotNull(nameof(items))]
		public static JsonArray? PackArray(Guid[]? items, CrystalJsonSettings? settings, ICrystalJsonTypeResolver? resolver)
		{
			if (items == null) return null;
			return PackSpan(new ReadOnlySpan<Guid>(items), settings, resolver);
		}

		[return: NotNullIfNotNull(nameof(items))]
		public static JsonArray? PackArray(Uuid128[]? items, CrystalJsonSettings? settings, ICrystalJsonTypeResolver? resolver)
		{
			if (items == null) return null;
			return PackSpan(new ReadOnlySpan<Uuid128>(items), settings, resolver);
		}

		[return: NotNullIfNotNull(nameof(items))]
		public static JsonArray? PackArray(Uuid64[]? items, CrystalJsonSettings? settings, ICrystalJsonTypeResolver? resolver)
		{
			if (items == null) return null;
			return PackSpan(new ReadOnlySpan<Uuid64>(items), settings, resolver);
		}

		[return: NotNullIfNotNull(nameof(items))]
		public static JsonArray? PackList(List<bool>? items, CrystalJsonSettings? settings, ICrystalJsonTypeResolver? resolver)
		{
			if (items == null) return null;
			return PackSpan(CollectionsMarshal.AsSpan(items), settings, resolver);
		}

		[return: NotNullIfNotNull(nameof(items))]
		public static JsonArray? PackList(List<int>? items, CrystalJsonSettings? settings, ICrystalJsonTypeResolver? resolver)
		{
			if (items == null) return null;
			return PackSpan(CollectionsMarshal.AsSpan(items), settings, resolver);
		}

		[return: NotNullIfNotNull(nameof(items))]
		public static JsonArray? PackList(List<long>? items, CrystalJsonSettings? settings, ICrystalJsonTypeResolver? resolver)
		{
			if (items == null) return null;
			return PackSpan(CollectionsMarshal.AsSpan(items), settings, resolver);
		}

		[return: NotNullIfNotNull(nameof(items))]
		public static JsonArray? PackList(List<float>? items, CrystalJsonSettings? settings, ICrystalJsonTypeResolver? resolver)
		{
			if (items == null) return null;
			return PackSpan(CollectionsMarshal.AsSpan(items), settings, resolver);
		}

		[return: NotNullIfNotNull(nameof(items))]
		public static JsonArray? PackList(List<double>? items, CrystalJsonSettings? settings, ICrystalJsonTypeResolver? resolver)
		{
			if (items == null) return null;
			return PackSpan(CollectionsMarshal.AsSpan(items), settings, resolver);
		}

		[return: NotNullIfNotNull(nameof(items))]
		public static JsonArray? PackList(List<string>? items, CrystalJsonSettings? settings, ICrystalJsonTypeResolver? resolver)
		{
			if (items == null) return null;
			return PackSpan(CollectionsMarshal.AsSpan(items), settings, resolver);
		}

		[return: NotNullIfNotNull(nameof(items))]
		public static JsonArray? PackList(List<Guid>? items, CrystalJsonSettings? settings, ICrystalJsonTypeResolver? resolver)
		{
			if (items == null) return null;
			return PackSpan(CollectionsMarshal.AsSpan(items), settings, resolver);
		}

		[return: NotNullIfNotNull(nameof(items))]
		public static JsonArray? PackList(List<Uuid64>? items, CrystalJsonSettings? settings, ICrystalJsonTypeResolver? resolver)
		{
			if (items == null) return null;
			return PackSpan(CollectionsMarshal.AsSpan(items), settings, resolver);
		}

		/// <summary>Converts a sequence of <see cref="string"/> into the equivalent <see cref="JsonArray"/></summary>
		/// <param name="items">Sequence to convert</param>
		/// <param name="settings">Serialization settings</param>
		/// <param name="resolver">Custom resolver used to bind the value into a managed type.</param>
		[return: NotNullIfNotNull(nameof(items))]
		public static JsonArray? PackEnumerable(IEnumerable<string>? items, CrystalJsonSettings? settings, ICrystalJsonTypeResolver? resolver)
		{
			if (items == null) return null;

			if (items.TryGetSpan(out var span))
			{
				return PackSpan(span, settings, resolver);
			}

			JsonArray arr = items.TryGetNonEnumeratedCount(out var count) ? new(count) : new();

			foreach (var item in items)
			{
				arr.Add(JsonString.Return(item));
			}

			if (settings.IsReadOnly())
			{
				arr = arr.FreezeUnsafe();
			}
			return arr;
		}

		/// <summary>Converts a sequence of <see cref="Guid"/> into the equivalent <see cref="JsonArray"/></summary>
		/// <param name="items">Sequence to convert</param>
		/// <param name="settings">Serialization settings</param>
		/// <param name="resolver">Custom resolver used to bind the value into a managed type.</param>
		[return: NotNullIfNotNull(nameof(items))]
		public static JsonArray? PackEnumerable(IEnumerable<Guid>? items, CrystalJsonSettings? settings, ICrystalJsonTypeResolver? resolver)
		{
			if (items == null) return null;

			if (items.TryGetSpan(out var span))
			{
				return PackSpan(span, settings, resolver);
			}

			JsonArray arr = items.TryGetNonEnumeratedCount(out var count) ? new(count) : new();

			foreach (var item in items)
			{
				arr.Add(JsonString.Return(item));
			}

			if (settings.IsReadOnly())
			{
				arr = arr.FreezeUnsafe();
			}
			return arr;
		}

		/// <summary>Converts a sequence of <see cref="Uuid128"/> into the equivalent <see cref="JsonArray"/></summary>
		/// <param name="items">Sequence to convert</param>
		/// <param name="settings">Serialization settings</param>
		/// <param name="resolver">Custom resolver used to bind the value into a managed type.</param>
		[return: NotNullIfNotNull(nameof(items))]
		public static JsonArray? PackEnumerable(IEnumerable<Uuid128>? items, CrystalJsonSettings? settings, ICrystalJsonTypeResolver? resolver)
		{
			if (items == null) return null;

			if (items.TryGetSpan(out var span))
			{
				return PackSpan(span, settings, resolver);
			}

			JsonArray arr = items.TryGetNonEnumeratedCount(out var count) ? new(count) : new();

			foreach (var item in items)
			{
				arr.Add(JsonString.Return(item));
			}

			if (settings.IsReadOnly())
			{
				arr = arr.FreezeUnsafe();
			}
			return arr;
		}

		public static JsonArray PackSpan<TValue>(ReadOnlySpan<TValue> items, CrystalJsonSettings? settings, ICrystalJsonTypeResolver? resolver)
		{
			return JsonArray.FromValues<TValue>(items, settings, resolver);
		}

		/// <summary>Pack a span of items that implements <see cref="IJsonPackable"/></summary>
		public static JsonArray PackSpanPackable<TPackable>(ReadOnlySpan<TPackable> items, CrystalJsonSettings? settings, ICrystalJsonTypeResolver? resolver)
			where TPackable : IJsonPackable
		{
			settings ??= CrystalJsonSettings.Json;
			resolver ??= CrystalJson.DefaultResolver;
			if (items.Length == 0) return settings.ReadOnly ? JsonArray.ReadOnly.Empty : [ ];

			var buffer = new JsonValue[items.Length];
			for (int i = 0; i < items.Length; i++)
			{
				buffer[i] = items[i]?.JsonPack(settings, resolver) ?? JsonNull.Null;
			}
			return new(buffer, items.Length, settings.ReadOnly);
		}

		/// <summary>Converts an array of <typeparamref name="TValue"/> into the equivalent <see cref="JsonArray"/></summary>
		/// <param name="items">Array to convert</param>
		/// <param name="settings">Serialization settings</param>
		/// <param name="resolver">Custom resolver used to bind the value into a managed type.</param>
		[return: NotNullIfNotNull(nameof(items))]
		public static JsonArray? PackArray<TValue>(TValue[]? items, CrystalJsonSettings? settings, ICrystalJsonTypeResolver? resolver)
		{
			return items is not null ? JsonArray.FromValues<TValue>(new ReadOnlySpan<TValue>(items), settings, resolver) : null;
		}

		/// <summary>Converts an array of items that implements <see cref="IJsonPackable"/> into the equivalent <see cref="JsonArray"/></summary>
		/// <param name="items">Array to convert</param>
		/// <param name="settings">Serialization settings</param>
		/// <param name="resolver">Custom resolver used to bind the value into a managed type.</param>
		[return: NotNullIfNotNull(nameof(items))]
		public static JsonArray? PackArrayPackable<TPackable>(TPackable[]? items, CrystalJsonSettings? settings, ICrystalJsonTypeResolver? resolver)
			where TPackable : IJsonPackable
		{
			return items is not null ? PackSpanPackable<TPackable>(new(items), settings, resolver) : null;
		}

		/// <summary>Converts a list of <typeparamref name="TValue"/> into the equivalent <see cref="JsonArray"/></summary>
		/// <param name="items">List to convert</param>
		/// <param name="settings">Serialization settings</param>
		/// <param name="resolver">Custom resolver used to bind the value into a managed type.</param>
		[return: NotNullIfNotNull(nameof(items))]
		public static JsonArray? PackList<TValue>(List<TValue>? items, CrystalJsonSettings? settings, ICrystalJsonTypeResolver? resolver)
		{
			return items is not null ? JsonArray.FromValues<TValue>(CollectionsMarshal.AsSpan(items), settings, resolver) : null;
		}

		/// <summary>Converts a list of items that implements <see cref="IJsonPackable"/> into the equivalent <see cref="JsonArray"/></summary>
		/// <param name="items">List to convert</param>
		/// <param name="settings">Serialization settings</param>
		/// <param name="resolver">Custom resolver used to bind the value into a managed type.</param>
		[return: NotNullIfNotNull(nameof(items))]
		public static JsonArray? PackListPackable<TPackable>(List<TPackable>? items, CrystalJsonSettings? settings, ICrystalJsonTypeResolver? resolver)
			where TPackable : IJsonPackable
		{
			return items is not null ? PackSpanPackable<TPackable>(CollectionsMarshal.AsSpan(items), settings, resolver) : null;
		}

		/// <summary>Converts a sequence of <typeparamref name="TValue"/> into the equivalent <see cref="JsonArray"/></summary>
		/// <param name="items">Sequence to convert</param>
		/// <param name="settings">Serialization settings</param>
		/// <param name="resolver">Custom resolver used to bind the value into a managed type.</param>
		[return: NotNullIfNotNull(nameof(items))]
		public static JsonArray? PackEnumerable<TValue>(IEnumerable<TValue?>? items, CrystalJsonSettings? settings, ICrystalJsonTypeResolver? resolver)
		{
			if (items == null) return null;
			return JsonArray.FromValues(items, settings, resolver);
		}

		/// <summary>Converts a sequence of items that implements <see cref="IJsonPackable"/> into the equivalent <see cref="JsonArray"/></summary>
		/// <param name="items">Sequence to convert</param>
		/// <param name="settings">Serialization settings</param>
		/// <param name="resolver">Custom resolver used to bind the value into a managed type.</param>
		[return: NotNullIfNotNull(nameof(items))]
		public static JsonArray? PackEnumerablePackable<TPackable>(IEnumerable<TPackable?>? items, CrystalJsonSettings? settings, ICrystalJsonTypeResolver? resolver)
			where TPackable : IJsonPackable
		{
			if (items == null) return null;
			if (items.TryGetSpan(out var span))
			{
				return PackSpanPackable(span, settings, resolver);
			}

			return PackEnumerableSlow(items, settings, resolver);

			static JsonArray PackEnumerableSlow(IEnumerable<TPackable?> items, CrystalJsonSettings? settings, ICrystalJsonTypeResolver? resolver)
			{
				settings ??= CrystalJsonSettings.Json;
				resolver ??= CrystalJson.DefaultResolver;
				var arr = new JsonArray(items.TryGetNonEnumeratedCount(out var count) ? count : 0);
				foreach (var item in items)
				{
					arr.Add(item?.JsonPack(settings, resolver) ?? JsonNull.Null);
				}
				return settings.ReadOnly ? arr.FreezeUnsafe() : arr;
			}
		}

		/// <summary>Converts a dictionary with string keys into the equivalent <see cref="JsonObject"/></summary>
		/// <param name="items">Dictionary to convert</param>
		/// <param name="settings">Serialization settings</param>
		/// <param name="resolver">Custom resolver used to bind the value into a managed type.</param>
		[return: NotNullIfNotNull(nameof(items))]
		public static JsonObject? PackDictionary<TValue>(Dictionary<string, TValue>? items, CrystalJsonSettings? settings, ICrystalJsonTypeResolver? resolver)
		{
			if (items == null) return null;
			return JsonObject.FromValues<TValue>(items, settings, resolver);
		}

		/// <summary>Converts a dictionary with string keys into the equivalent <see cref="JsonObject"/></summary>
		/// <param name="items">Dictionary to convert</param>
		/// <param name="settings">Serialization settings</param>
		/// <param name="resolver">Custom resolver used to bind the value into a managed type.</param>
		[return: NotNullIfNotNull(nameof(items))]
		public static JsonObject? PackDictionary<TValue>(IDictionary<string, TValue>? items, CrystalJsonSettings? settings, ICrystalJsonTypeResolver? resolver)
		{
			if (items == null) return null;
			return JsonObject.FromValues<TValue>(items, settings, resolver);
		}

		/// <summary>Converts a dictionary with string keys into the equivalent <see cref="JsonObject"/></summary>
		/// <param name="items">Dictionary to convert</param>
		/// <param name="settings">Serialization settings</param>
		/// <param name="resolver">Custom resolver used to bind the value into a managed type.</param>
		[return: NotNullIfNotNull(nameof(items))]
		public static JsonObject? PackDictionary<TValue>(IEnumerable<KeyValuePair<string, TValue>>? items, CrystalJsonSettings? settings, ICrystalJsonTypeResolver? resolver)
		{
			if (items == null) return null;
			return JsonObject.FromValues<TValue>(items, settings, resolver);
		}

		/// <summary>Deserializes a JSON value into an instance of <typeparamref name="T"/>, that is known to implement <see cref="IJsonDeserializable{T}"/></summary>
		/// <typeparam name="T">Type that implements <see cref="IJsonDeserializable{T}"/></typeparam>
		/// <param name="value">JSON value to deserialize</param>
		/// <param name="resolver">Custom resolver used to bind the value into a managed type.</param>
		/// <returns>Deserialized instance, or <c>null</c> if <paramref name="value"/> is null or missing</returns>
		/// <exception cref="JsonBindingException"> if <paramref name="value"/> could not be bound to the type <typeparamref name="T"/>.</exception>
		public static T? UnpackJsonDeserializable<T>(JsonValue? value, ICrystalJsonTypeResolver? resolver)
			where T : IJsonDeserializable<T>
		{
			if (value is null or JsonNull)
			{
				return default(T);
			}
			return T.JsonDeserialize(value, resolver);
		}

		/// <summary>Deserializes a JSON value into an instance of <typeparamref name="T"/>, that is known to implement <see cref="IJsonDeserializable{T}"/></summary>
		/// <typeparam name="T">Type that implements <see cref="IJsonDeserializable{T}"/></typeparam>
		/// <param name="value">JSON value to deserialize</param>
		/// <param name="missingValue">Fallback value returned when <paramref name="value"/> is null or missing.</param>
		/// <param name="resolver">Custom resolver used to bind the value into a managed type.</param>
		/// <returns>Deserialized instance, or <paramref name="missingValue"/> if <paramref name="value"/> is null or missing</returns>
		/// <exception cref="JsonBindingException"> if <paramref name="value"/> could not be bound to the type <typeparamref name="T"/>.</exception>
		[return: NotNullIfNotNull(nameof(missingValue))]
		public static T? UnpackJsonDeserializable<T>(JsonValue? value, T? missingValue, ICrystalJsonTypeResolver? resolver)
			where T : IJsonDeserializable<T>
		{
			if (value is null or JsonNull)
			{
				return missingValue;
			}
			return T.JsonDeserialize(value, resolver);
		}

		/// <summary>Deserializes a JSON value into an instance of <typeparamref name="T"/></summary>
		/// <typeparam name="T">Type of the target instance</typeparam>
		/// <param name="value">JSON value to deserialize</param>
		/// <param name="missingValue">Fallback value returned when <paramref name="value"/> is null or missing.</param>
		/// <param name="resolver">Custom resolver used to bind the value into a managed type.</param>
		/// <returns>Deserialized instance, or <paramref name="missingValue"/> if <paramref name="value"/> is null or missing</returns>
		/// <exception cref="JsonBindingException"> if <paramref name="value"/> could not be bound to the type <typeparamref name="T"/>.</exception>
		[return: NotNullIfNotNull(nameof(missingValue))]
		public static T? Unpack<T>(JsonValue? value, T? missingValue, ICrystalJsonTypeResolver? resolver)
		{
			return value.As<T>(missingValue, resolver);
		}

		/// <summary>Deserializes a JSON value into a <see cref="Nullable{T}"/> value.</summary>
		/// <typeparam name="T">Type of the target value</typeparam>
		/// <param name="value">JSON value to deserialize</param>
		/// <param name="resolver">Custom resolver used to bind the value into a managed type.</param>
		/// <returns>Deserialized instance, or <c>null</c> if <paramref name="value"/> is null or missing</returns>
		/// <exception cref="JsonBindingException"> if <paramref name="value"/> could not be bound to the type <typeparamref name="T"/>.</exception>
		public static T? UnpackNullableJsonDeserializable<T>(JsonValue? value, ICrystalJsonTypeResolver? resolver)
			where T : struct, IJsonDeserializable<T>
		{
			if (value is null or JsonNull)
			{
				return default(T);
			}
			return T.JsonDeserialize(value, resolver);
		}

		/// <summary>Deserializes a JSON value into a <see cref="Nullable{T}"/> value.</summary>
		/// <typeparam name="T">Type of the target value</typeparam>
		/// <param name="value">JSON value to deserialize</param>
		/// <param name="missingValue">Fallback value returned when <paramref name="value"/> is null or missing.</param>
		/// <param name="resolver">Custom resolver used to bind the value into a managed type.</param>
		/// <returns>Deserialized instance, or <paramref name="missingValue"/> if <paramref name="value"/> is null or missing</returns>
		/// <exception cref="JsonBindingException"> if <paramref name="value"/> could not be bound to the type <typeparamref name="T"/>.</exception>
		public static T? UnpackNullableJsonDeserializable<T>(JsonValue? value, T? missingValue, ICrystalJsonTypeResolver? resolver)
			where T : struct, IJsonDeserializable<T>
		{
			if (value is null or JsonNull)
			{
				return missingValue;
			}
			return T.JsonDeserialize(value, resolver);
		}

		/// <summary>Deserializes a required JSON value into an instance of <typeparamref name="T"/>, that is known to implement <see cref="IJsonDeserializable{T}"/></summary>
		/// <typeparam name="T">Type of the target instance</typeparam>
		/// <param name="value">JSON value to deserialize</param>
		/// <param name="resolver">Custom resolver used to bind the value into a managed type.</param>
		/// <param name="parent">Parent JSON object that holds this field (used when throwing exceptions).</param>
		/// <param name="fieldName">Name of the field in the parent JSON object that holds this value (used when throwing exceptions).</param>
		/// <returns>Deserialized instance</returns>
		/// <exception cref="JsonBindingException"> if <paramref name="value"/> is null or missing, or it could not be bound to the type <typeparamref name="T"/>.</exception>
		public static T UnpackRequiredJsonDeserializable<T>(JsonValue? value, ICrystalJsonTypeResolver? resolver, JsonValue? parent = null, string? fieldName = null)
			where T : IJsonDeserializable<T>
		{
			if (value is null or JsonNull)
			{
				throw (fieldName != null ? CrystalJson.Errors.Parsing_FieldIsNullOrMissing(parent, fieldName, null) : CrystalJson.Errors.Parsing_ValueIsNullOrMissing());
			}
			return T.JsonDeserialize(value, resolver);
		}

		/// <summary>Deserializes a required JSON value into an instance of <typeparamref name="T"/></summary>
		/// <typeparam name="T">Type that implements <see cref="IJsonDeserializable{T}"/></typeparam>
		/// <param name="converter">Deserializer instance to use</param>
		/// <param name="value">JSON value to deserialize</param>
		/// <param name="resolver">Custom resolver used to bind the value into a managed type.</param>
		/// <param name="parent">Parent JSON object that holds this field (used when throwing exceptions).</param>
		/// <param name="fieldName">Name of the field in the parent JSON object that holds this value (used when throwing exceptions).</param>
		/// <returns>Deserialized instance</returns>
		/// <exception cref="JsonBindingException"> if <paramref name="value"/> is null or missing, or it could not be bound to the type <typeparamref name="T"/>.</exception>
		public static T UnpackRequired<T>(this IJsonDeserializer<T> converter, JsonValue? value, ICrystalJsonTypeResolver? resolver, JsonValue? parent = null, string? fieldName = null)
			where T : notnull
		{
			if (value is null or JsonNull)
			{
				throw (fieldName != null ? CrystalJson.Errors.Parsing_FieldIsNullOrMissing(parent, fieldName, null) : CrystalJson.Errors.Parsing_ValueIsNullOrMissing());
			}
			return converter.Unpack(value, resolver);
		}

		/// <summary>Deserializes a required JSON value into an instance of <typeparamref name="T"/></summary>
		/// <typeparam name="T">Type that implements <see cref="IJsonDeserializable{T}"/></typeparam>
		/// <param name="value">JSON value to deserialize</param>
		/// <param name="resolver">Custom resolver used to bind the value into a managed type.</param>
		/// <param name="parent">Parent JSON object that holds this field (used when throwing exceptions).</param>
		/// <param name="fieldName">Name of the field in the parent JSON object that holds this value (used when throwing exceptions).</param>
		/// <returns>Deserialized instance</returns>
		/// <exception cref="JsonBindingException"> if <paramref name="value"/> is null or missing, or it could not be bound to the type <typeparamref name="T"/>.</exception>
		public static T UnpackRequired<T>(JsonValue? value, ICrystalJsonTypeResolver? resolver, JsonValue? parent = null, string? fieldName = null)
			where T : notnull
		{
			if (value is null or JsonNull)
			{
				throw (fieldName != null ? CrystalJson.Errors.Parsing_FieldIsNullOrMissing(parent, fieldName, null) : CrystalJson.Errors.Parsing_ValueIsNullOrMissing());
			}
			return value.Required<T>(resolver);
		}

		#endregion

	}

}
