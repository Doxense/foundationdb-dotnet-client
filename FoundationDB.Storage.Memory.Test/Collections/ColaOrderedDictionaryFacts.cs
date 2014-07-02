#region Copyright (c) 2013-2014, Doxense SAS. All rights reserved.
// See License.MD for license information
#endregion

namespace FoundationDB.Storage.Memory.Core.Test
{
	using NUnit.Framework;
	using System;
	using System.Collections.Generic;
	using System.Diagnostics;
	using System.Threading;

	[TestFixture]
	public class ColaOrderedDictionaryFacts
	{

		[Test]
		public void Test_Empty_Dictionary()
		{
			var cola = new ColaOrderedDictionary<int, string>(42, Comparer<int>.Default, StringComparer.Ordinal);
			Assert.That(cola.Count, Is.EqualTo(0));
			Assert.That(cola.KeyComparer, Is.SameAs(Comparer<int>.Default));
			Assert.That(cola.ValueComparer, Is.SameAs(StringComparer.Ordinal));
			Assert.That(cola.Capacity, Is.EqualTo(63), "Capacity should be the next power of 2, minus 1");
		}

		[Test]
		public void Test_ColaOrderedDictionary_Add()
		{
			var cmp = new CountingComparer<int>();

			var cola = new ColaOrderedDictionary<int, string>(cmp);
			Assert.That(cola.Count, Is.EqualTo(0));

			cola.Add(42, "42");
			cola.Debug_Dump();
			Assert.That(cola.Count, Is.EqualTo(1));
			Assert.That(cola.ContainsKey(42), Is.True);

			cola.Add(1, "1");
			cola.Debug_Dump();
			Assert.That(cola.Count, Is.EqualTo(2));
			Assert.That(cola.ContainsKey(1), Is.True);

			cola.Add(66, "66");
			cola.Debug_Dump();
			Assert.That(cola.Count, Is.EqualTo(3));
			Assert.That(cola.ContainsKey(66), Is.True);

			cola.Add(123, "123");
			cola.Debug_Dump();
			Assert.That(cola.Count, Is.EqualTo(4));
			Assert.That(cola.ContainsKey(123), Is.True);

			for(int i = 1; i < 100; i++)
			{
				cola.Add(-i, (-i).ToString());
			}
			cola.Debug_Dump();


			cmp.Reset();
			cola.ContainsKey(-99);
			Console.WriteLine("Lookup last inserted: " + cmp.Count);

			cmp.Reset();
			cola.ContainsKey(42);
			Console.WriteLine("Lookup first inserted: " + cmp.Count);

			cmp.Reset();
			cola.ContainsKey(77);
			Console.WriteLine("Lookup not found: " + cmp.Count);

			var keys = new List<int>();

			foreach(var kvp in cola)
			{
				Assert.That(kvp.Value, Is.EqualTo(kvp.Key.ToString()));
				keys.Add(kvp.Key);
			}

			Assert.That(keys.Count, Is.EqualTo(cola.Count));
			Assert.That(keys, Is.Ordered);
			Console.WriteLine(String.Join(", ", keys));

		}

		[Test]
		public void Test_ColaOrderedDictionary_Remove()
		{
			const int N = 100;

			// add a bunch of random values
			var rnd = new Random();
			int seed = 1333019583;// rnd.Next();
			Console.WriteLine("Seed " + seed);
			rnd = new Random(seed);

			var cola = new ColaOrderedDictionary<int, string>();
			var list = new List<int>();

			int x = 0;
			for (int i = 0; i < N; i++)
			{
				x += (1 + rnd.Next(10));
				string s = "value of " + x;

				cola.Add(x, s);
				list.Add(x);
			}
			Assert.That(cola.Count, Is.EqualTo(N));

			foreach(var item in list)
			{
				Assert.That(cola.ContainsKey(item), "{0} is missing", item);
			}

			cola.Debug_Dump();

			// now start removing items one by one
			while(list.Count > 0)
			{
				int p = rnd.Next(list.Count);
				x = list[p];
				list.RemoveAt(p);

				bool res = cola.Remove(x);
				if (!res) cola.Debug_Dump();
				Assert.That(res, Is.True, "Remove({0}) failed", x);

				Assert.That(cola.Count, Is.EqualTo(list.Count), "After removing {0}", x);
			}

			cola.Debug_Dump();

		}

		[Test]
		[Category("LongRunning")]
		public void Test_MiniBench()
		{
			const int N = 10 * 1000 * 1000;

			var rnd = new Random();
			long x;


			//WARMUP
			var store = new ColaOrderedDictionary<long, long>();
			store.Add(1, 1);
			store.Add(42, 42);
			store.Add(1234, 1234);
			store.TryGetValue(42, out x);
			store.TryGetValue(404, out x);

			#region Sequentially inserted....

			Console.WriteLine("Inserting " + N.ToString("N0") + " sequential key/value pairs into a COLA ordered dictionary");
			GC.Collect();
			store = new ColaOrderedDictionary<long, long>();
			long total = 0;
			var sw = Stopwatch.StartNew();
			for (int i = 0; i < N; i++)
			{
				store.SetItem(i, i);
				Interlocked.Increment(ref total);
				if (i % (N / 10) == 0) Console.Write(".");
			}
			sw.Stop();

			Console.WriteLine("done");
			Console.WriteLine("* Inserted: " + total.ToString("N0") + " keys");
			Console.WriteLine("* Elapsed : " + sw.Elapsed.TotalSeconds.ToString("N3") + " sec");
			Console.WriteLine("* KPS: " + (total / sw.Elapsed.TotalSeconds).ToString("N0") + " key/sec");
			Console.WriteLine("* Latency : " + (sw.Elapsed.TotalMilliseconds * 1000000 / total).ToString("N1") + " nanos / insert");

			// sequential reads

			sw.Restart();
			for (int i = 0; i < total; i++)
			{
				var _ = store.TryGetValue(i, out x);
				if (!_ || x != i) Assert.Fail();
			}
			sw.Stop();
			Console.WriteLine("SeqReadOrdered: " + total.ToString("N0") + " keys in " + sw.Elapsed.TotalSeconds.ToString("N3") + " sec => " + (total / sw.Elapsed.TotalSeconds).ToString("N0") + " kps");

			// random reads

			sw.Restart();
			for (int i = 0; i < total; i++)
			{
				var _ = store.TryGetValue(rnd.Next(N), out x);
				if (!_) Assert.Fail();
			}
			sw.Stop();
			Console.WriteLine("RndReadOrdered: " + total.ToString("N0") + " keys in " + sw.Elapsed.TotalSeconds.ToString("N3") + " sec => " + (total / sw.Elapsed.TotalSeconds).ToString("N0") + " kps");

			#endregion

			#region Randomly inserted....

			Console.WriteLine("(preparing random insert list)");

			var tmp = new long[N];
			var values = new long[N];
			for (int i = 0; i < N; i++)
			{
				tmp[i] = rnd.Next(N);
				values[i] = i;
			}
			Array.Sort(tmp, values);

			Console.WriteLine("Inserting " + N.ToString("N0") + " sequential keys into a COLA store");
			GC.Collect();
			store = new ColaOrderedDictionary<long, long>();
			total = 0;
			sw.Restart();
			for (int i = 0; i < N; i++)
			{
				store.Add(values[i], i);
				Interlocked.Increment(ref total);
				if (i % (N / 10) == 0) Console.Write(".");
			}
			sw.Stop();

			Console.WriteLine("done");
			Console.WriteLine("* Inserted: " + total.ToString("N0") + " keys");
			Console.WriteLine("* Elapsed : " + sw.Elapsed.TotalSeconds.ToString("N3") + " sec");
			Console.WriteLine("* KPS     : " + (total / sw.Elapsed.TotalSeconds).ToString("N0") + " key/sec");
			Console.WriteLine("* Latency : " + (sw.Elapsed.TotalMilliseconds * 1000000 / total).ToString("N1") + " nanos / insert");

			// sequential reads

			sw.Restart();
			for (int i = 0; i < total; i++)
			{
				var _ = store.TryGetValue(i, out x);
				if (!_) Assert.Fail();
			}
			sw.Stop();
			Console.WriteLine("SeqReadUnordered: " + total.ToString("N0") + " keys in " + sw.Elapsed.TotalSeconds.ToString("N3") + " sec => " + (total / sw.Elapsed.TotalSeconds).ToString("N0") + " kps");

			// random reads

			sw.Restart();
			for (int i = 0; i < total; i++)
			{
				var _ = store.TryGetValue(rnd.Next(N), out x);
				if (!_) Assert.Fail();
			}
			sw.Stop();
			Console.WriteLine("RndReadUnordered: " + total.ToString("N0") + " keys in " + sw.Elapsed.TotalSeconds.ToString("N3") + " sec => " + (total / sw.Elapsed.TotalSeconds).ToString("N0") + " kps");

			#endregion

		}

	}
}
