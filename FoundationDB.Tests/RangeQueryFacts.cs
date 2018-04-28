﻿#region BSD Licence
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
 
namespace FoundationDB.Client.Tests
{
	using System;
	using System.Collections.Generic;
	using System.Diagnostics;
	using System.Linq;
	using System.Threading.Tasks;
	using Doxense.Collections.Tuples;
	using Doxense.Linq;
	using Doxense.Linq.Async.Iterators;
	using Doxense.Serialization.Encoders;
	using FoundationDB.Layers.Directories;
	using NUnit.Framework;

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
				var location = await GetCleanDirectory(db, "Queries", "Range");

				// insert all values (batched)
				Log("Inserting {0:N0} keys...", N);
				var insert = Stopwatch.StartNew();

				using (var tr = db.BeginTransaction(this.Cancellation))
				{ 
					foreach (int i in Enumerable.Range(0, N))
					{
						tr.Set(location.Keys.Encode(i), Slice.FromInt32(i));
					}

					await tr.CommitAsync();
				}
				insert.Stop();

				Log("Committed {0:N0} keys in {1:N1} ms", N, insert.Elapsed.TotalMilliseconds);

				// GetRange values

				using (var tr = db.BeginTransaction(this.Cancellation))
				{
					var query = tr.GetRange(location.Keys.Encode(0), location.Keys.Encode(N));
					Assert.That(query, Is.Not.Null);
					Assert.That(query.Transaction, Is.SameAs(tr));
					Assert.That(query.Begin.Key, Is.EqualTo(location.Keys.Encode(0)));
					Assert.That(query.End.Key, Is.EqualTo(location.Keys.Encode(N)));
					Assert.That(query.Limit, Is.Null);
					Assert.That(query.TargetBytes, Is.Null);
					Assert.That(query.Reversed, Is.False);
					Assert.That(query.Mode, Is.EqualTo(FdbStreamingMode.Iterator));
					Assert.That(query.Snapshot, Is.False);
					Assert.That(query.Range.Begin, Is.EqualTo(query.Begin));
					Assert.That(query.Range.End, Is.EqualTo(query.End));

					Log("Getting range {0} ...", query.Range);

					var ts = Stopwatch.StartNew();
					var items = await query.ToListAsync();
					ts.Stop();

					Assert.That(items, Is.Not.Null);
					Assert.That(items.Count, Is.EqualTo(N));
					Log("Took {0:N1} ms to get {1:N0} results", ts.Elapsed.TotalMilliseconds, items.Count);

					for (int i = 0; i < N; i++)
					{
						var kvp = items[i];

						// key should be a tuple in the correct order
						var key = location.Keys.Unpack(kvp.Key);

						if (i % 128 == 0) Log("... {0} = {1}", key, kvp.Value);

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
				var location = await GetCleanDirectory(db, "Queries", "Range");

				var a = location.Partition.ByKey("a");
				var b = location.Partition.ByKey("b");
				var c = location.Partition.ByKey("c");

				// insert a bunch of keys under 'a', only one under 'b', and nothing under 'c'
				await db.WriteAsync((tr) =>
				{
					for (int i = 0; i < 10; i++)
					{
						tr.Set(a.Keys.Encode(i), Slice.FromInt32(i));
					}
					tr.Set(b.Keys.Encode(0), Slice.FromInt32(42));
				}, this.Cancellation);

				KeyValuePair<Slice, Slice> res;

				// A: more then one item
				using (var tr = db.BeginReadOnlyTransaction(this.Cancellation))
				{
					var query = tr.GetRange(a.Keys.ToRange());

					// should return the first one
					res = await query.FirstOrDefaultAsync();
					Assert.That(res.Key, Is.EqualTo(a.Keys.Encode(0)));
					Assert.That(res.Value, Is.EqualTo(Slice.FromInt32(0)));

					// should return the first one
					res = await query.FirstAsync();
					Assert.That(res.Key, Is.EqualTo(a.Keys.Encode(0)));
					Assert.That(res.Value, Is.EqualTo(Slice.FromInt32(0)));

					// should return the last one
					res = await query.LastOrDefaultAsync();
					Assert.That(res.Key, Is.EqualTo(a.Keys.Encode(9)));
					Assert.That(res.Value, Is.EqualTo(Slice.FromInt32(9)));

					// should return the last one
					res = await query.LastAsync();
					Assert.That(res.Key, Is.EqualTo(a.Keys.Encode(9)));
					Assert.That(res.Value, Is.EqualTo(Slice.FromInt32(9)));

					// should fail because there is more than one
					Assert.That(async () => await query.SingleOrDefaultAsync(), Throws.InstanceOf<InvalidOperationException>(), "SingleOrDefaultAsync should throw if the range returns more than 1 result");

					// should fail because there is more than one
					Assert.That(async () => await query.SingleAsync(), Throws.InstanceOf<InvalidOperationException>(), "SingleAsync should throw if the range returns more than 1 result");
				}

				// B: exactly one item
				using (var tr = db.BeginReadOnlyTransaction(this.Cancellation))
				{
					var query = tr.GetRange(b.Keys.ToRange());

					// should return the first one
					res = await query.FirstOrDefaultAsync();
					Assert.That(res.Key, Is.EqualTo(b.Keys.Encode(0)));
					Assert.That(res.Value, Is.EqualTo(Slice.FromInt32(42)));

					// should return the first one
					res = await query.FirstAsync();
					Assert.That(res.Key, Is.EqualTo(b.Keys.Encode(0)));
					Assert.That(res.Value, Is.EqualTo(Slice.FromInt32(42)));

					// should return the last one
					res = await query.LastOrDefaultAsync();
					Assert.That(res.Key, Is.EqualTo(b.Keys.Encode(0)));
					Assert.That(res.Value, Is.EqualTo(Slice.FromInt32(42)));

					// should return the last one
					res = await query.LastAsync();
					Assert.That(res.Key, Is.EqualTo(b.Keys.Encode(0)));
					Assert.That(res.Value, Is.EqualTo(Slice.FromInt32(42)));

					// should return the first one
					res = await query.SingleOrDefaultAsync();
					Assert.That(res.Key, Is.EqualTo(b.Keys.Encode(0)));
					Assert.That(res.Value, Is.EqualTo(Slice.FromInt32(42)));

					// should return the first one
					res = await query.SingleAsync();
					Assert.That(res.Key, Is.EqualTo(b.Keys.Encode(0)));
					Assert.That(res.Value, Is.EqualTo(Slice.FromInt32(42)));
				}

				// C: no items
				using (var tr = db.BeginReadOnlyTransaction(this.Cancellation))
				{
					var query = tr.GetRange(c.Keys.ToRange());

					// should return nothing
					res = await query.FirstOrDefaultAsync();
					Assert.That(res.Key, Is.EqualTo(Slice.Nil));
					Assert.That(res.Value, Is.EqualTo(Slice.Nil));

					// should return the first one
					Assert.That(async () => await query.FirstAsync(), Throws.InstanceOf<InvalidOperationException>(), "FirstAsync should throw if the range returns nothing");

					// should return the last one
					res = await query.LastOrDefaultAsync();
					Assert.That(res.Key, Is.EqualTo(Slice.Nil));
					Assert.That(res.Value, Is.EqualTo(Slice.Nil));

					// should return the last one
					Assert.That(async () => await query.LastAsync(), Throws.InstanceOf<InvalidOperationException>(), "LastAsync should throw if the range returns nothing");

					// should fail because there is more than one
					res = await query.SingleOrDefaultAsync();
					Assert.That(res.Key, Is.EqualTo(Slice.Nil));
					Assert.That(res.Value, Is.EqualTo(Slice.Nil));

					// should fail because there is none
					Assert.That(async () => await query.SingleAsync(), Throws.InstanceOf<InvalidOperationException>(), "SingleAsync should throw if the range returns nothing");
				}

				// A: with a size limit
				using (var tr = db.BeginReadOnlyTransaction(this.Cancellation))
				{
					var query = tr.GetRange(a.Keys.ToRange()).Take(5);

					// should return the fifth one
					res = await query.LastOrDefaultAsync();
					Assert.That(res.Key, Is.EqualTo(a.Keys.Encode(4)));
					Assert.That(res.Value, Is.EqualTo(Slice.FromInt32(4)));

					// should return the fifth one
					res = await query.LastAsync();
					Assert.That(res.Key, Is.EqualTo(a.Keys.Encode(4)));
					Assert.That(res.Value, Is.EqualTo(Slice.FromInt32(4)));
				}

				// A: with an offset
				using (var tr = db.BeginReadOnlyTransaction(this.Cancellation))
				{
					var query = tr.GetRange(a.Keys.ToRange()).Skip(5);

					// should return the fifth one
					res = await query.FirstOrDefaultAsync();
					Assert.That(res.Key, Is.EqualTo(a.Keys.Encode(5)));
					Assert.That(res.Value, Is.EqualTo(Slice.FromInt32(5)));

					// should return the fifth one
					res = await query.FirstAsync();
					Assert.That(res.Key, Is.EqualTo(a.Keys.Encode(5)));
					Assert.That(res.Value, Is.EqualTo(Slice.FromInt32(5)));
				}

			}
		}

		[Test]
		public async Task Test_Can_Get_Range_With_Limit()
		{
			using (var db = await OpenTestPartitionAsync())
			{

				// put test values in a namespace
				var location = await GetCleanDirectory(db, "Queries", "Range");

				var a = location.Partition.ByKey("a");

				// insert a bunch of keys under 'a'
				await db.WriteAsync((tr) =>
				{
					for (int i = 0; i < 10; i++)
					{
						tr.Set(a.Keys.Encode(i), Slice.FromInt32(i));
					}
					// add guard keys
					tr.Set(location.GetPrefix(), Slice.FromInt32(-1));
					tr.Set(location.GetPrefix() + (byte)255, Slice.FromInt32(-1));
				}, this.Cancellation);

				// Take(5) should return the first 5 items

				using (var tr = db.BeginReadOnlyTransaction(this.Cancellation))
				{
					var query = tr.GetRange(a.Keys.ToRange()).Take(5);
					Assert.That(query, Is.Not.Null);
					Assert.That(query.Limit, Is.EqualTo(5));

					var elements = await query.ToListAsync();
					Assert.That(elements, Is.Not.Null);
					Assert.That(elements.Count, Is.EqualTo(5));
					for (int i = 0; i < 5; i++)
					{
						Assert.That(elements[i].Key, Is.EqualTo(a.Keys.Encode(i)));
						Assert.That(elements[i].Value, Is.EqualTo(Slice.FromInt32(i)));
					}
				}

				// Take(12) should return only the 10 items

				using (var tr = db.BeginReadOnlyTransaction(this.Cancellation))
				{
					var query = tr.GetRange(a.Keys.ToRange()).Take(12);
					Assert.That(query, Is.Not.Null);
					Assert.That(query.Limit, Is.EqualTo(12));

					var elements = await query.ToListAsync();
					Assert.That(elements, Is.Not.Null);
					Assert.That(elements.Count, Is.EqualTo(10));
					for (int i = 0; i < 10; i++)
					{
						Assert.That(elements[i].Key, Is.EqualTo(a.Keys.Encode(i)));
						Assert.That(elements[i].Value, Is.EqualTo(Slice.FromInt32(i)));
					}
				}

				// Take(0) should return nothing

				using (var tr = db.BeginReadOnlyTransaction(this.Cancellation))
				{
					var query = tr.GetRange(a.Keys.ToRange()).Take(0);
					Assert.That(query, Is.Not.Null);
					Assert.That(query.Limit, Is.Zero);

					var elements = await query.ToListAsync();
					Assert.That(elements, Is.Not.Null);
					Assert.That(elements.Count, Is.Zero);
				}

			}
		}

		[Test]
		public async Task Test_Can_Skip()
		{
			using (var db = await OpenTestPartitionAsync())
			{
				// put test values in a namespace
				var location = await GetCleanDirectory(db, "Queries", "Range");

				// import test data
				var data = Enumerable.Range(0, 100).Select(x => new KeyValuePair<Slice, Slice>(location.Keys.Encode(x), Slice.FromFixed32(x)));
				await Fdb.Bulk.WriteAsync(db, data, this.Cancellation);

				// from the start
				using (var tr = db.BeginReadOnlyTransaction(this.Cancellation))
				{
					var query = tr.GetRange(location.Keys.ToRange());

					// |>>>>>>>>>>>>(50---------->99)|
					var res = await query.Skip(50).ToListAsync();
					Assert.That(res, Is.EqualTo(data.Skip(50).ToList()), "50 --> 99");

					// |>>>>>>>>>>>>>>>>(60------>99)|
					res = await query.Skip(50).Skip(10).ToListAsync();
					Assert.That(res, Is.EqualTo(data.Skip(60).ToList()), "60 --> 99");

					// |xxxxxxxxxxxxxxxxxxxxxxx(99->)|
					res = await query.Skip(99).ToListAsync();
					Assert.That(res.Count, Is.EqualTo(1));
					Assert.That(res, Is.EqualTo(data.Skip(99).ToList()), "99 --> 99");

					// |xxxxxxxxxxxxxxxxxxxxxxxxxxxxx|(100->)
					res = await query.Skip(100).ToListAsync();
					Assert.That(res.Count, Is.Zero, "100 --> 99");

					// |xxxxxxxxxxxxxxxxxxxxxxxxxxxxx|_____________(150->)
					res = await query.Skip(150).ToListAsync();
					Assert.That(res.Count, Is.Zero, "150 --> 100");
				}

				// from the end
				using (var tr = db.BeginReadOnlyTransaction(this.Cancellation))
				{
					var query = tr.GetRange(location.Keys.ToRange());

					// |(0 <--------- 49)<<<<<<<<<<<<<|
					var res = await query.Reverse().Skip(50).ToListAsync();
					Assert.That(res, Is.EqualTo(data.Reverse().Skip(50).ToList()), "0 <-- 49");

					// |(0 <----- 39)<<<<<<<<<<<<<<<<<|
					res = await query.Reverse().Skip(50).Skip(10).ToListAsync();
					Assert.That(res, Is.EqualTo(data.Reverse().Skip(60).ToList()), "0 <-- 39");

					// |(<- 0)<<<<<<<<<<<<<<<<<<<<<<<<|
					res = await query.Reverse().Skip(99).ToListAsync();
					Assert.That(res.Count, Is.EqualTo(1));
					Assert.That(res, Is.EqualTo(data.Reverse().Skip(99).ToList()), "0 <-- 0");

					// (<- -1)|<<<<<<<<<<<<<<<<<<<<<<<<<<<<<|
					res = await query.Reverse().Skip(100).ToListAsync();
					Assert.That(res.Count, Is.Zero, "0 <-- -1");

					// (<- -51)<<<<<<<<<<<<<|<<<<<<<<<<<<<<<<<<<<<<<<<<<<<|
					res = await query.Reverse().Skip(100).ToListAsync();
					Assert.That(res.Count, Is.Zero, "0 <-- -51");
				}

				// from both sides
				using (var tr = db.BeginReadOnlyTransaction(this.Cancellation))
				{
					var query = tr.GetRange(location.Keys.ToRange());

					// |>>>>>>>>>(25<------------74)<<<<<<<<|
					var res = await query.Skip(25).Reverse().Skip(25).ToListAsync();
					Assert.That(res, Is.EqualTo(data.Skip(25).Reverse().Skip(25).ToList()), "25 <-- 74");

					// |>>>>>>>>>(25------------>74)<<<<<<<<|
					res = await query.Skip(25).Reverse().Skip(25).Reverse().ToListAsync();
					Assert.That(res, Is.EqualTo(data.Skip(25).Reverse().Skip(25).Reverse().ToList()), "25 --> 74");
				}
			}
		}

		[Test]
		public async Task Test_Original_Range_Does_Not_Overflow()
		{
			using (var db = await OpenTestPartitionAsync())
			{
				// put test values in a namespace
				var location = await GetCleanDirectory(db, "Queries", "Range");

				// import test data
				var data = Enumerable.Range(0, 30).Select(x => (location.Keys.Encode(x), Slice.FromFixed32(x)));
				await Fdb.Bulk.WriteAsync(db, data, this.Cancellation);

				using (var tr = db.BeginReadOnlyTransaction(this.Cancellation))
				{
					var query = tr
						.GetRange(location.Keys.Encode(10), location.Keys.Encode(20)) // 10 -> 19
						.Take(20) // 10 -> 19 (limit 20)
						.Reverse(); // 19 -> 10 (limit 20)
					Log("query: {0}", query);

					// set a limit that overflows, and then reverse from it
					var res = await query.ToListAsync();
					Assert.That(res.Count, Is.EqualTo(10));
				}

				using (var tr = db.BeginReadOnlyTransaction(this.Cancellation))
				{
					var query = tr
						.GetRange(location.Keys.Encode(10), location.Keys.Encode(20)) // 10 -> 19
						.Reverse() // 19 -> 10
						.Take(20)  // 19 -> 10 (limit 20)
						.Reverse(); // 10 -> 19 (limit 20)
					Log("query: {0}", query);

					var res = await query.ToListAsync();
					Assert.That(res.Count, Is.EqualTo(10));
				}
			}
		}

		[Test]
		public async Task Test_Can_MergeSort()
		{
			int K = 3;
			int N = 100;

			using (var db = await OpenTestPartitionAsync())
			{
				var location = await GetCleanDirectory(db, "Queries", "MergeSort");

				// clear!
				await db.ClearRangeAsync(location, this.Cancellation);

				// create K lists
				var lists = Enumerable.Range(0, K).Select(i => location.Partition.ByKey(i)).ToArray();

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
							tr.Set(lists[k].Keys.Encode((i * K) + k), TuPack.EncodeKey(k, i));
						}
						await tr.CommitAsync();
					}
				}

				// MergeSorting all lists together should produce all integers from 0 to (K*N)-1, in order
				// we use the last part of the key for sorting

				using (var tr = db.BeginTransaction(this.Cancellation))
				{
					var merge = tr.MergeSort(
						lists.Select(list => KeySelectorPair.Create(list.Keys.ToRange())),
						kvp => location.Keys.DecodeLast<int>(kvp.Key)
						);

					Assert.That(merge, Is.Not.Null);
					Assert.That(merge, Is.InstanceOf<MergeSortAsyncIterator<KeyValuePair<Slice, Slice>, int, KeyValuePair<Slice, Slice>>>());

					var results = await merge.ToListAsync();
					Assert.That(results, Is.Not.Null);
					Assert.That(results.Count, Is.EqualTo(K * N));

					for (int i = 0; i < K * N; i++)
					{
						Assert.That(location.ExtractKey(results[i].Key), Is.EqualTo(TuPack.EncodeKey(i % K, i)));
						Assert.That(results[i].Value, Is.EqualTo(TuPack.EncodeKey(i % K, i / K)));
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
				var location = await GetCleanDirectory(db, "Queries", "Intersect");

				// create K lists
				var lists = Enumerable.Range(0, K).Select(i => location.Partition.ByKey(i)).ToArray();

				// lists[0] contains all multiples of 1
				// lists[1] contains all multiples of 2
				// lists[k-1] contains all multiples of K

				// more generally: lists[k][i] = (..., Intersect, k, i * (k + 1)) = (k, i)

				var series = Enumerable.Range(1, K).Select(k => Enumerable.Range(1, N).Select(x => k * x).ToArray()).ToArray();
				//foreach(var serie in series)
				//{
				//	Log(String.Join(", ", serie));
				//}

				for (int k = 0; k < K; k++)
				{
					//Log("> k = " + k);
					using (var tr = db.BeginTransaction(this.Cancellation))
					{
						for (int i = 0; i < N; i++)
						{
							var key = lists[k].Keys.Encode(series[k][i]);
							var value = TuPack.EncodeKey(k, i);
							//Log("> " + key + " = " + value);
							tr.Set(key, value);
						}
						await tr.CommitAsync();
					}
				}

				// Intersect all lists together should produce all integers that are multiples of numbers from 1 to K
				IEnumerable<int> xs = series[0];
				for (int i = 1; i < K; i++) xs = xs.Intersect(series[i]);
				var expected = xs.ToArray();
				Log("Expected: {0}", String.Join(", ", expected));

				using (var tr = db.BeginTransaction(this.Cancellation))
				{
					var merge = tr.Intersect(
						lists.Select(list => KeySelectorPair.Create(list.Keys.ToRange())),
						kvp => location.Keys.DecodeLast<int>(kvp.Key)
					);

					Assert.That(merge, Is.Not.Null);
					Assert.That(merge, Is.InstanceOf<IntersectAsyncIterator<KeyValuePair<Slice, Slice>, int, KeyValuePair<Slice, Slice>>>());

					var results = await merge.ToListAsync();
					Assert.That(results, Is.Not.Null);

					Assert.That(results.Count, Is.EqualTo(expected.Length));

					for (int i = 0; i < results.Count; i++)
					{
						Assert.That(location.Keys.DecodeLast<int>(results[i].Key), Is.EqualTo(expected[i]));
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
				// get a clean new directory
				var location = await GetCleanDirectory(db, "Queries", "Except");

				// create K lists
				var lists = Enumerable.Range(0, K).Select(i => location.Partition.ByKey(i)).ToArray();

				// lists[0] contains all multiples of 1
				// lists[1] contains all multiples of 2
				// lists[k-1] contains all multiples of K

				// more generally: lists[k][i] = (..., Intersect, k, i * (k + 1)) = (k, i)

				var series = Enumerable.Range(1, K).Select(k => Enumerable.Range(1, N).Select(x => k * x).ToArray()).ToArray();
				//foreach(var serie in series)
				//{
				//	Log(String.Join(", ", serie));
				//}

				for (int k = 0; k < K; k++)
				{
					//Log("> k = " + k);
					using (var tr = db.BeginTransaction(this.Cancellation))
					{
						for (int i = 0; i < N; i++)
						{
							var key = lists[k].Keys.Encode(series[k][i]);
							var value = TuPack.EncodeKey(k, i);
							//Log("> " + key + " = " + value);
							tr.Set(key, value);
						}
						await tr.CommitAsync();
					}
				}

				// Intersect all lists together should produce all integers that are prime numbers
				IEnumerable<int> xs = series[0];
				for (int i = 1; i < K; i++) xs = xs.Except(series[i]);
				var expected = xs.ToArray();
				Log("Expected: {0}", String.Join(", ", expected));

				using (var tr = db.BeginTransaction(this.Cancellation))
				{
					var merge = tr.Except(
						lists.Select(list => KeySelectorPair.Create(list.Keys.ToRange())),
						kvp => location.Keys.DecodeLast<int>(kvp.Key)
					);

					Assert.That(merge, Is.Not.Null);
					Assert.That(merge, Is.InstanceOf<ExceptAsyncIterator<KeyValuePair<Slice, Slice>, int, KeyValuePair<Slice, Slice>>>());

					var results = await merge.ToListAsync();
					Assert.That(results, Is.Not.Null);

					Assert.That(results.Count, Is.EqualTo(expected.Length));

					for (int i = 0; i < results.Count; i++)
					{
						Assert.That(location.Keys.DecodeLast<int>(results[i].Key), Is.EqualTo(expected[i]));
					}
				}

			}

		}

		[Test]
		public async Task Test_Range_Except_Composite_Key()
		{

			using (var db = await OpenTestPartitionAsync())
			{
				// get a clean new directory
				var location = await GetCleanDirectory(db, "Queries", "ExceptComposite");

				// Items contains a list of all ("user", id) that were created
				var locItems = (await location.CreateOrOpenAsync(db, "Items", this.Cancellation)).AsTyped<string, int>();
				// Processed contain the list of all ("user", id) that were processed
				var locProcessed = (await location.CreateOrOpenAsync(db, "Processed", this.Cancellation)).AsTyped<string, int>();

				// the goal is to have a query that returns the list of all unprocessed items (ie: in Items but not in Processed)

				await db.WriteAsync((tr) =>
				{
					// Items
					tr.Set(locItems.Keys["userA", 10093], Slice.Empty);
					tr.Set(locItems.Keys["userA", 19238], Slice.Empty);
					tr.Set(locItems.Keys["userB", 20003], Slice.Empty);
					// Processed
					tr.Set(locProcessed.Keys["userA", 19238], Slice.Empty);
				}, this.Cancellation);

				// the query (Items ∩ Processed) should return (userA, 10093) and (userB, 20003)

				// First Method: pass in a list of key ranges, and merge on the (Slice, Slice) pairs
				Trace.WriteLine("Method 1:");
				var results = await db.QueryAsync((tr) =>
				{
					var query = tr.Except(
						new[] { locItems.Keys.ToRange(), locProcessed.Keys.ToRange() },
						(kv) => TuPack.Unpack(kv.Key).Substring(-2), // note: keys come from any of the two ranges, so we must only keep the last 2 elements of the tuple
						TupleComparisons.Composite<string, int>() // compares t[0] as a string, and t[1] as an int
					);

					// problem: Except() still returns the original (Slice,Slice) pairs from the first range,
					// meaning that we still need to unpack agin the key (this time knowing the location)
					return query.Select(kv => locItems.Keys.Decode(kv.Key));
				}, this.Cancellation);

				foreach(var r in results)
				{
					Trace.WriteLine(r);
				}
				Assert.That(results.Count, Is.EqualTo(2));
				Assert.That(results[0], Is.EqualTo(("userA", 10093)));
				Assert.That(results[1], Is.EqualTo(("userB", 20003)));

				// Second Method: pre-parse the queries, and merge on the results directly
				Trace.WriteLine("Method 2:");
				results = await db.QueryAsync((tr) =>
				{
					var items = tr
						.GetRange(locItems.Keys.ToRange())
						.Select(kv => locItems.Keys.Decode(kv.Key));

					var processed = tr
						.GetRange(locProcessed.Keys.ToRange())
						.Select(kv => locProcessed.Keys.Decode(kv.Key));

					// items and processed are lists of (string, int) tuples, we can compare them directly
					var query = items.Except(processed, TupleComparisons.Composite<string, int>());

					// query is already a list of tuples, nothing more to do
					return query;
				}, this.Cancellation);

				foreach (var r in results)
				{
					Trace.WriteLine(r);
				}
				Assert.That(results.Count, Is.EqualTo(2));
				Assert.That(results[0], Is.EqualTo(("userA", 10093)));
				Assert.That(results[1], Is.EqualTo(("userB", 20003)));

			}

		}

	}
}
