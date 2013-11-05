#region BSD Licence
/* Copyright (c) 2013, Doxense SARL
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
	using System;
	using System.Diagnostics;

	/// <summary>Represents of pair of key selectors that range 'GetKey(Begin) &lt;= key &lt; GetKey(End)'</summary>
	[DebuggerDisplay("[ToString()]")]
	public struct FdbKeySelectorPair
	{
		/// <summary>Start of the range</summary>
		public readonly FdbKeySelector Begin;

		/// <summary>End of the range</summary>
		public readonly FdbKeySelector End;

		/// <summary>Create a new pair of key selectors</summary>
		/// <param name="beginInclusive">Selector for key from which to start iterating</param>
		/// <param name="endExclusive">Selector for key where to stop iterating</param>
		public FdbKeySelectorPair(FdbKeySelector beginInclusive, FdbKeySelector endExclusive)
		{
			this.Begin = beginInclusive;
			this.End = endExclusive;
		}

		/// <summary>Factory method for a pair of key selectors</summary>
		public static FdbKeySelectorPair Create(FdbKeySelector beginInclusive, FdbKeySelector endExclusive)
		{
			return new FdbKeySelectorPair(
				beginInclusive, 
				endExclusive
			);
		}

		/// <summary>Create a new pair of key selectors using FIRST_GREATER_OR_EQUAL on both keys</summary>
		public static FdbKeySelectorPair Create(Slice begin, Slice end)
		{
			return new FdbKeySelectorPair(
				FdbKeySelector.FirstGreaterOrEqual(begin),
				FdbKeySelector.FirstGreaterOrEqual(end)
			);
		}

		/// <summary>Create a new pair of key selectors using FIRST_GREATER_OR_EQUAL on both keys</summary>
		public static FdbKeySelectorPair Create<TKey>(TKey begin, TKey end)
			where TKey : IFdbKey
		{
			if (begin == null) throw new ArgumentNullException("begin");
			if (end == null) throw new ArgumentNullException("end");
			return new FdbKeySelectorPair(
				FdbKeySelector.FirstGreaterOrEqual(begin.ToFoundationDbKey()),
				FdbKeySelector.FirstGreaterOrEqual(end.ToFoundationDbKey())
			);
		}

		/// <summary>Create a new pair of key selectors using FIRST_GREATER_OR_EQUAL on both keys</summary>
		public static FdbKeySelectorPair Create(FdbKeyRange range)
		{
			return new FdbKeySelectorPair(
				FdbKeySelector.FirstGreaterOrEqual(range.Begin),
				FdbKeySelector.FirstGreaterOrEqual(range.End)
			);
		}

		/// <summary>Create a new pair of key selectors that will select all the keys that start with the specified prefix</summary>
		public static FdbKeySelectorPair StartsWith(Slice prefix)
		{
			var range = FdbKeyRange.StartsWith(prefix);

			return new FdbKeySelectorPair(
				FdbKeySelector.FirstGreaterOrEqual(range.Begin),
				FdbKeySelector.FirstGreaterOrEqual(range.End)
			);
		}

		/// <summary>Create a new pair of key selectors that will select all the keys that start with the specified prefix</summary>
		public static FdbKeySelectorPair StartsWith<TKey>(TKey prefix)
			where TKey : IFdbKey
		{
			if (prefix == null) throw new ArgumentNullException("prefix");
			return StartsWith(prefix.ToFoundationDbKey());
		}

		/// <summary>Returns a printable version of the pair of key selectors</summary>
		public override string ToString()
		{
			return "[ " + this.Begin.ToString() + ", " + this.End.ToString() + " )";
		}

	}

}
