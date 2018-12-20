﻿//TODO: License for samples/tutorials ???

namespace FoundationDB.Samples.Benchmarks
{
	using System;
	using System.Diagnostics;
	using System.Globalization;
	using System.IO;
	using System.Linq;
	using System.Threading;
	using System.Threading.Tasks;
	using Doxense.Collections.Tuples;
	using Doxense.Mathematics.Statistics;
	using FoundationDB.Client;

	public class BenchRunner : IAsyncTest
	{

		public enum BenchMode
		{
			GetReadVersion,
			Set,
			Get,
			Get10,
			GetRange,
			Watch,
			Mix,
		}

		public BenchRunner(BenchMode mode, int value = 1)
		{
			this.Mode = mode;
			this.Value = value;
			this.Histo = new RobustHistogram();
		}

		public string Name => "Bench" + this.Mode.ToString();

		public int Value { get; set; }

		public BenchMode Mode { get; }

		public IDynamicKeySubspace Subspace { get; private set; }

		public RobustHistogram Histo { get; }


		/// <summary>
		/// Setup the initial state of the database
		/// </summary>
		public async Task Init(IFdbDatabase db, CancellationToken ct)
		{
			// open the folder where we will store everything
			this.Subspace = await db.ReadWriteAsync(tr => db.Directory.CreateOrOpenAsync(tr, "Benchmarks"), ct);
		}

		public async Task Run(IFdbDatabase db, TextWriter log, CancellationToken ct)
		{
			const int WORKERS = 1;
			const int RUN_IN_SECONDS = 100;

			await Init(db, ct);
			log.WriteLine("Initialized for " + this.Mode.ToString());

			var timeline = new RobustTimeLine(
				TimeSpan.FromSeconds(1),
				RobustHistogram.TimeScale.Milliseconds,
				(histo, idx) =>
				{
					if (idx == 0)
					{
						Console.WriteLine("T+s | " + RobustHistogram.GetDistributionScale(RobustHistogram.HorizontalScale, 1, 5000 - 1) + " | ");
					}
					Console.WriteLine(String.Format(CultureInfo.InvariantCulture, "{0,3} | {1} | {2,6:#,##0.0} ms (+/- {3:#0.000})", idx, histo.GetDistribution(1, 5000 - 1), histo.Median, histo.MedianAbsoluteDeviation()));
					if (log != Console.Out) log.WriteLine(histo.GetReport(false));
					return false;
				}
			);

			var duration = Stopwatch.StartNew();

			var foo = this.Subspace.Keys.Encode("foo");
			var bar = Slice.FromString("bar");
			var barf = Slice.FromString("barf");

			long total = 0;

			timeline.Start();
			var elapsed = await Program.RunConcurrentWorkersAsync(
				WORKERS,
				async (i, _ct) =>
				{
					var dur = Stopwatch.StartNew();
					int k = 0;
					while (dur.Elapsed.TotalSeconds < RUN_IN_SECONDS)
					{
						var sw = Stopwatch.StartNew();
						switch(this.Mode)
						{
							case BenchMode.GetReadVersion:
							{
								await db.ReadAsync(tr => tr.GetReadVersionAsync(), ct);
								break;
							}
							case BenchMode.Get:
							{
								if (this.Value <= 1)
								{
									await db.ReadAsync(tr => tr.GetAsync(foo), ct);
								}
								else
								{
									var foos = TuPack.EncodePrefixedKeys(foo, Enumerable.Range(1, this.Value).ToArray());
									await db.ReadAsync(tr => tr.GetValuesAsync(foos), ct);
								}
								break;
							}
							case BenchMode.Set:
							{
								await db.WriteAsync(tr => tr.Set(foo, bar), ct);
								break;
							}
							case BenchMode.Watch:
							{
								(var v, var w) = await db.ReadWriteAsync(async tr => (await tr.GetAsync(foo), tr.Watch(foo, ct)), ct);

								// swap
								v = (v == bar) ? barf : bar;

								await db.WriteAsync((tr) => tr.Set(foo, v), ct);

								await w;

								break;
							}
						}
						sw.Stop();

						timeline.Add(sw.Elapsed.TotalMilliseconds);
						Console.Write(k.ToString() + "\r");

						++k;
						Interlocked.Increment(ref total);
					}
				},
				ct
			);
			timeline.Stop();
			Console.WriteLine("Done       ");
			Console.WriteLine("# Ran {0} transactions in {1:0.0##} sec", total, elapsed.TotalSeconds);

			var global = timeline.MergeResults();

			log.WriteLine("# Merged results:");
			log.WriteLine(global.GetReport(true));

			if (log != Console.Out)
			{
				Console.WriteLine("# Merged results:");
				Console.WriteLine(global.GetReport(true));
			}
		}

	}
}
