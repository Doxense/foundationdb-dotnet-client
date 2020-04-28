#region BSD License
/* Copyright (c) 2013-2020, Doxense SAS
All rights reserved.

Redistribution and use in source and binary forms, with or without
modification, are permitted provided that the following conditions are met:
	* Redistributions of source code must retain the above copyright
	  notice, this list of conditions and the following disclaimer.
	* Redistributions in binary form must reproduce the above copyright
	  notice, this list of conditions and the following disclaimer in the
	  documentation and/or other materials provided with the distribution.
	* Neither the name of Doxense nor the
	  names of its contributors may be used to endorse or promote products
	  derived from this software without specific prior written permission.

THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS "AS IS" AND
ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED
WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE
DISCLAIMED. IN NO EVENT SHALL <COPYRIGHT HOLDER> BE LIABLE FOR ANY
DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES
(INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES;
LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND
ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT
(INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS
SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
 */
#endregion

namespace FoundationDB.Client.Tests
{
	using System;
	using System.Linq;
	using NUnit.Framework;

	[TestFixture]
	public class FdbDirectoryPathFacts : FdbTest
	{

		[Test]
		public void Test_FdbDirectoryPath_Empty()
		{
			var empty = FdbPath.Empty;
			Assert.That(empty.IsAbsolute, Is.False);
			Assert.That(empty.IsEmpty, Is.True);
			Assert.That(empty.IsRoot, Is.False);
			Assert.That(empty.Count, Is.EqualTo(0));
			Assert.That(empty.ToString(), Is.EqualTo(string.Empty));
			Assert.That(empty.Name, Is.EqualTo(string.Empty));
			Assert.That(empty.Segments.Length, Is.EqualTo(0));
			Assert.That(empty.ToArray(), Is.EqualTo(new string[0]));

			Assert.That(empty, Is.EqualTo(FdbPath.Empty));
			Assert.That(empty == FdbPath.Empty, Is.True);
			Assert.That(empty != FdbPath.Empty, Is.False);
			Assert.That(empty, Is.Not.EqualTo(FdbPath.Root));
			Assert.That(empty == FdbPath.Root, Is.False);
			Assert.That(empty != FdbPath.Root, Is.True);

			Assert.That(FdbPath.Parse("Hello").StartsWith(empty), Is.True);
			Assert.That(FdbPath.Parse("/Hello").StartsWith(empty), Is.False);

		}

		[Test]
		public void Test_FdbDirectoryPath_Root()
		{
			var root = FdbPath.Root;
			Assert.That(root.IsAbsolute, Is.True);
			Assert.That(root.IsEmpty, Is.False);
			Assert.That(root.IsRoot, Is.True);
			Assert.That(root.Count, Is.EqualTo(0));
			Assert.That(root.ToString(), Is.EqualTo("/"));
			Assert.That(root.Name, Is.EqualTo(string.Empty));
			Assert.That(root.Segments.Length, Is.EqualTo(0));
			Assert.That(root.ToArray(), Is.EqualTo(new string[0]));

			Assert.That(root, Is.EqualTo(FdbPath.Root));
			Assert.That(root == FdbPath.Root, Is.True);
			Assert.That(root != FdbPath.Root, Is.False);
			Assert.That(root, Is.Not.EqualTo(FdbPath.Empty));
			Assert.That(root == FdbPath.Empty, Is.False);
			Assert.That(root != FdbPath.Empty, Is.True);

			Assert.That(FdbPath.Parse("Hello").StartsWith(root), Is.False);
			Assert.That(FdbPath.Parse("/Hello").StartsWith(root), Is.True);

		}

		[Test]
		public void Test_FdbDirectoryPath_Simple_Relative()
		{
			var foo = FdbPath.MakeRelative("Foo");
			Assert.That(foo.ToString(), Is.EqualTo("Foo"));
			Assert.That(foo.IsAbsolute, Is.False);
			Assert.That(foo.IsEmpty, Is.False);
			Assert.That(foo.IsRoot, Is.False);
			Assert.That(foo.Count, Is.EqualTo(1));
			Assert.That(foo[0], Is.EqualTo("Foo"));
			Assert.That(foo.Name, Is.EqualTo("Foo"));
			Assert.That(foo.ToArray(), Is.EqualTo(new [] { "Foo" }));
			Assert.That(foo.StartsWith(FdbPath.Empty), Is.True);
			Assert.That(foo.IsChildOf(FdbPath.Empty), Is.True);
			Assert.That(foo.EndsWith(FdbPath.Empty), Is.True);
			Assert.That(foo.IsParentOf(FdbPath.Empty), Is.False);

			var fooBar = foo["Bar"];
			Assert.That(fooBar.ToString(), Is.EqualTo("Foo/Bar"));
			Assert.That(fooBar.IsAbsolute, Is.False);
			Assert.That(fooBar.IsEmpty, Is.False);
			Assert.That(fooBar.IsRoot, Is.False);
			Assert.That(fooBar.Count, Is.EqualTo(2));
			Assert.That(fooBar[0], Is.EqualTo("Foo"));
			Assert.That(fooBar[1], Is.EqualTo("Bar"));
			Assert.That(fooBar.Name, Is.EqualTo("Bar"));
			Assert.That(fooBar.ToArray(), Is.EqualTo(new [] { "Foo", "Bar" }));
			Assert.That(fooBar.StartsWith(FdbPath.Empty), Is.True);
			Assert.That(fooBar.IsChildOf(FdbPath.Empty), Is.True);
			Assert.That(fooBar.IsParentOf(FdbPath.Empty), Is.False);
			Assert.That(fooBar.EndsWith(FdbPath.Empty), Is.True);
			Assert.That(fooBar.StartsWith(foo), Is.True);
			Assert.That(fooBar.IsChildOf(foo), Is.True);
			Assert.That(fooBar.EndsWith(foo), Is.False);
			Assert.That(fooBar.IsParentOf(foo), Is.False);

		}

		[Test]
		public void Test_FdbDirectoryPath_Simple_Absolute()
		{
			var foo = FdbPath.MakeAbsolute("Foo");
			Assert.That(foo.ToString(), Is.EqualTo("/Foo"));
			Assert.That(foo.IsAbsolute, Is.True);
			Assert.That(foo.IsEmpty, Is.False);
			Assert.That(foo.IsRoot, Is.False);
			Assert.That(foo.Count, Is.EqualTo(1));
			Assert.That(foo[0], Is.EqualTo("Foo"));
			Assert.That(foo.Name, Is.EqualTo("Foo"));
			Assert.That(foo.ToArray(), Is.EqualTo(new [] { "Foo" }));
			Assert.That(foo.StartsWith(FdbPath.Root), Is.True);
			Assert.That(foo.IsChildOf(FdbPath.Root), Is.True);
			Assert.That(foo.EndsWith(FdbPath.Root), Is.False);
			Assert.That(foo.IsParentOf(FdbPath.Root), Is.False);

			var fooBar = foo["Bar"];
			Assert.That(fooBar.ToString(), Is.EqualTo("/Foo/Bar"));
			Assert.That(fooBar.IsAbsolute, Is.True);
			Assert.That(fooBar.IsEmpty, Is.False);
			Assert.That(fooBar.IsRoot, Is.False);
			Assert.That(fooBar.Count, Is.EqualTo(2));
			Assert.That(fooBar[0], Is.EqualTo("Foo"));
			Assert.That(fooBar[1], Is.EqualTo("Bar"));
			Assert.That(fooBar.Name, Is.EqualTo("Bar"));
			Assert.That(fooBar.ToArray(), Is.EqualTo(new [] { "Foo", "Bar" }));
			Assert.That(fooBar.StartsWith(FdbPath.Root), Is.True);
			Assert.That(fooBar.IsChildOf(FdbPath.Root), Is.True);
			Assert.That(fooBar.IsParentOf(FdbPath.Root), Is.False);
			Assert.That(fooBar.EndsWith(FdbPath.Root), Is.False);
			Assert.That(fooBar.StartsWith(foo), Is.True);
			Assert.That(fooBar.IsChildOf(foo), Is.True);
			Assert.That(fooBar.IsParentOf(foo), Is.False);
			Assert.That(fooBar.EndsWith(foo), Is.False);
		}

		[Test]
		public void Test_FdbDirectoryPath_Substring_Absolute()
		{
			var path = FdbPath.MakeAbsolute("Foo", "Bar", "Baz");

			var slice = path.Substring(0, 2);
			Assert.That(slice.IsAbsolute, Is.True);
			Assert.That(slice[0], Is.EqualTo("Foo"));
			Assert.That(slice[1], Is.EqualTo("Bar"));
		}

		[Test]
		public void Test_FdbDirectoryPath_Parse()
		{
			// Relative paths

			FdbPath Parse(string value)
			{
				Log($"\"{value}\":");
				var path = FdbPath.Parse(value);
				if (path.IsEmpty)
					Log("> <empty>");
				else if (path.IsRoot)
					Log("> <root>");
				else 
					Log($"> Path='{path.ToString()}', Count={path.Count}, Name='{path.Name}', Absolute={path.IsAbsolute}");
				return path;
			}

			{ // Empty
				var path = Parse("");
				Assert.That(path.IsAbsolute, Is.False, ".Absolute");
				Assert.That(path.IsRoot, Is.False, ".IsRoot");
				Assert.That(path.IsEmpty, Is.True, ".IsEmpty");
				Assert.That(path.Count, Is.EqualTo(0), ".Count");
				Assert.That(path.ToString(), Is.EqualTo(""));
				Assert.That(path.Name, Is.EqualTo(string.Empty));
			}
			{ // Foo
				var path = Parse("Foo");
				Assert.That(path.IsAbsolute, Is.False, ".Absolute");
				Assert.That(path.IsRoot, Is.False, ".IsRoot");
				Assert.That(path.IsEmpty, Is.False, ".IsEmpty");
				Assert.That(path.Count, Is.EqualTo(1), ".Count");
				Assert.That(path[0], Is.EqualTo("Foo"));
				Assert.That(path.ToString(), Is.EqualTo("Foo"));
				Assert.That(path.Name, Is.EqualTo("Foo"));
			}
			{ // Foo/Bar/Baz
				var path = Parse("Foo/Bar/Baz");
				Assert.That(path.IsAbsolute, Is.False, ".Absolute");
				Assert.That(path.IsRoot, Is.False, ".IsRoot");
				Assert.That(path.IsEmpty, Is.False, ".IsEmpty");
				Assert.That(path.Count, Is.EqualTo(3), ".Count");
				Assert.That(path[0], Is.EqualTo("Foo"));
				Assert.That(path[1], Is.EqualTo("Bar"));
				Assert.That(path[2], Is.EqualTo("Baz"));
				Assert.That(path.ToString(), Is.EqualTo("Foo/Bar/Baz"));
				Assert.That(path.Name, Is.EqualTo("Baz"));
			}

			// Absolute path

			{ // Root ("/")
				var path = Parse("/");
				Assert.That(path.IsAbsolute, Is.True, ".Absolute");
				Assert.That(path.IsEmpty, Is.False, ".IsEmpty");
				Assert.That(path.IsRoot, Is.True, ".IsRoot");
				Assert.That(path.Count, Is.EqualTo(0));
				Assert.That(path.ToString(), Is.EqualTo("/"));
				Assert.That(path.Name, Is.EqualTo(string.Empty));
			}
			{ // /Foo
				var path = Parse("/Foo");
				Assert.That(path.IsAbsolute, Is.True, ".Absolute");
				Assert.That(path.IsEmpty, Is.False, ".IsEmpty");
				Assert.That(path.IsRoot, Is.False, ".IsRoot");
				Assert.That(path.Count, Is.EqualTo(1));
				Assert.That(path[0], Is.EqualTo("Foo"));
				Assert.That(path.ToString(), Is.EqualTo("/Foo"));
				Assert.That(path.Name, Is.EqualTo("Foo"));
			}

			{ // /Foo/Bar/Baz
				var path = Parse("/Foo/Bar/Baz");
				Assert.That(path.IsAbsolute, Is.True, ".Absolute");
				Assert.That(path.IsEmpty, Is.False, ".IsEmpty");
				Assert.That(path.IsRoot, Is.False, ".IsRoot");
				Assert.That(path.Count, Is.EqualTo(3));
				Assert.That(path[0], Is.EqualTo("Foo"));
				Assert.That(path[1], Is.EqualTo("Bar"));
				Assert.That(path[2], Is.EqualTo("Baz"));
				Assert.That(path.ToString(), Is.EqualTo("/Foo/Bar/Baz"));
				Assert.That(path.Name, Is.EqualTo("Baz"));
			}
			{ // /Foo\/Bar/Baz => { "Foo/Bar", "Baz" }
				var path = Parse("/Foo\\/Bar/Baz");
				Assert.That(path.IsAbsolute, Is.True, ".Absolute");
				Assert.That(path.IsEmpty, Is.False, ".IsEmpty");
				Assert.That(path.IsRoot, Is.False, ".IsRoot");
				Assert.That(path.Count, Is.EqualTo(2));
				Assert.That(path[0], Is.EqualTo("Foo/Bar"));
				Assert.That(path[1], Is.EqualTo("Baz"));
				Assert.That(path.ToString(), Is.EqualTo("/Foo\\/Bar/Baz"));
				Assert.That(path.Name, Is.EqualTo("Baz"));
			}
			{ // /Foo[Bar]/Baz => { "Foo[Bar]", "Baz" }
				var path = Parse("/Foo\\[Bar]/Baz");
				Assert.That(path.IsAbsolute, Is.True, ".Absolute");
				Assert.That(path.IsEmpty, Is.False, ".IsEmpty");
				Assert.That(path.IsRoot, Is.False, ".IsRoot");
				Assert.That(path.Count, Is.EqualTo(2));
				Assert.That(path[0], Is.EqualTo("Foo[Bar]"));
				Assert.That(path[1], Is.EqualTo("Baz"));
				Assert.That(path.ToString(), Is.EqualTo("/Foo\\[Bar]/Baz"));
				Assert.That(path.Name, Is.EqualTo("Baz"));
			}

		}

	}
}
