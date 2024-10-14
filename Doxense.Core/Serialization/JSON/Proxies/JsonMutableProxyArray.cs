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
	public readonly struct JsonMutableProxyArray<TValue> : IList<TValue>, IJsonSerializable, IJsonPackable
	{

		private readonly JsonArray m_array;
		private readonly IJsonConverter<TValue> m_converter;

		public JsonMutableProxyArray(JsonArray? array, IJsonConverter<TValue>? converter = null)
		{
			m_array = array ?? [ ];
			m_converter = converter ?? RuntimeJsonConverter<TValue>.Default;
		}
	
		TValue[] ToArray() => m_converter.JsonDeserializeArray(m_array);

		List<TValue> ToList() => m_converter.JsonDeserializeList(m_array);

		/// <inheritdoc />
		void IJsonSerializable.JsonSerialize(CrystalJsonWriter writer) => m_array.JsonSerialize(writer);

		/// <inheritdoc />
		JsonValue IJsonPackable.JsonPack(CrystalJsonSettings settings, ICrystalJsonTypeResolver resolver) => m_array;

		public static JsonMutableProxyArray<TValue> FromValue(TValue[] values, IJsonConverter<TValue>? converter = null)
		{
			converter ??= RuntimeJsonConverter<TValue>.Default;
			return new(converter.JsonPackArray(values), converter);
		}

		public JsonArray ToJson() => m_array;

		public JsonReadOnlyProxyArray<TValue> ToReadOnly() => new(m_array.ToReadOnly(), m_converter);

		/// <inheritdoc />
		public IEnumerator<TValue> GetEnumerator()
		{
			foreach (var item in m_array)
			{
				yield return m_converter.Unpack(item);
			}
		}

		/// <inheritdoc />
		IEnumerator IEnumerable.GetEnumerator() => this.GetEnumerator();

		/// <inheritdoc />
		public void Add(TValue item) => m_array.Add(m_converter.Pack(item));

		/// <inheritdoc />
		public void Clear() => m_array.Clear();

		/// <inheritdoc />
		bool ICollection<TValue>.Contains(TValue item) => throw new NotSupportedException("This operation is too costly.");

		/// <inheritdoc />
		public void CopyTo(TValue[] array, int arrayIndex)
		{
			Contract.NotNull(array);
			Contract.Positive(arrayIndex);
			Contract.DoesNotOverflow(array, arrayIndex, m_array.Count);

			foreach (var item in m_array)
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
		public void Insert(int index, TValue item) => m_array.Insert(index, m_converter.Pack(item));

		/// <inheritdoc />
		public void RemoveAt(int index) => m_array.RemoveAt(index);

		/// <inheritdoc />
		public TValue this[int index]
		{
			get => m_converter.Unpack(m_array[index]);
			set => m_array[index] = m_converter.Pack(value);
		}

		/// <inheritdoc />
		public override string ToString() => typeof(TValue).GetFriendlyName() + "[]";

	}

	/// <summary>Wraps a <see cref="JsonArray"/> into a typed mutable proxy that emulates an array of elements of type <typeparamref name="TValue"/></summary>
	/// <typeparam name="TValue">Emulated element type</typeparam>
	/// <typeparam name="TProxy">Corresponding <see cref="IJsonMutableProxy{TValue}"/> for type <typeparamref name="TValue"/>, usually source-generated</typeparam>
	[PublicAPI]
	public sealed class JsonMutableProxyArray<TValue, TProxy> : IList<TProxy>, IJsonSerializable, IJsonPackable
		where TProxy : IJsonMutableProxy<TValue, TProxy>
	{

		private readonly JsonValue m_value;

		public JsonMutableProxyArray(JsonValue? value, IJsonConverter<TValue>? converter = null)
		{
			m_value = value ?? JsonNull.Null;
		}

		TValue[] ToArray() => TProxy.Converter.JsonDeserializeArray(m_value)!;

		List<TValue> ToList() => TProxy.Converter.JsonDeserializeList(m_value)!;

		/// <inheritdoc />
		void IJsonSerializable.JsonSerialize(CrystalJsonWriter writer) => m_value.JsonSerialize(writer);

		/// <inheritdoc />
		JsonValue IJsonPackable.JsonPack(CrystalJsonSettings settings, ICrystalJsonTypeResolver resolver) => m_value;

		public static JsonMutableProxyArray<TValue> FromValue(TValue[] values, IJsonConverter<TValue>? converter = null)
		{
			converter ??= RuntimeJsonConverter<TValue>.Default;
			return new(converter.JsonPackArray(values), converter);
		}

		public JsonValue ToJson() => m_value;

		[Pure, MethodImpl(MethodImplOptions.NoInlining)]
		private InvalidOperationException OperationRequiresArrayOrNull() => new("This operation requires a valid JSON Array");

		/// <inheritdoc />
		public IEnumerator<TProxy> GetEnumerator()
		{
			if (m_value is not JsonArray array)
			{
				if (m_value is JsonNull)
				{
					yield break;
				}
				throw OperationRequiresArrayOrNull();
			}

			foreach (var item in array)
			{
				yield return TProxy.Create(item);
			}
		}

		/// <inheritdoc />
		IEnumerator IEnumerable.GetEnumerator() => this.GetEnumerator();

		/// <inheritdoc />
		public void Add(TProxy item)
		{
			if (m_value is not JsonArray array) throw OperationRequiresArrayOrNull();
			array.Add(item.ToJson());
		}

		public void Add(TValue item)
		{
			if (m_value is not JsonArray array) throw OperationRequiresArrayOrNull();
			array.Add(TProxy.Create(item).ToJson());
		}

		/// <inheritdoc />
		public void Clear()
		{
			if (m_value is not JsonArray array) throw OperationRequiresArrayOrNull();
			array.Clear();
		}

		/// <inheritdoc />
		bool ICollection<TProxy>.Contains(TProxy item) => throw new NotSupportedException("This operation is too costly.");

		/// <inheritdoc />
		public void CopyTo(TProxy[] array, int arrayIndex)
		{
			Contract.NotNull(array);
			Contract.Positive(arrayIndex);

			if (m_value is not JsonArray arr) throw OperationRequiresArrayOrNull();
			Contract.DoesNotOverflow(array, arrayIndex, arr.Count);

			foreach (var item in arr)
			{
				array[arrayIndex++] = TProxy.Create(item);
			}
		}

		/// <inheritdoc />
		bool ICollection<TProxy>.Remove(TProxy item) => throw new NotSupportedException("This operation is too costly.");

		/// <inheritdoc />
		int IList<TProxy>.IndexOf(TProxy item) => throw new NotSupportedException("This operation is too costly.");

		/// <inheritdoc />
		public int Count { get; }

		/// <inheritdoc />
		bool ICollection<TProxy>.IsReadOnly => false;

		/// <inheritdoc />
		public void Insert(int index, TProxy item)
		{
			if (m_value is not JsonArray array) throw OperationRequiresArrayOrNull();
			array.Insert(index, item.ToJson());
		}

		/// <inheritdoc />
		public void RemoveAt(int index)
		{
			if (m_value is not JsonArray array) throw OperationRequiresArrayOrNull();
			array.RemoveAt(index);
		}

		/// <inheritdoc />
		public TProxy this[int index]
		{
			get => TProxy.Create(m_value[index]);
			set => m_value[index] = value.ToJson();
		}

		/// <inheritdoc />
		public override string ToString() => typeof(TValue).GetFriendlyName() + "[]";

	}

}
