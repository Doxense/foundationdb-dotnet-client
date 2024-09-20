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
	using System.ComponentModel;

	/// <summary>Type that can serialize itself to JSON</summary>
	public interface IJsonSerializable
	{
		/// <summary>Serializes this instance as JSON</summary>
		/// <param name="writer">Writer that will output the content of this instance</param>
		void JsonSerialize(CrystalJsonWriter writer);

		//note: JsonDeserialize used to be in this interface, but has been moved to IJsonDeserialize, tagged as [Obsolete]
		// the correct way is to implement IJsonDeserializer<T> or have a ctor that takes in a JsonValue as first parameter

	}

	/// <summary>LEGACY: should be not implemented. Implement <see cref="IJsonDeserializer{TSelf}"/> instead.</summary>
	[Obsolete("Implement IJsonDeserializer<T> instead")]
	public interface IJsonDeserializable
	{
		// Why it's deprecated:
		// - does not work with read-only objects (cannot write to them after the ctor)
		// - does not support { get; init; } properties (for the same reason)
		// - does not work well with types that have custom initialization in the ctor (runs before we have the content)
		// - was created before the support for static methods in interfaces

		/// <summary>Injects the contant of a JSON Objet into an instance of this type</summary>
		/// <param name="value">Valeur</param>
		/// <param name="declaredType"></param>
		/// <param name="resolver">Optional custom resolver used to bind the value into a managed type.</param>
		[EditorBrowsable(EditorBrowsableState.Never)]
		void JsonDeserialize(JsonObject value, Type declaredType, ICrystalJsonTypeResolver resolver);

	}

	/// <summary>Type that can serialize instances of <typeparamref name="T"/> into JSON</summary>
	/// <typeparam name="T">Type of the values to serialize</typeparam>
	/// <remarks>Types can serialize themselves, but this interface can also be used on "helper types" that are separate (manually written, or via source code generation)</remarks>
	public interface IJsonSerializer<in T>
	{

		/// <summary>Writes a JSON representation of a value to the specified output</summary>
		/// <param name="writer">Writer that will outputs the JSON in the desired format</param>
		/// <param name="instance">Instance the will be serialized</param>
		/// <exception cref="JsonSerializationException">If an error occured during the serialization</exception>
		void JsonSerialize(CrystalJsonWriter writer, T? instance);

	}

	/// <summary>Type that can deserialize instances of <typeparamref name="T"/> from JSON</summary>
	/// <typeparam name="T">Type of the values to deserialize</typeparam>
	/// <remarks>Types can serialize themselves, but this interface can also be used on "helper types" that are separate (manually written, or via source code generation)</remarks>
	public interface IJsonDeserializerFor<out T>
	{

		/// <summary>Deserializes a JSON value into an instance of type <typeparam name="T"></typeparam></summary>
		/// <param name="value">JSON value to deserialize.</param>
		/// <param name="resolver">Optional custom resolver</param>
		/// <returns>Deserialized value</returns>
		/// <exception cref="JsonBindingException">If an error occurred during the deserialization</exception>
		T JsonDeserialize(JsonValue value, ICrystalJsonTypeResolver? resolver = null);

	}

}
