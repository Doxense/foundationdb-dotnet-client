#region Copyright (c) 2013-2014, Doxense SAS. All rights reserved.
// See License.MD for license information
#endregion

#define INSTRUMENT

namespace FoundationDB.Storage.Memory.Core
{
	using FoundationDB.Storage.Memory.Utils;
	using System;
	using System.Collections.Generic;
	using System.Diagnostics.Contracts;

	internal unsafe sealed class NativeKeyComparer : IComparer<IntPtr>, IEqualityComparer<IntPtr>
	{

		public int Compare(IntPtr left, IntPtr right)
		{
#if INSTRUMENT
			System.Threading.Interlocked.Increment(ref s_compareCalls);
#endif
			// this method will be called A LOT, so it should be as fast as possible...
			// We know that:
			// - caller should never compare nulls (it's a bug)
			// - empty keys can exist
			// - number of calls with left == right will be very small so may not be worth it to optimize (will slow down everything else)
			// - for db using the DirectoryLayer, almost all keys will start with 0x15 (prefix for an int in a tuple) so checking the first couple of bytes will not help much (long runs of keys starting with the same 2 or 3 bytes)
			Contract.Assert(left != IntPtr.Zero && right != IntPtr.Zero);

			// unwrap as pointers to the Key struct
			var leftKey = (Key*)left;
			var rightKey = (Key*)right;

			// these will probably cause a cache miss
			uint leftCount = leftKey->Size;
			uint rightCount = rightKey->Size;

			// but then memcmp will probably have the data in the cpu cache...
			int c = UnmanagedHelpers.NativeMethods.memcmp(
				&(leftKey->Data),
				&(rightKey->Data),
				new UIntPtr(leftCount < rightCount ? leftCount : rightCount)
			);
			return c != 0 ? c : (int)leftCount - (int)rightCount;
		}

		public bool Equals(IntPtr left, IntPtr right)
		{
#if INSTRUMENT
			System.Threading.Interlocked.Increment(ref s_equalsCalls);
#endif
			// unwrap as pointers to the Key struct
			var leftKey = (Key*)left;
			var rightKey = (Key*)right;

			if (leftKey->HashCode != rightKey->HashCode)
			{
				return false;
			}

			uint leftCount, rightCount;

			if (leftKey == null || (leftCount = leftKey->Size) == 0) return rightKey == null || rightKey->Size == 0;
			if (rightKey == null || (rightCount = rightKey->Size) == 0) return false;

			return leftCount == rightCount && 0 == UnmanagedHelpers.NativeMethods.memcmp(&(leftKey->Data), &(rightKey->Data), new UIntPtr(leftCount));
		}

		public int GetHashCode(IntPtr value)
		{
#if INSTRUMENT
			System.Threading.Interlocked.Increment(ref s_getHashCodeCalls);
#endif
			var key = (Key*)value.ToPointer();
			if (key == null) return -1;
			return key->HashCode;
		}

#if INSTRUMENT
		private static long s_compareCalls;
		private static long s_equalsCalls;
		private static long s_getHashCodeCalls;
#endif

		public static void GetCounters(out long compare, out long equals, out long getHashCode)
		{
#if INSTRUMENT
			compare = System.Threading.Interlocked.Read(ref s_compareCalls);
			equals = System.Threading.Interlocked.Read(ref s_equalsCalls);
			getHashCode = System.Threading.Interlocked.Read(ref s_getHashCodeCalls);
#else
			compare = 0;
			equals = 0;
			getHashCode = 0;
#endif
		}

		public static void ResetCounters()
		{
#if INSTRUMENT
			System.Threading.Interlocked.Exchange(ref s_compareCalls, 0);
			System.Threading.Interlocked.Exchange(ref s_equalsCalls, 0);
			System.Threading.Interlocked.Exchange(ref s_getHashCodeCalls, 0);
#endif
		}

	}

}
