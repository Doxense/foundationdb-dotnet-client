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

#if NET8_0_OR_GREATER

namespace Doxense.Serialization.Json.Tests
{
	using System.Buffers;
	using System.Collections.Generic;
	using System.ComponentModel.DataAnnotations;
	using System.Net;
	using Doxense.Mathematics.Statistics;

	#region Data Types...

	public record Person
	{
		[JsonProperty("firstName")]
		public string? FirstName { get; set; }

		[JsonProperty("familyName")]
		public string? FamilyName { get; set; }

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
		public IPAddress? LastAddress { get; init; }

	}

	[System.Text.Json.Serialization.JsonSourceGenerationOptions(System.Text.Json.JsonSerializerDefaults.Web /*, Converters = [ typeof(NodaTimeInstantJsonConverter) ], */)]
	[System.Text.Json.Serialization.JsonSerializable(typeof(MyAwesomeUser))]
	[System.Text.Json.Serialization.JsonSerializable(typeof(Person))]
	public partial class SystemTextJsonGeneratedSerializers : System.Text.Json.Serialization.JsonSerializerContext;

	#endregion

	[TestFixture]
	[Category("Core-SDK")]
	[Category("Core-JSON")]
	public class CrystalJsonSourceGeneratorFacts : SimpleTest
	{

		[Test]
		public void Generate_Source_Code()
		{
			//HACKHACK: for the moment this dumps the code in the test log,
			// you are supposed to copy/paste this code and replace the block at the end of this file!
			// => later we may have a fancy source code generator that does this automatically :)

			Assume.That(typeof(string[]).GetFriendlyName(), Is.EqualTo("string[]"));
			Assume.That(TypeHelper.GetCompilableTypeName(typeof(string[]), omitNamespace: false, global: true), Is.EqualTo("string[]"));
			Assume.That(TypeHelper.GetCompilableTypeName(typeof(bool?[]), omitNamespace: false, global: true), Is.EqualTo("bool?[]"));
			Assume.That(TypeHelper.GetCompilableTypeName(typeof(JsonValue), omitNamespace: false, global: true), Is.EqualTo("global::Doxense.Serialization.Json.JsonValue"));
			Assume.That(TypeHelper.GetCompilableTypeName(typeof(JsonPath?[]), omitNamespace: false, global: true), Is.EqualTo("global::Doxense.Serialization.Json.JsonPath?[]"));

			var gen = new CrystalJsonSourceGenerator()
			{
				Namespace = this.GetType().Namespace!,
				SerializerContainerName = "GeneratedSerializers"
			};

			gen.AddTypes(
			[
				typeof(Person),
				typeof(MyAwesomeUser),
			]);

			var source = gen.GenerateCode();
			Log(source);

#if !DISABLED
			WriteSourceCode("GeneratedSourceCode.cs", source);

			static void WriteSourceCode(string fileName, string source, [System.Runtime.CompilerServices.CallerFilePath] string? callerPath = null)
			{
				string filePath = System.IO.Path.Combine(System.IO.Path.GetDirectoryName(callerPath)!, fileName);
				System.IO.File.WriteAllText(filePath, source);
			}
#endif
		}

		private static MyAwesomeUser MakeSampleUser() => new()
		{
			Id = "b6a16abe-e30c-4198-8358-5f0d8fd9c283",
			DisplayName = "James Bond",
			Email = "bond@example.org",
			Type = 007,
			Roles = ["user", "secret_agent"],
			Metadata = new()
			{
				AccountCreated = DateTimeOffset.Parse("2024-09-20T12:34:56.7890123Z"),
				AccountModified = DateTimeOffset.Parse("2024-09-21T10:00:25.5461402Z"),
			},
			Items =
			[
				new() { Id = "382bb7cd-f9e4-4906-874e-ab88df954fa8", Level = 123, Path = JsonPath.Create("foo.bar") },
				new() { Id = "8092e57d-16b4-4afb-ae04-28acbeb22aa8", Level = 456, Path = JsonPath.Create("bars[2].foo"), Disabled = true }
			],
			Devices = new()
			{
				["Foo"] = new() { Id = "Foo", Model = "ACME Ultra Core 9100XX Ultra Series" },
				["Bar"] = new() { Id = "Bar", Model = "iHAL 42 Pro Ultra MaXX" },
			},
			Extras = JsonObject.ReadOnly.Create([
				("hello", "world"),
				("foo", 123),
				("bar", JsonArray.Create([ 1, 2, 3 ])),
			]),
		};

		[Test]
		public void Test_Custom_Serializer_Simple_Type()
		{
			{
				var person = new Person()
				{
					FamilyName = "Bond",
					FirstName = "James"
				};

				Log(person.ToString());
				Log();
				Log("# Reference System.Text.Json:");
				Log(System.Text.Json.JsonSerializer.Serialize(person, SystemTextJsonGeneratedSerializers.Default.Person));
				Log();

				Log("# Actual output:");
				var json = GeneratedSerializers.Person.ToJson(person);
				Log(json);
				Assert.That(json, Is.EqualTo("""{ "firstName": "James", "familyName": "Bond" }"""));
				Log();

				Log("# Parsing:");
				var parsed = GeneratedSerializers.Person.Deserialize(json);
				Assert.That(parsed, Is.Not.Null);
				Log(parsed.ToString());
				Assert.That(parsed.FirstName, Is.EqualTo("James"));
				Assert.That(parsed.FamilyName, Is.EqualTo("Bond"));
				Log();

				Log("# Packing:");
				var packed = GeneratedSerializers.Person.Pack(person);
				Dump(packed);
				Assert.That(packed, IsJson.Object);
				Assert.That(packed["firstName"], IsJson.EqualTo("James"));
				Assert.That(packed["familyName"], IsJson.EqualTo("Bond"));
				Log();
			}

			{
				var person = new Person() { FirstName = "👍", FamilyName = "🐶" };
				Log(person.ToString());
				Log();
				Log("# Reference System.Text.Json:");
				Log(System.Text.Json.JsonSerializer.Serialize(person, SystemTextJsonGeneratedSerializers.Default.Person));
				Log();

				Log("# Actual output:");
				var json = CrystalJson.Serialize(person, GeneratedSerializers.Person);
				Log(json);
				Assert.That(json, Is.EqualTo("""{ "firstName": "\ud83d\udc4d", "familyName": "\ud83d\udc36" }"""));

				var parsed = CrystalJson.Deserialize<Person>(json, GeneratedSerializers.Person);
				Assert.That(parsed, Is.Not.Null);
				Assert.That(parsed.FirstName, Is.EqualTo("👍"));
				Assert.That(parsed.FamilyName, Is.EqualTo("🐶"));

				Log();
			}

			Assert.Multiple(() =>
			{
				Assert.That(CrystalJson.Serialize(new(), GeneratedSerializers.Person), Is.EqualTo("""{ }"""));
				Assert.That(CrystalJson.Serialize(new() { FirstName = null, FamilyName = null }, GeneratedSerializers.Person), Is.EqualTo("""{ }"""));
				Assert.That(CrystalJson.Serialize(new() { FirstName = "", FamilyName = "" }, GeneratedSerializers.Person), Is.EqualTo("""{ "firstName": "", "familyName": "" }"""));
				Assert.That(CrystalJson.Serialize(new() { FirstName = "James" }, GeneratedSerializers.Person), Is.EqualTo("""{ "firstName": "James" }"""));
				Assert.That(CrystalJson.Serialize(new() { FamilyName = "Bond" }, GeneratedSerializers.Person), Is.EqualTo("""{ "familyName": "Bond" }"""));
			});
		}

		[Test]
		public void Test_Custom_Serializer_Complex_Type()
		{
			var user = MakeSampleUser();

			Log("Expected Json:");
			var expectedJson = CrystalJson.Serialize(user);
			Log(expectedJson);

			Log();
			Log("Actual Json:");
			var json = CrystalJson.Serialize(user, GeneratedSerializers.MyAwesomeUser);
			Log(json);
			Assert.That(json, Is.EqualTo(expectedJson));

			{ // Compare with System.Text.Json:
				Log();
				Log("System.Text.Json reference:");
				Log(System.Text.Json.JsonSerializer.Serialize(user, SystemTextJsonGeneratedSerializers.Default.MyAwesomeUser));
			}

			// ToSlice

			// non-pooled (return a copy)
			var bytes = CrystalJson.ToSlice(user, GeneratedSerializers.MyAwesomeUser);
			Assert.That(bytes.ToStringUtf8(), Is.EqualTo(json));

			{ // pooled (rented buffer)
				using (var res = CrystalJson.ToSlice(user, GeneratedSerializers.MyAwesomeUser, ArrayPool<byte>.Shared))
				{
					Assert.That(res.IsValid, Is.True);
					Assert.That(res.Count, Is.EqualTo(bytes.Count));
					if (!res.Data.Equals(bytes))
					{
						Assert.That(res.Data, Is.EqualTo(bytes));
					}
					if (!res.Span.SequenceEqual(bytes.Span))
					{
						Assert.That(res.Data, Is.EqualTo(bytes));
					}
				}
			}

			Log();
			Log("Parse...");
			var parsed = JsonValue.Parse(json);
			DumpCompact(parsed);
			Assert.That(parsed, IsJson.Object);
			Assert.Multiple(() =>
			{
				Assert.That(parsed["id"], IsJson.EqualTo(user.Id));
				Assert.That(parsed["displayName"], IsJson.EqualTo(user.DisplayName));
				Assert.That(parsed["email"], IsJson.EqualTo(user.Email));
				Assert.That(parsed["type"], IsJson.EqualTo(user.Type));
				Assert.That(parsed["metadata"], IsJson.Object);
				Assert.That(parsed["metadata"]["accountCreated"], IsJson.EqualTo(user.Metadata.AccountCreated));
				Assert.That(parsed["metadata"]["accountModified"], IsJson.EqualTo(user.Metadata.AccountModified));
				Assert.That(parsed["metadata"]["accountDisabled"], IsJson.EqualTo(user.Metadata.AccountDisabled));
				Assert.That(parsed["items"], IsJson.Array.And.OfSize(2));
				Assert.That(parsed["items"][0], IsJson.Object);
				Assert.That(parsed["items"][0]["id"], IsJson.EqualTo("382bb7cd-f9e4-4906-874e-ab88df954fa8"));
				Assert.That(parsed["items"][0]["level"], IsJson.EqualTo(123));
				Assert.That(parsed["items"][0]["path"], IsJson.EqualTo("foo.bar"));
				Assert.That(parsed["items"][0]["disabled"], IsJson.Null);
				Assert.That(parsed["items"][1], IsJson.Object);
				Assert.That(parsed["items"][1]["id"], IsJson.EqualTo("8092e57d-16b4-4afb-ae04-28acbeb22aa8"));
				Assert.That(parsed["items"][1]["level"], IsJson.EqualTo(456));
				Assert.That(parsed["items"][1]["path"], IsJson.EqualTo("bars[2].foo"));
				Assert.That(parsed["items"][1]["disabled"], IsJson.True);
				Assert.That(parsed["devices"], IsJson.Object.And.OfSize(2));
				Assert.That(parsed["devices"]["Foo"], IsJson.Object);
				Assert.That(parsed["devices"]["Foo"]["model"], IsJson.EqualTo("ACME Ultra Core 9100XX Ultra Series"));
				Assert.That(parsed["devices"]["Bar"], IsJson.Object);
				Assert.That(parsed["devices"]["Bar"]["model"], IsJson.EqualTo("iHAL 42 Pro Ultra MaXX"));
				Assert.That(parsed["extras"], IsJson.Object.And.EqualTo(user.Extras));
			});

			Log();
			Log("Deserialize...");
			var decoded = GeneratedSerializers.MyAwesomeUser.Unpack(parsed);
			Assert.That(decoded, Is.Not.Null);
			Assert.That(decoded.Id, Is.EqualTo(user.Id));
			Assert.That(decoded.DisplayName, Is.EqualTo(user.DisplayName));
			Assert.That(decoded.Email, Is.EqualTo(user.Email));
			Assert.That(decoded.Type, Is.EqualTo(user.Type));
			Assert.That(decoded.Metadata, Is.EqualTo(user.Metadata));
			Assert.That(decoded.Roles, Is.EqualTo(user.Roles));
			Assert.That(decoded.Devices, Is.EqualTo(user.Devices));
			Assert.That(decoded.Extras, IsJson.EqualTo(user.Extras));

			Log();
			Log("Pack...");
			var packed = GeneratedSerializers.MyAwesomeUser.Pack(user);
			Dump(packed);
			Assert.That(packed, IsJson.Object);
			Assert.Multiple(() =>
			{
				Assert.That(packed["id"], IsJson.EqualTo(user.Id));
				Assert.That(packed["displayName"], IsJson.EqualTo(user.DisplayName));
				Assert.That(packed["email"], IsJson.EqualTo(user.Email));
				Assert.That(packed["type"], IsJson.EqualTo(user.Type));
				Assert.That(packed["metadata"], IsJson.Object);
				Assert.That(packed["metadata"]["accountCreated"], IsJson.EqualTo(user.Metadata.AccountCreated));
				Assert.That(packed["metadata"]["accountModified"], IsJson.EqualTo(user.Metadata.AccountModified));
				Assert.That(packed["metadata"]["accountDisabled"], IsJson.EqualTo(user.Metadata.AccountDisabled));
				Assert.That(packed["items"], IsJson.Array.And.OfSize(2));
				Assert.That(packed["items"][0], IsJson.Object);
				Assert.That(packed["items"][0]["id"], IsJson.EqualTo("382bb7cd-f9e4-4906-874e-ab88df954fa8"));
				Assert.That(packed["items"][1], IsJson.Object);
				Assert.That(packed["items"][1]["id"], IsJson.EqualTo("8092e57d-16b4-4afb-ae04-28acbeb22aa8"));
				Assert.That(packed["devices"], IsJson.Object.And.OfSize(2));
				Assert.That(packed["devices"]["Foo"], IsJson.Object);
				Assert.That(packed["devices"]["Bar"], IsJson.Object);
				Assert.That(packed["extras"], IsJson.Object.And.EqualTo(user.Extras));
			});
		}

		[Test]
		public void Test_JsonReadOnlyProxy_FromValue_SimpleType()
		{
			// Test that FromValue(TValue) returns a read-only proxy that match the original instance

			var person = new Person()
			{
				FamilyName = "Bond",
				FirstName = "James"
			};

			Log("Person:");
			Log(person.ToString());

			// Convert the Person into a proxy that wraps a read-only JsonObject
			Log("FromValue(Person)");
			var proxy = GeneratedSerializers.Person.AsReadOnly(person);
			Log(proxy.ToString());
			Assert.That(proxy.FamilyName, Is.EqualTo("Bond"));
			Assert.That(proxy.FirstName, Is.EqualTo("James"));

			// inspect the wrapped JsonObject
			Log("ToJson()");
			var json = proxy.ToJson();
			Dump(json);
			Assert.That(json, IsJson.Object.And.ReadOnly);
			Assert.That(json["familyName"], IsJson.EqualTo("Bond"));
			Assert.That(json["firstName"], IsJson.EqualTo("James"));
			Assert.That(json, IsJson.OfSize(2));

			// serialize back into a Person
			Log("ToValue()");
			var decoded = proxy.ToValue();
			Assert.That(decoded, Is.Not.Null);
			Log(decoded.ToString());
			Assert.That(decoded, Is.InstanceOf<Person>().And.Not.SameAs(person));
			Assert.That(decoded.FamilyName, Is.EqualTo(person.FamilyName));
			Assert.That(decoded.FirstName, Is.EqualTo(person.FirstName));
		}

		[Test]
		public void Test_JsonReadOnlyProxy_Mutate_SimpleType()
		{
			var person = new Person()
			{
				FamilyName = "Bond",
				FirstName = "James"
			};
			Log("Person:");
			Dump(person);

			Log("ReadOnly:");
			var proxy = GeneratedSerializers.Person.AsReadOnly(person);
			Log(proxy.ToString());
			Assert.That(proxy.FamilyName, Is.EqualTo("Bond"));
			Assert.That(proxy.FirstName, Is.EqualTo("James"));
			Assert.That(proxy.ToJson(), IsJson.ReadOnly);

			{ // ReadOnly.With(Mutable => .... }
				var mutated = proxy.With(
					m =>
					{
						Assert.That(m.FamilyName, Is.EqualTo("Bond"));
						Assert.That(m.FirstName, Is.EqualTo("James"));
						// the JSON should a mutable copy of the original
						Assert.That(m.ToJson(), IsJson.Object.And.Mutable.And.EqualTo(proxy.ToJson()));

						m.FirstName = "Jim";
						Assert.That(m.FirstName, Is.EqualTo("Jim"));
						Assert.That(m.ToJson()["firstName"], IsJson.EqualTo("Jim"));
					});

				Log(mutated.ToString());
				// should return an updated object
				Assert.That(mutated.FamilyName, Is.EqualTo("Bond"));
				Assert.That(mutated.FirstName, Is.EqualTo("Jim"));
				Assert.That(mutated.ToJson(), IsJson.ReadOnly);

				// should not change the original!
				Assert.That(proxy.FamilyName, Is.EqualTo("Bond"));
				Assert.That(proxy.FirstName, Is.EqualTo("James"));
			}

			{ // ReadOnly.ToMutable with { ... }

				var mutated = proxy.ToMutable() with { FirstName = "Jim" };
				Log(mutated.ToString());

				// should return an updated object
				Assert.That(mutated.FamilyName, Is.EqualTo("Bond"));
				Assert.That(mutated.FirstName, Is.EqualTo("Jim"));
				Assert.That(mutated.ToJson(), IsJson.Mutable);

				// should not change the original!
				Assert.That(proxy.FamilyName, Is.EqualTo("Bond"));
				Assert.That(proxy.FirstName, Is.EqualTo("James"));

			}
		}

		[Test]
		public void Test_JsonReadOnlyProxy_Mutate_ComplexType()
		{
			var user = MakeSampleUser();

			Log("User:");
			Dump(user);

			Log("ReadOnly:");
			var proxy = GeneratedSerializers.MyAwesomeUser.AsReadOnly(user);
			Log(proxy.ToString());
			Assert.That(user.DisplayName, Is.EqualTo("James Bond"));
			Assert.That(user.Roles, Is.EqualTo((string[]) ["user", "secret_agent"]));
			Assert.That(proxy.ToJson(), IsJson.ReadOnly);

			{ // ReadOnly.With(Mutable => .... }
				var mutated = proxy.With(
					m =>
					{
						Assert.That(m.DisplayName, Is.EqualTo("James Bond"));
						Assert.That(m.Roles, Is.EqualTo((string[]) ["user", "secret_agent"]));
						// the JSON should a mutable copy of the original
						Assert.That(m.ToJson(), IsJson.Object.And.Mutable.And.EqualTo(proxy.ToJson()));

						m.DisplayName = "Jim Bond";
						Assert.That(m.DisplayName, Is.EqualTo("Jim Bond"));
						Assert.That(m.ToJson()["displayName"], IsJson.EqualTo("Jim Bond"));

						m.Roles = ["user", "secret_agent", "retired"];
						Assert.That(m.Roles, Is.EqualTo((string[]) ["user", "secret_agent", "retired"]));
						Assert.That(m.ToJson()["roles"], IsJson.Array.And.EqualTo((string[]) ["user", "secret_agent", "retired"]));
					});

				Log(mutated.ToString());
				// should return an updated object
				Assert.That(mutated.DisplayName, Is.EqualTo("Jim Bond"));
				Assert.That(mutated.Roles, Is.EqualTo((string[]) ["user", "secret_agent", "retired"]));
				Assert.That(mutated.ToJson(), IsJson.ReadOnly);

				// should not change the original!
				Assert.That(proxy.DisplayName, Is.EqualTo("James Bond"));
				Assert.That(proxy.Roles, Is.EqualTo((string[]) ["user", "secret_agent"]));
			}

			{ // ReadOnly.ToMutable with { ... }

				var mutated = proxy.ToMutable() with { Type = 008 };
				Log(mutated.ToString());

				// should return an updated object
				Assert.That(mutated.Type, Is.EqualTo(8));
				Assert.That(mutated.ToJson(), IsJson.Mutable);

				// should not change the original!
				Assert.That(proxy.Type, Is.EqualTo(7));
			}
		}

		[Test]
		public void Test_JsonMutableProxy_FromValue_SimpleType()
		{
			// Test that FromValue(TValue) returns a read-only proxy that match the original instance

			var person = new Person()
			{
				FamilyName = "Bond",
				FirstName = "James"
			};

			Log("Person:");
			Log(person.ToString());

			// Convert the Person into a proxy that wraps a read-only JsonObject
			Log("FromValue(Person)");
			var proxy = GeneratedSerializers.Person.ToMutable(person);
			Log(proxy.ToString());
			Assert.That(proxy.FamilyName, Is.EqualTo("Bond"));
			Assert.That(proxy.FirstName, Is.EqualTo("James"));

			// inspect the wrapped JsonObject
			Log("ToJson()");
			var json = proxy.ToJson();
			Dump(json);
			Assert.That(json, IsJson.Object.And.Mutable);
			Assert.That(json["familyName"], IsJson.EqualTo("Bond"));
			Assert.That(json["firstName"], IsJson.EqualTo("James"));
			Assert.That(json, IsJson.OfSize(2));

			// mutate the object
			proxy.FirstName = "Jim";

			Assert.That(proxy.FirstName, Is.EqualTo("Jim"));
			Assert.That(json["firstName"], IsJson.EqualTo("Jim"));
			// the original should not be changed
			Assert.That(person.FirstName, Is.EqualTo("James"));

			// serialize back into a Person
			Log("ToValue()");
			var decoded = proxy.ToValue();
			Assert.That(decoded, Is.Not.Null);
			Log(decoded.ToString());
			Assert.That(decoded, Is.InstanceOf<Person>().And.Not.SameAs(person));
			Assert.That(decoded.FamilyName, Is.EqualTo(person.FamilyName));
			Assert.That(decoded.FirstName, Is.EqualTo("Jim"));
		}

		[Test]
		public void Test_JsonReadOnlyProxy_Keeps_Extra_Fields()
		{
			var obj = JsonObject.ReadOnly.Create(
			[
				("familyName", "Bond"),
				("firstName", "James"),
				("hello", "world"),
				("foo", 123),
			]);
			Dump(obj);

			var proxy = new GeneratedSerializers.PersonReadOnly(obj);
			Log(proxy.ToString());
			Assert.That(proxy.FamilyName, Is.EqualTo("Bond"));
			Assert.That(proxy.FirstName, Is.EqualTo("James"));

			var updated = proxy.With(m => m.FirstName = "Jim");

			var export = updated.ToJson();
			Dump(obj);
			Assert.That(export, IsJson.Object);
			Assert.That(export["familyName"], IsJson.EqualTo("Bond"));
			Assert.That(export["firstName"], IsJson.EqualTo("Jim"));
			Assert.That(export["hello"], IsJson.EqualTo("world"));
			Assert.That(export["foo"], IsJson.EqualTo(123));
		}

		[Test]
		public void Test_JsonReadOnlyProxy_FromValue_ComplexType()
		{
			var user = MakeSampleUser();
			Log("User:");
			Log(user.ToString());

			Log("ReadOnly:");
			var proxy = GeneratedSerializers.MyAwesomeUser.AsReadOnly(user);
			Log(proxy.ToString());
			Assert.That(proxy.Id, Is.EqualTo(user.Id));
			Assert.That(proxy.DisplayName, Is.EqualTo(user.DisplayName));
			Assert.That(proxy.Metadata.AccountCreated, Is.EqualTo(user.Metadata.AccountCreated));
			Assert.That(proxy.Items[0].Id, Is.EqualTo(user.Items![0].Id));
			Assert.That(proxy.Devices["Foo"].Id, Is.EqualTo(user.Devices!["Foo"].Id));
			Assert.That(proxy.Extras, IsJson.ReadOnly.And.EqualTo(user.Extras));

			var json = proxy.ToJson();
			Log("JSON:");
			Dump(json);
			Assert.That(json, IsJson.Object.And.ReadOnly);
			Assert.That(json["id"], IsJson.EqualTo(user.Id));
			Assert.That(json["metadata"]["accountCreated"], IsJson.EqualTo(user.Metadata.AccountCreated));
			Assert.That(json["items"][0]["id"], IsJson.EqualTo(user.Items[0].Id));
			Assert.That(json["devices"]["Foo"]["id"], IsJson.EqualTo(user.Devices["Foo"].Id));
			Assert.That(json["extras"], IsJson.ReadOnly.And.EqualTo(user.Extras));

			var decoded = proxy.ToValue();
			Log("Decoded:");
			Log(decoded.ToString());
			Assert.That(decoded, Is.Not.Null);
			Assert.That(decoded.Id, Is.EqualTo(user.Id));
			Assert.That(decoded.DisplayName, Is.EqualTo(user.DisplayName));
		}

		[Test]
		public void Test_JsonMutableProxy_FromValue_ComplexType()
		{
			var user = MakeSampleUser();
			Log("User:");
			Log(user.ToString());

			Log("ReadOnly:");
			var proxy = GeneratedSerializers.MyAwesomeUser.ToMutable(user);
			Log(proxy.ToString());
			Assert.That(proxy.Id, Is.EqualTo(user.Id));
			Assert.That(proxy.DisplayName, Is.EqualTo(user.DisplayName));
			Assert.That(proxy.Metadata.AccountCreated, Is.EqualTo(user.Metadata.AccountCreated));
			Assert.That(proxy.Metadata.AccountModified, Is.EqualTo(user.Metadata.AccountModified));
			Assert.That(proxy.Items[0].Id, Is.EqualTo(user.Items![0].Id));
			Assert.That(proxy.Devices["Foo"].Id, Is.EqualTo(user.Devices!["Foo"].Id));
			Assert.That(proxy.Extras, IsJson.Mutable.And.EqualTo(user.Extras));

			Assert.That(proxy.Metadata.GetPath().ToString(), Is.EqualTo("metadata"));
			Assert.That(proxy.Items.GetPath().ToString(), Is.EqualTo("items"));
			Assert.That(proxy.Items[0].GetPath().ToString(), Is.EqualTo("items[0]"));
			Assert.That(proxy.Items[1].GetPath().ToString(), Is.EqualTo("items[1]"));
			Assert.That(proxy.Devices.GetPath().ToString(), Is.EqualTo("devices"));
			Assert.That(proxy.Devices["Foo"].GetPath().ToString(), Is.EqualTo("devices.Foo"));

			var json = proxy.ToJson();
			Log("JSON:");
			Dump(json);
			Assert.That(json, IsJson.Object.And.Mutable);
			Assert.That(json["id"], IsJson.EqualTo(user.Id));
			Assert.That(json["displayName"], IsJson.EqualTo(user.DisplayName));
			Assert.That(json["metadata"]["accountCreated"], IsJson.EqualTo(user.Metadata.AccountCreated));
			Assert.That(json["items"][0]["level"], IsJson.EqualTo(user.Items[0].Level));
			Assert.That(json["devices"]["Foo"]["id"], IsJson.EqualTo(user.Devices["Foo"].Id));
			Assert.That(json["extras"], IsJson.ReadOnly.And.EqualTo(user.Extras));

			// mutate

			proxy.DisplayName = "Jim Bond";
			proxy.Metadata.AccountModified = DateTimeOffset.Parse("2024-10-12T17:37:42.6732914Z");
			proxy.Items[0].Level = 8001;
			proxy.Items.Add(new MyAwesomeStruct() { Id = "47c774e7-29e7-40fe-ba06-3098cafe77be", Level = 789, Path = JsonPath.Create("hello") });
			proxy.Devices["Foo"].LastAddress = IPAddress.Parse("192.168.1.2");
			proxy.Devices.Remove("Bar");

			Log("Mutated:");
			Dump(proxy.ToJson());

			var decoded = proxy.ToValue();
			Log("Decoded:");
			Log(decoded.ToString());
			Assert.That(decoded, Is.Not.Null);
			Assert.That(decoded.Id, Is.EqualTo(user.Id));
			Assert.That(decoded.DisplayName, Is.EqualTo("Jim Bond"));
			Assert.That(decoded.Metadata.AccountCreated, Is.EqualTo(user.Metadata.AccountCreated));
			Assert.That(decoded.Metadata.AccountModified, Is.Not.EqualTo(user.Metadata.AccountCreated).And.EqualTo(DateTimeOffset.Parse("2024-10-12T17:37:42.6732914Z")));
			Assert.That(decoded.Items![0].Level, Is.EqualTo(8001));
			Assert.That(decoded.Items, Has.Count.EqualTo(3));
			Assert.That(decoded.Items![2].Level, Is.EqualTo(789));
			Assert.That(decoded.Devices!["Foo"].LastAddress, Is.EqualTo(IPAddress.Parse("192.168.1.2")));
			Assert.That(decoded.Devices, Does.Not.ContainKey("Bar"));
		}

		[Test]
		[Category("Benchmark")]
		public void Bench_Custom_Serializer()
		{
			var user = new MyAwesomeUser()
			{
				Id = "b6a16abe-e30c-4198-8358-5f0d8fd9c283",
				DisplayName = "James Bond",
				Email = "bond@example.org",
				Type = 007,
				Roles = [ "user", "secret_agent" ],
				Metadata = new ()
				{
					AccountCreated = DateTimeOffset.Parse("2024-09-20T12:34:56.7890123Z"),
					AccountModified = DateTimeOffset.Parse("2024-09-21T10:00:25.5461402Z"),
				},
				Items =
				[
					new() { Id = "382bb7cd-f9e4-4906-874e-ab88df954fa8", Level = 123, Path = JsonPath.Create("foo.bar") },
					new() { Id = "8092e57d-16b4-4afb-ae04-28acbeb22aa8", Level = 456, Path = JsonPath.Create("bars[2].foo"), Disabled = true }
				],
				Devices = new()
				{
					["Foo"] = new() { Id = "Foo", Model = "ACME Ultra Core 9100XX Ultra Series" },
					["Bar"] = new() { Id = "Bar", Model = "iHAL 42 Pro Ultra MaXX" },
				},
			};

			var stjOps = new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web);

			var json = CrystalJson.Serialize(user, GeneratedSerializers.MyAwesomeUser);
			var parsed = JsonValue.ParseObject(json);

			// warmup
			{
				_ = JsonValue.Parse(CrystalJson.Serialize(user)).As<MyAwesomeUser>();
				_ = GeneratedSerializers.MyAwesomeUser.Unpack(JsonValue.Parse(CrystalJson.Serialize(user, GeneratedSerializers.MyAwesomeUser)));
				_ = CrystalJson.Deserialize<MyAwesomeUser>(json);
				_ = System.Text.Json.JsonSerializer.Deserialize<MyAwesomeUser>(json, stjOps);
				_ = System.Text.Json.JsonSerializer.Deserialize<MyAwesomeUser>(json, SystemTextJsonGeneratedSerializers.Default.MyAwesomeUser);
			}

			Log($"JSON: {json.Length:N0} chars");

#if DEBUG
			const int RUNS = 25;
			const int ITERATIONS = 1_000;
#else
			const int RUNS = 50;
			const int ITERATIONS = 10_000;
#endif

			static void Report(string label, RobustBenchmark.Report<long> report)
			{
				Log($"* {label,-23}: {report.IterationsPerRun,7:N0} in {report.BestDuration.TotalMilliseconds,8:F1} ms at {report.BestIterationsPerSecond,10:N0} op/s ({report.BestIterationsNanos,9:N0} nanos), {(report.GcAllocatedOnThread / (report.NumberOfRuns * report.IterationsPerRun)),9:N0} allocated");
			}

			{
				var report = RobustBenchmark.Run(() => System.Text.Json.JsonSerializer.Serialize(user, stjOps), RUNS, ITERATIONS);
				Report("SERIALIZE TEXT STJ_DYN", report);
			}
			{
				var report = RobustBenchmark.Run(() => System.Text.Json.JsonSerializer.Serialize(user, SystemTextJsonGeneratedSerializers.Default.MyAwesomeUser), RUNS, ITERATIONS);
				Report("SERIALIZE TEXT STJ_GEN", report);
			}
			{
				var report = RobustBenchmark.Run(() => CrystalJson.Serialize(user), RUNS, ITERATIONS);
				Report("SERIALIZE TEXT CRY_DYN", report);
			}
			{
				var report = RobustBenchmark.Run(() => CrystalJson.Serialize(user, GeneratedSerializers.MyAwesomeUser), RUNS, ITERATIONS);
				Report("SERIALIZE TEXT CRY_GEN", report);
			}
			{
				var report = RobustBenchmark.Run(() => CrystalJson.ToSlice(user), RUNS, ITERATIONS);
				Report("SERIALIZE UTF8 CRY_DYN", report);
			}
			{
				var report = RobustBenchmark.Run(() => CrystalJson.ToSlice(user, GeneratedSerializers.MyAwesomeUser), RUNS, ITERATIONS);
				Report("SERIALIZE UTF8 CRY_GEN", report);
			}
			{
				var report = RobustBenchmark.Run(() =>
				{
					using var res = CrystalJson.ToSlice(user, GeneratedSerializers.MyAwesomeUser, ArrayPool<byte>.Shared);
					// use the JSON here to do something!
				}, RUNS, ITERATIONS);
				Report("SERIALIZE UTF8 POOLED", report);
			}

			{
				var report = RobustBenchmark.Run(() => System.Text.Json.JsonSerializer.Deserialize<MyAwesomeUser>(json, stjOps), RUNS, ITERATIONS);
				Report("DESERIALIZE STJ_DYN", report);
			}
			{
				var report = RobustBenchmark.Run(() => System.Text.Json.JsonSerializer.Deserialize<MyAwesomeUser>(json, SystemTextJsonGeneratedSerializers.Default.MyAwesomeUser), RUNS, ITERATIONS);
				Report("DESERIALIZE STJ_GEN", report);
			}
			{
				var report = RobustBenchmark.Run(() => CrystalJson.Deserialize<MyAwesomeUser>(json), RUNS, ITERATIONS);
				Report("DESERIALIZE RUNTIME", report);
			}
			{
				var report = RobustBenchmark.Run(() => GeneratedSerializers.MyAwesomeUser.Unpack(JsonValue.Parse(json)), RUNS, ITERATIONS);
				Report("DESERIALIZE CODEGEN", report);
			}

			{
				var report = RobustBenchmark.Run(() => parsed.As<MyAwesomeUser>(), RUNS, ITERATIONS);
				Report("AS<T> RUNTIME", report);
			}
			{
				var report = RobustBenchmark.Run(() => GeneratedSerializers.MyAwesomeUser.Unpack(parsed), RUNS, ITERATIONS);
				Report("AS<T> CODEGEN", report);
			}

			{
				var report = RobustBenchmark.Run(() => JsonValue.FromValue<MyAwesomeUser>(user), RUNS, ITERATIONS);
				Report("PACK RUNTIME", report);
			}
			{
				var report = RobustBenchmark.Run(() => GeneratedSerializers.MyAwesomeUser.Pack(user), RUNS, ITERATIONS);
				Report("PACK CODEGEN", report);
			}

		}

	}

}

#endif
