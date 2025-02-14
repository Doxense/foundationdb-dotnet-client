#region Copyright (c) 2023-2025 SnowBank SAS
//
// All rights are reserved. Reproduction or transmission in whole or in part, in
// any form or by any means, electronic, mechanical or otherwise, is prohibited
// without the prior written consent of the copyright owner.
//
#endregion

namespace Doxense.Serialization.Json
{

	public interface IJsonObservableProxy
	{

		/// <summary>Returns the underlying observable value</summary>
		ObservableJsonValue GetValue();

		/// <summary>Returns the proxied JSON Value</summary>
		/// <remarks>The returned value is mutable and can be changed directly.</remarks>
		JsonValue ToJson();

	}

	public interface IJsonObservableProxy<out TValue>
	{

		/// <summary>Returns an instance of <typeparamref name="TValue"/> with the same content as this proxy.</summary>
		public TValue ToValue();

	}

	public interface IJsonObservableProxy<TValue, out TObservableProxy> : IJsonObservableProxy<TValue>
		where TObservableProxy : IJsonObservableProxy<TValue, TObservableProxy>
	{

		/// <summary>Wraps a JSON Value into a mutable proxy for type <typeparamref name="TValue"/></summary>
		static abstract TObservableProxy Create(ObservableJsonValue obj);

		/// <summary>Returns the <see cref="IJsonConverter{TValue}"/> used by proxies of this type</summary>
		static abstract IJsonConverter<TValue> Converter { get; }

	}

	public abstract record JsonObservableProxyObjectBase
		: IJsonObservableProxy, IJsonSerializable, IJsonPackable
	{

		/// <summary>Wrapped JSON Object</summary>
		protected readonly ObservableJsonValue m_obj;

		protected JsonObservableProxyObjectBase(ObservableJsonValue obj)
		{
			m_obj = obj;
		}

		public ObservableJsonValue GetValue() => m_obj;

		/// <inheritdoc />
		public void JsonSerialize(CrystalJsonWriter writer) => m_obj.Json.JsonSerialize(writer);

		/// <inheritdoc />
		public JsonValue JsonPack(CrystalJsonSettings settings, ICrystalJsonTypeResolver resolver) => m_obj.Json;

		/// <inheritdoc />
		public JsonValue ToJson() => m_obj.Json;

	}

}
