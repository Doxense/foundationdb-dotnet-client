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
	using System.Collections.Immutable;
	using System.Diagnostics.CodeAnalysis;
	using System.Runtime.InteropServices;
	using Doxense.Linq;

	public static class JsonSerializerExtensions
	{

		#region IJsonDeserializerFor<T>...

		public static bool TryJsonDeserializeSpan<T>(this IJsonDeserializerFor<T> serializer, Span<T> destination, out int written, JsonValue value, ICrystalJsonTypeResolver? resolver = null)
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
				destination[i] = serializer.JsonDeserialize(input[i], resolver);
			}

			written = input.Length;
			return true;
		}

		[Pure]
		[return: NotNullIfNotNull(nameof(defaultValue))]
		public static T[]? JsonDeserializeArray<T>(this IJsonDeserializerFor<T> serializer, JsonValue? value, T[]? defaultValue = null, ICrystalJsonTypeResolver? resolver = null)
		{
			return !value.IsNullOrMissing() ? JsonDeserializeArray(serializer, value.AsArray(), resolver) : defaultValue;
		}

		[Pure]
		public static T[] JsonDeserializeArray<T>(this IJsonDeserializerFor<T> serializer, JsonArray array, ICrystalJsonTypeResolver? resolver = null)
		{
			var input = array.GetSpan();
			var result = new T[input.Length];

			for (int i = 0; i < input.Length; i++)
			{
				result[i] = serializer.JsonDeserialize(input[i], resolver);
			}

			return result;
		}

		[Pure]
		[return: NotNullIfNotNull(nameof(defaultValue))]
		public static List<T>? JsonDeserializeList<T>(this IJsonDeserializerFor<T> serializer, JsonValue? value, List<T>? defaultValue = null, ICrystalJsonTypeResolver? resolver = null)
		{
			return !value.IsNullOrMissing() ? JsonDeserializeList(serializer, value.AsArray(), resolver) : defaultValue;
		}

		[Pure]
		public static List<T> JsonDeserializeList<T>(this IJsonDeserializerFor<T> serializer, JsonArray array, ICrystalJsonTypeResolver? resolver = null)
		{
			var input = array.GetSpan();

#if NET8_0_OR_GREATER
			var result = new List<T>();
			// return a span with the correct size
			CollectionsMarshal.SetCount(result, input.Length);
			var span = CollectionsMarshal.AsSpan(result);

			for (int i = 0; i < input.Length; i++)
			{
				span[i] = serializer.JsonDeserialize(input[i], resolver);
			}
#else
			var result = new List<T>(array.Count);
			foreach (var item in array)
			{
				result.Add(serializer.JsonDeserialize(item, resolver));
			}
#endif

			return result;
		}

		[Pure]
		[return: NotNullIfNotNull(nameof(defaultValue))]
		public static ImmutableArray<T>? JsonDeserializeImmutableArray<T>(this IJsonDeserializerFor<T> serializer, JsonValue? value, ImmutableArray<T>? defaultValue = null, ICrystalJsonTypeResolver? resolver = null)
		{
			return !value.IsNullOrMissing() ? JsonDeserializeImmutableArray(serializer, value.AsArray(), resolver) : defaultValue;
		}

		[Pure]
		public static ImmutableArray<T> JsonDeserializeImmutableArray<T>(this IJsonDeserializerFor<T> serializer, JsonArray array, ICrystalJsonTypeResolver? resolver = null)
		{
			// will wrap the array, without any copy
			return ImmutableCollectionsMarshal.AsImmutableArray<T>(JsonDeserializeArray(serializer, array, resolver));
		}

		[Pure]
		[return: NotNullIfNotNull(nameof(defaultValue))]
		public static ImmutableList<T>? JsonDeserializeImmutableList<T>(this IJsonDeserializerFor<T> serializer, JsonValue? value, ImmutableList<T>? defaultValue = null, ICrystalJsonTypeResolver? resolver = null)
		{
			return !value.IsNullOrMissing() ? JsonDeserializeImmutableList(serializer, value.AsArray(), resolver) : defaultValue;
		}

		[Pure]
		public static ImmutableList<T> JsonDeserializeImmutableList<T>(this IJsonDeserializerFor<T> serializer, JsonArray array, ICrystalJsonTypeResolver? resolver = null)
		{
			// not sure if there is a way to fill the immutable list "in place"?
			return ImmutableList.Create<T>(JsonDeserializeArray(serializer, array, resolver));
		}

		[Pure]
		[return: NotNullIfNotNull(nameof(defaultValue))]
		public static Dictionary<string, TValue>? JsonDeserializeDictionary<TValue>(this IJsonDeserializerFor<TValue> serializer, JsonValue? value, Dictionary<string, TValue>? defaultValue = null, IEqualityComparer<string>? keyComparer = null, ICrystalJsonTypeResolver? resolver = null)
		{
			return !value.IsNullOrMissing() ? JsonDeserializeDictionary(serializer, value.AsObject(), keyComparer, resolver) : defaultValue;
		}

		[Pure]
		public static Dictionary<string, TValue> JsonDeserializeDictionary<TValue>(this IJsonDeserializerFor<TValue> serializer, JsonObject obj, IEqualityComparer<string>? keyComparer = null, ICrystalJsonTypeResolver? resolver = null)
		{
			var res = new Dictionary<string, TValue>(obj.Count, keyComparer);

			foreach (var kv in obj)
			{
				res.Add(kv.Key, serializer.JsonDeserialize(kv.Value, resolver));
			}

			return res;
		}

		#endregion


		#region CodeGen Helpers...

		// these methods are called by generated source code

		public static JsonArray JsonPackSpan(ReadOnlySpan<string> items, CrystalJsonSettings? settings = null, ICrystalJsonTypeResolver? resolver = null)
		{
			if (items.Length == 0)
			{
				return (settings?.ReadOnly ?? false) ? JsonArray.EmptyReadOnly : new ();
			}

			var arr = new JsonArray();
			var buf = arr.GetSpanAndSetCount(items.Length);

			for (int i = 0; i < items.Length; i++)
			{
				buf[i] = JsonString.Return(items[i]);
			}

			if (settings?.ReadOnly ?? false)
			{
				arr.FreezeUnsafe();
			}
			return arr;
		}

		public static JsonArray JsonPackSpan(ReadOnlySpan<bool> items, CrystalJsonSettings? settings = null, ICrystalJsonTypeResolver? resolver = null)
		{
			if (items.Length == 0)
			{
				return (settings?.ReadOnly ?? false) ? JsonArray.EmptyReadOnly : new();
			}

			var arr = new JsonArray();
			var buf = arr.GetSpanAndSetCount(items.Length);

			for (int i = 0; i < items.Length; i++)
			{
				buf[i] = items[i] ? JsonBoolean.True : JsonBoolean.False;
			}

			if (settings?.ReadOnly ?? false)
			{
				arr.FreezeUnsafe();
			}
			return arr;
		}

		public static JsonArray JsonPackSpan(ReadOnlySpan<int> items, CrystalJsonSettings? settings = null, ICrystalJsonTypeResolver? resolver = null)
		{
			if (items.Length == 0)
			{
				return (settings?.ReadOnly ?? false) ? JsonArray.EmptyReadOnly : new();
			}

			var arr = new JsonArray();
			var buf = arr.GetSpanAndSetCount(items.Length);

			for (int i = 0; i < items.Length; i++)
			{
				buf[i] = JsonNumber.Return(items[i]);
			}

			if (settings?.ReadOnly ?? false)
			{
				arr.FreezeUnsafe();
			}
			return arr;
		}

		public static JsonArray JsonPackSpan(ReadOnlySpan<long> items, CrystalJsonSettings? settings = null, ICrystalJsonTypeResolver? resolver = null)
		{
			if (items.Length == 0)
			{
				return (settings?.ReadOnly ?? false) ? JsonArray.EmptyReadOnly : new();
			}

			var arr = new JsonArray();
			var buf = arr.GetSpanAndSetCount(items.Length);

			for (int i = 0; i < items.Length; i++)
			{
				buf[i] = JsonNumber.Return(items[i]);
			}

			if (settings?.ReadOnly ?? false)
			{
				arr.FreezeUnsafe();
			}
			return arr;
		}

		public static JsonArray JsonPackSpan(ReadOnlySpan<float> items, CrystalJsonSettings? settings = null, ICrystalJsonTypeResolver? resolver = null)
		{
			if (items.Length == 0)
			{
				return (settings?.ReadOnly ?? false) ? JsonArray.EmptyReadOnly : new();
			}

			var arr = new JsonArray();
			var buf = arr.GetSpanAndSetCount(items.Length);

			for (int i = 0; i < items.Length; i++)
			{
				buf[i] = JsonNumber.Return(items[i]);
			}

			if (settings?.ReadOnly ?? false)
			{
				arr.FreezeUnsafe();
			}
			return arr;
		}

		public static JsonArray JsonPackSpan(ReadOnlySpan<double> items, CrystalJsonSettings? settings = null, ICrystalJsonTypeResolver? resolver = null)
		{
			if (items.Length == 0)
			{
				return (settings?.ReadOnly ?? false) ? JsonArray.EmptyReadOnly : new();
			}

			var arr = new JsonArray();
			var buf = arr.GetSpanAndSetCount(items.Length);

			for (int i = 0; i < items.Length; i++)
			{
				buf[i] = JsonNumber.Return(items[i]);
			}

			if (settings?.ReadOnly ?? false)
			{
				arr.FreezeUnsafe();
			}
			return arr;
		}

		public static JsonArray JsonPackSpan(ReadOnlySpan<Guid> items, CrystalJsonSettings? settings = null, ICrystalJsonTypeResolver? resolver = null)
		{
			if (items.Length == 0)
			{
				return (settings?.ReadOnly ?? false) ? JsonArray.EmptyReadOnly : new();
			}

			var arr = new JsonArray();
			var buf = arr.GetSpanAndSetCount(items.Length);

			for (int i = 0; i < items.Length; i++)
			{
				buf[i] = JsonString.Return(items[i]);
			}

			if (settings?.ReadOnly ?? false)
			{
				arr.FreezeUnsafe();
			}
			return arr;
		}

		public static JsonArray JsonPackSpan(ReadOnlySpan<Uuid128> items, CrystalJsonSettings? settings = null, ICrystalJsonTypeResolver? resolver = null)
		{
			if (items.Length == 0)
			{
				return (settings?.ReadOnly ?? false) ? JsonArray.EmptyReadOnly : new();
			}

			var arr = new JsonArray();
			var buf = arr.GetSpanAndSetCount(items.Length);

			for (int i = 0; i < items.Length; i++)
			{
				buf[i] = JsonString.Return(items[i]);
			}

			if (settings?.ReadOnly ?? false)
			{
				arr.FreezeUnsafe();
			}
			return arr;
		}

		public static JsonArray JsonPackSpan(ReadOnlySpan<Uuid64> items, CrystalJsonSettings? settings = null, ICrystalJsonTypeResolver? resolver = null)
		{
			if (items.Length == 0)
			{
				return (settings?.ReadOnly ?? false) ? JsonArray.EmptyReadOnly : new();
			}

			var arr = new JsonArray();
			var buf = arr.GetSpanAndSetCount(items.Length);

			for (int i = 0; i < items.Length; i++)
			{
				buf[i] = JsonString.Return(items[i]);
			}

			if (settings?.ReadOnly ?? false)
			{
				arr.FreezeUnsafe();
			}
			return arr;
		}

		[return: NotNullIfNotNull(nameof(items))]
		public static JsonArray? JsonPackEnumerable(IEnumerable<bool>? items, CrystalJsonSettings? settings = null, ICrystalJsonTypeResolver? resolver = null)
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

			if (settings?.ReadOnly ?? false)
			{
				arr.FreezeUnsafe();
			}
			return arr;
		}

		[return: NotNullIfNotNull(nameof(items))]
		public static JsonArray? JsonPackEnumerable(IEnumerable<int>? items, CrystalJsonSettings? settings = null, ICrystalJsonTypeResolver? resolver = null)
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

			if (settings?.ReadOnly ?? false)
			{
				arr.FreezeUnsafe();
			}
			return arr;
		}

		[return: NotNullIfNotNull(nameof(items))]
		public static JsonArray? JsonPackEnumerable(IEnumerable<long>? items, CrystalJsonSettings? settings = null, ICrystalJsonTypeResolver? resolver = null)
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

			if (settings?.ReadOnly ?? false)
			{
				arr.FreezeUnsafe();
			}
			return arr;
		}

		[return: NotNullIfNotNull(nameof(items))]
		public static JsonArray? JsonPackEnumerable(IEnumerable<float>? items, CrystalJsonSettings? settings = null, ICrystalJsonTypeResolver? resolver = null)
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

			if (settings?.ReadOnly ?? false)
			{
				arr.FreezeUnsafe();
			}
			return arr;
		}

		[return: NotNullIfNotNull(nameof(items))]
		public static JsonArray? JsonPackEnumerable(IEnumerable<double>? items, CrystalJsonSettings? settings = null, ICrystalJsonTypeResolver? resolver = null)
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

			if (settings?.ReadOnly ?? false)
			{
				arr.FreezeUnsafe();
			}
			return arr;
		}

		[return: NotNullIfNotNull(nameof(items))]
		public static JsonArray? JsonPackArray(bool[]? items, CrystalJsonSettings? settings = null, ICrystalJsonTypeResolver? resolver = null)
		{
			if (items == null) return null;
			return JsonPackSpan(new ReadOnlySpan<bool>(items), settings, resolver);
		}

		[return: NotNullIfNotNull(nameof(items))]
		public static JsonArray? JsonPackArray(int[]? items, CrystalJsonSettings? settings = null, ICrystalJsonTypeResolver? resolver = null)
		{
			if (items == null) return null;
			return JsonPackSpan(new ReadOnlySpan<int>(items), settings, resolver);
		}

		[return: NotNullIfNotNull(nameof(items))]
		public static JsonArray? JsonPackArray(long[]? items, CrystalJsonSettings? settings = null, ICrystalJsonTypeResolver? resolver = null)
		{
			if (items == null) return null;
			return JsonPackSpan(new ReadOnlySpan<long>(items), settings, resolver);
		}

		[return: NotNullIfNotNull(nameof(items))]
		public static JsonArray? JsonPackArray(float[]? items, CrystalJsonSettings? settings = null, ICrystalJsonTypeResolver? resolver = null)
		{
			if (items == null) return null;
			return JsonPackSpan(new ReadOnlySpan<float>(items), settings, resolver);
		}

		[return: NotNullIfNotNull(nameof(items))]
		public static JsonArray? JsonPackArray(double[]? items, CrystalJsonSettings? settings = null, ICrystalJsonTypeResolver? resolver = null)
		{
			if (items == null) return null;
			return JsonPackSpan(new ReadOnlySpan<double>(items), settings, resolver);
		}

		[return: NotNullIfNotNull(nameof(items))]
		public static JsonArray? JsonPackArray(string[]? items, CrystalJsonSettings? settings = null, ICrystalJsonTypeResolver? resolver = null)
		{
			if (items == null) return null;
			return JsonPackSpan(new ReadOnlySpan<string>(items), settings, resolver);
		}

		[return: NotNullIfNotNull(nameof(items))]
		public static JsonArray? JsonPackArray(Guid[]? items, CrystalJsonSettings? settings = null, ICrystalJsonTypeResolver? resolver = null)
		{
			if (items == null) return null;
			return JsonPackSpan(new ReadOnlySpan<Guid>(items), settings, resolver);
		}

		[return: NotNullIfNotNull(nameof(items))]
		public static JsonArray? JsonPackArray(Uuid128[]? items, CrystalJsonSettings? settings = null, ICrystalJsonTypeResolver? resolver = null)
		{
			if (items == null) return null;
			return JsonPackSpan(new ReadOnlySpan<Uuid128>(items), settings, resolver);
		}

		[return: NotNullIfNotNull(nameof(items))]
		public static JsonArray? JsonPackArray(Uuid64[]? items, CrystalJsonSettings? settings = null, ICrystalJsonTypeResolver? resolver = null)
		{
			if (items == null) return null;
			return JsonPackSpan(new ReadOnlySpan<Uuid64>(items), settings, resolver);
		}

		[return: NotNullIfNotNull(nameof(items))]
		public static JsonArray? JsonPackList(List<bool>? items, CrystalJsonSettings? settings = null, ICrystalJsonTypeResolver? resolver = null)
		{
			if (items == null) return null;
			return JsonPackSpan(CollectionsMarshal.AsSpan(items), settings, resolver);
		}

		[return: NotNullIfNotNull(nameof(items))]
		public static JsonArray? JsonPackList(List<int>? items, CrystalJsonSettings? settings = null, ICrystalJsonTypeResolver? resolver = null)
		{
			if (items == null) return null;
			return JsonPackSpan(CollectionsMarshal.AsSpan(items), settings, resolver);
		}

		[return: NotNullIfNotNull(nameof(items))]
		public static JsonArray? JsonPackList(List<long>? items, CrystalJsonSettings? settings = null, ICrystalJsonTypeResolver? resolver = null)
		{
			if (items == null) return null;
			return JsonPackSpan(CollectionsMarshal.AsSpan(items), settings, resolver);
		}

		[return: NotNullIfNotNull(nameof(items))]
		public static JsonArray? JsonPackList(List<float>? items, CrystalJsonSettings? settings = null, ICrystalJsonTypeResolver? resolver = null)
		{
			if (items == null) return null;
			return JsonPackSpan(CollectionsMarshal.AsSpan(items), settings, resolver);
		}

		[return: NotNullIfNotNull(nameof(items))]
		public static JsonArray? JsonPackList(List<double>? items, CrystalJsonSettings? settings = null, ICrystalJsonTypeResolver? resolver = null)
		{
			if (items == null) return null;
			return JsonPackSpan(CollectionsMarshal.AsSpan(items), settings, resolver);
		}

		[return: NotNullIfNotNull(nameof(items))]
		public static JsonArray? JsonPackList(List<string>? items, CrystalJsonSettings? settings = null, ICrystalJsonTypeResolver? resolver = null)
		{
			if (items == null) return null;
			return JsonPackSpan(CollectionsMarshal.AsSpan(items), settings, resolver);
		}

		[return: NotNullIfNotNull(nameof(items))]
		public static JsonArray? JsonPackList(List<Guid>? items, CrystalJsonSettings? settings = null, ICrystalJsonTypeResolver? resolver = null)
		{
			if (items == null) return null;
			return JsonPackSpan(CollectionsMarshal.AsSpan(items), settings, resolver);
		}

		[return: NotNullIfNotNull(nameof(items))]
		public static JsonArray? JsonPackList(List<Uuid64>? items, CrystalJsonSettings? settings = null, ICrystalJsonTypeResolver? resolver = null)
		{
			if (items == null) return null;
			return JsonPackSpan(CollectionsMarshal.AsSpan(items), settings, resolver);
		}

		[return: NotNullIfNotNull(nameof(items))]
		public static JsonArray? JsonPackEnumerable(IEnumerable<string>? items, CrystalJsonSettings? settings = null, ICrystalJsonTypeResolver? resolver = null)
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

			if (settings?.ReadOnly ?? false)
			{
				arr.FreezeUnsafe();
			}
			return arr;
		}

		[return: NotNullIfNotNull(nameof(items))]
		public static JsonArray? JsonPackEnumerable(IEnumerable<Guid>? items, CrystalJsonSettings? settings = null, ICrystalJsonTypeResolver? resolver = null)
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

			if (settings?.ReadOnly ?? false)
			{
				arr.FreezeUnsafe();
			}
			return arr;
		}

		[return: NotNullIfNotNull(nameof(items))]
		public static JsonArray? JsonPackEnumerable(IEnumerable<Uuid128>? items, CrystalJsonSettings? settings = null, ICrystalJsonTypeResolver? resolver = null)
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

			if (settings?.ReadOnly ?? false)
			{
				arr.FreezeUnsafe();
			}
			return arr;
		}

		[return: NotNullIfNotNull(nameof(items))]
		public static JsonArray? JsonPackSpan<TValue>(ReadOnlySpan<TValue> items, CrystalJsonSettings? settings = null, ICrystalJsonTypeResolver? resolver = null)
		{
			return JsonArray.FromValues<TValue>(items, settings, resolver);
		}

		[return: NotNullIfNotNull(nameof(items))]
		public static JsonArray? JsonPackArray<TValue>(TValue[]? items, CrystalJsonSettings? settings = null, ICrystalJsonTypeResolver? resolver = null)
		{
			if (items == null) return null;
			return JsonArray.FromValues<TValue>(new ReadOnlySpan<TValue>(items), settings, resolver);
		}

		[return: NotNullIfNotNull(nameof(items))]
		public static JsonArray? JsonPackList<TValue>(List<TValue>? items, CrystalJsonSettings? settings = null, ICrystalJsonTypeResolver? resolver = null)
		{
			if (items == null) return null;
			return JsonArray.FromValues<TValue>(CollectionsMarshal.AsSpan(items), settings, resolver);
		}

		[return: NotNullIfNotNull(nameof(items))]
		public static JsonArray? JsonPackEnumerable<TValue>(IEnumerable<TValue>? items, CrystalJsonSettings? settings = null, ICrystalJsonTypeResolver? resolver = null)
		{
			if (items == null) return null;
			return JsonArray.FromValues(items, settings, resolver);
		}

		[return: NotNullIfNotNull(nameof(items))]
		public static JsonObject? PackDictionary<TValue>(Dictionary<string, TValue>? items, CrystalJsonSettings? settings = null, ICrystalJsonTypeResolver? resolver = null)
		{
			if (items == null) return null;
			return JsonObject.FromValues<TValue>(items, null, settings, resolver);
		}

		[return: NotNullIfNotNull(nameof(items))]
		public static JsonObject? PackDictionary<TValue>(IDictionary<string, TValue>? items, CrystalJsonSettings? settings = null, ICrystalJsonTypeResolver? resolver = null)
		{
			if (items == null) return null;
			return JsonObject.FromValues<TValue>(items, null, settings, resolver);
		}

		[return: NotNullIfNotNull(nameof(items))]
		public static JsonObject? PackDictionary<TValue>(IEnumerable<KeyValuePair<string, TValue>>? items, CrystalJsonSettings? settings = null, ICrystalJsonTypeResolver? resolver = null)
		{
			if (items == null) return null;
			return JsonObject.FromValues<TValue>(items, null, settings, resolver);
		}

		#endregion

		#region IJsonPackerFor<T>...

		public static JsonArray JsonPackSpan<TValue>(this IJsonPackerFor<TValue> serializer, ReadOnlySpan<TValue> items, CrystalJsonSettings? settings = null, ICrystalJsonTypeResolver? resolver = null)
		{
			var result = new JsonArray();
			var span = result.GetSpanAndSetCount(items.Length);

			for (int i = 0; i < items.Length; i++)
			{
				span[i] = items[i] is not null ? serializer.JsonPack(items[i], settings, resolver) : JsonNull.Null;
			}

			if (settings?.ReadOnly ?? false)
			{
				result.FreezeUnsafe();
			}

			return result;
		}

		[return: NotNullIfNotNull(nameof(items))]
		public static JsonArray? JsonPackArray<TValue>(this IJsonPackerFor<TValue> serializer, TValue[]? items, CrystalJsonSettings? settings = null, ICrystalJsonTypeResolver? resolver = null)
		{
			if (items == null)
			{
				return null;
			}

			return JsonPackSpan<TValue>(serializer, new ReadOnlySpan<TValue>(items), settings, resolver);
		}

		[return: NotNullIfNotNull(nameof(items))]
		public static JsonArray? JsonPackList<TValue>(this IJsonPackerFor<TValue> serializer, List<TValue>? items, CrystalJsonSettings? settings = null, ICrystalJsonTypeResolver? resolver = null)
		{
			if (items == null)
			{
				return null;
			}

			return JsonPackSpan<TValue>(serializer, CollectionsMarshal.AsSpan(items), settings, resolver);
		}

		[return: NotNullIfNotNull(nameof(items))]
		public static JsonArray? JsonPackEnumerable<TValue>(this IJsonPackerFor<TValue> serializer, IEnumerable<TValue>? items, CrystalJsonSettings? settings = null, ICrystalJsonTypeResolver? resolver = null)
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
				result.Add(item is not null ? serializer.JsonPack(item, settings, resolver) : JsonNull.Null);
			}

			if (settings?.ReadOnly ?? false)
			{
				result.FreezeUnsafe();
			}

			return result;
		}

		[return: NotNullIfNotNull(nameof(items))]
		public static JsonObject? JsonPackObject<TValue>(this IJsonPackerFor<TValue> serializer, Dictionary<string, TValue>? items, CrystalJsonSettings? settings = null, ICrystalJsonTypeResolver? resolver = null)
		{
			if (items == null)
			{
				return null;
			}

			var result = new JsonObject(items.Count);
			foreach (var kv in items)
			{
				result[kv.Key] = kv.Value is not null ? serializer.JsonPack(kv.Value, settings, resolver) : JsonNull.Null;
			}
			return result;
		}

		[return: NotNullIfNotNull(nameof(items))]
		public static JsonObject? JsonPackObject<TValue>(this IJsonPackerFor<TValue> serializer, IDictionary<string, TValue>? items, CrystalJsonSettings? settings = null, ICrystalJsonTypeResolver? resolver = null)
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
					result[kv.Key] = kv.Value is not null ? serializer.JsonPack(kv.Value, settings, resolver) : JsonNull.Null;
				}
			}
			else
			{
				foreach (var kv in items)
				{
					result[kv.Key] = kv.Value is not null ? serializer.JsonPack(kv.Value, settings, resolver) : JsonNull.Null;
				}
			}
			return result;
		}

		#endregion

	}

}
