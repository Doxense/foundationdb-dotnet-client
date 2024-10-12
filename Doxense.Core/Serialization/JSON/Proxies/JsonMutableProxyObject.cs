#region Copyright (c) 2023-2024 SnowBank SAS
//
// All rights are reserved. Reproduction or transmission in whole or in part, in
// any form or by any means, electronic, mechanical or otherwise, is prohibited
// without the prior written consent of the copyright owner.
//
#endregion

namespace Doxense.Serialization.Json
{
	using System.Collections;
	using System.Diagnostics.CodeAnalysis;
	using System.Runtime.CompilerServices;

	/// <summary>Wraps a <see cref="JsonObject"/> into a typed mutable proxy that emulates a dictionary of elements of type <typeparamref name="TValue"/></summary>
	/// <typeparam name="TValue">Emulated element type</typeparam>
	[PublicAPI]
	public readonly struct JsonMutableProxyObject<TValue> : IDictionary<string, TValue>, IJsonSerializable, IJsonPackable
	{

		private readonly JsonObject m_obj;
		private readonly IJsonConverter<TValue> m_converter;

		public JsonMutableProxyObject(JsonObject? obj, IJsonConverter<TValue>? converter = null)
		{
			m_obj = obj ?? JsonObject.EmptyReadOnly;
			m_converter = converter ?? RuntimeJsonConverter<TValue>.Default;
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
		public override string ToString() => $"Dictionary<string, {typeof(TValue).GetFriendlyName()}>";
	}

	/// <summary>Wraps a <see cref="JsonObject"/> into a typed mutable proxy that emulates a dictionary of elements of type <typeparamref name="TValue"/></summary>
	/// <typeparam name="TValue">Emulated element type</typeparam>
	/// <typeparam name="TProxy">Corresponding <see cref="IJsonMutableProxy{TValue}"/> for type <typeparamref name="TValue"/>, usually source-generated</typeparam>
	[PublicAPI]
	public readonly struct JsonMutableProxyObject<TValue, TProxy> : IDictionary<string, TProxy>, IJsonSerializable, IJsonPackable
		where TProxy : IJsonMutableProxy<TValue, TProxy>
	{

		private readonly JsonValue m_value;

		public JsonMutableProxyObject(JsonValue? value) => m_value = value ?? JsonObject.EmptyReadOnly;

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
				array[arrayIndex++] = new(kv.Key, TProxy.Create(kv.Value));
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
				value = TProxy.Create(json);
				return true;
			}

			value = default;
			return false;
		}

		/// <inheritdoc />
		public TProxy this[string key]
		{
			get => TProxy.Create(m_value[key]);
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
				yield return new(kv.Key, TProxy.Create(kv.Value));
			}
		}

		/// <inheritdoc />
		IEnumerator IEnumerable.GetEnumerator() => this.GetEnumerator();

		/// <inheritdoc />
		public override string ToString() => $"Dictionary<string, {typeof(TValue).GetFriendlyName()}>";

	}

}
