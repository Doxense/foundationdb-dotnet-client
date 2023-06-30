#region Copyright (c) 2005-2023 Doxense SAS
// All rights reserved.
// 
// Redistribution and use in source and binary forms, with or without
// modification, are permitted provided that the following conditions are met:
// 	* Redistributions of source code must retain the above copyright
// 	  notice, this list of conditions and the following disclaimer.
// 	* Redistributions in binary form must reproduce the above copyright
// 	  notice, this list of conditions and the following disclaimer in the
// 	  documentation and/or other materials provided with the distribution.
// 	* Neither the name of Doxense nor the
// 	  names of its contributors may be used to endorse or promote products
// 	  derived from this software without specific prior written permission.
// 
// THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS "AS IS" AND
// ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED
// WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE
// DISCLAIMED. IN NO EVENT SHALL DOXENSE BE LIABLE FOR ANY
// DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES
// (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES;
// LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND
// ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT
// (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS
// SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
#endregion

namespace Doxense.Mathematics.Statistics // REVIEW: Doxense.Benchmarking ?
{
	using System;
	using System.Globalization;
	using System.Linq;
	using System.Runtime.CompilerServices;
	using System.Text;
	using Doxense.Runtime;
	using JetBrains.Annotations;

	/// <summary>Helper that will aggregate individual measurements, and output detailed distribution reports and charts.</summary>
	public sealed class RobustHistogram
	{

		public enum DimensionType
		{
			Unspecified = 0,
			/// <summary>Measuring a duration, or amount of time</summary>
			Time = 1,
			/// <summary>Size (in bytes)</summary>
			Size = 2,
			/// <summary>Ratio (0..1)</summary>
			Ratio = 3,
		}

		public enum TimeScale
		{
			// TIME
			Ticks,
			Nanoseconds,
			Microseconds,
			Milliseconds,
			Seconds,
			// SIZE
			Bytes,
			KiloBytes,
			MegaBytes,
			GigaBytes,
			// RATIO
			Ratio,
		}

		public const string HorizontalScale = "0.01     0.1             1        10              100             1k              10k             100k            1M              10M             100M            1G              10G             100G            1T              10T             100T            ";
		public const string HorizontalShade = "---------================---------================¤¤¤¤¤¤¤¤¤¤¤¤¤¤¤¤----------------================¤¤¤¤¤¤¤¤¤¤¤¤¤¤¤¤----------------================¤¤¤¤¤¤¤¤¤¤¤¤¤¤¤¤----------------================¤¤¤¤¤¤¤¤¤¤¤¤¤¤¤¤----------------================¤¤¤¤¤¤¤¤¤¤¤¤¤¤¤¤";

		private const int NumBuckets = 193;
		public const double MinValue = 0;
		public const double MaxValue = 1e200;
		private static readonly double[] BucketLimits = new double[NumBuckets]
		{
			/*   0 */ 0.001, 0.002, 0.0025, 0.005, 0.01, 0.02, 0.025, 0.03, 0.035, 0.04, 0.045, 0.05, 0.06, 0.07, 0.08, 0.09,
			/*  16 */ 0.10, 0.12, 0.14, 0.16, 0.18, 0.20, 0.25, 0.30, 0.35, 0.40, 0.45, 0.50, 0.60, 0.70, 0.80, 0.90,
			/*  32 */ 1, 1.2, 1.4, 1.6, 1.8, 2, 2.5, 3, 3.5, 4, 4.5, 5, 6, 7, 8, 9,
			/*  48 */ 10, 12, 14, 16, 18, 20, 25, 30, 35, 40, 45, 50, 60, 70, 80, 90,
			/*  64 */ 100, 120, 140, 160, 180, 200, 250, 300, 350, 400, 450, 500, 600, 700, 800, 900,
			/*  80 */ 1000, 1200, 1400, 1600, 1800, 2000, 2500, 3000, 3500, 4000, 4500, 5000, 6000, 7000, 8000, 9000,
			/*  96 */ 10000, 12000, 14000, 16000, 18000, 20000, 25000, 30000, 35000, 40000, 45000, 50000, 60000, 70000, 80000, 90000,
			/* 112 */ 100000, 120000, 140000, 160000, 180000, 200000, 250000, 300000, 350000, 400000, 450000, 500000, 600000, 700000, 800000, 900000,
			/* 128 */ 1000000, 1200000, 1400000, 1600000, 1800000, 2000000, 2500000, 3000000, 3500000, 4000000, 4500000, 5000000, 6000000, 7000000, 8000000, 9000000,
			/* 144 */ 10000000, 12000000, 14000000, 16000000, 18000000, 20000000, 25000000, 30000000, 35000000, 40000000, 45000000, 50000000, 60000000, 70000000, 80000000, 90000000,
			/* 160 */ 100000000, 120000000, 140000000, 160000000, 180000000, 200000000, 250000000, 300000000, 350000000, 400000000, 450000000, 500000000, 600000000, 700000000, 800000000, 900000000,
			/* 176 */ 1000000000, 1200000000, 1400000000, 1600000000, 1800000000, 2000000000, 2500000000, 3000000000, 3500000000, 4000000000, 4500000000, 5000000000, 6000000000, 7000000000, 8000000000, 9000000000,
			/* 192 */ 1e200
		};

		private const int INDEX_ONE = 32;
		private const int INDEX_HUNDRED = 64;
		private const int INDEX_THOUSAND = 80;
		private const int INDEX_MILLION = 128;
		private const int INDEX_BILLION = 176;
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
			switch (scale)
			{
				case TimeScale.Ticks:
				case TimeScale.Nanoseconds:
				case TimeScale.Microseconds:
				case TimeScale.Milliseconds:
				case TimeScale.Seconds:
				{
					this.Dimension = DimensionType.Time;
					break;
				}

				case TimeScale.Bytes:
				case TimeScale.KiloBytes:
				case TimeScale.MegaBytes:
				case TimeScale.GigaBytes:
				{
					this.Dimension = DimensionType.Size;
					break;
				}
				case TimeScale.Ratio:
				{
					this.Dimension = DimensionType.Ratio;
					break;
				}
			}
			this.TicksToUnit = GetScaleToTicksRatio(scale);
		}

		public static void Warmup()
		{
			PlatformHelpers.PreJit(typeof(RobustHistogram));
		}

		[Pure]
		private static double GetScaleToTicksRatio(TimeScale scale)
		{
			switch(scale)
			{
				case TimeScale.Nanoseconds: return 100; // 100 ns / tick
				case TimeScale.Ticks: return 1;
				case TimeScale.Microseconds: return 1.0 / 10; // 10 ticks / µsec
				case TimeScale.Milliseconds: return 1.0 / 10_000; // 10 K ticks / ms
				case TimeScale.Seconds: return 1.0 / 10_000_000; // 10 M ticks / s

				case TimeScale.Ratio: return 1;
				default: return 1;
			}
		}

		[Pure]
		private static double GetScaleFactor(TimeScale scale)
		{
			switch (scale)
			{
				// time is normalized into seconds
				case TimeScale.Nanoseconds: return 1E-9;
				case TimeScale.Ticks: return 1.0 / TimeSpan.TicksPerSecond;
				case TimeScale.Microseconds: return 1E-6;
				case TimeScale.Milliseconds: return 1E-3;
				case TimeScale.Seconds: return 1.0;

				// size is normalized into bytes
				case TimeScale.Bytes: return 1.0;
				case TimeScale.KiloBytes: return 1024;
				case TimeScale.MegaBytes: return 1024 * 1024;
				case TimeScale.GigaBytes: return 1024 * 1024 * 1024;

				// ratio is normalized into 100%
				case TimeScale.Ratio: return 100.0;
				default: return 1.0d;
			}
		}

		[Pure]
		private static string GetScaleUnit(TimeScale scale)
		{
			switch(scale)
			{
				case TimeScale.Ticks: return "t";
				case TimeScale.Nanoseconds: return "ns";
				case TimeScale.Microseconds: return "µs";
				case TimeScale.Milliseconds: return "ms";
				case TimeScale.Seconds: return "s";
				case TimeScale.Bytes: return "b";
				case TimeScale.KiloBytes: return "KiB";
				case TimeScale.MegaBytes: return "MiB";
				case TimeScale.GigaBytes: return "GiB";
				case TimeScale.Ratio: return "%";
				default: return String.Empty;
			}
		}

		[Pure]
		private string Friendly(double value, double xs)
		{

			//notes:
			// - time is normalized into "ticks" which are 1/10,000,000th of a second
			// - size is normalized into "bytes"

			// ReSharper disable once CompareOfFloatsByEqualityOperator
			if (value == 0.0) return "0";

			if (this.Dimension == DimensionType.Time)
			{
				value = value * xs;
				if (value < 0.000001 /* 1µs */)
				{
					return string.Format(CultureInfo.InvariantCulture, "{0:N1}ns", value * 1_000_000_000);
				}
				if (value < 0.001 /* 1ms */)
				{
					return string.Format(CultureInfo.InvariantCulture, "{0:N1}µs", value * 1_000_000);
				}
				if (value < 1 /* 100ms*/)
				{
					return string.Format(CultureInfo.InvariantCulture, "{0:N1}ms", value * 1_000);
				}
				if (value < 10 /* sec */)
				{
					return string.Format(CultureInfo.InvariantCulture, "{0:N2}s", value);
				}
				if (value < 60 /* 1 minute */)
				{
					return string.Format(CultureInfo.InvariantCulture, "{0:N1}s", value);
				}
				return string.Format(CultureInfo.InvariantCulture, "{0:N0}s", value);
			}

			if (this.Dimension == DimensionType.Size)
			{
				value = value * xs;
				if (value < 1000)
				{
					return string.Format(CultureInfo.InvariantCulture, "{0:N0} B", value);
				}
				if (value < 1000_000)
				{
					return string.Format(CultureInfo.InvariantCulture, "{0:N1} KB", value / 1000);
				}

				if (value < 1000_000_000)
				{
					return string.Format(CultureInfo.InvariantCulture, "{0:N1} MB", value / 1000_000);
				}

				return string.Format(CultureInfo.InvariantCulture, "{0:N1} GB", value / 1000_000_000);
			}

			if (this.Dimension == DimensionType.Ratio)
			{
				// normalized into %
				value = value * xs;
				return string.Format(CultureInfo.InvariantCulture, "{0:N1} %", value);
			}

			// "unspecified"

			if (value < 1)
			{
				if (value < 0.000001)
				{
					return string.Format(CultureInfo.InvariantCulture, "{0:N1}n", value * 1000000000);
				}
				if (value < 0.001)
				{
					return string.Format(CultureInfo.InvariantCulture, "{0:N1}µ", value * 1000000);
				}
				if (value < 0.01)
				{
					return string.Format(CultureInfo.InvariantCulture, "{0:N3}", value);
				}
				if (value < 0.1)
				{
					return string.Format(CultureInfo.InvariantCulture, "{0:N2}", value);
				}
				return string.Format(CultureInfo.InvariantCulture, "{0:N1}", value);
			}
			if (value <= 10)
			{
				return string.Format(CultureInfo.InvariantCulture, "{0:N1}", value);
			}
			if (value < 1000)
			{
				return string.Format(CultureInfo.InvariantCulture, "{0:N0}", value);
			}
			if (value < 1000 * 1000)
			{
				return string.Format(CultureInfo.InvariantCulture, "{0:N1} K", value / 1000);
			}
			return string.Format(CultureInfo.InvariantCulture, "{0:N1} M", value / 1000000);
		}

		[Pure]
		private TimeSpan ToTimeSpan(double value)
		{
			return TimeSpan.FromTicks((long)(value / this.TicksToUnit));
		}

		/// <summary>Vide les données de cet histogramme, afin de pouvoir le réutiliser pour une nouvelle campagne de mesure</summary>
		public void Clear()
		{
			this.Min = MaxValue;
			this.Max = MinValue;
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

		private static int GetBucketIndex(double value)
		{
			// On veut trouver pour chaque valeur 'x' le bucket 'B' qui correspond à: BucketLimits[B - 1] <= x < BucketLimits[B]
			// Si value > MaxValue, alors on considère qu'elle est quand même dans le dernier bucket, ce qui faussera les résultats

			// cette méthode est perf sensitive car elle apparait souvent dans les rapports de profiling
			// => on va d'abord déterminer dans quel "quadrant" se trouve la valeur, et finir par un binary search
			// => on part du principe que les valeurs les plus courrantes sont des petits nombres

			int p;
			if (value < 1)
			{ // 0 <= x < 1
				p = BinarySearch(BucketLimits, 0, INDEX_ONE, value);
			}
			else if (value < 100)
			{ // 1 <= x < 100
				p = BinarySearch(BucketLimits, INDEX_ONE, INDEX_HUNDRED - INDEX_ONE, value);
			}
			else if (value < 1000)
			{ // 100 <= x < 1K
				p = BinarySearch(BucketLimits, INDEX_HUNDRED, INDEX_THOUSAND - INDEX_HUNDRED, value);
			}
			else if (value < 1000 * 1000)
			{ // 1K <= x < 1M
				p = BinarySearch(BucketLimits, INDEX_THOUSAND, INDEX_MILLION - INDEX_THOUSAND, value);
			}
			else if (value < 1000 * 1000 * 1000)
			{ // 1M <= x < 1G
				p = BinarySearch(BucketLimits, INDEX_MILLION, INDEX_BILLION - INDEX_MILLION, value);
			}
			else
			{ // x >= 1G
				if (value >= MaxValue) return NumBuckets - 1;
				p = BinarySearch(BucketLimits, INDEX_BILLION, NumBuckets - INDEX_BILLION, value);
			}
			return p < 0 ? ~p : p + 1;
		}

		private static unsafe int BinarySearch(double[] array, int index, int length, double value)
		{
			int lo = index;
			int hi = index + length - 1;
			fixed (double* ptr = &array[0])
			{
				while (lo <= hi)
				{
					int idx = lo + (hi - lo >> 1);
					double x = ptr[idx];
					// ReSharper disable once CompareOfFloatsByEqualityOperator
					if (value == x) return idx;
					if (value > x)
					{
						lo = idx + 1;
					}
					else
					{
						hi = idx - 1;
					}
				}

				return ~lo;
			}
		}

#if FULL_DEBUG
		public static void ValidateGetBucketIndex()
		{
			// Pseudo-test qui permet de vérifier que GetBucketIndex(...) retourne les bonnes valeurs!

			Action<double> getBucket = (x) =>
			{
				int idx;
				idx = GetBucketIndex(x);
				Console.WriteLine($"{x,10:R}: idx={idx,3} : {(idx == 0 ? 0 : BucketLimits[idx - 1]),10:R} <= {x,10:R} < {BucketLimits[idx],10:R}");

			};

			getBucket(0);
			getBucket(0.001);
			getBucket(0.01);
			getBucket(0.1);
			getBucket(1);
			getBucket(10);
			getBucket(11);
			getBucket(12);
			getBucket(12.01);
			getBucket(50);
			getBucket(100);
			getBucket(500);
			getBucket(1000);
			getBucket(5000);
			getBucket(10000);
			getBucket(50000);
			getBucket(100000);
			getBucket(500000);
			getBucket(1000000);
			getBucket(5000000);
			getBucket(1e200);
			getBucket(1e201);
		}
#endif

		/// <summary>Ajoute une valeur (exprimée dans l'unitée de l'échelle associée à cet histogramme)</summary>
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

		/// <summary>Ajoute une durée qui sera adaptée à l'échelle de cet histogramme</summary>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void Add(TimeSpan value)
		{
			Add(value.Ticks * this.TicksToUnit);
		}

		/// <summary>Ajoute une durée (exprimée en ticks de TimeSpan) qui sera adaptée à l'échelle de cet histogramme</summary>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void AddTicks(long ticks)
		{
			Add(ticks * this.TicksToUnit);
		}

		/// <summary>Ajoute une durée (exprimée en ticks de TimeSpan) qui sera adaptée à l'échelle de cet histogramme</summary>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void AddNanos(double nanos)
		{
			Add(nanos * (this.TicksToUnit / 100.0)); // 100 nanos per tick
		}

		/// <summary>Echelle utilisée par cet histogramme</summary>
		public TimeScale Scale { get; }

		public DimensionType Dimension { get; }

		/// <summary>Facteur de conversion pour passer d'un tick de TimeSpan à l'unitée de l'échelle de cet histogramme</summary>
		private double TicksToUnit { get; }

		/// <summary>Retourne le nombre d'échantillons: Ts.Count()</summary>
		public int Count { get; private set; }

		/// <summary>Retourne l'échantillon minimum: Ts.Min()</summary>
		public double Min { get; private set; }

		/// <summary>Retourne l'échantillon maximum: Ts.Max()</summary>
		public double Max { get; private set; }

		/// <summary>Retourne la somme des valeurs: Ts.Sum(t => t)</summary>
		public double Sum
		{
			[Pure]
			get => this.InternalSum / SUM_RATIO;
		}

		/// <summary>Retourne la somme des carrés des valeurs: Ts.Sum(t => t * t)</summary>
		public double SumSquares
		{
			[Pure]
			get => this.InternalSumSquares / SUM_SQUARES_RATIO;
		}

		/// <summary>Somme de toutes les valeurs ajoutées à cet histogramme</summary>
		private double InternalSum { get; set; }

		/// <summary>Somme des carrés de toutes les valeurs ajoutées à cet histogramme</summary>
		private double InternalSumSquares { get; set; }

		/// <summary>Array contenant le nombre de samples ajoutés pour chaque bucket</summary>
		/// <remarks>La slot Buckets[B] contient le nombre de samples dont la valeur x est Buckets[B - 1] &lt;= x &lt; Buckets[B]</remarks>
		private long[] Buckets { get; set; }

		/// <summary>Retourne l'échantillon médian</summary>
		public double Median
		{
			[Pure]
			get => Percentile(50);
		}

		/// <summary>Retourne la valeur du percentile <paramref name="p"/> (entre 0 et 100)</summary>
		/// <param name="p">Valeur du percentile, entre 0 et 100 (ex: 50 pour la médiane)</param>
		/// <returns>Valeur du percentile correspondante</returns>
		[Pure]
		public double Percentile(double p)
		{
			if (this.Count <= 0) return double.NaN;
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

		/// <summary>Retourne le Mean Absolute Deviation</summary>
		[Pure]
		public double MAD()
		{
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
			return array.LastOrDefault()?.Deviation ?? double.NaN;
		}

		/// <summary>Retourne la valeur moyenne</summary>
		public double Average
		{
			[Pure]
			get => this.Count == 0 ? 0 : (this.Sum / this.Count);
		}

		/// <summary>Retourne la valeur de l'écart-type</summary>
		public double StandardDeviation
		{
			[Pure]
			get
			{
				if (this.Count == 0) return 0;
				double variance = ((this.SumSquares * this.Count) - (this.Sum * this.Sum)) / ((double) this.Count * this.Count);
				return Math.Sqrt(variance);
			}
		}

		[Pure]
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
				s[p + 0] = '[';
				s[p + 1] = 'N';
				s[p + 2] = 'E';
				s[p + 3] = 'G';
				s[p + 4] = ']';
				p += 5;
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

		/// <summary>Génère un areaplot correspondant à la distribution des éléments par valeur</summary>
		/// <returns>Chaîne qui ressemble à <code>"   __xX=-___x___     "</code></returns>
		[Pure]
		public string GetDistribution(double start = 1.0d, double end = MaxValue)
		{
			int offset = GetBucketIndex(start);
			int len = Math.Max(GetBucketIndex(end) - offset, 0);

			long max = this.Count > 0 ? this.Buckets.Skip(offset).Take(len).Max() : 0;

			if (max <= 0) return new string(' ', len);
			max = (3 * max + this.Count) / 4;

			char[] cs = new char[len];
			var rr = (double)(VerticalChartChars.Length - 1) / max;
			for (int i = 0; i < len; i++)
			{
				int p =  (int)Math.Ceiling(rr * this.Buckets[i + offset]);
				cs[i] = VerticalChartChars[p];
			}
			return new string(cs);
		}

		[Pure]
		public double[] GetDistributionData(double start = 1.0d, double end = MaxValue)
		{
			int offset = GetBucketIndex(start);
			int len = Math.Max(GetBucketIndex(end) - offset, 0);
			double[] xs = new double[len];
			for (int i = 0; i < len; i++)
			{
				xs[i] = this.Buckets[i + offset];
			}
			return xs;
		}

		[Pure]
		public string GetDistributionAuto() => GetDistribution(this.LowThreshold, this.HighThreshold);

		private static void Write(char[] buf, int offset, double p, char c, char ifEqual = '\0', char replaceWith = '\0')
		{
			int x = GetBucketIndex(p) - offset;
			if (x >= 0 && x < buf.Length)
			{
				if (ifEqual != '\0' && buf[x] == ifEqual)
					buf[x] = replaceWith;
				else
					buf[x] = c;
			}
		}

		/// <summary>Gènère un boxplot horizontale correspondant aux distributions des données</summary>
		/// <returns>Chaine qui ressemble à <code>"¤     ×·(——[==#===]——————)···×          @"</code>.
		/// Légende:
		/// - '#' = MEDIAN
		/// - '¤' et '@' = MIN et MAX
		/// - '[' et ']' = P25 et P75
		/// - '(' et ')' = P05 et P95
		/// - 'x' = P01 et P99
		/// </returns>
		[Pure]
		public string GetPercentile(double start = 1.0d, double end = MaxValue)
		{
			int offset = GetBucketIndex(start);
			int len = Math.Max(GetBucketIndex(end) - offset, 0);

			var percentiles = new double[101];
			percentiles[0] = 0;
			for (int i = 1; i <= 100; i++)
			{
				percentiles[i] = Percentile(i);
			}

			char[] cs = new char[len];
			for (int i = 0; i < len; i++)
			{
				double p = BucketLimits[i + offset];
				if (p >= percentiles[1] && p <= percentiles[99])
				{
					int idx = Array.BinarySearch(percentiles, p);
					if (idx < 0) idx = ~idx;
					cs[i] = idx < 5  | idx > 95 ? '\u00B7' // '.'
						  : idx < 25 | idx > 75 ? '\u2014' // '-'
						  : '=';
				}
				else
				{
					cs[i] = ' ';
				}
			}

			Write(cs, offset, percentiles[1], '\u00D7');	// P01
			Write(cs, offset, percentiles[99], '\u00D7');	// P99
			Write(cs, offset, percentiles[5], '(');			// P05
			Write(cs, offset, percentiles[95], ')');		// P95
			Write(cs, offset, percentiles[25], '[', '(', '{'); // P25
			Write(cs, offset, percentiles[75], ']', ')', '}'); // P75
			Write(cs, offset, this.Min, '¤');				// MIN
			Write(cs, offset, this.Max, '@');				// MAX
			Write(cs, offset, percentiles[50], '#');		// MEDIAN

			return new string(cs);
		}

		[Pure]
		public string GetPercentileAuto() => GetPercentile(this.LowThreshold, this.HighThreshold);

		[Pure]
		public static double GetMinThreshold(double value)
		{
			return Math.Pow(1000, Math.Floor(Math.Log10(value) / 3));
		}

		[Pure]
		public static double GetMaxThreshold(double value)
		{
			return Math.Pow(1000, Math.Ceiling(Math.Log10(value) / 3));
		}

		public double LowThreshold
		{
			[Pure]
			get => GetMinThreshold(this.Min);
		}

		public double HighThreshold
		{
			[Pure]
			get => GetMaxThreshold(this.Max);
		}

		/// <summary>Découpe la section d'une échelle graduée correspondant au min-max spécifié</summary>
		[Pure]
		public static string GetDistributionScale(string scaleString, double start = 1.0d, double end = MaxValue)
		{
			int offset = Math.Max(GetBucketIndex(start) - 1, 0);
			if (end >= MaxValue) return scaleString.Substring(offset);

			int len = Math.Max(GetBucketIndex(end) - offset, 1);
			return scaleString.Substring(offset, len);
		}

		[Pure]
		public string GetScaleAuto(string? scaleString = null) => GetDistributionScale(scaleString ?? HorizontalScale, this.LowThreshold, this.HighThreshold);

		/// <summary>Retourne une description textuelle des différents percentiles</summary>
		[Pure]
		public string GetPercentiles()
		{
			return String.Format(
				CultureInfo.InvariantCulture,
				"{0,5:#,##0.0} --| {1,5:#,##0.0} ==[ {2,5:#,##0.0} ]== {3,5:#,##0.0} |-- {4,5:#,##0.0}",
				this.Percentile(5),
				this.Percentile(25),
				this.Percentile(50),
				this.Percentile(75),
				this.Percentile(95)
			);
		}

		/// <summary>Génère un rapport des mesures</summary>
		/// <param name="detailed">Si false, génère un tableau qui tient en moins de 80 caractère de large. Si true, retourne une version plus large avec plus de bars graphs</param>
		[Pure]
		public string GetReport(bool detailed)
		{
			var r = new StringBuilder(1024);

			var unit = GetScaleUnit(this.Scale);
			var xs = GetScaleFactor(this.Scale);

			r.AppendLine(string.Format(CultureInfo.InvariantCulture,
				"- Total : {0:N0} ops in {1:0.0##} sec ({2:N0} ops/sec)",
				this.Count,
				ToTimeSpan(this.Sum).TotalSeconds,
				this.Count / ToTimeSpan(this.Sum).TotalSeconds
			));

			if (this.Count > 0)
			{
				double min = this.Count == 0 ? 0d : this.Min;
				double max = this.Max;
				double median = this.Median;

				r.AppendLine(String.Format(CultureInfo.InvariantCulture,
					"- Min/Max: {0} {2} .. {1} {2}",
					Friendly(min, xs), Friendly(max, xs), unit
				));
				r.AppendLine(String.Format(CultureInfo.InvariantCulture,
					"- Average: {0} {2} (+/-{1} {2})",
					Friendly(this.Average, xs), Friendly(this.StandardDeviation, xs), unit
				));
				r.AppendLine(String.Format(CultureInfo.InvariantCulture,
					"- Median : {0} {2} (+/-{1} {2})",
					Friendly(median, xs), Friendly(this.MAD(), xs), unit
				));
				r.AppendLine(String.Format(CultureInfo.InvariantCulture,
					"- Distrib: ({0}) - {1} =[ {2} ]= {3} - ({4})",
					Friendly(Percentile(5),  xs), Friendly(Percentile(25),  xs), Friendly(median,  xs), Friendly(Percentile(75),  xs), Friendly(Percentile(95), xs)
				));

				if (detailed)
				{
					r.AppendLine("   ___________________________________________________________________________________________________________________________________ ");
					r.AppendLine("  |____[ Min , Max )____|___Count____|__Percent____________________________________________________|___Cumulative________|__Weight____|");
				}
				else
				{
					r.AppendLine("   _________________________________________________________________________ ");
					r.AppendLine("  |____[ Min , Max )____|___Count____|__Percent__________________|__Cumul.__|");

				}

				double mult = 100.0d / this.Count;
				double sum = 0;
				for (int b = 0; b < NumBuckets; b++)
				{
					long count = this.Buckets[b];
					if (count <= 0) continue;

					sum += count;
					double left = ((b == 0) ? 0.0 : BucketLimits[b - 1]);
					double right = BucketLimits[b];

					r.Append(String.Format(CultureInfo.InvariantCulture,
						"  | {0,8} - {1,-8} | {2,10:#,###,###} | {3,7:##0.000}% " + (detailed ? "{5,50} " : "{5, 16} ") + "| {4,7:##0.000}%" + (detailed ? " {6,10} | {7,10:N0}" : ""),
						/* 0 */ Friendly(left, xs),			// left
						/* 1 */ Friendly(right, xs),		// right
						/* 2 */ count,						// count
						/* 3 */ mult * count,				// percentage
						/* 4 */ mult * sum,					// cumulative percentage
						/* 5 */ FormatHistoBar((double)count / this.Count, detailed ? 50 : 16, pad: ' '),
						/* 6 */ detailed ? FormatHistoBar(sum / this.Count, 10, pad: '-', sparse: true) : string.Empty,
						/* 7 */ detailed ? Friendly((count * (left + right) / 2), xs) : ""
					));
					r.AppendLine(" |");
				}
				if (detailed)
				{
					r.AppendLine("  |_____________________|____________|_____________________________________________________________|_____________________|____________|");
				}
				else
				{
					r.AppendLine("  |_____________________|____________|___________________________|__________|");
				}
			}
			return r.ToString();
		}

		public override string ToString()
		{
			return string.Format(CultureInfo.InvariantCulture, "Count={0}, Avg={1}, Min={2}, Max={3}, Med={4}", this.Count, this.Average, this.Count > 0 ? this.Min : 0, this.Max, this.Median);
		}

	}

}
