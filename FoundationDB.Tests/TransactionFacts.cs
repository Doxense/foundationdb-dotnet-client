#region BSD License
/* Copyright (c) 2005-2023 Doxense SAS
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

//#define ENABLE_LOGGING

// ReSharper disable AccessToDisposedClosure
namespace FoundationDB.Client.Tests
{
	using System;
	using System.Collections.Generic;
	using System.Linq;
	using System.Net;
	using System.Text;
	using System.Threading;
	using System.Threading.Tasks;
	using Doxense.Linq;
	using NUnit.Framework;

	[TestFixture]
	public class TransactionFacts : FdbTest
	{

		[Test]
		public async Task Test_Can_Create_And_Dispose_Transactions()
		{
			using (var db = await OpenTestDatabaseAsync())
			{
				Assert.That(db, Is.InstanceOf<FdbDatabase>(), "This test only works directly on FdbDatabase");

				using (var tr = (FdbTransaction) await db.BeginTransactionAsync(this.Cancellation))
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
				using (var tr = await db.BeginTransactionAsync(this.Cancellation))
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
				db.SetDefaultLogHandler(log => Log(log.GetTimingsReport(true)));

				using (var tr = await db.BeginReadOnlyTransactionAsync(this.Cancellation))
				{
					Assert.That(tr, Is.Not.Null);

					var subspace = (await db.Root.Resolve(tr))!;

					// reading should not fail
					await tr.GetAsync(subspace.Encode("Hello"));

					// any attempt to recast into a writable transaction should fail!
					var tr2 = (IFdbTransaction) tr;
					Assert.That(tr2.IsReadOnly, Is.True, "Transaction should be marked as readonly");
					var location = subspace.Partition.ByKey("ReadOnly");
					Assert.That(() => tr2.Set(location.Encode("Hello"), Slice.Empty), Throws.InvalidOperationException);
					Assert.That(() => tr2.Clear(location.Encode("Hello")), Throws.InvalidOperationException);
					Assert.That(() => tr2.ClearRange(location.Encode("ABC"), location.Encode("DEF")), Throws.InvalidOperationException);
					Assert.That(() => tr2.AtomicIncrement32(location.Encode("Counter")), Throws.InvalidOperationException);
				}
			}
		}

		[Test]
		public async Task Test_Creating_Concurrent_Transactions_Are_Independent()
		{
			using (var db = await OpenTestDatabaseAsync())
			{
				db.SetDefaultLogHandler(log => Log(log.GetTimingsReport(true)));

				IFdbTransaction tr1 = null;
				IFdbTransaction tr2 = null;
				try
				{
					// concurrent transactions should have separate FDB_FUTURE* handles

					tr1 = await db.BeginTransactionAsync(this.Cancellation);
					tr2 = await db.BeginTransactionAsync(this.Cancellation);

					Assert.That(tr1, Is.Not.Null);
					Assert.That(tr2, Is.Not.Null);

					Assert.That(tr1, Is.Not.SameAs(tr2), "Should create two different transaction objects");

					Assert.That(tr1, Is.InstanceOf<FdbTransaction>());
					Assert.That(tr2, Is.InstanceOf<FdbTransaction>());
					Assert.That(((FdbTransaction) tr1).Handler, Is.Not.EqualTo(((FdbTransaction) tr2).Handler), "Should have different FDB_FUTURE* handles");

					// disposing the first should not impact the second

					tr1.Dispose();

					Assert.That(((FdbTransaction) tr1).StillAlive, Is.False, "First transaction should be dead");
					Assert.That(((FdbTransaction) tr1).Handler.IsClosed, Is.True, "First FDB_FUTURE* handle should be closed");

					Assert.That(((FdbTransaction) tr2).StillAlive, Is.True, "Second transaction should still be alive");
					Assert.That(((FdbTransaction) tr2).Handler.IsClosed, Is.False, "Second FDB_FUTURE* handle should still be opened");
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
				db.SetDefaultLogHandler(log => Log(log.GetTimingsReport(true)));

				using (var tr = await db.BeginTransactionAsync(this.Cancellation))
				{
					Assert.That(tr, Is.InstanceOf<FdbTransaction>());

					// do nothing with it
					await tr.CommitAsync();
					// => should not fail!

					Assert.That(((FdbTransaction) tr).StillAlive, Is.False);
					Assert.That(((FdbTransaction) tr).State, Is.EqualTo(FdbTransaction.STATE_COMMITTED));
				}
			}
		}

		[Test]
		public async Task Test_Resetting_An_Empty_Transaction_Does_Nothing()
		{
			using (var db = await OpenTestDatabaseAsync())
			{
				db.SetDefaultLogHandler(log => Log(log.GetTimingsReport(true)));

				using (var tr = await db.BeginTransactionAsync(this.Cancellation))
				{
					// do nothing with it
					tr.Reset();
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
				db.SetDefaultLogHandler(log => Log(log.GetTimingsReport(true)));

				using (var tr = await db.BeginTransactionAsync(this.Cancellation))
				{
					Assert.That(tr, Is.InstanceOf<FdbTransaction>());

					// do nothing with it
					tr.Cancel();
					// => should not fail!

					Assert.That(((FdbTransaction) tr).StillAlive, Is.False);
					Assert.That(((FdbTransaction) tr).State, Is.EqualTo(FdbTransaction.STATE_CANCELED));
				}
			}
		}

		[Test]
		public async Task Test_Cancelling_Transaction_Before_Commit_Should_Throw_Immediately()
		{
			using (var db = await OpenTestDatabaseAsync())
			{
				var location = db.Root.ByKey("test").AsTyped<int>();
				await CleanLocation(db, location);

				db.SetDefaultLogHandler(log => Log(log.GetTimingsReport(true)));

				using (var tr = await db.BeginTransactionAsync(this.Cancellation))
				{
					var subspace = (await location.Resolve(tr))!;

					tr.Set(subspace[1], Value("hello"));
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

			using (var db = await OpenTestDatabaseAsync())
			{
				var location = db.Root.ByKey("test").AsTyped<int>();
				await CleanLocation(db, location);

				var rnd = new Random();

				db.SetDefaultLogHandler(log => Log(log.GetTimingsReport(true)));

				using (var tr = await db.BeginTransactionAsync(this.Cancellation))
				{
					var subspace = (await location.Resolve(tr))!;

					// Writes about 5 MB of stuff in 100k chunks
					for (int i = 0; i < 50; i++)
					{
						tr.Set(subspace[i], Slice.Random(rnd, 100 * 1000));
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

			using (var db = await OpenTestDatabaseAsync())
			{
				var location = db.Root.ByKey("test").AsTyped<int>();
				await CleanLocation(db, location);

				db.SetDefaultLogHandler(log => Log(log.GetTimingsReport(true)));

				var rnd = new Random();

				using (var cts = new CancellationTokenSource())
				using (var tr = await db.BeginTransactionAsync(cts.Token))
				{
					var subspace = (await location.Resolve(tr))!;

					// Writes about 5 MB of stuff in 100k chunks
					for (int i = 0; i < 50; i++)
					{
						tr.Set(subspace[i], Slice.Random(rnd, 100 * 1000));
					}

					// start commiting with a cancellation token
					var t = tr.CommitAsync();

					// but almost immediately cancel the token source
					await Task.Delay(1, this.Cancellation);

					Assume.That(t.IsCompleted, Is.False, "Commit task already completed before having a chance to cancel");
					cts.Cancel();

					Assert.That(async () => await t, Throws.InstanceOf<TaskCanceledException>(), "Cancelling a token passed to CommitAsync that is still pending should cancel the task");
				}
			}
		}

		[Test]
		public async Task Test_Can_Get_Transaction_Read_Version()
		{
			using (var db = await OpenTestDatabaseAsync())
			{
				db.SetDefaultLogHandler(log => Log(log.GetTimingsReport(true)));

				using (var tr = await db.BeginTransactionAsync(this.Cancellation))
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

			using (var db = await OpenTestDatabaseAsync())
			{
				long ticks = DateTime.UtcNow.Ticks;
				long writeVersion;
				long readVersion;

				var location = db.Root.ByKey("test").AsTyped<string>();
				await CleanLocation(db, location);

				db.SetDefaultLogHandler(log => Log(log.GetTimingsReport(true)));

				// write a bunch of keys
				using (var tr = await db.BeginTransactionAsync(this.Cancellation))
				{
					var subspace = (await location.Resolve(tr))!;

					tr.Set(subspace["hello"], Value("World!"));
					tr.Set(subspace["timestamp"], Slice.FromInt64(ticks));
					tr.Set(subspace["blob"], new byte[] { 42, 123, 7 }.AsSlice());

					await tr.CommitAsync();

					writeVersion = tr.GetCommittedVersion();
					Assert.That(writeVersion, Is.GreaterThan(0), "Committed version of non-empty transaction should be > 0");
				}

				// read them back
				using (var tr = await db.BeginTransactionAsync(this.Cancellation))
				{
					var subspace = (await location.Resolve(tr))!;

					readVersion = await tr.GetReadVersionAsync();
					Assert.That(readVersion, Is.GreaterThan(0), "Read version should be > 0");

					{
						var bytes = await tr.GetAsync(subspace["hello"]); // => 1007 "past_version"
						Assert.That(bytes.Array, Is.Not.Null);
						Assert.That(Encoding.UTF8.GetString(bytes.Array, bytes.Offset, bytes.Count), Is.EqualTo("World!"));
					}
					{
						var bytes = await tr.GetAsync(subspace["timestamp"]);
						Assert.That(bytes.Array, Is.Not.Null);
						Assert.That(bytes.ToInt64(), Is.EqualTo(ticks));
					}
					{
						var bytes = await tr.GetAsync(subspace["blob"]);
						Assert.That(bytes.Array, Is.Not.Null);
						Assert.That(bytes.Array, Is.EqualTo(new byte[] { 42, 123, 7 }));
					}
				}

				Assert.That(readVersion, Is.GreaterThanOrEqualTo(writeVersion), "Read version should not be before previous committed version");
			}
		}

		[Test]
		public async Task Test_Can_Resolve_Key_Selector()
		{
			using (var db = await OpenTestDatabaseAsync())
			{
				var location = db.Root.ByKey("keys").AsTyped<int>();
				await CleanLocation(db, location);

				#region Insert a bunch of keys ...

				using (var tr = await db.BeginTransactionAsync(this.Cancellation))
				{
					var subspace = (await location.Resolve(tr))!;

					// keys
					// - (test,) + \0
					// - (test, 0) .. (test, 19)
					// - (test,) + \xFF
					tr.Set(subspace.Append(FdbKey.MinValue), Value("min"));
					for (int i = 0; i < 20; i++)
					{
						tr.Set(subspace[i], Value(i.ToString()));
					}

					tr.Set(subspace.Append(FdbKey.MaxValue), Value("max"));
					await tr.CommitAsync();
				}

				#endregion

				db.SetDefaultLogHandler(log => Log(log.GetTimingsReport(true)));

				using (var tr = await db.BeginTransactionAsync(this.Cancellation))
				{
					var subspace = (await location.Resolve(tr))!;

					KeySelector sel;

					// >= 0
					sel = KeySelector.FirstGreaterOrEqual(subspace[0]);
					Assert.That(await tr.GetKeyAsync(sel), Is.EqualTo(subspace[0]), "fGE(0) should return 0");
					Assert.That(await tr.GetKeyAsync(sel - 1), Is.EqualTo(subspace.Append(FdbKey.MinValue)), "fGE(0)-1 should return minKey");
					Assert.That(await tr.GetKeyAsync(sel + 1), Is.EqualTo(subspace[1]), "fGE(0)+1 should return 1");

					// > 0
					sel = KeySelector.FirstGreaterThan(subspace[0]);
					Assert.That(await tr.GetKeyAsync(sel), Is.EqualTo(subspace[1]), "fGT(0) should return 1");
					Assert.That(await tr.GetKeyAsync(sel - 1), Is.EqualTo(subspace[0]), "fGT(0)-1 should return 0");
					Assert.That(await tr.GetKeyAsync(sel + 1), Is.EqualTo(subspace[2]), "fGT(0)+1 should return 2");

					// <= 10
					sel = KeySelector.LastLessOrEqual(subspace[10]);
					Assert.That(await tr.GetKeyAsync(sel), Is.EqualTo(subspace[10]), "lLE(10) should return 10");
					Assert.That(await tr.GetKeyAsync(sel - 1), Is.EqualTo(subspace[9]), "lLE(10)-1 should return 9");
					Assert.That(await tr.GetKeyAsync(sel + 1), Is.EqualTo(subspace[11]), "lLE(10)+1 should return 11");

					// < 10
					sel = KeySelector.LastLessThan(subspace[10]);
					Assert.That(await tr.GetKeyAsync(sel), Is.EqualTo(subspace[9]), "lLT(10) should return 9");
					Assert.That(await tr.GetKeyAsync(sel - 1), Is.EqualTo(subspace[8]), "lLT(10)-1 should return 8");
					Assert.That(await tr.GetKeyAsync(sel + 1), Is.EqualTo(subspace[10]), "lLT(10)+1 should return 10");

					// < 0
					sel = KeySelector.LastLessThan(subspace[0]);
					Assert.That(await tr.GetKeyAsync(sel), Is.EqualTo(subspace.Append(FdbKey.MinValue)), "lLT(0) should return minKey");
					Assert.That(await tr.GetKeyAsync(sel + 1), Is.EqualTo(subspace[0]), "lLT(0)+1 should return 0");

					// >= 20
					sel = KeySelector.FirstGreaterOrEqual(subspace[20]);
					Assert.That(await tr.GetKeyAsync(sel), Is.EqualTo(subspace.Append(FdbKey.MaxValue)), "fGE(20) should return maxKey");
					Assert.That(await tr.GetKeyAsync(sel - 1), Is.EqualTo(subspace[19]), "fGE(20)-1 should return 19");

					// > 19
					sel = KeySelector.FirstGreaterThan(subspace[19]);
					Assert.That(await tr.GetKeyAsync(sel), Is.EqualTo(subspace.Append(FdbKey.MaxValue)), "fGT(19) should return maxKey");
					Assert.That(await tr.GetKeyAsync(sel - 1), Is.EqualTo(subspace[19]), "fGT(19)-1 should return 19");
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
				ReadOnly = true,
			};
			using (var db = await Fdb.OpenAsync(options, this.Cancellation))
			{
				using (var tr = await db.BeginReadOnlyTransactionAsync(this.Cancellation))
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

					tr.Options.WithReadAccessToSystemKeys();

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
			using (var db = await OpenTestDatabaseAsync())
			{
				var location = db.Root.ByKey("Batch").AsTyped<int>();
				await CleanLocation(db, location);

				var ids = new[] { 8, 7, 2, 9, 5, 0, 3, 4, 6, 1 };

				using (var tr = await db.BeginTransactionAsync(this.Cancellation))
				{
					var subspace = (await location.Resolve(tr))!;

					for (int i = 0; i < ids.Length; i++)
					{
						tr.Set(subspace[i], Value("#" + i.ToString()));
					}

					await tr.CommitAsync();
				}

				db.SetDefaultLogHandler(log => Log(log.GetTimingsReport(true)));

				using (var tr = await db.BeginTransactionAsync(this.Cancellation))
				{
					var subspace = (await location.Resolve(tr))!;

					var results = await tr.GetValuesAsync(subspace.Encode(ids));

					Assert.That(results, Is.Not.Null);
					Assert.That(results.Length, Is.EqualTo(ids.Length));

					Log(string.Join(", ", results));

					for (int i = 0; i < ids.Length; i++)
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

			using (var db = await OpenTestDatabaseAsync())
			{
				var location = db.Root.ByKey("keys").AsTyped<int>();
				await CleanLocation(db, location);

				#region Insert a bunch of keys ...

				using (var tr = await db.BeginTransactionAsync(this.Cancellation))
				{
					var subspace = (await location.Resolve(tr))!;

					// keys
					// - (test,) + \0
					// - (test, 0) .. (test, N-1)
					// - (test,) + \xFF
					tr.Set(subspace.Append(FdbKey.MinValue), Value("min"));
					for (int i = 0; i < 20; i++)
					{
						tr.Set(subspace[i], Value(i.ToString()));
					}

					tr.Set(subspace.Append(FdbKey.MaxValue), Value("max"));
					await tr.CommitAsync();
				}

				#endregion

				db.SetDefaultLogHandler(log => Log(log.GetTimingsReport(true)));

				using (var tr = await db.BeginTransactionAsync(this.Cancellation))
				{
					var subspace = (await location.Resolve(tr))!;

					var selectors = Enumerable
						.Range(0, N)
						.Select((i) => KeySelector.FirstGreaterOrEqual(subspace[i]))
						.ToArray();

					// GetKeysAsync([])
					var results = await tr.GetKeysAsync(selectors);
					Assert.That(results, Is.Not.Null);
					Assert.That(results.Length, Is.EqualTo(20));
					for (int i = 0; i < N; i++)
					{
						Assert.That(results[i], Is.EqualTo(subspace[i]));
					}

					// GetKeysAsync(cast to enumerable)
					var results2 = await tr.GetKeysAsync((IEnumerable<KeySelector>) selectors);
					Assert.That(results2, Is.EqualTo(results));

					// GetKeysAsync(real enumerable)
					var results3 = await tr.GetKeysAsync(selectors.Select(x => x));
					Assert.That(results3, Is.EqualTo(results));
				}
			}
		}

		[Test]
		public async Task Test_Can_Check_Value()
		{
			using (var db = await OpenTestDatabaseAsync())
			{
				var location = db.Root.ByKey("test").AsTyped<string>();
				await CleanLocation(db, location);

				db.SetDefaultLogHandler(log => Log(log.GetTimingsReport(true)));

				// write a bunch of keys
				await db.WriteAsync(async tr =>
				{
					var subspace = (await location.Resolve(tr))!;
					tr.Set(subspace["hello"], Value("World!"));
					tr.Set(subspace["foo"], Slice.Empty);
				}, this.Cancellation);

				async Task Check(IFdbReadOnlyTransaction tr, Slice key, Slice expected, FdbValueCheckResult result, Slice actual)
				{
					Log($"Check {key} == {expected} ?");
					var res = await tr.CheckValueAsync(key, expected);
					Log($"> [{res.Result}], {res.Actual:V}");
					Assert.That(res.Actual, Is.EqualTo(actual), "Check({0} == {1}) => ({2}, {3}).Actual was {4}", key, expected, result, actual, res.Actual);
					Assert.That(res.Result, Is.EqualTo(result), "Check({0} == {1}) => ({2}, {3}).Result was {4}", key, expected, result, actual, res.Result);
				}

				// hello should only be equal to 'World!', not any other value, empty or nil
				using (var tr = await db.BeginTransactionAsync(this.Cancellation))
				{
					var subspace = (await location.Resolve(tr))!;

					// hello should only be equal to 'World!', not any other value, empty or nil
					await Check(tr, subspace["hello"], Value("World!"), FdbValueCheckResult.Success, Value("World!"));
					await Check(tr, subspace["hello"], Value("Le Monde!"), FdbValueCheckResult.Failed, Value("World!"));
					await Check(tr, subspace["hello"], Slice.Nil, FdbValueCheckResult.Failed, Value("World!"));
					await Check(tr, subspace["hello"], subspace["hello"], FdbValueCheckResult.Failed, Value("World!"));
				}

				// foo should only be equal to Empty, *not* Nil or any other value
				using (var tr = await db.BeginTransactionAsync(this.Cancellation))
				{
					var subspace = (await location.Resolve(tr))!;

					await Check(tr, subspace["foo"], Slice.Empty, FdbValueCheckResult.Success, Slice.Empty);
					await Check(tr, subspace["foo"], Value("bar"), FdbValueCheckResult.Failed, Slice.Empty);
					await Check(tr, subspace["foo"], Slice.Nil, FdbValueCheckResult.Failed, Slice.Empty);
					await Check(tr, subspace["foo"], subspace["foo"], FdbValueCheckResult.Failed, Slice.Empty);
				}

				// not_found should only be equal to Nil, *not* Empty or any other value
				using (var tr = await db.BeginTransactionAsync(this.Cancellation))
				{
					var subspace = (await location.Resolve(tr))!;

					await Check(tr, subspace["not_found"], Slice.Nil, FdbValueCheckResult.Success, Slice.Nil);
					await Check(tr, subspace["not_found"], Slice.Empty, FdbValueCheckResult.Failed, Slice.Nil);
					await Check(tr, subspace["not_found"], subspace["not_found"], FdbValueCheckResult.Failed, Slice.Nil);
				}

				// checking, changing and checking again: 2nd check should see the modified value!
				// not_found should only be equal to Nil, *not* Empty or any other value
				using (var tr = await db.BeginTransactionAsync(this.Cancellation))
				{
					var subspace = (await location.Resolve(tr))!;

					await Check(tr, subspace["hello"], Value("World!"), FdbValueCheckResult.Success, Value("World!"));
					await Check(tr, subspace["not_found"], Slice.Nil, FdbValueCheckResult.Success, Slice.Nil);

					tr.Set(subspace["hello"], Value("Le Monde!"));
					await Check(tr, subspace["hello"], Value("Le Monde!"), FdbValueCheckResult.Success, Value("Le Monde!"));
					await Check(tr, subspace["hello"], Value("World!"), FdbValueCheckResult.Failed, Value("Le Monde!"));

					tr.Set(subspace["not_found"], Value("Surprise!"));
					await Check(tr, subspace["not_found"], Value("Surprise!"), FdbValueCheckResult.Success, Value("Surprise!"));
					await Check(tr, subspace["not_found"], Slice.Nil, FdbValueCheckResult.Failed, Value("Surprise!"));

					//note: don't commit!
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
			using (var tr = await db.BeginTransactionAsync(this.Cancellation))
			{
				tr.Set(key, Slice.FromFixed32(x));
				await tr.CommitAsync();
			}

			// atomic key op y
			using (var tr = await db.BeginTransactionAsync(this.Cancellation))
			{
				tr.Atomic(key, Slice.FromFixed32(y), type);
				await tr.CommitAsync();
			}

			// read key
			using (var tr = await db.BeginTransactionAsync(this.Cancellation))
			{
				var data = await tr.GetAsync(key);
				Assert.That(data.Count, Is.EqualTo(4), "data.Count");

				Assert.That(data.ToInt32(), Is.EqualTo(expected), "0x{0:X8} {1} 0x{2:X8} = 0x{3:X8}", x, type, y, expected);
			}
		}

		[Test]
		public async Task Test_Can_Perform_Atomic_Operations()
		{
			// test that we can perform atomic mutations on keys

			using (var db = await OpenTestDatabaseAsync())
			{
				var location = db.Root.ByKey("test", "atomic");
				await CleanLocation(db, location);

				db.SetDefaultLogHandler(log => Log(log.GetTimingsReport(true)));

				//note: we take a risk by reading the key separately, but this simplifies the rest of the code !
				Task<Slice> ResolveKey(string name) => db.ReadAsync(async tr => (await location.Resolve(tr))!.Encode(name), this.Cancellation);

				Slice key;

				key = await ResolveKey("add");
				await PerformAtomicOperationAndCheck(db, key, 0, FdbMutationType.Add, 0);
				await PerformAtomicOperationAndCheck(db, key, 0, FdbMutationType.Add, 1);
				await PerformAtomicOperationAndCheck(db, key, 1, FdbMutationType.Add, 0);
				await PerformAtomicOperationAndCheck(db, key, -2, FdbMutationType.Add, 1);
				await PerformAtomicOperationAndCheck(db, key, -1, FdbMutationType.Add, 1);
				await PerformAtomicOperationAndCheck(db, key, 123456789, FdbMutationType.Add, 987654321);

				key = await ResolveKey("and");
				await PerformAtomicOperationAndCheck(db, key, 0, FdbMutationType.BitAnd, 0);
				await PerformAtomicOperationAndCheck(db, key, 0, FdbMutationType.BitAnd, 0x018055AA);
				await PerformAtomicOperationAndCheck(db, key, -1, FdbMutationType.BitAnd, 0x018055AA);
				await PerformAtomicOperationAndCheck(db, key, 0x00FF00FF, FdbMutationType.BitAnd, 0x018055AA);
				await PerformAtomicOperationAndCheck(db, key, 0x0F0F0F0F, FdbMutationType.BitAnd, 0x018055AA);

				key = await ResolveKey("or");
				await PerformAtomicOperationAndCheck(db, key, 0, FdbMutationType.BitOr, 0);
				await PerformAtomicOperationAndCheck(db, key, 0, FdbMutationType.BitOr, 0x018055AA);
				await PerformAtomicOperationAndCheck(db, key, -1, FdbMutationType.BitOr, 0x018055AA);
				await PerformAtomicOperationAndCheck(db, key, 0x00FF00FF, FdbMutationType.BitOr, 0x018055AA);
				await PerformAtomicOperationAndCheck(db, key, 0x0F0F0F0F, FdbMutationType.BitOr, 0x018055AA);

				key = await ResolveKey("xor");
				await PerformAtomicOperationAndCheck(db, key, 0, FdbMutationType.BitXor, 0);
				await PerformAtomicOperationAndCheck(db, key, 0, FdbMutationType.BitXor, 0x018055AA);
				await PerformAtomicOperationAndCheck(db, key, -1, FdbMutationType.BitXor, 0x018055AA);
				await PerformAtomicOperationAndCheck(db, key, 0x00FF00FF, FdbMutationType.BitXor, 0x018055AA);
				await PerformAtomicOperationAndCheck(db, key, 0x0F0F0F0F, FdbMutationType.BitXor, 0x018055AA);

				if (Fdb.ApiVersion >= 300)
				{
					key = await ResolveKey("max");
					await PerformAtomicOperationAndCheck(db, key, 0, FdbMutationType.Max, 0);
					await PerformAtomicOperationAndCheck(db, key, 0, FdbMutationType.Max, 1);
					await PerformAtomicOperationAndCheck(db, key, 1, FdbMutationType.Max, 0);
					await PerformAtomicOperationAndCheck(db, key, 2, FdbMutationType.Max, 1);
					await PerformAtomicOperationAndCheck(db, key, 123456789, FdbMutationType.Max, 987654321);

					key = await ResolveKey("min");
					await PerformAtomicOperationAndCheck(db, key, 0, FdbMutationType.Min, 0);
					await PerformAtomicOperationAndCheck(db, key, 0, FdbMutationType.Min, 1);
					await PerformAtomicOperationAndCheck(db, key, 1, FdbMutationType.Min, 0);
					await PerformAtomicOperationAndCheck(db, key, 2, FdbMutationType.Min, 1);
					await PerformAtomicOperationAndCheck(db, key, 123456789, FdbMutationType.Min, 987654321);
				}
				else
				{
					// calling with an unsupported mutation type should fail
					using (var tr = await db.BeginTransactionAsync(this.Cancellation))
					{
						key = await ResolveKey("invalid");
						Assert.That(() => tr.Atomic(key, Slice.FromFixed32(42), FdbMutationType.Max), Throws.InstanceOf<FdbException>().With.Property("Code").EqualTo(FdbError.InvalidMutationType));
					}
				}

				// calling with an invalid mutation type should fail
				using (var tr = await db.BeginTransactionAsync(this.Cancellation))
				{
					key = await ResolveKey("invalid");
					Assert.That(() => tr.Atomic(key, Slice.FromFixed32(42), (FdbMutationType) 42), Throws.InstanceOf<NotSupportedException>());
				}
			}
		}

		[Test]
		public async Task Test_Can_AtomicAdd32()
		{
			using (var db = await OpenTestDatabaseAsync())
			{
				Log(db.Root);
				var location = db.Root.ByKey("test", "atomic").AsTyped<string>();
				Log(location);
				await CleanLocation(db, location);

				db.SetDefaultLogHandler(log => Log(log.GetTimingsReport(true)));

				// setup
				await db.WriteAsync(async (tr) =>
				{
					Log("resolving...");
					var subspace = (await location.Resolve(tr))!;
					Log(subspace);
					tr.Set(subspace["AAA"], Slice.FromFixed32(0));
					tr.Set(subspace["BBB"], Slice.FromFixed32(1));
					tr.Set(subspace["CCC"], Slice.FromFixed32(43));
					tr.Set(subspace["DDD"], Slice.FromFixed32(255));
					//EEE does not exist
				}, this.Cancellation);

				// execute
				await db.WriteAsync(async (tr) =>
				{
					var subspace = (await location.Resolve(tr))!;
					tr.AtomicAdd32(subspace["AAA"], 1);
					tr.AtomicAdd32(subspace["BBB"], 42);
					tr.AtomicAdd32(subspace["CCC"], -1);
					tr.AtomicAdd32(subspace["DDD"], 42);
					tr.AtomicAdd32(subspace["EEE"], 42);
				}, this.Cancellation);

				// check
				_ = await db.ReadAsync(async (tr) =>
				{
					var subspace = (await location.Resolve(tr))!;
					Assert.That((await tr.GetAsync(subspace["AAA"])).ToHexaString(' '), Is.EqualTo("01 00 00 00"));
					Assert.That((await tr.GetAsync(subspace["BBB"])).ToHexaString(' '), Is.EqualTo("2B 00 00 00"));
					Assert.That((await tr.GetAsync(subspace["CCC"])).ToHexaString(' '), Is.EqualTo("2A 00 00 00"));
					Assert.That((await tr.GetAsync(subspace["DDD"])).ToHexaString(' '), Is.EqualTo("29 01 00 00"));
					Assert.That((await tr.GetAsync(subspace["EEE"])).ToHexaString(' '), Is.EqualTo("2A 00 00 00"));
					return 123;
				}, this.Cancellation);
			}
		}

		[Test]
		public async Task Test_Can_AtomicIncrement32()
		{
			using (var db = await OpenTestDatabaseAsync())
			{
				//await db.WriteAsync(tr => tr.ClearRange(db.GlobalSpace.ToRange()), this.Cancellation);

				var location = db.Root.ByKey("test", "atomic").AsTyped<string>();
				await CleanLocation(db, location);

				db.SetDefaultLogHandler(log => Log(log.GetTimingsReport(true)));

				// setup
				await db.WriteAsync(async tr =>
				{
					var subspace = (await location.Resolve(tr))!;
					tr.Set(subspace["AAA"], Slice.FromFixed32(0));
					tr.Set(subspace["BBB"], Slice.FromFixed32(1));
					tr.Set(subspace["CCC"], Slice.FromFixed32(42));
					tr.Set(subspace["DDD"], Slice.FromFixed32(255));
					//EEE does not exist
				}, this.Cancellation);

				// execute
				await db.WriteAsync(async tr =>
				{
					var subspace = (await location.Resolve(tr))!;
					tr.AtomicIncrement32(subspace["AAA"]);
					tr.AtomicIncrement32(subspace["BBB"]);
					tr.AtomicIncrement32(subspace["CCC"]);
					tr.AtomicIncrement32(subspace["DDD"]);
					tr.AtomicIncrement32(subspace["EEE"]);
				}, this.Cancellation);

				// check
				await db.ReadAsync(async (tr) =>
				{
					var subspace = (await location.Resolve(tr))!;
					Assert.That((await tr.GetAsync(subspace["AAA"])).ToHexaString(' '), Is.EqualTo("01 00 00 00"));
					Assert.That((await tr.GetAsync(subspace["BBB"])).ToHexaString(' '), Is.EqualTo("02 00 00 00"));
					Assert.That((await tr.GetAsync(subspace["CCC"])).ToHexaString(' '), Is.EqualTo("2B 00 00 00"));
					Assert.That((await tr.GetAsync(subspace["DDD"])).ToHexaString(' '), Is.EqualTo("00 01 00 00"));
					Assert.That((await tr.GetAsync(subspace["EEE"])).ToHexaString(' '), Is.EqualTo("01 00 00 00"));
				}, this.Cancellation);
			}
		}

		[Test]
		public async Task Test_Can_AtomicAdd64()
		{
			using (var db = await OpenTestDatabaseAsync())
			{
				var location = db.Root.ByKey("test", "atomic").AsTyped<string>();
				await CleanLocation(db, location);

				db.SetDefaultLogHandler(log => Log(log.GetTimingsReport(true)));

				// setup
				await db.WriteAsync(async (tr) =>
				{
					var subspace = (await location.Resolve(tr))!;
					tr.Set(subspace["AAA"], Slice.FromFixed64(0));
					tr.Set(subspace["BBB"], Slice.FromFixed64(1));
					tr.Set(subspace["CCC"], Slice.FromFixed64(43));
					tr.Set(subspace["DDD"], Slice.FromFixed64(255));
					//EEE does not exist
				}, this.Cancellation);

				// execute
				await db.WriteAsync(async (tr) =>
				{
					var subspace = (await location.Resolve(tr))!;
					tr.AtomicAdd64(subspace["AAA"], 1);
					tr.AtomicAdd64(subspace["BBB"], 42);
					tr.AtomicAdd64(subspace["CCC"], -1);
					tr.AtomicAdd64(subspace["DDD"], 42);
					tr.AtomicAdd64(subspace["EEE"], 42);
				}, this.Cancellation);

				// check
				await db.ReadAsync(async (tr) =>
				{
					var subspace = (await location.Resolve(tr))!;
					Assert.That((await tr.GetAsync(subspace["AAA"])).ToHexaString(' '), Is.EqualTo("01 00 00 00 00 00 00 00"));
					Assert.That((await tr.GetAsync(subspace["BBB"])).ToHexaString(' '), Is.EqualTo("2B 00 00 00 00 00 00 00"));
					Assert.That((await tr.GetAsync(subspace["CCC"])).ToHexaString(' '), Is.EqualTo("2A 00 00 00 00 00 00 00"));
					Assert.That((await tr.GetAsync(subspace["DDD"])).ToHexaString(' '), Is.EqualTo("29 01 00 00 00 00 00 00"));
					Assert.That((await tr.GetAsync(subspace["EEE"])).ToHexaString(' '), Is.EqualTo("2A 00 00 00 00 00 00 00"));
				}, this.Cancellation);
			}
		}

		[Test]
		public async Task Test_Can_AtomicIncrement64()
		{
			using (var db = await OpenTestDatabaseAsync())
			{
				var location = db.Root.ByKey("test", "atomic").AsTyped<string>();
				await CleanLocation(db, location);

				db.SetDefaultLogHandler(log => Log(log.GetTimingsReport(true)));

				// setup
				await db.WriteAsync(async (tr) =>
				{
					var subspace = (await location.Resolve(tr))!;
					tr.Set(subspace["AAA"], Slice.FromFixed64(0));
					tr.Set(subspace["BBB"], Slice.FromFixed64(1));
					tr.Set(subspace["CCC"], Slice.FromFixed64(42));
					tr.Set(subspace["DDD"], Slice.FromFixed64(255));
					//EEE does not exist
				}, this.Cancellation);

				// execute
				await db.WriteAsync(async (tr) =>
				{
					var subspace = (await location.Resolve(tr))!;
					tr.AtomicIncrement64(subspace["AAA"]);
					tr.AtomicIncrement64(subspace["BBB"]);
					tr.AtomicIncrement64(subspace["CCC"]);
					tr.AtomicIncrement64(subspace["DDD"]);
					tr.AtomicIncrement64(subspace["EEE"]);
				}, this.Cancellation);

				// check
				await db.ReadAsync(async (tr) =>
				{
					var subspace = (await location.Resolve(tr))!;
					Assert.That((await tr.GetAsync(subspace["AAA"])).ToHexaString(' '), Is.EqualTo("01 00 00 00 00 00 00 00"));
					Assert.That((await tr.GetAsync(subspace["BBB"])).ToHexaString(' '), Is.EqualTo("02 00 00 00 00 00 00 00"));
					Assert.That((await tr.GetAsync(subspace["CCC"])).ToHexaString(' '), Is.EqualTo("2B 00 00 00 00 00 00 00"));
					Assert.That((await tr.GetAsync(subspace["DDD"])).ToHexaString(' '), Is.EqualTo("00 01 00 00 00 00 00 00"));
					Assert.That((await tr.GetAsync(subspace["EEE"])).ToHexaString(' '), Is.EqualTo("01 00 00 00 00 00 00 00"));
				}, this.Cancellation);
			}
		}

		[Test]
		public async Task Test_Can_AtomicCompareAndClear()
		{
			using (var db = await OpenTestDatabaseAsync())
			{
				var location = db.Root.ByKey("test", "atomic").AsTyped<string>();
				await CleanLocation(db, location);

				db.SetDefaultLogHandler(log => Log(log.GetTimingsReport(true)));

				// setup
				await db.WriteAsync(async (tr) =>
				{
					var subspace = (await location.Resolve(tr))!;
					tr.Set(subspace["AAA"], Slice.FromFixed32(0));
					tr.Set(subspace["BBB"], Slice.FromFixed32(1));
					tr.Set(subspace["CCC"], Slice.FromFixed32(42));
					tr.Set(subspace["DDD"], Slice.FromFixed64(0));
					tr.Set(subspace["EEE"], Slice.FromFixed64(1));
					//FFF does not exist
				}, this.Cancellation);

				// execute
				await db.WriteAsync(async (tr) =>
				{
					var subspace = (await location.Resolve(tr))!;
					tr.AtomicCompareAndClear(subspace["AAA"], Slice.FromFixed32(0));  // should be cleared
					tr.AtomicCompareAndClear(subspace["BBB"], Slice.FromFixed32(0));  // should not be touched
					tr.AtomicCompareAndClear(subspace["CCC"], Slice.FromFixed32(42)); // should be cleared
					tr.AtomicCompareAndClear(subspace["DDD"], Slice.FromFixed64(0));  // should be cleared
					tr.AtomicCompareAndClear(subspace["EEE"], Slice.FromFixed64(0));  // should not be touched
					tr.AtomicCompareAndClear(subspace["FFF"], Slice.FromFixed64(42)); // should not be created
				}, this.Cancellation);

				// check
				_ = await db.ReadAsync(async (tr) =>
				{
					var subspace = (await location.Resolve(tr))!;
					Assert.That((await tr.GetAsync(subspace["AAA"])), Is.EqualTo(Slice.Nil));
					Assert.That((await tr.GetAsync(subspace["BBB"])).ToHexaString(' '), Is.EqualTo("01 00 00 00"));
					Assert.That((await tr.GetAsync(subspace["CCC"])), Is.EqualTo(Slice.Nil));
					Assert.That((await tr.GetAsync(subspace["DDD"])), Is.EqualTo(Slice.Nil));
					Assert.That((await tr.GetAsync(subspace["EEE"])).ToHexaString(' '), Is.EqualTo("01 00 00 00 00 00 00 00"));
					Assert.That((await tr.GetAsync(subspace["FFF"])), Is.EqualTo(Slice.Nil));
					return 123;
				}, this.Cancellation);
			}
		}

		[Test]
		public async Task Test_Can_AppendIfFits()
		{
			using (var db = await OpenTestDatabaseAsync())
			{
				var location = db.Root.ByKey("test", "atomic").AsTyped<string>();
				await CleanLocation(db, location);

				// setup
				await db.WriteAsync(async (tr) =>
				{
					var subspace = (await location.Resolve(tr))!;
					tr.Set(subspace["AAA"], Slice.Empty);
					tr.Set(subspace["BBB"], Slice.Repeat('B', 10));
					tr.Set(subspace["CCC"], Slice.Repeat('C', 90_000));
					tr.Set(subspace["DDD"], Slice.Repeat('D', 100_000));
					//EEE does not exist
				}, this.Cancellation);

				db.SetDefaultLogHandler(log => Log(log.GetTimingsReport(true)));

				// execute
				await db.WriteAsync(async (tr) =>
				{
					var subspace = (await location.Resolve(tr))!;
					tr.AtomicAppendIfFits(subspace["AAA"], Value("Hello, World!"));
					tr.AtomicAppendIfFits(subspace["BBB"], Value("Hello"));
					tr.AtomicAppendIfFits(subspace["BBB"], Value(", World!"));
					tr.AtomicAppendIfFits(subspace["CCC"], Slice.Repeat('c', 10_000)); // should just fit exactly!
					tr.AtomicAppendIfFits(subspace["DDD"], Value("!")); // should not fit!
					tr.AtomicAppendIfFits(subspace["EEE"], Value("Hello, World!"));
				}, this.Cancellation);

				// check
				await db.ReadAsync(async (tr) =>
				{
					var subspace = (await location.Resolve(tr))!;
					Assert.That((await tr.GetAsync(subspace["AAA"])).ToString(), Is.EqualTo("Hello, World!"));
					Assert.That((await tr.GetAsync(subspace["BBB"])).ToString(), Is.EqualTo("BBBBBBBBBBHello, World!"));
					Assert.That((await tr.GetAsync(subspace["CCC"])), Is.EqualTo(Slice.Repeat('C', 90_000) + Slice.Repeat('c', 10_000)));
					Assert.That((await tr.GetAsync(subspace["DDD"])), Is.EqualTo(Slice.Repeat('D', 100_000)));
					Assert.That((await tr.GetAsync(subspace["EEE"])).ToString(), Is.EqualTo("Hello, World!"));
				}, this.Cancellation);
			}
		}

		[Test]
		public async Task Test_Can_Snapshot_Read()
		{
			using (var db = await OpenTestDatabaseAsync())
			{
				var location = db.Root.ByKey("test").AsTyped<string>();
				await CleanLocation(db, location);

				db.SetDefaultLogHandler(log => Log(log.GetTimingsReport(true)));

				// write a bunch of keys
				await db.WriteAsync(async (tr) =>
				{
					var subspace = (await location.Resolve(tr))!;
					tr.Set(subspace["hello"], Value("World!"));
					tr.Set(subspace["foo"], Value("bar"));
				}, this.Cancellation);

				// read them using snapshot
				using (var tr = await db.BeginTransactionAsync(this.Cancellation))
				{
					var subspace = (await location.Resolve(tr))!;

					Slice bytes;

					bytes = await tr.Snapshot.GetAsync(subspace["hello"]);
					Assert.That(bytes.ToUnicode(), Is.EqualTo("World!"));

					bytes = await tr.Snapshot.GetAsync(subspace["foo"]);
					Assert.That(bytes.ToUnicode(), Is.EqualTo("bar"));
				}
			}
		}

		[Test]
		public async Task Test_CommittedVersion_On_ReadOnly_Transactions()
		{
			//note: until CommitAsync() is called, the value of the committed version is unspecified, but current implementation returns -1

			using (var db = await OpenTestDatabaseAsync())
			{
				using (var tr = await db.BeginTransactionAsync(this.Cancellation))
				{
					long ver = tr.GetCommittedVersion();
					Assert.That(ver, Is.EqualTo(-1), "Initial committed version");

					var subspace = (await db.Root.Resolve(tr))!;
					var _ = await tr.GetAsync(subspace.Encode("foo"));

					// until the transaction commits, the committed version will stay -1
					ver = tr.GetCommittedVersion();
					Assert.That(ver, Is.EqualTo(-1), "Committed version after a single read");

					// committing a read only transaction

					await tr.CommitAsync();

					ver = tr.GetCommittedVersion();
					Assert.That(ver, Is.EqualTo(-1), "Read-only committed transaction have a committed version of -1");
				}
			}
		}

		[Test]
		public async Task Test_CommittedVersion_On_Write_Transactions()
		{
			//note: until CommitAsync() is called, the value of the committed version is unspecified, but current implementation returns -1

			using (var db = await OpenTestDatabaseAsync())
			{
				using (var tr = await db.BeginTransactionAsync(this.Cancellation))
				{
					// take the read version (to compare with the committed version below)
					long readVersion = await tr.GetReadVersionAsync();

					long ver = tr.GetCommittedVersion();
					Assert.That(ver, Is.EqualTo(-1), "Initial committed version");

					var subspace = (await db.Root.Resolve(tr))!;
					tr.Set(subspace.Encode("foo"), Value("bar"));

					// until the transaction commits, the committed version should still be -1
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

			using (var db = await OpenTestDatabaseAsync())
			{
				using (var tr = await db.BeginTransactionAsync(this.Cancellation))
				{
					// take the read version (to compare with the committed version below)
					long rv1 = await tr.GetReadVersionAsync();

					var subspace = (await db.Root.Resolve(tr))!;

					// do something and commit
					tr.Set(subspace.Encode("foo"), Value("bar"));
					await tr.CommitAsync();
					long cv1 = tr.GetCommittedVersion();
					Log($"COMMIT: {rv1} / {cv1}");
					Assert.That(cv1, Is.GreaterThanOrEqualTo(rv1), "Committed version of write transaction should be >= the read version");

					// reset the transaction
					tr.Reset();

					long rv2 = await tr.GetReadVersionAsync();
					long cv2 = tr.GetCommittedVersion();
					Log($"RESET: {rv2} / {cv2}");
					//Note: the current fdb_c client does not revert the committed version to -1 ... ?
					//Assert.That(cv2, Is.EqualTo(-1), "Committed version should go back to -1 after reset");

					// read-only + commit
					await tr.GetAsync(subspace.Encode("foo"));
					await tr.CommitAsync();
					cv2 = tr.GetCommittedVersion();
					Log($"COMMIT2: {rv2} / {cv2}");
					Assert.That(cv2, Is.EqualTo(-1), "Committed version of read-only transaction should be -1 even the transaction was previously used to write something");
				}
			}
		}

		[Test]
		public async Task Test_Regular_Read_With_Concurrent_Change_Should_Conflict()
		{
			// see http://community.foundationdb.com/questions/490/snapshot-read-vs-non-snapshot-read/492

			using (var db = await OpenTestDatabaseAsync())
			{
				var location = db.Root.ByKey("test").AsTyped<string>();
				await CleanLocation(db, location);

				await db.WriteAsync(async (tr) =>
				{
					var subspace = (await location.Resolve(tr))!;
					tr.Set(subspace["foo"], Value("foo"));
				}, this.Cancellation);

				db.SetDefaultLogHandler(log => Log(log.GetTimingsReport(true)));

				using (var trA = await db.BeginTransactionAsync(this.Cancellation))
				using (var trB = await db.BeginTransactionAsync(this.Cancellation))
				{
					var subspaceA = (await location.Resolve(trA))!;
					var subspaceB = (await location.Resolve(trB))!;

					// regular read
					_ = await trA.GetAsync(subspaceA["foo"]);
					trA.Set(subspaceA["foo"], Value("bar"));

					// this will conflict with our read
					trB.Set(subspaceB["foo"], Value("bar"));
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

			using (var db = await OpenTestDatabaseAsync())
			{
				var location = db.Root.ByKey("test").AsTyped<string>();
				await CleanLocation(db, location);

				db.SetDefaultLogHandler(log => Log(log.GetTimingsReport(true)));

				await db.WriteAsync(async (tr) =>
				{
					var subspace = (await location.Resolve(tr))!;
					tr.Set(subspace["foo"], Value("foo"));
				}, this.Cancellation);

				using (var trA = await db.BeginTransactionAsync(this.Cancellation))
				using (var trB = await db.BeginTransactionAsync(this.Cancellation))
				{
					var subspaceA = (await location.Resolve(trA))!;
					var subspaceB = (await location.Resolve(trB))!;

					// reading with snapshot mode should not conflict
					_ = await trA.Snapshot.GetAsync(subspaceA["foo"]);
					trA.Set(subspaceA["foo"], Value("bar"));

					// this would normally conflicts with the previous read if it wasn't a snapshot read
					trB.Set(subspaceB["foo"], Value("bar"));
					await trB.CommitAsync();

					// should succeed
					await trA.CommitAsync();
				}
			}
		}

		[Test]
		public async Task Test_GetRange_With_Concurrent_Change_Should_Conflict()
		{
			using(var db = await OpenTestDatabaseAsync())
			{
				var location = db.Root.ByKey("test");
				await CleanLocation(db, location);

				await db.WriteAsync(async (tr) =>
				{
					var subspace = (await location.Resolve(tr))!;
					tr.Set(subspace.Encode("foo", 50), Value("fifty"));
				}, this.Cancellation);

				db.SetDefaultLogHandler(log => Log(log.GetTimingsReport(true)));

				// we will read the first key from [0, 100), expected 50
				// but another transaction will insert 42, in effect changing the result of our range
				// => this should conflict the GetRange

				using (var tr1 = await db.BeginTransactionAsync(this.Cancellation))
				{
					var subspace = (await location.Resolve(tr1))!;

					// [0, 100) limit 1 => 50
					var kvp = await tr1
						.GetRange(subspace.Encode("foo"), subspace.Encode("foo", 100))
						.FirstOrDefaultAsync();
					Assert.That(kvp.Key, Is.EqualTo(subspace.Encode("foo", 50)));

					// 42 < 50 > conflict !!!
					using (var tr2 = await db.BeginTransactionAsync(this.Cancellation))
					{
						var subspace2 = (await location.Resolve(tr2))!;
						tr2.Set(subspace2.Encode("foo", 42), Value("forty-two"));
						await tr2.CommitAsync();
					}

					// we need to write something to force a conflict
					tr1.Set(subspace.Encode("bar"), Slice.Empty);

					await TestHelpers.AssertThrowsFdbErrorAsync(() => tr1.CommitAsync(), FdbError.NotCommitted, "The Set(42) in TR2 should have conflicted with the GetRange(0, 100) in TR1");
				}

				// if the other transaction insert something AFTER 50, then the result of our GetRange would not change (because of the implied limit = 1)
				// => this should NOT conflict the GetRange
				// note that if we write something in the range (0, 100) but AFTER 50, it should not conflict because we are doing a limit=1

				await db.WriteAsync(async (tr) =>
				{
					var subspace = (await location.Resolve(tr))!;
					tr.ClearRange(subspace);
					tr.Set(subspace.Encode("foo", 50), Value("fifty"));
				}, this.Cancellation);

				using (var tr1 = await db.BeginTransactionAsync(this.Cancellation))
				{
					var subspace = (await location.Resolve(tr1))!;

					// [0, 100) limit 1 => 50
					var kvp = await tr1
						.GetRange(subspace.Encode("foo"), subspace.Encode("foo", 100))
						.FirstOrDefaultAsync();
					Assert.That(kvp.Key, Is.EqualTo(subspace.Encode("foo", 50)));

					// 77 > 50 => no conflict
					using (var tr2 = await db.BeginTransactionAsync(this.Cancellation))
					{
						var subspace2 = (await location.Resolve(tr2))!;
						tr2.Set(subspace2.Encode("foo", 77), Value("docm"));
						await tr2.CommitAsync();
					}

					// we need to write something to force a conflict
					tr1.Set(subspace.Encode("bar"), Slice.Empty);

					// should not conflict!
					await tr1.CommitAsync();
				}
			}
		}

		[Test]
		public async Task Test_GetKey_With_Concurrent_Change_Should_Conflict()
		{
			using (var db = await OpenTestDatabaseAsync())
			{
				var location = db.Root.ByKey("test");
				await CleanLocation(db, location);

				await db.WriteAsync(async (tr) =>
				{
					var subspace = (await location.Resolve(tr))!;
					tr.ClearRange(subspace);
					tr.Set(subspace.Encode("foo", 50), Value("fifty"));
				}, this.Cancellation);

				db.SetDefaultLogHandler(log => Log(log.GetTimingsReport(true)));

				// we will ask for the first key from >= 0, expecting 50, but if another transaction inserts something BEFORE 50, our key selector would have returned a different result, causing a conflict

				using (var tr1 = await db.BeginTransactionAsync(this.Cancellation))
				{
					var subspace = (await location.Resolve(tr1))!;
					// fGE{0} => 50
					var key = await tr1.GetKeyAsync(KeySelector.FirstGreaterOrEqual(subspace.Encode("foo", 0)));
					Assert.That(key, Is.EqualTo(subspace.Encode("foo", 50)));

					// 42 < 50 => conflict !!!
					using (var tr2 = await db.BeginTransactionAsync(this.Cancellation))
					{
						var subspace2 = (await location.Resolve(tr2))!;
						tr2.Set(subspace2.Encode("foo", 42), Value("forty-two"));
						await tr2.CommitAsync();
					}

					// we need to write something to force a conflict
					tr1.Set(subspace.Encode("bar"), Slice.Empty);

					await TestHelpers.AssertThrowsFdbErrorAsync(() => tr1.CommitAsync(), FdbError.NotCommitted, "The Set(42) in TR2 should have conflicted with the GetKey(fGE{0}) in TR1");
				}

				// if the other transaction insert something AFTER 50, our key selector would have still returned the same result, and we would have any conflict

				await db.WriteAsync(async (tr) =>
				{
					var subspace = (await location.Resolve(tr))!;
					tr.ClearRange(subspace);
					tr.Set(subspace.Encode("foo", 50), Value("fifty"));
				}, this.Cancellation);

				using (var tr1 = await db.BeginTransactionAsync(this.Cancellation))
				{
					var subspace = (await location.Resolve(tr1))!;
					// fGE{0} => 50
					var key = await tr1.GetKeyAsync(KeySelector.FirstGreaterOrEqual(subspace.Encode("foo", 0)));
					Assert.That(key, Is.EqualTo(subspace.Encode("foo", 50)));

					// 77 > 50 => no conflict
					using (var tr2 = await db.BeginTransactionAsync(this.Cancellation))
					{
						var subspace2 = (await location.Resolve(tr2))!;
						tr2.Set(subspace2.Encode("foo", 77), Value("docm"));
						await tr2.CommitAsync();
					}

					// we need to write something to force a conflict
					tr1.Set(subspace.Encode("bar"), Slice.Empty);

					// should not conflict!
					await tr1.CommitAsync();
				}

				// but if we have an large offset in the key selector, and another transaction insert something inside the offset window, the result would be different, and it should conflict

				await db.WriteAsync(async (tr) =>
				{
					var subspace = (await location.Resolve(tr))!;
					tr.ClearRange(subspace);
					tr.Set(subspace.Encode("foo", 50), Value("fifty"));
					tr.Set(subspace.Encode("foo", 100), Value("one hundred"));
				}, this.Cancellation);

				using (var tr1 = await db.BeginTransactionAsync(this.Cancellation))
				{
					var subspace = (await location.Resolve(tr1))!;

					// fGE{50} + 1 => 100
					var key = await tr1.GetKeyAsync(KeySelector.FirstGreaterOrEqual(subspace.Encode("foo", 50)) + 1);
					Assert.That(key, Is.EqualTo(subspace.Encode("foo", 100)));

					// 77 between 50 and 100 => conflict !!!
					using (var tr2 = await db.BeginTransactionAsync(this.Cancellation))
					{
						var subspace2 = (await location.Resolve(tr2))!;
						tr2.Set(subspace2.Encode("foo", 77), Value("docm"));
						await tr2.CommitAsync();
					}

					// we need to write something to force a conflict
					tr1.Set(subspace.Encode("bar"), Slice.Empty);

					// should conflict!
					await TestHelpers.AssertThrowsFdbErrorAsync(() => tr1.CommitAsync(), FdbError.NotCommitted, "The Set(77) in TR2 should have conflicted with the GetKey(fGE{50} + 1) in TR1");
				}

				// does conflict arise from changes in VALUES in the database? or from changes in RESULTS to user queries ?

				await db.WriteAsync(async (tr) =>
				{
					var subspace = (await location.Resolve(tr))!;
					tr.ClearRange(subspace);
					tr.Set(subspace.Encode("foo", 50), Value("fifty"));
					tr.Set(subspace.Encode("foo", 100), Value("one hundred"));
				}, this.Cancellation);

				using (var tr1 = await db.BeginTransactionAsync(this.Cancellation))
				{
					var subspace = (await location.Resolve(tr1))!;
					// fGT{50} => 100
					var key = await tr1.GetKeyAsync(KeySelector.FirstGreaterThan(subspace.Encode("foo", 50)));
					Assert.That(key, Is.EqualTo(subspace.Encode("foo", 100)));

					// another transaction changes the VALUE of 50 and 100 (but does not change the fact that they exist nor add keys in between)
					using (var tr2 = await db.BeginTransactionAsync(this.Cancellation))
					{
						var subspace2 = (await location.Resolve(tr2))!;
						tr2.Set(subspace2.Encode("foo", 100), Value("cent"));
						await tr2.CommitAsync();
					}

					// we need to write something to force a conflict
					tr1.Set(subspace.Encode("bar"), Slice.Empty);

					// this causes a conflict in the current version of FDB
					await TestHelpers.AssertThrowsFdbErrorAsync(() => tr1.CommitAsync(), FdbError.NotCommitted, "The Set(100) in TR2 should have conflicted with the GetKey(fGT{50}) in TR1");
				}

				// LastLessThan does not create conflicts if the pivot key is changed

				await db.WriteAsync(async (tr) =>
				{
					var subspace = (await location.Resolve(tr))!;
					tr.ClearRange(subspace);
					tr.Set(subspace.Encode("foo", 50), Value("fifty"));
					tr.Set(subspace.Encode("foo", 100), Value("one hundred"));
				}, this.Cancellation);

				using (var tr1 = await db.BeginTransactionAsync(this.Cancellation))
				{
					var subspace = (await location.Resolve(tr1))!;
					// lLT{100} => 50
					var key = await tr1.GetKeyAsync(KeySelector.LastLessThan(subspace.Encode("foo", 100)));
					Assert.That(key, Is.EqualTo(subspace.Encode("foo", 50)));

					// another transaction changes the VALUE of 50 and 100 (but does not change the fact that they exist nor add keys in between)
					using (var tr2 = await db.BeginTransactionAsync(this.Cancellation))
					{
						var subspace2 = (await location.Resolve(tr2))!;
						tr2.Clear(subspace2.Encode("foo", 100));
						await tr2.CommitAsync();
					}

					// we need to write something to force a conflict
					tr1.Set(subspace.Encode("bar"), Slice.Empty);

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

			using (var db = await OpenTestDatabaseAsync())
			{
				var location = db.Root.ByKey("test").AsTyped<string>();
				await CleanLocation(db, location);

				db.SetDefaultLogHandler(log => Log(log.GetTimingsReport(true)));

				await db.WriteAsync(async (tr) =>
				{
					var subspace = (await db.Root.Resolve(tr))!;
					tr.Set(subspace.Encode("test", "A"), Slice.FromInt32(1));
				}, this.Cancellation);
				using(var tr1 = await db.BeginTransactionAsync(this.Cancellation))
				{
					// make sure that T1 has seen the db BEFORE T2 gets executed, or else it will not really be initialized until after the first read or commit
					await tr1.GetReadVersionAsync();
					//T1 should be locked to a specific version of the db

					var subspace1 = (await db.Root.Resolve(tr1))!;
					var key = subspace1.Encode("test", "A");

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
				await db.WriteAsync(async (tr) =>
				{
					var subspace = (await db.Root.Resolve(tr))!;
					tr.Set(subspace.Encode("test", "A"), Slice.FromInt32(1));
				}, this.Cancellation);
				using (var tr1 = await db.BeginTransactionAsync(this.Cancellation))
				{
					//do NOT use T1 yet

					// change the value in T2
					await db.WriteAsync(async (tr2) =>
					{
						var subspace2 = (await db.Root.Resolve(tr2))!;
						tr2.Set(subspace2.Encode("test", "A"), Slice.FromInt32(2));
					}, this.Cancellation);


					// read the value in T1 and commits
					var subspace1 = (await db.Root.Resolve(tr1))!;
					var value = await tr1.GetAsync(subspace1.Encode("test", "A"));

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

			using (var db = await OpenTestDatabaseAsync())
			{
				var location = db.Root.ByKey("test").AsTyped<string>();
				await CleanLocation(db, location);

				// Reads (before and after):
				// - A and B will use regular reads
				// - C and D will use snapshot reads
				// Writes:
				// - A and C will be modified by the transaction itself
				// - B and D will be modified by a different transaction

				await db.WriteAsync(async (tr) =>
				{
					var subspace = (await location.Resolve(tr))!;
					tr.Set(subspace["A"], Value("a"));
					tr.Set(subspace["B"], Value("b"));
					tr.Set(subspace["C"], Value("c"));
					tr.Set(subspace["D"], Value("d"));
				}, this.Cancellation);

				Log("Initial db state:");
				await DumpSubspace(db, location);

				db.SetDefaultLogHandler(log => Log(log.GetTimingsReport(true)));

				using (var tr = await db.BeginTransactionAsync(this.Cancellation))
				{
					var subspace = (await location.Resolve(tr))!;

					// check initial state
					Assert.That((await tr.GetAsync(subspace["A"])).ToStringUtf8(), Is.EqualTo("a"));
					Assert.That((await tr.GetAsync(subspace["B"])).ToStringUtf8(), Is.EqualTo("b"));
					Assert.That((await tr.Snapshot.GetAsync(subspace["C"])).ToStringUtf8(), Is.EqualTo("c"));
					Assert.That((await tr.Snapshot.GetAsync(subspace["D"])).ToStringUtf8(), Is.EqualTo("d"));

					// mutate (not yet committed)
					tr.Set(subspace["A"], Value("aa"));
					tr.Set(subspace["C"], Value("cc"));
					await db.WriteAsync((tr2) =>
					{ // have another transaction change B and D under our nose
						tr2.Set(subspace["B"], Value("bb"));
						tr2.Set(subspace["D"], Value("dd"));
					}, this.Cancellation);

					// check what the transaction sees
					Assert.That((await tr.GetAsync(subspace["A"])).ToStringUtf8(), Is.EqualTo("aa"), "The transaction own writes should change the value of regular reads");
					Assert.That((await tr.GetAsync(subspace["B"])).ToStringUtf8(), Is.EqualTo("b"), "Other transaction writes should not change the value of regular reads");
					Assert.That((await tr.Snapshot.GetAsync(subspace["C"])).ToStringUtf8(), Is.EqualTo("cc"), "The transaction own writes should be visible in snapshot reads");
					Assert.That((await tr.Snapshot.GetAsync(subspace["D"])).ToStringUtf8(), Is.EqualTo("d"), "Other transaction writes should not change the value of snapshot reads");

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

			using (var db = await OpenTestDatabaseAsync())
			{
				var location = db.Root.ByKey("test").AsTyped<string>();
				await CleanLocation(db, location);

				// Reads (before and after):
				// - A and B will use regular reads
				// - C and D will use snapshot reads
				// Writes:
				// - A and C will be modified by the transaction itself
				// - B and D will be modified by a different transaction

				await db.WriteAsync(async (tr) =>
				{
					var subspace = (await location.Resolve(tr))!;
					tr.Set(subspace["A"], Value("a"));
					tr.Set(subspace["B"], Value("b"));
					tr.Set(subspace["C"], Value("c"));
					tr.Set(subspace["D"], Value("d"));
				}, this.Cancellation);

				Log("Initial db state:");
				await DumpSubspace(db, location);

				using (var tr = await db.BeginTransactionAsync(this.Cancellation))
				{
					var subspace = (await location.Resolve(tr))!;

					tr.Options.WithSnapshotReadYourWritesDisable();

					// check initial state
					Assert.That((await tr.GetAsync(subspace["A"])).ToStringUtf8(), Is.EqualTo("a"));
					Assert.That((await tr.GetAsync(subspace["B"])).ToStringUtf8(), Is.EqualTo("b"));
					Assert.That((await tr.Snapshot.GetAsync(subspace["C"])).ToStringUtf8(), Is.EqualTo("c"));
					Assert.That((await tr.Snapshot.GetAsync(subspace["D"])).ToStringUtf8(), Is.EqualTo("d"));

					// mutate (not yet committed)
					tr.Set(subspace["A"], Value("aa"));
					tr.Set(subspace["C"], Value("cc"));
					await db.WriteAsync((tr2) =>
					{ // have another transaction change B and D under our nose
						tr2.Set(subspace["B"], Value("bb"));
						tr2.Set(subspace["D"], Value("dd"));
					}, this.Cancellation);

					// check what the transaction sees
					Assert.That((await tr.GetAsync(subspace["A"])).ToStringUtf8(), Is.EqualTo("aa"), "The transaction own writes should change the value of regular reads");
					Assert.That((await tr.GetAsync(subspace["B"])).ToStringUtf8(), Is.EqualTo("b"), "Other transaction writes should not change the value of regular reads");
					//FAIL: test fails here because we read "CC" ??
					Assert.That((await tr.Snapshot.GetAsync(subspace["C"])).ToStringUtf8(), Is.EqualTo("c"), "The transaction own writes should not change the value of snapshot reads");
					Assert.That((await tr.Snapshot.GetAsync(subspace["D"])).ToStringUtf8(), Is.EqualTo("d"), "Other transaction writes should not change the value of snapshot reads");

					//note: committing here would conflict
				}
			}
		}

		[Test]
		public async Task Test_ReadYourWritesDisable_Isolation()
		{
			using (var db = await OpenTestDatabaseAsync())
			{
				var location = db.Root.ByKey("test");
				await CleanLocation(db, location);

				db.SetDefaultLogHandler(log => Log(log.GetTimingsReport(true)));

				#region Default behaviour...

				// By default, a transaction see its own writes with non-snapshot reads

				await db.WriteAsync(async (tr) =>
				{
					var subspace = (await location.Resolve(tr))!;
					tr.Set(subspace.Encode("a"), Value("a"));
					tr.Set(subspace.Encode("b", 10), Value("PRINT \"HELLO\""));
					tr.Set(subspace.Encode("b", 20), Value("GOTO 10"));
				}, this.Cancellation);

				using(var tr = await db.BeginTransactionAsync(this.Cancellation))
				{
					var subspace = (await location.Resolve(tr))!;

					var data = await tr.GetAsync(subspace.Encode("a"));
					Assert.That(data.ToUnicode(), Is.EqualTo("a"));
					
					var res = await tr.GetRange(subspace.EncodeRange("b")).Select(kvp => kvp.Value.ToString()).ToArrayAsync();
					Assert.That(res, Is.EqualTo(new [] { "PRINT \"HELLO\"", "GOTO 10" }));

					tr.Set(subspace.Encode("a"), Value("aa"));
					tr.Set(subspace.Encode("b", 15), Value("PRINT \"WORLD\""));

					data = await tr.GetAsync(subspace.Encode("a"));
					Assert.That(data.ToUnicode(), Is.EqualTo("aa"), "The transaction own writes should be visible by default");
					res = await tr.GetRange(subspace.EncodeRange("b")).Select(kvp => kvp.Value.ToString()).ToArrayAsync();
					Assert.That(res, Is.EqualTo(new[] { "PRINT \"HELLO\"", "PRINT \"WORLD\"", "GOTO 10" }), "The transaction own writes should be visible by default");

					//note: don't commit
				}

				#endregion

				#region ReadYourWritesDisable behaviour...

				// The ReadYourWritesDisable option cause reads to always return the value in the database

				using (var tr = await db.BeginTransactionAsync(this.Cancellation))
				{
					var subspace = (await location.Resolve(tr))!;

					tr.Options.WithReadYourWritesDisable();

					var data = await tr.GetAsync(subspace.Encode("a"));
					Assert.That(data.ToUnicode(), Is.EqualTo("a"));
					var res = await tr.GetRange(subspace.EncodeRange("b")).Select(kvp => kvp.Value.ToString()).ToArrayAsync();
					Assert.That(res, Is.EqualTo(new[] { "PRINT \"HELLO\"", "GOTO 10" }));

					tr.Set(subspace.Encode("a"), Value("aa"));
					tr.Set(subspace.Encode("b", 15), Value("PRINT \"WORLD\""));

					data = await tr.GetAsync(subspace.Encode("a"));
					Assert.That(data.ToUnicode(), Is.EqualTo("a"), "The transaction own writes should not be seen with ReadYourWritesDisable option enabled");
					res = await tr.GetRange(subspace.EncodeRange("b")).Select(kvp => kvp.Value.ToString()).ToArrayAsync();
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

			using (var db = await OpenTestDatabaseAsync())
			{
				var location = db.Root.ByKey("test").AsTyped<string>();
				await CleanLocation(db, location);

				long committedVersion;

				db.SetDefaultLogHandler(log => Log(log.GetTimingsReport(true)));

				// create first version
				using (var tr1 = await db.BeginTransactionAsync(this.Cancellation))
				{
					var subspace = (await location.Resolve(tr1))!;
					tr1.Set(subspace["concurrent"], Slice.FromByte(1));
					await tr1.CommitAsync();

					// get this version
					committedVersion = tr1.GetCommittedVersion();
				}

				// mutate in another transaction
				using (var tr2 = await db.BeginTransactionAsync(this.Cancellation))
				{
					var subspace = (await location.Resolve(tr2))!;
					tr2.Set(subspace["concurrent"], Slice.FromByte(2));
					await tr2.CommitAsync();
				}

				// read the value with TR1's committed version
				using (var tr3 = await db.BeginTransactionAsync(this.Cancellation))
				{
					tr3.SetReadVersion(committedVersion);

					long ver = await tr3.GetReadVersionAsync();
					Assert.That(ver, Is.EqualTo(committedVersion), "GetReadVersion should return the same value as SetReadVersion!");

					var subspace = (await location.Resolve(tr3))!;

					var bytes = await tr3.GetAsync(subspace["concurrent"]);

					Assert.That(bytes.GetBytes(), Is.EqualTo(new byte[] { 1 }), "Should have seen the first version!");
				}
			}
		}

		[Test]
		public async Task Test_Has_Access_To_System_Keys()
		{
			using (var db = await OpenTestDatabaseAsync())
			{
				db.SetDefaultLogHandler(log => Log(log.GetTimingsReport(true)));

				using (var tr = await db.BeginTransactionAsync(this.Cancellation))
				{

					// should fail if access to system keys has not been requested

					await TestHelpers.AssertThrowsFdbErrorAsync(
						() => tr.GetRange(Slice.FromByteString("\xFF"), Slice.FromByteString("\xFF\xFF"), new FdbRangeOptions { Limit = 10 }).ToListAsync(),
						FdbError.KeyOutsideLegalRange,
						"Should not have access to system keys by default"
					);

					// should succeed once system access has been requested
					tr.Options.WithReadAccessToSystemKeys();

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
				using (var tr = await db.BeginTransactionAsync(this.Cancellation))
				{
					Assert.That(tr.Options.Timeout, Is.EqualTo(15000), "Timeout (default)");
					Assert.That(tr.Options.RetryLimit, Is.Zero, "RetryLimit (default)");
					Assert.That(tr.Options.MaxRetryDelay, Is.Zero, "MaxRetryDelay (default)");

					tr.Options.Timeout = 1000; // 1 sec max
					tr.Options.RetryLimit = 5; // 5 retries max
					tr.Options.MaxRetryDelay = 500; // .5 sec max

					Assert.That(tr.Options.Timeout, Is.EqualTo(1000), "Timeout");
					Assert.That(tr.Options.RetryLimit, Is.EqualTo(5), "RetryLimit");
					Assert.That(tr.Options.MaxRetryDelay, Is.EqualTo(500), "MaxRetryDelay");
				}
			}
		}

		[Test]
		public async Task Test_Timeout_And_RetryLimit_Inherits_Default_From_Database()
		{
			using (var db = await OpenTestDatabaseAsync())
			{
				Assert.That(db.Options.DefaultTimeout, Is.EqualTo(15000), "db.DefaultTimeout (default)");
				Assert.That(db.Options.DefaultRetryLimit, Is.Zero, "db.DefaultRetryLimit (default)");
				Assert.That(db.Options.DefaultMaxRetryDelay, Is.Zero, "db.DefaultMaxRetryDelay (default)");

				db.Options.DefaultTimeout = 500;
				db.Options.DefaultRetryLimit = 3;
				db.Options.DefaultMaxRetryDelay = 600;

				Assert.That(db.Options.DefaultTimeout, Is.EqualTo(500), "db.DefaultTimeout");
				Assert.That(db.Options.DefaultRetryLimit, Is.EqualTo(3), "db.DefaultRetryLimit");
				Assert.That(db.Options.DefaultMaxRetryDelay, Is.EqualTo(600), "db.DefaultMaxRetryDelay");

				// transaction should be already configured with the default options

				using (var tr = await db.BeginTransactionAsync(this.Cancellation))
				{
					Assert.That(tr.Options.Timeout, Is.EqualTo(500), "tr.Options.Timeout");
					Assert.That(tr.Options.RetryLimit, Is.EqualTo(3), "tr.Options.RetryLimit");
					Assert.That(tr.Options.MaxRetryDelay, Is.EqualTo(600), "tr.Options.MaxRetryDelay");

					// changing the default on the db should only affect new transactions

					db.Options.DefaultTimeout = 600;
					db.Options.DefaultRetryLimit = 4;
					db.Options.DefaultMaxRetryDelay = 700;

					using (var tr2 = await db.BeginTransactionAsync(this.Cancellation))
					{
						Assert.That(tr2.Options.Timeout, Is.EqualTo(600), "tr2.Options.Timeout");
						Assert.That(tr2.Options.RetryLimit, Is.EqualTo(4), "tr2.Options.RetryLimit");
						Assert.That(tr2.Options.MaxRetryDelay, Is.EqualTo(700), "tr2.Options.MaxRetryDelay");

						// original tr should not be affected
						Assert.That(tr.Options.Timeout, Is.EqualTo(500), "tr.Options.Timeout");
						Assert.That(tr.Options.RetryLimit, Is.EqualTo(3), "tr.Options.RetryLimit");
						Assert.That(tr.Options.MaxRetryDelay, Is.EqualTo(600), "tr.Options.MaxRetryDelay");
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
				Assert.That(db.Options.DefaultTimeout, Is.EqualTo(15000), "db.DefaultTimeout (default)");
				Assert.That(db.Options.DefaultRetryLimit, Is.Zero, "db.DefaultRetryLimit (default)");

				// By default, a transaction that gets reset or retried, clears the RetryLimit and Timeout settings, which needs to be reset everytime.
				// But if the DefaultRetryLimit and DefaultTimeout are set on the database instance, they should automatically be re-applied inside transaction loops!
				db.Options.DefaultRetryLimit = 3;

				db.SetDefaultLogHandler(log => Log(log.GetTimingsReport(true)));

				int counter = 0;
				var t = db.ReadAsync<int>((tr) =>
				{
					++counter;
					Log($"Called {counter} time(s)");
					if (counter > 4)
					{
						go.Cancel();
						tr.Context.Abort = true;
						Assert.Fail("The retry loop was called too many times!");
					}

					Assert.That(tr.Options.RetryLimit, Is.EqualTo(3), "tr.Options.RetryLimit");

					// simulate a retryable error condition
					throw new FdbException(FdbError.TransactionTooOld);
				}, go.Token);

				try
				{
					await t;
					Assert.Fail("Should have failed!");
				}
				catch (AssertionException) { throw; }
				catch (Exception e)
				{
					Assert.That(e, Is.InstanceOf<FdbException>().With.Property("Code").EqualTo(FdbError.TransactionTooOld));
				}

				Assert.That(counter, Is.EqualTo(4), "1 first attempt + 3 retries = 4 executions");
			}
		}

		[Test]
		public async Task Test_Transaction_RetryLoop_Resets_RetryLimit_And_Timeout()
		{
			using (var db = await OpenTestDatabaseAsync())
			{
				db.SetDefaultLogHandler(log => Log(log.GetTimingsReport(true)));

				using (var tr = await db.BeginTransactionAsync(this.Cancellation))
				{
					// simulate a first error
					tr.Options.RetryLimit = 10;
					await tr.OnErrorAsync(FdbError.TransactionTooOld);
					Assert.That(tr.Options.RetryLimit, Is.Zero, "Retry limit should be reset");

					// simulate some more errors
					await tr.OnErrorAsync(FdbError.TransactionTooOld);
					await tr.OnErrorAsync(FdbError.TransactionTooOld);
					await tr.OnErrorAsync(FdbError.TransactionTooOld);
					await tr.OnErrorAsync(FdbError.TransactionTooOld);
					Assert.That(tr.Options.RetryLimit, Is.Zero, "Retry limit should be reset");

					// we still haven't failed 10 times..
					tr.Options.RetryLimit = 10;
					await tr.OnErrorAsync(FdbError.TransactionTooOld);
					Assert.That(tr.Options.RetryLimit, Is.Zero, "Retry limit should be reset");

					// we already have failed 6 times, so this one should abort
					tr.Options.RetryLimit = 2; // value is too low
					Assert.That(async () => await tr.OnErrorAsync(FdbError.TransactionTooOld), Throws.InstanceOf<FdbException>().With.Property("Code").EqualTo(FdbError.TransactionTooOld));
				}
			}
		}

		[Test]
		public async Task Test_Can_Add_Read_Conflict_Range()
		{
			using (var db = await OpenTestDatabaseAsync())
			{
				var location = db.Root.ByKey("conflict").AsTyped<int>();
				await CleanLocation(db, location);

				db.SetDefaultLogHandler(log => Log(log.GetTimingsReport(true)));

				using (var tr1 = await db.BeginTransactionAsync(this.Cancellation))
				{
					var subspace = (await location.Resolve(tr1))!;

					await tr1.GetAsync(subspace[1]);
					// tr1 writes to one key
					tr1.Set(subspace[1], Value("hello"));
					// but add the second as a conflict range
					tr1.AddReadConflictKey(subspace[2]);

					using (var tr2 = await db.BeginTransactionAsync(this.Cancellation))
					{
						var subspace2 = (await location.Resolve(tr2))!;

						// tr2 writes to the second key
						tr2.Set(subspace2[2], Value("world"));

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
			using (var db = await OpenTestDatabaseAsync())
			{
				var location = db.Root.ByKey("conflict");
				await CleanLocation(db, location);

				db.SetDefaultLogHandler(log => Log(log.GetTimingsReport(true)));

				using (var tr1 = await db.BeginTransactionAsync(this.Cancellation))
				{
					var subspace = (await location.Resolve(tr1))!;

					// tr1 reads the conflicting key
					await tr1.GetAsync(subspace.Encode(0));
					// and writes to key1
					tr1.Set(subspace.Encode(1), Value("hello"));

					using (var tr2 = await db.BeginTransactionAsync(this.Cancellation))
					{
						var subspace2 = (await location.Resolve(tr2))!;

						// tr2 changes key2, but adds a conflict range on the conflicting key
						tr2.Set(subspace2.Encode(2), Value("world"));

						// and writes on the third
						tr2.AddWriteConflictKey(subspace2.Encode(0)); // conflict!

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
			using (var db = await OpenTestDatabaseAsync())
			{
				var location = db.Root.ByKey("test", "bigbrother");
				await CleanLocation(db, location);

				db.SetDefaultLogHandler(log => Log(log.GetTimingsReport(true)));

				await db.WriteAsync(async tr =>
				{
					var subspace = (await location.Resolve(tr))!;
					tr.Set(subspace.Encode("watched"), Value("some value"));
					tr.Set(subspace.Encode("witness"), Value("some other value"));
				}, this.Cancellation);

				using (var cts = new CancellationTokenSource())
				{
					FdbWatch w1;
					FdbWatch w2;

					using (var tr = await db.BeginTransactionAsync(this.Cancellation))
					{
						var subspace = (await location.Resolve(tr))!;
						w1 = tr.Watch(subspace.Encode("watched"), cts.Token);
						w2 = tr.Watch(subspace.Encode("witness"), cts.Token);
						Assert.That(w1, Is.Not.Null);
						Assert.That(w2, Is.Not.Null);

						// note: Watches will get cancelled if the transaction is not committed !
						await tr.CommitAsync();
					}

					// Watches should survive the transaction
					await Task.Delay(100, this.Cancellation);
					Assert.That(w1.Task.Status, Is.EqualTo(TaskStatus.WaitingForActivation), "w1 should survive the transaction without being triggered");
					Assert.That(w2.Task.Status, Is.EqualTo(TaskStatus.WaitingForActivation), "w2 should survive the transaction without being triggered");

					await db.WriteAsync(async (tr) =>
					{
						var subspace = (await location.Resolve(tr))!;
						tr.Set(subspace.Encode("watched"), Value("some new value"));
					}, this.Cancellation);

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

			using (var db = await OpenTestDatabaseAsync())
			{
				var location = db.Root.ByKey("test", "bigbrother");
				await CleanLocation(db, location);

				using (var tr = await db.BeginTransactionAsync(this.Cancellation))
				{
					var subspace = (await location.Resolve(tr))!;
					var key = subspace.Encode("watched");

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
			using (var db = await OpenTestDatabaseAsync())
			{
				var location = db.Root.ByKey("test", "bigbrother");
				await CleanLocation(db, location);

				Log("Set to initial value...");
				await db.WriteAsync(async tr =>
				{
					var subspace = (await location.Resolve(tr))!;
					tr.Set(subspace.Encode("watched"), Value("initial value"));
				}, this.Cancellation);

				Log("Create watch...");
				var w = await db.ReadWriteAsync(async tr =>
				{
					var subspace = (await location.Resolve(tr))!;
					return tr.Watch(subspace.Encode("watched"), this.Cancellation);
				}, this.Cancellation);
				Assert.That(w.IsAlive, Is.True, "Watch should still be alive");
				Assert.That(w.Task.Status, Is.EqualTo(TaskStatus.WaitingForActivation));

				// change the key to the same value
				Log("Set to same value...");
				await db.WriteAsync(async tr =>
				{
					var subspace = (await location.Resolve(tr))!;
					tr.Set(subspace.Encode("watched"), Value("initial value"));
				}, this.Cancellation);

				//note: it is difficult to verify something "that should never happen"
				// let's say that 1sec is a good approximation of an infinite time
				Log("Watch should not fire");
				await Task.WhenAny(w.Task, Task.Delay(1_000, this.Cancellation));
				Assert.That(w.IsAlive, Is.True, "Watch should still be active");
				Assert.That(w.Task.Status, Is.EqualTo(TaskStatus.WaitingForActivation));

				// now really change the value
				Log("Set to a different value...");
				await db.WriteAsync(async tr =>
				{
					var subspace = (await location.Resolve(tr))!;
					tr.Set(subspace.Encode("watched"), Value("new value"));
				}, this.Cancellation);

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
			//note: starting from API level 630, options IncludePortInAddress is the default!

			using (var db = await OpenTestDatabaseAsync())
			{
				var location = db.Root.ByKey("location_api");
				await CleanLocation(db, location);

				await db.WriteAsync(async (tr) =>
				{
					var subspace = (await location.Resolve(tr))!;
					tr.Set(subspace.Encode(1), Value("one"));
				}, this.Cancellation);

				db.SetDefaultLogHandler(log => Log(log.GetTimingsReport(true)));

				// look for the address of key1
				using (var tr = await db.BeginTransactionAsync(this.Cancellation))
				{
					var subspace = (await location.Resolve(tr))!;

					var addresses = await tr.GetAddressesForKeyAsync(subspace.Encode(1));
					Assert.That(addresses, Is.Not.Null);
					Log($"{subspace.Encode(1)} is stored at: {string.Join(", ", addresses)}");
					Assert.That(addresses.Length, Is.GreaterThan(0));
					Assert.That(addresses[0], Is.Not.Null.Or.Empty);

					//note: it is difficult to test the returned value, because it depends on the test db configuration
					// it will most probably be 127.0.0.1 unless you have customized the Test DB settings to point to somewhere else
					// either way, it should look like a valid IP address (IPv4 or v6?)

					for (int i = 0; i < addresses.Length; i++)
					{
						var addr = addresses[i];
						Log($"- {addr}");
						// we expect "IP:PORT"
						Assert.That(addr, Is.Not.Null.Or.Empty);
						Assert.That(addr, Does.Contain(':'), "Result address '{0}' should contain a port number", addr);
						int p = addr.IndexOf(':');
						Assert.That(System.Net.IPAddress.TryParse(addr.Substring(0, p), out IPAddress address), Is.True, "Result address '{0}' does not seem to have a valid IP address '{1}'", addr, addr.Substring(0, p));
						Assert.That(int.TryParse(addr.Substring(p + 1), out var port), Is.True, "Result address '{0}' does not seem to have a valid port number '{1}'", addr, addr.Substring(p + 1));
					}
				}

				// do the same but for a key that does not exist
				using (var tr = await db.BeginTransactionAsync(this.Cancellation))
				{
					var subspace = (await location.Resolve(tr))!;

					var addresses = await tr.GetAddressesForKeyAsync(subspace.Encode(404));
					Assert.That(addresses, Is.Not.Null);
					Log($"{subspace.Encode(404)} would be stored at: {string.Join(", ", addresses)}");

					// the API still return a list of addresses, probably of servers that would store this value if you would call Set(...)

					for (int i = 0; i < addresses.Length; i++)
					{
						var addr = addresses[i];
						Log($"- {addr}");
						// we expect "IP:PORT"
						Assert.That(addr, Is.Not.Null.Or.Empty);
						Assert.That(addr, Does.Contain(':'), "Result address '{0}' should contain a port number", addr);
						int p = addr.IndexOf(':');
						Assert.That(System.Net.IPAddress.TryParse(addr.Substring(0, p), out IPAddress address), Is.True, "Result address '{0}' does not seem to have a valid IP address '{1}'", addr, addr.Substring(0, p));
						Assert.That(int.TryParse(addr.Substring(p + 1), out var port), Is.True, "Result address '{0}' does not seem to have a valid port number '{1}'", addr, addr.Substring(p + 1));
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
			};
			using (var db = await Fdb.OpenAsync(options, this.Cancellation))
			{
				using (var tr = await db.BeginReadOnlyTransactionAsync(this.Cancellation))
				{
					tr.Options.WithReadAccessToSystemKeys();
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

						Log($"- {key.Key:X} : ({key.Value.Count}) {key.Value:P}");
						Log($"  > node       = {nodeId}");
						Log($"  > machine    = {machineId}");
						Log($"  > datacenter = {dataCenterId}");
					}

					Log();

					// dump keyServers
					var shards = await tr.GetRange(Fdb.System.KeyServers, Fdb.System.KeyServers + Fdb.System.MaxValue)
						.Select(kvp => new KeyValuePair<Slice, Slice>(kvp.Key.Substring(Fdb.System.KeyServers.Count), kvp.Value))
						.ToListAsync();
					Log($"Key Servers: {shards.Count} shard(s)");

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
						for (int i = 0; i < n; i++)
						{
							ids[i] = key.Value.Substring(12 + i * 16, 16).ToHexaString();
							distinctNodes.Add(ids[i]);
						}

						replicationFactor = Math.Max(replicationFactor, ids.Length);

						// the node id seems to be at offset 12

						//Log("- " + key.Value.Substring(0, 12).ToAsciiOrHexaString() + " : " + String.Join(", ", ids) + " = " + key.Key);
					}

					Log();
					Log($"Distinct nodes: {distinctNodes.Count}");
					foreach (var machine in distinctNodes)
					{
						Log("- " + machine);
					}

					Log();
					Log($"Cluster topology: {distinctNodes.Count} process(es) with {(replicationFactor == 1 ? "single" : replicationFactor == 2 ? "double" : replicationFactor == 3 ? "triple" : replicationFactor.ToString())} replication");
				}
			}
		}

		[Test]
		public async Task Test_VersionStamps_Share_The_Same_Token_Per_Transaction_Attempt()
		{
			// Verify that we can set version-stamped keys inside a transaction

			using (var db = await OpenTestDatabaseAsync())
			{
				using (var tr = await db.BeginTransactionAsync(this.Cancellation))
				{
					// should return a 80-bit incomplete stamp, using a random token
					var x = tr.CreateVersionStamp();
					Log($"> x  : {x.ToSlice():X} => {x}");
					Assert.That(x.IsIncomplete, Is.True, "Placeholder token should be incomplete");
					Assert.That(x.HasUserVersion, Is.False);
					Assert.That(x.UserVersion, Is.Zero);
					Assert.That(x.TransactionVersion >> 56, Is.EqualTo(0xFF), "Highest 8 bit of Transaction Version should be set to 1");
					Assert.That(x.TransactionOrder >> 12, Is.EqualTo(0xF), "Highest 4 bits of Transaction Order should be set to 1");

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
					Assert.That(y.TransactionOrder >> 12, Is.EqualTo(0xF), "Highest 4 bits of Transaction Order should be set to 1");

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
			// Verify that we can set version-stamped keys inside a transaction

			using (var db = await OpenTestDatabaseAsync())
			{
				var location = db.Root.ByKey("versionstamps");

				await CleanLocation(db, location);

				db.SetDefaultLogHandler(log => Log(log.GetTimingsReport(true)));

				VersionStamp vsActual; // will contain the actual version stamp used by the database

				Log("Inserting keys with version stamps:");
				using (var tr = await db.BeginTransactionAsync(this.Cancellation))
				{
					var subspace = (await location.Resolve(tr))!;

					// should return a 80-bit incomplete stamp, using a random token
					var vs = tr.CreateVersionStamp();
					Log($"> placeholder stamp: {vs} with token '{vs.ToSlice():X}'");

					// a single key using the 80-bit stamp
					tr.SetVersionStampedKey(subspace.Encode("foo", vs, 123), Value("Hello, World!"));

					// simulate a batch of 3 keys, using 96-bits stamps
					tr.SetVersionStampedKey(subspace.Encode("bar", tr.CreateVersionStamp(0)), Value("Zero"));
					tr.SetVersionStampedKey(subspace.Encode("bar", tr.CreateVersionStamp(1)), Value("One"));
					tr.SetVersionStampedKey(subspace.Encode("bar", tr.CreateVersionStamp(42)), Value("FortyTwo"));

					// value that contain the stamp
					var val = Slice.FromString("$$$$$$$$$$Hello World!"); // '$' will be replaced by the stamp
					Log($"> {val:X}");
					tr.SetVersionStampedValue(subspace.Encode("baz"), val, 0);

					val = Slice.FromString("Hello,") + vs.ToSlice() + Slice.FromString(", World!"); // the middle of the value should be replaced with the VersionStamp
					Log($"> {val:X}");
					tr.SetVersionStampedValue(subspace.Encode("jazz"), val);

					// need to be request BEFORE the commit
					var vsTask = tr.GetVersionStampAsync();

					await tr.CommitAsync();
					Dump(tr.GetCommittedVersion());

					// need to be resolved AFTER the commit
					vsActual = await vsTask;
					Log($"> actual stamp: {vsActual} with token '{vsActual.ToSlice():X}'");
				}

				await DumpSubspace(db, location);

				Log("Checking database content:");
				using (var tr = await db.BeginReadOnlyTransactionAsync(this.Cancellation))
				{
					var subspace = (await location.Resolve(tr))!;
					{
						var foo = await tr.GetRange(subspace.EncodeRange("foo")).SingleAsync();
						Log("> Found 1 result under (foo,)");
						Log($"- {subspace.ExtractKey(foo.Key):K} = {foo.Value:V}");
						Assert.That(foo.Value.ToString(), Is.EqualTo("Hello, World!"));

						var t = subspace.Unpack(foo.Key);
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
						var items = await tr.GetRange(subspace.EncodeRange("bar")).ToListAsync();
						Log($"> Found {items.Count} results under (bar,)");
						foreach (var item in items)
						{
							Log($"- {subspace.ExtractKey(item.Key):K} = {item.Value:V}");
						}

						Assert.That(items.Count, Is.EqualTo(3), "Should have found 3 keys under 'foo'");

						Assert.That(items[0].Value.ToString(), Is.EqualTo("Zero"));
						var vs0 = subspace.DecodeLast<VersionStamp>(items[0].Key);
						Assert.That(vs0.IsIncomplete, Is.False);
						Assert.That(vs0.HasUserVersion, Is.True);
						Assert.That(vs0.UserVersion, Is.EqualTo(0));
						Assert.That(vs0.TransactionVersion, Is.EqualTo(vsActual.TransactionVersion));
						Assert.That(vs0.TransactionOrder, Is.EqualTo(vsActual.TransactionOrder));

						Assert.That(items[1].Value.ToString(), Is.EqualTo("One"));
						var vs1 = subspace.DecodeLast<VersionStamp>(items[1].Key);
						Assert.That(vs1.IsIncomplete, Is.False);
						Assert.That(vs1.HasUserVersion, Is.True);
						Assert.That(vs1.UserVersion, Is.EqualTo(1));
						Assert.That(vs1.TransactionVersion, Is.EqualTo(vsActual.TransactionVersion));
						Assert.That(vs1.TransactionOrder, Is.EqualTo(vsActual.TransactionOrder));

						Assert.That(items[2].Value.ToString(), Is.EqualTo("FortyTwo"));
						var vs42 = subspace.DecodeLast<VersionStamp>(items[2].Key);
						Assert.That(vs42.IsIncomplete, Is.False);
						Assert.That(vs42.HasUserVersion, Is.True);
						Assert.That(vs42.UserVersion, Is.EqualTo(42));
						Assert.That(vs42.TransactionVersion, Is.EqualTo(vsActual.TransactionVersion));
						Assert.That(vs42.TransactionOrder, Is.EqualTo(vsActual.TransactionOrder));
					}

					{
						var baz = await tr.GetAsync(subspace.Encode("baz"));
						Log($"> {baz:X}");
						// ensure that the first 10 bytes have been overwritten with the stamp
						Assert.That(baz.Count, Is.GreaterThan(0), "Key should be present in the database");
						Assert.That(baz.StartsWith(vsActual.ToSlice()), Is.True, "The first 10 bytes should match the resolved stamp");
						Assert.That(baz.Substring(10), Is.EqualTo(Slice.FromString("Hello World!")), "The rest of the slice should be untouched");
					}
					{
						var jazz = await tr.GetAsync(subspace.Encode("jazz"));
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

		[Test]
		public async Task Test_GetMetadataVersion()
		{
			//note: this test may be vulnerable to exterior changes to the database!
			using (var db = await OpenTestDatabaseAsync())
			{
				// reading the mv twice in _should_ return the same value, unless the test cluster is used by another application!

				db.SetDefaultLogHandler(log => Log(log.GetTimingsReport(true)));

				var version1 = await db.ReadAsync(tr => tr.GetMetadataVersionKeyAsync(), this.Cancellation);
				Assert.That(version1, Is.Not.Null, "Version should be valid");
				Log($"Version1: {version1}");

				var version2 = await db.ReadAsync(tr => tr.GetMetadataVersionKeyAsync(), this.Cancellation);
				Assert.That(version1, Is.Not.Null, "Version should be valid");
				Log($"Version2: {version2}");

				Assume.That(version2, Is.EqualTo(version1), "Metadata version should be stable! Make sure the test cluster is not used concurrently when running this test!");
				// if it fails randomly here, maybe due to another process interfering with us!

				Log("Changing version...");
				await db.WriteAsync(tr => tr.TouchMetadataVersionKey(), this.Cancellation);

				var version3 = await db.ReadAsync(tr => tr.GetMetadataVersionKeyAsync(), this.Cancellation);
				Log($"Version3: {version3}");
				Assert.That(version3, Is.Not.Null.And.Not.EqualTo(version2), "Metadata version should have changed");

				// changing the metadata version and then reading it back from the same transaction should return <null>
				await db.WriteAsync(async tr =>
				{
					// We can read the version before
					var before = await tr.GetMetadataVersionKeyAsync();
					Log($"Before: {before}");
					Assert.That(before, Is.Not.Null);

					// Another read attempt should return the cached value
					var cached = await tr.GetMetadataVersionKeyAsync();
					Log($"Cached: {before}");
					Assert.That(cached, Is.Not.Null.And.EqualTo(before));

					// change the version from inside the transaction
					Log("Mutate!");
					tr.TouchMetadataVersionKey();

					// we should not be able to get the version anymore (should return null)
					var after = await tr.GetMetadataVersionKeyAsync();
					Log($"After: {after}");
					Assert.That(after, Is.Null, "Should not be able to get the version right after changing it from the same transaction.");

				}, this.Cancellation);
			}
		}

		[Test]
		public async Task Test_GetMetadataVersion_Custom_Keys()
		{
			using (var db = await OpenTestDatabaseAsync())
			{
				db.SetDefaultLogHandler(log => Log(log.GetTimingsReport(true)));

				const string foo = "Foo";
				const string bar = "Bar";
				const string baz = "Baz";

				// initial setup:
				// - Foo: version stamp
				// - Bar: different version stamp
				// - Baz: _missing_

				await db.WriteAsync(async tr =>
				{
					var subspace = (await db.Root.Resolve(tr))!;
					tr.TouchMetadataVersionKey(subspace.Encode(foo));
				}, this.Cancellation);
				await db.WriteAsync(async tr =>
				{
					var subspace = (await db.Root.Resolve(tr))!;
					tr.TouchMetadataVersionKey(subspace.Encode(bar));
				}, this.Cancellation);
				await db.WriteAsync(async tr =>
				{
					var subspace = (await db.Root.Resolve(tr))!;
					tr.Clear(subspace.Encode(baz));
				}, this.Cancellation);

				// changing the metadata version and then reading it back from the same transaction CANNOT WORK!
				await db.WriteAsync(async tr =>
				{
					var subspace = (await db.Root.Resolve(tr))!;

					// We can read the version before
					var before1 = await tr.GetMetadataVersionKeyAsync(subspace.Encode(foo));
					Log($"Foo (before): {before1}");
					Assert.That(before1, Is.Not.Null);

					// Another read attempt should return the cached value
					var before2 = await tr.GetMetadataVersionKeyAsync(subspace.Encode(bar));
					Log($"Bar (before): {before2}");
					Assert.That(before2, Is.Not.Null.And.Not.EqualTo(before1));

					// Another read attempt should return the cached value
					var before3 = await tr.GetMetadataVersionKeyAsync(subspace.Encode(baz));
					Log($"Baz (before): {before3}");
					Assert.That(before3, Is.EqualTo(new VersionStamp()));

					// change the version from inside the transaction
					Log("Mutate Foo!");
					tr.TouchMetadataVersionKey(subspace.Encode(foo));

					// we should not be able to get the version anymore (should return null)
					var after1 = await tr.GetMetadataVersionKeyAsync(subspace.Encode(foo));
					Log($"Foo (after): {after1}");
					Assert.That(after1, Is.Null, "Should not be able to get the version right after changing it from the same transaction.");

					// We can read the version before
					var after2 = await tr.GetMetadataVersionKeyAsync(subspace.Encode(bar));
					Log($"Bar (after): {after2}");
					Assert.That(after2, Is.Not.Null.And.EqualTo(before2));

					// We can read the version before
					var after3 = await tr.GetMetadataVersionKeyAsync(subspace.Encode(baz));
					Log($"Baz (after): {after3}");
					Assert.That(after3, Is.EqualTo(new VersionStamp()));

				}, this.Cancellation);

			}
		}

		[Test, Category("LongRunning")][Ignore("Takes too much time!")]
		public async Task Test_VeryBadPractice_Future_Fuzzer()
		{
#if DEBUG
			const int DURATION_SEC = 5;
#else
			const int DURATION_SEC = 20;
#endif
			const int R = 100;

			using (var db = await OpenTestDatabaseAsync())
			{
				var rnd = new Random();
				int seed = rnd.Next();
				Log($"Using random seed {seed}");
				rnd = new Random(seed);

				await db.WriteAsync(async (tr) =>
				{
					var subspace = (await db.Root.Resolve(tr))!;
					for (int i = 0; i < R; i++)
					{
						tr.Set(subspace.Encode("Fuzzer", i), Slice.FromInt32(i));
					}
				}, this.Cancellation);

				var start = DateTime.UtcNow;
				Log($"This test will run for {DURATION_SEC} seconds");

				int time = 0;

				var alive = new List<IFdbTransaction>();
				var sb = new StringBuilder();

				while (DateTime.UtcNow - start < TimeSpan.FromSeconds(DURATION_SEC))
				{
					switch (rnd.Next(10))
					{
						case 0:
						{ // start a new transaction
							sb.Append('T');
							var tr = db.BeginTransaction(FdbTransactionMode.Default, this.Cancellation);
							alive.Add(tr);

							break;
						}
						case 1:
						{ // drop a random transaction
							if (alive.Count == 0) continue;
							sb.Append('L');
							int p = rnd.Next(alive.Count);

							alive.RemoveAt(p);
							//no dispose
							break;
						}
						case 2:
						{ // dispose a random transaction
							if (alive.Count == 0) continue;
							sb.Append('D');
							int p = rnd.Next(alive.Count);

							var tr = alive[p];
							tr.Dispose();
							alive.RemoveAt(p);
							break;
						}
						case 3:
						{ // GC!
							sb.Append('C');
							var tr = db.BeginTransaction(FdbTransactionMode.ReadOnly, this.Cancellation);
							alive.Add(tr);
							_ = await tr.GetReadVersionAsync();
							break;
						}

						case 4:
						case 5:
						case 6:
						{ // read a random value from a random transaction
							sb.Append('G');
							if (alive.Count == 0) break;
							int p = rnd.Next(alive.Count);
							var tr = alive[p];

							int x = rnd.Next(R);
							try
							{
								var subspace = (await db.Root.Resolve(tr))!; //TODO: cache subspace instance alongside transaction?
								_ = await tr.GetAsync(subspace.Encode("Fuzzer", x));
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
							if (alive.Count == 0) break;
							int p = rnd.Next(alive.Count);
							var tr = alive[p];

							int x = rnd.Next(R);
							var subspace = (await db.Root.Resolve(tr))!; //TODO: cache subspace instance alongside transaction?
							_ = tr.GetAsync(subspace.Encode("Fuzzer", x)).ContinueWith((_) => sb.Append('!') /*BUGBUG: locking ?*/, TaskContinuationOptions.NotOnRanToCompletion);
							// => t is not stored
							break;
						}

					}

					if ((time++) % 80 == 0)
					{
						Log(sb.ToString());
						Log($"State: {alive.Count}");
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

		[Test]
		public async Task Test_Value_Checks()
		{
			// Verify that value-check perform as expected:
			// - We have a set of keys that are used for value checks (AAA, BBB, ...)
			// - We have a "witness" key that will be used to verify if the transaction actually committed or not.
			// - On each retry of the retry-loop, we will check that the previous iteration did update the context state as it should have.

			//NOTE: this test is vulnerable to transient errors that could happen to the cluster while it runs! (timeouts, etc...)
			//TODO: we should use a more robust way to "skip" the retries that are for unrelated reasons?

			using (var db = await OpenTestDatabaseAsync())
			{
				var location = db.Root.ByKey("value_checks");

				await CleanLocation(db, location);

				db.SetDefaultLogHandler(log => Log(log.GetTimingsReport(true)));

				var initialA = Slice.FromStringAscii("Initial value of AAA");
				var initialB = Slice.FromStringAscii("Initial value of BBB");

				async Task RunCheck(Func<IFdbTransaction, bool> test, Func<IFdbTransaction, IDynamicKeySubspace, Task> handler, bool shouldCommit)
				{
					// read previous witness value
					await db.WriteAsync(async tr =>
					{
						tr.StopLogging();
						var subspace = (await location.Resolve(tr))!;

						tr.ClearRange(subspace.ToRange());
						tr.Set(subspace.Encode("AAA"), initialA);
						tr.Set(subspace.Encode("BBB"), initialB);
						// CCC does not exist
						tr.Set(subspace.Encode("Witness"), Slice.FromStringAscii("Initial witness value"));
					}, this.Cancellation);

					await db.WriteAsync(async tr =>
					{
						var checks = tr.Context.GetValueChecksFromPreviousAttempt(result: FdbValueCheckResult.Failed);
						Log($"- Retry #{tr.Context.Retries}: prev={tr.Context.PreviousError}, checksFromPrevious={checks.Count}");
						foreach (var check in checks)
						{
							Log($"  > [{check.Tag}]: {check.Result}, {FdbKey.Dump(check.Key)} => {check.Expected:V} vs {check.Actual:V}");
						}
						if (tr.Context.Retries > 10) Assert.Fail("Too many retries!");

						if (!test(tr)) return;

						var subspace = (await location.Resolve(tr))!;
						await handler(tr, subspace);
						tr.Set(subspace.Encode("Witness"), Slice.FromStringAscii("New witness value"));
					}, this.Cancellation);
					await DumpSubspace(db, location);

					// read back the witness key to see if commit happened or not.
					var actual = await db.ReadAsync(async tr =>
					{
						tr.StopLogging();
						var subspace = (await location.Resolve(tr))!;
						return await tr.GetAsync(subspace.Encode("Witness"));
					}, this.Cancellation);

					if (shouldCommit)
						Assert.That(actual, Is.EqualTo(Slice.FromStringAscii("New witness value")), "Transaction SHOULD have changed the database!");
					else
						Assert.That(actual, Is.EqualTo(Slice.FromStringAscii("Initial witness value")), "Transaction should NOT have changed the database!");
				}

				// Checking a key with its actual value should pass
				{
					Log("Value check for AAA == CORRECT => expect PASS...");
					await RunCheck(
						(tr) =>
						{
							if (tr.Context.Retries == 0)
							{ // first attempt: all should be default
								Assert.That(tr.Context.TestValueCheckFromPreviousAttempt("fooCheck"), Is.EqualTo(FdbValueCheckResult.Unknown));
								Assert.That(tr.Context.GetValueChecksFromPreviousAttempt("fooCheck"), Is.Empty);
								return true;
							}
							else
							{ // we don't expect any retries
								Assert.Fail("Should not execute more than once!");
								return false;
							}
						},
						(tr, subspace) =>
						{
							tr.Context.AddValueCheck("fooCheck", subspace.Encode("AAA"), initialA);
							return Task.CompletedTask;
						},
						shouldCommit: true
					);
				}

				// Checking a missing key with nil should pass
				{
					Log("Value check for CCC == Nil => expect PASS...");
					await RunCheck(
						(tr) =>
						{
							if (tr.Context.Retries == 0)
							{ // first attempt: all should be default
								Assert.That(tr.Context.TestValueCheckFromPreviousAttempt("fooCheck"), Is.EqualTo(FdbValueCheckResult.Unknown));
								Assert.That(tr.Context.GetValueChecksFromPreviousAttempt("fooCheck"), Is.Empty);
								return true;
							}
							else
							{ // we don't expect any retries
								Assert.Fail("Should not execute more than once!");
								return false;
							}
						},
						(tr, subspace) =>
						{
							tr.Context.AddValueCheck("fooCheck", subspace.Encode("CCC"), Slice.Nil);
							return Task.CompletedTask;
						},
						shouldCommit: true
					);
				}

				// Checking a multiple keys should pass
				{
					Log("Value check for (AAA == CORRECT) & (BBB == CORRECT) & (CCC == nil) => expect PASS...");
					await RunCheck(
						(tr) =>
						{
							if (tr.Context.Retries == 0)
							{ // first attepmpt: all should be default
								Assert.That(tr.Context.TestValueCheckFromPreviousAttempt("fooCheck"), Is.EqualTo(FdbValueCheckResult.Unknown));
								Assert.That(tr.Context.GetValueChecksFromPreviousAttempt("fooCheck"), Is.Empty);
								Assert.That(tr.Context.TestValueCheckFromPreviousAttempt("barCheck"), Is.EqualTo(FdbValueCheckResult.Unknown));
								Assert.That(tr.Context.GetValueChecksFromPreviousAttempt("barCheck"), Is.Empty);
								return true;
							}
							else
							{ // we don't expect any retries
								Assert.Fail("Should not execute more than once!");
								return false;
							}
						},
						(tr, subspace) =>
						{
							tr.Context.AddValueCheck("fooCheck", subspace.Encode("AAA"), initialA);
							tr.Context.AddValueCheck("barCheck", subspace.Encode("BBB"), initialB);
							return Task.CompletedTask;
						},
						shouldCommit: true
					);
				}

				// Checking a key with a different value should fail
				{
					Log("Value check BBB == INCORRECT => expect FAIL...");
					await RunCheck(
						(tr) =>
						{
							switch (tr.Context.Retries)
							{
								case 0:
									// on first attempt, everything should be default
									Assert.That(tr.Context.TestValueCheckFromPreviousAttempt("fooCheck"), Is.EqualTo(FdbValueCheckResult.Unknown));
									Assert.That(tr.Context.GetValueChecksFromPreviousAttempt("fooCheck"), Is.Empty);
									return true;
								case 1:
									// on second attempt, value-check "fooCheck" should be triggered
									Assert.That(tr.Context.TestValueCheckFromPreviousAttempt("fooCheck"), Is.EqualTo(FdbValueCheckResult.Failed));
									Assert.That(tr.Context.GetValueChecksFromPreviousAttempt("fooCheck"), Has.Count.EqualTo(1));
									Assert.That(tr.Context.GetValueChecksFromPreviousAttempt("fooCheck")[0].Result, Is.EqualTo(FdbValueCheckResult.Failed));
									Assert.That(tr.Context.TestValueCheckFromPreviousAttempt("unrelated"), Is.EqualTo(FdbValueCheckResult.Unknown));
									Assert.That(tr.Context.GetValueChecksFromPreviousAttempt("unrelated"), Is.Empty);
									Assert.That(tr.Context.PreviousError, Is.EqualTo(FdbError.NotCommitted), "Should emulate a 'not_committed'");
									return false; // stop
								default:
									Assert.Fail("Should not execute more than twice!");
									return false;
							}
						},
						(tr, subspace) =>
						{
							tr.Context.AddValueCheck("fooCheck", subspace.Encode("AAA"), Slice.FromStringAscii("Different value of AAA"));
							return Task.CompletedTask;
						},
						shouldCommit: false
					);
				}

				// Checking a missing key with a value should fail
				{
					Log("Value check CCC == SOMETHING => expect FAIL...");
					await RunCheck(
						(tr) =>
						{
							switch (tr.Context.Retries)
							{
								case 0:
									// on first attempt, everything should be default
									Assert.That(tr.Context.TestValueCheckFromPreviousAttempt("fooCheck"), Is.EqualTo(FdbValueCheckResult.Unknown));
									Assert.That(tr.Context.GetValueChecksFromPreviousAttempt("fooCheck"), Is.Empty);
									return true;
								case 1:
									// on second attempt, value-check "fooCheck" should be triggered
									Assert.That(tr.Context.TestValueCheckFromPreviousAttempt("fooCheck"), Is.EqualTo(FdbValueCheckResult.Failed));
									Assert.That(tr.Context.GetValueChecksFromPreviousAttempt("fooCheck"), Has.Count.EqualTo(1));
									Assert.That(tr.Context.GetValueChecksFromPreviousAttempt("fooCheck")[0].Result, Is.EqualTo(FdbValueCheckResult.Failed));
									Assert.That(tr.Context.TestValueCheckFromPreviousAttempt("unrelated"), Is.EqualTo(FdbValueCheckResult.Unknown));
									Assert.That(tr.Context.GetValueChecksFromPreviousAttempt("unrelated"), Is.Empty);
									Assert.That(tr.Context.PreviousError, Is.EqualTo(FdbError.NotCommitted), "Should emulate a 'not_committed'");
									return false; // stop
								default:
									Assert.Fail("Should not execute more than twice!");
									return false;
							}
						},
						(tr, subspace) =>
						{
							tr.Context.AddValueCheck("fooCheck", subspace.Encode("CCC"), Slice.FromStringAscii("Some value"));
							return Task.CompletedTask;
						},
						shouldCommit: false
					);
				}

				// Changing the value after the check should not be observed by the check
				{
					Log("Value check AAA == CORRECT; Set AAA = DIFFERENT => expect PASS...");
					await RunCheck(
						(tr) =>
						{
							switch (tr.Context.Retries)
							{
								case 0:
									// on first attempt, everything should be default
									Assert.That(tr.Context.TestValueCheckFromPreviousAttempt("fooCheck"), Is.EqualTo(FdbValueCheckResult.Unknown));
									Assert.That(tr.Context.GetValueChecksFromPreviousAttempt("fooCheck"), Is.Empty);
									return true;
								default:
									// should not fire twice!
									Assert.Fail("Should not execute more than once!");
									return false;
							}
						},
						(tr, subspace) =>
						{
							// check
							tr.Context.AddValueCheck("fooCheck", subspace.Encode("AAA"), initialA);
							// then change
							tr.Set(subspace.Encode("AAA"), Slice.FromStringAscii("Different value for AAA"));
							return Task.CompletedTask;
						},
						shouldCommit: true
					);
				}

				// Clearing the key after the check should not be observed by the check
				{
					Log("Value check AAA == CORRECT; Clear AAA expect PASS...");
					await RunCheck(
						(tr) =>
						{
							switch (tr.Context.Retries)
							{
								case 0:
									// on first attempt, everything should be default
									Assert.That(tr.Context.TestValueCheckFromPreviousAttempt("fooCheck"), Is.EqualTo(FdbValueCheckResult.Unknown));
									Assert.That(tr.Context.GetValueChecksFromPreviousAttempt("fooCheck"), Is.Empty);
									return true;
								default:
									// should not fire twice!
									Assert.Fail("Should not execute more than once!");
									return false;
							}
						},
						(tr, subspace) =>
						{
							// check
							tr.Context.AddValueCheck("fooCheck", subspace.Encode("AAA"), initialA);
							// then change
							tr.Clear(subspace.Encode("AAA"));
							return Task.CompletedTask;
						},
						shouldCommit: true
					);
				}

				// Changing the value BEFORE the check should be observed by the check
				{
					Log("Set AAA = DIFFERENT; Value check AAA == CORRECT => expect FAIL...");
					await RunCheck(
						(tr) =>
						{
							switch (tr.Context.Retries)
							{
								case 0:
									// on first attempt, everything should be default
									Assert.That(tr.Context.TestValueCheckFromPreviousAttempt("fooCheck"), Is.EqualTo(FdbValueCheckResult.Unknown));
									Assert.That(tr.Context.GetValueChecksFromPreviousAttempt("fooCheck"), Is.Empty);
									return true;
								case 1:
									// on second attempt, value-check "fooCheck" should be triggered
									Assert.That(tr.Context.TestValueCheckFromPreviousAttempt("fooCheck"), Is.EqualTo(FdbValueCheckResult.Failed));
									Assert.That(tr.Context.GetValueChecksFromPreviousAttempt("fooCheck"), Has.Count.EqualTo(1));
									Assert.That(tr.Context.GetValueChecksFromPreviousAttempt("fooCheck")[0].Result, Is.EqualTo(FdbValueCheckResult.Failed));
									Assert.That(tr.Context.TestValueCheckFromPreviousAttempt("unrelated"), Is.EqualTo(FdbValueCheckResult.Unknown));
									Assert.That(tr.Context.GetValueChecksFromPreviousAttempt("unrelated"), Is.Empty);
									Assert.That(tr.Context.PreviousError, Is.EqualTo(FdbError.NotCommitted), "Should emulate a 'not_committed'");
									return false; // stop
								default:
									Assert.Fail("Should not execute more than twice!");
									return false;
							}
						},
						(tr, subspace) =>
						{
							// change
							tr.Set(subspace.Encode("AAA"), Slice.FromStringAscii("Different value for AAA"));
							// then check
							tr.Context.AddValueCheck("fooCheck", subspace.Encode("AAA"), initialA);
							return Task.CompletedTask;
						},
						shouldCommit: false
					);
				}

				// Clearing a key BEFORE the check should be observed by the check
				{
					Log("Clear AAA; Value check AAA == CORRECT => expect FAIL...");
					await RunCheck(
						(tr) =>
						{
							switch (tr.Context.Retries)
							{
								case 0:
									// on first attempt, everything should be default
									Assert.That(tr.Context.TestValueCheckFromPreviousAttempt("fooCheck"), Is.EqualTo(FdbValueCheckResult.Unknown));
									Assert.That(tr.Context.GetValueChecksFromPreviousAttempt("fooCheck"), Is.Empty);
									return true;
								case 1:
									// on second attempt, value-check "fooCheck" should be triggered
									Assert.That(tr.Context.TestValueCheckFromPreviousAttempt("fooCheck"), Is.EqualTo(FdbValueCheckResult.Failed));
									Assert.That(tr.Context.GetValueChecksFromPreviousAttempt("fooCheck"), Has.Count.EqualTo(1));
									Assert.That(tr.Context.GetValueChecksFromPreviousAttempt("fooCheck")[0].Result, Is.EqualTo(FdbValueCheckResult.Failed));
									Assert.That(tr.Context.TestValueCheckFromPreviousAttempt("unrelated"), Is.EqualTo(FdbValueCheckResult.Unknown));
									Assert.That(tr.Context.GetValueChecksFromPreviousAttempt("unrelated"), Is.Empty);
									Assert.That(tr.Context.PreviousError, Is.EqualTo(FdbError.NotCommitted), "Should emulate a 'not_committed'");
									return false; // stop
								default:
									Assert.Fail("Should not execute more than twice!");
									return false;
							}
						},
						(tr, subspace) =>
						{
							// change
							tr.Clear(subspace.Encode("AAA"));
							// then check
							tr.Context.AddValueCheck("fooCheck", subspace.Encode("AAA"), initialA);
							return Task.CompletedTask;
						},
						shouldCommit: false
					);
				}
			}
		}

		[Test]
		public async Task Test_Value_Checks_Retries_On_Application_Exception()
		{
			// If we observe an application exception being thrown by the handler, normally we would stop the retry loop there.
			// But if there was at least one failed value-check, we HAVE to retry because it is possible that the application threw due to some invalid assumption.
			// Normally, any layer that used cached data will observe the failed check, and re-validate the cache.
			// If the application error was caused by this stale data, then it should not throw in the new attempt.
			// If the application error was caused by something completely unrelated, then it should throw again, and we should NOT retry

			using (var db = await OpenTestDatabaseAsync())
			{
				var location = db.Root.ByKey("value_checks");

				db.SetDefaultLogHandler(log => Log(log.GetTimingsReport(true)));

				for (int i = 0; i < 15; i++)
				{

					if (i % 5 == 0)
					{
						Log("Clear the database...");

						await CleanLocation(db, location);

						// if the application code fails we have to make sure that if there was also a failed value-check, the handler retries again!

						await db.WriteAsync(async tr =>
						{
							var subspace = (await location.Resolve(tr))!;
							tr.Set(subspace.Encode("Foo"), Slice.FromStringAscii("NotReady"));
							// Bar does not exist
						}, this.Cancellation);
					}

					var task = db.ReadWriteAsync(async tr =>
					{
						//note: this subspace does not use the DL so it does not introduce any value checks!
						var subspace = (await location.Resolve(tr))!;

						if (tr.Context.TestValueCheckFromPreviousAttempt("foo") == FdbValueCheckResult.Failed)
						{
							Log("# Oh, no! 'foo' check failed previously, check and initialze the db if required...");

							tr.Annotate("APP: doing the actual work to check the state of the db, and initialize the schema if required...");

							// read foo, and update the Bar key accordingly
							var foo = await tr.GetAsync(subspace.Encode("Foo"));
							if (foo.ToStringAscii() == "NotReady")
							{
								tr.Annotate("APP: initializing the database!");
								Log("# Moving 'foo' from Value1 to Value2 and setting Bar...");
								tr.Set(subspace.Encode("Foo"), Slice.FromStringAscii("Ready"));
								tr.Set(subspace.Encode("Bar"), Slice.FromStringAscii("Something"));
							}
						}
						else
						{
							tr.Annotate("APP: I'm feeling lucky! Let's assume the db is already initialized");
							tr.Context.AddValueCheck("foo", subspace.Encode("Foo"), Slice.FromStringAscii("Ready"));
						}
						// let's that if "Foo" was equal to "Value2", then "Bar" SHOULD exist
						// We simulate some application code reading the "Bar" value, and then finding out that it does not exist

						tr.Annotate("APP: The value of 'Bar' better not be empty...");
						var x = await tr.GetAsync(subspace.Encode("Bar"));
						Log($"On attempt #{tr.Context.Retries} we found the value of Bar to be '{x}'");
						if (x.IsNull)
						{
							tr.Annotate("APP: UH OH... something's wrong! let's throw an exception!!");
							throw new InvalidOperationException("Oh noes! There is some corruption in the database!");
						}

						return x.ToStringAscii();
					}, this.Cancellation);

					Assert.That(async () => await task, Is.EqualTo("Something"));
				}
			}
		}

		[Test]
		public async Task Test_Can_Get_Approximate_Size()
		{
			using (var db = await OpenTestDatabaseAsync())
			{
				// GET(KEY)
				Log("GET(KEY):");
				await db.ReadWriteAsync(
					async (tr) =>
					{
						// at the start, we expect a size of 0
						var size = await tr.GetApproximateSizeAsync();
						Log($"> Size at the start => {size}");
						Assert.That(size, Is.EqualTo(0));

						// currently, the formula seems to be: GET(KEY, VALUE) => 25 + (2 * KEY.Length)

						await tr.GetAsync(Slice.Empty);
						var prev = size;
						size = await tr.GetApproximateSizeAsync();
						Log($"> Size after reading '' => {size} (+{size - prev})");
						Assert.That(size, Is.GreaterThan(0));

						await tr.GetAsync(Literal("A"));
						prev = size;
						size = await tr.GetApproximateSizeAsync();
						Log($"> Size after reading 'A' => {size} (+{size - prev})");
						Assert.That(size, Is.GreaterThan(0));

						await tr.GetAsync(Literal("B"));
						prev = size;
						size = await tr.GetApproximateSizeAsync();
						Log($"> Size after reading 'B' => {size} (+{size - prev})");
						Assert.That(size, Is.GreaterThan(0));

						await tr.GetAsync(Literal("AB"));
						prev = size;
						size = await tr.GetApproximateSizeAsync();
						Log($"> Size after reading 'AB' => {size} (+{size - prev})");
						Assert.That(size, Is.GreaterThan(0));

						await tr.GetAsync(Literal(new string('z', 1000)));
						prev = size;
						size = await tr.GetApproximateSizeAsync();
						Log($"> Size after reading 1k*'z' => {size} (+{size - prev})");
						Assert.That(size, Is.GreaterThan(0));

						// prevent the transaction from commiting !
						tr.Reset();

						// after the reset, we expect the size to be back at 0
						size = await tr.GetApproximateSizeAsync();
						Log($"> Size after reset => {size}");
						Assert.That(size, Is.EqualTo(0));

						return size;
					},
					this.Cancellation);

				// SET(KEY, VALUE)
				Log("SET(KEY, VALUE):");
				await db.WriteAsync(
					async (tr) =>
					{
						// at the start, we expect a size of 0
						var size = await tr.GetApproximateSizeAsync();
						Log($"> Size at the start => {size}");
						Assert.That(size, Is.EqualTo(0));

						// we will NOT commit the transaction, so we can simply write "anywhere"

						// currently, the formula seems to be: SET(KEY, VALUE) => 53 + (3 * KEY.Length) + VALUE.Length

						tr.Set(Slice.Empty, Slice.Empty);
						var prev = size;
						size = await tr.GetApproximateSizeAsync();
						Log($"> Size after writing '' = '' => {size} (+{size - prev})");
						Assert.That(size, Is.GreaterThan(0));

						tr.Set(Literal("A"), Slice.Empty);
						prev = size;
						size = await tr.GetApproximateSizeAsync();
						Log($"> Size after writing 'A' = '' => {size} (+{size - prev})");
						Assert.That(size, Is.GreaterThan(prev));

						tr.Set(Literal("B"), Slice.Empty);
						prev = size;
						size = await tr.GetApproximateSizeAsync();
						Log($"> Size after writing 'B' = '' => {size} (+{size - prev})");
						Assert.That(size, Is.GreaterThan(prev));

						tr.Set(Literal("AB"), Slice.Empty);
						prev = size;
						size = await tr.GetApproximateSizeAsync();
						Log($"> Size after writing 'AB' = '' => {size} (+{size - prev})");
						Assert.That(size, Is.GreaterThan(prev));

						tr.Set(Literal("C"), Value("A"));
						prev = size;
						size = await tr.GetApproximateSizeAsync();
						Log($"> Size after writing 'C' = 'A' => {size} (+{size - prev})");
						Assert.That(size, Is.GreaterThan(prev));

						tr.Set(Literal("D"), Value(new string('z', 1000)));
						prev = size;
						size = await tr.GetApproximateSizeAsync();
						Log($"> Size after writing 'D' = 1k * 'z' => {size} (+{size - prev})");
						Assert.That(size, Is.GreaterThan(prev));

						// prevent the transaction from commiting !
						tr.Reset();

						// after the reset, we expect the size to be back at 0
						size = await tr.GetApproximateSizeAsync();
						Log($"> Size after reset => {size}");
						Assert.That(size, Is.EqualTo(0));

					},
					this.Cancellation);

				// SET(KEY, VALUE)
				Log("CLEAR(KEY):");
				await db.WriteAsync(
					async (tr) =>
					{
						// at the start, we expect a size of 0
						var size = await tr.GetApproximateSizeAsync();
						Log($"> Size at the start => {size}");
						Assert.That(size, Is.EqualTo(0));

						// we will NOT commit the transaction, so we can simply write "anywhere"

						// currently, the formula seems to be: CLEAR(KEY) => 50 + (4 * KEY.Length)

						tr.Clear(Slice.Empty);
						var prev = size;
						size = await tr.GetApproximateSizeAsync();
						Log($"> Size after clearaing '' => {size} (+{size - prev})");
						Assert.That(size, Is.GreaterThan(0));

						tr.Clear(Literal("A"));
						prev = size;
						size = await tr.GetApproximateSizeAsync();
						Log($"> Size after clearing 'A' => {size} (+{size - prev})");
						Assert.That(size, Is.GreaterThan(prev));

						tr.Clear(Literal("B"));
						prev = size;
						size = await tr.GetApproximateSizeAsync();
						Log($"> Size after clearing 'B' => {size} (+{size - prev})");
						Assert.That(size, Is.GreaterThan(prev));

						tr.Clear(Literal("AB"));
						prev = size;
						size = await tr.GetApproximateSizeAsync();
						Log($"> Size after clearing 'AB' => {size} (+{size - prev})");
						Assert.That(size, Is.GreaterThan(prev));

						tr.Clear(Literal(new string('z', 1000)));
						prev = size;
						size = await tr.GetApproximateSizeAsync();
						Log($"> Size after clearing 1k * 'z' => {size} (+{size - prev})");
						Assert.That(size, Is.GreaterThan(prev));

						// prevent the transaction from commiting !
						tr.Reset();

						// after the reset, we expect the size to be back at 0
						size = await tr.GetApproximateSizeAsync();
						Log($"> Size after reset => {size}");
						Assert.That(size, Is.EqualTo(0));

					},
					this.Cancellation);
			}
		}

		[Test]
		public async Task Test_Can_Get_Range_Split_Points()
		{
			const int NUM_ITEMS = 100_000;
			const int VALUE_SIZE = 50;
			const int CHUNK_SIZE = (NUM_ITEMS * (VALUE_SIZE + 16)) / 100; // we would like to split in ~100 chunks

			using (var db = await OpenTestDatabaseAsync())
			{
				var location = db.Root.ByKey("range_split_points");
				await CleanLocation(db, location);

				// we will setup a list of 1K keys with randomized value size (that we keep track of)
				var rnd = new Random(123456);
				var values = Enumerable.Range(0, NUM_ITEMS).Select(i => Slice.Random(rnd, VALUE_SIZE)).ToArray();

				const int BATCH_SIZE = 1_000_000 / VALUE_SIZE;
				Log($"Creating {values.Length:N0} keys ({VALUE_SIZE:N0} bytes per key) with {NUM_ITEMS * VALUE_SIZE:N0} total bytes ({BATCH_SIZE:N0} per batch)");
				for (int i = 0; i < values.Length; i += BATCH_SIZE)
				{
					await db.WriteAsync(async (tr) =>
					{
						var subspace = (await location.Resolve(tr))!;

						// fill the db with keys from (0,) = XXX to (N-1,) = XXX
						for (int j = 0; j < BATCH_SIZE && i + j < values.Length; j++)
						{
							tr.Set(subspace.Encode(i + j), values[i + j]);
						}
					}, this.Cancellation);
				}

				//db.SetDefaultLogHandler(log => Log(log.GetTimingsReport(true)));

				Log($"Get split points for chunks of {CHUNK_SIZE:N0} bytes...");
				var keys = await db.ReadAsync(async (tr) =>
				{
					var subspace = (await location.Resolve(tr))!;

					var begin = subspace.Encode(0);
					var end = subspace.Encode(values.Length);

					var keys = await tr.GetRangeSplitPointsAsync(begin, end, CHUNK_SIZE);
					Assert.That(keys, Is.Not.Null.Or.Empty);
					Log($"Found {keys.Length} split points");

					// looking at the implementation, it guarantess that the first and last "split points" will be the bounds of the range repeated (even if the keys do not exist)
					Assert.That(keys, Has.Length.GreaterThan(2), "We expect at least 1 split point between the bounds of the range!");
					Assert.That(keys[0], Is.EqualTo(begin), "First key should be the start of the range");
					Assert.That(keys[keys.Length - 1], Is.EqualTo(end), "Last key should be the end of the range");

					// all keys should be in ascending order
					for (int i = 1; i < keys.Length; i++)
					{
						Assert.That(keys[i], Is.GreaterThan(keys[i - 1]), "Split points should be sorted");
					}
					return keys;
				}, this.Cancellation);

				var chunks = new List<KeyValuePair<Slice, Slice>[]>();

				for(int i = 0; i < keys.Length - 1; i++)
				{
					var (chunk, begin, end) = await db.ReadAsync(async tr => 
					{
						var subspace = (await location.Resolve(tr))!;

						// we will get all the keys in between and dump some statistics
						var range = KeyRange.Create(keys[i], keys[i + 1]);
						var chunk = await tr.GetRange(range).ToArrayAsync();

						return (chunk, subspace.PrettyPrint(range.Begin), subspace.PrettyPrint(range.End));
					}, this.Cancellation);

					chunks.Add(chunk);
					Assert.That(chunk, Is.Not.Null.Or.Empty, "There should be at least one key in chunk {0}..{1}", begin, end);
					var actualChunkSize = chunk.Sum(kv => kv.Key.Count + kv.Value.Count);
					Log($"> {begin} .. {end}:\t{chunk.Length,6:N0} results, size(k+v) = {actualChunkSize,7:N0} bytes, ratio = {(100.0 * actualChunkSize) / CHUNK_SIZE,6:N0}%");

				}

				Log($"Statistics: smallest = {chunks.Min(c => c.Sum(kv => kv.Value.Count)):N0} bytes, largest = {chunks.Max(c => c.Sum(kv => kv.Value.Count)):N0} bytes, average = {chunks.Average(c => c.Sum(kv => kv.Value.Count)):N0} bytes");

				// we should have read all our keys!
				Assert.That(chunks.Sum(c => c.Length), Is.EqualTo(NUM_ITEMS), "All keys should be accounted for");
			}
		}

		[Test]
		public async Task Test_Can_Get_Estimated_Range_Size_Bytes()
		{
			const int NUM_ITEMS = 50_000;
			const int VALUE_SIZE = 32;

			using (var db = await OpenTestDatabaseAsync())
			{
				var location = db.Root.ByKey("range_size_bytes");
				await CleanLocation(db, location);

				// we will setup a list of N keys with randomized value size (that we keep track of)
				var rnd = new Random(123456);
				var values = Enumerable.Range(0, NUM_ITEMS).Select(i => Slice.Random(rnd, VALUE_SIZE)).ToArray();

				Log($"Creating {values.Length:N0} keys ({VALUE_SIZE:N0} bytes per key) with {NUM_ITEMS * VALUE_SIZE:N0} total bytes");
				await db.WriteAsync(async (tr) =>
				{
					var subspace = (await location.Resolve(tr))!;

					// fill the db with keys from (0,) = XXX to (N-1,) = XXX
					for (int i = 0; i < values.Length; i++)
					{
						tr.Set(subspace.Encode(i), values[i]);
					}
				}, this.Cancellation);

				Log($"Get estimated ranges size...");
				for (int i = 0; i < 25; i++)
				{
					await db.ReadAsync(async (tr) =>
					{
						var subspace = (await location.Resolve(tr))!;

						int x = rnd.Next(NUM_ITEMS);
						int y = rnd.Next(NUM_ITEMS);
						if (x == y) y++;
						if (x > y) { (x, y) = (y, x); }

						var begin = subspace.Encode(x);
						var end = subspace.Encode(y);

						var estimatedSize = await tr.GetEstimatedRangeSizeBytesAsync(begin, end);

						var exactSize = await tr.GetRange(begin, end).SumAsync(kv => kv.Value.Count + kv.Key.Count);

						Log($"> ({x,6:N0} .. {y,6:N0}): estimated = {estimatedSize,9:N0} bytes, exact(key+value) = {exactSize,9:N0} bytes, ratio = {(100.0 * estimatedSize) / exactSize,6:N1}%");
						Assert.That(estimatedSize, Is.GreaterThanOrEqualTo(0)); //note: it is _possible_ to have 0 for very small ranges :(

						return estimatedSize;
					}, this.Cancellation);
				}
			}
		}

	}

}
