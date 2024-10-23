#region Copyright (c) 2023-2024 SnowBank SAS, (c) 2005-2023 Doxense SAS
// All rights reserved.
// 
// Redistribution and use in source and binary forms, with or without
// modification, are permitted provided that the following conditions are met:
// 	* Redistributions of source code must retain the above copyright
// 	  notice, this list of conditions and the following disclaimer.
// 	* Redistributions in binary form must reproduce the above copyright
// 	  notice, this list of conditions and the following disclaimer in the
// 	  documentation and/or other materials provided with the distribution.
// 	* Neither the name of SnowBank nor the
// 	  names of its contributors may be used to endorse or promote products
// 	  derived from this software without specific prior written permission.
// 
// THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS "AS IS" AND
// ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED
// WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE
// DISCLAIMED. IN NO EVENT SHALL SNOWBANK SAS BE LIABLE FOR ANY
// DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES
// (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES;
// LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND
// ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT
// (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS
// SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
#endregion

// ReSharper disable PossibleMultipleEnumeration
// ReSharper disable JoinDeclarationAndInitializer
// ReSharper disable CanSimplifyStringEscapeSequence
// ReSharper disable StringLiteralTypo

namespace FoundationDB.Client.Tests
{
	using System.Globalization;
	using SnowBank.Testing;

	[TestFixture]
	public class KeyFacts : FdbTest
	{

		[Test]
		public void Test_FdbKey_Constants()
		{
			Assert.Multiple(() =>
			{
				Assert.That(FdbKey.MinValue.ToArray(), Is.EqualTo(new byte[] { 0 }));
				Assert.That(FdbKey.MaxValue.ToArray(), Is.EqualTo(new byte[] { 255 }));
				Assert.That(FdbKey.DirectoryPrefix.ToArray(), Is.EqualTo(new byte[] { 254 }));
				Assert.That(FdbKey.SystemPrefix.ToArray(), Is.EqualTo(new byte[] { 255 }));

				Assert.That(FdbKey.MinValueSpan.ToArray(), Is.EqualTo(new byte[] { 0 }));
				Assert.That(FdbKey.MaxValueSpan.ToArray(), Is.EqualTo(new byte[] { 255 }));
				Assert.That(FdbKey.DirectoryPrefixSpan.ToArray(), Is.EqualTo(new byte[] { 254 }));
				Assert.That(FdbKey.SystemPrefixSpan.ToArray(), Is.EqualTo(new byte[] { 255 }));
				Assert.That(FdbKey.SystemEndSpan.ToArray(), Is.EqualTo(new byte[] { 255, 255 }));
			});

			Assert.Multiple(() =>
			{
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
			});
		}

		[Test]
		public void Test_FdbKey_Increment()
		{
			Assert.That(FdbKey.Increment(Literal("Hello")).ToString(), Is.EqualTo("Hellp"));
			Assert.That(FdbKey.Increment(Literal("Hello\x00")).ToString(), Is.EqualTo("Hello<01>"));
			Assert.That(FdbKey.Increment(Literal("Hello\xFE")).ToString(), Is.EqualTo("Hello<FF>"));
			Assert.That(FdbKey.Increment(Literal("Hello\xFF")).ToString(), Is.EqualTo("Hellp"), "Should remove training \\xFF");
			Assert.That(FdbKey.Increment(Literal("A\xFF\xFF\xFF")).ToString(), Is.EqualTo("B"), "Should truncate all trailing \\xFFs");

			// corner cases
			Assert.That(() => FdbKey.Increment(Slice.Nil), Throws.InstanceOf<ArgumentException>().With.Property("ParamName").EqualTo("slice"));
			Assert.That(() => FdbKey.Increment(Slice.Empty), Throws.InstanceOf<ArgumentException>());
			Assert.That(() => FdbKey.Increment(Literal("\xFF")), Throws.InstanceOf<ArgumentException>());
		}

		[Test]
		public void Test_FdbKey_Merge()
		{
			// get a bunch of random slices
			var rnd = new Random();
			var slices = Enumerable.Range(0, 16).Select(_ => Slice.Random(rnd, 4 + rnd.Next(32))).ToArray();

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
			// ReSharper disable AssignNullToNotNullAttribute
			Assert.That(() => FdbKey.Merge(Slice.Empty, default(Slice[])!), Throws.ArgumentNullException.With.Property("ParamName").EqualTo("keys"));
			Assert.That(() => FdbKey.Merge(Slice.Empty, default(IEnumerable<Slice>)!), Throws.ArgumentNullException.With.Property("ParamName").EqualTo("keys"));
			// ReSharper restore AssignNullToNotNullAttribute
		}

		[Test]
		public void Test_FdbKey_BatchedRange()
		{
			// we want numbers from 0 to 99 in 5 batches of 20 contiguous items each

			IEnumerable<IEnumerable<int>> query = FdbKey.BatchedRange(0, 100, 20);
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

			IEnumerable<IEnumerable<KeyValuePair<int, int>>> query = FdbKey.Batched(0, N, W, B);
			Assert.That(query, Is.Not.Null);

			var batches = query.ToArray();
			Assert.That(batches, Is.Not.Null);
			Assert.That(batches.Length, Is.EqualTo(W));
			Assert.That(batches, Is.All.Not.Null);

			var used = new bool[N];

			var signal = new TaskCompletionSource<object?>();

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
					Assert.That(chunk.Value, Is.EqualTo(B), $"{chunk.Key}:{chunk.Value}");
					Assert.That(chunk.Key % B, Is.EqualTo(0), $"{chunk.Key}:{chunk.Value}");

					lock (used)
					{
						for (int i = chunk.Key; i < chunk.Key + chunk.Value; i++)
						{

							if (used[i])
								Assert.Fail($"Duplicate index {i} chunk {chunk.Key}:{chunk.Value} for worker {id}");
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
		public void Test_KeyRange_Contains()
		{
			KeyRange range;

			// ["", "")
			range = KeyRange.Empty;
			Log(range);
			Assert.That(range.Contains(Slice.Empty), Is.False);
			Assert.That(range.Contains(Literal("\x00")), Is.False);
			Assert.That(range.Contains(Literal("hello")), Is.False);
			Assert.That(range.Contains(Literal("\xFF")), Is.False);

			// ["", "\xFF" )
			range = KeyRange.Create(Slice.Empty, Literal("\xFF"));
			Log(range);
			Assert.That(range.Contains(Slice.Empty), Is.True);
			Assert.That(range.Contains(Literal("\x00")), Is.True);
			Assert.That(range.Contains(Literal("hello")), Is.True);
			Assert.That(range.Contains(Literal("\xFF")), Is.False);

			// ["\x00", "\xFF" )
			range = KeyRange.Create(Literal("\x00"), Literal("\xFF"));
			Log(range);
			Assert.That(range.Contains(Slice.Empty), Is.False);
			Assert.That(range.Contains(Literal("\x00")), Is.True);
			Assert.That(range.Contains(Literal("hello")), Is.True);
			Assert.That(range.Contains(Literal("\xFF")), Is.False);

			// corner cases
			Assert.That(KeyRange.Create(Literal("A"), Literal("A")).Contains(Literal("A")), Is.False, "Equal bounds");
		}

		[Test]
		public void Test_KeyRange_Test()
		{
			const int BEFORE = -1, INSIDE = 0, AFTER = +1;

			KeyRange range;

			// range: [ "A", "Z" )
			range = KeyRange.Create(Literal("A"), Literal("Z"));
			Log(range);

			// Excluding the end: < "Z"
			Assert.That(range.Test(Literal("\x00"), endIncluded: false), Is.EqualTo(BEFORE));
			Assert.That(range.Test(Literal("@"), endIncluded: false), Is.EqualTo(BEFORE));
			Assert.That(range.Test(Literal("A"), endIncluded: false), Is.EqualTo(INSIDE));
			Assert.That(range.Test(Literal("Z"), endIncluded: false), Is.EqualTo(AFTER));
			Assert.That(range.Test(Literal("Z\x00"), endIncluded: false), Is.EqualTo(AFTER));
			Assert.That(range.Test(Literal("\xFF"), endIncluded: false), Is.EqualTo(AFTER));

			// Including the end: <= "Z"
			Assert.That(range.Test(Literal("\x00"), endIncluded: true), Is.EqualTo(BEFORE));
			Assert.That(range.Test(Literal("@"), endIncluded: true), Is.EqualTo(BEFORE));
			Assert.That(range.Test(Literal("A"), endIncluded: true), Is.EqualTo(INSIDE));
			Assert.That(range.Test(Literal("Z"), endIncluded: true), Is.EqualTo(INSIDE));
			Assert.That(range.Test(Literal("Z\x00"), endIncluded: true), Is.EqualTo(AFTER));
			Assert.That(range.Test(Literal("\xFF"), endIncluded: true), Is.EqualTo(AFTER));

			range = KeyRange.Create(TuPack.EncodeKey("A"), TuPack.EncodeKey("Z"));
			Log(range);
			Assert.That(range.Test(TuPack.EncodeKey("@")), Is.EqualTo((BEFORE)));
			Assert.That(range.Test(TuPack.EncodeKey("A")), Is.EqualTo((INSIDE)));
			Assert.That(range.Test(TuPack.EncodeKey("Z")), Is.EqualTo((AFTER)));
			Assert.That(range.Test(TuPack.EncodeKey("Z"), endIncluded: true), Is.EqualTo(INSIDE));
		}

		[Test]
		public void Test_KeyRange_StartsWith()
		{
			KeyRange range;

			// "abc" => [ "abc", "abd" )
			range = KeyRange.StartsWith(Literal("abc"));
			Log(range);
			Assert.That(range.Begin, Is.EqualTo(Literal("abc")));
			Assert.That(range.End, Is.EqualTo(Literal("abd")));

			// "" => ArgumentException
			Assert.That(() => KeyRange.PrefixedBy(Slice.Empty), Throws.InstanceOf<ArgumentException>());

			// "\xFF" => ArgumentException
			Assert.That(() => KeyRange.PrefixedBy(Literal("\xFF")), Throws.InstanceOf<ArgumentException>());

			// null => ArgumentException
			Assert.That(() => KeyRange.PrefixedBy(Slice.Nil), Throws.InstanceOf<ArgumentException>());
		}

		[Test]
		public void Test_KeyRange_PrefixedBy()
		{
			KeyRange range;

			// "abc" => [ "abc\x00", "abd" )
			range = KeyRange.PrefixedBy(Literal("abc"));
			Log(range);
			Assert.That(range.Begin, Is.EqualTo(Literal("abc\x00")));
			Assert.That(range.End, Is.EqualTo(Literal("abd")));

			// "" => ArgumentException
			Assert.That(() => KeyRange.PrefixedBy(Slice.Empty), Throws.InstanceOf<ArgumentException>());

			// "\xFF" => ArgumentException
			Assert.That(() => KeyRange.PrefixedBy(Literal("\xFF")), Throws.InstanceOf<ArgumentException>());

			// null => ArgumentException
			Assert.That(() => KeyRange.PrefixedBy(Slice.Nil), Throws.InstanceOf<ArgumentException>());
		}

		[Test]
		public void Test_KeyRange_FromKey()
		{
			KeyRange range;

			// "" => [ "", "\x00" )
			range = KeyRange.FromKey(Slice.Empty);
			Log(range);
			Assert.That(range.Begin, Is.EqualTo(Slice.Empty));
			Assert.That(range.End, Is.EqualTo(Literal("\x00")));

			// "abc" => [ "abc", "abc\x00" )
			range = KeyRange.FromKey(Literal("abc"));
			Log(range);
			Assert.That(range.Begin, Is.EqualTo(Literal("abc")));
			Assert.That(range.End, Is.EqualTo(Literal("abc\x00")));

			// "\xFF" => [ "\xFF", "\xFF\x00" )
			range = KeyRange.FromKey(Literal("\xFF"));
			Log(range);
			Assert.That(range.Begin, Is.EqualTo(Literal("\xFF")));
			Assert.That(range.End, Is.EqualTo(Literal("\xFF\x00")));

			Assert.That(() => KeyRange.FromKey(Slice.Nil), Throws.InstanceOf<ArgumentException>());
		}

		[Test]
		public void Test_FdbKey_PrettyPrint()
		{
			// verify that the pretty printing of keys produce a user-friendly output
			Assert.Multiple(() =>
			{

				Assert.That(FdbKey.Dump(Slice.Nil), Is.EqualTo("<null>"));
				Assert.That(FdbKey.Dump(Slice.Empty), Is.EqualTo("<empty>"));

				Assert.That(FdbKey.Dump(Slice.FromByte(0)), Is.EqualTo("<00>"));
				Assert.That(FdbKey.Dump(Slice.FromByte(255)), Is.EqualTo("<FF>"));

				Assert.That(FdbKey.Dump(new byte[] { 0, 1, 2, 3, 4, 5, 6, 7 }.AsSlice()), Is.EqualTo("<00><01><02><03><04><05><06><07>"));
				Assert.That(FdbKey.Dump(new byte[] { 255, 254, 253, 252, 251, 250, 249, 248 }.AsSlice()), Is.EqualTo("(|System|<FE><FD><FC><FB><FA><F9><F8>,)"));

				Assert.That(FdbKey.Dump(Text("hello")), Is.EqualTo("hello"));
				Assert.That(FdbKey.Dump(Text("héllø")), Is.EqualTo("h<C3><A9>ll<C3><B8>"));

				Assert.That(FdbKey.Dump(Text("hello"u8)), Is.EqualTo("hello"));
				Assert.That(FdbKey.Dump(Text("héllø"u8)), Is.EqualTo("h<C3><A9>ll<C3><B8>"));

				Assert.That(FdbKey.Dump("hello"u8), Is.EqualTo("hello"));
				Assert.That(FdbKey.Dump("héllø"u8), Is.EqualTo("h<C3><A9>ll<C3><B8>"));

				// tuples should be decoded properly

				Assert.That(FdbKey.Dump(TuPack.EncodeKey(123)), Is.EqualTo("(123,)"), "Singleton tuples should end with a ','");
				Assert.That(FdbKey.Dump(TuPack.EncodeKey(Literal("hello"))), Is.EqualTo("(`hello`,)"), "ASCII strings should use single back quotes");
				Assert.That(FdbKey.Dump(TuPack.EncodeKey("héllø")), Is.EqualTo("(\"héllø\",)"), "Unicode strings should use double quotes");
				Assert.That(FdbKey.Dump(TuPack.EncodeKey(Text("héllø"u8))), Is.EqualTo("(`h<C3><A9>ll<C3><B8>`,)"), "Unicode strings should use double quotes");
				Assert.That(FdbKey.Dump(TuPack.EncodeKey(new byte[] { 1, 2, 3 }.AsSlice())), Is.EqualTo("(`<01><02><03>`,)"));
				Assert.That(FdbKey.Dump(TuPack.EncodeKey(123, 456)), Is.EqualTo("(123, 456)"), "Elements should be separated with a space, and not end up with ','");
				Assert.That(FdbKey.Dump(TuPack.EncodeKey(default(object), true, false)), Is.EqualTo("(null, true, false)"), "Booleans should be displayed as numbers, and null should be in lowercase"); //note: even though it's tempting to using Python's "Nil", it's not very ".NETty"

				//note: the string representation of double is not identical between NetFx and .NET Core! So we cannot use a constant literal here
				Assert.That(FdbKey.Dump(TuPack.EncodeKey(1.0d, Math.PI, Math.E)), Is.EqualTo("(1, " + Math.PI.ToString("R", CultureInfo.InvariantCulture) + ", " + Math.E.ToString("R", CultureInfo.InvariantCulture) + ")"), "Doubles should used dot and have full precision (17 digits)");
				Assert.That(FdbKey.Dump(TuPack.EncodeKey(1.0f, (float)Math.PI, (float)Math.E)), Is.EqualTo("(1, " + ((float) Math.PI).ToString("R", CultureInfo.InvariantCulture)+ ", " + ((float) Math.E).ToString("R", CultureInfo.InvariantCulture) + ")"), "Singles should used dot and have full precision (10 digits)");

				var guid = Guid.NewGuid();
				Assert.That(FdbKey.Dump(TuPack.EncodeKey(guid)), Is.EqualTo($"({guid:B},)"), "GUIDs should be displayed as a string literal, surrounded by {{...}}, and without quotes");
				var uuid128 = Uuid128.NewUuid();
				Assert.That(FdbKey.Dump(TuPack.EncodeKey(uuid128)), Is.EqualTo($"({uuid128:B},)"), "Uuid128s should be displayed as a string literal, surrounded by {{...}}, and without quotes");
				var uuid64 = Uuid64.NewUuid();
				Assert.That(FdbKey.Dump(TuPack.EncodeKey(uuid64)), Is.EqualTo($"({uuid64:B},)"), "Uuid64s should be displayed as a string literal, surrounded by {{...}}, and without quotes");

				// ranges should be decoded when possible
				var key = TuPack.ToRange(STuple.Create("hello"));
				// "<02>hello<00><00>" .. "<02>hello<00><FF>"
				Assert.That(FdbKey.PrettyPrint(key.Begin, FdbKey.PrettyPrintMode.Begin), Is.EqualTo("(\"hello\",).<00>"));
				Assert.That(FdbKey.PrettyPrint(key.End, FdbKey.PrettyPrintMode.End), Is.EqualTo("(\"hello\",).<FF>"));

				key = KeyRange.StartsWith(TuPack.EncodeKey("hello"));
				// "<02>hello<00>" .. "<02>hello<01>"
				Assert.That(FdbKey.PrettyPrint(key.Begin, FdbKey.PrettyPrintMode.Begin), Is.EqualTo("(\"hello\",)"));
				Assert.That(FdbKey.PrettyPrint(key.End, FdbKey.PrettyPrintMode.End), Is.EqualTo("(\"hello\",) + 1"));

				var t = TuPack.EncodeKey(123);
				Assert.That(FdbKey.PrettyPrint(t, FdbKey.PrettyPrintMode.Single), Is.EqualTo("(123,)"));
				Assert.That(FdbKey.PrettyPrint(TuPack.ToRange(t).Begin, FdbKey.PrettyPrintMode.Begin), Is.EqualTo("(123,).<00>"));
				Assert.That(FdbKey.PrettyPrint(TuPack.ToRange(t).End, FdbKey.PrettyPrintMode.End), Is.EqualTo("(123,).<FF>"));
			});
		}

		[Test]
		public void Test_KeyRange_Intersects()
		{
			static KeyRange MakeRange(byte x, byte y) => KeyRange.Create(Slice.FromByte(x), Slice.FromByte(y));

			Assert.Multiple(() =>
			{
				#region Not Intersecting...

				// [0, 1) [2, 3)
				// #X
				//   #X
				Assert.That(MakeRange(0, 1).Intersects(MakeRange(2, 3)), Is.False);
				// [2, 3) [0, 1)
				//   #X
				// #X
				Assert.That(MakeRange(2, 3).Intersects(MakeRange(0, 1)), Is.False);

				// [0, 1) [1, 2)
				// #X
				//  #X
				Assert.That(MakeRange(0, 1).Intersects(MakeRange(1, 2)), Is.False);
				// [1, 2) [0, 1)
				//  #X
				// #X
				Assert.That(MakeRange(1, 2).Intersects(MakeRange(0, 1)), Is.False);

				#endregion

				#region Intersecting...

				// [0, 2) [1, 3)
				// ##X
				//  ##X
				Assert.That(MakeRange(0, 2).Intersects(MakeRange(1, 3)), Is.True);
				// [1, 3) [0, 2)
				//  ##X
				// ##X
				Assert.That(MakeRange(1, 3).Intersects(MakeRange(0, 2)), Is.True);

				// [0, 1) [0, 2)
				// #X
				// ##X
				Assert.That(MakeRange(0, 1).Intersects(MakeRange(0, 2)), Is.True);
				// [0, 2) [0, 1)
				// ##X
				// #X
				Assert.That(MakeRange(0, 2).Intersects(MakeRange(0, 1)), Is.True);

				// [0, 2) [1, 2)
				// ##X
				//  #X
				Assert.That(MakeRange(0, 2).Intersects(MakeRange(1, 2)), Is.True);
				// [1, 2) [0, 2)
				//  #X
				// ##X
				Assert.That(MakeRange(1, 2).Intersects(MakeRange(0, 2)), Is.True);

				#endregion
			});

		}

		[Test]
		public void Test_KeyRange_Disjoint()
		{
			static KeyRange MakeRange(byte x, byte y) => KeyRange.Create(Slice.FromByte(x), Slice.FromByte(y));

			Assert.Multiple(() =>
			{
				#region Disjoint...

				// [0, 1) [2, 3)
				// #X
				//   #X
				Assert.That(MakeRange(0, 1).Disjoint(MakeRange(2, 3)), Is.True);
				// [2, 3) [0, 1)
				//   #X
				// #X
				Assert.That(MakeRange(2, 3).Disjoint(MakeRange(0, 1)), Is.True);

				#endregion

				#region Not Disjoint...

				// [0, 1) [1, 2)
				// #X
				//  #X
				Assert.That(MakeRange(0, 1).Disjoint(MakeRange(1, 2)), Is.False);
				// [1, 2) [0, 1)
				//  #X
				// #X
				Assert.That(MakeRange(1, 2).Disjoint(MakeRange(0, 1)), Is.False);

				// [0, 2) [1, 3)
				// ##X
				//  ##X
				Assert.That(MakeRange(0, 2).Disjoint(MakeRange(1, 3)), Is.False);
				// [1, 3) [0, 2)
				//  ##X
				// ##X
				Assert.That(MakeRange(1, 3).Disjoint(MakeRange(0, 2)), Is.False);

				// [0, 1) [0, 2)
				// #X
				// ##X
				Assert.That(MakeRange(0, 1).Disjoint(MakeRange(0, 2)), Is.False);
				// [0, 2) [0, 1)
				// ##X
				// #X
				Assert.That(MakeRange(0, 2).Disjoint(MakeRange(0, 1)), Is.False);

				// [0, 2) [1, 2)
				// ##X
				//  #X
				Assert.That(MakeRange(0, 2).Disjoint(MakeRange(1, 2)), Is.False);
				// [1, 2) [0, 2)
				//  #X
				// ##X
				Assert.That(MakeRange(1, 2).Disjoint(MakeRange(0, 2)), Is.False);

				#endregion
			});
		}

		[Test]
		public void Test_KeySelector_Create()
		{
			{ // fGE{...}
				var sel = KeySelector.FirstGreaterOrEqual(Key("Hello"));
				Log(sel);
				Assert.That(sel.Key, Is.EqualTo(Key("Hello")));
				Assert.That(sel.Offset, Is.EqualTo(1));
				Assert.That(sel.OrEqual, Is.False);
				Assert.That(sel.ToString(), Does.StartWith("fGE{").And.EndsWith("}"));
				var (key, orEqual, offset) = sel;
				Assert.That(key, Is.EqualTo(Key("Hello")));
				Assert.That(offset, Is.EqualTo(1));
				Assert.That(orEqual, Is.False);

			}
			{ // fGT{...}
				var sel = KeySelector.FirstGreaterThan(Key("Hello"));
				Log(sel);
				Assert.That(sel.Key, Is.EqualTo(Key("Hello")));
				Assert.That(sel.Offset, Is.EqualTo(1));
				Assert.That(sel.OrEqual, Is.True);
				Assert.That(sel.ToString(), Does.StartWith("fGT{").And.EndsWith("}"));
				var (key, orEqual, offset) = sel;
				Assert.That(key, Is.EqualTo(Key("Hello")));
				Assert.That(offset, Is.EqualTo(1));
				Assert.That(orEqual, Is.True);
			}
			{ // lLE{...}
				var sel = KeySelector.LastLessOrEqual(Key("Hello"));
				Log(sel);
				Assert.That(sel.Key, Is.EqualTo(Key("Hello")));
				Assert.That(sel.Offset, Is.EqualTo(0));
				Assert.That(sel.OrEqual, Is.True);
				Assert.That(sel.ToString(), Does.StartWith("lLE{").And.EndsWith("}"));
				var (key, orEqual, offset) = sel;
				Assert.That(key, Is.EqualTo(Key("Hello")));
				Assert.That(offset, Is.EqualTo(0));
				Assert.That(orEqual, Is.True);
			}
			{ // lLT{...}
				var sel = KeySelector.LastLessThan(Key("Hello"));
				Log(sel);
				Assert.That(sel.Key, Is.EqualTo(Key("Hello")));
				Assert.That(sel.Offset, Is.EqualTo(0));
				Assert.That(sel.OrEqual, Is.False);
				Assert.That(sel.ToString(), Does.StartWith("lLT{").And.EndsWith("}"));
				var (key, orEqual, offset) = sel;
				Assert.That(key, Is.EqualTo(Key("Hello")));
				Assert.That(offset, Is.EqualTo(0));
				Assert.That(orEqual, Is.False);
			}
			{ // custom offset
				var sel = new KeySelector(Key("Hello"), true, 123);
				Log(sel);
				Assert.That(sel.Key, Is.EqualTo(Key("Hello")));
				Assert.That(sel.Offset, Is.EqualTo(123));
				Assert.That(sel.OrEqual, Is.True);
				Assert.That(sel.ToString(), Does.StartWith("fGT{").And.EndsWith("} + 122"));
				var (key, orEqual, offset) = sel;
				Assert.That(key, Is.EqualTo(Key("Hello")));
				Assert.That(offset, Is.EqualTo(123));
				Assert.That(orEqual, Is.True);
			}
		}

		[Test]
		public void Test_KeySelector_Add_Offsets()
		{
			{ // KeySelector++
				var sel = KeySelector.FirstGreaterOrEqual(Key("Hello"));
				sel++;
				Log(sel);
				Assert.That(sel.Key, Is.EqualTo(Key("Hello")));
				Assert.That(sel.Offset, Is.EqualTo(2));
				Assert.That(sel.OrEqual, Is.False);
				Assert.That(sel.ToString(), Does.StartWith("fGE{").And.EndsWith("} + 1"));
			}
			{ // KeySelector--
				var sel = KeySelector.FirstGreaterOrEqual(Key("Hello"));
				sel--;
				Log(sel);
				Assert.That(sel.Key, Is.EqualTo(Key("Hello")));
				Assert.That(sel.Offset, Is.EqualTo(0));
				Assert.That(sel.OrEqual, Is.False);
				Assert.That(sel.ToString(), Does.StartWith("lLT{").And.EndsWith("}"));
			}
			{ // fGE{...} + offset
				var sel = KeySelector.FirstGreaterOrEqual(Key("Hello"));
				var next = sel + 123;
				Log(next);
				Assert.That(next.Key, Is.EqualTo(Key("Hello")));
				Assert.That(next.Offset, Is.EqualTo(124));
				Assert.That(next.OrEqual, Is.False);
				Assert.That(next.ToString(), Does.StartWith("fGE{").And.EndsWith("} + 123"));
			}
			{ // fGT{...} + offset
				var sel = KeySelector.FirstGreaterThan(Key("Hello"));
				var next = sel + 123;
				Log(next);
				Assert.That(next.Key, Is.EqualTo(Key("Hello")));
				Assert.That(next.Offset, Is.EqualTo(124));
				Assert.That(next.OrEqual, Is.True);
				Assert.That(next.ToString(), Does.StartWith("fGT{").And.EndsWith("} + 123"));
			}
			{ // fGE{...} - offset
				var sel = KeySelector.FirstGreaterOrEqual(Key("Hello"));
				var next = sel - 123;
				Log(next);
				Assert.That(next.Key, Is.EqualTo(Key("Hello")));
				Assert.That(next.Offset, Is.EqualTo(-122));
				Assert.That(next.OrEqual, Is.False);
				Assert.That(next.ToString(), Does.StartWith("lLT{").And.EndsWith("} - 122"));
			}
			{ // fGT{...} - offset
				var sel = KeySelector.FirstGreaterThan(Key("Hello"));
				var next = sel - 123;
				Log(next);
				Assert.That(next.Key, Is.EqualTo(Key("Hello")));
				Assert.That(next.Offset, Is.EqualTo(-122));
				Assert.That(next.OrEqual, Is.True);
				Assert.That(next.ToString(), Does.StartWith("lLE{").And.EndsWith("} - 122"));
			}

		}

		[Test]
		public void Test_KeySelectorPair_Create()
		{
			{
				// Create(KeySelector, KeySelector)
				var begin = KeySelector.LastLessThan(Key("Hello"));
				var end = KeySelector.FirstGreaterThan(Key("World"));
				var pair = KeySelectorPair.Create(begin, end);
				Log(pair);
				// must not change the selectors
				Assert.That(pair.Begin, Is.EqualTo(begin));
				Assert.That(pair.End, Is.EqualTo(end));
			}
			{
				// Create(KeyRange)
				var pair = KeySelectorPair.Create(KeyRange.Create(Key("Hello"), Key("World")));
				Log(pair);
				// must apply FIRST_GREATER_OR_EQUAL on both bounds
				Assert.That(pair.Begin, Is.EqualTo(KeySelector.FirstGreaterOrEqual(Key("Hello"))));
				Assert.That(pair.End, Is.EqualTo(KeySelector.FirstGreaterOrEqual(Key("World"))));
			}
			{
				// Create(Slice, Slice)
				var pair = KeySelectorPair.Create(Key("Hello"), Key("World"));
				Log(pair);
				// must apply FIRST_GREATER_OR_EQUAL on both bounds
				Assert.That(pair.Begin, Is.EqualTo(KeySelector.FirstGreaterOrEqual(Key("Hello"))));
				Assert.That(pair.End, Is.EqualTo(KeySelector.FirstGreaterOrEqual(Key("World"))));
			}
		}

		[Test]
		public void Test_KeySelectorPair_Deconstruct()
		{
			var (begin, end) = KeySelectorPair.Create(Key("Hello"), Key("World"));
			Assert.That(begin, Is.EqualTo(KeySelector.FirstGreaterOrEqual(Key("Hello"))));
			Assert.That(end, Is.EqualTo(KeySelector.FirstGreaterOrEqual(Key("World"))));
		}

		[Test]
		public void Test_KeySelectorPair_StartsWith()
		{
			var prefix = Pack(("Hello", "World"));
			var pair = KeySelectorPair.StartsWith(prefix);
			Log(pair);
			Assert.That(pair.Begin, Is.EqualTo(KeySelector.FirstGreaterOrEqual(prefix)));
			Assert.That(pair.End, Is.EqualTo(KeySelector.FirstGreaterOrEqual(FdbKey.Increment(prefix))));
		}

		[Test]
		public void Test_KeySelectorPair_Tail()
		{
			var prefix = Key("Hello", "World");
			var cursor = Key(123);
			var key = Key("Hello", "World", 123);
			Assume.That(key, Is.EqualTo(prefix + cursor));

			{ // orEqual: true
				var pair = KeySelectorPair.Tail(prefix, cursor, orEqual: true);
				Log(pair);
				Assert.That(pair.Begin, Is.EqualTo(KeySelector.FirstGreaterOrEqual(key)));
				Assert.That(pair.End, Is.EqualTo(KeySelector.FirstGreaterOrEqual(FdbKey.Increment(prefix))));
			}
			{ // orEqual: false
				var pair = KeySelectorPair.Tail(prefix, cursor, orEqual: false);
				Log(pair);
				Assert.That(pair.Begin, Is.EqualTo(KeySelector.FirstGreaterThan(key)));
				Assert.That(pair.End, Is.EqualTo(KeySelector.FirstGreaterOrEqual(FdbKey.Increment(prefix))));
			}
		}

		[Test]
		public void Test_KeySelectorPair_Head()
		{
			var prefix = Key("Hello", "World");
			var cursor = Key(123);
			var key = Key("Hello", "World", 123);
			Assume.That(key, Is.EqualTo(prefix + cursor));

			{ // orEqual: true
				var pair = KeySelectorPair.Head(prefix, cursor, orEqual: true);
				Log(pair);
				Assert.That(pair.Begin, Is.EqualTo(KeySelector.FirstGreaterThan(prefix)));
				Assert.That(pair.End, Is.EqualTo(KeySelector.FirstGreaterThan(key)));
			}
			{ // orEqual: false
				var pair = KeySelectorPair.Head(prefix, cursor, orEqual: false);
				Log(pair);
				Assert.That(pair.Begin, Is.EqualTo(KeySelector.FirstGreaterThan(prefix)));
				Assert.That(pair.End, Is.EqualTo(KeySelector.FirstGreaterOrEqual(key)));
			}
		}

	}
}
