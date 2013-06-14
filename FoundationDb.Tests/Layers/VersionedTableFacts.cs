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
	* Neither the name of the <organization> nor the
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

namespace FoundationDb.Layers.Tables.Tests
{
	using FoundationDb.Client;
	using FoundationDb.Client.Tests;
	using FoundationDb.Layers.Tuples;
	using NUnit.Framework;
	using System;
	using System.Collections.Generic;
	using System.Linq;
	using System.Threading.Tasks;

	[TestFixture]
	public class VersionedTableFacts
	{

		[Test]
		public async Task Test_FdbVersionedTable_Write_Multiple_SequentialVersions()
		{
			using (var db = await TestHelpers.OpenTestDatabaseAsync())
			{

				var subspace = new FdbSubspace(FdbTuple.Create("TblVerSeq"));

				// clear previous values
				await TestHelpers.DeleteSubspace(db, subspace);

				var table = new FdbVersionedTable<int, string>(
					"Foos",
					db, 
					subspace,
					FdbTupleKeyReader<int>.Default,
					new FdbSliceSerializer<string>(
						(str) => Slice.FromString(str),
						(slice) => slice.ToUnicode()
					)
				);

				// create a new version
				using (var tr = db.BeginTransaction())
				{
					var ver = await table.TryCreateNewVersionAsync(tr, 123, 0, "hello");
					// should return the previous version (null here because it is the first)
					Assert.That(ver, Is.Null);

					await tr.CommitAsync();
				}

#if DEBUG
				await TestHelpers.DumpSubspace(db, subspace);
#endif

				// read that version
				using (var tr = db.BeginTransaction())
				{
					// latest
					var value = await table.GetLastAsync(tr, 123);
					Assert.That(value, Is.EqualTo(new KeyValuePair<long?, string>(0, "hello")));

					// specific
					value = await table.GetVersionAsync(tr, 123, 0);
					Assert.That(value, Is.EqualTo(new KeyValuePair<long?, string>(0, "hello")));
				}

				// create a new version
				using (var tr = db.BeginTransaction())
				{
					var ver = await table.TryCreateNewVersionAsync(tr, 123, 1, "hello v2");
					// should return previous version, that is '0'
					Assert.That(ver, Is.EqualTo(0));

					await tr.CommitAsync();
				}

#if DEBUG
				await TestHelpers.DumpSubspace(db, subspace);
#endif

				// read that new version
				using (var tr = db.BeginTransaction())
				{
					// latest
					var value = await table.GetLastAsync(tr, 123);
					Assert.That(value, Is.EqualTo(new KeyValuePair<long?, string>(1, "hello v2")));

					// specific
					value = await table.GetVersionAsync(tr, 123, 1);
					Assert.That(value, Is.EqualTo(new KeyValuePair<long?, string>(1, "hello v2")));

					// but check that we can still access the previous version
					value = await table.GetVersionAsync(tr, 123, 0);
					Assert.That(value, Is.EqualTo(new KeyValuePair<long?, string>(0, "hello")));
				}

				// delete
				using (var tr = db.BeginTransaction())
				{
					//TODO: Delete !
					var ver = await table.TryCreateNewVersionAsync(tr, 123, 2, null);

					// should return previous version, that is '1'
					Assert.That(ver, Is.EqualTo(1));

					await tr.CommitAsync();
				}

#if DEBUG
				await TestHelpers.DumpSubspace(db, subspace);
#endif

				// read back
				using (var tr = db.BeginTransaction())
				{
					// latest
					var value = await table.GetLastAsync(tr, 123);
					Assert.That(value, Is.EqualTo(new KeyValuePair<long?, string>(2, null)));

					// specific
					value = await table.GetVersionAsync(tr, 123, 2);
					Assert.That(value, Is.EqualTo(new KeyValuePair<long?, string>(2, null)));

					// but check that we can still access the previous versions
					value = await table.GetVersionAsync(tr, 123, 1);
					Assert.That(value, Is.EqualTo(new KeyValuePair<long?, string>(1, "hello v2")));

					value = await table.GetVersionAsync(tr, 123, 0);
					Assert.That(value, Is.EqualTo(new KeyValuePair<long?, string>(0, "hello")));
				}

			}

		}

		[Test]
		public async Task Test_FdbVersionedTable_Write_Multiple_TimestampVersions()
		{

			using (var db = await TestHelpers.OpenTestDatabaseAsync())
			{
				var subspace = new FdbSubspace(FdbTuple.Create("TblVerTs"));

				// clear previous values
				await TestHelpers.DeleteSubspace(db, subspace);

				var table = new FdbVersionedTable<Guid, string>(
					"Bars",
					db,
					subspace,
					FdbTupleKeyReader<Guid>.Default,
					new FdbSliceSerializer<string>(
						(str) => Slice.FromString(str),
						(slice) => slice.ToUnicode()
					)
				);

				bool created = await table.OpenOrCreateAsync();
				Assert.That(created, Is.True);

				// generate a new key
				Guid id = Guid.NewGuid();

				// create a bunch of versions
				long[] vers = new long[] { 1234, 2345, 3456, 4567, 5678 };
				long firstVersion = vers[0];
				long lastVersion = vers[vers.Length - 1];

				// create a few versions
				var old = default(long?);
				foreach (long ver in vers)
				{
					using (var tr = db.BeginTransaction())
					{
						var previous = await table.TryCreateNewVersionAsync(tr, id, ver, "hello v" + ver);

						// should return the previous version
						Assert.That(previous, Is.EqualTo(old));

						await tr.CommitAsync();
					}
					old = ver;
				}

#if DEBUG
				await TestHelpers.DumpSubspace(db, subspace);
#endif


				// read the last version
				using (var tr = db.BeginTransaction())
				{
					// latest
					var value = await table.GetLastAsync(tr, id);
					Assert.That(value, Is.EqualTo(new KeyValuePair<long?, string>(lastVersion, "hello v" + lastVersion)));
				}

				// read each version specifically
				foreach (var ver in vers)
				{
					using (var tr = db.BeginTransaction())
					{
						var value = await table.GetVersionAsync(tr, id, ver);
						Assert.That(value, Is.EqualTo(new KeyValuePair<long?, string>(ver, "hello v" + ver)));
					}
				}

				// use non-specific versions to ensure that we always get the one alive at that time

				var rnd = new Random();
				for (int i = 0; i < 100; i++)
				{
					long searchVersion = rnd.Next((int)(lastVersion + 10));

					using (var tr = db.BeginTransaction())
					{
						var value = await table.GetVersionAsync(tr, id, searchVersion);
						//Assert.That(value, Is.EqualTo(new KeyValuePair<long?, string>(expectedVersion, "hello v" + expectedVersion)));
						if (searchVersion < firstVersion)
						{ // before first insert, should not exist
							Console.WriteLine("Looking for " + searchVersion + ", got " + value.Key + ", while expecting nothing");
							Assert.That(value.Key, Is.Null);
						}
						else
						{
							// last one that is not after
							long expected = vers.Where(x => x <= searchVersion).Last();
							Console.WriteLine("Looking for " + searchVersion + ", got " + value.Key + ", while expecting " + expected);
							Assert.That(value.Key, Is.EqualTo(expected));
						}

					}
				}


			}

		}

	}

}
