#region Copyright (c) 2023-2024 SnowBank SAS
//
// All rights are reserved. Reproduction or transmission in whole or in part, in
// any form or by any means, electronic, mechanical or otherwise, is prohibited
// without the prior written consent of the copyright owner.
//
#endregion

namespace Doxense.Serialization.Json
{

	/// <summary>Wraps a <see cref="JsonObject"/> into typed read-only view that emulates type <typeparamref name="TValue"/></summary>
	/// <typeparam name="TValue">Emulated data type</typeparam>
	/// <typeparam name="TReadOnly">CRTP that points to the type that implements this interface</typeparam>
	/// <typeparam name="TMutable">CRTP that points to the associated <see cref="IJsonMutable{TValue,TMutable}"/> type for the same <typeparamref name="TValue"/></typeparam>
	/// <remarks>
	/// <para>This interface is a marker for "wrapper types" that replicate the same set of properties and fields as <typeparamref name="TValue"/>, using a wrapped <see cref="JsonObject"/> as source.</para>
	/// </remarks>
	public interface IJsonReadOnly<TValue, out TReadOnly, out TMutable> : IJsonSerializable, IJsonPackable
		where TReadOnly : IJsonReadOnly<TValue, TReadOnly, TMutable>
		where TMutable : IJsonMutable<TValue, TMutable>
	{

		/// <summary>Returns a mutable JSON version of an instance of <typeparamref name="TValue"/></summary>
		static abstract TReadOnly FromValue(TValue value);

		/// <summary>Returns an instance of <typeparamref name="TValue"/> with the same content as this object.</summary>
		public TValue ToValue();

		/// <summary>Returns the wrapped JSON Object</summary>
		/// <remarks>This object is read-only and cannot be modified.</remarks>
		public JsonObject ToJson();

		/// <summary>Returns a mutable instance that is able to mutate a <i>copy</i> of the current object</summary>
		public TMutable ToMutable();

		/// <summary>Returns an updated read-only version of this object, after applying a set of mutations.</summary>
		/// <param name="modifier">Modifier that is passed a mutable version of the object, that will then be frozen into a new read-only object.</param>
		/// <returns>Updated read-only instance</returns>
		public TReadOnly With(Action<TMutable> modifier);

	}

	/// <summary>Wraps a <see cref="JsonObject"/> into typed mutable view that emulates type <typeparamref name="TValue"/></summary>
	/// <typeparam name="TValue">Emulated data type</typeparam>
	/// <typeparam name="TMutable">CRTP</typeparam>
	/// <remarks>
	/// <para>This interface is a marker for "wrapper types" that replicate the same set of properties and fields as <typeparamref name="TValue"/>, using a wrapped <see cref="JsonObject"/> as source.</para>
	/// </remarks>
	public interface IJsonMutable<in TValue, out TMutable> : IJsonSerializable, IJsonPackable
	{

		/// <summary>Returns a mutable JSON version of an instance of <typeparamref name="TValue"/></summary>
		static abstract TMutable FromValue(TValue value);

		/// <summary>Returns the wrapped JSON Object</summary>
		/// <remarks>This object is mutable and can be changed directly.</remarks>
		public JsonObject ToJson();

	}

}
