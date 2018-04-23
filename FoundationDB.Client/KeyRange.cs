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
	using Doxense.Diagnostics.Contracts;

	/// <summary>Represents a pair of keys defining the range 'Begin &lt;= key &gt; End'</summary>
	[DebuggerDisplay("Begin={Begin}, End={End}")]
	public struct KeyRange : IEquatable<KeyRange>, IComparable<KeyRange>
	{
		/// <summary>Returns an empty pair of keys</summary>
		public static KeyRange Empty => default(KeyRange);

		/// <summary>Returns a range that contains all the keys in the database</summary>
		public static KeyRange All => new KeyRange(FdbKey.MinValue, FdbKey.MaxValue);

		/// <summary>Start of the range</summary>
		public Slice Begin { get { return m_begin; } }
		private Slice m_begin; //PERF: readonly struct

		/// <summary>End of the range</summary>
		public Slice End { get { return m_end; } }
		private Slice m_end; //PERF: readonly struct

		/// <summary>
		/// Create a new range of keys
		/// </summary>
		/// <param name="begin">Start of range (usually included)</param>
		/// <param name="end">End of range (usually excluded)</param>
		public KeyRange(Slice begin, Slice end)
		{
			m_begin = begin;
			m_end = end;

			Contract.Ensures(m_begin <= m_end, "The range is inverted");
		}

		public static KeyRange Create(Slice a, Slice b)
		{
			return new KeyRange(a, b);
		}

		/// <summary>Create a range that will return all keys starting with <paramref name="prefix"/>: ('prefix' &lt;= k &lt; strinc('prefix'))</summary>
		/// <param name="prefix"></param>
		/// <returns></returns>
		public static KeyRange StartsWith(Slice prefix)
		{
			if (prefix.IsNull) throw Fdb.Errors.KeyCannotBeNull("prefix");
			if (prefix.Count == 0) return new KeyRange(Slice.Empty, FdbKey.MaxValue);


			// prefix => [ prefix, prefix + 1 )
			return new KeyRange(
				prefix,
				FdbKey.Increment(prefix)
			);
		}

		/// <summary>Create a range that selects all keys starting with <paramref name="prefix"/>, but not the prefix itself: ('prefix\x00' &lt;= k &lt; string('prefix')</summary>
		/// <param name="prefix">Key prefix (that will be excluded from the range)</param>
		/// <returns>Range including all keys with the specified prefix.</returns>
		public static KeyRange PrefixedBy(Slice prefix)
		{
			if (prefix.IsNull) throw Fdb.Errors.KeyCannotBeNull("prefix");

			// prefix => [ prefix."\0", prefix + 1)
			return new KeyRange(
				prefix + FdbKey.MinValue,
				FdbKey.Increment(prefix)
			);
		}

		/// <summary>Create a range that will only return <paramref name="key"/> itself ('key' &lt;= k &lt; 'key\x00')</summary>
		/// <param name="key">Key that will be returned by the range</param>
		/// <returns>Range that only return the specified key.</returns>
		public static KeyRange FromKey(Slice key)
		{
			if (key.IsNull) throw Fdb.Errors.KeyCannotBeNull();

			if (key.Count == 0)
			{ // "" => [ "", "\x00" )
				return new KeyRange(Slice.Empty, FdbKey.MinValue);
			}
			// key => [ key, key + '\0' )
			return new KeyRange(
				key,
				key + FdbKey.MinValue
			);
		}

		public override bool Equals(object obj)
		{
			return (obj is KeyRange) && Equals((KeyRange)obj);
		}

		public override int GetHashCode()
		{
			// ReSharper disable once NonReadonlyMemberInGetHashCode
			int h1 = m_begin.GetHashCode();
			// ReSharper disable once NonReadonlyMemberInGetHashCode
			int h2 = m_end.GetHashCode();
			return ((h1 << 5) + h1) ^ h2;
		}

		public bool Equals(KeyRange other)
		{
			return m_begin.Equals(other.m_begin) && m_end.Equals(other.m_end);
		}

		public static bool operator ==(KeyRange left, KeyRange right)
		{
			return left.m_begin.Equals(right.m_begin) && left.m_end.Equals(right.m_end);
		}

		public static bool operator !=(KeyRange left, KeyRange right)
		{
			return !left.m_begin.Equals(right.m_begin) || !left.m_end.Equals(right.m_end);
		}

		public int CompareTo(KeyRange other)
		{
			int c = m_begin.CompareTo(other.m_begin);
			if (c == 0) c = m_end.CompareTo(other.m_end);
			return c;
		}

		/// <summary>Combine another range with the current range, to produce a range that includes both (and all keys in between it the ranges are disjoint)</summary>
		/// <param name="other">Range to merge with the current range</param>
		/// <returns>New range where the Begin key is the smallest bound and the End key is the largest bound of both ranges.</returns>
		/// <remarks>If both range are disjoint, then the resulting range will also contain the keys in between.</remarks>
		public KeyRange Merge(KeyRange other)
		{
			Slice begin = m_begin.CompareTo(other.m_begin) <= 0 ? m_begin : other.m_begin;
			Slice end = m_end.CompareTo(other.m_end) >= 0 ? m_end : other.m_end;
			return new KeyRange(begin, end);
		}

		/// <summary>Checks whether the current and the specified range are intersecting (i.e: there exists at at least one key that belongs to both ranges)</summary>
		/// <param name="other">Range that is being checked for interection</param>
		/// <returns>True if the other range intersects the current range.</returns>
		/// <remarks>Note that ranges [0, 1) and [1, 2) do not intersect, since the end is exclusive by default</remarks>
		public bool Intersects(KeyRange other)
		{
			int c = m_begin.CompareTo(other.m_begin);
			if (c == 0)
			{ // share the same begin key
				return true;
			}
			else if (c < 0)
			{ // after us
				return m_end.CompareTo(other.m_begin) > 0;
			}
			else
			{  // before us
				return m_begin.CompareTo(other.m_end) < 0;
			}
		}

		/// <summary>Checks whether the current and the specified range are disjoint (i.e: there exists at least one key between both ranges)</summary>
		/// <param name="other"></param>
		/// <returns></returns>
		/// <remarks>Note that ranges [0, 1) and [1, 2) are not disjoint because, even though they do not intersect, they are both contiguous.</remarks>
		public bool Disjoint(KeyRange other)
		{
			int c = m_begin.CompareTo(other.m_begin);
			if (c == 0)
			{ // share the same begin key
				return false;
			}
			else if (c < 0)
			{ // after us
				return m_end.CompareTo(other.m_begin) < 0;
			}
			else
			{  // before us
				return m_begin.CompareTo(other.m_end) > 0;
			}
		}

		/// <summary>Returns true, if the key is contained in the range</summary>
		/// <param name="key"></param>
		/// <returns></returns>
		public bool Contains(Slice key)
		{
			return key.CompareTo(m_begin) >= 0 && key.CompareTo(m_end) < 0;
		}

		/// <summary>Test if <paramref name="key"/> is contained inside the range</summary>
		/// <param name="key">Key that will be compared with the the range's bounds</param>
		/// <param name="endIncluded">If true, the End bound is inclusive, otherwise it is exclusive</param>
		/// <returns>-1 if key is less than the lower bound of the range (<paramref name="key"/> &lt; Begin), +1 if the key is greater or equal to the higher bound of the range (<paramref name="key"/> &gt;= End) or 0 if it is inside the range (Begin &lt;= <paramref name="key"/> &lt; End)</returns>
		public int Test(Slice key, bool endIncluded = false)
		{
			// note: if the range is empty (Begin = End = Slice.Empty) then it should return 0

			if (m_begin.IsPresent && key.CompareTo(m_begin) < 0) return -1;
			if (m_end.IsPresent && key.CompareTo(m_end) >= (endIncluded ? 1 : 0)) return +1;
			return 0;
		}

		/// <summary>Returns a printable version of the range</summary>
		public override string ToString()
		{
			return "{" + FdbKey.PrettyPrint(m_begin, FdbKey.PrettyPrintMode.Begin) + ", " + FdbKey.PrettyPrint(m_end, FdbKey.PrettyPrintMode.End) + "}";
		}
	
	}

}
