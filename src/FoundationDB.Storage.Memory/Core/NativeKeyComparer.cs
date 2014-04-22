﻿#region Copyright (c) 2013-2014, Doxense SAS. All rights reserved.
// See License.MD for license information
#endregion

namespace FoundationDB.Storage.Memory.Core
{
	using FoundationDB.Storage.Memory.Utils;
	using System;
	using System.Collections.Generic;
	using System.Diagnostics.Contracts;

	internal unsafe sealed class NativeKeyComparer : IComparer<IntPtr>, IEqualityComparer<IntPtr>
	{
		// Keys:
		// * KEY_SIZE		UInt16		Size of the key
		// * KEY_FLAGS		UInt16		Misc flags
		// * VALUE_PTR		IntPtr		Pointer to the head of the value chain for this key
		// * KEY_BYTES		variable	Content of the key
		// * (padding)		variable	padding to align the size to 4 or 8 bytes

		public int Compare(IntPtr left, IntPtr right)
		{
			// this method will be called A LOT, so it should be as fast as possible...
			// We know that:
			// - caller should never compare nulls (it's a bug)
			// - empty keys do not exist

			Contract.Assert(left != IntPtr.Zero && right != IntPtr.Zero);

			// unwrap as pointers to the Key struct
			var leftKey = (Key*)left.ToPointer();
			var rightKey = (Key*)right.ToPointer();


			// these will probably cause a cache miss
			uint leftCount = leftKey->Size;
			uint rightCount = rightKey->Size;

			Contract.Assert(leftCount > 0 && rightCount > 0);

			// but then memcmp will probably have the data in the cpu cache...
			int c = UnmanagedHelpers.NativeMethods.memcmp(&(leftKey->Data), &(rightKey->Data), new UIntPtr(leftCount < rightCount ? leftCount : rightCount));
			if (c == 0) c = (int)leftCount - (int)rightCount;
			return c;
		}

		public bool Equals(IntPtr left, IntPtr right)
		{
			// unwrap as pointers to the Key struct
			var leftKey = (Key*)left.ToPointer();
			var rightKey = (Key*)right.ToPointer();

			uint leftCount, rightCount;

			if (leftKey == null || (leftCount = leftKey->Size) == 0) return rightKey == null || rightKey->Size == 0;
			if (rightKey == null || (rightCount = rightKey->Size) == 0) return false;

			return leftCount == rightCount && 0 == UnmanagedHelpers.NativeMethods.memcmp(&(leftKey->Data), &(rightKey->Data), new UIntPtr(leftCount));
		}

		public int GetHashCode(IntPtr value)
		{
			var key = (Key*)value.ToPointer();
			if (key == null) return -1;
			uint size = key->Size;
			if (size == 0) return 0;

			//TODO: use a better hash algorithm? (xxHash, CityHash, SipHash, ...?)
			// => will be called a lot when Slices are used as keys in an hash-based dictionary (like Dictionary<Slice, ...>)
			// => won't matter much for *ordered* dictionary that will probably use IComparer<T>.Compare(..) instead of the IEqalityComparer<T>.GetHashCode()/Equals() combo
			// => we don't need a cryptographic hash, just something fast and suitable for use with hashtables...
			// => probably best to select an algorithm that works on 32-bit or 64-bit chunks

			// <HACKHACK>: unoptimized 32 bits FNV-1a implementation
			uint h = 2166136261; // FNV1 32 bits offset basis
			byte* bytes = &(key->Data);
			while (size-- > 0)
			{
				h = (h ^ *bytes++) * 16777619; // FNV1 32 prime
			}
			return (int)h;
			// </HACKHACK>
		}
	}

}
