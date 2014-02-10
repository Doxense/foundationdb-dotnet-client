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

namespace FoundationDB.Layers.Directories
{
	using FoundationDB.Client;
	using FoundationDB.Client.Tests;
	using FoundationDB.Filters.Logging;
	using FoundationDB.Layers.Tuples;
	using NUnit.Framework;
	using System;
	using System.Collections.Generic;
	using System.Linq;
	using System.Threading.Tasks;

	[TestFixture]
	public class DirectoryFacts
	{

		[Test]
		public async Task Test_Allocator()
		{
			//FoundationDB.Client.Utils.Logging.SetLevel(System.Diagnostics.SourceLevels.Verbose);

			using (var db = await TestHelpers.OpenTestDatabaseAsync())
			{
				var location = db.Partition(Slice.FromString("hca"));
				await db.ClearRangeAsync(location);

#if DEBUG
				var list = new List<FdbTransactionLog>();
				var logged = new FdbLoggedDatabase(db, false, false, (tr) => { list.Add(tr.Log); });
#else
				var logged = db;
#endif

				var hpa = new FdbHighContentionAllocator(location);

				long id;
				var ids = new HashSet<long>();

				using (var tr = logged.BeginTransaction())
				{
					id = await hpa.AllocateAsync(tr);
					await tr.CommitAsync();
				}
				Console.WriteLine(id);
				ids.Add(id);

				await TestHelpers.DumpSubspace(db, location);

				for (int i = 0; i < 100; i++)
				{
					using (var tr = logged.BeginTransaction())
					{
						id = await hpa.AllocateAsync(tr);
						await tr.CommitAsync();
					}

					if (ids.Contains(id))
					{
						await TestHelpers.DumpSubspace(db, location);
						Assert.Fail("Duplicate key allocated: {0} (#{1})", id, i);
					}
					ids.Add(id);
				}

				await TestHelpers.DumpSubspace(db, location);

#if DEBUG
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
			using (var db = await TestHelpers.OpenTestDatabaseAsync())
			{
				// we will put everything under a custom namespace
				var location = db.Partition("DL");
				await db.ClearRangeAsync(location);

#if DEBUG
				var list = new List<FdbTransactionLog>();
				var logged = new FdbLoggedDatabase(db, false, false, (tr) => { list.Add(tr.Log); });
#else
				var logged = db;
#endif

				// put the nodes under (..,"DL",\xFE,) and the content under (..,"DL",)
				var directory = FdbDirectoryLayer.Create(location);

				Assert.That(directory.ContentSubspace, Is.Not.Null);
				Assert.That(directory.ContentSubspace.Key, Is.EqualTo(location.Key));
				Assert.That(directory.NodeSubspace, Is.Not.Null);
				Assert.That(directory.NodeSubspace.Key, Is.EqualTo(location.Key + Slice.FromByte(254)));

				// first call should create a new subspace (with a random prefix)
				FdbDirectorySubspace foo;

				using (var tr = logged.BeginTransaction())
				{
					foo = await directory.CreateOrOpenAsync(tr, new[] { "Foo" });
					await tr.CommitAsync();
				}

#if DEBUG
				await TestHelpers.DumpSubspace(db, location);
#endif

				Assert.That(foo, Is.Not.Null);
				Assert.That(foo.Path, Is.EqualTo(new[] { "Foo" }));
				Assert.That(foo.Layer, Is.EqualTo(Slice.Empty));
				Assert.That(foo.DirectoryLayer, Is.SameAs(directory));

				// second call should return the same subspace

				FdbDirectorySubspace foo2;

				foo2 = await directory.OpenAsync(logged, new[] { "Foo" });
#if DEBUG
				await TestHelpers.DumpSubspace(db, location);
#endif
				Assert.That(foo2, Is.Not.Null);
				Assert.That(foo2.Path, Is.EqualTo(new[] { "Foo" }));
				Assert.That(foo2.Layer, Is.EqualTo(Slice.Empty));
				Assert.That(foo2.DirectoryLayer, Is.SameAs(directory));
				Assert.That(foo2.Key, Is.EqualTo(foo.Key), "Second call to CreateOrOpen should return the same subspace");

#if DEBUG
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
			using (var db = await TestHelpers.OpenTestDatabaseAsync())
			{
				// we will put everything under a custom namespace
				var location = db.Partition("DL");
				await db.ClearRangeAsync(location);

#if DEBUG
				var list = new List<FdbTransactionLog>();
				var logged = new FdbLoggedDatabase(db, false, false, (tr) => { list.Add(tr.Log); });
#else
				var logged = db;
#endif

				// put the nodes under (..,"DL",\xFE,) and the content under (..,"DL",)
				var directory = FdbDirectoryLayer.Create(location);

				Assert.That(directory.ContentSubspace, Is.Not.Null);
				Assert.That(directory.ContentSubspace.Key, Is.EqualTo(location.Key));
				Assert.That(directory.NodeSubspace, Is.Not.Null);
				Assert.That(directory.NodeSubspace.Key, Is.EqualTo(location.Key + Slice.FromByte(254)));

				// first call should create a new subspace (with a random prefix)
				var foo = await directory.CreateOrOpenAsync(logged, new[] { "Foo" }, Slice.FromString("AcmeLayer"));
#if DEBUG
				await TestHelpers.DumpSubspace(db, location);
#endif

				Assert.That(foo, Is.Not.Null);
				Assert.That(foo.Path, Is.EqualTo(new[] { "Foo" }));
				Assert.That(foo.Layer.ToUnicode(), Is.EqualTo("AcmeLayer"));
				Assert.That(foo.DirectoryLayer, Is.SameAs(directory));

				// second call should return the same subspace
				var foo2 = await directory.OpenAsync(logged, new[] { "Foo" }, Slice.FromString("AcmeLayer"));
				Assert.That(foo2, Is.Not.Null);
				Assert.That(foo2.Path, Is.EqualTo(new[] { "Foo" }));
				Assert.That(foo2.Layer.ToUnicode(), Is.EqualTo("AcmeLayer"));
				Assert.That(foo2.DirectoryLayer, Is.SameAs(directory));
				Assert.That(foo2.Key, Is.EqualTo(foo.Key), "Second call to CreateOrOpen should return the same subspace");

				// opening it with wrong layer id should fail
				Assert.Throws<InvalidOperationException>(async () => await directory.OpenAsync(logged, new[] { "Foo" }, Slice.FromString("OtherLayer")), "Opening with invalid layer id should fail");

				// opening without specifying a layer should disable the layer check
				var foo3 = await directory.OpenAsync(logged, "Foo", layer: Slice.Nil);
				Assert.That(foo3, Is.Not.Null);
				Assert.That(foo3.Layer.ToUnicode(), Is.EqualTo("AcmeLayer"));

				// CheckLayer with the correct value should pass
				Assert.DoesNotThrow(() => foo3.CheckLayer(Slice.FromString("AcmeLayer")), "CheckLayer should not throw if the layer id is correct");

				// CheckLayer with the incorrect value should fail
				Assert.Throws<InvalidOperationException>(() => foo3.CheckLayer(Slice.FromString("OtherLayer")), "CheckLayer should throw if the layer id is not correct");

				// CheckLayer with empty string should do nothing
				foo3.CheckLayer(Slice.Empty);
				foo3.CheckLayer(Slice.Nil);

#if DEBUG
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

			using (var db = await TestHelpers.OpenTestDatabaseAsync())
			{

				// we will put everything under a custom namespace
				var location = db.Partition("DL");
				await db.ClearRangeAsync(location);

#if DEBUG
				var list = new List<FdbTransactionLog>();
				var logged = new FdbLoggedDatabase(db, false, false, (tr) => { list.Add(tr.Log); });
#else
				var logged = db;
#endif

				// put the nodes under (..,"DL",\xFE,) and the content under (..,"DL",)
				var directory = FdbDirectoryLayer.Create(location);

				FdbDirectorySubspace folder;
				using (var tr = logged.BeginTransaction())
				{

					folder = await directory.CreateOrOpenAsync(tr, new [] { "Foo", "Bar", "Baz" });
					await tr.CommitAsync();
				}
#if DEBUG
				await TestHelpers.DumpSubspace(db, location);
#endif

				Assert.That(folder, Is.Not.Null);
				Assert.That(folder.Path, Is.EqualTo(new [] { "Foo", "Bar", "Baz" }));

				// all the parent folders should also now exist
				var foo = await directory.OpenAsync(logged, new[] { "Foo" });
				var bar = await directory.OpenAsync(logged, new[] { "Foo", "Bar" });
				Assert.That(foo, Is.Not.Null);
				Assert.That(bar, Is.Not.Null);

#if DEBUG
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
			using (var db = await TestHelpers.OpenTestDatabaseAsync())
			{
				// we will put everything under a custom namespace
				var location = db.Partition("DL");
				await db.ClearRangeAsync(location);
				var directory = FdbDirectoryLayer.Create(location);

#if DEBUG
				var list = new List<FdbTransactionLog>();
				var logged = new FdbLoggedDatabase(db, false, false, (tr) => { list.Add(tr.Log); });
#else
				var logged = db;
#endif

				// linear subtree "/Foo/Bar/Baz"
				await directory.CreateOrOpenAsync(logged, new[] { "Foo", "Bar", "Baz" });
				// flat subtree "/numbers/0" to "/numbers/9"
				for (int i = 0; i < 10; i++) await directory.CreateOrOpenAsync(logged, new[] { "numbers", i.ToString() });
#if DEBUG
				await TestHelpers.DumpSubspace(db, location);
#endif

				var subdirs = await directory.ListAsync(logged, new[] { "Foo" });
				Assert.That(subdirs, Is.Not.Null);
				Assert.That(subdirs.Count, Is.EqualTo(1));
				Assert.That(subdirs[0], Is.EqualTo("Bar"));

				subdirs = await directory.ListAsync(logged, new[] { "Foo", "Bar" });
				Assert.That(subdirs, Is.Not.Null);
				Assert.That(subdirs.Count, Is.EqualTo(1));
				Assert.That(subdirs[0], Is.EqualTo("Baz"));

				subdirs = await directory.ListAsync(logged, new[] { "Foo", "Bar", "Baz" });
				Assert.That(subdirs, Is.Not.Null);
				Assert.That(subdirs.Count, Is.EqualTo(0));

				subdirs = await directory.ListAsync(logged, new[] { "numbers" });
				Assert.That(subdirs, Is.Not.Null);
				Assert.That(subdirs.Count, Is.EqualTo(10));
				Assert.That(subdirs, Is.EquivalentTo(Enumerable.Range(0, 10).Select(x => x.ToString()).ToList()));

#if DEBUG
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
			using (var db = await TestHelpers.OpenTestDatabaseAsync())
			{
				// we will put everything under a custom namespace
				var location = db.Partition("DL");
				await db.ClearRangeAsync(location);
				var directory = FdbDirectoryLayer.Create(location);

				// insert letters in random order
				var rnd = new Random();
				var letters = Enumerable.Range(65, 10).Select(x => new string((char)x, 1)).ToList();
				while(letters.Count > 0)
				{
					var i = rnd.Next(letters.Count);
					var s = letters[i];
					letters.RemoveAt(i);
					await directory.CreateOrOpenAsync(db, new [] { "letters", s });
				}

#if DEBUG
				await TestHelpers.DumpSubspace(db, location);
#endif

				// they should sorted when listed
				var subdirs = await directory.ListAsync(db, "letters");
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

			using (var db = await TestHelpers.OpenTestDatabaseAsync())
			{
				// we will put everything under a custom namespace
				var location = db.Partition("DL");
				await db.ClearRangeAsync(location);

#if DEBUG
				var list = new List<FdbTransactionLog>();
				var logged = new FdbLoggedDatabase(db, false, false, (tr) => { list.Add(tr.Log); });
#else
				var logged = db;
#endif

				// put the nodes under (..,"DL",\xFE,) and the content under (..,"DL",)
				var directory = FdbDirectoryLayer.Create(location);

				// create a folder at ('Foo',)
				var original = await directory.CreateOrOpenAsync(logged, "Foo");
#if DEBUG
				await TestHelpers.DumpSubspace(db, location);
#endif
				Assert.That(original, Is.Not.Null);
				Assert.That(original.Path, Is.EqualTo(new[] { "Foo" }));

				// rename/move it as ('Bar',)
				var renamed = await original.MoveToAsync(logged, new[] { "Bar" });
#if DEBUG
				await TestHelpers.DumpSubspace(db, location);
#endif
				Assert.That(renamed, Is.Not.Null);
				Assert.That(renamed.Path, Is.EqualTo(new [] { "Bar" }));
				Assert.That(renamed.Key, Is.EqualTo(original.Key));

				// opening the old path should fail
				Assert.Throws<InvalidOperationException>(async () => await directory.OpenAsync(logged, "Foo"));

				// opening the new path should succeed
				var folder = await directory.OpenAsync(logged, "Bar");
				Assert.That(folder, Is.Not.Null);
				Assert.That(folder.Path, Is.EqualTo(renamed.Path));
				Assert.That(folder.Key, Is.EqualTo(renamed.Key));

				// moving the folder under itself should fail
				Assert.Throws<InvalidOperationException>(async () => await folder.MoveToAsync(logged, new[] { "Bar", "Baz" }));
#if DEBUG
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
			using (var db = await TestHelpers.OpenTestDatabaseAsync())
			{
				var location = db.Partition("DL");
				await db.ClearRangeAsync(location);
				var directory = FdbDirectoryLayer.Create(location);

#if DEBUG
				var list = new List<FdbTransactionLog>();
				var logged = new FdbLoggedDatabase(db, false, false, (tr) => { list.Add(tr.Log); });
#else
				var logged = db;
#endif

				// RemoveAsync

				string[] path = new[] { "CrashTestDummy" };
				await directory.CreateAsync(logged, path);
#if DEBUG
				await TestHelpers.DumpSubspace(db, location);
#endif

				// removing an existing folder should succeeed
				await directory.RemoveAsync(logged, path);
#if DEBUG
				await TestHelpers.DumpSubspace(db, location);
#endif
				//TODO: call ExistsAsync(...) once it is implemented!

				// Removing it a second time should fail
				Assert.Throws<InvalidOperationException>(async () => await directory.RemoveAsync(logged, path), "Removing a non-existent directory should fail");

				// TryRemoveAsync

				await directory.CreateAsync(logged, path);

				// attempting to remove a folder should return true
				bool res = await directory.TryRemoveAsync(logged, path);
				Assert.That(res, Is.True);

				// further attempts should return false
				res = await directory.TryRemoveAsync(logged, path);
				Assert.That(res, Is.False);

				// Corner Cases

				// removing the root folder is not allowed (too dangerous)
				Assert.Throws<InvalidOperationException>(async () => await directory.RemoveAsync(logged, new string[0]), "Attempting to remove the root directory should fail");

#if DEBUG
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
			using (var db = await TestHelpers.OpenTestDatabaseAsync())
			{
				var location = db.Partition("DL");
				await db.ClearRangeAsync(location);
				var directory = FdbDirectoryLayer.Create(location);

#if DEBUG
				var list = new List<FdbTransactionLog>();
				var logged = new FdbLoggedDatabase(db, false, false, (tr) => { list.Add(tr.Log); });
#else
				var logged = db;
#endif

				var folder = await directory.CreateAsync(logged, "Test", layer: Slice.FromString("foo"));
#if DEBUG
				await TestHelpers.DumpSubspace(db, location);
#endif
				Assert.That(folder, Is.Not.Null);
				Assert.That(folder.Layer.ToUnicode(), Is.EqualTo("foo"));

				// change the layer to 'bar'
				var folder2 = await folder.ChangeLayerAsync(logged, Slice.FromString("bar"));
#if DEBUG
				await TestHelpers.DumpSubspace(db, location);
#endif
				Assert.That(folder2, Is.Not.Null);
				Assert.That(folder2.Layer.ToUnicode(), Is.EqualTo("bar"));
				Assert.That(folder2.Path, Is.EqualTo(FdbTuple.Create("Test")));
				Assert.That(folder2.Key, Is.EqualTo(folder.Key));

				// opening the directory with the new layer should succeed
				var folder3 = await directory.OpenAsync(logged, "Test", layer: Slice.FromString("bar"));
				Assert.That(folder3, Is.Not.Null);

				// opening the directory with the old layer should fail
				Assert.Throws<InvalidOperationException>(async () => await directory.OpenAsync(logged, "Test", layer: Slice.FromString("foo")));

#if DEBUG
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
			using (var db = await TestHelpers.OpenTestDatabaseAsync())
			{
				var location = db.Partition("DL");
				await db.ClearRangeAsync(location);

				var directory = FdbDirectoryLayer.Create(location);
				Console.WriteLine(directory);

				var partition = await directory.CreateAsync(db, "Foo", Slice.FromAscii("partition"));
				Console.WriteLine(partition);
				Assert.That(partition, Is.InstanceOf<FdbDirectoryPartition>());
				Assert.That(partition.Layer, Is.EqualTo(Slice.FromAscii("partition")));
				Assert.That(partition.Path, Is.EqualTo(new [] { "Foo" }), "Partition's path should be absolute");
				Assert.That(partition.DirectoryLayer, Is.Not.SameAs(directory), "Partitions should have their own DL");
				Assert.That(partition.DirectoryLayer.ContentSubspace.Key, Is.EqualTo(partition.Key), "Partition's content should be under the partition's prefix");
				Assert.That(partition.DirectoryLayer.NodeSubspace.Key, Is.EqualTo(partition.Key + FdbKey.Directory), "Partition's nodes should be under the partition's prefix");

				var bar = await partition.CreateAsync(db, "Bar");
				Console.WriteLine(bar);
				Assert.That(bar, Is.InstanceOf<FdbDirectorySubspace>());
				Assert.That(bar.Path, Is.EqualTo(new [] { "Foo", "Bar" }), "Path of directories under a partition should be absolute");
				Assert.That(bar.Key, Is.Not.EqualTo(partition.Key), "{0} should be located under {1}", bar, partition);
				Assert.That(bar.Key.StartsWith(partition.Key), Is.True, "{0} should be located under {1}", bar, partition);

				var baz = await partition.CreateAsync(db, "Baz");
				Console.WriteLine(baz);
				Assert.That(baz, Is.InstanceOf<FdbDirectorySubspace>());
				Assert.That(baz.Path, Is.EqualTo(new [] { "Foo", "Baz" }), "Path of directories under a partition should be absolute");
				Assert.That(baz.Key, Is.Not.EqualTo(partition.Key), "{0} should be located under {1}", baz, partition);
				Assert.That(baz.Key.StartsWith(partition.Key), Is.True, "{0} should be located under {1}", baz, partition);

				// create a 'Bar' under the root partition
				var other = await directory.CreateAsync(db, "Bar");
				Console.WriteLine(other);
				Assert.That(other.Path, Is.EqualTo(new string[] { "Bar" }));
				Assert.That(other.DirectoryLayer, Is.SameAs(directory));

				// Rename 'Bar' to 'BarBar'
				var bar2 = await bar.MoveToAsync(db, new [] { "Foo", "BarBar" });
				Console.WriteLine(bar2);
				Assert.That(bar2, Is.InstanceOf<FdbDirectorySubspace>());
				Assert.That(bar2, Is.Not.SameAs(bar));
				Assert.That(bar2.Key, Is.EqualTo(bar.Key));
				Assert.That(bar2.Path, Is.EqualTo(new[] { "Foo", "BarBar" }));
				Assert.That(bar2.DirectoryLayer, Is.SameAs(bar.DirectoryLayer));

				// Attempting to move 'Baz' to another partition should fail
				Assert.That(async () => await baz.MoveToAsync(db, new[] { "Baz" }), Throws.InstanceOf<InvalidOperationException>());

				// create a subpart
				var foo2 = await partition.CreateAsync(db, "Foo2", Slice.FromString("partition"));
				Assert.That(foo2, Is.InstanceOf<FdbDirectoryPartition>());
				// Attempting to move 'Baz' to a sub-partition should fail
				Assert.That(async () => await baz.MoveToAsync(db, new[] { "Foo", "Foo2", "Baz" }), Throws.InstanceOf<InvalidOperationException>());

				var baz2 = await foo2.CreateAsync(db, "Baz");
				Console.WriteLine(baz2);
				var movedBaz2 = await baz2.MoveToAsync(db, new [] { "Foo", "Foo2", "BazBaz" });
				Console.WriteLine(movedBaz2);
			}
		}


		[Test]
		public async Task Test_Directory_Methods_Should_Fail_With_Empty_Paths()
		{
			using (var db = await TestHelpers.OpenTestDatabaseAsync())
			{
				var location = db.Partition("DL");
				await db.ClearRangeAsync(location);
				var directory = FdbDirectoryLayer.Create(location);

				// CreateOrOpen
				Assert.Throws<ArgumentNullException>(async () => await directory.CreateOrOpenAsync(db, default(string[])));
				Assert.Throws<InvalidOperationException>(async () => await directory.CreateOrOpenAsync(db, new string[0]));
				Assert.Throws<InvalidOperationException>(async () => await directory.CreateOrOpenAsync(db, new string[] { "" }));
				Assert.Throws<InvalidOperationException>(async () => await directory.CreateOrOpenAsync(db, new string[] { " " }));
				Assert.Throws<InvalidOperationException>(async () => await directory.CreateOrOpenAsync(db, new string[] { "Foo", " ", "Bar" }));
				Assert.Throws<ArgumentNullException>(async () => await directory.CreateOrOpenAsync(db, default(string)));
				Assert.Throws<InvalidOperationException>(async () => await directory.CreateOrOpenAsync(db, String.Empty));
				Assert.Throws<InvalidOperationException>(async () => await directory.CreateOrOpenAsync(db, " "));

				// Create
				Assert.Throws<ArgumentNullException>(async () => await directory.CreateAsync(db, default(string[])));
				Assert.Throws<InvalidOperationException>(async () => await directory.CreateAsync(db, new string[0]));
				Assert.Throws<InvalidOperationException>(async () => await directory.CreateAsync(db, new string[] { "" }));
				Assert.Throws<InvalidOperationException>(async () => await directory.CreateAsync(db, new string[] { " " }));
				Assert.Throws<InvalidOperationException>(async () => await directory.CreateAsync(db, new string[] { "Foo", "", "Bar" }));
				Assert.Throws<ArgumentNullException>(async () => await directory.CreateAsync(db, default(string)));
				Assert.Throws<InvalidOperationException>(async () => await directory.CreateAsync(db, String.Empty));
				Assert.Throws<InvalidOperationException>(async () => await directory.CreateAsync(db, " "));

				// Open
				Assert.Throws<ArgumentNullException>(async () => await directory.OpenAsync(db, default(string[])));
				Assert.Throws<InvalidOperationException>(async () => await directory.OpenAsync(db, new string[0]));
				Assert.Throws<InvalidOperationException>(async () => await directory.OpenAsync(db, new string[] { "" }));
				Assert.Throws<InvalidOperationException>(async () => await directory.OpenAsync(db, new string[] { " " }));
				Assert.Throws<InvalidOperationException>(async () => await directory.OpenAsync(db, new string[] { "Foo", "", "Bar" }));
				Assert.Throws<ArgumentNullException>(async () => await directory.OpenAsync(db, default(string)));
				Assert.Throws<InvalidOperationException>(async () => await directory.OpenAsync(db, String.Empty));
				Assert.Throws<InvalidOperationException>(async () => await directory.OpenAsync(db, " "));

				// Move
				Assert.Throws<ArgumentNullException>(async () => await directory.MoveAsync(db, default(string[]), new[] { "foo" }));
				Assert.Throws<ArgumentNullException>(async () => await directory.MoveAsync(db, new[] { "foo" }, default(string[])));
				Assert.Throws<InvalidOperationException>(async () => await directory.MoveAsync(db, new string[0], new[] { "foo" }));
				Assert.Throws<InvalidOperationException>(async () => await directory.MoveAsync(db, new[] { "foo" }, new string[0]));

				// Remove
				Assert.Throws<ArgumentNullException>(async () => await directory.RemoveAsync(db, default(string[])));
				Assert.Throws<InvalidOperationException>(async () => await directory.RemoveAsync(db, new string[0]));
				Assert.Throws<InvalidOperationException>(async () => await directory.RemoveAsync(db, new string[] { "" }));
				Assert.Throws<InvalidOperationException>(async () => await directory.RemoveAsync(db, new string[] { " " }));
				Assert.Throws<InvalidOperationException>(async () => await directory.RemoveAsync(db, new string[] { "Foo", " ", "Bar" }));
				Assert.Throws<ArgumentNullException>(async () => await directory.RemoveAsync(db, default(string)));
				Assert.Throws<InvalidOperationException>(async () => await directory.RemoveAsync(db, String.Empty));
				Assert.Throws<InvalidOperationException>(async () => await directory.RemoveAsync(db, " "));

				// List
				Assert.Throws<ArgumentNullException>(async () => await directory.ListAsync(db, default(string[])));
				Assert.Throws<InvalidOperationException>(async () => await directory.ListAsync(db, new string[] { "" }));
				Assert.Throws<InvalidOperationException>(async () => await directory.ListAsync(db, new string[] { " " }));
				Assert.Throws<InvalidOperationException>(async () => await directory.ListAsync(db, new string[] { "Foo", "", "Bar" }));
				Assert.Throws<ArgumentNullException>(async () => await directory.ListAsync(db, default(string)));
				Assert.Throws<InvalidOperationException>(async () => await directory.ListAsync(db, String.Empty));
				Assert.Throws<InvalidOperationException>(async () => await directory.ListAsync(db, " "));

			}
		}
	}

}
