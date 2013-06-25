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

using FoundationDB.Client;
using FoundationDB.Client.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace FoundationDB.Layers.Tuples
{

	/// <summary>Add extensions methods that deal with tuples on various types</summary>
	public static class FdbTupleExtensions
	{
		#region IFdbTuple extensions...

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

			var writer = new FdbBufferWriter();

			tuple.PackTo(writer);
			writer.EnsureBytes(writer.Position + 2);
			if (!includePrefix) writer.WriteByte(0);
			int p0 = writer.Position;

			tuple.PackTo(writer);
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
		/// <returns>Tuple that contains only the last <param name="count"/> items of the current tuple</returns>
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

			if (count == 0) return FdbTuple.Empty;

			return tuple[offset, offset + count - 1];
		}

		/// <summary>Test if the start of current tuple is equal to another tuple</summary>
		/// <param name="left">Larger tuple</param>
		/// <param name="right">Smaller tuple</param>
		/// <returns>True if the beginning of <paramref name="left"/> is equal to <paramref name="right"/> or if both tuples are identical</returns>
		public static bool StartsWith(this IFdbTuple left, IFdbTuple right)
		{
			if (left == null) throw new ArgumentNullException("left");
			if (right == null) throw new ArgumentNullException("right");

			//TODO: move this on IFdbTuple interface ?
			return FdbTuple.StartsWith(left, right);
		}

		public static Slice Increment(this IFdbTuple tuple)
		{
			if (tuple == null) throw new ArgumentNullException("tuple");
			return FdbKey.Increment(tuple.ToSlice());
		}

		#endregion

		#region FdbTransaction extensions...

		/// <summary>Returns the value of a particular key</summary>
		/// <param name="key">Key to retrieve</param>
		/// <param name="snapshot"></param>
		/// <param name="ct">CancellationToken used to cancel this operation</param>
		/// <returns>Task that will return the value of the key if it is found, null if the key does not exist, or an exception</returns>
		/// <exception cref="System.ArgumentException">If the key is null or empty</exception>
		/// <exception cref="System.OperationCanceledException">If the cancellation token is already triggered</exception>
		/// <exception cref="System.ObjectDisposedException">If the transaction has already been completed</exception>
		/// <exception cref="System.InvalidOperationException">If the operation method is called from the Network Thread</exception>
		public static Task<Slice> GetAsync(this FdbTransaction trans, IFdbTuple key, bool snapshot = false, CancellationToken ct = default(CancellationToken))
		{
			Contract.Requires(trans != null);
			if (key == null) throw new ArgumentNullException("key");

			return trans.GetAsync(key.ToSlice(), snapshot, ct);
		}

		public static Task<List<KeyValuePair<int, Slice>>> GetBatchIndexedAsync(this FdbTransaction trans, IEnumerable<IFdbTuple> keys, bool snapshot = false, CancellationToken ct = default(CancellationToken))
		{
			if (keys == null) throw new ArgumentNullException("keys");

			ct.ThrowIfCancellationRequested();

			return trans.GetBatchIndexedAsync(FdbTuple.BatchPack(keys), snapshot, ct);
		}

		public static Task<List<KeyValuePair<int, Slice>>> GetBatchIndexedAsync(this FdbTransaction trans, IFdbTuple[] keys, bool snapshot = false, CancellationToken ct = default(CancellationToken))
		{
			if (keys == null) throw new ArgumentNullException("keys");
			ct.ThrowIfCancellationRequested();

			return trans.GetBatchIndexedAsync(FdbTuple.BatchPack(keys), snapshot, ct);
		}

		public static Task<List<KeyValuePair<IFdbTuple, Slice>>> GetBatchAsync(this FdbTransaction trans, IEnumerable<IFdbTuple> keys, bool snapshot = false, CancellationToken ct = default(CancellationToken))
		{
			if (keys == null) throw new ArgumentNullException("keys");

			return GetBatchAsync(trans, keys.ToArray(), snapshot, ct);
		}

		public static async Task<List<KeyValuePair<IFdbTuple, Slice>>> GetBatchAsync(this FdbTransaction trans, IFdbTuple[] keys, bool snapshot = false, CancellationToken ct = default(CancellationToken))
		{
			if (keys == null) throw new ArgumentNullException("keys");

			var indexedResults = await trans.GetBatchIndexedAsync(FdbTuple.BatchPack(keys), snapshot, ct);

			ct.ThrowIfCancellationRequested();

			// maps the index back to the original key
			return indexedResults
				.Select((kvp) => new KeyValuePair<IFdbTuple, Slice>(keys[kvp.Key], kvp.Value))
				.ToList();
		}

		public static FdbRangeQuery GetRange(this FdbTransaction trans, IFdbTuple beginInclusive, IFdbTuple endExclusive, int limit = 0, bool snapshot = false, bool reverse = false)
		{
			Contract.Requires(trans != null);

			var begin = beginInclusive != null ? beginInclusive.ToSlice() : FdbKey.MinValue;
			var end = endExclusive != null ? endExclusive.ToSlice() : FdbKey.MaxValue;

			return trans.GetRange(
				FdbTransaction.ToSelector(begin),
				FdbTransaction.ToSelector(end),
				limit,
				0,
				FdbStreamingMode.WantAll,
				snapshot,
				reverse
			);
		}

		public static FdbRangeQuery GetRangeStartsWith(this FdbTransaction trans, IFdbTuple suffix, int limit = 0, bool snapshot = false, bool reverse = false)
		{
			Contract.Requires(trans != null);
			if (suffix == null) throw new ArgumentNullException("suffix");

			var range = suffix.ToRange();

			return trans.GetRange(
				FdbKeySelector.FirstGreaterOrEqual(range.Begin),
				FdbKeySelector.FirstGreaterThan(range.End),
				limit,
				0,
				FdbStreamingMode.WantAll,
				snapshot,
				reverse
			);
		}

		public static void Set(this FdbTransaction trans, IFdbTuple key, Slice valueBytes)
		{
			Contract.Requires(trans != null);
			if (key == null) throw new ArgumentNullException("key");

			trans.Set(key.ToSlice(), valueBytes);
		}

		public static void Clear(this FdbTransaction trans, IFdbTuple key)
		{
			Contract.Requires(trans != null);
			if (key == null) throw new ArgumentNullException("key");

			trans.Clear(key.ToSlice());
		}

		public static void ClearRange(this FdbTransaction trans, IFdbTuple beginInclusive, IFdbTuple endExclusive)
		{
			Contract.Requires(trans != null);
			if (beginInclusive == null) throw new ArgumentNullException("beginInclusive");
			if (endExclusive == null) throw new ArgumentNullException("endExclusive");

			trans.ClearRange(beginInclusive.ToSlice(), endExclusive.ToSlice());
		}

		public static void ClearRange(this FdbTransaction trans, IFdbTuple prefix)
		{
			Contract.Requires(trans != null);
			if (prefix == null) throw new ArgumentNullException("prefix");

			var range = prefix.ToRange();
			trans.ClearRange(range.Begin, range.End);
		}

		#endregion

	}

}
