using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using NUnit.Framework;
using FoundationDb.Client;
using System.Threading.Tasks;
using System.Threading;
using FoundationDb.Client.Utils;

namespace FoundationDb.Tests
{

	[TestFixture]
	public class BufferWriterFacts
	{

		private static string Dump(ArraySegment<byte> buffer)
		{
			return String.Join(" ", buffer.Array.Skip(buffer.Offset).Take(buffer.Count).Select(b => b.ToString("X2")));
		}

		private static void PerformWriterTest<T>(Action<FdbBufferWriter, T> action, T value, string expectedResult, string message = null)
		{
			var writer = new FdbBufferWriter();
			action(writer, value);

			Assert.That(Dump(writer.ToArraySegment()), Is.EqualTo(expectedResult), "Value {0} ({1}) was not properly packed", value, (value == null ? "null" : value.GetType().Name));
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
			PerformWriterTest(test, 256L, "16 00 01");
			PerformWriterTest(test, 257L, "16 01 01");
			PerformWriterTest(test, 65535L, "16 FF FF");
			PerformWriterTest(test, 65536L, "17 00 00 01");
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
		public void Test_WriteUInt64()
		{
			Action<FdbBufferWriter, ulong> test = (writer, value) => writer.WriteUInt64(value);

			PerformWriterTest(test, 0UL, "14");

			PerformWriterTest(test, 1UL, "15 01");
			PerformWriterTest(test, 123UL, "15 7B");
			PerformWriterTest(test, 255UL, "15 FF");
			PerformWriterTest(test, 256UL, "16 00 01");
			PerformWriterTest(test, 257UL, "16 01 01");
			PerformWriterTest(test, 65535UL, "16 FF FF");
			PerformWriterTest(test, 65536UL, "17 00 00 01");
			PerformWriterTest(test, 65537UL, "17 01 00 01");

			PerformWriterTest(test, (1UL << 24) - 1, "17 FF FF FF");
			PerformWriterTest(test, 1UL << 24, "18 00 00 00 01");

			PerformWriterTest(test, (1UL << 32) - 1, "18 FF FF FF FF");
			PerformWriterTest(test, (1UL << 32), "19 00 00 00 00 01");

			PerformWriterTest(test, ulong.MaxValue, "1C FF FF FF FF FF FF FF FF");
			PerformWriterTest(test, ulong.MaxValue-1, "1C FE FF FF FF FF FF FF FF");

		}
	}
}
