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

//#define ENABLE_VALUETUPLES

namespace Doxense.Collections.Tuples
{
	using System;
	using System.Collections.Generic;
	using System.Runtime.CompilerServices;
	using Doxense.Diagnostics.Contracts;
	using Doxense.Collections.Tuples.Encoding;
	using Doxense.Memory;
	using FoundationDB.Client;
	using JetBrains.Annotations;

	/// <summary>Tuple Binary Encoding</summary>
	public static class TuPack
	{

		#region Packing...

		// Without prefix

		/// <summary>Pack a tuple into a slice</summary>
		/// <param name="tuple">Tuple that must be serialized into a binary slice</param>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static Slice Pack<TTuple>([CanBeNull] TTuple tuple)
			where TTuple : ITuple
		{
			return TupleEncoder.Pack(tuple);
		}

		/// <summary>Pack a tuple into a slice</summary>
		/// <param name="tuple">Tuple that must be serialized into a binary slice</param>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static Slice Pack<T1>(STuple<T1> tuple)
		{
			var empty = default(Slice);
			return TupleEncoder.Pack(empty, ref tuple);
		}

		/// <summary>Pack a tuple into a slice</summary>
		/// <param name="tuple">Tuple that must be serialized into a binary slice</param>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static Slice Pack<T1, T2>(STuple<T1, T2> tuple)
		{
			var empty = default(Slice);
			return TupleEncoder.Pack(empty, ref tuple);
		}

		/// <summary>Pack a tuple into a slice</summary>
		/// <param name="tuple">Tuple that must be serialized into a binary slice</param>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static Slice Pack<T1, T2, T3>(STuple<T1, T2, T3> tuple)
		{
			var empty = default(Slice);
			return TupleEncoder.Pack(empty, ref tuple);
		}

		/// <summary>Pack a tuple into a slice</summary>
		/// <param name="tuple">Tuple that must be serialized into a binary slice</param>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static Slice Pack<T1, T2, T3, T4>(STuple<T1, T2, T3, T4> tuple)
		{
			var empty = default(Slice);
			return TupleEncoder.Pack(empty, ref tuple);
		}

		/// <summary>Pack a tuple into a slice</summary>
		/// <param name="tuple">Tuple that must be serialized into a binary slice</param>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static Slice Pack<T1, T2, T3, T4, T5>(STuple<T1, T2, T3, T4, T5> tuple)
		{
			var empty = default(Slice);
			return TupleEncoder.Pack(empty, ref tuple);
		}

		/// <summary>Pack a tuple into a slice</summary>
		/// <param name="tuple">Tuple that must be serialized into a binary slice</param>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static Slice Pack<T1, T2, T3, T4, T5, T6>(STuple<T1, T2, T3, T4, T5, T6> tuple)
		{
			var empty = default(Slice);
			return TupleEncoder.Pack(empty, ref tuple);
		}

#if ENABLE_VALUETUPLES

		/// <summary>Pack a tuple into a slice</summary>
		/// <param name="tuple">Tuple that must be serialized into a binary slice</param>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static Slice Pack<T1>(ValueTuple<T1> tuple)
		{
			var empty = default(Slice);
			return TupleEncoder.Pack(empty, tuple.ToSTuple());
		}

		/// <summary>Pack a tuple into a slice</summary>
		/// <param name="tuple">Tuple that must be serialized into a binary slice</param>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static Slice Pack<T1, T2>(ValueTuple<T1, T2> tuple)
		{
			var empty = default(Slice);
			return TupleEncoder.Pack(empty, tuple.ToSTuple());
		}

		/// <summary>Pack a tuple into a slice</summary>
		/// <param name="tuple">Tuple that must be serialized into a binary slice</param>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static Slice Pack<T1, T2, T3>(ValueTuple<T1, T2, T3> tuple)
		{
			var empty = default(Slice);
			return TupleEncoder.Pack(empty, tuple.ToSTuple());
		}

		/// <summary>Pack a tuple into a slice</summary>
		/// <param name="tuple">Tuple that must be serialized into a binary slice</param>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static Slice Pack<T1, T2, T3, T4>(ValueTuple<T1, T2, T3, T4> tuple)
		{
			var empty = default(Slice);
			return TupleEncoder.Pack(empty, tuple.ToSTuple());
		}

		/// <summary>Pack a tuple into a slice</summary>
		/// <param name="tuple">Tuple that must be serialized into a binary slice</param>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static Slice Pack<T1, T2, T3, T4, T5>(ValueTuple<T1, T2, T3, T4, T5> tuple)
		{
			var empty = default(Slice);
			return TupleEncoder.Pack(empty, tuple.ToSTuple());
		}

		/// <summary>Pack a tuple into a slice</summary>
		/// <param name="tuple">Tuple that must be serialized into a binary slice</param>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static Slice Pack<T1, T2, T3, T4, T5, T6>(ValueTuple<T1, T2, T3, T4, T5, T6> tuple)
		{
			var empty = default(Slice);
			return TupleEncoder.Pack(empty, tuple.ToSTuple());
		}

#endif

		/// <summary>Pack an array of N-tuples, all sharing the same buffer</summary>
		/// <param name="tuples">Sequence of N-tuples to pack</param>
		/// <returns>Array containing the buffer segment of each packed tuple</returns>
		/// <example>BatchPack([ ("Foo", 1), ("Foo", 2) ]) => [ "\x02Foo\x00\x15\x01", "\x02Foo\x00\x15\x02" ] </example>
		[Pure, NotNull, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static Slice[] PackTuples([NotNull] params ITuple[] tuples)
		{
			var empty = default(Slice);
			return TupleEncoder.Pack(empty, tuples);
		}

		/// <summary>Pack an array of 1-tuples, all sharing the same buffer</summary>
		/// <param name="tuples">Sequence of N-tuples to pack</param>
		/// <returns>Array containing the buffer segment of each packed tuple</returns>
		/// <example>BatchPack([ ("Foo", 1), ("Foo", 2) ]) => [ "\x02Foo\x00\x15\x01", "\x02Foo\x00\x15\x02" ] </example>
		[Pure, NotNull, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static Slice[] PackTuples<T1>([NotNull] params STuple<T1>[] tuples)
		{
			var empty = default(Slice);
			return TupleEncoder.Pack(empty, tuples);
		}

		/// <summary>Pack an array of 2-tuples, all sharing the same buffer</summary>
		/// <param name="tuples">Sequence of N-tuples to pack</param>
		/// <returns>Array containing the buffer segment of each packed tuple</returns>
		/// <example>BatchPack([ ("Foo", 1), ("Foo", 2) ]) => [ "\x02Foo\x00\x15\x01", "\x02Foo\x00\x15\x02" ] </example>
		[Pure, NotNull, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static Slice[] PackTuples<T1, T2>([NotNull] params STuple<T1, T2>[] tuples)
		{
			var empty = default(Slice);
			return TupleEncoder.Pack(empty, tuples);
		}

		/// <summary>Pack an array of 3-tuples, all sharing the same buffer</summary>
		/// <param name="tuples">Sequence of N-tuples to pack</param>
		/// <returns>Array containing the buffer segment of each packed tuple</returns>
		/// <example>BatchPack([ ("Foo", 1), ("Foo", 2) ]) => [ "\x02Foo\x00\x15\x01", "\x02Foo\x00\x15\x02" ] </example>
		[Pure, NotNull, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static Slice[] PackTuples<T1, T2, T3>([NotNull] params STuple<T1, T2, T3>[] tuples)
		{
			var empty = default(Slice);
			return TupleEncoder.Pack(empty, tuples);
		}

		/// <summary>Pack an array of 4-tuples, all sharing the same buffer</summary>
		/// <param name="tuples">Sequence of N-tuples to pack</param>
		/// <returns>Array containing the buffer segment of each packed tuple</returns>
		/// <example>BatchPack([ ("Foo", 1), ("Foo", 2) ]) => [ "\x02Foo\x00\x15\x01", "\x02Foo\x00\x15\x02" ] </example>
		[Pure, NotNull, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static Slice[] PackTuples<T1, T2, T3, T4>([NotNull] params STuple<T1, T2, T3, T4>[] tuples)
		{
			var empty = default(Slice);
			return TupleEncoder.Pack(empty, tuples);
		}

		/// <summary>Pack an array of 5-tuples, all sharing the same buffer</summary>
		/// <param name="tuples">Sequence of N-tuples to pack</param>
		/// <returns>Array containing the buffer segment of each packed tuple</returns>
		/// <example>BatchPack([ ("Foo", 1), ("Foo", 2) ]) => [ "\x02Foo\x00\x15\x01", "\x02Foo\x00\x15\x02" ] </example>
		[Pure, NotNull, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static Slice[] PackTuples<T1, T2, T3, T4, T5>([NotNull] params STuple<T1, T2, T3, T4, T5>[] tuples)
		{
			var empty = default(Slice);
			return TupleEncoder.Pack(empty, tuples);
		}

		/// <summary>Pack an array of 6-tuples, all sharing the same buffer</summary>
		/// <param name="tuples">Sequence of N-tuples to pack</param>
		/// <returns>Array containing the buffer segment of each packed tuple</returns>
		/// <example>BatchPack([ ("Foo", 1), ("Foo", 2) ]) => [ "\x02Foo\x00\x15\x01", "\x02Foo\x00\x15\x02" ] </example>
		[Pure, NotNull, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static Slice[] PackTuples<T1, T2, T3, T4, T5, T6>([NotNull] params STuple<T1, T2, T3, T4, T5, T6>[] tuples)
		{
			var empty = default(Slice);
			return TupleEncoder.Pack(empty, tuples);
		}

		/// <summary>Pack a sequence of N-tuples, all sharing the same buffer</summary>
		/// <param name="tuples">Sequence of N-tuples to pack</param>
		/// <returns>Array containing the buffer segment of each packed tuple</returns>
		/// <example>BatchPack([ ("Foo", 1), ("Foo", 2) ]) => [ "\x02Foo\x00\x15\x01", "\x02Foo\x00\x15\x02" ] </example>
		[Pure, NotNull, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static Slice[] PackTuples([NotNull, InstantHandle] this IEnumerable<ITuple> tuples)
		{
			var empty = default(Slice);
			return TupleEncoder.Pack(empty, tuples);
		}

		/// <summary>Efficiently write the packed representation of a tuple</summary>
		/// <param name="writer">Output buffer</param>
		/// <param name="tuple">Tuple that must be serialized into a binary slice</param>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void PackTo<TTuple>(ref SliceWriter writer, [CanBeNull] TTuple tuple)
			where TTuple : ITuple
		{
			TupleEncoder.PackTo(ref writer, tuple);
		}

		// With prefix

		/// <summary>Efficiently concatenate a prefix with the packed representation of a tuple</summary>
		/// <param name="prefix">Prefix added to the start of the packed slice</param>
		/// <summary>Pack a tuple into a slice</summary>
		/// <param name="tuple">Tuple that must be serialized into a binary slice</param>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static Slice Pack<TTuple>(Slice prefix, [CanBeNull] TTuple tuple)
			where TTuple : ITuple
		{
			return TupleEncoder.Pack(prefix, tuple);
		}

		/// <summary>Pack a tuple into a slice</summary>
		/// <param name="prefix">Prefix added to the start of the packed slice</param>
		/// <param name="tuple">Tuple that must be serialized into a binary slice</param>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static Slice Pack<T1>(Slice prefix, STuple<T1> tuple)
		{
			return TupleEncoder.Pack(prefix, ref tuple);
		}

		/// <summary>Pack a tuple into a slice</summary>
		/// <param name="prefix">Prefix added to the start of the packed slice</param>
		/// <param name="tuple">Tuple that must be serialized into a binary slice</param>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static Slice Pack<T1, T2>(Slice prefix, STuple<T1, T2> tuple)
		{
			return TupleEncoder.Pack(prefix, ref tuple);
		}

		/// <summary>Pack a tuple into a slice</summary>
		/// <param name="prefix">Prefix added to the start of the packed slice</param>
		/// <param name="tuple">Tuple that must be serialized into a binary slice</param>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static Slice Pack<T1, T2, T3>(Slice prefix, STuple<T1, T2, T3> tuple)
		{
			return TupleEncoder.Pack(prefix, ref tuple);
		}

		/// <summary>Pack a tuple into a slice</summary>
		/// <param name="prefix">Prefix added to the start of the packed slice</param>
		/// <param name="tuple">Tuple that must be serialized into a binary slice</param>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static Slice Pack<T1, T2, T3, T4>(Slice prefix, STuple<T1, T2, T3, T4> tuple)
		{
			return TupleEncoder.Pack(prefix, ref tuple);
		}

		/// <summary>Pack a tuple into a slice</summary>
		/// <param name="prefix">Prefix added to the start of the packed slice</param>
		/// <param name="tuple">Tuple that must be serialized into a binary slice</param>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static Slice Pack<T1, T2, T3, T4, T5>(Slice prefix, STuple<T1, T2, T3, T4, T5> tuple)
		{
			return TupleEncoder.Pack(prefix, ref tuple);
		}

		/// <summary>Pack a tuple into a slice</summary>
		/// <param name="prefix">Common prefix added to all the tuples</param>
		/// <param name="tuple">Tuple that must be serialized into a binary slice</param>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static Slice Pack<T1, T2, T3, T4, T5, T6>(Slice prefix, STuple<T1, T2, T3, T4, T5, T6> tuple)
		{
			return TupleEncoder.Pack(prefix, ref tuple);
		}

		/// <summary>Pack an array of N-tuples, all sharing the same buffer</summary>
		/// <param name="prefix">Common prefix added to all the tuples</param>
		/// <param name="tuples">Sequence of N-tuples to pack</param>
		/// <returns>Array containing the buffer segment of each packed tuple</returns>
		/// <example>BatchPack("abc", [ ("Foo", 1), ("Foo", 2) ]) => [ "abc\x02Foo\x00\x15\x01", "abc\x02Foo\x00\x15\x02" ] </example>
		[Pure, NotNull, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static Slice[] PackTuples(Slice prefix, [NotNull] params ITuple[] tuples)
		{
			return TupleEncoder.Pack(prefix, tuples);
		}

		/// <summary>Pack a sequence of N-tuples, all sharing the same buffer</summary>
		/// <param name="prefix">Common prefix added to all the tuples</param>
		/// <param name="tuples">Sequence of N-tuples to pack</param>
		/// <returns>Array containing the buffer segment of each packed tuple</returns>
		/// <example>BatchPack("abc", [ ("Foo", 1), ("Foo", 2) ]) => [ "abc\x02Foo\x00\x15\x01", "abc\x02Foo\x00\x15\x02" ] </example>
		[Pure, NotNull, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static Slice[] PackTuples(Slice prefix, [NotNull] IEnumerable<ITuple> tuples)
		{
			return TupleEncoder.Pack(prefix, tuples);
		}

		[Pure, NotNull, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static Slice[] PackTuples<TElement, TTuple>(Slice prefix, [NotNull] TElement[] elements, Func<TElement, TTuple> transform)
			where TTuple : ITuple
		{
			return TupleEncoder.Pack(prefix, elements, transform);
		}

		[Pure, NotNull, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static Slice[] PackTuples<TElement, TTuple>(Slice prefix, [NotNull] IEnumerable<TElement> elements, Func<TElement, TTuple> transform)
			where TTuple : ITuple
		{
			return TupleEncoder.Pack(prefix, elements, transform);
		}

		#endregion

		#region Encode

		//REVIEW: EncodeKey/EncodeKeys? Encode/EncodeRange? EncodeValues? EncodeItems?

		/// <summary>Pack a 1-tuple directly into a slice</summary>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static Slice EncodeKey<T1>(T1 item1)
		{
			return TupleEncoder.EncodeKey(item1);
		}

		/// <summary>Pack a 2-tuple directly into a slice</summary>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static Slice EncodeKey<T1, T2>(T1 item1, T2 item2)
		{
			return TupleEncoder.EncodeKey(item1, item2);
		}

		/// <summary>Pack a 3-tuple directly into a slice</summary>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static Slice EncodeKey<T1, T2, T3>(T1 item1, T2 item2, T3 item3)
		{
			return TupleEncoder.EncodeKey(item1, item2, item3);
		}

		/// <summary>Pack a 4-tuple directly into a slice</summary>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static Slice EncodeKey<T1, T2, T3, T4>(T1 item1, T2 item2, T3 item3, T4 item4)
		{
			return TupleEncoder.EncodeKey(item1, item2, item3, item4);
		}

		/// <summary>Pack a 5-tuple directly into a slice</summary>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static Slice EncodeKey<T1, T2, T3, T4, T5>(T1 item1, T2 item2, T3 item3, T4 item4, T5 item5)
		{
			return TupleEncoder.EncodeKey(item1, item2, item3, item4, item5);
		}

		/// <summary>Pack a 6-tuple directly into a slice</summary>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static Slice EncodeKey<T1, T2, T3, T4, T5, T6>(T1 item1, T2 item2, T3 item3, T4 item4, T5 item5, T6 item6)
		{
			return TupleEncoder.EncodeKey(item1, item2, item3, item4, item5, item6);
		}

		/// <summary>Pack a 6-tuple directly into a slice</summary>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static Slice EncodeKey<T1, T2, T3, T4, T5, T6, T7>(T1 item1, T2 item2, T3 item3, T4 item4, T5 item5, T6 item6, T7 item7)
		{
			return TupleEncoder.EncodeKey(item1, item2, item3, item4, item5, item6, item7);
		}

		/// <summary>Pack a 6-tuple directly into a slice</summary>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static Slice EncodeKey<T1, T2, T3, T4, T5, T6, T7, T8>(T1 item1, T2 item2, T3 item3, T4 item4, T5 item5, T6 item6, T7 item7, T8 item8)
		{
			return TupleEncoder.EncodeKey(item1, item2, item3, item4, item5, item6, item7, item8);
		}

		[Pure, NotNull, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static Slice[] EncodeKeys<T1>([NotNull] IEnumerable<T1> keys)
		{
			var empty = default(Slice);
			return TupleEncoder.EncodePrefixedKeys(empty, keys);
		}

		/// <summary>Merge a sequence of keys with a same prefix, all sharing the same buffer</summary>
		/// <typeparam name="T">Type of the keys</typeparam>
		/// <param name="prefix">Prefix shared by all keys</param>
		/// <param name="keys">Sequence of keys to pack</param>
		/// <returns>Array of slices (for all keys) that share the same underlying buffer</returns>
		[Pure, NotNull, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static Slice[] EncodePrefixedKeys<T>(Slice prefix, [NotNull] IEnumerable<T> keys)
		{
			return TupleEncoder.EncodePrefixedKeys(prefix, keys);
		}

		[Pure, NotNull, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static Slice[] EncodeKeys<T>([NotNull] params T[] keys)
		{
			var empty = default(Slice);
			return TupleEncoder.EncodePrefixedKeys(empty, keys);
		}

		/// <summary>Merge an array of keys with a same prefix, all sharing the same buffer</summary>
		/// <typeparam name="T">Type of the keys</typeparam>
		/// <param name="prefix">Prefix shared by all keys</param>
		/// <param name="keys">Sequence of keys to pack</param>
		/// <returns>Array of slices (for all keys) that share the same underlying buffer</returns>
		[Pure, NotNull, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static Slice[] EncodePrefixedKeys<T>(Slice prefix, [NotNull] params T[] keys)
		{
			return TupleEncoder.EncodePrefixedKeys(prefix, keys);
		}

		/// <summary>Merge an array of elements, all sharing the same buffer</summary>
		/// <typeparam name="TElement">Type of the elements</typeparam>
		/// <typeparam name="TKey">Type of the keys extracted from the elements</typeparam>
		/// <param name="elements">Sequence of elements to pack</param>
		/// <param name="selector">Lambda that extract the key from each element</param>
		/// <returns>Array of slices (for all keys) that share the same underlying buffer</returns>
		[Pure, NotNull, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static Slice[] EncodeKeys<TKey, TElement>([NotNull] TElement[] elements, [NotNull] Func<TElement, TKey> selector)
		{
			var empty = default(Slice);
			return TupleEncoder.EncodePrefixedKeys(empty, elements, selector);
		}

		/// <summary>Merge an array of elements with a same prefix, all sharing the same buffer</summary>
		/// <typeparam name="TElement">Type of the elements</typeparam>
		/// <typeparam name="TKey">Type of the keys extracted from the elements</typeparam>
		/// <param name="prefix">Prefix shared by all keys (can be empty)</param>
		/// <param name="elements">Sequence of elements to pack</param>
		/// <param name="selector">Lambda that extract the key from each element</param>
		/// <returns>Array of slices (for all keys) that share the same underlying buffer</returns>
		[Pure, NotNull, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static Slice[] EncodePrefixedKeys<TKey, TElement>(Slice prefix, [NotNull] TElement[] elements, [NotNull] Func<TElement, TKey> selector)
		{
			return TupleEncoder.EncodePrefixedKeys(prefix, elements, selector);
		}

		/// <summary>Pack a sequence of keys with a same prefix, all sharing the same buffer</summary>
		/// <typeparam name="T">Type of the keys</typeparam>
		/// <param name="prefix">Prefix shared by all keys</param>
		/// <param name="keys">Sequence of keys to pack</param>
		/// <returns>Array of slices (for all keys) that share the same underlying buffer</returns>
		[Pure, NotNull, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static Slice[] EncodePrefixedKeys<T>([NotNull] ITuple prefix, [NotNull] IEnumerable<T> keys)
		{
			Contract.NotNull(prefix, nameof(prefix));

			return EncodePrefixedKeys(Pack(prefix), keys);
		}

		/// <summary>Pack a sequence of keys with a same prefix, all sharing the same buffer</summary>
		/// <typeparam name="T">Type of the keys</typeparam>
		/// <param name="prefix">Prefix shared by all keys</param>
		/// <param name="keys">Sequence of keys to pack</param>
		/// <returns>Array of slices (for all keys) that share the same underlying buffer</returns>
		[Pure, NotNull, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static Slice[] EncodePrefixedKeys<T>([NotNull] ITuple prefix, [NotNull] params T[] keys)
		{
			Contract.NotNull(prefix, nameof(prefix));

			return EncodePrefixedKeys(Pack(prefix), keys);
		}

		#endregion

		#region Ranges...

		/// <summary>Create a range that selects all tuples that are stored under the specified subspace: 'prefix\x00' &lt;= k &lt; 'prefix\xFF'</summary>
		/// <param name="prefix">Subspace binary prefix (that will be excluded from the range)</param>
		/// <returns>Range including all possible tuples starting with the specified prefix.</returns>
		/// <remarks>FdbTuple.ToRange(Slice.FromAscii("abc")) returns the range [ 'abc\x00', 'abc\xFF' )</remarks>
		[Pure]
		public static KeyRange ToRange(Slice prefix)
		{
			if (prefix.IsNull) throw new ArgumentNullException(nameof(prefix));
			//note: there is no guarantee that prefix is a valid packed tuple (could be any exotic binary prefix)

			// prefix => [ prefix."\0", prefix."\xFF" )
			return new KeyRange(
				prefix + 0x00,
				prefix + 0xFF
			);
		}

		/// <summary>Create a range that selects all the tuples of greater length than the specified <paramref name="tuple"/>, and that start with the specified elements: packed(tuple)+'\x00' &lt;= k &lt; packed(tuple)+'\xFF'</summary>
		/// <example>FdbTuple.ToRange(FdbTuple.Create("a", "b")) includes all tuples ("a", "b", ...), but not the tuple ("a", "b") itself.</example>
		[Pure]
		public static KeyRange ToRange<TTuple>([NotNull] TTuple tuple)
			where TTuple : ITuple
		{
			Contract.NotNullAllowStructs(tuple, nameof(tuple));

			// tuple => [ packed."\0", packed."\xFF" )
			var packed = TupleEncoder.Pack(tuple);
			return new KeyRange(
				packed + 0x00,
				packed + 0xFF
			);
		}

		/// <summary>Create a range that selects all the tuples of greater length than the specified <paramref name="tuple"/>, and that start with the specified elements: packed(tuple)+'\x00' &lt;= k &lt; packed(tuple)+'\xFF'</summary>
		/// <example>FdbTuple.ToRange(FdbTuple.Create("a", "b")) includes all tuples ("a", "b", ...), but not the tuple ("a", "b") itself.</example>
		[Pure]
		public static KeyRange ToRange<T1>(STuple<T1> tuple)
		{
			Contract.NotNullAllowStructs(tuple, nameof(tuple));

			// tuple => [ packed."\0", packed."\xFF" )
			var packed = TupleEncoder.Pack(tuple);
			return new KeyRange(
				packed + 0x00,
				packed + 0xFF
			);
		}

		/// <summary>Create a range that selects all the tuples of greater length than the specified <paramref name="tuple"/>, and that start with the specified elements: packed(tuple)+'\x00' &lt;= k &lt; packed(tuple)+'\xFF'</summary>
		/// <example>FdbTuple.ToRange(FdbTuple.Create("a", "b")) includes all tuples ("a", "b", ...), but not the tuple ("a", "b") itself.</example>
		[Pure]
		public static KeyRange ToRange<T1, T2>(STuple<T1, T2> tuple)
		{
			Contract.NotNullAllowStructs(tuple, nameof(tuple));

			// tuple => [ packed."\0", packed."\xFF" )
			var empty = default(Slice);
			var packed = TupleEncoder.Pack(empty, ref tuple);
			return new KeyRange(
				packed + 0x00,
				packed + 0xFF
			);
		}

		/// <summary>Create a range that selects all the tuples of greater length than the specified <paramref name="tuple"/>, and that start with the specified elements: packed(tuple)+'\x00' &lt;= k &lt; packed(tuple)+'\xFF'</summary>
		/// <example>FdbTuple.ToRange(FdbTuple.Create("a", "b")) includes all tuples ("a", "b", ...), but not the tuple ("a", "b") itself.</example>
		[Pure]
		public static KeyRange ToRange<T1, T2, T3>(STuple<T1, T2, T3> tuple)
		{
			Contract.NotNullAllowStructs(tuple, nameof(tuple));

			// tuple => [ packed."\0", packed."\xFF" )
			var empty = default(Slice);
			var packed = TupleEncoder.Pack(empty, ref tuple);
			return new KeyRange(
				packed + 0x00,
				packed + 0xFF
			);
		}

		/// <summary>Create a range that selects all the tuples of greater length than the specified <paramref name="tuple"/>, and that start with the specified elements: packed(tuple)+'\x00' &lt;= k &lt; packed(tuple)+'\xFF'</summary>
		/// <example>FdbTuple.ToRange(FdbTuple.Create("a", "b")) includes all tuples ("a", "b", ...), but not the tuple ("a", "b") itself.</example>
		[Pure]
		public static KeyRange ToRange<T1, T2, T3, T4>(STuple<T1, T2, T3, T4> tuple)
		{
			Contract.NotNullAllowStructs(tuple, nameof(tuple));

			// tuple => [ packed."\0", packed."\xFF" )
			var empty = default(Slice);
			var packed = TupleEncoder.Pack(empty, ref tuple);
			return new KeyRange(
				packed + 0x00,
				packed + 0xFF
			);
		}

		/// <summary>Create a range that selects all the tuples of greater length than the specified <paramref name="tuple"/>, and that start with the specified elements: packed(tuple)+'\x00' &lt;= k &lt; packed(tuple)+'\xFF'</summary>
		/// <example>FdbTuple.ToRange(FdbTuple.Create("a", "b")) includes all tuples ("a", "b", ...), but not the tuple ("a", "b") itself.</example>
		[Pure]
		public static KeyRange ToRange<T1, T2, T3, T4, T5>(STuple<T1, T2, T3, T4, T5> tuple)
		{
			Contract.NotNullAllowStructs(tuple, nameof(tuple));

			// tuple => [ packed."\0", packed."\xFF" )
			var empty = default(Slice);
			var packed = TupleEncoder.Pack(empty, ref tuple);
			return new KeyRange(
				packed + 0x00,
				packed + 0xFF
			);
		}

		/// <summary>Create a range that selects all the tuples of greater length than the specified <paramref name="tuple"/>, and that start with the specified elements: packed(tuple)+'\x00' &lt;= k &lt; packed(tuple)+'\xFF'</summary>
		/// <example>FdbTuple.ToRange(FdbTuple.Create("a", "b")) includes all tuples ("a", "b", ...), but not the tuple ("a", "b") itself.</example>
		[Pure]
		public static KeyRange ToRange<T1, T2, T3, T4, T5, T6>(STuple<T1, T2, T3, T4, T5, T6> tuple)
		{
			Contract.NotNullAllowStructs(tuple, nameof(tuple));

			// tuple => [ packed."\0", packed."\xFF" )
			var empty = default(Slice);
			var packed = TupleEncoder.Pack(empty, ref tuple);
			return new KeyRange(
				packed + 0x00,
				packed + 0xFF
			);
		}

		/// <summary>Create a range that selects all the tuples of greater length than the specified <paramref name="tuple"/>, and that start with the specified elements: packed(tuple)+'\x00' &lt;= k &lt; packed(tuple)+'\xFF'</summary>
		/// <example>FdbTuple.ToRange(Slice.FromInt32(42), FdbTuple.Create("a", "b")) includes all tuples \x2A.("a", "b", ...), but not the tuple \x2A.("a", "b") itself.</example>
		/// <remarks>If <paramref name="prefix"/> is the packed representation of a tuple, then unpacking the resulting key will produce a valid tuple. If not, then the resulting key will need to be truncated first before unpacking.</remarks>
		[Pure]
		public static KeyRange ToRange<TTuple>(Slice prefix, [NotNull] TTuple tuple)
			where TTuple : ITuple
		{
			Contract.NotNullAllowStructs(tuple, nameof(tuple));

			// tuple => [ prefix.packed."\0", prefix.packed."\xFF" )
			var packed = TupleEncoder.Pack(prefix, tuple);
			return new KeyRange(
				packed + 0x00,
				packed + 0xFF
			);
		}

		/// <summary>Create a range that selects all the tuples of greater length than the specified <paramref name="tuple"/>, and that start with the specified elements: packed(tuple)+'\x00' &lt;= k &lt; packed(tuple)+'\xFF'</summary>
		/// <example>FdbTuple.ToRange(FdbTuple.Create("a", "b")) includes all tuples ("a", "b", ...), but not the tuple ("a", "b") itself.</example>
		[Pure]
		public static KeyRange ToRange<T1>(Slice prefix, STuple<T1> tuple)
		{
			Contract.NotNullAllowStructs(tuple, nameof(tuple));

			// tuple => [ packed."\0", packed."\xFF" )
			var packed = TupleEncoder.Pack(prefix, tuple);
			return new KeyRange(
				packed + 0x00,
				packed + 0xFF
			);
		}

		/// <summary>Create a range that selects all the tuples of greater length than the specified <paramref name="tuple"/>, and that start with the specified elements: packed(tuple)+'\x00' &lt;= k &lt; packed(tuple)+'\xFF'</summary>
		/// <example>FdbTuple.ToRange(FdbTuple.Create("a", "b")) includes all tuples ("a", "b", ...), but not the tuple ("a", "b") itself.</example>
		[Pure]
		public static KeyRange ToRange<T1, T2>(Slice prefix, STuple<T1, T2> tuple)
		{
			Contract.NotNullAllowStructs(tuple, nameof(tuple));

			// tuple => [ packed."\0", packed."\xFF" )
			var packed = TupleEncoder.Pack(prefix, ref tuple);
			return new KeyRange(
				packed + 0x00,
				packed + 0xFF
			);
		}

		/// <summary>Create a range that selects all the tuples of greater length than the specified <paramref name="tuple"/>, and that start with the specified elements: packed(tuple)+'\x00' &lt;= k &lt; packed(tuple)+'\xFF'</summary>
		/// <example>FdbTuple.ToRange(FdbTuple.Create("a", "b")) includes all tuples ("a", "b", ...), but not the tuple ("a", "b") itself.</example>
		[Pure]
		public static KeyRange ToRange<T1, T2, T3>(Slice prefix, STuple<T1, T2, T3> tuple)
		{
			Contract.NotNullAllowStructs(tuple, nameof(tuple));

			// tuple => [ packed."\0", packed."\xFF" )
			var packed = TupleEncoder.Pack(prefix, ref tuple);
			return new KeyRange(
				packed + 0x00,
				packed + 0xFF
			);
		}

		/// <summary>Create a range that selects all the tuples of greater length than the specified <paramref name="tuple"/>, and that start with the specified elements: packed(tuple)+'\x00' &lt;= k &lt; packed(tuple)+'\xFF'</summary>
		/// <example>FdbTuple.ToRange(FdbTuple.Create("a", "b")) includes all tuples ("a", "b", ...), but not the tuple ("a", "b") itself.</example>
		[Pure]
		public static KeyRange ToRange<T1, T2, T3, T4>(Slice prefix, STuple<T1, T2, T3, T4> tuple)
		{
			Contract.NotNullAllowStructs(tuple, nameof(tuple));

			// tuple => [ packed."\0", packed."\xFF" )
			var packed = TupleEncoder.Pack(prefix, ref tuple);
			return new KeyRange(
				packed + 0x00,
				packed + 0xFF
			);
		}

		/// <summary>Create a range that selects all the tuples of greater length than the specified <paramref name="tuple"/>, and that start with the specified elements: packed(tuple)+'\x00' &lt;= k &lt; packed(tuple)+'\xFF'</summary>
		/// <example>FdbTuple.ToRange(FdbTuple.Create("a", "b")) includes all tuples ("a", "b", ...), but not the tuple ("a", "b") itself.</example>
		[Pure]
		public static KeyRange ToRange<T1, T2, T3, T4, T5>(Slice prefix, STuple<T1, T2, T3, T4, T5> tuple)
		{
			Contract.NotNullAllowStructs(tuple, nameof(tuple));

			// tuple => [ packed."\0", packed."\xFF" )
			var packed = TupleEncoder.Pack(prefix, ref tuple);
			return new KeyRange(
				packed + 0x00,
				packed + 0xFF
			);
		}

		/// <summary>Create a range that selects all the tuples of greater length than the specified <paramref name="tuple"/>, and that start with the specified elements: packed(tuple)+'\x00' &lt;= k &lt; packed(tuple)+'\xFF'</summary>
		/// <example>FdbTuple.ToRange(FdbTuple.Create("a", "b")) includes all tuples ("a", "b", ...), but not the tuple ("a", "b") itself.</example>
		[Pure]
		public static KeyRange ToRange<T1, T2, T3, T4, T5, T6>(Slice prefix, STuple<T1, T2, T3, T4, T5, T6> tuple)
		{
			Contract.NotNullAllowStructs(tuple, nameof(tuple));

			// tuple => [ packed."\0", packed."\xFF" )
			var packed = TupleEncoder.Pack(prefix, ref tuple);
			return new KeyRange(
				packed + 0x00,
				packed + 0xFF
			);
		}

		#endregion

		#region Unpacking...

		/// <summary>Unpack a tuple from a serialied key blob</summary>
		/// <param name="packedKey">Binary key containing a previously packed tuple</param>
		/// <returns>Unpacked tuple, or the empty tuple if the key is <see cref="Slice.Empty"/></returns>
		/// <exception cref="System.ArgumentNullException">If <paramref name="packedKey"/> is equal to <see cref="Slice.Nil"/></exception>
		[Pure, NotNull]
		public static ITuple Unpack(Slice packedKey)
		{
			if (packedKey.IsNull) throw new ArgumentNullException(nameof(packedKey), "Cannot unpack tuple from Nil");
			if (packedKey.Count == 0) return STuple.Empty;

			return TuplePackers.Unpack(packedKey, embedded: false);
		}

		/// <summary>Unpack a tuple from a binary representation</summary>
		/// <param name="packedKey">Binary key containing a previously packed tuple, or Slice.Nil</param>
		/// <returns>Unpacked tuple, the empty tuple if <paramref name="packedKey"/> is equal to <see cref="Slice.Empty"/>, or null if the key is <see cref="Slice.Nil"/></returns>
		[Pure, CanBeNull]
		public static ITuple UnpackOrDefault(Slice packedKey)
		{
			if (packedKey.IsNull) return null;
			if (packedKey.Count == 0) return STuple.Empty;
			return TuplePackers.Unpack(packedKey, embedded: false);
		}

		/// <summary>Unpack a tuple and only return its first element</summary>
		/// <typeparam name="T">Type of the first value in the decoded tuple</typeparam>
		/// <param name="packedKey">Slice that should be entirely parsable as a tuple</param>
		/// <returns>Decoded value of the first item in the tuple</returns>
		[Pure]
		public static T DecodeFirst<T>(Slice packedKey)
		{
			if (packedKey.IsNullOrEmpty) throw new InvalidOperationException("Cannot unpack the first element of an empty tuple");

			var slice = TuplePackers.UnpackFirst(packedKey);
			if (slice.IsNull) throw new InvalidOperationException("Failed to unpack tuple");

			return TuplePacker<T>.Deserialize(slice);
		}

		/// <summary>Unpack a tuple and only return its last element</summary>
		/// <typeparam name="T">Type of the last value in the decoded tuple</typeparam>
		/// <param name="packedKey">Slice that should be entirely parsable as a tuple</param>
		/// <returns>Decoded value of the last item in the tuple</returns>
		[Pure]
		public static T DecodeLast<T>(Slice packedKey)
		{
			if (packedKey.IsNullOrEmpty) throw new InvalidOperationException("Cannot unpack the last element of an empty tuple");

			var slice = TuplePackers.UnpackLast(packedKey);
			if (slice.IsNull) throw new InvalidOperationException("Failed to unpack tuple");

			return TuplePacker<T>.Deserialize(slice);
		}

		/// <summary>Unpack the value of a singleton tuple</summary>
		/// <typeparam name="T1">Type of the single value in the decoded tuple</typeparam>
		/// <param name="packedKey">Slice that should contain the packed representation of a tuple with a single element</param>
		/// <returns>Decoded value of the only item in the tuple. Throws an exception if the tuple is empty of has more than one element.</returns>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static T1 DecodeKey<T1>(Slice packedKey)
		{
			TupleEncoder.DecodeKey(packedKey, out STuple<T1> tuple);
			return tuple.Item1;
		}

		/// <summary>Unpack a key containing two elements</summary>
		/// <param name="packedKey">Slice that should contain the packed representation of a tuple with two elements</param>
		/// <returns>Decoded value of the elements int the tuple. Throws an exception if the tuple is empty of has more than elements.</returns>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static STuple<T1, T2> DecodeKey<T1, T2>(Slice packedKey)
		{
			TupleEncoder.DecodeKey(packedKey, out STuple<T1, T2> tuple);
			return tuple;
		}

		/// <summary>Unpack a key containing three elements</summary>
		/// <param name="packedKey">Slice that should contain the packed representation of a tuple with three elements</param>
		/// <returns>Decoded value of the elements int the tuple. Throws an exception if the tuple is empty of has more than elements.</returns>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static STuple<T1, T2, T3> DecodeKey<T1, T2, T3>(Slice packedKey)
		{
			TupleEncoder.DecodeKey(packedKey, out STuple<T1, T2, T3> tuple);
			return tuple;
		}

		/// <summary>Unpack a key containing four elements</summary>
		/// <param name="packedKey">Slice that should contain the packed representation of a tuple with four elements</param>
		/// <returns>Decoded value of the elements int the tuple. Throws an exception if the tuple is empty of has more than elements.</returns>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static STuple<T1, T2, T3, T4> DecodeKey<T1, T2, T3, T4>(Slice packedKey)
		{
			TupleEncoder.DecodeKey(packedKey, out STuple<T1, T2, T3, T4> tuple);
			return tuple;
		}

		/// <summary>Unpack a key containing five elements</summary>
		/// <param name="packedKey">Slice that should contain the packed representation of a tuple with five elements</param>
		/// <returns>Decoded value of the elements int the tuple. Throws an exception if the tuple is empty of has more than elements.</returns>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static STuple<T1, T2, T3, T4, T5> DecodeKey<T1, T2, T3, T4, T5>(Slice packedKey)
		{
			TupleEncoder.DecodeKey(packedKey, out STuple<T1, T2, T3, T4, T5> tuple);
			return tuple;
		}

		/// <summary>Unpack a key containing six elements</summary>
		/// <param name="packedKey">Slice that should contain the packed representation of a tuple with six elements</param>
		/// <returns>Decoded value of the elements int the tuple. Throws an exception if the tuple is empty of has more than elements.</returns>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static STuple<T1, T2, T3, T4, T5, T6> DecodeKey<T1, T2, T3, T4, T5, T6>(Slice packedKey)
		{
			TupleEncoder.DecodeKey(packedKey, out STuple<T1, T2, T3, T4, T5, T6> tuple);
			return tuple;
		}

		/// <summary>Unpack the next item in the tuple, and advance the cursor</summary>
		/// <typeparam name="T">Type of the next value in the tuple</typeparam>
		/// <param name="input">Reader positionned at the start of the next item to read</param>
		/// <param name="value">If decoding succeedsd, receives the decoded value.</param>
		/// <returns>True if the decoded succeeded (and <paramref name="value"/> receives the decoded value). False if the tuple has reached the end.</returns>
		public static bool DecodeNext<T>(ref TupleReader input, out T value)
		{
			if (!input.Input.HasMore)
			{
				value = default(T);
				return false;
			}

			var slice = TupleParser.ParseNext(ref input);
			value = TuplePacker<T>.Deserialize(slice);
			return true;
		}

		#endregion

		#region EncodePrefixedKey...

		//note: they are equivalent to the Pack<...>() methods, they only take a binary prefix

		/// <summary>Efficiently concatenate a prefix with the packed representation of a 1-tuple</summary>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static Slice EncodePrefixedKey<T1>(Slice prefix, T1 value)
		{
			return TupleEncoder.EncodePrefixedKey(prefix, value);
		}

		/// <summary>Efficiently concatenate a prefix with the packed representation of a 2-tuple</summary>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static Slice EncodePrefixedKey<T1, T2>(Slice prefix, T1 value1, T2 value2)
		{
			return TupleEncoder.EncodePrefixedKey(prefix, value1, value2);
		}

		/// <summary>Efficiently concatenate a prefix with the packed representation of a 3-tuple</summary>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static Slice EncodePrefixedKey<T1, T2, T3>(Slice prefix, T1 value1, T2 value2, T3 value3)
		{
			return TupleEncoder.EncodePrefixedKey(prefix, value1, value2, value3);
		}

		/// <summary>Efficiently concatenate a prefix with the packed representation of a 4-tuple</summary>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static Slice EncodePrefixedKey<T1, T2, T3, T4>(Slice prefix, T1 value1, T2 value2, T3 value3, T4 value4)
		{
			return TupleEncoder.EncodePrefixedKey(prefix, value1, value2, value3, value4);
		}

		/// <summary>Efficiently concatenate a prefix with the packed representation of a 5-tuple</summary>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static Slice EncodePrefixedKey<T1, T2, T3, T4, T5>(Slice prefix, T1 value1, T2 value2, T3 value3, T4 value4, T5 value5)
		{
			return TupleEncoder.EncodePrefixedKey(prefix, value1, value2, value3, value4, value5);
		}

		/// <summary>Efficiently concatenate a prefix with the packed representation of a 6-tuple</summary>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static Slice EncodePrefixedKey<T1, T2, T3, T4, T5, T6>(Slice prefix, T1 value1, T2 value2, T3 value3, T4 value4, T5 value5, T6 value6)
		{
			return TupleEncoder.EncodePrefixedKey(prefix, value1, value2, value3, value4, value5, value6);
		}

		/// <summary>Efficiently concatenate a prefix with the packed representation of a 7-tuple</summary>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static Slice EncodePrefixedKey<T1, T2, T3, T4, T5, T6, T7>(Slice prefix, T1 value1, T2 value2, T3 value3, T4 value4, T5 value5, T6 value6, T7 value7)
		{
			return TupleEncoder.EncodePrefixedKey(prefix, value1, value2, value3, value4, value5, value6, value7);
		}

		/// <summary>Efficiently concatenate a prefix with the packed representation of a 8-tuple</summary>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static Slice EncodePrefixedKey<T1, T2, T3, T4, T5, T6, T7, T8>(Slice prefix, T1 value1, T2 value2, T3 value3, T4 value4, T5 value5, T6 value6, T7 value7, T8 value8)
		{
			return TupleEncoder.EncodePrefixedKey(prefix, value1, value2, value3, value4, value5, value6, value7, value8);
		}

		#endregion

	}

}
