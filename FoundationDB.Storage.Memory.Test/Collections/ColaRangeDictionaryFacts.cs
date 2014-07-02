using FoundationDB.Client;
using FoundationDB.Layers.Tuples;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace FoundationDB.Storage.Memory.Core.Test
{
	[TestFixture]
	public class ColaRangeDictionaryFacts
	{

		[Test]
		public void Test_Empty_RangeDictionary()
		{
			var cola = new ColaRangeDictionary<int, string>(0, Comparer<int>.Default, StringComparer.Ordinal);
			Assert.That(cola.Count, Is.EqualTo(0));
			Assert.That(cola.KeyComparer, Is.SameAs(Comparer<int>.Default));
			Assert.That(cola.ValueComparer, Is.SameAs(StringComparer.Ordinal));
			Assert.That(cola.Capacity, Is.EqualTo(15), "Capacity should be the next power of 2, minus 1");
			Assert.That(cola.Bounds, Is.Not.Null);
			Assert.That(cola.Bounds.Begin, Is.EqualTo(0));
			Assert.That(cola.Bounds.End, Is.EqualTo(0));
		}

		[Test]
		public void Test_RangeDictionary_Insert_Single()
		{
			var cola = new ColaRangeDictionary<int, string>();
			Assert.That(cola.Count, Is.EqualTo(0));

			cola.Mark(0, 1, "A");
			Assert.That(cola.Count, Is.EqualTo(1));

			var items = cola.ToArray();
			Assert.That(items.Length, Is.EqualTo(1));
			Assert.That(items[0].Begin, Is.EqualTo(0));
			Assert.That(items[0].End, Is.EqualTo(1));
			Assert.That(items[0].Value, Is.EqualTo("A"));
		}

		[Test]
		public void Test_RangeDictionary_Insert_In_Order_Non_Overlapping()
		{
			var cola = new ColaRangeDictionary<int, string>();
			Assert.That(cola.Count, Is.EqualTo(0));

			cola.Mark(0, 1, "A");
			Console.WriteLine("FIRST  = { " + String.Join(", ", cola) + " }");
			Console.WriteLine("Bounds = " + cola.Bounds);
			cola.Debug_Dump();
			Assert.That(cola.Count, Is.EqualTo(1));

			cola.Mark(2, 3, "B");
			Console.WriteLine("SECOND = { " + String.Join(", ", cola) + " }");
			Console.WriteLine("Bounds = " + cola.Bounds);
			cola.Debug_Dump();
			Assert.That(cola.Count, Is.EqualTo(2));

			cola.Mark(4, 5, "C");
			Console.WriteLine("THIRD  = { " + String.Join(", ", cola) + " }");
			Console.WriteLine("Bounds = " + cola.Bounds);
			cola.Debug_Dump();

			Assert.That(cola.Count, Is.EqualTo(3));
			var runs = cola.ToArray();
			Assert.That(runs.Length, Is.EqualTo(3));

			Assert.That(runs[0].Begin, Is.EqualTo(0));
			Assert.That(runs[0].End, Is.EqualTo(1));
			Assert.That(runs[0].Value, Is.EqualTo("A"));

			Assert.That(runs[1].Begin, Is.EqualTo(2));
			Assert.That(runs[1].End, Is.EqualTo(3));
			Assert.That(runs[1].Value, Is.EqualTo("B"));

			Assert.That(runs[2].Begin, Is.EqualTo(4));
			Assert.That(runs[2].End, Is.EqualTo(5));
			Assert.That(runs[2].Value, Is.EqualTo("C"));

			Assert.That(cola.Bounds.Begin, Is.EqualTo(0));
			Assert.That(cola.Bounds.End, Is.EqualTo(5));
		}

		[Test]
		public void Test_RangeDictionary_Insert_Out_Of_Order_Non_Overlapping()
		{
			var cola = new ColaRangeDictionary<int, string>();
			Assert.That(cola.Count, Is.EqualTo(0));

			cola.Mark(0, 1, "A");
			Console.WriteLine("FIRST  = { " + String.Join(", ", cola) + " }");
			Console.WriteLine("Bounds = " + cola.Bounds);
			cola.Debug_Dump();
			Assert.That(cola.Count, Is.EqualTo(1));

			cola.Mark(4, 5, "B");
			Console.WriteLine("SECOND = { " + String.Join(", ", cola) + " }");
			Console.WriteLine("Bounds = " + cola.Bounds);
			cola.Debug_Dump();
			Assert.That(cola.Count, Is.EqualTo(2));

			cola.Mark(2, 3, "C");
			Console.WriteLine("THIRD  = { " + String.Join(", ", cola) + " }");
			Console.WriteLine("Bounds = " + cola.Bounds);
			cola.Debug_Dump();

			Assert.That(cola.Count, Is.EqualTo(3));
			var runs = cola.ToArray();
			Assert.That(runs.Length, Is.EqualTo(3));

			Assert.That(runs[0].Begin, Is.EqualTo(0));
			Assert.That(runs[0].End, Is.EqualTo(1));
			Assert.That(runs[0].Value, Is.EqualTo("A"));

			Assert.That(runs[1].Begin, Is.EqualTo(2));
			Assert.That(runs[1].End, Is.EqualTo(3));
			Assert.That(runs[1].Value, Is.EqualTo("C"));

			Assert.That(runs[2].Begin, Is.EqualTo(4));
			Assert.That(runs[2].End, Is.EqualTo(5));
			Assert.That(runs[2].Value, Is.EqualTo("B"));

			Assert.That(cola.Bounds.Begin, Is.EqualTo(0));
			Assert.That(cola.Bounds.End, Is.EqualTo(5));

		}
		[Test]
		public void Test_RangeDictionary_Insert_Partially_Overlapping()
		{
			var cola = new ColaRangeDictionary<int, string>();
			Assert.That(cola.Count, Is.EqualTo(0));

			cola.Mark(0, 1, "A");
			cola.Debug_Dump();
			Assert.That(cola.Count, Is.EqualTo(1));

			cola.Mark(0, 2, "B");
			cola.Debug_Dump();
			Assert.That(cola.Count, Is.EqualTo(1));

			cola.Mark(1, 3, "C");
			cola.Debug_Dump();
			Assert.That(cola.Count, Is.EqualTo(2));

			cola.Mark(-1, 2, "D");
			cola.Debug_Dump();
			Assert.That(cola.Count, Is.EqualTo(2));

			Console.WriteLine("Result = { " + String.Join(", ", cola) + " }");
			Console.WriteLine("Bounds = " + cola.Bounds);
		}

		[Test]
		public void Test_RangeDictionary_Insert_Completly_Overlapping()
		{
			var cola = new ColaRangeDictionary<int, string>();
			cola.Mark(4, 5, "A");
			Console.WriteLine("BEFORE = { " + String.Join(", ", cola) + " }");
			Console.WriteLine("Bounds = " + cola.Bounds);
			cola.Debug_Dump();
			Assert.That(cola.Count, Is.EqualTo(1));
			Assert.That(cola.Bounds.Begin, Is.EqualTo(4));
			Assert.That(cola.Bounds.End, Is.EqualTo(5));

			// overlaps all the ranges at once
			// 0123456789   0123456789   0123456789
			// ____A_____ + BBBBBBBBBB = BBBBBBBBBB
			cola.Mark(0, 10, "B");
			Console.WriteLine("AFTER  = { " + String.Join(", ", cola) + " }");
			Console.WriteLine("Bounds = " + cola.Bounds);
			cola.Debug_Dump();

			Assert.That(cola.Count, Is.EqualTo(1));
			var runs = cola.ToArray();
			Assert.That(runs.Length, Is.EqualTo(1));
			Assert.That(runs[0].Begin, Is.EqualTo(0));
			Assert.That(runs[0].End, Is.EqualTo(10));
			Assert.That(runs[0].Value, Is.EqualTo("B"));

			Assert.That(cola.Bounds.Begin, Is.EqualTo(0));
			Assert.That(cola.Bounds.End, Is.EqualTo(10));
		}

		[Test]
		public void Test_RangeDictionary_Insert_Contained()
		{
			var cola = new ColaRangeDictionary<int, string>();
			cola.Mark(0, 10, "A");
			Console.WriteLine("BEFORE = { " + String.Join(", ", cola) + " }");
			Console.WriteLine("Bounds = " + cola.Bounds);
			cola.Debug_Dump();
			Assert.That(cola.Count, Is.EqualTo(1));
			Assert.That(cola.Bounds.Begin, Is.EqualTo(0));
			Assert.That(cola.Bounds.End, Is.EqualTo(10));

			// overlaps all the ranges at once

			// 0123456789   0123456789   0123456789
			// AAAAAAAAAA + ____B_____ = AAAABAAAAA
			cola.Mark(4, 5, "B");
			Console.WriteLine("AFTER  = { " + String.Join(", ", cola) + " }");
			Console.WriteLine("Bounds = " + cola.Bounds);
			cola.Debug_Dump();
			Assert.That(cola.Count, Is.EqualTo(3));
			var items = cola.ToArray();
			Assert.That(items.Length, Is.EqualTo(3));

			Assert.That(items[0].Begin, Is.EqualTo(0));
			Assert.That(items[0].End, Is.EqualTo(4));
			Assert.That(items[0].Value, Is.EqualTo("A"));

			Assert.That(items[1].Begin, Is.EqualTo(4));
			Assert.That(items[1].End, Is.EqualTo(5));
			Assert.That(items[1].Value, Is.EqualTo("B"));

			Assert.That(items[2].Begin, Is.EqualTo(5));
			Assert.That(items[2].End, Is.EqualTo(10));
			Assert.That(items[2].Value, Is.EqualTo("A"));

			Assert.That(cola.Bounds.Begin, Is.EqualTo(0));
			Assert.That(cola.Bounds.End, Is.EqualTo(10));
		}

		[Test]
		public void Test_RangeDictionary_Insert_That_Fits_Between_Two_Ranges()
		{
			var cola = new ColaRangeDictionary<int, string>();
			cola.Mark(0, 1, "A");
			cola.Mark(2, 3, "B");
			cola.Debug_Dump();
			Assert.That(cola.Count, Is.EqualTo(2));

			cola.Mark(1, 2, "C");
			cola.Debug_Dump();
			Assert.That(cola.Count, Is.EqualTo(3));

			Console.WriteLine("Result = { " + String.Join(", ", cola) + " }");
			Console.WriteLine("Bounds = " + cola.Bounds);

			var items = cola.ToArray();
			Assert.That(items.Length, Is.EqualTo(3));

			Assert.That(items[0].Begin, Is.EqualTo(0));
			Assert.That(items[0].End, Is.EqualTo(1));
			Assert.That(items[0].Value, Is.EqualTo("A"));

			Assert.That(items[1].Begin, Is.EqualTo(1));
			Assert.That(items[1].End, Is.EqualTo(2));
			Assert.That(items[1].Value, Is.EqualTo("C"));

			Assert.That(items[2].Begin, Is.EqualTo(2));
			Assert.That(items[2].End, Is.EqualTo(3));
			Assert.That(items[2].Value, Is.EqualTo("B"));

		}

		[Test]
		public void Test_RangeDictionary_Insert_That_Join_Two_Ranges()
		{
			var cola = new ColaRangeDictionary<int, string>();
			cola.Mark(0, 1, "A");
			cola.Mark(2, 3, "A");
			Console.WriteLine("BEFORE = { " + String.Join(", ", cola) + " }");
			Console.WriteLine("Bounds = " + cola.Bounds);
			cola.Debug_Dump();
			Assert.That(cola.Count, Is.EqualTo(2));

			// A_A_ + _A__ = AAA_
			cola.Mark(1, 2, "A");
			Console.WriteLine("AFTER  = { " + String.Join(", ", cola) + " }");
			Console.WriteLine("Bounds = " + cola.Bounds);
			cola.Debug_Dump();

			Assert.That(cola.Count, Is.EqualTo(1));
			var runs = cola.ToArray();
			Assert.That(runs[0].Begin, Is.EqualTo(0));
			Assert.That(runs[0].End, Is.EqualTo(3));
			Assert.That(runs[0].Value, Is.EqualTo("A"));

		}

		[Test]
		public void Test_RangeDictionary_Insert_That_Replace_All_Ranges()
		{
			var cola = new ColaRangeDictionary<int, string>();
			cola.Mark(0, 1, "A");
			cola.Mark(2, 3, "A");
			cola.Mark(4, 5, "A");
			cola.Mark(6, 7, "A");
			cola.Debug_Dump();
			Assert.That(cola.Count, Is.EqualTo(4));
			Assert.That(cola.Bounds.Begin, Is.EqualTo(0));
			Assert.That(cola.Bounds.End, Is.EqualTo(7));

			cola.Mark(-1, 10, "B");
			cola.Debug_Dump();
			Assert.That(cola.Count, Is.EqualTo(1));
			Assert.That(cola.Bounds.Begin, Is.EqualTo(-1));
			Assert.That(cola.Bounds.End, Is.EqualTo(10));

			Console.WriteLine("Result = { " + String.Join(", ", cola) + " }");
			Console.WriteLine("Bounds = " + cola.Bounds);
		}

		[Test]
		public void Test_RangeDictionary_Insert_Backwards()
		{
			const int N = 100;

			var cola = new ColaRangeDictionary<int, string>();

			for(int i = N; i > 0; i--)
			{
				int x = i << 1;
				cola.Mark(x - 1, x, i.ToString());
			}

			Assert.That(cola.Count, Is.EqualTo(N));

			Console.WriteLine("Result = { " + String.Join(", ", cola) + " }");
			Console.WriteLine("Bounds = " + cola.Bounds);
		}

		[Test]
		public void Test_RangeDictionary_Insert_Random_Ranges()
		{
			const int N = 1000;
			const int K = 1000 * 1000;

			var cola = new ColaRangeDictionary<int, int>();

			var rnd = new Random();
			int seed = 2040305906; // rnd.Next();
			Console.WriteLine("seed " + seed);
			rnd = new Random(seed);

			int[] expected = new int[N];

			var sw = Stopwatch.StartNew();
			for (int i = 0; i < K; i++)
			{
				if (rnd.Next(10000) < 42)
				{
					//Console.WriteLine("Clear");
					cola.Clear();
				}
				else
				{

					int x = rnd.Next(N);
					int y = rnd.Next(2) == 0 ? x + 1 : rnd.Next(N);
					if (y == x) ++y;
					if (x <= y)
					{
						//Console.WriteLine();
						//Console.WriteLine("Add " + x + " ~ " + y + " = " + i);
						cola.Mark(x, y, i);
					}
					else
					{
						//Console.WriteLine();
						//Console.WriteLine("ddA " + y + " ~ " + x + " = " + i);
						cola.Mark(y, x, i);
					}
				}
				//Console.WriteLine("  = " + cola + " -- <> = " + cola.Bounds);
				//cola.Debug_Dump();
				
			}
			sw.Stop();

			Console.WriteLine("Inserted " + K.ToString("N0") + " random ranges in " + sw.Elapsed.TotalSeconds.ToString("N3") + " sec");
			cola.Debug_Dump();

			Console.WriteLine("Result = " + cola);
			Console.WriteLine("Bounds = " + cola.Bounds);

		}
	}
}
