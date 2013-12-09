#region Copyright (c) 2013-2014, Doxense SAS. All rights reserved.
// See License.MD for license information
#endregion

namespace FoundationDB.Storage.Memory.Core.Test
{
	using NUnit.Framework;
	using System;
	using System.Collections.Generic;
	using System.Diagnostics;
	using System.Linq;
	using System.Text;
	using System.Threading;
	using System.Threading.Tasks;

	[TestFixture]
	public class ColaStoreFacts
	{
		[Test]
		public void Test_Bit_Twiddling()
		{
			Assert.That(ColaStore.LowestBit(0), Is.EqualTo(0));
			Assert.That(ColaStore.HighestBit(0), Is.EqualTo(0));

			Assert.That(ColaStore.LowestBit(42), Is.EqualTo(1));
			Assert.That(ColaStore.HighestBit(42), Is.EqualTo(5));

			for (int i = 1; i < 30; i++)
			{
				int x = 1 << i;
				Assert.That(ColaStore.LowestBit(x), Is.EqualTo(i));
				Assert.That(ColaStore.HighestBit(x), Is.EqualTo(i));

				Assert.That(ColaStore.HighestBit(x - 1), Is.EqualTo(i - 1));
				Assert.That(ColaStore.LowestBit(x - 1), Is.EqualTo(0));
			}

			Assert.That(ColaStore.LowestBit(0x60000000), Is.EqualTo(29));
			for (int i = 1; i < 30; i++)
			{
				int x = int.MaxValue - ((1 << i) - 1);
				Assert.That(ColaStore.LowestBit(x), Is.EqualTo(i), "i={0}, x={1} : {2}", i, x.ToString("X8"), Convert.ToString(x, 2));
			}
		}

		[Test]
		public void Test_Map_Index_To_Address()
		{
			// index => (level, offset)

			int level, offset;
			for (int i = 0; i < 1024; i++)
			{
				level = ColaStore.FromIndex(i, out offset);
				Assert.That(((1 << level) - 1) + offset, Is.EqualTo(i), "{0} => ({1}, {2})", i, level, offset);
			}
		}

		[Test]
		public void Test_Map_Address_Index()
		{
			// index => (level, offset)

			for (int level = 0; level <= 10; level++)
			{
				int n = 1 << level;
				for (int offset = 0; offset < n; offset++)
				{
					int index = ColaStore.ToIndex(level, offset);
					Assert.That(index, Is.EqualTo(n - 1 + offset), "({0}, {1}) => {2}", level, offset, index);
				}
			}
		}

		[Test]
		public void Test_Map_Offset_To_Index()
		{
			//N = 1
			// > 0 [0]
			Assert.That(ColaStore.MapOffsetToIndex(1, 0), Is.EqualTo(0));
			Assert.That(() => ColaStore.MapOffsetToIndex(1, 1), Throws.InstanceOf<ArgumentOutOfRangeException>());

			//N = 2
			// > 0 [_]
			// > 1 [0, 1]
			Assert.That(ColaStore.MapOffsetToIndex(2, 0), Is.EqualTo(1));
			Assert.That(ColaStore.MapOffsetToIndex(2, 1), Is.EqualTo(2));
			Assert.That(() => ColaStore.MapOffsetToIndex(2, 2), Throws.InstanceOf<ArgumentOutOfRangeException>());

			//N = 3
			// > 0 [2]
			// > 1 [0, 1]
			Assert.That(ColaStore.MapOffsetToIndex(3, 0), Is.EqualTo(1));
			Assert.That(ColaStore.MapOffsetToIndex(3, 1), Is.EqualTo(2));
			Assert.That(ColaStore.MapOffsetToIndex(3, 2), Is.EqualTo(0));
			Assert.That(() => ColaStore.MapOffsetToIndex(3, 3), Throws.InstanceOf<ArgumentOutOfRangeException>());

			//N = 5
			// > 0 [4]
			// > 1 [_, _]
			// > 2 [0, 1, 2, 3]
			Assert.That(ColaStore.MapOffsetToIndex(5, 0), Is.EqualTo(3));
			Assert.That(ColaStore.MapOffsetToIndex(5, 1), Is.EqualTo(4));
			Assert.That(ColaStore.MapOffsetToIndex(5, 2), Is.EqualTo(5));
			Assert.That(ColaStore.MapOffsetToIndex(5, 3), Is.EqualTo(6));
			Assert.That(ColaStore.MapOffsetToIndex(5, 4), Is.EqualTo(0));
			Assert.That(() => ColaStore.MapOffsetToIndex(5, 5), Throws.InstanceOf<ArgumentOutOfRangeException>());

			// N = 10
			// > 0 [_]
			// > 1 [8,9]
			// > 2 [_,_,_,_]
			// > 3 [0,1,2,3,4,5,6,7]
			for (int i = 0; i < 8; i++) Assert.That(ColaStore.MapOffsetToIndex(10, i), Is.EqualTo(7 + i), "MapOffset(10, {0})", i);
			for (int i = 8; i < 10; i++) Assert.That(ColaStore.MapOffsetToIndex(10, i), Is.EqualTo(1 + (i - 8)), "MapOffset(10, {0})", i);
			Assert.That(() => ColaStore.MapOffsetToIndex(10, 123), Throws.InstanceOf<ArgumentOutOfRangeException>());
		}

		[Test]
		public void Test_ColaStore_Iterator_Seek()
		{

			var store = new ColaStore<int?>(0, Comparer<int?>.Default);

			for (int i = 0; i < 10; i++)
			{
				store.Insert(i);
			}
			store.Debug_Dump();

			var iterator = store.GetIterator();

			Assert.That(iterator.Seek2(5, true), Is.True);
			Assert.That(iterator.Current, Is.EqualTo(5));

			Assert.That(iterator.Seek2(5, false), Is.True);
			Assert.That(iterator.Current, Is.EqualTo(4));

			Assert.That(iterator.Seek2(9, true), Is.True);
			Assert.That(iterator.Current, Is.EqualTo(9));

			Assert.That(iterator.Seek2(9, false), Is.True);
			Assert.That(iterator.Current, Is.EqualTo(8));

			Assert.That(iterator.Seek2(0, true), Is.True);
			Assert.That(iterator.Current, Is.EqualTo(0));

			Assert.That(iterator.Seek2(0, false), Is.False);
			Assert.That(iterator.Current, Is.Null);

			Assert.That(iterator.Seek2(10, true), Is.True);
			Assert.That(iterator.Current, Is.EqualTo(9));

			Assert.That(iterator.Seek2(10, false), Is.True);
			Assert.That(iterator.Current, Is.EqualTo(9));

		}

		[Test]
		public void Test_ColaStore_Iterator_Seek_Randomized()
		{
			const int N = 1000;

			var store = new ColaStore<int?>(0, Comparer<int?>.Default);

			var rnd = new Random();
			int seed = rnd.Next();
			Console.WriteLine("seed = " + seed);
			rnd = new Random(seed);

			var list = Enumerable.Range(0, N).ToList();
			while(list.Count > 0)
			{
				int p = rnd.Next(list.Count);
				store.Insert(list[p]);
				list.RemoveAt(p);
			}
			store.Debug_Dump();

			for (int i = 0; i < N; i++)
			{
				var iterator = store.GetIterator();

				int p = rnd.Next(N);
				bool orEqual = rnd.Next(2) == 0;

				bool res = iterator.Seek2(p, orEqual);

				if (orEqual)
				{ // the key should exist
					Assert.That(res, Is.True, "Seek({0}, '<=')", p);
					Assert.That(iterator.Current, Is.EqualTo(p), "Seek({0}, '<=')", p);
					Assert.That(iterator.Valid, Is.True, "Seek({0}, '<=')", p);
				}
				else if (p == 0)
				{ // there is no key before the first
					Assert.That(res, Is.False, "Seek(0, '<')");
					Assert.That(iterator.Current, Is.Null, "Seek(0, '<')");
					Assert.That(iterator.Valid, Is.False, "Seek(0, '<')");
				}
				else
				{ // the key should exist
					Assert.That(res, Is.True, "Seek({0}, '<')", p);
					Assert.That(iterator.Current, Is.EqualTo(p - 1), "Seek({0}, '<')", p);
					Assert.That(iterator.Valid, Is.True, "Seek({0}, '<')", p);
				}
			}

		}

		[Test]
		public void Test_ColaStore_Iterator_Seek_Then_Next_Randomized()
		{
			const int N = 1000;
			const int K = 10;

			var store = new ColaStore<int?>(0, Comparer<int?>.Default);

			var rnd = new Random();
			int seed = rnd.Next();
			Console.WriteLine("seed = " + seed);
			rnd = new Random(seed);

			var list = Enumerable.Range(0, N).ToList();
			while (list.Count > 0)
			{
				int p = rnd.Next(list.Count);
				store.Insert(list[p]);
				list.RemoveAt(p);
			}
			store.Debug_Dump();

			for (int i = 0; i < N; i++)
			{
				var iterator = store.GetIterator();

				int p = rnd.Next(N);
				bool orEqual = rnd.Next(2) == 0;

				if (p == 0 && !orEqual) continue; //TODO: what to do for this case ?

				Assert.That(iterator.Seek2(p, orEqual), Is.True);
				int? x = iterator.Current;
				Assert.That(x, Is.EqualTo(orEqual ? p : p - 1));

				// all the next should be ordered (starting from p)
				while (x < N - 1)
				{
					Assert.That(iterator.Next2(), Is.True, "Seek({0}).Current({1}).Next()", p, x);
					Assert.That(iterator.Current, Is.EqualTo(x + 1), "Seek({0}).Current({1}).Next()", p, x);
					++x;
				}
				// the following Next() should go past the end
				Assert.That(iterator.Next2(), Is.False);
				Assert.That(iterator.Current, Is.Null);
				Assert.That(iterator.Valid, Is.False);

				// re-seek to the original location
				Assert.That(iterator.Seek2(p, orEqual), Is.True);
				x = iterator.Current;
				Assert.That(x, Is.EqualTo(orEqual ? p : p - 1));

				// now go backwards
				while (x > 0)
				{
					Assert.That(iterator.Previous2(), Is.True, "Seek({0}).Current({1}).Previous()", p, x);
					Assert.That(iterator.Current, Is.EqualTo(x - 1), "Seek({0}).Current({1}).Previous()", p, x);
					--x;
				}
				// the following Previous() should go past the beginning
				Assert.That(iterator.Previous2(), Is.False);
				Assert.That(iterator.Current, Is.Null);
				Assert.That(iterator.Valid, Is.False);

				if (p >= K && p < N - K)
				{ // jitter dance

					// start to original location
					Assert.That(iterator.Seek2(p, true), Is.True);
					Assert.That(iterator.Current, Is.EqualTo(p));

					var sb = new StringBuilder();
					sb.Append("Seek -> ");
					for(int j = 0; j < K; j++)
					{
						x = iterator.Current;
						sb.Append(iterator.Current);
						if (rnd.Next(2) == 0)
						{ // next
							sb.Append(" -> ");
							Assert.That(iterator.Next2(), Is.True, "{0}", sb);
							Assert.That(iterator.Current, Is.EqualTo(x + 1), "{0} = ?", sb);
						}
						else
						{ // prev
							sb.Append(" <- ");
							Assert.That(iterator.Previous2(), Is.True, "{0}", sb);
							Assert.That(iterator.Current, Is.EqualTo(x - 1), "{0} = ?", sb);
						}
					}
				}

			}

		}

		[Test]
		public void Test_MiniBench()
		{
			const int N = (1 << 23) - 1; // 10 * 1000 * 1000;

			var rnd = new Random();
			int offset, level;
			long x;


			//WARMUP
			var store = new ColaStore<long>(0, Comparer<long>.Default);
			store.Insert(1);
			store.Insert(42);
			store.Insert(1234);
			level = store.Find(1, out offset, out x);

			const int BS = (N + 1) / 128;
			var timings = new List<TimeSpan>(BS);
			timings.Add(TimeSpan.Zero);
			timings.Clear();

			#region Sequentially inserted....

			Console.WriteLine("Inserting {0} sequential keys into a COLA store", N);
			GC.Collect();
			store = new ColaStore<long>(0, Comparer<long>.Default);
			long total = 0;
			var sw = Stopwatch.StartNew();
			for (int i = 0; i < N; i++)
			{

				int y = rnd.Next(100);

				level = store.Find(y, out offset, out x);
				if (level < 0) store.Insert(i);
				else store.SetAt(level, offset, x);

				Interlocked.Increment(ref total);
				if ((i % BS) == BS - 1)
				{
					sw.Stop();
					timings.Add(sw.Elapsed);
					Console.Write(".");
					sw.Start();
				}
			}
			sw.Stop();

			Console.WriteLine("done");
			Console.WriteLine("* Inserted: " + total.ToString("N0") + " keys");
			Console.WriteLine("* Elapsed : " + sw.Elapsed.TotalSeconds.ToString("N3") + " sec");
			Console.WriteLine("* KPS: " + (total / sw.Elapsed.TotalSeconds).ToString("N0") + " key/sec");
			Console.WriteLine("* Latency : " + (sw.Elapsed.TotalMilliseconds * 1000000 / total).ToString("N1") + " nanos / insert");
			for (int i = 0; i < timings.Count; i++)
			{
				Console.WriteLine("" + ((i + 1) * BS).ToString() + "\t" + timings[i].TotalSeconds);
			}
			return;
			// sequential reads

			sw.Restart();
			for (int i = 0; i < total; i++)
			{
				level = store.Find(i, out offset, out x);
			}
			sw.Stop();
			Console.WriteLine("SeqReadOrdered: " + total.ToString("N0") + " keys in " + sw.Elapsed.TotalSeconds.ToString("N3") + " sec => " + (total / sw.Elapsed.TotalSeconds).ToString("N0") + " kps");

			// random reads

			sw.Restart();
			for (int i = 0; i < total; i++)
			{
				level = store.Find(rnd.Next(N), out offset, out x);
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
			store = new ColaStore<long>(0, Comparer<long>.Default);
			total = 0;

			timings.Clear();

			sw.Restart();
			for (int i = 0; i < N; i++)
			{
				level = store.Find(i, out offset, out x);
				store.Insert(values[i]);
				Interlocked.Increment(ref total);
				if ((i % BS) == BS - 1)
				{
					sw.Stop();
					timings.Add(sw.Elapsed);
					Console.Write(".");
					sw.Start();
				}
			}
			sw.Stop();

			Console.WriteLine("done");
			Console.WriteLine("* Inserted: " + total.ToString("N0") + " keys");
			Console.WriteLine("* Elapsed : " + sw.Elapsed.TotalSeconds.ToString("N3") + " sec");
			Console.WriteLine("* KPS     : " + (total / sw.Elapsed.TotalSeconds).ToString("N0") + " key/sec");
			Console.WriteLine("* Latency : " + (sw.Elapsed.TotalMilliseconds * 1000000 / total).ToString("N1") + " nanos / insert");

			for (int i = 0; i < timings.Count;i++)
			{
				Console.WriteLine("" + ((i + 1) * BS).ToString() + "\t" + timings[i].TotalSeconds);
			}

			// sequential reads

			sw.Restart();
			for (int i = 0; i < total; i++)
			{
				level = store.Find(i, out offset, out x);
			}
			sw.Stop();
			Console.WriteLine("SeqReadUnordered: " + total.ToString("N0") + " keys in " + sw.Elapsed.TotalSeconds.ToString("N3") + " sec => " + (total / sw.Elapsed.TotalSeconds).ToString("N0") + " kps");

			// random reads

			sw.Restart();
			for (int i = 0; i < total; i++)
			{
				level = store.Find(rnd.Next(N), out offset, out x);
			}
			sw.Stop();
			Console.WriteLine("RndReadUnordered: " + total.ToString("N0") + " keys in " + sw.Elapsed.TotalSeconds.ToString("N3") + " sec => " + (total / sw.Elapsed.TotalSeconds).ToString("N0") + " kps");

			#endregion

		}

	}

}
