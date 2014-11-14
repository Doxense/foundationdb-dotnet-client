#region Copyright (c) 2013-2014, Doxense SAS. All rights reserved.
// See License.MD for license information
#endregion

namespace FoundationDB.Storage.Memory.API.Tests
{
	using FoundationDB.Client;
	using FoundationDB.Layers.Directories;
	using FoundationDB.Layers.Indexing;
	using FoundationDB.Linq;
	using FoundationDB.Storage.Memory.Tests;
	using NUnit.Framework;
	using System;
	using System.Collections.Generic;
	using System.Diagnostics;
	using System.Linq;
	using System.Threading;
	using System.Threading.Tasks;

	[TestFixture]
	[Category("LongRunning")]
	public class Benchmarks : FdbTest
	{

		private static void DumpResult(string label, long total, long trans, TimeSpan elapsed)
		{
			Log(
				"{0,-12}: {1,10:N0} keys in {2,4:N3} sec => {3,9:N0} kps, {4,7:N0} tps",
				label,
				total,
				elapsed.TotalSeconds,
				total / elapsed.TotalSeconds,
				trans / elapsed.TotalSeconds
			);
		}

		private static void DumpMemory(bool collect = false)
		{
			if (collect)
			{
				GC.Collect();
				GC.WaitForPendingFinalizers();
				GC.Collect();
			}
			Log("Total memory: Managed={0:N1} KiB, WorkingSet={1:N1} KiB", GC.GetTotalMemory(false) / 1024.0, Environment.WorkingSet / 1024.0);
		}

		[Test]
		public async Task MiniBench()
		{
			const int M = 1 * 1000 * 1000;
			const int B = 100;
			const int ENTROPY = 10 * 1000;

			const int T = M / B;
			const int KEYSIZE = 10;
			const int VALUESIZE = 100;
			const bool RANDOM = false;

			var rnd = new Random();

			//WARMUP
			using (var db = MemoryDatabase.CreateNew("FOO"))
			{
				await db.WriteAsync((tr) => tr.Set(db.Tuples.EncodeKey("hello"), Slice.FromString("world")), this.Cancellation);
				Slice.Random(rnd, KEYSIZE);
				Slice.Random(rnd, VALUESIZE);
			}

			Log("Inserting {0}-bytes {1} keys / {2}-bytes values, in {3:N0} transactions", KEYSIZE, RANDOM ? "random" : "ordered", VALUESIZE, T);

			bool random = RANDOM;
			string fmt = "D" + KEYSIZE;
			using (var db = MemoryDatabase.CreateNew("FOO"))
			{
				DumpMemory(collect: true);

				long total = 0;

				var payload = new byte[ENTROPY + VALUESIZE];
				rnd.NextBytes(payload);
				// help with compression by doubling every byte
				for (int i = 0; i < payload.Length; i += 2) payload[i + 1] = payload[i];

				var sw = Stopwatch.StartNew();
				sw.Stop();

				sw.Restart();
				for (int i = 0; i < T; i++)
				{
					using (var tr = db.BeginTransaction(this.Cancellation))
					{
						for (int j = 0; j < B; j++)
						{
							Slice key;
							if (random)
							{
								do
								{
									key = Slice.Random(rnd, KEYSIZE);
								}
								while (key[0] == 255);
							}
							else
							{
								int x = i * B + j;
								//x = x % 1000;
								key = Slice.FromString(x.ToString(fmt));
							}

							tr.Set(key, Slice.Create(payload, rnd.Next(ENTROPY), VALUESIZE));
							Interlocked.Increment(ref total);
						}
						await tr.CommitAsync().ConfigureAwait(false);
					}
					if (i % 1000 == 0) Console.Write(".");// + (i * B).ToString("D10"));
				}

				sw.Stop();
				Log("done");
				Log("* Inserted: {0:N0} keys", total);
				Log("* Elapsed : {0:N3} sec", sw.Elapsed.TotalSeconds);
				Log("* TPS: {0:N0} transactions/sec", T / sw.Elapsed.TotalSeconds);
				Log("* KPS: {0:N0} keys/sec", total / sw.Elapsed.TotalSeconds);
				Log("* BPS: {0:N0} bytes/sec", (total * (KEYSIZE + VALUESIZE)) / sw.Elapsed.TotalSeconds);

				DumpMemory(collect: true);

				db.Debug_Dump(false);

				DumpResult("WriteSeq" + B, total, total / B, sw.Elapsed);

				string path = @".\\minibench.pndb";
				Log("Saving {0} ...", path);
				sw.Restart();
				await db.SaveSnapshotAsync(path);
				sw.Stop();
				Log("* Saved {0:N0} bytes in {1:N3} sec", new System.IO.FileInfo(path).Length, sw.Elapsed.TotalSeconds);

				Log("Warming up reads...");
				var data = await db.GetValuesAsync(Enumerable.Range(0, 100).Select(i => Slice.FromString(i.ToString(fmt))), this.Cancellation);

				Log("Starting read tests...");

				#region sequential reads

				sw.Restart();
				for (int i = 0; i < total; i += 10)
				{
					using (var tr = db.BeginReadOnlyTransaction(this.Cancellation))
					{
						await tr.GetValuesAsync(Enumerable.Range(i, 10).Select(x => Slice.FromString(x.ToString(fmt)))).ConfigureAwait(false);
					}
				}
				sw.Stop();
				DumpResult("SeqRead10", total, total / 10, sw.Elapsed);

				sw.Restart();
				for (int i = 0; i < total; i += 10)
				{
					using (var tr = db.BeginReadOnlyTransaction(this.Cancellation))
					{
						await tr.Snapshot.GetValuesAsync(Enumerable.Range(i, 10).Select(x => Slice.FromString(x.ToString(fmt)))).ConfigureAwait(false);
					}
				}
				sw.Stop();
				DumpResult("SeqRead10S", total, total / 10, sw.Elapsed);

				sw.Restart();
				for (int i = 0; i < total; i += 10)
				{
					using (var tr = db.BeginReadOnlyTransaction(this.Cancellation))
					{
						int x = i;
						int y = i + 10;
						await tr.GetRangeAsync(
							FdbKeySelector.FirstGreaterOrEqual(Slice.FromString(x.ToString(fmt))), 
							FdbKeySelector.FirstGreaterOrEqual(Slice.FromString(y.ToString(fmt)))
						).ConfigureAwait(false);
					}
				}
				sw.Stop();
				DumpResult("SeqRead10R", total, total / 10, sw.Elapsed);

				sw.Restart();
				for (int i = 0; i < total; i += 100)
				{
					using (var tr = db.BeginReadOnlyTransaction(this.Cancellation))
					{
						await tr.GetValuesAsync(Enumerable.Range(i, 100).Select(x => Slice.FromString(x.ToString(fmt)))).ConfigureAwait(false);
					}
				}
				sw.Stop();
				DumpResult("SeqRead100", total, total / 100, sw.Elapsed);

				sw.Restart();
				for (int i = 0; i < total; i += 100)
				{
					using (var tr = db.BeginReadOnlyTransaction(this.Cancellation))
					{
						await tr.Snapshot.GetValuesAsync(Enumerable.Range(i, 100).Select(x => Slice.FromString(x.ToString(fmt)))).ConfigureAwait(false);
					}
				}
				sw.Stop();
				DumpResult("SeqRead100S", total, total / 100, sw.Elapsed);

				sw.Restart();
				for (int i = 0; i < total; i += 100)
				{
					using (var tr = db.BeginReadOnlyTransaction(this.Cancellation))
					{
						int x = i;
						int y = i + 100;
						await tr.GetRangeAsync(
							FdbKeySelector.FirstGreaterOrEqual(Slice.FromString(x.ToString(fmt))),
							FdbKeySelector.FirstGreaterOrEqual(Slice.FromString(y.ToString(fmt)))
						).ConfigureAwait(false);
					}
				}
				sw.Stop();
				DumpResult("SeqRead100R", total, total / 100, sw.Elapsed);

				sw.Restart();
				for (int i = 0; i < total; i += 100)
				{
					using (var tr = db.BeginReadOnlyTransaction(this.Cancellation))
					{
						int x = i;
						int y = i + 100;
						await tr.Snapshot.GetRangeAsync(
							FdbKeySelector.FirstGreaterOrEqual(Slice.FromString(x.ToString(fmt))),
							FdbKeySelector.FirstGreaterOrEqual(Slice.FromString(y.ToString(fmt)))
						).ConfigureAwait(false);
					}
				}
				sw.Stop();
				DumpResult("SeqRead100RS", total, total / 100, sw.Elapsed);

				sw.Restart();
				for (int i = 0; i < total; i += 1000)
				{
					using (var tr = db.BeginReadOnlyTransaction(this.Cancellation))
					{
						await tr.GetValuesAsync(Enumerable.Range(i, 1000).Select(x => Slice.FromString(x.ToString(fmt)))).ConfigureAwait(false);
					}
				}
				sw.Stop();
				DumpResult("SeqRead1k", total, total / 1000, sw.Elapsed);

				#endregion

				DumpMemory();

				#region random reads

				//sw.Restart();
				//for (int i = 0; i < total; i++)
				//{
				//	using (var tr = db.BeginReadOnlyTransaction())
				//	{
				//		int x = rnd.Next((int)total);
				//		await tr.GetAsync(Slice.FromString(x.ToString(fmt)));
				//	}
				//}
				//sw.Stop();
				//Log("RndRead1   : " + total.ToString("N0") + " keys in " + sw.Elapsed.TotalSeconds.ToString("N3") + " sec => " + (total / sw.Elapsed.TotalSeconds).ToString("N0") + " kps");

				sw.Restart();
				for (int i = 0; i < total; i += 10)
				{
					using (var tr = db.BeginReadOnlyTransaction(this.Cancellation))
					{
						await tr.GetValuesAsync(Enumerable.Range(i, 10).Select(x => Slice.FromString(rnd.Next((int)total).ToString(fmt)))).ConfigureAwait(false);
					}

				}
				sw.Stop();
				//Log("RndRead10  : " + total.ToString("N0") + " keys in " + sw.Elapsed.TotalSeconds.ToString("N3") + " sec => " + (total / sw.Elapsed.TotalSeconds).ToString("N0") + " kps, " + (total / (10 * sw.Elapsed.TotalSeconds)).ToString("N0") + " tps");
				DumpResult("RndRead10", total, total / 10, sw.Elapsed);

				sw.Restart();
				for (int i = 0; i < total; i += 10)
				{
					using (var tr = db.BeginReadOnlyTransaction(this.Cancellation))
					{
						await tr.Snapshot.GetValuesAsync(Enumerable.Range(i, 10).Select(x => Slice.FromString(rnd.Next((int)total).ToString(fmt)))).ConfigureAwait(false);
					}

				}
				sw.Stop();
				//Log("RndRead10S : " + total.ToString("N0") + " keys in " + sw.Elapsed.TotalSeconds.ToString("N3") + " sec => " + (total / sw.Elapsed.TotalSeconds).ToString("N0") + " kps, " + (total / (10 * sw.Elapsed.TotalSeconds)).ToString("N0") + " tps");
				DumpResult("RndRead10S", total, total / 10, sw.Elapsed);

				sw.Restart();
				for (int i = 0; i < total; i += 10)
				{
					using (var tr = db.BeginReadOnlyTransaction(this.Cancellation))
					{
						int x = rnd.Next((int)total - 10);
						int y = x + 10;
						await tr.GetRangeAsync(
							FdbKeySelector.FirstGreaterOrEqual(Slice.FromString(x.ToString(fmt))),
							FdbKeySelector.FirstGreaterOrEqual(Slice.FromString(y.ToString(fmt)))
						).ConfigureAwait(false);
					}

				}
				sw.Stop();
				//Log("RndRead10R : " + total.ToString("N0") + " keys in " + sw.Elapsed.TotalSeconds.ToString("N3") + " sec => " + (total / sw.Elapsed.TotalSeconds).ToString("N0") + " kps, " + (total / (10 * sw.Elapsed.TotalSeconds)).ToString("N0") + " tps");
				DumpResult("RndRead10R", total, total / 10, sw.Elapsed);

				sw.Restart();
				for (int i = 0; i < total; i += 100)
				{
					using (var tr = db.BeginReadOnlyTransaction(this.Cancellation))
					{
						await tr.GetValuesAsync(Enumerable.Range(i, 100).Select(x => Slice.FromString(rnd.Next((int)total).ToString(fmt)))).ConfigureAwait(false);
					}

				}
				sw.Stop();
				//Log("RndRead100 : " + total.ToString("N0") + " keys in " + sw.Elapsed.TotalSeconds.ToString("N3") + " sec => " + (total / sw.Elapsed.TotalSeconds).ToString("N0") + " kps, " + (total / (100 * sw.Elapsed.TotalSeconds)).ToString("N0") + " tps");
				DumpResult("RndRead100", total, total / 100, sw.Elapsed);

				sw.Restart();
				for (int i = 0; i < total; i += 1000)
				{
					using (var tr = db.BeginReadOnlyTransaction(this.Cancellation))
					{
						await tr.GetValuesAsync(Enumerable.Range(i, 1000).Select(x => Slice.FromString(rnd.Next((int)total).ToString(fmt)))).ConfigureAwait(false);
					}

				}
				sw.Stop();
				//Log("RndRead1k  : " + total.ToString("N0") + " keys in " + sw.Elapsed.TotalSeconds.ToString("N3") + " sec => " + (total / sw.Elapsed.TotalSeconds).ToString("N0") + " kps, " + (total / (1000 * sw.Elapsed.TotalSeconds)).ToString("N0") + " tps");
				DumpResult("RndRead1k", total, total / 1000, sw.Elapsed);

				#endregion

				DumpMemory();

				#region Parallel Reads...

				int CPUS = Environment.ProcessorCount;

				long read = 0;
				var mre = new ManualResetEvent(false);
				var tasks = Enumerable
					.Range(0, CPUS)
					.Select(k => Task.Run(async () =>
					{
						var rndz = new Random(k);
						mre.WaitOne();

						int keys = 0;
						for (int j = 0; j < 20; j++)
						{
							for (int i = 0; i < total / CPUS; i += 100)
							{
								int pp = i;// rndz.Next((int)total - 10);
								using (var tr = db.BeginReadOnlyTransaction(this.Cancellation))
								{
									var res = await tr.GetValuesAsync(Enumerable.Range(i, 100).Select(x => Slice.FromString((pp + x).ToString(fmt)))).ConfigureAwait(false);
									keys += res.Length;
								}
							}
						}
						Interlocked.Add(ref read, keys);
						return keys;
					})).ToArray();

				sw.Restart();
				mre.Set();
				await Task.WhenAll(tasks);
				sw.Stop();
				mre.Dispose();
				//Log("ParaSeqRead: " + read.ToString("N0") + " keys in " + sw.Elapsed.TotalSeconds.ToString("N3") + " sec => " + (read / sw.Elapsed.TotalSeconds).ToString("N0") + " kps");
				DumpResult("ParaSeqRead", read, read / 100, sw.Elapsed);

				read = 0;
				mre = new ManualResetEvent(false);
				tasks = Enumerable
					.Range(0, CPUS)
					.Select(k => Task.Run(async () =>
					{
						var rndz = new Random(k);
						mre.WaitOne();

						int keys = 0;
						for (int j = 0; j < 20; j++)
						{
							for (int i = 0; i < total / CPUS; i += 100)
							{
								int pp = i;// rndz.Next((int)total - 100);
								using (var tr = db.BeginReadOnlyTransaction(this.Cancellation))
								{
									var res = await tr.GetRangeAsync(
										FdbKeySelector.FirstGreaterOrEqual(Slice.FromString(pp.ToString(fmt))),
										FdbKeySelector.FirstGreaterOrEqual(Slice.FromString((pp + 100).ToString(fmt)))
									).ConfigureAwait(false);

									keys += res.Count;
								}
							}
						}
						Interlocked.Add(ref read, keys);
						return keys;
					})).ToArray();

				sw.Restart();
				mre.Set();
				await Task.WhenAll(tasks);
				sw.Stop();
				mre.Dispose();
				DumpResult("ParaSeqRange", read, read / 100, sw.Elapsed);
				#endregion

				DumpMemory();

			}

		}

	}
}
