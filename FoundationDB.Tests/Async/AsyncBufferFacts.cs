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

namespace FoundationDB.Async.Tests
{
	using FoundationDB.Async;
	using NUnit.Framework;
	using System;
	using System.Collections.Generic;
	using System.Linq;
	using System.Threading;
	using System.Threading.Tasks;

	[TestFixture]
	public class AsyncBufferFacts
	{

		[Test]
		public async Task Test_AsyncTaskBuffer_In_Arrival_Order()
		{
			// Test that we can queue N async tasks in a buffer that will only accept K tasks at a time,
			// and pump them into a list that will received in arrival order

			const int N = 20;
			const int K = 5;

			// since this can lock up, we need a global timeout !
			using (var go = new CancellationTokenSource(TimeSpan.FromSeconds(10)))
			{
				var token = go.Token;

				var list = new List<int>();
				bool didComplete = false;
				Exception error = null;
				var target = AsyncHelpers.CreateTarget<int>(
					(x, ct) =>
					{
						Console.WriteLine("[target#" + Thread.CurrentThread.ManagedThreadId + "] received " + x);
						list.Add(x);
					},
					() =>
					{
						didComplete = true;
						Console.WriteLine("[target#" + Thread.CurrentThread.ManagedThreadId + "] completed");
					},
					(e) =>
					{
						error = e.SourceException;
						Console.WriteLine("[target#" + Thread.CurrentThread.ManagedThreadId + "] error " + e.SourceException.ToString());
					}
				);

				var buffer = AsyncHelpers.CreateOrderPreservingAsyncBuffer<int>(K);

				Console.WriteLine("starting pumping");
				var pumpTask = buffer.PumpToAsync(target, token);

				var rnd = new Random(0x1337);
				for (int i = 0; i < N; i++)
				{
					int x = i;
					var task = Task.Run(async () =>
					{
						await Task.Delay(10 + rnd.Next(50));
						Console.WriteLine("[source#" + Thread.CurrentThread.ManagedThreadId + "] produced " + x);
						return x;
					});

					await buffer.OnNextAsync(task, token);
				}
				// signal the end
				buffer.OnCompleted();

				Console.WriteLine("finished producing");

				await pumpTask;

				Console.WriteLine("finsihed pumping");

				Console.WriteLine("Result: " + String.Join(", ", list));
				Assert.That(didComplete, Is.True);
				Assert.That(error, Is.Null);
				Assert.That(list, Is.EqualTo(Enumerable.Range(0, N).ToArray()));
			}

		}

		[Test]
		public async Task Test_AsyncTaskBuffer_In_Completion_Order()
		{
			// Test that we can queue N async tasks in a buffer that will only accept K tasks at a time,
			// and pump them into a list that will received in completion order

			const int N = 20;
			const int K = 5;

			// since this can lock up, we need a global timeout !
			using (var go = new CancellationTokenSource(TimeSpan.FromSeconds(10)))
			{
				var token = go.Token;

				var list = new List<int>();
				bool didComplete = false;
				Exception error = null;
				var target = AsyncHelpers.CreateTarget<int>(
					(x, ct) =>
					{
						Console.WriteLine("[target#" + Thread.CurrentThread.ManagedThreadId + "] received " + x);
						list.Add(x);
					},
					() =>
					{
						didComplete = true;
						Console.WriteLine("[target#" + Thread.CurrentThread.ManagedThreadId + "] completed");
					},
					(e) =>
					{
						error = e.SourceException;
						Console.WriteLine("[target#" + Thread.CurrentThread.ManagedThreadId + "] error " + e.SourceException.ToString());
					}
				);

				var buffer = AsyncHelpers.CreateUnorderedAsyncBuffer<int>(K);

				Console.WriteLine("starting pumping");
				var pumpTask = buffer.PumpToAsync(target, token);

				var rnd = new Random(0x1337);
				for (int i = 0; i < N; i++)
				{
					int x = i;
					var task = Task.Run(async () =>
					{
						await Task.Delay(10 + rnd.Next(50));
						Console.WriteLine("[source#" + Thread.CurrentThread.ManagedThreadId + "] produced " + x);
						return x;
					});

					await buffer.OnNextAsync(task, token);
				}
				// signal the end
				buffer.OnCompleted();

				Console.WriteLine("finished producing");

				await pumpTask;

				Console.WriteLine("finsihed pumping");

				Console.WriteLine("Result: " + String.Join(", ", list));
				Assert.That(didComplete, Is.True);
				Assert.That(error, Is.Null);
				//note: order doesn't matter, but all should be there
				Assert.That(list, Is.EquivalentTo(Enumerable.Range(0, N).ToArray()));
			}

		}

		[Test]
		public async Task Test_FdbAsyncTransform()
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
						await Task.Delay(5 + rnd1.Next(25));
						return Math.Sqrt(x);
					},
					queue,
					TaskScheduler.Default
				);

				var pumpTask = AsyncHelpers.PumpToListAsync(queue, new List<double>(N));

				var rnd2 = new Random(5678);

				for (int i = 0; i < N; i++)
				{
					// emulate a batched source
					if (i % 10 == 0) await Task.Delay(100);

					await transform.OnNextAsync(i, token);
				}
				transform.OnCompleted();

				var list = await pumpTask;

				Console.WriteLine("results: " + String.Join(", ", list));
				Assert.That(list, Is.EqualTo(Enumerable.Range(0, N).Select(x => Math.Sqrt(x)).ToArray()));
			}
		}


	}


}
