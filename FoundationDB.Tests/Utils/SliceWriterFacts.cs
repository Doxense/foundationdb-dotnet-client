#region BSD License
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

namespace Doxense.Memory.Tests
{
	using System;
	using System.Text;
	using Doxense.Memory;
	using FoundationDB.Client.Tests;
	using NUnit.Framework;

	[TestFixture]
	public class SliceWriterFacts : FdbTest
	{

		private static string Clean(string value)
		{
			var sb = new StringBuilder(value.Length + 8);
			foreach (var c in value)
			{
				if (c < ' ') sb.Append("\\x").Append(((int)c).ToString("x2")); else sb.Append(c);
			}
			return sb.ToString();
		}

		private delegate void TestHandler<in T>(ref SliceWriter writer, T value);

		private static void PerformWriterTest<T>(TestHandler<T> action, T value, string expectedResult, string message = null)
		{
			var writer = default(SliceWriter);
			action(ref writer, value);

			Assert.That(writer.ToSlice().ToHexaString(' '), Is.EqualTo(expectedResult), "Value {0} ({1}) was not properly packed. {2}", value == null ? "<null>" : value is string ? Clean(value as string) : value.ToString(), (value == null ? "null" : value.GetType().Name), message);
		}

		[Test]
		public void Test_Empty_Writer()
		{
			var writer = default(SliceWriter);
			Assert.That(writer.Position, Is.EqualTo(0));
			Assert.That(writer.HasData, Is.False);
			Assert.That(writer.Buffer, Is.Null);
			Assert.That(writer.ToSlice(), Is.EqualTo(Slice.Empty));
		}

		[Test]
		public void Test_WriteBytes()
		{
			{
				TestHandler<byte[]> test = (ref SliceWriter writer, byte[] value) => writer.WriteBytes(value);

				PerformWriterTest(test, null, "");
				PerformWriterTest(test, new byte[0], "");
				PerformWriterTest(test, new byte[] {66}, "42");
				PerformWriterTest(test, new byte[] {65, 66, 67}, "41 42 43");
			}
			{
				TestHandler<Slice> test = (ref SliceWriter writer, Slice value) => writer.WriteBytes(value);

				PerformWriterTest(test, Slice.Nil, "");
				PerformWriterTest(test, Slice.Empty, "");
				PerformWriterTest(test, Slice.FromByte(66), "42");
				PerformWriterTest(test, new byte[] { 65, 66, 67 }.AsSlice(), "41 42 43");
				PerformWriterTest(test, new byte[] { 65, 66, 67, 68, 69 }.AsSlice(1, 3), "42 43 44");
			}
			{
				TestHandler<MutableSlice> test = (ref SliceWriter writer, MutableSlice value) => writer.WriteBytes(value);

				PerformWriterTest(test, MutableSlice.Nil, "");
				PerformWriterTest(test, MutableSlice.Empty, "");
				PerformWriterTest(test, MutableSlice.FromByte(66), "42");
				PerformWriterTest(test, new byte[] { 65, 66, 67 }.AsMutableSlice(), "41 42 43");
				PerformWriterTest(test, new byte[] { 65, 66, 67, 68, 69 }.AsMutableSlice(1, 3), "42 43 44");
			}
			{
				TestHandler<Memory<byte>> test = (ref SliceWriter writer, Memory<byte> value) => writer.WriteBytes(value.Span);

				PerformWriterTest(test, default, "");
				PerformWriterTest(test, new byte[] { 66 }.AsMemory(), "42");
				PerformWriterTest(test, new byte[] { 65, 66, 67 }.AsMemory(), "41 42 43");
				PerformWriterTest(test, new byte[] { 65, 66, 67, 68, 69 }.AsMemory(1, 3), "42 43 44");
			}
		}

		[Test]
		public void Test_WriteByte_Unsigned()
		{
			TestHandler<byte> test = (ref SliceWriter writer, byte value) => writer.WriteByte(value);

			PerformWriterTest<byte>(test, 0, "00");
			PerformWriterTest<byte>(test, 1, "01");
			PerformWriterTest<byte>(test, 42, "2A");
			PerformWriterTest<byte>(test, 255, "FF");
		}

		[Test]
		public void Test_WriteByte_Signed()
		{
			TestHandler<sbyte> test = (ref SliceWriter writer, sbyte value) => writer.WriteByte(value);

			PerformWriterTest<sbyte>(test, 0, "00");
			PerformWriterTest<sbyte>(test, 1, "01");
			PerformWriterTest<sbyte>(test, 42, "2A");
			PerformWriterTest<sbyte>(test, sbyte.MaxValue, "7F");
			PerformWriterTest<sbyte>(test, -1, "FF");
			PerformWriterTest<sbyte>(test, sbyte.MinValue, "80");
		}

		[Test]
		public void Test_WriteFixed16_Unsigned()
		{
			TestHandler<ushort> test = (ref SliceWriter writer, ushort value) => writer.WriteFixed16(value);

			PerformWriterTest<ushort>(test, 0, "00 00");
			PerformWriterTest<ushort>(test, 1, "01 00");
			PerformWriterTest<ushort>(test, 0x12, "12 00");
			PerformWriterTest<ushort>(test, 0x1234, "34 12");
			PerformWriterTest<ushort>(test, ushort.MaxValue, "FF FF");
		}

		[Test]
		public void Test_WriteFixed16_Signed()
		{
			TestHandler<short> test = (ref SliceWriter writer, short value) => writer.WriteFixed16(value);

			PerformWriterTest<short>(test, 0, "00 00");
			PerformWriterTest<short>(test, 1, "01 00");
			PerformWriterTest<short>(test, 0x12, "12 00");
			PerformWriterTest<short>(test, 0x1234, "34 12");
			PerformWriterTest<short>(test, short.MaxValue, "FF 7F");
			PerformWriterTest<short>(test, -1, "FF FF");
			PerformWriterTest<short>(test, short.MinValue, "00 80");
		}

		[Test]
		public void Test_WriteFixed16BE_Unsigned()
		{
			TestHandler<ushort> test = (ref SliceWriter writer, ushort value) => writer.WriteFixed16BE(value);

			PerformWriterTest<ushort>(test, 0, "00 00");
			PerformWriterTest<ushort>(test, 1, "00 01");
			PerformWriterTest<ushort>(test, 0x12, "00 12");
			PerformWriterTest<ushort>(test, 0x1234, "12 34");
			PerformWriterTest<ushort>(test, ushort.MaxValue, "FF FF");
		}

		[Test]
		public void Test_WriteFixed16BE_Signed()
		{
			TestHandler<short> test = (ref SliceWriter writer, short value) => writer.WriteFixed16BE(value);

			PerformWriterTest<short>(test, 0, "00 00");
			PerformWriterTest<short>(test, 1, "00 01");
			PerformWriterTest<short>(test, 0x12, "00 12");
			PerformWriterTest<short>(test, 0x1234, "12 34");
			PerformWriterTest<short>(test, short.MaxValue, "7F FF");
			PerformWriterTest<short>(test, -1, "FF FF");
			PerformWriterTest<short>(test, short.MinValue, "80 00");
		}

		[Test]
		public void Test_WriteFixed32_Unsigned()
		{
			TestHandler<uint> test = (ref SliceWriter writer, uint value) => writer.WriteFixed32(value);

			PerformWriterTest<uint>(test, 0U, "00 00 00 00");
			PerformWriterTest<uint>(test, 1U, "01 00 00 00");
			PerformWriterTest<uint>(test, 0x12U, "12 00 00 00");
			PerformWriterTest<uint>(test, 0x1234U, "34 12 00 00");
			PerformWriterTest<uint>(test, ushort.MaxValue, "FF FF 00 00");
			PerformWriterTest<uint>(test, 0x123456U, "56 34 12 00");
			PerformWriterTest<uint>(test, 0xDEADBEEF, "EF BE AD DE");
			PerformWriterTest<uint>(test, uint.MaxValue, "FF FF FF FF");
		}

		[Test]
		public void Test_WriteFixed32_Signed()
		{
			TestHandler<int> test = (ref SliceWriter writer, int value) => writer.WriteFixed32(value);

			PerformWriterTest<int>(test, 0, "00 00 00 00");
			PerformWriterTest<int>(test, 1, "01 00 00 00");
			PerformWriterTest<int>(test, 0x12, "12 00 00 00");
			PerformWriterTest<int>(test, 0x1234, "34 12 00 00");
			PerformWriterTest<int>(test, short.MaxValue, "FF 7F 00 00");
			PerformWriterTest<int>(test, ushort.MaxValue, "FF FF 00 00");
			PerformWriterTest<int>(test, 0x123456, "56 34 12 00");
			PerformWriterTest<int>(test, unchecked((int)0xDEADBEEF), "EF BE AD DE");
			PerformWriterTest<int>(test, int.MaxValue, "FF FF FF 7F");
			PerformWriterTest<int>(test, -1, "FF FF FF FF");
			PerformWriterTest<int>(test, short.MinValue, "00 80 FF FF");
			PerformWriterTest<int>(test, int.MinValue, "00 00 00 80");

		}

		[Test]
		public void Test_WriteFixed32BE_Unsigned()
		{
			TestHandler<uint> test = (ref SliceWriter writer, uint value) => writer.WriteFixed32BE(value);

			PerformWriterTest<uint>(test, 0U, "00 00 00 00");
			PerformWriterTest<uint>(test, 1U, "00 00 00 01");
			PerformWriterTest<uint>(test, 0x12U, "00 00 00 12");
			PerformWriterTest<uint>(test, 0x1234U, "00 00 12 34");
			PerformWriterTest<uint>(test, ushort.MaxValue, "00 00 FF FF");
			PerformWriterTest<uint>(test, 0x123456U, "00 12 34 56");
			PerformWriterTest<uint>(test, 0xDEADBEEF, "DE AD BE EF");
			PerformWriterTest<uint>(test, uint.MaxValue, "FF FF FF FF");
		}

		[Test]
		public void Test_WriteFixed32BE_Signed()
		{
			TestHandler<int> test = (ref SliceWriter writer, int value) => writer.WriteFixed32BE(value);

			PerformWriterTest<int>(test, 0, "00 00 00 00");
			PerformWriterTest<int>(test, 1, "00 00 00 01");
			PerformWriterTest<int>(test, 0x12, "00 00 00 12");
			PerformWriterTest<int>(test, 0x1234, "00 00 12 34");
			PerformWriterTest<int>(test, short.MaxValue, "00 00 7F FF");
			PerformWriterTest<int>(test, ushort.MaxValue, "00 00 FF FF");
			PerformWriterTest<int>(test, 0x123456, "00 12 34 56");
			PerformWriterTest<int>(test, unchecked((int)0xDEADBEEF), "DE AD BE EF");
			PerformWriterTest<int>(test, int.MaxValue, "7F FF FF FF");
			PerformWriterTest<int>(test, -1, "FF FF FF FF");
			PerformWriterTest<int>(test, short.MinValue, "FF FF 80 00");
			PerformWriterTest<int>(test, int.MinValue, "80 00 00 00");

		}

		[Test]
		public void Test_WriteFixed64_Unsigned()
		{
			TestHandler<ulong> test = (ref SliceWriter writer, ulong value) => writer.WriteFixed64(value);

			PerformWriterTest<ulong>(test, 0UL, "00 00 00 00 00 00 00 00");
			PerformWriterTest<ulong>(test, 1UL, "01 00 00 00 00 00 00 00");
			PerformWriterTest<ulong>(test, 0x12UL, "12 00 00 00 00 00 00 00");
			PerformWriterTest<ulong>(test, 0x1234UL, "34 12 00 00 00 00 00 00");
			PerformWriterTest<ulong>(test, ushort.MaxValue, "FF FF 00 00 00 00 00 00");
			PerformWriterTest<ulong>(test, 0x123456UL, "56 34 12 00 00 00 00 00");
			PerformWriterTest<ulong>(test, 0x12345678UL, "78 56 34 12 00 00 00 00");
			PerformWriterTest<ulong>(test, uint.MaxValue, "FF FF FF FF 00 00 00 00");
			PerformWriterTest<ulong>(test, 0x123456789AUL, "9A 78 56 34 12 00 00 00");
			PerformWriterTest<ulong>(test, 0x123456789ABCUL, "BC 9A 78 56 34 12 00 00");
			PerformWriterTest<ulong>(test, 0x123456789ABCDEUL, "DE BC 9A 78 56 34 12 00");
			PerformWriterTest<ulong>(test, 0xBADC0FFEE0DDF00DUL, "0D F0 DD E0 FE 0F DC BA");
			PerformWriterTest<ulong>(test, ulong.MaxValue, "FF FF FF FF FF FF FF FF");
		}

		[Test]
		public void Test_WriteFixed64_Signed()
		{
			TestHandler<long> test = (ref SliceWriter writer, long value) => writer.WriteFixed64(value);

			PerformWriterTest<long>(test, 0L, "00 00 00 00 00 00 00 00");
			PerformWriterTest<long>(test, 1L, "01 00 00 00 00 00 00 00");
			PerformWriterTest<long>(test, 0x12L, "12 00 00 00 00 00 00 00");
			PerformWriterTest<long>(test, 0x1234L, "34 12 00 00 00 00 00 00");
			PerformWriterTest<long>(test, short.MaxValue, "FF 7F 00 00 00 00 00 00");
			PerformWriterTest<long>(test, ushort.MaxValue, "FF FF 00 00 00 00 00 00");
			PerformWriterTest<long>(test, 0x123456L, "56 34 12 00 00 00 00 00");
			PerformWriterTest<long>(test, 0x12345678L, "78 56 34 12 00 00 00 00");
			PerformWriterTest<long>(test, int.MaxValue, "FF FF FF 7F 00 00 00 00");
			PerformWriterTest<long>(test, uint.MaxValue, "FF FF FF FF 00 00 00 00");
			PerformWriterTest<long>(test, 0x123456789AL, "9A 78 56 34 12 00 00 00");
			PerformWriterTest<long>(test, 0x123456789ABCL, "BC 9A 78 56 34 12 00 00");
			PerformWriterTest<long>(test, 0x123456789ABCDEL, "DE BC 9A 78 56 34 12 00");
			PerformWriterTest<long>(test, unchecked((long) 0xBADC0FFEE0DDF00D), "0D F0 DD E0 FE 0F DC BA");
			PerformWriterTest<long>(test, long.MaxValue, "FF FF FF FF FF FF FF 7F");
			PerformWriterTest<long>(test, -1L, "FF FF FF FF FF FF FF FF");
			PerformWriterTest<long>(test, short.MinValue, "00 80 FF FF FF FF FF FF");
			PerformWriterTest<long>(test, int.MinValue, "00 00 00 80 FF FF FF FF");
			PerformWriterTest<long>(test, long.MinValue, "00 00 00 00 00 00 00 80");
		}

		[Test]
		public void Test_WriteFixed64BE_Unsigned()
		{
			TestHandler<ulong> test = (ref SliceWriter writer, ulong value) => writer.WriteFixed64BE(value);

			PerformWriterTest<ulong>(test, 0UL, "00 00 00 00 00 00 00 00");
			PerformWriterTest<ulong>(test, 1UL, "00 00 00 00 00 00 00 01");
			PerformWriterTest<ulong>(test, 0x12UL, "00 00 00 00 00 00 00 12");
			PerformWriterTest<ulong>(test, 0x1234UL, "00 00 00 00 00 00 12 34");
			PerformWriterTest<ulong>(test, ushort.MaxValue, "00 00 00 00 00 00 FF FF");
			PerformWriterTest<ulong>(test, 0x123456UL, "00 00 00 00 00 12 34 56");
			PerformWriterTest<ulong>(test, 0x12345678UL, "00 00 00 00 12 34 56 78");
			PerformWriterTest<ulong>(test, uint.MaxValue, "00 00 00 00 FF FF FF FF");
			PerformWriterTest<ulong>(test, 0x123456789AUL, "00 00 00 12 34 56 78 9A");
			PerformWriterTest<ulong>(test, 0x123456789ABCUL, "00 00 12 34 56 78 9A BC");
			PerformWriterTest<ulong>(test, 0x123456789ABCDEUL, "00 12 34 56 78 9A BC DE");
			PerformWriterTest<ulong>(test, 0xBADC0FFEE0DDF00DUL, "BA DC 0F FE E0 DD F0 0D");
			PerformWriterTest<ulong>(test, ulong.MaxValue, "FF FF FF FF FF FF FF FF");
		}

		[Test]
		public void Test_WriteFixed64BE_Signed()
		{
			TestHandler<long> test = (ref SliceWriter writer, long value) => writer.WriteFixed64BE(value);

			PerformWriterTest<long>(test, 0L, "00 00 00 00 00 00 00 00");
			PerformWriterTest<long>(test, 1L, "00 00 00 00 00 00 00 01");
			PerformWriterTest<long>(test, 0x12L, "00 00 00 00 00 00 00 12");
			PerformWriterTest<long>(test, 0x1234L, "00 00 00 00 00 00 12 34");
			PerformWriterTest<long>(test, short.MaxValue, "00 00 00 00 00 00 7F FF");
			PerformWriterTest<long>(test, ushort.MaxValue, "00 00 00 00 00 00 FF FF");
			PerformWriterTest<long>(test, 0x123456L, "00 00 00 00 00 12 34 56");
			PerformWriterTest<long>(test, 0x12345678L, "00 00 00 00 12 34 56 78");
			PerformWriterTest<long>(test, int.MaxValue, "00 00 00 00 7F FF FF FF");
			PerformWriterTest<long>(test, uint.MaxValue, "00 00 00 00 FF FF FF FF");
			PerformWriterTest<long>(test, 0x123456789AL, "00 00 00 12 34 56 78 9A");
			PerformWriterTest<long>(test, 0x123456789ABCL, "00 00 12 34 56 78 9A BC");
			PerformWriterTest<long>(test, 0x123456789ABCDEL, "00 12 34 56 78 9A BC DE");
			PerformWriterTest<long>(test, unchecked((long)0xBADC0FFEE0DDF00D), "BA DC 0F FE E0 DD F0 0D");
			PerformWriterTest<long>(test, long.MaxValue, "7F FF FF FF FF FF FF FF");
			PerformWriterTest<long>(test, -1L, "FF FF FF FF FF FF FF FF");
			PerformWriterTest<long>(test, short.MinValue, "FF FF FF FF FF FF 80 00");
			PerformWriterTest<long>(test, int.MinValue, "FF FF FF FF 80 00 00 00");
			PerformWriterTest<long>(test, long.MinValue, "80 00 00 00 00 00 00 00");
		}

		[Test]
		public void Test_WriteVarint32()
		{
			TestHandler<uint> test = (ref SliceWriter writer, uint value) => writer.WriteVarInt32(value);

			PerformWriterTest(test, 0U, "00");
			PerformWriterTest(test, 1U, "01");
			PerformWriterTest(test, 127U, "7F");
			PerformWriterTest(test, 128U, "80 01");
			PerformWriterTest(test, 255U, "FF 01");
			PerformWriterTest(test, 256U, "80 02");
			PerformWriterTest(test, 16383U, "FF 7F");
			PerformWriterTest(test, 16384U, "80 80 01");
			PerformWriterTest(test, 2097151U, "FF FF 7F");
			PerformWriterTest(test, 2097152U, "80 80 80 01");
			PerformWriterTest(test, 268435455U, "FF FF FF 7F");
			PerformWriterTest(test, 268435456U, "80 80 80 80 01");
			PerformWriterTest(test, uint.MaxValue, "FF FF FF FF 0F");
		}

		[Test]
		public void Test_WriteVarint64()
		{
			TestHandler<ulong> test = (ref SliceWriter writer, ulong value) => writer.WriteVarInt64(value);

			PerformWriterTest(test, 0UL, "00");
			PerformWriterTest(test, 1UL, "01");
			PerformWriterTest(test, 127UL, "7F");
			PerformWriterTest(test, 128UL, "80 01");
			PerformWriterTest(test, 255UL, "FF 01");
			PerformWriterTest(test, 256UL, "80 02");
			PerformWriterTest(test, 16383UL, "FF 7F");
			PerformWriterTest(test, 16384UL, "80 80 01");
			PerformWriterTest(test, 2097151UL, "FF FF 7F");
			PerformWriterTest(test, 2097152UL, "80 80 80 01");
			PerformWriterTest(test, 268435455UL, "FF FF FF 7F");
			PerformWriterTest(test, 268435456UL, "80 80 80 80 01");
			PerformWriterTest(test, 34359738367UL, "FF FF FF FF 7F");
			PerformWriterTest(test, 34359738368UL, "80 80 80 80 80 01");
			PerformWriterTest(test, 4398046511103UL, "FF FF FF FF FF 7F");
			PerformWriterTest(test, 4398046511104UL, "80 80 80 80 80 80 01");
			PerformWriterTest(test, 562949953421311UL, "FF FF FF FF FF FF 7F");
			PerformWriterTest(test, 562949953421312UL, "80 80 80 80 80 80 80 01");
			PerformWriterTest(test, 72057594037927935UL, "FF FF FF FF FF FF FF 7F");
			PerformWriterTest(test, 72057594037927936UL, "80 80 80 80 80 80 80 80 01");
			PerformWriterTest(test, 9223372036854775807UL, "FF FF FF FF FF FF FF FF 7F");
			PerformWriterTest(test, 9223372036854775808UL, "80 80 80 80 80 80 80 80 80 01");
			PerformWriterTest(test, ulong.MaxValue, "FF FF FF FF FF FF FF FF FF 01");
		}

		[Test]
		public void Test_WriteVarBytes()
		{
			TestHandler<Slice> test = (ref SliceWriter writer, Slice value) => writer.WriteVarBytes(value);

			PerformWriterTest(test, Slice.Nil, "00");
			PerformWriterTest(test, Slice.Empty, "00");
			PerformWriterTest(test, Slice.FromByte(42), "01 2A");
			PerformWriterTest(test, Slice.FromByte(255), "01 FF");
			PerformWriterTest(test, Slice.FromString("ABC"), "03 41 42 43");
			PerformWriterTest(test, Slice.FromFixedU32(0xDEADBEEF), "04 EF BE AD DE");
		}

		[Test]
		public void Test_WriteBase10_Signed()
		{
			TestHandler<int> test = (ref SliceWriter writer, int value) => writer.WriteBase10(value);

			// positive numbers
			PerformWriterTest(test, 0, "30");
			PerformWriterTest(test, 1, "31");
			PerformWriterTest(test, 9, "39");
			PerformWriterTest(test, 10, "31 30");
			PerformWriterTest(test, 42, "34 32");
			PerformWriterTest(test, 99, "39 39");
			PerformWriterTest(test, 100, "31 30 30");
			PerformWriterTest(test, 123, "31 32 33");
			PerformWriterTest(test, 999, "39 39 39");
			PerformWriterTest(test, 1000, "31 30 30 30");
			PerformWriterTest(test, 1234, "31 32 33 34");
			PerformWriterTest(test, 9999, "39 39 39 39");
			PerformWriterTest(test, 10000, "31 30 30 30 30");
			PerformWriterTest(test, 12345, "31 32 33 34 35");
			PerformWriterTest(test, 99999, "39 39 39 39 39");
			PerformWriterTest(test, 100000, "31 30 30 30 30 30");
			PerformWriterTest(test, 123456, "31 32 33 34 35 36");
			PerformWriterTest(test, 999999, "39 39 39 39 39 39");
			PerformWriterTest(test, 1000000, "31 30 30 30 30 30 30");
			PerformWriterTest(test, 1234567, "31 32 33 34 35 36 37");
			PerformWriterTest(test, 9999999, "39 39 39 39 39 39 39");
			PerformWriterTest(test, 10000000, "31 30 30 30 30 30 30 30");
			PerformWriterTest(test, 12345678, "31 32 33 34 35 36 37 38");
			PerformWriterTest(test, 99999999, "39 39 39 39 39 39 39 39");
			PerformWriterTest(test, 100000000, "31 30 30 30 30 30 30 30 30");
			PerformWriterTest(test, 123456789, "31 32 33 34 35 36 37 38 39");
			PerformWriterTest(test, 999999999, "39 39 39 39 39 39 39 39 39");
			PerformWriterTest(test, int.MaxValue, "32 31 34 37 34 38 33 36 34 37");

			// negative numbers
			PerformWriterTest(test, -1, "2D 31");
			PerformWriterTest(test, -9, "2D 39");
			PerformWriterTest(test, -10, "2D 31 30");
			PerformWriterTest(test, -42, "2D 34 32");
			PerformWriterTest(test, -99, "2D 39 39");
			PerformWriterTest(test, -100, "2D 31 30 30");
			PerformWriterTest(test, -123, "2D 31 32 33");
			PerformWriterTest(test, -999, "2D 39 39 39");
			PerformWriterTest(test, -1000, "2D 31 30 30 30");
			PerformWriterTest(test, -1234, "2D 31 32 33 34");
			PerformWriterTest(test, -9999, "2D 39 39 39 39");
			PerformWriterTest(test, -10000, "2D 31 30 30 30 30");
			PerformWriterTest(test, -12345, "2D 31 32 33 34 35");
			PerformWriterTest(test, -99999, "2D 39 39 39 39 39");
			PerformWriterTest(test, -100000, "2D 31 30 30 30 30 30");
			PerformWriterTest(test, -123456, "2D 31 32 33 34 35 36");
			PerformWriterTest(test, -999999, "2D 39 39 39 39 39 39");
			PerformWriterTest(test, -1000000, "2D 31 30 30 30 30 30 30");
			PerformWriterTest(test, -1234567, "2D 31 32 33 34 35 36 37");
			PerformWriterTest(test, -9999999, "2D 39 39 39 39 39 39 39");
			PerformWriterTest(test, -10000000, "2D 31 30 30 30 30 30 30 30");
			PerformWriterTest(test, -12345678, "2D 31 32 33 34 35 36 37 38");
			PerformWriterTest(test, -99999999, "2D 39 39 39 39 39 39 39 39");
			PerformWriterTest(test, -100000000, "2D 31 30 30 30 30 30 30 30 30");
			PerformWriterTest(test, -123456789, "2D 31 32 33 34 35 36 37 38 39");
			PerformWriterTest(test, -999999999, "2D 39 39 39 39 39 39 39 39 39");
			PerformWriterTest(test, int.MinValue, "2D 32 31 34 37 34 38 33 36 34 38");
		}

		[Test]
		public void Test_WriteBase10_Unsigned()
		{
			TestHandler<uint> test = (ref SliceWriter writer, uint value) => writer.WriteBase10(value);

			// positive numbers
			PerformWriterTest<uint>(test, 0, "30");
			PerformWriterTest<uint>(test, 1, "31");
			PerformWriterTest<uint>(test, 9, "39");
			PerformWriterTest<uint>(test, 10, "31 30");
			PerformWriterTest<uint>(test, 42, "34 32");
			PerformWriterTest<uint>(test, 99, "39 39");
			PerformWriterTest<uint>(test, 100, "31 30 30");
			PerformWriterTest<uint>(test, 123, "31 32 33");
			PerformWriterTest<uint>(test, 999, "39 39 39");
			PerformWriterTest<uint>(test, 1000, "31 30 30 30");
			PerformWriterTest<uint>(test, 1234, "31 32 33 34");
			PerformWriterTest<uint>(test, 9999, "39 39 39 39");
			PerformWriterTest<uint>(test, 10000, "31 30 30 30 30");
			PerformWriterTest<uint>(test, 12345, "31 32 33 34 35");
			PerformWriterTest<uint>(test, 99999, "39 39 39 39 39");
			PerformWriterTest<uint>(test, 100000, "31 30 30 30 30 30");
			PerformWriterTest<uint>(test, 123456, "31 32 33 34 35 36");
			PerformWriterTest<uint>(test, 999999, "39 39 39 39 39 39");
			PerformWriterTest<uint>(test, 1000000, "31 30 30 30 30 30 30");
			PerformWriterTest<uint>(test, 1234567, "31 32 33 34 35 36 37");
			PerformWriterTest<uint>(test, 9999999, "39 39 39 39 39 39 39");
			PerformWriterTest<uint>(test, 10000000, "31 30 30 30 30 30 30 30");
			PerformWriterTest<uint>(test, 12345678, "31 32 33 34 35 36 37 38");
			PerformWriterTest<uint>(test, 99999999, "39 39 39 39 39 39 39 39");
			PerformWriterTest<uint>(test, 100000000, "31 30 30 30 30 30 30 30 30");
			PerformWriterTest<uint>(test, 123456789, "31 32 33 34 35 36 37 38 39");
			PerformWriterTest<uint>(test, 999999999, "39 39 39 39 39 39 39 39 39");
			PerformWriterTest<uint>(test, int.MaxValue, "32 31 34 37 34 38 33 36 34 37");
			PerformWriterTest<uint>(test, uint.MaxValue, "34 32 39 34 39 36 37 32 39 35");
		}

		[Test]
		public void Test_Indexer()
		{
			var slice = new SliceWriter();
			slice.WriteFixed64(0xBADC0FFEE0DDF00DUL);

			Assert.That(slice[0], Is.EqualTo(0x0D));
			Assert.That(slice[1], Is.EqualTo(0xF0));
			Assert.That(slice[2], Is.EqualTo(0xDD));
			Assert.That(slice[3], Is.EqualTo(0xE0));
			Assert.That(slice[4], Is.EqualTo(0xFE));
			Assert.That(slice[5], Is.EqualTo(0x0F));
			Assert.That(slice[6], Is.EqualTo(0xDC));
			Assert.That(slice[7], Is.EqualTo(0xBA));

			Assert.That(slice[-1], Is.EqualTo(0xBA));
			Assert.That(slice[-2], Is.EqualTo(0xDC));
			Assert.That(slice[-3], Is.EqualTo(0x0F));
			Assert.That(slice[-4], Is.EqualTo(0xFE));
			Assert.That(slice[-5], Is.EqualTo(0xE0));
			Assert.That(slice[-6], Is.EqualTo(0xDD));
			Assert.That(slice[-7], Is.EqualTo(0xF0));
			Assert.That(slice[-8], Is.EqualTo(0x0D));

			Assert.That(() => slice[8], Throws.InstanceOf<IndexOutOfRangeException>());
			Assert.That(() => slice[-9], Throws.InstanceOf<IndexOutOfRangeException>());
		}

		[Test]
		public void Test_Flush()
		{
			var writer = new SliceWriter();
			writer.WriteBytes(Slice.FromString("hello world!"));
			Assert.That(writer.Position, Is.EqualTo(12));

			writer.Flush(5);
			Assert.That(writer.Position, Is.EqualTo(7));
			Assert.That(writer.ToSlice().ToString(), Is.EqualTo(" world!"));

			writer.Flush(1);
			Assert.That(writer.Position, Is.EqualTo(6));
			Assert.That(writer.ToSlice().ToString(), Is.EqualTo("world!"));

			writer.Flush(0);
			Assert.That(writer.Position, Is.EqualTo(6));
			Assert.That(writer.ToSlice().ToString(), Is.EqualTo("world!"));

			// REVIEW: should we throw if we flush more bytes than in the writer? (currently, it just clears it)
			writer.Flush(7);
			Assert.That(writer.Position, Is.EqualTo(0));
			Assert.That(writer.ToSlice(), Is.EqualTo(Slice.Empty));
		}

		[Test]
		public void Test_Skip()
		{
			var writer = new SliceWriter();
			writer.WriteBytes(Slice.FromString("hello"));
			Assert.That(writer.Position, Is.EqualTo(5));
			Assert.That(writer.ToSlice().ToString(), Is.EqualTo("hello"));

			// default pad is 255
			Assert.That(writer.Skip(3), Is.EqualTo(5));
			Assert.That(writer.Position, Is.EqualTo(8));
			Assert.That(writer.ToSlice().ToString(), Is.EqualTo("hello<FF><FF><FF>"));

			writer.WriteBytes(Slice.FromString("world"));
			Assert.That(writer.Position, Is.EqualTo(13));
			Assert.That(writer.ToSlice().ToString(), Is.EqualTo("hello<FF><FF><FF>world"));

			// custom pad
			Assert.That(writer.Skip(5, 42), Is.EqualTo(13));
			Assert.That(writer.Position, Is.EqualTo(18));
			Assert.That(writer.ToSlice().ToString(), Is.EqualTo("hello<FF><FF><FF>world*****"));
		}

		[Test]
		public void Test_ToSlice()
		{
			var writer = new SliceWriter(64);
			var slice = writer.ToSlice();
			//note: slice.Array is not guaranteed to be equal to writer.Buffer
			Assert.That(slice.Count, Is.EqualTo(0));
			Assert.That(slice.Offset, Is.EqualTo(0));

			writer.WriteBytes(Slice.FromString("hello world!"));
			slice = writer.ToSlice();
			Assert.That(slice.Array, Is.SameAs(writer.Buffer));
			Assert.That(slice.Offset, Is.EqualTo(0));
			Assert.That(slice.Count, Is.EqualTo(12));
			Assert.That(slice.ToStringAscii(), Is.EqualTo("hello world!"));

			writer.WriteBytes(Slice.FromString("foo"));
			slice = writer.ToSlice();
			Assert.That(slice.Array, Is.SameAs(writer.Buffer));
			Assert.That(slice.Offset, Is.EqualTo(0));
			Assert.That(slice.Count, Is.EqualTo(15));
			Assert.That(slice.ToStringAscii(), Is.EqualTo("hello world!foo"));
		}

		[Test]
		public void Test_Head()
		{
			var writer = new SliceWriter(64);
			var slice = writer.Head(0);
			Assert.That(slice.Count, Is.EqualTo(0));
			Assert.That(slice.Offset, Is.EqualTo(0));
			//note: slice.Array is not guaranteed to be equal to writer.Buffer
			Assert.That(() => writer.Head(1), Throws.InstanceOf<ArgumentOutOfRangeException>());

			writer.WriteBytes(Slice.FromString("hello world!"));
			slice = writer.Head(5);
			Assert.That(slice.Array, Is.SameAs(writer.Buffer));
			Assert.That(slice.Offset, Is.EqualTo(0));
			Assert.That(slice.Count, Is.EqualTo(5));
			Assert.That(slice.ToStringAscii(), Is.EqualTo("hello"));

			slice = writer.Head(12);
			Assert.That(slice.Array, Is.SameAs(writer.Buffer));
			Assert.That(slice.Offset, Is.EqualTo(0));
			Assert.That(slice.Count, Is.EqualTo(12));
			Assert.That(slice.ToStringAscii(), Is.EqualTo("hello world!"));

			Assert.That(() => writer.Head(13), Throws.InstanceOf<ArgumentOutOfRangeException>());
			Assert.That(() => writer.Head(-1), Throws.InstanceOf<ArgumentOutOfRangeException>());

			writer.WriteBytes(Slice.FromString("foo"));
			slice = writer.Head(3);
			Assert.That(slice.Array, Is.SameAs(writer.Buffer));
			Assert.That(slice.Offset, Is.EqualTo(0));
			Assert.That(slice.Count, Is.EqualTo(3));
			Assert.That(slice.ToStringAscii(), Is.EqualTo("hel"));

			slice = writer.Head(15);
			Assert.That(slice.Array, Is.SameAs(writer.Buffer));
			Assert.That(slice.Offset, Is.EqualTo(0));
			Assert.That(slice.Count, Is.EqualTo(15));
			Assert.That(slice.ToStringAscii(), Is.EqualTo("hello world!foo"));

			Assert.That(() => writer.Head(16), Throws.InstanceOf<ArgumentOutOfRangeException>());

		}

		[Test]
		public void Test_Tail()
		{
			var writer = new SliceWriter(64);
			var slice = writer.Tail(0);
			Assert.That(slice.Count, Is.EqualTo(0));
			Assert.That(slice.Offset, Is.EqualTo(0));
			//note: slice.Array is not guaranteed to be equal to writer.Buffer
			Assert.That(() => writer.Head(1), Throws.InstanceOf<ArgumentOutOfRangeException>());

			writer.WriteBytes(Slice.FromString("hello world!"));
			slice = writer.Tail(6);
			Assert.That(slice.Array, Is.SameAs(writer.Buffer));
			Assert.That(slice.Offset, Is.EqualTo(6));
			Assert.That(slice.Count, Is.EqualTo(6));
			Assert.That(slice.ToStringAscii(), Is.EqualTo("world!"));

			slice = writer.Tail(12);
			Assert.That(slice.Array, Is.SameAs(writer.Buffer));
			Assert.That(slice.Offset, Is.EqualTo(0));
			Assert.That(slice.Count, Is.EqualTo(12));
			Assert.That(slice.ToStringAscii(), Is.EqualTo("hello world!"));

			Assert.That(() => writer.Tail(13), Throws.InstanceOf<ArgumentOutOfRangeException>());
			Assert.That(() => writer.Tail(-1), Throws.InstanceOf<ArgumentOutOfRangeException>());

			writer.WriteBytes(Slice.FromString("foo"));
			slice = writer.Tail(3);
			Assert.That(slice.Array, Is.SameAs(writer.Buffer));
			Assert.That(slice.Offset, Is.EqualTo(12));
			Assert.That(slice.Count, Is.EqualTo(3));
			Assert.That(slice.ToStringAscii(), Is.EqualTo("foo"));

			slice = writer.Tail(15);
			Assert.That(slice.Array, Is.SameAs(writer.Buffer));
			Assert.That(slice.Offset, Is.EqualTo(0));
			Assert.That(slice.Count, Is.EqualTo(15));
			Assert.That(slice.ToStringAscii(), Is.EqualTo("hello world!foo"));

			Assert.That(() => writer.Tail(16), Throws.InstanceOf<ArgumentOutOfRangeException>());

		}

		[Test]
		public void Test_AppendBytes()
		{
			var writer = new SliceWriter(64);
			var slice = writer.AppendBytes(Slice.Empty);
			//note: slice.Array is not guaranteed to be equal to writer.Buffer
			Assert.That(slice.Offset, Is.EqualTo(0));
			Assert.That(slice.Count, Is.EqualTo(0));

			slice = writer.AppendBytes(Slice.FromString("hello world!"));
			Assert.That(slice.Array, Is.SameAs(writer.Buffer));
			Assert.That(slice.Offset, Is.EqualTo(0));
			Assert.That(slice.Count, Is.EqualTo(12));
			Assert.That(slice.ToStringUtf8(), Is.EqualTo("hello world!"));
			Assert.That(writer.ToSlice().ToStringUtf8(), Is.EqualTo("hello world!"));

			var foo = Slice.FromString("foo");
			slice = writer.AppendBytes(foo);
			Assert.That(slice.Array, Is.SameAs(writer.Buffer));
			Assert.That(slice.Offset, Is.EqualTo(12));
			Assert.That(slice.Count, Is.EqualTo(3));
			Assert.That(slice.ToStringUtf8(), Is.EqualTo("foo"));
			Assert.That(writer.ToSlice().ToStringUtf8(), Is.EqualTo("hello world!foo"));

			var bar = Slice.FromString("bar");
			slice = writer.AppendBytes(bar.Span);
			Assert.That(slice.Array, Is.SameAs(writer.Buffer));
			Assert.That(slice.Offset, Is.EqualTo(15));
			Assert.That(slice.Count, Is.EqualTo(3));
			Assert.That(slice.ToStringUtf8(), Is.EqualTo("bar"));
			Assert.That(writer.ToSlice().ToStringUtf8(), Is.EqualTo("hello world!foobar"));

			var baz = Slice.FromString("baz");
			slice = writer.AppendBytes(baz.Array, baz.Offset, baz.Count);
			Assert.That(slice.Array, Is.SameAs(writer.Buffer));
			Assert.That(slice.Offset, Is.EqualTo(18));
			Assert.That(slice.Count, Is.EqualTo(3));
			Assert.That(slice.ToStringUtf8(), Is.EqualTo("baz"));
			Assert.That(writer.ToSlice().ToStringUtf8(), Is.EqualTo("hello world!foobarbaz"));

			unsafe
			{
				slice = writer.AppendBytes(null);
			}
			//note: slice.Array is not guaranteed to be equal to writer.Buffer
			Assert.That(slice.Offset, Is.EqualTo(0)); //REVIEW: should we return (Buffer, Position, 0) instead of (EmptyArray, 0, 0) ?
			Assert.That(slice.Count, Is.EqualTo(0));
		}

		[Test]
		public void Test_WriteBytes_Resize_Buffer()
		{

			// check buffer resize occurs as intended
			var original = new byte[32];
			var writer = new SliceWriter(original);
			Assert.That(writer.Buffer, Is.SameAs(original));

			// first write should not resize the buffer
			writer.WriteBytes(Slice.Repeat((byte)'a', 24));
			Assert.That(writer.Buffer, Is.SameAs(original));
			Assert.That(writer.ToSlice().ToStringAscii(), Is.EqualTo("aaaaaaaaaaaaaaaaaaaaaaaa"));

			// second write should resize the buffer
			writer.WriteBytes(Slice.Repeat((byte)'b', 24));
			// buffer should have been replaced with larger one
			Assert.That(writer.Buffer, Is.Not.SameAs(original));
			Assert.That(writer.Buffer.Length, Is.GreaterThanOrEqualTo(48));

			//but the content should be unchanged
			Assert.That(writer.ToSlice().ToStringAscii(), Is.EqualTo("aaaaaaaaaaaaaaaaaaaaaaaabbbbbbbbbbbbbbbbbbbbbbbb"));

			// adding exactly what is missing should not resize the buffer
			writer = new SliceWriter(original);
			writer.WriteBytes(Slice.Repeat((byte)'c', original.Length));
			Assert.That(writer.Buffer, Is.SameAs(original));
			Assert.That(writer.ToSlice().ToStringAscii(), Is.EqualTo("cccccccccccccccccccccccccccccccc"));

			// adding nothing should not resize the buffer
			writer.WriteBytes(Slice.Empty);
			Assert.That(writer.Buffer, Is.SameAs(original));
			Assert.That(writer.ToSlice().ToStringAscii(), Is.EqualTo("cccccccccccccccccccccccccccccccc"));

			// adding a single byte should resize the buffer
			writer.WriteBytes(Slice.FromChar('Z'));
			Assert.That(writer.Buffer, Is.Not.SameAs(original));
			Assert.That(writer.Buffer.Length, Is.GreaterThanOrEqualTo(33));
			Assert.That(writer.ToSlice().ToStringAscii(), Is.EqualTo("ccccccccccccccccccccccccccccccccZ"));
		}

		[Test]
		public void Test_AppendBytes_Resize_Buffer()
		{

			// check buffer resize occurs as intended
			var original = new byte[32];
			var writer = new SliceWriter(original);
			Assert.That(writer.Buffer, Is.SameAs(original));

			// first write should not resize the buffer
			var aaa = writer.AppendBytes(Slice.Repeat((byte) 'a', 24));
			Assert.That(aaa.Array, Is.SameAs(original));

			// second write should resize the buffer
			var bbb = writer.AppendBytes(Slice.Repeat((byte) 'b', 24));
			Assert.That(bbb.Array, Is.SameAs(writer.Buffer));
			//note: buffer should have been copied between both calls, so 'aaa' should point to the OLD buffer
			Assert.That(bbb.Array, Is.Not.SameAs(original));
			//but the content should be unchanged
			Assert.That(writer.ToSlice().ToStringAscii(), Is.EqualTo("aaaaaaaaaaaaaaaaaaaaaaaabbbbbbbbbbbbbbbbbbbbbbbb"));
			// => mutating aaa should not change the buffer
			aaa.Array[aaa.Offset] = (byte) 'Z';
			Assert.That(writer.ToSlice().ToStringAscii(), Is.EqualTo("aaaaaaaaaaaaaaaaaaaaaaaabbbbbbbbbbbbbbbbbbbbbbbb"));
			// => but mutating bbb should change the buffer
			bbb.Array[bbb.Offset] = (byte)'Z';
			Assert.That(writer.ToSlice().ToStringAscii(), Is.EqualTo("aaaaaaaaaaaaaaaaaaaaaaaaZbbbbbbbbbbbbbbbbbbbbbbb"));
		}
	}
}
