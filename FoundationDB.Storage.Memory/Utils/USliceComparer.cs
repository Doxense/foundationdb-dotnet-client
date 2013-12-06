#region Copyright Doxense 2013
//
// All rights are reserved. Reproduction or transmission in whole or in part, in
// any form or by any means, electronic, mechanical or otherwise, is prohibited
// without the prior written consent of the copyright owner.
//
#endregion

namespace FoundationDB.Storage.Memory.Core
{
	using System;
	using System.Collections.Generic;

	/// <summary>Performs optimized equality and comparison checks on Slices</summary>
	public unsafe sealed class USliceComparer : IComparer<USlice>, IEqualityComparer<USlice>, IComparer<KeyValuePair<USlice, USlice>>
	{
		/// <summary>Default instance of the slice comparator</summary>
		public static readonly USliceComparer Default = new USliceComparer();

		private USliceComparer()
		{ }

		/// <summary>Lexicographically compare two slices and returns an indication of their relative sort order</summary>
		/// <param name="x">Slice compared with <paramref name="y"/></param>
		/// <param name="y">Slice compared with <paramref name="x"/></param>
		/// <returns>Returns a NEGATIVE value if <paramref name="x"/> is LESS THAN <paramref name="y"/>, ZERO if <paramref name="x"/> is EQUAL TO <paramref name="y"/>, and a POSITIVE value if <paramref name="x"/> is GREATER THAN <paramref name="y"/>.</returns>
		/// <remarks>If both <paramref name="x"/> and <paramref name="y"/> are nil or empty, the comparison will return ZERO. If only <paramref name="y"/> is nil or empty, it will return a NEGATIVE value. If only <paramref name="x"/> is nil or empty, it will return a POSITIVE value.</remarks>
		public int Compare(USlice x, USlice y)
		{
			return UnmanagedHelpers.CompareUnsafe(x.Data, x.Count, y.Data, y.Count);
		}

		/// <summary>Checks if two slices are equal.</summary>
		/// <param name="x">Slice compared with <paramref name="y"/></param>
		/// <param name="y">Slice compared with <paramref name="x"/></param>
		/// <returns>true if <paramref name="x"/> and <paramref name="y"/> have the same size and contain the same sequence of bytes; otherwise, false.</returns>
		public bool Equals(USlice x, USlice y)
		{
			return x.Count == y.Count && 0 == UnmanagedHelpers.CompareUnsafe(x.Data, x.Count, y.Data, y.Count);
		}

		/// <summary>Computes the hash code of a slice</summary>
		/// <param name="obj">A slice</param>
		/// <returns>A 32-bit signed hash coded calculated from all the bytes in the slice</returns>
		public int GetHashCode(USlice obj)
		{
			if (obj.Data == null) return 0;
			//return ComputeHashCode(obj.Array, obj.Offset, obj.Count);
			return 123; //TODO!
		}

		int IComparer<KeyValuePair<USlice, USlice>>.Compare(KeyValuePair<USlice, USlice> x, KeyValuePair<USlice, USlice> y)
		{
			return UnmanagedHelpers.CompareUnsafe(x.Key.Data, x.Key.Count, y.Key.Data, y.Key.Count);
		}
	}

}
