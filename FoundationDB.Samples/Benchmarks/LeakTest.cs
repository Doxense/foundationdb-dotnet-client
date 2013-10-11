//TODO: License for samples/tutorials ???

namespace FoundationDB.Samples.Tutorials
{
	using FoundationDB.Client;
	using FoundationDB.Client.Native;
	using FoundationDB.Layers.Directories;
	using FoundationDB.Layers.Tuples;
	using FoundationDB.Linq;
	using System;
	using System.Collections.Generic;
	using System.Diagnostics;
	using System.Globalization;
	using System.Linq;
	using System.Text;
	using System.Threading;
	using System.Threading.Tasks;

	public class LeakTest : IAsyncTest
	{

		public LeakTest(int k, int m, int n, TimeSpan delay)
		{
			this.K = k;
			this.M = m;
			this.N = n;
			this.Delay = delay;
		}

		public int K { get; private set; }
		public int M { get; private set; }
		public int N { get; private set; }
		public TimeSpan Delay { get; private set; }

		public FdbSubspace Subspace { get; private set; }

		/// <summary>
		/// Setup the initial state of the database
		/// </summary>
		public async Task Init(FdbDatabasePartition db, CancellationToken ct)
		{
			// open the folder where we will store everything
			this.Subspace = await db.CreateOrOpenDirectoryAsync(FdbTuple.Create("Benchmarks", "LeakTest"), cancellationToken: ct);

			// clear all previous values
			await db.ClearRangeAsync(this.Subspace, ct);

			// insert all the classes
			await db.WriteAsync((tr) =>
			{
				tr.Set(this.Subspace.Concat(FdbKey.MinValue), Slice.FromString("BEGIN"));
				tr.Set(this.Subspace.Concat(FdbKey.MaxValue), Slice.FromString("END"));
			}, ct);
		}

		/// <summary>
		/// Simulate a student that is really indecisive
		/// </summary>
		public async Task RunWorker(IFdbDatabase db, int id,  CancellationToken ct)
		{
			string student = "WORKER" + id.ToString("D04");

			var rnd = new Random(id * 7);
			var values = new string[this.M];
			for (int i = 0; i < values.Length;i++)
			{
				values[i] = "initial_value_" + rnd.Next();
			}

			var prefix = this.Subspace.Partition(student);

			for (int i = 0; i < 1/*this.N*/ && !ct.IsCancellationRequested; i++)
			{
				// randomly mutate values
				var n = rnd.Next(values.Length / 2);
				for (int j = 0; j < n;j++)
				{
					values[rnd.Next(values.Length)] = "value_" + i.ToString() + "_" + rnd.Next().ToString();
				}

				long now = DateTime.Now.Ticks / TimeSpan.TicksPerMillisecond;
				// write everything


				await db.WriteAsync((tr) =>
				{
					if (tr.Context.Retries > 0) Console.Write("!");
					for (int j = 0; j < values.Length; j++)
					{
						tr.Set(prefix.Pack(j, now), Slice.FromString(values[j] + new string('A', 100)));
					}
				}, ct);
				Console.Write(".");

				//var r = await db.ReadAsync(async (tr) =>
				//{
				//	if (tr.Context.Retries > 0) Console.Write("!");
				//	return await Task.WhenAll(Enumerable.Range(0, values.Length).Select(x => tr.GetRange(FdbKeyRange.StartsWith(prefix.Pack(x))).LastOrDefaultAsync()));
				//}, ct);
				//if (i % 10 == 0) Console.Write(":");

				//await Task.Delay(this.Delay);
			}

			ct.ThrowIfCancellationRequested();

		}

		#region IAsyncTest...

		public string Name { get { return "LeakTest"; } }

		public async Task Run(FdbDatabasePartition db, CancellationToken ct)
		{
			await Init(db, ct);
			Console.WriteLine("Initialized");

			ThreadPool.SetMinThreads(100, 100);

			DateTime start = DateTime.Now;
			DateTime last = start;
			long workingSet = Environment.WorkingSet;
			long totalMemory = GC.GetTotalMemory(false);

			TimerCallback timerHandler = (_) =>
			{
				var now = DateTime.Now;
				long ws = Environment.WorkingSet;
				long tm = GC.GetTotalMemory(false);
				var sb = new StringBuilder();
				sb.AppendLine();
				sb.AppendLine("T+" + (now - start).TotalSeconds.ToString("N1") + "s : WS=" + ws.ToString("N0") + " bytes (" + (ws - workingSet).ToString("N0") + "), NM=" + tm.ToString("N0") + " bytes (" + (tm - totalMemory).ToString("N0") + ")");
				sb.AppendLine("  trans: " + DebugCounters.TransactionHandlesTotal.ToString("N0") + " (" + DebugCounters.TransactionHandles + "), futures: " + DebugCounters.FutureHandlesTotal.ToString("N0") + " (" + DebugCounters.FutureHandles + "), callbacks: " + DebugCounters.CallbackHandlesTotal.ToString("N0") + "(" + DebugCounters.CallbackHandles + ")");
				sb.AppendLine("  cpu: " + PerfCounters.ProcessorTime.NextValue().ToString("N1") + "%, private: " + PerfCounters.PrivateBytes.NextValue().ToString("N0") + ", gen0: " + PerfCounters.ClrGen0Collections.NextValue() + ", gen1: " + PerfCounters.ClrGen1Collections.NextValue() + ", gen2: " + PerfCounters.ClrGen2Collections.NextValue());
				Console.Write(sb.ToString());
				workingSet = ws;
				totalMemory = tm;
				/*
				if (now - last >= TimeSpan.FromMinutes(1))
				{
					Console.Write("GC...");
					GC.Collect();
					GC.WaitForPendingFinalizers();
					GC.Collect();
					Console.WriteLine(" Done");
					last = now;
				}
				 */
			};

			timerHandler(null);

			using (var timer = new Timer(timerHandler, null, TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(5)))
			{
				for (int k = 0; k < this.N; k++)
				{
					// run multiple students
					var elapsed = await TestRunner.RunConcurrentWorkersAsync(
						K,
						(i, _ct) => RunWorker(db, i, _ct),
						ct
					);

					Console.WriteLine();
					Console.WriteLine("Ran {0} workers in {1:0.0##} sec", this.K, elapsed.TotalSeconds);

					await Task.Delay(TimeSpan.FromSeconds(21));
				}

			}

			timerHandler(null);
		}

		#endregion

	}
}
