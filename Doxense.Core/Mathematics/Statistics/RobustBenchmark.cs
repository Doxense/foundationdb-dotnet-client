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
	using System.Collections.Generic;
	using System.Diagnostics;
	using System.Linq;
	using Doxense.Runtime;
	using JetBrains.Annotations;

	[PublicAPI]
	public static class RobustBenchmark
	{

		public static void Warmup()
		{
			PlatformHelpers.PreJit(typeof(RobustBenchmark));
		}

		[DebuggerDisplay("Index={Index}, {Rejected?\"BAD \":\"GOOD \"}, Duration={Duration}, Iterations={Iterations}, Result={Result}")]
		public sealed class RunData<TResult>
		{
			public int Index { get; set; }
			public long Iterations { get; set; }
			public TimeSpan Duration { get; set; }
			public bool Rejected { get; set; }
			public required TResult Result { get; set; }
		}

		[DebuggerDisplay("R={NumberOfRuns}, ITER={IterationsPerRun}, DUR={TotalDuration}, BIPS={BestIterationsPerSecond}, NANOS={BestIterationsNanos}")]
		public sealed class Report<TResult>
		{
			public int NumberOfRuns { get; set; }
			public int IterationsPerRun { get; set; }

			public TimeSpan RawTotal { get; set; }

			public required IList<long> RawTimes { get; set; }

			public required IList<TResult> Results { get; set; }

			public IList<RunData<TResult>>? Runs { get; set; }

			public int RejectedRuns { get; set; }

			public IList<TimeSpan>? Times { get; set; }

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

			/// <summary>Number of gen0 collection per 1 million iterations</summary>
			public double GC0 { get; set; }
			/// <summary>Number of gen2 collection per 1 million iterations</summary>
			public double GC1 { get; set; }
			/// <summary>Number of gen2 collection per 1 million iterations</summary>
			public double GC2 { get; set; }

			public TimeSpan BestRunTotalTime { get; set; }

			public RobustHistogram? Histogram { get; set; }
		}

		private struct RobustStopWatch
		{
			/// <summary>Conversion ratio from timestamp ticks to TimeSpan ticks</summary>
			private static readonly double TicksFrequency = (double)TimeSpan.TicksPerSecond / Stopwatch.Frequency;

			/// <summary>Minimum number of TimeSpan ticks that can accurately be measured</summary>
			public static readonly long EpsilonDuration = (long)TicksFrequency;

			public long StartTimeStamp;
			public long Total;
			public bool IsRunning;

			public void Restart()
			{
				this.StartTimeStamp = Stopwatch.GetTimestamp();
				this.Total = 0;
				this.IsRunning = true;
			}

			public void Start()
			{
				if (!this.IsRunning)
				{
					this.StartTimeStamp = Stopwatch.GetTimestamp();
					this.IsRunning = true;
				}
			}

			public void Stop()
			{
				if (this.IsRunning)
				{
					Total += Stopwatch.GetTimestamp() - this.StartTimeStamp;
					if (Total < 0L) Total = 0;
					IsRunning = false;
				}
			}

			public long ElapsedRawTicks => (this.IsRunning ? (Stopwatch.GetTimestamp() - this.StartTimeStamp) : 0) + this.Total;

			public TimeSpan Elapsed => GetDuration(this.ElapsedRawTicks);

			public long ElapsedTicks => (long)Math.Round(this.ElapsedRawTicks * TicksFrequency, MidpointRounding.AwayFromZero);

			public double ElapsedMicroseconds => this.ElapsedRawTicks * TicksFrequency * 0.1d;

			public double ElapsedMilliseconds => this.ElapsedRawTicks * TicksFrequency * 0.0001d;

			public double ElapsedSeconds => this.ElapsedRawTicks * TicksFrequency * 0.0000001d;

			public static TimeSpan GetDuration(long ticks) => TimeSpan.FromTicks((long)Math.Round(ticks * TicksFrequency, MidpointRounding.AwayFromZero));

		}

		public static Report<long> Run(Action<int> test, int runs, int iterations, RobustHistogram? histo = null)
		{
			return Run<int, long>(
				() => iterations,
				(_, i) => test(i),
				(_) => iterations,
				runs,
				iterations,
				histo
			);
		}

		public static Report<TResult> Run<TState, TResult>(Func<TState> setup, Action<TState, int> test, Func<TState, TResult> cleanup, int runs, int iterations, RobustHistogram? histo = null)
		{
			var times = new List<long>(runs);
			var results = new List<TResult>(runs);

			var global = Stopwatch.StartNew();
			// note: au cas où ca serait le premier hit sur Stopwatch, on appelle une deuxième fois !
			global.Restart();

			TimeSpan bestRunTotalTime = TimeSpan.MaxValue;
			var totalTimePerRun = new RobustStopWatch();
			var iterTimePerRun = new RobustStopWatch();
			var sw = new RobustStopWatch();

			int totalGC0 = 0;
			int totalGC1 = 0;
			int totalGC2 = 0;

			// note: le premier run est toujours ignoré !
			for (int k = -1; k < runs; k++)
			{
				var swH = new RobustStopWatch();

				totalTimePerRun.Restart();
				var state = setup();

				var gcCount0 = GC.CollectionCount(0);
				var gcCount1 = GC.CollectionCount(1);
				var gcCount2 = GC.CollectionCount(2);

				iterTimePerRun.Restart();
				if (histo != null)
				{
					for (int i = 0; i < iterations; i++)
					{
						sw.Restart();
						test(state, i);
						sw.Stop();
						swH.Start();
						histo.Add(sw.Elapsed);
						swH.Stop();
					}
				}
				else
				{
					for (int i = 0; i < iterations; i++)
					{
						test(state, i);
					}
				}
				iterTimePerRun.Stop();

				if (k >= 0)
				{
					totalGC0 += GC.CollectionCount(0) - gcCount0;
					totalGC1 += GC.CollectionCount(1) - gcCount1;
					totalGC2 += GC.CollectionCount(2) - gcCount2;
				}

				var result = cleanup(state);
				totalTimePerRun.Stop();
				if (totalTimePerRun.Elapsed < bestRunTotalTime) bestRunTotalTime = totalTimePerRun.Elapsed;

				if (k >= 0)
				{
					var t = iterTimePerRun.ElapsedRawTicks - swH.ElapsedRawTicks;
					if (t < RobustStopWatch.EpsilonDuration) t = RobustStopWatch.EpsilonDuration; // minimum !!!
					times.Add(t);
					results.Add(result);
				}
			}

			global.Stop();

			var report = new Report<TResult>
			{
				NumberOfRuns = runs,
				IterationsPerRun = iterations,
				Results = results,
				RawTimes = times,
				RawTotal = global.Elapsed,
				BestRunTotalTime = bestRunTotalTime,
				Histogram = histo,
				GC0 = totalGC0, //(totalGC0 * 1000000.0) / (runs * iterations),
				GC1 = totalGC1, //(totalGC1 * 1000000.0) / (runs * iterations),
				GC2 = totalGC2, //(totalGC2 * 1000000.0) / (runs * iterations),
			};

			var filtered = PeirceCriterion.FilterOutliers(times, x => x, out var _outliers, out var rejected).ToList();
			//var filtered = DixonTest.ComputeOutliers(times, x => (double)x, DixonTest.Confidence.CL95, DixonTest.Mode.Upper, out _outliers, out rejected).ToList();
			var outliers = _outliers.ToArray();

			report.Times = filtered.Select(x => RobustStopWatch.GetDuration(x)).ToList();
			report.RejectedRuns = rejected;
			report.Runs = times.Select((ticks, i) => new RunData<TResult>
			{
				Index = i,
				Iterations = iterations,
				Duration = RobustStopWatch.GetDuration(ticks),
				Rejected = outliers.Contains(i),
				Result = results[i],
			}).ToList();

			report.TotalDuration = RobustStopWatch.GetDuration(filtered.Sum());
			report.TotalIterations = (long)iterations * filtered.Count;

			// medianne
			var sorted = filtered.ToArray();
			Array.Sort(sorted);
			report.BestDuration = RobustStopWatch.GetDuration(times.Min());
			report.AverageDuration = RobustStopWatch.GetDuration((long)sorted.Average());
			long median = Median(sorted);
			report.MedianDuration = RobustStopWatch.GetDuration(median);
			report.StdDevDuration = RobustStopWatch.GetDuration(MAD(sorted, median));
			//Console.WriteLine("Median of " + String.Join(", ", sorted) + " is " + Median(sorted));

			report.MedianIterationsPerSecond = report.TotalIterations / report.TotalDuration.TotalSeconds;
			report.MedianIterationsNanos = (double)(report.TotalDuration.Ticks * 100) / report.TotalIterations;

			report.BestIterationsPerSecond = report.IterationsPerRun / report.BestDuration.TotalSeconds;
			report.BestIterationsNanos = (double)(report.BestDuration.Ticks * 100) / report.IterationsPerRun;

			report.StdDevIterationNanos = (double)(report.StdDevDuration.Ticks * 100)  / report.TotalIterations;

			//Console.WriteLine(Doxense.Serialization.Json.CrystalJson.Serialize(report, Doxense.Serialization.Json.CrystalJsonSettings.JsonIndented));

			return report;
		}

		private static long Median(long[] sortedData)
		{
			int n = sortedData.Length;
			return n == 0 ? 0 : (n & 1) == 1 ? sortedData[n >> 1] : (sortedData[n >> 1] + sortedData[(n >> 1) - 1]) >> 1;
		}

		private static long MAD(long[] sortedData, long med)
		{
			// calcule la variance
			// NOTE: on la calcule par rpt au median, *PAS* la moyenne arithmétique !
			long sum = 0;
			foreach (long data in sortedData)
			{
				long x = data - med;
				sum += x * x;
			}

			double variance = sortedData.Length == 0 ? 0.0 : ((double)sum / sortedData.Length);
			return (long)Math.Sqrt(variance);
		}

	}


}
