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
	using NUnit.Framework;
	using System;
	using System.Collections.Generic;
	using System.Diagnostics;
	using System.Linq;
	using System.Text;
	using System.Threading;
	using System.Threading.Tasks;

	[TestFixture]
	public class TransactionFacts
	{

		[Test]
		public async Task Test_Can_Create_And_Dispose_Transactions()
		{
			using (var db = await TestHelpers.OpenTestDatabaseAsync())
			{
				Assert.That(db, Is.InstanceOf<FdbDatabase>(), "This test only works directly on FdbDatabase");

				using (var tr = (FdbTransaction)db.BeginTransaction())
				{
					Assert.That(tr, Is.Not.Null, "BeginTransaction should return a valid instance");
					Assert.That(tr.State == FdbTransaction.STATE_READY, "Transaction should be in ready state");
					Assert.That(tr.StillAlive, Is.True, "Transaction should be alive");
					Assert.That(tr.Handle.IsInvalid, Is.False, "Transaction handle should be valid");
					Assert.That(tr.Handle.IsClosed, Is.False, "Transaction handle should not be closed");
					Assert.That(tr.Database, Is.SameAs(db), "Transaction should reference the parent Database");
					Assert.That(tr.Size, Is.EqualTo(0), "Estimated size should be zero");
					Assert.That(tr.IsReadOnly, Is.False, "Transaction is not read-only");

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
		public async Task Test_Creating_A_ReadOnly_Transaction_Throws_When_Writing()
		{
			using (var db = await TestHelpers.OpenTestDatabaseAsync())
			{
				using (var tr = db.BeginReadOnlyTransaction())
				{
					Assert.That(tr, Is.Not.Null);
					
					// reading should not fail
					await tr.GetAsync(db.Pack("Hello"));

					// any attempt to recast into a writeable transaction should fail!
					var tr2 = (IFdbTransaction)tr;
					Assert.That(tr2.IsReadOnly, Is.True, "Transaction should be marked as readonly");
					var location = db.Partition("ReadOnly");
					Assert.That(() => tr2.Set(location.Pack("Hello"), Slice.Empty), Throws.InvalidOperationException);
					Assert.That(() => tr2.Clear(location.Pack("Hello")), Throws.InvalidOperationException);
					Assert.That(() => tr2.ClearRange(location.Pack("ABC"), location.Pack("DEF")), Throws.InvalidOperationException);
					Assert.That(() => tr2.Atomic(location.Pack("Counter"), Slice.FromFixed32(1), FdbMutationType.Add), Throws.InvalidOperationException);
				}
			}
		}

		[Test]
		public async Task Test_Creating_Concurrent_Transactions_Are_Independent()
		{
			using (var db = await TestHelpers.OpenTestDatabaseAsync())
			{
				IFdbTransaction tr1 = null;
				IFdbTransaction tr2 = null;
				try
				{
					// concurrent transactions should have separate FDB_FUTURE* handles

					tr1 = db.BeginTransaction();
					tr2 = db.BeginTransaction();

					Assert.That(tr1, Is.Not.Null);
					Assert.That(tr2, Is.Not.Null);

					Assert.That(tr1, Is.Not.SameAs(tr2), "Should create two different transaction objects");

					Assert.That(tr1, Is.InstanceOf<FdbTransaction>());
					Assert.That(tr2, Is.InstanceOf<FdbTransaction>());
					Assert.That(((FdbTransaction)tr1).Handle, Is.Not.EqualTo(((FdbTransaction)tr2).Handle), "Should have different FDB_FUTURE* handles");

					// disposing the first should not impact the second

					tr1.Dispose();

					Assert.That(((FdbTransaction)tr1).StillAlive, Is.False, "First transaction should be dead");
					Assert.That(((FdbTransaction)tr1).Handle.IsClosed, Is.True, "First FDB_FUTURE* handle should be closed");

					Assert.That(((FdbTransaction)tr2).StillAlive, Is.True, "Second transaction should still be alive");
					Assert.That(((FdbTransaction)tr2).Handle.IsClosed, Is.False, "Second FDB_FUTURE* handle should still be opened");
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
					Assert.That(tr, Is.InstanceOf<FdbTransaction>());

					// do nothing with it
					await tr.CommitAsync();
					// => should not fail!

					Assert.That(((FdbTransaction)tr).StillAlive, Is.False);
					Assert.That(((FdbTransaction)tr).State, Is.EqualTo(FdbTransaction.STATE_COMMITTED));
				}
			}
		}

		[Test]
		public async Task Test_Resetting_An_Empty_Transaction_Does_Nothing()
		{
			using (var db = await TestHelpers.OpenTestPartitionAsync())
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
		public async Task Test_Cancelling_An_Empty_Transaction_Does_Nothing()
		{
			using (var db = await TestHelpers.OpenTestDatabaseAsync())
			{
				using (var tr = db.BeginTransaction())
				{
					Assert.That(tr, Is.InstanceOf<FdbTransaction>());

					// do nothing with it
					tr.Cancel();
					// => should not fail!

					Assert.That(((FdbTransaction)tr).StillAlive, Is.False);
					Assert.That(((FdbTransaction)tr).State, Is.EqualTo(FdbTransaction.STATE_CANCELED));
				}
			}
		}

		[Test]
		public async Task Test_Cancelling_Transaction_Before_Commit_Should_Throw_Immediately()
		{

			using (var db = await TestHelpers.OpenTestPartitionAsync())
			{
				var location = db.Partition("test");

				using(var tr = db.BeginTransaction())
				{
					tr.Set(location.Pack(1), Slice.FromString("hello"));
					tr.Cancel();

					await TestHelpers.AssertThrowsFdbErrorAsync(
						() => tr.CommitAsync(),
						FdbError.TransactionCancelled,
						"Committing an already cancelled exception should fail"
					);
				}
			}
		}

		[Test]
		public async Task Test_Cancelling_Transaction_During_Commit_Should_Abort_Task()
		{
			// we need to simulate some load on the db, to be able to cancel a Commit after it started, but before it completes
			// => we will try to commit a very large transaction in order to give us some time
			// note: if this test fails because it commits to fast, that means that your system is foo fast :)

			using (var db = await TestHelpers.OpenTestPartitionAsync())
			{
				var location = db.Partition("test");

				await db.ClearRangeAsync(location);

				var rnd = new Random();

				using (var tr = db.BeginTransaction())
				{
					// Writes about 5 MB of stuff in 100k chunks
					for (int i = 0; i < 50; i++)
					{
						tr.Set(location.Pack(i), Slice.Random(rnd, 100 * 1000));
					}

					// start commiting
					var t = tr.CommitAsync();

					// but almost immediately cancel the transaction
					await Task.Delay(1);
					if (t.IsCompleted) Assert.Inconclusive("Commit task already completed before having a chance to cancel");
					tr.Cancel();

					await TestHelpers.AssertThrowsFdbErrorAsync(
						() => t,
						FdbError.TransactionCancelled,
						"Cancelling a transaction that is writing to the server should fail the commit task"
					);
				}
			}
		}

		[Test]
		public async Task Test_Cancelling_Token_During_Commit_Should_Abort_Task()
		{
			// we need to simulate some load on the db, to be able to cancel the token passed to Commit after it started, but before it completes
			// => we will try to commit a very large transaction in order to give us some time
			// note: if this test fails because it commits to fast, that means that your system is foo fast :)

			using (var db = await TestHelpers.OpenTestPartitionAsync())
			{
				var location = db.Partition("test");

				await db.ClearRangeAsync(location);

				var rnd = new Random();

				using(var cts = new CancellationTokenSource())
				using (var tr = db.BeginTransaction(cts.Token))
				{
					// Writes about 5 MB of stuff in 100k chunks
					for (int i = 0; i < 50; i++)
					{
						tr.Set(location.Pack(i), Slice.Random(rnd, 100 * 1000));
					}

					// start commiting with a cancellation token
					var t = tr.CommitAsync();

					// but almost immediately cancel the token source
					await Task.Delay(1);

					if (t.IsCompleted) Assert.Inconclusive("Commit task already completed before having a chance to cancel");
					cts.Cancel();

					await TestHelpers.AssertThrowsAsync<TaskCanceledException>(() => t, "Cancelling a token passed to CommitAsync that is still pending should cancel the task");
				}
			}
		}

		[Test]
		public async Task Test_Can_Get_Transaction_Read_Version()
		{
			using (var db = await TestHelpers.OpenTestPartitionAsync())
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

			using (var db = await TestHelpers.OpenTestPartitionAsync())
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
		public async Task Test_Can_Resolve_Key_Selector()
		{
			using (var db = await TestHelpers.OpenTestPartitionAsync())
			{
				var location = db.Partition("keys");
				await db.ClearRangeAsync(location);

				var minKey = location.Key + FdbKey.MinValue;
				var maxKey = location.Key + FdbKey.MaxValue;

				#region Insert a bunch of keys ...
				using (var tr = db.BeginTransaction())
				{
					// keys
					// - (test,) + \0
					// - (test, 0) .. (test, 19)
					// - (test,) + \xFF
					tr.Set(minKey, Slice.FromString("min"));
					for (int i = 0; i < 20; i++)
					{
						tr.Set(location.Pack(i), Slice.FromString(i.ToString()));
					}
					tr.Set(maxKey, Slice.FromString("max"));
					await tr.CommitAsync();
				}
				#endregion

				using (var tr = db.BeginTransaction())
				{
					FdbKeySelector sel;

					// >= 0
					sel = FdbKeySelector.FirstGreaterOrEqual(location.Pack(0));
					Assert.That(await tr.GetKeyAsync(sel), Is.EqualTo(location.Pack(0)), "fGE(0) should return 0");
					Assert.That(await tr.GetKeyAsync(sel - 1), Is.EqualTo(minKey), "fGE(0)-1 should return minKey");
					Assert.That(await tr.GetKeyAsync(sel + 1), Is.EqualTo(location.Pack(1)), "fGE(0)+1 should return 1");

					// > 0
					sel = FdbKeySelector.FirstGreaterThan(location.Pack(0));
					Assert.That(await tr.GetKeyAsync(sel), Is.EqualTo(location.Pack(1)), "fGT(0) should return 1");
					Assert.That(await tr.GetKeyAsync(sel - 1), Is.EqualTo(location.Pack(0)), "fGT(0)-1 should return 0");
					Assert.That(await tr.GetKeyAsync(sel + 1), Is.EqualTo(location.Pack(2)), "fGT(0)+1 should return 2");

					// <= 10
					sel = FdbKeySelector.LastLessOrEqual(location.Pack(10));
					Assert.That(await tr.GetKeyAsync(sel), Is.EqualTo(location.Pack(10)), "lLE(10) should return 10");
					Assert.That(await tr.GetKeyAsync(sel - 1), Is.EqualTo(location.Pack(9)), "lLE(10)-1 should return 9");
					Assert.That(await tr.GetKeyAsync(sel + 1), Is.EqualTo(location.Pack(11)), "lLE(10)+1 should return 11");

					// < 10
					sel = FdbKeySelector.LastLessThan(location.Pack(10));
					Assert.That(await tr.GetKeyAsync(sel), Is.EqualTo(location.Pack(9)), "lLT(10) should return 9");
					Assert.That(await tr.GetKeyAsync(sel - 1), Is.EqualTo(location.Pack(8)), "lLT(10)-1 should return 8");
					Assert.That(await tr.GetKeyAsync(sel + 1), Is.EqualTo(location.Pack(10)), "lLT(10)+1 should return 10");

					// < 0
					sel = FdbKeySelector.LastLessThan(location.Pack(0));
					Assert.That(await tr.GetKeyAsync(sel), Is.EqualTo(minKey), "lLT(0) should return minKey");
					Assert.That(await tr.GetKeyAsync(sel + 1), Is.EqualTo(location.Pack(0)), "lLT(0)+1 should return 0");

					// >= 20
					sel = FdbKeySelector.FirstGreaterOrEqual(location.Pack(20));
					Assert.That(await tr.GetKeyAsync(sel), Is.EqualTo(maxKey), "fGE(20) should return maxKey");
					Assert.That(await tr.GetKeyAsync(sel - 1), Is.EqualTo(location.Pack(19)), "fGE(20)-1 should return 19");

					// > 19
					sel = FdbKeySelector.FirstGreaterThan(location.Pack(19));
					Assert.That(await tr.GetKeyAsync(sel), Is.EqualTo(maxKey), "fGT(19) should return maxKey");
					Assert.That(await tr.GetKeyAsync(sel - 1), Is.EqualTo(location.Pack(19)), "fGT(19)-1 should return 19");
				}
			}
		}

		[Test]
		public async Task Test_Get_Multiple_Values()
		{
			using (var db = await TestHelpers.OpenTestPartitionAsync())
			{

				var location = db.Partition("Batch");
				await db.ClearRangeAsync(location);

				int[] ids = new int[] { 8, 7, 2, 9, 5, 0, 3, 4, 6, 1 };

				using (var tr = db.BeginTransaction())
				{
					for (int i = 0; i < ids.Length; i++)
					{
						tr.Set(location.Pack(i), Slice.FromString("#" + i.ToString()));
					}
					await tr.CommitAsync();
				}

				using(var tr = db.BeginTransaction())
				{
					var keys = ids.Select(id => location.Pack(id)).ToArray();

					var results = await tr.GetValuesAsync(keys);

					Assert.That(results, Is.Not.Null);
					Assert.That(results.Length, Is.EqualTo(ids.Length));

					Console.WriteLine(String.Join(", ", results));

					for (int i = 0; i < ids.Length;i++)
					{
						Assert.That(results[i].ToString(), Is.EqualTo("#" + ids[i].ToString()));
					}

				}

			}
		}

		[Test]
		public async Task Test_Get_Multiple_Keys()
		{
			const int N = 20;

			using(var db = await TestHelpers.OpenTestPartitionAsync())
			{

				var location = db.Partition("keys");
				await db.ClearRangeAsync(location);

				var minKey = location.Key + FdbKey.MinValue;
				var maxKey = location.Key + FdbKey.MaxValue;

				#region Insert a bunch of keys ...
				using (var tr = db.BeginTransaction())
				{
					// keys
					// - (test,) + \0
					// - (test, 0) .. (test, N-1)
					// - (test,) + \xFF
					tr.Set(minKey, Slice.FromString("min"));
					for (int i = 0; i < 20; i++)
					{
						tr.Set(location.Pack(i), Slice.FromString(i.ToString()));
					}
					tr.Set(maxKey, Slice.FromString("max"));
					await tr.CommitAsync();
				}
				#endregion

				using (var tr = db.BeginTransaction())
				{
					var selectors = Enumerable.Range(0, N).Select((i) => FdbKeySelector.FirstGreaterOrEqual(location.Pack(i))).ToArray();

					// GetKeysAsync([])
					var results = await tr.GetKeysAsync(selectors);
					Assert.That(results, Is.Not.Null);
					Assert.That(results.Length, Is.EqualTo(20));
					for (int i = 0; i < N; i++)
					{
						Assert.That(results[i], Is.EqualTo(location.Pack(i)));
					}

					// GetKeysAsync(cast to enumerable)
					var results2 = await tr.GetKeysAsync((IEnumerable<FdbKeySelector>)selectors);
					Assert.That(results2, Is.EqualTo(results));

					// GetKeysAsync(real enumerable)
					var results3 = await tr.GetKeysAsync(selectors.Select(x => x));
					Assert.That(results3, Is.EqualTo(results));
				}
			}
		}

		/// <summary>Performs (x OP y) and ensure that the result is correct</summary>
		private async Task PerformAtomicOperationAndCheck(IFdbDatabase db, Slice key, int x, FdbMutationType type, int y)
		{

			int expected = 0;
			switch(type)
			{
				case FdbMutationType.And: expected = x & y; break;
				case FdbMutationType.Or: expected = x | y; break;
				case FdbMutationType.Xor: expected = x ^ y; break;
				case FdbMutationType.Add: expected = x + y; break;
				default: Assert.Fail("Invalid operation type"); break;
			}

			// set key = x
			using(var tr = db.BeginTransaction())
			{
				tr.Set(key, Slice.FromFixed32(x));
				await tr.CommitAsync();
			}

			// atomic key op y
			using (var tr = db.BeginTransaction())
			{
				tr.Atomic(key, Slice.FromFixed32(y), type);
				await tr.CommitAsync();
			}

			// read key
			using(var tr = db.BeginTransaction())
			{
				var data = await tr.GetAsync(key);
				Assert.That(data.Count, Is.EqualTo(4), "data.Count");

				Assert.That(data.ToInt32(), Is.EqualTo(expected), "0x{0} {1} 0x{2} = 0x{0}", x.ToString("X8"), type.ToString(), y.ToString("X8"), expected.ToString("X8"));
			}
		}

		[Test]
		public async Task Test_Can_Perform_Atomic_Operations()
		{
			// test that we can perform atomic mutations on keys

			using (var db = await TestHelpers.OpenTestPartitionAsync())
			{
				var location = await db.CreateOrOpenDirectoryAsync(FdbTuple.Create("test", "atomic"));
				await db.ClearRangeAsync(location);

				Slice key;

				key = location.Pack("add");
				await PerformAtomicOperationAndCheck(db, key, 0, FdbMutationType.Add, 0);
				await PerformAtomicOperationAndCheck(db, key, 0, FdbMutationType.Add, 1);
				await PerformAtomicOperationAndCheck(db, key, 1, FdbMutationType.Add, 0);
				await PerformAtomicOperationAndCheck(db, key, -2, FdbMutationType.Add, 1);
				await PerformAtomicOperationAndCheck(db, key, -1, FdbMutationType.Add, 1);
				await PerformAtomicOperationAndCheck(db, key, 123456789, FdbMutationType.Add, 987654321);

				key = location.Pack("and");
				await PerformAtomicOperationAndCheck(db, key, 0, FdbMutationType.And, 0);
				await PerformAtomicOperationAndCheck(db, key, 0, FdbMutationType.And, 0x018055AA);
				await PerformAtomicOperationAndCheck(db, key, -1, FdbMutationType.And, 0x018055AA);
				await PerformAtomicOperationAndCheck(db, key, 0x00FF00FF, FdbMutationType.And, 0x018055AA);
				await PerformAtomicOperationAndCheck(db, key, 0x0F0F0F0F, FdbMutationType.And, 0x018055AA);

				key = location.Pack("or");
				await PerformAtomicOperationAndCheck(db, key, 0, FdbMutationType.Or, 0);
				await PerformAtomicOperationAndCheck(db, key, 0, FdbMutationType.Or, 0x018055AA);
				await PerformAtomicOperationAndCheck(db, key, -1, FdbMutationType.Or, 0x018055AA);
				await PerformAtomicOperationAndCheck(db, key, 0x00FF00FF, FdbMutationType.Or, 0x018055AA);
				await PerformAtomicOperationAndCheck(db, key, 0x0F0F0F0F, FdbMutationType.Or, 0x018055AA);

				key = location.Pack("xor");
				await PerformAtomicOperationAndCheck(db, key, 0, FdbMutationType.Xor, 0);
				await PerformAtomicOperationAndCheck(db, key, 0, FdbMutationType.Xor, 0x018055AA);
				await PerformAtomicOperationAndCheck(db, key, -1, FdbMutationType.Xor, 0x018055AA);
				await PerformAtomicOperationAndCheck(db, key, 0x00FF00FF, FdbMutationType.Xor, 0x018055AA);
				await PerformAtomicOperationAndCheck(db, key, 0x0F0F0F0F, FdbMutationType.Xor, 0x018055AA);

			}
		}

		[Test]
		public async Task Test_Can_Snapshot_Read()
		{

			using(var db = await TestHelpers.OpenTestPartitionAsync())
			{
				var location = db.Partition("test");

				await db.ClearRangeAsync(location);

				// write a bunch of keys
				await db.WriteAsync((tr) =>
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

			using (var db = await TestHelpers.OpenTestPartitionAsync())
			{
				var location = db.Partition("test");

				await db.ClearRangeAsync(location);

				await db.WriteAsync((tr) =>
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

					// should fail with a "not_comitted" error
					await TestHelpers.AssertThrowsFdbErrorAsync(
						() => trA.CommitAsync(),
						FdbError.NotCommitted,
						"Commit should conflict !"
					);
				}
			}

		}

		[Test]
		public async Task Test_Snapshot_Read_With_Concurrent_Change_Should_Not_Conflict()
		{

			// see http://community.foundationdb.com/questions/490/snapshot-read-vs-non-snapshot-read/492

			using (var db = await TestHelpers.OpenTestPartitionAsync())
			{
				var location = db.Partition("test");
				await db.ClearRangeAsync(location);

				await db.WriteAsync((tr) =>
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
		public async Task Test_GetRange_With_Concurrent_Change_Should_Conflict()
		{
			using(var db = await TestHelpers.OpenTestPartitionAsync())
			{

				var loc = db.Partition("test");

				await db.WriteAsync((tr) =>
				{
					tr.ClearRange(loc);
					tr.Set(loc.Pack("foo", 50), Slice.FromAscii("fifty"));
				});

				// we will read the first key from [0, 100), expected 50
				// but another transaction will insert 42, in effect changing the result of our range
				// => this should conflict the GetRange

				using (var tr1 = db.BeginTransaction())
				{
					// [0, 100) limit 1 => 50
					await tr1
						.GetRange(loc.Pack("foo"), loc.Pack("foo", 100))
						.FirstOrDefaultAsync();

					// 42 < 50 > conflict !!!
					using (var tr2 = db.BeginTransaction())
					{
						tr2.Set(loc.Pack("foo", 42), Slice.FromAscii("forty-two"));
						await tr2.CommitAsync();
					}

					// we need to write something to force a conflict
					tr1.Set(loc.Pack("bar"), Slice.Empty);

					await TestHelpers.AssertThrowsFdbErrorAsync(() => tr1.CommitAsync(), FdbError.NotCommitted, "The Set(42) in TR2 should have conflicted with the GetRange(0, 100) in TR1");
				}

				// if the other transaction insert something AFTER 50, then the result of our GetRange would not change (because of the implied limit = 1)
				// => this should NOT conflict the GetRange
				// note that if we write something in the range (0, 100) but AFTER 50, it should not conflict because we are doing a limit=1

				await db.WriteAsync((tr) =>
				{
					tr.ClearRange(loc);
					tr.Set(loc.Pack("foo", 50), Slice.FromAscii("fifty"));
				});

				using (var tr1 = db.BeginTransaction())
				{
					// [0, 100) limit 1 => 50
					await tr1
						.GetRange(loc.Pack("foo"), loc.Pack("foo", 100))
						.FirstOrDefaultAsync();

					// 77 > 50 => no conflict
					using (var tr2 = db.BeginTransaction())
					{
						tr2.Set(loc.Pack("foo", 77), Slice.FromAscii("docm"));
						await tr2.CommitAsync();
					}

					// we need to write something to force a conflict
					tr1.Set(loc.Pack("bar"), Slice.Empty);

					// should not conflict!
					await tr1.CommitAsync();
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

			using (var db = await TestHelpers.OpenTestPartitionAsync())
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
			using (var db = await TestHelpers.OpenTestPartitionAsync())
			{

				using (var tr = db.BeginTransaction())
				{

					// should fail if access to system keys has not been requested

					try
					{
						var _ = await tr.GetRange(Slice.FromAscii("\xFF"), Slice.FromAscii("\xFF\xFF"), new FdbRangeOptions { Limit = 10 }).ToListAsync();
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

					var keys = await tr.GetRange(Slice.FromAscii("\xFF"), Slice.FromAscii("\xFF\xFF"), new FdbRangeOptions { Limit = 10 }).ToListAsync();
					Assert.That(keys, Is.Not.Null);
				}

			}
		}

		[Test]
		public async Task Test_Can_Set_Timeout_And_RetryLimit()
		{
			using (var db = await TestHelpers.OpenTestPartitionAsync())
			{

				using (var tr = db.BeginTransaction())
				{
					Assert.That(tr.Timeout, Is.EqualTo(0), "Timeout (default)");
					Assert.That(tr.RetryLimit, Is.EqualTo(0), "RetryLimit (default)");

					tr.Timeout = 1000; // 1 sec max
					tr.RetryLimit = 5; // 5 retries max

					Assert.That(tr.Timeout, Is.EqualTo(1000), "Timeout");
					Assert.That(tr.RetryLimit, Is.EqualTo(5), "RetryLimit");
				}
			}
		}

		[Test]
		public async Task Test_Timeout_And_RetryLimit_Inherits_Default_From_Database()
		{
			using (var db = await TestHelpers.OpenTestDatabaseAsync())
			{
				Assert.That(db.DefaultTimeout, Is.EqualTo(0), "db.DefaultTimeout (default)");
				Assert.That(db.DefaultRetryLimit, Is.EqualTo(0), "db.DefaultRetryLimit (default)");

				db.DefaultTimeout = 500;
				db.DefaultRetryLimit = 3;

				Assert.That(db.DefaultTimeout, Is.EqualTo(500), "db.DefaultTimeout");
				Assert.That(db.DefaultRetryLimit, Is.EqualTo(3), "db.DefaultRetryLimit");

				// transaction should be already configured with the default options

				using (var tr = db.BeginTransaction())
				{
					Assert.That(tr.Timeout, Is.EqualTo(500), "tr.Timeout");
					Assert.That(tr.RetryLimit, Is.EqualTo(3), "tr.RetryLimit");

					// changing the default on the db should only affect new transactions

					db.DefaultTimeout = 600;
					db.DefaultRetryLimit = 4;

					using(var tr2 = db.BeginTransaction())
					{
						Assert.That(tr2.Timeout, Is.EqualTo(600), "tr2.Timeout");
						Assert.That(tr2.RetryLimit, Is.EqualTo(4), "tr2.RetryLimit");

						// original tr should not be affected
						Assert.That(tr.Timeout, Is.EqualTo(500), "tr.Timeout");
						Assert.That(tr.RetryLimit, Is.EqualTo(3), "tr.RetryLimit");
					}

				}

			}
		}

		[Test]
		public async Task Test_Can_Add_Read_Conflict_Range()
		{
			using (var db = await TestHelpers.OpenTestPartitionAsync())
			{
				var location = db.Partition("conflict");

				await db.ClearRangeAsync(location);

				var key1 = location.Pack(1);
				var key2 = location.Pack(2);

				using (var tr1 = db.BeginTransaction())
				{
					await tr1.GetAsync(key1);
					// tr1 writes to one key
					tr1.Set(key1, Slice.FromString("hello"));
					// but add the second as a conflict range
					tr1.AddReadConflictKey(key2);

					using (var tr2 = db.BeginTransaction())
					{
						// tr2 writes to the second key
						tr2.Set(key2, Slice.FromString("world"));

						// tr2 should succeed
						await tr2.CommitAsync();
					}

					// tr1 should conflict on the second key
					await TestHelpers.AssertThrowsFdbErrorAsync(
						() => tr1.CommitAsync(),
						FdbError.NotCommitted,
						"Transaction should have resulted in a conflict on key2"
					);
				}
			}
		}

		[Test]
		public async Task Test_Can_Add_Write_Conflict_Range()
		{
			using (var db = await TestHelpers.OpenTestPartitionAsync())
			{
				var location = db.Partition("conflict");

				await db.ClearRangeAsync(location);

				var keyConflict = location.Pack(0);
				var key1 = location.Pack(1);
				var key2 = location.Pack(2);

				using (var tr1 = db.BeginTransaction())
				{

					// tr1 reads the conflicting key
					await tr1.GetAsync(keyConflict);
					// and writes to key1
					tr1.Set(key1, Slice.FromString("hello"));

					using (var tr2 = db.BeginTransaction())
					{
						// tr2 changes key2, but adds a conflict range on the conflicting key
						tr2.Set(key2, Slice.FromString("world"));

						// and writes on the third
						tr2.AddWriteConflictKey(keyConflict);

						await tr2.CommitAsync();
					}

					// tr1 should conflict
					await TestHelpers.AssertThrowsFdbErrorAsync(
						() => tr1.CommitAsync(),
						FdbError.NotCommitted,
						"Transaction should have resulted in a conflict"
					);
				}
			}
		}

		[Test]
		public async Task Test_Can_Setup_And_Cancel_Watches()
		{
			using (var db = await TestHelpers.OpenTestPartitionAsync())
			{
				var location = db.Partition("test", "bigbrother");

				await db.ClearRangeAsync(location);

				var key1 = location.Pack("watched");
				var key2 = location.Pack("witness");

				await db.WriteAsync((tr) =>
				{
					tr.Set(key1, Slice.FromString("some value"));
					tr.Set(key2, Slice.FromString("some other value"));
				});

				using (var cts = new CancellationTokenSource())
				{
					FdbWatch w1;
					FdbWatch w2;

					using (var tr = db.BeginTransaction())
					{
						w1 = tr.Watch(key1, cts.Token);
						w2 = tr.Watch(key2, cts.Token);
						Assert.That(w1, Is.Not.Null);
						Assert.That(w2, Is.Not.Null);

						// note: Watches will get cancelled if the transaction is not committed !
						await tr.CommitAsync();

					}

					// Watches should survive the transaction
					await Task.Delay(100);
					Assert.That(w1.Task.Status, Is.EqualTo(TaskStatus.WaitingForActivation), "w1 should survive the transaction without being triggered");
					Assert.That(w2.Task.Status, Is.EqualTo(TaskStatus.WaitingForActivation), "w2 should survive the transaction without being triggered");

					await db.WriteAsync((tr) => tr.Set(key1, Slice.FromString("some new value")));

					// the first watch should have triggered
					await Task.Delay(100);
					Assert.That(w1.Task.Status, Is.EqualTo(TaskStatus.RanToCompletion), "w1 should have been triggered because key1 was changed");
					Assert.That(w2.Task.Status, Is.EqualTo(TaskStatus.WaitingForActivation), "w2 should still be pending because key2 was untouched");

					// cancelling the token associated to the watch should cancel them
					cts.Cancel();

					await Task.Delay(100);
					Assert.That(w2.Task.Status, Is.EqualTo(TaskStatus.Canceled), "w2 should have been cancelled");

				}

			}

		}

		[Test]
		public async Task Test_Can_Get_Addresses_For_Key()
		{
			using (var db = await TestHelpers.OpenTestPartitionAsync())
			{
				var location = db.Partition("location_api");

				await db.ClearRangeAsync(location);

				var key1 = location.Pack(1);
				var key404 = location.Pack(404);

				await db.WriteAsync((tr) =>
				{
					tr.Set(key1, Slice.FromString("one"));
				});

				// look for the address of key1
				using (var tr = db.BeginTransaction())
				{
					var addresses = await tr.GetAddressesForKeyAsync(key1);
					Assert.That(addresses, Is.Not.Null);
					Debug.WriteLine(key1.ToString() + " is stored at: " + String.Join(", ", addresses));
					Assert.That(addresses.Length, Is.GreaterThan(0));
					Assert.That(addresses[0], Is.Not.Null.Or.Empty);

					//note: it is difficult to test the returned value, because it depends on the test db configuration
					// it will most probably be 127.0.0.1 unless you have customized the Test DB settings to point to somewhere else
					// either way, it should look like a valid IP address (IPv4 or v6?)

					for (int i = 0; i < addresses.Length; i++)
					{
						System.Net.IPAddress addr;
						Assert.That(System.Net.IPAddress.TryParse(addresses[i], out addr), Is.True, "Result address {0} does not seem to be a valid IP address", addresses[i]);
					}
				}

				// do the same but for a key that does not exist
				using (var tr = db.BeginTransaction())
				{
					var addresses = await tr.GetAddressesForKeyAsync(key404);
					Assert.That(addresses, Is.Not.Null);
					Debug.WriteLine(key404.ToString() + " would be stored at: " + String.Join(", ", addresses));

					// the API still return a list of addresses, probably of servers that would store this value if you would call Set(...)

					for (int i = 0; i < addresses.Length; i++)
					{
						System.Net.IPAddress addr;
						Assert.That(System.Net.IPAddress.TryParse(addresses[i], out addr), Is.True, "Result address {0} does not seem to be a valid IP address", addresses[i]);
					}

				}
			}

		}

		[Test]
		public async Task Test_Can_Get_Boundary_Keys()
		{
			using (var db = await TestHelpers.OpenTestPartitionAsync())
			{
				using(var tr = db.BeginTransaction().WithAccessToSystemKeys())
				{
					// dump nodes
					Console.WriteLine("Server List:");
					var keys = await tr.GetRange(Fdb.SystemKeys.ServerList, Fdb.SystemKeys.ServerList + Fdb.SystemKeys.MaxValue)
						.Select(kvp => new KeyValuePair<Slice, Slice>(kvp.Key.Substring(Fdb.SystemKeys.ServerList.Count), kvp.Value))
						.ToListAsync();
					foreach (var key in keys)
					{
						// the node id seems to be at offset 8
						var nodeId = key.Value.Substring(8, 16).ToHexaString();
						// the machine id seems to be at offset 24
						var machineId = key.Value.Substring(24, 16).ToHexaString();
						// the datacenter id seems to be at offset 32
						var dataCenterId = key.Value.Substring(32, 16).ToHexaString();

						Console.WriteLine("- " + key.Key.ToHexaString() + ": (" + key.Value.Count + ") " + key.Value.ToAsciiOrHexaString());
						Console.WriteLine("  > node       = " + nodeId);
						Console.WriteLine("  > machine    = " + machineId);
						Console.WriteLine("  > datacenter = " + dataCenterId);
					}

					// dump keyServers
					Console.WriteLine("Key Servers:");
					keys = await tr.GetRange(Fdb.SystemKeys.KeyServers, Fdb.SystemKeys.KeyServers + Fdb.SystemKeys.MaxValue)
						.Select(kvp => new KeyValuePair<Slice, Slice>(kvp.Key.Substring(Fdb.SystemKeys.KeyServers.Count), kvp.Value))
						.ToListAsync();
					foreach(var key in keys)
					{
						// the node id seems to be at offset 12
						var nodeId = key.Value.Substring(12, 16).ToHexaString();

						Console.WriteLine("- (" + key.Value.Count + ") " + key.Value.ToAsciiOrHexaString() + " : " + nodeId + " = " + key.Key);
					}
				}
			}
		}

	}

}
