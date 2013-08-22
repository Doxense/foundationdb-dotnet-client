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
	public class RangeQueryFacts
	{

		[Test]
		public async Task Test_Can_Get_Range()
		{
			// test that we can get a range of keys

			const int N = 1000; // total item count

			using (var db = await TestHelpers.OpenTestDatabaseAsync())
			{
				// put test values in a namespace
				var location = db.Partition("Range");

				// cleanup everything
				using (var tr = db.BeginTransaction())
				{
					tr.ClearRange(location);
					await tr.CommitAsync();
				}

				// insert all values (batched)
				Console.WriteLine("Inserting " + N.ToString("N0") + " keys...");
				var insert = Stopwatch.StartNew();

				using (var tr = db.BeginTransaction())
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

				using (var tr = db.BeginTransaction())
				{
					var query = tr.GetRange(location.Pack(0), location.Pack(N));
					Assert.That(query, Is.Not.Null);
					Assert.That(query, Is.InstanceOf<IFdbAsyncEnumerable<KeyValuePair<Slice, Slice>>>());

					Console.WriteLine("Getting range " + query.Range.Start + " -> " + query.Range.Stop + " ...");

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
		public async Task Test_Can_Get_Range_Batched()
		{
			// test that we can get a range of keys

			const int N = 1000; // total item count

			using (var db = await TestHelpers.OpenTestDatabaseAsync())
			{
				// put test values in a namespace
				var location = db.Partition("Range");

				// cleanup everything
				using (var tr = db.BeginTransaction())
				{
					tr.ClearRange(location);
					await tr.CommitAsync();
				}

				// insert all values (batched)
				Console.WriteLine("Inserting " + N.ToString("N0") + " keys...");
				var insert = Stopwatch.StartNew();

				using (var tr = db.BeginTransaction())
				{
					foreach (int i in Enumerable.Range(0, N))
					{
						tr.Set(location.Pack(i), Slice.FromInt32(i));
					}

					await tr.CommitAsync();
				}
				insert.Stop();

				Console.WriteLine("Committed " + N + " keys in " + insert.Elapsed.TotalMilliseconds.ToString("N1") + " ms");

				// GetRange by batch

				using (var tr = db.BeginTransaction())
				{
					var query = tr
						.GetRange(location.Pack(0), location.Pack(N))
						.Batched();

					Assert.That(query, Is.Not.Null);
					Assert.That(query, Is.InstanceOf<IFdbAsyncEnumerable<KeyValuePair<Slice, Slice>[]>>());

					var ts = Stopwatch.StartNew();
					var chunks = await query.ToListAsync();
					ts.Stop();

					Assert.That(chunks, Is.Not.Null);
					Assert.That(chunks.Count, Is.GreaterThan(0), "Should at least return one chunk");
					Console.WriteLine("Got " + chunks.Count + " chunks");
					Assert.That(chunks, Is.All.Not.Null, "Should nether return null chunks");
					Assert.That(chunks, Is.All.Not.Empty, "Should nether return empty chunks");
					Assert.That(chunks.Sum(c => c.Length), Is.EqualTo(N), "Total size should match");
					Console.WriteLine("Took " + ts.Elapsed.TotalMilliseconds.ToString("N1") + " ms to get " + chunks.Count.ToString("N0") + " chunks");

					var keys = chunks.SelectMany(chunk => chunk.Select(x => FdbTuple.Unpack(x.Key).Last<int>())).ToArray();
					Assert.That(keys.Length, Is.EqualTo(N));
					var values = chunks.SelectMany(chunk => chunk.Select(x => x.Value.ToInt32())).ToArray();
					Assert.That(values.Length, Is.EqualTo(N));

					for (int i = 0; i < N; i++)
					{
						Assert.That(keys[i], Is.EqualTo(i));
						Assert.That(values[i], Is.EqualTo(i));
					}

				}
			}
		}

		[Test]
		public async Task Test_Can_MergeSort()
		{
			int K = 3;
			int N = 100;

			using(var db = await TestHelpers.OpenTestDatabaseAsync())
			{

				var location = db.Partition("MergeSort");

				// clear!
				await db.ClearRangeAsync(location);

				// create K lists
				var lists = Enumerable.Range(0, K).Select(i => location.Partition(i)).ToArray();

				// lists[0] contains all multiples of K ([0, 0], [K, 1], [2K, 2], ...)
				// lists[1] contains all multiples of K, offset by 1 ([1, 0], [K+1, 1], [2K+1, 2], ...)
				// lists[k-1] contains all multiples of K, offset by k-1 ([K-1, 0], [2K-1, 1], [3K-1, 2], ...)

				// more generally: lists[k][i] = (..., MergeSort, k, (i * K) + k) = (k, i)

				for (int k = 0; k < K; k++)
				{
					using (var tr = db.BeginTransaction())
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

				using (var tr = db.BeginTransaction())
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

			using (var db = await TestHelpers.OpenTestDatabaseAsync())
			{

				var location = db.Partition("Intersect");

				// clear!
				await db.ClearRangeAsync(location);

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
					using (var tr = db.BeginTransaction())
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

				using (var tr = db.BeginTransaction())
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

			using (var db = await TestHelpers.OpenTestDatabaseAsync())
			{

				var location = db.Partition("Except");

				// clear!
				await db.ClearRangeAsync(location);

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
					using (var tr = db.BeginTransaction())
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

				using (var tr = db.BeginTransaction())
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
