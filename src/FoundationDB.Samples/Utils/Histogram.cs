﻿#region Copyright Attribution
// Copyright (c) 2011 The LevelDB Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file. See the AUTHORS file for names of contributors.

// This is a port and adaptation of the 'histogram.cc' file of LevelDB 1.9. (https://code.google.com/p/leveldb/source/browse/util/histogram.cc)
// > Ported from C++ to C#
// > Added more buckets for smaller precisions (may break the Standard Deviation)
// > Added timescale units configuration (defaults to milliseconds)
// > Added Median Absolute Deviation
// > Added nicer ASCII barcharts and formating helper methods

#endregion

namespace Doxense.Mathematics.Statistics
{
	using System;
	using System.Collections.Generic;
	using System.Diagnostics;
	using System.Globalization;
	using System.Linq;
	using System.Text;

	public sealed class RobustHistogram
	{

		public enum TimeScale
		{
			Ticks,
			Nanoseconds,
			Microseconds,
			Milliseconds,
			Seconds,
		}

		public const string HorizontalScale = "0.01     0.1             1        10              100             1k              10k             100k            1M              10M             100M            1G              10G             100G            1T              10T             100T            ";
		public const string HorizontalShade = "---------================---------================¤¤¤¤¤¤¤¤¤¤¤¤¤¤¤¤----------------================¤¤¤¤¤¤¤¤¤¤¤¤¤¤¤¤----------------================¤¤¤¤¤¤¤¤¤¤¤¤¤¤¤¤----------------================¤¤¤¤¤¤¤¤¤¤¤¤¤¤¤¤----------------================¤¤¤¤¤¤¤¤¤¤¤¤¤¤¤¤";

		const int NumBuckets = 154 + 25;
		private static readonly double[] BucketLimits = new double[NumBuckets]
		{
			0.01, 0.02, 0.03, 0.04, 0.05, 0.06, 0.07, 0.08, 0.09,
			0.10, 0.12, 0.14, 0.16, 0.18, 0.20, 0.25, 0.30, 0.35, 0.40, 0.45, 0.50, 0.60, 0.70, 0.80, 0.90,
			1, 2, 3, 4, 5, 6, 7, 8, 9,
			10, 12, 14, 16, 18, 20, 25, 30, 35, 40, 45, 50, 60, 70, 80, 90,
			100, 120, 140, 160, 180, 200, 250, 300, 350, 400, 450, 500, 600, 700, 800, 900,
			1000, 1200, 1400, 1600, 1800, 2000, 2500, 3000, 3500, 4000, 4500, 5000, 6000, 7000, 8000, 9000,
			10000, 12000, 14000, 16000, 18000, 20000, 25000, 30000, 35000, 40000, 45000, 50000, 60000, 70000, 80000, 90000,
			100000, 120000, 140000, 160000, 180000, 200000, 250000, 300000, 350000, 400000, 450000, 500000, 600000, 700000, 800000, 900000,
			1000000, 1200000, 1400000, 1600000, 1800000, 2000000, 2500000, 3000000, 3500000, 4000000, 4500000, 5000000, 6000000, 7000000, 8000000, 9000000, 
			10000000, 12000000, 14000000, 16000000, 18000000, 20000000, 25000000, 30000000, 35000000, 40000000, 45000000, 50000000, 60000000, 70000000, 80000000, 90000000,
			100000000, 120000000, 140000000, 160000000, 180000000, 200000000, 250000000, 300000000, 350000000, 400000000, 450000000, 500000000, 600000000, 700000000, 800000000, 900000000,
			1000000000, 1200000000, 1400000000, 1600000000, 1800000000, 2000000000, 2500000000, 3000000000, 3500000000, 4000000000, 4500000000, 5000000000, 6000000000, 7000000000, 8000000000, 9000000000,
			1e200
		};
		private static readonly double SUM_RATIO = (1d / BucketLimits[0]);
		private static readonly double SUM_SQUARES_RATIO = SUM_RATIO * SUM_RATIO;

		private static readonly string BarTicksChars = "<[[((|))]]>";
		private static readonly string BarChartChars = " .:;+=xX$&#";
		private static readonly string VerticalChartChars = " _.-:=xX&#@";

		public RobustHistogram()
			: this(TimeScale.Microseconds)
		{ }

		public RobustHistogram(TimeScale scale)
		{
			this.Clear();
			this.Scale = scale;
			this.TicksToUnit = GetScaleToTicksRatio(scale);
		}

		private static double GetScaleToTicksRatio(TimeScale scale)
		{
			switch(scale)
			{
				case TimeScale.Ticks: return 1.0d;
				case TimeScale.Nanoseconds: return 1E-2d;
				case TimeScale.Microseconds: return 1E1d;
				case TimeScale.Milliseconds: return 1E4d;
				case TimeScale.Seconds: return 1E7d;
				default: return 1.0d;
			}
		}

		private static string GetScaleUnit(TimeScale scale)
		{
			switch(scale)
			{
				case TimeScale.Ticks: return "t";
				case TimeScale.Nanoseconds: return "ns";
				case TimeScale.Microseconds: return "µs";
				case TimeScale.Milliseconds: return "ms";
				case TimeScale.Seconds: return "s";
				default: return String.Empty;
			}
		}

		private TimeSpan ToTimeSpan(double value)
		{
			return TimeSpan.FromTicks((long)(value * this.TicksToUnit));
		}

		public void Clear()
		{
			this.Min = BucketLimits[NumBuckets - 1];
			this.Max = 0;
			this.Count = 0;
			this.InternalSum = 0;
			this.InternalSumSquares = 0;
			if (this.Buckets == null)
			{
				this.Buckets = new long[NumBuckets];
			}
			else
			{
				Array.Clear(this.Buckets, 0, this.Buckets.Length);
			}
		}

		public void Merge(RobustHistogram other)
		{
			if (other.Min < this.Min) this.Min = other.Min;
			if (other.Max > this.Max) this.Max = other.Max;
			this.Count += other.Count;
			this.InternalSum += other.InternalSum;
			this.InternalSumSquares += other.InternalSumSquares;
			for (int b = 0; b < NumBuckets; b++)
			{
				this.Buckets[b] += other.Buckets[b];
			}
		}

		public void AddTicks(long ticks)
		{
			Add(ticks / this.TicksToUnit);
		}

		private static int GetBucketIndex(double value)
		{
			// Linear search to find the corresponding bucket index
			int index = 0;
			while (index < NumBuckets - 1 && BucketLimits[index] <= value)
			{
				++index;
			}
			return index;
		}

		public void Add(double value)
		{
			int bucketIndex = GetBucketIndex(value);
			++this.Buckets[bucketIndex];
			if (this.Min > value) this.Min = value;
			if (this.Max < value) this.Max = value;
			++this.Count;
			this.InternalSum += (value * SUM_RATIO);
			this.InternalSumSquares += (value * value * SUM_SQUARES_RATIO);
		}

		public void Add(TimeSpan value)
		{
			AddTicks(value.Ticks);
		}

		public TimeScale Scale { get; private set; }

		private double TicksToUnit { get; set; }

		/// <summary>Retourne le nombre d'échantillons: Ts.Count()</summary>
		public int Count { get; private set; }

		/// <summary>Retourne l'échantillon minimum: Ts.Min()</summary>
		public double Min { get; private set; }

		/// <summary>Retourne l'échantillon maximum: Ts.Max()</summary>
		public double Max { get; private set; }

		/// <summary>Retourne la somme des valeurs: Ts.Sum(t => t)</summary>
		public double Sum { get { return this.InternalSum / SUM_RATIO; } }

		/// <summary>Retourne la somme des carrés des valeurs: Ts.Sum(t => t * t)</summary>
		public double SumSquares { get { return this.InternalSumSquares / SUM_SQUARES_RATIO; } }

		private double InternalSum { get; set; }
		private double InternalSumSquares { get; set; }

		private long[] Buckets { get; set; }

		/// <summary>Retourne l'échantillon médian</summary>
		public double Median
		{
			get { return Percentile(50); }
		}

		/// <summary>Retourne la valeur du percentile <paramref name="p"/> (entre 0 et 100)</summary>
		/// <param name="p">Valeur du percentile, entre 0 et 100 (ex: 50 pour la médiane)</param>
		/// <returns>Valeur du percentile correspondante</returns>
		public double Percentile(double p)
		{
			double threshold = this.Count * (p / 100.0d);
			double sum = 0;
			for (int b = 0; b < this.Buckets.Length; b++)
			{
				sum += this.Buckets[b];
				if (sum >= threshold)
				{
					// Scale linearly within this bucket
					double leftPoint = (b == 0) ? 0 : BucketLimits[b - 1];
					double rightPoint = BucketLimits[b];
					double leftSum = sum - this.Buckets[b];
					double rightSum = sum;
					double pos = (threshold - leftSum) / (rightSum - leftSum);
					double r = leftPoint + (rightPoint - leftPoint) * pos;
					if (r < this.Min) r = this.Min;
					if (r > this.Max) r = this.Max;
					return r;
				}
			}
			return this.Max;
		}

		/// <summary>Retourne la Median Absolute Devitation des échantillons</summary>
		public double MedianAbsoluteDeviation()
		{
			// see: http://en.wikipedia.org/wiki/Median_absolute_deviation

			// DISCLAIMER: This is probably broken!
			// I'm using the midpoint of each bucket as the value used to compute the deviation, and the same approximation method used in Percentile(..) to compute the resulting median.

			if (this.Count == 0) return 0;
			var median = Percentile(50);

			var array = this.Buckets
				.Select((x, i) =>
				{
					double leftPoint = i > 0 ? BucketLimits[i - 1] : 0;
					double rightPoint = BucketLimits[i];
					// on considère qu'on est au millieu
					return new { Count = x, Deviation = Math.Abs(((leftPoint + rightPoint) / 2d) - median) };
				})
				.Where(kvp => kvp.Count > 0)
				.OrderBy((kvp => kvp.Deviation))
				.ToArray();

			double threshold = this.Count * 0.5d;
			double sum = 0;
			for (int b = 0; b < array.Length; b++)
			{
				sum += array[b].Count;
				if (sum >= threshold)
				{
					// Scale linearly within this bucket
					double leftPoint = (b == 0) ? 0 : array[b - 1].Deviation;
					double rightPoint = array[b].Deviation;
					double leftSum = sum - array[b].Count;
					double rightSum = sum;
					double pos = (threshold - leftSum) / (rightSum - leftSum);
					double r = leftPoint + (rightPoint - leftPoint) * pos;
					return r;
				}
			}
			return array.Length == 0 ? 0 : array[array.Length - 1].Deviation;
		}

		/// <summary>Retourne la valeur moyenne</summary>
		public double Average
		{
			get { return this.Count == 0 ? 0 : (this.Sum / this.Count); }
		}

		/// <summary>Retourne la valeur de l'écart-type</summary>
		public double StandardDeviation
		{
			get
			{
				if (this.Count == 0) return 0;
				double variance = ((this.SumSquares * this.Count) - (this.Sum * this.Sum)) / (this.Count * this.Count);
				return Math.Sqrt(variance);
			}
		}

		private static string FormatHistoBar(double value, int chars, char pad = '\0', bool sparse = false)
		{
			int marks = (int)Math.Round((value * chars * 10), MidpointRounding.AwayFromZero);

			if (value >= 1)
			{
				if (value > 1.0 + double.Epsilon) return new string('+', chars);
				if (sparse) return new string(pad == '\0' ? ' ' : pad, chars - 1) + "@";
				return new string('@', chars);
			}

			char[] s = new char[chars];

			int p = 0;

			if (value < -double.Epsilon)
			{
				s[p++] = '[';
				s[p++] = 'N';
				s[p++] = 'E';
				s[p++] = 'G'; 
				s[p++] = ']';
			}
			else if (marks == 0)
			{
				if (value > double.Epsilon) s[p++] = '`';
			}
			else if (marks > 0)
			{
				while (marks > 10)
				{
					s[p++] = !sparse ? '#' : pad == '\0' ? ' ' : pad;
					marks -= 10;
				}

				if (marks > 0)
				{
					if (sparse) 
						s[p++] = BarTicksChars[marks];
					else
						s[p++] = BarChartChars[marks];
				}
			}

			if (pad != '\0')
			{
				while (p < chars) s[p++] = pad;
			}

			return new string(s, 0, p);
		}

		public string GetDistribution(double begin = 1.0d, double end = 1E200, int fold = 0)
		{
			if (fold < 1) fold = 1;
			int offset = GetBucketIndex(begin);
			int len = GetBucketIndex(end) - offset;

			var data = new long[fold == 1 ? len : (int)Math.Ceiling(1.0 * len / fold)];
			for (int i = 0; i < len; i++) { data[i / fold] += this.Buckets[offset + i]; }

			long max = this.Count > 0 ? data.Max() : 0;

			if (max <= 0) return new string(' ', len);
			max = (3 * max + this.Count) / 4;

			char[] cs = new char[data.Length];
			var rr = (double)(VerticalChartChars.Length - 1) / max;
			for (int i = 0; i < cs.Length; i++)
			{
				int p = Math.Min((int)Math.Ceiling(rr * data[i]), 10);
				cs[i] = VerticalChartChars[p];
			}
			return new string(cs);
		}

		public static string GetDistributionScale(string scaleString, double begin = 1.0d, double end = 1E200)
		{
			int offset = GetBucketIndex(begin) - 1;
			int len = GetBucketIndex(end) - offset - 1;
			return scaleString.Substring(offset, len);
		}

		public string GetPercentiles()
		{
			return String.Format(
				CultureInfo.InvariantCulture,
				"{0:5,#,##0.0} --| {1:5,#,##0.0} ==[ {2:5,#,##0.0} ]== {3:5,#,##0.0} |-- {4:5,#,##0.0}",
				this.Percentile(5),
				this.Percentile(25),
				this.Percentile(50),
				this.Percentile(75),
				this.Percentile(95)
			);
		}

		/// <summary>Génère un rapport des mesures</summary>
		/// <param name="detailed">Si false, génère un tableau qui tient en moins de 80 caractère de large. Si true, retourne une version plus large avec plus de bars graphs</param>
		public string GetReport(bool detailed)
		{
			var r = new StringBuilder();

			var unit = GetScaleUnit(this.Scale);

			r.AppendLine(String.Format(CultureInfo.InvariantCulture,
				"- Total : {0:0.0##} sec, {1:N0} ops",
				ToTimeSpan(this.Sum).TotalSeconds,
				this.Count
			));

			if (this.Count > 0)
			{
				double min = this.Count == 0 ? 0d : this.Min;
				double max = this.Max;
				double median = this.Median;

				// MAD
				for (int i = 0; i < this.Buckets.Length; i++)
				{
				}

				r.AppendLine(String.Format(CultureInfo.InvariantCulture,
					"- Min/Max: {0,6:0.000} {3} .. {1,6:0.000} {3}, Average: {2:0.000} {3}",
					min, max, this.Average, unit
				));
				r.AppendLine(String.Format(CultureInfo.InvariantCulture,
					"- Median : {0,6:0.000} {3} (+/-{1:0.000} {3}), StdDev: {2:0.000}",
					median, this.MedianAbsoluteDeviation(), this.StandardDeviation, unit
				));
				r.AppendLine(String.Format(CultureInfo.InvariantCulture,
					"- Distrib: ({0:#,##0.0}) - {1:#,##0.0} =[ {2:#,##0.0} ]= {3:#,##0.0} - ({4:#,##0.0})",
					this.Percentile(5), this.Percentile(25), median, this.Percentile(75), this.Percentile(95)
				));

				if (detailed)
				{
					r.AppendLine("   _____________________________________________________________________________________________________________________"); //_________");
					r.AppendLine("  |____[ Min , Max )____|___Count___|__Percent____________________________________________________|___Cumulative________|");//____kOps_|");
				}
				else
				{
					r.AppendLine("   ________________________________________________________________________ ");
					r.AppendLine("  |____[ Min , Max )____|___Count___|__Percent__________________|__Cumul.__|");
				}
				double mult = 100.0d / this.Count;
				double sum = 0;
				for (int b = 0; b < NumBuckets; b++)
				{
					if (this.Buckets[b] <= 0) continue;
					sum += this.Buckets[b];
					r.Append(String.Format(CultureInfo.InvariantCulture,
						"  | {0,8:###,##0.###} - {1,-8:###,##0.###} | {2,9:#,###,###} | {3,7:##0.000}% " + (detailed ? "{5,50} " : "{5, 16} ") + "| {4,7:##0.000}%" + (detailed ? " {6,10}"/*" | {7,7:0.0}"*/ : ""),
						/* 0 */ ((b == 0) ? 0.0 : BucketLimits[b - 1]),     // left
						/* 1 */ BucketLimits[b],							// right
						/* 2 */ this.Buckets[b],							// count
						/* 3 */ mult * this.Buckets[b],						// percentage
						/* 4 */ mult * sum,									// cumulative percentage
						/* 5 */ FormatHistoBar((double)this.Buckets[b] / this.Count, detailed ? 50 : 16, pad: ' '),
						/* 6 */ detailed ? FormatHistoBar(sum / this.Count, 10, pad: '-', sparse: true) : string.Empty /*,
						(0.001d / ToTimeSpan(BucketLimits[b]).TotalSeconds)*/
					));
					r.AppendLine(" |");
				}
				if (detailed)
				{
					r.AppendLine("  `---------------------------------------------------------------------------------------------------------------------'"); // ---------
				}
				else
				{
					r.AppendLine("  `------------------------------------------------------------------------'");
				}
			}
			return r.ToString();
		}

		public override string ToString()
		{
			return String.Format(CultureInfo.InvariantCulture, "Count={0}, Avg={1}, Min={2}, Max={3}", this.Count, this.Average, this.Count > 0 ? this.Min : 0, this.Max);
		}

	}

	public class RobustTimeLine
	{

		public List<RobustHistogram> Histos { get; private set; }
		public TimeSpan Step { get; private set; }
		public RobustHistogram.TimeScale Scale { get; private set; }
		public Func<RobustHistogram, int, bool> Completed { get; private set; }

		private int LastIndex { get; set; }
		private Stopwatch Clock { get; set; }
		private int Offset { get; set; }

		public RobustTimeLine(TimeSpan step, RobustHistogram.TimeScale scale = RobustHistogram.TimeScale.Milliseconds, Func<RobustHistogram, int, bool> onCompleted = null)
		{
			if (step <= TimeSpan.Zero) throw new ArgumentException("Time step must be greater than zero", "step");

			this.Histos = new List<RobustHistogram>();
			this.Step = step;
			this.Completed = onCompleted;
			this.Clock = Stopwatch.StartNew();
		}

		public int Count
		{
			get { return this.Histos.Count; }
		}

		public void Start()
		{
			this.Clock.Restart();
		}

		public void Stop()
		{
			this.Clock.Stop();
		}

		private int GetGraphIndex(TimeSpan elapsed)
		{
			return (int)(elapsed.Ticks / this.Step.Ticks);
		}

		private bool HasFrame(int index)
		{
			index -= this.Offset;
			return index >= 0 && index < this.Histos.Count;
		}

		private RobustHistogram GetFrame(TimeSpan elapsed)
		{
			int index = GetGraphIndex(elapsed);

			if (index != this.LastIndex && this.Completed != null && HasFrame(this.LastIndex))
			{
				if (this.Completed(this.Histos[this.LastIndex - this.Offset], this.LastIndex))
				{ // reset!
					this.Histos.Clear();
					this.Offset = this.LastIndex;
				}
				this.LastIndex = index;
			}

			while (!HasFrame(index))
			{
				var histo = new RobustHistogram(this.Scale);
				this.Histos.Add(histo);
			}

			return this.Histos[index - this.Offset];
		}

		public void Add(double value)
		{
			GetFrame(this.Clock.Elapsed).Add(value);
		}

		public void Add(TimeSpan value)
		{
			GetFrame(this.Clock.Elapsed).Add(value);
		}

		public RobustHistogram MergeResults(TimeSpan window)
		{
			return MergeResults((int)Math.Max(1, Math.Ceiling((double)window.Ticks / this.Step.Ticks)));
		}

		public RobustHistogram MergeResults(int samples)
		{
			var merged = new RobustHistogram(this.Scale);
			foreach (var histo in this.Histos.Reverse<RobustHistogram>().Take(samples))
			{
				merged.Merge(histo);
			}
			return merged;
		}

		public RobustHistogram MergeResults()
		{
			return MergeResults(this.Histos.Count);
		}

	}

}
