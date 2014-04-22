#region Copyright (c) 2013-2014, Doxense SAS. All rights reserved.
// See License.MD for license information
#endregion

namespace FoundationDB.Storage.Memory.Core
{
	using System;
	using System.Collections.Generic;

	internal sealed class SequenceComparer : IComparer<ulong>, IEqualityComparer<ulong>
	{
		public static readonly SequenceComparer Default = new SequenceComparer();

		private SequenceComparer()
		{ }

		public int Compare(ulong x, ulong y)
		{
			if (x < y) return -1;
			if (x > y) return +1;
			return 0;
		}

		public bool Equals(ulong x, ulong y)
		{
			return x == y;
		}

		public int GetHashCode(ulong x)
		{
			return (((int)x) ^ ((int)(x >> 32)));
		}
	}

}
