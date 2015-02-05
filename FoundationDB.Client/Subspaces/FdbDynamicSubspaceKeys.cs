#region BSD Licence
/* Copyright (c) 2013-2015, Doxense SAS
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

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using FoundationDB.Client.Utils;
using FoundationDB.Layers.Tuples;
using JetBrains.Annotations;

namespace FoundationDB.Client
{

	internal static class Batched<TValue, TState>
	{

		public delegate void Handler(ref SliceWriter writer, TValue item, TState protocol);

		[NotNull]
		public static Slice[] Convert(SliceWriter writer, [NotNull, ItemNotNull] IEnumerable<TValue> values, Handler handler, TState state)
		{
			Contract.Requires(values != null && handler != null);

			//Note on performance: 
			// - we will reuse the same buffer for each temp key, and copy them into a slice buffer
			// - doing it this way adds a memory copy (writer => buffer) but reduce the number of byte[] allocations (and reduce the GC overhead)

			int start = writer.Position;

			var buffer = new SliceBuffer();

			var coll = values as ICollection<TValue>;
			if (coll != null)
			{ // pre-allocate the final array with the correct size
				var res = new Slice[coll.Count];
				int p = 0;
				foreach (var tuple in coll)
				{
					// reset position to just after the subspace prefix
					writer.Position = start;

					handler(ref writer, tuple, state);

					// copy full key in the buffer
					res[p++] = buffer.Intern(writer.ToSlice());
				}
				Contract.Assert(p == res.Length);
				return res;
			}
			else
			{ // we won't now the array size until the end...
				var res = new List<Slice>();
				foreach (var tuple in values)
				{
					// reset position to just after the subspace prefix
					writer.Position = start;

					handler(ref writer, tuple, state);

					// copy full key in the buffer
					res.Add(buffer.Intern(writer.ToSlice()));
				}
				return res.ToArray();
			}
		}
	}

	/// <summary>Key helper for a dynamic TypeSystem</summary>
	public struct FdbDynamicSubspaceKeys
	{
		//NOTE: everytime an IFdbTuple is used here, it is as a container (vector of objects), and NOT as the Tuple Encoding scheme ! (separate concept)

		[NotNull]
		public readonly IFdbSubspace Subspace;

		[NotNull]
		public readonly IFdbTypeSystem Protocol;

		public FdbDynamicSubspaceKeys([NotNull] IFdbSubspace subspace, [NotNull] IFdbTypeSystem protocol)
		{
			Contract.Requires(subspace != null && protocol != null);
			this.Subspace = subspace;
			this.Protocol = protocol;
		}

		public FdbKeyRange ToRange()
		{
			return this.Protocol.ToRange(this.Subspace.Key);
		}

		public FdbKeyRange ToRange([NotNull] IFdbTuple tuple)
		{
			return this.Protocol.ToRange(Pack(tuple));
		}

		public FdbKeyRange ToRange([NotNull] ITupleFormattable tuple)
		{
			return this.Protocol.ToRange(Pack(tuple));
		}

		public Slice this[[NotNull] IFdbTuple tuple]
		{
			get { return Pack(tuple); }
		}

		public Slice this[[NotNull] ITupleFormattable item]
		{
			get { return Pack(item); }
		}

		public Slice Pack([NotNull] IFdbTuple tuple)
		{
			if (tuple == null) throw new ArgumentNullException("tuple");

			var writer = this.Subspace.GetWriter();
			this.Protocol.PackKey(ref writer, tuple);
			return writer.ToSlice();
		}

		public Slice Pack([NotNull] ITupleFormattable item)
		{
			if (item == null) throw new ArgumentNullException("item");
			return Pack(item.ToTuple());
		}

		public Slice[] Pack([NotNull, ItemNotNull] IEnumerable<IFdbTuple> tuples)
		{
			if (tuples == null) throw new ArgumentNullException("tuples");

			return Batched<IFdbTuple, IFdbTypeSystem>.Convert(
				this.Subspace.GetWriter(),
				tuples,
				(ref SliceWriter writer, IFdbTuple tuple, IFdbTypeSystem protocol) => protocol.PackKey(ref writer, tuple),
				this.Protocol
			);
		}

		public Slice Encode<T>(T item1)
		{
			var writer = this.Subspace.GetWriter();
			this.Protocol.EncodeKey(ref writer, item1);
			return writer.ToSlice();
		}

		public Slice[] Encode<T>(IEnumerable<T> items)
		{
			return Batched<T, IFdbTypeSystem>.Convert(
				this.Subspace.GetWriter(),
				items,
				(ref SliceWriter writer, T item, IFdbTypeSystem protocol) => protocol.EncodeKey<T>(ref writer, item),
				this.Protocol
            );
		}

		public Slice[] Encode<TSource, T>(IEnumerable<TSource> items, Func<TSource, T> selector)
		{
			return Batched<TSource, IFdbTypeSystem>.Convert(
				this.Subspace.GetWriter(),
				items,
				(ref SliceWriter writer, TSource item, IFdbTypeSystem protocol) => protocol.EncodeKey<T>(ref writer, selector(item)),
				this.Protocol
			);
		}

		public Slice Encode<T1, T2>(T1 item1, T2 item2)
		{
			var writer = this.Subspace.GetWriter();
			this.Protocol.EncodeKey(ref writer, item1, item2);
			return writer.ToSlice();
		}

		public Slice[] Encode<TItem, T1, T2>(IEnumerable<TItem> items, Func<TItem, T1> selector1, Func<TItem, T2> selector2)
		{
			return Batched<TItem, IFdbTypeSystem>.Convert(
				this.Subspace.GetWriter(),
				items,
				(ref SliceWriter writer, TItem item, IFdbTypeSystem protocol) => protocol.EncodeKey<T1, T2>(ref writer, selector1(item), selector2(item)),
				this.Protocol
			);
		}

		public Slice Encode<T1, T2, T3>(T1 item1, T2 item2, T3 item3)
		{
			var writer = this.Subspace.GetWriter();
			this.Protocol.EncodeKey(ref writer, item1, item2, item3);
			return writer.ToSlice();
		}

		public Slice Encode<T1, T2, T3, T4>(T1 item1, T2 item2, T3 item3, T4 item4)
		{
			var writer = this.Subspace.GetWriter();
			this.Protocol.EncodeKey(ref writer, item1, item2, item3, item4);
			return writer.ToSlice();
		}

		public Slice Encode<T1, T2, T3, T4, T5>(T1 item1, T2 item2, T3 item3, T4 item4, T5 item5)
		{
			var writer = this.Subspace.GetWriter();
			this.Protocol.EncodeKey(ref writer, item1, item2, item3, item4, item5);
			return writer.ToSlice();
		}

		public Slice Encode<T1, T2, T3, T4, T5, T6>(T1 item1, T2 item2, T3 item3, T4 item4, T5 item5, T6 item6)
		{
			var writer = this.Subspace.GetWriter();
			this.Protocol.EncodeKey(ref writer, item1, item2, item3, item4, item5, item6);
			return writer.ToSlice();
		}

		public Slice Encode<T1, T2, T3, T4, T5, T6, T7>(T1 item1, T2 item2, T3 item3, T4 item4, T5 item5, T6 item6, T7 item7)
		{
			var writer = this.Subspace.GetWriter();
			this.Protocol.EncodeKey(ref writer, item1, item2, item3, item4, item5, item6, item7);
			return writer.ToSlice();
		}

		public Slice Encode<T1, T2, T3, T4, T5, T6, T7, T8>(T1 item1, T2 item2, T3 item3, T4 item4, T5 item5, T6 item6, T7 item7, T8 item8)
		{
			var writer = this.Subspace.GetWriter();
			this.Protocol.EncodeKey(ref writer, item1, item2, item3, item4, item5, item6, item7, item8);
			return writer.ToSlice();
		}

		public IFdbTuple Unpack(Slice packed)
		{
			return this.Protocol.UnpackKey(this.Subspace.ExtractKey(packed));
		}

		private static T[] BatchDecode<T>(IEnumerable<Slice> packed, IFdbSubspace subspace, IFdbTypeSystem protocol, Func<Slice, IFdbTypeSystem, T> decode)
		{
			var coll = packed as ICollection<Slice>;
			if (coll != null)
			{
				var res = new T[coll.Count];
				int p = 0;
				foreach (var data in packed)
				{
					res[p++] = decode(subspace.ExtractKey(data), protocol);
				}
				Contract.Assert(p == res.Length);
				return res;
			}
			else
			{
				var res = new List<T>();
				foreach (var data in packed)
				{
					res.Add(decode(subspace.ExtractKey(data), protocol));
				}
				return res.ToArray();
			}
		}

		public IFdbTuple[] Unpack(IEnumerable<Slice> packed)
		{
			return BatchDecode(packed, this.Subspace, this.Protocol, (data, protocol) => protocol.UnpackKey(data));
		}

		public T1 Decode<T1>(Slice packed)
		{
			return this.Protocol.DecodeKey<T1>(this.Subspace.ExtractKey(packed));
		}

		public IEnumerable<T1> Decode<T1>(IEnumerable<Slice> packed)
		{
			return BatchDecode(packed, this.Subspace, this.Protocol, (data, protocol) => protocol.DecodeKey<T1>(data));
		}

		public FdbTuple<T1, T2> Decode<T1, T2>(Slice packed)
		{
			return this.Protocol.DecodeKey<T1, T2>(this.Subspace.ExtractKey(packed));
		}

		public IEnumerable<FdbTuple<T1, T2>> Decode<T1, T2>(IEnumerable<Slice> packed)
		{
			return BatchDecode(packed, this.Subspace, this.Protocol, (data, protocol) => protocol.DecodeKey<T1, T2>(data));
		}

		public FdbTuple<T1, T2, T3> Decode<T1, T2, T3>(Slice packed)
		{
			return this.Protocol.DecodeKey<T1, T2, T3>(this.Subspace.ExtractKey(packed));
		}

		public IEnumerable<FdbTuple<T1, T2, T3>> Decode<T1, T2, T3>(IEnumerable<Slice> packed)
		{
			return BatchDecode(packed, this.Subspace, this.Protocol, (data, protocol) => protocol.DecodeKey<T1, T2, T3>(data));
		}

		public FdbTuple<T1, T2, T3, T4> Decode<T1, T2, T3, T4>(Slice packed)
		{
			return this.Protocol.DecodeKey<T1, T2, T3, T4>(this.Subspace.ExtractKey(packed));
		}

		public IEnumerable<FdbTuple<T1, T2, T3, T4>> Decode<T1, T2, T3, T4>(IEnumerable<Slice> packed)
		{
			return BatchDecode(packed, this.Subspace, this.Protocol, (data, protocol) => protocol.DecodeKey<T1, T2, T3, T4>(data));
		}

		public FdbTuple<T1, T2, T3, T4, T5> Decode<T1, T2, T3, T4, T5>(Slice packed)
		{
			return this.Protocol.DecodeKey<T1, T2, T3, T4, T5>(this.Subspace.ExtractKey(packed));
		}

		public IEnumerable<FdbTuple<T1, T2, T3, T4, T5>> Decode<T1, T2, T3, T4, T5>(IEnumerable<Slice> packed)
		{
			return BatchDecode(packed, this.Subspace, this.Protocol, (data, protocol) => protocol.DecodeKey<T1, T2, T3, T4, T5>(data));
		}

		public T DecodeFirst<T>(Slice packed)
		{
			return this.Protocol.DecodeKeyFirst<T>(this.Subspace.ExtractKey(packed));
		}
		public IEnumerable<T> DecodeFirst<T>(IEnumerable<Slice> packed)
		{
			return BatchDecode(packed, this.Subspace, this.Protocol, (data, protocol) => protocol.DecodeKeyFirst<T>(data));
		}

		public T DecodeLast<T>(Slice packed)
		{
			return this.Protocol.DecodeKeyLast<T>(this.Subspace.ExtractKey(packed));
		}
		public IEnumerable<T> DecodeLast<T>(Slice[] packed)
		{
			return BatchDecode(packed, this.Subspace, this.Protocol, (data, protocol) => protocol.DecodeKeyLast<T>(data));
		}

		#region Append: Subspace => Tuple

		/// <summary>Return an empty tuple that is attached to this subspace</summary>
		/// <returns>Empty tuple that can be extended, and whose packed representation will always be prefixed by the subspace key</returns>
		[NotNull]
		public IFdbTuple ToTuple()
		{
			return new FdbPrefixedTuple(this.Subspace.Key, FdbTuple.Empty);
		}

		/// <summary>Attach a tuple to an existing subspace.</summary>
		/// <param name="tuple">Tuple whose items will be appended at the end of the current subspace</param>
		/// <returns>Tuple that wraps the items of <paramref name="tuple"/> and whose packed representation will always be prefixed by the subspace key.</returns>
		[NotNull]
		public IFdbTuple Concat([NotNull] IFdbTuple tuple)
		{
			return new FdbPrefixedTuple(this.Subspace.Key, tuple);
		}

		/// <summary>Convert a formattable item into a tuple that is attached to this subspace.</summary>
		/// <param name="formattable">Item that can be converted into a tuple</param>
		/// <returns>Tuple that is the logical representation of the item, and whose packed representation will always be prefixed by the subspace key.</returns>
		/// <remarks>This is the equivalent of calling 'subspace.Create(formattable.ToTuple())'</remarks>
		[NotNull]
		public IFdbTuple Concat([NotNull] ITupleFormattable formattable)
		{
			if (formattable == null) throw new ArgumentNullException("formattable");
			var tuple = formattable.ToTuple();
			if (tuple == null) throw new InvalidOperationException("Formattable item cannot return an empty tuple");
			return new FdbPrefixedTuple(this.Subspace.Key, tuple);
		}

		/// <summary>Create a new 1-tuple that is attached to this subspace</summary>
		/// <typeparam name="T">Type of the value to append</typeparam>
		/// <param name="value">Value that will be appended</param>
		/// <returns>Tuple of size 1 that contains <paramref name="value"/>, and whose packed representation will always be prefixed by the subspace key.</returns>
		/// <remarks>This is the equivalent of calling 'subspace.Create(FdbTuple.Create&lt;T&gt;(value))'</remarks>
		[NotNull]
		public IFdbTuple Append<T>(T value)
		{
			return new FdbPrefixedTuple(this.Subspace.Key, FdbTuple.Create<T>(value));
		}

		/// <summary>Create a new 2-tuple that is attached to this subspace</summary>
		/// <typeparam name="T1">Type of the first value to append</typeparam>
		/// <typeparam name="T2">Type of the second value to append</typeparam>
		/// <param name="item1">First value that will be appended</param>
		/// <param name="item2">Second value that will be appended</param>
		/// <returns>Tuple of size 2 that contains <paramref name="item1"/> and <paramref name="item2"/>, and whose packed representation will always be prefixed by the subspace key.</returns>
		/// <remarks>This is the equivalent of calling 'subspace.Create(FdbTuple.Create&lt;T1, T2&gt;(item1, item2))'</remarks>
		[NotNull]
		public IFdbTuple Append<T1, T2>(T1 item1, T2 item2)
		{
			return new FdbPrefixedTuple(this.Subspace.Key, FdbTuple.Create<T1, T2>(item1, item2));
		}

		/// <summary>Create a new 3-tuple that is attached to this subspace</summary>
		/// <typeparam name="T1">Type of the first value to append</typeparam>
		/// <typeparam name="T2">Type of the second value to append</typeparam>
		/// <typeparam name="T3">Type of the third value to append</typeparam>
		/// <param name="item1">First value that will be appended</param>
		/// <param name="item2">Second value that will be appended</param>
		/// <param name="item3">Third value that will be appended</param>
		/// <returns>Tuple of size 3 that contains <paramref name="item1"/>, <paramref name="item2"/> and <paramref name="item3"/>, and whose packed representation will always be prefixed by the subspace key.</returns>
		/// <remarks>This is the equivalent of calling 'subspace.Create(FdbTuple.Create&lt;T1, T2, T3&gt;(item1, item2, item3))'</remarks>
		[NotNull]
		public IFdbTuple Append<T1, T2, T3>(T1 item1, T2 item2, T3 item3)
		{
			return new FdbPrefixedTuple(this.Subspace.Key, FdbTuple.Create<T1, T2, T3>(item1, item2, item3));
		}

		/// <summary>Create a new 4-tuple that is attached to this subspace</summary>
		/// <typeparam name="T1">Type of the first value to append</typeparam>
		/// <typeparam name="T2">Type of the second value to append</typeparam>
		/// <typeparam name="T3">Type of the third value to append</typeparam>
		/// <typeparam name="T4">Type of the fourth value to append</typeparam>
		/// <param name="item1">First value that will be appended</param>
		/// <param name="item2">Second value that will be appended</param>
		/// <param name="item3">Third value that will be appended</param>
		/// <param name="item4">Fourth value that will be appended</param>
		/// <returns>Tuple of size 4 that contains <paramref name="item1"/>, <paramref name="item2"/>, <paramref name="item3"/> and <paramref name="item4"/>, and whose packed representation will always be prefixed by the subspace key.</returns>
		/// <remarks>This is the equivalent of calling 'subspace.Create(FdbTuple.Create&lt;T1, T2, T3, T4&gt;(item1, item2, item3, item4))'</remarks>
		[NotNull]
		public IFdbTuple Append<T1, T2, T3, T4>(T1 item1, T2 item2, T3 item3, T4 item4)
		{
			return new FdbPrefixedTuple(this.Subspace.Key, FdbTuple.Create<T1, T2, T3, T4>(item1, item2, item3, item4));
		}

		/// <summary>Create a new 5-tuple that is attached to this subspace</summary>
		/// <typeparam name="T1">Type of the first value to append</typeparam>
		/// <typeparam name="T2">Type of the second value to append</typeparam>
		/// <typeparam name="T3">Type of the third value to append</typeparam>
		/// <typeparam name="T4">Type of the fourth value to append</typeparam>
		/// <typeparam name="T5">Type of the fifth value to append</typeparam>
		/// <param name="item1">First value that will be appended</param>
		/// <param name="item2">Second value that will be appended</param>
		/// <param name="item3">Third value that will be appended</param>
		/// <param name="item4">Fourth value that will be appended</param>
		/// <param name="item5">Fifth value that will be appended</param>
		/// <returns>Tuple of size 5 that contains <paramref name="item1"/>, <paramref name="item2"/>, <paramref name="item3"/>, <paramref name="item4"/> and <paramref name="item5"/>, and whose packed representation will always be prefixed by the subspace key.</returns>
		/// <remarks>This is the equivalent of calling 'subspace.Create(FdbTuple.Create&lt;T1, T2, T3, T4, T5&gt;(item1, item2, item3, item4, item5))'</remarks>
		[NotNull]
		public IFdbTuple Append<T1, T2, T3, T4, T5>(T1 item1, T2 item2, T3 item3, T4 item4, T5 item5)
		{
			return new FdbPrefixedTuple(this.Subspace.Key, FdbTuple.Create<T1, T2, T3, T4, T5>(item1, item2, item3, item4, item5));
		}

		#endregion

	}
}