#region Copyright (c) 2023-2024 SnowBank SAS
//
// All rights are reserved. Reproduction or transmission in whole or in part, in
// any form or by any means, electronic, mechanical or otherwise, is prohibited
// without the prior written consent of the copyright owner.
//
#endregion

namespace Doxense.Serialization.Json
{

	/// <summary>Wraps a <see cref="JsonValue"/> into typed read-only proxy that emulates the type <typeparamref name="TValue"/></summary>
	/// <typeparam name="TValue">Emulated data type</typeparam>
	/// <remarks>
	/// <para>This interface is a marker for "wrapper types" that replicate the same set of properties and fields as <typeparamref name="TValue"/>, using a wrapped <see cref="JsonValue"/> as source.</para>
	/// </remarks>
	[PublicAPI]
	public interface IJsonReadOnlyProxy<out TValue> : IJsonSerializable, IJsonPackable
	{

		/// <summary>Returns an instance of <typeparamref name="TValue"/> with the same content as this proxy.</summary>
		public TValue ToValue();

		/// <summary>Returns the proxied JSON Value</summary>
		/// <remarks>The returned value is read-only and cannot be changed.</remarks>
		public JsonValue ToJson();

	}

	/// <summary>Wraps a <see cref="JsonValue"/> into typed read-only proxy that emulates the type <typeparamref name="TValue"/></summary>
	/// <typeparam name="TValue">Emulated data type</typeparam>
	/// <typeparam name="TReadOnlyProxy">CRTP for the type that implements this interface</typeparam>
	/// <remarks>
	/// <para>This interface is a marker for "wrapper types" that replicate the same set of properties and fields as <typeparamref name="TValue"/>, using a wrapped <see cref="JsonValue"/> as source.</para>
	/// </remarks>
	[PublicAPI]
	public interface IJsonReadOnlyProxy<TValue, out TReadOnlyProxy> : IJsonReadOnlyProxy<TValue>
		where TReadOnlyProxy : IJsonReadOnlyProxy<TValue, TReadOnlyProxy>
	{

		/// <summary>Wraps a JSON Value into a read-only proxy for type <typeparamref name="TValue"/></summary>
		static abstract TReadOnlyProxy Create(JsonValue value, IJsonConverter<TValue>? converter = null);

		/// <summary>Wraps an instance type <typeparamref name="TValue"/> into read-only proxy</summary>
		static abstract TReadOnlyProxy Create(TValue value, CrystalJsonSettings? settings = null, ICrystalJsonTypeResolver? resolver = null);

		/// <summary>Returns the <see cref="IJsonConverter{TValue}"/> used by proxies of this type</summary>
		static abstract IJsonConverter<TValue> Converter { get; }

	}

	/// <summary>Wraps a <see cref="JsonValue"/> into typed read-only proxy that emulates the type <typeparamref name="TValue"/></summary>
	/// <typeparam name="TValue">Emulated data type</typeparam>
	/// <typeparam name="TReadOnlyProxy">CRTP for the type that implements this interface</typeparam>
	/// <typeparam name="TMutableProxy">CRTP for the corresponding <see cref="IJsonMutableProxy{TValue,TMutableProxy,TReadOnlyProxy}"/> of this type</typeparam>
	/// <remarks>
	/// <para>This interface is a marker for "wrapper types" that replicate the same set of properties and fields as <typeparamref name="TValue"/>, using a wrapped <see cref="JsonValue"/> as source.</para>
	/// </remarks>
	[PublicAPI]
	public interface IJsonReadOnlyProxy<TValue, out TReadOnlyProxy, out TMutableProxy> : IJsonReadOnlyProxy<TValue, TReadOnlyProxy>
		where TReadOnlyProxy : IJsonReadOnlyProxy<TValue, TReadOnlyProxy, TMutableProxy>
		where TMutableProxy : IJsonMutableProxy<TValue, TMutableProxy, TReadOnlyProxy>
	{

		/// <summary>Returns a mutable proxy that is able to update a <i>copy</i> of the wrapped JSON value</summary>
		public TMutableProxy ToMutable();

		/// <summary>Returns an updated read-only version of this proxy, after applying a set of mutations.</summary>
		/// <param name="modifier">Handler that is passed a mutable copy of the object, which will be frozen into a new read-only object after the handler returns.</param>
		/// <returns>Updated read-only instance</returns>
		/// <remarks>
		/// <para>The original read-only object will NOT be modified.</para>
		/// </remarks>
		public TReadOnlyProxy With(Action<TMutableProxy> modifier);

	}

}
