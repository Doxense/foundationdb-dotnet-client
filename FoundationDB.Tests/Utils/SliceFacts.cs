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
	using NUnit.Framework;
	using System;
	using System.Collections.Generic;
	using System.IO;
	using System.Linq;
	using System.Text;
	using System.Threading.Tasks;

	[TestFixture]
	public class SliceFacts : FdbTest
	{

		[Test]
		public void Test_Slice_Nil()
		{
			// Slice.Nil is the equivalent of 'default(byte[])'

			Assert.That(Slice.Nil.Count, Is.EqualTo(0));
			Assert.That(Slice.Nil.Offset, Is.EqualTo(0));
			Assert.That(Slice.Nil.Array, Is.Null);

			Assert.That(Slice.Nil.IsNull, Is.True);
			Assert.That(Slice.Nil.HasValue, Is.False);
			Assert.That(Slice.Nil.IsEmpty, Is.False);
			Assert.That(Slice.Nil.IsNullOrEmpty, Is.True);
			Assert.That(Slice.Nil.IsPresent, Is.False);

			Assert.That(Slice.Nil.GetBytes(), Is.Null);
			Assert.That(Slice.Nil.ToAscii(), Is.Null);
			Assert.That(Slice.Nil.ToUnicode(), Is.Null);
			Assert.That(Slice.Nil.ToAsciiOrHexaString(), Is.EqualTo(String.Empty));
		}

		[Test]
		public void Test_Slice_Empty()
		{
			// Slice.Empty is the equivalent of 'new byte[0]'

			Assert.That(Slice.Empty.Count, Is.EqualTo(0));
			Assert.That(Slice.Empty.Offset, Is.EqualTo(0));
			Assert.That(Slice.Empty.Array, Is.Not.Null);
			Assert.That(Slice.Empty.Array.Length, Is.EqualTo(0));

			Assert.That(Slice.Empty.IsNull, Is.False);
			Assert.That(Slice.Empty.HasValue, Is.True);
			Assert.That(Slice.Empty.IsEmpty, Is.True);
			Assert.That(Slice.Empty.IsNullOrEmpty, Is.True);
			Assert.That(Slice.Empty.IsPresent, Is.False);

			Assert.That(Slice.Empty.GetBytes(), Is.EqualTo(new byte[0]));
			Assert.That(Slice.Empty.ToAscii(), Is.EqualTo(String.Empty));
			Assert.That(Slice.Empty.ToUnicode(), Is.EqualTo(String.Empty));
			Assert.That(Slice.Empty.ToAsciiOrHexaString(), Is.EqualTo("''"));
		}

		[Test]
		public void Test_Slice_With_Content()
		{
			Slice slice = Slice.FromAscii("ABC");

			Assert.That(slice.Count, Is.EqualTo(3));
			Assert.That(slice.Offset, Is.EqualTo(0));
			Assert.That(slice.Array, Is.Not.Null);
			Assert.That(slice.Array.Length, Is.GreaterThanOrEqualTo(3));

			Assert.That(slice.IsNull, Is.False);
			Assert.That(slice.HasValue, Is.True);
			Assert.That(slice.IsEmpty, Is.False);
			Assert.That(slice.IsNullOrEmpty, Is.False);
			Assert.That(slice.IsPresent, Is.True);

			Assert.That(slice.GetBytes(), Is.EqualTo(new byte[3] { 65, 66, 67 }));
			Assert.That(slice.ToAscii(), Is.EqualTo("ABC"));
			Assert.That(slice.ToUnicode(), Is.EqualTo("ABC"));
			Assert.That(slice.ToAsciiOrHexaString(), Is.EqualTo("'ABC'"));
		}

		[Test]
		public void Test_Slice_Create_With_Capacity()
		{
			Assert.That(Slice.Create(0).GetBytes(), Is.EqualTo(new byte[0]));
			Assert.That(Slice.Create(16).GetBytes(), Is.EqualTo(new byte[16]));

			Assert.That(() => Slice.Create(-1), Throws.InstanceOf<ArgumentException>());
		}

		[Test]
		public void Test_Slice_Create_With_Byte_Array()
		{
			Assert.That(Slice.Create(default(byte[])).GetBytes(), Is.EqualTo(null));
			Assert.That(Slice.Create(new byte[0]).GetBytes(), Is.EqualTo(new byte[0]));
			Assert.That(Slice.Create(new byte[] { 1, 2, 3 }).GetBytes(), Is.EqualTo(new byte[] { 1, 2, 3 }));

			// the array return by GetBytes() should not be the same array that was passed to Create !
			byte[] tmp = Guid.NewGuid().ToByteArray(); // create a 16-byte array
			var slice = Slice.Create(tmp);
			Assert.That(slice.Array, Is.SameAs(tmp));
			Assert.That(slice.Offset, Is.EqualTo(0));
			Assert.That(slice.Count, Is.EqualTo(tmp.Length));
			// they should be equal, but not the same !
			Assert.That(slice.GetBytes(), Is.EqualTo(tmp));
			Assert.That(slice.GetBytes(), Is.Not.SameAs(tmp));

			// create from a slice of the array
			slice = Slice.Create(tmp, 4, 7);
			Assert.That(slice.Array, Is.SameAs(tmp));
			Assert.That(slice.Offset, Is.EqualTo(4));
			Assert.That(slice.Count, Is.EqualTo(7));
			var buf = new byte[7];
			Array.Copy(tmp, 4, buf, 0, 7);
			Assert.That(slice.GetBytes(), Is.EqualTo(buf));

			Assert.That(Slice.Create(default(byte[])), Is.EqualTo(Slice.Nil));
			Assert.That(Slice.Create(new byte[0]), Is.EqualTo(Slice.Empty));
		}

		[Test]
		public void Test_Slice_Create_Validates_Arguments()
		{
			// null array only allowed with offset=0 and count=0
			Assert.That(() => Slice.Create(null, 0, 1), Throws.InstanceOf<ArgumentException>());
			Assert.That(() => Slice.Create(null, 1, 0), Throws.InstanceOf<ArgumentException>());
			Assert.That(() => Slice.Create(null, 1, 1), Throws.InstanceOf<ArgumentException>());

			// empty array only allowed with offset=0 and count=0
			Assert.That(() => Slice.Create(new byte[0], 0, 1), Throws.InstanceOf<ArgumentException>());
			Assert.That(() => Slice.Create(new byte[0], 1, 0), Throws.InstanceOf<ArgumentException>());
			Assert.That(() => Slice.Create(new byte[0], 1, 1), Throws.InstanceOf<ArgumentException>());

			// last item must fit in the buffer
			Assert.That(() => Slice.Create(new byte[3], 0, 4), Throws.InstanceOf<ArgumentException>());
			Assert.That(() => Slice.Create(new byte[3], 1, 3), Throws.InstanceOf<ArgumentException>());
			Assert.That(() => Slice.Create(new byte[3], 3, 1), Throws.InstanceOf<ArgumentException>());

			// negative arguments
			//TODO: should we allow negative indexing where Slice.Create(..., -1, 1) would mean "the last byte" ?
			Assert.That(() => Slice.Create(new byte[3], -1, 1), Throws.InstanceOf<ArgumentException>());
			Assert.That(() => Slice.Create(new byte[3], 0, -1), Throws.InstanceOf<ArgumentException>());
			Assert.That(() => Slice.Create(new byte[3], -1, -1), Throws.InstanceOf<ArgumentException>());
		}

		[Test]
		public void Test_Slice_Create_With_ArraySegment()
		{
			Slice slice;
			byte[] tmp = Guid.NewGuid().ToByteArray();

			slice = Slice.Create(new ArraySegment<byte>(tmp));
			Assert.That(slice.Array, Is.SameAs(tmp));
			Assert.That(slice.Offset, Is.EqualTo(0));
			Assert.That(slice.Count, Is.EqualTo(tmp.Length));
			// they should be equal, but not the same !
			Assert.That(slice.GetBytes(), Is.EqualTo(tmp));
			Assert.That(slice.GetBytes(), Is.Not.SameAs(tmp));

			slice = Slice.Create(new ArraySegment<byte>(tmp, 4, 7));
			Assert.That(slice.Array, Is.SameAs(tmp));
			Assert.That(slice.Offset, Is.EqualTo(4));
			Assert.That(slice.Count, Is.EqualTo(7));
			var buf = new byte[7];
			Array.Copy(tmp, 4, buf, 0, 7);
			Assert.That(slice.GetBytes(), Is.EqualTo(buf));

			Assert.That(Slice.Create(default(ArraySegment<byte>)), Is.EqualTo(Slice.Nil));
			Assert.That(Slice.Create(new ArraySegment<byte>(new byte[0])), Is.EqualTo(Slice.Empty));
		}

		[Test]
		public void Test_Slice_Pseudo_Random()
		{
			Slice slice;
			var rng = new Random();

			slice = Slice.Random(rng, 16);
			Assert.That(slice.Array, Is.Not.Null);
			Assert.That(slice.Array.Length, Is.GreaterThanOrEqualTo(16));
			Assert.That(slice.Offset, Is.EqualTo(0));
			Assert.That(slice.Count, Is.EqualTo(16));
			// can't really test random data, appart from checking that it's not filled with zeroes
			Assert.That(slice.GetBytes(), Is.Not.All.EqualTo(0));

			Assert.That(Slice.Random(rng, 0), Is.EqualTo(Slice.Empty));

			Assert.That(() => Slice.Random(default(System.Random), 16), Throws.InstanceOf<ArgumentNullException>());
			Assert.That(() => Slice.Random(rng, -1), Throws.InstanceOf<ArgumentException>());
		}

		[Test]
		public void Test_Slice_Cryptographic_Random()
		{
			Slice slice;
			var rng = System.Security.Cryptography.RandomNumberGenerator.Create();

			// normal
			slice = Slice.Random(rng, 16);
			Assert.That(slice.Array, Is.Not.Null);
			Assert.That(slice.Array.Length, Is.GreaterThanOrEqualTo(16));
			Assert.That(slice.Offset, Is.EqualTo(0));
			Assert.That(slice.Count, Is.EqualTo(16));
			// can't really test random data, appart from checking that it's not filled with zeroes
			Assert.That(slice.GetBytes(), Is.Not.All.EqualTo(0));

			// non-zero bytes
			// we can't 100% test that, unless with a lot of iterations...
			for (int i = 0; i < 256; i++)
			{
				Assert.That(
					Slice.Random(rng, 256, nonZeroBytes: true).GetBytes(),
					Is.All.Not.EqualTo(0)
				);
			}

			Assert.That(Slice.Random(rng, 0), Is.EqualTo(Slice.Empty));
			Assert.That(() => Slice.Random(default(System.Security.Cryptography.RandomNumberGenerator), 16), Throws.InstanceOf<ArgumentNullException>());
			Assert.That(() => Slice.Random(rng, -1), Throws.InstanceOf<ArgumentException>());
		}

		[Test]
		public void Test_Slice_FromAscii()
		{
			Assert.That(Slice.FromAscii(default(string)).GetBytes(), Is.Null);
			Assert.That(Slice.FromAscii(String.Empty).GetBytes(), Is.EqualTo(new byte[0]));
			Assert.That(Slice.FromAscii("ABC").GetBytes(), Is.EqualTo(new byte[] { 0x41, 0x42, 0x43 }));

			// if the string contains non-ASCII chars, it will be corrupted
			// note: the line below should contain two kanjis. If your editor displays '??' or squares, it is probably not able to display unicode chars properly
			var slice = Slice.FromAscii("hello 世界"); // 8 'letters'
			Assert.That(slice.GetBytes(), Is.EqualTo(Encoding.Default.GetBytes("hello 世界")));
			Assert.That(slice.ToAscii(), Is.EqualTo("hello ??"), "non-ASCII chars should be converted to '?'");
			Assert.That(slice.Count, Is.EqualTo(8));

			//REVIEW: should FromAscii() throw an exception on non-ASCII chars? It will silently corrupt strings if nobody checks the value....
		}

		[Test]
		public void Test_Slice_FromString_Uses_UTF8()
		{
			Assert.That(Slice.FromString(default(string)).GetBytes(), Is.Null);
			Assert.That(Slice.FromString(String.Empty).GetBytes(), Is.EqualTo(new byte[0]));
			Assert.That(Slice.FromString("ABC").GetBytes(), Is.EqualTo(new byte[] { 0x41, 0x42, 0x43 }));
			Assert.That(Slice.FromString("é").GetBytes(), Is.EqualTo(new byte[] { 0xC3, 0xA9 }));

			// if the string contains UTF-8 characters, it should be encoded properly
			// note: the line below should contain two kanjis. If your editor displays '??' or squares, it is probably not able to display unicode chars properly
			var slice = Slice.FromString("héllø 世界"); // 8 'letters'
			Assert.That(slice.GetBytes(), Is.EqualTo(Encoding.UTF8.GetBytes("héllø 世界")));
			Assert.That(slice.ToUnicode(), Is.EqualTo("héllø 世界"), "non-ASCII chars should not be corrupted");
			Assert.That(slice.Count, Is.EqualTo(14));
		}

		[Test]
		public void Test_Slice_FromInt32()
		{
			// 32-bit integers should be encoded in little endian, and with 1, 2 or 4 bytes
			// 0x12 -> { 12 }
			// 0x1234 -> { 34 12 }
			// 0x123456 -> { 56 34 12 00 }
			// 0x12345678 -> { 78 56 34 12 }

			Assert.That(Slice.FromInt32(0x12).ToHexaString(), Is.EqualTo("12"));
			Assert.That(Slice.FromInt32(0x1234).ToHexaString(), Is.EqualTo("3412"));
			Assert.That(Slice.FromInt32(0x123456).ToHexaString(), Is.EqualTo("56341200"));
			Assert.That(Slice.FromInt32(0x12345678).ToHexaString(), Is.EqualTo("78563412"));

			Assert.That(Slice.FromInt32(0).ToHexaString(), Is.EqualTo("00"));
			Assert.That(Slice.FromInt32(1).ToHexaString(), Is.EqualTo("01"));
			Assert.That(Slice.FromInt32(255).ToHexaString(), Is.EqualTo("ff"));
			Assert.That(Slice.FromInt32(256).ToHexaString(), Is.EqualTo("0001"));
			Assert.That(Slice.FromInt32(65535).ToHexaString(), Is.EqualTo("ffff"));
			Assert.That(Slice.FromInt32(65536).ToHexaString(), Is.EqualTo("00000100"));
			Assert.That(Slice.FromInt32(int.MaxValue).ToHexaString(), Is.EqualTo("ffffff7f"));
			Assert.That(Slice.FromInt32(int.MinValue).ToHexaString(), Is.EqualTo("00000080"));
		}

		[Test]
		public void Test_Slice_ToInt32()
		{
			Assert.That(Slice.Create(new byte[] { 0x12 }).ToInt32(), Is.EqualTo(0x12));
			Assert.That(Slice.Create(new byte[] { 0x34, 0x12 }).ToInt32(), Is.EqualTo(0x1234));
			Assert.That(Slice.Create(new byte[] { 0x56, 0x34, 0x12 }).ToInt32(), Is.EqualTo(0x123456));
			Assert.That(Slice.Create(new byte[] { 0x56, 0x34, 0x12, 00 }).ToInt32(), Is.EqualTo(0x123456));
			Assert.That(Slice.Create(new byte[] { 0x78, 0x56, 0x34, 0x12 }).ToInt32(), Is.EqualTo(0x12345678));

			Assert.That(Slice.Create(new byte[] { 0 }).ToInt32(), Is.EqualTo(0));
			Assert.That(Slice.Create(new byte[] { 255 }).ToInt32(), Is.EqualTo(255));
			Assert.That(Slice.Create(new byte[] { 0, 1 }).ToInt32(), Is.EqualTo(256));
			Assert.That(Slice.Create(new byte[] { 255, 255 }).ToInt32(), Is.EqualTo(65535));
			Assert.That(Slice.Create(new byte[] { 0, 0, 1 }).ToInt32(), Is.EqualTo(1 << 16));
			Assert.That(Slice.Create(new byte[] { 0, 0, 1, 0 }).ToInt32(), Is.EqualTo(1 << 16));
			Assert.That(Slice.Create(new byte[] { 255, 255, 255 }).ToInt32(), Is.EqualTo((1 << 24) - 1));
			Assert.That(Slice.Create(new byte[] { 0, 0, 0, 1 }).ToInt32(), Is.EqualTo(1 << 24));
			Assert.That(Slice.Create(new byte[] { 255, 255, 255, 127 }).ToInt32(), Is.EqualTo(int.MaxValue));
		}

		[Test]
		public void Test_Slice_FromInt64()
		{
			// 64-bit integers should be encoded in little endian, and with 1, 2, 4 or 8 bytes
			// 0x12 -> { 12 }
			// 0x1234 -> { 34 12 }
			// 0x123456 -> { 56 34 12 00 }
			// 0x12345678 -> { 78 56 34 12 }
			// 0x123456789A -> { 9A 78 56 34 12 00 00 00}
			// 0x123456789ABC -> { BC 9A 78 56 34 12 00 00}
			// 0x123456789ABCDE -> { DE BC 9A 78 56 34 12 00}
			// 0x123456789ABCDEF0 -> { F0 DE BC 9A 78 56 34 12 }

			Assert.That(Slice.FromInt64(0x12).ToHexaString(), Is.EqualTo("12"));
			Assert.That(Slice.FromInt64(0x1234).ToHexaString(), Is.EqualTo("3412"));
			Assert.That(Slice.FromInt64(0x123456).ToHexaString(), Is.EqualTo("56341200"));
			Assert.That(Slice.FromInt64(0x12345678).ToHexaString(), Is.EqualTo("78563412"));
			Assert.That(Slice.FromInt64(0x123456789A).ToHexaString(), Is.EqualTo("9a78563412000000"));
			Assert.That(Slice.FromInt64(0x123456789ABC).ToHexaString(), Is.EqualTo("bc9a785634120000"));
			Assert.That(Slice.FromInt64(0x123456789ABCDE).ToHexaString(), Is.EqualTo("debc9a7856341200"));
			Assert.That(Slice.FromInt64(0x123456789ABCDEF0).ToHexaString(), Is.EqualTo("f0debc9a78563412"));

			Assert.That(Slice.FromInt64(0).ToHexaString(), Is.EqualTo("00"));
			Assert.That(Slice.FromInt64(1).ToHexaString(), Is.EqualTo("01"));
			Assert.That(Slice.FromInt64(255).ToHexaString(), Is.EqualTo("ff"));
			Assert.That(Slice.FromInt64(256).ToHexaString(), Is.EqualTo("0001"));
			Assert.That(Slice.FromInt64(65535).ToHexaString(), Is.EqualTo("ffff"));
			Assert.That(Slice.FromInt64(65536).ToHexaString(), Is.EqualTo("00000100"));
			Assert.That(Slice.FromInt64(int.MaxValue).ToHexaString(), Is.EqualTo("ffffff7f"));
			Assert.That(Slice.FromInt64(int.MinValue).ToHexaString(), Is.EqualTo("00000080ffffffff"));
			Assert.That(Slice.FromInt64(1L + int.MaxValue).ToHexaString(), Is.EqualTo("0000008000000000"));
			Assert.That(Slice.FromInt64(long.MaxValue).ToHexaString(), Is.EqualTo("ffffffffffffff7f"));
			Assert.That(Slice.FromInt64(long.MinValue).ToHexaString(), Is.EqualTo("0000000000000080"));

		}

		[Test]
		public void Test_Slice_ToInt64()
		{
			Assert.That(Slice.Create(new byte[] { 0x12 }).ToInt64(), Is.EqualTo(0x12));
			Assert.That(Slice.Create(new byte[] { 0x34, 0x12 }).ToInt64(), Is.EqualTo(0x1234));
			Assert.That(Slice.Create(new byte[] { 0x56, 0x34, 0x12 }).ToInt64(), Is.EqualTo(0x123456));
			Assert.That(Slice.Create(new byte[] { 0x56, 0x34, 0x12, 00 }).ToInt64(), Is.EqualTo(0x123456));
			Assert.That(Slice.Create(new byte[] { 0x78, 0x56, 0x34, 0x12 }).ToInt64(), Is.EqualTo(0x12345678));
			Assert.That(Slice.Create(new byte[] { 0x9A, 0x78, 0x56, 0x34, 0x12 }).ToInt64(), Is.EqualTo(0x123456789A));
			Assert.That(Slice.Create(new byte[] { 0xBC, 0x9A, 0x78, 0x56, 0x34, 0x12 }).ToInt64(), Is.EqualTo(0x123456789ABC));
			Assert.That(Slice.Create(new byte[] { 0xDE, 0xBC, 0x9A, 0x78, 0x56, 0x34, 0x12 }).ToInt64(), Is.EqualTo(0x123456789ABCDE));
			Assert.That(Slice.Create(new byte[] { 0xF0, 0xDE, 0xBC, 0x9A, 0x78, 0x56, 0x34, 0x12 }).ToInt64(), Is.EqualTo(0x123456789ABCDEF0));

			Assert.That(Slice.Create(new byte[] { 0 }).ToInt64(), Is.EqualTo(0L));
			Assert.That(Slice.Create(new byte[] { 255 }).ToInt64(), Is.EqualTo(255L));
			Assert.That(Slice.Create(new byte[] { 0, 1 }).ToInt64(), Is.EqualTo(256L));
			Assert.That(Slice.Create(new byte[] { 255, 255 }).ToInt64(), Is.EqualTo(65535L));
			Assert.That(Slice.Create(new byte[] { 0, 0, 1 }).ToInt64(), Is.EqualTo(1L << 16));
			Assert.That(Slice.Create(new byte[] { 0, 0, 1, 0 }).ToInt64(), Is.EqualTo(1L << 16));
			Assert.That(Slice.Create(new byte[] { 255, 255, 255 }).ToInt64(), Is.EqualTo((1L << 24) - 1));
			Assert.That(Slice.Create(new byte[] { 0, 0, 0, 1 }).ToInt64(), Is.EqualTo(1L << 24));
			Assert.That(Slice.Create(new byte[] { 0, 0, 0, 0, 1 }).ToInt64(), Is.EqualTo(1L << 32));
			Assert.That(Slice.Create(new byte[] { 0, 0, 0, 0, 0, 1 }).ToInt64(), Is.EqualTo(1L << 40));
			Assert.That(Slice.Create(new byte[] { 0, 0, 0, 0, 0, 0, 1 }).ToInt64(), Is.EqualTo(1L << 48));
			Assert.That(Slice.Create(new byte[] { 0, 0, 0, 0, 0, 0, 0, 1 }).ToInt64(), Is.EqualTo(1L << 56));
			Assert.That(Slice.Create(new byte[] { 255, 255, 255, 127 }).ToInt64(), Is.EqualTo(int.MaxValue));
			Assert.That(Slice.Create(new byte[] { 255, 255, 255, 255, 255, 255, 255, 127 }).ToInt64(), Is.EqualTo(long.MaxValue));

			// should validate the arguments
			var x = Slice.Create(new byte[] { 0x78, 0x56, 0x34, 0x12 });
			Assert.That(() => MutateOffset(x, -1).ToInt64(), Throws.InstanceOf<FormatException>());
			Assert.That(() => MutateCount(x, 5).ToInt64(), Throws.InstanceOf<FormatException>());
			Assert.That(() => MutateArray(x, null).ToInt64(), Throws.InstanceOf<FormatException>());
		}

		[Test]
		public void Test_Slice_FromUInt64()
		{
			// 64-bit integers should be encoded in little endian, and with 1, 2, 4 or 8 bytes
			// 0x12 -> { 12 }
			// 0x1234 -> { 34 12 }
			// 0x123456 -> { 56 34 12 00 }
			// 0x12345678 -> { 78 56 34 12 }
			// 0x123456789A -> { 9A 78 56 34 12 00 00 00}
			// 0x123456789ABC -> { BC 9A 78 56 34 12 00 00}
			// 0x123456789ABCDE -> { DE BC 9A 78 56 34 12 00}
			// 0x123456789ABCDEF0 -> { F0 DE BC 9A 78 56 34 12 }

			Assert.That(Slice.FromUInt64(0x12UL).ToHexaString(), Is.EqualTo("12"));
			Assert.That(Slice.FromUInt64(0x1234UL).ToHexaString(), Is.EqualTo("3412"));
			Assert.That(Slice.FromUInt64(0x123456UL).ToHexaString(), Is.EqualTo("56341200"));
			Assert.That(Slice.FromUInt64(0x12345678UL).ToHexaString(), Is.EqualTo("78563412"));
			Assert.That(Slice.FromUInt64(0x123456789AUL).ToHexaString(), Is.EqualTo("9a78563412000000"));
			Assert.That(Slice.FromUInt64(0x123456789ABCUL).ToHexaString(), Is.EqualTo("bc9a785634120000"));
			Assert.That(Slice.FromUInt64(0x123456789ABCDEUL).ToHexaString(), Is.EqualTo("debc9a7856341200"));
			Assert.That(Slice.FromUInt64(0x123456789ABCDEF0UL).ToHexaString(), Is.EqualTo("f0debc9a78563412"));

			Assert.That(Slice.FromUInt64(0UL).ToHexaString(), Is.EqualTo("00"));
			Assert.That(Slice.FromUInt64(1UL).ToHexaString(), Is.EqualTo("01"));
			Assert.That(Slice.FromUInt64(255UL).ToHexaString(), Is.EqualTo("ff"));
			Assert.That(Slice.FromUInt64(256UL).ToHexaString(), Is.EqualTo("0001"));
			Assert.That(Slice.FromUInt64(ushort.MaxValue).ToHexaString(), Is.EqualTo("ffff"));
			Assert.That(Slice.FromUInt64(65536UL).ToHexaString(), Is.EqualTo("00000100"));
			Assert.That(Slice.FromUInt64(int.MaxValue).ToHexaString(), Is.EqualTo("ffffff7f"));
			Assert.That(Slice.FromUInt64(uint.MaxValue).ToHexaString(), Is.EqualTo("ffffffff"));
			Assert.That(Slice.FromUInt64(long.MaxValue).ToHexaString(), Is.EqualTo("ffffffffffffff7f"));
			Assert.That(Slice.FromUInt64(ulong.MaxValue).ToHexaString(), Is.EqualTo("ffffffffffffffff"));

		}

		[Test]
		public void Test_Slice_ToUInt64()
		{
			Assert.That(Slice.Create(new byte[] { 0x12 }).ToUInt64(), Is.EqualTo(0x12));
			Assert.That(Slice.Create(new byte[] { 0x34, 0x12 }).ToUInt64(), Is.EqualTo(0x1234));
			Assert.That(Slice.Create(new byte[] { 0x56, 0x34, 0x12 }).ToUInt64(), Is.EqualTo(0x123456));
			Assert.That(Slice.Create(new byte[] { 0x56, 0x34, 0x12, 00 }).ToUInt64(), Is.EqualTo(0x123456));
			Assert.That(Slice.Create(new byte[] { 0x78, 0x56, 0x34, 0x12 }).ToUInt64(), Is.EqualTo(0x12345678));
			Assert.That(Slice.Create(new byte[] { 0x9A, 0x78, 0x56, 0x34, 0x12 }).ToUInt64(), Is.EqualTo(0x123456789A));
			Assert.That(Slice.Create(new byte[] { 0xBC, 0x9A, 0x78, 0x56, 0x34, 0x12 }).ToUInt64(), Is.EqualTo(0x123456789ABC));
			Assert.That(Slice.Create(new byte[] { 0xDE, 0xBC, 0x9A, 0x78, 0x56, 0x34, 0x12 }).ToUInt64(), Is.EqualTo(0x123456789ABCDE));
			Assert.That(Slice.Create(new byte[] { 0xF0, 0xDE, 0xBC, 0x9A, 0x78, 0x56, 0x34, 0x12 }).ToUInt64(), Is.EqualTo(0x123456789ABCDEF0));

			Assert.That(Slice.Create(new byte[] { 0 }).ToUInt64(), Is.EqualTo(0UL));
			Assert.That(Slice.Create(new byte[] { 255 }).ToUInt64(), Is.EqualTo(255UL));
			Assert.That(Slice.Create(new byte[] { 0, 1 }).ToUInt64(), Is.EqualTo(256UL));
			Assert.That(Slice.Create(new byte[] { 255, 255 }).ToUInt64(), Is.EqualTo(65535UL));
			Assert.That(Slice.Create(new byte[] { 0, 0, 1 }).ToUInt64(), Is.EqualTo(1UL << 16));
			Assert.That(Slice.Create(new byte[] { 0, 0, 1, 0 }).ToUInt64(), Is.EqualTo(1UL << 16));
			Assert.That(Slice.Create(new byte[] { 255, 255, 255 }).ToUInt64(), Is.EqualTo((1UL << 24) - 1));
			Assert.That(Slice.Create(new byte[] { 0, 0, 0, 1 }).ToUInt64(), Is.EqualTo(1UL << 24));
			Assert.That(Slice.Create(new byte[] { 0, 0, 0, 0, 1 }).ToUInt64(), Is.EqualTo(1UL << 32));
			Assert.That(Slice.Create(new byte[] { 0, 0, 0, 0, 0, 1 }).ToUInt64(), Is.EqualTo(1UL << 40));
			Assert.That(Slice.Create(new byte[] { 0, 0, 0, 0, 0, 0, 1 }).ToUInt64(), Is.EqualTo(1UL << 48));
			Assert.That(Slice.Create(new byte[] { 0, 0, 0, 0, 0, 0, 0, 1 }).ToUInt64(), Is.EqualTo(1UL << 56));
			Assert.That(Slice.Create(new byte[] { 255, 255, 255, 127 }).ToUInt64(), Is.EqualTo(int.MaxValue));
			Assert.That(Slice.Create(new byte[] { 255, 255, 255, 255 }).ToUInt64(), Is.EqualTo(uint.MaxValue));
			Assert.That(Slice.Create(new byte[] { 255, 255, 255, 255, 255, 255, 255, 127 }).ToUInt64(), Is.EqualTo(long.MaxValue));
			Assert.That(Slice.Create(new byte[] { 255, 255, 255, 255, 255, 255, 255, 255 }).ToUInt64(), Is.EqualTo(ulong.MaxValue));

			// should validate the arguments
			var x = Slice.Create(new byte[] { 0x78, 0x56, 0x34, 0x12 });
			Assert.That(() => MutateOffset(x, -1).ToUInt64(), Throws.InstanceOf<FormatException>());
			Assert.That(() => MutateCount(x, 5).ToUInt64(), Throws.InstanceOf<FormatException>());
			Assert.That(() => MutateArray(x, null).ToUInt64(), Throws.InstanceOf<FormatException>());		
		}

		[Test]
		public void Test_Slice_FromGuid()
		{
			// Verify that System.GUID are stored as UUIDs using RFC 4122, and not their natural in-memory format

			Slice slice;

			// empty guid should be all zeroes
			slice = Slice.FromGuid(Guid.Empty);
			Assert.That(slice.ToHexaString(), Is.EqualTo("00000000000000000000000000000000"));

			// GUIDs should be stored using RFC 4122 (big endian)
			var guid = new Guid("00112233-4455-6677-8899-aabbccddeeff");

			// byte order should follow the string!
			slice = Slice.FromGuid(guid);
			Assert.That(slice.ToHexaString(), Is.EqualTo("00112233445566778899aabbccddeeff"), "Slice.FromGuid() should use the RFC 4122 encoding");

			// but guid in memory should follow MS format
			slice = Slice.Create(guid.ToByteArray()); // <-- this is BAD, don't try this at home !
			Assert.That(slice.ToHexaString(), Is.EqualTo("33221100554477668899aabbccddeeff"));	
		}

		[Test]
		public void Test_Slice_ToGuid()
		{
			Slice slice;
			Guid guid;

			// all zeroes should return Guid.Empty
			slice = Slice.Create(16);
			Assert.That(slice.ToGuid(), Is.EqualTo(Guid.Empty));

			// RFC 4122 encoded UUIDs should be properly reversed when converted to System.GUID
			slice = Slice.FromHexa("00112233445566778899aabbccddeeff");
			guid = slice.ToGuid();
			Assert.That(guid.ToString(), Is.EqualTo("00112233-4455-6677-8899-aabbccddeeff"), "slice.ToGuid() should convert RFC 4122 encoded UUIDs into native System.Guid");

			// round-trip
			guid = Guid.NewGuid();
			Assert.That(Slice.FromGuid(guid).ToGuid(), Is.EqualTo(guid));

			Assert.That(Slice.FromAscii(guid.ToString()).ToGuid(), Is.EqualTo(guid), "String literals should also be converted if they match the expected format");

			Assert.That(() => Slice.FromAscii("random text").ToGuid(), Throws.InstanceOf<FormatException>());

			// should validate the arguments
			var x = Slice.FromGuid(guid);
			Assert.That(() => MutateOffset(x, -1).ToGuid(), Throws.InstanceOf<FormatException>());
			Assert.That(() => MutateCount(x, 17).ToGuid(), Throws.InstanceOf<FormatException>());
			Assert.That(() => MutateArray(x, null).ToGuid(), Throws.InstanceOf<FormatException>());

		}

		[Test]
		public void Test_Slice_FromUuid()
		{
			// Verify that FoundationDb.Client.Uuid are stored as 128-bit UUIDs using RFC 4122

			Slice slice;

			// empty guid should be all zeroes
			slice = Slice.FromUuid(Uuid.Empty);
			Assert.That(slice.ToHexaString(), Is.EqualTo("00000000000000000000000000000000"));

			// UUIDs should be stored using RFC 4122 (big endian)
			var uuid = new Uuid("00112233-4455-6677-8899-aabbccddeeff");

			// byte order should follow the string!
			slice = Slice.FromUuid(uuid);
			Assert.That(slice.ToHexaString(), Is.EqualTo("00112233445566778899aabbccddeeff"), "Slice.FromUuid() should preserve RFC 4122 orderig");

			// ToByteArray() should also be safe
			slice = Slice.Create(uuid.ToByteArray());
			Assert.That(slice.ToHexaString(), Is.EqualTo("00112233445566778899aabbccddeeff"));
		}

		[Test]
		public void Test_Slice_ToUuid()
		{
			Slice slice;
			Uuid uuid;

			// all zeroes should return Uuid.Empty
			slice = Slice.Create(16);
			Assert.That(slice.ToUuid(), Is.EqualTo(Uuid.Empty));

			// RFC 4122 encoded UUIDs should not keep the byte ordering
			slice = Slice.FromHexa("00112233445566778899aabbccddeeff");
			uuid = slice.ToUuid();
			Assert.That(uuid.ToString(), Is.EqualTo("00112233-4455-6677-8899-aabbccddeeff"), "slice.ToUuid() should preserve RFC 4122 ordering");

			// round-trip
			uuid = Uuid.NewUuid();
			Assert.That(Slice.FromUuid(uuid).ToUuid(), Is.EqualTo(uuid));

			Assert.That(Slice.FromAscii(uuid.ToString()).ToUuid(), Is.EqualTo(uuid), "String literals should also be converted if they match the expected format");

			Assert.That(() => Slice.FromAscii("random text").ToUuid(), Throws.InstanceOf<FormatException>());

			// should validate the arguments
			var x = Slice.FromUuid(uuid);
			Assert.That(() => MutateOffset(x, -1).ToUuid(), Throws.InstanceOf<FormatException>());
			Assert.That(() => MutateCount(x, 17).ToUuid(), Throws.InstanceOf<FormatException>());
			Assert.That(() => MutateArray(x, null).ToUuid(), Throws.InstanceOf<FormatException>());
		}

		[Test]
		public void Test_Slice_FromFixed32()
		{
			// FromFixed32 always produce 4 bytes and uses Little Endian

			Assert.That(Slice.FromFixed32(0).GetBytes(), Is.EqualTo(new byte[4]));
			Assert.That(Slice.FromFixed32(1).GetBytes(), Is.EqualTo(new byte[] { 1, 0, 0, 0 }));
			Assert.That(Slice.FromFixed32(256).GetBytes(), Is.EqualTo(new byte[] { 0, 1, 0, 0 }));
			Assert.That(Slice.FromFixed32(65536).GetBytes(), Is.EqualTo(new byte[] { 0, 0, 1, 0 }));
			Assert.That(Slice.FromFixed32(16777216).GetBytes(), Is.EqualTo(new byte[] { 0, 0, 0, 1 }));
			Assert.That(Slice.FromFixed32(short.MaxValue).GetBytes(), Is.EqualTo(new byte[] { 255, 127, 0, 0 }));
			Assert.That(Slice.FromFixed32(int.MaxValue).GetBytes(), Is.EqualTo(new byte[] { 255, 255, 255, 127 }));

			Assert.That(Slice.FromFixed32(-1).GetBytes(), Is.EqualTo(new byte[] { 255, 255, 255, 255 }));
			Assert.That(Slice.FromFixed32(-256).GetBytes(), Is.EqualTo(new byte[] { 0, 255, 255, 255 }));
			Assert.That(Slice.FromFixed32(-65536).GetBytes(), Is.EqualTo(new byte[] { 0, 0, 255, 255 }));
			Assert.That(Slice.FromFixed32(-16777216).GetBytes(), Is.EqualTo(new byte[] { 0, 0, 0, 255 }));
			Assert.That(Slice.FromFixed32(int.MinValue).GetBytes(), Is.EqualTo(new byte[] { 0, 0, 0, 128 }));

			var rnd = new Random();
			for (int i = 0; i < 1000; i++)
			{
				int x = rnd.Next() * (rnd.Next(2) == 0 ? +1 : -1);
				Slice s = Slice.FromFixed32(x);
				Assert.That(s.Count, Is.EqualTo(4));
				Assert.That(s.ToInt32(), Is.EqualTo(x));
			}
		}

		[Test]
		public void Test_Slice_FromFixed64()
		{
			// FromFixed64 always produce 8 bytes and uses Little Endian

			Assert.That(Slice.FromFixed64(0L).GetBytes(), Is.EqualTo(new byte[8]));
			Assert.That(Slice.FromFixed64(1L).GetBytes(), Is.EqualTo(new byte[] { 1, 0, 0, 0, 0, 0, 0, 0 }));
			Assert.That(Slice.FromFixed64(1L << 8).GetBytes(), Is.EqualTo(new byte[] { 0, 1, 0, 0, 0, 0, 0, 0 }));
			Assert.That(Slice.FromFixed64(1L << 16).GetBytes(), Is.EqualTo(new byte[] { 0, 0, 1, 0, 0, 0, 0, 0 }));
			Assert.That(Slice.FromFixed64(1L << 24).GetBytes(), Is.EqualTo(new byte[] { 0, 0, 0, 1, 0, 0, 0, 0 }));
			Assert.That(Slice.FromFixed64(1L << 32).GetBytes(), Is.EqualTo(new byte[] { 0, 0, 0, 0, 1, 0, 0, 0 }));
			Assert.That(Slice.FromFixed64(1L << 40).GetBytes(), Is.EqualTo(new byte[] { 0, 0, 0, 0, 0, 1, 0, 0 }));
			Assert.That(Slice.FromFixed64(1L << 48).GetBytes(), Is.EqualTo(new byte[] { 0, 0, 0, 0, 0, 0, 1, 0 }));
			Assert.That(Slice.FromFixed64(1L << 56).GetBytes(), Is.EqualTo(new byte[] { 0, 0, 0, 0, 0, 0, 0, 1 }));
			Assert.That(Slice.FromFixed64(short.MaxValue).GetBytes(), Is.EqualTo(new byte[] { 255, 127, 0, 0, 0, 0, 0, 0 }));
			Assert.That(Slice.FromFixed64(int.MaxValue).GetBytes(), Is.EqualTo(new byte[] { 255, 255, 255, 127, 0, 0, 0, 0 }));
			Assert.That(Slice.FromFixed64(long.MaxValue).GetBytes(), Is.EqualTo(new byte[] { 255, 255, 255, 255, 255, 255, 255, 127 }));

			Assert.That(Slice.FromFixed64(-1L).GetBytes(), Is.EqualTo(new byte[] { 255, 255, 255, 255, 255, 255, 255, 255 }));
			Assert.That(Slice.FromFixed64(-256L).GetBytes(), Is.EqualTo(new byte[] { 0, 255, 255, 255, 255, 255, 255, 255 }));
			Assert.That(Slice.FromFixed64(-65536L).GetBytes(), Is.EqualTo(new byte[] { 0, 0, 255, 255, 255, 255, 255, 255 }));
			Assert.That(Slice.FromFixed64(-16777216L).GetBytes(), Is.EqualTo(new byte[] { 0, 0, 0, 255, 255, 255, 255, 255 }));
			Assert.That(Slice.FromFixed64(-4294967296L).GetBytes(), Is.EqualTo(new byte[] { 0, 0, 0, 0, 255, 255, 255, 255 }));
			Assert.That(Slice.FromFixed64(long.MinValue).GetBytes(), Is.EqualTo(new byte[] { 0, 0, 0, 0, 0, 0, 0, 128 }));

			var rnd = new Random();
			for (int i = 0; i < 1000; i++)
			{
				long x = (long)rnd.Next() * rnd.Next() * (rnd.Next(2) == 0 ? +1 : -1);
				Slice s = Slice.FromFixed64(x);
				Assert.That(s.Count, Is.EqualTo(8));
				Assert.That(s.ToInt64(), Is.EqualTo(x));
			}
		}

		[Test]
		public void Test_Slice_FromFixedU64()
		{
			// FromFixed64 always produce 8 bytes and uses Little Endian

			Assert.That(Slice.FromFixedU64(0UL).GetBytes(), Is.EqualTo(new byte[8]));
			Assert.That(Slice.FromFixedU64(1UL).GetBytes(), Is.EqualTo(new byte[] { 1, 0, 0, 0, 0, 0, 0, 0 }));
			Assert.That(Slice.FromFixedU64(1UL << 8).GetBytes(), Is.EqualTo(new byte[] { 0, 1, 0, 0, 0, 0, 0, 0 }));
			Assert.That(Slice.FromFixedU64(1UL << 16).GetBytes(), Is.EqualTo(new byte[] { 0, 0, 1, 0, 0, 0, 0, 0 }));
			Assert.That(Slice.FromFixedU64(1UL << 24).GetBytes(), Is.EqualTo(new byte[] { 0, 0, 0, 1, 0, 0, 0, 0 }));
			Assert.That(Slice.FromFixedU64(1UL << 32).GetBytes(), Is.EqualTo(new byte[] { 0, 0, 0, 0, 1, 0, 0, 0 }));
			Assert.That(Slice.FromFixedU64(1UL << 40).GetBytes(), Is.EqualTo(new byte[] { 0, 0, 0, 0, 0, 1, 0, 0 }));
			Assert.That(Slice.FromFixedU64(1UL << 48).GetBytes(), Is.EqualTo(new byte[] { 0, 0, 0, 0, 0, 0, 1, 0 }));
			Assert.That(Slice.FromFixedU64(1UL << 56).GetBytes(), Is.EqualTo(new byte[] { 0, 0, 0, 0, 0, 0, 0, 1 }));
			Assert.That(Slice.FromFixedU64(ushort.MaxValue).GetBytes(), Is.EqualTo(new byte[] { 255, 255, 0, 0, 0, 0, 0, 0 }));
			Assert.That(Slice.FromFixedU64(int.MaxValue).GetBytes(), Is.EqualTo(new byte[] { 255, 255, 255, 127, 0, 0, 0, 0 }));
			Assert.That(Slice.FromFixedU64(uint.MaxValue).GetBytes(), Is.EqualTo(new byte[] { 255, 255, 255, 255, 0, 0, 0, 0 }));
			Assert.That(Slice.FromFixedU64(long.MaxValue).GetBytes(), Is.EqualTo(new byte[] { 255, 255, 255, 255, 255, 255, 255, 127 }));
			Assert.That(Slice.FromFixedU64(ulong.MaxValue).GetBytes(), Is.EqualTo(new byte[] { 255, 255, 255, 255, 255, 255, 255, 255 }));

			var rnd = new Random();
			for (int i = 0; i < 1000; i++)
			{
				ulong x = (ulong)rnd.Next() * (ulong)rnd.Next();
				Slice s = Slice.FromFixedU64(x);
				Assert.That(s.Count, Is.EqualTo(8));
				Assert.That(s.ToUInt64(), Is.EqualTo(x));
			}
		}

		[Test]
		public void Test_Slice_Equality()
		{

			var a = Slice.Create(new byte[] { 1, 2, 3 });
			var b = Slice.Create(new byte[] { 1, 2, 3 });
			var c = Slice.Create(new byte[] { 0, 1, 2, 3, 4 }, 1, 3);
			var x = Slice.Create(new byte[] { 4, 5, 6 });
			var y = Slice.Create(new byte[] { 1, 2, 3 }, 0, 2);
			var z = Slice.Create(new byte[] { 1, 2, 3, 4 });

			// equals
			Assert.That(a, Is.EqualTo(a));
			Assert.That(a, Is.EqualTo(b));
			Assert.That(a, Is.EqualTo(c));
			Assert.That(b, Is.EqualTo(a));
			Assert.That(b, Is.EqualTo(b));
			Assert.That(b, Is.EqualTo(c));
			Assert.That(c, Is.EqualTo(a));
			Assert.That(c, Is.EqualTo(b));
			Assert.That(c, Is.EqualTo(c));

			// not equals
			Assert.That(a, Is.Not.EqualTo(x));
			Assert.That(a, Is.Not.EqualTo(y));
			Assert.That(a, Is.Not.EqualTo(z));
		}

		[Test]
		public void Test_Slice_Equality_Corner_Cases()
		{
			Assert.That(Slice.Create(null), Is.EqualTo(Slice.Nil));
			Assert.That(Slice.Create(new byte[0]), Is.EqualTo(Slice.Empty));
			
			Assert.That(Slice.Create(null) == Slice.Nil, Is.True, "null == Nil");
			Assert.That(Slice.Create(null) == Slice.Empty, Is.False, "null != Empty");
			Assert.That(Slice.Create(new byte[0]) == Slice.Empty, Is.True, "[0] == Empty");
			Assert.That(Slice.Create(new byte[0]) == Slice.Nil, Is.False, "[0] != Nill");

			// "slice == null" should be the equivalent to "slice.IsNull" so only true for Slice.Nil
			Assert.That(Slice.Nil == null, Is.True, "'Slice.Nil == null' is true");
			Assert.That(Slice.Empty == null, Is.False, "'Slice.Empty == null' is false");
			Assert.That(Slice.FromByte(1) == null, Is.False, "'{1} == null' is false");
			Assert.That(null == Slice.Nil, Is.True, "'Slice.Nil == null' is true");
			Assert.That(null == Slice.Empty, Is.False, "'Slice.Empty == null' is false");
			Assert.That(null == Slice.FromByte(1), Is.False, "'{1} == null' is false");

			// "slice != null" should be the equivalent to "slice.HasValue" so only false for Slice.Nil
			Assert.That(Slice.Nil != null, Is.False, "'Slice.Nil != null' is false");
			Assert.That(Slice.Empty != null, Is.True, "'Slice.Empty != null' is true");
			Assert.That(Slice.FromByte(1) != null, Is.True, "'{1} != null' is true");
			Assert.That(null != Slice.Nil, Is.False, "'Slice.Nil != null' is false");
			Assert.That(null != Slice.Empty, Is.True, "'Slice.Empty != null' is true");
			Assert.That(null != Slice.FromByte(1), Is.True, "'{1} != null' is true");
		}

		[Test]
		public void Test_Slice_Equality_TwoByteArrayWithSameContentShouldReturnTrue()
		{
			var s1 = Slice.FromAscii("abcd");
			var s2 = Slice.FromAscii("abcd");
			Assert.IsTrue(s1.Equals(s2), "'abcd' should equals 'abcd'");
		}

		[Test]
		public void Test_Slice_Equality_TwoByteArrayWithSameContentFromSameOriginalBufferShouldReturnTrue()
		{
			var origin = System.Text.Encoding.ASCII.GetBytes("abcdabcd");
			var a1 = new ArraySegment<byte>(origin, 0, 4); //"abcd", refer first part of origin buffer
			var s1 = Slice.Create(a1); //
			var a2 = new ArraySegment<byte>(origin, 4, 4);//"abcd", refer second part of origin buffer
			var s2 = Slice.Create(a2);
			Assert.IsTrue(s1.Equals(s2), "'abcd' should equals 'abcd'");
		}

		[Test]
		public void Test_Slice_Equality_Malformed()
		{
			var good = Slice.FromAscii("good");
			var evil = Slice.FromAscii("evil");

			// argument should be validated
			Assert.That(() => good.Equals(MutateOffset(evil, -1)), Throws.InstanceOf<FormatException>());
			Assert.That(() => good.Equals(MutateCount(evil, 666)), Throws.InstanceOf<FormatException>());
			Assert.That(() => good.Equals(MutateArray(evil, null)), Throws.InstanceOf<FormatException>());
			Assert.That(() => good.Equals(MutateOffset(MutateCount(evil, 5), -1)), Throws.InstanceOf<FormatException>());

			// instance should also be validated
			Assert.That(() => MutateOffset(evil, -1).Equals(good), Throws.InstanceOf<FormatException>());
			Assert.That(() => MutateCount(evil, 666).Equals(good), Throws.InstanceOf<FormatException>());
			Assert.That(() => MutateArray(evil, null).Equals(good), Throws.InstanceOf<FormatException>());
			Assert.That(() => MutateOffset(MutateCount(evil, 5), -1).Equals(good), Throws.InstanceOf<FormatException>());
		}

		[Test]
		public void Test_Slice_Hash_Code()
		{
			// note: the test values MAY change if the hashcode algorithm is modified.
			// That means that if all the asserts in this test fail, you should probably ensure that the expected results are still valid.

			Assert.That(Slice.Nil.GetHashCode(), Is.EqualTo(0), "Nil hashcode should always be 0");
			Assert.That(Slice.Empty.GetHashCode(), Is.Not.EqualTo(0), "Empty hashcode should not be equal to 0");

			Assert.That(Slice.FromString("abc").GetHashCode(), Is.EqualTo(Slice.FromString("abc").GetHashCode()), "Hashcode should not depend on the backing array");
			Assert.That(Slice.FromString("zabcz").Substring(1, 3).GetHashCode(), Is.EqualTo(Slice.FromString("abc").GetHashCode()), "Hashcode should not depend on the offset in the array");
			Assert.That(Slice.FromString("abc").GetHashCode(), Is.Not.EqualTo(Slice.FromString("abcd").GetHashCode()), "Hashcode should include all the bytes");

			// should validate the arguments
			var x = Slice.FromString("evil");
			Assert.That(() => MutateOffset(x, -1).GetHashCode(), Throws.InstanceOf<FormatException>());
			Assert.That(() => MutateCount(x, 17).GetHashCode(), Throws.InstanceOf<FormatException>());
			Assert.That(() => MutateArray(x, null).GetHashCode(), Throws.InstanceOf<FormatException>());
		}

		[Test]
		public void Test_Slice_Comparison()
		{
			var a = Slice.FromAscii("a");
			var ab = Slice.FromAscii("ab");
			var abc = Slice.FromAscii("abc");
			var abc2 = Slice.FromAscii("abc"); // same bytes but different buffer
			var b = Slice.FromAscii("b");

			// a = b
			Assert.That(a.CompareTo(a), Is.EqualTo(0));
			Assert.That(ab.CompareTo(ab), Is.EqualTo(0));
			Assert.That(abc.CompareTo(abc), Is.EqualTo(0));
			Assert.That(abc.CompareTo(abc2), Is.EqualTo(0));

			// a < b
			Assert.That(a.CompareTo(b), Is.LessThan(0));
			Assert.That(a.CompareTo(ab), Is.LessThan(0));
			Assert.That(a.CompareTo(abc), Is.LessThan(0));

			// a > b
			Assert.That(b.CompareTo(a), Is.GreaterThan(0));
			Assert.That(b.CompareTo(ab), Is.GreaterThan(0));
			Assert.That(b.CompareTo(abc), Is.GreaterThan(0));
	
		}

		[Test]
		public void Test_Slice_Comparison_Corner_Cases()
		{
			// Nil == Empty
			Assert.That(Slice.Nil.CompareTo(Slice.Nil), Is.EqualTo(0));
			Assert.That(Slice.Empty.CompareTo(Slice.Empty), Is.EqualTo(0));
			Assert.That(Slice.Nil.CompareTo(Slice.Empty), Is.EqualTo(0));
			Assert.That(Slice.Empty.CompareTo(Slice.Nil), Is.EqualTo(0));

			// X > NULL, NULL < X
			var abc = Slice.FromAscii("abc");
			Assert.That(abc.CompareTo(Slice.Nil), Is.GreaterThan(0));
			Assert.That(abc.CompareTo(Slice.Empty), Is.GreaterThan(0));
			Assert.That(Slice.Nil.CompareTo(abc), Is.LessThan(0));
			Assert.That(Slice.Empty.CompareTo(abc), Is.LessThan(0));
		}

		[Test]
		public void Test_Slice_Comparison_Malformed()
		{
			var good = Slice.FromAscii("good");
			var evil = Slice.FromAscii("evil");

			// argument should be validated
			Assert.That(() => good.CompareTo(MutateOffset(evil, -1)), Throws.InstanceOf<FormatException>());
			Assert.That(() => good.CompareTo(MutateCount(evil, 666)), Throws.InstanceOf<FormatException>());
			Assert.That(() => good.CompareTo(MutateArray(evil, null)), Throws.InstanceOf<FormatException>());
			Assert.That(() => good.CompareTo(MutateOffset(MutateCount(evil, 5), -1)), Throws.InstanceOf<FormatException>());

			// instance should also be validated
			Assert.That(() => MutateOffset(evil, -1).CompareTo(good), Throws.InstanceOf<FormatException>());
			Assert.That(() => MutateCount(evil, 666).CompareTo(good), Throws.InstanceOf<FormatException>());
			Assert.That(() => MutateArray(evil, null).CompareTo(good), Throws.InstanceOf<FormatException>());
			Assert.That(() => MutateOffset(MutateCount(evil, 5), -1).CompareTo(good), Throws.InstanceOf<FormatException>());
		}

		private static readonly string UNICODE_TEXT = "Thïs Ïs à strîng thât contaÎns somé ùnicodè charactêrs and should be encoded in UTF-8: よろしくお願いします";
		private static readonly byte[] UNICODE_BYTES = Encoding.UTF8.GetBytes(UNICODE_TEXT);

		[Test]
		public void Test_Slice_FromStream()
		{
			Slice slice;

			using(var ms = new MemoryStream(UNICODE_BYTES))
			{
				slice = Slice.FromStream(ms);
			}
			Assert.That(slice.Count, Is.EqualTo(UNICODE_BYTES.Length));
			Assert.That(slice.GetBytes(), Is.EqualTo(UNICODE_BYTES));
			Assert.That(slice.ToUnicode(), Is.EqualTo(UNICODE_TEXT));

			Assert.That(() => Slice.FromStream(null), Throws.InstanceOf<ArgumentNullException>(), "Should throw if null");
			Assert.That(Slice.FromStream(Stream.Null), Is.EqualTo(Slice.Nil), "Stream.Null should return Slice.Nil");

			using(var ms = new MemoryStream())
			{
				ms.Close();
				Assert.That(() => Slice.FromStream(ms), Throws.InstanceOf<InvalidOperationException>(), "Reading from a disposed stream should throw");
			}
		}

		[Test]
		public async Task Test_Slice_FromStreamAsync()
		{
			Slice slice;

			// Reading from a MemoryStream should use the non-async path
			using (var ms = new MemoryStream(UNICODE_BYTES))
			{
				slice = await Slice.FromStreamAsync(ms, this.Cancellation);
			}
			Assert.That(slice.Count, Is.EqualTo(UNICODE_BYTES.Length));
			Assert.That(slice.GetBytes(), Is.EqualTo(UNICODE_BYTES));
			Assert.That(slice.ToUnicode(), Is.EqualTo(UNICODE_TEXT));

			// Reading from a FileStream should use the async path
			var tmp = Path.GetTempFileName();
			try
			{
				File.WriteAllBytes(tmp, UNICODE_BYTES);
				using(var fs = File.OpenRead(tmp))
				{
					slice = await Slice.FromStreamAsync(fs, this.Cancellation);
				}
			}
			finally
			{
				File.Delete(tmp);
			}

			Assert.That(slice.Count, Is.EqualTo(UNICODE_BYTES.Length));
			Assert.That(slice.GetBytes(), Is.EqualTo(UNICODE_BYTES));
			Assert.That(slice.ToUnicode(), Is.EqualTo(UNICODE_TEXT));
		}

		[Test]
		public void Test_SliceStream_Basics()
		{
			using (var stream = Slice.FromString(UNICODE_TEXT).AsStream())
			{
				Assert.That(stream, Is.Not.Null);
				Assert.That(stream.Length, Is.EqualTo(UNICODE_BYTES.Length));
				Assert.That(stream.Position, Is.EqualTo(0));

				Assert.That(stream.CanRead, Is.True);
				Assert.That(stream.CanWrite, Is.False);
				Assert.That(stream.CanSeek, Is.True);

				Assert.That(stream.CanTimeout, Is.False);
				Assert.That(() => stream.ReadTimeout, Throws.InstanceOf<InvalidOperationException>());
				Assert.That(() => stream.ReadTimeout = 123, Throws.InstanceOf<InvalidOperationException>());

				stream.Close();
				Assert.That(stream.Length, Is.EqualTo(0));
				Assert.That(stream.CanRead, Is.False);
				Assert.That(stream.CanSeek, Is.False);
			}
		}

		[Test]
		public void Test_SliceStream_ReadByte()
		{

			// ReadByte
			using (var stream = Slice.FromString(UNICODE_TEXT).AsStream())
			{
				var ms = new MemoryStream();
				int b;
				while ((b = stream.ReadByte()) >= 0)
				{
					ms.WriteByte((byte)b);
					Assert.That(ms.Length, Is.LessThanOrEqualTo(UNICODE_BYTES.Length));
				}
				Assert.That(ms.Length, Is.EqualTo(UNICODE_BYTES.Length));
				Assert.That(ms.ToArray(), Is.EqualTo(UNICODE_BYTES));
			}
		}

		[Test]
		public void Test_SliceStream_Read()
		{
			var rnd = new Random();

			// Read (all at once)
			using (var stream = Slice.FromString(UNICODE_TEXT).AsStream())
			{
				var buf = new byte[UNICODE_BYTES.Length];
				int readBytes = stream.Read(buf, 0, UNICODE_BYTES.Length);
				Assert.That(readBytes, Is.EqualTo(UNICODE_BYTES.Length));
				Assert.That(buf, Is.EqualTo(UNICODE_BYTES));
			}

			// Read (random chunks)
			for (int i = 0; i < 100; i++)
			{
				using (var stream = Slice.FromString(UNICODE_TEXT).AsStream())
				{
					var ms = new MemoryStream();

					int remaining = UNICODE_BYTES.Length;
					while (remaining > 0)
					{
						int chunkSize = 1 + rnd.Next(remaining - 1);
						var buf = new byte[chunkSize];

						int readBytes = stream.Read(buf, 0, chunkSize);
						Assert.That(readBytes, Is.EqualTo(chunkSize));

						ms.Write(buf, 0, buf.Length);
						remaining -= chunkSize;
					}

					Assert.That(ms.ToArray(), Is.EqualTo(UNICODE_BYTES));
				}
			}
		}

		[Test]
		public void Test_SliceStream_CopyTo()
		{
			// CopyTo
			using (var stream = Slice.FromString(UNICODE_TEXT).AsStream())
			{
				var ms = new MemoryStream();
				stream.CopyTo(ms);
				Assert.That(ms.Length, Is.EqualTo(UNICODE_BYTES.Length));
			}

		}
	
		[Test]
		public void Test_SliceListStream_Basics()
		{
			const int N = 65536;
			var rnd = new Random();
			Slice slice;

			// create a random buffer
			var bytes = new byte[N];
			rnd.NextBytes(bytes);

			// splits it in random slices
			var slices = new List<Slice>();
			int r = N;
			int p = 0;
			while(r > 0)
			{
				int sz = Math.Min(1 + rnd.Next(1024), r);
				slice = Slice.Create(bytes, p, sz);
				if (rnd.Next(2) == 1) slice = slice.Memoize();
				slices.Add(slice);

				p += sz;
				r -= sz;
			}
			Assert.That(slices.Sum(sl => sl.Count), Is.EqualTo(N));

			using(var stream = new SliceListStream(slices.ToArray()))
			{
				Assert.That(stream.Position, Is.EqualTo(0));
				Assert.That(stream.Length, Is.EqualTo(N));
				Assert.That(stream.CanRead, Is.True);
				Assert.That(stream.CanSeek, Is.True);
				Assert.That(stream.CanWrite, Is.False);
				Assert.That(stream.CanTimeout, Is.False);

				// CopyTo
				var ms = new MemoryStream();
				stream.CopyTo(ms);
				Assert.That(ms.ToArray(), Is.EqualTo(bytes));

				// Seek
				Assert.That(stream.Position, Is.EqualTo(N));
				Assert.That(stream.Seek(0, SeekOrigin.Begin), Is.EqualTo(0));
				Assert.That(stream.Position, Is.EqualTo(0));

				// Read All
				var buf = new byte[N];
				int readBytes = stream.Read(buf, 0, N);
				Assert.That(readBytes, Is.EqualTo(N));
				Assert.That(buf, Is.EqualTo(bytes));
			}
		}

		[Test]
		public void Test_Slice_Join_Array()
		{
			var a = Slice.FromString("A");
			var b = Slice.FromString("BB");
			var c = Slice.FromString("CCC");

			// empty separator should just join all slices together
			Assert.That(Slice.Join(Slice.Empty, new Slice[0]), Is.EqualTo(Slice.Empty));
			Assert.That(Slice.Join(Slice.Empty, new[] { Slice.Empty }), Is.EqualTo(Slice.Empty));
			Assert.That(Slice.Join(Slice.Empty, new[] { a }), Is.EqualTo(Slice.FromString("A")));
			Assert.That(Slice.Join(Slice.Empty, new[] { a, b }), Is.EqualTo(Slice.FromString("ABB")));
			Assert.That(Slice.Join(Slice.Empty, new[] { a, b, c }), Is.EqualTo(Slice.FromString("ABBCCC")));
			Assert.That(Slice.Join(Slice.Empty, new[] { a, b, c }).Offset, Is.EqualTo(0));
			Assert.That(Slice.Join(Slice.Empty, new[] { a, b, c }).Count, Is.EqualTo(6));

			// single byte separator
			var sep = Slice.FromChar(',');
			Assert.That(Slice.Join(sep, new Slice[0]), Is.EqualTo(Slice.Empty));
			Assert.That(Slice.Join(sep, new[] { Slice.Empty }), Is.EqualTo(Slice.Empty));
			Assert.That(Slice.Join(sep, new[] { a }), Is.EqualTo(Slice.FromString("A")));
			Assert.That(Slice.Join(sep, new[] { a, b }), Is.EqualTo(Slice.FromString("A,BB")));
			Assert.That(Slice.Join(sep, new[] { a, b, c }), Is.EqualTo(Slice.FromString("A,BB,CCC")));
			Assert.That(Slice.Join(sep, new[] { a, b, c }).Offset, Is.EqualTo(0));
			Assert.That(Slice.Join(sep, new[] { a, b, c }).Count, Is.EqualTo(8));
			Assert.That(Slice.Join(sep, new[] { a, Slice.Empty, c }), Is.EqualTo(Slice.FromString("A,,CCC")));
			Assert.That(Slice.Join(sep, new[] { Slice.Empty, b, c }), Is.EqualTo(Slice.FromString(",BB,CCC")));
			Assert.That(Slice.Join(sep, new[] { Slice.Empty, Slice.Empty, Slice.Empty }), Is.EqualTo(Slice.FromString(",,")));

			// multi byte separator, with a non-0 offset
			sep = Slice.FromString("!<@>!").Substring(1, 3);
			Assert.That(sep.Offset, Is.EqualTo(1));
			Assert.That(Slice.Join(sep, new Slice[0]), Is.EqualTo(Slice.Empty));
			Assert.That(Slice.Join(sep, new[] { Slice.Empty }), Is.EqualTo(Slice.Empty));
			Assert.That(Slice.Join(sep, new[] { a }), Is.EqualTo(Slice.FromString("A")));
			Assert.That(Slice.Join(sep, new[] { a, b }), Is.EqualTo(Slice.FromString("A<@>BB")));
			Assert.That(Slice.Join(sep, new[] { a, b, c }), Is.EqualTo(Slice.FromString("A<@>BB<@>CCC")));
			Assert.That(Slice.Join(sep, new[] { a, b, c }).Offset, Is.EqualTo(0));
			Assert.That(Slice.Join(sep, new[] { a, b, c }).Count, Is.EqualTo(12));

			// join slices that use the same underlying buffer
			string s = "hello world!!!";
			byte[] tmp = Encoding.UTF8.GetBytes(s);
			var slices = new Slice[tmp.Length];
			for (int i = 0; i < tmp.Length; i++) slices[i] = Slice.Create(tmp, i, 1);
			Assert.That(Slice.Join(Slice.Empty, slices), Is.EqualTo(Slice.FromString(s)));
			Assert.That(Slice.Join(Slice.FromChar(':'), slices), Is.EqualTo(Slice.FromString("h:e:l:l:o: :w:o:r:l:d:!:!:!")));
		}

		[Test]
		public void Test_Slice_Join_Enumerable()
		{
			var query = Enumerable.Range(1, 3).Select(c => Slice.FromString(new string((char)(64 + c), c)));

			Assert.That(Slice.Join(Slice.Empty, Enumerable.Empty<Slice>()), Is.EqualTo(Slice.Empty));
			Assert.That(Slice.Join(Slice.Empty, query), Is.EqualTo(Slice.FromString("ABBCCC")));
			Assert.That(Slice.Join(Slice.Empty, query).Offset, Is.EqualTo(0));
			Assert.That(Slice.Join(Slice.Empty, query).Count, Is.EqualTo(6));

			var sep = Slice.FromChar(',');
			Assert.That(Slice.Join(sep, Enumerable.Empty<Slice>()), Is.EqualTo(Slice.Empty));
			Assert.That(Slice.Join(sep, query), Is.EqualTo(Slice.FromString("A,BB,CCC")));
			Assert.That(Slice.Join(sep, query).Offset, Is.EqualTo(0));
			Assert.That(Slice.Join(sep, query).Count, Is.EqualTo(8));

			var arr = query.ToArray();
			Assert.That(Slice.Join(Slice.Empty, (IEnumerable<Slice>)arr), Is.EqualTo(Slice.FromString("ABBCCC")));
			Assert.That(Slice.Join(Slice.Empty, (IEnumerable<Slice>)arr).Offset, Is.EqualTo(0));
			Assert.That(Slice.Join(Slice.Empty, (IEnumerable<Slice>)arr).Count, Is.EqualTo(6));
			
		}

		[Test]
		public void Test_Slice_Split()
		{
			var a = Slice.FromString("A");
			var b = Slice.FromString("BB");
			var c = Slice.FromString("CCC");
			var comma = Slice.FromChar(',');

			Assert.That(Slice.FromString("A").Split(comma), Is.EqualTo(new[] { a }));
			Assert.That(Slice.FromString("A,BB").Split(comma), Is.EqualTo(new[] { a, b }));
			Assert.That(Slice.FromString("A,BB,CCC").Split(comma), Is.EqualTo(new[] { a, b, c }));

			// empty values should be kept or discarded, depending on the option settings
			Assert.That(Slice.FromString("A,,CCC").Split(comma, StringSplitOptions.None), Is.EqualTo(new[] { a, Slice.Empty, c }));
			Assert.That(Slice.FromString("A,,CCC").Split(comma, StringSplitOptions.RemoveEmptyEntries), Is.EqualTo(new[] { a, c }));

			// edge cases
			// > should behave the same as String.Split()
			Assert.That(Slice.Empty.Split(comma, StringSplitOptions.None), Is.EqualTo(new [] { Slice.Empty  }));
			Assert.That(Slice.Empty.Split(comma, StringSplitOptions.RemoveEmptyEntries), Is.EqualTo(new Slice[0]));
			Assert.That(Slice.FromString("A,").Split(comma, StringSplitOptions.None), Is.EqualTo(new[] { a, Slice.Empty }));
			Assert.That(Slice.FromString("A,").Split(comma, StringSplitOptions.RemoveEmptyEntries), Is.EqualTo(new [] { a }));
			Assert.That(Slice.FromString(",").Split(comma, StringSplitOptions.RemoveEmptyEntries), Is.EqualTo(new Slice[0]));
			Assert.That(Slice.FromString(",,,").Split(comma, StringSplitOptions.RemoveEmptyEntries), Is.EqualTo(new Slice[0]));

			// multi-bytes separator with an offset
			var sep = Slice.FromString("!<@>!").Substring(1, 3);
			Assert.That(Slice.FromString("A<@>BB<@>CCC").Split(sep), Is.EqualTo(new[] { a, b, c }));
		}

		[Test]
		public void Test_SliceHelpers_Align()
		{
			// Even though 0 is a multiple of 16, it is always rounded up to 16 to simplify buffer handling logic
			Assert.That(SliceHelpers.Align(0), Is.EqualTo(16));
			// 1..16 => 16
			for (int i = 1; i <= 16; i++) { Assert.That(SliceHelpers.Align(i), Is.EqualTo(16), "Align({0}) => 16", i); }
			// 17..32 => 32
			for (int i = 17; i <= 32; i++) { Assert.That(SliceHelpers.Align(i), Is.EqualTo(32), "Align({0}) => 32", i); }
			// 33..48 => 48
			for (int i = 33; i <= 48; i++) { Assert.That(SliceHelpers.Align(i), Is.EqualTo(48), "Align({0}) => 48", i); }

			// 2^N-1
			for (int i = 6; i < 30; i++)
			{
				Assert.That(SliceHelpers.Align((1 << i) - 1), Is.EqualTo(1 << i));
			}
			// largest non overflowing
			Assert.That(() => SliceHelpers.Align(int.MaxValue - 15), Is.EqualTo((int.MaxValue - 15)));

			// overflow
			Assert.That(() => SliceHelpers.Align(int.MaxValue), Throws.InstanceOf<OverflowException>());
			Assert.That(() => SliceHelpers.Align(int.MaxValue - 14), Throws.InstanceOf<OverflowException>());

			// negative values
			Assert.That(() => SliceHelpers.Align(-1), Throws.InstanceOf<ArgumentOutOfRangeException>());
			Assert.That(() => SliceHelpers.Align(int.MinValue), Throws.InstanceOf<ArgumentOutOfRangeException>());
		}

		[Test]
		public void Test_SliceHelpers_NextPowerOfTwo()
		{
			// 0 is a special case, to simplify bugger handling logic
			Assert.That(SliceHelpers.NextPowerOfTwo(0), Is.EqualTo(1), "Special case for 0");
			Assert.That(SliceHelpers.NextPowerOfTwo(1), Is.EqualTo(1));
			Assert.That(SliceHelpers.NextPowerOfTwo(2), Is.EqualTo(2));

			for (int i = 2; i < 31; i++)
			{
				Assert.That(SliceHelpers.NextPowerOfTwo((1 << i) - 1), Is.EqualTo(1 << i));
				Assert.That(SliceHelpers.NextPowerOfTwo(1 << i), Is.EqualTo(1 << i));
			}
		}

		[Test]
		public void Test_SliceHelpers_ComputeHashCode()
		{
			//note: if everything fails, check that the hashcode algorithm hasn't changed also !

			Assert.That(SliceHelpers.ComputeHashCode(new byte[0], 0, 0), Is.EqualTo(-2128831035));
			Assert.That(SliceHelpers.ComputeHashCode(new byte[1], 0, 1), Is.EqualTo(84696351));
			Assert.That(SliceHelpers.ComputeHashCode(new byte[2], 0, 1), Is.EqualTo(84696351));
			Assert.That(SliceHelpers.ComputeHashCode(new byte[2], 1, 1), Is.EqualTo(84696351));
			Assert.That(SliceHelpers.ComputeHashCode(new byte[2], 0, 2), Is.EqualTo(292984781));
			Assert.That(SliceHelpers.ComputeHashCode(Encoding.Default.GetBytes("hello"), 0, 5), Is.EqualTo(1335831723));

			Assert.That(SliceHelpers.ComputeHashCodeUnsafe(new byte[0], 0, 0), Is.EqualTo(-2128831035));
			Assert.That(SliceHelpers.ComputeHashCodeUnsafe(new byte[1], 0, 1), Is.EqualTo(84696351));
			Assert.That(SliceHelpers.ComputeHashCodeUnsafe(new byte[2], 0, 1), Is.EqualTo(84696351));
			Assert.That(SliceHelpers.ComputeHashCodeUnsafe(new byte[2], 1, 1), Is.EqualTo(84696351));
			Assert.That(SliceHelpers.ComputeHashCodeUnsafe(new byte[2], 0, 2), Is.EqualTo(292984781));
			Assert.That(SliceHelpers.ComputeHashCodeUnsafe(Encoding.Default.GetBytes("hello"), 0, 5), Is.EqualTo(1335831723));
		}

		#region Black Magic Incantations...

		// The Slice struct is not blittable, so we can't take its address and modify it via pointers trickery.
		// Since its ctor is checking the arguments in Debug mode and all its fields are readonly, the only way to inject bad values is to use reflection.

		private static Slice MutateOffset(Slice value, int offset)
		{
			// Don't try this at home !
			object tmp = value;
			typeof(Slice).GetField("Offset").SetValue(tmp, offset);
			return (Slice)tmp;
		}

		private static Slice MutateCount(Slice value, int offset)
		{
			// Don't try this at home !
			object tmp = value;
			typeof(Slice).GetField("Offset").SetValue(tmp, offset);
			return (Slice)tmp;
		}

		private static Slice MutateArray(Slice value, byte[] array)
		{
			// Don't try this at home !
			object tmp = value;
			typeof(Slice).GetField("Array").SetValue(tmp, array);
			return (Slice)tmp;
		}

		#endregion

	}
}
