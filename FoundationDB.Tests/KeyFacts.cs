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
	using FoundationDB.Client;
	using FoundationDB.Layers.Tuples;
	using NUnit.Framework;
	using System;
	using System.Collections.Generic;
	using System.Linq;
	using System.Text;
	using System.Threading.Tasks;

	[TestFixture]
	public class KeyFacts
	{

		[Test]
		public void Test_FdbKey_Constants()
		{
			Assert.That(FdbKey.MinValue.GetBytes(), Is.EqualTo(new byte[] { 0 }));
			Assert.That(FdbKey.MaxValue.GetBytes(), Is.EqualTo(new byte[] { 255 }));
			Assert.That(FdbKey.System.GetBytes(), Is.EqualTo(new byte[] { 255 }));
			Assert.That(FdbKey.Directory.GetBytes(), Is.EqualTo(new byte[] { 254 }));

			Assert.That(Fdb.SystemKeys.ConfigPrefix.ToString(), Is.EqualTo("<FF>/conf/"));
			Assert.That(Fdb.SystemKeys.Coordinators.ToString(), Is.EqualTo("<FF>/coordinators"));
			Assert.That(Fdb.SystemKeys.KeyServers.ToString(), Is.EqualTo("<FF>/keyServers/"));
			Assert.That(Fdb.SystemKeys.MinValue.ToString(), Is.EqualTo("<FF><00>"));
			Assert.That(Fdb.SystemKeys.MaxValue.ToString(), Is.EqualTo("<FF><FF>"));
			Assert.That(Fdb.SystemKeys.ServerKeys.ToString(), Is.EqualTo("<FF>/serverKeys/"));
			Assert.That(Fdb.SystemKeys.ServerList.ToString(), Is.EqualTo("<FF>/serverList/"));
			Assert.That(Fdb.SystemKeys.Workers.ToString(), Is.EqualTo("<FF>/workers/"));
		}

		[Test]
		public void Test_FdbKey_Increment()
		{

			var key = FdbKey.Increment(Slice.FromAscii("Hello"));
			Assert.That(key.ToAscii(), Is.EqualTo("Hellp"));

			key = FdbKey.Increment(Slice.FromAscii("Hello\x00"));
			Assert.That(key.ToAscii(), Is.EqualTo("Hello\x01"));

			key = FdbKey.Increment(Slice.FromAscii("Hello\xFE"));
			Assert.That(key.ToAscii(), Is.EqualTo("Hello\xFF"));

			key = FdbKey.Increment(Slice.FromAscii("Hello\xFF"));
			Assert.That(key.ToAscii(), Is.EqualTo("Hellp"), "Should remove training \\xFF");

			key = FdbKey.Increment(Slice.FromAscii("A\xFF\xFF\xFF"));
			Assert.That(key.ToAscii(), Is.EqualTo("B"), "Should truncate all trailing \\xFFs");

			// corner cases
			Assert.That(() => FdbKey.Increment(Slice.Nil), Throws.InstanceOf<ArgumentException>().With.Property("ParamName").EqualTo("slice"));
			Assert.That(() => FdbKey.Increment(Slice.Empty), Throws.InstanceOf<OverflowException>());
			Assert.That(() => FdbKey.Increment(Slice.FromAscii("\xFF")), Throws.InstanceOf<OverflowException>());

		}

		[Test]
		public void Test_FdbKey_Merge()
		{
			// get a bunch of random slices
			var rnd = new Random();
			var slices = Enumerable.Range(0, 16).Select(x => Slice.Random(rnd, 4 + rnd.Next(32))).ToArray();

			var merged = FdbKey.Merge(Slice.FromByte(42), slices);
			Assert.That(merged, Is.Not.Null);
			Assert.That(merged.Length, Is.EqualTo(slices.Length));

			for (int i = 0; i < slices.Length; i++)
			{
				var expected = Slice.FromByte(42) + slices[i];
				Assert.That(merged[i], Is.EqualTo(expected));

				Assert.That(merged[i].Array, Is.SameAs(merged[0].Array), "All slices should be stored in the same buffer");
				if (i > 0) Assert.That(merged[i].Offset, Is.EqualTo(merged[i - 1].Offset + merged[i - 1].Count), "All slices should be contiguous");
			}

			// corner cases
			Assert.That(() => FdbKey.Merge(Slice.Empty, default(Slice[])), Throws.InstanceOf<ArgumentNullException>().With.Property("ParamName").EqualTo("keys"));
			Assert.That(() => FdbKey.Merge(Slice.Empty, default(IEnumerable<Slice>)), Throws.InstanceOf<ArgumentNullException>().With.Property("ParamName").EqualTo("keys"));
		}

		[Test]
		public void Test_FdbKey_Merge_Of_T()
		{
			string[] words = new string[] { "hello", "world", "très bien", "断トツ", "abc\0def", null, String.Empty };

			var merged = FdbKey.Merge(Slice.FromByte(42), words);
			Assert.That(merged, Is.Not.Null);
			Assert.That(merged.Length, Is.EqualTo(words.Length));

			for (int i = 0; i < words.Length; i++)
			{
				var expected = Slice.FromByte(42) + FdbTuple.Pack(words[i]);
				Assert.That(merged[i], Is.EqualTo(expected));

				Assert.That(merged[i].Array, Is.SameAs(merged[0].Array), "All slices should be stored in the same buffer");
				if (i > 0) Assert.That(merged[i].Offset, Is.EqualTo(merged[i - 1].Offset + merged[i - 1].Count), "All slices should be contiguous");
			}

			// corner cases
			Assert.That(() => FdbKey.Merge<int>(Slice.Empty, default(int[])), Throws.InstanceOf<ArgumentNullException>().With.Property("ParamName").EqualTo("keys"));
			Assert.That(() => FdbKey.Merge<int>(Slice.Empty, default(IEnumerable<int>)), Throws.InstanceOf<ArgumentNullException>().With.Property("ParamName").EqualTo("keys"));
		}

		[Test]
		public void Test_FdbKey_BatchedRange()
		{
			// we want numbers from 0 to 99 in 5 batches of 20 contiguous items each

			var query = FdbKey.BatchedRange(0, 100, 20);
			Assert.That(query, Is.Not.Null);

			var batches = query.ToArray();
			Assert.That(batches, Is.Not.Null);
			Assert.That(batches.Length, Is.EqualTo(5));
			Assert.That(batches, Is.All.Not.Null);

			// each batch should be an enumerable that will return 20 items each
			for (int i = 0; i < batches.Length; i++)
			{
				var items = batches[i].ToArray();
				Assert.That(items, Is.Not.Null.And.Length.EqualTo(20));
				for (int j = 0; j < items.Length; j++)
				{
					Assert.That(items[j], Is.EqualTo(j + i * 20));
				}
			}
		}

		[Test]
		public async Task Test_FdbKey_Batched()
		{
			// we want numbers from 0 to 999 split between 5 workers that will consume batches of 20 items at a time
			// > we get 5 enumerables that all take ranges from the same pool and all complete where there is no more values

			const int N = 1000;
			const int B = 20;
			const int W = 5;

			var query = FdbKey.Batched(0, N, W, B);
			Assert.That(query, Is.Not.Null);

			var batches = query.ToArray();
			Assert.That(batches, Is.Not.Null);
			Assert.That(batches.Length, Is.EqualTo(W));
			Assert.That(batches, Is.All.Not.Null);

			var used = new bool[N];

			var signal = new TaskCompletionSource<object>();

			// each batch should return new numbers
			var tasks = batches.Select(async (iterator, id) =>
			{
				// force async
				await signal.Task;

				foreach (var chunk in iterator)
				{
					// kvp = (offset, count)
					// > count should always be 20
					// > offset should always be a multiple of 20
					// > there should never be any overlap between workers
					Assert.That(chunk.Value, Is.EqualTo(B), "{0}:{1}", chunk.Key, chunk.Value);
					Assert.That(chunk.Key % B, Is.EqualTo(0), "{0}:{1}", chunk.Key, chunk.Value);

					lock (used)
					{
						for (int i = chunk.Key; i < chunk.Key + chunk.Value; i++)
						{

							if (used[i])
								Assert.Fail("Duplicate index {0} chunk {1}:{2} for worker {2}", i, chunk.Key, chunk.Value, id);
							else
								used[i] = true;
						}
					}

					await Task.Delay(1);
				}
			}).ToArray();

			var _ = Task.Run(() => signal.TrySetResult(null));

			await Task.WhenAll(tasks);

			Assert.That(used, Is.All.True);
		}
	
		[Test]
		public void Test_FdbKeyRange_Contains()
		{
			FdbKeyRange range;

			// ["", "")
			range = FdbKeyRange.Empty;
			Assert.That(range.Contains(Slice.Empty), Is.False);
			Assert.That(range.Contains(Slice.FromAscii("\x00")), Is.False);
			Assert.That(range.Contains(Slice.FromAscii("hello")), Is.False);
			Assert.That(range.Contains(Slice.FromAscii("\xFF")), Is.False);

			// ["", "\xFF" )
			range = new FdbKeyRange(Slice.Empty, Slice.FromAscii("\xFF"));
			Assert.That(range.Contains(Slice.Empty), Is.True);
			Assert.That(range.Contains(Slice.FromAscii("\x00")), Is.True);
			Assert.That(range.Contains(Slice.FromAscii("hello")), Is.True);
			Assert.That(range.Contains(Slice.FromAscii("\xFF")), Is.False);

			// ["\x00", "\xFF" )
			range = new FdbKeyRange(Slice.FromAscii("\x00"), Slice.FromAscii("\xFF"));
			Assert.That(range.Contains(Slice.Empty), Is.False);
			Assert.That(range.Contains(Slice.FromAscii("\x00")), Is.True);
			Assert.That(range.Contains(Slice.FromAscii("hello")), Is.True);
			Assert.That(range.Contains(Slice.FromAscii("\xFF")), Is.False);

			// corner cases
			Assert.That(new FdbKeyRange(Slice.FromAscii("A"), Slice.FromAscii("A")).Contains(Slice.FromAscii("A")), Is.False, "Equal bounds");
			Assert.That(new FdbKeyRange(Slice.FromAscii("Z"), Slice.FromAscii("A")).Contains(Slice.FromAscii("K")), Is.False, "Reversed bounds");
		}

		[Test]
		public void Test_FdbKeyRange_Test()
		{
			const int BEFORE = -1, INSIDE = 0, AFTER = +1;

			FdbKeyRange range;

			// range: [ "A", "Z" )
			range = new FdbKeyRange(Slice.FromAscii("A"), Slice.FromAscii("Z"));

			// Excluding the end: < "Z"
			Assert.That(range.Test(Slice.FromAscii("\x00"), endIncluded: false), Is.EqualTo(BEFORE));
			Assert.That(range.Test(Slice.FromAscii("@"), endIncluded: false), Is.EqualTo(BEFORE));
			Assert.That(range.Test(Slice.FromAscii("A"), endIncluded: false), Is.EqualTo(INSIDE));
			Assert.That(range.Test(Slice.FromAscii("Z"), endIncluded: false), Is.EqualTo(AFTER));
			Assert.That(range.Test(Slice.FromAscii("Z\x00"), endIncluded: false), Is.EqualTo(AFTER));
			Assert.That(range.Test(Slice.FromAscii("\xFF"), endIncluded: false), Is.EqualTo(AFTER));

			// Including the end: <= "Z"
			Assert.That(range.Test(Slice.FromAscii("\x00"), endIncluded: true), Is.EqualTo(BEFORE));
			Assert.That(range.Test(Slice.FromAscii("@"), endIncluded: true), Is.EqualTo(BEFORE));
			Assert.That(range.Test(Slice.FromAscii("A"), endIncluded: true), Is.EqualTo(INSIDE));
			Assert.That(range.Test(Slice.FromAscii("Z"), endIncluded: true), Is.EqualTo(INSIDE));
			Assert.That(range.Test(Slice.FromAscii("Z\x00"), endIncluded: true), Is.EqualTo(AFTER));
			Assert.That(range.Test(Slice.FromAscii("\xFF"), endIncluded: true), Is.EqualTo(AFTER));
		}

		[Test]
		public void Test_FdbKeyRange_StartsWith()
		{
			FdbKeyRange range;

			// "" => [ "", "\xFF\xFF" )
			range = FdbKeyRange.StartsWith(Slice.Empty);
			Assert.That(range.Begin, Is.EqualTo(Slice.Empty));
			Assert.That(range.End, Is.EqualTo(Slice.FromAscii("\xFF\xFF")));

			// "abc" => [ "abc", "abd" )
			range = FdbKeyRange.StartsWith(Slice.FromAscii("abc"));
			Assert.That(range.Begin, Is.EqualTo(Slice.FromAscii("abc")));
			Assert.That(range.End, Is.EqualTo(Slice.FromAscii("abd")));

			// "\xFF" => [ "\xFF", "\xFF\xFF" )
			range = FdbKeyRange.StartsWith(Slice.FromAscii("\xFF"));
			Assert.That(range.Begin, Is.EqualTo(Slice.FromAscii("\xFF")));
			Assert.That(range.End, Is.EqualTo(Slice.FromAscii("\xFF\xFF")));

			Assert.That(() => FdbKeyRange.StartsWith(Slice.Nil), Throws.InstanceOf<ArgumentException>());
		}

		[Test]
		public void Test_FdbKeyRange_PrefixedBy()
		{
			FdbKeyRange range;

			// "" => [ "\x00", "\xFF\xFF" )
			range = FdbKeyRange.PrefixedBy(Slice.Empty);
			Assert.That(range.Begin, Is.EqualTo(Slice.FromAscii("\x00")));
			Assert.That(range.End, Is.EqualTo(Slice.FromAscii("\xFF\xFF")));

			// "abc" => [ "abc\x00", "abd" )
			range = FdbKeyRange.PrefixedBy(Slice.FromAscii("abc"));
			Assert.That(range.Begin, Is.EqualTo(Slice.FromAscii("abc\x00")));
			Assert.That(range.End, Is.EqualTo(Slice.FromAscii("abd")));

			// "\xFF" => [ "\xFF\x00", "\xFF\xFF" )
			range = FdbKeyRange.PrefixedBy(Slice.FromAscii("\xFF"));
			Assert.That(range.Begin, Is.EqualTo(Slice.FromAscii("\xFF\x00")));
			Assert.That(range.End, Is.EqualTo(Slice.FromAscii("\xFF\xFF")));

			Assert.That(() => FdbKeyRange.PrefixedBy(Slice.Nil), Throws.InstanceOf<ArgumentException>());
		}

		[Test]
		public void Test_FdbKeyRange_FromKey()
		{
			FdbKeyRange range;

			// "" => [ "", "\x00" )
			range = FdbKeyRange.FromKey(Slice.Empty);
			Assert.That(range.Begin, Is.EqualTo(Slice.Empty));
			Assert.That(range.End, Is.EqualTo(Slice.FromAscii("\x00")));

			// "abc" => [ "abc", "abc\x00" )
			range = FdbKeyRange.FromKey(Slice.FromAscii("abc"));
			Assert.That(range.Begin, Is.EqualTo(Slice.FromAscii("abc")));
			Assert.That(range.End, Is.EqualTo(Slice.FromAscii("abc\x00")));

			// "\xFF" => [ "\xFF", "\xFF\x00" )
			range = FdbKeyRange.FromKey(Slice.FromAscii("\xFF"));
			Assert.That(range.Begin, Is.EqualTo(Slice.FromAscii("\xFF")));
			Assert.That(range.End, Is.EqualTo(Slice.FromAscii("\xFF\x00")));

			Assert.That(() => FdbKeyRange.FromKey(Slice.Nil), Throws.InstanceOf<ArgumentException>());
		}

	}
}
