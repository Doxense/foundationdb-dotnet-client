// #region Copyright (c) 2023-2024 SnowBank SAS
// //
// // All rights are reserved. Reproduction or transmission in whole or in part, in
// // any form or by any means, electronic, mechanical or otherwise, is prohibited
// // without the prior written consent of the copyright owner.
// //
// #endregion

#if NET9_0_OR_GREATER

namespace Doxense.Serialization.Json.Tests
{
	using System.Buffers;
	using System.Collections.Generic;
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

		[JsonProperty("id")]
		public required string Id { get; init; }

		[JsonProperty("displayName")]
		public required string DisplayName { get; init; }

		[JsonProperty("email")]
		public required string Email { get; init; }

		[JsonProperty("type")]
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
		[JsonProperty("accountCreated")]
		public DateTimeOffset AccountCreated { get; init; }

		[JsonProperty("accountModified")]
		public DateTimeOffset AccountModified { get; init; }

		[JsonProperty("accountDisabled")]
		public DateTimeOffset? AccountDisabled { get; init; }
	}

	public record struct MyAwesomeStruct
	{
		[JsonProperty("id")]
		public required string Id { get; init; }

		[JsonProperty("level")]
		public required int Level { get; init; }

		[JsonProperty("path")]
		public required JsonPath Path { get; init; }

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
	public partial class SystemTextJsonGeneratedSerializers : System.Text.Json.Serialization.JsonSerializerContext
	{
	}

	#endregion

	[TestFixture]
	[Category("Core-SDK")]
	[Category("Core-JSON")]
	public class CrystalJsonSourceGeneratorFacts : SimpleTest
	{

		[Test]
		public void Test_Generate_Source_Code()
		{
			//HACKHACK: for the moment this dumps the code in the test log,
			// you are supposed to copy/paste this code and replace the block at the end of this file!
			// => later we may have a fancy source code generator that does this automatically :)

			Assume.That(typeof(string[]).GetFriendlyName(), Is.EqualTo("string[]"));
			Assume.That(TypeHelper.GetCompilableTypeName(typeof(string[]), omitNamespace: false, global: true), Is.EqualTo("string[]"));
			Assume.That(TypeHelper.GetCompilableTypeName(typeof(bool?[]), omitNamespace: false, global: true), Is.EqualTo("bool?[]"));

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
		}

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
				Log(parsed?.ToString());
				Assert.That(parsed, Is.Not.Null);
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
				Extras = JsonObject.Create([
					("hello", "world"),
					("foo", 123),
					("bar", JsonArray.Create([ 1, 2, 3 ])),
				]),
			};

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
		public void Test_ImpromptuReadOnly_Simple_Type()
		{
			{
				var person = new Person()
				{
					FamilyName = "Bond",
					FirstName = "James"
				};
				Log("Person:");
				Dump(person);

				Log("ReadOnly:");
				var ro = GeneratedSerializers.ImpromptuReadOnlyPerson.FromValue(person);
				Log(ro.ToString());
				Assert.That(ro.FamilyName, Is.EqualTo("Bond"));
				Assert.That(ro.FirstName, Is.EqualTo("James"));

				var json = ro.ToJson();
				Log("JSON:");
				Dump(json);
				Assert.That(json, IsJson.Object.And.ReadOnly);
				Assert.That(json["familyName"], IsJson.EqualTo("Bond"));
				Assert.That(json["firstName"], IsJson.EqualTo("James"));
				Assert.That(json, IsJson.OfSize(2));

			}
		}

		[Test]
		public void Test_ImpromptuReadOnly_Can_Mutate()
		{
			var person = new Person()
			{
				FamilyName = "Bond",
				FirstName = "James"
			};
			Log("Person:");
			Dump(person);

			Log("ReadOnly:");
			var ro = GeneratedSerializers.ImpromptuReadOnlyPerson.FromValue(person);
			Log(ro.ToString());
			Assert.That(ro.FamilyName, Is.EqualTo("Bond"));
			Assert.That(ro.FirstName, Is.EqualTo("James"));
			Assert.That(ro.ToJson(), IsJson.ReadOnly);

			var mutated = ro.With(m =>
			{
				Assert.That(m.FamilyName, Is.EqualTo("Bond"));
				Assert.That(m.FirstName, Is.EqualTo("James"));
				// the JSON should a mutable copy of the original
				Assert.That(m.ToJson(), IsJson.Object.And.Mutable.And.EqualTo(ro.ToJson()));

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
			Assert.That(ro.FamilyName, Is.EqualTo("Bond"));
			Assert.That(ro.FirstName, Is.EqualTo("James"));
		}

		[Test]
		public void Test_ImpromptuReadOnly_Keeps_Extra_Fields()
		{
			var obj = JsonObject.CreateReadOnly(
			[
				("familyName", "Bond"),
				("firstName", "James"),
				("hello", "world"),
				("foo", 123),
			]);
			Dump(obj);

			var ro = new GeneratedSerializers.ImpromptuReadOnlyPerson(obj);
			Log(ro.ToString());
			Assert.That(ro.FamilyName, Is.EqualTo("Bond"));
			Assert.That(ro.FirstName, Is.EqualTo("James"));

			var updated = ro.With(m => m.FirstName = "Jim");

			var export = updated.ToJson();
			Dump(obj);
			Assert.That(export, IsJson.Object);
			Assert.That(export["familyName"], IsJson.EqualTo("Bond"));
			Assert.That(export["firstName"], IsJson.EqualTo("Jim"));
			Assert.That(export["hello"], IsJson.EqualTo("world"));
			Assert.That(export["foo"], IsJson.EqualTo(123));
		}

		[Test]
		public void Test_ImpromptuReadOnly_Complex_Type()
		{
			var user = new MyAwesomeUser()
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
				Extras = JsonObject.Create([
					("hello", "world"),
					("foo", 123),
					("bar", JsonArray.Create([ 1, 2, 3 ])),
				]),
			};
			Log("User:");
			Log(user.ToString());

			Log("ReadOnly:");
			var ro = GeneratedSerializers.ImpromptuReadOnlyMyAwesomeUser.FromValue(user);
			Log(ro.ToString());
			Assert.That(ro.Id, Is.EqualTo(user.Id));
			Assert.That(ro.DisplayName, Is.EqualTo(user.DisplayName));

			var json = ro.ToJson();
			Log("JSON:");
			Dump(json);
			Assert.That(json, IsJson.Object.And.ReadOnly);
			Assert.That(json["id"], IsJson.EqualTo("b6a16abe-e30c-4198-8358-5f0d8fd9c283"));

			var decoded = ro.ToValue();
			Log("Decoded:");
			Log(decoded.ToString());
			Assert.That(decoded, Is.Not.Null);
			Assert.That(decoded.Id, Is.EqualTo(user.Id));
			Assert.That(decoded.DisplayName, Is.EqualTo(user.DisplayName));
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

			var stjOps = new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web) { };

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

namespace Doxense.Serialization.Json.Tests
{

	// ReSharper disable GrammarMistakeInComment
	// ReSharper disable InconsistentNaming
	// ReSharper disable JoinDeclarationAndInitializer
	// ReSharper disable PartialTypeWithSinglePart
	// ReSharper disable RedundantNameQualifier

	public partial class GeneratedSerializers
	{

		#region Person ...

		/// <summary>JSON converter for type <see cref="Doxense.Serialization.Json.Tests.Person">Person</see></summary>
		public static PersonJsonConverter Person => m_cachedPerson ??= new();

		private static PersonJsonConverter? m_cachedPerson;

		/// <summary>Converts instances of type <see cref="T:Doxense.Serialization.Json.Tests.Person">Person</see> to and from JSON.</summary>
		[global::System.Diagnostics.CodeAnalysis.DynamicallyAccessedMembers(global::System.Diagnostics.CodeAnalysis.DynamicallyAccessedMemberTypes.All)]
		[global::System.CodeDom.Compiler.GeneratedCode("CrystalJsonSourceGenerator", "0.1")]
		[global::System.Diagnostics.DebuggerNonUserCode()]
		public sealed class PersonJsonConverter : global::Doxense.Serialization.Json.IJsonConverter<global::Doxense.Serialization.Json.Tests.Person>
		{

			#region Serialization...

			private static readonly global::Doxense.Serialization.Json.JsonEncodedPropertyName _firstName = new("firstName");
			private static readonly global::Doxense.Serialization.Json.JsonEncodedPropertyName _familyName = new("familyName");

			public void Serialize(global::Doxense.Serialization.Json.CrystalJsonWriter writer, global::Doxense.Serialization.Json.Tests.Person? instance)
			{
				if (instance is null)
				{
					writer.WriteNull();
					return;
				}

				if (instance.GetType() != typeof(global::Doxense.Serialization.Json.Tests.Person))
				{
					global::Doxense.Serialization.Json.CrystalJsonVisitor.VisitValue(instance, typeof(global::Doxense.Serialization.Json.Tests.Person), writer);
					return;
				}

				var state = writer.BeginObject();

				// string FirstName => "firstName"
				// TODO: unsupported enumerable type: System.String
				// unknown type
				writer.WriteField(_firstName, instance.FirstName);

				// string FamilyName => "familyName"
				// TODO: unsupported enumerable type: System.String
				// unknown type
				writer.WriteField(_familyName, instance.FamilyName);

				writer.EndObject(state);
			}

			#endregion

			#region Packing...

			public global::Doxense.Serialization.Json.JsonValue Pack(global::Doxense.Serialization.Json.Tests.Person? instance, global::Doxense.Serialization.Json.CrystalJsonSettings? settings = default, global::Doxense.Serialization.Json.ICrystalJsonTypeResolver? resolver = default)
			{
				if (instance is null)
				{
					return global::Doxense.Serialization.Json.JsonNull.Null;
				}

				if (instance.GetType() != typeof(global::Doxense.Serialization.Json.Tests.Person))
				{
					return global::Doxense.Serialization.Json.JsonValue.FromValue(instance);
				}

				global::Doxense.Serialization.Json.JsonValue? value;
				var readOnly = settings?.ReadOnly ?? false;
				var keepNulls = settings?.ShowNullMembers ?? false;

				var obj = new global::Doxense.Serialization.Json.JsonObject(2);

				// string FirstName => "firstName"
				value = global::Doxense.Serialization.Json.JsonString.Return(instance.FirstName);
				if (keepNulls || value is not null or JsonNull)
				{
					obj["firstName"] = value;
				}

				// string FamilyName => "familyName"
				value = global::Doxense.Serialization.Json.JsonString.Return(instance.FamilyName);
				if (keepNulls || value is not null or JsonNull)
				{
					obj["familyName"] = value;
				}
				if (readOnly)
				{
					return FreezeUnsafe(obj);
				}

				return obj;
			}

			#endregion

			#region Deserialization...

			// FirstName { get; init; }
			[global::System.Runtime.CompilerServices.UnsafeAccessor(global::System.Runtime.CompilerServices.UnsafeAccessorKind.Field, Name = "<FirstName>k__BackingField")]
			private static extern ref string FirstNameAccessor(global::Doxense.Serialization.Json.Tests.Person instance);

			// FamilyName { get; init; }
			[global::System.Runtime.CompilerServices.UnsafeAccessor(global::System.Runtime.CompilerServices.UnsafeAccessorKind.Field, Name = "<FamilyName>k__BackingField")]
			private static extern ref string FamilyNameAccessor(global::Doxense.Serialization.Json.Tests.Person instance);

			public global::Doxense.Serialization.Json.Tests.Person Unpack(global::Doxense.Serialization.Json.JsonValue value, global::Doxense.Serialization.Json.ICrystalJsonTypeResolver? resolver = default)
			{
				var obj = value.AsObject();
				var instance = global::System.Activator.CreateInstance<global::Doxense.Serialization.Json.Tests.Person>();

				foreach (var kv in obj)
				{
					switch (kv.Key)
					{
						case "firstName": FirstNameAccessor(instance) = kv.Value.ToStringOrDefault(null)!; break;
						case "familyName": FamilyNameAccessor(instance) = kv.Value.ToStringOrDefault(null)!; break;
					}
				}

				return instance;
			}

			#endregion

		}


		/// <summary>Wraps a <see cref="JsonObject"/> into something that looks like a Person</summary>
		public readonly record struct ImpromptuReadOnlyPerson : global::Doxense.Serialization.Json.IJsonReadOnly<global::Doxense.Serialization.Json.Tests.Person, ImpromptuReadOnlyPerson, ImpromptuMutablePerson>
		{

			/// <summary>JSON Object that is wrapped</summary>
			private readonly global::Doxense.Serialization.Json.JsonObject m_obj;

			public ImpromptuReadOnlyPerson(global::Doxense.Serialization.Json.JsonObject obj) => m_obj = obj;

			/// <inheritdoc />
			public static ImpromptuReadOnlyPerson FromValue(global::Doxense.Serialization.Json.Tests.Person value)
			{
				global::Doxense.Diagnostics.Contracts.Contract.NotNull(value);
				return new((global::Doxense.Serialization.Json.JsonObject) GeneratedSerializers.Person.Pack(value, global::Doxense.Serialization.Json.CrystalJsonSettings.JsonReadOnly));
			}

			/// <inheritdoc />
			public global::Doxense.Serialization.Json.Tests.Person ToValue() => GeneratedSerializers.Person.Unpack(m_obj);

			/// <inheritdoc />
			public global::Doxense.Serialization.Json.JsonObject ToJson() => m_obj;

			/// <inheritdoc />
			public ImpromptuMutablePerson ToMutable() => new(m_obj.Copy());

			/// <inheritdoc />
			public ImpromptuReadOnlyPerson With(Action<ImpromptuMutablePerson> modifier)
			{
				var copy = m_obj.Copy();
				modifier(new(copy));
				return new(FreezeUnsafe(copy));
			}

			/// <inheritdoc />
			void global::Doxense.Serialization.Json.IJsonSerializable.JsonSerialize(global::Doxense.Serialization.Json.CrystalJsonWriter writer) => m_obj.JsonSerialize(writer);

			/// <inheritdoc />
			JsonValue global::Doxense.Serialization.Json.IJsonPackable.JsonPack(global::Doxense.Serialization.Json.CrystalJsonSettings settings, global::Doxense.Serialization.Json.ICrystalJsonTypeResolver resolver) => m_obj;

			/// <inheritdoc cref="Person.FirstName" />
			public string FirstName => m_obj.Get<string>("firstName");

			/// <inheritdoc cref="Person.FamilyName" />
			public string FamilyName => m_obj.Get<string>("familyName");

		}


		/// <summary>Wraps a <see cref="JsonObject"/> into something that looks like a Person</summary>
		public readonly record struct ImpromptuMutablePerson : global::Doxense.Serialization.Json.IJsonMutable<global::Doxense.Serialization.Json.Tests.Person, ImpromptuMutablePerson>
		{

			private readonly global::Doxense.Serialization.Json.JsonObject m_obj;

			public ImpromptuMutablePerson(global::Doxense.Serialization.Json.JsonObject obj) => m_obj = obj;

			/// <inheritdoc />
			public static ImpromptuMutablePerson FromValue(global::Doxense.Serialization.Json.Tests.Person value)
			{
				global::Doxense.Diagnostics.Contracts.Contract.NotNull(value);
				return new((global::Doxense.Serialization.Json.JsonObject) GeneratedSerializers.Person.Pack(value, global::Doxense.Serialization.Json.CrystalJsonSettings.Json));
			}

			/// <inheritdoc />
			public global::Doxense.Serialization.Json.JsonObject ToJson() => m_obj;

			/// <inheritdoc />
			public ImpromptuReadOnlyPerson ToReadOnly() => new (m_obj.ToReadOnly());

			/// <inheritdoc />
			void global::Doxense.Serialization.Json.IJsonSerializable.JsonSerialize(global::Doxense.Serialization.Json.CrystalJsonWriter writer) => m_obj.JsonSerialize(writer);

			/// <inheritdoc />
			JsonValue global::Doxense.Serialization.Json.IJsonPackable.JsonPack(global::Doxense.Serialization.Json.CrystalJsonSettings settings, global::Doxense.Serialization.Json.ICrystalJsonTypeResolver resolver) => settings.IsReadOnly() ? m_obj.ToReadOnly() : m_obj;

			/// <inheritdoc cref="Person.FirstName" />
			public string FirstName
			{
				get => m_obj.Get<string>("firstName");
				set => m_obj.Set<string>("firstName", value);
			}

			/// <inheritdoc cref="Person.FamilyName" />
			public string FamilyName
			{
				get => m_obj.Get<string>("familyName");
				set => m_obj.Set<string>("familyName", value);
			}

		}

		#endregion

		#region MyAwesomeUser ...

		/// <summary>JSON converter for type <see cref="Doxense.Serialization.Json.Tests.MyAwesomeUser">MyAwesomeUser</see></summary>
		public static MyAwesomeUserJsonConverter MyAwesomeUser => m_cachedMyAwesomeUser ??= new();

		private static MyAwesomeUserJsonConverter? m_cachedMyAwesomeUser;

		/// <summary>Converts instances of type <see cref="T:Doxense.Serialization.Json.Tests.MyAwesomeUser">MyAwesomeUser</see> to and from JSON.</summary>
		[global::System.Diagnostics.CodeAnalysis.DynamicallyAccessedMembers(global::System.Diagnostics.CodeAnalysis.DynamicallyAccessedMemberTypes.All)]
		[global::System.CodeDom.Compiler.GeneratedCode("CrystalJsonSourceGenerator", "0.1")]
		[global::System.Diagnostics.DebuggerNonUserCode()]
		public sealed class MyAwesomeUserJsonConverter : global::Doxense.Serialization.Json.IJsonConverter<global::Doxense.Serialization.Json.Tests.MyAwesomeUser>
		{

			#region Serialization...

			private static readonly global::Doxense.Serialization.Json.JsonEncodedPropertyName _id = new("id");
			private static readonly global::Doxense.Serialization.Json.JsonEncodedPropertyName _displayName = new("displayName");
			private static readonly global::Doxense.Serialization.Json.JsonEncodedPropertyName _email = new("email");
			private static readonly global::Doxense.Serialization.Json.JsonEncodedPropertyName _type = new("type");
			private static readonly global::Doxense.Serialization.Json.JsonEncodedPropertyName _roles = new("roles");
			private static readonly global::Doxense.Serialization.Json.JsonEncodedPropertyName _metadata = new("metadata");
			private static readonly global::Doxense.Serialization.Json.JsonEncodedPropertyName _items = new("items");
			private static readonly global::Doxense.Serialization.Json.JsonEncodedPropertyName _devices = new("devices");
			private static readonly global::Doxense.Serialization.Json.JsonEncodedPropertyName _extras = new("extras");

			public void Serialize(global::Doxense.Serialization.Json.CrystalJsonWriter writer, global::Doxense.Serialization.Json.Tests.MyAwesomeUser? instance)
			{
				if (instance is null)
				{
					writer.WriteNull();
					return;
				}

				var state = writer.BeginObject();

				// string Id => "id"
				// TODO: unsupported enumerable type: System.String
				// unknown type
				writer.WriteField(_id, instance.Id);

				// string DisplayName => "displayName"
				// TODO: unsupported enumerable type: System.String
				// unknown type
				writer.WriteField(_displayName, instance.DisplayName);

				// string Email => "email"
				// TODO: unsupported enumerable type: System.String
				// unknown type
				writer.WriteField(_email, instance.Email);

				// int Type => "type"
				// unknown type
				writer.WriteField(_type, instance.Type);

				// string[] Roles => "roles"
				// TODO: unsupported enumerable type: System.String[]
				// unknown type
				writer.WriteField(_roles, instance.Roles);

				// MyAwesomeMetadata Metadata => "metadata"
				// custom!
				writer.WriteField(_metadata, instance.Metadata, GeneratedSerializers.MyAwesomeMetadata);

				// List<MyAwesomeStruct> Items => "items"
				// custom array!
				writer.WriteFieldArray(_items, instance.Items, GeneratedSerializers.MyAwesomeStruct);

				// Dictionary<string, MyAwesomeDevice> Devices => "devices"
				// dictionary with string key
				writer.WriteFieldDictionary(_devices, instance.Devices, GeneratedSerializers.MyAwesomeDevice);

				// JsonObject Extras => "extras"
				// TODO: unsupported dictionary type: key=System.String, value=Doxense.Serialization.Json.JsonValue
				// unknown type
				writer.WriteField(_extras, instance.Extras);

				writer.EndObject(state);
			}

			#endregion

			#region Packing...

			public global::Doxense.Serialization.Json.JsonValue Pack(global::Doxense.Serialization.Json.Tests.MyAwesomeUser? instance, global::Doxense.Serialization.Json.CrystalJsonSettings? settings = default, global::Doxense.Serialization.Json.ICrystalJsonTypeResolver? resolver = default)
			{
				if (instance is null)
				{
					return global::Doxense.Serialization.Json.JsonNull.Null;
				}

				global::Doxense.Serialization.Json.JsonValue? value;
				var readOnly = settings?.ReadOnly ?? false;
				var keepNulls = settings?.ShowNullMembers ?? false;

				var obj = new global::Doxense.Serialization.Json.JsonObject(9);

				// string Id => "id"
				value = global::Doxense.Serialization.Json.JsonString.Return(instance.Id);
				if (keepNulls || value is not null or JsonNull)
				{
					obj["id"] = value;
				}

				// string DisplayName => "displayName"
				value = global::Doxense.Serialization.Json.JsonString.Return(instance.DisplayName);
				if (keepNulls || value is not null or JsonNull)
				{
					obj["displayName"] = value;
				}

				// string Email => "email"
				value = global::Doxense.Serialization.Json.JsonString.Return(instance.Email);
				if (keepNulls || value is not null or JsonNull)
				{
					obj["email"] = value;
				}

				// int Type => "type"
				// fast!
				value = global::Doxense.Serialization.Json.JsonNumber.Return(instance.Type);
				obj["type"] = value;

				// string[] Roles => "roles"
				value = global::Doxense.Serialization.Json.JsonSerializerExtensions.JsonPackArray(instance.Roles, settings, resolver);
				if (keepNulls || value is not null or JsonNull)
				{
					obj["roles"] = value;
				}

				// MyAwesomeMetadata Metadata => "metadata"
				// custom!
				value = GeneratedSerializers.MyAwesomeMetadata.Pack(instance.Metadata, settings, resolver);
				if (keepNulls || value is not null or JsonNull)
				{
					obj["metadata"] = value;
				}

				// List<MyAwesomeStruct> Items => "items"
				value = GeneratedSerializers.MyAwesomeStruct.JsonPackList(instance.Items, settings, resolver);
				if (keepNulls || value is not null or JsonNull)
				{
					obj["items"] = value;
				}

				// Dictionary<string, MyAwesomeDevice> Devices => "devices"
				value = GeneratedSerializers.MyAwesomeDevice.JsonPackObject(instance.Devices, settings, resolver);
				if (keepNulls || value is not null or JsonNull)
				{
					obj["devices"] = value;
				}

				// JsonObject Extras => "extras"
				value = readOnly ? instance.Extras?.ToReadOnly() : instance.Extras;
				if (keepNulls || value is not null or JsonNull)
				{
					obj["extras"] = value;
				}
				if (readOnly)
				{
					return FreezeUnsafe(obj);
				}

				return obj;
			}

			#endregion

			#region Deserialization...

			// Id { get; init; }
			[global::System.Runtime.CompilerServices.UnsafeAccessor(global::System.Runtime.CompilerServices.UnsafeAccessorKind.Field, Name = "<Id>k__BackingField")]
			private static extern ref string IdAccessor(global::Doxense.Serialization.Json.Tests.MyAwesomeUser instance);

			// DisplayName { get; init; }
			[global::System.Runtime.CompilerServices.UnsafeAccessor(global::System.Runtime.CompilerServices.UnsafeAccessorKind.Field, Name = "<DisplayName>k__BackingField")]
			private static extern ref string DisplayNameAccessor(global::Doxense.Serialization.Json.Tests.MyAwesomeUser instance);

			// Email { get; init; }
			[global::System.Runtime.CompilerServices.UnsafeAccessor(global::System.Runtime.CompilerServices.UnsafeAccessorKind.Field, Name = "<Email>k__BackingField")]
			private static extern ref string EmailAccessor(global::Doxense.Serialization.Json.Tests.MyAwesomeUser instance);

			// Type { get; init; }
			[global::System.Runtime.CompilerServices.UnsafeAccessor(global::System.Runtime.CompilerServices.UnsafeAccessorKind.Field, Name = "<Type>k__BackingField")]
			private static extern ref int TypeAccessor(global::Doxense.Serialization.Json.Tests.MyAwesomeUser instance);

			// Roles { get; init; }
			[global::System.Runtime.CompilerServices.UnsafeAccessor(global::System.Runtime.CompilerServices.UnsafeAccessorKind.Field, Name = "<Roles>k__BackingField")]
			private static extern ref string[] RolesAccessor(global::Doxense.Serialization.Json.Tests.MyAwesomeUser instance);

			// Metadata { get; init; }
			[global::System.Runtime.CompilerServices.UnsafeAccessor(global::System.Runtime.CompilerServices.UnsafeAccessorKind.Field, Name = "<Metadata>k__BackingField")]
			private static extern ref global::Doxense.Serialization.Json.Tests.MyAwesomeMetadata MetadataAccessor(global::Doxense.Serialization.Json.Tests.MyAwesomeUser instance);

			// Items { get; init; }
			[global::System.Runtime.CompilerServices.UnsafeAccessor(global::System.Runtime.CompilerServices.UnsafeAccessorKind.Field, Name = "<Items>k__BackingField")]
			private static extern ref global::System.Collections.Generic.List<global::Doxense.Serialization.Json.Tests.MyAwesomeStruct> ItemsAccessor(global::Doxense.Serialization.Json.Tests.MyAwesomeUser instance);

			// Devices { get; init; }
			[global::System.Runtime.CompilerServices.UnsafeAccessor(global::System.Runtime.CompilerServices.UnsafeAccessorKind.Field, Name = "<Devices>k__BackingField")]
			private static extern ref global::System.Collections.Generic.Dictionary<string, global::Doxense.Serialization.Json.Tests.MyAwesomeDevice> DevicesAccessor(global::Doxense.Serialization.Json.Tests.MyAwesomeUser instance);

			// Extras { get; init; }
			[global::System.Runtime.CompilerServices.UnsafeAccessor(global::System.Runtime.CompilerServices.UnsafeAccessorKind.Field, Name = "<Extras>k__BackingField")]
			private static extern ref global::Doxense.Serialization.Json.JsonObject ExtrasAccessor(global::Doxense.Serialization.Json.Tests.MyAwesomeUser instance);

			public global::Doxense.Serialization.Json.Tests.MyAwesomeUser Unpack(global::Doxense.Serialization.Json.JsonValue value, global::Doxense.Serialization.Json.ICrystalJsonTypeResolver? resolver = default)
			{
				var obj = value.AsObject();
				var instance = global::System.Activator.CreateInstance<global::Doxense.Serialization.Json.Tests.MyAwesomeUser>();

				foreach (var kv in obj)
				{
					switch (kv.Key)
					{
						case "id": IdAccessor(instance) = kv.Value.ToStringOrDefault(null)!; break;
						case "displayName": DisplayNameAccessor(instance) = kv.Value.ToStringOrDefault(null)!; break;
						case "email": EmailAccessor(instance) = kv.Value.ToStringOrDefault(null)!; break;
						case "type": TypeAccessor(instance) = kv.Value.ToInt32(); break;
						case "roles": RolesAccessor(instance) = kv.Value.AsArrayOrDefault()?.ToArray<string>(null, resolver)!; break;
						case "metadata": MetadataAccessor(instance) = GeneratedSerializers.MyAwesomeMetadata.Unpack(kv.Value, resolver)!; break;
						case "items": ItemsAccessor(instance) = GeneratedSerializers.MyAwesomeStruct.JsonDeserializeList(kv.Value, defaultValue: null, resolver: resolver)!; break;
						case "devices": DevicesAccessor(instance) = GeneratedSerializers.MyAwesomeDevice.JsonDeserializeDictionary(kv.Value, defaultValue: null, keyComparer: null, resolver: resolver)!; break;
						case "extras": ExtrasAccessor(instance) = kv.Value.AsObjectOrDefault()!; break;
					}
				}

				return instance;
			}

			#endregion

		}


		/// <summary>Wraps a <see cref="JsonObject"/> into something that looks like a MyAwesomeUser</summary>
		public readonly record struct ImpromptuReadOnlyMyAwesomeUser : global::Doxense.Serialization.Json.IJsonReadOnly<global::Doxense.Serialization.Json.Tests.MyAwesomeUser, ImpromptuReadOnlyMyAwesomeUser, ImpromptuMutableMyAwesomeUser>
		{

			/// <summary>JSON Object that is wrapped</summary>
			private readonly global::Doxense.Serialization.Json.JsonObject m_obj;

			public ImpromptuReadOnlyMyAwesomeUser(global::Doxense.Serialization.Json.JsonObject obj) => m_obj = obj;

			/// <inheritdoc />
			public static ImpromptuReadOnlyMyAwesomeUser FromValue(global::Doxense.Serialization.Json.Tests.MyAwesomeUser value)
			{
				global::Doxense.Diagnostics.Contracts.Contract.NotNull(value);
				return new((global::Doxense.Serialization.Json.JsonObject) GeneratedSerializers.MyAwesomeUser.Pack(value, global::Doxense.Serialization.Json.CrystalJsonSettings.JsonReadOnly));
			}

			/// <inheritdoc />
			public global::Doxense.Serialization.Json.Tests.MyAwesomeUser ToValue() => GeneratedSerializers.MyAwesomeUser.Unpack(m_obj);

			/// <inheritdoc />
			public global::Doxense.Serialization.Json.JsonObject ToJson() => m_obj;

			/// <inheritdoc />
			public ImpromptuMutableMyAwesomeUser ToMutable() => new(m_obj.Copy());

			/// <inheritdoc />
			public ImpromptuReadOnlyMyAwesomeUser With(Action<ImpromptuMutableMyAwesomeUser> modifier)
			{
				var copy = m_obj.Copy();
				modifier(new(copy));
				return new(FreezeUnsafe(copy));
			}

			/// <inheritdoc />
			void global::Doxense.Serialization.Json.IJsonSerializable.JsonSerialize(global::Doxense.Serialization.Json.CrystalJsonWriter writer) => m_obj.JsonSerialize(writer);

			/// <inheritdoc />
			JsonValue global::Doxense.Serialization.Json.IJsonPackable.JsonPack(global::Doxense.Serialization.Json.CrystalJsonSettings settings, global::Doxense.Serialization.Json.ICrystalJsonTypeResolver resolver) => m_obj;

			/// <inheritdoc cref="MyAwesomeUser.Id" />
			public string Id => m_obj.Get<string>("id");

			/// <inheritdoc cref="MyAwesomeUser.DisplayName" />
			public string DisplayName => m_obj.Get<string>("displayName");

			/// <inheritdoc cref="MyAwesomeUser.Email" />
			public string Email => m_obj.Get<string>("email");

			/// <inheritdoc cref="MyAwesomeUser.Type" />
			public int Type => m_obj.Get<int>("type");

			/// <inheritdoc cref="MyAwesomeUser.Roles" />
			public string[] Roles => m_obj.Get<string[]>("roles");

			/// <inheritdoc cref="MyAwesomeUser.Metadata" />
			public global::Doxense.Serialization.Json.Tests.MyAwesomeMetadata Metadata => m_obj.Get<global::Doxense.Serialization.Json.Tests.MyAwesomeMetadata>("metadata");

			/// <inheritdoc cref="MyAwesomeUser.Items" />
			public global::System.Collections.Generic.List<global::Doxense.Serialization.Json.Tests.MyAwesomeStruct> Items => m_obj.Get<global::System.Collections.Generic.List<global::Doxense.Serialization.Json.Tests.MyAwesomeStruct>>("items");

			/// <inheritdoc cref="MyAwesomeUser.Devices" />
			public global::System.Collections.Generic.Dictionary<string, global::Doxense.Serialization.Json.Tests.MyAwesomeDevice> Devices => m_obj.Get<global::System.Collections.Generic.Dictionary<string, global::Doxense.Serialization.Json.Tests.MyAwesomeDevice>>("devices");

			/// <inheritdoc cref="MyAwesomeUser.Extras" />
			public global::Doxense.Serialization.Json.JsonObject Extras => m_obj.Get<global::Doxense.Serialization.Json.JsonObject>("extras");

		}


		/// <summary>Wraps a <see cref="JsonObject"/> into something that looks like a MyAwesomeUser</summary>
		public readonly record struct ImpromptuMutableMyAwesomeUser : global::Doxense.Serialization.Json.IJsonMutable<global::Doxense.Serialization.Json.Tests.MyAwesomeUser, ImpromptuMutableMyAwesomeUser>
		{

			private readonly global::Doxense.Serialization.Json.JsonObject m_obj;

			public ImpromptuMutableMyAwesomeUser(global::Doxense.Serialization.Json.JsonObject obj) => m_obj = obj;

			/// <inheritdoc />
			public static ImpromptuMutableMyAwesomeUser FromValue(global::Doxense.Serialization.Json.Tests.MyAwesomeUser value)
			{
				global::Doxense.Diagnostics.Contracts.Contract.NotNull(value);
				return new((global::Doxense.Serialization.Json.JsonObject) GeneratedSerializers.MyAwesomeUser.Pack(value, global::Doxense.Serialization.Json.CrystalJsonSettings.Json));
			}

			/// <inheritdoc />
			public global::Doxense.Serialization.Json.JsonObject ToJson() => m_obj;

			/// <inheritdoc />
			public ImpromptuReadOnlyMyAwesomeUser ToReadOnly() => new (m_obj.ToReadOnly());

			/// <inheritdoc />
			void global::Doxense.Serialization.Json.IJsonSerializable.JsonSerialize(global::Doxense.Serialization.Json.CrystalJsonWriter writer) => m_obj.JsonSerialize(writer);

			/// <inheritdoc />
			JsonValue global::Doxense.Serialization.Json.IJsonPackable.JsonPack(global::Doxense.Serialization.Json.CrystalJsonSettings settings, global::Doxense.Serialization.Json.ICrystalJsonTypeResolver resolver) => settings.IsReadOnly() ? m_obj.ToReadOnly() : m_obj;

			/// <inheritdoc cref="MyAwesomeUser.Id" />
			public string Id
			{
				get => m_obj.Get<string>("id");
				set => m_obj.Set<string>("id", value);
			}

			/// <inheritdoc cref="MyAwesomeUser.DisplayName" />
			public string DisplayName
			{
				get => m_obj.Get<string>("displayName");
				set => m_obj.Set<string>("displayName", value);
			}

			/// <inheritdoc cref="MyAwesomeUser.Email" />
			public string Email
			{
				get => m_obj.Get<string>("email");
				set => m_obj.Set<string>("email", value);
			}

			/// <inheritdoc cref="MyAwesomeUser.Type" />
			public int Type
			{
				get => m_obj.Get<int>("type");
				set => m_obj.Set<int>("type", value);
			}

			/// <inheritdoc cref="MyAwesomeUser.Roles" />
			public string[] Roles
			{
				get => m_obj.Get<string[]>("roles");
				set => m_obj.Set<string[]>("roles", value);
			}

			/// <inheritdoc cref="MyAwesomeUser.Metadata" />
			public global::Doxense.Serialization.Json.Tests.MyAwesomeMetadata Metadata
			{
				get => m_obj.Get<global::Doxense.Serialization.Json.Tests.MyAwesomeMetadata>("metadata");
				set => m_obj.Set<global::Doxense.Serialization.Json.Tests.MyAwesomeMetadata>("metadata", value);
			}

			/// <inheritdoc cref="MyAwesomeUser.Items" />
			public global::System.Collections.Generic.List<global::Doxense.Serialization.Json.Tests.MyAwesomeStruct> Items
			{
				get => m_obj.Get<global::System.Collections.Generic.List<global::Doxense.Serialization.Json.Tests.MyAwesomeStruct>>("items");
				set => m_obj.Set<global::System.Collections.Generic.List<global::Doxense.Serialization.Json.Tests.MyAwesomeStruct>>("items", value);
			}

			/// <inheritdoc cref="MyAwesomeUser.Devices" />
			public global::System.Collections.Generic.Dictionary<string, global::Doxense.Serialization.Json.Tests.MyAwesomeDevice> Devices
			{
				get => m_obj.Get<global::System.Collections.Generic.Dictionary<string, global::Doxense.Serialization.Json.Tests.MyAwesomeDevice>>("devices");
				set => m_obj.Set<global::System.Collections.Generic.Dictionary<string, global::Doxense.Serialization.Json.Tests.MyAwesomeDevice>>("devices", value);
			}

			/// <inheritdoc cref="MyAwesomeUser.Extras" />
			public global::Doxense.Serialization.Json.JsonObject Extras
			{
				get => m_obj.Get<global::Doxense.Serialization.Json.JsonObject>("extras");
				set => m_obj.Set<global::Doxense.Serialization.Json.JsonObject>("extras", value);
			}

		}

		#endregion

		#region MyAwesomeMetadata ...

		/// <summary>JSON converter for type <see cref="Doxense.Serialization.Json.Tests.MyAwesomeMetadata">MyAwesomeMetadata</see></summary>
		public static MyAwesomeMetadataJsonConverter MyAwesomeMetadata => m_cachedMyAwesomeMetadata ??= new();

		private static MyAwesomeMetadataJsonConverter? m_cachedMyAwesomeMetadata;

		/// <summary>Converts instances of type <see cref="T:Doxense.Serialization.Json.Tests.MyAwesomeMetadata">MyAwesomeMetadata</see> to and from JSON.</summary>
		[global::System.Diagnostics.CodeAnalysis.DynamicallyAccessedMembers(global::System.Diagnostics.CodeAnalysis.DynamicallyAccessedMemberTypes.All)]
		[global::System.CodeDom.Compiler.GeneratedCode("CrystalJsonSourceGenerator", "0.1")]
		[global::System.Diagnostics.DebuggerNonUserCode()]
		public sealed class MyAwesomeMetadataJsonConverter : global::Doxense.Serialization.Json.IJsonConverter<global::Doxense.Serialization.Json.Tests.MyAwesomeMetadata>
		{

			#region Serialization...

			private static readonly global::Doxense.Serialization.Json.JsonEncodedPropertyName _accountCreated = new("accountCreated");
			private static readonly global::Doxense.Serialization.Json.JsonEncodedPropertyName _accountModified = new("accountModified");
			private static readonly global::Doxense.Serialization.Json.JsonEncodedPropertyName _accountDisabled = new("accountDisabled");

			public void Serialize(global::Doxense.Serialization.Json.CrystalJsonWriter writer, global::Doxense.Serialization.Json.Tests.MyAwesomeMetadata? instance)
			{
				if (instance is null)
				{
					writer.WriteNull();
					return;
				}

				var state = writer.BeginObject();

				// DateTimeOffset AccountCreated => "accountCreated"
				// unknown type
				writer.WriteField(_accountCreated, instance.AccountCreated);

				// DateTimeOffset AccountModified => "accountModified"
				// unknown type
				writer.WriteField(_accountModified, instance.AccountModified);

				// DateTimeOffset? AccountDisabled => "accountDisabled"
				// unknown type
				writer.WriteField(_accountDisabled, instance.AccountDisabled);

				writer.EndObject(state);
			}

			#endregion

			#region Packing...

			public global::Doxense.Serialization.Json.JsonValue Pack(global::Doxense.Serialization.Json.Tests.MyAwesomeMetadata? instance, global::Doxense.Serialization.Json.CrystalJsonSettings? settings = default, global::Doxense.Serialization.Json.ICrystalJsonTypeResolver? resolver = default)
			{
				if (instance is null)
				{
					return global::Doxense.Serialization.Json.JsonNull.Null;
				}

				global::Doxense.Serialization.Json.JsonValue? value;
				var readOnly = settings?.ReadOnly ?? false;
				var keepNulls = settings?.ShowNullMembers ?? false;

				var obj = new global::Doxense.Serialization.Json.JsonObject(3);

				// DateTimeOffset AccountCreated => "accountCreated"
				// fast!
				value = global::Doxense.Serialization.Json.JsonDateTime.Return(instance.AccountCreated);
				obj["accountCreated"] = value;

				// DateTimeOffset AccountModified => "accountModified"
				// fast!
				value = global::Doxense.Serialization.Json.JsonDateTime.Return(instance.AccountModified);
				obj["accountModified"] = value;

				// DateTimeOffset? AccountDisabled => "accountDisabled"
				// fast!
				{
					var tmp = instance.AccountDisabled;
					value = tmp.HasValue ? global::Doxense.Serialization.Json.JsonDateTime.Return(tmp.Value) : null;
					if (keepNulls || value is not null or JsonNull)
					{
						obj["accountDisabled"] = value;
					}
				}
				if (readOnly)
				{
					return FreezeUnsafe(obj);
				}

				return obj;
			}

			#endregion

			#region Deserialization...

			// AccountCreated { get; init; }
			[global::System.Runtime.CompilerServices.UnsafeAccessor(global::System.Runtime.CompilerServices.UnsafeAccessorKind.Field, Name = "<AccountCreated>k__BackingField")]
			private static extern ref global::System.DateTimeOffset AccountCreatedAccessor(global::Doxense.Serialization.Json.Tests.MyAwesomeMetadata instance);

			// AccountModified { get; init; }
			[global::System.Runtime.CompilerServices.UnsafeAccessor(global::System.Runtime.CompilerServices.UnsafeAccessorKind.Field, Name = "<AccountModified>k__BackingField")]
			private static extern ref global::System.DateTimeOffset AccountModifiedAccessor(global::Doxense.Serialization.Json.Tests.MyAwesomeMetadata instance);

			// AccountDisabled { get; init; }
			[global::System.Runtime.CompilerServices.UnsafeAccessor(global::System.Runtime.CompilerServices.UnsafeAccessorKind.Field, Name = "<AccountDisabled>k__BackingField")]
			private static extern ref global::System.DateTimeOffset? AccountDisabledAccessor(global::Doxense.Serialization.Json.Tests.MyAwesomeMetadata instance);

			public global::Doxense.Serialization.Json.Tests.MyAwesomeMetadata Unpack(global::Doxense.Serialization.Json.JsonValue value, global::Doxense.Serialization.Json.ICrystalJsonTypeResolver? resolver = default)
			{
				var obj = value.AsObject();
				var instance = global::System.Activator.CreateInstance<global::Doxense.Serialization.Json.Tests.MyAwesomeMetadata>();

				foreach (var kv in obj)
				{
					switch (kv.Key)
					{
						case "accountCreated": AccountCreatedAccessor(instance) = kv.Value.ToDateTimeOffset(); break;
						case "accountModified": AccountModifiedAccessor(instance) = kv.Value.ToDateTimeOffset(); break;
						case "accountDisabled": AccountDisabledAccessor(instance) = kv.Value.ToDateTimeOffsetOrDefault(null); break;
					}
				}

				return instance;
			}

			#endregion

		}


		/// <summary>Wraps a <see cref="JsonObject"/> into something that looks like a MyAwesomeMetadata</summary>
		public readonly record struct ImpromptuReadOnlyMyAwesomeMetadata : global::Doxense.Serialization.Json.IJsonReadOnly<global::Doxense.Serialization.Json.Tests.MyAwesomeMetadata, ImpromptuReadOnlyMyAwesomeMetadata, ImpromptuMutableMyAwesomeMetadata>
		{

			/// <summary>JSON Object that is wrapped</summary>
			private readonly global::Doxense.Serialization.Json.JsonObject m_obj;

			public ImpromptuReadOnlyMyAwesomeMetadata(global::Doxense.Serialization.Json.JsonObject obj) => m_obj = obj;

			/// <inheritdoc />
			public static ImpromptuReadOnlyMyAwesomeMetadata FromValue(global::Doxense.Serialization.Json.Tests.MyAwesomeMetadata value)
			{
				global::Doxense.Diagnostics.Contracts.Contract.NotNull(value);
				return new((global::Doxense.Serialization.Json.JsonObject) GeneratedSerializers.MyAwesomeMetadata.Pack(value, global::Doxense.Serialization.Json.CrystalJsonSettings.JsonReadOnly));
			}

			/// <inheritdoc />
			public global::Doxense.Serialization.Json.Tests.MyAwesomeMetadata ToValue() => GeneratedSerializers.MyAwesomeMetadata.Unpack(m_obj);

			/// <inheritdoc />
			public global::Doxense.Serialization.Json.JsonObject ToJson() => m_obj;

			/// <inheritdoc />
			public ImpromptuMutableMyAwesomeMetadata ToMutable() => new(m_obj.Copy());

			/// <inheritdoc />
			public ImpromptuReadOnlyMyAwesomeMetadata With(Action<ImpromptuMutableMyAwesomeMetadata> modifier)
			{
				var copy = m_obj.Copy();
				modifier(new(copy));
				return new(FreezeUnsafe(copy));
			}

			/// <inheritdoc />
			void global::Doxense.Serialization.Json.IJsonSerializable.JsonSerialize(global::Doxense.Serialization.Json.CrystalJsonWriter writer) => m_obj.JsonSerialize(writer);

			/// <inheritdoc />
			JsonValue global::Doxense.Serialization.Json.IJsonPackable.JsonPack(global::Doxense.Serialization.Json.CrystalJsonSettings settings, global::Doxense.Serialization.Json.ICrystalJsonTypeResolver resolver) => m_obj;

			/// <inheritdoc cref="MyAwesomeMetadata.AccountCreated" />
			public global::System.DateTimeOffset AccountCreated => m_obj.Get<global::System.DateTimeOffset>("accountCreated");

			/// <inheritdoc cref="MyAwesomeMetadata.AccountModified" />
			public global::System.DateTimeOffset AccountModified => m_obj.Get<global::System.DateTimeOffset>("accountModified");

			/// <inheritdoc cref="MyAwesomeMetadata.AccountDisabled" />
			public global::System.DateTimeOffset? AccountDisabled => m_obj.Get<global::System.DateTimeOffset?>("accountDisabled");

		}


		/// <summary>Wraps a <see cref="JsonObject"/> into something that looks like a MyAwesomeMetadata</summary>
		public readonly record struct ImpromptuMutableMyAwesomeMetadata : global::Doxense.Serialization.Json.IJsonMutable<global::Doxense.Serialization.Json.Tests.MyAwesomeMetadata, ImpromptuMutableMyAwesomeMetadata>
		{

			private readonly global::Doxense.Serialization.Json.JsonObject m_obj;

			public ImpromptuMutableMyAwesomeMetadata(global::Doxense.Serialization.Json.JsonObject obj) => m_obj = obj;

			/// <inheritdoc />
			public static ImpromptuMutableMyAwesomeMetadata FromValue(global::Doxense.Serialization.Json.Tests.MyAwesomeMetadata value)
			{
				global::Doxense.Diagnostics.Contracts.Contract.NotNull(value);
				return new((global::Doxense.Serialization.Json.JsonObject) GeneratedSerializers.MyAwesomeMetadata.Pack(value, global::Doxense.Serialization.Json.CrystalJsonSettings.Json));
			}

			/// <inheritdoc />
			public global::Doxense.Serialization.Json.JsonObject ToJson() => m_obj;

			/// <inheritdoc />
			public ImpromptuReadOnlyMyAwesomeMetadata ToReadOnly() => new (m_obj.ToReadOnly());

			/// <inheritdoc />
			void global::Doxense.Serialization.Json.IJsonSerializable.JsonSerialize(global::Doxense.Serialization.Json.CrystalJsonWriter writer) => m_obj.JsonSerialize(writer);

			/// <inheritdoc />
			JsonValue global::Doxense.Serialization.Json.IJsonPackable.JsonPack(global::Doxense.Serialization.Json.CrystalJsonSettings settings, global::Doxense.Serialization.Json.ICrystalJsonTypeResolver resolver) => settings.IsReadOnly() ? m_obj.ToReadOnly() : m_obj;

			/// <inheritdoc cref="MyAwesomeMetadata.AccountCreated" />
			public global::System.DateTimeOffset AccountCreated
			{
				get => m_obj.Get<global::System.DateTimeOffset>("accountCreated");
				set => m_obj.Set<global::System.DateTimeOffset>("accountCreated", value);
			}

			/// <inheritdoc cref="MyAwesomeMetadata.AccountModified" />
			public global::System.DateTimeOffset AccountModified
			{
				get => m_obj.Get<global::System.DateTimeOffset>("accountModified");
				set => m_obj.Set<global::System.DateTimeOffset>("accountModified", value);
			}

			/// <inheritdoc cref="MyAwesomeMetadata.AccountDisabled" />
			public global::System.DateTimeOffset? AccountDisabled
			{
				get => m_obj.Get<global::System.DateTimeOffset?>("accountDisabled");
				set => m_obj.Set<global::System.DateTimeOffset?>("accountDisabled", value);
			}

		}

		#endregion

		#region MyAwesomeStruct ...

		/// <summary>JSON converter for type <see cref="Doxense.Serialization.Json.Tests.MyAwesomeStruct">MyAwesomeStruct</see></summary>
		public static MyAwesomeStructJsonConverter MyAwesomeStruct => m_cachedMyAwesomeStruct ??= new();

		private static MyAwesomeStructJsonConverter? m_cachedMyAwesomeStruct;

		/// <summary>Converts instances of type <see cref="T:Doxense.Serialization.Json.Tests.MyAwesomeStruct">MyAwesomeStruct</see> to and from JSON.</summary>
		[global::System.Diagnostics.CodeAnalysis.DynamicallyAccessedMembers(global::System.Diagnostics.CodeAnalysis.DynamicallyAccessedMemberTypes.All)]
		[global::System.CodeDom.Compiler.GeneratedCode("CrystalJsonSourceGenerator", "0.1")]
		[global::System.Diagnostics.DebuggerNonUserCode()]
		public sealed class MyAwesomeStructJsonConverter : global::Doxense.Serialization.Json.IJsonConverter<global::Doxense.Serialization.Json.Tests.MyAwesomeStruct>
		{

			#region Serialization...

			private static readonly global::Doxense.Serialization.Json.JsonEncodedPropertyName _id = new("id");
			private static readonly global::Doxense.Serialization.Json.JsonEncodedPropertyName _level = new("level");
			private static readonly global::Doxense.Serialization.Json.JsonEncodedPropertyName _path = new("path");
			private static readonly global::Doxense.Serialization.Json.JsonEncodedPropertyName _disabled = new("disabled");

			public void Serialize(global::Doxense.Serialization.Json.CrystalJsonWriter writer, global::Doxense.Serialization.Json.Tests.MyAwesomeStruct instance)
			{
				var state = writer.BeginObject();

				// string Id => "id"
				// TODO: unsupported enumerable type: System.String
				// unknown type
				writer.WriteField(_id, instance.Id);

				// int Level => "level"
				// unknown type
				writer.WriteField(_level, instance.Level);

				// JsonPath Path => "path"
				// TODO: unsupported enumerable type: Doxense.Serialization.Json.JsonPath
				// unknown type
				writer.WriteField(_path, instance.Path);

				// bool? Disabled => "disabled"
				// unknown type
				writer.WriteField(_disabled, instance.Disabled);

				writer.EndObject(state);
			}

			#endregion

			#region Packing...

			public global::Doxense.Serialization.Json.JsonValue Pack(global::Doxense.Serialization.Json.Tests.MyAwesomeStruct instance, global::Doxense.Serialization.Json.CrystalJsonSettings? settings = default, global::Doxense.Serialization.Json.ICrystalJsonTypeResolver? resolver = default)
			{
				global::Doxense.Serialization.Json.JsonValue? value;
				var readOnly = settings?.ReadOnly ?? false;
				var keepNulls = settings?.ShowNullMembers ?? false;

				var obj = new global::Doxense.Serialization.Json.JsonObject(4);

				// string Id => "id"
				value = global::Doxense.Serialization.Json.JsonString.Return(instance.Id);
				if (keepNulls || value is not null or JsonNull)
				{
					obj["id"] = value;
				}

				// int Level => "level"
				// fast!
				value = global::Doxense.Serialization.Json.JsonNumber.Return(instance.Level);
				obj["level"] = value;

				// JsonPath Path => "path"
				// fast!
				value = JsonValue.FromValue<global::Doxense.Serialization.Json.JsonPath>(instance.Path);
				obj["path"] = value;

				// bool? Disabled => "disabled"
				// fast!
				{
					var tmp = instance.Disabled;
					value = tmp.HasValue ? JsonBoolean.Return(tmp.Value) : null;
					if (keepNulls || value is not null or JsonNull)
					{
						obj["disabled"] = value;
					}
				}
				if (readOnly)
				{
					return FreezeUnsafe(obj);
				}

				return obj;
			}

			#endregion

			#region Deserialization...

			// Id { get; init; }
			[global::System.Runtime.CompilerServices.UnsafeAccessor(global::System.Runtime.CompilerServices.UnsafeAccessorKind.Field, Name = "<Id>k__BackingField")]
			private static extern ref string IdAccessor(ref global::Doxense.Serialization.Json.Tests.MyAwesomeStruct instance);

			// Level { get; init; }
			[global::System.Runtime.CompilerServices.UnsafeAccessor(global::System.Runtime.CompilerServices.UnsafeAccessorKind.Field, Name = "<Level>k__BackingField")]
			private static extern ref int LevelAccessor(ref global::Doxense.Serialization.Json.Tests.MyAwesomeStruct instance);

			// Path { get; init; }
			[global::System.Runtime.CompilerServices.UnsafeAccessor(global::System.Runtime.CompilerServices.UnsafeAccessorKind.Field, Name = "<Path>k__BackingField")]
			private static extern ref global::Doxense.Serialization.Json.JsonPath PathAccessor(ref global::Doxense.Serialization.Json.Tests.MyAwesomeStruct instance);

			// Disabled { get; init; }
			[global::System.Runtime.CompilerServices.UnsafeAccessor(global::System.Runtime.CompilerServices.UnsafeAccessorKind.Field, Name = "<Disabled>k__BackingField")]
			private static extern ref bool? DisabledAccessor(ref global::Doxense.Serialization.Json.Tests.MyAwesomeStruct instance);

			public global::Doxense.Serialization.Json.Tests.MyAwesomeStruct Unpack(global::Doxense.Serialization.Json.JsonValue value, global::Doxense.Serialization.Json.ICrystalJsonTypeResolver? resolver = default)
			{
				var obj = value.AsObject();
				var instance = global::System.Activator.CreateInstance<global::Doxense.Serialization.Json.Tests.MyAwesomeStruct>();

				foreach (var kv in obj)
				{
					switch (kv.Key)
					{
						case "id": IdAccessor(ref instance) = kv.Value.ToStringOrDefault(null)!; break;
						case "level": LevelAccessor(ref instance) = kv.Value.ToInt32(); break;
						case "path": PathAccessor(ref instance) = global::Doxense.Serialization.Json.JsonSerializerExtensions.Unpack<global::Doxense.Serialization.Json.JsonPath>(kv.Value, resolver)!; break;
						case "disabled": DisabledAccessor(ref instance) = kv.Value.ToBooleanOrDefault(null); break;
					}
				}

				return instance;
			}

			#endregion

		}


		/// <summary>Wraps a <see cref="JsonObject"/> into something that looks like a MyAwesomeStruct</summary>
		public readonly record struct ImpromptuReadOnlyMyAwesomeStruct : global::Doxense.Serialization.Json.IJsonReadOnly<global::Doxense.Serialization.Json.Tests.MyAwesomeStruct, ImpromptuReadOnlyMyAwesomeStruct, ImpromptuMutableMyAwesomeStruct>
		{

			/// <summary>JSON Object that is wrapped</summary>
			private readonly global::Doxense.Serialization.Json.JsonObject m_obj;

			public ImpromptuReadOnlyMyAwesomeStruct(global::Doxense.Serialization.Json.JsonObject obj) => m_obj = obj;

			/// <inheritdoc />
			public static ImpromptuReadOnlyMyAwesomeStruct FromValue(global::Doxense.Serialization.Json.Tests.MyAwesomeStruct value)
			{
				global::Doxense.Diagnostics.Contracts.Contract.NotNull(value);
				return new((global::Doxense.Serialization.Json.JsonObject) GeneratedSerializers.MyAwesomeStruct.Pack(value, global::Doxense.Serialization.Json.CrystalJsonSettings.JsonReadOnly));
			}

			/// <inheritdoc />
			public global::Doxense.Serialization.Json.Tests.MyAwesomeStruct ToValue() => GeneratedSerializers.MyAwesomeStruct.Unpack(m_obj);

			/// <inheritdoc />
			public global::Doxense.Serialization.Json.JsonObject ToJson() => m_obj;

			/// <inheritdoc />
			public ImpromptuMutableMyAwesomeStruct ToMutable() => new(m_obj.Copy());

			/// <inheritdoc />
			public ImpromptuReadOnlyMyAwesomeStruct With(Action<ImpromptuMutableMyAwesomeStruct> modifier)
			{
				var copy = m_obj.Copy();
				modifier(new(copy));
				return new(FreezeUnsafe(copy));
			}

			/// <inheritdoc />
			void global::Doxense.Serialization.Json.IJsonSerializable.JsonSerialize(global::Doxense.Serialization.Json.CrystalJsonWriter writer) => m_obj.JsonSerialize(writer);

			/// <inheritdoc />
			JsonValue global::Doxense.Serialization.Json.IJsonPackable.JsonPack(global::Doxense.Serialization.Json.CrystalJsonSettings settings, global::Doxense.Serialization.Json.ICrystalJsonTypeResolver resolver) => m_obj;

			/// <inheritdoc cref="MyAwesomeStruct.Id" />
			public string Id => m_obj.Get<string>("id");

			/// <inheritdoc cref="MyAwesomeStruct.Level" />
			public int Level => m_obj.Get<int>("level");

			/// <inheritdoc cref="MyAwesomeStruct.Path" />
			public global::Doxense.Serialization.Json.JsonPath Path => m_obj.Get<global::Doxense.Serialization.Json.JsonPath>("path");

			/// <inheritdoc cref="MyAwesomeStruct.Disabled" />
			public bool? Disabled => m_obj.Get<bool?>("disabled");

		}


		/// <summary>Wraps a <see cref="JsonObject"/> into something that looks like a MyAwesomeStruct</summary>
		public readonly record struct ImpromptuMutableMyAwesomeStruct : global::Doxense.Serialization.Json.IJsonMutable<global::Doxense.Serialization.Json.Tests.MyAwesomeStruct, ImpromptuMutableMyAwesomeStruct>
		{

			private readonly global::Doxense.Serialization.Json.JsonObject m_obj;

			public ImpromptuMutableMyAwesomeStruct(global::Doxense.Serialization.Json.JsonObject obj) => m_obj = obj;

			/// <inheritdoc />
			public static ImpromptuMutableMyAwesomeStruct FromValue(global::Doxense.Serialization.Json.Tests.MyAwesomeStruct value)
			{
				global::Doxense.Diagnostics.Contracts.Contract.NotNull(value);
				return new((global::Doxense.Serialization.Json.JsonObject) GeneratedSerializers.MyAwesomeStruct.Pack(value, global::Doxense.Serialization.Json.CrystalJsonSettings.Json));
			}

			/// <inheritdoc />
			public global::Doxense.Serialization.Json.JsonObject ToJson() => m_obj;

			/// <inheritdoc />
			public ImpromptuReadOnlyMyAwesomeStruct ToReadOnly() => new (m_obj.ToReadOnly());

			/// <inheritdoc />
			void global::Doxense.Serialization.Json.IJsonSerializable.JsonSerialize(global::Doxense.Serialization.Json.CrystalJsonWriter writer) => m_obj.JsonSerialize(writer);

			/// <inheritdoc />
			JsonValue global::Doxense.Serialization.Json.IJsonPackable.JsonPack(global::Doxense.Serialization.Json.CrystalJsonSettings settings, global::Doxense.Serialization.Json.ICrystalJsonTypeResolver resolver) => settings.IsReadOnly() ? m_obj.ToReadOnly() : m_obj;

			/// <inheritdoc cref="MyAwesomeStruct.Id" />
			public string Id
			{
				get => m_obj.Get<string>("id");
				set => m_obj.Set<string>("id", value);
			}

			/// <inheritdoc cref="MyAwesomeStruct.Level" />
			public int Level
			{
				get => m_obj.Get<int>("level");
				set => m_obj.Set<int>("level", value);
			}

			/// <inheritdoc cref="MyAwesomeStruct.Path" />
			public global::Doxense.Serialization.Json.JsonPath Path
			{
				get => m_obj.Get<global::Doxense.Serialization.Json.JsonPath>("path");
				set => m_obj.Set<global::Doxense.Serialization.Json.JsonPath>("path", value);
			}

			/// <inheritdoc cref="MyAwesomeStruct.Disabled" />
			public bool? Disabled
			{
				get => m_obj.Get<bool?>("disabled");
				set => m_obj.Set<bool?>("disabled", value);
			}

		}

		#endregion

		#region MyAwesomeDevice ...

		/// <summary>JSON converter for type <see cref="Doxense.Serialization.Json.Tests.MyAwesomeDevice">MyAwesomeDevice</see></summary>
		public static MyAwesomeDeviceJsonConverter MyAwesomeDevice => m_cachedMyAwesomeDevice ??= new();

		private static MyAwesomeDeviceJsonConverter? m_cachedMyAwesomeDevice;

		/// <summary>Converts instances of type <see cref="T:Doxense.Serialization.Json.Tests.MyAwesomeDevice">MyAwesomeDevice</see> to and from JSON.</summary>
		[global::System.Diagnostics.CodeAnalysis.DynamicallyAccessedMembers(global::System.Diagnostics.CodeAnalysis.DynamicallyAccessedMemberTypes.All)]
		[global::System.CodeDom.Compiler.GeneratedCode("CrystalJsonSourceGenerator", "0.1")]
		[global::System.Diagnostics.DebuggerNonUserCode()]
		public sealed class MyAwesomeDeviceJsonConverter : global::Doxense.Serialization.Json.IJsonConverter<global::Doxense.Serialization.Json.Tests.MyAwesomeDevice>
		{

			#region Serialization...

			private static readonly global::Doxense.Serialization.Json.JsonEncodedPropertyName _id = new("id");
			private static readonly global::Doxense.Serialization.Json.JsonEncodedPropertyName _model = new("model");
			private static readonly global::Doxense.Serialization.Json.JsonEncodedPropertyName _lastSeen = new("lastSeen");
			private static readonly global::Doxense.Serialization.Json.JsonEncodedPropertyName _lastAddress = new("lastAddress");

			public void Serialize(global::Doxense.Serialization.Json.CrystalJsonWriter writer, global::Doxense.Serialization.Json.Tests.MyAwesomeDevice? instance)
			{
				if (instance is null)
				{
					writer.WriteNull();
					return;
				}

				var state = writer.BeginObject();

				// string Id => "id"
				// TODO: unsupported enumerable type: System.String
				// unknown type
				writer.WriteField(_id, instance.Id);

				// string Model => "model"
				// TODO: unsupported enumerable type: System.String
				// unknown type
				writer.WriteField(_model, instance.Model);

				// DateTimeOffset? LastSeen => "lastSeen"
				// unknown type
				writer.WriteField(_lastSeen, instance.LastSeen);

				// IPAddress LastAddress => "lastAddress"
				// unknown type
				writer.WriteField(_lastAddress, instance.LastAddress);

				writer.EndObject(state);
			}

			#endregion

			#region Packing...

			public global::Doxense.Serialization.Json.JsonValue Pack(global::Doxense.Serialization.Json.Tests.MyAwesomeDevice? instance, global::Doxense.Serialization.Json.CrystalJsonSettings? settings = default, global::Doxense.Serialization.Json.ICrystalJsonTypeResolver? resolver = default)
			{
				if (instance is null)
				{
					return global::Doxense.Serialization.Json.JsonNull.Null;
				}

				global::Doxense.Serialization.Json.JsonValue? value;
				var readOnly = settings?.ReadOnly ?? false;
				var keepNulls = settings?.ShowNullMembers ?? false;

				var obj = new global::Doxense.Serialization.Json.JsonObject(4);

				// string Id => "id"
				value = global::Doxense.Serialization.Json.JsonString.Return(instance.Id);
				if (keepNulls || value is not null or JsonNull)
				{
					obj["id"] = value;
				}

				// string Model => "model"
				value = global::Doxense.Serialization.Json.JsonString.Return(instance.Model);
				if (keepNulls || value is not null or JsonNull)
				{
					obj["model"] = value;
				}

				// DateTimeOffset? LastSeen => "lastSeen"
				// fast!
				{
					var tmp = instance.LastSeen;
					value = tmp.HasValue ? global::Doxense.Serialization.Json.JsonDateTime.Return(tmp.Value) : null;
					if (keepNulls || value is not null or JsonNull)
					{
						obj["lastSeen"] = value;
					}
				}

				// IPAddress LastAddress => "lastAddress"
				value = JsonValue.FromValue<global::System.Net.IPAddress>(instance.LastAddress);
				if (keepNulls || value is not null or JsonNull)
				{
					obj["lastAddress"] = value;
				}
				if (readOnly)
				{
					return FreezeUnsafe(obj);
				}

				return obj;
			}

			#endregion

			#region Deserialization...

			// Id { get; init; }
			[global::System.Runtime.CompilerServices.UnsafeAccessor(global::System.Runtime.CompilerServices.UnsafeAccessorKind.Field, Name = "<Id>k__BackingField")]
			private static extern ref string IdAccessor(global::Doxense.Serialization.Json.Tests.MyAwesomeDevice instance);

			// Model { get; init; }
			[global::System.Runtime.CompilerServices.UnsafeAccessor(global::System.Runtime.CompilerServices.UnsafeAccessorKind.Field, Name = "<Model>k__BackingField")]
			private static extern ref string ModelAccessor(global::Doxense.Serialization.Json.Tests.MyAwesomeDevice instance);

			// LastSeen { get; init; }
			[global::System.Runtime.CompilerServices.UnsafeAccessor(global::System.Runtime.CompilerServices.UnsafeAccessorKind.Field, Name = "<LastSeen>k__BackingField")]
			private static extern ref global::System.DateTimeOffset? LastSeenAccessor(global::Doxense.Serialization.Json.Tests.MyAwesomeDevice instance);

			// LastAddress { get; init; }
			[global::System.Runtime.CompilerServices.UnsafeAccessor(global::System.Runtime.CompilerServices.UnsafeAccessorKind.Field, Name = "<LastAddress>k__BackingField")]
			private static extern ref global::System.Net.IPAddress LastAddressAccessor(global::Doxense.Serialization.Json.Tests.MyAwesomeDevice instance);

			public global::Doxense.Serialization.Json.Tests.MyAwesomeDevice Unpack(global::Doxense.Serialization.Json.JsonValue value, global::Doxense.Serialization.Json.ICrystalJsonTypeResolver? resolver = default)
			{
				var obj = value.AsObject();
				var instance = global::System.Activator.CreateInstance<global::Doxense.Serialization.Json.Tests.MyAwesomeDevice>();

				foreach (var kv in obj)
				{
					switch (kv.Key)
					{
						case "id": IdAccessor(instance) = kv.Value.ToStringOrDefault(null)!; break;
						case "model": ModelAccessor(instance) = kv.Value.ToStringOrDefault(null)!; break;
						case "lastSeen": LastSeenAccessor(instance) = kv.Value.ToDateTimeOffsetOrDefault(null); break;
						case "lastAddress": LastAddressAccessor(instance) = kv.Value.As<global::System.Net.IPAddress>(defaultValue: null, resolver: resolver)!; break;
					}
				}

				return instance;
			}

			#endregion

		}


		/// <summary>Wraps a <see cref="JsonObject"/> into something that looks like a MyAwesomeDevice</summary>
		public readonly record struct ImpromptuReadOnlyMyAwesomeDevice : global::Doxense.Serialization.Json.IJsonReadOnly<global::Doxense.Serialization.Json.Tests.MyAwesomeDevice, ImpromptuReadOnlyMyAwesomeDevice, ImpromptuMutableMyAwesomeDevice>
		{

			/// <summary>JSON Object that is wrapped</summary>
			private readonly global::Doxense.Serialization.Json.JsonObject m_obj;

			public ImpromptuReadOnlyMyAwesomeDevice(global::Doxense.Serialization.Json.JsonObject obj) => m_obj = obj;

			/// <inheritdoc />
			public static ImpromptuReadOnlyMyAwesomeDevice FromValue(global::Doxense.Serialization.Json.Tests.MyAwesomeDevice value)
			{
				global::Doxense.Diagnostics.Contracts.Contract.NotNull(value);
				return new((global::Doxense.Serialization.Json.JsonObject) GeneratedSerializers.MyAwesomeDevice.Pack(value, global::Doxense.Serialization.Json.CrystalJsonSettings.JsonReadOnly));
			}

			/// <inheritdoc />
			public global::Doxense.Serialization.Json.Tests.MyAwesomeDevice ToValue() => GeneratedSerializers.MyAwesomeDevice.Unpack(m_obj);

			/// <inheritdoc />
			public global::Doxense.Serialization.Json.JsonObject ToJson() => m_obj;

			/// <inheritdoc />
			public ImpromptuMutableMyAwesomeDevice ToMutable() => new(m_obj.Copy());

			/// <inheritdoc />
			public ImpromptuReadOnlyMyAwesomeDevice With(Action<ImpromptuMutableMyAwesomeDevice> modifier)
			{
				var copy = m_obj.Copy();
				modifier(new(copy));
				return new(FreezeUnsafe(copy));
			}

			/// <inheritdoc />
			void global::Doxense.Serialization.Json.IJsonSerializable.JsonSerialize(global::Doxense.Serialization.Json.CrystalJsonWriter writer) => m_obj.JsonSerialize(writer);

			/// <inheritdoc />
			JsonValue global::Doxense.Serialization.Json.IJsonPackable.JsonPack(global::Doxense.Serialization.Json.CrystalJsonSettings settings, global::Doxense.Serialization.Json.ICrystalJsonTypeResolver resolver) => m_obj;

			/// <inheritdoc cref="MyAwesomeDevice.Id" />
			public string Id => m_obj.Get<string>("id");

			/// <inheritdoc cref="MyAwesomeDevice.Model" />
			public string Model => m_obj.Get<string>("model");

			/// <inheritdoc cref="MyAwesomeDevice.LastSeen" />
			public global::System.DateTimeOffset? LastSeen => m_obj.Get<global::System.DateTimeOffset?>("lastSeen");

			/// <inheritdoc cref="MyAwesomeDevice.LastAddress" />
			public global::System.Net.IPAddress LastAddress => m_obj.Get<global::System.Net.IPAddress>("lastAddress");

		}


		/// <summary>Wraps a <see cref="JsonObject"/> into something that looks like a MyAwesomeDevice</summary>
		public readonly record struct ImpromptuMutableMyAwesomeDevice : global::Doxense.Serialization.Json.IJsonMutable<global::Doxense.Serialization.Json.Tests.MyAwesomeDevice, ImpromptuMutableMyAwesomeDevice>
		{

			private readonly global::Doxense.Serialization.Json.JsonObject m_obj;

			public ImpromptuMutableMyAwesomeDevice(global::Doxense.Serialization.Json.JsonObject obj) => m_obj = obj;

			/// <inheritdoc />
			public static ImpromptuMutableMyAwesomeDevice FromValue(global::Doxense.Serialization.Json.Tests.MyAwesomeDevice value)
			{
				global::Doxense.Diagnostics.Contracts.Contract.NotNull(value);
				return new((global::Doxense.Serialization.Json.JsonObject) GeneratedSerializers.MyAwesomeDevice.Pack(value, global::Doxense.Serialization.Json.CrystalJsonSettings.Json));
			}

			/// <inheritdoc />
			public global::Doxense.Serialization.Json.JsonObject ToJson() => m_obj;

			/// <inheritdoc />
			public ImpromptuReadOnlyMyAwesomeDevice ToReadOnly() => new (m_obj.ToReadOnly());

			/// <inheritdoc />
			void global::Doxense.Serialization.Json.IJsonSerializable.JsonSerialize(global::Doxense.Serialization.Json.CrystalJsonWriter writer) => m_obj.JsonSerialize(writer);

			/// <inheritdoc />
			JsonValue global::Doxense.Serialization.Json.IJsonPackable.JsonPack(global::Doxense.Serialization.Json.CrystalJsonSettings settings, global::Doxense.Serialization.Json.ICrystalJsonTypeResolver resolver) => settings.IsReadOnly() ? m_obj.ToReadOnly() : m_obj;

			/// <inheritdoc cref="MyAwesomeDevice.Id" />
			public string Id
			{
				get => m_obj.Get<string>("id");
				set => m_obj.Set<string>("id", value);
			}

			/// <inheritdoc cref="MyAwesomeDevice.Model" />
			public string Model
			{
				get => m_obj.Get<string>("model");
				set => m_obj.Set<string>("model", value);
			}

			/// <inheritdoc cref="MyAwesomeDevice.LastSeen" />
			public global::System.DateTimeOffset? LastSeen
			{
				get => m_obj.Get<global::System.DateTimeOffset?>("lastSeen");
				set => m_obj.Set<global::System.DateTimeOffset?>("lastSeen", value);
			}

			/// <inheritdoc cref="MyAwesomeDevice.LastAddress" />
			public global::System.Net.IPAddress LastAddress
			{
				get => m_obj.Get<global::System.Net.IPAddress>("lastAddress");
				set => m_obj.Set<global::System.Net.IPAddress>("lastAddress", value);
			}

		}

		#endregion

		#region Helpers...
		[global::System.Runtime.CompilerServices.UnsafeAccessor(global::System.Runtime.CompilerServices.UnsafeAccessorKind.Method, Name = "FreezeUnsafe")]
		private static extern global::Doxense.Serialization.Json.JsonObject FreezeUnsafe(global::Doxense.Serialization.Json.JsonObject instance);
		[global::System.Runtime.CompilerServices.UnsafeAccessor(global::System.Runtime.CompilerServices.UnsafeAccessorKind.Method, Name = "FreezeUnsafe")]
		private static extern global::Doxense.Serialization.Json.JsonArray FreezeUnsafe(global::Doxense.Serialization.Json.JsonArray instance);
		#endregion
	}

}

#endif
