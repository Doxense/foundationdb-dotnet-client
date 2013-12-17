#region Copyright (c) 2013-2014, Doxense SAS. All rights reserved.
// See License.MD for license information
#endregion

namespace FoundationDB.Storage.Memory.API.Tests
{
	using FoundationDB.Client;
	using FoundationDB.Layers.Directories;
	using FoundationDB.Layers.Indexing;
	using FoundationDB.Layers.Tables;
	using NUnit.Framework;
	using System;
	using System.Diagnostics;
	using System.Linq;
	using System.Threading;
	using System.Threading.Tasks;

	[TestFixture]
	public class MemoryTransactionFacts
	{

		[Test]
		public async Task Test_Hello_World()
		{
			using (var db = MemoryDatabase.CreateNew("DB", FdbSubspace.Empty, false))
			{
				var key = db.Pack("hello");

				// v1
				await db.WriteAsync((tr) => tr.Set(key, Slice.FromString("World!")));
				db.Debug_Dump();
				var data = await db.ReadAsync((tr) => tr.GetAsync(key));
				Assert.That(data.ToUnicode(), Is.EqualTo("World!"));

				// v2
				await db.WriteAsync((tr) => tr.Set(key, Slice.FromString("Le Monde!")));
				db.Debug_Dump();
				data = await db.ReadAsync((tr) => tr.GetAsync(key));
				Assert.That(data.ToUnicode(), Is.EqualTo("Le Monde!"));

				using (var tr1 = db.BeginTransaction())
				{
					await tr1.GetReadVersionAsync();

					await db.WriteAsync((tr2) => tr2.Set(key, Slice.FromString("Sekai!")));
					db.Debug_Dump();

					data = await tr1.GetAsync(key);
					Assert.That(data.ToUnicode(), Is.EqualTo("Le Monde!"));
				}

				data = await db.ReadAsync((tr) => tr.GetAsync(key));
				Assert.That(data.ToUnicode(), Is.EqualTo("Sekai!"));

				// Collect memory
				Trace.WriteLine("### GARBAGE COLLECT! ###");
				db.Collect();
				db.Debug_Dump();

			}
		}

		[Test]
		public async Task Test_GetKey()
		{
			Slice key;
			Slice value;

			using (var db = MemoryDatabase.CreateNew("DB"))
			{

				using (var tr = db.BeginTransaction())
				{
					tr.Set(db.Pack(0), Slice.FromString("first"));
					tr.Set(db.Pack(10), Slice.FromString("ten"));
					tr.Set(db.Pack(20), Slice.FromString("ten ten"));
					tr.Set(db.Pack(42), Slice.FromString("narf!"));
					tr.Set(db.Pack(100), Slice.FromString("a hundred missipis"));
					await tr.CommitAsync();
				}

				db.Debug_Dump();

				using (var tr = db.BeginTransaction())
				{

					value = await tr.GetAsync(db.Pack(42));
					Console.WriteLine(value);
					Assert.That(value.ToString(), Is.EqualTo("narf!"));

					key = await tr.GetKeyAsync(FdbKeySelector.FirstGreaterOrEqual(db.Pack(42)));
					Assert.That(key, Is.EqualTo(db.Pack(42)));

					key = await tr.GetKeyAsync(FdbKeySelector.FirstGreaterThan(db.Pack(42)));
					Assert.That(key, Is.EqualTo(db.Pack(100)));

					key = await tr.GetKeyAsync(FdbKeySelector.LastLessOrEqual(db.Pack(42)));
					Assert.That(key, Is.EqualTo(db.Pack(42)));

					key = await tr.GetKeyAsync(FdbKeySelector.LastLessThan(db.Pack(42)));
					Assert.That(key, Is.EqualTo(db.Pack(20)));

					var keys = await tr.GetKeysAsync(new[]
					{
						FdbKeySelector.FirstGreaterOrEqual(db.Pack(42)),
						FdbKeySelector.FirstGreaterThan(db.Pack(42)),
						FdbKeySelector.LastLessOrEqual(db.Pack(42)),
						FdbKeySelector.LastLessThan(db.Pack(42))
					});

					Assert.That(keys.Length, Is.EqualTo(4));
					Assert.That(keys[0], Is.EqualTo(db.Pack(42)));
					Assert.That(keys[1], Is.EqualTo(db.Pack(100)));
					Assert.That(keys[2], Is.EqualTo(db.Pack(42)));
					Assert.That(keys[3], Is.EqualTo(db.Pack(20)));

					await tr.CommitAsync();
				}

			}

		}

		[Test]
		public async Task Test_GetKey_ReadConflicts()
		{
			Slice key;

			using (var db = MemoryDatabase.CreateNew("FOO"))
			{
				using(var tr = db.BeginTransaction())
				{
					tr.Set(db.Pack(42), Slice.FromString("42"));
					tr.Set(db.Pack(50), Slice.FromString("50"));
					tr.Set(db.Pack(60), Slice.FromString("60"));
					await tr.CommitAsync();
				}
				db.Debug_Dump();

				Func<FdbKeySelector, Slice, Task> check = async (selector, expected) =>
				{
					using (var tr = db.BeginTransaction())
					{
						key = await tr.GetKeyAsync(selector);
						await tr.CommitAsync();
						Assert.That(key, Is.EqualTo(expected), selector.ToString() + " => " + FdbKey.Dump(expected));
					}
				};

				await check(
					FdbKeySelector.FirstGreaterOrEqual(db.Pack(50)), 
					db.Pack(50)
				);
				await check(
					FdbKeySelector.FirstGreaterThan(db.Pack(50)),
					db.Pack(60)
				);

				await check(
					FdbKeySelector.FirstGreaterOrEqual(db.Pack(49)),
					db.Pack(50)
				);
				await check(
					FdbKeySelector.FirstGreaterThan(db.Pack(49)),
					db.Pack(50)
				);

				await check(
					FdbKeySelector.FirstGreaterOrEqual(db.Pack(49)) + 1,
					db.Pack(60)
				);
				await check(
					FdbKeySelector.FirstGreaterThan(db.Pack(49)) + 1,
					db.Pack(60)
				);

				await check(
					FdbKeySelector.LastLessOrEqual(db.Pack(49)),
					db.Pack(42)
				);
				await check(
					FdbKeySelector.LastLessThan(db.Pack(49)),
					db.Pack(42)
				);
			}
		}

		[Test]
		public async Task Test_GetRangeAsync()
		{
			Slice key;

			using (var db = MemoryDatabase.CreateNew("DB"))
			{

				using (var tr = db.BeginTransaction())
				{
					for (int i = 0; i <= 100; i++)
					{
						tr.Set(db.Pack(i), Slice.FromString("value of " + i));
					}
					await tr.CommitAsync();
				}

				db.Debug_Dump();

				// verify that key selectors work find
				using (var tr = db.BeginTransaction())
				{
					key = await tr.GetKeyAsync(FdbKeySelector.FirstGreaterOrEqual(FdbKey.MaxValue));
					if (key != FdbKey.MaxValue) Assert.Inconclusive("Key selectors are buggy: fGE(max)");
					key = await tr.GetKeyAsync(FdbKeySelector.LastLessOrEqual(FdbKey.MaxValue));
					if (key != FdbKey.MaxValue) Assert.Inconclusive("Key selectors are buggy: lLE(max)");
					key = await tr.GetKeyAsync(FdbKeySelector.LastLessThan(FdbKey.MaxValue));
					if (key != db.Pack(100)) Assert.Inconclusive("Key selectors are buggy: lLT(max)");
				}

				using (var tr = db.BeginTransaction())
				{

					var chunk = await tr.GetRangeAsync(
						FdbKeySelector.FirstGreaterOrEqual(db.Pack(0)),
						FdbKeySelector.FirstGreaterOrEqual(db.Pack(50))
					);
#if DEBUG
					for (int i = 0; i < chunk.Count; i++)
					{
						Console.WriteLine(i.ToString() + " : " + chunk.Chunk[i].Key + " = " + chunk.Chunk[i].Value);
					}
#endif

					Assert.That(chunk.Count, Is.EqualTo(50), "chunk.Count");
					Assert.That(chunk.HasMore, Is.False, "chunk.HasMore");
					Assert.That(chunk.Reversed, Is.False, "chunk.Reversed");
					Assert.That(chunk.Iteration, Is.EqualTo(1), "chunk.Iteration");

					for (int i = 0; i < 50; i++)
					{
						Assert.That(chunk.Chunk[i].Key, Is.EqualTo(db.Pack(i)), "[{0}].Key", i);
						Assert.That(chunk.Chunk[i].Value.ToString(), Is.EqualTo("value of " + i), "[{0}].Value", i);
					}

					await tr.CommitAsync();
				}

				using (var tr = db.BeginTransaction())
				{

					var chunk = await tr.GetRangeAsync(
						FdbKeySelector.FirstGreaterOrEqual(db.Pack(0)),
						FdbKeySelector.FirstGreaterOrEqual(db.Pack(50)),
						new FdbRangeOptions { Reverse = true }
					);
#if DEBUG
					for (int i = 0; i < chunk.Count; i++)
					{
						Console.WriteLine(i.ToString() + " : " + chunk.Chunk[i].Key + " = " + chunk.Chunk[i].Value);
					}
#endif

					Assert.That(chunk.Count, Is.EqualTo(50), "chunk.Count");
					Assert.That(chunk.HasMore, Is.False, "chunk.HasMore");
					Assert.That(chunk.Reversed, Is.True, "chunk.Reversed");
					Assert.That(chunk.Iteration, Is.EqualTo(1), "chunk.Iteration");

					for (int i = 0; i < 50; i++)
					{
						Assert.That(chunk.Chunk[i].Key, Is.EqualTo(db.Pack(49 - i)), "[{0}].Key", i);
						Assert.That(chunk.Chunk[i].Value.ToString(), Is.EqualTo("value of " + (49 - i)), "[{0}].Value", i);
					}

					await tr.CommitAsync();
				}

				using (var tr = db.BeginTransaction())
				{

					var chunk = await tr.GetRangeAsync(
						FdbKeySelector.FirstGreaterOrEqual(db.Pack(0)),
						FdbKeySelector.FirstGreaterOrEqual(FdbKey.MaxValue),
						new FdbRangeOptions { Reverse = true, Limit = 1 }
					);
#if DEBUG
					for (int i = 0; i < chunk.Count; i++)
					{
						Console.WriteLine(i.ToString() + " : " + chunk.Chunk[i].Key + " = " + chunk.Chunk[i].Value);
					}
#endif

					Assert.That(chunk.Count, Is.EqualTo(1), "chunk.Count");
					Assert.That(chunk.HasMore, Is.True, "chunk.HasMore");
					Assert.That(chunk.Reversed, Is.True, "chunk.Reversed");
					Assert.That(chunk.Iteration, Is.EqualTo(1), "chunk.Iteration");

					await tr.CommitAsync();
				}

			}

		}

		[Test]
		public async Task Test_GetRange()
		{

			using (var db = MemoryDatabase.CreateNew("DB"))
			{

				using (var tr = db.BeginTransaction())
				{
					for (int i = 0; i <= 100; i++)
					{
						tr.Set(db.Pack(i), Slice.FromString("value of " + i));
					}
					await tr.CommitAsync();
				}

				db.Debug_Dump();

				using (var tr = db.BeginTransaction())
				{

					var results = await tr
						.GetRange(db.Pack(0), db.Pack(50))
						.ToListAsync();

					Assert.That(results, Is.Not.Null);
#if DEBUG
					for (int i = 0; i < results.Count; i++)
					{
						Console.WriteLine(i.ToString() + " : " + results[i].Key + " = " + results[i].Value);
					}
#endif

					Assert.That(results.Count, Is.EqualTo(50));
					for (int i = 0; i < 50; i++)
					{
						Assert.That(results[i].Key, Is.EqualTo(db.Pack(i)), "[{0}].Key", i);
						Assert.That(results[i].Value.ToString(), Is.EqualTo("value of " + i), "[{0}].Value", i);
					}

					await tr.CommitAsync();
				}

				using (var tr = db.BeginTransaction())
				{

					var results = await tr
						.GetRange(db.Pack(0), db.Pack(50), new FdbRangeOptions { Reverse = true })
						.ToListAsync();
					Assert.That(results, Is.Not.Null);
#if DEBUG
					for (int i = 0; i < results.Count; i++)
					{
						Console.WriteLine(i.ToString() + " : " + results[i].Key + " = " + results[i].Value);
					}
#endif

					Assert.That(results.Count, Is.EqualTo(50));
					for (int i = 0; i < 50; i++)
					{
						Assert.That(results[i].Key, Is.EqualTo(db.Pack(49 - i)), "[{0}].Key", i);
						Assert.That(results[i].Value.ToString(), Is.EqualTo("value of " + (49 - i)), "[{0}].Value", i);
					}

					await tr.CommitAsync();
				}

				using (var tr = db.BeginTransaction())
				{
					var result = await tr
						.GetRange(db.Pack(0), FdbKey.MaxValue, new FdbRangeOptions { Reverse = true })
						.FirstOrDefaultAsync();

					Console.WriteLine(result.Key + " = " + result.Value);
					Assert.That(result.Key, Is.EqualTo(db.Pack(100)));
					Assert.That(result.Value.ToString(), Is.EqualTo("value of 100"));

					await tr.CommitAsync();
				}

			}

		}

		[Test]
		public async Task Test_Conflicts()
		{

			// this SHOULD NOT conflict
			using (var db = MemoryDatabase.CreateNew("DB"))
			{

				using (var tr1 = db.BeginTransaction())
				{
					using (var tr2 = db.BeginTransaction())
					{
						tr2.Set(db.Pack("foo"), Slice.FromString("changed"));
						await tr2.CommitAsync();
					}

					var x = await tr1.GetAsync(db.Pack("foo"));
					tr1.Set(db.Pack("bar"), Slice.FromString("other"));

					await tr1.CommitAsync();
				}

			}

			// this SHOULD conflict
			using (var db = MemoryDatabase.CreateNew("DB"))
			{

				using (var tr1 = db.BeginTransaction())
				{
					var x = await tr1.GetAsync(db.Pack("foo"));

					using (var tr2 = db.BeginTransaction())
					{
						tr2.Set(db.Pack("foo"), Slice.FromString("changed"));
						await tr2.CommitAsync();
					}

					tr1.Set(db.Pack("bar"), Slice.FromString("other"));

					Assert.That(async () => await tr1.CommitAsync(), Throws.InstanceOf<FdbException>().With.Property("Code").EqualTo(FdbError.NotCommitted));
				}

			}

			// this SHOULD conflict
			using (var db = MemoryDatabase.CreateNew("DB"))
			{

				using (var tr1 = db.BeginTransaction())
				{
					await tr1.GetReadVersionAsync();

					using (var tr2 = db.BeginTransaction())
					{
						tr2.Set(db.Pack("foo"), Slice.FromString("changed"));
						await tr2.CommitAsync();
					}

					var x = await tr1.GetAsync(db.Pack("foo"));
					tr1.Set(db.Pack("bar"), Slice.FromString("other"));

					Assert.That(async () => await tr1.CommitAsync(), Throws.InstanceOf<FdbException>().With.Property("Code").EqualTo(FdbError.NotCommitted));
				}

			}

			// this SHOULD NOT conflict
			using (var db = MemoryDatabase.CreateNew("DB"))
			{

				using (var tr1 = db.BeginTransaction())
				{
					var x = await tr1.Snapshot.GetAsync(db.Pack("foo"));

					using (var tr2 = db.BeginTransaction())
					{
						tr2.Set(db.Pack("foo"), Slice.FromString("changed"));
						await tr2.CommitAsync();
					}

					tr1.Set(db.Pack("bar"), Slice.FromString("other"));

					await tr1.CommitAsync();
				}

			}


		}

		private async Task Scenario1(IFdbTransaction tr)
		{

			tr.Set(Slice.FromAscii("hello"), Slice.FromAscii("world!"));
			tr.Clear(Slice.FromAscii("removed"));
			var result = await tr.GetAsync(Slice.FromAscii("narf"));
		}

		private async Task Scenario2(IFdbTransaction tr)
		{
			var location = FdbSubspace.Create(Slice.FromAscii("TEST"));
			tr.ClearRange(FdbKeyRange.StartsWith(location.Key));
			for (int i = 0; i < 10; i++)
			{
				tr.Set(location.Pack(i), Slice.FromString("value of " + i));
			}
		}

		private async Task Scenario3(IFdbTransaction tr)
		{
			var location = FdbSubspace.Create(Slice.FromAscii("TEST"));

			tr.Set(location.Key + (byte)'a', Slice.FromAscii("A"));
			tr.AtomicAdd(location.Key + (byte)'k', Slice.FromFixed32(1));
			tr.Set(location.Key + (byte)'z', Slice.FromAscii("C"));
			tr.ClearRange(location.Key + (byte)'a', location.Key + (byte)'k');
			tr.ClearRange(location.Key + (byte)'k', location.Key + (byte)'z');
		}

		private async Task Scenario4(IFdbTransaction tr)
		{
			var location = FdbSubspace.Create(Slice.FromAscii("TEST"));

			//tr.Set(location.Key, Slice.FromString("NARF"));
			//tr.AtomicAdd(location.Key, Slice.FromFixedU32(1));
			tr.AtomicAnd(location.Key, Slice.FromFixedU32(7));
			tr.AtomicXor(location.Key, Slice.FromFixedU32(3));
			tr.AtomicXor(location.Key, Slice.FromFixedU32(15));
		}

		private async Task Scenario5(IFdbTransaction tr)
		{
			var location = FdbSubspace.Create(Slice.FromAscii("TEST"));

			//tr.Set(location.Pack(42), Slice.FromString("42"));
			//tr.Set(location.Pack(50), Slice.FromString("50"));
			//tr.Set(location.Pack(60), Slice.FromString("60"));

			var x = await tr.GetKeyAsync(FdbKeySelector.LastLessThan(location.Pack(49)));
			Console.WriteLine(x);

			tr.Set(location.Pack("FOO"), Slice.FromString("BAR"));

		}

		[Test]
		public async Task Test_Compare_Implementations()
		{
			int mode = 5;

			using (var db = await Fdb.OpenAsync(@"c:\temp\fdb\fdb1.cluster", "DB"))
			{
				using (var tr = db.BeginTransaction())
				{
					await tr.GetReadVersionAsync();

					switch (mode)
					{
						case 1: await Scenario1(tr); break;
						case 2: await Scenario2(tr); break;
						case 3: await Scenario3(tr); break;
						case 4: await Scenario4(tr); break;
						case 5: await Scenario5(tr); break;
					}

					await tr.CommitAsync();
				}
			}

			using (var db = MemoryDatabase.CreateNew("DB"))
			{
				using (var tr = db.BeginTransaction(FdbTransactionMode.Default))
				{
					await tr.GetReadVersionAsync();

					switch (mode)
					{
						case 1: await Scenario1(tr); break;
						case 2: await Scenario2(tr); break;
						case 3: await Scenario3(tr); break;
						case 4: await Scenario4(tr); break;
						case 5: await Scenario5(tr); break;
					}

					await tr.CommitAsync();
				}

				db.Debug_Dump();
			}
		}

		[Test]
		public async Task Test_Write_Then_Read()
		{
			using (var db = MemoryDatabase.CreateNew("FOO"))
			{
				using(var tr = db.BeginTransaction())
				{
					tr.Set(Slice.FromString("hello"), Slice.FromString("World!"));
					tr.AtomicAdd(Slice.FromString("counter"), Slice.FromFixed32(1));
					tr.Set(Slice.FromString("foo"), Slice.FromString("bar"));
					await tr.CommitAsync();
				}

				db.Debug_Dump();

				using (var tr = db.BeginTransaction())
				{
					var result = await tr.GetAsync(Slice.FromString("hello"));
					Assert.That(result, Is.Not.Null);
					Assert.That(result.ToString(), Is.EqualTo("World!"));

					result = await tr.GetAsync(Slice.FromString("counter"));
					Assert.That(result, Is.Not.Null);
					Assert.That(result.ToInt32(), Is.EqualTo(1));

					result = await tr.GetAsync(Slice.FromString("foo"));
					Assert.That(result.ToString(), Is.EqualTo("bar"));

				}

				using (var tr = db.BeginTransaction())
				{
					tr.Set(Slice.FromString("hello"), Slice.FromString("Le Monde!"));
					tr.AtomicAdd(Slice.FromString("counter"), Slice.FromFixed32(1));
					tr.Set(Slice.FromString("narf"), Slice.FromString("zort"));
					await tr.CommitAsync();
				}

				db.Debug_Dump();

				using (var tr = db.BeginTransaction())
				{
					var result = await tr.GetAsync(Slice.FromString("hello"));
					Assert.That(result, Is.Not.Null);
					Assert.That(result.ToString(), Is.EqualTo("Le Monde!"));

					result = await tr.GetAsync(Slice.FromString("counter"));
					Assert.That(result, Is.Not.Null);
					Assert.That(result.ToInt32(), Is.EqualTo(2));

					result = await tr.GetAsync(Slice.FromString("foo"));
					Assert.That(result, Is.Not.Null);
					Assert.That(result.ToString(), Is.EqualTo("bar"));

					result = await tr.GetAsync(Slice.FromString("narf"));
					Assert.That(result, Is.Not.Null);
					Assert.That(result.ToString(), Is.EqualTo("zort"));
				}

				// Collect memory
				Trace.WriteLine("### GARBAGE COLLECT! ###");
				db.Collect();
				db.Debug_Dump();
			}
		}

		[Test]
		public async Task Test_Atomic()
		{
			using (var db = MemoryDatabase.CreateNew("DB"))
			{
				var key1 = db.Pack(1);
				var key2 = db.Pack(2);
				var key16 = db.Pack(16);

				for (int i = 0; i < 10; i++)
				{
					using (var tr = db.BeginTransaction())
					{
						tr.AtomicAdd(key1, Slice.FromFixed64(1));
						tr.AtomicAdd(key2, Slice.FromFixed64(2));
						tr.AtomicAdd(key16, Slice.FromFixed64(16));

						await tr.CommitAsync();
					}
				}

				db.Debug_Dump();

				// Collect memory
				Trace.WriteLine("### GARBAGE COLLECT! ###");
				db.Collect();
				db.Debug_Dump();
			}
		}

		[Test]
		public async Task Test_Use_Simple_Layer()
		{
			using (var db = MemoryDatabase.CreateNew("FOO"))
			{

				var table = new FdbTable<int, string>("Foos", db.GlobalSpace.Partition("Foos"), KeyValueEncoders.Values.StringEncoder);
				var index = new FdbIndex<int, string>("Foos.ByColor", db.GlobalSpace.Partition("Foos", "Color"));

				using(var tr = db.BeginTransaction())
				{
					table.Set(tr, 3, @"{ ""name"": ""Juliet"", ""color"": ""red"" }");
					table.Set(tr, 2, @"{ ""name"": ""Joey"", ""color"": ""blue"" }");
					table.Set(tr, 1, @"{ ""name"": ""Bob"", ""color"": ""red"" }");

					index.Add(tr, 3, "red");
					index.Add(tr, 2, "blue");
					index.Add(tr, 1, "red");

					await tr.CommitAsync();
				}

				db.Debug_Dump(true);

				// Collect memory
				Trace.WriteLine("### GARBAGE COLLECT! ###");
				db.Collect();
				db.Debug_Dump();
			}
		}

		[Test]
		public async Task Test_Use_Directory_Layer()
		{
			using (var db = MemoryDatabase.CreateNew("DB"))
			{

				var dl = new FdbDirectoryLayer();

				using (var tr = db.BeginTransaction())
				{
					var foos = await dl.CreateOrOpenAsync(tr, new[] { "Foos" });
					var bars = await dl.CreateOrOpenAsync(tr, new[] { "Bars" });

					var foo123 = await dl.CreateOrOpenAsync(tr, new[] { "Foos", "123" });
					var bar456 = await bars.CreateOrOpenAsync(tr, new[] { "123" });

					await tr.CommitAsync();
				}

				db.Debug_Dump();

				// Collect memory
				Trace.WriteLine("### GARBAGE COLLECT! ###");
				db.Collect();
				db.Debug_Dump();
			}
		}

		[Test]
		public async Task Test_Can_Resolve_Key_Selector_Outside_Boundaries()
		{
			// test various corner cases:

			// - k < first_key or k <= <00> resolves to:
			//   - '' always

			// - k > last_key or k >= <FF> resolve to:
			//	 - '<FF>' when access to system keys is off
			//   - '<FF>/backupRange' (usually) when access to system keys is ON

			// - k >= <FF><00> resolves to:
			//   - key_outside_legal_range when access to system keys is off
			//   - '<FF>/backupRange' (usually) when access to system keys is ON

			// - k >= <FF><FF> resolved to:
			//   - key_outside_legal_range when access to system keys is off
			//   - '<FF><FF>' when access to system keys is ON

			Slice key;

			using (var db = MemoryDatabase.CreateNew("FOO"))
			{

				using(var tr = db.BeginTransaction())
				{
					tr.Set(Slice.FromString("A"), Slice.FromString("min"));
					tr.Set(Slice.FromString("Z"), Slice.FromString("max"));
					await tr.CommitAsync();
				}

				using (var tr = db.BeginReadOnlyTransaction())
				{
					// before <00>
					key = await tr.GetKeyAsync(FdbKeySelector.LastLessThan(FdbKey.MinValue));
					Assert.That(key, Is.EqualTo(Slice.Empty), "lLT(<00>) => ''");

					// before the first key in the db
					var minKey = await tr.GetKeyAsync(FdbKeySelector.FirstGreaterOrEqual(FdbKey.MinValue));
					Assert.That(minKey, Is.Not.Null);
					Console.WriteLine("minKey = " + minKey);
					key = await tr.GetKeyAsync(FdbKeySelector.LastLessThan(minKey));
					Assert.That(key, Is.EqualTo(Slice.Empty), "lLT(min_key) => ''");

					// after the last key in the db

					var maxKey = await tr.GetKeyAsync(FdbKeySelector.LastLessThan(FdbKey.MaxValue));
					Assert.That(maxKey, Is.Not.Null);
					Console.WriteLine("maxKey = " + maxKey);
					key = await tr.GetKeyAsync(FdbKeySelector.FirstGreaterThan(maxKey));
					Assert.That(key, Is.EqualTo(FdbKey.MaxValue), "fGT(maxKey) => <FF>");

					// after <FF>
					key = await tr.GetKeyAsync(FdbKeySelector.FirstGreaterThan(FdbKey.MaxValue));
					Assert.That(key, Is.EqualTo(FdbKey.MaxValue), "fGT(<FF>) => <FF>");
					Assert.That(async () => await tr.GetKeyAsync(FdbKeySelector.FirstGreaterThan(Slice.FromAscii("\xFF\xFF"))), Throws.InstanceOf<FdbException>().With.Property("Code").EqualTo(FdbError.KeyOutsideLegalRange));
					Assert.That(async () => await tr.GetKeyAsync(FdbKeySelector.LastLessThan(Slice.FromAscii("\xFF\x00"))), Throws.InstanceOf<FdbException>().With.Property("Code").EqualTo(FdbError.KeyOutsideLegalRange));

					tr.WithAccessToSystemKeys();

					var firstSystemKey = await tr.GetKeyAsync(FdbKeySelector.FirstGreaterThan(FdbKey.MaxValue));
					// usually the first key in the system space is <FF>/backupDataFormat, but that may change in the future version.
					Assert.That(firstSystemKey, Is.Not.Null);
					Assert.That(firstSystemKey, Is.GreaterThan(FdbKey.MaxValue), "key should be between <FF> and <FF><FF>");
					Assert.That(firstSystemKey, Is.LessThan(Slice.FromAscii("\xFF\xFF")), "key should be between <FF> and <FF><FF>");

					// with access to system keys, the maximum possible key becomes <FF><FF>
					key = await tr.GetKeyAsync(FdbKeySelector.FirstGreaterOrEqual(Slice.FromAscii("\xFF\xFF")));
					Assert.That(key, Is.EqualTo(Slice.FromAscii("\xFF\xFF")), "fGE(<FF><FF>) => <FF><FF> (with access to system keys)");
					key = await tr.GetKeyAsync(FdbKeySelector.FirstGreaterThan(Slice.FromAscii("\xFF\xFF")));
					Assert.That(key, Is.EqualTo(Slice.FromAscii("\xFF\xFF")), "fGT(<FF><FF>) => <FF><FF> (with access to system keys)");

					key = await tr.GetKeyAsync(FdbKeySelector.LastLessThan(Slice.FromAscii("\xFF\x00")));
					Assert.That(key, Is.EqualTo(maxKey), "lLT(<FF><00>) => max_key (with access to system keys)");
					key = await tr.GetKeyAsync(FdbKeySelector.FirstGreaterThan(maxKey));
					Assert.That(key, Is.EqualTo(firstSystemKey), "fGT(max_key) => first_system_key (with access to system keys)");

				}
			}

		}

		[Test]
		public async Task Test_MiniBench()
		{
			const int M = 10 * 1000 * 1000;
			const int B = 100;
			const int W = 1;

			const int T = M / (B * W);
			const int KEYSIZE = 10;
			const int VALUESIZE = 100;
			const bool RANDOM = false;

			var rnd = new Random();

			//WARMUP
			using (var db = MemoryDatabase.CreateNew("FOO"))
			{
				await db.WriteAsync((tr) => tr.Set(Slice.FromString("hello"), Slice.FromString("world")));
				Slice.Random(rnd, KEYSIZE);
				Slice.Random(rnd, VALUESIZE);
			}

			Console.WriteLine("Inserting " + KEYSIZE + "-bytes " + (RANDOM ? "random" : "ordered") + " keys / " + VALUESIZE + "-bytes values, in " + T.ToString("N0") + " transactions");

			bool random = RANDOM;
			string fmt = "D" + KEYSIZE;
			using (var db = MemoryDatabase.CreateNew("FOO"))
			{
				GC.Collect();
				GC.WaitForPendingFinalizers();
				GC.Collect();
				Console.WriteLine("Total memory: Managed=" + GC.GetTotalMemory(false).ToString("N0") + ", WorkingSet=" + Environment.WorkingSet.ToString("N0"));

				long total = 0;

				var payload = new byte[1000 + VALUESIZE];
				rnd.NextBytes(payload);

				Task[] tasks = new Task[W];
				for (int w = 0; w < W; w++)
				{
					int wkid = w;
					tasks[w] = Task.Run(async () =>
					{
						await Task.Delay(10).ConfigureAwait(false);

						int offset = wkid * T * B;
						for (int i = 0; i < T; i++)
						{
							using (var tr = db.BeginTransaction())
							{
								for (int j = 0; j < B; j++)
								{
									Slice key;
									if (random)
									{
										do
										{
											key = Slice.Random(rnd, KEYSIZE);
										}
										while (key[0] == 255);
									}
									else
									{
										int x = i * B + offset + j;
										//x = x % 1000;
										key = Slice.FromString(x.ToString(fmt));
									}

									tr.Set(key, Slice.Create(payload, rnd.Next(1000), VALUESIZE));
									//tr.Set(key, Slice.Random(rnd, VALUESIZE));
									//tr.Set(key, Slice.FromString("written at " + (i * B + offset + j)));
									Interlocked.Increment(ref total);
								}
								await tr.CommitAsync().ConfigureAwait(false);
							}
							if (i % 1000 == 0) Console.Write(".");// + (i * B).ToString("D10"));
						}
					});


				}
				var sw = Stopwatch.StartNew();
				Task.WaitAll(tasks);
				sw.Stop();
				Console.WriteLine("done");
				Console.WriteLine("* Inserted: " + total.ToString("N0") + " keys");
				Console.WriteLine("* Elapsed : " + sw.Elapsed.TotalSeconds.ToString("N3") + " sec");
				Console.WriteLine("* TPS: " + (T / sw.Elapsed.TotalSeconds).ToString("N0") + " transaction/sec");
				Console.WriteLine("* KPS: " + (total / sw.Elapsed.TotalSeconds).ToString("N0") + " key/sec");
				Console.WriteLine("* BPS: " + ((total * (KEYSIZE + VALUESIZE)) / sw.Elapsed.TotalSeconds).ToString("N0") + " byte/sec");

				GC.Collect();
				GC.WaitForPendingFinalizers();
				GC.Collect();
				Console.WriteLine("Total memory: " + GC.GetTotalMemory(false).ToString("N0") + ", " + Environment.WorkingSet.ToString("N0"));

				db.Debug_Dump(false);

				var data = await db.GetValuesAsync(Enumerable.Range(0, 1000).Select(i => Slice.FromString(i.ToString(fmt))));

				#region sequential reads

				sw.Restart();
				for (int i = 0; i < total; i++)
				{
					using (var tr = db.BeginReadOnlyTransaction())
					{
						await db.GetAsync(Slice.FromString(i.ToString(fmt)));
					}
				}
				sw.Stop();
				Console.WriteLine("SeqRead1  : " + total.ToString("N0") + " keys in " + sw.Elapsed.TotalSeconds.ToString("N3") + " sec => " + (total / sw.Elapsed.TotalSeconds).ToString("N0") + " kps");

				sw.Restart();
				for (int i = 0; i < total; i += 10)
				{
					using (var tr = db.BeginReadOnlyTransaction())
					{
						await db.GetValuesAsync(Enumerable.Range(i, 10).Select(x => Slice.FromString(x.ToString(fmt))));
					}

				}
				sw.Stop();
				Console.WriteLine("SeqRead10 : " + total.ToString("N0") + " keys in " + sw.Elapsed.TotalSeconds.ToString("N3") + " sec => " + (total / sw.Elapsed.TotalSeconds).ToString("N0") + " kps");

				sw.Restart();
				for (int i = 0; i < total; i += 100)
				{
					using (var tr = db.BeginReadOnlyTransaction())
					{
						await db.GetValuesAsync(Enumerable.Range(i, 100).Select(x => Slice.FromString(x.ToString(fmt))));
					}

				}
				sw.Stop();
				Console.WriteLine("SeqRead100: " + total.ToString("N0") + " keys in " + sw.Elapsed.TotalSeconds.ToString("N3") + " sec => " + (total / sw.Elapsed.TotalSeconds).ToString("N0") + " kps");

				sw.Restart();
				for (int i = 0; i < total; i += 1000)
				{
					using (var tr = db.BeginReadOnlyTransaction())
					{
						await db.GetValuesAsync(Enumerable.Range(i, 1000).Select(x => Slice.FromString(x.ToString(fmt))));
					}

				}
				sw.Stop();
				Console.WriteLine("SeqRead1k : " + total.ToString("N0") + " keys in " + sw.Elapsed.TotalSeconds.ToString("N3") + " sec => " + (total / sw.Elapsed.TotalSeconds).ToString("N0") + " kps");

				#endregion

				#region random reads

				sw.Restart();
				for (int i = 0; i < total; i++)
				{
					using (var tr = db.BeginReadOnlyTransaction())
					{
						int x = rnd.Next((int)total);
						await db.GetAsync(Slice.FromString(x.ToString(fmt)));
					}
				}
				sw.Stop();
				Console.WriteLine("RndRead1  : " + total.ToString("N0") + " keys in " + sw.Elapsed.TotalSeconds.ToString("N3") + " sec => " + (total / sw.Elapsed.TotalSeconds).ToString("N0") + " kps");

				sw.Restart();
				for (int i = 0; i < total; i += 10)
				{
					using (var tr = db.BeginReadOnlyTransaction())
					{
						await db.GetValuesAsync(Enumerable.Range(i, 10).Select(x => Slice.FromString(rnd.Next((int)total).ToString(fmt))));
					}

				}
				sw.Stop();
				Console.WriteLine("RndRead10 : " + total.ToString("N0") + " keys in " + sw.Elapsed.TotalSeconds.ToString("N3") + " sec => " + (total / sw.Elapsed.TotalSeconds).ToString("N0") + " kps");

				sw.Restart();
				for (int i = 0; i < total; i += 100)
				{
					using (var tr = db.BeginReadOnlyTransaction())
					{
						await db.GetValuesAsync(Enumerable.Range(i, 100).Select(x => Slice.FromString(rnd.Next((int)total).ToString(fmt))));
					}

				}
				sw.Stop();
				Console.WriteLine("RndRead100: " + total.ToString("N0") + " keys in " + sw.Elapsed.TotalSeconds.ToString("N3") + " sec => " + (total / sw.Elapsed.TotalSeconds).ToString("N0") + " kps");

				sw.Restart();
				for (int i = 0; i < total; i += 1000)
				{
					using (var tr = db.BeginReadOnlyTransaction())
					{
						await db.GetValuesAsync(Enumerable.Range(i, 1000).Select(x => Slice.FromString(rnd.Next((int)total).ToString(fmt))));
					}

				}
				sw.Stop();
				Console.WriteLine("RndRead1k : " + total.ToString("N0") + " keys in " + sw.Elapsed.TotalSeconds.ToString("N3") + " sec => " + (total / sw.Elapsed.TotalSeconds).ToString("N0") + " kps");

				#endregion

				#region Parallel Reads...

				var mre = new ManualResetEvent(false);

				int CPUS = Environment.ProcessorCount;
				long kkk = 0;
				tasks = Enumerable
					.Range(0, CPUS)
					.Select(k => Task.Run(async () =>
					{
						var rndz = new Random(k);
						//Console.WriteLine("T" + k + " waiting");
						mre.WaitOne();
						//Console.WriteLine("T" + k + " go");

						int keys = 0;
						for (int i = 0; i < total / CPUS; i += 10)
						{
							int pp = i;// rndz.Next((int)total - 10);
							using (var tr = db.BeginReadOnlyTransaction())
							{
								var res = await db.GetValuesAsync(Enumerable.Range(i, 10).Select(x => Slice.FromString((pp + x).ToString(fmt))));
								keys += res.Length;
							}
						}
						//Console.WriteLine("T" + k + " done");
						Interlocked.Add(ref kkk, keys);
						return keys;
					})).ToArray();

				sw.Restart();
				mre.Set();
				await Task.WhenAll(tasks);
				sw.Stop();
				Console.WriteLine("ParaSeqRead: " + kkk.ToString("N0") + " keys in " + sw.Elapsed.TotalSeconds.ToString("N3") + " sec => " + (kkk / sw.Elapsed.TotalSeconds).ToString("N0") + " kps");

				#endregion
			}

		}

	}
}
