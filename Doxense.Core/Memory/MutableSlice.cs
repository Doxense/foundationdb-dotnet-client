#region Copyright (c) 2023-2024 SnowBank SAS, (c) 2005-2023 Doxense SAS
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

#if DEPRECATED

namespace System
{
	using System.ComponentModel;
	using System.Security.Cryptography;
	using System.Text;
	using Doxense.Memory;

	/// <summary>Delimits a mutable section of a byte array</summary>
	/// <remarks>
	/// A Slice if the logical equivalent to a <see cref="Memory{T}">Memory&lt;byte&gt;</see>.
	/// It is expected that consumers of this type will somehow mutate the content of the slice (and thus the content of the backing array).
	/// If the buffer must become logically "read-only" after a step, it can be converted into a read-only <see cref="Slice"/> at any time, though the owner of the mutable slice SHOULD NOT mutate the content as long as someone is consuming the read-only version of it!
	/// </remarks>
	[/*PublicAPI,*/ ImmutableObject(true), DebuggerDisplay("Count={Count}, Offset={Offset}"), DebuggerTypeProxy(typeof(DebugView))]
	[DebuggerNonUserCode] //remove this when you need to troubleshoot this class!
	public readonly struct MutableSlice : IEquatable<MutableSlice>, IEquatable<Slice>, IEquatable<byte[]>, IComparable<MutableSlice>, IFormattable
	{
		#region Static Members...

		/// <summary>Null slice ("no segment")</summary>
		public static readonly MutableSlice Nil = default;

		/// <summary>Empty slice ("segment of 0 bytes")</summary>
		//note: we allocate a 1-byte array so that we can get a pointer to &slice.Array[slice.Offset] even for the empty slice
		public static readonly MutableSlice Empty = new (new byte[1], 0, 0);

		#endregion

		//REVIEW: Layout: should we maybe swap things around? .Count seems to be the most often touched field before the rest
		// => Should it be Array/Offset/Count (current), or Count/Offset/Array ?

		/// <summary>Pointer to the buffer (or null for <see cref="MutableSlice.Nil"/>)</summary>
		public readonly byte[]? Array;

		/// <summary>Offset of the first byte of the slice in the parent buffer</summary>
		public readonly int Offset;

		/// <summary>Number of bytes in the slice</summary>
		public readonly int Count;

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		internal MutableSlice(byte[]? array, int offset, int count)
		{
			this.Array = array;
			this.Offset = offset;
			this.Count = count;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		internal MutableSlice(byte[] array)
		{
			this.Array = array;
			this.Offset = 0;
			this.Count = array.Length;
		}

		/// <summary>Creates a slice mapping a section of a buffer</summary>
		/// <param name="buffer">Original buffer</param>
		/// <param name="offset">Offset into buffer</param>
		/// <param name="count">Number of bytes</param>
		/// <returns>Slice that maps this segment of buffer.</returns>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static MutableSlice Create(byte[] buffer, int offset, int count)
		{
			Contract.DoesNotOverflow(buffer, offset, count);
			return new MutableSlice(buffer, offset, count);
		}

		/// <summary>Creates a slice mapping a section of a buffer</summary>
		/// <param name="buffer">Original buffer</param>
		/// <returns>Slice that maps this segment of buffer.</returns>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static MutableSlice Create(byte[]? buffer)
		{
			if (buffer == null) return MutableSlice.Nil;
			if (buffer.Length == 0) return MutableSlice.Empty;
			return new MutableSlice(buffer, 0, buffer.Length);
		}

		/// <summary>Creates a new empty slice of a specified size containing all zeroes</summary>
		public static MutableSlice Zero(int size)
		{
			Contract.Positive(size);
			return size != 0 ? new MutableSlice(new byte[size], 0, size) : MutableSlice.Empty;
		}

		/// <summary>Creates a new empty slice of a specified size containing all zeroes</summary>
		[Pure]
		public static MutableSlice Zero(uint size)
		{
			Contract.LessOrEqual(size, (uint) int.MaxValue);
			return size != 0 ? new MutableSlice(new byte[size], 0, (int) size) : MutableSlice.Empty;
		}

		/// <summary>Creates a new slice with a copy of the array</summary>
		[Pure]
		public static MutableSlice Copy(byte[] source)
		{
			Contract.NotNull(source);
			if (source.Length == 0) return Empty;
			return Copy(source, 0, source.Length);
		}

		/// <summary>Creates a new slice with a copy of the array segment</summary>
		[Pure]
		public static MutableSlice Copy(byte[] source, int offset, int count)
		{
			return Copy(new ReadOnlySpan<byte>(source, offset, count));
		}

		/// <summary>Creates a new slice with a copy of the span</summary>
		[Pure]
		public static MutableSlice Copy(ReadOnlySpan<byte> source)
		{
			if (source.Length == 0) return Empty;
			var tmp = source.ToArray();
			return new MutableSlice(tmp, 0, source.Length);
		}

		/// <summary>Creates a new slice with a copy of the span, using a scratch buffer</summary>
		[Pure]
		public static MutableSlice Copy(ReadOnlySpan<byte> source, ref byte[]? buffer)
		{
			if (source.Length == 0) return Empty;
			var tmp = UnsafeHelpers.EnsureCapacity(ref buffer, BitHelpers.NextPowerOfTwo(source.Length));
			source.CopyTo(tmp);
			return new MutableSlice(tmp, 0, source.Length);
		}

		/// <summary>Implicitly converts a <see cref="MutableSlice"/> into a <see cref="Slice"/></summary>
		[Pure, DebuggerNonUserCode, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static implicit operator Slice(MutableSlice value) => value.Slice;

		/// <summary>Implicitly converts a Slice into an <see cref="Span{T}">Span&lt;byte&gt;</see></summary>
		[Pure, DebuggerNonUserCode, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static implicit operator Span<byte>(MutableSlice value)
		{
			return new Span<byte>(value.Array, value.Offset, value.Count);
		}

		/// <summary>Returns a writable <see cref="Span{T}"><c>Span&lt;byte&gt;</c></see> that wraps the content of this slice</summary>
		public Span<byte> Span
		{
			[Pure, DebuggerNonUserCode, MethodImpl(MethodImplOptions.AggressiveInlining)]
			get => this.Count != 0 ? new Span<byte>(this.Array, this.Offset, this.Count) : default;
		}

		/// <summary>Returns a writable <see cref="Memory{T}"><c>Memory&lt;byte&gt;</c></see> that wraps the content of this slice</summary>
		public Memory<byte> Memory
		{
			[Pure, DebuggerNonUserCode, MethodImpl(MethodImplOptions.AggressiveInlining)]
			get => this.Count != 0 ? new Memory<byte>(this.Array, this.Offset, this.Count) : default;
		}

		/// <summary>Returns a read-only <see cref="Slice"/> that wraps the content of this slice</summary>
		public Slice Slice
		{
			[Pure, DebuggerNonUserCode, MethodImpl(MethodImplOptions.AggressiveInlining)]
			get => this.Count != 0 ? new Slice(this.Array!, this.Offset, this.Count) : this.Array != null ? Slice.Empty : Slice.Nil;
		}

		/// <summary>Returns true is the slice is not null</summary>
		/// <remarks>An empty slice is NOT considered null</remarks>
		public bool HasValue
		{
			[Pure, DebuggerNonUserCode, MethodImpl(MethodImplOptions.AggressiveInlining)]
			get => this.Array != null;
		}

		/// <summary>Returns true if the slice is null</summary>
		/// <remarks>An empty slice is NOT considered null</remarks>
		public bool IsNull
		{
			[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
			get => this.Array == null;
		}

		/// <summary>Return true if the slice is not null but contains 0 bytes</summary>
		/// <remarks>A null slice is NOT empty</remarks>
		public bool IsEmpty
		{
			[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
			get => this.Count == 0 && this.Array != null;
		}

		/// <summary>Returns true if the slice is null or empty, or false if it contains at least one byte</summary>
		public bool IsNullOrEmpty
		{
			[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
			get => this.Count == 0;
		}

		/// <summary>Returns true if the slice contains at least one byte, or false if it is null or empty</summary>
		public bool IsPresent
		{
			[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
			get => this.Count > 0;
		}

		/// <summary>Replace <see cref="Nil"/> with <see cref="Empty"/></summary>
		/// <returns>The same slice if it is not <see cref="Nil"/>; otherwise, <see cref="Empty"/></returns>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public MutableSlice OrEmpty()
		{
			return this.Count > 0? this : Empty;
		}

		/// <summary>Return a byte array containing all the bytes of the slice, or null if the slice is null</summary>
		/// <returns>Byte array with a copy of the slice, or null</returns>
		[Pure]
		public byte[]? GetBytes()
		{
			return this.Array == null ? null : this.Span.ToArray();
		}

		/// <summary>Return a byte array containing all the bytes of the slice, or and empty array if the slice is null or empty</summary>
		/// <returns>Byte array with a copy of the slice</returns>
		[Pure]
		public byte[] GetBytesOrEmpty()
		{
			//note: this is a convenience method for code where dealing with null is a pain, or where it has already checked IsNull
			return this.Count == 0 ? [ ] : this.Span.ToArray();
		}

		/// <summary>Return a byte array containing a subset of the bytes of the slice, or null if the slice is null</summary>
		/// <returns>Byte array with a copy of a subset of the slice, or null</returns>
		[Pure]
		public byte[] GetBytes(int offset, int count)
		{
			//TODO: throw if this.Array == null ? (what does "Slice.Nil.GetBytes(..., 0)" mean ?)
			return this.Span.Slice(offset, count).ToArray();
		}

		/// <summary>Map an offset in the slice into the absolute offset in the buffer</summary>
		/// <param name="index">Relative offset (negative values mean from the end)</param>
		/// <returns>Absolute offset in the buffer</returns>
		/// <exception cref="IndexOutOfRangeException">If the index is outside the slice</exception>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private int MapToOffset(int index)
		{
			int p = NormalizeIndex(index);
			if ((uint) p >= (uint) this.Count) throw UnsafeHelpers.Errors.IndexOutOfBound();
			return checked(this.Offset + p);
		}

		/// <summary>Normalize negative index values into offset from the start</summary>
		/// <param name="index">Relative offset (negative values mean from the end)</param>
		/// <returns>Relative offset from the start of the slice</returns>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		private int NormalizeIndex(int index)
		{
			return index < 0 ? checked(index + this.Count) : index;
		}

		/// <summary>Returns the value of one byte in the slice</summary>
		/// <param name="index">Offset of the byte (negative values means start from the end)</param>
		public byte this[int index]
		{
			[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
			get => this.Array![MapToOffset(index)];
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			set => this.Array![MapToOffset(index)] = value;
		}

		/// <summary>Returns a reference to a specific position in the slice</summary>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		[EditorBrowsable(EditorBrowsableState.Never)]
		public ref readonly byte ItemRef(int index)
		{
			return ref this.Array![MapToOffset(index)];
		}

		/// <summary>Returns a substring of the current slice that fits withing the specified index range</summary>
		/// <param name="start">The starting position of the substring. Positive values means from the start, negative values means from the end</param>
		/// <param name="end">The end position (excluded) of the substring. Positive values means from the start, negative values means from the end</param>
		/// <returns>Subslice</returns>
		public MutableSlice this[int start, int end]
		{
			get
			{
				start = NormalizeIndex(start);
				end = NormalizeIndex(end);

				// bound check
				if (start < 0) start = 0;
				if (end > this.Count) end = this.Count;

				if (start >= end) return MutableSlice.Empty;
				if (start == 0 && end == this.Count) return this;

				checked { return new MutableSlice(this.Array, this.Offset + start, end - start); }
			}
			set
			{
				var chunk = this[start, end];
				if (chunk.Count != value.Count) throw new ArgumentException("Replacement slice must have the same size as the selected range");
				value.CopyTo(chunk.Span);
			}
		}

		/// <summary>
		/// Returns a reference to the first byte in the slice.
		/// If the slice is empty, returns a reference to the location where the first character would have been stored.
		/// Such a reference can be used for pinning but must never be de-referenced.
		/// </summary>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		[EditorBrowsable(EditorBrowsableState.Never)]
		public ref byte DangerousGetPinnableReference()
		{
			//note: this is the equivalent of MemoryMarshal.GetReference(..) and does not check for the 0-length case!
			return ref this.Array![this.Offset];
		}

		/// <summary>
		/// Returns a reference to the 0th element of the Span. If the Span is empty, returns null reference.
		/// It can be used for pinning and is required to support the use of span within a fixed statement.
		/// </summary>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		[EditorBrowsable(EditorBrowsableState.Never)]
		public ref byte GetPinnableReference()
		{
			unsafe
			{
				return ref (this.Count != 0) ? ref this.Array![this.Offset] : ref Unsafe.AsRef<byte>(null);
			}
		}

		/// <summary>Copy this slice into another buffer, and move the cursor</summary>
		/// <param name="buffer">Buffer where to copy this slice</param>
		/// <remarks>Updated buffer that starts after the copied slice</remarks>
		public Span<byte> WriteTo(Span<byte> buffer)
		{
			if (buffer.Length == 0) return buffer;
			this.Span.CopyTo(buffer);
			return buffer.Slice(this.Count);
		}

		public void CopyTo(Span<byte> destination)
		{
			this.Span.CopyTo(destination);
		}

		public bool TryCopyTo(Span<byte> destination)
		{
			return this.Span.TryCopyTo(destination);
		}

		/// <summary>Retrieves a substring from this instance. The substring starts at a specified character position.</summary>
		/// <param name="offset">The starting position of the substring. Positive values means from the start, negative values means from the end</param>
		/// <returns>A slice that is equivalent to the substring that begins at <paramref name="offset"/> (from the start or the end depending on the sign) in this instance, or Slice.Empty if <paramref name="offset"/> is equal to the length of the slice.</returns>
		/// <remarks>The substring does not copy the original data, and refers to the same buffer as the original slice. Any change to the parent slice's buffer will be seen by the substring. You must call Memoize() on the resulting substring if you want a copy</remarks>
		/// <example>{"ABCDE"}.Substring(0) => {"ABC"}
		/// {"ABCDE"}.Substring(1} => {"BCDE"}
		/// {"ABCDE"}.Substring(-2} => {"DE"}
		/// {"ABCDE"}.Substring(5} => Slice.Empty
		/// Slice.Empty.Substring(0) => Slice.Empty
		/// Slice.Nil.Substring(0) => Slice.Empty
		/// </example>
		/// <exception cref="System.ArgumentOutOfRangeException"><paramref name="offset"/> indicates a position not within this instance, or <paramref name="offset"/> is less than zero</exception>
		[Pure]
		public MutableSlice Substring(int offset)
		{
			int len = this.Count;

			// negative values mean from the end
			if (offset < 0) offset += this.Count;
			//REVIEW: TODO: get rid of negative indexing, and create a different "substring from the end" method?

			// bound check
			if ((uint) offset > (uint) len) UnsafeHelpers.Errors.ThrowOffsetOutsideSlice();

			int r = len - offset;
			return r != 0 ? new MutableSlice(this.Array, this.Offset + offset, r) : MutableSlice.Empty;
		}

		/// <summary>Retrieves a substring from this instance. The substring starts at a specified character position and has a specified length.</summary>
		/// <param name="offset">The starting position of the substring. Positive values means from the start, negative values means from the end</param>
		/// <param name="count">Number of bytes in the substring</param>
		/// <returns>A slice that is equivalent to the substring of length <paramref name="count"/> that begins at <paramref name="offset"/> (from the start or the end depending on the sign) in this instance, or Slice.Empty if count is zero.</returns>
		/// <remarks>The substring does not copy the original data, and refers to the same buffer as the original slice. Any change to the parent slice's buffer will be seen by the substring. You must call Memoize() on the resulting substring if you want a copy</remarks>
		/// <example>{"ABCDE"}.Substring(0, 3) => {"ABC"}
		/// {"ABCDE"}.Substring(1, 3} => {"BCD"}
		/// {"ABCDE"}.Substring(-2, 2} => {"DE"}
		/// Slice.Empty.Substring(0, 0) => Slice.Empty
		/// Slice.Nil.Substring(0, 0) => Slice.Empty
		/// </example>
		/// <exception cref="System.ArgumentOutOfRangeException"><paramref name="offset"/> plus <paramref name="count"/> indicates a position not within this instance, or <paramref name="offset"/> or <paramref name="count"/> is less than zero</exception>
		[Pure]
		public MutableSlice Substring(int offset, int count)
		{
			if (count == 0) return MutableSlice.Empty;
			int len = this.Count;

			// bound check
			if ((uint) offset >= (uint) len || (uint) count > (uint)(len - offset)) UnsafeHelpers.Errors.ThrowOffsetOutsideSlice();

			return new MutableSlice(this.Array, this.Offset + offset, count);
		}

		/// <summary>Returns the first key lexicographically that does not have the passed in <paramref name="slice"/> as a prefix</summary>
		/// <param name="slice">Slice to increment</param>
		/// <returns>New slice that is guaranteed to be the first key lexicographically higher than <paramref name="slice"/> which does not have <paramref name="slice"/> as a prefix</returns>
		/// <remarks>If the last byte is already equal to 0xFF, it will rollover to 0x00 and the next byte will be incremented.</remarks>
		/// <exception cref="ArgumentException">If the Slice is equal to Slice.Nil</exception>
		/// <exception cref="OverflowException">If the Slice is the empty string or consists only of 0xFF bytes</exception>
		/// <example>
		/// Slice.Increment(Slice.FromString("ABC")) => "ABD"
		/// Slice.Increment(Slice.FromHexa("01 FF")) => { 02 }
		/// </example>
		public static MutableSlice Increment(Slice slice)
		{
			if (slice.IsNull) throw ThrowHelper.ArgumentException(nameof(slice), "Cannot increment null buffer");

			int lastNonFfByte;
			var tmp = slice.ToArray();
			for (lastNonFfByte = tmp.Length - 1; lastNonFfByte >= 0; --lastNonFfByte)
			{
				if (tmp[lastNonFfByte] != 0xFF)
				{
					++tmp[lastNonFfByte];
					break;
				}
			}

			if (lastNonFfByte < 0)
			{
				throw ThrowHelper.ArgumentException(nameof(slice), "Cannot increment key");
			}

			return new MutableSlice(tmp, 0, lastNonFfByte + 1);
		}

		/// <summary>Creates a new slice that contains the same byte repeated</summary>
		/// <param name="value">Byte that will fill the slice</param>
		/// <param name="count">Number of bytes</param>
		/// <returns>New slice that contains <paramref name="count"/> times the byte <paramref name="value"/>.</returns>
		public static MutableSlice Repeat(byte value, int count)
		{
			Contract.Positive(count, message: "count", paramName: nameof(count));
			if (count == 0) return MutableSlice.Empty;

			var res = new byte[count];
			res.AsSpan().Fill(value);
			return new MutableSlice(res, 0, res.Length);
		}

		/// <summary>Creates a new slice that contains the same byte repeated</summary>
		/// <param name="value">ASCII character (between 0 and 255) that will fill the slice. If <paramref name="value"/> is greater than 0xFF, only the 8 lowest bits will be used</param>
		/// <param name="count">Number of bytes</param>
		/// <returns>New slice that contains <paramref name="count"/> times the byte <paramref name="value"/>.</returns>
		public static MutableSlice Repeat(char value, int count)
		{
			Contract.Positive(count, message: "count", paramName: nameof(count));
			if (count == 0) return MutableSlice.Empty;

			var res = new byte[count];
			res.AsSpan().Fill((byte) value);
			return new MutableSlice(res, 0, res.Length);
		}

		/// <summary>Create a new slice filled with random bytes taken from a random number generator</summary>
		/// <param name="prng">Pseudo random generator to use (needs locking if instance is shared)</param>
		/// <param name="count">Number of random bytes to generate</param>
		/// <returns>Slice of <paramref name="count"/> bytes taken from <paramref name="prng"/></returns>
		/// <remarks>Warning: <see cref="System.Random"/> is not thread-safe ! If the <paramref name="prng"/> instance is shared between threads, then it needs to be locked before calling this method.</remarks>
		public static MutableSlice Random(Random prng, int count)
		{
			Contract.NotNull(prng);
			if (count < 0) throw ThrowHelper.ArgumentOutOfRangeException(nameof(count), count, "Count cannot be negative");
			if (count == 0) return MutableSlice.Empty;

			var bytes = new byte[count];
			prng.NextBytes(bytes);
			return new MutableSlice(bytes, 0, count);
		}

		/// <summary>Create a new slice filled with random bytes taken from a cryptographic random number generator</summary>
		/// <param name="rng">Random generator to use (needs locking if instance is shared)</param>
		/// <param name="count">Number of random bytes to generate</param>
		/// <param name="nonZeroBytes">If true, produce a sequence of non-zero bytes.</param>
		/// <returns>Slice of <paramref name="count"/> bytes taken from <paramref name="rng"/></returns>
		/// <remarks>Warning: All RNG implementations may not be thread-safe ! If the <paramref name="rng"/> instance is shared between threads, then it may need to be locked before calling this method.</remarks>
		public static MutableSlice Random(RandomNumberGenerator rng, int count, bool nonZeroBytes = false)
		{
			Contract.NotNull(rng);
			if (count < 0) throw ThrowHelper.ArgumentOutOfRangeException(nameof(count), count, "Count cannot be negative");
			if (count == 0) return MutableSlice.Empty;

			var bytes = new byte[count];

			if (nonZeroBytes)
				rng.GetNonZeroBytes(bytes);
			else
				rng.GetBytes(bytes);

			return new MutableSlice(bytes, 0, count);
		}

		/// <summary>Returns a printable representation of the key</summary>
		/// <remarks>You can roundtrip the result of calling slice.ToString() by passing it to <see cref="System.Slice.Unescape(string?)"/>(string) and get back the original slice.</remarks>
		public override string ToString()
		{
			return this.Slice.ToString();
		}

		public string ToString(string? format)
		{
			return this.Slice.ToString(format, null);
		}

		/// <summary>Formats the slice using the specified encoding</summary>
		/// <param name="format">A single format specifier that indicates how to format the value of this Slice. The <paramref name="format"/> parameter can be "N", "D", "X", or "P". If format is null or an empty string (""), "D" is used. A lower case character will usually produce lowercased hexadecimal letters.</param>
		/// <param name="provider">This parameter is not used</param>
		/// <returns></returns>
		/// <remarks>
		/// The format <b>D</b> is the default, and produce a roundtrip-able version of the slice, using &lt;XX&gt; tokens for non-printable bytes.
		/// The format <b>N</b> (or <b>n</b>) produces a compact hexadecimal string (without separators).
		/// The format <b>X</b> (or <b>x</b>) produces an hexadecimal string with spaces between each bytes.
		/// The format <b>P</b> is the equivalent of calling <see cref="Slice.PrettyPrint()"/>.
		/// </remarks>
		public string ToString(string? format, IFormatProvider? provider)
		{
			return this.Slice.ToString(format, provider);
		}

		#region Equality, Comparison...

		/// <summary>Checks if an object is equal to the current slice</summary>
		/// <param name="obj">Object that can be either another slice, a byte array, or a byte array segment.</param>
		/// <returns>true if the object represents a sequence of bytes that has the same size and same content as the current slice.</returns>
		public override bool Equals(object? obj)
		{
			switch (obj)
			{
				case null: return this.Array == null;
				case MutableSlice slice: return Equals(slice);
				case Slice slice: return Equals(slice);
				case byte[] bytes: return Equals(bytes);
			}
			return false;
		}

		/// <summary>Gets the hash code for this slice</summary>
		/// <returns>A 32-bit signed hash code calculated from all the bytes in the slice.</returns>
		public override int GetHashCode()
		{
			EnsureSliceIsValid();
			return this.Array == null ? 0 : UnsafeHelpers.ComputeHashCode(this.Span);
		}

		/// <summary>Checks if another slice is equal to the current slice.</summary>
		/// <param name="other">Slice compared with the current instance</param>
		/// <returns>true if both slices have the same size and contain the same sequence of bytes; otherwise, false.</returns>
		public bool Equals(MutableSlice other)
		{
			other.EnsureSliceIsValid();
			this.EnsureSliceIsValid();

			// note: Slice.Nil != Slice.Empty
			if (this.Array == null) return other.Array == null;
			if (other.Array == null) return false;

			return this.Count == other.Count && this.Span.SequenceEqual(other.Span);
		}

		/// <summary>Checks if the content of a span is equal to the current slice.</summary>
		/// <param name="other">Span of memory compared with the current instance</param>
		/// <returns>true if both locations have the same size and contain the same sequence of bytes; otherwise, false.</returns>
		[Pure]
		public bool Equals(ReadOnlySpan<byte> other)
		{
			this.EnsureSliceIsValid();

			// note: Nil and Empty are both equal to empty span
			if (this.Array == null || this.Count== 0) return other.Length == 0;

			return this.Count == other.Length && this.Span.SequenceEqual(other);
		}

		/// <summary>Lexicographically compare this slice with another one, and return an indication of their relative sort order</summary>
		/// <param name="other">Slice to compare with this instance</param>
		/// <returns>Returns a NEGATIVE value if the current slice is LESS THAN <paramref name="other"/>, ZERO if it is EQUAL TO <paramref name="other"/>, and a POSITIVE value if it is GREATER THAN <paramref name="other"/>.</returns>
		/// <remarks>If both this instance and <paramref name="other"/> are Nil or Empty, the comparison will return ZERO. If only <paramref name="other"/> is Nil or Empty, it will return a NEGATIVE value. If only this instance is Nil or Empty, it will return a POSITIVE value.</remarks>
		public int CompareTo(MutableSlice other)
		{
			other.EnsureSliceIsValid();
			this.EnsureSliceIsValid();

			if (this.Count == 0) return other.Count == 0 ? 0 : -1;
			if (other.Count == 0) return +1;
			return this.Span.SequenceCompareTo(other.Span);
		}

		/// <summary>Lexicographically compare this slice with another span, and return an indication of their relative sort order</summary>
		/// <param name="other">Span of memory to compare with this instance</param>
		/// <returns>Returns a NEGATIVE value if the current slice is LESS THAN <paramref name="other"/>, ZERO if it is EQUAL TO <paramref name="other"/>, and a POSITIVE value if it is GREATER THAN <paramref name="other"/>.</returns>
		public int CompareTo(ReadOnlySpan<byte> other)
		{
			this.EnsureSliceIsValid();

			if (this.Count == 0) return other.Length == 0 ? 0 : -1;
			if (other.Length == 0) return +1;
			return this.Span.SequenceCompareTo(other);
		}

		/// <summary>Checks if the content of a byte array segment matches the current slice.</summary>
		/// <param name="other">Byte array segment compared with the current instance</param>
		/// <returns>true if both segment and slice have the same size and contain the same sequence of bytes; otherwise, false.</returns>
		public bool Equals(Slice other)
		{
			return this.Count == other.Count && this.Span.SequenceEqual(other.Span);
		}

		/// <summary>Checks if the content of a byte array matches the current slice.</summary>
		/// <param name="other">Byte array compared with the current instance</param>
		/// <returns>true if the both array and slice have the same size and contain the same sequence of bytes; otherwise, false.</returns>
		public bool Equals(byte[]? other)
		{
			if (other == null) return this.Array == null;
			return this.Count == other.Length && this.Span.SequenceEqual(other);
		}

		#endregion

		#region Sanity Checking...

		/// <summary>Verifies that the <see cref="Offset"/> and <see cref="Count"/> fields represent a valid location in <see cref="Array"/></summary>
		/// <remarks>This method is inlined for best performance</remarks>
		/// <exception cref="FormatException">If the slice is not a valid section of a buffer</exception>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void EnsureSliceIsValid()
		{
			// Conditions for a slice to be valid:
			// - Count equal to 0 (other fields are ignored)
			// - Count greater than 0 and Array not null and all the bytes of the slice are contained in the underlying buffer

			int count = this.Count;
			if (count != 0)
			{
				var array = this.Array;
				if (array == null || (uint) count > (long) array.Length - (uint) this.Offset)
				{
					throw MalformedSlice(this);
				}
			}
		}

		/// <summary>Reject an invalid slice by throw an error with the appropriate diagnostic message.</summary>
		/// <param name="slice">Slice that is being naughty</param>
		[Pure, MethodImpl(MethodImplOptions.NoInlining)]
		public static Exception MalformedSlice(MutableSlice slice)
		{
#if DEBUG
			// If you break here, that means that a slice is invalid (negative count, offset, ...), which may be a sign of memory corruption!
			// You should walk up the stack to see what is going on !
			if (System.Diagnostics.Debugger.IsAttached) System.Diagnostics.Debugger.Break();
#endif

			if (slice.Offset < 0) return UnsafeHelpers.Errors.SliceOffsetNotNeg();
			if (slice.Count < 0) return UnsafeHelpers.Errors.SliceCountNotNeg();
			if (slice.Count > 0)
			{
				if (slice.Array == null) return UnsafeHelpers.Errors.SliceBufferNotNull();
				if (slice.Offset + slice.Count > slice.Array.Length) return UnsafeHelpers.Errors.SliceBufferTooSmall();
			}
			// maybe it's Lupus ?
			return UnsafeHelpers.Errors.SliceInvalid();
		}

		#endregion

		[UsedImplicitly(ImplicitUseTargetFlags.WithMembers)]
		private sealed class DebugView
		{
			private readonly MutableSlice m_slice;

			public DebugView(MutableSlice slice)
			{
				m_slice = slice;
			}

			public int Count => m_slice.Count;

			public byte[]? Data
			{
				get
				{
					if (m_slice.Count == 0) return m_slice.Array == null ? null : [ ];
					if (m_slice.Offset == 0 && m_slice.Count == m_slice.Array!.Length) return m_slice.Array;
					var tmp = new byte[m_slice.Count];
					System.Array.Copy(m_slice.Array!, m_slice.Offset, tmp, 0, m_slice.Count);
					return tmp;
				}
			}

			public string Content => Slice.Dump(m_slice, maxSize: Slice.DefaultPrettyPrintSize);

			/// <summary>Encoding using only for display purpose: we don't want to throw in the 'Text' property if the input is not text!</summary>
			private static readonly UTF8Encoding Utf8NoBomEncodingNoThrow = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: false);

			public string? Text
			{
				get
				{
					if (m_slice.Count == 0) return m_slice.Array == null ? null : string.Empty;
					return Slice.EscapeString(new StringBuilder(m_slice.Count + 16), m_slice.Span, Utf8NoBomEncodingNoThrow).ToString();
				}
			}

			public string? Hexa
			{
				get
				{
					if (m_slice.Count == 0) return m_slice.Array == null ? null : string.Empty;
					return m_slice.Count <= Slice.DefaultPrettyPrintSize
						? m_slice.Slice.ToHexString(' ')
						: m_slice.Substring(0, Slice.DefaultPrettyPrintSize).Slice.ToHexString(' ') + "[\u2026]";
				}
			}

		}

	}

	/// <summary>Helper methods for Slice</summary>
	public static class MutableSliceExtensions
	{
		[Pure, DebuggerNonUserCode, MethodImpl(MethodImplOptions.NoInlining)]
		private static MutableSlice EmptyOrNil(byte[]? array)
		{
			//note: we consider the "empty" or "nil" case less frequent, so we handle it in a non-inlined method
			return array == null ? default : MutableSlice.Empty;
		}

		/// <summary>Handle the Nil/Empty memoization</summary>
		[Pure, DebuggerNonUserCode, MethodImpl(MethodImplOptions.NoInlining)]
		private static MutableSlice EmptyOrNil(byte[]? array, int count)
		{
			//note: we consider the "empty" or "nil" case less frequent, so we handle it in a non-inlined method
			if (array == null) return count == 0 ? default : throw UnsafeHelpers.Errors.BufferArrayNotNull();
			return MutableSlice.Empty;
		}

		/// <summary>Return a slice that wraps the whole array</summary>
		[Pure, DebuggerNonUserCode, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static MutableSlice AsMutableSlice(this byte[]? bytes)
		{
			return bytes != null && bytes.Length != 0 ? new MutableSlice(bytes, 0, bytes.Length) : EmptyOrNil(bytes);
		}

		/// <summary>Return the tail of the array, starting from the specified offset</summary>
		/// <param name="bytes">Underlying buffer to slice</param>
		/// <param name="offset">Offset to the first byte of the slice</param>
		/// <returns></returns>
		[Pure, DebuggerNonUserCode, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static MutableSlice AsMutableSlice(this byte[] bytes, [Positive] int offset)
		{
			//note: this method is DANGEROUS! Caller may thing that it is passing a count instead of an offset.
			Contract.NotNull(bytes);
			if ((uint) offset > (uint) bytes.Length) UnsafeHelpers.Errors.ThrowBufferArrayToSmall();
			return bytes.Length != 0 ? new MutableSlice(bytes, offset, bytes.Length - offset) : MutableSlice.Empty;
		}

		/// <summary>Return a slice from the sub-section of the byte array</summary>
		/// <param name="bytes">Underlying buffer to slice</param>
		/// <param name="offset">Offset to the first element of the slice (if not empty)</param>
		/// <param name="count">Number of bytes to take</param>
		/// <returns>
		/// Slice that maps the corresponding sub-section of the array.
		/// If <paramref name="count"/> then either <see cref="MutableSlice.Empty">Slice.Empty</see> or <see cref="MutableSlice.Nil">Slice.Nil</see> will be returned, in order to not keep a reference to the whole buffer.
		/// </returns>
		[Pure, DebuggerNonUserCode, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static MutableSlice AsMutableSlice(this byte[]? bytes, [Positive] int offset, [Positive] int count)
		{
			//note: this method will frequently be called with offset==0, so we should optimize for this case!
			if (bytes == null || count == 0) return EmptyOrNil(bytes, count);

			// bound check
			// ReSharper disable once PossibleNullReferenceException
			if ((uint) offset >= (uint) bytes.Length || (uint) count > (uint) (bytes.Length - offset)) UnsafeHelpers.Errors.ThrowOffsetOutsideSlice();

			return new MutableSlice(bytes, offset, count);
		}

		/// <summary>Return a slice from the sub-section of the byte array</summary>
		/// <param name="bytes">Underlying buffer to slice</param>
		/// <param name="offset">Offset to the first element of the slice (if not empty)</param>
		/// <param name="count">Number of bytes to take</param>
		/// <returns>
		/// Slice that maps the corresponding sub-section of the array.
		/// If <paramref name="count"/> then either <see cref="MutableSlice.Empty">Slice.Empty</see> or <see cref="MutableSlice.Nil">Slice.Nil</see> will be returned, in order to not keep a reference to the whole buffer.
		/// </returns>
		[Pure, DebuggerNonUserCode, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static MutableSlice AsMutableSlice(this byte[]? bytes, uint offset, uint count)
		{
			//note: this method will frequently be called with offset==0, so we should optimize for this case!
			if (bytes == null || count == 0) return EmptyOrNil(bytes, (int) count);

			// bound check
			if (offset >= (uint) bytes.Length || count > ((uint) bytes.Length - offset)) UnsafeHelpers.Errors.ThrowOffsetOutsideSlice();

			return new MutableSlice(bytes, (int) offset, (int) count);
		}

		[Pure, DebuggerNonUserCode, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static MutableSlice AsMutableSlice(this ArraySegment<byte> self)
		{
			// We trust the ArraySegment<byte> ctor to validate the arguments before hand.
			// If somehow the arguments were corrupted (intentionally or not), then the same problem could have happened with the slice anyway!

			// ReSharper disable once AssignNullToNotNullAttribute
			return self.Count != 0 ? new MutableSlice(self.Array, self.Offset, self.Count) : EmptyOrNil(self.Array, self.Count);
		}

		/// <summary>Return an array segment that maps the same location as this mutable slice</summary>
		[Pure, DebuggerNonUserCode, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static ArraySegment<byte> ToArraySegment(this MutableSlice self) => self.Array != null ? new ArraySegment<byte>(self.Array, self.Offset, self.Count) : default;

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void CopyTo(this ReadOnlySpan<byte> source, MutableSlice destination)
		{
			if (source.Length != 0) source.CopyTo(destination.Span);
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void CopyTo(this Span<byte> source, MutableSlice destination)
		{
			if (source.Length != 0) source.CopyTo(destination.Span);
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool TryCopyTo(this ReadOnlySpan<byte> source, MutableSlice destination)
		{
			return source.Length == 0 || source.TryCopyTo(destination.Span);
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool TryCopyTo(this Span<byte> source, MutableSlice destination)
		{
			return source.Length == 0 || source.TryCopyTo(destination.Span);
		}

	}

}

#endif
