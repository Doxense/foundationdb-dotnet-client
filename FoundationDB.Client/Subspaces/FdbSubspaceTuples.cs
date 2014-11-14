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

namespace FoundationDB.Client
{
	using FoundationDB.Layers.Tuples;
	using JetBrains.Annotations;
	using System;
	using System.Linq;
	using System.Collections.Generic;
	using FoundationDB.Client.Utils;
	using System.Diagnostics;


	/// <summary>Provides of methods to encode and decodes keys using the Tuple Encoding format</summary>
	public struct FdbSubspaceTuples
	{

		/// <summary>Ref to the parent subspace</summary>
		private readonly IFdbSubspace m_subspace;

		/// <summary>Wraps an existing subspace</summary>
		/// <param name="subspace"></param>
		public FdbSubspaceTuples(IFdbSubspace subspace)
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
		/// <returns>Key which starts with the subspace prefix, followed by the packed representation of <paramref name="tuple"/>. This key can be parsed back to an equivalent tuple by calling <see cref="Unpack(IFdbTuple)"/>.</returns>
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
		/// <returns>Key which starts with the subspace prefix, followed by the packed representation of <paramref name="tuple"/>. This key can be parsed back to an equivalent tuple by calling <see cref="Unpack(IFdbTuple)"/>.</returns>
		/// <remarks>If <paramref name="tuple"/> is null or empty, then the prefix of the subspace is returned.</remarks>
		[DebuggerStepThrough]
		public Slice Pack([NotNull] IFdbTuple tuple)
		{
			if (tuple == null) throw new ArgumentNullException("tuple");
			return FdbTuple.PackWithPrefix(m_subspace.Key, tuple);
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

			return FdbTuple.PackRangeWithPrefix(m_subspace.Key, tuples);
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
		/// <returns>Key which starts with the subspace prefix, followed by the packed representation of <paramref name="tuple"/>. This key can be parsed back to an equivalent tuple by calling <see cref="Unpack(IFdbTuple)"/>.</returns>
		/// <remarks>If <paramref name="tuple"/> is null or empty, then the prefix of the subspace is returned.</remarks>
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

			return FdbTuple.PackRangeWithPrefix(m_subspace.Key, items.Select((item) => item.ToTuple()));
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
		/// <returns>Unpacked tuple that is relative to the current subspace, or null if the key is equal to Slice.Nil</returns>
		/// <example>new Subspace([FE]).Unpack([FE 02 'H' 'e' 'l' 'l' 'o' 00 15 1]) => ("hello", 1,)</example>
		/// <remarks>If <paramref name="key"/> is equal to the subspace prefix, then an empty tuple is returned.</remarks>
		/// <exception cref="System.ArgumentOutOfRangeException">If the unpacked tuple is not contained in this subspace</exception>
		[CanBeNull]
		public IFdbTuple Unpack(Slice key)
		{
			// We special case 'Slice.Nil' because it is returned by GetAsync(..) when the key does not exist
			// This is to simplifiy decoding logic where the caller could do "var foo = FdbTuple.Unpack(await tr.GetAsync(...))" and then only have to test "if (foo != null)"
			if (key.IsNull) return null;

			return FdbTuple.Unpack(m_subspace.ExtractKey(key, boundCheck: true));
		}

		/// <summary>Unpack an sequence of keys into tuples, with the subspace prefix removed</summary>
		/// <param name="keys">Packed version of keys inside this subspace</param>
		/// <returns>Unpacked tuples that are relative to the current subspace</returns>
		[NotNull]
		public IFdbTuple[] Unpack([NotNull] IEnumerable<Slice> keys)
		{
			// return an array with the keys minus the subspace's prefix
			var extracted = m_subspace.ExtractKeys(keys, boundCheck: true);

			// unpack everything
			var prefix = m_subspace.Key;
			var tuples = new IFdbTuple[extracted.Length];
			for(int i = 0; i < extracted.Length; i++)
			{
				if (extracted[i].HasValue) tuples[i] = new FdbPrefixedTuple(prefix, FdbTuple.Unpack(extracted[i]));
			}
			return tuples;
		}

		/// <summary>Unpack an array of keys into tuples, with the subspace prefix removed</summary>
		/// <param name="keys">Packed version of keys inside this subspace</param>
		/// <returns>Unpacked tuples that are relative to the current subspace</returns>
		[NotNull]
		public IFdbTuple[] Unpack([NotNull] params Slice[] keys)
		{
			return Unpack((IEnumerable<Slice>)keys);
		}

		#endregion

		#region ToRange: Tuple => Range

		public FdbKeyRange ToRange([NotNull] IFdbTuple tuple)
		{
			if (tuple == null) throw new ArgumentNullException("tuple");
			return m_subspace.ToRange(tuple.ToSlice());
		}

		public FdbKeyRange ToRange([NotNull] ITupleFormattable item)
		{
			if (item == null) throw new ArgumentNullException("item");
			return ToRange(item.ToTuple());
		}

		#endregion

		#region EncodeKey: (T1, T2, ...) => Slice

		/// <summary>Create a new key by appending a value to the current subspace</summary>
		/// <typeparam name="T">Type of the value</typeparam>
		/// <param name="key">Value that will be appended at the end of the key</param>
		/// <returns>Key the correspond to the concatenation of the current subspace's prefix and <paramref name="key"/></returns>
		/// <example>tuple.Pack(x) is equivalent to tuple.Append(x).ToSlice()</example>
		public Slice EncodeKey<T>(T key)
		{
			return FdbTuple.PackWithPrefix<T>(m_subspace.Key, key);
		}

		/// <summary>Create a new key by appending two values to the current subspace</summary>
		/// <typeparam name="T1">Type of the next to last value</typeparam>
		/// <typeparam name="T2">Type of the last value</typeparam>
		/// <param name="key1">Value that will be in the next to last position</param>
		/// <param name="key2">Value that will be in the last position</param>
		/// <returns>Key the correspond to the concatenation of the current subspace's prefix, <paramref name="key1"/> and <paramref name="key2"/></returns>
		/// <example>(...,).Pack(x, y) is equivalent to (...,).Append(x).Append(y).ToSlice()</example>
		public Slice EncodeKey<T1, T2>(T1 key1, T2 key2)
		{
			return FdbTuple.PackWithPrefix<T1, T2>(m_subspace.Key, key1, key2);
		}

		/// <summary>Create a new key by appending three values to the current subspace</summary>
		/// <typeparam name="T1">Type of the first value</typeparam>
		/// <typeparam name="T2">Type of the second value</typeparam>
		/// <typeparam name="T3">Type of the thrid value</typeparam>
		/// <param name="key1">Value that will be appended first</param>
		/// <param name="key2">Value that will be appended second</param>
		/// <param name="key3">Value that will be appended third</param>
		/// <returns>Key the correspond to the concatenation of the current subspace's prefix, <paramref name="key1"/>, <paramref name="key2"/> and <paramref name="key3"/></returns>
		/// <example>tuple.Pack(x, y, z) is equivalent to tuple.Append(x).Append(y).Append(z).ToSlice()</example>
		public Slice EncodeKey<T1, T2, T3>(T1 key1, T2 key2, T3 key3)
		{
			return FdbTuple.PackWithPrefix<T1, T2, T3>(m_subspace.Key, key1, key2, key3);
		}

		/// <summary>Create a new key by appending three values to the current subspace</summary>
		/// <typeparam name="T1">Type of the first value</typeparam>
		/// <typeparam name="T2">Type of the second value</typeparam>
		/// <typeparam name="T3">Type of the third value</typeparam>
		/// <typeparam name="T4">Type of the fourth value</typeparam>
		/// <param name="key1">Value that will be appended first</param>
		/// <param name="key2">Value that will be appended second</param>
		/// <param name="key3">Value that will be appended third</param>
		/// <param name="key4">Value that will be appended fourth</param>
		/// <returns>Key the correspond to the concatenation of the current subspace's prefix, <paramref name="key1"/>, <paramref name="key2"/>, <paramref name="key3"/> and <paramref name="key4"/></returns>
		/// <example>tuple.Pack(w, x, y, z) is equivalent to tuple.Append(w).Append(x).Append(y).Append(z).ToSlice()</example>
		public Slice EncodeKey<T1, T2, T3, T4>(T1 key1, T2 key2, T3 key3, T4 key4)
		{
			return FdbTuple.PackWithPrefix<T1, T2, T3, T4>(m_subspace.Key, key1, key2, key3, key4);
		}

		/// <summary>Merge a sequence of keys with the subspace's prefix, all sharing the same buffer</summary>
		/// <typeparam name="T">Type of the keys</typeparam>
		/// <param name="keys">Sequence of keys to pack</param>
		/// <returns>Array of slices (for all keys) that share the same underlying buffer</returns>
		[NotNull]
		public Slice[] EncodeKeys<T>([NotNull] IEnumerable<T> keys)
		{
			return FdbTuple.PackRangeWithPrefix<T>(m_subspace.Key, keys);
		}

		/// <summary>Merge a sequence of keys with the subspace's prefix, all sharing the same buffer</summary>
		/// <typeparam name="T">Type of the keys</typeparam>
		/// <param name="keys">Sequence of keys to pack</param>
		/// <returns>Array of slices (for all keys) that share the same underlying buffer</returns>
		[NotNull]
		public Slice[] EncodeKeys<T>([NotNull] T[] keys)
		{
			return FdbTuple.PackRangeWithPrefix<T>(m_subspace.Key, keys);
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
			return FdbTuple.PackRangeWithPrefix<TKey, TElement>(m_subspace.Key, elements, selector);
		}

		#endregion

		#region DecodeKey: Slice => (T1, T2, ...)

		/// <summary>Unpack a key into a singleton tuple, and return the single element</summary>
		/// <typeparam name="T">Expected type of the only element</typeparam>
		/// <param name="key">Packed version of a key that should fit inside this subspace</param>
		/// <returns>Converted value of the only element in the tuple. Throws an exception if the tuple is empty or contains more than one element</returns>
		/// <example>new Subspace([FE]).UnpackSingle&lt;int&gt;([FE 02 'H' 'e' 'l' 'l' 'o' 00]) => (string) "Hello"</example>
		public T DecodeKey<T>(Slice key)
		{
			return FdbTuple.UnpackSingle<T>(m_subspace.ExtractKey(key, boundCheck: true));
		}


		public FdbTuple<T1, T2> DecodeKey<T1, T2>(Slice key)
		{
			var tuple = Unpack(key);
			if (tuple == null) throw new FormatException("The specified key does not contain any items");
			if (tuple.Count != 2) throw new FormatException("The specified key is not a tuple with 2 items");

			return FdbTuple.Create<T1, T2>(
				tuple.Get<T1>(0),
				tuple.Get<T2>(1)
			);
		}

		public FdbTuple<T1, T2, T3> DecodeKey<T1, T2, T3>(Slice key)
		{
			var tuple = Unpack(key);
			if (tuple == null) throw new FormatException("The specified key does not contain any items");
			if (tuple.Count != 3) throw new FormatException("The specified key is not a tuple with 3 items");

			return FdbTuple.Create<T1, T2, T3>(
				tuple.Get<T1>(0),
				tuple.Get<T2>(1),
				tuple.Get<T3>(2)
			);
		}

		public FdbTuple<T1, T2, T3, T4> DecodeKey<T1, T2, T3, T4>(Slice key)
		{
			var tuple = Unpack(key);
			if (tuple == null) throw new FormatException("The specified key does not contain any items");
			if (tuple.Count != 4) throw new FormatException("The specified key is not a tuple with 4 items");

			return FdbTuple.Create<T1, T2, T3, T4>(
				tuple.Get<T1>(0),
				tuple.Get<T2>(1),
				tuple.Get<T3>(2),
				tuple.Get<T4>(3)
			);
		}

		/// <summary>Unpack a key into a tuple, and return only the first element</summary>
		/// <typeparam name="T">Expected type of the last element</typeparam>
		/// <param name="key">Packed version of a key that should fit inside this subspace</param>
		/// <returns>Converted value of the last element of the tuple</returns>
		/// <example>new Subspace([FE]).UnpackLast&lt;int&gt;([FE 02 'H' 'e' 'l' 'l' 'o' 00 15 1]) => (string) "Hello"</example>
		public T DecodeFirst<T>(Slice key)
		{
			return FdbTuple.UnpackFirst<T>(m_subspace.ExtractKey(key, boundCheck: true));
		}

		/// <summary>Unpack a key into a tuple, and return only the last element</summary>
		/// <typeparam name="T">Expected type of the last element</typeparam>
		/// <param name="key">Packed version of a key that should fit inside this subspace</param>
		/// <returns>Converted value of the last element of the tuple</returns>
		/// <example>new Subspace([FE]).UnpackLast&lt;int&gt;([FE 02 'H' 'e' 'l' 'l' 'o' 00 15 1]) => (int) 1</example>
		public T DecodeLast<T>(Slice key)
		{
			return FdbTuple.UnpackLast<T>(m_subspace.ExtractKey(key, boundCheck: true));
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
				values[i] = FdbTuple.UnpackFirst<T>(m_subspace.ExtractKey(keys[i], boundCheck: true));
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
				values[i] = FdbTuple.UnpackLast<T>(m_subspace.ExtractKey(keys[i], boundCheck: true));
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
				values[i] = FdbTuple.UnpackSingle<T>(m_subspace.ExtractKey(keys[i], boundCheck: true));
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
		/// <param name="value1">First value that will be appended</param>
		/// <param name="value2">Second value that will be appended</param>
		/// <returns>Tuple of size 2 that contains <paramref name="value1"/> and <paramref name="value2"/>, and whose packed representation will always be prefixed by the subspace key.</returns>
		/// <remarks>This is the equivalent of calling 'subspace.Create(FdbTuple.Create&lt;T1, T2&gt;(value1, value2))'</remarks>
		[NotNull]
		public IFdbTuple Append<T1, T2>(T1 value1, T2 value2)
		{
			return new FdbPrefixedTuple(m_subspace.Key, FdbTuple.Create<T1, T2>(value1, value2));
		}

		/// <summary>Create a new 3-tuple that is attached to this subspace</summary>
		/// <typeparam name="T1">Type of the first value to append</typeparam>
		/// <typeparam name="T2">Type of the second value to append</typeparam>
		/// <typeparam name="T3">Type of the third value to append</typeparam>
		/// <param name="value1">First value that will be appended</param>
		/// <param name="value2">Second value that will be appended</param>
		/// <param name="value3">Third value that will be appended</param>
		/// <returns>Tuple of size 3 that contains <paramref name="value1"/>, <paramref name="value2"/> and <paramref name="value3"/>, and whose packed representation will always be prefixed by the subspace key.</returns>
		/// <remarks>This is the equivalent of calling 'subspace.Create(FdbTuple.Create&lt;T1, T2, T3&gt;(value1, value2, value3))'</remarks>
		[NotNull]
		public IFdbTuple Append<T1, T2, T3>(T1 value1, T2 value2, T3 value3)
		{
			return new FdbPrefixedTuple(m_subspace.Key, FdbTuple.Create<T1, T2, T3>(value1, value2, value3));
		}

		/// <summary>Create a new 4-tuple that is attached to this subspace</summary>
		/// <typeparam name="T1">Type of the first value to append</typeparam>
		/// <typeparam name="T2">Type of the second value to append</typeparam>
		/// <typeparam name="T3">Type of the third value to append</typeparam>
		/// <typeparam name="T4">Type of the fourth value to append</typeparam>
		/// <param name="value1">First value that will be appended</param>
		/// <param name="value2">Second value that will be appended</param>
		/// <param name="value3">Third value that will be appended</param>
		/// <param name="value4">Fourth value that will be appended</param>
		/// <returns>Tuple of size 4 that contains <paramref name="value1"/>, <paramref name="value2"/>, <paramref name="value3"/> and <paramref name="value4"/>, and whose packed representation will always be prefixed by the subspace key.</returns>
		/// <remarks>This is the equivalent of calling 'subspace.Create(FdbTuple.Create&lt;T1, T2, T3, T4&gt;(value1, value2, value3, value4))'</remarks>
		[NotNull]
		public IFdbTuple Append<T1, T2, T3, T4>(T1 value1, T2 value2, T3 value3, T4 value4)
		{
			return new FdbPrefixedTuple(m_subspace.Key, FdbTuple.Create<T1, T2, T3, T4>(value1, value2, value3, value4));
		}

		#endregion

	}

}
