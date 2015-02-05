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

namespace FoundationDB.Client
{
	using FoundationDB.Layers.Tuples;
	using JetBrains.Annotations;
	using System;
	using System.Collections.Generic;
	using FoundationDB.Client.Utils;
	using System.Diagnostics;


	/// <summary>Provides of methods to encode and decodes keys using the Tuple Encoding format</summary>
	[Obsolete("REMOVE ME!")]
	public struct FdbSubspaceTuples_OLD
	{

		/// <summary>Ref to the parent subspace</summary>
		private readonly IFdbSubspace m_subspace;

		/// <summary>Wraps an existing subspace</summary>
		/// <param name="subspace"></param>
		public FdbSubspaceTuples_OLD(IFdbSubspace subspace)
		{
			Contract.Requires(subspace != null);
			m_subspace = subspace;
		}

		public IFdbSubspace Subspace
		{
			[DebuggerStepThrough]
			[NotNull] //note: except for corner cases like default(FdbTupleSubspace) or unallocated value
			get { return m_subspace; }
		}

		/// <summary>Return a key that is composed of the subspace prefix, and the packed representation of a tuple.</summary>
		/// <param name="tuple">Tuple to pack (can be null or empty)</param>
		/// <returns>Key which starts with the subspace prefix, followed by the packed representation of <paramref name="tuple"/>. This key can be parsed back to an equivalent tuple by calling <see cref="Unpack(Slice)"/>.</returns>
		/// <remarks>If <paramref name="tuple"/> is null or empty, then the prefix of the subspace is returned.</remarks>
		public Slice this[[NotNull] IFdbTuple tuple]
		{
			[DebuggerStepThrough]
			get { return Pack(tuple); }
		}

		public Slice this[[NotNull] ITupleFormattable item]
		{
			[DebuggerStepThrough]
			get { return Pack(item); }
		}

		#region Pack: Tuple => Slice

		/// <summary>Return a key that is composed of the subspace prefix, and the packed representation of a tuple.</summary>
		/// <param name="tuple">Tuple to pack (can be null or empty)</param>
		/// <returns>Key which starts with the subspace prefix, followed by the packed representation of <paramref name="tuple"/>. This key can be parsed back to an equivalent tuple by calling <see cref="Unpack(Slice)"/>.</returns>
		/// <remarks>If <paramref name="tuple"/> is null or empty, then the prefix of the subspace is returned.</remarks>
		[DebuggerStepThrough]
		public Slice Pack([NotNull] IFdbTuple tuple)
		{
			if (tuple == null) throw new ArgumentNullException("tuple");
			return FdbTuple.Pack(m_subspace.Key, tuple);
		}

		/// <summary>Pack a sequence of tuples, all sharing the same buffer</summary>
		/// <param name="tuples">Sequence of N-tuples to pack</param>
		/// <returns>Array containing the buffer segment of each packed tuple</returns>
		/// <example>tuple.Pack(new [] { "abc", [ ("Foo", 1), ("Foo", 2) ] }) => [ "abc\x02Foo\x00\x15\x01", "abc\x02Foo\x00\x15\x02" ] </example>
		[DebuggerStepThrough]
		[NotNull]
		public Slice[] Pack([NotNull] IEnumerable<IFdbTuple> tuples)
		{
			if (tuples == null) throw new ArgumentNullException("tuples");

			return FdbTuple.Pack(m_subspace.Key, tuples);
		}

		/// <summary>Pack a sequence of tuples, all sharing the same buffer</summary>
		/// <param name="tuples">Sequence of N-tuples to pack</param>
		/// <returns>Array containing the buffer segment of each packed tuple</returns>
		/// <example>BatchPack("abc", [ ("Foo", 1), ("Foo", 2) ]) => [ "abc\x02Foo\x00\x15\x01", "abc\x02Foo\x00\x15\x02" ] </example>
		[DebuggerStepThrough]
		[NotNull]
		public Slice[] Pack([NotNull] params IFdbTuple[] tuples)
		{
			return Pack((IEnumerable<IFdbTuple>)tuples);
		}

		/// <summary>Return a key that is composed of the subspace prefix, and the packed representation of a tuple.</summary>
		/// <param name="item">Tuple to pack (can be null or empty)</param>
		/// <returns>Key which starts with the subspace prefix, followed by the packed representation of <paramref name="item"/>. This key can be parsed back to an equivalent tuple by calling <see cref="Unpack(Slice)"/>.</returns>
		/// <remarks>If <paramref name="item"/> is null or empty, then the prefix of the subspace is returned.</remarks>
		[DebuggerStepThrough]
		public Slice Pack([NotNull] ITupleFormattable item)
		{
			if (item == null) throw new ArgumentNullException("item");
			return Pack(item.ToTuple());
		}

		/// <summary>Pack a sequence of keys, all sharing the same buffer</summary>
		/// <param name="items">Sequence of N-tuples to pack</param>
		/// <returns>Array containing the buffer segment of each packed tuple</returns>
		/// <example>Pack("abc", [ ("Foo", 1), ("Foo", 2) ]) => [ "abc\x02Foo\x00\x15\x01", "abc\x02Foo\x00\x15\x02" ] </example>
		[DebuggerStepThrough]
		[NotNull]
		public Slice[] Pack([NotNull] IEnumerable<ITupleFormattable> items)
		{
			if (items == null) throw new ArgumentNullException("items");

			return FdbTuple.Pack(m_subspace.Key, items, (item) => item.ToTuple());
		}

		/// <summary>Pack a sequence of keys, all sharing the same buffer</summary>
		/// <param name="items">Sequence of N-tuples to pack</param>
		/// <returns>Array containing the buffer segment of each packed tuple</returns>
		/// <example>Pack("abc", [ ("Foo", 1), ("Foo", 2) ]) => [ "abc\x02Foo\x00\x15\x01", "abc\x02Foo\x00\x15\x02" ] </example>
		[DebuggerStepThrough]
		[NotNull]
		public Slice[] Pack([NotNull] params ITupleFormattable[] items)
		{
			return Pack((IEnumerable<ITupleFormattable>)items);
		}

		#endregion

		#region Unpack: Slice => Tuple

		/// <summary>Unpack a key into a tuple, with the subspace prefix removed</summary>
		/// <param name="key">Packed version of a key that should fit inside this subspace.</param>
		/// <returns>Unpacked tuple that is relative to the current subspace, or the empty tuple if <paramref name="key"/> is equal to the prefix of this tuple.</returns>
		/// <example>new Subspace([FE]).Unpack([FE 02 'H' 'e' 'l' 'l' 'o' 00 15 1]) => ("hello", 1,)</example>
		/// <remarks>If <paramref name="key"/> is equal to the subspace prefix, then an empty tuple is returned.</remarks>
		/// <exception cref="System.ArgumentNullException">If <paramref name="key"/> is <see cref="Slice.Nil"/></exception>
		/// <exception cref="System.ArgumentOutOfRangeException">If the unpacked tuple is not contained in this subspace</exception>
		[NotNull]
		public IFdbTuple Unpack(Slice key)
		{
			if (key.IsNull) throw new ArgumentNullException("key");
			return FdbTuple.Unpack(m_subspace.ExtractKey(key, boundCheck: true));
		}

		/// <summary>Unpack a key into a tuple, with the subspace prefix removed</summary>
		/// <param name="key">Packed version of a key that should fit inside this subspace.</param>
		/// <returns>Unpacked tuple that is relative to the current subspace, the empty tuple if <paramref name="key"/> is equal to <see cref="Slice.Empty"/>, or null if <paramref name="key"/> is equal to <see cref="Slice.Nil"/></returns>
		/// <example>new Subspace([FE]).UnpackOrDefault([FE 02 'H' 'e' 'l' 'l' 'o' 00 15 1]) => ("hello", 1,)</example>
		/// <remarks>If <paramref name="key"/> is equal to the subspace prefix, then an empty tuple is returned.</remarks>
		/// <exception cref="System.ArgumentOutOfRangeException">If the unpacked tuple is not contained in this subspace</exception>
		[CanBeNull]
		public IFdbTuple UnpackOrDefault(Slice key)
		{
			// We special case 'Slice.Nil' because it is returned by GetAsync(..) when the key does not exist.
			// This simplifies the decoding logic where the caller could do "var foo = FdbTuple.UnpackOrDefault(await tr.GetAsync(...))" and then only have to test "if (foo != null)"
			if (key.IsNull) return null;

			return FdbTuple.UnpackOrDefault(m_subspace.ExtractKey(key, boundCheck: true));
		}

		/// <summary>Unpack an sequence of keys into tuples, with the subspace prefix removed</summary>
		/// <param name="keys">Packed version of keys inside this subspace</param>
		/// <returns>Unpacked tuples that are relative to the current subspace</returns>
		[NotNull, ItemNotNull]
		public IFdbTuple[] Unpack([NotNull] IEnumerable<Slice> keys)
		{
			// return an array with the keys minus the subspace's prefix
			var extracted = m_subspace.ExtractKeys(keys, boundCheck: true);

			// unpack everything
			var prefix = m_subspace.Key;
			var tuples = new IFdbTuple[extracted.Length];
			for(int i = 0; i < extracted.Length; i++)
			{
				if (extracted[i].IsNull) throw new InvalidOperationException("The list of keys contains at least one element which is null.");
				tuples[i] = new FdbPrefixedTuple(prefix, FdbTuple.Unpack(extracted[i]));
			}
			return tuples;
		}

		/// <summary>Unpack an sequence of keys into tuples, with the subspace prefix removed</summary>
		/// <param name="keys">Packed version of keys inside this subspace</param>
		/// <returns>Unpacked tuples that are relative to the current subspace</returns>
		[NotNull, ItemCanBeNull]
		public IFdbTuple[] UnpackOrDefault([NotNull] IEnumerable<Slice> keys)
		{
			// return an array with the keys minus the subspace's prefix
			var extracted = m_subspace.ExtractKeys(keys, boundCheck: true);

			// unpack everything
			var prefix = m_subspace.Key;
			var tuples = new IFdbTuple[extracted.Length];
			for (int i = 0; i < extracted.Length; i++)
			{
				if (extracted[i].HasValue) tuples[i] = new FdbPrefixedTuple(prefix, FdbTuple.UnpackOrDefault(extracted[i]));
			}
			return tuples;
		}

		/// <summary>Unpack an array of keys into tuples, with the subspace prefix removed</summary>
		/// <param name="keys">Packed version of keys inside this subspace</param>
		/// <returns>Unpacked tuples that are relative to the current subspace.</returns>
		[NotNull, ItemNotNull]
		public IFdbTuple[] Unpack([NotNull] params Slice[] keys)
		{
			//note: this overload allows writing ".Unpack(foo, bar, baz)" instead of ".Unpack(new [] { foo, bar, baz })"
			return Unpack((IEnumerable<Slice>)keys);
		}

		/// <summary>Unpack an array of keys into tuples, with the subspace prefix removed</summary>
		/// <param name="keys">Packed version of keys inside this subspace</param>
		/// <returns>Unpacked tuples that are relative to the current subspace. If a key is equal to <see cref="Slice.Nil"/> then the corresponding tuple will be null</returns>
		[NotNull, ItemCanBeNull]
		public IFdbTuple[] UnpackOrDefault([NotNull] params Slice[] keys)
		{
			//note: this overload allows writing ".UnpackOrDefault(foo, bar, baz)" instead of ".UnpackOrDefault(new [] { foo, bar, baz })"
			return UnpackOrDefault((IEnumerable<Slice>)keys);
		}

		#endregion

		#region ToRange: Tuple => Range

		public FdbKeyRange ToRange()
		{
			return FdbTuple.ToRange(m_subspace.Key);
		}

		/// <summary>Gets a key range respresenting all keys strictly within a sub-section of this Subspace.</summary>
		/// <param name="suffix">Suffix added to the subspace prefix</param>
		/// <rereturns>Key range that, when passed to ClearRange() or GetRange(), would clear or return all the keys contained by this subspace, excluding the subspace prefix itself.</rereturns>
		public FdbKeyRange ToRange(Slice suffix)
		{
			return FdbTuple.ToRange(m_subspace.Key.Concat(suffix));
		}

		public FdbKeyRange ToRange([NotNull] IFdbTuple tuple)
		{
			if (tuple == null) throw new ArgumentNullException("tuple");
			return FdbTuple.ToRange(FdbTuple.Pack(m_subspace.Key, tuple));
		}

		public FdbKeyRange ToRange([NotNull] ITupleFormattable item)
		{
			if (item == null) throw new ArgumentNullException("item");
			return ToRange(item.ToTuple());
		}

		#endregion

		#region EncodeKey: (T1, T2, ...) => Slice

		/// <summary>Create a new key by adding a single item to the current subspace</summary>
		/// <typeparam name="T">Type of the item</typeparam>
		/// <param name="item">Item that will be appended at the end of the key</param>
		/// <returns>Key that is equivalent to adding the packed singleton <paramref name="item"/> to the subspace's prefix</returns>
		/// <example>tuple.Pack(x) is equivalent to tuple.Append(x).ToSlice()</example>
		/// <remarks>The key produced can be decoded back into the original value by calling <see cref="DecodeKey{T}"/>, or a tuple by calling <see cref="Unpack(Slice)"/></remarks>
		public Slice EncodeKey<T>(T item)
		{
			return FdbTuple.EncodePrefixedKey<T>(m_subspace.Key, item);
		}

		/// <summary>Create a new key by adding two items to the current subspace</summary>
		/// <typeparam name="T1">Type of the first item</typeparam>
		/// <typeparam name="T2">Type of the second item</typeparam>
		/// <param name="item1">Item in the first position</param>
		/// <param name="item2">Item in the second position</param>
		/// <returns>Key that is equivalent to adding the packed pair (<paramref name="item1"/>, <paramref name="item2"/>) to the subspace's prefix</returns>
		/// <example>{subspace}.EncodeKey(x, y) is much faster way to do {subspace}.Key + FdbTuple.Create(x, y).ToSlice()</example>
		/// <remarks>The key produced can be decoded back into a pair by calling either <see cref="DecodeKey{T1, T2}"/> or <see cref="Unpack(Slice)"/></remarks>
		public Slice EncodeKey<T1, T2>(T1 item1, T2 item2)
		{
			return FdbTuple.EncodePrefixedKey<T1, T2>(m_subspace.Key, item1, item2);
		}

		/// <summary>Create a new key by adding three items to the current subspace</summary>
		/// <typeparam name="T1">Type of the first item</typeparam>
		/// <typeparam name="T2">Type of the second item</typeparam>
		/// <typeparam name="T3">Type of the third item</typeparam>
		/// <param name="item1">Item in the first position</param>
		/// <param name="item2">Item in the second position</param>
		/// <param name="item3">Item in the third position</param>
		/// <returns>Key that is equivalent to adding the packed triplet (<paramref name="item1"/>, <paramref name="item2"/>, <paramref name="item3"/>) to the subspace's prefix</returns>
		/// <example>{subspace}.EncodeKey(x, y, z) is much faster way to do {subspace}.Key + FdbTuple.Create(x, y, z).ToSlice()</example>
		/// <remarks>The key produced can be decoded back into a triplet by calling either <see cref="DecodeKey{T1, T2, T3}"/> or <see cref="Unpack(Slice)"/></remarks>
		public Slice EncodeKey<T1, T2, T3>(T1 item1, T2 item2, T3 item3)
		{
			return FdbTuple.EncodePrefixedKey<T1, T2, T3>(m_subspace.Key, item1, item2, item3);
		}

		/// <summary>Create a new key by adding four items to the current subspace</summary>
		/// <typeparam name="T1">Type of the first item</typeparam>
		/// <typeparam name="T2">Type of the second item</typeparam>
		/// <typeparam name="T3">Type of the third item</typeparam>
		/// <typeparam name="T4">Type of the fourth item</typeparam>
		/// <param name="item1">Item in the first position</param>
		/// <param name="item2">Item in the second position</param>
		/// <param name="item3">Item in the third position</param>
		/// <param name="item4">Item in the fourth position</param>
		/// <returns>Key that is equivalent to adding the packed tuple quad (<paramref name="item1"/>, <paramref name="item2"/>, <paramref name="item3"/>, <paramref name="item4"/>) to the subspace's prefix</returns>
		/// <example>{subspace}.EncodeKey(w, x, y, z) is much faster way to do {subspace}.Key + FdbTuple.Create(w, x, y, z).ToSlice()</example>
		/// <remarks>The key produced can be decoded back into a quad by calling either <see cref="DecodeKey{T1, T2, T3, T4}"/> or <see cref="Unpack(Slice)"/></remarks>
		public Slice EncodeKey<T1, T2, T3, T4>(T1 item1, T2 item2, T3 item3, T4 item4)
		{
			return FdbTuple.EncodePrefixedKey<T1, T2, T3, T4>(m_subspace.Key, item1, item2, item3, item4);
		}

		/// <summary>Create a new key by adding five items to the current subspace</summary>
		/// <typeparam name="T1">Type of the first item</typeparam>
		/// <typeparam name="T2">Type of the second item</typeparam>
		/// <typeparam name="T3">Type of the third item</typeparam>
		/// <typeparam name="T4">Type of the fourth item</typeparam>
		/// <typeparam name="T5">Type of the fifth item</typeparam>
		/// <param name="item1">Item in the first position</param>
		/// <param name="item2">Item in the second position</param>
		/// <param name="item3">Item in the third position</param>
		/// <param name="item4">Item in the fourth position</param>
		/// <param name="item5">Item in the fifth position</param>
		/// <returns>Key that is equivalent to adding the packed tuple (<paramref name="item1"/>, <paramref name="item2"/>, <paramref name="item3"/>, <paramref name="item4"/>, <paramref name="item5"/>) to the subspace's prefix</returns>
		/// <example>{subspace}.EncodeKey(w, x, y, z) is much faster way to do {subspace}.Key + FdbTuple.Create(w, x, y, z).ToSlice()</example>
		/// <remarks>The key produced can be decoded back into a tuple by calling either <see cref="DecodeKey{T1, T2, T3, T4, T5}"/> or <see cref="Unpack(Slice)"/></remarks>
		public Slice EncodeKey<T1, T2, T3, T4, T5>(T1 item1, T2 item2, T3 item3, T4 item4, T5 item5)
		{
			return FdbTuple.EncodePrefixedKey<T1, T2, T3, T4, T5>(m_subspace.Key, item1, item2, item3, item4, item5);
		}

		/// <summary>Create a new key by adding six items to the current subspace</summary>
		/// <typeparam name="T1">Type of the first item</typeparam>
		/// <typeparam name="T2">Type of the second item</typeparam>
		/// <typeparam name="T3">Type of the third item</typeparam>
		/// <typeparam name="T4">Type of the fourth item</typeparam>
		/// <typeparam name="T5">Type of the fifth item</typeparam>
		/// <typeparam name="T6">Type of the sixth item</typeparam>
		/// <param name="item1">Item in the first position</param>
		/// <param name="item2">Item in the second position</param>
		/// <param name="item3">Item in the third position</param>
		/// <param name="item4">Item in the fourth position</param>
		/// <param name="item5">Item in the fifth position</param>
		/// <param name="item6">Item in the sixth position</param>
		/// <returns>Key that is equivalent to adding the packed tuple (<paramref name="item1"/>, <paramref name="item2"/>, <paramref name="item3"/>, <paramref name="item4"/>, <paramref name="item5"/>, <paramref name="item6"/>) to the subspace's prefix</returns>
		/// <example>{subspace}.EncodeKey(w, x, y, z) is much faster way to do {subspace}.Key + FdbTuple.Create(w, x, y, z).ToSlice()</example>
		/// <remarks>The key produced can be decoded back into a tuple by calling <see cref="Unpack(Slice)"/></remarks>
		public Slice EncodeKey<T1, T2, T3, T4, T5, T6>(T1 item1, T2 item2, T3 item3, T4 item4, T5 item5, T6 item6)
		{
			return FdbTuple.EncodePrefixedKey<T1, T2, T3, T4, T5, T6>(m_subspace.Key, item1, item2, item3, item4, item5, item6);
		}

		/// <summary>Create a new key by adding seven items to the current subspace</summary>
		/// <typeparam name="T1">Type of the first item</typeparam>
		/// <typeparam name="T2">Type of the second item</typeparam>
		/// <typeparam name="T3">Type of the third item</typeparam>
		/// <typeparam name="T4">Type of the fourth item</typeparam>
		/// <typeparam name="T5">Type of the fifth item</typeparam>
		/// <typeparam name="T6">Type of the sixth item</typeparam>
		/// <typeparam name="T7">Type of the seventh item</typeparam>
		/// <param name="item1">Item in the first position</param>
		/// <param name="item2">Item in the second position</param>
		/// <param name="item3">Item in the third position</param>
		/// <param name="item4">Item in the fourth position</param>
		/// <param name="item5">Item in the fifth position</param>
		/// <param name="item6">Item in the sixth position</param>
		/// <param name="item7">Item in the seventh position</param>
		/// <returns>Key that is equivalent to adding the packed tuple (<paramref name="item1"/>, <paramref name="item2"/>, <paramref name="item3"/>, <paramref name="item4"/>, <paramref name="item5"/>, <paramref name="item6"/>, <paramref name="item7"/>) to the subspace's prefix</returns>
		/// <example>{subspace}.EncodeKey(w, x, y, z) is much faster way to do {subspace}.Key + FdbTuple.Create(w, x, y, z).ToSlice()</example>
		/// <remarks>The key produced can be decoded back into a tuple by calling <see cref="Unpack(Slice)"/></remarks>
		public Slice EncodeKey<T1, T2, T3, T4, T5, T6, T7>(T1 item1, T2 item2, T3 item3, T4 item4, T5 item5, T6 item6, T7 item7)
		{
			return FdbTuple.EncodePrefixedKey<T1, T2, T3, T4, T5, T6, T7>(m_subspace.Key, item1, item2, item3, item4, item5, item6, item7);
		}


		/// <summary>Create a new key by adding eight items to the current subspace</summary>
		/// <typeparam name="T1">Type of the first item</typeparam>
		/// <typeparam name="T2">Type of the second item</typeparam>
		/// <typeparam name="T3">Type of the third item</typeparam>
		/// <typeparam name="T4">Type of the fourth item</typeparam>
		/// <typeparam name="T5">Type of the fifth item</typeparam>
		/// <typeparam name="T6">Type of the sixth item</typeparam>
		/// <typeparam name="T7">Type of the seventh item</typeparam>
		/// <typeparam name="T8">Type of the eight item</typeparam>
		/// <param name="item1">Item in the first position</param>
		/// <param name="item2">Item in the second position</param>
		/// <param name="item3">Item in the third position</param>
		/// <param name="item4">Item in the fourth position</param>
		/// <param name="item5">Item in the fifth position</param>
		/// <param name="item6">Item in the sixth position</param>
		/// <param name="item7">Item in the seventh position</param>
		/// <param name="item8">Item in the eigth position</param>
		/// <returns>Key that is equivalent to adding the packed tuple (<paramref name="item1"/>, <paramref name="item2"/>, <paramref name="item3"/>, <paramref name="item4"/>, <paramref name="item5"/>, <paramref name="item6"/>, <paramref name="item7"/>, <paramref name="item8"/>) to the subspace's prefix</returns>
		/// <example>{subspace}.EncodeKey(w, x, y, z) is much faster way to do {subspace}.Key + FdbTuple.Create(w, x, y, z).ToSlice()</example>
		/// <remarks>The key produced can be decoded back into a tuple by calling <see cref="Unpack(Slice)"/></remarks>
		public Slice EncodeKey<T1, T2, T3, T4, T5, T6, T7, T8>(T1 item1, T2 item2, T3 item3, T4 item4, T5 item5, T6 item6, T7 item7, T8 item8)
		{
			return FdbTuple.EncodePrefixedKey<T1, T2, T3, T4, T5, T6, T7, T8>(m_subspace.Key, item1, item2, item3, item4, item5, item6, item7, item8);
		}

		/// <summary>Merge a sequence of keys with the subspace's prefix, all sharing the same buffer</summary>
		/// <typeparam name="T">Type of the keys</typeparam>
		/// <param name="keys">Sequence of keys to pack</param>
		/// <returns>Array of slices (for all keys) that share the same underlying buffer</returns>
		[NotNull]
		public Slice[] EncodeKeys<T>([NotNull] IEnumerable<T> keys)
		{
			return FdbTuple.EncodePrefixedKeys(m_subspace.Key, keys);
		}

		/// <summary>Merge a sequence of keys with the subspace's prefix, all sharing the same buffer</summary>
		/// <typeparam name="T">Type of the keys</typeparam>
		/// <param name="keys">Sequence of keys to pack</param>
		/// <returns>Array of slices (for all keys) that share the same underlying buffer</returns>
		[NotNull]
		public Slice[] EncodeKeys<T>([NotNull] T[] keys)
		{
			return FdbTuple.EncodePrefixedKeys<T>(m_subspace.Key, keys);
		}

		/// <summary>Merge a sequence of elements with the subspace's prefix, all sharing the same buffer</summary>
		/// <typeparam name="TElement">Type of the elements</typeparam>
		/// <typeparam name="TKey">Type of the keys extracted from the elements</typeparam>
		/// <param name="elements">Sequence of elements to pack</param>
		/// <param name="selector">Lambda that extract the key from each element</param>
		/// <returns>Array of slices (for all keys) that share the same underlying buffer</returns>
		[NotNull]
		public Slice[] EncodeKeys<TKey, TElement>([NotNull] TElement[] elements, [NotNull] Func<TElement, TKey> selector)
		{
			return FdbTuple.EncodePrefixedKeys<TKey, TElement>(m_subspace.Key, elements, selector);
		}

		#endregion

		#region DecodeKey: Slice => (T1, T2, ...)

		/// <summary>Unpack a key into a singleton tuple, and return the single element</summary>
		/// <typeparam name="T">Expected type of the only element</typeparam>
		/// <param name="key">Packed version of a key that should fit inside this subspace</param>
		/// <returns>Tuple of size 1, with the converted value of the only element in the tuple. Throws an exception if the tuple is empty or contains more than one element</returns>
		/// <example>new Subspace([FE]).UnpackSingle&lt;int&gt;([FE 02 'H' 'e' 'l' 'l' 'o' 00]) => (string) "Hello"</example>
		public T DecodeKey<T>(Slice key)
		{
			return FdbTuple.DecodeKey<T>(m_subspace.ExtractKey(key, boundCheck: true));
		}

		/// <summary>Unpack a key into a pair of elements</summary>
		/// <typeparam name="T1">Type of the first element</typeparam>
		/// <typeparam name="T2">Type of the second element</typeparam>
		/// <param name="key">Packed version of a key that should fit inside this subspace, and is composed of two elements.</param>
		/// <returns>Tuple of size 2, with the converted values of the tuple. Throws an exception if the tuple is empty or contains more than two elements</returns>
		public FdbTuple<T1, T2> DecodeKey<T1, T2>(Slice key)
		{
			if (key.IsNullOrEmpty) throw new FormatException("The specified key is empty");
			var tuple = Unpack(key);
			if (tuple.Count != 2) throw new FormatException("The specified key is not a tuple with two items");

			return FdbTuple.Create<T1, T2>(
				tuple.Get<T1>(0),
				tuple.Get<T2>(1)
			);
		}

		/// <summary>Unpack a key into a triplet of elements</summary>
		/// <typeparam name="T1">Type of the first element</typeparam>
		/// <typeparam name="T2">Type of the second element</typeparam>
		/// <typeparam name="T3">Type of the third element</typeparam>
		/// <param name="key">Packed version of a key that should fit inside this subspace, and is composed of three elements.</param>
		/// <returns>Tuple of size 3, with the converted values of the tuple. Throws an exception if the tuple is empty or contains more than three elements</returns>
		public FdbTuple<T1, T2, T3> DecodeKey<T1, T2, T3>(Slice key)
		{
			if (key.IsNullOrEmpty) throw new FormatException("The specified key is empty");
			var tuple = Unpack(key);
			if (tuple.Count != 3) throw new FormatException("The specified key is not a tuple with three items");

			return FdbTuple.Create<T1, T2, T3>(
				tuple.Get<T1>(0),
				tuple.Get<T2>(1),
				tuple.Get<T3>(2)
			);
		}

		/// <summary>Unpack a key into a quartet of elements</summary>
		/// <typeparam name="T1">Type of the first element</typeparam>
		/// <typeparam name="T2">Type of the second element</typeparam>
		/// <typeparam name="T3">Type of the third element</typeparam>
		/// <typeparam name="T4">Type of the fourth element</typeparam>
		/// <param name="key">Packed version of a key that should fit inside this subspace, and is composed of four elements.</param>
		/// <returns>Tuple of size 3, with the converted values of the tuple. Throws an exception if the tuple is empty or contains more than four elements</returns>
		public FdbTuple<T1, T2, T3, T4> DecodeKey<T1, T2, T3, T4>(Slice key)
		{
			if (key.IsNullOrEmpty) throw new FormatException("The specified key is empty");
			var tuple = Unpack(key);
			if (tuple.Count != 4) throw new FormatException("The specified key is not a tuple with four items");

			return FdbTuple.Create<T1, T2, T3, T4>(
				tuple.Get<T1>(0),
				tuple.Get<T2>(1),
				tuple.Get<T3>(2),
				tuple.Get<T4>(3)
			);
		}

		/// <summary>Unpack a key into a quintet of elements</summary>
		/// <typeparam name="T1">Type of the first element</typeparam>
		/// <typeparam name="T2">Type of the second element</typeparam>
		/// <typeparam name="T3">Type of the third element</typeparam>
		/// <typeparam name="T4">Type of the fourth element</typeparam>
		/// <typeparam name="T5">Type of the fifth element</typeparam>
		/// <param name="key">Packed version of a key that should fit inside this subspace, and is composed of five elements.</param>
		/// <returns>Tuple of size 3, with the converted values of the tuple. Throws an exception if the tuple is empty or contains more than five elements</returns>
		public FdbTuple<T1, T2, T3, T4, T5> DecodeKey<T1, T2, T3, T4, T5>(Slice key)
		{
			if (key.IsNullOrEmpty) throw new FormatException("The specified key is empty");
			var tuple = Unpack(key);
			if (tuple.Count != 5) throw new FormatException("The specified key is not a tuple with five items");

			return FdbTuple.Create<T1, T2, T3, T4, T5>(
				tuple.Get<T1>(0),
				tuple.Get<T2>(1),
				tuple.Get<T3>(2),
				tuple.Get<T4>(3),
				tuple.Get<T5>(4)
			);
		}

		//note: there is no DecodeKey(slice) => object[] because this would encourage the bad practive of dealing with tuples as object[] arrays !

		/// <summary>Unpack a key into a tuple, and return only the first element</summary>
		/// <typeparam name="T">Expected type of the last element</typeparam>
		/// <param name="key">Packed version of a key that should fit inside this subspace</param>
		/// <returns>Converted value of the last element of the tuple</returns>
		/// <example>new Subspace([FE]).UnpackLast&lt;int&gt;([FE 02 'H' 'e' 'l' 'l' 'o' 00 15 1]) => (string) "Hello"</example>
		public T DecodeFirst<T>(Slice key)
		{
			return FdbTuple.DecodeFirst<T>(m_subspace.ExtractKey(key, boundCheck: true));
		}

		/// <summary>Unpack a key into a tuple, and return only the last element</summary>
		/// <typeparam name="T">Expected type of the last element</typeparam>
		/// <param name="key">Packed version of a key that should fit inside this subspace</param>
		/// <returns>Converted value of the last element of the tuple</returns>
		/// <example>new Subspace([FE]).UnpackLast&lt;int&gt;([FE 02 'H' 'e' 'l' 'l' 'o' 00 15 1]) => (int) 1</example>
		public T DecodeLast<T>(Slice key)
		{
			return FdbTuple.DecodeLast<T>(m_subspace.ExtractKey(key, boundCheck: true));
		}

		/// <summary>Unpack an array of key into tuples, and return an array with only the first elements of each tuple</summary>
		/// <typeparam name="T">Expected type of the first element of all the keys</typeparam>
		/// <param name="keys">Array of packed keys that should all fit inside this subspace</param>
		/// <returns>Array containing the converted values of the first elements of each tuples</returns>
		[NotNull]
		public T[] DecodeKeysFirst<T>([NotNull] Slice[] keys)
		{
			if (keys == null) throw new ArgumentNullException("keys");

			var values = new T[keys.Length];
			for (int i = 0; i < keys.Length; i++)
			{
				//REVIEW: what should we do if we encounter Slice.Nil keys ??
				values[i] = FdbTuple.DecodeFirst<T>(m_subspace.ExtractKey(keys[i], boundCheck: true));
			}
			return values;
		}

		/// <summary>Unpack an array of key into tuples, and return an array with only the last elements of each tuple</summary>
		/// <typeparam name="T">Expected type of the last element of all the keys</typeparam>
		/// <param name="keys">Array of packed keys that should all fit inside this subspace</param>
		/// <returns>Array containing the converted values of the last elements of each tuples</returns>
		[NotNull]
		public T[] DecodeKeysLast<T>([NotNull] Slice[] keys)
		{
			if (keys == null) throw new ArgumentNullException("keys");

			var values = new T[keys.Length];
			for (int i = 0; i < keys.Length; i++)
			{
				//REVIEW: what should we do if we encounter Slice.Nil keys ??
				values[i] = FdbTuple.DecodeLast<T>(m_subspace.ExtractKey(keys[i], boundCheck: true));
			}
			return values;
		}

		/// <summary>Unpack an array of key into singleton tuples, and return an array with value of each tuple</summary>
		/// <typeparam name="T">Expected type of the only element of all the keys</typeparam>
		/// <param name="keys">Array of packed keys that should all fit inside this subspace</param>
		/// <returns>Array containing the converted values of the only elements of each tuples. Throws an exception if one key contains more than one element</returns>
		[NotNull]
		public T[] DecodeKeys<T>([NotNull] Slice[] keys)
		{
			if (keys == null) throw new ArgumentNullException("keys");

			var values = new T[keys.Length];
			for (int i = 0; i < keys.Length; i++)
			{
				//REVIEW: what should we do if we encounter Slice.Nil keys ??
				values[i] = FdbTuple.DecodeKey<T>(m_subspace.ExtractKey(keys[i], boundCheck: true));
			}
			return values;
		}

		#endregion

		#region Append: Subspace => Tuple

		/// <summary>Return an empty tuple that is attached to this subspace</summary>
		/// <returns>Empty tuple that can be extended, and whose packed representation will always be prefixed by the subspace key</returns>
		[NotNull]
		public IFdbTuple ToTuple()
		{
			return new FdbPrefixedTuple(m_subspace.Key, FdbTuple.Empty);
		}

		/// <summary>Attach a tuple to an existing subspace.</summary>
		/// <param name="tuple">Tuple whose items will be appended at the end of the current subspace</param>
		/// <returns>Tuple that wraps the items of <paramref name="tuple"/> and whose packed representation will always be prefixed by the subspace key.</returns>
		[NotNull]
		public IFdbTuple Concat([NotNull] IFdbTuple tuple)
		{
			return new FdbPrefixedTuple(m_subspace.Key, tuple);
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
			return new FdbPrefixedTuple(m_subspace.Key, tuple);
		}

		/// <summary>Create a new 1-tuple that is attached to this subspace</summary>
		/// <typeparam name="T">Type of the value to append</typeparam>
		/// <param name="value">Value that will be appended</param>
		/// <returns>Tuple of size 1 that contains <paramref name="value"/>, and whose packed representation will always be prefixed by the subspace key.</returns>
		/// <remarks>This is the equivalent of calling 'subspace.Create(FdbTuple.Create&lt;T&gt;(value))'</remarks>
		[NotNull]
		public IFdbTuple Append<T>(T value)
		{
			return new FdbPrefixedTuple(m_subspace.Key, FdbTuple.Create<T>(value));
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
			return new FdbPrefixedTuple(m_subspace.Key, FdbTuple.Create<T1, T2>(item1, item2));
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
			return new FdbPrefixedTuple(m_subspace.Key, FdbTuple.Create<T1, T2, T3>(item1, item2, item3));
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
			return new FdbPrefixedTuple(m_subspace.Key, FdbTuple.Create<T1, T2, T3, T4>(item1, item2, item3, item4));
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
			return new FdbPrefixedTuple(m_subspace.Key, FdbTuple.Create<T1, T2, T3, T4, T5>(item1, item2, item3, item4, item5));
		}

		#endregion

	}

}
