#region BSD Licence
/* Copyright (c) 2013-2014, Doxense SAS
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

namespace FoundationDB.Client
{
	using FoundationDB.Client.Utils;
	using JetBrains.Annotations;
	using System;
	using System.Diagnostics;
	using System.Runtime.CompilerServices;

	/// <summary>Slice buffer that emulates a pseudo-stream using a byte array that will automatically grow in size, if necessary</summary>
	/// <remarks>IMPORTANT: This struct does not extensively check the parameters! The caller should ensure that everything is valid (this is to get the max performance when serializing keys and values)</remarks>
	[DebuggerDisplay("Position={Position}, Capacity={Buffer == null ? -1 : Buffer.Length}"), DebuggerTypeProxy(typeof(SliceWriter.DebugView))]
	public struct SliceWriter
	{
		// Invariant
		// * Valid data always start at offset 0
		// * 'this.Position' is equal to the current size as well as the offset of the next available free spot
		// * 'this.Buffer' is either null (meaning newly created stream), or is at least as big as this.Position

		#region Private Members...

		/// <summary>Buffer holding the data</summary>
		public byte[] Buffer;

		/// <summary>Position in the buffer ( == number of already written bytes)</summary>
		public int Position;

		#endregion

		#region Constructors...

		/// <summary>Returns a new, empty, slice writer</summary>
		public static SliceWriter Empty { get { return default(SliceWriter); } }

		/// <summary>Create a new empty binary buffer with an initial allocated size</summary>
		/// <param name="capacity">Initial capacity of the buffer</param>
		public SliceWriter(int capacity)
		{
			if (capacity < 0) throw new ArgumentOutOfRangeException("capacity");

			this.Buffer = new byte[capacity];
			this.Position = 0;
		}

		/// <summary>Create a new binary writer using an existing buffer</summary>
		/// <param name="buffer">Initial buffer</param>
		/// <remarks>Since the content of the <paramref name="buffer"/> will be modified, only a temporary or scratch buffer should be used. If the writer needs to grow, a new buffer will be allocated.</remarks>
		public SliceWriter([NotNull] byte[] buffer)
			: this(buffer, 0)
		{ }

		/// <summary>Create a new binary buffer using an existing buffer and with the cursor to a specific location</summary>
		/// <remarks>Since the content of the <paramref name="buffer"/> will be modified, only a temporary or scratch buffer should be used. If the writer needs to grow, a new buffer will be allocated.</remarks>
		public SliceWriter([NotNull] byte[] buffer, int index)
		{
			if (buffer == null) throw new ArgumentNullException("buffer");
			if (index < 0 || index > buffer.Length) throw new ArgumentOutOfRangeException("index");

			this.Buffer = buffer;
			this.Position = index;
		}

		/// <summary>Creates a new binary buffer, initialized by copying pre-existing data</summary>
		/// <param name="prefix">Data that will be copied at the start of the buffer</param>
		/// <param name="capacity">Optional initial capacity of the buffer</param>
		/// <remarks>The cursor will already be placed at the end of the prefix</remarks>
		public SliceWriter(Slice prefix, int capacity = 0)
		{
			if (capacity < 0) throw new ArgumentException("Capacity must be a positive integer.", "capacity");

			int n = prefix.Count;
			Contract.Assert(n >= 0);

			if (capacity == 0)
			{ // most frequent usage is to add a packed integer at the end of a prefix
				capacity = SliceHelpers.Align(n + 8);
			}
			else
			{
				capacity = Math.Max(capacity, n);
			}

			var buffer = new byte[capacity];
			if (n > 0) prefix.CopyTo(buffer, 0);

			this.Buffer = buffer;
			this.Position = n;
		}

		#endregion

		#region Public Properties...

		/// <summary>Returns true if the buffer contains at least some data</summary>
		public bool HasData
		{
			get { return this.Position > 0; }
		}

		/// <summary>Return the byte at the specified index</summary>
		/// <param name="index">Index in the buffer (0-based if positive, from the end if negative)</param>
		public byte this[int index]
		{
			[Pure]
			get
			{
				Contract.Assert(this.Buffer != null && this.Position >= 0);
				//note: we will get bound checking for free in release builds
				if (index < 0) index += this.Position;
				if (index < 0 || index >= this.Position) throw new IndexOutOfRangeException();
				return this.Buffer[index];
			}
		}

		/// <summary>Returns a slice pointing to a segment inside the buffer</summary>
		/// <param name="beginInclusive">The starting position of the substring. Positive values means from the start, negative values means from the end</param>
		/// <param name="endExclusive">The end position (excluded) of the substring. Positive values means from the start, negative values means from the end</param>
		/// <returns>Slice that corresponds to the section selected. If the <paramref name="beginInclusive"/> if equal to or greater than <paramref name="endExclusive"/> then an empty Slice is returned</returns>
		/// <exception cref="ArgumentOutOfRangeException">If either <paramref name="beginInclusive"/> or <paramref name="endExclusive"/> is outside of the currently allocated buffer.</exception>
		public Slice this[int? beginInclusive, int? endExclusive]
		{
			[Pure]
			get
			{
				int from = beginInclusive ?? 0;
				int until = endExclusive ?? this.Position;

				// remap negative indexes
				if (from < 0) from += this.Position;
				if (until < 0) until += this.Position;

				// bound check
				if (from < 0 || from >= this.Position) throw new ArgumentOutOfRangeException("beginInclusive", "The start index must be inside the bounds of the buffer.");
				if (until < 0 || until > this.Position) throw new ArgumentOutOfRangeException("endExclusive", "The end index must be inside the bounds of the buffer.");

				// chop chop
				int count = until - from;
				return count > 0 ? new Slice(this.Buffer, from, count) : Slice.Empty;
			}
		}

		#endregion

		/// <summary>Returns a byte array filled with the contents of the buffer</summary>
		/// <remarks>The buffer is copied in the byte array. And change to one will not impact the other</remarks>
		[Pure, NotNull]
		public byte[] GetBytes()
		{
			Contract.Requires(this.Position >= 0);

			var bytes = new byte[this.Position];
			if (this.Position > 0)
			{
				Contract.Assert(this.Buffer != null && this.Buffer.Length >= this.Position);
				SliceHelpers.CopyBytesUnsafe(bytes, 0, this.Buffer, 0, bytes.Length);
			}
			return bytes;
		}

		/// <summary>Returns a slice pointing to the content of the buffer</summary>
		/// <remarks>Any change to the slice will change the buffer !</remarks>
		[Pure]
		public Slice ToSlice()
		{
			if (this.Buffer == null || this.Position == 0)
			{
				return Slice.Empty;
			}
			else
			{
				Contract.Assert(this.Buffer.Length >= this.Position);
				return new Slice(this.Buffer, 0, this.Position);
			}
		}

		/// <summary>Returns a slice pointing to the first <paramref name="count"/> bytes of the buffer</summary>
		/// <param name="count">Size of the segment</param>
		/// <remarks>Any change to the slice will change the buffer !</remarks>
		/// <exception cref="ArgumentException">If <paramref name="count"/> is less than zero, or larger than the current buffer size</exception>
		[Pure]
		public Slice ToSlice(int count)
		{
			if (count < 0 || count > this.Position) throw new ArgumentException("count");

			return count > 0 ? new Slice(this.Buffer, 0, count) : Slice.Empty;
		}

		/// <summary>Returns a slice pointing to a segment inside the buffer</summary>
		/// <param name="offset">Offset of the segment from the start of the buffer</param>
		/// <remarks>Any change to the slice will change the buffer !</remarks>
		/// <exception cref="ArgumentException">If <paramref name="offset"/> is less then zero, or after the current position</exception>
		[Pure]
		public Slice Substring(int offset)
		{
			if (offset < 0 || offset > this.Position) throw new ArgumentException("Offset must be inside the buffer", "offset");

			int count = this.Position - offset;
			return count > 0 ? new Slice(this.Buffer, offset, this.Position - offset) : Slice.Empty;
		}

		/// <summary>Returns a slice pointing to a segment inside the buffer</summary>
		/// <param name="offset">Offset of the segment from the start of the buffer</param>
		/// <param name="count">Size of the segment</param>
		/// <remarks>Any change to the slice will change the buffer !</remarks>
		/// <exception cref="ArgumentException">If either <paramref name="offset"/> or <paramref name="count"/> are less then zero, or do not fit inside the current buffer</exception>
		[Pure]
		public Slice Substring(int offset, int count)
		{
			if (offset < 0 || offset >= this.Position) throw new ArgumentException("Offset must be inside the buffer", "offset");
			if (count < 0 || offset + count > this.Position) throw new ArgumentException("The buffer is too small", "count");

			return count > 0 ? new Slice(this.Buffer, offset, count) : Slice.Empty;
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
				//TODO: native memset() ?
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
			if (bytes == 0) return this.Position;
			if (bytes < 0) throw new ArgumentOutOfRangeException("bytes");

			if (bytes < this.Position)
			{ // copy the left over data to the start of the buffer
				int remaining = this.Position - bytes;
				SliceHelpers.CopyBytesUnsafe(this.Buffer, 0, this.Buffer, bytes, remaining);
				this.Position = remaining;
				return remaining;
			}
			else
			{
				//REVIEW: should we throw if there are less bytes in the buffer than we want to flush ?
				this.Position = 0;
				return 0;
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
				if (this.Buffer.Length > 4096 && (this.Position << 3) <= Buffer.Length)
				{ // Shrink it
					Buffer = new byte[SliceHelpers.NextPowerOfTwo(this.Position)];
				}
				else
				{ // Clear it
					//TODO: native memset() ?
					Array.Clear(Buffer, 0, this.Position);
				}
				this.Position = 0;
			}
		}

		/// <summary>Advance the cursor of the buffer without writing anything, and return the previous position</summary>
		/// <param name="skip">Number of bytes to skip</param>
		/// <param name="pad">Pad value (0xFF by default)</param>
		/// <returns>Position of the cursor BEFORE moving it. Can be used as a marker to go back later and fill some value</returns>
		/// <remarks>Will fill the skipped bytes with <paramref name="pad"/></remarks>
		public int Skip(int skip, byte pad = 0xFF)
		{
			Contract.Requires(skip > 0);

			EnsureBytes(skip);
			var buffer = this.Buffer;
			int p = this.Position;
			for (int i = 0; i < skip; i++)
			{
				buffer[p + i] = pad;
			}
			this.Position = p + skip;
			return p;
		}

		/// <summary>Rewinds the cursor to a previous position in the buffer, while saving the current position</summary>
		/// <param name="cursor">Will receive the current cursor position</param>
		/// <param name="position">Previous position in the buffer</param>
		public void Rewind(out int cursor, int position)
		{
			Contract.Requires(position >= 0 && position <= this.Position);
			cursor = this.Position;
			this.Position = position;
		}

		/// <summary>Add a byte to the end of the buffer, and advance the cursor</summary>
		/// <param name="value">Byte, 8 bits</param>
#if !NET_4_0
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
#endif
		public void WriteByte(byte value)
		{
			EnsureBytes(1);
			this.Buffer[this.Position] = value;
			++this.Position;
		}

#if !NET_4_0
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
#endif
		internal void UnsafeWriteByte(byte value)
		{
			Contract.Requires(this.Buffer != null && this.Position < this.Buffer.Length);
			this.Buffer[this.Position++] = value;
		}

#if !NET_4_0
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
#endif
		internal void WriteByte2(byte value1, byte value2)
		{
			EnsureBytes(2);

			int p = this.Position;
			this.Buffer[p] = value1;
			this.Buffer[p + 1] = value2;
			this.Position = p + 2;
		}

#if !NET_4_0
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
#endif
		internal void UnsafeWriteByte2(byte value1, byte value2)
		{
			Contract.Requires(this.Buffer != null && this.Position + 1 < this.Buffer.Length);
			int p = this.Position;
			this.Buffer[p] = value1;
			this.Buffer[p + 1] = value2;
			this.Position = p + 2;
		}

#if !NET_4_0
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
#endif
		internal void WriteByte3(byte value1, byte value2, byte value3)
		{
			EnsureBytes(3);

			var buffer = this.Buffer;
			int p = this.Position;
			buffer[p] = value1;
			buffer[p + 1] = value2;
			buffer[p + 2] = value3;
			this.Position = p + 3;
		}

#if !NET_4_0
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
#endif
		internal void UnsafeWriteByte3(byte value1, byte value2, byte value3)
		{
			Contract.Requires(this.Buffer != null && this.Position + 2 < this.Buffer.Length);
			var buffer = this.Buffer;
			int p = this.Position;
			buffer[p] = value1;
			buffer[p + 1] = value2;
			buffer[p + 2] = value3;
			this.Position = p + 3;
		}

#if !NET_4_0
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
#endif
		internal void WriteByte4(byte value1, byte value2, byte value3, byte value4)
		{
			EnsureBytes(4);

			var buffer = this.Buffer;
			int p = this.Position;
			buffer[p] = value1;
			buffer[p + 1] = value2;
			buffer[p + 2] = value3;
			buffer[p + 3] = value4;
			this.Position = p + 4;
		}

#if !NET_4_0
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
#endif
		internal void UnsafeWriteByte4(byte value1, byte value2, byte value3, byte value4)
		{
			Contract.Requires(this.Buffer != null && this.Position + 3 < this.Buffer.Length);
			var buffer = this.Buffer;
			int p = this.Position;
			buffer[p] = value1;
			buffer[p + 1] = value2;
			buffer[p + 2] = value3;
			buffer[p + 3] = value4;
			this.Position = p + 4;
		}

#if !NET_4_0
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
#endif
		internal void WriteByte5(byte value1, byte value2, byte value3, byte value4, byte value5)
		{
			EnsureBytes(5);

			var buffer = this.Buffer;
			int p = this.Position;
			buffer[p] = value1;
			buffer[p + 1] = value2;
			buffer[p + 2] = value3;
			buffer[p + 3] = value4;
			buffer[p + 4] = value5;
			this.Position = p + 5;
		}

#if !NET_4_0
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
#endif
		internal void UnsafeWriteByte5(byte value1, byte value2, byte value3, byte value4, byte value5)
		{
			Contract.Requires(this.Buffer != null && this.Position + 4 < this.Buffer.Length);
			var buffer = this.Buffer;
			int p = this.Position;
			buffer[p] = value1;
			buffer[p + 1] = value2;
			buffer[p + 2] = value3;
			buffer[p + 3] = value4;
			buffer[p + 4] = value5;
			this.Position = p + 5;
		}

		/// <summary>Append a byte array to the end of the buffer</summary>
		/// <param name="data"></param>
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
			SliceHelpers.EnsureBufferIsValid(data, offset, count);

			if (count > 0)
			{
				EnsureBytes(count);
				SliceHelpers.CopyBytesUnsafe(this.Buffer, this.Position, data, offset, count);
				this.Position += count;
			}
		}

		/// <summary>Append a chunk of memory to the end of the buffer</summary>
		public unsafe void WriteBytesUnsafe(byte* data, int count)
		{
			if (data == null) throw new ArgumentNullException("data");
			if (count < 0) throw new ArgumentOutOfRangeException("count");

			if (count > 0)
			{
				EnsureBytes(count);
				SliceHelpers.CopyBytesUnsafe(this.Buffer, this.Position, data, count);
				this.Position += count;
			}
		}

		internal void UnsafeWriteBytes(byte[] data, int offset, int count)
		{
			Contract.Requires(this.Buffer != null && this.Position >= 0 && data != null && count >= 0 && this.Position + count <= this.Buffer.Length && offset >= 0 && offset + count <= data.Length);

			if (count > 0)
			{
				SliceHelpers.CopyBytesUnsafe(this.Buffer, this.Position, data, offset, count);
				this.Position += count;
			}
		}

		/// <summary>Append a segment of bytes to the end of the buffer</summary>
		public void WriteBytes(Slice data)
		{
			SliceHelpers.EnsureSliceIsValid(ref data);

			int n = data.Count;
			if (n > 0)
			{
				EnsureBytes(n);
				SliceHelpers.CopyBytesUnsafe(this.Buffer, this.Position, data.Array, data.Offset, n);
				this.Position += n;
			}
		}

		internal unsafe void WriteBytes(byte* data, int count)
		{
			if (count == 0) return;
			if (data == null) throw new ArgumentNullException("data");
			if (count < 0) throw new ArgumentException("count");

			EnsureBytes(count);
			Contract.Assert(this.Buffer != null && this.Position >= 0 && this.Position + count <= this.Buffer.Length);

			SliceHelpers.CopyBytesUnsafe(this.Buffer, this.Position, data, count);
			this.Position += count;
		}

		internal unsafe void UnsafeWriteBytes(byte* data, int count)
		{
			if (count <= 0) return;

			Contract.Requires(this.Buffer != null && this.Position >= 0 && data != null && count >= 0 && this.Position + count <= this.Buffer.Length);

			SliceHelpers.CopyBytesUnsafe(this.Buffer, this.Position, data, count);
			this.Position += count;
		}

		#region Fixed, Little-Endian

		/// <summary>Writes a 16-bit unsigned integer, using little-endian encoding</summary>
		/// <remarks>Advances the cursor by 2 bytes</remarks>
		public void WriteFixed16(uint value)
		{
			EnsureBytes(2);
			var buffer = this.Buffer;
			int p = this.Position;
			buffer[p] = (byte)value;
			buffer[p + 1] = (byte)(value >> 8);
			this.Position = p + 2;
		}

		/// <summary>Writes a 32-bit unsigned integer, using little-endian encoding</summary>
		/// <remarks>Advances the cursor by 4 bytes</remarks>
		public void WriteFixed32(uint value)
		{
			EnsureBytes(4);
			var buffer = this.Buffer;
			int p = this.Position;
			buffer[p] = (byte)value;
			buffer[p + 1] = (byte)(value >> 8);
			buffer[p + 2] = (byte)(value >> 16);
			buffer[p + 3] = (byte)(value >> 24);
			this.Position = p + 4;
		}

		/// <summary>Writes a 64-bit unsigned integer, using little-endian encoding</summary>
		/// <remarks>Advances the cursor by 8 bytes</remarks>
		public void WriteFixed64(ulong value)
		{
			EnsureBytes(8);
			var buffer = this.Buffer;
			int p = this.Position;
			buffer[p] = (byte)value;
			buffer[p + 1] = (byte)(value >> 8);
			buffer[p + 2] = (byte)(value >> 16);
			buffer[p + 3] = (byte)(value >> 24);
			buffer[p + 4] = (byte)(value >> 32);
			buffer[p + 5] = (byte)(value >> 40);
			buffer[p + 6] = (byte)(value >> 48);
			buffer[p + 7] = (byte)(value >> 56);
			this.Position = p + 8;
		}

		#endregion

		#region Fixed, Big-Endian

		/// <summary>Writes a 16-bit unsigned integer, using big-endian encoding</summary>
		/// <remarks>Advances the cursor by 2 bytes</remarks>
		public void WriteFixed16BE(uint value)
		{
			EnsureBytes(2);
			var buffer = this.Buffer;
			int p = this.Position;
			buffer[p] = (byte)(value >> 8);
			buffer[p + 1] = (byte)value;
			this.Position = p + 2;
		}

		/// <summary>Writes a 32-bit unsigned integer, using big-endian encoding</summary>
		/// <remarks>Advances the cursor by 4 bytes</remarks>
		public void WriteFixed32BE(uint value)
		{
			EnsureBytes(4);
			var buffer = this.Buffer;
			int p = this.Position;
			buffer[p] = (byte)(value >> 24);
			buffer[p + 1] = (byte)(value >> 16);
			buffer[p + 2] = (byte)(value >> 8);
			buffer[p + 3] = (byte)(value);
			this.Position = p + 4;
		}

		/// <summary>Writes a 64-bit unsigned integer, using big-endian encoding</summary>
		/// <remarks>Advances the cursor by 8 bytes</remarks>
		public void WriteFixed64BE(ulong value)
		{
			EnsureBytes(8);
			var buffer = this.Buffer;
			int p = this.Position;
			buffer[p] = (byte)(value >> 56);
			buffer[p + 1] = (byte)(value >> 48);
			buffer[p + 2] = (byte)(value >> 40);
			buffer[p + 3] = (byte)(value >> 32);
			buffer[p + 4] = (byte)(value >> 24);
			buffer[p + 5] = (byte)(value >> 16);
			buffer[p + 6] = (byte)(value >> 8);
			buffer[p + 7] = (byte)(value);
			this.Position = p + 8;
		}

		#endregion

		#region Variable size

		/// <summary>Writes a 7-bit encoded unsigned int (aka 'Varint16') at the end, and advances the cursor</summary>
		public void WriteVarint16(ushort value)
		{
			const uint MASK = 128;

			if (value < (1 << 7))
			{
				WriteByte((byte)value);
			}
			else if (value < (1 << 14))
			{
				WriteByte2(
					(byte)(value | MASK),
					(byte)(value >> 7)
				);
			}
			else
			{
				WriteByte3(
					(byte)(value | MASK),
					(byte)((value >> 7) | MASK),
					(byte)(value >> 14)
				);
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
				WriteByte2(
					(byte)(value | MASK),
					(byte)(value >> 7)
				);
			}
			else if (value < (1 << 21))
			{
				WriteByte3(
					(byte)(value | MASK),
					(byte)((value >> 7) | MASK),
					(byte)(value >> 14)
				);
			}
			else if (value < (1 << 28))
			{
				WriteByte4(
					(byte)(value | MASK),
					(byte)((value >> 7) | MASK),
					(byte)((value >> 14) | MASK),
					(byte)(value >> 21)
				);
			}
			else
			{
				WriteByte5(
					(byte)(value | MASK),
					(byte)((value >> 7) | MASK),
					(byte)((value >> 14) | MASK),
					(byte)((value >> 21) | MASK),
					(byte)(value >> 28)
				);
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

		/// <summary>Writes a length-prefixed byte array, and advances the cursor</summary>
		public void WriteVarbytes(Slice value)
		{
			//REVIEW: what should we do for Slice.Nil ?

			SliceHelpers.EnsureSliceIsValid(ref value);
			int n = value.Count;
			if (n < 128)
			{
				EnsureBytes(n + 1);
				var buffer = this.Buffer;
				int p = this.Position;
				// write the count (single byte)
				buffer[p] = (byte)n;
				// write the bytes
				if (n > 0)
				{
					SliceHelpers.CopyBytesUnsafe(buffer, p + 1, value.Array, value.Offset, n);
				}
				this.Position = p + n + 1;
			}
			else
			{
				// write the count
				WriteVarint32((uint)value.Count);
				// write the bytes
				SliceHelpers.CopyBytesUnsafe(this.Buffer, this.Position, value.Array, value.Offset, n);
				this.Position += n;
			}
		}

		#endregion

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
				GrowBuffer(ref Buffer, Position + count);
			}
			Contract.Ensures(this.Buffer != null && this.Buffer.Length >= this.Position + count);
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
		/// <param name="buffer">Reference to the variable holding the buffer to create/resize. If null, a new buffer will be allocated. If not, the content of the buffer will be copied into the new buffer.</param>
		/// <param name="minimumCapacity">Mininum guaranteed buffer size after resizing.</param>
		/// <remarks>The buffer will be resized to the maximum betweeb the previous size multiplied by 2, and <paramref name="minimumCapacity"/>. The capacity will always be rounded to a multiple of 16 to reduce memory fragmentation</remarks>
		public static void GrowBuffer(ref byte[] buffer, int minimumCapacity = 0)
		{
			Contract.Requires(minimumCapacity >= 0);

			// double the size of the buffer, or use the minimum required
			long newSize = Math.Max(buffer == null ? 0 : (((long)buffer.Length) << 1), minimumCapacity);

			// .NET (as of 4.5) cannot allocate an array with more than 2^31 - 1 items...
			if (newSize > 0x7fffffffL) FailCannotGrowBuffer();

			// round up to 16 bytes, to reduce fragmentation
			int size = SliceHelpers.Align((int)newSize);

			Array.Resize(ref buffer, size);
		}

		[ContractAnnotation("=> halt")]
		private static void FailCannotGrowBuffer()
		{
#if DEBUG
			// If you breakpoint here, that means that you probably have an uncheked maximum buffer size, or a runaway while(..) { append(..) } code in your layer code !
			// => you should ALWAYS ensure a reasonable maximum size of your allocations !
			if (Debugger.IsAttached) Debugger.Break();
#endif
			// note: some methods in the BCL do throw an OutOfMemoryException when attempting to allocated more than 2^31
			throw new OutOfMemoryException("Buffer cannot be resized, because it would exceed the maximum allowed size");
		}

		[UsedImplicitly(ImplicitUseTargetFlags.WithMembers)]
		private sealed class DebugView
		{
			private readonly SliceWriter m_writer;

			public DebugView(SliceWriter writer)
			{
				m_writer = writer;
			}

			public byte[] Data
			{
				get
				{
					if (m_writer.Buffer.Length == m_writer.Position) return m_writer.Buffer;
					var tmp = new byte[m_writer.Position];
					System.Array.Copy(m_writer.Buffer, tmp, tmp.Length);
					return tmp;
				}
			}

			public int Position
			{
				get { return m_writer.Position; }
			}

		}

	}

}
