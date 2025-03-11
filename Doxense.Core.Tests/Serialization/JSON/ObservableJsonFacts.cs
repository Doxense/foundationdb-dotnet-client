#region Copyright (c) 2023-2024 SnowBank SAS
//
// All rights are reserved. Reproduction or transmission in whole or in part, in
// any form or by any means, electronic, mechanical or otherwise, is prohibited
// without the prior written consent of the copyright owner.
//
#endregion

namespace Doxense.Serialization.Json.Tests
{
	using System.Collections.Generic;

	[TestFixture]
	[Category("Core-SDK")]
	[Category("Core-JSON")]
	[Parallelizable(ParallelScope.All)]
	public sealed class ObservableJsonFacts : SimpleTest
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
			var obj = tr.FromJson(tr.NewObject());

			obj["hello"].Set("world");
			obj["level"].Set(8001);
			obj["point"]["x"].Set(123);
			obj["point"].Set("y", 456);
			obj.Set(JsonPath.Create("point.z"), 789);
			obj["foo"]["bar"].Set("baz", true);
			obj["items"][1].Set("two");

			Dump(obj.Json);

			Assert.That(
				obj.Json,
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
					("items", JsonArray.Create(null, "two")),
				]))
			);

			foreach (var (op, path, arg) in tr.Changes)
			{
				Log($"- {op}, `{path}`, {arg:Q}");
			}

		}

		public class ObservableJsonCapturingContext : IObservableJsonContext
		{

			public List<(string Op, JsonPath Path, JsonValue Value)> Reads { get; } = [ ];

			public ObservableJsonValue FromJson(JsonValue value) => new(this, null, default, null, value);

			public ObservableJsonValue FromJson(ObservableJsonValue? parent, ReadOnlyMemory<char> key, JsonValue value) => new(this, parent, key, null, value);

			public ObservableJsonValue FromJson(ObservableJsonValue? parent, Index index, JsonValue value) => new(this, parent, default, index, value);

			/// <inheritdoc />
			public void RecordRead(ObservableJsonValue instance, ReadOnlyMemory<char> key, JsonValue argument, bool existOnly) => this.Reads.Add((existOnly ? "test" : "read", instance.GetPath(key), argument));

			/// <inheritdoc />
			public void RecordRead(ObservableJsonValue instance, Index index, JsonValue argument, bool existOnly) => this.Reads.Add((existOnly ? "test" : "read", instance.GetPath(index), argument));

			/// <inheritdoc />
			public void RecordLength(ObservableJsonValue instance, JsonValue argument) => this.Reads.Add(("size", instance.GetPath(), argument));

			/// <inheritdoc />
			public void Reset()
			{
				this.Reads.Clear();
			}

		}

		private static JsonObject GetSampleObject() => new JsonObject()
		{
			["foo"] = 123,
			["bar"] = true,
			["hello"] = JsonObject.Create([ ("world", 456), ("there", 789) ]),
			["items"] = JsonArray.Create([ "one", "two", "three", "four" ]),
			["point"] = JsonObject.Create([ ("x", 1.0), ("y", -2.0), ("z", 3.0) ]),
		};

		[Test]
		public void Test_Read_Access()
		{
			var tr = new ObservableJsonCapturingContext();
			var obj = GetSampleObject();
			var doc = tr.FromJson(obj);

			Assert.That(doc["foo"].As<int>(), Is.EqualTo(123));
			Assert.That(doc.Get<bool>("bar"), Is.True);
			Assert.That(doc["hello"]["world"].ToJson(), IsJson.EqualTo(456));
			Assert.That(doc["point"].ContainsKey("x"), Is.True);
			Assert.That(doc["point"]["y"].Exists(), Is.True);
			Assert.That(doc["point"]["z"].IsNullOrMissing(), Is.False);
			Assert.That(doc["items"][1].As<string>(), Is.EqualTo("two"));
			Assert.That(doc["items"][^1].As<string>(), Is.EqualTo("four"));

			Log("Reads:");
			foreach (var (op, path, value) in tr.Reads)
			{
				Log($"- {op} `{path}` => {value:Q}");
			}
		}

	}

}
