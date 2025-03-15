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
	using System.Collections;

	/// <summary>Wraps a <see cref="JsonArray"/> into a typed mutable proxy that emulates an array of elements of type <typeparamref name="TValue"/></summary>
	/// <typeparam name="TValue">Emulated element type</typeparam>
	[PublicAPI]
	public readonly struct JsonWritableProxyArray<TValue> : IList<TValue>, IJsonSerializable, IJsonPackable
	{

		private readonly MutableJsonValue m_value;

		private readonly IJsonConverter<TValue> m_converter;

		public JsonWritableProxyArray(MutableJsonValue value, IJsonConverter<TValue>? converter = null, IJsonProxyNode? parent = null, JsonPathSegment segment = default)
		{
			m_value = value;
			m_converter = converter ?? RuntimeJsonConverter<TValue>.Default;
		}

		[Pure, MethodImpl(MethodImplOptions.NoInlining)]
		private static InvalidOperationException OperationRequiresArray() => new("This operation requires a valid JSON Array");

		public TValue[] ToArray() => m_value.Json switch
		{
			JsonArray arr => m_converter.JsonDeserializeArray(arr),
			JsonNull => [ ],
			_ => throw OperationRequiresArray(),
		};

		public List<TValue> ToList() => m_value.Json switch
		{
			JsonArray arr => m_converter.JsonDeserializeList(arr),
			JsonNull => [ ],
			_ => throw OperationRequiresArray(),
		};

		/// <inheritdoc />
		void IJsonSerializable.JsonSerialize(CrystalJsonWriter writer) => m_value.Json.JsonSerialize(writer);

		/// <inheritdoc />
		JsonValue IJsonPackable.JsonPack(CrystalJsonSettings settings, ICrystalJsonTypeResolver resolver) => m_value.Json;

		public JsonValue ToJson() => m_value.Json;

		/// <inheritdoc />
		public IEnumerator<TValue> GetEnumerator()
		{
			if (m_value.Json is not JsonArray arr)
			{
				if (m_value.Json is JsonNull)
				{
					yield break;
				}
				throw OperationRequiresArray();
			}

			foreach (var item in arr)
			{
				yield return m_converter.Unpack(item);
			}
		}

		/// <inheritdoc />
		IEnumerator IEnumerable.GetEnumerator() => this.GetEnumerator();

		/// <inheritdoc />
		public void Add(TValue item) => m_value.Add(m_converter.Pack(item));

		/// <inheritdoc />
		public void Clear() => m_value.Clear();

		/// <inheritdoc />
		bool ICollection<TValue>.Contains(TValue item) => throw new NotSupportedException("This operation is too costly.");

		/// <inheritdoc />
		public void CopyTo(TValue[] array, int arrayIndex)
		{
			Contract.NotNull(array);
			Contract.Positive(arrayIndex);
			Contract.DoesNotOverflow(array, arrayIndex, m_value.Count);

			if (m_value.Json is not JsonArray arr)
			{
				if (m_value.Json is JsonNull)
				{
					return;
				}
				throw OperationRequiresArray();
			}

			foreach (var item in arr)
			{
				array[arrayIndex++] = m_converter.Unpack(item);
			}
		}

		/// <inheritdoc />
		bool ICollection<TValue>.Remove(TValue item) => throw new NotSupportedException("This operation is too costly.");

		/// <inheritdoc />
		int IList<TValue>.IndexOf(TValue item) => throw new NotSupportedException("This operation is too costly.");

		/// <inheritdoc />
		public int Count { get; }

		/// <inheritdoc />
		bool ICollection<TValue>.IsReadOnly => false;

		/// <inheritdoc />
		public void Insert(int index, TValue item) => m_value.Insert(index, m_converter.Pack(item));

		/// <inheritdoc />
		public void RemoveAt(int index) => m_value.RemoveAt(index);

		/// <inheritdoc />
		public TValue this[int index]
		{
			get => m_converter.Unpack(m_value.Json[index]);
			set => m_value.Set(index, m_converter.Pack(value));
		}

		/// <inheritdoc />
		public override string ToString() => typeof(TValue).GetFriendlyName() + "[]";

	}

	/// <summary>Wraps a <see cref="JsonArray"/> into a typed mutable proxy that emulates an array of elements of type <typeparamref name="TValue"/></summary>
	/// <typeparam name="TValue">Emulated element type</typeparam>
	/// <typeparam name="TProxy">Corresponding <see cref="IJsonWritableProxy{TValue}"/> for type <typeparamref name="TValue"/>, usually source-generated</typeparam>
	[PublicAPI]
	public sealed class JsonWritableProxyArray<TValue, TProxy> : IList<TProxy>, IJsonProxyNode, IJsonSerializable, IJsonPackable
		where TProxy : IJsonWritableProxy<TValue, TProxy>
	{

		private readonly MutableJsonValue m_value;

		public JsonWritableProxyArray(MutableJsonValue value)
		{
			m_value = value;
		}

		/// <inheritdoc />
		JsonType IJsonProxyNode.Type => JsonType.Array;

		/// <inheritdoc />
		IJsonProxyNode? IJsonProxyNode.Parent => m_value.Parent;

		/// <inheritdoc />
		JsonPathSegment IJsonProxyNode.Segment => m_value.Segment;

		/// <inheritdoc />
		int IJsonProxyNode.Depth => m_value.Depth;

		/// <inheritdoc />
		void IJsonProxyNode.WritePath(ref JsonPathBuilder builder)
		{
			m_value.WritePath(ref builder);
		}

		public TValue[] ToArray() => m_value.Json switch
		{
			JsonArray arr => TProxy.Converter.JsonDeserializeArray(arr),
			JsonNull => [ ],
			_ => throw OperationRequiresArrayOrNull(),
		};

		public List<TValue> ToList() => m_value.Json switch
		{
			JsonArray arr => TProxy.Converter.JsonDeserializeList(arr),
			JsonNull => [ ],
			_ => throw OperationRequiresArrayOrNull(),
		};

		/// <inheritdoc />
		void IJsonSerializable.JsonSerialize(CrystalJsonWriter writer) => m_value.Json.JsonSerialize(writer);

		/// <inheritdoc />
		JsonValue IJsonPackable.JsonPack(CrystalJsonSettings settings, ICrystalJsonTypeResolver resolver) => m_value.Json;

		public JsonValue ToJson() => m_value.Json;

		[Pure, MethodImpl(MethodImplOptions.NoInlining)]
		private static InvalidOperationException OperationRequiresArrayOrNull() => new("This operation requires a valid JSON Array");

		/// <inheritdoc />
		public IEnumerator<TProxy> GetEnumerator()
		{
			if (m_value.Json is not JsonArray array)
			{
				if (m_value.Json is JsonNull)
				{
					yield break;
				}
				throw OperationRequiresArrayOrNull();
			}

			for(int i = 0; i < array.Count; i++)
			{
				yield return TProxy.Create(m_value[i]);
			}
		}

		/// <inheritdoc />
		IEnumerator IEnumerable.GetEnumerator() => this.GetEnumerator();

		/// <inheritdoc />
		public void Add(TProxy value) => m_value.Add(value.ToJson());

		public void Add(TValue value) => m_value.Add(TProxy.Converter.Pack(value));

		/// <inheritdoc />
		public void Clear() => m_value.Clear();

		/// <inheritdoc />
		bool ICollection<TProxy>.Contains(TProxy item) => throw new NotSupportedException("This operation is too costly.");

		/// <inheritdoc />
		public void CopyTo(TProxy[] array, int arrayIndex)
		{
			Contract.NotNull(array);
			Contract.Positive(arrayIndex);

			if (m_value.Json is not JsonArray arr)
			{
				if (m_value.Json is JsonNull)
				{
					return;
				}
				throw OperationRequiresArrayOrNull();
			}
			Contract.DoesNotOverflow(array, arrayIndex, arr.Count);

			for(int i = 0; i < arr.Count; i++)
			{
				array[arrayIndex++] = TProxy.Create(m_value[i]);
			}
		}

		/// <inheritdoc />
		bool ICollection<TProxy>.Remove(TProxy item) => throw new NotSupportedException("This operation is too costly.");

		/// <inheritdoc />
		int IList<TProxy>.IndexOf(TProxy item) => throw new NotSupportedException("This operation is too costly.");

		/// <inheritdoc />
		public int Count => m_value.Count;

		/// <inheritdoc />
		bool ICollection<TProxy>.IsReadOnly => false;

		/// <inheritdoc />
		public void Insert(int index, TProxy item) => m_value.Insert(index, item.ToJson());

		public void Insert(Index index, TProxy item) => m_value.Insert(index, item.ToJson());

		/// <inheritdoc />
		public void RemoveAt(int index) => m_value.RemoveAt(index);

		public void RemoveAt(Index index) => m_value.RemoveAt(index);

		/// <inheritdoc />
		public TProxy this[int index]
		{
			get => TProxy.Create(m_value[index]);
			set => m_value.Set(index, value.ToJson());
		}

		/// <inheritdoc />
		public override string ToString() => typeof(TValue).GetFriendlyName() + "[]";

	}

}
