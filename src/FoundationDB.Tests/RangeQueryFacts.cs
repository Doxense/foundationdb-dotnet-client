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

namespace FoundationDB.Client.Tests
{
	using FoundationDB.Layers.Tuples;
	using FoundationDB.Linq;
	using NUnit.Framework;
	using System;
	using System.Collections.Generic;
	using System.Diagnostics;
	using System.Linq;
	using System.Text;
	using System.Threading.Tasks;

	[TestFixture]
	public class RangeQueryFacts : FdbTest
	{

		[Test]
		public async Task Test_Can_Get_Range()
		{
			// test that we can get a range of keys

			const int N = 1000; // total item count

			using (var db = await OpenTestPartitionAsync())
			{
				// put test values in a namespace
				var location = await GetCleanDirectory(db, "range");

				// insert all values (batched)
				Console.WriteLine("Inserting " + N.ToString("N0") + " keys...");
				var insert = Stopwatch.StartNew();

				using (var tr = db.BeginTransaction(this.Cancellation))
				{ 
					foreach (int i in Enumerable.Range(0, N))
					{
						tr.Set(location.Pack(i), Slice.FromInt32(i));
					}

					await tr.CommitAsync();
				}
				insert.Stop();

				Console.WriteLine("Committed " + N + " keys in " + insert.Elapsed.TotalMilliseconds.ToString("N1") + " ms");

				// GetRange values

				using (var tr = db.BeginTransaction(this.Cancellation))
				{
					var query = tr.GetRange(location.Pack(0), location.Pack(N));
					Assert.That(query, Is.Not.Null);
					Assert.That(query.Transaction, Is.SameAs(tr));
					Assert.That(query.Begin.Key, Is.EqualTo(location.Pack(0)));
					Assert.That(query.End.Key, Is.EqualTo(location.Pack(N)));
					Assert.That(query.Limit, Is.EqualTo(0));
					Assert.That(query.TargetBytes, Is.EqualTo(0));
					Assert.That(query.Reverse, Is.False);
					Assert.That(query.Mode, Is.EqualTo(FdbStreamingMode.Iterator));
					Assert.That(query.Snapshot, Is.False);
					Assert.That(query.Range.Begin, Is.EqualTo(query.Begin));
					Assert.That(query.Range.End, Is.EqualTo(query.End));

					Console.WriteLine("Getting range " + query.Range.ToString() + " ...");

					var ts = Stopwatch.StartNew();
					var items = await query.ToListAsync();
					ts.Stop();

					Assert.That(items, Is.Not.Null);
					Assert.That(items.Count, Is.EqualTo(N));
					Console.WriteLine("Took " + ts.Elapsed.TotalMilliseconds.ToString("N1") + " ms to get " + items.Count.ToString("N0") + " results");

					for (int i = 0; i < N; i++)
					{
						var kvp = items[i];

						// key should be a tuple in the correct order
						var key = location.Unpack(kvp.Key);

						if (i % 128 == 0) Console.WriteLine("... " + key.ToString() + " = " + kvp.Value.ToString());

						Assert.That(key.Count, Is.EqualTo(1));
						Assert.That(key.Get<int>(-1), Is.EqualTo(i));

						// value should be a guid
						Assert.That(kvp.Value.ToInt32(), Is.EqualTo(i));
					}
				}

			}
		}

		[Test]
		public async Task Test_Can_Get_Range_First_Single_And_Last()
		{
			using (var db = await OpenTestPartitionAsync())
			{
				// put test values in a namespace
				var location = await GetCleanDirectory(db, "range");

				var a = location.Partition("a");
				var b = location.Partition("b");
				var c = location.Partition("c");

				// insert a bunch of keys under 'a', only one under 'b', and nothing under 'c'
				using (var tr = db.BeginTransaction(this.Cancellation))
				{
					for (int i = 0; i < 10; i++)
					{
						tr.Set(a.Pack(i), Slice.FromInt32(i));
					}
					tr.Set(b.Pack(0), Slice.FromInt32(42));
					await tr.CommitAsync();
				}

				KeyValuePair<Slice, Slice> res;

				// A: more then one item
				using (var tr = db.BeginTransaction(this.Cancellation))
				{
					var query = tr.GetRange(a.ToRange());

					// should return the first one
					res = await query.FirstOrDefaultAsync();
					Assert.That(res.Key, Is.EqualTo(a.Pack(0)));
					Assert.That(res.Value, Is.EqualTo(Slice.FromInt32(0)));

					// should return the first one
					res = await query.FirstAsync();
					Assert.That(res.Key, Is.EqualTo(a.Pack(0)));
					Assert.That(res.Value, Is.EqualTo(Slice.FromInt32(0)));

					// should return the last one
					res = await query.LastOrDefaultAsync();
					Assert.That(res.Key, Is.EqualTo(a.Pack(9)));
					Assert.That(res.Value, Is.EqualTo(Slice.FromInt32(9)));

					// should return the last one
					res = await query.LastAsync();
					Assert.That(res.Key, Is.EqualTo(a.Pack(9)));
					Assert.That(res.Value, Is.EqualTo(Slice.FromInt32(9)));

					// should fail because there is more than one
					Assert.Throws<InvalidOperationException>(async () => await query.SingleOrDefaultAsync(), "SingleOrDefaultAsync should throw if the range returns more than 1 result");

					// should fail because there is more than one
					Assert.Throws<InvalidOperationException>(async () => await query.SingleAsync(), "SingleAsync should throw if the range returns more than 1 result");
				}

				// B: exactly one item
				using (var tr = db.BeginTransaction(this.Cancellation))
				{
					var query = tr.GetRange(b.ToRange());

					// should return the first one
					res = await query.FirstOrDefaultAsync();
					Assert.That(res.Key, Is.EqualTo(b.Pack(0)));
					Assert.That(res.Value, Is.EqualTo(Slice.FromInt32(42)));

					// should return the first one
					res = await query.FirstAsync();
					Assert.That(res.Key, Is.EqualTo(b.Pack(0)));
					Assert.That(res.Value, Is.EqualTo(Slice.FromInt32(42)));

					// should return the last one
					res = await query.LastOrDefaultAsync();
					Assert.That(res.Key, Is.EqualTo(b.Pack(0)));
					Assert.That(res.Value, Is.EqualTo(Slice.FromInt32(42)));

					// should return the last one
					res = await query.LastAsync();
					Assert.That(res.Key, Is.EqualTo(b.Pack(0)));
					Assert.That(res.Value, Is.EqualTo(Slice.FromInt32(42)));

					// should return the first one
					res = await query.SingleOrDefaultAsync();
					Assert.That(res.Key, Is.EqualTo(b.Pack(0)));
					Assert.That(res.Value, Is.EqualTo(Slice.FromInt32(42)));

					// should return the first one
					res = await query.SingleAsync();
					Assert.That(res.Key, Is.EqualTo(b.Pack(0)));
					Assert.That(res.Value, Is.EqualTo(Slice.FromInt32(42)));
				}

				// C: no items
				using (var tr = db.BeginTransaction(this.Cancellation))
				{
					var query = tr.GetRange(c.ToRange());

					// should return nothing
					res = await query.FirstOrDefaultAsync();
					Assert.That(res.Key, Is.EqualTo(Slice.Nil));
					Assert.That(res.Value, Is.EqualTo(Slice.Nil));

					// should return the first one
					Assert.Throws<InvalidOperationException>(async () => await query.FirstAsync(), "FirstAsync should throw if the range returns nothing");

					// should return the last one
					res = await query.LastOrDefaultAsync();
					Assert.That(res.Key, Is.EqualTo(Slice.Nil));
					Assert.That(res.Value, Is.EqualTo(Slice.Nil));

					// should return the last one
					Assert.Throws<InvalidOperationException>(async () => await query.LastAsync(), "LastAsync should throw if the range returns nothing");

					// should fail because there is more than one
					res = await query.SingleOrDefaultAsync();
					Assert.That(res.Key, Is.EqualTo(Slice.Nil));
					Assert.That(res.Value, Is.EqualTo(Slice.Nil));

					// should fail because there is none
					Assert.Throws<InvalidOperationException>(async () => await query.SingleAsync(), "SingleAsync should throw if the range returns nothing");
				}

			}
		}

		[Test]
		public async Task Test_Can_MergeSort()
		{
			int K = 3;
			int N = 100;

			using(var db = await OpenTestPartitionAsync())
			{

				var location = db.Partition("MergeSort");

				// clear!
				await db.ClearRangeAsync(location, this.Cancellation);

				// create K lists
				var lists = Enumerable.Range(0, K).Select(i => location.Partition(i)).ToArray();

				// lists[0] contains all multiples of K ([0, 0], [K, 1], [2K, 2], ...)
				// lists[1] contains all multiples of K, offset by 1 ([1, 0], [K+1, 1], [2K+1, 2], ...)
				// lists[k-1] contains all multiples of K, offset by k-1 ([K-1, 0], [2K-1, 1], [3K-1, 2], ...)

				// more generally: lists[k][i] = (..., MergeSort, k, (i * K) + k) = (k, i)

				for (int k = 0; k < K; k++)
				{
					using (var tr = db.BeginTransaction(this.Cancellation))
					{
						for (int i = 0; i < N; i++)
						{
							tr.Set(lists[k].Pack((i * K) + k), FdbTuple.Pack(k, i));
						}
						await tr.CommitAsync();
					}
				}

				// MergeSorting all lists together should produce all integers from 0 to (K*N)-1, in order
				// we use the last part of the key for sorting

				using (var tr = db.BeginTransaction(this.Cancellation))
				{
					var merge = tr.MergeSort(
						lists.Select(list => list.ToSelectorPair()),
						kvp => location.UnpackLast<int>(kvp.Key)
					);

					Assert.That(merge, Is.Not.Null);
					Assert.That(merge, Is.InstanceOf<FdbMergeSortIterator<KeyValuePair<Slice, Slice>, int, KeyValuePair<Slice, Slice>>>());

					var results = await merge.ToListAsync();
					Assert.That(results, Is.Not.Null);
					Assert.That(results.Count, Is.EqualTo(K * N));

					for(int i=0;i<K*N;i++)
					{
						Assert.That(location.Extract(results[i].Key), Is.EqualTo(FdbTuple.Pack(i % K, i)));
						Assert.That(results[i].Value, Is.EqualTo(FdbTuple.Pack(i % K, i / K)));
					}
				}


			}

		}

		[Test]
		public async Task Test_Range_Intersect()
		{
			int K = 3;
			int N = 100;

			using (var db = await OpenTestPartitionAsync())
			{

				var location = db.Partition("Intersect");

				// clear!
				await db.ClearRangeAsync(location, this.Cancellation);

				// create K lists
				var lists = Enumerable.Range(0, K).Select(i => location.Partition(i)).ToArray();

				// lists[0] contains all multiples of 1
				// lists[1] contains all multiples of 2
				// lists[k-1] contains all multiples of K

				// more generally: lists[k][i] = (..., Intersect, k, i * (k + 1)) = (k, i)

				var series = Enumerable.Range(1, K).Select(k => Enumerable.Range(1, N).Select(x => k * x).ToArray()).ToArray();
				//foreach(var serie in series)
				//{
				//	Console.WriteLine(String.Join(", ", serie));
				//}

				for (int k = 0; k < K; k++)
				{
					//Console.WriteLine("> k = " + k);
					using (var tr = db.BeginTransaction(this.Cancellation))
					{
						for (int i = 0; i < N; i++)
						{
							var key = lists[k].Pack(series[k][i]);
							var value = FdbTuple.Pack(k, i);
							//Console.WriteLine("> " + key + " = " + value);
							tr.Set(key, value);
						}
						await tr.CommitAsync();
					}
				}

				// Intersect all lists together should produce all integers that are multiples of numbers from 1 to K
				IEnumerable<int> xs = series[0];
				for (int i = 1; i < K; i++) xs = xs.Intersect(series[i]);
				var expected = xs.ToArray();
				Console.WriteLine(String.Join(", ", expected));

				using (var tr = db.BeginTransaction(this.Cancellation))
				{
					var merge = tr.Intersect(
						lists.Select(list => list.ToSelectorPair()),
						kvp => location.UnpackLast<int>(kvp.Key)
					);

					Assert.That(merge, Is.Not.Null);
					Assert.That(merge, Is.InstanceOf<FdbIntersectIterator<KeyValuePair<Slice, Slice>, int, KeyValuePair<Slice, Slice>>>());

					var results = await merge.ToListAsync();
					Assert.That(results, Is.Not.Null);

					Assert.That(results.Count, Is.EqualTo(expected.Length));

					for (int i = 0; i < results.Count; i++)
					{
						Assert.That(location.UnpackLast<int>(results[i].Key), Is.EqualTo(expected[i]));
					}
				}


			}

		}

		[Test]
		public async Task Test_Range_Except()
		{
			int K = 3;
			int N = 100;

			using (var db = await OpenTestPartitionAsync())
			{

				var location = db.Partition("Except");

				// clear!
				await db.ClearRangeAsync(location, this.Cancellation);

				// create K lists
				var lists = Enumerable.Range(0, K).Select(i => location.Partition(i)).ToArray();

				// lists[0] contains all multiples of 1
				// lists[1] contains all multiples of 2
				// lists[k-1] contains all multiples of K

				// more generally: lists[k][i] = (..., Intersect, k, i * (k + 1)) = (k, i)

				var series = Enumerable.Range(1, K).Select(k => Enumerable.Range(1, N).Select(x => k * x).ToArray()).ToArray();
				//foreach(var serie in series)
				//{
				//	Console.WriteLine(String.Join(", ", serie));
				//}

				for (int k = 0; k < K; k++)
				{
					//Console.WriteLine("> k = " + k);
					using (var tr = db.BeginTransaction(this.Cancellation))
					{
						for (int i = 0; i < N; i++)
						{
							var key = lists[k].Pack(series[k][i]);
							var value = FdbTuple.Pack(k, i);
							//Console.WriteLine("> " + key + " = " + value);
							tr.Set(key, value);
						}
						await tr.CommitAsync();
					}
				}

				// Intersect all lists together should produce all integers that are prime numbers
				IEnumerable<int> xs = series[0];
				for (int i = 1; i < K; i++) xs = xs.Except(series[i]);
				var expected = xs.ToArray();
				Console.WriteLine(String.Join(", ", expected));

				using (var tr = db.BeginTransaction(this.Cancellation))
				{
					var merge = tr.Except(
						lists.Select(list => list.ToSelectorPair()),
						kvp => location.UnpackLast<int>(kvp.Key)
					);

					Assert.That(merge, Is.Not.Null);
					Assert.That(merge, Is.InstanceOf<FdbExceptIterator<KeyValuePair<Slice, Slice>, int, KeyValuePair<Slice, Slice>>>());

					var results = await merge.ToListAsync();
					Assert.That(results, Is.Not.Null);

					Assert.That(results.Count, Is.EqualTo(expected.Length));

					for (int i = 0; i < results.Count; i++)
					{
						Assert.That(location.UnpackLast<int>(results[i].Key), Is.EqualTo(expected[i]));
					}
				}

			}

		}

	}
}
