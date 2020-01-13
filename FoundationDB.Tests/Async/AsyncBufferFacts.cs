#region BSD License
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

namespace Doxense.Async.Tests
{
	using System;
	using System.Collections.Generic;
	using System.Diagnostics;
	using System.Linq;
	using System.Runtime.ExceptionServices;
	using System.Threading;
	using System.Threading.Tasks;
	using FoundationDB.Client.Tests;
	using NUnit.Framework;

	[TestFixture]
	public class AsyncBufferFacts : FdbTest
	{

		[Test]
		public async Task Test_AsyncTaskBuffer_In_Arrival_Order()
		{
			// Test that we can queue N async tasks in a buffer that will only accept K tasks at a time,
			// and pump them into a list that will received in arrival order

			const int ITER = 10;
			const int N = 20;
			const int K = 5;

			for (int r = 0; r < ITER; r++)
			{
				// since this can lock up, we need a global timeout !
				using (var go = new CancellationTokenSource(TimeSpan.FromSeconds(10)))
				{
					var token = go.Token;
					token.Register(() => Log("### TIMEOUT EXPIRED!"));

					var list = new List<int>();
					bool didComplete = false;
					Exception error = null;
					var target = AsyncHelpers.CreateTarget<int>(
						(x, ct) =>
						{
							Log("[target#" + Thread.CurrentThread.ManagedThreadId + "] received " + x);
							list.Add(x);
						},
						() =>
						{
							didComplete = true;
							Log("[target#" + Thread.CurrentThread.ManagedThreadId + "] completed");
						},
						(e) =>
						{
							error = e.SourceException;
							Log("[target#" + Thread.CurrentThread.ManagedThreadId + "] error " + e.SourceException.ToString());
						}
					);

					var buffer = AsyncHelpers.CreateOrderPreservingAsyncBuffer<int>(K);

					Log("### Starting pumping");
					var pumpTask = buffer.PumpToAsync(target, token);

					var rnd = new Random(0x1337);
					for (int i = 0; i < N; i++)
					{
						int x = i;
						var task = Task.Run(
							async () =>
							{
								await Task.Delay(10 + rnd.Next(50), token);
								Log("[source#" + Thread.CurrentThread.ManagedThreadId + "] produced " + x);
								return x;
							},
							token);

						await buffer.OnNextAsync(task, token);
					}
					// signal the end
					buffer.OnCompleted();

					Log("### Finished producing");

					await Task.WhenAny(pumpTask, Task.Delay(15 * 1000, go.Token));
					if (!pumpTask.IsCompleted)
					{
						Log("FAILED: HARD TIMEOUT! PumpTask did not complete in time :(");
						Assert.Fail("The PumpTask did not complete in time");
					}
					Assert.That(async () => await pumpTask, Throws.Nothing, "PumpTask failed");

					Log("### Finished pumping");

					Log("Result: " + String.Join(", ", list));
					Assert.That(didComplete, Is.True);
					Assert.That(error, Is.Null);
					Assert.That(list, Is.EqualTo(Enumerable.Range(0, N).ToArray()));
				}
			}

		}

		[Test]
		public async Task Test_AsyncTaskBuffer_In_Completion_Order()
		{
			// Test that we can queue N async tasks in a buffer that will only accept K tasks at a time,
			// and pump them into a list that will be received in completion order

			const int ITER = 10;
			const int N = 20;
			const int K = 5;

			for (int r = 0; r < ITER; r++)
			{
				Log();
				Log($"######### RUN {r}");
				Log();

				int completeCount = 0;
				string[] stacks = new string[16];

				// since this can lock up, we need a global timeout !
				using (var go = new CancellationTokenSource(TimeSpan.FromSeconds(10)))
				{
					var token = go.Token;
					token.Register(() => Log("### TIMEOUT EXPIRED!"));

					var list = new List<int>();
					bool didComplete = false;
					ExceptionDispatchInfo error = null;
					var clock = Stopwatch.StartNew();

					var target = AsyncHelpers.CreateTarget<int>(
						onNext: (x, ct) =>
						{
							Log($"[target#{Thread.CurrentThread.ManagedThreadId,-2} @ {clock.Elapsed.TotalMilliseconds:N3}] onNext {x}");
							list.Add(x);
						},
						onCompleted: () =>
						{
							int n = Interlocked.Increment(ref completeCount);
							stacks[n - 1] = Environment.StackTrace;
							if (n > 1)
							{
								Log("*** OnComplete() CALLED MULTIPLE TIMES!! :(");
								if (System.Diagnostics.Debugger.IsAttached) System.Diagnostics.Debugger.Break();
							}
							didComplete = true;
							Log($"[target#{Thread.CurrentThread.ManagedThreadId,-2} @ {clock.Elapsed.TotalMilliseconds:N3}] onCompleted");
						},
						onError: (e) =>
						{
							error = e;
							Log($"[target#{Thread.CurrentThread.ManagedThreadId,-2} @ {clock.Elapsed.TotalMilliseconds:N3}] onError {e.SourceException}");
						}
					);

					var buffer = AsyncHelpers.CreateUnorderedAsyncBuffer<int>(K);

					Log("### Starting pumping");
					var pumpTask = buffer.PumpToAsync(target, token);

					var rnd = new Random(0x1337);
					for (int i = 0; i < N; i++)
					{
						int x = i;
						var task = Task.Run(
							async () =>
							{
								Log($"[source#{Thread.CurrentThread.ManagedThreadId,-2} @ {clock.Elapsed.TotalMilliseconds:N3}] thinking {x} on task {Task.CurrentId}");
								// simulate random workload
								await Task.Delay(10 + rnd.Next(50), token);
								Log($"[source#{Thread.CurrentThread.ManagedThreadId,-2} @ {clock.Elapsed.TotalMilliseconds:N3}] produced {x} on task {Task.CurrentId}");
								return x;
							},
							token);

						//Log($"[parent#{Thread.CurrentThread.ManagedThreadId,-2} @ {clock.Elapsed.TotalMilliseconds:N3}] calling OnNextAsync({task.Id}) for {x}...");
						var t = buffer.OnNextAsync(task, token);
						//Log($"[parent#{Thread.CurrentThread.ManagedThreadId,-2} @ {clock.Elapsed.TotalMilliseconds:N3}] called OnNextAsync({task.Id}) for {x} : {t.Status}");
						await t;
						//Log($"[parent#{Thread.CurrentThread.ManagedThreadId,-2} @ {clock.Elapsed.TotalMilliseconds:N3}] awaited OnNextAsync({task.Id}) for {x}");
					}
					// signal the end
					buffer.OnCompleted();

					Log("### Finished producing!");

					await Task.WhenAny(pumpTask, Task.Delay(15 * 1000, go.Token));
					if (!pumpTask.IsCompleted)
					{
						Log("FAILED: HARD TIMEOUT! PumpTask did not complete in time :(");
						Log($"DidComplete: {didComplete}");
						Log($"Error: {error?.SourceException}");
						Assert.Fail("The PumpTask did not complete in time");
					}
					Assert.That(async () => await pumpTask, Throws.Nothing, "PumpTask failed");

					Log("### Finished pumping");

					Log($"Result: {String.Join(", ", list)}");
					Assert.That(didComplete, Is.True);
					Assert.That(error, Is.Null);
					//note: order doesn't matter, but all should be there
					Assert.That(list, Is.EquivalentTo(Enumerable.Range(0, N).ToArray()));
				}
			}

		}

		[Test]
		public async Task Test_AsyncTransform()
		{
			// async transform start concurrent tasks for all source items

			const int N = 100;
			const int MAX_CAPACITY = 5;

			// since this can lock up, we need a global timeout !
			using (var go = new CancellationTokenSource(TimeSpan.FromSeconds(10)))
			{
				var token = go.Token;

				var rnd1 = new Random(1234);

				var queue = AsyncHelpers.CreateOrderPreservingAsyncBuffer<double>(MAX_CAPACITY);

				var transform = new AsyncTransform<int, double>(
					async (x, _) =>
					{
						// each element takes a random time to compute
						await Task.Delay(5 + rnd1.Next(25), this.Cancellation);
						return Math.Sqrt(x);
					},
					queue,
					TaskScheduler.Default
				);

				var pumpTask = queue.PumpToListAsync(token);

				for (int i = 0; i < N; i++)
				{
					// emulate a batched source
					if (i % 10 == 0) await Task.Delay(100, this.Cancellation);

					await transform.OnNextAsync(i, token);
				}
				transform.OnCompleted();


				await Task.WhenAny(pumpTask, Task.Delay(10 * 1000, go.Token));
				if (!pumpTask.IsCompleted)
				{
					Log("FAILED: HARD TIMEOUT! PumpTask did not complete in time :(");
					Assert.Fail("The PumpTask did not complete in time");
				}

				var list = await pumpTask;

				Log($"results: {String.Join(", ", list)}");
				Assert.That(list, Is.EqualTo(Enumerable.Range(0, N).Select(x => Math.Sqrt(x)).ToArray()));
			}
		}

		[Test]
		public async Task Test_AsyncPump_Stops_On_First_Error()
		{

			const int MAX_CAPACITY = 5;

			// since this can lock up, we need a global timeout !
			using (var go = new CancellationTokenSource(TimeSpan.FromSeconds(10)))
			{
				var token = go.Token;

				var queue = AsyncHelpers.CreateOrderPreservingAsyncBuffer<int>(MAX_CAPACITY);

				var pumpTask = queue.PumpToListAsync(token);

#pragma warning disable 162
				await queue.OnNextAsync(Task.FromResult(0), token);
				await queue.OnNextAsync(Task.FromResult(1), token);
				await queue.OnNextAsync(Task.Run<int>(() => { throw new InvalidOperationException("Oops"); return 123; }, this.Cancellation), token);
				await queue.OnNextAsync(Task.FromResult(3), token);
				await queue.OnNextAsync(Task.Run<int>(() => { throw new InvalidOperationException("Epic Fail"); return 456; }, this.Cancellation), token);
				queue.OnCompleted();
#pragma warning restore 162

				await Task.WhenAny(pumpTask, Task.Delay(10 * 1000, go.Token));
				if (!pumpTask.IsCompleted)
				{
					Log("FAILED: HARD TIMEOUT! PumpTask did not complete in time :(");
					Assert.Fail("The PumpTask did not complete in time");
				}

				Assert.That(async () => await pumpTask, Throws.InvalidOperationException.With.Message.EqualTo("Oops"), "Pump should rethrow the first exception encountered");
				//REVIEW: should we instead use AggregateException if multiple errors are pushed?
				// => AggregateException is a pain to use :(

			}

		}

	}

}
