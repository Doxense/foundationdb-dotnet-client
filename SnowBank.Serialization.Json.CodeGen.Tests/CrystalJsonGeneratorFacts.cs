namespace Doxense.Serialization.Json.CodeGen.Tests
{
	using NUnit.Framework;
	using SnowBank.Testing;

	public sealed record Person
	{

		[JsonProperty("familyName")]
		public required string FamilyName { get; init; }

		[JsonProperty("firstName")]
		public required string FirstName { get; init; }

	}

	[CrystalJsonConverter]
	[CrystalJsonInclude(typeof(Person))]
	public static partial class GeneratedConverters
	{

	}


	[TestFixture]
	public class CrystalJsonGeneratorFacts : SimpleTest
	{
			
		[Test]
		public void Test_Generates_Converter()
		{

			var person = new Person() { FamilyName = "Bond", FirstName = "James" };

			//TODO!
			var json = GeneratedConverters.PersonConverter.Serialize(person);
			Log(json);
		}

	}

}
