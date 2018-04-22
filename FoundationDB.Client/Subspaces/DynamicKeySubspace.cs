#region BSD Licence
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

namespace FoundationDB.Client
{
	using System;
	using System.Collections.Generic;
	using System.Linq;
	using Doxense.Collections.Tuples;
	using Doxense.Collections.Tuples.Encoding;
	using Doxense.Diagnostics.Contracts;
	using Doxense.Memory;
	using JetBrains.Annotations;

	public class DynamicKeySubspace : KeySubspace, IDynamicKeySubspace
	{
		/// <summary>Create a new subspace from a binary prefix</summary>
		/// <param name="rawPrefix">Prefix of the new subspace</param>
		/// <param name="copy">If true, take a copy of the prefix</param>
		/// <param name="encoder">Type System used to encode keys in this subspace (optional, will use Tuple Encoding by default)</param>
		internal DynamicKeySubspace(Slice rawPrefix, bool copy, IDynamicKeyEncoder encoder)
			:  base (rawPrefix, copy)
		{
			this.Encoder = encoder ?? TypeSystem.Default.GetDynamicEncoder();
			this.Keys = new DynamicKeys(this, this.Encoder);
			this.Partition = new DynamicPartition(this, this.Encoder);
		}

		public DynamicKeySubspace(Slice rawPrefix, IDynamicKeyEncoder encoder)
			: this(rawPrefix, true, encoder)
		{ }

		protected override IKeySubspace CreateChildren(Slice suffix)
		{
			return new DynamicKeySubspace(ConcatKey(suffix), this.Encoder);
		}

		/// <summary>Encoder for the keys of this subspace</summary>
		public IDynamicKeyEncoder Encoder { get; }

		/// <summary>Return a view of all the possible binary keys of this subspace</summary>
		public DynamicKeys Keys { get; }

		/// <summary>Returns an helper object that knows how to create sub-partitions of this subspace</summary>
		public DynamicPartition Partition { get; }

	}

	/// <summary>Key helper for a dynamic TypeSystem</summary>
	public /*readonly*/ struct DynamicKeys
	{
		//NOTE: everytime an ITuple is used here, it is as a container (vector of objects), and NOT as the Tuple Encoding scheme ! (separate concept)

		/// <summary>Parent subspace</summary>
		[NotNull]
		public readonly IKeySubspace Subspace;

		/// <summary>Encoder used to format keys in this subspace</summary>
		[NotNull]
		public readonly IDynamicKeyEncoder Encoder;

		public DynamicKeys([NotNull] IKeySubspace subspace, [NotNull] IDynamicKeyEncoder encoder)
		{
			Contract.Requires(subspace != null && encoder != null);
			this.Subspace = subspace;
			this.Encoder = encoder;
		}

		/// <summary>Return a key range that encompass all the keys inside this subspace, according to the current key encoder</summary>
		public KeyRange ToRange()
		{
			return this.Encoder.ToRange(this.Subspace.GetPrefix());
		}

		/// <summary>Return a key range that encompass all the keys inside a partition of this subspace, according to the current key encoder</summary>
		/// <param name="tuple">Tuple used as a prefix for the range</param>
		public KeyRange ToRange([NotNull] ITuple tuple)
		{
			return this.Encoder.ToRange(Pack(tuple));
		}

		/// <summary>Return a key range that encompass all the keys inside a partition of this subspace, according to the current key encoder</summary>
		/// <param name="item">Convertible item used as a prefix for the range</param>
		public KeyRange ToRange([NotNull] ITupleFormattable item)
		{
			return this.Encoder.ToRange(Pack(item));
		}

		/// <summary>Convert a tuple into a key of this subspace</summary>
		/// <param name="tuple">Tuple that will be packed and appended to the subspace prefix</param>
		/// <remarks>This is a shortcut for <see cref="Pack(Doxense.Collections.Tuples.ITuple)"/></remarks>
		public Slice this[[NotNull] ITuple tuple] => Pack(tuple);

		/// <summary>Convert an item into a key of this subspace</summary>
		/// <param name="item">Convertible item that will be packed and appended to the subspace prefix</param>
		/// <remarks>This is a shortcut for <see cref="Pack(ITupleFormattable)"/></remarks>
		public Slice this[[NotNull] ITupleFormattable item] => Pack(item);

		/// <summary>Convert a tuple into a key of this subspace</summary>
		/// <param name="tuple">Tuple that will be packed and appended to the subspace prefix</param>
		public Slice Pack([NotNull] ITuple tuple)
		{
			Contract.NotNull(tuple, nameof(tuple));

			var writer = this.Subspace.GetWriter();
			this.Encoder.PackKey(ref writer, tuple);
			return writer.ToSlice();
		}

		/// <summary>Convert a batch of tuples into keys of this subspace, in an optimized way.</summary>
		/// <param name="tuples">Sequence of tuple that will be packed and appended to the subspace prefix</param>
		public Slice[] PackMany([NotNull, ItemNotNull] IEnumerable<ITuple> tuples)
		{
			if (tuples == null) throw new ArgumentNullException(nameof(tuples));

			return Batched<ITuple, IDynamicKeyEncoder>.Convert(
				this.Subspace.GetWriter(),
				tuples,
				(ref SliceWriter writer, ITuple tuple, IDynamicKeyEncoder encoder) => encoder.PackKey(ref writer, tuple),
				this.Encoder
			);
		}

		/// <summary>Convert an item into a key of this subspace</summary>
		/// <param name="item">Convertible item that will be packed and appended to the subspace prefix</param>
		public Slice Pack([NotNull] ITupleFormattable item)
		{
			if (item == null) throw new ArgumentNullException(nameof(item));

			return Pack(item.ToTuple());
		}

		/// <summary>Convert a batch of items into keys of this subspace, in an optimized way.</summary>
		/// <param name="items">Sequence of convertible items that will be packed and appended to the subspace prefix</param>
		public Slice[] PackMany([NotNull, ItemNotNull] IEnumerable<ITupleFormattable> items)
		{
			if (items == null) throw new ArgumentNullException(nameof(items));

			return Batched<ITuple, IDynamicKeyEncoder>.Convert(
				this.Subspace.GetWriter(),
				items.Select(item => item.ToTuple()),
				(ref SliceWriter writer, ITuple tuple, IDynamicKeyEncoder encoder) => encoder.PackKey(ref writer, tuple),
				this.Encoder
			);
		}

		/// <summary>Encode a key which is composed of a single element</summary>
		public Slice Encode<T>(T item1)
		{
			var writer = this.Subspace.GetWriter();
			this.Encoder.EncodeKey(ref writer, item1);
			return writer.ToSlice();
		}

		/// <summary>Encode a batch of keys, each one composed of a single element</summary>
		public Slice[] EncodeMany<T>(IEnumerable<T> items)
		{
			return Batched<T, IDynamicKeyEncoder>.Convert(
				this.Subspace.GetWriter(),
				items,
				(ref SliceWriter writer, T item, IDynamicKeyEncoder encoder) => encoder.EncodeKey<T>(ref writer, item),
				this.Encoder
			);
		}

		/// <summary>Encode a batch of keys, each one composed of a single value extracted from each elements</summary>
		public Slice[] EncodeMany<TSource, T>(IEnumerable<TSource> items, Func<TSource, T> selector)
		{
			return Batched<TSource, IDynamicKeyEncoder>.Convert(
				this.Subspace.GetWriter(),
				items,
				(ref SliceWriter writer, TSource item, IDynamicKeyEncoder encoder) => encoder.EncodeKey<T>(ref writer, selector(item)),
				this.Encoder
			);
		}

		/// <summary>Encode a key which is composed of a two elements</summary>
		public Slice Encode<T1, T2>(T1 item1, T2 item2)
		{
			var writer = this.Subspace.GetWriter();
			this.Encoder.EncodeKey(ref writer, item1, item2);
			return writer.ToSlice();
		}

		/// <summary>Encode a batch of keys, each one composed of two values extracted from each elements</summary>
		public Slice[] EncodeMany<TItem, T1, T2>(IEnumerable<TItem> items, Func<TItem, T1> selector1, Func<TItem, T2> selector2)
		{
			return Batched<TItem, IDynamicKeyEncoder>.Convert(
				this.Subspace.GetWriter(),
				items,
				(ref SliceWriter writer, TItem item, IDynamicKeyEncoder encoder) => encoder.EncodeKey<T1, T2>(ref writer, selector1(item), selector2(item)),
				this.Encoder
			);
		}

		/// <summary>Encode a key which is composed of three elements</summary>
		public Slice Encode<T1, T2, T3>(T1 item1, T2 item2, T3 item3)
		{
			var writer = this.Subspace.GetWriter();
			this.Encoder.EncodeKey(ref writer, item1, item2, item3);
			return writer.ToSlice();
		}

		/// <summary>Encode a batch of keys, each one composed of three values extracted from each elements</summary>
		public Slice[] EncodeMany<TItem, T1, T2, T3>(IEnumerable<TItem> items, Func<TItem, T1> selector1, Func<TItem, T2> selector2, Func<TItem, T3> selector3)
		{
			return Batched<TItem, IDynamicKeyEncoder>.Convert(
				this.Subspace.GetWriter(),
				items,
				(ref SliceWriter writer, TItem item, IDynamicKeyEncoder encoder) => encoder.EncodeKey<T1, T2, T3>(ref writer, selector1(item), selector2(item), selector3(item)),
				this.Encoder
			);
		}

		/// <summary>Encode a key which is composed of four elements</summary>
		public Slice Encode<T1, T2, T3, T4>(T1 item1, T2 item2, T3 item3, T4 item4)
		{
			var writer = this.Subspace.GetWriter();
			this.Encoder.EncodeKey(ref writer, item1, item2, item3, item4);
			return writer.ToSlice();
		}

		/// <summary>Encode a batch of keys, each one composed of four values extracted from each elements</summary>
		public Slice[] EncodeMany<TItem, T1, T2, T3, T4>(IEnumerable<TItem> items, Func<TItem, T1> selector1, Func<TItem, T2> selector2, Func<TItem, T3> selector3, Func<TItem, T4> selector4)
		{
			return Batched<TItem, IDynamicKeyEncoder>.Convert(
				this.Subspace.GetWriter(),
				items,
				(ref SliceWriter writer, TItem item, IDynamicKeyEncoder encoder) => encoder.EncodeKey<T1, T2, T3, T4>(ref writer, selector1(item), selector2(item), selector3(item), selector4(item)),
				this.Encoder
			);
		}

		/// <summary>Encode a key which is composed of five elements</summary>
		public Slice Encode<T1, T2, T3, T4, T5>(T1 item1, T2 item2, T3 item3, T4 item4, T5 item5)
		{
			var writer = this.Subspace.GetWriter();
			this.Encoder.EncodeKey(ref writer, item1, item2, item3, item4, item5);
			return writer.ToSlice();
		}

		/// <summary>Encode a batch of keys, each one composed of five values extracted from each elements</summary>
		public Slice[] EncodeMany<TItem, T1, T2, T3, T4, T5>(IEnumerable<TItem> items, Func<TItem, T1> selector1, Func<TItem, T2> selector2, Func<TItem, T3> selector3, Func<TItem, T4> selector4, Func<TItem, T5> selector5)
		{
			return Batched<TItem, IDynamicKeyEncoder>.Convert(
				this.Subspace.GetWriter(),
				items,
				(ref SliceWriter writer, TItem item, IDynamicKeyEncoder encoder) => encoder.EncodeKey<T1, T2, T3, T4, T5>(ref writer, selector1(item), selector2(item), selector3(item), selector4(item), selector5(item)),
				this.Encoder
			);
		}

		/// <summary>Encode a key which is composed of six elements</summary>
		public Slice Encode<T1, T2, T3, T4, T5, T6>(T1 item1, T2 item2, T3 item3, T4 item4, T5 item5, T6 item6)
		{
			var writer = this.Subspace.GetWriter();
			this.Encoder.EncodeKey(ref writer, item1, item2, item3, item4, item5, item6);
			return writer.ToSlice();
		}

		/// <summary>Encode a batch of keys, each one composed of six values extracted from each elements</summary>
		public Slice[] EncodeMany<TItem, T1, T2, T3, T4, T5, T6>(IEnumerable<TItem> items, Func<TItem, T1> selector1, Func<TItem, T2> selector2, Func<TItem, T3> selector3, Func<TItem, T4> selector4, Func<TItem, T5> selector5, Func<TItem, T6> selector6)
		{
			return Batched<TItem, IDynamicKeyEncoder>.Convert(
				this.Subspace.GetWriter(),
				items,
				(ref SliceWriter writer, TItem item, IDynamicKeyEncoder encoder) => encoder.EncodeKey<T1, T2, T3, T4, T5, T6>(ref writer, selector1(item), selector2(item), selector3(item), selector4(item), selector5(item), selector6(item)),
				this.Encoder
			);
		}

		/// <summary>Encode a key which is composed of seven elements</summary>
		public Slice Encode<T1, T2, T3, T4, T5, T6, T7>(T1 item1, T2 item2, T3 item3, T4 item4, T5 item5, T6 item6, T7 item7)
		{
			var writer = this.Subspace.GetWriter();
			this.Encoder.EncodeKey(ref writer, item1, item2, item3, item4, item5, item6, item7);
			return writer.ToSlice();
		}

		/// <summary>Encode a batch of keys, each one composed of seven values extracted from each elements</summary>
		public Slice[] EncodeMany<TItem, T1, T2, T3, T4, T5, T6, T7>(IEnumerable<TItem> items, Func<TItem, T1> selector1, Func<TItem, T2> selector2, Func<TItem, T3> selector3, Func<TItem, T4> selector4, Func<TItem, T5> selector5, Func<TItem, T6> selector6, Func<TItem, T7> selector7)
		{
			return Batched<TItem, IDynamicKeyEncoder>.Convert(
				this.Subspace.GetWriter(),
				items,
				(ref SliceWriter writer, TItem item, IDynamicKeyEncoder encoder) => encoder.EncodeKey<T1, T2, T3, T4, T5, T6, T7>(ref writer, selector1(item), selector2(item), selector3(item), selector4(item), selector5(item), selector6(item), selector7(item)),
				this.Encoder
			);
		}

		/// <summary>Encode a key which is composed of eight elements</summary>
		public Slice Encode<T1, T2, T3, T4, T5, T6, T7, T8>(T1 item1, T2 item2, T3 item3, T4 item4, T5 item5, T6 item6, T7 item7, T8 item8)
		{
			var writer = this.Subspace.GetWriter();
			this.Encoder.EncodeKey(ref writer, item1, item2, item3, item4, item5, item6, item7, item8);
			return writer.ToSlice();
		}

		/// <summary>Encode a batch of keys, each one composed of eight values extracted from each elements</summary>
		public Slice[] EncodeMany<TItem, T1, T2, T3, T4, T5, T6, T7, T8>(IEnumerable<TItem> items, Func<TItem, T1> selector1, Func<TItem, T2> selector2, Func<TItem, T3> selector3, Func<TItem, T4> selector4, Func<TItem, T5> selector5, Func<TItem, T6> selector6, Func<TItem, T7> selector7, Func<TItem, T8> selector8)
		{
			return Batched<TItem, IDynamicKeyEncoder>.Convert(
				this.Subspace.GetWriter(),
				items,
				(ref SliceWriter writer, TItem item, IDynamicKeyEncoder encoder) => encoder.EncodeKey<T1, T2, T3, T4, T5, T6, T7, T8>(ref writer, selector1(item), selector2(item), selector3(item), selector4(item), selector5(item), selector6(item), selector7(item), selector8(item)),
				this.Encoder
			);
		}

		/// <summary>Unpack a key of this subspace, back into a tuple</summary>
		/// <param name="packed">Key that was produced by a previous call to <see cref="Pack(Doxense.Collections.Tuples.ITuple)"/></param>
		/// <returns>Original tuple</returns>
		public ITuple Unpack(Slice packed)
		{
			return this.Encoder.UnpackKey(this.Subspace.ExtractKey(packed));
		}

		private static T[] BatchDecode<T>(IEnumerable<Slice> packed, IKeySubspace subspace, IDynamicKeyEncoder encoder, Func<Slice, IDynamicKeyEncoder, T> decode)
		{
			if (packed is ICollection<Slice> coll)
			{
				var res = new T[coll.Count];
				int p = 0;
				foreach (var data in packed)
				{
					res[p++] = decode(subspace.ExtractKey(data), encoder);
				}
				Contract.Assert(p == res.Length);
				return res;
			}
			else
			{
				var res = new List<T>();
				foreach (var data in packed)
				{
					res.Add(decode(subspace.ExtractKey(data), encoder));
				}
				return res.ToArray();
			}
		}

		/// <summary>Unpack a batch of keys of this subspace, back into an array of tuples</summary>
		/// <param name="packed">Sequence of keys that were produced by a previous call to <see cref="Pack(Doxense.Collections.Tuples.ITuple)"/> or <see cref="PackMany(IEnumerable{Doxense.Collections.Tuples.ITuple})"/></param>
		/// <returns>Array containing the original tuples</returns>
		public ITuple[] UnpackMany(IEnumerable<Slice> packed)
		{
			return BatchDecode(packed, this.Subspace, this.Encoder, (data, encoder) => encoder.UnpackKey(data));
		}

		/// <summary>Decode a key of this subspace, composed of a single element</summary>
		public T1 Decode<T1>(Slice packed)
		{
			return this.Encoder.DecodeKey<T1>(this.Subspace.ExtractKey(packed));
		}

		/// <summary>Decode a batch of keys of this subspace, each one composed of a single element</summary>
		public IEnumerable<T1> DecodeMany<T1>(IEnumerable<Slice> packed)
		{
			return BatchDecode(packed, this.Subspace, this.Encoder, (data, encoder) => encoder.DecodeKey<T1>(data));
		}

		/// <summary>Decode a key of this subspace, composed of exactly two elements</summary>
		public STuple<T1, T2> Decode<T1, T2>(Slice packed)
		{
			return this.Encoder.DecodeKey<T1, T2>(this.Subspace.ExtractKey(packed));
		}

		/// <summary>Decode a batch of keys of this subspace, each one composed of exactly two elements</summary>
		public IEnumerable<STuple<T1, T2>> DecodeMany<T1, T2>(IEnumerable<Slice> packed)
		{
			return BatchDecode(packed, this.Subspace, this.Encoder, (data, encoder) => encoder.DecodeKey<T1, T2>(data));
		}

		/// <summary>Decode a key of this subspace, composed of exactly three elements</summary>
		public STuple<T1, T2, T3> Decode<T1, T2, T3>(Slice packed)
		{
			return this.Encoder.DecodeKey<T1, T2, T3>(this.Subspace.ExtractKey(packed));
		}

		/// <summary>Decode a batch of keys of this subspace, each one composed of exactly three elements</summary>
		public IEnumerable<STuple<T1, T2, T3>> DecodeMany<T1, T2, T3>(IEnumerable<Slice> packed)
		{
			return BatchDecode(packed, this.Subspace, this.Encoder, (data, encoder) => encoder.DecodeKey<T1, T2, T3>(data));
		}

		/// <summary>Decode a key of this subspace, composed of exactly four elements</summary>
		public STuple<T1, T2, T3, T4> Decode<T1, T2, T3, T4>(Slice packed)
		{
			return this.Encoder.DecodeKey<T1, T2, T3, T4>(this.Subspace.ExtractKey(packed));
		}

		/// <summary>Decode a batch of keys of this subspace, each one composed of exactly four elements</summary>
		public IEnumerable<STuple<T1, T2, T3, T4>> DecodeMany<T1, T2, T3, T4>(IEnumerable<Slice> packed)
		{
			return BatchDecode(packed, this.Subspace, this.Encoder, (data, encoder) => encoder.DecodeKey<T1, T2, T3, T4>(data));
		}

		/// <summary>Decode a key of this subspace, composed of exactly five elements</summary>
		public STuple<T1, T2, T3, T4, T5> Decode<T1, T2, T3, T4, T5>(Slice packed)
		{
			return this.Encoder.DecodeKey<T1, T2, T3, T4, T5>(this.Subspace.ExtractKey(packed));
		}

		/// <summary>Decode a batch of keys of this subspace, each one composed of exactly five elements</summary>
		public IEnumerable<STuple<T1, T2, T3, T4, T5>> DecodeMany<T1, T2, T3, T4, T5>(IEnumerable<Slice> packed)
		{
			return BatchDecode(packed, this.Subspace, this.Encoder, (data, encoder) => encoder.DecodeKey<T1, T2, T3, T4, T5>(data));
		}

		/// <summary>Decode a key of this subspace, and return only the first element without decoding the rest the key.</summary>
		/// <remarks>This method is faster than unpacking the complete key and reading only the first element.</remarks>
		public T DecodeFirst<T>(Slice packed)
		{
			return this.Encoder.DecodeKeyFirst<T>(this.Subspace.ExtractKey(packed));
		}

		/// <summary>Decode a batch of keys of this subspace, and for each one, return only the first element without decoding the rest of the key.</summary>
		/// <remarks>This method is faster than unpacking the complete key and reading only the first element.</remarks>
		public IEnumerable<T> DecodeFirstMany<T>(IEnumerable<Slice> packed)
		{
			return BatchDecode(packed, this.Subspace, this.Encoder, (data, encoder) => encoder.DecodeKeyFirst<T>(data));
		}

		/// <summary>Decode a key of this subspace, and return only the last element without decoding the rest.</summary>
		/// <remarks>This method is faster than unpacking the complete key and reading only the last element.</remarks>
		public T DecodeLast<T>(Slice packed)
		{
			return this.Encoder.DecodeKeyLast<T>(this.Subspace.ExtractKey(packed));
		}

		/// <summary>Decode a batch of keys of this subspace, and for each one, return only the last element without decoding the rest of the key.</summary>
		/// <remarks>This method is faster than unpacking the complete key and reading only the last element.</remarks>
		public IEnumerable<T> DecodeLastMany<T>(Slice[] packed)
		{
			return BatchDecode(packed, this.Subspace, this.Encoder, (data, encoder) => encoder.DecodeKeyLast<T>(data));
		}

		#region Append: Subspace => Tuple

		/// <summary>Return an empty tuple that is attached to this subspace</summary>
		/// <returns>Empty tuple that can be extended, and whose packed representation will always be prefixed by the subspace key</returns>
		[NotNull]
		public ITuple ToTuple()
		{
			return new PrefixedTuple(this.Subspace.GetPrefix(), STuple.Empty);
		}

		/// <summary>Attach a tuple to an existing subspace.</summary>
		/// <param name="tuple">Tuple whose items will be appended at the end of the current subspace</param>
		/// <returns>Tuple that wraps the items of <paramref name="tuple"/> and whose packed representation will always be prefixed by the subspace key.</returns>
		[NotNull]
		public ITuple Concat([NotNull] ITuple tuple)
		{
			return new PrefixedTuple(this.Subspace.GetPrefix(), tuple);
		}

		/// <summary>Convert a formattable item into a tuple that is attached to this subspace.</summary>
		/// <param name="formattable">Item that can be converted into a tuple</param>
		/// <returns>Tuple that is the logical representation of the item, and whose packed representation will always be prefixed by the subspace key.</returns>
		/// <remarks>This is the equivalent of calling 'subspace.Create(formattable.ToTuple())'</remarks>
		[NotNull]
		public ITuple Concat([NotNull] ITupleFormattable formattable)
		{
			if (formattable == null) throw new ArgumentNullException(nameof(formattable));
			var tuple = formattable.ToTuple();
			if (tuple == null) throw new InvalidOperationException("Formattable item cannot return an empty tuple");
			return new PrefixedTuple(this.Subspace.GetPrefix(), tuple);
		}

		/// <summary>Create a new 1-tuple that is attached to this subspace</summary>
		/// <typeparam name="T">Type of the value to append</typeparam>
		/// <param name="value">Value that will be appended</param>
		/// <returns>Tuple of size 1 that contains <paramref name="value"/>, and whose packed representation will always be prefixed by the subspace key.</returns>
		/// <remarks>This is the equivalent of calling 'subspace.Create(STuple.Create&lt;T&gt;(value))'</remarks>
		[NotNull]
		public ITuple Append<T>(T value)
		{
			return new PrefixedTuple(this.Subspace.GetPrefix(), STuple.Create<T>(value));
		}

		/// <summary>Create a new 2-tuple that is attached to this subspace</summary>
		/// <typeparam name="T1">Type of the first value to append</typeparam>
		/// <typeparam name="T2">Type of the second value to append</typeparam>
		/// <param name="item1">First value that will be appended</param>
		/// <param name="item2">Second value that will be appended</param>
		/// <returns>Tuple of size 2 that contains <paramref name="item1"/> and <paramref name="item2"/>, and whose packed representation will always be prefixed by the subspace key.</returns>
		/// <remarks>This is the equivalent of calling 'subspace.Create(STuple.Create&lt;T1, T2&gt;(item1, item2))'</remarks>
		[NotNull]
		public ITuple Append<T1, T2>(T1 item1, T2 item2)
		{
			return new PrefixedTuple(this.Subspace.GetPrefix(), STuple.Create<T1, T2>(item1, item2));
		}

		/// <summary>Create a new 3-tuple that is attached to this subspace</summary>
		/// <typeparam name="T1">Type of the first value to append</typeparam>
		/// <typeparam name="T2">Type of the second value to append</typeparam>
		/// <typeparam name="T3">Type of the third value to append</typeparam>
		/// <param name="item1">First value that will be appended</param>
		/// <param name="item2">Second value that will be appended</param>
		/// <param name="item3">Third value that will be appended</param>
		/// <returns>Tuple of size 3 that contains <paramref name="item1"/>, <paramref name="item2"/> and <paramref name="item3"/>, and whose packed representation will always be prefixed by the subspace key.</returns>
		/// <remarks>This is the equivalent of calling 'subspace.Create(STuple.Create&lt;T1, T2, T3&gt;(item1, item2, item3))'</remarks>
		[NotNull]
		public ITuple Append<T1, T2, T3>(T1 item1, T2 item2, T3 item3)
		{
			return new PrefixedTuple(this.Subspace.GetPrefix(), STuple.Create<T1, T2, T3>(item1, item2, item3));
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
		/// <remarks>This is the equivalent of calling 'subspace.Create(STuple.Create&lt;T1, T2, T3, T4&gt;(item1, item2, item3, item4))'</remarks>
		[NotNull]
		public ITuple Append<T1, T2, T3, T4>(T1 item1, T2 item2, T3 item3, T4 item4)
		{
			return new PrefixedTuple(this.Subspace.GetPrefix(), STuple.Create<T1, T2, T3, T4>(item1, item2, item3, item4));
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
		/// <remarks>This is the equivalent of calling 'subspace.Create(STuple.Create&lt;T1, T2, T3, T4, T5&gt;(item1, item2, item3, item4, item5))'</remarks>
		[NotNull]
		public ITuple Append<T1, T2, T3, T4, T5>(T1 item1, T2 item2, T3 item3, T4 item4, T5 item5)
		{
			return new PrefixedTuple(this.Subspace.GetPrefix(), STuple.Create<T1, T2, T3, T4, T5>(item1, item2, item3, item4, item5));
		}

		#endregion

	}

	public /*readonly*/ struct DynamicPartition
	{

		[NotNull]
		public readonly IDynamicKeySubspace Subspace;

		[NotNull]
		public readonly IDynamicKeyEncoder Encoder;

		public DynamicPartition([NotNull] IDynamicKeySubspace subspace, [NotNull] IDynamicKeyEncoder encoder)
		{
			Contract.Requires(subspace != null && encoder != null);
			this.Subspace = subspace;
			this.Encoder = encoder;
		}

		/// <summary>Returns the same view but using a different Type System</summary>
		/// <param name="encoding">Type System that will code keys in this new view</param>
		/// <returns>Review that will partition this subspace using a different Type System</returns>
		/// <remarks>
		/// This should only be used for one-off usages where creating a new subspace just to encode one key would be overkill.
		/// If you are calling this in a loop, consider creating a new subspace using that encoding.
		/// </remarks>
		[Pure]
		public DynamicPartition Using([NotNull] IKeyEncoding encoding)
		{
			Contract.NotNull(encoding, nameof(encoding));
			var encoder = encoding.GetDynamicEncoder();
			return UsingEncoder(encoder);
		}

		/// <summary>Returns the same view but using a different Type System</summary>
		/// <param name="encoder">Type System that will code keys in this new view</param>
		/// <returns>Review that will partition this subspace using a different Type System</returns>
		/// <remarks>
		/// This should only be used for one-off usages where creating a new subspace just to encode one key would be overkill.
		/// If you are calling this in a loop, consider creating a new subspace using that encoder.
		/// </remarks>
		[Pure]
		public DynamicPartition UsingEncoder([NotNull] IDynamicKeyEncoder encoder)
		{
			return new DynamicPartition(this.Subspace, encoder);
		}

		/// <summary>Create a new subspace by appdending a suffix to the current subspace</summary>
		/// <param name="suffix">Suffix of the new subspace</param>
		/// <returns>New subspace with prefix equal to the current subspace's prefix, followed by <paramref name="suffix"/></returns>
		public IDynamicKeySubspace this[Slice suffix]
		{
			[Pure, NotNull]
			get
			{
				if (suffix.IsNull) throw new ArgumentException("Partition suffix cannot be null", nameof(suffix));
				//TODO: find a way to limit the number of copies of the key?
				return new DynamicKeySubspace(this.Subspace.ConcatKey(suffix), false, this.Encoder);
			}
		}

		public IDynamicKeySubspace this[ITuple tuple]
		{
			[Pure, ContractAnnotation("null => halt; notnull => notnull")]
			get
			{
				if (tuple == null) throw new ArgumentNullException(nameof(tuple));
				//TODO: find a way to limit the number of copies of the packed tuple?
				return new DynamicKeySubspace(this.Subspace.Keys.Pack(tuple), false, this.Encoder);
			}
		}

		public IDynamicKeySubspace this[ITupleFormattable item]
		{
			[Pure, ContractAnnotation("null => halt; notnull => notnull")]
			get
			{
				Contract.NotNull(item, nameof(item));
				var tuple = item.ToTuple();
				if (tuple == null) throw new InvalidOperationException("Formattable item returned an empty tuple");
				return this[tuple];
			}
		}

		/// <summary>Partition this subspace into a child subspace</summary>
		/// <typeparam name="T">Type of the child subspace key</typeparam>
		/// <param name="value">Value of the child subspace</param>
		/// <returns>New subspace that is logically contained by the current subspace</returns>
		/// <remarks>Subspace([Foo, ]).Partition(Bar) is equivalent to Subspace([Foo, Bar, ])</remarks>
		/// <example>
		/// new FdbSubspace(["Users", ]).Partition("Contacts") == new FdbSubspace(["Users", "Contacts", ])
		/// </example>
		[Pure, NotNull]
		public IDynamicKeySubspace ByKey<T>(T value)
		{
			return new DynamicKeySubspace(this.Subspace.Keys.Encode<T>(value), false, this.Encoder);
		}

		/// <summary>Partition this subspace into a child subspace</summary>
		/// <typeparam name="T1">Type of the first subspace key</typeparam>
		/// <typeparam name="T2">Type of the second subspace key</typeparam>
		/// <param name="value1">Value of the first subspace key</param>
		/// <param name="value2">Value of the second subspace key</param>
		/// <returns>New subspace that is logically contained by the current subspace</returns>
		/// <remarks>Subspace([Foo, ]).Partition(Bar, Baz) is equivalent to Subspace([Foo, Bar, Baz])</remarks>
		/// <example>
		/// new FdbSubspace(["Users", ]).Partition("Contacts", "Friends") == new FdbSubspace(["Users", "Contacts", "Friends", ])
		/// </example>
		[Pure, NotNull]
		public IDynamicKeySubspace ByKey<T1, T2>(T1 value1, T2 value2)
		{
			return new DynamicKeySubspace(this.Subspace.Keys.Encode<T1, T2>(value1, value2), false, this.Encoder);
		}

		/// <summary>Partition this subspace into a child subspace</summary>
		/// <typeparam name="T1">Type of the first subspace key</typeparam>
		/// <typeparam name="T2">Type of the second subspace key</typeparam>
		/// <typeparam name="T3">Type of the third subspace key</typeparam>
		/// <param name="value1">Value of the first subspace key</param>
		/// <param name="value2">Value of the second subspace key</param>
		/// <param name="value3">Value of the third subspace key</param>
		/// <returns>New subspace that is logically contained by the current subspace</returns>
		/// <example>
		/// new FdbSubspace(["Users", ]).Partition("John Smith", "Contacts", "Friends") == new FdbSubspace(["Users", "John Smith", "Contacts", "Friends", ])
		/// </example>
		[Pure, NotNull]
		public IDynamicKeySubspace ByKey<T1, T2, T3>(T1 value1, T2 value2, T3 value3)
		{
			return new DynamicKeySubspace(this.Subspace.Keys.Encode<T1, T2, T3>(value1, value2, value3), false, this.Encoder);
		}

		/// <summary>Partition this subspace into a child subspace</summary>
		/// <typeparam name="T1">Type of the first subspace key</typeparam>
		/// <typeparam name="T2">Type of the second subspace key</typeparam>
		/// <typeparam name="T3">Type of the third subspace key</typeparam>
		/// <typeparam name="T4">Type of the fourth subspace key</typeparam>
		/// <param name="value1">Value of the first subspace key</param>
		/// <param name="value2">Value of the second subspace key</param>
		/// <param name="value3">Value of the third subspace key</param>
		/// <param name="value4">Value of the fourth subspace key</param>
		/// <returns>New subspace that is logically contained by the current subspace</returns>
		/// <example>
		/// new FdbSubspace(["Users", ]).Partition("John Smith", "Contacts", "Friends", "Messages") == new FdbSubspace(["Users", "John Smith", "Contacts", "Friends", "Messages", ])
		/// </example>
		[Pure, NotNull]
		public IDynamicKeySubspace ByKey<T1, T2, T3, T4>(T1 value1, T2 value2, T3 value3, T4 value4)
		{
			return new DynamicKeySubspace(this.Subspace.Keys.Encode<T1, T2, T3, T4>(value1, value2, value3, value4), false, this.Encoder);
		}

	}

}
