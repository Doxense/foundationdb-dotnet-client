#region Copyright (c) 2023-2025 SnowBank SAS, (c) 2005-2023 Doxense SAS
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

namespace SnowBank.Data.Json
{

	/// <summary>Types that implement this interface support packing directly into a <see cref="JsonValue"/></summary>
	/// <remarks>Types that also support packing to a <see cref="JsonValue"/> should implement <see cref="IJsonDeserializable{T}"/> as well.</remarks>
	[PublicAPI]
	public interface IJsonPacker<in T>
	{

		/// <summary>Converts an instance of this type into the equivalent <see cref="JsonValue"/></summary>
		/// <param name="instance">Value to convert</param>
		/// <param name="settings">Serialization settings</param>
		/// <param name="resolver">Custom resolver used to bind the value into a managed type.</param>
		/// <remarks>Most types will produce a <see cref="JsonObject"/>, but some simple types may return a packed <see cref="JsonString"/>, or as a tuple represented by a <see cref="JsonArray"/></remarks>
		JsonValue Pack(T instance, CrystalJsonSettings? settings = null, ICrystalJsonTypeResolver? resolver = null);

	}

	internal sealed class DefaultJsonPacker<T> : IJsonPacker<T>
	{

		public Func<T, CrystalJsonSettings, ICrystalJsonTypeResolver, JsonValue> Handler { get; }

		public DefaultJsonPacker(Func<T, CrystalJsonSettings, ICrystalJsonTypeResolver, JsonValue>? handler)
		{
			this.Handler = handler ?? (static (_, _, _) => throw new NotSupportedException("Operation not supported"));
		}

		/// <inheritdoc />
		public JsonValue Pack(T instance, CrystalJsonSettings? settings = null, ICrystalJsonTypeResolver? resolver = null)
		{
			return this.Handler(instance, settings ?? CrystalJsonSettings.Json, resolver ?? CrystalJson.DefaultResolver);
		}

	}

}
