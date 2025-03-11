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

		public class FakeMutableTransaction : IMutableJsonTransaction
		{

			public List<(string Op, JsonPath Path, JsonValue? Argument)> Changes { get; } = [ ];

			public bool Logged { get; set; }

			/// <inheritdoc />
			public bool HasMutations => this.Changes.Count > 0;

			/// <inheritdoc />
			public int Count => this.Changes.Count;

			/// <inheritdoc />
			public JsonObject NewObject() => JsonObject.Create();

			/// <inheritdoc />
			public JsonArray NewArray() => new JsonArray();

			/// <inheritdoc />
			public MutableJsonValue FromJson(JsonValue value) => new(this, null, default, null, value);

			/// <inheritdoc />
			public MutableJsonValue FromJson(MutableJsonValue parent, ReadOnlyMemory<char> key, JsonValue value) => new(this, parent, key, null, value);

			/// <inheritdoc />
			public MutableJsonValue FromJson(MutableJsonValue parent, Index index, JsonValue value) => new(this, parent, default, index, value);

			private void RecordMutation((string Op, JsonPath Path, JsonValue? Arg) record)
			{
				this.Changes.Add(record);
				if (this.Logged) Log($"# {record}");
			}

			/// <inheritdoc />
			public void RecordAdd(MutableJsonValue instance, ReadOnlyMemory<char> key, JsonValue argument) => RecordMutation(("add", instance.GetPath(key), argument));

			/// <inheritdoc />
			public void RecordAdd(MutableJsonValue instance, int index, JsonValue argument) => RecordMutation(("add", instance.GetPath(index), argument));

			/// <inheritdoc />
			public void RecordUpdate(MutableJsonValue instance, ReadOnlyMemory<char> key, JsonValue argument) => RecordMutation(("update", instance.GetPath(key), argument));

			/// <inheritdoc />
			public void RecordUpdate(MutableJsonValue instance, int index, JsonValue argument) => RecordMutation(("update", instance.GetPath(index), argument));

			/// <inheritdoc />
			public void RecordPatch(MutableJsonValue instance, ReadOnlyMemory<char> key, JsonValue argument) => RecordMutation(("patch", instance.GetPath(key), argument));

			/// <inheritdoc />
			public void RecordPatch(MutableJsonValue instance, int index, JsonValue argument) => RecordMutation(("patch", instance.GetPath(index), argument));

			/// <inheritdoc />
			public void RecordDelete(MutableJsonValue instance, ReadOnlyMemory<char> key) => RecordMutation(("delete", instance.GetPath(key), null));

			/// <inheritdoc />
			public void RecordDelete(MutableJsonValue instance, int index) => RecordMutation(("delete", instance.GetPath(index), null));

			/// <inheritdoc />
			public void RecordTruncate(MutableJsonValue instance, int length) => RecordMutation(("truncate", instance.GetPath(), length));

			/// <inheritdoc />
			public void RecordClear(MutableJsonValue instance) => RecordMutation(("delete", instance.GetPath(), null));

			/// <inheritdoc />
			public void Reset() => this.Changes.Clear();

		}

		[Test]
		public void Test_Fill_Empty_Observable_Object()
		{
			var tr = new FakeMutableTransaction() { Logged = true };

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
