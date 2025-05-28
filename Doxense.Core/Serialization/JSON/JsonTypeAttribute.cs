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

#if DEPRECATED

namespace SnowBank.Data.Json
{

	/// <summary>Attribute that controls how a type is serialized into JSON</summary>
	[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct | AttributeTargets.Interface)]
	[PublicAPI]
	public sealed class JsonTypeAttribute : Attribute
	{

		/* How To Use:
		//
		// This attributes helps with serialization/deserialization of "trees" of derived classes that will be deserialized from their base class or interface.
		//
		// Ex: with IAnimal being the "root" of the tree, and concrete types Dog and Cat implementing IAnimal:
		//
		//   [JsonType(TypePropertyName = "Type")]
		//   public interface IAnimal
		//   {
		//      string Type { get; }
		//      string Name { get; }
		//   }
		//
		//   [JsonType(BaseType = typeof(IAnimal), ClassId = "Cat")]
		//   public record Cat : IAnimal
		//   {
		//      public string Type => "Cat";
		//      public string Name { get; init; }
		//      public int LivesRemaining { get; init; }
		//   }
		//
		//   [JsonType(BaseType = typeof(IAnimal), ClassId = "Dog")]
		//   public record Dog : IAnimal
		//   {
		//      public string Type => "Dog";
		//      public string Name { get; init; }
		//      public bool IsGoodBoy { get; init; }
		//   }
		//
		// When serializing an IAnimal object, the "_class" property will be ommitted
		// => { "Type": "Cat", "Name": "Mr Smithy", LivesRemaining: 7 }
		// When deserializing an IAnimal object, the "Type" property will be read ('Cat'), and an instance of the (only) type that is registered for the corresponding ClassId of this base type will be created (class 'Cat')
		//
		*/

		/// <summary>Base type of this "type tree", usually the top interface or base class</summary>
		/// <remarks>Should be applied on derived types to point to the "root" of this particular "type tree". In the case of an abstract class that implements and interface, if should still point to the interface has the root.</remarks>
		/// <example><c>[JsonType(BaseType = typeof(IAnimal), ...)]</c></example>
		public Type? BaseType { get; set; }

		/// <summary>Name of the field or property that contains the unique "class id"</summary>
		/// <example><c>[JsonType(TypePropertyName = nameof(IAnimal.Type))]</c></example>
		public string? TypePropertyName { get; set; }

		/// <summary>Class ID for this concrete type</summary>
		/// <remarks>Should be unique inside a specific "type tree". Separate "trees" can reuse the same class ids</remarks>
		/// <example><c>[JsonType(BaseType = typeof(IAnimal), TypePropertyName = "Cat")]</c></example>
		public string? ClassId { get; set; }

	}

}

#endif
