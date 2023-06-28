#region Copyright Doxense 2010-2021
//
// All rights are reserved. Reproduction or transmission in whole or in part, in
// any form or by any means, electronic, mechanical or otherwise, is prohibited
// without the prior written consent of the copyright owner.
//
#endregion

namespace Doxense.Serialization.Json
{
	using System;

	public class JsonTypeAttribute : Attribute
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

		public JsonTypeAttribute() { }

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
