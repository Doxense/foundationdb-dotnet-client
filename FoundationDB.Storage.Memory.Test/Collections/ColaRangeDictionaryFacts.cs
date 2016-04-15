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
		public void Test_Can_Remove()
		{
			var dico = GetFilledRange();
			//on supprime tout
			dico.Remove(0, 100, -100, (x, y) => x + y);
			Assert.That(dico.Count, Is.EqualTo(0));

			dico = GetFilledRange();
			//on ampute le premier range
			dico.Remove(0, 12, -12, (x, y) => x + y);
			Assert.That(dico.Count, Is.EqualTo(5));
			int i = 0;
			foreach (var entry in dico)
			{
				if (i == 0) CompareEntries(entry, 0, 3, true);
				else if (i == 1) CompareEntries(entry, 8, 38, false);
				else if (i == 2) CompareEntries(entry, 39, 50, true);
				else if (i == 3) CompareEntries(entry, 51, 53, true);
				else if (i == 4) CompareEntries(entry, 56, 63, false);
				i++;
			}

			//on supprime un truc a cheval sur plusieurs ranges
			dico = GetFilledRange();
			dico.Remove(12, 55, -43, (x, y) => x + y);
			Assert.That(dico.Count, Is.EqualTo(4));
			i = 0;
			foreach (var entry in dico)
			{
				if (i == 0) CompareEntries(entry, 10, 12, true);
				if (i == 1) CompareEntries(entry, 12, 19, true);
				else if (i == 2) CompareEntries(entry, 20, 22, true);
				else if (i == 3) CompareEntries(entry, 25, 32, false);
				i++;
			}

			//on supprime avant le début
			dico = GetFilledRange();
			dico.Remove(0, 8, -8, (x, y) => x + y);
			Assert.That(dico.Count, Is.EqualTo(5));
			i = 0;
			foreach (var entry in dico)
			{
				if (i == 0) CompareEntries(entry, 2, 7, true);
				else if (i == 1) CompareEntries(entry, 12, 42, false);
				else if (i == 2) CompareEntries(entry, 43, 54, true);
				else if (i == 3) CompareEntries(entry, 55, 57, true);
				else if (i == 4) CompareEntries(entry, 60, 67, false);
				i++;
			}

			//on supprimme exactement 2 ranges
			dico = GetFilledRange();
			dico.Remove(20, 62, -42, (x, y) => x + y);
			Assert.That(dico.Count, Is.EqualTo(3));
			i = 0;
			foreach (var entry in dico)
			{
				if (i == 0) CompareEntries(entry, 10, 15, true);
				else if (i == 1) CompareEntries(entry, 21, 23, true);
				else if (i == 2) CompareEntries(entry, 26, 33, false);
				i++;
			}

			//on supprime de maniere a ce que ca termine sur la fin d'un range
			dico = GetFilledRange();
			dico.Remove(0, 50, -50, (x, y) => x + y);
			Assert.That(dico.Count, Is.EqualTo(3));
			i = 0;
			foreach (var entry in dico)
			{
				if (i == 0) CompareEntries(entry, 1, 12, true);
				else if (i == 1) CompareEntries(entry, 13, 15, true);
				else if (i == 2) CompareEntries(entry, 18, 25, false);
				i++;
			}

			//on supprimme jusqu'a la fin du premier
			dico = GetFilledRange();
			dico.Remove(0, 15, -15, (x, y) => x + y);
			Assert.That(dico.Count, Is.EqualTo(4));
			i = 0;
			foreach (var entry in dico)
			{
				if (i == 0) CompareEntries(entry, 5, 35, false);
				else if (i == 1) CompareEntries(entry, 36, 47, true);
				else if (i == 2) CompareEntries(entry, 48, 50, true);
				else if (i == 3) CompareEntries(entry, 53, 60, false);
				i++;
			}

			//on supprime jusqu'au milieu du 3e
			dico = GetFilledRange();
			dico.Remove(0, 60, -60, (x, y) => x + y);
			Assert.That(dico.Count, Is.EqualTo(3));
			i = 0;
			foreach (var entry in dico)
			{
				if (i == 0) CompareEntries(entry, 0, 2, true);
				else if (i == 1) CompareEntries(entry, 3, 5, true);
				else if (i == 2) CompareEntries(entry, 8, 15, false);
				i++;
			}

			//on supprime jusqu'au debut du 3e
			dico = GetFilledRange();
			dico.Remove(0, 51, -51, (x, y) => x + y);
			Assert.That(dico.Count, Is.EqualTo(3));
			i = 0;
			foreach (var entry in dico)
			{
				if (i == 0) CompareEntries(entry, 0, 11, true);
				else if (i == 1) CompareEntries(entry, 12, 14, true);
				else if (i == 2) CompareEntries(entry, 17, 24, false);
				i++;
			}

			//on supprime le début du 2e
			dico = GetFilledRange();
			dico.Remove(20, 30, -10, (x, y) => x + y);
			Assert.That(dico.Count, Is.EqualTo(5));
			i = 0;
			foreach (var entry in dico)
			{
				if (i == 0) CompareEntries(entry, 10, 15, true);
				else if (i == 1) CompareEntries(entry, 20, 40, false);
				else if (i == 2) CompareEntries(entry, 41, 52, true);
				else if (i == 3) CompareEntries(entry, 53, 55, true);
				else if (i == 4) CompareEntries(entry, 58, 65, false);
				i++;
			}

			//on supprime le 2e
			dico = GetFilledRange();
			dico.Remove(20, 50, -30, (x, y) => x + y);
			Assert.That(dico.Count, Is.EqualTo(4));
			i = 0;
			foreach (var entry in dico)
			{
				if (i == 0) CompareEntries(entry, 10, 15, true);
				else if (i == 1) CompareEntries(entry, 21, 32, true);
				else if (i == 2) CompareEntries(entry, 33, 35, true);
				else if (i == 3) CompareEntries(entry, 38, 45, false);
				i++;
			}

			//on supprime le 1er et un bout du 2e
			dico = GetFilledRange();
			dico.Remove(10, 30, -20, (x, y) => x + y);
			Assert.That(dico.Count, Is.EqualTo(4));
			i = 0;
			foreach (var entry in dico)
			{
				if (i == 0) CompareEntries(entry, 10, 30, false);
				else if (i == 1) CompareEntries(entry, 31, 42, true);
				else if (i == 2) CompareEntries(entry, 43, 45, true);
				else if (i == 3) CompareEntries(entry, 48, 55, false);
				i++;
			}

			//on supprime un morceau du second
			dico = GetFilledRange();
			dico.Remove(30, 40, -10, (x, y) => x + y);
			Assert.That(dico.Count, Is.EqualTo(6));
			i = 0;
			foreach (var entry in dico)
			{
				if (i == 0) CompareEntries(entry, 10, 15, true);
				else if (i == 1) CompareEntries(entry, 20, 30, false);
				else if (i == 2) CompareEntries(entry, 30, 40, false);
				else if (i == 3) CompareEntries(entry, 41, 52, true);
				else if (i == 4) CompareEntries(entry, 53, 55, true);
				else if (i == 5) CompareEntries(entry, 58, 65, false);
				i++;
			}

			//on supprime la fin du second
			dico = GetFilledRange();
			dico.Remove(30, 50, -20, (x, y) => x + y);
			Assert.That(dico.Count, Is.EqualTo(5));
			i = 0;
			foreach (var entry in dico)
			{
				if (i == 0) CompareEntries(entry, 10, 15, true);
				else if (i == 1) CompareEntries(entry, 20, 30, false);
				else if (i == 2) CompareEntries(entry, 31, 42, true);
				else if (i == 3) CompareEntries(entry, 43, 45, true);
				else if (i == 4) CompareEntries(entry, 48, 55, false);
				i++;
			}
		}

		public void CompareEntries<TKey, TValue>(ColaRangeDictionary<int, bool>.Entry entry, TKey begin, TKey end, TValue value)
		{
			Assert.That(entry.Begin, Is.EqualTo(begin));
			Assert.That(entry.End, Is.EqualTo(end));
			Assert.That(entry.Value, Is.EqualTo(value));
		}

		public ColaRangeDictionary<int, bool> GetFilledRange()
		{
			//returns a colaRange prefilled with this :
			// {10-15, true}
			// {20-50, false}
			// {51,62, true}
			// {63,65, true}
			// {68,75, false}

			var cola = new ColaRangeDictionary<int, bool>();
			cola.Mark(10, 15, true);
			cola.Mark(20, 50, false);
			cola.Mark(51, 62, true);
			cola.Mark(63, 65, true);
			cola.Mark(68, 75, false);

			return cola;
		}

		enum RangeColor
		{
			Black,
			White
		}

		[Test]
		public void Test_RangeDictionary_Black_And_White()
		{
			// we have a space from 0 <= x < 100 that is empty
			// we insert a random serie of ranges that are either Black or White
			// after each run, we check that all ranges are correctly ordered, merged, and so on.

			const int S = 100; // [0, 100)
			const int N = 1000; // number of repetitions
			const int K = 25; // max number of ranges inserted per run

			var rnd = new Random();
			int seed = rnd.Next();
			Console.WriteLine("Using random seed " + seed);
			rnd = new Random(seed);

			for(int i = 0; i< N; i++)
			{
				var cola = new ColaRangeDictionary<int, RangeColor>();

				var witnessColors = new RangeColor?[S];
				var witnessIndexes = new int?[S];

				// choose a random number of ranges
				int k = rnd.Next(3, K);

				Trace.WriteLine("");
				Trace.WriteLine(String.Format("# Starting run {0} with {1} insertions", i, k));

				int p = 0;
				for(int j = 0; j<k;j++)
				{
					var begin = rnd.Next(S);
					// 50/50 of inserting a single element, or a range
					var end = (rnd.Next(2) == 1 ? begin : rnd.Next(2) == 1 ? rnd.Next(begin, S) : Math.Min(S - 1, begin + rnd.Next(5))) + 1; // reminder: +1 because 'end' is EXCLUDED 
					Assert.That(begin, Is.LessThan(end));
					// 50/50 for the coloring
					var color = rnd.Next(2) == 1 ? RangeColor.White : RangeColor.Black;

					// uncomment this line if you want to reproduce this exact run
					//Console.WriteLine("\t\tcola.Mark(" + begin + ", " + end + ", RangeColor." + color + ");");

					cola.Mark(begin, end, color);
					for(int z = begin; z < end; z++)
					{
						witnessColors[z] = color;
						witnessIndexes[z] = p;
					}

					//Console.WriteLine(" >        |{0}|", String.Join("", witnessIndexes.Select(x => x.HasValue ? (char)('A' + x.Value) : ' ')));
					Debug.WriteLine("          |{0}| + [{1,2}, {2,2}) = {3} > #{4,2} [ {5} ]", String.Join("", witnessColors.Select(w => !w.HasValue ? ' ' : w.Value == RangeColor.Black ? '#' : '°')), begin, end, color, cola.Count, String.Join(", ", cola));

					++p;
				}

				// pack the witness list into ranges
				var witnessRanges = new List<FdbTuple<int, int, RangeColor>>();
				RangeColor? prev = null;
				p = 0;
				for (int z = 1; z < S;z++)
				{
					if (witnessColors[z] != prev)
					{ // switch

						if (prev.HasValue)
						{
							witnessRanges.Add(FdbTuple.Create(p, z, prev.Value));
						}
						p = z;
						prev = witnessColors[z];
					}
				}

				Trace.WriteLine(String.Format("> RANGES: #{0,2} [ {1} ]", cola.Count, String.Join(", ", cola)));
				Trace.WriteLine(String.Format("          #{0,2} [ {1} ]", witnessRanges.Count, String.Join(", ", witnessRanges)));

				var counter = new int[S];
				var observedIndexes = new int?[S];
				var observedColors = new RangeColor?[S];
				p = 0;
				foreach(var range in cola)
				{
					Assert.That(range.Begin < range.End, "Begin < End {0}", range);
					for (int z = range.Begin; z < range.End; z++)
					{
						observedIndexes[z] = p;
						counter[z]++;
						observedColors[z] = range.Value;
					}
					++p;
				}

				Trace.WriteLine(String.Format("> INDEXS: |{0}|", String.Join("", observedIndexes.Select(x => x.HasValue ? (char)('A' + x.Value) : ' '))));
				Trace.WriteLine(String.Format("          |{0}|", String.Join("", witnessIndexes.Select(x => x.HasValue ? (char)('A' + x.Value) : ' '))));

				Trace.WriteLine(String.Format("> COLORS: |{0}|", String.Join("", observedColors.Select(w => !w.HasValue ? ' ' : w.Value == RangeColor.Black ? '#' : '°'))));
				Trace.WriteLine(String.Format("          |{0}|", String.Join("", witnessColors.Select(w => !w.HasValue ? ' ' : w.Value == RangeColor.Black ? '#' : '°'))));

				// verify the colors
				foreach(var range in cola)
				{
					for (int z = range.Begin; z < range.End; z++)
					{
						Assert.That(range.Value, Is.EqualTo(witnessColors[z]), "#{0} color mismatch for {1}", z, range);
						Assert.That(counter[z], Is.EqualTo(1), "Duplicate at offset #{0} for {1}", z, range);
					}
				}

				// verify that nothing was missed
				for(int z = 0; z < S; z++)
				{
					if (witnessColors[z] == null)
					{
						if (counter[z] != 0) Trace.WriteLine("@ FAIL!!! |" + new string('-', z) + "^");
						Assert.That(counter[z], Is.EqualTo(0), "Should be void at offset {0}", z);
					}
					else
					{
						if (counter[z] != 1) Trace.WriteLine("@ FAIL!!! |" + new string('-', z) + "^");
						Assert.That(counter[z], Is.EqualTo(1), "Should be filled with {1} at offset {0}", z, witnessColors[z]);
					}
				}
			}
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
