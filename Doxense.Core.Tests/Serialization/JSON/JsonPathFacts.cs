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

namespace Doxense.Serialization.Json.Tests
{
	using System;
	using JetBrains.Annotations;
	using NUnit.Framework;
	using SnowBank.Testing;

	[TestFixture]
	public sealed class JsonPathFacts : SimpleTest
	{

		[Test]
		public void Test_JsonPath_Empty()
		{
			Assert.That(JsonPath.Empty.IsEmpty(), Is.True);
			Assert.That(JsonPath.Empty.ToString(), Is.EqualTo(""));
			Assert.That(JsonPath.Empty, Is.EqualTo(JsonPath.Empty));
			Assert.That(JsonPath.Empty, Is.EqualTo(""));
			Assert.That(JsonPath.Empty.Equals(default(string)), Is.True);

			Assert.That(JsonPath.Empty.IsParentOf(JsonPath.Empty), Is.False);
			Assert.That(JsonPath.Empty.IsChildOf(JsonPath.Empty), Is.False);

			Assert.That(JsonPath.Empty.TryGetKey(out var key), Is.False);
			Assert.That(key.Length, Is.Zero);
			Assert.That(JsonPath.Empty.GetKey().Length, Is.Zero);

			Assert.That(JsonPath.Empty.TryGetIndex(out var index), Is.False);
			Assert.That(index, Is.EqualTo(default(Index)));
			Assert.That(JsonPath.Empty.GetIndex(), Is.Null);

			Assert.That(JsonPath.Empty.ParseNext(out var span, out var idx, out var consumed), Is.EqualTo(JsonPath.Empty));
			Assert.That(span.Length, Is.Zero);
			Assert.That(idx, Is.Default);
			Assert.That(consumed, Is.Zero);
		}

		[Test]
		public void Test_JsonPath_Basics()
		{
			var foo = JsonPath.Create("foo");
			Assert.That(foo.IsEmpty(), Is.False);
			Assert.That(foo.ToString(), Is.EqualTo("foo"));
			Assert.That(foo, Is.EqualTo(foo));
			Assert.That(foo, Is.Not.EqualTo(JsonPath.Empty));
			Assert.That(foo.Equals("foo"), Is.True);
			Assert.That(foo.GetKey().ToString(), Is.EqualTo("foo"));
			Assert.That(foo.GetIndex(), Is.Null);
			Assert.That(foo.GetParent(), Is.EqualTo(JsonPath.Empty));

			var fooBar = JsonPath.Create("foo.bar");
			Assert.That(fooBar.IsEmpty(), Is.False);
			Assert.That(fooBar.ToString(), Is.EqualTo("foo.bar"));
			Assert.That(fooBar, Is.EqualTo(fooBar));
			Assert.That(fooBar, Is.Not.EqualTo(foo));
			Assert.That(fooBar, Is.Not.EqualTo(JsonPath.Empty));
			Assert.That(fooBar.Equals("foo.bar"), Is.True);
			Assert.That(fooBar.GetKey().ToString(), Is.EqualTo("bar"));
			Assert.That(fooBar.GetIndex(), Is.Null);
			Assert.That(fooBar.GetParent(), Is.EqualTo("foo"));

			var foo42 = JsonPath.Create("foo[42]");
			Assert.That(foo42.IsEmpty(), Is.False);
			Assert.That(foo42.ToString(), Is.EqualTo("foo[42]"));
			Assert.That(foo42, Is.EqualTo(foo42));
			Assert.That(foo42, Is.Not.EqualTo(fooBar));
			Assert.That(foo42, Is.Not.EqualTo(foo));
			Assert.That(foo42, Is.Not.EqualTo(JsonPath.Empty));
			Assert.That(foo42.Equals("foo[42]"), Is.True);
			Assert.That(foo42.GetKey().ToString(), Is.EqualTo(""));
			Assert.That(foo42.GetIndex(), Is.EqualTo((Index) 42));
			Assert.That(foo42.GetParent(), Is.EqualTo("foo"));

			var foo42Bar = JsonPath.Create("foo[42].bar");
			Assert.That(foo42Bar.IsEmpty(), Is.False);
			Assert.That(foo42Bar.ToString(), Is.EqualTo("foo[42].bar"));
			Assert.That(foo42Bar, Is.EqualTo(foo42Bar));
			Assert.That(foo42Bar, Is.Not.EqualTo(foo42));
			Assert.That(foo42Bar, Is.Not.EqualTo(fooBar));
			Assert.That(foo42Bar, Is.Not.EqualTo(foo));
			Assert.That(foo42Bar, Is.Not.EqualTo(JsonPath.Empty));
			Assert.That(foo42Bar.Equals("foo[42].bar"), Is.True);
			Assert.That(foo42Bar.GetKey().ToString(), Is.EqualTo("bar"));
			Assert.That(foo42Bar.GetIndex(), Is.Null);
			Assert.That(foo42Bar.GetParent(), Is.EqualTo("foo[42]"));

		}

		[Test]
		public void Test_JsonPath_Concat()
		{
			var root = JsonPath.Empty;
			Assert.That(root["foo"], Is.EqualTo((JsonPath) "foo"));
			Assert.That(root["foo"]["bar"], Is.EqualTo((JsonPath) "foo.bar"));
			Assert.That(root["foo"]["bar"]["baz"], Is.EqualTo((JsonPath) "foo.bar.baz"));
			Assert.That(root["foo"][42], Is.EqualTo((JsonPath) "foo[42]"));
			Assert.That(root["foo"][42]["bar"], Is.EqualTo((JsonPath) "foo[42].bar"));
			Assert.That(root["foo"][42][^1], Is.EqualTo((JsonPath) "foo[42][^1]"));
			Assert.That(root[42], Is.EqualTo((JsonPath) "[42]"));
			Assert.That(root[42]["foo"], Is.EqualTo((JsonPath) "[42].foo"));
			Assert.That(root[42][^1], Is.EqualTo((JsonPath) "[42][^1]"));
		}

		[Test]
		public void Test_Json_GetKey()
		{
			static void Verify(string path, string expected)
			{
				var p = JsonPath.Create(path);
				Log($"# '{p}'.GetIndex() => {(expected.Length > 0 ? expected :  "<null>")}");
				var actual = p.GetKey();
				Assert.That(actual.ToString(), Is.EqualTo(expected), $"Path '{p}' should end with key '{expected}'");

				if (expected == "")
				{
					Assert.That(p.TryGetKey(out actual), Is.False, $"Path '{p}' should not end with a key");
					Assert.That(actual.Length, Is.Zero);
				}
				else
				{
					Assert.That(p.TryGetKey(out actual), Is.True, $"Path '{p}' should not end with a key");
					Assert.That(actual.ToString(), Is.EqualTo(expected), $"Path '{p}' should end with key '{expected}'");
				}
			}

			Verify("foo", "foo");
			Verify("foo.bar", "bar");
			Verify("foo.bar.baz", "baz");
			Verify("foo[42].bar", "bar");
			Verify("[42].bar", "bar");

			Verify("", "");
			Verify("[42]", "");
			Verify("foo[42]", "");
			Verify("foo.bar[42]", "");
		}

		[Test]
		public void Test_Json_Index()
		{
			static void Verify(string path, Index? expected)
			{
				var p = JsonPath.Create(path);
				Log($"# '{p}'.GetIndex() => {(expected?.ToString() ?? "<null>")}");
				var actual = p.GetIndex();
				Assert.That(actual, Is.EqualTo(expected), $"Path '{p}' should end with index '{expected}'");

				if (expected == null)
				{
					Assert.That(p.TryGetIndex(out var index), Is.False, $"Path '{p}' should not end with an index");
					Assert.That(index, Is.Default);
				}
				else
				{
					Assert.That(p.TryGetIndex(out var index), Is.True, $"Path '{p}' should not end with an index");
					Assert.That(index, Is.EqualTo(expected.Value), $"Path '{p}' should end with index '{expected}'");
				}

			}

			Verify("[42]", 42);
			Verify("foo[42]", 42);
			Verify("foo.bar[42]", 42);
			Verify("[^1]", ^1);
			Verify("foo[^1]", ^1);
			Verify("foo.bar[^1]", ^1);
			
			Verify("", null);
			Verify("foo", null);
			Verify("foo.bar", null);
			Verify("foo.bar.baz", null);
			Verify("foo[42].bar", null);
			Verify("[42].bar", null);

		}

		[Test]
		public void Test_JsonPath_GetParent()
		{
			static void Verify(string path, string parent)
			{
				Log($"# '{path}'.GetParent() => '{parent}'");
				Assert.That(JsonPath.Create(path).GetParent().ToString(), Is.EqualTo(parent), $"Parent of '{path}' should be '{parent}'");
			}

			Verify("", "");
			Verify("foo", "");
			Verify("[42]", "");
			Verify("foo.bar", "foo");
			Verify("foo[42]", "foo");
			Verify("[42].foo", "[42]");
			Verify("[42][^1]", "[42]");
			Verify("foo.bar.baz", "foo.bar");
			Verify("foo.bar[42]", "foo.bar");
			Verify("foo[42].baz", "foo[42]");
			Verify("[42].bar.baz", "[42].bar");
		}

		[Test]
		public void Test_JsonPath_IsParentOf()
		{

			static void SouldBeParent(string parent, string child)
			{
				Log($"# '{parent}'.IsParentOf('{child}') => true");
				Assert.That(JsonPath.Create(parent).IsParentOf(JsonPath.Create(child)), Is.True, $"Path '{parent}' should be a parent of '{child}'");
				Assert.That(JsonPath.Create(parent).IsParentOf(child.AsSpan()), Is.True, $"Path '{parent}' should be a parent of '{child}'");
			}

			static void SouldNotBeParent(string parent, string child)
			{
				Log($"# '{parent}'.IsParentOf('{child}') => false");
				Assert.That(JsonPath.Create(parent).IsParentOf(JsonPath.Create(child)), Is.False, $"Path '{parent}' should NOT be a parent of '{child}'");
				Assert.That(JsonPath.Create(parent).IsParentOf(child.AsSpan()), Is.False, $"Path '{parent}' should NOT be a parent of '{child}'");
			}

			// PARENT
			SouldBeParent("", "foo");
			SouldBeParent("", "[42]");
			SouldBeParent("foo", "foo.bar");
			SouldBeParent("foo", "foo[42]");
			SouldBeParent("foo[42]", "foo[42].bar");
			SouldBeParent("foo[42]", "foo[42][^1]");
			SouldBeParent("a.b.c.d.e.f", "a.b.c.d.e.f.g.h.i");

			// NOT PARENT
			SouldNotBeParent("foo", "bar");
			SouldNotBeParent("foo.bar", "foo");
			SouldNotBeParent("foo[42]", "foo");
			SouldNotBeParent("foos", "foo.bar");
			SouldNotBeParent("foos", "foo[42]");
			SouldNotBeParent("foo", "foos.bar");
			SouldNotBeParent("foo", "foos[42]");
			SouldNotBeParent("a.a.a.a.a.a", "a.a.a.A.a.a.a");
		}

		[Test]
		public void Test_JsonPath_IsChildOf()
		{

			static void ShouldBeChild(string child, string parent)
			{
				Log($"# '{child}'.IsChildOf('{parent}') => true");
				Assert.That(JsonPath.Create(child).IsChildOf(JsonPath.Create(parent)), Is.True, $"Path '{child}' should be a child of '{parent}'");
				Assert.That(JsonPath.Create(child).IsChildOf(parent.AsSpan()), Is.True, $"Path '{child}' should be a child of '{parent}'");
			}

			static void ShouldNotBeChild(string child, string parent)
			{
				Log($"# '{child}'.IsChildOf('{parent}') => false");
				Assert.That(JsonPath.Create(child).IsChildOf(JsonPath.Create(parent)), Is.False, $"Path '{child}' should NOT be a child of '{parent}'");
				Assert.That(JsonPath.Create(child).IsChildOf(parent.AsSpan()), Is.False, $"Path '{child}' should NOT be a child of '{parent}'");
			}

			// PARENT
			ShouldBeChild("foo", "");
			ShouldBeChild("[42]", "");
			ShouldBeChild("foo.bar", "foo");
			ShouldBeChild("foo[42]", "foo");
			ShouldBeChild("foo[42].bar", "foo[42]");
			ShouldBeChild("foo[42][^1]", "foo[42]");
			ShouldBeChild("a.b.c.d.e.f.g.h.i", "a.b.c.d.e.f");

			// NOT PARENT
			ShouldNotBeChild("bar", "foo");
			ShouldNotBeChild("foo", "foo.bar");
			ShouldNotBeChild("foo", "foo[42]");
			ShouldNotBeChild("foo.bar", "foos");
			ShouldNotBeChild("foo[42]", "foos");
			ShouldNotBeChild("foos.bar", "foo");
			ShouldNotBeChild("foos[42]", "foo");
			ShouldNotBeChild("a.a.a.A.a.a.a", "a.a.a.a.a.a");
		}

		[Test]
		public void Test_JsonPath_GetCommonAncestor()
		{
			static void Verify(string a, string b, string common, string left, string right)
			{
				Log($"# '{a}' / '{b}'");
				var c = JsonPath.Create(a).GetCommonAncestor(JsonPath.Create(b), out var l, out var r);
				Log($"- '{c}' > '{l}'");
				Log($"- '{c}' > '{r}'");
				Assert.That(c.ToString(), Is.EqualTo(common), $"Common path must be '{common}'");
				Assert.That(l.ToString(), Is.EqualTo(left), $"Left path must be '{left}'");
				Assert.That(r.ToString(), Is.EqualTo(right), $"Right path must be '{right}'");
			}

			{
				// ""
				// ""
				Verify("", "", "", "", "");
			}
			{
				// ""
				// foo
				Verify("", "foo", "", "", "foo");
			}
			{
				// foo
				// ""
				Verify("foo", "", "", "foo", "");
			}
			{
				// hello
				// world
				Verify("hello", "world", "", "hello", "world");
			}
			{
				// hello.world
				// foo.bar
				Verify("hello.world", "foo.bar", "", "hello.world", "foo.bar");
			}
			{
				// foo
				// [42]
				Verify("foo", "[42]", "", "foo", "[42]");
			}
			{
				// foo.bar
				// foo.jazz
				Verify("foo.bar", "foo.jazz", "foo", "bar", "jazz");
			}
			{ 
				// foo[1]
				// foo[42]
				Verify("foo[1]", "foo[42]", "foo", "[1]", "[42]");
			}
			{ 
				// foo.bar
				// foo[42]
				Verify("foo.bar", "foo[42]", "foo", "bar", "[42]");
			}
			{ 
				// foo.bar
				// foo.baz
				Verify("foo.bar", "foo.baz", "foo", "bar", "baz");
			}
			{ 
				// foo[41]
				// foo[42]
				Verify("foo[41]", "foo[42]", "foo", "[41]", "[42]");
			}

		}

		[Test]
		public void Test_JsonPath_Tokenizer()
		{

			[MustDisposeResource]
			static JsonPath.Tokenizer Tokenize(string path)
			{
				Log($"Tokenizing '{path}'...");
				return JsonPath.Create(path).GetEnumerator();
			}

			static void Step(ref JsonPath.Tokenizer it, string? key, Index? index, string parent)
			{
				Assert.That(it.MoveNext(), Is.True, $"Expected Next() to be {key}/{index}/{parent}");
				var current = it.Current;
				Log($"- Current: key={(current.Key.IsEmpty ? "<null>" : $"'{current.Key.ToString()}'")} / idx={current.Index.ToString()} / patyh='{current.Parent}'");
				if (key != null)
				{
					Assert.That(current.Key.ToString(), Is.EqualTo(key), $"Expected next token to be key '{key}' with parent '{parent}'");
				}
				else
				{
					Assert.That(current.Key.Length, Is.Zero, $"Expected next token to be index '{index}' with parent '{parent}'");
				}

				if (index != null)
				{
					Assert.That(current.Index, Is.EqualTo(index.Value), $"Expected next token to be index '{index}' with parent '{parent}'");
				}
				else
				{
					Assert.That(current.Index, Is.Default, $"Expected next token to be key '{key}' with parent '{parent}'");
				}

				Assert.That(current.Parent.ToString(), Is.EqualTo(parent));
			}

			static void End(ref JsonPath.Tokenizer it)
			{
				Assert.That(it.MoveNext(), Is.False);
				Log("- End");
				it.Dispose();
			}

			{
				var it = Tokenize("");
				End(ref it);
			}

			{
				var it = Tokenize("foo");
				Step(ref it, "foo", null, "");
				End(ref it);
			}

			{
				var it = Tokenize("[42]");
				Step(ref it, null, 42, "");
				End(ref it);
			}

			{
				var it = Tokenize("foo.bar");
				Step(ref it, "foo", null, "");
				Step(ref it, "bar", null, "foo");
				End(ref it);
			}

			{
				var it = Tokenize("foo[42]");
				Step(ref it, "foo", null, "");
				Step(ref it, null, 42, "foo");
				End(ref it);
			}

			{
				var it = Tokenize("[42].foo");
				Step(ref it, null, 42, "");
				Step(ref it, "foo", null, "[42]");
				End(ref it);
			}

			{
				var it = Tokenize("[42][^1]");
				Step(ref it, null, 42, "");
				Step(ref it, null, ^1, "[42]");
				End(ref it);
			}

			{
				var it = Tokenize("foo[42].bar");
				Step(ref it, "foo", null, "");
				Step(ref it, null, 42, "foo");
				Step(ref it, "bar", null, "foo[42]");
				End(ref it);
			}

			{
				var it = Tokenize("foo.bar[42].baz[^1][2].jazz[123][456]");
				Step(ref it, "foo", null, "");
				Step(ref it, "bar", null, "foo");
				Step(ref it, null, 42, "foo.bar");
				Step(ref it, "baz", null, "foo.bar[42]");
				Step(ref it, null, ^1, "foo.bar[42].baz");
				Step(ref it, null, 2, "foo.bar[42].baz[^1]");
				Step(ref it, "jazz", null, "foo.bar[42].baz[^1][2]");
				Step(ref it, null, 123, "foo.bar[42].baz[^1][2].jazz");
				Step(ref it, null, 456, "foo.bar[42].baz[^1][2].jazz[123]");
				End(ref it);
			}
		}
	}

}
