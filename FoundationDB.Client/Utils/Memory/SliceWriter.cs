#region BSD Licence
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

//#define ENABLE_ARRAY_POOL
//#define ENABLE_SPAN

namespace Doxense.Memory
{
	using System;
	using System.Diagnostics;
	using System.Globalization;
	using System.Runtime.CompilerServices;
	using System.Text;
	using Doxense.Diagnostics.Contracts;
	using JetBrains.Annotations;
#if ENABLE_SPAN
	using System.Runtime.InteropServices;
#endif

	/// <summary>Slice buffer that emulates a pseudo-stream using a byte array that will automatically grow in size, if necessary</summary>
	/// <remarks>This struct MUST be passed by reference!</remarks>
	[PublicAPI, DebuggerDisplay("Position={Position}, Capacity={Capacity}"), DebuggerTypeProxy(typeof(SliceWriter.DebugView))]
	[DebuggerNonUserCode] //remove this when you need to troubleshoot this class!
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

		/// <summary>Create a new empty binary buffer with an initial allocated size</summary>
		/// <param name="capacity">Initial capacity of the buffer</param>
		public SliceWriter(int capacity)
		{
			Contract.Positive(capacity, nameof(capacity));

#if ENABLE_ARRAY_POOL
			this.Buffer = capacity == 0 ? Array.Empty<byte>() : ArrayPool<byte>.Shared.Rent(capacity);
#else
			this.Buffer = capacity == 0 ? Array.Empty<byte>() : new byte[capacity];
#endif
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
			Contract.NotNull(buffer, nameof(buffer));
			Contract.Between(index, 0, buffer.Length, nameof(index));

			this.Buffer = buffer;
			this.Position = index;
		}

		/// <summary>Creates a new binary buffer, initialized by copying pre-existing data</summary>
		/// <param name="prefix">Data that will be copied at the start of the buffer</param>
		/// <param name="capacity">Optional initial capacity of the buffer</param>
		/// <remarks>The cursor will already be placed at the end of the prefix</remarks>
		public SliceWriter(Slice prefix, int capacity = 0)
		{
			prefix.EnsureSliceIsValid();
			Contract.Positive(capacity, nameof(capacity));

			int n = prefix.Count;
			Contract.Assert(n >= 0);

			if (capacity == 0)
			{ // most frequent usage is to add a packed integer at the end of a prefix
				capacity = BitHelpers.AlignPowerOfTwo(n + 8, 16);
			}
			else
			{
				capacity = BitHelpers.AlignPowerOfTwo(Math.Max(capacity, n), 16);
			}

#if ENABLE_ARRAY_POOL
			var buffer = ArrayPool<byte>.Shared.Rent(capacity);
#else
			var buffer = new byte[capacity];
#endif
			if (n > 0) prefix.CopyTo(buffer, 0);

			this.Buffer = buffer;
			this.Position = n;
		}

		#endregion

		#region Public Properties...

		/// <summary>Returns true if the buffer contains at least some data</summary>
		public bool HasData => this.Position > 0;

		/// <summary>Capacity of the internal buffer</summary>
		public int Capacity => this.Buffer?.Length ?? 0;

		/// <summary>Return the byte at the specified index</summary>
		/// <param name="index">Index in the buffer (0-based if positive, from the end if negative)</param>
		public byte this[int index]
		{
			[Pure]
			get
			{
				int pos = this.Position;
				Contract.Assert(this.Buffer != null && pos >= 0);
				//note: we will get bound checking for free in release builds
				if (index < 0) index += pos;
				if ((uint) index >= pos) throw ThrowHelper.IndexOutOfRangeException();
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
				int pos = this.Position;
				int until = endExclusive ?? pos;

				// remap negative indexes
				if (from < 0) from += pos;
				if (until < 0) until += pos;

				// bound check
				if ((uint) from >= pos) throw ThrowHelper.ArgumentOutOfRangeException(nameof(beginInclusive), beginInclusive, "The start index must be inside the bounds of the buffer.");
				if ((uint) until > pos) throw ThrowHelper.ArgumentOutOfRangeException(nameof(endExclusive), endExclusive, "The end index must be inside the bounds of the buffer.");

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
			int p = this.Position;
			if (p == 0) return Array.Empty<byte>();

			var bytes = new byte[p];
			if (p > 0)
			{
				Contract.Assert(this.Buffer != null && this.Buffer.Length >= this.Position);
				UnsafeHelpers.CopyUnsafe(bytes, 0, this.Buffer, 0, bytes.Length);
			}
			return bytes;
		}

		/// <summary>Returns a buffer segment pointing to the content of the buffer</summary>
		/// <remarks>Any change to the segment will change the buffer !</remarks>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public ArraySegment<byte> ToArraySegment()
		{
			return ToSlice();
		}

		/// <summary>Returns a slice pointing to the content of the buffer</summary>
		/// <remarks>Any change to the slice will change the buffer !</remarks>
		[Pure]
		public Slice ToSlice()
		{
			var buffer = this.Buffer;
			var p = this.Position;
			if (buffer == null | p == 0)
			{ // empty buffer
				return Slice.Empty;
			}
			Contract.Assert(buffer.Length >= p, "Current position is outside of the buffer");
			return new Slice(buffer, 0, p);
		}

		/// <summary>Returns a slice pointing to the first <paramref name="count"/> bytes of the buffer</summary>
		/// <param name="count">Size of the segment to return.</param>
		/// <returns>Slice that contains the first <paramref name="count"/> bytes written to this buffer</returns>
		/// <remarks>Any change to the slice will change the buffer !</remarks>
		/// <example>
		/// ({HELLO WORLD}).Head(5) => {HELLO}
		/// ({HELLO WORLD}).Head(1) => {H}
		/// {{HELLO WORLD}).Head(0) => {}
		/// </example>
		/// <exception cref="ArgumentException">If <paramref name="count"/> is less than zero, or larger than the current buffer size</exception>
		[Pure]
		public Slice Head(int count)
		{
			if (count == 0) return Slice.Empty;
			if ((uint) count > this.Position) throw ThrowHelper.ArgumentOutOfRangeException(nameof(count), "Buffer is too small");
			return new Slice(this.Buffer, 0, count);
		}

		/// <summary>Returns a slice pointing to the first <paramref name="count"/> bytes of the buffer</summary>
		/// <param name="count">Size of the segment to return.</param>
		/// <returns>Slice that contains the first <paramref name="count"/> bytes written to this buffer</returns>
		/// <remarks>Any change to the slice will change the buffer !</remarks>
		/// <example>
		/// ({HELLO WORLD}).Head(5) => {HELLO}
		/// ({HELLO WORLD}).Head(1) => {H}
		/// {{HELLO WORLD}).Head(0) => {}
		/// </example>
		/// <exception cref="ArgumentException">If <paramref name="count"/> is less than zero, or larger than the current buffer size</exception>
		[Pure]
		public Slice Head(uint count)
		{
			if (count == 0) return Slice.Empty;
			if (count > this.Position) throw ThrowHelper.ArgumentOutOfRangeException(nameof(count), "Buffer is too small");
			return new Slice(this.Buffer, 0, (int) count);
		}

		/// <summary>Returns a slice pointer to the last <paramref name="count"/> bytes of the buffer</summary>
		/// <param name="count">Size of the segment to return.</param>
		/// <returns>Slice that contains the last <paramref name="count"/> bytes written to this buffer</returns>
		/// <remarks>Any change to the slice will change the buffer !</remarks>
		/// <example>
		/// ({HELLO WORLD}).Tail(5) => {WORLD}
		/// ({HELLO WORLD}).Tail(1) => {D}
		/// {{HELLO WORLD}).Tail(0) => {}
		/// </example>
		/// <exception cref="ArgumentException">If <paramref name="count"/> is less than zero, or larger than the current buffer size</exception>
		public Slice Tail(int count)
		{
			if (count == 0) return Slice.Empty;
			int p = this.Position;
			if ((uint) count > p) throw ThrowHelper.ArgumentOutOfRangeException(nameof(count), "Buffer is too small");
			return new Slice(this.Buffer, p - count, count);
		}

		/// <summary>Returns a slice pointer to the last <paramref name="count"/> bytes of the buffer</summary>
		/// <param name="count">Size of the segment to return.</param>
		/// <returns>Slice that contains the last <paramref name="count"/> bytes written to this buffer</returns>
		/// <remarks>Any change to the slice will change the buffer !</remarks>
		/// <example>
		/// ({HELLO WORLD}).Tail(5) => {WORLD}
		/// ({HELLO WORLD}).Tail(1) => {D}
		/// {{HELLO WORLD}).Tail(0) => {}
		/// </example>
		/// <exception cref="ArgumentException">If <paramref name="count"/> is less than zero, or larger than the current buffer size</exception>
		public Slice Tail(uint count)
		{
			if (count == 0) return Slice.Empty;
			int p = this.Position;
			if (count > p) throw ThrowHelper.ArgumentOutOfRangeException(nameof(count), "Buffer is too small");
			return new Slice(this.Buffer, p - (int) count, (int) count);
		}

		/// <summary>Returns a slice pointing to a segment inside the buffer</summary>
		/// <param name="offset">Offset of the segment from the start of the buffer</param>
		/// <remarks>Any change to the slice will change the buffer !</remarks>
		/// <exception cref="ArgumentException">If <paramref name="offset"/> is less then zero, or after the current position</exception>
		[Pure]
		public Slice Substring(int offset)
		{
			int p = this.Position;
			if (offset < 0 || offset > p) throw ThrowHelper.ArgumentException(nameof(offset), "Offset must be inside the buffer");
			int count = p - offset;
			return count > 0 ? new Slice(this.Buffer, offset, p - offset) : Slice.Empty;
		}

		/// <summary>Returns a slice pointing to a segment inside the buffer</summary>
		/// <param name="offset">Offset of the segment from the start of the buffer</param>
		/// <param name="count">Size of the segment</param>
		/// <remarks>Any change to the slice will change the buffer !</remarks>
		/// <exception cref="ArgumentException">If either <paramref name="offset"/> or <paramref name="count"/> are less then zero, or do not fit inside the current buffer</exception>
		[Pure]
		public Slice Substring(int offset, int count)
		{
			int p = this.Position;
			if ((uint) offset >= p) throw ThrowHelper.ArgumentException(nameof(offset), "Offset must be inside the buffer");
			if (count < 0 | offset + count > p) throw ThrowHelper.ArgumentException(nameof(count), "The buffer is too small");

			return count > 0 ? new Slice(this.Buffer, offset, count) : Slice.Empty;
		}

		/// <summary>Truncate the buffer by setting the cursor to the specified position.</summary>
		/// <param name="position">New size of the buffer</param>
		/// <remarks>If the buffer was smaller, it will be resized and filled with zeroes. If it was biffer, the cursor will be set to the specified position, but previous data will not be deleted.</remarks>
		public void SetLength(int position)
		{
			Contract.Requires(position >= 0);

			int p = this.Position;
			if (p < position)
			{
				int missing = position - p;
				var buffer = EnsureBytes(missing);
				//TODO: native memset() ?
				Array.Clear(buffer, p, missing);
			}
			this.Position = position;
		}

		/// <summary>Delete the first N bytes of the buffer, and shift the remaining to the front</summary>
		/// <param name="bytes">Number of bytes to remove at the head of the buffer</param>
		/// <returns>New size of the buffer (or 0 if it is empty)</returns>
		/// <remarks>This should be called after every successfull write to the underlying stream, to update the buffer.</remarks>
		public int Flush(int bytes) //REVIEW: plutot renommer en "RemoveHead"? ou faire un vrai "RemoveAt(offset, count)" ?
		{
			if (bytes == 0) return this.Position;
			if (bytes < 0) throw ThrowHelper.ArgumentOutOfRangeException(nameof(bytes));

			if (bytes < this.Position)
			{ // copy the left over data to the start of the buffer
				int remaining = this.Position - bytes;
				UnsafeHelpers.CopyUnsafe(this.Buffer, 0, this.Buffer, bytes, remaining);
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
		/// <param name="zeroes">If true, fill the existing buffer with zeroes, if it is reused, to ensure that no previous data can leak.</param>
		/// <remarks>If the current buffer is large enough, and less than 1/8th was used, then it will be discarded and a new smaller one will be allocated as needed</remarks>
		public void Reset(bool zeroes = false)
		{
			if (this.Position > 0)
			{
				Contract.Assert(this.Buffer != null && this.Buffer.Length >= this.Position);
				// reduce size ?
				// If the buffer exceeds 64K and we used less than 1/8 of it the last time, we will "shrink" the buffer
				if (this.Buffer.Length > 65536 && this.Position <= (this.Buffer.Length >> 3))
				{ // kill the buffer
					this.Buffer = null;
					//TODO: return to a central buffer pool?
				}
				else if (zeroes)
				{ // Clear it
					unsafe
					{
						fixed (byte* ptr = this.Buffer)
						{
							UnsafeHelpers.ClearUnsafe(ptr, checked((uint)this.Buffer.Length));
						}
					}
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
			Contract.Requires(skip >= 0);

			var buffer = EnsureBytes(skip);
			int p = this.Position;
			if (skip == 0) return p;
			if (skip <= 8)
			{
				for (int i = 0; i < skip; i++)
				{
					buffer[p + i] = pad;
				}
			}
			else
			{
				unsafe
				{
					fixed (byte* ptr = &buffer[p])
					{
						UnsafeHelpers.FillUnsafe(ptr, checked((uint) skip), pad);
					}
				}
			}
			this.Position = p + skip;
			return p;
		}

		/// <summary>Advance the cursor by the specified amount, and return the skipped over chunk (that can be filled later by the caller)</summary>
		/// <param name="count">Number of bytes to allocate</param>
		/// <param name="pad">Pad value (0xFF by default)</param>
		/// <returns>Slice that corresponds to the reserved segment in the buffer</returns>
		/// <remarks>Will fill the reserved segment with <paramref name="pad"/> and the cursor will be positionned immediately after the segment.</remarks>
		public Slice Allocate(int count, byte pad = 0xFF)
		{
			Contract.Positive(count, nameof(count));
			if (count == 0) return Slice.Empty;

			int offset = Skip(count, pad);
			return new Slice(this.Buffer, offset, count);
		}

		/// <summary>Advance the cursor by the amount required end up on an aligned byte position</summary>
		/// <param name="aligment">Number of bytes to align to</param>
		/// <param name="pad">Pad value (0 by default)</param>
		public void Align(int aligment, byte pad = 0)
		{
			Contract.Requires(aligment > 0);
			int r = this.Position % aligment;
			if (r > 0) Skip(aligment - r, pad);
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

		#region Bytes...

		/// <summary>Add a byte to the end of the buffer, and advance the cursor</summary>
		/// <param name="value">Byte, 8 bits</param>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void WriteByte(byte value)
		{
			var buffer = EnsureBytes(1);
			int p = this.Position;
			buffer[p] = value;
			this.Position = p + 1;
		}

		/// <summary>Add a byte to the end of the buffer, and advance the cursor</summary>
		/// <param name="value">Byte, 8 bits</param>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void WriteByte(int value)
		{
			var buffer = EnsureBytes(1);
			int p = this.Position;
			buffer[p] = (byte) value;
			this.Position = p + 1;
		}

		/// <summary>Add a byte to the end of the buffer, and advance the cursor</summary>
		/// <param name="value">Byte, 8 bits</param>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void WriteByte(sbyte value)
		{
			var buffer = EnsureBytes(1);
			int p = this.Position;
			buffer[p] = (byte) value;
			this.Position = p + 1;
		}

		/// <summary>Add a 1-byte boolean to the end of the buffer, and advance the cursor</summary>
		/// <param name="value">Boolean, encoded as either 0 or 1.</param>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void WriteByte(bool value)
		{
			var buffer = EnsureBytes(1);
			int p = this.Position;
			buffer[p] = value ? (byte) 1 : (byte) 0;
			this.Position = p + 1;
		}

		/// <summary>Dangerously write a sigle byte at the end of the buffer, without any capacity checks!</summary>
		/// <remarks>
		/// This method DOES NOT check the buffer capacity before writing, and caller MUST have resized the buffer beforehand!
		/// Failure to do so may introduce memory correction (buffer overflow!).
		/// This should ONLY be used in performance-sensitive code paths that have been audited thoroughly!
		/// </remarks>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void UnsafeWriteByte(byte value)
		{
			Contract.Requires(this.Buffer != null && this.Position < this.Buffer.Length);
			this.Buffer[this.Position++] = value;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void WriteBytes(byte value1, byte value2)
		{
			var buffer = EnsureBytes(2);
			int p = this.Position;
			buffer[p] = value1;
			buffer[p + 1] = value2;
			this.Position = p + 2;
		}

		/// <summary>Dangerously write two bytes at the end of the buffer, without any capacity checks!</summary>
		/// <remarks>
		/// This method DOES NOT check the buffer capacity before writing, and caller MUST have resized the buffer beforehand!
		/// Failure to do so may introduce memory correction (buffer overflow!).
		/// This should ONLY be used in performance-sensitive code paths that have been audited thoroughly!
		/// </remarks>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void UnsafeWriteBytes(byte value1, byte value2)
		{
			Contract.Requires(this.Buffer != null && this.Position + 1 < this.Buffer.Length);
			int p = this.Position;
			this.Buffer[p] = value1;
			this.Buffer[p + 1] = value2;
			this.Position = p + 2;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void WriteBytes(byte value1, byte value2, byte value3)
		{
			var buffer = EnsureBytes(3);
			int p = this.Position;
			buffer[p] = value1;
			buffer[p + 1] = value2;
			buffer[p + 2] = value3;
			this.Position = p + 3;
		}

		/// <summary>Dangerously write three bytes at the end of the buffer, without any capacity checks!</summary>
		/// <remarks>
		/// This method DOES NOT check the buffer capacity before writing, and caller MUST have resized the buffer beforehand!
		/// Failure to do so may introduce memory correction (buffer overflow!).
		/// This should ONLY be used in performance-sensitive code paths that have been audited thoroughly!
		/// </remarks>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void UnsafeWriteBytes(byte value1, byte value2, byte value3)
		{
			Contract.Requires(this.Buffer != null && this.Position + 2 < this.Buffer.Length);
			var buffer = this.Buffer;
			int p = this.Position;
			buffer[p] = value1;
			buffer[p + 1] = value2;
			buffer[p + 2] = value3;
			this.Position = p + 3;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void WriteBytes(byte value1, byte value2, byte value3, byte value4)
		{
			var buffer = EnsureBytes(4);
			int p = this.Position;
			buffer[p] = value1;
			buffer[p + 1] = value2;
			buffer[p + 2] = value3;
			buffer[p + 3] = value4;
			this.Position = p + 4;
		}

		/// <summary>Dangerously write four bytes at the end of the buffer, without any capacity checks!</summary>
		/// <remarks>
		/// This method DOES NOT check the buffer capacity before writing, and caller MUST have resized the buffer beforehand!
		/// Failure to do so may introduce memory correction (buffer overflow!).
		/// This should ONLY be used in performance-sensitive code paths that have been audited thoroughly!
		/// </remarks>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void UnsafeWriteBytes(byte value1, byte value2, byte value3, byte value4)
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

		/// <summary>Dangerously write five bytes at the end of the buffer, without any capacity checks!</summary>
		/// <remarks>
		/// This method DOES NOT check the buffer capacity before writing, and caller MUST have resized the buffer beforehand!
		/// Failure to do so may introduce memory correction (buffer overflow!).
		/// This should ONLY be used in performance-sensitive code paths that have been audited thoroughly!
		/// </remarks>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void WriteBytes(byte value1, byte value2, byte value3, byte value4, byte value5)
		{
			var buffer = EnsureBytes(5);
			int p = this.Position;
			buffer[p] = value1;
			buffer[p + 1] = value2;
			buffer[p + 2] = value3;
			buffer[p + 3] = value4;
			buffer[p + 4] = value5;
			this.Position = p + 5;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void UnsafeWriteBytes(byte value1, byte value2, byte value3, byte value4, byte value5)
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

		/// <summary>Write a byte array to the end of the buffer</summary>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void WriteBytes([CanBeNull] byte[] data)
		{
			if (data != null)
			{
				WriteBytes(data, 0, data.Length);
			}
		}

		/// <summary>Write a chunk of a byte array to the end of the buffer</summary>
		public void WriteBytes(byte[] data, int offset, int count)
		{
			if (count > 0)
			{
				UnsafeHelpers.EnsureBufferIsValidNotNull(data, offset, count);
				int p = this.Position;
				UnsafeHelpers.CopyUnsafe(EnsureBytes(count), p, data, offset, count);
				this.Position = checked(p + count);
			}
		}

		/// <summary>Write a chunk of a byte array to the end of the buffer, with a prefix</summary>
		public void WriteBytes(byte prefix, byte[] data, int offset, int count)
		{
			if (count >= 0)
			{
				if (count > 0) UnsafeHelpers.EnsureBufferIsValidNotNull(data, offset, count);
				var buffer = EnsureBytes(count + 1);
				int p = this.Position;
				buffer[p] = prefix;
				if (count > 0) UnsafeHelpers.CopyUnsafe(buffer, p + 1, data, offset, count);
				this.Position = checked(p + 1 + count);
			}
		}

		/// <summary>Dangerously write a chunk of memory to the end of the buffer, without any capacity checks!</summary>
		/// <remarks>
		/// This method DOES NOT check the buffer capacity before writing, and caller MUST have resized the buffer beforehand!
		/// Failure to do so may introduce memory correction (buffer overflow!).
		/// This should ONLY be used in performance-sensitive code paths that have been audited thoroughly!
		/// </remarks>
		public void UnsafeWriteBytes(byte[] data, int offset, int count)
		{
			Contract.Requires(this.Buffer != null && this.Position >= 0 && data != null && count >= 0 && this.Position + count <= this.Buffer.Length && offset >= 0 && offset + count <= data.Length);

			if (count > 0)
			{
				int p = this.Position;
				UnsafeHelpers.CopyUnsafe(this.Buffer, p, data, offset, count);
				this.Position = checked(p + count);
			}
		}

		/// <summary>Write a segment of bytes to the end of the buffer</summary>
		public void WriteBytes(Slice data)
		{
			data.EnsureSliceIsValid();

			int count = data.Count;
			if (count > 0)
			{
				int p = this.Position;
				UnsafeHelpers.CopyUnsafe(EnsureBytes(count), p, data.Array, data.Offset, count);
				this.Position = checked(p + count);
			}
		}

		/// <summary>Write a segment of bytes to the end of the buffer</summary>
		public void WriteBytes(ref Slice data)
		{
			data.EnsureSliceIsValid();

			int count = data.Count;
			if (count > 0)
			{
				int p = this.Position;
				UnsafeHelpers.CopyUnsafe(EnsureBytes(count), p, data.Array, data.Offset, count);
				this.Position = checked(p + count);
			}
		}

#if ENABLE_SPAN
		/// <summary>Write a segment of bytes to the end of the buffer</summary>
		public void WriteBytes(ReadOnlySpan<byte> data)
		{
			int count = data.Length;
			if (count > 0)
			{
				int p = this.Position;
				UnsafeHelpers.CopyUnsafe(EnsureBytes(count), p, data);
			}
		}
#endif

		/// <summary>Write a segment of bytes to the end of the buffer, with a prefix</summary>
		public void WriteBytes(byte prefix, Slice data)
		{
			data.EnsureSliceIsValid();

			int count = data.Count;
			var buffer = EnsureBytes(count + 1);
			int p = this.Position;
			buffer[p] = prefix;
			if (count > 0) UnsafeHelpers.CopyUnsafe(buffer, p + 1, data.Array, data.Offset, count);
			this.Position = checked(p + count + 1);
		}

#if ENABLE_SPAN
		/// <summary>Write a segment of bytes to the end of the buffer, with a prefix</summary>
		public void WriteBytes(byte prefix, ReadOnlySpan<byte> data)
		{
			int count = data.Length;
			var buffer = EnsureBytes(count + 1);
			int p = this.Position;
			buffer[p] = prefix;
			if (count > 0)
			{
				UnsafeHelpers.CopyUnsafe(buffer, p + 1, data);
			}
			this.Position = checked(p + count + 1);
		}
#endif

		/// <summary>Write a segment of bytes to the end of the buffer</summary>
		public unsafe void WriteBytes(byte* data, uint count)
		{
			if (count == 0) return;
			if (data == null) throw ThrowHelper.ArgumentNullException(nameof(data));

			var buffer = EnsureBytes(count);
			int p = this.Position;
			Contract.Assert(buffer != null && p >= 0 && p + count <= buffer.Length);

			//note: we compute the end offset BEFORE, to protect against arithmetic overflow
			int q = checked((int)(p + count));
			UnsafeHelpers.CopyUnsafe(buffer, p, data, count);
			this.Position = q;
		}

		/// <summary>Append a segment of bytes with a prefix to the end of the buffer</summary>
		/// <param name="prefix">Byte added before the data</param>
		/// <param name="data">Pointer to the start of the data to append</param>
		/// <param name="count">Number of bytes to append (excluding the prefix)</param>
		public unsafe void WriteBytes(byte prefix, byte* data, uint count)
		{
			if (count != 0 && data == null) throw ThrowHelper.ArgumentNullException(nameof(data));

			var buffer = EnsureBytes(count + 1);
			int p = this.Position;
			Contract.Assert(buffer != null && p >= 0 && p + 1 + count <= buffer.Length);

			//note: we compute the end offset BEFORE, to protect against arithmetic overflow
			int q = checked((int)(p + 1 +count));
			buffer[p] = prefix;
			UnsafeHelpers.CopyUnsafe(buffer, p + 1, data, count);
			this.Position = q;
		}

		/// <summary>Dangerously write a segment of bytes at the end of the buffer, without any capacity checks!</summary>
		/// <remarks>
		/// This method DOES NOT check the buffer capacity before writing, and caller MUST have resized the buffer beforehand!
		/// Failure to do so may introduce memory correction (buffer overflow!).
		/// This should ONLY be used in performance-sensitive code paths that have been audited thoroughly!
		/// </remarks>
		public unsafe void UnsafeWriteBytes(byte* data, uint count)
		{
			if (count != 0)
			{
				int p = this.Position;
				Contract.Requires(this.Buffer != null && p >= 0 && data != null && p + count <= this.Buffer.Length);

				int q = checked((int)(p + count));
				UnsafeHelpers.CopyUnsafe(this.Buffer, p, data, count);
				this.Position = q;
			}
		}

		// Appending is used when the caller want to get a Slice that points to the location where the bytes where written in the internal buffer

		/// <summary>Append a byte array to the end of the buffer</summary>
		public Slice AppendBytes(byte[] data)
		{
			if (data == null) return Slice.Empty;
			return AppendBytes(data, 0, data.Length);
		}

		/// <summary>Append a chunk of a byte array to the end of the buffer</summary>
		[Pure]
		public Slice AppendBytes(byte[] data, int offset, int count)
		{
			if (count == 0) return Slice.Empty;

			UnsafeHelpers.EnsureBufferIsValidNotNull(data, offset, count);
			int p = this.Position;
			var buffer = EnsureBytes(count);
			UnsafeHelpers.CopyUnsafe(buffer, p, data, offset, count);
			this.Position = checked(p + count);
			return new Slice(buffer, p, count);
		}

		/// <summary>Append a segment of bytes to the end of the buffer</summary>
		/// <param name="data">Buffer containing the data to append</param>
		/// <returns>Slice that maps the interned data using the writer's buffer.</returns>
		/// <remarks>If you do not need the resulting Slice, you should call <see cref="WriteBytes(Slice)"/> instead!</remarks>
		[Pure]
		public Slice AppendBytes(Slice data)
		{
			data.EnsureSliceIsValid();

			int count = data.Count;
			if (count == 0) return Slice.Empty;

			int p = this.Position;
			var buffer = EnsureBytes(count);
			UnsafeHelpers.CopyUnsafe(buffer, p, data.Array, data.Offset, count);
			this.Position = checked(p + count);
			return new Slice(buffer, p, count);
		}

		/// <summary>Write a segment of bytes to the end of the buffer</summary>
		/// <param name="data">Buffer containing the data to append</param>
		/// <returns>Slice that maps the interned data using the writer's buffer.</returns>
		/// <remarks>If you do not need the resulting Slice, you should call <see cref="WriteBytes(Slice)"/> instead!</remarks>
		[Pure]
		public Slice AppendBytes(ref Slice data)
		{
			data.EnsureSliceIsValid();

			int count = data.Count;
			if (count == 0) return Slice.Empty;

			int p = this.Position;
			var buffer = EnsureBytes(count);
			UnsafeHelpers.CopyUnsafe(buffer, p, data.Array, data.Offset, count);
			this.Position = checked(p + count);
			return new Slice(buffer, p, count);
		}

		/// <summary>Append a segment of bytes to the end of the buffer</summary>
		/// <param name="data">Pointer to the start of the data to append</param>
		/// <param name="count">Number of bytes to append</param>
		/// <returns>Slice that maps to the section of buffer that contains the appended data</returns>
		/// <remarks>If you do not need the resulting Slice, you should call <see cref="WriteBytes(byte*,uint)"/> instead!</remarks>
		[Pure]
		public unsafe Slice AppendBytes(byte* data, uint count)
		{
			if (count == 0) return Slice.Empty;
			if (data == null) throw ThrowHelper.ArgumentNullException(nameof(data));

			var buffer = EnsureBytes(count);
			int p = this.Position;
			Contract.Assert(buffer != null && p >= 0 && p + count <= buffer.Length);

			int q = checked((int)(p + count));
			UnsafeHelpers.CopyUnsafe(buffer, p, data, count);
			this.Position = q;
			return new Slice(buffer, p, q - p);
		}

		#endregion

		#region Fixed, Little-Endian

		/// <summary>Writes a 16-bit unsigned integer, using little-endian encoding</summary>
		/// <remarks>Advances the cursor by 2 bytes</remarks>
		public void WriteFixed16(short value)
		{
			int p = this.Position;
			unsafe
			{
				fixed (byte* ptr = &EnsureBytes(2)[p])
				{
					UnsafeHelpers.StoreInt16LE(ptr, value);
				}
			}
			this.Position = p + 2;
		}

		/// <summary>Writes a 16-bit unsigned integer, using little-endian encoding</summary>
		/// <remarks>Advances the cursor by 2 bytes</remarks>
		public void WriteFixed16(ushort value)
		{
			int p = this.Position;
			unsafe
			{
				fixed (byte* ptr = &EnsureBytes(2)[p])
				{
					UnsafeHelpers.StoreUInt16LE(ptr, value);
				}
			}
			this.Position = p + 2;
		}

		/// <summary>Writes a 16-bit unsigned integer, using little-endian encoding</summary>
		/// <remarks>Advances the cursor by 2 bytes</remarks>
		public void WriteFixed24(int value)
		{
			int p = this.Position;
			unsafe
			{
				fixed (byte* ptr = &EnsureBytes(3)[p])
				{
					UnsafeHelpers.StoreUInt24LE(ptr, (uint) value);
				}
			}
			this.Position = p + 3;
		}

		/// <summary>Writes a 16-bit unsigned integer, using little-endian encoding</summary>
		/// <remarks>Advances the cursor by 2 bytes</remarks>
		public void WriteFixed24(uint value)
		{
			int p = this.Position;
			unsafe
			{
				fixed (byte* ptr = &EnsureBytes(3)[p])
				{
					UnsafeHelpers.StoreUInt24LE(ptr, value);
				}
			}
			this.Position = p + 3;
		}

		/// <summary>Writes a 32-bit signed integer, using little-endian encoding</summary>
		/// <remarks>Advances the cursor by 4 bytes</remarks>
		public void WriteFixed32(int value)
		{
			int p = this.Position;
			unsafe
			{
				fixed (byte* ptr = &EnsureBytes(4)[p])
				{
					UnsafeHelpers.WriteFixed32Unsafe(ptr, (uint) value);
				}
			}
			this.Position = p + 4;
		}

		/// <summary>Writes a 32-bit unsigned integer, using little-endian encoding</summary>
		/// <remarks>Advances the cursor by 4 bytes</remarks>
		public void WriteFixed32(uint value)
		{
			int p = this.Position;
			unsafe
			{
				fixed (byte* ptr = &EnsureBytes(4)[p])
				{
					UnsafeHelpers.WriteFixed32Unsafe(ptr, value);
				}
			}
			this.Position = p + 4;
		}

		/// <summary>Writes a 64-bit signed integer, using little-endian encoding</summary>
		/// <remarks>Advances the cursor by 8 bytes</remarks>
		public void WriteFixed64(long value)
		{
			int p = this.Position;
			unsafe
			{
				fixed (byte* ptr = &EnsureBytes(8)[p])
				{
					UnsafeHelpers.WriteFixed64Unsafe(ptr, (ulong) value);
				}
			}
			this.Position = p + 8;
		}

		/// <summary>Writes a 64-bit unsigned integer, using little-endian encoding</summary>
		/// <remarks>Advances the cursor by 8 bytes</remarks>
		public void WriteFixed64(ulong value)
		{
			int p = this.Position;
			unsafe
			{
				fixed (byte* ptr = &EnsureBytes(8)[p])
				{
					UnsafeHelpers.WriteFixed64Unsafe(ptr, value);
				}
			}
			this.Position = p + 8;
		}

		#endregion

		#region Fixed, Big-Endian

		/// <summary>Writes a 16-bit signed integer, using big-endian encoding</summary>
		/// <remarks>Advances the cursor by 2 bytes</remarks>
		public void WriteFixed16BE(int value)
		{
			var buffer = EnsureBytes(2);
			int p = this.Position;
			buffer[p] = (byte)(value >> 8);
			buffer[p + 1] = (byte)value;
			this.Position = p + 2;
		}

		/// <summary>Writes a 16-bit unsigned integer, using big-endian encoding</summary>
		/// <remarks>Advances the cursor by 2 bytes</remarks>
		public void WriteFixed16BE(uint value)
		{
			var buffer = EnsureBytes(2);
			int p = this.Position;
			buffer[p] = (byte)(value >> 8);
			buffer[p + 1] = (byte)value;
			this.Position = p + 2;
		}

		/// <summary>Writes a 24-bit signed integer, using big-endian encoding</summary>
		/// <remarks>Advances the cursor by 2 bytes</remarks>
		public void WriteFixed24BE(int value)
		{
			var buffer = EnsureBytes(3);
			int p = this.Position;
			unsafe
			{
				fixed (byte* ptr = &buffer[p])
				{
					UnsafeHelpers.StoreInt24BE(ptr, value);
				}
			}
			this.Position = p + 3;
		}

		/// <summary>Writes a 24-bit unsigned integer, using big-endian encoding</summary>
		/// <remarks>Advances the cursor by 3 bytes</remarks>
		public void WriteFixed24BE(uint value)
		{
			var buffer = EnsureBytes(3);
			int p = this.Position;
			unsafe
			{
				fixed (byte* ptr = &buffer[p])
				{
					UnsafeHelpers.StoreUInt24BE(ptr, value);
				}
			}
			this.Position = p + 3;
		}

		/// <summary>Writes a 32-bit signed integer, using big-endian encoding</summary>
		/// <remarks>Advances the cursor by 4 bytes</remarks>
		public void WriteFixed32BE(int value)
		{
			var buffer = EnsureBytes(4);
			int p = this.Position;
			buffer[p] = (byte)(value >> 24);
			buffer[p + 1] = (byte)(value >> 16);
			buffer[p + 2] = (byte)(value >> 8);
			buffer[p + 3] = (byte)(value);
			this.Position = p + 4;
		}

		/// <summary>Writes a 32-bit unsigned integer, using big-endian encoding</summary>
		/// <remarks>Advances the cursor by 4 bytes</remarks>
		public void WriteFixed32BE(uint value)
		{
			var buffer = EnsureBytes(4);
			int p = this.Position;
			buffer[p] = (byte)(value >> 24);
			buffer[p + 1] = (byte)(value >> 16);
			buffer[p + 2] = (byte)(value >> 8);
			buffer[p + 3] = (byte)(value);
			this.Position = p + 4;
		}

		/// <summary>Writes a 64-bit signed integer, using big-endian encoding</summary>
		/// <remarks>Advances the cursor by 8 bytes</remarks>
		public void WriteFixed64BE(long value)
		{
			var buffer = EnsureBytes(8);
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

		/// <summary>Writes a 64-bit unsigned integer, using big-endian encoding</summary>
		/// <remarks>Advances the cursor by 8 bytes</remarks>
		public void WriteFixed64BE(ulong value)
		{
			var buffer = EnsureBytes(8);
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

		#region Decimals...

		public void WriteSingle(float value)
		{
			var buffer = EnsureBytes(4);
			int p = this.Position;
			unsafe
			{
				fixed (byte* ptr = &buffer[p])
				{
					*((int*)ptr) = *(int*)(&value);
				}
			}
			this.Position = p + 4;
		}

		public void WriteSingle(byte prefix, float value)
		{
			var buffer = EnsureBytes(5);
			int p = this.Position;
			unsafe
			{
				fixed (byte* ptr = &buffer[p])
				{
					ptr[0] = prefix;
					*((int*)(ptr + 1)) = *(int*)(&value);
				}
			}
			this.Position = p + 5;
		}

		public void WriteDouble(double value)
		{
			var buffer = EnsureBytes(8);
			int p = this.Position;
			unsafe
			{
				fixed (byte* ptr = &buffer[p])
				{
					*((long*)ptr) = *(long*)(&value);
				}
			}
			this.Position = p + 8;
		}

		public void WriteDouble(byte prefix, double value)
		{
			var buffer = EnsureBytes(9);
			int p = this.Position;
			unsafe
			{
				fixed (byte* ptr = &buffer[p])
				{
					ptr[0] = prefix;
					*((long*)(ptr + 1)) = *(long*)(&value);
				}
			}
			this.Position = p + 9;
		}

		#endregion

		#region Variable size

		#region VarInts...

		/// <summary>Writes a 7-bit encoded unsigned int (aka 'Varint16') at the end, and advances the cursor</summary>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void WriteVarInt16(ushort value)
		{
			if (value < (1 << 7))
			{
				WriteByte((byte)value);
			}
			else
			{
				WriteVarInt16Slow(value);
			}
		}

		private void WriteVarInt16Slow(ushort value)
		{
			const uint MASK = 128;
			//note: value is known to be >= 128
			if (value < (1 << 14))
			{
				WriteBytes(
					(byte)(value | MASK),
					(byte)(value >> 7)
				);
			}
			else
			{
				WriteBytes(
					(byte)(value | MASK),
					(byte)((value >> 7) | MASK),
					(byte)(value >> 14)
				);
			}

		}

		/// <summary>Writes a 7-bit encoded unsigned int (aka 'Varint32') at the end, and advances the cursor</summary>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void WriteVarInt32(uint value)
		{
			if (value < (1 << 7))
			{
				WriteByte((byte) value);
			}
			else
			{
				WriteVarInt32Slow(value);
			}
		}

		private void WriteVarInt32Slow(uint value)
		{
			const uint MASK = 128;
			//note: value is known to be >= 128
			if (value < (1 << 14))
			{
				WriteBytes(
					(byte)(value | MASK),
					(byte)(value >> 7)
				);
			}
			else if (value < (1 << 21))
			{
				WriteBytes(
					(byte)(value | MASK),
					(byte)((value >> 7) | MASK),
					(byte)(value >> 14)
				);
			}
			else if (value < (1 << 28))
			{
				WriteBytes(
					(byte)(value | MASK),
					(byte)((value >> 7) | MASK),
					(byte)((value >> 14) | MASK),
					(byte)(value >> 21)
				);
			}
			else
			{
				WriteBytes(
					(byte)(value | MASK),
					(byte)((value >> 7) | MASK),
					(byte)((value >> 14) | MASK),
					(byte)((value >> 21) | MASK),
					(byte)(value >> 28)
				);
			}
		}

		/// <summary>Writes a 7-bit encoded unsigned long (aka 'Varint64') at the end, and advances the cursor</summary>
		public void WriteVarInt64(ulong value)
		{
			//note: if the size if 64-bits, we probably expact values to always be way above 128 so no need to optimize for this case here

			const uint MASK = 128;
			// max encoded size is 10 bytes
			var buffer = EnsureBytes(UnsafeHelpers.SizeOfVarInt(value));
			int p = this.Position;
			while (value >= MASK)
			{
				buffer[p++] = (byte) ((value & (MASK - 1)) | MASK);
				value >>= 7;
			}
			buffer[p++] = (byte) value;
			this.Position = p;
		}

		#endregion

		#region VarBytes...

		/// <summary>Writes a length-prefixed byte array, and advances the cursor</summary>
		public void WriteVarBytes(Slice value)
		{
			//REVIEW: what should we do for Slice.Nil ?

			value.EnsureSliceIsValid();
			int n = value.Count;
			if (n >= 128)
			{
				WriteVarBytesSlow(value);
				return;
			}

			var buffer = EnsureBytes(n + 1);
			int p = this.Position;
			// write the count (single byte)
			buffer[p] = (byte)n;
			// write the bytes
			if (n > 0) UnsafeHelpers.CopyUnsafe(buffer, p + 1, value.Array, value.Offset, n);
			this.Position = checked(p + n + 1);
		}

#if ENABLE_SPAN
		/// <summary>Writes a length-prefixed byte array, and advances the cursor</summary>
		public void WriteVarBytes(ReadOnlySpan<byte> value)
		{
			int n = value.Length;
			if (n >= 128)
			{
				WriteVarBytesSlow(value);
				return;
			}

			var buffer = EnsureBytes(n + 1);
			int p = this.Position;
			// write the count (single byte)
			buffer[p] = (byte)n;
			// write the bytes
			if (n > 0)
			{
				UnsafeHelpers.CopyUnsafe(buffer, p + 1, value);
			}
			this.Position = checked(p + n + 1);
		}
#endif

#if ENABLE_SPAN
		private void WriteVarBytesSlow(ReadOnlySpan<byte> value)
		{
			int n = value.Length;
			EnsureBytes(checked(n + 5));
			// write the count
			WriteVarInt32((uint) n);
			// write the bytes
			int p = this.Position;
			UnsafeHelpers.CopyUnsafe(this.Buffer, p, value);
			this.Position = checked(p + n);
		}
#else
		private void WriteVarBytesSlow(Slice value)
		{
			int n = value.Count;
			EnsureBytes(checked(n + 5));
			// write the count
			WriteVarInt32((uint) n);
			// write the bytes
			int p = this.Position;
			UnsafeHelpers.CopyUnsafe(this.Buffer, p, value.Array, value.Offset, n);
			this.Position = checked(p + n);
		}
#endif

		/// <summary>Writes a length-prefixed byte array, and advances the cursor</summary>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void WriteVarBytes([NotNull] byte[] bytes)
		{
			Contract.Requires(bytes != null);
			WriteVarBytes(bytes.AsSlice());
		}

		/// <summary>Writes a length-prefixed byte array, and advances the cursor</summary>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void WriteVarBytes([NotNull] byte[] bytes, int offset, int count)
		{
			Contract.Requires(count == 0 || bytes != null);
			WriteVarBytes(bytes.AsSlice(offset, count));
		}

		public unsafe void WriteVarBytes(byte* data, uint count)
		{
			if (count >= 128)
			{
				WriteVarBytesSlow(data, count);
				return;
			}

			var buffer = EnsureBytes(count + 1);
			int p = this.Position;
			// write the count (single byte)
			buffer[p] = (byte) count;
			// write the bytes
			if (count > 0)
			{
				Contract.Assert(data != null);
				UnsafeHelpers.CopyUnsafe(buffer, p + 1, data, count);
			}
			this.Position = checked(p + (int) count + 1);
		}

		private unsafe void WriteVarBytesSlow(byte* data, uint n)
		{
			Contract.Assert(data != null);

			// 32-bit varint may take up to 5 bytes
			EnsureBytes(n + 5);

			// write the count
			WriteVarInt32(n);
			// write the bytes
			int p = this.Position;
			UnsafeHelpers.CopyUnsafe(this.Buffer, p, data, n);
			this.Position = checked((int)(p + n));
		}

		#endregion

		#region VarString...

		// all VarStrings are encoded as a VarInt that contains the number of following encoded bytes
		// => caller MUST KNOWN the encoding! (usually UTF-8)
		// => the string's length is NOT stored!

		/// <summary>Write a variabe-sized string, using the specified encoding</summary>
		/// <param name="value"></param>
		/// <param name="encoding"></param>
		public void WriteVarString(string value, Encoding encoding = null)
		{
			if (encoding == null)
			{
				WriteVarStringUtf8(value);
				return;
			}
			int byteCount = encoding.GetByteCount(value);
			if (byteCount == 0)
			{
				WriteByte(0);
				return;
			}
			WriteVarInt32((uint) byteCount);
			int p = this.Position;
			int n = encoding.GetBytes(s: value, charIndex: 0, charCount: value.Length, bytes: this.Buffer, byteIndex: p);
			this.Position = checked(p + n);
		}

		/// <summary>Write a variable-sized string, encoded using UTF-8</summary>
		/// <param name="value">String to append</param>
		/// <remarks>The null and empty string will be stored the same way. Caller must use a different technique if they must be stored differently.</remarks>
		public void WriteVarStringUtf8(string value)
		{
			// Format:
			// - VarInt     Number of following bytes
			// - Byte[]     UTF-8 encoded bytes
			// Examples:
			// - "" => { 0x00 }
			// - "ABC" => { 0x03 'A' 'B' 'C' }
			// - "Héllo" => { 0x06 'h' 0xC3 0xA9 'l' 'l' 'o' }

			// We need to know the encoded size beforehand, because we need to write the size first!
			int byteCount = Encoding.UTF8.GetByteCount(value);
			if (byteCount == 0)
			{ // nul or empty string
				WriteByte(0);
			}
			else if (byteCount == value.Length)
			{ // ASCII!
				WriteVarAsciiInternal(value);
			}
			else
			{ // contains non-ASCII characters, we will need to encode
				WriteVarStringUtf8Internal(value, byteCount);
			}
		}

		private void WriteVarStringUtf8Internal(string value, int byteCount)
		{
			Contract.Assert(value != null && byteCount > 0 && byteCount >= value.Length);
			EnsureBytes(byteCount + UnsafeHelpers.SizeOfVarBytes(byteCount));
			WriteVarInt32((uint)byteCount);
			int p = this.Position;
			int n = Encoding.UTF8.GetBytes(s: value, charIndex: 0, charCount: value.Length, bytes: this.Buffer, byteIndex: p);
			this.Position = checked(p + n);
		}

		/// <summary>Write a variable-sized string, which is known to only contain ASCII characters (0..127)</summary>
		/// <remarks>This is faster than <see cref="WriteVarString(string, Encoding)"/> when the caller KNOWS that the string is ASCII only. This should only be used with keywords and constants, NOT with user input!</remarks>
		/// <exception cref="ArgumentException">If the string contains characters above 127</exception>
		public void WriteVarStringAscii(string value)
		{
			if (string.IsNullOrEmpty(value))
			{
				WriteByte(0);
			}
			else
			{
				WriteVarAsciiInternal(value);
			}
		}

		/// <summary>Write a variable string that is known to only contain ASCII characters</summary>
		private unsafe void WriteVarAsciiInternal(string value)
		{
			// Caller must ensure that string is ASCII only! (otherwise it will be corrupted)
			Contract.Requires(!string.IsNullOrEmpty(value));

			int len = value.Length;
			var buffer = EnsureBytes(len + UnsafeHelpers.SizeOfVarBytes(len));
			int p = this.Position;

			fixed (byte* bytes = &buffer[p])
			fixed (char* chars = value)
			{
				var outp = UnsafeHelpers.WriteVarInt32Unsafe(bytes, (uint) value.Length);
				p += (int) (outp - bytes);
				int mask = 0;
				for (int i = 0; i < len; i++)
				{
					var c = chars[i];
					mask |= c;
					outp[i] = (byte)c;
				}
				if (mask >= 128) throw ThrowHelper.ArgumentException(nameof(value), "The specified string must only contain ASCII characters.");
			}
			this.Position = checked(p + value.Length);
		}

		#endregion

		#endregion

		#region UUIDs...

		/// <summary>Write a 128-bit UUID, and advances the cursor</summary>
		public void WriteUuid128(Uuid128 value)
		{
			var buffer = EnsureBytes(16);
			int p = this.Position;
			unsafe
			{
				fixed (byte* ptr = &buffer[p])
				{
					value.WriteToUnsafe(ptr);
				}
			}
			this.Position = p + 16;
		}

		/// <summary>Write a 128-bit UUID, and advances the cursor</summary>
		public void UnsafeWriteUuid128(Uuid128 value)
		{
			Contract.Requires(this.Buffer != null && this.Position + 15 < this.Buffer.Length);
			int p = this.Position;
			unsafe
			{
				fixed (byte* ptr = &this.Buffer[p])
				{
					value.WriteToUnsafe(ptr);
				}
			}
			this.Position = p + 16;
		}

		/// <summary>Write a 128-bit UUID, and advances the cursor</summary>
		public void WriteUuid64(Uuid64 value)
		{
			var buffer = EnsureBytes(8);
			int p = this.Position;
			unsafe
			{
				fixed (byte* ptr = &buffer[p])
				{
					value.WriteToUnsafe(ptr);
				}
			}
			this.Position = p + 8;
		}

		/// <summary>Write a 128-bit UUID, and advances the cursor</summary>
		public void UnsafeWriteUuid64(Uuid64 value)
		{
			Contract.Requires(this.Buffer != null && this.Position + 7 < this.Buffer.Length);
			int p = this.Position;
			unsafe
			{
				fixed (byte* ptr = &this.Buffer[p])
				{
					value.WriteToUnsafe(ptr);
				}
			}
			this.Position = p + 8;
		}

		#endregion

		#region Fixed-Size Text

		/// <summary>Write a string using UTF-8</summary>
		/// <param name="value">Text to write</param>
		/// <returns>Number of bytes written</returns>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public int WriteString(string value)
		{
			return WriteStringUtf8(value);
		}

#if ENABLE_SPAN
		/// <summary>Write a string using UTF-8</summary>
		/// <param name="value">Text to write</param>
		/// <returns>Number of bytes written</returns>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public int WriteString(ReadOnlySpan<char> value)
		{
			return WriteStringUtf8(value);
		}
#endif

		/// <summary>Write a string using the specified encoding</summary>
		/// <param name="value">Text to write</param>
		/// <param name="encoding">Encoding used to convert the text to bytes</param>
		/// <returns>Number of bytes written</returns>
		public int WriteString(string value, Encoding encoding)
		{
			if (string.IsNullOrEmpty(value)) return 0;

			// In order to estimate the required capacity, we try to guess for very small strings, but compute the actual value for larger strings,
			// so that we don't waste to much memory (up to 6x the string length in the worst case scenario)
			var buffer = EnsureBytes(value.Length > 128 ? encoding.GetByteCount(value) : encoding.GetMaxByteCount(value.Length));

			int p = this.Position;
			int n = encoding.GetBytes(value, 0, value.Length, buffer, p);
			this.Position = p + n;
			return n;
		}

		/// <summary>Write a string using UTF-8</summary>
		/// <param name="value">Text to write</param>
		/// <returns>Number of bytes written</returns>
		public int WriteStringUtf8(string value)
		{
			if (string.IsNullOrEmpty(value)) return 0;

			// In order to estimate the required capacity, we try to guess for very small strings, but compute the actual value for larger strings,
			// so that we don't waste to much memory (up to 6x the string length in the worst case scenario)
			var buffer = EnsureBytes(value.Length > 128
				? Encoding.UTF8.GetByteCount(value)
				: Encoding.UTF8.GetMaxByteCount(value.Length));

			int p = this.Position;
			int n = Encoding.UTF8.GetBytes(s: value, charIndex: 0, charCount: value.Length, bytes: buffer, byteIndex: p);
			this.Position = checked(p + n);
			return n;
		}

#if ENABLE_SPAN
		/// <summary>Write a string using UTF-8</summary>
		/// <returns>Number of bytes written</returns>
		public int WriteStringUtf8(char[] chars, int offset, int count)
		{
			return WriteStringUtf8(new ReadOnlySpan<char>(chars, offset, count));
		}

		/// <summary>Write a string using UTF-8</summary>
		/// <returns>Number of bytes written</returns>
		public int WriteStringUtf8(ReadOnlySpan<char> chars)
		{
			int count = chars.Length;
			if (count == 0) return 0;

			unsafe
			{
				fixed (char* inp = &MemoryMarshal.GetReference(chars))
				{
					// pour estimer la capacité, on fait une estimation a la louche pour des petites strings, mais on va calculer la bonne valeur pour des string plus grandes,
					// afin d'éviter de gaspiller trop de mémoire (potentiellement jusqu'a 6 fois la taille)
					var buffer = EnsureBytes(count > 128
						? Encoding.UTF8.GetByteCount(inp, count)
						: Encoding.UTF8.GetMaxByteCount(count));

					int p = this.Position;
					fixed (byte* outp = &buffer[p])
					{
						int n = Encoding.UTF8.GetBytes(chars: inp, charCount: count, bytes: outp, byteCount: buffer.Length - p);
						this.Position = checked(p + n);
						return n;
					}
				}
			}
		}
#endif

		[Pure, NotNull, MethodImpl(MethodImplOptions.NoInlining)]
		private static Exception FailInvalidUtf8CodePoint()
		{
			return new DecoderFallbackException("Failed to encode invalid Unicode CodePoint into UTF-8");
		}

		/// <summary>Write a string that only contains ASCII</summary>
		/// <param name="value">String with characters only in the 0..127 range</param>
		/// <remarks>Faster than <see cref="WriteString(string, Encoding)"/> when writing Magic Strings or ascii keywords</remarks>
		/// <returns>Number of bytes written</returns>
		public int WriteStringAscii(string value)
		{
			Contract.Requires(value != null);

			var buffer = EnsureBytes(value.Length);
			int p = this.Position;
			foreach (var c in value)
			{
				buffer[p++] = (byte) c;
			}
			this.Position = p;
			return value.Length;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void WriteBase10(int value)
		{
			if ((uint) value <= 9)
			{
				WriteByte('0' + value);
			}
			else
			{
				WriteBase10Slow(value);
			}
		}

		public void WriteBase10(long value)
		{
			if ((ulong) value <= 9)
			{
				WriteByte('0' + (int) value);
			}
			else if (value <= int.MaxValue)
			{
				WriteBase10Slow((int) value);
			}
			else
			{
				WriteBase10Slower(value);
			}
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void WriteBase10(uint value)
		{
			if (value <= 9)
			{
				WriteByte('0' + (int) value);
			}
			else
			{
				WriteBase10Slow(value);
			}
		}

		public void WriteBase10(ulong value)
		{
			if (value <= 9)
			{
				WriteByte('0' + (int) value);
			}
			else if (value <= uint.MaxValue)
			{
				WriteBase10Slow((uint) value);
			}
			else
			{
				WriteBase10Slower(value);
			}
		}

		private void WriteBase10Slow(int value)
		{
			if (value < 0)
			{ // negative numbers
				if (value == int.MinValue)
				{ // cannot do Abs(MinValue), so special case for this one
					WriteStringAscii("-2147483648");
					return;
				}
				WriteByte('-');
				value = -value;
			}

			if (value < 10)
			{
				WriteByte((byte) ('0' + value));
			}
			else if (value < 100)
			{
				WriteBytes(
					(byte) ('0' + (value / 10)),
					(byte) ('0' + (value % 10))
				);
			}
			else if (value < 1000)
			{
				WriteBytes(
					(byte) ('0' + (value / 100)),
					(byte) ('0' + (value / 10) % 10),
					(byte) ('0' + (value % 10))
				);
			}
			else if (value < 10 * 1000)
			{
				WriteBytes(
					(byte) ('0' + (value / 1000)),
					(byte) ('0' + (value / 100) % 10),
					(byte) ('0' + (value / 10) % 10),
					(byte) ('0' + (value % 10))
				);
			}
			else if (value < 100 * 1000)
			{
				WriteBytes(
					(byte) ('0' + (value / 10000)),
					(byte) ('0' + (value / 1000) % 10),
					(byte) ('0' + (value / 100) % 10),
					(byte) ('0' + (value / 10) % 10),
					(byte) ('0' + (value % 10))
				);
			}
			else
			{
				WriteBase10Slower(value);
			}
		}

		private void WriteBase10Slower(long value)
		{
			//TODO: OPTIMIZE: sans allocations?
			WriteStringAscii(value.ToString(CultureInfo.InvariantCulture));
		}

		private void WriteBase10Slow(uint value)
		{
			if (value < 10)
			{
				WriteByte((byte) ('0' + value));
			}
			else if (value < 100)
			{
				WriteBytes(
					(byte) ('0' + (value / 10)),
					(byte) ('0' + (value % 10))
				);
			}
			else if (value < 1000)
			{
				WriteBytes(
					(byte) ('0' + (value / 100)),
					(byte) ('0' + (value / 10) % 10),
					(byte) ('0' + (value % 10))
				);
			}
			else if (value < 10 * 1000)
			{
				WriteBytes(
					(byte) ('0' + (value / 1000)),
					(byte) ('0' + (value / 100) % 10),
					(byte) ('0' + (value / 10) % 10),
					(byte) ('0' + (value % 10))
				);
			}
			else if (value < 100 * 1000)
			{
				WriteBytes(
					(byte) ('0' + (value / 10000)),
					(byte) ('0' + (value / 1000) % 10),
					(byte) ('0' + (value / 100) % 10),
					(byte) ('0' + (value / 10) % 10),
					(byte) ('0' + (value % 10))
				);
			}
			else
			{
				WriteBase10Slower(value);
			}
		}

		private void WriteBase10Slower(ulong value)
		{
			//TODO: OPTIMIZE: sans allocations?
			WriteStringAscii(value.ToString(CultureInfo.InvariantCulture));
		}

		#endregion

		#region Patching

		#region 8-bits...

		/// <summary>Overwrite a section of the buffer that was already written, with the specified data</summary>
		/// <param name="index">Offset from the start of the buffer where to start replacing</param>
		/// <param name="data">Data that will overwrite the buffer at the specified <paramref name="index"/></param>
		/// <remarks>You must ensure that replaced section does not overlap with the current position!</remarks>
		public void PatchBytes(int index, Slice data)
		{
			if (index + data.Count > this.Position) throw ThrowHelper.IndexOutOfRangeException();
			data.CopyTo(this.Buffer, index);
		}

		/// <summary>Overwrite a section of the buffer that was already written, with the specified data</summary>
		/// <remarks>You must ensure that replaced section does not overlap with the current position!</remarks>
		public void PatchBytes(int index, byte[] buffer, int offset, int count)
		{
			if (index + count > this.Position) throw ThrowHelper.IndexOutOfRangeException();
			System.Buffer.BlockCopy(buffer, offset, this.Buffer, index, count);
		}

		/// <summary>Overwrite a byte of the buffer that was already written</summary>
		/// <remarks>You must ensure that replaced byte is before the current position!</remarks>
		public void PatchByte(int index, byte value)
		{
			if ((uint) index >= this.Position) throw ThrowHelper.IndexOutOfRangeException();
			this.Buffer[index] = value;
		}

		/// <summary>Overwrite a byte of the buffer that was already written</summary>
		/// <remarks>You must ensure that replaced byte is before the current position!</remarks>
		public void PatchByte(int index, int value)
		{
			//note: convenience method, because C# compiler likes to produce 'int' when combining bits together
			if ((uint) index >= this.Position) throw ThrowHelper.IndexOutOfRangeException();
			this.Buffer[index] = (byte) value;
		}

		#endregion

		#region 16-bits...

		/// <summary>Overwrite a word of the buffer that was already written</summary>
		/// <remarks>You must ensure that replaced word is before the current position!</remarks>
		public void PatchInt16(int index, short value)
		{
			if (index + 2 > this.Position) ThrowHelper.ThrowIndexOutOfRangeException();
			unsafe
			{
				fixed (byte* ptr = &this.Buffer[index])
				{
					UnsafeHelpers.WriteFixed16Unsafe(ptr, (ushort) value);
				}
			}
		}

		/// <summary>Overwrite a word of the buffer that was already written</summary>
		/// <remarks>You must ensure that replaced word is before the current position!</remarks>
		public void PatchUInt16(int index, ushort value)
		{
			if (index + 2 > this.Position) ThrowHelper.ThrowIndexOutOfRangeException();
			unsafe
			{
				fixed (byte* ptr = &this.Buffer[index])
				{
					UnsafeHelpers.WriteFixed16Unsafe(ptr, value);
				}
			}
		}

		/// <summary>Overwrite a word of the buffer that was already written</summary>
		/// <remarks>You must ensure that replaced word is before the current position!</remarks>
		public void PatchInt16BE(int index, short value)
		{
			if (index + 2 > this.Position) ThrowHelper.ThrowIndexOutOfRangeException();
			unsafe
			{
				fixed (byte* ptr = &this.Buffer[index])
				{
					UnsafeHelpers.WriteFixed16BEUnsafe(ptr, (ushort) value);
				}
			}
		}

		/// <summary>Overwrite a word of the buffer that was already written</summary>
		/// <remarks>You must ensure that replaced word is before the current position!</remarks>
		public void PatchUInt16BE(int index, ushort value)
		{
			if (index + 2 > this.Position) ThrowHelper.ThrowIndexOutOfRangeException();
			unsafe
			{
				fixed (byte* ptr = &this.Buffer[index])
				{
					UnsafeHelpers.WriteFixed16BEUnsafe(ptr, value);
				}
			}
		}

		#endregion

		#region 32-bits...

		/// <summary>Overwrite a dword of the buffer that was already written</summary>
		/// <remarks>You must ensure that replaced dword is before the current position!</remarks>
		public void PatchInt32(int index, int value)
		{
			if (index + 4 > this.Position) ThrowHelper.ThrowIndexOutOfRangeException();
			unsafe
			{
				fixed (byte* ptr = &this.Buffer[index])
				{
					UnsafeHelpers.WriteFixed32Unsafe(ptr, (uint) value);
				}
			}
		}

		/// <summary>Overwrite a dword of the buffer that was already written</summary>
		/// <remarks>You must ensure that replaced dword is before the current position!</remarks>
		public void PatchUInt32(int index, uint value)
		{
			if (index + 4 > this.Position) ThrowHelper.ThrowIndexOutOfRangeException();
			unsafe
			{
				fixed (byte* ptr = &this.Buffer[index])
				{
					UnsafeHelpers.WriteFixed32Unsafe(ptr, value);
				}
			}
		}

		/// <summary>Overwrite a dword of the buffer that was already written</summary>
		/// <remarks>You must ensure that replaced dword is before the current position!</remarks>
		public void PatchInt32BE(int index, int value)
		{
			if (index + 4 > this.Position) ThrowHelper.ThrowIndexOutOfRangeException();
			unsafe
			{
				fixed (byte* ptr = &this.Buffer[index])
				{
					UnsafeHelpers.WriteFixed32BEUnsafe(ptr, (uint) value);
				}
			}
		}

		/// <summary>Overwrite a dword of the buffer that was already written</summary>
		/// <remarks>You must ensure that replaced dword is before the current position!</remarks>
		public void PatchUInt32BE(int index, uint value)
		{
			if (index + 4 > this.Position) ThrowHelper.ThrowIndexOutOfRangeException();
			unsafe
			{
				fixed (byte* ptr = &this.Buffer[index])
				{
					UnsafeHelpers.WriteFixed32BEUnsafe(ptr, value);
				}
			}
		}

		#endregion

		#region 64-bits...

		/// <summary>Overwrite a qword of the buffer that was already written</summary>
		/// <remarks>You must ensure that replaced qword is before the current position!</remarks>
		public void PatchInt64(int index, long value)
		{
			if (index + 8 > this.Position) ThrowHelper.ThrowIndexOutOfRangeException();
			unsafe
			{
				fixed (byte* ptr = &this.Buffer[index])
				{
					UnsafeHelpers.WriteFixed64Unsafe(ptr, (ulong) value);
				}
			}
		}

		/// <summary>Overwrite a qword of the buffer that was already written</summary>
		/// <remarks>You must ensure that replaced qword is before the current position!</remarks>
		public void PatchUInt64(int index, ulong value)
		{
			if (index + 8 > this.Position) ThrowHelper.ThrowIndexOutOfRangeException();
			unsafe
			{
				fixed (byte* ptr = &this.Buffer[index])
				{
					UnsafeHelpers.WriteFixed64Unsafe(ptr, value);
				}
			}
		}

		/// <summary>Overwrite a qword of the buffer that was already written</summary>
		/// <remarks>You must ensure that replaced qword is before the current position!</remarks>
		public void PatchInt64BE(int index, long value)
		{
			if (index + 8 > this.Position) ThrowHelper.ThrowIndexOutOfRangeException();
			unsafe
			{
				fixed (byte* ptr = &this.Buffer[index])
				{
					UnsafeHelpers.WriteFixed64BEUnsafe(ptr, (ulong) value);
				}
			}
		}

		/// <summary>Overwrite a qword of the buffer that was already written</summary>
		/// <remarks>You must ensure that replaced qword is before the current position!</remarks>
		public void PatchUInt64BE(int index, ulong value)
		{
			if (index + 8 > this.Position) ThrowHelper.ThrowIndexOutOfRangeException();
			unsafe
			{
				fixed (byte* ptr = &this.Buffer[index])
				{
					UnsafeHelpers.WriteFixed64BEUnsafe(ptr, value);
				}
			}
		}

		#endregion

		#endregion

		/// <summary>Return the remaining capacity in the current underlying buffer</summary>
		public int RemainingCapacity
		{
			get
			{
				var buffer = this.Buffer;
				if (buffer == null || this.Position >= buffer.Length) return 0;
				return buffer.Length - this.Position;
			}
		}

		/// <summary>Ensures that we can fit the specified amount of data at the end of the buffer</summary>
		/// <param name="count">Number of bytes that will be written</param>
		/// <remarks>If the buffer is too small, it will be resized, and all previously written data will be copied</remarks>
		[NotNull, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public byte[] EnsureBytes(int count)
		{
			//REVIEW: en C#7 on pourrait retourner le tuple (buffer, pos) !

			Contract.Requires(count >= 0);
			var buffer = this.Buffer;
			if (buffer == null || this.Position + count > buffer.Length)
			{
				buffer = GrowBuffer(ref this.Buffer, this.Position + count);
				Contract.Ensures(buffer != null && buffer.Length >= this.Position + count);
			}
			return buffer;
		}

#if ENABLE_ARRAY_POOL

		/// <summary>Ensures that we can fit the specified amount of data at the end of the buffer</summary>
		/// <param name="count">Number of bytes that will be written</param>
		/// <param name="pool"></param>
		/// <remarks>If the buffer is too small, it will be resized, and all previously written data will be copied</remarks>
		[NotNull, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public byte[] EnsureBytes(int count, ArrayPool<byte> pool)
		{
			//REVIEW: en C#7 on pourrait retourner le tuple (buffer, pos) !

			Contract.Requires(count >= 0);
			var buffer = this.Buffer;
			if (buffer == null || this.Position + count > buffer.Length)
			{
				buffer = GrowBuffer(ref this.Buffer, this.Position + count, pool);
				Contract.Ensures(buffer != null && buffer.Length >= this.Position + count);
			}
			return buffer;
		}

#endif

		/// <summary>Ensures that we can fit the specified  amount of data at the end of the buffer</summary>
		/// <param name="count">Number of bytes that will be written</param>
		/// <remarks>If the buffer is too small, it will be resized, and all previously written data will be copied</remarks>
		[NotNull, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public byte[] EnsureBytes(uint count)
		{
			return EnsureBytes(checked((int) count));
		}

		/// <summary>Ensures that we can fit data at a specifc offset in the buffer</summary>
		/// <param name="offset">Offset into the buffer (from the start)</param>
		/// <param name="count">Number of bytes that will be written at this offset</param>
		/// <remarks>If the buffer is too small, it will be resized, and all previously written data will be copied</remarks>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void EnsureOffsetAndSize(int offset, int count)
		{
			Contract.Requires(offset >= 0 && count >= 0);
			if (this.Buffer == null || offset + count > this.Buffer.Length)
			{
				GrowBuffer(ref this.Buffer, offset + count);
			}
		}

		/// <summary>Resize a buffer by doubling its capacity</summary>
		/// <param name="buffer">Reference to the variable holding the buffer to create/resize. If null, a new buffer will be allocated. If not, the content of the buffer will be copied into the new buffer.</param>
		/// <param name="minimumCapacity">Mininum guaranteed buffer size after resizing.</param>
		/// <remarks>The buffer will be resized to the maximum between the previous size multiplied by 2, and <paramref name="minimumCapacity"/>. The capacity will always be rounded to a multiple of 16 to reduce memory fragmentation</remarks>
		[NotNull, MethodImpl(MethodImplOptions.NoInlining)]
		public static byte[] GrowBuffer(
			ref byte[] buffer,
			int minimumCapacity = 0
#if ENABLE_ARRAY_POOL
			, ArrayPool<byte> pool = null
#endif
			)
		{
			Contract.Requires(minimumCapacity >= 0);

			// double the size of the buffer, or use the minimum required
			long newSize = Math.Max(buffer == null ? 0 : (((long) buffer.Length) << 1), minimumCapacity);

			// .NET (as of 4.5) cannot allocate an array with more than 2^31 - 1 items...
			if (newSize > 0x7fffffffL) throw FailCannotGrowBuffer();

			// round up to 16 bytes, to reduce fragmentation
			int size = BitHelpers.AlignPowerOfTwo((int) newSize, 16);

#if ENABLE_ARRAY_POOL
			if (pool == null)
			{
				Array.Resize(ref buffer, size);
			}
			else
			{ // use the pool to resize the buffer
				pool.Resize(ref buffer, size);
			}
#else
			Array.Resize(ref buffer, size);
#endif
			return buffer;
		}

		[Pure, NotNull, MethodImpl(MethodImplOptions.NoInlining)]
		private static Exception FailCannotGrowBuffer()
		{
#if DEBUG
			// If you breakpoint here, that means that you probably have an uncheked maximum buffer size, or a runaway while(..) { append(..) } code in your layer code !
			// => you should ALWAYS ensure a reasonable maximum size of your allocations !
			if (System.Diagnostics.Debugger.IsAttached) System.Diagnostics.Debugger.Break();
#endif
			// note: some methods in the BCL do throw an OutOfMemoryException when attempting to allocated more than 2^31
			return new OutOfMemoryException("Buffer cannot be resized, because it would exceed the maximum allowed size");
		}

		[UsedImplicitly(ImplicitUseTargetFlags.WithMembers)]
		private sealed class DebugView
		{

			public DebugView(SliceWriter writer)
			{
				this.Data = new Slice(writer.Buffer, 0, writer.Position);
				this.Position = writer.Position;
				this.Capacity = writer.Buffer.Length;
			}

			public Slice Data { get; }

			public int Position { get; }

			public int Capacity { get; }

		}

	}

}
