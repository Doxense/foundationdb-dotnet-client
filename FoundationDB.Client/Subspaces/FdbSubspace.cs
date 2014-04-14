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
	using System.Linq;
	using System.Collections.Generic;

	public interface IFdbSubspace : IFdbKey
	{

		/// <summary>Returns the raw prefix of this subspace</summary>
		Slice Key { get; }

		/// <summary>Tests whether the specified <paramref name="key"/> starts with this Subspace's prefix, indicating that the Subspace logically contains <paramref name="key"/>.</summary>
		/// <param name="key">The key to be tested</param>
		/// <remarks>The key Slice.Nil is not contained by any Subspace, so subspace.Contains(Slice.Nil) will always return false</remarks>
		bool Contains(Slice key);

		/// <summary>Appends a key to the subspace key</summary>
		/// <remarks>This is the equivalent of calling 'subspace.Key + key'</remarks>
		Slice Concat(Slice key);

		/// <summary>Removes the subspace prefix from a binary key, and only return the tail, or Slice.Nil if the key does not fit inside the namespace</summary>
		/// <param name="key">Complete key that contains the current subspace prefix, and a binary suffix</param>
		/// <returns>Binary suffix of the key (or Slice.Empty is the key is exactly equal to the subspace prefix). If the key is outside of the subspace, returns Slice.Nil</returns>
		Slice Extract(Slice key);

		Slice ExtractAndCheck(Slice key);

		/// <summary>Gets a key range respresenting all keys strictly in the Subspace.</summary>
		FdbKeyRange ToRange();

		//TODO: add more !
	}

	/// <summary>Adds a prefix on every keys, to group them inside a common subspace</summary>
	public class FdbSubspace : IFdbSubspace
	{
		/// <summary>Empty subspace, that does not add any prefix to the keys</summary>
		public static readonly FdbSubspace Empty = new FdbSubspace(Slice.Empty);

		/// <summary>Binary prefix of this subspace</summary>
		private readonly Slice m_rawPrefix;

		/// <summary>Returns the raw prefix of this subspace</summary>
		/// <remarks>Will throw if the prefix is not publicly visible, as is the case for Directory Partitions</remarks>
		public Slice Key
		{
			get { return GetKeyPrefix(); }
		}

		/// <summary>Returns the key of this directory subspace</summary>
		/// <remarks>This should only be used by methods that can use the key internally, even if it is not supposed to be exposed (as is the case for directory partitions)</remarks>
		protected Slice InternalKey
		{
			get { return m_rawPrefix; }
		}

		#region Constructors...

		/// <summary>Wraps an existing subspace</summary>
		protected FdbSubspace(FdbSubspace copy)
		{
			if (copy == null) throw new ArgumentNullException("copy");
			if (copy.m_rawPrefix.IsNull) throw new ArgumentException("The subspace key cannot be null. Use Slice.Empty if you want a subspace with no prefix.", "copy");
			m_rawPrefix = copy.m_rawPrefix;
		}

		/// <summary>Create a new subspace from a binary prefix</summary>
		/// <param name="rawPrefix">Prefix of the new subspace</param>
		/// <param name="copy">If true, take a copy of the prefix</param>
		protected FdbSubspace(Slice rawPrefix, bool copy)
		{
			if (rawPrefix.IsNull) throw new ArgumentException("The subspace key cannot be null. Use Slice.Empty if you want a subspace with no prefix.", "rawPrefix");
			if (copy) rawPrefix = rawPrefix.Memoize();
			m_rawPrefix = rawPrefix.Memoize();
		}

		/// <summary>Create a new subspace from a binary prefix</summary>
		/// <param name="rawPrefix">Prefix of the new subspace</param>
		public FdbSubspace(Slice rawPrefix)
			: this(rawPrefix, true)
		{ }

		/// <summary>Create a new subspace from a Tuple prefix</summary>
		/// <param name="tuple">Tuple packed to produce the prefix</param>
		public FdbSubspace(IFdbTuple tuple)
		{
			if (tuple == null) throw new ArgumentNullException("tuple");
			m_rawPrefix = tuple.ToSlice().Memoize();
		}

		/// <summary>Create a new subspace of the current subspace</summary>
		/// <param name="suffix">Binary suffix that will be appended to the current prefix</param>
		/// <returns>New subspace whose prefix is the concatenation of the parent prefix, and <paramref name="suffix"/></returns>
		public FdbSubspace this[Slice suffix]
		{
			// note: there is a difference with the Pyton layer because here we don't use Tuple encoding, but just concat the slices together.
			// the .NET equivalent of the subspace.__getitem__(self, name) method would be subspace.Partition<Slice>(name) or subspace[FdbTuple.Create<Slice>(name)] !
			get
			{
				if (suffix.IsNull) throw new ArgumentException("The subspace key cannot be null. Use Slice.Empty if you want a subspace with no prefix.", "suffix");
				return FdbSubspace.Create(GetKeyPrefix() + suffix);
			}
		}

		/// <summary>Create a new subspace of the current subspace</summary>
		/// <param name="tuple">Binary suffix that will be appended to the current prefix</param>
		/// <returns>New subspace whose prefix is the concatenation of the parent prefix, and <paramref name="suffix"/></returns>
		public FdbSubspace this[IFdbTuple tuple]
		{
			get { return Partition(tuple); }
		}

		#endregion

		#region Static Prefix Helpers...

		public static FdbSubspace Create(Slice slice)
		{
			return new FdbSubspace(slice);
		}

		public static FdbSubspace Create(IFdbTuple tuple)
		{
			return new FdbSubspace(tuple);
		}

		internal FdbSubspace Copy()
		{
			return new FdbSubspace(this.InternalKey.Memoize());
		}

		#endregion

		#region Partition...

		/// <summary>Returns the key to use when creating direct keys that are inside this subspace</summary>
		/// <returns>Prefix that must be added to all keys created by this subspace</returns>
		/// <remarks>Subspaces that disallow the creation of keys should override this method and throw an exception</remarks>
		protected virtual Slice GetKeyPrefix()
		{
			return m_rawPrefix;
		}

		/// <summary>Partition this subspace into a child subspace</summary>
		/// <typeparam name="T">Type of the child subspace key</typeparam>
		/// <param name="value">Value of the child subspace</param>
		/// <returns>New subspace that is logically contained by the current subspace</returns>
		/// <remarks>Subspace([Foo, ]).Partition(Bar) is equivalent to Subspace([Foo, Bar, ])</remarks>
		/// <example>
		/// new FdbSubspace(["Users", ]).Partition("Contacts") == new FdbSubspace(["Users", "Contacts", ])
		/// </example>
		public FdbSubspace Partition<T>(T value)
		{
			//TODO: this should go into a FdbTupleSubspace, because it collides with FdbEncoderSubspace<T> !
			return new FdbSubspace(FdbTuple.Concat<T>(GetKeyPrefix(), value));
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
		public FdbSubspace Partition<T1, T2>(T1 value1, T2 value2)
		{
			//TODO: this should go into a FdbTupleSubspace, because it collides with FdbEncoderSubspace<T1, T2> !
			return new FdbSubspace(FdbTuple.Concat<T1, T2>(GetKeyPrefix(), value1, value2));
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
		public FdbSubspace Partition<T1, T2, T3>(T1 value1, T2 value2, T3 value3)
		{
			//TODO: this should go into a FdbTupleSubspace, because it collides with FdbEncoderSubspace<T1, T2, T3> !
			return new FdbSubspace(FdbTuple.Concat(GetKeyPrefix(), new FdbTuple<T1, T2, T3>(value1, value2, value3)));
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
		public FdbSubspace Partition<T1, T2, T3, T4>(T1 value1, T2 value2, T3 value3, T4 value4)
		{
			//TODO: this should go into a FdbTupleSubspace, because it collides with FdbEncoderSubspace<T1, T2, T3, T4> !
			return new FdbSubspace(FdbTuple.Concat(GetKeyPrefix(), new FdbTuple<T1, T2, T3, T4>(value1, value2, value3, value4)));
		}

		/// <summary>Parition this subspace by appending a tuple</summary>
		/// <param name="tuple">Tuple that will be used for this partition</param>
		/// <returns>New subspace that is creating by combining the namespace prefix and <paramref name="tuple"/></returns>
		/// <remarks>Subspace([Foo, ]).Partition([Bar, Baz, ]) is equivalent to Subspace([Foo, Bar, Baz,])</remarks>
		/// <example>
		/// new FdbSubspace(["Users", ]).Partition(["Contacts", "Friends", ]) => new FdbSubspace(["Users", "Contacts", "Friends", ])
		/// </example>
		public FdbSubspace Partition(IFdbTuple tuple)
		{
			if (tuple == null) throw new ArgumentNullException("tuple");
			if (tuple.Count == 0) return this;
			return new FdbSubspace(FdbTuple.Concat(GetKeyPrefix(), tuple));
		}

		/// <summary>Partition this subspace into a child subspace</summary>
		/// <param name="formattable">a ITupleFormattable, <paramref name="formattable"/>.ToTuple() will be used for this partition</param>
		/// <returns>New subspace that is creating by combining the namespace prefix and <paramref name="formattable"/></returns>
		/// <remarks>Subspace([Foo, ]).Partition(Bar) is equivalent to Subspace([Foo, Bar, ])</remarks>
		/// <example>
		/// new FdbSubspace(["Users", ]).Partition("Contacts") == new FdbSubspace(["Users", "Contacts", ])
		/// </example>
		public FdbSubspace Partition(ITupleFormattable formattable)
		{
			if (formattable == null) throw new ArgumentNullException("formattable");
			var tuple = formattable.ToTuple();
			if (tuple == null) throw new InvalidOperationException("Formattable item returned an empty tuple");
			return Partition(tuple);
		}

		/// <summary>Tests whether the specified <paramref name="key"/> starts with this Subspace's prefix, indicating that the Subspace logically contains <paramref name="key"/>.</summary>
		/// <param name="key">The key to be tested</param>
		/// <remarks>The key Slice.Nil is not contained by any Subspace, so subspace.Contains(Slice.Nil) will always return false</remarks>
		public bool Contains(Slice key)
		{
			return key.HasValue && key.StartsWith(this.InternalKey);
		}

		/// <summary>Tests whether the specified <paramref name="key"/> starts with this Subspace's prefix, indicating that the Subspace logically contains <paramref name="key"/>.</summary>
		/// <param name="key">The key to be tested</param>
		/// <exception cref="System.ArgumentNullException">If <paramref name="key"/> is null</exception>
		public bool Contains<TKey>(TKey key)
			where TKey : IFdbKey
		{
			if (key == null) throw new ArgumentNullException("key");
			return this.Contains(key.ToFoundationDbKey());
		}

		#endregion

		#region Pack...

		public Slice Pack(IFdbTuple tuple)
		{
			return FdbTuple.Concat(GetKeyPrefix(), tuple);
		}

		public Slice Pack(ITupleFormattable item)
		{
			if (item == null) throw new ArgumentNullException("item");
			var prefix = GetKeyPrefix();
			var tuple = item.ToTuple();
			if (tuple == null) throw new InvalidOperationException("The item returned an empty tuple");
			return FdbTuple.Concat(prefix, tuple);
		}

		/// <summary>Create a new key by appending a value to the current tuple</summary>
		/// <param name="key">Value that will be appended at the end of the key</param>
		/// <returns>Key the correspond to the concatenation of the current tuple and <paramref name="key"/></returns>
		/// <example>tuple.PackBoxed(x) is the non-generic equivalent of tuple.Pack&lt;object&gt;(tuple)</example>
		public Slice PackBoxed(object item)
		{
			return FdbTuple.ConcatBoxed(GetKeyPrefix(), item);
		}

		/// <summary>Create a new key by appending a value to the current tuple</summary>
		/// <typeparam name="T">Type of the value</typeparam>
		/// <param name="key">Value that will be appended at the end of the key</param>
		/// <returns>Key the correspond to the concatenation of the current tuple and <paramref name="key"/></returns>
		/// <example>tuple.Pack(x) is equivalent to tuple.Append(x).ToSlice()</example>
		public Slice Pack<T>(T key)
		{
			return FdbTuple.Concat<T>(GetKeyPrefix(), key);
		}

		/// <summary>Create a new key by appending two values to the current tuple</summary>
		/// <typeparam name="T1">Type of the next to last value</typeparam>
		/// <typeparam name="T2">Type of the last value</typeparam>
		/// <param name="key1">Value that will be in the next to last position</param>
		/// <param name="key2">Value that will be in the last position</param>
		/// <returns>Key the correspond to the concatenation of the current tuple, <paramref name="key1"/> and <paramref name="key2"/></returns>
		/// <example>(...,).Pack(x, y) is equivalent to (...,).Append(x).Append(y).ToSlice()</example>
		public Slice Pack<T1, T2>(T1 key1, T2 key2)
		{
			return FdbTuple.Concat<T1, T2>(GetKeyPrefix(), key1, key2);
		}

		/// <summary>Create a new key by appending three values to the current tuple</summary>
		/// <typeparam name="T1">Type of the first value</typeparam>
		/// <typeparam name="T2">Type of the second value</typeparam>
		/// <typeparam name="T3">Type of the thrid value</typeparam>
		/// <param name="key1">Value that will be appended first</param>
		/// <param name="key2">Value that will be appended second</param>
		/// <param name="key3">Value that will be appended third</param>
		/// <returns>Key the correspond to the concatenation of the current tuple, <paramref name="key1"/>, <paramref name="key2"/> and <paramref name="key3"/></returns>
		/// <example>tuple.Pack(x, y, z) is equivalent to tuple.Append(x).Append(y).Append(z).ToSlice()</example>
		public Slice Pack<T1, T2, T3>(T1 key1, T2 key2, T3 key3)
		{
			return FdbTuple.Concat<T1, T2, T3>(GetKeyPrefix(), key1, key2, key3);
		}

		/// <summary>Create a new key by appending three values to the current tuple</summary>
		/// <typeparam name="T1">Type of the first value</typeparam>
		/// <typeparam name="T2">Type of the second value</typeparam>
		/// <typeparam name="T3">Type of the third value</typeparam>
		/// <typeparam name="T4">Type of the fourth value</typeparam>
		/// <param name="key1">Value that will be appended first</param>
		/// <param name="key2">Value that will be appended second</param>
		/// <param name="key3">Value that will be appended third</param>
		/// <param name="key4">Value that will be appended fourth</param>
		/// <returns>Key the correspond to the concatenation of the current tuple, <paramref name="key1"/>, <paramref name="key2"/>, <paramref name="key3"/> and <paramref name="key4"/></returns>
		/// <example>tuple.Pack(w, x, y, z) is equivalent to tuple.Append(w).Append(x).Append(y).Append(z).ToSlice()</example>
		public Slice Pack<T1, T2, T3, T4>(T1 key1, T2 key2, T3 key3, T4 key4)
		{
			return FdbTuple.Concat<T1, T2, T3, T4>(GetKeyPrefix(), key1, key2, key3, key4);
		}

		/// <summary>Merge a sequence of keys with the subspace's prefix, all sharing the same buffer</summary>
		/// <typeparam name="T">Type of the keys</typeparam>
		/// <param name="keys">Sequence of keys to pack</param>
		/// <returns>Array of slices (for all keys) that share the same underlying buffer</returns>
		public Slice[] Merge(IEnumerable<Slice> keys)
		{
			return FdbKey.Merge(GetKeyPrefix(), keys);
		}

		/// <summary>Merge an array of keys with the subspace's prefix, all sharing the same buffer</summary>
		/// <typeparam name="T">Type of the keys</typeparam>
		/// <param name="keys">Array of keys to pack</param>
		/// <returns>Array of slices (for all keys) that share the same underlying buffer</returns>
		public Slice[] Merge(params Slice[] keys)
		{
			return FdbKey.Merge(GetKeyPrefix(), keys);
		}

		/// <summary>Merge a sequence of keys with the subspace's prefix, all sharing the same buffer</summary>
		/// <typeparam name="T">Type of the keys</typeparam>
		/// <param name="keys">Sequence of keys to pack</param>
		/// <returns>Array of slices (for all keys) that share the same underlying buffer</returns>
		public Slice[] PackRange<T>(IEnumerable<T> keys)
		{
			return FdbTuple.PackRange<T>(GetKeyPrefix(), keys);
		}

		/// <summary>Merge a sequence of keys with the subspace's prefix, all sharing the same buffer</summary>
		/// <typeparam name="T">Type of the keys</typeparam>
		/// <param name="keys">Sequence of keys to pack</param>
		/// <returns>Array of slices (for all keys) that share the same underlying buffer</returns>
		public Slice[] PackRange<T>(T[] keys)
		{
			return FdbTuple.PackRange<T>(GetKeyPrefix(), keys);
		}

		/// <summary>Pack a sequence of keys with the subspace's prefix, all sharing the same buffer</summary>
		/// <param name="keys">Sequence of keys to pack</param>
		/// <returns>Array of slices (for all keys) that share the same underlying buffer</returns>
		public Slice[] PackBoxedRange(IEnumerable<object> keys)
		{
			return FdbTuple.PackBoxedRange(GetKeyPrefix(), keys);
		}

		/// <summary>Pack a sequence of keys with the subspace's prefix, all sharing the same buffer</summary>
		/// <param name="keys">Sequence of keys to pack</param>
		/// <returns>Array of slices (for all keys) that share the same underlying buffer</returns>
		public Slice[] PackBoxedRange(object[] keys)
		{
			//note: cannot use "params object[]" because it may conflict with PackRange(IEnumerable<object>)
			return FdbTuple.PackBoxedRange(GetKeyPrefix(), keys);
		}

		#endregion

		#region Append...

		//REVIEW: we should move these methods as extension methods on the IFdbKey interface !

		/// <summary>Return an empty tuple that is attached to this subspace</summary>
		/// <returns>Empty tuple that can be extended, and whose packed representation will always be prefixed by the subspace key</returns>
		public IFdbTuple ToTuple()
		{
			return new FdbPrefixedTuple(GetKeyPrefix(), FdbTuple.Empty);
		}

		/// <summary>Attach a tuple to an existing subspace.</summary>
		/// <param name="value">Tuple whose items will be appended at the end of the current subspace</param>
		/// <returns>Tuple that wraps the items of <param name="tuple"/> and whose packed representation will always be prefixed by the subspace key.</returns>
		public IFdbTuple Append(IFdbTuple tuple)
		{
			return new FdbPrefixedTuple(GetKeyPrefix(), tuple);
		}

		public IFdbTuple AppendBoxed(object value)
		{
			return new FdbPrefixedTuple(GetKeyPrefix(), FdbTuple.CreateBoxed(value));
		}

		/// <summary>Convert a formattable item into a tuple that is attached to this subspace.</summary>
		/// <param name="formattable">Item that can be converted into a tuple</param>
		/// <returns>Tuple that is the logical representation of the item, and whose packed representation will always be prefixed by the subspace key.</returns>
		/// <remarks>This is the equivalent of calling 'subspace.Create(formattable.ToTuple())'</remarks>
		public IFdbTuple Append(ITupleFormattable formattable)
		{
			if (formattable == null) throw new ArgumentNullException("formattable");
			var tuple = formattable.ToTuple();
			if (tuple == null) throw new InvalidOperationException("Formattable item cannot return an empty tuple");
			return new FdbPrefixedTuple(GetKeyPrefix(), tuple);
		}

		/// <summary>Create a new 1-tuple that is attached to this subspace</summary>
		/// <typeparam name="T">Type of the value to append</typeparam>
		/// <param name="value">Value that will be appended</param>
		/// <returns>Tuple of size 1 that contains <paramref name="value"/>, and whose packed representation will always be prefixed by the subspace key.</returns>
		/// <remarks>This is the equivalent of calling 'subspace.Create(FdbTuple.Create&lt;T&gt;(value))'</remarks>
		public IFdbTuple Append<T>(T value)
		{
			return new FdbPrefixedTuple(GetKeyPrefix(), FdbTuple.Create<T>(value));
		}

		/// <summary>Create a new 2-tuple that is attached to this subspace</summary>
		/// <typeparam name="T1">Type of the first value to append</typeparam>
		/// <typeparam name="T2">Type of the second value to append</typeparam>
		/// <param name="value1">First value that will be appended</param>
		/// <param name="value2">Second value that will be appended</param>
		/// <returns>Tuple of size 2 that contains <paramref name="value1"/> and <paramref name="value2"/>, and whose packed representation will always be prefixed by the subspace key.</returns>
		/// <remarks>This is the equivalent of calling 'subspace.Create(FdbTuple.Create&lt;T1, T2&gt;(value1, value2))'</remarks>
		public IFdbTuple Append<T1, T2>(T1 value1, T2 value2)
		{
			return new FdbPrefixedTuple(GetKeyPrefix(), FdbTuple.Create<T1, T2>(value1, value2));
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
		public IFdbTuple Append<T1, T2, T3>(T1 value1, T2 value2, T3 value3)
		{
			return new FdbPrefixedTuple(GetKeyPrefix(), FdbTuple.Create<T1, T2, T3>(value1, value2, value3));
		}

		/// <summary>Create a new 4-tuple that is attached to this subspace</summary>
		/// <typeparam name="T1">Type of the first value to append</typeparam>
		/// <typeparam name="T2">Type of the second value to append</typeparam>
		/// <typeparam name="T3">Type of the third value to append</typeparam>
		/// <typeparam name="T3">Type of the fourth value to append</typeparam>
		/// <param name="value1">First value that will be appended</param>
		/// <param name="value2">Second value that will be appended</param>
		/// <param name="value3">Third value that will be appended</param>
		/// <param name="value3">Fourth value that will be appended</param>
		/// <returns>Tuple of size 4 that contains <paramref name="value1"/>, <paramref name="value2"/>, <paramref name="value3"/> and <paramref name="value4"/>, and whose packed representation will always be prefixed by the subspace key.</returns>
		/// <remarks>This is the equivalent of calling 'subspace.Create(FdbTuple.Create&lt;T1, T2, T3, T4&gt;(value1, value2, value3, value4))'</remarks>
		public IFdbTuple Append<T1, T2, T3, T4>(T1 value1, T2 value2, T3 value3, T4 value4)
		{
			return new FdbPrefixedTuple(GetKeyPrefix(), FdbTuple.Create<T1, T2, T3, T4>(value1, value2, value3, value4));
		}

		/// <summary>Create a new N-tuple that is attached to this subspace</summary>
		/// <param name="items">Array of items of the new tuple</param>
		/// <returns>Tuple of size <paramref name="items"/>.Length, and whose packed representation will always be prefixed by the subspace key.</returns>
		/// <remarks>This is the equivalent of calling 'subspace.Create(FdbTuple.Create(items))'</remarks>
		public IFdbTuple Append(params object[] items)
		{ //REVIEW: Can be ambiguous with Append<object[]>() so should be renamed to AppendBoxed(..) ?
			return new FdbPrefixedTuple(GetKeyPrefix(), FdbTuple.Create(items));
		}

		#endregion

		#region Unpack...

		/// <summary>Unpack a key into a tuple, with the subspace prefix removed</summary>
		/// <param name="key">Packed version of a key that should fit inside this subspace.</param>
		/// <returns>Unpacked tuple that are relative to the current subspace, or null if the key is equal to Slice.Nil</returns>
		/// <example>new Subspace([FE]).Unpack([FE 02 'H' 'e' 'l' 'l' 'o' 00 15 1]) => ("hello", 1,)</example>
		/// <exception cref="System.ArgumentOutOfRangeException">If the unpacked tuple is not contained in this subspace</exception>
		public IFdbTuple Unpack(Slice key)
		{
			// We special case 'Slice.Nil' because it is returned by GetAsync(..) when the key does not exist
			// This is to simplifiy decoding logic where the caller could do "var foo = FdbTuple.Unpack(await tr.GetAsync(...))" and then only have to test "if (foo != null)"
			if (key.IsNull) return null;

			var prefix = GetKeyPrefix();
			return new FdbPrefixedTuple(prefix, FdbTuple.UnpackWithoutPrefix(key, prefix));
		}

		//TODO: add missing UnpackFirst<T>(Slice)

		/// <summary>Unpack a key into a tuple, and return only the last element</summary>
		/// <typeparam name="T">Expected type of the last element</typeparam>
		/// <param name="key">Packed version of a key that should fit inside this subspace</param>
		/// <returns>Converted value of the last element of the tuple</returns>
		/// <example>new Subspace([FE]).UnpackLast&lt;int&gt;([FE 02 'H' 'e' 'l' 'l' 'o' 00 15 1]) => (int) 1</example>
		public T UnpackLast<T>(Slice key)
		{
			return FdbTuple.UnpackLastWithoutPrefix<T>(key, GetKeyPrefix());
		}

		/// <summary>Unpack a key into a singleton tuple, and return the single element</summary>
		/// <typeparam name="T">Expected type of the only element</typeparam>
		/// <param name="key">Packed version of a key that should fit inside this subspace</param>
		/// <returns>Converted value of the only element in the tuple. Throws an exception if the tuple is empty or contains more than one element</returns>
		/// <example>new Subspace([FE]).UnpackSingle&lt;int&gt;([FE 02 'H' 'e' 'l' 'l' 'o' 00]) => (string) "Hello"</example>
		public T UnpackSingle<T>(Slice key)
		{
			return FdbTuple.UnpackLastWithoutPrefix<T>(key, GetKeyPrefix());
		}

		/// <summary>Unpack an array of keys in tuples, with the subspace prefix removed</summary>
		/// <param name="keys">Packed version of keys inside this subspace</param>
		/// <returns>Unpacked tuples that are relative to the current subspace</returns>
		public IFdbTuple[] Unpack(Slice[] keys)
		{
			var tuples = new IFdbTuple[keys.Length];

			if (keys.Length > 0)
			{
				var prefix = GetKeyPrefix();
				for (int i = 0; i < keys.Length; i++)
				{
					if (keys[i].HasValue)
					{
						tuples[i] = new FdbPrefixedTuple(prefix, FdbTuple.UnpackWithoutPrefix(keys[i], prefix));
					}
				}
			}

			return tuples;
		}

		//TODO: add missing UnpackFirst<T>(Slice[])

		/// <summary>Unpack an array of key into tuples, and return an array with only the last elements of each tuple</summary>
		/// <typeparam name="T">Expected type of the last element of all the keys</typeparam>
		/// <param name="keys">Array of packed keys that should all fit inside this subspace</param>
		/// <returns>Array containing the converted values of the last elements of each tuples</returns>
		public T[] UnpackLast<T>(Slice[] keys)
		{
			if (keys == null) throw new ArgumentNullException("keys");

			var values = new T[keys.Length];

			if (keys.Length > 0)
			{
				var prefix = GetKeyPrefix();
				for (int i = 0; i < keys.Length; i++)
				{
					values[i] = FdbTuple.UnpackLastWithoutPrefix<T>(keys[i], prefix);
				}
			}

			return values;
		}

		/// <summary>Unpack an array of key into singleton tuples, and return an array with value of each tuple</summary>
		/// <typeparam name="T">Expected type of the only element of all the keys</typeparam>
		/// <param name="keys">Array of packed keys that should all fit inside this subspace</param>
		/// <returns>Array containing the converted values of the only elements of each tuples. Throws an exception if one key contains more than one element</returns>
		public T[] UnpackSingle<T>(Slice[] keys)
		{
			if (keys == null) throw new ArgumentNullException("keys");

			var values = new T[keys.Length];

			if (keys.Length > 0)
			{
				var prefix = GetKeyPrefix();
				for (int i = 0; i < keys.Length; i++)
				{
					values[i] = FdbTuple.UnpackSingleWithoutPrefix<T>(keys[i], prefix);
				}
			}

			return values;
		}

		#endregion

		#region Slice Manipulation...

		//REVIEW: these methods could be moved to extension methods on IFdbKey, because they only need the raw prefix ...

		/// <summary>Append a key to the subspace key</summary>
		/// <remarks>This is the equivalent of calling 'subspace.Key + key'</remarks>
		public Slice Concat(Slice key)
		{
			return Slice.Concat(GetKeyPrefix(), key);
		}

		/// <summary>Append a pair of keys to the subspace key</summary>
		/// <remarks>This is the equivalent of calling 'subspace.Key + key1 + key2'</remarks>
		public Slice Concat(Slice key1, Slice key2)
		{
			return Slice.Concat(GetKeyPrefix(), key1, key2);
		}

		/// <summary>Append a batch of keys to the subspace key</summary>
		/// <param name="keys">Array of key suffix</param>
		/// <returns>Array of keys each prefixed by the subspace key</returns>
		public Slice[] Concat(Slice[] keys)
		{ //REVIEW: should we change to (params Slice[] keys) ?
			return FdbKey.Merge(GetKeyPrefix(), keys);
		}

		/// <summary>Append a key to the subspace key</summary>
		/// <typeparam name="TKey">type of the key, must implements IFdbKey</typeparam>
		/// <param name="key"></param>
		/// <returns>Return Slice : 'subspace.Key + key'</returns>
		public Slice Concat<TKey>(TKey key)
			where TKey : IFdbKey
		{
			if (key == null) throw new ArgumentNullException("key");
			return GetKeyPrefix() + key.ToFoundationDbKey();
		}

		/// <summary>Append a sequence of keys with the subspace's prefix, all sharing the same buffer</summary>
		/// <typeparam name="TKey">type of the key, must implements IFdbKey</typeparam>
		/// <param name="key"></param>
		/// <returns>Return Slice : 'subspace.Key + key'</returns>
		public Slice[] ConcatRange<TKey>(IEnumerable<TKey> keys)
			where TKey : IFdbKey
		{
			if (keys == null) throw new ArgumentNullException("keys");
			return GetKeyPrefix().ConcatRange(keys.Select((key) => key.ToFoundationDbKey()));
		}

		/// <summary>Remove the subspace prefix from a binary key, and only return the tail, or Slice.Nil if the key does not fit inside the namespace</summary>
		/// <param name="key">Complete key that contains the current subspace prefix, and a binary suffix</param>
		/// <returns>Binary suffix of the key (or Slice.Empty is the key is exactly equal to the subspace prefix). If the key is outside of the subspace, returns Slice.Nil</returns>
		/// <remarks>This is the inverse operation of <see cref="FdbSubspace.Concat(Slice)"/></remarks>
		public Slice Extract(Slice key)
		{
			if (key.IsNull) return Slice.Nil;

			var prefix = GetKeyPrefix();
			if (!key.StartsWith(prefix))
			{
				// or should we throw ?
				return Slice.Nil;
			}

			return key.Substring(prefix.Count);
		}

		//REVIEW: add Extract<TKey>() where TKey : IFdbKey ?

		/// <summary>Remove the subspace prefix from a batch of binary keys, and only return the tail, or Slice.Nil if a key does not fit inside the namespace</summary>
		/// <param name="keys">Array of complete keys that contains the current subspace prefix, and a binary suffix</param>
		/// <returns>Array of only the binary suffix of the keys, Slice.Empty for a key that is exactly equal to the subspace prefix, or Slice.Nil for a key that is outside of the subspace</returns>
		/// <remarks>This is the inverse operation of <see cref="FdbSubspace.Concat(Slice[])"/></remarks>
		public Slice[] Extract(Slice[] keys)
		{ //REVIEW: rename to ExtractRange ?
			if (keys == null) throw new ArgumentNullException("keys");

			var results = new Slice[keys.Length];
			for (int i = 0; i < keys.Length; i++)
			{
				results[i] = Extract(keys[i]);
			}

			return results;
		}

		/// <summary>Check that a key fits inside this subspace, and return '' or '\xFF' if it is outside the bounds</summary>
		/// <param name="key">Key that needs to be checked</param>
		/// <param name="allowSystemKeys">If true, allow keys that starts with \xFF even if this subspace is not the Empty subspace or System subspace itself.</param>
		/// <returns>The <paramref name="key"/> unchanged if it is contained in the namespace, Slice.Empty if it was before the subspace, or FdbKey.MaxValue if it was after.</returns>
		public Slice BoundCheck(Slice key, bool allowSystemKeys)
		{
			//note: Since this is needed to make GetRange/GetKey work properly, this should work for all subspace, include directory partitions
			var prefix = this.InternalKey;

			// don't touch to nil and keys inside the globalspace
			if (key.IsNull || key.StartsWith(prefix)) return key;

			// let the system keys pass
			if (allowSystemKeys && key.Count > 0 && key[0] == 255) return key;

			// The key is outside the bounds, and must be corrected
			// > return empty if we are before
			// > return \xFF if we are after
			if (key < GetKeyPrefix())
				return Slice.Empty;
			else
				return FdbKey.System;
		}

		public Slice ExtractAndCheck(Slice key)
		{
			var prefix = GetKeyPrefix();

			// ensure that the key starts with the prefix
			if (!key.StartsWith(prefix)) FailKeyOutOfBound(key);

			return key.Substring(prefix.Count);
		}

		protected void FailKeyOutOfBound(Slice key)
		{
#if DEBUG
			// only in debug mode, because have the key and subspace in the exception message could leak sensitive information
			string msg = String.Format("The key {0} does not belong to subspace {1}", FdbKey.Dump(key), this.ToString());
#else
			string msg = "The specifed key does not belong to this subspace";
#endif
			throw new ArgumentException(msg, "key");
		}

		//REVIEW: add missing overrides of ExtractAndCheck that takes a Slice[], and an IFdbKey ?

		#endregion

		#region ToRange...

		/// <summary>Gets a key range respresenting all keys strictly in the Subspace.</summary>
		public FdbKeyRange ToRange()
		{
			return FdbTuple.ToRange(GetKeyPrefix());
		}

		public FdbKeyRange ToRange(Slice key)
		{
			return FdbTuple.ToRange(GetKeyPrefix() + key);
		}

		public FdbKeyRange ToRange<TKey>(TKey key)
			where TKey : IFdbKey
		{
			if (key == null) throw new ArgumentNullException("key");
			return FdbTuple.ToRange(GetKeyPrefix() + key.ToFoundationDbKey());
		}

		/// <summary>Gets a key range representing all keys in the Subspace strictly starting with the specified Tuple.</summary>
		public FdbKeyRange ToRange(IFdbTuple tuple)
		{
			return FdbTuple.ToRange(FdbTuple.Concat(GetKeyPrefix(), tuple));
		}

		public FdbKeySelectorPair ToSelectorPair()
		{
			return FdbKeySelectorPair.Create(ToRange());
		}

		#endregion

		public virtual string DumpKey(Slice key)
		{
			// note: we can't use ExtractAndCheck(...) because it may throw in derived classes
			var prefix = this.InternalKey;
			if (!key.StartsWith(prefix)) FailKeyOutOfBound(key);

			return FdbKey.Dump(key.Substring(prefix.Count));
		}

		Slice IFdbKey.ToFoundationDbKey()
		{
			return GetKeyPrefix();
		}

		public override string ToString()
		{
			return String.Format("Subspace({0})", this.InternalKey.ToString());
		}

		public override int GetHashCode()
		{
			return this.InternalKey.GetHashCode();
		}

		public override bool Equals(object obj)
		{
			if (object.ReferenceEquals(obj, this)) return true;
			if (obj is FdbSubspace) return this.InternalKey.Equals((obj as FdbSubspace).Key);
			if (obj is Slice) return this.InternalKey.Equals((Slice)obj);
			return false;
		}
	
	}

}
