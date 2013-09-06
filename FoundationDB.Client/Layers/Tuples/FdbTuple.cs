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
	using FoundationDB.Client.Converters;
	using FoundationDB.Client.Utils;
	using System;
	using System.Collections;
	using System.Collections.Generic;
	using System.Globalization;
	using System.Text;

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

			object IReadOnlyList<object>.this[int index]
			{
				get { throw new IndexOutOfRangeException(); }
			}

			public IFdbTuple this[int? from, int? to]
			{
				//REVIEW: should we throw if from/to are not null, 0 or -1 ?
				get { return this; }
			}

			public R Get<R>(int index)
			{
				throw new IndexOutOfRangeException();
			}

			public R Last<R>()
			{
				throw new InvalidOperationException("Tuple is empty");
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

			public bool Equals(IFdbTuple value)
			{
				return value != null && value.Count == 0;
			}

			public override bool Equals(object obj)
			{
				return Equals(obj as IFdbTuple);
			}

			bool System.Collections.IStructuralEquatable.Equals(object other, System.Collections.IEqualityComparer comparer)
			{
				var tuple = other as IFdbTuple;
				return tuple != null && tuple.Count == 0;
			}

			int System.Collections.IStructuralEquatable.GetHashCode(System.Collections.IEqualityComparer comparer)
			{
				return 0;
			}

		}

		#region Creation

		public static IFdbTuple CreateBoxed(object item)
		{
			return new FdbTuple<object>(item);
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

		/// <summary>Create a new N-tuple, from N items</summary>
		/// <param name="items">Array of items to wrap in a tuple</param>
		/// <remarks>If the original array is mutated, the tuple will replect the changes!</remarks>
		public static IFdbTuple Create(params object[] items)
		{
			if (items == null) throw new ArgumentNullException("items");

			if (items.Length == 0) return FdbTuple.Empty;

			// review: should be create a copy ?
			// can mutate if passed a pre-allocated array: { var foo = new objec[123]; Create(foo); foo[42] = "bad"; }
			return new FdbListTuple(items, 0, items.Length);
		}

		/// <summary>Create a new N-tuple, from a section of an array of items</summary>
		/// <remarks>If the original array is mutated, the tuple will replect the changes!</remarks>
		public static IFdbTuple FromArray(object[] items, int offset, int count)
		{
			if (items == null) throw new ArgumentNullException("items");
			if (offset < 0) throw new ArgumentOutOfRangeException("offset", "Offset cannot be less than zero");
			if (count < 0) throw new ArgumentOutOfRangeException("count", "Count cannot be less than zero");
			if (offset + count > items.Length) throw new ArgumentOutOfRangeException("count", "Source array is too small");

			if (count == 0) return FdbTuple.Empty;

			// review: should be create a copy ?
			// can mutate if passed a pre-allocated array: { var foo = new objec[123]; Create(foo); foo[42] = "bad"; }
			return new FdbListTuple(items, offset, count);
		}

		/// <summary>Create a new N-tuple from a sequence of items</summary>
		public static IFdbTuple FromEnumerable(IEnumerable<object> items)
		{
			if (items == null) throw new ArgumentNullException("items");

			// may already be a tuple (because it implements IE<obj>)
			var tuple = items as IFdbTuple;
			if (tuple == null)
			{
				tuple = new FdbListTuple(items);
			}
			return tuple;
		}

		#endregion

		#region Packing...

		public static Slice PackBoxed(object item)
		{
			var writer = new FdbBufferWriter();
			FdbTuplePackers.SerializeObjectTo(writer, item);
			return writer.ToSlice();
		}

		/// <summary>Pack a 1-tuple directly into a slice</summary>
		public static Slice Pack<T1>(T1 item1)
		{
			var writer = new FdbBufferWriter();
			FdbTuplePacker<T1>.SerializeTo(writer, item1);
			return writer.ToSlice();
		}

		/// <summary>Pack a 2-tuple directly into a slice</summary>
		public static Slice Pack<T1, T2>(T1 item1, T2 item2)
		{
			var writer = new FdbBufferWriter();
			FdbTuplePacker<T1>.SerializeTo(writer, item1);
			FdbTuplePacker<T2>.SerializeTo(writer, item2);
			return writer.ToSlice();
		}

		/// <summary>Pack a 3-tuple directly into a slice</summary>
		public static Slice Pack<T1, T2, T3>(T1 item1, T2 item2, T3 item3)
		{
			var writer = new FdbBufferWriter();
			FdbTuplePacker<T1>.SerializeTo(writer, item1);
			FdbTuplePacker<T2>.SerializeTo(writer, item2);
			FdbTuplePacker<T3>.SerializeTo(writer, item3);
			return writer.ToSlice();
		}

		/// <summary>Pack a 4-tuple directly into a slice</summary>
		public static Slice Pack<T1, T2, T3, T4>(T1 item1, T2 item2, T3 item3, T4 item4)
		{
			var writer = new FdbBufferWriter();
			FdbTuplePacker<T1>.SerializeTo(writer, item1);
			FdbTuplePacker<T2>.SerializeTo(writer, item2);
			FdbTuplePacker<T3>.SerializeTo(writer, item3);
			FdbTuplePacker<T4>.SerializeTo(writer, item4);
			return writer.ToSlice();
		}

		/// <summary>Pack a sequence of N-tuples, all sharing the same buffer</summary>
		/// <param name="tuples">Sequence of N-tuples to pack</param>
		/// <returns>Array containing the buffer segment of each packed tuple</returns>
		/// <example>BatchPack([ ("Foo", 1), ("Foo", 2) ]) => [ "\x02Foo\x00\x15\x01", "\x02Foo\x00\x15\x02" ] </example>
		public static Slice[] BatchPack(IEnumerable<IFdbTuple> tuples)
		{
			if (tuples == null) throw new ArgumentNullException("tuples");

			// use optimized version for arrays
			var array = tuples as IFdbTuple[];
			if (array != null) return BatchPack(array);

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

		/// <summary>Pack a sequence of N-tuples, all sharing the same buffer</summary>
		/// <param name="tuples">Sequence of N-tuples to pack</param>
		/// <returns>Array containing the buffer segment of each packed tuple</returns>
		/// <example>BatchPack([ ("Foo", 1), ("Foo", 2) ]) => [ "\x02Foo\x00\x15\x01", "\x02Foo\x00\x15\x02" ] </example>
		public static Slice[] BatchPack(params IFdbTuple[] tuples)
		{
			if (tuples == null) throw new ArgumentNullException("tuples");

			// pre-allocate by supposing that each tuple will take at least 16 bytes
			var writer = new FdbBufferWriter(tuples.Length * 16);
			var next = new List<int>(tuples.Length);

			//TODO: use multiple buffers if item count is huge ?

			foreach (var tuple in tuples)
			{
				tuple.PackTo(writer);
				next.Add(writer.Position);
			}

			return FdbKey.SplitIntoSegments(writer.Buffer, 0, next);
		}

		/// <summary>Pack a sequence of keys with a same prefix, all sharing the same buffer</summary>
		/// <param name="prefix">Prefix shared by all keys</param>
		/// <param name="keys">Sequence of keys to pack</param>
		/// <returns>Array of slices (for all keys) that share the same underlying buffer</returns>
		public static Slice[] BatchPackBoxed(IFdbTuple prefix, IEnumerable<object> keys)
		{
			if (prefix == null) throw new ArgumentNullException("prefix");

			return FdbKey.Merge<object>(prefix.ToSlice(), keys);
		}

		/// <summary>Pack a sequence of keys with a same prefix, all sharing the same buffer</summary>
		/// <typeparam name="T">Type of the keys</typeparam>
		/// <param name="prefix">Prefix shared by all keys</param>
		/// <param name="keys">Sequence of keys to pack</param>
		/// <returns>Array of slices (for all keys) that share the same underlying buffer</returns>
		public static Slice[] BatchPackBoxed(IFdbTuple prefix, params object[] keys)
		{
			if (prefix == null) throw new ArgumentNullException("prefix");

			return FdbKey.Merge<object>(prefix.ToSlice(), keys);
		}

		/// <summary>Pack a sequence of keys with a same prefix, all sharing the same buffer</summary>
		/// <typeparam name="T">Type of the keys</typeparam>
		/// <param name="prefix">Prefix shared by all keys</param>
		/// <param name="keys">Sequence of keys to pack</param>
		/// <returns>Array of slices (for all keys) that share the same underlying buffer</returns>
		public static Slice[] BatchPack<T>(IFdbTuple prefix, IEnumerable<T> keys)
		{
			if (prefix == null) throw new ArgumentNullException("prefix");

			return FdbKey.Merge<T>(prefix.ToSlice(), keys);
		}

		/// <summary>Pack a sequence of keys with a same prefix, all sharing the same buffer</summary>
		/// <typeparam name="T">Type of the keys</typeparam>
		/// <param name="prefix">Prefix shared by all keys</param>
		/// <param name="keys">Sequence of keys to pack</param>
		/// <returns>Array of slices (for all keys) that share the same underlying buffer</returns>
		public static Slice[] BatchPack<T>(IFdbTuple prefix, params T[] keys)
		{
			if (prefix == null) throw new ArgumentNullException("prefix");

			return FdbKey.Merge<T>(prefix.ToSlice(), keys);
		}

		#endregion

		#region Unpacking...

		/// <summary>Unpack a tuple from a serialied key blob</summary>
		/// <param name="packedKey">Binary key containing a previously packed tuple</param>
		/// <returns>Unpacked tuple</returns>
		public static IFdbTuple Unpack(Slice packedKey)
		{
			if (!packedKey.HasValue) return null;
			if (packedKey.IsEmpty) return FdbTuple.Empty;

			return FdbTuplePackers.Unpack(packedKey);
		}

		/// <summary>Unpack a key that should be contained inside a prefix tuple</summary>
		/// <param name="packedKey">Packed key</param>
		/// <param name="prefix">Expected prefix of the key</param>
		/// <returns>Unpacked tuple (minus the prefix) or an exception if the key is outside the prefix</returns>
		/// <exception cref="System.ArgumentNullException">If prefix is null</exception>
		/// <exception cref="System.ArgumentOutOfRangeException">If the unpacked key is outside the specified prefix</exception>
		public static IFdbTuple UnpackWithoutPrefix(Slice packedKey, Slice prefix)
		{
			// ensure that the key starts with the prefix
			if (!packedKey.StartsWith(prefix)) throw new ArgumentOutOfRangeException("packedKey", "The specifed packed tuple does not start with the expected prefix");

			// unpack the key, minus the prefix
			return FdbTuplePackers.Unpack(packedKey.Substring(prefix.Count));
		}

		/// <summary>Unpack only the last value of a packed tuple</summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="packedKey"></param>
		/// <returns></returns>
		public static T UnpackLast<T>(Slice packedKey)
		{
			if (packedKey.IsNullOrEmpty) throw new InvalidOperationException("Tuple is empty");

			//TODO: optimzed version ?
			var slice = FdbTuplePackers.UnpackLast(packedKey);
			if (!slice.HasValue) throw new InvalidOperationException("Failed to unpack tuple");

			object value = FdbTuplePackers.DeserializeBoxed(slice);
			return FdbConverters.ConvertBoxed<T>(value);
		}

		public static T UnpackLastWithoutPrefix<T>(Slice packedKey, Slice prefix)
		{
			// ensure that the key starts with the prefix
			if (!packedKey.StartsWith(prefix)) throw new ArgumentOutOfRangeException("packedKey", "The specifed packed tuple does not start with the expected prefix");
			if (packedKey.Count == prefix.Count) throw new InvalidOperationException("Cannot unpack the last element of an empty tuple");

			// unpack the key, minus the prefix
			return UnpackLast<T>(packedKey.Substring(prefix.Count));
		}

		#endregion

		#region Concat...
		
		//note: they are equivalent to the Pack<...>() methods, they only take a binary prefix

		public static Slice Concat(Slice prefix, IFdbTuple tuple)
		{
			if (tuple == null || tuple.Count == 0) return prefix;

			var writer = new FdbBufferWriter();
			writer.WriteBytes(prefix);
			tuple.PackTo(writer);
			return writer.ToSlice();
		}

		public static Slice ConcatBoxed(Slice prefix, object value)
		{
			var writer = new FdbBufferWriter();
			writer.WriteBytes(prefix);
			FdbTuplePackers.SerializeObjectTo(writer, value);
			return writer.ToSlice();
		}

		public static Slice Concat<T>(Slice prefix, T value)
		{
			var writer = new FdbBufferWriter();
			writer.WriteBytes(prefix);
			FdbTuplePacker<T>.Serializer(writer, value);
			return writer.ToSlice();
		}

		public static Slice Concat<T1, T2>(Slice prefix, T1 value1, T2 value2)
		{
			var writer = new FdbBufferWriter();
			writer.WriteBytes(prefix);
			FdbTuplePacker<T1>.Serializer(writer, value1);
			FdbTuplePacker<T2>.Serializer(writer, value2);
			return writer.ToSlice();
		}

		public static Slice Concat<T1, T2, T3>(Slice prefix, T1 value1, T2 value2, T3 value3)
		{
			var writer = new FdbBufferWriter();
			writer.WriteBytes(prefix);
			FdbTuplePacker<T1>.Serializer(writer, value1);
			FdbTuplePacker<T2>.Serializer(writer, value2);
			FdbTuplePacker<T3>.Serializer(writer, value3);
			return writer.ToSlice();
		}

		public static Slice Concat<T1, T2, T3, T4>(Slice prefix, T1 value1, T2 value2, T3 value3, T4 value4)
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

		#region Internal Helpers...

		/// <summary>Create a range that selects all the descendant keys starting with <paramref name="prefix"/>: ('prefix\x00' &lt;= k &lt; 'prefix\xFF')</summary>
		/// <param name="prefix">Key prefix (that will be excluded from the range)</param>
		/// <returns>Range including all keys with the specified prefix.</returns>
		/// <remarks>This is mostly used when the keys correspond to packed tuples</remarks>
		public static FdbKeyRange Descendants(Slice prefix)
		{
			if (!prefix.HasValue) throw new ArgumentNullException("prefix");

			if (prefix.Count == 0)
			{ // "" => [ \0, \xFF )
				return FdbKeyRange.All;
			}

			// prefix => [ prefix."\0", prefix."\xFF" )
			return new FdbKeyRange(
				prefix + FdbKey.MinValue,
				prefix + FdbKey.MaxValue
			);
		}

		/// <summary>Create a range that selects all the descendant keys starting with <paramref name="prefix"/>: ('prefix\x00' &lt;= k &lt; 'prefix\xFF')</summary>
		/// <param name="prefix">Key prefix (that will be excluded from the range)</param>
		/// <returns>Range including all keys with the specified prefix.</returns>
		/// <remarks>This is mostly used when the keys correspond to packed tuples</remarks>
		public static FdbKeyRange DescendantsAndSelf(Slice prefix)
		{
			if (!prefix.HasValue) throw new ArgumentNullException("prefix");

			if (prefix.Count == 0)
			{ // "" => [ \0, \xFF )
				return FdbKeyRange.All;
			}

			// prefix => [ prefix."\0", prefix."\xFF" )
			return new FdbKeyRange(
				prefix,
				prefix + FdbKey.MaxValue
			);
		}

		/// <summary>Converts any object into a displayble string, for logging/debugging purpose</summary>
		/// <param name="item">Object to stringify</param>
		/// <returns>String representation of the object</returns>
		/// <example>
		/// Stringify(null) => "nil"
		/// Stringify("hello") => "\"hello\""
		/// Stringify(123) => "123"
		/// Stringify(123.4) => "123.4"
		/// Stringify(true) => "true"
		/// Stringify(Slice) => hexa decimal string ("01 23 45 67 89 AB CD EF")
		/// </example>
		internal static string Stringify(object item)
		{
			if (item == null) return "nil";

			var s = item as string;
			if (s != null) return "\"" + s + "\"";

			if (item is char) return "\"" + (char)item + "\"";

			if (item is Slice) return ((Slice)item).ToAsciiOrHexaString();
			if (item is byte[]) return Slice.Create(item as byte[]).ToAsciiOrHexaString();

			if (item is FdbTupleAlias) return "{" + item.ToString() + "}";

			var f = item as IFormattable;
			if (f != null) return f.ToString(null, CultureInfo.InvariantCulture);

			// This will probably not give a meaningful result ... :(
			return item.ToString();
		}

		/// <summary>Convert a list of object into a displaying string, for loggin/debugging purpose</summary>
		/// <param name="items">Array containing items to stringfy</param>
		/// <param name="offset">Start offset of the items to convert</param>
		/// <param name="count">Number of items to convert</param>
		/// <returns>String representation of the tuple in the form "(item1, item2, ... itemN,)"</returns>
		/// <example>ToString(FdbTuple.Create("hello", 123, true, "world")) => "(\"hello\", 123, true, \"world\",)</example>
		internal static string ToString(object[] items, int offset, int count)
		{
			if (items == null) return String.Empty;
			if (count == 0) return "()";

			var sb = new StringBuilder();
			sb.Append('(').Append(Stringify(items[offset++]));
			while (--count > 0)
			{
				sb.Append(", ").Append(Stringify(items[offset++]));
			}
			return sb.Append(",)").ToString();
		}

		/// <summary>Convert a sequence of object into a displaying string, for loggin/debugging purpose</summary>
		/// <param name="items">Sequence of items to stringfy</param>
		/// <returns>String representation of the tuple in the form "(item1, item2, ... itemN,)"</returns>
		/// <example>ToString(FdbTuple.Create("hello", 123, true, "world")) => "(\"hello\", 123, true, \"world\",)</example>
		internal static string ToString(IEnumerable<object> items)
		{
			if (items == null) return String.Empty;
			using (var enumerator = items.GetEnumerator())
			{
				if (!enumerator.MoveNext()) return "()";

				var sb = new StringBuilder();
				sb.Append('(').Append(Stringify(enumerator.Current));
				while (enumerator.MoveNext())
				{
					sb.Append(", ").Append(Stringify(enumerator.Current));
				}

				return sb.Append(",)").ToString();
			}
		}

		/// <summary>Default (non-optimized) implementation of IFdbTuple.this[long?, long?]</summary>
		/// <param name="tuple">Tuple to slice</param>
		/// <param name="from">Start offset of the section (included)</param>
		/// <param name="to">End offset of the section (included)</param>
		/// <returns>New tuple only containing items inside this section</returns>
		internal static IFdbTuple Splice(IFdbTuple tuple, int? from, int? to)
		{
			Contract.Requires(tuple != null);
			int count = tuple.Count;
			if (count == 0) return FdbTuple.Empty;

			int start = MapIndex(from ?? 0, count);
			int end = MapIndex(to ?? -1, count);

			int len = end - start + 1;

			if (len <= 0) return FdbTuple.Empty;
			if (start == 0 && len == count) return tuple;
			switch(len)
			{
				case 1: return new FdbListTuple(new object[] { tuple[start] }, 0, 1);
				case 2: return new FdbListTuple(new object[] { tuple[start], tuple[start + 1] }, 0, 2);
				default:
				{
					var items = new object[len];
					//note: can be slow for tuples using linked-lists, but hopefully they will have their own Slice implementation...
					int p = 0, q = start, n = len;
					while (n-- > 0)
					{
						items[p++] = tuple[q++];
					}
					return new FdbListTuple(items, 0, len);
				}
			}
		}

		/// <summary>Default (non-optimized) implementation for IFdbTuple.StartsWith()</summary>
		/// <param name="a">Larger tuple</param>
		/// <param name="b">Smaller tuple</param>
		/// <returns>True if <paramref name="a"/> starts with (or is equal to) <paramref name="b"/></returns>
		internal static bool StartsWith(IFdbTuple a, IFdbTuple b)
		{
			if (object.ReferenceEquals(a, b)) return true;
			int an = a.Count;
			int bn = b.Count;

			if (bn > an) return false;
			if (bn == 0) return true; // note: 'an' can only be 0 because of previous test

			for (int i = 0; i < bn; i++)
			{
				if (a[i] != b[i]) return false;
			}
			return true;
		}

		/// <summary>Helper to copy the content of a tuple at a specific position in an array</summary>
		/// <returns>Updated offset just after the last element of the copied tuple</returns>
		internal static int CopyTo(IFdbTuple tuple, object[] array, int offset)
		{
			Contract.Requires(tuple != null);
			Contract.Requires(array != null);
			Contract.Requires(offset >= 0);

			foreach (var item in tuple)
			{
				array[offset++] = item;
			}
			return offset;
		}

		/// <summary>Maps a relative index into an absolute index</summary>
		/// <param name="index">Relative index in the tuple (from the end if negative)</param>
		/// <param name="count">Size of the tuple</param>
		/// <returns>Absolute index from the start of the tuple, or exception if outside of the tuple</returns>
		/// <exception cref="System.IndexOutOfRangeException">If the absolute index is outside of the tuple (&lt;0 or &gt;=<paramref name="count"/>)</exception>
		internal static int MapIndex(int index, int count, bool dontCheckBounds = false)
		{
			int offset = index;
			if (offset < 0) offset += count;
			if (offset < 0 || offset >= count) FailIndexOutOfRange(index, count);
			return offset;
		}

		/// <summary>Maps a relative index into an absolute index</summary>
		/// <param name="index">Relative index in the tuple (from the end if negative)</param>
		/// <param name="count">Size of the tuple</param>
		/// <returns>Absolute index from the start of the tuple, or exception if outside of the tuple</returns>
		/// <exception cref="System.IndexOutOfRangeException">If the absolute index is outside of the tuple (&lt;0 or &gt;=<paramref name="count"/>)</exception>
		internal static int MapIndexBounded(int index, int count)
		{
			if (index < 0) index += count;
			return Math.Max(Math.Min(index, count), 0);
		}

		private static void FailIndexOutOfRange(int index, int count)
		{
			throw new IndexOutOfRangeException(String.Format("Index {0} is outside of the tuple's range (0..{1})", index, count - 1));
		}

		internal static int CombineHashCodes(int h1, int h2)
		{
			return ((h1 << 5) + h1) ^ h2;
		}

		internal static int CombineHashCodes(int h1, int h2, int h3)
		{
			int h = ((h1 << 5) + h1) ^ h2;
			return ((h << 5) + h) ^ h3;
		}

		internal static int CombineHashCodes(int h1, int h2, int h3, int h4)
		{
			return CombineHashCodes(CombineHashCodes(h1, h2), CombineHashCodes(h3, h4));
		}

		internal static bool Equals(IFdbTuple left, object other, IEqualityComparer comparer)
		{
			return object.ReferenceEquals(left, null) ? other == null : FdbTuple.Equals(left, other as IFdbTuple, comparer);
		}

		internal static bool Equals(IFdbTuple x, IFdbTuple y, IEqualityComparer comparer)
		{
			if (object.ReferenceEquals(x, y)) return true;
			if (object.ReferenceEquals(x, null) || object.ReferenceEquals(y, null)) return false;

			return x.Count == y.Count && DeepEquals(x, y, comparer);
		}

		internal static bool DeepEquals(IFdbTuple x, IFdbTuple y, IEqualityComparer comparer)
		{
			using (var xs = x.GetEnumerator())
			using (var ys = y.GetEnumerator())
			{
				while (xs.MoveNext())
				{
					if (!ys.MoveNext()) return false;

					return comparer.Equals(xs.Current, ys.Current);
				}

				return !ys.MoveNext();
			}
		}

		internal static int StructuralGetHashCode(IFdbTuple tuple, IEqualityComparer comparer)
		{
			if (object.ReferenceEquals(tuple, null))
			{
				return comparer.GetHashCode(null);
			}

			int h = 0;
			foreach(var item in tuple)
			{
				h = CombineHashCodes(h, comparer.GetHashCode(item));
			}
			return h;
		}

		internal static int StructuralCompare(IFdbTuple x, IFdbTuple y, IComparer comparer)
		{
			Contract.Requires(comparer != null);

			if (object.ReferenceEquals(x, y)) return 0;
			if (object.ReferenceEquals(x, null)) return -1;
			if (object.ReferenceEquals(y, null)) return 1;

			using (var xs = x.GetEnumerator())
			using (var ys = y.GetEnumerator())
			{
				while (xs.MoveNext())
				{
					if (!ys.MoveNext()) return 1;

					int cmp = comparer.Compare(xs.Current, ys.Current);
					if (cmp != 0) return cmp;

				}
				return ys.MoveNext() ? -1 : 0;
			}
		}

		#endregion

	}

}
