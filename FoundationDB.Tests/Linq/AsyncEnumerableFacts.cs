#region BSD License
/* Copyright (c) 2013-2018, Doxense SAS
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

// ReSharper disable AccessToDisposedClosure
// ReSharper disable AccessToModifiedClosure
namespace Doxense.Linq.Tests
{
	using System;
	using System.Collections.Generic;
	using System.Diagnostics;
	using System.Linq;
	using System.Runtime.ExceptionServices;
	using System.Threading;
	using System.Threading.Tasks;
	using Doxense;
	using Doxense.Async;
	using Doxense.Linq;
	using Doxense.Linq.Async.Iterators;
	using FoundationDB.Client.Tests;
	using NUnit.Framework;

	[TestFixture]
	public class AsyncEnumerableFacts : FdbTest
	{

		[Test]
		public async Task Test_Can_Convert_Enumerable_To_AsyncEnumerable()
		{
			// we need to make sure this works, because we will use this a lot for other tests

			var source = Enumerable.Range(0, 10).ToAsyncEnumerable();
			Assert.That(source, Is.Not.Null);

			var results = new List<int>();
			await using (var iterator = source.GetAsyncEnumerator(this.Cancellation))
			{
				while (await iterator.MoveNextAsync())
				{
					Assert.That(results.Count, Is.LessThan(10));
					results.Add(iterator.Current);
				}
			}
			Assert.That(results.Count, Is.EqualTo(10));
			Assert.That(results, Is.EqualTo(new[] { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9 }));
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
			await using (var iterator = source.GetAsyncEnumerator(this.Cancellation))
			{
				while (await iterator.MoveNextAsync())
				{
					Assert.That(results.Count, Is.LessThan(10));
					results.Add(iterator.Current);
				}
			}
			Assert.That(results.Count, Is.EqualTo(10));
			Assert.That(results, Is.EqualTo(new[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 }));
		}

		[Test]
		public async Task Test_Can_ToListAsync()
		{
			var source = Enumerable.Range(0, 10).ToAsyncEnumerable();

			List<int> list = await source.ToListAsync();
			Assert.That(list, Is.EqualTo(new[] { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9 }));
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
			Assert.That(array, Is.EqualTo(new [] { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9 }));
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
			var empty = AsyncEnumerable.Empty<int>();
			Assert.That(empty, Is.Not.Null);

			await using (var it = empty.GetAsyncEnumerator(this.Cancellation))
			{
				// accessing "Current" should always fail
				Assert.That(() => it.Current, Throws.InvalidOperationException);

				// MoveNext should return an already completed 'false' result
				var next = it.MoveNextAsync();
				Assert.That(next.IsCompleted, Is.True);
				Assert.That(next.Result, Is.False);

				Assert.That(() => it.Current, Throws.InvalidOperationException);
			}

			var results = await empty.ToListAsync();
			Assert.That(results, Is.Empty);

			bool any = await empty.AnyAsync();
			Assert.That(any, Is.False);

			bool none = await empty.NoneAsync();
			Assert.That(none, Is.True);

			int count = await empty.CountAsync();
			Assert.That(count, Is.Zero);
		}

		[Test]
		public async Task Test_Singleton()
		{
			var singleton = AsyncEnumerable.Singleton(42);
			Assert.That(singleton, Is.Not.Null);

			await using (var iterator = singleton.GetAsyncEnumerator(this.Cancellation))
			{
				// initial value of Current should be default(int)
				Assert.That(iterator.Current, Is.Zero);
				
				// first call to MoveNext should return an already completed 'true' result
				var next = iterator.MoveNextAsync();
				Assert.That(next.IsCompleted, Is.True);
				Assert.That(next.Result, Is.True);
				Assert.That(iterator.Current, Is.EqualTo(42));
				// second call to MoveNext should return an already completed 'false' result
				next = iterator.MoveNextAsync();
				Assert.That(next.IsCompleted, Is.True);
				Assert.That(next.Result, Is.False);
			}

			var results = await singleton.ToListAsync();
			Assert.That(results, Is.EqualTo(new [] { 42 }));

			bool any = await singleton.AnyAsync();
			Assert.That(any, Is.True);

			bool none = await singleton.NoneAsync();
			Assert.That(none, Is.False);

			int count = await singleton.CountAsync();
			Assert.That(count, Is.EqualTo(1));
		}

		[Test]
		public async Task Test_Producer_Single()
		{
			// Func<T>

			var singleton = AsyncEnumerable.Single(() => 42);
			Assert.That(singleton, Is.Not.Null);

			await using(var iterator = singleton.GetAsyncEnumerator(this.Cancellation))
			{
				var next = await iterator.MoveNextAsync();
				Assert.That(next, Is.True);
				Assert.That(iterator.Current, Is.EqualTo(42));
				next = await iterator.MoveNextAsync();
				Assert.That(next, Is.False);
			}

			var list = await singleton.ToListAsync();
			Assert.That(list, Is.EqualTo(new[] { 42 }));

			var array = await singleton.ToArrayAsync();
			Assert.That(array, Is.EqualTo(new[] { 42 }));

			bool any = await singleton.AnyAsync();
			Assert.That(any, Is.True);

			bool none = await singleton.NoneAsync();
			Assert.That(none, Is.False);

			int count = await singleton.CountAsync();
			Assert.That(count, Is.EqualTo(1));

			// Func<Task<T>>

			singleton = AsyncEnumerable.Single(() => Task.Delay(50).ContinueWith(_ => 42));
			Assert.That(singleton, Is.Not.Null);

			await using (var iterator = singleton.GetAsyncEnumerator(this.Cancellation))
			{
				var next = await iterator.MoveNextAsync();
				Assert.That(next, Is.True);
				Assert.That(iterator.Current, Is.EqualTo(42));
				next = await iterator.MoveNextAsync();
				Assert.That(next, Is.False);
			}

			list = await singleton.ToListAsync();
			Assert.That(list, Is.EqualTo(new[] { 42 }));

			array = await singleton.ToArrayAsync();
			Assert.That(array, Is.EqualTo(new[] { 42 }));

			any = await singleton.AnyAsync();
			Assert.That(any, Is.True);

			none = await singleton.NoneAsync();
			Assert.That(none, Is.False);

			count = await singleton.CountAsync();
			Assert.That(count, Is.EqualTo(1));

			// Func<CancellationToken, Task<T>>

			singleton = AsyncEnumerable.Single((ct) => Task.Delay(50, ct).ContinueWith(_ => 42, ct));
			Assert.That(singleton, Is.Not.Null);

			await using (var iterator = singleton.GetAsyncEnumerator(this.Cancellation))
			{
				var next = await iterator.MoveNextAsync();
				Assert.That(next, Is.True);
				Assert.That(iterator.Current, Is.EqualTo(42));
				next = await iterator.MoveNextAsync();
				Assert.That(next, Is.False);
			}

			list = await singleton.ToListAsync();
			Assert.That(list, Is.EqualTo(new[] { 42 }));

			array = await singleton.ToArrayAsync();
			Assert.That(array, Is.EqualTo(new[] { 42 }));

			any = await singleton.AnyAsync();
			Assert.That(any, Is.True);

			none = await singleton.NoneAsync();
			Assert.That(none, Is.False);

			count = await singleton.CountAsync();
			Assert.That(count, Is.EqualTo(1));
		}

		[Test]
		public async Task Test_Can_Select_Sync()
		{
			var source = Enumerable.Range(0, 10).ToAsyncEnumerable();

			var selected = source.Select(x => x + 1);
			Assert.That(selected, Is.Not.Null);
			Assert.That(selected, Is.InstanceOf<WhereSelectAsyncIterator<int, int>>());

			await using (var iterator = selected.GetAsyncEnumerator(this.Cancellation))
			{
				ValueTask<bool> next;
				// first 10 calls should return an already completed 'true' task, and current value should match
				for (int i = 0; i < 10; i++)
				{
					next = iterator.MoveNextAsync();
					Assert.That(next.IsCompleted, Is.True);
					Assert.That(next.Result, Is.True);
					Assert.That(iterator.Current, Is.EqualTo(i + 1));
				}
				// last call should return an already completed 'false' task
				next = iterator.MoveNextAsync();
				Assert.That(next.IsCompleted, Is.True);
				Assert.That(next.Result, Is.False);
			}

			var list = await selected.ToListAsync();
			Assert.That(list, Is.EqualTo(new[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 }));

			var array = await selected.ToArrayAsync();
			Assert.That(array, Is.EqualTo(new[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 }));
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
			Assert.That(selected, Is.InstanceOf<WhereSelectAsyncIterator<int, int>>());

			var list = await selected.ToListAsync();
			Assert.That(list, Is.EqualTo(new[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 }));

			var array = await selected.ToArrayAsync();
			Assert.That(array, Is.EqualTo(new[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 }));
		}

		[Test]
		public async Task Test_Can_Select_Multiple_Times()
		{
			var source = Enumerable.Range(0, 10).ToAsyncEnumerable();

			var squares = source.Select(x => (long)x * x);
			Assert.That(squares, Is.Not.Null);
			Assert.That(squares, Is.InstanceOf<WhereSelectAsyncIterator<int, long>>());

			var roots = squares.Select(x => Math.Sqrt(x));
			Assert.That(roots, Is.Not.Null);
			Assert.That(roots, Is.InstanceOf<WhereSelectAsyncIterator<int, double>>());

			var list = await roots.ToListAsync();
			Assert.That(list, Is.EqualTo(new [] { 0.0, 1.0, 2.0, 3.0, 4.0, 5.0, 6.0, 7.0, 8.0, 9.0 }));

			var array = await roots.ToArrayAsync();
			Assert.That(array, Is.EqualTo(new[] { 0.0, 1.0, 2.0, 3.0, 4.0, 5.0, 6.0, 7.0, 8.0, 9.0 }));
		}

		[Test]
		public async Task Test_Can_Select_Async_Multiple_Times()
		{
			var source = Enumerable.Range(0, 10).ToAsyncEnumerable();

			var squares = source.Select(x => Task.FromResult((long)x * x));
			Assert.That(squares, Is.Not.Null);
			Assert.That(squares, Is.InstanceOf<WhereSelectAsyncIterator<int, long>>());

			var roots = squares.Select(x => Task.FromResult(Math.Sqrt(x)));
			Assert.That(roots, Is.Not.Null);
			Assert.That(roots, Is.InstanceOf<WhereSelectAsyncIterator<int, double>>());

			var list = await roots.ToListAsync();
			Assert.That(list, Is.EqualTo(new [] { 0.0, 1.0, 2.0, 3.0, 4.0, 5.0, 6.0, 7.0, 8.0, 9.0 }));

			var array = await roots.ToArrayAsync();
			Assert.That(array, Is.EqualTo(new[] { 0.0, 1.0, 2.0, 3.0, 4.0, 5.0, 6.0, 7.0, 8.0, 9.0 }));
		}

		[Test]
		public async Task Test_Can_Where()
		{
			var source = Enumerable.Range(0, 10).ToAsyncEnumerable();

			var query = source.Where(x => x % 2 == 1);
			Assert.That(query, Is.Not.Null);

			await using (var iterator = query.GetAsyncEnumerator(this.Cancellation))
			{
				ValueTask<bool> next;
				// only half the items match, so only 5 are expected to go out of the enumeration...
				for (int i = 0; i < 5; i++)
				{
					next = iterator.MoveNextAsync();
					Assert.That(next.IsCompleted, Is.True);
					Assert.That(next.Result, Is.True);
					Assert.That(iterator.Current, Is.EqualTo(i * 2 + 1));
				}
				// last call should return false
				next = iterator.MoveNextAsync();
				Assert.That(next.IsCompleted, Is.True);
				Assert.That(next.Result, Is.False);
			}

			var list = await query.ToListAsync();
			Assert.That(list, Is.EqualTo(new[] { 1, 3, 5, 7, 9 }));

			var array = await query.ToArrayAsync();
			Assert.That(array, Is.EqualTo(new[] { 1, 3, 5, 7, 9 }));
		}

		[Test]
		public async Task Test_Can_Take()
		{
			var source = Enumerable.Range(0, 42).ToAsyncEnumerable();

			var query = source.Take(10);
			Assert.That(query, Is.Not.Null);
			Assert.That(query, Is.InstanceOf<WhereSelectAsyncIterator<int, int>>());

			await using (var iterator = query.GetAsyncEnumerator(this.Cancellation))
			{
				ValueTask<bool> next;
				// first 10 calls should return an already completed 'true' task, and current value should match
				for (int i = 0; i < 10; i++)
				{
					next = iterator.MoveNextAsync();
					Assert.That(next.IsCompleted, Is.True);
					Assert.That(next.Result, Is.True);
					Assert.That(iterator.Current, Is.EqualTo(i));
				}
				// last call should return an already completed 'false' task
				next = iterator.MoveNextAsync();
				Assert.That(next.IsCompleted, Is.True);
				Assert.That(next.Result, Is.False);
			}

			var list = await query.ToListAsync();
			Assert.That(list, Is.EqualTo(new[] { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9 }));

			var array = await query.ToArrayAsync();
			Assert.That(array, Is.EqualTo(new[] { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9 }));
		}


		[Test]
		public async Task Test_Can_Where_And_Take()
		{
			var source = Enumerable.Range(0, 42).ToAsyncEnumerable();

			var query = source
				.Where(x => x % 2 == 1)
				.Take(10);
			Assert.That(query, Is.Not.Null);
			Assert.That(query, Is.InstanceOf<WhereSelectAsyncIterator<int, int>>());

			await using (var iterator = query.GetAsyncEnumerator(this.Cancellation))
			{
				ValueTask<bool> next;
				// first 10 calls should return an already completed 'true' task, and current value should match
				for (int i = 0; i < 10; i++)
				{
					next = iterator.MoveNextAsync();
					Assert.That(next.IsCompleted, Is.True);
					Assert.That(next.Result, Is.True);
					Assert.That(iterator.Current, Is.EqualTo(i * 2 + 1));
				}
				// last call should return an already completed 'false' task
				next = iterator.MoveNextAsync();
				Assert.That(next.IsCompleted, Is.True);
				Assert.That(next.Result, Is.False);
			}

			var list = await query.ToListAsync();
			Assert.That(list, Is.EqualTo(new[] { 1, 3, 5, 7, 9, 11, 13, 15, 17, 19 }));

			var array = await query.ToArrayAsync();
			Assert.That(array, Is.EqualTo(new[] { 1, 3, 5, 7, 9, 11, 13, 15, 17, 19 }));
		}

		[Test]
		public async Task Test_Can_Take_And_Where()
		{
			var source = Enumerable.Range(0, 42).ToAsyncEnumerable();

			var query = source
				.Take(10)
				.Where(x => x % 2 == 1);
			Assert.That(query, Is.Not.Null);
			Assert.That(query, Is.InstanceOf<WhereAsyncIterator<int>>());

			var list = await query.ToListAsync();
			Assert.That(list, Is.EqualTo(new[] { 1, 3, 5, 7, 9 }));

			var array = await query.ToArrayAsync();
			Assert.That(array, Is.EqualTo(new[] { 1, 3, 5, 7, 9 }));
		}

		[Test]
		public async Task Test_Can_Combine_Where_Clauses()
		{
			var source = Enumerable.Range(0, 42).ToAsyncEnumerable();

			var query = source
				.Where(x => x % 2 == 1)
				.Where(x => x % 3 == 0);
			Assert.That(query, Is.Not.Null);
			Assert.That(query, Is.InstanceOf<WhereAsyncIterator<int>>()); // should have been optimized

			var list = await query.ToListAsync();
			Assert.That(list, Is.EqualTo(new[] { 3, 9, 15, 21, 27, 33, 39 }));

			var array = await query.ToArrayAsync();
			Assert.That(array, Is.EqualTo(new[] { 3, 9, 15, 21, 27, 33, 39 }));
		}

		[Test]
		public async Task Test_Can_Skip_And_Where()
		{
			var source = Enumerable.Range(0, 42).ToAsyncEnumerable();

			var query = source
				.Skip(21)
				.Where(x => x % 2 == 1);
			Assert.That(query, Is.Not.Null);
			Assert.That(query, Is.InstanceOf<WhereAsyncIterator<int>>());

			var list = await query.ToListAsync();
			Assert.That(list, Is.EqualTo(new[] { 21, 23, 25, 27, 29, 31, 33, 35, 37, 39, 41 }));

			var array = await query.ToArrayAsync();
			Assert.That(array, Is.EqualTo(new[] { 21, 23, 25, 27, 29, 31, 33, 35, 37, 39, 41 }));
		}

		[Test]
		public async Task Test_Can_Where_And_Skip()
		{
			var source = Enumerable.Range(0, 42).ToAsyncEnumerable();

			var query = source
				.Where(x => x % 2 == 1)
				.Skip(15);
			Assert.That(query, Is.Not.Null);
			Assert.That(query, Is.InstanceOf<WhereSelectAsyncIterator<int, int>>()); // should be optimized

			var list = await query.ToListAsync();
			Assert.That(list, Is.EqualTo(new[] { 31, 33, 35, 37, 39, 41 }));

			var array = await query.ToArrayAsync();
			Assert.That(array, Is.EqualTo(new[] { 31, 33, 35, 37, 39, 41 }));
		}

		[Test]
		public async Task Test_Can_SelectMany()
		{
			var source = Enumerable.Range(0, 5).ToAsyncEnumerable();

			var query = source.SelectMany((x) => Enumerable.Repeat((char)(65 + x), x));
			Assert.That(query, Is.Not.Null);

			var list = await query.ToListAsync();
			Assert.That(list, Is.EqualTo(new [] { 'B', 'C', 'C', 'D', 'D', 'D', 'E', 'E', 'E', 'E' }));

			var array = await query.ToArrayAsync();
			Assert.That(array, Is.EqualTo(new[] { 'B', 'C', 'C', 'D', 'D', 'D', 'E', 'E', 'E', 'E' }));
		}

		[Test]
		public async Task Test_Can_Get_First()
		{
			var source = Enumerable.Range(42, 3).ToAsyncEnumerable();
			int first = await source.FirstAsync();
			Assert.That(first, Is.EqualTo(42));

			source = AsyncEnumerable.Empty<int>();
			Assert.That(() => source.FirstAsync().GetAwaiter().GetResult(), Throws.InstanceOf<InvalidOperationException>());

		}

		[Test]
		public async Task Test_Can_Get_FirstOrDefault()
		{
			var source = Enumerable.Range(42, 3).ToAsyncEnumerable();
			int first = await source.FirstOrDefaultAsync();
			Assert.That(first, Is.EqualTo(42));

			source = AsyncEnumerable.Empty<int>();
			first = await source.FirstOrDefaultAsync();
			Assert.That(first, Is.EqualTo(0));

		}

		[Test]
		public async Task Test_Can_Get_Single()
		{
			var source = Enumerable.Range(42, 1).ToAsyncEnumerable();
			int first = await source.SingleAsync();
			Assert.That(first, Is.EqualTo(42));

			source = AsyncEnumerable.Empty<int>();
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

			source = AsyncEnumerable.Empty<int>();
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

			source = AsyncEnumerable.Empty<int>();
			Assert.That(() => source.LastAsync().GetAwaiter().GetResult(), Throws.InstanceOf<InvalidOperationException>());

		}

		[Test]
		public async Task Test_Can_Get_LastOrDefault()
		{
			var source = Enumerable.Range(42, 3).ToAsyncEnumerable();
			int first = await source.LastOrDefaultAsync();
			Assert.That(first, Is.EqualTo(44));

			source = AsyncEnumerable.Empty<int>();
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

			source = AsyncEnumerable.Empty<int>();
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

			source = AsyncEnumerable.Empty<int>();
			item = await source.ElementAtOrDefaultAsync(0);
			Assert.That(item, Is.EqualTo(0));
			item = await source.ElementAtOrDefaultAsync(42);
			Assert.That(item, Is.EqualTo(0));
		}

		[Test]
		public async Task Test_Can_Distinct()
		{
			var items = new[] { 1, 42, 7, 42, 9, 13, 7, 66 };
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
			var items = new [] { "World", "hello", "Hello", "world", "World!", "FileNotFound" };

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
			Assert.That(items, Is.EqualTo(new[] { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9 }));
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
			Assert.That(items, Is.EqualTo(new[] { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9 }));
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

			any = await AsyncEnumerable.Empty<int>().AnyAsync();
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

			any = await AsyncEnumerable.Empty<int>().AnyAsync(x => x == 42);
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

			none = await AsyncEnumerable.Empty<int>().NoneAsync();
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

			any = await AsyncEnumerable.Empty<int>().NoneAsync(x => x == 42);
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
			source = AsyncEnumerable.Empty<int>();
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
			source = AsyncEnumerable.Empty<int>();
			Assert.That(() => source.MaxAsync().GetAwaiter().GetResult(), Throws.InstanceOf<InvalidOperationException>());
		}

		[Test]
		public async Task Test_Can_Sum()
		{
			var rnd = new Random(1234);

			{ // int
				var items = Enumerable.Range(0, 100).Select(_ => rnd.Next(0, 1000)).ToList();

				var source = items.ToAsyncEnumerable();
				long sum = await source.SumAsync();
				long expected = 0;
				foreach (var x in items) expected = checked(expected + x);
				Assert.That(sum, Is.EqualTo(expected));

				// empty should return 0
				source = AsyncEnumerable.Empty<int>();
				sum = await source.SumAsync();
				Assert.That(sum, Is.EqualTo(0));
			}

			{ // uint
				var items = Enumerable.Range(0, 100).Select(_ => (uint) rnd.Next(0, 1000)).ToList();

				var source = items.ToAsyncEnumerable();
				ulong sum = await source.SumAsync();
				ulong expected = 0;
				foreach (var x in items) expected = checked(expected + x);
				Assert.That(sum, Is.EqualTo(expected));

				// empty should return 0
				source = AsyncEnumerable.Empty<uint>();
				sum = await source.SumAsync();
				Assert.That(sum, Is.EqualTo(0));
			}

			{ // long
				var items = Enumerable.Range(0, 100).Select(_ => (long) rnd.Next(0, 1000)).ToList();

				var source = items.ToAsyncEnumerable();
				long sum = await source.SumAsync();
				long expected = 0;
				foreach (var x in items) expected = checked(expected + x);
				Assert.That(sum, Is.EqualTo(expected));

				// empty should return 0
				source = AsyncEnumerable.Empty<long>();
				sum = await source.SumAsync();
				Assert.That(sum, Is.EqualTo(0));
			}

			{ // ulong
				var items = Enumerable.Range(0, 100).Select(_ => (ulong) rnd.Next(0, 1000)).ToList();

				var source = items.ToAsyncEnumerable();
				ulong sum = await source.SumAsync();
				ulong expected = 0;
				foreach (var x in items) expected = checked(expected + x);
				Assert.That(sum, Is.EqualTo(expected));

				// empty should return 0
				source = AsyncEnumerable.Empty<ulong>();
				sum = await source.SumAsync();
				Assert.That(sum, Is.EqualTo(0));
			}

			{ // float
				var items = Enumerable.Range(0, 100).Select(_ => (float) rnd.NextDouble()).ToList();

				var source = items.ToAsyncEnumerable();
				float sum = await source.SumAsync();
				float expected = 0f;
				foreach (var x in items) expected = expected + x;
				Assert.That(sum, Is.EqualTo(expected));

				// empty should return 0
				source = AsyncEnumerable.Empty<float>();
				sum = await source.SumAsync();
				Assert.That(sum, Is.EqualTo(0.0f));
			}

			{ // double
				var items = Enumerable.Range(0, 100).Select(_ => rnd.NextDouble()).ToList();

				var source = items.ToAsyncEnumerable();
				double sum = await source.SumAsync();
				double expected = 0f;
				foreach (var x in items) expected = expected + x;
				Assert.That(sum, Is.EqualTo(expected));

				// empty should return 0
				source = AsyncEnumerable.Empty<double>();
				sum = await source.SumAsync();
				Assert.That(sum, Is.EqualTo(0.0f));
			}

			// overflow detection
			Assert.That(async () => await (new[] {int.MaxValue, int.MaxValue}.ToAsyncEnumerable()).SumAsync(), Throws.InstanceOf<OverflowException>());
			Assert.That(async () => await (new[] {uint.MaxValue, uint.MaxValue}.ToAsyncEnumerable()).SumAsync(), Throws.InstanceOf<OverflowException>());
			Assert.That(async () => await (new[] {long.MaxValue, long.MaxValue}.ToAsyncEnumerable()).SumAsync(), Throws.InstanceOf<OverflowException>());
			Assert.That(async () => await (new[] {ulong.MaxValue, ulong.MaxValue}.ToAsyncEnumerable()).SumAsync(), Throws.InstanceOf<OverflowException>());
		}

		[Test]
		public async Task Test_Can_Sum_Selector()
		{
			var rnd = new Random(1234);
			var items = Enumerable.Range(0, 100).Select(idx => new { Index = idx, Integer = rnd.Next(0, 1000), Decimal = rnd.NextDouble() }).ToList();

			{ // int
				var source = items.ToAsyncEnumerable();
				long sum = await source.SumAsync(x => x.Integer);
				long expected = 0;
				foreach (var x in items) expected = checked(expected + x.Integer);
				Assert.That(sum, Is.EqualTo(expected));
			}

			{ // uint
				var source = items.ToAsyncEnumerable();
				ulong sum = await source.SumAsync(x => (uint) x.Integer);
				ulong expected = 0;
				foreach (var x in items) expected = checked(expected + (uint) x.Integer);
				Assert.That(sum, Is.EqualTo(expected));
			}

			{ // long
				var source = items.ToAsyncEnumerable();
				long sum = await source.SumAsync(x => (long) x.Integer);
				long expected = 0;
				foreach (var x in items) expected = checked(expected + x.Integer);
				Assert.That(sum, Is.EqualTo(expected));
			}

			{ // ulong
				var source = items.ToAsyncEnumerable();
				ulong sum = await source.SumAsync(x => (ulong) x.Integer);
				ulong expected = 0;
				foreach (var x in items) expected = checked(expected + (ulong) x.Integer);
				Assert.That(sum, Is.EqualTo(expected));
			}

			{ // float
				var source = items.ToAsyncEnumerable();
				float sum = await source.SumAsync(x => (float) x.Decimal);
				float expected = 0f;
				foreach (var x in items) expected = expected + (float) x.Decimal;
				Assert.That(sum, Is.EqualTo(expected));
			}

			{ // double
				var source = items.ToAsyncEnumerable();
				double sum = await source.SumAsync(x => x.Decimal);
				double expected = 0f;
				foreach (var x in items) expected = expected + x.Decimal;
				Assert.That(sum, Is.EqualTo(expected));
			}

			// overflow detection
			Assert.That(async () => await (new[] { "FOO", "BAR" }.ToAsyncEnumerable()).SumAsync(x => int.MaxValue), Throws.InstanceOf<OverflowException>());
			Assert.That(async () => await (new[] { "FOO", "BAR" }.ToAsyncEnumerable()).SumAsync(x => uint.MaxValue), Throws.InstanceOf<OverflowException>());
			Assert.That(async () => await (new[] { "FOO", "BAR" }.ToAsyncEnumerable()).SumAsync(x => long.MaxValue), Throws.InstanceOf<OverflowException>());
			Assert.That(async () => await (new[] { "FOO", "BAR" }.ToAsyncEnumerable()).SumAsync(x => ulong.MaxValue), Throws.InstanceOf<OverflowException>());
		}

		[Test]
		public async Task Test_Can_OrderBy()
		{
			var rnd = new Random(1234);
			var items = Enumerable.Range(0, 100).Select(_ => rnd.Next()).ToList();

			var source = items.ToAsyncEnumerable();

			var query = source.OrderBy((x) => x);
			Assert.That(query, Is.Not.Null);
			var res = await query.ToListAsync();
			Assert.That(res, Is.Not.Null);
			Assert.That(res, Is.EqualTo(items.OrderBy((x) => x).ToList()));

			query = source.OrderByDescending((x) => x);
			Assert.That(query, Is.Not.Null);
			res = await query.ToListAsync();
			Assert.That(res, Is.Not.Null);
			Assert.That(res, Is.EqualTo(items.OrderByDescending((x) => x).ToList()));
		}

		[Test]
		public async Task Test_Can_OrderBy_With_Custom_Comparer()
		{
			var items = new[] { "c", "B", "a", "D" };

			var source = items.ToAsyncEnumerable();

			// ordinal should put upper before lower
			var query = source.OrderBy((x) => x, StringComparer.Ordinal);
			Assert.That(query, Is.Not.Null);
			var res = await query.ToListAsync();
			Assert.That(res, Is.Not.Null);
			Assert.That(res, Is.EqualTo(new [] { "B", "D", "a", "c" }));

			// ordinal ingore case should mixe upper and lower
			query = source.OrderBy((x) => x, StringComparer.OrdinalIgnoreCase);
			Assert.That(query, Is.Not.Null);
			res = await query.ToListAsync();
			Assert.That(res, Is.Not.Null);
			Assert.That(res, Is.EqualTo(new[] { "a", "B", "c", "D" }));
		}

		[Test]
		public async Task Test_Can_ThenBy()
		{
			var rnd = new Random(1234);
			var pairs = Enumerable.Range(0, 100).Select(_ => new KeyValuePair<int, int>(rnd.Next(10), rnd.Next())).ToList();
			var source = pairs.ToAsyncEnumerable();

			var query = source.OrderBy(kvp => kvp.Key).ThenBy(kvp => kvp.Value);
			Assert.That(query, Is.Not.Null);
			var res = await query.ToListAsync();
			Assert.That(res, Is.Not.Null);
			Assert.That(res, Is.EqualTo(pairs.OrderBy(kvp => kvp.Key).ThenBy(kvp => kvp.Value).ToList()));

			query = source.OrderBy(kvp => kvp.Key).ThenByDescending(kvp => kvp.Value);
			Assert.That(query, Is.Not.Null);
			res = await query.ToListAsync();
			Assert.That(res, Is.Not.Null);
			Assert.That(res, Is.EqualTo(pairs.OrderBy(kvp => kvp.Key).ThenByDescending(kvp => kvp.Value).ToList()));

			query = source.OrderByDescending(kvp => kvp.Key).ThenBy(kvp => kvp.Value);
			Assert.That(query, Is.Not.Null);
			res = await query.ToListAsync();
			Assert.That(res, Is.Not.Null);
			Assert.That(res, Is.EqualTo(pairs.OrderByDescending(kvp => kvp.Key).ThenBy(kvp => kvp.Value).ToList()));

			query = source.OrderByDescending(kvp => kvp.Key).ThenByDescending(kvp => kvp.Value);
			Assert.That(query, Is.Not.Null);
			res = await query.ToListAsync();
			Assert.That(res, Is.Not.Null);
			Assert.That(res, Is.EqualTo(pairs.OrderByDescending(kvp => kvp.Key).ThenByDescending(kvp => kvp.Value).ToList()));
		}

		[Test]
		public async Task Test_Can_Batch()
		{
			var items = Enumerable.Range(0, 100).ToList();

			var source = items.ToAsyncEnumerable();

			// evenly divided

			var query = source.Batch(20);
			Assert.That(query, Is.Not.Null);

			var results = await query.ToListAsync();
			Assert.That(results, Is.Not.Null.And.Count.EqualTo(5));
			Assert.That(results[0], Is.EqualTo(Enumerable.Range(0, 20).ToArray()));
			Assert.That(results[1], Is.EqualTo(Enumerable.Range(20, 20).ToArray()));
			Assert.That(results[2], Is.EqualTo(Enumerable.Range(40, 20).ToArray()));
			Assert.That(results[3], Is.EqualTo(Enumerable.Range(60, 20).ToArray()));
			Assert.That(results[4], Is.EqualTo(Enumerable.Range(80, 20).ToArray()));

			// unevenly divided

			query = source.Batch(32);
			Assert.That(query, Is.Not.Null);

			results = await query.ToListAsync();
			Assert.That(results, Is.Not.Null.And.Count.EqualTo(4));
			Assert.That(results[0], Is.EqualTo(Enumerable.Range(0, 32).ToArray()));
			Assert.That(results[1], Is.EqualTo(Enumerable.Range(32, 32).ToArray()));
			Assert.That(results[2], Is.EqualTo(Enumerable.Range(64, 32).ToArray()));
			Assert.That(results[3], Is.EqualTo(Enumerable.Range(96, 4).ToArray()));

			// empty

			query = AsyncEnumerable.Empty<int>().Batch(20);
			Assert.That(query, Is.Not.Null);

			results = await query.ToListAsync();
			Assert.That(results, Is.Not.Null.And.Empty);
		}

		[Test]
		public async Task Test_Can_Window()
		{

			// generate a source that stalls every 13 items, from 0 to 49

			var source = new AnonymousAsyncGenerator<int>((index, ct) =>
			{
				if (index >= 50) return Task.FromResult(Maybe.Nothing<int>());
				if (index % 13 == 0) return Task.Delay(100, this.Cancellation).ContinueWith((_) => Maybe.Return((int)index));
				return Task.FromResult(Maybe.Return((int)index));
			});

			// window size larger than sequence period

			var query = source.Window(20);
			Assert.That(query, Is.Not.Null);

			var results = await query.ToListAsync();
			Assert.That(results, Is.Not.Null.And.Count.EqualTo(4));
			Assert.That(results[0], Is.EqualTo(Enumerable.Range(0, 13).ToArray()));
			Assert.That(results[1], Is.EqualTo(Enumerable.Range(13, 13).ToArray()));
			Assert.That(results[2], Is.EqualTo(Enumerable.Range(26, 13).ToArray()));
			Assert.That(results[3], Is.EqualTo(Enumerable.Range(39, 11).ToArray()));

			// window size smaller than sequence period

			query = source.Window(10);
			Assert.That(query, Is.Not.Null);

			results = await query.ToListAsync();
			//REVIEW: right now the Window operator will produce small windows at the end of a period which may not be the most efficient...
			//TODO: optimize the implementation to try to squeeze out small buffers with only a couple items, and update this unit test!
			Assert.That(results, Is.Not.Null.And.Count.EqualTo(8));
			Assert.That(results[0], Is.EqualTo(Enumerable.Range(0, 10).ToArray()));
			Assert.That(results[1], Is.EqualTo(Enumerable.Range(10, 3).ToArray()));
			Assert.That(results[2], Is.EqualTo(Enumerable.Range(13, 10).ToArray()));
			Assert.That(results[3], Is.EqualTo(Enumerable.Range(23, 3).ToArray()));
			Assert.That(results[4], Is.EqualTo(Enumerable.Range(26, 10).ToArray()));
			Assert.That(results[5], Is.EqualTo(Enumerable.Range(36, 3).ToArray()));
			Assert.That(results[6], Is.EqualTo(Enumerable.Range(39, 10).ToArray()));
			Assert.That(results[7], Is.EqualTo(Enumerable.Range(49, 1).ToArray()));
		}

		[Test]
		public async Task Test_Can_Prefetch_On_Constant_Latency_Source()
		{
			int called = 0;
			var sw = new Stopwatch();

			Log("CONSTANT LATENCY GENERATOR:");

			// this iterator waits on each item produced
			var source = new AnonymousAsyncGenerator<int>((index, ct) =>
			{
				Interlocked.Increment(ref called);
				if (index >= 10) return Task.FromResult(Maybe.Nothing<int>());
				return Task.Delay(15, this.Cancellation).ContinueWith((_) => Maybe.Return((int)index));
			});

			var results = await source.ToListAsync();
			Assert.That(results, Is.Not.Null);
			Assert.That(results, Is.EqualTo(Enumerable.Range(0, 10).ToList()));

			// record the timing and call history to ensure that inner is called at least twice before the first item gets out

			Func<int, (int Value, int Called)> record = (x) => (x, Volatile.Read(ref called));

			// without pre-fetching, the number of calls should match for the producer and the consumer
			called = 0;
			sw.Restart();
			var withoutPrefetching = await source.Select(record).ToListAsync(this.Cancellation);
			Log($"P0: {string.Join(", ", withoutPrefetching)}");
			Assert.That(withoutPrefetching.Select(x => x.Value), Is.EqualTo(Enumerable.Range(0, 10)));
			Assert.That(withoutPrefetching.Select(x => x.Called), Is.EqualTo(Enumerable.Range(1, 10)));

			// with pre-fetching, the consumer should always have one item in advance
			called = 0;
			sw.Restart();
			var withPrefetching1 = await source.Prefetch().Select(record).ToListAsync(this.Cancellation);
			Log($"P1: {string.Join(", ", withPrefetching1)}");
			Assert.That(withPrefetching1.Select(x => x.Value), Is.EqualTo(Enumerable.Range(0, 10)));
			Assert.That(withPrefetching1.Select(x => x.Called), Is.EqualTo(Enumerable.Range(2, 10)));

			// pre-fetching more than 1 item on a consumer that is not buffered should not change the picture (since we can only read one ahead anyway)
			//REVIEW: maybe we should change the implementation of the operator so that it still prefetch items in the background if the rest of the query is lagging a bit?
			called = 0;
			sw.Restart();
			var withPrefetching2 = await source.Prefetch(2).Select(record).ToListAsync(this.Cancellation);
			Log($"P2: {string.Join(", ", withPrefetching2)}");
			Assert.That(withPrefetching2.Select(x => x.Value), Is.EqualTo(Enumerable.Range(0, 10)));
			Assert.That(withPrefetching2.Select(x => x.Called), Is.EqualTo(Enumerable.Range(2, 10)));
		}

		[Test]
		public async Task Test_Can_Prefetch_On_Bursty_Source()
		{
			int called = 0;
			var sw = new Stopwatch();

			Log("BURSTY GENERATOR:");

			// this iterator produce burst of items
			var source = new AnonymousAsyncGenerator<int>((index, ct) =>
			{
				Interlocked.Increment(ref called);
				if (index >= 10) return Task.FromResult(Maybe.Nothing<int>());
				if (index % 4 == 0) return Task.Delay(100, this.Cancellation).ContinueWith((_) => Maybe.Return((int)index));
				return Task.FromResult(Maybe.Return((int)index));
			});

			(int Value, int Called, TimeSpan Elapsed) Record(int x)
			{
				var res = (x, Volatile.Read(ref called), sw.Elapsed);
				sw.Restart();
				return res;
			}

			// without pre-fetching, the number of calls should match for the producer and the consumer
			called = 0;
			sw.Restart();
			var withoutPrefetching = await source.Select(Record).ToListAsync(this.Cancellation);
			Log($"P0: {string.Join(", ", withoutPrefetching)}");
			Assert.That(withoutPrefetching.Select(x => x.Value), Is.EqualTo(Enumerable.Range(0, 10)));

			// with pre-fetching K, the consumer should always have K items in advance
			//REVIEW: maybe we should change the implementation of the operator so that it still prefetch items in the background if the rest of the query is lagging a bit?
			for (int K = 1; K <= 4; K++)
			{
				called = 0;
				sw.Restart();
				var withPrefetchingK = await source.Prefetch(K).Select(Record).ToListAsync(this.Cancellation);
				Log($"P{K}: {string.Join(", ", withPrefetchingK)}");
				Assert.That(withPrefetchingK.Select(x => x.Value), Is.EqualTo(Enumerable.Range(0, 10)));
				Assert.That(withPrefetchingK[0].Called, Is.EqualTo(K + 1), "Generator must have {0} call(s) in advance!", K);
				Assert.That(withPrefetchingK.Select(x => x.Called), Is.All.LessThanOrEqualTo(11));
			}

			// if pre-fetching more than the period of the producer, we should not have any perf gain
			called = 0;
			sw.Restart();
			var withPrefetching5 = await source.Prefetch(5).Select(Record).ToListAsync(this.Cancellation);
			Log($"P5: {string.Join(", ", withPrefetching5)}");
			Assert.That(withPrefetching5.Select(x => x.Value), Is.EqualTo(Enumerable.Range(0, 10)));
			Assert.That(withPrefetching5[0].Called, Is.EqualTo(5), "Generator must have only 4 calls in advance because it only produces 4 items at a time!");
			Assert.That(withPrefetching5.Select(x => x.Called), Is.All.LessThanOrEqualTo(11));
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
			Assert.That(results.Select(t => t.Value).ToArray(), Is.EqualTo(new [] { 1, 3, 5, 7, 9 }));
			Assert.That(results.Select(t => t.Square).ToArray(), Is.EqualTo(new [] { 1, 9, 25, 49, 81 }));
			Assert.That(results.Select(t => t.Value).ToArray(), Is.EqualTo(new [] { 1.0, 3.0, 5.0, 7.0, 9.0 }));
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
			Assert.That(results.Select(t => t.X).ToArray(), Is.EqualTo(new [] { 1, 3, 3, 3, 5, 5, 5, 5, 5, 7, 7, 7, 7, 7, 7, 7, 9, 9, 9, 9, 9, 9, 9, 9, 9 }));
			Assert.That(results.Select(t => t.Y).ToArray(), Is.EqualTo(new [] { 'B', 'D', 'D', 'D', 'F', 'F', 'F', 'F', 'F', 'H', 'H', 'H', 'H', 'H', 'H', 'H', 'J', 'J', 'J', 'J', 'J', 'J', 'J', 'J', 'J' }));
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

			await using (var iterator = query.GetAsyncEnumerator(this.Cancellation))
			{
				// first move next should succeed
				bool res = await iterator.MoveNextAsync();
				Assert.That(res, Is.True);

				// second move next should fail
				Assert.That(async () => await iterator.MoveNextAsync(), Throws.InstanceOf<FormatException>().With.Message.EqualTo("KABOOM"), "Should have failed");

				// accessing current should rethrow the exception
				Assert.That(() => iterator.Current, Throws.InstanceOf<InvalidOperationException>());

				// another attempt at MoveNext should fail immediately but with a different error
				Assert.That(async () => await iterator.MoveNextAsync(), Throws.InstanceOf<ObjectDisposedException>());
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
						await Task.Delay(ms, this.Cancellation);
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
								Log("** " + sw.Elapsed + " start " + x + " (" + n + ")");
#if DEBUG_STACK_TRACES
								Log("> " + new StackTrace().ToString().Replace("\r\n", "\r\n> "));
#endif
								int ms;
								lock (rnd) { ms = rnd.Next(25) + 50; }
								await Task.Delay(ms, this.Cancellation);
								Log("** " + sw.Elapsed + " stop " + x + " (" + Volatile.Read(ref concurrent) + ")");

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
					new ParallelAsyncQueryOptions { MaxConcurrency = MAX_CONCURRENCY }
				);

				var results = await query.ToListAsync(token);

				Assert.That(Volatile.Read(ref concurrent), Is.EqualTo(0));
				Log("Results: " + string.Join(", ", results));
				Assert.That(results, Is.EqualTo(Enumerable.Range(1, N).Select(x => x * x).ToArray()));
			}

		}

		[Test]
		public async Task Test_AsyncBuffer()
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
						Log("[consumer] start receiving next...");
						var msg = await buffer.ReceiveAsync(token);
#if DEBUG_STACK_TRACES
						Log("[consumer] > " + new StackTrace().ToString().Replace("\r\n", "\r\n[consumer] > "));
#endif
						if (msg.HasValue)
						{
							Log("[consumer] Got value " + msg.Value);
						}
						else if (msg.HasValue)
						{
							Log("[consumer] Got error: " + msg.Error);
							msg.ThrowForNonSuccess();
							break;
						}
						else
						{
							Log("[consumer] Done!");
							break;
						}

					}

				}, token);

				int i = 0;

				// first 5 calls to enqueue should already be completed
				while (!token.IsCancellationRequested && i < MAX_CAPACITY * 10)
				{
					Log("[PRODUCER] Publishing " + i);
#if DEBUG_STACK_TRACES
					Log("[PRODUCER] > " + new StackTrace().ToString().Replace("\r\n", "\r\n[PRODUCER] > "));
#endif
					await buffer.OnNextAsync(i, token);
					++i;
					Log("[PRODUCER] Published");
#if DEBUG_STACK_TRACES
					Log("[PRODUCER] > " + new StackTrace().ToString().Replace("\r\n", "\r\n[PRODUCER] > "));
#endif

					if (rnd.Next(10) < 2)
					{
						Log("[PRODUCER] Thinking " + i);
						await Task.Delay(10, this.Cancellation);
					}
				}

				Log("[PRODUCER] COMPLETED!");
				buffer.OnCompleted();

				var t = await Task.WhenAny(pump, Task.Delay(TimeSpan.FromSeconds(10), token));
				Assert.That(t, Is.SameAs(pump));

			}
		}

		[Test]
		public async Task Test_AsyncIteratorPump()
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
					Log("[PRODUCER] publishing " + x + " at " + sw.Elapsed.TotalMilliseconds + " on #" + Thread.CurrentThread.ManagedThreadId);
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
						Log("[consumer] onNextAsync(" + x + ") at " + sw.Elapsed.TotalMilliseconds + " on #" + Thread.CurrentThread.ManagedThreadId);
#if DEBUG_STACK_TRACES
						Log("> " + new StackTrace().ToString().Replace("\r\n", "\r\n> "));
#endif
						ct.ThrowIfCancellationRequested();
						items.Add(x);
						return Task.CompletedTask;
					},
					onCompleted: () =>
					{
						Log("[consumer] onCompleted() at " + sw.Elapsed.TotalMilliseconds + " on #" + Thread.CurrentThread.ManagedThreadId);
#if DEBUG_STACK_TRACES
						Log("> " + new StackTrace().ToString().Replace("\r\n", "\r\n> "));
#endif
						done = true;
					},
					onError: (x) =>
					{
						Log("[consumer] onError()  at " + sw.Elapsed.TotalMilliseconds + " on #" + Thread.CurrentThread.ManagedThreadId);
						Log("[consumer] > " + x);
						error = x;
						go.Cancel();
					}
				);

				await using(var inner = source.GetAsyncEnumerator(this.Cancellation))
				{
					var pump = new AsyncIteratorPump<int>(inner, queue);

					Log("[PUMP] Start pumping on #" + Thread.CurrentThread.ManagedThreadId);
					sw.Start();
					await pump.PumpAsync(token);
					sw.Stop();
					Log("[PUMP] Pumping completed! at " + sw.Elapsed.TotalMilliseconds + " on #" + Thread.CurrentThread.ManagedThreadId);

					// We should have N items, plus 1 message for the completion
					Assert.That(items.Count, Is.EqualTo(N));
					Assert.That(done, Is.True);
					error?.Throw();

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
				if (referenceError == null) Assert.Fail("{0}(): The async query failed but not there reference query {1} : {2}", label, witness.Expression, asyncError);
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
					Log("FAIL: " + witness.Expression + "\r\n >  " + x.Message);
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
				if (referenceError == null) Assert.Fail("{0}(): The async query failed but not there reference query {1} : {2}", label, witness.Expression, asyncError);
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
					Log("FAIL: " + witness.Expression + "\r\n >  " + x.Message);
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

			int[] sourceOfInts = { 1, 7, 42, -456, 123, int.MaxValue, -1, 1023, 0, short.MinValue, 5, 13, -273, 2013, 4534, -999 };

			const int N = 1000;

			var rnd = new Random(); // new Random(1234)

			for(int i=0;i<N;i++)
			{

				IAsyncEnumerable<int> query = sourceOfInts.ToAsyncEnumerable();
				IEnumerable<int> reference = sourceOfInts;
				IQueryable<int> witness = sourceOfInts.AsQueryable();

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

		[Test]
		public async Task Test_Record_Items()
		{

			var items = Enumerable.Range(0, 10);
			var source = items.ToAsyncEnumerable();

			var before = new List<int>();
			var after = new List<int>();

			var a = source.Observe((x) => before.Add(x));
			var b = a.Where((x) => x % 2 == 1);
			var c = b.Observe((x) => after.Add(x));
			var d = c.Select((x) => x + 1);

			var query = source
				.Observe((x) => before.Add(x))
				.Where((x) => x % 2 == 1)
				.Observe((x) => after.Add(x))
				.Select((x) => x + 1);

			Log("query: " + query);

			var results = await query.ToListAsync();

			Log($"input : {string.Join(", ", items)}");
			Log($"before: {string.Join(", ", before)}");
			Log($"after : {string.Join(", ", after)}");
			Log($"output: {string.Join(", ", results)}");

			Assert.That(before, Is.EqualTo(Enumerable.Range(0, 10).ToList()));
			Assert.That(after, Is.EqualTo(Enumerable.Range(0, 10).Where(x => x % 2 == 1).ToList()));
			Assert.That(results, Is.EqualTo(Enumerable.Range(1, 5).Select(x => x * 2).ToList()));

		}
	}

}
