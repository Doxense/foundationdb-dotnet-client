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
	using System.Collections;

	/// <summary>Wraps a <see cref="JsonObject"/> into a typed mutable proxy that emulates a dictionary of elements of type <typeparamref name="TValue"/></summary>
	/// <typeparam name="TValue">Emulated element type</typeparam>
	[PublicAPI]
	[DebuggerDisplay("{ToString(),nq}, Count={Count}")]
	public readonly struct JsonWritableProxyDictionary<TValue> : IDictionary<string, TValue>, IJsonProxyNode, IJsonSerializable, IJsonPackable
	{

		private readonly MutableJsonValue m_obj;

		private readonly IJsonConverter<TValue> m_converter;
	
		public JsonWritableProxyDictionary(MutableJsonValue obj, IJsonConverter<TValue>? converter = null, IJsonProxyNode? parent = null, JsonPathSegment segment = default)
		{
			m_obj = obj;
			m_converter = converter ?? RuntimeJsonConverter<TValue>.Default;
		}

		/// <inheritdoc />
		JsonType IJsonProxyNode.Type => JsonType.Object;

		/// <inheritdoc />
		IJsonProxyNode? IJsonProxyNode.Parent => m_obj.Parent;

		/// <inheritdoc />
		JsonPathSegment IJsonProxyNode.Segment => m_obj.Segment;

		/// <inheritdoc />
		int IJsonProxyNode.Depth => m_obj.Depth;

		/// <inheritdoc />
		void IJsonProxyNode.WritePath(ref JsonPathBuilder builder)
		{
			m_obj.WritePath(ref builder);
		}

		/// <inheritdoc />
		void ICollection<KeyValuePair<string, TValue>>.Add(KeyValuePair<string, TValue> item) => Add(item.Key, item.Value);

		/// <inheritdoc />
		public void Clear() => m_obj.Clear();

		/// <inheritdoc />
		bool ICollection<KeyValuePair<string, TValue>>.Contains(KeyValuePair<string, TValue> item) => throw new NotSupportedException("This operation is too costly.");


		[Pure, MethodImpl(MethodImplOptions.NoInlining)]
		private static InvalidOperationException OperationRequiresObjectOrNull() => new("This operation requires a valid JSON Object");

		/// <inheritdoc />
		public void CopyTo(KeyValuePair<string, TValue>[] array, int arrayIndex)
		{
			Contract.NotNull(array);
			Contract.Positive(arrayIndex);
			Contract.DoesNotOverflow(array, arrayIndex, m_obj.Count);

			if (m_obj.Json is not JsonObject obj)
			{
				if (m_obj.Json.IsNullOrMissing())
				{
					return;
				}
				throw OperationRequiresObjectOrNull();
			}
			foreach (var kv in obj)
			{
				array[arrayIndex++] = new(kv.Key, m_converter.Unpack(kv.Value));
			}
		}

		/// <inheritdoc />
		public bool Remove(KeyValuePair<string, TValue> item) => throw new NotSupportedException("This operation is too costly.");

		public int Count => m_obj.Count;

		/// <inheritdoc />
		bool ICollection<KeyValuePair<string, TValue>>.IsReadOnly => false;

		/// <inheritdoc />
		public void Add(string key, TValue value) => m_obj.Add(key, m_converter.Pack(value));

		/// <inheritdoc />
		public bool ContainsKey(string key) => m_obj.ContainsKey(key);

		/// <inheritdoc />
		public bool Remove(string key) => m_obj.Remove(key);

		/// <inheritdoc />
		public bool TryGetValue(string key, [MaybeNullWhen(false)] out TValue value)
		{
			if (!m_obj.Json.TryGetValue(key, out var json))
			{
				value = default;
				return false;
			}

			value = m_converter.Unpack(json);
			return true;
		}

		/// <inheritdoc />
		public TValue this[string key]
		{
			get => m_converter.Unpack(m_obj.Json[key]);
			set => m_obj.Set(key, m_converter.Pack(value));
		}

		/// <inheritdoc />
		public ICollection<string> Keys => m_obj.Json switch
		{
			JsonObject obj => obj.Keys,
			JsonNull => [ ],
			_ => throw OperationRequiresObjectOrNull(),
		};

		/// <inheritdoc />
		public ICollection<TValue> Values => throw new NotImplementedException();

		/// <inheritdoc />
		void IJsonSerializable.JsonSerialize(CrystalJsonWriter writer) => m_obj.Json.JsonSerialize(writer);

		/// <inheritdoc />
		JsonValue IJsonPackable.JsonPack(CrystalJsonSettings settings, ICrystalJsonTypeResolver resolver) => m_obj.Json;

		public Dictionary<string, TValue> ToDictionary() => m_obj.Json switch
		{
			JsonObject obj => m_converter.JsonDeserializeDictionary(obj),
			JsonNull => [ ],
			_ => throw OperationRequiresObjectOrNull(),
		};

		public JsonValue ToJson() => m_obj.Json;

		/// <inheritdoc />
		public IEnumerator<KeyValuePair<string, TValue>> GetEnumerator()
		{
			if (m_obj.Json is not JsonObject obj)
			{
				if (m_obj.Json is JsonNull)
				{
					yield break;
				}

				throw OperationRequiresObjectOrNull();
			}

			foreach (var kv in obj)
			{
				yield return new(kv.Key, m_converter.Unpack(kv.Value));
			}
		}

		/// <inheritdoc />
		IEnumerator IEnumerable.GetEnumerator() => this.GetEnumerator();

		/// <inheritdoc />
		public override string ToString() => $"Dictionary<string, {typeof(TValue).GetFriendlyName()}>({this.GetPath()})";
	}

	/// <summary>Wraps a <see cref="JsonObject"/> into a typed mutable proxy that emulates a dictionary of elements of type <typeparamref name="TValue"/></summary>
	/// <typeparam name="TValue">Emulated element type</typeparam>
	/// <typeparam name="TProxy">Corresponding <see cref="IJsonWritableProxy{TValue}"/> for type <typeparamref name="TValue"/>, usually source-generated</typeparam>
	[PublicAPI]
	public readonly struct JsonWritableProxyDictionary<TValue, TProxy> : IDictionary<string, TProxy>, IJsonProxyNode, IJsonSerializable, IJsonPackable
		where TProxy : IJsonWritableProxy<TValue, TProxy>
	{

		private readonly MutableJsonValue m_value;

		public JsonWritableProxyDictionary(MutableJsonValue value, IJsonProxyNode? parent = null, JsonPathSegment segment = default)
		{
			m_value = value;
		}

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

		[Pure, MethodImpl(MethodImplOptions.NoInlining)]
		private static InvalidOperationException OperationRequiresObjectOrNull() => new("This operation requires a valid JSON Object");

		/// <inheritdoc />
		void ICollection<KeyValuePair<string, TProxy>>.Add(KeyValuePair<string, TProxy> item) => Add(item.Key, item.Value);

		/// <inheritdoc />
		public void Clear() => m_value.Clear();

		/// <inheritdoc />
		bool ICollection<KeyValuePair<string, TProxy>>.Contains(KeyValuePair<string, TProxy> item) => throw new NotSupportedException("This operation is too costly.");

		/// <inheritdoc />
		public void CopyTo(KeyValuePair<string, TProxy>[] array, int arrayIndex)
		{
			Contract.NotNull(array);
			Contract.Positive(arrayIndex);

			if (m_value.Json is not JsonObject obj)
			{
				if (m_value.Json is JsonNull) return;
				throw OperationRequiresObjectOrNull();
			}

			Contract.DoesNotOverflow(array, arrayIndex, obj.Count);

			foreach (var k in obj.Keys)
			{
				array[arrayIndex++] = new(k, TProxy.Create(m_value[k]));
			}
		}

		/// <inheritdoc />
		bool ICollection<KeyValuePair<string, TProxy>>.Remove(KeyValuePair<string, TProxy> item) => throw new NotSupportedException("This operation is too costly.");

		public int Count => m_value.Count;

		/// <inheritdoc />
		bool ICollection<KeyValuePair<string, TProxy>>.IsReadOnly => false;

		/// <inheritdoc />
		public void Add(string key, TProxy value) => m_value.Add(key, value.ToJson());

		/// <inheritdoc />
		public bool ContainsKey(string key) => m_value.ContainsKey(key);

		/// <inheritdoc />
		public bool Remove(string key) => m_value.Remove(key);

		/// <inheritdoc />
		public bool TryGetValue(string key, [MaybeNullWhen(false)] out TProxy value)
		{
			if (m_value.TryGetValue(key, out var json))
			{
				value = TProxy.Create(json); //BUGBUG
				return true;
			}

			value = default;
			return false;
		}

		/// <inheritdoc />
		public TProxy this[string key]
		{
			get => TProxy.Create(m_value[key]);
			set => m_value.Set(key, value.ToJson());
		}

		public TProxy this[ReadOnlyMemory<char> key]
		{
			get => TProxy.Create(m_value[key]);
			set => m_value.Set(key, value.ToJson());
		}

		/// <inheritdoc />
		public ICollection<string> Keys => m_value.Json switch
		{
			JsonObject obj => obj.Keys,
			JsonNull => [ ],
			_ => throw OperationRequiresObjectOrNull(),
		};

		/// <inheritdoc />
		public ICollection<TProxy> Values => throw new NotImplementedException();

		/// <inheritdoc />
		void IJsonSerializable.JsonSerialize(CrystalJsonWriter writer) => m_value.Json.JsonSerialize(writer);

		/// <inheritdoc />
		JsonValue IJsonPackable.JsonPack(CrystalJsonSettings settings, ICrystalJsonTypeResolver resolver) => m_value.Json;

		public Dictionary<string, TValue> ToDictionary() => m_value.Json switch
		{
			JsonObject obj => TProxy.Converter.JsonDeserializeDictionary(obj),
			JsonNull => [ ],
			_ => throw OperationRequiresObjectOrNull(),
		};

		public JsonValue ToJson() => m_value.Json;

		/// <inheritdoc />
		public IEnumerator<KeyValuePair<string, TProxy>> GetEnumerator()
		{
			if (m_value.Json is not JsonObject obj)
			{
				if (m_value.Json is JsonNull)
				{
					yield break;
				}
				throw OperationRequiresObjectOrNull();
			}
			foreach (var k in obj.Keys)
			{
				yield return new(k, TProxy.Create(m_value[k]));
			}
		}

		/// <inheritdoc />
		IEnumerator IEnumerable.GetEnumerator() => this.GetEnumerator();

		/// <inheritdoc />
		public override string ToString() => $"Dictionary<string, {typeof(TValue).GetFriendlyName()}>";

		/// <inheritdoc />
		public JsonType Type { get; }

	}

}
