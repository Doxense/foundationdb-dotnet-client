﻿#region Copyright (c) 2023-2024 SnowBank SAS, (c) 2005-2023 Doxense SAS
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

	/// <summary>Type that can deserialize instances of <typeparamref name="T"/> from JSON</summary>
	/// <typeparam name="T">Type of the values to deserialize</typeparam>
	/// <remarks>Types can serialize themselves, but this interface can also be used on "helper types" that are separate (manually written, or via source code generation)</remarks>
	public interface IJsonDeserializer<out T>
	{

		/// <summary>Deserializes a JSON value into an instance of type <typeparam name="T"></typeparam></summary>
		/// <param name="value">JSON value to deserialize.</param>
		/// <param name="resolver">Optional custom resolver</param>
		/// <returns>Deserialized value</returns>
		/// <exception cref="JsonBindingException">If an error occurred during the deserialization</exception>
		T Deserialize(JsonValue value, ICrystalJsonTypeResolver? resolver = null);
		//REVIEW: should we also pass some settings?

	}

	internal sealed class DefaultJsonDeserializer<T> : IJsonDeserializer<T>
	{

		public Func<JsonValue, ICrystalJsonTypeResolver, T> Handler { get; }

		public DefaultJsonDeserializer(Func<JsonValue, ICrystalJsonTypeResolver, T>? handler)
		{
			this.Handler = handler ?? (static (_, _) => throw new NotSupportedException("Operation not supported"));
		}

		/// <inheritdoc />
		public T Deserialize(JsonValue value, ICrystalJsonTypeResolver? resolver = null)
		{
			return this.Handler(value, resolver ?? CrystalJson.DefaultResolver);
		}

	}

}
