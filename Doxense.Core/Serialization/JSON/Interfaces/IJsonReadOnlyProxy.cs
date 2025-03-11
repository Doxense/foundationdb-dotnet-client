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
		/// <remarks>
		/// <para>The returned value is read-only and cannot be changed.</para>
		/// </remarks>
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
	/// <typeparam name="TMutableProxy">CRTP for the corresponding <see cref="IJsonWritableProxy{TValue,TMutableProxy,TReadOnlyProxy}"/> of this type</typeparam>
	/// <remarks>
	/// <para>This interface is a marker for "wrapper types" that replicate the same set of properties and fields as <typeparamref name="TValue"/>, using a wrapped <see cref="JsonValue"/> as source.</para>
	/// </remarks>
	[PublicAPI]
	public interface IJsonReadOnlyProxy<TValue, out TReadOnlyProxy, out TMutableProxy> : IJsonReadOnlyProxy<TValue, TReadOnlyProxy>
		where TReadOnlyProxy : IJsonReadOnlyProxy<TValue, TReadOnlyProxy, TMutableProxy>
		where TMutableProxy : IJsonWritableProxy<TValue, TMutableProxy, TReadOnlyProxy>
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
