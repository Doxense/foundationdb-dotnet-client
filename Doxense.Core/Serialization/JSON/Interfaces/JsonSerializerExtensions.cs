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
	using System.Collections.Immutable;
	using System.Diagnostics.CodeAnalysis;
	using System.Runtime.InteropServices;

	public static class JsonSerializerExtensions
	{

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

	}

}
