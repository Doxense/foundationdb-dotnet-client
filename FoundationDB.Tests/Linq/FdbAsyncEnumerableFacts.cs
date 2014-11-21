#region BSD Licence
/* Copyright (c) 2013, Doxense SARL
All rights reserved.

Redistribution and use in source and binary forms, with or without
modification, are permitted provided that the following conditions are met:
	* Redistributions of source code must retain the above copyright
	  notice, this list of conditions and the following disclaimer.
	* Redistributions in binary form must reproduce the above copyright
	  notice, this list of conditions and the following disclaimer in the
	  documentation and/or other materials provided with the distribution.
	* Neither the name of Doxense nor the
	  names of its contributors may be used to endorse or promote products
	  derived from this software without specific prior written permission.

THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS "AS IS" AND
ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED
WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE
DISCLAIMED. IN NO EVENT SHALL <COPYRIGHT HOLDER> BE LIABLE FOR ANY
DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES
(INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES;
LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND
ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT
(INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS
SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
 */
#endregion

namespace FoundationDB.Linq.Tests
{
	using FoundationDB.Async;
	using FoundationDB.Client.Tests;
	using NUnit.Framework;
	using System;
	using System.Collections.Generic;
	using System.Diagnostics;
	using System.Linq;
	using System.Runtime.ExceptionServices;
	using System.Threading;
	using System.Threading.Tasks;

	[TestFixture]
	public class FdbAsyncEnumerableFacts
	{

		[Test]
		public async Task Test_Can_Convert_Enumerable_To_AsyncEnumerable()
		{
			// we need to make sure this works, because we will use this a lot for other tests

			var source = Enumerable.Range(0, 10).ToAsyncEnumerable();
			Assert.That(source, Is.Not.Null);

			var results = new List<int>();
			using (var iterator = source.GetEnumerator())
			{
				while (await iterator.MoveNext(CancellationToken.None))
				{
					Assert.That(results.Count, Is.LessThan(10));
					results.Add(iterator.Current);
				}
			}
			Assert.That(results.Count, Is.EqualTo(10));
			Assert.That(results, Is.EqualTo(new int[] { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9 }));
		}

		[Test]
		public async Task Test_Can_Convert_Enumerable_To_AsyncEnumerable_With_Async_Transform()
		{
			// we need to make sure this works, because we will use this a lot for other tests

			var source = Enumerable.Range(0, 10).ToAsyncEnumerable(async (x) =>
			{
				await Task.Delay(10);
				return x + 1;
			});
			Assert.That(source, Is.Not.Null);

			var results = new List<int>();
			using (var iterator = source.GetEnumerator())
			{
				while (await iterator.MoveNext(CancellationToken.None))
				{
					Assert.That(results.Count, Is.LessThan(10));
					results.Add(iterator.Current);
				}
			}
			Assert.That(results.Count, Is.EqualTo(10));
			Assert.That(results, Is.EqualTo(new int[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 }));
		}

		[Test]
		public async Task Test_Can_ToListAsync()
		{
			var source = Enumerable.Range(0, 10).ToAsyncEnumerable();

			List<int> list = await source.ToListAsync();
			Assert.That(list, Is.EqualTo(new int[] { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9 }));
		}

		[Test]
		public async Task Test_Can_ToListAsync_Big()
		{
			var source = Enumerable.Range(0, 1000).ToAsyncEnumerable();

			List<int> list = await source.ToListAsync();
			Assert.That(list, Is.EqualTo(Enumerable.Range(0, 1000).ToList()));
		}

		[Test]
		public async Task Test_Can_ToArrayAsync()
		{
			var source = Enumerable.Range(0, 10).ToAsyncEnumerable();

			int[] array = await source.ToArrayAsync();
			Assert.That(array, Is.EqualTo(new int[] { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9 }));
		}

		[Test]
		public async Task Test_Can_ToArrayAsync_Big()
		{
			var source = Enumerable.Range(0, 1000).ToAsyncEnumerable();

			int[] array = await source.ToArrayAsync();

			Assert.That(array, Is.EqualTo(Enumerable.Range(0, 1000).ToArray()));
		}

		[Test]
		public async Task Test_Empty()
		{
			var empty = FdbAsyncEnumerable.Empty<int>();
			Assert.That(empty, Is.Not.Null);

			var results = await empty.ToListAsync();
			Assert.That(results, Is.Empty);

			bool any = await empty.AnyAsync();
			Assert.That(any, Is.False);

			bool none = await empty.NoneAsync();
			Assert.That(none, Is.True);

			int count = await empty.CountAsync();
			Assert.That(count, Is.EqualTo(0));
		}

		[Test]
		public async Task Test_Singleton()
		{
			var singleton = FdbAsyncEnumerable.Singleton(42);
			Assert.That(singleton, Is.Not.Null);

			var results = await singleton.ToListAsync();
			Assert.That(results, Is.EqualTo(new int[] { 42 }));

			bool any = await singleton.AnyAsync();
			Assert.That(any, Is.True);

			bool none = await singleton.NoneAsync();
			Assert.That(none, Is.False);

			int count = await singleton.CountAsync();
			Assert.That(count, Is.EqualTo(1));
		}

		[Test]
		public async Task Test_Can_Select_Sync()
		{
			var source = Enumerable.Range(0, 10).ToAsyncEnumerable();

			var selected = source.Select(x => x + 1);
			Assert.That(selected, Is.Not.Null);
			Assert.That(selected, Is.InstanceOf<FdbWhereSelectAsyncIterator<int, int>>());

			var results = await selected.ToListAsync();
			Assert.That(results, Is.EqualTo(new int[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 }));
		}

		[Test]
		public async Task Test_Can_Select_Async()
		{
			var source = Enumerable.Range(0, 10).ToAsyncEnumerable();

			var selected = source.Select(async (x) =>
			{
				await Task.Delay(10);
				return x + 1;
			});
			Assert.That(selected, Is.Not.Null);
			Assert.That(selected, Is.InstanceOf<FdbWhereSelectAsyncIterator<int, int>>());

			var results = await selected.ToListAsync();
			Assert.That(results, Is.EqualTo(new int[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 }));
		}

		[Test]
		public async Task Test_Can_Select_Multiple_Times()
		{
			var source = Enumerable.Range(0, 10).ToAsyncEnumerable();

			var squares = source.Select(x => (long)x * x);
			Assert.That(squares, Is.Not.Null);
			Assert.That(squares, Is.InstanceOf<FdbWhereSelectAsyncIterator<int, long>>());

			var roots = squares.Select(x => Math.Sqrt(x));
			Assert.That(roots, Is.Not.Null);
			Assert.That(roots, Is.InstanceOf<FdbWhereSelectAsyncIterator<int, double>>());

			var results = await roots.ToListAsync();
			Assert.That(results, Is.EqualTo(new double[] { 0.0, 1.0, 2.0, 3.0, 4.0, 5.0, 6.0, 7.0, 8.0, 9.0 }));
		}

		[Test]
		public async Task Test_Can_Select_Async_Multiple_Times()
		{
			var source = Enumerable.Range(0, 10).ToAsyncEnumerable();

			var squares = source.Select(x => Task.FromResult((long)x * x));
			Assert.That(squares, Is.Not.Null);
			Assert.That(squares, Is.InstanceOf<FdbWhereSelectAsyncIterator<int, long>>());

			var roots = squares.Select(x => Task.FromResult(Math.Sqrt(x)));
			Assert.That(roots, Is.Not.Null);
			Assert.That(roots, Is.InstanceOf<FdbWhereSelectAsyncIterator<int, double>>());

			var results = await roots.ToListAsync();
			Assert.That(results, Is.EqualTo(new double[] { 0.0, 1.0, 2.0, 3.0, 4.0, 5.0, 6.0, 7.0, 8.0, 9.0 }));
		}

		[Test]
		public async Task Test_Can_Where()
		{
			var source = Enumerable.Range(0, 10).ToAsyncEnumerable();

			var query = source.Where(x => x % 2 == 1);
			Assert.That(query, Is.Not.Null);

			var results = await query.ToListAsync();
			Assert.That(results, Is.EqualTo(new int[] { 1, 3, 5, 7, 9 }));
		}

		[Test]
		public async Task Test_Can_Take()
		{
			var source = Enumerable.Range(0, 42).ToAsyncEnumerable();

			var query = source.Take(10);
			Assert.That(query, Is.Not.Null);
			Assert.That(query, Is.InstanceOf<FdbWhereSelectAsyncIterator<int, int>>());

			var results = await query.ToListAsync();
			Assert.That(results, Is.EqualTo(new int[] { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9 }));
		}


		[Test]
		public async Task Test_Can_Where_And_Take()
		{
			var source = Enumerable.Range(0, 42).ToAsyncEnumerable();

			var query = source
				.Where(x => x % 2 == 1)
				.Take(10);
			Assert.That(query, Is.Not.Null);
			Assert.That(query, Is.InstanceOf<FdbWhereSelectAsyncIterator<int, int>>());

			var results = await query.ToListAsync();
			Assert.That(results, Is.EqualTo(new int[] { 1, 3, 5, 7, 9, 11, 13, 15, 17, 19 }));
		}

		[Test]
		public async Task Test_Can_Take_And_Where()
		{
			var source = Enumerable.Range(0, 42).ToAsyncEnumerable();

			var query = source
				.Take(10)
				.Where(x => x % 2 == 1);
			Assert.That(query, Is.Not.Null);
			Assert.That(query, Is.InstanceOf<FdbWhereAsyncIterator<int>>());

			var results = await query.ToListAsync();
			Assert.That(results, Is.EqualTo(new int[] { 1, 3, 5, 7, 9 }));
		}

		[Test]
		public async Task Test_Can_Combine_Where_Clauses()
		{
			var source = Enumerable.Range(0, 42).ToAsyncEnumerable();

			var query = source
				.Where(x => x % 2 == 1)
				.Where(x => x % 3 == 0);
			Assert.That(query, Is.Not.Null);
			Assert.That(query, Is.InstanceOf<FdbWhereAsyncIterator<int>>()); // should have been optimized

			var results = await query.ToListAsync();
			Assert.That(results, Is.EqualTo(new int[] { 3, 9, 15, 21, 27, 33, 39 }));
		}

		[Test]
		public async Task Test_Can_Skip_And_Where()
		{
			var source = Enumerable.Range(0, 42).ToAsyncEnumerable();

			var query = source
				.Skip(21)
				.Where(x => x % 2 == 1);
			Assert.That(query, Is.Not.Null);
			Assert.That(query, Is.InstanceOf<FdbWhereAsyncIterator<int>>());

			var results = await query.ToListAsync();
			Assert.That(results, Is.EqualTo(new int[] { 21, 23, 25, 27, 29, 31, 33, 35, 37, 39, 41 }));
		}

		[Test]
		public async Task Test_Can_Where_And_Skip()
		{
			var source = Enumerable.Range(0, 42).ToAsyncEnumerable();

			var query = source
				.Where(x => x % 2 == 1)
				.Skip(15);
			Assert.That(query, Is.Not.Null);
			Assert.That(query, Is.InstanceOf<FdbWhereSelectAsyncIterator<int, int>>()); // should be optimized

			var results = await query.ToListAsync();
			Assert.That(results, Is.EqualTo(new int[] { 31, 33, 35, 37, 39, 41 }));
		}

		[Test]
		public async Task Test_Can_SelectMany()
		{
			var source = Enumerable.Range(0, 5).ToAsyncEnumerable();

			var query = source.SelectMany((x) => Enumerable.Repeat((char)(65 + x), x));
			Assert.That(query, Is.Not.Null);

			var results = await query.ToListAsync();
			Assert.That(results, Is.EqualTo(new [] { 'B', 'C', 'C', 'D', 'D', 'D', 'E', 'E', 'E', 'E' }));
		}

		[Test]
		public async Task Test_Can_Get_First()
		{
			var source = Enumerable.Range(42, 3).ToAsyncEnumerable();
			int first = await source.FirstAsync();
			Assert.That(first, Is.EqualTo(42));

			source = FdbAsyncEnumerable.Empty<int>();
			Assert.That(() => source.FirstAsync().GetAwaiter().GetResult(), Throws.InstanceOf<InvalidOperationException>());

		}

		[Test]
		public async Task Test_Can_Get_FirstOrDefault()
		{
			var source = Enumerable.Range(42, 3).ToAsyncEnumerable();
			int first = await source.FirstOrDefaultAsync();
			Assert.That(first, Is.EqualTo(42));

			source = FdbAsyncEnumerable.Empty<int>();
			first = await source.FirstOrDefaultAsync();
			Assert.That(first, Is.EqualTo(0));

		}

		[Test]
		public async Task Test_Can_Get_Single()
		{
			var source = Enumerable.Range(42, 1).ToAsyncEnumerable();
			int first = await source.SingleAsync();
			Assert.That(first, Is.EqualTo(42));

			source = FdbAsyncEnumerable.Empty<int>();
			Assert.That(() => source.SingleAsync().GetAwaiter().GetResult(), Throws.InstanceOf<InvalidOperationException>());

			source = Enumerable.Range(42, 3).ToAsyncEnumerable();
			Assert.That(() => source.SingleAsync().GetAwaiter().GetResult(), Throws.InstanceOf<InvalidOperationException>());

		}

		[Test]
		public async Task Test_Can_Get_SingleOrDefault()
		{
			var source = Enumerable.Range(42, 1).ToAsyncEnumerable();
			int first = await source.SingleOrDefaultAsync();
			Assert.That(first, Is.EqualTo(42));

			source = FdbAsyncEnumerable.Empty<int>();
			first = await source.SingleOrDefaultAsync();
			Assert.That(first, Is.EqualTo(0));

			source = Enumerable.Range(42, 3).ToAsyncEnumerable();
			Assert.That(() => source.SingleOrDefaultAsync().GetAwaiter().GetResult(), Throws.InstanceOf<InvalidOperationException>());
		}

		[Test]
		public async Task Test_Can_Get_Last()
		{
			var source = Enumerable.Range(42, 3).ToAsyncEnumerable();
			int first = await source.LastAsync();
			Assert.That(first, Is.EqualTo(44));

			source = FdbAsyncEnumerable.Empty<int>();
			Assert.That(() => source.LastAsync().GetAwaiter().GetResult(), Throws.InstanceOf<InvalidOperationException>());

		}

		[Test]
		public async Task Test_Can_Get_LastOrDefault()
		{
			var source = Enumerable.Range(42, 3).ToAsyncEnumerable();
			int first = await source.LastOrDefaultAsync();
			Assert.That(first, Is.EqualTo(44));

			source = FdbAsyncEnumerable.Empty<int>();
			first = await source.LastOrDefaultAsync();
			Assert.That(first, Is.EqualTo(0));

		}

		[Test]
		public async Task Test_Can_Get_ElementAt()
		{
			var source = Enumerable.Range(42, 10).ToAsyncEnumerable();

			Assert.That(() => source.ElementAtAsync(-1).GetAwaiter().GetResult(), Throws.InstanceOf<ArgumentOutOfRangeException>());

			int item = await source.ElementAtAsync(0);
			Assert.That(item, Is.EqualTo(42));

			item = await source.ElementAtAsync(5);
			Assert.That(item, Is.EqualTo(47));

			item = await source.ElementAtAsync(9);
			Assert.That(item, Is.EqualTo(51));

			Assert.That(() => source.ElementAtAsync(10).GetAwaiter().GetResult(), Throws.InstanceOf<InvalidOperationException>());

			source = FdbAsyncEnumerable.Empty<int>();
			Assert.That(() => source.ElementAtAsync(0).GetAwaiter().GetResult(), Throws.InstanceOf<InvalidOperationException>());
		}

		[Test]
		public async Task Test_Can_Get_ElementAtOrDefault()
		{
			var source = Enumerable.Range(42, 10).ToAsyncEnumerable();

			Assert.That(() => source.ElementAtOrDefaultAsync(-1).GetAwaiter().GetResult(), Throws.InstanceOf<ArgumentOutOfRangeException>());

			int item = await source.ElementAtOrDefaultAsync(0);
			Assert.That(item, Is.EqualTo(42));

			item = await source.ElementAtOrDefaultAsync(5);
			Assert.That(item, Is.EqualTo(47));

			item = await source.ElementAtOrDefaultAsync(9);
			Assert.That(item, Is.EqualTo(51));

			item = await source.ElementAtOrDefaultAsync(10);
			Assert.That(item, Is.EqualTo(0));

			source = FdbAsyncEnumerable.Empty<int>();
			item = await source.ElementAtOrDefaultAsync(0);
			Assert.That(item, Is.EqualTo(0));
			item = await source.ElementAtOrDefaultAsync(42);
			Assert.That(item, Is.EqualTo(0));
		}

		[Test]
		public async Task Test_Can_Distinct()
		{
			var items = new int[] { 1, 42, 7, 42, 9, 13, 7, 66 };
			var source = items.ToAsyncEnumerable();

			var distincts = await source.Distinct().ToListAsync();
			Assert.That(distincts, Is.Not.Null.And.EqualTo(items.Distinct().ToList()));

			var sequence = Enumerable.Range(0, 100).Select(x => (x * 1049) % 43);
			source = sequence.ToAsyncEnumerable();
			distincts = await source.Distinct().ToListAsync();
			Assert.That(distincts, Is.Not.Null.And.EqualTo(sequence.Distinct().ToList()));
		}

		[Test]
		public async Task Test_Can_Distinct_With_Comparer()
		{
			var items = new string[] { "World", "hello", "Hello", "world", "World!", "FileNotFound" };

			var source = items.ToAsyncEnumerable();

			var distincts = await source.Distinct(StringComparer.Ordinal).ToListAsync();
			Assert.That(distincts, Is.Not.Null.And.EqualTo(items.Distinct(StringComparer.Ordinal).ToList()));

			distincts = await source.Distinct(StringComparer.OrdinalIgnoreCase).ToListAsync();
			Assert.That(distincts, Is.Not.Null.And.EqualTo(items.Distinct(StringComparer.OrdinalIgnoreCase).ToList()));
		}

		[Test]
		public async Task Test_Can_ForEach()
		{
			var source = Enumerable.Range(0, 10).ToAsyncEnumerable();

			var items = new List<int>();

			await source.ForEachAsync((x) =>
			{
				Assert.That(items.Count, Is.LessThan(10));
				items.Add(x);
			});

			Assert.That(items.Count, Is.EqualTo(10));
			Assert.That(items, Is.EqualTo(new int[] { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9 }));
		}

		[Test]
		public async Task Test_Can_ForEach_Async()
		{
			var source = Enumerable.Range(0, 10).ToAsyncEnumerable();

			var items = new List<int>();

			await source.ForEachAsync(async (x) =>
			{
				Assert.That(items.Count, Is.LessThan(10));
				await Task.Delay(10);
				items.Add(x);
			});

			Assert.That(items.Count, Is.EqualTo(10));
			Assert.That(items, Is.EqualTo(new int[] { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9 }));
		}

		[Test]
		public async Task Test_Can_Any()
		{
			var source = Enumerable.Range(0, 10).ToAsyncEnumerable();
			bool any = await source.AnyAsync();
			Assert.That(any, Is.True);

			source = Enumerable.Range(0, 1).ToAsyncEnumerable();
			any = await source.AnyAsync();
			Assert.That(any, Is.True);

			any = await FdbAsyncEnumerable.Empty<int>().AnyAsync();
			Assert.That(any, Is.False);
		}

		[Test]
		public async Task Test_Can_Any_With_Predicate()
		{
			var source = Enumerable.Range(0, 10).ToAsyncEnumerable();

			bool any = await source.AnyAsync(x => x % 2 == 1);
			Assert.That(any, Is.True);

			any = await source.AnyAsync(x => x < 0);
			Assert.That(any, Is.False);

			any = await FdbAsyncEnumerable.Empty<int>().AnyAsync(x => x == 42);
			Assert.That(any, Is.False);
		}

		[Test]
		public async Task Test_Can_None()
		{
			var source = Enumerable.Range(0, 10).ToAsyncEnumerable();
			bool none = await source.NoneAsync();
			Assert.That(none, Is.False);

			source = Enumerable.Range(0, 1).ToAsyncEnumerable();
			none = await source.NoneAsync();
			Assert.That(none, Is.False);

			none = await FdbAsyncEnumerable.Empty<int>().NoneAsync();
			Assert.That(none, Is.True);
		}

		[Test]
		public async Task Test_Can_None_With_Predicate()
		{
			var source = Enumerable.Range(0, 10).ToAsyncEnumerable();

			bool any = await source.NoneAsync(x => x % 2 == 1);
			Assert.That(any, Is.False);

			any = await source.NoneAsync(x => x < 0);
			Assert.That(any, Is.True);

			any = await FdbAsyncEnumerable.Empty<int>().NoneAsync(x => x == 42);
			Assert.That(any, Is.True);
		}

		[Test]
		public async Task Test_Can_Count()
		{
			var source = Enumerable.Range(0, 10).ToAsyncEnumerable();

			var count = await source.CountAsync();

			Assert.That(count, Is.EqualTo(10));
		}

		[Test]
		public async Task Test_Can_Count_With_Predicate()
		{
			var source = Enumerable.Range(0, 10).ToAsyncEnumerable();

			var count = await source.CountAsync(x => x % 2 == 1);

			Assert.That(count, Is.EqualTo(5));
		}

		[Test]
		public async Task Test_Can_Min()
		{
			var rnd = new Random(1234);
			var items = Enumerable.Range(0, 100).Select(_ => rnd.Next()).ToList();

			var source = items.ToAsyncEnumerable();
			int min = await source.MinAsync();
			Assert.That(min, Is.EqualTo(items.Min()));

			// if min is the first
			items[0] = min - 1;
			source = items.ToAsyncEnumerable();
			min = await source.MinAsync();
			Assert.That(min, Is.EqualTo(items.Min()));

			// if min is the last
			items[items.Count - 1] = min - 1;
			source = items.ToAsyncEnumerable();
			min = await source.MinAsync();
			Assert.That(min, Is.EqualTo(items.Min()));

			// empty should fail
			source = FdbAsyncEnumerable.Empty<int>();
			Assert.That(() => source.MinAsync().GetAwaiter().GetResult(), Throws.InstanceOf<InvalidOperationException>());
		}

		[Test]
		public async Task Test_Can_Max()
		{
			var rnd = new Random(1234);
			var items = Enumerable.Range(0, 100).Select(_ => rnd.Next()).ToList();

			var source = items.ToAsyncEnumerable();
			int max = await source.MaxAsync();
			Assert.That(max, Is.EqualTo(items.Max()));

			// if max is the first
			items[0] = max + 1;
			source = items.ToAsyncEnumerable();
			max = await source.MaxAsync();
			Assert.That(max, Is.EqualTo(items.Max()));

			// if max is the last
			items[items.Count - 1] = max + 1;
			source = items.ToAsyncEnumerable();
			max = await source.MaxAsync();
			Assert.That(max, Is.EqualTo(items.Max()));

			// empty should fail
			source = FdbAsyncEnumerable.Empty<int>();
			Assert.That(() => source.MaxAsync().GetAwaiter().GetResult(), Throws.InstanceOf<InvalidOperationException>());
		}

		[Test]
		public async Task Test_Can_Sum_Signed()
		{
			var rnd = new Random(1234);
			var items = Enumerable.Range(0, 100).Select(_ => (long)rnd.Next()).ToList();

			var source = items.ToAsyncEnumerable();
			long sum = await source.SumAsync();
			long expected = 0;
			foreach (var x in items) expected = checked(expected + x);
			Assert.That(sum, Is.EqualTo(expected));

			// empty should return 0
			source = FdbAsyncEnumerable.Empty<long>();
			sum = await source.SumAsync();
			Assert.That(sum, Is.EqualTo(0));
		}

		[Test]
		public async Task Test_Can_Sum_Unsigned()
		{
			var rnd = new Random(1234);
			var items = Enumerable.Range(0, 100).Select(_ => (ulong)rnd.Next()).ToList();

			var source = items.ToAsyncEnumerable();
			ulong sum = await source.SumAsync();
			ulong expected = 0;
			foreach (var x in items) expected = checked(expected + x);
			Assert.That(sum, Is.EqualTo(expected));

			// empty should return 0
			source = FdbAsyncEnumerable.Empty<ulong>();
			sum = await source.SumAsync();
			Assert.That(sum, Is.EqualTo(0));
		}

		[Test]
		public async Task Test_Can_Select_Anonymous_Types()
		{
			var source = Enumerable.Range(0, 10).ToAsyncEnumerable();

			var results = await source
				.Select((x) => new { Value = x, Square = x * x, Root = Math.Sqrt(x), Odd = x % 2 == 1 })
				.Where((t) => t.Odd)
				.ToListAsync();

			Assert.That(results, Is.Not.Null);
			Assert.That(results.Count, Is.EqualTo(5));

			Assert.That(results[0].Value, Is.EqualTo(1));
			Assert.That(results[0].Square, Is.EqualTo(1));
			Assert.That(results[0].Root, Is.EqualTo(1.0));
			Assert.That(results[0].Odd, Is.True);
		}

		[Test]
		public async Task Test_Can_Select_With_LINQ_Syntax()
		{
			// ensure that we can also use the "from ... select ... where" syntax

			var results = await 
				(from x in Enumerable.Range(0, 10).ToAsyncEnumerable()
				let t = new { Value = x, Square = x * x, Root = Math.Sqrt(x), Odd = x % 2 == 1 }
				where t.Odd
				select t)
				.ToListAsync();

			Assert.That(results.Count, Is.EqualTo(5));
			Assert.That(results.Select(t => t.Value).ToArray(), Is.EqualTo(new int[] { 1, 3, 5, 7, 9 }));
			Assert.That(results.Select(t => t.Square).ToArray(), Is.EqualTo(new int[] { 1, 9, 25, 49, 81 }));
			Assert.That(results.Select(t => t.Value).ToArray(), Is.EqualTo(new double[] { 1.0, 3.0, 5.0, 7.0, 9.0 }));
			Assert.That(results.All(t => t.Odd), Is.True);
		}

		[Test]
		public async Task Test_Can_SelectMany_With_LINQ_Syntax()
		{

			var results = await
			(from x in Enumerable.Range(0, 10).ToAsyncEnumerable()
			 from y in Enumerable.Repeat((char)(65 + x), x)
			 let t = new { X = x, Y = y, Odd = x % 2 == 1 }
			 where t.Odd
			 select t)
			 .ToListAsync();

			Assert.That(results.Count, Is.EqualTo(25));
			Assert.That(results.Select(t => t.X).ToArray(), Is.EqualTo(new int[] { 1, 3, 3, 3, 5, 5, 5, 5, 5, 7, 7, 7, 7, 7, 7, 7, 9, 9, 9, 9, 9, 9, 9, 9, 9 }));
			Assert.That(results.Select(t => t.Y).ToArray(), Is.EqualTo(new char[] { 'B', 'D', 'D', 'D', 'F', 'F', 'F', 'F', 'F', 'H', 'H', 'H', 'H', 'H', 'H', 'H', 'J', 'J', 'J', 'J', 'J', 'J', 'J', 'J', 'J' }));
			Assert.That(results.All(t => t.Odd), Is.True);
		}

		[Test]
		public async Task Test_Exceptions_Are_Propagated_To_Caller()
		{
			var query = Enumerable.Range(0, 10).ToAsyncEnumerable()
				.Select(x =>
				{
					if (x == 1) throw new FormatException("KABOOM");
					Assert.That(x, Is.LessThan(1));
					return x;
				});

			using (var iterator = query.GetEnumerator())
			{
				// first move next should succeed
				bool res = await iterator.MoveNext(CancellationToken.None);
				Assert.That(res, Is.True);

				// second move next should fail
				var x = Assert.Throws<FormatException>(async () => await iterator.MoveNext(CancellationToken.None), "Should have failed");
				Assert.That(x.Message, Is.EqualTo("KABOOM"));

				// accessing current should rethrow the exception
				Assert.That(() => iterator.Current, Throws.InstanceOf<InvalidOperationException>());

				// another attempt at MoveNext should fail immediately but with a different error
				Assert.Throws<ObjectDisposedException>(async () => await iterator.MoveNext(CancellationToken.None));
			}
		}

		[Test]
		public async Task Test_Parallel_Select_Async()
		{
			const int MAX_CONCURRENCY = 5;
			const int N = 20;

			// since this can lock up, we need a global timeout !
			using (var go = new CancellationTokenSource(TimeSpan.FromSeconds(10)))
			{
				var token = go.Token;

				int concurrent = 0;
				var rnd = new Random();

				var sw = Stopwatch.StartNew();

				var query = Enumerable.Range(1, N)
					.ToAsyncEnumerable()
					.Select(async x =>
					{
						int ms;
						lock (rnd) {  ms = 10 + rnd.Next(25); }
						await Task.Delay(ms);
						return x;
					})
					.SelectAsync(async (x, ct) =>
					{
						try
						{
							var n = Interlocked.Increment(ref concurrent);
							try
							{
								Assert.That(n, Is.LessThanOrEqualTo(MAX_CONCURRENCY));
								Console.WriteLine("** " + sw.Elapsed + " start " + x + " (" + n + ")");
#if DEBUG_STACK_TRACES
								Console.WriteLine("> " + new StackTrace().ToString().Replace("\r\n", "\r\n> "));
#endif
								int ms;
								lock (rnd) { ms = rnd.Next(25) + 50; }
								await Task.Delay(ms);
								Console.WriteLine("** " + sw.Elapsed + " stop " + x + " (" + Volatile.Read(ref concurrent) + ")");

								return x * x;
							}
							finally
							{
								n = Interlocked.Decrement(ref concurrent);
								Assert.That(n, Is.GreaterThanOrEqualTo(0));
							}
						}
						catch(Exception e)
						{
							Console.Error.WriteLine("Thread #" + x + " failed: " + e.ToString());
							throw;
						}
					},
					new FdbParallelQueryOptions { MaxConcurrency = MAX_CONCURRENCY }
				);

				var results = await query.ToListAsync(token);

				Assert.That(Volatile.Read(ref concurrent), Is.EqualTo(0));
				Console.WriteLine("Results: " + string.Join(", ", results));
				Assert.That(results, Is.EqualTo(Enumerable.Range(1, N).Select(x => x * x).ToArray()));
			}

		}

		[Test]
		public async Task Test_FdbAsyncBuffer()
		{
			const int MAX_CAPACITY = 5;

			var buffer = new AsyncTransformQueue<int, int>((x, _) => Task.FromResult(x * x), MAX_CAPACITY, TaskScheduler.Default);

			// since this can lock up, we need a global timeout !
			using (var go = new CancellationTokenSource(TimeSpan.FromSeconds(10)))
			{
				var token = go.Token;

				var rnd = new Random(1234);

				// setup a pump loop
				var pump = Task.Run(async () =>
				{
					while (!token.IsCancellationRequested)
					{
						Console.WriteLine("[consumer] start receiving next...");
						var msg = await buffer.ReceiveAsync(token);
#if DEBUG_STACK_TRACES
						Console.WriteLine("[consumer] > " + new StackTrace().ToString().Replace("\r\n", "\r\n[consumer] > "));
#endif
						if (msg.HasValue)
						{
							Console.WriteLine("[consumer] Got value " + msg.Value);
						}
						else if (msg.HasValue)
						{
							Console.WriteLine("[consumer] Got error: " + msg.Error);
							msg.ThrowIfFailed();
							break;
						}
						else
						{
							Console.WriteLine("[consumer] Done!");
							break;
						}

					}

				}, token);

				int i = 0;

				// first 5 calls to enqueue should already be completed
				while (!token.IsCancellationRequested && i < MAX_CAPACITY * 10)
				{
					Console.WriteLine("[PRODUCER] Publishing " + i);
#if DEBUG_STACK_TRACES
					Console.WriteLine("[PRODUCER] > " + new StackTrace().ToString().Replace("\r\n", "\r\n[PRODUCER] > "));
#endif
					await buffer.OnNextAsync(i, token);
					++i;
					Console.WriteLine("[PRODUCER] Published");
#if DEBUG_STACK_TRACES
					Console.WriteLine("[PRODUCER] > " + new StackTrace().ToString().Replace("\r\n", "\r\n[PRODUCER] > "));
#endif

					if (rnd.Next(10) < 2)
					{
						Console.WriteLine("[PRODUCER] Thinking " + i);
						await Task.Delay(10);
					}
				}

				Console.WriteLine("[PRODUCER] COMPLETED!");
				buffer.OnCompleted();

				var t = await Task.WhenAny(pump, Task.Delay(TimeSpan.FromSeconds(10), token));
				Assert.That(t, Is.SameAs(pump));

			}
		}

		[Test]
		public async Task Test_FdbASyncIteratorPump()
		{
			const int N = 20;

			var rnd = new Random(1234);
			var sw = new Stopwatch();

			// the source outputs items while randomly waiting
			var source = Enumerable.Range(0, N)
				.ToAsyncEnumerable()
				.Select(async x =>
				{
					if (rnd.Next(10) < 2)
					{
						await Task.Delay(15);
					}
					Console.WriteLine("[PRODUCER] publishing " + x + " at " + sw.Elapsed.TotalMilliseconds + " on #" + Thread.CurrentThread.ManagedThreadId);
					return x;
				});

			// since this can lock up, we need a global timeout !
			using (var go = new CancellationTokenSource(TimeSpan.FromSeconds(10)))
			{
				var token = go.Token;

				var items = new List<int>();
				bool done = false;
				ExceptionDispatchInfo error = null;

				var queue = AsyncHelpers.CreateTarget<int>(
					onNextAsync: (x, ct) =>
					{
						Console.WriteLine("[consumer] onNextAsync(" + x + ") at " + sw.Elapsed.TotalMilliseconds + " on #" + Thread.CurrentThread.ManagedThreadId);
#if DEBUG_STACK_TRACES
						Console.WriteLine("> " + new StackTrace().ToString().Replace("\r\n", "\r\n> "));
#endif
						ct.ThrowIfCancellationRequested();
						items.Add(x);
						return TaskHelpers.CompletedTask;
					},
					onCompleted: () =>
					{
						Console.WriteLine("[consumer] onCompleted() at " + sw.Elapsed.TotalMilliseconds + " on #" + Thread.CurrentThread.ManagedThreadId);
#if DEBUG_STACK_TRACES
						Console.WriteLine("> " + new StackTrace().ToString().Replace("\r\n", "\r\n> "));
#endif
						done = true;
					},
					onError: (x) =>
					{
						Console.WriteLine("[consumer] onError()  at " + sw.Elapsed.TotalMilliseconds + " on #" + Thread.CurrentThread.ManagedThreadId);
						Console.WriteLine("[consumer] > " + x);
						error = x;
						go.Cancel();
					}
				);

				using(var inner = source.GetEnumerator())
				{
					var pump = new FdbAsyncIteratorPump<int>(inner, queue);

					Console.WriteLine("[PUMP] Start pumping on #" + Thread.CurrentThread.ManagedThreadId);
					sw.Start();
					await pump.PumpAsync(token);
					sw.Stop();
					Console.WriteLine("[PUMP] Pumping completed! at " + sw.Elapsed.TotalMilliseconds + " on #" + Thread.CurrentThread.ManagedThreadId);

					// We should have N items, plus 1 message for the completion
					Assert.That(items.Count, Is.EqualTo(N));
					Assert.That(done, Is.True);
					if (error != null) error.Throw();

					for (int i = 0; i < N; i++)
					{
						Assert.That(items[i], Is.EqualTo(i));
					}
				}

			}

		}
	
		private static async Task VerifyResult<T>(Func<Task<T>> asyncQuery, Func<T> referenceQuery, IQueryable<T> witness, string label)
		{
			Exception asyncError = null;
			Exception referenceError = null;

			T asyncResult = default(T);
			T referenceResult = default(T);

			try { asyncResult = await asyncQuery().ConfigureAwait(false); }
			catch (Exception e) { asyncError = e; }

			try { referenceResult = referenceQuery(); }
			catch (Exception e) { referenceError = e; }

			if (asyncError != null)
			{
				if (referenceError == null) Assert.Fail("{0}(): The async query failed but not there reference query {1} : {2}", label, witness.Expression, asyncError.ToString());
				//TODO: compare exception types ?
			}
			else if (referenceError != null)
			{
				Assert.Fail("{0}(): The referency query {1} failed ({2}) but the async query returned: {3}", label, witness.Expression, referenceError.Message, asyncResult);
			}
			else
			{
				try
				{
					Assert.That(asyncResult, Is.EqualTo(referenceResult), "{0}(): {1}", label, witness.Expression);
				}
				catch(AssertionException x)
				{
					Console.WriteLine("FAIL: " + witness.Expression + "\r\n >  " + x.Message);
				}
			}

		}

		private static async Task VerifySequence<T, TSeq>(Func<Task<TSeq>> asyncQuery, Func<TSeq> referenceQuery, IQueryable<T> witness, string label)
			where TSeq : class, IEnumerable<T>
		{
			Exception asyncError = null;
			Exception referenceError = null;

			TSeq asyncResult = null;
			TSeq referenceResult = null;

			try { asyncResult = await asyncQuery().ConfigureAwait(false); }
			catch (Exception e) { asyncError = e; }

			try { referenceResult = referenceQuery(); }
			catch (Exception e) { referenceError = e; }

			if (asyncError != null)
			{
				if (referenceError == null) Assert.Fail("{0}(): The async query failed but not there reference query {1} : {2}", label, witness.Expression, asyncError.ToString());
				//TODO: compare exception types ?
			}
			else if (referenceError != null)
			{
				Assert.Fail("{0}(): The referency query {1} failed ({2}) but the async query returned: {3}", label, witness.Expression, referenceError.Message, asyncResult);
			}
			else
			{
				try
				{
					Assert.That(asyncResult, Is.EqualTo(referenceResult), "{0}(): {1}", label, witness.Expression);
				}
				catch (AssertionException x)
				{
					Console.WriteLine("FAIL: " + witness.Expression + "\r\n >  " + x.Message);
				}
			}

		}

		[Test]
		public async Task Test_AsyncLinq_vs_LinqToObject()
		{
			// Construct a random async query in parallel with the equivalent LINQ-to-Object and ensure that they produce the same result

			// Then we call each of these methods: Count(), ToList(), ToArray(), First(), FirstOrDefault(), Single(), SingleOrDefault(), All(), None(), Any()
			// * if they produce a result, it must match
			// * if one fail, the other must also fail with an equivalent error (ex: Single() if the collection as more than one).

			// note: we will also create a third LINQ query using lambda expressions, just to be able to have a nicer ToString() in case of errors

			int[] SourceOfInts = new int[] { 1, 7, 42, -456, 123, int.MaxValue, -1, 1023, 0, short.MinValue, 5, 13, -273, 2013, 4534, -999 };
			
			const int N = 1000;

			var rnd = new Random(); // new Random(1234)

			for(int i=0;i<N;i++)
			{

				IFdbAsyncEnumerable<int> query = SourceOfInts.ToAsyncEnumerable();
				IEnumerable<int> reference = SourceOfInts;
				IQueryable<int> witness = Queryable.AsQueryable(SourceOfInts);

				// optional where
				switch (rnd.Next(6))
				{
					case 0:
					{ // keep about 50% of the items
						query = query.Where(x => x > 0);
						reference = reference.Where(x => x > 0);
						witness = witness.Where(x => x > 0);
						break;
					}
					case 1:
					{ // keep no items at all
						query = query.Where(x => false);
						reference = reference.Where(x => false);
						witness = witness.Where(x => false);
						break;
					}
					case 2:
					{ // keep only one item
						query = query.Where(x => x == 42);
						reference = reference.Where(x => x == 42);
						witness = witness.Where(x => x == 42);
						break;
					}
				}

				// optional transform (keep the type)
				if (rnd.Next(5) == 0)
				{
					query = query.Select(x => x >> 1);
					reference = reference.Select(x => x >> 1);
					witness = witness.Select(x => x >> 1);
				}

				// optional transform that change the type (and back)
				if (rnd.Next(3) == 0)
				{
					var sq = query.Select(x => x.ToString());
					var sr = reference.Select(x => x.ToString());
					var sw = witness.Select(x => x.ToString());

					// optional where
					if (rnd.Next(2) == 0)
					{
						sq = sq.Where(s => s.Length <= 2);
						sr = sr.Where(s => s.Length <= 2);
						sw = sw.Where(s => s.Length <= 2);
					}

					// convert back
					query = sq.Select(x => Int32.Parse(x));
					reference = sr.Select(x => Int32.Parse(x));
					witness = sw.Select(x => Int32.Parse(x));
				}

				// optional Skip
#if false // TODO !
				switch (rnd.Next(10))
				{
					case 0:
					{ // skip a few
						query = query.Skip(3);
						reference = reference.Skip(3);
						witness = witness.Skip(3);
						break;
					}
					case 1:
					{ // only take 1 
						query = query.Skip(1);
						reference = reference.Skip(1);
						witness = witness.Skip(1);
						break;
					}
				}
#endif

				// optional Take
				switch(rnd.Next(10))
				{
					case 0:
					{ // only take a few
						query = query.Take(3);
						reference = reference.Take(3);
						witness = witness.Take(3);
						break;
					}
					case 1:
					{ // only take 1 
						query = query.Take(1);
						reference = reference.Take(1);
						witness = witness.Take(1);
						break;
					}
				}

				// => ensure that results are coherent

				await VerifyResult(
					() => query.CountAsync(),
					() => reference.Count(),
					witness,
					"Count"
				);

				await VerifySequence(
					() => query.ToListAsync(),
					() => reference.ToList(),
					witness,
					"ToList"
				);

				await VerifySequence(
					() => query.ToArrayAsync(),
					() => reference.ToArray(),
					witness,
					"ToArray"
				);

				await VerifyResult(
					() => query.FirstAsync(),
					() => reference.First(),
					witness,
					"First"
				);

				await VerifyResult(
					() => query.FirstOrDefaultAsync(),
					() => reference.FirstOrDefault(),
					witness,
					"FirstOrDefault"
				);

				await VerifyResult(
					() => query.SingleAsync(),
					() => reference.Single(),
					witness,
					"Single"
				);

				await VerifyResult(
					() => query.SingleOrDefaultAsync(),
					() => reference.SingleOrDefault(),
					witness,
					"SingleOrDefault"
				);

				await VerifyResult(
					() => query.LastAsync(),
					() => reference.Last(),
					witness,
					"Last"
				);

				await VerifyResult(
					() => query.LastOrDefaultAsync(),
					() => reference.LastOrDefault(),
					witness,
					"LastOrDefault"
				);

				await VerifyResult(
					() => query.NoneAsync(),
					() => !reference.Any(),
					witness.Select(x => false), // makes the compiler happy
					"None"
				);

			}
			

		}

	}

}
