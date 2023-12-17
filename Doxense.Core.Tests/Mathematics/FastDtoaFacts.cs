#region Copyright (c) 2023 SnowBank SAS, (c) 2005-2023 Doxense SAS
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

namespace Doxense.Mathematics.Test
{
	using System;
	using System.Globalization;
	using Doxense.Mathematics.Statistics;
	using Doxense.Testing;
	using NUnit.Framework;

	[TestFixture]
	[Category("Core-SDK")]
	public class FastDtoaFacts : DoxenseTest
	{

		[Test]
		public void Test_DiyDouble_Constants()
		{
			Assert.That(DiyDouble.SignMask,		Is.EqualTo(0x8000000000000000ul), "Sign bit is at bit 63 (MSB)");
			Assert.That(DiyDouble.ExponentMask,    Is.EqualTo(0x7FF0000000000000ul), "Exponent takes 11 bits (52 to 62)");
			Assert.That(DiyDouble.SignificandMask, Is.EqualTo(0x000FFFFFFFFFFFFFul), "Significant takes 52 bits (0 to 51)");
			Assert.That(DiyDouble.HiddenBit,       Is.EqualTo(0x0010000000000000ul), "Hidden bit is at bit 53");
			Assert.That(DiyDouble.SignificandSize, Is.EqualTo(53), "Includes the hidden bit");
			Assert.That(DiyDouble.PhysicalSignificandSize, Is.EqualTo(52), "Excludes the hidden bit");

			Assert.That(DiyDouble.ExponentBias, Is.EqualTo(1075), "Exponent Bias");
			Assert.That(DiyDouble.DenormalExponent, Is.EqualTo(-1074), "Denormal Exponent");
			Assert.That(DiyDouble.MaxExponent, Is.EqualTo(972), "Max Exponent");
			Assert.That(DiyDouble.Infinity, Is.EqualTo(0x7FF0000000000000ul), "Infinity");
			Assert.That(DiyDouble.NaN, Is.EqualTo(0x7FF8000000000000ul), "NaN");
		}

		[Test]
		public void Test_DiySingle_Constants()
		{
			Assert.That(DiySingle.SignMask,        Is.EqualTo(0x80000000u), "Sign bit is at bit 31 (MSB)");
			Assert.That(DiySingle.ExponentMask,    Is.EqualTo(0x7F800000u), "Exponent takes 8 bits (23 to 30)");
			Assert.That(DiySingle.SignificandMask, Is.EqualTo(0x007FFFFFu), "Significant takes 23 bits (0 to 22)");
			Assert.That(DiySingle.HiddenBit,       Is.EqualTo(0x00800000u), "Hidden bit is at bit 23");
			Assert.That(DiySingle.SignificandSize, Is.EqualTo(24), "Includes the hidden bit");
			Assert.That(DiySingle.PhysicalSignificandSize, Is.EqualTo(23), "Excludes the hidden bit");

			Assert.That(DiySingle.ExponentBias, Is.EqualTo(150), "Exponent Bias");
			Assert.That(DiySingle.DenormalExponent, Is.EqualTo(-149), "Denormal Exponent");
			Assert.That(DiySingle.MaxExponent, Is.EqualTo(105), "Max Exponent");
			Assert.That(DiySingle.Infinity, Is.EqualTo(0x7F800000u), "Infinity");
			Assert.That(DiySingle.NaN, Is.EqualTo(0x7FC00000u), "NaN");
		}

		[Test]
		public void Test_DiyDouble_Properties()
		{
			static void Check(double value)
			{
				DiyDouble d = value;
				Log(d);

				Assert.That(d.Value, Is.EqualTo(value), $"Value {d}");
				ulong r;
				unsafe
				{
					r = *((ulong*) (&d));
				}

				Assert.That(d.Raw, Is.EqualTo(r), $"Raw {d}");

				Assert.That(d.IsNaN, Is.EqualTo(double.IsNaN(value)), $"NaN? {d}");
				Assert.That(d.IsInfinite, Is.EqualTo(double.IsInfinity(value)), $"Infinity? {d}");
				Assert.That(d.Sign, Is.EqualTo(value >= 0 ? +1 : -1), $"Sign? {d}");

				Assert.That(d.IsInteger, Is.EqualTo(!double.IsInfinity(value) && !double.IsNaN(value) && (value - Math.Truncate(value)) == 0.0), $"Integer? {d}");
			}

			Check(0.0);
			Check(double.Epsilon);
			Check(1.0);
			Check(-1.0);
			Check(double.NaN);
			Check(double.PositiveInfinity);
			Check(double.NegativeInfinity);
			Check((double)int.MaxValue);
			Check((double)long.MaxValue);
			Check(Math.PI);
			Check(Math.E);
			Check(1.0 / 3.0);
			Check(11.0 / 10.0);
			Check(1025.0 / 1024.0);

			var rnd = new Random(7654321);
			for (int i = 0; i < 10; i++)
			{
				Check(rnd.NextDouble());
			}
			for (int i = 0; i < 10; i++)
			{
				Check((double)rnd.Next());
			}
			for (int i = 0; i < 10; i++)
			{
				Check(0.5d + (double)rnd.Next());
			}
			for (int i = 0; i < 63; i++)
			{
				Check(rnd.NextDouble() * (1UL << i));
			}

		}

		[Test]
		public void Test_DiySingle_Properties()
		{
			static void Check(float value)
			{
				DiySingle d = value;
				Log(d);

				Assert.That(d.Value, Is.EqualTo(value), $"Value {d}");
				uint r;
				unsafe
				{
					r = *((uint*) (&d));
				}

				Assert.That(d.Raw, Is.EqualTo(r), $"Raw {d}");

				Assert.That(d.IsNaN, Is.EqualTo(float.IsNaN(value)), $"NaN? {d}");
				Assert.That(d.IsInfinite, Is.EqualTo(float.IsInfinity(value)), $"Infinity? {d}");
				Assert.That(d.Sign, Is.EqualTo(value >= 0 ? +1 : -1), $"Sign? {d}");

				Assert.That(d.IsInteger, Is.EqualTo(!float.IsInfinity(value) && !float.IsNaN(value) && (value - (float) (int) value) == 0), $"Integer? {d}");
			}

			Check(0.0f);
			Check(float.Epsilon);
			Check(1.0f);
			Check(-1.0f);
			Check(float.NaN);
			Check(float.PositiveInfinity);
			Check(float.NegativeInfinity);
			Check((float)Math.PI);
			Check((float)Math.E);
			Check(1.0f / 3.0f);
			Check(11.0f / 10.0f);
			Check(1025.0f / 1024.0f);

			var rnd = new Random(7654321);
			for (int i = 0; i < 10; i++)
			{
				Check((float)rnd.NextDouble());
			}
			for (int i = 0; i < 10; i++)
			{
				Check((float)rnd.Next());
			}
			for (int i = 0; i < 10; i++)
			{
				Check(0.5f + (float)rnd.Next());
			}
			for (int i = 0; i < 31; i++)
			{
				Check((float)(rnd.NextDouble() * (1U << i)));
			}

		}

		[Test]
		public void Test_FormatDouble()
		{
			var chars = new char[32];

			string Check(double d, string expected)
			{
				Array.Clear(chars, 0, chars.Length);
				string s = FastDtoa.FormatDouble(d);
				Assert.That(s, Is.Not.Null);
				Assert.That(s.Length, Is.GreaterThan(0));
				//Log("dtoa({0}) => \"{1}\" [{2}]", d.ToString("R", CultureInfo.InvariantCulture), s, n);

				Assert.That(s, Is.EqualTo(expected), $"ToShortest({d:R}, {d:G17}, (0x{BitConverter.DoubleToInt64Bits(d):X16})");

				double roundTrip = Double.Parse(s, CultureInfo.InvariantCulture);
				Log("0x{4:X16} : {0:R} ~> \"{1}\" ~> 0x{5:X16} : {2:R} => {3}", d, s, roundTrip, d == roundTrip, BitConverter.DoubleToInt64Bits(d), BitConverter.DoubleToInt64Bits(roundTrip));
				Assert.That(roundTrip, Is.EqualTo(d), $"MISTMATCH {BitConverter.DoubleToInt64Bits(d):X16} -> {s} -> {BitConverter.DoubleToInt64Bits(roundTrip):X16}");

				return s;
			}

			Check(0.0, "0");
			Check(1.0, "1");
			Check(1.23, "1.23");
			Check(12345.0, "12345");
			Check(12345e23, "1.2345E+27");
			Check(1.0 / 3.0, "0.3333333333333333");
			Check(10.0 / 11.0, "0.9090909090909091");
			Check(Math.PI, "3.141592653589793");
			Check(Math.E, "2.718281828459045");
			Check(1e21, "1E+21");
			Check(1e20, "100000000000000000000");
			Check(111111111111111111111.0, "111111111111111110000");
			Check(1111111111111111111111.0, "1.1111111111111111E+21");
			Check(11111111111111111111111.0, "1.1111111111111111E+22");
			Check(-0.00001, "-0.00001");
			Check(-0.000001, "-0.000001");
			Check(-0.0000001, "-1E-7");
			Check(-0.0, "0");
			Check(5e-324, "5E-324");
			Check(double.MaxValue, "1.7976931348623157E+308");
			Check(double.MinValue, "-1.7976931348623157E+308");
			Check(2147483648.0, "2147483648");
			Check(4294967272.0, "4294967272");
			Check(4.1855804968213567e298, "4.185580496821357E+298");

			// corner cases
			Check(3.5844466002796428e+298, "3.5844466002796428E+298"); //BUGBUG: BCL rajoute le '+' pour l'exponent


			// x86/x64 rounding mode differences
			if (IntPtr.Size == 4)
			{
				//BUGBUG: ces valeurs ne roundtrippent pas exactement en x64 ! :(
				Check(1.0000000012588799, "1.00000000125888");
				Check(5.5626846462680035e-309, "5.562684646268003E-309");
			}

		}

		[Test, Category("LongRunning")]
		public void Test_All_The_Things()
		{
			Log($"Test running in {(IntPtr.Size == 4 ? "x86" : "x64")}");
			long b = BitConverter.DoubleToInt64Bits(1);
			int num = 0;
			int notParsed = 0;
			int notEqualOneUlp = 0;
			int notEqual = 0;
			for (int i = 0; i < 10 * 1000 * 1000; i++)
			{
				double d = BitConverter.Int64BitsToDouble(b + i);

				var s = FastDtoa.FormatDouble(d);
				double dd;
				++num;
				if (!double.TryParse(s, NumberStyles.Float | NumberStyles.AllowThousands, CultureInfo.InvariantCulture, out dd))
				{
					++notParsed;
				}
				else if (d != dd)
				{
					if (Math.Abs(BitConverter.DoubleToInt64Bits(dd) - BitConverter.DoubleToInt64Bits(d)) <= 1)
					{
						++notEqualOneUlp;
					}
					else
					{
						++notEqual;
					}
					Log(d.ToString("R") + " / " + s);
				}
			}

			Log($"{num} tests: {notParsed} failed to parse, {notEqual} != more than 1 ULP, {notEqualOneUlp} != by exactly 1 ULP");
			Assert.That(notParsed, Is.EqualTo(0), "Numbers that failed parsing should be 0");
			Assert.That(notEqual, Is.EqualTo(0), "Numbers that differ from more than one ULP after roundtripping");
			// le rounding mode de x64 n'est pas le même que x86, ce qui entraine des différences de 1 ULP dans certaisn cas en x86
			if (IntPtr.Size == 4)
			{
				Assert.That(notEqualOneUlp, Is.EqualTo(0), "Numbers that differ by only one ULP after roundtripping (x86)");
			}
			else
			{
				Assert.That(notEqualOneUlp, Is.LessThanOrEqualTo(227), "Numbers that differ by only one ULP after roundtripping (x64)");
			}
		}

		[Test]
		public void Bench_Comparison()
		{
			const int ITER = 10 * 1000;
			const int RUNS = 20;

			double[] nums = new double[ITER];
			var rnd = new Random(1234567);
			for (int i = 0; i < nums.Length; i++)
			{
				nums[i] = rnd.NextDouble() * 1000000;
			}

			RobustBenchmark.Report<double[]> report;

			// Integer Baseline
			report = RobustBenchmark.Run(
				() => nums,
				(t, i) =>
				{
					var d = (long)t[i];
					string s;
					s = d.ToString(null, CultureInfo.InvariantCulture);
					s = d.ToString(null, CultureInfo.InvariantCulture);
					s = d.ToString(null, CultureInfo.InvariantCulture);
					s = d.ToString(null, CultureInfo.InvariantCulture);
					s = d.ToString(null, CultureInfo.InvariantCulture);
				},
				(t) => t,
				RUNS,
				ITER
			);
			Log($"Int64.ToString()    : Med={report.MedianIterationsNanos / 5:N1}, Best={report.BestIterationsNanos / 5:N1}, StdDev={report.StdDevIterationNanos / 5:N2}, GCs={report.GC0} / {report.GC1} / {report.GC2}");

			// Double Baseline
			report = RobustBenchmark.Run(
				() => nums,
				(t, i) =>
				{
					var d = t[i];
					string s;
					s = d.ToString("R", CultureInfo.InvariantCulture);
					s = d.ToString("R", CultureInfo.InvariantCulture);
					s = d.ToString("R", CultureInfo.InvariantCulture);
					s = d.ToString("R", CultureInfo.InvariantCulture);
					s = d.ToString("R", CultureInfo.InvariantCulture);				
				},
				(t) => t,
				RUNS,
				ITER
			);
			Log($"Double.ToString('R'): Med={report.MedianIterationsNanos / 5:N1}, Best={report.BestIterationsNanos / 5:N1}, StdDev={report.StdDevIterationNanos / 5:N2}, GCs={report.GC0} / {report.GC1} / {report.GC2}");

			// Double Baseline (no decimals)
			report = RobustBenchmark.Run(
				() => nums,
				(t, i) =>
				{
					var d = Math.Floor(t[i]);
					string s;
					s = d.ToString("R", CultureInfo.InvariantCulture);
					s = d.ToString("R", CultureInfo.InvariantCulture);
					s = d.ToString("R", CultureInfo.InvariantCulture);
					s = d.ToString("R", CultureInfo.InvariantCulture);
					s = d.ToString("R", CultureInfo.InvariantCulture);
				},
				(t) => t,
				RUNS,
				ITER
			);
			Log($"  (integer)         : Med={report.MedianIterationsNanos / 5:N1}, Best={report.BestIterationsNanos / 5:N1}, StdDev={report.StdDevIterationNanos / 5:N2}, GCs={report.GC0} / {report.GC1} / {report.GC2}");

			// Grisu3 ToString()
			report = RobustBenchmark.Run(
				() => nums,
				(t, i) =>
				{
					var d = t[i];
					string s;
					s = FastDtoa.FormatDouble(d);
					s = FastDtoa.FormatDouble(d);
					s = FastDtoa.FormatDouble(d);
					s = FastDtoa.FormatDouble(d);
					s = FastDtoa.FormatDouble(d);
				},
				(t) => t,
				RUNS,
				ITER
			);
			Log($"Grisu3 string       : Med={report.MedianIterationsNanos / 5:N1}, Best={report.BestIterationsNanos / 5:N1}, StdDev={report.StdDevIterationNanos / 5:N2}, GCs={report.GC0} / {report.GC1} / {report.GC2}");

			// Grisu3 ToString() (no decimals)
			report = RobustBenchmark.Run(
				() => nums,
				(t, i) =>
				{
					var d = Math.Floor(t[i]);
					string s;
					s = FastDtoa.FormatDouble(d);
					s = FastDtoa.FormatDouble(d);
					s = FastDtoa.FormatDouble(d);
					s = FastDtoa.FormatDouble(d);
					s = FastDtoa.FormatDouble(d);
				},
				(t) => t,
				RUNS,
				ITER
			);
			Log($"  (integer)         : Med={report.MedianIterationsNanos / 5:N1}, Best={report.BestIterationsNanos / 5:N1}, StdDev={report.StdDevIterationNanos / 5:N2}, GCs={report.GC0} / {report.GC1} / {report.GC2}");

			// Grisu3 ToBuffer()
			var chars = new char[32];
			report = RobustBenchmark.Run(
				() => nums,
				(t, i) =>
				{
					var d = t[i];
					var buf = chars;
					int n;
					n = FastDtoa.FormatDouble(d, buf, 0);
					n = FastDtoa.FormatDouble(d, buf, 0);
					n = FastDtoa.FormatDouble(d, buf, 0);
					n = FastDtoa.FormatDouble(d, buf, 0);
					n = FastDtoa.FormatDouble(d, buf, 0);
				},
				(t) => t,
				RUNS,
				ITER
			);

			// Grisu3 ToBuffer() (no decimals)
			Log($"Grisu3 char[]       : Med={report.MedianIterationsNanos / 5:N1}, Best={report.BestIterationsNanos / 5:N1}, StdDev={report.StdDevIterationNanos / 5:N2}, GCs={report.GC0} / {report.GC1} / {report.GC2}");
			report = RobustBenchmark.Run(
				() => nums,
				(t, i) =>
				{
					var d = Math.Floor(t[i]);
					var buf = chars;
					int n;
					n = FastDtoa.FormatDouble(d, buf, 0);
					n = FastDtoa.FormatDouble(d, buf, 0);
					n = FastDtoa.FormatDouble(d, buf, 0);
					n = FastDtoa.FormatDouble(d, buf, 0);
					n = FastDtoa.FormatDouble(d, buf, 0);
				},
				(t) => t,
				RUNS,
				ITER
			);
			Log($"  (integer)         : Med={report.MedianIterationsNanos / 5:N1}, Best={report.BestIterationsNanos / 5:N1}, StdDev={report.StdDevIterationNanos / 5:N2}, GCs={report.GC0} / {report.GC1} / {report.GC2}");
		}

	}

}
