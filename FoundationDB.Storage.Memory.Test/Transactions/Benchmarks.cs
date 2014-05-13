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
			Console.WriteLine(
				"{0,-12}: {1, 10} keys in {2,4} sec => {3,9} kps, {4,7} tps",
				label,
				total.ToString("N0"),
				elapsed.TotalSeconds.ToString("N3"),
				(total / elapsed.TotalSeconds).ToString("N0"),
				(trans / elapsed.TotalSeconds).ToString("N0")
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
			Console.WriteLine("Total memory: Managed=" + (GC.GetTotalMemory(false) / 1024.0).ToString("N1") + " kB, WorkingSet=" + (Environment.WorkingSet / 1024.0).ToString("N1") + " kB");
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
				await db.WriteAsync((tr) => tr.Set(db.Pack("hello"), Slice.FromString("world")), this.Cancellation);
				Slice.Random(rnd, KEYSIZE);
				Slice.Random(rnd, VALUESIZE);
			}

			Console.WriteLine("Inserting " + KEYSIZE + "-bytes " + (RANDOM ? "random" : "ordered") + " keys / " + VALUESIZE + "-bytes values, in " + T.ToString("N0") + " transactions");

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
				Console.WriteLine("done");
				Console.WriteLine("* Inserted: " + total.ToString("N0") + " keys");
				Console.WriteLine("* Elapsed : " + sw.Elapsed.TotalSeconds.ToString("N3") + " sec");
				Console.WriteLine("* TPS: " + (T / sw.Elapsed.TotalSeconds).ToString("N0") + " transaction/sec");
				Console.WriteLine("* KPS: " + (total / sw.Elapsed.TotalSeconds).ToString("N0") + " key/sec");
				Console.WriteLine("* BPS: " + ((total * (KEYSIZE + VALUESIZE)) / sw.Elapsed.TotalSeconds).ToString("N0") + " byte/sec");

				DumpMemory(collect: true);

				db.Debug_Dump(false);

				DumpResult("WriteSeq" + B, total, total / B, sw.Elapsed);

				Console.WriteLine("Saving ...");
				sw.Restart();
				await db.SaveSnapshotAsync(".\\minibench.pndb");
				sw.Stop();
				Console.WriteLine("* Saved in " + sw.Elapsed.TotalSeconds.ToString("N3") + " sec");

				Console.WriteLine("Warming up reads...");
				var data = await db.GetValuesAsync(Enumerable.Range(0, 100).Select(i => Slice.FromString(i.ToString(fmt))), this.Cancellation);

				Console.WriteLine("Starting read tests...");

				#region sequential reads

				//sw.Restart();
				//for (int i = 0; i < total; i++)
				//{
				//	using (var tr = db.BeginReadOnlyTransaction())
				//	{
				//		await tr.GetAsync(Slice.FromString(i.ToString(fmt)));
				//	}
				//}
				//sw.Stop();
				//Console.WriteLine("SeqRead1   : " + total.ToString("N0") + " keys in " + sw.Elapsed.TotalSeconds.ToString("N3") + " sec => " + (total / sw.Elapsed.TotalSeconds).ToString("N0") + " kps");

				sw.Restart();
				for (int i = 0; i < total; i += 10)
				{
					using (var tr = db.BeginReadOnlyTransaction(this.Cancellation))
					{
						await tr.GetValuesAsync(Enumerable.Range(i, 10).Select(x => Slice.FromString(x.ToString(fmt)))).ConfigureAwait(false);
					}

				}
				sw.Stop();
				//Console.WriteLine("SeqRead10  : " + total.ToString("N0") + " keys in " + sw.Elapsed.TotalSeconds.ToString("N3") + " sec => " + (total / sw.Elapsed.TotalSeconds).ToString("N0") + " kps, " + (total / (10 * sw.Elapsed.TotalSeconds)).ToString("N0") + " tps");
				DumpResult("SeqRead10", total, total / 10, sw.Elapsed);

				sw.Restart();
				for (int i = 0; i < total; i += 10)
				{
					using (var tr = db.BeginReadOnlyTransaction(this.Cancellation))
					{
						await tr.GetValuesAsync(Enumerable.Range(i, 10).Select(x => Slice.FromString(x.ToString(fmt)))).ConfigureAwait(false);
					}

				}
				sw.Stop();
				//Console.WriteLine("SeqRead10S : " + total.ToString("N0") + " keys in " + sw.Elapsed.TotalSeconds.ToString("N3") + " sec => " + (total / sw.Elapsed.TotalSeconds).ToString("N0") + " kps, " + (total / (10 * sw.Elapsed.TotalSeconds)).ToString("N0") + " tps");
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
				//Console.WriteLine("SeqRead10R : " + total.ToString("N0") + " keys in " + sw.Elapsed.TotalSeconds.ToString("N3") + " sec => " + (total / sw.Elapsed.TotalSeconds).ToString("N0") + " kps, " + (total / (10 * sw.Elapsed.TotalSeconds)).ToString("N0") + " tps");
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
				//Console.WriteLine("SeqRead100 : " + total.ToString("N0") + " keys in " + sw.Elapsed.TotalSeconds.ToString("N3") + " sec => " + (total / sw.Elapsed.TotalSeconds).ToString("N0") + " kps, " + (total / (100 * sw.Elapsed.TotalSeconds)).ToString("N0") + " tps");
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
				//Console.WriteLine("SeqRead100S : " + total.ToString("N0") + " keys in " + sw.Elapsed.TotalSeconds.ToString("N3") + " sec => " + (total / sw.Elapsed.TotalSeconds).ToString("N0") + " kps, " + (total / (100 * sw.Elapsed.TotalSeconds)).ToString("N0") + " tps");
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
				//Console.WriteLine("SeqRead100R : " + total.ToString("N0") + " keys in " + sw.Elapsed.TotalSeconds.ToString("N3") + " sec => " + (total / sw.Elapsed.TotalSeconds).ToString("N0") + " kps, " + (total / (100 * sw.Elapsed.TotalSeconds)).ToString("N0") + " tps");
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
				//Console.WriteLine("SeqRead100RS: " + total.ToString("N0") + " keys in " + sw.Elapsed.TotalSeconds.ToString("N3") + " sec => " + (total / sw.Elapsed.TotalSeconds).ToString("N0") + " kps, " + (total / (100 * sw.Elapsed.TotalSeconds)).ToString("N0") + " tps");
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
				//Console.WriteLine("SeqRead1k   : " + total.ToString("N0") + " keys in " + sw.Elapsed.TotalSeconds.ToString("N3") + " sec => " + (total / sw.Elapsed.TotalSeconds).ToString("N0") + " kps, " + (total / (1000 * sw.Elapsed.TotalSeconds)).ToString("N0") + " tps");
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
				//Console.WriteLine("RndRead1   : " + total.ToString("N0") + " keys in " + sw.Elapsed.TotalSeconds.ToString("N3") + " sec => " + (total / sw.Elapsed.TotalSeconds).ToString("N0") + " kps");

				sw.Restart();
				for (int i = 0; i < total; i += 10)
				{
					using (var tr = db.BeginReadOnlyTransaction(this.Cancellation))
					{
						await tr.GetValuesAsync(Enumerable.Range(i, 10).Select(x => Slice.FromString(rnd.Next((int)total).ToString(fmt)))).ConfigureAwait(false);
					}

				}
				sw.Stop();
				//Console.WriteLine("RndRead10  : " + total.ToString("N0") + " keys in " + sw.Elapsed.TotalSeconds.ToString("N3") + " sec => " + (total / sw.Elapsed.TotalSeconds).ToString("N0") + " kps, " + (total / (10 * sw.Elapsed.TotalSeconds)).ToString("N0") + " tps");
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
				//Console.WriteLine("RndRead10S : " + total.ToString("N0") + " keys in " + sw.Elapsed.TotalSeconds.ToString("N3") + " sec => " + (total / sw.Elapsed.TotalSeconds).ToString("N0") + " kps, " + (total / (10 * sw.Elapsed.TotalSeconds)).ToString("N0") + " tps");
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
				//Console.WriteLine("RndRead10R : " + total.ToString("N0") + " keys in " + sw.Elapsed.TotalSeconds.ToString("N3") + " sec => " + (total / sw.Elapsed.TotalSeconds).ToString("N0") + " kps, " + (total / (10 * sw.Elapsed.TotalSeconds)).ToString("N0") + " tps");
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
				//Console.WriteLine("RndRead100 : " + total.ToString("N0") + " keys in " + sw.Elapsed.TotalSeconds.ToString("N3") + " sec => " + (total / sw.Elapsed.TotalSeconds).ToString("N0") + " kps, " + (total / (100 * sw.Elapsed.TotalSeconds)).ToString("N0") + " tps");
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
				//Console.WriteLine("RndRead1k  : " + total.ToString("N0") + " keys in " + sw.Elapsed.TotalSeconds.ToString("N3") + " sec => " + (total / sw.Elapsed.TotalSeconds).ToString("N0") + " kps, " + (total / (1000 * sw.Elapsed.TotalSeconds)).ToString("N0") + " tps");
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
				//Console.WriteLine("ParaSeqRead: " + read.ToString("N0") + " keys in " + sw.Elapsed.TotalSeconds.ToString("N3") + " sec => " + (read / sw.Elapsed.TotalSeconds).ToString("N0") + " kps");
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
