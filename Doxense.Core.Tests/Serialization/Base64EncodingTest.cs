#region Copyright (c) 2005-2023 Doxense SAS
//
// All rights are reserved. Reproduction or transmission in whole or in part, in
// any form or by any means, electronic, mechanical or otherwise, is prohibited
// without the prior written consent of the copyright owner.
//
#endregion

namespace Doxense.Serialization.Tests
{
	using System;
	using System.Diagnostics;
	using System.IO;
	using System.Text;
	using Doxense.Testing;
	using NUnit.Framework;

	[TestFixture]
	[Category("Core-SDK")]
	public class Base64EncodingTest : DoxenseTest
	{

		[Test]
		public void Test_Basics()
		{

			byte[] data = new byte[1024];
			new Random().NextBytes(data);
			for (int i = 0; i < 256; i++) data[i] = (byte) i;

			string expected = Convert.ToBase64String(data, 0, 1024);
			Assert.That(Base64Encoding.ToBase64String(data.AsSlice(0, 1024)), Is.EqualTo(expected));
			Assert.That(Base64Encoding.ToBase64UrlString(data.AsSlice(0, 1024)), Is.EqualTo(expected.TrimEnd('=').Replace('+', '-').Replace('/', '_')));

			for (int i = 1; i < data.Length; i++)
			{
				expected = Convert.ToBase64String(data, 0, i);
				string actual = Base64Encoding.ToBase64String(data.AsSlice(0, i));

				if (expected != actual)
				{
					Assert.That(expected, Is.EqualTo(actual), "FAIL! " + i);
				}
			}
		}

		[Test, Category("Benchmark")]
		//[Ignore("Super long")]
		public void Bench_Compare_ToString_With_BCL()
		{
#if DEBUG
			Assert.Warn("The results of this test are invalid when run in DEBUG build!");
			Log("###########################################################################");
			Log("### DEBUG WARNING ### THIS BENCHMARK IS INVALID WHEN RUN IN DEBUG MODE! ###");
			Log("###########################################################################");
			Log();
#endif
			byte[] data = new byte[1024];
			new Random().NextBytes(data);
			for (int i = 0; i < 256; i++) data[i] = (byte)i;

			//WARMUP + JIT
			Assume.That(Base64Encoding.ToBase64String(data.AsSlice(0, 1024)), Is.EqualTo(Convert.ToBase64String(data, 0, 1024)));

			string s1 = null, s2 = null;
			var sw = new Stopwatch();

			TimeSpan durA = TimeSpan.Zero;
			TimeSpan durB = TimeSpan.Zero;


			const int R = 5;
			const int N = 250 * 1000;
			for (int r = -1; r < R; r++)
			{
				var gc = GC.CollectionCount(0);
				sw.Restart();
				for (int i = 0; i < N; i++)
				{
					s1 = Convert.ToBase64String(data, 0, (i % data.Length) + 1);
				}
				sw.Stop();
				gc = GC.CollectionCount(0) - gc;
				if (r >= 0)
				{
					Log($"#{r,-2} => BCL: {sw.Elapsed} for {N:N0} with {gc:N0} GC0 => {data.Length * N / (sw.Elapsed.TotalSeconds * 1048576),8:N1} MB/sec | {new string('A', (int)Math.Round(data.Length * N / (sw.Elapsed.TotalSeconds * 1048576 * 50), MidpointRounding.AwayFromZero))}");
					durA += sw.Elapsed;
				}
				GC.Collect();

				gc = GC.CollectionCount(0);
				sw.Restart();
				for (int i = 0; i < N; i++)
				{
					s2 = Base64Encoding.ToBase64String(data.AsSlice(0, (i % data.Length) + 1));
				}
				sw.Stop();
				gc = GC.CollectionCount(0) - gc;
				if (r >= 0)
				{
					Log($"#{r,-2} => DOX: {sw.Elapsed} for {N:N0} with {gc:N0} GC0 => {data.Length * N / (sw.Elapsed.TotalSeconds * 1048576),8:N1} MB/sec | {new string('B', (int) Math.Round(data.Length * N / (sw.Elapsed.TotalSeconds * 1048576 * 50), MidpointRounding.AwayFromZero))}");
					durB += sw.Elapsed;
				}
				GC.Collect();
			}

			Log($"### BCL {durA.TotalMilliseconds / R:N1} ms <- VS -> DOX {durB.TotalMilliseconds / R:N1} ms => x {durA.TotalSeconds / durB.TotalSeconds:N2}");

			Log("EXPECTED: {0}", s1);
			Log("ACTUAL  : {0}", s2);
			Assert.That(s2, Is.EqualTo(s1));
		}

		[Test, Category("Benchmark")]
		//[Ignore("Super long")]
		public void Bench_Compare_TextWriter_Append_With_BCL()
		{
#if DEBUG
			Assert.Warn("The results of this test are invalid when run in DEBUG build!");
			Log("###########################################################################");
			Log("### DEBUG WARNING ### THIS BENCHMARK IS INVALID WHEN RUN IN DEBUG MODE! ###");
			Log("###########################################################################");
			Log();
#endif
			byte[] data = new byte[256 * 1024];
			new Random().NextBytes(data);
			for (int i = 0; i < 256; i++) data[i] = (byte)i;

			//WARMUP + JIT
			Assume.That(Base64Encoding.ToBase64String(data.AsSlice(0, 1024)), Is.EqualTo(Convert.ToBase64String(data, 0, 1024)));

			const int N = 10 * 1000;

			GC.Collect();
			GC.WaitForPendingFinalizers();
			GC.Collect();

			{
				long gc = GC.CollectionCount(0);
				StringWriter buf = null;
				var sw = Stopwatch.StartNew();
				for (int i = 0; i < N; i++)
				{
					if (buf == null || (i & 0xFF) == 0) buf = new StringWriter(new StringBuilder(1024 * 1024));
					buf.Write(Convert.ToBase64String(data, 0, data.Length));
				}
				sw.Stop();
				gc = GC.CollectionCount(0) - gc;
				Log($"BCL: {sw.Elapsed} for {N:N0} with {gc:N0} GC0");
			}

			GC.Collect();
			GC.WaitForPendingFinalizers();
			GC.Collect();

			{
				long gc = GC.CollectionCount(0);
				StringWriter buf = null;
				var sw = Stopwatch.StartNew();
				for (int i = 0; i < N; i++)
				{
					if (buf == null || (i & 0xFF) == 0) buf = new StringWriter(new StringBuilder(1024 * 1024));
					Base64Encoding.EncodeTo(buf, data);
				}
				sw.Stop();
				gc = GC.CollectionCount(0) - gc;
				Log($"DOX: {sw.Elapsed} for {N:N0} with {gc:N0} GC0");
			}

			//Console.WriteLine("AFTER");
			//Console.ReadKey();

			Assert.That(true, Is.True); // pour faire plaisir a R#
		}

		[Test]
		public void Test_Versus()
		{
			byte[] original = Encoding.UTF8.GetBytes("Ecchi na no wa, ikenai to omoimasu!");

			for (int i = 0; i < original.Length; i++)
			{
				byte[] source = new byte[i];
				Array.Copy(original, source, i);

				string b64 = Convert.ToBase64String(source);
				byte[] decoded = Base64Encoding.FromBase64String(b64);
				if (!decoded.AsSlice().Equals(source))
				{
					Log("|" + b64 + "|");
					DumpVersus(source, decoded);
					Assert.That(decoded, Is.EqualTo(source), "Decode Base64 buffer does not match original");
				}
			}
		}

		[Test]
		public void Test_Subset()
		{
			byte[] original = Encoding.UTF8.GetBytes("Ecchi na no wa, ikenai to omoimasu!");

			for (int i = 0; i < original.Length; i++)
			{
				byte[] source = new byte[i];
				Array.Copy(original, source, i);

				string b64 = Base64Encoding.ToBase64UrlString(source);
				byte[] decoded = Base64Encoding.FromBase64UrlString(b64);
				if (!decoded.AsSlice().Equals(source))
				{
					Log("|" + b64 + "|");
					DumpVersus(source, decoded);
					Assert.That(decoded, Is.EqualTo(source));
				}
			}

			const int N = 10 * 1000;
			var rnd = new Random();
			for (int i = 0; i < N; i++)
			{
				int sz = rnd.Next(0, 255);
				var data = new byte[sz];

				rnd.NextBytes(data);

				string encoded = Base64Encoding.ToBase64UrlString(data);
				byte[] decoded = Base64Encoding.FromBase64UrlString(encoded);
				if (!decoded.AsSlice().Equals(data))
				{
					Log("|" + encoded + "|");
					DumpVersus(data, decoded);
					Assert.That(decoded, Is.EqualTo(data));
				}
			}

		}

	}

}
