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
		public static void Set(this FdbTransaction transaction, IFdbTuple tuple, byte[] value)
		{
			transaction.Set(tuple.ToArraySegment(), new ArraySegment<byte>(value));
		}

		public static void Set(this FdbTransaction transaction, IFdbTuple tuple, string value)
		{
			transaction.Set(tuple.ToArraySegment(), FdbCore.GetValueBytes(value));
		}

		public static Task<byte[]> GetAsync(this FdbTransaction transaction, IFdbTuple tuple, bool snapshot = false, CancellationToken ct = default(CancellationToken))
		{
			return transaction.GetAsync(tuple.ToArraySegment(), snapshot, ct);
		}

		public static byte[] Get(this FdbTransaction transaction, IFdbTuple tuple, bool snapshot = false, CancellationToken ct = default(CancellationToken))
		{
			return transaction.Get(tuple.ToArraySegment(), snapshot, ct);
		}

		public static ArraySegment<byte> ToArraySegment(this IFdbKey tuple)
		{
			var writer = new BinaryWriteBuffer();
			tuple.PackTo(writer);
			return writer.ToArraySegment();
		}

		public static byte[] ToBytes(this IFdbKey tuple)
		{
			var writer = new BinaryWriteBuffer();
			tuple.PackTo(writer);
			return writer.GetBytes();
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

	}

}
