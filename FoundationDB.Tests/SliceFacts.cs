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
	using System.Text;

	[TestFixture]
	public class SliceFacts
	{

		[Test]
		public void Test_Slice_Nil()
		{
			// Slice.Nil is the equivalent of 'default(byte[])'

			Assert.That(Slice.Nil.Count, Is.EqualTo(0));
			Assert.That(Slice.Nil.Offset, Is.EqualTo(0));
			Assert.That(Slice.Nil.Array, Is.Null);

			Assert.That(Slice.Nil.IsNullOrEmpty, Is.True);
			Assert.That(Slice.Nil.HasValue, Is.False);

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

			Assert.That(Slice.Empty.IsNullOrEmpty, Is.True);
			Assert.That(Slice.Empty.HasValue, Is.True);

			Assert.That(Slice.Empty.GetBytes(), Is.EqualTo(new byte[0]));
			Assert.That(Slice.Empty.ToAscii(), Is.EqualTo(String.Empty));
			Assert.That(Slice.Empty.ToUnicode(), Is.EqualTo(String.Empty));
			Assert.That(Slice.Empty.ToAsciiOrHexaString(), Is.EqualTo("''"));
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
			Assert.That(Slice.Create(new byte[0]).GetBytes(), Is.EqualTo(new byte[0]));
			Assert.That(Slice.Create(new byte[] { 1, 2, 3 }).GetBytes(), Is.EqualTo(new byte[] { 1, 2, 3 }));

			// the array return by GetBytes() should not be the same array that was passed to Create !
			byte[] tmp = Guid.NewGuid().ToByteArray(); // create a 16-byte array
			var slice = Slice.Create(tmp);
			// they should be equal, but not the same !
			Assert.That(slice.GetBytes(), Is.EqualTo(tmp));
			Assert.That(slice.GetBytes(), Is.Not.SameAs(tmp));
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
		}
	}
}
