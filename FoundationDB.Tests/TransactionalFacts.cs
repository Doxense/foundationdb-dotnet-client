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
	using System.Threading;
	using System.Threading.Tasks;

	[TestFixture]
	public class TransactionalFacts : FdbTest
	{
		[Test]
		public async Task Test_ReadAsync_Should_Normally_Execute_Only_Once()
		{
			using (var db = await OpenTestPartitionAsync())
			{
				var location = await db.Directory.CreateOrOpenAsync(new[] { "Transactionals" }, this.Cancellation);

				string secret = Guid.NewGuid().ToString();

				using(var tr = db.BeginTransaction(this.Cancellation))
				{
					tr.Set(location.Keys.Encode("Hello"), Slice.FromString(secret));
					await tr.CommitAsync();
				}

				int called = 0;
				Slice result = await db.ReadAsync<Slice>((tr) =>
				{
					++called;
					Assert.That(tr, Is.Not.Null);
					Assert.That(tr.Context, Is.Not.Null);
					Assert.That(tr.Context.Database, Is.SameAs(db));
					Assert.That(tr.Context.Shared, Is.True);

					return tr.GetAsync(location.Keys.Encode("Hello"));
				}, this.Cancellation);

				Assert.That(called, Is.EqualTo(1)); // note: if this assert fails, first ensure that you did not get a transient error while running this test!
				Assert.That(result.ToUnicode(), Is.EqualTo(secret));
			}
		}

		[Test]
		public async Task Test_Transactionals_Rethrow_Regular_Exceptions()
		{
			using (var db = await OpenTestPartitionAsync())
			{
				int called = 0;

				// ReadAsync should return a failed Task, and not bubble up the exception.
				var task = db.ReadAsync((tr) =>
				{
					Assert.That(called, Is.EqualTo(0), "ReadAsync should not retry on regular exceptions");
					++called;
					throw new InvalidOperationException("Boom");
				}, this.Cancellation);
				Assert.That(task, Is.Not.Null);
				// the exception should be unwrapped (ie: we should not see an AggregateException, but the actual exception)
				Assert.Throws<InvalidOperationException>(async () => await task, "ReadAsync should rethrow any regular exceptions");
			}

		}

		[Test]
		public async Task Test_Transactionals_Retries_On_Transient_Errors()
		{
			using (var db = await OpenTestPartitionAsync())
			{
				var location = await GetCleanDirectory(db, "Transactionals");

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
		public async Task Test_Transactionals_Retries_Do_Not_Leak_When_Reading_Too_Much()
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

				var location = await GetCleanDirectory(db, "Transactionals");

				// insert a good amount of test data

				var sw = Stopwatch.StartNew();
				Console.WriteLine("Inserting test data (this may take a few minutes)...");
				var rnd = new Random();
				await Fdb.Bulk.WriteAsync(db, Enumerable.Range(0, 100 * 1000).Select(i => new KeyValuePair<Slice, Slice>(location.Keys.Encode(i), Slice.Random(rnd, 4096))), this.Cancellation);
				sw.Stop();
				Console.WriteLine("> done in " + sw.Elapsed);

				using (var timer = new System.Threading.Timer((_) => { Console.WriteLine("WorkingSet: {0:N0}, Managed: {1:N0}", Environment.WorkingSet, GC.GetTotalMemory(false)); }, null, 1000, 1000))
				{
					try
					{
						var result = await db.ReadAsync((tr) =>
						{
							Console.WriteLine("Retry #" + tr.Context.Retries + " @ " + tr.Context.ElapsedTotal);
							return tr.GetRange(location.Keys.ToRange()).ToListAsync();
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
				Console.WriteLine("Transaction destroyed!");
				Thread.Sleep(5000);

				Console.WriteLine("Cleaning managed memory");
				GC.Collect();
				GC.WaitForPendingFinalizers();
				GC.Collect();

				Console.WriteLine("Waiting...");
				Thread.Sleep(5000);

				Console.WriteLine("byte");
			}
		}

		[Test]
		public async Task Test_Transactionals_Should_Not_Execute_If_Already_Canceled()
		{
			using (var db = await OpenTestPartitionAsync())
			{
				using (var go = new CancellationTokenSource())
				{
					go.Cancel();

					bool called = false;

					// ReadAsync should return a canceled Task, and never call the handler
					var t = db.ReadAsync((tr) =>
					{
						called = true;
						Console.WriteLine("FAILED");
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
		public async Task Test_Transactionals_ReadOnly_Should_Deny_Write_Attempts()
		{
			using (var db = await OpenTestPartitionAsync())
			{
				var location = await GetCleanDirectory(db, "Transactionals");

				var t = db.ReadAsync((IFdbReadOnlyTransaction tr) =>
				{
					Assert.That(tr, Is.Not.Null);

					// force the read-only into a writable interface
					var hijack = tr as IFdbTransaction;
					Assume.That(hijack, Is.Not.Null, "This test requires the transaction to implement IFdbTransaction !");

					// this call should fail !
					hijack.Set(location.Keys.Encode("Hello"), Slice.FromString("Hijacked"));

					Assert.Fail("Calling Set() on a read-only transaction should fail");
					return Task.FromResult(123);
				}, this.Cancellation);

				Assert.Throws<InvalidOperationException>(async () => await t, "Forcing writes on a read-only transaction should fail");
			}

		}

		[Test]
		public async Task Test_ReadOnly_Transactionals_Do_Not_See_Changes_From_Other_Transactions()
		{
			// A read-only transaction will never see changes that have been committed after its read-version, wheter it uses Snapshot reads or not.

			using (var db = await OpenTestPartitionAsync())
			{
				var location = await GetCleanDirectory(db, "Transactionals");

				// setup the keys from 0 to 9
				await db.WriteAsync((tr) =>
				{
					for (int i = 0; i < 10; i++)
					{
						tr.Set(location.Keys.Encode(i), Slice.FromInt32(i));
					}
				}, this.Cancellation);


				// simulate a slow scan that will sequentially read the values, while they are modified by other transactions in the background

				var results = await db.ReadAsync(async (tr) =>
				{
					int[] values = new int[10];

					// read 0..2
					for (int i = 0; i < 3; i++)
					{
						values[i] = (await tr.GetAsync(location.Keys.Encode(i))).ToInt32();
					}

					// another transaction commits a change to 3 before we read it
					await db.WriteAsync((tr2) => tr2.Set(location.Keys.Encode(3), Slice.FromInt32(42)), this.Cancellation);

					// read 3 to 7
					for (int i = 3; i < 7; i++)
					{
						values[i] = (await tr.GetAsync(location.Keys.Encode(i))).ToInt32();
					}

					// another transaction commits a change to 6 after it has been read
					await db.WriteAsync((tr2) => tr2.Set(location.Keys.Encode(6), Slice.FromInt32(66)), this.Cancellation);

					// read 7 to 9
					for (int i = 7; i < 10; i++)
					{
						values[i] = (await tr.GetAsync(location.Keys.Encode(i))).ToInt32();
					}

					return values;
				}, this.Cancellation);

				// The values of 3 and 6 should be the initial values
				Assert.That(results, Is.EqualTo(Enumerable.Range(0, 10).ToList()));
			}
		}

	}
}
