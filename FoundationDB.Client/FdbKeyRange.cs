﻿#region BSD Licence
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

	/// <summary>Represents a pair of keys defining the range 'Begin &lt;= key &gt; End'</summary>
	[DebuggerDisplay("Begin={Begin}, End={End}")]
	public struct FdbKeyRange
	{
		/// <summary>Returns an empty pair of keys</summary>
		public static FdbKeyRange Empty { get { return default(FdbKeyRange); } }

		/// <summary>Start of the range</summary>
		public readonly Slice Begin;

		/// <summary>End of the range</summary>
		public readonly Slice End;

		/// <summary>
		/// Create a new range of keys
		/// </summary>
		/// <param name="begin">Start of range (usually included)</param>
		/// <param name="end">End of range (usually excluded)</param>
		public FdbKeyRange(Slice begin, Slice end)
		{
			this.Begin = begin;
			this.End = end;
		}

		/// <summary>Create a range that will return all keys starting with <paramref name="prefix"/>: ('prefix' &lt;= k &lt; strinc('prefix'))</summary>
		/// <param name="prefix"></param>
		/// <returns></returns>
		public static FdbKeyRange StartsWith(Slice prefix)
		{
			if (prefix.IsNull) throw Fdb.Errors.KeyCannotBeNull("prefix");

			// prefix => [ prefix, prefix + 1 )
			return new FdbKeyRange(
				prefix,
				FdbKey.Increment(prefix)
			);
		}

		/// <summary>Create a range that selects all keys starting with <paramref name="prefix"/>, but not the prefix itself: ('prefix\x00' &lt;= k &lt; string('prefix')</summary>
		/// <param name="prefix">Key prefix (that will be excluded from the range)</param>
		/// <returns>Range including all keys with the specified prefix.</returns>
		public static FdbKeyRange PrefixedBy(Slice prefix)
		{
			if (prefix.IsNull) throw Fdb.Errors.KeyCannotBeNull("prefix");

			// prefix => [ prefix."\0", prefix + 1)
			return new FdbKeyRange(
				prefix + FdbKey.MinValue,
				FdbKey.Increment(prefix)
			);
		}

		/// <summary>Create a range that will only return <paramref name="key"/> itself ('key' &lt;= k &lt; 'key\x00')</summary>
		/// <param name="key">Key that will be returned by the range</param>
		/// <returns>Range that only return the specified key.</returns>
		public static FdbKeyRange FromKey(Slice key)
		{
			if (key.IsNull) throw Fdb.Errors.KeyCannotBeNull();

			if (key.Count == 0)
			{ // "" => [ "", "\x00" )
				return new FdbKeyRange(Slice.Empty, FdbKey.MinValue);
			}
			// key => [ key, key + '\0' )
			return new FdbKeyRange(
				key,
				key + FdbKey.MinValue
			);
		}

		/// <summary>Returns true, if the key is contained in the range</summary>
		/// <param name="key"></param>
		/// <returns></returns>
		public bool Contains(Slice key)
		{
			return key.CompareTo(this.Begin) >= 0 && key.CompareTo(this.End) < 0;
		}

		/// <summary>Test if <paramref name="key"/> is contained inside the range</summary>
		/// <param name="key">Key that will be compared with the the range's bounds</param>
		/// <param name="endIncluded">If true, the End bound is inclusive, otherwise it is exclusive</param>
		/// <returns>-1 if key is less than the lower bound of the range (<paramref name="key"/> &lt; Begin), +1 if the key is greater or equal to the higher bound of the range (<paramref name="key"/> &gt;= End) or 0 if it is inside the range (Begin &lt;= <paramref name="key"/> &lt; End)</returns>
		public int Test(Slice key, bool endIncluded = false)
		{
			// note: if the range is empty (Begin = End = Slice.Empty) then it should return 0

			if (this.Begin.IsPresent && key.CompareTo(this.Begin) < 0) return -1;
			if (this.End.IsPresent && key.CompareTo(this.End) >= (endIncluded ? 1 : 0)) return +1;
			return 0;
		}

		/// <summary>Returns a printable version of the range</summary>
		public override string ToString()
		{
			return "{\"" + this.Begin.ToString() + "\", \"" + this.End.ToString() + "}";
		}
	
	}

}
