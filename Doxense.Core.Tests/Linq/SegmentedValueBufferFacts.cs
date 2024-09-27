#region Copyright (c) 2023-2024 SnowBank SAS, (c) 2005-2023 Doxense SAS
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

#if NET8_0_OR_GREATER

namespace Doxense.Linq.Tests
{
	using System.Buffers;
	using System.Collections.Generic;
	using System.Linq;
	using System.Runtime.CompilerServices;

	[TestFixture]
	[Category("Core-SDK")]
	[Parallelizable(ParallelScope.All)]
	public class SegmentedValueBufferFacts : SimpleTest
	{

		[Test]
		public void Test_Buffer_Basics()
		{
			var scratch = new SegmentedValueBuffer<int>.Scratch();
			var buffer = new SegmentedValueBuffer<int>(scratch);

			buffer.Add(1);
			Assert.That(buffer.Count, Is.EqualTo(1));

			buffer.Add(2);
			Assert.That(buffer.Count, Is.EqualTo(2));

			buffer.Add(3);
			Assert.That(buffer.Count, Is.EqualTo(3));

			buffer.AddRange([ 4, 5, 6 ]);
			Assert.That(buffer.Count, Is.EqualTo(6));

			buffer.AddRange(Enumerable.Range(7, 100 - 6));
			Assert.That(buffer.Count, Is.EqualTo(100));

			Assert.That(buffer.ToArray(), Is.EqualTo(Enumerable.Range(1, 100).ToArray()));
			Assert.That(buffer.ToList(), Is.EqualTo(Enumerable.Range(1, 100).ToList()));

			Assert.That(buffer[0], Is.EqualTo(1)); // in the initial chunk
			Assert.That(buffer[7], Is.EqualTo(8)); // last of initial chunk
			Assert.That(buffer[9], Is.EqualTo(10)); // in the first rented chunk
			Assert.That(buffer[41], Is.EqualTo(42)); // in the second rented chunk
			Assert.That(buffer[99], Is.EqualTo(100)); // in the last rented chunk

			buffer.Dispose();
		}

		[Test]
		public void Test_Buffer_With_Initial_Scratch()
		{
			var scratch = new SegmentedValueBuffer<int>.Scratch();
			using var buffer = new SegmentedValueBuffer<int>(scratch);

			Assert.That(buffer.Count, Is.EqualTo(0));
			Assert.That(buffer.Capacity, Is.GreaterThanOrEqualTo(8));
			Assert.That(buffer.ToArray(), Is.Empty);

			buffer.Add(123);

			Assert.That(buffer.Count, Is.EqualTo(1));
			Assert.That(buffer.Capacity, Is.GreaterThanOrEqualTo(8));

			Assert.That(scratch[0], Is.EqualTo(123), "Should have used the scratch for the first item");
			Assert.That(Unsafe.AreSame(ref buffer[0], ref scratch[0]), "Should reference the scratch for the first item");

			Assert.That(buffer.ToArray(), Is.EqualTo((int[]) [ 123 ]));

		}

		[Test]
		public void Test_Buffer_With_Heap_Backed_Array()
		{
			// array on the heap
			var array = new int[7];

			using var buffer = new SegmentedValueBuffer<int>(array);
			Assert.That(buffer.Count, Is.EqualTo(0));
			Assert.That(buffer.Capacity, Is.GreaterThanOrEqualTo(7));
			Assert.That(buffer.ToArray(), Is.Empty);

			buffer.Add(123);

			Assert.That(buffer.Count, Is.EqualTo(1));
			Assert.That(buffer.Capacity, Is.EqualTo(array.Length));

			Assert.That(array[0], Is.EqualTo(123), "Should have used the array for the first item");
			Assert.That(Unsafe.AreSame(ref buffer[0], ref array[0]), "Should reference the array for the first item");

			Assert.That(buffer.ToArray(), Is.EqualTo((int[]) [ 123 ]));
		}

		[Test]
		public void Test_Buffer_With_Stack_Backed_Array()
		{
			// use the stack
			Span<int> span = stackalloc int[7];

			using var buffer = new SegmentedValueBuffer<int>(span);
			Assert.That(buffer.Count, Is.EqualTo(0));
			Assert.That(buffer.Capacity, Is.GreaterThanOrEqualTo(7));
			Assert.That(buffer.ToArray(), Is.Empty);

			buffer.Add(123);

			Assert.That(buffer.Count, Is.EqualTo(1));
			Assert.That(buffer.Capacity, Is.EqualTo(span.Length));

			Assert.That(span[0], Is.EqualTo(123), "Should have used the array for the first item");
			Assert.That(Unsafe.AreSame(ref buffer[0], ref span[0]), "Should reference the stack for the first item");

			Assert.That(buffer.ToArray(), Is.EqualTo((int[]) [ 123 ]));
		}

		[Test]
		public void Test_Buffer_Expand_Should_Use_The_Pool()
		{
			// use the stack for the initial array
			Span<int> span = stackalloc int[7];
			span.Fill(-1);

			using var buffer = new SegmentedValueBuffer<int>(span);
			buffer.Add(123);

			Assert.That(buffer.Count, Is.EqualTo(1));
			Assert.That(buffer.Capacity, Is.EqualTo(span.Length));
			Assert.That(buffer[0], Is.EqualTo(123));
			Assert.That(buffer.ToArray(), Is.EqualTo((int[]) [ 123 ]));
			Assert.That(span.ToArray(), Is.EqualTo((int[]) [ 123, -1, -1, -1, -1, -1, -1 ]), "Should have written to the stack-based buffer");

			Assert.That(Unsafe.AreSame(ref buffer[0], ref span[0]), Is.True, "Should use the stack as the buffer");
			
			// should fill the buffer
			buffer.AddRange([ 2, 3, 4, 5, 6, 7 ]);
			Assert.That(buffer.Count, Is.EqualTo(7));
			Assert.That(buffer.Capacity, Is.EqualTo(span.Length));
			Assert.That(buffer[0], Is.EqualTo(123));
			Assert.That(buffer[1], Is.EqualTo(2));
			Assert.That(buffer[2], Is.EqualTo(3));
			Assert.That(buffer[3], Is.EqualTo(4));
			Assert.That(buffer[4], Is.EqualTo(5));
			Assert.That(buffer[5], Is.EqualTo(6));
			Assert.That(buffer[6], Is.EqualTo(7));
			Assert.That(buffer.ToArray(), Is.EqualTo((int[]) [ 123, 2, 3, 4, 5, 6, 7 ]));
			Assert.That(span.ToArray(), Is.EqualTo((int[]) [ 123, 2, 3, 4, 5, 6, 7 ]), "Should have written to the stack-based buffer");

			Assert.That(Unsafe.AreSame(ref buffer[0], ref span[0]), Is.True, "Should use the stack as the buffer");

			// adding more should switch to a heap-backed pooled buffer
			buffer.Add(8);
			Assert.That(buffer.Count, Is.EqualTo(8));
			Assert.That(buffer.Capacity, Is.GreaterThan(span.Length));
			Assert.That(buffer[0], Is.EqualTo(123));
			Assert.That(buffer[1], Is.EqualTo(2));
			Assert.That(buffer[2], Is.EqualTo(3));
			Assert.That(buffer[3], Is.EqualTo(4));
			Assert.That(buffer[4], Is.EqualTo(5));
			Assert.That(buffer[5], Is.EqualTo(6));
			Assert.That(buffer[6], Is.EqualTo(7));
			Assert.That(buffer[7], Is.EqualTo(8));

			Assert.That(Unsafe.AreSame(ref buffer[0], ref span[0]), Is.True);
		}

		[Test]
		public void Test_Buffer_AddRange()
		{
			var pool = HistoryRecordingArrayPool.Create<int>();

			var initial = new int[6];
			using var buffer = new SegmentedValueBuffer<int>(initial, pool);
			Assert.That(buffer.Count, Is.EqualTo(0));
			Assert.That(buffer.Capacity, Is.EqualTo(initial.Length));

			Log("Add(42)");
			buffer.Add(42);
			Assert.That(buffer.Count, Is.EqualTo(1));
			Assert.That(buffer.ToArray(), Is.EqualTo(((int[]) [ 42 ])));
			Assert.That(buffer.Capacity, Is.EqualTo(initial.Length));
			Assert.That(initial, Is.EqualTo((int[]) [ 42, 0, 0, 0, 0, 0 ]), "Should use the initial buffer");
			Assert.That(pool.Rented, Is.Empty, "Should not use the pool");

			// mutating the original buffer before the first resize should be observed
			initial[0] = 1;
			Assert.That(buffer.ToArray(), Is.EqualTo(((int[]) [ 1 ])));

			// add items that fit in the buffer
			Log("AddRange([ 2, 3, 4 ])");
			buffer.AddRange([ 2, 3, 4 ]);
			Assert.That(buffer.Count, Is.EqualTo(4));
			Assert.That(buffer.ToArray(), Is.EqualTo(((int[]) [ 1, 2, 3, 4 ])));
			Assert.That(buffer.Capacity, Is.EqualTo(initial.Length));
			Assert.That(initial, Is.EqualTo((int[]) [ 1, 2, 3, 4, 0, 0 ]), "Should use the initial buffer");
			pool.Dump();
			Assert.That(pool.Rented, Is.Empty, "Should not use the pool");

			// too many items, should trigger a resize!
			Log("AddRange([ 5, 6, 7, 8 ])");
			buffer.AddRange([ 5, 6, 7, 8 ]);
			Assert.That(buffer.Count, Is.EqualTo(8));
			Assert.That(buffer.ToArray(), Is.EqualTo(((int[]) [ 1, 2, 3, 4, 5, 6, 7, 8 ])));
			Assert.That(buffer.Capacity, Is.GreaterThanOrEqualTo(8));
			Assert.That(initial, Is.EqualTo((int[]) [ 1, 2, 3, 4, 5, 6 ]), "Should have filled the initial buffer");
			pool.Dump();
			Assert.That(pool.Rented, Has.Count.EqualTo(1), "Should have rented a buffer from the pool");
			Assert.That(pool.Rented[0].Size, Is.EqualTo(16));

			// mutating the original buffer _should_ change the buffer content
			initial[0] = 42;
			Assert.That(buffer[0], Is.EqualTo(42));
			Assert.That(buffer.ToArray(), Is.EqualTo(((int[]) [ 42, 2, 3, 4, 5, 6, 7, 8 ])));

			// should rent more pages
			Log("AddRange([ 9 .. 100 ])");
			buffer.AddRange(Enumerable.Range(7, 100 - 8));
			pool.Dump();
			Assert.That(pool.Rented, Has.Count.EqualTo(3), "Should have rented a 3 buffers from the pool");
			Assert.That(pool.Rented[0].Size, Is.EqualTo(16));
			Assert.That(pool.Rented[1].Size, Is.EqualTo(32));
			Assert.That(pool.Rented[2].Size, Is.EqualTo(64));
		}

		[Test]
		public void Test_Buffer_AddRange_RandomWalk()
		{
			// process a list of N random items by adding them to the buffer via random small chunks
			
			var randomItems= GetRandomNumbers(1000);
			Dump(randomItems);

			var scratch = new SegmentedValueBuffer<int>.Scratch();
			using var buffer = new SegmentedValueBuffer<int>(scratch);

			ReadOnlySpan<int> remaining = randomItems;
			while (remaining.Length > 0)
			{
				var chunk = NextRandomChunk(remaining, 17);
				Assert.That(chunk.Length, Is.GreaterThan(0).And.LessThanOrEqualTo(remaining.Length));

				buffer.AddRange(chunk);

				remaining = remaining.Slice(chunk.Length);
			}

			Assert.That(buffer.Count, Is.EqualTo(randomItems.Length));
			Assert.That(buffer.Capacity, Is.GreaterThanOrEqualTo(randomItems.Length));
			Assert.That(buffer.ToArray(), Is.EqualTo(randomItems));
		}

		[Test]
		public void Test_Buffer_Dispose()
		{
			var pool = HistoryRecordingArrayPool.Create<int>();

			var scratch = new SegmentedValueBuffer<int>.Scratch();
			var buffer = new SegmentedValueBuffer<int>(scratch, pool);

			Log("Add 100 items...");
			buffer.AddRange(Enumerable.Range(1, 100));
			Assert.That(buffer.Count, Is.EqualTo(100));
			Assert.That(buffer.Capacity, Is.EqualTo(120));
			pool.Dump();
			Assert.That(pool.NotReturned, Is.Not.Empty);

			Log("Dispose buffer...");
			buffer.Dispose();

			Assert.That(buffer.Count, Is.EqualTo(0));
			Assert.That(buffer.Capacity, Is.EqualTo(8));

			pool.Dump();
			Assert.That(pool.NotReturned, Is.Empty);

			Log("Reuse after dispose...");

			buffer.AddRange([ 123, 456, 789 ]);
			Assert.That(buffer.Count, Is.EqualTo(3));
			Assert.That(buffer.ToArray(), Is.EqualTo((int[]) [ 123, 456, 789 ]));
		}

		[Test]
		public void Test_Buffer_Clear()
		{
			var pool = HistoryRecordingArrayPool.Create<int>();

			var scratch = new SegmentedValueBuffer<int>.Scratch();
			using var buffer = new SegmentedValueBuffer<int>(scratch, pool);

			// single segment

			buffer.AddRange([ 1, 2, 3, 4 ]);
			Assert.That(buffer.ToArray(), Is.EqualTo((int[]) [ 1, 2, 3, 4 ]));
			Assert.That(buffer.Count, Is.EqualTo(4));
			Assert.That(buffer.Capacity, Is.EqualTo(8));
			Assert.That(pool.Rented, Is.Empty, "Should not use the pool for small buffer");
			Assert.That(pool.NotReturned, Is.Empty, "Should not use the pool for small buffer");

			buffer.Clear();
			Assert.That(buffer.ToArray(), Is.Empty);
			Assert.That(buffer.Count, Is.EqualTo(0));
			Assert.That(buffer.Capacity, Is.EqualTo(8));
			Assert.That(pool.Returned, Is.Empty, "Should not return anything");

			// multiple segments
			buffer.AddRange(Enumerable.Range(1, 100).ToArray());
			Assert.That(buffer.ToArray(), Is.EqualTo(Enumerable.Range(1, 100).ToArray()));
			Log("Rented:");
			foreach (var rented in pool.Rented)
			{
				Log($"- {rented.Size}");
			}
			Assert.That(pool.Rented, Has.Count.EqualTo(3), "Should have rented 3 buffers");
			Assert.That(buffer.Count, Is.EqualTo(100));
			Assert.That(buffer.Capacity, Is.EqualTo(120));

			buffer.Clear();
			Log("Returned:");
			foreach (var rented in pool.Returned)
			{
				Log($"- {rented.Array.Length}: {rented.Snapshot}");
			}
			Assert.That(pool.Returned, Has.Count.EqualTo(3), "Should have returned all 3 buffers");
			Assert.That(buffer.Count, Is.EqualTo(0));
			Assert.That(buffer.Capacity, Is.EqualTo(8));
		}

		[Test]
		public void Test_Buffer_Enumerator()
		{
			var scratch = new SegmentedValueBuffer<int>.Scratch();
			using var buffer = new SegmentedValueBuffer<int>(scratch);

			// empty
			using (var it = buffer.GetEnumerator())
			{
				Assert.That(it.MoveNext(), Is.False);
			}

			// one
			buffer.Add(1);
			Assert.That(buffer.Count, Is.EqualTo(1));
			using (var it = buffer.GetEnumerator())
			{
				Assert.That(it.MoveNext(), Is.True);
				Assert.That(it.Current, Is.EqualTo(1));
				Assert.That(it.MoveNext(), Is.False);
			}

			// initial full
			buffer.AddRange([ 2, 3, 4, 5, 6, 7, 8 ]);
			Assert.That(buffer.Count, Is.EqualTo(8));
			using (var it = buffer.GetEnumerator())
			{
				for (int i = 0; i < 8; i++)
				{
					Assert.That(it.MoveNext(), Is.True);
					Assert.That(it.Current, Is.EqualTo(i + 1));
				}
				Assert.That(it.MoveNext(), Is.False);
			}

			// multiple segments
			buffer.AddRange(Enumerable.Range(9, 100 - 8));
			Assert.That(buffer.Count, Is.EqualTo(100));
			using (var it = buffer.GetEnumerator())
			{
				for (int i = 0; i < 100; i++)
				{
					Assert.That(it.MoveNext(), Is.True, $"#{i}");
					Assert.That(it.Current, Is.EqualTo(i + 1), $"#{i}");
				}
				Assert.That(it.MoveNext(), Is.False);
			}

			int count = 0;
			foreach (var x in buffer)
			{
				Assert.That(count, Is.LessThan(100));
				++count;
				Assert.That(x, Is.EqualTo(count));
			}

		}

		[Test]
		public void Test_Buffer_ToArray_Single_Segment()
		{
			// start with an empty buffer (no allocations yet)
			var scratch = new SegmentedValueBuffer<int>.Scratch();
			using var buffer = new SegmentedValueBuffer<int>(scratch);

			Assert.That(buffer.Count, Is.EqualTo(0));
			Assert.That(buffer.Capacity, Is.EqualTo(8));

			// adding first item should allocate a buffer from the pool
			buffer.AddRange([ 1, 2, 3 ]);
			Assert.That(buffer.Count, Is.EqualTo(3));

			var array1 = buffer.ToArray();
			Assert.That(array1, Is.EqualTo((int[]) [ 1,2 ,3 ]));

			// can call ToArray() multiple times without changing the buffer, but should return a new array each time
			var array2 = buffer.ToArray();
			Assert.That(array2, Is.EqualTo((int[]) [ 1,2 ,3 ]));
			Assert.That(array2, Is.Not.SameAs(array1));

			// if we reuse the buffer it should keep the same buffer
			buffer.Add(4);

			Assert.That(buffer.Count, Is.EqualTo(4));
			var array3 = buffer.ToArray();
			Assert.That(array3, Is.EqualTo((int[]) [ 1, 2, 3, 4 ]));

			// ToArrayAndClear() should clear the buffer
			var array4 = buffer.ToArrayAndClear();
			Assert.That(array4, Is.EqualTo((int[]) [ 1, 2, 3, 4 ]));
			Assert.That(buffer.Count, Is.EqualTo(0), "Buffer should be cleared");

			// further calls should return empty list
			Assert.That(buffer.ToArray(), Is.Empty);
		}

		[Test]
		public void Test_Buffer_ToArray_Multiple_Segments()
		{
			// start with an empty buffer (no allocations yet)
			var scratch = new SegmentedValueBuffer<int>.Scratch();
			using var buffer = new SegmentedValueBuffer<int>(scratch);

			Assert.That(buffer.Count, Is.EqualTo(0));
			Assert.That(buffer.Capacity, Is.EqualTo(8));

			// adding first item should allocate a buffer from the pool
			buffer.AddRange(Enumerable.Range(1, 100));
			Assert.That(buffer.Count, Is.EqualTo(100));

			var array1 = buffer.ToArray();
			Assert.That(array1, Has.Length.EqualTo(100).And.EqualTo(Enumerable.Range(1, 100)));

			// can call ToArray() multiple times without changing the buffer, but should return a new array each time
			var array2 = buffer.ToArray();
			Assert.That(array2, Has.Length.EqualTo(100).And.EqualTo(Enumerable.Range(1, 100)));
			Assert.That(array2, Is.Not.SameAs(array1));

			// if we reuse the buffer it should keep the same buffer
			buffer.Add(101);

			Assert.That(buffer.Count, Is.EqualTo(101));
			var array3 = buffer.ToArray();
			Assert.That(array3, Has.Length.EqualTo(101).And.EqualTo(Enumerable.Range(1, 101)));

			// ToArrayAndClear() should clear the buffer
			var array4 = buffer.ToArrayAndClear();
			Assert.That(array4, Has.Length.EqualTo(101).And.EqualTo(Enumerable.Range(1, 101)));
			Assert.That(buffer.Count, Is.EqualTo(0), "Buffer should be cleared");

			// further calls should return empty list
			Assert.That(buffer.ToArray(), Is.Empty);
		}

		[Test]
		public void Test_Buffer_ToList_Single_Segment()
		{
			// start with an empty buffer (no allocations yet)
			var scratch = new SegmentedValueBuffer<int>.Scratch();
			using var buffer = new SegmentedValueBuffer<int>(scratch);

			Assert.That(buffer.Count, Is.EqualTo(0));
			Assert.That(buffer.Capacity, Is.EqualTo(8));

			// adding first item should allocate a buffer from the pool
			buffer.AddRange([ 1, 2, 3 ]);
			Assert.That(buffer.Count, Is.EqualTo(3));

			var list1 = buffer.ToList();
			Assert.That(list1, Is.EqualTo((List<int>) [ 1,2 ,3 ]));

			// can call ToArray() multiple times without changing the buffer, but should return a new array each time
			var list2 = buffer.ToList();
			Assert.That(list2, Is.EqualTo((List<int>) [ 1,2 ,3 ]));
			Assert.That(list2, Is.Not.SameAs(list1));

			// if we reuse the buffer it should keep the same buffer
			buffer.Add(4);

			Assert.That(buffer.Count, Is.EqualTo(4));
			var list3 = buffer.ToArray();
			Assert.That(list3, Is.EqualTo((List<int>) [ 1, 2, 3, 4 ]));

			// ToListAndClear() should clear the buffer
			var list4 = buffer.ToListAndClear();
			Assert.That(list4, Is.EqualTo((List<int>) [ 1, 2, 3, 4 ]));
			Assert.That(buffer.Count, Is.EqualTo(0), "Buffer should be cleared");

			// further calls should return empty list
			Assert.That(buffer.ToList(), Is.Empty);
		}

		[Test]
		public void Test_Buffer_ToList_Multiple_Segments()
		{
			// start with an empty buffer (no allocations yet)
			var scratch = new SegmentedValueBuffer<int>.Scratch();
			using var buffer = new SegmentedValueBuffer<int>(scratch);

			Assert.That(buffer.Count, Is.EqualTo(0));
			Assert.That(buffer.Capacity, Is.EqualTo(8));

			// adding first item should allocate a buffer from the pool
			buffer.AddRange(Enumerable.Range(1, 100));
			Assert.That(buffer.Count, Is.EqualTo(100));

			var array1 = buffer.ToList();
			Assert.That(array1, Has.Count.EqualTo(100).And.EqualTo(Enumerable.Range(1, 100)));

			// can call ToArray() multiple times without changing the buffer, but should return a new array each time
			var array2 = buffer.ToList();
			Assert.That(array2, Has.Count.EqualTo(100).And.EqualTo(Enumerable.Range(1, 100)));
			Assert.That(array2, Is.Not.SameAs(array1));

			// if we reuse the buffer it should keep the same buffer
			buffer.Add(101);

			Assert.That(buffer.Count, Is.EqualTo(101));
			var array3 = buffer.ToList();
			Assert.That(array3, Has.Count.EqualTo(101).And.EqualTo(Enumerable.Range(1, 101)));

			// ToListAndClear() should clear the buffer
			var array4 = buffer.ToListAndClear();
			Assert.That(array4, Has.Count.EqualTo(101).And.EqualTo(Enumerable.Range(1, 101)));
			Assert.That(buffer.Count, Is.EqualTo(0), "Buffer should be cleared");

			// further calls should return empty list
			Assert.That(buffer.ToList(), Is.Empty);
		}

		[Test]
		public void Test_Buffer_TryCopyTo_Single_Segment()
		{
			// start with an empty buffer (no allocations yet)
			var scratch = new SegmentedValueBuffer<int>.Scratch();
			using var buffer = new SegmentedValueBuffer<int>(scratch);

			Assert.That(buffer.Count, Is.EqualTo(0));
			Assert.That(buffer.Capacity, Is.EqualTo(8));

			buffer.AddRange([ 1, 2, 3, 4 ]);

			// buffer with extra space
			var buf = new int[13];
			Assert.That(buffer.TryCopyTo(buf, out int written), Is.True.WithOutput(written).EqualTo(4));
			Assert.That(buf, Is.EqualTo((int[]) [ 1, 2, 3, 4, 0, 0, 0, 0, 0, 0, 0, 0, 0 ]));
			// buffer with exact size

			buf = new int[4];
			Assert.That(buffer.TryCopyTo(buf, out written), Is.True.WithOutput(written).EqualTo(4));
			Assert.That(buf, Is.EqualTo((int[]) [ 1, 2, 3, 4 ]));

			// buffer too small
			Assert.That(buffer.TryCopyTo(new int[3], out written), Is.False.WithOutput(written).EqualTo(0));
			Assert.That(buffer.TryCopyTo([ ], out written), Is.False.WithOutput(written).EqualTo(0));
		}

		[Test]
		public void Test_Buffer_TryCopyTo_Multiple_Segments()
		{
			// start with an empty buffer (no allocations yet)
			var scratch = new SegmentedValueBuffer<int>.Scratch();
			using var buffer = new SegmentedValueBuffer<int>(scratch);

			Assert.That(buffer.Count, Is.EqualTo(0));
			Assert.That(buffer.Capacity, Is.EqualTo(8));

			// empty should not copy anything
			Assert.That(buffer.TryCopyTo([ ], out int written), Is.True.WithOutput(written).EqualTo(0));

			int[] buf = [ -1, -1, -1, -1 ];
			Assert.That(buffer.TryCopyTo(buf, out written), Is.True.WithOutput(written).EqualTo(0));
			Assert.That(buf, Is.EqualTo((int[]) [ -1, -1, -1, -1 ]));

			buffer.AddRange(Enumerable.Range(1, 100));

			// buffer with extra space
			buf = new int[137];
			Assert.That(buffer.TryCopyTo(buf, out written), Is.True.WithOutput(written).EqualTo(100));
			Assert.That(buf, Is.EqualTo(Enumerable.Range(1, 100).Concat(Enumerable.Repeat(0, 37)).ToArray()));
			// buffer with exact size

			buf = new int[100];
			Assert.That(buffer.TryCopyTo(buf, out written), Is.True.WithOutput(written).EqualTo(100));
			Assert.That(buf, Is.EqualTo(Enumerable.Range(1, 100).ToArray()));

			// buffer too small
			Assert.That(buffer.TryCopyTo(new int[7], out written), Is.False.WithOutput(written).EqualTo(0));
			Assert.That(buffer.TryCopyTo(new int[8 + 15], out written), Is.False.WithOutput(written).EqualTo(0));
			Assert.That(buffer.TryCopyTo(new int[99], out written), Is.False.WithOutput(written).EqualTo(0));
			Assert.That(buffer.TryCopyTo([ ], out written), Is.False.WithOutput(written).EqualTo(0));

			//note: can't use Assert.That(() => buffer.CopyTo(....)) because it is a ref struct!
			try
			{
				buffer.CopyTo([ ]);
				buffer.CopyTo(new int[7]);
				buffer.CopyTo(new int[8 + 15]);
				buffer.CopyTo(new int[8 + 16 + 31]);
				buffer.CopyTo(new int[99]);
				Assert.Fail("Should have failed to copy!");
			}
			catch (AssertionException)
			{
				throw;
			}
			catch (Exception e)
			{
				Assert.That(e, Is.InstanceOf<ArgumentException>(), "Invalid exception type thrown");
			}
		}

		[Test]
		public void Test_Buffer_TryCopyTo_BufferWriter()
		{

			// start with an empty buffer (no allocations yet)
			var scratch = new SegmentedValueBuffer<int>.Scratch();
			using var buffer = new SegmentedValueBuffer<int>(scratch);

			buffer.AddRange([ 1, 2, 3, 4 ]);
			{
				var writer = new ArrayBufferWriter<int>();
				buffer.CopyTo(writer);

				Assert.That(writer.WrittenSpan.ToArray(), Is.EqualTo((int[]) [ 1, 2, 3, 4 ]));
			}

			buffer.AddRange(Enumerable.Range(5, 100 - 4));
			{
				var writer = new ArrayBufferWriter<int>();
				buffer.CopyTo(writer);

				Assert.That(writer.WrittenSpan.ToArray(), Is.EqualTo(Enumerable.Range(1, 100).ToArray()));
			}
		}

	}

}

#endif
