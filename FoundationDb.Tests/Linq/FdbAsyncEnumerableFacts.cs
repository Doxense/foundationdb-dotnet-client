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
	* Neither the name of the <organization> nor the
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

namespace FoundationDb.Linq.Tests
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
		public async Task Test_Can_Get_First()
		{
			var source = Enumerable.Range(42, 3).ToAsyncEnumerable();
			int first = await source.First();
			Assert.That(first, Is.EqualTo(42));

			source = FdbAsyncEnumerable.Empty<int>();
			Assert.That(() => source.First().GetAwaiter().GetResult(), Throws.InstanceOf<InvalidOperationException>());

		}

		[Test]
		public async Task Test_Can_Get_FirstOrDefault()
		{
			var source = Enumerable.Range(42, 3).ToAsyncEnumerable();
			int first = await source.FirstOrDefault();
			Assert.That(first, Is.EqualTo(42));

			source = FdbAsyncEnumerable.Empty<int>();
			first = await source.FirstOrDefault();
			Assert.That(first, Is.EqualTo(0));

		}

		[Test]
		public async Task Test_Can_Get_Single()
		{
			var source = Enumerable.Range(42, 1).ToAsyncEnumerable();
			int first = await source.Single();
			Assert.That(first, Is.EqualTo(42));

			source = FdbAsyncEnumerable.Empty<int>();
			Assert.That(() => source.Single().GetAwaiter().GetResult(), Throws.InstanceOf<InvalidOperationException>());

			source = Enumerable.Range(42, 3).ToAsyncEnumerable();
			Assert.That(() => source.Single().GetAwaiter().GetResult(), Throws.InstanceOf<InvalidOperationException>());

		}

		[Test]
		public async Task Test_Can_Get_SingleOrDefault()
		{
			var source = Enumerable.Range(42, 1).ToAsyncEnumerable();
			int first = await source.SingleOrDefault();
			Assert.That(first, Is.EqualTo(42));

			source = FdbAsyncEnumerable.Empty<int>();
			first = await source.SingleOrDefault();
			Assert.That(first, Is.EqualTo(0));

			source = Enumerable.Range(42, 3).ToAsyncEnumerable();
			Assert.That(() => source.SingleOrDefault().GetAwaiter().GetResult(), Throws.InstanceOf<InvalidOperationException>());
		}

		[Test]
		public async Task Test_Can_Get_Last()
		{
			var source = Enumerable.Range(42, 3).ToAsyncEnumerable();
			int first = await source.Last();
			Assert.That(first, Is.EqualTo(44));

			source = FdbAsyncEnumerable.Empty<int>();
			Assert.That(() => source.Last().GetAwaiter().GetResult(), Throws.InstanceOf<InvalidOperationException>());

		}

		[Test]
		public async Task Test_Can_Get_LastOrDefault()
		{
			var source = Enumerable.Range(42, 3).ToAsyncEnumerable();
			int first = await source.LastOrDefault();
			Assert.That(first, Is.EqualTo(44));

			source = FdbAsyncEnumerable.Empty<int>();
			first = await source.LastOrDefault();
			Assert.That(first, Is.EqualTo(0));

		}

		[Test]
		public async Task Test_Can_ForEach()
		{
			var source = Enumerable.Range(0, 10).ToAsyncEnumerable();

			var items = new List<int>();

			await source.ForEach((x) =>
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

			await source.ForEach(async (x) =>
			{
				Assert.That(items.Count, Is.LessThan(10));
				await Task.Delay(10);
				items.Add(x);
			});

			Assert.That(items.Count, Is.EqualTo(10));
			Assert.That(items, Is.EqualTo(new int[] { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9 }));
		}
	}
}
