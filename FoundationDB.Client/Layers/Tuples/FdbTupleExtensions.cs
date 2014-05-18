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

namespace FoundationDB.Layers.Tuples
{
	using FoundationDB.Client;
	using JetBrains.Annotations;
	using System;

	/// <summary>Add extensions methods that deal with tuples on various types</summary>
	public static class FdbTupleExtensions
	{
		#region IFdbTuple extensions...

		/// <summary>Returns true if the tuple is either null or empty</summary>
		public static bool IsNullOrEmpty(this IFdbTuple tuple)
		{
			return tuple == null || tuple.Count == 0;
		}

		/// <summary>Returns true if the tuple is not null, and contains only one item</summary>
		public static bool IsSingleton(this IFdbTuple tuple)
		{
			return tuple != null && tuple.Count == 1;
		}

		/// <summary>Returns an array containing all the objects of a tuple</summary>
		[NotNull]
		public static object[] ToArray(this IFdbTuple tuple)
		{
			if (tuple == null) throw new ArgumentNullException("tuple");

			var items = new object[tuple.Count];
			if (items.Length > 0)
			{
				tuple.CopyTo(items, 0);
			}
			return items;
		}

		/// <summary>Returns a typed array containing all the items of a tuple</summary>
		[NotNull]
		public static T[] ToArray<T>(this IFdbTuple tuple)
		{
			if (tuple == null) throw new ArgumentNullException("tuple");

			var items = new T[tuple.Count];
			if (items.Length > 0)
			{
				for (int i = 0; i < items.Length; i++)
				{
					items[i] = tuple.Get<T>(i);
				}
			}
			return items;			
		}

		/// <summary>Returns a byte array containing the packed version of a tuple</summary>
		[CanBeNull]
		public static byte[] GetBytes(this IFdbTuple tuple)
		{
			return tuple.ToSlice().GetBytes();
		}

		/// <summary>Concatenates two tuples together</summary>
		[NotNull]
		public static IFdbTuple Concat(this IFdbTuple head, IFdbTuple tail)
		{
			if (head == null) throw new ArgumentNullException("head");
			if (tail == null) throw new ArgumentNullException("tail");

			int n1 = head.Count;
			if (n1 == 0) return tail;

			int n2 = tail.Count;
			if (n2 == 0) return head;

			return new FdbJoinedTuple(head, tail);
		}

		/// <summary>Appends two values at the end of a tuple</summary>
		[NotNull]
		public static IFdbTuple Append<T1, T2>(this IFdbTuple tuple, T1 value1, T2 value2)
		{
			if (tuple == null) throw new ArgumentNullException("tuple");
			return new FdbJoinedTuple(tuple, FdbTuple.Create<T1, T2>(value1, value2));
		}

		/// <summary>Appends three values at the end of a tuple</summary>
		[NotNull]
		public static IFdbTuple Append<T1, T2, T3>(this IFdbTuple tuple, T1 value1, T2 value2, T3 value3)
		{
			if (tuple == null) throw new ArgumentNullException("tuple");
			return new FdbJoinedTuple(tuple, FdbTuple.Create<T1, T2, T3>(value1, value2, value3));
		}

		/// <summary>Appends four values at the end of a tuple</summary>
		[NotNull]
		public static IFdbTuple Append<T1, T2, T3, T4>(this IFdbTuple tuple, T1 value1, T2 value2, T3 value3, T4 value4)
		{
			if (tuple == null) throw new ArgumentNullException("tuple");
			return new FdbJoinedTuple(tuple, FdbTuple.Create<T1, T2, T3, T4>(value1, value2, value3, value4));
		}

		/// <summary>Creates a key range containing all children of this tuple, from tuple.pack()+'\0' to tuple.pack()+'\xFF'</summary>
		/// <param name="tuple">Tuple that is the suffix of all keys</param>
		/// <returns>Range of all keys suffixed by the tuple. The tuple itself will not be included</returns>
		public static FdbKeyRange ToRange(this IFdbTuple tuple)
		{
			return ToRange(tuple, false);
		}

		/// <summary>Creates a key range containing all children of tuple, optionally including the tuple itself.</summary>
		/// <param name="tuple">Tuple that is the prefix of all keys</param>
		/// <param name="includePrefix">If true, the tuple key itself is included, if false only the children keys are included</param>
		/// <returns>Range of all keys suffixed by the tuple. The tuple itself will be included if <paramref name="includePrefix"/> is true</returns>
		public static FdbKeyRange ToRange(this IFdbTuple tuple, bool includePrefix)
		{
			if (tuple == null) throw new ArgumentNullException("tuple");

			// We want to allocate only one byte[] to store both keys, and map both Slice to each chunk
			// So we will serialize the tuple two times in the same writer

			var writer = SliceWriter.Empty;

			tuple.PackTo(ref writer);
			writer.EnsureBytes(writer.Position + 2);
			if (!includePrefix) writer.WriteByte(0);
			int p0 = writer.Position;

			tuple.PackTo(ref writer);
			writer.WriteByte(0xFF);
			int p1 = writer.Position;

			return new FdbKeyRange(
				new Slice(writer.Buffer, 0, p0),
				new Slice(writer.Buffer, p0, p1 - p0)
			);
		}

		/// <summary>Creates pre-packed and isolated copy of this tuple</summary>
		/// <param name="tuple"></param>
		/// <returns>Create a copy of the tuple that can be reused frequently to pack values</returns>
		/// <remarks>If the tuple is already memoized, the current instance will be returned</remarks>
		[CanBeNull]
		public static FdbMemoizedTuple Memoize(this IFdbTuple tuple)
		{
			if (tuple == null) return null;

			var memoized = tuple as FdbMemoizedTuple ?? new FdbMemoizedTuple(tuple.ToArray(), tuple.ToSlice());

			return memoized;
		}

		/// <summary>Unpack a tuple from this slice</summary>
		/// <param name="slice"></param>
		/// <returns>Unpacked tuple if the slice contains data, FdbTuple.Empty if the slice is empty, or null if the slice is Slice.Nil</returns>
		[CanBeNull]
		public static IFdbTuple ToTuple(this Slice slice)
		{
			if (slice.IsNullOrEmpty)
			{
				return slice.HasValue ? FdbTuple.Empty : null;
			}

			return FdbTuple.Unpack(slice);
		}

		/// <summary>Returns a substring of the current tuple</summary>
		/// <param name="tuple">Current tuple</param>
		/// <param name="offset">Offset from the start of the current tuple (negative value means from the end)</param>
		/// <returns>Tuple that contains only the items past the first <param name="offset"/> items of the current tuple</returns>
		[NotNull]
		public static IFdbTuple Substring(this IFdbTuple tuple, int offset)
		{
			if (tuple == null) throw new ArgumentNullException("tuple");

			return tuple[offset, null];
		}

		/// <summary>Returns a substring of the current tuple</summary>
		/// <param name="tuple">Current tuple</param>
		/// <param name="offset">Offset from the start of the current tuple (negative value means from the end)</param>
		/// <param name="count">Number of items to keep</param>
		/// <returns>Tuple that contains only the selected items from the current tuple</returns>
		[NotNull]
		public static IFdbTuple Substring(this IFdbTuple tuple, int offset, int count)
		{
			if (tuple == null) throw new ArgumentNullException("tuple");
			if (count < 0) throw new ArgumentOutOfRangeException("count", count, "Count cannot be negative.");

			if (count == 0) return FdbTuple.Empty;

			return tuple[offset, offset + count];
		}

		/// <summary>Test if the start of current tuple is equal to another tuple</summary>
		/// <param name="left">Larger tuple</param>
		/// <param name="right">Smaller tuple</param>
		/// <returns>True if the beginning of <paramref name="left"/> is equal to <paramref name="right"/> or if both tuples are identical</returns>
		public static bool StartsWith(this IFdbTuple left, IFdbTuple right)
		{
			if (left == null) throw new ArgumentNullException("left");
			if (right == null) throw new ArgumentNullException("right");

			//REVIEW: move this on IFdbTuple interface ?
			return FdbTuple.StartsWith(left, right);
		}

		/// <summary>Test if the end of current tuple is equal to another tuple</summary>
		/// <param name="left">Larger tuple</param>
		/// <param name="right">Smaller tuple</param>
		/// <returns>True if the end of <paramref name="left"/> is equal to <paramref name="right"/> or if both tuples are identical</returns>
		public static bool EndsWith(this IFdbTuple left, IFdbTuple right)
		{
			if (left == null) throw new ArgumentNullException("left");
			if (right == null) throw new ArgumentNullException("right");

			//REVIEW: move this on IFdbTuple interface ?
			return FdbTuple.EndsWith(left, right);
		}

		/// <summary>Returns a key that is immediately after the packed representation of this tuple</summary>
		/// <remarks>This is the equivalent of manually packing the tuple and incrementing the resulting slice</remarks>
		public static Slice Increment(this IFdbTuple tuple)
		{
			if (tuple == null) throw new ArgumentNullException("tuple");
			return FdbKey.Increment(tuple.ToSlice());
		}

		public static FdbKeySelectorPair ToSelectorPair(this IFdbTuple tuple)
		{
			if (tuple == null) throw new ArgumentNullException("tuple");

			return FdbKeySelectorPair.StartsWith(tuple.ToSlice());
		}

		#endregion

	}

}
