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

namespace FoundationDb.Client.Utils
{
	using FoundationDb.Layers.Tuples;
	using System;
	using System.Diagnostics;
	using System.Runtime.CompilerServices;
	using System.Text;

	/// <summary>Helper class that emulates a pseudo-stream using a byte buffer that will automatically grow in size, if necessary</summary>
	/// <remarks>IMPORTANT: This class does not extensively check the parameters! The caller must ensure that everything is valid (this is to get the max performance when serializing keys and values)</remarks>
	[DebuggerDisplay("Position={this.Position}, Capacity={this.Buffer == null ? -1 : this.Buffer.Length}")]
	public sealed class FdbBufferWriter
	{
		// Invariant
		// * Valid data always start at offset 0
		// * 'this.Position' is equal to the current size as well as the offset of the next available free spot
		// * 'this.Buffer' is either null (meaning newly created stream), or is at least as big as this.Position

		#region Constants...

		/// <summary>Minimum size of buffer</summary>
		private const int MIN_SIZE = 32;

		/// <summary>Empty buffer</summary>
		internal static readonly byte[] Empty = new byte[0];

		#endregion

		#region Private Members...

		/// <summary>Buffer holding the data</summary>
		public byte[] Buffer;

		/// <summary>Position in the buffer ( == number of already written bytes)</summary>
		public int Position;

		#endregion

		#region Constructors...

		public FdbBufferWriter()
		{ }

		public FdbBufferWriter(int capacity)
		{
			this.Buffer = new byte[capacity];
		}

		public FdbBufferWriter(byte[] buffer)
		{
			this.Buffer = buffer;
		}

		public FdbBufferWriter(byte[] buffer, int index)
		{
			this.Buffer = buffer;
			this.Position = index;
		}

		#endregion

		#region Public Properties...

		/// <summary>Returns true is the buffer contains at least some data</summary>
		public bool HasData
		{
#if !NET_4_0
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
#endif
			get { return this.Position > 0; }
		}

		#endregion
		
		/// <summary>Returns a byte array filled with the contents of the buffer</summary>
		/// <remarks>The buffer is copied in the byte array. And change to one will not impact the other</remarks>
		public byte[] GetBytes()
		{
			var bytes = new byte[this.Position];
			if (this.Position > 0)
			{
				System.Buffer.BlockCopy(this.Buffer, 0, bytes, 0, this.Position);
			}
			return bytes;
		}

		/// <summary>Returns a slice pointing to the content of the buffer</summary>
		/// <remarks>Any change to the slice will change the buffer !</remarks>
#if !NET_4_0
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
#endif
		public Slice ToSlice()
		{
			return new Slice(this.Buffer ?? Empty, 0, this.Position);
		}

		/// <summary>Returns a slice pointing to the first <paramref name="count"/> bytes of the buffer</summary>
		/// <param name="count">Size of the segment</param>
		/// <remarks>Any change to the slice will change the buffer !</remarks>
#if !NET_4_0
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
#endif
		public Slice ToSlice(int count)
		{
			if (count < 0 || count > this.Position) throw new ArgumentNullException("count");

			Contract.Requires(count >= 0 && count <= this.Position);
			return new Slice(this.Buffer, 0, count);
		}

		/// <summary>Returns a slice pointing to a segment inside the buffer</summary>
		/// <param name="offset">Offset of the segment from the start of the buffer</param>
		/// <param name="count">Size of the segment</param>
		/// <remarks>Any change to the slice will change the buffer !</remarks>
#if !NET_4_0
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
#endif
		public Slice ToSlice(int offset, int count)
		{
			if (offset < 0 || offset >= this.Position) throw new ArgumentException("offset");
			if (count < 0 || offset + count > this.Position) throw new ArgumentException("count");

			return new Slice(this.Buffer, offset, count);
		}

		/// <summary>Truncate the buffer by setting the cursor to the specified position.</summary>
		/// <param name="position">New size of the buffer</param>
		/// <remarks>If the buffer was smaller, it will be resized and filled with zeroes. If it was biffer, the cursor will be set to the specified position, but previous data will not be deleted.</remarks>
		public void SetLength(int position)
		{
			Contract.Requires(position >= 0);

			if (this.Position < position)
			{
				int missing = position - this.Position;
				EnsureBytes(missing);
				Array.Clear(this.Buffer, this.Position, missing);
			}
			this.Position = position;
		}

		/// <summary>Delete the first N bytes of the buffer, and shift the remaining to the front</summary>
		/// <param name="bytes">Number of bytes to remove at the head of the buffer</param>
		/// <returns>New size of the buffer (or 0 if it is empty)</returns>
		/// <remarks>This should be called after every successfull write to the underlying stream, to update the buffer.</remarks>
		public int Flush(int bytes)
		{
			Contract.Requires(bytes > 0, null, "bytes > 0");
			Contract.Requires(bytes <= this.Position, null, "bytes <= this.Position");

			if (bytes < this.Position)
			{ // Il y aura des données à garder, on les copie au début du stream
				System.Buffer.BlockCopy(this.Buffer, bytes, this.Buffer, 0, this.Position - bytes);
				return this.Position -= bytes;
			}
			else
			{
				return this.Position = 0;
			}
		}

		/// <summary>Empties the current buffer after a succesfull write</summary>
		/// <remarks>Shrink the buffer if a lot of memory is wated</remarks>
		public void Reset()
		{
			if (this.Position > 0)
			{
				// reduce size ?
				// If the buffer exceeds 4K and we used less than 1/8 of it the last time, we will "shrink" the buffer
				if (this.Buffer.Length > 4096 && this.Position * 8 <= Buffer.Length)
				{ // Shrink it
					Buffer = new byte[NextPowerOfTwo(this.Position)];
				}
				else
				{ // Clear it
					Array.Clear(Buffer, 0, this.Position);
				}
				this.Position = 0;
			}
		}

		/// <summary>Advance the cursor of the buffer without writing anything</summary>
		/// <param name="skip">Number of bytes to skip</param>
		/// <returns>Position of the cursor BEFORE moving it. Can be used as a marker to go back later and fill some value</returns>
		/// <remarks>Will fill the skipped bytes with 0xFF</remarks>
		public int Skip(int skip)
		{
			Contract.Requires(skip > 0);

			int before = this.Position;
			EnsureBytes(skip);
			for (int i = 0; i < skip; i++)
			{
				this.Buffer[before + i] = 0xFF;
			}
			this.Position = before + skip;
			return before;
		}

		/// <summary>Add a byte to the end of the buffer, and advance the cursor</summary>
		/// <param name="value">Byte, 8 bits</param>
#if !NET_4_0
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
#endif
		public void WriteByte(byte value)
		{
			EnsureBytes(1);
			this.Buffer[this.Position++] = value;
		}

#if !NET_4_0
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
#endif
		internal void UnsafeWriteByte(byte value)
		{
			Contract.Requires(this.Buffer != null && this.Position < this.Buffer.Length);
			this.Buffer[this.Position++] = value;
		}

		/// <summary>Append a byte array to the end of the buffer</summary>
		/// <param name="data"></param>
#if !NET_4_0
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
#endif
		public void WriteBytes(byte[] data)
		{
			if (data != null)
			{
				WriteBytes(data, 0, data.Length);
			}
		}

		/// <summary>Append a chunk of a byte array to the end of the buffer</summary>
		/// <param name="data"></param>
		/// <param name="offset"></param>
		/// <param name="count"></param>
		public void WriteBytes(byte[] data, int offset, int count)
		{
			Contract.Requires(data != null);
			Contract.Requires(offset >= 0);
			Contract.Requires(count >= 0);
			Contract.Requires(offset + count <= data.Length);

			if (count > 0)
			{
				EnsureBytes(count);
				System.Buffer.BlockCopy(data, offset, this.Buffer, this.Position, count);
				this.Position += count;
			}
		}

		internal void UnsafeWriteBytes(byte[] data, int offset, int count)
		{
			Contract.Requires(this.Buffer != null && this.Position >= 0 && data != null && count >= 0 && this.Position + count <= this.Buffer.Length && offset >= 0 && offset + count <= data.Length);

			if (count > 0)
			{
				System.Buffer.BlockCopy(data, offset, this.Buffer, this.Position, count);
				this.Position += count;
			}
		}

		/// <summary>Append a segment of bytes to the end of the buffer</summary>
		/// <param name="data"></param>
		/// <param name="offset"></param>
		/// <param name="count"></param>
		public void WriteBytes(Slice data)
		{
			Contract.Requires(data.HasValue);
			Contract.Requires(data.Offset >= 0);
			Contract.Requires(data.Count >= 0);
			Contract.Requires(data.Offset + data.Count <= data.Array.Length);

			int n = data.Count;
			if (n > 0)
			{
				EnsureBytes(n);
				System.Buffer.BlockCopy(data.Array, data.Offset, this.Buffer, this.Position, n);
				this.Position += n;
			}
		}

		internal unsafe void WriteBytes(byte* data, int count)
		{
			Contract.Requires(data != null);
			Contract.Requires(count >= 0);

			if (count > 0)
			{
				EnsureBytes(count);
				System.Runtime.InteropServices.Marshal.Copy(new IntPtr(data), this.Buffer, this.Position, count);
				this.Position += count;
			}
		}

		internal unsafe void UnsafeWriteBytes(byte* data, int count)
		{
			Contract.Requires(this.Buffer != null && this.Position >= 0 && data != null && count >= 0 && this.Position + count <= this.Buffer.Length);

			if (count > 0)
			{
				System.Runtime.InteropServices.Marshal.Copy(new IntPtr(data), this.Buffer, this.Position, count);
				this.Position += count;
			}
		}

		/// <summary>Writes a 7-bit encoded unsigned int (aka 'Varint32') at the end, and advances the cursor</summary>
		public void WriteVarint32(uint value)
		{
			const uint MASK = 128;

			if (value < (1 << 7))
			{
				WriteByte((byte)value);
			}
			else if (value < (1 << 14))
			{
				EnsureBytes(2);
				UnsafeWriteByte((byte)(value | MASK));
				UnsafeWriteByte((byte)(value >> 7));
			}
			else if (value < (1 << 21))
			{
				EnsureBytes(2);
				UnsafeWriteByte((byte)(value | MASK));
				UnsafeWriteByte((byte)((value >> 7) | MASK));
				UnsafeWriteByte((byte)(value >> 14));
			}
			else if (value < (1 << 28))
			{
				EnsureBytes(2);
				UnsafeWriteByte((byte)(value | MASK));
				UnsafeWriteByte((byte)((value >> 7) | MASK));
				UnsafeWriteByte((byte)((value >> 14) | MASK));
				UnsafeWriteByte((byte)(value >> 21));
			}
			else
			{
				EnsureBytes(2);
				UnsafeWriteByte((byte)(value | MASK));
				UnsafeWriteByte((byte)((value >> 7) | MASK));
				UnsafeWriteByte((byte)((value >> 14) | MASK));
				UnsafeWriteByte((byte)((value >> 21) | MASK));
				UnsafeWriteByte((byte)(value >> 28));
			}
		}

		/// <summary>Writes a 7-bit encoded unsigned long (aka 'Varint64') at the end, and advances the cursor</summary>
		public void WriteVarint64(ulong value)
		{
			const uint MASK = 128;

			// max size is 5
			EnsureBytes(value < (1 << 7) ? 1 : value < (1 << 14) ? 2 : value < (1 << 21) ? 3 : 10);

			var buffer = this.Buffer;
			int p = this.Position;
			while (value >= MASK)
			{
				buffer[p++] = (byte)((value & (MASK - 1)) | MASK);
				value >>= 7;
			}

			buffer[p++] = (byte)value;
			this.Position = p;
		}

		/// <summary>Ensures that we can fit a specific amount of data at the end of the buffer</summary>
		/// <param name="count">Number of bytes that will be written</param>
		/// <remarks>If the buffer is too small, it will be resized</remarks>
#if !NET_4_0
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
#endif
		public void EnsureBytes(int count)
		{
			Contract.Requires(count >= 0);

			if (Buffer == null || Position + count > Buffer.Length)
			{
				// note: double la taille du buffer
				GrowBuffer(ref Buffer, Position + count);
			}
		}

		/// <summary>Ensures that we can fit data at a specifc offset in the buffer</summary>
		/// <param name="offset">Offset into the buffer (from the start)</param>
		/// <param name="count">Number of bytes that will be written at this offset</param>
		/// <remarks>If the buffer is too small, it will be resized</remarks>
#if !NET_4_0
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
#endif
		public void EnsureOffsetAndSize(int offset, int count)
		{
			Contract.Requires(offset >= 0);
			Contract.Requires(count >= 0);

			if (this.Buffer == null || offset + count > this.Buffer.Length)
			{
				GrowBuffer(ref this.Buffer, offset + count);
			}
		}

		/// <summary>Resize a buffer by doubling its capacity</summary>
		/// <param name="buffer">Reference to the variable holding the buffer to create/resize</param>
		/// <param name="minimumCapacity">Capacité minimum du buffer (si vide initialement) ou 0 pour "autogrowth"</param>
		/// <remarks>The buffer will be resized to the maximum betweeb the previous size multiplied by 2, and <paramref name="minimumCapacity"/>. The capacity will always be rounded to a multiple of 16 to reduce memory fragmentation</remarks>
		public static void GrowBuffer(ref byte[] buffer, int minimumCapacity = 0)
		{
			Contract.Requires(minimumCapacity >= 0);

			// essayes de doubler la taille du buffer, ou prendre le minimum demandé
			int newSize = buffer == null ? 0 : (buffer.Length << 1);
			if (newSize < minimumCapacity) newSize = minimumCapacity;

			// .NET (as of 4.5) cannot allocate an array with more then 2^31 - 1 items...
			if (newSize > 2147483647) FailCannotGrowBuffer();

			// round to the next multiple of 16 bytes (to reduce fragmentation)
			if (newSize < MIN_SIZE)
			{
				newSize = MIN_SIZE;
			}
			else if ((newSize & 0xF) != 0)
			{
				checked { newSize = (newSize + 0xF) & 0x7FFFFFF8; }
			}

			Array.Resize(ref buffer, newSize);
		}

		private static void FailCannotGrowBuffer()
		{
			//REVIEW: should we throw an OutOfMemoryException ? or ArgumentOutOfRangeException ?
			throw new InvalidOperationException("Buffer cannot be resize, because it would larger than the maximum allowed size");
		}

		/// <summary>Round a number to the next power of 2</summary>
		/// <param name="x">Positive integer that will be rounded up (if not already a power of 2)</param>
		/// <returns>Smallest power of 2 that is greater then or equal to <paramref name="x"/></returns>
		/// <remarks>Will return 1 for <paramref name="x"/> = 0 (because 0 is not a power 2 !), and will throws for <paramref name="x"/> &lt; 0</remarks>
		/// <exception cref="System.ArgumentOutOfRangeException">If <paramref name="x"/> is a negative number</exception>
		public static int NextPowerOfTwo(int x)
		{
			// cf http://en.wikipedia.org/wiki/Power_of_two#Algorithm_to_round_up_to_power_of_two

			// special case
			if (x == 0) return 1;
			if (x < 0) throw new ArgumentOutOfRangeException("x", x, "Cannot compute the next power of two for negative numbers");
			//TODO: check for overflow at if x > 2^30 ?

			--x;
			x |= (x >> 1);
			x |= (x >> 2);
			x |= (x >> 4);
			x |= (x >> 8);
			x |= (x >> 16);
			return x + 1;
		}

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
		/// <param name="value">32-bit integer</param>
		/// <returns>Index of the most significant bit (0-based)</returns>
#if !NET_4_0
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
#endif
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

	}

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
