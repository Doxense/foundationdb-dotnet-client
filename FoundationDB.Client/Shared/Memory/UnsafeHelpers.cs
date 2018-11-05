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

// If defined, means that the host process will ALWAYS run in a Little Endian context, and we can use some optimizations to speed up encoding and decoding values to and from memory buffers.
// If undefined, then fallback to architecture-agnostic way of handling bit and little endian values
// note: when enabled, the code assumes that the CPU supports unaligned stores and loads
#define EXPECT_LITTLE_ENDIAN_HOST

// Enable the use of Span<T> and ReadOnlySpan<T>
//#define ENABLE_SPAN

//note: we would like to use Vector<byte> from System.Numerics.Vectors (which is converted to SIMD by the JIT), but this is not really practical just yet:
// - v4.0 of the assembly does NOT have Vector<T>, which was removed between beta, and only came back in 4.1-beta
// - the ctor Vector<byte>(byte* ptr, int offset) is currently private, which means that we cannot use it with unsafe pointers yet
// - there does not seem to be any SIMD way to implement memcmp with the current Vector<T> API, unless doing some trickery with substracting and looking for 0s

#if !USE_SHARED_FRAMEWORK

namespace Doxense.Memory
{
	using System;
	using System.Diagnostics;
	using System.IO;
	using System.Runtime.CompilerServices;
	using System.Runtime.ConstrainedExecution;
	using System.Runtime.InteropServices;
	using System.Security;
	using JetBrains.Annotations;
	using Doxense.Diagnostics.Contracts;

	/// <summary>Helper methods for dealing with unmanaged memory. HANDLE WITH CARE!</summary>
	/// <remarks>Use of this class is unsafe. YOU HAVE BEEN WARNED!</remarks>
	[DebuggerNonUserCode] // <-- remove this when debugging the class itself!
	public static unsafe class UnsafeHelpers
	{

#if EXPECT_LITTLE_ENDIAN_HOST
		private const bool IsLittleEndian = true;
#else
		//note: should be optimized as a const by the JIT!
		private static readonly bool IsLittleEndian = BitConverter.IsLittleEndian;
#endif

		/// <summary>Validates that <paramref name="offset"/> and <paramref name="count"/> represent a valid location in <paramref name="array"/></summary>
		/// <remarks>If <paramref name="count"/> is 0, then <paramref name="array"/> is allowed to be null</remarks>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void EnsureBufferIsValid(byte[] array, int offset, int count)
		{
			// note: same test has for a Slice
			if (count != 0 && (array == null || (uint) offset > (uint) array.Length || (uint) count > (uint) (array.Length - offset)))
			{
				throw Errors.MalformedBuffer(array, offset, count);
			}
		}

		/// <summary>Validates that <paramref name="offset"/> and <paramref name="count"/> represent a valid location in <paramref name="array"/></summary>
		/// <remarks>If <paramref name="count"/> is 0, then <paramref name="array"/> is allowed to be null</remarks>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void EnsureBufferIsValid(byte[] array, uint offset, uint count)
		{
			// note: same test has for a Slice
			if (count != 0 && (array == null || (long) count > (long) array.Length - offset))
			{
				throw Errors.MalformedBuffer(array, offset, count);
			}
		}

		/// <summary>Validates that <paramref name="offset"/> and <paramref name="count"/> represent a valid location in <paramref name="array"/></summary>
		/// <remarks><paramref name="array"/> is not allowed to be null, even if <paramref name="count"/> is 0.</remarks>
		[ContractAnnotation("array:null => halt"), MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void EnsureBufferIsValidNotNull(byte[] array, int offset, int count)
		{
			// note: same test has for a Slice
			if (array == null || (uint) offset > (uint) array.Length || (uint) count > (uint) (array.Length - offset))
			{
				throw Errors.MalformedBuffer(array, offset, count);
			}
		}

		/// <summary>Validates that <paramref name="offset"/> and <paramref name="count"/> represent a valid location in <paramref name="array"/></summary>
		/// <remarks><paramref name="array"/> is not allowed to be null, even if <paramref name="count"/> is 0.</remarks>
		[ContractAnnotation("array:null => halt"), MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void EnsureBufferIsValidNotNull(byte[] array, uint offset, uint count)
		{
			// note: same test has for a Slice
			if (array == null || (long) count > (long) array.Length - offset)
			{
				throw Errors.MalformedBuffer(array, offset, count);
			}
		}

		/// <summary>Validates that an unmanged buffer represents a valid memory location</summary>
		/// <remarks>If <paramref name="count"/> is 0, then <paramref name="bytes"/> is allowed to be null</remarks>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void EnsureBufferIsValid(byte* bytes, long count)
		{
			if (count != 0 & (bytes == null || count < 0))
			{
				throw Errors.MalformedBuffer(bytes, count);
			}
		}

		/// <summary>Validates that an unmanaged buffer represents a valid memory location</summary>
		/// <remarks><paramref name="bytes"/> is not allowed to be null, even if <paramref name="count"/> is 0.</remarks>
		[ContractAnnotation("bytes:null => halt")]
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void EnsureBufferIsValidNotNull(byte* bytes, long count)
		{
			if (bytes == null || count < 0)
			{
				throw Errors.MalformedBuffer(bytes, count);
			}
		}

		/// <summary>Compare two byte segments for equality</summary>
		/// <param name="left">Left buffer</param>
		/// <param name="leftOffset">Start offset in left buffer</param>
		/// <param name="right">Right buffer</param>
		/// <param name="rightOffset">Start offset in right buffer</param>
		/// <param name="count">Number of bytes to compare</param>
		/// <returns>true if all bytes are the same in both segments</returns>
		[Pure]
		public static bool SameBytes(byte[] left, int leftOffset, byte[] right, int rightOffset, int count)
		{
			EnsureBufferIsValid(left, leftOffset, count);
			EnsureBufferIsValid(right, rightOffset, count);

			if (left == null || right == null) return left == right;
			return SameBytesUnsafe(left, leftOffset, right, rightOffset, count);
		}

#if ENABLE_SPAN
		/// <summary>Compare two spans for equality</summary>
		/// <param name="left">Left buffer</param>
		/// <param name="right">Right buffer</param>
		/// <returns>true if all bytes are the same in both segments</returns>
		public static bool SameBytes(ReadOnlySpan<byte> left, ReadOnlySpan<byte> right)
		{
			if (left.Length != right.Length) return false;
			//REVIEW: is there a more direct wait to compare two spans ?? (did not find anything in ReadOnlySpan, MemoryExtensions nor MemoryMarshal ... ?)
			fixed (byte* pLeft = &MemoryMarshal.GetReference(left))
			fixed (byte* pRight = &MemoryMarshal.GetReference(right))
			{
				//TODO: version of comapre that is optimized for equality checks!
				return 0 == CompareUnsafe(pLeft, pRight, (uint) left.Length);
			}
		}
#endif

		/// <summary>Compare two byte segments for equality, without validating the arguments</summary>
		/// <param name="left">Left buffer</param>
		/// <param name="leftOffset">Start offset in left buffer</param>
		/// <param name="right">Right buffer</param>
		/// <param name="rightOffset">Start offset in right buffer</param>
		/// <param name="count">Number of bytes to compare</param>
		/// <returns>true if all bytes are the same in both segments</returns>
		[Pure]
		public static bool SameBytesUnsafe([NotNull] byte[] left, int leftOffset, [NotNull] byte[] right, int rightOffset, int count)
		{
			Contract.Requires(left != null && leftOffset >= 0 && right != null && rightOffset >= 0 && count >= 0);

			if (count == 0 || (object.ReferenceEquals(left, right) && leftOffset == rightOffset))
			{ // empty, or same segment of the same buffer
				return true;
			}

			fixed (byte* pLeft = &left[leftOffset])
			fixed (byte* pRight = &right[rightOffset])
			{
				//TODO: version of comapre that is optimized for equality checks!
				return 0 == CompareUnsafe(pLeft, pRight, checked((uint)count));
			}
		}

		/// <summary>Compare two byte buffers lexicographically</summary>
		/// <param name="left">Left buffer</param>
		/// <param name="right">Right buffer</param>
		/// <returns>Returns zero if both buffers are identical (same bytes), a negative value if left is lexicographically less than right, or a positive value if left is lexicographically greater than right</returns>
		/// <remarks>The comparison algorithm respect the following:
		/// * "A" &lt; "B"
		/// * "A" &lt; "AA"
		/// * "AA" &lt; "B"
		/// </remarks>
		[Pure]
		public static int Compare([NotNull] byte[] left, [NotNull] byte[] right)
		{
			Contract.NotNull(left, nameof(left));
			Contract.NotNull(right, nameof(right));
			return CompareUnsafe(left, 0, left.Length, right, 0, right.Length);
		}

		/// <summary>Compare two byte segments lexicographically</summary>
		/// <param name="left">Left buffer</param>
		/// <param name="leftOffset">Start offset in left buffer</param>
		/// <param name="leftCount">Number of bytes in left buffer</param>
		/// <param name="right">Right buffer</param>
		/// <param name="rightOffset">Start offset in right buffer</param>
		/// <param name="rightCount">Number of bytes in right buffer</param>
		/// <returns>Returns zero if segments are identical (same bytes), a negative value if left is lexicographically less than right, or a positive value if left is lexicographically greater than right</returns>
		/// <remarks>The comparison algorithm respect the following:
		/// * "A" &lt; "B"
		/// * "A" &lt; "AA"
		/// * "AA" &lt; "B"
		/// </remarks>
		[Pure]
		public static int Compare([NotNull] byte[] left, int leftOffset, int leftCount, [NotNull] byte[] right, int rightOffset, int rightCount)
		{
			EnsureBufferIsValidNotNull(left, leftOffset, leftCount);
			EnsureBufferIsValidNotNull(right, rightOffset, rightCount);

			return CompareUnsafe(left, leftOffset, leftCount, right, rightOffset, rightCount);
		}

		/// <summary>Compare two byte segments lexicographically, without validating the arguments</summary>
		/// <param name="left">Left buffer</param>
		/// <param name="leftOffset">Start offset in left buffer</param>
		/// <param name="leftCount">Number of bytes in left buffer</param>
		/// <param name="right">Right buffer</param>
		/// <param name="rightOffset">Start offset in right buffer</param>
		/// <param name="rightCount">Number of bytes in right buffer</param>
		/// <returns>Returns zero if segments are identical (same bytes), a negative value if left is lexicographically less than right, or a positive value if left is lexicographically greater than right</returns>
		/// <remarks>The comparison algorithm respect the following:
		/// * "A" &lt; "B"
		/// * "A" &lt; "AA"
		/// * "AA" &lt; "B"
		/// </remarks>
		[Pure]
		public static int CompareUnsafe([NotNull] byte[] left, int leftOffset, int leftCount, [NotNull] byte[] right, int rightOffset, int rightCount)
		{
			Contract.Requires(left != null && right != null && leftOffset >= 0 && leftCount >= 0 && rightOffset >= 0 && rightCount >= 0);

			if (object.ReferenceEquals(left, right) && leftCount == rightCount && leftOffset == rightOffset)
			{ // same segment in the same buffer
				return 0;
			}

			fixed (byte* pLeft = &left[leftOffset])
			fixed (byte* pRight = &right[rightOffset])
			{
				return CompareUnsafe(pLeft, (uint) leftCount, pRight, (uint) rightCount);
			}
		}

		/// <summary>Ensure that the specified temporary buffer is large enough</summary>
		/// <param name="buffer">Pointer to a temporary scratch buffer (previous data will not be maintained)</param>
		/// <param name="minCapacity">Minimum expected capacity</param>
		/// <returns>Same buffer if it was large enough, or a new allocated buffer with length greater than or equal to <see cref="minCapacity"/></returns>
		[Pure, NotNull, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static byte[] EnsureCapacity(ref byte[] buffer, int minCapacity)
		{
			if (buffer == null || buffer.Length < minCapacity)
			{
				buffer = AllocateAligned(minCapacity);
			}
			return buffer;
		}

		/// <summary>Ensure that the specified temporary buffer is large enough</summary>
		/// <param name="buffer">Pointer to a temporary scratch buffer (previous data will not be maintained)</param>
		/// <param name="minCapacity">Minimum expected capacity</param>
		/// <returns>Same buffer if it was large enough, or a new allocated buffer with length greater than or equal to <see cref="minCapacity"/></returns>
		[Pure, NotNull, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static byte[] EnsureCapacity(ref byte[] buffer, uint minCapacity)
		{
			if (minCapacity > int.MaxValue) throw FailBufferTooLarge(minCapacity);
			if (buffer == null || buffer.Length < (int) minCapacity)
			{
				buffer = AllocateAligned((int) minCapacity);
			}
			return buffer;
		}

		[Pure, NotNull, MethodImpl(MethodImplOptions.NoInlining)]
		private static byte[] AllocateAligned(int minCapacity)
		{
			if (minCapacity < 0) throw FailBufferTooLarge(minCapacity); //note: probably an integer overlofw (unsigned -> signed)
			return new byte[BitHelpers.AlignPowerOfTwo(minCapacity, 8)];
		}

		[Pure, NotNull, MethodImpl(MethodImplOptions.NoInlining)]
		private static Exception FailBufferTooLarge(long minCapacity)
		{
			return new ArgumentOutOfRangeException(nameof(minCapacity), minCapacity, "Cannot allocate buffer larger than 2GB.");
		}

		/// <summary>Copy the content of a byte segment into another. CAUTION: The arguments are NOT in the same order as Buffer.BlockCopy() or Array.Copy() !</summary>
		/// <param name="dst">Destination buffer</param>
		/// <param name="dstOffset">Offset in destination buffer</param>
		/// <param name="src">Source buffer</param>
		/// <param name="srcOffset">Offset in source buffer</param>
		/// <param name="count">Number of bytes to copy</param>
		/// <remarks>CAUTION: THE ARGUMENTS ARE REVERSED! They are in the same order as memcpy() and memmove(), with destination first, and source second!</remarks>
		[DebuggerStepThrough]
		public static void Copy(byte[] dst, int dstOffset, byte[] src, int srcOffset, int count)
		{
			if (count > 0)
			{
				EnsureBufferIsValidNotNull(dst, dstOffset, count);
				EnsureBufferIsValidNotNull(src, srcOffset, count);

				fixed (byte* pDst = &dst[dstOffset]) // throw if dst == null or dstOffset outside of the array
				fixed (byte* pSrc = &src[srcOffset]) // throw if src == null or srcOffset outside of the array
				{
					Buffer.MemoryCopy(pSrc, pDst, dst.Length - dstOffset, count);
				}
			}
		}

		/// <summary>Copy the content of a byte segment into another. CAUTION: The arguments are NOT in the same order as Buffer.BlockCopy() or Array.Copy() !</summary>
		/// <param name="dst">Destination buffer</param>
		/// <param name="dstOffset">Offset in destination buffer</param>
		/// <param name="src">Source buffer</param>
		/// <param name="srcOffset">Offset in source buffer</param>
		/// <param name="count">Number of bytes to copy</param>
		/// <remarks>CAUTION: THE ARGUMENTS ARE REVERSED! They are in the same order as memcpy() and memmove(), with destination first, and source second!</remarks>
		[DebuggerStepThrough]
		public static void Copy(byte[] dst, uint dstOffset, byte[] src, uint srcOffset, uint count)
		{
			if (count > 0)
			{
				EnsureBufferIsValidNotNull(dst, dstOffset, count);
				EnsureBufferIsValidNotNull(src, srcOffset, count);

				fixed (byte* pDst = &dst[dstOffset]) // throw if dst == null or dstOffset outside of the array
				fixed (byte* pSrc = &src[srcOffset]) // throw if src == null or srcOffset outside of the array
				{
					Buffer.MemoryCopy(pSrc, pDst, dst.Length - dstOffset, count);
				}
			}
		}

#if ENABLE_SPAN
		public static void Copy(Span<byte> destination, byte[] src, int srcOffset, int count)
		{
			if (count > 0)
			{
				new ReadOnlySpan<byte>(src, srcOffset, count).CopyTo(destination);
			}
		}

		public static void Copy(Span<byte> destination, Slice source)
		{
			if (source.Count > 0)
			{
				new ReadOnlySpan<byte>(source.Array, source.Offset, source.Count).CopyTo(destination);
			}
		}

		public static void Copy(byte[] dst, int dstOffset, ReadOnlySpan<byte> source)
		{
			if (source.Length > 0)
			{
				source.CopyTo(new Span<byte>(dst).Slice(dstOffset));
			}
		}

		public static void Copy(Slice destination, ReadOnlySpan<byte> source)
		{
			if (source.Length > 0)
			{
				source.CopyTo(new Span<byte>(destination.Array, destination.Offset, destination.Count));
			}
		}
#endif

		/// <summary>Copy the content of a byte segment into another, without validating the arguments. CAUTION: The arguments are NOT in the same order as Buffer.BlockCopy() or Array.Copy() !</summary>
		/// <param name="dst">Destination buffer</param>
		/// <param name="dstOffset">Offset in destination buffer</param>
		/// <param name="src">Source buffer</param>
		/// <param name="srcOffset">Offset in source buffer</param>
		/// <param name="count">Number of bytes to copy</param>
		/// <remarks>CAUTION: THE ARGUMENTS ARE REVERSED! They are in the same order as memcpy() and memmove(), with destination first, and source second!</remarks>
		[DebuggerStepThrough]
		public static void CopyUnsafe([NotNull] byte[] dst, int dstOffset, [NotNull] byte[] src, int srcOffset, int count)
		{
			//Contract.Requires(count >= 0);
			if (count > 0)
			{
				//Contract.Requires(dst != null && dstOffset >= 0 && src != null && srcOffset >= 0);

				fixed (byte* pDst = &dst[dstOffset])
				fixed (byte* pSrc = &src[srcOffset])
				{
					Buffer.MemoryCopy(pSrc, pDst, count, count);
				}
			}
		}

#if ENABLE_SPAN
		/// <summary>Copy the content of a native byte segment into a managed segment, without validating the arguments.</summary>
		/// <param name="dst">Destination buffer</param>
		/// <param name="dstOffset">Offset in destination buffer</param>
		/// <param name="src">Point to the source buffer</param>
		/// <param name="count">Number of bytes to copy</param>
		/// <remarks>CAUTION: THE ARGUMENTS ARE REVERSED! They are in the same order as memcpy() and memmove(), with destination first, and source second!</remarks>
		[DebuggerStepThrough]
		public static void CopyUnsafe([NotNull] byte[] dst, int dstOffset, ReadOnlySpan<byte> src)
		{
			//Contract.Requires(dst != null && dstOffset >= 0 && src.Length >= 0);

			fixed (byte* pDst = &dst[dstOffset])
			fixed (byte* pSrc = &MemoryMarshal.GetReference(src))
			{
				Buffer.MemoryCopy(pSrc, pDst, src.Length, src.Length);
			}
		}
#endif

		/// <summary>Copy the content of a native byte segment into a managed segment, without validating the arguments.</summary>
		/// <param name="dst">Destination buffer</param>
		/// <param name="dstOffset">Offset in destination buffer</param>
		/// <param name="src">Point to the source buffer</param>
		/// <param name="count">Number of bytes to copy</param>
		/// <remarks>CAUTION: THE ARGUMENTS ARE REVERSED! They are in the same order as memcpy() and memmove(), with destination first, and source second!</remarks>
		[DebuggerStepThrough]
		public static void CopyUnsafe([NotNull] byte[] dst, int dstOffset, byte* src, int count)
		{
			//Contract.Requires(dst != null && src != null && dstOffset >= 0 && count >= 0);

			fixed (byte* pDst = &dst[dstOffset])
			{
				Buffer.MemoryCopy(src, pDst, count, count);
			}
		}

		/// <summary>Copy the content of a native byte segment into a managed segment, without validating the arguments.</summary>
		/// <param name="dst">Destination buffer</param>
		/// <param name="dstOffset">Offset in destination buffer</param>
		/// <param name="src">Point to the source buffer</param>
		/// <param name="count">Number of bytes to copy</param>
		/// <remarks>CAUTION: THE ARGUMENTS ARE REVERSED! They are in the same order as memcpy() and memmove(), with destination first, and source second!</remarks>
		[DebuggerStepThrough]
		public static void CopyUnsafe([NotNull] byte[] dst, int dstOffset, byte* src, uint count)
		{
			//Contact.Requires(dst != null && src != null && dstOffset >= 0);

			fixed (byte* pDst = &dst[dstOffset])
			{
				Buffer.MemoryCopy(src, pDst, count, count);
			}
		}

		/// <summary>Copy a managed slice to the specified memory location</summary>
		/// <param name="dest">Where to copy the bytes</param>
		/// <param name="src">Reference to the first byte to copy</param>
		/// <param name="count">Number of bytes to copy</param>
		[SecurityCritical, ReliabilityContract(Consistency.WillNotCorruptState, Cer.Success)]
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void CopyUnsafe(byte* dest, ref byte src, int count)
		{
			if (count > 0)
			{
				Contract.Requires(dest != null);
				fixed (byte* ptr = &src)
				{
					Buffer.MemoryCopy(ptr, dest, count, count);
				}
			}
		}
		
		/// <summary>Copy a managed slice to the specified memory location</summary>
		/// <param name="dest">Where to copy the bytes</param>
		/// <param name="src">Slice of managed memory that will be copied to the destination</param>
		[SecurityCritical, ReliabilityContract(Consistency.WillNotCorruptState, Cer.Success)]
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void CopyUnsafe(byte* dest, Slice src)
		{
			int count = src.Count;
			if (count > 0)
			{
				Contract.Requires(dest != null && src.Array != null && src.Offset >= 0 && src.Count >= 0);
				fixed (byte* ptr = &src.DangerousGetPinnableReference())
				{
					Buffer.MemoryCopy(ptr, dest, count, count);
				}
			}
		}

		[SecurityCritical, ReliabilityContract(Consistency.WillNotCorruptState, Cer.Success)]
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void CopyUnsafe(Slice dest, byte* src, uint count)
		{
			if (count > 0)
			{
				Contract.Requires(dest.Array != null && dest.Offset >= 0 && dest.Count >= 0 && src != null);
				fixed (byte* ptr = &dest.DangerousGetPinnableReference())
				{
					Buffer.MemoryCopy(src, ptr, dest.Count, count);
				}
			}
		}

		/// <summary>Dangerously copy native memory from one location to another</summary>
		/// <param name="dest">Where to copy the bytes</param>
		/// <param name="src">Where to read the bytes</param>
		/// <param name="count">Number of bytes to copy</param>
		[SecurityCritical, ReliabilityContract(Consistency.WillNotCorruptState, Cer.Success)]
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void CopyUnsafe([NotNull] byte* dest, [NotNull] byte* src, uint count)
		{
			Contract.Requires(dest != null && src != null);
			Buffer.MemoryCopy(src, dest, count, count);
		}

		/// <summary>Compare two buffers in memory, using the lexicographical order, without checking the arguments</summary>
		/// <param name="left">Pointer to the first buffer</param>
		/// <param name="leftCount">Size (in bytes) of the first buffer</param>
		/// <param name="right">Pointer to the second buffer</param>
		/// <param name="rightCount">Size (in bytes) of the second buffer</param>
		/// <returns>The returned value will be &lt; 0 if <paramref name="left"/> is "before" <paramref name="right"/>, 0 if <paramref name="left"/> is the same as <paramref name="right"/>, and &lt; 0 if <paramref name="left"/> is "after" right.</returns>
		[SecurityCritical, ReliabilityContract(Consistency.WillNotCorruptState, Cer.Success)]
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static int CompareUnsafe(byte* left, uint leftCount, byte* right, uint rightCount)
		{
			Contract.Requires((left != null || leftCount == 0) && (right != null || rightCount == 0));

			int c = CompareUnsafe(left, right, Math.Min(leftCount, rightCount));
			return c != 0 ? c : (int) (leftCount - rightCount);
		}

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static int CompareUnsafe(byte* left, byte* right, uint count)
		{
			// the most frequent case is to compare keys that are natural or GUIDs,
			// in which case there is a very high probability that the first byte is different already
			// => we check for that case immediately
			if (count != 0 && *left != *right) return *left - *right;
			//REVIEW: we could special case count==4 or count==8 because they are probably frequent (FreeSpace map uses 4, indexes may use 8, ...)
			return CompareUnsafeInternal(left, right, count);
		}

		/// <summary>Compare two buffers in memory, using the lexicographical order, without checking the arguments</summary>
		/// <param name="left">Pointer to the first buffer</param>
		/// <param name="right">Pointer to the second buffer</param>
		/// <param name="count">Size (in bytes) of both buffers</param>
		/// <returns>The returned value will be &lt; 0 if <paramref name="left"/> is "before" <paramref name="right"/>, 0 if <paramref name="left"/> is the same as <paramref name="right"/>, and &lt; 0 if <paramref name="left"/> is "after" right.</returns>
		[Pure, MethodImpl(MethodImplOptions.NoInlining)]
		private static int CompareUnsafeInternal(byte* left, byte* right, uint count)
		{
			Contract.Requires(count == 0 || (left != null && right != null));

			// We would like to always use memcmp (fastest), but the overhead of PInvoke makes it slower for small keys (<= 256)
			// For these, we will use a custom implementation which is a bit slower than memcmp but faster than the overhead of PInvoke.

			if (count == 0) return 0;

			// the minimum size to amortize the cost of P/Invoke seems to be over 256 bytes, On My Machine(tm)
			if (count > 256)
			{
				return _memcmp(left, right, count);
			}

			// we will scan the strings by XORing together segments of 8 bytes (then 4, then 2, ...) looking for the first segment that contains at least one difference (ie: at least one bit set after XORing)
			// then, if we find a difference, we will "fine tune" the pointers to locate the first byte that is different
			// then, we will return the difference between the bytes at this location

			// Sample scenario:
			//            __ cursor   ___ first difference is at byte (cursor + 4)
			//           v           v
			// LEFT : .. AA AA AA AA AA AA AA AA ..
			// RIGHT: .. AA AA AA AA BB AA AA AA ..
			// XOR  :  ( 00 00 00 00 11 00 00 00 )
			//
			// The result of the XOR is 0x11000000 and is not equal to 0, so the first difference is within these 8 bytes
			// The first 4 bytes of the result are 0, which means that the difference is at offset 4 (ie: we needed to SHR 8 the result 4 times before having at least one bit set in 0..7
			//
			// L XOR R:  00 00 00 00 11 00 00 00
			// offset :  +0 +1 +2 +3 +4 +5 +6 +7
			//                       ^^__ first non-zero byte

			// number of 16-bytes segments to scan
			long x;
			if (count >= 16)
			{
				long y;
				byte* end = left + (count & ~0xF);
				while (left < end)
				{
					// parallelize the reads
					x = *(long*) left ^ *(long*) right;
					y = *(long*) (left + 8) ^ *(long*) (right + 8);
					if (x != 0)
					{
						goto fine_tune_8;
					}
					if (y != 0)
					{
						x = y;
						goto fine_tune_8_with_offset;
					}
					left += 16;
					right += 16;
				}

				if ((count & 0xF) == 0)
				{ // size is multiple of 16 with no differences => equal
					return 0; // fast path for Guid keys
				}
			}

			// use the last 4 bits in the count to parse the tail

			if ((count & 8) != 0)
			{ // at least 8 bytes remaining
				x = *(long*) left ^ *(long*) right;
				if (x != 0) goto fine_tune_8;
				if ((count & 7) == 0) return 0; // fast path for long keys
				left += 8;
				right += 8;
			}
			if ((count & 4) != 0)
			{ // at least 4 bytes remaining
				x = *(int*) left ^ *(int*) right;
				if (x != 0) goto fine_tune_4;
				if ((count & 3) == 0) return 0; // fast path for int keys
				left += 4;
				right += 4;
			}
			if ((count & 2) != 0)
			{ // at least 2 bytes remaining
				x = *(short*) left ^ *(short*) right;
				if (x != 0) goto fine_tune_2;
				left += 2;
				right += 2;
			}
			if ((count & 1) != 0)
			{ // at least one byte remaining
				return left[0] - right[0];
			}
			// both strings are equal
			return 0;

		fine_tune_8_with_offset:
			// adjust the pointers (we were looking at the upper 8 bytes in a 16-bytes segment
			left += 8;
			right += 8;

		fine_tune_8:
			// the difference is somewhere in the last 8 bytes
			if ((uint)x == 0)
			{ // it is not in the first 4 bytes
				x >>= 32;
				left += 4;
				right += 4;
			}
		fine_tune_4:
			// the difference is somewhere in the last 4 bytes
			if ((ushort) x == 0)
			{ // if is not in the first 2 bytes
				// the difference is either at +2 or +3
				return (x & 0xFF0000) == 0
					? left[3] - right[3]
					: left[2] - right[2];
			}

		fine_tune_2:
			// the difference is somewhere in the last 2 bytes
			return (x & 0xFF) == 0
				? left[1] - right[1]
				: left[0] - right[0];
		}

		[Pure, MethodImpl(MethodImplOptions.NoInlining)]
		private static int _memcmp([NotNull] byte* left, byte* right, uint count)
		{
			return NativeMethods.memcmp(left, right, (UIntPtr) count);
		}

		/// <summary>Fill the content of a managed segment with zeroes</summary>
		public static void Clear([NotNull] byte[] bytes, int offset, int count)
		{
			if (count > 0)
			{
				EnsureBufferIsValidNotNull(bytes, offset, count);
				fixed (byte* ptr = &bytes[offset])
				{
					ClearUnsafe(ptr, (uint) count);
				}
			}
		}

		/// <summary>Fill the content of a managed segment with zeroes</summary>
		public static void Clear([NotNull] byte[] bytes, uint offset, uint count)
		{
			if (count > 0)
			{
				EnsureBufferIsValidNotNull(bytes, offset, count);
				fixed (byte* ptr = &bytes[offset])
				{
					ClearUnsafe(ptr, count);
				}
			}
		}

		/// <summary>Fill the content of a managed slice with zeroes</summary>
		public static void Clear(Slice buffer)
		{
			Clear(buffer.Array, buffer.Offset, buffer.Count);
		}

		/// <summary>Fill the content of an unmanaged buffer with zeroes, without checking the arguments</summary>
		/// <remarks>WARNING: invalid use of this method WILL corrupt the heap!</remarks>
		[SecurityCritical, ReliabilityContract(Consistency.WillNotCorruptState, Cer.Success)]
		public static void ClearUnsafe([NotNull] byte* ptr, uint length)
		{
			Contract.Requires(ptr != null);
			switch (length)
			{
				case 0:
					return;
				case 1:
					*ptr = 0;
					return;
				case 2:
					*(short*) ptr = 0;
					return;
				case 3:
					*(short*) ptr = 0;
					*(ptr + 2) = 0;
					return;
				case 4:
					*(int*) ptr = 0;
					return;
				case 5:
					((int*) ptr)[0] = 0;
					*(ptr + 4) = 0;
					return;
				case 6:
					*(int*) ptr = 0;
					*(short*) (ptr + 4) = 0;
					return;
				case 7:
					*(int*)ptr = 0;
					*(short*)(ptr + 4) = 0;
					*(ptr + 6) = 0;
					return;
				case 8:
					*(long*)ptr = 0;
					return;
			}

			if (length >= 512)
			{ // PInvoke into the native memset
				_memset(ptr, 0, length);
				return;
			}

			while (length >= 16)
			{
				((long*) ptr)[0] = 0;
				((long*) ptr)[1] = 0;
				ptr += 16;
				length -= 16;
			}
			if ((length & 8) != 0)
			{
				((long*)ptr)[0] = 0;
				ptr += 8;
			}
			if ((length & 4) != 0)
			{
				((uint*) ptr)[0] = 0;
				ptr += 4;
			}
			if ((length & 2) != 0)
			{
				((short*)ptr)[0] = 0;
				ptr += 2;
			}
			if ((length & 1) != 0)
			{
				*ptr = 0;
			}
		}

		/// <summary>Fill the content of an unmanaged buffer with zeroes, without checking the arguments</summary>
		/// <remarks>WARNING: invalid use of this method WILL corrupt the heap!</remarks>
		public static void ClearUnsafe([NotNull] byte* ptr, ulong length)
		{
			//pre-check incase of uint overflow
			if (length >= 512)
			{
				Contract.Requires(ptr != null);
				_memset(ptr, 0, length);
			}
			else
			{
				ClearUnsafe(ptr, (uint) length);
			}
		}

		/// <summary>Fill the content of an unamanged array with zeroes, without checking the arguments</summary>
		/// <param name="ptr">Pointer to the start of the array</param>
		/// <param name="count">Number of items to clear</param>
		/// <param name="sizeOfItem">Size (in bytes) of one item</param>
		/// <remarks>Will clear <paramref name="count"/> * <paramref name="sizeOfItem"/> elements in the array</remarks>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void ClearUnsafe([NotNull] void* ptr, [Positive] int count, uint sizeOfItem)
		{
			ClearUnsafe((byte*) ptr, checked((uint) count * sizeOfItem));
		}

		/// <summary>Fill the content of a managed segment with the same byte repeated</summary>
		public static void Fill([NotNull] byte[] bytes, int offset, int count, byte filler)
		{
			if (count > 0)
			{
				EnsureBufferIsValidNotNull(bytes, offset, count);
				fixed (byte* ptr = &bytes[offset])
				{
					if (filler == 0)
					{
						ClearUnsafe(ptr, (uint)count);
					}
					else
					{
						_memset(ptr, filler, (uint)count);
					}
				}
			}
		}

		[SecurityCritical, ReliabilityContract(Consistency.WillNotCorruptState, Cer.Success)]
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void FillUnsafe([NotNull] byte* ptr, uint count, byte filler)
		{
			if (count != 0)
			{
				Contract.Requires(ptr != null);
				_memset(ptr, filler, count);
			}
		}

		public static void FillUnsafe([NotNull] byte* ptr, ulong count, byte filler)
		{
			if (count != 0)
			{
				Contract.Requires(ptr != null);
				_memset(ptr, filler, count);
			}
		}

		[SecurityCritical]
		[ReliabilityContract(Consistency.WillNotCorruptState, Cer.Success)]
		[MethodImpl(MethodImplOptions.NoInlining)]
		private static void _memset([NotNull] byte* ptr, byte filler, uint count)
		{
			NativeMethods.memset(ptr, filler, (UIntPtr) count);
		}

		[SecurityCritical]
		[ReliabilityContract(Consistency.WillNotCorruptState, Cer.Success)]
		[MethodImpl(MethodImplOptions.NoInlining)]
		private static void _memset([NotNull] byte* ptr, byte filler, ulong count)
		{
			NativeMethods.memset(ptr, filler, (UIntPtr) count);
		}

		/// <summary>Add padding bytes to the end of buffer if it is not aligned to a specific value, and advance the cursor</summary>
		/// <param name="buffer">Start of a buffer that may need padding</param>
		/// <param name="size">Size of the buffer</param>
		/// <param name="alignment">Required alignement of the buffer size, which MUST be a power of two. If the buffer is not aligned, additional 0 bytes are added at the end.</param>
		/// <returns>Address of the next byte after the buffer, with padding included</returns>
		[NotNull]
		public static byte* PadBuffer([NotNull] byte* buffer, uint size, uint alignment)
		{
			Contract.PointerNotNull(buffer, nameof(buffer));
			Contract.PowerOfTwo(alignment, nameof(alignment));
			uint pad = size % (alignment - 1);
			byte* ptr = buffer + size;
			if (pad != 0)
			{
				ClearUnsafe(ptr, pad);
				ptr += alignment - pad;
			}
			return ptr;
		}

		/// <summary>Compute the hash code of a byte segment</summary>
		/// <param name="bytes">Buffer</param>
		/// <param name="offset">Offset of the start of the segment in the buffer</param>
		/// <param name="count">Number of bytes in the segment</param>
		/// <returns>A 32-bit signed hash code calculated from all the bytes in the segment.</returns>
		/// <remarks>This should only be used for dictionaries or hashset that reside in memory only! The hashcode could change at any time in future versions.</remarks>
		public static int ComputeHashCode(byte[] bytes, int offset, int count)
		{
			if (count == 0) return unchecked((int) 2166136261);
			EnsureBufferIsValidNotNull(bytes, offset, count);
			fixed (byte* ptr = &bytes[offset])
			{
				return ComputeHashCodeUnsafe(ptr, (uint) count);
			}
		}

		/// <summary>Compute the hash code of a byte buffer</summary>
		/// <remarks>This should only be used for dictionaries or hashset that reside in memory only! The hashcode could change at any time in future versions.</remarks>
		public static int ComputeHashCode(byte* bytes, uint count)
		{
			if (count == 0) return unchecked((int) 2166136261);
			EnsureBufferIsValidNotNull(bytes, count);
			return ComputeHashCodeUnsafe(bytes, count);
		}

		/// <summary>Compute the hash code of a byte buffer</summary>
		/// <param name="bytes">Array that contains the byte buffer (ignored if count == 0)</param>
		/// <param name="offset">Offset of the first byte in the buffer (ignored if count == 0)</param>
		/// <param name="count">Number of bytes in the buffer</param>
		/// <returns>A 32-bit signed hash code calculated from all the bytes in the segment.</returns>
		/// <remarks>
		/// If count == 0, then the value of <paramref name="bytes"/> is ignored.
		/// This should only be used for dictionaries or hashset that reside in memory only! The hashcode could change at any time in future versions.
		/// </remarks>
		internal static int ComputeHashCodeUnsafe([NotNull] byte[] bytes, int offset, int count)
		{
			if (count == 0) return unchecked((int) 2166136261);
			fixed (byte* ptr = &bytes[offset])
			{
				return ComputeHashCodeUnsafe(ptr, (uint) count);
			}
		}

		/// <summary>Compute the hash code of a byte buffer</summary>
		/// <param name="bytes">Pointer to the first byte of the buffer (ignored if count == 0)</param>
		/// <param name="count">Number of bytes in the buffer</param>
		/// <returns>A 32-bit signed hash code calculated from all the bytes in the segment.</returns>
		/// <remarks>This should only be used for dictionaries or hashset that reside in memory only! The hashcode could change at any time in future versions.</remarks>
		internal static int ComputeHashCodeUnsafe([NotNull] byte* bytes, uint count)
		{
			//note: callers should have handled the case where bytes == null, but they can call us with count == 0
			Contract.Requires(bytes != null);

			//TODO: use a better hash algorithm? (xxHash, CityHash, SipHash, ...?)
			// => will be called a lot when Slices are used as keys in an hash-based dictionary (like Dictionary<Slice, ...>)
			// => won't matter much for *ordered* dictionary that will probably use IComparer<T>.Compare(..) instead of the IEqalityComparer<T>.GetHashCode()/Equals() combo
			// => we don't need a cryptographic hash, just something fast and suitable for use with hashtables...
			// => probably best to select an algorithm that works on 32-bit or 64-bit chunks

			// <HACKHACK>: unoptimized 32 bits FNV-1a implementation
			uint h = 2166136261; // FNV1 32 bits offset basis
			uint n = count;
			while (n > 0)
			{
				h = unchecked ((h ^ *bytes++) * 16777619); // FNV1 32 prime
				--n;
			}
			return unchecked((int) h);
			// </HACKHACK>
		}

		[NotNull, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static byte* WriteBytesUnsafe([NotNull] byte* cursor, [NotNull] byte* data, uint count)
		{
			Contract.Requires(cursor != null && data != null);
			if (count > 0) System.Buffer.MemoryCopy(data, cursor, count, count);
			return cursor + count;
		}

		[NotNull, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static byte* WriteBytes([NotNull] byte* cursor, [NotNull] byte* stop, [NotNull] byte* data, uint count)
		{
			Contract.Requires(cursor != null && stop != null && data != null);
			if (count > 0)
			{
				if (cursor + count > stop) throw Errors.BufferOutOfBound();
				System.Buffer.MemoryCopy(data, cursor, count, count);
			}
			return cursor + count;
		}

		#region VarInt Encoding...

		// VarInt encoding uses 7-bit per byte for the value, and uses the 8th bit as a "continue" (1) or "stop" (0) bit.
		// The values is stored in Little Endian, ie: first the 7 lowest bits, then the next 7 lowest bits, until the 7 highest bits.
		//
		// ex: 0xxxxxxx = 1 byte (<= 127)
		//     1xxxxxxx 0xxxxxxx = 2 bytes (<= 16383)
		//     1xxxxxxx 1xxxxxxx 0xxxxxxx = 3 bytes (<= 2097151)
		//
		// The number of bytes required to store uint.MaxValue is 5 bytes, and for ulong.MaxValue is 9 bytes.

		/// <summary>Return the size (in bytes) that a 32-bit number would need when encoded as a VarInt</summary>
		/// <param name="value">Number that needs to be encoded</param>
		/// <returns>Number of bytes needed (1-5)</returns>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static uint SizeOfVarInt(uint value)
		{
			return value < (1U << 7) ? 1 : SizeOfVarIntSlow(value);
		}

		private static uint SizeOfVarIntSlow(uint value)
		{
			// count is already known to be >= 128
			if (value < (1U << 14)) return 2;
			if (value < (1U << 21)) return 3;
			if (value < (1U << 28)) return 4;
			return 5;
		}

		/// <summary>Return the size (in bytes) that a 64-bit number would need when encoded as a VarInt</summary>
		/// <param name="value">Number that needs to be encoded</param>
		/// <returns>Number of bytes needed (1-10)</returns>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static uint SizeOfVarInt(ulong value)
		{
			return value < (1UL << 7) ? 1 : SizeOfVarIntSlow(value);
		}

		private static uint SizeOfVarIntSlow(ulong value)
		{
			// value is already known to be >= 128
			if (value < (1UL << 14)) return 2;
			if (value < (1UL << 21)) return 3;
			if (value < (1UL << 28)) return 4;
			if (value < (1UL << 35)) return 5;
			if (value < (1UL << 42)) return 6;
			if (value < (1UL << 49)) return 7;
			if (value < (1UL << 56)) return 8;
			if (value < (1UL << 63)) return 9;
			return 10;
		}

		/// <summary>Return the size (in bytes) that a variable-size array of bytes would need when encoded as a VarBytes</summary>
		/// <param name="size">Size (in bytes) of the array</param>
		/// <returns>Number of bytes needed to encoded the size of the array, and the array itself (1 + N &lt;= size &lt;= 5 + N)</returns>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static uint SizeOfVarBytes(uint size)
		{
			return checked(size + SizeOfVarInt(size));
		}
		/// <summary>Return the size (in bytes) that a variable-size array of bytes would need when encoded as a VarBytes</summary>
		/// <param name="size">Size (in bytes) of the array</param>
		/// <returns>Number of bytes needed to encoded the size of the array, and the array itself (1 + N &lt;= size &lt;= 5 + N)</returns>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static int SizeOfVarBytes(int size)
		{
			return checked(size + (int) SizeOfVarInt((uint) size));
		}

		/// <summary>Append a variable sized number to the output buffer</summary>
		/// <param name="cursor">Pointer to the next free byte in the buffer</param>
		/// <param name="value">Value of the number to output</param>
		/// <returns>Pointer updated with the number of bytes written</returns>
		/// <remarks>Will write between 1 and 3 bytes</remarks>
		[NotNull, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static byte* WriteVarInt16Unsafe([NotNull] byte* cursor, uint value)
		{
			Contract.Requires(cursor != null);
			//note: use of '&' is intentional (prevent a branch in the generated code)
			if (value < 0x80)
			{
				*cursor = (byte) value;
				return cursor + 1;
			}
			return WriteVarInt32UnsafeSlow(cursor, value);
		}

		/// <summary>Append a variable sized number to the output buffer</summary>
		/// <param name="cursor">Pointer to the next free byte in the buffer</param>
		/// <param name="stop"></param>
		/// <param name="value">Value of the number to output</param>
		/// <returns>Pointer updated with the number of bytes written</returns>
		/// <remarks>Will write between 1 and 3 bytes</remarks>
		[NotNull, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static byte* WriteVarInt16([NotNull] byte* cursor, [NotNull] byte* stop, ushort value)
		{
			Contract.Requires(cursor != null && stop != null);
			//note: use of '&' is intentional (prevent a branch in the generated code)
			if (cursor < stop & value < 0x80)
			{
				*cursor = (byte) value;
				return cursor + 1;
			}
			return WriteVarInt32Slow(cursor, stop, value);
		}

		/// <summary>Reads a 7-bit encoded unsigned int (aka 'Varint16') from the buffer, and advances the cursor</summary>
		/// <remarks>Can read up to 3 bytes from the input</remarks>
		[NotNull]
		public static byte* ReadVarint16([NotNull] byte* cursor, [NotNull] byte* stop, out ushort value)
		{
			Contract.Requires(cursor != null && stop != null);
			if (cursor < stop && (value = *cursor) < 0x80)
			{
				return cursor + 1;
			}
			return ReadVarint16Slow(cursor, stop, out value);
		}

		/// <summary>Reads a 7-bit encoded unsigned int (aka 'Varint32') from the buffer, and advances the cursor</summary>
		/// <remarks>Can read up to 5 bytes from the input</remarks>
		[NotNull]
		private static byte* ReadVarint16Slow([NotNull] byte* cursor, [NotNull] byte* stop, out ushort value)
		{
			uint n;

			// unless  cursor >= stop, we already know that the first byte has the MSB set
			if (cursor >= stop) goto overflow;
			uint b = cursor[0];
			Contract.Assert(b >= 0x80);
			uint res = b & 0x7F;

			if (cursor + 1 >= stop) goto overflow;
			b = cursor[1];
			res |= (b & 0x7F) << 7;
			if (b < 0x80)
			{
				n = 2;
				goto done;
			}

			if (cursor + 2 >= stop) goto overflow;
			b = cursor[2];
			// third should only have 2 bits worth of data
			if (b >= 0x04) throw Errors.VarIntOverflow();
			res |= (b & 0x3) << 14;
			n = 3;
			//TODO: check overflow bits?

		done:
			value = (ushort) res;
			return cursor + n;

		overflow:
			value = 0;
			throw Errors.VarIntTruncated();
		}

		/// <summary>Reads a 7-bit encoded unsigned int (aka 'Varint16') from the buffer, and advances the cursor</summary>
		/// <remarks>Can read up to 3 bytes from the input</remarks>
		[NotNull]
		public static byte* ReadVarint16Unsafe([NotNull] byte* cursor, out ushort value)
		{
			Contract.Requires(cursor != null);
			uint n = 1;

			//TODO: we expect most values to be small (count or array length), so we should optimize for single byte varints where byte[0] <= 127 should be inlined, and defer to a slower method if >= 128.

			uint b = cursor[0];
			uint res = b & 0x7F;
			if (b < 0x80)
			{
				goto done;
			}

			b = cursor[1];
			res |= (b & 0x7F) << 7;
			if (b < 0x80)
			{
				n = 2;
				goto done;
			}

			b = cursor[2];
			// third should only have 2 bits worth of data
			if (b >= 0x04) throw Errors.VarIntOverflow();
			res |= (b & 0x3) << 14;
			n = 3;

		done:
			value = (ushort) res;
			return cursor + n;
		}

		/// <summary>Append a variable sized number to the output buffer</summary>
		/// <param name="cursor">Pointer to the next free byte in the buffer</param>
		/// <param name="value">Value of the number to output</param>
		/// <returns>Pointer updated with the number of bytes written</returns>
		/// <remarks>Will write between 1 and 5 bytes</remarks>
		[NotNull, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static byte* WriteVarInt32Unsafe([NotNull] byte* cursor, uint value)
		{
			Contract.Requires(cursor != null);
			if (value < 0x80)
			{
				*cursor = (byte) value;
				return cursor + 1;
			}
			return WriteVarInt32UnsafeSlow(cursor, value);
		}

		/// <summary>Append a variable sized number to the output buffer</summary>
		/// <param name="cursor">Pointer to the next free byte in the buffer</param>
		/// <param name="value">Value of the number to output</param>
		/// <returns>Pointer updated with the number of bytes written</returns>
		/// <remarks>Will write between 1 and 5 bytes</remarks>
		[NotNull, MethodImpl(MethodImplOptions.NoInlining)]
		private static byte* WriteVarInt32UnsafeSlow([NotNull] byte* cursor, uint value)
		{
			byte* ptr = cursor;
			while (value >= 0x80)
			{
				*ptr = (byte)(value | 0x80);
				value >>= 7;
				++ptr;
			}
			*ptr = (byte)value;
			return ptr + 1;
		}

		/// <summary>Append a variable sized number to the output buffer</summary>
		/// <param name="cursor">Pointer to the next free byte in the buffer</param>
		/// <param name="stop"></param>
		/// <param name="value">Value of the number to output</param>
		/// <returns>Pointer updated with the number of bytes written</returns>
		/// <remarks>Will write between 1 and 5 bytes</remarks>
		[NotNull, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static byte* WriteVarInt32([NotNull] byte* cursor, [NotNull] byte* stop, uint value)
		{
			Contract.Requires(cursor != null && stop != null);
			//note: use of '&' is intentional (prevent a branch in the generated code)
			if (cursor < stop & value < 0x80)
			{
				*cursor = (byte)value;
				return cursor + 1;
			}
			return WriteVarInt32Slow(cursor, stop, value);
		}

		[NotNull, MethodImpl(MethodImplOptions.NoInlining)]
		private static byte* WriteVarInt32Slow([NotNull] byte* cursor, [NotNull] byte* stop, uint value)
		{
			//note: we know that value >= 128 (or that cursor is >= stop, in which case we will immediately fail below)
			byte* ptr = cursor;
			do
			{
				if (ptr >= stop) throw Errors.BufferOutOfBound();
				*ptr = (byte) (value | 0x80);
				value >>= 7;
				++ptr;
			} while (value >= 0x80);

			if (ptr >= stop) throw Errors.BufferOutOfBound();
			*ptr = (byte) value;
			return ptr + 1;
		}

		/// <summary>Reads a 7-bit encoded unsigned int (aka 'Varint32') from the buffer, and advances the cursor</summary>
		/// <remarks>Can read up to 5 bytes from the input</remarks>
		[NotNull]
		public static byte* ReadVarint32Unsafe([NotNull] byte* cursor, out uint value)
		{
			Contract.Requires(cursor != null);
			uint n = 1;

			//TODO: we expect most values to be small (count or array length), so we should optimize for single byte varints where byte[0] <= 127 should be inlined, and defer to a slower method if >= 128.

			uint b = cursor[0];
			uint res = b & 0x7F;
			if (b < 0x80)
			{
				goto done;
			}

			b = cursor[1];
			res |= (b & 0x7F) << 7;
			if (b < 0x80)
			{
				n = 2;
				goto done;
			}

			b = cursor[2];
			res |= (b & 0x7F) << 14;
			if (b < 0x80)
			{
				n = 3;
				goto done;
			}

			b = cursor[3];
			res |= (b & 0x7F) << 21;
			if (b < 0x80)
			{
				n = 4;
				goto done;
			}

			// the fifth byte should only have 4 bits worth of data
			b = cursor[4];
			if (b >= 0x20) throw Errors.VarIntOverflow();
			res |= (b & 0x1F) << 28;
			n = 5;

		done:
			value = res;
			return cursor + n;
		}

		[NotNull, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static byte* ReadVarint32([NotNull] byte* cursor, [NotNull] byte* stop, out uint value)
		{
			Contract.Requires(cursor != null && stop != null);
			if (cursor < stop && (value = *cursor) < 0x80)
			{
				return cursor + 1;
			}
			return ReadVarint32Slow(cursor, stop, out value);
		}

		/// <summary>Reads a 7-bit encoded unsigned int (aka 'Varint32') from the buffer, and advances the cursor</summary>
		/// <remarks>Can read up to 5 bytes from the input</remarks>
		[NotNull]
		private static byte* ReadVarint32Slow([NotNull] byte* cursor, [NotNull] byte* stop, out uint value)
		{
			uint n;

			// unless  cursor >= stop, we already know that the first byte has the MSB set
			if (cursor >= stop) goto overflow;
			uint b = cursor[0];
			Contract.Assert(b >= 0x80);
			uint res = b & 0x7F;

			if (cursor + 1 >= stop) goto overflow;
			b = cursor[1];
			res |= (b & 0x7F) << 7;
			if (b < 0x80)
			{
				n = 2;
				goto done;
			}

			if (cursor + 2 >= stop) goto overflow;
			b = cursor[2];
			res |= (b & 0x7F) << 14;
			if (b < 0x80)
			{
				n = 3;
				goto done;
			}

			if (cursor + 3 >= stop) goto overflow;
			b = cursor[3];
			res |= (b & 0x7F) << 21;
			if (b < 0x80)
			{
				n = 4;
				goto done;
			}

			// the fifth byte should only have 4 bits worth of data
			if (cursor + 4 >= stop) goto overflow;
			b = cursor[4];
			if (b >= 0x20) throw Errors.VarIntOverflow();
			res |= (b & 0x1F) << 28;
			n = 5;

		done:
			value = res;
			return cursor + n;

		overflow:
			value = 0;
			throw Errors.VarIntTruncated();
		}

		/// <summary>Append a variable sized number to the output buffer</summary>
		/// <param name="cursor">Pointer to the next free byte in the buffer</param>
		/// <param name="value">Value of the number to output</param>
		/// <returns>Pointer updated with the number of bytes written</returns>
		/// <remarks>Will write between 1 and 10 bytes</remarks>
		[NotNull, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static byte* WriteVarInt64Unsafe([NotNull] byte* cursor, ulong value)
		{
			Contract.Requires(cursor != null);
			if (value < 0x80)
			{
				*cursor = (byte)value;
				return cursor + 1;
			}
			return WriteVarInt64UnsafeSlow(cursor, value);
		}

		[NotNull, MethodImpl(MethodImplOptions.NoInlining)]
		private static byte* WriteVarInt64UnsafeSlow([NotNull] byte* cursor, ulong value)
		{
			//note: we know that value >= 128
			byte* ptr = cursor;
			do
			{
				*ptr = (byte) (value | 0x80);
				value >>= 7;
				++ptr;
			} while (value >= 0x80);
			*ptr = (byte)value;
			return ptr + 1;
		}

		/// <summary>Append a variable sized number to the output buffer</summary>
		/// <param name="cursor">Pointer to the next free byte in the buffer</param>
		/// <param name="stop">Stop address (to prevent overflow)</param>
		/// <param name="value">Value of the number to output</param>
		/// <returns>Pointer updated with the number of bytes written</returns>
		/// <remarks>Will write between 1 and 10 bytes</remarks>
		[NotNull, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static byte* WriteVarInt64([NotNull] byte* cursor, byte* stop, ulong value)
		{
			Contract.Requires(cursor != null && stop != null);
			//note: use of '&' is intentional (prevent a branch in the generated code)
			if (cursor < stop & value < 0x80)
			{
				*cursor = (byte) value;
				return cursor + 1;
			}
			return WriteVarInt64Slow(cursor, stop, value);
		}

		[NotNull, MethodImpl(MethodImplOptions.NoInlining)]
		private static byte* WriteVarInt64Slow([NotNull] byte* cursor, byte* stop, ulong value)
		{
			//note: we know that value >= 128 (or that cursor is >= stop, in which case we will immediately fail below)
			byte* ptr = cursor;
			do
			{
				if (ptr >= stop) throw Errors.BufferOutOfBound();
				*ptr = (byte) (value | 0x80);
				value >>= 7;
				++ptr;
			} while (value >= 0x80);

			if (ptr >= stop) throw Errors.BufferOutOfBound();
			*ptr = (byte)value;
			return ptr + 1;
		}

		/// <summary>Reads a 7-bit encoded unsigned long (aka 'Varint32') from the buffer, and advances the cursor</summary>
		/// <remarks>Can read up to 10 bytes from the input</remarks>
		[NotNull]
		public static byte* ReadVarint64Unsafe([NotNull] byte* cursor, out ulong value)
		{
			Contract.Requires(cursor != null);
			uint n = 1;

			//note: we expect the value to be large (most frequent use it to decode a Sequence Number), so there is no point in optimizing for single byte varints...

			ulong b = cursor[0];
			ulong res = b & 0x7F;
			if (b < 0x80)
			{
				goto done;
			}

			b = cursor[1];
			res |= (b & 0x7F) << 7;
			if (b < 0x80)
			{
				n = 2;
				goto done;
			}

			b = cursor[2];
			res |= (b & 0x7F) << 14;
			if (b < 0x80)
			{
				n = 3;
				goto done;
			}

			b = cursor[3];
			res |= (b & 0x7F) << 21;
			if (b < 0x80)
			{
				n = 4;
				goto done;
			}

			b = cursor[4];
			res |= (b & 0x7F) << 28;
			if (b < 0x80)
			{
				n = 5;
				goto done;
			}

			b = cursor[5];
			res |= (b & 0x7F) << 35;
			if (b < 0x80)
			{
				n = 6;
				goto done;
			}

			b = cursor[6];
			res |= (b & 0x7F) << 42;
			if (b < 0x80)
			{
				n = 7;
				goto done;
			}

			b = cursor[7];
			res |= (b & 0x7F) << 49;
			if (b < 0x80)
			{
				n = 8;
				goto done;
			}

			b = cursor[8];
			res |= (b & 0x7F) << 56;
			if (b < 0x80)
			{
				n = 9;
				goto done;
			}

			// the tenth byte should only have 1 bit worth of data
			b = cursor[9];
			if (b > 1) throw Errors.VarIntOverflow();
			res |= (b & 0x1) << 63;
			n = 10;

		done:
			value = res;
			return cursor + n;
		}

		/// <summary>Reads a 7-bit encoded unsigned long (aka 'Varint32') from the buffer, and advances the cursor</summary>
		/// <remarks>Can read up to 10 bytes from the input</remarks>
		[NotNull, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static byte* ReadVarint64([NotNull] byte* cursor, [NotNull] byte* stop, out ulong value)
		{
			Contract.Requires(cursor != null && stop != null);
			if (cursor < stop && (value = *cursor) < 0x80)
			{
				return cursor + 1;
			}
			else
			{
				return ReadVarint64Slow(cursor, stop, out value);
			}
		}

		[NotNull, MethodImpl(MethodImplOptions.NoInlining)]
		private static byte* ReadVarint64Slow([NotNull] byte* cursor, [NotNull] byte* stop, out ulong value)
		{
			uint n;

			// unless cursor >= stop, we already know that the first byte has the MSB set
			if (cursor >= stop) goto overflow;
			ulong b = cursor[0];
			Contract.Assert(b >= 0x80);
			ulong res = b & 0x7F;

			if (cursor >= stop) goto overflow;
			b = cursor[1];
			res |= (b & 0x7F) << 7;
			if (b < 0x80)
			{
				n = 2;
				goto done;
			}

			if (cursor >= stop) goto overflow;
			b = cursor[2];
			res |= (b & 0x7F) << 14;
			if (b < 0x80)
			{
				n = 3;
				goto done;
			}

			if (cursor >= stop) goto overflow;
			b = cursor[3];
			res |= (b & 0x7F) << 21;
			if (b < 0x80)
			{
				n = 4;
				goto done;
			}

			if (cursor >= stop) goto overflow;
			b = cursor[4];
			res |= (b & 0x7F) << 28;
			if (b < 0x80)
			{
				n = 5;
				goto done;
			}

			if (cursor >= stop) goto overflow;
			b = cursor[5];
			res |= (b & 0x7F) << 35;
			if (b < 0x80)
			{
				n = 6;
				goto done;
			}

			if (cursor >= stop) goto overflow;
			b = cursor[6];
			res |= (b & 0x7F) << 42;
			if (b < 0x80)
			{
				n = 7;
				goto done;
			}

			if (cursor >= stop) goto overflow;
			b = cursor[7];
			res |= (b & 0x7F) << 49;
			if (b < 0x80)
			{
				n = 8;
				goto done;
			}

			if (cursor >= stop) goto overflow;
			b = cursor[8];
			res |= (b & 0x7F) << 56;
			if (b < 0x80)
			{
				n = 9;
				goto done;
			}

			// the tenth byte should only have 1 bit worth of data
			if (cursor >= stop) goto overflow;
			b = cursor[9];
			if (b > 1) throw Errors.VarIntOverflow();
			res |= (b & 0x1) << 63;
			n = 10;

		done:
			value = res;
			return cursor + n;

		overflow:
			value = 0;
			throw Errors.VarIntTruncated();
		}

		/// <summary>Append a variable size byte sequence, using the VarInt encoding</summary>
		/// <remarks>This method performs bound checking.</remarks>
		[NotNull]
		public static byte* WriteVarBytes([NotNull] byte* ptr, [NotNull] byte* stop, byte* data, uint count)
		{
			if (count == 0)
			{ // "Nil"
				if (ptr >= stop) throw Errors.BufferOutOfBound();
				*ptr = 0;
				return ptr + 1;
			}
			var cursor = WriteVarInt32(ptr, stop, count);
			return WriteBytes(cursor, stop, data, count);
		}

		/// <summary>Append a variable size byte sequence with an extra 0 at the end, using the VarInt encoding</summary>
		/// <remarks>This method performs bound checking.</remarks>
		[NotNull]
		public static byte* WriteZeroTerminatedVarBytes([NotNull] byte* ptr, [NotNull] byte* stop, byte* data, uint count)
		{
			var cursor = WriteVarInt32(ptr, stop, count + 1);
			cursor = WriteBytes(cursor, stop, data, count);
			if (cursor >= stop) throw Errors.BufferOutOfBound();
			*cursor = 0;
			return cursor + 1;
		}

		#endregion

		#region Endianness...

#if EXPECT_LITTLE_ENDIAN_HOST
		// ReSharper disable ConditionIsAlwaysTrueOrFalse
		// ReSharper disable UnreachableCode
#pragma warning disable 162
#endif

		#region 16-bits

		/// <summary>Swap the order of the bytes in a 16-bit word</summary>
		/// <param name="value">0x0123</param>
		/// <returns>0x2301</returns>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static ushort ByteSwap16(ushort value)
		{
			return (ushort) ((value << 8) | (value >> 8));
		}

		/// <summary>Swap the order of the bytes in a 16-bit word</summary>
		/// <param name="value">0x0123</param>
		/// <returns>0x2301</returns>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static short ByteSwap16(short value)
		{
			//note: masking is required to get rid of the sign bit
			return (short) ((value << 8) | ((value >> 8) & 0xFF));
		}

		/// <summary>Load a 16-bit integer from an in-memory buffer that holds a value in Little-Endian ordering (also known as Host Order)</summary>
		/// <param name="ptr">Memory address of a 2-byte location</param>
		/// <returns>Logical value in host order</returns>
		/// <remarks><see cref="LoadInt16LE"/>([ 0x34, 0x12) => 0x1234</remarks>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static short LoadInt16LE([NotNull] void* ptr)
		{
			return IsLittleEndian ? *(short*)ptr : ByteSwap16(*(short*)ptr);
		}

		/// <summary>Load a 16-bit integer from an in-memory buffer that holds a value in Little-Endian ordering (also known as Host Order)</summary>
		/// <param name="ptr">Memory address of a 2-byte location</param>
		/// <returns>Logical value in host order</returns>
		/// <remarks><see cref="LoadUInt16LE"/>([ 0x34, 0x12) => 0x1234</remarks>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static ushort LoadUInt16LE([NotNull] void* ptr)
		{
			return IsLittleEndian ? *(ushort*) ptr : ByteSwap16(*(ushort*) ptr);
		}

		/// <summary>Store a 16-bit integer in an in-memory buffer that must hold a value in Little-Endian ordering (also known as Host Order)</summary>
		/// <param name="ptr">Memory address of a 2-byte location</param>
		/// <param name="value">Logical value to store in the buffer</param>
		/// <remarks><see cref="StoreInt16LE"/>(ptr, 0x1234) => ptr[0] == 0x34, ptr[1] == 0x12</remarks>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void StoreInt16LE([NotNull] void* ptr, short value)
		{
			*(short*)ptr = IsLittleEndian ? value : ByteSwap16(value);
		}

		/// <summary>Store a 16-bit integer in an in-memory buffer that must hold a value in Little-Endian ordering (also known as Host Order)</summary>
		/// <param name="ptr">Memory address of a 2-byte location</param>
		/// <param name="value">Logical value to store in the buffer</param>
		/// <remarks><see cref="StoreUInt16LE"/>(ptr, 0x1234) => ptr[0] == 0x34, ptr[1] == 0x12</remarks>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void StoreUInt16LE([NotNull] void* ptr, ushort value)
		{
			*(ushort*) ptr = IsLittleEndian ? value : ByteSwap16(value);
		}

		/// <summary>Load a 16-bit integer from an in-memory buffer that holds a value in Little-Endian ordering (also known as Host Order)</summary>
		/// <param name="ptr">Memory address of a 2-byte location</param>
		/// <returns>Logical value in host order</returns>
		/// <remarks><see cref="LoadInt16BE"/>([ 0x34, 0x12) => 0x1234</remarks>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static short LoadInt16BE([NotNull] void* ptr)
		{
			return IsLittleEndian ? ByteSwap16(*(short*) ptr) : *(short*) ptr;
		}

		/// <summary>Load a 16-bit integer from an in-memory buffer that holds a value in Big-Endian ordering (also known as Network Order)</summary>
		/// <param name="ptr">Memory address of a 2-byte location</param>
		/// <returns>Logical value in host order</returns>
		/// <remarks><see cref="LoadUInt16BE"/>([ 0x12, 0x34) => 0x1234</remarks>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static ushort LoadUInt16BE([NotNull] void* ptr)
		{
			return IsLittleEndian ? ByteSwap16(*(ushort*) ptr) : *(ushort*) ptr;
		}

		/// <summary>Store a 16-bit integer in an in-memory buffer that must hold a value in Big-Endian ordering (also known as Network Order)</summary>
		/// <param name="ptr">Memory address of a 2-byte location</param>
		/// <param name="value">Logical value to store in the buffer</param>
		/// <remarks><see cref="StoreUInt16BE"/>(ptr, 0x1234) => ptr[0] == 0x12, ptr[1] == 0x34</remarks>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void StoreInt16BE([NotNull] void* ptr, short value)
		{
			*(short*) ptr = IsLittleEndian ? ByteSwap16(value) : value;
		}

		/// <summary>Store a 16-bit integer in an in-memory buffer that must hold a value in Big-Endian ordering (also known as Network Order)</summary>
		/// <param name="ptr">Memory address of a 2-byte location</param>
		/// <param name="value">Logical value to store in the buffer</param>
		/// <remarks><see cref="StoreUInt16BE"/>(ptr, 0x1234) => ptr[0] == 0x12, ptr[1] == 0x34</remarks>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void StoreUInt16BE([NotNull] void* ptr, ushort value)
		{
			*(ushort*) ptr = IsLittleEndian ? ByteSwap16(value) : value;
		}

		#endregion

		#region 24-bits

		/// <summary>Swap the order of the bytes in a 24-bit word</summary>
		/// <param name="value">0x012345</param>
		/// <returns>0x452301</returns>
		/// <remarks>Bits 24-31 are ignored</remarks>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static uint ByteSwap24(uint value)
		{
			return (value & 0xFF) << 16 | (value & 0x00FF00) | ((value & 0xFF0000) >> 16);
		}

		/// <summary>Swap the order of the bytes in a 24-bit word</summary>
		/// <param name="value">0x0123</param>
		/// <returns>0x2301</returns>
		/// <remarks>Bits 24-31 are ignored</remarks>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static int ByteSwap24(int value)
		{
			//note: masking is required to get rid of the sign bit
			return (value & 0xFF) << 16 | (value & 0x00FF00) | ((value & 0xFF0000) >> 16);
		}

		/// <summary>Load a 24-bit integer from an in-memory buffer that holds a value in Little-Endian ordering (also known as Host Order)</summary>
		/// <param name="ptr">Memory address of a 2-byte location</param>
		/// <returns>Logical value in host order</returns>
		/// <remarks><see cref="LoadInt24LE"/>([ 0x56, 0x34, 0x12 ]) => 0x123456</remarks>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static int LoadInt24LE([NotNull] void* ptr)
		{
			uint x = *(ushort*) ptr;
			x |= (uint) ((byte*) ptr)[2] << 16;
			return IsLittleEndian ? (int) x : (int) ByteSwap24(x);
		}

		/// <summary>Load a 24-bit integer from an in-memory buffer that holds a value in Little-Endian ordering (also known as Host Order)</summary>
		/// <param name="ptr">Memory address of a 2-byte location</param>
		/// <returns>Logical value in host order</returns>
		/// <remarks><see cref="LoadUInt24LE"/>([ 0x56, 0x34, 0x12 ]) => 0x123456</remarks>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static uint LoadUInt24LE([NotNull] void* ptr)
		{
			uint x = *(ushort*)ptr;
			x |= (uint) ((byte*) ptr)[2] << 16;
			return IsLittleEndian ? x : ByteSwap24(x);
		}

		/// <summary>Store a 24-bit integer in an in-memory buffer that must hold a value in Little-Endian ordering (also known as Host Order)</summary>
		/// <param name="ptr">Memory address of a 3-byte location</param>
		/// <param name="value">Logical value to store in the buffer. Bits 24-31 are ignored</param>
		/// <remarks><see cref="StoreInt24LE"/>(ptr, 0x123456) => ptr[0] == 0x56, ptr[1] == 0x34, ptr[2] == 0x12</remarks>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void StoreInt24LE([NotNull] void* ptr, int value)
		{
			int x = IsLittleEndian ? value : ByteSwap24(value);
			*(short*) ptr = (short) x;
			((byte*) ptr)[2] = (byte) (x >> 16);
		}

		/// <summary>Store a 24-bit integer in an in-memory buffer that must hold a value in Little-Endian ordering (also known as Host Order)</summary>
		/// <param name="ptr">Memory address of a 3-byte location</param>
		/// <param name="value">Logical value to store in the buffer. Bits 24-31 are ignored</param>
		/// <remarks><see cref="StoreUInt24LE"/>(ptr, 0x123456) => ptr[0] == 0x56, ptr[1] == 0x34, ptr[2] == 0x12</remarks>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void StoreUInt24LE([NotNull] void* ptr, uint value)
		{
			uint x = IsLittleEndian ? value : ByteSwap24(value);
			*(ushort*)ptr = (ushort)x;
			((byte*)ptr)[2] = (byte)(x >> 16);
		}

		/// <summary>Load a 24-bit integer from an in-memory buffer that holds a value in Big-Endian ordering (also known as Network Order)</summary>
		/// <param name="ptr">Memory address of a 3-byte location</param>
		/// <returns>Logical value in host order</returns>
		/// <remarks><see cref="LoadInt24BE"/>([ 0x12, 0x34, 0x56 ]) => 0x123456</remarks>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static int LoadInt24BE([NotNull] void* ptr)
		{
			uint x = *(ushort*) ptr | ((uint) ((byte*) ptr)[2] << 16);
			return IsLittleEndian ? ByteSwap24((int) x) : (int) x;
		}

		/// <summary>Load a 24-bit integer from an in-memory buffer that holds a value in Big-Endian ordering (also known as Network Order)</summary>
		/// <param name="ptr">Memory address of a 3-byte location</param>
		/// <returns>Logical value in host order</returns>
		/// <remarks><see cref="LoadUInt24BE"/>([ 0x12, 0x34, 0x56 ]) => 0x123456</remarks>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static uint LoadUInt24BE([NotNull] void* ptr)
		{
			uint x = *(ushort*) ptr | ((uint) ((byte*) ptr)[2] << 16);
			return IsLittleEndian ? ByteSwap24(x) : x;
		}

		/// <summary>Store a 24-bit integer in an in-memory buffer that must hold a value in Big-Endian ordering (also known as Network Order)</summary>
		/// <param name="ptr">Memory address of a 3-byte location</param>
		/// <param name="value">Logical value to store in the buffer. Bits 24-31 are ignored</param>
		/// <remarks><see cref="StoreInt24BE"/>(ptr, 0x123456) => ptr[0] == 0x12, ptr[1] == 0x34, ptr[2] = 0x56</remarks>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void StoreInt24BE([NotNull] void* ptr, int value)
		{
			int x = IsLittleEndian ? ByteSwap24(value) : value;
			*(short*) ptr = (short) x;
			((byte*) ptr)[2] = (byte) (x >> 16);
		}

		/// <summary>Store a 24-bit integer in an in-memory buffer that must hold a value in Big-Endian ordering (also known as Network Order)</summary>
		/// <param name="ptr">Memory address of a 3-byte location</param>
		/// <param name="value">Logical value to store in the buffer. Bits 24-31 are ignored</param>
		/// <remarks><see cref="StoreUInt24BE"/>(ptr, 0x123456) => ptr[0] == 0x12, ptr[1] == 0x34, ptr[2] = 0x56</remarks>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void StoreUInt24BE([NotNull] void* ptr, uint value)
		{
			uint x = IsLittleEndian ? ByteSwap24(value) : value;
			*(ushort*)ptr = (ushort)x;
			((byte*)ptr)[2] = (byte)(x >> 16);
		}

		#endregion

		#region 32-bits

		/// <summary>Swap the order of the bytes in a 32-bit word</summary>
		/// <param name="value">0x01234567</param>
		/// <returns>0x67452301</returns>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static uint ByteSwap32(uint value)
		{
			const uint MASK1_HI = 0xFF00FF00;
			const uint MASK1_LO = 0x00FF00FF;
			//PERF: do not remove the local 'tmp' variable (reusing 'value' is 4X slower with RyuJit64 than introducing a tmp variable)
			uint tmp = ((value << 8) & MASK1_HI) | ((value >> 8) & MASK1_LO);
			return (tmp << 16) | (tmp >> 16);
		}

		/// <summary>Swap the order of the bytes in a 32-bit word</summary>
		/// <param name="value">0x01234567</param>
		/// <returns>0x67452301</returns>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static int ByteSwap32(int value)
		{
			const int MASK1_HI = unchecked((int) 0xFF00FF00);
			const int MASK1_LO = 0x00FF00FF;
			//PERF: do not remove the local 'tmp' variable! Reusing 'value' is 4X slower with RyuJit64 than introducing a tmp variable
			int tmp = ((value << 8) & MASK1_HI) | ((value >> 8) & MASK1_LO);
			return (tmp << 16) | ((tmp >> 16) & 0xFFFF);
		}

		/// <summary>Load a 32-bit integer from an in-memory buffer that holds a value in Little-Endian ordering (also known as Host Order)</summary>
		/// <param name="ptr">Memory address of a 4-byte location</param>
		/// <returns>Logical value in host order</returns>
		/// <remarks><see cref="LoadInt32LE"/>([ 0x78, 0x56, 0x34, 0x12) => 0x12345678</remarks>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static int LoadInt32LE([NotNull] void* ptr)
		{
			return IsLittleEndian ? *(int*) ptr : ByteSwap32(*(int*) ptr);
		}

		/// <summary>Load a 32-bit integer from an in-memory buffer that holds a value in Little-Endian ordering (also known as Host Order)</summary>
		/// <param name="ptr">Memory address of a 4-byte location</param>
		/// <returns>Logical value in host order</returns>
		/// <remarks><see cref="LoadUInt32LE"/>([ 0x78, 0x56, 0x34, 0x12) => 0x12345678</remarks>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static uint LoadUInt32LE([NotNull] void* ptr)
		{
			return IsLittleEndian ? * (uint*) ptr : ByteSwap32(* (uint*) ptr);
		}

		/// <summary>Store a 32-bit integer in an in-memory buffer that must hold a value in Little-Endian ordering (also known as Host Order)</summary>
		/// <param name="ptr">Memory address of a 4-byte location</param>
		/// <param name="value">Logical value to store in the buffer</param>
		/// <remarks><see cref="StoreInt32LE"/>(0x12345678) => ptr[0] == 0x78, ptr[1] == 0x56, ptr[2] == 0x34, ptr[3] == 0x12</remarks>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void StoreInt32LE([NotNull] void* ptr, int value)
		{
			*(int*) ptr = IsLittleEndian ? value : ByteSwap32(value);
		}

		/// <summary>Store a 32-bit integer in an in-memory buffer that must hold a value in Little-Endian ordering (also known as Host Order)</summary>
		/// <param name="ptr">Memory address of a 4-byte location</param>
		/// <param name="value">Logical value to store in the buffer</param>
		/// <remarks><see cref="StoreUInt32LE"/>(0x12345678) => ptr[0] == 0x78, ptr[1] == 0x56, ptr[2] == 0x34, ptr[3] == 0x12</remarks>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void StoreUInt32LE([NotNull] void* ptr, uint value)
		{
			*(uint*) ptr = IsLittleEndian ? value : ByteSwap32(value);
		}

		/// <summary>Load a 32-bit integer from an in-memory buffer that holds a value in Big-Endian ordering (also known as Network Order)</summary>
		/// <param name="ptr">Memory address of a 4-byte location</param>
		/// <returns>Logical value in host order</returns>
		/// <remarks><see cref="LoadInt32BE"/>([ 0x12, 0x34, 0x56, 0x78) => 0x12345678</remarks>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static int LoadInt32BE([NotNull] void* ptr)
		{
			return IsLittleEndian ? ByteSwap32(*(int*) ptr) : *(int*) ptr;
		}

		/// <summary>Load a 32-bit integer from an in-memory buffer that holds a value in Big-Endian ordering (also known as Network Order)</summary>
		/// <param name="ptr">Memory address of a 4-byte location</param>
		/// <returns>Logical value in host order</returns>
		/// <remarks><see cref="LoadUInt32BE"/>([ 0x12, 0x34, 0x56, 0x78) => 0x12345678</remarks>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static uint LoadUInt32BE([NotNull] void* ptr)
		{
			return IsLittleEndian ? ByteSwap32(*(uint*) ptr) : *(uint*) ptr;
		}

		/// <summary>Store a 32-bit integer in an in-memory buffer that must hold a value in Big-Endian ordering (also known as Network Order)</summary>
		/// <param name="ptr">Memory address of a 4-byte location</param>
		/// <param name="value">Logical value to store in the buffer</param>
		/// <remarks><see cref="StoreInt32BE"/>(ptr, 0x12345678) => ptr[0] == 0x12, ptr[1] == 0x34, ptr[2] == 0x56, ptr[3] == 0x78</remarks>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void StoreInt32BE([NotNull] void* ptr, int value)
		{
			*(int*) ptr = IsLittleEndian ? ByteSwap32(value) : value;
		}

		/// <summary>Store a 32-bit integer in an in-memory buffer that must hold a value in Big-Endian ordering (also known as Network Order)</summary>
		/// <param name="ptr">Memory address of a 4-byte location</param>
		/// <param name="value">Logical value to store in the buffer</param>
		/// <remarks><see cref="StoreUInt32BE"/>(ptr, 0x12345678) => ptr[0] == 0x12, ptr[1] == 0x34, ptr[2] == 0x56, ptr[3] == 0x78</remarks>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void StoreUInt32BE([NotNull] void* ptr, uint value)
		{
			*(uint*) ptr = IsLittleEndian ? ByteSwap32(value) : value;
		}

		#endregion

		#region 64-bits

		/// <summary>Swap the order of the bytes in a 64-bit word</summary>
		/// <param name="value">0x0123456789ABCDEF</param>
		/// <returns>0xEFCDAB8967452301</returns>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static ulong ByteSwap64(ulong value)
		{
			const ulong MASK1_HI = 0xFF00FF00FF00FF00UL;
			const ulong MASK1_LO = 0x00FF00FF00FF00FFUL;
			const ulong MASK2_HI = 0xFFFF0000FFFF0000UL;
			const ulong MASK2_LO = 0x0000FFFF0000FFFFUL;

			//PERF: do not remove the local 'tmp' variable! Reusing 'value' is 4X slower with RyuJit64 than introducing a tmp variable
			ulong tmp = ((value << 8) & MASK1_HI) | ((value >> 8) & MASK1_LO); // swap pairs of 1 byte
			tmp = ((tmp << 16) & MASK2_HI) | ((tmp >> 16) & MASK2_LO); // swap pairs of 2 bytes
			return (tmp << 32) | (tmp >> 32); // swap pairs of 4 bytes
		}

		/// <summary>Swap the order of the bytes in a 64-bit word</summary>
		/// <param name="value">0x0123456789ABCDEF</param>
		/// <returns>0xEFCDAB8967452301</returns>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static long ByteSwap64(long value)
		{
			const long MASK1_HI = unchecked((long) 0xFF00FF00FF00FF00L);
			const long MASK1_LO = 0x00FF00FF00FF00FFL;
			const long MASK2_HI = unchecked((long) 0xFFFF0000FFFF0000L);
			const long MASK2_LO = 0x0000FFFF0000FFFFL;

			//PERF: do not remove the local 'tmp' variable! Reusing 'value' is 4X slower with RyuJit64 than introducing a tmp variable
			long tmp = ((value << 8) & MASK1_HI) | ((value >> 8) & MASK1_LO); // swap pairs of 1 byte
			tmp = ((tmp << 16) & MASK2_HI) | ((tmp >> 16) & MASK2_LO); // swap pairs of 2 bytes
			return (tmp << 32) | ((tmp >> 32) & 0xFFFFFFFFL); // swap pairs of 4 bytes
		}

		/// <summary>Load a 64-bit integer from an in-memory buffer that holds a value in Little-Endian ordering (also known as Host Order)</summary>
		/// <param name="ptr">Memory address of an 8-byte location</param>
		/// <returns>Logical value in host order</returns>
		/// <remarks><see cref="LoadInt64LE"/>([ 0xEF, 0xCD, 0xAB, 0x89, 0x67, 0x456, 0x23, 0x01) => 0x0123456789ABCDEF</remarks>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static long LoadInt64LE([NotNull] void* ptr)
		{
			return IsLittleEndian ? *(long*) ptr : ByteSwap64(*(long*) ptr);
		}

		/// <summary>Load a 64-bit integer from an in-memory buffer that holds a value in Little-Endian ordering (also known as Host Order)</summary>
		/// <param name="ptr">Memory address of an 8-byte location</param>
		/// <returns>Logical value in host order</returns>
		/// <remarks><see cref="LoadUInt64LE"/>([ 0xEF, 0xCD, 0xAB, 0x89, 0x67, 0x456, 0x23, 0x01) => 0x0123456789ABCDEF</remarks>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static ulong LoadUInt64LE([NotNull] void* ptr)
		{
			return IsLittleEndian ? *(ulong*) ptr : ByteSwap64(*(ulong*) ptr);
		}

		/// <summary>Store a 64-bit integer in an in-memory buffer that must hold a value in Little-Endian ordering (also known as Host Order)</summary>
		/// <param name="ptr">Memory address of an 8-byte location</param>
		/// <param name="value">Logical value to store in the buffer</param>
		/// <remarks><see cref="StoreInt64LE"/>(0x0123456789ABCDEF) => ptr[0] == 0xEF, ptr[1] == 0xCD, ptr[2] == 0xAB, ptr[3] == 0x89, ..., ptr[7] == 0x01</remarks>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void StoreInt64LE([NotNull] void* ptr, long value)
		{
			*(long*) ptr = IsLittleEndian ? value : ByteSwap64(value);
		}

		/// <summary>Store a 64-bit integer in an in-memory buffer that must hold a value in Little-Endian ordering (also known as Host Order)</summary>
		/// <param name="ptr">Memory address of an 8-byte location</param>
		/// <param name="value">Logical value to store in the buffer</param>
		/// <remarks><see cref="StoreUInt64LE"/>(0x0123456789ABCDEF) => ptr[0] == 0xEF, ptr[1] == 0xCD, ptr[2] == 0xAB, ptr[3] == 0x89, ..., ptr[7] == 0x01</remarks>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void StoreUInt64LE([NotNull] void* ptr, ulong value)
		{
			*(ulong*) ptr = IsLittleEndian ? value : ByteSwap64(value);
		}

		/// <summary>Load a 64-bit integer from an in-memory buffer that holds a value in Big-Endian ordering (also known as Network Order)</summary>
		/// <param name="ptr">Memory address of an 8-byte location</param>
		/// <returns>Logical value in host order</returns>
		/// <remarks><see cref="LoadInt64BE"/>([ 0x01, 0x23, 0x45, 0x67, 0x89, 0xAB, 0xCD, 0xEF) => 0x0123456789ABCDEF</remarks>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static long LoadInt64BE([NotNull] void* ptr)
		{
			return IsLittleEndian ? ByteSwap64(*(long*) ptr) : *(long*) ptr;
		}

		/// <summary>Load a 64-bit integer from an in-memory buffer that holds a value in Big-Endian ordering (also known as Network Order)</summary>
		/// <param name="ptr">Memory address of an 8-byte location</param>
		/// <returns>Logical value in host order</returns>
		/// <remarks><see cref="LoadUInt64BE"/>([ 0x01, 0x23, 0x45, 0x67, 0x89, 0xAB, 0xCD, 0xEF) => 0x0123456789ABCDEF</remarks>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static ulong LoadUInt64BE([NotNull] void* ptr)
		{
			return IsLittleEndian ? ByteSwap64(*(ulong*) ptr) : *(ulong*) ptr;
		}

		/// <summary>Store a 64-bit integer in an in-memory buffer that must hold a value in Big-Endian ordering (also known as Network Order)</summary>
		/// <param name="ptr">Memory address of an 8-byte location</param>
		/// <param name="value">Logical value to store in the buffer</param>
		/// <remarks><see cref="StoreInt64BE"/>(ptr, 0x0123456789ABCDEF) => ptr[0] == 0x01, ptr[1] == 0x23, ptr[2] == 0x45, ptr[3] == 0x67, ..., ptr[7] == 0xEF</remarks>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void StoreInt64BE([NotNull] void* ptr, long value)
		{
			*(long*) ptr = IsLittleEndian ? ByteSwap64(value) : value;
		}

		/// <summary>Store a 64-bit integer in an in-memory buffer that must hold a value in Big-Endian ordering (also known as Network Order)</summary>
		/// <param name="ptr">Memory address of an 8-byte location</param>
		/// <param name="value">Logical value to store in the buffer</param>
		/// <remarks><see cref="StoreUInt64BE"/>(ptr, 0x0123456789ABCDEF) => ptr[0] == 0x01, ptr[1] == 0x23, ptr[2] == 0x45, ptr[3] == 0x67, ..., ptr[7] == 0xEF</remarks>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void StoreUInt64BE([NotNull] void* ptr, ulong value)
		{
			*(ulong*) ptr = IsLittleEndian ? ByteSwap64(value) : value;
		}

		#endregion

#if EXPECT_LITTLE_ENDIAN_HOST
		#pragma warning restore 162
		// ReSharper restore UnreachableCode
		// ReSharper restore ConditionIsAlwaysTrueOrFalse
#endif

		#endregion

		#region Fixed-Size Encoding

		// Plain old encoding where 32-bit values are stored using 4 bytes, 64-bit values are stored using 8 bytes, etc...
		// Methods without suffix use Little-Endian, while methods with 'BE' suffix uses Big Endian.

		#region 16-bit

		/// <summary>Append a fixed size 16-bit number to the output buffer, using little-endian ordering</summary>
		[NotNull, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static byte* WriteFixed16Unsafe([NotNull] byte* cursor, ushort value)
		{
			Contract.Requires(cursor != null);
			StoreUInt16LE((ushort*) cursor, value);
			return cursor + 2;
		}

		/// <summary>Append a fixed size 16-bit number to the output buffer, using little-endian ordering</summary>
		/// <remarks>This method DOES perform bound checking! Caller must ensure that the buffer has enough capacity</remarks>
		[NotNull, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static byte* WriteFixed16([NotNull] byte* cursor, [NotNull] byte* stop, ushort value)
		{
			Contract.Requires(cursor != null & stop != null);
			if (cursor + 2 > stop) throw Errors.BufferOutOfBound();
			StoreUInt16LE((ushort*) cursor, value);
			return cursor + 2;
		}

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static ushort ReadFixed16([NotNull] byte* p)
		{
			return LoadUInt16LE((ushort*) p);
		}

		[NotNull, Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static byte* ReadFixed16([NotNull] byte* p, out ushort value)
		{
			value = LoadUInt16LE((ushort*) p);
			return p + 2;
		}

		/// <summary>Append a fixed size 16-bit number to the output buffer, using little-endian ordering</summary>
		[NotNull, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static byte* WriteFixed16BEUnsafe([NotNull] byte* cursor, ushort value)
		{
			Contract.Requires(cursor != null);
			StoreUInt16BE((ushort*) cursor, value);
			return cursor + 2;
		}

		/// <summary>Append a fixed size 16-bit number to the output buffer, using little-endian ordering</summary>
		/// <remarks>This method DOES perform bound checking! Caller must ensure that the buffer has enough capacity</remarks>
		[NotNull, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static byte* WriteFixed16BE([NotNull] byte* cursor, [NotNull] byte* stop, ushort value)
		{
			Contract.Requires(cursor != null && stop != null);
			if (cursor + 2 > stop) throw Errors.BufferOutOfBound();
			StoreUInt16BE((ushort*) cursor, value);
			return cursor + 2;
		}

		/// <summary>Write a 16-bit zero</summary>
		[NotNull, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static byte* WriteZeroFixed16([NotNull] byte* cursor)
		{
			// this does not care about LE or BE
			*((ushort*)cursor) = 0;
			return cursor + 2;
		}

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static ushort ReadFixed16BE([NotNull] byte* p)
		{
			return LoadUInt16BE((ushort*) p);
		}

		[NotNull, Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static byte* ReadFixed16BE([NotNull] byte* p, out ushort value)
		{
			value = LoadUInt16BE((ushort*) p);
			return p + 2;
		}

		#endregion

		#region 32-bits

		/// <summary>Append a fixed size 32-bit number to the output buffer, using little-endian ordering</summary>
		[NotNull, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static byte* WriteFixed32Unsafe([NotNull] byte* cursor, uint value)
		{
			Contract.Requires(cursor != null);
			StoreUInt32LE((uint*) cursor, value);
			return cursor + 4;
		}

		/// <summary>Append a fixed size 32-bit number to the output buffer, using little-endian ordering</summary>
		/// <remarks>This method DOES perform bound checking! Caller must ensure that the buffer has enough capacity</remarks>
		[NotNull, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static byte* WriteFixed32([NotNull] byte* cursor, [NotNull] byte* stop, uint value)
		{
			Contract.Requires(cursor != null && stop != null);
			if (cursor + 4 > stop) throw Errors.BufferOutOfBound();
			StoreUInt32LE((uint*) cursor, value);
			return cursor + 4;
		}

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static uint ReadFixed32([NotNull] byte* p)
		{
			return LoadUInt32LE((uint*) p);
		}

		[NotNull, Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static byte* ReadFixed32([NotNull] byte* p, out uint value)
		{
			value = LoadUInt32LE((uint*) p);
			return p + 4;
		}

		/// <summary>Append a fixed size 32-bit number to the output buffer, using little-endian ordering</summary>
		[NotNull, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static byte* WriteFixed32BEUnsafe([NotNull] byte* cursor, uint value)
		{
			Contract.Requires(cursor != null);
			StoreUInt32BE((uint*) cursor, value);
			return cursor + 4;
		}

		/// <summary>Append a fixed size 32-bit number to the output buffer, using little-endian ordering</summary>
		/// <remarks>This method DOES perform bound checking! Caller must ensure that the buffer has enough capacity</remarks>
		[NotNull, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static byte* WriteFixed32BE([NotNull] byte* cursor, [NotNull] byte* stop, uint value)
		{
			Contract.Requires(cursor != null && stop != null);
			if (cursor + 4 > stop) throw Errors.BufferOutOfBound();
			StoreUInt32BE((uint*) cursor, value);
			return cursor + 4;
		}

		/// <summary>Write a 32-bit zero</summary>
		[NotNull, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static byte* WriteZeroFixed32([NotNull] byte* cursor)
		{
			// this does not care about LE or BE
			*((uint*)cursor) = 0;
			return cursor + 4;
		}

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static uint ReadFixed32BE([NotNull] byte* p)
		{
			return LoadUInt32BE((uint*) p);
		}

		[NotNull, Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static byte* ReadFixed32BE([NotNull] byte* p, out uint value)
		{
			value = LoadUInt32BE((uint*) p);
			return p + 4;
		}

		#endregion

		#region 64-bits

		/// <summary>Append a fixed size 64-bit number to the output buffer, using little-endian ordering</summary>
		[NotNull, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static byte* WriteFixed64Unsafe([NotNull] byte* cursor, ulong value)
		{
			Contract.Requires(cursor != null);
			StoreUInt64LE((ulong*) cursor, value);
			return cursor + 8;
		}

		/// <summary>Append a fixed size 64-bit number to the output buffer, using little-endian ordering</summary>
		/// <remarks>This method DOES perform bound checking! Caller must ensure that the buffer has enough capacity</remarks>
		[NotNull, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static byte* WriteFixed64([NotNull] byte* cursor, [NotNull] byte* stop, ulong value)
		{
			Contract.Requires(cursor != null && stop != null);
			if (cursor + 8 > stop) throw Errors.BufferOutOfBound();
			StoreUInt64LE((ulong*) cursor, value);
			return cursor + 8;
		}

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static ulong ReadFixed64([NotNull] byte* p)
		{
			return LoadUInt64LE((ulong*) p);
		}

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static byte* ReadFixed64([NotNull] byte* p, out ulong value)
		{
			value = LoadUInt64LE((ulong*) p);
			return p + 8;
		}

		/// <summary>Append a fixed size 64-bit number to the output buffer, using little-endian ordering</summary>
		[NotNull, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static byte* WriteFixed64BEUnsafe([NotNull] byte* cursor, ulong value)
		{
			Contract.Requires(cursor != null);
			StoreUInt64BE((ulong*) cursor, value);
			return cursor + 8;
		}

		/// <summary>Append a fixed size 64-bit number to the output buffer, using little-endian ordering</summary>
		/// <remarks>This method DOES perform bound checking! Caller must ensure that the buffer has enough capacity</remarks>
		[NotNull, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static byte* WriteFixed64BE([NotNull] byte* cursor, [NotNull] byte* stop, ulong value)
		{
			Contract.Requires(cursor != null && stop != null);
			if (cursor + 8 > stop) throw Errors.BufferOutOfBound();
			StoreUInt64BE((ulong*) cursor, value);
			return cursor + 8;
		}

		/// <summary>Write a 64-bit zero</summary>
		[NotNull, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static byte* WriteZeroFixed64([NotNull] byte* cursor)
		{
			// this does not care about LE or BE
			*((ulong*)cursor) = 0;
			return cursor + 8;
		}

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static ulong ReadFixed64BE([NotNull] byte* p)
		{
			return LoadUInt64BE((ulong*) p);
		}

		[NotNull, Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static byte* ReadFixed64BE([NotNull] byte* p, out ulong value)
		{
			value = LoadUInt64BE((ulong*) p);
			return p + 8;
		}

		#endregion

		#endregion

		#region Compact Unordered Encoding...

		// Simple encoding where each integer is stored using the smallest number of bytes possible.
		// The encoded result does preserve the value ordering, and the caller needs to remember the result size in order to decode the value from a stream.
		// Values from 0 to 0xFF will use 1 byte, values from 0x100 for 0xFFFF will use two bytes, and so on.

		/// <summary>Return the minimum number of bytes that hold the bits set (1) in a 32-bit unsigned integer</summary>
		/// <param name="value">Number that needs to be encoded</param>
		/// <returns>Number of bytes needed (1-4)</returns>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static uint SizeOfCompact16(ushort value)
		{
			return value <= 0xFF ? 1U : 2U;
		}

		/// <summary>Return the minimum number of bytes that hold the bits set (1) in a 32-bit unsigned integer</summary>
		/// <param name="value">Number that needs to be encoded</param>
		/// <returns>Number of bytes needed (1-4)</returns>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static uint SizeOfCompact32(uint value)
		{
			return value <= 0xFF ? 1U : SizeOfCompact32Slow(value);
		}

		[Pure, MethodImpl(MethodImplOptions.NoInlining)]
		private static uint SizeOfCompact32Slow(uint value)
		{
			// value is already known to be >= 256
			if (value < (1U << 16)) return 2;
			if (value < (1U << 24)) return 3;
			return 4;
		}

		/// <summary>Return the minimum number of bytes that hold the bits set (1) in a 64-bit unsigned integer</summary>
		/// <param name="value">Number that needs to be encoded</param>
		/// <returns>Number of bytes needed (1-8)</returns>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static uint SizeOfCompact64(ulong value)
		{
			return value <= 0xFF ? 1U : SizeOfCompact64Slow(value);
		}

		[Pure, MethodImpl(MethodImplOptions.NoInlining)]
		private static uint SizeOfCompact64Slow(ulong value)
		{
			// value is already known to be >= 256
			if (value < (1UL << 16)) return 2;
			if (value < (1UL << 24)) return 3;
			if (value < (1UL << 32)) return 4;
			if (value < (1UL << 40)) return 5;
			if (value < (1UL << 48)) return 6;
			if (value < (1UL << 56)) return 7;
			return 8;
		}

		[NotNull, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static byte* WriteCompact16Unsafe([NotNull] byte* ptr, ushort value)
		{
			Contract.Requires(ptr != null);
			if (value <= 0xFF)
			{
				*ptr = (byte) value;
				return ptr + 1;
			}

			StoreUInt16LE((ushort*) ptr, value);
			return ptr + 2;
		}

		[NotNull, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static byte* WriteCompact16BEUnsafe([NotNull] byte* ptr, ushort value)
		{
			Contract.Requires(ptr != null);
			if (value <= 0xFF)
			{
				*ptr = (byte) value;
				return ptr + 1;
			}

			StoreUInt16BE((ushort*) ptr, value);
			return ptr + 2;
		}

		[NotNull, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static byte* WriteCompact32Unsafe([NotNull] byte* ptr, uint value)
		{
			Contract.Requires(ptr != null);
			if (value <= 0xFF)
			{
				ptr[0] = (byte) value;
				return ptr + 1;
			}
			return WriteCompact32UnsafeSlow(ptr, value);
		}

		[NotNull]
		private static byte* WriteCompact32UnsafeSlow([NotNull] byte* ptr, uint value)
		{
			if (value <= 0xFFFF)
			{
				StoreUInt16LE((ushort*) ptr, (ushort) value);
				return ptr + 2;
			}

			if (value <= 0xFFFFFF)
			{
				StoreUInt16LE((ushort*) ptr, (ushort) value);
				ptr[2] = (byte) (value >> 16);
				return ptr + 3;
			}

			StoreUInt32LE((uint*) ptr, value);
			return ptr + 4;
		}

		[NotNull, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static byte* WriteCompact32BEUnsafe([NotNull] byte* ptr, uint value)
		{
			Contract.Requires(ptr != null);
			if (value <= 0xFF)
			{
				ptr[0] = (byte) value;
				return ptr + 1;
			}
			return WriteCompact32BEUnsafeSlow(ptr, value);
		}

		[NotNull]
		private static byte* WriteCompact32BEUnsafeSlow([NotNull] byte* ptr, uint value)
		{
			if (value <= 0xFFFF)
			{
				StoreUInt16BE((ushort*) ptr, (ushort) value);
				return ptr + 2;
			}

			if (value <= 0xFFFFFF)
			{
				ptr[0] = (byte) (value >> 16);
				StoreUInt16BE((ushort*) (ptr + 1), (ushort) value);
				return ptr + 3;
			}

			StoreUInt32BE((uint*) ptr, value);
			return ptr + 4;
		}

		[NotNull, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static byte* WriteCompact64Unsafe([NotNull] byte* ptr, ulong value)
		{
			Contract.Requires(ptr != null);
			if (value <= 0xFF)
			{ // 1 byte
				ptr[0] = (byte) value;
				return ptr + 1;
			}

			if (value >= 0x100000000000000)
			{ // 8 bytes
				StoreUInt64LE((ulong*) ptr, value);
				return ptr + 8;
			}

			return WriteCompact64UnsafeSlow(ptr, value);
		}

		[NotNull]
		private static byte* WriteCompact64UnsafeSlow([NotNull] byte* ptr, ulong value)
		{
			if (value <= 0xFFFFFFFF)
			{ // 2 .. 4 bytes

				if (value >= 0x1000000)
				{
					// 4 bytes
					StoreUInt32LE((uint*) ptr, (uint) value);
					return ptr + 4;
				}

				StoreUInt16LE((ushort*) ptr, (ushort) value);

				if (value <= 0xFFFF)
				{ // 2 bytes
					return ptr + 2;
				}

				// 3 bytes
				ptr[2] = (byte) (value >> 16);
				return ptr + 3;
			}
			else
			{ // 5 .. 7 bytes
				StoreUInt32LE((uint*) ptr, (uint) value);

				if (value <= 0xFFFFFFFFFF)
				{ // 5 bytes
					ptr[4] = (byte) (value >> 32);
					return ptr + 5;
				}

				if (value <= 0xFFFFFFFFFFFF)
				{ // 6 bytes
					StoreUInt16LE((ushort*) (ptr + 4), (ushort) (value >> 32));
					return ptr + 6;
				}

				// 7 bytes
				Contract.Assert(value <= 0xFFFFFFFFFFFFFF);
				StoreUInt16LE((ushort*) (ptr + 4), (ushort) (value >> 32));
				ptr[6] = (byte) (value >> 48);
				return ptr + 7;
			}
		}

		[NotNull, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static byte* WriteCompact64BEUnsafe([NotNull] byte* ptr, ulong value)
		{
			Contract.Requires(ptr != null);
			if (value <= 0xFF)
			{ // 1 byte
				ptr[0] = (byte) value;
				return ptr + 1;
			}

			if (value >= 0x100000000000000)
			{ // 8 bytes
				StoreUInt64BE((ulong*) ptr, value);
				return ptr + 8;
			}

			return WriteCompact64BEUnsafeSlow(ptr, value);
		}

		[NotNull]
		private static byte* WriteCompact64BEUnsafeSlow([NotNull] byte* ptr, ulong value)
		{
			if (value <= 0xFFFFFFFF)
			{ // 2 .. 4 bytes

				if (value >= 0x1000000)
				{
					// 4 bytes
					StoreUInt32BE((uint*) ptr, (uint) value);
					return ptr + 4;
				}


				if (value <= 0xFFFF)
				{ // 2 bytes
					StoreUInt16BE((ushort*) ptr, (ushort) value);
					return ptr + 2;
				}

				// 3 bytes
				StoreUInt16BE((ushort*) ptr, (ushort) (value >> 8));
				ptr[2] = (byte) value;
				return ptr + 3;
			}
			else
			{ // 5 .. 7 bytes

				if (value <= 0xFFFFFFFFFF)
				{ // 5 bytes
					StoreUInt32BE((uint*) ptr, (uint) (value >> 8));
					ptr[4] = (byte) value;
					return ptr + 5;
				}

				if (value <= 0xFFFFFFFFFFFF)
				{ // 6 bytes
					StoreUInt32BE((uint*) ptr, (uint) (value >> 16));
					StoreUInt16BE((ushort*) (ptr + 4), (ushort) value);
					return ptr + 6;
				}

				// 7 bytes
				Contract.Assert(value <= 0xFFFFFFFFFFFFFF);
				StoreUInt32BE((uint*) ptr, (uint) (value >> 24));
				StoreUInt16BE((ushort*) (ptr + 4), (ushort) (value >> 8));
				ptr[6] = (byte) value;
				return ptr + 7;
			}
		}

		#endregion

		#region Compact Ordered Encoding...

		// Specialized encoding to store counters (integers) using as few bytes as possible, but with the ordering preserved when using lexicographical order, i.e: Encoded(-1) < Encoded(0) < Encoded(42) < Encoded(12345678)
		//
		// There are two variantes: Unsigned and Signed which encodes either positive values (ie: sizes, count, ...) or negatives/values (integers, deltas, coordinates, ...)

		#region Unsigned

		// The signed variant uses the 3 highest bits to encode the number of extra bytes needed to store the value.
		// - The 5 lowest bits of the start byte are the 5 highest bits of the encoded value
		// - Each additional byte stores the next 8 bits until the last byte that stores the lowest 8 bits.
		// - To prevent multiple ways of encoding the same value (ex: 0 can be stored as '00' or '20 00' or '04 00 00'), and preserve the ordering guarantees, only the smallest form is legal
		// - Only values between 0 and 2^61 -1 can be encoded that way! (values >= 2^60 are NOT SUPPORTED).
		// - 4 bytes can encode up to 2^29-1 (~ sizes up to 512 MB), 8 bytes up to 2^61-1 (~ sizes up to 2 Exabytes)
		//
		// WIRE FORMAT: BBBNNNNN (NNNNNNNN ...)
		//
		//    MIN       MAX           SIZE       WIRE FORMAT                                                    = VALUE
		//     0        31          1 byte       000AAAAA                                                       = b_AAAAA (5 bits)
		//    32     (1<<13)-1      2 bytes      001AAAAA BBBBBBBB                                              = b_AAAAA_BBBBBBBB (13 bits)
		//  (1<<13)  (1<<21)-1      3 bytes      010AAAAA BBBBBBBB CCCCCCCC                                     = b_AAAAA_BBBBBBBB_CCCCCCCC (21 bits)
		//    ...
		//  (1<<53)  (1<<61)-1      8 bytes      111AAAAA BBBBBBBB CCCCCCCC DDDDDDDD EEEEEEEE FFFFFFFF GGGGGGGG = b_AAAAA_BBBBBBBB_CCCCCCCC_DDDDDDDD_EEEEEEEE_FFFFFFFF_GGGGGGGG (61 bits)
		//
		// Examples:
		// -      0 => b_000_00000 => (1) '00'
		// -      1 => b_000_00001 => (1) '01'
		// -     31 => b_000_11111 => (1) '1F'
		// -     32 => b_001_00000_00100000 => (2) '20 20'
		// -    123 => b_001_00000_01111011 => (2) '20 7B'
		// -   1234 => b_001_00100_11010010 => (2) '24 D2'
		// -  12345 => b_010_00000_00110000_00111001 => (3) '40 30 39'
		// - 2^16-1 => b_010_00000_11111111_11111111 => (3) '40 FF FF'
		// - 2^16   => b_010_00001_00000000_00000000 => (3) '41 00 00'
		// - 2^21-1 => b_010_11111_11111111_11111111 => (3) '5F FF FF'
		// - 2^21   => b_011_00000_00100000_00000000_00000000 => (4) '60 20 00 00'
		// - 2^29-1 => b_011_11111_11111111_11111111_11111111 => (4) '7F FF FF FF'
		// - 2^29   => b_100_00000_00100000_00000000_00000000_00000000 => (5) '80 20 00 00 00'
		// - 2^31-1 => b_100_00000_01111111_11111111_11111111_11111111 => (5) '80 7F FF FF FF'
		// - 2^32-1 => b_100_00000_11111111_11111111_11111111_11111111 => (5) '80 FF FF FF FF'
		// - 2^32   => b_100_00001_00000000_00000000_00000000_00000000 => (5) '81 00 00 00 00'
		// - 2^61-1 => b_111_11111_11111111_11111111_11111111_11111111_11111111_11111111_11111111 => (8) 'FF FF FF FF FF FF FF FF'

		private const int OCU_LEN0 = 0 << 5;
		private const int OCU_LEN1 = 1 << 5;
		private const int OCU_LEN2 = 2 << 5;
		private const int OCU_LEN3 = 3 << 5;
		private const int OCU_LEN4 = 4 << 5;
		private const int OCU_LEN5 = 5 << 5;
		private const int OCU_LEN6 = 6 << 5;
		private const int OCU_LEN7 = 7 << 5;
		private const int OCU_BITMAK = (1 << 5) - 1;
		private const uint OCU_MAX0 = (1U << 5) - 1;
		private const uint OCU_MAX1 = (1U << (5 + 8)) - 1;
		private const uint OCU_MAX2 = (1U << (5 + 8 * 2)) - 1;
		private const uint OCU_MAX3 = (1U << (5 + 8 * 3)) - 1;
		private const ulong OCU_MAX4 = (1UL << (5 + 8 * 4)) - 1;
		private const ulong OCU_MAX5 = (1UL << (5 + 8 * 5)) - 1;
		private const ulong OCU_MAX6 = (1UL << (5 + 8 * 6)) - 1;
		private const ulong OCU_MAX7 = (1UL << (5 + 8 * 7)) - 1;


		/// <summary>Return the size (in bytes) that a 32-bit counter value would need with the Compact Order Unsigned encoding</summary>
		/// <param name="value">Number that needs to be encoded</param>
		/// <returns>Number of bytes needed (1-5)</returns>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static int SizeOfOrderedUInt32(uint value)
		{
			return value <= OCU_MAX0 ? 1
			     : value <= OCU_MAX1 ? 2
			     : value <= OCU_MAX2 ? 3
			     : value <= OCU_MAX3 ? 4
			     : 5;
		}

		/// <summary>Return the size (in bytes) that a 64-bit counter value would need with the Compact Order Unsigned encoding</summary>
		/// <param name="value">Number that needs to be encoded, between 0 and 2^60-1</param>
		/// <returns>Number of bytes needed (1-8), or 0 if the number would overflow (2^60 or greater)</returns>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static int SizeOfOrderedUInt64(ulong value)
		{
			return value <= OCU_MAX0 ? 1
				 : value <= OCU_MAX1 ? 2
				 : value <= OCU_MAX2 ? 3
				 : value <= OCU_MAX3 ? 4
				 : value <= OCU_MAX4 ? 5
				 : value <= OCU_MAX5 ? 6
				 : value <= OCU_MAX6 ? 7
				 : value <= OCU_MAX7 ? 8
				 : 0; // this would throw!
		}

		/// <summary>Append an unsigned 32-bit counter value using a compact ordered encoding</summary>
		/// <param name="cursor">Pointer to the next free byte in the buffer</param>
		/// <param name="value">Positive counter value</param>
		/// <returns>Pointer updated with the number of bytes written</returns>
		/// <remarks>Will write between 1 and 5 bytes</remarks>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static byte* WriteOrderedUInt32Unsafe([NotNull] byte* cursor, uint value)
		{
			if (value <= OCU_MAX0)
			{ // < 32
				*cursor = (byte) (OCU_LEN0 | value);
				return cursor + 1;
			}
			if (value <= OCU_MAX1)
			{ // < 8 KB
				cursor[0] = (byte) (OCU_LEN1 | (value >> 8));
				cursor[1] = (byte) (value);
				return cursor + 2;
			}
			return WriteOrderedUInt32UnsafeSlow(cursor, value);
		}

		[MethodImpl(MethodImplOptions.NoInlining)]
		private static byte* WriteOrderedUInt32UnsafeSlow([NotNull] byte* cursor, uint value)
		{
			if (value <= OCU_MAX2)
			{ // < 2 MB
				cursor[0] = (byte)(OCU_LEN2 | (value >> 16));
				cursor[1] = (byte)(value >> 8);
				cursor[2] = (byte)(value);
				return cursor + 3;
			}
			if (value <= OCU_MAX3)
			{ // < 512 MB
				cursor[0] = (byte)(OCU_LEN3 | (value >> 24));
				cursor[1] = (byte)(value >> 16);
				cursor[2] = (byte)(value >> 8);
				cursor[3] = (byte)(value);
				return cursor + 4;
			}
			cursor[0] = OCU_LEN4; // we waste a byte for values >= 512MB, which is unfortunate...
			cursor[1] = (byte)(value >> 24);
			cursor[2] = (byte)(value >> 16);
			cursor[3] = (byte)(value >> 8);
			cursor[4] = (byte)(value);
			return cursor + 5;
		}

		/// <summary>Append an unsigned 64-bit counter value (up to 2^61-1) using the Compact Ordered Unsigned encoding</summary>
		/// <param name="cursor">Pointer to the next free byte in the buffer</param>
		/// <param name="value">Positive counter value that must be between 0 and 2^61 - 1 (2,305,843,009,213,693,951 or 0x1FFFFFFFFFFFFFFF)</param>
		/// <returns>Pointer updated with the number of bytes written</returns>
		/// <remarks>Will write between 1 and 8 bytes</remarks>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static byte* WriteOrderedUInt64Unsafe([NotNull] byte* cursor, ulong value)
		{
			return value <= uint.MaxValue ? WriteOrderedUInt32Unsafe(cursor, (uint) value) : WriteOrderedUInt64UnsafeSlow(cursor, value);
		}

		[MethodImpl(MethodImplOptions.NoInlining)]
		private static byte* WriteOrderedUInt64UnsafeSlow([NotNull] byte* cursor, ulong value)
		{
			if (value <= OCU_MAX4)
			{
				cursor[0] = (byte)(OCU_LEN4 | (value >> 32));
				cursor[1] = (byte)(value >> 24);
				cursor[2] = (byte)(value >> 16);
				cursor[3] = (byte)(value >> 8);
				cursor[4] = (byte)(value);
				return cursor + 5;
			}
			if (value <= OCU_MAX5)
			{
				cursor[0] = (byte)(OCU_LEN5 | (value >> 40));
				cursor[1] = (byte)(value >> 32);
				cursor[2] = (byte)(value >> 24);
				cursor[3] = (byte)(value >> 16);
				cursor[4] = (byte)(value >> 8);
				cursor[5] = (byte)(value);
				return cursor + 6;
			}
			if (value <= OCU_MAX6)
			{
				cursor[0] = (byte)(OCU_LEN6 | (value >> 48));
				cursor[1] = (byte)(value >> 40);
				cursor[2] = (byte)(value >> 32);
				cursor[3] = (byte)(value >> 24);
				cursor[4] = (byte)(value >> 16);
				cursor[5] = (byte)(value >> 8);
				cursor[6] = (byte)(value);
				return cursor + 7;
			}

			if (value <= OCU_MAX7)
			{
				cursor[0] = (byte) (OCU_LEN7 | (value >> 56));
				cursor[1] = (byte) (value >> 48);
				cursor[2] = (byte) (value >> 40);
				cursor[3] = (byte) (value >> 32);
				cursor[4] = (byte) (value >> 24);
				cursor[5] = (byte) (value >> 16);
				cursor[6] = (byte) (value >> 8);
				cursor[7] = (byte) (value);
				return cursor + 8;
			}

			throw new ArgumentOutOfRangeException(nameof(value), value, "Value must be less then 2^60");
		}

		/// <summary>Read an unsigned 32-bit counter value encoded using the Compact Ordered Unsigned encoding</summary>
		/// <param name="cursor"></param>
		/// <param name="value"></param>
		/// <returns></returns>
		public static byte* ReadOrderedUInt32Unsafe(byte* cursor, out uint value)
		{
			uint start = cursor[0];
			switch (start >> 5)
			{
				case 0:
					value = (start & OCU_BITMAK);
					return cursor + 1;
				case 1:
					value = ((start & OCU_BITMAK) << 8) | ((uint) cursor[1]);
					return cursor + 2;
				case 2:
					value = ((start & OCU_BITMAK) << 16) | ((uint) cursor[1] << 8) | ((uint) cursor[2]);
					return cursor + 3;
				case 3:
					value = ((start & OCU_BITMAK) << 24) | ((uint)cursor[1] << 16) | ((uint)cursor[2] << 8) | (uint)cursor[3];
					return cursor + 4;
				case 4:
					// start bits MUST be 0 (else, there is an overflow)
					if ((start & OCU_BITMAK) != 0) throw new InvalidDataException(); //TODO: message?
					value = ((uint)cursor[1] << 24) | ((uint)cursor[2] << 16) | ((uint)cursor[3] << 8) | (uint)cursor[4];
					return cursor + 5;
				default:
					// overflow?
					throw new InvalidDataException(); //TODO: message?
			}
		}

		/// <summary>Read an unsigned 64-bit counter value encoded using the Compact Ordered Unsigned encoding</summary>
		/// <param name="cursor"></param>
		/// <param name="value"></param>
		/// <returns></returns>
		public static byte* ReadOrderedUInt64Unsafe(byte* cursor, out ulong value)
		{
			ulong start = cursor[0];
			switch (start >> 5)
			{
				case 0:
					value = (start & OCU_BITMAK);
					return cursor + 1;
				case 1:
					value = ((start & OCU_BITMAK) << 8) | ((ulong)cursor[1]);
					return cursor + 2;
				case 2:
					value = ((start & OCU_BITMAK) << 16) | ((ulong)cursor[1] << 8) | ((ulong)cursor[2]);
					return cursor + 3;
				case 3:
					value = ((start & OCU_BITMAK) << 24) | ((ulong)cursor[1] << 16) | ((ulong)cursor[2] << 8) | ((ulong)cursor[3]);
					return cursor + 4;
				case 4:
					value = ((start & OCU_BITMAK) << 32) | ((ulong)cursor[1] << 24) | ((ulong)cursor[2] << 16) | ((ulong)cursor[3] << 8) | ((ulong)cursor[4]);
					return cursor + 5;
				case 5:
					value = ((start & OCU_BITMAK) << 40) | ((ulong)cursor[1] << 32) | ((ulong)cursor[2] << 24) | ((ulong)cursor[3] << 16) | ((ulong)cursor[4] << 8) | ((ulong)cursor[5]);
					return cursor + 6;
				case 6:
					value = ((start & OCU_BITMAK) << 48) | ((ulong)cursor[1] << 40) | ((ulong)cursor[2] << 32) | ((ulong)cursor[3] << 24) | ((ulong)cursor[4] << 16) | ((ulong)cursor[5] << 8) | ((ulong)cursor[6]);
					return cursor + 7;
				default: // 7
					value = ((start & OCU_BITMAK) << 56) | ((ulong)cursor[1] << 48) | ((ulong)cursor[2] << 40) | ((ulong)cursor[3] << 32) | ((ulong)cursor[4] << 24) | ((ulong)cursor[5] << 16) | ((ulong)cursor[6] << 8) | ((ulong)cursor[7]);
					return cursor + 8;
			}
		}

		#endregion

		#region Signed

		// The signed variant is very similar, except that the start byte uses an additional "Sign" bit (inverted)
		// - The hight bit (bit 7) of the start byte is 0 for negative numbers, and 1 for positive numbers
		// - The next 3 bits (bits 6-4) of the start byte encode the number of extra bytes following
		// - The last 4 bits (bit 3-0) contain the 4 highest bits of the encoded value
		// - Each additional byte stores the next 8 bits until the last byte that stores the lowest 8 bits.
		// - For negative values, the number of bytes required is computed by using Abs(X)-1, but the original negative value is used (after masking)
		//   i.e.: -1 becomes -(-1)-1 = 0 (which fits in 4 bits), and will be encoded as (-1) & 0xF = b_0_000_1111 = '0F', and 0 will be encoded as b_1_000_0000 = '10' (which is indeeded sorted after '0F')
		// - Only values between -2^60 and 2^60-1 can be encoded that way! (values < -2^60 or >= 2^60 are NOT SUPPORTED)

		// WIRE FORMAT: SBBBNNNN (NNNNNNNN ...)
		// - if S = 0, X is negative: BBB = 7 - exta bytes, NNN...N = 2's complement of X
		// - if S = 1, X is positive: BBB = exta bytes, NNN...N = X
		//
		//    MIN       MAX           SIZE       WIRE FORMAT                                                    = VALUE
		//  -(1<<60)  -(1<<52)-1    8 bytes      1111AAAA BBBBBBBB CCCCCCCC DDDDDDDD EEEEEEEE FFFFFFFF GGGGGGGG = b_AAAA_BBBBBBBB_CCCCCCCC_DDDDDDDD_EEEEEEEE_FFFFFFFF_GGGGGGGG (60 bits)
		//    ...
		//  -(1<<12)    -17         2 bytes      1001AAAA BBBBBBBB                                              = ~(b_AAAA_BBBBBBBB - 1) (12 bits)
		//   -16        -1          1 byte       0000AAAA                                                       = ~(b_AAAA - 1) (4 bits)
		//     0        +15         1 byte       1000AAAA                                                       = b_AAAA (4 bits)
		//    +16    (1<<12)-1      2 bytes      1001AAAA BBBBBBBB                                              = b_AAAA_BBBBBBBB (12 bits)
		//    ...
		//  (1<<52)  (1<<60)-1      8 bytes      1111AAAA BBBBBBBB CCCCCCCC DDDDDDDD EEEEEEEE FFFFFFFF GGGGGGGG = b_AAAA_BBBBBBBB_CCCCCCCC_DDDDDDDD_EEEEEEEE_FFFFFFFF_GGGGGGGG (60 bits)
		//
		// Examples:
		// -      0 => b_1_000_0000 => (1) '80'
		// -      1 => b_1_000_0001 => (1) '81'
		// -     15 => b_1_000_1111 => (1) '8F'
		// -     16 => b_1_001_0000_00010000 => (2) '90 10'
		// -    123 => b_1_001_0000_01111011 => (2) '90 7B'
		// -   1234 => b_1_001_0100_11010010 => (2) '94 D2'
		// -  12345 => b_1_010_0000_00110000_00111001 => (3) 'A0 30 39'
		// - 2^16-1 => b_1_010_0001_00000000_00000000 => (3) 'A1 00 00'
		// - 2^20-1 => b_1_010_1111_11111111_11111111 => (3) 'AF FF FF'
		// - 2^21   => b_1_011_0000_00100000_00000000_00000000 => (4) 'B0 20 00 00'
		// - 2^28-1 => b_1_011_1111_11111111_11111111_11111111 => (4) 'BF FF FF FF'
		// - 2^32-1 => b_1_100_0000_11111111_11111111_11111111_11111111 => (4) 'C0 FF FF FF FF'
		// - 2^32   => b_1_100_0001_00000000_00000000_00000000_00000000 => (4) 'C1 00 00 00 00'
		// - 2^60-1 => b_1_111_1111_11111111_11111111_11111111_11111111_11111111_11111111_11111111 => (8) 'FF FF FF FF FF FF FF FF'

		//TODO!

		#endregion

		#endregion

		/// <summary>Convert a char in range '0-9A-Fa-f' into a value between 0 and 15</summary>
		/// <remarks>Result is unspecified if char is not in the valid range!</remarks>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static int Nibble(char c)
		{
			// The lowest 4 bits almost give us the result we want:
			// - '0'..'9': (c & 15) = 0..9; need to add 0 to get correct result
			// - 'A'..'F': (c & 15) = 1..6; need to add 9 to get correct reuslt
			// - 'a'..'f': (c & 15) = 1..6; need to add 9 to get correct reuslt
			// We just need to tweak the value to have a bit that is different between digits and letters, and use that bit to compute the final offset of 0 or 9
			return (c & 15) + (((((c + 16) & ~64) >> 4) & 1) * 9);
		}

		/// <summary>Convert values between 0 and 15 into a character from in range '0-9A-F'</summary>
		/// <remarks>Only the lower 4 bits are used, so the caller does not need to mask out the upper bits!</remarks>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static char Nibble(int x)
		{
			// We first tweak the value in order to have a bit that is different between 0-9 and 10-15.
			// Then, we use that bit to compute the final offset that will end up adding +48 or +55
			//  0-9  : X + 54 + 1 - (1 x 7) = X + 48 = '0'-'9'
			// 10-15 : X + 54 + 1 - (0 x 7) = X + 55 = 'A'-'F'
			int tmp = ((x & 0xF) + 54);
			return (char)(tmp + 1 - ((tmp & 32) >> 5) * 7);
			//REVIEW: '* 7' could probably be replaced with some shift/add trickery... (but maybe the JIT will do it for us?)
		}

		#region String Helpers...

		/// <summary>Check if a string only contains characters between 0 and 127 (ASCII)</summary>
		[Pure]
		public static bool IsAsciiString([NotNull] string value)
		{
			Contract.Requires(value != null);
			fixed (char* pChars = value)
			{
				return IsAsciiString(pChars, value.Length);
			}
		}

		/// <summary>Check if a section of a string only contains characters between 0 and 127 (ASCII)</summary>
		[Pure]
		public static bool IsAsciiString([NotNull] string value, int offset, int count)
		{
			Contract.Requires(value != null && offset >= 0 && count <= 0 && offset + count <= value.Length);
			if (count == 0) return true;
			fixed (char* pChars = value)
			{
				return IsAsciiString(pChars + offset, count);
			}
		}

#if ENABLE_SPAN
		/// <summary>Check if a section of a string only contains characters between 0 and 127 (ASCII)</summary>
		[Pure]
		public static bool IsAsciiString(ReadOnlySpan<char> value)
		{
			if (value.Length == 0) return true;
			fixed (char* pChars = &MemoryMarshal.GetReference(value))
			{
				return IsAsciiString(pChars, value.Length);
			}
		}
#endif

		/// <summary>Check if a string only contains characters between 0 and 127 (ASCII)</summary>
		[Pure]
		public static bool IsAsciiString([NotNull] char* pChars, int numChars)
		{
			Contract.Requires(pChars != null);
			// we test if each char has at least one bit set above bit 7, ie: (char & 0xFF80) != 0
			// to speed things up, we check multiple chars at a time

			#region Performance Notes...
			/*
			The following loop is optimized to produce the best x64 code with Deskop CLR RyuJitJIT (x64) that is currently in 4.6.2 (preview)
			=> if the JIT changes, we may need to revisit!

			Currently, the x64 code generated for the main unrolled loop looks like this:

			MAIN_LOOP:
				// rax = ptr
				// rcx = end
				(01)    cmp     rax,rcx                 // while (ptr < end)
				(02)    jae     TAIL                    // => bypass for small strings <= 7 chars

			LOOP:
				(03)    mov     r8,qword ptr [rax]      // ulong x1 = *(ulong*) (ptr + 0);
				(04)    mov     r9,qword ptr [rax+8]    // ulong x2 = *(ulong*) (ptr + 8);
				(05)    mov     r10,qword ptr [rax+10h] // ulong x3 = *(ulong*) (ptr + 8);
				(06)    mov     r11,qword ptr [rax+18h] // ulong x4 = *(ulong*) (ptr + 12);
				(07)    mov     rsi,0FF80FF80FF80FF80h
				(08)    and     r8,rsi                  // x1 &= MASK4;
				(09)    and     r9,rsi                  // x2 &= MASK4;
				(10)    and     r10,rsi                 // x3 &= MASK4;
				(11)    and     r11,rsi                 // x4 &= MASK4;
				(12)    add     rax,20h                 // ptr += 16;
				(13)    or      r8,r9                   // (x1 != 0 || x2 != 0)
				(14)    mov     r9,r10
				(15)    or      r9,r11                  // (x3 != 0 || x4 != 0)
				(16)    or      r8,r9                   // (...) || (...)
				(17)    test    r8,r8                   // if (...) ...
				(18)    jne     INVALID                 // ... goto INVALID;
				(19)    cmp     rax,rcx                 // while (ptr < end)
				(20)    jb      LOOP                    // ... (continue)

			TAIL:
				// continue for size <= 7

			Commentary:
			- At 3 to 6 we parallelize the reads from memory into 4 register
			- At 8 to 11 we perform the ANDs again in a way that can be //ized by the CPU
			- At 12, we pre-increment the pointer, so that the value is ready at 19
			- At 13 to 16, the whole if expression is optimized into a 3 or in cascade.
			  - note: doing "(... || ...) || (... || ...)" is ~5% faster than "(... || ... || ... || ...)" on my CPU
			- At 18, we jump to the "INVALID" case, instead of doing "return false", because current JIT produce better code that way
			  - note: if we "return false" here, the JIT adds an additional JMP inside the loop, which if ~15% slower on my CPU
			*/
			#endregion

			const  ulong MASK_4_CHARS = 0xFF80FF80FF80FF80UL;
			const   uint MASK_2_CHARS = 0xFF80FF80U;
			const ushort MASK_1_CHAR = 0xFF80;

			char* ptr = pChars;
			char* end = ptr + (numChars & ~15);
			while (ptr < end)
			{
				ulong x1 = *(ulong*) (ptr + 0);
				ulong x2 = *(ulong*) (ptr + 4);
				ulong x3 = *(ulong*) (ptr + 8);
				ulong x4 = *(ulong*) (ptr + 12);
				// combine all the bits together in stages
				x1 |= x2;
				x3 |= x4;
				x1 |= x3;
				// drop the LS 7 bits
				x1 &= MASK_4_CHARS;
				ptr += 16;
				if (x1 != 0) goto INVALID;
			}

			if ((numChars & 8) != 0)
			{
				ulong x1 = *(ulong*) (ptr + 0);
				ulong x2 = *(ulong*) (ptr + 4);
				x1 = x1 | x2;
				x1 &= MASK_4_CHARS;
				ptr += 8;
				if (x1 != 0) goto INVALID;
			}

			if ((numChars & 4) != 0)
			{
				ulong x1 = *(ulong*) ptr & MASK_4_CHARS;
				if (x1 != 0) goto INVALID;
				ptr += 4;
			}
			if ((numChars & 2) != 0)
			{
				uint x1 = *(uint*) ptr & MASK_2_CHARS;
				if (x1 != 0) goto INVALID;
				ptr += 2;
			}
			// check the last character, if present
			return (numChars & 1) == 0 || (*ptr & MASK_1_CHAR) == 0;

		INVALID:
			// there is one character that is >= 0x80 in the string
			return false;
		}

		/// <summary>Check if a section of byte array only contains bytes between 0 and 127 (7-bit ASCII)</summary>
		/// <returns>False if at least one byte has bit 7 set to 1; otherwise, True.</returns>
		[Pure]
		public static bool IsAsciiBytes([NotNull] byte[] array, int offset, int count)
		{
			Contract.Requires(array != null);
			fixed (byte* pBytes = &array[offset])
			{
				return IsAsciiBytes(pBytes, checked((uint) count));
			}
		}

		/// <summary>Check if a memory region only contains bytes between 0 and 127 (7-bit ASCII)</summary>
		/// <returns>False if at least one byte has bit 7 set to 1; otherwise, True.</returns>
		[Pure]
		public static bool IsAsciiBytes([NotNull] byte* buffer, uint count)
		{
			Contract.Requires(buffer != null);

			// we test if each byte has at least one bit set above bit 7, ie: (byte & 0x80) != 0
			// to speed things up, we check multiple bytes at a time

			const ulong MASK_8 = 0x8080808080808080UL;
			const uint MASK_4 = 0x80808080U;
			const int MASK_2 = 0x8080;
			const int MASK_1 = 0x80;

			byte* end = buffer + (count & ~31);
			byte* ptr = buffer;
			while (ptr < end)
			{
				ulong x1 = *((ulong*) ptr + 0);
				ulong x2 = *((ulong*) ptr + 1);
				ulong x3 = *((ulong*) ptr + 2);
				ulong x4 = *((ulong*) ptr + 3);
				x1 |= x2;
				x3 |= x4;
				x1 |= x3;
				x1 &= MASK_8;
				ptr += 32;
				if (x1 != 0) goto INVALID;
			}

			if ((count & 16) != 0)
			{
				ulong x1 = *((ulong*) ptr + 0);
				ulong x2 = *((ulong*) ptr + 1);
				x1 |= x2;
				x1 &= MASK_8;
				ptr += 16;
				if (x1 != 0) goto INVALID;
			}
			if ((count & 8) != 0)
			{
				if ((*((ulong*) ptr) & MASK_8) != 0) goto INVALID;
				ptr += 8;
			}
			if ((count & 4) != 0)
			{
				if ((*((uint*) ptr) & MASK_4) != 0) goto INVALID;
				ptr += 4;
			}
			if ((count & 2) != 0)
			{
				if ((*((ushort*) ptr) & MASK_2) != 0) goto INVALID;
				ptr += 2;
			}
			if ((count & 1) != 0)
			{
				return *ptr < MASK_1;
			}
			// there is one character that is >= 0x80 in the string
			return true;
		INVALID:
			return false;
		}

		/// <summary>Convert a byte stream into a .NET string by expanding each byte to 16 bits characters</summary>
		/// <returns>Equivalent .NET string</returns>
		/// <remarks>
		/// This is safe to use with 7-bit ASCII strings.
		/// You should *NOT* use this if the buffer contains ANSI or UTF-8 encoded strings!
		/// If the bufer contains bytes that are >= 0x80, they will be mapped to the equivalent Unicode code points (0x80..0xFF), WITHOUT converting them using current ANSI code page.
		/// </remarks>
		/// <example>
		/// ConvertToByteString(new byte[] { 'A', 'B', 'C' }, 0, 3) => "ABC"
		/// ConvertToByteString(new byte[] { 255, 'A', 'B', 'C' }, 0, 4) => "\xffABC"
		/// ConvertToByteString(UTF8("é"), ...) => "Ã©" (len=2, 'C3 A9')
		/// </example>
		[Pure, NotNull]
		public static string ConvertToByteString([NotNull] byte[] array, int offset, int count)
		{
			Contract.Requires(array != null && offset >= 0 && count >= 0 && offset + count <= array.Length);

			// fast allocate a new empty string that will be mutated in-place.
			//note: this calls String::CtorCharCount() which in turn calls FastAllocateString(..), but will not fill the buffer with 0s if 'char' == '\0'
			string str = new string('\0', count);

			fixed (byte* ptr = &array[offset])
			fixed (char* pChars = str)
			{
				ConvertToByteStringUnsafe(pChars, ptr, (uint) count);
				return str;
			}
		}

		/// <summary>Convert a byte stream into a .NET string by expanding each byte to 16 bits characters</summary>
		/// <returns>Equivalent .NET string</returns>
		/// <remarks>
		/// This is safe to use with 7-bit ASCII strings.
		/// You should *NOT* use this if the buffer contains ANSI or UTF-8 encoded strings!
		/// If the bufer contains bytes that are >= 0x80, they will be mapped to the equivalent Unicode code points (0x80..0xFF), WITHOUT converting them using current ANSI code page.
		/// </remarks>
		[Pure, NotNull]
		public static string ConvertToByteString(byte* pBytes, uint count)
		{
			Contract.Requires(pBytes != null);

			if (count == 0) return String.Empty;

			// fast allocate a new empty string that will be mutated in-place.
			//note: this calls String::CtorCharCount() which in turn calls FastAllocateString(..), but will not fill the buffer with 0s if 'char' == '\0'
			string str = new string('\0', checked((int) count));
			fixed (char* pChars = str)
			{
				ConvertToByteStringUnsafe(pChars, pBytes, count);
				return str;
			}
		}

		internal static void ConvertToByteStringUnsafe(char* pChars, byte* pBytes, uint count)
		{
			byte* inp = pBytes;
			char* outp = pChars;

			// unroll 4 characters at a time
			byte* inend = pBytes + (count & ~3);
			while (inp < inend)
			{
				//this loop has been verified to produce the best x64 code I could get out from the DesktopCLR JIT (4.6.x)
				long x = *(long*) inp;
				// split
				long y1 = x & 0xFF;
				long y2 = x & 0xFF00;
				long y3 = x & 0xFF0000;
				long y4 = x & 0xFF000000;
				// shift
				y2 <<= 8;
				y3 <<= 16;
				y4 <<= 24;
				// merge
				y1 |= y2;
				y3 |= y4;
				y1 |= y3;
				// output
				*(long*) outp = y1;
				inp += 4;
				outp += 4;
			}
			// complete the tail

			if ((count & 2) != 0)
			{ // two chars
				int x = *(ushort*) inp;
				// split
				int y1 = x & 0xFF;
				int y2 = x & 0xFF00;
				// shift
				y2 <<= 8;
				// merge
				y2 |= y1;
				// output
				*(int*) outp = y2;
				inp += 2;
				outp += 2;
			}

			if ((count & 1) != 0)
			{ // one char
				*outp = (char) *inp;
			}
		}
		#endregion

		[SuppressUnmanagedCodeSecurity]
		[SecurityCritical]
		internal static class NativeMethods
		{
			// C/C++		.NET
			// ---------------------------------
			// void*		byte* (or IntPtr)
			// size_t		UIntPtr (or IntPtr)
			// int			int
			// char			byte

			/// <summary>Compare characters in two buffers.</summary>
			/// <param name="buf1">First buffer.</param>
			/// <param name="buf2">Second buffer.</param>
			/// <param name="count">Number of bytes to compare.</param>
			/// <returns>The return value indicates the relationship between the buffers.</returns>
			[DllImport("msvcrt.dll", CallingConvention = CallingConvention.Cdecl, SetLastError = false)]
			[ReliabilityContract(Consistency.WillNotCorruptState, Cer.Success)]
			public static extern int memcmp(byte* buf1, byte* buf2, UIntPtr count);

			/// <summary>Moves one buffer to another.</summary>
			/// <param name="dest">Destination object.</param>
			/// <param name="src">Source object.</param>
			/// <param name="count">Number of bytes to copy.</param>
			/// <returns>The value of dest.</returns>
			/// <remarks>Copies count bytes from src to dest. If some regions of the source area and the destination overlap, both functions ensure that the original source bytes in the overlapping region are copied before being overwritten.</remarks>
			[DllImport("msvcrt.dll", CallingConvention = CallingConvention.Cdecl, SetLastError = false)]
			[ReliabilityContract(Consistency.WillNotCorruptState, Cer.Success)]
			public static extern byte* memmove(byte* dest, byte* src, UIntPtr count);

			/// <summary>Sets buffers to a specified character.</summary>
			/// <param name="dest">Pointer to destination</param>
			/// <param name="ch">Character to set</param>
			/// <param name="count">Number of characters</param>
			/// <returns>memset returns the value of dest.</returns>
			/// <remarks>The memset function sets the first count bytes of dest to the character c.</remarks>
			[DllImport("msvcrt.dll", CallingConvention = CallingConvention.Cdecl, SetLastError = false)]
			public static extern byte* memset(byte* dest, int ch, UIntPtr count);

		}

		[DebuggerNonUserCode]
		internal static class Errors
		{

			/// <summary>Reject an invalid slice by throw an error with the appropriate diagnostic message.</summary>
			/// <exception cref="ArgumentException">If the corresponding slice is invalid (offset or count out of bounds, array is null, ...)</exception>
			[Pure, NotNull, MethodImpl(MethodImplOptions.NoInlining)]
			public static Exception MalformedBuffer(byte* bytes, long count)
			{
				if (count < 0) return BufferCountNotNeg();
				if (count > 0)
				{
					if (bytes == null) return BufferArrayNotNull();
				}
				// maybe it's Lupus ?
				return BufferInvalid();
			}

			/// <summary>Reject an invalid slice by throw an error with the appropriate diagnostic message.</summary>
			/// <exception cref="ArgumentException">If the corresponding slice is invalid (offset or count out of bounds, array is null, ...)</exception>
			[Pure, NotNull, MethodImpl(MethodImplOptions.NoInlining)]
			public static Exception MalformedBuffer(byte[] array, long offset, long count)
			{
				if (offset < 0) return BufferOffsetNotNeg();
				if (count < 0) return BufferCountNotNeg();
				if (count > 0)
				{
					if (array == null) return BufferArrayNotNull();
					if (offset + count > array.Length) return BufferArrayToSmall();
				}
				// maybe it's Lupus ?
				return BufferInvalid();
			}

			[Pure, NotNull, MethodImpl(MethodImplOptions.NoInlining)]
			public static OverflowException PowerOfTwoOverflow()
			{
				return new OverflowException("Cannot compute the next power of two because the value would overflow.");
			}

			[Pure, NotNull, MethodImpl(MethodImplOptions.NoInlining)]
			public static OverflowException PowerOfTwoNegative()
			{
				return new OverflowException("Cannot compute the next power of two for negative numbers.");
			}

			/// <summary>Reject an attempt to write past the end of a buffer</summary>
			[Pure, NotNull, MethodImpl(MethodImplOptions.NoInlining)]
			public static InvalidOperationException BufferOutOfBound()
			{
				return new InvalidOperationException("Attempt to write outside of the buffer, or at a position that would overflow past the end.");
			}

			[ContractAnnotation("=> halt"), MethodImpl(MethodImplOptions.AggressiveInlining)]
			public static void ThrowOffsetOutsideSlice()
			{
				throw OffsetOutsideSlice();
			}

			[Pure, NotNull, MethodImpl(MethodImplOptions.NoInlining)]
			public static Exception OffsetOutsideSlice()
			{
				// ReSharper disable once NotResolvedInText
				return ThrowHelper.ArgumentOutOfRangeException("offset", "Offset is outside the bounds of the slice.");
			}

			[ContractAnnotation("=> halt"), MethodImpl(MethodImplOptions.AggressiveInlining)]
			public static void ThrowIndexOutOfBound(int index)
			{
				throw IndexOutOfBound(index);
			}

			[Pure, NotNull, MethodImpl(MethodImplOptions.NoInlining)]
			public static IndexOutOfRangeException IndexOutOfBound(int index)
			{
				return new IndexOutOfRangeException("Index is outside the slice");
			}

			[Pure, NotNull, MethodImpl(MethodImplOptions.NoInlining)]
			public static FormatException SliceOffsetNotNeg()
			{
				return new FormatException("The specified slice has a negative offset, which is not legal. This may be a side effect of memory corruption.");
			}

			[ContractAnnotation("=> halt"), MethodImpl(MethodImplOptions.AggressiveInlining)]
			public static void ThrowSliceCountNotNeg()
			{
				throw SliceCountNotNeg();
			}

			[Pure, NotNull, MethodImpl(MethodImplOptions.NoInlining)]
			public static FormatException SliceCountNotNeg()
			{
				return new FormatException("The specified slice has a negative size, which is not legal. This may be a side effect of memory corruption.");
			}

			[Pure, NotNull, MethodImpl(MethodImplOptions.NoInlining)]
			public static FormatException SliceBufferNotNull()
			{
				return new FormatException("The specified slice is missing its underlying buffer.");
			}

			[ContractAnnotation("=> halt"), MethodImpl(MethodImplOptions.AggressiveInlining)]
			public static void ThrowSliceBufferTooSmall()
			{
				throw SliceBufferTooSmall();
			}

			[Pure, NotNull, MethodImpl(MethodImplOptions.NoInlining)]
			public static FormatException SliceBufferTooSmall()
			{
				return new FormatException("The specified slice is larger than its underlying buffer.");
			}

			[Pure, NotNull, MethodImpl(MethodImplOptions.NoInlining)]
			public static FormatException SliceInvalid()
			{
				return new FormatException("The specified slice is invalid.");
			}

			[ContractAnnotation("=>halt"), MethodImpl(MethodImplOptions.NoInlining)]
			public static T ThrowSliceTooLargeForConversion<T>(int size)
			{
				throw new FormatException($"Cannot convert slice to value of type {typeof(T).Name} because it is larger than {size} bytes.");
			}

			[ContractAnnotation("=>halt"), MethodImpl(MethodImplOptions.NoInlining)]
			public static T ThrowSliceSizeInvalidForConversion<T>(int size)
			{
				throw new FormatException($"Cannot convert slice of size {size} to value of type {typeof(T).Name}.");
			}

			[Pure, NotNull, MethodImpl(MethodImplOptions.NoInlining)]
			public static ArgumentException BufferOffsetNotNeg()
			{
				// ReSharper disable once NotResolvedInText
				return new ArgumentException("The specified segment has a negative offset, which is not legal. This may be a side effect of memory corruption.", "offset");
			}

			[Pure, NotNull, MethodImpl(MethodImplOptions.NoInlining)]
			public static ArgumentException BufferCountNotNeg()
			{
				// ReSharper disable once NotResolvedInText
				return new ArgumentException("The specified segment has a negative size, which is not legal. This may be a side effect of memory corruption.", "count");
			}

			[Pure, NotNull, MethodImpl(MethodImplOptions.NoInlining)]
			public static ArgumentException BufferArrayNotNull()
			{
				// ReSharper disable once NotResolvedInText
				return new ArgumentException("The specified segment is missing its underlying buffer.", "array");
			}

			[ContractAnnotation("=> halt"), MethodImpl(MethodImplOptions.AggressiveInlining)]
			public static void ThrowBufferArrayToSmall()
			{
				throw BufferArrayToSmall();
			}

			[Pure, NotNull, MethodImpl(MethodImplOptions.NoInlining)]
			public static ArgumentException BufferArrayToSmall()
			{
				// ReSharper disable once NotResolvedInText
				return new ArgumentException("The specified segment is larger than its underlying buffer.", "count");
			}

			[Pure, NotNull, MethodImpl(MethodImplOptions.NoInlining)]
			public static ArgumentException BufferInvalid()
			{
				// ReSharper disable once NotResolvedInText
				return new ArgumentException("The specified segment is invalid.");
			}

			[Pure, NotNull, MethodImpl(MethodImplOptions.NoInlining)]
			public static FormatException VarIntOverflow()
			{
				return new FormatException("Malformed Varint would overflow the expected range");
			}

			[Pure, NotNull, MethodImpl(MethodImplOptions.NoInlining)]
			public static FormatException VarIntTruncated()
			{
				return new FormatException("Malformed Varint seems to be truncated");
			}

			[Pure, NotNull, MethodImpl(MethodImplOptions.NoInlining)]
			public static FormatException VarBytesTruncated()
			{
				return new FormatException("Malformed VarBytes seems to be truncated");
			}

		}
	}

}

#endif
