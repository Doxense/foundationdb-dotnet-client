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

namespace FoundationDB.Client
{
	using FoundationDB.Client.Utils;
	using System;
	using System.Collections.Generic;
	using System.Runtime.ConstrainedExecution;
	using System.Runtime.InteropServices;
	using System.Security;

	/// <summary>Performs optimized equality and comparison checks on Slices</summary>
	public sealed class SliceComparer : IComparer<Slice>, IEqualityComparer<Slice>
	{
		/// <summary>Default instance of the slice comparator</summary>
		public static readonly SliceComparer Default = new SliceComparer();

		private SliceComparer()
		{ }

		/// <summary>Lexicographically compare two slices and returns an indication of their relative sort order</summary>
		/// <param name="x">Slice compared with <paramref name="y"/></param>
		/// <param name="y">Slice compared with <paramref name="x"/></param>
		/// <returns>Returns a NEGATIVE value if <paramref name="x"/> is LESS THAN <paramref name="y"/>, ZERO if <paramref name="x"/> is EQUAL TO <paramref name="y"/>, and a POSITIVE value if <paramref name="x"/> is GREATER THAN <paramref name="y"/>.</returns>
		/// <remarks>If both <paramref name="x"/> and <paramref name="y"/> are nil or empty, the comparison will return ZERO. If only <paramref name="y"/> is nil or empty, it will return a NEGATIVE value. If only <paramref name="x"/> is nil or empty, it will return a POSITIVE value.</remarks>
		public int Compare(Slice x, Slice y)
		{
			if (x.Count == 0) return y.Count == 0 ? 0 : -1;
			if (y.Count == 0) return +1;
			return SliceHelpers.CompareBytes(x.Array, x.Offset, x.Count, y.Array, y.Offset, y.Count);
		}

		/// <summary>Checks if two slices are equal.</summary>
		/// <param name="x">Slice compared with <paramref name="y"/></param>
		/// <param name="y">Slice compared with <paramref name="x"/></param>
		/// <returns>true if <paramref name="x"/> and <paramref name="y"/> have the same size and contain the same sequence of bytes; otherwise, false.</returns>
		public bool Equals(Slice x, Slice y)
		{
			return x.Count == y.Count && SliceHelpers.SameBytes(x.Array, x.Offset, x.Array, x.Offset, y.Count);
		}

		/// <summary>Computes the hash code of a slice</summary>
		/// <param name="obj">A slice</param>
		/// <returns>A 32-bit signed hash coded calculated from all the bytes in the slice</returns>
		public int GetHashCode(Slice obj)
		{
			if (obj.Array == null) return 0;
			return SliceHelpers.ComputeHashCode(obj.Array, obj.Offset, obj.Count);
		}

	}

}
