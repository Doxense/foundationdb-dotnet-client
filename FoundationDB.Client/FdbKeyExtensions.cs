#region Copyright (c) 2023-2025 SnowBank SAS, (c) 2005-2023 Doxense SAS
// All rights reserved.
// 
// Redistribution and use in source and binary forms, with or without
// modification, are permitted provided that the following conditions are met:
// 	* Redistributions of source code must retain the above copyright
// 	  notice, this list of conditions and the following disclaimer.
// 	* Redistributions in binary form must reproduce the above copyright
// 	  notice, this list of conditions and the following disclaimer in the
// 	  documentation and/or other materials provided with the distribution.
// 	* Neither the name of SnowBank nor the
// 	  names of its contributors may be used to endorse or promote products
// 	  derived from this software without specific prior written permission.
// 
// THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS "AS IS" AND
// ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED
// WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE
// DISCLAIMED. IN NO EVENT SHALL SNOWBANK SAS BE LIABLE FOR ANY
// DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES
// (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES;
// LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND
// ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT
// (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS
// SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
#endregion

namespace FoundationDB.Client
{
	using System;
	using System.ComponentModel;

	/// <summary>Extension methods for working with <see cref="FdbKey{TKey,TEncoder}"/></summary>
	[PublicAPI]
	public static class FdbKeyExtensions
	{

		#region Ranges...

		/// <summary>Returns a key that adds a <c>0x00</c> suffix to get the immediate successor of this key in the database (ex: <c>`abc`</c> => <c>`abc\x00`</c>)</summary>
		/// <typeparam name="TKey">Type of the key</typeparam>
		/// <param name="key">Input key</param>
		/// <returns>Key that, when encoded into binary, will append the byte <c>0x00</c> to the representation of <paramref name="key"/></returns>
		/// <remarks>
		/// <para>It is guaranteed that there can not be any keys between <paramref name="key"/> and the returned key.</para>
		/// <para>This can be used to produce the "end" key for a read or write conflict range, that will match a single key</para>
		/// <para>For example, <c>subspace.GetKey(123).GetSuccessor()</c> will generate <c>`\x15\x7B`</c> + <c>`\x00`</c> = <c>`\x15\x7B\x00`</c>, which is the same as <c>subspace.GetKey(123, null)</c>, the next possible key.</para>
		/// <para>It should be combined with <see cref="FdbKeySelector.FirstGreaterOrEqual{TKey}"/> to produce a valid 'end' selector for a range read</para>
		/// </remarks>
		/// <seealso cref="FdbKeySelector.FirstGreaterOrEqual{TKey}"/>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static FdbSuccessorKey<TKey> Successor<TKey>(this TKey key)
			where TKey : struct, IFdbKey
			=> new(key);

		/// <summary>Returns the last legal key that is a child of this key, and that can be expressed using tuples.</summary>
		/// <param name="key">Parent key</param>
		/// <returns>Key that is equal to this key, with an extra <c>`\xFF`</c> byte at the end</returns>
		/// <remarks>
		/// <para>This key can be used to match all tuple-based keys that could be located under this key. If the keys could include any arbitrary binary sequence, especially if the first byte of the part of the relative part can start with <c>`\xff`</c>, consider using <see cref="NextSibling"/> instead.</para>
		/// </remarks>
		/// <seealso cref="NextSibling"/>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static FdbLastKey<TKey> Last<TKey>(this TKey key)
			where TKey : struct, IFdbKey
			=> new(key);

		/// <summary>Returns a key that increments the last byte to get the key that is immediately after all the keys in the database that have the current key has a prefix (ex: <c>`abc`</c> => <c>`abd`</c>, <c>123</c> => <c>124</c>)</summary>
		/// <typeparam name="TKey">Type of the key</typeparam>
		/// <param name="key">Input key</param>
		/// <returns>Key that, when encoded into binary, will not have <paramref name="key"/> as its prefix</returns>
		/// <remarks>
		/// <para>This can be used to produce the "end" key for a range read that will return all the values under this key.</para>
		/// <para>For example, <c>tr.GetRange(subspace.GetKey(123), subspace.GetKey(123).GetNextSibling())</c> will be equivalent to <c>tr.GetRange(subspace.GetKey(123), subspace.GetKey(124))</c></para>
		/// <para>Please be careful when using this with tuples where the last elements is a <see cref="string"/> or <see cref="Slice"/>: the encoding adds an extra <c>0x00</c> bytes after the element (ex: <c>"hello"</c> => <c>`\x02hello\x00`</c>), which means that its successor (<c>`\x02hello\x01`</c>) will not be a valid tuple encoding, but will still be valid as an 'end' key.</para>
		/// <para>It should be combined with <see cref="FdbKeySelector.FirstGreaterOrEqual{TKey}"/> to produce a valid 'end' selector for a range read</para>
		/// </remarks>
		/// <seealso cref="FdbKeySelector.FirstGreaterOrEqual{TKey}"/>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static FdbNextSiblingKey<TKey> NextSibling<TKey>(this TKey key)
			where TKey : struct, IFdbKey
			=> new(key);

		/// <summary>Returns a range that will all the children of this key</summary>
		/// <typeparam name="TKey">Type of the key</typeparam>
		/// <param name="key">Key that will be used as a prefix</param>
		/// <param name="inclusive">If <c>true</c> the key itself will be included in the range; otherwise, the range will start immediately after this key.</param>
		/// <returns>Range that matches all keys that start with <paramref name="key"/> (included)</returns>
		/// <para>Ex: <c>subspace.GetKey(123).StartsWith()</c> will match all the keys of the form <c>(..., 123, ...)</c>, but not <c>(..., 123)</c> itself.</para>
		/// <para>Ex: <c>subspace.GetKey(123).StartsWith(inclusive: true)</c> will match <c>(..., 123)</c> as well as all the keys of the form <c>(..., 123, ...)</c>.</para>
		/// <para>Please be careful when using this with tuples where the last elements is a <see cref="string"/> or <see cref="Slice"/>: the encoding adds an extra <c>0x00</c> bytes after the element (ex: <c>"hello"</c> => <c>`\x02hello\x00`</c>), which means that <c>(..., "abc")</c> is <b>NOT</b> a child of <c>(..., "ab")</c></para>
		public static FdbKeyPrefixRange<TKey> ToRange<TKey>(this TKey key, bool inclusive = false)
			where TKey : struct, IFdbKey
			=> new(key, inclusive);

		#endregion

		#region Selectors...

		/// <summary>Creates a key selector that will select the last key in the database that is less than this <paramref name="key"/></summary>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static FdbKeySelector<TKey> LastLessThan<TKey>(this TKey key)
			where TKey : struct, IFdbKey =>
			FdbKeySelector.LastLessThan(in key);

		/// <summary>Creates a key selector that will select the last key in the database that is less than or equal to this <paramref name="key"/></summary>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static FdbKeySelector<TKey> LastLessOrEqual<TKey>(this TKey key)
			where TKey : struct, IFdbKey =>
			FdbKeySelector.LastLessOrEqual(in key);

		/// <summary>Creates a key selector that will select the first key in the database that is greater than this <paramref name="key"/></summary>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static FdbKeySelector<TKey> FirstGreaterThan<TKey>(this TKey key)
			where TKey : struct, IFdbKey =>
			FdbKeySelector.FirstGreaterThan(in key);

		/// <summary>Creates a key selector that will select the first key in the database that is greater than or equal to this <paramref name="key"/></summary>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static FdbKeySelector<TKey> FirstGreaterOrEqual<TKey>(this TKey key)
			where TKey : struct, IFdbKey =>
			FdbKeySelector.FirstGreaterOrEqual(in key);

		#endregion

		#region ToSubspace...

		/// <summary>Returns a new subspace that will use this key as its prefix</summary>
		/// <typeparam name="TKey"></typeparam>
		/// <param name="key"></param>
		/// <returns></returns>
		public static IKeySubspace ToSubspace<TKey>(this TKey key)
			where TKey : struct, IFdbKey
		{
			var parent = key.GetSubspace();

			// the key is tied to the subspace context
			var context = parent?.Context ?? SubspaceContext.Default;
			context.EnsureIsValid();

			return new KeySubspace(FdbKeyHelpers.ToSlice(in key), context);
		}

		#endregion

		#region Key...

		#region IKeySubspace.Key(...)...

		/// <summary>Returns the root key of this subspace</summary>
		/// <param name="subspace">Subspace to use as the source</param>
		/// <returns>Key that is equal to the prefix of the subspace</returns>
		/// <remarks>
		/// <para>This key can be used to create ranges that match all keys that could be located in this subspace</para>
		/// </remarks>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		[EditorBrowsable(EditorBrowsableState.Always)]
		public static FdbSubspaceKey Key(this IKeySubspace subspace) => new(subspace);

		/// <summary>Returns a key that represents the first possible child of this subspace</summary>
		/// <param name="subspace">Subspace to use as the source</param>
		/// <returns>Key that is equal to the prefix of the subspace with an extra <c>`\x00`</c> byte at the end</returns>
		/// <remarks>
		/// <para>This key can be used to create ranges that match all keys that could be located in this subspace, except the subspace prefix itself.</para>
		/// <para>This is equivalent to <c>subspace.Key(null)</c>, since <c>null</c> is encoded as <c>`\x00`</c> as well.</para>
		/// </remarks>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static FdbSuccessorKey<FdbSubspaceKey> First(this IKeySubspace subspace)
			=> new(new(subspace));

		/// <summary>Returns the last legal key of this subspace that can be expressed using tuples.</summary>
		/// <param name="subspace">Subspace to use as the source</param>
		/// <returns>Key that is equal to the prefix of the subspace with an extra <c>`\xFF`</c> byte at the end</returns>
		/// <remarks>
		/// <para>This key can be used to match all tuple-based keys that could be located in this subspace. If the keys could include any arbitrary binary sequence, especially if the first byte of the part of the relative part can start with <c>`\xff`</c>, consider using <see cref="NextSibling"/> instead.</para>
		/// </remarks>
		/// <seealso cref="NextSibling"/>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static FdbLastKey<FdbSubspaceKey> Last(this IKeySubspace subspace)
			=> new(new(subspace));

		/// <summary>Returns a key that is the next possible key that is not a child of this subspace</summary>
		/// <param name="subspace">Input subspace</param>
		/// <returns>Key that, when encoded into binary, is equal to the prefix of subspace with the last byte incremented (with carry propagation)</returns>
		/// <remarks>
		/// <para>This can be used to produce the "end" key for a range read that will return all the values under this key, including values not generated with the Tuple Encoding (that use <c>`\xFF`</c> as possible byte prefix).</para>
		/// <para>For example, <c>tr.GetRange(subspace.GetKey(123), subspace.GetNextSibling())</c> will be equivalent to <c>tr.GetRange(subspace.GetPrefix() + TuPack.EncodeKey(123), FdbKey.Increment(subspace.GetPrefix()))</c></para>
		/// <para>Please note that this key technically "belongs" to another subspace, which could influence the current transaction with false "conflicts". When all keys in the subspace are produced using the Tuple Encoding, consider using <see cref="Last"/> instead.</para>
		/// </remarks>
		/// <seealso cref="Last"/>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static FdbNextSiblingKey<FdbSubspaceKey> NextSibling(this IKeySubspace subspace)
			=> new(new(subspace));

		/// <summary>Returns a key under this subspace</summary>
		/// <param name="subspace">Subspace that contains the key</param>
		/// <param name="item1">value of the single element in the key</param>
		/// <remarks>
		/// <para>Example:<code>
		/// // reads the document for 'user123' under this subspace
		/// var value = await tr.GetAsync(subspace.GetKey("user123"));</code></para>
		/// <para>Please remember that if you pass a tuple as argument to this method, it will be added as an <i>embedded</i> tuple, which may not be what you expect! To generate a key from items already stored in a tuple, use <see cref="Tuple{T1}(IKeySubspace,STuple{T1})"/> instead.</para>
		/// </remarks>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static FdbTupleKey<T1> Key<T1>(this IKeySubspace subspace, T1 item1) => new(subspace, item1);

		/// <summary>Returns a key with 2 elements under this subspace</summary>
		/// <param name="subspace">Subspace that contains the key</param>
		/// <param name="item1">value of the 1st element in the key</param>
		/// <param name="item2">value of the 2nd element in the key</param>
		/// <remarks>
		/// <para>Example:<code>
		/// const int DOCUMENTS = 0; // subsection that store the JSON encoded documents
		/// const int VERSIONS = 1;  // subsection that store the versions of the documents
		/// tr.Set(subspace.GetKey(DOCUMENTS, "user123"), jsonBytes);
		/// tr.AtomicIncrement64(subspace.GetKey(VERSIONS, "user123"));</code></para>
		/// <para>Please remember that if you pass a tuple as argument to this method, it will be added as an <i>embedded</i> tuple, which may not be what you expect! To generate a key from items already stored in a tuple, use <see cref="Tuple{T1,T2}(IKeySubspace,in STuple{T1,T2})"/> instead.</para>
		/// </remarks>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static FdbTupleKey<T1, T2> Key<T1, T2>(this IKeySubspace subspace, T1 item1, T2 item2) => new(subspace, item1, item2);

		/// <summary>Returns a key with 3 elements under this subspace</summary>
		/// <param name="subspace">Subspace that contains the key</param>
		/// <param name="item1">value of the 1st element in the key</param>
		/// <param name="item2">value of the 2nd element in the key</param>
		/// <param name="item3">value of the 3rd element in the key</param>
		/// <remarks>
		/// <para>Example: <code>
		/// const INDEX_BY_CITY = 2; // subsection that stores a non-unique index of users by their city name.
		/// tr.Set(subspace.GetKey(INDEX_BY_CITY, "Tokyo", "user123"), Slice.Empty)</code></para>
		/// <para>Please remember that if you pass a tuple as argument to this method, it will be added as an <i>embedded</i> tuple, which may not be what you expect! To generate a key from items already stored in a tuple, use <see cref="Tuple{T1,T2,T3}(IKeySubspace,in STuple{T1,T2,T3})"/> instead.</para>
		/// </remarks>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static FdbTupleKey<T1, T2, T3> Key<T1, T2, T3>(this IKeySubspace subspace, T1 item1, T2 item2, T3 item3) => new(subspace, item1, item2, item3);

		/// <summary>Returns a key with 4 elements under this subspace</summary>
		/// <param name="subspace">Subspace that contains the key</param>
		/// <param name="item1">value of the 1st element in the key</param>
		/// <param name="item2">value of the 2nd element in the key</param>
		/// <param name="item3">value of the 3rd element in the key</param>
		/// <param name="item4">value of the 4th element in the key</param>
		/// <remarks>
		/// <para>Please remember that if you pass a tuple as argument to this method, it will be added as an <i>embedded</i> tuple, which may not be what you expect! To generate a key from items already stored in a tuple, use <see cref="Tuple{T1,T2,T3,T4}(IKeySubspace,in STuple{T1,T2,T3,T4})"/> instead.</para>
		/// </remarks>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static FdbTupleKey<T1, T2, T3, T4> Key<T1, T2, T3, T4>(this IKeySubspace subspace, T1 item1, T2 item2, T3 item3, T4 item4) => new(subspace, item1, item2, item3, item4);

		/// <summary>Returns a key with 5 elements under this subspace</summary>
		/// <param name="subspace">Subspace that contains the key</param>
		/// <param name="item1">value of the 1st element in the key</param>
		/// <param name="item2">value of the 2nd element in the key</param>
		/// <param name="item3">value of the 3rd element in the key</param>
		/// <param name="item4">value of the 4th element in the key</param>
		/// <param name="item5">value of the 5th element in the key</param>
		/// <remarks>
		/// <para>Please remember that if you pass a tuple as argument to this method, it will be added as an <i>embedded</i> tuple, which may not be what you expect! To generate a key from items already stored in a tuple, use <see cref="Tuple{T1,T2,T3,T4,T5}(IKeySubspace,in STuple{T1,T2,T3,T4,T5})"/> instead.</para>
		/// </remarks>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static FdbTupleKey<T1, T2, T3, T4, T5> Key<T1, T2, T3, T4, T5>(this IKeySubspace subspace, T1 item1, T2 item2, T3 item3, T4 item4, T5 item5) => new(subspace, item1, item2, item3, item4, item5);

		/// <summary>Returns a key with 6 elements under this subspace</summary>
		/// <param name="subspace">Subspace that contains the key</param>
		/// <param name="item1">value of the 1st element in the key</param>
		/// <param name="item2">value of the 2nd element in the key</param>
		/// <param name="item3">value of the 3rd element in the key</param>
		/// <param name="item4">value of the 4th element in the key</param>
		/// <param name="item5">value of the 5th element in the key</param>
		/// <param name="item6">value of the 6th element in the key</param>
		/// <remarks>
		/// <para>Please remember that if you pass a tuple as argument to this method, it will be added as an <i>embedded</i> tuple, which may not be what you expect! To generate a key from items already stored in a tuple, use <see cref="Tuple{T1,T2,T3,T4,T5,T6}(IKeySubspace,in STuple{T1,T2,T3,T4,T5,T6})"/> instead.</para>
		/// </remarks>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static FdbTupleKey<T1, T2, T3, T4, T5, T6> Key<T1, T2, T3, T4, T5, T6>(this IKeySubspace subspace, T1 item1, T2 item2, T3 item3, T4 item4, T5 item5, T6 item6) => new(subspace, item1, item2, item3, item4, item5, item6);

		/// <summary>Returns a key with 7 elements under this subspace</summary>
		/// <param name="subspace">Subspace that contains the key</param>
		/// <param name="item1">value of the 1st element in the key</param>
		/// <param name="item2">value of the 2nd element in the key</param>
		/// <param name="item3">value of the 3rd element in the key</param>
		/// <param name="item4">value of the 4th element in the key</param>
		/// <param name="item5">value of the 5th element in the key</param>
		/// <param name="item6">value of the 6th element in the key</param>
		/// <param name="item7">value of the 7th element in the key</param>
		/// <remarks>
		/// <para>Please remember that if you pass a tuple as argument to this method, it will be added as an <i>embedded</i> tuple, which may not be what you expect! To generate a key from items already stored in a tuple, use <see cref="Tuple{T1,T2,T3,T4,T5,T6,T7}(IKeySubspace,in STuple{T1,T2,T3,T4,T5,T6,T7})"/> instead.</para>
		/// </remarks>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static FdbTupleKey<T1, T2, T3, T4, T5, T6, T7> Key<T1, T2, T3, T4, T5, T6, T7>(this IKeySubspace subspace, T1 item1, T2 item2, T3 item3, T4 item4, T5 item5, T6 item6, T7 item7) => new(subspace, item1, item2, item3, item4, item5, item6, item7);

		/// <summary>Returns a key with 8 elements under this subspace</summary>
		/// <param name="subspace">Subspace that contains the key</param>
		/// <param name="item1">value of the 1st element in the key</param>
		/// <param name="item2">value of the 2nd element in the key</param>
		/// <param name="item3">value of the 3rd element in the key</param>
		/// <param name="item4">value of the 4th element in the key</param>
		/// <param name="item5">value of the 5th element in the key</param>
		/// <param name="item6">value of the 6th element in the key</param>
		/// <param name="item7">value of the 7th element in the key</param>
		/// <param name="item8">value of the 8th element in the key</param>
		/// <remarks>
		/// <para>Please remember that if you pass a tuple as argument to this method, it will be added as an <i>embedded</i> tuple, which may not be what you expect! To generate a key from items already stored in a tuple, use <see cref="Tuple{T1,T2,T3,T4,T5,T6,T7,T8}(IKeySubspace,in STuple{T1,T2,T3,T4,T5,T6,T7,T8})"/> instead.</para>
		/// </remarks>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static FdbTupleKey<T1, T2, T3, T4, T5, T6, T7, T8> Key<T1, T2, T3, T4, T5, T6, T7, T8>(this IKeySubspace subspace, T1 item1, T2 item2, T3 item3, T4 item4, T5 item5, T6 item6, T7 item7, T8 item8) => new(subspace, item1, item2, item3, item4, item5, item6, item7, item8);

		#endregion

		#region IFdbKey.Key(...)...

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static FdbTupleSuffixKey<TKey, STuple<T1>> Key<TKey, T1>(this TKey key, T1 item1)
			where TKey : struct, IFdbKey
			=> new(key, new(item1));

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static FdbTupleSuffixKey<TKey, STuple<T1, T2>> Key<TKey, T1, T2>(this TKey key, T1 item1, T2 item2)
			where TKey : struct, IFdbKey
			=> new(key, new(item1, item2));

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static FdbTupleSuffixKey<TKey, STuple<T1, T2, T3>> Key<TKey, T1, T2, T3>(this TKey key, T1 item1, T2 item2, T3 item3)
			where TKey : struct, IFdbKey
			=> new(key, new(item1, item2, item3));

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static FdbTupleSuffixKey<TKey, STuple<T1, T2, T3, T4>> Key<TKey, T1, T2, T3, T4>(this TKey key, T1 item1, T2 item2, T3 item3, T4 item4)
			where TKey : struct, IFdbKey
			=> new(key, new(item1, item2, item3, item4));

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static FdbTupleSuffixKey<TKey, STuple<T1, T2, T3, T4, T5>> Key<TKey, T1, T2, T3, T4, T5>(this TKey key, T1 item1, T2 item2, T3 item3, T4 item4, T5 item5)
			where TKey : struct, IFdbKey
			=> new(key, new(item1, item2, item3, item4, item5));

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static FdbTupleSuffixKey<TKey, STuple<T1, T2, T3, T4, T5, T6>> Key<TKey, T1, T2, T3, T4, T5, T6>(this TKey key, T1 item1, T2 item2, T3 item3, T4 item4, T5 item5, T6 item6)
			where TKey : struct, IFdbKey
			=> new(key, new(item1, item2, item3, item4, item5, item6));

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static FdbTupleSuffixKey<TKey, STuple<T1, T2, T3, T4, T5, T6, T7>> Key<TKey, T1, T2, T3, T4, T5, T6, T7>(this TKey key, T1 item1, T2 item2, T3 item3, T4 item4, T5 item5, T6 item6, T7 item7)
			where TKey : struct, IFdbKey
			=> new(key, new(item1, item2, item3, item4, item5, item6, item7));

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static FdbTupleSuffixKey<TKey, STuple<T1, T2, T3, T4, T5, T6, T7, T8>> Key<TKey, T1, T2, T3, T4, T5, T6, T7, T8>(this TKey key, T1 item1, T2 item2, T3 item3, T4 item4, T5 item5, T6 item6, T7 item7, T8 item8)
			where TKey : struct, IFdbKey
			=> new(key, new(item1, item2, item3, item4, item5, item6, item7, item8));

		#endregion

		#endregion

		#region Tuple...

		#region IKeySubspace.Tuple(STuple<...>)

		/// <summary>Returns a key that appends the packed items of the given tuple to subspace's prefix</summary>
		/// <param name="subspace">Subspace that contains the key</param>
		/// <param name="items">Elements of the key</param>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static FdbTupleKey Tuple(this IKeySubspace subspace, IVarTuple items) => new(subspace, items);

		/// <summary>Returns a key that appends the packed items of the given tuple to subspace's prefix</summary>
		/// <param name="subspace">Subspace that contains the key</param>
		/// <param name="items">elements of the key</param>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static FdbTupleKey<T1> Tuple<T1>(this IKeySubspace subspace, STuple<T1> items) => new(subspace, in items);

		/// <summary>Returns a key that appends the packed items of the given tuple to subspace's prefix</summary>
		/// <param name="subspace">Subspace that contains the key</param>
		/// <param name="items">elements of the key</param>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static FdbTupleKey<T1, T2> Tuple<T1, T2>(this IKeySubspace subspace, in STuple<T1, T2> items) => new(subspace, in items);

		/// <summary>Returns a key that appends the packed items of the given tuple to subspace's prefix</summary>
		/// <param name="subspace">Subspace that contains the key</param>
		/// <param name="items">elements of the key</param>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static FdbTupleKey<T1, T2, T3> Tuple<T1, T2, T3>(this IKeySubspace subspace, in STuple<T1, T2, T3> items) => new(subspace, in items);

		/// <summary>Returns a key that appends the packed items of the given tuple to subspace's prefix</summary>
		/// <param name="subspace">Subspace that contains the key</param>
		/// <param name="items">elements of the key</param>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static FdbTupleKey<T1, T2, T3, T4> Tuple<T1, T2, T3, T4>(this IKeySubspace subspace, in STuple<T1, T2, T3, T4> items) => new(subspace, in items);

		/// <summary>Returns a key that appends the packed items of the given tuple to subspace's prefix</summary>
		/// <param name="subspace">Subspace that contains the key</param>
		/// <param name="items">elements of the key</param>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static FdbTupleKey<T1, T2, T3, T4, T5> Tuple<T1, T2, T3, T4, T5>(this IKeySubspace subspace, in STuple<T1, T2, T3, T4, T5> items) => new(subspace, in items);

		/// <summary>Returns a key that appends the packed items of the given tuple to subspace's prefix</summary>
		/// <param name="subspace">Subspace that contains the key</param>
		/// <param name="items">elements of the key</param>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static FdbTupleKey<T1, T2, T3, T4, T5, T6> Tuple<T1, T2, T3, T4, T5, T6>(this IKeySubspace subspace, in STuple<T1, T2, T3, T4, T5, T6> items) => new(subspace, in items);

		/// <summary>Returns a key that appends the packed items of the given tuple to subspace's prefix</summary>
		/// <param name="subspace">Subspace that contains the key</param>
		/// <param name="items">elements of the key</param>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static FdbTupleKey<T1, T2, T3, T4, T5, T6, T7> Tuple<T1, T2, T3, T4, T5, T6, T7>(this IKeySubspace subspace, in STuple<T1, T2, T3, T4, T5, T6, T7> items) => new(subspace, in items);

		/// <summary>Returns a key that appends the packed items of the given tuple to subspace's prefix</summary>
		/// <param name="subspace">Subspace that contains the key</param>
		/// <param name="items">elements of the key</param>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static FdbTupleKey<T1, T2, T3, T4, T5, T6, T7, T8> Tuple<T1, T2, T3, T4, T5, T6, T7, T8>(this IKeySubspace subspace, in STuple<T1, T2, T3, T4, T5, T6, T7, T8> items) => new(subspace, in items);

		#endregion

		#region IKeySubspace.Tuple(ValueTuple<...>)

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static FdbTupleKey<T1> Tuple<T1>(this IKeySubspace subspace, ValueTuple<T1> items) => new(subspace, items.Item1);

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static FdbTupleKey<T1, T2> Tuple<T1, T2>(this IKeySubspace subspace, in ValueTuple<T1, T2> items) => new(subspace, items.Item1, items.Item2);

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static FdbTupleKey<T1, T2, T3> Tuple<T1, T2, T3>(this IKeySubspace subspace, in ValueTuple<T1, T2, T3> items) => new(subspace, items.Item1, items.Item2, items.Item3);

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static FdbTupleKey<T1, T2, T3, T4> Tuple<T1, T2, T3, T4>(this IKeySubspace subspace, in ValueTuple<T1, T2, T3, T4> items) => new(subspace, items.Item1, items.Item2, items.Item3, items.Item4);

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static FdbTupleKey<T1, T2, T3, T4, T5> Tuple<T1, T2, T3, T4, T5>(this IKeySubspace subspace, in ValueTuple<T1, T2, T3, T4, T5> items) => new(subspace, items.Item1, items.Item2, items.Item3, items.Item4, items.Item5);

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static FdbTupleKey<T1, T2, T3, T4, T5, T6> Tuple<T1, T2, T3, T4, T5, T6>(this IKeySubspace subspace, in ValueTuple<T1, T2, T3, T4, T5, T6> items) => new(subspace, items.Item1, items.Item2, items.Item3, items.Item4, items.Item5, items.Item6);

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static FdbTupleKey<T1, T2, T3, T4, T5, T6, T7> Tuple<T1, T2, T3, T4, T5, T6, T7>(this IKeySubspace subspace, in ValueTuple<T1, T2, T3, T4, T5, T6, T7> items) => new(subspace, items.Item1, items.Item2, items.Item3, items.Item4, items.Item5, items.Item6, items.Item7);

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static FdbTupleKey<T1, T2, T3, T4, T5, T6, T7, T8> Tuple<T1, T2, T3, T4, T5, T6, T7, T8>(this IKeySubspace subspace, in ValueTuple<T1, T2, T3, T4, T5, T6, T7, ValueTuple<T8>> items) => new(subspace, items.Item1, items.Item2, items.Item3, items.Item4, items.Item5, items.Item6, items.Item7, items.Item8);

		#endregion

		#region IFdbKey.Tuple(STuple<...>)

		/// <summary>Appends the packed elements of a tuple after the current key</summary>
		/// <typeparam name="TKey">Type of the parent key</typeparam>
		/// <typeparam name="TTuple">Type of the tuple</typeparam>
		/// <param name="key">Parent key</param>
		/// <param name="items">Tuples with the items to append</param>
		/// <returns>New key that will append the <paramref name="items"/> at the end of the current <paramref name="key"/></returns>
		public static FdbTupleSuffixKey<TKey, TTuple> Tuple<TKey, TTuple>(this TKey key, TTuple items)
			where TKey : struct, IFdbKey
			where TTuple : IVarTuple
		{
			return new(key, items);
		}

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static FdbTupleSuffixKey<TKey, STuple<T1>> Tuple<TKey, T1>(this TKey key, in STuple<T1> items)
			where TKey : struct, IFdbKey
			=> new(key, in items);

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static FdbTupleSuffixKey<TKey, STuple<T1, T2>> Tuple<TKey, T1, T2>(this TKey key, in STuple<T1, T2> items)
			where TKey : struct, IFdbKey
			=> new(key, in items);

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static FdbTupleSuffixKey<TKey, STuple<T1, T2, T3>> Tuple<TKey, T1, T2, T3>(this TKey key, in STuple<T1, T2, T3> items)
			where TKey : struct, IFdbKey
			=> new(key, in items);

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static FdbTupleSuffixKey<TKey, STuple<T1, T2, T3, T4>> Tuple<TKey, T1, T2, T3, T4>(this TKey key, in STuple<T1, T2, T3, T4> items)
			where TKey : struct, IFdbKey
			=> new(key, in items);

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static FdbTupleSuffixKey<TKey, STuple<T1, T2, T3, T4, T5>> Tuple<TKey, T1, T2, T3, T4, T5>(this TKey key, in STuple<T1, T2, T3, T4, T5> items)
			where TKey : struct, IFdbKey
			=> new(key, in items);

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static FdbTupleSuffixKey<TKey, STuple<T1, T2, T3, T4, T5, T6>> Tuple<TKey, T1, T2, T3, T4, T5, T6>(this TKey key, in STuple<T1, T2, T3, T4, T5, T6> items)
			where TKey : struct, IFdbKey
			=> new(key, in items);

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static FdbTupleSuffixKey<TKey, STuple<T1, T2, T3, T4, T5, T6, T7>> Tuple<TKey, T1, T2, T3, T4, T5, T6, T7>(this TKey key, in STuple<T1, T2, T3, T4, T5, T6, T7> items)
			where TKey : struct, IFdbKey
			=> new(key, in items);

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static FdbTupleSuffixKey<TKey, STuple<T1, T2, T3, T4, T5, T6, T7, T8>> Tuple<TKey, T1, T2, T3, T4, T5, T6, T7, T8>(this TKey key, in STuple<T1, T2, T3, T4, T5, T6, T7, T8> items)
			where TKey : struct, IFdbKey
			=> new(key, in items);

		#endregion

		#region IFdbKey.Tuple(ValueTuple<...>)

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static FdbTupleSuffixKey<TKey, STuple<T1>> Tuple<TKey, T1>(this TKey key, in ValueTuple<T1> items)
			where TKey : struct, IFdbKey
			=> new(key, new(items.Item1));

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static FdbTupleSuffixKey<TKey, STuple<T1, T2>> Tuple<TKey, T1, T2>(this TKey key, in ValueTuple<T1, T2> items)
			where TKey : struct, IFdbKey
			=> new(key, new(items.Item1, items.Item2));

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static FdbTupleSuffixKey<TKey, STuple<T1, T2, T3>> Tuple<TKey, T1, T2, T3>(this TKey key, in ValueTuple<T1, T2, T3> items)
			where TKey : struct, IFdbKey
			=> new(key, new(items.Item1, items.Item2, items.Item3));

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static FdbTupleSuffixKey<TKey, STuple<T1, T2, T3, T4>> Tuple<TKey, T1, T2, T3, T4>(this TKey key, in ValueTuple<T1, T2, T3, T4> items)
			where TKey : struct, IFdbKey
			=> new(key, new(items.Item1, items.Item2, items.Item3, items.Item4));

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static FdbTupleSuffixKey<TKey, STuple<T1, T2, T3, T4, T5>> Tuple<TKey, T1, T2, T3, T4, T5>(this TKey key, in ValueTuple<T1, T2, T3, T4, T5> items)
			where TKey : struct, IFdbKey
			=> new(key, new(items.Item1, items.Item2, items.Item3, items.Item4, items.Item5));

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static FdbTupleSuffixKey<TKey, STuple<T1, T2, T3, T4, T5, T6>> Tuple<TKey, T1, T2, T3, T4, T5, T6>(this TKey key, in ValueTuple<T1, T2, T3, T4, T5, T6> items)
			where TKey : struct, IFdbKey
			=> new(key, new(items.Item1, items.Item2, items.Item3, items.Item4, items.Item5, items.Item6));

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static FdbTupleSuffixKey<TKey, STuple<T1, T2, T3, T4, T5, T6, T7>> Tuple<TKey, T1, T2, T3, T4, T5, T6, T7>(this TKey key, in ValueTuple<T1, T2, T3, T4, T5, T6, T7> items)
			where TKey : struct, IFdbKey
			=> new(key, new(items.Item1, items.Item2, items.Item3, items.Item4, items.Item5, items.Item6, items.Item7));

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static FdbTupleSuffixKey<TKey, STuple<T1, T2, T3, T4, T5, T6, T7, T8>> Tuple<TKey, T1, T2, T3, T4, T5, T6, T7, T8>(this TKey key, in ValueTuple<T1, T2, T3, T4, T5, T6, T7, ValueTuple<T8>> items)
			where TKey : struct, IFdbKey
			=> new(key, new(items.Item1, items.Item2, items.Item3, items.Item4, items.Item5, items.Item6, items.Item7, items.Item8));

		#endregion

		#endregion

		#region Bytes...

		#region IKeySubspace.Bytes(...)

		/// <summary>Returns a key that appends a binary suffix to the subspace's prefix</summary>
		/// <param name="subspace">Parent subspace</param>
		/// <param name="suffix">Binary suffix</param>
		/// <returns>Key that will output the subspace prefix, followed by the binary suffix</returns>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static FdbSuffixKey Bytes(this IKeySubspace subspace, ReadOnlySpan<byte> suffix)
		{
			Contract.NotNull(subspace);
			return new(subspace, Slice.FromBytes(suffix));
		}

		/// <summary>Returns a key that appends a binary suffix to the subspace's prefix</summary>
		/// <param name="subspace">Parent subspace</param>
		/// <param name="suffix">Binary suffix</param>
		/// <returns>Key that will output the subspace prefix, followed by the binary suffix</returns>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static FdbSuffixKey Bytes(this IKeySubspace subspace, Slice suffix)
		{
			Contract.NotNull(subspace);
			return new(subspace, suffix);
		}

		/// <summary>Returns a key that appends a binary suffix to the subspace's prefix</summary>
		/// <param name="subspace">Parent subspace</param>
		/// <param name="suffix">Binary suffix</param>
		/// <returns>Key that will output the subspace prefix, followed by the binary suffix</returns>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static FdbSuffixKey Bytes(this IKeySubspace subspace, byte[]? suffix)
		{
			Contract.NotNull(subspace);
			return new(subspace, suffix.AsSlice());
		}

		/// <summary>Returns a key that appends a string encoded as UTF-8 to the subspace's prefix</summary>
		/// <param name="subspace">Parent subspace</param>
		/// <param name="suffix">Binary suffix</param>
		/// <returns>Key that will output the subspace prefix, followed by the binary suffix</returns>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static FdbSuffixKey Bytes(this IKeySubspace subspace, string suffix)
		{
			Contract.NotNull(subspace);
			Contract.NotNull(suffix);
			return new(subspace, Slice.FromStringUtf8(suffix));
		}

		#endregion

		#region IFdbKey.Bytes(...)

		/// <summary>Returns a key that appends a binary suffix to the current key</summary>
		/// <param name="key">Parent key</param>
		/// <param name="suffix">Binary suffix</param>
		/// <returns>Key that will append the binary suffix to the current key</returns>
		/// <remarks>Please note that this will generate a different encoding than calling <see cref="Key{Slice}"/> with a slice (which uses the Tuple Encoding).</remarks>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static FdbSuffixKey<TKey> Bytes<TKey>(this TKey key, ReadOnlySpan<byte> suffix)
			where TKey : struct, IFdbKey
			=> new(key, Slice.FromBytes(suffix));

		/// <summary>Returns a key that appends a binary suffix to the current key</summary>
		/// <param name="key">Parent key</param>
		/// <param name="suffix">Binary suffix</param>
		/// <returns>Key that will append the binary suffix to the current key</returns>
		/// <remarks>Please note that this will generate a different encoding than calling <see cref="Key{Slice}"/> with a slice (which uses the Tuple Encoding).</remarks>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static FdbSuffixKey<TKey> Bytes<TKey>(this TKey key, Slice suffix)
			where TKey : struct, IFdbKey
			=> new(key, suffix);

		/// <summary>Returns a key that appends a binary suffix to the current key</summary>
		/// <param name="key">Parent key</param>
		/// <param name="suffix">Binary suffix</param>
		/// <returns>Key that will append the binary suffix to the current key</returns>
		/// <remarks>Please note that this will generate a different encoding than calling <see cref="Key{Slice}"/> with a slice (which uses the Tuple Encoding).</remarks>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static FdbSuffixKey<TKey> Bytes<TKey>(this TKey key, byte[]? suffix)
			where TKey : struct, IFdbKey
			=> new(key, suffix.AsSlice());

		/// <summary>Returns a key that appends a string encoded as UTF-8 to the current key</summary>
		/// <param name="key">Parent key</param>
		/// <param name="suffix">Suffix string, encoded as UTF-8 bytes</param>
		/// <returns>Key that will append the binary suffix to the current key</returns>
		/// <remarks>Please note that this will generate a different encoding than calling <see cref="Key{string}"/> with a string (which uses the Tuple Encoding).</remarks>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static FdbSuffixKey<TKey> Bytes<TKey>(this TKey key, string suffix)
			where TKey : struct, IFdbKey
			=> new(key, Slice.FromStringUtf8(suffix));

		#endregion

		#endregion

		#region Encoding...

		/// <summary>Encodes this key into <see cref="Slice"/></summary>
		/// <param name="key">Key to encode</param>
		/// <returns><see cref="Slice"/> that contains the binary representation of this key</returns>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static Slice ToSlice<TKey>(this TKey key) 
			where TKey : struct, IFdbKey
			=> FdbKeyHelpers.ToSlice(in key);

		/// <summary>Encodes this key into <see cref="Slice"/>, using backing buffer rented from a pool</summary>
		/// <param name="key">Key to encode</param>
		/// <param name="pool">Pool used to rent the buffer (<see cref="ArrayPool{T}.Shared"/> is <c>null</c>)</param>
		/// <returns><see cref="SliceOwner"/> that contains the binary representation of this key</returns>
		[Pure, MustDisposeResource, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static SliceOwner ToSlice<TKey>(this TKey key, ArrayPool<byte>? pool)
			where TKey : struct, IFdbKey
			=> FdbKeyHelpers.ToSlice(in key, pool);

		/// <summary>Returns a pre-encoded version of a key</summary>
		/// <typeparam name="TKey">Type of the key to pre-encode</typeparam>
		/// <param name="key">Key to pre-encoded</param>
		/// <returns>Key with a cached version of the encoded original</returns>
		/// <remarks>This key can be used multiple times without re-encoding the original</remarks>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static FdbRawKey Memoize<TKey>(this TKey key)
			where TKey : struct, IFdbKey
		{
			if (typeof(TKey) == typeof(FdbRawKey))
			{ // already cached!
				return (FdbRawKey) (object) key;
			}
			return new(key.ToSlice());
		}

		#endregion

		/// <summary>Checks if the key is in the System keyspace (starts with <c>`\xFF`</c>)</summary>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool IsSystem<TKey>(this TKey key)
			where TKey : struct, IFdbKey
		{
			if (typeof(TKey) == typeof(FdbSystemKey))
			{
				return true;
			}
			return FdbKeyHelpers.IsSystem(in key);
		}

		#region Decode...

		//TODO: move this to FdbKeyExtensions

		/// <summary>Decodes a binary slice into a tuple of arbitrary length</summary>
		/// <returns>Tuple of any size (0 to N)</returns>
		public static IVarTuple Unpack(this IKeySubspace self, Slice packedKey) //REVIEW: consider changing return type to SlicedTuple ?
		{
			return TuPack.Unpack(self.ExtractKey(packedKey));
		}

		/// <summary>Decodes a binary slice into a tuple of arbitrary length</summary>
		/// <returns>Tuple of any size (0 to N)</returns>
		public static SpanTuple Unpack(this IKeySubspace self, ReadOnlySpan<byte> packedKey) //REVIEW: consider changing return type to SlicedTuple ?
		{
			return SpanTuple.Unpack(self.ExtractKey(packedKey));
		}

		/// <summary>Decode a key of this subspace, composed of a single element</summary>
		[MustUseReturnValue, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static T1? Decode<T1>(this IKeySubspace self, Slice packedKey)
			=> TuPack.DecodeKey<T1>(self.ExtractKey(packedKey));

		/// <summary>Decode a key of this subspace, composed of a single element</summary>
		[MustUseReturnValue, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static T1? Decode<T1>(this IKeySubspace self, ReadOnlySpan<byte> packedKey)
			=> TuPack.DecodeKey<T1>(self.ExtractKey(packedKey));

		/// <summary>Decode a key of this subspace, composed of exactly two elements</summary>
		[MustUseReturnValue, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static STuple<T1?, T2?> Decode<T1, T2>(this IKeySubspace self, Slice packedKey)
			=> TuPack.DecodeKey<T1, T2>(self.ExtractKey(packedKey));

		/// <summary>Decode a key of this subspace, composed of exactly two elements</summary>
		[MustUseReturnValue, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static STuple<T1?, T2?> Decode<T1, T2>(this IKeySubspace self, ReadOnlySpan<byte> packedKey)
			=> TuPack.DecodeKey<T1, T2>(self.ExtractKey(packedKey));

		/// <summary>Decode a key of this subspace, composed of exactly three elements</summary>
		[MustUseReturnValue, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static STuple<T1?, T2?, T3?> Decode<T1, T2, T3>(this IKeySubspace self, Slice packedKey)
			=> TuPack.DecodeKey<T1, T2, T3>(self.ExtractKey(packedKey));

		/// <summary>Decode a key of this subspace, composed of exactly three elements</summary>
		[MustUseReturnValue, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static STuple<T1?, T2?, T3?> Decode<T1, T2, T3>(this IKeySubspace self, ReadOnlySpan<byte> packedKey)
			=> TuPack.DecodeKey<T1, T2, T3>(self.ExtractKey(packedKey));

		/// <summary>Decode a key of this subspace, composed of exactly four elements</summary>
		[MustUseReturnValue, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static STuple<T1?, T2?, T3?, T4?> Decode<T1, T2, T3, T4>(this IKeySubspace self, Slice packedKey)
			=> TuPack.DecodeKey<T1, T2, T3, T4>(self.ExtractKey(packedKey));

		/// <summary>Decode a key of this subspace, composed of exactly four elements</summary>
		[MustUseReturnValue, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static STuple<T1?, T2?, T3?, T4?> Decode<T1, T2, T3, T4>(this IKeySubspace self, ReadOnlySpan<byte> packedKey)
			=> TuPack.DecodeKey<T1, T2, T3, T4>(self.ExtractKey(packedKey));

		/// <summary>Decode a key of this subspace, composed of exactly five elements</summary>
		[MustUseReturnValue, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static STuple<T1?, T2?, T3?, T4?, T5?> Decode<T1, T2, T3, T4, T5>(this IKeySubspace self, Slice packedKey)
			=> TuPack.DecodeKey<T1, T2, T3, T4, T5>(self.ExtractKey(packedKey));

		/// <summary>Decode a key of this subspace, composed of exactly five elements</summary>
		[MustUseReturnValue, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static STuple<T1?, T2?, T3?, T4?, T5?> Decode<T1, T2, T3, T4, T5>(this IKeySubspace self, ReadOnlySpan<byte> packedKey)
			=> TuPack.DecodeKey<T1, T2, T3, T4, T5>(self.ExtractKey(packedKey));

		/// <summary>Decode a key of this subspace, composed of exactly six elements</summary>
		[MustUseReturnValue, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static STuple<T1?, T2?, T3?, T4?, T5?, T6?> Decode<T1, T2, T3, T4, T5, T6>(this IKeySubspace self, Slice packedKey)
			=> TuPack.DecodeKey<T1, T2, T3, T4, T5, T6>(self.ExtractKey(packedKey));

		/// <summary>Decode a key of this subspace, composed of exactly six elements</summary>
		[MustUseReturnValue, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static STuple<T1?, T2?, T3?, T4?, T5?, T6?> Decode<T1, T2, T3, T4, T5, T6>(this IKeySubspace self, ReadOnlySpan<byte> packedKey)
			=> TuPack.DecodeKey<T1, T2, T3, T4, T5, T6>(self.ExtractKey(packedKey));

		/// <summary>Decode a key of this subspace, composed of exactly seven elements</summary>
		[MustUseReturnValue, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static STuple<T1?, T2?, T3?, T4?, T5?, T6?, T7?> Decode<T1, T2, T3, T4, T5, T6, T7>(this IKeySubspace self, Slice packedKey)
			=> TuPack.DecodeKey<T1, T2, T3, T4, T5, T6, T7>(self.ExtractKey(packedKey));

		/// <summary>Decode a key of this subspace, composed of exactly seven elements</summary>
		[MustUseReturnValue, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static STuple<T1?, T2?, T3?, T4?, T5?, T6?, T7?> Decode<T1, T2, T3, T4, T5, T6, T7>(this IKeySubspace self, ReadOnlySpan<byte> packedKey)
			=> TuPack.DecodeKey<T1, T2, T3, T4, T5, T6, T7>(self.ExtractKey(packedKey));

		/// <summary>Decode a key of this subspace, composed of exactly seven elements</summary>
		[MustUseReturnValue, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static STuple<T1?, T2?, T3?, T4?, T5?, T6?, T7?, T8?> Decode<T1, T2, T3, T4, T5, T6, T7, T8>(this IKeySubspace self, ReadOnlySpan<byte> packedKey)
			=> TuPack.DecodeKey<T1, T2, T3, T4, T5, T6, T7, T8>(self.ExtractKey(packedKey));

		/// <summary>Decode a key of this subspace, and return the element at the given index</summary>
		[MustUseReturnValue, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static T1? DecodeAt<T1>(this IKeySubspace self, Slice packedKey, int index)
			=> TuPack.DecodeKeyAt<T1>(self.ExtractKey(packedKey), index);

		/// <summary>Decode a key of this subspace, and return the element at the given index</summary>
		[MustUseReturnValue, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static T1? DecodeAt<T1>
			(this IKeySubspace self, ReadOnlySpan<byte> packedKey, int index)
			=> TuPack.DecodeKeyAt<T1>(self.ExtractKey(packedKey), index);

		/// <summary>Decode a key of this subspace, and return only the first element without decoding the rest the key.</summary>
		/// <remarks>This method is faster than unpacking the complete key and reading only the first element.</remarks>
		[MustUseReturnValue, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static T1? DecodeFirst<T1>
			(this IKeySubspace self, Slice packedKey, int? expectedSize = null)
			=> TuPack.DecodeFirst<T1>(self.ExtractKey(packedKey), expectedSize);

		/// <summary>Decode a key of this subspace, and return only the first element without decoding the rest the key.</summary>
		/// <remarks>This method is faster than unpacking the complete key and reading only the first element.</remarks>
		[MustUseReturnValue, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static T1? DecodeFirst<T1>
			(this IKeySubspace self, ReadOnlySpan<byte> packedKey, int? expectedSize = null)
			=> TuPack.DecodeFirst<T1>(self.ExtractKey(packedKey), expectedSize);

		/// <summary>Decode a key of this subspace, and return only the first two elements without decoding the rest the key.</summary>
		/// <remarks>This method is faster than unpacking the complete key and reading only two elements.</remarks>
		[MustUseReturnValue, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static (T1?, T2?) DecodeFirst<T1, T2>
			(this IKeySubspace self, Slice packedKey, int? expectedSize = null)
			=> TuPack.DecodeFirst<T1, T2>(self.ExtractKey(packedKey), expectedSize);

		/// <summary>Decode a key of this subspace, and return only the first two elements without decoding the rest the key.</summary>
		/// <remarks>This method is faster than unpacking the complete key and reading only two elements.</remarks>
		[MustUseReturnValue, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static (T1?, T2?) DecodeFirst<T1, T2>
			(this IKeySubspace self, ReadOnlySpan<byte> packedKey, int? expectedSize = null)
			=> TuPack.DecodeFirst<T1, T2>(self.ExtractKey(packedKey), expectedSize);

		/// <summary>Decode a key of this subspace, and return only the first three elements without decoding the rest the key.</summary>
		/// <remarks>This method is faster than unpacking the complete key and reading only three elements.</remarks>
		[MustUseReturnValue, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static (T1?, T2?, T3?) DecodeFirst<T1, T2, T3>
			(this IKeySubspace self, Slice packedKey, int? expectedSize = null)
			=> TuPack.DecodeFirst<T1, T2, T3>(self.ExtractKey(packedKey), expectedSize);

		/// <summary>Decode a key of this subspace, and return only the first three elements without decoding the rest the key.</summary>
		/// <remarks>This method is faster than unpacking the complete key and reading only three elements.</remarks>
		[MustUseReturnValue, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static (T1?, T2?, T3?) DecodeFirst<T1, T2, T3>
			(this IKeySubspace self, ReadOnlySpan<byte> packedKey, int? expectedSize = null)
			=> TuPack.DecodeFirst<T1, T2, T3>(self.ExtractKey(packedKey), expectedSize);

		/// <summary>Decode a key of this subspace, and return only the last element without decoding the rest.</summary>
		/// <remarks>This method is faster than unpacking the complete key and reading only the last element.</remarks>
		[MustUseReturnValue, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static T1? DecodeLast<T1>
			(this IKeySubspace self, Slice packedKey, int? expectedSize = null)
			=> TuPack.DecodeLast<T1>(self.ExtractKey(packedKey), expectedSize);

		/// <summary>Decode a key of this subspace, and return only the last element without decoding the rest.</summary>
		/// <remarks>This method is faster than unpacking the complete key and reading only the last element.</remarks>
		[MustUseReturnValue, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static T1? DecodeLast<T1>
			(this IKeySubspace self, ReadOnlySpan<byte> packedKey, int? expectedSize = null)
			=> TuPack.DecodeLast<T1>(self.ExtractKey(packedKey), expectedSize);

		/// <summary>Decode a key of this subspace, and return only the last two elements without decoding the rest.</summary>
		/// <remarks>This method is faster than unpacking the complete key and reading only the last elements.</remarks>
		[MustUseReturnValue, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static (T1?, T2?) DecodeLast<T1, T2>
			(this IKeySubspace self, Slice packedKey, int? expectedSize = null)
			=> TuPack.DecodeLast<T1, T2>(self.ExtractKey(packedKey), expectedSize);

		/// <summary>Decode a key of this subspace, and return only the last two elements without decoding the rest.</summary>
		/// <remarks>This method is faster than unpacking the complete key and reading only the last elements.</remarks>
		[MustUseReturnValue, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static (T1?, T2?) DecodeLast<T1, T2>
			(this IKeySubspace self, ReadOnlySpan<byte> packedKey, int? expectedSize = null)
			=> TuPack.DecodeLast<T1, T2>(self.ExtractKey(packedKey), expectedSize);

		/// <summary>Decode a key of this subspace, and return only the last three elements without decoding the rest.</summary>
		/// <remarks>This method is faster than unpacking the complete key and reading only the last elements.</remarks>
		[MustUseReturnValue, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static (T1?, T2?, T3?) DecodeLast<T1, T2, T3>
			(this IKeySubspace self, Slice packedKey, int? expectedSize = null)
			=> TuPack.DecodeLast<T1, T2, T3>(self.ExtractKey(packedKey), expectedSize);

		/// <summary>Decode a key of this subspace, and return only the last three elements without decoding the rest.</summary>
		/// <remarks>This method is faster than unpacking the complete key and reading only the last elements.</remarks>
		[MustUseReturnValue, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static (T1?, T2?, T3?) DecodeLast<T1, T2, T3>
			(this IKeySubspace self, ReadOnlySpan<byte> packedKey, int? expectedSize = null)
			=> TuPack.DecodeLast<T1, T2, T3>(self.ExtractKey(packedKey), expectedSize);

		/// <summary>Decode a key of this subspace, and return only the last three elements without decoding the rest.</summary>
		/// <remarks>This method is faster than unpacking the complete key and reading only the last elements.</remarks>
		[MustUseReturnValue, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static (T1?, T2?, T3?, T4?) DecodeLast<T1, T2, T3, T4>
			(this IKeySubspace self, Slice packedKey, int? expectedSize = null)
			=> TuPack.DecodeLast<T1, T2, T3, T4>(self.ExtractKey(packedKey), expectedSize);

		/// <summary>Decode a key of this subspace, and return only the last three elements without decoding the rest.</summary>
		/// <remarks>This method is faster than unpacking the complete key and reading only the last elements.</remarks>
		[MustUseReturnValue, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static (T1?, T2?, T3?, T4?) DecodeLast<T1, T2, T3, T4>
			(this IKeySubspace self, ReadOnlySpan<byte> packedKey, int? expectedSize = null)
			=> TuPack.DecodeLast<T1, T2, T3, T4>(self.ExtractKey(packedKey), expectedSize);

		#endregion

	}

	/// <summary>Helper methods used by <see cref="IFdbKey"/> implementations</summary>
	internal static class FdbKeyHelpers
	{

		/// <summary>Returns a pre-encoded version of a key</summary>
		/// <typeparam name="TKey">Type of the key to pre-encode</typeparam>
		/// <param name="key">Key to pre-encoded</param>
		/// <returns>Key with a cached version of the encoded original</returns>
		/// <remarks>This key can be used multiple times without re-encoding the original</remarks>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static FdbRawKey Memoize<TKey>(in TKey key)
			where TKey : struct, IFdbKey
		{
			if (typeof(TKey) == typeof(FdbRawKey))
			{ // already cached!
				return (FdbRawKey) (object) key;
			}

			return new(key.ToSlice());
		}

		/// <summary>Checks if the key is in the System keyspace (starts with <c>`\xFF`</c>)</summary>
		public static bool IsSystem<TKey>(in TKey key)
			where TKey : struct, IFdbKey
		{
			if (typeof(TKey) == typeof(FdbSystemKey))
			{
				return true;
			}

			// if the key is complete, 
			if (key.TryGetSpan(out var span))
			{
				return span.Length != 0 && span[0] == 0xFF;
			}

			// check the subspace prefix (rare, but could happen)
			var subspace = key.GetSubspace();
			if (subspace is not null && subspace.TryGetSpan(out span) && span.Length > 0)
			{
				return span[0] == 0xFF;
			}

			// we have to render the key, unfortunately
			using var bytes = Encode(in key, ArrayPool<byte>.Shared);
			return bytes.Data.StartsWith((byte) 0xFF);
		}

		/// <summary>Compares the prefix of two subspaces for equality</summary>
		public static bool Equals(IKeySubspace? subspace, IKeySubspace? other)
		{
			return (subspace ?? KeySubspace.Empty).Equals(other ?? KeySubspace.Empty);
		}

		/// <summary>Compares the prefix of two subspaces</summary>
		public static int CompareTo(IKeySubspace? subspace, IKeySubspace? other)
		{
			return (subspace ?? KeySubspace.Empty).CompareTo(other ?? KeySubspace.Empty);
		}

		/// <inheritdoc cref="CompareTo{TKey}(in TKey,System.ReadOnlySpan{byte})"/>
		public static int CompareTo<TKey>(in TKey key, Slice expectedBytes)
			where TKey : struct, IFdbKey
			=> !expectedBytes.IsNull ? CompareTo(in key, expectedBytes.Span) : +1;

		/// <summary>Compares a key with a specific encoded binary representation</summary>
		/// <typeparam name="TKey">Type of the key</typeparam>
		/// <param name="key">Key being compared</param>
		/// <param name="expectedBytes">Encoded bytes to compare with</param>
		/// <returns><c>0</c> if the key encodes to the exact same bytes, a negative number if it would be sorted before, or a positive number if it would be sorted after</returns>
		/// <remarks>
		/// <para>If the key is not pre-encoded, this method will encode the value into a pooled buffer, and then compare the bytes.</para>
		/// </remarks>
		public static int CompareTo<TKey>(in TKey key, ReadOnlySpan<byte> expectedBytes)
			where TKey : struct, IFdbKey
		{
			if (key.TryGetSpan(out var span))
			{
				return span.SequenceCompareTo(expectedBytes);
			}

			using var bytes = Encode(in key, ArrayPool<byte>.Shared);
			return bytes.Span.SequenceCompareTo(expectedBytes);
		}

		/// <summary>Compares two keys by their encoded binary representation</summary>
		/// <typeparam name="TKey">Type of the first key</typeparam>
		/// <typeparam name="TOtherKey">Type of the second key</typeparam>
		/// <param name="key">First key to compare</param>
		/// <param name="other">Second key to compare</param>
		/// <returns><c>0</c> if both keys are equal, a negative number if <paramref name="key"/> would be sorted before <paramref name="other"/>, or a positive number if <paramref name="key"/> would be sorted after <paramref name="other"/></returns>
		/// <remarks>
		/// <para>If the either key is not pre-encoded, this method will encode the value into a pooled buffer, and then compare the bytes.</para>
		/// </remarks>
		public static int CompareTo<TKey, TOtherKey>(in TKey key, in TOtherKey other)
			where TKey : struct, IFdbKey
			where TOtherKey : struct, IFdbKey
		{
			if (other.TryGetSpan(out var otherSpan))
			{
				return CompareTo(in key, otherSpan);
			}
			if (key.TryGetSpan(out var keySpan))
			{
				return CompareTo(in other, keySpan);
			}
			return CompareToIncompatible(in key, in other);

			static int CompareToIncompatible(in TKey key, in TOtherKey other)
			{
				using var keyBytes = Encode(in key, ArrayPool<byte>.Shared);
				using var otherBytes = Encode(in other, ArrayPool<byte>.Shared);
				return keyBytes.Span.SequenceCompareTo(otherBytes.Span);
			}
		}

		/// <summary>Checks if the key, once encoded, would be equal to the specified bytes</summary>
		/// <typeparam name="TKey">Type of the first key</typeparam>
		/// <typeparam name="TOtherKey">Type of the second key</typeparam>
		/// <param name="key">First key to test</param>
		/// <param name="other">Second key to test</param>
		/// <returns><c>true</c> if the both key encodes to the exact same bytes; otherwise, <c>false</c></returns>
		/// <remarks>
		/// <para>If the either key is not pre-encoded, this method will encode the value into a pooled buffer, and then compare the bytes.</para>
		/// </remarks>
		public static bool Equals<TKey, TOtherKey>(in TKey key, in TOtherKey other)
			where TKey : struct, IFdbKey
			where TOtherKey : struct, IFdbKey
		{
			if (other.TryGetSpan(out var otherSpan))
			{
				return Equals(in key, otherSpan);
			}
			if (key.TryGetSpan(out var keySpan))
			{
				return Equals(in other, keySpan);
			}
			return EqualsIncompatible(in key, in other);

			static bool EqualsIncompatible(in TKey key, in TOtherKey other)
			{
				using var keyBytes = Encode(in key, ArrayPool<byte>.Shared);
				using var otherBytes = Encode(in other, ArrayPool<byte>.Shared);
				return keyBytes.Span.SequenceEqual(otherBytes.Span);
			}
		}

		/// <summary>Checks if the key, once encoded, would be equal to the specified bytes</summary>
		/// <typeparam name="TKey">Type of the first key</typeparam>
		/// <param name="key">First key to test</param>
		/// <param name="other">Second key to test</param>
		/// <returns><c>true</c> if the both key encodes to the exact same bytes; otherwise, <c>false</c></returns>
		/// <remarks>
		/// <para>If the either key is not pre-encoded, this method will encode the value into a pooled buffer, and then compare the bytes.</para>
		/// </remarks>
		public static bool Equals<TKey>(in TKey key, IFdbKey other)
			where TKey : struct, IFdbKey
		{
			if (other.TryGetSpan(out var otherSpan))
			{
				return Equals(in key, otherSpan);
			}
			if (key.TryGetSpan(out var keySpan))
			{
				return other.Equals(keySpan);
			}
			return EqualsIncompatible(in key, other);

			static bool EqualsIncompatible(in TKey key, IFdbKey other)
			{
				using var keyBytes = Encode(in key, ArrayPool<byte>.Shared);
				using var otherBytes = Encode(other, ArrayPool<byte>.Shared);
				return keyBytes.Span.SequenceEqual(otherBytes.Span);
			}
		}

		/// <inheritdoc cref="Equals{TKey}(in TKey,System.ReadOnlySpan{byte})"/>
		public static bool Equals<TKey>(in TKey key, Slice expectedBytes)
			where TKey : struct, IFdbKey
			=> Equals(in key, expectedBytes.Span);

		/// <summary>Checks if the key, once encoded, would be equal to the specified bytes</summary>
		/// <typeparam name="TKey">Type of the key</typeparam>
		/// <param name="key">Key to test</param>
		/// <param name="expectedBytes">Expected encoded bytes</param>
		/// <returns><c>true</c> if the key encodes to the exact same bytes; otherwise, <c>false</c></returns>
		/// <remarks>
		/// <para>If the key is not pre-encoded, this method will encode the value into a pooled buffer, and then compare the bytes.</para>
		/// </remarks>
		public static bool Equals<TKey>(in TKey key, ReadOnlySpan<byte> expectedBytes)
			where TKey : struct, IFdbKey
		{
			if (key.TryGetSpan(out var span))
			{
				return span.SequenceEqual(expectedBytes);
			}

			using var bytes = Encode(in key, ArrayPool<byte>.Shared);
			return bytes.Span.SequenceEqual(expectedBytes);
		}

		/// <summary>Encodes this key into <see cref="Slice"/></summary>
		/// <param name="key">Key to encode</param>
		/// <returns><see cref="Slice"/> that contains the binary representation of this key</returns>
		/// <exception cref="InvalidOperationException">when the key exceeds the maximum allowed key size (see <see cref="FdbKey.MaxSize"/>)</exception>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static Slice ToSlice<TKey>(in TKey key)
			where TKey : struct, IFdbKey
		{
			if (typeof(TKey) == typeof(FdbRawKey))
			{
				return ((FdbRawKey) (object) key).Data;
			}

			if (key.TryGetSpan(out var span))
			{
				return Slice.FromBytes(span);
			}

			return ToSliceSlow(in key);

			[MethodImpl(MethodImplOptions.NoInlining)]
			static Slice ToSliceSlow(in TKey key)
			{
				byte[]? tmp = null;
				if (key.TryGetSizeHint(out var capacity))
				{
					if (capacity <= 0)
					{
#if DEBUG
						// probably a bug in TryGetSizeHint which returned "true" instead of "false"!
						if (System.Diagnostics.Debugger.IsAttached) System.Diagnostics.Debugger.Break();
#endif
						capacity = 16;
					}

					// we will hope for the best, and pre-allocate the slice
					tmp = new byte[capacity];
					if (key.TryEncode(tmp, out var bytesWritten))
					{
						return tmp.AsSlice(0, bytesWritten);
					}

					if (capacity >= FdbKey.MaxSize)
					{
						goto key_too_long;
					}

					capacity *= 2;
				}
				else
				{
					capacity = 128;
				}

				var pool = ArrayPool<byte>.Shared;
				try
				{
					while (true)
					{
						tmp = pool.Rent(capacity);
						if (key.TryEncode(tmp, out int bytesWritten))
						{
							return tmp.AsSlice(0, bytesWritten).Copy();
						}

						pool.Return(tmp);
						tmp = null;

						if (capacity >= FdbKey.MaxSize)
						{
							goto key_too_long;
						}

						capacity *= 2;
					}
				}
				catch (Exception)
				{
					if (tmp is not null)
					{
						pool.Return(tmp);
					}

					throw;
				}

			key_too_long:
				// it would be too large anyway!
				throw new ArgumentException("Cannot encode key because it would exceed the maximum allowed length.");
			}
		}

		/// <summary>Encodes this key into <see cref="Slice"/>, using backing buffer rented from a pool</summary>
		/// <param name="key">Key to encode</param>
		/// <param name="pool">Pool used to rent the buffer (<see cref="ArrayPool{T}.Shared"/> is <c>null</c>)</param>
		/// <returns><see cref="SliceOwner"/> that contains the binary representation of this key</returns>
		/// <exception cref="InvalidOperationException">when the key exceeds the maximum allowed key size (see <see cref="FdbKey.MaxSize"/>)</exception>
		[MustDisposeResource, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static SliceOwner ToSlice<TKey>(in TKey key, ArrayPool<byte>? pool)
			where TKey : struct, IFdbKey
		{
			if (typeof(TKey) == typeof(FdbRawKey))
			{
				return SliceOwner.Wrap(((FdbRawKey) (object) key).Data);
			}

			pool ??= ArrayPool<byte>.Shared;
			return key.TryGetSpan(out var span)
				? SliceOwner.Copy(span, pool)
				: Encode(in key, pool);
		}

		/// <summary>Encodes a boxed key, using a pooled buffer</summary>
		/// <remarks>This method is less efficient than the generic implementations, and should only be used when there is no other solution.</remarks>
		/// <exception cref="InvalidOperationException">when the key exceeds the maximum allowed key size (see <see cref="FdbKey.MaxSize"/>)</exception>
		[MustDisposeResource, MethodImpl(MethodImplOptions.NoInlining)]
		public static SliceOwner Encode(IFdbKey key, ArrayPool<byte>? pool, int? sizeHint = null)
		{
			Contract.Debug.Requires(pool != null);

			int capacity;
			if (sizeHint is not null)
			{
				capacity = sizeHint.Value;
			}
			else if (!key.TryGetSizeHint(out capacity))
			{
				capacity = 0;
			}
			if (capacity <= 0)
			{
				capacity = 128;
			}

			byte[]? tmp = null;
			try
			{
				while (true)
				{
					tmp = pool.Rent(capacity);
					if (key.TryEncode(tmp, out int bytesWritten))
					{
						if (bytesWritten == 0)
						{
							pool.Return(tmp);
							tmp = null;
							return SliceOwner.Empty;
						}

						return SliceOwner.Create(tmp.AsSlice(0, bytesWritten), pool);
					}

					pool.Return(tmp);
					tmp = null;

					if (capacity >= FdbKey.MaxSize)
					{
						// it would be too large anyway!
						throw new ArgumentException("Cannot encode key because it would exceed the maximum allowed length.");
					}
					capacity *= 2;
				}
			}
			catch(Exception)
			{
				if (tmp is not null)
				{
					pool.Return(tmp);
				}
				throw;
			}
		}

		/// <summary>Encodes a key, using a pooled buffer</summary>
		/// <exception cref="InvalidOperationException">when the key exceeds the maximum allowed key size (see <see cref="FdbKey.MaxSize"/>)</exception>
		[MustDisposeResource, MethodImpl(MethodImplOptions.NoInlining)]
		public static SliceOwner Encode<TKey>(in TKey key, ArrayPool<byte>? pool, int? sizeHint = null)
			where TKey : struct, IFdbKey
		{
			Contract.Debug.Requires(pool != null);

			int capacity;
			if (sizeHint is not null)
			{
				capacity = sizeHint.Value;
			}
			else if (!key.TryGetSizeHint(out capacity))
			{
				capacity = 0;
			}
			if (capacity <= 0)
			{
				capacity = 128;
			}

			byte[]? tmp = null;
			try
			{
				while (true)
				{
					tmp = pool.Rent(capacity);
					if (key.TryEncode(tmp, out int bytesWritten))
					{
						if (bytesWritten == 0)
						{
							pool.Return(tmp);
							tmp = null;
							return SliceOwner.Empty;
						}

						return SliceOwner.Create(tmp.AsSlice(0, bytesWritten), pool);
					}

					pool.Return(tmp);
					tmp = null;

					if (capacity >= FdbKey.MaxSize)
					{
						// it would be too large anyway!
						throw new InvalidOperationException("Cannot encode key because it would exceed the maximum allowed length.");
					}
					capacity *= 2;
				}
			}
			catch(Exception)
			{
				if (tmp is not null)
				{
					pool.Return(tmp);
				}
				throw;
			}
		}

		/// <summary>Encodes a key, using a pooled buffer</summary>
		[MustUseReturnValue, MethodImpl(MethodImplOptions.NoInlining)]
		public static ReadOnlySpan<byte> Encode<TKey>(scoped in TKey key, scoped ref byte[]? buffer, ArrayPool<byte>? pool, int? sizeHint = null)
			where TKey : struct, IFdbKey
		{
			Contract.Debug.Requires(pool != null);

			int capacity;
			if (sizeHint is not null)
			{
				capacity = sizeHint.Value;
			}
			else if (!key.TryGetSizeHint(out capacity))
			{
				capacity = 0;
			}
			if (capacity <= 0)
			{
				capacity = 128;
			}

			while (true)
			{
				if (buffer is null)
				{
					buffer = pool.Rent(capacity);
				}
				else if (buffer.Length < capacity)
				{
					pool.Return(buffer);
					buffer = pool.Rent(capacity);
				}

				if (key.TryEncode(buffer, out int bytesWritten))
				{
					return bytesWritten > 0 ? buffer.AsSpan(0, bytesWritten) : default;
				}

				if (capacity >= FdbKey.MaxSize)
				{
					// it would be too large anyway!
					throw new ArgumentException("Cannot encode key because it would exceed the maximum allowed length.");
				}
				capacity *= 2;
			}
		}

		/// <summary>Encodes a key, using a pooled buffer</summary>
		[MustUseReturnValue, MethodImpl(MethodImplOptions.NoInlining)]
		public static Slice ToSlice<TKey>(scoped in TKey key, scoped ref byte[]? buffer, ArrayPool<byte>? pool)
			where TKey : struct, IFdbKey
		{
			Contract.Debug.Requires(pool != null);

			if (!key.TryGetSizeHint(out int capacity) || capacity <= 0)
			{
				capacity = 128;
			}

			while (true)
			{
				if (buffer is null)
				{
					buffer = pool.Rent(capacity);
				}
				else if (buffer.Length < capacity)
				{
					pool.Return(buffer);
					buffer = pool.Rent(capacity);
				}

				if (key.TryEncode(buffer, out int bytesWritten))
				{
					return bytesWritten > 0 ? buffer.AsSlice(0, bytesWritten) : Slice.Empty;
				}

				if (capacity >= FdbKey.MaxSize)
				{
					// it would be too large anyway!
					throw new ArgumentException("Cannot encode key because it would exceed the maximum allowed length.");
				}
				capacity *= 2;
			}
		}

	}

}
