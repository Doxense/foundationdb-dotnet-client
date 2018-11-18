#region BSD License
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

//#define ENABLE_SPAN

#if !USE_SHARED_FRAMEWORK

namespace System
{
	using System;
	using System.Collections.Generic;
	using System.ComponentModel;
	using System.Diagnostics;
	using System.IO;
	using System.Linq;
	using System.Runtime.CompilerServices;
	using System.Runtime.InteropServices;
	using System.Text;
	using System.Threading;
	using System.Threading.Tasks;
	using Doxense.Diagnostics.Contracts;
	using Doxense.Memory;
	using JetBrains.Annotations;

	/// <summary>Delimits a section of a byte array</summary>
	/// <remarks>A Slice if the logical equivalent to a <see cref="ReadOnlySpan{T}">ReadOnlySpan&lt;byte&gt;</see></remarks>
	[PublicAPI, ImmutableObject(true), DebuggerDisplay("Count={Count}, Offset={Offset}"), DebuggerTypeProxy(typeof(Slice.DebugView))]
	[DebuggerNonUserCode] //remove this when you need to troubleshoot this class!
	public readonly partial struct Slice : IEquatable<Slice>, IEquatable<ArraySegment<byte>>, IEquatable<byte[]>, IComparable<Slice>, IFormattable
	{
		#region Static Members...

		/// <summary>Null slice ("no segment")</summary>
		public static readonly Slice Nil = default(Slice);

		/// <summary>Empty slice ("segment of 0 bytes")</summary>
		//note: we allocate a 1-byte array so that we can get a pointer to &slice.Array[slice.Offset] even for the empty slice
		public static readonly Slice Empty = new Slice(new byte[1], 0, 0);

		/// <summary>Cached array of bytes from 0 to 255</summary>
		[NotNull]
		internal static readonly byte[] ByteSprite = CreateByteSprite();

		private static byte[] CreateByteSprite()
		{
			var tmp = new byte[256];
			for (int i = 0; i < tmp.Length; i++) tmp[i] = (byte) i;
			return tmp;
		}

		#endregion

		//REVIEW: Layout: should we maybe swap things around? .Count seems to be the most often touched field before the rest
		// => Should it be Array/Offset/Count (current), or Count/Offset/Array ?

		/// <summary>Pointer to the buffer (or null for <see cref="Slice.Nil"/>)</summary>
		public readonly byte[] Array;

		/// <summary>Offset of the first byte of the slice in the parent buffer</summary>
		public readonly int Offset;

		/// <summary>Number of bytes in the slice</summary>
		public readonly int Count;

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		internal Slice([NotNull] byte[] array, int offset, int count)
		{
			//Paranoid.Requires(array != null && offset >= 0 && offset <= array.Length && count >= 0 && offset + count <= array.Length);
			this.Array = array;
			this.Offset = offset;
			this.Count = count;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		internal Slice([NotNull] byte[] array)
		{
			//Paranoid.Requires(array != null);
			this.Array = array;
			this.Offset = 0;
			this.Count = array.Length;
		}

		/// <summary>Creates a slice mapping a section of a buffer, without any sanity checks or buffer optimization</summary>
		/// <param name="buffer">Original buffer</param>
		/// <param name="offset">Offset into buffer</param>
		/// <param name="count">Number of bytes</param>
		/// <returns>Slice that maps this segment of buffer.</returns>
		/// <example>
		/// Slice.CreateUnsafe(buffer, 1, 5) => Slice { Array = buffer, Offset = 1, Count = 5 }
		/// </example>
		/// <remarks>
		/// Use this method ONLY if you are 100% sure that the slice will be valid. Failure to do so may introduce memory corruption!
		/// Also, please note that this method will NOT optimize the case where count == 0, and will keep a reference to the original buffer!
		/// The caller is responsible for handle that scenario if it is important!
		/// </remarks>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static Slice CreateUnsafe([NotNull] byte[] buffer, [Positive] int offset, [Positive] int count)
		{
			Contract.Requires(buffer != null && (uint) offset <= (uint) buffer.Length && (uint) count <= (uint) (buffer.Length - offset));
			return new Slice(buffer, offset, count);
		}

		/// <summary>Creates a slice mapping a section of a buffer, without any sanity checks or buffer optimization</summary>
		/// <param name="buffer">Original buffer</param>
		/// <param name="offset">Offset into buffer</param>
		/// <param name="count">Number of bytes</param>
		/// <returns>Slice that maps this segment of buffer.</returns>
		/// <example>
		/// Slice.CreateUnsafe(buffer, 1, 5) => Slice { Array = buffer, Offset = 1, Count = 5 }
		/// </example>
		/// <remarks>
		/// Use this method ONLY if you are 100% sure that the slice will be valid. Failure to do so may introduce memory corruption!
		/// Also, please note that this method will NOT optimize the case where count == 0, and will keep a reference to the original buffer!
		/// The caller is responsible for handle that scenario if it is important!
		/// </remarks>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static Slice CreateUnsafe([NotNull] byte[] buffer, uint offset, uint count)
		{
			Contract.Requires(buffer != null && offset <= (uint) buffer.Length && count <= ((uint) buffer.Length - offset));
			return new Slice(buffer, (int) offset, (int) count);
		}

		/// <summary>Creates a new empty slice of a specified size containing all zeroes</summary>
		public static Slice Create(int size)
		{
			Contract.Positive(size, nameof(size));
			return size != 0 ? new Slice(new byte[size]) : Slice.Empty;
		}

		/// <summary>Creates a new empty slice of a specified size containing all zeroes</summary>
		[Pure]
		public static Slice Create(uint size)
		{
			Contract.LessOrEqual(size, int.MaxValue, nameof(size));
			return size != 0 ? new Slice(new byte[size]) : Slice.Empty;
		}

		/// <summary>Creates a new slice with a copy of the array</summary>
		[Pure]
		public static Slice Copy(byte[] source)
		{
			Contract.NotNull(source, nameof(source));
			if (source.Length == 0) return Empty;
			return Copy(source, 0, source.Length);
		}

#if ENABLE_SPAN

		/// <summary>Creates a new slice with a copy of the array segment</summary>
		[Pure]
		public static Slice Copy(byte[] source, int offset, int count)
		{
			return Copy(new ReadOnlySpan<byte>(source, offset, count));
		}

		/// <summary>Creates a new slice with a copy of the span</summary>
		[Pure]
		public static Slice Copy(ReadOnlySpan<byte> source)
		{
			if (source.Length == 0) return Empty;
			var tmp = source.ToArray();
			return new Slice(tmp, 0, source.Length);
		}

		/// <summary>Creates a new slice with a copy of the span, using a scratch buffer</summary>
		[Pure]
		public static Slice Copy(ReadOnlySpan<byte> source, [CanBeNull] ref byte[] buffer)
		{
			if (source.Length == 0) return Empty;
			var tmp = UnsafeHelpers.EnsureCapacity(ref buffer, BitHelpers.NextPowerOfTwo(source.Length));
			UnsafeHelpers.Copy(tmp, 0, source);
			return new Slice(tmp, 0, source.Length);
		}

#else

		/// <summary>Creates a new slice with a copy of the array segment</summary>
		[Pure]
		public static Slice Copy(byte[] source, int offset, int count)
		{
			if (count == 0) return source == null ? Nil : Empty;
			var tmp = new byte[count];
			UnsafeHelpers.Copy(tmp, 0, source, offset, count);
			return new Slice(tmp, 0, count);
		}

		/// <summary>Creates a new slice with a copy of the span, using a scratch buffer</summary>
		[Pure]
		public static Slice Copy(Slice source, [CanBeNull] ref byte[] buffer)
		{
			if (source.Count == 0) return source.Array == null ? default(Slice) : Empty;
			var tmp = UnsafeHelpers.EnsureCapacity(ref buffer, BitHelpers.NextPowerOfTwo(source.Count));
			UnsafeHelpers.Copy(tmp, 0, source.Array, source.Offset, source.Count);
			return new Slice(tmp, 0, source.Count);
		}

#endif

		/// <summary>Creates a new slice with a copy of an unmanaged memory buffer</summary>
		/// <param name="source">Pointer to unmanaged buffer</param>
		/// <param name="count">Number of bytes in the buffer</param>
		/// <returns>Slice with a managed copy of the data</returns>
		[Pure]
		public static Slice Copy(IntPtr source, int count)
		{
			unsafe
			{
				return Copy((byte*) source.ToPointer(), count);
			}
		}

		/// <summary>Creates a new slice with a copy of an unmanaged memory buffer</summary>
		/// <param name="source">Pointer to unmanaged buffer</param>
		/// <param name="count">Number of bytes in the buffer</param>
		/// <returns>Slice with a managed copy of the data</returns>
		[Pure]
		public static unsafe Slice Copy(void * source, int count)
		{
			return Copy((byte*) source, count);
		}


		/// <summary>Creates a new slice with a copy of an unmanaged memory buffer</summary>
		/// <param name="source">Pointer to unmanaged buffer</param>
		/// <param name="count">Number of bytes in the buffer</param>
		/// <returns>Slice with a managed copy of the data</returns>
		[Pure]
		public static unsafe Slice Copy(byte* source, int count)
		{
			if (count == 0)
			{
				return source == null ? default(Slice) : Empty;
			}
			Contract.PointerNotNull(source, nameof(source));
			Contract.Positive(count, nameof(count));

			if (count == 1)
			{ // Use the sprite cache
				return Slice.FromByte(*source);
			}

			var bytes = new byte[count];
			UnsafeHelpers.CopyUnsafe(bytes, 0, source, (uint) count);
			return new Slice(bytes, 0, count);
		}

#if ENABLE_SPAN
		/// <summary>Return a copy of the memory content of an array of item</summary>
		public static Slice CopyMemory<T>(ReadOnlySpan<T> items)
			where T : struct
		{
			return Copy(MemoryMarshal.AsBytes(items));
		}

		/// <summary>Return a copy of the memory content of an array of item</summary>
		public static Slice CopyMemory<T>(ReadOnlySpan<T> items, [CanBeNull] ref byte[] buffer)
			where T : struct
		{
			return Copy(MemoryMarshal.AsBytes(items), ref buffer);
		}
#endif

		/// <summary>Implicitly converts a Slice into an <see cref="ArraySegment{T}">ArraySegment&lt;byte&gt;</see></summary>
		[Pure, DebuggerNonUserCode, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static implicit operator ArraySegment<byte>(Slice value)
		{
			return value.HasValue ? new ArraySegment<byte>(value.Array, value.Offset, value.Count) : default(ArraySegment<byte>);
		}

		/// <summary>Implicitly converts an <see cref="ArraySegment{T}">ArraySegment&lt;byte&gt;</see> into a Slice</summary>
		[Pure, DebuggerNonUserCode, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static implicit operator Slice(ArraySegment<byte> value)
		{
			if (value.Count == 0) return value.Array == null ? default(Slice) : Slice.Empty;
			return new Slice(value.Array, value.Offset, value.Count);
		}

#if ENABLE_SPAN
		/// <summary>Converts a Slice into an <see cref="Span{T}">Span&lt;byte&gt;</see></summary>
		[Pure, DebuggerNonUserCode, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static explicit operator Span<byte>(Slice value)
		{
			//note: explicit because casting to writable Span<byte> MAY be dangerous, and we need opt-in from the caller!
			return new Span<byte>(value.Array, value.Offset, value.Count);
		}

		/// <summary>Implicitly converts a Slice into an <see cref="ReadOnlySpan{T}">ReadOnlySpan&lt;byte&gt;</see></summary>
		[Pure, DebuggerNonUserCode, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static implicit operator ReadOnlySpan<byte>(Slice value)
		{
			//note: implicit because casting to non-writable ReadOnlySpan<byte> is safe
			return new ReadOnlySpan<byte>(value.Array, value.Offset, value.Count);
		}
#endif

		/// <summary>Returns true is the slice is not null</summary>
		/// <remarks>An empty slice is NOT considered null</remarks>
		public bool HasValue
		{
			[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
			get { return this.Array != null; }
		}

		/// <summary>Returns true if the slice is null</summary>
		/// <remarks>An empty slice is NOT considered null</remarks>
		public bool IsNull
		{
			[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
			get { return this.Array == null; }
		}

		/// <summary>Return true if the slice is not null but contains 0 bytes</summary>
		/// <remarks>A null slice is NOT empty</remarks>
		public bool IsEmpty
		{
			[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
			get { return this.Count == 0 && this.Array != null; }
		}

		/// <summary>Returns true if the slice is null or empty, or false if it contains at least one byte</summary>
		public bool IsNullOrEmpty
		{
			[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
			get { return this.Count == 0; }
		}

		/// <summary>Returns true if the slice contains at least one byte, or false if it is null or empty</summary>
		public bool IsPresent
		{
			[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
			get { return this.Count > 0; }
		}

		/// <summary>Replace <see cref="Nil"/> with <see cref="Empty"/></summary>
		/// <returns>The same slice if it is not <see cref="Nil"/>; otherwise, <see cref="Empty"/></returns>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public Slice OrEmpty()
		{
			return this.Count > 0? this : Empty;
		}

		/// <summary>Return a byte array containing all the bytes of the slice, or null if the slice is null</summary>
		/// <returns>Byte array with a copy of the slice, or null</returns>
		[Pure, CanBeNull]
		public byte[] GetBytes()
		{
			int len = this.Count;
			if (len == 0) return this.Array == null ? null : System.Array.Empty<byte>();
			EnsureSliceIsValid();

			var tmp = new byte[len];
			UnsafeHelpers.CopyUnsafe(tmp, 0, this.Array, this.Offset, len);
			return tmp;
		}

		/// <summary>Return a byte array containing all the bytes of the slice, or and empty array if the slice is null or empty</summary>
		/// <returns>Byte array with a copy of the slice</returns>
		[Pure, NotNull]
		public byte[] GetBytesOrEmpty()
		{
			//note: this is a convenience method for code where dealing with null is a pain, or where it has already checked IsNull
			int len = this.Count;
			if (len == 0) return System.Array.Empty<byte>();
			EnsureSliceIsValid();

			var tmp = new byte[len];
			UnsafeHelpers.CopyUnsafe(tmp, 0, this.Array, this.Offset, len);
			return tmp;
		}

		/// <summary>Return a byte array containing a subset of the bytes of the slice, or null if the slice is null</summary>
		/// <returns>Byte array with a copy of a subset of the slice, or null</returns>
		[Pure, NotNull]
		public byte[] GetBytes(int offset, int count)
		{
			//TODO: throw if this.Array == null ? (what does "Slice.Nil.GetBytes(..., 0)" mean ?)

			if (offset < 0) throw new ArgumentOutOfRangeException(nameof(offset));

			int len = this.Count;
			if ((uint) count > (uint) len || (uint) count > (uint) (len - offset)) throw new ArgumentOutOfRangeException(nameof(count));

			if (count == 0) return System.Array.Empty<byte>();
			EnsureSliceIsValid();

			var tmp = new byte[count];
			UnsafeHelpers.CopyUnsafe(tmp, 0, this.Array, this.Offset + offset, count);
			return tmp;
		}

		/// <summary>Return a SliceReader that can decode this slice into smaller fields</summary>
		[Obsolete("Use ToSliceReader() instead")]
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public SliceReader GetReader()
		{
			return new SliceReader(this);
		}

		/// <summary>Return a SliceReader that can decode this slice into smaller fields</summary>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public SliceReader ToSliceReader()
		{
			return new SliceReader(this);
		}

		/// <summary>Return a stream that wraps this slice</summary>
		/// <returns>Stream that will read the slice from the start.</returns>
		/// <remarks>
		/// You can use this method to convert text into specific encodings, load bitmaps (JPEG, PNG, ...), or any serialization format that requires a Stream or TextReader instance.
		/// Disposing this stream will have no effect on the slice.
		/// </remarks>
		[Pure, NotNull]
		public SliceStream ToSliceStream()
		{
			EnsureSliceIsValid();
			return new SliceStream(this);
		}

		/// <summary>Returns a new slice that contains an isolated copy of the buffer</summary>
		/// <returns>Slice that is equivalent, but is isolated from any changes to the buffer</returns>
		[Pure]
		public Slice Memoize()
		{
			if (this.Count == 0) return this.Array == null ? Slice.Nil : Slice.Empty;
			// ReSharper disable once AssignNullToNotNullAttribute
			return new Slice(GetBytes());
		}

		/// <summary>Map an offset in the slice into the absolute offset in the buffer, without any bound checking</summary>
		/// <param name="index">Relative offset (negative values mean from the end)</param>
		/// <returns>Absolute offset in the buffer</returns>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private int UnsafeMapToOffset(int index)
		{
			return this.Offset + NormalizeIndex(index);
		}

		/// <summary>Map an offset in the slice into the absolute offset in the buffer</summary>
		/// <param name="index">Relative offset (negative values mean from the end)</param>
		/// <returns>Absolute offset in the buffer</returns>
		/// <exception cref="IndexOutOfRangeException">If the index is outside the slice</exception>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private int MapToOffset(int index)
		{
			int p = NormalizeIndex(index);
			if ((uint) p >= (uint) this.Count) UnsafeHelpers.Errors.ThrowIndexOutOfBound(index);
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
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			get { return this.Array[MapToOffset(index)]; }
		}

#if ENABLE_SPAN
		/// <summary>Returns a reference to a specific position in the slice</summary>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		[EditorBrowsable(EditorBrowsableState.Never)]
		public ref readonly byte ItemRef(int index)
		{
			return ref this.Array[MapToOffset(index)];
		}
#endif

		/// <summary>Returns a substring of the current slice that fits withing the specified index range</summary>
		/// <param name="start">The starting position of the substring. Positive values means from the start, negative values means from the end</param>
		/// <param name="end">The end position (excluded) of the substring. Positive values means from the start, negative values means from the end</param>
		/// <returns>Subslice</returns>
		public Slice this[int start, int end]
		{
			get
			{
				start = NormalizeIndex(start);
				end = NormalizeIndex(end);

				// bound check
				if (start < 0) start = 0;
				if (end > this.Count) end = this.Count;

				if (start >= end) return Slice.Empty;
				if (start == 0 && end == this.Count) return this;

				checked { return new Slice(this.Array, this.Offset + start, end - start); }
			}
		}

		/// <summary>
		/// Returns a reference to the first byte in the slice.
		/// If the slice is empty, returns a reference to the location where the first character would have been stored.
		/// Such a reference can be used for pinning but must never be dereferenced.
		/// </summary>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		[EditorBrowsable(EditorBrowsableState.Never)]
		public ref byte DangerousGetPinnableReference()
		{
			//note: this is the equivalent of MemoryMarshal.GetReference(..) and does not check for the 0-length case!
			return ref this.Array[this.Offset];
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
				return ref (this.Count != 0) ? ref this.Array[this.Offset] : ref Unsafe.AsRef<byte>(null);
			}
		}

		/// <summary>Copy this slice into another buffer, and move the cursor</summary>
		/// <param name="buffer">Buffer where to copy this slice</param>
		/// <param name="cursor">Offset into the destination buffer</param>
		public void WriteTo([NotNull] byte[] buffer, ref int cursor)
		{
			//note: CopyBytes will validate all the parameters
			int count = this.Count;
			UnsafeHelpers.Copy(buffer, cursor, this.Array, this.Offset, count);
			cursor += count;
		}

		public void CopyTo(Slice destination)
		{
			if (destination.Count < this.Count) throw UnsafeHelpers.Errors.SliceBufferTooSmall();
			UnsafeHelpers.Copy(destination.Array, destination.Offset, this.Array, this.Offset, this.Count);
		}

#if ENABLE_SPAN
		public void CopyTo(Span<byte> destination)
		{
			if (destination.Length < this.Count) throw UnsafeHelpers.Errors.SliceBufferTooSmall();
			UnsafeHelpers.Copy(destination, this.Array, this.Offset, this.Count);
		}
#endif

		/// <summary>Copy this slice into another buffer</summary>
		/// <param name="buffer">Buffer where to copy this slice</param>
		/// <param name="offset">Offset into the destination buffer</param>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void CopyTo([NotNull] byte[] buffer, int offset)
		{
			UnsafeHelpers.Copy(buffer, offset, this.Array, this.Offset, this.Count);
		}

		/// <summary>Copy this slice into memory and return the advanced cursor</summary>
		/// <param name="ptr">Pointer where to copy this slice</param>
		/// <param name="end">Pointer to the next byte after the last availble position in the output buffer</param>
		/// <remarks>Copy will fail if there is not enough space in the output buffer (ie: if it would writer at or after <paramref name="end"/>)</remarks>
		[NotNull]
		public unsafe byte* CopyToUnsafe([NotNull] byte* ptr, [NotNull] byte* end)
		{
			if (ptr == null | end == null) throw new ArgumentNullException(ptr == null ? nameof(ptr) : nameof(end));
			long count = this.Count;
			byte* next = ptr + count;
			if (next > end) throw new ArgumentException("Slice is too large to fit in the specified output buffer");
			if (count > 0)
			{
				fixed (byte* bytes = &DangerousGetPinnableReference())
				{
					Buffer.MemoryCopy(bytes, ptr, count, count);
				}
			}
			return next;
		}

		/// <summary>Try to copy this slice into memory and return the advanced cursor, if the destination is large enough</summary>
		/// <param name="ptr">Pointer where to copy this slice</param>
		/// <param name="end">Pointer to the next byte after the last availble position in the output buffer</param>
		/// <returns>Point to the advanced memory position, or null if the destination buffer was too small</returns>
		[CanBeNull]
		public unsafe byte* TryCopyToUnsafe([NotNull] byte* ptr, [NotNull] byte* end)
		{
			if (ptr == null | end == null) throw new ArgumentNullException(ptr == null ? nameof(ptr) : nameof(end));
			long count = this.Count;
			byte* next = ptr + count;
			if (next > end) return null;
			if (count > 0)
			{
				fixed (byte* bytes = &DangerousGetPinnableReference())
				{
					Buffer.MemoryCopy(bytes, ptr, count, count);
				}
			}
			return next;
		}

		/// <summary>Copy this slice into memory and return the advanced cursor</summary>
		/// <param name="ptr">Pointer where to copy this slice</param>
		/// <param name="count">Capacity of the output buffer</param>
		/// <remarks>Copy will fail if there is not enough space in the output buffer</remarks>
		public IntPtr CopyTo(IntPtr ptr, long count)
		{
			unsafe
			{
				byte* p = (byte*) ptr.ToPointer();
				return (IntPtr) CopyToUnsafe(p, p + count);
			}
		}

		/// <summary>Copy this slice into memory and return the advanced cursor</summary>
		/// <param name="ptr">Pointer where to copy this slice</param>
		/// <param name="count">Capacity of the output buffer</param>
		/// <return>Updated pointer after the copy, of <see cref="IntPtr.Zero"/> if the destination buffer was too small</return>
		public bool TryCopyTo(IntPtr ptr, long count)
		{
			unsafe
			{
				byte* p = (byte*) ptr.ToPointer();
				return null != TryCopyToUnsafe(p, p + count);
			}
		}

		/// <summary>Retrieves a substring from this instance. The substring starts at a specified character position.</summary>
		/// <param name="offset">The starting position of the substring. Positive values mmeans from the start, negative values means from the end</param>
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
		public Slice Substring(int offset)
		{
			int len = this.Count;

			// negative values mean from the end
			if (offset < 0) offset += this.Count;
			//REVIEW: TODO: get rid of negative indexing, and create a different "substring from the end" method?

			// bound check
			if ((uint) offset > (uint) len) UnsafeHelpers.Errors.ThrowOffsetOutsideSlice();

			int r = len - offset;
			return r != 0 ? new Slice(this.Array, this.Offset + offset, r) : Slice.Empty;
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
		public Slice Substring(int offset, int count)
		{
			if (count == 0) return Slice.Empty;
			int len = this.Count;

			// bound check
			if ((uint) offset >= (uint) len || (uint) count > (uint)(len - offset)) UnsafeHelpers.Errors.ThrowOffsetOutsideSlice();

			return new Slice(this.Array, this.Offset + offset, count);
		}

		/// <summary>Truncate the slice if its size exceeds the specified length.</summary>
		/// <param name="maxSize">Maximum size.</param>
		/// <returns>Slice of at most the specified size, or smaller if the original slice does not exceed the size.</returns>
		/// <example><list type="table">
		///   <item><term>Smaller than maxSize is unmodified</term><description><code>{"Hello, World!"}.Truncate(20) => {"Hello, World!"}</code></description></item>
		///   <item><term>Larger than maxSize is truncated</term><description><code>{"Hello, World!"}.Truncate(5) => {"Hello"}</code></description></item>
		///   <item><term>Truncating to 0 returns Empty (or Nil)</term><description><code>{"Hello, World!"}.Truncate(0) == Slice.Empty</code></description></item>
		/// </list></example>
		[Pure]
		public Slice Truncate([Positive] int maxSize)
		{
			//note: the only difference with Substring(0, maxSize) is that we don't throw if the slice is smaller than !
			Contract.Positive(maxSize, nameof(maxSize));

			if (maxSize == 0) return this.Array == null ? Nil : Empty;
			return this.Count <= maxSize ? this : new Slice(this.Array, this.Offset, maxSize);
		}

		/// <summary>Returns a slice array that contains the sub-slices in this instance that are delimited by the specified separator</summary>
		/// <param name="separator">The slice that delimits the sub-slices in this instance.</param>
		/// <param name="options"><see cref="StringSplitOptions.RemoveEmptyEntries"/> to omit empty array elements from the array returned; or <see cref="StringSplitOptions.None"/> to include empty array elements in the array returned.</param>
		/// <returns>An array whose elements contains the sub-slices in this instance that are delimited by the value of <paramref name="separator"/>.</returns>
		[Pure]
		public Slice[] Split(Slice separator, StringSplitOptions options = StringSplitOptions.None)
		{
			return Split(this, separator, options);
		}

		[Pure]
		public Slice[] Split(int stride)
		{
			return Split(this, stride);
		}

		/// <summary>Reports the zero-based index of the first occurence of the specified slice in this instance.</summary>
		/// <param name="value">The slice to seek</param>
		/// <returns>The zero-based index of <paramref name="value"/> if that slice is found, or -1 if it is not. If <paramref name="value"/> is <see cref="Slice.Empty"/>, then the return value is -1.</returns>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public int IndexOf(Slice value)
		{
			return Find(this, value);
		}

		/// <summary>Reports the zero-based index of the first occurence of the specified slice in this instance. The search starts at a specified position.</summary>
		/// <param name="value">The slice to seek</param>
		/// <param name="startIndex">The search starting position</param>
		/// <returns>The zero-based index of <paramref name="value"/> if that slice is found, or -1 if it is not. If <paramref name="value"/> is <see cref="Slice.Empty"/>, then the return value is startIndex</returns>
		[Pure]
		public int IndexOf(Slice value, int startIndex)
		{
			return Substring(startIndex).IndexOf(value);
		}

		/// <summary>Reports the zero-based index of the first occurence of the specified byte in this instance.</summary>
		/// <param name="value">The byte to seek</param>
		/// <returns>The zero-based index of <paramref name="value"/> if that slice is found, or -1 if it is not.</returns>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public int IndexOf(byte value)
		{
			return Find(this, value);
		}

		/// <summary>Reports the zero-based index of the first occurence of the specified byte in this instance. The search starts at a specified position.</summary>
		/// <param name="value">The byte to seek</param>
		/// <param name="startIndex">The search starting position</param>
		/// <returns>The zero-based index of <paramref name="value"/> if that byte is found, or -1 if it is not.</returns>
		[Pure]
		public int IndexOf(byte value, int startIndex)
		{
			int len = this.Count;
			if ((uint) startIndex >= (uint) len) UnsafeHelpers.Errors.ThrowOffsetOutsideSlice();

			var tmp = new Slice(this.Array, this.Offset + startIndex, len - startIndex);
			int idx = Find(tmp, value);
			return idx >= 0 ? checked(startIndex + idx) : -1;
		}

		/// <summary>Determines whether the beginning of this slice instance matches a specified slice.</summary>
		/// <param name="value">The slice to compare</param>
		/// <returns><b>true</b> if <paramref name="value"/> matches the beginning of this slice; otherwise, <b>false</b></returns>
		[Pure]
		public bool StartsWith(Slice value)
		{
			if (!value.HasValue) throw ThrowHelper.ArgumentNullException(nameof(value));

			int count = value.Count;

			// any strings starts with the empty string
			if (count == 0) return true;

			// prefix cannot be bigger
			if ((uint) count > (uint) this.Count) return false;

			return UnsafeHelpers.SameBytes(this.Array, this.Offset, value.Array, value.Offset, count);
		}

		/// <summary>Determines whether the end of this slice instance matches a specified slice.</summary>
		/// <param name="value">The slice to compare to the substring at the end of this instance.</param>
		/// <returns><b>true</b> if <paramref name="value"/> matches the end of this slice; otherwise, <b>false</b></returns>
		[Pure]
		public bool EndsWith(Slice value)
		{
			if (!value.HasValue) throw ThrowHelper.ArgumentNullException(nameof(value));

			// any strings ends with the empty string
			int count = value.Count;
			if (count == 0) return true;

			// suffix cannot be bigger
			int len = this.Count;
			if ((uint) count > (uint) len) return false;

			return UnsafeHelpers.SameBytes(this.Array, this.Offset + (len - count), value.Array, value.Offset, count);
		}

		/// <summary>Equivalent of StartsWith, but the returns false if both slices are identical</summary>
		[Pure]
		public bool PrefixedBy(Slice parent)
		{
			int count = parent.Count;

			// empty is a parent of everyone
			if (count == 0) return true;

			// we must have at least one more byte then the parent
			if (this.Count <= count) return false;

			// must start with the same bytes
			return UnsafeHelpers.SameBytes(parent.Array, parent.Offset, this.Array, this.Offset, count);
		}

		/// <summary>Equivalent of EndsWith, but the returns false if both slices are identical</summary>
		[Pure]
		public bool SuffixedBy(Slice parent)
		{
			// empty is a parent of everyone
			int count = parent.Count;
			if (count == 0) return true;

			// empty is not a child of anything
			int len = this.Count;
			if (len == 0) return false;

			// we must have at least one more byte then the parent
			if (len <= count) return false;

			// must start with the same bytes
			return UnsafeHelpers.SameBytes(parent.Array, parent.Offset + (len - count), this.Array, this.Offset, count);
		}

		/// <summary>Append/Merge a slice at the end of the current slice</summary>
		/// <param name="tail">Slice that must be appended</param>
		/// <returns>Merged slice if both slices are contigous, or a new slice containg the content of the current slice, followed by the tail slice. Or Slice.Empty if both parts are nil or empty</returns>
		[Pure]
		public Slice Concat(Slice tail)
		{
			if (tail.Count == 0) return this.Count > 0 ? this: Slice.Empty;
			if (this.Count == 0) return tail;

			tail.EnsureSliceIsValid();
			this.EnsureSliceIsValid();

			// special case: adjacent segments ?
			if (object.ReferenceEquals(this.Array, tail.Array) && this.Offset + this.Count == tail.Offset)
			{
				return new Slice(this.Array, this.Offset, this.Count + tail.Count);
			}

			byte[] tmp = new byte[this.Count + tail.Count];
			UnsafeHelpers.CopyUnsafe(tmp, 0, this.Array, this.Offset, this.Count);
			UnsafeHelpers.CopyUnsafe(tmp, this.Count, tail.Array, tail.Offset, tail.Count);
			return new Slice(tmp);
		}

		/// <summary>Append an array of slice at the end of the current slice, all sharing the same buffer</summary>
		/// <param name="slices">Slices that must be appended</param>
		/// <returns>Array of slices (for all keys) that share the same underlying buffer</returns>
		[Pure, NotNull]
		public Slice[] ConcatRange([NotNull] Slice[] slices)
		{
			Contract.NotNull(slices, nameof(slices));
			EnsureSliceIsValid();

			// pre-allocate by computing final buffer capacity
			var prefixSize = this.Count;
			var capacity = slices.Sum((slice) => prefixSize + slice.Count);
			var writer = new SliceWriter(capacity);
			var next = new List<int>(slices.Length);

			//TODO: use multiple buffers if item count is huge ?

			foreach (var slice in slices)
			{
				writer.WriteBytes(in this);
				writer.WriteBytes(in slice);
				next.Add(writer.Position);
			}

			return SplitIntoSegments(writer.Buffer, 0, next);
		}

		/// <summary>Append a sequence of slice at the end of the current slice, all sharing the same buffer</summary>
		/// <param name="slices">Slices that must be appended</param>
		/// <returns>Array of slices (for all keys) that share the same underlying buffer</returns>
		[Pure, NotNull]
		public Slice[] ConcatRange([NotNull] IEnumerable<Slice> slices)
		{
			Contract.NotNull(slices, nameof(slices));

			// use optimized version for arrays
			if (slices is Slice[] array) return ConcatRange(array);

			var next = new List<int>();
			var writer = default(SliceWriter);

			//TODO: use multiple buffers if item count is huge ?

			foreach (var slice in slices)
			{
				writer.WriteBytes(in this);
				writer.WriteBytes(in slice);
				next.Add(writer.Position);
			}

			return SplitIntoSegments(writer.Buffer, 0, next);

		}

		/// <summary>Split a buffer containing multiple contiguous segments into an array of segments</summary>
		/// <param name="buffer">Buffer containing all the segments</param>
		/// <param name="start">Offset of the start of the first segment</param>
		/// <param name="endOffsets">Array containing, for each segment, the offset of the following segment</param>
		/// <returns>Array of segments</returns>
		/// <example>SplitIntoSegments("HelloWorld", 0, [5, 10]) => [{"Hello"}, {"World"}]</example>
		[NotNull]
		public static Slice[] SplitIntoSegments([NotNull] byte[] buffer, int start, [NotNull] List<int> endOffsets)
		{
			Contract.Requires(buffer != null && endOffsets != null);
			var result = new Slice[endOffsets.Count];
			int i = 0;
			int p = start;
			foreach (var end in endOffsets)
			{
				result[i++] = new Slice(buffer, p, end - p);
				p = end;
			}

			return result;
		}

		/// <summary>Concatenate two slices together</summary>
		public static Slice Concat(Slice a, Slice b)
		{
			return a.Concat(b);
		}

		/// <summary>Concatenate three slices together</summary>
		public static Slice Concat(Slice a, Slice b, Slice c)
		{
			int count = a.Count + b.Count + c.Count;
			if (count == 0) return Slice.Empty;
			var writer = new SliceWriter(count);
			writer.WriteBytes(in a);
			writer.WriteBytes(in b);
			writer.WriteBytes(in c);
			return writer.ToSlice();
		}

		/// <summary>Concatenate an array of slices into a single slice</summary>
		public static Slice Concat(params Slice[] args)
		{
			int count = 0;
			for (int i = 0; i < args.Length; i++) count += args[i].Count;
			if (count == 0) return Slice.Empty;
			var writer = new SliceWriter(count);
			for (int i = 0; i < args.Length; i++) writer.WriteBytes(in args[i]);
			return writer.ToSlice();
		}

		/// <summary>Adds a prefix to a list of slices</summary>
		/// <param name="prefix">Prefix to add to all the slices</param>
		/// <param name="slices">List of slices to process</param>
		/// <returns>Array of slice that all start with <paramref name="prefix"/> and followed by the corresponding entry in <paramref name="slices"/></returns>
		/// <remarks>This method is optmized to reduce the amount of memory allocated</remarks>
		[Pure, NotNull]
		public static Slice[] ConcatRange(Slice prefix, IEnumerable<Slice> slices)
		{
			Contract.NotNull(slices, nameof(slices));

			if (prefix.IsNullOrEmpty)
			{ // nothing to do, but we still need to copy the array
				return slices.ToArray();
			}

			Slice[] res;
			Slice[] arr;
			ICollection<Slice> coll;

			if ((arr = slices as Slice[]) != null)
			{	// fast-path for arrays (most frequent with range reads)

				// we wil use a SliceBuffer to store all the keys produced in as few byte[] arrays as needed

				// precompute the exact size needed
				int totalSize = prefix.Count * arr.Length;
				for (int i = 0; i < arr.Length; i++) totalSize += arr[i].Count;
				var buf = new SliceBuffer(Math.Min(totalSize, 64 * 1024));

				res = new Slice[arr.Length];
				for (int i = 0; i < arr.Length; i++)
				{
					res[i] = buf.Intern(prefix, arr[i], aligned: false);
				}
			}
			else if ((coll = slices as ICollection<Slice>) != null)
			{  // collection (size known)

				//TODO: also use a SliceBuffer since we could precompute the total size...

				res = new Slice[coll.Count];
				int p = 0;
				foreach (var suffix in coll)
				{
					res[p++] = prefix.Concat(suffix);
				}
			}
			else
			{  // streaming sequence (size unknown)

				//note: we can only scan the list once, so would be no way to get a sensible value for the buffer's page size
				var list = new List<Slice>();
				foreach (var suffix in slices)
				{
					list.Add(prefix.Concat(suffix));
				}
				res = list.ToArray();
			}

			return res;
		}

		/// <summary>Reports the zero-based index of the first occurrence of the specified slice in this source.</summary>
		/// <param name="source">The slice Input slice</param>
		/// <param name="value">The slice to seek</param>
		/// <returns>Offset of the match if positive, or no occurence was found if negative</returns>
		[Pure]
		public static int Find(Slice source, Slice value)
		{
			const int NOT_FOUND = -1;

			source.EnsureSliceIsValid();
			source.EnsureSliceIsValid();

			int m = value.Count;
			if (m == 0) return 0;

			int n = source.Count;
			if (n == 0) return NOT_FOUND;

			if (m == n) return source.Equals(value) ? 0 : NOT_FOUND;
			if (m <= n)
			{
				//TODO: OPTIMIZE: write a version that uses pointers!
				byte[] src = source.Array;
				int p = source.Offset;
				byte firstByte = value[0];

				// note: this is a very simplistic way to find a value, and is optimized for the case where the separator is only one byte (most common)
				n -= (m - 1); // no need to scan the tail because it would not fit
				while (n-- > 0)
				{
					if (src[p++] == firstByte)
					{ // possible match ?
						if (m == 1 || UnsafeHelpers.SameBytesUnsafe(src, p, value.Array, value.Offset + 1, m - 1))
						{
							return p - source.Offset - 1;
						}
					}
				}
			}

			return NOT_FOUND;
		}

		/// <summary>Reports the zero-based index of the first occurrence of the specified byte in this source.</summary>
		/// <param name="source">The slice Input slice</param>
		/// <param name="value">The byte to find</param>
		/// <returns>Offset of the match if positive, or the byte was not found if negative</returns>
		[Pure]
		public static int Find(Slice source, byte value)
		{
			source.EnsureSliceIsValid();

			const int NOT_FOUND = -1;
			int n = source.Count;
			if (n == 0) return NOT_FOUND;
			unsafe
			{
				//TODO: Optimize this!
				fixed (byte* ptr = &source.DangerousGetPinnableReference())
				{
					byte* inp = ptr;
					while (n-- > 0)
					{
						if (*inp == value)
						{ // match
							return checked((int)(inp - ptr));
						}
						++inp;
					}
				}
			}
			return NOT_FOUND;
		}

		/// <summary>Concatenates all the elements of a slice array, using the specified separator between each element.</summary>
		/// <param name="separator">The slice to use as a separator. Can be empty.</param>
		/// <param name="values">An array that contains the elements to concatenate.</param>
		/// <returns>A slice that consists of the elements in a value delimited by the <paramref name="separator"/> slice. If <paramref name="values"/> is an empty array, the method returns <see cref="Slice.Empty"/>.</returns>
		/// <exception cref="ArgumentNullException">If <paramref name="values"/> is null.</exception>
		public static Slice Join(Slice separator, [NotNull] Slice[] values)
		{
			Contract.NotNull(values, nameof(values));

			int count = values.Length;
			if (count == 0) return Slice.Empty;
			if (count == 1) return values[0];
			return Join(separator, values, 0, count);
		}

		/// <summary>Concatenates the specified elements of a slice array, using the specified separator between each element.</summary>
		/// <param name="separator">The slice to use as a separator. Can be empty.</param>
		/// <param name="values">An array that contains the elements to concatenate.</param>
		/// <param name="startIndex">The first element in <paramref name="values"/> to use.</param>
		/// <param name="count">The number of elements of <paramref name="values"/> to use.</param>
		/// <returns>A slice that consists of the slices in <paramref name="values"/> delimited by the <paramref name="separator"/> slice. -or- <see cref="Slice.Empty"/> if <paramref name="count"/> is zero, <paramref name="values"/> has no elements, or <paramref name="separator"/> and all the elements of <paramref name="values"/> are <see cref="Slice.Empty"/>.</returns>
		/// <exception cref="ArgumentNullException">If <paramref name="values"/> is null.</exception>
		/// <exception cref="ArgumentOutOfRangeException">If <paramref name="startIndex"/> or <paramref name="count"/> is less than zero. -or- <paramref name="startIndex"/> plus <paramref name="count"/> is greater than the number of elements in <paramref name="values"/>.</exception>
		public static Slice Join(Slice separator, [NotNull] Slice[] values, int startIndex, int count)
		{
			// Note: this method is modeled after String.Join() and should behave the same
			// - Only difference is that Slice.Nil and Slice.Empty are equivalent (either for separator, or for the elements of the array)

			Contract.NotNull(values, nameof(values));

			if (startIndex < 0) throw ThrowHelper.ArgumentOutOfRangeException(nameof(startIndex), startIndex, "Start index must be a positive integer");
			if (count < 0) throw ThrowHelper.ArgumentOutOfRangeException(nameof(count), count, "Count must be a positive integer");
			if (startIndex > values.Length - count) throw ThrowHelper.ArgumentOutOfRangeException(nameof(startIndex), startIndex, "Start index must fit within the array");

			if (count == 0) return Slice.Empty;
			if (count == 1) return values[startIndex];

			int size = 0;
			for (int i = 0; i < values.Length; i++) size += values[i].Count;
			size += (values.Length - 1) * separator.Count;

			// if the size overflows, that means that the resulting buffer would need to be >= 2 GB, which is not possible!
			if (size < 0) throw new OutOfMemoryException();

			//note: we want to make sure the buffer of the writer will be the exact size (so that we can use the result as a byte[] without copying again)
			var tmp = new byte[size];
			var writer = new SliceWriter(tmp);
			for (int i = 0; i < values.Length; i++)
			{
				if (i > 0) writer.WriteBytes(in separator);
				writer.WriteBytes(in values[i]);
			}
			Contract.Assert(writer.Buffer.Length == size);
			return writer.ToSlice();
		}

		/// <summary>Concatenates the specified elements of a slice sequence, using the specified separator between each element.</summary>
		/// <param name="separator">The slice to use as a separator. Can be empty.</param>
		/// <param name="values">A sequence will return the elements to concatenate.</param>
		/// <returns>A slice that consists of the slices in <paramref name="values"/> delimited by the <paramref name="separator"/> slice. -or- <see cref="Slice.Empty"/> if <paramref name="values"/> has no elements, or <paramref name="separator"/> and all the elements of <paramref name="values"/> are <see cref="Slice.Empty"/>.</returns>
		/// <exception cref="ArgumentNullException">If <paramref name="values"/> is null.</exception>
		public static Slice Join(Slice separator, [NotNull] IEnumerable<Slice> values)
		{
			Contract.NotNull(values, nameof(values));
			var array = (values as Slice[]) ?? values.ToArray();
			return Join(separator, array, 0, array.Length);
		}

		/// <summary>Concatenates the specified elements of a slice array, using the specified separator between each element.</summary>
		/// <param name="separator">The slice to use as a separator. Can be empty.</param>
		/// <param name="values">An array that contains the elements to concatenate.</param>
		/// <param name="startIndex">The first element in <paramref name="values"/> to use.</param>
		/// <param name="count">The number of elements of <paramref name="values"/> to use.</param>
		/// <returns>A byte array that consists of the slices in <paramref name="values"/> delimited by the <paramref name="separator"/> slice. -or- an empty array if <paramref name="count"/> is zero, <paramref name="values"/> has no elements, or <paramref name="separator"/> and all the elements of <paramref name="values"/> are <see cref="Slice.Empty"/>.</returns>
		/// <exception cref="ArgumentNullException">If <paramref name="values"/> is null.</exception>
		/// <exception cref="ArgumentOutOfRangeException">If <paramref name="startIndex"/> or <paramref name="count"/> is less than zero. -or- <paramref name="startIndex"/> plus <paramref name="count"/> is greater than the number of elements in <paramref name="values"/>.</exception>
		[NotNull]
		public static byte[] JoinBytes(Slice separator, [NotNull] Slice[] values, int startIndex, int count)
		{
			// Note: this method is modeled after String.Join() and should behave the same
			// - Only difference is that Slice.Nil and Slice.Empty are equivalent (either for separator, or for the elements of the array)

			Contract.NotNull(values, nameof(values));
			//REVIEW: support negative indexing ?
			if (startIndex < 0) throw ThrowHelper.ArgumentOutOfRangeException(nameof(startIndex), startIndex, "Start index must be a positive integer");
			if (count < 0) throw ThrowHelper.ArgumentOutOfRangeException(nameof(count), count, "Count must be a positive integer");
			if (startIndex > values.Length - count) throw ThrowHelper.ArgumentOutOfRangeException(nameof(startIndex), startIndex, "Start index must fit within the array");

			if (count == 0) return System.Array.Empty<byte>();
			if (count == 1) return values[startIndex].GetBytes() ?? System.Array.Empty<byte>();

			int size = 0;
			for (int i = 0; i < count; i++) size = checked(size + values[startIndex + i].Count);
			size = checked(size + (count - 1) * separator.Count);

			// if the size overflows, that means that the resulting buffer would need to be >= 2 GB, which is not possible!
			if (size < 0) throw new OutOfMemoryException();

			//note: we want to make sure the buffer of the writer will be the exact size (so that we can use the result as a byte[] without copying again)
			var tmp = new byte[size];
			int p = 0;
			for (int i = 0; i < count; i++)
			{
				if (i > 0) separator.WriteTo(tmp, ref p);
				values[startIndex + i].WriteTo(tmp, ref p);
			}
			Contract.Assert(p == tmp.Length);
			return tmp;
		}

		/// <summary>Concatenates the specified elements of a slice sequence, using the specified separator between each element.</summary>
		/// <param name="separator">The slice to use as a separator. Can be empty.</param>
		/// <param name="values">A sequence will return the elements to concatenate.</param>
		/// <returns>A byte array that consists of the slices in <paramref name="values"/> delimited by the <paramref name="separator"/> slice. -or- an empty array if <paramref name="values"/> has no elements, or <paramref name="separator"/> and all the elements of <paramref name="values"/> are <see cref="Slice.Empty"/>.</returns>
		/// <exception cref="ArgumentNullException">If <paramref name="values"/> is null.</exception>
		[NotNull]
		public static byte[] JoinBytes(Slice separator, [NotNull] IEnumerable<Slice> values)
		{
			Contract.NotNull(values, nameof(values));
			var array = (values as Slice[]) ?? values.ToArray();
			return JoinBytes(separator, array, 0, array.Length);
		}

		/// <summary>Returns a slice array that contains the sub-slices in <paramref name="input"/> that are delimited by <paramref name="separator"/>. A parameter specifies whether to return empty array elements.</summary>
		/// <param name="input">Input slice that must be split into sub-slices</param>
		/// <param name="separator">Separator that delimits the sub-slices in <paramref name="input"/>. Cannot be empty or nil</param>
		/// <param name="options"><see cref="StringSplitOptions.RemoveEmptyEntries"/> to omit empty array alements from the array returned; or <see cref="StringSplitOptions.None"/> to include empty array elements in the array returned.</param>
		/// <returns>An array whose elements contain the sub-slices that are delimited by <paramref name="separator"/>.</returns>
		/// <exception cref="System.ArgumentException">If <paramref name="separator"/> is empty, or if <paramref name="options"/> is not one of the <see cref="StringSplitOptions"/> values.</exception>
		/// <remarks>If <paramref name="input"/> does not contain the delimiter, the returned array consists of a single element that repeats the input, or an empty array if input is itself empty.
		/// To reduce memory usage, the sub-slices returned in the array will all share the same underlying buffer of the input slice.</remarks>
		[NotNull]
		public static Slice[] Split(Slice input, Slice separator, StringSplitOptions options = StringSplitOptions.None)
		{
			// this method is made to behave the same way as String.Split(), especially the following edge cases
			// - Empty.Split(..., StringSplitOptions.None) => { Empty }
			// - Empty.Split(..., StringSplitOptions.RemoveEmptyEntries) => { }
			// differences:
			// - If input is Nil, it is considered equivalent to Empty
			// - If separator is Nil or Empty, the method throws

			var list = new List<Slice>();

			if (separator.Count <= 0) throw ThrowHelper.ArgumentException(nameof(separator), "Separator must have at least one byte");
			if (options < StringSplitOptions.None || options > StringSplitOptions.RemoveEmptyEntries) throw ThrowHelper.ArgumentException(nameof(options));

			bool skipEmpty = options.HasFlag(StringSplitOptions.RemoveEmptyEntries);
			if (input.Count == 0)
			{
				return skipEmpty ? System.Array.Empty<Slice>() : new[] { Slice.Empty };
			}

			while (input.Count > 0)
			{
				int p = Find(input, separator);
				if (p < 0)
				{ // last chunk
					break;
				}
				if (p == 0)
				{ // empty chunk
					if (!skipEmpty) list.Add(Slice.Empty);
				}
				else
				{
					list.Add(input.Substring(0, p));
				}
				// note: we checked earlier that separator.Count > 0, so we are guaranteed to advance the cursor
				input = input.Substring(p + separator.Count);
			}

			if (input.Count > 0 || !skipEmpty)
			{
				list.Add(input);
			}

			return list.ToArray();
		}

		/// <summary>Returns a slice array that contains the sub-slices in <paramref name="input"/> by cutting fixed-length chunks or size <paramref name="stride"/>.</summary>
		/// <param name="input">Input slice that must be split into sub-slices</param>
		/// <param name="stride">Size of each chunk that will be cut from <paramref name="input"/>. Must be greater or equal to 1.</param>
		/// <returns>
		/// An array whose elements contain the sub-slices, each of size <paramref name="stride"/>, except the last slice that may be smaller if the length of <paramref name="input"/> is not a multiple of <paramref name="stride"/>.
		/// If <paramref name="input"/> is <see cref="Slice.Nil"/> then the array will be empty.
		/// If it is <see cref="Slice.Empty"/> then the array will we of length 1 and contain the empty slice.
		/// </returns>
		/// <remarks>To reduce memory usage, the sub-slices returned in the array will all share the same underlying buffer of the input slice.</remarks>
		[NotNull]
		public static Slice[] Split(Slice input, int stride)
		{
			Contract.GreaterOrEqual(stride, 1, nameof (stride));

			if (input.IsNull) return System.Array.Empty<Slice>();

			if (input.Count <= stride)
			{ // single element
				return new [] { input };
			}

			// how many slices? (last one may be incomplete)
			int count = (input.Count + (stride - 1)) / stride;
			var result = new Slice[count];

			int p = 0;
			int r = input.Count;
			for(int i = 0; i < result.Length; i++)
			{
				Contract.Assert(r >= 0);
				result[i] = new Slice(input.Array, input.Offset + p, Math.Min(r, stride));
				p += stride;
				r -= stride;
			}

			return result;
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
		public static Slice Increment(Slice slice)
		{
			if (slice.IsNull) throw ThrowHelper.ArgumentException(nameof(slice), "Cannot increment null buffer");

			int lastNonFfByte;
			var tmp = slice.GetBytesOrEmpty();
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
				throw ThrowHelper.ArgumentException(nameof(slice), "Cannot increment key"); //TODO: PoneyDB.Errors.CannotIncrementKey();
			}

			return new Slice(tmp, 0, lastNonFfByte + 1);
		}

		/// <summary>Merge an array of keys with a same prefix, all sharing the same buffer</summary>
		/// <param name="prefix">Prefix shared by all keys</param>
		/// <param name="keys">Array of keys to pack</param>
		/// <returns>Array of slices (for all keys) that share the same underlying buffer</returns>
		[NotNull]
		public static Slice[] Merge(Slice prefix, [NotNull] Slice[] keys)
		{
			Contract.NotNull(keys, nameof(keys));

			//REVIEW: merge this code with Slice.ConcatRange!

			if (keys.Length == 0) return System.Array.Empty<Slice>();

			// we can pre-allocate exactly the buffer by computing the total size of all keys
			int size = keys.Length * prefix.Count;
			for (int i = 0; i < keys.Length; i++) size += keys[i].Count;

			var writer = new SliceWriter(size);
			var next = new List<int>(keys.Length);

			//TODO: use multiple buffers if item count is huge ?
			bool hasPrefix = prefix.IsPresent;
			foreach (var key in keys)
			{
				if (hasPrefix) writer.WriteBytes(in prefix);
				writer.WriteBytes(in key);
				next.Add(writer.Position);
			}

			return SplitIntoSegments(writer.Buffer, 0, next);
		}

		/// <summary>Merge a sequence of keys with a same prefix, all sharing the same buffer</summary>
		/// <param name="prefix">Prefix shared by all keys</param>
		/// <param name="keys">Sequence of keys to pack</param>
		/// <returns>Array of slices (for all keys) that share the same underlying buffer</returns>
		[NotNull]
		public static Slice[] Merge(Slice prefix, [NotNull] IEnumerable<Slice> keys)
		{
			Contract.NotNull(keys, nameof(keys));

			//REVIEW: merge this code with Slice.ConcatRange!

			// use optimized version for arrays
			if (keys is Slice[] array) return Merge(prefix, array);

			// pre-allocate with a count if we can get one...
			var next = keys is ICollection<Slice> coll ? new List<int>(coll.Count) : new List<int>();
			var writer = default(SliceWriter);

			//TODO: use multiple buffers if item count is huge ?

			bool hasPrefix = prefix.IsPresent;
			foreach (var key in keys)
			{
				if (hasPrefix) writer.WriteBytes(in prefix);
				writer.WriteBytes(in key);
				next.Add(writer.Position);
			}

			return SplitIntoSegments(writer.Buffer, 0, next);
		}

		/// <summary>Creates a new slice that contains the same byte repeated</summary>
		/// <param name="value">Byte that will fill the slice</param>
		/// <param name="count">Number of bytes</param>
		/// <returns>New slice that contains <paramref name="count"/> times the byte <paramref name="value"/>.</returns>
		public static Slice Repeat(byte value, int count)
		{
			Contract.Positive(count, nameof(count), "count");
			if (count == 0) return Slice.Empty;

			var res = new byte[count];
			UnsafeHelpers.Fill(res, 0, count, value);
			return new Slice(res);
		}

		/// <summary>Creates a new slice that contains the same byte repeated</summary>
		/// <param name="value">ASCII character (between 0 and 255) that will fill the slice. If <paramref name="value"/> is greater than 0xFF, only the 8 lowest bits will be used</param>
		/// <param name="count">Number of bytes</param>
		/// <returns>New slice that contains <paramref name="count"/> times the byte <paramref name="value"/>.</returns>
		public static Slice Repeat(char value, int count)
		{
			Contract.Positive(count, nameof(count), "count");
			if (count == 0) return Slice.Empty;

			var res = new byte[count];
			UnsafeHelpers.Fill(res, 0, count, (byte) value);
			return new Slice(res);
		}

		/// <summary>Create a new slice filled with random bytes taken from a random number generator</summary>
		/// <param name="prng">Pseudo random generator to use (needs locking if instance is shared)</param>
		/// <param name="count">Number of random bytes to generate</param>
		/// <returns>Slice of <paramref name="count"/> bytes taken from <paramref name="prng"/></returns>
		/// <remarks>Warning: <see cref="System.Random"/> is not thread-safe ! If the <paramref name="prng"/> instance is shared between threads, then it needs to be locked before calling this method.</remarks>
		public static Slice Random([NotNull] Random prng, int count)
		{
			Contract.NotNull(prng, nameof(prng));
			if (count < 0) throw ThrowHelper.ArgumentOutOfRangeException(nameof(count), count, "Count cannot be negative");
			if (count == 0) return Slice.Empty;

			var bytes = new byte[count];
			prng.NextBytes(bytes);
			return new Slice(bytes, 0, count);
		}

		/// <summary>Create a new slice filled with random bytes taken from a cryptographic random number generator</summary>
		/// <param name="rng">Random generator to use (needs locking if instance is shared)</param>
		/// <param name="count">Number of random bytes to generate</param>
		/// <param name="nonZeroBytes">If true, produce a sequence of non-zero bytes.</param>
		/// <returns>Slice of <paramref name="count"/> bytes taken from <paramref name="rng"/></returns>
		/// <remarks>Warning: All RNG implementations may not be thread-safe ! If the <paramref name="rng"/> instance is shared between threads, then it may need to be locked before calling this method.</remarks>
		public static Slice Random([NotNull] System.Security.Cryptography.RandomNumberGenerator rng, int count, bool nonZeroBytes = false)
		{
			Contract.NotNull(rng, nameof(rng));
			if (count < 0) throw ThrowHelper.ArgumentOutOfRangeException(nameof(count), count, "Count cannot be negative");
			if (count == 0) return Slice.Empty;

			var bytes = new byte[count];

			if (nonZeroBytes)
				rng.GetNonZeroBytes(bytes);
			else
				rng.GetBytes(bytes);

			return new Slice(bytes, 0, count);
		}

		/// <summary>Returns the lowest of two keys</summary>
		/// <param name="a">First key</param>
		/// <param name="b">Second key</param>
		/// <returns>The key that is BEFORE the other, using lexicographical order</returns>
		/// <remarks>If both keys are equal, then <paramref name="a"/> is returned</remarks>
		public static Slice Min(Slice a, Slice b)
		{
			return a.CompareTo(b) <= 0 ? a : b;
		}

		/// <summary>Returns the lowest of three keys</summary>
		/// <param name="a">First key</param>
		/// <param name="b">Second key</param>
		/// <param name="c">Second key</param>
		/// <returns>The key that is BEFORE the other two, using lexicographical order</returns>
		public static Slice Min(Slice a, Slice b, Slice c)
		{
			return a.CompareTo(b) <= 0
				? (a.CompareTo(c) <= 0 ? a : c)
				: (b.CompareTo(c) <= 0 ? b : c);
		}

		public static Slice Min(params Slice[] values)
		{
			switch (values.Length)
			{
				case 0: return Slice.Nil;
				case 1: return values[0];
				case 2: return Min(values[0], values[1]);
				case 3: return Min(values[0], values[1], values[3]);
				default:
				{
					Slice min = values[0];
					for (int i = 1; i < values.Length; i++)
					{
						if (values[i].CompareTo(min) < 0) min = values[i];
					}
					return min;
				}
			}
		}

		/// <summary>Returns the highest of two keys</summary>
		/// <param name="a">First key</param>
		/// <param name="b">Second key</param>
		/// <returns>The key that is AFTER the other, using lexicographical order</returns>
		/// <remarks>If both keys are equal, then <paramref name="a"/> is returned</remarks>
		public static Slice Max(Slice a, Slice b)
		{
			return a.CompareTo(b) >= 0 ? a : b;
		}

		/// <summary>Returns the highest of three keys</summary>
		/// <param name="a">First key</param>
		/// <param name="b">Second key</param>
		/// <param name="c">Second key</param>
		/// <returns>The key that is AFTER the other two, using lexicographical order</returns>
		public static Slice Max(Slice a, Slice b, Slice c)
		{
			return a.CompareTo(b) >= 0
				? (a.CompareTo(c) >= 0 ? a : c)
				: (b.CompareTo(c) >= 0 ? b : c);
		}

		public static Slice Max(params Slice[] values)
		{
			switch (values.Length)
			{
				case 0: return Slice.Nil;
				case 1: return values[0];
				case 2: return Max(values[0], values[1]);
				case 3: return Max(values[0], values[1], values[3]);
				default:
					{
						Slice max = values[0];
						for (int i = 1; i < values.Length; i++)
						{
							if (values[i].CompareTo(max) > 0) max = values[i];
						}
						return max;
					}
			}
		}

		#region Slice arithmetics...

		/// <summary>Compare two slices for equality</summary>
		/// <returns>True if the slices contains the same bytes</returns>
		public static bool operator ==(Slice a, Slice b)
		{
			return a.Equals(b);
		}

		/// <summary>Compare two slices for inequality</summary>
		/// <returns>True if the slices do not contain the same bytes</returns>
		public static bool operator !=(Slice a, Slice b)
		{
			return !a.Equals(b);
		}

		/// <summary>Compare two slices</summary>
		/// <returns>True if <paramref name="a"/> is lexicographically less than <paramref name="a"/>; otherwise, false.</returns>
		public static bool operator <(Slice a, Slice b)
		{
			return a.CompareTo(b) < 0;
		}

		/// <summary>Compare two slices</summary>
		/// <returns>True if <paramref name="a"/> is lexicographically less than or equal to <paramref name="a"/>; otherwise, false.</returns>
		public static bool operator <=(Slice a, Slice b)
		{
			return a.CompareTo(b) <= 0;
		}

		/// <summary>Compare two slices</summary>
		/// <returns>True if <paramref name="a"/> is lexicographically greater than <paramref name="a"/>; otherwise, false.</returns>
		public static bool operator >(Slice a, Slice b)
		{
			return a.CompareTo(b) > 0;
		}

		/// <summary>Compare two slices</summary>
		/// <returns>True if <paramref name="a"/> is lexicographically greater than or equal to <paramref name="a"/>; otherwise, false.</returns>
		public static bool operator >=(Slice a, Slice b)
		{
			return a.CompareTo(b) >= 0;
		}

		/// <summary>Append/Merge two slices together</summary>
		/// <param name="a">First slice</param>
		/// <param name="b">Second slice</param>
		/// <returns>Merged slices if both slices are contigous, or a new slice containg the content of the first slice, followed by the second</returns>
		public static Slice operator +(Slice a, Slice b)
		{
			return a.Concat(b);
		}

		/// <summary>Appends a byte at the end of the slice</summary>
		/// <param name="a">First slice</param>
		/// <param name="b">Byte to append at the end</param>
		/// <returns>New slice with the byte appended</returns>
		public static Slice operator +(Slice a, byte b)
		{
			if (a.Count == 0) return Slice.FromByte(b);
			var tmp = new byte[a.Count + 1];
			UnsafeHelpers.CopyUnsafe(tmp, 0, a.Array, a.Offset, a.Count);
			tmp[a.Count] = b;
			return new Slice(tmp);
		}

		/// <summary>Remove <paramref name="n"/> bytes at the end of slice <paramref name="s"/></summary>
		/// <returns>Smaller slice</returns>
		public static Slice operator -(Slice s, int n)
		{
			if (n < 0) throw ThrowHelper.ArgumentOutOfRangeException(nameof(n), "Cannot subtract a negative number from a slice");
			if (n > s.Count) throw ThrowHelper.ArgumentOutOfRangeException(nameof(n), "Cannout substract more bytes than the slice contains");

			if (n == 0) return s;
			if (n == s.Count) return Slice.Empty;

			return new Slice(s.Array, s.Offset, s.Count - n);
		}

		// note: We also need overloads with Nullable<Slice>'s to be able to do things like "if (slice == null)", "if (slice != null)" or "if (null != slice)".
		// For structs that have "==" / "!=" operators, the compiler will think that when you write "slice == null", you really mean "(Slice?)slice == default(Slice?)", and that would ALWAYS false if you don't have specialized overloads to intercept.

		/// <summary>Determines whether two specified instances of <see cref="Slice"/> are equal</summary>
		public static bool operator ==(Slice? a, Slice? b)
		{
			return a.GetValueOrDefault().Equals(b.GetValueOrDefault());
		}

		/// <summary>Determines whether two specified instances of <see cref="Slice"/> are not equal</summary>
		public static bool operator !=(Slice? a, Slice? b)
		{
			return !a.GetValueOrDefault().Equals(b.GetValueOrDefault());
		}

		/// <summary>Determines whether one specified <see cref="Slice"/> is less than another specified <see cref="Slice"/>.</summary>
		public static bool operator <(Slice? a, Slice? b)
		{
			return a.GetValueOrDefault() < b.GetValueOrDefault();
		}

		/// <summary>Determines whether one specified <see cref="Slice"/> is less than or equal to another specified <see cref="Slice"/>.</summary>
		public static bool operator <=(Slice? a, Slice? b)
		{
			return a.GetValueOrDefault() <= b.GetValueOrDefault();
		}

		/// <summary>Determines whether one specified <see cref="Slice"/> is greater than another specified <see cref="Slice"/>.</summary>
		public static bool operator >(Slice? a, Slice? b)
		{
			return a.GetValueOrDefault() > b.GetValueOrDefault();
		}

		/// <summary>Determines whether one specified <see cref="Slice"/> is greater than or equal to another specified <see cref="Slice"/>.</summary>
		public static bool operator >=(Slice? a, Slice? b)
		{
			return a.GetValueOrDefault() >= b.GetValueOrDefault();
		}

		/// <summary>Concatenates two <see cref="Slice"/> together.</summary>
		public static Slice operator +(Slice? a, Slice? b)
		{
			// note: makes "slice + null" work!
			return a.GetValueOrDefault().Concat(b.GetValueOrDefault());
		}

		#endregion

		/// <summary>Returns a printable representation of the key</summary>
		/// <remarks>You can roundtrip the result of calling slice.ToString() by passing it to <see cref="Slice.Unescape"/>(string) and get back the original slice.</remarks>
		public override string ToString()
		{
			return Dump(this);
		}

		public string ToString(string format)
		{
			return ToString(format, null);
		}

		/// <summary>Formats the slice using the specified encoding</summary>
		/// <param name="format">A single format specifier that indicates how to format the value of this Slice. The <paramref name="format"/> parameter can be "N", "D", "X", or "P". If format is null or an empty string (""), "D" is used. A lower case character will usually produce lowercased hexadecimal letters.</param>
		/// <param name="provider">This paramater is not used</param>
		/// <returns></returns>
		/// <remarks>
		/// The format <b>D</b> is the default, and produce a round-trippable version of the slice, using &lt;XX&gt; tokens for non-printables bytes.
		/// The format <b>N</b> (or <b>n</b>) produces a compact hexadecimal string (without separators).
		/// The format <b>X</b> (or <b>x</b>) produces an hexadecimal string with spaces between each bytes.
		/// The format <b>P</b> is the equivalent of calling <see cref="PrettyPrint()"/>.
		/// </remarks>
		public string ToString(string format, IFormatProvider provider)
		{
			switch (format ?? "D")
			{
				case "D":
				case "d":
					return Dump(this);

				case "N":
					return ToHexaString(lower: false);
				case "n":
					return ToHexaString(lower: true);

				case "X":
					return ToHexaString(' ', lower: false);
				case "x":
					return ToHexaString(' ', lower: true);

				case "P":
				case "p":
					return PrettyPrint();

				case "K":
				case "k":
					return PrettyPrint(); //TODO: Key ! (cf USlice)

				case "V":
				case "v":
					return PrettyPrint(); //TODO: Value ! (cf USlice)

				default:
					throw new FormatException("Format is invalid or not supported");
			}
		}

		/// <summary>Returns a printable representation of a key</summary>
		/// <remarks>This may not be efficient, so it should only be use for testing/logging/troubleshooting</remarks>
		[NotNull]
		public static string Dump(Slice value, int maxSize = 1024) //REVIEW: rename this to Encode(..) or Escape(..)
		{
			if (value.Count == 0) return value.HasValue ? "<empty>" : "<null>";

			value.EnsureSliceIsValid();

			var buffer = value.Array;
			int count = Math.Min(value.Count, maxSize);
			int pos = value.Offset;

			var sb = new StringBuilder(count + 16);
			while (count-- > 0)
			{
				int c = buffer[pos++];
				if (c < 32 || c >= 127 || c == 60)
				{
					sb.Append('<');
					int x = c >> 4;
					sb.Append((char)(x + (x < 10 ? 48 : 55)));
					x = c & 0xF;
					sb.Append((char)(x + (x < 10 ? 48 : 55)));
					sb.Append('>');
				}
				else
				{
					sb.Append((char)c);
				}
			}
			if (value.Count > maxSize) sb.Append("[\u2026]"); // Unicode for '...'
			return sb.ToString();
		}

		/// <summary>Decode the string that was generated by slice.ToString() or Slice.Dump(), back into the original slice</summary>
		/// <remarks>This may not be efficient, so it should only be use for testing/logging/troubleshooting</remarks>
		public static Slice Unescape(string value) //REVIEW: rename this to Decode() if we changed Dump() to Encode()
		{
			var writer = default(SliceWriter);
			for (int i = 0; i < value.Length; i++)
			{
				char c = value[i];
				if (c == '<')
				{
					if (value[i + 3] != '>') throw new FormatException($"Invalid escape character at offset {i}");
					c = (char)(NibbleToDecimal(value[i + 1]) << 4 | NibbleToDecimal(value[i + 2]));
					i += 3;
				}
				writer.WriteByte((byte)c);
			}
			return writer.ToSlice();
		}

		#region Streams...

		/// <summary>Read the content of a stream into a slice</summary>
		/// <param name="data">Source stream, that must be in a readable state</param>
		/// <returns>Slice containing the stream content (or <see cref="Slice.Nil"/> if the stream is <see cref="Stream.Null"/>)</returns>
		/// <exception cref="ArgumentNullException">If <paramref name="data"/> is null.</exception>
		/// <exception cref="InvalidOperationException">If the size of the <paramref name="data"/> stream exceeds <see cref="int.MaxValue"/> or if it does not support reading.</exception>
		public static Slice FromStream([NotNull] Stream data)
		{
			Contract.NotNull(data, nameof(data));

			// special case for empty values
			if (data == Stream.Null) return Slice.Nil;
			if (!data.CanRead) throw ThrowHelper.InvalidOperationException("Cannot read from provided stream");

			if (data.Length == 0) return Slice.Empty;
			if (data.Length > int.MaxValue) throw ThrowHelper.InvalidOperationException("Streams of more than 2GB are not supported");
			//TODO: other checks?

			int length;
			checked { length = (int)data.Length; }

			if (data is MemoryStream || data is UnmanagedMemoryStream) // other types of already completed streams ?
			{ // read synchronously
				return LoadFromNonBlockingStream(data, length);
			}

			// read asynchronoulsy
			return LoadFromBlockingStream(data, length);
		}

		/// <summary>Asynchronously read the content of a stream into a slice</summary>
		/// <param name="data">Source stream, that must be in a readable state</param>
		/// <param name="ct">Optional cancellation token for this operation</param>
		/// <returns>Slice containing the stream content (or <see cref="Slice.Nil"/> if the stream is <see cref="Stream.Null"/>)</returns>
		/// <exception cref="ArgumentNullException">If <paramref name="data"/> is null.</exception>
		/// <exception cref="InvalidOperationException">If the size of the <paramref name="data"/> stream exceeds <see cref="int.MaxValue"/> or if it does not support reading.</exception>
		public static Task<Slice> FromStreamAsync([NotNull] Stream data, CancellationToken ct)
		{
			Contract.NotNull(data, nameof(data));

			// special case for empty values
			if (data == Stream.Null) return Task.FromResult(Slice.Nil);
			if (!data.CanRead) throw ThrowHelper.InvalidOperationException("Cannot read from provided stream");

			if (data.Length == 0) return Task.FromResult(Slice.Empty);
			if (data.Length > int.MaxValue) throw ThrowHelper.InvalidOperationException("Streams of more than 2GB are not supported");
			//TODO: other checks?

			if (ct.IsCancellationRequested) return Task.FromCanceled<Slice>(ct);

			int length;
			checked { length = (int)data.Length; }

			if (data is MemoryStream || data is UnmanagedMemoryStream) // other types of already completed streams ?
			{ // read synchronously
				return Task.FromResult(LoadFromNonBlockingStream(data, length));
			}

			// read asynchronoulsy
			return LoadFromBlockingStreamAsync(data, length, 0, ct);
		}

		/// <summary>Read from a non-blocking stream that already contains all the data in memory (MemoryStream, UnmanagedStream, ...)</summary>
		/// <param name="source">Source stream</param>
		/// <param name="length">Number of bytes to read from the stream</param>
		/// <returns>Slice containing the loaded data</returns>
		private static Slice LoadFromNonBlockingStream([NotNull] Stream source, int length)
		{
			Contract.Requires(source != null && source.CanRead && source.Length <= int.MaxValue);

			if (source is MemoryStream ms)
			{ // Already holds onto a byte[]

				//note: should be use GetBuffer() ? It can throws and is dangerous (could mutate)
				return ms.ToArray().AsSlice();
			}

			// read it in bulk, without buffering

			var buffer = new byte[length]; //TODO: round up to avoid fragmentation ?

			// note: reading should usually complete with only one big read, but loop until completed, just to be sure
			int p = 0;
			int r = length;
			while (r > 0)
			{
				int n = source.Read(buffer, p, r);
				if (n <= 0) throw ThrowHelper.InvalidOperationException($"Unexpected end of stream at {p:N0} / {length:N0} bytes");
				p += n;
				r -= n;
			}
			Contract.Assert(r == 0 && p == length);

			return buffer.AsSlice();
		}

		/// <summary>Synchronously read from a blocking stream (FileStream, NetworkStream, ...)</summary>
		/// <param name="source">Source stream</param>
		/// <param name="length">Number of bytes to read from the stream</param>
		/// <param name="chunkSize">If non zero, max amount of bytes to read in one chunk. If zero, tries to read everything at once</param>
		/// <returns>Slice containing the loaded data</returns>
		private static Slice LoadFromBlockingStream([NotNull] Stream source, int length, int chunkSize = 0)
		{
			Contract.Requires(source != null && source.CanRead && source.Length <= int.MaxValue && chunkSize >= 0);

			if (chunkSize == 0) chunkSize = int.MaxValue;

			var buffer = new byte[length]; //TODO: round up to avoid fragmentation ?

			// note: reading should usually complete with only one big read, but loop until completed, just to be sure
			int p = 0;
			int r = length;
			while (r > 0)
			{
				int c = Math.Max(r, chunkSize);
				int n = source.Read(buffer, p, c);
				if (n <= 0) throw ThrowHelper.InvalidOperationException($"Unexpected end of stream at {p:N0} / {length:N0} bytes");
				p += n;
				r -= n;
			}
			Contract.Assert(r == 0 && p == length);

			return buffer.AsSlice();
		}

		/// <summary>Asynchronously read from a blocking stream (FileStream, NetworkStream, ...)</summary>
		/// <param name="source">Source stream</param>
		/// <param name="length">Number of bytes to read from the stream</param>
		/// <param name="chunkSize">If non zero, max amount of bytes to read in one chunk. If zero, tries to read everything at once</param>
		/// <param name="ct">Optional cancellation token for this operation</param>
		/// <returns>Slice containing the loaded data</returns>
		private static async Task<Slice> LoadFromBlockingStreamAsync([NotNull] Stream source, int length, int chunkSize, CancellationToken ct)
		{
			Contract.Requires(source != null && source.CanRead && source.Length <= int.MaxValue && chunkSize >= 0);

			if (chunkSize == 0) chunkSize = int.MaxValue;

			var buffer = new byte[length]; //TODO: round up to avoid fragmentation ?

			// note: reading should usually complete with only one big read, but loop until completed, just to be sure
			int p = 0;
			int r = length;
			while (r > 0)
			{
				int c = Math.Min(r, chunkSize);
				int n = await source.ReadAsync(buffer, p, c, ct);
				if (n <= 0) throw ThrowHelper.InvalidOperationException($"Unexpected end of stream at {p:N0} / {length:N0} bytes");
				p += n;
				r -= n;
			}
			Contract.Assert(r == 0 && p == length);

			return buffer.AsSlice();
		}

		#endregion

		#region Equality, Comparison...

		/// <summary>Checks if an object is equal to the current slice</summary>
		/// <param name="obj">Object that can be either another slice, a byte array, or a byte array segment.</param>
		/// <returns>true if the object represents a sequence of bytes that has the same size and same content as the current slice.</returns>
		public override bool Equals(object obj)
		{
			switch (obj)
			{
				case null: return this.Array == null;
				case Slice slice: return Equals(slice);
				case ArraySegment<byte> segment: return Equals(segment);
				case byte[] bytes: return Equals(bytes);
			}
			return false;
		}

		/// <summary>Gets the hash code for this slice</summary>
		/// <returns>A 32-bit signed hash code calculated from all the bytes in the slice.</returns>
		public override int GetHashCode()
		{
			EnsureSliceIsValid();
			return this.Array == null ? 0 : UnsafeHelpers.ComputeHashCodeUnsafe(this.Array, this.Offset, this.Count);
		}

		/// <summary>Checks if another slice is equal to the current slice.</summary>
		/// <param name="other">Slice compared with the current instance</param>
		/// <returns>true if both slices have the same size and contain the same sequence of bytes; otherwise, false.</returns>
		public bool Equals(Slice other)
		{
			other.EnsureSliceIsValid();
			this.EnsureSliceIsValid();

			// note: Slice.Nil != Slice.Empty
			if (this.Array == null) return other.Array == null;
			if (other.Array == null) return false;

			return this.Count == other.Count && UnsafeHelpers.SameBytesUnsafe(this.Array, this.Offset, other.Array, other.Offset, this.Count);
		}

		/// <summary>Lexicographically compare this slice with another one, and return an indication of their relative sort order</summary>
		/// <param name="other">Slice to compare with this instance</param>
		/// <returns>Returns a NEGATIVE value if the current slice is LESS THAN <paramref name="other"/>, ZERO if it is EQUAL TO <paramref name="other"/>, and a POSITIVE value if it is GREATER THAN <paramref name="other"/>.</returns>
		/// <remarks>If both this instance and <paramref name="other"/> are Nil or Empty, the comparison will return ZERO. If only <paramref name="other"/> is Nil or Empty, it will return a NEGATIVE value. If only this instance is Nil or Empty, it will return a POSITIVE value.</remarks>
		public int CompareTo(Slice other)
		{
			if (this.Count == 0) return other.Count == 0 ? 0 : -1;
			if (other.Count == 0) return +1;
			other.EnsureSliceIsValid();
			this.EnsureSliceIsValid();
			return UnsafeHelpers.CompareUnsafe(this.Array, this.Offset, this.Count, other.Array, other.Offset, other.Count);
		}

		/// <summary>Checks if the content of a byte array segment matches the current slice.</summary>
		/// <param name="other">Byte array segment compared with the current instance</param>
		/// <returns>true if both segment and slice have the same size and contain the same sequence of bytes; otherwise, false.</returns>
		public bool Equals(ArraySegment<byte> other)
		{
			return this.Count == other.Count && UnsafeHelpers.SameBytes(this.Array, this.Offset, other.Array, other.Offset, this.Count);
		}

		/// <summary>Checks if the content of a byte array matches the current slice.</summary>
		/// <param name="other">Byte array compared with the current instance</param>
		/// <returns>true if the both array and slice have the same size and contain the same sequence of bytes; otherwise, false.</returns>
		public bool Equals(byte[] other)
		{
			if (other == null) return this.Array == null;
			return this.Count == other.Length && UnsafeHelpers.SameBytes(this.Array, this.Offset, other, 0, this.Count);
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
			// - Count greather than 0 and Array not null and all the bytes of the slice are contained in the underlying buffer

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
		/// <param name="slice">Slice that is being naugthy</param>
		[Pure, NotNull, MethodImpl(MethodImplOptions.NoInlining)]
		public static Exception MalformedSlice(Slice slice)
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

		/// <summary>Return the sum of the size of all the slices with an additionnal prefix</summary>
		/// <param name="prefix">Size of a prefix that would be added before each slice</param>
		/// <param name="slices">Array of slices</param>
		/// <returns>Combined total size of all the slices and the prefixes</returns>
		public static int GetTotalSize(int prefix, [NotNull] Slice[] slices)
		{
			long size = prefix * slices.Length;
			for (int i = 0; i < slices.Length; i++)
			{
				size += slices[i].Count;
			}
			return checked((int)size);
		}

		/// <summary>Return the sum of the size of all the slices with an additionnal prefix</summary>
		/// <param name="prefix">Size of a prefix that would be added before each slice</param>
		/// <param name="slices">Array of slices</param>
		/// <returns>Combined total size of all the slices and the prefixes</returns>
		public static int GetTotalSize(int prefix, [NotNull] Slice?[] slices)
		{
			long size = prefix * slices.Length;
			for (int i = 0; i < slices.Length; i++)
			{
				size += slices[i].GetValueOrDefault().Count;
			}
			return checked((int)size);
		}

		/// <summary>Return the sum of the size of all the slices with an additionnal prefix</summary>
		/// <param name="prefix">Size of a prefix that would be added before each slice</param>
		/// <param name="slices">Array of slices</param>
		/// <returns>Combined total size of all the slices and the prefixes</returns>
		public static int GetTotalSize(int prefix, [NotNull] List<Slice> slices)
		{
			long size = prefix * slices.Count;
			foreach (var val in slices)
			{
				size += val.Count;
			}
			return checked((int)size);
		}

		/// <summary>Return the sum of the size of all the slices with an additionnal prefix</summary>
		/// <param name="prefix">Size of a prefix that would be added before each slice</param>
		/// <param name="slices">Array of slices</param>
		/// <returns>Combined total size of all the slices and the prefixes</returns>
		public static int GetTotalSize(int prefix, [NotNull] List<Slice?> slices)
		{
			long size = prefix * slices.Count;
			foreach (var val in slices)
			{
				size += val.GetValueOrDefault().Count;
			}
			return checked((int)size);
		}

		/// <summary>Return the sum of the size of all the slices with an additionnal prefix, and test if they all share the same buffer</summary>
		/// <param name="prefix">Size of a prefix that would be added before each slice</param>
		/// <param name="slices">Array of slices</param>
		/// <param name="commonStore">Receives null if at least two slices are stored in a different buffer. If not null, return the common buffer for all the keys</param>
		/// <returns>Combined total size of all the slices and the prefixes</returns>
		public static int GetTotalSizeAndCommonStore(int prefix, [NotNull] Slice[] slices, out byte[] commonStore)
		{
			if (slices.Length == 0)
			{
				commonStore = null;
				return 0;
			}
			byte[] store = slices[0].Array;
			if (slices.Length == 1)
			{
				commonStore = store;
				return prefix + slices[0].Count;
			}

			bool sameStore = true;
			long size = slices[0].Count + slices.Length * prefix;
			for (int i = 1; i < slices.Length; i++)
			{
				size += slices[i].Count;
				sameStore &= (slices[i].Array == store);
			}
			commonStore = sameStore ? store : null;
			return checked((int)size);
		}

		/// <summary>Return the sum of the size of all the slices with an additionnal prefix, and test if they all share the same buffer</summary>
		/// <param name="prefix">Size of a prefix that would be added before each slice</param>
		/// <param name="slices">Array of slices</param>
		/// <param name="commonStore">Receives null if at least two slices are stored in a different buffer. If not null, return the common buffer for all the keys</param>
		/// <returns>Combined total size of all the slices and the prefixes</returns>
		public static int GetTotalSizeAndCommonStore(int prefix, [NotNull] List<Slice> slices, out byte[] commonStore)
		{
			Contract.Requires(slices != null);
			if (slices.Count == 0)
			{
				commonStore = null;
				return 0;
			}
			byte[] store = slices[0].Array;
			if (slices.Count == 1)
			{
				commonStore = store;
				return prefix + slices[0].Count;
			}

			bool sameStore = true;
			long size = slices[0].Count + slices.Count * prefix;
			foreach (var val in slices)
			{
				size += val.Count;
				sameStore &= (val.Array == store);
			}
			commonStore = sameStore ? store : null;
			return checked((int)size);
		}

		/// <summary>Structure that keeps buffers from moving in memory during GC collections</summary>
		/// <remarks>
		/// Caller must ensure that this structure is properly Disposed in all executions paths once the buffers are not needed anymore!
		/// It is safe to call Dispose() multiple times (though the buffers will be unpinned on the first call)
		/// </remarks>
		public struct Pinned : IDisposable
		{

			/// <summary>GC Handle on the main buffer</summary>
			internal GCHandle Handle;

			/// <summary>Additionnal GC Handles (optionnal)</summary>
			internal readonly GCHandle[] Handles;

			internal object Owner;

			internal Pinned([NotNull] object owner, [NotNull] byte[] buffer, [CanBeNull] List<Slice> extra)
			{
				Contract.Requires(owner != null && buffer != null);

				this.Owner = buffer;
				this.Handle = GCHandle.Alloc(buffer, GCHandleType.Pinned);
				if (extra == null || extra.Count == 0)
				{
					this.Handles = null;
				}
				else
				{
					var handles = new GCHandle[extra.Count];
					this.Handles = handles;
					int p = 0;
					foreach (var chunk in extra)
					{
						handles[p++] = GCHandle.Alloc(chunk.Array, GCHandleType.Pinned);
					}
					handles[p] = GCHandle.Alloc(buffer);
				}
			}

			public bool IsAllocated => this.Handle.IsAllocated;

			public void Dispose()
			{
				if (this.Owner != null)
				{
					if (this.Handle.IsAllocated) this.Handle.Free();
					var handles = this.Handles;
					if (handles != null)
					{
						for (int i = 0; i < handles.Length; i++)
						{
							if (handles[i].IsAllocated) handles[i].Free();
						}
					}
					this.Owner = null;
				}
			}
		}

		[UsedImplicitly(ImplicitUseTargetFlags.WithMembers)]
		private sealed class DebugView
		{
			private readonly Slice m_slice;

			public DebugView(Slice slice)
			{
				m_slice = slice;
			}

			public int Count => m_slice.Count;

			public byte[] Data
			{
				get
				{
					if (m_slice.Count == 0) return m_slice.Array == null ? null : System.Array.Empty<byte>();
					if (m_slice.Offset == 0 && m_slice.Count == m_slice.Array.Length) return m_slice.Array;
					var tmp = new byte[m_slice.Count];
					System.Array.Copy(m_slice.Array, m_slice.Offset, tmp, 0, m_slice.Count);
					return tmp;
				}
			}

			public string Content => Slice.Dump(m_slice, maxSize: 1024);

			/// <summary>Encoding using only for display purpose: we don't want to throw in the 'Text' property if the input is not text!</summary>
			[NotNull]
			private static readonly UTF8Encoding Utf8NoBomEncodingNoThrow = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: false);

			public string Text
			{
				get
				{
					if (m_slice.Count == 0) return m_slice.Array == null ? null : String.Empty;
					return EscapeString(new StringBuilder(m_slice.Count + 16), m_slice.Array, m_slice.Offset, m_slice.Count, Utf8NoBomEncodingNoThrow).ToString();
				}
			}

			public string Hexa
			{
				get
				{
					if (m_slice.Count == 0) return m_slice.Array == null ? null : String.Empty;
					return m_slice.Count <= 1024
						? m_slice.ToHexaString(' ')
						: m_slice.Substring(0, 1024).ToHexaString(' ') + "[\u2026]";
				}
			}

		}

	}

	/// <summary>Helper methods for Slice</summary>
	public static class SliceExtensions
	{
		[Pure, DebuggerNonUserCode, MethodImpl(MethodImplOptions.NoInlining)]
		private static Slice EmptyOrNil(byte[] array)
		{
			//note: we consider the "empty" or "nil" case less frequent, so we handle it in a non-inlined method
			return array == null ? default(Slice) : Slice.Empty;
		}

		/// <summary>Handle the Nil/Empty memoization</summary>
		[Pure, DebuggerNonUserCode, MethodImpl(MethodImplOptions.NoInlining)]
		private static Slice EmptyOrNil([CanBeNull] byte[] array, int count)
		{
			//note: we consider the "empty" or "nil" case less frequent, so we handle it in a non-inlined method
			if (array == null) return count == 0 ? default(Slice) : throw UnsafeHelpers.Errors.BufferArrayNotNull();
			return Slice.Empty;
		}

		/// <summary>Return a slice that wraps the whole array</summary>
		[Pure, DebuggerNonUserCode, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static Slice AsSlice([CanBeNull] this byte[] bytes)
		{
			return bytes != null && bytes.Length > 0 ? new Slice(bytes, 0, bytes.Length) : EmptyOrNil(bytes);
		}

		/// <summary>Return the tail of the array, starting from the specified offset</summary>
		/// <param name="bytes">Underlying buffer to slice</param>
		/// <param name="offset">Offset to the first byte of the slice</param>
		/// <returns></returns>
		[Pure, DebuggerNonUserCode, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static Slice AsSlice([NotNull] this byte[] bytes, [Positive] int offset)
		{
			//note: this method is DANGEROUS! Caller may thing that it is passing a count instead of an offset.
			Contract.NotNull(bytes, nameof(bytes));
			if ((uint) offset > (uint) bytes.Length) UnsafeHelpers.Errors.ThrowBufferArrayToSmall();
			return bytes.Length != 0 ? new Slice(bytes, offset, bytes.Length - offset) : Slice.Empty;
		}

		/// <summary>Return a slice from the sub-section of the byte array</summary>
		/// <param name="bytes">Underlying buffer to slice</param>
		/// <param name="offset">Offset to the first element of the slice (if not empty)</param>
		/// <param name="count">Number of bytes to take</param>
		/// <returns>
		/// Slice that maps the corresponding sub-section of the array.
		/// If <paramref name="count"/> then either <see cref="Slice.Empty">Slice.Empty</see> or <see cref="Slice.Nil">Slice.Nil</see> will be returned, in order to not keep a reference to the whole buffer.
		/// </returns>
		[Pure, DebuggerNonUserCode, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static Slice AsSlice([CanBeNull] this byte[] bytes, [Positive] int offset, [Positive] int count)
		{
			//note: this method will frequently be called with offset==0, so we should optimize for this case!
			if (bytes == null | count == 0) return EmptyOrNil(bytes, count);

			// bound check
			// ReSharper disable once PossibleNullReferenceException
			if ((uint) offset >= (uint) bytes.Length || (uint) count > (uint) (bytes.Length - offset)) UnsafeHelpers.Errors.ThrowOffsetOutsideSlice();

			return new Slice(bytes, offset, count);
		}

		/// <summary>Return a slice from the sub-section of the byte array</summary>
		/// <param name="bytes">Underlying buffer to slice</param>
		/// <param name="offset">Offset to the first element of the slice (if not empty)</param>
		/// <param name="count">Number of bytes to take</param>
		/// <returns>
		/// Slice that maps the corresponding sub-section of the array.
		/// If <paramref name="count"/> then either <see cref="Slice.Empty">Slice.Empty</see> or <see cref="Slice.Nil">Slice.Nil</see> will be returned, in order to not keep a reference to the whole buffer.
		/// </returns>
		[Pure, DebuggerNonUserCode, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static Slice AsSlice([CanBeNull] this byte[] bytes, uint offset, uint count)
		{
			//note: this method will frequently be called with offset==0, so we should optimize for this case!
			if (bytes == null | count == 0) return EmptyOrNil(bytes, (int) count);

			// bound check
			if (offset >= (uint) bytes.Length || count > ((uint) bytes.Length - offset)) UnsafeHelpers.Errors.ThrowOffsetOutsideSlice();

			return new Slice(bytes, (int) offset, (int) count);
		}

		[Pure, DebuggerNonUserCode, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static Slice AsSlice(this ArraySegment<byte> self)
		{
			// We trust the ArraySegment<byte> ctor to valide the arguments before hand.
			// If somehow the arguments were corrupted (intentionally or not), then the same problem could have happened with the slice anyway!

			// ReSharper disable once AssignNullToNotNullAttribute
			return self.Count != 0 ? new Slice(self.Array, self.Offset, self.Count) : EmptyOrNil(self.Array, self.Count);
		}

		/// <summary>Return a slice from the sub-section of an array segment</summary>
		[Pure, DebuggerNonUserCode, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static Slice AsSlice(this ArraySegment<byte> self, int offset, int count)
		{
			return AsSlice(self).Substring(offset, count);
		}

#if ENABLE_SPAN
		/// <summary>Convert this <see cref="Slice"/> into the equivalent <see cref="ReadOnlySpan{T}">ReadOnlySpan&lt;byte&gt;</see>.</summary>
		/// <remarks>Both <see cref="Slice.Nil"/> and <see cref="Slice.Empty"/> will be converted into an empty span</remarks>
		[Pure, DebuggerNonUserCode, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static ReadOnlySpan<byte> AsReadOnlySpan(this Slice self)
		{
			return new ReadOnlySpan<byte>(self.Array, self.Offset, self.Count);
		}

		/// <summary>Convert this <see cref="Slice"/> into the equivalent <see cref="ReadOnlySpan{T}">ReadOnlySpan&lt;byte&gt;</see>.</summary>
		/// <exception cref="ArgumentNullException">If <see cref="self"/> is <see cref="Slice.Nil"/></exception>
		[Pure, DebuggerNonUserCode, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static ReadOnlySpan<byte> AsReadOnlySpan(this Slice self, int start)
		{
			var x = self.Substring(start);
			return new ReadOnlySpan<byte>(x.Array, x.Offset, x.Count);
		}

		/// <summary>Convert this <see cref="Slice"/> into the equivalent <see cref="ReadOnlySpan{T}">ReadOnlySpan&lt;byte&gt;</see>.</summary>
		/// <exception cref="ArgumentNullException">If <see cref="self"/> is <see cref="Slice.Nil"/></exception>
		[Pure, DebuggerNonUserCode, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static ReadOnlySpan<byte> AsReadOnlySpan(this Slice self, int start, int length)
		{
			var x = self.Substring(start, length);
			return new ReadOnlySpan<byte>(x.Array, x.Offset, x.Count);
		}

		/// <summary>Convert this <see cref="Slice"/> into the equivalent <see cref="Span{T}">Span&lt;byte&gt;</see>.</summary>
		/// <remarks>Both <see cref="Slice.Nil"/> and <see cref="Slice.Empty"/> will be converted into an empty span</remarks>
		[Pure, DebuggerNonUserCode, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static Span<byte> AsSpan(this Slice self)
		{
			return new Span<byte>(self.Array, self.Offset, self.Count);
		}
		
		/// <summary>Convert this <see cref="Slice"/> into the equivalent <see cref="Span{T}">Span&lt;byte&gt;</see>.</summary>
		/// <exception cref="ArgumentNullException">If <see cref="self"/> is <see cref="Slice.Nil"/></exception>
		[Pure, DebuggerNonUserCode, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static Span<byte> AsSpan(this Slice self, int start)
		{
			var x = self.Substring(start);
			return new Span<byte>(x.Array, x.Offset, x.Count);
		}

		/// <summary>Convert this <see cref="Slice"/> into the equivalent <see cref="Span{T}">Span&lt;byte&gt;</see>.</summary>
		/// <exception cref="ArgumentNullException">If <see cref="self"/> is <see cref="Slice.Nil"/></exception>
		[Pure, DebuggerNonUserCode, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static Span<byte> AsSpan(this Slice self, int start, int length)
		{
			var x = self.Substring(start, length);
			return new Span<byte>(x.Array, x.Offset, x.Count);
		}
#endif

		[Pure, DebuggerNonUserCode, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static SliceReader ToSliceReader(this byte[] self)
		{
			return new SliceReader(self);
		}

		[Pure, DebuggerNonUserCode, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static SliceReader ToSliceReader(this byte[] self, int count)
		{
			return new SliceReader(self, 0, count);
		}

		[Pure, DebuggerNonUserCode, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static SliceReader ToSliceReader(this byte[] self, int offset, int count)
		{
			return new SliceReader(self, offset, count);
		}

		[Pure, NotNull, DebuggerNonUserCode]
		public static SliceStream AsStream(this Slice slice) //REVIEW: => ToStream() ?
		{
			if (slice.IsNull) throw ThrowHelper.InvalidOperationException("Slice cannot be null");
			//TODO: have a singleton for the empty slice ?
			return new SliceStream(slice);
		}

#if ENABLE_SPAN
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void CopyTo(this ReadOnlySpan<byte> source, Slice destination)
		{
			source.CopyTo(new Span<byte>(destination.Array, destination.Offset, destination.Count));
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void CopyTo(this Span<byte> source, Slice destination)
		{
			if (source.Length > 0) source.CopyTo(new Span<byte>(destination.Array, destination.Offset, destination.Count));
		}
#endif

	}

}

#endif
