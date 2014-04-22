﻿#region BSD Licence
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

namespace FoundationDB.Layers.Tuples
{
	using FoundationDB.Client;
	using FoundationDB.Client.Converters;
	using FoundationDB.Client.Utils;
	using System;
	using System.Collections;
	using System.Collections.Generic;
	using System.Globalization;
	using System.Linq;
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

			public void PackTo(ref SliceWriter writer)
			{
				//NO-OP
			}

			public Slice ToSlice()
			{
				return Slice.Empty;
			}

			Slice IFdbKey.ToFoundationDbKey()
			{
				return this.ToSlice();
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

		/// <summary>Create a new 1-tuple, holding only one item</summary>
		/// <remarks>This is the non-generic equivalent of FdbTuple.Create&lt;object&gt;()</remarks>
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

		/// <summary>Create a new 4-tuple, holding four items</summary>
		public static FdbTuple<T1, T2, T3, T4> Create<T1, T2, T3, T4>(T1 item1, T2 item2, T3 item3, T4 item4)
		{
			return new FdbTuple<T1, T2, T3, T4>(item1, item2, item3, item4);
		}

		/// <summary>Create a new N-tuple, from N items</summary>
		/// <param name="items">Items to wrap in a tuple</param>
		/// <remarks>If you already have an array of items, you should call <see cref="CreateRange(object[])"/> instead. Mutating the array, would also mutate the tuple!</remarks>
		public static IFdbTuple Create(params object[] items)
		{
			if (items == null) throw new ArgumentNullException("items");

			//note: this is a convenience method for people that wants to pass more than 3 args arguments, and not have to call CreateRange(object[]) method

			if (items.Length == 0) return FdbTuple.Empty;

			// We don't copy the array, and rely on the fact that the array was created by the compiler and that nobody will get a reference on it.
			return new FdbListTuple(items, 0, items.Length);
		}

		/// <summary>Create a new N-tuple that wraps an array of untyped items</summary>
		/// <remarks>If the original array is mutated, the tuple will reflect the changes!</remarks>
		public static IFdbTuple Wrap(object[] items)
		{
			if (items == null) throw new ArgumentNullException("items");

			if (items.Length == 0) return FdbTuple.Empty;

			// review: should be create a copy ?
			return new FdbListTuple(items, 0, items.Length);
		}

		/// <summary>Create a new N-tuple that wraps a section of an array of untyped items</summary>
		/// <remarks>If the original array is mutated, the tuple will reflect the changes!</remarks>
		public static IFdbTuple Wrap(object[] items, int offset, int count)
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

		/// <summary>Create a new N-tuple, from an array of untyped items</summary>
		public static IFdbTuple CreateRange(object[] items)
		{
			if (items == null) throw new ArgumentNullException("items");

			return CreateRange(items, 0, items.Length);
		}

		/// <summary>Create a new N-tuple, from a section of an array of untyped items</summary>
		public static IFdbTuple CreateRange(object[] items, int offset, int count)
		{
			if (items == null) throw new ArgumentNullException("items");
			if (offset < 0) throw new ArgumentOutOfRangeException("offset", "Offset cannot be less than zero");
			if (count < 0) throw new ArgumentOutOfRangeException("count", "Count cannot be less than zero");
			if (offset + count > items.Length) throw new ArgumentOutOfRangeException("count", "Source array is too small");

			if (count == 0) return FdbTuple.Empty;

			// copy the items
			var tmp = new object[count];
			Array.Copy(items, offset, tmp, 0, count);
			return new FdbListTuple(tmp, 0, count);
		}

		/// <summary>Create a new N-tuple from a sequence of items</summary>
		public static IFdbTuple CreateRange(IEnumerable<object> items)
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

		/// <summary>Create a new N-tuple, from an array of typed items</summary>
		public static IFdbTuple CreateRange<T>(T[] items)
		{
			if (items == null) throw new ArgumentNullException("items");

			return CreateRange<T>(items, 0, items.Length);
		}

		/// <summary>Create a new N-tuple, from a section of an array of typed items</summary>
		public static IFdbTuple CreateRange<T>(T[] items, int offset, int count)
		{
			if (items == null) throw new ArgumentNullException("items");
			if (offset < 0) throw new ArgumentOutOfRangeException("offset", "Offset cannot be less than zero");
			if (count < 0) throw new ArgumentOutOfRangeException("count", "Count cannot be less than zero");
			if (offset + count > items.Length) throw new ArgumentOutOfRangeException("count", "Source array is too small");

			if (count == 0) return FdbTuple.Empty;

			// copy the items
			var tmp = new object[count];
			Array.Copy(items, offset, tmp, 0, count);
			return new FdbListTuple(tmp, 0, count);
		}

		/// <summary>Create a new N-tuple from a sequence of typed items</summary>
		public static IFdbTuple CreateRange<T>(IEnumerable<T> items)
		{
			if (items == null) throw new ArgumentNullException("items");

			// may already be a tuple (because it implements IE<obj>)
			var tuple = items as IFdbTuple;
			if (tuple == null)
			{
				object[] tmp = items.Cast<object>().ToArray();
				tuple = new FdbListTuple(tmp, 0, tmp.Length);
			}
			return tuple;
		}

		#endregion

		#region Packing...

		/// <summary>Pack a 1-tuple directly into a slice</summary>
		/// <remarks>This is the non-generic equivalent of FdbTuple.Pack&lt;object&gt;()</remarks>
		public static Slice PackBoxed(object item)
		{
			var writer = SliceWriter.Empty;
			FdbTuplePackers.SerializeObjectTo(ref writer, item);
			return writer.ToSlice();
		}

		/// <summary>Pack a 1-tuple directly into a slice</summary>
		public static Slice Pack<T1>(T1 item1)
		{
			var writer = SliceWriter.Empty;
			FdbTuplePacker<T1>.SerializeTo(ref writer, item1);
			return writer.ToSlice();
		}

		/// <summary>Pack a 2-tuple directly into a slice</summary>
		public static Slice Pack<T1, T2>(T1 item1, T2 item2)
		{
			var writer = SliceWriter.Empty;
			FdbTuplePacker<T1>.SerializeTo(ref writer, item1);
			FdbTuplePacker<T2>.SerializeTo(ref writer, item2);
			return writer.ToSlice();
		}

		/// <summary>Pack a 3-tuple directly into a slice</summary>
		public static Slice Pack<T1, T2, T3>(T1 item1, T2 item2, T3 item3)
		{
			var writer = SliceWriter.Empty;
			FdbTuplePacker<T1>.SerializeTo(ref writer, item1);
			FdbTuplePacker<T2>.SerializeTo(ref writer, item2);
			FdbTuplePacker<T3>.SerializeTo(ref writer, item3);
			return writer.ToSlice();
		}

		/// <summary>Pack a 4-tuple directly into a slice</summary>
		public static Slice Pack<T1, T2, T3, T4>(T1 item1, T2 item2, T3 item3, T4 item4)
		{
			var writer = SliceWriter.Empty;
			FdbTuplePacker<T1>.SerializeTo(ref writer, item1);
			FdbTuplePacker<T2>.SerializeTo(ref writer, item2);
			FdbTuplePacker<T3>.SerializeTo(ref writer, item3);
			FdbTuplePacker<T4>.SerializeTo(ref writer, item4);
			return writer.ToSlice();
		}

		/// <summary>Merge a sequence of keys with a same prefix, all sharing the same buffer</summary>
		/// <typeparam name="T">Type of the keys</typeparam>
		/// <param name="prefix">Prefix shared by all keys</param>
		/// <param name="keys">Sequence of keys to pack</param>
		/// <returns>Array of slices (for all keys) that share the same underlying buffer</returns>
		public static Slice[] PackRange<T>(Slice prefix, IEnumerable<T> keys)
		{
			if (prefix == null) throw new ArgumentNullException("prefix");
			if (keys == null) throw new ArgumentNullException("keys");

			// use optimized version for arrays
			var array = keys as T[];
			if (array != null) return PackRange<T>(prefix, array);

			var next = new List<int>();
			var writer = SliceWriter.Empty;
			var packer = FdbTuplePacker<T>.Encoder;

			//TODO: use multiple buffers if item count is huge ?

			foreach (var key in keys)
			{
				if (prefix.IsPresent) writer.WriteBytes(prefix);
				packer(ref writer, key);
				next.Add(writer.Position);
			}

			return FdbKey.SplitIntoSegments(writer.Buffer, 0, next);
		}

		/// <summary>Merge an array of keys with a same prefix, all sharing the same buffer</summary>
		/// <typeparam name="T">Type of the keys</typeparam>
		/// <param name="prefix">Prefix shared by all keys</param>
		/// <param name="keys">Sequence of keys to pack</param>
		/// <returns>Array of slices (for all keys) that share the same underlying buffer</returns>
		public static Slice[] PackRange<T>(Slice prefix, params T[] keys)
		{
			if (prefix == null) throw new ArgumentNullException("prefix");
			if (keys == null) throw new ArgumentNullException("keys");

			// pre-allocate by guessing that each key will take at least 8 bytes. Even if 8 is too small, we should have at most one or two buffer resize
			var writer = new SliceWriter(keys.Length * (prefix.Count + 8));
			var next = new List<int>(keys.Length);
			var packer = FdbTuplePacker<T>.Encoder;

			//TODO: use multiple buffers if item count is huge ?

			foreach (var key in keys)
			{
				if (prefix.IsPresent) writer.WriteBytes(prefix);
				packer(ref writer, key);
				next.Add(writer.Position);
			}

			return FdbKey.SplitIntoSegments(writer.Buffer, 0, next);
		}

		/// <summary>Pack a sequence of N-tuples, all sharing the same buffer</summary>
		/// <param name="tuples">Sequence of N-tuples to pack</param>
		/// <returns>Array containing the buffer segment of each packed tuple</returns>
		/// <example>BatchPack([ ("Foo", 1), ("Foo", 2) ]) => [ "\x02Foo\x00\x15\x01", "\x02Foo\x00\x15\x02" ] </example>
		public static Slice[] PackRange(IEnumerable<IFdbTuple> tuples)
		{
			return PackRange(Slice.Nil, tuples);
		}

		/// <summary>Pack a sequence of N-tuples, all sharing the same buffer</summary>
		/// <param name="prefix">Commong prefix added to all the tuples</param>
		/// <param name="tuples">Sequence of N-tuples to pack</param>
		/// <returns>Array containing the buffer segment of each packed tuple</returns>
		/// <example>BatchPack("abc", [ ("Foo", 1), ("Foo", 2) ]) => [ "abc\x02Foo\x00\x15\x01", "abc\x02Foo\x00\x15\x02" ] </example>
		public static Slice[] PackRange(Slice prefix, IEnumerable<IFdbTuple> tuples)
		{
			if (tuples == null) throw new ArgumentNullException("tuples");

			// use optimized version for arrays
			var array = tuples as IFdbTuple[];
			if (array != null) return PackRange(prefix, array);

			var next = new List<int>();
			var writer = SliceWriter.Empty;

			//TODO: use multiple buffers if item count is huge ?

			foreach(var tuple in tuples)
			{
				writer.WriteBytes(prefix);
				tuple.PackTo(ref writer);
				next.Add(writer.Position);
			}

			return FdbKey.SplitIntoSegments(writer.Buffer, 0, next);
		}

		/// <summary>Pack an array of N-tuples, all sharing the same buffer</summary>
		/// <param name="tuples">Sequence of N-tuples to pack</param>
		/// <returns>Array containing the buffer segment of each packed tuple</returns>
		/// <example>BatchPack([ ("Foo", 1), ("Foo", 2) ]) => [ "\x02Foo\x00\x15\x01", "\x02Foo\x00\x15\x02" ] </example>
		public static Slice[] PackRange(IFdbTuple[] tuples)
		{
			return PackRange(Slice.Nil, tuples);
		}

		/// <summary>Pack an array of N-tuples, all sharing the same buffer</summary>
		/// <param name="prefix">Commong prefix added to all the tuples</param>
		/// <param name="tuples">Sequence of N-tuples to pack</param>
		/// <returns>Array containing the buffer segment of each packed tuple</returns>
		/// <example>BatchPack("abc", [ ("Foo", 1), ("Foo", 2) ]) => [ "abc\x02Foo\x00\x15\x01", "abc\x02Foo\x00\x15\x02" ] </example>
		public static Slice[] PackRange(Slice prefix, params IFdbTuple[] tuples)
		{
			if (tuples == null) throw new ArgumentNullException("tuples");

			// pre-allocate by supposing that each tuple will take at least 16 bytes
			var writer = new SliceWriter(tuples.Length * (16 + prefix.Count));
			var next = new List<int>(tuples.Length);

			//TODO: use multiple buffers if item count is huge ?

			foreach (var tuple in tuples)
			{
				writer.WriteBytes(prefix);
				tuple.PackTo(ref writer);
				next.Add(writer.Position);
			}

			return FdbKey.SplitIntoSegments(writer.Buffer, 0, next);
		}

		/// <summary>Pack a sequence of keys with a same prefix, all sharing the same buffer</summary>
		/// <typeparam name="T">Type of the keys</typeparam>
		/// <param name="prefix">Prefix shared by all keys</param>
		/// <param name="keys">Sequence of keys to pack</param>
		/// <returns>Array of slices (for all keys) that share the same underlying buffer</returns>
		public static Slice[] PackRange<T>(IFdbTuple prefix, IEnumerable<T> keys)
		{
			if (prefix == null) throw new ArgumentNullException("prefix");

			return PackRange<T>(prefix.ToSlice(), keys);
		}

		/// <summary>Pack a sequence of keys with a same prefix, all sharing the same buffer</summary>
		/// <typeparam name="T">Type of the keys</typeparam>
		/// <param name="prefix">Prefix shared by all keys</param>
		/// <param name="keys">Sequence of keys to pack</param>
		/// <returns>Array of slices (for all keys) that share the same underlying buffer</returns>
		public static Slice[] PackRange<T>(IFdbTuple prefix, params T[] keys)
		{
			if (prefix == null) throw new ArgumentNullException("prefix");

			return PackRange<T>(prefix.ToSlice(), keys);
		}

		/// <summary>Pack a sequence of keys with a same prefix, all sharing the same buffer</summary>
		/// <param name="prefix">Prefix shared by all keys</param>
		/// <param name="keys">Sequence of keys to pack</param>
		/// <returns>Array of slices (for all keys) that share the same underlying buffer</returns>
		public static Slice[] PackBoxedRange(Slice prefix, IEnumerable<object> keys)
		{
			if (prefix == null) throw new ArgumentNullException("prefix");

			return PackRange<object>(prefix, keys);
		}

		/// <summary>Pack a sequence of keys with a same prefix, all sharing the same buffer</summary>
		/// <param name="prefix">Prefix shared by all keys</param>
		/// <param name="keys">Sequence of keys to pack</param>
		/// <returns>Array of slices (for all keys) that share the same underlying buffer</returns>
		public static Slice[] PackBoxedRange(Slice prefix, object[] keys)
		{
			//note: we don't use "params object[] keys" because it can be ambiguous when passing an 'object[]' parameter (because an object[] is also an object)
			if (prefix == null) throw new ArgumentNullException("prefix");

			return PackRange<object>(prefix, keys);
		}

		/// <summary>Pack a sequence of keys with a same prefix, all sharing the same buffer</summary>
		/// <param name="prefix">Prefix shared by all keys</param>
		/// <param name="keys">Sequence of keys to pack</param>
		/// <returns>Array of slices (for all keys) that share the same underlying buffer</returns>
		public static Slice[] PackBoxedRange(IFdbTuple prefix, IEnumerable<object> keys)
		{
			if (prefix == null) throw new ArgumentNullException("prefix");

			return PackRange<object>(prefix.ToSlice(), keys);
		}

		/// <summary>Pack a sequence of keys with a same prefix, all sharing the same buffer</summary>
		/// <param name="prefix">Prefix shared by all keys</param>
		/// <param name="keys">Sequence of keys to pack</param>
		/// <returns>Array of slices (for all keys) that share the same underlying buffer</returns>
		public static Slice[] PackBoxedRange(IFdbTuple prefix, object[] keys)
		{
			//note: we don't use "params object[] keys" because it can be ambiguous when passing an 'object[]' parameter (because an object[] is also an object)
			if (prefix == null) throw new ArgumentNullException("prefix");

			return PackRange<object>(prefix.ToSlice(), keys);
		}

		#endregion

		#region Unpacking...

		/// <summary>Unpack a tuple from a serialied key blob</summary>
		/// <param name="packedKey">Binary key containing a previously packed tuple</param>
		/// <returns>Unpacked tuple</returns>
		public static IFdbTuple Unpack(Slice packedKey)
		{
			if (packedKey.IsNullOrEmpty) return packedKey.HasValue ? FdbTuple.Empty : null;

			return FdbTuplePackers.Unpack(packedKey);
		}

		/// <summary>Unpack a tuple from a serialized key, after removing the prefix</summary>
		/// <param name="packedKey">Packed key</param>
		/// <param name="prefix">Expected prefix of the key (that is not part of the tuple)</param>
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

		/// <summary>Unpack a tuple and only return its first element</summary>
		/// <typeparam name="T">Type of the first value in the decoded tuple</typeparam>
		/// <param name="packedKey">Slice that should be entirely parsable as a tuple</param>
		/// <returns>Decoded value of the first item in the tuple</returns>
		public static T UnpackFirst<T>(Slice packedKey)
		{
			if (packedKey.IsNullOrEmpty) throw new InvalidOperationException("Cannot unpack the first element of an empty tuple");

			var slice = FdbTuplePackers.UnpackFirst(packedKey);
			if (slice.IsNull) throw new InvalidOperationException("Failed to unpack tuple");

			//TODO: FdbTuplePackers.Deserialize<T>(slice) ?
			object value = FdbTuplePackers.DeserializeBoxed(slice);
			return FdbConverters.ConvertBoxed<T>(value);
		}

		/// <summary>Unpack a tuple and only return its first element, after removing <paramref name="prefix"/> from the start of the buffer</summary>
		/// <typeparam name="T">Type of the first value in the decoded tuple</typeparam>
		/// <param name="packedKey">Slice composed of <paramref name="prefix"/> followed by a packed tuple</param>
		/// <param name="prefix">Expected prefix of the key (that is not part of the tuple)</param>
		/// <returns>Decoded value of the first item in the tuple</returns>
		public static T UnpackFirstWithoutPrefix<T>(Slice packedKey, Slice prefix)
		{
			// ensure that the key starts with the prefix
			if (!packedKey.StartsWith(prefix))
			{
#if DEBUG
				//REVIEW: for now only in debug mode, because leaking keys in exceptions mesasges may not be a good idea?
				throw new ArgumentOutOfRangeException("packedKey", String.Format("The specifed packed tuple ({0}) does not start with the expected prefix ({1})", FdbKey.Dump(packedKey), FdbKey.Dump(prefix)));
#else
				throw new ArgumentOutOfRangeException("packedKey", "The specifed packed tuple does not start with the expected prefix");
#endif
			}

			// unpack the key, minus the prefix
			return UnpackFirst<T>(packedKey.Substring(prefix.Count));
		}

		/// <summary>Unpack a tuple and only return its last element</summary>
		/// <typeparam name="T">Type of the last value in the decoded tuple</typeparam>
		/// <param name="packedKey">Slice that should be entirely parsable as a tuple</param>
		/// <returns>Decoded value of the last item in the tuple</returns>
		public static T UnpackLast<T>(Slice packedKey)
		{
			if (packedKey.IsNullOrEmpty) throw new InvalidOperationException("Cannot unpack the last element of an empty tuple");

			var slice = FdbTuplePackers.UnpackLast(packedKey);
			if (slice.IsNull) throw new InvalidOperationException("Failed to unpack tuple");

			//TODO: FdbTuplePackers.Deserialize<T>(slice) ?
			object value = FdbTuplePackers.DeserializeBoxed(slice);
			return FdbConverters.ConvertBoxed<T>(value);
		}

		/// <summary>Unpack a tuple and only return its last element, after removing <paramref name="prefix"/> from the start of the buffer</summary>
		/// <typeparam name="T">Type of the last value in the decoded tuple</typeparam>
		/// <param name="packedKey">Slice composed of <paramref name="prefix"/> followed by a packed tuple</param>
		/// <param name="prefix">Expected prefix of the key (that is not part of the tuple)</param>
		/// <returns>Decoded value of the last item in the tuple</returns>
		public static T UnpackLastWithoutPrefix<T>(Slice packedKey, Slice prefix)
		{
			// ensure that the key starts with the prefix
			if (!packedKey.StartsWith(prefix))
			{
#if DEBUG
				//REVIEW: for now only in debug mode, because leaking keys in exceptions mesasges may not be a good idea?
				throw new ArgumentOutOfRangeException("packedKey", String.Format("The specifed packed tuple ({0}) does not start with the expected prefix ({1})", FdbKey.Dump(packedKey), FdbKey.Dump(prefix)));
#else
				throw new ArgumentOutOfRangeException("packedKey", "The specifed packed tuple does not start with the expected prefix");
#endif
			}

			// unpack the key, minus the prefix
			return UnpackLast<T>(packedKey.Substring(prefix.Count));
		}

		/// <summary>Unpack the value of a singletion tuple</summary>
		/// <typeparam name="T">Type of the single value in the decoded tuple</typeparam>
		/// <param name="packedKey">Slice that should contain the packed representation of a tuple with a single element</param>
		/// <returns>Decoded value of the only item in the tuple. Throws an exception if the tuple is empty of has more than one element.</returns>
		public static T UnpackSingle<T>(Slice packedKey)
		{
			if (packedKey.IsNullOrEmpty) throw new InvalidOperationException("Cannot unpack a single value out of an empty tuple");

			var slice = FdbTuplePackers.UnpackSingle(packedKey);
			if (slice.IsNull) throw new InvalidOperationException("Failed to unpack singleton tuple");

			//TODO: FdbTuplePackers.Deserialize<T>(slice) ?
			object value = FdbTuplePackers.DeserializeBoxed(slice);
			return FdbConverters.ConvertBoxed<T>(value);
		}

		/// <summary>Unpack the value of a singleton tuple, after removing <paramref name="prefix"/> from the start of the buffer</summary>
		/// <typeparam name="T">Type of the single value in the decoded tuple</typeparam>
		/// <param name="packedKey">Slice composed of <paramref name="prefix"/> followed by a packed singleton tuple</param>
		/// <param name="prefix">Expected prefix of the key (that is not part of the tuple)</param>
		/// <returns>Decoded value of the only item in the tuple. Throws an exception if the tuple is empty of has more than one element.</returns>
		public static T UnpackSingleWithoutPrefix<T>(Slice packedKey, Slice prefix)
		{
			// ensure that the key starts with the prefix
			if (!packedKey.StartsWith(prefix)) throw new ArgumentOutOfRangeException("packedKey", "The specifed packed tuple does not start with the expected prefix");

			// unpack the key, minus the prefix
			return UnpackSingle<T>(packedKey.Substring(prefix.Count));
		}

		public static bool UnpackNext<T>(ref SliceReader input, out T value)
		{
			if (!input.HasMore)
			{
				value = default(T);
				return false;
			}

			var slice = FdbTuplePackers.ParseNext(ref input);
			//TODO: FdbTuplePackers.Deserialize<T>(slice) ?
			value = FdbConverters.ConvertBoxed<T>(FdbTuplePackers.DeserializeBoxed(slice));
			return true;
		}

		#endregion

		#region Concat...
		
		//note: they are equivalent to the Pack<...>() methods, they only take a binary prefix

		/// <summary>Efficiently concatenate a prefix with the packed representation of a tuple</summary>
		public static Slice Concat(Slice prefix, IFdbTuple tuple)
		{
			if (tuple == null || tuple.Count == 0) return prefix;

			var writer = SliceWriter.Empty;
			writer.WriteBytes(prefix);
			tuple.PackTo(ref writer);
			return writer.ToSlice();
		}

		/// <summary>Efficiently concatenate a prefix with the packed representation of a 1-tuple</summary>
		/// <remarks>This is the non-generic equivalent of FdbTuple.Concat&lt;object&gt;()</remarks>
		public static Slice ConcatBoxed(Slice prefix, object value)
		{
			var writer = SliceWriter.Empty;
			writer.WriteBytes(prefix);
			FdbTuplePackers.SerializeObjectTo(ref writer, value);
			return writer.ToSlice();
		}

		/// <summary>Efficiently concatenate a prefix with the packed representation of a 1-tuple</summary>
		public static Slice Concat<T>(Slice prefix, T value)
		{
			var writer = SliceWriter.Empty;
			writer.WriteBytes(prefix);
			FdbTuplePacker<T>.Encoder(ref writer, value);
			return writer.ToSlice();
		}

		/// <summary>Efficiently concatenate a prefix with the packed representation of a 2-tuple</summary>
		public static Slice Concat<T1, T2>(Slice prefix, T1 value1, T2 value2)
		{
			var writer = SliceWriter.Empty;
			writer.WriteBytes(prefix);
			FdbTuplePacker<T1>.Encoder(ref writer, value1);
			FdbTuplePacker<T2>.Encoder(ref writer, value2);
			return writer.ToSlice();
		}

		/// <summary>Efficiently concatenate a prefix with the packed representation of a 3-tuple</summary>
		public static Slice Concat<T1, T2, T3>(Slice prefix, T1 value1, T2 value2, T3 value3)
		{
			var writer = SliceWriter.Empty;
			writer.WriteBytes(prefix);
			FdbTuplePacker<T1>.Encoder(ref writer, value1);
			FdbTuplePacker<T2>.Encoder(ref writer, value2);
			FdbTuplePacker<T3>.Encoder(ref writer, value3);
			return writer.ToSlice();
		}

		/// <summary>Efficiently concatenate a prefix with the packed representation of a 4-tuple</summary>
		public static Slice Concat<T1, T2, T3, T4>(Slice prefix, T1 value1, T2 value2, T3 value3, T4 value4)
		{
			var writer = SliceWriter.Empty;
			writer.WriteBytes(prefix);
			FdbTuplePacker<T1>.Encoder(ref writer, value1);
			FdbTuplePacker<T2>.Encoder(ref writer, value2);
			FdbTuplePacker<T3>.Encoder(ref writer, value3);
			FdbTuplePacker<T4>.Encoder(ref writer, value4);
			return writer.ToSlice();
		}

		#endregion

		#region Internal Helpers...

		/// <summary>Determines whether the specified tuple instances are considered equal</summary>
		/// <param name="left">Left tuple</param>
		/// <param name="right">Right tuple</param>
		/// <returns>True if the tuples are considered equal; otherwise, false. If both <paramref name="left"/> and <paramref name="right"/> are null, the methods returns true;</returns>
		/// <remarks>This method is equivalent of calling left.Equals(right), </remarks>
		public static bool Equals(IFdbTuple left, IFdbTuple right)
		{
			if (object.ReferenceEquals(left, null)) return object.ReferenceEquals(right, null);
			return left.Equals(right);
		}

		/// <summary>Determines whether the specifield tuple instances are considered similar</summary>
		/// <param name="left">Left tuple</param>
		/// <param name="right">Right tuple</param>
		/// <returns>True if the tuples are considered similar; otherwise, false. If both <paramref name="left"/> and <paramref name="right"/> are null, the methods returns true;</returns>
		public static bool Equivalent(IFdbTuple left, IFdbTuple right)
		{
			if (object.ReferenceEquals(left, null)) return object.ReferenceEquals(right, null);
			return !object.ReferenceEquals(right, null) && Equals(left, right, FdbTupleComparisons.Default);
		}

		/// <summary>Create a range that selects all tuples that are stored under the specified subspace: 'prefix\x00' &lt;= k &lt; 'prefix\xFF'</summary>
		/// <param name="prefix">Subspace binary prefix (that will be excluded from the range)</param>
		/// <returns>Range including all possible tuples starting with the specified prefix.</returns>
		/// <remarks>FdbTuple.ToRange(Slice.FromAscii("abc")) returns the range [ 'abc\x00', 'abc\xFF' )</remarks>
		public static FdbKeyRange ToRange(Slice prefix)
		{
			if (prefix.IsNull) throw new ArgumentNullException("prefix");

			//note: there is no guarantee that prefix is a valid packed tuple (could be any exotic binary prefix)

			// prefix => [ prefix."\0", prefix."\xFF" )
			return new FdbKeyRange(
				prefix + FdbKey.MinValue,
				prefix + FdbKey.MaxValue
			);
		}

		/// <summary>
		/// Create a range that selects all the tuples of greater length than the specified <paramref name="tuple"/>, and that start with the specified elements: packed(tuple)+'\x00' &lt;= k &lt; packed(tuple)+'\xFF'
		/// </summary>
		/// <example>FdbTuple.ToRange(FdbTuple.Create("a", "b")) includes all tuples ("a", "b", ...), but not the tuple ("a", "b") itself.</example>
		public static FdbKeyRange ToRange(IFdbTuple tuple)
		{
			if (tuple == null) throw new ArgumentNullException("tuple");

			// tuple => [ packed."\0", packed."\xFF" )
			var packed = tuple.ToSlice();

			return new FdbKeyRange(
				packed + FdbKey.MinValue,
				packed + FdbKey.MaxValue
			);
		}

		private static readonly string TokenNull = "null";
		private static readonly string TokenDoubleQuote = "\"";
		private static readonly string TokenSingleQuote = "'";
		private static readonly string TokenOpenBracket = "{";
		private static readonly string TokenCloseBracket = "}";
		private static readonly string TokenTupleEmpty = "()";
		private static readonly string TokenTupleSep = ", ";
		private static readonly string TokenTupleClose = ",)";

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
			if (item == null) return TokenNull;

			var s = item as string;
			//TODO: escape the string? If it contains \0 or control chars, it can cause problems in the console or debugger output
			if (s != null) return TokenDoubleQuote + s + TokenDoubleQuote; /* "hello" */

			if (item is char) return TokenSingleQuote + (char)item + TokenSingleQuote; /* 'X' */ 

			if (item is Slice) return ((Slice)item).ToAsciiOrHexaString();
			if (item is byte[]) return Slice.Create(item as byte[]).ToAsciiOrHexaString();

			if (item is FdbTupleAlias) return TokenOpenBracket + ((FdbTupleAlias)item).ToString() + TokenCloseBracket; /* {X} */

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
			if (count == 0) return TokenTupleEmpty; /* "()" */

			var sb = new StringBuilder();
			sb.Append('(').Append(Stringify(items[offset++]));
			while (--count > 0)
			{
				sb.Append(TokenTupleSep /* ", " */).Append(Stringify(items[offset++]));
			}
			return sb.Append(TokenTupleClose /* ",)" */).ToString();
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
				if (!enumerator.MoveNext()) return TokenTupleEmpty; /* "()" */

				var sb = new StringBuilder();
				sb.Append('(').Append(Stringify(enumerator.Current));
				while (enumerator.MoveNext())
				{
					sb.Append(TokenTupleSep /* ", " */).Append(Stringify(enumerator.Current));
				}

				return sb.Append(TokenTupleClose /* ",)" */).ToString();
			}
		}

		/// <summary>Default (non-optimized) implementation of IFdbTuple.this[long?, long?]</summary>
		/// <param name="tuple">Tuple to slice</param>
		/// <param name="fromIncluded">Start offset of the section (included)</param>
		/// <param name="toExcluded">End offset of the section (included)</param>
		/// <returns>New tuple only containing items inside this section</returns>
		internal static IFdbTuple Splice(IFdbTuple tuple, int? fromIncluded, int? toExcluded)
		{
			Contract.Requires(tuple != null);
			int count = tuple.Count;
			if (count == 0) return FdbTuple.Empty;

			int start = fromIncluded.HasValue ? MapIndexBounded(fromIncluded.Value, count) : 0;
			int end = toExcluded.HasValue ? MapIndexBounded(toExcluded.Value, count) : count;

			int len = end - start;

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
					int q = start;
					for (int p = 0; p < items.Length; p++)
					{
						items[p] = tuple[q++];
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
				if (!object.Equals(a[i], b[i])) return false;
			}
			return true;
		}

		/// <summary>Default (non-optimized) implementation for IFdbTuple.EndsWith()</summary>
		/// <param name="a">Larger tuple</param>
		/// <param name="b">Smaller tuple</param>
		/// <returns>True if <paramref name="a"/> starts with (or is equal to) <paramref name="b"/></returns>
		internal static bool EndsWith(IFdbTuple a, IFdbTuple b)
		{
			if (object.ReferenceEquals(a, b)) return true;
			int an = a.Count;
			int bn = b.Count;

			if (bn > an) return false;
			if (bn == 0) return true; // note: 'an' can only be 0 because of previous test

			int offset = an - bn;
			for (int i = 0; i < bn; i++)
			{
				if (!object.Equals(a[offset + i], b[i])) return false;
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
		internal static int MapIndex(int index, int count)
		{
			int offset = index;
			if (offset < 0) offset += count;
			if (offset < 0 || offset >= count) FailIndexOutOfRange(index, count);
			return offset;
		}

		/// <summary>Maps a relative index into an absolute index</summary>
		/// <param name="index">Relative index in the tuple (from the end if negative)</param>
		/// <param name="count">Size of the tuple</param>
		/// <returns>Absolute index from the start of the tuple. Truncated to 0 if index is before the start of the tuple, or to <paramref name="count"/> if the index is after the end of the tuple</returns>
		internal static int MapIndexBounded(int index, int count)
		{
			if (index < 0) index += count;
			return Math.Max(Math.Min(index, count), 0);
		}

		internal static void FailIndexOutOfRange(int index, int count)
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
