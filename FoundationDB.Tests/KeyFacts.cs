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

	[TestFixture]
	public class KeyFacts
	{

		[Test]
		public void Test_FdbKey_Increment()
		{

			var key = FdbKey.Increment(FdbKey.Ascii("Hello"));
			Assert.That(FdbKey.Ascii(key), Is.EqualTo("Hellp"));

			key = FdbKey.Increment(FdbKey.Ascii("Hello\x00"));
			Assert.That(FdbKey.Ascii(key), Is.EqualTo("Hello\x01"));

			key = FdbKey.Increment(FdbKey.Ascii("Hello\xFE"));
			Assert.That(FdbKey.Ascii(key), Is.EqualTo("Hello\xFF"));

			key = FdbKey.Increment(FdbKey.Ascii("Hello\xFF"));
			Assert.That(FdbKey.Ascii(key), Is.EqualTo("Hellp\x00"));

			key = FdbKey.Increment(FdbKey.Ascii("A\xFF\xFF\xFF"));
			Assert.That(FdbKey.Ascii(key), Is.EqualTo("B\x00\x00\x00"));

		}

		[Test]
		public void Test_FdbKey_AreEqual()
		{
			Assert.That(FdbKey.Ascii("Hello").Equals(FdbKey.Ascii("Hello")), Is.True);
			Assert.That(FdbKey.Ascii("Hello") == FdbKey.Ascii("Hello"), Is.True);

			Assert.That(FdbKey.Ascii("Hello").Equals(FdbKey.Ascii("Helloo")), Is.False);
			Assert.That(FdbKey.Ascii("Hello") == FdbKey.Ascii("Helloo"), Is.False);
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


	}
}
