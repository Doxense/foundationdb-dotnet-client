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
	using FoundationDB.Client;
	using FoundationDB.Layers.Tuples;
	using NUnit.Framework;
	using System;
	using System.Collections.Generic;
	using System.Diagnostics;
	using System.Linq;
	using System.Threading;
	using System.Threading.Tasks;

	[TestFixture]
	public class DatabaseBulkFacts : FdbTest
	{

		[Test]
		public async Task Test_Can_Bulk_Insert()
		{
			const int N = 20 * 1000;

			using (var db = await OpenTestPartitionAsync())
			{
				Console.WriteLine("Bulk inserting " + N + " random items...");

				var location = await GetCleanDirectory(db, "Bulk");

				var rnd = new Random(2403);
				var data = Enumerable.Range(0, N)
					.Select((x) => new KeyValuePair<Slice, Slice>(location.Pack(x.ToString("x8")), Slice.Random(rnd, 16 + rnd.Next(240))))
					.ToArray();

				Console.WriteLine("Total data size is " + data.Sum(x => x.Key.Count + x.Value.Count).ToString("N0") + " bytes");

				Console.WriteLine("Starting...");

				var sw = Stopwatch.StartNew();
				long? lastReport = null;
				int called = 0;
				long count = await Fdb.Bulk.WriteAsync(
					db,
					data,
					new Progress<long>((n) =>
					{
						++called;
						lastReport = n;
						Console.WriteLine("Chunk #" + called + " : " + n.ToString());
					}),
					this.Cancellation
				);
				sw.Stop();

				Console.WriteLine("Done in " + sw.Elapsed.TotalSeconds.ToString("N3") + " secs and " + called + " chunks");

				Assert.That(count, Is.EqualTo(N));
				Assert.That(lastReport, Is.EqualTo(N));
				Assert.That(called, Is.GreaterThan(0));

				// read everything back...

				Console.WriteLine("Reading everything back...");

				var stored = await db.ReadAsync((tr) =>
				{
					return tr.GetRangeStartsWith(location).ToArrayAsync();
				}, this.Cancellation);

				Assert.That(stored.Length, Is.EqualTo(N));
				Assert.That(stored, Is.EqualTo(data));
			}
		}

		[Test]
		public async Task Test_Can_Batch_Read_AsyncWithContextAndState()
		{
			const int N = 50 * 1000;

			using (var db = await OpenTestPartitionAsync())
			{

				Console.WriteLine("Bulk inserting " + N + " items...");
				var location = await GetCleanDirectory(db, "Bulk");

				Console.WriteLine("Preparing...");

				await Fdb.Bulk.WriteAsync(
					db,
					Enumerable.Range(1, N).Select((x) => new KeyValuePair<Slice, Slice>(location.Pack(x), Slice.FromInt32(x))),
					null,
					this.Cancellation
				);

				Console.WriteLine("Reading...");

				long total = 0;
				long count = 0;
				int chunks = 0;
				var sw = Stopwatch.StartNew();
				await Fdb.Bulk.ReadBatchedAsync(
					db,
					Enumerable.Range(1, N).Select(x => location.Pack(x)),
					() => FdbTuple.Create(0L, 0L),
					async (xs, ctx, state) =>
					{
						Interlocked.Increment(ref chunks);
						Console.WriteLine("> Called with batch of " + xs.Length.ToString("N0") + " at offset " + ctx.Position.ToString("N0") + " of gen " + ctx.Generation + " with step " + ctx.Step + " and cooldown " + ctx.Cooldown + " (genElapsed=" + ctx.ElapsedGeneration + ", totalElapsed=" + ctx.ElapsedTotal + ")");

						var throttle = Task.Delay(TimeSpan.FromMilliseconds(10 + (xs.Length / 25) * 5)); // magic numbers to try to last longer than 5 sec
						var results = await ctx.Transaction.GetValuesAsync(xs);
						await throttle;

						long sum = 0;
						for (int i = 0; i < results.Length; i++)
						{
							sum += results[i].ToInt32();
						}
						return FdbTuple.Create(state.Item1 + sum, state.Item2 + results.Length);
					},
					(state) =>
					{
						Interlocked.Add(ref total, state.Item1);
						Interlocked.Add(ref count, state.Item2);
					},
					this.Cancellation
				);
				sw.Stop();

				Console.WriteLine("Done in " + sw.Elapsed.TotalSeconds.ToString("N3") + " seconds and " + chunks + " chunks");
				Console.WriteLine("Sum of integers 1 to " + count + " is " + total);
			}
		}

		[Test]
		public async Task Test_Can_Batch_Read_WithContextAndState()
		{
			const int N = 50 * 1000;

			using (var db = await OpenTestPartitionAsync())
			{

				Console.WriteLine("Bulk inserting " + N + " items...");
				var location = await GetCleanDirectory(db, "Bulk");

				Console.WriteLine("Preparing...");

				await Fdb.Bulk.WriteAsync(
					db,
					Enumerable.Range(1, N).Select((x) => new KeyValuePair<Slice, Slice>(location.Pack(x), Slice.FromInt32(x))),
					null,
					this.Cancellation
				);

				Console.WriteLine("Reading...");

				long total = 0;
				long count = 0;
				int chunks = 0;
				var sw = Stopwatch.StartNew();
				await Fdb.Bulk.ReadBatchedAsync(
					db,
					Enumerable.Range(1, N).Select(x => location.Pack(x)),
					() => FdbTuple.Create(0L, 0L), // (sum, count)
					(xs, ctx, state) =>
					{
						Interlocked.Increment(ref chunks);
						Console.WriteLine("> Called with batch of " + xs.Length.ToString("N0") + " at offset " + ctx.Position.ToString("N0") + " of gen " + ctx.Generation + " with step " + ctx.Step + " and cooldown " + ctx.Cooldown + " (gen=" + ctx.ElapsedGeneration + ", total=" + ctx.ElapsedTotal + ")");

						var t = ctx.Transaction.GetValuesAsync(xs);
						Thread.Sleep(TimeSpan.FromMilliseconds(10 + (xs.Length / 25) * 5)); // magic numbers to try to last longer than 5 sec
						var results = t.Result; // <-- this is bad practice, never do that in real life, 'mkay?

						long sum = 0;
						for (int i = 0; i < results.Length; i++)
						{
							sum += results[i].ToInt32();
						}
						return FdbTuple.Create(
							state.Item1 + sum, // updated sum
							state.Item2 + results.Length // updated count
						);
					},
					(state) =>
					{
						Interlocked.Add(ref total, state.Item1);
						Interlocked.Add(ref count, state.Item2);
					},
					this.Cancellation
				);
				sw.Stop();

				Console.WriteLine("Done in " + sw.Elapsed.TotalSeconds.ToString("N3") + " seconds and " + chunks + " chunks");
				Console.WriteLine("Sum of integers 1 to " + count + " is " + total);

			}
		}

		[Test]
		public async Task Test_Can_Batch_Read_AsyncWithContext()
		{
			const int N = 50 * 1000;

			using (var db = await OpenTestPartitionAsync())
			{

				Console.WriteLine("Bulk inserting " + N + " items...");
				var location = await GetCleanDirectory(db, "Bulk");

				Console.WriteLine("Preparing...");

				await Fdb.Bulk.WriteAsync(
					db,
					Enumerable.Range(1, N).Select((x) => new KeyValuePair<Slice, Slice>(location.Pack(x), Slice.FromInt32(x))),
					null,
					this.Cancellation
				);

				Console.WriteLine("Reading...");

				long total = 0;
				long count = 0;
				int chunks = 0;
				var sw = Stopwatch.StartNew();
				await Fdb.Bulk.ReadBatchedAsync(
					db,
					Enumerable.Range(1, N).Select(x => location.Pack(x)),
					async (xs, ctx) =>
					{
						Interlocked.Increment(ref chunks);
						Console.WriteLine("> Called with batch of " + xs.Length.ToString("N0") + " at offset " + ctx.Position.ToString("N0") + " of gen " + ctx.Generation + " with step " + ctx.Step + " and cooldown " + ctx.Cooldown + " (genElapsed=" + ctx.ElapsedGeneration + ", totalElapsed=" + ctx.ElapsedTotal + ")");

						var throttle = Task.Delay(TimeSpan.FromMilliseconds(10 + (xs.Length / 25) * 5)); // magic numbers to try to last longer than 5 sec
						var results = await ctx.Transaction.GetValuesAsync(xs);
						await throttle;

						long sum = 0;
						for (int i = 0; i < results.Length; i++)
						{
							sum += results[i].ToInt32();
						}
						Interlocked.Add(ref total, sum);
						Interlocked.Add(ref count, results.Length);
					},
					this.Cancellation
				);
				sw.Stop();

				Console.WriteLine("Done in " + sw.Elapsed.TotalSeconds.ToString("N3") + " seconds and " + chunks + " chunks");
				Console.WriteLine("Sum of integers 1 to " + count + " is " + total);

			}
		}

	}
}
