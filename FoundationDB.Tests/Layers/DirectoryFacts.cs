#region BSD License
/* Copyright (c) 2013-2018, Doxense SAS
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

// ReSharper disable AssignNullToNotNullAttribute
#define ENABLE_LOGGING

namespace FoundationDB.Client.Tests
{
	using System;
	using System.Collections.Generic;
	using System.Diagnostics;
	using System.Linq;
	using System.Threading.Tasks;
	using Doxense.Collections.Tuples;
	using FoundationDB.Client;
	using FoundationDB.Filters.Logging;
	using FoundationDB.Layers.Allocators;
	using NUnit.Framework;
	using NUnit.Framework.Constraints;

	[TestFixture]
	public class DirectoryLayerFacts : FdbTest
	{

		[Test]
		public async Task Test_Allocator()
		{
			//FoundationDB.Client.Utils.Logging.SetLevel(System.Diagnostics.SourceLevels.Verbose);

			using (var db = await OpenTestDatabaseAsync())
			{
				var location = db.Root.ByKey(Slice.FromString("hca"));
				await CleanLocation(db, location);

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
				using (var tr = await logged.BeginTransactionAsync(this.Cancellation))
				{
					var state = await hpa.Resolve(tr);
					id = await state.AllocateAsync(tr);
					await tr.CommitAsync();
				}
				ids.Add(id);

				await DumpSubspace(db, location);

				// allocate a batch of new ids
				for (int i = 0; i < 100; i++)
				{
					using (var tr = await logged.BeginTransactionAsync(this.Cancellation))
					{
						var state = await hpa.Resolve(tr);
						id = await state.AllocateAsync(tr);
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
					Log(log.GetTimingsReport(true)); 
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
				var location = db.Root.ByKey("DL");
				await CleanLocation(db, location);

#if ENABLE_LOGGING
				var logged = new FdbLoggedDatabase(db, false, false, (tr) => { Log(tr.Log.GetTimingsReport(true)); });
#else
				var logged = db;
#endif

				// put the nodes under (..,"DL",\xFE,) and the content under (..,"DL",)
				var directory = FdbDirectoryLayer.Create(location);

				Assert.That(directory.Content, Is.Not.Null);
				Assert.That(directory.Content, Is.EqualTo(location));

				// first call should create a new subspace (with a random prefix)
				FdbDirectorySubspace foo;

				using (var tr = await logged.BeginTransactionAsync(this.Cancellation))
				{
					foo = await directory.CreateOrOpenAsync(tr, new[] { "Foo" });
					await tr.CommitAsync();
				}

#if DEBUG
				Log("After creating 'Foo':");
				await DumpSubspace(db, location);
#endif

				Assert.That(foo, Is.Not.Null);
				Assert.That(foo.FullName, Is.EqualTo("Foo"), "foo.FullName");
				Assert.That(foo.Path, Is.EqualTo(new[] { "Foo" }), "foo.Path");
				Assert.That(foo.Layer, Is.EqualTo(Slice.Empty), "foo.Layer");
				Assert.That(foo.DirectoryLayer, Is.SameAs(directory), "foo.DirectoryLayer");
				Assert.That(foo.Context, Is.Not.Null, ".Context");

				// second call should return the same subspace

				var foo2 = await logged.ReadAsync(tr => directory.OpenAsync(tr, new[] { "Foo" }), this.Cancellation);
#if DEBUG
				Log("After opening 'Foo':");
				await DumpSubspace(db, location);
#endif
				Assert.That(foo2, Is.Not.Null);
				Assert.That(foo2.FullName, Is.EqualTo("Foo"), "foo2.FullName");
				Assert.That(foo2.Path, Is.EqualTo(new[] { "Foo" }), "foo2.Path");
				Assert.That(foo2.Layer, Is.EqualTo(Slice.Empty), "foo2.Layer");
				Assert.That(foo2.DirectoryLayer, Is.SameAs(directory), "foo2.DirectoryLayer");
				Assert.That(foo2.Context, Is.Not.Null, "foo2.Context");

				Assert.That(foo2.GetPrefix(), Is.EqualTo(foo.GetPrefix()), "Second call to CreateOrOpen should return the same subspace");
			}
		}

		[Test]
		public async Task Test_CreateOrOpen_With_Layer()
		{
			using (var db = await OpenTestDatabaseAsync())
			{
				// we will put everything under a custom namespace
				var location = db.Root.ByKey("DL");
				await CleanLocation(db, location);

#if ENABLE_LOGGING
				var list = new List<FdbTransactionLog>();
				var logged = new FdbLoggedDatabase(db, false, false, (tr) => { list.Add(tr.Log); });
#else
				var logged = db;
#endif

				// put the nodes under (..,"DL",\xFE,) and the content under (..,"DL",)
				var directory = FdbDirectoryLayer.Create(location);

				Assert.That(directory.Content, Is.Not.Null);
				Assert.That(directory.Content, Is.EqualTo(location));

				// first call should create a new subspace (with a random prefix)
				var foo = await logged.ReadWriteAsync(tr => directory.CreateOrOpenAsync(tr, new[] { "Foo" }, Slice.FromString("AcmeLayer")), this.Cancellation);
#if DEBUG
				await DumpSubspace(db, location);
#endif

				Assert.That(foo, Is.Not.Null);
				Assert.That(foo.FullName, Is.EqualTo("Foo"));
				Assert.That(foo.Path, Is.EqualTo(new[] { "Foo" }));
				Assert.That(foo.Layer.ToUnicode(), Is.EqualTo("AcmeLayer"));
				Assert.That(foo.DirectoryLayer, Is.SameAs(directory));

				// second call should return the same subspace
				var foo2 = await logged.ReadAsync(tr => directory.OpenAsync(tr, new[] { "Foo" }, Slice.FromString("AcmeLayer")), this.Cancellation);
				Assert.That(foo2, Is.Not.Null);
				Assert.That(foo2.FullName, Is.EqualTo("Foo"));
				Assert.That(foo2.Path, Is.EqualTo(new[] { "Foo" }));
				Assert.That(foo2.Layer.ToUnicode(), Is.EqualTo("AcmeLayer"));
				Assert.That(foo2.DirectoryLayer, Is.SameAs(directory));
				Assert.That(foo2.GetPrefix(), Is.EqualTo(foo.GetPrefix()), "Second call to CreateOrOpen should return the same subspace");

				// opening it with wrong layer id should fail
				Assert.That(async () => await logged.ReadAsync(tr => directory.OpenAsync(tr, new[] { "Foo" }, Slice.FromString("OtherLayer")), this.Cancellation), Throws.InstanceOf<InvalidOperationException>(), "Opening with invalid layer id should fail");

				// opening without specifying a layer should disable the layer check
				var foo3 = await logged.ReadAsync(tr => directory.OpenAsync(tr, "Foo", layer: Slice.Nil), this.Cancellation);
				Assert.That(foo3, Is.Not.Null);
				Assert.That(foo3.Layer.ToUnicode(), Is.EqualTo("AcmeLayer"));

				// CheckLayer with the correct value should pass
				Assert.DoesNotThrow(() => foo3.CheckLayer(Slice.FromString("AcmeLayer")), "CheckLayer should not throw if the layer id is correct");

				// CheckLayer with the incorrect value should fail
				Assert.That(() => foo3.CheckLayer(Slice.FromString("OtherLayer")), Throws.InstanceOf<InvalidOperationException>(), "CheckLayer should throw if the layer id is not correct");

				// CheckLayer with empty string should do nothing
				foo3.CheckLayer(Slice.Empty);
				foo3.CheckLayer(Slice.Nil);

#if ENABLE_LOGGING
				foreach (var log in list)
				{
					Log(log.GetTimingsReport(true));
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
				var location = db.Root.ByKey("DL");
				await CleanLocation(db, location);

#if ENABLE_LOGGING
				var list = new List<FdbTransactionLog>();
				var logged = new FdbLoggedDatabase(db, false, false, (tr) => { list.Add(tr.Log); });
#else
				var logged = db;
#endif

				// put the nodes under (..,"DL",\xFE,) and the content under (..,"DL",)
				var directory = FdbDirectoryLayer.Create(location);

				FdbDirectorySubspace folder;
				using (var tr = await logged.BeginTransactionAsync(this.Cancellation))
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
				var foo = await logged.ReadAsync(tr => directory.OpenAsync(tr, new[] { "Foo" }), this.Cancellation);
				var bar = await logged.ReadAsync(tr => directory.OpenAsync(tr, new[] { "Foo", "Bar" }), this.Cancellation);
				Assert.That(foo, Is.Not.Null);
				Assert.That(bar, Is.Not.Null);

#if ENABLE_LOGGING
				foreach (var log in list)
				{
					Log(log.GetTimingsReport(true));
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
				var location = db.Root.ByKey("DL");
				await CleanLocation(db, location);

				var directory = FdbDirectoryLayer.Create(location);

#if ENABLE_LOGGING
				var list = new List<FdbTransactionLog>();
				var logged = new FdbLoggedDatabase(db, false, false, (tr) => { list.Add(tr.Log); });
#else
				var logged = db;
#endif

				// linear subtree "/Foo/Bar/Baz"
				await logged.ReadWriteAsync(tr => directory.CreateOrOpenAsync(tr, new[] { "Foo", "Bar", "Baz" }), this.Cancellation);
				// flat subtree "/numbers/0" to "/numbers/9"
				await logged.WriteAsync(async tr =>
				{
					for (int i = 0; i < 10; i++) await directory.CreateOrOpenAsync(tr, new[] { "numbers", i.ToString() });
				}, this.Cancellation);
#if DEBUG
				await DumpSubspace(db, location);
#endif

				var subdirs = await logged.ReadAsync(tr => directory.ListAsync(tr, new[] { "Foo" }), this.Cancellation);
				Assert.That(subdirs, Is.Not.Null);
				Assert.That(subdirs.Count, Is.EqualTo(1));
				Assert.That(subdirs[0], Is.EqualTo("Bar"));

				subdirs = await logged.ReadAsync(tr => directory.ListAsync(tr, new[] { "Foo", "Bar" }), this.Cancellation);
				Assert.That(subdirs, Is.Not.Null);
				Assert.That(subdirs.Count, Is.EqualTo(1));
				Assert.That(subdirs[0], Is.EqualTo("Baz"));

				subdirs = await logged.ReadAsync(tr => directory.ListAsync(tr, new[] { "Foo", "Bar", "Baz" }), this.Cancellation);
				Assert.That(subdirs, Is.Not.Null);
				Assert.That(subdirs.Count, Is.Zero);

				subdirs = await logged.ReadAsync(tr => directory.ListAsync(tr, new[] { "numbers" }), this.Cancellation);
				Assert.That(subdirs, Is.Not.Null);
				Assert.That(subdirs.Count, Is.EqualTo(10));
				Assert.That(subdirs, Is.EquivalentTo(Enumerable.Range(0, 10).Select(x => x.ToString()).ToList()));

#if ENABLE_LOGGING
				foreach (var log in list)
				{
					Log(log.GetTimingsReport(true));
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
				var location = db.Root.ByKey("DL");
				await CleanLocation(db, location);

				var directory = FdbDirectoryLayer.Create(location);

				// insert letters in random order
				await db.WriteAsync(async tr =>
				{
					var rnd = new Random();
					var letters = Enumerable.Range(65, 10).Select(x => new string((char) x, 1)).ToList();
					while (letters.Count > 0)
					{
						var i = rnd.Next(letters.Count);
						var s = letters[i];
						letters.RemoveAt(i);
						await directory.CreateOrOpenAsync(tr, new[] { "letters", s });
					}
				}, this.Cancellation);

#if DEBUG
				await DumpSubspace(db, location);
#endif

				// they should sorted when listed
				await db.ReadAsync(async tr =>
				{
					var subdirs = await directory.ListAsync(tr, "letters");
					Assert.That(subdirs, Is.Not.Null);
					Assert.That(subdirs.Count, Is.EqualTo(10));
					for (int i = 0; i < subdirs.Count; i++)
					{
						Assert.That(subdirs[i], Is.EqualTo(new string((char) (65 + i), 1)));
					}

					return default(object);
				}, this.Cancellation);

			}
		}

		[Test]
		public async Task Test_Move_Folder()
		{
			// Create a folder ("foo", "bar", "baz") and ensure that all the parent folder are also creating and linked properly

			using (var db = await OpenTestDatabaseAsync())
			{
				// we will put everything under a custom namespace
				var location = db.Root.ByKey("DL");
				await CleanLocation(db, location);

#if ENABLE_LOGGING
				var list = new List<FdbTransactionLog>();
				var logged = new FdbLoggedDatabase(db, false, false, (tr) => { list.Add(tr.Log); });
#else
				var logged = db;
#endif

				// put the nodes under (..,"DL",\xFE,) and the content under (..,"DL",)
				var directory = FdbDirectoryLayer.Create(location);

				// create a folder at ('Foo',)
				Slice originalPrefix = await logged.ReadWriteAsync(async tr =>
				{
					var original = await directory.CreateOrOpenAsync(tr, "Foo");
#if DEBUG
					await DumpSubspace(db, location);
#endif
					Assert.That(original, Is.Not.Null);
					Assert.That(original.FullName, Is.EqualTo("Foo"));
					Assert.That(original.Path, Is.EqualTo(new[] { "Foo" }));

					// rename/move it as ('Bar',)
					var renamed = await original.MoveToAsync(tr, new[] { "Bar" });
#if DEBUG
					await DumpSubspace(db, location);
#endif
					Assert.That(renamed, Is.Not.Null);
					Assert.That(renamed.FullName, Is.EqualTo("Bar"));
					Assert.That(renamed.Path, Is.EqualTo(FdbDirectoryPath.Combine("Bar")));
					Assert.That(renamed.GetPrefix(), Is.EqualTo(original.GetPrefix()));

					return original.GetPrefix();
				}, this.Cancellation);

				// opening the old path should fail
				Assert.That(async () => await logged.ReadAsync(tr => directory.OpenAsync(tr, "Foo"), this.Cancellation), Throws.InstanceOf<InvalidOperationException>());

				// opening the new path should succeed
				await logged.WriteAsync(async tr =>
				{
					var folder = await directory.OpenAsync(tr, "Bar");
					Assert.That(folder, Is.Not.Null);
					Assert.That(folder.FullName, Is.EqualTo("Bar"));
					Assert.That(folder.Path, Is.EqualTo(FdbDirectoryPath.Combine("Bar")));
					Assert.That(folder.GetPrefix(), Is.EqualTo(originalPrefix));

					// moving the folder under itself should fail
					Assert.That(async () => await folder.MoveToAsync(tr, new[] { "Bar", "Baz" }), Throws.InstanceOf<InvalidOperationException>());
				}, this.Cancellation);

#if ENABLE_LOGGING
				foreach (var log in list)
				{
					Log(log.GetTimingsReport(true));
				}
#endif
			}
		}

		[Test]
		public async Task Test_Remove_Folder()
		{
			using (var db = await OpenTestDatabaseAsync())
			{
				var location = db.Root.ByKey("DL");
				await CleanLocation(db, location);

				var directory = FdbDirectoryLayer.Create(location);

#if ENABLE_LOGGING
				var list = new List<FdbTransactionLog>();
				var logged = new FdbLoggedDatabase(db, false, false, (tr) => { list.Add(tr.Log); });
#else
				var logged = db;
#endif

				// RemoveAsync

				string[] path = new[] { "CrashTestDummy" };
				await logged.ReadWriteAsync(tr => directory.CreateAsync(tr, path), this.Cancellation);
#if DEBUG
				await DumpSubspace(db, location);
#endif

				// removing an existing folder should succeed
				await logged.WriteAsync(tr => directory.RemoveAsync(tr, path), this.Cancellation);
#if DEBUG
				await DumpSubspace(db, location);
#endif
				//TODO: call ExistsAsync(...) once it is implemented!

				// Removing it a second time should fail
				Assert.That(
					async () => await logged.WriteAsync(tr => directory.RemoveAsync(tr, path), this.Cancellation), 
					Throws.InstanceOf<InvalidOperationException>(), 
					"Removing a non-existent directory should fail"
				);

				// TryRemoveAsync

				await logged.ReadWriteAsync(tr => directory.CreateAsync(tr, path), this.Cancellation);

				// attempting to remove a folder should return true
				bool res = await logged.ReadWriteAsync(tr => directory.TryRemoveAsync(tr, path), this.Cancellation);
				Assert.That(res, Is.True);

				// further attempts should return false
				res = await logged.ReadWriteAsync(tr => directory.TryRemoveAsync(tr, path), this.Cancellation);
				Assert.That(res, Is.False);

				// Corner Cases

				// removing the root folder is not allowed (too dangerous)
				Assert.That(async () => await logged.WriteAsync(tr => directory.RemoveAsync(tr, FdbDirectoryPath.Empty), this.Cancellation), Throws.InstanceOf<InvalidOperationException>(), "Attempting to remove the root directory should fail");

#if ENABLE_LOGGING
				foreach (var log in list)
				{
					Log(log.GetTimingsReport(true));
				}
#endif
			}
		}

		[Test]
		public async Task Test_Can_Change_Layer_Of_Existing_Directory()
		{
			using (var db = await OpenTestDatabaseAsync())
			{
				var location = db.Root.ByKey("DL");
				await CleanLocation(db, location);

				var directory = FdbDirectoryLayer.Create(location);

#if ENABLE_LOGGING
				var logged = db.Logged((tr) => Log(tr.Log.GetTimingsReport(true)));
#else
				var logged = db;
#endif

				await logged.WriteAsync(async tr =>
				{
					var folder = await directory.CreateAsync(tr, "Test", layer: Slice.FromString("foo"));
#if DEBUG
					await DumpSubspace(db, location);
#endif
					Assert.That(folder, Is.Not.Null);
					Assert.That(folder.Layer.ToUnicode(), Is.EqualTo("foo"));
	
					var folder2 = await folder.ChangeLayerAsync(tr, Slice.FromString("bar"));
#if DEBUG
					await DumpSubspace(db, location);
#endif
					Assert.That(folder2, Is.Not.Null);
					Assert.That(folder2.Layer.ToUnicode(), Is.EqualTo("bar"));
					Assert.That(folder2.FullName, Is.EqualTo("Test"));
					Assert.That(folder2.Path, Is.EqualTo(new[] { "Test" }));
					Assert.That(folder2.GetPrefix(), Is.EqualTo(folder.GetPrefix()));
				}, this.Cancellation);

				// opening the directory with the new layer should succeed
				await logged.ReadAsync(async tr =>
				{
					var folder3 = await directory.OpenAsync(tr, "Test", layer: Slice.FromString("bar"));
					Assert.That(folder3, Is.Not.Null);
					return default(object);
				}, this.Cancellation);

				// opening the directory with the old layer should fail
				Assert.That(
					async () => await logged.ReadAsync(tr => directory.OpenAsync(tr, "Test", Slice.FromString("foo")), this.Cancellation),
					Throws.InstanceOf<InvalidOperationException>()
				);

			}
		}

		[Test]
		public async Task Test_Directory_Partitions()
		{
			using (var db = await OpenTestDatabaseAsync())
			{
				var location = db.Root.ByKey("DL");
				await CleanLocation(db, location);

#if ENABLE_LOGGING
				var logged = db.Logged((tr) => Log(tr.Log.GetTimingsReport(true)));
#else
				var logged = db;
#endif

				var directory = FdbDirectoryLayer.Create(location);
				Dump(directory);

				await logged.WriteAsync(async tr =>
				{
					Log("Creating partition /Foo$ ...");
					var partition = await directory.CreateAsync(tr, "Foo$", Slice.FromStringAscii("partition"));
					Dump(partition);
					await DumpSubspace(tr, location);
					// we can't get the partition key directory (because it's a root directory) so we need to cheat a little bit
					var partitionKey = partition.Copy().GetPrefix();
					Log($"> Created with prefix: {partitionKey:K}");

					Assert.That(partition, Is.InstanceOf<FdbDirectoryPartition>());
					Assert.That(partition.Layer, Is.EqualTo(Slice.FromStringAscii("partition")));
					Assert.That(partition.FullName, Is.EqualTo("Foo$"));
					Assert.That(partition.Path, Is.EqualTo(new[] { "Foo$" }), "Partition's path should be absolute");
					Assert.That(partition.DirectoryLayer, Is.SameAs(directory), "Partitions share the same DL");
					//Assert.That((await partition.DirectoryLayer.Content.Resolve(tr)).GetPrefix(), Is.EqualTo(partitionKey), "Partition's content should be under the partition's prefix");
					//Assert.That((await partition.DirectoryLayer.Nodes.Resolve(tr)).GetPrefix(), Is.EqualTo(partitionKey + FdbKey.Directory), "Partition's nodes should be under the partition's prefix");

					Log("Creating sub-directory Bar under partition Foo$ ...");
					var bar = await partition.CreateAsync(tr, "Bar");
					Dump(bar);
					await DumpSubspace(tr, location);
					Assert.That(bar, Is.InstanceOf<FdbDirectorySubspace>());
					Assert.That(bar.Path.ToString(), Is.EqualTo("Foo$/Bar"), "Path of directories under a partition should be absolute");
					Assert.That(bar.GetPrefix(), Is.Not.EqualTo(partitionKey), "{0} should be located under {1}", bar, partition);
					Assert.That(bar.GetPrefix().StartsWith(partitionKey), Is.True, "{0} should be located under {1}", bar, partition);

					Log("Creating sub-directory /Foo$/Baz starting from the root...");
					var baz = await directory.CreateAsync(tr, FdbDirectoryPath.Combine("Foo$", "Baz"));
					Dump(baz);
					await DumpSubspace(tr, location);
					Assert.That(baz, Is.InstanceOf<FdbDirectorySubspace>());
					Assert.That(baz.FullName, Is.EqualTo("Foo$/Baz"));
					Assert.That(baz.Path, Is.EqualTo(new[] { "Foo$", "Baz" }), "Path of directories under a partition should be absolute");
					Assert.That(baz.GetPrefix(), Is.Not.EqualTo(partitionKey), "{0} should be located under {1}", baz, partition);
					Assert.That(baz.GetPrefix().StartsWith(partitionKey), Is.True, "{0} should be located under {1}", baz, partition);

					// Rename 'Bar' to 'BarBar'
					Log("Renaming /Foo$/Bar to /Foo$/BarBar...");
					var bar2 = await bar.MoveToAsync(tr, FdbDirectoryPath.Combine("Foo$", "BarBar"));
					Dump(bar2);
					await DumpSubspace(tr, location);
					Assert.That(bar2, Is.InstanceOf<FdbDirectorySubspace>());
					Assert.That(bar2, Is.Not.SameAs(bar));
					Assert.That(bar2.GetPrefix(), Is.EqualTo(bar.GetPrefix()));
					Assert.That(bar2.FullName, Is.EqualTo("Foo$/BarBar"));
					Assert.That(bar2.Path, Is.EqualTo(new[] { "Foo$", "BarBar" }));
					Assert.That(bar2.DirectoryLayer, Is.SameAs(bar.DirectoryLayer));
				}, this.Cancellation);
			}
		}

		[Test]
		public async Task Test_Directory_Cannot_Move_To_Another_Partition()
		{
			using (var db = await OpenTestDatabaseAsync())
			{
				var location = db.Root.ByKey("DL");
				await CleanLocation(db, location);

#if ENABLE_LOGGING
				var logged = db.Logged((tr) => Log(tr.Log.GetTimingsReport(true)));
#else
				var logged = db;
#endif

				var directory = FdbDirectoryLayer.Create(location);
				Dump(directory);

				await logged.WriteAsync(async tr =>
				{

					Log("Creating /Foo$ ...");
					var foo = await directory.CreateAsync(tr, "Foo$", Slice.FromStringAscii("partition"));
					Dump(foo);
					await DumpSubspace(tr, location);

					Log("Creating /Foo$/Bar ...");
					// create a 'Bar' under the 'Foo' partition
					var bar = await foo.CreateAsync(tr, "Bar");
					Dump(bar);
					await DumpSubspace(tr, location);

					Assert.That(bar.FullName, Is.EqualTo("Foo$/Bar"));
					Assert.That(bar.Path, Is.EqualTo(new string[] { "Foo$", "Bar" }));
					Assert.That(bar.DirectoryLayer, Is.SameAs(directory));
					Assert.That(bar.DirectoryLayer, Is.SameAs(foo.DirectoryLayer));

					// Attempting to move 'Bar' outside the Foo partition should fail
					Log("Attempting to move /Foo$/Bar to /Bar ...");
					Assert.That(
						async () => await bar.MoveToAsync(tr, FdbDirectoryPath.Combine("Bar")),
						Throws.InstanceOf<InvalidOperationException>()
					);

				}, this.Cancellation);

				Log("Attempting to move /Foo$/Bar to /Bar ...");
				Assert.That(
					async () => await logged.ReadWriteAsync(tr => directory.MoveAsync(tr, FdbDirectoryPath.Combine("Foo", "Bar"), FdbDirectoryPath.Combine("Bar")), this.Cancellation),
					Throws.InstanceOf<InvalidOperationException>()
				);
			}

		}

		[Test]
		public async Task Test_Directory_Cannot_Move_To_A_Sub_Partition()
		{
			using (var db = await OpenTestDatabaseAsync())
			{
				var location = db.Root.ByKey("DL");
				await CleanLocation(db, location);

#if ENABLE_LOGGING
				var logged = db.Logged((tr) => Log(tr.Log.GetTimingsReport(true)));
#else
				var logged = db;
#endif

				var directory = FdbDirectoryLayer.Create(location);
				Dump(directory);

				await logged.WriteAsync(async tr =>
				{
					Log("Create [Outer$]");
					var outer = await directory.CreateAsync(tr, "Outer", Slice.FromStringAscii("partition"));
					Dump(outer);
					await DumpSubspace(tr, location);

					// create a 'Inner' subpartition under the 'Outer' partition
					Log("Create [Outer$][Inner$]");
					var inner = await outer.CreateAsync(tr, "Inner", Slice.FromString("partition"));
					Dump(inner);
					await DumpSubspace(tr, location);

					Assert.That(inner.Path, Is.EqualTo(FdbDirectoryPath.Combine("Outer", "Inner")));
					Assert.That(inner.FullName, Is.EqualTo("Outer/Inner"));
					Assert.That(inner.DirectoryLayer, Is.SameAs(directory));
					Assert.That(inner.DirectoryLayer, Is.SameAs(outer.DirectoryLayer));

					// create folder /Outer/Foo
					Log("Create [Outer$][Foo]...");
					var foo = await outer.CreateAsync(tr, "Foo");
					await DumpSubspace(tr, location);
					Assert.That(foo.Path, Is.EqualTo(FdbDirectoryPath.Combine("Outer", "Foo")));
					Assert.That(foo.FullName, Is.EqualTo("Outer/Foo"));

					// create folder /Outer/Inner/Bar
					Log("Create [Outer$/Inner$][Bar]...");
					var bar = await inner.CreateAsync(tr, "Bar");
					await DumpSubspace(tr, location);
					Assert.That(bar.Path, Is.EqualTo(FdbDirectoryPath.Combine("Outer", "Inner", "Bar")));
					Assert.That(bar.FullName, Is.EqualTo("Outer/Inner/Bar"));

					// Attempting to move 'Foo' inside the Inner partition should fail
					Assert.That(async () => await foo.MoveToAsync(tr, FdbDirectoryPath.Combine("Outer", "Inner", "Foo")), Throws.InstanceOf<InvalidOperationException>());
					Assert.That(async () => await directory.MoveAsync(tr, FdbDirectoryPath.Combine("Outer", "Foo"), FdbDirectoryPath.Combine("Outer", "Inner", "Foo")), Throws.InstanceOf<InvalidOperationException>());

					// Attempting to move 'Bar' outside the Inner partition should fail
					Assert.That(async () => await bar.MoveToAsync(tr, FdbDirectoryPath.Combine("Outer", "Bar")), Throws.InstanceOf<InvalidOperationException>());
					Assert.That(async () => await directory.MoveAsync(tr, FdbDirectoryPath.Combine("Outer", "Inner", "Bar"), FdbDirectoryPath.Combine("Outer", "Bar")), Throws.InstanceOf<InvalidOperationException>());

					// Moving 'Foo' inside the Outer partition itself should work
					Log("Create [Outer$/SubFolder]...");
					await directory.CreateAsync(tr, FdbDirectoryPath.Combine("Outer", "SubFolder")); // parent of destination folder must already exist when moving...
					await DumpSubspace(tr, location);

					Log("Move [Outer$/Foo] to [Outer$/SubFolder/Foo]");
					var foo2 = await directory.MoveAsync(tr, FdbDirectoryPath.Combine("Outer", "Foo"), FdbDirectoryPath.Combine("Outer", "SubFolder", "Foo"));
					await DumpSubspace(tr, location);
					Assert.That(foo2.Path, Is.EqualTo(FdbDirectoryPath.Combine("Outer", "SubFolder", "Foo")));
					Assert.That(foo2.FullName, Is.EqualTo("Outer/SubFolder/Foo"));
					Assert.That(foo2.GetPrefix(), Is.EqualTo(foo.GetPrefix()));

					// Moving 'Bar' inside the Inner partition itself should work
					Log("Create 'Outer/Inner/SubFolder'...");
					await directory.CreateAsync(tr, new[] { "Outer", "Inner", "SubFolder" }); // parent of destination folder must already exist when moving...
					await DumpSubspace(tr, location);

					Log("Move 'Outer/Inner/Bar' to 'Outer/Inner/SubFolder/Bar'");
					var bar2 = await directory.MoveAsync(tr, FdbDirectoryPath.Combine("Outer", "Inner", "Bar"), FdbDirectoryPath.Combine("Outer", "Inner", "SubFolder", "Bar"));
					await DumpSubspace(tr, location);
					Assert.That(bar2.Path, Is.EqualTo(FdbDirectoryPath.Combine("Outer", "Inner", "SubFolder", "Bar")));
					Assert.That(bar2.FullName, Is.EqualTo("Outer/Inner/SubFolder/Bar"));
					Assert.That(bar2.GetPrefix(), Is.EqualTo(bar.GetPrefix()));
				}, this.Cancellation);
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
				var location = db.Root.ByKey("DL");
				await CleanLocation(db, location);

				var directory = FdbDirectoryLayer.Create(location);

				using (var tr = await db.BeginTransactionAsync(this.Cancellation))
				{
					// create foo
					Log("Creating /Foo$ ...");
					var foo = await directory.CreateOrOpenAsync(tr, "Foo$", Slice.FromString("partition"));
					Dump(foo);
					await DumpSubspace(tr, location);
					Assert.That(foo, Is.Not.Null);
					Assert.That(foo.FullName, Is.EqualTo("Foo$"));
					Assert.That(foo.Path, Is.EqualTo(new[] { "Foo$" }));
					Assert.That(foo.Layer, Is.EqualTo(Slice.FromString("partition")));
					Assert.That(foo, Is.InstanceOf<FdbDirectoryPartition>());

					// verify list
					Log("Checking top...");
					var folders = await directory.ListAsync(tr);
					Assert.That(folders, Is.EqualTo(new[] { "Foo$" }));

					// rename to bar
					Log("Renaming [Foo$] to [Bar$]");
					var bar = await foo.MoveToAsync(tr, "Bar$");
					await DumpSubspace(tr, location);
					Assert.That(bar, Is.Not.Null);
					Assert.That(bar.FullName, Is.EqualTo("Bar$"));
					Assert.That(bar.Path, Is.EqualTo(new[] { "Bar$" }));
					Assert.That(bar.Layer, Is.EqualTo(Slice.FromString("partition")));
					Assert.That(bar, Is.InstanceOf<FdbDirectoryPartition>());

					// should have kept the same prefix
					//note: we need to cheat to get the key of the partition
					Assert.That(bar.Copy().GetPrefix(), Is.EqualTo(foo.Copy().GetPrefix()), "Prefix for partition should not changed after being moved");

					// verify list again
					folders = await directory.ListAsync(tr);
					Assert.That(folders, Is.EqualTo(new[] { "Bar$" }));

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
				var location = db.Root.ByKey("DL");
				await CleanLocation(db, location);

				var directory = FdbDirectoryLayer.Create(location);

				using (var tr = await db.BeginTransactionAsync(this.Cancellation))
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
				var location = db.Root.ByKey("DL");
				await CleanLocation(db, location);

				var directory = FdbDirectoryLayer.Create(location);

				var logged = db;

				// CreateOrOpen
				Assert.That(async () => await logged.ReadWriteAsync(tr => directory.CreateOrOpenAsync(tr, default(string[])), this.Cancellation), Throws.InstanceOf<ArgumentNullException>());
				Assert.That(async () => await logged.ReadWriteAsync(tr => directory.CreateOrOpenAsync(tr, new string[0]), this.Cancellation), Throws.InstanceOf<InvalidOperationException>());
				Assert.That(async () => await logged.ReadWriteAsync(tr => directory.CreateOrOpenAsync(tr, default(string)), this.Cancellation), Throws.InstanceOf<ArgumentNullException>());

				// Create
				Assert.That(async () => await logged.ReadWriteAsync(tr => directory.CreateAsync(tr, default(string[])), this.Cancellation), Throws.InstanceOf<ArgumentNullException>());
				Assert.That(async () => await logged.ReadWriteAsync(tr => directory.CreateAsync(tr, new string[0]), this.Cancellation), Throws.InstanceOf<InvalidOperationException>());
				Assert.That(async () => await logged.ReadWriteAsync(tr => directory.CreateAsync(tr, default(string)), this.Cancellation), Throws.InstanceOf<ArgumentNullException>());

				// Open
				Assert.That(async () => await logged.ReadAsync(tr => directory.OpenAsync(tr, default(string[])), this.Cancellation), Throws.InstanceOf<ArgumentNullException>());
				Assert.That(async () => await logged.ReadAsync(tr => directory.OpenAsync(tr, new string[0]), this.Cancellation), Throws.InstanceOf<InvalidOperationException>());
				Assert.That(async () => await logged.ReadAsync(tr => directory.OpenAsync(tr, default(string)), this.Cancellation), Throws.InstanceOf<ArgumentNullException>());

				// Move
				Assert.That(async () => await logged.ReadWriteAsync(tr => directory.MoveAsync(tr, default(string[]), new[] { "foo" }), this.Cancellation), Throws.InstanceOf<ArgumentNullException>());
				Assert.That(async () => await logged.ReadWriteAsync(tr => directory.MoveAsync(tr, new[] { "foo" }, default(string[])), this.Cancellation), Throws.InstanceOf<ArgumentNullException>());
				Assert.That(async () => await logged.ReadWriteAsync(tr => directory.MoveAsync(tr, new string[0], new[] { "foo" }), this.Cancellation), Throws.InstanceOf<InvalidOperationException>());
				Assert.That(async () => await logged.ReadWriteAsync(tr => directory.MoveAsync(tr, new[] { "foo" }, new string[0]), this.Cancellation), Throws.InstanceOf<InvalidOperationException>());

				// Remove
				Assert.That(async () => await logged.WriteAsync(tr => directory.RemoveAsync(tr, default(string[])), this.Cancellation), Throws.InstanceOf<ArgumentNullException>());
				Assert.That(async () => await logged.WriteAsync(tr => directory.RemoveAsync(tr, new string[0]), this.Cancellation), Throws.InstanceOf<InvalidOperationException>());
				Assert.That(async () => await logged.WriteAsync(tr => directory.RemoveAsync(tr, new string[] { "Foo", " ", "Bar" }), this.Cancellation), Throws.InstanceOf<InvalidOperationException>());
				Assert.That(async () => await logged.WriteAsync(tr => directory.RemoveAsync(tr, default(string)), this.Cancellation), Throws.InstanceOf<ArgumentNullException>());

				// List
				Assert.That(async () => await logged.ReadAsync(tr => directory.ListAsync(tr, default(string[])), this.Cancellation), Throws.InstanceOf<ArgumentNullException>());
				Assert.That(async () => await logged.ReadAsync(tr => directory.ListAsync(tr, new string[] { "Foo", "", "Bar" }), this.Cancellation), Throws.InstanceOf<InvalidOperationException>());
				Assert.That(async () => await logged.ReadAsync(tr => directory.ListAsync(tr, default(string)), this.Cancellation), Throws.InstanceOf<ArgumentNullException>());

			}
		}

		[Test]
		public async Task Test_Directory_Partitions_Should_Disallow_Creation_Of_Direct_Keys()
		{
			// an instance of FdbDirectoryPartition should throw when attempting to create keys directly under it, and should only be used to create/open sub directories
			// => this is because keys created directly will most certainly conflict with directory subspaces

			using (var db = await OpenTestDatabaseAsync())
			{
				var location = db.Root.ByKey("DL");
				await CleanLocation(db, location);

				var logged = db;

				var directory = FdbDirectoryLayer.Create(location);
				Dump(directory);

				// the constraint will always be the same for all the checks
				void ShouldFail<T>(ActualValueDelegate<T> del)
				{
					Assert.That(del, Throws.InstanceOf<InvalidOperationException>().With.Message.Contains("root of directory partition"));
				}

				void ShouldPass<T>(ActualValueDelegate<T> del)
				{
					Assert.That(del, Throws.Nothing);
				}

				await logged.WriteAsync(async tr =>
				{
					var partition = await directory.CreateAsync(tr, "Foo", Slice.FromStringAscii("partition"));
					Log($"Partition: {partition.Descriptor.Prefix:K}");
					//note: if we want a testable key INSIDE the partition, we have to get it from a sub-directory
					var subdir = await partition.CreateOrOpenAsync(tr, "Bar");
					Log($"SubDir: {subdir.Descriptor.Prefix:K}");
					var barKey = subdir.GetPrefix();

					// === PASS ===
					// these methods are allowed to succeed on directory partitions, because we need them for the rest to work

					ShouldPass(() => partition.Copy().GetPrefix()); // EXCEPTION: we need this to work, because that's the only way that the unit tests above can see the partition key!
					ShouldPass(() => partition.ToString()); // EXCEPTION: this should never fail!
					ShouldPass(() => partition.DumpKey(barKey)); // EXCEPTION: this should always work, because this can be used for debugging and logging...
					ShouldPass(() => partition.BoundCheck(barKey, true)); // EXCEPTION: needs to work because it is used by GetRange() and GetKey()

					// === FAIL ====

					// Key
					ShouldFail(() => partition.GetPrefix());

					// Contains
					ShouldFail(() => partition.Contains(barKey));

					// Extract / ExtractAndCheck / BoundCheck
					ShouldFail(() => partition.ExtractKey(barKey, boundCheck: false));
					ShouldFail(() => partition.ExtractKey(barKey, boundCheck: true));

					// Partition
					ShouldFail(() => partition.Partition.ByKey(123));
					ShouldFail(() => partition.Partition.ByKey(123, "hello"));
					ShouldFail(() => partition.Partition.ByKey(123, "hello", false));
					ShouldFail(() => partition.Partition.ByKey(123, "hello", false, "world"));

					// Keys

					ShouldFail(() => partition.Append(Slice.FromString("hello")));
					var subspace = await location.Resolve(tr);
					ShouldFail(() => partition.Append(subspace.GetPrefix()));
					ShouldFail(() => partition[STuple.Create("hello", 123)]);

					ShouldFail(() => partition.ToRange());
					ShouldFail(() => partition.ToRange(Slice.FromString("hello")));
					ShouldFail(() => partition.ToRange(TuPack.EncodeKey("hello")));

					// Tuples

					ShouldFail(() => partition.Encode(123));
					ShouldFail(() => partition.Encode(123, "hello"));
					ShouldFail(() => partition.Encode(123, "hello", false));
					ShouldFail(() => partition.Encode(123, "hello", false, "world"));
					ShouldFail(() => partition.Encode<object>(123));

					ShouldFail(() => partition.EncodeMany<int>(new[] { 123, 456, 789 }));
					ShouldFail(() => partition.EncodeMany<int>((IEnumerable<int>) new[] { 123, 456, 789 }));
					ShouldFail(() => partition.EncodeMany<object>(new object[] { 123, "hello", true }));
					ShouldFail(() => partition.EncodeMany<object>((IEnumerable<object>) new object[] { 123, "hello", true }));

					ShouldFail(() => partition.Unpack(barKey));
					ShouldFail(() => partition.Decode<int>(barKey));
					ShouldFail(() => partition.DecodeLast<int>(barKey));
					ShouldFail(() => partition.DecodeFirst<int>(barKey));

					ShouldFail(() => partition.ToRange());
					ShouldFail(() => partition.ToRange(Slice.FromString("hello")));
					ShouldFail(() => partition.PackRange(STuple.Create("hello")));

				}, this.Cancellation);
			}
		}

		[Test]
		public async Task Test_Concurrent_Directory_Creation()
		{
			using (var db = await OpenTestDatabaseAsync())
			{
				var location = db.Root.ByKey("DL");
				await CleanLocation(db, location);

				var logged = db;

				var directory = FdbDirectoryLayer.Create(location);
				Dump(directory);

				//to prevent any side effect from first time initialization of the directory layer, already create one dummy folder
				await logged.ReadWriteAsync(tr => directory.CreateAsync(tr, "Zero"), this.Cancellation);

				var logdb = db.Logged((tr) => Log(tr.Log.GetTimingsReport(true)));

				var f = FdbDirectoryLayer.AnnotateTransactions;
				try
				{
					FdbDirectoryLayer.AnnotateTransactions = true;

					using (var tr1 = await logdb.BeginTransactionAsync(this.Cancellation))
					using (var tr2 = await logdb.BeginTransactionAsync(this.Cancellation))
					{

						await Task.WhenAll(
							tr1.GetReadVersionAsync(),
							tr2.GetReadVersionAsync()
						);

						// T1 creates first directory
						var first = await directory.CreateAsync(tr1, new[] { "First" }, Slice.Nil);
						tr1.Set(first.GetPrefix(), Value("This belongs to the first directory"));

						// T2 creates second directory
						var second = await directory.CreateAsync(tr2, new[] { "Second" }, Slice.Nil);
						tr2.Set(second.GetPrefix(), Value("This belongs to the second directory"));

						// T1 commits first
						Log("Committing T1...");
						await tr1.CommitAsync();
						Log("T1 committed");
						tr1.Dispose(); // force T1 to be dumped immediately

						// T2 commits second
						Log("Committing T2...");
						await tr2.CommitAsync();
						Log("T2 committed");
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
				var location = db.Root.ByKey("DL");
				await CleanLocation(db, location);

				var logged = db;

				// to keep the test db in a good shape, we will still use tuples for our custom prefix,
				// but using strings so that they do not collide with the integers used by the normal allocator
				// ie: regular prefix would be ("DL", 123) and our custom prefixes will be ("DL", "abc")

				var directory = FdbDirectoryLayer.Create(location);
				Dump(directory);

				//to prevent any side effect from first time initialization of the directory layer, already create one dummy folder
				await logged.ReadWriteAsync(tr => directory.CreateAsync(tr, "Zero"), this.Cancellation);

				var logdb = db.Logged((tr) => Log(tr.Log.GetTimingsReport(true)));

				var f = FdbDirectoryLayer.AnnotateTransactions;
				try
				{
					FdbDirectoryLayer.AnnotateTransactions = true;

					using (var tr1 = await logdb.BeginTransactionAsync(this.Cancellation))
					using (var tr2 = await logdb.BeginTransactionAsync(this.Cancellation))
					{

						await Task.WhenAll(
							tr1.GetReadVersionAsync(),
							tr2.GetReadVersionAsync()
						);

						var subspace1 = await location.Resolve(tr1);
						var subspace2 = await location.Resolve(tr2);

						var first = await directory.RegisterAsync(tr1, new[] { "First" }, Slice.Nil, subspace1.Encode("abc"));
						tr1.Set(first.GetPrefix(), Value("This belongs to the first directory"));

						var second = await directory.RegisterAsync(tr2, new[] { "Second" }, Slice.Nil, subspace2.Encode("def"));
						tr2.Set(second.GetPrefix(), Value("This belongs to the second directory"));

						Log("Committing T1...");
						await tr1.CommitAsync();
						Log("T1 committed");
						tr1.Dispose(); // force T1 to be dumped immediately

						Log("Committing T2...");
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
								Log("INCONCLUSIVE!");
								Assert.Inconclusive("FIXME: Current implementation of DirectoryLayer creates read conflict when registering directories with custom key prefix");
							}
							throw;
						}
						Log("T2 committed");
					}
				}
				finally
				{
					FdbDirectoryLayer.AnnotateTransactions = f;
				}

			}

		}

		[Test]
		public async Task Test_TryOpenCached()
		{
			using (var db = await OpenTestDatabaseAsync())
			{
				// we will put everything under a custom namespace
				var location = db.Root.ByKey("DL");
				await CleanLocation(db, location);

#if ENABLE_LOGGING
				var logged = new FdbLoggedDatabase(db, false, false, (tr) => { Log(tr.Log.GetTimingsReport(true)); });
#else
				var logged = db;
#endif

				// put the nodes under (..,"DL",\xFE,) and the content under (..,"DL",)
				var directory = FdbDirectoryLayer.Create(location);

				Assert.That(directory.Content, Is.Not.Null);
				Assert.That(directory.Content, Is.EqualTo(location));

				void Validate(FdbDirectorySubspace actual, FdbDirectorySubspace expected)
				{
					Assert.That(actual, Is.Not.Null);
					Assert.That(actual.FullName, Is.EqualTo(expected.FullName));
					Assert.That(actual.Path, Is.EqualTo(expected.Path));
					Assert.That(actual.Layer, Is.EqualTo(expected.Layer));
					Assert.That(actual.DirectoryLayer, Is.SameAs(expected.DirectoryLayer));
					Assert.That(actual.GetPrefix(), Is.EqualTo(expected.GetPrefix()));
				}

				// first, initialize the subspace
				Log("Creating 'Foo' ...");
				var foo = await logged.ReadWriteAsync(tr => directory.CreateAsync(tr, "Foo"), this.Cancellation);
				Assert.That(foo, Is.Not.Null);
				Assert.That(foo.FullName, Is.EqualTo("Foo"));
				Assert.That(foo.Path, Is.EqualTo(new[] { "Foo" }));
				Assert.That(foo.Layer, Is.EqualTo(Slice.Empty));
				Assert.That(foo.DirectoryLayer, Is.SameAs(directory));
				Assert.That(foo.Context, Is.InstanceOf<SubspaceContext>());
#if DEBUG
				Log("After creating 'Foo':");
				await DumpSubspace(db, location);
#endif
				// first transaction wants to open the folder, which is not in cache.
				// => we expect the subspace to be a new instance
				Log("OpenCached (#1)...");
				var foo1 = await logged.ReadAsync(async tr =>
				{
					var folder = await directory.TryOpenCachedAsync(tr, "Foo");
					Validate(folder, foo);
					return folder.Descriptor;
				}, this.Cancellation);

				// This instance should not be the same as the one returned by CreateAsync(..) because the layer does not know then if the transaction will commit or not
				// => we only cache when _reading_ from the db.
				Assert.That(foo1, Is.Not.SameAs(foo), "Subspace should NOT be cached when created, only when first accessed in another transaction!");

				// second transaction wants to open the same folder, which should be in cache
				// => we expect the subspace to be the SAME instance
				Log("OpenCached (#2)...");
				var foo2 = await logged.ReadAsync(async tr =>
				{
					var folder = await directory.TryOpenCachedAsync(tr, "Foo");
					Validate(folder, foo);
					return folder.Descriptor;

				}, this.Cancellation);
				// We expect to get the same instance as the previous call, since no change was made to the directory layer
				Assert.That(foo2, Is.SameAs(foo1), "Subspace descriptor should be the same instance as previous transaction!");

				// update the global metadata version of the database
				Log("Bump the global metadataVersion only...");
				await logged.WriteAsync(tr => tr.TouchMetadataVersionKey(), this.Cancellation);

				// This should NOT bust the DL cache because the change is unrelated
				Log("OpenCached (#3)...");
				var foo3 = await logged.ReadAsync(async tr =>
				{
					var folder = await directory.TryOpenCachedAsync(tr, "Foo");
					Validate(folder, foo);
					return folder.Descriptor;
				}, this.Cancellation);

				//TODO: currently the DL does NOT have its own "version key" so it has to drop the cache whenever the MV is changed globally, which is not efficient
				// => If we implement a proper cache management, we may need to update the behavior of this test!

				// We expect the instance to change since the cache SHOULD have been reset after the change to the metadataVersion
				Assert.That(foo3, Is.SameAs(foo1), "Subspace should be the same instance as previous transaction after bumping the global metadataVersion");

				// creating a subfolder /Foo/Bar should bust the cache of the partition
				Log("Creating 'Foo/Bar' ...");
				await logged.ReadWriteAsync(tr => directory.CreateAsync(tr, new[] { "Foo", "Bar" }), this.Cancellation);
#if DEBUG
				Log("After creating 'Foo/Bar':");
				await DumpSubspace(db, location);
#endif

				Log("OpenCached (#4)...");
				var foo4 = await logged.ReadAsync(async tr =>
				{
					var folder = await directory.TryOpenCachedAsync(tr, "Foo");
					Validate(folder, foo);
					return folder.Descriptor;
				}, this.Cancellation);

				// We expect the instance to change since the cache SHOULD have been reset after the change to the metadataVersion
				Assert.That(foo4, Is.Not.SameAs(foo3), "Subspace should NOT be the same instance as previous transaction after modifying the partition!");

				Log("Get 'Foo'...");
				// now another read, this time should be a cached instance
				var foo5 = await logged.ReadAsync(async tr =>
				{
					var folder = await directory.TryOpenCachedAsync(tr, "Foo");
					Validate(folder, foo);
					return folder.Descriptor;
				}, this.Cancellation);

				// We expect the instance to change since the cache SHOULD have been reset after the change to the metadataVersion
				Assert.That(foo5, Is.SameAs(foo4), "Subspace should be the same cached instance");
			}
		}

		[Test]
		public async Task Test_TryOpenCached_Sequential()
		{
			using (var db = await OpenTestDatabaseAsync())
			{
				// we will put everything under a custom namespace
				var location = db.Root.ByKey("DL");
				await CleanLocation(db, location);

#if ENABLE_LOGGING
				var logged = new FdbLoggedDatabase(db, false, false, (tr) => { Log(tr.Log.GetTimingsReport(true)); });
#else
				var logged = db;
#endif

				var directory = FdbDirectoryLayer.Create(location);

				// create multiple batches of folders, and attempt to read them all back sequentially
				// - without cache, we expect the latency to be latency for GRV plus N times the latency per folder
				// - with a cache, we expect the latency to be identical the first time, and then only the GRV latency until the next mutation

				const int K = 5; // number of batches
				const int N = 5; // number of folders per batch
				const int R = 10; // number of executions per batch

				// create a bunch of subspaces and read them back multiple times

				for(int k = 1; k <= K; k++)
				{
					Log($"Creating Batch #{k}/{K}...");
					await logged.WriteAsync(async tr =>
					{
						for (int i = 0; i < N; i++) await directory.CreateAsync(tr, FdbDirectoryPath.Combine("Tests", k.ToString(), i.ToString()));
					},
					this.Cancellation);

					Log($"> Reading...");
					for (int r = 0; r < R; r++)
					{
						var sw = Stopwatch.StartNew();
						var folders = await logged.ReadAsync(async tr =>
						{
							// reading sequentially should be slow without caching for the first request, but then should be very fast
							var res = new List<FdbDirectorySubspace>();
							for (int i = 0; i < N; i++)
							{
								res.Add(await directory.TryOpenCachedAsync(tr, FdbDirectoryPath.Combine("Tests", k.ToString(), i.ToString())));
							}
							return res; 
						}, this.Cancellation);
						var dur = sw.Elapsed;

						Log($"  - {dur.TotalMilliseconds:N3} ms");

						Assert.That(folders, Is.Not.Null.And.Count.EqualTo(N));
					}
				}
			}
		}

		[Test]
		public async Task Test_TryOpenCached_Poisoned_After_Mutation()
		{

			using (var db = await OpenTestDatabaseAsync())
			{
				// we will put everything under a custom namespace
				var location = db.Root.ByKey("DL");
				await CleanLocation(db, location);

#if ENABLE_LOGGING
				var logged = new FdbLoggedDatabase(db, false, false, (tr) => { Log(tr.Log.GetTimingsReport(true)); });
#else
				var logged = db;
#endif

				var directory = FdbDirectoryLayer.Create(location);

				await logged.ReadWriteAsync(tr => directory.CreateAsync(tr, "Foo"), this.Cancellation);

				var fooCached = await logged.ReadAsync(async tr =>
				{
					var folder = await directory.TryOpenCachedAsync(tr, "Foo");
					Assert.That(folder.Context, Is.InstanceOf<FdbDirectoryLayer.State>());
					return folder;
				}, this.Cancellation);

				// the fooCached instance should dead outside the transaction!
				Assert.That(() => fooCached.GetPrefix(), Throws.InstanceOf<InvalidOperationException>(), "Accessing a cached subspace outside the transaction should throw");

				var fooUncached = await logged.ReadAsync(tr => directory.TryOpenAsync(tr, "Foo"), this.Cancellation);
				Assert.That(() => fooUncached.Context.EnsureIsValid(), Throws.Nothing, "Accessing a non-cached subspace outside the transaction should not throw");
			}
		}


		[Test]
		public async Task Test_SubspacePath_Resolve()
		{
			using (var db = await OpenTestDatabaseAsync())
			{
				// we will put everything under a custom namespace
				var location = db.Root.ByKey("DL");
				await CleanLocation(db, location);

#if ENABLE_LOGGING
				var logged = new FdbLoggedDatabase(db, false, false, (tr) => { Log(tr.Log.GetTimingsReport(true)); });
#else
				var logged = db;
#endif

				var directory = FdbDirectoryLayer.Create(location);

				var dir = directory["Hello"]["World"];

				// create the corresponding location
				Log("Creating " + dir);
				var prefix = await logged.ReadWriteAsync(async tr => (await directory.CreateAsync(tr, dir.Path)).GetPrefix(), this.Cancellation);
				Log("> Created under " + prefix);

				await DumpSubspace(db, location);

				// resolve the location
				await logged.ReadAsync(async tr =>
				{
					Log("Resolving " + dir);
					var subspace = await dir.Resolve(tr);
					Assert.That(subspace, Is.Not.Null);
					Assert.That(subspace.Path, Is.EqualTo(dir.Path), ".Path");
					Log("> Found under " + subspace.GetPrefix());
					Assert.That(subspace.GetPrefix(), Is.EqualTo(prefix), ".Prefix");
				}, this.Cancellation);

				// resolving again should return a cached version
				await logged.ReadAsync(async tr =>
				{
					var subspace = await dir.Resolve(tr);
					Assert.That(subspace.GetPrefix(), Is.EqualTo(prefix), ".Prefix");
				}, this.Cancellation);

			}
		}

		[Test]
		public void Test_FdbDirectoryPath_Basics()
		{

			{
				var path = FdbDirectoryPath.Empty;
				Assert.That(path.IsEmpty, Is.True, "Empty.IsEmpty");
				Assert.That(path.Count, Is.EqualTo(0), "Empty.Count");
				Assert.That(path.Name, Is.EqualTo(string.Empty), "Empty.Name");
				Assert.That(path.ToString(), Is.EqualTo(string.Empty), "Empty.ToString()");

				Assert.That(path, Is.EqualTo(FdbDirectoryPath.Empty), "Empty.Equals(Empty)");
				Assert.That(path == FdbDirectoryPath.Empty, Is.True, "Empty == Empty");
				Assert.That(path != FdbDirectoryPath.Empty, Is.False, "Empty != Empty");
			}

			{
				var path = FdbDirectoryPath.Combine("Foo");
				Assert.That(path.IsEmpty, Is.False, "[Foo].IsEmpty");
				Assert.That(path.Count, Is.EqualTo(1), "[Foo].Count");
				Assert.That(path.Name, Is.EqualTo("Foo"), "[Foo].Name");
				Assert.That(path.ToString(), Is.EqualTo("Foo"), "[Foo].ToString()");
				Assert.That(path[0], Is.EqualTo("Foo"), "[Foo][0]");
				Assert.That(path.GetParent(), Is.EqualTo(FdbDirectoryPath.Empty), "[Foo].Name");

				Assert.That(path, Is.EqualTo(path), "[Foo].Equals([Foo])");
				Assert.That(path == path, Is.True, "[Foo] == [Foo]");
				Assert.That(path != path, Is.False, "[Foo] != [Foo]");

				Assert.That(path, Is.EqualTo(FdbDirectoryPath.Combine("Foo")), "[Foo].Equals([Foo]')");
				Assert.That(path, Is.EqualTo(FdbDirectoryPath.Combine("Foo", "Bar").GetParent()), "[Foo].Equals([Foo/Bar].GetParent())");

				Assert.That(path, Is.Not.EqualTo(FdbDirectoryPath.Empty), "[Foo].Equals(Empty)");
				Assert.That(path == FdbDirectoryPath.Empty, Is.False, "[Foo] == Empty");
				Assert.That(path != FdbDirectoryPath.Empty, Is.True, "[Foo] != Empty");
			}

			{
				var path1 = FdbDirectoryPath.Combine("Foo", "Bar");
				var path2 = FdbDirectoryPath.Parse("Foo/Bar");
				var path3 = new FdbDirectoryPath(new [] { "Foo", "Bar"});

				Assert.That(path2, Is.EqualTo(path1), "path1 eq path2");
				Assert.That(path3, Is.EqualTo(path1), "path1 eq path3");
				Assert.That(path3, Is.EqualTo(path2), "path2 eq path3");

				Assert.That(path2.GetHashCode(), Is.EqualTo(path1.GetHashCode()), "h(path1) == h(path2)");
				Assert.That(path3.GetHashCode(), Is.EqualTo(path1.GetHashCode()), "h(path1) == h(path3)");
			}

		}

	}

}
