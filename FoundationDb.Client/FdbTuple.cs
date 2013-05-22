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

using FoundationDb.Client.Tuples;
using FoundationDb.Client.Utils;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;

namespace FoundationDb.Client
{

	/// <summary>Factory class for Tuples</summary>
	public static class FdbTuple
	{
		/// <summary>Empty tuple</summary>
		/// <remarks>Not to be mistaken with a 1-tuple containing 'null' !</remarks>
		public static readonly IFdbTuple Empty = new EmptyTuple();

		/// <summary>Empty tuple (singleton that is used as a base for other tuples)</summary>
		internal sealed class EmptyTuple : IFdbTuple
		{

			public int Count
			{
				get { return 0; }
			}

			object IFdbTuple.this[int index]
			{
				get { throw new IndexOutOfRangeException(); }
			}

			public R Get<R>(int index)
			{
				throw new IndexOutOfRangeException();
			}

			IFdbTuple IFdbTuple.Append<T1>(T1 value)
			{
				return this.Append<T1>(value);
			}

			public FdbTuple<T1> Append<T1>(T1 value)
			{
				return new FdbTuple<T1>(value);
			}

			public IFdbTuple AppendRange(IFdbTuple value)
			{
				return value;
			}

			public void PackTo(FdbBufferWriter writer)
			{
				//NO-OP
			}

			public Slice ToSlice()
			{
				return Slice.Empty;
			}

			public void CopyTo(object[] array, int offset)
			{
				//NO-OP
			}

			public IEnumerator<object> GetEnumerator()
			{
				yield break;
			}

			System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
			{
				return this.GetEnumerator();
			}

			public override string ToString()
			{
				return "()";
			}

			public override int GetHashCode()
			{
				return 0;
			}

		}

		/// <summary>Create a new 1-tuple, holding only one item</summary>
		public static FdbTuple<T1> Create<T1>(T1 item1)
		{
			return new FdbTuple<T1>(item1);
		}

		/// <summary>Create a new 2-tuple, holding two items</summary>
		public static FdbTuple<T1, T2> Create<T1, T2>(T1 item1, T2 item2)
		{
			return new FdbTuple<T1, T2>(item1, item2);
		}

		/// <summary>Create a new 3-tuple, holding three items</summary>
		public static FdbTuple<T1, T2, T3> Create<T1, T2, T3>(T1 item1, T2 item2, T3 item3)
		{
			return new FdbTuple<T1, T2, T3>(item1, item2, item3);
		}

		/// <summary>Create a new 4-tuple, holding four items</summary>
		public static FdbTuple<T1, T2, T3, T4> Create<T1, T2, T3, T4>(T1 item1, T2 item2, T3 item3, T4 item4)
		{
			return new FdbTuple<T1, T2, T3, T4>(item1, item2, item3, item4);
		}

		/// <summary>Create a new 5-tuple, holding five items</summary>
		public static FdbTuple<T1, T2, T3, T4, T5> Create<T1, T2, T3, T4, T5>(T1 item1, T2 item2, T3 item3, T4 item4, T5 item5)
		{
			return new FdbTuple<T1, T2, T3, T4, T5>(item1, item2, item3, item4, item5);
		}

		/// <summary>Create a new N-tuple, from N items</summary>
		public static FdbTupleList Create(params object[] items)
		{
			var list = new List<object>(items);
			return new FdbTupleList(list);
		}

		/// <summary>Create a new N-tuple from a sequence of items</summary>
		public static FdbTupleList Create(IEnumerable<object> items)
		{
			if (items == null) throw new ArgumentNullException("items");

			var tuple = items as FdbTupleList;
			if (tuple == null)
			{
				tuple = new FdbTupleList(new List<object>(items));
			}
			return tuple;
		}

		internal static string ToString(IEnumerable<object> items)
		{
			if (items == null) return String.Empty;
			using (var enumerator = items.GetEnumerator())
			{
				if (!enumerator.MoveNext()) return "()";

				var sb = new StringBuilder().Append('(').Append(enumerator.Current);
				while (enumerator.MoveNext())
				{
					sb.Append(", ").Append(enumerator.Current);
				}

				return sb.Append(')').ToString();
			}
		}

		/// <summary>Pack a sequence of N-tuples, all sharing the same buffer</summary>
		/// <param name="tuples">Sequence of N-tuples to pack</param>
		/// <returns>Array containing the buffer segment of each packed tuple</returns>
		/// <example>BatchPack([ ("Foo", 1), ("Foo", 2) ]) => [ "\x02Foo\x00\x15\x01", "\x02Foo\x00\x15\x02" ] </example>
		public static Slice[] BatchPack(IEnumerable<IFdbTuple> tuples)
		{
			var next = new List<int>();
			var writer = new FdbBufferWriter();

			//TODO: use multiple buffers if item count is huge ?

			foreach(var tuple in tuples)
			{
				tuple.PackTo(writer);
				next.Add(writer.Position);
			}

			return FdbKey.SplitIntoSegments(writer.Buffer, 0, next);
		}

		/// <summary>Unpack a tuple from a serialied key blob</summary>
		/// <param name="packedKey">Binary key containing a previously packed tuple</param>
		/// <returns>Unpacked tuple</returns>
		public static IFdbTuple Unpack(Slice packedKey)
		{
			if (!packedKey.HasValue) return null;
			if (packedKey.IsEmpty) return FdbTuple.Empty;

			return FdbTuplePackers.Unpack(packedKey);
		}

		internal static void CopyTo(IFdbTuple tuple, object[] array, int offset)
		{
			Contract.Requires(tuple != null);
			Contract.Requires(array != null);
			Contract.Requires(offset >= 0);

			foreach (var item in tuple)
			{
				array[offset++] = item;
			}
		}

		internal static int MapIndex(int index, int count)
		{
			if (index < 0) index += count;
			if (index >= 0 && index < count) return index;
			throw new ArgumentOutOfRangeException("count");		
		}

	}

}
