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
	* Neither the name of the <organization> nor the
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

namespace FoundationDb.Client.Utils.Tests
{
	using FoundationDb.Client;
	using FoundationDb.Client.Utils;
	using NUnit.Framework;
	using System;
	using System.Collections.Generic;
	using System.Linq;
	using System.Text;

	[TestFixture]
	public class BufferWriterFacts
	{

		private static string Dump(Slice buffer)
		{
			return String.Join(" ", buffer.Array.Skip(buffer.Offset).Take(buffer.Count).Select(b => b.ToString("X2")));
		}

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

			Assert.That(Dump(writer.ToSlice()), Is.EqualTo(expectedResult), "Value {0} ({1}) was not properly packed", value == null ? "<null>" : value is string ? Clean(value as string) : value.ToString(), (value == null ? "null" : value.GetType().Name));
		}

		[Test]
		public void Test_WriteInt64()
		{
			Action<FdbBufferWriter, long> test = (writer, value) => writer.WriteInt64(value);

			PerformWriterTest(test, 0L, "14");

			PerformWriterTest(test, 1L, "15 01");
			PerformWriterTest(test, 2L, "15 02");
			PerformWriterTest(test, 123L, "15 7B");
			PerformWriterTest(test, 255L, "15 FF");
			PerformWriterTest(test, 256L, "16 01 00");
			PerformWriterTest(test, 257L, "16 01 01");
			PerformWriterTest(test, 65535L, "16 FF FF");
			PerformWriterTest(test, 65536L, "17 01 00 00");
			PerformWriterTest(test, 65537L, "17 01 00 01");

			PerformWriterTest(test, -1L, "13 FE");
			PerformWriterTest(test, -123L, "13 84");
			PerformWriterTest(test, -255L, "13 00");
			PerformWriterTest(test, -256L, "12 FF FE");
			PerformWriterTest(test, -65535L, "12 00 00");
			PerformWriterTest(test, -65536L, "11 FF FF FE");

			PerformWriterTest(test, (1L << 24) - 1, "17 FF FF FF");
			PerformWriterTest(test, 1L << 24, "18 00 00 00 01");

			PerformWriterTest(test, (1L << 32) - 1, "18 FF FF FF FF");
			PerformWriterTest(test, (1L << 32), "19 00 00 00 00 01");

			PerformWriterTest(test, long.MaxValue, "1C FF FF FF FF FF FF FF 7F");
			PerformWriterTest(test, long.MinValue, "0C 00 00 00 00 00 00 00 80");
			PerformWriterTest(test, long.MaxValue - 1, "1C FE FF FF FF FF FF FF 7F");
			PerformWriterTest(test, long.MinValue + 1, "0C 01 00 00 00 00 00 00 80");

		}

		[Test]
		public void Test_WriteInt64_Ordered()
		{
			var list = new List<KeyValuePair<long, Slice>>();

			Action<long> test = (x) =>
			{
				var writer = new FdbBufferWriter();
				writer.WriteInt64(x);
				var res = new KeyValuePair<long, Slice>(x, writer.ToSlice());
				list.Add(res);
				Console.WriteLine("{0,20} : {0:x16} {1}", res.Key, res.Value.ToString());
			};

			// We can't test 2^64 values, be we are interested at what happens around powers of two (were size can change)

			// negatives
			for (int i = 63; i >= 3; i--)
			{
				long x = -(1L << i);

				if (i < 63)
				{
					test(x - 2);
					test(x - 1);
				}
				test(x + 0);
				test(x + 1);
				test(x + 2);
			}

			test(-2);
			test(0);
			test(+1);
			test(+2);

			// positives
			for (int i = 3; i <= 63; i++)
			{
				long x = (1L << i);

				test(x - 2);
				test(x - 1);
				if (i < 63)
				{
					test(x + 0);
					test(x + 1);
					test(x + 2);
				}
			}

			KeyValuePair<long, Slice> previous = list[0];
			for (int i = 1; i < list.Count; i++)
			{
				KeyValuePair<long, Slice> current = list[i];

				Assert.That(current.Key, Is.GreaterThan(previous.Key));
				Assert.That(current.Value, Is.GreaterThan(previous.Value), "Expect {0} > {1}", current.Key, previous.Key);

				previous = current;
			}
		}

		[Test]
		public void Test_WriteUInt64()
		{
			Action<FdbBufferWriter, ulong> test = (writer, value) => writer.WriteUInt64(value);

			PerformWriterTest(test, 0UL, "14");

			PerformWriterTest(test, 1UL, "15 01");
			PerformWriterTest(test, 123UL, "15 7B");
			PerformWriterTest(test, 255UL, "15 FF");
			PerformWriterTest(test, 256UL, "16 01 00");
			PerformWriterTest(test, 257UL, "16 01 01");
			PerformWriterTest(test, 65535UL, "16 FF FF");
			PerformWriterTest(test, 65536UL, "17 01 00 00");
			PerformWriterTest(test, 65537UL, "17 01 00 01");

			PerformWriterTest(test, (1UL << 24) - 1, "17 FF FF FF");
			PerformWriterTest(test, 1UL << 24, "18 01 00 00 00");

			PerformWriterTest(test, (1UL << 32) - 1, "18 FF FF FF FF");
			PerformWriterTest(test, (1UL << 32), "19 01 00 00 00 00");

			PerformWriterTest(test, ulong.MaxValue, "1C FF FF FF FF FF FF FF FF");
			PerformWriterTest(test, ulong.MaxValue-1, "1C FF FF FF FF FF FF FF FE");

		}

		[Test]
		public void Test_WriteUInt64_Ordered()
		{
			var list = new List<KeyValuePair<ulong, Slice>>();

			Action<ulong> test = (x) =>
			{
				var writer = new FdbBufferWriter();
				writer.WriteUInt64(x);
				var res = new KeyValuePair<ulong, Slice>(x, writer.ToSlice());
				list.Add(res);
#if DEBUG
				Console.WriteLine("{0,20} : {0:x16} {1}", res.Key, res.Value.ToString());
#endif
			};

			// We can't test 2^64 values, be we are interested at what happens around powers of two (were size can change)

			test(0);
			test(1);

			// positives
			for (int i = 3; i <= 63; i++)
			{
				ulong x = (1UL << i);

				test(x - 2);
				test(x - 1);
				test(x + 0);
				test(x + 1);
				test(x + 2);
			}
			test(ulong.MaxValue - 2);
			test(ulong.MaxValue - 1);
			test(ulong.MaxValue);

			KeyValuePair<ulong, Slice> previous = list[0];
			for (int i = 1; i < list.Count; i++)
			{
				KeyValuePair<ulong, Slice> current = list[i];

				Assert.That(current.Key, Is.GreaterThan(previous.Key));
				Assert.That(current.Value, Is.GreaterThan(previous.Value), "Expect {0} > {1}", current.Key, previous.Key);

				previous = current;
			}
		}

		[Test]
		public void Test_WriteAsciiString()
		{
			Action<FdbBufferWriter, string> test = (writer, value) => writer.WriteAsciiString(value);

			PerformWriterTest(test, null, "00");
			PerformWriterTest(test, String.Empty, "01 00");
			PerformWriterTest(test, "A", "01 41 00");
			PerformWriterTest(test, "ABC", "01 41 42 43 00");

			// Must escape '\0' contained in the string as '\x00\xFF'
			PerformWriterTest(test, "\0", "01 00 FF 00");
			PerformWriterTest(test, "A\0", "01 41 00 FF 00");
			PerformWriterTest(test, "\0A", "01 00 FF 41 00");
			PerformWriterTest(test, "A\0\0A", "01 41 00 FF 00 FF 41 00");
			PerformWriterTest(test, "A\0B\0\xFF", "01 41 00 FF 42 00 FF FF 00");
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

	}
}
