#region Copyright (c) 2023 SnowBank SAS, (c) 2005-2023 Doxense SAS
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

namespace Doxense.Runtime.Comparison.Tests
{
	using System;
	using System.Collections.Generic;
	using Doxense.Testing;
	using NUnit.Framework;

	[TestFixture]
	[Category("Core-SDK")]
	public class ModelComparerFacts : DoxenseTest
	{

		private static void EnsureEqual<T>(string label, IEqualityComparer<T> cmp, T left, T right)
		{
			if (!cmp.Equals(left, right))
			{
				Dump("LEFT", left);
				Dump("RIGHT", right);
				Assert.Fail("{0}: Left and Right {1} should be equal, but were found to be different!", label, typeof(T).Name);
			}
		}

		private static void EnsureDifferent<T>(string label, IEqualityComparer<T> cmp, T left, T right)
		{
			if (cmp.Equals(left, right))
			{
				Dump("LEFT", left);
				Dump("RIGHT", right);
				Assert.Fail("{0}: Left and Right {1} should be different, but were found to be equal!", label, typeof(T).Name);
			}
		}

		[Test]
		public void Test_Compare_Sealed_Model()
		{
			var cmp = ModelComparer.Comparer<SaMereModel>.Default;
			Assert.That(cmp, Is.Not.Null);

			EnsureEqual(
				"null == null",
				cmp,
				null,
				null
			);

			var a = new SaMereModel
			{
				String = "Hello",
				Int32 = 123,
			};

			EnsureEqual(
				"A == A (same instance)",
				cmp,
				a,
				a
			);

			EnsureDifferent(
				"A != null",
				cmp,
				a,
				null
			);

			EnsureDifferent(
				"null != A",
				cmp,
				a,
				null
			);

			var a2 = new SaMereModel
			{
				String = "Hello",
				Int32 = 123,
			};

			EnsureEqual(
				"A == A2 (different instances)",
				cmp,
				a,
				a2
			);

			a2.Bool = true;
			EnsureDifferent(
				"A == A2' (changed content)",
				cmp,
				a,
				a2
			);

			var b = new SaMereModel
			{
				String = "World",
				Int32 = 123,
			};
			EnsureDifferent(
				"A != B (different String)",
				cmp,
				a,
				b
			);

			var c = new SaMereModel
			{
				String = "Hello",
				Int32 = 456,
			};
			EnsureDifferent(
				"A != C (different Int32)",
				cmp,
				a,
				b
			);
			EnsureDifferent(
				"B != C (different String && Int32)",
				cmp,
				b,
				c
			);
		}

		[Test]
		public void Test_Compare_Abstract_Model()
		{
			var cmp = ModelComparer.Comparer<SaMereBase>.Default;
			Assert.That(cmp, Is.Not.Null);

			EnsureEqual("null == null", cmp, null, null);

			var a = new SaMereEnShort
			{
				Foo = "a",
				Short = "a"
			};

			EnsureEqual("A == A (same instance)", cmp, a, a);
			EnsureDifferent("A != null", cmp, a, null);
			EnsureDifferent("null != A", cmp, null, a);

			var a2 = new SaMereEnShort
			{
				Foo = "a",
				Short = "a"
			};
			EnsureEqual("A == A2 (same content)", cmp, a, a2);

			a2.Foo = "aa";
			EnsureDifferent("A != A2' (mutated)", cmp, a, a2);

			var b = new SaMereEnShort
			{
				Foo = "a",
				Short = "b"
			};
			EnsureDifferent("A != B (same type, but different content)", cmp, a, b);

			var c = new SaMereEnBikini()
			{
				Foo = "a",
				Bikini = "c"
			};
			EnsureDifferent("A != C (different types)", cmp, a, c);
			EnsureDifferent("B != C (different types)", cmp, b, c);
		}

		[Test]
		public void Test_Compare_Array_Properties()
		{
			var cmp = ModelComparer.Comparer<ArrayModel>.Default;

			EnsureEqual(
				"null == null",
				cmp,
				new ArrayModel(),
				new ArrayModel()
			);

			EnsureEqual(
				"[0] == [0] (singleton)",
				cmp,
				new ArrayModel { Items = Array.Empty<string>() },
				new ArrayModel { Items = Array.Empty<string>() }
			);

			EnsureEqual(
				"[0] == [0] (different instances)",
				cmp,
				new ArrayModel { Items = new string[0] },
				new ArrayModel { Items = new string[0] }
			);

			EnsureDifferent(
				"[0] == null",
				cmp,
				new ArrayModel { Items = new string[0] },
				new ArrayModel { Items = null }
			);

			EnsureEqual(
				"Same length and content",
				cmp,
				new ArrayModel { Items = new[] {"Hello", "World" } },
				new ArrayModel { Items = new[] {"Hello", "World" } }
			);

			EnsureDifferent(
				"Different length",
				cmp,
				new ArrayModel { Items = new[] {"Hello", "World" } },
				new ArrayModel { Items = new[] {"Hello", "World", "!"} }
			);

			EnsureDifferent(
				"Same length, different content",
				cmp,
				new ArrayModel { Items = new[] {"Hello", "World" } },
				new ArrayModel { Items = new[] {"Hello", "Monde" } }
			);

			EnsureDifferent(
				"Case sensitive",
				cmp,
				new ArrayModel { Items = new[] {"Hello", "World" } },
				new ArrayModel { Items = new[] {"hello", "WORLD" } }
			);
		}

		[Test]
		public void Test_Compare_List_Properties()
		{
			var cmp = ModelComparer.Comparer<ListModel>.Default;

			EnsureEqual(
				"null == null",
				cmp,
				new ListModel(),
				new ListModel()
			);

			EnsureEqual(
				"[0] == [0]",
				cmp,
				new ListModel { Items = new List<string>() },
				new ListModel { Items = new List<string>() }
			);

			EnsureDifferent(
				"[0] == null",
				cmp,
				new ListModel { Items = new List<string>() },
				new ListModel { Items = null }
			);

			EnsureEqual(
				"Same length and content",
				cmp,
				new ListModel { Items = new List<string> { "Hello", "World" } },
				new ListModel { Items = new List<string> { "Hello", "World" } }
			);

			EnsureDifferent(
				"Order does matter",
				cmp,
				new ListModel { Items = new List<string> { "Hello", "World" } },
				new ListModel { Items = new List<string> { "World", "Hello" } }
			);

			EnsureDifferent(
				"Different length",
				cmp,
				new ListModel { Items = new List<string> { "Hello", "World" } },
				new ListModel { Items = new List<string> { "Hello", "World", "!"} }
			);

			EnsureDifferent(
				"Same length, different content",
				cmp,
				new ListModel { Items = new List<string> { "Hello", "World" } },
				new ListModel { Items = new List<string> { "Hello", "Monde" } }
			);

			EnsureDifferent(
				"Case sensitive",
				cmp,
				new ListModel { Items = new List<string> { "Hello", "World" } },
				new ListModel { Items = new List<string> { "hello", "WORLD" } }
			);
		}

		[Test]
		public void Test_Compare_Dictionary_Properties()
		{
			var cmp = ModelComparer.Comparer<DictionaryModel>.Default;

			EnsureEqual(
				"null == null",
				cmp,
				new DictionaryModel(),
				new DictionaryModel()
			);

			EnsureEqual(
				"[0] == [0]",
				cmp,
				new DictionaryModel { Items = new Dictionary<string, FooModel>() },
				new DictionaryModel { Items = new Dictionary<string, FooModel>() }
			);

			EnsureDifferent(
				"[0] == null",
				cmp,
				new DictionaryModel { Items = new Dictionary<string, FooModel>() },
				new DictionaryModel { Items = null }
			);

			EnsureEqual(
				"Same length and content",
				cmp,
				new DictionaryModel { Items = new Dictionary<string, FooModel> { ["FOO"] = new FooModel { Id = 1, Name = "Foo" }, ["BAR"] = new FooModel { Id = 2, Name = "Bar" } } },
				new DictionaryModel { Items = new Dictionary<string, FooModel> { ["FOO"] = new FooModel { Id = 1, Name = "Foo" }, ["BAR"] = new FooModel { Id = 2, Name = "Bar" } } }
			);

			EnsureEqual(
				"Order does not matter",
				cmp,
				new DictionaryModel { Items = new Dictionary<string, FooModel> { ["FOO"] = new FooModel { Id = 1, Name = "Foo" }, ["BAR"] = new FooModel { Id = 2, Name = "Bar" } } },
				new DictionaryModel { Items = new Dictionary<string, FooModel> { ["BAR"] = new FooModel { Id = 2, Name = "Bar" }, ["FOO"] = new FooModel { Id = 1, Name = "Foo" } } }
			);

			EnsureDifferent(
				"Different length",
				cmp,
				new DictionaryModel { Items = new Dictionary<string, FooModel> { ["FOO"] = new FooModel { Id = 1, Name = "Foo" }, ["BAR"] = new FooModel { Id = 2, Name = "Bar" } } },
				new DictionaryModel { Items = new Dictionary<string, FooModel> { ["FOO"] = new FooModel { Id = 1, Name = "Foo" } } }
			);

			EnsureDifferent(
				"Same length, different content",
				cmp,
				new DictionaryModel { Items = new Dictionary<string, FooModel> { ["FOO"] = new FooModel { Id = 1, Name = "Foo" }, ["BAR"] = new FooModel { Id = 2, Name = "Bar" } } },
				new DictionaryModel { Items = new Dictionary<string, FooModel> { ["FOO"] = new FooModel { Id = 2, Name = "Bar" }, ["BAR"] = new FooModel { Id = 1, Name = "Foo" } } }
			);

			EnsureDifferent(
				"Case sensitive",
				cmp,
				new DictionaryModel { Items = new Dictionary<string, FooModel> { ["FOO"] = new FooModel { Id = 1, Name = "Foo" }, ["Bar"] = new FooModel { Id = 2, Name = "Bar" } } },
				new DictionaryModel { Items = new Dictionary<string, FooModel> { ["Foo"] = new FooModel { Id = 1, Name = "Foo" }, ["BAR"] = new FooModel { Id = 2, Name = "Bar" } } }
			);

			EnsureEqual(
				"Use Dictionary's own Key Comparer",
				cmp,
				new DictionaryModel { Items = new Dictionary<string, FooModel>(StringComparer.OrdinalIgnoreCase) { ["FOO"] = new FooModel { Id = 1, Name = "Foo" }, ["Bar"] = new FooModel { Id = 2, Name = "Bar" } } },
				new DictionaryModel { Items = new Dictionary<string, FooModel>(StringComparer.OrdinalIgnoreCase) { ["Foo"] = new FooModel { Id = 1, Name = "Foo" }, ["BAR"] = new FooModel { Id = 2, Name = "Bar" } } }
			);

		}

		[Test]
		public void Test_Compare_HashSet_Properties()
		{
			var cmp = ModelComparer.Comparer<HashSetModel>.Default;

			EnsureEqual(
				"null == null",
				cmp,
				new HashSetModel(),
				new HashSetModel()
			);

			EnsureEqual(
				"[0] == [0]",
				cmp,
				new HashSetModel { Items = new HashSet<string>() },
				new HashSetModel { Items = new HashSet<string>() }
			);

			EnsureDifferent(
				"[0] == null",
				cmp,
				new HashSetModel { Items = new HashSet<string>() },
				new HashSetModel { Items = null }
			);

			EnsureEqual(
				"Same length and content",
				cmp,
				new HashSetModel { Items = new HashSet<string> { "Hello", "World" } },
				new HashSetModel { Items = new HashSet<string> { "Hello", "World" } }
			);

			EnsureEqual(
				"Order doesn't matter",
				cmp,
				new HashSetModel { Items = new HashSet<string> { "Hello", "World" } },
				new HashSetModel { Items = new HashSet<string> { "World", "Hello" } }
			);

			EnsureDifferent(
				"Different length",
				cmp,
				new HashSetModel { Items = new HashSet<string> { "Hello", "World" } },
				new HashSetModel { Items = new HashSet<string> { "Hello", "World", "!"} }
			);

			EnsureDifferent(
				"Same length, different content",
				cmp,
				new HashSetModel { Items = new HashSet<string> { "Hello", "World" } },
				new HashSetModel { Items = new HashSet<string> { "Hello", "Monde" } }
			);

			EnsureDifferent(
				"Case sensitive",
				cmp,
				new HashSetModel { Items = new HashSet<string> { "Hello", "World" } },
				new HashSetModel { Items = new HashSet<string> { "hello", "WORLD" } }
			);

			EnsureEqual(
				"Use HashSet's own Comparer",
				cmp,
				new HashSetModel { Items = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Hello", "World" } },
				new HashSetModel { Items = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "hello", "WORLD" } }
			);

		}


		[Test]
		public void Test_HashCode_Sealed_Model()
		{
			var comparer = ModelComparer.Comparer<SimpleEntity>.Default;
			Assert.That(comparer, Is.Not.Null);

			// le comparer est "null safe", et retourne toujours 0 dans ce cas
			// ReSharper disable once AssignNullToNotNullAttribute
			var h0 = comparer.GetHashCode(null);
			Log($"h0  = {h0:X}");

			var id = Guid.Parse("2ff308b5-2628-4c49-8864-baa45db3f6c0");
			var a = new SimpleEntity
			{
				Id = id,
				Name = "Foo",
				Description = "Bar", // not covered by hash function
				Version = 123,
			};

			int ha = comparer.GetHashCode(a);
			Log($"ha  = {ha:X}");
			Assert.That(ha, Is.Not.Zero);

			var a2 = new SimpleEntity
			{
				Id = id,
				Name = "Foo",
				Description = "Baz", // not covered by hash function
				Version = 123,
			};

			int ha2 = comparer.GetHashCode(a2);
			Log($"ha2 = {ha2:X}");
			Assert.That(ha2, Is.EqualTo(ha), "Hash should be the same (only Description changes by should not be used)");

			var b = new SimpleEntity
			{
				Id = id,
				Name = "Bar",
				Description = "Bar", // not covered by hash function
				Version = 123,
			};
			int hb = comparer.GetHashCode(b);
			Log($"hb  = {hb:X}");
			Assert.That(hb, Is.Not.EqualTo(ha), "Hashcode should be different (Name is different)");

			var c = new SimpleEntity
			{
				Id = Guid.Parse("74757289-0e13-44cd-9833-2c180d2610cc"),
				Name = "Foo",
				Description = "Bar", // not covered by hash function
				Version = 123,
			};
			int hc = comparer.GetHashCode(c);
			Log($"hc  = {hc:X}");
			Assert.That(hc, Is.Not.EqualTo(ha), "Hashcode should be different (Id is different)");
			Assert.That(hc, Is.Not.EqualTo(hb), "Hashcode should be different (Id & Name are different)");
		}

	}

	public sealed class SimpleEntity
	{
		[Primary]
		public Guid Id { get; set; }

		[Primary]
		public string Name { get; set; }

		public string Description { get; set; }

		[Primary]
		public long Version { get; set; }

		public DateTime Created { get; set; }

		public DateTime? Modified { get; set; }

		public bool IsDeleted { get; set; }
	}

	public sealed class ArrayModel
	{
		public string[] Items { get; set; }

	}

	public sealed class ListModel
	{
		public List<string> Items { get; set; }
	}

	public sealed class FooModel
	{
		public int Id { get; set; }

		public string Name { get; set; }

	}

	public sealed class DictionaryModel
	{
		public Dictionary<string, FooModel> Items { get; set; }
	}

	public sealed class HashSetModel
	{
		public HashSet<string> Items { get; set; }
	}

	/// <summary>Exemple d'une classe Model qui est sealed, et contient quelques Nested Types (également sealed)</summary>
	public sealed class SaMereModel
	{
		public bool Bool { get; set; }

		public string String { get; set; }

		public int Int32 { get; set; }

		public double Double { get; set; }

		public DateTime DateTime { get; set; }

		public Guid Guid { get; set; }

		public int? MaybeInt32 { get; set; }

		public long? MaybeInt64 { get; set; }

		public DateTime? MaybeDateTime { get; set; }

		public Guid? MaybeGuid { get; set; }

		/// <summary>Instance d'un Nested Type</summary>
		public NestedSealedClass SomeClass { get; set; } = new NestedSealedClass();

		public NestedStruct SomeStruct;

		public sealed class NestedSealedClass
		{
			public string Hello { get; set; }

			public bool Foo { get; set; }
		}

		public struct NestedStruct
		{
			public string Hello;
			public string Foo;
		}

	}

	public abstract class SaMereBase
	{
		public string Foo { get; set; }
	}

	public sealed class SaMereEnShort : SaMereBase
	{
		public string Short { get; set; }
	}

	public sealed class SaMereEnBikini : SaMereBase
	{
		public string Bikini { get; set; }
	}

}
