#region Copyright (c) 2005-2023 Doxense SAS
// See License.MD for license information
#endregion

namespace Doxense.IO.Tests
{
	using System;
	using System.Collections.Generic;
	using System.Linq;
	using Doxense.Memory;
	using Doxense.Testing;
	using NUnit.Framework;

	/// <summary>Actor that pushes messages around until a counter reaches 0</summary>
	[TestFixture]
	[Category("Core-SDK")]
	public class BitMapFacts : DoxenseTest
	{

		[Test]
		public void Test_BitMap64_Set()
		{
			var map = new BitMap64(128);
			Assume.That(map.ToString("B"), Is.EqualTo("00000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000"));
			Assert.That(map.Population(), Is.EqualTo(0));

			map.Set(0);
			Assert.That(map.ToString("B"), Is.EqualTo("10000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000"));
			Assert.That(map.Population(), Is.EqualTo(1));

			map.Set(7);
			Assert.That(map.ToString("B"), Is.EqualTo("10000001000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000"));
			Assert.That(map.Population(), Is.EqualTo(2));

			map.Set(8);
			Assert.That(map.ToString("B"), Is.EqualTo("10000001100000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000"));
			Assert.That(map.Population(), Is.EqualTo(3));

			map.Set(42);
			Assert.That(map.ToString("B"), Is.EqualTo("10000001100000000000000000000000000000000010000000000000000000000000000000000000000000000000000000000000000000000000000000000000"));
			Assert.That(map.Population(), Is.EqualTo(4));

			map.Set(63);
			Assert.That(map.ToString("B"), Is.EqualTo("10000001100000000000000000000000000000000010000000000000000000010000000000000000000000000000000000000000000000000000000000000000"));
			Assert.That(map.Population(), Is.EqualTo(5));

			map.Flip(64);
			Assert.That(map.ToString("B"), Is.EqualTo("10000001100000000000000000000000000000000010000000000000000000011000000000000000000000000000000000000000000000000000000000000000"));
			Assert.That(map.Population(), Is.EqualTo(6));

			map[127] = true;
			Assert.That(map.ToString("B"), Is.EqualTo("10000001100000000000000000000000000000000010000000000000000000011000000000000000000000000000000000000000000000000000000000000001"));
			Assert.That(map.Population(), Is.EqualTo(7));

			map.Toggle(126, true);
			Assert.That(map.ToString("B"), Is.EqualTo("10000001100000000000000000000000000000000010000000000000000000011000000000000000000000000000000000000000000000000000000000000011"));
			Assert.That(map.Population(), Is.EqualTo(8));

			map[42] = true; // already set
			Assert.That(map.ToString("B"), Is.EqualTo("10000001100000000000000000000000000000000010000000000000000000011000000000000000000000000000000000000000000000000000000000000011"));
			Assert.That(map.Population(), Is.EqualTo(8));

			Assert.That(() => map.Set(128), Throws.InstanceOf<IndexOutOfRangeException>());
			Assert.That(map.ToString("B"), Is.EqualTo("10000001100000000000000000000000000000000010000000000000000000011000000000000000000000000000000000000000000000000000000000000011"));

			Assert.That(() => map.Set(-1), Throws.InstanceOf<IndexOutOfRangeException>());
			Assert.That(map.ToString("B"), Is.EqualTo("10000001100000000000000000000000000000000010000000000000000000011000000000000000000000000000000000000000000000000000000000000011"));

			Assert.That(map.ToString("X"), Is.EqualTo("8000040000000181C000000000000001"));
		}

		[Test]
		public void Test_BitMap64_Clear()
		{
			const int BITS = 128;
			var map = new BitMap64(BITS);
			map.SetAll();
			Assume.That(map.ToString("B"), Is.EqualTo("11111111111111111111111111111111111111111111111111111111111111111111111111111111111111111111111111111111111111111111111111111111"));
			Assert.That(map.Population(), Is.EqualTo(BITS));

			map.Clear(0);
			Assert.That(map.ToString("B"), Is.EqualTo("01111111111111111111111111111111111111111111111111111111111111111111111111111111111111111111111111111111111111111111111111111111"));
			Assert.That(map.Population(), Is.EqualTo(BITS - 1));

			map.Clear(7);
			Assert.That(map.ToString("B"), Is.EqualTo("01111110111111111111111111111111111111111111111111111111111111111111111111111111111111111111111111111111111111111111111111111111"));
			Assert.That(map.Population(), Is.EqualTo(BITS - 2));

			map.Clear(8);
			Assert.That(map.ToString("B"), Is.EqualTo("01111110011111111111111111111111111111111111111111111111111111111111111111111111111111111111111111111111111111111111111111111111"));
			Assert.That(map.Population(), Is.EqualTo(BITS - 3));

			map.Clear(42);
			Assert.That(map.ToString("B"), Is.EqualTo("01111110011111111111111111111111111111111101111111111111111111111111111111111111111111111111111111111111111111111111111111111111"));
			Assert.That(map.Population(), Is.EqualTo(BITS - 4));

			map.Clear(63);
			Assert.That(map.ToString("B"), Is.EqualTo("01111110011111111111111111111111111111111101111111111111111111101111111111111111111111111111111111111111111111111111111111111111"));
			Assert.That(map.Population(), Is.EqualTo(BITS - 5));

			map.Flip(64);
			Assert.That(map.ToString("B"), Is.EqualTo("01111110011111111111111111111111111111111101111111111111111111100111111111111111111111111111111111111111111111111111111111111111"));
			Assert.That(map.Population(), Is.EqualTo(BITS - 6));

			map[127] = false;
			Assert.That(map.ToString("B"), Is.EqualTo("01111110011111111111111111111111111111111101111111111111111111100111111111111111111111111111111111111111111111111111111111111110"));
			Assert.That(map.Population(), Is.EqualTo(BITS - 7));

			map.Toggle(126, false);
			Assert.That(map.ToString("B"), Is.EqualTo("01111110011111111111111111111111111111111101111111111111111111100111111111111111111111111111111111111111111111111111111111111100"));
			Assert.That(map.Population(), Is.EqualTo(BITS - 8));

			map[42] = false; // already cleared
			Assert.That(map.ToString("B"), Is.EqualTo("01111110011111111111111111111111111111111101111111111111111111100111111111111111111111111111111111111111111111111111111111111100"));
			Assert.That(map.Population(), Is.EqualTo(BITS - 8));

			Assert.That(() => map.Set(128), Throws.InstanceOf<IndexOutOfRangeException>());
			Assert.That(map.ToString("B"), Is.EqualTo("01111110011111111111111111111111111111111101111111111111111111100111111111111111111111111111111111111111111111111111111111111100"));

			Assert.That(() => map.Set(-1), Throws.InstanceOf<IndexOutOfRangeException>());
			Assert.That(map.ToString("B"), Is.EqualTo("01111110011111111111111111111111111111111101111111111111111111100111111111111111111111111111111111111111111111111111111111111100"));

			Assert.That(map.ToString("X"), Is.EqualTo("7FFFFBFFFFFFFE7E3FFFFFFFFFFFFFFE"));
		}

		[Test]
		public void Test_BitMap64_Population()
		{
			const int BITS = 64 + 128 + 256;
			var map = new BitMap64(BITS);
			Assert.That(map.Population(), Is.EqualTo(0));
			map[42] = true;
			map[128] = true;
			map[BITS - 1] = true;
			Assert.That(map.Population(), Is.EqualTo(3));

			map.SetAll();
			Assert.That(map.Population(),Is.EqualTo(BITS));

			map.Clear(42);
			Assert.That(map.Population(), Is.EqualTo(BITS - 1));

			map.ClearAll();
			Assert.That(map.Population(), Is.EqualTo(0));
		}

		[Test]
		public void Test_BitMap46_ClearAll_ResetAll()
		{
			const int BITS = 128;

			var map = new BitMap64(BITS);
			map[42] = true;
			map[63] = true;
			Assert.That(map.Population(), Is.EqualTo(2));

			// clear all bits
			map.ClearAll();
			// should be filled with zeroes
			Assert.That(map.ToString("X"), Is.EqualTo("00000000000000000000000000000000"));
			Assert.That(map.Population(), Is.EqualTo(0));
			Assert.That(map.GetLowestBit(), Is.EqualTo(-1L));
			Assert.That(map.GetHighestBit(), Is.EqualTo(-1L));

			// set all bits
			map.SetAll();
			// should be filled with ones
			Assert.That(map.ToString("X"), Is.EqualTo("FFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFF"));
			Assert.That(map.Population(), Is.EqualTo(BITS));
			Assert.That(map.GetLowestBit(), Is.EqualTo(0));
			Assert.That(map.GetHighestBit(), Is.EqualTo(BITS - 1));

			map[42] = false;
			Assert.That(map.ToString("X"), Is.EqualTo("FFFFFBFFFFFFFFFFFFFFFFFFFFFFFFFF"));
			Assert.That(map.Population(), Is.EqualTo(BITS - 1));
			Assert.That(map.GetLowestBit(), Is.EqualTo(0));
			Assert.That(map.GetHighestBit(), Is.EqualTo(BITS - 1));
		}

		[Test]
		public void Test_BitMap64_SetClear_And_Count()
		{
			var map = new BitMap64(128);
			long pop = 0;
			Assert.That(map.Population(), Is.EqualTo(0));

			map.SetAndCount(42, ref pop);
			Assume.That(map.GetSetBits().ToArray(), Is.EqualTo(new [] { 42 }));
			Assert.That(pop, Is.EqualTo(1));
			Assert.That(map.Population(), Is.EqualTo(pop));

			map.SetAndCount(63, ref pop);
			Assume.That(map.GetSetBits().ToArray(), Is.EqualTo(new[] { 42, 63 }));
			Assert.That(pop, Is.EqualTo(2));
			Assert.That(map.Population(), Is.EqualTo(pop));

			// already set
			map.SetAndCount(42, ref pop);
			Assume.That(map.GetSetBits().ToArray(), Is.EqualTo(new[] { 42, 63 }));
			Assert.That(pop, Is.EqualTo(2));
			Assert.That(map.Population(), Is.EqualTo(pop));

			// out of bounds
			Assert.That(() => map.SetAndCount(256, ref pop), Throws.Exception);
			Assume.That(map.GetSetBits().ToArray(), Is.EqualTo(new[] { 42, 63 }));
			Assert.That(pop, Is.EqualTo(2));
			Assert.That(map.Population(), Is.EqualTo(pop));

			// remove non existing
			map.ClearAndCount(79, ref pop);
			Assume.That(map.GetSetBits().ToArray(), Is.EqualTo(new[] { 42, 63 }));
			Assert.That(pop, Is.EqualTo(2));
			Assert.That(map.Population(), Is.EqualTo(pop));

			// remove existing
			map.ClearAndCount(63, ref pop);
			Assume.That(map.GetSetBits().ToArray(), Is.EqualTo(new[] { 42 }));
			Assert.That(pop, Is.EqualTo(1));
			Assert.That(map.Population(), Is.EqualTo(pop));

			// remove existing again
			map.ClearAndCount(63, ref pop);
			Assume.That(map.GetSetBits().ToArray(), Is.EqualTo(new[] { 42 }));
			Assert.That(pop, Is.EqualTo(1));
			Assert.That(map.Population(), Is.EqualTo(pop));

			// remove last
			map.ClearAndCount(42, ref pop);
			Assume.That(map.GetSetBits().ToArray(), Is.EqualTo(new int[0]));
			Assert.That(pop, Is.EqualTo(0));
			Assert.That(map.Population(), Is.EqualTo(pop));

			// remove any
			map.ClearAndCount(123, ref pop);
			Assume.That(map.GetSetBits().ToArray(), Is.EqualTo(new int[0]));
			Assert.That(pop, Is.EqualTo(0));
			Assert.That(map.Population(), Is.EqualTo(pop));

		}

		[Test]
		public void Test_BitMap64_Lowest_Highest_Bit()
		{
			const int BITS = 128;

			var map = new BitMap64(BITS);
			Assert.That(map.GetLowestBit(), Is.EqualTo(-1));
			Assert.That(map.GetHighestBit(), Is.EqualTo(-1));

			map[42] = true;
			Assert.That(map.GetLowestBit(), Is.EqualTo(42));
			Assert.That(map.GetHighestBit(), Is.EqualTo(42));

			map[63] = true;
			Assert.That(map.GetLowestBit(), Is.EqualTo(42));
			Assert.That(map.GetHighestBit(), Is.EqualTo(63));

			map[BITS - 1] = true;
			Assert.That(map.GetLowestBit(), Is.EqualTo(42));
			Assert.That(map.GetHighestBit(), Is.EqualTo(BITS - 1));

			map[0] = true;
			Assert.That(map.GetLowestBit(), Is.EqualTo(0));
			Assert.That(map.GetHighestBit(), Is.EqualTo(BITS - 1));

			map[BITS - 1] = false;
			Assert.That(map.GetLowestBit(), Is.EqualTo(0));
			Assert.That(map.GetHighestBit(), Is.EqualTo(63));

			map[0] = false;
			Assert.That(map.GetLowestBit(), Is.EqualTo(42));
			Assert.That(map.GetHighestBit(), Is.EqualTo(63));
		}

		[Test]
		public void Test_BitMap64_FindNext()
		{
			const int SIZE = 256;

			// bit map (empty)
			var map = new BitMap64(SIZE);

			Action<int, int, int> verify = (start, end, expect) =>
			{
#if DEBUG
				string s = map.ToString("B");
				if (start > 0) s = new string('_', start) + s.Substring(start);
				if (end <= SIZE) s = s.Substring(0, end) + new string('_', SIZE - end);
				Log("MAP: " + s);
#endif
				var actual = map.FindNext(start, end);
#if DEBUG
				if (expect >= 0)
				{
					Log($"     {new string(' ', expect)}{'^'} {expect} -> {actual}");
				}
				else
				{
					Log($"     <not found> -> {actual}");
				}
#endif
				if (actual != expect)
				{
					Assert.That(actual, Is.EqualTo(expect), "Invalid result in range {0} <= idx < {1}", start, end);
				}
			};

			verify(0, SIZE, -1);

			map.Set(42);
			verify(0, SIZE, 42);
			verify(42, SIZE, 42);
			verify(0, 43, 42);
			verify(43, SIZE, -1);
			verify(0, 42, -1);

			map.Set(40);
			verify(0, SIZE, 40);
			verify(40, SIZE, 40);
			verify(0, 41, 40);
			verify(0, 43, 40);
			verify(43, SIZE, -1);
			verify(0, 40, -1);

			verify(40, 43, 40);
			verify(41, 43, 42);
			verify(42, 43, 42);

			map.ClearAll();
			map.Set(234);
			verify(0, SIZE, 234);
			verify(234, SIZE, 234);
			verify(42, 237, 234);
			verify(231, 237, 234);
			verify(0, 234, -1);
			verify(0, 235, 234);
		}

		[Test]
		public void Test_BitMap64_TestAndClear()
		{
			var map = new BitMap64(128);

			Assert.That(map.TestAndClear(42), Is.False);
			Assert.That(map.ToString("B"), Is.EqualTo("00000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000"));

			map.Set(42);
			Assert.That(map.ToString("B"), Is.EqualTo("00000000000000000000000000000000000000000010000000000000000000000000000000000000000000000000000000000000000000000000000000000000"));
			Assert.That(map.TestAndClear(42), Is.True);
			Assert.That(map.Test(42), Is.False);
			Assert.That(map.ToString("B"), Is.EqualTo("00000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000"));

			Assert.That(map.TestAndClear(42), Is.False);
			Assert.That(map.ToString("B"), Is.EqualTo("00000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000"));

			map.Set(41);
			Assert.That(map.ToString("B"), Is.EqualTo("00000000000000000000000000000000000000000100000000000000000000000000000000000000000000000000000000000000000000000000000000000000"));
			Assert.That(map.TestAndClear(42), Is.False);
			Assert.That(map.Test(42), Is.False);
			Assert.That(map.ToString("B"), Is.EqualTo("00000000000000000000000000000000000000000100000000000000000000000000000000000000000000000000000000000000000000000000000000000000"));

			map.Set(43);
			Assert.That(map.ToString("B"), Is.EqualTo("00000000000000000000000000000000000000000101000000000000000000000000000000000000000000000000000000000000000000000000000000000000"));
			Assert.That(map.TestAndClear(42), Is.False);
			Assert.That(map.Test(42), Is.False);
			Assert.That(map.ToString("B"), Is.EqualTo("00000000000000000000000000000000000000000101000000000000000000000000000000000000000000000000000000000000000000000000000000000000"));

			map.Set(42 + 64);
			Assert.That(map.ToString("B"), Is.EqualTo("00000000000000000000000000000000000000000101000000000000000000000000000000000000000000000000000000000000001000000000000000000000"));
			Assert.That(map.TestAndClear(42), Is.False);
			Assert.That(map.Test(42), Is.False);
			Assert.That(map.ToString("B"), Is.EqualTo("00000000000000000000000000000000000000000101000000000000000000000000000000000000000000000000000000000000001000000000000000000000"));

			map.Set(42);
			Assert.That(map.Test(42), Is.True);
			Assert.That(map.ToString("B"), Is.EqualTo("00000000000000000000000000000000000000000111000000000000000000000000000000000000000000000000000000000000001000000000000000000000"));
			Assert.That(map.TestAndClear(42), Is.True);
			Assert.That(map.Test(42), Is.False);
			Assert.That(map.ToString("B"), Is.EqualTo("00000000000000000000000000000000000000000101000000000000000000000000000000000000000000000000000000000000001000000000000000000000"));

			Assert.That(map.TestAndClear(42), Is.False);
			Assert.That(map.Test(42), Is.False);
			Assert.That(map.ToString("B"), Is.EqualTo("00000000000000000000000000000000000000000101000000000000000000000000000000000000000000000000000000000000001000000000000000000000"));

		}

		[Test]
		public void Test_BitMap64_Enumerable()
		{
			var map = new BitMap64(256);
			map[1] = true;
			map[2] = true;
			map[5] = true;
			map[7] = true;
			map[42] = true;
			map[63] = true;
			map[123] = true;
			map[128] = true;
			map[255] = true;

			using (var it = map.GetEnumerator())
			{
				for (int i = 0; i < 256; i++)
				{
					Assert.That(it.MoveNext(), Is.True);
					Assert.That(it.Current, Is.EqualTo(map[i]), "#{0}", i);
				}
				Assert.That(it.MoveNext(), Is.False, "Should be done after 256 bits");
			}

			// LINQ
			Assert.That(map.Select((b, i) => new KeyValuePair<int, bool>(i, b)).Where(kv => kv.Value).Select(kv => kv.Key).ToArray(), Is.EqualTo(new[] { 1, 2, 5, 7, 42, 63, 123, 128, 255 }));

			// ToArray
			var bits = map.ToArray();
			Assert.That(bits.Length, Is.EqualTo(256));
			for (int i = 0; i < bits.Length; i++)
			{
				Assert.That(bits[i], Is.EqualTo(map[i]), "#{0}", i);
			}

		}

	}
}
