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
	using System;

	/// <summary>Types that implement this interface support serialization directly into a <see cref="JsonValue"/>, usually as a JSON Object or Array</summary>
	/// <remarks>Types that also support deserializing from a <see cref="JsonValue"/> should implement <see cref="IJsonUnpackable{TSelf}"/> as well.</remarks>
	public interface IJsonPackable
	{

		/// <summary>Convert an instance of this type into the equivalent JSON value, usually a JSON Object or Array</summary>
		/// <param name="settings">Serialization settings</param>
		/// <param name="resolver">Custom resolver used to bind the value into a managed type.</param>
		/// <remarks>If the type also implements <see cref="IJsonUnpackable{TSelf}"/>, the result of <see cref="IJsonUnpackable{TSelf}.JsonUnpack">parsing</see> the resulting JSON value should produce an object that is equivalent to the original (or as close as possible if some values loose some precision after serialization)</remarks>
		JsonValue JsonPack(CrystalJsonSettings settings, ICrystalJsonTypeResolver resolver);

	}


	/// <summary>Interface indiquant qu'un objet peut gérer lui même la sérialisation vers/depuis un DOM JSON</summary>
	[Obsolete("Consider implementing both IJsonPackable and IJsonUnpackable<T> instead")]
	public interface IJsonBindable : IJsonPackable
	{
		/// <summary>Initialize this value by loading the contents of a previously serialize JSON value</summary>
		/// <param name="value">JSON value that will be bound to this instance</param>
		/// <param name="resolver">Custom resolver used to bind the value into a managed type.</param>
		[Obsolete("Consider calling the static IJsonUnpackable<T>.JsonUnpack(...) overload instead")]
		void JsonUnpack(JsonValue value, ICrystalJsonTypeResolver resolver);
	}

	/// <summary>Types that implement this interface support deserialization directly from a <see cref="JsonValue"/></summary>
	/// <remarks>Types that also support serializing to a <see cref="JsonValue"/> should implement <see cref="IJsonPackable"/> as well.</remarks>
 #if !NET8_0_OR_GREATER
	[System.Runtime.Versioning.RequiresPreviewFeatures]
#endif
	public interface IJsonUnpackable<out TSelf>
	{
		/// <summary>Deserialize an instance of type <typeparamref name="TSelf"/> from parsed JSON value</summary>
		/// <param name="value">JSON value that will be bound to the new instance</param>
		/// <param name="resolver">Custom resolver used to bind the value into a managed type.</param>
		/// <returns>A new instance of <typeparamref name="TSelf"/> that has been initialized from the contents of <paramref name="value"/>.</returns>
		static abstract TSelf JsonUnpack(JsonValue value, ICrystalJsonTypeResolver? resolver = null);

	}

}
