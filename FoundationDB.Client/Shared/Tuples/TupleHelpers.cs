#region BSD License
/* Copyright (c) 2013-2018, Doxense SAS
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

#if !USE_SHARED_FRAMEWORK

namespace Doxense.Collections.Tuples
{
	using System;
	using System.Collections;
	using System.Runtime.CompilerServices;
	using Doxense.Diagnostics.Contracts;
	using JetBrains.Annotations;

	public static class TupleHelpers
	{

		/// <summary>Default (non-optimized) implementation of ITuple.this[long?, long?]</summary>
		/// <param name="tuple">Tuple to slice</param>
		/// <param name="fromIncluded">Start offset of the section (included)</param>
		/// <param name="toExcluded">End offset of the section (included)</param>
		/// <returns>New tuple only containing items inside this section</returns>
		[NotNull]
		public static IVarTuple Splice([NotNull] IVarTuple tuple, int? fromIncluded, int? toExcluded)
		{
			Contract.Requires(tuple != null);
			int count = tuple.Count;
			if (count == 0) return STuple.Empty;

			int start = fromIncluded.HasValue ? MapIndexBounded(fromIncluded.Value, count) : 0;
			int end = toExcluded.HasValue ? MapIndexBounded(toExcluded.Value, count) : count;

			int len = end - start;

			if (len <= 0) return STuple.Empty;
			if (start == 0 && len == count) return tuple;
			switch (len)
			{
				case 1:
					return new ListTuple(new[] { tuple[start] }, 0, 1);
				case 2:
					return new ListTuple(new[] { tuple[start], tuple[start + 1] }, 0, 2);
				default:
				{
					var items = new object[len];
					//note: can be slow for tuples using linked-lists, but hopefully they will have their own Slice implementation...
					int q = start;
					for (int p = 0; p < items.Length; p++)
					{
						items[p] = tuple[q++];
					}
					return new ListTuple(items, 0, len);
				}
			}
		}

		/// <summary>Default (non-optimized) implementation for ITuple.StartsWith()</summary>
		/// <param name="a">Larger tuple</param>
		/// <param name="b">Smaller tuple</param>
		/// <returns>True if <paramref name="a"/> starts with (or is equal to) <paramref name="b"/></returns>
		public static bool StartsWith([NotNull] IVarTuple a, [NotNull] IVarTuple b)
		{
			Contract.Requires(a != null && b != null);
			if (object.ReferenceEquals(a, b)) return true;
			int an = a.Count;
			int bn = b.Count;

			if (bn > an) return false;
			if (bn == 0) return true; // note: 'an' can only be 0 because of previous test

			for (int i = 0; i < bn; i++)
			{
				if (!object.Equals(a[i], b[i])) return false;
			}
			return true;
		}

		/// <summary>Default (non-optimized) implementation for ITuple.EndsWith()</summary>
		/// <param name="a">Larger tuple</param>
		/// <param name="b">Smaller tuple</param>
		/// <returns>True if <paramref name="a"/> starts with (or is equal to) <paramref name="b"/></returns>
		public static bool EndsWith([NotNull] IVarTuple a, [NotNull] IVarTuple b)
		{
			Contract.Requires(a != null && b != null);
			if (object.ReferenceEquals(a, b)) return true;
			int an = a.Count;
			int bn = b.Count;

			if (bn > an) return false;
			if (bn == 0) return true; // note: 'an' can only be 0 because of previous test

			int offset = an - bn;
			for (int i = 0; i < bn; i++)
			{
				if (!object.Equals(a[offset + i], b[i])) return false;
			}
			return true;
		}

		/// <summary>Helper to copy the content of a tuple at a specific position in an array</summary>
		/// <returns>Updated offset just after the last element of the copied tuple</returns>
		public static int CopyTo([NotNull] IVarTuple tuple, [NotNull] object[] array, int offset)
		{
			Contract.Requires(tuple != null && array != null && offset >= 0);

			foreach (var item in tuple)
			{
				array[offset++] = item;
			}
			return offset;
		}

		/// <summary>Maps a relative index into an absolute index</summary>
		/// <param name="index">Relative index in the tuple (from the end if negative)</param>
		/// <param name="count">Size of the tuple</param>
		/// <returns>Absolute index from the start of the tuple, or exception if outside of the tuple</returns>
		/// <exception cref="System.IndexOutOfRangeException">If the absolute index is outside of the tuple (&lt;0 or &gt;=<paramref name="count"/>)</exception>
		public static int MapIndex(int index, int count)
		{
			int offset = index;
			if (offset < 0) offset += count;
			if (offset < 0 || offset >= count) return FailIndexOutOfRange<int>(index, count);
			return offset;
		}

		/// <summary>Maps a relative index into an absolute index</summary>
		/// <param name="index">Relative index in the tuple (from the end if negative)</param>
		/// <param name="count">Size of the tuple</param>
		/// <returns>Absolute index from the start of the tuple. Truncated to 0 if index is before the start of the tuple, or to <paramref name="count"/> if the index is after the end of the tuple</returns>
		public static int MapIndexBounded(int index, int count)
		{
			if (index < 0) index += count;
			return Math.Max(Math.Min(index, count), 0);
		}

		[ContractAnnotation("=> halt"), MethodImpl(MethodImplOptions.NoInlining)]
		public static T FailIndexOutOfRange<T>(int index, int count)
		{
			throw new IndexOutOfRangeException($"Index {index} is outside of the tuple range (0..{count - 1})");
		}

		public static bool Equals(IVarTuple left, object other, [NotNull] IEqualityComparer comparer)
		{
			return object.ReferenceEquals(left, null) ? other == null : Equals(left, other as IVarTuple, comparer);
		}

		public static bool Equals(IVarTuple x, IVarTuple y, [NotNull] IEqualityComparer comparer)
		{
			if (object.ReferenceEquals(x, y)) return true;
			if (object.ReferenceEquals(x, null) || object.ReferenceEquals(y, null)) return false;

			return x.Count == y.Count && DeepEquals(x, y, comparer);
		}

		public static bool DeepEquals([NotNull] IVarTuple x, [NotNull] IVarTuple y, [NotNull] IEqualityComparer comparer)
		{
			Contract.Requires(x != null && y != null && comparer != null);

			using (var xs = x.GetEnumerator())
			using (var ys = y.GetEnumerator())
			{
				while (xs.MoveNext())
				{
					if (!ys.MoveNext()) return false;
					if (!comparer.Equals(xs.Current, ys.Current)) return false;
				}

				return !ys.MoveNext();
			}
		}

		public static int StructuralGetHashCode(IVarTuple tuple, [NotNull] IEqualityComparer comparer)
		{
			Contract.Requires(comparer != null);

			if (object.ReferenceEquals(tuple, null))
			{
				return comparer.GetHashCode(null);
			}

			int h = 0;
			foreach (var item in tuple)
			{
				h = HashCodes.Combine(h, comparer.GetHashCode(item));
			}
			return h;
		}

		public static int StructuralCompare(IVarTuple x, IVarTuple y, [NotNull] IComparer comparer)
		{
			Contract.Requires(comparer != null);

			if (object.ReferenceEquals(x, y)) return 0;
			if (object.ReferenceEquals(x, null)) return -1;
			if (object.ReferenceEquals(y, null)) return 1;

			using (var xs = x.GetEnumerator())
			using (var ys = y.GetEnumerator())
			{
				while (xs.MoveNext())
				{
					if (!ys.MoveNext()) return 1;

					int cmp = comparer.Compare(xs.Current, ys.Current);
					if (cmp != 0) return cmp;

				}
				return ys.MoveNext() ? -1 : 0;
			}
		}
	}
}

#endif
