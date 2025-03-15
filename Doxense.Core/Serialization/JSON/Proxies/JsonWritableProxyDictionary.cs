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

		private readonly JsonObject m_obj;

		private readonly IJsonConverter<TValue> m_converter;
	
		private readonly IJsonProxyNode? m_parent;

		private readonly JsonPathSegment m_segment;

		private readonly int m_depth;

		public JsonWritableProxyDictionary(JsonObject? obj, IJsonConverter<TValue>? converter = null, IJsonProxyNode? parent = null, JsonPathSegment segment = default)
		{
			m_obj = obj ?? JsonObject.EmptyReadOnly;
			m_converter = converter ?? RuntimeJsonConverter<TValue>.Default;
			m_parent = parent;
			m_segment = segment;
			m_depth = (parent?.Depth ?? -1) + 1;
		}

		/// <inheritdoc />
		JsonType IJsonProxyNode.Type => JsonType.Object;

		/// <inheritdoc />
		IJsonProxyNode? IJsonProxyNode.Parent => m_parent;

		/// <inheritdoc />
		JsonPathSegment IJsonProxyNode.Segment => m_segment;

		/// <inheritdoc />
		int IJsonProxyNode.Depth => m_depth;

		/// <inheritdoc />
		void IJsonProxyNode.WritePath(ref JsonPathBuilder builder)
		{
			m_parent?.WritePath(ref builder);
			builder.Append(m_segment);
		}

		/// <inheritdoc />
		void ICollection<KeyValuePair<string, TValue>>.Add(KeyValuePair<string, TValue> item) => Add(item.Key, item.Value);

		/// <inheritdoc />
		public void Clear()
		{
			m_obj.Clear();
		}

		/// <inheritdoc />
		bool ICollection<KeyValuePair<string, TValue>>.Contains(KeyValuePair<string, TValue> item) => throw new NotSupportedException("This operation is too costly.");

		/// <inheritdoc />
		public void CopyTo(KeyValuePair<string, TValue>[] array, int arrayIndex)
		{
			Contract.NotNull(array);
			Contract.Positive(arrayIndex);
			Contract.DoesNotOverflow(array, arrayIndex, m_obj.Count);

			foreach (var kv in m_obj)
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
			if (!m_obj.TryGetValue(key, out var json))
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
			get => m_converter.Unpack(m_obj[key]);
			set => m_obj[key] = m_converter.Pack(value);
		}

		/// <inheritdoc />
		public ICollection<string> Keys => m_obj.Keys;

		/// <inheritdoc />
		public ICollection<TValue> Values => throw new NotImplementedException();

		/// <inheritdoc />
		void IJsonSerializable.JsonSerialize(CrystalJsonWriter writer) => m_obj.JsonSerialize(writer);

		/// <inheritdoc />
		JsonValue IJsonPackable.JsonPack(CrystalJsonSettings settings, ICrystalJsonTypeResolver resolver) => m_obj;

		public Dictionary<string, TValue> ToDictionary() => m_converter.JsonDeserializeDictionary(m_obj);

		public JsonObject ToJson() => m_obj;

		/// <inheritdoc />
		public IEnumerator<KeyValuePair<string, TValue>> GetEnumerator()
		{
			foreach (var kv in m_obj)
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

		private readonly JsonValue m_value;

		private readonly IJsonProxyNode? m_parent;

		private readonly JsonPathSegment m_segment;

		private readonly int m_depth;

		public JsonWritableProxyDictionary(JsonValue? value, IJsonProxyNode? parent = null, JsonPathSegment segment = default)
		{
			m_value = value ?? JsonObject.EmptyReadOnly;
			m_parent = parent;
			m_segment = segment;
			m_depth = (parent?.Depth ?? -1) + 1;
		}

		/// <inheritdoc />
		IJsonProxyNode? IJsonProxyNode.Parent => m_parent;

		/// <inheritdoc />
		JsonPathSegment IJsonProxyNode.Segment => m_segment;

		/// <inheritdoc />
		int IJsonProxyNode.Depth => m_depth;

		/// <inheritdoc />
		void IJsonProxyNode.WritePath(ref JsonPathBuilder builder)
		{
			m_parent?.WritePath(ref builder);
			builder.Append(m_segment);
		}

		[Pure, MethodImpl(MethodImplOptions.NoInlining)]
		private InvalidOperationException OperationRequiresObjectOrNull() => new("This operation requires a valid JSON Object");

		/// <inheritdoc />
		void ICollection<KeyValuePair<string, TProxy>>.Add(KeyValuePair<string, TProxy> item) => Add(item.Key, item.Value);

		/// <inheritdoc />
		public void Clear()
		{
			if (m_value is not JsonObject obj) throw OperationRequiresObjectOrNull();
			obj.Clear();
		}

		/// <inheritdoc />
		bool ICollection<KeyValuePair<string, TProxy>>.Contains(KeyValuePair<string, TProxy> item) => throw new NotSupportedException("This operation is too costly.");

		/// <inheritdoc />
		public void CopyTo(KeyValuePair<string, TProxy>[] array, int arrayIndex)
		{
			Contract.NotNull(array);
			Contract.Positive(arrayIndex);

			if (m_value is not JsonObject obj)
			{
				if (m_value is JsonNull) return;
				throw OperationRequiresObjectOrNull();
			}

			Contract.DoesNotOverflow(array, arrayIndex, obj.Count);

			foreach (var kv in obj)
			{
				array[arrayIndex++] = new(kv.Key, TProxy.Create(kv.Value, parent: this, segment: new(kv.Key))); //BUGBUG
			}
		}

		/// <inheritdoc />
		bool ICollection<KeyValuePair<string, TProxy>>.Remove(KeyValuePair<string, TProxy> item) => throw new NotSupportedException("This operation is too costly.");

		public int Count => m_value is JsonObject obj ? obj.Count : m_value is JsonNull ? 0 : throw OperationRequiresObjectOrNull();

		/// <inheritdoc />
		bool ICollection<KeyValuePair<string, TProxy>>.IsReadOnly => false;

		/// <inheritdoc />
		public void Add(string key, TProxy value)
		{
			if (m_value is not JsonObject obj) throw OperationRequiresObjectOrNull();
			obj.Add(key, value.ToJson());
		}

		/// <inheritdoc />
		public bool ContainsKey(string key) => m_value is JsonObject obj ? obj.ContainsKey(key) : m_value is JsonNull ? false : throw OperationRequiresObjectOrNull();

		/// <inheritdoc />
		public bool Remove(string key)
		{
			if (m_value is not JsonObject obj) throw OperationRequiresObjectOrNull();
			return obj.Remove(key);
		}

		/// <inheritdoc />
		public bool TryGetValue(string key, [MaybeNullWhen(false)] out TProxy value)
		{
			if (m_value is JsonObject obj && obj.TryGetValue(key, out var json))
			{
				value = TProxy.Create(json, parent: this, segment: new(key)); //BUGBUG
				return true;
			}

			value = default;
			return false;
		}

		/// <inheritdoc />
		public TProxy this[string key]
		{
			get => TProxy.Create(m_value[key], parent: this, segment: new(key)); //BUGBUG
			set => m_value[key] = value.ToJson();
		}

		/// <inheritdoc />
		public ICollection<string> Keys => m_value is JsonObject obj ? obj.Keys : m_value is JsonNull ? Array.Empty<string>() : throw OperationRequiresObjectOrNull();

		/// <inheritdoc />
		public ICollection<TProxy> Values => throw new NotImplementedException();

		/// <inheritdoc />
		void IJsonSerializable.JsonSerialize(CrystalJsonWriter writer) => m_value.JsonSerialize(writer);

		/// <inheritdoc />
		JsonValue IJsonPackable.JsonPack(CrystalJsonSettings settings, ICrystalJsonTypeResolver resolver) => m_value;

		public Dictionary<string, TValue> ToDictionary() => TProxy.Converter.JsonDeserializeDictionary(m_value)!;

		public JsonValue ToJson() => m_value;

		/// <inheritdoc />
		public IEnumerator<KeyValuePair<string, TProxy>> GetEnumerator()
		{
			if (m_value is not JsonObject obj)
			{
				if (m_value is JsonNull)
				{
					yield break;
				}
				throw OperationRequiresObjectOrNull();
			}
			foreach (var kv in obj)
			{
				yield return new(kv.Key, TProxy.Create(kv.Value, parent: this, segment: new (kv.Key))); //BUGBUG
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
