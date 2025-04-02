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

namespace Doxense.Serialization.Json
{
	using System;
	using System.Buffers;
	using System.Collections.Immutable;
	using System.Runtime.InteropServices;
	using Doxense.Linq;

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

		public static T Deserialize<T>(this IJsonDeserializer<T> serializer, string jsonText, CrystalJsonSettings? settings = null, ICrystalJsonTypeResolver? resolver = null)
			where T : notnull
		{
			return serializer.Unpack(CrystalJson.Parse(jsonText, settings), resolver);
		}

		public static T Deserialize<T>(this IJsonDeserializer<T> serializer, ReadOnlySpan<char> jsonText, CrystalJsonSettings? settings = null, ICrystalJsonTypeResolver? resolver = null)
			where T : notnull
		{
			return serializer.Unpack(CrystalJson.Parse(jsonText, settings), resolver);
		}

		public static T Deserialize<T>(this IJsonDeserializer<T> serializer, Slice jsonBytes, CrystalJsonSettings? settings = null, ICrystalJsonTypeResolver? resolver = null)
			where T : notnull
		{
			return serializer.Unpack(CrystalJson.Parse(jsonBytes, settings), resolver);
		}

		/// <summary>Deserializes an instance of type <typeparamref name="T"/> from a JSON string literal</summary>
		/// <param name="jsonBytes">UTF-8 encoded JSON document to parse</param>
		/// <param name="settings">Serialization settings (use default JSON settings if null)</param>
		/// <returns>Deserialized instance.</returns>
		/// <exception cref="FormatException">If the JSON document is not syntactically correct.</exception>
		/// <exception cref="JsonBindingException">If the parsed JSON document cannot be bound to an instance of <typeparamref name="T"/>.</exception>
		public static T Deserialize<T>(this IJsonDeserializer<T> serializer, ReadOnlySpan<byte> jsonBytes, CrystalJsonSettings? settings = null, ICrystalJsonTypeResolver? resolver = null)
			where T : notnull
		{
			return serializer.Unpack(CrystalJson.Parse(jsonBytes, settings), resolver);
		}

		public static bool TryJsonDeserializeArray<T>(this IJsonDeserializer<T> serializer, Span<T> destination, out int written, JsonValue value, ICrystalJsonTypeResolver? resolver = null)
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
				throw ThrowHelper.ArgumentException(nameof(destination), "Destination buffer is too small");
			}

			for (int i = 0; i < input.Length; i++)
			{
				destination[i] = serializer.Unpack(input[i], resolver);
			}

			written = input.Length;
			return true;
		}

		[Pure]
		[return: NotNullIfNotNull(nameof(defaultValue))]
		public static T[]? JsonDeserializeArray<T>(this IJsonDeserializer<T> serializer, JsonValue? value, T[]? defaultValue = null, ICrystalJsonTypeResolver? resolver = null, string? fieldName = null)
			=> value switch
			{
				JsonArray arr => JsonDeserializeArray(serializer, arr, resolver),
				null or JsonNull => defaultValue,
				_ => throw (fieldName != null ? CrystalJson.Errors.Parsing_CannotCastFieldToJsonArray(value, fieldName) : CrystalJson.Errors.Parsing_CannotCastToJsonArray(value))
			};

		/// <summary>Deserializes a <see cref="JsonArray"/> into a <see cref="List{T}"/>, stored into a <b>required</b> field of a parent object</summary>
		[Pure]
		public static T[] JsonDeserializeArrayRequired<T>(this IJsonDeserializer<T> serializer, JsonValue? value, ICrystalJsonTypeResolver? resolver = null, string? fieldName = null)
			=> value switch
			{
				JsonArray arr => JsonDeserializeArray(serializer, arr, resolver),
				null or JsonNull => throw (fieldName != null ? CrystalJson.Errors.Parsing_FieldIsNullOrMissing(null, fieldName, null) : CrystalJson.Errors.Parsing_ValueIsNullOrMissing()),
				_ => throw (fieldName != null ? CrystalJson.Errors.Parsing_CannotCastFieldToJsonArray(value, fieldName) : CrystalJson.Errors.Parsing_CannotCastToJsonArray(value))
			};

		[Pure]
		public static T[] JsonDeserializeArray<T>(this IJsonDeserializer<T> serializer, JsonArray array, ICrystalJsonTypeResolver? resolver = null)
		{
			var input = array.GetSpan();
			var result = new T[input.Length];

			for (int i = 0; i < input.Length; i++)
			{
				result[i] = serializer.Unpack(input[i], resolver);
			}

			return result;
		}

		/// <summary>Deserializes a <see cref="JsonArray"/> into a <see cref="List{T}"/>, stored into an optional field of a parent object</summary>
		[Pure]
		[return: NotNullIfNotNull(nameof(defaultValue))]
		public static List<T>? JsonDeserializeList<T>(this IJsonDeserializer<T> serializer, JsonValue? value, List<T>? defaultValue = null, ICrystalJsonTypeResolver? resolver = null, string? fieldName = null)
			=> value switch
			{
				JsonArray arr => JsonDeserializeList(serializer, arr, resolver),
				null or JsonNull => defaultValue,
				_ => throw (fieldName != null ? CrystalJson.Errors.Parsing_CannotCastFieldToJsonArray(value, fieldName) : CrystalJson.Errors.Parsing_CannotCastToJsonArray(value))
			};

		/// <summary>Deserializes a <see cref="JsonArray"/> into a <see cref="List{T}"/>, stored into a <b>required</b> field of a parent object</summary>
		[Pure]
		public static List<T> JsonDeserializeListRequired<T>(this IJsonDeserializer<T> serializer, JsonValue? value, ICrystalJsonTypeResolver? resolver = null, string? fieldName = null)
			=> value switch
			{
				JsonArray arr => JsonDeserializeList(serializer, arr, resolver),
				null or JsonNull => throw (fieldName != null ? CrystalJson.Errors.Parsing_FieldIsNullOrMissing(null, fieldName, null) : CrystalJson.Errors.Parsing_ValueIsNullOrMissing()),
				_ => throw (fieldName != null ? CrystalJson.Errors.Parsing_CannotCastFieldToJsonArray(value, fieldName) : CrystalJson.Errors.Parsing_CannotCastToJsonArray(value))
			};

		/// <summary>Deserializes a <see cref="JsonArray"/> into a <see cref="List{T}"/></summary>
		[Pure]
		public static List<T> JsonDeserializeList<T>(this IJsonDeserializer<T> serializer, JsonArray array, ICrystalJsonTypeResolver? resolver = null)
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

		[Pure]
		[return: NotNullIfNotNull(nameof(defaultValue))]
		public static ImmutableArray<T>? JsonDeserializeImmutableArray<T>(this IJsonDeserializer<T> serializer, JsonValue? value, ImmutableArray<T>? defaultValue = null, ICrystalJsonTypeResolver? resolver = null, string? fieldName = null)
			=> value switch
			{
				null or JsonNull => defaultValue,
				JsonArray arr => JsonDeserializeImmutableArray(serializer, arr, resolver),
				_ => throw (fieldName != null ? CrystalJson.Errors.Parsing_CannotCastFieldToJsonArray(value, fieldName) : CrystalJson.Errors.Parsing_CannotCastToJsonArray(value))
			};

		[Pure]
		public static ImmutableArray<T> JsonDeserializeImmutableArray<T>(this IJsonDeserializer<T> serializer, JsonArray array, ICrystalJsonTypeResolver? resolver = null)
		{
			// will wrap the array, without any copy
			return ImmutableCollectionsMarshal.AsImmutableArray<T>(JsonDeserializeArray(serializer, array, resolver));
		}

		[Pure]
		[return: NotNullIfNotNull(nameof(defaultValue))]
		public static ImmutableList<T>? JsonDeserializeImmutableList<T>(this IJsonDeserializer<T> serializer, JsonValue? value, ImmutableList<T>? defaultValue = null, ICrystalJsonTypeResolver? resolver = null, string? fieldName = null)
			=> value switch
			{
				null or JsonNull => defaultValue,
				JsonArray arr => JsonDeserializeImmutableList(serializer, arr, resolver),
				_ => throw (fieldName != null ? CrystalJson.Errors.Parsing_CannotCastFieldToJsonArray(value, fieldName) : CrystalJson.Errors.Parsing_CannotCastToJsonArray(value))
			};

		[Pure]
		public static ImmutableList<T> JsonDeserializeImmutableList<T>(this IJsonDeserializer<T> serializer, JsonArray array, ICrystalJsonTypeResolver? resolver = null)
		{
			// not sure if there is a way to fill the immutable list "in place"?
			return ImmutableList.Create<T>(JsonDeserializeArray(serializer, array, resolver));
		}

		[Pure]
		[return: NotNullIfNotNull(nameof(defaultValue))]
		public static Dictionary<string, TValue>? JsonDeserializeDictionary<TValue>(this IJsonDeserializer<TValue> serializer, JsonValue? value, Dictionary<string, TValue>? defaultValue = null, IEqualityComparer<string>? keyComparer = null, ICrystalJsonTypeResolver? resolver = null, string? fieldName = null)
			=> value switch
			{
				null or JsonNull => defaultValue,
				JsonObject obj => JsonDeserializeDictionary(serializer, obj, keyComparer, resolver),
				_ => throw (fieldName != null ? CrystalJson.Errors.Parsing_CannotCastFieldToJsonObject(value, fieldName) : CrystalJson.Errors.Parsing_CannotCastToJsonObject(value))
			};

		[Pure]
		public static Dictionary<string, TValue> JsonDeserializeDictionary<TValue>(this IJsonDeserializer<TValue> serializer, JsonObject obj, IEqualityComparer<string>? keyComparer = null, ICrystalJsonTypeResolver? resolver = null)
		{
			var res = new Dictionary<string, TValue>(obj.Count, keyComparer);

			foreach (var kv in obj)
			{
				res.Add(kv.Key, serializer.Unpack(kv.Value, resolver));
			}

			return res;
		}

		#endregion

		#region IJsonPackerFor<T>...

		public static JsonArray JsonPackSpan<TValue>(this IJsonPacker<TValue> serializer, ReadOnlySpan<TValue> items, CrystalJsonSettings? settings = null, ICrystalJsonTypeResolver? resolver = null)
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

		[return: NotNullIfNotNull(nameof(items))]
		public static JsonArray? JsonPackArray<TValue>(this IJsonPacker<TValue> serializer, TValue[]? items, CrystalJsonSettings? settings = null, ICrystalJsonTypeResolver? resolver = null)
		{
			if (items == null)
			{
				return null;
			}

			return JsonPackSpan<TValue>(serializer, new ReadOnlySpan<TValue>(items), settings, resolver);
		}

		[return: NotNullIfNotNull(nameof(items))]
		public static JsonArray? JsonPackList<TValue>(this IJsonPacker<TValue> serializer, List<TValue>? items, CrystalJsonSettings? settings = null, ICrystalJsonTypeResolver? resolver = null)
		{
			if (items == null)
			{
				return null;
			}

			return JsonPackSpan<TValue>(serializer, CollectionsMarshal.AsSpan(items), settings, resolver);
		}

		[return: NotNullIfNotNull(nameof(items))]
		public static JsonArray? JsonPackEnumerable<TValue>(this IJsonPacker<TValue> serializer, IEnumerable<TValue>? items, CrystalJsonSettings? settings = null, ICrystalJsonTypeResolver? resolver = null)
		{
			if (items == null)
			{
				return null;
			}

			if (Buffer<TValue>.TryGetSpan(items, out var span))
			{
				return JsonPackSpan<TValue>(serializer, span, settings, resolver);
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

		[return: NotNullIfNotNull(nameof(items))]
		public static JsonObject? JsonPackObject<TValue>(this IJsonPacker<TValue> serializer, Dictionary<string, TValue>? items, CrystalJsonSettings? settings = null, ICrystalJsonTypeResolver? resolver = null)
		{
			if (items == null)
			{
				return null;
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

		[return: NotNullIfNotNull(nameof(items))]
		public static JsonObject? JsonPackObject<TValue>(this IJsonPacker<TValue> serializer, IDictionary<string, TValue>? items, CrystalJsonSettings? settings = null, ICrystalJsonTypeResolver? resolver = null)
		{
			if (items == null)
			{
				return null;
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

		#endregion

		#region CodeGen Helpers...

		// these methods are called by generated source code

		public static JsonArray JsonPackSpan(ReadOnlySpan<string> items, CrystalJsonSettings? settings, ICrystalJsonTypeResolver? resolver)
		{
			if (items.Length == 0)
			{
				return (settings.IsReadOnly()) ? JsonArray.ReadOnly.Empty : new ();
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

		public static JsonArray JsonPackSpan(ReadOnlySpan<bool> items, CrystalJsonSettings? settings, ICrystalJsonTypeResolver? resolver)
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

		public static JsonArray JsonPackSpan(ReadOnlySpan<int> items, CrystalJsonSettings? settings, ICrystalJsonTypeResolver? resolver)
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

		public static JsonArray JsonPackSpan(ReadOnlySpan<long> items, CrystalJsonSettings? settings, ICrystalJsonTypeResolver? resolver)
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

		public static JsonArray JsonPackSpan(ReadOnlySpan<float> items, CrystalJsonSettings? settings, ICrystalJsonTypeResolver? resolver)
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

		public static JsonArray JsonPackSpan(ReadOnlySpan<double> items, CrystalJsonSettings? settings, ICrystalJsonTypeResolver? resolver)
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

		public static JsonArray JsonPackSpan(ReadOnlySpan<Guid> items, CrystalJsonSettings? settings, ICrystalJsonTypeResolver? resolver)
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

		public static JsonArray JsonPackSpan(ReadOnlySpan<Uuid128> items, CrystalJsonSettings? settings, ICrystalJsonTypeResolver? resolver)
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

		public static JsonArray JsonPackSpan(ReadOnlySpan<Uuid64> items, CrystalJsonSettings? settings, ICrystalJsonTypeResolver? resolver)
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

		[return: NotNullIfNotNull(nameof(items))]
		public static JsonArray? JsonPackEnumerable(IEnumerable<bool>? items, CrystalJsonSettings? settings, ICrystalJsonTypeResolver? resolver)
		{
			if (items == null) return null;

			if (Buffer<bool>.TryGetSpan(items, out var span))
			{
				return JsonPackSpan(span, settings, resolver);
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

		[return: NotNullIfNotNull(nameof(items))]
		public static JsonArray? JsonPackEnumerable(IEnumerable<int>? items, CrystalJsonSettings? settings, ICrystalJsonTypeResolver? resolver)
		{
			if (items == null) return null;

			if (Buffer<int>.TryGetSpan(items, out var span))
			{
				return JsonPackSpan(span, settings, resolver);
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
		public static JsonArray? JsonPackEnumerable(IEnumerable<long>? items, CrystalJsonSettings? settings, ICrystalJsonTypeResolver? resolver)
		{
			if (items == null) return null;

			if (Buffer<long>.TryGetSpan(items, out var span))
			{
				return JsonPackSpan(span, settings, resolver);
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
		public static JsonArray? JsonPackEnumerable(IEnumerable<float>? items, CrystalJsonSettings? settings, ICrystalJsonTypeResolver? resolver)
		{
			if (items == null) return null;

			if (Buffer<float>.TryGetSpan(items, out var span))
			{
				return JsonPackSpan(span, settings, resolver);
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
		public static JsonArray? JsonPackEnumerable(IEnumerable<double>? items, CrystalJsonSettings? settings, ICrystalJsonTypeResolver? resolver)
		{
			if (items == null) return null;

			if (Buffer<double>.TryGetSpan(items, out var span))
			{
				return JsonPackSpan(span, settings, resolver);
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
		public static JsonArray? JsonPackArray(bool[]? items, CrystalJsonSettings? settings, ICrystalJsonTypeResolver? resolver)
		{
			if (items == null) return null;
			return JsonPackSpan(new ReadOnlySpan<bool>(items), settings, resolver);
		}

		[return: NotNullIfNotNull(nameof(items))]
		public static JsonArray? JsonPackArray(int[]? items, CrystalJsonSettings? settings, ICrystalJsonTypeResolver? resolver)
		{
			if (items == null) return null;
			return JsonPackSpan(new ReadOnlySpan<int>(items), settings, resolver);
		}

		[return: NotNullIfNotNull(nameof(items))]
		public static JsonArray? JsonPackArray(long[]? items, CrystalJsonSettings? settings, ICrystalJsonTypeResolver? resolver)
		{
			if (items == null) return null;
			return JsonPackSpan(new ReadOnlySpan<long>(items), settings, resolver);
		}

		[return: NotNullIfNotNull(nameof(items))]
		public static JsonArray? JsonPackArray(float[]? items, CrystalJsonSettings? settings, ICrystalJsonTypeResolver? resolver)
		{
			if (items == null) return null;
			return JsonPackSpan(new ReadOnlySpan<float>(items), settings, resolver);
		}

		[return: NotNullIfNotNull(nameof(items))]
		public static JsonArray? JsonPackArray(double[]? items, CrystalJsonSettings? settings, ICrystalJsonTypeResolver? resolver)
		{
			if (items == null) return null;
			return JsonPackSpan(new ReadOnlySpan<double>(items), settings, resolver);
		}

		[return: NotNullIfNotNull(nameof(items))]
		public static JsonArray? JsonPackArray(string[]? items, CrystalJsonSettings? settings, ICrystalJsonTypeResolver? resolver)
		{
			if (items == null) return null;
			return JsonPackSpan(new ReadOnlySpan<string>(items), settings, resolver);
		}

		[return: NotNullIfNotNull(nameof(items))]
		public static JsonArray? JsonPackArray(Guid[]? items, CrystalJsonSettings? settings, ICrystalJsonTypeResolver? resolver)
		{
			if (items == null) return null;
			return JsonPackSpan(new ReadOnlySpan<Guid>(items), settings, resolver);
		}

		[return: NotNullIfNotNull(nameof(items))]
		public static JsonArray? JsonPackArray(Uuid128[]? items, CrystalJsonSettings? settings, ICrystalJsonTypeResolver? resolver)
		{
			if (items == null) return null;
			return JsonPackSpan(new ReadOnlySpan<Uuid128>(items), settings, resolver);
		}

		[return: NotNullIfNotNull(nameof(items))]
		public static JsonArray? JsonPackArray(Uuid64[]? items, CrystalJsonSettings? settings, ICrystalJsonTypeResolver? resolver)
		{
			if (items == null) return null;
			return JsonPackSpan(new ReadOnlySpan<Uuid64>(items), settings, resolver);
		}

		[return: NotNullIfNotNull(nameof(items))]
		public static JsonArray? JsonPackList(List<bool>? items, CrystalJsonSettings? settings, ICrystalJsonTypeResolver? resolver)
		{
			if (items == null) return null;
			return JsonPackSpan(CollectionsMarshal.AsSpan(items), settings, resolver);
		}

		[return: NotNullIfNotNull(nameof(items))]
		public static JsonArray? JsonPackList(List<int>? items, CrystalJsonSettings? settings, ICrystalJsonTypeResolver? resolver)
		{
			if (items == null) return null;
			return JsonPackSpan(CollectionsMarshal.AsSpan(items), settings, resolver);
		}

		[return: NotNullIfNotNull(nameof(items))]
		public static JsonArray? JsonPackList(List<long>? items, CrystalJsonSettings? settings, ICrystalJsonTypeResolver? resolver)
		{
			if (items == null) return null;
			return JsonPackSpan(CollectionsMarshal.AsSpan(items), settings, resolver);
		}

		[return: NotNullIfNotNull(nameof(items))]
		public static JsonArray? JsonPackList(List<float>? items, CrystalJsonSettings? settings, ICrystalJsonTypeResolver? resolver)
		{
			if (items == null) return null;
			return JsonPackSpan(CollectionsMarshal.AsSpan(items), settings, resolver);
		}

		[return: NotNullIfNotNull(nameof(items))]
		public static JsonArray? JsonPackList(List<double>? items, CrystalJsonSettings? settings, ICrystalJsonTypeResolver? resolver)
		{
			if (items == null) return null;
			return JsonPackSpan(CollectionsMarshal.AsSpan(items), settings, resolver);
		}

		[return: NotNullIfNotNull(nameof(items))]
		public static JsonArray? JsonPackList(List<string>? items, CrystalJsonSettings? settings, ICrystalJsonTypeResolver? resolver)
		{
			if (items == null) return null;
			return JsonPackSpan(CollectionsMarshal.AsSpan(items), settings, resolver);
		}

		[return: NotNullIfNotNull(nameof(items))]
		public static JsonArray? JsonPackList(List<Guid>? items, CrystalJsonSettings? settings, ICrystalJsonTypeResolver? resolver)
		{
			if (items == null) return null;
			return JsonPackSpan(CollectionsMarshal.AsSpan(items), settings, resolver);
		}

		[return: NotNullIfNotNull(nameof(items))]
		public static JsonArray? JsonPackList(List<Uuid64>? items, CrystalJsonSettings? settings, ICrystalJsonTypeResolver? resolver)
		{
			if (items == null) return null;
			return JsonPackSpan(CollectionsMarshal.AsSpan(items), settings, resolver);
		}

		[return: NotNullIfNotNull(nameof(items))]
		public static JsonArray? JsonPackEnumerable(IEnumerable<string>? items, CrystalJsonSettings? settings, ICrystalJsonTypeResolver? resolver)
		{
			if (items == null) return null;

			if (Buffer<string>.TryGetSpan(items, out var span))
			{
				return JsonPackSpan(span, settings, resolver);
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

		[return: NotNullIfNotNull(nameof(items))]
		public static JsonArray? JsonPackEnumerable(IEnumerable<Guid>? items, CrystalJsonSettings? settings, ICrystalJsonTypeResolver? resolver)
		{
			if (items == null) return null;

			if (Buffer<Guid>.TryGetSpan(items, out var span))
			{
				return JsonPackSpan(span, settings, resolver);
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

		[return: NotNullIfNotNull(nameof(items))]
		public static JsonArray? JsonPackEnumerable(IEnumerable<Uuid128>? items, CrystalJsonSettings? settings, ICrystalJsonTypeResolver? resolver)
		{
			if (items == null) return null;

			if (Buffer<Uuid128>.TryGetSpan(items, out var span))
			{
				return JsonPackSpan(span, settings, resolver);
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

		[return: NotNullIfNotNull(nameof(items))]
		public static JsonArray? JsonPackSpan<TValue>(ReadOnlySpan<TValue> items, CrystalJsonSettings? settings, ICrystalJsonTypeResolver? resolver)
		{
			return JsonArray.FromValues<TValue>(items, settings, resolver);
		}

		[return: NotNullIfNotNull(nameof(items))]
		public static JsonArray? JsonPackArray<TValue>(TValue[]? items, CrystalJsonSettings? settings, ICrystalJsonTypeResolver? resolver)
		{
			if (items == null) return null;
			return JsonArray.FromValues<TValue>(new ReadOnlySpan<TValue>(items), settings, resolver);
		}

		[return: NotNullIfNotNull(nameof(items))]
		public static JsonArray? JsonPackList<TValue>(List<TValue>? items, CrystalJsonSettings? settings, ICrystalJsonTypeResolver? resolver)
		{
			if (items == null) return null;
			return JsonArray.FromValues<TValue>(CollectionsMarshal.AsSpan(items), settings, resolver);
		}

		[return: NotNullIfNotNull(nameof(items))]
		public static JsonArray? JsonPackEnumerable<TValue>(IEnumerable<TValue>? items, CrystalJsonSettings? settings, ICrystalJsonTypeResolver? resolver)
		{
			if (items == null) return null;
			return JsonArray.FromValues(items, settings, resolver);
		}

		[return: NotNullIfNotNull(nameof(items))]
		public static JsonObject? PackDictionary<TValue>(Dictionary<string, TValue>? items, CrystalJsonSettings? settings, ICrystalJsonTypeResolver? resolver)
		{
			if (items == null) return null;
			return JsonObject.FromValues<TValue>(items, settings, resolver);
		}

		[return: NotNullIfNotNull(nameof(items))]
		public static JsonObject? PackDictionary<TValue>(IDictionary<string, TValue>? items, CrystalJsonSettings? settings, ICrystalJsonTypeResolver? resolver)
		{
			if (items == null) return null;
			return JsonObject.FromValues<TValue>(items, settings, resolver);
		}

		[return: NotNullIfNotNull(nameof(items))]
		public static JsonObject? PackDictionary<TValue>(IEnumerable<KeyValuePair<string, TValue>>? items, CrystalJsonSettings? settings, ICrystalJsonTypeResolver? resolver)
		{
			if (items == null) return null;
			return JsonObject.FromValues<TValue>(items, settings, resolver);
		}

		public static T? Unpack<T>(JsonValue? value, ICrystalJsonTypeResolver? resolver)
			where T : IJsonDeserializable<T>
		{
			if (value is null or JsonNull)
			{
				return default(T);
			}
			return T.JsonDeserialize(value, resolver);
		}

		[return: NotNullIfNotNull(nameof(missingValue))]
		public static T? Unpack<T>(JsonValue? value, T? missingValue, ICrystalJsonTypeResolver? resolver)
			where T : IJsonDeserializable<T>
		{
			if (value is null or JsonNull)
			{
				return missingValue;
			}
			return T.JsonDeserialize(value, resolver);
		}

		public static T? UnpackNullable<T>(JsonValue? value, ICrystalJsonTypeResolver? resolver)
			where T : struct, IJsonDeserializable<T>
		{
			if (value is null or JsonNull)
			{
				return default(T);
			}
			return T.JsonDeserialize(value, resolver);
		}

		public static T UnpackRequired<T>(JsonValue? value, ICrystalJsonTypeResolver? resolver, JsonValue? parent = null, string? fieldName = null)
			where T : IJsonDeserializable<T>
		{
			if (value is null or JsonNull)
			{
				throw (fieldName != null ? CrystalJson.Errors.Parsing_FieldIsNullOrMissing(parent, fieldName, null) : CrystalJson.Errors.Parsing_ValueIsNullOrMissing());
			}
			return T.JsonDeserialize(value, resolver);
		}

		public static T UnpackRequired<T>(this IJsonDeserializer<T> converter, JsonValue? value, ICrystalJsonTypeResolver? resolver, JsonValue? parent = null, string? fieldName = null)
		{
			if (value is null or JsonNull)
			{
				throw (fieldName != null ? CrystalJson.Errors.Parsing_FieldIsNullOrMissing(parent, fieldName, null) : CrystalJson.Errors.Parsing_ValueIsNullOrMissing());
			}
			return converter.Unpack(value, resolver);
		}

		#endregion

	}

}
