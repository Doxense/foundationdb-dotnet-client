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

namespace FoundationDB.Client
{
	using FoundationDB.Client.Utils;
	using FoundationDB.Layers.Tuples;
	using System;
	using System.Collections.Generic;
	using System.Linq;
	using System.Threading;
	using System.Threading.Tasks;

	/// <summary>Extensions methods to add FdbSubspace overrides to various types</summary>
	public static class FdbSubspaceExtensions
	{

		#region FDB API...

		/// <summary>Clear the entire content of a subspace</summary>
		public static void ClearRange(this IFdbTransaction trans, FdbSubspace subspace)
		{
			Contract.Requires(trans != null && subspace != null);

			trans.ClearRange(FdbKeyRange.StartsWith(subspace.Key));
		}

		/// <summary>Clear the entire content of a subspace</summary>
		public static Task ClearRangeAsync(this IFdbTransactional db, FdbSubspace subspace, CancellationToken cancellationToken)
		{
			if (db == null) throw new ArgumentNullException("db");
			if (subspace == null) throw new ArgumentNullException("subspace");

			return db.WriteAsync((tr) => ClearRange(tr, subspace), cancellationToken);
		}

		/// <summary>Returns all the keys inside of a subspace</summary>
		public static FdbRangeQuery<KeyValuePair<Slice, Slice>> GetRangeStartsWith(this IFdbReadOnlyTransaction trans, FdbSubspace subspace, FdbRangeOptions options = null)
		{
			Contract.Requires(trans != null && subspace != null);

			return trans.GetRange(FdbKeyRange.StartsWith(subspace.Key), options);
		}

		/// <summary>Read a key inside a subspace</summary>
		/// <example>
		/// Both lines are equivalent:
		/// tr.GetAsync(new FdbSubspace("Hello"), FdbTuple.Create("World"));
		/// tr.GetAsync(FdbTuple.Create("Hello", "World"));
		/// </example>
		public static Task<Slice> GetAsync(this IFdbReadOnlyTransaction trans, FdbSubspace subspace, IFdbTuple key)
		{
			Contract.Requires(trans != null && subspace != null && key != null);

			return trans.GetAsync(subspace.Pack(key));
		}

		/// <summary>Write a key inside a subspace</summary>
		/// <example>
		/// Both lines are equivalent:
		/// tr.Set(new FdbSubspace("Hello"), FdbTuple.Create("World"), some_value);
		/// tr.Set(FdbTuple.Create("Hello", "World"), some_value);
		/// </example>
		public static void Set(this IFdbTransaction trans, FdbSubspace subspace, IFdbTuple key, Slice value)
		{
			Contract.Requires(trans != null && subspace != null && key != null);

			trans.Set(subspace.Pack(key), value);
		}

		#endregion

		#region Contains...

		/// <summary>Tests whether the specified <paramref name="key"/> starts with this Subspace's prefix, indicating that the Subspace logically contains <paramref name="key"/>.</summary>
		/// <param name="key">The key to be tested</param>
		/// <exception cref="System.ArgumentNullException">If <paramref name="key"/> is null</exception>
		public static bool Contains<TKey>(this FdbSubspace subspace, TKey key)
			where TKey : IFdbKey
		{
			if (key == null) throw new ArgumentNullException("key");
			return subspace.Contains(key.ToFoundationDbKey());
		}

		#endregion

		#region Concat...

		/// <summary>Append a key to the subspace key</summary>
		public static Slice Concat(this IFdbSubspace subspace, Slice key)
		{
			return Slice.Concat(subspace.ToFoundationDbKey(), key);
		}

		/// <summary>Append a key to the subspace key</summary>
		/// <typeparam name="TKey">type of the key, must implements IFdbKey</typeparam>
		/// <param name="key"></param>
		/// <returns>Return Slice : 'subspace.Key + key'</returns>
		public static Slice Concat<TKey>(this IFdbSubspace subspace, TKey key)
			where TKey : IFdbKey
		{
			if (key == null) throw new ArgumentNullException("key");
			return Slice.Concat(subspace.ToFoundationDbKey(), key.ToFoundationDbKey());
		}

		/// <summary>Merge an array of keys with the subspace's prefix, all sharing the same buffer</summary>
		/// <param name="keys">Array of keys to pack</param>
		/// <returns>Array of slices (for all keys) that share the same underlying buffer</returns>
		public static Slice[] ConcatRange(this IFdbSubspace subspace, params Slice[] keys)
		{
			if (keys == null) throw new ArgumentNullException("keys");
			return subspace.ToFoundationDbKey().ConcatRange(keys);
		}

		/// <summary>Merge a sequence of keys with the subspace's prefix, all sharing the same buffer</summary>
		/// <param name="keys">Sequence of keys to pack</param>
		/// <returns>Array of slices (for all keys) that share the same underlying buffer</returns>
		public static Slice[] ConcatRange(this IFdbSubspace subspace, IEnumerable<Slice> keys)
		{
			if (keys == null) throw new ArgumentNullException("keys");
			return subspace.ToFoundationDbKey().ConcatRange(keys);
		}

		/// <summary>Append a sequence of keys with the subspace's prefix, all sharing the same buffer</summary>
		/// <typeparam name="TKey">type of the key, must implements IFdbKey</typeparam>
		/// <param name="keys"></param>
		/// <returns>Return Slice : 'subspace.Key + key'</returns>
		public static Slice[] ConcatRange<TKey>(this IFdbSubspace subspace, IEnumerable<TKey> keys)
			where TKey : IFdbKey
		{
			if (keys == null) throw new ArgumentNullException("keys");
			return subspace.ToFoundationDbKey().ConcatRange(keys.Select((key) => key.ToFoundationDbKey()));
		}

		#endregion

		#region Partition...

		/// <summary>Partition this subspace into a child subspace</summary>
		/// <typeparam name="T">Type of the child subspace key</typeparam>
		/// <param name="value">Value of the child subspace</param>
		/// <returns>New subspace that is logically contained by the current subspace</returns>
		/// <remarks>Subspace([Foo, ]).Partition(Bar) is equivalent to Subspace([Foo, Bar, ])</remarks>
		/// <example>
		/// new FdbSubspace(["Users", ]).Partition("Contacts") == new FdbSubspace(["Users", "Contacts", ])
		/// </example>
		public static FdbSubspace Partition<T>(this IFdbSubspace subspace, T value)
		{
			//TODO: this should go into a FdbTupleSubspace, because it collides with FdbEncoderSubspace<T> !
			return new FdbSubspace(FdbTuple.Concat<T>(subspace.ToFoundationDbKey(), value));
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
		public static FdbSubspace Partition<T1, T2>(this IFdbSubspace subspace, T1 value1, T2 value2)
		{
			//TODO: this should go into a FdbTupleSubspace, because it collides with FdbEncoderSubspace<T1, T2> !
			return new FdbSubspace(FdbTuple.Concat<T1, T2>(subspace.ToFoundationDbKey(), value1, value2));
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
		public static FdbSubspace Partition<T1, T2, T3>(this IFdbSubspace subspace, T1 value1, T2 value2, T3 value3)
		{
			//TODO: this should go into a FdbTupleSubspace, because it collides with FdbEncoderSubspace<T1, T2, T3> !
			return new FdbSubspace(FdbTuple.Concat(subspace.ToFoundationDbKey(), new FdbTuple<T1, T2, T3>(value1, value2, value3)));
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
		public static FdbSubspace Partition<T1, T2, T3, T4>(this IFdbSubspace subspace, T1 value1, T2 value2, T3 value3, T4 value4)
		{
			//TODO: this should go into a FdbTupleSubspace, because it collides with FdbEncoderSubspace<T1, T2, T3, T4> !
			return new FdbSubspace(FdbTuple.Concat(subspace.ToFoundationDbKey(), new FdbTuple<T1, T2, T3, T4>(value1, value2, value3, value4)));
		}

		/// <summary>Parition this subspace by appending a tuple</summary>
		/// <param name="tuple">Tuple that will be used for this partition</param>
		/// <returns>New subspace that is creating by combining the namespace prefix and <paramref name="tuple"/></returns>
		/// <remarks>Subspace([Foo, ]).Partition([Bar, Baz, ]) is equivalent to Subspace([Foo, Bar, Baz,])</remarks>
		/// <example>
		/// new FdbSubspace(["Users", ]).Partition(["Contacts", "Friends", ]) => new FdbSubspace(["Users", "Contacts", "Friends", ])
		/// </example>
		public static FdbSubspace Partition(this IFdbSubspace subspace, IFdbTuple tuple)
		{
			if (tuple == null) throw new ArgumentNullException("tuple");
			if (tuple.Count == 0)
				return new FdbSubspace(subspace.ToFoundationDbKey());
			else
				return new FdbSubspace(FdbTuple.Concat(subspace.ToFoundationDbKey(), tuple));
		}

		/// <summary>Partition this subspace into a child subspace</summary>
		/// <param name="formattable">a ITupleFormattable, <paramref name="formattable"/>.ToTuple() will be used for this partition</param>
		/// <returns>New subspace that is creating by combining the namespace prefix and <paramref name="formattable"/></returns>
		/// <remarks>Subspace([Foo, ]).Partition(Bar) is equivalent to Subspace([Foo, Bar, ])</remarks>
		/// <example>
		/// new FdbSubspace(["Users", ]).Partition("Contacts") == new FdbSubspace(["Users", "Contacts", ])
		/// </example>
		public static FdbSubspace Partition(this IFdbSubspace subspace, ITupleFormattable formattable)
		{
			if (formattable == null) throw new ArgumentNullException("formattable");
			var tuple = formattable.ToTuple();
			if (tuple == null) throw new InvalidOperationException("Formattable item returned an empty tuple");
			return Partition(subspace, tuple);
		}

		#endregion

		#region Tuples...

		/// <summary>Return an empty tuple that is attached to this subspace</summary>
		/// <returns>Empty tuple that can be extended, and whose packed representation will always be prefixed by the subspace key</returns>
		public static IFdbTuple ToTuple(this IFdbSubspace subspace)
		{
			return new FdbPrefixedTuple(subspace.ToFoundationDbKey(), FdbTuple.Empty);
		}

		/// <summary>Attach a tuple to an existing subspace.</summary>
		/// <param name="tuple">Tuple whose items will be appended at the end of the current subspace</param>
		/// <returns>Tuple that wraps the items of <paramref name="tuple"/> and whose packed representation will always be prefixed by the subspace key.</returns>
		public static IFdbTuple Append(this IFdbSubspace subspace, IFdbTuple tuple)
		{
			return new FdbPrefixedTuple(subspace.ToFoundationDbKey(), tuple);
		}

		public static IFdbTuple AppendBoxed(this IFdbSubspace subspace, object value)
		{
			return new FdbPrefixedTuple(subspace.ToFoundationDbKey(), FdbTuple.CreateBoxed(value));
		}

		/// <summary>Convert a formattable item into a tuple that is attached to this subspace.</summary>
		/// <param name="formattable">Item that can be converted into a tuple</param>
		/// <returns>Tuple that is the logical representation of the item, and whose packed representation will always be prefixed by the subspace key.</returns>
		/// <remarks>This is the equivalent of calling 'subspace.Create(formattable.ToTuple())'</remarks>
		public static IFdbTuple Append(this IFdbSubspace subspace, ITupleFormattable formattable)
		{
			if (formattable == null) throw new ArgumentNullException("formattable");
			var tuple = formattable.ToTuple();
			if (tuple == null) throw new InvalidOperationException("Formattable item cannot return an empty tuple");
			return new FdbPrefixedTuple(subspace.ToFoundationDbKey(), tuple);
		}

		/// <summary>Create a new 1-tuple that is attached to this subspace</summary>
		/// <typeparam name="T">Type of the value to append</typeparam>
		/// <param name="value">Value that will be appended</param>
		/// <returns>Tuple of size 1 that contains <paramref name="value"/>, and whose packed representation will always be prefixed by the subspace key.</returns>
		/// <remarks>This is the equivalent of calling 'subspace.Create(FdbTuple.Create&lt;T&gt;(value))'</remarks>
		public static IFdbTuple Append<T>(this IFdbSubspace subspace, T value)
		{
			return new FdbPrefixedTuple(subspace.ToFoundationDbKey(), FdbTuple.Create<T>(value));
		}

		/// <summary>Create a new 2-tuple that is attached to this subspace</summary>
		/// <typeparam name="T1">Type of the first value to append</typeparam>
		/// <typeparam name="T2">Type of the second value to append</typeparam>
		/// <param name="value1">First value that will be appended</param>
		/// <param name="value2">Second value that will be appended</param>
		/// <returns>Tuple of size 2 that contains <paramref name="value1"/> and <paramref name="value2"/>, and whose packed representation will always be prefixed by the subspace key.</returns>
		/// <remarks>This is the equivalent of calling 'subspace.Create(FdbTuple.Create&lt;T1, T2&gt;(value1, value2))'</remarks>
		public static IFdbTuple Append<T1, T2>(this IFdbSubspace subspace, T1 value1, T2 value2)
		{
			return new FdbPrefixedTuple(subspace.ToFoundationDbKey(), FdbTuple.Create<T1, T2>(value1, value2));
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
		public static IFdbTuple Append<T1, T2, T3>(this IFdbSubspace subspace, T1 value1, T2 value2, T3 value3)
		{
			return new FdbPrefixedTuple(subspace.ToFoundationDbKey(), FdbTuple.Create<T1, T2, T3>(value1, value2, value3));
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
		public static IFdbTuple Append<T1, T2, T3, T4>(this IFdbSubspace subspace, T1 value1, T2 value2, T3 value3, T4 value4)
		{
			return new FdbPrefixedTuple(subspace.ToFoundationDbKey(), FdbTuple.Create<T1, T2, T3, T4>(value1, value2, value3, value4));
		}

		/// <summary>Create a new N-tuple that is attached to this subspace</summary>
		/// <param name="items">Array of items of the new tuple</param>
		/// <returns>Tuple of size <paramref name="items"/>.Length, and whose packed representation will always be prefixed by the subspace key.</returns>
		/// <remarks>This is the equivalent of calling 'subspace.Create(FdbTuple.Create(items))'</remarks>
		public static IFdbTuple AppendBoxed(this IFdbSubspace subspace, params object[] items)
		{ //REVIEW: Append(arrayOfObjects) is ambiguous with Append(new object[] { arrayOfObjects }) because an object[] is also an object
			return Append(subspace, FdbTuple.Create(items));
		}

		/// <summary>Create a new key by appending a formattable object to the current subspace</summary>
		/// <param name="tuple">Tuple to pack (can be empty)</param>
		/// <returns>Key the correspond to the concatenation of the current subspace's prefix and the packed representation of <paramref name="tuple"/></returns>
		public static Slice Pack(this IFdbSubspace subspace, IFdbTuple tuple)
		{
			return FdbTuple.Concat(subspace.ToFoundationDbKey(), tuple);
		}

		/// <summary>Create a new key by appending a formattable object to the current subspace</summary>
		/// <param name="item">Instance of a type that can be transformed into a Tuple</param>
		/// <returns>Key the correspond to the concatenation of the current subspace's prefix and the packed representation of the tuple returned by <paramref name="item"/>.ToTuple()</returns>
		/// <exception cref="System.ArgumentNullException">If <paramref name="item"/> is null</exception>
		/// <exception cref="System.InvalidOperationException">If calling <paramref name="item"/>.ToTuple() returns null.</exception>
		public static Slice Pack(this IFdbSubspace subspace, ITupleFormattable item)
		{
			if (item == null) throw new ArgumentNullException("item");
			var tuple = item.ToTuple();
			if (tuple == null) throw new InvalidOperationException("The specified item returned a null tuple");
			return Pack(subspace, tuple);
		}

		/// <summary>Create a new key by appending a value to the current subspace</summary>
		/// <param name="item">Value that will be appended at the end of the key</param>
		/// <returns>Key the correspond to the concatenation of the current subspace's prefix and <paramref name="item"/></returns>
		/// <example>tuple.PackBoxed(x) is the non-generic equivalent of tuple.Pack&lt;object&gt;(tuple)</example>
		public static Slice PackBoxed(this IFdbSubspace subspace, object item)
		{
			return FdbTuple.ConcatBoxed(subspace.ToFoundationDbKey(), item);
		}

		/// <summary>Create a new key by appending a value to the current subspace</summary>
		/// <typeparam name="T">Type of the value</typeparam>
		/// <param name="key">Value that will be appended at the end of the key</param>
		/// <returns>Key the correspond to the concatenation of the current subspace's prefix and <paramref name="key"/></returns>
		/// <example>tuple.Pack(x) is equivalent to tuple.Append(x).ToSlice()</example>
		public static Slice Pack<T>(this IFdbSubspace subspace, T key)
		{
			return FdbTuple.Concat<T>(subspace.ToFoundationDbKey(), key);
		}

		/// <summary>Create a new key by appending two values to the current subspace</summary>
		/// <typeparam name="T1">Type of the next to last value</typeparam>
		/// <typeparam name="T2">Type of the last value</typeparam>
		/// <param name="key1">Value that will be in the next to last position</param>
		/// <param name="key2">Value that will be in the last position</param>
		/// <returns>Key the correspond to the concatenation of the current subspace's prefix, <paramref name="key1"/> and <paramref name="key2"/></returns>
		/// <example>(...,).Pack(x, y) is equivalent to (...,).Append(x).Append(y).ToSlice()</example>
		public static Slice Pack<T1, T2>(this IFdbSubspace subspace, T1 key1, T2 key2)
		{
			return FdbTuple.Concat<T1, T2>(subspace.ToFoundationDbKey(), key1, key2);
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
		public static Slice Pack<T1, T2, T3>(this IFdbSubspace subspace, T1 key1, T2 key2, T3 key3)
		{
			return FdbTuple.Concat<T1, T2, T3>(subspace.ToFoundationDbKey(), key1, key2, key3);
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
		public static Slice Pack<T1, T2, T3, T4>(this IFdbSubspace subspace, T1 key1, T2 key2, T3 key3, T4 key4)
		{
			return FdbTuple.Concat<T1, T2, T3, T4>(subspace.ToFoundationDbKey(), key1, key2, key3, key4);
		}

		/// <summary>Pack a sequence of tuples, all sharing the same buffer</summary>
		/// <param name="tuples">Sequence of N-tuples to pack</param>
		/// <returns>Array containing the buffer segment of each packed tuple</returns>
		/// <example>BatchPack("abc", [ ("Foo", 1), ("Foo", 2) ]) => [ "abc\x02Foo\x00\x15\x01", "abc\x02Foo\x00\x15\x02" ] </example>
		public static Slice[] PackRange(this IFdbSubspace subspace, params IFdbTuple[] tuples)
		{
			return FdbTuple.PackRange(subspace.ToFoundationDbKey(), tuples);
		}

		/// <summary>Pack a sequence of tuples, all sharing the same buffer</summary>
		/// <param name="tuples">Sequence of N-tuples to pack</param>
		/// <returns>Array containing the buffer segment of each packed tuple</returns>
		/// <example>BatchPack("abc", [ ("Foo", 1), ("Foo", 2) ]) => [ "abc\x02Foo\x00\x15\x01", "abc\x02Foo\x00\x15\x02" ] </example>
		public static Slice[] PackRange(this IFdbSubspace subspace, IEnumerable<IFdbTuple> tuples)
		{
			return FdbTuple.PackRange(subspace.ToFoundationDbKey(), tuples);
		}

		/// <summary>Merge a sequence of keys with the subspace's prefix, all sharing the same buffer</summary>
		/// <typeparam name="T">Type of the keys</typeparam>
		/// <param name="keys">Sequence of keys to pack</param>
		/// <returns>Array of slices (for all keys) that share the same underlying buffer</returns>
		public static Slice[] PackRange<T>(this IFdbSubspace subspace, IEnumerable<T> keys)
		{
			return FdbTuple.PackRange<T>(subspace.ToFoundationDbKey(), keys);
		}

		/// <summary>Merge a sequence of keys with the subspace's prefix, all sharing the same buffer</summary>
		/// <typeparam name="T">Type of the keys</typeparam>
		/// <param name="keys">Sequence of keys to pack</param>
		/// <returns>Array of slices (for all keys) that share the same underlying buffer</returns>
		public static Slice[] PackRange<T>(this IFdbSubspace subspace, T[] keys)
		{
			return FdbTuple.PackRange<T>(subspace.ToFoundationDbKey(), keys);
		}

		/// <summary>Pack a sequence of keys with the subspace's prefix, all sharing the same buffer</summary>
		/// <param name="keys">Sequence of keys to pack</param>
		/// <returns>Array of slices (for all keys) that share the same underlying buffer</returns>
		public static Slice[] PackBoxedRange(this IFdbSubspace subspace, IEnumerable<object> keys)
		{
			return FdbTuple.PackBoxedRange(subspace.ToFoundationDbKey(), keys);
		}

		/// <summary>Pack a sequence of keys with the subspace's prefix, all sharing the same buffer</summary>
		/// <param name="keys">Sequence of keys to pack</param>
		/// <returns>Array of slices (for all keys) that share the same underlying buffer</returns>
		public static Slice[] PackBoxedRange(this IFdbSubspace subspace, object[] keys)
		{
			//note: cannot use "params object[]" because it may conflict with PackRange(IEnumerable<object>)
			return FdbTuple.PackBoxedRange(subspace.ToFoundationDbKey(), keys);
		}

		#endregion

		#region Unpack...

		//REVIEW: right now we can't hook these methods to the IFdbKey interface because we need "ExtractAndCheck" that is on defined on FdbSubspace

		/// <summary>Unpack a key into a tuple, with the subspace prefix removed</summary>
		/// <param name="key">Packed version of a key that should fit inside this subspace.</param>
		/// <returns>Unpacked tuple that is relative to the current subspace, or null if the key is equal to Slice.Nil</returns>
		/// <example>new Subspace([FE]).Unpack([FE 02 'H' 'e' 'l' 'l' 'o' 00 15 1]) => ("hello", 1,)</example>
		/// <exception cref="System.ArgumentOutOfRangeException">If the unpacked tuple is not contained in this subspace</exception>
		public static IFdbTuple Unpack(this FdbSubspace subspace, Slice key)
		{
			// We special case 'Slice.Nil' because it is returned by GetAsync(..) when the key does not exist
			// This is to simplifiy decoding logic where the caller could do "var foo = FdbTuple.Unpack(await tr.GetAsync(...))" and then only have to test "if (foo != null)"
			if (key.IsNull) return null;

			return new FdbPrefixedTuple(subspace.Key, FdbTuple.Unpack(subspace.ExtractAndCheck(key)));
		}

		/// <summary>Unpack a key into a tuple, and return only the first element</summary>
		/// <typeparam name="T">Expected type of the last element</typeparam>
		/// <param name="key">Packed version of a key that should fit inside this subspace</param>
		/// <returns>Converted value of the last element of the tuple</returns>
		/// <example>new Subspace([FE]).UnpackLast&lt;int&gt;([FE 02 'H' 'e' 'l' 'l' 'o' 00 15 1]) => (string) "Hello"</example>
		public static T UnpackFirst<T>(this FdbSubspace subspace, Slice key)
		{
			return FdbTuple.UnpackFirst<T>(subspace.ExtractAndCheck(key));
		}

		/// <summary>Unpack a key into a tuple, and return only the last element</summary>
		/// <typeparam name="T">Expected type of the last element</typeparam>
		/// <param name="key">Packed version of a key that should fit inside this subspace</param>
		/// <returns>Converted value of the last element of the tuple</returns>
		/// <example>new Subspace([FE]).UnpackLast&lt;int&gt;([FE 02 'H' 'e' 'l' 'l' 'o' 00 15 1]) => (int) 1</example>
		public static T UnpackLast<T>(this FdbSubspace subspace, Slice key)
		{
			return FdbTuple.UnpackLast<T>(subspace.ExtractAndCheck(key));
		}

		/// <summary>Unpack a key into a singleton tuple, and return the single element</summary>
		/// <typeparam name="T">Expected type of the only element</typeparam>
		/// <param name="key">Packed version of a key that should fit inside this subspace</param>
		/// <returns>Converted value of the only element in the tuple. Throws an exception if the tuple is empty or contains more than one element</returns>
		/// <example>new Subspace([FE]).UnpackSingle&lt;int&gt;([FE 02 'H' 'e' 'l' 'l' 'o' 00]) => (string) "Hello"</example>
		public static T UnpackSingle<T>(this FdbSubspace subspace, Slice key)
		{
			return FdbTuple.UnpackSingle<T>(subspace.ExtractAndCheck(key));
		}

		/// <summary>Unpack an array of keys in tuples, with the subspace prefix removed</summary>
		/// <param name="keys">Packed version of keys inside this subspace</param>
		/// <returns>Unpacked tuples that are relative to the current subspace</returns>
		public static IFdbTuple[] Unpack(this FdbSubspace subspace, Slice[] keys)
		{
			if (keys == null) throw new ArgumentNullException("keys");

			var prefix = subspace.Key;
			var tuples = new IFdbTuple[keys.Length];

			if (keys.Length > 0)
			{
				for (int i = 0; i < keys.Length; i++)
				{
					if (keys[i].HasValue)
					{
						tuples[i] = new FdbPrefixedTuple(prefix, FdbTuple.Unpack(subspace.ExtractAndCheck(keys[i])));
					}
				}
			}

			return tuples;
		}

		/// <summary>Unpack an array of key into tuples, and return an array with only the first elements of each tuple</summary>
		/// <typeparam name="T">Expected type of the first element of all the keys</typeparam>
		/// <param name="keys">Array of packed keys that should all fit inside this subspace</param>
		/// <returns>Array containing the converted values of the first elements of each tuples</returns>
		public static T[] UnpackFirst<T>(this FdbSubspace subspace, Slice[] keys)
		{
			if (keys == null) throw new ArgumentNullException("keys");

			var values = new T[keys.Length];

			if (keys.Length > 0)
			{
				for (int i = 0; i < keys.Length; i++)
				{
					values[i] = FdbTuple.UnpackFirst<T>(subspace.ExtractAndCheck(keys[i]));
				}
			}

			return values;
		}

		/// <summary>Unpack an array of key into tuples, and return an array with only the last elements of each tuple</summary>
		/// <typeparam name="T">Expected type of the last element of all the keys</typeparam>
		/// <param name="keys">Array of packed keys that should all fit inside this subspace</param>
		/// <returns>Array containing the converted values of the last elements of each tuples</returns>
		public static T[] UnpackLast<T>(this FdbSubspace subspace, Slice[] keys)
		{
			if (keys == null) throw new ArgumentNullException("keys");

			var values = new T[keys.Length];

			if (keys.Length > 0)
			{
				for (int i = 0; i < keys.Length; i++)
				{
					values[i] = FdbTuple.UnpackLast<T>(subspace.ExtractAndCheck(keys[i]));
				}
			}

			return values;
		}

		/// <summary>Unpack an array of key into singleton tuples, and return an array with value of each tuple</summary>
		/// <typeparam name="T">Expected type of the only element of all the keys</typeparam>
		/// <param name="keys">Array of packed keys that should all fit inside this subspace</param>
		/// <returns>Array containing the converted values of the only elements of each tuples. Throws an exception if one key contains more than one element</returns>
		public static T[] UnpackSingle<T>(this FdbSubspace subspace, Slice[] keys)
		{
			if (keys == null) throw new ArgumentNullException("keys");

			var values = new T[keys.Length];

			if (keys.Length > 0)
			{
				for (int i = 0; i < keys.Length; i++)
				{
					values[i] = FdbTuple.UnpackSingle<T>(subspace.ExtractAndCheck(keys[i]));
				}
			}

			return values;
		}

		#endregion

		#region ToRange...

		public static FdbKeyRange ToRange<TKey>(this FdbSubspace subspace, TKey key)
			where TKey : IFdbKey
		{
			if (key == null) throw new ArgumentNullException("key");
			return subspace.ToRange(key.ToFoundationDbKey());
		}

		/// <summary>Gets a key range representing all keys in the Subspace strictly starting with the specified Tuple.</summary>
		public static FdbKeyRange ToRange(this FdbSubspace subspace, IFdbTuple tuple)
		{
			if (tuple == null) throw new ArgumentNullException("tuple");
			return subspace.ToRange(tuple.ToSlice());
		}

		public static FdbKeySelectorPair ToSelectorPair(this FdbSubspace subspace)
		{
			return FdbKeySelectorPair.Create(subspace.ToRange());
		}

		#endregion
	}
}
