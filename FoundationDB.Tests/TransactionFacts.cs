#region BSD Licence
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

// ReSharper disable AccessToDisposedClosure
namespace FoundationDB.Client.Tests
{
	using NUnit.Framework;
	using System;
	using System.Collections.Generic;
	using System.Diagnostics;
	using System.Linq;
	using System.Text;
	using System.Threading;
	using System.Threading.Tasks;

	[TestFixture]
	public class TransactionFacts : FdbTest
	{

		[Test]

		public async Task Test_Can_Create_And_Dispose_Transactions()
		{
			using (var db = await OpenTestDatabaseAsync())
			{
				Assert.That(db, Is.InstanceOf<FdbDatabase>(), "This test only works directly on FdbDatabase");

				using (var tr = (FdbTransaction)db.BeginTransaction(this.Cancellation))
				{
					Assert.That(tr, Is.Not.Null, "BeginTransaction should return a valid instance");
					Assert.That(tr.State == FdbTransaction.STATE_READY, "Transaction should be in ready state");
					Assert.That(tr.StillAlive, Is.True, "Transaction should be alive");
					Assert.That(tr.Handler.IsClosed, Is.False, "Transaction handle should not be closed");
					Assert.That(tr.Database, Is.SameAs(db), "Transaction should reference the parent Database");
					Assert.That(tr.Size, Is.Zero, "Estimated size should be zero");
					Assert.That(tr.IsReadOnly, Is.False, "Transaction is not read-only");
					Assert.That(tr.IsSnapshot, Is.False, "Transaction is not in snapshot mode by default");

					// manually dispose the transaction
					tr.Dispose();

					Assert.That(tr.State == FdbTransaction.STATE_DISPOSED, "Transaction should now be in the disposed state");
					Assert.That(tr.StillAlive, Is.False, "Transaction should be not be alive anymore");
					Assert.That(tr.Handler.IsClosed, Is.True, "Transaction handle should now be closed");

					// multiple calls to dispose should not do anything more
					Assert.That(() => { tr.Dispose(); }, Throws.Nothing);
				}
			}
		}

		[Test]
		public async Task Test_Can_Get_A_Snapshot_Version_Of_A_Transaction()
		{
			using (var db = await OpenTestDatabaseAsync())
			{
				using (var tr = db.BeginTransaction(this.Cancellation))
				{
					Assert.That(tr, Is.Not.Null, "BeginTransaction should return a valid instance");
					Assert.That(tr.IsSnapshot, Is.False, "Transaction is not in snapshot mode by default");

					// verify that the snapshot version is also ok
					var snapshot = tr.Snapshot;
					Assert.That(snapshot, Is.Not.Null, "tr.Snapshot should never return null");
					Assert.That(snapshot.IsSnapshot, Is.True, "Snapshot transaction should be marked as such");
					Assert.That(tr.Snapshot, Is.SameAs(snapshot), "tr.Snapshot should not create a new instance");
					Assert.That(snapshot.Id, Is.EqualTo(tr.Id), "Snapshot transaction should have the same id as its parent");
					Assert.That(snapshot.Context, Is.SameAs(tr.Context), "Snapshot transaction should have the same context as its parent");
				}
			}
		}

		[Test]
		public async Task Test_Creating_A_ReadOnly_Transaction_Throws_When_Writing()
		{
			using (var db = await OpenTestDatabaseAsync())
			{
				using (var tr = db.BeginReadOnlyTransaction(this.Cancellation))
				{
					Assert.That(tr, Is.Not.Null);

					// reading should not fail
					await tr.GetAsync(db.Keys.Encode("Hello"));

					// any attempt to recast into a writeable transaction should fail!
					var tr2 = (IFdbTransaction)tr;
					Assert.That(tr2.IsReadOnly, Is.True, "Transaction should be marked as readonly");
					var location = db.Partition.ByKey("ReadOnly");
					Assert.That(() => tr2.Set(location.Keys.Encode("Hello"), Slice.Empty), Throws.InvalidOperationException);
					Assert.That(() => tr2.Clear(location.Keys.Encode("Hello")), Throws.InvalidOperationException);
					Assert.That(() => tr2.ClearRange(location.Keys.Encode("ABC"), location.Keys.Encode("DEF")), Throws.InvalidOperationException);
					Assert.That(() => tr2.AtomicIncrement32(location.Keys.Encode("Counter")), Throws.InvalidOperationException);
				}
			}
		}

		[Test]
		public async Task Test_Creating_Concurrent_Transactions_Are_Independent()
		{
			using (var db = await OpenTestDatabaseAsync())
			{
				IFdbTransaction tr1 = null;
				IFdbTransaction tr2 = null;
				try
				{
					// concurrent transactions should have separate FDB_FUTURE* handles

					tr1 = db.BeginTransaction(this.Cancellation);
					tr2 = db.BeginTransaction(this.Cancellation);

					Assert.That(tr1, Is.Not.Null);
					Assert.That(tr2, Is.Not.Null);

					Assert.That(tr1, Is.Not.SameAs(tr2), "Should create two different transaction objects");

					Assert.That(tr1, Is.InstanceOf<FdbTransaction>());
					Assert.That(tr2, Is.InstanceOf<FdbTransaction>());
					Assert.That(((FdbTransaction)tr1).Handler, Is.Not.EqualTo(((FdbTransaction)tr2).Handler), "Should have different FDB_FUTURE* handles");

					// disposing the first should not impact the second

					tr1.Dispose();

					Assert.That(((FdbTransaction)tr1).StillAlive, Is.False, "First transaction should be dead");
					Assert.That(((FdbTransaction)tr1).Handler.IsClosed, Is.True, "First FDB_FUTURE* handle should be closed");

					Assert.That(((FdbTransaction)tr2).StillAlive, Is.True, "Second transaction should still be alive");
					Assert.That(((FdbTransaction)tr2).Handler.IsClosed, Is.False, "Second FDB_FUTURE* handle should still be opened");
				}
				finally
				{
					tr1?.Dispose();
					tr2?.Dispose();

				}
			}
		}

		[Test]
		public async Task Test_Commiting_An_Empty_Transaction_Does_Nothing()
		{
			using (var db = await OpenTestDatabaseAsync())
			{
				using (var tr = db.BeginTransaction(this.Cancellation))
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
			using (var db = await OpenTestPartitionAsync())
			{
				using (var tr = db.BeginTransaction(this.Cancellation))
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
			using (var db = await OpenTestDatabaseAsync())
			{
				using (var tr = db.BeginTransaction(this.Cancellation))
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

			using (var db = await OpenTestPartitionAsync())
			{
				var location = db.Partition.ByKey("test");

				using (var tr = db.BeginTransaction(this.Cancellation))
				{
					tr.Set(location.Keys.Encode(1), Slice.FromString("hello"));
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

			using (var db = await OpenTestPartitionAsync())
			{
				var location = db.Partition.ByKey("test");

				await db.ClearRangeAsync(location, this.Cancellation);

				var rnd = new Random();

				using (var tr = db.BeginTransaction(this.Cancellation))
				{
					// Writes about 5 MB of stuff in 100k chunks
					for (int i = 0; i < 50; i++)
					{
						tr.Set(location.Keys.Encode(i), Slice.Random(rnd, 100 * 1000));
					}

					// start commiting
					var t = tr.CommitAsync();

					// but almost immediately cancel the transaction
					await Task.Delay(1);
					Assume.That(t.IsCompleted, Is.False, "Commit task already completed before having a chance to cancel");
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

			using (var db = await OpenTestPartitionAsync())
			{
				var location = db.Partition.ByKey("test");

				await db.ClearRangeAsync(location, this.Cancellation);

				var rnd = new Random();

				using(var cts = new CancellationTokenSource())
				using (var tr = db.BeginTransaction(cts.Token))
				{
					// Writes about 5 MB of stuff in 100k chunks
					for (int i = 0; i < 50; i++)
					{
						tr.Set(location.Keys.Encode(i), Slice.Random(rnd, 100 * 1000));
					}

					// start commiting with a cancellation token
					var t = tr.CommitAsync();

					// but almost immediately cancel the token source
					await Task.Delay(1);

					Assume.That(t.IsCompleted, Is.False, "Commit task already completed before having a chance to cancel");
					cts.Cancel();

					Assert.That(async () => await t, Throws.InstanceOf<TaskCanceledException>(), "Cancelling a token passed to CommitAsync that is still pending should cancel the task");
				}
			}
		}

		[Test]
		public async Task Test_Can_Get_Transaction_Read_Version()
		{
			using (var db = await OpenTestPartitionAsync())
			{
				using (var tr = db.BeginTransaction(this.Cancellation))
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

			using (var db = await OpenTestPartitionAsync())
			{
				long ticks = DateTime.UtcNow.Ticks;
				long writeVersion;
				long readVersion;

				var location = db.Partition.ByKey("test");

				// write a bunch of keys
				using (var tr = db.BeginTransaction(this.Cancellation))
				{
					tr.Set(location.Keys.Encode("hello"), Slice.FromString("World!"));
					tr.Set(location.Keys.Encode("timestamp"), Slice.FromInt64(ticks));
					tr.Set(location.Keys.Encode("blob"), new byte[] { 42, 123, 7 }.AsSlice());

					await tr.CommitAsync();

					writeVersion = tr.GetCommittedVersion();
					Assert.That(writeVersion, Is.GreaterThan(0), "Commited version of non-empty transaction should be > 0");
				}

				// read them back
				using (var tr = db.BeginTransaction(this.Cancellation))
				{
					Slice bytes;

					readVersion = await tr.GetReadVersionAsync();
					Assert.That(readVersion, Is.GreaterThan(0), "Read version should be > 0");

					bytes = await tr.GetAsync(location.Keys.Encode("hello")); // => 1007 "past_version"
					Assert.That(bytes.Array, Is.Not.Null);
					Assert.That(Encoding.UTF8.GetString(bytes.Array, bytes.Offset, bytes.Count), Is.EqualTo("World!"));

					bytes = await tr.GetAsync(location.Keys.Encode("timestamp"));
					Assert.That(bytes.Array, Is.Not.Null);
					Assert.That(bytes.ToInt64(), Is.EqualTo(ticks));

					bytes = await tr.GetAsync(location.Keys.Encode("blob"));
					Assert.That(bytes.Array, Is.Not.Null);
					Assert.That(bytes.Array, Is.EqualTo(new byte[] { 42, 123, 7 }));
				}

				Assert.That(readVersion, Is.GreaterThanOrEqualTo(writeVersion), "Read version should not be before previous committed version");
			}
		}

		[Test]
		public async Task Test_Can_Resolve_Key_Selector()
		{
			using (var db = await OpenTestPartitionAsync())
			{
				var location = db.Partition.ByKey("keys");
				await db.ClearRangeAsync(location, this.Cancellation);

				var minKey = location.GetPrefix() + FdbKey.MinValue;
				var maxKey = location.GetPrefix() + FdbKey.MaxValue;

				#region Insert a bunch of keys ...
				using (var tr = db.BeginTransaction(this.Cancellation))
				{
					// keys
					// - (test,) + \0
					// - (test, 0) .. (test, 19)
					// - (test,) + \xFF
					tr.Set(minKey, Slice.FromString("min"));
					for (int i = 0; i < 20; i++)
					{
						tr.Set(location.Keys.Encode(i), Slice.FromString(i.ToString()));
					}
					tr.Set(maxKey, Slice.FromString("max"));
					await tr.CommitAsync();
				}
				#endregion

				using (var tr = db.BeginTransaction(this.Cancellation))
				{
					KeySelector sel;

					// >= 0
					sel = KeySelector.FirstGreaterOrEqual(location.Keys.Encode(0));
					Assert.That(await tr.GetKeyAsync(sel), Is.EqualTo(location.Keys.Encode(0)), "fGE(0) should return 0");
					Assert.That(await tr.GetKeyAsync(sel - 1), Is.EqualTo(minKey), "fGE(0)-1 should return minKey");
					Assert.That(await tr.GetKeyAsync(sel + 1), Is.EqualTo(location.Keys.Encode(1)), "fGE(0)+1 should return 1");

					// > 0
					sel = KeySelector.FirstGreaterThan(location.Keys.Encode(0));
					Assert.That(await tr.GetKeyAsync(sel), Is.EqualTo(location.Keys.Encode(1)), "fGT(0) should return 1");
					Assert.That(await tr.GetKeyAsync(sel - 1), Is.EqualTo(location.Keys.Encode(0)), "fGT(0)-1 should return 0");
					Assert.That(await tr.GetKeyAsync(sel + 1), Is.EqualTo(location.Keys.Encode(2)), "fGT(0)+1 should return 2");

					// <= 10
					sel = KeySelector.LastLessOrEqual(location.Keys.Encode(10));
					Assert.That(await tr.GetKeyAsync(sel), Is.EqualTo(location.Keys.Encode(10)), "lLE(10) should return 10");
					Assert.That(await tr.GetKeyAsync(sel - 1), Is.EqualTo(location.Keys.Encode(9)), "lLE(10)-1 should return 9");
					Assert.That(await tr.GetKeyAsync(sel + 1), Is.EqualTo(location.Keys.Encode(11)), "lLE(10)+1 should return 11");

					// < 10
					sel = KeySelector.LastLessThan(location.Keys.Encode(10));
					Assert.That(await tr.GetKeyAsync(sel), Is.EqualTo(location.Keys.Encode(9)), "lLT(10) should return 9");
					Assert.That(await tr.GetKeyAsync(sel - 1), Is.EqualTo(location.Keys.Encode(8)), "lLT(10)-1 should return 8");
					Assert.That(await tr.GetKeyAsync(sel + 1), Is.EqualTo(location.Keys.Encode(10)), "lLT(10)+1 should return 10");

					// < 0
					sel = KeySelector.LastLessThan(location.Keys.Encode(0));
					Assert.That(await tr.GetKeyAsync(sel), Is.EqualTo(minKey), "lLT(0) should return minKey");
					Assert.That(await tr.GetKeyAsync(sel + 1), Is.EqualTo(location.Keys.Encode(0)), "lLT(0)+1 should return 0");

					// >= 20
					sel = KeySelector.FirstGreaterOrEqual(location.Keys.Encode(20));
					Assert.That(await tr.GetKeyAsync(sel), Is.EqualTo(maxKey), "fGE(20) should return maxKey");
					Assert.That(await tr.GetKeyAsync(sel - 1), Is.EqualTo(location.Keys.Encode(19)), "fGE(20)-1 should return 19");

					// > 19
					sel = KeySelector.FirstGreaterThan(location.Keys.Encode(19));
					Assert.That(await tr.GetKeyAsync(sel), Is.EqualTo(maxKey), "fGT(19) should return maxKey");
					Assert.That(await tr.GetKeyAsync(sel - 1), Is.EqualTo(location.Keys.Encode(19)), "fGT(19)-1 should return 19");
				}
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

			// note: we can't have any prefix on the keys, so open the test database in read-only mode
			var options = new FdbConnectionOptions
			{
				ClusterFile = TestHelpers.TestClusterFile,
				DbName = TestHelpers.TestDbName,
				ReadOnly = true,
			};
			using (var db = await Fdb.OpenAsync(options, this.Cancellation))
			{
				using (var tr = db.BeginReadOnlyTransaction(this.Cancellation))
				{
					// before <00>
					key = await tr.GetKeyAsync(KeySelector.LastLessThan(FdbKey.MinValue));
					Assert.That(key, Is.EqualTo(Slice.Empty), "lLT(<00>) => ''");

					// before the first key in the db
					var minKey = await tr.GetKeyAsync(KeySelector.FirstGreaterOrEqual(FdbKey.MinValue));
					Assert.That(minKey, Is.Not.Null);
					key = await tr.GetKeyAsync(KeySelector.LastLessThan(minKey));
					Assert.That(key, Is.EqualTo(Slice.Empty), "lLT(min_key) => ''");

					// after the last key in the db

					var maxKey = await tr.GetKeyAsync(KeySelector.LastLessThan(FdbKey.MaxValue));
					Assert.That(maxKey, Is.Not.Null);
					key = await tr.GetKeyAsync(KeySelector.FirstGreaterThan(maxKey));
					Assert.That(key, Is.EqualTo(FdbKey.MaxValue), "fGT(maxKey) => <FF>");

					// after <FF>
					key = await tr.GetKeyAsync(KeySelector.FirstGreaterThan(FdbKey.MaxValue));
					Assert.That(key, Is.EqualTo(FdbKey.MaxValue), "fGT(<FF>) => <FF>");
					Assert.That(async () => await tr.GetKeyAsync(KeySelector.FirstGreaterThan(FdbKey.MaxValue + FdbKey.MaxValue)), Throws.InstanceOf<FdbException>().With.Property("Code").EqualTo(FdbError.KeyOutsideLegalRange));
					Assert.That(async () => await tr.GetKeyAsync(KeySelector.LastLessThan(Fdb.System.MinValue)), Throws.InstanceOf<FdbException>().With.Property("Code").EqualTo(FdbError.KeyOutsideLegalRange));

					tr.WithReadAccessToSystemKeys();

					var firstSystemKey = await tr.GetKeyAsync(KeySelector.FirstGreaterThan(FdbKey.MaxValue));
					// usually the first key in the system space is <FF>/backupDataFormat, but that may change in the future version.
					Assert.That(firstSystemKey, Is.Not.Null);
					Assert.That(firstSystemKey, Is.GreaterThan(FdbKey.MaxValue), "key should be between <FF> and <FF><FF>");
					Assert.That(firstSystemKey, Is.LessThan(Fdb.System.MaxValue), "key should be between <FF> and <FF><FF>");

					// with access to system keys, the maximum possible key becomes <FF><FF>
					key = await tr.GetKeyAsync(KeySelector.FirstGreaterOrEqual(Fdb.System.MaxValue));
					Assert.That(key, Is.EqualTo(Fdb.System.MaxValue), "fGE(<FF><FF>) => <FF><FF> (with access to system keys)");
					key = await tr.GetKeyAsync(KeySelector.FirstGreaterThan(Fdb.System.MaxValue));
					Assert.That(key, Is.EqualTo(Fdb.System.MaxValue), "fGT(<FF><FF>) => <FF><FF> (with access to system keys)");

					key = await tr.GetKeyAsync(KeySelector.LastLessThan(Fdb.System.MinValue));
					Assert.That(key, Is.EqualTo(maxKey), "lLT(<FF><00>) => max_key (with access to system keys)");
					key = await tr.GetKeyAsync(KeySelector.FirstGreaterThan(maxKey));
					Assert.That(key, Is.EqualTo(firstSystemKey), "fGT(max_key) => first_system_key (with access to system keys)");

				}
			}

		}

		[Test]
		public async Task Test_Get_Multiple_Values()
		{
			using (var db = await OpenTestPartitionAsync())
			{

				var location = db.Partition.ByKey("Batch");
				await db.ClearRangeAsync(location, this.Cancellation);

				int[] ids = new int[] { 8, 7, 2, 9, 5, 0, 3, 4, 6, 1 };

				using (var tr = db.BeginTransaction(this.Cancellation))
				{
					for (int i = 0; i < ids.Length; i++)
					{
						tr.Set(location.Keys.Encode(i), Slice.FromString("#" + i.ToString()));
					}
					await tr.CommitAsync();
				}

				using (var tr = db.BeginTransaction(this.Cancellation))
				{
					var keys = ids.Select(id => location.Keys.Encode(id)).ToArray();

					var results = await tr.GetValuesAsync(keys);

					Assert.That(results, Is.Not.Null);
					Assert.That(results.Length, Is.EqualTo(ids.Length));

					Log(String.Join(", ", results));

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

			using(var db = await OpenTestPartitionAsync())
			{

				var location = db.Partition.ByKey("keys");
				await db.ClearRangeAsync(location, this.Cancellation);

				var minKey = location.GetPrefix() + FdbKey.MinValue;
				var maxKey = location.GetPrefix() + FdbKey.MaxValue;

				#region Insert a bunch of keys ...
				using (var tr = db.BeginTransaction(this.Cancellation))
				{
					// keys
					// - (test,) + \0
					// - (test, 0) .. (test, N-1)
					// - (test,) + \xFF
					tr.Set(minKey, Slice.FromString("min"));
					for (int i = 0; i < 20; i++)
					{
						tr.Set(location.Keys.Encode(i), Slice.FromString(i.ToString()));
					}
					tr.Set(maxKey, Slice.FromString("max"));
					await tr.CommitAsync();
				}
				#endregion

				using (var tr = db.BeginTransaction(this.Cancellation))
				{
					var selectors = Enumerable.Range(0, N).Select((i) => KeySelector.FirstGreaterOrEqual(location.Keys.Encode(i))).ToArray();

					// GetKeysAsync([])
					var results = await tr.GetKeysAsync(selectors);
					Assert.That(results, Is.Not.Null);
					Assert.That(results.Length, Is.EqualTo(20));
					for (int i = 0; i < N; i++)
					{
						Assert.That(results[i], Is.EqualTo(location.Keys.Encode(i)));
					}

					// GetKeysAsync(cast to enumerable)
					var results2 = await tr.GetKeysAsync((IEnumerable<KeySelector>)selectors);
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
				case FdbMutationType.BitAnd: expected = x & y; break;
				case FdbMutationType.BitOr: expected = x | y; break;
				case FdbMutationType.BitXor: expected = x ^ y; break;
				case FdbMutationType.Add: expected = x + y; break;
				case FdbMutationType.Max: expected = Math.Max(x, y); break;
				case FdbMutationType.Min: expected = Math.Min(x, y); break;
				default: Assert.Fail("Invalid operation type"); break;
			}

			// set key = x
			using (var tr = db.BeginTransaction(this.Cancellation))
			{
				tr.Set(key, Slice.FromFixed32(x));
				await tr.CommitAsync();
			}

			// atomic key op y
			using (var tr = db.BeginTransaction(this.Cancellation))
			{
				tr.Atomic(key, Slice.FromFixed32(y), type);
				await tr.CommitAsync();
			}

			// read key
			using (var tr = db.BeginTransaction(this.Cancellation))
			{
				var data = await tr.GetAsync(key);
				Assert.That(data.Count, Is.EqualTo(4), "data.Count");

				Assert.That(data.ToInt32(), Is.EqualTo(expected), "0x{0} {1} 0x{2} = 0x{3}", x.ToString("X8"), type.ToString(), y.ToString("X8"), expected.ToString("X8"));
			}
		}

		[Test]
		public async Task Test_Can_Perform_Atomic_Operations()
		{
			// test that we can perform atomic mutations on keys

			using (var db = await OpenTestPartitionAsync())
			{
				var location = await GetCleanDirectory(db, "test", "atomic");

				Slice key;

				key = location.Keys.Encode("add");
				await PerformAtomicOperationAndCheck(db, key, 0, FdbMutationType.Add, 0);
				await PerformAtomicOperationAndCheck(db, key, 0, FdbMutationType.Add, 1);
				await PerformAtomicOperationAndCheck(db, key, 1, FdbMutationType.Add, 0);
				await PerformAtomicOperationAndCheck(db, key, -2, FdbMutationType.Add, 1);
				await PerformAtomicOperationAndCheck(db, key, -1, FdbMutationType.Add, 1);
				await PerformAtomicOperationAndCheck(db, key, 123456789, FdbMutationType.Add, 987654321);

				key = location.Keys.Encode("and");
				await PerformAtomicOperationAndCheck(db, key, 0, FdbMutationType.BitAnd, 0);
				await PerformAtomicOperationAndCheck(db, key, 0, FdbMutationType.BitAnd, 0x018055AA);
				await PerformAtomicOperationAndCheck(db, key, -1, FdbMutationType.BitAnd, 0x018055AA);
				await PerformAtomicOperationAndCheck(db, key, 0x00FF00FF, FdbMutationType.BitAnd, 0x018055AA);
				await PerformAtomicOperationAndCheck(db, key, 0x0F0F0F0F, FdbMutationType.BitAnd, 0x018055AA);

				key = location.Keys.Encode("or");
				await PerformAtomicOperationAndCheck(db, key, 0, FdbMutationType.BitOr, 0);
				await PerformAtomicOperationAndCheck(db, key, 0, FdbMutationType.BitOr, 0x018055AA);
				await PerformAtomicOperationAndCheck(db, key, -1, FdbMutationType.BitOr, 0x018055AA);
				await PerformAtomicOperationAndCheck(db, key, 0x00FF00FF, FdbMutationType.BitOr, 0x018055AA);
				await PerformAtomicOperationAndCheck(db, key, 0x0F0F0F0F, FdbMutationType.BitOr, 0x018055AA);

				key = location.Keys.Encode("xor");
				await PerformAtomicOperationAndCheck(db, key, 0, FdbMutationType.BitXor, 0);
				await PerformAtomicOperationAndCheck(db, key, 0, FdbMutationType.BitXor, 0x018055AA);
				await PerformAtomicOperationAndCheck(db, key, -1, FdbMutationType.BitXor, 0x018055AA);
				await PerformAtomicOperationAndCheck(db, key, 0x00FF00FF, FdbMutationType.BitXor, 0x018055AA);
				await PerformAtomicOperationAndCheck(db, key, 0x0F0F0F0F, FdbMutationType.BitXor, 0x018055AA);

				if (Fdb.ApiVersion >= 300)
				{
					key = location.Keys.Encode("max");
					await PerformAtomicOperationAndCheck(db, key, 0, FdbMutationType.Max, 0);
					await PerformAtomicOperationAndCheck(db, key, 0, FdbMutationType.Max, 1);
					await PerformAtomicOperationAndCheck(db, key, 1, FdbMutationType.Max, 0);
					await PerformAtomicOperationAndCheck(db, key, 2, FdbMutationType.Max, 1);
					await PerformAtomicOperationAndCheck(db, key, 123456789, FdbMutationType.Max, 987654321);

					key = location.Keys.Encode("min");
					await PerformAtomicOperationAndCheck(db, key, 0, FdbMutationType.Min, 0);
					await PerformAtomicOperationAndCheck(db, key, 0, FdbMutationType.Min, 1);
					await PerformAtomicOperationAndCheck(db, key, 1, FdbMutationType.Min, 0);
					await PerformAtomicOperationAndCheck(db, key, 2, FdbMutationType.Min, 1);
					await PerformAtomicOperationAndCheck(db, key, 123456789, FdbMutationType.Min, 987654321);
				}
				else
				{
					// calling with an unsupported mutation type should fail
					using (var tr = db.BeginTransaction(this.Cancellation))
					{
						key = location.Keys.Encode("invalid");
						Assert.That(() => tr.Atomic(key, Slice.FromFixed32(42), FdbMutationType.Max), Throws.InstanceOf<FdbException>().With.Property("Code").EqualTo(FdbError.InvalidMutationType));
					}
				}

				// calling with an invalid mutation type should fail
				using (var tr = db.BeginTransaction(this.Cancellation))
				{
					key = location.Keys.Encode("invalid");
					Assert.That(() => tr.Atomic(key, Slice.FromFixed32(42), (FdbMutationType) 42), Throws.InstanceOf<FdbException>().With.Property("Code").EqualTo(FdbError.InvalidMutationType));
				}
			}
		}

		[Test]
		public async Task Test_Can_AtomicAdd32()
		{
			using (var db = await OpenTestPartitionAsync())
			{
				var location = db.Partition.ByKey("test", "atomic");

				// setup
				await db.ClearRangeAsync(location, this.Cancellation);
				await db.WriteAsync((tr) =>
				{
					tr.Set(location.Keys.Encode("AAA"), Slice.FromFixed32(0));
					tr.Set(location.Keys.Encode("BBB"), Slice.FromFixed32(1));
					tr.Set(location.Keys.Encode("CCC"), Slice.FromFixed32(43));
					tr.Set(location.Keys.Encode("DDD"), Slice.FromFixed32(255));
					//EEE does not exist
				}, this.Cancellation);

				// execute
				await db.WriteAsync((tr) =>
				{
					tr.AtomicAdd32(location.Keys.Encode("AAA"), 1);
					tr.AtomicAdd32(location.Keys.Encode("BBB"), 42);
					tr.AtomicAdd32(location.Keys.Encode("CCC"), -1);
					tr.AtomicAdd32(location.Keys.Encode("DDD"), 42);
					tr.AtomicAdd32(location.Keys.Encode("EEE"), 42);
				}, this.Cancellation);

				// check
				_ = await db.ReadAsync(async (tr) =>
				{
					Assert.That((await tr.GetAsync(location.Keys.Encode("AAA"))).ToHexaString(' '), Is.EqualTo("01 00 00 00"));
					Assert.That((await tr.GetAsync(location.Keys.Encode("BBB"))).ToHexaString(' '), Is.EqualTo("2B 00 00 00"));
					Assert.That((await tr.GetAsync(location.Keys.Encode("CCC"))).ToHexaString(' '), Is.EqualTo("2A 00 00 00"));
					Assert.That((await tr.GetAsync(location.Keys.Encode("DDD"))).ToHexaString(' '), Is.EqualTo("29 01 00 00"));
					Assert.That((await tr.GetAsync(location.Keys.Encode("EEE"))).ToHexaString(' '), Is.EqualTo("2A 00 00 00"));
					return 123;
				}, this.Cancellation);
			}
		}

		[Test]
		public async Task Test_Can_AtomicIncrement32()
		{
			using (var db = await OpenTestPartitionAsync())
			{
				var location = db.Partition.ByKey("test", "atomic");

				// setup
				await db.ClearRangeAsync(location, this.Cancellation);
				await db.WriteAsync((tr) =>
				{
					tr.Set(location.Keys.Encode("AAA"), Slice.FromFixed32(0));
					tr.Set(location.Keys.Encode("BBB"), Slice.FromFixed32(1));
					tr.Set(location.Keys.Encode("CCC"), Slice.FromFixed32(42));
					tr.Set(location.Keys.Encode("DDD"), Slice.FromFixed32(255));
					//EEE does not exist
				}, this.Cancellation);

				// execute
				await db.WriteAsync((tr) =>
				{
					tr.AtomicIncrement32(location.Keys.Encode("AAA"));
					tr.AtomicIncrement32(location.Keys.Encode("BBB"));
					tr.AtomicIncrement32(location.Keys.Encode("CCC"));
					tr.AtomicIncrement32(location.Keys.Encode("DDD"));
					tr.AtomicIncrement32(location.Keys.Encode("EEE"));
				}, this.Cancellation);

				// check
				_ = await db.ReadAsync(async (tr) =>
				{
					Assert.That((await tr.GetAsync(location.Keys.Encode("AAA"))).ToHexaString(' '), Is.EqualTo("01 00 00 00"));
					Assert.That((await tr.GetAsync(location.Keys.Encode("BBB"))).ToHexaString(' '), Is.EqualTo("02 00 00 00"));
					Assert.That((await tr.GetAsync(location.Keys.Encode("CCC"))).ToHexaString(' '), Is.EqualTo("2B 00 00 00"));
					Assert.That((await tr.GetAsync(location.Keys.Encode("DDD"))).ToHexaString(' '), Is.EqualTo("00 01 00 00"));
					Assert.That((await tr.GetAsync(location.Keys.Encode("EEE"))).ToHexaString(' '), Is.EqualTo("01 00 00 00"));
					return 123;
				}, this.Cancellation);
			}
		}

		[Test]
		public async Task Test_Can_AtomicAdd64()
		{
			using (var db = await OpenTestPartitionAsync())
			{
				var location = db.Partition.ByKey("test", "atomic");

				// setup
				await db.ClearRangeAsync(location, this.Cancellation);
				await db.WriteAsync((tr) =>
				{
					tr.Set(location.Keys.Encode("AAA"), Slice.FromFixed64(0));
					tr.Set(location.Keys.Encode("BBB"), Slice.FromFixed64(1));
					tr.Set(location.Keys.Encode("CCC"), Slice.FromFixed64(43));
					tr.Set(location.Keys.Encode("DDD"), Slice.FromFixed64(255));
					//EEE does not exist
				}, this.Cancellation);

				// execute
				await db.WriteAsync((tr) =>
				{
					tr.AtomicAdd64(location.Keys.Encode("AAA"), 1);
					tr.AtomicAdd64(location.Keys.Encode("BBB"), 42);
					tr.AtomicAdd64(location.Keys.Encode("CCC"), -1);
					tr.AtomicAdd64(location.Keys.Encode("DDD"), 42);
					tr.AtomicAdd64(location.Keys.Encode("EEE"), 42);
				}, this.Cancellation);

				// check
				_ = await db.ReadAsync(async (tr) =>
				{
					Assert.That((await tr.GetAsync(location.Keys.Encode("AAA"))).ToHexaString(' '), Is.EqualTo("01 00 00 00 00 00 00 00"));
					Assert.That((await tr.GetAsync(location.Keys.Encode("BBB"))).ToHexaString(' '), Is.EqualTo("2B 00 00 00 00 00 00 00"));
					Assert.That((await tr.GetAsync(location.Keys.Encode("CCC"))).ToHexaString(' '), Is.EqualTo("2A 00 00 00 00 00 00 00"));
					Assert.That((await tr.GetAsync(location.Keys.Encode("DDD"))).ToHexaString(' '), Is.EqualTo("29 01 00 00 00 00 00 00"));
					Assert.That((await tr.GetAsync(location.Keys.Encode("EEE"))).ToHexaString(' '), Is.EqualTo("2A 00 00 00 00 00 00 00"));
					return 123;
				}, this.Cancellation);
			}
		}

		[Test]
		public async Task Test_Can_AtomicIncrement64()
		{
			using (var db = await OpenTestPartitionAsync())
			{
				var location = db.Partition.ByKey("test", "atomic");

				// setup
				await db.ClearRangeAsync(location, this.Cancellation);
				await db.WriteAsync((tr) =>
				{
					tr.Set(location.Keys.Encode("AAA"), Slice.FromFixed64(0));
					tr.Set(location.Keys.Encode("BBB"), Slice.FromFixed64(1));
					tr.Set(location.Keys.Encode("CCC"), Slice.FromFixed64(42));
					tr.Set(location.Keys.Encode("DDD"), Slice.FromFixed64(255));
					//EEE does not exist
				}, this.Cancellation);

				// execute
				await db.WriteAsync((tr) =>
				{
					tr.AtomicIncrement64(location.Keys.Encode("AAA"));
					tr.AtomicIncrement64(location.Keys.Encode("BBB"));
					tr.AtomicIncrement64(location.Keys.Encode("CCC"));
					tr.AtomicIncrement64(location.Keys.Encode("DDD"));
					tr.AtomicIncrement64(location.Keys.Encode("EEE"));
				}, this.Cancellation);

				// check
				_ = await db.ReadAsync(async (tr) =>
				{
					Assert.That((await tr.GetAsync(location.Keys.Encode("AAA"))).ToHexaString(' '), Is.EqualTo("01 00 00 00 00 00 00 00"));
					Assert.That((await tr.GetAsync(location.Keys.Encode("BBB"))).ToHexaString(' '), Is.EqualTo("02 00 00 00 00 00 00 00"));
					Assert.That((await tr.GetAsync(location.Keys.Encode("CCC"))).ToHexaString(' '), Is.EqualTo("2B 00 00 00 00 00 00 00"));
					Assert.That((await tr.GetAsync(location.Keys.Encode("DDD"))).ToHexaString(' '), Is.EqualTo("00 01 00 00 00 00 00 00"));
					Assert.That((await tr.GetAsync(location.Keys.Encode("EEE"))).ToHexaString(' '), Is.EqualTo("01 00 00 00 00 00 00 00"));
					return 123;
				}, this.Cancellation);
			}
		}

		[Test]
		public async Task Test_Can_AppendIfFits()
		{
			using (var db = await OpenTestPartitionAsync())
			{
				var location = db.Partition.ByKey("test", "atomic");

				// setup
				await db.ClearRangeAsync(location, this.Cancellation);
				await db.WriteAsync((tr) =>
				{
					tr.Set(location.Keys.Encode("AAA"), Slice.Empty);
					tr.Set(location.Keys.Encode("BBB"), Slice.Repeat('B', 10));
					tr.Set(location.Keys.Encode("CCC"), Slice.Repeat('C', 90_000));
					tr.Set(location.Keys.Encode("DDD"), Slice.Repeat('D', 100_000));
					//EEE does not exist
				}, this.Cancellation);

				// execute
				await db.WriteAsync((tr) =>
				{
					tr.AtomicAppendIfFits(location.Keys.Encode("AAA"), Slice.FromString("Hello, World!"));
					tr.AtomicAppendIfFits(location.Keys.Encode("BBB"), Slice.FromString("Hello"));
					tr.AtomicAppendIfFits(location.Keys.Encode("BBB"), Slice.FromString(", World!"));
					tr.AtomicAppendIfFits(location.Keys.Encode("CCC"), Slice.Repeat('c', 10_000)); // should just fit exactly!
					tr.AtomicAppendIfFits(location.Keys.Encode("DDD"), Slice.FromString("!")); // should not fit!
					tr.AtomicAppendIfFits(location.Keys.Encode("EEE"), Slice.FromString("Hello, World!"));
				}, this.Cancellation);

				// check
				_ = await db.ReadAsync(async (tr) =>
				{
					Assert.That((await tr.GetAsync(location.Keys.Encode("AAA"))).ToString(), Is.EqualTo("Hello, World!"));
					Assert.That((await tr.GetAsync(location.Keys.Encode("BBB"))).ToString(), Is.EqualTo("BBBBBBBBBBHello, World!"));
					Assert.That((await tr.GetAsync(location.Keys.Encode("CCC"))), Is.EqualTo(Slice.Repeat('C', 90_000) + Slice.Repeat('c', 10_000)));
					Assert.That((await tr.GetAsync(location.Keys.Encode("DDD"))), Is.EqualTo(Slice.Repeat('D', 100_000)));
					Assert.That((await tr.GetAsync(location.Keys.Encode("EEE"))).ToString(), Is.EqualTo("Hello, World!"));
					return 123;
				}, this.Cancellation);
			}
		}

		[Test]
		public async Task Test_Can_Snapshot_Read()
		{

			using(var db = await OpenTestPartitionAsync())
			{
				var location = db.Partition.ByKey("test");

				await db.ClearRangeAsync(location, this.Cancellation);

				// write a bunch of keys
				await db.WriteAsync((tr) =>
				{
					tr.Set(location.Keys.Encode("hello"), Slice.FromString("World!"));
					tr.Set(location.Keys.Encode("foo"), Slice.FromString("bar"));
				}, this.Cancellation);

				// read them using snapshot
				using (var tr = db.BeginTransaction(this.Cancellation))
				{
					Slice bytes;

					bytes = await tr.Snapshot.GetAsync(location.Keys.Encode("hello"));
					Assert.That(bytes.ToUnicode(), Is.EqualTo("World!"));

					bytes = await tr.Snapshot.GetAsync(location.Keys.Encode("foo"));
					Assert.That(bytes.ToUnicode(), Is.EqualTo("bar"));

				}

			}

		}

		[Test]
		public async Task Test_CommittedVersion_On_ReadOnly_Transactions()
		{
			//note: until CommitAsync() is called, the value of the committed version is unspecified, but current implementation returns -1

			using (var db = await OpenTestPartitionAsync())
			{
				using (var tr = db.BeginTransaction(this.Cancellation))
				{
					long ver = tr.GetCommittedVersion();
					Assert.That(ver, Is.EqualTo(-1), "Initial committed version");

					var _ = await tr.GetAsync(db.Keys.Encode("foo"));

					// until the transction commits, the committed version will stay -1
					ver = tr.GetCommittedVersion();
					Assert.That(ver, Is.EqualTo(-1), "Committed version after a single read");

					// committing a read only transaction

					await tr.CommitAsync();

					ver = tr.GetCommittedVersion();
					Assert.That(ver, Is.EqualTo(-1), "Read-only comiitted transaction have a committed version of -1");
				}
			}
		}

		[Test]
		public async Task Test_CommittedVersion_On_Write_Transactions()
		{
			//note: until CommitAsync() is called, the value of the committed version is unspecified, but current implementation returns -1

			using (var db = await OpenTestPartitionAsync())
			{
				using (var tr = db.BeginTransaction(this.Cancellation))
				{
					// take the read version (to compare with the committed version below)
					long readVersion = await tr.GetReadVersionAsync();

					long ver = tr.GetCommittedVersion();
					Assert.That(ver, Is.EqualTo(-1), "Initial committed version");

					tr.Set(db.Keys.Encode("foo"), Slice.FromString("bar"));

					// until the transction commits, the committed version should still be -1
					ver = tr.GetCommittedVersion();
					Assert.That(ver, Is.EqualTo(-1), "Committed version after a single write");

					// committing a read only transaction

					await tr.CommitAsync();

					ver = tr.GetCommittedVersion();
					Assert.That(ver, Is.GreaterThanOrEqualTo(readVersion), "Committed version of write transaction should be >= the read version");
				}
			}
		}

		[Test]
		public async Task Test_CommittedVersion_After_Reset()
		{
			//note: until CommitAsync() is called, the value of the committed version is unspecified, but current implementation returns -1

			using (var db = await OpenTestPartitionAsync())
			{
				using (var tr = db.BeginTransaction(this.Cancellation))
				{
					// take the read version (to compare with the committed version below)
					long rv1 = await tr.GetReadVersionAsync();
					// do something and commit
					tr.Set(db.Keys.Encode("foo"), Slice.FromString("bar"));
					await tr.CommitAsync();
					long cv1 = tr.GetCommittedVersion();
					Log("COMMIT: {0} / {1}", rv1, cv1);
					Assert.That(cv1, Is.GreaterThanOrEqualTo(rv1), "Committed version of write transaction should be >= the read version");

					// reset the transaction
					tr.Reset();

					long rv2 = await tr.GetReadVersionAsync();
					long cv2 = tr.GetCommittedVersion();
					Log("RESET: {0} / {1}", rv2, cv2);
					//Note: the current fdb_c client does not revert the commited version to -1 ... ?
					//Assert.That(cv2, Is.EqualTo(-1), "Committed version should go back to -1 after reset");

					// read-only + commit
					await tr.GetAsync(db.Keys.Encode("foo"));
					await tr.CommitAsync();
					cv2 = tr.GetCommittedVersion();
					Log("COMMIT2: {0} / {1}", rv2, cv2);
					Assert.That(cv2, Is.EqualTo(-1), "Committed version of read-only transaction should be -1 even the transaction was previously used to write something");

				}
			}
		}

		[Test]
		public async Task Test_Regular_Read_With_Concurrent_Change_Should_Conflict()
		{
			// see http://community.foundationdb.com/questions/490/snapshot-read-vs-non-snapshot-read/492

			using (var db = await OpenTestPartitionAsync())
			{
				var location = db.Partition.ByKey("test");

				await db.ClearRangeAsync(location, this.Cancellation);

				await db.WriteAsync((tr) =>
				{
					tr.Set(location.Keys.Encode("foo"), Slice.FromString("foo"));
				}, this.Cancellation);

				using (var trA = db.BeginTransaction(this.Cancellation))
				using (var trB = db.BeginTransaction(this.Cancellation))
				{
					// regular read
					var foo = await trA.GetAsync(location.Keys.Encode("foo"));
					trA.Set(location.Keys.Encode("foo"), Slice.FromString("bar"));

					// this will conflict with our read
					trB.Set(location.Keys.Encode("foo"), Slice.FromString("bar"));
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

			using (var db = await OpenTestPartitionAsync())
			{
				var location = db.Partition.ByKey("test");
				await db.ClearRangeAsync(location, this.Cancellation);

				await db.WriteAsync((tr) =>
				{
					tr.Set(location.Keys.Encode("foo"), Slice.FromString("foo"));
				}, this.Cancellation);

				using (var trA = db.BeginTransaction(this.Cancellation))
				using (var trB = db.BeginTransaction(this.Cancellation))
				{
					// reading with snapshot mode should not conflict
					var foo = await trA.Snapshot.GetAsync(location.Keys.Encode("foo"));
					trA.Set(location.Keys.Encode("foo"), Slice.FromString("bar"));

					// this would normally conflicts with the previous read if it wasn't a snapshot read
					trB.Set(location.Keys.Encode("foo"), Slice.FromString("bar"));
					await trB.CommitAsync();

					// should succeed
					await trA.CommitAsync();
				}
			}

		}

		[Test]
		public async Task Test_GetRange_With_Concurrent_Change_Should_Conflict()
		{
			using(var db = await OpenTestPartitionAsync())
			{

				var loc = db.Partition.ByKey("test");

				await db.WriteAsync((tr) =>
				{
					tr.ClearRange(loc);
					tr.Set(loc.Keys.Encode("foo", 50), Slice.FromString("fifty"));
				}, this.Cancellation);

				// we will read the first key from [0, 100), expected 50
				// but another transaction will insert 42, in effect changing the result of our range
				// => this should conflict the GetRange

				using (var tr1 = db.BeginTransaction(this.Cancellation))
				{
					// [0, 100) limit 1 => 50
					var kvp = await tr1
						.GetRange(loc.Keys.Encode("foo"), loc.Keys.Encode("foo", 100))
						.FirstOrDefaultAsync();
					Assert.That(kvp.Key, Is.EqualTo(loc.Keys.Encode("foo", 50)));

					// 42 < 50 > conflict !!!
					using (var tr2 = db.BeginTransaction(this.Cancellation))
					{
						tr2.Set(loc.Keys.Encode("foo", 42), Slice.FromString("forty-two"));
						await tr2.CommitAsync();
					}

					// we need to write something to force a conflict
					tr1.Set(loc.Keys.Encode("bar"), Slice.Empty);

					await TestHelpers.AssertThrowsFdbErrorAsync(() => tr1.CommitAsync(), FdbError.NotCommitted, "The Set(42) in TR2 should have conflicted with the GetRange(0, 100) in TR1");
				}

				// if the other transaction insert something AFTER 50, then the result of our GetRange would not change (because of the implied limit = 1)
				// => this should NOT conflict the GetRange
				// note that if we write something in the range (0, 100) but AFTER 50, it should not conflict because we are doing a limit=1

				await db.WriteAsync((tr) =>
				{
					tr.ClearRange(loc);
					tr.Set(loc.Keys.Encode("foo", 50), Slice.FromString("fifty"));
				}, this.Cancellation);

				using (var tr1 = db.BeginTransaction(this.Cancellation))
				{
					// [0, 100) limit 1 => 50
					var kvp = await tr1
						.GetRange(loc.Keys.Encode("foo"), loc.Keys.Encode("foo", 100))
						.FirstOrDefaultAsync();
					Assert.That(kvp.Key, Is.EqualTo(loc.Keys.Encode("foo", 50)));

					// 77 > 50 => no conflict
					using (var tr2 = db.BeginTransaction(this.Cancellation))
					{
						tr2.Set(loc.Keys.Encode("foo", 77), Slice.FromString("docm"));
						await tr2.CommitAsync();
					}

					// we need to write something to force a conflict
					tr1.Set(loc.Keys.Encode("bar"), Slice.Empty);

					// should not conflict!
					await tr1.CommitAsync();
				}

			}
		}

		[Test]
		public async Task Test_GetKey_With_Concurrent_Change_Should_Conflict()
		{
			using (var db = await OpenTestPartitionAsync())
			{

				var loc = db.Partition.ByKey("test");

				await db.WriteAsync((tr) =>
				{
					tr.ClearRange(loc);
					tr.Set(loc.Keys.Encode("foo", 50), Slice.FromString("fifty"));
				}, this.Cancellation);

				// we will ask for the first key from >= 0, expecting 50, but if another transaction inserts something BEFORE 50, our key selector would have returned a different result, causing a conflict

				using (var tr1 = db.BeginTransaction(this.Cancellation))
				{
					// fGE{0} => 50
					var key = await tr1.GetKeyAsync(KeySelector.FirstGreaterOrEqual(loc.Keys.Encode("foo", 0)));
					Assert.That(key, Is.EqualTo(loc.Keys.Encode("foo", 50)));

					// 42 < 50 => conflict !!!
					using (var tr2 = db.BeginTransaction(this.Cancellation))
					{
						tr2.Set(loc.Keys.Encode("foo", 42), Slice.FromString("forty-two"));
						await tr2.CommitAsync();
					}

					// we need to write something to force a conflict
					tr1.Set(loc.Keys.Encode("bar"), Slice.Empty);

					await TestHelpers.AssertThrowsFdbErrorAsync(() => tr1.CommitAsync(), FdbError.NotCommitted, "The Set(42) in TR2 should have conflicted with the GetKey(fGE{0}) in TR1");
				}

				// if the other transaction insert something AFTER 50, our key selector would have stil returned the same result, and we would have any conflict

				await db.WriteAsync((tr) =>
				{
					tr.ClearRange(loc);
					tr.Set(loc.Keys.Encode("foo", 50), Slice.FromString("fifty"));
				}, this.Cancellation);

				using (var tr1 = db.BeginTransaction(this.Cancellation))
				{
					// fGE{0} => 50
					var key = await tr1.GetKeyAsync(KeySelector.FirstGreaterOrEqual(loc.Keys.Encode("foo", 0)));
					Assert.That(key, Is.EqualTo(loc.Keys.Encode("foo", 50)));

					// 77 > 50 => no conflict
					using (var tr2 = db.BeginTransaction(this.Cancellation))
					{
						tr2.Set(loc.Keys.Encode("foo", 77), Slice.FromString("docm"));
						await tr2.CommitAsync();
					}

					// we need to write something to force a conflict
					tr1.Set(loc.Keys.Encode("bar"), Slice.Empty);

					// should not conflict!
					await tr1.CommitAsync();
				}

				// but if we have an large offset in the key selector, and another transaction insert something inside the offset window, the result would be different, and it should conflict

				await db.WriteAsync((tr) =>
				{
					tr.ClearRange(loc);
					tr.Set(loc.Keys.Encode("foo", 50), Slice.FromString("fifty"));
					tr.Set(loc.Keys.Encode("foo", 100), Slice.FromString("one hundred"));
				}, this.Cancellation);

				using (var tr1 = db.BeginTransaction(this.Cancellation))
				{
					// fGE{50} + 1 => 100
					var key = await tr1.GetKeyAsync(KeySelector.FirstGreaterOrEqual(loc.Keys.Encode("foo", 50)) + 1);
					Assert.That(key, Is.EqualTo(loc.Keys.Encode("foo", 100)));

					// 77 between 50 and 100 => conflict !!!
					using (var tr2 = db.BeginTransaction(this.Cancellation))
					{
						tr2.Set(loc.Keys.Encode("foo", 77), Slice.FromString("docm"));
						await tr2.CommitAsync();
					}

					// we need to write something to force a conflict
					tr1.Set(loc.Keys.Encode("bar"), Slice.Empty);

					// should conflict!
					await TestHelpers.AssertThrowsFdbErrorAsync(() => tr1.CommitAsync(), FdbError.NotCommitted, "The Set(77) in TR2 should have conflicted with the GetKey(fGE{50} + 1) in TR1");
				}

				// does conflict arise from changes in VALUES in the database? or from changes in RESULTS to user queries ?

				await db.WriteAsync((tr) =>
				{
					tr.ClearRange(loc);
					tr.Set(loc.Keys.Encode("foo", 50), Slice.FromString("fifty"));
					tr.Set(loc.Keys.Encode("foo", 100), Slice.FromString("one hundred"));
				}, this.Cancellation);

				using (var tr1 = db.BeginTransaction(this.Cancellation))
				{
					// fGT{50} => 100
					var key = await tr1.GetKeyAsync(KeySelector.FirstGreaterThan(loc.Keys.Encode("foo", 50)));
					Assert.That(key, Is.EqualTo(loc.Keys.Encode("foo", 100)));

					// another transaction changes the VALUE of 50 and 100 (but does not change the fact that they exist nor add keys in between)
					using (var tr2 = db.BeginTransaction(this.Cancellation))
					{
						tr2.Set(loc.Keys.Encode("foo", 100), Slice.FromString("cent"));
						await tr2.CommitAsync();
					}

					// we need to write something to force a conflict
					tr1.Set(loc.Keys.Encode("bar"), Slice.Empty);

					// this causes a conflict in the current version of FDB
					await TestHelpers.AssertThrowsFdbErrorAsync(() => tr1.CommitAsync(), FdbError.NotCommitted, "The Set(100) in TR2 should have conflicted with the GetKey(fGT{50}) in TR1");
				}

				// LastLessThan does not create conflicts if the pivot key is changed

				await db.WriteAsync((tr) =>
				{
					tr.ClearRange(loc);
					tr.Set(loc.Keys.Encode("foo", 50), Slice.FromString("fifty"));
					tr.Set(loc.Keys.Encode("foo", 100), Slice.FromString("one hundred"));
				}, this.Cancellation);

				using (var tr1 = db.BeginTransaction(this.Cancellation))
				{
					// lLT{100} => 50
					var key = await tr1.GetKeyAsync(KeySelector.LastLessThan(loc.Keys.Encode("foo", 100)));
					Assert.That(key, Is.EqualTo(loc.Keys.Encode("foo", 50)));

					// another transaction changes the VALUE of 50 and 100 (but does not change the fact that they exist nor add keys in between)
					using (var tr2 = db.BeginTransaction(this.Cancellation))
					{
						tr2.Clear(loc.Keys.Encode("foo", 100));
						await tr2.CommitAsync();
					}

					// we need to write something to force a conflict
					tr1.Set(loc.Keys.Encode("bar"), Slice.Empty);

					// this causes a conflict in the current version of FDB
					await tr1.CommitAsync();
				}

			}
		}

		[Test]
		public async Task Test_Read_Isolation()
		{
			// > initial state: A = 1
			// > T1 starts
			// > T1 gets read_version
			// >				> T2 starts
			// >				> T2 set A = 2
			// >				> T2 commits successfully
			// > T1 reads A
			// > T1 commits

			// T1 should see A == 1, because it was started before T2

			using (var db = await OpenTestPartitionAsync())
			{
				var location = db.Partition.ByKey("test");
				var key = location.Keys.Encode("A");

				await db.ClearRangeAsync(location, this.Cancellation);

				await db.WriteAsync((tr) => tr.Set(key, Slice.FromInt32(1)), this.Cancellation);
				using(var tr1 = db.BeginTransaction(this.Cancellation))
				{
					// make sure that T1 has seen the db BEFORE T2 gets executed, or else it will not really be initialized until after the first read or commit
					await tr1.GetReadVersionAsync();
					//T1 should be locked to a specific version of the db

					// change the value in T2
					await db.WriteAsync((tr) => tr.Set(key, Slice.FromInt32(2)), this.Cancellation);

					// read the value in T1 and commits
					var value = await tr1.GetAsync(key);

					Assert.That(value, Is.Not.Null);
					Assert.That(value.ToInt32(), Is.EqualTo(1), "T1 should NOT have seen the value modified by T2");

					// committing should not conflict, because we read the value AFTER it was changed
					await tr1.CommitAsync();
				}

				// If we do the same thing, but this time without get GetReadVersion(), then T1 should see the change made by T2 because it's actual start is delayed

				// > initial state: A = 1
				// > T1 starts
				// >				> T2 starts
				// >				> T2 set A = 2
				// >				> T2 commits successfully
				// > T1 reads A
				// > T1 commits

				// T1 should see A == 2, because in reality, it was started after T2
				await db.WriteAsync((tr) => tr.Set(key, Slice.FromInt32(1)), this.Cancellation);
				using (var tr1 = db.BeginTransaction(this.Cancellation))
				{
					//do NOT use T1 yet

					// change the value in T2
					await db.WriteAsync((tr) => tr.Set(key, Slice.FromInt32(2)), this.Cancellation);

					// read the value in T1 and commits
					var value = await tr1.GetAsync(key);

					Assert.That(value, Is.Not.Null);
					Assert.That(value.ToInt32(), Is.EqualTo(2), "T1 should have seen the value modified by T2");

					// committing should not conflict, because we read the value AFTER it was changed
					await tr1.CommitAsync();
				}

			}
		}

		[Test]
		public async Task Test_Read_Isolation_From_Writes()
		{
			// By default:
			// - Regular reads see the writes made by the transaction itself, but not the writes made by other transactions that committed in between
			// - Snapshot reads never see the writes made since the transaction read version, but will see the writes made by the transaction itself

			using (var db = await OpenTestPartitionAsync())
			{
				var location = db.Partition.ByKey("test");
				await db.ClearRangeAsync(location, this.Cancellation);

				var A = location.Keys.Encode("A");
				var B = location.Keys.Encode("B");
				var C = location.Keys.Encode("C");
				var D = location.Keys.Encode("D");

				// Reads (before and after):
				// - A and B will use regular reads
				// - C and D will use snapshot reads
				// Writes:
				// - A and C will be modified by the transaction itself
				// - B and D will be modified by a different transaction

				await db.WriteAsync((tr) =>
				{
					tr.Set(A, Slice.FromString("a"));
					tr.Set(B, Slice.FromString("b"));
					tr.Set(C, Slice.FromString("c"));
					tr.Set(D, Slice.FromString("d"));
				}, this.Cancellation);

				Log("Initial db state:");
				await DumpSubspace(db, location);

				using (var tr = db.BeginTransaction(this.Cancellation))
				{

					// check initial state
					Assert.That((await tr.GetAsync(A)).ToStringUtf8(), Is.EqualTo("a"));
					Assert.That((await tr.GetAsync(B)).ToStringUtf8(), Is.EqualTo("b"));
					Assert.That((await tr.Snapshot.GetAsync(C)).ToStringUtf8(), Is.EqualTo("c"));
					Assert.That((await tr.Snapshot.GetAsync(D)).ToStringUtf8(), Is.EqualTo("d"));

					// mutate (not yet comitted)
					tr.Set(A, Slice.FromString("aa"));
					tr.Set(C, Slice.FromString("cc"));
					await db.WriteAsync((tr2) =>
					{ // have another transaction change B and D under our nose
						tr2.Set(B, Slice.FromString("bb"));
						tr2.Set(D, Slice.FromString("dd"));
					}, this.Cancellation);

					// check what the transaction sees
					Assert.That((await tr.GetAsync(A)).ToStringUtf8(), Is.EqualTo("aa"), "The transaction own writes should change the value of regular reads");
					Assert.That((await tr.GetAsync(B)).ToStringUtf8(), Is.EqualTo("b"), "Other transaction writes should not change the value of regular reads");
					Assert.That((await tr.Snapshot.GetAsync(C)).ToStringUtf8(), Is.EqualTo("cc"), "The transaction own writes should be visible in snapshot reads");
					Assert.That((await tr.Snapshot.GetAsync(D)).ToStringUtf8(), Is.EqualTo("d"), "Other transaction writes should not change the value of snapshot reads");

					//note: committing here would conflict
				}
			}
		}

		[Test]
		public async Task Test_Read_Isolation_From_Writes_Pre_300()
		{
			// By in API v200 and below:
			// - Regular reads see the writes made by the transaction itself, but not the writes made by other transactions that committed in between
			// - Snapshot reads never see the writes made since the transaction read version, but will see the writes made by the transaction itself
			// In API 300, this can be emulated by setting the SnapshotReadYourWriteDisable options

			using (var db = await OpenTestPartitionAsync())
			{
				var location = db.Partition.ByKey("test");
				await db.ClearRangeAsync(location, this.Cancellation);

				var A = location.Keys.Encode("A");
				var B = location.Keys.Encode("B");
				var C = location.Keys.Encode("C");
				var D = location.Keys.Encode("D");

				// Reads (before and after):
				// - A and B will use regular reads
				// - C and D will use snapshot reads
				// Writes:
				// - A and C will be modified by the transaction itself
				// - B and D will be modified by a different transaction

				await db.WriteAsync((tr) =>
				{
					tr.Set(A, Slice.FromString("a"));
					tr.Set(B, Slice.FromString("b"));
					tr.Set(C, Slice.FromString("c"));
					tr.Set(D, Slice.FromString("d"));
				}, this.Cancellation);

				Log("Initial db state:");
				await DumpSubspace(db, location);

				using (var tr = db.BeginTransaction(this.Cancellation))
				{
					tr.SetOption(FdbTransactionOption.SnapshotReadYourWriteDisable);

					// check initial state
					Assert.That((await tr.GetAsync(A)).ToStringUtf8(), Is.EqualTo("a"));
					Assert.That((await tr.GetAsync(B)).ToStringUtf8(), Is.EqualTo("b"));
					Assert.That((await tr.Snapshot.GetAsync(C)).ToStringUtf8(), Is.EqualTo("c"));
					Assert.That((await tr.Snapshot.GetAsync(D)).ToStringUtf8(), Is.EqualTo("d"));

					// mutate (not yet comitted)
					tr.Set(A, Slice.FromString("aa"));
					tr.Set(C, Slice.FromString("cc"));
					await db.WriteAsync((tr2) =>
					{ // have another transaction change B and D under our nose
						tr2.Set(B, Slice.FromString("bb"));
						tr2.Set(D, Slice.FromString("dd"));
					}, this.Cancellation);

					// check what the transaction sees
					Assert.That((await tr.GetAsync(A)).ToStringUtf8(), Is.EqualTo("aa"), "The transaction own writes should change the value of regular reads");
					Assert.That((await tr.GetAsync(B)).ToStringUtf8(), Is.EqualTo("b"), "Other transaction writes should not change the value of regular reads");
					//FAIL: test fails here because we read "CC" ??
					Assert.That((await tr.Snapshot.GetAsync(C)).ToStringUtf8(), Is.EqualTo("c"), "The transaction own writes should not change the value of snapshot reads");
					Assert.That((await tr.Snapshot.GetAsync(D)).ToStringUtf8(), Is.EqualTo("d"), "Other transaction writes should not change the value of snapshot reads");

					//note: committing here would conflict
				}
			}
		}

		[Test]
		public async Task Test_ReadYourWritesDisable_Isolation()
		{
			using (var db = await OpenTestPartitionAsync())
			{
				var location = db.Partition.ByKey("test");
				await db.ClearRangeAsync(location, this.Cancellation);

				var a = location.Keys.Encode("A");
				var b = location.Partition.ByKey("B");

				#region Default behaviour...

				// By default, a transaction see its own writes with non-snapshot reads

				await db.WriteAsync((tr) =>
				{
					tr.Set(a, Slice.FromString("a"));
					tr.Set(b.Keys.Encode(10), Slice.FromString("PRINT \"HELLO\""));
					tr.Set(b.Keys.Encode(20), Slice.FromString("GOTO 10"));
				}, this.Cancellation);

				using(var tr = db.BeginTransaction(this.Cancellation))
				{
					var data = await tr.GetAsync(a);
					Assert.That(data.ToUnicode(), Is.EqualTo("a"));
					var res = await tr.GetRange(b.Keys.ToRange()).Select(kvp => kvp.Value.ToString()).ToArrayAsync();
					Assert.That(res, Is.EqualTo(new [] { "PRINT \"HELLO\"", "GOTO 10" }));

					tr.Set(a, Slice.FromString("aa"));
					tr.Set(b.Keys.Encode(15), Slice.FromString("PRINT \"WORLD\""));

					data = await tr.GetAsync(a);
					Assert.That(data.ToUnicode(), Is.EqualTo("aa"), "The transaction own writes should be visible by default");
					res = await tr.GetRange(b.Keys.ToRange()).Select(kvp => kvp.Value.ToString()).ToArrayAsync();
					Assert.That(res, Is.EqualTo(new[] { "PRINT \"HELLO\"", "PRINT \"WORLD\"", "GOTO 10" }), "The transaction own writes should be visible by default");

					//note: don't commit
				}

				#endregion

				#region ReadYourWritesDisable behaviour...

				// The ReadYourWritesDisable option cause reads to always return the value in the database

				using (var tr = db.BeginTransaction(this.Cancellation))
				{
					tr.SetOption(FdbTransactionOption.ReadYourWritesDisable);

					var data = await tr.GetAsync(a);
					Assert.That(data.ToUnicode(), Is.EqualTo("a"));
					var res = await tr.GetRange(b.Keys.ToRange()).Select(kvp => kvp.Value.ToString()).ToArrayAsync();
					Assert.That(res, Is.EqualTo(new[] { "PRINT \"HELLO\"", "GOTO 10" }));

					tr.Set(a, Slice.FromString("aa"));
					tr.Set(b.Keys.Encode(15), Slice.FromString("PRINT \"WORLD\""));

					data = await tr.GetAsync(a);
					Assert.That(data.ToUnicode(), Is.EqualTo("a"), "The transaction own writes should not be seen with ReadYourWritesDisable option enabled");
					res = await tr.GetRange(b.Keys.ToRange()).Select(kvp => kvp.Value.ToString()).ToArrayAsync();
					Assert.That(res, Is.EqualTo(new[] { "PRINT \"HELLO\"", "GOTO 10" }), "The transaction own writes should not be seen with ReadYourWritesDisable option enabled");

					//note: don't commit
				}

				#endregion
			}
		}

		[Test]
		public async Task Test_Can_Set_Read_Version()
		{
			// Verify that we can set a read version on a transaction
			// * tr1 will set value to 1
			// * tr2 will set value to 2
			// * tr3 will SetReadVersion(TR1.CommittedVersion) and we expect it to read 1 (and not 2)

			using (var db = await OpenTestPartitionAsync())
			{
				var location = db.Partition.ByKey("test");

				long commitedVersion;

				// create first version
				using (var tr1 = db.BeginTransaction(this.Cancellation))
				{
					tr1.Set(location.Keys.Encode("concurrent"), Slice.FromByte(1));
					await tr1.CommitAsync();

					// get this version
					commitedVersion = tr1.GetCommittedVersion();
				}

				// mutate in another transaction
				using (var tr2 = db.BeginTransaction(this.Cancellation))
				{
					tr2.Set(location.Keys.Encode("concurrent"), Slice.FromByte(2));
					await tr2.CommitAsync();
				}

				// read the value with TR1's commited version
				using (var tr3 = db.BeginTransaction(this.Cancellation))
				{
					tr3.SetReadVersion(commitedVersion);

					long ver = await tr3.GetReadVersionAsync();
					Assert.That(ver, Is.EqualTo(commitedVersion), "GetReadVersion should return the same value as SetReadVersion!");

					var bytes = await tr3.GetAsync(location.Keys.Encode("concurrent"));

					Assert.That(bytes.GetBytes(), Is.EqualTo(new byte[] { 1 }), "Should have seen the first version!");
				}

			}

		}

		[Test]
		public async Task Test_Has_Access_To_System_Keys()
		{
			using (var db = await OpenTestPartitionAsync())
			{

				using (var tr = db.BeginTransaction(this.Cancellation))
				{

					// should fail if access to system keys has not been requested

					await TestHelpers.AssertThrowsFdbErrorAsync(
						() => tr.GetRange(Slice.FromByteString("\xFF"), Slice.FromByteString("\xFF\xFF"), new FdbRangeOptions { Limit = 10 }).ToListAsync(),
						FdbError.KeyOutsideLegalRange,
						"Should not have access to system keys by default"
					);

					// should succeed once system access has been requested
					tr.WithReadAccessToSystemKeys();

					var keys = await tr.GetRange(Slice.FromByteString("\xFF"), Slice.FromByteString("\xFF\xFF"), new FdbRangeOptions { Limit = 10 }).ToListAsync();
					Assert.That(keys, Is.Not.Null);
				}

			}
		}

		[Test]
		public async Task Test_Can_Set_Timeout_And_RetryLimit()
		{
			using (var db = await OpenTestDatabaseAsync())
			{
				using (var tr = db.BeginTransaction(this.Cancellation))
				{
					Assert.That(tr.Timeout, Is.EqualTo(15000), "Timeout (default)");
					Assert.That(tr.RetryLimit, Is.Zero, "RetryLimit (default)");
					Assert.That(tr.MaxRetryDelay, Is.Zero, "MaxRetryDelay (default)");

					tr.Timeout = 1000; // 1 sec max
					tr.RetryLimit = 5; // 5 retries max
					tr.MaxRetryDelay = 500; // .5 sec max

					Assert.That(tr.Timeout, Is.EqualTo(1000), "Timeout");
					Assert.That(tr.RetryLimit, Is.EqualTo(5), "RetryLimit");
					Assert.That(tr.MaxRetryDelay, Is.EqualTo(500), "MaxRetryDelay");
				}
			}
		}

		[Test]
		public async Task Test_Timeout_And_RetryLimit_Inherits_Default_From_Database()
		{
			using (var db = await OpenTestDatabaseAsync())
			{
				Assert.That(db.DefaultTimeout, Is.EqualTo(15000), "db.DefaultTimeout (default)");
				Assert.That(db.DefaultRetryLimit, Is.Zero, "db.DefaultRetryLimit (default)");
				Assert.That(db.DefaultMaxRetryDelay, Is.Zero, "db.DefaultMaxRetryDelay (default)");

				db.DefaultTimeout = 500;
				db.DefaultRetryLimit = 3;
				db.DefaultMaxRetryDelay = 600;

				Assert.That(db.DefaultTimeout, Is.EqualTo(500), "db.DefaultTimeout");
				Assert.That(db.DefaultRetryLimit, Is.EqualTo(3), "db.DefaultRetryLimit");
				Assert.That(db.DefaultMaxRetryDelay, Is.EqualTo(600), "db.DefaultMaxRetryDelay");

				// transaction should be already configured with the default options

				using (var tr = db.BeginTransaction(this.Cancellation))
				{
					Assert.That(tr.Timeout, Is.EqualTo(500), "tr.Timeout");
					Assert.That(tr.RetryLimit, Is.EqualTo(3), "tr.RetryLimit");
					Assert.That(tr.MaxRetryDelay, Is.EqualTo(600), "tr.MaxRetryDelay");

					// changing the default on the db should only affect new transactions

					db.DefaultTimeout = 600;
					db.DefaultRetryLimit = 4;
					db.DefaultMaxRetryDelay = 700;

					using (var tr2 = db.BeginTransaction(this.Cancellation))
					{
						Assert.That(tr2.Timeout, Is.EqualTo(600), "tr2.Timeout");
						Assert.That(tr2.RetryLimit, Is.EqualTo(4), "tr2.RetryLimit");
						Assert.That(tr2.MaxRetryDelay, Is.EqualTo(700), "tr2.MaxRetryDelay");

						// original tr should not be affected
						Assert.That(tr.Timeout, Is.EqualTo(500), "tr.Timeout");
						Assert.That(tr.RetryLimit, Is.EqualTo(3), "tr.RetryLimit");
						Assert.That(tr.MaxRetryDelay, Is.EqualTo(600), "tr.MaxRetryDelay");
					}

				}

			}
		}

		[Test]
		public async Task Test_Transaction_RetryLoop_Respects_DefaultRetryLimit_Value()
		{
			using (var db = await OpenTestDatabaseAsync())
			using (var go = new CancellationTokenSource())
			{
				Assert.That(db.DefaultTimeout, Is.EqualTo(15000), "db.DefaultTimeout (default)");
				Assert.That(db.DefaultRetryLimit, Is.Zero, "db.DefaultRetryLimit (default)");

				// By default, a transaction that gets reset or retried, clears the RetryLimit and Timeout settings, which needs to be reset everytime.
				// But if the DefaultRetryLimit and DefaultTimeout are set on the database instance, they should automatically be re-applied inside transaction loops!
				db.DefaultRetryLimit = 3;

				int counter = 0;
				var t = db.ReadAsync<int>((tr) =>
				{
					++counter;
					Log("Called {0} time(s)", counter);
					if (counter > 4)
					{
						go.Cancel();
						tr.Context.Abort = true;
						Assert.Fail("The retry loop was called too many times!");
					}

					Assert.That(tr.RetryLimit, Is.EqualTo(3));

					// simulate a retryable error condition
					throw new FdbException(FdbError.PastVersion);
				}, go.Token);

				try
				{
					await t;
					Assert.Fail("Should have failed!");
				}
				catch (AssertionException) { throw; }
				catch (Exception e)
				{
					Assert.That(e, Is.InstanceOf<FdbException>().With.Property("Code").EqualTo(FdbError.PastVersion));
				}
				Assert.That(counter, Is.EqualTo(4), "1 first attempt + 3 retries = 4 executions");
			}
		}

		[Test]
		public async Task Test_Transaction_RetryLoop_Resets_RetryLimit_And_Timeout()
		{
			using (var db = await OpenTestDatabaseAsync())
			using (var go = new CancellationTokenSource())
			{
				using (var tr = db.BeginTransaction(this.Cancellation))
				{
					// simulate a first error
					tr.RetryLimit = 10;
					await tr.OnErrorAsync(FdbError.PastVersion);
					Assert.That(tr.RetryLimit, Is.Zero, "Retry limit should be reset");

					// simulate some more errors
					await tr.OnErrorAsync(FdbError.PastVersion);
					await tr.OnErrorAsync(FdbError.PastVersion);
					await tr.OnErrorAsync(FdbError.PastVersion);
					await tr.OnErrorAsync(FdbError.PastVersion);
					Assert.That(tr.RetryLimit, Is.Zero, "Retry limit should be reset");

					// we still haven't failed 10 times..
					tr.RetryLimit = 10;
					await tr.OnErrorAsync(FdbError.PastVersion);
					Assert.That(tr.RetryLimit, Is.Zero, "Retry limit should be reset");

					// we already have failed 6 times, so this one should abort
					tr.RetryLimit = 2; // value is too low
					Assert.That(async () => await tr.OnErrorAsync(FdbError.PastVersion), Throws.InstanceOf<FdbException>().With.Property("Code").EqualTo(FdbError.PastVersion));
				}
			}
		}

		[Test]
		public async Task Test_Can_Add_Read_Conflict_Range()
		{
			using (var db = await OpenTestPartitionAsync())
			{
				var location = db.Partition.ByKey("conflict");

				await db.ClearRangeAsync(location, this.Cancellation);

				var key1 = location.Keys.Encode(1);
				var key2 = location.Keys.Encode(2);

				using (var tr1 = db.BeginTransaction(this.Cancellation))
				{
					await tr1.GetAsync(key1);
					// tr1 writes to one key
					tr1.Set(key1, Slice.FromString("hello"));
					// but add the second as a conflict range
					tr1.AddReadConflictKey(key2);

					using (var tr2 = db.BeginTransaction(this.Cancellation))
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
			using (var db = await OpenTestPartitionAsync())
			{
				var location = db.Partition.ByKey("conflict");

				await db.ClearRangeAsync(location, this.Cancellation);

				var keyConflict = location.Keys.Encode(0);
				var key1 = location.Keys.Encode(1);
				var key2 = location.Keys.Encode(2);

				using (var tr1 = db.BeginTransaction(this.Cancellation))
				{

					// tr1 reads the conflicting key
					await tr1.GetAsync(keyConflict);
					// and writes to key1
					tr1.Set(key1, Slice.FromString("hello"));

					using (var tr2 = db.BeginTransaction(this.Cancellation))
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
			using (var db = await OpenTestPartitionAsync())
			{
				var location = db.Partition.ByKey("test", "bigbrother");

				await db.ClearRangeAsync(location, this.Cancellation);

				var key1 = location.Keys.Encode("watched");
				var key2 = location.Keys.Encode("witness");

				await db.SetValuesAsync(new []
				{
					(key1, Slice.FromString("some value")),
					(key2, Slice.FromString("some other value")),
				}, this.Cancellation);

				using (var cts = new CancellationTokenSource())
				{
					FdbWatch w1;
					FdbWatch w2;

					using (var tr = db.BeginTransaction(this.Cancellation))
					{
						w1 = tr.Watch(key1, cts.Token);
						w2 = tr.Watch(key2, cts.Token);
						Assert.That(w1, Is.Not.Null);
						Assert.That(w2, Is.Not.Null);

						// note: Watches will get cancelled if the transaction is not committed !
						await tr.CommitAsync();
					}

					// Watches should survive the transaction
					await Task.Delay(100, this.Cancellation);
					Assert.That(w1.Task.Status, Is.EqualTo(TaskStatus.WaitingForActivation), "w1 should survive the transaction without being triggered");
					Assert.That(w2.Task.Status, Is.EqualTo(TaskStatus.WaitingForActivation), "w2 should survive the transaction without being triggered");

					await db.WriteAsync((tr) => tr.Set(key1, Slice.FromString("some new value")), this.Cancellation);

					// the first watch should have triggered
					await Task.Delay(100, this.Cancellation);
					Assert.That(w1.Task.Status, Is.EqualTo(TaskStatus.RanToCompletion), "w1 should have been triggered because key1 was changed");
					Assert.That(w2.Task.Status, Is.EqualTo(TaskStatus.WaitingForActivation), "w2 should still be pending because key2 was untouched");

					// cancelling the token associated to the watch should cancel them
					cts.Cancel();

					await Task.Delay(100, this.Cancellation);
					Assert.That(w2.Task.Status, Is.EqualTo(TaskStatus.Canceled), "w2 should have been cancelled");
				}
			}
		}

		[Test]
		public async Task Test_Cannot_Use_Transaction_CancellationToken_With_Watch()
		{
			// tr.Watch(..., tr.Cancellation) is forbidden, because the watch would not survive the transaction

			using (var db = await OpenTestPartitionAsync())
			{
				using (var tr = db.BeginTransaction(this.Cancellation))
				{
					var location = db.Partition.ByKey("test", "bigbrother");

					await db.ClearRangeAsync(location, this.Cancellation);

					var key = location.Keys.Encode("watched");

					Assert.That(() => tr.Watch(key, tr.Cancellation), Throws.Exception, "Watch(...) should reject the transaction's own cancellation");

					// should accept the same token used for the retry loop
					var w = tr.Watch(key, this.Cancellation);
					Assert.That(w, Is.Not.Null);
					w.Cancel();

					// should accept CancellationToken.None
					w = tr.Watch(key, this.Cancellation);
					Assert.That(w, Is.Not.Null);
					w.Cancel();

					// should accept some other cancellation token
					using (var cts = new CancellationTokenSource())
					{
						w = tr.Watch(key, cts.Token);
						Assert.That(w, Is.Not.Null);
						w.Cancel();
					}
				}
			}
		}

		[Test]
		public async Task Test_Setting_Key_To_Same_Value_Should_Not_Trigger_Watch()
		{
			using (var db = await OpenTestPartitionAsync())
			{
				var location = db.Partition.ByKey("test", "bigbrother");

				await db.ClearRangeAsync(location, this.Cancellation);

				var key = location.Keys.Encode("watched");

				Log("Set to initial value...");
				await db.SetAsync(key, Slice.FromString("initial value"), this.Cancellation);

				Log("Create watch...");
				var w = await db.ReadWriteAsync(tr => tr.Watch(key, this.Cancellation), this.Cancellation);
				Assert.That(w.IsAlive, Is.True, "Watch should still be alive");
				Assert.That(w.Task.Status, Is.EqualTo(TaskStatus.WaitingForActivation));

				// change the key to the same value
				Log("Set to same value...");
				await db.SetAsync(key, Slice.FromString("initial value"), this.Cancellation);

				//note: it is difficult to verify something "that should never happen"
				// let's say that 1sec is a good approximation of an inifinite time
				Log("Watch should not fire");
				await Task.WhenAny(w.Task, Task.Delay(1_000, this.Cancellation));
				Assert.That(w.IsAlive, Is.True, "Watch should still be active");
				Assert.That(w.Task.Status, Is.EqualTo(TaskStatus.WaitingForActivation));

				// now really change the value
				Log("Set to a different value...");
				await db.SetAsync(key, Slice.FromString("new value"), this.Cancellation);

				Log("Watch should fire...");
				await Task.WhenAny(w.Task, Task.Delay(1_000, this.Cancellation));
				if (!w.Task.IsCompleted)
				{
					Assert.That(w.Task.Status, Is.EqualTo(TaskStatus.RanToCompletion), "Watch should have fired by now!");
				}
				else
				{
					await w;
				}
			}
		}

		[Test]
		public async Task Test_Can_Get_Addresses_For_Key()
		{
			using (var db = await OpenTestPartitionAsync())
			{
				var location = db.Partition.ByKey("location_api");

				await db.ClearRangeAsync(location, this.Cancellation);

				var key1 = location.Keys.Encode(1);
				var key404 = location.Keys.Encode(404);

				await db.WriteAsync((tr) =>
				{
					tr.Set(key1, Slice.FromString("one"));
				}, this.Cancellation);

				// look for the address of key1
				using (var tr = db.BeginTransaction(this.Cancellation))
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
				using (var tr = db.BeginTransaction(this.Cancellation))
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
			var options = new FdbConnectionOptions
			{
				ClusterFile = TestHelpers.TestClusterFile,
				DbName = TestHelpers.TestDbName
			};
			using (var db = await Fdb.OpenAsync(options, this.Cancellation))
			{
				//var cf = await db.GetCoordinatorsAsync();
				//Log("Connected to {0}", cf.ToString());

				using (var tr = db.BeginReadOnlyTransaction(this.Cancellation).WithReadAccessToSystemKeys())
				{
					// dump nodes
					Log("Server List:");
					var servers = await tr.GetRange(Fdb.System.ServerList, Fdb.System.ServerList + Fdb.System.MaxValue)
						.Select(kvp => new KeyValuePair<Slice, Slice>(kvp.Key.Substring(Fdb.System.ServerList.Count), kvp.Value))
						.ToListAsync();
					foreach (var key in servers)
					{
						// the node id seems to be at offset 8
						var nodeId = key.Value.Substring(8, 16).ToHexaString();
						// the machine id seems to be at offset 24
						var machineId = key.Value.Substring(24, 16).ToHexaString();
						// the datacenter id seems to be at offset 40
						var dataCenterId = key.Value.Substring(40, 16).ToHexaString();

						Log("- {0:X} : ({1}) {2:P}", key.Key, key.Value.Count, key.Value);
						Log("  > node       = {0}", nodeId);
						Log("  > machine    = {0}", machineId);
						Log("  > datacenter = {0}", dataCenterId);
					}
					Log();

					// dump keyServers
					var shards = await tr.GetRange(Fdb.System.KeyServers, Fdb.System.KeyServers + Fdb.System.MaxValue)
						.Select(kvp => new KeyValuePair<Slice, Slice>(kvp.Key.Substring(Fdb.System.KeyServers.Count), kvp.Value))
						.ToListAsync();
					Log("Key Servers: {0} shard(s)", shards.Count);

					HashSet<string> distinctNodes = new HashSet<string>(StringComparer.Ordinal);
					int replicationFactor = 0;
					string[] ids = null;
					foreach (var key in shards)
					{
						// - the first 12 bytes are some sort of header:
						//		- bytes 0-5 usually are 01 00 01 10 A2 00
						//		- bytes 6-7 contains 0x0FDB which is the product's signature
						//		- bytes 8-9 contains the version (02 00 for "2.0"?)
						// - they are followed by k x 16-bytes machine id where k is the replication factor of the cluster
						// - followed by 4 bytes (usually all zeroes)
						// Size should be 16 x (k + 1) bytes

						int n = (key.Value.Count - 16) >> 4;
						if (ids == null || ids.Length != n) ids = new string[n];
						for(int i=0;i<n;i++)
						{
							ids[i] = key.Value.Substring(12 + i * 16, 16).ToHexaString();
							distinctNodes.Add(ids[i]);
						}
						replicationFactor = Math.Max(replicationFactor, ids.Length);

						// the node id seems to be at offset 12

						//Log("- " + key.Value.Substring(0, 12).ToAsciiOrHexaString() + " : " + String.Join(", ", ids) + " = " + key.Key);
					}
					Log();
					Log("Distinct nodes: {0}", distinctNodes.Count);
					foreach(var machine in distinctNodes)
					{
						Log("- " + machine);
					}
					Log();
					Log("Cluster topology: {0} process(es) with {1} replication", distinctNodes.Count, replicationFactor == 1 ? "single" : replicationFactor == 2 ? "double" : replicationFactor == 3 ? "triple" : replicationFactor.ToString());
				}
			}
		}

		[Test]
		public async Task Test_Simple_Read_Transaction()
		{
			using(var db = await OpenTestDatabaseAsync())
			{
				var location = db.GlobalSpace;

				using(var tr = db.BeginTransaction(this.Cancellation))
				{
					await tr.GetReadVersionAsync();

					var a = location[Slice.FromString("A")];
					var b = location[Slice.FromString("B")];
					var c = location[Slice.FromString("C")];
					var z = location[Slice.FromString("Z")];

					//await tr.GetAsync(location.Concat(Slice.FromString("KEY")));

					//tr.Set(location.Concat(Slice.FromString("BAR")), Slice.FromString("FOO"));
					//tr.Clear(location.Concat(Slice.FromString("ZORT")));
					//tr.Clear(location.Concat(Slice.FromString("NARF")));
					tr.Set(a, Slice.FromString("BAR"));
					tr.Set(b, Slice.FromString("BAZ"));
					tr.Set(c, Slice.FromString("BAT"));
					tr.ClearRange(a, c);

					//tr.ClearRange(location.Concat(Slice.FromString("A")), location.Concat(Slice.FromString("Z")));
					//tr.Set(location.Concat(Slice.FromString("C")), Slice.Empty);

					//var slice = await tr.GetRange(location.Concat(Slice.FromString("A")), location.Concat(Slice.FromString("Z"))).FirstOrDefaultAsync();
					//Log(slice);

					//tr.AddReadConflictKey(location.Concat(Slice.FromString("READ_CONFLICT")));
					//tr.AddWriteConflictKey(location.Concat(Slice.FromString("WRITE_CONFLICT")));

					//tr.AddReadConflictRange(new KeyRange(location.Concat(Slice.FromString("D")), location.Concat(Slice.FromString("E"))));
					//tr.AddReadConflictRange(new KeyRange(location.Concat(Slice.FromString("C")), location.Concat(Slice.FromString("G"))));
					//tr.AddReadConflictRange(new KeyRange(location.Concat(Slice.FromString("B")), location.Concat(Slice.FromString("F"))));
					//tr.AddReadConflictRange(new KeyRange(location.Concat(Slice.FromString("A")), location.Concat(Slice.FromString("Z"))));


					await tr.CommitAsync();
				}

			}
		}

		[Test]
		public async Task Test_VersionStamps_Share_The_Same_Token_Per_Transaction_Attempt()
		{
			// Veryify that we can set versionstamped keys inside a transaction

			using (var db = await OpenTestDatabaseAsync())
			{
				using (var tr = db.BeginTransaction(this.Cancellation))
				{
					// should return a 80-bit incomplete stamp, using a random token
					var x = tr.CreateVersionStamp();
					Log($"> x  : {x.ToSlice():X} => {x}");
					Assert.That(x.IsIncomplete, Is.True, "Placeholder token should be incomplete");
					Assert.That(x.HasUserVersion, Is.False);
					Assert.That(x.UserVersion, Is.Zero);
					Assert.That(x.TransactionVersion >> 56, Is.EqualTo(0xFF), "Highest 8 bit of Transaction Version should be set to 1");
					Assert.That(x.TransactionOrder >> 12, Is.EqualTo(0xF), "Hight 4 bits of Transaction Order should be set to 1");

					// should return a 96-bit incomplete stamp, using a the same random token and user version 0
					var x0 = tr.CreateVersionStamp(0);
					Log($"> x0 : {x0.ToSlice():X} => {x0}");
					Assert.That(x0.IsIncomplete, Is.True, "Placeholder token should be incomplete");
					Assert.That(x0.TransactionVersion, Is.EqualTo(x.TransactionVersion), "All generated stamps by one transaction should share the random token value ");
					Assert.That(x0.TransactionOrder, Is.EqualTo(x.TransactionOrder), "All generated stamps by one transaction should share the random token value ");
					Assert.That(x0.HasUserVersion, Is.True);
					Assert.That(x0.UserVersion, Is.EqualTo(0));

					// should return a 96-bit incomplete stamp, using a the same random token and user version 1
					var x1 = tr.CreateVersionStamp(1);
					Log($"> x1 : {x1.ToSlice():X} => {x1}");
					Assert.That(x1.IsIncomplete, Is.True, "Placeholder token should be incomplete");
					Assert.That(x1.TransactionVersion, Is.EqualTo(x.TransactionVersion), "All generated stamps by one transaction should share the random token value ");
					Assert.That(x1.TransactionOrder, Is.EqualTo(x.TransactionOrder), "All generated stamps by one transaction should share the random token value ");
					Assert.That(x1.HasUserVersion, Is.True);
					Assert.That(x1.UserVersion, Is.EqualTo(1));

					// should return a 96-bit incomplete stamp, using a the same random token and user version 42
					var x42 = tr.CreateVersionStamp(42);
					Log($"> x42: {x42.ToSlice():X} => {x42}");
					Assert.That(x42.IsIncomplete, Is.True, "Placeholder token should be incomplete");
					Assert.That(x42.TransactionVersion, Is.EqualTo(x.TransactionVersion), "All generated stamps by one transaction should share the random token value ");
					Assert.That(x42.TransactionOrder, Is.EqualTo(x.TransactionOrder), "All generated stamps by one transaction should share the random token value ");
					Assert.That(x42.HasUserVersion, Is.True);
					Assert.That(x42.UserVersion, Is.EqualTo(42));

					// Reset the transaction
					// => stamps should use a new value
					Log("Reset!");
					tr.Reset();

					var y = tr.CreateVersionStamp();
					Log($"> y  : {y.ToSlice():X} => {y}'");
					Assert.That(y, Is.Not.EqualTo(x), "VersionStamps should change when a transaction is reset");

					Assert.That(y.IsIncomplete, Is.True, "Placeholder token should be incomplete");
					Assert.That(y.HasUserVersion, Is.False);
					Assert.That(y.UserVersion, Is.Zero);
					Assert.That(y.TransactionVersion >> 56, Is.EqualTo(0xFF), "Highest 8 bit of Transaction Version should be set to 1");
					Assert.That(y.TransactionOrder >> 12, Is.EqualTo(0xF), "Hight 4 bits of Transaction Order should be set to 1");

					var y42 = tr.CreateVersionStamp(42);
					Log($"> y42: {y42.ToSlice():X} => {y42}");
					Assert.That(y42.IsIncomplete, Is.True, "Placeholder token should be incomplete");
					Assert.That(y42.TransactionVersion, Is.EqualTo(y.TransactionVersion), "All generated stamps by one transaction should share the random token value ");
					Assert.That(y42.TransactionOrder, Is.EqualTo(y.TransactionOrder), "All generated stamps by one transaction should share the random token value ");
					Assert.That(y42.HasUserVersion, Is.True);
					Assert.That(y42.UserVersion, Is.EqualTo(42));
				}
			}
		}

		[Test]
		public async Task Test_VersionStamp_Operations()
		{
			// Veryify that we can set versionstamped keys inside a transaction

			using (var db = await OpenTestDatabaseAsync())
			{
				var location = db.Partition.ByKey("versionstamps");

				await db.ClearRangeAsync(location, this.Cancellation);

				VersionStamp vsActual; // will contain the actual version stamp used by the database

				Log("Inserting keys with version stamps:");
				using (var tr = db.BeginTransaction(this.Cancellation))
				{

					// should return a 80-bit incomplete stamp, using a random token
					var vs = tr.CreateVersionStamp();
					Log($"> placeholder stamp: {vs} with token '{vs.ToSlice():X}'");

					// a single key using the 80-bit stamp
					tr.SetVersionStampedKey(location.Keys.Encode("foo", vs, 123), Slice.FromString("Hello, World!"));

					// simulate a batch of 3 keys, using 96-bits stamps
					tr.SetVersionStampedKey(location.Keys.Encode("bar", tr.CreateVersionStamp(0)), Slice.FromString("Zero"));
					tr.SetVersionStampedKey(location.Keys.Encode("bar", tr.CreateVersionStamp(1)), Slice.FromString("One"));
					tr.SetVersionStampedKey(location.Keys.Encode("bar", tr.CreateVersionStamp(42)), Slice.FromString("FortyTwo"));

					// value that contain the stamp
					var val = Slice.FromString("$$$$$$$$$$Hello World!"); // '$' will be replaced by the stamp
					Log($"> {val:X}");
					tr.SetVersionStampedValue(location.Keys.Encode("baz"), val, 0);

					val = Slice.FromString("Hello,") + vs.ToSlice() + Slice.FromString(", World!"); // the middle of the value should be replaced with the VersionStamp
					Log($"> {val:X}");
					tr.SetVersionStampedValue(location.Keys.Encode("jazz"), val);

					// need to be request BEFORE the commit
					var vsTask = tr.GetVersionStampAsync();

					await tr.CommitAsync();
					Log(tr.GetCommittedVersion());

					// need to be resolved AFTER the commit
					vsActual = await vsTask;
					Log($"> actual stamp: {vsActual} with token '{vsActual.ToSlice():X}'");
				}

				await DumpSubspace(db, location);

				Log("Checking database content:");
				using (var tr = db.BeginReadOnlyTransaction(this.Cancellation))
				{
					{
						var foo = await tr.GetRange(location.Keys.EncodeRange("foo")).SingleAsync();
						Log("> Found 1 result under (foo,)");
						Log($"- {location.ExtractKey(foo.Key):K} = {foo.Value:V}");
						Assert.That(foo.Value.ToString(), Is.EqualTo("Hello, World!"));

						var t = location.Keys.Unpack(foo.Key);
						Assert.That(t.Get<string>(0), Is.EqualTo("foo"));
						Assert.That(t.Get<int>(2), Is.EqualTo(123));

						var vs = t.Get<VersionStamp>(1);
						Assert.That(vs.IsIncomplete, Is.False);
						Assert.That(vs.HasUserVersion, Is.False);
						Assert.That(vs.UserVersion, Is.Zero);
						Assert.That(vs.TransactionVersion, Is.EqualTo(vsActual.TransactionVersion));
						Assert.That(vs.TransactionOrder, Is.EqualTo(vsActual.TransactionOrder));
					}

					{
						var items = await tr.GetRange(location.Keys.EncodeRange("bar")).ToListAsync();
						Log($"> Found {items.Count} results under (bar,)");
						foreach (var item in items)
						{
							Log($"- {location.ExtractKey(item.Key):K} = {item.Value:V}");
						}

						Assert.That(items.Count, Is.EqualTo(3), "Should have found 3 keys under 'foo'");

						Assert.That(items[0].Value.ToString(), Is.EqualTo("Zero"));
						var vs0 = location.Keys.DecodeLast<VersionStamp>(items[0].Key);
						Assert.That(vs0.IsIncomplete, Is.False);
						Assert.That(vs0.HasUserVersion, Is.True);
						Assert.That(vs0.UserVersion, Is.EqualTo(0));
						Assert.That(vs0.TransactionVersion, Is.EqualTo(vsActual.TransactionVersion));
						Assert.That(vs0.TransactionOrder, Is.EqualTo(vsActual.TransactionOrder));

						Assert.That(items[1].Value.ToString(), Is.EqualTo("One"));
						var vs1 = location.Keys.DecodeLast<VersionStamp>(items[1].Key);
						Assert.That(vs1.IsIncomplete, Is.False);
						Assert.That(vs1.HasUserVersion, Is.True);
						Assert.That(vs1.UserVersion, Is.EqualTo(1));
						Assert.That(vs1.TransactionVersion, Is.EqualTo(vsActual.TransactionVersion));
						Assert.That(vs1.TransactionOrder, Is.EqualTo(vsActual.TransactionOrder));

						Assert.That(items[2].Value.ToString(), Is.EqualTo("FortyTwo"));
						var vs42 = location.Keys.DecodeLast<VersionStamp>(items[2].Key);
						Assert.That(vs42.IsIncomplete, Is.False);
						Assert.That(vs42.HasUserVersion, Is.True);
						Assert.That(vs42.UserVersion, Is.EqualTo(42));
						Assert.That(vs42.TransactionVersion, Is.EqualTo(vsActual.TransactionVersion));
						Assert.That(vs42.TransactionOrder, Is.EqualTo(vsActual.TransactionOrder));
					}

					{
						var baz = await tr.GetAsync(location.Keys.Encode("baz"));
						Log($"> {baz:X}");
						// ensure that the first 10 bytes have been overwritten with the stamp
						Assert.That(baz.Count, Is.GreaterThan(0), "Key should be present in the database");
						Assert.That(baz.StartsWith(vsActual.ToSlice()), Is.True, "The first 10 bytes should match the resolved stamp");
						Assert.That(baz.Substring(10), Is.EqualTo(Slice.FromString("Hello World!")), "The rest of the slice should be untouched");
					}
					{
						var jazz = await tr.GetAsync(location.Keys.Encode("jazz"));
						Log($"> {jazz:X}");
						// ensure that the first 10 bytes have been overwritten with the stamp
						Assert.That(jazz.Count, Is.GreaterThan(0), "Key should be present in the database");
						Assert.That(jazz.Substring(6, 10), Is.EqualTo(vsActual.ToSlice()), "The bytes 6 to 15 should match the resolved stamp");
						Assert.That(jazz.Substring(0, 6), Is.EqualTo(Slice.FromString("Hello,")), "The start of the slice should be left intact");
						Assert.That(jazz.Substring(16), Is.EqualTo(Slice.FromString(", World!")), "The end of the slice should be left intact");
					}
				}

			}
		}

		[Test, Category("LongRunning")]
		public async Task Test_BadPractice_Future_Fuzzer()
		{
#if DEBUG
			const int DURATION_SEC = 5;
#else
			const int DURATION_SEC = 20;
#endif
			const int R = 100;

			using (var db = await OpenTestDatabaseAsync())
			{
				var location = db.Partition.ByKey("Fuzzer");

				var rnd = new Random();
				int seed = rnd.Next();
				Log("Using random seeed {0}", seed);
				rnd = new Random(seed);

				await db.WriteAsync((tr) =>
				{
					for (int i = 0; i < R; i++)
					{
						tr.Set(location.Keys.Encode(i), Slice.FromInt32(i));
					}
				}, this.Cancellation);

				var start = DateTime.UtcNow;
				Log("This test will run for {0} seconds", DURATION_SEC);

				int time = 0;

				List<IFdbTransaction> m_alive = new List<IFdbTransaction>();
				var sb = new StringBuilder();
				while (DateTime.UtcNow - start < TimeSpan.FromSeconds(DURATION_SEC))
				{
					switch (rnd.Next(10))
					{
						case 0:
						{ // start a new transaction
							sb.Append('T');
							var tr = db.BeginTransaction(FdbTransactionMode.Default, this.Cancellation);
							m_alive.Add(tr);
							break;
						}
						case 1:
						{ // drop a random transaction
							if (m_alive.Count == 0) continue;
							sb.Append('L');
							int p = rnd.Next(m_alive.Count);

							m_alive.RemoveAt(p);
							//no dispose
							break;
						}
						case 2:
						{ // dispose a random transaction
							if (m_alive.Count == 0) continue;
							sb.Append('D');
							int p = rnd.Next(m_alive.Count);

							var tr = m_alive[p];
							tr.Dispose();
							m_alive.RemoveAt(p);
							break;
						}
						case 3:
						{ // GC!
							sb.Append('C');
							var tr = db.BeginTransaction(FdbTransactionMode.ReadOnly, this.Cancellation);
							m_alive.Add(tr);
							_ = await tr.GetReadVersionAsync();
							break;
						}

						case 4:
						case 5:
						case 6:
						{ // read a random value from a random transaction
							sb.Append('G');
							if (m_alive.Count == 0) break;
							int p = rnd.Next(m_alive.Count);
							var tr = m_alive[p];

							int x = rnd.Next(R);
							try
							{
								_ = await tr.GetAsync(location.Keys.Encode(x));
							}
							catch (FdbException)
							{
								sb.Append('!');
							}
							break;
						}
						case 7:
						case 8:
						case 9:
						{ // read a random value, but drop the task
							sb.Append('g');
							if (m_alive.Count == 0) break;
							int p = rnd.Next(m_alive.Count);
							var tr = m_alive[p];

							int x = rnd.Next(R);
							_ = tr.GetAsync(location.Keys.Encode(x)).ContinueWith((_) => sb.Append('!') /*BUGBUG: locking ?*/, TaskContinuationOptions.NotOnRanToCompletion);
							// => t is not stored
							break;
						}

					}
					if ((time++) % 80 == 0)
					{
						Log(sb.ToString());
						Log("State: {0}", m_alive.Count);
						sb.Clear();
						sb.Append('C');
						GC.Collect();
						GC.WaitForPendingFinalizers();
						GC.Collect();
					}

				}

				GC.Collect();
				GC.WaitForPendingFinalizers();
				GC.Collect();
			}

		}
	}
}
