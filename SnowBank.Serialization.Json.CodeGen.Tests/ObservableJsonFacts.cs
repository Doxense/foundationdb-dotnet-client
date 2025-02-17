#region Copyright (c) 2023-2024 SnowBank SAS
//
// All rights are reserved. Reproduction or transmission in whole or in part, in
// any form or by any means, electronic, mechanical or otherwise, is prohibited
// without the prior written consent of the copyright owner.
//
#endregion

namespace Doxense.Serialization.Json.Tests
{

	[TestFixture]
	[Category("Core-SDK")]
	[Category("Core-JSON")]
	[Parallelizable(ParallelScope.All)]
	public sealed class GeneratedObservableJsonFacts : SimpleTest
	{

		public class FakeObservableTransaction : IObservableJsonTransaction
		{

			public List<(string Op, string? Path, JsonValue? Argument)> Changes { get; } = [ ];

			public bool Logged { get; set; }

			/// <inheritdoc />
			public bool HasMutations => this.Changes.Count > 0;

			/// <inheritdoc />
			public int Count => this.Changes.Count;

			/// <inheritdoc />
			public ObservableJsonValue NewObject(ObservableJsonPath path) => new(this, path, JsonObject.Create());

			/// <inheritdoc />
			public ObservableJsonValue NewArray(ObservableJsonPath path) => new(this, path, new JsonArray());

			/// <inheritdoc />
			public ObservableJsonValue FromJson(ObservableJsonValue parent, string key, JsonValue? value) => ObservableJson.FromJson(this, new(parent, key), value ?? JsonNull.Null);

			/// <inheritdoc />
			public ObservableJsonValue FromJson(ObservableJsonValue parent, int index, JsonValue? value) => ObservableJson.FromJson(this, new(parent, index), value ?? JsonNull.Null);

			/// <inheritdoc />
			public ObservableJsonValue FromJson(ObservableJsonValue parent, Index index, JsonValue? value) => ObservableJson.FromJson(this, new(parent, index), value ?? JsonNull.Null);

			public void Record((string Op, string? Path, JsonValue? Arg) record)
			{
				this.Changes.Add(record);
				if (this.Logged) Log($"# {record}");
			}

			public void Record(string op, ObservableJsonValue? instance, ReadOnlySpan<char> key, JsonValue? arg = null) => Record((op, ObservableJson.ComputePath(instance, key), arg?.Copy()));

			public void Record(string op, ObservableJsonValue? instance, int index, JsonValue? arg = null) => Record((op, ObservableJson.ComputePath(instance, index), arg?.Copy()));

			/// <inheritdoc />
			public void RecordAdd(ObservableJsonValue instance, ReadOnlySpan<char> key, JsonValue argument) => Record("add", instance, key, argument);

			/// <inheritdoc />
			public void RecordAdd(ObservableJsonValue instance, int index, JsonValue argument) => Record("add", instance, index, argument);

			/// <inheritdoc />
			public void RecordUpdate(ObservableJsonValue instance, ReadOnlySpan<char> key, JsonValue argument) => Record("update", instance, key, argument);

			/// <inheritdoc />
			public void RecordUpdate(ObservableJsonValue instance, int index, JsonValue argument) => Record("update", instance, index, argument);

			/// <inheritdoc />
			public void RecordPatch(ObservableJsonValue instance, ReadOnlySpan<char> key, JsonValue argument) => Record("patch", instance, key, argument);

			/// <inheritdoc />
			public void RecordPatch(ObservableJsonValue instance, int index, JsonValue argument) => Record("patch", instance, index, argument);

			/// <inheritdoc />
			public void RecordDelete(ObservableJsonValue instance, ReadOnlySpan<char> key) => Record("delete", instance, key);

			/// <inheritdoc />
			public void RecordDelete(ObservableJsonValue instance, int index) => Record("delete", instance, index);

			/// <inheritdoc />
			public void RecordClear(ObservableJsonValue instance) => Record("delete", instance, "");

			/// <inheritdoc />
			public void Reset() => Record("delete", null, null);

		}

		[Test]
		public void Test_Fill_Empty_Observable_Object()
		{
			var tr = new FakeObservableTransaction() { Logged = true };

			var doc = SomeConverters.Something.ToObservable(tr, new JsonObject());

			doc.Hello = "world";
			doc.Level = 8001;
			doc.Point.X = 123;
			doc.Point.Y = 456;
			doc.Point.Z = 789;
			doc.Foo.Bar.Baz = true;

			Dump(doc.GetValue());

			Assert.That(
				doc.GetValue(),
				IsJson.Object.And.EqualTo(JsonObject.Create(
				[
					("hello", "world"),
					("level", 8001),
					("point", JsonObject.Create(
					[
						("x", 123),
						("y", 456),
						("z", 789)
					])),
					("foo", JsonObject.Create("bar", JsonObject.Create("baz", true))),
				]))
			);

			foreach (var (op, path, arg) in tr.Changes)
			{
				Log($"- {op}, `{path}`, {arg:Q}");
			}

		}

	}

	[CrystalJsonConverter]
	[CrystalJsonSerializable(typeof(Something))]
	public static partial class SomeConverters
	{

	}

	public sealed record Something
	{

		[JsonProperty("hello")]
		public required string Hello { get; init; }

		[JsonProperty("level")]
		public int Level { get; init; }

		[JsonProperty("point")]
		public SomePoint? Point { get; init; }

		[JsonProperty("foo")]
		public SomeFoo? Foo { get; init; }

	}

	public sealed record SomeFoo
	{
		[JsonProperty("bar")]
		public SomeBar? Bar { get; init; }
	}

	public sealed record SomeBar
	{
		[JsonProperty("baz")]
		public bool Baz { get; init; }
	}

	public sealed record SomePoint
	{
		[JsonProperty("x")]
		public double X { get; init; }

		[JsonProperty("y")]
		public double Y { get; init; }

		[JsonProperty("z")]
		public double Z { get; init; }

	}

}
