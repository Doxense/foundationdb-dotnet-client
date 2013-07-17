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

namespace FoundationDB.Client.Tests
{
	using FoundationDB.Layers.Tuples;
	using FoundationDB.Linq;
	using NUnit.Framework;
	using System;
	using System.Collections.Generic;
	using System.Diagnostics;
	using System.Linq;
	using System.Text;
	using System.Threading.Tasks;

	[TestFixture]
	public class TransactionFacts
	{

		[Test]
		public async Task Test_Can_Create_And_Dispose_Transactions()
		{
			using (var db = await TestHelpers.OpenTestDatabaseAsync())
			{
				using (var tr = db.BeginTransaction())
				{
					Assert.That(tr, Is.Not.Null, "BeginTransaction should return a valid instance");
					Assert.That(tr.State == FdbTransaction.STATE_READY, "Transaction should be in ready state");
					Assert.That(tr.StillAlive, Is.True, "Transaction should be alive");
					Assert.That(tr.Handle.IsInvalid, Is.False, "Transaction handle should be valid");
					Assert.That(tr.Handle.IsClosed, Is.False, "Transaction handle should not be closed");
					Assert.That(tr.Database, Is.SameAs(db), "Transaction should reference the parent Database");
					Assert.That(tr.Size, Is.EqualTo(0), "Estimated size should be zero");

					// manually dispose the transaction
					tr.Dispose();

					Assert.That(tr.State == FdbTransaction.STATE_DISPOSED, "Transaction should now be in the disposed state");
					Assert.That(tr.StillAlive, Is.False, "Transaction should be not be alive anymore");
					Assert.That(tr.Handle.IsClosed, Is.True, "Transaction handle should now be closed");

					// multiple calls to dispose should not do anything more
					Assert.That(() => { tr.Dispose(); }, Throws.Nothing);
				}
			}
		}
		
		[Test]
		public async Task Test_Creating_Concurrent_Transactions_Are_Independent()
		{
			using (var db = await TestHelpers.OpenTestDatabaseAsync())
			{
				FdbTransaction tr1 = null;
				FdbTransaction tr2 = null;
				try
				{
					// concurrent transactions should have separate FDB_FUTURE* handles

					tr1 = db.BeginTransaction();
					tr2 = db.BeginTransaction();

					Assert.That(tr1, Is.Not.Null);
					Assert.That(tr2, Is.Not.Null);
					Assert.That(tr1, Is.Not.SameAs(tr2), "Should create two different transaction objects");
					Assert.That(tr1.Handle, Is.Not.EqualTo(tr2.Handle), "Should have different FDB_FUTURE* handles");

					// disposing the first should not impact the second

					tr1.Dispose();

					Assert.That(tr1.StillAlive, Is.False, "First transaction should be dead");
					Assert.That(tr1.Handle.IsClosed, Is.True, "First FDB_FUTURE* handle should be closed");

					Assert.That(tr2.StillAlive, Is.True, "Second transaction should still be alive");
					Assert.That(tr2.Handle.IsClosed, Is.False, "Second FDB_FUTURE* handle should still be opened");
				}
				finally
				{
					if (tr1 != null) tr1.Dispose();
					if (tr2 != null) tr2.Dispose();

				}
			}
		}

		[Test]
		public async Task Test_Commiting_An_Empty_Transaction_Does_Nothing()
		{
			using (var db = await TestHelpers.OpenTestDatabaseAsync())
			{
				using (var tr = db.BeginTransaction())
				{
					// do nothing with it
					await tr.CommitAsync();
					// => should not fail!

					Assert.That(tr.StillAlive, Is.False);
					Assert.That(tr.State, Is.EqualTo(FdbTransaction.STATE_COMMITTED));
				}
			}
		}

		[Test]
		public async Task Test_Rolling_Back_An_Empty_Transaction_Does_Nothing()
		{
			using (var db = await TestHelpers.OpenTestDatabaseAsync())
			{
				using (var tr = db.BeginTransaction())
				{
					// do nothing with it
					tr.Rollback();
					// => should not fail!

					Assert.That(tr.StillAlive, Is.False);
					Assert.That(tr.State, Is.EqualTo(FdbTransaction.STATE_ROLLEDBACK));
				}
			}
		}

		[Test]
		public async Task Test_Resetting_An_Empty_Transaction_Does_Nothing()
		{
			using (var db = await TestHelpers.OpenTestDatabaseAsync())
			{
				using (var tr = db.BeginTransaction())
				{
					// do nothing with it
					await tr.CommitAsync();
					// => should not fail!

					// Committed version should be -1 (where is is specified?)
					long ver = tr.GetCommittedVersion();
					Assert.That(ver, Is.EqualTo(-1), "Committed version of empty transaction should be -1");

				}
			}
		}

		[Test]
		public async Task Test_Can_Get_Transaction_Read_Version()
		{
			using (var db = await TestHelpers.OpenTestDatabaseAsync())
			{
				using (var tr = db.BeginTransaction())
				{
					long ver = await tr.GetReadVersionAsync();
					Assert.That(ver, Is.GreaterThan(0), "Read version should be > 0");

					// if we ask for it again, we should have the same value
					long ver2 = await tr.GetReadVersionAsync();
					Assert.That(ver2, Is.EqualTo(ver), "Read version should not change inside same transaction");
				}
			}
		}

		[Test]
		public async Task Test_Write_And_Read_Simple_Keys()
		{
			// test that we can read and write simple keys

			using (var db = await TestHelpers.OpenTestDatabaseAsync())
			{
				long ticks = DateTime.UtcNow.Ticks;
				long writeVersion;
				long readVersion;

				var location = db.Partition("test");

				// write a bunch of keys
				using (var tr = db.BeginTransaction())
				{
					tr.Set(location.Pack("hello"), Slice.FromString("World!"));
					tr.Set(location.Pack("timestamp"), Slice.FromInt64(ticks));
					tr.Set(location.Pack("blob"), Slice.Create(new byte[] { 42, 123, 7 }));

					await tr.CommitAsync();

					writeVersion = tr.GetCommittedVersion();
					Assert.That(writeVersion, Is.GreaterThan(0), "Commited version of non-empty transaction should be > 0");
				}

				// read them back
				using (var tr = db.BeginTransaction())
				{
					Slice bytes;

					readVersion = await tr.GetReadVersionAsync();
					Assert.That(readVersion, Is.GreaterThan(0), "Read version should be > 0");

					bytes = await tr.GetAsync(location.Pack("hello")); // => 1007 "past_version"
					Assert.That(bytes.Array, Is.Not.Null);
					Assert.That(Encoding.UTF8.GetString(bytes.Array, bytes.Offset, bytes.Count), Is.EqualTo("World!"));

					bytes = await tr.GetAsync(location.Pack("timestamp"));
					Assert.That(bytes.Array, Is.Not.Null);
					Assert.That(bytes.ToInt64(), Is.EqualTo(ticks));

					bytes = await tr.GetAsync(location.Pack("blob"));
					Assert.That(bytes.Array, Is.Not.Null);
					Assert.That(bytes.Array, Is.EqualTo(new byte[] { 42, 123, 7 }));
				}

				Assert.That(readVersion, Is.GreaterThanOrEqualTo(writeVersion), "Read version should not be before previous committed version");
			}
		}

		[Test]
		public async Task Test_Can_Snapshot_Read()
		{

			using(var db = await TestHelpers.OpenTestDatabaseAsync())
			{
				var location = db.Partition("test");

				await db.ClearRangeAsync(location);

				// write a bunch of keys
				await db.Attempt.Change((tr) =>
				{
					tr.Set(location.Pack("hello"), Slice.FromString("World!"));
					tr.Set(location.Pack("foo"), Slice.FromString("bar"));
				});

				// read them using snapshot
				using(var tr = db.BeginTransaction())
				{
					Slice bytes;

					bytes = await tr.Snapshot.GetAsync(location.Pack("hello"));
					Assert.That(bytes.ToUnicode(), Is.EqualTo("World!"));

					bytes = await tr.Snapshot.GetAsync(location.Pack("foo"));
					Assert.That(bytes.ToUnicode(), Is.EqualTo("bar"));

				}

			}

		}

		[Test]
		public async Task Test_Regular_Read_With_Concurrent_Change_Should_Conflict()
		{
			// see http://community.foundationdb.com/questions/490/snapshot-read-vs-non-snapshot-read/492

			using (var db = await TestHelpers.OpenTestDatabaseAsync())
			{
				var location = db.Partition("test");

				await db.ClearRangeAsync(location);

				// 
				await db.Attempt.Change((tr) =>
				{
					tr.Set(location.Pack("foo"), Slice.FromString("foo"));
				});

				using (var trA = db.BeginTransaction())
				using (var trB = db.BeginTransaction())
				{
					// regular read
					var foo = await trA.GetAsync(location.Pack("foo"));
					trA.Set(location.Pack("foo"), Slice.FromString("bar"));

					// this will conflict with our read
					trB.Set(location.Pack("foo"), Slice.FromString("bar"));
					await trB.CommitAsync();

					// should succeed
					try
					{
						await trA.CommitAsync();
						Assert.Fail("Commit should conflict !");
					}
					catch (AssertionException) { throw; }
					catch (Exception e)
					{
						Assert.That(e, Is.InstanceOf<FdbException>().With.Property("Code").EqualTo(FdbError.NotCommitted));
					}
				}
			}

		}

		[Test]
		public async Task Test_Snapshot_Read_With_Concurrent_Change_Should_Not_Conflict()
		{

			// see http://community.foundationdb.com/questions/490/snapshot-read-vs-non-snapshot-read/492

			using (var db = await TestHelpers.OpenTestDatabaseAsync())
			{
				var location = db.Partition("test");

				await db.ClearRangeAsync(location);

				// 
				await db.Attempt.Change((tr) =>
				{
					tr.Set(location.Pack("foo"), Slice.FromString("foo"));
				});

				using (var trA = db.BeginTransaction())
				using (var trB = db.BeginTransaction())
				{
					// reading with snapshot mode should not conflict
					var foo = await trA.Snapshot.GetAsync(location.Pack("foo"));
					trA.Set(location.Pack("foo"), Slice.FromString("bar"));

					// this would normally conflicts with the previous read if it wasn't a snapshot read
					trB.Set(location.Pack("foo"), Slice.FromString("bar"));
					await trB.CommitAsync();

					// should succeed
					await trA.CommitAsync();
				}
			}

		}

		[Test]
		public async Task Test_Can_Set_Read_Version()
		{
			// Verify that we can set a read version on a transaction
			// * tr1 will set value to 1
			// * tr2 will set value to 2
			// * tr3 will SetReadVersion(TR1.CommittedVersion) and we expect it to read 1 (and not 2)

			using (var db = await TestHelpers.OpenTestDatabaseAsync())
			{
				var location = db.Partition("test");

				long commitedVersion;

				// create first version
				using (var tr1 = db.BeginTransaction())
				{
					tr1.Set(location.Pack("concurrent"), Slice.Create(new byte[] { 1 }));
					await tr1.CommitAsync();

					// get this version
					commitedVersion = tr1.GetCommittedVersion();
				}

				// mutate in another transaction
				using (var tr2 = db.BeginTransaction())
				{
					tr2.Set(location.Pack("concurrent"), Slice.Create(new byte[] { 2 }));
					await tr2.CommitAsync();
				}

				// read the value with TR1's commited version
				using (var tr3 = db.BeginTransaction())
				{
					tr3.SetReadVersion(commitedVersion);

					long ver = await tr3.GetReadVersionAsync();
					Assert.That(ver, Is.EqualTo(commitedVersion), "GetReadVersion should return the same value as SetReadVersion!");

					var bytes = await tr3.GetAsync(location.Pack("concurrent"));

					Assert.That(bytes, Is.Not.Null);
					Assert.That(bytes, Is.EqualTo(new byte[] { 1 }), "Should have seen the first version!");

				}

			}

		}

		[Test]
		public async Task Test_Has_Access_To_System_Keys()
		{
			using (var db = await TestHelpers.OpenTestDatabaseAsync())
			{

				using (var tr = db.BeginTransaction())
				{

					// should fail if access to system keys has not been requested

					try
					{
						var _ = await tr.GetRangeStartsWith(Slice.FromAscii("\xFF"), new FdbRangeOptions { Limit = 10 }).ToListAsync();
						Assert.Fail("Should not have access to system keys by default");
					}
					catch (Exception e)
					{
						Assert.That(e, Is.InstanceOf<FdbException>());
						var x = (FdbException)e;
						Assert.That(x.Code, Is.EqualTo(FdbError.KeyOutsideLegalRange));
					}

					// should succeed once system access has been requested
					tr.WithAccessToSystemKeys();

					var keys = await tr.GetRangeStartsWith(Slice.FromAscii("\xFF"), new FdbRangeOptions { Limit = 10 }).ToListAsync();
					Assert.That(keys, Is.Not.Null);
				}

			}
		}

		[Test]
		public async Task Test_Cannot_Read_Or_Write_Outside_Of_Restricted_Key_Space()
		{
			using (var db = await TestHelpers.OpenTestDatabaseAsync())
			{
				var space = db.Partition(123);

				db.RestrictKeySpace(space.Tuple);
			
				Assert.That(db.ValidateKey(space.Pack("hello")), Is.Null, "key inside range should be ok");

				// bounds should be allowed
				var range = space.ToRange();
				Assert.That(db.ValidateKey(range.Begin), Is.Null, "range + '\\0' should be allowed");
				Assert.That(db.ValidateKey(range.End), Is.Null, "range + '\\FF' should be allowed");

				// before/after should be denied
				Assert.That(db.ValidateKey(db.Partition(122).Pack("hello")), Is.InstanceOf<FdbException>().And.Property("Code").EqualTo(FdbError.KeyOutsideLegalRange), "key before the range should be denied");
				Assert.That(db.ValidateKey(db.Partition(124).Pack("hello")), Is.InstanceOf<FdbException>().And.Property("Code").EqualTo(FdbError.KeyOutsideLegalRange), "key after the range should be denied");

				// the range prefix itself is not allowed
				Assert.That(db.ValidateKey(space.Tuple.Packed), Is.InstanceOf<FdbException>().And.Property("Code").EqualTo(FdbError.KeyOutsideLegalRange), "Range prefix itself is not allowed");

				// check that methods also respect the key range
				using (var tr = db.BeginTransaction())
				{
					// should allow writing inside the space
					tr.Set(space.Pack("hello"), Slice.Empty);
					tr.Set(range.Begin, Slice.Empty);
					tr.Set(range.End, Slice.Empty);

					// should not allow outside of the space
					Assert.That(
						Assert.Throws<FdbException>(() => tr.Set(db.Namespace.Pack(122), Slice.Empty), "Key is less than minimum allowed"),
						Has.Property("Code").EqualTo(FdbError.KeyOutsideLegalRange)
					);
					Assert.That(
						Assert.Throws<FdbException>(() => tr.Set(db.Namespace.Pack(124), Slice.Empty), "Key is more than maximum allowed"),
						Has.Property("Code").EqualTo(FdbError.KeyOutsideLegalRange)
					);

					// should not allow the prefix itself
					Assert.That(
						Assert.Throws<FdbException>(() => tr.Set(space.Tuple, Slice.Empty)),
						Has.Property("Code").EqualTo(FdbError.KeyOutsideLegalRange)
					);

					// check that the commit does not blow up
					await tr.CommitAsync();
				}

			}
		}
	}
}
