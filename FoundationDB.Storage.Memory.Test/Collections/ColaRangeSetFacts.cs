using FoundationDB.Client;
using FoundationDB.Layers.Tuples;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace FoundationDB.Storage.Memory.Core.Test
{
	[TestFixture]
	public class ColaRangeSetFacts
	{

		[Test]
		public void Test_Empty_RangeSet()
		{
			var cola = new ColaRangeSet<int>(0, Comparer<int>.Default);
			Assert.That(cola.Count, Is.EqualTo(0));
			Assert.That(cola.Comparer, Is.SameAs(Comparer<int>.Default));
			Assert.That(cola.Capacity, Is.EqualTo(15), "Capacity should be the next power of 2, minus 1");
			Assert.That(cola.Bounds, Is.Not.Null);
			Assert.That(cola.Bounds.Begin, Is.EqualTo(0));
			Assert.That(cola.Bounds.End, Is.EqualTo(0));
		}

		[Test]
		public void Test_RangeSet_Insert_Non_Overlapping()
		{
			var cola = new ColaRangeSet<int>();
			Assert.That(cola.Count, Is.EqualTo(0));

			cola.Mark(0, 1);
			cola.Debug_Dump();
			Assert.That(cola.Count, Is.EqualTo(1));

			cola.Mark(2, 3);
			cola.Debug_Dump();
			Assert.That(cola.Count, Is.EqualTo(2));

			cola.Mark(4, 5);
			cola.Debug_Dump();
			Assert.That(cola.Count, Is.EqualTo(3));

			Assert.That(cola.Bounds.Begin, Is.EqualTo(0));
			Assert.That(cola.Bounds.End, Is.EqualTo(5));

			Console.WriteLine("Result = { " + String.Join(", ", cola) + " }");
			Console.WriteLine("Bounds = " + cola.Bounds);
		}

		[Test]
		public void Test_RangeSet_Insert_Partially_Overlapping()
		{
			var cola = new ColaRangeSet<int>();
			Assert.That(cola.Count, Is.EqualTo(0));

			cola.Mark(0, 1);
			cola.Debug_Dump();
			Assert.That(cola.Count, Is.EqualTo(1));

			cola.Mark(0, 2);
			cola.Debug_Dump();
			Assert.That(cola.Count, Is.EqualTo(1));

			cola.Mark(1, 3);
			cola.Debug_Dump();
			Assert.That(cola.Count, Is.EqualTo(1));

			cola.Mark(-1, 2);
			cola.Debug_Dump();
			Assert.That(cola.Count, Is.EqualTo(1));

			Console.WriteLine("Result = { " + String.Join(", ", cola) + " }");
			Console.WriteLine("Bounds = " + cola.Bounds);
		}

		[Test]
		public void Test_RangeSet_Insert_Completly_Overlapping()
		{
			var cola = new ColaRangeSet<int>();
			cola.Mark(1, 2);
			cola.Mark(4, 5);
			cola.Debug_Dump();
			Assert.That(cola.Count, Is.EqualTo(2));
			Assert.That(cola.Bounds.Begin, Is.EqualTo(1));
			Assert.That(cola.Bounds.End, Is.EqualTo(5));

			// overlaps the first range completely
			cola.Mark(0, 3);
			cola.Debug_Dump();
			Assert.That(cola.Count, Is.EqualTo(2));
			Assert.That(cola.Bounds.Begin, Is.EqualTo(0));
			Assert.That(cola.Bounds.End, Is.EqualTo(5));

			Console.WriteLine("Result = { " + String.Join(", ", cola) + " }");
			Console.WriteLine("Bounds = " + cola.Bounds);

		}

		[Test]
		public void Test_RangeSet_Insert_That_Join_Two_Ranges()
		{
			var cola = new ColaRangeSet<int>();
			cola.Mark(0, 1);
			cola.Mark(2, 3);
			cola.Debug_Dump();
			Assert.That(cola.Count, Is.EqualTo(2));

			cola.Mark(1, 2);
			cola.Debug_Dump();
			Assert.That(cola.Count, Is.EqualTo(1));

			Console.WriteLine("Result = { " + String.Join(", ", cola) + " }");
			Console.WriteLine("Bounds = " + cola.Bounds);
		}

		[Test]
		public void Test_RangeSet_Insert_That_Replace_All_Ranges()
		{
			var cola = new ColaRangeSet<int>();
			cola.Mark(0, 1);
			cola.Mark(2, 3);
			cola.Mark(4, 5);
			cola.Mark(6, 7);
			cola.Debug_Dump();
			Assert.That(cola.Count, Is.EqualTo(4));
			Assert.That(cola.Bounds.Begin, Is.EqualTo(0));
			Assert.That(cola.Bounds.End, Is.EqualTo(7));

			cola.Mark(-1, 10);
			cola.Debug_Dump();
			Assert.That(cola.Count, Is.EqualTo(1));
			Assert.That(cola.Bounds.Begin, Is.EqualTo(-1));
			Assert.That(cola.Bounds.End, Is.EqualTo(10));

			Console.WriteLine("Result = { " + String.Join(", ", cola) + " }");
			Console.WriteLine("Bounds = " + cola.Bounds);
		}

		[Test]
		public void Test_RangeSet_Insert_Points()
		{
			var cola = new ColaRangeSet<int>();
			cola.Mark(1);
			cola.Mark(2);
			cola.Mark(3);
			cola.Mark(4);
			cola.Debug_Dump();
			Assert.That(cola.Count, Is.EqualTo(4));
			Assert.That(cola.Bounds.Begin, Is.EqualTo(1));
			Assert.That(cola.Bounds.End, Is.EqualTo(4));

			// should replace 2 and 3
			cola.Mark(2, 3);

			Console.WriteLine("Result = { " + String.Join(", ", cola) + " }");
			Console.WriteLine("Bounds = " + cola.Bounds);
		}
	
		[Test]
		public void Test_RangeSet_Insert_Backwards()
		{
			const int N = 100;

			var cola = new ColaRangeSet<int>();

			for(int i = N; i > 0; i--)
			{
				int x = i << 1;
				cola.Mark(x - 1, x);
			}

			Assert.That(cola.Count, Is.EqualTo(N));

			Console.WriteLine("Result = { " + String.Join(", ", cola) + " }");
			Console.WriteLine("Bounds = " + cola.Bounds);
		}
	}
}
