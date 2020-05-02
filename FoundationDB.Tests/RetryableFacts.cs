﻿#region BSD License
/* Copyright (c) 2013-2020, Doxense SAS
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
	using System;
	using System.Diagnostics;
	using System.Linq;
	using System.Threading;
	using System.Threading.Tasks;
	using NUnit.Framework;

	[TestFixture]
	public class RetryableFacts : FdbTest
	{
		[Test]
		public async Task Test_ReadAsync_Should_Normally_Execute_Only_Once()
		{
			using (var db = await OpenTestPartitionAsync())
			{
				var location = db.Root["Retryable"];
				await CleanLocation(db, location);

				string secret = Guid.NewGuid().ToString();

				await db.WriteAsync(async tr =>
				{
					var subspace = await location.Resolve(tr);
					tr.Set(subspace.Encode("Hello"), Value(secret));
				}, this.Cancellation);

				int called = 0;
				var result = await db.ReadAsync<Slice>(async (tr) =>
				{
					++called;
					Assert.That(tr, Is.Not.Null);
					Assert.That(tr.Context, Is.Not.Null);
					Assert.That(tr.Context.Database, Is.SameAs(db));
					Assert.That(tr.Context.Shared, Is.True);

					var subspace = await location.Resolve(tr);
					return await tr.GetAsync(subspace.Encode("Hello"));
				}, this.Cancellation);

				Assert.That(called, Is.EqualTo(1)); // note: if this assert fails, first ensure that you did not get a transient error while running this test!
				Assert.That(result.ToUnicode(), Is.EqualTo(secret));
			}
		}

		[Test]
		public async Task Test_Retryable_Rethrows_Regular_Exceptions()
		{
			using (var db = await OpenTestPartitionAsync())
			{
				int called = 0;

				// ReadAsync should return a failed Task, and not bubble up the exception.
				var task = db.ReadAsync<int>((tr) =>
				{
					Assert.That(called, Is.Zero, "ReadAsync should not retry on regular exceptions");
					++called;
					throw new InvalidOperationException("Boom");
				}, this.Cancellation);
				Assert.That(task, Is.Not.Null);
				// the exception should be unwrapped (ie: we should not see an AggregateException, but the actual exception)
				Assert.That(async () => await task, Throws.InstanceOf<InvalidOperationException>(), "ReadAsync should rethrow any regular exceptions");
			}

		}

		[Test]
		public async Task Test_Retryable_Retries_On_Transient_Errors()
		{
			using (var db = await OpenTestPartitionAsync())
			{
				int called = 0;
				int? id = null;

				using (var go = new CancellationTokenSource())
				{
					// ReadAsync should return a failed Task, and not bubble up the exception.
					var res = await db.ReadAsync((tr) =>
					{
						try
						{
							++called;

							Assert.That(tr, Is.Not.Null);
							Assert.That(tr.Context, Is.Not.Null, "tr.Context should not be null");
							Assert.That(tr.Context.Retries, Is.EqualTo(called - 1), "tr.Context.Retries should equal the number of calls to the handler, minus one");

							if (id == null) id = tr.Id;
							Assert.That(tr.Id, Is.EqualTo(id.Value), "The same transaction should be passed multiple times");

							if (called < 3)
							{ // fool the retry loop into thinking that a retryable error occurred
								throw new FdbException(FdbError.PastVersion, "Fake error");
							}
							Assert.That(called, Is.GreaterThanOrEqualTo(3), "The handler should not be called again if it completed previously");
							return Task.FromResult(123);
						}
						catch(AssertionException)
						{ // protection against infinite loops
							go.Cancel();
							throw;
						}
					}, go.Token);

					Assert.That(res, Is.EqualTo(123));
					Assert.That(called, Is.EqualTo(3));
				}
			}

		}

		[Test][Category("LongRunning")]
		[Ignore("This tests a bug in an old version (v2.0.7) and takes a long time to run!")]
		public async Task Test_Retryable_Retries_Do_Not_Leak_When_Reading_Too_Much()
		{
			// we have a transaction that tries to read too much data, and will always take more than 5 seconds to execute
			// => in versions of fdb_c.dll up to 2.0.7, this leaks memory because the internal cache is not cleared after each reset.
			// => this is fixed in 2.0.8 and up

			using (var db = await OpenTestPartitionAsync())
			{
				// this is a safety to ensure that you do not kill your server
				db.DefaultTimeout = 0;
				db.DefaultRetryLimit = 10;
				// => with 10 retries, this test may consume about 5 GB of ram is there is a leak.

				var location = db.Root["Retryable"];
				await CleanLocation(db, location);

				// insert a good amount of test data

				var sw = Stopwatch.StartNew();
				Log("Inserting test data (this may take a few minutes)...");
				var rnd = new Random();
				for(int i = 0; i < 100; i++)
				{
					await db.WriteAsync(async tr =>
					{
						var subspace = await location.Resolve(tr);
						for (int j = 0; j < 1000; j++)
						{
							tr.Set(subspace.Encode(i * 1000 + j), Slice.Random(rnd, 4096));
						}
					}, this.Cancellation);
				}
				sw.Stop();
				Log("> done in " + sw.Elapsed);

				using (var timer = new System.Threading.Timer((_) => { Log($"WorkingSet: {Environment.WorkingSet:N0}, Managed: {GC.GetTotalMemory(false):N0}"); }, null, 1000, 1000))
				{
					try
					{
						var result = await db.ReadAsync(async (tr) =>
						{
							Log("Retry #" + tr.Context.Retries + " @ " + tr.Context.ElapsedTotal);
							var subspace = await location.Resolve(tr);
							return tr.GetRange(subspace.ToRange()).ToListAsync();
						}, this.Cancellation);

						Assert.Fail("Too fast! increase the amount of inserted data, or slow down the system!");
					}
					catch (FdbException e)
					{
						// max retry limit should throw a past_version
						if (e.Code != FdbError.PastVersion)
						{ // unexpected
							throw;
						}
					}
				}
				// to help see the effect in a profiler, dispose the transaction first, wait 5 sec then do a full GC, and then wait a bit before exiting the process
				Log("Transaction destroyed!");
				Thread.Sleep(5000);

				Log("Cleaning managed memory");
				GC.Collect();
				GC.WaitForPendingFinalizers();
				GC.Collect();

				Log("Waiting...");
				Thread.Sleep(5000);

				Log("byte");
			}
		}

		[Test]
		public async Task Test_Retryable_Should_Not_Execute_If_Already_Canceled()
		{
			using (var db = await OpenTestPartitionAsync())
			{
				using (var go = new CancellationTokenSource())
				{
					go.Cancel();

					bool called = false;

					// ReadAsync should return a canceled Task, and never call the handler
					var t = db.ReadAsync<int>((tr) =>
					{
						called = true;
						Log("FAILED");
						throw new InvalidOperationException("Failed");
					}, go.Token);

					Assert.That(t.IsCompleted, "Returned task should already be canceled");
					Assert.That(t.Status, Is.EqualTo(TaskStatus.Canceled), "Returned task should be in the canceled state");
					Assert.That(called, Is.False, "Handler should not be called with an already canceled token");
					var _ = t.Exception;
				}
			}

		}

		[Test]
		public async Task Test_Retryable_ReadOnly_Should_Deny_Write_Attempts()
		{
			using (var db = await OpenTestPartitionAsync())
			{
				var location = db.Root["Retryable"];
				await CleanLocation(db, location);

				var t = db.ReadAsync(async (tr) =>
				{
					Assert.That(tr, Is.Not.Null);

					// force the read-only into a writable interface
					var hijack = tr as IFdbTransaction;
					Assume.That(hijack, Is.Not.Null, "This test requires the transaction to implement IFdbTransaction !");

					var subspace = await location.Resolve(tr);

					// this call should fail !
					hijack.Set(subspace.Encode("Hello"), Value("Hijacked"));

					Assert.Fail("Calling Set() on a read-only transaction should fail");
					return Task.FromResult(123);
				}, this.Cancellation);

				Assert.That(async () => await t, Throws.InstanceOf<InvalidOperationException>(), "Forcing writes on a read-only transaction should fail");
			}

		}

		[Test]
		public async Task Test_ReadOnly_Retryable_Do_Not_See_Changes_From_Other_Transactions()
		{
			// A read-only transaction will never see changes that have been committed after its read-version, whether it uses Snapshot reads or not.

			using (var db = await OpenTestPartitionAsync())
			{
				var location = db.Root["Retryable"];
				await CleanLocation(db, location);

				// setup the keys from 0 to 9
				await db.WriteAsync(async (tr) =>
				{
					var subspace = await location.Resolve(tr);
					for (int i = 0; i < 10; i++)
					{
						tr.Set(subspace.Encode(i), Slice.FromInt32(i));
					}
				}, this.Cancellation);


				// simulate a slow scan that will sequentially read the values, while they are modified by other transactions in the background

				var results = await db.ReadAsync(async (tr) =>
				{
					var subspace = await location.Resolve(tr);

					int[] values = new int[10];

					// read 0..2
					for (int i = 0; i < 3; i++)
					{
						values[i] = (await tr.GetAsync(subspace.Encode(i))).ToInt32();
					}

					// another transaction commits a change to 3 before we read it
					await db.WriteAsync((tr2) => tr2.Set(subspace.Encode(3), Slice.FromInt32(42)), this.Cancellation);

					// read 3 to 7
					for (int i = 3; i < 7; i++)
					{
						values[i] = (await tr.GetAsync(subspace.Encode(i))).ToInt32();
					}

					// another transaction commits a change to 6 after it has been read
					await db.WriteAsync((tr2) => tr2.Set(subspace.Encode(6), Slice.FromInt32(66)), this.Cancellation);

					// read 7 to 9
					for (int i = 7; i < 10; i++)
					{
						values[i] = (await tr.GetAsync(subspace.Encode(i))).ToInt32();
					}

					return values;
				}, this.Cancellation);

				// The values of 3 and 6 should be the initial values
				Assert.That(results, Is.EqualTo(Enumerable.Range(0, 10).ToList()));
			}
		}

		[Test]
		public async Task Test_Retryable_Execute_Success_Handler_Only_Once()
		{
			using (var db = await OpenTestPartitionAsync())
			{
				var location = db.Root["Retryable"];
				await CleanLocation(db, location);

				string secret = Guid.NewGuid().ToString();

				await db.WriteAsync(async tr =>
				{
					var subspace = await location.Resolve(tr);
					tr.Set(subspace.Encode("Hello"), Value(secret));
				}, this.Cancellation);

				int called = 0;
				var result = await db.ReadWriteAsync(
					async (tr) =>
					{
						if (tr.Context.Retries == 0) throw new FdbException(FdbError.NotCommitted, "Fake Not Committed!");
						var subspace = await location.Resolve(tr);
						tr.Set(subspace.Encode("World"), Slice.Empty);
						return await tr.GetAsync(subspace.Encode("Hello"));
					},
					(tr, res) =>
					{
						called++;
						Assert.That(tr.Context.Retries == 1, "Transaction should only have retried once!");
						Assert.That(res.ToUnicode(), Is.EqualTo(secret), "Argument passed to success callback does not match expected value.");
					},
					this.Cancellation);

				Assert.That(called, Is.EqualTo(1), "Success callback should only have been called once");
				Assert.That(result.ToUnicode(), Is.EqualTo(secret), "Result does not match expected value.");
			}
		}

		[Test]
		public async Task Test_Retryable_Never_Execute_Success_Handler_If_Failed()
		{
			using (var db = await OpenTestPartitionAsync())
			{
				var location = db.Root["Retryable"];
				await CleanLocation(db, location);

				string secret = Guid.NewGuid().ToString();

				await db.WriteAsync(async tr =>
				{
					var subspace = await location.Resolve(tr);
					tr.Set(subspace.Encode("Hello"), Value(secret));
				}, this.Cancellation);

				int called = 0;
				Assert.That(
					async () => await db.ReadWriteAsync<Slice>(
						(tr) => throw new InvalidOperationException("KAPOW!"),
						(tr, res) =>
						{
							called++;
							Assert.Fail("Success callback should never have been called!");
						},
						this.Cancellation),
					Throws.InstanceOf<InvalidOperationException>().With.Message.EqualTo("KAPOW!"),
					"Success callback should only have been called once"
				);

				Assert.That(called, Is.Zero, "Success callback should never have been called!");
			}
		}

		[Test]
		public async Task Test_Retryable_Mutating_Transaction_In_Success_Handler_Should_Fail()
		{
			// Verify that attempting to keep using the transaction instance in the success handler will throw and InvalidOperationException

			using (var db = await OpenTestPartitionAsync())
			{
				var location = db.Root["Retryable"];
				await CleanLocation(db, location);

				// Cannot set a key after commit
				//note: we assume that nobody will move the directories during the test execution!
				Slice key = await db.ReadAsync(async tr => (await location.Resolve(tr)).Encode("hello"), this.Cancellation);
				Log("Using key: " + key);

				Assert.That(
					async () => await db.WriteAsync(
						(tr) => tr.Set(key, Value("Set")),
						(tr) => tr.Set(key, Slice.Empty),
						this.Cancellation),
					Throws.InstanceOf<InvalidOperationException>().With.Message.EqualTo("The transaction has already been committed"),
					"Trying to write in success handler should fail"
				);
				//note: since the transaction is already committed, we should observe its result
				Assert.That(await db.ReadAsync(tr => tr.GetAsync(key), this.Cancellation), Is.EqualTo(Slice.FromString("Set")));

				// Cannot double-commit!
				Assert.That(
					async () => await db.WriteAsync(
						(tr) => tr.Set(key, Value("Commit")),
						(tr) => tr.CommitAsync(),
						this.Cancellation),
					Throws.InstanceOf<InvalidOperationException>().With.Message.EqualTo("The transaction has already been committed"),
					"Trying to double-Commit in success handler should fail"
				);
				//note: since the transaction is already committed, we should observe its result
				Assert.That(await db.ReadAsync(tr => tr.GetAsync(key), this.Cancellation), Is.EqualTo(Slice.FromString("Commit")));

				// Cannot read a key after commit
				Assert.That(
					async () => await db.ReadWriteAsync( //TODO: replace with a simpler variant!
						(tr) =>
						{
							tr.Set(key, Value("Get"));
							return Task.CompletedTask;
						},
						async (tr) => await tr.GetAsync(key),
						this.Cancellation),
					Throws.InstanceOf<InvalidOperationException>().With.Message.EqualTo("The transaction has already been committed"),
					"Trying to read a key in success handler should fail"
				);
				//note: since the transaction is already committed, we should observe its result
				Assert.That(await db.ReadAsync(tr => tr.GetAsync(key), this.Cancellation), Is.EqualTo(Slice.FromString("Get")));

				// GetCommitVersion() is allowed to be executed AFTER the commit!
				var cv = await db.ReadWriteAsync( //TODO: replace with a simpler variant!
					(tr) =>
					{
						tr.Set(key, Value("GetCommitVersion"));
						return Task.CompletedTask;
					},
					(tr) => tr.GetCommittedVersion(),
					this.Cancellation
				);
				Assert.That(cv, Is.GreaterThan(0));
				Assert.That(
					await db.ReadAsync(tr => tr.GetAsync(key), this.Cancellation),
					Is.EqualTo(Slice.FromString("GetCommitVersion"))
				);

				// GetReadVersionAsync() is allowed to be executed AFTER the commit!
				var rv = await db.ReadWriteAsync( //TODO: replace with a simpler variant!
					(tr) =>
					{
						tr.Set(key, Value("GetReadVersion"));
						return Task.CompletedTask;
					},
					(tr) => tr.GetReadVersionAsync(),
					this.Cancellation
				);
				Assert.That(rv, Is.GreaterThan(0));
				Assert.That(
					await db.ReadAsync(tr => tr.GetAsync(key), this.Cancellation),
					Is.EqualTo(Slice.FromString("GetReadVersion"))
				);
			}
		}

		[Test]
		public async Task Test_Retryable_GetVersionStamp_Pattern()
		{
			using (var db = await OpenTestDatabaseAsync())
			{
				var location = db.Root["Retryable"];
				await CleanLocation(db, location);

				// Cannot get version stamp after commit
				Assert.That(
					async () => await db.ReadWriteAsync(
					async (tr) =>
					{
						var subspace = await location.Resolve(tr);
						var key = subspace.Encode("Hello");
						tr.Set(key, Value("GetVersionStamp"));
						tr.SetVersionStampedKey(key + tr.CreateVersionStamp().ToSlice(), Slice.Empty);
					},
					(tr) => tr.GetVersionStampAsync(),
					this.Cancellation),
					Throws.InstanceOf<InvalidOperationException>().With.Message.EqualTo("The transaction has already been committed"),
					"Trying to read a key in success handler should fail"
				);
				//note: since the transaction is already committed, we should observe its result
				Assert.That(await db.ReadAsync(async tr =>
				{
					var subspace = await location.Resolve(tr);
					return await tr.GetAsync(subspace.Encode("Hello"));
				}, this.Cancellation), Is.EqualTo(Slice.FromString("GetVersionStamp")));

				// but getting stamp before commit and awaiting it after should work
				VersionStamp st = await db.ReadWriteAsync(
					async (tr) =>
					{
						var subspace = await location.Resolve(tr);
						var key = subspace.Encode("Hello");
						var prev = await tr.GetAsync(key);
						tr.Set(key, Value("GetVersionStamp2"));
						tr.SetVersionStampedKey(key + VersionStamp.Incomplete().ToSlice(), key.Count, prev);
						return new { Stamp = tr.GetVersionStampAsync() }; //REVIEW: "return tr.GetVersionStampAsync()" will deadlock because 'ReadWrite' will try to await it!
					},
					(tr, res) => res.Stamp,
					this.Cancellation
				);
				Assert.That(st.IsIncomplete, Is.False, "Stamp should be completed");
				Assert.That(st.TransactionVersion, Is.Not.Zero.And.Not.EqualTo(ulong.MaxValue), "Stamp should be completed");
				Assert.That(await db.ReadAsync(async tr =>
				{
					var subspace = await location.Resolve(tr);
					return await tr.GetAsync(subspace.Encode("Hello"));
				}, this.Cancellation), Is.EqualTo(Slice.FromString("GetVersionStamp2")));
			}
		}

	}
}
