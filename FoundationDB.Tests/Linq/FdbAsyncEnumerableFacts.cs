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
	using NUnit.Framework;
	using System;
	using System.Collections.Generic;
	using System.Linq;
	using System.Threading;
	using System.Threading.Tasks;

	[TestFixture]
	public class FdbAsyncEnumerableFacts
	{

		[Test]
		public async Task Test_Can_Convert_Enumerable_To_AsyncEnmuerable()
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

			var list = await source.ToListAsync();
			Assert.That(list, Is.EqualTo(new int[] { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9 }));
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

			int count = await singleton.CountAsync();
			Assert.That(count, Is.EqualTo(1));
		}

		[Test]
		public async Task Test_Can_Select_Sync()
		{
			var source = Enumerable.Range(0, 10).ToAsyncEnumerable();

			var selected = source.Select(x => x + 1);
			Assert.That(selected, Is.Not.Null);

			var results = await selected.ToListAsync();
			Assert.That(results, Is.EqualTo(new int[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 }));
		}

		[Test]
		public async Task Test_Can_Select_Sync_With_Index()
		{
			var source = Enumerable.Range(100, 10).ToAsyncEnumerable();

			var selected = source.Select((x, i) => x + i);
			Assert.That(selected, Is.Not.Null);

			var results = await selected.ToListAsync();
			Assert.That(results, Is.EqualTo(new int[] { 100, 102, 104, 106, 108, 110, 112, 114, 116, 118 }));
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

			var results = await selected.ToListAsync();
			Assert.That(results, Is.EqualTo(new int[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 }));
		}

		[Test]
		public async Task Test_Can_Select_Async_With_Index()
		{
			var source = Enumerable.Range(100, 10).ToAsyncEnumerable();

			var selected = source.Select(async (x, i) =>
			{
				await Task.Delay(10);
				return x + i;
			});
			Assert.That(selected, Is.Not.Null);

			var results = await selected.ToListAsync();
			Assert.That(results, Is.EqualTo(new int[] { 100, 102, 104, 106, 108, 110, 112, 114, 116, 118 }));
		}

		[Test]
		public async Task Test_Can_Select_Multiple_Times()
		{
			var source = Enumerable.Range(0, 10).ToAsyncEnumerable();

			var squares = source.Select(x => Task.FromResult(x * x));
			Assert.That(squares, Is.Not.Null);

			var roots = squares.Select(x => Math.Sqrt(x));
			Assert.That(roots, Is.Not.Null);

			var results = await roots.ToListAsync();
			Assert.That(results, Is.EqualTo(new double[] { 0.0, 1.0, 2.0, 3.0, 4.0, 5.0, 6.0, 7.0, 8.0, 9.0 }));
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
		public async Task Test_Can_Where()
		{
			var source = Enumerable.Range(0, 10).ToAsyncEnumerable();

			var query = source.Where(x => x % 2 == 1);
			Assert.That(query, Is.Not.Null);

			var results = await query.ToListAsync();
			Assert.That(results, Is.EqualTo(new int[] { 1, 3, 5, 7, 9 }));
		}

		[Test]
		public async Task Test_Can_Where_Indexed()
		{
			var source = Enumerable.Range(42, 10).ToAsyncEnumerable();

			var query = source.Where((x, i) => i == 0 || i == 3 || i == 6);
			Assert.That(query, Is.Not.Null);

			var results = await query.ToListAsync();
			Assert.That(results, Is.EqualTo(new int[] { 42, 45, 48 }));
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
			Assert.That(results[0].Value, Is.EqualTo(1));
			Assert.That(results[0].Square, Is.EqualTo(1));
			Assert.That(results[0].Root, Is.EqualTo(1.0));
			Assert.That(results[0].Odd, Is.True);
		}

	}
}
