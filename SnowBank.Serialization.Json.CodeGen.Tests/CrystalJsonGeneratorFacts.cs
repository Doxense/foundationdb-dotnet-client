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

namespace Doxense.Serialization.Json.CodeGen.Tests
{
	using System.ComponentModel.DataAnnotations;
	using NUnit.Framework;
	using SnowBank.Testing;

	public sealed record Person
	{

		[JsonProperty("familyName")]
		public required string FamilyName { get; init; }

		[JsonProperty("firstName")]
		public required string FirstName { get; init; }

		[JsonProperty("dob")]
		public DateOnly DateOfBirth { get; init; }

		public int? Zobi { get; init; }
		
	}

	public sealed record MyAwesomeUser
	{

		/// <summary>User ID.</summary>
		[Key, JsonProperty("id")]
		public required string Id { get; init; }

		/// <summary>Full name, for display purpose</summary>
		[JsonProperty("displayName")]
		public required string DisplayName { get; init; }

		/// <summary>Primary email for this account</summary>
		[JsonProperty("email")]
		public required string Email { get; init; }

		[JsonProperty("type", DefaultValue = 777)]
		public int Type { get; init; }

		[JsonProperty("roles")]
		public string[]? Roles { get; init; }

		[JsonProperty("metadata")]
		public required MyAwesomeMetadata Metadata { get; init; }

		[JsonProperty("items")]
		public required List<MyAwesomeStruct>? Items { get; init; }

		[JsonProperty("devices")]
		public Dictionary<string, MyAwesomeDevice>? Devices { get; init; }

		[JsonProperty("extras")]
		public JsonObject? Extras { get; init; }

	}

	public sealed record MyAwesomeMetadata
	{
		/// <summary>Date at which this account was created</summary>
		[JsonProperty("accountCreated")]
		public DateTimeOffset AccountCreated { get; init; }

		/// <summary>Date at which this account was last modified</summary>
		[JsonProperty("accountModified")]
		public DateTimeOffset AccountModified { get; init; }

		/// <summary>Date at which this account was deleted, or <see langword="null"/> if it is still active</summary>
		[JsonProperty("accountDisabled")]
		public DateTimeOffset? AccountDisabled { get; init; }
	}

	public record struct MyAwesomeStruct
	{

		/// <summary>Some fancy ID</summary>
		[Key, JsonProperty("id")]
		public required string Id { get; init; }

		/// <summary>Is it over 8000?</summary>
		[JsonProperty("level")]
		public required int Level { get; init; }

		/// <summary>Path to enlightenment</summary>
		[JsonProperty("path")]
		public required JsonPath Path { get; init; }

		[JsonProperty("paths")]
		public JsonPath[]? Paths { get; init; }

		[JsonProperty("maybePath")]
		public JsonPath? MaybePath { get; init; }

		/// <summary>End of the road</summary>
		[JsonProperty("disabled")]
		public bool? Disabled { get; init; }

	}

	public sealed record MyAwesomeDevice
	{
		[JsonProperty("id")]
		public required string Id { get; init; }

		[JsonProperty("model")]
		public required string Model { get; init; }

		[JsonProperty("lastSeen")]
		public DateTimeOffset? LastSeen { get; init; }

		[JsonProperty("lastAddress")]
		public System.Net.IPAddress? LastAddress { get; init; }

	}
	
	[CrystalJsonConverter]
	[CrystalJsonSerializable(typeof(Person))]
	//[CrystalJsonSerializable(typeof(MyAwesomeDevice))]
	public static partial class GeneratedConverters
	{
		// generated code goes here!
	}

	[TestFixture]
	public class CrystalJsonGeneratorFacts : SimpleTest
	{
		
		[Test]
		public void Test_Generates_Code_For_Person()
		{
			// check the serializer singleton
			var serializer = GeneratedConverters.Person;
			Assert.That(serializer, Is.Not.Null);
			
			// check the property names (should be camelCased)
			Assert.That(GeneratedConverters.PersonJsonConverter.PropertyNames.FamilyName, Is.EqualTo("familyName"));
			Assert.That(GeneratedConverters.PersonJsonConverter.PropertyNames.FirstName, Is.EqualTo("firstName"));
			Assert.That(GeneratedConverters.PersonJsonConverter.PropertyNames.GetAllNames(), Is.EquivalentTo(new [] { "familyName", "firstName" }));
			
			var person = new Person()
			{
				FamilyName = "Bond",
				FirstName = "James"
			};
			
			Log("Serialize:");
			{
				var writer = new CrystalJsonWriter(0, CrystalJsonSettings.JsonIndented, CrystalJson.DefaultResolver);
				GeneratedConverters.Person.Serialize(writer, person);
				var jsonText = writer.GetString();
				Log(jsonText);
				var obj = JsonValue.ParseObject(jsonText);
				Assert.That(obj["familyName"], IsJson.EqualTo("Bond"));
				Assert.That(obj["firstName"], IsJson.EqualTo("James"));
			}

			Log("Pack:");
			{
				var packed = GeneratedConverters.Person.Pack(person);
				Dump(packed);
				Assert.That(packed, IsJson.Object);
				Assert.That(packed["familyName"], IsJson.EqualTo("Bond"));
				Assert.That(packed["firstName"], IsJson.EqualTo("James"));
			}

			Log("Unpack:");
			{
				var packed = JsonObject.Create([
					("familyName", "Bond"),
					("firstName", "James"),
				]);
				var unpacked = GeneratedConverters.Person.Unpack(packed);
				Dump(unpacked);
				Assert.That(unpacked.FamilyName, Is.EqualTo("Bond"));
				Assert.That(unpacked.FirstName, Is.EqualTo("James"));
			}

		}

	}

}
