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

				var hpa = new FdbHighContentionAllocator(location);

				long id;
				var ids = new HashSet<long>();

				using (var tr = db.BeginTransaction())
				{
					id = await hpa.AllocateAsync(tr);
					await tr.CommitAsync();

				}
				Console.WriteLine(id);
				ids.Add(id);

				await TestHelpers.DumpSubspace(db, location);

				for (int i = 0; i < 100; i++)
				{
					using (var tr = db.BeginTransaction())
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

				// put the nodes under (..,"DL",\xFE,) and the content under (..,"DL",)
				var directory = new FdbDirectoryLayer(location[FdbKey.Directory], location);

				Assert.That(directory.ContentSubspace, Is.Not.Null);
				Assert.That(directory.ContentSubspace.Key, Is.EqualTo(location.Key));
				Assert.That(directory.NodeSubspace, Is.Not.Null);
				Assert.That(directory.NodeSubspace.Key, Is.EqualTo(location.Key + Slice.FromByte(254)));

				// first call should create a new subspace (with a random prefix)
				FdbDirectorySubspace foo;

				using (var tr = db.BeginTransaction())
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

				foo2 = await directory.OpenAsync(db, new[] { "Foo" });
#if DEBUG
				await TestHelpers.DumpSubspace(db, location);
#endif
				Assert.That(foo2, Is.Not.Null);
				Assert.That(foo2.Path, Is.EqualTo(new[] { "Foo" }));
				Assert.That(foo2.Layer, Is.EqualTo(Slice.Empty));
				Assert.That(foo2.DirectoryLayer, Is.SameAs(directory));
				Assert.That(foo2.Key, Is.EqualTo(foo.Key), "Second call to CreateOrOpen should return the same subspace");
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

				// put the nodes under (..,"DL",\xFE,) and the content under (..,"DL",)
				var directory = new FdbDirectoryLayer(location[FdbKey.Directory], location);

				Assert.That(directory.ContentSubspace, Is.Not.Null);
				Assert.That(directory.ContentSubspace.Key, Is.EqualTo(location.Key));
				Assert.That(directory.NodeSubspace, Is.Not.Null);
				Assert.That(directory.NodeSubspace.Key, Is.EqualTo(location.Key + Slice.FromByte(254)));

				// first call should create a new subspace (with a random prefix)
				var foo = await directory.CreateOrOpenAsync(db, new[] { "Foo" }, Slice.FromString("AcmeLayer"));
#if DEBUG
				await TestHelpers.DumpSubspace(db, location);
#endif

				Assert.That(foo, Is.Not.Null);
				Assert.That(foo.Path, Is.EqualTo(new[] { "Foo" }));
				Assert.That(foo.Layer.ToUnicode(), Is.EqualTo("AcmeLayer"));
				Assert.That(foo.DirectoryLayer, Is.SameAs(directory));

				// second call should return the same subspace
				var foo2 = await directory.OpenAsync(db, new [] { "Foo" }, Slice.FromString("AcmeLayer"));
				Assert.That(foo2, Is.Not.Null);
				Assert.That(foo2.Path, Is.EqualTo(new[] { "Foo" }));
				Assert.That(foo2.Layer.ToUnicode(), Is.EqualTo("AcmeLayer"));
				Assert.That(foo2.DirectoryLayer, Is.SameAs(directory));
				Assert.That(foo2.Key, Is.EqualTo(foo.Key), "Second call to CreateOrOpen should return the same subspace");

				// opening it with wrong layer id should fail
				Assert.Throws<InvalidOperationException>(async () => await directory.OpenAsync(db, new[] { "Foo" }, Slice.FromString("OtherLayer")), "Opening with invalid layer id should fail");

				// opening without specifying a layer should disable the layer check
				var foo3 = await directory.OpenAsync(db, "Foo", layer: Slice.Nil);
				Assert.That(foo3, Is.Not.Null);
				Assert.That(foo3.Layer.ToUnicode(), Is.EqualTo("AcmeLayer"));

				// CheckLayer with the correct value should pass
				Assert.DoesNotThrow(() => foo3.CheckLayer(Slice.FromString("AcmeLayer")), "CheckLayer should not throw if the layer id is correct");

				// CheckLayer with the incorrect value should fail
				Assert.Throws<InvalidOperationException>(() => foo3.CheckLayer(Slice.FromString("OtherLayer")), "CheckLayer should throw if the layer id is not correct");

				// CheckLayer with empty string should do nothing
				foo3.CheckLayer(Slice.Empty);
				foo3.CheckLayer(Slice.Nil);
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

				// put the nodes under (..,"DL",\xFE,) and the content under (..,"DL",)
				var directory = FdbDirectoryLayer.FromSubspace(location);

				FdbDirectorySubspace folder;
				using (var tr = db.BeginTransaction())
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
				var foo = await directory.OpenAsync(db, new[] { "Foo" });
				var bar = await directory.OpenAsync(db, new[] { "Foo", "Bar" });
				Assert.That(foo, Is.Not.Null);
				Assert.That(bar, Is.Not.Null);
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
				var directory = FdbDirectoryLayer.FromSubspace(location);

				// linear subtree "/Foo/Bar/Baz"
				await directory.CreateOrOpenAsync(db, new[] { "Foo", "Bar", "Baz" });
				// flat subtree "/numbers/0" to "/numbers/9"
				for (int i = 0; i < 10; i++) await directory.CreateOrOpenAsync(db, new[] { "numbers", i.ToString() });
#if DEBUG
				await TestHelpers.DumpSubspace(db, location);
#endif

				var subdirs = await directory.ListAsync(db, new[] { "Foo" });
				Assert.That(subdirs, Is.Not.Null);
				Assert.That(subdirs.Count, Is.EqualTo(1));
				Assert.That(subdirs[0], Is.EqualTo("Bar"));

				subdirs = await directory.ListAsync(db, new[] { "Foo", "Bar" });
				Assert.That(subdirs, Is.Not.Null);
				Assert.That(subdirs.Count, Is.EqualTo(1));
				Assert.That(subdirs[0], Is.EqualTo("Baz"));

				subdirs = await directory.ListAsync(db, new[] { "Foo", "Bar", "Baz" });
				Assert.That(subdirs, Is.Not.Null);
				Assert.That(subdirs.Count, Is.EqualTo(0));

				subdirs = await directory.ListAsync(db, new[] { "numbers" });
				Assert.That(subdirs, Is.Not.Null);
				Assert.That(subdirs.Count, Is.EqualTo(10));
				Assert.That(subdirs, Is.EquivalentTo(Enumerable.Range(0, 10).Select(x => x.ToString()).ToList()));

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
				var directory = FdbDirectoryLayer.FromSubspace(location);

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

				// put the nodes under (..,"DL",\xFE,) and the content under (..,"DL",)
				var directory = new FdbDirectoryLayer(location[FdbKey.Directory], location);

				// create a folder at ('Foo',)
				var original = await directory.CreateOrOpenAsync(db, "Foo");
#if DEBUG
				await TestHelpers.DumpSubspace(db, location);
#endif
				Assert.That(original, Is.Not.Null);
				Assert.That(original.Path, Is.EqualTo(new[] { "Foo" }));

				// rename/move it as ('Bar',)
				var renamed = await original.MoveAsync(db, new [] { "Bar" });
#if DEBUG
				await TestHelpers.DumpSubspace(db, location);
#endif
				Assert.That(renamed, Is.Not.Null);
				Assert.That(renamed.Path, Is.EqualTo(new [] { "Bar" }));
				Assert.That(renamed.Key, Is.EqualTo(original.Key));

				// opening the old path should fail
				Assert.Throws<InvalidOperationException>(async () => await directory.OpenAsync(db, "Foo"));

				// opening the new path should succeed
				var folder = await directory.OpenAsync(db, "Bar");
				Assert.That(folder, Is.Not.Null);
				Assert.That(folder.Path, Is.EqualTo(renamed.Path));
				Assert.That(folder.Key, Is.EqualTo(renamed.Key));

				// moving the folder under itself should fail
				Assert.Throws<InvalidOperationException>(async () => await folder.MoveAsync(db, new[] { "Bar", "Baz" }));
			}
		}

		[Test]
		public async Task Test_Remove_Folder()
		{
			using (var db = await TestHelpers.OpenTestDatabaseAsync())
			{
				var location = db.Partition("DL");
				await db.ClearRangeAsync(location);
				var directory = FdbDirectoryLayer.FromSubspace(location);

				// RemoveAsync

				string[] path = new[] { "CrashTestDummy" };
				await directory.CreateAsync(db, path);
#if DEBUG
				await TestHelpers.DumpSubspace(db, location);
#endif

				// removing an existing folder should succeeed
				await directory.RemoveAsync(db, path);
#if DEBUG
				await TestHelpers.DumpSubspace(db, location);
#endif
				//TODO: call ExistsAsync(...) once it is implemented!

				// Removing it a second time should fail
				Assert.Throws<InvalidOperationException>(async () => await directory.RemoveAsync(db, path), "Removing a non-existent directory should fail");

				// TryRemoveAsync

				await directory.CreateAsync(db, path);

				// attempting to remove a folder should return true
				bool res = await directory.TryRemoveAsync(db, path);
				Assert.That(res, Is.True);

				// further attempts should return false
				res = await directory.TryRemoveAsync(db, path);
				Assert.That(res, Is.False);

				// Corner Cases

				// removing the root folder is not allowed (too dangerous)
				Assert.Throws<InvalidOperationException>(async () => await directory.RemoveAsync(db, new string[0]), "Attempting to remove the root directory should fail");
			}
		}

		[Test]
		public async Task Test_Can_Change_Layer_Of_Existing_Directory()
		{
			using (var db = await TestHelpers.OpenTestDatabaseAsync())
			{
				var location = db.Partition("DL");
				await db.ClearRangeAsync(location);
				var directory = FdbDirectoryLayer.FromSubspace(location);

				var folder = await directory.CreateAsync(db, "Test", layer: Slice.FromString("foo"));
#if DEBUG
				await TestHelpers.DumpSubspace(db, location);
#endif
				Assert.That(folder, Is.Not.Null);
				Assert.That(folder.Layer.ToUnicode(), Is.EqualTo("foo"));

				// change the layer to 'bar'
				var folder2 = await folder.ChangeLayerAsync(db, Slice.FromString("bar"));
#if DEBUG
				await TestHelpers.DumpSubspace(db, location);
#endif
				Assert.That(folder2, Is.Not.Null);
				Assert.That(folder2.Layer.ToUnicode(), Is.EqualTo("bar"));
				Assert.That(folder2.Path, Is.EqualTo(FdbTuple.Create("Test")));
				Assert.That(folder2.Key, Is.EqualTo(folder.Key));

				// opening the directory with the new layer should succeed
				var folder3 = await directory.OpenAsync(db, "Test", layer: Slice.FromString("bar"));
				Assert.That(folder3, Is.Not.Null);

				// opening the directory with the old layer should fail
				Assert.Throws<InvalidOperationException>(async () => await directory.OpenAsync(db, "Test", layer: Slice.FromString("foo")));

			}
		}

		[Test]
		public async Task Test_Directory_Methods_Should_Fail_With_Empty_Paths()
		{
			using (var db = await TestHelpers.OpenTestDatabaseAsync())
			{
				var location = db.Partition("DL");
				await db.ClearRangeAsync(location);
				var directory = FdbDirectoryLayer.FromSubspace(location);

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
