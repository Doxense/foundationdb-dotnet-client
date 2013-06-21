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

using FoundationDB.Client;
using FoundationDB.Client.Utils;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace FoundationDB.Layers.Tuples
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

			public IFdbTuple this[int? from, int? to]
			{
				//REVIEW: should we throw if from/to are not null, 0 or -1 ?
				get { return this; }
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

			public bool Equals(IFdbTuple value)
			{
				return value != null && value.Count == 0;
			}

			public override bool Equals(object obj)
			{
				return Equals(obj as IFdbTuple);
			}

		}

		#region Creation

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
		public static IFdbTuple Create(object[] items, int offset, int count)
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
		public static IFdbTuple Create(IEnumerable<object> items)
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
			var next = new List<int>();
			var writer = new FdbBufferWriter();

			//TODO: pre-allocated buffer ?
			//TODO: use multiple buffers if item count is huge ?

			foreach(var tuple in tuples)
			{
				tuple.PackTo(writer);
				next.Add(writer.Position);
			}

			return FdbKey.SplitIntoSegments(writer.Buffer, 0, next);
		}

		/// <summary>Pack a sequence of keys with a same prefix, all sharing the same buffer</summary>
		/// <typeparam name="T">Type of the keys</typeparam>
		/// <param name="prefix">Prefix shared by all keys</param>
		/// <param name="keys">Sequence of keys to pack</param>
		/// <returns>Array of slices (for all keys) that share the same underlying buffer</returns>
		public static Slice[] BatchPack<T>(IFdbTuple prefix, IEnumerable<T> keys)
		{
			var next = new List<int>();
			var writer = new FdbBufferWriter();

			var slice = prefix.ToSlice();
			var packer = FdbTuplePackers.GetSerializer<T>();

			//TODO: pre-allocated buffer ?
			//TODO: use multiple buffers if item count is huge ?

			foreach (var key in keys)
			{
				writer.WriteBytes(slice);
				packer(writer, key);
				next.Add(writer.Position);
			}

			return FdbKey.SplitIntoSegments(writer.Buffer, 0, next);
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
			if (prefix == null) throw new ArgumentNullException("prefix");

			// ensure that the key starts with the prefix
			if (!packedKey.StartsWith(prefix)) throw new ArgumentOutOfRangeException("packedKey", "The specifed packed tuple does not start with the expected prefix");

			// unpack the key, minus the prefix
			return FdbTuplePackers.Unpack(packedKey.Substring(prefix.Count));
		}

		#endregion

		#region Internal Helpers...

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

			var f = item as IFormattable;
			if (f != null) return f.ToString(null, CultureInfo.InvariantCulture);

			var b = item as byte[];
			if (b != null) return new Slice(b, 0, b.Length).ToHexaString(' ');

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

		internal static int CombineHashCode(int h1, int h2)
		{
			// h(0) = hash_function(x[0])
			// h(n) = ROTL(h(n-1), 13) ^ hash_function(x[n])

			return ((h1 << 13) | (h1 >> (32 - 13))) ^ h2;
		}

		#endregion

	}

}
