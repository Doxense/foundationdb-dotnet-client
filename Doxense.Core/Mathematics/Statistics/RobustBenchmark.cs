#region Copyright (c) 2023-2024 SnowBank SAS, (c) 2005-2023 Doxense SAS
// All rights reserved.
// 
// Redistribution and use in source and binary forms, with or without
// modification, are permitted provided that the following conditions are met:
// 	* Redistributions of source code must retain the above copyright
// 	  notice, this list of conditions and the following disclaimer.
// 	* Redistributions in binary form must reproduce the above copyright
// 	  notice, this list of conditions and the following disclaimer in the
// 	  documentation and/or other materials provided with the distribution.
// 	* Neither the name of SnowBank nor the
// 	  names of its contributors may be used to endorse or promote products
// 	  derived from this software without specific prior written permission.
// 
// THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS "AS IS" AND
// ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED
// WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE
// DISCLAIMED. IN NO EVENT SHALL SNOWBANK SAS BE LIABLE FOR ANY
// DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES
// (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES;
// LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND
// ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT
// (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS
// SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
#endregion

namespace Doxense.Mathematics.Statistics
{
	using System;
	using System.Linq;
	using Doxense.Runtime;

	[PublicAPI]
	public static class RobustBenchmark
	{

		public static void Warmup()
		{
			PlatformHelpers.PreJit(typeof(RobustBenchmark));
		}

		[DebuggerDisplay("Index={Index}, {Rejected?\"BAD \":\"GOOD \"}, Duration={Duration}, Iterations={Iterations}, Result={Result}")]
		[PublicAPI]
		public sealed class RunData<TResult>
		{
			public int Index { get; set; }
			public long Iterations { get; set; }
			public TimeSpan Duration { get; set; }
			public bool Rejected { get; set; }
			public required TResult Result { get; set; }
		}

		[DebuggerDisplay("R={NumberOfRuns}, ITER={IterationsPerRun}, DUR={TotalDuration}, BIPS={BestIterationsPerSecond}, NANOS={BestIterationsNanos}")]
		[PublicAPI]
		public sealed class Report<TResult>
		{

			public int NumberOfRuns { get; set; }

			public int IterationsPerRun { get; set; }

			public TimeSpan RawTotal { get; set; }

			public required List<TimeSpan> RawTimes { get; set; }

			public required List<TResult> Results { get; set; }

			public List<RunData<TResult>>? Runs { get; set; }

			public int RejectedRuns { get; set; }

			public List<TimeSpan>? Times { get; set; }

			public long TotalIterations { get; set; }

			public TimeSpan TotalDuration { get; set;  }

			public TimeSpan AverageDuration { get; set; }

			public TimeSpan MedianDuration { get; set; }

			public double MedianIterationsPerSecond { get; set; }

			public double MedianIterationsNanos { get; set; }

			public TimeSpan BestDuration { get; set; }

			public double BestIterationsPerSecond { get; set; }

			public double BestIterationsNanos { get; set; }

			public TimeSpan StdDevDuration { get; set; }

			public double StdDevIterationNanos { get; set; }

			// ReSharper disable InconsistentNaming

			public long GcAllocatedOnThread { get; set; }

			/// <summary>Number of gen0 collection per 1 million iterations</summary>
			public double GC0 { get; set; }

			/// <summary>Number of gen2 collection per 1 million iterations</summary>
			public double GC1 { get; set; }

			/// <summary>Number of gen2 collection per 1 million iterations</summary>
			public double GC2 { get; set; }

			// ReSharper restore InconsistentNaming

			public TimeSpan BestRunTotalTime { get; set; }

			public RobustHistogram? Histogram { get; set; }

		}

		public static Report<long> Run(Action test, int runs, int iterations, RobustHistogram? histo = null)
		{
			return Run(
				global: test,
				setup: (_) => (long) iterations,
				test: static (g, _, _) =>
				{
					g();
					return 0;
				},
				cleanup: (_, _) => (long) iterations,
				runs,
				iterations,
				histo
			);
		}

		public static Report<long> Run<T>(Func<T> test, int runs, int iterations, RobustHistogram? histo = null)
		{
			return Run(
				global: test,
				setup: (_) => (long) iterations,
				test: static (g, _, _) => g(),
				cleanup: (_, _) => (long) iterations,
				runs,
				iterations,
				histo
			);
		}

		public static Report<long> Run(Action<int> test, int runs, int iterations, RobustHistogram? histo = null)
		{
			return Run(
				global: test,
				setup: (_) => (long) iterations,
				test: static (g, _, i) =>
				{
					g(i);
					return i;
				},
				cleanup: static (_, s) => s,
				runs,
				iterations,
				histo
			);
		}

		public static Report<TResult> Run<TState, TResult>(TState state, Action<TState> test, Func<TState, TResult> cleanup, int runs, int iterations, RobustHistogram? histo = null)
		{
			return Run(
				global: (state, test, cleanup),
				setup: static (g) => g.state,
				test: static (g, s, i) =>
				{
					g.test(s);
					return i;
				},
				cleanup: static (g, s) => g.cleanup(s),
				runs,
				iterations,
				histo
			);
		}

		public static Report<TResult> Run<TState, TResult>(TState state, Action<TState, int> test, Func<TState, TResult> cleanup, int runs, int iterations, RobustHistogram? histo = null)
		{
			return Run(
				global: (State: state, Test: test, Cleanup: cleanup),
				setup: static (g) => g.State,
				test: static (g, s, i) =>
				{
					g.Test(s, i);
					return s;
				},
				cleanup: static (g, s) => g.Cleanup(s),
				runs,
				iterations,
				histo
			);
		}

		public static Report<TResult> Run<TState, TResult, TIntermediate>(TState state, Func<TState, int, TIntermediate> test, Func<TState, TResult> cleanup, int runs, int iterations, RobustHistogram? histo = null)
		{
			return Run(
				global: (State: state, Test: test, Cleanup: cleanup),
				setup: static (g) => g.State,
				test: static (g, s, i) => g.Test(s, i),
				cleanup: static (g, s) => g.Cleanup(s),
				runs,
				iterations,
				histo
			);
		}

		private static TimeSpan GetElapsedTime(long startingTimestamp)
			=> GetElapsedTime(startingTimestamp, Stopwatch.GetTimestamp());

		private static TimeSpan GetElapsedTime(long startingTimestamp, long endingTimestamp)
#if NET8_0_OR_GREATER
			=> Stopwatch.GetElapsedTime(startingTimestamp, endingTimestamp);
#else
			=> new TimeSpan((long) ((endingTimestamp - startingTimestamp) * s_tickFrequency));

		private static readonly double s_tickFrequency = (double) TimeSpan.TicksPerSecond / Stopwatch.Frequency;
#endif

		public static Report<TResult> Run<TGlobal, TState, TResult, TIntermediate>(TGlobal global, Func<TGlobal, TState> setup, Func<TGlobal, TState, int, TIntermediate> test, Func<TGlobal, TState, TResult> cleanup, int runs, int iterations, RobustHistogram? histo = null)
		{
			var times = new List<TimeSpan>(runs);
			var results = new List<TResult>(runs);

			// first call to ensure JIT has already done at least Tier 0
			long startTimestamp = Stopwatch.GetTimestamp();
			_ = GetElapsedTime(startTimestamp);
			
			TimeSpan bestRunTotalTime = TimeSpan.MaxValue;

			int totalGc0 = 0;
			int totalGc1 = 0;
			int totalGc2 = 0;

			long totalAllocatedOnThread = 0;

			var totalElapsed = TimeSpan.Zero;
			startTimestamp = Stopwatch.GetTimestamp();

			// note: le premier run est toujours ignoré !
			for (int k = -1; k < runs; k++)
			{
				long runStart = Stopwatch.GetTimestamp();
				long ts = runStart;

				var state = setup(global);

				var gcCount0 = GC.CollectionCount(0);
				var gcCount1 = GC.CollectionCount(1);
				var gcCount2 = GC.CollectionCount(2);
				long allocatedAtStart = GC.GetAllocatedBytesForCurrentThread();

				var overhead = GetElapsedTime(ts);
				long iterStart = Stopwatch.GetTimestamp();
				if (histo != null)
				{
					for (int i = 0; i < iterations; i++)
					{

						ts = Stopwatch.GetTimestamp();
						test(global, state, i);
						var testElapsed = GetElapsedTime(ts);

						histo.Add(testElapsed);
						overhead += GetElapsedTime(ts) - testElapsed;
					}
				}
				else
				{
					for (int i = 0; i < iterations; i++)
					{
						test(global, state, i);
					}
				}
				var iterElapsed = GetElapsedTime(iterStart);

				long allocatedOnThread = GC.GetAllocatedBytesForCurrentThread() - allocatedAtStart;

				if (k >= 0)
				{
					totalGc0 += GC.CollectionCount(0) - gcCount0;
					totalGc1 += GC.CollectionCount(1) - gcCount1;
					totalGc2 += GC.CollectionCount(2) - gcCount2;
					totalAllocatedOnThread += allocatedOnThread;
				}

				var result = cleanup(global, state);
				totalElapsed += iterElapsed;

				if (k >= 0)
				{
					var t = iterElapsed - overhead;
					if (t.Ticks < 1) t = new TimeSpan(1);
					times.Add(iterElapsed - overhead);
					results.Add(result);
				}
			}

			var report = new Report<TResult>
			{
				NumberOfRuns = runs,
				IterationsPerRun = iterations,
				Results = results,
				RawTimes = times,
				RawTotal = GetElapsedTime(startTimestamp),
				BestRunTotalTime = bestRunTotalTime,
				Histogram = histo,
				GcAllocatedOnThread = totalAllocatedOnThread,
				GC0 = totalGc0, //(totalGC0 * 1000000.0) / (runs * iterations),
				GC1 = totalGc1, //(totalGC1 * 1000000.0) / (runs * iterations),
				GC2 = totalGc2, //(totalGC2 * 1000000.0) / (runs * iterations),
			};

			var filtered = PeirceCriterion.FilterOutliers(times, x => (double) x.Ticks, out var outliers);

			var outliersMap = outliers.ToArray();

			report.Times = filtered;
			report.RejectedRuns = outliers.Count;
			report.Runs = times.Select((ticks, i) => new RunData<TResult>
			{
				Index = i,
				Iterations = iterations,
				Duration = ticks,
				Rejected = outliersMap.Contains(i),
				Result = results[i],
			}).ToList();

			report.TotalDuration = TimeSpan.FromTicks(filtered.Sum(x => x.Ticks));
			report.TotalIterations = (long) iterations * filtered.Count;

			// medianne
			var sorted = filtered.ToArray();
			Array.Sort(sorted);
			report.BestDuration = times.Min();
			report.AverageDuration = TimeSpan.FromTicks((long) sorted.Average(x => (double) x.Ticks));
			var median = Median(sorted);
			report.MedianDuration = median;
			report.StdDevDuration = MeanAbsoluteDeviation(sorted, median);

			report.MedianIterationsPerSecond = report.TotalIterations / report.TotalDuration.TotalSeconds;
			report.MedianIterationsNanos = (double) (report.TotalDuration.Ticks * 100) / report.TotalIterations;

			report.BestIterationsPerSecond = report.IterationsPerRun / report.BestDuration.TotalSeconds;
			report.BestIterationsNanos = (double) (report.BestDuration.Ticks * 100) / report.IterationsPerRun;

			report.StdDevIterationNanos = (double) (report.StdDevDuration.Ticks * 100) / report.TotalIterations;

			return report;
		}

		private static TimeSpan Median(TimeSpan[] sortedData)
		{
			int n = sortedData.Length;
			return n == 0 ? default : (n & 1) == 1 ? sortedData[n >> 1] : TimeSpan.FromTicks((sortedData[n >> 1].Ticks + sortedData[(n >> 1) - 1].Ticks) >> 1);
		}

		private static TimeSpan MeanAbsoluteDeviation(TimeSpan[] sortedData, TimeSpan med)
		{
			// calcule la variance
			// NOTE: on la calcule par rpt au median, *PAS* la moyenne arithmétique !
			long sum = 0;
			foreach (var data in sortedData)
			{
				var x = (data - med).Ticks;
				sum += x * x;
			}

			var variance = sortedData.Length == 0 ? 0.0 : ((double) sum / sortedData.Length);
			return System.TimeSpan.FromTicks((long) Math.Sqrt(variance));
		}

	}

}
