#region Copyright (c) 2023-2024 SnowBank SAS
//
// All rights are reserved. Reproduction or transmission in whole or in part, in
// any form or by any means, electronic, mechanical or otherwise, is prohibited
// without the prior written consent of the copyright owner.
//
#endregion

namespace Doxense.Serialization.Json
{

	/// <summary>Wraps a <see cref="JsonValue"/> into typed mutable proxy that emulates the type <typeparamref name="TValue"/></summary>
	/// <typeparam name="TValue">Emulated data type</typeparam>
	/// <remarks>
	/// <para>This interface is a marker for "wrapper types" that replicate the same set of properties and fields as <typeparamref name="TValue"/>, using a wrapped <see cref="JsonValue"/> as source.</para>
	/// </remarks>
	[PublicAPI]
	public interface IJsonMutableProxy<out TValue> : IJsonSerializable, IJsonPackable
	{

		/// <summary>Returns an instance of <typeparamref name="TValue"/> with the same content as this proxy.</summary>
		public TValue ToValue();

		/// <summary>Returns the proxied JSON Value</summary>
		/// <remarks>The returned value is mutable and can be changed directly.</remarks>
		JsonValue ToJson();

	}

	/// <summary>Wraps a <see cref="JsonValue"/> into typed mutable proxy that emulates the type <typeparamref name="TValue"/></summary>
	/// <typeparam name="TValue">Emulated data type</typeparam>
	/// <typeparam name="TMutableProxy">CRTP for the type that implements this interface</typeparam>
	/// <remarks>
	/// <para>This interface is a marker for "wrapper types" that replicate the same set of properties and fields as <typeparamref name="TValue"/>, using a wrapped <see cref="JsonValue"/> as source.</para>
	/// </remarks>
	[PublicAPI]
	public interface IJsonMutableProxy<TValue, out TMutableProxy> : IJsonMutableProxy<TValue>
		where TMutableProxy : IJsonMutableProxy<TValue, TMutableProxy>
	{

		/// <summary>Wraps a JSON Value into a mutable proxy for type <typeparamref name="TValue"/></summary>
		static abstract TMutableProxy Create(JsonValue obj, IJsonConverter<TValue>? converter = null);

		/// <summary>Wraps an instance type <typeparamref name="TValue"/> into mutable proxy</summary>
		static abstract TMutableProxy Create(TValue value, CrystalJsonSettings? settings = null, ICrystalJsonTypeResolver? resolver = null);

		/// <summary>Returns the <see cref="IJsonConverter{TValue}"/> used by proxies of this type</summary>
		static abstract IJsonConverter<TValue> Converter { get; }

	}

	/// <summary>Wraps a <see cref="JsonValue"/> into typed mutable proxy that emulates the type <typeparamref name="TValue"/></summary>
	/// <typeparam name="TValue">Emulated data type</typeparam>
	/// <typeparam name="TMutableProxy">CRTP for the type that implements this interface</typeparam>
	/// <typeparam name="TReadOnlyProxy">CRTP for the corresponding <see cref="IJsonReadOnlyProxy{TValue,TReadOnlyProxy,TMutableProxy}"/> of this type</typeparam>
	/// <remarks>
	/// <para>This interface is a marker for "wrapper types" that replicate the same set of properties and fields as <typeparamref name="TValue"/>, using a wrapped <see cref="JsonValue"/> as source.</para>
	/// </remarks>
	[PublicAPI]
	public interface IJsonMutableProxy<TValue, out TMutableProxy, out TReadOnlyProxy> : IJsonMutableProxy<TValue, TMutableProxy>
		where TMutableProxy : IJsonMutableProxy<TValue, TMutableProxy, TReadOnlyProxy>
		where TReadOnlyProxy : IJsonReadOnlyProxy<TValue, TReadOnlyProxy, TMutableProxy>
	{

		/// <summary>Converts this mutable proxy, back into the equivalent read-only proxy.</summary>
		/// <remarks>Any future changes to this mutable proxy will not impact the returned value</remarks>
		TReadOnlyProxy ToReadOnly();

	}

}
