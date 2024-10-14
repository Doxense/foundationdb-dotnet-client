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
