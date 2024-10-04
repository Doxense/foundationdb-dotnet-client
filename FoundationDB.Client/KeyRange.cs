#region Copyright (c) 2023-2024 SnowBank SAS, (c) 2005-2023 Doxense SAS
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
	using System.ComponentModel;
	using System.Diagnostics;
	using System.Runtime.CompilerServices;

	/// <summary>Represents a pair of keys defining the range 'Begin &lt;= key &gt; End'</summary>
	[DebuggerDisplay("Begin={Begin}, End={End}")]
	[PublicAPI]
	public readonly struct KeyRange : IEquatable<KeyRange>, IComparable<KeyRange>, IEquatable<(Slice Begin, Slice End)>, IComparable<(Slice Begin, Slice End)>
	{

		/// <summary>Start of the range</summary>
		public readonly Slice Begin;

		/// <summary>End of the range</summary>
		public readonly Slice End;

		/// <summary>Create a new range of keys</summary>
		/// <param name="begin">Start of range (usually included)</param>
		/// <param name="end">End of range (usually excluded)</param>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public KeyRange(Slice begin, Slice end)
		{
			this.Begin = begin;
			this.End = end;
			Contract.Debug.Ensures(this.Begin <= this.End, "The range is inverted");
		}

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static KeyRange Create(Slice a, Slice b)
		{
			return new KeyRange(a, b);
		}

		/// <summary>Returns an empty pair of keys</summary>
		public static readonly KeyRange Empty;

		/// <summary>Returns a range that contains all the keys in the database</summary>
		public static KeyRange All => new KeyRange(FdbKey.MinValue, FdbKey.MaxValue);

		/// <summary>Create a range that will return all keys starting with <paramref name="prefix"/>: ('prefix' &lt;= k &lt; strinc('prefix'))</summary>
		[Pure]
		public static KeyRange StartsWith(Slice prefix)
		{
			if (prefix.Count == 0)
			{
				if (prefix.IsNull) throw Fdb.Errors.KeyCannotBeNull(nameof(prefix));
				return new KeyRange(Slice.Empty, FdbKey.MaxValue);
			}

			// prefix => [ prefix, prefix + 1 )
			return new KeyRange(
				prefix,
				FdbKey.Increment(prefix)
			);
		}

		/// <summary>Create a range that selects all keys starting with <paramref name="prefix"/>, but not the prefix itself: ('prefix\x00' &lt;= k &lt; string('prefix')</summary>
		/// <param name="prefix">Key prefix (that will be excluded from the range)</param>
		/// <returns>Range including all keys with the specified prefix.</returns>
		[Pure]
		public static KeyRange PrefixedBy(Slice prefix)
		{
			if (prefix.IsNull) throw Fdb.Errors.KeyCannotBeNull(nameof(prefix));

			// prefix => [ prefix."\0", prefix + 1)
			return new KeyRange(
				prefix + FdbKey.MinValue,
				FdbKey.Increment(prefix)
			);
		}

		/// <summary>Create a range that will only return <paramref name="key"/> itself ('key' &lt;= k &lt; 'key\x00')</summary>
		/// <param name="key">Key that will be returned by the range</param>
		/// <returns>Range that only return the specified key.</returns>
		[Pure]
		public static KeyRange FromKey(Slice key)
		{
			if (key.Count == 0)
			{ // "" => [ "", "\x00" )
				if (key.IsNull) throw Fdb.Errors.KeyCannotBeNull();
				return new KeyRange(Slice.Empty, FdbKey.MinValue);
			}
			// key => [ key, key + '\0' )
			return new KeyRange(
				key,
				key + FdbKey.MinValue
			);
		}

		public override bool Equals(object? obj)
		{
			if (obj is KeyRange range) return Equals(range);
			if (obj is ValueTuple<Slice, Slice> tuple) return Equals(tuple);
			return false;
		}

		public override int GetHashCode()
		{
			return HashCode.Combine(this.Begin.GetHashCode(), this.End.GetHashCode());
		}

		public bool Equals(KeyRange other)
		{
			return this.Begin.Equals(other.Begin) && this.End.Equals(other.End);
		}

		public bool Equals((Slice Begin, Slice End) other)
		{
			return this.Begin.Equals(other.Begin) && this.End.Equals(other.End);
		}

		public static bool operator ==(KeyRange left, KeyRange right)
		{
			return left.Begin.Equals(right.Begin) && left.End.Equals(right.End);
		}

		public static bool operator !=(KeyRange left, KeyRange right)
		{
			return !left.Begin.Equals(right.Begin) || !left.End.Equals(right.End);
		}

		public int CompareTo(KeyRange other)
		{
			int c = this.Begin.CompareTo(other.Begin);
			if (c == 0) c = this.End.CompareTo(other.End);
			return c;
		}

		public int CompareTo((Slice Begin, Slice End) other)
		{
			int c = this.Begin.CompareTo(other.Begin);
			if (c == 0) c = this.End.CompareTo(other.End);
			return c;
		}

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static implicit operator KeyRange((Slice Begin, Slice End) range)
		{
			return new KeyRange(range.Begin, range.End);
		}

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static implicit operator (Slice Begin, Slice End)(KeyRange range)
		{
			return (range.Begin, range.End);
		}

		/// <summary>Combine another range with the current range, to produce a range that includes both (and all keys in between it the ranges are disjoint)</summary>
		/// <param name="other">Range to merge with the current range</param>
		/// <returns>New range where the Begin key is the smallest bound and the End key is the largest bound of both ranges.</returns>
		/// <remarks>If both range are disjoint, then the resulting range will also contain the keys in between.</remarks>
		[Pure]
		public KeyRange Merge(in KeyRange other)
		{
			var begin = this.Begin.CompareTo(other.Begin) <= 0 ? this.Begin : other.Begin;
			var end = this.End.CompareTo(other.End) >= 0 ? this.End : other.End;
			return new KeyRange(begin, end);
		}

		/// <summary>Checks whether the current and the specified range are intersecting (i.e: there exists at least one key that belongs to both ranges)</summary>
		/// <param name="other">Range that is being checked for interaction</param>
		/// <returns>True if the other range intersects the current range.</returns>
		/// <remarks>Note that ranges [0, 1) and [1, 2) do not intersect, since the end is exclusive by default</remarks>
		[Pure]
		public bool Intersects(in KeyRange other)
		{
			int c = this.Begin.CompareTo(other.Begin);
			if (c == 0)
			{ // share the same begin key
				return true;
			}
			if (c < 0)
			{ // after us
				return this.End.CompareTo(other.Begin) > 0;
			}
			// before us
			return this.Begin.CompareTo(other.End) < 0;
		}

		/// <summary>Checks whether the current and the specified range are disjoint (i.e: there exists at least one key between both ranges)</summary>
		/// <param name="other"></param>
		/// <returns></returns>
		/// <remarks>Note that ranges [0, 1) and [1, 2) are not disjoint because, even though they do not intersect, they are both contiguous.</remarks>
		[Pure]
		public bool Disjoint(in KeyRange other)
		{
			int c = this.Begin.CompareTo(other.Begin);
			if (c == 0)
			{ // share the same begin key
				return false;
			}
			if (c < 0)
			{ // after us
				return this.End.CompareTo(other.Begin) < 0;
			}
			// before us
			return this.Begin.CompareTo(other.End) > 0;
		}

		/// <summary>Returns true, if the key is contained in the range</summary>
		/// <param name="key"></param>
		/// <returns></returns>
		[Pure]
		public bool Contains(Slice key)
		{
			return key.CompareTo(this.Begin) >= 0 && key.CompareTo(this.End) < 0;
		}

		/// <summary>Test if <paramref name="key"/> is contained inside the range</summary>
		/// <param name="key">Key that will be compared with the range's bounds</param>
		/// <param name="endIncluded">If true, the End bound is inclusive, otherwise it is exclusive</param>
		/// <returns>-1 if key is less than the lower bound of the range (<paramref name="key"/> &lt; Begin), +1 if the key is greater or equal to the higher bound of the range (<paramref name="key"/> &gt;= End) or 0 if it is inside the range (Begin &lt;= <paramref name="key"/> &lt; End)</returns>
		[Pure]
		public int Test(Slice key, bool endIncluded = false)
		{
			// note: if the range is empty (Begin = End = Slice.Empty) then it should return 0

			if (this.Begin.Count != 0 && key.CompareTo(this.Begin) < 0) return -1;
			if (this.End.Count != 0 && key.CompareTo(this.End) >= (endIncluded ? 1 : 0)) return +1;
			return 0;
		}

		/// <summary>Deconstructs this key range</summary>
		/// <param name="begin">Receives the <see cref="Begin"/> key</param>
		/// <param name="end">Receives the <see cref="End"/> key</param>
		[EditorBrowsable(EditorBrowsableState.Never)]
		public void Deconstruct(out Slice begin, out Slice end)
		{
			begin = this.Begin;
			end = this.End;
		}

		/// <summary>Returns a printable version of the range</summary>
		public override string ToString()
		{
			return $"{{{FdbKey.PrettyPrint(this.Begin, FdbKey.PrettyPrintMode.Begin)}, {FdbKey.PrettyPrint(this.End, FdbKey.PrettyPrintMode.End)}}}";
		}
	
		[DebuggerDisplay("Mode={m_mode}")]
		public sealed class Comparer : IComparer<KeyRange>, IEqualityComparer<KeyRange>
		{
			private const int BOTH = 0;
			private const int BEGIN = 1;
			private const int END = 2;

			public static readonly Comparer Default = new Comparer(BOTH);
			public static readonly Comparer Begin = new Comparer(BEGIN);
			public static readonly Comparer End = new Comparer(END);

			private readonly int m_mode;

			private Comparer(int mode)
			{
				Contract.Debug.Requires(mode >= BOTH && mode <= END);
				m_mode = mode;
			}

			public int Compare(KeyRange x, KeyRange y)
			{
				switch (m_mode)
				{
					case BEGIN: return x.Begin.CompareTo(y.Begin);
					case END: return x.End.CompareTo(y.End);
					default: return x.CompareTo(y);
				}
			}

			public bool Equals(KeyRange x, KeyRange y)
			{
				switch(m_mode)
				{
					case BEGIN: return x.Begin.Equals(y.Begin);
					case END: return x.End.Equals(y.End);
					default: return x.Equals(y);
				}
			}

			public int GetHashCode(KeyRange obj)
			{
				switch(m_mode)
				{
					case BEGIN: return obj.Begin.GetHashCode();
					case END: return obj.End.GetHashCode();
					default: return obj.GetHashCode();
				}
			}
		}


	}

}
