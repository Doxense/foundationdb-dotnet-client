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
	using FoundationDB.Client;
	using FoundationDB.Layers.Tuples;
	using NUnit.Framework;
	using System;
	using System.Collections.Generic;
	using System.Linq;
	using System.Text;
	using System.Threading;
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

			Assert.That(Fdb.System.Coordinators.ToString(), Is.EqualTo("<FF>/coordinators"));
			Assert.That(Fdb.System.KeyServers.ToString(), Is.EqualTo("<FF>/keyServers/"));
			Assert.That(Fdb.System.MinValue.ToString(), Is.EqualTo("<FF><00>"));
			Assert.That(Fdb.System.MaxValue.ToString(), Is.EqualTo("<FF><FF>"));
			Assert.That(Fdb.System.ServerKeys.ToString(), Is.EqualTo("<FF>/serverKeys/"));
			Assert.That(Fdb.System.ServerList.ToString(), Is.EqualTo("<FF>/serverList/"));
			Assert.That(Fdb.System.BackupDataFormat.ToString(), Is.EqualTo("<FF>/backupDataFormat"));
			Assert.That(Fdb.System.InitId.ToString(), Is.EqualTo("<FF>/init_id"));
			Assert.That(Fdb.System.ConfigKey("hello").ToString(), Is.EqualTo("<FF>/conf/hello"));
			Assert.That(Fdb.System.GlobalsKey("world").ToString(), Is.EqualTo("<FF>/globals/world"));
			Assert.That(Fdb.System.WorkersKey("foo", "bar").ToString(), Is.EqualTo("<FF>/workers/foo/bar"));
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
			Assert.That(() => FdbKey.Increment(Slice.Empty), Throws.InstanceOf<ArgumentException>());
			Assert.That(() => FdbKey.Increment(Slice.FromAscii("\xFF")), Throws.InstanceOf<ArgumentException>());

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
				await signal.Task.ConfigureAwait(false);

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
								Assert.Fail("Duplicate index {0} chunk {1}:{2} for worker {3}", i, chunk.Key, chunk.Value, id);
							else
								used[i] = true;
						}
					}

					await Task.Delay(1).ConfigureAwait(false);
				}
			}).ToArray();

			ThreadPool.UnsafeQueueUserWorkItem((_) => signal.TrySetResult(null), null);

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
			range = FdbKeyRange.Create(Slice.Empty, Slice.FromAscii("\xFF"));
			Assert.That(range.Contains(Slice.Empty), Is.True);
			Assert.That(range.Contains(Slice.FromAscii("\x00")), Is.True);
			Assert.That(range.Contains(Slice.FromAscii("hello")), Is.True);
			Assert.That(range.Contains(Slice.FromAscii("\xFF")), Is.False);

			// ["\x00", "\xFF" )
			range = FdbKeyRange.Create(Slice.FromAscii("\x00"), Slice.FromAscii("\xFF"));
			Assert.That(range.Contains(Slice.Empty), Is.False);
			Assert.That(range.Contains(Slice.FromAscii("\x00")), Is.True);
			Assert.That(range.Contains(Slice.FromAscii("hello")), Is.True);
			Assert.That(range.Contains(Slice.FromAscii("\xFF")), Is.False);

			// corner cases
			Assert.That(FdbKeyRange.Create(Slice.FromAscii("A"), Slice.FromAscii("A")).Contains(Slice.FromAscii("A")), Is.False, "Equal bounds");
		}

		[Test]
		public void Test_FdbKeyRange_Test()
		{
			const int BEFORE = -1, INSIDE = 0, AFTER = +1;

			FdbKeyRange range;

			// range: [ "A", "Z" )
			range = FdbKeyRange.Create(Slice.FromAscii("A"), Slice.FromAscii("Z"));

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

			range = FdbKeyRange.Create(FdbTuple.Pack("A"), FdbTuple.Pack("Z"));
			Assert.That(range.Test(FdbTuple.Create("@")), Is.EqualTo((BEFORE)));
			Assert.That(range.Test(FdbTuple.Create("A")), Is.EqualTo((INSIDE)));
			Assert.That(range.Test(FdbTuple.Create("Z")), Is.EqualTo((AFTER)));
			Assert.That(range.Test(FdbTuple.Create("Z"), endIncluded: true), Is.EqualTo(INSIDE));
		}

		[Test]
		public void Test_FdbKeyRange_StartsWith()
		{
			FdbKeyRange range;

			// "abc" => [ "abc", "abd" )
			range = FdbKeyRange.StartsWith(Slice.FromAscii("abc"));
			Assert.That(range.Begin, Is.EqualTo(Slice.FromAscii("abc")));
			Assert.That(range.End, Is.EqualTo(Slice.FromAscii("abd")));

			// "" => ArgumentException
			Assert.That(() => FdbKeyRange.PrefixedBy(Slice.Empty), Throws.InstanceOf<ArgumentException>());

			// "\xFF" => ArgumentException
			Assert.That(() => FdbKeyRange.PrefixedBy(Slice.FromAscii("\xFF")), Throws.InstanceOf<ArgumentException>());

			// null => ArgumentException
			Assert.That(() => FdbKeyRange.PrefixedBy(Slice.Nil), Throws.InstanceOf<ArgumentException>());
		}

		[Test]
		public void Test_FdbKeyRange_PrefixedBy()
		{
			FdbKeyRange range;

			// "abc" => [ "abc\x00", "abd" )
			range = FdbKeyRange.PrefixedBy(Slice.FromAscii("abc"));
			Assert.That(range.Begin, Is.EqualTo(Slice.FromAscii("abc\x00")));
			Assert.That(range.End, Is.EqualTo(Slice.FromAscii("abd")));

			// "" => ArgumentException
			Assert.That(() => FdbKeyRange.PrefixedBy(Slice.Empty), Throws.InstanceOf<ArgumentException>());

			// "\xFF" => ArgumentException
			Assert.That(() => FdbKeyRange.PrefixedBy(Slice.FromAscii("\xFF")), Throws.InstanceOf<ArgumentException>());

			// null => ArgumentException
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

		[Test]
		public void Test_FdbKey_PrettyPrint()
		{
			// verify that the pretty printing of keys produce a user friendly output

			Assert.That(FdbKey.Dump(Slice.Nil), Is.EqualTo("<null>"));
			Assert.That(FdbKey.Dump(Slice.Empty), Is.EqualTo("<empty>"));

			Assert.That(FdbKey.Dump(Slice.FromByte(0)), Is.EqualTo("<00>"));
			Assert.That(FdbKey.Dump(Slice.FromByte(255)), Is.EqualTo("<FF>"));

			Assert.That(FdbKey.Dump(Slice.Create(new byte[] { 0, 1, 2, 3, 4, 5, 6, 7 })), Is.EqualTo("<00><01><02><03><04><05><06><07>"));
			Assert.That(FdbKey.Dump(Slice.Create(new byte[] { 255, 254, 253, 252, 251, 250, 249, 248 })), Is.EqualTo("<FF><FE><FD><FC><FB><FA><F9><F8>"));

			Assert.That(FdbKey.Dump(Slice.FromString("hello")), Is.EqualTo("hello"));
			Assert.That(FdbKey.Dump(Slice.FromString("héllø")), Is.EqualTo("h<C3><A9>ll<C3><B8>"));

			// tuples should be decoded properly

			Assert.That(FdbKey.Dump(FdbTuple.Pack(123)), Is.EqualTo("(123,)"));		
			Assert.That(FdbKey.Dump(FdbTuple.Pack(Slice.FromAscii("hello"))), Is.EqualTo("('hello',)"), "ASCII strings use single quotes");
			Assert.That(FdbKey.Dump(FdbTuple.Pack("héllø")), Is.EqualTo("(\"héllø\",)"), "Unicode strings use double quotes"); 
			Assert.That(FdbKey.Dump(FdbTuple.Pack(Slice.Create(new byte[] { 1, 2, 3 }))), Is.EqualTo("(<01 02 03>,)"));
			Assert.That(FdbKey.Dump(FdbTuple.Pack("hello", 123, -42)), Is.EqualTo("(\"hello\", 123, -42,)"));
			var guid = Guid.NewGuid();
			Assert.That(FdbKey.Dump(FdbTuple.Pack(guid)), Is.EqualTo("(" + guid.ToString() + ",)"), "GUIDs are displayed as a string literal");

			// ranges should be decoded when possible
			var key = FdbTuple.ToRange(FdbTuple.Create("hello"));
			// "<02>hello<00><00>" .. "<02>hello<00><FF>"
			Assert.That(FdbKey.PrettyPrint(key.Begin, FdbKey.PrettyPrintMode.Begin), Is.EqualTo("(\"hello\",).<00>"));
			Assert.That(FdbKey.PrettyPrint(key.End, FdbKey.PrettyPrintMode.End), Is.EqualTo("(\"hello\",).<FF>"));

			key = FdbKeyRange.StartsWith(FdbTuple.Pack("hello"));
			// "<02>hello<00>" .. "<02>hello<01>"
			Assert.That(FdbKey.PrettyPrint(key.Begin, FdbKey.PrettyPrintMode.Begin), Is.EqualTo("(\"hello\",)"));
			Assert.That(FdbKey.PrettyPrint(key.End, FdbKey.PrettyPrintMode.End), Is.EqualTo("(\"hello\",) + 1"));

			var t = FdbTuple.Pack(123);
			Assert.That(FdbKey.PrettyPrint(t, FdbKey.PrettyPrintMode.Single), Is.EqualTo("(123,)"));
			Assert.That(FdbKey.PrettyPrint(FdbTuple.ToRange(t).Begin, FdbKey.PrettyPrintMode.Begin), Is.EqualTo("(123,).<00>"));
			Assert.That(FdbKey.PrettyPrint(FdbTuple.ToRange(t).End, FdbKey.PrettyPrintMode.End), Is.EqualTo("(123,).<FF>"));

		}

		[Test]
		public void Test_FdbKeyRange_Intersects()
		{
			Func<byte, byte, FdbKeyRange> range = (x, y) => FdbKeyRange.Create(Slice.FromByte(x), Slice.FromByte(y));

			#region Not Intersecting...

			// [0, 1) [2, 3)
			// #X
			//   #X
			Assert.That(range(0, 1).Intersects(range(2, 3)), Is.False);
			// [2, 3) [0, 1)
			//   #X
			// #X
			Assert.That(range(2, 3).Intersects(range(0, 1)), Is.False);

			// [0, 1) [1, 2)
			// #X
			//  #X
			Assert.That(range(0, 1).Intersects(range(1, 2)), Is.False);
			// [1, 2) [0, 1)
			//  #X
			// #X
			Assert.That(range(1, 2).Intersects(range(0, 1)), Is.False);

			#endregion

			#region Intersecting...

			// [0, 2) [1, 3)
			// ##X
			//  ##X
			Assert.That(range(0, 2).Intersects(range(1, 3)), Is.True);
			// [1, 3) [0, 2)
			//  ##X
			// ##X
			Assert.That(range(1, 3).Intersects(range(0, 2)), Is.True);

			// [0, 1) [0, 2)
			// #X
			// ##X
			Assert.That(range(0, 1).Intersects(range(0, 2)), Is.True);
			// [0, 2) [0, 1)
			// ##X
			// #X
			Assert.That(range(0, 2).Intersects(range(0, 1)), Is.True);

			// [0, 2) [1, 2)
			// ##X
			//  #X
			Assert.That(range(0, 2).Intersects(range(1, 2)), Is.True);
			// [1, 2) [0, 2)
			//  #X
			// ##X
			Assert.That(range(1, 2).Intersects(range(0, 2)), Is.True);

			#endregion

		}

		[Test]
		public void Test_FdbKeyRange_Disjoint()
		{
			Func<byte, byte, FdbKeyRange> range = (x, y) => FdbKeyRange.Create(Slice.FromByte(x), Slice.FromByte(y));

			#region Disjoint...

			// [0, 1) [2, 3)
			// #X
			//   #X
			Assert.That(range(0, 1).Disjoint(range(2, 3)), Is.True);
			// [2, 3) [0, 1)
			//   #X
			// #X
			Assert.That(range(2, 3).Disjoint(range(0, 1)), Is.True);

			#endregion

			#region Not Disjoint...

			// [0, 1) [1, 2)
			// #X
			//  #X
			Assert.That(range(0, 1).Disjoint(range(1, 2)), Is.False);
			// [1, 2) [0, 1)
			//  #X
			// #X
			Assert.That(range(1, 2).Disjoint(range(0, 1)), Is.False);

			// [0, 2) [1, 3)
			// ##X
			//  ##X
			Assert.That(range(0, 2).Disjoint(range(1, 3)), Is.False);
			// [1, 3) [0, 2)
			//  ##X
			// ##X
			Assert.That(range(1, 3).Disjoint(range(0, 2)), Is.False);

			// [0, 1) [0, 2)
			// #X
			// ##X
			Assert.That(range(0, 1).Disjoint(range(0, 2)), Is.False);
			// [0, 2) [0, 1)
			// ##X
			// #X
			Assert.That(range(0, 2).Disjoint(range(0, 1)), Is.False);

			// [0, 2) [1, 2)
			// ##X
			//  #X
			Assert.That(range(0, 2).Disjoint(range(1, 2)), Is.False);
			// [1, 2) [0, 2)
			//  #X
			// ##X
			Assert.That(range(1, 2).Disjoint(range(0, 2)), Is.False);

			#endregion

		}

	}
}
