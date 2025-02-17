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
			var obj = tr.NewObject(ObservableJsonPath.Root);

			obj["hello"].Set("world");
			obj["level"].Set(8001);
			obj["point"]["x"].Set(123);
			obj["point"].Set("y", 456);
			obj.Set(JsonPath.Create("point.z"), 789);
			obj["foo"]["bar"].Set("baz", true);

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
				]))
			);

			foreach (var (op, path, arg) in tr.Changes)
			{
				Log($"- {op}, `{path}`, {arg:Q}");
			}

		}

	}

}
