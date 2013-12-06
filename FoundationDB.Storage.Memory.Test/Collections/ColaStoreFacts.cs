#region Copyright (c) 2013-2014, Doxense SAS. All rights reserved.
// See License.MD for license information
#endregion

namespace FoundationDB.Storage.Memory.Core.Test
{
	using NUnit.Framework;
	using System;
	using System.Collections.Generic;
	using System.Linq;
	using System.Text;

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

	}

}
