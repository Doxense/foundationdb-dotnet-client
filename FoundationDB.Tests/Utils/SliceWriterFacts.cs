#region BSD Licence
/* Copyright (c) 2013-2014, Doxense SAS
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

namespace FoundationDB.Client.Utils.Tests
{
	using FoundationDB.Client;
	using NUnit.Framework;
	using System;
	using System.Text;

	[TestFixture]
	public class SliceWriterFacts
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
			var writer = SliceWriter.Empty;
			action(ref writer, value);

			Assert.That(writer.ToSlice().ToHexaString(' '), Is.EqualTo(expectedResult), "Value {0} ({1}) was not properly packed", value == null ? "<null>" : value is string ? Clean(value as string) : value.ToString(), (value == null ? "null" : value.GetType().Name));
		}

		[Test]
		public void Test_Empty_Writer()
		{
			var writer = SliceWriter.Empty;
			Assert.That(writer.Position, Is.EqualTo(0));
			Assert.That(writer.HasData, Is.False);
			Assert.That(writer.Buffer, Is.Null);
			Assert.That(writer.ToSlice(), Is.EqualTo(Slice.Empty));
		}

		[Test]
		public void Test_WriteBytes()
		{
			TestHandler<byte[]> test = (ref SliceWriter writer, byte[] value) => writer.WriteBytes(value);

			PerformWriterTest(test, null, "");
			PerformWriterTest(test, new byte[0], "");
			PerformWriterTest(test, new byte[] { 66 }, "42");
			PerformWriterTest(test, new byte[] { 65, 66, 67 }, "41 42 43");
		}

		[Test]
		public void Test_WriteByte()
		{
			TestHandler<byte> test = (ref SliceWriter writer, byte value) => writer.WriteByte(value);

			PerformWriterTest(test, default(byte), "00");
			PerformWriterTest(test, (byte)1, "01");
			PerformWriterTest(test, (byte)42, "2A");
			PerformWriterTest(test, (byte)255, "FF");
		}

		[Test]
		public void Test_WriteFixed32()
		{
			TestHandler<uint> test = (ref SliceWriter writer, uint value) => writer.WriteFixed32(value);

			PerformWriterTest(test, 0U, "00 00 00 00");
			PerformWriterTest(test, 1U, "01 00 00 00");
			PerformWriterTest(test, 0x12U, "12 00 00 00");
			PerformWriterTest(test, 0x1234U, "34 12 00 00");
			PerformWriterTest(test, ushort.MaxValue, "FF FF 00 00");
			PerformWriterTest(test, 0x123456U, "56 34 12 00");
			PerformWriterTest(test, 0xDEADBEEF, "EF BE AD DE");
			PerformWriterTest(test, uint.MaxValue, "FF FF FF FF");
		}

		[Test]
		public void Test_WriteFixed64()
		{
			TestHandler<ulong> test = (ref SliceWriter writer, ulong value) => writer.WriteFixed64(value);

			PerformWriterTest(test, 0UL, "00 00 00 00 00 00 00 00");
			PerformWriterTest(test, 1UL, "01 00 00 00 00 00 00 00");
			PerformWriterTest(test, 0x12UL, "12 00 00 00 00 00 00 00");
			PerformWriterTest(test, 0x1234UL, "34 12 00 00 00 00 00 00");
			PerformWriterTest(test, ushort.MaxValue, "FF FF 00 00 00 00 00 00");
			PerformWriterTest(test, 0x123456UL, "56 34 12 00 00 00 00 00");
			PerformWriterTest(test, 0x12345678UL, "78 56 34 12 00 00 00 00");
			PerformWriterTest(test, uint.MaxValue, "FF FF FF FF 00 00 00 00");
			PerformWriterTest(test, 0x123456789AUL, "9A 78 56 34 12 00 00 00");
			PerformWriterTest(test, 0x123456789ABCUL, "BC 9A 78 56 34 12 00 00");
			PerformWriterTest(test, 0x123456789ABCDEUL, "DE BC 9A 78 56 34 12 00");
			PerformWriterTest(test, 0xBADC0FFEE0DDF00DUL, "0D F0 DD E0 FE 0F DC BA");
			PerformWriterTest(test, ulong.MaxValue, "FF FF FF FF FF FF FF FF");
		}

		[Test]
		public void Test_WriteVarint32()
		{
			TestHandler<uint> test = (ref SliceWriter writer, uint value) => writer.WriteVarint32(value);

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
			TestHandler<ulong> test = (ref SliceWriter writer, ulong value) => writer.WriteVarint64(value);

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
			TestHandler<Slice> test = (ref SliceWriter writer, Slice value) => writer.WriteVarbytes(value);

			PerformWriterTest(test, Slice.Nil, "00");
			PerformWriterTest(test, Slice.Empty, "00");
			PerformWriterTest(test, Slice.FromByte(42), "01 2A");
			PerformWriterTest(test, Slice.FromByte(255), "01 FF");
			PerformWriterTest(test, Slice.FromString("ABC"), "03 41 42 43");
			PerformWriterTest(test, Slice.FromFixedU32(0xDEADBEEF), "04 EF BE AD DE");
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

	}
}
