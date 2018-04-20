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

namespace FoundationDB.Layers.Counters.Tests
{
	using FoundationDB.Client.Tests;
	using NUnit.Framework;
	using System;
	using System.Diagnostics;
	using System.Linq;
	using System.Threading;
	using System.Threading.Tasks;

	[TestFixture]
	[Obsolete]
	public class CounterFacts : FdbTest
	{

		[Test]
		public async Task Test_FdbCounter_Can_Increment_And_SetTotal()
		{
			using (var db = await OpenTestPartitionAsync())
			{
				var location = await GetCleanDirectory(db, "counters", "simple");

				var counter = new FdbHighContentionCounter(db, location);

				await counter.AddAsync(100, this.Cancellation);
				Assert.That(await counter.GetSnapshotAsync(this.Cancellation), Is.EqualTo(100));

				await counter.AddAsync(-10, this.Cancellation);
				Assert.That(await counter.GetSnapshotAsync(this.Cancellation), Is.EqualTo(90));

				await counter.SetTotalAsync(500, this.Cancellation);
				Assert.That(await counter.GetSnapshotAsync(this.Cancellation), Is.EqualTo(500));
			}
		}

		[Test]
		public async Task Bench_FdbCounter_Increment_Sequentially()
		{
			const int N = 100;

			using (var db = await OpenTestPartitionAsync())
			{
				var location = await GetCleanDirectory(db, "counters", "big");

				var c = new FdbHighContentionCounter(db, location);

				Console.WriteLine("Doing " + N + " inserts in one thread...");

				var sw = Stopwatch.StartNew();
				for (int i = 0; i < N; i++)
				{
					await c.AddAsync(1, this.Cancellation);
				}
				sw.Stop();

				Console.WriteLine("> " + N + " completed in " + sw.Elapsed.TotalMilliseconds.ToString("N1") + " ms (" + (sw.Elapsed.TotalMilliseconds * 1000 / N).ToString("N0") + " µs/add)");

#if DEBUG
				await DumpSubspace(db, location);
#endif

				Assert.That(await c.GetSnapshotAsync(this.Cancellation), Is.EqualTo(N));
			}

		}

		[Test]
		public async Task Bench_FdbCounter_Increment_Concurrently()
		{
			const int B = 100;

			// repeat the process 10 times...
			foreach(int W in new [] { 1, 2, 5, 10, 20, 50, 100 })
			{
				int N = B * W;

				using (var db = await OpenTestPartitionAsync())
				{
					var location = await GetCleanDirectory(db, "counters", "big", W.ToString());

					var c = new FdbHighContentionCounter(db, location);

					Console.WriteLine("Doing " + W + " x " + B + " inserts in " + W + " threads...");

					var signal = new TaskCompletionSource<object>();
					var done = new TaskCompletionSource<object>();
					var workers = Enumerable.Range(0, W)
						.Select(async (id) =>
						{
							await signal.Task.ConfigureAwait(false);
							for (int i = 0; i < B; i++)
							{
								await c.AddAsync(1, this.Cancellation).ConfigureAwait(false);
							}
						}).ToArray();

					var sw = Stopwatch.StartNew();
					// start
					ThreadPool.UnsafeQueueUserWorkItem((_) => signal.TrySetResult(null), null);
					// wait
					await Task.WhenAll(workers);
					sw.Stop();
					Console.WriteLine("> " + N + " completed in " + sw.Elapsed.TotalMilliseconds.ToString("N1") + " ms (" + (sw.Elapsed.TotalMilliseconds * 1000 / B).ToString("N0") + " µs/add)");

					long n = await c.GetSnapshotAsync(this.Cancellation);
					if (n != N)
					{ // fail
						await DumpSubspace(db, location);
						Assert.That(n, Is.EqualTo(N), "Counter value does not match (first call)");
					}

					// wait a bit, in case there was some coalesce still running...
					await Task.Delay(200);
					n = await c.GetSnapshotAsync(this.Cancellation);
					if (n != N)
					{ // fail
						await DumpSubspace(db, location);
						Assert.That(n, Is.EqualTo(N), "Counter value does not match (second call)");
					}

				}
			}

		}


	}

}
