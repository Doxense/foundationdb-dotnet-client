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

	/// <summary>Wraps a <see cref="JsonValue"/> into typed mutable proxy</summary>
	[PublicAPI]
	public interface IJsonWritableProxy : IJsonSerializable, IJsonPackable
	{
		/// <summary>Returns the proxied JSON Value</summary>
		/// <remarks>The returned value is mutable and can be changed directly.</remarks>
		JsonValue ToJson();

	}

	/// <summary>Wraps a <see cref="JsonValue"/> into typed mutable proxy that emulates the type <typeparamref name="TValue"/></summary>
	/// <typeparam name="TValue">Emulated data type</typeparam>
	/// <remarks>
	/// <para>This interface is a marker for "wrapper types" that replicate the same set of properties and fields as <typeparamref name="TValue"/>, using a wrapped <see cref="JsonValue"/> as source.</para>
	/// </remarks>
	[PublicAPI]
	public interface IJsonWritableProxy<out TValue> : IJsonWritableProxy
	{

		/// <summary>Returns an instance of <typeparamref name="TValue"/> with the same content as this proxy.</summary>
		public TValue ToValue();

	}

	/// <summary>Wraps a <see cref="JsonValue"/> into typed mutable proxy that emulates the type <typeparamref name="TValue"/></summary>
	/// <typeparam name="TValue">Emulated data type</typeparam>
	/// <typeparam name="TMutableProxy">CRTP for the type that implements this interface</typeparam>
	/// <remarks>
	/// <para>This interface is a marker for "wrapper types" that replicate the same set of properties and fields as <typeparamref name="TValue"/>, using a wrapped <see cref="JsonValue"/> as source.</para>
	/// </remarks>
	[PublicAPI]
	public interface IJsonWritableProxy<TValue, out TMutableProxy> : IJsonWritableProxy<TValue>
		where TMutableProxy : IJsonWritableProxy<TValue, TMutableProxy>
	{

		/// <summary>Wraps a JSON Value into a mutable proxy for type <typeparamref name="TValue"/></summary>
		static abstract TMutableProxy Create(JsonValue obj, IJsonProxyNode? parent = null, JsonPathSegment segment = default, IJsonConverter<TValue>? converter = null);

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
	public interface IJsonWritableProxy<TValue, out TMutableProxy, out TReadOnlyProxy> : IJsonWritableProxy<TValue, TMutableProxy>
		where TMutableProxy : IJsonWritableProxy<TValue, TMutableProxy, TReadOnlyProxy>
		where TReadOnlyProxy : IJsonReadOnlyProxy<TValue, TReadOnlyProxy, TMutableProxy>
	{

		/// <summary>Converts this mutable proxy, back into the equivalent read-only proxy.</summary>
		/// <remarks>Any future changes to this mutable proxy will not impact the returned value</remarks>
		TReadOnlyProxy ToReadOnly();

	}

	/// <summary>Base class for custom or source generated implementations of a <see cref="IJsonWritableProxy"/></summary>
	/// <remarks>This contains all the boilerplate implementation that is common to most custom implementations</remarks>
	public abstract record JsonWritableProxyObjectBase
		: IJsonWritableProxy, IJsonProxyNode
	{

		/// <summary>Wrapped JSON Object</summary>
		protected readonly JsonObject m_obj;

		/// <summary>Parent object or array, or <c>null</c> if this is the top-level of the document</summary>
		protected readonly IJsonProxyNode? m_parent;

		/// <summary>Segment of path from the parent node to this instance</summary>
		protected readonly JsonPathSegment m_segment;

		protected readonly int m_depth;

		/// <inheritdoc />
		JsonType IJsonProxyNode.Type => JsonType.Object;

		protected JsonWritableProxyObjectBase(JsonValue value, IJsonProxyNode? parent, JsonPathSegment segment)
		{
			m_obj = value.AsObject();
			m_parent = parent;
			m_segment = segment;
			m_depth = (parent?.Depth ?? -1) + 1;
		}

		/// <inheritdoc />
		IJsonProxyNode? IJsonProxyNode.Parent => m_parent;

		/// <inheritdoc />
		JsonPathSegment IJsonProxyNode.Segment => m_segment;

		/// <inheritdoc />
		int IJsonProxyNode.Depth => m_depth;

		/// <inheritdoc />
		void IJsonProxyNode.WritePath(ref JsonPathBuilder builder)
		{
			m_parent?.WritePath(ref builder);
			builder.Append(m_segment);
		}

		/// <inheritdoc />
		public void JsonSerialize(CrystalJsonWriter writer) => m_obj.JsonSerialize(writer);

		/// <inheritdoc />
		public JsonValue JsonPack(CrystalJsonSettings settings, ICrystalJsonTypeResolver resolver) => m_obj;

		/// <inheritdoc />
		public JsonValue ToJson() => m_obj;

	}

}
