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

//#define MEASURE

namespace FoundationDB.Client
{
	using FoundationDB.Client.Utils;
	using JetBrains.Annotations;
	using System;
	using System.Runtime.CompilerServices;
	using System.Runtime.InteropServices;
	using System.Security;

	internal static class SliceHelpers
	{

#if !NET_4_0
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
#endif
		public static void EnsureSliceIsValid(ref Slice slice)
		{
			// this method is used everywhere, and is consistently the top 1 method by callcount when using a profiler,
			// so we must make sure that it gets inline whenever possible.

			if (slice.Count == 0 && slice.Offset >= 0) return;
			if (slice.Count < 0 || slice.Offset < 0 || slice.Array == null || slice.Offset + slice.Count > slice.Array.Length)
			{
				ThrowMalformedSlice(slice);
			}
		}

		/// <summary>Reject an invalid slice by throw an error with the appropriate diagnostic message.</summary>
		/// <param name="slice">Slice that is being naugthy</param>
		[ContractAnnotation("=> halt")]
		private static void ThrowMalformedSlice(Slice slice)
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

#if !NET_4_0
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
#endif
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
		private static void ThrowMalformedBuffer(byte[] array, int offset, int count)
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

#if MEASURE
		public static int[] CompareHistogram = new int[65536];
		public static double[] CompareDurations = new double[65536];
#endif

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

#if MEASURE
			int n = count;
			if (n < SliceHelpers.CompareHistogram.Length) ++SliceHelpers.CompareHistogram[n];
			var sw = System.Diagnostics.Stopwatch.StartNew();
			try {
#endif
			if (count == 0) return true;
			unsafe
			{
				fixed (byte* pLeft = &left[leftOffset])
				fixed (byte* pRight = &right[rightOffset])
				{
					return 0 == NativeMethods.memcmp(pLeft, pRight, new IntPtr(count));
				}
			}
#if MEASURE
			}
			finally
			{
				sw.Stop();
				if (n < SliceHelpers.CompareDurations.Length) SliceHelpers.CompareDurations[n] += (sw.Elapsed.TotalMilliseconds * 1E6);
			}
#endif
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

			int count = Math.Min(leftCount, rightCount);
#if MEASURE
			int n = count;
			if (n < SliceHelpers.CompareHistogram.Length) ++SliceHelpers.CompareHistogram[n];
			var sw = System.Diagnostics.Stopwatch.StartNew();
			try {
#endif
			if (count > 0)
			{
				unsafe
				{
					fixed (byte* pLeft = &left[leftOffset])
					fixed (byte* pRight = &right[rightOffset])
					{
						int c = NativeMethods.memcmp(pLeft, pRight, new IntPtr(count));
						if (c != 0) return c;
					}
				}
			}
			return leftCount - rightCount;
#if MEASURE
			}
			finally
			{
				sw.Stop();
				if (n < SliceHelpers.CompareDurations.Length) SliceHelpers.CompareDurations[n] += (sw.Elapsed.TotalMilliseconds * 1E6);
			}
#endif
		}

#if MEASURE
		public static int[] CopyHistogram = new int[65536];
		public static double[] CopyDurations = new double[65536];
#endif

		/// <summary>Copy the content of a byte segment into another, without validating the arguments. CAUTION: The arguments are NOT in the same order as Buffer.BlockCopy() or Array.Copy() !</summary>
		/// <param name="dst">Destination buffer</param>
		/// <param name="dstOffset">Offset in destination buffer</param>
		/// <param name="src">Source buffer</param>
		/// <param name="srcOffset">Offset in source buffer</param>
		/// <param name="count">Number of bytes to copy</param>
		/// <remarks>CAUTION: THE ARGUMENTS ARE REVERSED! They are in the same order as memcpy() and memmove(), with destination first, and source second!</remarks>
		public static unsafe void CopyBytesUnsafe([NotNull] byte[] dst, int dstOffset, [NotNull] byte[] src, int srcOffset, int count)
		{
			Contract.Requires(dst != null && src != null && dstOffset >= 0 && srcOffset >= 0 && count >= 0);

#if MEASURE
			int n = count;
			if (n < SliceHelpers.CopyHistogram.Length) ++SliceHelpers.CopyHistogram[n];
			var sw = System.Diagnostics.Stopwatch.StartNew();
#endif
			if (count > 0)
			{
				fixed (byte* pDst = &dst[dstOffset])
				fixed (byte* pSrc = &src[srcOffset])
				{
					NativeMethods.memmove(pDst, pSrc, new IntPtr(count));
				}
			}
#if MEASURE
			sw.Stop();
			if (n < SliceHelpers.CopyDurations.Length) SliceHelpers.CopyDurations[n] += (sw.Elapsed.TotalMilliseconds * 1E6);
#endif
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

#if MEASURE
			int n = count;
			if (n < SliceHelpers.CopyHistogram.Length) ++SliceHelpers.CopyHistogram[n];
			var sw = System.Diagnostics.Stopwatch.StartNew();
#endif
			if (count > 0)
			{
				fixed (byte* pDst = &dst[dstOffset])
				{
					NativeMethods.memmove(pDst, src, new IntPtr(count));
				}
			}
#if MEASURE
			sw.Stop();
			if (n < SliceHelpers.CopyDurations.Length) SliceHelpers.CopyDurations[n] += (sw.Elapsed.TotalMilliseconds * 1E6);
#endif
		}

		/// <summary>Fill the content of a managed segment with the same byte repeated</summary>
		public static void SetBytes(byte[] bytes, int offset, int count, byte value)
		{
			SliceHelpers.EnsureBufferIsValid(bytes, offset, count);

			if (count > 0)
			{
				unsafe
				{
					fixed (byte* ptr = &bytes[offset])
					{
						NativeMethods.memset(ptr, value, new IntPtr(count));
					}
				}
			}
		}

		/// <summary>Fill the content of a native byte segment with the same byte repeated</summary>
		public static unsafe void SetBytes(byte* bytes, int count, byte value)
		{
			if (bytes == null) throw new ArgumentNullException("bytes");
			if (count < 0) throw new ArgumentException("Count cannot be a negative number.", "count");

			NativeMethods.memset(bytes, value, new IntPtr(count));
		}

		[SuppressUnmanagedCodeSecurity]
		private static unsafe class NativeMethods
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

			/// <summary>Sets the first <paramref name="count"/> bytes of <paramref name="dest"/> to the byte <paramref name="c"/>.</summary>
			/// <param name="dest">Pointer to destination</param>
			/// <param name="c">Byte to set</param>
			/// <param name="count">Number of bytes</param>
			/// <returns>The value of <paramref name="dest"/></returns>
			[DllImport("msvcrt.dll", CallingConvention = CallingConvention.Cdecl, SetLastError = false)]
			public static extern byte* memset(byte* dest, int c, IntPtr count);

		}

	}

}
