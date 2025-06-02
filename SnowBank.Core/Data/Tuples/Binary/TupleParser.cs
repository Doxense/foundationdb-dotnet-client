#region Copyright (c) 2023-2025 SnowBank SAS, (c) 2005-2023 Doxense SAS
// All rights reserved.
// 
// Redistribution and use in source and binary forms, with or without
// modification, are permitted provided that the following conditions are met:
// 	* Redistributions of source code must retain the above copyright
// 	  notice, this list of conditions and the following disclaimer.
// 	* Redistributions in binary form must reproduce the above copyright
// 	  notice, this list of conditions and the following disclaimer in the
// 	  documentation and/or other materials provided with the distribution.
// 	* Neither the name of SnowBank nor the
// 	  names of its contributors may be used to endorse or promote products
// 	  derived from this software without specific prior written permission.
// 
// THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS "AS IS" AND
// ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED
// WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE
// DISCLAIMED. IN NO EVENT SHALL SNOWBANK SAS BE LIABLE FOR ANY
// DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES
// (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES;
// LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND
// ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT
// (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS
// SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
#endregion

namespace SnowBank.Data.Tuples.Binary
{
	using System.Buffers;
	using System.Buffers.Binary;
	using System.Numerics;
	using System.Text;
	using SnowBank.Data.Tuples;

	/// <summary>Helper class that contains low-level encoders for the tuple binary format</summary>
	public static class TupleParser
	{

		#region Serialization...

		/// <summary>Writes a null value at the end, and advance the cursor</summary>
		public static void WriteNil(ref TupleWriter writer)
		{
			if (writer.Depth == 0)
			{ // at the top level, NILs are escaped as <00>
				writer.Output.WriteByte(TupleTypes.Nil);
			}
			else
			{ // inside a tuple, NILs are escaped as <00><FF>
				writer.Output.WriteBytes(TupleTypes.Nil, 0xFF);
			}
		}

		public static void WriteBool(ref TupleWriter writer, bool value)
		{
			// null  => 00
			// false => 26
			// true  => 27
			//note: old versions used to encode bool as integer 0 or 1
			writer.Output.WriteByte(value ? TupleTypes.True : TupleTypes.False);
		}

		public static void WriteBool(ref TupleWriter writer, bool? value)
		{
			// null  => 00
			// false => 26
			// true  => 27
			if (value != null)
			{
				writer.Output.WriteByte(value.Value ? TupleTypes.True : TupleTypes.False);
			}
			else
			{
				WriteNil(ref writer);
			}
		}

		/// <summary>Writes an UInt8 at the end, and advance the cursor</summary>
		/// <param name="writer">Target buffer</param>
		/// <param name="value">Unsigned BYTE, 32 bits</param>
		public static void WriteByte(ref TupleWriter writer, byte value)
		{
			if (value == 0)
			{ // zero
				writer.Output.WriteByte(TupleTypes.IntZero);
			}
			else
			{ // 1..255: frequent for array index
				writer.Output.WriteBytes(TupleTypes.IntPos1, value);
			}
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void WriteByte(ref TupleWriter writer, byte? value)
		{
			if (!value.HasValue) WriteNil(ref writer); else WriteByte(ref writer, value.Value);
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
					writer.Output.WriteByte(TupleTypes.IntZero);
					return;
				}

				if (value > 0)
				{ // 1..255: frequent for array index
					writer.Output.WriteBytes(TupleTypes.IntPos1, (byte) value);
					return;
				}

				if (value > -256)
				{ // -255..-1
					writer.Output.WriteBytes(TupleTypes.IntNeg1, (byte) (255 + value));
					return;
				}
			}

			WriteInt64Slow(ref writer, value);
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void WriteInt32(ref TupleWriter writer, int? value)
		{
			if (!value.HasValue) WriteNil(ref writer); else WriteInt32(ref writer, value.Value);
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
					writer.Output.WriteByte(TupleTypes.IntZero);
					return;
				}

				if (value > 0)
				{ // 1..255: frequent for array index
					writer.Output.WriteBytes(TupleTypes.IntPos1, (byte) value);
					return;
				}

				if (value > -256)
				{ // -255..-1
					writer.Output.WriteBytes(TupleTypes.IntNeg1, (byte) (255 + value));
					return;
				}
			}

			WriteInt64Slow(ref writer, value);
		}


		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void WriteInt64(ref TupleWriter writer, long? value)
		{
			if (!value.HasValue) WriteNil(ref writer); else WriteInt64(ref writer, value.Value);
		}

		private static void WriteInt64Slow(ref TupleWriter writer, long value)
		{
			// we are only called for values <= -256 or >= 256

			// determine the number of bytes needed to encode the absolute value
			int bytes = NumberOfBytes(value);

			var buffer = writer.Output.EnsureBytes(bytes + 1);
			int p = writer.Output.Position;

			ulong v;
			if (value > 0)
			{ // simple case
				buffer[p++] = (byte)(TupleTypes.IntZero + bytes);
				v = (ulong)value;
			}
			else
			{ // we will encode the one's complement of the absolute value
				// -1 => 0xFE
				// -256 => 0xFFFE
				// -65536 => 0xFFFFFE
				buffer[p++] = (byte)(TupleTypes.IntZero - bytes);
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
					writer.Output.WriteByte(TupleTypes.IntZero);
				}
				else
				{ // 1..255
					writer.Output.WriteBytes(TupleTypes.IntPos1, (byte)value);
				}
			}
			else
			{ // >= 256
				WriteUInt64Slow(ref writer, value);
			}
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void WriteUInt32(ref TupleWriter writer, uint? value)
		{
			if (!value.HasValue) WriteNil(ref writer); else WriteUInt32(ref writer, value.Value);
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
					writer.Output.WriteByte(TupleTypes.IntZero);
				}
				else
				{ // 1..255
					writer.Output.WriteBytes(TupleTypes.IntPos1, (byte) value);
				}
			}
			else
			{ // >= 256
				WriteUInt64Slow(ref writer, value);
			}
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void WriteUInt64(ref TupleWriter writer, ulong? value)
		{
			if (!value.HasValue) WriteNil(ref writer); else WriteUInt64(ref writer, value.Value);
		}

		private static void WriteUInt64Slow(ref TupleWriter writer, ulong value)
		{
			// We are only called for values >= 256

			// determine the number of bytes needed to encode the value
			int bytes = NumberOfBytes(value);

			var buffer = writer.Output.EnsureBytes(bytes + 1);
			int p = writer.Output.Position;

			// simple case (ulong can only be positive)
			buffer[p++] = (byte) (TupleTypes.IntZero + bytes);

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
			{ // positive
				bits |= 0x80000000U;
			}
			var buffer = writer.Output.Allocate(5);
			buffer[0] = TupleTypes.Single;
			buffer[1] = (byte)(bits >> 24);
			buffer[2] = (byte)(bits >> 16);
			buffer[3] = (byte)(bits >> 8);
			buffer[4] = (byte)(bits);
		}


		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void WriteSingle(ref TupleWriter writer, float? value)
		{
			if (!value.HasValue) WriteNil(ref writer); else WriteSingle(ref writer, value.Value);
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
			{ // positive
				bits |= 0x8000000000000000UL;
			}
			var buffer = writer.Output.AllocateSpan(9);
			buffer[0] = TupleTypes.Double;
			buffer[1] = (byte)(bits >> 56);
			buffer[2] = (byte)(bits >> 48);
			buffer[3] = (byte)(bits >> 40);
			buffer[4] = (byte)(bits >> 32);
			buffer[5] = (byte)(bits >> 24);
			buffer[6] = (byte)(bits >> 16);
			buffer[7] = (byte)(bits >> 8);
			buffer[8] = (byte)(bits);
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void WriteDouble(ref TupleWriter writer, double? value)
		{
			if (!value.HasValue) WriteNil(ref writer); else WriteDouble(ref writer, value.Value);
		}

		public static void WriteDecimal(ref TupleWriter writer, decimal value)
		{
			throw new NotImplementedException();
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void WriteDecimal(ref TupleWriter writer, decimal? value)
		{
			if (!value.HasValue) WriteNil(ref writer); else WriteDecimal(ref writer, value.Value);
		}

#if NET8_0_OR_GREATER

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void WriteInt128(ref TupleWriter writer, Int128 value)
		{
			if (value >= 0)
			{
				WriteUInt128(ref writer, (UInt128) value);
			}
			else
			{
				WriteNegativeInt128(ref writer, value);
			}

			[MethodImpl(MethodImplOptions.NoInlining)]
			static void WriteNegativeInt128(ref TupleWriter writer, Int128 value)
			{

				if (value >= long.MinValue)
				{
					WriteInt64(ref writer, (long) value);
					return;
				}

				// Negative values are stored as one's complement (-1 => 0xFE)
				// - -256 ➜ -1 => 0B 01 00 00 ➜ 0B 01 FE

				// To get a compact representation, we need to get rid of the leading "FF",
				// BUT, to key ordering, we also need to reverse the length!
				// - we format the number as the one's complement of the absolute value, so -1 will be ... FF FF FF FE
				// - we remove all the leading 'FF', so we end up with only 'FE', which would take one byte to encode
				// - we compute 256 - LENGTH = 255, and use 0xFF as the "count"

				// get the one's complement of the absolute value (-1 => ... FF FE)
				value = ~(-value);

				// write the value into a tmp buffer, so that we can extract the minimum length
				Span<byte> tmp = stackalloc byte[16];
				BinaryPrimitives.WriteInt128BigEndian(tmp, value);

				// count the number of leading zeros
				int p = tmp.IndexOfAnyExcept((byte) 0xFF);
				if (p > 0)
				{
					tmp = tmp[p..];
				}

				var buffer = writer.Output.AllocateSpan(1 + 1 + tmp.Length);
				buffer[0] = TupleTypes.NegativeBigInteger;
				buffer[1] = (byte) (tmp.Length ^ 0xFF);
				tmp.CopyTo(buffer[2..]);
			}
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void WriteInt128(ref TupleWriter writer, Int128? value)
		{
			if (!value.HasValue) WriteNil(ref writer); else WriteInt128(ref writer, value.Value);
		}

		public static void WriteUInt128(ref TupleWriter writer, UInt128 value)
		{
			if (value == 0)
			{ // zero is encoded as a 0-length number
				writer.Output.WriteByte(TupleTypes.IntZero);
			}
			else if (value <= ulong.MaxValue)
			{ // "small big" integer fits in a single byte
				WriteUInt64(ref writer, (ulong) value);
			}
			else
			{
				WriteUInt128Slow(ref writer, value);
			}

			static void WriteUInt128Slow(ref TupleWriter writer, UInt128 value)
			{

				// write the value into a tmp buffer, so that we can extract the minimum length
				Span<byte> tmp = stackalloc byte[16];
				BinaryPrimitives.WriteUInt128BigEndian(tmp, value);

				// count the number of leading zeros
				int p = tmp.IndexOfAnyExcept((byte) 0);
				if (p > 0)
				{
					tmp = tmp[p..];
				}

				// we don't know the size yet, but the maximum length will be 18 bytes (header, count, 16 bytes)
				var buffer = writer.Output.AllocateSpan(1 + 1 + tmp.Length);
				buffer[0] = TupleTypes.PositiveBigInteger;
				buffer[1] = (byte) tmp.Length;
				tmp.CopyTo(buffer[2..]);
			}
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void WriteUInt128(ref TupleWriter writer, UInt128? value)
		{
			if (!value.HasValue) WriteNil(ref writer); else WriteUInt128(ref writer, value.Value);
		}

#endif

		public static void WriteBigInteger(ref TupleWriter writer, BigInteger value)
		{
			// lala
			if (value.IsZero)
			{ // zero 
				writer.Output.WriteByte(TupleTypes.IntZero);
				return;
			}

			if (value.Sign >= 0)
			{
				if (value <= ulong.MaxValue)
				{ // "small big number"
					WriteUInt64(ref writer, (ulong) value);
					return;
				}

				int count = value.GetByteCount();
				var buffer = writer.Output.GetSpan(checked(2 + count));

				value.TryWriteBytes(buffer[2..], out int written, isUnsigned: true, isBigEndian: true);

				buffer[0] = TupleTypes.PositiveBigInteger;
				buffer[1] = (byte) written;

				writer.Output.Advance(2 + written);
			}
			else
			{
				if (value >= long.MinValue)
				{ // "small big number"
					WriteInt64(ref writer, (long) value);
					return;
				}

				//TODO: handle the range between -(2^63 - 1) and -(2^64 - 1), we still are in "IntNeg8" !

				--value;

				int count = value.GetByteCount();
				var buffer = writer.Output.GetSpan(checked(2 + count));

				value.TryWriteBytes(buffer[2..], out int written, isUnsigned: false, isBigEndian: true);

				buffer[0] = TupleTypes.NegativeBigInteger;
				buffer[1] = (byte) (written ^ 0xFF);

				writer.Output.Advance(2 + written);
			}

		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void WriteBigInteger(ref TupleWriter writer, BigInteger? value)
		{
			if (!value.HasValue) WriteNil(ref writer); else WriteBigInteger(ref writer, value.Value);
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void WriteTimeSpan(ref TupleWriter writer, TimeSpan value)
		{
			// We have the same precision problem with storing DateTimes:
			// - Storing the number of ticks keeps the exact value, but is Windows-centric
			// - Storing the number of milliseconds as an integer will round the precision to 1 millisecond, which is not acceptable
			// - We could store the the number of milliseconds as a floating point value, which would require support of Floating Points in the Tuple Encoding (currently a Draft)
			// - It is frequent for JSON APIs and other database engines to represent durations as a number of SECONDS, using a floating point number.

			// Right now, we will store the duration as the number of seconds, using a 64-bit float

			WriteDouble(ref writer, value.TotalSeconds);
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void WriteTimeSpan(ref TupleWriter writer, TimeSpan? value)
		{
			if (!value.HasValue) WriteNil(ref writer); else WriteTimeSpan(ref writer, value.Value);
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void WriteDateTime(ref TupleWriter writer, DateTime value)
		{
			// The problem of serializing DateTime: TimeZone? Precision?
			// - Since we are going to lose the TimeZone infos anyway, we can just store everything in UTC and let the caller deal with it
			// - DateTime in .NET uses Ticks which produce numbers too large to fit in the 56 bits available in JavaScript
			// - Most other *nix uses the number of milliseconds since 1970-Jan-01 UTC, but if we store as an integer we will lose some precision (rounded to nearest millisecond)
			// - We could store the number of milliseconds as a floating point value, which would require support of Floating Points in the Tuple Encoding (currently a Draft)
			// - Other database engines store dates as a number of DAYS since Epoch, using a floating point number. This allows for quickly extracting the date by truncating the value, and the time by using the decimal part

			// Right now, we will store the date as the number of DAYS since Epoch, using a 64-bit float.
			// => storing a number of ticks would be MS-only anyway (56-bit limit in JS)
			// => JS binding MAY support decoding of 64-bit floats in the future, in which case the value would be preserved exactly.

			const long UNIX_EPOCH_EPOCH = 621355968000000000L;
			WriteDouble(ref writer, (value.ToUniversalTime().Ticks - UNIX_EPOCH_EPOCH) / (double) TimeSpan.TicksPerDay);
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void WriteDateTime(ref TupleWriter writer, DateTime? value)
		{
			if (!value.HasValue) WriteNil(ref writer); else WriteDateTime(ref writer, value.Value);
		}

		/// <summary>Writes a DateTimeOffset converted to the number of days since the Unix Epoch and stored as a 64-bit decimal</summary>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void WriteDateTimeOffset(ref TupleWriter writer, DateTimeOffset value)
		{
			// The problem of serializing DateTimeOffset: TimeZone? Precision?
			// - Since we are going to lose the TimeZone infos anyway, we can just store everything in UTC and let the caller deal with it
			// - DateTimeOffset in .NET uses Ticks which produce numbers too large to fit in the 56 bits available in JavaScript
			// - Most other *nix uses the number of milliseconds since 1970-Jan-01 UTC, but if we store as an integer we will lose some precision (rounded to nearest millisecond)
			// - We could store the number of milliseconds as a floating point value, which would require support of Floating Points in the Tuple Encoding (currently a Draft)
			// - Other database engines store dates as a number of DAYS since Epoch, using a floating point number. This allows for quickly extracting the date by truncating the value, and the time by using the decimal part

			// Right now, we will store the date as the number of DAYS since Epoch, using a 64-bit float.
			// => storing a number of ticks would be MS-only anyway (56-bit limit in JS)
			// => JS binding MAY support decoding of 64-bit floats in the future, in which case the value would be preserved exactly.

			//REVIEW: why not use an embedded tupple: (ElapsedDays, TimeZoneOffset) ?
			// - pros: keeps the timezone offset
			// - cons: would not be compatible with DateTime

			const long UNIX_EPOCH_EPOCH = 621355968000000000L;
			WriteDouble(ref writer, (value.ToUniversalTime().Ticks - UNIX_EPOCH_EPOCH) / (double) TimeSpan.TicksPerDay);
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void WriteDateTimeOffset(ref TupleWriter writer, DateTimeOffset? value)
		{
			if (!value.HasValue) WriteNil(ref writer); else WriteDateTimeOffset(ref writer, value.Value);
		}


		/// <summary>Writes a string encoded in UTF-8</summary>
		public static unsafe void WriteString(ref TupleWriter writer, string? value)
		{
			if (value == null)
			{ // "00"
				WriteNil(ref writer);
			}
			else if (value.Length == 0)
			{ // "02 00"
				writer.Output.WriteBytes(TupleTypes.Utf8, 0x00);
			}
			else
			{
				fixed(char* chars = value)
				{
					if (!TryWriteUnescapedUtf8String(ref writer, chars, value.Length))
					{ // the string contains \0 chars, we need to do it the hard way
						WriteNulEscapedBytes(ref writer, TupleTypes.Utf8, Encoding.UTF8.GetBytes(value));
					}
				}
			}
		}

		/// <summary>Writes a char array encoded in UTF-8</summary>
		internal static unsafe void WriteChars(ref TupleWriter writer, char[]? value, int offset, int count)
		{
			Contract.Debug.Requires(offset >= 0 && count >= 0);

			if (count == 0)
			{
				if (value == null)
				{ // "00"
					WriteNil(ref writer);
				}
				else
				{ // "02 00"
					writer.Output.WriteBytes(TupleTypes.Utf8, 0x00);
				}
			}
			else
			{
				//TODO: convert this to use Span<char> !
				fixed (char* chars = value)
				{
					if (!TryWriteUnescapedUtf8String(ref writer, chars + offset, count))
					{ // the string contains \0 chars, we need to do it the hard way
						WriteNulEscapedBytes(ref writer, TupleTypes.Utf8, Encoding.UTF8.GetBytes(value!, 0, count));
					}
				}
			}
		}

		private static unsafe void WriteUnescapedAsciiChars(ref TupleWriter writer, char* chars, int count)
		{
			Contract.Debug.Requires(chars != null && count >= 0);

			// copy and convert an ASCII string directly into the destination buffer

			writer.Output.EnsureBytes(2 + count);
			int pos = writer.Output.Position;
			char* end = chars + count;
			fixed (byte* buffer = writer.Output.Buffer)
			{
				buffer[pos++] = TupleTypes.Utf8;
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
			Contract.Debug.Requires(chars != null && count >= 0);

			// Several observations:
			// * Most strings will be keywords or ASCII-only with no zeroes. These can be copied directly to the buffer
			// * We will only attempt to optimize strings that don't have any 00 to escape to 00 FF. For these, we will fallback to converting to byte[] then escaping.
			// * Since .NET's strings are UTF-16, the max possible UNICODE value to encode is 0xFFFF, which takes 3 bytes in UTF-8 (EF BF BF)
			// * Most western europe languages have only a few non-ASCII chars here and there, and most of them will only use 2 bytes (ex: 'é' => 'C3 A9')
			// * More complex scripts with dedicated symbol pages (kanjis, arabic, ....) will take 2 or 3 bytes for each character.

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
			// We will iterate through the string, filling as much of the buffer as possible

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
			// * Eastern languages will have all chars encoded to 3 bytes. If we also pre-allocated 50% more, we should only need one resize of the buffer (150% x 2 = 300%), which is acceptable
			writer.Output.EnsureBytes(checked(2 + count + (count >> 1))); // preallocate 150% of the string + 2 bytes
			writer.Output.UnsafeWriteByte(TupleTypes.Utf8);

			var encoder = Encoding.UTF8.GetEncoder();
			// note: encoder.Convert() tries to fill up the buffer as much as possible with complete chars, and will set 'done' to true when all chars have been converted.
			do
			{
				encoder.Convert(ptr, remaining, buf, bufLen, true, out int charsUsed, out int bytesUsed, out done);
				if (bytesUsed > 0)
				{
					writer.Output.WriteBytes(new ReadOnlySpan<byte>(buf, bytesUsed));
				}
				remaining -= charsUsed;
				ptr += charsUsed;
			}
			while (!done);
			Contract.Debug.Assert(remaining == 0 && ptr == end);

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
				writer.Output.WriteBytes(TupleTypes.Utf8, 0x00, 0xFF, 0x00);
			}
			else if (value < 0x80)
			{ // 0x00..0x7F => 0xxxxxxx
				writer.Output.WriteBytes(TupleTypes.Utf8, (byte)value, 0x00);
			}
			else if (value <  0x800)
			{ // 0x80..0x7FF => 110xxxxx 10xxxxxx => two bytes
				writer.Output.WriteBytes(TupleTypes.Utf8, (byte)(0xC0 | (value >> 6)), (byte)(0x80 | (value & 0x3F)), 0x00);
			}
			else
			{ // 0x800..0xFFFF => 11110xxx 10xxxxxx 10xxxxxx 10xxxxxx
				// note: System.Char is 16 bits, and thus cannot represent UNICODE chars above 0xFFFF.
				// => This means that a System.Char will never take more than 3 bytes in UTF-8 !
				var tmp = Encoding.UTF8.GetBytes(new string(value, 1));
				writer.Output.EnsureBytes(tmp.Length + 2);
				writer.Output.UnsafeWriteByte(TupleTypes.Utf8);
				writer.Output.UnsafeWriteBytes(tmp.AsSpan());
				writer.Output.UnsafeWriteByte(0x00);
			}
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void WriteChar(ref TupleWriter writer, char? value)
		{
			if (!value.HasValue) WriteNil(ref writer); else WriteChar(ref writer, value.Value);
		}

		/// <summary>Writes a binary string</summary>
		public static void WriteBytes(ref TupleWriter writer, byte[]? value)
		{
			if (value == null)
			{
				WriteNil(ref writer);
			}
			else
			{
				WriteNulEscapedBytes(ref writer, TupleTypes.Bytes, value.AsSpan());
			}
		}

		/// <summary>Writes a binary string</summary>
		public static void WriteBytes(ref TupleWriter writer, byte[] value, int offset, int count)
		{
			WriteNulEscapedBytes(ref writer, TupleTypes.Bytes, value.AsSpan(offset, count));
		}

		/// <summary>Writes a binary string</summary>
		public static void WriteBytes(ref TupleWriter writer, ArraySegment<byte> value)
		{
			if (value.Count == 0 && value.Array == null)
			{ // default(ArraySegment<byte>) ~= null
				WriteNil(ref writer);
			}
			else
			{
				WriteNulEscapedBytes(ref writer, TupleTypes.Bytes, value.AsSpan());
			}
		}

		/// <summary>Writes a binary string</summary>
		public static void WriteBytes(ref TupleWriter writer, Slice value)
		{
			if (value.IsNull)
			{
				WriteNil(ref writer);
			}
			else
			{
				WriteNulEscapedBytes(ref writer, TupleTypes.Bytes, value.Span);
			}
		}

		/// <summary>Writes a binary string</summary>
		public static void WriteBytes(ref TupleWriter writer, ReadOnlySpan<byte> value)
		{
			WriteNulEscapedBytes(ref writer, TupleTypes.Bytes, value);
		}

		/// <summary>Writes a buffer with all instances of 0 escaped as '00 FF'</summary>
		internal static void WriteNulEscapedBytes(ref TupleWriter writer, byte type, ReadOnlySpan<byte> value)
		{
			int n = value.Length;

			// we need to know if there are any NUL chars (\0) that need escaping...
			// (we will also need to add 1 byte to the buffer size per NUL)
			foreach(var b in value)
			{
				//TODO: optimize this!
				if (b == 0) ++n;
			}

			var buffer = writer.Output.EnsureBytes(n + 2);
			int p = writer.Output.Position;
			buffer[p++] = type;
			if (n > 0)
			{
				if (n == value.Length)
				{ // no NULs in the string, can copy all at once
					value.CopyTo(buffer.AsSpan(p));
					p += n;
				}
				else
				{ // we need to escape all NULs
					foreach(var b in value)
					{
						//TODO: optimize this!
						buffer[p++] = b;
						if (b == 0) buffer[p++] = 0xFF;
					}
				}
			}
			buffer[p] = 0x00;
			writer.Output.Position = p + 1;
		}

		/// <summary>Writes a RFC 4122 encoded 16-byte Microsoft GUID</summary>
		public static void WriteGuid(ref TupleWriter writer, Guid value)
		{
			var span = writer.Output.AllocateSpan(17);
			span[0] = TupleTypes.Uuid128;
			// Guids should be stored using the RFC 4122 standard, so we need to swap some parts of the System.Guid (handled by Uuid128)
			new Uuid128(value).WriteTo(span.Slice(1));
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void WriteGuid(ref TupleWriter writer, Guid? value)
		{
			if (!value.HasValue)
			{
				WriteNil(ref writer);
			}
			else
			{
				WriteGuid(ref writer, value.Value);
			}
		}

		/// <summary>Writes a RFC 4122 encoded 128-bit UUID</summary>
		public static void WriteUuid128(ref TupleWriter writer, Uuid128 value)
		{
			var span = writer.Output.AllocateSpan(17);
			span[0] = TupleTypes.Uuid128;
			value.WriteTo(span.Slice(1));
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void WriteUuid128(ref TupleWriter writer, Uuid128? value)
		{
			if (!value.HasValue)
			{
				WriteNil(ref writer);
			}
			else
			{
				WriteUuid128(ref writer, value.Value);
			}
		}

		/// <summary>Writes a 96-bit UUID</summary>
		public static void WriteUuid96(ref TupleWriter writer, Uuid96 value)
		{
			var span = writer.Output.AllocateSpan(13);
			span[0] = TupleTypes.VersionStamp96;
			value.WriteTo(span.Slice(1));
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void WriteUuid96(ref TupleWriter writer, Uuid96? value)
		{
			if (!value.HasValue)
			{
				WriteNil(ref writer);
			}
			else
			{
				WriteUuid96(ref writer, value.Value);
			}
		}

		/// <summary>Writes a 80-bit UUID</summary>
		public static void WriteUuid80(ref TupleWriter writer, Uuid80 value)
		{
			var span = writer.Output.AllocateSpan(11);
			span[0] = TupleTypes.VersionStamp80;
			value.WriteToUnsafe(span.Slice(1));
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void WriteUuid80(ref TupleWriter writer, Uuid80? value)
		{
			if (!value.HasValue)
			{
				WriteNil(ref writer);
			}
			else
			{
				WriteUuid80(ref writer, value.Value);
			}
		}

		/// <summary>Writes a 64-bit UUID</summary>
		public static void WriteUuid64(ref TupleWriter writer, Uuid64 value)
		{
			var span = writer.Output.AllocateSpan(9);
			span[0] = TupleTypes.Uuid64;
			value.WriteToUnsafe(span.Slice(1));
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void WriteUuid64(ref TupleWriter writer, Uuid64? value)
		{
			if (!value.HasValue)
			{
				WriteNil(ref writer);
			}
			else
			{
				WriteUuid64(ref writer, value.Value);
			}
		}

		public static void WriteVersionStamp(ref TupleWriter writer, VersionStamp value)
		{
			if (value.HasUserVersion)
			{ // 96-bits VersionStamp
				var span = writer.Output.AllocateSpan(13);
				span[0] = TupleTypes.VersionStamp96;
				VersionStamp.WriteUnsafe(span.Slice(1), in value);
			}
			else
			{ // 80-bits VersionStamp
				var span = writer.Output.AllocateSpan(11);
				span[0] = TupleTypes.VersionStamp80;
				VersionStamp.WriteUnsafe(span.Slice(1), in value);
			}
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void WriteVersionStamp(ref TupleWriter writer, VersionStamp? value)
		{
			if (!value.HasValue)
			{
				WriteNil(ref writer);
			}
			else
			{
				WriteVersionStamp(ref writer, value.Value);
			}
		}

		public static void WriteUserType(ref TupleWriter writer, TuPackUserType? value)
		{
			if (value == null)
			{
				WriteNil(ref writer);
				return;
			}

			ref readonly Slice arg = ref value.Value;
			if (arg.Count == 0)
			{
				writer.Output.WriteByte((byte) value.Type);
			}
			else
			{
				writer.Output.EnsureBytes(checked(1 + arg.Count));
				writer.Output.UnsafeWriteByte((byte) value.Type);
				writer.Output.UnsafeWriteBytes(arg);
			}
		}

		/// <summary>Mark the start of a new embedded tuple</summary>
		public static void BeginTuple(ref TupleWriter writer)
		{
			writer.Depth++;
			writer.Output.WriteByte(TupleTypes.EmbeddedTuple);
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
			int bytes = type - TupleTypes.IntZero;
			if (bytes == 0) return 0L;

			bool neg = false;
			if (bytes < 0)
			{
				bytes = -bytes;
				neg = true;
			}

			if (bytes > 8) throw new FormatException("Invalid size for tuple integer");
			long value = (long) slice.ReadUInt64BE(1, bytes);

			if (neg)
			{ // the value is encoded as the one's complement of the absolute value
				value = (-(~value));
				if (bytes < 8) value |= (-1L << (bytes << 3));
				return value;
			}

			return value;
		}

		/// <summary>Parse a tuple segment containing a signed 64-bit integer</summary>
		/// <remarks>This method should only be used by custom decoders.</remarks>
		public static long ParseInt64(int type, ReadOnlySpan<byte> slice)
		{
			int bytes = type - TupleTypes.IntZero;
			if (bytes == 0) return 0L;

			bool neg = false;
			if (bytes < 0)
			{
				bytes = -bytes;
				neg = true;
			}

			if (bytes > 8) throw new FormatException("Invalid size for tuple integer");
			long value = (long) slice.Slice(1, bytes).ToUInt64BE();

			if (neg)
			{ // the value is encoded as the one's complement of the absolute value
				value = (-(~value));
				if (bytes < 8) value |= (-1L << (bytes << 3));
				return value;
			}

			return value;
		}

		private static bool ShouldUnescapeByteString(ReadOnlySpan<byte> buffer)
		{
			// check for nulls

			foreach (var b in buffer)
			{
				if (b == 0)
				{ // found a 0, switch to slow path
					return true;
				}
			}

			// buffer is clean, we can return it as-is
			return true;
		}

		private static bool TryUnescapeByteString(ReadOnlySpan<byte> buffer, Span<byte> output, out int bytesWritten)
		{
			int p = 0;
			for(int i = 0; i < buffer.Length; i++)
			{
				byte b = buffer[i];
				if (b == 0)
				{ // skip next FF
					//TODO: check that next byte really is 0xFF
					++i;
				}

				if (p >= output.Length) goto too_small;
				output[p++] = b;
			}

			bytesWritten = p;
			return true;
		too_small:
			bytesWritten = 0;
			return false;
		}

		/// <summary>Parse a tuple segment containing a byte array</summary>
		[Pure]
		public static Slice ParseBytes(Slice slice)
		{
			Contract.Debug.Requires(slice.HasValue && slice[0] == TupleTypes.Bytes && slice[slice.Count - 1] == 0);
			if (slice.Count <= 2) return Slice.Empty;

			var chunk = slice.Substring(1, slice.Count - 2);
			if (!ShouldUnescapeByteString(chunk.Span))
			{
				return chunk;
			}

			var span = new byte[chunk.Count];
			if (!TryUnescapeByteString(chunk.Span, span, out int written))
			{ // should never happen since decoding can only reduce the size!?
				throw new InvalidOperationException();
			}
			Contract.Debug.Requires(written <= span.Length);

			return new Slice(span, 0, written);
		}

		/// <summary>Parse a tuple segment containing a byte array</summary>
		[Pure]
		public static Slice ParseBytes(ReadOnlySpan<byte> slice)
		{
			Contract.Debug.Requires(slice.Length > 1 && slice[0] == TupleTypes.Bytes && slice[^1] == 0);
			if (slice.Length <= 2) return Slice.Empty;

			var chunk = slice.Slice(1, slice.Length - 2);
			if (!ShouldUnescapeByteString(chunk))
			{
				return Slice.Copy(chunk);
			}

			var span = new byte[chunk.Length];
			if (!TryUnescapeByteString(chunk, span, out int written))
			{ // should never happen since decoding can only reduce the size!?
				throw new InvalidOperationException();
			}
			Contract.Debug.Requires(written <= span.Length);

			return new Slice(span, 0, written);
		}

		/// <summary>Parse a tuple segment containing an ASCII string stored as a byte array</summary>
		[Pure]
		public static string ParseAscii(Slice slice)
		{
			Contract.Debug.Requires(slice.HasValue && slice[0] == TupleTypes.Bytes && slice[-1] == 0);

			if (slice.Count <= 2) return string.Empty;

			var chunk = slice.Substring(1, slice.Count - 2).Span;
			if (!ShouldUnescapeByteString(chunk))
			{
				return Encoding.Default.GetString(chunk);
			}
			var buffer = ArrayPool<byte>.Shared.Rent(chunk.Length);
			if (!TryUnescapeByteString(chunk, buffer, out int written))
			{ // should never happen since decoding can only reduce the size!?
				throw new InvalidOperationException();
			}

			string s = Encoding.Default.GetString(buffer, 0, written);
			ArrayPool<byte>.Shared.Return(buffer);
			return s;
		}

		/// <summary>Parse a tuple segment containing an ASCII string stored as a byte array</summary>
		[Pure]
		public static string ParseAscii(ReadOnlySpan<byte> slice)
		{
			Contract.Debug.Requires(slice.Length > 0 && slice[0] == TupleTypes.Bytes && slice[^1] == 0);

			if (slice.Length <= 2) return string.Empty;

			var chunk = slice.Slice(1, slice.Length - 2);
			if (!ShouldUnescapeByteString(chunk))
			{
				return Encoding.Default.GetString(chunk);
			}
			var buffer = ArrayPool<byte>.Shared.Rent(chunk.Length);
			if (!TryUnescapeByteString(chunk, buffer, out int written))
			{ // should never happen since decoding can only reduce the size!?
				throw new InvalidOperationException();
			}

			string s = Encoding.Default.GetString(buffer, 0, written);
			ArrayPool<byte>.Shared.Return(buffer);
			return s;
		}

		/// <summary>Parse a tuple segment containing a unicode string</summary>
		[Pure]
		public static string ParseUnicode(Slice slice)
		{
			Contract.Debug.Requires(slice.HasValue && slice[0] == TupleTypes.Utf8 && slice[-1] == 0);

			if (slice.Count <= 2) return string.Empty;

			var chunk = slice.Substring(1, slice.Count - 2).Span;
			if (!ShouldUnescapeByteString(chunk))
			{
				return Encoding.UTF8.GetString(chunk);
			}
			var buffer = ArrayPool<byte>.Shared.Rent(chunk.Length);
			if (!TryUnescapeByteString(chunk, buffer, out int written))
			{ // should never happen since decoding can only reduce the size!?
				throw new InvalidOperationException();
			}

			var s = Encoding.UTF8.GetString(buffer, 0, written);
			ArrayPool<byte>.Shared.Return(buffer);
			return s;
		}

		/// <summary>Parse a tuple segment containing a unicode string</summary>
		[Pure]
		public static string ParseUnicode(ReadOnlySpan<byte> slice)
		{
			Contract.Debug.Requires(slice.Length > 1 && slice[0] == TupleTypes.Utf8 && slice[^1] == 0);

			if (slice.Length <= 2) return string.Empty;

			var chunk = slice.Slice(1, slice.Length - 2);
			if (!ShouldUnescapeByteString(chunk))
			{
				return Encoding.UTF8.GetString(chunk);
			}
			var buffer = ArrayPool<byte>.Shared.Rent(chunk.Length);
			if (!TryUnescapeByteString(chunk, buffer, out int written))
			{ // should never happen since decoding can only reduce the size!?
				throw new InvalidOperationException();
			}

			var s = Encoding.UTF8.GetString(buffer, 0, written);
			ArrayPool<byte>.Shared.Return(buffer);
			return s;
		}

		/// <summary>Parse a tuple segment containing an embedded tuple</summary>
		[Pure]
		public static IVarTuple ParseEmbeddedTuple(Slice slice)
		{
			Contract.Debug.Requires(slice.Count > 0 && slice[0] == TupleTypes.EmbeddedTuple && slice[^1] == 0);
			if (slice.Count <= 2) return STuple.Empty;

			var chunk = slice.Substring(1, slice.Count - 2);
			var reader = new TupleReader(chunk.Span, depth: 1);
			return TuplePackers.Unpack(ref reader).ToTuple(chunk);
		}

		/// <summary>Parse a tuple segment containing an embedded tuple</summary>
		[Pure]
		public static SpanTuple ParseEmbeddedTuple(ReadOnlySpan<byte> slice)
		{
			Contract.Debug.Requires(slice.Length > 0 && slice[0] == TupleTypes.EmbeddedTuple && slice[^1] == 0);
			if (slice.Length <= 2) return SpanTuple.Empty;

			var reader = new TupleReader(slice.Slice(1, slice.Length - 2), depth: 1);
			return TuplePackers.Unpack(ref reader);
		}

		/// <summary>Parse a tuple segment containing a negative big integer</summary>
		public static BigInteger ParseNegativeBigInteger(ReadOnlySpan<byte> slice)
		{
			Contract.Debug.Requires(slice.Length > 2 && slice[0] == TupleTypes.NegativeBigInteger);

			if (slice.Length is < 3)
			{
				throw new FormatException("Slice has invalid size for a Big Integer containing a UInt128");
			}

			int len = slice[1];
			len ^= 0xFF;
			if (slice.Length != len + 2)
			{
				throw new FormatException("Slice length does not match the embedded length for a Big Integer");
			}

			var value = new BigInteger(slice[2..], isUnsigned: false, isBigEndian: true);

			return value + 1;
		}

		/// <summary>Parse a tuple segment containing a positive big integer</summary>
		public static BigInteger ParsePositiveBigInteger(ReadOnlySpan<byte> slice)
		{
			Contract.Debug.Requires(slice.Length > 1 && slice[0] == TupleTypes.PositiveBigInteger);

			if (slice.Length is < 2)
			{
				throw new FormatException("Slice has invalid size for a Big Integer containing a UInt128");
			}

			int len = slice[1];
			if (slice.Length != len + 2)
			{
				throw new FormatException("Slice length does not match the embedded length for a Big Integer");
			}

			return new BigInteger(slice[2..], isUnsigned: true, isBigEndian: true);
		}

		/// <summary>Parse a tuple segment containing a single precision number (float32)</summary>
		[Pure]
		public static float ParseSingle(Slice slice)
		{
			Contract.Debug.Requires(slice.HasValue && slice[0] == TupleTypes.Single);

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
			{ // positive
				bits ^= 0x80000000U;
			}

			float value;
			unsafe { value = *((float*)&bits); }

			return value;
		}

		/// <summary>Parse a tuple segment containing a single precision number (float32)</summary>
		[Pure]
		public static float ParseSingle(ReadOnlySpan<byte> slice)
		{
			Contract.Debug.Requires(slice.Length > 0 && slice[0] == TupleTypes.Single);

			if (slice.Length != 5)
			{
				throw new FormatException("Slice has invalid size for a Single");
			}

			// We need to reverse encoding process: if first byte < 0x80 then it is negative (bits need to be flipped), else it is positive (highest bit must be set to 0)

			// read the raw bits
			uint bits = BinaryPrimitives.ReadUInt32BigEndian(slice.Slice(1, 4));

			if ((bits & 0x80000000U) == 0)
			{ // negative
				bits = ~bits;
			}
			else
			{ // positive
				bits ^= 0x80000000U;
			}

			float value;
			unsafe { value = *((float*)&bits); }

			return value;
		}

		/// <summary>Parse a tuple segment containing a double precision number (float64)</summary>
		[Pure]
		public static double ParseDouble(Slice slice)
		{
			Contract.Debug.Requires(slice.HasValue && slice[0] == TupleTypes.Double);

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
			{ // positive
				bits ^= 0x8000000000000000UL;
			}

			// note: we could use BitConverter.Int64BitsToDouble(...), but it does the same thing, and also it does not exist for floats...
			double value;
			unsafe { value = *((double*)&bits); }

			return value;
		}

		/// <summary>Parse a tuple segment containing a double precision number (float64)</summary>
		[Pure]
		public static double ParseDouble(ReadOnlySpan<byte> slice)
		{
			Contract.Debug.Requires(slice.Length > 0 && slice[0] == TupleTypes.Double);

			if (slice.Length != 9)
			{
				throw new FormatException("Slice has invalid size for a Double");
			}

			// We need to reverse encoding process: if first byte < 0x80 then it is negative (bits need to be flipped), else it is positive (highest bit must be set to 0)

			// read the raw bits
			ulong bits = BinaryPrimitives.ReadUInt64BigEndian(slice.Slice(1, 8));

			if ((bits & 0x8000000000000000UL) == 0)
			{ // negative
				bits = ~bits;
			}
			else
			{ // positive
				bits ^= 0x8000000000000000UL;
			}

			// note: we could use BitConverter.Int64BitsToDouble(...), but it does the same thing, and also it does not exist for floats...
			double value;
			unsafe { value = *((double*)&bits); }

			return value;
		}

		/// <summary>Parse a tuple segment containing a quadruple precision number (float128)</summary>
		[Pure]
		public static decimal ParseDecimal(Slice slice)
		{
			Contract.Debug.Requires(slice.HasValue && slice[0] == TupleTypes.Decimal);

			if (slice.Count != 17)
			{
				throw new FormatException("Slice has invalid size for a Decimal");
			}

			throw new NotImplementedException();
		}

		/// <summary>Parse a tuple segment containing a quadruple precision number (float128)</summary>
		[Pure]
		public static decimal ParseDecimal(ReadOnlySpan<byte> slice)
		{
			Contract.Debug.Requires(slice.Length > 0 && slice[0] == TupleTypes.Decimal);

			if (slice.Length != 17)
			{
				throw new FormatException("Slice has invalid size for a Decimal");
			}

			throw new NotImplementedException();
		}

#if NET8_0_OR_GREATER

		/// <summary>Parse a tuple segment containing a positive big integer into a <see cref="UInt128"/></summary>
		[Pure]
		public static UInt128 ParsePositiveUInt128(ReadOnlySpan<byte> slice)
		{
			Contract.Debug.Requires(slice.Length > 0 && slice[0] == TupleTypes.PositiveBigInteger);

			if (slice.Length is < 2 or > 18)
			{
				throw new FormatException("Slice has invalid size for a Big Integer containing a UInt128");
			}

			int len = slice[1];
			if (slice.Length != len + 2)
			{
				throw new FormatException("Slice length does not match the embedded length for a Big Integer");
			}

			if (len >= 16)
			{
				return BinaryPrimitives.ReadUInt128BigEndian(slice[2..]);
			}

			// we need to copy it to add back the leading zeroes
			Span<byte> tmp = stackalloc byte[16];
			tmp.Clear();
			slice[2..].CopyTo(tmp[(16 - len)..]);
			return BinaryPrimitives.ReadUInt128BigEndian(tmp);
		}

		/// <summary>Parse a tuple segment containing a negative big integer into a <see cref="Int128"/></summary>
		[Pure]
		public static Int128 ParseNegativeInt128(ReadOnlySpan<byte> slice)
		{
			Contract.Debug.Requires(slice.Length > 0 && slice[0] == TupleTypes.NegativeBigInteger);

			if (slice.Length is < 2 or > 18)
			{
				throw new FormatException("Slice has invalid size for a negative Big Integer containing a Int128");
			}

			int len = slice[1];
			if (len == 0)
			{
				throw new FormatException("Slice cannot have an embedded length of 0 for a negative Big Integer");
			}

			len ^= 0xFF;
			if (slice.Length != len + 2)
			{
				throw new FormatException("Slice length does not match the embedded length for a negative Big Integer");
			}

			Int128 value;
			if (len >= 16)
			{
				value = BinaryPrimitives.ReadInt128BigEndian(slice[2..]);
			}
			else
			{
				// we need to copy it to add back the leading zeroes
				Span<byte> tmp = stackalloc byte[16];
				tmp.Fill(0xFF);
				slice[2..].CopyTo(tmp[(16 - len)..]);
				value = BinaryPrimitives.ReadInt128BigEndian(tmp);
			}

			// inverse the one's complement (0xFE -> -1)
			value += 1;

			return value;
		}

 #endif

		/// <summary>Parse a tuple segment containing a 128-bit GUID</summary>
		[Pure]
		public static Guid ParseGuid(Slice slice)
		{
			Contract.Debug.Requires(slice.HasValue && slice[0] == TupleTypes.Uuid128);

			if (slice.Count != 17)
			{
				throw new FormatException("Slice has invalid size for a GUID");
			}

			// We store them in RFC 4122 under the hood, so we need to reverse them to the MS format
			return Uuid128.Convert(slice.Substring(1));
		}

		/// <summary>Parse a tuple segment containing a 128-bit GUID</summary>
		[Pure]
		public static Guid ParseGuid(ReadOnlySpan<byte> slice)
		{
			Contract.Debug.Requires(slice.Length > 0 && slice[0] == TupleTypes.Uuid128);

			if (slice.Length != 17)
			{
				throw new FormatException("Slice has invalid size for a GUID");
			}

			// We store them in RFC 4122 under the hood, so we need to reverse them to the MS format
			return Uuid128.Convert(slice.Slice(1));
		}

		/// <summary>Parse a tuple segment containing a 128-bit UUID</summary>
		[Pure]
		public static Uuid128 ParseUuid128(Slice slice)
		{
			Contract.Debug.Requires(slice.HasValue && slice[0] == TupleTypes.Uuid128);

			if (slice.Count != 17)
			{
				throw new FormatException("Slice has invalid size for a 128-bit UUID");
			}

			return new Uuid128(slice.Substring(1));
		}

		/// <summary>Parse a tuple segment containing a 128-bit UUID</summary>
		[Pure]
		public static Uuid128 ParseUuid128(ReadOnlySpan<byte> slice)
		{
			Contract.Debug.Requires(slice.Length > 0 && slice[0] == TupleTypes.Uuid128);

			if (slice.Length != 17)
			{
				throw new FormatException("Slice has invalid size for a 128-bit UUID");
			}

			return new Uuid128(slice.Slice(1));
		}

		/// <summary>Parse a tuple segment containing a 64-bit UUID</summary>
		[Pure]
		public static Uuid64 ParseUuid64(Slice slice)
		{
			Contract.Debug.Requires(slice.HasValue && slice[0] == TupleTypes.Uuid64);

			if (slice.Count != 9)
			{
				throw new FormatException("Slice has invalid size for a 64-bit UUID");
			}

			return Uuid64.Read(slice.Substring(1, 8));
		}

		/// <summary>Parse a tuple segment containing a 64-bit UUID</summary>
		[Pure]
		public static Uuid64 ParseUuid64(ReadOnlySpan<byte> slice)
		{
			Contract.Debug.Requires(slice.Length > 0 && slice[0] == TupleTypes.Uuid64);

			if (slice.Length != 9)
			{
				throw new FormatException("Slice has invalid size for a 64-bit UUID");
			}

			return Uuid64.Read(slice.Slice(1, 8));
		}

		/// <summary>Parse a tuple segment containing an 80-bit or 96-bit VersionStamp</summary>
		[Pure]
		public static VersionStamp ParseVersionStamp(Slice slice)
		{
			Contract.Debug.Requires(slice.HasValue && (slice[0] == TupleTypes.VersionStamp80 || slice[0] == TupleTypes.VersionStamp96));

			if (slice.Count != 11 && slice.Count != 13)
			{
				throw new FormatException("Slice has invalid size for a VersionStamp");
			}

			return VersionStamp.ReadFrom(slice.Substring(1));
		}

		/// <summary>Parse a tuple segment containing an 80-bit or 96-bit VersionStamp</summary>
		[Pure]
		public static VersionStamp ParseVersionStamp(ReadOnlySpan<byte> slice)
		{
			Contract.Debug.Requires(slice.Length > 0 && (slice[0] is TupleTypes.VersionStamp80 or TupleTypes.VersionStamp96));

			if (slice.Length != 11 && slice.Length != 13)
			{
				throw new FormatException("Slice has invalid size for a VersionStamp");
			}

			return VersionStamp.ReadFrom(slice[1..]);
		}

		#endregion

		#region Parsing...

		/// <summary>Decode the next token from a packed tuple</summary>
		/// <returns>Token decoded, or Slice.Nil if there was no more data in the buffer</returns>
		public static bool TryParseNext(ref TupleReader reader, out Range token, out Exception? error)
		{
			int r = reader.Remaining;
			if (r <= 0)
			{ // End of Stream
				token = default;
				error = null;
				return false;
			}
			int type = reader.Peek();
			switch (type)
			{
				case TupleTypes.Nil:
				{ // <00> / <00><FF> => null
					if (reader.Depth > 0)
					{ // must be <00><FF> inside an embedded tuple
						if (r > 1 && reader.PeekAt(1) == 0xFF)
						{ // this is a Nil entry
							token = new Range(reader.Cursor, reader.Cursor + 1);
							reader.Advance(2);
							error = null;
							return true;
						}
						else
						{ // this is the end of the embedded tuple
							reader.Advance(1);
							token = default;
							error = null;
							return false;
						}
					}
					else
					{ // can be <00> outside an embedded tuple
						token = new Range(reader.Cursor, reader.Cursor + 1);
						reader.Advance(1);
						error = null;
						return true;
					}
				}

				case TupleTypes.Bytes:
				{ // <01>(bytes)<00>
					return reader.TryReadByteString(out token, out error);
				}

				case TupleTypes.Utf8:
				{ // <02>(utf8 bytes)<00>
					return reader.TryReadByteString(out token, out error);
				}

				case TupleTypes.LegacyTupleStart:
				{ // <03>(packed tuple)<04>

					//note: this format is NOT SUPPORTED ANYMORE, because it was not compatible with the current spec (<03>...<00> instead of <03>...<04> and is replaced by <05>....<00>)
					//we prefer throwing here instead of still attempting to decode the tuple, because it could silently break layers (if we read an old-style key and update it with the new-style format)
					token = default;
					error = TupleParser.FailLegacyTupleNotSupported();
					return false;
				}
				case TupleTypes.EmbeddedTuple:
				{ // <05>(packed tuple)<00>
					//PERF: currently, we will first scan to get all the bytes of this tuple, and parse it later.
					// This means that we may need to scan multiple times the bytes, which may not be efficient if there are multiple embedded tuples inside each other
					return TryReadEmbeddedTupleBytes(ref reader, out token, out error);
				}

				case TupleTypes.NegativeBigInteger:
				{ // <0B><##>(bytes)
					return reader.TryReadNegativeBigInteger(out token, out error);
				}

				case TupleTypes.PositiveBigInteger:
				{ // <1D><##>(bytes)
					return reader.TryReadPositiveBigInteger(out token, out error);
				}

				case TupleTypes.Single:
				{ // <20>(4 bytes)
					return reader.TryReadBytes(5, out token, out error);
				}

				case TupleTypes.Double:
				{ // <21>(8 bytes)
					return reader.TryReadBytes(9, out token, out error);
				}

				case TupleTypes.Triple:
				{ // <22>(10 bytes)
					return reader.TryReadBytes(11, out token, out error);
				}

				case TupleTypes.Decimal:
				{ // <23>(16 bytes)
					return reader.TryReadBytes(17, out token, out error);
				}

				case TupleTypes.False:
				{ // <26>
					return reader.TryReadBytes(1, out token, out error);
				}
				case TupleTypes.True:
				{ // <27>
					return reader.TryReadBytes(1, out token, out error);
				}

				case TupleTypes.Uuid128:
				{ // <30>(16 bytes)
					return reader.TryReadBytes(17, out token, out error);
				}

				case TupleTypes.Uuid64:
				{ // <31>(8 bytes)
					return reader.TryReadBytes(9, out token, out error);
				}

				case TupleTypes.VersionStamp80:
				{ // <32>(10 bytes)
					return reader.TryReadBytes(11, out token, out error);
				}

				case TupleTypes.VersionStamp96:
				{ // <33>(12 bytes)
					return reader.TryReadBytes(13, out token, out error);
				}

				case TupleTypes.Directory:
				case TupleTypes.Escape:
				{ // <FE> or <FF>

					// if <FF> and this is the first byte, we are reading a system key like '\xFF/something"
					if (reader.Cursor == 0)
					{
						token = new(reader.Cursor, reader.Input.Length);
						reader.Advance(reader.Remaining);
						error = null;
						return true;
					}

					return reader.TryReadBytes(1, out token, out error);
				}
			}

			if (type <= TupleTypes.IntPos8 && type >= TupleTypes.IntNeg8)
			{
				int bytes = type - TupleTypes.IntZero;
				if (bytes < 0) bytes = -bytes;

				return reader.TryReadBytes(1 + bytes, out token, out error);
			}

			token = default;
			error = new FormatException($"Invalid tuple type byte {type} at index {reader.Cursor}/{reader.Input.Length}");
			return false;
		}

		/// <summary>Read an embedded tuple, without parsing it</summary>
		private static bool TryReadEmbeddedTupleBytes(ref TupleReader reader, out Range token, out Exception? error)
		{
			// The current embedded tuple starts here, and stops on a <00>, but itself can contain more embedded tuples, and could have a <00> bytes as part of regular items (like bytes, strings, that end with <00> or could contain a <00><FF> ...)
			// This means that we have to parse the tuple recursively, discard the tokens, and note where the cursor ended. The parsing of the tuple itself will be processed later.

			++reader.Depth;
			int start = reader.Cursor;
			reader.Advance(1);

			while(reader.HasMore)
			{
				if (!TryParseNext(ref reader, out token, out error))
				{
					if (error != null)
					{
						return false;
					}

					// the token will be Nil for either the end of the stream, or the end of the tuple
					// => since we already tested Input.HasMore, we know we are in the later case
					if (token.Equals(default))
					{
						//note: ParseNext() has already eaten the <00>
						--reader.Depth;
						int end = reader.Cursor;
						token = new Range(start, end);
						error = null;
						return true;
					}
					// else: ignore this token, it will be processed later if the tuple is unpacked and accessed
				}
			}

			token = default;
			error = new FormatException($"Truncated embedded tuple started at index {start}/{reader.Input.Length}");
			return false;
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
				if (!reader.HasMore) return false;
				if (!TryParseNext(ref reader, out _, out var error))
				{
					if (error != null) throw error;
					return false;
				}
			}
			return true;
		}

#if NET9_0_OR_GREATER

		/// <summary>Visit the different tokens of a packed tuple</summary>
		/// <param name="reader">Reader positioned at the start of a packed tuple</param>
		/// <param name="visitor">Lambda called for each segment of a tuple. Returns true to continue parsing, or false to stop</param>
		/// <returns>Number of tokens that have been visited until either <paramref name="visitor"/> returned false, or <paramref name="reader"/> reached the end.</returns>
		public static T VisitNext<T>(ref TupleReader reader, Func<ReadOnlySpan<byte>, TupleSegmentType, T> visitor)
		{
			if (!reader.HasMore) throw new InvalidOperationException("The reader has already reached the end");
			if (!TryParseNext(ref reader, out var token, out var error))
			{
				if (error != null) throw error;
			}

			var slice = reader.Input[token];
			return visitor(slice, TupleTypes.DecodeSegmentType(slice));
		}

#endif

		[Pure, MethodImpl(MethodImplOptions.NoInlining)]
		internal static Exception FailLegacyTupleNotSupported()
		{
			return new FormatException("Old style embedded tuples (0x03) are not supported anymore.");
		}

		#endregion

		#region Bits Twiddling...

#if !NET6_0_OR_GREATER
		/// <summary>Lookup table used to compute the index of the most significant bit</summary>
		private static readonly int[] MultiplyDeBruijnBitPosition =
		[
			0, 9, 1, 10, 13, 21, 2, 29, 11, 14, 16, 18, 22, 25, 3, 30,
			8, 12, 20, 28, 15, 17, 24, 7, 19, 27, 23, 6, 26, 5, 4, 31
		];
#endif

		/// <summary>Returns the minimum number of bytes needed to represent a value</summary>
		/// <remarks>Note: will return 1 even for <param name="v"/> == 0</remarks>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static int NumberOfBytes(long v)
		{
			return v >= 0 ? NumberOfBytes((ulong) v) : v != long.MinValue ? NumberOfBytes((ulong) - v) : 8;
		}

		/// <summary>Returns the minimum number of bytes needed to represent a value</summary>
		/// <returns>Note: will return 1 even for <param name="v"/> == 0</returns>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static int NumberOfBytes(ulong v)
		{
#if NET6_0_OR_GREATER
			return (BitOperations.Log2(v) + 8) >> 3;
#else
			int msb = 0;

			if (v > 0xFFFFFFFF)
			{ // for 64-bit values, shift everything by 32 bits to the right
				msb += 32;
				v >>= 32;
			}
			msb += MostSignificantBit((uint) v);
			return (msb + 8) >> 3;
#endif
		}

#if !NET6_0_OR_GREATER
		/// <summary>Returns the position of the most significant bit (0-based) in a 32-bit integer</summary>
		/// <param name="v">32-bit integer</param>
		/// <returns>Index of the most significant bit (0-based)</returns>
		private static int MostSignificantBit(uint v)
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
#endif

		#endregion

	}

}
