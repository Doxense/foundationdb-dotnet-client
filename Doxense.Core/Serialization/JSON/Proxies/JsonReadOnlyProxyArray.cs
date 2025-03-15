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

		private readonly ObservableJsonValue m_array;
		private readonly IJsonConverter<TValue> m_converter;

		public JsonReadOnlyProxyArray(ObservableJsonValue array, IJsonConverter<TValue>? converter = null)
		{
			m_array = array;
			m_converter = converter ?? RuntimeJsonConverter<TValue>.Default;
		}

		/// <inheritdoc />
		void IJsonSerializable.JsonSerialize(CrystalJsonWriter writer) => m_array.ToJson().JsonSerialize(writer);

		/// <inheritdoc />
		JsonValue IJsonPackable.JsonPack(CrystalJsonSettings settings, ICrystalJsonTypeResolver resolver) => m_array.ToJson();

		public TValue[] ToArray() => m_converter.JsonDeserializeArray(m_array.ToJson().AsArrayOrEmpty());

		public List<TValue> ToList() => m_converter.JsonDeserializeList(m_array.ToJson().AsArrayOrEmpty());

		public JsonValue ToJson() => m_array.ToJson();

		public JsonWritableProxyArray<TValue> ToMutable() => new(m_array.GetJsonUnsafe().AsArrayOrDefault()?.Copy(), m_converter);

		public JsonReadOnlyProxyArray<TValue> With(Action<JsonWritableProxyArray<TValue>> modifier)
		{
			var copy = m_array.GetJsonUnsafe().AsArrayOrEmpty().Copy();
			modifier(new(copy, m_converter));
			return new(m_array.Visit(copy.FreezeUnsafe()), m_converter);
		}

		/// <inheritdoc />
		public IEnumerator<TValue> GetEnumerator()
		{
			// we cannot know how the result will be used, so mark this a full "Value" read.

			var value = m_array.ToJson();
			if (value is not JsonArray array)
			{
				if (value is JsonNull)
				{
					yield break;
				}
				throw new InvalidOperationException("Cannot iterate a non-array");
			}
			foreach(var item in array)
			{
				yield return m_converter.Unpack(item);
			}
		}

		/// <inheritdoc />
		IEnumerator IEnumerable.GetEnumerator() => this.GetEnumerator();

		/// <inheritdoc />
		public int Count => m_array.Count;

		/// <summary>Tests if the array is present.</summary>
		/// <returns><c>false</c> if the wrapped JSON value is null or empty; otherwise, <c>true</c>.</returns>
		public bool Exists() => m_array.Exists();

		/// <inheritdoc />
		public TValue this[int index] => m_array.TryGetValue(index, m_converter, out var value) ? value : m_converter.Unpack(JsonNull.Error);

		public TValue this[Index index] => m_array.TryGetValue(index, m_converter, out var value) ? value : m_converter.Unpack(JsonNull.Error);

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

		private readonly ObservableJsonValue m_value;

		public JsonReadOnlyProxyArray(ObservableJsonValue value)
		{
			m_value = value;
		}

		/// <inheritdoc />
		void IJsonSerializable.JsonSerialize(CrystalJsonWriter writer) => m_value.ToJson().JsonSerialize(writer);

		/// <inheritdoc />
		JsonValue IJsonPackable.JsonPack(CrystalJsonSettings settings, ICrystalJsonTypeResolver resolver) => m_value.ToJson();

		public JsonValue ToJson() => m_value.ToJson();

		[Pure, MethodImpl(MethodImplOptions.NoInlining)]
		private static InvalidOperationException OperationRequiresArrayOrNull() => new("This operation requires a valid JSON Array");

		public IEnumerator<TProxy> GetEnumerator()
		{
			// the result would change if there are additional items in the array
			if (!m_value.TryGetCount(out var count))
			{
				if (m_value.GetJsonUnsafe())
				{
					yield break;
				}
				throw OperationRequiresArrayOrNull();
			}

			for(int i = 0; i < count; i++)
			{
				yield return TProxy.Create(m_value[i]);
			}
		}

		/// <inheritdoc />
		IEnumerator IEnumerable.GetEnumerator() => this.GetEnumerator();

		/// <summary>Tests if the array is present.</summary>
		/// <returns><c>false</c> if the wrapped JSON value is null or empty; otherwise, <c>true</c>.</returns>
		public bool Exists() => m_value.Exists();

		/// <inheritdoc />
		public int Count => m_value.Count;

		/// <inheritdoc />
		public TProxy this[int index] => TProxy.Create(m_value.Get(index));

		public TProxy this[Index index] => TProxy.Create(m_value.Get(index));

		/// <inheritdoc />
		public override string ToString() => typeof(TValue).GetFriendlyName() + "[]";

	}

}
