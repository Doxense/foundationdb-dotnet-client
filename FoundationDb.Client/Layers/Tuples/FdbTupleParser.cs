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

namespace FoundationDB.Layers.Tuples
{
	using FoundationDB.Client.Utils;
	using System;
	using System.Text;

	public static class FdbTupleParser
	{
		/// <summary>Writes a null value at the end, and advance the cursor</summary>
		public static void WriteNil(FdbBufferWriter writer)
		{
			writer.WriteByte(FdbTupleTypes.Nil);
		}

		/// <summary>Writes an Int64 at the end, and advance the cursor</summary>
		/// <param name="value">Signed QWORD, 64 bits, High Endian</param>
		public static void WriteInt64(FdbBufferWriter writer, long value)
		{
			if (value <= 255)
			{
				if (value == 0)
				{ // zero
					writer.WriteByte(FdbTupleTypes.IntZero);
					return;
				}

				if (value > 0)
				{ // 1..255: frequent for array index
					writer.EnsureBytes(2);
					writer.UnsafeWriteByte(FdbTupleTypes.IntPos1);
					writer.UnsafeWriteByte((byte)value);
					return;
				}

				if (value > -256)
				{ // -255..-1
					writer.EnsureBytes(2);
					writer.UnsafeWriteByte(FdbTupleTypes.IntNeg1);
					writer.UnsafeWriteByte((byte)(255 + value));
					return;
				}
			}

			WriteInt64Slow(writer, value);
		}

		private static void WriteInt64Slow(FdbBufferWriter writer, long value)
		{
			// we are only called for values <= -256 or >= 256

			// determine the number of bytes needed to encode the absolute value
			int bytes = FdbBufferWriter.NumberOfBytes(value);

			writer.EnsureBytes(bytes + 1);

			var buffer = writer.Buffer;
			int p = writer.Position;

			ulong v;
			if (value > 0)
			{ // simple case
				buffer[p++] = (byte)(FdbTupleTypes.IntBase + bytes);
				v = (ulong)value;
			}
			else
			{ // we will encode the one's complement of the absolute value
				// -1 => 0xFE
				// -256 => 0xFFFE
				// -65536 => 0xFFFFFE
				buffer[p++] = (byte)(FdbTupleTypes.IntBase - bytes);
				v = (ulong)(~(-value));
			}

			if (bytes > 0)
			{
				// head
				--bytes;
				int shift = bytes << 3;

				while (bytes-- > 0)
				{
					buffer[p++] = (byte)(v >> shift);
					shift -= 8;
				}
				// last
				buffer[p++] = (byte)v;
			}
			writer.Position = p;
		}

		/// <summary>Writes an UInt64 at the end, and advance the cursor</summary>
		/// <param name="value">Signed QWORD, 64 bits, High Endian</param>
		public static void WriteUInt64(FdbBufferWriter writer, ulong value)
		{
			if (value <= 255)
			{
				if (value == 0)
				{ // 0
					writer.WriteByte(FdbTupleTypes.IntZero);
				}
				else
				{ // 1..255
					writer.EnsureBytes(2);
					writer.UnsafeWriteByte(FdbTupleTypes.IntPos1);
					writer.UnsafeWriteByte((byte)value);
				}
			}
			else
			{ // >= 256
				WriteUInt64Slow(writer, value);
			}
		}

		private static void WriteUInt64Slow(FdbBufferWriter writer, ulong value)
		{
			// We are only called for values >= 256

			// determine the number of bytes needed to encode the value
			int bytes = FdbBufferWriter.NumberOfBytes(value);

			writer.EnsureBytes(bytes + 1);

			var buffer = writer.Buffer;
			int p = writer.Position;

			// simple case (ulong can only be positive)
			buffer[p++] = (byte)(FdbTupleTypes.IntBase + bytes);

			if (bytes > 0)
			{
				// head
				--bytes;
				int shift = bytes << 3;

				while (bytes-- > 0)
				{
					buffer[p++] = (byte)(value >> shift);
					shift -= 8;
				}
				// last
				buffer[p++] = (byte)value;
			}

			writer.Position = p;
		}

		/// <summary>Writes a binary string</summary>
		public static void WriteBytes(FdbBufferWriter writer, byte[] value)
		{
			if (value == null)
			{
				writer.WriteByte(FdbTupleTypes.Nil);
			}
			else
			{
				WriteNulEscapedBytes(writer, FdbTupleTypes.Bytes, value);
			}
		}

		/// <summary>Writes a string containing only ASCII chars</summary>
		public static void WriteAsciiString(FdbBufferWriter writer, string value)
		{
			if (value == null)
			{
				writer.WriteByte(FdbTupleTypes.Nil);
			}
			else
			{
				WriteNulEscapedBytes(writer, FdbTupleTypes.Bytes, value.Length == 0 ? FdbBufferWriter.Empty : Encoding.Default.GetBytes(value));
			}
		}

		/// <summary>Writes a string encoded in UTF-8</summary>
		public static void WriteString(FdbBufferWriter writer, string value)
		{
			if (value == null)
			{
				writer.WriteByte(FdbTupleTypes.Nil);
			}
			else
			{
				WriteNulEscapedBytes(writer, FdbTupleTypes.Utf8, value.Length == 0 ? FdbBufferWriter.Empty : Encoding.UTF8.GetBytes(value));
			}
		}

		/// <summary>Writes a buffer with all instances of 0 escaped as '00 FF'</summary>
		private static void WriteNulEscapedBytes(FdbBufferWriter writer, byte type, byte[] value)
		{
			int n = value.Length;
			// we need to know if there are any NUL chars (\0) that need escaping...
			// (we will also need to add 1 byte to the buffer size per NUL)
			foreach (byte b in value)
			{
				if (b == 0) ++n;
			}

			writer.EnsureBytes(n + 2);
			var buffer = writer.Buffer;
			int p = writer.Position;
			buffer[p++] = type;
			if (n > 0)
			{
				if (n == value.Length)
				{ // no NULs in the string, can copy all at once
					System.Buffer.BlockCopy(value, 0, buffer, p, n);
					p += n;
				}
				else
				{ // we need to escape all NULs
					foreach (byte b in value)
					{
						buffer[p++] = b;
						if (b == 0) buffer[p++] = 0xFF;
					}
				}
			}
			buffer[p++] = FdbTupleTypes.Nil;
			writer.Position = p;
		}

		/// <summary>Writes a binary string</summary>
		public static void WriteBytes(FdbBufferWriter writer, byte[] value, int offset, int count)
		{
			WriteNulEscapedBytes(writer, FdbTupleTypes.Bytes, value, offset, count);
		}

		/// <summary>Writes a binary string</summary>
		public static void WriteBytes(FdbBufferWriter writer, ArraySegment<byte> value)
		{
			WriteNulEscapedBytes(writer, FdbTupleTypes.Bytes, value.Array, value.Offset, value.Count);
		}

		/// <summary>Writes a buffer with all instances of 0 escaped as '00 FF'</summary>
		private static void WriteNulEscapedBytes(FdbBufferWriter writer, byte type, byte[] value, int offset, int count)
		{
			int n = count;

			// we need to know if there are any NUL chars (\0) that need escaping...
			// (we will also need to add 1 byte to the buffer size per NUL)
			for (int i = offset, end = offset + count; i < end; ++i)
			{
				if (value[i] == 0) ++n;
			}

			writer.EnsureBytes(n + 2);
			var buffer = writer.Buffer;
			int p = writer.Position;
			buffer[p++] = type;
			if (n > 0)
			{
				if (n == value.Length)
				{ // no NULs in the string, can copy all at once
					System.Buffer.BlockCopy(value, 0, buffer, p, n);
					p += n;
				}
				else
				{ // we need to escape all NULs
					for(int i = offset, end = offset + count; i < end; ++i)
					{
						byte b = value[i];
						buffer[p++] = b;
						if (b == 0) buffer[p++] = 0xFF;
					}
				}
			}
			buffer[p++] = FdbTupleTypes.Nil;
			writer.Position = p;
		}

		/// <summary>Writes a GUID encoded as a 16-byte UUID</summary>
		public static void WriteGuid(FdbBufferWriter writer, Guid value)
		{
			writer.EnsureBytes(17);
			writer.UnsafeWriteByte(FdbTupleTypes.Guid);
			unsafe
			{
				byte* ptr = (byte*)&value;
				writer.UnsafeWriteBytes(ptr, 16);
			}
		}
	}
}
