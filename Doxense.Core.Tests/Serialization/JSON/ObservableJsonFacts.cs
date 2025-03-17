#region Copyright (c) 2023-2024 SnowBank SAS
//
// All rights are reserved. Reproduction or transmission in whole or in part, in
// any form or by any means, electronic, mechanical or otherwise, is prohibited
// without the prior written consent of the copyright owner.
//
#endregion

namespace Doxense.Serialization.Json.Tests
{
	using System.Collections;
	using System.Collections.Generic;
	using System.Diagnostics;
	using System.Runtime.CompilerServices;
	using System.Runtime.InteropServices;

	[TestFixture]
	[Category("Core-SDK")]
	[Category("Core-JSON")]
	[Parallelizable(ParallelScope.All)]
	public sealed class ObservableJsonFacts : SimpleTest
	{

		public class FakeMutableContext : IMutableJsonContext
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
			public MutableJsonValue FromJson(JsonValue value) => new(this, null, default, value);

			/// <inheritdoc />
			public MutableJsonValue FromJson(MutableJsonValue parent, JsonPathSegment segment, JsonValue value) => new(this, parent, segment, value);

			private void RecordMutation((string Op, JsonPath Path, JsonValue? Arg) record)
			{
				this.Changes.Add(record);
				if (this.Logged) Log($"# {record}");
			}

			/// <inheritdoc />
			public void RecordAdd(IJsonProxyNode instance, JsonPathSegment child, JsonValue argument) => RecordMutation(("add", instance.GetPath(child), argument));

			/// <inheritdoc />
			public void RecordUpdate(IJsonProxyNode instance, JsonPathSegment child, JsonValue argument) => RecordMutation(("update", instance.GetPath(child), argument));

			/// <inheritdoc />
			public void RecordPatch(IJsonProxyNode instance, JsonPathSegment child, JsonValue argument) => RecordMutation(("patch", instance.GetPath(child), argument));

			/// <inheritdoc />
			public void RecordDelete(IJsonProxyNode instance, JsonPathSegment child) => RecordMutation(("delete", instance.GetPath(child), null));

			/// <inheritdoc />
			public void RecordTruncate(IJsonProxyNode instance, int length) => RecordMutation(("truncate", instance.GetPath(), length));

			/// <inheritdoc />
			public void RecordClear(IJsonProxyNode instance) => RecordMutation(("delete", instance.GetPath(), null));

			/// <inheritdoc />
			public void Reset() => this.Changes.Clear();

		}

		[Test]
		public void Test_Fill_Empty_Observable_Object()
		{
			var tr = new FakeMutableContext() { Logged = true };
			var obj = tr.FromJson(tr.NewObject());

			obj["hello"].Set("world");
			obj["level"].Set(8001);
			obj["point"]["x"].Set(123);
			obj["point"].Set("y", 456);
			obj.Set(JsonPath.Create("point.z"), 789);
			obj["foo"]["bar"].Set("baz", true);
			obj["items"][1].Set("two");

			Dump(obj.ToJson());

			Assert.That(
				obj.ToJson(),
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

		private static JsonObject GetSampleObject() => new()
		{
			["foo"] = 123,
			["bar"] = true,
			["hello"] = JsonObject.Create([ ("world", 456), ("there", 789) ]),
			["items"] = JsonArray.Create([ "one", "two", "three", "four" ]),
			["point"] = JsonObject.Create([ ("x", 1.23), ("y", -2.45), ("z", Math.PI) ]),
		};

		private static List<(JsonPath Path, ObservableJsonAccess Access, JsonValue? Value)> CaptureReads(JsonValue value, Action<ObservableJsonValue> handler, [CallerArgumentExpression(nameof(handler))] string? expr = null)
		{
			var ctx = new ObservableJsonTraceCapturingContext();
			Log("# " + expr);
			handler(ctx.FromJson(value));
			var records = ctx.Trace.GetRecords();
			foreach (var (op, path, arg) in records)
			{
				Log($"  - <{op}> `{path}` => {arg:Q}");
			}
			return records;
		}

		private static ObservableJsonTrace CaptureTrace(JsonValue value, Action<ObservableJsonValue> handler, [CallerArgumentExpression(nameof(handler))] string? expr = null)
		{
			var ctx = new ObservableJsonTraceCapturingContext();
			Log("# " + expr);
			handler(ctx.FromJson(value));
			foreach (var (path, op, arg) in ctx.Trace.GetRecords())
			{
				Log($"- `{path}` =>  <{op}> {arg:Q}");
			}
			return ctx.Trace;
		}

		[Test]
		public void Test_Capture_List_Of_Reads()
		{
			var obj = GetSampleObject();

			{ // Get<T>(...)
				var reads = CaptureReads(obj, (doc) => _ = doc.Get<int>("foo"));
				Assert.That(reads, Has.Count.EqualTo(1));
				Assert.That(reads[0], Is.EqualTo(("foo", ObservableJsonAccess.Value, (JsonValue) 123)));
			}

			{ // Get(...).As<T>()
				var reads = CaptureReads(obj, (doc) => _ = doc["foo"].As<int>());
				Assert.That(reads, Has.Count.EqualTo(1));
				Assert.That(reads[0], Is.EqualTo(("foo", ObservableJsonAccess.Value, obj["foo"])));
			}

			{ // ContainsKey(...)
				var reads = CaptureReads(obj, (doc) => _ = doc.ContainsKey("foo"));
				Assert.That(reads, Has.Count.EqualTo(1));
				Assert.That(reads[0], Is.EqualTo(("foo", ObservableJsonAccess.Exists, obj["foo"])));
			}

			{ // traverse parent to read multiple children
				var reads = CaptureReads(obj, (doc) => _ = doc["point"]["x"].As<double>() + doc["point"]["y"].As<double>());
				Assert.That(reads, Has.Count.EqualTo(2));
				Assert.That(reads[0], Is.EqualTo(("point.x", ObservableJsonAccess.Value, obj["point"]["x"])));
				Assert.That(reads[1], Is.EqualTo(("point.y", ObservableJsonAccess.Value, obj["point"]["y"])));
			}

			{ // reading child, then parent, should collapse into a single read
				var reads = CaptureReads(obj, (doc) =>
				{
					_ = doc["point"]["x"].As<double>();
					_ = doc["point"]["y"].As<double>();
					_ = doc["point"].As<Dictionary<string, double>>(); // should override the reads for x & y
				});
				Assert.That(reads, Has.Count.EqualTo(1));
				Assert.That(reads[0], Is.EqualTo(("point", ObservableJsonAccess.Value, obj["point"])));
			}
			{ // reading child, then testing parent for equality, should NOT collapse into a single read
				var reads = CaptureReads(obj, (doc) =>
				{
					_ = doc["point"]["x"].As<double>();
					_ = doc["point"]["y"].As<double>();
					_ = doc["point"].Exists(); // should keep the reads for x & y
				});
				Assert.That(reads, Has.Count.EqualTo(3));
				Assert.That(reads[0], Is.EqualTo(("point", ObservableJsonAccess.Exists, JsonBoolean.True)));
				Assert.That(reads[1], Is.EqualTo(("point.x", ObservableJsonAccess.Value, obj["point"]["x"])));
				Assert.That(reads[2], Is.EqualTo(("point.y", ObservableJsonAccess.Value, obj["point"]["y"])));
			}

			{ // conditional branch
				var reads = CaptureReads(obj, (doc) => _ = doc.Get<bool>("bar") ? doc["hello"].Get<int>("world") : doc["hello"].Get<int>("there"));
				Assert.That(reads, Has.Count.EqualTo(2));
				Assert.That(reads[0], Is.EqualTo(("bar", ObservableJsonAccess.Value, (JsonValue) true)));
				Assert.That(reads[1], Is.EqualTo(("hello.world", ObservableJsonAccess.Value, obj["hello"]["world"])));
			}

			{ // array item
				var reads = CaptureReads(obj, (doc) => _ = doc["items"][1].As<string>());
				Assert.That(reads, Has.Count.EqualTo(1));
				Assert.That(reads[0], Is.EqualTo(("items[1]", ObservableJsonAccess.Value, obj["items"][1])));
			}

			{ // array item from end
				var reads = CaptureReads(obj, (doc) => _ = doc["items"][^1].As<string>());
				Assert.That(reads, Has.Count.EqualTo(1));
				Assert.That(reads[0], Is.EqualTo(("items[^1]", ObservableJsonAccess.Value, obj["items"][^1])));
			}

			{ // array count
				var reads = CaptureReads(obj, (doc) => _ = doc["items"].Count);
				Assert.That(reads, Has.Count.EqualTo(1));
				Assert.That(reads[0], Is.EqualTo(("items", ObservableJsonAccess.Length, JsonNumber.Return(4))));
			}

			{ // array count on missing field
				var reads = CaptureReads(obj, (doc) => _ = doc["not_found"].Count);
				Assert.That(reads, Has.Count.EqualTo(1));
				Assert.That(reads[0], Is.EqualTo(("not_found", ObservableJsonAccess.Type, JsonNumber.Return((int) JsonType.Null))));
			}

		}

		[Test]
		public void Test_Capture_Trace()
		{
			var obj = GetSampleObject();

			{ // Get<T>(...)
				var trace = CaptureTrace(obj, (doc) => _ = doc.Get<int>("foo"));

				// the root was traversed
				Assert.That(trace.Root.Access, Is.EqualTo(ObservableJsonAccess.None));
				Assert.That(trace.Root.Value, Is.Null);
				Assert.That(trace.Root.Children, Is.Not.Null.And.ContainKey(new JsonPathSegment("foo")));
				// the 'foo' field was accessed by value
				var foo = trace.Root.Children["foo"];
				Assert.That(foo.Access, Is.EqualTo(ObservableJsonAccess.Value));
				Assert.That(foo.Value, Is.SameAs(obj["foo"]));
				Assert.That(foo.Children, Is.Null);
			}

			{ // ContainsKey(...)
				var trace = CaptureTrace(obj, (doc) => _ = doc.ContainsKey("foo"));

				// the root was traversed
				Assert.That(trace.Root.Access, Is.EqualTo(ObservableJsonAccess.None));
				Assert.That(trace.Root.Value, Is.Null);
				Assert.That(trace.Root.Children, Is.Not.Null.And.ContainKey(new JsonPathSegment("foo")));
				// the 'foo' field was tested for existence
				var foo = trace.Root.Children["foo"];
				Assert.That(foo.Access, Is.EqualTo(ObservableJsonAccess.Exists));
				Assert.That(foo.Value, IsJson.True);
				Assert.That(foo.Children, Is.Null);
			}

			{ // traverse node to read child
				var trace = CaptureTrace(obj, (doc) => _ = doc["point"].Get<double>("x"));

				// the root was traversed
				Assert.That(trace.Root.Access, Is.EqualTo(ObservableJsonAccess.None));
				Assert.That(trace.Root.Value, Is.Null);
				Assert.That(trace.Root.Children, Is.Not.Null.And.ContainKey(new JsonPathSegment("point")));
				// the 'point' field was traversed
				var point = trace.Root.Children["point"];
				Assert.That(point.Access, Is.EqualTo(ObservableJsonAccess.None));
				Assert.That(point.Value, Is.Null);
				Assert.That(point.Children, Is.Not.Null.And.ContainKey(new JsonPathSegment("x")));
				// the 'x' field was read by value
				var x = point.Children["x"];
				Assert.That(x.Access, Is.EqualTo(ObservableJsonAccess.Value));
				Assert.That(x.Value, Is.SameAs(obj["point"]["x"]));
				Assert.That(x.Children, Is.Null);
			}

			{ // conditional branch
				var trace = CaptureTrace(obj, (doc) => _ = doc.Get<bool>("bar") ? doc["hello"].Get<int>("world") : doc["hello"].Get<int>("there"));

				// the root was traversed
				Assert.That(trace.Root.Access, Is.EqualTo(ObservableJsonAccess.None));
				Assert.That(trace.Root.Value, Is.Null);
				Assert.That(trace.Root.Children, Is.Not.Null);
				// the 'bar' field was read by value
				Assert.That(trace.Root.Children, Does.ContainKey(new JsonPathSegment("bar")));
				var bar = trace.Root.Children["bar"];
				Assert.That(bar.Access, Is.EqualTo(ObservableJsonAccess.Value));
				Assert.That(bar.Value, Is.SameAs(obj["bar"]));
				Assert.That(bar.Children, Is.Null);
				// then, the hello field was traversed
				var hello = trace.Root.Children["hello"];
				Assert.That(trace.Root.Children, Does.ContainKey(new JsonPathSegment("hello")));
				Assert.That(hello.Access, Is.EqualTo(ObservableJsonAccess.None));
				Assert.That(hello.Value, Is.Null);
				Assert.That(hello.Children, Is.Not.Null);
				// and the hello.world field was read by value
				Assert.That(hello.Children, Does.ContainKey(new JsonPathSegment("world")));
				var world = hello.Children["world"];
				Assert.That(world.Access, Is.EqualTo(ObservableJsonAccess.Value));
				Assert.That(world.Value, Is.SameAs(obj["hello"]["world"]));
				Assert.That(world.Children, Is.Null);
				// nothing else
				Assert.That(hello.Children, Has.Count.EqualTo(1));
				Assert.That(trace.Root.Children, Has.Count.EqualTo(2));
			}

			{ // array item
				var trace = CaptureTrace(obj, (doc) => _ = doc["items"][1].As<string>());
			}

			{ // array item from end
				var trace = CaptureTrace(obj, (doc) => _ = doc["items"][^1].As<string>());
			}

			{ // array count
				var trace = CaptureTrace(obj, (doc) => _ = doc["items"].Count);
			}
		}

		[Test]
		public void Test_Match_Trace_Get_Direct_Field()
		{
			var obj = GetSampleObject();

			var trace = CaptureTrace(obj, (doc) => _ = doc.Get<int>("foo"));

			Assert.That(trace.IsMatch(obj), Is.True, "Same object should match the trace");
			Assert.That(trace.IsMatch(obj.Copy()), Is.True, "A perfect copy of the object should match the trace");
			Assert.That(trace.IsMatch(obj.CopyAndSet("bar", false)), Is.True, "Changing another field should match");
			Assert.That(trace.IsMatch(obj.CopyAndSet("foo", 124)), Is.False, "Changing a field with Value access should not match");
			Assert.That(trace.IsMatch(obj.CopyAndRemove("foo")), Is.False, "Removing a captured field should not match");
		}

		[Test]
		public void Test_Match_Trace_Get_Indirect_Field()
		{
			var obj = GetSampleObject();
			var trace = CaptureTrace(obj, (doc) => _ = doc["point"].Get<double>("x"));

			Assert.That(trace.IsMatch(obj), Is.True, "Same object should match the trace");
			Assert.That(trace.IsMatch(obj.Copy()), Is.True, "A perfect copy of the object should match the trace");
			Assert.That(trace.IsMatch(obj.CopyAndSet(JsonPath.Empty["point"]["y"], Math.E)), Is.True, "Changing another field should match");
			Assert.That(trace.IsMatch(obj.CopyAndSet(JsonPath.Empty["point"]["x"], 42)), Is.False, "Changing a field with Value access should not match");
			Assert.That(trace.IsMatch(obj.CopyAndSet(JsonPath.Empty["point"]["x"], JsonNull.Null)), Is.False, "Removing a field with Value access should not match");
			Assert.That(trace.IsMatch(obj.CopyAndSet(JsonPath.Empty["point"]["x"], JsonNull.Missing)), Is.False, "Removing a field with Value access should not match");
			Assert.That(trace.IsMatch(obj.CopyAndRemove("point")), Is.False, "Removing a parent of a captured field should not match");
		}

		[Test]
		public void Test_Match_Trace_ContainsKey()
		{
			var obj = GetSampleObject();
			var trace = CaptureTrace(obj, (doc) => _ = doc.ContainsKey("foo"));

			Assert.That(trace.IsMatch(obj), Is.True, "Same object should match the trace");
			Assert.That(trace.IsMatch(obj.Copy()), Is.True, "A perfect copy of the object should match the trace");
			Assert.That(trace.IsMatch(obj.CopyAndSet("bar", false)), Is.True, "Changing another field should match");
			Assert.That(trace.IsMatch(obj.CopyAndSet("foo", 124)), Is.True, "Changing a field with Exists access should match");
			Assert.That(trace.IsMatch(obj.CopyAndRemove("foo")), Is.False, "Removing a field with Exists access should not match");
		}

		[Test]
		public void Test_Match_Trace_Conditional_Branch()
		{
			var obj = GetSampleObject();
			var trace = CaptureTrace(obj, (doc) => _ = doc.Get<bool>("bar") ? doc["hello"].Get<int>("world") : doc["hello"].Get<int>("there"));

			Assert.That(trace.IsMatch(obj), Is.True, "Same object should match the trace");
			Assert.That(trace.IsMatch(obj.Copy()), Is.True, "A perfect copy of the object should match the trace");
			Assert.That(trace.IsMatch(obj.CopyAndSet(JsonPath.Empty["foo"], 456)), Is.True, "Changing another field should match");
			Assert.That(trace.IsMatch(obj.CopyAndSet(JsonPath.Empty["bar"], false)), Is.False, "Changing the value of the conditional should not match");
			Assert.That(trace.IsMatch(obj.CopyAndSet(JsonPath.Empty["hello"]["there"], -555)), Is.True, "Changing the condition branch not taken should match");
			Assert.That(trace.IsMatch(obj.CopyAndSet(JsonPath.Empty["hello"]["world"], -555)), Is.False, "Changing the condition branch taken should match");
		}

	}

}
