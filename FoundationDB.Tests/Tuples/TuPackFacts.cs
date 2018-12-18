﻿#region BSD License
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

// ReSharper disable AccessToModifiedClosure
namespace Doxense.Collections.Tuples.Tests
{
	using System;
	using System.Collections.Generic;
	using System.Diagnostics;
	using System.Linq;
	using System.Net;
	using Doxense.Collections.Tuples.Encoding;
	using FoundationDB.Client.Tests;
	using NUnit.Framework;

	[TestFixture]
	public class TuPackFacts : FdbTest
	{

		#region Serialization...

		[Test]
		public void Test_TuplePack_Serialize_Bytes()
		{
			// Byte arrays are stored with prefix '01' followed by the bytes, and terminated by '00'. All occurrences of '00' in the byte array are escaped with '00 FF'
			// - Best case:  packed_size = 2 + array_len
			// - Worst case: packed_size = 2 + array_len * 2

			Assert.That(TuPack.EncodeKey(new byte[] { 0x12, 0x34, 0x56, 0x78, 0x9A, 0xBC, 0xDE, 0xF0 }).ToString(), Is.EqualTo("<01><12>4Vx<9A><BC><DE><F0><00>"));
			Assert.That(TuPack.Pack(ValueTuple.Create(new byte[] { 0x12, 0x34, 0x56, 0x78, 0x9A, 0xBC, 0xDE, 0xF0 })).ToString(), Is.EqualTo("<01><12>4Vx<9A><BC><DE><F0><00>"));

			Assert.That(TuPack.EncodeKey(new byte[] { 0x12, 0x34, 0x56, 0x78, 0x9A, 0xBC, 0xDE, 0xF0 }.AsSlice()).ToString(), Is.EqualTo("<01><12>4Vx<9A><BC><DE><F0><00>"));
			Assert.That(TuPack.Pack(ValueTuple.Create(new byte[] { 0x12, 0x34, 0x56, 0x78, 0x9A, 0xBC, 0xDE, 0xF0 }.AsSlice())).ToString(), Is.EqualTo("<01><12>4Vx<9A><BC><DE><F0><00>"));

			Assert.That(TuPack.EncodeKey(new byte[] { 0x00, 0x42 }).ToString(), Is.EqualTo("<01><00><FF>B<00>"));
			Assert.That(TuPack.EncodeKey(new byte[] { 0x42, 0x00 }).ToString(), Is.EqualTo("<01>B<00><FF><00>"));
			Assert.That(TuPack.EncodeKey(new byte[] { 0x42, 0x00, 0x42 }).ToString(), Is.EqualTo("<01>B<00><FF>B<00>"));
			Assert.That(TuPack.EncodeKey(new byte[] { 0x42, 0x00, 0x00, 0x42 }).ToString(), Is.EqualTo("<01>B<00><FF><00><FF>B<00>"));
		}

		[Test]
		public void Test_TuplePack_Deserialize_Bytes()
		{
			IVarTuple t;

			t = TuPack.Unpack(Slice.Unescape("<01><01><23><45><67><89><AB><CD><EF><00>"));
			Assert.That(t.Get<byte[]>(0), Is.EqualTo(new byte[] {0x01, 0x23, 0x45, 0x67, 0x89, 0xAB, 0xCD, 0xEF}));
			Assert.That(t.Get<Slice>(0).ToHexaString(' '), Is.EqualTo("01 23 45 67 89 AB CD EF"));

			t = TuPack.Unpack(Slice.Unescape("<01><42><00><FF><00>"));
			Assert.That(t.Get<byte[]>(0), Is.EqualTo(new byte[] {0x42, 0x00}));
			Assert.That(t.Get<Slice>(0).ToHexaString(' '), Is.EqualTo("42 00"));

			t = TuPack.Unpack(Slice.Unescape("<01><00><FF><42><00>"));
			Assert.That(t.Get<byte[]>(0), Is.EqualTo(new byte[] {0x00, 0x42}));
			Assert.That(t.Get<Slice>(0).ToHexaString(' '), Is.EqualTo("00 42"));

			t = TuPack.Unpack(Slice.Unescape("<01><42><00><FF><42><00>"));
			Assert.That(t.Get<byte[]>(0), Is.EqualTo(new byte[] {0x42, 0x00, 0x42}));
			Assert.That(t.Get<Slice>(0).ToHexaString(' '), Is.EqualTo("42 00 42"));

			t = TuPack.Unpack(Slice.Unescape("<01><42><00><FF><00><FF><42><00>"));
			Assert.That(t.Get<byte[]>(0), Is.EqualTo(new byte[] {0x42, 0x00, 0x00, 0x42}));
			Assert.That(t.Get<Slice>(0).ToHexaString(' '), Is.EqualTo("42 00 00 42"));

			Assert.That(TuPack.DecodeKey<byte[]>(Slice.Unescape("<01><01><23><45><67><89><AB><CD><EF><00>")), Is.EqualTo(new byte[] { 0x01, 0x23, 0x45, 0x67, 0x89, 0xAB, 0xCD, 0xEF }));
			Assert.That(TuPack.DecodeKey<Slice>(Slice.Unescape("<01><01><23><45><67><89><AB><CD><EF><00>")), Is.EqualTo(new byte[] { 0x01, 0x23, 0x45, 0x67, 0x89, 0xAB, 0xCD, 0xEF }.AsSlice()));
		}

		[Test]
		public void Test_TuplePack_Serialize_Unicode_Strings()
		{
			// Unicode strings are stored with prefix '02' followed by the utf8 bytes, and terminated by '00'. All occurrences of '00' in the UTF8 bytes are escaped with '00 FF'

			// simple string
			Assert.That(TuPack.EncodeKey("hello world").ToString(), Is.EqualTo("<02>hello world<00>"));
			Assert.That(TuPack.Pack(ValueTuple.Create("hello world")).ToString(), Is.EqualTo("<02>hello world<00>"));

			// empty
			Assert.That(TuPack.EncodeKey(String.Empty).ToString(), Is.EqualTo("<02><00>"));

			// null
			Assert.That(TuPack.EncodeKey(default(string)).ToString(), Is.EqualTo("<00>"));

			// unicode
			// note: Encoding.UTF8.GetBytes("こんにちは世界") => { e3 81 93 e3 82 93 e3 81 ab e3 81 a1 e3 81 af e4 b8 96 e7 95 8c }
			Assert.That(TuPack.EncodeKey("こんにちは世界").ToString(), Is.EqualTo("<02><E3><81><93><E3><82><93><E3><81><AB><E3><81><A1><E3><81><AF><E4><B8><96><E7><95><8C><00>"));
		}

		[Test]
		public void Test_TuplePack_Deserialize_Unicode_Strings()
		{
			IVarTuple t;

			// simple string
			t = TuPack.Unpack(Slice.Unescape("<02>hello world<00>"));
			Assert.That(t.Get<string>(0), Is.EqualTo("hello world"));
			Assert.That(t[0], Is.EqualTo("hello world"));

			// empty
			t = TuPack.Unpack(Slice.Unescape("<02><00>"));
			Assert.That(t.Get<string>(0), Is.EqualTo(String.Empty));
			Assert.That(t[0], Is.EqualTo(String.Empty));

			// null
			t = TuPack.Unpack(Slice.Unescape("<00>"));
			Assert.That(t.Get<string>(0), Is.EqualTo(default(string)));
			Assert.That(t[0], Is.Null);

			// unicode
			t = TuPack.Unpack(Slice.Unescape("<02><E3><81><93><E3><82><93><E3><81><AB><E3><81><A1><E3><81><AF><E4><B8><96><E7><95><8C><00>"));
			// note: Encoding.UTF8.GetString({ e3 81 93 e3 82 93 e3 81 ab e3 81 a1 e3 81 af e4 b8 96 e7 95 8c }) => "こんにちは世界"
			Assert.That(t.Get<string>(0), Is.EqualTo("こんにちは世界"));
			Assert.That(t[0], Is.EqualTo("こんにちは世界"));

			Assert.That(TuPack.DecodeKey<string>(Slice.Unescape("<02>hello world<00>")), Is.EqualTo("hello world"));
		}

		[Test]
		public void Test_TuplePack_Serialize_Guids()
		{
			// 128-bit Guids are stored with prefix '30' followed by 16 bytes formatted according to RFC 4122

			// System.Guid are stored in Little-Endian, but RFC 4122's UUIDs are stored in Big Endian, so per convention we will swap them

			// note: new Guid(bytes from 0 to 15) => "03020100-0504-0706-0809-0a0b0c0d0e0f";
			Assert.That(TuPack.EncodeKey(Guid.Parse("00010203-0405-0607-0809-0a0b0c0d0e0f")).ToString(), Is.EqualTo("0<00><01><02><03><04><05><06><07><08><09><0A><0B><0C><0D><0E><0F>"));
			Assert.That(TuPack.Pack(ValueTuple.Create(Guid.Parse("00010203-0405-0607-0809-0a0b0c0d0e0f"))).ToString(), Is.EqualTo("0<00><01><02><03><04><05><06><07><08><09><0A><0B><0C><0D><0E><0F>"));

			Assert.That(TuPack.EncodeKey(Guid.Empty).ToString(), Is.EqualTo("0<00><00><00><00><00><00><00><00><00><00><00><00><00><00><00><00>"));

		}

		[Test]
		public void Test_TuplePack_Deserialize_Guids()
		{
			// 128-bit Guids are stored with prefix '30' followed by 16 bytes
			// we also accept byte arrays (prefix '01') if they are of length 16

			IVarTuple packed;

			packed = TuPack.Unpack(Slice.Unescape("<30><00><01><02><03><04><05><06><07><08><09><0A><0B><0C><0D><0E><0F>"));
			Assert.That(packed.Get<Guid>(0), Is.EqualTo(Guid.Parse("00010203-0405-0607-0809-0a0b0c0d0e0f")));
			Assert.That(packed[0], Is.EqualTo(Guid.Parse("00010203-0405-0607-0809-0a0b0c0d0e0f")));

			packed = TuPack.Unpack(Slice.Unescape("<30><00><00><00><00><00><00><00><00><00><00><00><00><00><00><00><00>"));
			Assert.That(packed.Get<Guid>(0), Is.EqualTo(Guid.Empty));
			Assert.That(packed[0], Is.EqualTo(Guid.Empty));

			// unicode string
			packed = TuPack.Unpack(Slice.Unescape("<02>03020100-0504-0706-0809-0a0b0c0d0e0f<00>"));
			Assert.That(packed.Get<Guid>(0), Is.EqualTo(Guid.Parse("03020100-0504-0706-0809-0a0b0c0d0e0f")));
			//note: t[0] returns a string, not a GUID

			// null maps to Guid.Empty
			packed = TuPack.Unpack(Slice.Unescape("<00>"));
			Assert.That(packed.Get<Guid>(0), Is.EqualTo(Guid.Empty));
			//note: t[0] returns null, not a GUID

			Assert.That(TuPack.DecodeKey<Guid>(Slice.Unescape("<30><00><01><02><03><04><05><06><07><08><09><0A><0B><0C><0D><0E><0F>")), Is.EqualTo(Guid.Parse("00010203-0405-0607-0809-0a0b0c0d0e0f")));
		}

		[Test]
		public void Test_TuplePack_Serialize_Uuid128s()
		{
			// UUID128s are stored with prefix '30' followed by 16 bytes formatted according to RFC 4122

			// note: new Uuid(bytes from 0 to 15) => "03020100-0504-0706-0809-0a0b0c0d0e0f";
			Assert.That(TuPack.EncodeKey(Uuid128.Parse("00010203-0405-0607-0809-0a0b0c0d0e0f")).ToString(), Is.EqualTo("0<00><01><02><03><04><05><06><07><08><09><0A><0B><0C><0D><0E><0F>"));
			Assert.That(TuPack.Pack(ValueTuple.Create(Uuid128.Parse("00010203-0405-0607-0809-0a0b0c0d0e0f"))).ToString(), Is.EqualTo("0<00><01><02><03><04><05><06><07><08><09><0A><0B><0C><0D><0E><0F>"));

			Assert.That(TuPack.EncodeKey(Uuid128.Empty).ToString(), Is.EqualTo("0<00><00><00><00><00><00><00><00><00><00><00><00><00><00><00><00>"));
		}

		[Test]
		public void Test_TuplePack_Deserialize_Uuid128s()
		{
			// UUID128s are stored with prefix '30' followed by 16 bytes (the result of uuid.ToByteArray())
			// we also accept byte arrays (prefix '01') if they are of length 16

			IVarTuple packed;

			// note: new Uuid(bytes from 0 to 15) => "00010203-0405-0607-0809-0a0b0c0d0e0f";
			packed = TuPack.Unpack(Slice.Unescape("<30><00><01><02><03><04><05><06><07><08><09><0A><0B><0C><0D><0E><0F>"));
			Assert.That(packed.Get<Uuid128>(0), Is.EqualTo(Uuid128.Parse("00010203-0405-0607-0809-0a0b0c0d0e0f")));
			Assert.That(packed[0], Is.EqualTo(Uuid128.Parse("00010203-0405-0607-0809-0a0b0c0d0e0f")));

			packed = TuPack.Unpack(Slice.Unescape("<30><00><00><00><00><00><00><00><00><00><00><00><00><00><00><00><00>"));
			Assert.That(packed.Get<Uuid128>(0), Is.EqualTo(Uuid128.Empty));
			Assert.That(packed[0], Is.EqualTo(Uuid128.Empty));

			// unicode string
			packed = TuPack.Unpack(Slice.Unescape("<02>00010203-0405-0607-0809-0a0b0c0d0e0f<00>"));
			Assert.That(packed.Get<Uuid128>(0), Is.EqualTo(Uuid128.Parse("00010203-0405-0607-0809-0a0b0c0d0e0f")));
			//note: t[0] returns a string, not a UUID

			// null maps to Uuid.Empty
			packed = TuPack.Unpack(Slice.Unescape("<00>"));
			Assert.That(packed.Get<Uuid128>(0), Is.EqualTo(Uuid128.Empty));
			//note: t[0] returns null, not a UUID

			Assert.That(TuPack.DecodeKey<Uuid128>(Slice.Unescape("<30><00><01><02><03><04><05><06><07><08><09><0A><0B><0C><0D><0E><0F>")), Is.EqualTo(Uuid128.Parse("00010203-0405-0607-0809-0a0b0c0d0e0f")));
		}

		[Test]
		public void Test_TuplePack_Serialize_Uuid96s()
		{
			// Uuid96 instances are stored with prefix 0x32 followed by 12 bytes, and should preserve ordering

			Slice packed;

			packed = TuPack.EncodeKey(Uuid96.Parse("00010203-04050607-08090A0B"));
			Assert.That(packed.ToHexaString(' '), Is.EqualTo("33 00 01 02 03 04 05 06 07 08 09 0A 0B"));

			packed = TuPack.EncodeKey(Uuid96.Parse("01234567-89ABCDEF-55AA33CC"));
			Assert.That(packed.ToHexaString(' '), Is.EqualTo("33 01 23 45 67 89 AB CD EF 55 AA 33 CC"));

			packed = TuPack.EncodeKey(Uuid96.Empty);
			Assert.That(packed.ToHexaString(' '), Is.EqualTo("33 00 00 00 00 00 00 00 00 00 00 00 00"));

			packed = TuPack.EncodeKey(new Uuid96(0x12345678, 0xBADC0FFEE0DDF00DUL));
			Assert.That(packed.ToHexaString(' '), Is.EqualTo("33 12 34 56 78 BA DC 0F FE E0 DD F0 0D"));

			packed = TuPack.EncodeKey(new Uuid96(0xFFFFFFFF, 0xDEADBEEFL));
			Assert.That(packed.ToHexaString(' '), Is.EqualTo("33 FF FF FF FF 00 00 00 00 DE AD BE EF"));
		}

		[Test]
		public void Test_TuplePack_Deserialize_Uuid96s()
		{
			// Uuid96 instances are stored with prefix 0x33 followed by 12 bytes (the result of uuid.ToByteArray())
			// we also accept byte arrays (prefix '01') if they are of length 12, and unicode strings (prefix '02')

			IVarTuple packed;

			packed = TuPack.Unpack(Slice.FromHexa("33 01 23 45 67 89 AB CD EF 55 AA 33 CC"));
			Assert.That(packed.Get<Uuid96>(0), Is.EqualTo(Uuid96.Parse("01234567-89ABCDEF-55AA33CC")));
			Assert.That(packed[0], Is.EqualTo((VersionStamp) Uuid96.Parse("01234567-89ABCDEF-55AA33CC")));

			packed = TuPack.Unpack(Slice.FromHexa("33 00 00 00 00 00 00 00 00 00 00 00 00"));
			Assert.That(packed.Get<Uuid96>(0), Is.EqualTo(Uuid96.Empty));
			Assert.That(packed[0], Is.EqualTo((VersionStamp) Uuid96.Empty));

			// 8 bytes
			packed = TuPack.Unpack(Slice.FromHexa("01 01 23 45 67 89 ab cd ef 55 AA 33 CC 00"));
			Assert.That(packed.Get<Uuid96>(0), Is.EqualTo(Uuid96.Parse("01234567-89ABCDEF-55AA33CC")));
			//note: t[0] returns a string, not a UUID

			// unicode string
			packed = TuPack.Unpack(Slice.Unescape("<02>01234567-89abcdef-55aa33cc<00>"));
			Assert.That(packed.Get<Uuid96>(0), Is.EqualTo(Uuid96.Parse("01234567-89abcdef-55aa33cc")));
			packed = TuPack.Unpack(Slice.Unescape("<02>0123456789abcdef55aa33cc<00>"));
			Assert.That(packed.Get<Uuid96>(0), Is.EqualTo(Uuid96.Parse("01234567-89abcdef-55aa33cc")));

			// null maps to Uuid.Empty
			packed = TuPack.Unpack(Slice.Unescape("<00>"));
			Assert.That(packed.Get<Uuid96>(0), Is.EqualTo(Uuid96.Empty));
			//note: t[0] returns null, not a UUID

		}

		[Test]
		public void Test_TuplePack_Serialize_Uuid80s()
		{
			// Uuid80 instances are stored with prefix 0x32 followed by 10 bytes, and should preserve ordering

			Slice packed;

			packed = TuPack.EncodeKey(Uuid80.Parse("0001-02030405-06070809"));
			Assert.That(packed.ToHexaString(' '), Is.EqualTo("32 00 01 02 03 04 05 06 07 08 09"));

			packed = TuPack.EncodeKey(Uuid80.Parse("0123-456789AB-CDEF55AA"));
			Assert.That(packed.ToHexaString(' '), Is.EqualTo("32 01 23 45 67 89 AB CD EF 55 AA"));

			packed = TuPack.EncodeKey(Uuid80.Empty);
			Assert.That(packed.ToHexaString(' '), Is.EqualTo("32 00 00 00 00 00 00 00 00 00 00"));

			packed = TuPack.EncodeKey(new Uuid80(0x1234, 0xBADC0FFEE0DDF00DUL));
			Assert.That(packed.ToHexaString(' '), Is.EqualTo("32 12 34 BA DC 0F FE E0 DD F0 0D"));

			packed = TuPack.EncodeKey(new Uuid80(0xFFFF, 0xDEADBEEFL));
			Assert.That(packed.ToHexaString(' '), Is.EqualTo("32 FF FF 00 00 00 00 DE AD BE EF"));
		}

		[Test]
		public void Test_TuplePack_Deserialize_Uuid80s()
		{
			// Uuid80 instances are stored with prefix '31' followed by 8 bytes (the result of uuid.ToByteArray())
			// we also accept byte arrays (prefix '01') if they are of length 8, and unicode strings (prefix '02')

			IVarTuple packed;

			packed = TuPack.Unpack(Slice.FromHexa("32 01 23 45 67 89 AB CD EF 55 AA"));
			Assert.That(packed.Get<Uuid80>(0), Is.EqualTo(Uuid80.Parse("0123-456789AB-CDEF55AA")));
			Assert.That(packed[0], Is.EqualTo((VersionStamp) Uuid80.Parse("0123-456789AB-CDEF55AA")));

			packed = TuPack.Unpack(Slice.FromHexa("32 00 00 00 00 00 00 00 00 00 00"));
			Assert.That(packed.Get<Uuid80>(0), Is.EqualTo(Uuid80.Empty));
			Assert.That(packed[0], Is.EqualTo((VersionStamp) Uuid80.Empty));

			// 8 bytes
			packed = TuPack.Unpack(Slice.FromHexa("01 01 23 45 67 89 ab cd ef 55 AA 00"));
			Assert.That(packed.Get<Uuid80>(0), Is.EqualTo(Uuid80.Parse("0123-456789AB-CDEF55AA")));
			//note: t[0] returns a string, not a UUID

			// unicode string
			packed = TuPack.Unpack(Slice.Unescape("<02>0123-456789ab-cdef55aa<00>"));
			Assert.That(packed.Get<Uuid80>(0), Is.EqualTo(Uuid80.Parse("0123-456789ab-cdef55aa")));
			packed = TuPack.Unpack(Slice.Unescape("<02>0123456789abcdef55aa<00>"));
			Assert.That(packed.Get<Uuid80>(0), Is.EqualTo(Uuid80.Parse("0123-456789ab-cdef55aa")));

			// null maps to Uuid.Empty
			packed = TuPack.Unpack(Slice.Unescape("<00>"));
			Assert.That(packed.Get<Uuid80>(0), Is.EqualTo(Uuid80.Empty));
			//note: t[0] returns null, not a UUID

		}

		[Test]
		public void Test_TuplePack_Serialize_Uuid64s()
		{
			// UUID64s are stored with prefix '31' followed by 8 bytes formatted according to RFC 4122

			// note: new Uuid(bytes from 0 to 7) => "00010203-04050607";
			Assert.That(TuPack.EncodeKey(Uuid64.Parse("00010203-04050607")).ToString(), Is.EqualTo("1<00><01><02><03><04><05><06><07>"));
			Assert.That(TuPack.Pack(ValueTuple.Create(Uuid64.Parse("00010203-04050607"))).ToString(), Is.EqualTo("1<00><01><02><03><04><05><06><07>"));

			Assert.That(TuPack.EncodeKey(Uuid64.Parse("01234567-89ABCDEF")).ToString(), Is.EqualTo("1<01>#Eg<89><AB><CD><EF>"));
			Assert.That(TuPack.EncodeKey(Uuid64.Empty).ToString(), Is.EqualTo("1<00><00><00><00><00><00><00><00>"));
			Assert.That(TuPack.EncodeKey(new Uuid64(0xBADC0FFEE0DDF00DUL)).ToString(), Is.EqualTo("1<BA><DC><0F><FE><E0><DD><F0><0D>"));
			Assert.That(TuPack.EncodeKey(new Uuid64(0xDEADBEEFL)).ToString(), Is.EqualTo("1<00><00><00><00><DE><AD><BE><EF>"));
		}

		[Test]
		public void Test_TuplePack_Deserialize_Uuid64s()
		{
			// UUID64s are stored with prefix '31' followed by 8 bytes (the result of uuid.ToByteArray())
			// we also accept byte arrays (prefix '01') if they are of length 8, and unicode strings (prefix '02')

			IVarTuple packed;

			// note: new Uuid(bytes from 0 to 15) => "00010203-0405-0607-0809-0a0b0c0d0e0f";
			packed = TuPack.Unpack(Slice.Unescape("<31><01><23><45><67><89><AB><CD><EF>"));
			Assert.That(packed.Get<Uuid64>(0), Is.EqualTo(Uuid64.Parse("01234567-89abcdef")));
			Assert.That(packed[0], Is.EqualTo(Uuid64.Parse("01234567-89abcdef")));

			packed = TuPack.Unpack(Slice.Unescape("<31><00><00><00><00><00><00><00><00>"));
			Assert.That(packed.Get<Uuid64>(0), Is.EqualTo(Uuid64.Empty));
			Assert.That(packed[0], Is.EqualTo(Uuid64.Empty));

			// 8 bytes
			packed = TuPack.Unpack(Slice.Unescape("<01><01><23><45><67><89><ab><cd><ef><00>"));
			Assert.That(packed.Get<Uuid64>(0), Is.EqualTo(Uuid64.Parse("01234567-89abcdef")));
			//note: t[0] returns a string, not a UUID

			// unicode string
			packed = TuPack.Unpack(Slice.Unescape("<02>01234567-89abcdef<00>"));
			Assert.That(packed.Get<Uuid64>(0), Is.EqualTo(Uuid64.Parse("01234567-89abcdef")));
			//note: t[0] returns a string, not a UUID

			// null maps to Uuid.Empty
			packed = TuPack.Unpack(Slice.Unescape("<00>"));
			Assert.That(packed.Get<Uuid64>(0), Is.EqualTo(Uuid64.Empty));
			//note: t[0] returns null, not a UUID

			Assert.That(TuPack.DecodeKey<Uuid64>(Slice.Unescape("<31><01><23><45><67><89><AB><CD><EF>")), Is.EqualTo(Uuid64.Parse("01234567-89abcdef")));
		}

		[Test]
		public void Test_TuplePack_Serialize_Integers()
		{
			// Positive integers are stored with a variable-length encoding.
			// - The prefix is 0x14 + the minimum number of bytes to encode the integer, from 0 to 8, so valid prefixes range from 0x14 to 0x1C
			// - The bytes are stored in High-Endian (ie: the upper bits first)
			// Examples:
			// - 0 => <14>
			// - 1..255 => <15><##>
			// - 256..65535 .. => <16><HH><LL>
			// - ulong.MaxValue => <1C><FF><FF><FF><FF><FF><FF><FF><FF>

			Assert.That(
				TuPack.EncodeKey(0).ToString(),
				Is.EqualTo("<14>")
			);

			Assert.That(
				TuPack.EncodeKey(1).ToString(),
				Is.EqualTo("<15><01>")
			);

			Assert.That(
				TuPack.EncodeKey(255).ToString(),
				Is.EqualTo("<15><FF>")
			);

			Assert.That(
				TuPack.EncodeKey(256).ToString(),
				Is.EqualTo("<16><01><00>")
			);

			Assert.That(
				TuPack.EncodeKey(65535).ToString(),
				Is.EqualTo("<16><FF><FF>")
			);

			Assert.That(
				TuPack.EncodeKey(65536).ToString(),
				Is.EqualTo("<17><01><00><00>")
			);

			Assert.That(
				TuPack.EncodeKey(int.MaxValue).ToString(),
				Is.EqualTo("<18><7F><FF><FF><FF>")
			);

			// signed max
			Assert.That(
				TuPack.EncodeKey(long.MaxValue).ToString(),
				Is.EqualTo("<1C><7F><FF><FF><FF><FF><FF><FF><FF>")
			);

			// unsigned max
			Assert.That(
				TuPack.EncodeKey(ulong.MaxValue).ToString(),
				Is.EqualTo("<1C><FF><FF><FF><FF><FF><FF><FF><FF>")
			);
		}

		[Test]
		public void Test_TuplePack_Deserialize_Integers()
		{
			void Verify(string encoded, long value)
			{
				var slice = Slice.Unescape(encoded);
				Assert.That(TuplePackers.DeserializeBoxed(slice), Is.EqualTo(value), "DeserializeBoxed({0})", encoded);

				// int64
				Assert.That(TuplePackers.DeserializeInt64(slice), Is.EqualTo(value), "DeserializeInt64({0})", encoded);
				Assert.That(TuplePacker<long>.Deserialize(slice), Is.EqualTo(value), "Deserialize<long>({0})", encoded);

				// uint64
				if (value >= 0)
				{
					Assert.That(TuplePackers.DeserializeUInt64(slice), Is.EqualTo((ulong) value), "DeserializeUInt64({0})", encoded);
					Assert.That(TuplePacker<ulong>.Deserialize(slice), Is.EqualTo((ulong) value), "Deserialize<ulong>({0})", encoded);
				}
				else
				{
					Assert.That<ulong>(() => TuplePackers.DeserializeUInt64(slice), Throws.InstanceOf<OverflowException>(), "DeserializeUInt64({0})", encoded);
				}

				// int32
				if (value <= int.MaxValue && value >= int.MinValue)
				{
					Assert.That(TuplePackers.DeserializeInt32(slice), Is.EqualTo((int) value), "DeserializeInt32({0})", encoded);
					Assert.That(TuplePacker<long>.Deserialize(slice), Is.EqualTo((int) value), "Deserialize<int>({0})", encoded);
				}
				else
				{
					Assert.That<int>(() => TuplePackers.DeserializeInt32(slice), Throws.InstanceOf<OverflowException>(), "DeserializeInt32({0})", encoded);
				}

				// uint32
				if (value <= uint.MaxValue && value >= 0)
				{
					Assert.That(TuplePackers.DeserializeUInt32(slice), Is.EqualTo((uint) value), "DeserializeUInt32({0})", encoded);
					Assert.That(TuplePacker<uint>.Deserialize(slice), Is.EqualTo((uint) value), "Deserialize<uint>({0})", encoded);
				}
				else
				{
					Assert.That<uint>(() => TuplePackers.DeserializeUInt32(slice), Throws.InstanceOf<OverflowException>(), "DeserializeUInt32({0})", encoded);
				}

				// int16
				if (value <= short.MaxValue && value >= short.MinValue)
				{
					Assert.That(TuplePackers.DeserializeInt16(slice), Is.EqualTo((short) value), "DeserializeInt16({0})", encoded);
					Assert.That(TuplePacker<short>.Deserialize(slice), Is.EqualTo((short) value), "Deserialize<short>({0})", encoded);
				}
				else
				{
					Assert.That<short>(() => TuplePackers.DeserializeInt16(slice), Throws.InstanceOf<OverflowException>(), "DeserializeInt16({0})", encoded);
				}

				// uint16
				if (value <= ushort.MaxValue && value >= 0)
				{
					Assert.That(TuplePackers.DeserializeUInt16(slice), Is.EqualTo((ushort) value), "DeserializeUInt16({0})", encoded);
					Assert.That(TuplePacker<ushort>.Deserialize(slice), Is.EqualTo((ushort) value), "Deserialize<ushort>({0})", encoded);
				}
				else
				{
					Assert.That<ushort>(() => TuplePackers.DeserializeUInt16(slice), Throws.InstanceOf<OverflowException>(), "DeserializeUInt16({0})", encoded);
				}

				// sbyte
				if (value <= sbyte.MaxValue && value >= sbyte.MinValue)
				{
					Assert.That(TuplePackers.DeserializeSByte(slice), Is.EqualTo((sbyte) value), "DeserializeSByte({0})", encoded);
					Assert.That(TuplePacker<sbyte>.Deserialize(slice), Is.EqualTo((sbyte) value), "Deserialize<sbyte>({0})", encoded);
				}
				else
				{
					Assert.That<sbyte>(() => TuplePackers.DeserializeSByte(slice), Throws.InstanceOf<OverflowException>(), "DeserializeSByte({0})", encoded);
				}

				// byte
				if (value <= 255 && value >= 0)
				{
					Assert.That(TuplePackers.DeserializeByte(slice), Is.EqualTo((byte) value), "DeserializeByte({0})", encoded);
					Assert.That(TuplePacker<byte>.Deserialize(slice), Is.EqualTo((byte) value), "Deserialize<byte>({0})", encoded);
				}
				else
				{
					Assert.That<byte>(() => TuplePackers.DeserializeByte(slice), Throws.InstanceOf<OverflowException>(), "DeserializeByte({0})", encoded);
				}
			}

			Verify("<14>", 0);
			Verify("<15>{", 123);
			Verify("<15><80>", 128);
			Verify("<15><FF>", 255);
			Verify("<16><01><00>", 256);
			Verify("<16><04><D2>", 1234);
			Verify("<16><80><00>", 32768);
			Verify("<16><FF><FF>", 65535);
			Verify("<17><01><00><00>", 65536);
			Verify("<13><FE>", -1);
			Verify("<13><00>", -255);
			Verify("<12><FE><FF>", -256);
			Verify("<12><00><00>", -65535);
			Verify("<11><FE><FF><FF>", -65536);
			Verify("<18><7F><FF><FF><FF>", int.MaxValue);
			Verify("<10><7F><FF><FF><FF>", int.MinValue);
			Verify("<1C><7F><FF><FF><FF><FF><FF><FF><FF>", long.MaxValue);
			Verify("<0C><7F><FF><FF><FF><FF><FF><FF><FF>", long.MinValue);
		}

		[Test]
		public void Test_TuplePack_Serialize_Negative_Integers()
		{
			// Negative integers are stored with a variable-length encoding.
			// - The prefix is 0x14 - the minimum number of bytes to encode the integer, from 0 to 8, so valid prefixes range from 0x0C to 0x13
			// - The value is encoded as the one's complement, and stored in High-Endian (ie: the upper bits first)
			// - There is no way to encode '-0', it will be encoded as '0' (<14>)
			// Examples:
			// - -255..-1 => <13><00> .. <13><FE>
			// - -65535..-256 => <12><00>00> .. <12><FE><FF>
			// - long.MinValue => <0C><7F><FF><FF><FF><FF><FF><FF><FF>

			Assert.That(
				TuPack.EncodeKey(-1).ToString(),
				Is.EqualTo("<13><FE>")
			);

			Assert.That(
				TuPack.EncodeKey(-255).ToString(),
				Is.EqualTo("<13><00>")
			);

			Assert.That(
				TuPack.EncodeKey(-256).ToString(),
				Is.EqualTo("<12><FE><FF>")
			);
			Assert.That(
				TuPack.EncodeKey(-257).ToString(),
				Is.EqualTo("<12><FE><FE>")
			);

			Assert.That(
				TuPack.EncodeKey(-65535).ToString(),
				Is.EqualTo("<12><00><00>")
			);
			Assert.That(
				TuPack.EncodeKey(-65536).ToString(),
				Is.EqualTo("<11><FE><FF><FF>")
			);

			Assert.That(
				TuPack.EncodeKey(int.MinValue).ToString(),
				Is.EqualTo("<10><7F><FF><FF><FF>")
			);

			Assert.That(
				TuPack.EncodeKey(long.MinValue).ToString(),
				Is.EqualTo("<0C><7F><FF><FF><FF><FF><FF><FF><FF>")
			);
		}

		[Test]
		public void Test_TuplePack_Serialize_Singles()
		{
			// 32-bit floats are stored in 5 bytes, using the prefix 0x20 followed by the High-Endian representation of their normalized form

			Assert.That(TuPack.EncodeKey(0f).ToHexaString(' '), Is.EqualTo("20 80 00 00 00"));
			Assert.That(TuPack.EncodeKey(42f).ToHexaString(' '), Is.EqualTo("20 C2 28 00 00"));
			Assert.That(TuPack.EncodeKey(-42f).ToHexaString(' '), Is.EqualTo("20 3D D7 FF FF"));

			Assert.That(TuPack.EncodeKey((float) Math.Sqrt(2)).ToHexaString(' '), Is.EqualTo("20 BF B5 04 F3"));

			Assert.That(TuPack.EncodeKey(float.MinValue).ToHexaString(' '), Is.EqualTo("20 00 80 00 00"), "float.MinValue");
			Assert.That(TuPack.EncodeKey(float.MaxValue).ToHexaString(' '), Is.EqualTo("20 FF 7F FF FF"), "float.MaxValue");
			Assert.That(TuPack.EncodeKey(-0f).ToHexaString(' '), Is.EqualTo("20 7F FF FF FF"), "-0f");
			Assert.That(TuPack.EncodeKey(float.NegativeInfinity).ToHexaString(' '), Is.EqualTo("20 00 7F FF FF"), "float.NegativeInfinity");
			Assert.That(TuPack.EncodeKey(float.PositiveInfinity).ToHexaString(' '), Is.EqualTo("20 FF 80 00 00"), "float.PositiveInfinity");
			Assert.That(TuPack.EncodeKey(float.Epsilon).ToHexaString(' '), Is.EqualTo("20 80 00 00 01"), "+float.Epsilon");
			Assert.That(TuPack.EncodeKey(-float.Epsilon).ToHexaString(' '), Is.EqualTo("20 7F FF FF FE"), "-float.Epsilon");

			// all possible variants of NaN should all be equal
			Assert.That(TuPack.EncodeKey(float.NaN).ToHexaString(' '), Is.EqualTo("20 00 3F FF FF"), "float.NaN");

			// cook up a non standard NaN (with some bits set in the fraction)
			float f = float.NaN; // defined as 1f / 0f
			uint nan;
			unsafe { nan = *((uint*) &f); }
			nan += 123;
			unsafe { f = *((float*) &nan); }
			Assert.That(float.IsNaN(f), Is.True);
			Assert.That(
				TuPack.EncodeKey(f).ToHexaString(' '),
				Is.EqualTo("20 00 3F FF FF"),
				"All variants of NaN must be normalized"
				//note: if we have 20 00 3F FF 84, that means that the NaN was not normalized
			);

		}

		[Test]
		public void Test_TuplePack_Deserialize_Singles()
		{
			Assert.That(TuPack.DecodeKey<float>(Slice.FromHexa("20 80 00 00 00")), Is.EqualTo(0f), "0f");
			Assert.That(TuPack.DecodeKey<float>(Slice.FromHexa("20 C2 28 00 00")), Is.EqualTo(42f), "42f");
			Assert.That(TuPack.DecodeKey<float>(Slice.FromHexa("20 3D D7 FF FF")), Is.EqualTo(-42f), "-42f");

			Assert.That(TuPack.DecodeKey<float>(Slice.FromHexa("20 BF B5 04 F3")), Is.EqualTo((float) Math.Sqrt(2)), "Sqrt(2)");

			// well known values
			Assert.That(TuPack.DecodeKey<float>(Slice.FromHexa("20 00 80 00 00")), Is.EqualTo(float.MinValue), "float.MinValue");
			Assert.That(TuPack.DecodeKey<float>(Slice.FromHexa("20 FF 7F FF FF")), Is.EqualTo(float.MaxValue), "float.MaxValue");
			Assert.That(TuPack.DecodeKey<float>(Slice.FromHexa("20 7F FF FF FF")), Is.EqualTo(-0f), "-0f");
			Assert.That(TuPack.DecodeKey<float>(Slice.FromHexa("20 00 7F FF FF")), Is.EqualTo(float.NegativeInfinity), "float.NegativeInfinity");
			Assert.That(TuPack.DecodeKey<float>(Slice.FromHexa("20 FF 80 00 00")), Is.EqualTo(float.PositiveInfinity), "float.PositiveInfinity");
			Assert.That(TuPack.DecodeKey<float>(Slice.FromHexa("20 00 80 00 00")), Is.EqualTo(float.MinValue), "float.Epsilon");
			Assert.That(TuPack.DecodeKey<float>(Slice.FromHexa("20 80 00 00 01")), Is.EqualTo(float.Epsilon), "+float.Epsilon");
			Assert.That(TuPack.DecodeKey<float>(Slice.FromHexa("20 7F FF FF FE")), Is.EqualTo(-float.Epsilon), "-float.Epsilon");

			// all possible variants of NaN should end up equal and normalized to float.NaN
			Assert.That(TuPack.DecodeKey<float>(Slice.FromHexa("20 00 3F FF FF")), Is.EqualTo(float.NaN), "float.NaN");
			Assert.That(TuPack.DecodeKey<float>(Slice.FromHexa("20 00 3F FF FF")), Is.EqualTo(float.NaN), "float.NaN");
		}

		[Test]
		public void Test_TuplePack_Serialize_Doubles()
		{
			// 64-bit floats are stored in 9 bytes, using the prefix 0x21 followed by the High-Endian representation of their normalized form

			Assert.That(TuPack.EncodeKey(0d).ToHexaString(' '), Is.EqualTo("21 80 00 00 00 00 00 00 00"));
			Assert.That(TuPack.EncodeKey(42d).ToHexaString(' '), Is.EqualTo("21 C0 45 00 00 00 00 00 00"));
			Assert.That(TuPack.EncodeKey(-42d).ToHexaString(' '), Is.EqualTo("21 3F BA FF FF FF FF FF FF"));

			Assert.That(TuPack.EncodeKey(Math.PI).ToHexaString(' '), Is.EqualTo("21 C0 09 21 FB 54 44 2D 18"));
			Assert.That(TuPack.EncodeKey(Math.E).ToHexaString(' '), Is.EqualTo("21 C0 05 BF 0A 8B 14 57 69"));

			Assert.That(TuPack.EncodeKey(double.MinValue).ToHexaString(' '), Is.EqualTo("21 00 10 00 00 00 00 00 00"), "double.MinValue");
			Assert.That(TuPack.EncodeKey(double.MaxValue).ToHexaString(' '), Is.EqualTo("21 FF EF FF FF FF FF FF FF"), "double.MaxValue");
			Assert.That(TuPack.EncodeKey(-0d).ToHexaString(' '), Is.EqualTo("21 7F FF FF FF FF FF FF FF"), "-0d");
			Assert.That(TuPack.EncodeKey(double.NegativeInfinity).ToHexaString(' '), Is.EqualTo("21 00 0F FF FF FF FF FF FF"), "double.NegativeInfinity");
			Assert.That(TuPack.EncodeKey(double.PositiveInfinity).ToHexaString(' '), Is.EqualTo("21 FF F0 00 00 00 00 00 00"), "double.PositiveInfinity");
			Assert.That(TuPack.EncodeKey(double.Epsilon).ToHexaString(' '), Is.EqualTo("21 80 00 00 00 00 00 00 01"), "+double.Epsilon");
			Assert.That(TuPack.EncodeKey(-double.Epsilon).ToHexaString(' '), Is.EqualTo("21 7F FF FF FF FF FF FF FE"), "-double.Epsilon");

			// all possible variants of NaN should all be equal

			Assert.That(TuPack.EncodeKey(double.NaN).ToHexaString(' '), Is.EqualTo("21 00 07 FF FF FF FF FF FF"), "double.NaN");

			// cook up a non standard NaN (with some bits set in the fraction)
			double d = double.NaN; // defined as 1d / 0d
			ulong nan;
			unsafe { nan = *((ulong*) &d); }
			nan += 123;
			unsafe { d = *((double*) &nan); }
			Assert.That(double.IsNaN(d), Is.True);
			Assert.That(
				TuPack.EncodeKey(d).ToHexaString(' '),
				Is.EqualTo("21 00 07 FF FF FF FF FF FF")
				//note: if we have 21 00 07 FF FF FF FF FF 84, that means that the NaN was not normalized
			);

			// roundtripping vectors of doubles
			var tuple = STuple.Create(Math.PI, Math.E, Math.Log(1), Math.Log(2));
			Assert.That(TuPack.Unpack(TuPack.EncodeKey(Math.PI, Math.E, Math.Log(1), Math.Log(2))), Is.EqualTo(tuple));
			Assert.That(TuPack.Unpack(TuPack.Pack(STuple.Create(Math.PI, Math.E, Math.Log(1), Math.Log(2)))), Is.EqualTo(tuple));
			Assert.That(TuPack.Unpack(TuPack.Pack(STuple.Empty.Append(Math.PI).Append(Math.E).Append(Math.Log(1)).Append(Math.Log(2)))), Is.EqualTo(tuple));
		}

		[Test]
		public void Test_TuplePack_Deserialize_Doubles()
		{
			Assert.That(TuPack.DecodeKey<double>(Slice.FromHexa("21 80 00 00 00 00 00 00 00")), Is.EqualTo(0d), "0d");
			Assert.That(TuPack.DecodeKey<double>(Slice.FromHexa("21 C0 45 00 00 00 00 00 00")), Is.EqualTo(42d), "42d");
			Assert.That(TuPack.DecodeKey<double>(Slice.FromHexa("21 3F BA FF FF FF FF FF FF")), Is.EqualTo(-42d), "-42d");

			Assert.That(TuPack.DecodeKey<double>(Slice.FromHexa("21 C0 09 21 FB 54 44 2D 18")), Is.EqualTo(Math.PI), "Math.PI");
			Assert.That(TuPack.DecodeKey<double>(Slice.FromHexa("21 C0 05 BF 0A 8B 14 57 69")), Is.EqualTo(Math.E), "Math.E");

			Assert.That(TuPack.DecodeKey<double>(Slice.FromHexa("21 00 10 00 00 00 00 00 00")), Is.EqualTo(double.MinValue), "double.MinValue");
			Assert.That(TuPack.DecodeKey<double>(Slice.FromHexa("21 FF EF FF FF FF FF FF FF")), Is.EqualTo(double.MaxValue), "double.MaxValue");
			Assert.That(TuPack.DecodeKey<double>(Slice.FromHexa("21 7F FF FF FF FF FF FF FF")), Is.EqualTo(-0d), "-0d");
			Assert.That(TuPack.DecodeKey<double>(Slice.FromHexa("21 00 0F FF FF FF FF FF FF")), Is.EqualTo(double.NegativeInfinity), "double.NegativeInfinity");
			Assert.That(TuPack.DecodeKey<double>(Slice.FromHexa("21 FF F0 00 00 00 00 00 00")), Is.EqualTo(double.PositiveInfinity), "double.PositiveInfinity");
			Assert.That(TuPack.DecodeKey<double>(Slice.FromHexa("21 80 00 00 00 00 00 00 01")), Is.EqualTo(double.Epsilon), "+double.Epsilon");
			Assert.That(TuPack.DecodeKey<double>(Slice.FromHexa("21 7F FF FF FF FF FF FF FE")), Is.EqualTo(-double.Epsilon), "-double.Epsilon");

			// all possible variants of NaN should end up equal and normalized to double.NaN
			Assert.That(TuPack.DecodeKey<double>(Slice.FromHexa("21 00 07 FF FF FF FF FF FF")), Is.EqualTo(double.NaN), "double.NaN");
			Assert.That(TuPack.DecodeKey<double>(Slice.FromHexa("21 00 07 FF FF FF FF FF 84")), Is.EqualTo(double.NaN), "double.NaN");
		}

		[Test]
		public void Test_TuplePack_Serialize_Booleans()
		{
			// Booleans are stored as <26> for false, and <27> for true

			// bool
			Assert.That(TuPack.EncodeKey(false).ToHexaString(' '), Is.EqualTo("26"));
			Assert.That(TuPack.EncodeKey(true).ToHexaString(' '), Is.EqualTo("27"));

			// bool?
			Assert.That(TuPack.EncodeKey(default(bool?)).ToHexaString(' '), Is.EqualTo("00"));
			Assert.That(TuPack.EncodeKey((bool?) false).ToHexaString(' '), Is.EqualTo("26"));
			Assert.That(TuPack.EncodeKey((bool?) true).ToHexaString(' '), Is.EqualTo("27"));

			// tuple containing bools
			Assert.That(TuPack.EncodeKey(true).ToHexaString(' '), Is.EqualTo("27"));
			Assert.That(TuPack.EncodeKey(true, default(string), false).ToHexaString(' '), Is.EqualTo("27 00 26"));
		}

		[Test]
		public void Test_TuplePack_Deserialize_Booleans()
		{
			// Null, 0, and empty byte[]/strings are equivalent to False. All others are equivalent to True

			// Falsy...
			Assert.That(TuPack.DecodeKey<bool>(Slice.Unescape("<26>")), Is.False, "False => False");
			Assert.That(TuPack.DecodeKey<bool>(Slice.Unescape("<00>")), Is.False, "Null => False");
			Assert.That(TuPack.DecodeKey<bool>(Slice.Unescape("<14>")), Is.False, "0 => False");
			Assert.That(TuPack.DecodeKey<bool>(Slice.Unescape("<01><00>")), Is.False, "byte[0] => False");
			Assert.That(TuPack.DecodeKey<bool>(Slice.Unescape("<02><00>")), Is.False, "String.Empty => False");

			// Truthy
			Assert.That(TuPack.DecodeKey<bool>(Slice.Unescape("<27>")), Is.True, "True => True");
			Assert.That(TuPack.DecodeKey<bool>(Slice.Unescape("<15><01>")), Is.True, "1 => True");
			Assert.That(TuPack.DecodeKey<bool>(Slice.Unescape("<13><FE>")), Is.True, "-1 => True");
			Assert.That(TuPack.DecodeKey<bool>(Slice.Unescape("<01>Hello<00>")), Is.True, "'Hello' => True");
			Assert.That(TuPack.DecodeKey<bool>(Slice.Unescape("<02>Hello<00>")), Is.True, "\"Hello\" => True");
			Assert.That(TuPack.DecodeKey<bool>(TuPack.EncodeKey(123456789)), Is.True, "random int => True");

			Assert.That(TuPack.DecodeKey<bool>(Slice.Unescape("<02>True<00>")), Is.True, "\"True\" => True");
			Assert.That(TuPack.DecodeKey<bool>(Slice.Unescape("<02>False<00>")), Is.True, "\"False\" => True ***");
			// note: even though it would be tempting to convert the string "false" to False, it is not a standard behavior accross all bindings

			// When decoded to object, they should return false and true
			Assert.That(TuplePackers.DeserializeBoxed(TuPack.EncodeKey(false)), Is.False);
			Assert.That(TuplePackers.DeserializeBoxed(TuPack.EncodeKey(true)), Is.True);
		}

		[Test]
		public void Test_TuplePack_Serialize_VersionStamps()
		{
			// incomplete, 80 bits
			Assert.That(
				TuPack.EncodeKey(VersionStamp.Incomplete()).ToHexaString(' '),
				Is.EqualTo("32 FF FF FF FF FF FF FF FF FF FF")
			);
			Assert.That(
				TuPack.Pack(ValueTuple.Create(VersionStamp.Incomplete())).ToHexaString(' '),
				Is.EqualTo("32 FF FF FF FF FF FF FF FF FF FF")
			);

			// incomplete, 96 bits
			Assert.That(
				TuPack.EncodeKey(VersionStamp.Incomplete(0)).ToHexaString(' '),
				Is.EqualTo("33 FF FF FF FF FF FF FF FF FF FF 00 00")
			);
			Assert.That(
				TuPack.EncodeKey(VersionStamp.Incomplete(42)).ToHexaString(' '),
				Is.EqualTo("33 FF FF FF FF FF FF FF FF FF FF 00 2A")
			);
			Assert.That(
				TuPack.EncodeKey(VersionStamp.Incomplete(456)).ToHexaString(' '),
				Is.EqualTo("33 FF FF FF FF FF FF FF FF FF FF 01 C8")
			);
			Assert.That(
				TuPack.EncodeKey(VersionStamp.Incomplete(65535)).ToHexaString(' '),
				Is.EqualTo("33 FF FF FF FF FF FF FF FF FF FF FF FF")
			);

			// complete, 80 bits
			Assert.That(
				TuPack.EncodeKey(VersionStamp.Complete(0x0123456789ABCDEF, 1234)).ToHexaString(' '),
				Is.EqualTo("32 01 23 45 67 89 AB CD EF 04 D2")
			);
			Assert.That(
				TuPack.Pack(ValueTuple.Create(VersionStamp.Complete(0x0123456789ABCDEF, 1234))).ToHexaString(' '),
				Is.EqualTo("32 01 23 45 67 89 AB CD EF 04 D2")
			);

			// complete, 96 bits
			Assert.That(
				TuPack.EncodeKey(VersionStamp.Complete(0x0123456789ABCDEF, 1234, 0)).ToHexaString(' '),
				Is.EqualTo("33 01 23 45 67 89 AB CD EF 04 D2 00 00")
			);
			Assert.That(
				TuPack.EncodeKey(VersionStamp.Complete(0x0123456789ABCDEF, 1234, 42)).ToHexaString(' '),
				Is.EqualTo("33 01 23 45 67 89 AB CD EF 04 D2 00 2A")
			);
			Assert.That(
				TuPack.EncodeKey(VersionStamp.Complete(0x0123456789ABCDEF, 65535, 42)).ToHexaString(' '),
				Is.EqualTo("33 01 23 45 67 89 AB CD EF FF FF 00 2A")
			);
			Assert.That(
				TuPack.EncodeKey(VersionStamp.Complete(0x0123456789ABCDEF, 1234, 65535)).ToHexaString(' '),
				Is.EqualTo("33 01 23 45 67 89 AB CD EF 04 D2 FF FF")
			);
		}

		[Test]
		public void Test_TuplePack_Deserailize_VersionStamps()
		{
			Assert.That(TuPack.Unpack(Slice.FromHexa("32 FF FF FF FF FF FF FF FF FF FF")).Get<VersionStamp>(0), Is.EqualTo(VersionStamp.Incomplete()), "Incomplete()");
			Assert.That(TuPack.Unpack(Slice.FromHexa("32 01 23 45 67 89 AB CD EF 04 D2")).Get<VersionStamp>(0), Is.EqualTo(VersionStamp.Complete(0x0123456789ABCDEF, 1234)), "Complete(..., 1234)");

			Assert.That(TuPack.DecodeKey<VersionStamp>(Slice.FromHexa("32 FF FF FF FF FF FF FF FF FF FF")), Is.EqualTo(VersionStamp.Incomplete()), "Incomplete()");

			Assert.That(TuPack.DecodeKey<VersionStamp>(Slice.FromHexa("33 FF FF FF FF FF FF FF FF FF FF 00 00")), Is.EqualTo(VersionStamp.Incomplete(0)), "Incomplete(0)");
			Assert.That(TuPack.DecodeKey<VersionStamp>(Slice.FromHexa("33 FF FF FF FF FF FF FF FF FF FF 00 2A")), Is.EqualTo(VersionStamp.Incomplete(42)), "Incomplete(42)");
			Assert.That(TuPack.DecodeKey<VersionStamp>(Slice.FromHexa("33 FF FF FF FF FF FF FF FF FF FF 01 C8")), Is.EqualTo(VersionStamp.Incomplete(456)), "Incomplete(456)");
			Assert.That(TuPack.DecodeKey<VersionStamp>(Slice.FromHexa("33 FF FF FF FF FF FF FF FF FF FF FF FF")), Is.EqualTo(VersionStamp.Incomplete(65535)), "Incomplete(65535)");

			Assert.That(TuPack.DecodeKey<VersionStamp>(Slice.FromHexa("32 01 23 45 67 89 AB CD EF 04 D2")), Is.EqualTo(VersionStamp.Complete(0x0123456789ABCDEF, 1234)), "Complete(..., 1234)");

			Assert.That(TuPack.DecodeKey<VersionStamp>(Slice.FromHexa("33 01 23 45 67 89 AB CD EF 04 D2 00 00")), Is.EqualTo(VersionStamp.Complete(0x0123456789ABCDEF, 1234, 0)), "Complete(..., 1234, 0)");
			Assert.That(TuPack.DecodeKey<VersionStamp>(Slice.FromHexa("33 01 23 45 67 89 AB CD EF 04 D2 00 2A")), Is.EqualTo(VersionStamp.Complete(0x0123456789ABCDEF, 1234, 42)), "Complete(..., 1234, 42)");
			Assert.That(TuPack.DecodeKey<VersionStamp>(Slice.FromHexa("33 01 23 45 67 89 AB CD EF FF FF 00 2A")), Is.EqualTo(VersionStamp.Complete(0x0123456789ABCDEF, 65535, 42)), "Complete(..., 65535, 42)");
			Assert.That(TuPack.DecodeKey<VersionStamp>(Slice.FromHexa("33 01 23 45 67 89 AB CD EF 04 D2 FF FF")), Is.EqualTo(VersionStamp.Complete(0x0123456789ABCDEF, 1234, 65535)), "Complete(..., 1234, 65535)");
		}

		[Test]
		public void Test_TuplePack_Serialize_Custom_Types()
		{
			// 64-bit floats are stored in 9 bytes, using the prefix 0x21 followed by the High-Endian representation of their normalized form

			Assert.That(TuPack.EncodeKey(TuPackUserType.System).ToHexaString(' '), Is.EqualTo("FF"));
			Assert.That(TuPack.EncodeKey(TuPackUserType.Directory).ToHexaString(' '), Is.EqualTo("FE"));
			Assert.That(TuPack.EncodeKey(TuPackUserType.Directory, 42, "Hello").ToHexaString(' '), Is.EqualTo("FE 15 2A 02 48 65 6C 6C 6F 00"));

			Assert.That(TuPack.EncodeKey(42, TuPackUserType.Directory, "Hello").ToHexaString(' '), Is.EqualTo("15 2A FE 02 48 65 6C 6C 6F 00"));
			Assert.That(TuPack.Pack((42, TuPackUserType.Directory, "Hello")).ToHexaString(' '), Is.EqualTo("15 2A FE 02 48 65 6C 6C 6F 00"));

			Assert.That(TuPack.EncodeKey(TuPackUserType.System, "Hello").ToHexaString(' '), Is.EqualTo("FF 02 48 65 6C 6C 6F 00"));
			Assert.That(TuPack.Pack((TuPackUserType.System, "Hello")).ToHexaString(' '), Is.EqualTo("FF 02 48 65 6C 6C 6F 00"));
		}

		[Test]
		public void Test_TuplePack_Deserialize_Custom_Types()
		{
			Assert.That(TuPack.DecodeKey<TuPackUserType>(Slice.FromHexa("FF")), Is.EqualTo(TuPackUserType.System));
			Assert.That(TuPack.DecodeKey<TuPackUserType>(Slice.FromHexa("FE")), Is.EqualTo(TuPackUserType.Directory));
			Assert.That(TuPack.DecodeKey<int, TuPackUserType, string>(Slice.FromHexa("15 2A FE 02 48 65 6C 6C 6F 00")), Is.EqualTo(STuple.Create(42, TuPackUserType.Directory, "Hello")));

			Assert.That(TuPack.Unpack(Slice.FromHexa("FF"))[0], Is.EqualTo(TuPackUserType.System));
			Assert.That(TuPack.Unpack(Slice.FromHexa("FE"))[0], Is.EqualTo(TuPackUserType.Directory));
			Assert.That(TuPack.Unpack(Slice.FromHexa("15 2A FE 02 48 65 6C 6C 6F 00")), Is.EqualTo(STuple.Create(42, TuPackUserType.Directory, "Hello")));
		}

		[Test]
		public void Test_TuplePack_Serialize_IPAddress()
		{
			// IP Addresses are stored as a byte array (<01>..<00>), in network order (big-endian)
			// They will take from 6 to 10 bytes, depending on the number of '.0' in them.

			Assert.That(
				TuPack.EncodeKey(IPAddress.Loopback).ToHexaString(' '),
				Is.EqualTo("01 7F 00 FF 00 FF 01 00")
			);

			Assert.That(
				TuPack.EncodeKey(IPAddress.Any).ToHexaString(' '),
				Is.EqualTo("01 00 FF 00 FF 00 FF 00 FF 00")
			);

			Assert.That(
				TuPack.EncodeKey(IPAddress.Parse("1.2.3.4")).ToHexaString(' '),
				Is.EqualTo("01 01 02 03 04 00")
			);

			Assert.That(
				TuPack.Pack(ValueTuple.Create(IPAddress.Loopback)).ToHexaString(' '),
				Is.EqualTo("01 7F 00 FF 00 FF 01 00")
			);

		}


		[Test]
		public void Test_TuplePack_Deserialize_IPAddress()
		{
			Assert.That(TuPack.Unpack(Slice.Unescape("<01><7F><00><FF><00><FF><01><00>")).Get<IPAddress>(0), Is.EqualTo(IPAddress.Parse("127.0.0.1")));

			Assert.That(TuPack.DecodeKey<IPAddress>(Slice.Unescape("<01><7F><00><FF><00><FF><01><00>")), Is.EqualTo(IPAddress.Parse("127.0.0.1")));
			Assert.That(TuPack.DecodeKey<IPAddress>(Slice.Unescape("<01><00><FF><00><FF><00><FF><00><FF><00>")), Is.EqualTo(IPAddress.Parse("0.0.0.0")));
			Assert.That(TuPack.DecodeKey<IPAddress>(Slice.Unescape("<01><01><02><03><04><00>")), Is.EqualTo(IPAddress.Parse("1.2.3.4")));

			Assert.That(TuPack.DecodeKey<IPAddress>(TuPack.EncodeKey("127.0.0.1")), Is.EqualTo(IPAddress.Loopback));

			var ip = IPAddress.Parse("192.168.0.1");
			Assert.That(TuPack.DecodeKey<IPAddress>(TuPack.EncodeKey(ip.ToString())), Is.EqualTo(ip));
			Assert.That(TuPack.DecodeKey<IPAddress>(TuPack.EncodeKey(ip.GetAddressBytes())), Is.EqualTo(ip));
#pragma warning disable 618
			Assert.That(TuPack.DecodeKey<IPAddress>(TuPack.EncodeKey(ip.Address)), Is.EqualTo(ip));
#pragma warning restore 618
		}

		[Test]
		public void Test_TuplePack_NullableTypes()
		{
			// Nullable types will either be encoded as <14> for null, or their regular encoding if not null

			// serialize

			Assert.That(TuPack.EncodeKey<int?>(0), Is.EqualTo(Slice.Unescape("<14>")));
			Assert.That(TuPack.EncodeKey<int?>(123), Is.EqualTo(Slice.Unescape("<15>{")));
			Assert.That(TuPack.EncodeKey<int?>(null), Is.EqualTo(Slice.Unescape("<00>")));

			Assert.That(TuPack.EncodeKey<long?>(0L), Is.EqualTo(Slice.Unescape("<14>")));
			Assert.That(TuPack.EncodeKey<long?>(123L), Is.EqualTo(Slice.Unescape("<15>{")));
			Assert.That(TuPack.EncodeKey<long?>(null), Is.EqualTo(Slice.Unescape("<00>")));

			Assert.That(TuPack.EncodeKey<bool?>(true), Is.EqualTo(Slice.Unescape("<27>")));
			Assert.That(TuPack.EncodeKey<bool?>(false), Is.EqualTo(Slice.Unescape("<26>")));
			Assert.That(TuPack.EncodeKey<bool?>(null), Is.EqualTo(Slice.Unescape("<00>")), "Maybe it was File Not Found?");

			Assert.That(TuPack.EncodeKey<Guid?>(Guid.Empty), Is.EqualTo(Slice.Unescape("0<00><00><00><00><00><00><00><00><00><00><00><00><00><00><00><00>")));
			Assert.That(TuPack.EncodeKey<Guid?>(null), Is.EqualTo(Slice.Unescape("<00>")));

			Assert.That(TuPack.EncodeKey<TimeSpan?>(TimeSpan.Zero), Is.EqualTo(Slice.Unescape("!<80><00><00><00><00><00><00><00>")));
			Assert.That(TuPack.EncodeKey<TimeSpan?>(null), Is.EqualTo(Slice.Unescape("<00>")));

			Assert.That(TuPack.Pack(ValueTuple.Create<int?>(123)), Is.EqualTo(Slice.Unescape("<15>{")));
			Assert.That(TuPack.Pack(ValueTuple.Create<long?>(123)), Is.EqualTo(Slice.Unescape("<15>{")));
			Assert.That(TuPack.Pack(ValueTuple.Create<bool?>(true)), Is.EqualTo(Slice.Unescape("<27>")));

			// deserialize

			Assert.That(TuPack.DecodeKey<int?>(Slice.Unescape("<14>")), Is.EqualTo(0));
			Assert.That(TuPack.DecodeKey<int?>(Slice.Unescape("<15>{")), Is.EqualTo(123));
			Assert.That(TuPack.DecodeKey<int?>(Slice.Unescape("<00>")), Is.Null);
			Assert.That(TuPack.Unpack(Slice.Unescape("<15>{")).Get<int?>(0), Is.EqualTo(123));
			Assert.That(TuPack.Unpack(Slice.Unescape("<00>")).Get<int?>(0), Is.Null);

			Assert.That(TuPack.DecodeKey<int?>(Slice.Unescape("<14>")), Is.EqualTo(0L));
			Assert.That(TuPack.DecodeKey<long?>(Slice.Unescape("<15>{")), Is.EqualTo(123L));
			Assert.That(TuPack.DecodeKey<long?>(Slice.Unescape("<00>")), Is.Null);
			Assert.That(TuPack.Unpack(Slice.Unescape("<15>{")).Get<long?>(0), Is.EqualTo(123L));
			Assert.That(TuPack.Unpack(Slice.Unescape("<00>")).Get<long?>(0), Is.Null);

			Assert.That(TuPack.DecodeKey<bool?>(Slice.Unescape("<27>")), Is.True);
			Assert.That(TuPack.DecodeKey<bool?>(Slice.Unescape("<15><01>")), Is.True);
			Assert.That(TuPack.DecodeKey<bool?>(Slice.Unescape("<26>")), Is.False);
			Assert.That(TuPack.DecodeKey<bool?>(Slice.Unescape("<14>")), Is.False);
			Assert.That(TuPack.DecodeKey<bool?>(Slice.Unescape("<00>")), Is.Null);

			Assert.That(TuPack.DecodeKey<Guid?>(Slice.Unescape("0<00><00><00><00><00><00><00><00><00><00><00><00><00><00><00><00>")), Is.EqualTo(Guid.Empty));
			Assert.That(TuPack.DecodeKey<Guid?>(Slice.Unescape("<00>")), Is.Null);

			Assert.That(TuPack.DecodeKey<TimeSpan?>(Slice.Unescape("<14>")), Is.EqualTo(TimeSpan.Zero));
			Assert.That(TuPack.DecodeKey<TimeSpan?>(Slice.Unescape("<00>")), Is.Null);

		}

		[Test]
		public void Test_TuplePack_Serialize_Embedded_Tuples()
		{
			void Verify(IVarTuple t, string expected)
			{
				var key = TuPack.Pack(t);
				Assert.That(key.ToHexaString(' '), Is.EqualTo(expected));
				var t2 = TuPack.Unpack(key);
				Assert.That(t2, Is.Not.Null);
				Assert.That(t2.Count, Is.EqualTo(t.Count), "{0}", t2);
				Assert.That(t2, Is.EqualTo(t));
			}

			// Index composite key
			IVarTuple value = STuple.Create(2014, 11, 6); // Indexing a date value (Y, M, D)
			string docId = "Doc123";
			// key would be "(..., value, id)"

			Verify(
				STuple.Empty,
				""
			);
			Verify(
				STuple.Create(42, value, docId),
				"15 2A 05 16 07 DE 15 0B 15 06 00 02 44 6F 63 31 32 33 00"
			);
			Verify(
				STuple.Create(new object[] {42, value, docId}),
				"15 2A 05 16 07 DE 15 0B 15 06 00 02 44 6F 63 31 32 33 00"
			);
			Verify(
				STuple.Create(42).Append(value).Append(docId),
				"15 2A 05 16 07 DE 15 0B 15 06 00 02 44 6F 63 31 32 33 00"
			);
			Verify(
				STuple.Create(42).Append(value, docId),
				"15 2A 05 16 07 DE 15 0B 15 06 00 02 44 6F 63 31 32 33 00"
			);

			// multiple depth
			Verify(
				STuple.Create(1, STuple.Create(2, 3), STuple.Create(STuple.Create(4, 5, 6)), 7),
				"15 01 05 15 02 15 03 00 05 05 15 04 15 05 15 06 00 00 15 07"
			);

			// corner cases
			Verify(
				STuple.Create(STuple.Empty),
				"05 00" // empty tuple should have header and footer
			);
			Verify(
				STuple.Create(STuple.Empty, default(string)),
				"05 00 00" // outer null should not be escaped
			);
			// corner cases
			Verify(
				STuple.Create(STuple.Create(STuple.Empty, STuple.Empty), STuple.Empty),
				"05 05 00 05 00 00 05 00"
			);
			Verify(
				STuple.Create(STuple.Create(default(string)), default(string)),
				"05 00 FF 00 00" // inner null should be escaped, but not outer
			);
			Verify(
				STuple.Create(STuple.Create(0x100, 0x10000, 0x1000000)),
				"05 16 01 00 17 01 00 00 18 01 00 00 00 00"
			);
			Verify(
				STuple.Create(default(string), STuple.Empty, default(string), STuple.Create(default(string)), default(string)),
				"00 05 00 00 05 00 FF 00 00" // inner null should be escaped, but not outer
			);

		}

		[Test]
		public void Test_TuplePack_Deserialize_Embedded_Tuples()
		{
			// ( (42, (2014, 11, 6), "Hello", true), )
			var packed = TuPack.EncodeKey(STuple.Create(42, STuple.Create(2014, 11, 6), "Hello", true));
			Log($"t = {TuPack.Unpack(packed)}");
			Assert.That(packed[0], Is.EqualTo(TupleTypes.EmbeddedTuple), "Missing Embedded Tuple marker");
			{
				var t = TuPack.Unpack(packed);
				Assert.That(t, Is.Not.Null);
				Assert.That(t.Count, Is.EqualTo(1));
				var t1 = t.Get<IVarTuple>(0);
				Assert.That(t1.Count, Is.EqualTo(4));
				Assert.That(t1.Get<int>(0), Is.EqualTo(42));
				Assert.That(t1.Get<IVarTuple>(1), Is.EqualTo(STuple.Create(2014, 11, 6)));
				Assert.That(t1.Get<string>(2), Is.EqualTo("Hello"));
				Assert.That(t1.Get<bool>(3), Is.True);
			}
			{
				var t = TuPack.DecodeKey<IVarTuple>(packed);
				Assert.That(t, Is.Not.Null);
				Assert.That(t.Count, Is.EqualTo(4));
				Assert.That(t.Get<int>(0), Is.EqualTo(42));
				Assert.That(t.Get<IVarTuple>(1), Is.EqualTo(STuple.Create(2014, 11, 6)));
				Assert.That(t.Get<string>(2), Is.EqualTo("Hello"));
				Assert.That(t.Get<bool>(3), Is.True);
			}
			{
				var t = TuPack.DecodeKey<STuple<int, IVarTuple, string, bool>>(packed);
				Assert.That(t, Is.Not.Null);
				Assert.That(t.Item1, Is.EqualTo(42));
				Assert.That(t.Item2, Is.EqualTo(STuple.Create(2014, 11, 6)));
				Assert.That(t.Item3, Is.EqualTo("Hello"));
				Assert.That(t.Item4, Is.True);
			}
			{
				var t = TuPack.DecodeKey<(int, IVarTuple, string, bool)>(packed);
				Assert.That(t, Is.Not.Null);
				Assert.That(t, Is.EqualTo((42, STuple.Create(2014, 11, 6), "Hello", true)));
			}
			{
				STuple<int, STuple<int, int, int>, string, bool> t = TuPack.DecodeKey<STuple<int, STuple<int, int, int>, string, bool>>(packed);
				Assert.That(t, Is.Not.Null);
				Assert.That(t.Item1, Is.EqualTo(42));
				Assert.That(t.Item2, Is.EqualTo(STuple.Create(2014, 11, 6)));
				Assert.That(t.Item3, Is.EqualTo("Hello"));
				Assert.That(t.Item4, Is.True);
			}
			{
				var t = TuPack.DecodeKey<(int, (int, int, int), string, bool)>(packed);
				Assert.That(t, Is.EqualTo((42, (2014, 11, 6), "Hello", true)));
			}

			// empty tuples
			{
				Assert.That(TuPack.Unpack(Slice.Empty), Is.EqualTo(STuple.Empty), "()");
				Assert.That(TuPack.Unpack(Slice.Unescape("<05><00>")), Is.EqualTo(STuple.Create(STuple.Empty)), "((),)");
				Assert.That(TuPack.Unpack(Slice.Unescape("<05><05><00><05><00><00><05><00>")), Is.EqualTo(STuple.Create(STuple.Create(STuple.Empty, STuple.Empty), STuple.Empty)), "(((),()),())");
			}

			// (null,)
			packed = Slice.FromByte(0);
			Log($"t = {TuPack.Unpack(packed)}");
			{
				var t = TuPack.DecodeKey<IVarTuple>(packed);
				Assert.That(t, Is.Null);
			}
			{
				var t = TuPack.DecodeKey<STuple<int, STuple<int, int, int>, string, bool>>(packed);
				Assert.That(t.Item1, Is.EqualTo(0));
				Assert.That(t.Item2, Is.EqualTo(default(STuple<int, int, int>)));
				Assert.That(t.Item3, Is.Null);
				Assert.That(t.Item4, Is.False);
			}
			{
				var t = TuPack.DecodeKey<(int, (int, int, int), string, bool)>(packed);
				Assert.That(t, Is.EqualTo((0, (0, 0, 0), default(string), false)));
			}

			//fallback if encoded as slice
			packed = TuPack.EncodeKey(TuPack.EncodeKey(42, STuple.Create(2014, 11, 6), "Hello", true));
			Log($"t = {TuPack.Unpack(packed)}");
			Assert.That(packed[0], Is.EqualTo(TupleTypes.Bytes), "Missing Slice marker");
			{
				var t = TuPack.DecodeKey<STuple<int, STuple<int, int, int>, string, bool>>(packed);
				Assert.That(t, Is.Not.Null);
				Assert.That(t.Item1, Is.EqualTo(42));
				Assert.That(t.Item2, Is.EqualTo(STuple.Create(2014, 11, 6)));
				Assert.That(t.Item3, Is.EqualTo("Hello"));
				Assert.That(t.Item4, Is.True);
			}
			{
				var t = TuPack.DecodeKey<(int, (int, int, int), string, bool)>(packed);
				Assert.That(t, Is.EqualTo((42, (2014, 11, 6), "Hello", true)));
			}
		}

		[Test]
		public void Test_TuplePack_SameBytes()
		{
			// two ways on packing the "same" tuple yield the same binary output
			{
				var expected = TuPack.EncodeKey("Hello World");
				Assert.That(TuPack.Pack(ValueTuple.Create("Hello World")), Is.EqualTo(expected));
				Assert.That(TuPack.Pack(STuple.Create("Hello World")), Is.EqualTo(expected));
				Assert.That(TuPack.Pack(((IVarTuple) STuple.Create("Hello World"))), Is.EqualTo(expected));
				Assert.That(TuPack.Pack(STuple.Create(new object[] {"Hello World"})), Is.EqualTo(expected));
				Assert.That(TuPack.Pack(STuple.Create("Hello World", 1234).Substring(0, 1)), Is.EqualTo(expected));
			}
			{
				var expected = TuPack.EncodeKey("Hello World", 1234);
				Assert.That(TuPack.Pack(("Hello World", 1234)), Is.EqualTo(expected));
				Assert.That(TuPack.Pack(STuple.Create("Hello World", 1234)), Is.EqualTo(expected));
				Assert.That(TuPack.Pack(((IVarTuple) STuple.Create("Hello World", 1234))), Is.EqualTo(expected));
				Assert.That(TuPack.Pack(STuple.Create("Hello World").Append(1234)), Is.EqualTo(expected));
				Assert.That(TuPack.Pack(((IVarTuple) STuple.Create("Hello World")).Append(1234)), Is.EqualTo(expected));
				Assert.That(TuPack.Pack(STuple.Create(new object[] {"Hello World", 1234})), Is.EqualTo(expected));
				Assert.That(TuPack.Pack(STuple.Create("Hello World", 1234, "Foo").Substring(0, 2)), Is.EqualTo(expected));
			}
			{
				var expected = TuPack.EncodeKey("Hello World", 1234, "Foo");
				Assert.That(TuPack.Pack(("Hello World", 1234, "Foo")), Is.EqualTo(expected));
				Assert.That(TuPack.Pack(STuple.Create("Hello World", 1234, "Foo")), Is.EqualTo(expected));
				Assert.That(TuPack.Pack(((IVarTuple) STuple.Create("Hello World", 1234, "Foo"))), Is.EqualTo(expected));
				Assert.That(TuPack.Pack(STuple.Create("Hello World").Append(1234).Append("Foo")), Is.EqualTo(expected));
				Assert.That(TuPack.Pack(((IVarTuple) STuple.Create("Hello World")).Append(1234).Append("Foo")), Is.EqualTo(expected));
				Assert.That(TuPack.Pack(STuple.Create(new object[] {"Hello World", 1234, "Foo"})), Is.EqualTo(expected));
				Assert.That(TuPack.Pack(STuple.Create("Hello World", 1234, "Foo", "Bar").Substring(0, 3)), Is.EqualTo(expected));
			}

			// also, there should be no differences between int,long,uint,... if they have the same value
			Assert.That(TuPack.Pack(("Hello", 123)), Is.EqualTo(TuPack.Pack(("Hello", 123L))));
			Assert.That(TuPack.Pack(STuple.Create("Hello", 123)), Is.EqualTo(TuPack.Pack(STuple.Create("Hello", 123L))));
			Assert.That(TuPack.Pack(STuple.Create("Hello", -123)), Is.EqualTo(TuPack.Pack(STuple.Create("Hello", -123L))));

			// GUID / UUID128 should pack the same way
			var g = Guid.NewGuid();
			Assert.That(TuPack.Pack(STuple.Create(g)), Is.EqualTo(TuPack.Pack(STuple.Create((Uuid128) g))), "GUID vs UUID128");
		}

		[Test]
		public void Test_TuplePack_Numbers_Are_Sorted_Lexicographically()
		{
			// pick two numbers 'x' and 'y' at random, and check that the order of 'x' compared to 'y' is the same as 'pack(tuple(x))' compared to 'pack(tuple(y))'

			// ie: ensure that x.CompareTo(y) always has the same sign as Tuple(x).CompareTo(Tuple(y))

			const int N = 1 * 1000 * 1000;
			var rnd = new Random();
			var sw = Stopwatch.StartNew();

			for (int i = 0; i < N; i++)
			{
				int x = rnd.Next() - 1073741824;
				int y = x;
				while (y == x)
				{
					y = rnd.Next() - 1073741824;
				}

				var t1 = TuPack.EncodeKey(x);
				var t2 = TuPack.EncodeKey(y);

				int dint = x.CompareTo(y);
				int dtup = t1.CompareTo(t2);

				if (dtup == 0) Assert.Fail("Tuples for x={0} and y={1} should not have the same packed value", x, y);

				// compare signs
				if (Math.Sign(dint) != Math.Sign(dtup))
				{
					Assert.Fail("Tuples for x={0} and y={1} are not sorted properly ({2} / {3}): t(x)='{4}' and t(y)='{5}'", x, y, dint, dtup, t1.ToString(), t2.ToString());
				}
			}
			sw.Stop();
			Log("Checked {0:N0} tuples in {1:N1} ms", N, sw.ElapsedMilliseconds);

		}

		#endregion

		[Test]
		public void Test_TuplePack_Pack()
		{
			Assert.That(
				TuPack.Pack(STuple.Create()),
				Is.EqualTo(Slice.Empty)
			);
			Assert.That(
				TuPack.Pack(STuple.Create("hello world")).ToString(),
				Is.EqualTo("<02>hello world<00>")
			);
			Assert.That(
				TuPack.Pack(STuple.Create("hello", "world")).ToString(),
				Is.EqualTo("<02>hello<00><02>world<00>")
			);
			Assert.That(
				TuPack.Pack(STuple.Create("hello world", 123)).ToString(),
				Is.EqualTo("<02>hello world<00><15>{")
			);
			Assert.That(
				TuPack.Pack(STuple.Create("hello world", 1234, -1234)).ToString(),
				Is.EqualTo("<02>hello world<00><16><04><D2><12><FB>-")
			);
			Assert.That(
				TuPack.Pack(STuple.Create("hello world", 123, false)).ToString(),
				Is.EqualTo("<02>hello world<00><15>{&")
			);
			Assert.That(
				TuPack.Pack(STuple.Create("hello world", 123, false, new byte[] {123, 1, 66, 0, 42})).ToString(),
				Is.EqualTo("<02>hello world<00><15>{&<01>{<01>B<00><FF>*<00>")
			);
			Assert.That(
				TuPack.Pack(STuple.Create("hello world", 123, false, new byte[] { 123, 1, 66, 0, 42 }, Math.PI)).ToString(),
				Is.EqualTo("<02>hello world<00><15>{&<01>{<01>B<00><FF>*<00>!<C0><09>!<FB>TD-<18>")
			);
			Assert.That(
				TuPack.Pack(STuple.Create("hello world", 123, false, new byte[] { 123, 1, 66, 0, 42 }, Math.PI, -1234L)).ToString(),
				Is.EqualTo("<02>hello world<00><15>{&<01>{<01>B<00><FF>*<00>!<C0><09>!<FB>TD-<18><12><FB>-")
			);
			Assert.That(
				TuPack.Pack(STuple.Create("hello world", 123, false, new byte[] { 123, 1, 66, 0, 42 }, Math.PI, -1234L, "こんにちは世界")).ToString(),
				Is.EqualTo("<02>hello world<00><15>{&<01>{<01>B<00><FF>*<00>!<C0><09>!<FB>TD-<18><12><FB>-<02><E3><81><93><E3><82><93><E3><81><AB><E3><81><A1><E3><81><AF><E4><B8><96><E7><95><8C><00>")
			);
			Assert.That(
				TuPack.Pack(STuple.Create("hello world", 123, false, new byte[] { 123, 1, 66, 0, 42 }, Math.PI, -1234L, "こんにちは世界", true)).ToString(),
				Is.EqualTo("<02>hello world<00><15>{&<01>{<01>B<00><FF>*<00>!<C0><09>!<FB>TD-<18><12><FB>-<02><E3><81><93><E3><82><93><E3><81><AB><E3><81><A1><E3><81><AF><E4><B8><96><E7><95><8C><00>'")
			);
			Assert.That(
				TuPack.Pack(STuple.Create(new object[] {"hello world", 123, false, new byte[] {123, 1, 66, 0, 42}})).ToString(),
				Is.EqualTo("<02>hello world<00><15>{&<01>{<01>B<00><FF>*<00>")
			);
			Assert.That(
				TuPack.Pack(STuple.FromArray(new object[] {"hello world", 123, false, new byte[] {123, 1, 66, 0, 42}}, 1, 2)).ToString(),
				Is.EqualTo("<15>{&")
			);
			Assert.That(
				TuPack.Pack(STuple.FromEnumerable(new List<object> {"hello world", 123, false, new byte[] {123, 1, 66, 0, 42}})).ToString(),
				Is.EqualTo("<02>hello world<00><15>{&<01>{<01>B<00><FF>*<00>")
			);

		}

		[Test]
		public void Test_TuplePack_Pack_With_Prefix()
		{

			Slice prefix = Slice.FromString("ABC");

			Assert.That(
				TuPack.Pack(prefix, STuple.Create()).ToString(),
				Is.EqualTo("ABC")
			);
			Assert.That(
				TuPack.Pack(prefix, STuple.Create("hello world")).ToString(),
				Is.EqualTo("ABC<02>hello world<00>")
			);
			Assert.That(
				TuPack.Pack(prefix, STuple.Create("hello", "world")).ToString(),
				Is.EqualTo("ABC<02>hello<00><02>world<00>")
			);
			Assert.That(
				TuPack.Pack(prefix, STuple.Create("hello world", 123)).ToString(),
				Is.EqualTo("ABC<02>hello world<00><15>{")
			);
			Assert.That(
				TuPack.Pack(prefix, STuple.Create("hello world", 1234, -1234)).ToString(),
				Is.EqualTo("ABC<02>hello world<00><16><04><D2><12><FB>-")
			);
			Assert.That(
				TuPack.Pack(prefix, STuple.Create("hello world", 123, false)).ToString(),
				Is.EqualTo("ABC<02>hello world<00><15>{&")
			);
			Assert.That(
				TuPack.Pack(prefix, STuple.Create("hello world", 123, false, new byte[] { 123, 1, 66, 0, 42 })).ToString(),
				Is.EqualTo("ABC<02>hello world<00><15>{&<01>{<01>B<00><FF>*<00>")
			);
			Assert.That(
				TuPack.Pack(prefix, STuple.Create("hello world", 123, false, new byte[] { 123, 1, 66, 0, 42 }, Math.PI)).ToString(),
				Is.EqualTo("ABC<02>hello world<00><15>{&<01>{<01>B<00><FF>*<00>!<C0><09>!<FB>TD-<18>")
			);
			Assert.That(
				TuPack.Pack(prefix, STuple.Create("hello world", 123, false, new byte[] { 123, 1, 66, 0, 42 }, Math.PI, -1234L)).ToString(),
				Is.EqualTo("ABC<02>hello world<00><15>{&<01>{<01>B<00><FF>*<00>!<C0><09>!<FB>TD-<18><12><FB>-")
			);
			Assert.That(
				TuPack.Pack(prefix, STuple.Create("hello world", 123, false, new byte[] { 123, 1, 66, 0, 42 }, Math.PI, -1234L, "こんにちは世界")).ToString(),
				Is.EqualTo("ABC<02>hello world<00><15>{&<01>{<01>B<00><FF>*<00>!<C0><09>!<FB>TD-<18><12><FB>-<02><E3><81><93><E3><82><93><E3><81><AB><E3><81><A1><E3><81><AF><E4><B8><96><E7><95><8C><00>")
			);
			Assert.That(
				TuPack.Pack(prefix, STuple.Create("hello world", 123, false, new byte[] { 123, 1, 66, 0, 42 }, Math.PI, -1234L, "こんにちは世界", true)).ToString(),
				Is.EqualTo("ABC<02>hello world<00><15>{&<01>{<01>B<00><FF>*<00>!<C0><09>!<FB>TD-<18><12><FB>-<02><E3><81><93><E3><82><93><E3><81><AB><E3><81><A1><E3><81><AF><E4><B8><96><E7><95><8C><00>'")
			);
			Assert.That(
				TuPack.Pack(prefix, STuple.Create(new object[] { "hello world", 123, false, new byte[] { 123, 1, 66, 0, 42 } })).ToString(),
				Is.EqualTo("ABC<02>hello world<00><15>{&<01>{<01>B<00><FF>*<00>")
			);
			Assert.That(
				TuPack.Pack(prefix, STuple.FromArray(new object[] { "hello world", 123, false, new byte[] { 123, 1, 66, 0, 42 } }, 1, 2)).ToString(),
				Is.EqualTo("ABC<15>{&")
			);
			Assert.That(
				TuPack.Pack(prefix, STuple.FromEnumerable(new List<object> { "hello world", 123, false, new byte[] { 123, 1, 66, 0, 42 } })).ToString(),
				Is.EqualTo("ABC<02>hello world<00><15>{&<01>{<01>B<00><FF>*<00>")
			);

			// Nil or Empty slice should be equivalent to no prefix
			Assert.That(
				TuPack.Pack(Slice.Nil, STuple.Create("hello world", 123)).ToString(),
				Is.EqualTo("<02>hello world<00><15>{")
			);
			Assert.That(
				TuPack.Pack(Slice.Empty, STuple.Create("hello world", 123)).ToString(),
				Is.EqualTo("<02>hello world<00><15>{")
			);
		}

		[Test]
		public void Test_TuplePack_PackTuples()
		{
			{
				Slice[] slices;
				var tuples = new IVarTuple[]
				{
					STuple.Create("hello"),
					STuple.Create(123),
					STuple.Create(false),
					STuple.Create("world", 456, true)
				};

				// array version
				slices = TuPack.PackTuples(tuples);
				Assert.That(slices, Is.Not.Null);
				Assert.That(slices.Length, Is.EqualTo(tuples.Length));
				Assert.That(slices, Is.EqualTo(tuples.Select(t => TuPack.Pack(t))));

				// IEnumerable version that is passed an array
				slices = tuples.PackTuples();
				Assert.That(slices, Is.Not.Null);
				Assert.That(slices.Length, Is.EqualTo(tuples.Length));
				Assert.That(slices, Is.EqualTo(tuples.Select(t => TuPack.Pack(t))));

				// IEnumerable version but with a "real" enumerable
				slices = tuples.Select(t => t).PackTuples();
				Assert.That(slices, Is.Not.Null);
				Assert.That(slices.Length, Is.EqualTo(tuples.Length));
				Assert.That(slices, Is.EqualTo(tuples.Select(t => TuPack.Pack(t))));
			}

			//Optimized STuple<...> versions

			{
				var packed = TuPack.PackTuples(
					STuple.Create("Hello"),
					STuple.Create(123, true),
					STuple.Create(Math.PI, -1234L)
				);
				Assert.That(packed, Is.Not.Null.And.Length.EqualTo(3));
				Assert.That(packed[0].ToString(), Is.EqualTo("<02>Hello<00>"));
				Assert.That(packed[1].ToString(), Is.EqualTo("<15>{'"));
				Assert.That(packed[2].ToString(), Is.EqualTo("!<C0><09>!<FB>TD-<18><12><FB>-"));
				Assert.That(packed[1].Array, Is.SameAs(packed[0].Array), "Should share same bufer");
				Assert.That(packed[2].Array, Is.SameAs(packed[0].Array), "Should share same bufer");
			}

			{
				var packed = TuPack.PackTuples(
					STuple.Create(123),
					STuple.Create(456),
					STuple.Create(789)
				);
				Assert.That(packed, Is.Not.Null.And.Length.EqualTo(3));
				Assert.That(packed[0].ToString(), Is.EqualTo("<15>{"));
				Assert.That(packed[1].ToString(), Is.EqualTo("<16><01><C8>"));
				Assert.That(packed[2].ToString(), Is.EqualTo("<16><03><15>"));
				Assert.That(packed[1].Array, Is.SameAs(packed[0].Array), "Should share same bufer");
				Assert.That(packed[2].Array, Is.SameAs(packed[0].Array), "Should share same bufer");
			}

			{
				var packed = TuPack.PackTuples(
					STuple.Create(123, true),
					STuple.Create(456, false),
					STuple.Create(789, false)
				);
				Assert.That(packed, Is.Not.Null.And.Length.EqualTo(3));
				Assert.That(packed[0].ToString(), Is.EqualTo("<15>{'"));
				Assert.That(packed[1].ToString(), Is.EqualTo("<16><01><C8>&"));
				Assert.That(packed[2].ToString(), Is.EqualTo("<16><03><15>&"));
				Assert.That(packed[1].Array, Is.SameAs(packed[0].Array), "Should share same bufer");
				Assert.That(packed[2].Array, Is.SameAs(packed[0].Array), "Should share same bufer");
			}

			{
				var packed = TuPack.PackTuples(
					STuple.Create("foo", 123, true),
					STuple.Create("bar", 456, false),
					STuple.Create("baz", 789, false)
				);
				Assert.That(packed, Is.Not.Null.And.Length.EqualTo(3));
				Assert.That(packed[0].ToString(), Is.EqualTo("<02>foo<00><15>{'"));
				Assert.That(packed[1].ToString(), Is.EqualTo("<02>bar<00><16><01><C8>&"));
				Assert.That(packed[2].ToString(), Is.EqualTo("<02>baz<00><16><03><15>&"));
				Assert.That(packed[1].Array, Is.SameAs(packed[0].Array), "Should share same bufer");
				Assert.That(packed[2].Array, Is.SameAs(packed[0].Array), "Should share same bufer");
			}

			{
				var packed = TuPack.PackTuples(
					STuple.Create("foo", 123, true, "yes"),
					STuple.Create("bar", 456, false, "yes"),
					STuple.Create("baz", 789, false, "no")
				);
				Assert.That(packed, Is.Not.Null.And.Length.EqualTo(3));
				Assert.That(packed[0].ToString(), Is.EqualTo("<02>foo<00><15>{'<02>yes<00>"));
				Assert.That(packed[1].ToString(), Is.EqualTo("<02>bar<00><16><01><C8>&<02>yes<00>"));
				Assert.That(packed[2].ToString(), Is.EqualTo("<02>baz<00><16><03><15>&<02>no<00>"));
				Assert.That(packed[1].Array, Is.SameAs(packed[0].Array), "Should share same bufer");
				Assert.That(packed[2].Array, Is.SameAs(packed[0].Array), "Should share same bufer");
			}

			{
				var packed = TuPack.PackTuples(
					STuple.Create("foo", 123, true, "yes", 7),
					STuple.Create("bar", 456, false, "yes", 42),
					STuple.Create("baz", 789, false, "no", 9)
				);
				Assert.That(packed, Is.Not.Null.And.Length.EqualTo(3));
				Assert.That(packed[0].ToString(), Is.EqualTo("<02>foo<00><15>{'<02>yes<00><15><07>"));
				Assert.That(packed[1].ToString(), Is.EqualTo("<02>bar<00><16><01><C8>&<02>yes<00><15>*"));
				Assert.That(packed[2].ToString(), Is.EqualTo("<02>baz<00><16><03><15>&<02>no<00><15><09>"));
				Assert.That(packed[1].Array, Is.SameAs(packed[0].Array), "Should share same bufer");
				Assert.That(packed[2].Array, Is.SameAs(packed[0].Array), "Should share same bufer");
			}

			{
				var packed = TuPack.PackTuples(
					STuple.Create("foo", 123, true, "yes", 7, 1.5d),
					STuple.Create("bar", 456, false, "yes", 42, 0.7d),
					STuple.Create("baz", 789, false, "no", 9, 0.66d)
				);
				Assert.That(packed, Is.Not.Null.And.Length.EqualTo(3));
				Assert.That(packed[0].ToString(), Is.EqualTo("<02>foo<00><15>{'<02>yes<00><15><07>!<BF><F8><00><00><00><00><00><00>"));
				Assert.That(packed[1].ToString(), Is.EqualTo("<02>bar<00><16><01><C8>&<02>yes<00><15>*!<BF><E6>ffffff"));
				Assert.That(packed[2].ToString(), Is.EqualTo("<02>baz<00><16><03><15>&<02>no<00><15><09>!<BF><E5><1E><B8>Q<EB><85><1F>"));
				Assert.That(packed[1].Array, Is.SameAs(packed[0].Array), "Should share same bufer");
				Assert.That(packed[2].Array, Is.SameAs(packed[0].Array), "Should share same bufer");
			}
		}

		[Test]
		public void Test_TuplePack_PackTuples_With_Prefix()
		{
			Slice prefix = Slice.FromString("ABC");

			{
				Slice[] slices;
				var tuples = new IVarTuple[]
				{
					STuple.Create("hello"),
					STuple.Create(123),
					STuple.Create(false),
					STuple.Create("world", 456, true)
				};

				// array version
				slices = TuPack.PackTuples(prefix, tuples);
				Assert.That(slices, Is.Not.Null);
				Assert.That(slices.Length, Is.EqualTo(tuples.Length));
				Assert.That(slices, Is.EqualTo(tuples.Select(t => prefix + TuPack.Pack(t))));

				// LINQ version
				slices = TuPack.PackTuples(prefix, tuples.Select(x => x));
				Assert.That(slices, Is.Not.Null);
				Assert.That(slices.Length, Is.EqualTo(tuples.Length));
				Assert.That(slices, Is.EqualTo(tuples.Select(t => prefix + TuPack.Pack(t))));

			}

			//Optimized STuple<...> versions

			{
				var packed = TuPack.PackTuples(
					prefix,
					STuple.Create("Hello"),
					STuple.Create(123, true),
					STuple.Create(Math.PI, -1234L)
				);
				Assert.That(packed, Is.Not.Null.And.Length.EqualTo(3));
				Assert.That(packed[0].ToString(), Is.EqualTo("ABC<02>Hello<00>"));
				Assert.That(packed[1].ToString(), Is.EqualTo("ABC<15>{'"));
				Assert.That(packed[2].ToString(), Is.EqualTo("ABC!<C0><09>!<FB>TD-<18><12><FB>-"));
				Assert.That(packed[1].Array, Is.SameAs(packed[0].Array), "Should share same bufer");
				Assert.That(packed[2].Array, Is.SameAs(packed[0].Array), "Should share same bufer");
			}

			{
				var packed = TuPack.PackTuples(
					prefix,
					STuple.Create(123),
					STuple.Create(456),
					STuple.Create(789)
				);
				Assert.That(packed, Is.Not.Null.And.Length.EqualTo(3));
				Assert.That(packed[0].ToString(), Is.EqualTo("ABC<15>{"));
				Assert.That(packed[1].ToString(), Is.EqualTo("ABC<16><01><C8>"));
				Assert.That(packed[2].ToString(), Is.EqualTo("ABC<16><03><15>"));
				Assert.That(packed[1].Array, Is.SameAs(packed[0].Array), "Should share same bufer");
				Assert.That(packed[2].Array, Is.SameAs(packed[0].Array), "Should share same bufer");
			}

			{
				var packed = TuPack.PackTuples(
					prefix,
					STuple.Create(123, true),
					STuple.Create(456, false),
					STuple.Create(789, false)
				);
				Assert.That(packed, Is.Not.Null.And.Length.EqualTo(3));
				Assert.That(packed[0].ToString(), Is.EqualTo("ABC<15>{'"));
				Assert.That(packed[1].ToString(), Is.EqualTo("ABC<16><01><C8>&"));
				Assert.That(packed[2].ToString(), Is.EqualTo("ABC<16><03><15>&"));
				Assert.That(packed[1].Array, Is.SameAs(packed[0].Array), "Should share same bufer");
				Assert.That(packed[2].Array, Is.SameAs(packed[0].Array), "Should share same bufer");
			}

			{
				var packed = TuPack.PackTuples(
					prefix,
					STuple.Create("foo", 123, true),
					STuple.Create("bar", 456, false),
					STuple.Create("baz", 789, false)
				);
				Assert.That(packed, Is.Not.Null.And.Length.EqualTo(3));
				Assert.That(packed[0].ToString(), Is.EqualTo("ABC<02>foo<00><15>{'"));
				Assert.That(packed[1].ToString(), Is.EqualTo("ABC<02>bar<00><16><01><C8>&"));
				Assert.That(packed[2].ToString(), Is.EqualTo("ABC<02>baz<00><16><03><15>&"));
				Assert.That(packed[1].Array, Is.SameAs(packed[0].Array), "Should share same bufer");
				Assert.That(packed[2].Array, Is.SameAs(packed[0].Array), "Should share same bufer");
			}

		}

		[Test]
		public void Test_TuplePack_EncodeKey()
		{
			Assert.That(
				TuPack.EncodeKey("hello world").ToString(),
				Is.EqualTo("<02>hello world<00>")
			);
			Assert.That(
				TuPack.EncodeKey("hello", "world").ToString(),
				Is.EqualTo("<02>hello<00><02>world<00>")
			);
			Assert.That(
				TuPack.EncodeKey("hello world", 123).ToString(),
				Is.EqualTo("<02>hello world<00><15>{")
			);
			Assert.That(
				TuPack.EncodeKey("hello world", 1234, -1234).ToString(),
				Is.EqualTo("<02>hello world<00><16><04><D2><12><FB>-")
			);
			Assert.That(
				TuPack.EncodeKey("hello world", 123, false).ToString(),
				Is.EqualTo("<02>hello world<00><15>{&")
			);
			Assert.That(
				TuPack.EncodeKey("hello world", 123, false, new byte[] {123, 1, 66, 0, 42}).ToString(),
				Is.EqualTo("<02>hello world<00><15>{&<01>{<01>B<00><FF>*<00>")
			);
			Assert.That(
				TuPack.EncodeKey("hello world", 123, false, new byte[] { 123, 1, 66, 0, 42 }, Math.PI).ToString(),
				Is.EqualTo("<02>hello world<00><15>{&<01>{<01>B<00><FF>*<00>!<C0><09>!<FB>TD-<18>")
			);
			Assert.That(
				TuPack.EncodeKey("hello world", 123, false, new byte[] { 123, 1, 66, 0, 42 }, Math.PI, -1234L).ToString(),
				Is.EqualTo("<02>hello world<00><15>{&<01>{<01>B<00><FF>*<00>!<C0><09>!<FB>TD-<18><12><FB>-")
			);
			Assert.That(
				TuPack.EncodeKey("hello world", 123, false, new byte[] { 123, 1, 66, 0, 42 }, Math.PI, -1234L, "こんにちは世界").ToString(),
				Is.EqualTo("<02>hello world<00><15>{&<01>{<01>B<00><FF>*<00>!<C0><09>!<FB>TD-<18><12><FB>-<02><E3><81><93><E3><82><93><E3><81><AB><E3><81><A1><E3><81><AF><E4><B8><96><E7><95><8C><00>")
			);
			Assert.That(
				TuPack.EncodeKey("hello world", 123, false, new byte[] { 123, 1, 66, 0, 42 }, Math.PI, -1234L, "こんにちは世界", true).ToString(),
				Is.EqualTo("<02>hello world<00><15>{&<01>{<01>B<00><FF>*<00>!<C0><09>!<FB>TD-<18><12><FB>-<02><E3><81><93><E3><82><93><E3><81><AB><E3><81><A1><E3><81><AF><E4><B8><96><E7><95><8C><00>'")
			);

		}

		[Test]
		public void Test_TuplePack_EncodeKey_With_Prefix()
		{
			Slice prefix = Slice.FromString("ABC");

			Assert.That(
				TuPack.EncodePrefixedKey(prefix, "hello world").ToString(),
				Is.EqualTo("ABC<02>hello world<00>")
			);
			Assert.That(
				TuPack.EncodePrefixedKey(prefix, "hello", "world").ToString(),
				Is.EqualTo("ABC<02>hello<00><02>world<00>")
			);
			Assert.That(
				TuPack.EncodePrefixedKey(prefix, "hello world", 123).ToString(),
				Is.EqualTo("ABC<02>hello world<00><15>{")
			);
			Assert.That(
				TuPack.EncodePrefixedKey(prefix, "hello world", 1234, -1234).ToString(),
				Is.EqualTo("ABC<02>hello world<00><16><04><D2><12><FB>-")
			);
			Assert.That(
				TuPack.EncodePrefixedKey(prefix, "hello world", 123, false).ToString(),
				Is.EqualTo("ABC<02>hello world<00><15>{&")
			);
			Assert.That(
				TuPack.EncodePrefixedKey(prefix, "hello world", 123, false, new byte[] { 123, 1, 66, 0, 42 }).ToString(),
				Is.EqualTo("ABC<02>hello world<00><15>{&<01>{<01>B<00><FF>*<00>")
			);
			Assert.That(
				TuPack.EncodePrefixedKey(prefix, "hello world", 123, false, new byte[] { 123, 1, 66, 0, 42 }, Math.PI).ToString(),
				Is.EqualTo("ABC<02>hello world<00><15>{&<01>{<01>B<00><FF>*<00>!<C0><09>!<FB>TD-<18>")
			);
			Assert.That(
				TuPack.EncodePrefixedKey(prefix, "hello world", 123, false, new byte[] { 123, 1, 66, 0, 42 }, Math.PI, -1234L).ToString(),
				Is.EqualTo("ABC<02>hello world<00><15>{&<01>{<01>B<00><FF>*<00>!<C0><09>!<FB>TD-<18><12><FB>-")
			);
			Assert.That(
				TuPack.EncodePrefixedKey(prefix, "hello world", 123, false, new byte[] { 123, 1, 66, 0, 42 }, Math.PI, -1234L, "こんにちは世界").ToString(),
				Is.EqualTo("ABC<02>hello world<00><15>{&<01>{<01>B<00><FF>*<00>!<C0><09>!<FB>TD-<18><12><FB>-<02><E3><81><93><E3><82><93><E3><81><AB><E3><81><A1><E3><81><AF><E4><B8><96><E7><95><8C><00>")
			);
			Assert.That(
				TuPack.EncodePrefixedKey(prefix, "hello world", 123, false, new byte[] { 123, 1, 66, 0, 42 }, Math.PI, -1234L, "こんにちは世界", true).ToString(),
				Is.EqualTo("ABC<02>hello world<00><15>{&<01>{<01>B<00><FF>*<00>!<C0><09>!<FB>TD-<18><12><FB>-<02><E3><81><93><E3><82><93><E3><81><AB><E3><81><A1><E3><81><AF><E4><B8><96><E7><95><8C><00>'")
			);

		}

		[Test]
		public void Test_TuplePack_EncodeKey_Boxed()
		{
			Slice slice;

			slice = TuPack.EncodeKey<object>(default(object));
			Assert.That(slice.ToString(), Is.EqualTo("<00>"));

			slice = TuPack.EncodeKey<object>(1);
			Assert.That(slice.ToString(), Is.EqualTo("<15><01>"));

			slice = TuPack.EncodeKey<object>(1L);
			Assert.That(slice.ToString(), Is.EqualTo("<15><01>"));

			slice = TuPack.EncodeKey<object>(1U);
			Assert.That(slice.ToString(), Is.EqualTo("<15><01>"));

			slice = TuPack.EncodeKey<object>(1UL);
			Assert.That(slice.ToString(), Is.EqualTo("<15><01>"));

			slice = TuPack.EncodeKey<object>(false);
			Assert.That(slice.ToString(), Is.EqualTo("&"));

			slice = TuPack.EncodeKey<object>(true);
			Assert.That(slice.ToString(), Is.EqualTo("'"));

			slice = TuPack.EncodeKey<object>(new byte[] {4, 5, 6});
			Assert.That(slice.ToString(), Is.EqualTo("<01><04><05><06><00>"));

			slice = TuPack.EncodeKey<object>("hello");
			Assert.That(slice.ToString(), Is.EqualTo("<02>hello<00>"));
		}

		[Test]
		public void Test_TuplePack_EncodeKeys()
		{
			//Optimized STuple<...> versions

			{
				var packed = TuPack.EncodeKeys(
					"foo",
					"bar",
					"baz"
				);
				Assert.That(packed, Is.Not.Null.And.Length.EqualTo(3));
				Assert.That(packed[0].ToString(), Is.EqualTo("<02>foo<00>"));
				Assert.That(packed[1].ToString(), Is.EqualTo("<02>bar<00>"));
				Assert.That(packed[2].ToString(), Is.EqualTo("<02>baz<00>"));
				Assert.That(packed[1].Array, Is.SameAs(packed[0].Array), "Should share same bufer");
				Assert.That(packed[2].Array, Is.SameAs(packed[0].Array), "Should share same bufer");
			}

			{
				var packed = TuPack.EncodeKeys(
					123,
					456,
					789
				);
				Assert.That(packed, Is.Not.Null.And.Length.EqualTo(3));
				Assert.That(packed[0].ToString(), Is.EqualTo("<15>{"));
				Assert.That(packed[1].ToString(), Is.EqualTo("<16><01><C8>"));
				Assert.That(packed[2].ToString(), Is.EqualTo("<16><03><15>"));
				Assert.That(packed[1].Array, Is.SameAs(packed[0].Array), "Should share same bufer");
				Assert.That(packed[2].Array, Is.SameAs(packed[0].Array), "Should share same bufer");
			}


			{
				var packed = TuPack.EncodeKeys(Enumerable.Range(0, 3));
				Assert.That(packed, Is.Not.Null.And.Length.EqualTo(3));
				Assert.That(packed[0].ToString(), Is.EqualTo("<14>"));
				Assert.That(packed[1].ToString(), Is.EqualTo("<15><01>"));
				Assert.That(packed[2].ToString(), Is.EqualTo("<15><02>"));
				Assert.That(packed[1].Array, Is.SameAs(packed[0].Array), "Should share same bufer");
				Assert.That(packed[2].Array, Is.SameAs(packed[0].Array), "Should share same bufer");
			}

			{
				var packed = TuPack.EncodeKeys(new[] {"Bonjour", "le", "Monde"}, (s) => s.Length);
				Assert.That(packed, Is.Not.Null.And.Length.EqualTo(3));
				Assert.That(packed[0].ToString(), Is.EqualTo("<15><07>"));
				Assert.That(packed[1].ToString(), Is.EqualTo("<15><02>"));
				Assert.That(packed[2].ToString(), Is.EqualTo("<15><05>"));
				Assert.That(packed[1].Array, Is.SameAs(packed[0].Array), "Should share same bufer");
				Assert.That(packed[2].Array, Is.SameAs(packed[0].Array), "Should share same bufer");
			}

		}

		[Test]
		public void Test_TuplePack_EncodeKeys_With_Prefix()
		{
			Slice prefix = Slice.FromString("ABC");

			{
				var packed = TuPack.EncodePrefixedKeys(
					prefix,
					"foo",
					"bar",
					"baz"
				);
				Assert.That(packed, Is.Not.Null.And.Length.EqualTo(3));
				Assert.That(packed[0].ToString(), Is.EqualTo("ABC<02>foo<00>"));
				Assert.That(packed[1].ToString(), Is.EqualTo("ABC<02>bar<00>"));
				Assert.That(packed[2].ToString(), Is.EqualTo("ABC<02>baz<00>"));
				Assert.That(packed[1].Array, Is.SameAs(packed[0].Array), "Should share same bufer");
				Assert.That(packed[2].Array, Is.SameAs(packed[0].Array), "Should share same bufer");
			}

			{
				var packed = TuPack.EncodePrefixedKeys(
					prefix,
					123,
					456,
					789
				);
				Assert.That(packed, Is.Not.Null.And.Length.EqualTo(3));
				Assert.That(packed[0].ToString(), Is.EqualTo("ABC<15>{"));
				Assert.That(packed[1].ToString(), Is.EqualTo("ABC<16><01><C8>"));
				Assert.That(packed[2].ToString(), Is.EqualTo("ABC<16><03><15>"));
				Assert.That(packed[1].Array, Is.SameAs(packed[0].Array), "Should share same bufer");
				Assert.That(packed[2].Array, Is.SameAs(packed[0].Array), "Should share same bufer");
			}

			{
				var packed = TuPack.EncodePrefixedKeys(
					prefix,
					new[] { "Bonjour", "le", "Monde" },
					(s) => s.Length
				);
				Assert.That(packed, Is.Not.Null.And.Length.EqualTo(3));
				Assert.That(packed[0].ToString(), Is.EqualTo("ABC<15><07>"));
				Assert.That(packed[1].ToString(), Is.EqualTo("ABC<15><02>"));
				Assert.That(packed[2].ToString(), Is.EqualTo("ABC<15><05>"));
				Assert.That(packed[1].Array, Is.SameAs(packed[0].Array), "Should share same bufer");
				Assert.That(packed[2].Array, Is.SameAs(packed[0].Array), "Should share same bufer");
			}

		}

		[Test]
		public void Test_TuplePack_SerializersOfT()
		{
			Slice prefix = Slice.FromString("ABC");
			{
				var serializer = TupleSerializer<int>.Default;
				var t = STuple.Create(123);
				var tw = new TupleWriter();
				tw.Output.WriteBytes(prefix);
				serializer.PackTo(ref tw, in t);
				Assert.That(tw.ToSlice().ToString(), Is.EqualTo("ABC<15>{"));
			}
			{
				var serializer = TupleSerializer<string>.Default;
				var t = STuple.Create("foo");
				var tw = new TupleWriter();
				tw.Output.WriteBytes(prefix);
				serializer.PackTo(ref tw, in t);
				Assert.That(tw.ToSlice().ToString(), Is.EqualTo("ABC<02>foo<00>"));
			}

			{
				var serializer = TupleSerializer<string, int>.Default;
				var t = STuple.Create("foo", 123);
				var tw = new TupleWriter();
				tw.Output.WriteBytes(prefix);
				serializer.PackTo(ref tw, in t);
				Assert.That(tw.ToSlice().ToString(), Is.EqualTo("ABC<02>foo<00><15>{"));
			}

			{
				var serializer = TupleSerializer<string, bool, int>.Default;
				var t = STuple.Create("foo", false, 123);
				var tw = new TupleWriter();
				tw.Output.WriteBytes(prefix);
				serializer.PackTo(ref tw, in t);
				Assert.That(tw.ToSlice().ToString(), Is.EqualTo("ABC<02>foo<00>&<15>{"));
			}

			{
				var serializer = TupleSerializer<string, bool, int, long>.Default;
				var t = STuple.Create("foo", false, 123, -1L);
				var tw = new TupleWriter();
				tw.Output.WriteBytes(prefix);
				serializer.PackTo(ref tw, in t);
				Assert.That(tw.ToSlice().ToString(), Is.EqualTo("ABC<02>foo<00>&<15>{<13><FE>"));
			}

			{
				var serializer = TupleSerializer<string, bool, int, long, string>.Default;
				var t = STuple.Create("foo", false, 123, -1L, "narf");
				var tw = new TupleWriter();
				tw.Output.WriteBytes(prefix);
				serializer.PackTo(ref tw, in t);
				Assert.That(tw.ToSlice().ToString(), Is.EqualTo("ABC<02>foo<00>&<15>{<13><FE><02>narf<00>"));
			}

			{
				var serializer = TupleSerializer<string, bool, int, long, string, double>.Default;
				var t = STuple.Create("foo", false, 123, -1L, "narf", Math.PI);
				var tw = new TupleWriter();
				tw.Output.WriteBytes(prefix);
				serializer.PackTo(ref tw, in t);
				Assert.That(tw.ToSlice().ToString(), Is.EqualTo("ABC<02>foo<00>&<15>{<13><FE><02>narf<00>!<C0><09>!<FB>TD-<18>"));
			}

		}
		[Test]
		public void Test_TuplePack_Unpack()
		{

			var packed = TuPack.EncodeKey("hello world");
			Log(packed);

			var tuple = TuPack.Unpack(packed);
			Assert.That(tuple, Is.Not.Null);
			Log(tuple);
			Assert.That(tuple.Count, Is.EqualTo(1));
			Assert.That(tuple.Get<string>(0), Is.EqualTo("hello world"));

			packed = TuPack.EncodeKey("hello world", 123);
			Log(packed);

			tuple = TuPack.Unpack(packed);
			Assert.That(tuple, Is.Not.Null);
			Log(tuple);
			Assert.That(tuple.Count, Is.EqualTo(2));
			Assert.That(tuple.Get<string>(0), Is.EqualTo("hello world"));
			Assert.That(tuple.Get<int>(1), Is.EqualTo(123));

			packed = TuPack.EncodeKey(1, 256, 257, 65536, int.MaxValue, long.MaxValue);
			Log(packed);

			tuple = TuPack.Unpack(packed);
			Assert.That(tuple, Is.Not.Null);
			Assert.That(tuple.Count, Is.EqualTo(6));
			Assert.That(tuple.Get<int>(0), Is.EqualTo(1));
			Assert.That(tuple.Get<int>(1), Is.EqualTo(256));
			Assert.That(tuple.Get<int>(2), Is.EqualTo(257), ((SlicedTuple) tuple).GetSlice(2).ToString());
			Assert.That(tuple.Get<int>(3), Is.EqualTo(65536));
			Assert.That(tuple.Get<int>(4), Is.EqualTo(int.MaxValue));
			Assert.That(tuple.Get<long>(5), Is.EqualTo(long.MaxValue));

			packed = TuPack.EncodeKey(-1, -256, -257, -65536, int.MinValue, long.MinValue);
			Log(packed);

			tuple = TuPack.Unpack(packed);
			Assert.That(tuple, Is.Not.Null);
			Assert.That(tuple, Is.InstanceOf<SlicedTuple>());
			Log(tuple);
			Assert.That(tuple.Count, Is.EqualTo(6));
			Assert.That(tuple.Get<int>(0), Is.EqualTo(-1));
			Assert.That(tuple.Get<int>(1), Is.EqualTo(-256));
			Assert.That(tuple.Get<int>(2), Is.EqualTo(-257), "Slice is " + ((SlicedTuple) tuple).GetSlice(2).ToString());
			Assert.That(tuple.Get<int>(3), Is.EqualTo(-65536));
			Assert.That(tuple.Get<int>(4), Is.EqualTo(int.MinValue));
			Assert.That(tuple.Get<long>(5), Is.EqualTo(long.MinValue));
		}

		[Test]
		public void Test_TuplePack_DecodeKey()
		{
			Assert.That(
				TuPack.DecodeKey<string>(Slice.Unescape("<02>hello world<00>")),
				Is.EqualTo("hello world")
			);
			Assert.That(
				TuPack.DecodeKey<int>(Slice.Unescape("<15>{")),
				Is.EqualTo(123)
			);
			Assert.That(
				TuPack.DecodeKey<string, string>(Slice.Unescape("<02>hello<00><02>world<00>")),
				Is.EqualTo(STuple.Create("hello", "world"))
			);
			Assert.That(
				TuPack.DecodeKey<string, int>(Slice.Unescape("<02>hello world<00><15>{")),
				Is.EqualTo(STuple.Create("hello world", 123))
			);
			Assert.That(
				TuPack.DecodeKey<string, int, long>(Slice.Unescape("<02>hello world<00><16><04><D2><12><FB>-")),
				Is.EqualTo(STuple.Create("hello world", 1234, -1234L))
			);
			Assert.That(
				TuPack.DecodeKey<string, int, bool>(Slice.Unescape("<02>hello world<00><15>{&")),
				Is.EqualTo(STuple.Create("hello world", 123, false))
			);
			Assert.That(
				TuPack.DecodeKey<string, int, bool, Slice>(Slice.Unescape("<02>hello world<00><15>{&<01>{<01>B<00><FF>*<00>")),
				Is.EqualTo(STuple.Create("hello world", 123, false, new byte[] { 123, 1, 66, 0, 42 }.AsSlice()))
			);
			Assert.That(
				TuPack.DecodeKey<string, int, bool, Slice, double>(Slice.Unescape("<02>hello world<00><15>{&<01>{<01>B<00><FF>*<00>!<C0><09>!<FB>TD-<18>")),
				Is.EqualTo(STuple.Create("hello world", 123, false, new byte[] { 123, 1, 66, 0, 42 }.AsSlice(), Math.PI))
			);
			Assert.That(
				TuPack.DecodeKey<string, int, bool, Slice, double, long>(Slice.Unescape("<02>hello world<00><15>{&<01>{<01>B<00><FF>*<00>!<C0><09>!<FB>TD-<18><12><FB>-")),
				Is.EqualTo(STuple.Create("hello world", 123, false, new byte[] { 123, 1, 66, 0, 42 }.AsSlice(), Math.PI, -1234L))
			);
			//TODO: if/when we have tuples with 7 or 8 items...
			//Assert.That(
			//	TuPack.DecodeKey<string, int, bool, Slice, double, long, string>(Slice.Unescape("<02>hello world<00><15>{&<01>{<01>B<00><FF>*<00>!<C0><09>!<FB>TD-<18><12><FB>-<02><E3><81><93><E3><82><93><E3><81><AB><E3><81><A1><E3><81><AF><E4><B8><96><E7><95><8C><00>")),
			//	Is.EqualTo(STuple.Create("hello world", 123, false, Slice.Create(new byte[] { 123, 1, 66, 0, 42 }), Math.PI, -1234L, "こんにちは世界"))
			//);
			//Assert.That(
			//	TuPack.DecodeKey<string, int, bool, Slice, double, long, string, bool>(Slice.Unescape("<02>hello world<00><15>{&<01>{<01>B<00><FF>*<00>!<C0><09>!<FB>TD-<18><12><FB>-<02><E3><81><93><E3><82><93><E3><81><AB><E3><81><A1><E3><81><AF><E4><B8><96><E7><95><8C><00><15><01>")),
			//	Is.EqualTo(STuple.Create("hello world", 123, false, Slice.Create(new byte[] { 123, 1, 66, 0, 42 }), Math.PI, -1234L, "こんにちは世界", true))
			//);
		}

		[Test]
		public void Test_TuplePack_EncodeKeys_Of_T()
		{
			Slice[] slices;

			#region PackRange(Tuple, ...)

			var tuple = STuple.Create("hello");
			int[] items = new int[] {1, 2, 3, 123, -1, int.MaxValue};

			// array version
			slices = TuPack.EncodePrefixedKeys<int>(tuple, items);
			Assert.That(slices, Is.Not.Null);
			Assert.That(slices.Length, Is.EqualTo(items.Length));
			Assert.That(slices, Is.EqualTo(items.Select(x => TuPack.Pack(tuple.Append(x)))));

			// IEnumerable version that is passed an array
			slices = TuPack.EncodePrefixedKeys<int>(tuple, (IEnumerable<int>) items);
			Assert.That(slices, Is.Not.Null);
			Assert.That(slices.Length, Is.EqualTo(items.Length));
			Assert.That(slices, Is.EqualTo(items.Select(x => TuPack.Pack(tuple.Append(x)))));

			// IEnumerable version but with a "real" enumerable
			slices = TuPack.EncodePrefixedKeys<int>(tuple, items.Select(t => t));
			Assert.That(slices, Is.Not.Null);
			Assert.That(slices.Length, Is.EqualTo(items.Length));
			Assert.That(slices, Is.EqualTo(items.Select(x => TuPack.Pack(tuple.Append(x)))));

			#endregion

			#region PackRange(Slice, ...)

			string[] words = {"hello", "world", "très bien", "断トツ", "abc\0def", null, String.Empty};

			var merged = TuPack.EncodePrefixedKeys(Slice.FromByte(42), words);
			Assert.That(merged, Is.Not.Null);
			Assert.That(merged.Length, Is.EqualTo(words.Length));

			for (int i = 0; i < words.Length; i++)
			{
				var expected = Slice.FromByte(42) + TuPack.EncodeKey(words[i]);
				Assert.That(merged[i], Is.EqualTo(expected));

				Assert.That(merged[i].Array, Is.SameAs(merged[0].Array), "All slices should be stored in the same buffer");
				if (i > 0) Assert.That(merged[i].Offset, Is.EqualTo(merged[i - 1].Offset + merged[i - 1].Count), "All slices should be contiguous");
			}

			// corner cases
			// ReSharper disable AssignNullToNotNullAttribute
			Assert.That(
				() => TuPack.EncodePrefixedKeys<int>(Slice.Empty, default(int[])),
				Throws.InstanceOf<ArgumentNullException>().With.Property("ParamName").EqualTo("keys"));
			Assert.That(
				() => TuPack.EncodePrefixedKeys<int>(Slice.Empty, default(IEnumerable<int>)),
				Throws.InstanceOf<ArgumentNullException>().With.Property("ParamName").EqualTo("keys"));
			// ReSharper restore AssignNullToNotNullAttribute

			#endregion
		}

		[Test]
		public void Test_TuplePack_EncodeKeys_Boxed()
		{
			Slice[] slices;
			var tuple = STuple.Create("hello");
			object[] items = {"world", 123, false, Guid.NewGuid(), long.MinValue};

			// array version
			slices = TuPack.EncodePrefixedKeys<object>(tuple, items);
			Assert.That(slices, Is.Not.Null);
			Assert.That(slices.Length, Is.EqualTo(items.Length));
			Assert.That(slices, Is.EqualTo(items.Select(x => TuPack.Pack(tuple.Append(x)))));

			// IEnumerable version that is passed an array
			slices = TuPack.EncodePrefixedKeys<object>(tuple, (IEnumerable<object>) items);
			Assert.That(slices, Is.Not.Null);
			Assert.That(slices.Length, Is.EqualTo(items.Length));
			Assert.That(slices, Is.EqualTo(items.Select(x => TuPack.Pack(tuple.Append(x)))));

			// IEnumerable version but with a "real" enumerable
			slices = TuPack.EncodePrefixedKeys<object>(tuple, items.Select(t => t));
			Assert.That(slices, Is.Not.Null);
			Assert.That(slices.Length, Is.EqualTo(items.Length));
			Assert.That(slices, Is.EqualTo(items.Select(x => TuPack.Pack(tuple.Append(x)))));
		}

		[Test]
		public void Test_TuplePack_Unpack_First_And_Last()
		{
			// should only work with tuples having at least one element

			Slice packed;

			packed = TuPack.EncodeKey(1);
			Assert.That(TuPack.DecodeFirst<int>(packed), Is.EqualTo(1));
			Assert.That(TuPack.DecodeFirst<string>(packed), Is.EqualTo("1"));
			Assert.That(TuPack.DecodeLast<int>(packed), Is.EqualTo(1));
			Assert.That(TuPack.DecodeLast<string>(packed), Is.EqualTo("1"));

			packed = TuPack.EncodeKey(1, 2);
			Assert.That(TuPack.DecodeFirst<int>(packed), Is.EqualTo(1));
			Assert.That(TuPack.DecodeFirst<string>(packed), Is.EqualTo("1"));
			Assert.That(TuPack.DecodeLast<int>(packed), Is.EqualTo(2));
			Assert.That(TuPack.DecodeLast<string>(packed), Is.EqualTo("2"));

			packed = TuPack.EncodeKey(1, 2, 3);
			Assert.That(TuPack.DecodeFirst<int>(packed), Is.EqualTo(1));
			Assert.That(TuPack.DecodeFirst<string>(packed), Is.EqualTo("1"));
			Assert.That(TuPack.DecodeLast<int>(packed), Is.EqualTo(3));
			Assert.That(TuPack.DecodeLast<string>(packed), Is.EqualTo("3"));

			packed = TuPack.EncodeKey(1, 2, 3, 4);
			Assert.That(TuPack.DecodeFirst<int>(packed), Is.EqualTo(1));
			Assert.That(TuPack.DecodeFirst<string>(packed), Is.EqualTo("1"));
			Assert.That(TuPack.DecodeLast<int>(packed), Is.EqualTo(4));
			Assert.That(TuPack.DecodeLast<string>(packed), Is.EqualTo("4"));

			packed = TuPack.EncodeKey(1, 2, 3, 4, 5);
			Assert.That(TuPack.DecodeFirst<int>(packed), Is.EqualTo(1));
			Assert.That(TuPack.DecodeFirst<string>(packed), Is.EqualTo("1"));
			Assert.That(TuPack.DecodeLast<int>(packed), Is.EqualTo(5));
			Assert.That(TuPack.DecodeLast<string>(packed), Is.EqualTo("5"));

			packed = TuPack.EncodeKey(1, 2, 3, 4, 5, 6);
			Assert.That(TuPack.DecodeFirst<int>(packed), Is.EqualTo(1));
			Assert.That(TuPack.DecodeFirst<string>(packed), Is.EqualTo("1"));
			Assert.That(TuPack.DecodeLast<int>(packed), Is.EqualTo(6));
			Assert.That(TuPack.DecodeLast<string>(packed), Is.EqualTo("6"));

			packed = TuPack.EncodeKey(1, 2, 3, 4, 5, 6, 7);
			Assert.That(TuPack.DecodeFirst<int>(packed), Is.EqualTo(1));
			Assert.That(TuPack.DecodeFirst<string>(packed), Is.EqualTo("1"));
			Assert.That(TuPack.DecodeLast<int>(packed), Is.EqualTo(7));
			Assert.That(TuPack.DecodeLast<string>(packed), Is.EqualTo("7"));

			packed = TuPack.EncodeKey(1, 2, 3, 4, 5, 6, 7, 8);
			Assert.That(TuPack.DecodeFirst<int>(packed), Is.EqualTo(1));
			Assert.That(TuPack.DecodeFirst<string>(packed), Is.EqualTo("1"));
			Assert.That(TuPack.DecodeLast<int>(packed), Is.EqualTo(8));
			Assert.That(TuPack.DecodeLast<string>(packed), Is.EqualTo("8"));

			Assert.That(() => TuPack.DecodeFirst<string>(Slice.Nil), Throws.InstanceOf<InvalidOperationException>());
			Assert.That(() => TuPack.DecodeFirst<string>(Slice.Empty), Throws.InstanceOf<InvalidOperationException>());
			Assert.That(() => TuPack.DecodeLast<string>(Slice.Nil), Throws.InstanceOf<InvalidOperationException>());
			Assert.That(() => TuPack.DecodeLast<string>(Slice.Empty), Throws.InstanceOf<InvalidOperationException>());

		}

		[Test]
		public void Test_TuplePack_UnpackSingle()
		{
			// should only work with tuples having exactly one element

			Slice packed;

			packed = TuPack.EncodeKey(1);
			Assert.That(TuPack.DecodeKey<int>(packed), Is.EqualTo(1));
			Assert.That(TuPack.DecodeKey<string>(packed), Is.EqualTo("1"));

			packed = TuPack.EncodeKey("Hello\0World");
			Assert.That(TuPack.DecodeKey<string>(packed), Is.EqualTo("Hello\0World"));

			Assert.That(() => TuPack.DecodeKey<string>(Slice.Nil), Throws.InstanceOf<InvalidOperationException>());
			Assert.That(() => TuPack.DecodeKey<string>(Slice.Empty), Throws.InstanceOf<InvalidOperationException>());
			Assert.That(() => TuPack.DecodeKey<int>(TuPack.EncodeKey(1, 2)), Throws.InstanceOf<FormatException>());
			Assert.That(() => TuPack.DecodeKey<int>(TuPack.EncodeKey(1, 2, 3)), Throws.InstanceOf<FormatException>());
			Assert.That(() => TuPack.DecodeKey<int>(TuPack.EncodeKey(1, 2, 3, 4)), Throws.InstanceOf<FormatException>());
			Assert.That(() => TuPack.DecodeKey<int>(TuPack.EncodeKey(1, 2, 3, 4, 5)), Throws.InstanceOf<FormatException>());
			Assert.That(() => TuPack.DecodeKey<int>(TuPack.EncodeKey(1, 2, 3, 4, 5, 6)), Throws.InstanceOf<FormatException>());
			Assert.That(() => TuPack.DecodeKey<int>(TuPack.EncodeKey(1, 2, 3, 4, 5, 6, 7)), Throws.InstanceOf<FormatException>());
			Assert.That(() => TuPack.DecodeKey<int>(TuPack.EncodeKey(1, 2, 3, 4, 5, 6, 7, 8)), Throws.InstanceOf<FormatException>());

		}

		[Test]
		public void Test_TuplePack_ToRange()
		{
			(Slice Begin, Slice End) range;

			// ToRange() should add 0x00 and 0xFF to the packed representations of the tuples
			// note: we cannot increment the key to get the End key, because it conflicts with the Tuple Binary Encoding itself

			// Slice
			range = TuPack.ToRange(Slice.FromString("ABC"));
			Assert.That(range.Begin.ToString(), Is.EqualTo("ABC<00>"), "Begin key should be suffixed by 0x00");
			Assert.That(range.End.ToString(), Is.EqualTo("ABC<FF>"), "End key should be suffixed by 0xFF");

			// Tuples

			range = TuPack.ToRange(STuple.Create("Hello"));
			Assert.That(range.Begin.ToString(), Is.EqualTo("<02>Hello<00><00>"));
			Assert.That(range.End.ToString(), Is.EqualTo("<02>Hello<00><FF>"));

			range = TuPack.ToRange(STuple.Create("Hello", 123));
			Assert.That(range.Begin.ToString(), Is.EqualTo("<02>Hello<00><15>{<00>"));
			Assert.That(range.End.ToString(), Is.EqualTo("<02>Hello<00><15>{<FF>"));

			range = TuPack.ToRange(STuple.Create("Hello", 123, true));
			Assert.That(range.Begin.ToString(), Is.EqualTo("<02>Hello<00><15>{'<00>"));
			Assert.That(range.End.ToString(), Is.EqualTo("<02>Hello<00><15>{'<FF>"));

			range = TuPack.ToRange(STuple.Create("Hello", 123, true, -1234L));
			Assert.That(range.Begin.ToString(), Is.EqualTo("<02>Hello<00><15>{'<12><FB>-<00>"));
			Assert.That(range.End.ToString(), Is.EqualTo("<02>Hello<00><15>{'<12><FB>-<FF>"));

			range = TuPack.ToRange(STuple.Create("Hello", 123, true, -1234L, "こんにちは世界"));
			Assert.That(range.Begin.ToString(), Is.EqualTo("<02>Hello<00><15>{'<12><FB>-<02><E3><81><93><E3><82><93><E3><81><AB><E3><81><A1><E3><81><AF><E4><B8><96><E7><95><8C><00><00>"));
			Assert.That(range.End.ToString(), Is.EqualTo("<02>Hello<00><15>{'<12><FB>-<02><E3><81><93><E3><82><93><E3><81><AB><E3><81><A1><E3><81><AF><E4><B8><96><E7><95><8C><00><FF>"));

			range = TuPack.ToRange(STuple.Create("Hello", 123, true, -1234L, "こんにちは世界", Math.PI));
			Assert.That(range.Begin.ToString(), Is.EqualTo("<02>Hello<00><15>{'<12><FB>-<02><E3><81><93><E3><82><93><E3><81><AB><E3><81><A1><E3><81><AF><E4><B8><96><E7><95><8C><00>!<C0><09>!<FB>TD-<18><00>"));
			Assert.That(range.End.ToString(), Is.EqualTo("<02>Hello<00><15>{'<12><FB>-<02><E3><81><93><E3><82><93><E3><81><AB><E3><81><A1><E3><81><AF><E4><B8><96><E7><95><8C><00>!<C0><09>!<FB>TD-<18><FF>"));

			range = TuPack.ToRange(STuple.Create("Hello", 123, true, -1234L, "こんにちは世界", Math.PI, false));
			Assert.That(range.Begin.ToString(), Is.EqualTo("<02>Hello<00><15>{'<12><FB>-<02><E3><81><93><E3><82><93><E3><81><AB><E3><81><A1><E3><81><AF><E4><B8><96><E7><95><8C><00>!<C0><09>!<FB>TD-<18>&<00>"));
			Assert.That(range.End.ToString(), Is.EqualTo("<02>Hello<00><15>{'<12><FB>-<02><E3><81><93><E3><82><93><E3><81><AB><E3><81><A1><E3><81><AF><E4><B8><96><E7><95><8C><00>!<C0><09>!<FB>TD-<18>&<FF>"));

			range = TuPack.ToRange(STuple.Create("Hello", 123, true, -1234L, "こんにちは世界", Math.PI, false, "TheEnd"));
			Assert.That(range.Begin.ToString(), Is.EqualTo("<02>Hello<00><15>{'<12><FB>-<02><E3><81><93><E3><82><93><E3><81><AB><E3><81><A1><E3><81><AF><E4><B8><96><E7><95><8C><00>!<C0><09>!<FB>TD-<18>&<02>TheEnd<00><00>"));
			Assert.That(range.End.ToString(), Is.EqualTo("<02>Hello<00><15>{'<12><FB>-<02><E3><81><93><E3><82><93><E3><81><AB><E3><81><A1><E3><81><AF><E4><B8><96><E7><95><8C><00>!<C0><09>!<FB>TD-<18>&<02>TheEnd<00><FF>"));
		}

		[Test]
		public void Test_TuplePack_ToRange_With_Prefix()
		{
			Slice prefix = Slice.FromString("ABC");
			(Slice Begin, Slice End) range;

			range = TuPack.ToRange(prefix, STuple.Create("Hello"));
			Assert.That(range.Begin.ToString(), Is.EqualTo("ABC<02>Hello<00><00>"));
			Assert.That(range.End.ToString(), Is.EqualTo("ABC<02>Hello<00><FF>"));

			range = TuPack.ToRange(prefix, STuple.Create("Hello", 123));
			Assert.That(range.Begin.ToString(), Is.EqualTo("ABC<02>Hello<00><15>{<00>"));
			Assert.That(range.End.ToString(), Is.EqualTo("ABC<02>Hello<00><15>{<FF>"));

			range = TuPack.ToRange(prefix, STuple.Create("Hello", 123, true));
			Assert.That(range.Begin.ToString(), Is.EqualTo("ABC<02>Hello<00><15>{'<00>"));
			Assert.That(range.End.ToString(), Is.EqualTo("ABC<02>Hello<00><15>{'<FF>"));

			range = TuPack.ToRange(prefix, STuple.Create("Hello", 123, true, -1234L));
			Assert.That(range.Begin.ToString(), Is.EqualTo("ABC<02>Hello<00><15>{'<12><FB>-<00>"));
			Assert.That(range.End.ToString(), Is.EqualTo("ABC<02>Hello<00><15>{'<12><FB>-<FF>"));

			range = TuPack.ToRange(prefix, STuple.Create("Hello", 123, true, -1234L, "こんにちは世界"));
			Assert.That(range.Begin.ToString(), Is.EqualTo("ABC<02>Hello<00><15>{'<12><FB>-<02><E3><81><93><E3><82><93><E3><81><AB><E3><81><A1><E3><81><AF><E4><B8><96><E7><95><8C><00><00>"));
			Assert.That(range.End.ToString(), Is.EqualTo("ABC<02>Hello<00><15>{'<12><FB>-<02><E3><81><93><E3><82><93><E3><81><AB><E3><81><A1><E3><81><AF><E4><B8><96><E7><95><8C><00><FF>"));

			range = TuPack.ToRange(prefix, STuple.Create("Hello", 123, true, -1234L, "こんにちは世界", Math.PI));
			Assert.That(range.Begin.ToString(), Is.EqualTo("ABC<02>Hello<00><15>{'<12><FB>-<02><E3><81><93><E3><82><93><E3><81><AB><E3><81><A1><E3><81><AF><E4><B8><96><E7><95><8C><00>!<C0><09>!<FB>TD-<18><00>"));
			Assert.That(range.End.ToString(), Is.EqualTo("ABC<02>Hello<00><15>{'<12><FB>-<02><E3><81><93><E3><82><93><E3><81><AB><E3><81><A1><E3><81><AF><E4><B8><96><E7><95><8C><00>!<C0><09>!<FB>TD-<18><FF>"));

			range = TuPack.ToRange(prefix, STuple.Create("Hello", 123, true, -1234L, "こんにちは世界", Math.PI, false));
			Assert.That(range.Begin.ToString(), Is.EqualTo("ABC<02>Hello<00><15>{'<12><FB>-<02><E3><81><93><E3><82><93><E3><81><AB><E3><81><A1><E3><81><AF><E4><B8><96><E7><95><8C><00>!<C0><09>!<FB>TD-<18>&<00>"));
			Assert.That(range.End.ToString(), Is.EqualTo("ABC<02>Hello<00><15>{'<12><FB>-<02><E3><81><93><E3><82><93><E3><81><AB><E3><81><A1><E3><81><AF><E4><B8><96><E7><95><8C><00>!<C0><09>!<FB>TD-<18>&<FF>"));

			range = TuPack.ToRange(prefix, STuple.Create("Hello", 123, true, -1234L, "こんにちは世界", Math.PI, false, "TheEnd"));
			Assert.That(range.Begin.ToString(), Is.EqualTo("ABC<02>Hello<00><15>{'<12><FB>-<02><E3><81><93><E3><82><93><E3><81><AB><E3><81><A1><E3><81><AF><E4><B8><96><E7><95><8C><00>!<C0><09>!<FB>TD-<18>&<02>TheEnd<00><00>"));
			Assert.That(range.End.ToString(), Is.EqualTo("ABC<02>Hello<00><15>{'<12><FB>-<02><E3><81><93><E3><82><93><E3><81><AB><E3><81><A1><E3><81><AF><E4><B8><96><E7><95><8C><00>!<C0><09>!<FB>TD-<18>&<02>TheEnd<00><FF>"));

			// Nil or Empty prefix should not add anything

			range = TuPack.ToRange(Slice.Nil, STuple.Create("Hello", 123));
			Assert.That(range.Begin.ToString(), Is.EqualTo("<02>Hello<00><15>{<00>"));
			Assert.That(range.End.ToString(), Is.EqualTo("<02>Hello<00><15>{<FF>"));

			range = TuPack.ToRange(Slice.Empty, STuple.Create("Hello", 123));
			Assert.That(range.Begin.ToString(), Is.EqualTo("<02>Hello<00><15>{<00>"));
			Assert.That(range.End.ToString(), Is.EqualTo("<02>Hello<00><15>{<FF>"));

		}

		[Test]
		public void Test_TuPack_ValueTuple_Pack()
		{
			Assert.That(
				TuPack.Pack(ValueTuple.Create("hello world")).ToString(),
				Is.EqualTo("<02>hello world<00>")
			);
			Assert.That(
				TuPack.Pack(ValueTuple.Create("hello world", 123)).ToString(),
				Is.EqualTo("<02>hello world<00><15>{")
			);
			Assert.That(
				TuPack.Pack(ValueTuple.Create("hello world", 123, false)).ToString(),
				Is.EqualTo("<02>hello world<00><15>{&")
			);
			Assert.That(
				TuPack.Pack(ValueTuple.Create("hello world", 123, false, new byte[] { 123, 1, 66, 0, 42 })).ToString(),
				Is.EqualTo("<02>hello world<00><15>{&<01>{<01>B<00><FF>*<00>")
			);
			Assert.That(
				TuPack.Pack(ValueTuple.Create("hello world", 123, false, new byte[] { 123, 1, 66, 0, 42 }, Math.PI)).ToString(),
				Is.EqualTo("<02>hello world<00><15>{&<01>{<01>B<00><FF>*<00>!<C0><09>!<FB>TD-<18>")
			);
			Assert.That(
				TuPack.Pack(ValueTuple.Create("hello world", 123, false, new byte[] { 123, 1, 66, 0, 42 }, Math.PI, -1234L)).ToString(),
				Is.EqualTo("<02>hello world<00><15>{&<01>{<01>B<00><FF>*<00>!<C0><09>!<FB>TD-<18><12><FB>-")
			);

			{ // Embedded Tuples
				var packed = TuPack.Pack(("hello", (123, false), "world"));
				Assert.That(
					packed.ToString(),
					Is.EqualTo("<02>hello<00><05><15>{&<00><02>world<00>")
				);
				var t = TuPack.DecodeKey<string, (int, bool), string>(packed);
				Assert.That(t, Is.EqualTo(("hello", (123, false), "world")));
			}

		}

	}

}
