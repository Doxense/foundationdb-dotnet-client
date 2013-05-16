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
	* Neither the name of the <organization> nor the
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

using FoundationDb.Client.Utils;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace FoundationDb.Client.Tuples
{

	public static class FdbTupleExtensions
	{
		public static void Set(this FdbTransaction transaction, IFdbTuple tuple, Slice value)
		{
			transaction.Set(tuple.ToSlice(), value);
		}

		public static void Clear(this FdbTransaction transaction, IFdbTuple tuple)
		{
			transaction.Clear(tuple.ToSlice());
		}

		public static void ClearRange(this FdbTransaction transaction, IFdbTuple beginInclusive, IFdbTuple endExclusive)
		{
			transaction.ClearRange(beginInclusive.ToSlice(), endExclusive.ToSlice());
		}

		public static void ClearRange(this FdbTransaction transaction, IFdbTuple prefix)
		{
			var range = prefix.ToRange();
			transaction.ClearRange(range.Begin, range.End);
		}

		public static Task<Slice> GetAsync(this FdbTransaction transaction, IFdbTuple tuple, bool snapshot = false, CancellationToken ct = default(CancellationToken))
		{
			return transaction.GetAsync(tuple.ToSlice(), snapshot, ct);
		}

		public static Slice Get(this FdbTransaction transaction, IFdbTuple tuple, bool snapshot = false, CancellationToken ct = default(CancellationToken))
		{
			return transaction.Get(tuple.ToSlice(), snapshot, ct);
		}

		public static byte[] ToByteArray(this IFdbTuple tuple)
		{
			var writer = new FdbBufferWriter();
			tuple.PackTo(writer);
			return writer.ToByteArray();
		}

		public static IFdbTuple Concat(this IFdbTuple first, IFdbTuple second)
		{
			if (second.Count == 0) return first;
			if (first.Count == 0) return second;

			var firstList = first as FdbTupleList;
			if (firstList != null)
			{ // optimized path
				return firstList.Concat(second);
			}

			// create a new list with both
			var list = new List<object>(first.Count + second.Count);
			list.AddRange(first);
			list.AddRange(second);
			return new FdbTupleList(list);
		}

		public static IFdbTuple AppendRange(this IFdbTuple tuple, params object[] items)
		{
			if (items == null) throw new ArgumentNullException("items");
			if (items.Length == 0) return tuple;

			var tupleList = tuple as FdbTupleList;
			if (tupleList != null) return tupleList.AppendRange(items);

			var list = new List<object>(tuple.Count + tuple.Count);
			list.AddRange(tuple);
			list.AddRange(items);
			return new FdbTupleList(list);
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

		public static Task<FdbSearchResults> GetRangeAsync(this FdbTransaction transaction, IFdbTuple suffix, bool snapshot = false, CancellationToken ct = default(CancellationToken))
		{
			var range = suffix.ToRange();

			return transaction.GetRangeAsync(
				FdbKeySelector.FirstGreaterOrEqual(range.Begin),
				FdbKeySelector.FirstGreaterThan(range.End),
				snapshot,
				ct
			);
		}

	}

}
