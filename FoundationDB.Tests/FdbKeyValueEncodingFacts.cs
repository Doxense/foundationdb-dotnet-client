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

namespace FoundationDB.Client.Tests
{
	using SnowBank.Data.Tuples.Binary;

	[TestFixture]
	[Category("Fdb-Client-InProc")]
	[Parallelizable(ParallelScope.All)]
	public class FdbKeyValueEncodingFacts : FdbSimpleTest
	{

		[Test]
		public void Test_FdbKey_Raw_Encoding()
		{
			{ // Slice
				var rawKey = Slice.FromBytes("Hello, World!"u8);

				var key = FdbKey.Create(rawKey);
				Assert.That(key.ToSlice(), Is.EqualTo(rawKey));
				Assert.That(key.TryGetSpan(out var span), Is.True.WithOutput(span.ToSlice()).EqualTo(rawKey));
				Assert.That(key.TryGetSizeHint(out int size), Is.True.WithOutput(size).EqualTo(rawKey.Count));
			}
			{ // byte[]
				var rawKey = "Hello, World!"u8.ToArray();

				var key = FdbKey.Create(rawKey);
				Assert.That(key.ToSlice().ToArray(), Is.EqualTo(rawKey));
				Assert.That(key.TryGetSpan(out var span), Is.True.WithOutput(span.ToArray()).EqualTo(rawKey));
				Assert.That(key.TryGetSizeHint(out int size), Is.True.WithOutput(size).EqualTo(rawKey.Length));
			}
		}

		[Test]
		public void Test_FdbKey_Tuple_Encoding()
		{
			static void Verify<TTuple>(Slice prefix, TTuple items)
				where TTuple : IVarTuple
			{
				var subspace = new DynamicKeySubspace(prefix, TuPack.Encoding.GetDynamicKeyEncoder(), SubspaceContext.Default);
				var packed = subspace.Pack(items);
				Log($"# {prefix:x} + {items}: -> {packed:x}");
				if (!packed.StartsWith(prefix)) throw new InvalidOperationException();

				var key = FdbKey.Create(subspace, in items);

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
		public void Test_FdbValue_Raw_Encoding()
		{
			{ // Slice
				var rawValue = Slice.FromBytes("Hello, World!"u8);

				var value = FdbValue.Binary.FromBytes(rawValue);
				Assert.That(value.ToSlice(), Is.EqualTo(rawValue));
				Assert.That(value.TryGetSpan(out var span), Is.True.WithOutput(span.ToSlice()).EqualTo(rawValue));
				Assert.That(value.TryGetSizeHint(out int size), Is.True.WithOutput(size).EqualTo(rawValue.Count));
			}
			{ // byte[]
				var rawValue = "Hello, World!"u8.ToArray();

				var value = FdbValue.Binary.FromBytes(rawValue);
				Assert.That(value.ToSlice().ToArray(), Is.EqualTo(rawValue));
				Assert.That(value.TryGetSpan(out var span), Is.True.WithOutput(span.ToArray()).EqualTo(rawValue));
				Assert.That(value.TryGetSizeHint(out int size), Is.True.WithOutput(size).EqualTo(rawValue.Length));
			}
			{ // byte[]
				var ms = new MemoryStream();
				ms.Write("Hello, World!"u8);

				var value = FdbValue.Binary.FromStream(ms);
				Assert.That(value.ToSlice().ToArray(), Is.EqualTo(ms.ToArray()));
				Assert.That(value.TryGetSpan(out var span), Is.True.WithOutput(span.ToArray()).EqualTo(ms.ToArray()));
				Assert.That(value.TryGetSizeHint(out int size), Is.True.WithOutput(size).EqualTo(ms.Length));
			}
		}

		[Test]
		public void Test_FdbValue_Utf8_Encoding()
		{
			{ // string
				var rawValue = "Hello, World!";

				var value = FdbValue.Text.FromUtf8(rawValue);
				Assert.That(value.ToSlice(), Is.EqualTo(rawValue));
				Assert.That(value.TryGetSpan(out var span), Is.True.WithOutput(span.ToSlice()).EqualTo(rawValue));
				Assert.That(value.TryGetSizeHint(out int size), Is.True.WithOutput(size).EqualTo(rawValue.Length));
			}
			{ // ReadOnlyMemory<char>
				ReadOnlyMemory<char> rawValue = "Hello, World!".ToArray();

				var value = FdbValue.Text.FromUtf8(rawValue);
				Assert.That(value.ToSlice().ToArray(), Is.EqualTo(rawValue.ToArray()));
				Assert.That(value.TryGetSpan(out var span), Is.True.WithOutput(span.ToArray()).EqualTo(rawValue));
				Assert.That(value.TryGetSizeHint(out int size), Is.True.WithOutput(size).EqualTo(rawValue.Length));
			}
			{ // ReadOnlySpan<char>
				ReadOnlySpan<char> rawValue = "Hello, World!";

				var value = FdbValue.Text.FromUtf8(rawValue);
				Assert.That(value.ToSlice().ToArray(), Is.EqualTo(rawValue.ToArray()));
				Assert.That(value.TryGetSpan(out var span), Is.True.WithOutput(span.ToArray()).EqualTo(rawValue.ToArray()));
				Assert.That(value.TryGetSizeHint(out int size), Is.True.WithOutput(size).EqualTo(rawValue.Length));
			}
		}

		[Test]
		public void Test_FdbValue_Tuple_Encoding()
		{
			static void Verify<TTuple>(in TTuple tuple)
				where TTuple : IVarTuple
			{
				var expectedPacked = TuPack.Pack(in tuple);
				var expectedUnpacked = TuPack.Unpack(expectedPacked);
				Log($"# ({tuple.GetType().GetFriendlyName()}) {tuple}: -> [{expectedPacked.Count:N0}] {expectedPacked:x} -> {expectedUnpacked}");

				var value = FdbValue.Tuples.Pack(in tuple);
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
				Verify(FdbValue.FixedSize.LittleEndian.FromInt32(0x12), Slice.FromFixed32(0x12));
				Verify(FdbValue.FixedSize.LittleEndian.FromInt32(0x1234), Slice.FromFixed32(0x1234));
				Verify(FdbValue.FixedSize.LittleEndian.FromInt32(0x123456), Slice.FromFixed32(0x123456));
				Verify(FdbValue.FixedSize.LittleEndian.FromInt32(0x12345678), Slice.FromFixed32(0x12345678));
				Verify(FdbValue.FixedSize.LittleEndian.FromInt32(-1), Slice.FromFixed32(-1));
				Verify(FdbValue.FixedSize.LittleEndian.FromInt32(-123456), Slice.FromFixed32(-123456));
				Verify(FdbValue.FixedSize.LittleEndian.FromInt32(int.MinValue), Slice.FromFixed32(int.MinValue));

				// UInt32
				Verify(FdbValue.FixedSize.LittleEndian.FromUInt32(0), Slice.FromFixedU32(0));
				Verify(FdbValue.FixedSize.LittleEndian.FromUInt32(0x12), Slice.FromFixedU32(0x12));
				Verify(FdbValue.FixedSize.LittleEndian.FromUInt32(0x1234), Slice.FromFixedU32(0x1234));
				Verify(FdbValue.FixedSize.LittleEndian.FromUInt32(0x123456), Slice.FromFixedU32(0x123456));
				Verify(FdbValue.FixedSize.LittleEndian.FromUInt32(0x12345678), Slice.FromFixedU32(0x12345678));

				// Int64
				Verify(FdbValue.FixedSize.LittleEndian.FromInt64(0), Slice.FromFixed64(0));
				Verify(FdbValue.FixedSize.LittleEndian.FromInt64(0x12), Slice.FromFixed64(0x12));
				Verify(FdbValue.FixedSize.LittleEndian.FromInt64(0x1234), Slice.FromFixed64(0x1234));
				Verify(FdbValue.FixedSize.LittleEndian.FromInt64(0x123456), Slice.FromFixed64(0x123456));
				Verify(FdbValue.FixedSize.LittleEndian.FromInt64(0x12345678), Slice.FromFixed64(0x12345678));
				Verify(FdbValue.FixedSize.LittleEndian.FromInt64(0x123456789A), Slice.FromFixed64(0x123456789A));
				Verify(FdbValue.FixedSize.LittleEndian.FromInt64(0x123456789ABC), Slice.FromFixed64(0x123456789ABC));
				Verify(FdbValue.FixedSize.LittleEndian.FromInt64(0x123456789ABCDE), Slice.FromFixed64(0x123456789ABCDE));
				Verify(FdbValue.FixedSize.LittleEndian.FromInt64(0x123456789ABCDEF0), Slice.FromFixed64(0x123456789ABCDEF0));
				Verify(FdbValue.FixedSize.LittleEndian.FromInt64(-1), Slice.FromFixed64(-1));
				Verify(FdbValue.FixedSize.LittleEndian.FromInt64(-123456), Slice.FromFixed64(-123456));
				Verify(FdbValue.FixedSize.LittleEndian.FromInt64(int.MinValue), Slice.FromFixed64(int.MinValue));
				Verify(FdbValue.FixedSize.LittleEndian.FromInt64(long.MinValue), Slice.FromFixed64(long.MinValue));

				// UInt64
				Verify(FdbValue.FixedSize.LittleEndian.FromUInt64(0), Slice.FromFixedU64(0));
				Verify(FdbValue.FixedSize.LittleEndian.FromUInt64(0x12), Slice.FromFixedU64(0x12));
				Verify(FdbValue.FixedSize.LittleEndian.FromUInt64(0x1234), Slice.FromFixedU64(0x1234));
				Verify(FdbValue.FixedSize.LittleEndian.FromUInt64(0x123456), Slice.FromFixedU64(0x123456));
				Verify(FdbValue.FixedSize.LittleEndian.FromUInt64(0x12345678), Slice.FromFixedU64(0x12345678));
				Verify(FdbValue.FixedSize.LittleEndian.FromUInt64(0x123456789A), Slice.FromFixedU64(0x123456789A));
				Verify(FdbValue.FixedSize.LittleEndian.FromUInt64(0x123456789ABC), Slice.FromFixedU64(0x123456789ABC));
				Verify(FdbValue.FixedSize.LittleEndian.FromUInt64(0x123456789ABCDE), Slice.FromFixedU64(0x123456789ABCDE));
				Verify(FdbValue.FixedSize.LittleEndian.FromUInt64(0x123456789ABCDEF0), Slice.FromFixedU64(0x123456789ABCDEF0));
				Verify(FdbValue.FixedSize.LittleEndian.FromUInt64(ulong.MaxValue), Slice.FromFixedU64(ulong.MaxValue));
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
				Verify(FdbValue.FixedSize.BigEndian.FromInt32(0x12), Slice.FromFixed32BE(0x12));
				Verify(FdbValue.FixedSize.BigEndian.FromInt32(0x1234), Slice.FromFixed32BE(0x1234));
				Verify(FdbValue.FixedSize.BigEndian.FromInt32(0x123456), Slice.FromFixed32BE(0x123456));
				Verify(FdbValue.FixedSize.BigEndian.FromInt32(0x12345678), Slice.FromFixed32BE(0x12345678));
				Verify(FdbValue.FixedSize.BigEndian.FromInt32(-1), Slice.FromFixed32BE(-1));
				Verify(FdbValue.FixedSize.BigEndian.FromInt32(-123456), Slice.FromFixed32BE(-123456));
				Verify(FdbValue.FixedSize.BigEndian.FromInt32(int.MinValue), Slice.FromFixed32BE(int.MinValue));

				// UInt32
				Verify(FdbValue.FixedSize.BigEndian.FromUInt32(0), Slice.FromFixedU32BE(0));
				Verify(FdbValue.FixedSize.BigEndian.FromUInt32(0x12), Slice.FromFixedU32BE(0x12));
				Verify(FdbValue.FixedSize.BigEndian.FromUInt32(0x1234), Slice.FromFixedU32BE(0x1234));
				Verify(FdbValue.FixedSize.BigEndian.FromUInt32(0x123456), Slice.FromFixedU32BE(0x123456));
				Verify(FdbValue.FixedSize.BigEndian.FromUInt32(0x12345678), Slice.FromFixedU32BE(0x12345678));

				// Int64
				Verify(FdbValue.FixedSize.BigEndian.FromInt64(0), Slice.FromFixed64BE(0));
				Verify(FdbValue.FixedSize.BigEndian.FromInt64(0x12), Slice.FromFixed64BE(0x12));
				Verify(FdbValue.FixedSize.BigEndian.FromInt64(0x1234), Slice.FromFixed64BE(0x1234));
				Verify(FdbValue.FixedSize.BigEndian.FromInt64(0x123456), Slice.FromFixed64BE(0x123456));
				Verify(FdbValue.FixedSize.BigEndian.FromInt64(0x12345678), Slice.FromFixed64BE(0x12345678));
				Verify(FdbValue.FixedSize.BigEndian.FromInt64(0x123456789A), Slice.FromFixed64BE(0x123456789A));
				Verify(FdbValue.FixedSize.BigEndian.FromInt64(0x123456789ABC), Slice.FromFixed64BE(0x123456789ABC));
				Verify(FdbValue.FixedSize.BigEndian.FromInt64(0x123456789ABCDE), Slice.FromFixed64BE(0x123456789ABCDE));
				Verify(FdbValue.FixedSize.BigEndian.FromInt64(0x123456789ABCDEF0), Slice.FromFixed64BE(0x123456789ABCDEF0));
				Verify(FdbValue.FixedSize.BigEndian.FromInt64(-1), Slice.FromFixed64BE(-1));
				Verify(FdbValue.FixedSize.BigEndian.FromInt64(-123456), Slice.FromFixed64BE(-123456));
				Verify(FdbValue.FixedSize.BigEndian.FromInt64(int.MinValue), Slice.FromFixed64BE(int.MinValue));
				Verify(FdbValue.FixedSize.BigEndian.FromInt64(long.MinValue), Slice.FromFixed64BE(long.MinValue));

				// UInt64
				Verify(FdbValue.FixedSize.BigEndian.FromUInt64(0), Slice.FromFixedU64BE(0));
				Verify(FdbValue.FixedSize.BigEndian.FromUInt64(0x12), Slice.FromFixedU64BE(0x12));
				Verify(FdbValue.FixedSize.BigEndian.FromUInt64(0x1234), Slice.FromFixedU64BE(0x1234));
				Verify(FdbValue.FixedSize.BigEndian.FromUInt64(0x123456), Slice.FromFixedU64BE(0x123456));
				Verify(FdbValue.FixedSize.BigEndian.FromUInt64(0x12345678), Slice.FromFixedU64BE(0x12345678));
				Verify(FdbValue.FixedSize.BigEndian.FromUInt64(0x123456789A), Slice.FromFixedU64BE(0x123456789A));
				Verify(FdbValue.FixedSize.BigEndian.FromUInt64(0x123456789ABC), Slice.FromFixedU64BE(0x123456789ABC));
				Verify(FdbValue.FixedSize.BigEndian.FromUInt64(0x123456789ABCDE), Slice.FromFixedU64BE(0x123456789ABCDE));
				Verify(FdbValue.FixedSize.BigEndian.FromUInt64(0x123456789ABCDEF0), Slice.FromFixedU64BE(0x123456789ABCDEF0));
				Verify(FdbValue.FixedSize.BigEndian.FromUInt64(ulong.MaxValue), Slice.FromFixedU64BE(ulong.MaxValue));
			});

		}

	}

}
