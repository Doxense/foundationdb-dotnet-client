#region BSD License
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

#if !USE_SHARED_FRAMEWORK

namespace Doxense.Memory
{
	using System;
	using System.Buffers;
	using System.Diagnostics;
	using System.Globalization;
	using System.Runtime.CompilerServices;
	using System.Runtime.InteropServices;
	using System.Text;
	using Doxense.Diagnostics.Contracts;
	using Doxense.Memory.Text;
	using Doxense.Serialization;
	using Doxense.Text;
	using JetBrains.Annotations;

	/// <summary>Slice buffer that emulates a pseudo-stream using a byte array that will automatically grow in size, if necessary</summary>
	/// <remarks>This struct MUST be passed by reference!</remarks>
	[PublicAPI, DebuggerDisplay("Position={Position}, Capacity={Capacity}"), DebuggerTypeProxy(typeof(SliceWriter.DebugView))]
	[DebuggerNonUserCode] //remove this when you need to troubleshoot this class!
	public struct SliceWriter : IBufferWriter<byte>, ISliceSerializable
	{
		// Invariant
		// * Valid data always start at offset 0
		// * 'this.Position' is equal to the current size as well as the offset of the next available free spot
		// * 'this.Buffer' is either null (meaning newly created stream), or is at least as big as this.Position

		#region Private Members...

		/// <summary>Buffer holding the data</summary>
		/// <remarks>Consider calling <see cref="GetBufferUnsafe"/> to protect against this field being null.</remarks>
		public byte[]? Buffer;

		/// <summary>Position in the buffer ( == number of already written bytes)</summary>
		public int Position;

		public readonly ArrayPool<byte>? Pool;

		#endregion

		#region Constructors...

		/// <summary>Create a new empty binary buffer with an initial allocated size</summary>
		/// <param name="capacity">Initial capacity of the buffer</param>
		public SliceWriter([Positive] int capacity)
		{
			Contract.Positive(capacity);

			this.Buffer = capacity == 0 ? Array.Empty<byte>() : ArrayPool<byte>.Shared.Rent(capacity); //REVIEW: BUGBUG: est-ce une bonne idée d'utiliser un pool ici?
			this.Position = 0;
			this.Pool = null;
		}

		/// <summary>Create a new empty binary buffer with an initial allocated size</summary>
		/// <param name="capacity">Initial capacity of the buffer</param>
		/// <param name="pool">Pool qui sera utilisé pour la gestion des buffers</param>
		public SliceWriter([Positive] int capacity, ArrayPool<byte> pool)
		{
			Contract.Positive(capacity);
			Contract.NotNull(pool);

			this.Buffer = capacity == 0 ? Array.Empty<byte>() : pool.Rent(capacity);
			this.Position = 0;
			this.Pool = pool;
		}

		/// <summary>Create a new binary writer using an existing buffer</summary>
		/// <param name="buffer">Initial buffer</param>
		/// <remarks>Since the content of the <paramref name="buffer"/> will be modified, only a temporary or scratch buffer should be used. If the writer needs to grow, a new buffer will be allocated.</remarks>
		public SliceWriter(byte[] buffer)
		{
			Contract.NotNull(buffer);

			this.Buffer = buffer;
			this.Position = 0;
			this.Pool = null;
		}

		/// <summary>Create a new binary buffer using an existing buffer and with the cursor to a specific location</summary>
		/// <remarks>Since the content of the <paramref name="buffer"/> will be modified, only a temporary or scratch buffer should be used. If the writer needs to grow, a new buffer will be allocated.</remarks>
		public SliceWriter(byte[] buffer, int index)
		{
			Contract.NotNull(buffer);
			Contract.Between(index, 0, buffer.Length);

			this.Buffer = buffer;
			this.Position = index;
			this.Pool = null;
		}

		/// <summary>Creates a new binary buffer, initialized by copying pre-existing data</summary>
		/// <param name="prefix">Data that will be copied at the start of the buffer</param>
		/// <param name="capacity">Optional initial capacity of the buffer</param>
		/// <remarks>The cursor will already be placed at the end of the prefix</remarks>
		public SliceWriter(Slice prefix, int capacity = 0)
		{
			prefix.EnsureSliceIsValid();
			Contract.Positive(capacity);

			var pool = ArrayPool<byte>.Shared;
			int n = prefix.Count;
			Contract.Debug.Assert(n >= 0);

			if (capacity == 0)
			{ // most frequent usage is to add a packed integer at the end of a prefix
				capacity = BitHelpers.AlignPowerOfTwo(n + 8, 16);
			}
			else
			{
				capacity = BitHelpers.AlignPowerOfTwo(Math.Max(capacity, n), 16);
			}

			var buffer = pool.Rent(capacity);
			if (n > 0) prefix.CopyTo(buffer, 0);

			this.Buffer = buffer;
			this.Position = n;
			this.Pool = pool;
		}

		#endregion

		#region Public Properties...

		/// <summary>Returns true if the buffer contains at least some data</summary>
		public bool HasData
		{
			[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
			get => this.Position > 0;
		}

		/// <summary>Capacity of the internal buffer</summary>
		public int Capacity
		{
			[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
			get => this.Buffer?.Length ?? 0;
		}

		/// <summary>Return the byte at the specified index</summary>
		/// <param name="index">Index in the buffer (0-based if positive, from the end if negative)</param>
		public byte this[int index]
		{
			[Pure]
			get
			{
				int pos = this.Position;
				Contract.Debug.Assert(this.Buffer != null && pos >= 0);
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
				return count > 0 ? new Slice(this.Buffer!, from, count) : Slice.Empty;
			}
		}

#if USE_RANGE_API

		public byte this[Index index] => this[index.GetOffset(this.Position)];

		public Slice this[Range range] => Substring(range);

#endif

		#endregion

		/// <summary>Returns the underlying buffer holding the data</summary>
		/// <remarks>This will never return until, unlike <see cref="Buffer"/> which can be null if the instance was never written to.</remarks>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public byte[] GetBufferUnsafe() => this.Buffer ?? Array.Empty<byte>();

		/// <summary>Returns a byte array filled with the contents of the buffer</summary>
		/// <remarks>The buffer is copied in the byte array. And change to one will not impact the other</remarks>
		[Pure]
		public byte[] GetBytes()
		{
			int p = this.Position;
			return p != 0 ? this.Buffer.AsSpan(0, p).ToArray() : Array.Empty<byte>();
		}

		/// <summary>Returns a buffer segment pointing to the content of the buffer</summary>
		/// <remarks>Any change to the segment will change the buffer !</remarks>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public ArraySegment<byte> ToArraySegment()
		{
			return ToSlice();
		}

		/// <summary>Returns a <see cref="ReadOnlySpan{T}">ReadOnlySpan&lt;byte&gt;</see> pointing to the content of the buffer</summary>
		[Pure]
		public ReadOnlySpan<byte> ToSpan()
		{
			var buffer = this.Buffer;
			var p = this.Position;
			if (buffer == null || p == 0)
			{ // empty buffer
				return default;
			}
			Contract.Debug.Assert(buffer.Length >= p, "Current position is outside of the buffer");
			return new ReadOnlySpan<byte>(buffer, 0, p);
		}

		/// <summary>Returns a <see cref="Slice"/> pointing to the content of the buffer</summary>
		/// <remarks>Any change to the slice will change the buffer !</remarks>
		[Pure]
		public Slice ToSlice()
		{
			var buffer = this.Buffer;
			var p = this.Position;
			if (buffer == null || p == 0)
			{ // empty buffer
				return Slice.Empty;
			}
			Contract.Debug.Assert(buffer.Length >= p, "Current position is outside of the buffer");
			return new Slice(buffer, 0, p);
		}

		/// <summary>Returns a <see cref="MutableSlice"/> pointing to the content of the buffer</summary>
		/// <remarks>Any change to the slice will change the buffer !</remarks>
		[Pure]
		public MutableSlice ToMutableSlice()
		{
			var buffer = this.Buffer;
			var p = this.Position;
			if (buffer == null || p == 0)
			{ // empty buffer
				return MutableSlice.Empty;
			}
			Contract.Debug.Assert(buffer.Length >= p, "Current position is outside of the buffer");
			return new MutableSlice(buffer, 0, p);
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
			return new Slice(this.Buffer!, 0, count);
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
			return new Slice(this.Buffer!, 0, (int) count);
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
			return new Slice(this.Buffer!, p - count, count);
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
			return new Slice(this.Buffer!, p - (int) count, (int) count);
		}

		/// <summary>Returns a slice pointing to a segment inside the buffer</summary>
		/// <param name="offset">Offset of the segment from the start of the buffer</param>
		/// <remarks>Any change to the slice will change the buffer !</remarks>
		/// <exception cref="ArgumentException">If <paramref name="offset"/> is less then zero, or after the current position</exception>
		[Pure]
		public Slice Substring(int offset) //REVIEW: => Slice(offset)
		{
			int p = this.Position;
			if (offset < 0 || offset > p) throw ThrowHelper.ArgumentException(nameof(offset), "Offset must be inside the buffer");
			int count = p - offset;
			return count > 0 ? new Slice(this.Buffer!, offset, p - offset) : Slice.Empty;
		}

		/// <summary>Returns a slice pointing to a segment inside the buffer</summary>
		/// <param name="offset">Offset of the segment from the start of the buffer</param>
		/// <param name="count">Size of the segment</param>
		/// <remarks>Any change to the slice will change the buffer !</remarks>
		/// <exception cref="ArgumentException">If either <paramref name="offset"/> or <paramref name="count"/> are less then zero, or do not fit inside the current buffer</exception>
		[Pure]
		public Slice Substring(int offset, int count) //REVIEW: => Slice(offset, count)
		{
			int p = this.Position;
			if ((uint) offset >= p) throw ThrowHelper.ArgumentException(nameof(offset), "Offset must be inside the buffer");
			if (count < 0 | offset + count > p) throw ThrowHelper.ArgumentException(nameof(count), "The buffer is too small");

			return count > 0 ? new Slice(this.Buffer!, offset, count) : Slice.Empty;
		}

#if USE_RANGE_API

		/// <summary>Returns a slice pointing to a segment inside the buffer</summary>
		/// <param name="range">Range to return</param>
		/// <remarks>Any change to the slice will change the buffer !</remarks>
		/// <exception cref="ArgumentException">If the <paramref name="range"/> does not fit inside the current buffer</exception>
		public Slice Substring(Range range)
		{
			(int offset, int count) = range.GetOffsetAndLength(this.Position);
			return count > 0 ? new Slice(this.Buffer!, offset, count) : Slice.Empty;
		}

#endif

		/// <summary>Truncate the buffer by setting the cursor to the specified position.</summary>
		/// <param name="position">New size of the buffer</param>
		/// <remarks>If the buffer was smaller, it will be resized and filled with zeroes. If it was bigger, the cursor will be set to the specified position, but previous data will not be deleted.</remarks>
		public void SetLength(int position)
		{
			Contract.Debug.Requires(position >= 0);

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
		/// <remarks>This should be called after every successful write to the underlying stream, to update the buffer.</remarks>
		public int Flush(int bytes) //REVIEW: plutot renommer en "RemoveHead"? ou faire un vrai "RemoveAt(offset, count)" ?
		{
			if (bytes == 0) return this.Position;
			if (bytes < 0) throw ThrowHelper.ArgumentOutOfRangeException(nameof(bytes));

			if (bytes < this.Position)
			{ // copy the left over data to the start of the buffer
				int remaining = this.Position - bytes;
				this.Buffer.AsSpan(bytes, remaining).CopyTo(this.Buffer.AsSpan());
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

		/// <summary>Empties the current buffer after a successful write</summary>
		/// <param name="shrink">If <c>true</c>, release the buffer if it was large and mostly unused. If <c>false</c>, keep the same buffer independent of its current size</param>
		/// <param name="zeroes">If <c>true</c>, fill the existing buffer with zeroes, if it is reused, to ensure that no previous data can leak.</param>
		/// <remarks>If the current buffer is large enough, and less than 1/8th was used, then it will be discarded and a new smaller one will be allocated as needed</remarks>
		public void Reset(bool shrink = false, bool zeroes = false)
		{
			var buffer = this.Buffer;
			Contract.Debug.Requires(buffer != null && buffer.Length >= this.Position);
			// reduce size ?
			// If the buffer exceeds 64K and we used less than 1/8 of it the last time, we will "shrink" the buffer
			if (shrink && buffer.Length > 65536 && this.Position <= (buffer.Length >> 3))
			{ // kill the buffer
				this.Pool?.Return(buffer, zeroes);
				this.Buffer = Array.Empty<byte>();
			}
			else if (zeroes)
			{ // Clear it
				buffer.AsSpan(0, this.Position).Clear();
			}
			this.Position = 0;
		}

		/// <summary>Retourne le buffer actuel dans le pool utilisé par ce writer</summary>
		/// <remarks>ATTENTION: l'appelant ne doit PLUS accéder (en read ou write) au buffer exposé par ce writer avant l'appel à cette méthode!</remarks>
		public void Release(bool clear = false)
		{
			var buffer = this.Buffer;
			this.Buffer = Array.Empty<byte>();
			this.Position = 0;
			if (buffer != null) this.Pool?.Return(buffer, clear);
		}

		/// <summary>Advance the cursor of the buffer without writing anything, and return the previous position</summary>
		/// <param name="skip">Number of bytes to skip</param>
		/// <param name="pad">Pad value (0xFF by default)</param>
		/// <returns>Position of the cursor BEFORE moving it. Can be used as a marker to go back later and fill some value</returns>
		/// <remarks>Will fill the skipped bytes with <paramref name="pad"/></remarks>
		public int Skip([Positive] int skip, byte pad = 0xFF)
		{
			Contract.Debug.Requires(skip >= 0);

			int p = this.Position;
			if (skip == 0) return p;

			EnsureBytes(skip).AsSpan(p, skip).Fill(pad);
			this.Position = p + skip;
			return p;
		}

		/// <summary>Advance the cursor by the specified amount, and return the skipped over chunk (that can be filled later by the caller)</summary>
		/// <param name="count">Number of bytes to allocate</param>
		/// <param name="pad">Pad value (0xFF by default)</param>
		/// <returns>Slice that corresponds to the reserved segment in the buffer</returns>
		/// <remarks>Will fill the reserved segment with <paramref name="pad"/> and the cursor will be positioned immediately after the segment.</remarks>
		public MutableSlice Allocate(int count, byte pad)
		{
			Contract.Positive(count);
			if (count == 0) return MutableSlice.Empty;

			int offset = Skip(count, pad);
			return new MutableSlice(this.Buffer!, offset, count);
		}

		/// <summary>Advance the cursor by the specified amount, and return the skipped over chunk (that can be filled later by the caller)</summary>
		/// <param name="count">Number of bytes to allocate</param>
		/// <returns>Slice that corresponds to the reserved segment in the buffer</returns>
		public MutableSlice Allocate(int count)
		{
			Contract.Positive(count);
			if (count == 0) return MutableSlice.Empty;

			var buffer = EnsureBytes(count);
			int p = this.Position;
			this.Position = p + count;
			return new MutableSlice(buffer, p, count);
		}

		/// <summary>Advance the cursor by the amount required end up on an aligned byte position</summary>
		/// <param name="alignment">Number of bytes to align to</param>
		/// <param name="pad">Pad value (0 by default)</param>
		public void Align(int alignment, byte pad = 0)
		{
			Contract.Debug.Requires(alignment > 0);
			int r = this.Position % alignment;
			if (r > 0) Skip(alignment - r, pad);
		}

		/// <summary>Rewinds the cursor to a previous position in the buffer, while saving the current position</summary>
		/// <param name="cursor">Will receive the current cursor position</param>
		/// <param name="position">Previous position in the buffer</param>
		public void Rewind(out int cursor, int position)
		{
			Contract.Debug.Requires(position >= 0 && position <= this.Position);
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

		/// <summary>Dangerously write a single byte at the end of the buffer, without any capacity checks!</summary>
		/// <remarks>
		/// This method DOES NOT check the buffer capacity before writing, and caller MUST have resized the buffer beforehand!
		/// Failure to do so may introduce memory correction (buffer overflow!).
		/// This should ONLY be used in performance-sensitive code paths that have been audited thoroughly!
		/// </remarks>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void UnsafeWriteByte(byte value)
		{
			Contract.Debug.Requires(this.Buffer != null && this.Position < this.Buffer.Length);
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
			Contract.Debug.Requires(this.Buffer != null && this.Position + 1 < this.Buffer.Length);
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
			Contract.Debug.Requires(this.Buffer != null && this.Position + 2 < this.Buffer.Length);
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
			Contract.Debug.Requires(this.Buffer != null && this.Position + 3 < this.Buffer.Length);
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
			Contract.Debug.Requires(this.Buffer != null && this.Position + 4 < this.Buffer.Length);
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
		public void WriteBytes(byte[]? data)
		{
			if (data != null)
			{
				WriteBytes(data.AsSpan());
			}
		}

		/// <summary>Write a chunk of a byte array to the end of the buffer</summary>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		[Obsolete("Use ReadOnlySpan<byte> instead.")]
		public void WriteBytes(byte[] data, int offset, int count)
		{
			if (count > 0)
			{
				WriteBytes(data.AsSpan(offset, count));
			}
		}

		/// <summary>Write a segment of bytes to the end of the buffer</summary>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void WriteBytes(Slice data)
		{
			WriteBytes(data.Span);
		}

		/// <summary>Write a segment of bytes to the end of the buffer</summary>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void WriteBytes(MutableSlice data)
		{
			WriteBytes(data.Span);
		}

		/// <summary>Write a segment of bytes to the end of the buffer</summary>
		public void WriteBytes(ReadOnlySpan<byte> data)
		{
			int count = data.Length;
			if (count > 0)
			{
				int p = this.Position;
				data.CopyTo(EnsureBytes(count).AsSpan(p));
				this.Position = checked(p + count);
			}
		}

		/// <summary>Write a chunk of a byte array to the end of the buffer, with a prefix</summary>
		[Obsolete("Use ReadOnlySpan<byte> instead.")]
		public void WriteBytes(byte prefix, byte[] data, int offset, int count)
		{
			WriteBytes(prefix, count != 0 ? data.AsSpan(offset, count) : default);
		}

		/// <summary>Write a segment of bytes to the end of the buffer, with a prefix</summary>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void WriteBytes(byte prefix, Slice data)
		{
			WriteBytes(prefix, data.Span);
		}

		/// <summary>Write a segment of bytes to the end of the buffer, with a prefix</summary>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void WriteBytes(byte prefix, MutableSlice data)
		{
			WriteBytes(prefix, data.Span);
		}

		/// <summary>Write a segment of bytes to the end of the buffer, with a prefix</summary>
		public void WriteBytes(byte prefix, ReadOnlySpan<byte> data)
		{
			int count = data.Length;
			var buffer = EnsureBytes(checked(count + 1));
			int p = this.Position;
			buffer[p] = prefix;
			if (count > 0) data.CopyTo(buffer.AsSpan(p + 1));
			this.Position = checked(p + count + 1);
		}

		/// <summary>Dangerously write a segment of bytes at the end of the buffer, without any capacity checks!</summary>
		/// <remarks>
		/// This method DOES NOT check the buffer capacity before writing, and caller MUST have resized the buffer beforehand!
		/// Failure to do so may introduce memory correction (buffer overflow!).
		/// This should ONLY be used in performance-sensitive code paths that have been audited thoroughly!
		/// </remarks>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void UnsafeWriteBytes(Slice data)
		{
			UnsafeWriteBytes(data.Span);
		}

		/// <summary>Dangerously write a segment of bytes at the end of the buffer, without any capacity checks!</summary>
		/// <remarks>
		/// This method DOES NOT check the buffer capacity before writing, and caller MUST have resized the buffer beforehand!
		/// Failure to do so may introduce memory correction (buffer overflow!).
		/// This should ONLY be used in performance-sensitive code paths that have been audited thoroughly!
		/// </remarks>
		public void UnsafeWriteBytes(ReadOnlySpan<byte> data)
		{
			if (data.Length != 0)
			{
				int p = this.Position;
				Contract.Debug.Requires(this.Buffer != null && p >= 0 && data != null && p + data.Length <= this.Buffer.Length);

				int q = checked(p + data.Length);
				data.CopyTo(this.Buffer.AsSpan(p));
				this.Position = q;
			}
		}

		// Appending is used when the caller want to get a Slice that points to the location where the bytes where written in the internal buffer

		/// <summary>Append a byte array to the end of the buffer</summary>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public Slice AppendBytes(byte[]? data)
		{
			return data != null ? AppendBytes(data.AsSpan()) : Slice.Empty;
		}

		/// <summary>Append a chunk of a byte array to the end of the buffer</summary>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		[Obsolete("Use ReadOnlySpan<byte> instead.")]
		public Slice AppendBytes(byte[] data, int offset, int count)
		{
			return count != 0 ? AppendBytes(data.AsSpan(offset, count)) : Slice.Empty;
		}

		/// <summary>Append a segment of bytes to the end of the buffer</summary>
		/// <param name="data">Buffer containing the data to append</param>
		/// <returns>Slice that maps the interned data using the writer's buffer.</returns>
		/// <remarks>If you do not need the resulting Slice, you should call <see cref="WriteBytes(Slice)"/> instead!</remarks>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public Slice AppendBytes(Slice data)
		{
			return AppendBytes(data.Span);
		}

		/// <summary>Append a segment of bytes to the end of the buffer</summary>
		/// <param name="data">Buffer containing the data to append</param>
		/// <returns>Slice that maps the interned data using the writer's buffer.</returns>
		/// <remarks>If you do not need the resulting Slice, you should call <see cref="WriteBytes(Slice)"/> instead!</remarks>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public Slice AppendBytes(MutableSlice data)
		{
			return AppendBytes(data.Span);
		}

		/// <summary>Append a segment of bytes to the end of the buffer</summary>
		/// <param name="data">Buffer containing the data to append</param>
		/// <returns>Slice that maps the interned data using the writer's buffer.</returns>
		/// <remarks>If you do not need the resulting Slice, you should call <see cref="WriteBytes(Slice)"/> instead!</remarks>
		[Pure]
		public Slice AppendBytes(ReadOnlySpan<byte> data)
		{
			int count = data.Length;
			if (count == 0) return Slice.Empty;

			int p = this.Position;
			var buffer = EnsureBytes(count);
			data.CopyTo(buffer.AsSpan(p));
			this.Position = checked(p + count);
			return new Slice(buffer, p, count);
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
			//note: if the size if 64-bits, we probably expect values to always be way above 128 so no need to optimize for this case here

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
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void WriteVarBytes(byte[] bytes)
		{
			Contract.Debug.Requires(bytes != null);
			WriteVarBytes(bytes.AsSpan());
		}

		/// <summary>Writes a length-prefixed byte array, and advances the cursor</summary>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		[Obsolete("Use ReadOnlySpan<byte> instead.")]
		public void WriteVarBytes(byte[] bytes, int offset, int count)
		{
			Contract.Debug.Requires(count == 0 || bytes != null);
			WriteVarBytes(bytes.AsSpan(offset, count));
		}

		/// <summary>Writes a length-prefixed byte array, and advances the cursor</summary>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void WriteVarBytes(Slice value)
		{
			WriteVarBytes(value.Span);
		}

		/// <summary>Writes a length-prefixed byte array, and advances the cursor</summary>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void WriteVarBytes(MutableSlice value)
		{
			WriteVarBytes(value.Span);
		}

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
			if (n > 0) value.CopyTo(buffer.AsSpan(p + 1));
			this.Position = checked(p + n + 1);
		}

		private void WriteVarBytesSlow(ReadOnlySpan<byte> value)
		{
			int n = value.Length;
			var buffer = EnsureBytes(checked(n + 5));

			// write the count
			WriteVarInt32((uint) n);
			Contract.Debug.Assert(this.Buffer == buffer);

			// write the bytes
			int p = this.Position;
			value.CopyTo(buffer.AsSpan(p));
			this.Position = checked(p + n);
		}

		#endregion

		#region VarString...

		// all VarStrings are encoded as a VarInt that contains the number of following encoded bytes
		// => caller MUST KNOWN the encoding! (usually UTF-8)
		// => the string's length is NOT stored!

		/// <summary>Write a variable-sized string, using the specified encoding</summary>
		public void WriteVarString(string? value, Encoding? encoding = null)
		{
			if (encoding == null)
			{
				WriteVarStringUtf8(value);
				return;
			}
			uint byteCount;
			if (value == null || (byteCount = checked((uint) encoding.GetByteCount(value))) == 0)
			{
				WriteByte(0);
				return;
			}
			var buffer = EnsureBytes(byteCount + UnsafeHelpers.SizeOfVarBytes(byteCount));

			// write the count
			WriteVarInt32(byteCount);
			Contract.Debug.Assert(this.Buffer == buffer);

			// write the chars
			int p = this.Position;
			int n = encoding.GetBytes(s: value, charIndex: 0, charCount: value.Length, bytes: buffer, byteIndex: p);
			this.Position = checked(p + n);
		}

		/// <summary>Write a variable-sized string, encoded using UTF-8</summary>
		/// <param name="value">String to append</param>
		/// <remarks>The null and empty string will be stored the same way. Caller must use a different technique if they must be stored differently.</remarks>
		public void WriteVarStringUtf8(string? value)
		{
			// Format:
			// - VarInt     Number of following bytes
			// - Byte[]     UTF-8 encoded bytes
			// Examples:
			// - "" => { 0x00 }
			// - "ABC" => { 0x03 'A' 'B' 'C' }
			// - "Héllo" => { 0x06 'h' 0xC3 0xA9 'l' 'l' 'o' }

			// We need to know the encoded size beforehand, because we need to write the size first!
			int byteCount;
			if (value == null || (byteCount = Encoding.UTF8.GetByteCount(value)) == 0)
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
			Contract.Debug.Assert(value != null && byteCount > 0 && byteCount >= value.Length);
			var buffer = EnsureBytes(byteCount + UnsafeHelpers.SizeOfVarBytes(byteCount));
			WriteVarInt32((uint)byteCount);
			int p = this.Position;
			int n = Encoding.UTF8.GetBytes(s: value, charIndex: 0, charCount: value.Length, bytes: buffer, byteIndex: p);
			this.Position = checked(p + n);
		}

		/// <summary>Write a variable-sized string, which is known to only contain ASCII characters (0..127)</summary>
		/// <remarks>This is faster than <see cref="WriteVarString(string, Encoding)"/> when the caller KNOWS that the string is ASCII only. This should only be used with keywords and constants, NOT with user input!</remarks>
		/// <exception cref="ArgumentException">If the string contains characters above 127</exception>
		public void WriteVarStringAscii(string? value)
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
			Contract.Debug.Requires(!string.IsNullOrEmpty(value));

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
		public void WriteUuid128(in Uuid128 value)
		{
			value.WriteToUnsafe(AllocateSpan(Uuid128.SizeOf));
		}

		/// <summary>Write a 96-bit UUID, and advances the cursor</summary>
		public void WriteUuid96(in Uuid96 value)
		{
			value.WriteToUnsafe(AllocateSpan(Uuid96.SizeOf));
		}

		/// <summary>Write a 80-bit UUID, and advances the cursor</summary>
		public void WriteUuid80(in Uuid80 value)
		{
			value.WriteToUnsafe(AllocateSpan(Uuid80.SizeOf));
		}

		/// <summary>Write a 128-bit UUID, and advances the cursor</summary>
		public void WriteUuid64(Uuid64 value)
		{
			value.WriteToUnsafe(AllocateSpan(Uuid64.SizeOf));
		}

		#endregion

		#region Fixed-Size Text

		/// <summary>Write a string using UTF-8</summary>
		/// <param name="value">Text to write</param>
		/// <returns>Number of bytes written</returns>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public int WriteString(string? value)
		{
			return WriteStringUtf8(value);
		}

		/// <summary>Write a string using UTF-8</summary>
		/// <param name="value">Text to write</param>
		/// <returns>Number of bytes written</returns>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public int WriteString(ReadOnlySpan<char> value)
		{
			return WriteStringUtf8(value);
		}

		/// <summary>Write a string using the specified encoding</summary>
		/// <param name="value">Text to write</param>
		/// <param name="encoding">Encoding used to convert the text to bytes</param>
		/// <returns>Number of bytes written</returns>
		public int WriteString(string? value, Encoding encoding)
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

		/// <summary>Write an already UTF-8 encoded string</summary>
		/// <returns>Number of bytes written</returns>
		public int WriteString(Utf8String value)
		{
			if (value.IsNullOrEmpty) return 0;
			int n = value.Buffer.Count;
			var buffer = EnsureBytes(n);
			int p = this.Position;
			value.Buffer.CopyTo(buffer, p);
			this.Position = p + n;
			return n;
		}

		/// <summary>Write a string using UTF-8</summary>
		/// <param name="value">Text to write</param>
		/// <returns>Number of bytes written</returns>
		public int WriteStringUtf8(string? value)
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

		/// <summary>Write a string using UTF-8</summary>
		/// <returns>Number of bytes written</returns>
		[Obsolete("Use ReadOnlySpan<char> instead.")]
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
					// afin d'éviter de gaspiller trop de mémoire (potentiellement jusqu'à 6 fois la taille)
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

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public int WriteStringUtf8(char value)
		{
			if (value < 0x80)
			{
				WriteByte((byte) value);
				return 1;
			}
			return WriteStringUtf8Slow(value);
		}

		private int WriteStringUtf8Slow(char value)
		{
			int p = this.Position;
			if (!Utf8Encoder.TryWriteUnicodeCodePoint(ref this, (UnicodeCodePoint)value))
			{
				throw FailInvalidUtf8CodePoint();
			}
			return this.Position - p;
		}

		[Pure, MethodImpl(MethodImplOptions.NoInlining)]
		private static Exception FailInvalidUtf8CodePoint()
		{
			return new DecoderFallbackException("Failed to encode invalid Unicode CodePoint into UTF-8");
		}

		/// <summary>Write a string that only contains ASCII</summary>
		/// <param name="value">String with characters only in the 0..127 range</param>
		/// <remarks>Faster than <see cref="WriteString(string, Encoding)"/> when writing Magic Strings or ascii keywords</remarks>
		/// <returns>Number of bytes written</returns>
		public int WriteStringAscii(string? value)
		{
			if (string.IsNullOrEmpty(value)) return 0;

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
			data.Span.CopyTo(this.Buffer.AsSpan(index));
		}

		/// <summary>Overwrite a section of the buffer that was already written, with the specified data</summary>
		/// <param name="index">Offset from the start of the buffer where to start replacing</param>
		/// <param name="data">Data that will overwrite the buffer at the specified <paramref name="index"/></param>
		/// <remarks>You must ensure that replaced section does not overlap with the current position!</remarks>
		public void PatchBytes(int index, ReadOnlySpan<byte> data)
		{
			if (index + data.Length > this.Position) throw ThrowHelper.IndexOutOfRangeException();
			data.CopyTo(this.Buffer.AsSpan(index));
		}

		/// <summary>Overwrite a section of the buffer that was already written, with the specified data</summary>
		/// <remarks>You must ensure that replaced section does not overlap with the current position!</remarks>
		[Obsolete("Use ReadOnlySpan<byte> instead.")]
		public void PatchBytes(int index, byte[] buffer, int offset, int count)
		{
			if (index + count > this.Position) throw ThrowHelper.IndexOutOfRangeException();
			buffer.AsSpan(offset, count).CopyTo(this.Buffer.AsSpan(index));
		}

		/// <summary>Overwrite a byte of the buffer that was already written</summary>
		/// <remarks>You must ensure that replaced byte is before the current position!</remarks>
		public void PatchByte(int index, byte value)
		{
			var buffer = this.Buffer;
			if ((uint) index >= this.Position || buffer == null) throw ThrowHelper.IndexOutOfRangeException();
			buffer[index] = value;
		}

		/// <summary>Overwrite a byte of the buffer that was already written</summary>
		/// <remarks>You must ensure that replaced byte is before the current position!</remarks>
		public void PatchByte(int index, int value)
		{
			//note: convenience method, because C# compiler likes to produce 'int' when combining bits together
			var buffer = this.Buffer;
			if ((uint) index >= this.Position || buffer == null) throw ThrowHelper.IndexOutOfRangeException();
			buffer[index] = (byte) value;
		}

		#endregion

		#region 16-bits...

		/// <summary>Overwrite a word of the buffer that was already written</summary>
		/// <remarks>You must ensure that replaced word is before the current position!</remarks>
		public void PatchInt16(int index, short value)
		{
			var buffer = this.Buffer;
			if (index + 2 > this.Position || buffer == null) throw ThrowHelper.IndexOutOfRangeException();
			unsafe
			{
				fixed (byte* ptr = &buffer[index])
				{
					UnsafeHelpers.WriteFixed16Unsafe(ptr, (ushort) value);
				}
			}
		}

		/// <summary>Overwrite a word of the buffer that was already written</summary>
		/// <remarks>You must ensure that replaced word is before the current position!</remarks>
		public void PatchUInt16(int index, ushort value)
		{
			var buffer = this.Buffer;
			if (index + 2 > this.Position || buffer == null) throw ThrowHelper.IndexOutOfRangeException();
			unsafe
			{
				fixed (byte* ptr = &buffer[index])
				{
					UnsafeHelpers.WriteFixed16Unsafe(ptr, value);
				}
			}
		}

		/// <summary>Overwrite a word of the buffer that was already written</summary>
		/// <remarks>You must ensure that replaced word is before the current position!</remarks>
		public void PatchInt16BE(int index, short value)
		{
			var buffer = this.Buffer;
			if (index + 2 > this.Position || buffer == null) throw ThrowHelper.IndexOutOfRangeException();
			unsafe
			{
				fixed (byte* ptr = &buffer[index])
				{
					UnsafeHelpers.WriteFixed16BEUnsafe(ptr, (ushort) value);
				}
			}
		}

		/// <summary>Overwrite a word of the buffer that was already written</summary>
		/// <remarks>You must ensure that replaced word is before the current position!</remarks>
		public void PatchUInt16BE(int index, ushort value)
		{
			var buffer = this.Buffer;
			if (index + 2 > this.Position || buffer == null) throw ThrowHelper.IndexOutOfRangeException();
			unsafe
			{
				fixed (byte* ptr = &buffer[index])
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
			var buffer = this.Buffer;
			if (index + 4 > this.Position || buffer == null) throw ThrowHelper.IndexOutOfRangeException();
			unsafe
			{
				fixed (byte* ptr = &buffer[index])
				{
					UnsafeHelpers.WriteFixed32Unsafe(ptr, (uint) value);
				}
			}
		}

		/// <summary>Overwrite a dword of the buffer that was already written</summary>
		/// <remarks>You must ensure that replaced dword is before the current position!</remarks>
		public void PatchUInt32(int index, uint value)
		{
			var buffer = this.Buffer;
			if (index + 4 > this.Position || buffer == null) throw ThrowHelper.IndexOutOfRangeException();
			unsafe
			{
				fixed (byte* ptr = &buffer[index])
				{
					UnsafeHelpers.WriteFixed32Unsafe(ptr, value);
				}
			}
		}

		/// <summary>Overwrite a dword of the buffer that was already written</summary>
		/// <remarks>You must ensure that replaced dword is before the current position!</remarks>
		public void PatchInt32BE(int index, int value)
		{
			var buffer = this.Buffer;
			if (index + 4 > this.Position || buffer == null) throw ThrowHelper.IndexOutOfRangeException();
			unsafe
			{
				fixed (byte* ptr = &buffer[index])
				{
					UnsafeHelpers.WriteFixed32BEUnsafe(ptr, (uint) value);
				}
			}
		}

		/// <summary>Overwrite a dword of the buffer that was already written</summary>
		/// <remarks>You must ensure that replaced dword is before the current position!</remarks>
		public void PatchUInt32BE(int index, uint value)
		{
			var buffer = this.Buffer;
			if (index + 4 > this.Position || buffer == null) throw ThrowHelper.IndexOutOfRangeException();
			unsafe
			{
				fixed (byte* ptr = &buffer[index])
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
			var buffer = this.Buffer;
			if (index + 8 > this.Position || buffer == null) throw ThrowHelper.IndexOutOfRangeException();
			unsafe
			{
				fixed (byte* ptr = &buffer[index])
				{
					UnsafeHelpers.WriteFixed64Unsafe(ptr, (ulong) value);
				}
			}
		}

		/// <summary>Overwrite a qword of the buffer that was already written</summary>
		/// <remarks>You must ensure that replaced qword is before the current position!</remarks>
		public void PatchUInt64(int index, ulong value)
		{
			var buffer = this.Buffer;
			if (index + 8 > this.Position || buffer == null) throw ThrowHelper.IndexOutOfRangeException();
			unsafe
			{
				fixed (byte* ptr = &buffer[index])
				{
					UnsafeHelpers.WriteFixed64Unsafe(ptr, value);
				}
			}
		}

		/// <summary>Overwrite a qword of the buffer that was already written</summary>
		/// <remarks>You must ensure that replaced qword is before the current position!</remarks>
		public void PatchInt64BE(int index, long value)
		{
			var buffer = this.Buffer;
			if (index + 8 > this.Position || buffer == null) throw ThrowHelper.IndexOutOfRangeException();
			unsafe
			{
				fixed (byte* ptr = &buffer[index])
				{
					UnsafeHelpers.WriteFixed64BEUnsafe(ptr, (ulong) value);
				}
			}
		}

		/// <summary>Overwrite a qword of the buffer that was already written</summary>
		/// <remarks>You must ensure that replaced qword is before the current position!</remarks>
		public void PatchUInt64BE(int index, ulong value)
		{
			var buffer = this.Buffer;
			if (index + 8 > this.Position || buffer == null) throw ThrowHelper.IndexOutOfRangeException();
			unsafe
			{
				fixed (byte* ptr = &buffer[index])
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
			[Pure]
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
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public byte[] EnsureBytes(int count)
		{
			//REVIEW: en C#7 on pourrait retourner le tuple (buffer, pos) !

			Contract.Debug.Requires(count >= 0);
			var buffer = this.Buffer;
			if (buffer == null || this.Position + count > buffer.Length)
			{
				buffer = GrowBuffer(ref this.Buffer, this.Position + count, this.Pool);
				Contract.Debug.Ensures(buffer != null && buffer.Length >= this.Position + count);
			}
			return buffer;
		}

		public void Advance(int count)
		{
			int newPos = checked(this.Position + count);
			if (newPos > this.Capacity) throw ErrorCannotAdvancePastEndOfBuffer();
			this.Position = newPos;
		}

		[Pure, MethodImpl(MethodImplOptions.NoInlining)]
		private static Exception ErrorCannotAdvancePastEndOfBuffer()
		{
			return new InvalidOperationException("Cannot advance past the end of the allocated buffer.");
		}

		public Span<byte> GetSpan(int minCapacity)
		{
			Contract.Debug.Requires(minCapacity >= 0);
			var buffer = this.Buffer;
			int pos = this.Position;
			if (buffer == null || pos + minCapacity > buffer.Length)
			{
				buffer = GrowBuffer(ref this.Buffer, pos + minCapacity);
				Contract.Debug.Ensures(buffer != null && buffer.Length >= pos + minCapacity);
			}
			return buffer.AsSpan(pos);
		}
		
		public Memory<byte> GetMemory(int minCapacity)
		{
			Contract.Debug.Requires(minCapacity >= 0);
			var buffer = this.Buffer;
			int pos = this.Position;
			if (buffer == null || pos + minCapacity > buffer.Length)
			{
				buffer = GrowBuffer(ref this.Buffer, pos + minCapacity);
				Contract.Debug.Ensures(buffer != null && buffer.Length >= pos + minCapacity);
			}
			return buffer.AsMemory(pos);
		}

		public Span<byte> AllocateSpan(int count)
		{
			Contract.Debug.Requires(count >= 0);
			var buffer = this.Buffer;
			int pos = this.Position;
			int newPos = checked(pos + count);
			if (buffer == null || newPos > buffer.Length)
			{
				buffer = GrowBuffer(ref this.Buffer, newPos);
				Contract.Debug.Ensures(buffer != null && buffer.Length >= newPos);
			}
			this.Position = newPos;
			return buffer.AsSpan(pos, count);
		}

		public Memory<byte> AllocateMemory(int count)
		{
			Contract.Debug.Requires(count >= 0);
			var buffer = this.Buffer;
			int pos = this.Position;
			int newPos = checked(pos + count);
			if (buffer == null || newPos > buffer.Length)
			{
				buffer = GrowBuffer(ref this.Buffer, newPos);
				Contract.Debug.Ensures(buffer != null && buffer.Length >= newPos);
			}
			this.Position = newPos;
			return buffer.AsMemory(pos, count);
		}

		/// <summary>Ensures that we can fit the specified amount of data at the end of the buffer</summary>
		/// <param name="count">Number of bytes that will be written</param>
		/// <param name="pool"></param>
		/// <remarks>If the buffer is too small, it will be resized, and all previously written data will be copied</remarks>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public byte[] EnsureBytes(int count, ArrayPool<byte> pool)
		{
			//REVIEW: en C#7 on pourrait retourner le tuple (buffer, pos) !

			Contract.Debug.Requires(count >= 0);
			var buffer = this.Buffer;
			if (buffer == null || this.Position + count > buffer.Length)
			{
				buffer = GrowBuffer(ref this.Buffer, this.Position + count, pool);
				Contract.Debug.Ensures(buffer != null && buffer.Length >= this.Position + count);
			}
			return buffer;
		}

		/// <summary>Ensures that we can fit the specified  amount of data at the end of the buffer</summary>
		/// <param name="count">Number of bytes that will be written</param>
		/// <remarks>If the buffer is too small, it will be resized, and all previously written data will be copied</remarks>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public byte[] EnsureBytes(uint count)
		{
			return EnsureBytes(checked((int) count));
		}

		/// <summary>Ensures that we can fit data at a specific offset in the buffer</summary>
		/// <param name="offset">Offset into the buffer (from the start)</param>
		/// <param name="count">Number of bytes that will be written at this offset</param>
		/// <remarks>If the buffer is too small, it will be resized, and all previously written data will be copied</remarks>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void EnsureOffsetAndSize(int offset, int count)
		{
			Contract.Debug.Requires(offset >= 0 && count >= 0);
			if (this.Buffer == null || offset + count > this.Buffer.Length)
			{
				GrowBuffer(ref this.Buffer, offset + count, this.Pool);
			}
		}

		/// <summary>Resize a buffer by doubling its capacity</summary>
		/// <param name="buffer">Reference to the variable holding the buffer to create/resize. If null, a new buffer will be allocated. If not, the content of the buffer will be copied into the new buffer.</param>
		/// <param name="minimumCapacity">Minimum guaranteed buffer size after resizing.</param>
		/// <param name="pool">Optional pool used by this buffer</param>
		/// <remarks>The buffer will be resized to the maximum between the previous size multiplied by 2, and <paramref name="minimumCapacity"/>. The capacity will always be rounded to a multiple of 16 to reduce memory fragmentation</remarks>
		[MethodImpl(MethodImplOptions.NoInlining)]
		public static byte[] GrowBuffer(
			ref byte[]? buffer,
			int minimumCapacity = 0,
			ArrayPool<byte>? pool = null
		)
		{
			Contract.Debug.Requires(minimumCapacity >= 0);

			// double the size of the buffer, or use the minimum required
			long newSize = Math.Max(buffer == null ? 0 : (((long) buffer.Length) << 1), minimumCapacity);

			// .NET (as of 4.5) cannot allocate an array with more than 2^31 - 1 items...
			if (newSize > 0x7fffffffL) throw FailCannotGrowBuffer();

			if (pool == null)
			{ // use the heap
				int size = BitHelpers.AlignPowerOfTwo((int) newSize, 16); // round up to 16 bytes, to reduce fragmentation
				Array.Resize(ref buffer, size);
			}
			else
			{ // use the pool to resize the buffer
				int size = Math.Max((int) newSize, 64); // with a pool, we can ask for more bytes initially
				ResizeUsingPool(pool, ref buffer, size);
			}
			return buffer;
		}

		/// <summary>Resize a buffer obtained from this pool, using another (larger) buffer from the same pool</summary>
		/// <param name="pool">Buffer pool</param>
		/// <param name="array">IN: Buffer previously obtained from <see cref="pool"/>; OUT: new buffer with the same content</param>
		/// <param name="newSize">New size for the buffer</param>
		/// <remarks>
		/// If <paramref name="array"/> is null, a new buffer is allocated.
		/// If <paramref name="array"/> is already large enough, no copy is performed.
		/// Else, a new buffer is allocated from the <paramref name="pool"/>, and the content is copied other.
		/// </remarks>
		private static void ResizeUsingPool(ArrayPool<byte> pool, [System.Diagnostics.CodeAnalysis.NotNull] ref byte[]? array, int newSize)
		{
			Contract.NotNull(pool);
			Contract.Positive(newSize);

			var larray = array;
			if (larray == null)
			{
				array = pool.Rent(newSize);
				return;
			}

			if (larray.Length != newSize)
			{
				byte[] newArray = pool.Rent(newSize);
				if (larray.Length > 0)
				{
					larray.AsSpan().CopyTo(newArray);
				}
				//note: we don't return empty buffers, because we may return Array.Empty<byte>() by mistake!
				if (larray.Length != 0)
				{
					pool.Return(larray);
				}
				array = newArray;
			}
		}

		[Pure, MethodImpl(MethodImplOptions.NoInlining)]
		private static Exception FailCannotGrowBuffer()
		{
#if DEBUG
			// If you breakpoint here, that means that you probably have an unchecked maximum buffer size, or a runaway while(..) { append(..) } code in your layer code !
			// => you should ALWAYS ensure a reasonable maximum size of your allocations !
			if (System.Diagnostics.Debugger.IsAttached) System.Diagnostics.Debugger.Break();
#endif
			// note: some methods in the BCL do throw an OutOfMemoryException when attempting to allocated more than 2^31
			return new OutOfMemoryException("Buffer cannot be resized, because it would exceed the maximum allowed size");
		}

		#region ISliceSerializable

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void WriteTo(ref SliceWriter writer)
		{
			writer.WriteBytes(ToSpan());
		}

		#endregion

		[UsedImplicitly(ImplicitUseTargetFlags.WithMembers)]
		private sealed class DebugView
		{

			public DebugView(SliceWriter writer)
			{
				this.Data = writer.ToSlice();
				this.Position = writer.Position;
				this.Capacity = writer.Capacity;
			}

			public Slice Data { get; }

			public int Position { get; }

			public int Capacity { get; }

		}

	}

}

#endif
