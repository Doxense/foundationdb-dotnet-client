#region Copyright (c) 2023-2025 SnowBank SAS, (c) 2005-2023 Doxense SAS
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

// ReSharper disable VariableLengthStringHexEscapeSequence
namespace FoundationDB.Client.Tests
{
	using SnowBank.Data.Tuples.Binary;

	[TestFixture]
	[Category("Fdb-Client-InProc")]
	[Parallelizable(ParallelScope.All)]
	public class FdbKeyFacts : FdbSimpleTest
	{

		[Test]
		public void Test_FdbRawKey_Basics()
		{
			FdbRawKey hello = Slice.FromBytes("hello"u8);
			FdbRawKey world = Slice.FromBytes("world"u8);

			{ // Empty
				var k = FdbKey.FromBytes(Slice.Empty);
				Assert.That(k.Data, Is.EqualTo(Slice.Empty));
				Assert.That(((IFdbKey) k).GetSubspace(), Is.Null);

				Assert.That(k.ToSlice(), Is.EqualTo(Slice.Empty));

				Assert.That(k, Is.EqualTo(new FdbRawKey(Slice.Empty)));
				Assert.That(k, Is.EqualTo(Slice.Empty));

				Assert.That(k, Is.Not.EqualTo(Slice.Nil));
				Assert.That(k, Is.Not.EqualTo(hello));

				Assert.That(k, Is.EqualTo((object) new FdbRawKey(Slice.Empty)));
				Assert.That(k, Is.EqualTo((object) Slice.Empty));

				Assert.That(k.CompareTo(Slice.Empty), Is.Zero);
			}
			{ // Not Empty
				var k = FdbKey.FromBytes("hello"u8);
				Assert.That(k.Data, Is.EqualTo(hello));
				Assert.That(((IFdbKey) k).GetSubspace(), Is.Null);

				Assert.That(k.ToSlice(), Is.EqualTo(hello));
				Assert.That(k.ToSlice().Array, Is.SameAs(k.Data.Array), "Should expose the wrapped slice");

				Assert.That(k, Is.EqualTo(hello));
				Assert.That(k, Is.Not.EqualTo(Slice.Nil));
				Assert.That(k, Is.Not.EqualTo(Slice.Empty));
				Assert.That(k, Is.Not.EqualTo(world));

				Assert.That(k.CompareTo(hello), Is.Zero);
				Assert.That(k.CompareTo(Slice.Empty), Is.GreaterThan(0));
				Assert.That(k.CompareTo(world), Is.LessThan(0));
			}
		}

		[Test]
		public void Test_FdbRawKey_Encoding()
		{
			static void Verify(Slice value)
			{
				{ // Slice
					var rawKey = value;

					var key = FdbKey.FromBytes(rawKey);
					Assert.That(key.ToSlice(), Is.EqualTo(rawKey));
					Assert.That(key.TryGetSpan(out var span), Is.True.WithOutput(span.ToSlice()).EqualTo(rawKey));
					Assert.That(key.TryGetSizeHint(out int size), Is.True.WithOutput(size).EqualTo(rawKey.Count));

					Assert.That(((IFdbKey) key).GetSubspace(), Is.Null);
				}
				{ // byte[]
					var rawKey = value.ToArray();

					var key = FdbKey.FromBytes(rawKey);
					Assert.That(key.ToSlice().ToArray(), Is.EqualTo(rawKey));
					Assert.That(key.TryGetSpan(out var span), Is.True.WithOutput(span.ToArray()).EqualTo(rawKey));
					Assert.That(key.TryGetSizeHint(out int size), Is.True.WithOutput(size).EqualTo(rawKey.Length));
					Assert.That(((IFdbKey) key).GetSubspace(), Is.Null);
				}
			}

			Verify(Slice.FromBytes("Hello, World!"u8));
		}

		[Test]
		public void Test_FdbTupleKey_Basics()
		{
			var subspace = GetSubspace(FdbPath.Absolute("Foo", "Bar"), STuple.Create(42));
			var other = GetSubspace(STuple.Create(37));
			var child = GetSubspace(STuple.Create(42, "hello"));

			var g = Guid.NewGuid();
			var now = DateTime.Now;
			var vs = VersionStamp.FromUuid80(Uuid80.NewUuid());

			var k1 = subspace.Key("hello");
			var k2 = subspace.Key("hello", 123);
			var k3 = subspace.Key("hello", 123, "world");
			var k4 = subspace.Key("hello", 123, "world", true);
			var k5 = subspace.Key("hello", 123, "world", true, Math.PI);
			var k6 = subspace.Key("hello", 123, "world", true, Math.PI, g);
			var k7 = subspace.Key("hello", 123, "world", true, Math.PI, g, now);
			var k8 = subspace.Key("hello", 123, "world", true, Math.PI, g, now, vs);

			var kv1 = subspace.Tuple((IVarTuple) STuple.Create("hello"));
			var kv2 = subspace.Tuple((IVarTuple) STuple.Create("hello", 123));
			var kv3 = subspace.Tuple((IVarTuple) STuple.Create("hello", 123, "world"));
			var kv4 = subspace.Tuple((IVarTuple) STuple.Create("hello", 123, "world", true));
			var kv5 = subspace.Tuple((IVarTuple) STuple.Create("hello", 123, "world", true, Math.PI));
			var kv6 = subspace.Tuple((IVarTuple) STuple.Create("hello", 123, "world", true, Math.PI, g));
			var kv7 = subspace.Tuple((IVarTuple) STuple.Create("hello", 123, "world", true, Math.PI, g, now));
			var kv8 = subspace.Tuple((IVarTuple) STuple.Create("hello", 123, "world", true, Math.PI, g, now, vs));

			Assert.Multiple(() =>
			{ // T1
				Log($"# {k1}");
				Assert.That(k1.Subspace, Is.SameAs(subspace));
				Assert.That(k1.Item1, Is.EqualTo("hello"));
				Assert.That(STuple.Create(k1.Item1), Is.EqualTo(kv1.Items));

				Assert.That(k1.ToString(), Is.EqualTo("/Foo/Bar(\"hello\",)"));

				Assert.That(k1.ToSlice(), Is.EqualTo(TuPack.EncodeKey(42, "hello")));
				Assert.That(kv1.ToSlice(), Is.EqualTo(TuPack.EncodeKey(42, "hello")));

				Assert.That(k1.Equals(k1), Is.True);
				Assert.That(k1.Equals(kv1), Is.True);
				Assert.That(k1.Equals(subspace.Key("hello")), Is.True);
				Assert.That(k1.Equals(child.Key()), Is.True);
				Assert.That(k1.Equals(TuPack.EncodeKey(42, "hello")), Is.True);

				Assert.That(k1.Equals(k2), Is.False);
				Assert.That(k1.Equals(kv2), Is.False);
				Assert.That(k1.Equals(other.Key("hello")), Is.False);
				Assert.That(k1.Equals(subspace.Key("world")), Is.False);
			});
			Assert.Multiple(() =>
			{ // T1, T2
				Log($"# {k2}");
				Assert.That(k2.Subspace, Is.SameAs(subspace));
				Assert.That(k2.Items.Item1, Is.EqualTo("hello"));
				Assert.That(k2.Items.Item2, Is.EqualTo(123));
				Assert.That(k2.Items, Is.EqualTo(kv2.Items));

				Assert.That(k2.ToSlice(), Is.EqualTo(TuPack.EncodeKey(42, "hello", 123)));
				Assert.That(kv2.ToSlice(), Is.EqualTo(TuPack.EncodeKey(42, "hello", 123)));

				Assert.That(k2.Equals(k2), Is.True);
				Assert.That(k2.Equals(kv2), Is.True);
				Assert.That(k2.Equals(subspace.Key("hello", 123)), Is.True);
				Assert.That(k2.Equals(child.Key(123)), Is.True);
				Assert.That(k2.Equals(TuPack.EncodeKey(42, "hello", 123)), Is.True);
				Assert.That(k2.Equals(subspace.Bytes(TuPack.EncodeKey("hello", 123))), Is.True);
				Assert.That(k2.Equals(subspace.Key("hello").Bytes(TuPack.EncodeKey(123))), Is.True);

				Assert.That(k2.Equals(other.Key("hello", 123)), Is.False);
				Assert.That(k2.Equals(subspace.Key("world", 123)), Is.False);
				Assert.That(k2.Equals(subspace.Key("hello", 456)), Is.False);
			});
			Assert.Multiple(() =>
			{ // T1, T2, T3
				Log($"# {k3}");
				Assert.That(k3.Subspace, Is.SameAs(subspace));
				Assert.That(k3.Items.Item1, Is.EqualTo("hello"));
				Assert.That(k3.Items.Item2, Is.EqualTo(123));
				Assert.That(k3.Items.Item3, Is.EqualTo("world"));
				Assert.That(k3.Items, Is.EqualTo(kv3.Items));

				Assert.That(k3.ToSlice(), Is.EqualTo(TuPack.EncodeKey(42, "hello", 123, "world")));
				Assert.That(kv3.ToSlice(), Is.EqualTo(TuPack.EncodeKey(42, "hello", 123, "world")));

				Assert.That(k3.Equals(k3), Is.True);
				Assert.That(k3.Equals(kv3), Is.True);
				Assert.That(k3.Equals(subspace.Key("hello", 123, "world")), Is.True);

			});
			Assert.Multiple(() =>
			{ // T1, T2, T3, T4
				Log($"# {k4}");
				Assert.That(k4.Subspace, Is.SameAs(subspace));
				Assert.That(k4.Items.Item1, Is.EqualTo("hello"));
				Assert.That(k4.Items.Item2, Is.EqualTo(123));
				Assert.That(k4.Items.Item3, Is.EqualTo("world"));
				Assert.That(k4.Items.Item4, Is.True);
				Assert.That(k4.Items, Is.EqualTo(kv4.Items));

				Assert.That(k4.ToSlice(), Is.EqualTo(TuPack.EncodeKey(42, "hello", 123, "world", true)));
				Assert.That(kv4.ToSlice(), Is.EqualTo(TuPack.EncodeKey(42, "hello", 123, "world", true)));

				Assert.That(k4.Equals(k4), Is.True);
				Assert.That(k4.Equals(kv4), Is.True);
				Assert.That(k4.Equals(subspace.Key("hello", 123, "world", true)), Is.True);
			});
			Assert.Multiple(() =>
			{ // T1, T2, T3, T4, T5
				Log($"# {k5}");
				Assert.That(k5.Subspace, Is.SameAs(subspace));
				Assert.That(k5.Items.Item1, Is.EqualTo("hello"));
				Assert.That(k5.Items.Item2, Is.EqualTo(123));
				Assert.That(k5.Items.Item3, Is.EqualTo("world"));
				Assert.That(k5.Items.Item4, Is.True);
				Assert.That(k5.Items.Item5, Is.EqualTo(Math.PI));
				Assert.That(k5.Items, Is.EqualTo(kv5.Items));

				Assert.That(k5.ToSlice(), Is.EqualTo(TuPack.EncodeKey(42, "hello", 123, "world", true, Math.PI)));
				Assert.That(kv5.ToSlice(), Is.EqualTo(TuPack.EncodeKey(42, "hello", 123, "world", true, Math.PI)));

				Assert.That(k5.Equals(k5), Is.True);
				Assert.That(k5.Equals(kv5), Is.True);
				Assert.That(k5.Equals(subspace.Key("hello", 123, "world", true, Math.PI)), Is.True);
			});
			Assert.Multiple(() =>
			{ // T1, T2, T3, T4, T5, T6
				Log($"# {k6}");
				Assert.That(k6.Subspace, Is.SameAs(subspace));
				Assert.That(k6.Items.Item1, Is.EqualTo("hello"));
				Assert.That(k6.Items.Item2, Is.EqualTo(123));
				Assert.That(k6.Items.Item3, Is.EqualTo("world"));
				Assert.That(k6.Items.Item4, Is.True);
				Assert.That(k6.Items.Item5, Is.EqualTo(Math.PI));
				Assert.That(k6.Items.Item6, Is.EqualTo(g));
				Assert.That(k6.Items, Is.EqualTo(kv6.Items));

				Assert.That(k6.ToSlice(), Is.EqualTo(TuPack.EncodeKey(42, "hello", 123, "world", true, Math.PI, g)));
				Assert.That(kv6.ToSlice(), Is.EqualTo(TuPack.EncodeKey(42, "hello", 123, "world", true, Math.PI, g)));

				Assert.That(k6.Equals(k6), Is.True);
				Assert.That(k6.Equals(kv6), Is.True);
				Assert.That(k6.Equals(subspace.Key("hello", 123, "world", true, Math.PI, g)), Is.True);
			});
			Assert.Multiple(() =>
			{ // T1, T2, T3, T4, T5, T6, T7
				Log($"# {k7}");
				Assert.That(k7.Subspace, Is.SameAs(subspace));
				Assert.That(k7.Items.Item1, Is.EqualTo("hello"));
				Assert.That(k7.Items.Item2, Is.EqualTo(123));
				Assert.That(k7.Items.Item3, Is.EqualTo("world"));
				Assert.That(k7.Items.Item4, Is.True);
				Assert.That(k7.Items.Item5, Is.EqualTo(Math.PI));
				Assert.That(k7.Items.Item6, Is.EqualTo(g));
				Assert.That(k7.Items.Item7, Is.EqualTo(now));
				Assert.That(k7.Items, Is.EqualTo(kv7.Items));

				Assert.That(k7.ToSlice(), Is.EqualTo(TuPack.EncodeKey(42, "hello", 123, "world", true, Math.PI, g, now)));
				Assert.That(kv7.ToSlice(), Is.EqualTo(TuPack.EncodeKey(42, "hello", 123, "world", true, Math.PI, g, now)));

				Assert.That(k7.Equals(k7), Is.True);
				Assert.That(k7.Equals(kv7), Is.True);
				Assert.That(k7.Equals(subspace.Key("hello", 123, "world", true, Math.PI, g, now)), Is.True);
			});
			Assert.Multiple(() =>
			{ // T1, T2, T3, T4, T5, T6, T7, T8
				Log($"# {k8}");
				Assert.That(k8.Subspace, Is.SameAs(subspace));
				Assert.That(k8.Items.Item1, Is.EqualTo("hello"));
				Assert.That(k8.Items.Item2, Is.EqualTo(123));
				Assert.That(k8.Items.Item3, Is.EqualTo("world"));
				Assert.That(k8.Items.Item4, Is.True);
				Assert.That(k8.Items.Item5, Is.EqualTo(Math.PI));
				Assert.That(k8.Items.Item6, Is.EqualTo(g));
				Assert.That(k8.Items.Item7, Is.EqualTo(now));
				Assert.That(k8.Items.Item8, Is.EqualTo(vs));
				Assert.That(k8.Items, Is.EqualTo(kv8.Items));

				Assert.That(k8.ToSlice(), Is.EqualTo(TuPack.EncodeKey(42, "hello", 123, "world", true, Math.PI, g, now, vs)));

				Assert.That(k8.Equals(k8), Is.True);
				Assert.That(k8.Equals(kv8), Is.True);
				Assert.That(k8.Equals(subspace.Key("hello", 123, "world", true, Math.PI, g, now, vs)), Is.True);
			});

			// Append Matrix

			Assert.Multiple(() =>
			{
				// T1
				Assert.That(k1.Key(123), Is.EqualTo(k2));
				Assert.That(k1.Key(123, "world"), Is.EqualTo(k3));
				Assert.That(k1.Key(123, "world", true), Is.EqualTo(k4));
				Assert.That(k1.Key(123, "world", true, Math.PI), Is.EqualTo(k5));
				Assert.That(k1.Key(123, "world", true, Math.PI, g), Is.EqualTo(k6));
				Assert.That(k1.Key(123, "world", true, Math.PI, g, now), Is.EqualTo(k7));
				Assert.That(k1.Key(123, "world", true, Math.PI, g, now, vs), Is.EqualTo(k8));

				// T2
				Assert.That(k2.Key("world"), Is.EqualTo(k3));
				Assert.That(k2.Key("world", true), Is.EqualTo(k4));
				Assert.That(k2.Key("world", true, Math.PI), Is.EqualTo(k5));
				Assert.That(k2.Key("world", true, Math.PI, g), Is.EqualTo(k6));
				Assert.That(k2.Key("world", true, Math.PI, g, now), Is.EqualTo(k7));
				Assert.That(k2.Key("world", true, Math.PI, g, now, vs), Is.EqualTo(k8));

				// T3
				Assert.That(k3.Key(true), Is.EqualTo(k4));
				Assert.That(k3.Key(true, Math.PI), Is.EqualTo(k5));
				Assert.That(k3.Key(true, Math.PI, g), Is.EqualTo(k6));
				Assert.That(k3.Key(true, Math.PI, g, now), Is.EqualTo(k7));
				Assert.That(k3.Key(true, Math.PI, g, now, vs), Is.EqualTo(k8));

				// T4
				Assert.That(k4.Key(Math.PI), Is.EqualTo(k5));
				Assert.That(k4.Key(Math.PI, g), Is.EqualTo(k6));
				Assert.That(k4.Key(Math.PI, g, now), Is.EqualTo(k7));
				Assert.That(k4.Key(Math.PI, g, now, vs), Is.EqualTo(k8));

				// T5
				Assert.That(k5.Key(g), Is.EqualTo(k6));
				Assert.That(k5.Key(g, now), Is.EqualTo(k7));
				Assert.That(k5.Key(g, now, vs), Is.EqualTo(k8));

				// T6
				Assert.That(k6.Key(now), Is.EqualTo(k7));
				Assert.That(k6.Key(now, vs), Is.EqualTo(k8));

				// T7
				Assert.That(k7.Key(vs), Is.EqualTo(k8));
			});
		}

		[Test]
		public void Test_FdbVarTupleKey_Encoding()
		{
			static void Verify(Slice prefix, IVarTuple items)
			{
				var subspace = KeySubspace.FromKey(prefix);
				var packed = prefix + TuPack.Pack(items);
				Log($"# {prefix:x} + {items}: -> {packed:x}");
				if (!packed.StartsWith(prefix)) throw new InvalidOperationException();

				var key = subspace.Tuple(items);

				Assert.That(key.TryGetSpan(out var span), Is.False.WithOutput(span.Length).Zero, "");
				Assert.That(key.TryGetSizeHint(out int size), Is.False.WithOutput(size).Zero);

				var buffer = new byte[packed.Count + 32];

				// buffer large enough
				var chunk = buffer.AsSpan();
				chunk.Fill(0x55);
				buffer.AsSpan(chunk.Length).Fill(0xAA);
				Assert.That(key.TryEncode(chunk, out int bytesWritten), Is.True.WithOutput(bytesWritten).EqualTo(packed.Count));
				Assert.That(chunk[..bytesWritten].ToSlice(), Is.EqualTo(packed));

				// buffer with exact size
				chunk = buffer.AsSpan(0, packed.Count);
				chunk.Fill(0x55);
				buffer.AsSpan(chunk.Length).Fill(0xAA);
				Assert.That(key.TryEncode(chunk, out bytesWritten), Is.True.WithOutput(bytesWritten).EqualTo(packed.Count));
				Assert.That(chunk[..bytesWritten].ToSlice(), Is.EqualTo(packed));

				// buffer that is too small by 1 byte
				if (packed.Count > 0)
				{
					chunk = buffer.AsSpan(0, packed.Count - 1);
					chunk.Fill(0x55);
					buffer.AsSpan(chunk.Length).Fill(0xAA);
					Assert.That(key.TryEncode(chunk, out bytesWritten), Is.False.WithOutput(bytesWritten).Zero);
				}

				// buffer that is about 50% the required capacity
				if (packed.Count > 2)
				{
					chunk = buffer.AsSpan(0, packed.Count / 2);
					chunk.Fill(0x55);
					buffer.AsSpan(chunk.Length).Fill(0xAA);
					Assert.That(key.TryEncode(chunk, out bytesWritten), Is.False.WithOutput(bytesWritten).Zero);
				}

				// ToSlice()
				Assert.That(key.ToSlice(), Is.EqualTo(packed));

				// ToSliceOwner()
				var so = key.ToSlice(ArrayPool<byte>.Shared);
				Assert.That(so.Data, Is.EqualTo(packed));
				so.Dispose();
			}

			Assert.Multiple(() =>
			{
				Verify(TuPack.EncodeKey(42), STuple.Create());
				Verify(TuPack.EncodeKey(42), STuple.Create("World"));
				Verify(TuPack.EncodeKey(42), STuple.Create(true, "World"));
				Verify(TuPack.EncodeKey(42), STuple.Create(true, "World", 123));
				Verify(TuPack.EncodeKey(42), STuple.Create(true, "World", 123, Math.PI));
				Verify(TuPack.EncodeKey(42), STuple.Create(true, "World", 123, Math.PI, false));
				Verify(TuPack.EncodeKey(42), STuple.Create(true, "World", 123, Math.PI, false, DateTime.Now));
				Verify(TuPack.EncodeKey(42), STuple.Create(true, STuple.Create("World", STuple.Create(123, Math.PI), false), DateTime.Now));
				Verify(TuPack.EncodeKey(42, 123), STuple.Create("Hello", "World"));

			});
		}

		[Test]
		public void Test_FdbTupleKey_Encoding()
		{
			static void Verify<TKey, TTuple>(IKeySubspace subspace, TKey key, TTuple items)
				where TKey : struct, IFdbKey
				where TTuple : IVarTuple
			{
				var prefix = subspace.GetPrefix();
				Log($"# {prefix:x} + {items}");

				var packedTuple = TuPack.Pack(items);
				var expected = prefix + packedTuple;
				Log($"  - expected: [{expected.Count:N0}] {expected:x}");

				if (items.Count == 0)
				{
					Assert.That(key.TryGetSpan(out var span), Is.True.WithOutput(span.Length).EqualTo(prefix.Count));
				}
				else
				{
					Assert.That(key.TryGetSpan(out var span), Is.False.WithOutput(span.Length).Zero);
				}

				var buffer = new byte[expected.Count + 32];

				// buffer large enough
				var chunk = buffer.AsSpan();
				chunk.Fill(0x55);
				Assert.That(key.TryEncode(chunk, out int bytesWritten), Is.True, $"Failed to encode key: {items}");
				Log($"  - actual  : [{bytesWritten:N0}] {Slice.FromBytes(chunk[..bytesWritten]):x}");
				if (!chunk[..bytesWritten].SequenceEqual(expected.Span))
				{
					DumpVersus(chunk[..bytesWritten], expected.Span);
					Assert.That(bytesWritten, Is.EqualTo(expected.Count), $"Encoded length mismatch: {items}");
					Assert.That(chunk[..prefix.Count].ToArray(), Is.EqualTo(prefix.ToArray()), $"Encoded key does not contains the prefix! {items}");
					Assert.That(chunk[..bytesWritten].ToArray(), Is.EqualTo(expected.ToArray()), $"Encoded key mismatch: {items}");
				}

				// check if the size hint was correct
				if (key.TryGetSizeHint(out int size))
				{
					Log($"  - sizeHint: true, {size}");
					Assert.That(size, Is.GreaterThanOrEqualTo(packedTuple.Count));
				}
				else
				{
					Log("  - sizeHint: false, 0");
					Assert.That(size, Is.Zero);
				}

				// buffer with exact size
				chunk = buffer.AsSpan(0, expected.Count);
				chunk.Fill(0x55);
				buffer.AsSpan(chunk.Length).Fill(0xAA);
				Assert.That(key.TryEncode(chunk, out bytesWritten), Is.True, $"Failed to encode key: {items}");
				if (!chunk[..bytesWritten].SequenceEqual(expected.Span))
				{
					DumpVersus(chunk[..bytesWritten], expected.Span);
					Assert.That(bytesWritten, Is.EqualTo(expected.Count), $"Encoded length mismatch: {items}");
					Assert.That(chunk[..prefix.Count].ToArray(), Is.EqualTo(prefix.ToArray()), $"Encoded key does not contains the prefix! {items}");
					Assert.That(chunk[..bytesWritten].ToArray(), Is.EqualTo(expected.ToArray()), $"Encoded key mismatch: {items}");
				}
				Assert.That(buffer.AsSpan(chunk.Length).ContainsAnyExcept((byte) 0xAA), Is.False);

				// test all possible sizes that are not large enough
				for(int i = expected.Count - 1; i >= 0; i--)
				{
					chunk = buffer.AsSpan(0, i);
					chunk.Fill(0x55);
					buffer.AsSpan(chunk.Length).Fill(0xAA);
					Assert.That(key.TryEncode(chunk, out bytesWritten), Is.False.WithOutput(bytesWritten).Zero);
					Assert.That(buffer.AsSpan(chunk.Length).ContainsAnyExcept((byte) 0xAA), Is.False);
				}

				// ToSlice()
				Assert.That(key.ToSlice(), Is.EqualTo(expected));

				// ToSliceOwner()
				var so = key.ToSlice(ArrayPool<byte>.Shared);
				Assert.That(so.Data, Is.EqualTo(expected));
				so.Dispose();
			}

			var subspace = KeySubspace.FromKey(TuPack.EncodeKey(42));

			var now = DateTime.Now;
			var vs = VersionStamp.Incomplete(0x1234);
			var uuid128 = Uuid128.NewUuid();
			Verify(subspace, subspace.Key(), STuple.Create());
			Verify(subspace, subspace.Key("Hello"), STuple.Create("Hello"));
			Verify(subspace, subspace.Key("Héllo", "Wörld!"), STuple.Create("Héllo", "Wörld!"));
			Verify(subspace, subspace.Key("Hello", true, "Wörld"), STuple.Create("Hello", true, "Wörld"));
			Verify(subspace, subspace.Key("Hello", true, "Wörld", 123), STuple.Create("Hello", true, "Wörld", 123));
			Verify(subspace, subspace.Key("Hello", true, "Wörld", 123, Math.PI), STuple.Create("Hello", true, "Wörld", 123, Math.PI));
			Verify(subspace, subspace.Key("Hello", true, "Wörld", 123, Math.PI, vs), STuple.Create("Hello", true, "Wörld", 123, Math.PI, vs));
			Verify(subspace, subspace.Key("Hello", true, "Wörld", 123, Math.PI, vs, now), STuple.Create("Hello", true, "Wörld", 123, Math.PI, vs, now));
			Verify(subspace, subspace.Key("Hello", true, "Wörld", 123, Math.PI, vs, now, uuid128), STuple.Create("Hello", true, "Wörld", 123, Math.PI, vs, now, uuid128));
			Verify(subspace, subspace.Key("Hello", true, STuple.Create("Wörld", STuple.Create(123, Math.PI), vs), now, uuid128), STuple.Create("Hello", true, STuple.Create("Wörld", STuple.Create(123, Math.PI), vs), now, uuid128));
		}

		[Test]
		public void Test_FdbSystemKey_Basics()
		{
			{ // 0xFF
				var k = FdbSystemKey.System;
				Log($"# {k}");
				Assert.That(k.IsSpecial, Is.False);
				Assert.That(k.Suffix, Is.EqualTo(Slice.Empty));

				Assert.That(k.ToSlice(), Is.EqualTo(Slice.FromBytes([ 0xFF ])));

				Assert.That(k, Is.EqualTo(new FdbSystemKey(Slice.Empty, false)));
				Assert.That(k, Is.EqualTo(new FdbRawKey(Slice.FromBytes([ 0xFF ]))));
				Assert.That(k, Is.EqualTo(Slice.FromBytes([ 0xFF ])));
				Assert.That(k, Is.EqualTo(new FdbTupleKey(null, STuple.Create(TuPackUserType.System))));
				Assert.That(k, Is.EqualTo(new FdbTupleKey(null, STuple.Create(TuPackUserType.System))));

				Assert.That(k, Is.Not.EqualTo(new FdbSystemKey(Slice.Empty, true)));
				Assert.That(k, Is.Not.EqualTo(new FdbRawKey(Slice.FromBytes([ 0xFF, 0xFF ]))));
				Assert.That(k, Is.Not.EqualTo(Slice.FromBytes([ 0xFF, 0xFF ])));
			}
			{ // 0xFF 0xFF
				var k = FdbSystemKey.Special;
				Log($"# {k}");
				Assert.That(k.IsSpecial, Is.True);
				Assert.That(k.Suffix, Is.EqualTo(Slice.Empty));

				Assert.That(k.ToSlice(), Is.EqualTo(Slice.FromBytes([ 0xFF, 0xFF ])));

				Assert.That(k, Is.EqualTo(new FdbSystemKey(Slice.Empty, true)));
				Assert.That(k, Is.EqualTo(new FdbRawKey(Slice.FromBytes([ 0xFF, 0xFF ]))));
				Assert.That(k, Is.EqualTo(Slice.FromBytes([ 0xFF, 0xFF ])));
				Assert.That(k, Is.EqualTo(new FdbTupleKey(null, STuple.Create(TuPackUserType.Special))));

				Assert.That(k, Is.Not.EqualTo(new FdbSystemKey(Slice.Empty, false)));
				Assert.That(k, Is.Not.EqualTo(new FdbRawKey(Slice.FromBytes([ 0xFF ]))));
				Assert.That(k, Is.Not.EqualTo(Slice.FromBytes([ 0xFF ])));
			}
			{ // System: MetadataVersion
				var k = FdbKey.ToSystemKey("/metadataVersion");

				Log($"# {k}");
				Assert.That(k.IsSpecial, Is.False);
				Assert.That(k.Suffix, Is.EqualTo(Slice.FromString("/metadataVersion")));

				var expectedBytes = Slice.FromByteString("\xFF/metadataVersion");
				Assert.That(k.ToSlice(), Is.EqualTo(expectedBytes));

				Assert.That(k, Is.EqualTo(new FdbSystemKey(Slice.FromString("/metadataVersion"), special: false)));
				Assert.That(k, Is.EqualTo(new FdbRawKey(expectedBytes)));
				Assert.That(k, Is.EqualTo(expectedBytes));
				Assert.That(k, Is.EqualTo(new FdbTupleKey(null, STuple.Create(TuPackUserType.SystemKey("/metadataVersion")))));

				Assert.That(k, Is.Not.EqualTo(new FdbSystemKey(Slice.FromString("/metadataVersion"), special: true)));
				Assert.That(k, Is.Not.EqualTo(new FdbSystemKey(Slice.FromString("/metadataversion"), special: false)));
				Assert.That(k, Is.Not.EqualTo(new FdbVarTupleValue(STuple.Create(TuPackUserType.SpecialKey("/metadataVersion")))));

			}
			{ // Special: Status Json
				var k = FdbKey.ToSpecialKey("/status/json");
				Log($"# {k}");
				Assert.That(k.IsSpecial, Is.True);
				Assert.That(k.Suffix, Is.EqualTo(Slice.FromString("/status/json")));

				var expectedBytes = Slice.FromByteString("\xFF\xFF/status/json");
				Assert.That(k.ToSlice(), Is.EqualTo(expectedBytes));

				Assert.That(k, Is.EqualTo(new FdbSystemKey(Slice.FromString("/status/json"), special: true)));
				Assert.That(k, Is.EqualTo(new FdbRawKey(expectedBytes)));
				Assert.That(k, Is.EqualTo(expectedBytes));
				Assert.That(k, Is.EqualTo(new FdbTupleKey(null, STuple.Create(TuPackUserType.SpecialKey("/status/json")))));

				Assert.That(k, Is.Not.EqualTo(new FdbSystemKey(Slice.FromString("/status/json"), special: false)));
				Assert.That(k, Is.Not.EqualTo(new FdbSystemKey(Slice.FromString("/status/JSON"), special: true)));
				Assert.That(k, Is.Not.EqualTo(new FdbSystemKey(Slice.FromString("/status/json/"), special: true)));
				Assert.That(k, Is.Not.EqualTo(new FdbVarTupleValue(STuple.Create(TuPackUserType.SystemKey("/status/json")))));

				Assert.That(k.Equals<FdbSystemKey>(new(Slice.FromString("/status/json"), special: true)), Is.True);
				Assert.That(k.Equals<FdbRawKey>(new(expectedBytes)), Is.True);
				Assert.That(k.Equals<FdbTupleKey>(new(null, STuple.Create(TuPackUserType.SpecialKey("/status/json")))), Is.True);
			}
		}

		[Test]
		public void Test_FdbSystemKey_Comparisons()
		{
			var metadataVersion = FdbKey.ToSystemKey("/metadataVersion");
			var statusJson = FdbKey.ToSpecialKey("/status/json");

			Assert.That(FdbSystemKey.System, Is.EqualTo(FdbKey.FromBytes([ 0xFF ])));
			Assert.That(FdbSystemKey.Special, Is.EqualTo(FdbKey.FromBytes([ 0xFF, 0xFF ])));

			Assert.That(metadataVersion, Is.EqualTo(FdbKey.ToSystemKey("/metadata"u8).Bytes("Version"u8)));

			Assert.That(FdbSystemKey.System, Is.LessThan(FdbSystemKey.Special));
			Assert.That(FdbSystemKey.Special, Is.GreaterThan(FdbSystemKey.System));

			Assert.That(metadataVersion, Is.GreaterThan(FdbSystemKey.System));
			Assert.That(metadataVersion, Is.LessThan(FdbSystemKey.Special));
			Assert.That(metadataVersion, Is.GreaterThan(FdbKey.FromBytes("/metadataVersion"u8)));

			Assert.That(statusJson, Is.GreaterThan(FdbSystemKey.System));
			Assert.That(statusJson, Is.GreaterThan(FdbSystemKey.Special));
			Assert.That(statusJson, Is.GreaterThan(metadataVersion));
			Assert.That(metadataVersion, Is.GreaterThan(FdbKey.FromBytes("/status/json"u8)));

			Assert.That(FdbSystemKey.System.Bytes("/metadataVersion"), Is.EqualTo(metadataVersion));
			Assert.That(FdbSystemKey.System.Bytes("/metadataVersion").ToSlice(), Is.EqualTo(Slice.FromByteString("\xFF/metadataVersion")));

			Assert.That(FdbSystemKey.Special.Bytes("/status/json"), Is.EqualTo(statusJson));
			Assert.That(FdbSystemKey.Special.Bytes("/status/json").ToSlice(), Is.EqualTo(Slice.FromByteString("\xFF\xFF/status/json")));
			Assert.That(FdbSystemKey.Special.Bytes("/status").Bytes("/json"), Is.EqualTo(statusJson));
		}

		[Test]
		public void Test_GetSuccessor_Key()
		{
			{
				var k = FdbKey.FromBytes(Slice.Empty).Successor();
				Log($"# {k}");
				Assert.That(k.Parent.Data, Is.EqualTo(Slice.Empty));
				Assert.That(k.ToSlice(), Is.EqualTo(Slice.FromBytes([ 0 ])));
			}
			{
				var k = FdbKey.FromBytes([ 0x00 ]).Successor();
				Log($"# {k}");
				Assert.That(k.ToSlice(), Is.EqualTo(Slice.FromBytes([ 0x00, 0 ])));
			}
			{
				var k = FdbKey.FromBytes([ 0xFF ]).Successor();
				Log($"# {k}");
				Assert.That(k.ToSlice(), Is.EqualTo(Slice.FromBytes([ 0xFF, 0 ])));
			}
			{
				var p = FdbKey.FromBytes(Slice.FromBytes("hello"u8));
				var k = p.Successor();
				Log($"# {k}");

				Assert.That(k.Parent, Is.EqualTo(p));

				Assert.That(k.ToSlice(), Is.EqualTo(Slice.FromBytes("hello\0"u8)));
			}
			{
				var subspace = GetSubspace(FdbPath.Parse("/Foo/Bar"), STuple.Create(42));
				var p = subspace.Key("hello");
				var k = p.Successor();
				Log($"# {k}");

				Assert.That(k.Parent, Is.EqualTo(p));

				Assert.That(k.ToSlice(), Is.EqualTo(Slice.FromBytes("\x15\x2A\x02hello\0\0"u8)));
			}
		}

		[Test]
		public void Test_GetNextSibling_Key()
		{
			{
				var k = FdbKey.FromBytes(Slice.Empty).NextSibling();
				Log($"# {k}");
				Assert.That(k.Parent.Data, Is.EqualTo(Slice.Empty));
				Assert.That(k.ToSlice(), Is.EqualTo(Slice.FromBytes([ 0 ])));
			}
			{
				var k = FdbKey.FromBytes(Slice.FromBytes([ 0x00 ])).NextSibling();
				Log($"# {k}");
				Assert.That(k.Parent.Data, Is.EqualTo(Slice.FromBytes([ 0x00 ])));
				Assert.That(k.ToSlice(), Is.EqualTo(Slice.FromBytes([ 0x01 ])));
			}
			{
				var k = FdbKey.FromBytes(Slice.FromBytes([ 0xFF ])).NextSibling();
				Log($"# {k}");
				Assert.That(() => k.ToSlice(), Throws.ArgumentException);
			}
			{
				var k = FdbKey.FromBytes(Slice.FromBytes([ 0x00, 0xFF ])).NextSibling();
				Log($"# {k}");
				Assert.That(k.ToSlice(), Is.EqualTo(Slice.FromBytes([ 0x01 ])));
			}
			{
				var k = FdbKey.FromBytes(Slice.FromBytes([ 0x00, 0xFF, 0xFF ])).NextSibling();
				Log($"# {k}");
				Assert.That(k.ToSlice(), Is.EqualTo(Slice.FromBytes([ 0x01 ])));
			}
			{
				var k = FdbKey.FromBytes(Slice.FromBytes([ 0xFF, 0xFF, 0xFF ])).NextSibling();
				Log($"# {k}");
				Assert.That(() => k.ToSlice(), Throws.ArgumentException);
			}
			{
				var p = FdbKey.FromBytes("hello"u8);
				var k = p.NextSibling();
				Log($"# {k}");

				Assert.That(k.Parent, Is.EqualTo(p));
				Assert.That(k.ToSlice(), Is.EqualTo(Slice.FromBytes("hellp"u8)));
			}
			{
				var subspace = GetSubspace(FdbPath.Parse("/Foo/Bar"), STuple.Create(42));
				var p = subspace.Key("hello");
				var k = p.NextSibling();
				Log($"# {k}");

				Assert.That(k.Parent, Is.EqualTo(p));

				Assert.That(k.ToSlice(), Is.EqualTo(Slice.FromBytes("\x15\x2A\x02hello\x01"u8)));
			}
		}

	}

}
