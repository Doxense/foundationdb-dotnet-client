//TODO: License for samples/tutorials ???

namespace FoundationDB.Samples.Tutorials
{
	using FoundationDB.Client;
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
				values[i] = "initial_value";
			}

			var prefix = this.Subspace.Partition(student);

			for (int i = 0; i < this.N && !ct.IsCancellationRequested; i++)
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
						tr.Set(prefix.Pack(j, now), Slice.FromString(values[j]));
					}
				}, ct);
				Console.Write(".");

				var r = await db.ReadAsync(async (tr) =>
				{
					if (tr.Context.Retries > 0) Console.Write("!");
					return await Task.WhenAll(Enumerable.Range(0, values.Length).Select(x => tr.GetRange(FdbKeyRange.StartsWith(prefix.Pack(x))).LastOrDefaultAsync()));
				}, ct);
				Console.Write(":");

				await Task.Delay(this.Delay);
			}

			ct.ThrowIfCancellationRequested();

		}

		#region IAsyncTest...

		public string Name { get { return "LeakTest"; } }

		public async Task Run(FdbDatabasePartition db, CancellationToken ct)
		{
			await Init(db, ct);
			Console.WriteLine("Initialized");

			var p = Process.GetCurrentProcess();
			var pcGen0Collections = new PerformanceCounter(".NET CLR Memory", "# Gen 0 Collections", p.ProcessName, true);
			var pcGen1Collections = new PerformanceCounter(".NET CLR Memory", "# Gen 1 Collections", p.ProcessName, true);
			var pcGen2Collections = new PerformanceCounter(".NET CLR Memory", "# Gen 1 Collections", p.ProcessName, true);
			var pcCPU = new PerformanceCounter("Process", "% Processor Time", p.ProcessName, true);
			var pcPrivateBytes = new PerformanceCounter("Process", "Private Bytes", p.ProcessName, true);

			DateTime start = DateTime.Now;
			DateTime last = start;
			long workingSet = Environment.WorkingSet;
			long totalMemory = GC.GetTotalMemory(false);
			using (var timer = new Timer((_) =>
			{
				var now = DateTime.Now;
				Console.WriteLine();
				long ws = Environment.WorkingSet;
				long tm = GC.GetTotalMemory(false);
				Console.WriteLine("T+" + (now - start).TotalSeconds.ToString("N1") + "s : WS=" + ws.ToString("N0") + " bytes (" + (ws - workingSet).ToString("N0") + "), NM=" + tm.ToString("N0") + " bytes (" + (tm - totalMemory).ToString("N0") + ")");
				Console.WriteLine("  cpu: " + pcCPU.NextValue() + "%, private: " + pcPrivateBytes.NextValue().ToString("N0") + ", gen0: " + pcGen0Collections.NextValue() + ", gen1: " + pcGen1Collections.NextValue() + ", gen2: " + pcGen2Collections.NextValue());
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
			}, null, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(10)))
			{

				// run multiple students
				var elapsed = await TestRunner.RunConcurrentWorkersAsync(
					K,
					(i, _ct) => RunWorker(db, i, _ct),
					ct
				);

				Console.WriteLine("Ran {0} workers in {1:0.0##} sec", this.K, elapsed.TotalSeconds);
			}
		}

		#endregion

	}
}
