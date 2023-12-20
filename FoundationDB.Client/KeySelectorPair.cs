#region Copyright (c) 2023 SnowBank SAS, (c) 2005-2023 Doxense SAS
// All rights reserved.
// 
// Redistribution and use in source and binary forms, with or without
// modification, are permitted provided that the following conditions are met:
// 	* Redistributions of source code must retain the above copyright
// 	  notice, this list of conditions and the following disclaimer.
// 	* Redistributions in binary form must reproduce the above copyright
// 	  notice, this list of conditions and the following disclaimer in the
// 	  documentation and/or other materials provided with the distribution.
// 	* Neither the name of SnowBank nor the
// 	  names of its contributors may be used to endorse or promote products
// 	  derived from this software without specific prior written permission.
// 
// THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS "AS IS" AND
// ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED
// WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE
// DISCLAIMED. IN NO EVENT SHALL SNOWBANK SAS BE LIABLE FOR ANY
// DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES
// (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES;
// LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND
// ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT
// (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS
// SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
#endregion

namespace FoundationDB.Client
{
	using System;
	using System.Diagnostics;

	/// <summary>Represents of pair of key selectors that range 'GetKey(Begin) &lt;= key &lt; GetKey(End)'</summary>
	[DebuggerDisplay("[ToString()]")]
	public readonly struct KeySelectorPair
	{
		/// <summary>Start of the range</summary>
		public readonly KeySelector Begin;

		/// <summary>End of the range</summary>
		public readonly KeySelector End;

		/// <summary>Create a new pair of key selectors</summary>
		/// <param name="beginInclusive">Selector for key from which to start iterating</param>
		/// <param name="endExclusive">Selector for key where to stop iterating</param>
		public KeySelectorPair(KeySelector beginInclusive, KeySelector endExclusive)
		{
			this.Begin = beginInclusive;
			this.End = endExclusive;
		}

		/// <summary>Factory method for a pair of key selectors</summary>
		public static KeySelectorPair Create(KeySelector beginInclusive, KeySelector endExclusive)
		{
			return new KeySelectorPair(
				beginInclusive, 
				endExclusive
			);
		}

		/// <summary>Create a new pair of key selectors using FIRST_GREATER_OR_EQUAL on both keys</summary>
		public static KeySelectorPair Create(Slice begin, Slice end)
		{
			return new KeySelectorPair(
				KeySelector.FirstGreaterOrEqual(begin),
				KeySelector.FirstGreaterOrEqual(end)
			);
		}

		/// <summary>Create a new pair of key selectors using FIRST_GREATER_OR_EQUAL on both keys</summary>
		public static KeySelectorPair Create(KeyRange range)
		{
			return new KeySelectorPair(
				KeySelector.FirstGreaterOrEqual(range.Begin),
				KeySelector.FirstGreaterOrEqual(range.End)
			);
		}

		/// <summary>Create a new pair of key selectors that will select all the keys that start with the specified prefix</summary>
		public static KeySelectorPair StartsWith(Slice prefix)
		{
			var range = KeyRange.StartsWith(prefix);

			return new KeySelectorPair(
				KeySelector.FirstGreaterOrEqual(range.Begin),
				KeySelector.FirstGreaterOrEqual(range.End)
			);
		}

		/// <summary>Returns a printable version of the pair of key selectors</summary>
		public override string ToString()
		{
			return $"[ {this.Begin.PrettyPrint(FdbKey.PrettyPrintMode.Begin)}, {this.End.PrettyPrint(FdbKey.PrettyPrintMode.End)} )";
		}

	}

}
