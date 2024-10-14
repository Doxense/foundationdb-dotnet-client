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

	/// <summary>Wraps a <see cref="JsonObject"/> into a typed read-only proxy that emulates a dictionary of elements of type <typeparamref name="TValue"/></summary>
	/// <typeparam name="TValue">Emulated element type</typeparam>
	[PublicAPI]
	public readonly struct JsonReadOnlyProxyObject<TValue> : IReadOnlyDictionary<string, TValue>, IJsonSerializable, IJsonPackable
	{

		private readonly JsonValue m_value;
		private readonly IJsonConverter<TValue> m_converter;

		public JsonReadOnlyProxyObject(JsonValue? value, IJsonConverter<TValue>? converter = null)
		{
			m_value = value ?? JsonNull.Null;
			m_converter = converter ?? RuntimeJsonConverter<TValue>.Default;
		}

		[Pure, MethodImpl(MethodImplOptions.NoInlining)]
		private InvalidOperationException OperationRequiresObjectOrNull() => new("This operation requires a valid JSON Object");

		/// <inheritdoc />
		public int Count => m_value switch
		{
			JsonObject obj => obj.Count,
			JsonNull => 0,
			_ => throw OperationRequiresObjectOrNull()
		};

		/// <inheritdoc />
		public TValue this[string key] => m_converter.Unpack(m_value[key]);

		/// <inheritdoc />
		public IEnumerable<string> Keys => m_value switch
		{
			JsonObject obj => obj.Keys,
			JsonNull => Array.Empty<string>(),
			_ => throw OperationRequiresObjectOrNull()
		};

		/// <inheritdoc />
		public IEnumerable<TValue> Values => throw new NotImplementedException();

		/// <inheritdoc />
		public bool ContainsKey(string key) => m_value switch
		{
			JsonObject obj => obj.ContainsKey(key),
			JsonNull => false,
			_ => throw OperationRequiresObjectOrNull()
		};

		/// <inheritdoc />
		public bool TryGetValue(string key, [MaybeNullWhen(false)] out TValue value)
		{
			if (m_value is JsonObject obj && obj.TryGetValue(key, out var json))
			{
				value = m_converter.Unpack(json);
				return true;
			}

			value = default;
			return false;
		}

		/// <inheritdoc />
		void IJsonSerializable.JsonSerialize(CrystalJsonWriter writer) => m_value.JsonSerialize(writer);

		/// <inheritdoc />
		JsonValue IJsonPackable.JsonPack(CrystalJsonSettings settings, ICrystalJsonTypeResolver resolver) => m_value;

		public Dictionary<string, TValue> ToDictionary() => m_converter.JsonDeserializeDictionary(m_value)!;

		public JsonValue ToJson() => m_value;

		/// <inheritdoc />
		public IEnumerator<KeyValuePair<string, TValue>> GetEnumerator()
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
				yield return new(kv.Key, m_converter.Unpack(kv.Value));
			}
		}

		/// <inheritdoc />
		IEnumerator IEnumerable.GetEnumerator() => this.GetEnumerator();

		/// <inheritdoc />
		public override string ToString() => $"Dictionary<string, {typeof(TValue).GetFriendlyName()}>";

	}

	/// <summary>Wraps a <see cref="JsonObject"/> into a typed read-only proxy that emulates a dictionary of elements of type <typeparamref name="TValue"/></summary>
	/// <typeparam name="TValue">Emulated element type</typeparam>
	/// <typeparam name="TProxy">Corresponding <see cref="IJsonReadOnlyProxy{TValue}"/> for type <typeparamref name="TValue"/>, usually source-generated</typeparam>
	[PublicAPI]
	public readonly struct JsonReadOnlyProxyObject<TValue, TProxy> : IReadOnlyDictionary<string, TProxy>, IJsonSerializable, IJsonPackable
		where TProxy : IJsonReadOnlyProxy<TValue, TProxy>
	{

		private readonly JsonValue m_value;

		public JsonReadOnlyProxyObject(JsonValue? value)
		{
			m_value = value ?? JsonNull.Null;
		}

		/// <inheritdoc />
		public int Count => m_value switch
		{
			JsonObject obj => obj.Count,
			JsonNull => 0,
			_ => throw OperationRequiresObjectOrNull()
		};

		/// <inheritdoc />
		public TProxy this[string key] => TProxy.Create(m_value[key]);

		[Pure, MethodImpl(MethodImplOptions.NoInlining)]
		private InvalidOperationException OperationRequiresObjectOrNull() => new("This operation requires a valid JSON Object");

		/// <inheritdoc />
		public IEnumerable<string> Keys => m_value switch
		{
			JsonObject obj => obj.Keys,
			JsonNull => Array.Empty<string>(),
			_ => throw OperationRequiresObjectOrNull()
		};

		/// <inheritdoc />
		public IEnumerable<TProxy> Values => throw new NotImplementedException();

		/// <inheritdoc />
		public bool ContainsKey(string key) => m_value switch
		{
			JsonObject obj => obj.ContainsKey(key),
			JsonNull => false,
			_ => throw OperationRequiresObjectOrNull()
		};

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
		void IJsonSerializable.JsonSerialize(CrystalJsonWriter writer) => m_value.JsonSerialize(writer);

		/// <inheritdoc />
		JsonValue IJsonPackable.JsonPack(CrystalJsonSettings settings, ICrystalJsonTypeResolver resolver) => m_value;

		public Dictionary<string, TValue> ToDictionary() => TProxy.Converter.JsonDeserializeDictionary(m_value.AsObject());

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
