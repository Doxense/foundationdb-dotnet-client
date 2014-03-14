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
	using FoundationDB.Client.Utils;
	using System;
	using System.Collections.Generic;
	using System.Linq;
	using System.Threading;
	using System.Threading.Tasks;

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
		public static byte[] GetBytes(this IFdbTuple tuple)
		{
			return tuple.ToSlice().GetBytes();
		}

		/// <summary>Concatenates two tuples together</summary>
		public static IFdbTuple Concat(this IFdbTuple first, IFdbTuple second)
		{
			if (first == null) throw new ArgumentNullException("first");
			if (second == null) throw new ArgumentNullException("second");

			int n1 = first.Count;
			if (n1 == 0) return second;

			int n2 = second.Count;
			if (n2 == 0) return first;

			return new FdbJoinedTuple(first, second);
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
		public static FdbMemoizedTuple Memoize(this IFdbTuple tuple)
		{
			if (tuple == null) return null;

			var memoized = tuple as FdbMemoizedTuple;
			if (memoized == null)
			{
				memoized = new FdbMemoizedTuple(tuple.ToArray(), tuple.ToSlice());
			}

			return memoized;
		}

		/// <summary>Unpack a tuple from this slice</summary>
		/// <param name="slice"></param>
		/// <returns>Unpacked tuple if the slice contains data, FdbTuple.Empty if the slice is empty, or null if the slice is Slice.Nil</returns>
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

		#region FdbTransaction extensions...

		//NOTE: most of these are now obsolete, since IFdbTuple implements IFdbKey !

#if REFACTORED

		/// <summary>Returns the value of a particular key</summary>
		/// <param name="key">Key to retrieve</param>
		/// <returns>Task that will return the value of the key if it is found, null if the key does not exist, or an exception</returns>
		/// <exception cref="System.ArgumentException">If the key is null or empty</exception>
		/// <exception cref="System.OperationCanceledException">If the cancellation token is already triggered</exception>
		/// <exception cref="System.ObjectDisposedException">If the transaction has already been completed</exception>
		/// <exception cref="System.InvalidOperationException">If the operation method is called from the Network Thread</exception>
		public static Task<Slice> GetAsync(this IFdbReadOnlyTransaction trans, IFdbTuple key)
		{
			Contract.Requires(trans != null);
			if (key == null) throw new ArgumentNullException("key");

			return trans.GetAsync(key.ToSlice());
		}

		public static Task<Slice[]> GetValuesAsync(this IFdbReadOnlyTransaction trans, IFdbTuple[] tuples)
		{
			if (trans == null) throw new ArgumentNullException("trans");
			if (tuples == null) throw new ArgumentNullException("tuples");

			return trans.GetValuesAsync(FdbTuple.PackRange(tuples));
		}

		public static Task<Slice[]> GetValuesAsync(this IFdbReadOnlyTransaction trans, IEnumerable<IFdbTuple> tuples)
		{
			if (trans == null) throw new ArgumentNullException("trans");
			if (tuples == null) throw new ArgumentNullException("tuples");

			return trans.GetValuesAsync(FdbTuple.PackRange(tuples));
		}

		public static Task<List<KeyValuePair<IFdbTuple, Slice>>> GetBatchAsync(this IFdbReadOnlyTransaction trans, IEnumerable<IFdbTuple> tuples)
		{
			if (trans == null) throw new ArgumentNullException("trans");
			if (tuples == null) throw new ArgumentNullException("tuples");

			return GetBatchAsync(trans, tuples.ToArray());
		}

		public static async Task<List<KeyValuePair<IFdbTuple, Slice>>> GetBatchAsync(this IFdbReadOnlyTransaction trans, IFdbTuple[] tuples)
		{
			var results = await GetValuesAsync(trans, tuples).ConfigureAwait(false);

			// maps the index back to the original key
			return results
				.Select((value, i) => new KeyValuePair<IFdbTuple, Slice>(tuples[i], value))
				.ToList();
		}

		public static FdbRangeQuery<KeyValuePair<Slice, Slice>> GetRange(this IFdbReadOnlyTransaction trans, IFdbTuple beginInclusive, IFdbTuple endExclusive, FdbRangeOptions options = null)
		{
			Contract.Requires(trans != null);

			var begin = beginInclusive != null ? beginInclusive.ToSlice() : FdbKey.MinValue;
			var end = endExclusive != null ? endExclusive.ToSlice() : FdbKey.MaxValue;

			return trans.GetRange(begin, end, options);
		}

		public static FdbRangeQuery<KeyValuePair<Slice, Slice>> GetRange(this IFdbReadOnlyTransaction trans, IFdbTuple beginInclusive, IFdbTuple endExclusive, int limit, bool reverse = false)
		{
			var begin = beginInclusive != null ? beginInclusive.ToSlice() : FdbKey.MinValue;
			var end = endExclusive != null ? endExclusive.ToSlice() : FdbKey.MaxValue;

			return trans.GetRange(begin, end, new FdbRangeOptions(limit: limit, reverse: reverse));
		}

		public static void Set(this IFdbTransaction trans, IFdbTuple key, Slice value)
		{
			Contract.Requires(trans != null);
			if (key == null) throw new ArgumentNullException("key");

			trans.Set(key.ToSlice(), value);
		}

		public static void Clear(this IFdbTransaction trans, IFdbTuple key)
		{
			Contract.Requires(trans != null);
			if (key == null) throw new ArgumentNullException("key");

			trans.Clear(key.ToSlice());
		}

		public static void ClearRange(this IFdbTransaction trans, IFdbTuple beginInclusive, IFdbTuple endExclusive)
		{
			Contract.Requires(trans != null);
			if (beginInclusive == null) throw new ArgumentNullException("beginInclusive");
			if (endExclusive == null) throw new ArgumentNullException("endExclusive");

			trans.ClearRange(beginInclusive.ToSlice(), endExclusive.ToSlice());
		}

		public static void ClearRange(this IFdbTransaction trans, IFdbTuple prefix)
		{
			Contract.Requires(trans != null);
			if (prefix == null) throw new ArgumentNullException("prefix");

			var range = prefix.ToRange();
			trans.ClearRange(range.Begin, range.End);
		}

		/// <summary>
		/// Adds a tuple prefix to the transaction’s read conflict ranges as if you had read the key. As a result, other transactions that write to any key contained in this tuple prefix could cause the transaction to fail with a conflict.
		/// </summary>
		public static void AddReadConflictRange(this IFdbTransaction trans, IFdbTuple prefix)
		{
			if (trans == null) throw new ArgumentNullException("trans");
			if (prefix == null) throw new ArgumentNullException("prefix");

			trans.AddConflictRange(prefix.ToRange(), FdbConflictRangeType.Read);
		}

		/// <summary>
		/// Adds a tuple to the transaction’s read conflict ranges as if you had read the key. As a result, other transactions that write to this key could cause the transaction to fail with a conflict.
		/// </summary>
		public static void AddReadConflictKey(this IFdbTransaction trans, IFdbTuple tuple)
		{
			if (trans == null) throw new ArgumentNullException("trans");
			if (tuple == null) throw new ArgumentNullException("tuple");

			trans.AddConflictRange(FdbKeyRange.FromKey(tuple.ToSlice()), FdbConflictRangeType.Read);
		}

		/// <summary>
		/// Adds a tuple prefix to the transaction’s write conflict ranges as if you had cleared the range. As a result, other transactions that concurrently read any key contained in this tuple prefix could fail with a conflict.
		/// </summary>
		public static void AddWriteConflictRange(this IFdbTransaction trans, IFdbTuple prefix)
		{
			if (trans == null) throw new ArgumentNullException("trans");
			if (prefix == null) throw new ArgumentNullException("prefix");

			trans.AddConflictRange(prefix.ToRange(), FdbConflictRangeType.Write);
		}

		/// <summary>
		/// Adds a tuple to the transaction’s write conflict ranges as if you had cleared the key. As a result, other transactions that concurrently read this key could fail with a conflict.
		/// </summary>
		public static void AddWriteConflictKey(this IFdbTransaction trans, IFdbTuple tuple)
		{
			if (trans == null) throw new ArgumentNullException("trans");
			if (tuple == null) throw new ArgumentNullException("tuple");

			trans.AddConflictRange(FdbKeyRange.FromKey(tuple.ToSlice()), FdbConflictRangeType.Write);
		}

#endif

		#endregion

	}

}
