﻿#region BSD Licence
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

#if REFACTORING_IN_PROGRESS

		[Test]
		public async Task Test_Can_Select_Sync_With_Index()
		{
			var source = Enumerable.Range(100, 10).ToAsyncEnumerable();

			var selected = source.Select((x, i) => x + i);
			Assert.That(selected, Is.Not.Null);

			var results = await selected.ToListAsync();
			Assert.That(results, Is.EqualTo(new int[] { 100, 102, 104, 106, 108, 110, 112, 114, 116, 118 }));
		}

#endif

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

#if REFACTORING_IN_PROGRESS

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

#endif

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
		public async Task Test_Can_Take()
		{
			var source = Enumerable.Range(0, 42).ToAsyncEnumerable();

			var query = source.Take(10);
			Assert.That(query, Is.Not.Null);
			Assert.That(query, Is.InstanceOf<FdbWhereSelectAsyncIterator<int, int>>());

			var results = await query.ToListAsync();
			Assert.That(results.Count, Is.EqualTo(10));
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
			Assert.That(results.Count, Is.EqualTo(10));
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
			Assert.That(results.Count, Is.EqualTo(5));
			Assert.That(results, Is.EqualTo(new int[] { 1, 3, 5, 7, 9 }));
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

#if REFACTORING_IN_PROGRESS

		[Test]
		public async Task Test_Can_Where_Indexed()
		{
			var source = Enumerable.Range(42, 10).ToAsyncEnumerable();

			var query = source.Where((x, i) => i == 0 || i == 3 || i == 6);
			Assert.That(query, Is.Not.Null);

			var results = await query.ToListAsync();
			Assert.That(results, Is.EqualTo(new int[] { 42, 45, 48 }));
		}

#endif

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
				try
				{
					res = await iterator.MoveNext(CancellationToken.None);
					Assert.Fail("Should have failed");
				}
				catch (AssertionException) { throw; }
				catch (Exception e)
				{
					Assert.That(e, Is.InstanceOf<FormatException>().And.Message.EqualTo("KABOOM"));
				}

				// accessing current should rethrow the exception
				Assert.That(() => iterator.Current, Throws.InstanceOf<InvalidOperationException>());

				// another attempt at MoveNext should fail immediately but with a different error
				try
				{
					res = await iterator.MoveNext(CancellationToken.None);
				}
				catch (AssertionException) { throw; }
				catch (Exception e)
				{
					Assert.That(e, Is.InstanceOf<InvalidOperationException>());
				}
			}
		}

		[Test]
		public async Task Test_Parallel_Select_Async()
		{
			const int MAX_CONCURRENCY = 5;

			// since this can lock up, we need a global timeout !
			using (var go = new CancellationTokenSource(TimeSpan.FromSeconds(10)))
			{
				var token = go.Token;

				int concurrent = 0;
				var rnd = new Random();

				var sw = Stopwatch.StartNew();

				var query = Enumerable.Range(1, 10)
					.ToAsyncEnumerable()
					.Select(async x => { await Task.Delay(new Random().Next(100)); return x; })
					.SelectAsync(async (x, ct) =>
					{
						var n = Interlocked.Increment(ref concurrent);
						Assert.That(n, Is.LessThanOrEqualTo(MAX_CONCURRENCY));
						try
						{
							Console.WriteLine("** " + sw.Elapsed.TotalMilliseconds + " start " + x + " (" + n + ")");
#if DEBUG_STACK_TRACES
							Console.WriteLine("> " + new StackTrace().ToString().Replace("\r\n", "\r\n> "));
#endif
							int ms;
							lock (rnd) { ms = rnd.Next(25) + 50; }
							await Task.Delay(ms);
							Console.WriteLine("** " + sw.Elapsed.TotalMilliseconds + " stop " + x + " (" + Volatile.Read(ref concurrent) + ")");

							return x * x;
						}
						finally
						{
							n = Interlocked.Decrement(ref concurrent);
							Assert.That(n, Is.GreaterThanOrEqualTo(0));
						}
					},
					new FdbParallelQueryOptions { MaxConcurrency = MAX_CONCURRENCY }
				);

				var results = await query.ToListAsync(token);

				Assert.That(Volatile.Read(ref concurrent), Is.EqualTo(0));
				Console.WriteLine(string.Join(", ", results));
				Assert.That(results, Is.EqualTo(new int[] { 1, 4, 9, 16, 25, 36, 49, 64, 81, 100 }));
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
	
	}

}
