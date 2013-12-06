#region Copyright (c) 2013-2014, Doxense SAS. All rights reserved.
// See License.MD for license information
#endregion

namespace FoundationDB.Storage.Memory.Core.Test
{
	using FoundationDB.Client;
	using FoundationDB.Layers.Tuples;
	using NUnit.Framework;
	using System;
	using System.Collections.Generic;
	using System.Globalization;
	using System.Linq;

	[TestFixture]
	public class ColaOrderedSetFacts
	{

		[Test]
		public void Test_Empty_ColaSet()
		{
			var cola = new ColaOrderedSet<string>(42, StringComparer.Ordinal);
			Assert.That(cola.Count, Is.EqualTo(0));
			Assert.That(cola.Comparer, Is.SameAs(StringComparer.Ordinal));
			Assert.That(cola.Capacity, Is.EqualTo(63), "Capacity should be the next power of 2, minus 1");
		}

		[Test]
		public void Test_Capacity_Is_Rounded_Up()
		{
			// default capacity is 4 levels, for 31 items max
			var cola = new ColaOrderedSet<string>();
			Assert.That(cola.Capacity, Is.EqualTo(31));

			// 63 items completely fill 5 levels
			cola = new ColaOrderedSet<string>(63);
			Assert.That(cola.Capacity, Is.EqualTo(63));

			// 64 items need 6 levels, which can hold up to 127 items
			cola = new ColaOrderedSet<string>(64);
			Assert.That(cola.Capacity, Is.EqualTo(127));
		}

		[Test]
		public void Test_ColaOrderedSet_Add()
		{
			var cola = new ColaOrderedSet<int?>();
			Assert.That(cola.Count, Is.EqualTo(0));

			cola.Add(42);
			cola.Debug_Dump();
			Assert.That(cola.Count, Is.EqualTo(1));
			Assert.That(cola.Contains(42), Is.True);

			cola.Add(1);
			cola.Debug_Dump();
			Assert.That(cola.Count, Is.EqualTo(2));
			Assert.That(cola.Contains(1), Is.True);

			cola.Add(66);
			cola.Debug_Dump();
			Assert.That(cola.Count, Is.EqualTo(3));
			Assert.That(cola.Contains(66), Is.True);

			cola.Add(123);
			cola.Debug_Dump();
			Assert.That(cola.Count, Is.EqualTo(4));
			Assert.That(cola.Contains(123), Is.True);

			cola.Add(-77);
			cola.Add(-76);
			cola.Add(-75);
			cola.Add(-74);
			cola.Add(-73);
			cola.Debug_Dump();
			Assert.That(cola.Count, Is.EqualTo(9));
		}

		[Test]
		public void Test_ColaOrderedSet_Remove()
		{
			const int N = 1000;

			var cola = new ColaOrderedSet<int>();
			var list = new List<int>();

			for (int i = 0; i < N;i++)
			{
				cola.Add(i);
				list.Add(i);
			}
			cola.Debug_Dump();
			Assert.That(cola.Count, Is.EqualTo(N));

			for (int i = 0; i < N; i++)
			{
				Assert.That(cola.Contains(list[i]), Is.True, "{0} is missing (offset {1})", list[i], i);
			}

			var rnd = new Random();
			int seed = 1073704892; // rnd.Next();
			Console.WriteLine("Seed: " + seed);
			rnd = new Random(seed);
			int old = -1;
			while (list.Count > 0)
			{
				int p = rnd.Next(list.Count);
				int x = list[p]; 
				if (!cola.Contains(x))
				{
					Assert.Fail("{0} disapeared after removing {1} ?", x, old);
				}

				bool res = cola.Remove(x);
				Assert.That(res, Is.True, "Removing {0} did nothing", x);
				//Assert.That(cola.Contains(191), "blah {0}", x);

				list.RemoveAt(p);
				Assert.That(cola.Count, Is.EqualTo(list.Count));
				old = x;
			}
			cola.Debug_Dump();

		}

		[Test]
		public void Test_CopyTo_Return_Ordered_Values()
		{
			const int N = 1000;
			var rnd = new Random();

			var cola = new ColaOrderedSet<int>();

			// create a list of random values
			var numbers = new int[N];
			for (int i = 0, x = 0; i < N; i++) numbers[i] = (x += 1 + rnd.Next(10));

			// insert the list in a random order
			var list = new List<int>(numbers);
			while(list.Count > 0)
			{
				int p = rnd.Next(list.Count);
				cola.Add(list[p]);
				list.RemoveAt(p);
			}
			cola.Debug_Dump();
			Assert.That(cola.Count, Is.EqualTo(N));

			// we can now sort the numbers to get a reference sequence
			Array.Sort(numbers);

			// foreach()ing should return the value in natural order
			list.Clear();
			foreach (var x in cola) list.Add(x);
			Assert.That(list.Count, Is.EqualTo(N));
			Assert.That(list, Is.EqualTo(numbers));

			// CopyTo() should produce the item in the expected order
			var tmp = new int[N];
			cola.CopyTo(tmp);
			Assert.That(tmp, Is.EqualTo(numbers));

			// ToArray() should do the same thing
			tmp = cola.ToArray();
			Assert.That(tmp, Is.EqualTo(numbers));

		}

		[Test]
		public void Test_Check_Costs()
		{
			const int N = 100;
			var cmp = new CountingComparer<Slice>(Comparer<Slice>.Default);
			var cola = new ColaOrderedSet<Slice>(cmp);

			Console.WriteLine(String.Format(CultureInfo.InvariantCulture, "Parameters: N = {0}, Log(N) = {1}, Log2(N) = {2}, N.Log2(N) = {3}", N, Math.Log(N), Math.Log(N, 2), N * Math.Log(N, 2)));

			Console.WriteLine("Inserting (" + N + " items)");
			for (int i = 0; i < N; i++)
			{
				cola.Add(FdbTuple.Pack(i << 1));
			}

			Console.WriteLine("> " + cmp.Count + " cmps (" + ((double)cmp.Count / N) + " / insert)");
			cola.Debug_Dump();

			Console.WriteLine("Full scan (" + (N << 1) + " lookups)");
			cmp.Reset();
			int n = 0;
			for (int i = 0; i < (N << 1); i++)
			{
				if (cola.Contains(FdbTuple.Pack(i))) ++n;
			}
			Assert.That(n, Is.EqualTo(N));
			Console.WriteLine("> " + cmp.Count + " cmps (" + ((double)cmp.Count / (N << 1)) + " / lookup)");

			cmp.Reset();
			n = 0;
			int tail = Math.Min(16, N >> 1);
			int offset = N - tail;
			Console.WriteLine("Tail scan (" + tail + " lookups)");
			for (int i = 0; i < tail; i++)
			{
				if (cola.Contains(FdbTuple.Pack(offset + i))) ++n;
			}
			Console.WriteLine("> " + cmp.Count + " cmps (" + ((double)cmp.Count / tail) + " / lookup)");

			Console.WriteLine("ForEach");
			cmp.Reset();
			int p = 0;
			foreach(var x in cola)
			{
				Assert.That(FdbTuple.UnpackSingle<int>(x), Is.EqualTo(p << 1));
				++p;
			}
			Assert.That(p, Is.EqualTo(N));
			Console.WriteLine("> " + cmp.Count + " cmps (" + ((double)cmp.Count / N) + " / item)");
		}

	}
}
