#region BSD Licence
/* Copyright (c) 2013-2015, Doxense SAS
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
	using FoundationDB.Client;
	using FoundationDB.Client.Utils;
	using JetBrains.Annotations;
	using System;
	using System.Text;

	/// <summary>Helper class that contains low-level encoders for the tuple binary format</summary>
	public static class FdbTupleParser
	{
		#region Serialization...

		/// <summary>Writes a null value at the end, and advance the cursor</summary>
		public static void WriteNil(ref TupleWriter writer)
		{
			if (writer.Depth == 0)
			{ // at the top level, NILs are escaped as <00>
				writer.Output.WriteByte(FdbTupleTypes.Nil);
			}
			else
			{ // inside a tuple, NILs are escaped as <00><FF>
				writer.Output.WriteByte2(FdbTupleTypes.Nil, 0xFF);
			}
		}

		public static void WriteBool(ref TupleWriter writer, bool value)
		{
			// To be compatible with other bindings, we will encode False as the number 0, and True as the number 1
			if (value)
			{ // true => 15 01
				writer.Output.WriteByte2(FdbTupleTypes.IntPos1, 1);
			}
			else
			{ // false => 14
				writer.Output.WriteByte(FdbTupleTypes.IntZero);
			}
		}

		/// <summary>Writes an UInt8 at the end, and advance the cursor</summary>
		/// <param name="writer">Target buffer</param>
		/// <param name="value">Unsigned BYTE, 32 bits</param>
		public static void WriteByte(ref TupleWriter writer, byte value)
		{
			if (value == 0)
			{ // zero
				writer.Output.WriteByte(FdbTupleTypes.IntZero);
			}
			else
			{ // 1..255: frequent for array index
				writer.Output.WriteByte2(FdbTupleTypes.IntPos1, value);
			}
		}

		/// <summary>Writes an Int32 at the end, and advance the cursor</summary>
		/// <param name="writer">Target buffer</param>
		/// <param name="value">Signed DWORD, 32 bits, High Endian</param>
		public static void WriteInt32(ref TupleWriter writer, int value)
		{
			if (value <= 255)
			{
				if (value == 0)
				{ // zero
					writer.Output.WriteByte(FdbTupleTypes.IntZero);
					return;
				}

				if (value > 0)
				{ // 1..255: frequent for array index
					writer.Output.WriteByte2(FdbTupleTypes.IntPos1, (byte)value);
					return;
				}

				if (value > -256)
				{ // -255..-1
					writer.Output.WriteByte2(FdbTupleTypes.IntNeg1, (byte)(255 + value));
					return;
				}
			}

			WriteInt64Slow(ref writer, value);
		}

		/// <summary>Writes an Int64 at the end, and advance the cursor</summary>
		/// <param name="writer">Target buffer</param>
		/// <param name="value">Signed QWORD, 64 bits, High Endian</param>
		public static void WriteInt64(ref TupleWriter writer, long value)
		{
			if (value <= 255)
			{
				if (value == 0)
				{ // zero
					writer.Output.WriteByte(FdbTupleTypes.IntZero);
					return;
				}

				if (value > 0)
				{ // 1..255: frequent for array index
					writer.Output.WriteByte2(FdbTupleTypes.IntPos1, (byte)value);
					return;
				}

				if (value > -256)
				{ // -255..-1
					writer.Output.WriteByte2(FdbTupleTypes.IntNeg1, (byte)(255 + value));
					return;
				}
			}

			WriteInt64Slow(ref writer, value);
		}

		private static void WriteInt64Slow(ref TupleWriter writer, long value)
		{
			// we are only called for values <= -256 or >= 256

			// determine the number of bytes needed to encode the absolute value
			int bytes = NumberOfBytes(value);

			writer.Output.EnsureBytes(bytes + 1);

			var buffer = writer.Output.Buffer;
			int p = writer.Output.Position;

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
			writer.Output.Position = p;
		}

		/// <summary>Writes an UInt32 at the end, and advance the cursor</summary>
		/// <param name="writer">Target buffer</param>
		/// <param name="value">Signed DWORD, 32 bits, High Endian</param>
		public static void WriteUInt32(ref TupleWriter writer, uint value)
		{
			if (value <= 255)
			{
				if (value == 0)
				{ // 0
					writer.Output.WriteByte(FdbTupleTypes.IntZero);
				}
				else
				{ // 1..255
					writer.Output.WriteByte2(FdbTupleTypes.IntPos1, (byte)value);
				}
			}
			else
			{ // >= 256
				WriteUInt64Slow(ref writer, value);
			}
		}

		/// <summary>Writes an UInt64 at the end, and advance the cursor</summary>
		/// <param name="writer">Target buffer</param>
		/// <param name="value">Signed QWORD, 64 bits, High Endian</param>
		public static void WriteUInt64(ref TupleWriter writer, ulong value)
		{
			if (value <= 255)
			{
				if (value == 0)
				{ // 0
					writer.Output.WriteByte(FdbTupleTypes.IntZero);
				}
				else
				{ // 1..255
					writer.Output.WriteByte2(FdbTupleTypes.IntPos1, (byte)value);
				}
			}
			else
			{ // >= 256
				WriteUInt64Slow(ref writer, value);
			}
		}

		private static void WriteUInt64Slow(ref TupleWriter writer, ulong value)
		{
			// We are only called for values >= 256

			// determine the number of bytes needed to encode the value
			int bytes = NumberOfBytes(value);

			writer.Output.EnsureBytes(bytes + 1);

			var buffer = writer.Output.Buffer;
			int p = writer.Output.Position;

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

			writer.Output.Position = p;
		}

		/// <summary>Writes an Single at the end, and advance the cursor</summary>
		/// <param name="writer">Target buffer</param>
		/// <param name="value">IEEE Floating point, 32 bits, High Endian</param>
		public static void WriteSingle(ref TupleWriter writer, float value)
		{
			// The double is converted to its Big-Endian IEEE binary representation
			// - If the sign bit is set, flip all the bits
			// - If the sign bit is not set, just flip the sign bit
			// This ensures that all negative numbers have their first byte < 0x80, and all positive numbers have their first byte >= 0x80

			// Special case for NaN: All variants are normalized to float.NaN !
			if (float.IsNaN(value)) value = float.NaN;

			// note: there is no BitConverter.SingleToInt32Bits(...), so we have to do it ourselves...
			uint bits;
			unsafe { bits = *((uint*)&value); }

			if ((bits & 0x80000000U) != 0)
			{ // negative
				bits = ~bits;
			}
			else
			{ // postive
				bits |= 0x80000000U;
			}
			writer.Output.EnsureBytes(5);
			var buffer = writer.Output.Buffer;
			int p = writer.Output.Position;
			buffer[p + 0] = FdbTupleTypes.Single;
			buffer[p + 1] = (byte)(bits >> 24);
			buffer[p + 2] = (byte)(bits >> 16);
			buffer[p + 3] = (byte)(bits >> 8);
			buffer[p + 4] = (byte)(bits);
			writer.Output.Position = p + 5;
		}

		/// <summary>Writes an Double at the end, and advance the cursor</summary>
		/// <param name="writer">Target buffer</param>
		/// <param name="value">IEEE Floating point, 64 bits, High Endian</param>
		public static void WriteDouble(ref TupleWriter writer, double value)
		{
			// The double is converted to its Big-Endian IEEE binary representation
			// - If the sign bit is set, flip all the bits
			// - If the sign bit is not set, just flip the sign bit
			// This ensures that all negative numbers have their first byte < 0x80, and all positive numbers have their first byte >= 0x80

			// Special case for NaN: All variants are normalized to float.NaN !
			if (double.IsNaN(value)) value = double.NaN;

			// note: we could use BitConverter.DoubleToInt64Bits(...), but it does the same thing, and also it does not exist for floats...
			ulong bits;
			unsafe { bits = *((ulong*)&value); }

			if ((bits & 0x8000000000000000UL) != 0)
			{ // negative
				bits = ~bits;
			}
			else
			{ // postive
				bits |= 0x8000000000000000UL;
			}
			writer.Output.EnsureBytes(9);
			var buffer = writer.Output.Buffer;
			int p = writer.Output.Position;
			buffer[p] = FdbTupleTypes.Double;
			buffer[p + 1] = (byte)(bits >> 56);
			buffer[p + 2] = (byte)(bits >> 48);
			buffer[p + 3] = (byte)(bits >> 40);
			buffer[p + 4] = (byte)(bits >> 32);
			buffer[p + 5] = (byte)(bits >> 24);
			buffer[p + 6] = (byte)(bits >> 16);
			buffer[p + 7] = (byte)(bits >> 8);
			buffer[p + 8] = (byte)(bits);
			writer.Output.Position = p + 9;
		}

		/// <summary>Writes a binary string</summary>
		public static void WriteBytes(ref TupleWriter writer, byte[] value)
		{
			if (value == null)
			{
				WriteNil(ref writer);
			}
			else
			{
				WriteNulEscapedBytes(ref writer, FdbTupleTypes.Bytes, value);
			}
		}

		/// <summary>Writes a string encoded in UTF-8</summary>
		public static unsafe void WriteString(ref TupleWriter writer, string value)
		{
			if (value == null)
			{ // "00"
				WriteNil(ref writer);
			}
			else if (value.Length == 0)
			{ // "02 00"
				writer.Output.WriteByte2(FdbTupleTypes.Utf8, 0x00);
			}
			else
			{
				fixed(char* chars = value)
				{
					if (!TryWriteUnescapedUtf8String(ref writer, chars, value.Length))
					{ // the string contains \0 chars, we need to do it the hard way
						WriteNulEscapedBytes(ref writer, FdbTupleTypes.Utf8, Encoding.UTF8.GetBytes(value));
					}
				}
			}
		}

		/// <summary>Writes a char array encoded in UTF-8</summary>
		internal static unsafe void WriteChars(ref TupleWriter writer, char[] value, int offset, int count)
		{
			Contract.Requires(offset >= 0 && count >= 0);

			if (count == 0)
			{
				if (value == null)
				{ // "00"
					WriteNil(ref writer);
				}
				else
				{ // "02 00"
					writer.Output.WriteByte2(FdbTupleTypes.Utf8, 0x00);
				}
			}
			else
			{
				fixed (char* chars = value)
				{
					if (!TryWriteUnescapedUtf8String(ref writer, chars + offset, count))
					{ // the string contains \0 chars, we need to do it the hard way
						WriteNulEscapedBytes(ref writer, FdbTupleTypes.Utf8, Encoding.UTF8.GetBytes(value, 0, count));
					}
				}
			}
		}

		private static unsafe void WriteUnescapedAsciiChars(ref TupleWriter writer, char* chars, int count)
		{
			Contract.Requires(chars != null && count >= 0);

			// copy and convert an ASCII string directly into the destination buffer

			writer.Output.EnsureBytes(2 + count);
			int pos = writer.Output.Position;
			char* end = chars + count;
			fixed (byte* buffer = writer.Output.Buffer)
			{
				buffer[pos++] = FdbTupleTypes.Utf8;
				//OPTIMIZE: copy 2 or 4 chars at once, unroll loop?
				while(chars < end)
				{
					buffer[pos++] = (byte)(*chars++);
				}
				buffer[pos] = 0x00;
				writer.Output.Position = pos + 1;
			}
		}

		private static unsafe bool TryWriteUnescapedUtf8String(ref TupleWriter writer, char* chars, int count)
		{
			Contract.Requires(chars != null && count >= 0);

			// Several observations:
			// * Most strings will be keywords or ASCII-only with no zeroes. These can be copied directly to the buffer
			// * We will only attempt to optimze strings that don't have any 00 to escape to 00 FF. For these, we will fallback to converting to byte[] then escaping.
			// * Since .NET's strings are UTF-16, the max possible UNICODE value to encode is 0xFFFF, which takes 3 bytes in UTF-8 (EF BF BF)
			// * Most western europe languages have only a few non-ASCII chars here and there, and most of them will only use 2 bytes (ex: 'é' => 'C3 A9')
			// * More complex scripts with dedicated symbol pages (kanjis, arabic, ....) will take 2 or 3 bytes for each charecter.

			// We will first do a pass to check for the presence of 00 and non-ASCII chars
			// => if we find at least on 00, we fallback to escaping the result of Encoding.UTF8.GetBytes()
			// => if we find only ASCII (1..127) chars, we have an optimized path that will truncate the chars to bytes
			// => if not, we will use an UTF8Encoder to convert the string to UTF-8, in chunks, using a small buffer allocated on the stack

			#region First pass: look for \0 and non-ASCII chars

			// fastest way to check for non-ASCII, is to OR all the chars together, and look at bits 7 to 15. If they are not all zero, there is at least ONE non-ASCII char.
			// also, we abort as soon as we find a \0

			char* ptr = chars;
			char* end = chars + count;
			char mask = '\0', c;
			while (ptr < end && (c = *ptr) != '\0') { mask |= c; ++ptr; }

			if (ptr < end) return false; // there is at least one \0 in the string

			// bit 7-15 all unset means the string is pure ASCII
			if ((mask >> 7) == 0)
			{ // => directly dump the chars to the buffer
				WriteUnescapedAsciiChars(ref writer, chars, count);
				return true;
			}

			#endregion

			#region Second pass: encode the string to UTF-8, in chunks

			// Here we know that there is at least one unicode char, and that there are no \0
			// We will tterate through the string, filling as much of the buffer as possible

			bool done;
			int remaining = count;
			ptr = chars;

			// We need at most 3 * CHUNK_SIZE to encode the chunk
			// > For small strings, we will allocated exactly string.Length * 3 bytes, and will be done in one chunk
			// > For larger strings, we will call encoder.Convert(...) until it says it is done.
			const int CHUNK_SIZE = 1024;
			int bufLen = Encoding.UTF8.GetMaxByteCount(Math.Min(count, CHUNK_SIZE));
			byte* buf = stackalloc byte[bufLen];

			// We can not really predict the final size of the encoded string, but:
			// * Western languages have a few chars that usually need 2 bytes. If we pre-allocate 50% more bytes, it should fit most of the time, without too much waste
			// * Eastern langauges will have all chars encoded to 3 bytes. If we also pre-allocated 50% more, we should only need one resize of the buffer (150% x 2 = 300%), which is acceptable
			writer.Output.EnsureBytes(checked(2 + count + (count >> 1))); // preallocate 150% of the string + 2 bytes
			writer.Output.UnsafeWriteByte(FdbTupleTypes.Utf8);

			var encoder = Encoding.UTF8.GetEncoder();
			// note: encoder.Convert() tries to fill up the buffer as much as possible with complete chars, and will set 'done' to true when all chars have been converted.
			do
			{
				int charsUsed, bytesUsed;
				encoder.Convert(ptr, remaining, buf, bufLen, true, out charsUsed, out bytesUsed, out done);
				if (bytesUsed > 0)
				{
					writer.Output.WriteBytes(buf, bytesUsed);
				}
				remaining -= charsUsed;
				ptr += charsUsed;
			}
			while (!done);
			Contract.Assert(remaining == 0 && ptr == end);

			// close the string
			writer.Output.WriteByte(0x00);

			#endregion

			return true;
		}

		/// <summary>Writes a char encoded in UTF-8</summary>
		public static void WriteChar(ref TupleWriter writer, char value)
		{
			if (value == 0)
			{ // NUL => "00 0F"
				// note: \0 is the only unicode character that will produce a zero byte when converted in UTF-8
				writer.Output.WriteByte4(FdbTupleTypes.Utf8, 0x00, 0xFF, 0x00);
			}
			else if (value < 0x80)
			{ // 0x00..0x7F => 0xxxxxxx
				writer.Output.WriteByte3(FdbTupleTypes.Utf8, (byte)value, 0x00);
			}
			else if (value <  0x800)
			{ // 0x80..0x7FF => 110xxxxx 10xxxxxx => two bytes
				writer.Output.WriteByte4(FdbTupleTypes.Utf8, (byte)(0xC0 | (value >> 6)), (byte)(0x80 | (value & 0x3F)), 0x00);
			}
			else
			{ // 0x800..0xFFFF => 11110xxx 10xxxxxx 10xxxxxx 10xxxxxx
				// note: System.Char is 16 bits, and thus cannot represent UNICODE chars above 0xFFFF.
				// => This means that a System.Char will never take more than 3 bytes in UTF-8 !
				var tmp = Encoding.UTF8.GetBytes(new string(value, 1));
				writer.Output.EnsureBytes(tmp.Length + 2);
				writer.Output.UnsafeWriteByte(FdbTupleTypes.Utf8);
				writer.Output.UnsafeWriteBytes(tmp, 0, tmp.Length);
				writer.Output.UnsafeWriteByte(0x00);
			}
		}

		/// <summary>Writes a binary string</summary>
		public static void WriteBytes(ref TupleWriter writer, [NotNull] byte[] value, int offset, int count)
		{
			WriteNulEscapedBytes(ref writer, FdbTupleTypes.Bytes, value, offset, count);
		}

		/// <summary>Writes a binary string</summary>
		public static void WriteBytes(ref TupleWriter writer, ArraySegment<byte> value)
		{
			WriteNulEscapedBytes(ref writer, FdbTupleTypes.Bytes, value.Array, value.Offset, value.Count);
		}

		/// <summary>Writes a buffer with all instances of 0 escaped as '00 FF'</summary>
		internal static void WriteNulEscapedBytes(ref TupleWriter writer, byte type, [NotNull] byte[] value, int offset, int count)
		{
			int n = count;

			// we need to know if there are any NUL chars (\0) that need escaping...
			// (we will also need to add 1 byte to the buffer size per NUL)
			for (int i = offset, end = offset + count; i < end; ++i)
			{
				if (value[i] == 0) ++n;
			}

			writer.Output.EnsureBytes(n + 2);
			var buffer = writer.Output.Buffer;
			int p = writer.Output.Position;
			buffer[p++] = type;
			if (n > 0)
			{
				if (n == count)
				{ // no NULs in the string, can copy all at once
					SliceHelpers.CopyBytesUnsafe(buffer, p, value, offset, n);
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
			buffer[p] = 0x00;
			writer.Output.Position = p + 1;
		}

		/// <summary>Writes a buffer with all instances of 0 escaped as '00 FF'</summary>
		private static void WriteNulEscapedBytes(ref TupleWriter writer, byte type, [NotNull] byte[] value)
		{
			int n = value.Length;
			// we need to know if there are any NUL chars (\0) that need escaping...
			// (we will also need to add 1 byte to the buffer size per NUL)
			foreach (byte b in value)
			{
				if (b == 0) ++n;
			}

			writer.Output.EnsureBytes(n + 2);
			var buffer = writer.Output.Buffer;
			int p = writer.Output.Position;
			buffer[p++] = type;
			if (n > 0)
			{
				if (n == value.Length)
				{ // no NULs in the string, can copy all at once
					SliceHelpers.CopyBytesUnsafe(buffer, p, value, 0, n);
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
			buffer[p++] = 0x00;
			writer.Output.Position = p;
		}

		/// <summary>Writes a RFC 4122 encoded 16-byte Microsoft GUID</summary>
		public static void WriteGuid(ref TupleWriter writer, Guid value)
		{
			writer.Output.EnsureBytes(17);
			writer.Output.UnsafeWriteByte(FdbTupleTypes.Uuid128);
			unsafe
			{
				// UUIDs are stored using the RFC 4122 standard, so we need to swap some parts of the System.Guid

				byte* ptr = stackalloc byte[16];
				Uuid128.Write(value, ptr);
				writer.Output.UnsafeWriteBytes(ptr, 16);
			}
		}

		/// <summary>Writes a RFC 4122 encoded 128-bit UUID</summary>
		public static void WriteUuid128(ref TupleWriter writer, Uuid128 value)
		{
			writer.Output.EnsureBytes(17);
			writer.Output.UnsafeWriteByte(FdbTupleTypes.Uuid128);
			unsafe
			{
				byte* ptr = stackalloc byte[16];
				value.WriteTo(ptr);
				writer.Output.UnsafeWriteBytes(ptr, 16);
			}
		}

		/// <summary>Writes a 64-bit UUID</summary>
		public static void WriteUuid64(ref TupleWriter writer, Uuid64 value)
		{
			writer.Output.EnsureBytes(9);
			writer.Output.UnsafeWriteByte(FdbTupleTypes.Uuid64);
			unsafe
			{
				byte* ptr = stackalloc byte[8];
				value.WriteTo(ptr);
				writer.Output.UnsafeWriteBytes(ptr, 8);
			}
		}

		/// <summary>Mark the start of a new embedded tuple</summary>
		public static void BeginTuple(ref TupleWriter writer)
		{
			writer.Depth++;
			writer.Output.WriteByte(FdbTupleTypes.TupleStart);
		}

		/// <summary>Mark the end of an embedded tuple</summary>
		public static void EndTuple(ref TupleWriter writer)
		{
			writer.Output.WriteByte(0x00);
			writer.Depth--;
		}

		#endregion

		#region Deserialization...

		/// <summary>Parse a tuple segment containing a signed 64-bit integer</summary>
		/// <remarks>This method should only be used by custom decoders.</remarks>
		public static long ParseInt64(int type, Slice slice)
		{
			int bytes = type - FdbTupleTypes.IntBase;
			if (bytes == 0) return 0L;

			bool neg = false;
			if (bytes < 0)
			{
				bytes = -bytes;
				neg = true;
			}

			if (bytes > 8) throw new FormatException("Invalid size for tuple integer");
			long value = (long)slice.ReadUInt64BE(1, bytes);

			if (neg)
			{ // the value is encoded as the one's complement of the absolute value
				value = (-(~value));
				if (bytes < 8) value |= (-1L << (bytes << 3));
				return value;
			}

			return value;
		}

		internal static ArraySegment<byte> UnescapeByteString([NotNull] byte[] buffer, int offset, int count)
		{
			Contract.Requires(buffer != null && offset >= 0 && count >= 0);

			// check for nulls
			int p = offset;
			int end = offset + count;

			while (p < end)
			{
				if (buffer[p] == 0)
				{ // found a 0, switch to slow path
					return UnescapeByteStringSlow(buffer, offset, count, p - offset);
				}
				++p;
			}
			// buffer is clean, we can return it as-is
			return new ArraySegment<byte>(buffer, offset, count);
		}

		internal static ArraySegment<byte> UnescapeByteStringSlow([NotNull] byte[] buffer, int offset, int count, int offsetOfFirstZero = 0)
		{
			Contract.Requires(buffer != null && offset >= 0 && count >= 0);

			var tmp = new byte[count];

			int p = offset;
			int end = offset + count;
			int i = 0;
			if (offsetOfFirstZero > 0)
			{
				SliceHelpers.CopyBytesUnsafe(tmp, 0, buffer, offset, offsetOfFirstZero);
				p += offsetOfFirstZero;
				i = offsetOfFirstZero;
			}

			while (p < end)
			{
				byte b = buffer[p++];
				if (b == 0)
				{ // skip next FF
					//TODO: check that next byte really is 0xFF
					++p;
				}
				tmp[i++] = b;
			}

			return new ArraySegment<byte>(tmp, 0, i);
		}

		/// <summary>Parse a tuple segment containing a byte array</summary>
		public static Slice ParseBytes(Slice slice)
		{
			Contract.Requires(slice.HasValue && slice[0] == FdbTupleTypes.Bytes && slice[-1] == 0);
			if (slice.Count <= 2) return Slice.Empty;

			var decoded = UnescapeByteString(slice.Array, slice.Offset + 1, slice.Count - 2);

			return new Slice(decoded.Array, decoded.Offset, decoded.Count);
		}

		/// <summary>Parse a tuple segment containing an ASCII string stored as a byte array</summary>
		public static string ParseAscii(Slice slice)
		{
			Contract.Requires(slice.HasValue && slice[0] == FdbTupleTypes.Bytes && slice[-1] == 0);

			if (slice.Count <= 2) return String.Empty;

			var decoded = UnescapeByteString(slice.Array, slice.Offset + 1, slice.Count - 2);

			return Encoding.Default.GetString(decoded.Array, decoded.Offset, decoded.Count);
		}

		/// <summary>Parse a tuple segment containing a unicode string</summary>
		public static string ParseUnicode(Slice slice)
		{
			Contract.Requires(slice.HasValue && slice[0] == FdbTupleTypes.Utf8 && slice[-1] == 0);

			if (slice.Count <= 2) return String.Empty;
			//TODO: check args
			var decoded = UnescapeByteString(slice.Array, slice.Offset + 1, slice.Count - 2);
			return Encoding.UTF8.GetString(decoded.Array, decoded.Offset, decoded.Count);
		}

		/// <summary>Parse a tuple segment containing an embedded tuple</summary>
		public static IFdbTuple ParseTuple(Slice slice)
		{
			Contract.Requires(slice.HasValue && slice[0] == FdbTupleTypes.TupleStart && slice[-1] == 0);
			if (slice.Count <= 2) return FdbTuple.Empty;

			return FdbTuplePackers.Unpack(slice.Substring(1, slice.Count - 2), true);
		}

		/// <summary>Parse a tuple segment containing a single precision number (float32)</summary>
		public static float ParseSingle(Slice slice)
		{
			Contract.Requires(slice.HasValue && slice[0] == FdbTupleTypes.Single);

			if (slice.Count != 5)
			{
				throw new FormatException("Slice has invalid size for a Single");
			}

			// We need to reverse encoding process: if first byte < 0x80 then it is negative (bits need to be flipped), else it is positive (highest bit must be set to 0)

			// read the raw bits
			uint bits = slice.ReadUInt32BE(1, 4); //OPTIMIZE: inline version?

			if ((bits & 0x80000000U) == 0)
			{ // negative
				bits = ~bits;
			}
			else
			{ // postive
				bits ^= 0x80000000U;
			}

			float value;
			unsafe { value = *((float*)&bits); }

			return value;
		}

		/// <summary>Parse a tuple segment containing a double precision number (float64)</summary>
		public static double ParseDouble(Slice slice)
		{
			Contract.Requires(slice.HasValue && slice[0] == FdbTupleTypes.Double);

			if (slice.Count != 9)
			{
				throw new FormatException("Slice has invalid size for a Double");
			}

			// We need to reverse encoding process: if first byte < 0x80 then it is negative (bits need to be flipped), else it is positive (highest bit must be set to 0)

			// read the raw bits
			ulong bits = slice.ReadUInt64BE(1, 8); //OPTIMIZE: inline version?

			if ((bits & 0x8000000000000000UL) == 0)
			{ // negative
				bits = ~bits;
			}
			else
			{ // postive
				bits ^= 0x8000000000000000UL;
			}

			// note: we could use BitConverter.Int64BitsToDouble(...), but it does the same thing, and also it does not exist for floats...
			double value;
			unsafe { value = *((double*)&bits); }

			return value;
		}

		/// <summary>Parse a tuple segment containing a 128-bit GUID</summary>
		public static Guid ParseGuid(Slice slice)
		{
			Contract.Requires(slice.HasValue && slice[0] == FdbTupleTypes.Uuid128);

			if (slice.Count != 17)
			{
				throw new FormatException("Slice has invalid size for a GUID");
			}

			// We store them in RFC 4122 under the hood, so we need to reverse them to the MS format
			return Uuid128.Convert(new Slice(slice.Array, slice.Offset + 1, 16));
		}

		/// <summary>Parse a tuple segment containing a 128-bit UUID</summary>
		public static Uuid128 ParseUuid128(Slice slice)
		{
			Contract.Requires(slice.HasValue && slice[0] == FdbTupleTypes.Uuid128);

			if (slice.Count != 17)
			{
				throw new FormatException("Slice has invalid size for a 128-bit UUID");
			}

			return new Uuid128(new Slice(slice.Array, slice.Offset + 1, 16));
		}

		/// <summary>Parse a tuple segment containing a 64-bit UUID</summary>
		public static Uuid64 ParseUuid64(Slice slice)
		{
			Contract.Requires(slice.HasValue && slice[0] == FdbTupleTypes.Uuid64);

			if (slice.Count != 9)
			{
				throw new FormatException("Slice has invalid size for a 64-bit UUID");
			}

			return new Uuid64(new Slice(slice.Array, slice.Offset + 1, 8));
		}

		#endregion

		#region Parsing...

		/// <summary>Decode the next token from a packed tuple</summary>
		/// <param name="reader">Parser from wich to read the next token</param>
		/// <returns>Token decoded, or Slice.Nil if there was no more data in the buffer</returns>
		public static Slice ParseNext(ref TupleReader reader)
		{
			int type = reader.Input.PeekByte();
			switch (type)
			{
				case -1:
				{ // End of Stream
					return Slice.Nil;
				}

				case FdbTupleTypes.Nil:
				{ // <00> / <00><FF> => null
					if (reader.Depth > 0)
					{ // must be <00><FF> inside an embedded tuple
						if (reader.Input.PeekByteAt(1) == 0xFF)
						{ // this is a Nil entry
							reader.Input.Skip(2);
							return Slice.Empty;
						}
						else
						{ // this is the end of the embedded tuple
							reader.Input.Skip(1);
							return Slice.Nil;
						}
					}
					else
					{ // can be <00> outside an embedded tuple
						reader.Input.Skip(1);
						return Slice.Empty;
					}
				}

				case FdbTupleTypes.Bytes:
				{ // <01>(bytes)<00>
					return reader.Input.ReadByteString();
				}

				case FdbTupleTypes.Utf8:
				{ // <02>(utf8 bytes)<00>
					return reader.Input.ReadByteString();
				}

				case FdbTupleTypes.TupleStart:
				{ // <03>(packed tuple)<04>

					//PERF: currently, we will first scan to get all the bytes of this tuple, and parse it later.
					// This means that we may need to scan multiple times the bytes, which may not be efficient if there are multiple embedded tuples inside each other
					return ReadEmbeddedTupleBytes(ref reader);
				}
				case FdbTupleTypes.Single:
				{ // <20>(4 bytes)
					return reader.Input.ReadBytes(5);
				}

				case FdbTupleTypes.Double:
				{ // <21>(8 bytes)
					return reader.Input.ReadBytes(9);
				}

				case FdbTupleTypes.Uuid128:
				{ // <30>(16 bytes)
					return reader.Input.ReadBytes(17);
				}

				case FdbTupleTypes.Uuid64:
				{ // <31>(8 bytes)
					return reader.Input.ReadBytes(9);
				}

				case FdbTupleTypes.AliasDirectory:
				case FdbTupleTypes.AliasSystem:
				{ // <FE> or <FF>
					return reader.Input.ReadBytes(1);
				}
			}

			if (type <= FdbTupleTypes.IntPos8 && type >= FdbTupleTypes.IntNeg8)
			{
				int bytes = type - FdbTupleTypes.IntZero;
				if (bytes < 0) bytes = -bytes;

				return reader.Input.ReadBytes(1 + bytes);
			}

			throw new FormatException(String.Format("Invalid tuple type byte {0} at index {1}/{2}", type, reader.Input.Position, reader.Input.Buffer.Count));
		}

		/// <summary>Read an embedded tuple, without parsing it</summary>
		internal static Slice ReadEmbeddedTupleBytes(ref TupleReader reader)
		{
			// The current embedded tuple starts here, and stops on a <00>, but itself can contain more embedded tuples, and could have a <00> bytes as part of regular items (like bytes, strings, that end with <00> or could contain a <00><FF> ...)
			// This means that we have to parse the tuple recursively, discard the tokens, and note where the cursor ended. The parsing of the tuple itself will be processed later.

			++reader.Depth;
			int start = reader.Input.Position;
			reader.Input.Skip(1);

			while(reader.Input.HasMore)
			{
				var token = ParseNext(ref reader);
				// the token will be Nil for either the end of the stream, or the end of the tuple
				// => since we already tested Input.HasMore, we know we are in the later case
				if (token.IsNull)
				{
					--reader.Depth;
					//note: ParseNext() has already eaten the <00>
					int end = reader.Input.Position;
					return reader.Input.Buffer.Substring(start, end - start);
				}
				// else: ignore this token, it will be processed later if the tuple is unpacked and accessed
			}

			throw new FormatException(String.Format("Truncated embedded tuple started at index {0}/{1}", start, reader.Input.Buffer.Count));
		}

		/// <summary>Skip a number of tokens</summary>
		/// <param name="reader">Cursor in the packed tuple to decode</param>
		/// <param name="count">Number of tokens to skip</param>
		/// <returns>True if there was <paramref name="count"/> tokens, false if the reader was too small.</returns>
		/// <remarks>Even if this method return true, you need to check that the reader has not reached the end before reading more token!</remarks>
		public static bool Skip(ref TupleReader reader, int count)
		{
			while (count-- > 0)
			{
				if (!reader.Input.HasMore) return false;
				var token = FdbTupleParser.ParseNext(ref reader);
				if (token.IsNull) return false;
			}
			return true;
		}

		/// <summary>Visit the different tokens of a packed tuple</summary>
		/// <param name="input">Reader positionned at the start of a packed tuple</param>
		/// <param name="visitor">Lambda called for each segment of a tuple. Returns true to continue parsing, or false to stop</param>
		/// <returns>Number of tokens that have been visited until either <paramref name="visitor"/> returned false, or <paramerf name="reader"> reached the end.</returns>
		public static T VisitNext<T>(ref TupleReader reader, Func<Slice, FdbTupleSegmentType, T> visitor)
		{
			if (!reader.Input.HasMore) throw new InvalidOperationException("The reader has already reached the end");
			var token = FdbTupleParser.ParseNext(ref reader);
			return visitor(token, FdbTupleTypes.DecodeSegmentType(ref token));
		}

		#endregion

		#region Bits Twiddling...

		/// <summary>Lookup table used to compute the index of the most significant bit</summary>
		private static readonly int[] MultiplyDeBruijnBitPosition = new int[32]
		{
			0, 9, 1, 10, 13, 21, 2, 29, 11, 14, 16, 18, 22, 25, 3, 30,
			8, 12, 20, 28, 15, 17, 24, 7, 19, 27, 23, 6, 26, 5, 4, 31
		};

		/// <summary>Returns the minimum number of bytes needed to represent a value</summary>
		/// <remarks>Note: will return 1 even for <param name="v"/> == 0</remarks>
		public static int NumberOfBytes(uint v)
		{
			return (MostSignificantBit(v) + 8) >> 3;
		}

		/// <summary>Returns the minimum number of bytes needed to represent a value</summary>
		/// <remarks>Note: will return 1 even for <param name="v"/> == 0</remarks>
		public static int NumberOfBytes(long v)
		{
			return v >= 0 ? NumberOfBytes((ulong)v) : v != long.MinValue ? NumberOfBytes((ulong)-v) : 8;
		}

		/// <summary>Returns the minimum number of bytes needed to represent a value</summary>
		/// <returns>Note: will return 1 even for <param name="v"/> == 0</returns>
		public static int NumberOfBytes(ulong v)
		{
			int msb = 0;

			if (v > 0xFFFFFFFF)
			{ // for 64-bit values, shift everything by 32 bits to the right
				msb += 32;
				v >>= 32;
			}
			msb += MostSignificantBit((uint)v);
			return (msb + 8) >> 3;
		}

		/// <summary>Returns the position of the most significant bit (0-based) in a 32-bit integer</summary>
		/// <param name="v">32-bit integer</param>
		/// <returns>Index of the most significant bit (0-based)</returns>
		public static int MostSignificantBit(uint v)
		{
			// from: http://graphics.stanford.edu/~seander/bithacks.html#IntegerLogDeBruijn

			v |= v >> 1; // first round down to one less than a power of 2 
			v |= v >> 2;
			v |= v >> 4;
			v |= v >> 8;
			v |= v >> 16;

			var r = (v * 0x07C4ACDDU) >> 27;
			return MultiplyDeBruijnBitPosition[r];
		}

		#endregion

	}
}
