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

namespace FoundationDB.Client.Tests
{
	using System;
	using System.Collections.Generic;
	using System.Diagnostics;
	using System.IO;
	using System.Linq;
	using System.Text;
	using System.Threading;
	using System.Threading.Tasks;
	using Doxense.Collections.Tuples;
	using FoundationDB.Client;
	using FoundationDB.Filters.Logging;
	using FoundationDB.Layers.Directories;
	using NUnit.Framework;

	[TestFixture]
	public class DatabaseBulkFacts : FdbTest
	{

		[Test]
		public async Task Test_Can_Bulk_Insert_Raw_Data()
		{
			const int N = 20 * 1000;

			using (var db = await OpenTestPartitionAsync())
			{
				Log("Bulk inserting {0:N0} random items...", N);

				var location = await GetCleanDirectory(db, "Bulk", "Write");

				var rnd = new Random(2403);
				var data = Enumerable.Range(0, N)
					.Select((x) => (Key: location.Keys.Encode(x.ToString("x8")), Value: Slice.Random(rnd, 16 + rnd.Next(240))))
					.ToArray();

				Log("Total data size is {0:N0} bytes", data.Sum(x => x.Key.Count + x.Value.Count));

				Log("Starting...");

				var sw = Stopwatch.StartNew();
				long? lastReport = null;
				int called = 0;
				long count = await Fdb.Bulk.WriteAsync(
					db,
					data,
					new Fdb.Bulk.WriteOptions
					{
						Progress = new Progress<long>((n) =>
						{
							++called;
							lastReport = n;
							Log("Chunk #{0} : {1}", called, n);
						})
					},
					this.Cancellation
				);
				sw.Stop();

				//note: calls to Progress<T> are async, so we need to wait a bit ...
				Thread.Sleep(640); // "Should be enough"

				Log("Done in {0:N3} sec and {1} chunks", sw.Elapsed.TotalSeconds, called);

				Assert.That(count, Is.EqualTo(N), "count");
				Assert.That(lastReport, Is.EqualTo(N), "lastResport");
				Assert.That(called, Is.GreaterThan(0), "called");

				// read everything back...

				Log("Reading everything back...");

				var stored = await db.ReadAsync((tr) =>
				{
					return tr.GetRangeStartsWith(location).ToArrayAsync();
				}, this.Cancellation);

				Assert.That(stored.Length, Is.EqualTo(N));
				Assert.That(stored, Is.EqualTo(data));
			}
		}

		[Test]
		public async Task Test_Can_Bulk_Insert_Items()
		{
			const int N = 20 * 1000;

			using (var db = await OpenTestPartitionAsync())
			{
				db.DefaultTimeout = 60 * 1000;

				Log("Generating {0:N0} random items...", N);

				var location = await GetCleanDirectory(db, "Bulk", "Insert");

				var rnd = new Random(2403);
				var data = Enumerable.Range(0, N)
					.Select((x) => new KeyValuePair<int, int>(x, 16 + (int)(Math.Pow(rnd.NextDouble(), 4) * 1 * 1000)))
					.ToList();

				long totalSize = data.Sum(x => (long)x.Value);
				Log("Total size is ~ {0:N0} bytes", totalSize);

				Log("Starting...");

				long called = 0;
				var uniqueKeys = new HashSet<int>();
				var sw = Stopwatch.StartNew();
				long count = await Fdb.Bulk.InsertAsync(
					db,
					data,
					(kv, tr) =>
					{
						++called;
						uniqueKeys.Add(kv.Key);
						tr.Set(
							location.Keys.Encode(kv.Key),
							Slice.FromString(new string('A', kv.Value))
						);
					},
					this.Cancellation
				);
				sw.Stop();

				//note: calls to Progress<T> are async, so we need to wait a bit ...
				Thread.Sleep(640);   // "Should be enough"

				Log("Done in {0:N3} sec for {1:N0} keys and {2:N0} bytes", sw.Elapsed.TotalSeconds, count, totalSize);
				Log("> Throughput {0:N0} key/sec and {1:N3} MB/sec", count / sw.Elapsed.TotalSeconds, totalSize / (1024 * 1024 * sw.Elapsed.TotalSeconds));
				Log("Called {0:N0} for {1:N0} unique keys", called, uniqueKeys.Count);

				Assert.That(count, Is.EqualTo(N), "count");
				Assert.That(uniqueKeys.Count, Is.EqualTo(N), "unique keys");
				Assert.That(called, Is.EqualTo(N), "number of calls (no retries)");

				// read everything back...

				Log("Reading everything back...");

				var stored = await db.ReadAsync((tr) =>
				{
					return tr.GetRange(location.Keys.ToRange()).ToArrayAsync();
				}, this.Cancellation);

				Assert.That(stored.Length, Is.EqualTo(N), "DB contains less or more items than expected");
				for (int i = 0; i < stored.Length;i++)
				{
					Assert.That(stored[i].Key, Is.EqualTo(location.Keys.Encode(data[i].Key)), "Key #{0}", i);
					Assert.That(stored[i].Value.Count, Is.EqualTo(data[i].Value), "Value #{0}", i);
				}

				// cleanup because this test can produce a lot of data
				await location.RemoveAsync(db, this.Cancellation);
			}
		}

		[Test]
		public async Task Test_Can_Batch_ForEach_AsyncWithContextAndState()
		{
			const int N = 50 * 1000;

			using (var db = await OpenTestPartitionAsync())
			{

				Log("Bulk inserting {0:N0} items...", N);
				var location = await GetCleanDirectory(db, "Bulk", "ForEach");

				Log("Preparing...");

				await Fdb.Bulk.WriteAsync(
					db,
					Enumerable.Range(1, N).Select((x) => (location.Keys.Encode(x), Slice.FromInt32(x))),
					this.Cancellation
				);

				Log("Reading...");

				long total = 0;
				long count = 0;
				int chunks = 0;
				var sw = Stopwatch.StartNew();
				await Fdb.Bulk.ForEachAsync(
					db,
					Enumerable.Range(1, N).Select(x => location.Keys.Encode(x)),
					() => (Total: 0L, Count: 0L),
					async (xs, ctx, state) =>
					{
						Interlocked.Increment(ref chunks);
						Log("> Called with batch of {0:N0} items at offset {1:N0} of gen #{2} with step {3:N0} and cooldown {4} (generation = {5:N3} sec, total = {6:N3} sec)", xs.Length, ctx.Position, ctx.Generation, ctx.Step, ctx.Cooldown, ctx.ElapsedGeneration.TotalSeconds, ctx.ElapsedTotal.TotalSeconds);

						var throttle = Task.Delay(TimeSpan.FromMilliseconds(10 + (xs.Length / 25) * 5)); // magic numbers to try to last longer than 5 sec
						var results = await ctx.Transaction.GetValuesAsync(xs);
						await throttle;

						long sum = 0;
						foreach (Slice x in results)
						{
							sum += x.ToInt32();
						}

						state.Total += sum;
						state.Count += results.Length;
						return state;
					},
					(state) =>
					{
						Interlocked.Add(ref total, state.Total);
						Interlocked.Add(ref count, state.Count);
					},
					this.Cancellation
				);
				sw.Stop();

				Log("Done in {0:N3} sec and {1} chunks", sw.Elapsed.TotalSeconds, chunks);
				Log("Sum of integers 1 to {0:N0} is {1:N0}", count, total);

				// cleanup because this test can produce a lot of data
				await location.RemoveAsync(db, this.Cancellation);
			}
		}

		[Test]
		public async Task Test_Can_Bulk_Batched_Insert_Items()
		{
			const int N = 200 * 1000;

			using (var db = await OpenTestPartitionAsync())
			{
				db.DefaultTimeout = 60 * 1000;

				Log("Generating {0:N0} random items...", N);

				var location = await GetCleanDirectory(db, "Bulk", "Insert");

				var rnd = new Random(2403);
				var data = Enumerable.Range(0, N)
					.Select((x) => new KeyValuePair<int, int>(x, 16 + (int)(Math.Pow(rnd.NextDouble(), 4) * 1000)))
					.ToList();

				long totalSize = data.Sum(x => (long)x.Value);
				Log("Total size is ~ {0:N0} bytes", totalSize);

				Log("Starting...");

				long called = 0;
				var uniqueKeys = new HashSet<int>();
				var batchCounts = new List<int>();
				var trSizes = new List<long>();
				var sw = Stopwatch.StartNew();
				long count = await Fdb.Bulk.InsertBatchedAsync(
					db,
					data,
					(kvps, tr) =>
					{
						++called;
						batchCounts.Add(kvps.Length);
						foreach (var kv in kvps)
						{
							uniqueKeys.Add(kv.Key);
							tr.Set(
								location.Keys.Encode(kv.Key),
								Slice.FromString(new string('A', kv.Value))
							);
						}
						trSizes.Add(tr.Size);
						//Log("> Added {0:N0} items to transaction, yielding {1:N0} bytes", kvps.Length, tr.Size);
					},
					new Fdb.Bulk.WriteOptions() { BatchCount = 100 },
					this.Cancellation
				);
				sw.Stop();

				//note: calls to Progress<T> are async, so we need to wait a bit ...
				Thread.Sleep(640);   // "Should be enough"

				Log("Done in {0:N3} sec for {1:N0} keys and {2:N0} bytes", sw.Elapsed.TotalSeconds, count, totalSize);
				Log("> Throughput {0:N0} key/sec and {1:N3} MB/sec", count / sw.Elapsed.TotalSeconds, totalSize / (1024 * 1024 * sw.Elapsed.TotalSeconds));
				Log("Called {0:N0} for {1:N0} unique keys", called, uniqueKeys.Count);

				Assert.That(count, Is.EqualTo(N), "count");
				Assert.That(uniqueKeys.Count, Is.EqualTo(N), "unique keys");
				Assert.That(batchCounts.Sum(), Is.EqualTo(N), "total of keys per batch (no retries)");

				Log("Batch counts: {0}", String.Join(", ", batchCounts));
				Log("Batch sizes : {0}", String.Join(", ", trSizes));
				Log("Total Size  : {0:N0}", trSizes.Sum());

				// read everything back...

				Log("Reading everything back...");

				var stored = await db.ReadAsync((tr) =>
				{
					return tr.GetRange(location.Keys.ToRange()).ToArrayAsync();
				}, this.Cancellation);

				Log("Read {0:N0} keys", stored.Length);

				Assert.That(stored.Length, Is.EqualTo(N), "DB contains less or more items than expected");
				for (int i = 0; i < stored.Length; i++)
				{
					Assert.That(stored[i].Key, Is.EqualTo(location.Keys.Encode(data[i].Key)), "Key #{0}", i);
					Assert.That(stored[i].Value.Count, Is.EqualTo(data[i].Value), "Value #{0}", i);
				}

				// cleanup because this test can produce a lot of data
				await location.RemoveAsync(db, this.Cancellation);
			}
		}

		[Test]
		public async Task Test_Can_Batch_ForEach_WithContextAndState()
		{
			const int N = 50 * 1000;

			using (var db = await OpenTestPartitionAsync())
			{

				Log("Bulk inserting {0:N0} items...", N);
				var location = await GetCleanDirectory(db, "Bulk", "ForEach");

				Log("Preparing...");

				await Fdb.Bulk.WriteAsync(
					db,
					Enumerable.Range(1, N).Select((x) => (location.Keys.Encode(x), Slice.FromInt32(x))),
					this.Cancellation
				);

				Log("Reading...");

				long total = 0;
				long count = 0;
				int chunks = 0;
				var sw = Stopwatch.StartNew();
				await Fdb.Bulk.ForEachAsync(
					db,
					Enumerable.Range(1, N).Select(x => location.Keys.Encode(x)),
					() => (Total: 0L, Count: 0L),
					(xs, ctx, state) =>
					{
						Interlocked.Increment(ref chunks);
						Log("> Called with batch of {0:N0} at offset {1:N0} of gen {2} with step {3} and cooldown {4} (generation = {5:N3} sec, total = {6:N3} sec)", xs.Length, ctx.Position, ctx.Generation, ctx.Step, ctx.Cooldown, ctx.ElapsedGeneration.TotalSeconds, ctx.ElapsedTotal.TotalSeconds);

						var t = ctx.Transaction.GetValuesAsync(xs);
						Thread.Sleep(TimeSpan.FromMilliseconds(10 + (xs.Length / 25) * 5)); // magic numbers to try to last longer than 5 sec
						var results = t.Result; // <-- this is bad practice, never do that in real life, 'mkay?

						long sum = 0;
						foreach (Slice x in results)
						{
							sum += x.ToInt32();
						}

						state.Total += sum;
						state.Count += results.Length;
						return state;
					},
					(state) =>
					{
						Interlocked.Add(ref total, state.Total);
						Interlocked.Add(ref count, state.Count);
					},
					this.Cancellation
				);
				sw.Stop();

				Log("Done in {0:N3} sec and {1} chunks", sw.Elapsed.TotalSeconds, chunks);
				Log("Sum of integers 1 to {0:N0} is {1:N0}", count, total);

				// cleanup because this test can produce a lot of data
				await location.RemoveAsync(db, this.Cancellation);
			}
		}

		[Test]
		public async Task Test_Can_Batch_ForEach_AsyncWithContext()
		{
			const int N = 50 * 1000;

			using (var db = await OpenTestPartitionAsync())
			{

				Log("Bulk inserting {0:N0} items...", N);
				var location = await GetCleanDirectory(db, "Bulk", "ForEach");

				Log("Preparing...");

				await Fdb.Bulk.WriteAsync(
					db,
					Enumerable.Range(1, N).Select((x) => (location.Keys.Encode(x), Slice.FromInt32(x))),
					this.Cancellation
				);

				Log("Reading...");

				long total = 0;
				long count = 0;
				int chunks = 0;
				var sw = Stopwatch.StartNew();
				await Fdb.Bulk.ForEachAsync(
					db,
					Enumerable.Range(1, N).Select(x => location.Keys.Encode(x)),
					async (xs, ctx) =>
					{
						Interlocked.Increment(ref chunks);
						Log("> Called with batch of " + xs.Length.ToString("N0") + " at offset " + ctx.Position.ToString("N0") + " of gen " + ctx.Generation + " with step " + ctx.Step + " and cooldown " + ctx.Cooldown + " (genElapsed=" + ctx.ElapsedGeneration + ", totalElapsed=" + ctx.ElapsedTotal + ")");

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

				Log("Done in {0:N3} sec and {1} chunks", sw.Elapsed.TotalSeconds, chunks);
				Log("Sum of integers 1 to {0:N0} is {1:N0}", count, total);

				// cleanup because this test can produce a lot of data
				await location.RemoveAsync(db, this.Cancellation);
			}
		}

		[Test]
		public async Task Test_Can_Batch_Aggregate()
		{
			const int N = 50 * 1000;

			using (var db = await OpenTestPartitionAsync())
			{

				Log("Bulk inserting {0:N0} items...", N);
				var location = await GetCleanDirectory(db, "Bulk", "Aggregate");

				Log("Preparing...");

				var rnd = new Random(2403);
				var source = Enumerable.Range(1, N).Select((x) => new KeyValuePair<int, int>(x, rnd.Next(1000))).ToList();

				await Fdb.Bulk.WriteAsync(
					db,
					source.Select((x) => (location.Keys.Encode(x.Key), Slice.FromInt32(x.Value))),
					this.Cancellation
				);

				Log("Reading...");

				int chunks = 0;
				var sw = Stopwatch.StartNew();
				long total = await Fdb.Bulk.AggregateAsync(
					db,
					source.Select(x => location.Keys.Encode(x.Key)),
					() => 0L,
					async (xs, ctx, sum) =>
					{
						Interlocked.Increment(ref chunks);
						Log("> Called with batch of " + xs.Length.ToString("N0") + " at offset " + ctx.Position.ToString("N0") + " of gen " + ctx.Generation + " with step " + ctx.Step + " and cooldown " + ctx.Cooldown + " (genElapsed=" + ctx.ElapsedGeneration + ", totalElapsed=" + ctx.ElapsedTotal + ")");

						var throttle = Task.Delay(TimeSpan.FromMilliseconds(10 + (xs.Length / 25) * 5)); // magic numbers to try to last longer than 5 sec
						var results = await ctx.Transaction.GetValuesAsync(xs);
						await throttle;

						for (int i = 0; i < results.Length; i++)
						{
							sum += results[i].ToInt32();
						}
						return sum;
					},
					this.Cancellation
				);
				sw.Stop();

				Log("Done in {0:N0} sec and {1} chunks", sw.Elapsed.TotalSeconds, chunks);

				long actual = source.Sum(x => (long)x.Value);
				Log("> Computed sum of the {0:N0} random values is {1:N0}", N, total);
				Log("> Actual sum of the {0:N0} random values is {1:N0}", N, actual);
				Assert.That(total, Is.EqualTo(actual));

				// cleanup because this test can produce a lot of data
				await location.RemoveAsync(db, this.Cancellation);
			}
		}

		[Test]
		public async Task Test_Can_Batch_Aggregate_With_Transformed_Result()
		{
			const int N = 50 * 1000;

			using (var db = await OpenTestPartitionAsync())
			{

				Log("Bulk inserting {0:N0} items...", N);
				var location = await GetCleanDirectory(db, "Bulk", "Aggregate");

				Log("Preparing...");

				var rnd = new Random(2403);
				var source = Enumerable.Range(1, N).Select((x) => new KeyValuePair<int, int>(x, rnd.Next(1000))).ToList();

				await Fdb.Bulk.WriteAsync(
					db,
					source.Select((x) => (location.Keys.Encode(x.Key), Slice.FromInt32(x.Value))),
					this.Cancellation
				);

				Log("Reading...");

				int chunks = 0;
				var sw = Stopwatch.StartNew();
				double average = await Fdb.Bulk.AggregateAsync(
					db,
					source.Select(x => location.Keys.Encode(x.Key)),
					() => (Total: 0L, Count: 0L),
					async (xs, ctx, state) =>
					{
						Interlocked.Increment(ref chunks);
						Log("> Called with batch of " + xs.Length.ToString("N0") + " at offset " + ctx.Position.ToString("N0") + " of gen " + ctx.Generation + " with step " + ctx.Step + " and cooldown " + ctx.Cooldown + " (genElapsed=" + ctx.ElapsedGeneration + ", totalElapsed=" + ctx.ElapsedTotal + ")");

						var throttle = Task.Delay(TimeSpan.FromMilliseconds(10 + (xs.Length / 25) * 5)); // magic numbers to try to last longer than 5 sec
						var results = await ctx.Transaction.GetValuesAsync(xs);
						await throttle;

						long sum = 0L;
						foreach (Slice x in results)
						{
							sum += x.ToInt32();
						}
						state.Total += sum;
						state.Count += results.Length;
						return state;
					},
					(state) => (double) state.Total / state.Count,
					this.Cancellation
				);
				sw.Stop();

				Log("Done in {0:N3} sec and {1} chunks", sw.Elapsed.TotalSeconds, chunks);

				double actual = (double)source.Sum(x => (long)x.Value) / source.Count;
				Log("> Computed average of the {0:N0} random values is {1:N3}", N, average);
				Log("> Actual average of the {0:N0} random values is {1:N3}", N, actual);
				Assert.That(average, Is.EqualTo(actual).Within(double.Epsilon));

				// cleanup because this test can produce a lot of data
				await location.RemoveAsync(db, this.Cancellation);
			}
		}

		[Test]
		public async Task Test_Can_Export_To_Disk()
		{
			const int N = 50 * 1000;

			using (var zedb = await OpenTestPartitionAsync())
			{
				var db = zedb.Logged((tr) => Log(tr.Log.GetTimingsReport(true)));

				Log("Bulk inserting {0:N0} items...", N);
				var location = await GetCleanDirectory(db, "Bulk", "Export");

				Log("Preparing...");

				var rnd = new Random(2403);
				var source = Enumerable
					.Range(1, N)
					.Select((x) => new KeyValuePair<Guid, Slice>(Guid.NewGuid(), Slice.Random(rnd, rnd.Next(8, 256))))
					.ToList();

				Log("Inserting...");

				await Fdb.Bulk.WriteAsync(
					db.WithoutLogging(),
					source.Select((x) => (location.Keys.Encode(x.Key), x.Value)),
					this.Cancellation
				);

				string path = Path.Combine(TestContext.CurrentContext.WorkDirectory, "export.txt");
				Log("Exporting to disk... " + path);
				int chunks = 0;
				var sw = Stopwatch.StartNew();
				using (var file = File.CreateText(path))
				{
					double average = await Fdb.Bulk.ExportAsync(
						db,
						location.Keys.ToRange(),
						async (xs, pos, ct) =>
						{
							Assert.That(xs, Is.Not.Null);

							Interlocked.Increment(ref chunks);
							Log("> Called with batch [{0:N0}..{1:N0}] ({2:N0} items, {3:N0} bytes)", pos, pos + xs.Length - 1, xs.Length, xs.Sum(kv => kv.Key.Count + kv.Value.Count));

							//TO CHECK:
							// => keys are ordered in the batch
							// => no duplicates

							var sb = new StringBuilder(4096);
							foreach(var x in xs)
							{
								sb.AppendFormat("{0} = {1}\r\n", location.Keys.Decode<Guid>(x.Key), x.Value.ToBase64());
							}
							await file.WriteAsync(sb.ToString());
						},
						this.Cancellation
					);
				}
				sw.Stop();
				Log("Done in {0:N3} sec and {1} chunks", sw.Elapsed.TotalSeconds, chunks);
				Log("File size is {0} bytes", new FileInfo(path).Length);

				// cleanup because this test can produce a lot of data
				await location.RemoveAsync(zedb, this.Cancellation);

				File.Delete(path);
			}
		}

	}
}
