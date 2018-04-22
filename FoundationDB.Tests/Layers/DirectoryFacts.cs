#region BSD Licence
/* Copyright (c) 2013, Doxense SARL
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

#undef ENABLE_LOGGING

namespace FoundationDB.Layers.Directories
{
	using System;
	using System.Collections.Generic;
	using System.Linq;
	using System.Threading.Tasks;
	using Doxense.Collections.Tuples;
	using FoundationDB.Client;
	using FoundationDB.Client.Tests;
	using FoundationDB.Filters.Logging;
	using NUnit.Framework;

	[TestFixture]
	public class DirectoryFacts : FdbTest
	{

		[Test]
		public async Task Test_Allocator()
		{
			//FoundationDB.Client.Utils.Logging.SetLevel(System.Diagnostics.SourceLevels.Verbose);

			using (var db = await OpenTestDatabaseAsync())
			{
				var location = db.Partition.ByKey(Slice.FromString("hca"));
				await db.ClearRangeAsync(location, this.Cancellation);

#if ENABLE_LOGGING
				var list = new List<FdbTransactionLog>();
				var logged = new FdbLoggedDatabase(db, false, false, (tr) => { list.Add(tr.Log); });
#else
				var logged = db;
#endif

				var hpa = new FdbHighContentionAllocator(location);

				long id;
				var ids = new HashSet<long>();

				// allocate a single new id
				using (var tr = logged.BeginTransaction(this.Cancellation))
				{
					id = await hpa.AllocateAsync(tr);
					await tr.CommitAsync();
				}
				ids.Add(id);

				await DumpSubspace(db, location);

				// allocate a batch of new ids
				for (int i = 0; i < 100; i++)
				{
					using (var tr = logged.BeginTransaction(this.Cancellation))
					{
						id = await hpa.AllocateAsync(tr);
						await tr.CommitAsync();
					}

					if (ids.Contains(id))
					{
						await DumpSubspace(db, location);
						Assert.Fail("Duplicate key allocated: {0} (#{1})", id, i);
					}
					ids.Add(id);
				}

				await DumpSubspace(db, location);

#if ENABLE_LOGGING
				foreach(var log in list)
				{
					Console.WriteLine(log.GetTimingsReport(true)); 
				}
#endif
			}
		}

		[Test]
		public async Task Test_CreateOrOpen_Simple()
		{
			using (var db = await OpenTestDatabaseAsync())
			{
				// we will put everything under a custom namespace
				var location = db.Partition.ByKey("DL");
				await db.ClearRangeAsync(location, this.Cancellation);

#if ENABLE_LOGGING
				var list = new List<FdbTransactionLog>();
				var logged = new FdbLoggedDatabase(db, false, false, (tr) => { list.Add(tr.Log); });
#else
				var logged = db;
#endif

				// put the nodes under (..,"DL",\xFE,) and the content under (..,"DL",)
				var directory = FdbDirectoryLayer.Create(location);

				Assert.That(directory.ContentSubspace, Is.Not.Null);
				Assert.That(directory.ContentSubspace.GetPrefix(), Is.EqualTo(location.GetPrefix()));
				Assert.That(directory.NodeSubspace, Is.Not.Null);
				Assert.That(directory.NodeSubspace.GetPrefix(), Is.EqualTo(location.GetPrefix() + Slice.FromByte(254)));

				// first call should create a new subspace (with a random prefix)
				FdbDirectorySubspace foo;

				using (var tr = logged.BeginTransaction(this.Cancellation))
				{
					foo = await directory.CreateOrOpenAsync(tr, new[] { "Foo" });
					await tr.CommitAsync();
				}

#if DEBUG
				await DumpSubspace(db, location);
#endif

				Assert.That(foo, Is.Not.Null);
				Assert.That(foo.FullName, Is.EqualTo("Foo"));
				Assert.That(foo.Path, Is.EqualTo(new[] { "Foo" }));
				Assert.That(foo.Layer, Is.EqualTo(Slice.Empty));
				Assert.That(foo.DirectoryLayer, Is.SameAs(directory));

				// second call should return the same subspace

				FdbDirectorySubspace foo2;

				foo2 = await directory.OpenAsync(logged, new[] { "Foo" }, this.Cancellation);
#if DEBUG
				await DumpSubspace(db, location);
#endif
				Assert.That(foo2, Is.Not.Null);
				Assert.That(foo2.FullName, Is.EqualTo("Foo"));
				Assert.That(foo2.Path, Is.EqualTo(new[] { "Foo" }));
				Assert.That(foo2.Layer, Is.EqualTo(Slice.Empty));
				Assert.That(foo2.DirectoryLayer, Is.SameAs(directory));
				Assert.That(foo2.GetPrefix(), Is.EqualTo(foo.GetPrefix()), "Second call to CreateOrOpen should return the same subspace");

#if ENABLE_LOGGING
				foreach (var log in list)
				{
					Console.WriteLine(log.GetTimingsReport(true));
				}
#endif
			}
		}

		[Test]
		public async Task Test_CreateOrOpen_With_Layer()
		{
			using (var db = await OpenTestDatabaseAsync())
			{
				// we will put everything under a custom namespace
				var location = db.Partition.ByKey("DL");
				await db.ClearRangeAsync(location, this.Cancellation);

#if ENABLE_LOGGING
				var list = new List<FdbTransactionLog>();
				var logged = new FdbLoggedDatabase(db, false, false, (tr) => { list.Add(tr.Log); });
#else
				var logged = db;
#endif

				// put the nodes under (..,"DL",\xFE,) and the content under (..,"DL",)
				var directory = FdbDirectoryLayer.Create(location);

				Assert.That(directory.ContentSubspace, Is.Not.Null);
				Assert.That(directory.ContentSubspace, Is.EqualTo(location));
				Assert.That(directory.NodeSubspace, Is.Not.Null);
				Assert.That(directory.NodeSubspace.GetPrefix(), Is.EqualTo(location.GetPrefix() + Slice.FromByte(254)));

				// first call should create a new subspace (with a random prefix)
				var foo = await directory.CreateOrOpenAsync(logged, new[] { "Foo" }, Slice.FromString("AcmeLayer"), this.Cancellation);
#if DEBUG
				await DumpSubspace(db, location);
#endif

				Assert.That(foo, Is.Not.Null);
				Assert.That(foo.FullName, Is.EqualTo("Foo"));
				Assert.That(foo.Path, Is.EqualTo(new[] { "Foo" }));
				Assert.That(foo.Layer.ToUnicode(), Is.EqualTo("AcmeLayer"));
				Assert.That(foo.DirectoryLayer, Is.SameAs(directory));

				// second call should return the same subspace
				var foo2 = await directory.OpenAsync(logged, new[] { "Foo" }, Slice.FromString("AcmeLayer"), this.Cancellation);
				Assert.That(foo2, Is.Not.Null);
				Assert.That(foo2.FullName, Is.EqualTo("Foo"));
				Assert.That(foo2.Path, Is.EqualTo(new[] { "Foo" }));
				Assert.That(foo2.Layer.ToUnicode(), Is.EqualTo("AcmeLayer"));
				Assert.That(foo2.DirectoryLayer, Is.SameAs(directory));
				Assert.That(foo2.GetPrefix(), Is.EqualTo(foo.GetPrefix()), "Second call to CreateOrOpen should return the same subspace");

				// opening it with wrong layer id should fail
				Assert.Throws<InvalidOperationException>(async () => await directory.OpenAsync(logged, new[] { "Foo" }, Slice.FromString("OtherLayer"), this.Cancellation), "Opening with invalid layer id should fail");

				// opening without specifying a layer should disable the layer check
				var foo3 = await directory.OpenAsync(logged, "Foo", layer: Slice.Nil, ct: this.Cancellation);
				Assert.That(foo3, Is.Not.Null);
				Assert.That(foo3.Layer.ToUnicode(), Is.EqualTo("AcmeLayer"));

				// CheckLayer with the correct value should pass
				Assert.DoesNotThrow(() => foo3.CheckLayer(Slice.FromString("AcmeLayer")), "CheckLayer should not throw if the layer id is correct");

				// CheckLayer with the incorrect value should fail
				Assert.Throws<InvalidOperationException>(() => foo3.CheckLayer(Slice.FromString("OtherLayer")), "CheckLayer should throw if the layer id is not correct");

				// CheckLayer with empty string should do nothing
				foo3.CheckLayer(Slice.Empty);
				foo3.CheckLayer(Slice.Nil);

#if ENABLE_LOGGING
				foreach (var log in list)
				{
					Console.WriteLine(log.GetTimingsReport(true));
				}
#endif
			}
		}

		[Test]
		public async Task Test_CreateOrOpen_SubFolder()
		{
			// Create a folder ("foo", "bar", "baz") and ensure that all the parent folder are also creating and linked properly

			using (var db = await OpenTestDatabaseAsync())
			{

				// we will put everything under a custom namespace
				var location = db.Partition.ByKey("DL");
				await db.ClearRangeAsync(location, this.Cancellation);

#if ENABLE_LOGGING
				var list = new List<FdbTransactionLog>();
				var logged = new FdbLoggedDatabase(db, false, false, (tr) => { list.Add(tr.Log); });
#else
				var logged = db;
#endif

				// put the nodes under (..,"DL",\xFE,) and the content under (..,"DL",)
				var directory = FdbDirectoryLayer.Create(location);

				FdbDirectorySubspace folder;
				using (var tr = logged.BeginTransaction(this.Cancellation))
				{

					folder = await directory.CreateOrOpenAsync(tr, new [] { "Foo", "Bar", "Baz" });
					await tr.CommitAsync();
				}
#if DEBUG
				await DumpSubspace(db, location);
#endif

				Assert.That(folder, Is.Not.Null);
				Assert.That(folder.FullName, Is.EqualTo("Foo/Bar/Baz"));
				Assert.That(folder.Path, Is.EqualTo(new[] { "Foo", "Bar", "Baz" }));

				// all the parent folders should also now exist
				var foo = await directory.OpenAsync(logged, new[] { "Foo" }, this.Cancellation);
				var bar = await directory.OpenAsync(logged, new[] { "Foo", "Bar" }, this.Cancellation);
				Assert.That(foo, Is.Not.Null);
				Assert.That(bar, Is.Not.Null);

#if ENABLE_LOGGING
				foreach (var log in list)
				{
					Console.WriteLine(log.GetTimingsReport(true));
				}
#endif
			}
		}

		[Test]
		public async Task Test_List_SubFolders()
		{
			using (var db = await OpenTestDatabaseAsync())
			{
				// we will put everything under a custom namespace
				var location = db.Partition.ByKey("DL");
				await db.ClearRangeAsync(location, this.Cancellation);
				var directory = FdbDirectoryLayer.Create(location);

#if ENABLE_LOGGING
				var list = new List<FdbTransactionLog>();
				var logged = new FdbLoggedDatabase(db, false, false, (tr) => { list.Add(tr.Log); });
#else
				var logged = db;
#endif

				// linear subtree "/Foo/Bar/Baz"
				await directory.CreateOrOpenAsync(logged, new[] { "Foo", "Bar", "Baz" }, this.Cancellation);
				// flat subtree "/numbers/0" to "/numbers/9"
				for (int i = 0; i < 10; i++) await directory.CreateOrOpenAsync(logged, new[] { "numbers", i.ToString() }, this.Cancellation);
#if DEBUG
				await DumpSubspace(db, location);
#endif

				var subdirs = await directory.ListAsync(logged, new[] { "Foo" }, this.Cancellation);
				Assert.That(subdirs, Is.Not.Null);
				Assert.That(subdirs.Count, Is.EqualTo(1));
				Assert.That(subdirs[0], Is.EqualTo("Bar"));

				subdirs = await directory.ListAsync(logged, new[] { "Foo", "Bar" }, this.Cancellation);
				Assert.That(subdirs, Is.Not.Null);
				Assert.That(subdirs.Count, Is.EqualTo(1));
				Assert.That(subdirs[0], Is.EqualTo("Baz"));

				subdirs = await directory.ListAsync(logged, new[] { "Foo", "Bar", "Baz" }, this.Cancellation);
				Assert.That(subdirs, Is.Not.Null);
				Assert.That(subdirs.Count, Is.EqualTo(0));

				subdirs = await directory.ListAsync(logged, new[] { "numbers" }, this.Cancellation);
				Assert.That(subdirs, Is.Not.Null);
				Assert.That(subdirs.Count, Is.EqualTo(10));
				Assert.That(subdirs, Is.EquivalentTo(Enumerable.Range(0, 10).Select(x => x.ToString()).ToList()));

#if ENABLE_LOGGING
				foreach (var log in list)
				{
					Console.WriteLine(log.GetTimingsReport(true));
				}
#endif
			}
		}

		[Test]
		public async Task Test_List_Folders_Should_Be_Sorted_By_Name()
		{
			using (var db = await OpenTestDatabaseAsync())
			{
				// we will put everything under a custom namespace
				var location = db.Partition.ByKey("DL");
				await db.ClearRangeAsync(location, this.Cancellation);
				var directory = FdbDirectoryLayer.Create(location);

				// insert letters in random order
				var rnd = new Random();
				var letters = Enumerable.Range(65, 10).Select(x => new string((char)x, 1)).ToList();
				while(letters.Count > 0)
				{
					var i = rnd.Next(letters.Count);
					var s = letters[i];
					letters.RemoveAt(i);
					await directory.CreateOrOpenAsync(db, new[] { "letters", s }, this.Cancellation);
				}

#if DEBUG
				await DumpSubspace(db, location);
#endif

				// they should sorted when listed
				var subdirs = await directory.ListAsync(db, "letters", this.Cancellation);
				Assert.That(subdirs, Is.Not.Null);
				Assert.That(subdirs.Count, Is.EqualTo(10));
				for (int i = 0; i < subdirs.Count; i++)
				{
					Assert.That(subdirs[i], Is.EqualTo(new string((char)(65 + i), 1)));
				}

			}
		}

		[Test]
		public async Task Test_Move_Folder()
		{
			// Create a folder ("foo", "bar", "baz") and ensure that all the parent folder are also creating and linked properly

			using (var db = await OpenTestDatabaseAsync())
			{
				// we will put everything under a custom namespace
				var location = db.Partition.ByKey("DL");
				await db.ClearRangeAsync(location, this.Cancellation);

#if ENABLE_LOGGING
				var list = new List<FdbTransactionLog>();
				var logged = new FdbLoggedDatabase(db, false, false, (tr) => { list.Add(tr.Log); });
#else
				var logged = db;
#endif

				// put the nodes under (..,"DL",\xFE,) and the content under (..,"DL",)
				var directory = FdbDirectoryLayer.Create(location);

				// create a folder at ('Foo',)
				var original = await directory.CreateOrOpenAsync(logged, "Foo", this.Cancellation);
#if DEBUG
				await DumpSubspace(db, location);
#endif
				Assert.That(original, Is.Not.Null);
				Assert.That(original.FullName, Is.EqualTo("Foo"));
				Assert.That(original.Path, Is.EqualTo(new[] { "Foo" }));

				// rename/move it as ('Bar',)
				var renamed = await original.MoveToAsync(logged, new[] { "Bar" }, this.Cancellation);
#if DEBUG
				await DumpSubspace(db, location);
#endif
				Assert.That(renamed, Is.Not.Null);
				Assert.That(renamed.FullName, Is.EqualTo("Bar"));
				Assert.That(renamed.Path, Is.EqualTo(new[] { "Bar" }));
				Assert.That(renamed.GetPrefix(), Is.EqualTo(original.GetPrefix()));

				// opening the old path should fail
				Assert.Throws<InvalidOperationException>(async () => await directory.OpenAsync(logged, "Foo", this.Cancellation));

				// opening the new path should succeed
				var folder = await directory.OpenAsync(logged, "Bar", this.Cancellation);
				Assert.That(folder, Is.Not.Null);
				Assert.That(folder.FullName, Is.EqualTo(renamed.FullName));
				Assert.That(folder.Path, Is.EqualTo(renamed.Path));
				Assert.That(folder.GetPrefix(), Is.EqualTo(renamed.GetPrefix()));

				// moving the folder under itself should fail
				Assert.Throws<InvalidOperationException>(async () => await folder.MoveToAsync(logged, new[] { "Bar", "Baz" }, this.Cancellation));
#if ENABLE_LOGGING
				foreach (var log in list)
				{
					Console.WriteLine(log.GetTimingsReport(true));
				}
#endif
			}
		}

		[Test]
		public async Task Test_Remove_Folder()
		{
			using (var db = await OpenTestDatabaseAsync())
			{
				var location = db.Partition.ByKey("DL");
				await db.ClearRangeAsync(location, this.Cancellation);
				var directory = FdbDirectoryLayer.Create(location);

#if ENABLE_LOGGING
				var list = new List<FdbTransactionLog>();
				var logged = new FdbLoggedDatabase(db, false, false, (tr) => { list.Add(tr.Log); });
#else
				var logged = db;
#endif

				// RemoveAsync

				string[] path = new[] { "CrashTestDummy" };
				await directory.CreateAsync(logged, path, this.Cancellation);
#if DEBUG
				await DumpSubspace(db, location);
#endif

				// removing an existing folder should succeeed
				await directory.RemoveAsync(logged, path, this.Cancellation);
#if DEBUG
				await DumpSubspace(db, location);
#endif
				//TODO: call ExistsAsync(...) once it is implemented!

				// Removing it a second time should fail
				Assert.Throws<InvalidOperationException>(async () => await directory.RemoveAsync(logged, path, this.Cancellation), "Removing a non-existent directory should fail");

				// TryRemoveAsync

				await directory.CreateAsync(logged, path, this.Cancellation);

				// attempting to remove a folder should return true
				bool res = await directory.TryRemoveAsync(logged, path, this.Cancellation);
				Assert.That(res, Is.True);

				// further attempts should return false
				res = await directory.TryRemoveAsync(logged, path, this.Cancellation);
				Assert.That(res, Is.False);

				// Corner Cases

				// removing the root folder is not allowed (too dangerous)
				Assert.Throws<InvalidOperationException>(async () => await directory.RemoveAsync(logged, new string[0], this.Cancellation), "Attempting to remove the root directory should fail");

#if ENABLE_LOGGING
				foreach (var log in list)
				{
					Console.WriteLine(log.GetTimingsReport(true));
				}
#endif
			}
		}

		[Test]
		public async Task Test_Can_Change_Layer_Of_Existing_Directory()
		{
			using (var db = await OpenTestDatabaseAsync())
			{
				var location = db.Partition.ByKey("DL");
				await db.ClearRangeAsync(location, this.Cancellation);
				var directory = FdbDirectoryLayer.Create(location);

#if ENABLE_LOGGING
				var list = new List<FdbTransactionLog>();
				var logged = new FdbLoggedDatabase(db, false, false, (tr) => { list.Add(tr.Log); });
#else
				var logged = db;
#endif

				var folder = await directory.CreateAsync(logged, "Test", layer: Slice.FromString("foo"), ct: this.Cancellation);
#if DEBUG
				await DumpSubspace(db, location);
#endif
				Assert.That(folder, Is.Not.Null);
				Assert.That(folder.Layer.ToUnicode(), Is.EqualTo("foo"));

				// change the layer to 'bar'
				var folder2 = await folder.ChangeLayerAsync(logged, Slice.FromString("bar"), this.Cancellation);
#if DEBUG
				await DumpSubspace(db, location);
#endif
				Assert.That(folder2, Is.Not.Null);
				Assert.That(folder2.Layer.ToUnicode(), Is.EqualTo("bar"));
				Assert.That(folder2.FullName, Is.EqualTo("Test"));
				Assert.That(folder2.Path, Is.EqualTo(STuple.Create("Test")));
				Assert.That(folder2.GetPrefix(), Is.EqualTo(folder.GetPrefix()));

				// opening the directory with the new layer should succeed
				var folder3 = await directory.OpenAsync(logged, "Test", layer: Slice.FromString("bar"), ct: this.Cancellation);
				Assert.That(folder3, Is.Not.Null);

				// opening the directory with the old layer should fail
				Assert.Throws<InvalidOperationException>(async () => await directory.OpenAsync(logged, "Test", layer: Slice.FromString("foo"), ct: this.Cancellation));

#if ENABLE_LOGGING
				foreach (var log in list)
				{
					Console.WriteLine(log.GetTimingsReport(true));
				}
#endif
			}
		}

		[Test]
		public async Task Test_Directory_Partitions()
		{
			using (var db = await OpenTestDatabaseAsync())
			{
				var location = db.Partition.ByKey("DL");
				await db.ClearRangeAsync(location, this.Cancellation);

				var directory = FdbDirectoryLayer.Create(location);
				Console.WriteLine(directory);

				var partition = await directory.CreateAsync(db, "Foo", Slice.FromStringAscii("partition"), this.Cancellation);
				// we can't get the partition key directory (because it's a root directory) so we need to cheat a little bit
				var partitionKey = KeySubspace.Copy(partition).GetPrefix();
				Console.WriteLine(partition);
				Assert.That(partition, Is.InstanceOf<FdbDirectoryPartition>());
				Assert.That(partition.Layer, Is.EqualTo(Slice.FromStringAscii("partition")));
				Assert.That(partition.FullName, Is.EqualTo("Foo"));
				Assert.That(partition.Path, Is.EqualTo(new[] { "Foo" }), "Partition's path should be absolute");
				Assert.That(partition.DirectoryLayer, Is.Not.SameAs(directory), "Partitions should have their own DL");
				Assert.That(partition.DirectoryLayer.ContentSubspace.GetPrefix(), Is.EqualTo(partitionKey), "Partition's content should be under the partition's prefix");
				Assert.That(partition.DirectoryLayer.NodeSubspace.GetPrefix(), Is.EqualTo(partitionKey + FdbKey.Directory), "Partition's nodes should be under the partition's prefix");

				var bar = await partition.CreateAsync(db, "Bar", this.Cancellation);
				Console.WriteLine(bar);
				Assert.That(bar, Is.InstanceOf<FdbDirectorySubspace>());
				Assert.That(bar.Path, Is.EqualTo(new [] { "Foo", "Bar" }), "Path of directories under a partition should be absolute");
				Assert.That(bar.GetPrefix(), Is.Not.EqualTo(partitionKey), "{0} should be located under {1}", bar, partition);
				Assert.That(bar.GetPrefix().StartsWith(partitionKey), Is.True, "{0} should be located under {1}", bar, partition);

				var baz = await partition.CreateAsync(db, "Baz", this.Cancellation);
				Console.WriteLine(baz);
				Assert.That(baz, Is.InstanceOf<FdbDirectorySubspace>());
				Assert.That(baz.FullName, Is.EqualTo("Foo/Baz"));
				Assert.That(baz.Path, Is.EqualTo(new[] { "Foo", "Baz" }), "Path of directories under a partition should be absolute");
				Assert.That(baz.GetPrefix(), Is.Not.EqualTo(partitionKey), "{0} should be located under {1}", baz, partition);
				Assert.That(baz.GetPrefix().StartsWith(partitionKey), Is.True, "{0} should be located under {1}", baz, partition);

				// Rename 'Bar' to 'BarBar'
				var bar2 = await bar.MoveToAsync(db, new[] { "Foo", "BarBar" }, this.Cancellation);
				Console.WriteLine(bar2);
				Assert.That(bar2, Is.InstanceOf<FdbDirectorySubspace>());
				Assert.That(bar2, Is.Not.SameAs(bar));
				Assert.That(bar2.GetPrefix(), Is.EqualTo(bar.GetPrefix()));
				Assert.That(bar2.FullName, Is.EqualTo("Foo/BarBar"));
				Assert.That(bar2.Path, Is.EqualTo(new[] { "Foo", "BarBar" }));
				Assert.That(bar2.DirectoryLayer, Is.SameAs(bar.DirectoryLayer));
			}
		}

		[Test]
		public async Task Test_Directory_Cannot_Move_To_Another_Partition()
		{
			using (var db = await OpenTestDatabaseAsync())
			{
				var location = db.Partition.ByKey("DL");
				await db.ClearRangeAsync(location, this.Cancellation);

				var directory = FdbDirectoryLayer.Create(location);
				Console.WriteLine(directory);

				var foo = await directory.CreateAsync(db, "Foo", Slice.FromStringAscii("partition"), this.Cancellation);
				Console.WriteLine(foo);

				// create a 'Bar' under the 'Foo' partition
				var bar = await foo.CreateAsync(db, "Bar", this.Cancellation);
				Console.WriteLine(bar);
				Assert.That(bar.FullName, Is.EqualTo("Foo/Bar"));
				Assert.That(bar.Path, Is.EqualTo(new string[] { "Foo", "Bar" }));
				Assert.That(bar.DirectoryLayer, Is.Not.SameAs(directory));
				Assert.That(bar.DirectoryLayer, Is.SameAs(foo.DirectoryLayer));

				// Attempting to move 'Bar' outside the Foo partition should fail
				Assert.That(async () => await bar.MoveToAsync(db, new[] { "Bar" }, this.Cancellation), Throws.InstanceOf<InvalidOperationException>());
				Assert.That(async () => await directory.MoveAsync(db, new[] { "Foo", "Bar" }, new[] { "Bar" }, this.Cancellation), Throws.InstanceOf<InvalidOperationException>());
			}

		}

		[Test]
		public async Task Test_Directory_Cannot_Move_To_A_Sub_Partition()
		{
			using (var db = await OpenTestDatabaseAsync())
			{
				var location = db.Partition.ByKey("DL");
				await db.ClearRangeAsync(location, this.Cancellation);

				var directory = FdbDirectoryLayer.Create(location);
				Console.WriteLine(directory);

				var outer = await directory.CreateAsync(db, "Outer", Slice.FromStringAscii("partition"), this.Cancellation);
				Console.WriteLine(outer);

				// create a 'Inner' subpartition under the 'Outer' partition
				var inner = await outer.CreateAsync(db, "Inner", Slice.FromString("partition"), this.Cancellation);
				Console.WriteLine(inner);
				Assert.That(inner.FullName, Is.EqualTo("Outer/Inner"));
				Assert.That(inner.Path, Is.EqualTo(new string[] { "Outer", "Inner" }));
				Assert.That(inner.DirectoryLayer, Is.Not.SameAs(directory));
				Assert.That(inner.DirectoryLayer, Is.Not.SameAs(outer.DirectoryLayer));

				// create folder /Outer/Foo
				var foo = await outer.CreateAsync(db, "Foo", this.Cancellation);
				Assert.That(foo.FullName, Is.EqualTo("Outer/Foo"));
				Assert.That(foo.Path, Is.EqualTo(new string[] { "Outer", "Foo" }));
				// create folder /Outer/Inner/Bar
				var bar = await inner.CreateAsync(db, "Bar", this.Cancellation);
				Assert.That(bar.FullName, Is.EqualTo("Outer/Inner/Bar"));
				Assert.That(bar.Path, Is.EqualTo(new string[] { "Outer", "Inner", "Bar" }));

				// Attempting to move 'Foo' inside the Inner partition should fail
				Assert.That(async () => await foo.MoveToAsync(db, new[] { "Outer", "Inner", "Foo" }, this.Cancellation), Throws.InstanceOf<InvalidOperationException>());
				Assert.That(async () => await directory.MoveAsync(db, new[] { "Outer", "Foo" }, new[] { "Outer", "Inner", "Foo" }, this.Cancellation), Throws.InstanceOf<InvalidOperationException>());

				// Attemping to move 'Bar' outside the Inner partition should fail
				Assert.That(async () => await bar.MoveToAsync(db, new[] { "Outer", "Bar" }, this.Cancellation), Throws.InstanceOf<InvalidOperationException>());
				Assert.That(async () => await directory.MoveAsync(db, new[] { "Outer", "Inner", "Bar" }, new[] { "Outer", "Bar" }, this.Cancellation), Throws.InstanceOf<InvalidOperationException>());

				// Moving 'Foo' inside the Outer partition itself should work
				await directory.CreateAsync(db, new[] { "Outer", "SubFolder" }, this.Cancellation); // parent of destination folder must already exist when moving...
				var foo2 = await directory.MoveAsync(db, new[] { "Outer", "Foo" }, new[] { "Outer", "SubFolder", "Foo" }, this.Cancellation);
				Assert.That(foo2.FullName, Is.EqualTo("Outer/SubFolder/Foo"));
				Assert.That(foo2.Path, Is.EqualTo(new[] { "Outer", "SubFolder", "Foo" }));
				Assert.That(foo2.GetPrefix(), Is.EqualTo(foo.GetPrefix()));

				// Moving 'Bar' inside the Inner partition itself should work
				await directory.CreateAsync(db, new[] { "Outer", "Inner", "SubFolder" }, this.Cancellation); // parent of destination folder must already exist when moving...
				var bar2 = await directory.MoveAsync(db, new[] { "Outer", "Inner", "Bar" }, new[] { "Outer", "Inner", "SubFolder", "Bar" }, this.Cancellation);
				Assert.That(bar2.FullName, Is.EqualTo("Outer/Inner/SubFolder/Bar"));
				Assert.That(bar2.Path, Is.EqualTo(new[] { "Outer", "Inner", "SubFolder", "Bar" }));
				Assert.That(bar2.GetPrefix(), Is.EqualTo(bar.GetPrefix()));
			}

		}

		[Test]
		public async Task Test_Renaming_Partition_Uses_Parent_DirectoryLayer()
		{
			// - Create a partition "foo"
			// - Verify that DL.List() returns only "foo"
			// - Rename partition "foo" to "bar"
			// - Verify that DL.List() now returns only "bar"

			using (var db = await OpenTestDatabaseAsync())
			{
				var location = db.Partition.ByKey("DL");
				await db.ClearRangeAsync(location, this.Cancellation);
				var directory = FdbDirectoryLayer.Create(location);

				using (var tr = db.BeginTransaction(this.Cancellation))
				{
					// create foo
					var foo = await directory.CreateOrOpenAsync(tr, "foo", Slice.FromString("partition"));
					Assert.That(foo, Is.Not.Null);
					Assert.That(foo.FullName, Is.EqualTo("foo"));
					Assert.That(foo.Path, Is.EqualTo(new[] { "foo" }));
					Assert.That(foo.Layer, Is.EqualTo(Slice.FromString("partition")));
					Assert.That(foo, Is.InstanceOf<FdbDirectoryPartition>());

					// verify list
					var folders = await directory.ListAsync(tr);
					Assert.That(folders, Is.EqualTo(new[] { "foo" }));

					// rename to bar
					var bar = await foo.MoveToAsync(tr, new[] { "bar" });
					Assert.That(bar, Is.Not.Null);
					Assert.That(bar.FullName, Is.EqualTo("bar"));
					Assert.That(bar.Path, Is.EqualTo(new[] { "bar" }));
					Assert.That(bar.Layer, Is.EqualTo(Slice.FromString("partition")));
					Assert.That(bar, Is.InstanceOf<FdbDirectoryPartition>());

					// should have kept the same prefix
					//note: we need to cheat to get the key of the partition
					Assert.That(KeySubspace.Copy(bar).GetPrefix(), Is.EqualTo(KeySubspace.Copy(foo).GetPrefix()));

					// verify list again
					folders = await directory.ListAsync(tr);
					Assert.That(folders, Is.EqualTo(new[] { "bar" }));

					//no need to commit
				}
			}
		}

		[Test]
		public async Task Test_Removing_Partition_Uses_Parent_DirectoryLayer()
		{
			// - Create a partition "foo" in ROOT
			// - Verify that ROOT.List() returns "foo"
			// - Remove "foo" via dir.RemoveAsync()
			// - Verify that ROOT.List() now returns an empty list
			// - Verify that dir.ExistsAsync() returns false

			using (var db = await OpenTestDatabaseAsync())
			{
				var location = db.Partition.ByKey("DL");
				await db.ClearRangeAsync(location, this.Cancellation);
				var directory = FdbDirectoryLayer.Create(location);

				using (var tr = db.BeginTransaction(this.Cancellation))
				{
					// create foo
					var foo = await directory.CreateOrOpenAsync(tr, "foo", Slice.FromString("partition"));
					Assert.That(foo, Is.Not.Null);
					Assert.That(foo.FullName, Is.EqualTo("foo"));
					Assert.That(foo.Path, Is.EqualTo(new[] { "foo" }));
					Assert.That(foo.Layer, Is.EqualTo(Slice.FromString("partition")));
					Assert.That(foo, Is.InstanceOf<FdbDirectoryPartition>());

					// verify list
					var folders = await directory.ListAsync(tr);
					Assert.That(folders, Is.EqualTo(new[] { "foo" }));

					// delete foo
					await foo.RemoveAsync(tr);

					// verify list again
					folders = await directory.ListAsync(tr);
					Assert.That(folders, Is.Empty);

					// verify that it does not exist anymore
					var res = await foo.ExistsAsync(tr);
					Assert.That(res, Is.False);

					res = await directory.ExistsAsync(tr, "foo");
					Assert.That(res, Is.False);

					//no need to commit
				}
			}
		}

		[Test]
		public async Task Test_Directory_Methods_Should_Fail_With_Empty_Paths()
		{
			using (var db = await OpenTestDatabaseAsync())
			{
				var location = db.Partition.ByKey("DL");
				await db.ClearRangeAsync(location, this.Cancellation);
				var directory = FdbDirectoryLayer.Create(location);

				// CreateOrOpen
				Assert.Throws<ArgumentNullException>(async () => await directory.CreateOrOpenAsync(db, default(string[]), this.Cancellation));
				Assert.Throws<InvalidOperationException>(async () => await directory.CreateOrOpenAsync(db, new string[0], this.Cancellation));
				Assert.Throws<ArgumentNullException>(async () => await directory.CreateOrOpenAsync(db, default(string), this.Cancellation));

				// Create
				Assert.Throws<ArgumentNullException>(async () => await directory.CreateAsync(db, default(string[]), this.Cancellation));
				Assert.Throws<InvalidOperationException>(async () => await directory.CreateAsync(db, new string[0], this.Cancellation));
				Assert.Throws<ArgumentNullException>(async () => await directory.CreateAsync(db, default(string), this.Cancellation));

				// Open
				Assert.Throws<ArgumentNullException>(async () => await directory.OpenAsync(db, default(string[]), this.Cancellation));
				Assert.Throws<InvalidOperationException>(async () => await directory.OpenAsync(db, new string[0], this.Cancellation));
				Assert.Throws<ArgumentNullException>(async () => await directory.OpenAsync(db, default(string), this.Cancellation));

				// Move
				Assert.Throws<ArgumentNullException>(async () => await directory.MoveAsync(db, default(string[]), new[] { "foo" }, this.Cancellation));
				Assert.Throws<ArgumentNullException>(async () => await directory.MoveAsync(db, new[] { "foo" }, default(string[]), this.Cancellation));
				Assert.Throws<InvalidOperationException>(async () => await directory.MoveAsync(db, new string[0], new[] { "foo" }, this.Cancellation));
				Assert.Throws<InvalidOperationException>(async () => await directory.MoveAsync(db, new[] { "foo" }, new string[0], this.Cancellation));

				// Remove
				Assert.Throws<ArgumentNullException>(async () => await directory.RemoveAsync(db, default(string[]), this.Cancellation));
				Assert.Throws<InvalidOperationException>(async () => await directory.RemoveAsync(db, new string[0], this.Cancellation));
				Assert.Throws<InvalidOperationException>(async () => await directory.RemoveAsync(db, new string[] { "Foo", " ", "Bar" }, this.Cancellation));
				Assert.Throws<ArgumentNullException>(async () => await directory.RemoveAsync(db, default(string), this.Cancellation));

				// List
				Assert.Throws<ArgumentNullException>(async () => await directory.ListAsync(db, default(string[]), this.Cancellation));
				Assert.Throws<InvalidOperationException>(async () => await directory.ListAsync(db, new string[] { "Foo", "", "Bar" }, this.Cancellation));
				Assert.Throws<ArgumentNullException>(async () => await directory.ListAsync(db, default(string), this.Cancellation));

			}
		}

		[Test]
		public async Task Test_Directory_Partitions_Should_Disallow_Creation_Of_Direct_Keys()
		{
			// an instance of FdbDirectoryPartition should throw when attempting to create keys directly under it, and should only be used to create/open sub directories
			// => this is because keys created directly will most certainly conflict with directory subspaces

			using (var db = await OpenTestDatabaseAsync())
			{
				var location = db.Partition.ByKey("DL");
				await db.ClearRangeAsync(location, this.Cancellation);

				var directory = FdbDirectoryLayer.Create(location);
				Console.WriteLine(directory);

				var partition = await directory.CreateAsync(db, "Foo", Slice.FromStringAscii("partition"), this.Cancellation);
				//note: if we want a testable key INSIDE the partition, we have to get it from a sub-directory
				var subdir = await partition.CreateOrOpenAsync(db, "Bar", this.Cancellation);
				var barKey = subdir.GetPrefix();

				// the constraint will always be the same for all the checks
				Action<TestDelegate> shouldFail = (del) =>
				{
					Assert.That(del, Throws.InstanceOf<InvalidOperationException>().With.Message.StringContaining("root of a directory partition"));
				};
				Action<TestDelegate> shouldPass = (del) =>
				{
					Assert.That(del, Throws.Nothing);
				};

				// === PASS ===
				// these methods are allowed to succeed on directory partitions, because we need them for the rest to work

				shouldPass(() => { var _ = KeySubspace.Copy(partition).GetPrefix(); }); // EXCEPTION: we need this to work, because that's the only way that the unit tests above can see the partition key!
				shouldPass(() => partition.ToString()); // EXCEPTION: this should never fail!
				shouldPass(() => partition.DumpKey(barKey)); // EXCEPTION: this should always work, because this can be used for debugging and logging...
				shouldPass(() => partition.BoundCheck(barKey, true)); // EXCEPTION: needs to work because it is used by GetRange() and GetKey()

				// === FAIL ====

				// Key
				shouldFail(() => { var _ = partition.GetPrefix(); });

				// Contains
				shouldFail(() => partition.Contains(barKey));

				// Extract / ExtractAndCheck / BoundCheck
				shouldFail(() => partition.ExtractKey(barKey, boundCheck: false));
				shouldFail(() => partition.ExtractKey(barKey, boundCheck: true));
				shouldFail(() => partition.ExtractKeys(new[] { barKey, barKey + FdbKey.MinValue }));

				// Partition
				shouldFail(() => partition.Partition.ByKey(123));
				shouldFail(() => partition.Partition.ByKey(123, "hello"));
				shouldFail(() => partition.Partition.ByKey(123, "hello", false));
				shouldFail(() => partition.Partition.ByKey(123, "hello", false, "world"));

				// Keys

				shouldFail(() => partition.ConcatKey(Slice.FromString("hello")));
				shouldFail(() => partition.ConcatKey(location.GetPrefix()));
				shouldFail(() => partition.ConcatKeys(new[] { Slice.FromString("hello"), Slice.FromString("world"), Slice.FromString("!") }));

				shouldFail(() => { var _ = partition[Slice.FromString("hello")]; });
				shouldFail(() => { var _ = partition[location.GetPrefix()]; });

				shouldFail(() => partition.ToRange());
				shouldFail(() => partition.ToRange(Slice.FromString("hello")));
				shouldFail(() => partition.ToRange(TuPack.EncodeKey("hello")));

 				// Tuples

				shouldFail(() => partition.Keys.Encode(123));
				shouldFail(() => partition.Keys.Encode(123, "hello"));
				shouldFail(() => partition.Keys.Encode(123, "hello", false));
				shouldFail(() => partition.Keys.Encode(123, "hello", false, "world"));
				shouldFail(() => partition.Keys.Encode<object>(123));

				shouldFail(() => partition.Keys.EncodeMany<int>(new[] { 123, 456, 789 }));
				shouldFail(() => partition.Keys.EncodeMany<int>((IEnumerable<int>)new[] { 123, 456, 789 }));
				shouldFail(() => partition.Keys.EncodeMany<object>(new object[] { 123, "hello", true }));
				shouldFail(() => partition.Keys.EncodeMany<object>((IEnumerable<object>)new object[] { 123, "hello", true }));

				shouldFail(() => partition.Keys.Unpack(barKey));
				shouldFail(() => partition.Keys.UnpackMany(new[] { barKey, barKey + TuPack.EncodeKey(123) }));
				shouldFail(() => partition.Keys.Decode<int>(barKey));
				shouldFail(() => partition.Keys.DecodeMany<int>(new[] { barKey, barKey }));
				shouldFail(() => partition.Keys.DecodeLast<int>(barKey));
				shouldFail(() => partition.Keys.DecodeLastMany<int>(new[] { barKey, barKey + TuPack.EncodeKey(123) }));
				shouldFail(() => partition.Keys.DecodeFirst<int>(barKey));
				shouldFail(() => partition.Keys.DecodeFirstMany<int>(new[] { barKey, barKey + TuPack.EncodeKey(123) }));

				//FIXME: need to re-enable this code!
#if REFACTORING_IN_PROGRESS
				shouldFail(() => partition.Keys.ToTuple());

				shouldFail(() => partition.Keys.Append(123));
				shouldFail(() => partition.Keys.Append(123, "hello"));
				shouldFail(() => partition.Keys.Append(123, "hello", false));
				shouldFail(() => partition.Keys.Append(123, "hello", false, "world"));
				shouldFail(() => partition.Keys.Concat(STuple.Create(123, "hello", false, "world")));
				shouldFail(() => partition.Keys.Append(new object[] { 123, "hello", false, "world" }));
#endif

				shouldFail(() => partition.Keys.ToRange());
				shouldFail(() => partition.ToRange(Slice.FromString("hello")));
				shouldFail(() => partition.Keys.ToRange(STuple.Create("hello")));

			}
		}

		[Test]
		public async Task Test_Concurrent_Directory_Creation()
		{
			using (var db = await OpenTestDatabaseAsync())
			{
				var location = db.Partition.ByKey("DL");
				await db.ClearRangeAsync(location, this.Cancellation);

				var directory = FdbDirectoryLayer.Create(location);
				Console.WriteLine(directory);

				//to prevent any side effect from first time initialization of the directory layer, already create one dummy folder
				await directory.CreateAsync(db, "Zero", this.Cancellation);

				var logdb = db.Logged((tr) => Console.WriteLine(tr.Log.GetTimingsReport(true)));

				var f = FdbDirectoryLayer.AnnotateTransactions;
				try
				{
					FdbDirectoryLayer.AnnotateTransactions = true;

					using (var tr1 = logdb.BeginTransaction(this.Cancellation))
					using (var tr2 = logdb.BeginTransaction(this.Cancellation))
					{

						await Task.WhenAll(
							tr1.GetReadVersionAsync(),
							tr2.GetReadVersionAsync()
						);

						var first = await directory.CreateAsync(tr1, new[] { "First" }, Slice.Nil);
						tr1.Set(first.GetPrefix(), Slice.FromString("This belongs to the first directory"));

						var second = await directory.CreateAsync(tr2, new[] { "Second" }, Slice.Nil);
						tr2.Set(second.GetPrefix(), Slice.FromString("This belongs to the second directory"));

						Console.WriteLine("Committing T1...");
						await tr1.CommitAsync();
						Console.WriteLine("T1 committed");
						tr1.Dispose(); // force T1 to be dumped immediately

						Console.WriteLine("Committing T2...");
						await tr2.CommitAsync();
						Console.WriteLine("T2 committed");
					}
				}
				finally
				{
					FdbDirectoryLayer.AnnotateTransactions = f;
				}

			}

		}

		[Test]
		public async Task Test_Concurrent_Directory_Creation_With_Custom_Prefix()
		{
			using (var db = await OpenTestDatabaseAsync())
			{
				var location = db.Partition.ByKey("DL");
				await db.ClearRangeAsync(location, this.Cancellation);

				// to keep the test db in a good shape, we will still use tuples for our custom prefix,
				// but using strings so that they do not collide with the integers used by the normal allocator
				// ie: regular prefix would be ("DL", 123) and our custom prefixes will be ("DL", "abc")

				var directory = FdbDirectoryLayer.Create(location);
				Console.WriteLine(directory);

				//to prevent any side effect from first time initialization of the directory layer, already create one dummy folder
				await directory.CreateAsync(db, "Zero", this.Cancellation);

				var logdb = db.Logged((tr) => Console.WriteLine(tr.Log.GetTimingsReport(true)));

				var f = FdbDirectoryLayer.AnnotateTransactions;
				try
				{
					FdbDirectoryLayer.AnnotateTransactions = true;

					using (var tr1 = logdb.BeginTransaction(this.Cancellation))
					using (var tr2 = logdb.BeginTransaction(this.Cancellation))
					{

						await Task.WhenAll(
							tr1.GetReadVersionAsync(),
							tr2.GetReadVersionAsync()
						);

						var first = await directory.RegisterAsync(tr1, new[] { "First" }, Slice.Nil, location.Keys.Encode("abc"));
						tr1.Set(first.GetPrefix(), Slice.FromString("This belongs to the first directory"));

						var second = await directory.RegisterAsync(tr2, new[] { "Second" }, Slice.Nil, location.Keys.Encode("def"));
						tr2.Set(second.GetPrefix(), Slice.FromString("This belongs to the second directory"));

						Console.WriteLine("Committing T1...");
						await tr1.CommitAsync();
						Console.WriteLine("T1 committed");
						tr1.Dispose(); // force T1 to be dumped immediately

						Console.WriteLine("Committing T2...");
						try
						{
							await tr2.CommitAsync();
						}
						catch(FdbException x)
						{
							if (x.Code == FdbError.NotCommitted)
							{
								// Current implementation of the DirectoryLayer conflicts with other transactions when checking if the custom key prefix has already been used in the DL
								// cf: http://community.foundationdb.com/questions/4493/im-seeing-conflicts-when-creating-directory-subspa.html#answer-4497
								Assert.Inconclusive("FIXME: Current implementation of DirectoryLayer creates read conflict when registering directories with custom key prefix");
							}
							throw;
						}
						Console.WriteLine("T2 committed");
					}
				}
				finally
				{
					FdbDirectoryLayer.AnnotateTransactions = f;
				}

			}

		}

	}

}
