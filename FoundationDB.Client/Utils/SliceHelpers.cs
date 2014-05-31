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

#define USE_NATIVE_MEMORY_OPERATORS

namespace FoundationDB.Client
{
	using FoundationDB.Client.Utils;
	using JetBrains.Annotations;
	using System;
	using System.Runtime.CompilerServices;
	using System.Runtime.ConstrainedExecution;
	using System.Runtime.InteropServices;
	using System.Security;

	internal static class SliceHelpers
	{

		public static void EnsureSliceIsValid(ref Slice slice)
		{
			if (slice.Count == 0 && slice.Offset >= 0) return;
			if (slice.Count < 0 || slice.Offset < 0 || slice.Array == null || slice.Offset + slice.Count > slice.Array.Length)
			{
				ThrowMalformedSlice(slice);
			}
		}

		/// <summary>Reject an invalid slice by throw an error with the appropriate diagnostic message.</summary>
		/// <param name="slice">Slice that is being naugthy</param>
		[ContractAnnotation("=> halt")]
		public static void ThrowMalformedSlice(Slice slice)
		{
#if DEBUG
			// If you break here, that means that a slice is invalid (negative count, offset, ...), which may be a sign of memory corruption!
			// You should walk up the stack to see what is going on !
			if (System.Diagnostics.Debugger.IsAttached) System.Diagnostics.Debugger.Break();
#endif

			if (slice.Offset < 0) throw new FormatException("The specified slice has a negative offset, which is not legal. This may be a side effect of memory corruption.");
			if (slice.Count < 0) throw new FormatException("The specified slice has a negative size, which is not legal. This may be a side effect of memory corruption.");
			if (slice.Count > 0)
			{
				if (slice.Array == null) throw new FormatException("The specified slice is missing its underlying buffer.");
				if (slice.Offset + slice.Count > slice.Array.Length) throw new FormatException("The specified slice is larger than its underlying buffer.");
			}
			// maybe it's Lupus ?
			throw new FormatException("The specified slice is invalid.");
		}

		public static void EnsureBufferIsValid(byte[] array, int offset, int count)
		{
			if (count == 0 && offset >= 0) return;
			if (count < 0 || offset < 0 || array == null || offset + count > array.Length)
			{
				ThrowMalformedBuffer(array, offset, count);
			}
		}

		/// <summary>Reject an invalid slice by throw an error with the appropriate diagnostic message.</summary>
		[ContractAnnotation("=> halt")]
		public static void ThrowMalformedBuffer(byte[] array, int offset, int count)
		{
			if (offset < 0) throw new ArgumentException("The specified segment has a negative offset, which is not legal. This may be a side effect of memory corruption.", "offset");
			if (count < 0) throw new ArgumentException("The specified segment has a negative size, which is not legal. This may be a side effect of memory corruption.", "count");
			if (count > 0)
			{
				if (array == null) throw new ArgumentException("The specified segment is missing its underlying buffer.", "array");
				if (offset + count > array.Length) throw new ArgumentException("The specified segment is larger than its underlying buffer.", "count");
			}
			// maybe it's Lupus ?
			throw new ArgumentException("The specified segment is invalid.");
		}

		/// <summary>Round a size to a multiple of 16</summary>
		/// <param name="size">Minimum size required</param>
		/// <returns>Size rounded up to the next multiple of 16</returns>
		/// <exception cref="System.OverflowException">If the rounded size overflows over 2 GB</exception>
		public static int Align(int size)
		{
			const int ALIGNMENT = 16; // MUST BE A POWER OF TWO!
			const int MASK = (-ALIGNMENT) & int.MaxValue;

			if (size <= ALIGNMENT)
			{
				if (size < 0) throw new ArgumentOutOfRangeException("size", "Size cannot be negative");
				return ALIGNMENT;
			}
			// force an exception if we overflow above 2GB
			checked { return (size + (ALIGNMENT - 1)) & MASK; }
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

		/// <summary>Compute the hash code of a byte segment</summary>
		/// <param name="bytes">Buffer</param>
		/// <param name="offset">Offset of the start of the segment in the buffer</param>
		/// <param name="count">Number of bytes in the segment</param>
		/// <returns>A 32-bit signed hash code calculated from all the bytes in the segment.</returns>
		public static int ComputeHashCode([NotNull] byte[] bytes, int offset, int count)
		{
			if (bytes == null || offset < 0 || count < 0 || offset + count > bytes.Length) SliceHelpers.ThrowMalformedBuffer(bytes, offset, count);

			return ComputeHashCodeUnsafe(bytes, offset, count);
		}

		/// <summary>Compute the hash code of a byte segment, without validating the arguments</summary>
		/// <param name="bytes">Buffer</param>
		/// <param name="offset">Offset of the start of the segment in the buffer</param>
		/// <param name="count">Number of bytes in the segment</param>
		/// <returns>A 32-bit signed hash code calculated from all the bytes in the segment.</returns>
		public static int ComputeHashCodeUnsafe([NotNull] byte[] bytes, int offset, int count)
		{
			Contract.Requires(bytes != null && offset >= 0 && count >= 0);

			//TODO: use a better hash algorithm? (xxHash, CityHash, SipHash, ...?)
			// => will be called a lot when Slices are used as keys in an hash-based dictionary (like Dictionary<Slice, ...>)
			// => won't matter much for *ordered* dictionary that will probably use IComparer<T>.Compare(..) instead of the IEqalityComparer<T>.GetHashCode()/Equals() combo
			// => we don't need a cryptographic hash, just something fast and suitable for use with hashtables...
			// => probably best to select an algorithm that works on 32-bit or 64-bit chunks

			// <HACKHACK>: unoptimized 32 bits FNV-1a implementation
			uint h = 2166136261; // FNV1 32 bits offset basis
			int p = offset;
			int n = count;
			while (n-- > 0)
			{
				h = (h ^ bytes[p++]) * 16777619; // FNV1 32 prime
			}
			return (int)h;
			// </HACKHACK>
		}

		/// <summary>Compare two byte segments for equality</summary>
		/// <param name="left">Left buffer</param>
		/// <param name="leftOffset">Start offset in left buffer</param>
		/// <param name="right">Right buffer</param>
		/// <param name="rightOffset">Start offset in right buffer</param>
		/// <param name="count">Number of bytes to compare</param>
		/// <returns>true if all bytes are the same in both segments</returns>
		public static bool SameBytes(byte[] left, int leftOffset, byte[] right, int rightOffset, int count)
		{
			SliceHelpers.EnsureBufferIsValid(left, leftOffset, count);
			SliceHelpers.EnsureBufferIsValid(right, rightOffset, count);

			if (left == null || right == null) return left == right;
			return SameBytesUnsafe(left, leftOffset, right, rightOffset, count);
		}

		/// <summary>Compare two byte segments for equality, without validating the arguments</summary>
		/// <param name="left">Left buffer</param>
		/// <param name="leftOffset">Start offset in left buffer</param>
		/// <param name="right">Right buffer</param>
		/// <param name="rightOffset">Start offset in right buffer</param>
		/// <param name="count">Number of bytes to compare</param>
		/// <returns>true if all bytes are the same in both segments</returns>
		public static bool SameBytesUnsafe([NotNull] byte[] left, int leftOffset, [NotNull] byte[] right, int rightOffset, int count)
		{
			Contract.Requires(left != null && leftOffset >= 0 && right != null && rightOffset >= 0 && count >= 0);

			// for very small keys, the cost of pinning and marshalling may be too high
			if (count <= 8)
			{
				while(count--> 0)
				{
					if (left[leftOffset++] != right[rightOffset++]) return false;
				}
				return true;
			}

			if (object.ReferenceEquals(left, right))
			{ // In cases where the keys are backed by the same buffer, we don't need to pin the same buffer twice

				if (leftOffset == rightOffset)
				{ // same segment in the same buffer
					return true;
				}

				unsafe
				{
					fixed (byte* ptr = left)
					{
						return 0 == CompareMemoryUnsafe(ptr + leftOffset, ptr + rightOffset, count);
					}
				}
			}
			else
			{
				unsafe
				{
					fixed (byte* pLeft = left)
					fixed (byte* pRight = right)
					{
						return 0 == CompareMemoryUnsafe(pLeft + leftOffset, pRight + rightOffset, count);
					}
				}
			}
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
		/// * "AA" &lt; "B"</remarks>
		public static int CompareBytes(byte[] left, int leftOffset, int leftCount, byte[] right, int rightOffset, int rightCount)
		{
			SliceHelpers.EnsureBufferIsValid(left, leftOffset, leftCount);
			SliceHelpers.EnsureBufferIsValid(right, rightOffset, rightCount);

			return CompareBytesUnsafe(left, leftOffset, leftCount, right, rightOffset, rightCount);
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
		/// * "AA" &lt; "B"</remarks>
		public static int CompareBytesUnsafe([NotNull] byte[] left, int leftOffset, int leftCount, [NotNull] byte[] right, int rightOffset, int rightCount)
		{
			Contract.Requires(left != null && right != null && leftOffset >= 0 && leftCount >= 0 && rightOffset >= 0 && rightCount >= 0);

			if (object.ReferenceEquals(left, right))
			{ // In cases where the keys are backed by the same buffer, we don't need to pin the same buffer twice

				if (leftCount == rightCount && leftOffset == rightOffset)
				{ // same segment in the same buffer
					return 0;
				}

				unsafe
				{
					fixed (byte* ptr = left)
					{
						int n = CompareMemoryUnsafe(ptr + leftOffset, ptr + rightOffset, Math.Min(leftCount, rightCount));
						return n != 0 ? n : leftCount - rightCount;
					}
				}
			}
			else
			{
				unsafe
				{
					fixed (byte* pLeft = left)
					fixed (byte* pRight = right)
					{
						int n = CompareMemoryUnsafe(pLeft + leftOffset, pRight + rightOffset, Math.Min(leftCount, rightCount));
						return n != 0 ? n : leftCount - rightCount;
					}
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
		public static void CopyBytes(byte[] dst, int dstOffset, byte[] src, int srcOffset, int count)
		{
			SliceHelpers.EnsureBufferIsValid(dst, dstOffset, count);
			SliceHelpers.EnsureBufferIsValid(src, srcOffset, count);

			CopyBytesUnsafe(dst, dstOffset, src, srcOffset, count);
		}

		/// <summary>Copy the content of a byte segment into another, without validating the arguments. CAUTION: The arguments are NOT in the same order as Buffer.BlockCopy() or Array.Copy() !</summary>
		/// <param name="dst">Destination buffer</param>
		/// <param name="dstOffset">Offset in destination buffer</param>
		/// <param name="src">Source buffer</param>
		/// <param name="srcOffset">Offset in source buffer</param>
		/// <param name="count">Number of bytes to copy</param>
		/// <remarks>CAUTION: THE ARGUMENTS ARE REVERSED! They are in the same order as memcpy() and memmove(), with destination first, and source second!</remarks>
		public static void CopyBytesUnsafe([NotNull] byte[] dst, int dstOffset, [NotNull] byte[] src, int srcOffset, int count)
		{
			Contract.Requires(dst != null && src != null && dstOffset >= 0 && srcOffset >= 0 && count >= 0);

			if (count <= 8)
			{ // for very small keys, the cost of pinning and marshalling may be to high

				while(count-- > 0)
				{
					dst[dstOffset++] = src[srcOffset++];
				}
			}
			else if (object.ReferenceEquals(dst, src))
			{ // In cases where the keys are backed by the same buffer, we don't need to pin the same buffer twice

				unsafe
				{
					fixed (byte* ptr = dst)
					{
						MoveMemoryUnsafe(ptr + dstOffset, ptr + srcOffset, count);
					}
				}
			}
			else
			{
				unsafe
				{
					fixed (byte* pDst = dst)
					fixed (byte* pSrc = src)
					{
						MoveMemoryUnsafe(pDst + dstOffset, pSrc + srcOffset, count);
					}
				}
			}
		}

		/// <summary>Copy the content of a native byte segment into a managed segment, without validating the arguments.</summary>
		/// <param name="dst">Destination buffer</param>
		/// <param name="dstOffset">Offset in destination buffer</param>
		/// <param name="src">Point to the source buffer</param>
		/// <param name="count">Number of bytes to copy</param>
		/// <remarks>CAUTION: THE ARGUMENTS ARE REVERSED! They are in the same order as memcpy() and memmove(), with destination first, and source second!</remarks>
		public static unsafe void CopyBytesUnsafe([NotNull] byte[] dst, int dstOffset, byte* src, int count)
		{
			Contract.Requires(dst != null && src != null && dstOffset >= 0 && count >= 0);

			if (count <= 8)
			{
				while(count-- > 0)
				{
					dst[dstOffset++] = *src++;
				}
			}
			else
			{
				fixed(byte* ptr = dst)
				{
					MoveMemoryUnsafe(ptr + dstOffset, src, count);
				}
			}
		}

		/// <summary>Dangerously copy native memory from one location to another</summary>
		/// <param name="dest">Where to copy the bytes</param>
		/// <param name="src">Where to read the bytes</param>
		/// <param name="count">Number of bytes to copy</param>
		[SecurityCritical, ReliabilityContract(Consistency.WillNotCorruptState, Cer.Success)]
#if USE_NATIVE_MEMORY_OPERATORS && !NET_4_0
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
#endif
		private static unsafe void MoveMemoryUnsafe(byte* dest, byte* src, int count)
		{
			Contract.Requires(dest != null && src != null && count >= 0);

#if USE_NATIVE_MEMORY_OPERATORS
			NativeMethods.memmove(dest, src, new IntPtr(count));
#else
			if (count >= 16)
			{
				do
				{
					*((int*)(dest + 0)) = *((int*)(src + 0));
					*((int*)(dest + 4)) = *((int*)(src + 4));
					*((int*)(dest + 8)) = *((int*)(src + 8));
					*((int*)(dest + 12)) = *((int*)(src + 12));
					dest += 16;
					src += 16;
				}
				while ((count -= 16) >= 16);
			}
			if (count > 0)
			{
				if ((count & 8) != 0)
				{
					*((int*)(dest + 0)) = *((int*)(src + 0));
					*((int*)(dest + 4)) = *((int*)(src + 4));
					dest += 8;
					src += 8;
				}
				if ((count & 4) != 0)
				{
					*((int*)dest) = *((int*)src);
					dest += 4;
					src += 4;
				}
				if ((count & 2) != 0)
				{
					*((short*)dest) = *((short*)src);
					dest += 2;
					src += 2;
				}
				if ((count & 1) != 0)
				{
					*dest = *src;
				}
			}
#endif
		}

		/// <summary>Returns the offset of the first difference found between two buffers of the same size</summary>
		/// <param name="left">Pointer to the first byte of the left buffer</param>
		/// <param name="right">Pointer to the first byte of the right buffer</param>
		/// <param name="count">Number of bytes to compare in both buffers</param>
		/// <returns>Offset (from the first byte) of the first difference encountered, or -1 if both buffers are identical.</returns>
		[SecurityCritical, ReliabilityContract(Consistency.WillNotCorruptState, Cer.Success)]
#if USE_NATIVE_MEMORY_OPERATORS && !NET_4_0
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
#endif
		private static unsafe int CompareMemoryUnsafe(byte* left, byte* right, int count)
		{
			Contract.Requires(left != null && right != null && count >= 0);

#if USE_NATIVE_MEMORY_OPERATORS
			return NativeMethods.memcmp(left, right, new IntPtr(count));
#else

			// We want to scan in chunks of 8 bytes, until we find a difference (or there's less than 8 bytes remaining).
			// If we find a difference that way, we backtrack and then scan byte per byte to locate the location of the mismatch.
			// for the last 1 to 7 bytes, we just do a regular check

			// XOR Comparison: We XOR two 8-bytes chunks together.
			// - If all bytes are identical, the XOR result will be 0.
			// - If at least one bit is difference, the XOR result will be non-zero, and the first different will be in the first non-zero byte.

			// Identical data:
			//	left : "11 22 33 44 55 66 77 88" => 0x8877665544332211
			//	right: "11 22 33 44 55 66 77 88" => 0x8877665544332211
			//	left XOR right => 0x8877665544332211 ^ 0x8877665544332211 = 0

			// Different data:
			//	left : "11 22 33 44 55 66 77 88" => 0x8877665544332211
			//	right: "11 22 33 44 55 AA BB CC" => 0xCCBBAA5544332211
			//	left XOR right =0x8877665544332211 ^ 0xCCBBAA5544332211 = 0x44CCCC0000000000
			//  the first non-zero byte is at offset 5 (big-endian) with the value of 0xCC

			byte* start = left;

			//TODO: align the start of the 8-byte scan to an 8-byte aligne memory address ?

			// compares using 8-bytes chunks
			while (count >= 8)
			{
				ulong k = *((ulong*)left) ^ *((ulong*)right);

				if (k != 0)
				{ // there is difference in these 8 bytes, iterate until we find it
					int p = 0;
					while ((k & 0xFF) == 0)
					{
						++p;
						k >>= 8;
					}
					return left[p] - right[p];
				}
				left += 8;
				right += 8;
				count -= 8;
			}

			// if more than 4 bytes remain, check 32 bits at a time
			if (count >= 4)
			{
				if (*((uint*)left) != *((uint*)right))
				{
					goto compare_tail;
				}
				left += 4;
				right += 4;
				count -= 4;
			}

			// from here, there is at mos 3 bytes remaining

		compare_tail:
			while (count-- > 0)
			{
				int n = *(left++) - *(right++);
				if (n != 0) return n;
			}
			return 0;
#endif
		}

#if USE_NATIVE_MEMORY_OPERATORS

		[SuppressUnmanagedCodeSecurity]
		internal static unsafe class NativeMethods
		{

			/// <summary>Compare characters in two buffers.</summary>
			/// <param name="buf1">First buffer.</param>
			/// <param name="buf2">Second buffer.</param>
			/// <param name="count">Number of bytes to compare.</param>
			/// <returns>The return value indicates the relationship between the buffers.</returns>
			[DllImport("msvcrt.dll", CallingConvention = CallingConvention.Cdecl, SetLastError = false)]
			public static extern int memcmp(byte* buf1, byte* buf2, IntPtr count);

			/// <summary>Moves one buffer to another.</summary>
			/// <param name="dest">Destination object.</param>
			/// <param name="src">Source object.</param>
			/// <param name="count">Number of bytes to copy.</param>
			/// <returns>The value of dest.</returns>
			/// <remarks>Copies count bytes from src to dest. If some regions of the source area and the destination overlap, both functions ensure that the original source bytes in the overlapping region are copied before being overwritten.</remarks>
			[DllImport("msvcrt.dll", CallingConvention = CallingConvention.Cdecl, SetLastError = false)]
			public static extern byte* memmove(byte* dest, byte* src, IntPtr count);

		}

#endif

	}

}
