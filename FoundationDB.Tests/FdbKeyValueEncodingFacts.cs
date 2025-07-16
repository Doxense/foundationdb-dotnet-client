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

// ReSharper disable StringLiteralTypo
namespace FoundationDB.Client.Tests
{
	using System.Text;
	using SnowBank.Data.Tuples.Binary;

	[TestFixture]
	[Category("Fdb-Client-InProc")]
	[Parallelizable(ParallelScope.All)]
	public class FdbKeyValueEncodingFacts : FdbSimpleTest
	{

		[Test]
		public void Test_FdbKey_Raw_Encoding()
		{
			static void Verify(Slice value)
			{
				{ // Slice
					var rawKey = value;

					var key = FdbKey.ToBytes(rawKey);
					Assert.That(key.ToSlice(), Is.EqualTo(rawKey));
					Assert.That(key.TryGetSpan(out var span), Is.True.WithOutput(span.ToSlice()).EqualTo(rawKey));
					Assert.That(key.TryGetSizeHint(out int size), Is.True.WithOutput(size).EqualTo(rawKey.Count));

					Assert.That(key.IsNull, Is.False);
					Assert.That(((IFdbKey) key).GetSubspace(), Is.Null);
				}
				{ // byte[]
					var rawKey = value.ToArray();

					var key = FdbKey.ToBytes(rawKey);
					Assert.That(key.ToSlice().ToArray(), Is.EqualTo(rawKey));
					Assert.That(key.TryGetSpan(out var span), Is.True.WithOutput(span.ToArray()).EqualTo(rawKey));
					Assert.That(key.TryGetSizeHint(out int size), Is.True.WithOutput(size).EqualTo(rawKey.Length));
					Assert.That(key.IsNull, Is.False);
					Assert.That(((IFdbKey) key).GetSubspace(), Is.Null);
				}
			}

			Verify(Slice.FromBytes("Hello, World!"u8));
		}

		[Test]
		public void Test_FdbKey_VarTuple_Encoding()
		{
			static void Verify(Slice prefix, IVarTuple items)
			{
				var subspace = new DynamicKeySubspace(prefix, TuPack.Encoding.GetDynamicKeyEncoder(), SubspaceContext.Default);
				var packed = subspace.Pack(items);
				Log($"# {prefix:x} + {items}: -> {packed:x}");
				if (!packed.StartsWith(prefix)) throw new InvalidOperationException();

				var key = subspace.PackKey(items);

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
		public void Test_FdbKey_STuple_Encoding()
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

				Assert.That(key.TryGetSpan(out var span), Is.False.WithOutput(span.Length).Zero, "");

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

			var subspace = new DynamicKeySubspace(TuPack.EncodeKey(42), TuPack.Encoding.GetDynamicKeyEncoder(), SubspaceContext.Default);

			var now = DateTime.Now;
			var vs = VersionStamp.Incomplete(0x1234);
			var uuid128 = Uuid128.NewUuid();
			Verify(subspace, subspace.GetKey(), STuple.Create());
			Verify(subspace, subspace.GetKey("Hello"), STuple.Create("Hello"));
			Verify(subspace, subspace.GetKey("Héllo", "Wörld!"), STuple.Create("Héllo", "Wörld!"));
			Verify(subspace, subspace.GetKey("Hello", true, "Wörld"), STuple.Create("Hello", true, "Wörld"));
			Verify(subspace, subspace.GetKey("Hello", true, "Wörld", 123), STuple.Create("Hello", true, "Wörld", 123));
			Verify(subspace, subspace.GetKey("Hello", true, "Wörld", 123, Math.PI), STuple.Create("Hello", true, "Wörld", 123, Math.PI));
			Verify(subspace, subspace.GetKey("Hello", true, "Wörld", 123, Math.PI, vs), STuple.Create("Hello", true, "Wörld", 123, Math.PI, vs));
			Verify(subspace, subspace.GetKey("Hello", true, "Wörld", 123, Math.PI, vs, now), STuple.Create("Hello", true, "Wörld", 123, Math.PI, vs, now));
			Verify(subspace, subspace.GetKey("Hello", true, "Wörld", 123, Math.PI, vs, now, uuid128), STuple.Create("Hello", true, "Wörld", 123, Math.PI, vs, now, uuid128));
			Verify(subspace, subspace.GetKey("Hello", true, STuple.Create("Wörld", STuple.Create(123, Math.PI), vs), now, uuid128), STuple.Create("Hello", true, STuple.Create("Wörld", STuple.Create(123, Math.PI), vs), now, uuid128));
		}

		[Test]
		public void Test_FdbValue_Raw_Encoding()
		{
			{ // Slice
				var rawValue = Slice.FromBytes("Hello, World!"u8);

				var value = FdbValue.ToBytes(rawValue);
				Assert.That(value.ToSlice(), Is.EqualTo(rawValue));
				Assert.That(value.TryGetSpan(out var span), Is.True.WithOutput(span.ToSlice()).EqualTo(rawValue));
				Assert.That(value.TryGetSizeHint(out int size), Is.True.WithOutput(size).EqualTo(rawValue.Count));
			}
			{ // byte[]
				var rawValue = "Hello, World!"u8.ToArray();

				var value = FdbValue.ToBytes(rawValue);
				Assert.That(value.ToSlice().ToArray(), Is.EqualTo(rawValue));
				Assert.That(value.TryGetSpan(out var span), Is.True.WithOutput(span.ToArray()).EqualTo(rawValue));
				Assert.That(value.TryGetSizeHint(out int size), Is.True.WithOutput(size).EqualTo(rawValue.Length));
			}
			{ // MemoryStream
				var ms = new MemoryStream();
				ms.Write("Hello, World!"u8);

				var value = FdbValue.ToBytes(ms);
				Assert.That(value.ToSlice().ToArray(), Is.EqualTo(ms.ToArray()));
				Assert.That(value.TryGetSpan(out var span), Is.True.WithOutput(span.ToArray()).EqualTo(ms.ToArray()));
				Assert.That(value.TryGetSizeHint(out int size), Is.True.WithOutput(size).EqualTo(ms.Length));
			}
		}

		[Test]
		public void Test_FdbValue_Utf8_Encoding()
		{
			static void Verify(string literal, ReadOnlySpan<byte> expectedUtf8)
			{
				var expected = Slice.FromBytes(expectedUtf8);
				Log($"# [{literal.Length:N0}] \"{literal}\"");
				Log($"> expected: [{expected.Count:N0}] `{expected}`");

				{ // string
					var value = FdbValue.ToTextUtf8(literal);
					var slice = value.ToSlice();
					Log($"> actual  : [{slice.Count:N0}] `{slice}`");
					Assert.That(slice, Is.EqualTo(expected));
					Assert.That(value.TryGetSpan(out var span), Is.EqualTo(expectedUtf8.Length == 0).WithOutput(span.Length).Zero);
					Assert.That(value.TryGetSizeHint(out int size), Is.True.WithOutput(size).GreaterThanOrEqualTo(expected.Count));
				}
				{ // ReadOnlyMemory<char>
					var value = FdbValue.ToTextUtf8(literal.ToArray().AsMemory());
					Assert.That(value.ToSlice(), Is.EqualTo(expected));
					Assert.That(value.TryGetSpan(out var span), Is.EqualTo(expectedUtf8.Length == 0).WithOutput(span.Length).Zero);
					Assert.That(value.TryGetSizeHint(out int size), Is.True.WithOutput(size).GreaterThanOrEqualTo(expected.Count));
				}
				{ // char[]
					var value = FdbValue.ToTextUtf8(literal.ToArray());
					Assert.That(value.ToSlice(), Is.EqualTo(expected));
					Assert.That(value.TryGetSpan(out var span), Is.EqualTo(expectedUtf8.Length == 0).WithOutput(span.Length).Zero);
					Assert.That(value.TryGetSizeHint(out int size), Is.True.WithOutput(size).GreaterThanOrEqualTo(expected.Count));
				}
				{ // StringBuilder
					var sb = new StringBuilder().Append(literal);
					var value = FdbValue.ToTextUtf8(sb);
					Assert.That(value.ToSlice(), Is.EqualTo(expected));
					Assert.That(value.TryGetSpan(out var span), Is.EqualTo(expectedUtf8.Length == 0).WithOutput(span.Length).Zero);
					Assert.That(value.TryGetSizeHint(out int size), Is.True.WithOutput(size).GreaterThanOrEqualTo(expected.Count)); // GetMaxByteCount() always add 3 bytes to the length (for the BOM??)
				}
				{ // ReadOnlySpan<char>
					var value = FdbValue.ToTextUtf8(literal.AsSpan());
					Assert.That(value.ToSlice(), Is.EqualTo(expected));
					Assert.That(value.TryGetSpan(out var span), Is.EqualTo(expectedUtf8.Length == 0).WithOutput(span.Length).Zero);
					Assert.That(value.TryGetSizeHint(out int size), Is.True.WithOutput(size).GreaterThanOrEqualTo(expected.Count));
				}

				// repeatedly test encoding with various buffer sizes
				Span<byte> buffer = stackalloc byte[expectedUtf8.Length + 1];
				for(int i = buffer.Length; i >= 0; i--)
				{
					buffer.Fill(0xFF);
					var span = buffer[..i];
					span.Fill(0xAA);

					var value = FdbValue.ToTextUtf8(literal);
					if (i >= expectedUtf8.Length)
					{
						Assert.That(value.TryEncode(span, out var bytesWritten), Is.True.WithOutput(bytesWritten).EqualTo(expectedUtf8.Length));
						if (!span[..bytesWritten].SequenceEqual(expectedUtf8))
						{
							Assert.That(Slice.FromBytes(span[..bytesWritten]), Is.EqualTo(expected));
						}
					}
					else
					{
						Assert.That(value.TryEncode(span, out var bytesWritten), Is.False.WithOutput(bytesWritten).Zero);
					}
					Assert.That(buffer[i..].ContainsAnyExcept((byte) 0xFF), Is.False);
				}
			}

			Verify("", ""u8);
			Verify("A", "A"u8);
			Verify("Hello, World!", "Hello, World!"u8);
			Verify("こんにちは世界", "こんにちは世界"u8);
			Verify("Voix ambiguë d’un cœur qui, au zéphyr, préfère les jattes de kiwis", "Voix ambiguë d’un cœur qui, au zéphyr, préfère les jattes de kiwis"u8);

			var largeAscii = GetRandomHexString(1025);
			Verify(largeAscii, Encoding.UTF8.GetBytes(largeAscii));

			var largeUnicode = GetRandomString("こんにちは世界", 513);
			Verify(largeUnicode, Encoding.UTF8.GetBytes(largeUnicode));
		}

		[Test]
		public void Test_FdbValue_Tuple_Encoding()
		{
			static void Verify<TTuple>(in TTuple tuple)
				where TTuple : IVarTuple
			{
				var expectedPacked = TuPack.Pack(tuple);
				var expectedUnpacked = TuPack.Unpack(expectedPacked);
				Log($"# ({tuple.GetType().GetFriendlyName()}) {tuple}: -> [{expectedPacked.Count:N0}] {expectedPacked:x} -> {expectedUnpacked}");

				var value = FdbValue.FromTuple(tuple);
				var slice = value.ToSlice();
				Assert.That(slice, Is.EqualTo(expectedPacked));
				Assert.That(value.TryGetSpan(out var span), Is.False.WithOutput(span.Length).Zero);
				Assert.That(value.TryGetSizeHint(out int size), Is.False.WithOutput(size).Zero);

				var unpacked = TuPack.Unpack(slice);
				Assert.That(unpacked, Is.EqualTo(expectedUnpacked));
			}

			Verify(STuple.Create());
			Verify(STuple.Create("Hello"));
			Verify(STuple.Create("Hello", true));
			Verify(STuple.Create("Hello", true, "World"));
			Verify(STuple.Create("Hello", true, "World", 123));
			Verify(STuple.Create("Hello", true, "World", 123, null, DateTime.Now));
			Verify(STuple.Create("Hello", true, "World", 123, null, DateTime.Now, Guid.NewGuid()));
			Verify(STuple.Create("Hello", true, "World", 123, null, DateTime.Now, Guid.NewGuid(), VersionStamp.Incomplete(0x1234)));

			Verify(STuple.Create(STuple.Create(STuple.Create(STuple.Create()))));
			Verify(STuple.Create("Hello", STuple.Create(true, "World", STuple.Create(123, null, DateTime.Now), Guid.NewGuid())));

			Verify((IVarTuple) STuple.Create("Hello", true, "World", 123));
			Verify(STuple.Create((ReadOnlySpan<object>) [ "Hello", true, "World", 123 ]));
			Verify(SlicedTuple.Unpack(TuPack.EncodeKey("Hello", true, "World", 123)));

			Verify(STuple.Create(default(string), default(bool?), default(int?), default(long?), default(TimeSpan?), default(VersionStamp?), default(Uuid128?)));

			Verify(STuple.Create("ThisIsAVeryLongTupleThatWillSurelyNotFitInTheInitialBufferAndRequireABufferRentedFromAPoolAndMultipleCallsToTheTryEncodeMethod", Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid()));
		}

		[Test]
		public void Test_FdbValue_LittleEndian_Encoding()
		{
			static void Verify<TValue, TEncoder>(FdbValue<TValue, TEncoder> value, Slice expected)
				where TEncoder : struct, ISpanEncoder<TValue>
			{
				var slice = value.ToSlice();
				if (!slice.Equals(expected))
				{
					DumpVersus(slice, expected);
					Assert.That(slice, Is.EqualTo(expected));
				}
				Assert.That(value.TryGetSpan(out var span), Is.False.WithOutput(span.Length).Zero);
				Assert.That(value.TryGetSizeHint(out int size), Is.True.WithOutput(size).EqualTo(expected.Count));
			}

			Assert.Multiple(() =>
			{
				// Int32
				Verify(FdbValue.ToFixed32LittleEndian(0x12), Slice.FromFixed32(0x12));
				Verify(FdbValue.ToFixed32LittleEndian(0x1234), Slice.FromFixed32(0x1234));
				Verify(FdbValue.ToFixed32LittleEndian(0x123456), Slice.FromFixed32(0x123456));
				Verify(FdbValue.ToFixed32LittleEndian(0x12345678), Slice.FromFixed32(0x12345678));
				Verify(FdbValue.ToFixed32LittleEndian(-1), Slice.FromFixed32(-1));
				Verify(FdbValue.ToFixed32LittleEndian(-123456), Slice.FromFixed32(-123456));
				Verify(FdbValue.ToFixed32LittleEndian(int.MinValue), Slice.FromFixed32(int.MinValue));

				// UInt32
				Verify(FdbValue.ToFixed32LittleEndian((uint)0), Slice.FromFixedU32(0));
				Verify(FdbValue.ToFixed32LittleEndian((uint)0x12), Slice.FromFixedU32(0x12));
				Verify(FdbValue.ToFixed32LittleEndian((uint)0x1234), Slice.FromFixedU32(0x1234));
				Verify(FdbValue.ToFixed32LittleEndian((uint)0x123456), Slice.FromFixedU32(0x123456));
				Verify(FdbValue.ToFixed32LittleEndian((uint)0x12345678), Slice.FromFixedU32(0x12345678));

				// Int64
				Verify(FdbValue.ToFixed64LittleEndian(0), Slice.FromFixed64(0));
				Verify(FdbValue.ToFixed64LittleEndian(0x12), Slice.FromFixed64(0x12));
				Verify(FdbValue.ToFixed64LittleEndian(0x1234), Slice.FromFixed64(0x1234));
				Verify(FdbValue.ToFixed64LittleEndian(0x123456), Slice.FromFixed64(0x123456));
				Verify(FdbValue.ToFixed64LittleEndian(0x12345678), Slice.FromFixed64(0x12345678));
				Verify(FdbValue.ToFixed64LittleEndian(0x123456789A), Slice.FromFixed64(0x123456789A));
				Verify(FdbValue.ToFixed64LittleEndian(0x123456789ABC), Slice.FromFixed64(0x123456789ABC));
				Verify(FdbValue.ToFixed64LittleEndian(0x123456789ABCDE), Slice.FromFixed64(0x123456789ABCDE));
				Verify(FdbValue.ToFixed64LittleEndian(0x123456789ABCDEF0), Slice.FromFixed64(0x123456789ABCDEF0));
				Verify(FdbValue.ToFixed64LittleEndian(-1), Slice.FromFixed64(-1));
				Verify(FdbValue.ToFixed64LittleEndian(-123456), Slice.FromFixed64(-123456));
				Verify(FdbValue.ToFixed64LittleEndian(int.MinValue), Slice.FromFixed64(int.MinValue));
				Verify(FdbValue.ToFixed64LittleEndian(long.MinValue), Slice.FromFixed64(long.MinValue));

				// UInt64
				Verify(FdbValue.ToFixed64LittleEndian((ulong)0), Slice.FromFixedU64(0));
				Verify(FdbValue.ToFixed64LittleEndian((ulong)0x12), Slice.FromFixedU64(0x12));
				Verify(FdbValue.ToFixed64LittleEndian((ulong)0x1234), Slice.FromFixedU64(0x1234));
				Verify(FdbValue.ToFixed64LittleEndian((ulong)0x123456), Slice.FromFixedU64(0x123456));
				Verify(FdbValue.ToFixed64LittleEndian((ulong)0x12345678), Slice.FromFixedU64(0x12345678));
				Verify(FdbValue.ToFixed64LittleEndian((ulong)0x123456789A), Slice.FromFixedU64(0x123456789A));
				Verify(FdbValue.ToFixed64LittleEndian((ulong)0x123456789ABC), Slice.FromFixedU64(0x123456789ABC));
				Verify(FdbValue.ToFixed64LittleEndian((ulong)0x123456789ABCDE), Slice.FromFixedU64(0x123456789ABCDE));
				Verify(FdbValue.ToFixed64LittleEndian((ulong)0x123456789ABCDEF0), Slice.FromFixedU64(0x123456789ABCDEF0));
				Verify(FdbValue.ToFixed64LittleEndian(ulong.MaxValue), Slice.FromFixedU64(ulong.MaxValue));
			});

		}

		[Test]
		public void Test_FdbValue_BigEndian_Encoding()
		{
			static void Verify<TValue, TEncoder>(FdbValue<TValue, TEncoder> value, Slice expected)
				where TEncoder : struct, ISpanEncoder<TValue>
			{
				var slice = value.ToSlice();
				if (!slice.Equals(expected))
				{
					DumpVersus(slice, expected);
					Assert.That(slice, Is.EqualTo(expected));
				}
				Assert.That(value.TryGetSpan(out var span), Is.False.WithOutput(span.Length).Zero);
				Assert.That(value.TryGetSizeHint(out int size), Is.True.WithOutput(size).EqualTo(expected.Count));
			}

			Assert.Multiple(() =>
			{
				// Int32
				Verify(FdbValue.ToFixed32BigEndian(0x12), Slice.FromFixed32BE(0x12));
				Verify(FdbValue.ToFixed32BigEndian(0x1234), Slice.FromFixed32BE(0x1234));
				Verify(FdbValue.ToFixed32BigEndian(0x123456), Slice.FromFixed32BE(0x123456));
				Verify(FdbValue.ToFixed32BigEndian(0x12345678), Slice.FromFixed32BE(0x12345678));
				Verify(FdbValue.ToFixed32BigEndian(-1), Slice.FromFixed32BE(-1));
				Verify(FdbValue.ToFixed32BigEndian(-123456), Slice.FromFixed32BE(-123456));
				Verify(FdbValue.ToFixed32BigEndian(int.MinValue), Slice.FromFixed32BE(int.MinValue));

				// UInt32
				Verify(FdbValue.ToFixed32BigEndian((uint)0), Slice.FromFixedU32BE(0));
				Verify(FdbValue.ToFixed32BigEndian((uint)0x12), Slice.FromFixedU32BE(0x12));
				Verify(FdbValue.ToFixed32BigEndian((uint)0x1234), Slice.FromFixedU32BE(0x1234));
				Verify(FdbValue.ToFixed32BigEndian((uint)0x123456), Slice.FromFixedU32BE(0x123456));
				Verify(FdbValue.ToFixed32BigEndian((uint)0x12345678), Slice.FromFixedU32BE(0x12345678));

				// Int64
				Verify(FdbValue.ToFixed64BigEndian(0), Slice.FromFixed64BE(0));
				Verify(FdbValue.ToFixed64BigEndian(0x12), Slice.FromFixed64BE(0x12));
				Verify(FdbValue.ToFixed64BigEndian(0x1234), Slice.FromFixed64BE(0x1234));
				Verify(FdbValue.ToFixed64BigEndian(0x123456), Slice.FromFixed64BE(0x123456));
				Verify(FdbValue.ToFixed64BigEndian(0x12345678), Slice.FromFixed64BE(0x12345678));
				Verify(FdbValue.ToFixed64BigEndian(0x123456789A), Slice.FromFixed64BE(0x123456789A));
				Verify(FdbValue.ToFixed64BigEndian(0x123456789ABC), Slice.FromFixed64BE(0x123456789ABC));
				Verify(FdbValue.ToFixed64BigEndian(0x123456789ABCDE), Slice.FromFixed64BE(0x123456789ABCDE));
				Verify(FdbValue.ToFixed64BigEndian(0x123456789ABCDEF0), Slice.FromFixed64BE(0x123456789ABCDEF0));
				Verify(FdbValue.ToFixed64BigEndian(-1), Slice.FromFixed64BE(-1));
				Verify(FdbValue.ToFixed64BigEndian(-123456), Slice.FromFixed64BE(-123456));
				Verify(FdbValue.ToFixed64BigEndian(int.MinValue), Slice.FromFixed64BE(int.MinValue));
				Verify(FdbValue.ToFixed64BigEndian(long.MinValue), Slice.FromFixed64BE(long.MinValue));

				// UInt64
				Verify(FdbValue.ToFixed64BigEndian((ulong)0), Slice.FromFixedU64BE(0));
				Verify(FdbValue.ToFixed64BigEndian((ulong)0x12), Slice.FromFixedU64BE(0x12));
				Verify(FdbValue.ToFixed64BigEndian((ulong)0x1234), Slice.FromFixedU64BE(0x1234));
				Verify(FdbValue.ToFixed64BigEndian((ulong)0x123456), Slice.FromFixedU64BE(0x123456));
				Verify(FdbValue.ToFixed64BigEndian((ulong)0x12345678), Slice.FromFixedU64BE(0x12345678));
				Verify(FdbValue.ToFixed64BigEndian((ulong)0x123456789A), Slice.FromFixedU64BE(0x123456789A));
				Verify(FdbValue.ToFixed64BigEndian((ulong)0x123456789ABC), Slice.FromFixedU64BE(0x123456789ABC));
				Verify(FdbValue.ToFixed64BigEndian((ulong)0x123456789ABCDE), Slice.FromFixedU64BE(0x123456789ABCDE));
				Verify(FdbValue.ToFixed64BigEndian((ulong)0x123456789ABCDEF0), Slice.FromFixedU64BE(0x123456789ABCDEF0));
				Verify(FdbValue.ToFixed64BigEndian(ulong.MaxValue), Slice.FromFixedU64BE(ulong.MaxValue));
			});

		}

	}

}
