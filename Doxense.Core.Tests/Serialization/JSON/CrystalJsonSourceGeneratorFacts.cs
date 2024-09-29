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
	using NodaTime;

	[System.Text.Json.Serialization.JsonSourceGenerationOptions(System.Text.Json.JsonSerializerDefaults.Web /*, Converters = [ typeof(NodaTimeInstantJsonConverter) ], */)]
	[System.Text.Json.Serialization.JsonSerializable(typeof(MyAwesomeUser))]
	[System.Text.Json.Serialization.JsonSerializable(typeof(Person))]
	public partial class SystemTextJsonGeneratedSerializers : System.Text.Json.Serialization.JsonSerializerContext
	{
	}

	public class NodaTimeInstantJsonConverter : System.Text.Json.Serialization.JsonConverter<NodaTime.Instant>
	{
		/// <inheritdoc />
		public override Instant Read(ref System.Text.Json.Utf8JsonReader reader, Type typeToConvert, System.Text.Json.JsonSerializerOptions options)
		{
			var str = reader.GetString();
			if (string.IsNullOrEmpty(str)) return default;
			var res = NodaTime.Text.InstantPattern.ExtendedIso.Parse(str);
			res.TryGetValue(default, out var instant);
			return instant;
		}

		/// <inheritdoc />
		public override void Write(System.Text.Json.Utf8JsonWriter writer, Instant value, System.Text.Json.JsonSerializerOptions options)
		{
			writer.WriteStringValue(value.ToDateTimeUtc().ToString("O"));
		}
	}

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

		[JsonProperty("disabled")]
		public bool? Disabled { get; init; }
	}

	public sealed record MyAwesomeDevice
	{
		public required string Id { get; init; }

		public required string Model { get; init; }

		public DateTimeOffset? LastSeen { get; init; }

		public IPAddress? LastAddress { get; init; }

	}

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
			Assume.That(TypeHelper.GetCompilableTypeName(typeof(string[]), ommitNamespace: false, global: true), Is.EqualTo("string[]"));
			Assume.That(TypeHelper.GetCompilableTypeName(typeof(bool?[]), ommitNamespace: false, global: true), Is.EqualTo("bool?[]"));

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
				var person = new Person() { FamilyName = "Bond", FirstName = "James" };
				Log(person.ToString());
				Log();
				Log("# Reference System.Text.Json:");
				Log(System.Text.Json.JsonSerializer.Serialize(person, SystemTextJsonGeneratedSerializers.Default.Person));
				Log();

				Log("# Actual output:");
				var json = CrystalJson.Serialize(person, GeneratedSerializers.Person);
				Log(json);
				Assert.That(json, Is.EqualTo("""{ "firstName": "James", "familyName": "Bond" }"""));
				Log();

				Log("# Parsing:");
				var parsed = CrystalJson.Deserialize<Person>(json, GeneratedSerializers.Person);
				Log(parsed?.ToString());
				Assert.That(parsed, Is.Not.Null);
				Assert.That(parsed.FirstName, Is.EqualTo("James"));
				Assert.That(parsed.FamilyName, Is.EqualTo("Bond"));
				Log();

				Log("# Packing:");
				var packed = GeneratedSerializers.Person.JsonPack(person);
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
					new() { Id = "382bb7cd-f9e4-4906-874e-ab88df954fa8", Level = 123 },
					new() { Id = "8092e57d-16b4-4afb-ae04-28acbeb22aa8", Level = 456, Disabled = true }
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

			Log();
			Log("System.Text.Json reference:");
			Log(System.Text.Json.JsonSerializer.Serialize(user, SystemTextJsonGeneratedSerializers.Default.MyAwesomeUser));

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
				Assert.That(parsed["items"][1], IsJson.Object);
				Assert.That(parsed["items"][1]["id"], IsJson.EqualTo("8092e57d-16b4-4afb-ae04-28acbeb22aa8"));
				Assert.That(parsed["devices"], IsJson.Object.And.OfSize(2));
				Assert.That(parsed["devices"]["Foo"], IsJson.Object);
				Assert.That(parsed["devices"]["Bar"], IsJson.Object);
				Assert.That(parsed["extras"], IsJson.Object.And.EqualTo(user.Extras));
			});

			Log();
			Log("Deserialize...");
			var decoded = GeneratedSerializers.MyAwesomeUser.JsonDeserialize(parsed);
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
			var packed = GeneratedSerializers.MyAwesomeUser.JsonPack(user);
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
					new() { Id = "382bb7cd-f9e4-4906-874e-ab88df954fa8", Level = 123 },
					new() { Id = "8092e57d-16b4-4afb-ae04-28acbeb22aa8", Level = 456, Disabled = true }
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
				_ = GeneratedSerializers.MyAwesomeUser.JsonDeserialize(JsonValue.Parse(CrystalJson.Serialize(user, GeneratedSerializers.MyAwesomeUser)));
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
				var report = RobustBenchmark.Run(() => GeneratedSerializers.MyAwesomeUser.JsonDeserialize(JsonValue.Parse(json)), RUNS, ITERATIONS);
				Report("DESERIALIZE CODEGEN", report);
			}

			{
				var report = RobustBenchmark.Run(() => parsed.As<MyAwesomeUser>(), RUNS, ITERATIONS);
				Report("AS<T> RUNTIME", report);
			}
			{
				var report = RobustBenchmark.Run(() => GeneratedSerializers.MyAwesomeUser.JsonDeserialize(parsed), RUNS, ITERATIONS);
				Report("AS<T> CODEGEN", report);
			}

			{
				var report = RobustBenchmark.Run(() => JsonValue.FromValue<MyAwesomeUser>(user), RUNS, ITERATIONS);
				Report("PACK RUNTIME", report);
			}
			{
				var report = RobustBenchmark.Run(() => GeneratedSerializers.MyAwesomeUser.JsonPack(user), RUNS, ITERATIONS);
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

		/// <summary>Serializer for type <see cref="Doxense.Serialization.Json.Tests.Person">Person</see></summary>
		public static _PersonJsonSerializer Person => m_cachedPerson ??= new();

		private static _PersonJsonSerializer? m_cachedPerson;

		[global::System.Diagnostics.CodeAnalysis.DynamicallyAccessedMembers(global::System.Diagnostics.CodeAnalysis.DynamicallyAccessedMemberTypes.All)]
		[global::System.CodeDom.Compiler.GeneratedCode("CrystalJsonSourceGenerator", "0.1")]
		[global::System.Diagnostics.DebuggerNonUserCode()]
		public sealed class _PersonJsonSerializer : global::Doxense.Serialization.Json.IJsonSerializer<global::Doxense.Serialization.Json.Tests.Person>, global::Doxense.Serialization.Json.IJsonPackerFor<global::Doxense.Serialization.Json.Tests.Person>, global::Doxense.Serialization.Json.IJsonDeserializerFor<global::Doxense.Serialization.Json.Tests.Person>
		{

			#region Serialization...

			private static readonly global::Doxense.Serialization.Json.JsonEncodedPropertyName _firstName = new("firstName");
			private static readonly global::Doxense.Serialization.Json.JsonEncodedPropertyName _familyName = new("familyName");

			public void JsonSerialize(global::Doxense.Serialization.Json.CrystalJsonWriter writer, global::Doxense.Serialization.Json.Tests.Person? instance)
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
				// fast!
				writer.WriteField(in _firstName, instance.FirstName);

				// string FamilyName => "familyName"
				// fast!
				writer.WriteField(in _familyName, instance.FamilyName);

				writer.EndObject(state);
			}

			#endregion

			#region Packing...

			public global::Doxense.Serialization.Json.JsonValue JsonPack(global::Doxense.Serialization.Json.Tests.Person? instance, global::Doxense.Serialization.Json.CrystalJsonSettings? settings = default, global::Doxense.Serialization.Json.ICrystalJsonTypeResolver? resolver = default)
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
					FreezeUnsafe(obj);
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

			public global::Doxense.Serialization.Json.Tests.Person JsonDeserialize(global::Doxense.Serialization.Json.JsonValue value, global::Doxense.Serialization.Json.ICrystalJsonTypeResolver? resolver = default)
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

		#endregion

		#region MyAwesomeUser ...

		/// <summary>Serializer for type <see cref="Doxense.Serialization.Json.Tests.MyAwesomeUser">MyAwesomeUser</see></summary>
		public static _MyAwesomeUserJsonSerializer MyAwesomeUser => m_cachedMyAwesomeUser ??= new();

		private static _MyAwesomeUserJsonSerializer? m_cachedMyAwesomeUser;

		[global::System.Diagnostics.CodeAnalysis.DynamicallyAccessedMembers(global::System.Diagnostics.CodeAnalysis.DynamicallyAccessedMemberTypes.All)]
		[global::System.CodeDom.Compiler.GeneratedCode("CrystalJsonSourceGenerator", "0.1")]
		[global::System.Diagnostics.DebuggerNonUserCode()]
		public sealed class _MyAwesomeUserJsonSerializer : global::Doxense.Serialization.Json.IJsonSerializer<global::Doxense.Serialization.Json.Tests.MyAwesomeUser>, global::Doxense.Serialization.Json.IJsonPackerFor<global::Doxense.Serialization.Json.Tests.MyAwesomeUser>, global::Doxense.Serialization.Json.IJsonDeserializerFor<global::Doxense.Serialization.Json.Tests.MyAwesomeUser>
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

			public void JsonSerialize(global::Doxense.Serialization.Json.CrystalJsonWriter writer, global::Doxense.Serialization.Json.Tests.MyAwesomeUser? instance)
			{
				if (instance is null)
				{
					writer.WriteNull();
					return;
				}

				var state = writer.BeginObject();

				// string Id => "id"
				// fast!
				writer.WriteField(in _id, instance.Id);

				// string DisplayName => "displayName"
				// fast!
				writer.WriteField(in _displayName, instance.DisplayName);

				// string Email => "email"
				// fast!
				writer.WriteField(in _email, instance.Email);

				// int Type => "type"
				// fast!
				writer.WriteField(in _type, instance.Type);

				// string[] Roles => "roles"
				// fast array!
				writer.WriteFieldArray(in _roles, instance.Roles);

				// MyAwesomeMetadata Metadata => "metadata"
				// custom!
				writer.WriteField(in _metadata, instance.Metadata, GeneratedSerializers.MyAwesomeMetadata);

				// List<MyAwesomeStruct> Items => "items"
				// custom array!
				writer.WriteFieldArray(in _items, instance.Items, GeneratedSerializers.MyAwesomeStruct);

				// Dictionary<string, MyAwesomeDevice> Devices => "devices"
				// dictionary with string key
				writer.WriteFieldDictionary(in _devices, instance.Devices, GeneratedSerializers.MyAwesomeDevice);

				// JsonObject Extras => "extras"
				// fast!
				writer.WriteField(in _extras, instance.Extras);

				writer.EndObject(state);
			}

			#endregion

			#region Packing...

			public global::Doxense.Serialization.Json.JsonValue JsonPack(global::Doxense.Serialization.Json.Tests.MyAwesomeUser? instance, global::Doxense.Serialization.Json.CrystalJsonSettings? settings = default, global::Doxense.Serialization.Json.ICrystalJsonTypeResolver? resolver = default)
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
				value = GeneratedSerializers.MyAwesomeMetadata.JsonPack(instance.Metadata, settings, resolver);
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
				value = instance.Extras;
				if (keepNulls || value is not null or JsonNull)
				{
					obj["extras"] = value;
				}
				if (readOnly)
				{
					FreezeUnsafe(obj);
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

			public global::Doxense.Serialization.Json.Tests.MyAwesomeUser JsonDeserialize(global::Doxense.Serialization.Json.JsonValue value, global::Doxense.Serialization.Json.ICrystalJsonTypeResolver? resolver = default)
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
						case "metadata": MetadataAccessor(instance) = GeneratedSerializers.MyAwesomeMetadata.JsonDeserialize(kv.Value, resolver)!; break;
						case "items": ItemsAccessor(instance) = GeneratedSerializers.MyAwesomeStruct.JsonDeserializeList(kv.Value, defaultValue: null, resolver: resolver)!; break;
						case "devices": DevicesAccessor(instance) = GeneratedSerializers.MyAwesomeDevice.JsonDeserializeDictionary(kv.Value, defaultValue: null, keyComparer: null, resolver: resolver)!; break;
						case "extras": ExtrasAccessor(instance) = kv.Value.AsObjectOrDefault()!; break;
					}
				}

				return instance;
			}

			#endregion

		}

		#endregion

		#region MyAwesomeMetadata ...

		/// <summary>Serializer for type <see cref="Doxense.Serialization.Json.Tests.MyAwesomeMetadata">MyAwesomeMetadata</see></summary>
		public static _MyAwesomeMetadataJsonSerializer MyAwesomeMetadata => m_cachedMyAwesomeMetadata ??= new();

		private static _MyAwesomeMetadataJsonSerializer? m_cachedMyAwesomeMetadata;

		[global::System.Diagnostics.CodeAnalysis.DynamicallyAccessedMembers(global::System.Diagnostics.CodeAnalysis.DynamicallyAccessedMemberTypes.All)]
		[global::System.CodeDom.Compiler.GeneratedCode("CrystalJsonSourceGenerator", "0.1")]
		[global::System.Diagnostics.DebuggerNonUserCode()]
		public sealed class _MyAwesomeMetadataJsonSerializer : global::Doxense.Serialization.Json.IJsonSerializer<global::Doxense.Serialization.Json.Tests.MyAwesomeMetadata>, global::Doxense.Serialization.Json.IJsonPackerFor<global::Doxense.Serialization.Json.Tests.MyAwesomeMetadata>, global::Doxense.Serialization.Json.IJsonDeserializerFor<global::Doxense.Serialization.Json.Tests.MyAwesomeMetadata>
		{

			#region Serialization...

			private static readonly global::Doxense.Serialization.Json.JsonEncodedPropertyName _accountCreated = new("accountCreated");
			private static readonly global::Doxense.Serialization.Json.JsonEncodedPropertyName _accountModified = new("accountModified");
			private static readonly global::Doxense.Serialization.Json.JsonEncodedPropertyName _accountDisabled = new("accountDisabled");

			public void JsonSerialize(global::Doxense.Serialization.Json.CrystalJsonWriter writer, global::Doxense.Serialization.Json.Tests.MyAwesomeMetadata? instance)
			{
				if (instance is null)
				{
					writer.WriteNull();
					return;
				}

				var state = writer.BeginObject();

				// DateTimeOffset AccountCreated => "accountCreated"
				// unknown type
				writer.WriteField(in _accountCreated, instance.AccountCreated);

				// DateTimeOffset AccountModified => "accountModified"
				// unknown type
				writer.WriteField(in _accountModified, instance.AccountModified);

				// DateTimeOffset? AccountDisabled => "accountDisabled"
				// unknown type
				writer.WriteField(in _accountDisabled, instance.AccountDisabled);

				writer.EndObject(state);
			}

			#endregion

			#region Packing...

			public global::Doxense.Serialization.Json.JsonValue JsonPack(global::Doxense.Serialization.Json.Tests.MyAwesomeMetadata? instance, global::Doxense.Serialization.Json.CrystalJsonSettings? settings = default, global::Doxense.Serialization.Json.ICrystalJsonTypeResolver? resolver = default)
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
					FreezeUnsafe(obj);
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

			public global::Doxense.Serialization.Json.Tests.MyAwesomeMetadata JsonDeserialize(global::Doxense.Serialization.Json.JsonValue value, global::Doxense.Serialization.Json.ICrystalJsonTypeResolver? resolver = default)
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

		#endregion

		#region MyAwesomeStruct ...

		/// <summary>Serializer for type <see cref="Doxense.Serialization.Json.Tests.MyAwesomeStruct">MyAwesomeStruct</see></summary>
		public static _MyAwesomeStructJsonSerializer MyAwesomeStruct => m_cachedMyAwesomeStruct ??= new();

		private static _MyAwesomeStructJsonSerializer? m_cachedMyAwesomeStruct;

		[global::System.Diagnostics.CodeAnalysis.DynamicallyAccessedMembers(global::System.Diagnostics.CodeAnalysis.DynamicallyAccessedMemberTypes.All)]
		[global::System.CodeDom.Compiler.GeneratedCode("CrystalJsonSourceGenerator", "0.1")]
		[global::System.Diagnostics.DebuggerNonUserCode()]
		public sealed class _MyAwesomeStructJsonSerializer : global::Doxense.Serialization.Json.IJsonSerializer<global::Doxense.Serialization.Json.Tests.MyAwesomeStruct>, global::Doxense.Serialization.Json.IJsonPackerFor<global::Doxense.Serialization.Json.Tests.MyAwesomeStruct>, global::Doxense.Serialization.Json.IJsonDeserializerFor<global::Doxense.Serialization.Json.Tests.MyAwesomeStruct>
		{

			#region Serialization...

			private static readonly global::Doxense.Serialization.Json.JsonEncodedPropertyName _id = new("id");
			private static readonly global::Doxense.Serialization.Json.JsonEncodedPropertyName _level = new("level");
			private static readonly global::Doxense.Serialization.Json.JsonEncodedPropertyName _disabled = new("disabled");

			public void JsonSerialize(global::Doxense.Serialization.Json.CrystalJsonWriter writer, global::Doxense.Serialization.Json.Tests.MyAwesomeStruct instance)
			{
				var state = writer.BeginObject();

				// string Id => "id"
				// fast!
				writer.WriteField(in _id, instance.Id);

				// int Level => "level"
				// fast!
				writer.WriteField(in _level, instance.Level);

				// bool? Disabled => "disabled"
				// fast!
				writer.WriteField(in _disabled, instance.Disabled);

				writer.EndObject(state);
			}

			#endregion

			#region Packing...

			public global::Doxense.Serialization.Json.JsonValue JsonPack(global::Doxense.Serialization.Json.Tests.MyAwesomeStruct instance, global::Doxense.Serialization.Json.CrystalJsonSettings? settings = default, global::Doxense.Serialization.Json.ICrystalJsonTypeResolver? resolver = default)
			{
				global::Doxense.Serialization.Json.JsonValue? value;
				var readOnly = settings?.ReadOnly ?? false;
				var keepNulls = settings?.ShowNullMembers ?? false;

				var obj = new global::Doxense.Serialization.Json.JsonObject(3);

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
					FreezeUnsafe(obj);
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

			// Disabled { get; init; }
			[global::System.Runtime.CompilerServices.UnsafeAccessor(global::System.Runtime.CompilerServices.UnsafeAccessorKind.Field, Name = "<Disabled>k__BackingField")]
			private static extern ref bool? DisabledAccessor(ref global::Doxense.Serialization.Json.Tests.MyAwesomeStruct instance);

			public global::Doxense.Serialization.Json.Tests.MyAwesomeStruct JsonDeserialize(global::Doxense.Serialization.Json.JsonValue value, global::Doxense.Serialization.Json.ICrystalJsonTypeResolver? resolver = default)
			{
				var obj = value.AsObject();
				var instance = global::System.Activator.CreateInstance<global::Doxense.Serialization.Json.Tests.MyAwesomeStruct>();

				foreach (var kv in obj)
				{
					switch (kv.Key)
					{
						case "id": IdAccessor(ref instance) = kv.Value.ToStringOrDefault(null)!; break;
						case "level": LevelAccessor(ref instance) = kv.Value.ToInt32(); break;
						case "disabled": DisabledAccessor(ref instance) = kv.Value.ToBooleanOrDefault(null); break;
					}
				}

				return instance;
			}

			#endregion

		}

		#endregion

		#region MyAwesomeDevice ...

		/// <summary>Serializer for type <see cref="Doxense.Serialization.Json.Tests.MyAwesomeDevice">MyAwesomeDevice</see></summary>
		public static _MyAwesomeDeviceJsonSerializer MyAwesomeDevice => m_cachedMyAwesomeDevice ??= new();

		private static _MyAwesomeDeviceJsonSerializer? m_cachedMyAwesomeDevice;

		[global::System.Diagnostics.CodeAnalysis.DynamicallyAccessedMembers(global::System.Diagnostics.CodeAnalysis.DynamicallyAccessedMemberTypes.All)]
		[global::System.CodeDom.Compiler.GeneratedCode("CrystalJsonSourceGenerator", "0.1")]
		[global::System.Diagnostics.DebuggerNonUserCode()]
		public sealed class _MyAwesomeDeviceJsonSerializer : global::Doxense.Serialization.Json.IJsonSerializer<global::Doxense.Serialization.Json.Tests.MyAwesomeDevice>, global::Doxense.Serialization.Json.IJsonPackerFor<global::Doxense.Serialization.Json.Tests.MyAwesomeDevice>, global::Doxense.Serialization.Json.IJsonDeserializerFor<global::Doxense.Serialization.Json.Tests.MyAwesomeDevice>
		{

			#region Serialization...

			private static readonly global::Doxense.Serialization.Json.JsonEncodedPropertyName _Id = new("Id");
			private static readonly global::Doxense.Serialization.Json.JsonEncodedPropertyName _Model = new("Model");
			private static readonly global::Doxense.Serialization.Json.JsonEncodedPropertyName _LastSeen = new("LastSeen");
			private static readonly global::Doxense.Serialization.Json.JsonEncodedPropertyName _LastAddress = new("LastAddress");

			public void JsonSerialize(global::Doxense.Serialization.Json.CrystalJsonWriter writer, global::Doxense.Serialization.Json.Tests.MyAwesomeDevice? instance)
			{
				if (instance is null)
				{
					writer.WriteNull();
					return;
				}

				var state = writer.BeginObject();

				// string Id => "Id"
				// fast!
				writer.WriteField(in _Id, instance.Id);

				// string Model => "Model"
				// fast!
				writer.WriteField(in _Model, instance.Model);

				// DateTimeOffset? LastSeen => "LastSeen"
				// unknown type
				writer.WriteField(in _LastSeen, instance.LastSeen);

				// IPAddress LastAddress => "LastAddress"
				// unknown type
				writer.WriteField(in _LastAddress, instance.LastAddress);

				writer.EndObject(state);
			}

			#endregion

			#region Packing...

			public global::Doxense.Serialization.Json.JsonValue JsonPack(global::Doxense.Serialization.Json.Tests.MyAwesomeDevice? instance, global::Doxense.Serialization.Json.CrystalJsonSettings? settings = default, global::Doxense.Serialization.Json.ICrystalJsonTypeResolver? resolver = default)
			{
				if (instance is null)
				{
					return global::Doxense.Serialization.Json.JsonNull.Null;
				}

				global::Doxense.Serialization.Json.JsonValue? value;
				var readOnly = settings?.ReadOnly ?? false;
				var keepNulls = settings?.ShowNullMembers ?? false;

				var obj = new global::Doxense.Serialization.Json.JsonObject(4);

				// string Id => "Id"
				value = global::Doxense.Serialization.Json.JsonString.Return(instance.Id);
				if (keepNulls || value is not null or JsonNull)
				{
					obj["Id"] = value;
				}

				// string Model => "Model"
				value = global::Doxense.Serialization.Json.JsonString.Return(instance.Model);
				if (keepNulls || value is not null or JsonNull)
				{
					obj["Model"] = value;
				}

				// DateTimeOffset? LastSeen => "LastSeen"
				// fast!
				{
					var tmp = instance.LastSeen;
					value = tmp.HasValue ? global::Doxense.Serialization.Json.JsonDateTime.Return(tmp.Value) : null;
					if (keepNulls || value is not null or JsonNull)
					{
						obj["LastSeen"] = value;
					}
				}

				// IPAddress LastAddress => "LastAddress"
				value = JsonValue.FromValue<global::System.Net.IPAddress>(instance.LastAddress);
				if (keepNulls || value is not null or JsonNull)
				{
					obj["LastAddress"] = value;
				}
				if (readOnly)
				{
					FreezeUnsafe(obj);
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

			public global::Doxense.Serialization.Json.Tests.MyAwesomeDevice JsonDeserialize(global::Doxense.Serialization.Json.JsonValue value, global::Doxense.Serialization.Json.ICrystalJsonTypeResolver? resolver = default)
			{
				var obj = value.AsObject();
				var instance = global::System.Activator.CreateInstance<global::Doxense.Serialization.Json.Tests.MyAwesomeDevice>();

				foreach (var kv in obj)
				{
					switch (kv.Key)
					{
						case "Id": IdAccessor(instance) = kv.Value.ToStringOrDefault(null)!; break;
						case "Model": ModelAccessor(instance) = kv.Value.ToStringOrDefault(null)!; break;
						case "LastSeen": LastSeenAccessor(instance) = kv.Value.ToDateTimeOffsetOrDefault(null); break;
						case "LastAddress": LastAddressAccessor(instance) = kv.Value.As<global::System.Net.IPAddress>(defaultValue: null, resolver: resolver)!; break;
					}
				}

				return instance;
			}

			#endregion

		}

		#endregion

		#region Helpers...
		[global::System.Runtime.CompilerServices.UnsafeAccessor(global::System.Runtime.CompilerServices.UnsafeAccessorKind.Method, Name = "FreezeUnsafe")]
		private static extern ref string FreezeUnsafe(global::Doxense.Serialization.Json.JsonObject instance);
		[global::System.Runtime.CompilerServices.UnsafeAccessor(global::System.Runtime.CompilerServices.UnsafeAccessorKind.Method, Name = "FreezeUnsafe")]
		private static extern ref string FreezeUnsafe(global::Doxense.Serialization.Json.JsonArray instance);
		#endregion
	}

}

#endif
