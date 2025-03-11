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

	/// <summary>Wraps a <see cref="JsonArray"/> into a typed read-only proxy that emulates an array of elements of type <typeparamref name="TValue"/></summary>
	/// <typeparam name="TValue">Emulated element type</typeparam>
	[PublicAPI]
	public readonly struct JsonReadOnlyProxyArray<TValue> : IReadOnlyList<TValue>, IJsonSerializable, IJsonPackable
	{

		private readonly JsonArray m_array;
		private readonly IJsonConverter<TValue> m_converter;

		public JsonReadOnlyProxyArray(JsonArray? array, IJsonConverter<TValue>? converter = null)
		{
			m_array = array ?? JsonArray.EmptyReadOnly;
			m_converter = converter ?? RuntimeJsonConverter<TValue>.Default;
		}

		/// <inheritdoc />
		void IJsonSerializable.JsonSerialize(CrystalJsonWriter writer) => m_array.JsonSerialize(writer);

		/// <inheritdoc />
		JsonValue IJsonPackable.JsonPack(CrystalJsonSettings settings, ICrystalJsonTypeResolver resolver) => m_array;

		public static JsonReadOnlyProxyArray<TValue> FromValue(TValue[] values, IJsonConverter<TValue>? converter = null)
		{
			converter ??= RuntimeJsonConverter<TValue>.Default;
			return new(converter.JsonPackArray(values), converter);
		}

		public TValue[] ToArray() => m_converter.JsonDeserializeArray(m_array);

		public List<TValue> ToList() => m_converter.JsonDeserializeList(m_array);

		public JsonValue ToJson() => m_array;

		public JsonWritableProxyArray<TValue> ToMutable() => new(m_array.Copy(), m_converter);

		public JsonReadOnlyProxyArray<TValue> With(Action<JsonWritableProxyArray<TValue>> modifier)
		{
			var copy = m_array.Copy();
			modifier(new(copy, m_converter));
			return new(copy.FreezeUnsafe(), m_converter);
		}

		/// <inheritdoc />
		public IEnumerator<TValue> GetEnumerator()
		{
			if (m_array != null)
			{
				foreach (var item in m_array)
				{
					yield return m_converter.Unpack(item);
				}
			}
		}

		/// <inheritdoc />
		IEnumerator IEnumerable.GetEnumerator() => this.GetEnumerator();

		/// <inheritdoc />
		public int Count => m_array.Count;

		/// <inheritdoc />
		public TValue this[int index] => m_converter.Unpack(m_array[index]);

		/// <inheritdoc />
		public override string ToString() => typeof(TValue).GetFriendlyName() + "[]";

	}

	/// <summary>Wraps a <see cref="JsonArray"/> into a typed read-only proxy that emulates an array of elements of type <typeparamref name="TValue"/></summary>
	/// <typeparam name="TValue">Emulated element type</typeparam>
	/// <typeparam name="TProxy">Corresponding <see cref="IJsonReadOnlyProxy{TValue}"/> for type <typeparamref name="TValue"/>, usually source-generated</typeparam>
	[PublicAPI]
	public readonly struct JsonReadOnlyProxyArray<TValue, TProxy> : IReadOnlyList<TProxy>, IJsonSerializable, IJsonPackable
		where TProxy : IJsonReadOnlyProxy<TValue, TProxy>
	{

		private readonly JsonValue m_value;

		public JsonReadOnlyProxyArray(JsonValue? value)
		{
			m_value = value ?? JsonNull.Null;
		}

		/// <inheritdoc />
		void IJsonSerializable.JsonSerialize(CrystalJsonWriter writer) => m_value.JsonSerialize(writer);

		/// <inheritdoc />
		JsonValue IJsonPackable.JsonPack(CrystalJsonSettings settings, ICrystalJsonTypeResolver resolver) => m_value;

		public static JsonReadOnlyProxyArray<TValue> FromValue(TValue[] values, IJsonConverter<TValue>? converter = null)
		{
			converter ??= RuntimeJsonConverter<TValue>.Default;
			return new(converter.JsonPackArray(values), converter);
		}

		public JsonValue ToJson() => m_value;

		[Pure, MethodImpl(MethodImplOptions.NoInlining)]
		private InvalidOperationException OperationRequiresObjectOrNull() => new("This operation requires a valid JSON Object");

		public IEnumerator<TProxy> GetEnumerator()
		{
			if (m_value is not JsonArray array)
			{
				if (m_value is JsonNull)
				{
					yield break;
				}
				throw OperationRequiresObjectOrNull();
			}

			foreach (var item in array)
			{
				yield return TProxy.Create(item);
			}
		}

		/// <inheritdoc />
		IEnumerator IEnumerable.GetEnumerator() => this.GetEnumerator();

		/// <inheritdoc />
		public int Count => m_value switch
		{
			JsonArray array => array.Count,
			JsonNull => 0,
			_ => throw OperationRequiresObjectOrNull()
		};

		/// <inheritdoc />
		public TProxy this[int index] => TProxy.Create(m_value[index]);

		/// <inheritdoc />
		public override string ToString() => typeof(TValue).GetFriendlyName() + "[]";

	}

}
