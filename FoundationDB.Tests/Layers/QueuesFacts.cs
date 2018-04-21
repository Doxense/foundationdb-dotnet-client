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

#undef ENABLE_LOGGING

namespace FoundationDB.Layers.Collections.Tests
{
	using FoundationDB.Async;
	using FoundationDB.Client;
	using FoundationDB.Client.Tests;
	using FoundationDB.Filters.Logging;
	using NUnit.Framework;
	using System;
	using System.Collections.Generic;
	using System.Diagnostics;
	using System.Linq;
	using System.Threading;
	using System.Threading.Tasks;

	[TestFixture]
	public class QueuesFacts : FdbTest
	{
		[Test]
		public async Task Test_Queue_Fast()
		{
			// without high contention protecction

			using (var db = await OpenTestPartitionAsync())
			{
				var location = await GetCleanDirectory(db, "queue");

				var queue = new FdbQueue<int>(location, highContention: false);

				Console.WriteLine("Clear Queue");
				await db.WriteAsync((tr) => queue.Clear(tr), this.Cancellation);

				Console.WriteLine("Empty? " + await db.ReadAsync((tr) => queue.EmptyAsync(tr), this.Cancellation));

				Console.WriteLine("Push 10, 8, 6");
				await db.ReadWriteAsync((tr) => queue.PushAsync(tr, 10), this.Cancellation);
				await db.ReadWriteAsync((tr) => queue.PushAsync(tr, 8), this.Cancellation);
				await db.ReadWriteAsync((tr) => queue.PushAsync(tr, 6), this.Cancellation);

#if DEBUG
				await DumpSubspace(db, location);
#endif

				// Empty?
				bool empty = await db.ReadAsync((tr) => queue.EmptyAsync(tr), this.Cancellation);
				Console.WriteLine("Empty? " + empty);
				Assert.That(empty, Is.False);

				Optional<int> item = await queue.PopAsync(db, this.Cancellation);
				Console.WriteLine("Pop item: " + item);
				Assert.That((int)item, Is.EqualTo(10));
				item = await db.ReadWriteAsync((tr) => queue.PeekAsync(tr), this.Cancellation);
				Console.WriteLine("Next item: " + item);
				Assert.That((int)item, Is.EqualTo(8));
#if DEBUG
				await DumpSubspace(db, location);
#endif

				item = await queue.PopAsync(db, this.Cancellation);
				Console.WriteLine("Pop item: " + item);
				Assert.That((int)item, Is.EqualTo(8));
#if DEBUG
				await DumpSubspace(db, location);
#endif

				item = await queue.PopAsync(db, this.Cancellation);
				Console.WriteLine("Pop item: " + item);
				Assert.That((int)item, Is.EqualTo(6));
#if DEBUG
				await DumpSubspace(db, location);
#endif

				empty = await db.ReadAsync((tr) => queue.EmptyAsync(tr), this.Cancellation);
				Console.WriteLine("Empty? " + empty);
				Assert.That(empty, Is.True);

				Console.WriteLine("Push 5");
				await db.ReadWriteAsync((tr) => queue.PushAsync(tr, 5), this.Cancellation);
#if DEBUG
				await DumpSubspace(db, location);
#endif

				Console.WriteLine("Clear Queue");
				await db.WriteAsync((tr) => queue.Clear(tr), this.Cancellation);
#if DEBUG
				await DumpSubspace(db, location);
#endif

				empty = await db.ReadAsync((tr) => queue.EmptyAsync(tr), this.Cancellation);
				Console.WriteLine("Empty? " + empty);
				Assert.That(empty, Is.True);
			}
		}

		[Test]
		public async Task Test_Single_Client()
		{
			using (var db = await OpenTestPartitionAsync())
			{
				var location = await GetCleanDirectory(db, "queue");

				var queue = new FdbQueue<int>(location, highContention: false);

				await db.WriteAsync((tr) => queue.Clear(tr), this.Cancellation);

				for (int i = 0; i < 10; i++)
				{
					await db.ReadWriteAsync((tr) => queue.PushAsync(tr, i), this.Cancellation);
				}

				for (int i = 0; i < 10; i++)
				{
					var r = await queue.PopAsync(db, this.Cancellation);
					Assert.That(r.HasValue, Is.True);
					Assert.That(r.Value, Is.EqualTo(i));
				}

				bool empty = await db.ReadAsync((tr) => queue.EmptyAsync(tr), this.Cancellation);
				Assert.That(empty, Is.True);
			}

		}

		private static async Task RunMultiClientTest(IFdbDatabase db, KeySubspace location, bool highContention, string desc, int K, int NUM, CancellationToken ct)
		{
			Console.WriteLine("Starting {0} test with {1} threads and {2} iterations", desc, K, NUM);

			var queue = new FdbQueue<string>(location, highContention);
			await db.WriteAsync((tr) => queue.Clear(tr), ct);

			// use a CTS to ensure that everything will stop in case of problems...
			using (var go = new CancellationTokenSource(TimeSpan.FromSeconds(30)))
			{
				var tok = go.Token;

				var pushLock = new AsyncCancelableMutex(tok);
				var popLock = new AsyncCancelableMutex(tok);

				int pushCount = 0;
				int popCount = 0;
				int stalls = 0;

				var pushTreads = Enumerable.Range(0, K)
					.Select(async id =>
					{
						// wait for the signal
						await pushLock.Task.ConfigureAwait(false);

						var res = new List<string>(NUM);

						for (int i = 0; i < NUM; i++)
						{
							var item = id.ToString() + "." + i.ToString();
							await db.ReadWriteAsync((tr) => queue.PushAsync(tr, item), tok).ConfigureAwait(false);

							Interlocked.Increment(ref pushCount);
							res.Add(item);
						}

						return res;
					}).ToArray();

				var popThreads = Enumerable.Range(0, K)
					.Select(async id =>
					{
						// make everyone wait a bit, to ensure that they all start roughly at the same time
						await popLock.Task.ConfigureAwait(false);

						var res = new List<string>(NUM);

						int i = 0;
						while (i < NUM)
						{
							var item = await queue.PopAsync(db, tok).ConfigureAwait(false);
							if (item.HasValue)
							{
								Interlocked.Increment(ref popCount);
								res.Add(item.Value);
								++i;
							}
							else
							{
								Interlocked.Increment(ref stalls);
								await Task.Delay(10).ConfigureAwait(false);
							}
						}

						return res;
					}).ToArray();

				var sw = Stopwatch.StartNew();

				pushLock.Set(async: true);
				await Task.Delay(100);
				popLock.Set(async: true);

				//using (var timer = new Timer((_) =>
				//{
				//	var __ = TestHelpers.DumpSubspace(db, location);
				//}, null, 1000, 4000))
				{

					await Task.WhenAll(pushTreads);
					await Task.WhenAll(popThreads);
				}

				sw.Stop();
				Console.WriteLine("> Finished {0} test in {1} seconds", desc, sw.Elapsed.TotalSeconds);
				Console.WriteLine("> Pushed {0}, Popped {1} and Stalled {2}", pushCount, popCount, stalls);

				var pushedItems = pushTreads.SelectMany(t => t.Result).ToList();
				var poppedItems = popThreads.SelectMany(t => t.Result).ToList();

				Assert.That(pushCount, Is.EqualTo(K * NUM));
				Assert.That(popCount, Is.EqualTo(K * NUM));

				// all pushed items should have been popped (with no duplicates)
				Assert.That(poppedItems, Is.EquivalentTo(pushedItems));

				// the queue should be empty
				bool empty = await db.ReadAsync((tr) => queue.EmptyAsync(tr), ct);
				Assert.That(empty, Is.True);
			}
		}

		[Test]
		[Ignore("Comment this when running benchmarks")]
		public async Task Test_Multi_Client_Simple()
		{
			int NUM = 100;

			using (var db = await OpenTestPartitionAsync())
			{
				var location = await GetCleanDirectory(db, "queue");

				await RunMultiClientTest(db, location, false, "simple queue", 1, NUM, this.Cancellation);
				await RunMultiClientTest(db, location, false, "simple queue", 2, NUM, this.Cancellation);
				await RunMultiClientTest(db, location, false, "simple queue", 4, NUM, this.Cancellation);
				await RunMultiClientTest(db, location, false, "simple queue", 10, NUM, this.Cancellation);
			}
		}

		[Test]
		[Ignore("Comment this when running benchmarks")]
		public async Task Test_Multi_Client_HighContention()
		{
			int NUM = 100;

			using (var db = await OpenTestPartitionAsync())
			{
				var location = await GetCleanDirectory(db, "queue");

				await RunMultiClientTest(db, location, true, "high contention queue", 1, NUM, this.Cancellation);
				await RunMultiClientTest(db, location, true, "high contention queue", 2, NUM, this.Cancellation);
				await RunMultiClientTest(db, location, true, "high contention queue", 4, NUM, this.Cancellation);
				await RunMultiClientTest(db, location, true, "high contention queue", 10, NUM, this.Cancellation);
			}
		}

		[Test]
		public async Task Test_Log_Queue()
		{
			int NUM = 100;

			using (var db = await OpenTestPartitionAsync())
			{
				var location = await GetCleanDirectory(db, "queue");

#if ENABLE_LOGGING
				var list = new List<FdbTransactionLog>(NUM);
				var logged = new FdbLoggedDatabase(db, false, false, (tr) => { lock (list) { list.Add(tr.Log); } });
#else
				var logged = db;
#endif

				await RunMultiClientTest(logged, location, false, "simple queue", 4, NUM, this.Cancellation);
#if ENABLE_LOGGING
				foreach (var log in list)
				{
					Console.WriteLine(log.GetTimingsReport(true));
				}
				list.Clear();
#endif

				Console.WriteLine("------------------------------------------------");

				await RunMultiClientTest(logged, location, true, "high contention queue", 4, NUM, this.Cancellation);
#if ENABLE_LOGGING
				foreach (var log in list)
				{
					Console.WriteLine(log.GetTimingsReport(true));
				}
				list.Clear();
#endif

				Console.WriteLine("------------------------------------------------");

			}

		}

	}

}
