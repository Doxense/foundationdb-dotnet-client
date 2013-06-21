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

namespace FoundationDB.Client.Utils.Tests
{
	using FoundationDB.Client;
	using FoundationDB.Client.Utils;
	using NUnit.Framework;
	using System;
	using System.Collections.Generic;
	using System.Linq;
	using System.Text;

	[TestFixture]
	public class BufferWriterFacts
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

		private static void PerformWriterTest<T>(Action<FdbBufferWriter, T> action, T value, string expectedResult, string message = null)
		{
			var writer = new FdbBufferWriter();
			action(writer, value);

			Assert.That(writer.ToSlice().ToHexaString(' '), Is.EqualTo(expectedResult), "Value {0} ({1}) was not properly packed", value == null ? "<null>" : value is string ? Clean(value as string) : value.ToString(), (value == null ? "null" : value.GetType().Name));
		}

		[Test]
		public void Test_WriteBytes()
		{
			Action<FdbBufferWriter, byte[]> test = (writer, value) => writer.WriteBytes(value);

			PerformWriterTest(test, null, "");
			PerformWriterTest(test, new byte[0], "");
			PerformWriterTest(test, new byte[] { 66 }, "42");
			PerformWriterTest(test, new byte[] { 65, 66, 67 }, "41 42 43");
		}

		[Test]
		public void Test_WriteByte()
		{
			Action<FdbBufferWriter, byte> test = (writer, value) => writer.WriteByte(value);

			PerformWriterTest(test, default(byte), "00");
			PerformWriterTest(test, (byte)1, "01");
			PerformWriterTest(test, (byte)42, "2A");
			PerformWriterTest(test, (byte)255, "FF");
		}

		[Test]
		public void Test_WriteVarint32()
		{
			Action<FdbBufferWriter, uint> test = (writer, value) => writer.WriteVarint32(value);

			PerformWriterTest(test, 0U, "00");
			PerformWriterTest(test, 1U, "01");
			PerformWriterTest(test, 127U, "7F");
			PerformWriterTest(test, 128U, "80 01");
			PerformWriterTest(test, 255U, "FF 01");
			PerformWriterTest(test, 256U, "80 02");
		}

		[Test]
		public void Test_WriteVarint64()
		{
			Action<FdbBufferWriter, ulong> test = (writer, value) => writer.WriteVarint64(value);

			PerformWriterTest(test, 0UL, "00");
			PerformWriterTest(test, 1UL, "01");
			PerformWriterTest(test, 127UL, "7F");
			PerformWriterTest(test, 128UL, "80 01");
			PerformWriterTest(test, 255UL, "FF 01");
			PerformWriterTest(test, 256UL, "80 02");
		}

	}
}
