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

	public sealed record Person
	{
		public string? Firstame { get; set; }

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
				typeof(MyAwesomeUser),
				typeof(MyAwesomeStruct), //BUGBUG:
				typeof(MyAwesomeDevice), //BUGBUG:
			]);

			var source = gen.GenerateCode();
			Log(source);
		}

		[Test]
		public void Test_Custom_Serializer_Basics()
		{


			var person = new Person() { FamilyName = "Bond", Firstame = "James" };


			Log(System.Text.Json.JsonSerializer.Serialize(person, SystemTextJsonGeneratedSerializers.Default.Person));

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

			Log("Actual Json:");
			var json = CrystalJson.Serialize(user, GeneratedSerializers.MyAwesomeUser);
			Log(json);
			Assert.That(json, Is.EqualTo(expectedJson));

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

			Log("Parse...");
			var parsed = JsonValue.ParseObject(json);
			DumpCompact(parsed);

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

			var stjopts = new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web) { };

			var json = CrystalJson.Serialize(user, GeneratedSerializers.MyAwesomeUser);
			var parsed = JsonValue.ParseObject(json);

			// warmup
			{
				_ = JsonValue.Parse(CrystalJson.Serialize(user)).As<MyAwesomeUser>();
				_ = GeneratedSerializers.MyAwesomeUser.JsonDeserialize(JsonValue.Parse(CrystalJson.Serialize(user, GeneratedSerializers.MyAwesomeUser)));
				_ = CrystalJson.Deserialize<MyAwesomeUser>(json);
				_ = System.Text.Json.JsonSerializer.Deserialize<MyAwesomeUser>(json, stjopts);
				_ = System.Text.Json.JsonSerializer.Deserialize<MyAwesomeUser>(json, SystemTextJsonGeneratedSerializers.Default.MyAwesomeUser);
			}

			Log($"JSON: {json.Length:N0} chars");

			{
				var report = RobustBenchmark.Run(() => System.Text.Json.JsonSerializer.Serialize(user, stjopts), 5, 100_000);
				Log($"* SERIALIZE TEXT STJ_DYN: {report.IterationsPerRun:N0} in {report.BestDuration.TotalMilliseconds:F1} ms at {report.BestIterationsPerSecond:N0} op/s ({report.BestIterationsNanos:N0} nanos), {report.GC0 / report.Runs!.Count} GC0");
			}
			{
				var report = RobustBenchmark.Run(() => System.Text.Json.JsonSerializer.Serialize(user, SystemTextJsonGeneratedSerializers.Default.MyAwesomeUser), 5, 100_000);
				Log($"* SERIALIZE TEXT STJ_GEN: {report.IterationsPerRun:N0} in {report.BestDuration.TotalMilliseconds:F1} ms at {report.BestIterationsPerSecond:N0} op/s ({report.BestIterationsNanos:N0} nanos), {report.GC0 / report.Runs!.Count} GC0");
			}
			{
				var report = RobustBenchmark.Run(() => CrystalJson.Serialize(user), 5, 100_000);
				Log($"* SERIALIZE TEXT CRY_DYN: {report.IterationsPerRun:N0} in {report.BestDuration.TotalMilliseconds:F1} ms at {report.BestIterationsPerSecond:N0} op/s ({report.BestIterationsNanos:N0} nanos), {report.GC0 / report.Runs!.Count} GC0");
			}
			{
				var report = RobustBenchmark.Run(() => CrystalJson.Serialize(user, GeneratedSerializers.MyAwesomeUser), 5, 100_000);
				Log($"* SERIALIZE TEXT CRY_GEN: {report.IterationsPerRun:N0} in {report.BestDuration.TotalMilliseconds:F1} ms at {report.BestIterationsPerSecond:N0} op/s ({report.BestIterationsNanos:N0} nanos), {report.GC0 / report.Runs!.Count} GC0");
			}
			{
				var report = RobustBenchmark.Run(() => CrystalJson.ToSlice(user), 5, 100_000);
				Log($"* SERIALIZE UTF8 CRY_DYN: {report.IterationsPerRun:N0} in {report.BestDuration.TotalMilliseconds:F1} ms at {report.BestIterationsPerSecond:N0} op/s ({report.BestIterationsNanos:N0} nanos), {report.GC0 / report.Runs!.Count} GC0");
			}
			{
				var report = RobustBenchmark.Run(() => CrystalJson.ToSlice(user, GeneratedSerializers.MyAwesomeUser), 5, 100_000);
				Log($"* SERIALIZE UTF8 CRY_GEN: {report.IterationsPerRun:N0} in {report.BestDuration.TotalMilliseconds:F1} ms at {report.BestIterationsPerSecond:N0} op/s ({report.BestIterationsNanos:N0} nanos), {report.GC0 / report.Runs!.Count} GC0");
			}
			{
				var report = RobustBenchmark.Run(() =>
				{
					using var res = CrystalJson.ToSlice(user, GeneratedSerializers.MyAwesomeUser, ArrayPool<byte>.Shared);
					// use the JSON here to do something!
				}, 5, 100_000);
				Log($"* SERIALIZE UTF8 POOLED: {report.IterationsPerRun:N0} in {report.BestDuration.TotalMilliseconds:F1} ms at {report.BestIterationsPerSecond:N0} op/s ({report.BestIterationsNanos:N0} nanos), {report.GC0 / report.Runs!.Count} GC0");
			}

			{
				var report = RobustBenchmark.Run(() => System.Text.Json.JsonSerializer.Deserialize<MyAwesomeUser>(json, stjopts), 5, 100_000);
				Log($"* DESR STJ_DYN: {report.IterationsPerRun:N0} in {report.BestDuration.TotalMilliseconds:F1} ms at {report.BestIterationsPerSecond:N0} op/s ({report.BestIterationsNanos:N0} nanos), {report.GC0 / report.Runs!.Count} GC0");
			}
			{
				var report = RobustBenchmark.Run(() => System.Text.Json.JsonSerializer.Deserialize<MyAwesomeUser>(json, SystemTextJsonGeneratedSerializers.Default.MyAwesomeUser), 5, 100_000);
				Log($"* DESR STJ_GEN: {report.IterationsPerRun:N0} in {report.BestDuration.TotalMilliseconds:F1} ms at {report.BestIterationsPerSecond:N0} op/s ({report.BestIterationsNanos:N0} nanos), {report.GC0 / report.Runs!.Count} GC0");
			}
			{
				var report = RobustBenchmark.Run(() => CrystalJson.Deserialize<MyAwesomeUser>(json), 5, 100_000);
				Log($"* DESR RUNTIME: {report.IterationsPerRun:N0} in {report.BestDuration.TotalMilliseconds:F1} ms at {report.BestIterationsPerSecond:N0} op/s ({report.BestIterationsNanos:N0} nanos), {report.GC0 / report.Runs!.Count} GC0");
			}
			{
				var report = RobustBenchmark.Run(() => GeneratedSerializers.MyAwesomeUser.JsonDeserialize(JsonValue.Parse(json)), 5, 100_000);
				Log($"* DESR CODEGEN: {report.IterationsPerRun:N0} in {report.BestDuration.TotalMilliseconds:F1} ms at {report.BestIterationsPerSecond:N0} op/s ({report.BestIterationsNanos:N0} nanos), {report.GC0 / report.Runs!.Count} GC0");
			}

			{
				var report = RobustBenchmark.Run(() => parsed.As<MyAwesomeUser>(), 5, 100_000);
				Log($"* AS<> RUNTIME: {report.IterationsPerRun:N0} in {report.BestDuration.TotalMilliseconds:F1} ms at {report.BestIterationsPerSecond:N0} op/s ({report.BestIterationsNanos:N0} nanos), {report.GC0 / report.Runs!.Count} GC0");
			}
			{
				var report = RobustBenchmark.Run(() => GeneratedSerializers.MyAwesomeUser.JsonDeserialize(parsed), 5, 100_000);
				Log($"* AS<> CODEGEN: {report.IterationsPerRun:N0} in {report.BestDuration.TotalMilliseconds:F1} ms at {report.BestIterationsPerSecond:N0} op/s ({report.BestIterationsNanos:N0} nanos), {report.GC0 / report.Runs!.Count} GC0");
			}
		}


	}

}

namespace Doxense.Serialization.Json.Tests
{

	// ReSharper disable InconsistentNaming
	// ReSharper disable PartialTypeWithSinglePart
	// ReSharper disable RedundantNameQualifier

	public partial class GeneratedSerializers
	{

		#region MyAwesomeUser ...

		/// <summary>Serializer for type <see cref="Doxense.Serialization.Json.Tests.MyAwesomeUser">MyAwesomeUser</see></summary>
		public static _MyAwesomeUserJsonSerializer MyAwesomeUser => m_cachedMyAwesomeUser ??= new _MyAwesomeUserJsonSerializer();

		private static _MyAwesomeUserJsonSerializer? m_cachedMyAwesomeUser;

		[global::System.Diagnostics.CodeAnalysis.DynamicallyAccessedMembers(global::System.Diagnostics.CodeAnalysis.DynamicallyAccessedMemberTypes.All)]
		[global::System.CodeDom.Compiler.GeneratedCode("CrystalJsonSourceGenerator", "0.1")]
		[global::System.Diagnostics.DebuggerNonUserCode()]
		public sealed class _MyAwesomeUserJsonSerializer : global::Doxense.Serialization.Json.IJsonSerializer<global::Doxense.Serialization.Json.Tests.MyAwesomeUser>, global::Doxense.Serialization.Json.IJsonDeserializerFor<global::Doxense.Serialization.Json.Tests.MyAwesomeUser>
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

				if (instance.GetType() != typeof(global::Doxense.Serialization.Json.Tests.MyAwesomeUser))
				{
					global::Doxense.Serialization.Json.CrystalJsonVisitor.VisitValue(instance, typeof(global::Doxense.Serialization.Json.Tests.MyAwesomeUser), writer);
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
						case "items": ItemsAccessor(instance) = GeneratedSerializers.MyAwesomeStruct.JsonDeserializeList<global::Doxense.Serialization.Json.Tests.MyAwesomeStruct>(kv.Value, defaultValue: null, resolver: resolver)!; break;
						case "devices": DevicesAccessor(instance) = GeneratedSerializers.MyAwesomeDevice.JsonDeserializeDictionary<global::Doxense.Serialization.Json.Tests.MyAwesomeDevice>(kv.Value, defaultValue: null, keyComparer: null, resolver: resolver)!; break;
						case "extras": ExtrasAccessor(instance) = kv.Value.AsObjectOrDefault()!; break;
					}
				}

				return instance;
			}

			#endregion

		}

		#endregion

		#region MyAwesomeStruct ...

		/// <summary>Serializer for type <see cref="Doxense.Serialization.Json.Tests.MyAwesomeStruct">MyAwesomeStruct</see></summary>
		public static _MyAwesomeStructJsonSerializer MyAwesomeStruct => m_cachedMyAwesomeStruct ??= new _MyAwesomeStructJsonSerializer();

		private static _MyAwesomeStructJsonSerializer? m_cachedMyAwesomeStruct;

		[global::System.Diagnostics.CodeAnalysis.DynamicallyAccessedMembers(global::System.Diagnostics.CodeAnalysis.DynamicallyAccessedMemberTypes.All)]
		[global::System.CodeDom.Compiler.GeneratedCode("CrystalJsonSourceGenerator", "0.1")]
		[global::System.Diagnostics.DebuggerNonUserCode()]
		public sealed class _MyAwesomeStructJsonSerializer : global::Doxense.Serialization.Json.IJsonSerializer<global::Doxense.Serialization.Json.Tests.MyAwesomeStruct>, global::Doxense.Serialization.Json.IJsonDeserializerFor<global::Doxense.Serialization.Json.Tests.MyAwesomeStruct>
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
		public static _MyAwesomeDeviceJsonSerializer MyAwesomeDevice => m_cachedMyAwesomeDevice ??= new _MyAwesomeDeviceJsonSerializer();

		private static _MyAwesomeDeviceJsonSerializer? m_cachedMyAwesomeDevice;

		[global::System.Diagnostics.CodeAnalysis.DynamicallyAccessedMembers(global::System.Diagnostics.CodeAnalysis.DynamicallyAccessedMemberTypes.All)]
		[global::System.CodeDom.Compiler.GeneratedCode("CrystalJsonSourceGenerator", "0.1")]
		[global::System.Diagnostics.DebuggerNonUserCode()]
		public sealed class _MyAwesomeDeviceJsonSerializer : global::Doxense.Serialization.Json.IJsonSerializer<global::Doxense.Serialization.Json.Tests.MyAwesomeDevice>, global::Doxense.Serialization.Json.IJsonDeserializerFor<global::Doxense.Serialization.Json.Tests.MyAwesomeDevice>
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

				// Instant? LastSeen => "LastSeen"
				// fast!
				writer.WriteField(in _LastSeen, instance.LastSeen);

				// IPAddress LastAddress => "LastAddress"
				// unknown type
				writer.WriteField(in _LastAddress, instance.LastAddress);

				writer.EndObject(state);
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
			private static extern ref DateTimeOffset? LastSeenAccessor(global::Doxense.Serialization.Json.Tests.MyAwesomeDevice instance);

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

		#region MyAwesomeMetadata ...

		/// <summary>Serializer for type <see cref="Doxense.Serialization.Json.Tests.MyAwesomeMetadata">MyAwesomeMetadata</see></summary>
		public static _MyAwesomeMetadataJsonSerializer MyAwesomeMetadata => m_cachedMyAwesomeMetadata ??= new _MyAwesomeMetadataJsonSerializer();

		private static _MyAwesomeMetadataJsonSerializer? m_cachedMyAwesomeMetadata;

		[global::System.Diagnostics.CodeAnalysis.DynamicallyAccessedMembers(global::System.Diagnostics.CodeAnalysis.DynamicallyAccessedMemberTypes.All)]
		[global::System.CodeDom.Compiler.GeneratedCode("CrystalJsonSourceGenerator", "0.1")]
		[global::System.Diagnostics.DebuggerNonUserCode()]
		public sealed class _MyAwesomeMetadataJsonSerializer : global::Doxense.Serialization.Json.IJsonSerializer<global::Doxense.Serialization.Json.Tests.MyAwesomeMetadata>, global::Doxense.Serialization.Json.IJsonDeserializerFor<global::Doxense.Serialization.Json.Tests.MyAwesomeMetadata>
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

				// Instant AccountCreated => "accountCreated"
				// fast!
				writer.WriteField(in _accountCreated, instance.AccountCreated);

				// Instant AccountModified => "accountModified"
				// fast!
				writer.WriteField(in _accountModified, instance.AccountModified);

				// Instant? AccountDisabled => "accountDisabled"
				// fast!
				writer.WriteField(in _accountDisabled, instance.AccountDisabled);

				writer.EndObject(state);
			}

			#endregion

			#region Deserialization...

			// AccountCreated { get; init; }
			[global::System.Runtime.CompilerServices.UnsafeAccessor(global::System.Runtime.CompilerServices.UnsafeAccessorKind.Field, Name = "<AccountCreated>k__BackingField")]
			private static extern ref DateTimeOffset AccountCreatedAccessor(global::Doxense.Serialization.Json.Tests.MyAwesomeMetadata instance);

			// AccountModified { get; init; }
			[global::System.Runtime.CompilerServices.UnsafeAccessor(global::System.Runtime.CompilerServices.UnsafeAccessorKind.Field, Name = "<AccountModified>k__BackingField")]
			private static extern ref DateTimeOffset AccountModifiedAccessor(global::Doxense.Serialization.Json.Tests.MyAwesomeMetadata instance);

			// AccountDisabled { get; init; }
			[global::System.Runtime.CompilerServices.UnsafeAccessor(global::System.Runtime.CompilerServices.UnsafeAccessorKind.Field, Name = "<AccountDisabled>k__BackingField")]
			private static extern ref DateTimeOffset? AccountDisabledAccessor(global::Doxense.Serialization.Json.Tests.MyAwesomeMetadata instance);

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

	}

}

#endif
