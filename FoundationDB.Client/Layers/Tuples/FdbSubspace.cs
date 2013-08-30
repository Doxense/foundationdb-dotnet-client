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

namespace FoundationDB.Layers.Tuples
{
	using FoundationDB.Client;
	using FoundationDB.Client.Utils;
	using System;

	/// <summary>Adds a prefix on every keys, to group them inside a common subspace</summary>
	public class FdbSubspace
	{
		/// <summary>Empty subspace, that does not add any prefix to the keys</summary>
		public static readonly FdbSubspace Empty = new FdbSubspace(FdbTuple.Empty);

		/// <summary>Return the packed binary prefix of this subspace</summary>
		public Slice Key { get; private set; }

		#region Constructors...

		public FdbSubspace(Slice key)
		{
			this.Key = key.Memoize();
		}

		/// <summary>Create a new subspace that wraps a Tuple</summary>
		/// <param name="tuple"></param>
		public FdbSubspace(IFdbTuple tuple)
		{
			this.Key = tuple.ToSlice().Memoize();
		}

		internal FdbSubspace Copy()
		{
			return new FdbSubspace(this.Key);
		}

		internal static Slice Concat(Slice prefix, Slice key)
		{
			return prefix + key;
		}

		internal static Slice Concat(Slice prefix, IFdbTuple tuple)
		{
			if (tuple == null || tuple.Count == 0) return prefix;

			var writer = new FdbBufferWriter();
			writer.WriteBytes(prefix);
			if (tuple != null)
			{
				tuple.PackTo(writer);
			}
			return writer.ToSlice();
		}

		internal static Slice Concat<T>(Slice prefix, T value)
		{
			var writer = new FdbBufferWriter();
			writer.WriteBytes(prefix);
			FdbTuplePacker<T>.Serializer(writer, value);
			return writer.ToSlice();			
		}

		internal static Slice Concat<T1, T2>(Slice prefix, T1 value1, T2 value2)
		{
			var writer = new FdbBufferWriter();
			writer.WriteBytes(prefix);
			FdbTuplePacker<T1>.Serializer(writer, value1);
			FdbTuplePacker<T2>.Serializer(writer, value2);
			return writer.ToSlice();
		}

		internal static Slice Concat<T1, T2, T3>(Slice prefix, T1 value1, T2 value2, T3 value3)
		{
			var writer = new FdbBufferWriter();
			writer.WriteBytes(prefix);
			FdbTuplePacker<T1>.Serializer(writer, value1);
			FdbTuplePacker<T2>.Serializer(writer, value2);
			FdbTuplePacker<T3>.Serializer(writer, value3);
			return writer.ToSlice();
		}

		internal static Slice Concat<T1, T2, T3, T4>(Slice prefix, T1 value1, T2 value2, T3 value3, T4 value4)
		{
			var writer = new FdbBufferWriter();
			writer.WriteBytes(prefix);
			FdbTuplePacker<T1>.Serializer(writer, value1);
			FdbTuplePacker<T2>.Serializer(writer, value2);
			FdbTuplePacker<T3>.Serializer(writer, value3);
			FdbTuplePacker<T4>.Serializer(writer, value4);
			return writer.ToSlice();
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
		public FdbSubspace Partition<T>(T value)
		{
			return new FdbSubspace(FdbSubspace.Concat<T>(this.Key, value));
		}

		/// <summary>Partition this subspace into a child subspace</summary>
		/// <typeparam name="T1">Type of the primary subspace key</typeparam>
		/// <typeparam name="T2">Type of the secondary subspace key</typeparam>
		/// <param name="value1">Value of the primary subspace key</param>
		/// <param name="value1">Value of the secondary subspace key</param>
		/// <returns>New subspace that is logically contained by the current subspace</returns>
		/// <remarks>Subspace([Foo, ]).Partition(Bar, Baz) is equivalent to Subspace([Foo, Bar, Baz])</remarks>
		/// <example>
		/// new FdbSubspace(["Users", ]).Partition("Contacts", "Friends") == new FdbSubspace(["Users", "Contacts", "Friends", ])
		/// </example>
		public FdbSubspace Partition<T1, T2>(T1 value1, T2 value2)
		{
			return new FdbSubspace(FdbSubspace.Concat<T1, T2>(this.Key, value1, value2));
		}

		/// <summary>Partition this subspace into a child subspace</summary>
		/// <typeparam name="T1">Type of the primary subspace key</typeparam>
		/// <typeparam name="T2">Type of the secondary subspace key</typeparam>
		/// <typeparam name="T2">Type of the tertiary subspace key</typeparam>
		/// <param name="value1">Value of the primary subspace key</param>
		/// <param name="value1">Value of the secondary subspace key</param>
		/// <param name="value1">Value of the tertiary subspace key</param>
		/// <returns>New subspace that is logically contained by the current subspace</returns>
		/// <example>
		/// new FdbSubspace(["Users", ]).Partition("John Smith", "Contacts", "Friends") == new FdbSubspace(["Users", "John Smith", "Contacts", "Friends", ])
		/// </example>
		public FdbSubspace Partition<T1, T2, T3>(T1 value1, T2 value2, T3 value3)
		{
			return new FdbSubspace(FdbSubspace.Concat(this.Key, new FdbTuple<T1, T2, T3>(value1, value2, value3)));
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
			return new FdbSubspace(FdbSubspace.Concat(this.Key, tuple));
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

		/// <summary>Returns true if <paramref name="key"/> is contained withing this subspace's tuple (or is equal to tuple itself)</summary>
		/// <param name="key"></param>
		/// <returns></returns>
		public bool Contains(Slice key)
		{
			return key.HasValue && key.StartsWith(this.Key);
		}

		#endregion

		#region Pack...

		public Slice Pack(IFdbTuple tuple)
		{
			return FdbSubspace.Concat(this.Key, tuple);
		}

		public Slice Pack(ITupleFormattable item)
		{
			if (item == null) throw new ArgumentNullException("item");
			var tuple = item.ToTuple();
			if (tuple == null) throw new InvalidOperationException("The item returned an empty tuple");
			return FdbSubspace.Concat(this.Key, tuple);
		}

		/// <summary>Create a new key by appending a value to the current tuple</summary>
		/// <typeparam name="T">Type of the value</typeparam>
		/// <param name="key">Value that will be appended at the end of the key</param>
		/// <returns>Key the correspond to the concatenation of the current tuple and <paramref name="key"/></returns>
		/// <example>tuple.Pack(x) is equivalent to tuple.Append(x).ToSlice()</example>
		public Slice Pack<T>(T key)
		{
			return FdbSubspace.Concat<T>(this.Key, key);
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
			return FdbSubspace.Concat<T1, T2>(this.Key, key1, key2);
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
			return FdbSubspace.Concat<T1, T2, T3>(this.Key, key1, key2, key3);
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
			return FdbSubspace.Concat<T1, T2, T3, T4>(this.Key, key1, key2, key3, key4);
		}

		#endregion

		#region Append...

		/// <summary>Return an empty tuple that is attached to this subspace</summary>
		/// <returns>Empty tuple that can be extended, and whose packed representation will always be prefixed by the subspace key</returns>
		public FdbSubspaceTuple ToTuple()
		{
			return new FdbSubspaceTuple(this, FdbTuple.Empty);
		}

		/// <summary>Attach a tuple to an existing subspace.</summary>
		/// <param name="value">Tuple whose items will be appended at the end of the current subspace</param>
		/// <returns>Tuple that wraps the items of <param name="tuple"/> and whose packed representation will always be prefixed by the subspace key.</returns>
		public FdbSubspaceTuple Create(IFdbTuple tuple)
		{
			return new FdbSubspaceTuple(this, tuple);
		}

		/// <summary>Convert a formattable item into a tuple that is attached to this subspace.</summary>
		/// <param name="formattable">Item that can be converted into a tuple</param>
		/// <returns>Tuple that is the logical representation of the item, and whose packed representation will always be prefixed by the subspace key.</returns>
		/// <remarks>This is the equivalent of calling 'subspace.Create(formattable.ToTuple())'</remarks>
		public FdbSubspaceTuple Create(ITupleFormattable formattable)
		{
			if (formattable == null) throw new ArgumentNullException("formattable");
			var tuple = formattable.ToTuple();
			if (tuple == null) throw new InvalidOperationException("Formattable item cannot return an empty tuple");
			return new FdbSubspaceTuple(this, tuple);
		}

		/// <summary>Create a new 1-tuple that is attached to this subspace</summary>
		/// <typeparam name="T">Type of the value to append</typeparam>
		/// <param name="value">Value that will be appended</param>
		/// <returns>Tuple of size 1 that contains <paramref name="value"/>, and whose packed representation will always be prefixed by the subspace key.</returns>
		/// <remarks>This is the equivalent of calling 'subspace.Create(FdbTuple.Create&lt;T&gt;(value))'</remarks>
		public FdbSubspaceTuple Create<T>(T value)
		{
			return new FdbSubspaceTuple(this, FdbTuple.Create<T>(value));
		}

		/// <summary>Create a new 2-tuple that is attached to this subspace</summary>
		/// <typeparam name="T1">Type of the first value to append</typeparam>
		/// <typeparam name="T2">Type of the second value to append</typeparam>
		/// <param name="value1">First value that will be appended</param>
		/// <param name="value2">Second value that will be appended</param>
		/// <returns>Tuple of size 2 that contains <paramref name="value1"/> and <paramref name="value2"/>, and whose packed representation will always be prefixed by the subspace key.</returns>
		/// <remarks>This is the equivalent of calling 'subspace.Create(FdbTuple.Create&lt;T1, T2&gt;(value1, value2))'</remarks>
		public FdbSubspaceTuple Create<T1, T2>(T1 value1, T2 value2)
		{
			return new FdbSubspaceTuple(this, FdbTuple.Create<T1, T2>(value1, value2));
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
		public FdbSubspaceTuple Create<T1, T2, T3>(T1 value1, T2 value2, T3 value3)
		{
			return new FdbSubspaceTuple(this, FdbTuple.Create<T1, T2, T3>(value1, value2, value3));
		}

		/// <summary>Create a new N-tuple that is attached to this subspace</summary>
		/// <param name="items">Array of items of the new tuple</param>
		/// <returns>Tuple of size <paramref name="items"/>.Length, and whose packed representation will always be prefixed by the subspace key.</returns>
		/// <remarks>This is the equivalent of calling 'subspace.Create(FdbTuple.Create(items))'</remarks>
		public FdbSubspaceTuple Create(params object[] items)
		{
			return new FdbSubspaceTuple(this, FdbTuple.Create(items));
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
			if (!key.HasValue) return null;

			return new FdbSubspaceTuple(this, FdbTuple.UnpackWithoutPrefix(key, this.Key));
		}

		/// <summary>Unpack a key into a tuple, and return only the last element</summary>
		/// <typeparam name="T">Expected type of the last element</typeparam>
		/// <param name="key">Packed version of a key that should fit inside this subspace</param>
		/// <returns>Converted value of the last element of the tuple</returns>
		/// <example>new Subspace([FE]).UnpackLast&lt;int&gt;([FE 02 'H' 'e' 'l' 'l' 'o' 00 15 1]) => (int) 1</example>
		public T UnpackLast<T>(Slice key)
		{
			return FdbTuple.UnpackLastWithoutPrefix<T>(key, this.Key);
		}

		/// <summary>Unpack an array of keys in tuples, with the subspace prefix removed</summary>
		/// <param name="keys">Packed version of keys inside this subspace</param>
		/// <returns>Unpacked tuples that are relative to the current subspace</returns>
		public IFdbTuple[] Unpack(Slice[] keys)
		{
			var tuples = new IFdbTuple[keys.Length];

			if (keys.Length > 0)
			{
				var prefix = this.Key;
				for (int i = 0; i < keys.Length; i++)
				{
					if (keys[i].HasValue)
					{
						tuples[i] = new FdbSubspaceTuple(this, FdbTuple.UnpackWithoutPrefix(keys[i], prefix));
					}
				}
			}

			return tuples;
		}

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
				var prefix = this.Key;
				for (int i = 0; i < keys.Length; i++)
				{
					values[i] = FdbTuple.UnpackLastWithoutPrefix<T>(keys[i], prefix);
				}
			}

			return values;
		}

		#endregion

		#region Slice Manipulation...

		/// <summary>Concatenate the specified key to the subspace key</summary>
		/// <remarks>This is the equivalent of calling 'subspace.Key + key'</remarks>
		public Slice Concat(Slice key)
		{
			var writer = OpenBuffer(key.Count);
			writer.WriteBytes(key);
			return writer.ToSlice();
		}

		/// <summary>Concatenate a batch of keys to the subspace key</summary>
		/// <param name="keys">Array of key suffix</param>
		/// <returns>Array of keys each prefixed by the subspace key</returns>
		public Slice[] Concat(Slice[] keys)
		{
			return FdbKey.Merge(this.Key, keys);
		}

		/// <summary>Remove the subspace prefix from a binary key, and only return the tail, or Slice.Nil if the key does not fit inside the namespace</summary>
		/// <param name="key">Complete key that contains the current subspace prefix, and a binary suffix</param>
		/// <returns>Binary suffix of the key (or Slice.Empty is the key is exactly equal to the subspace prefix). If the key is outside of the subspace, returns Slice.Nil</returns>
		/// <remarks>This is the inverse operation of <see cref="FdbSubspace.Concat(Slice)"/></remarks>
		public Slice Extract(Slice key)
		{
			if (!key.HasValue) return Slice.Nil;

			if (!key.StartsWith(this.Key))
			{
				// or should we throw ?
				return Slice.Nil;
			}

			return key.Substring(this.Key.Count);
		}

		/// <summary>Remove the subspace prefix from a batch of binary keys, and only return the tail, or Slice.Nil if a key does not fit inside the namespace</summary>
		/// <param name="keys">Array of complete keys that contains the current subspace prefix, and a binary suffix</param>
		/// <returns>Array of only the binary suffix of the keys, Slice.Empty for a key that is exactly equal to the subspace prefix, or Slice.Nil for a key that is outside of the subspace</returns>
		/// <remarks>This is the inverse operation of <see cref="FdbSubspace.Concat(Slice[])"/></remarks>
		public Slice[] Extract(Slice[] keys)
		{
			if (keys == null) throw new ArgumentNullException("keys");

			var results = new Slice[keys.Length];
			for (int i = 0; i < keys.Length; i++)
			{
				results[i] = Extract(keys[i]);
			}

			return results;
		}

		#endregion

		public FdbKeyRange ToRange()
		{
			return FdbKeyRange.FromPrefix(this.Key);
		}

		public FdbKeyRange ToRange(IFdbTuple tuple)
		{
			return FdbKeyRange.FromPrefix(FdbSubspace.Concat(this.Key, tuple));
		}

		public FdbKeySelectorPair ToSelectorPair()
		{
			return FdbKeySelectorPair.Create(ToRange());
		}

		internal FdbBufferWriter OpenBuffer(int extraBytes = 0)
		{
			var writer = new FdbBufferWriter();
			if (extraBytes > 0) writer.EnsureBytes(extraBytes + this.Key.Count);
			writer.WriteBytes(this.Key);
			return writer;
		}

		public override string ToString()
		{
			return this.Key.ToString();
		}

		public override int GetHashCode()
		{
			return this.Key.GetHashCode();
		}

		public override bool Equals(object obj)
		{
			if (object.ReferenceEquals(obj, this)) return true;
			if (obj is FdbSubspace) return this.Key.Equals((obj as FdbSubspace).Key);
			if (obj is Slice) return this.Key.Equals((Slice)obj);
			return false;
		}
	
	}

}
