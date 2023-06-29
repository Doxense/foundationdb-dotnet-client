﻿#region BSD License
/* Copyright (c) 2005-2023 Doxense SAS
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

namespace Doxense.Slices.Tests
{
	using System;
	using Doxense.Testing;
	using NUnit.Framework;

	[TestFixture]
	[Category("Core-SDK")]
	public class SliceReaderFacts : DoxenseTest
	{

		[Test]
		public void Test_ToSliceReader()
		{
			{
				var reader = Slice.Empty.ToSliceReader();
				Assert.That(reader.Position, Is.EqualTo(0));
				Assert.That(reader.Buffer, Is.EqualTo(Slice.Empty));
				Assert.That(reader.Remaining, Is.EqualTo(0));
				Assert.That(reader.HasMore, Is.False);
				Assert.That(reader.Head, Is.EqualTo(Slice.Empty));
				Assert.That(reader.Tail, Is.EqualTo(Slice.Empty));
			}
			{
				var reader = Slice.FromString("Hello World").ToSliceReader();
				Assert.That(reader.Position, Is.EqualTo(0));
				Assert.That(reader.Buffer, Is.EqualTo(Slice.FromString("Hello World")));
				Assert.That(reader.Buffer.Offset, Is.EqualTo(0));
				Assert.That(reader.Buffer.Count, Is.EqualTo(11));
				Assert.That(reader.Remaining, Is.EqualTo(11));
				Assert.That(reader.HasMore, Is.True);
				Assert.That(reader.Head, Is.EqualTo(Slice.Empty));
				Assert.That(reader.Tail, Is.EqualTo(Slice.FromString("Hello World")));
			}
			{
				var reader = Slice.FromString("XxXxHello WorldxXxX").Substring(4, 11).ToSliceReader();
				Assert.That(reader.Position, Is.EqualTo(0));
				Assert.That(reader.Buffer, Is.EqualTo(Slice.FromString("Hello World")));
				Assert.That(reader.Buffer.Offset, Is.EqualTo(4));
				Assert.That(reader.Buffer.Count, Is.EqualTo(11));
				Assert.That(reader.Remaining, Is.EqualTo(11));
				Assert.That(reader.HasMore, Is.True);
				Assert.That(reader.Head, Is.EqualTo(Slice.Empty));
				Assert.That(reader.Tail, Is.EqualTo(Slice.FromString("Hello World")));
			}
		}

		[Test]
		public void Test_ReadByte()
		{
			var data = Slice.FromString("ABCD");
			var reader = data.ToSliceReader();
			Assert.That(reader.ReadByte(), Is.EqualTo('A'));
			Assert.That(reader.Position, Is.EqualTo(1));
			Assert.That(reader.ReadByte(), Is.EqualTo('B'));
			Assert.That(reader.Position, Is.EqualTo(2));
			Assert.That(reader.ReadByte(), Is.EqualTo('C'));
			Assert.That(reader.Position, Is.EqualTo(3));
			Assert.That(reader.ReadByte(), Is.EqualTo('D'));
			Assert.That(reader.Position, Is.EqualTo(4));
			Assert.That(() => reader.ReadByte(), Throws.InstanceOf<FormatException>());
			Assert.That(reader.Position, Is.EqualTo(4));
		}

		[Test]
		public void Test_ReadBytes()
		{
			var data = Slice.FromString("ABCDEFGHIJKLMNOPQRSTUVWXYZ");
			var reader = data.ToSliceReader();
			Assert.That(reader.ReadBytes(4), Is.EqualTo(Slice.FromString("ABCD")));
			Assert.That(reader.Position, Is.EqualTo(4));
			Assert.That(reader.ReadBytes(4), Is.EqualTo(Slice.FromString("EFGH")));
			Assert.That(reader.Position, Is.EqualTo(8));
			Assert.That(reader.ReadBytes(1), Is.EqualTo(Slice.FromString("I")));
			Assert.That(reader.Position, Is.EqualTo(9));
			Assert.That(reader.ReadBytes(2), Is.EqualTo(Slice.FromString("JK")));
			Assert.That(reader.Position, Is.EqualTo(11));
			Assert.That(reader.ReadBytes(0), Is.EqualTo(Slice.Empty));
			Assert.That(reader.Position, Is.EqualTo(11));
			Assert.That(reader.ReadBytes(14), Is.EqualTo(Slice.FromString("LMNOPQRSTUVWXY")));
			Assert.That(reader.Position, Is.EqualTo(25));
			Assert.That(() => reader.ReadBytes(2), Throws.InstanceOf<FormatException>());
			Assert.That(reader.Position, Is.EqualTo(25));
			Assert.That(reader.ReadBytes(1), Is.EqualTo(Slice.FromString("Z")));
			Assert.That(reader.Position, Is.EqualTo(26));
			Assert.That(reader.ReadBytes(0), Is.EqualTo(Slice.Empty));
			Assert.That(reader.Position, Is.EqualTo(26));
			Assert.That(() => reader.ReadBytes(1), Throws.InstanceOf<FormatException>());
			Assert.That(reader.Position, Is.EqualTo(26));
			Assert.That(() => reader.ReadBytes(-4), Throws.InstanceOf<FormatException>());
			Assert.That(reader.Position, Is.EqualTo(26));
		}

		[Test]
		public void Test_ReadFixed16()
		{
			var data = Slice.FromString("01234");
			var reader = data.ToSliceReader();
			Assert.That(reader.ReadFixed16(), Is.EqualTo(0x3130));
			Assert.That(reader.Position, Is.EqualTo(2));
			Assert.That(reader.ReadFixed16BE(), Is.EqualTo(0x3233));
			Assert.That(reader.Position, Is.EqualTo(4));
			Assert.That(() => reader.ReadFixed16(), Throws.InstanceOf<FormatException>());
			Assert.That(reader.Position, Is.EqualTo(4));
			Assert.That(reader.Tail, Is.EqualTo(Slice.FromString("4")));
		}

		[Test]
		public void Test_ReadFixed24()
		{
			var data = Slice.FromString("01234567");
			var reader = data.ToSliceReader();
			Assert.That(reader.ReadFixed24(), Is.EqualTo(0x323130));
			Assert.That(reader.Position, Is.EqualTo(3));
			Assert.That(reader.ReadFixed24BE(), Is.EqualTo(0x333435));
			Assert.That(reader.Position, Is.EqualTo(6));
			Assert.That(() => reader.ReadFixed24(), Throws.InstanceOf<FormatException>());
			Assert.That(reader.Position, Is.EqualTo(6));
			Assert.That(reader.Tail, Is.EqualTo(Slice.FromString("67")));
		}

		[Test]
		public void Test_ReadFixed32()
		{
			var data = Slice.FromString("0123456789");
			var reader = data.ToSliceReader();
			Assert.That(reader.ReadFixed32(), Is.EqualTo(0x33323130));
			Assert.That(reader.Position, Is.EqualTo(4));
			Assert.That(reader.ReadFixed32BE(), Is.EqualTo(0x34353637));
			Assert.That(reader.Position, Is.EqualTo(8));
			Assert.That(() => reader.ReadFixed32(), Throws.InstanceOf<FormatException>());
			Assert.That(reader.Position, Is.EqualTo(8));
			Assert.That(reader.Tail, Is.EqualTo(Slice.FromString("89")));
		}

		[Test]
		public void Test_ReadFixed64()
		{
			var data = Slice.FromString("0123456789ABCDEFGH");
			var reader = data.ToSliceReader();
			Assert.That(reader.ReadFixed64(), Is.EqualTo(0x3736353433323130));
			Assert.That(reader.Position, Is.EqualTo(8));
			Assert.That(reader.ReadFixed64BE(), Is.EqualTo(0x3839414243444546));
			Assert.That(reader.Position, Is.EqualTo(16));
			Assert.That(() => reader.ReadFixed64(), Throws.InstanceOf<FormatException>());
			Assert.That(reader.Position, Is.EqualTo(16));
			Assert.That(reader.Tail, Is.EqualTo(Slice.FromString("GH")));
		}

		[Test]
		public void Test_ReadUuid64()
		{
			var data = Slice.FromString("0123456789ABCDEFGH");
			var reader = data.ToSliceReader();
			Assert.That(reader.ReadUuid64().ToString(), Is.EqualTo("30313233-34353637"));
			Assert.That(reader.Position, Is.EqualTo(8));
			Assert.That(reader.ReadUuid64().ToString(), Is.EqualTo("38394142-43444546"));
			Assert.That(reader.Position, Is.EqualTo(16));
			Assert.That(() => reader.ReadUuid64(), Throws.InstanceOf<FormatException>());
			Assert.That(reader.Position, Is.EqualTo(16));
			Assert.That(reader.Tail, Is.EqualTo(Slice.FromString("GH")));
		}

		[Test]
		public void Test_ReadUuid128()
		{
			var data = Slice.FromString("0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZ");
			var reader = data.ToSliceReader();
			Assert.That(reader.ReadUuid128().ToString(), Is.EqualTo("30313233-3435-3637-3839-414243444546"));
			Assert.That(reader.Position, Is.EqualTo(16));
			Assert.That(reader.ReadUuid128().ToString(), Is.EqualTo("4748494a-4b4c-4d4e-4f50-515253545556"));
			Assert.That(reader.Position, Is.EqualTo(32));
			Assert.That(() => reader.ReadUuid128(), Throws.InstanceOf<FormatException>());
			Assert.That(reader.Position, Is.EqualTo(32));
			Assert.That(reader.Tail, Is.EqualTo(Slice.FromString("WXYZ")));
		}

	}
}
