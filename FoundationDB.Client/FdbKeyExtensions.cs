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

	/// <summary>Extension methods for working with <see cref="FdbKey{TKey,TEncoder}"/></summary>
	public static class FdbKeyExtensions
	{

		#region ToRange...

		/// <summary>Returns a range that captures all the keys that have this key as a prefix</summary>
		/// <param name="key">Key to encode</param>
		/// <param name="excluded">If <c>true</c> the key itself is will not be included in the range, which corresponds to calling <see cref="KeyRange.PrefixedBy"/>. If <c>false</c> (default), it will be included, which corresponds to calling <see cref="KeyRange.StartsWith"/></param>
		/// <remarks>
		/// <para>For example, if the key corresponds to the bytes <c>`abc`</c>, the range will match all keys 'k' such that <c>`abc'</c> &lt;= k &lt; <c>`abd'</c>.</para>
		/// <para>When excluded is <c>true</c>, the range will now match all keys 'k' such that <c>`abc'</c> &lt; k &lt; <c>`abd'</c>, which is equivalent to <c>`abc\x00'</c> &lt;= k &lt; <c>`abd'</c>.</para>
		/// <para>Please note that <c>subspace.GetKey("abc", 123).GetRange()</c> is equivalent to <c>subspace.GetRange("abc", 123)</c></para>
		/// </remarks>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static FdbKeyPrefixRange<TKey> ToRange<TKey>(this TKey key, bool excluded = false)
			where TKey : struct, IFdbKey
		{
			return excluded
				? FdbKeyRange.PrefixedBy(in key)
				: FdbKeyRange.StartsWith(in key);
		}

		/// <summary>Returns a key that adds a <c>0x00</c> suffix to get the immediate successor of this key in the database (ex: <c>`abc`</c> => <c>`abc\x00`</c>)</summary>
		/// <typeparam name="TKey">Type of the key</typeparam>
		/// <param name="key">Input key</param>
		/// <returns>Key that, when encoded into binary, will append the byte <c>0x00</c> to the representation of <paramref name="key"/></returns>
		/// <remarks>
		/// <para>It is guaranteed that there can not be any keys between <paramref name="key"/> and the returned key.</para>
		/// <para>This can be used to produce the "end" key for a read or write conflict range, that will match a single key</para>
		/// <para>For example, <c>subspace.GetKey(123).GetSuccessor()</c> will generate <c>'\x15\x7B'</c> + <c>'\x00'</c> = <c>'\x15\x7B\x00'</c>, which is the same as <c>subspace.GetKey(123, null)</c>, the next possible key.</para>
		/// <para>It should be combined with <see cref="FdbKeySelector.FirstGreaterOrEqual{TKey}"/> to produce a valid 'end' selector for a range read</para>
		/// </remarks>
		/// <seealso cref="FdbKeySelector.FirstGreaterOrEqual{TKey}"/>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static FdbSuccessorKey<TKey> GetSuccessor<TKey>(this TKey key)
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
		public static FdbNextKey<TKey> GetNextSibling<TKey>(this TKey key)
			where TKey : struct, IFdbKey
			=> new(key);

		/// <summary>Returns a sub-range that will match all keys that are children of <paramref name="key"/> and between the two given bounds</summary>
		/// <typeparam name="TKey">Type of the key</typeparam>
		/// <typeparam name="TTuple">Type of the cursor</typeparam>
		/// <param name="key">Parent key</param>
		/// <param name="from">Inclusive lower bound (relative to this key), or <c>null</c> to read from the start of the range</param>
		/// <param name="to">Exclusive upper bound (relative to this key), or <c>null</c> to read to the end of the range</param>
		/// <returns></returns>
		public static FdbBetweenRange<TKey, TTuple> Between<TKey, TTuple>(this TKey key, TTuple from, TTuple to)
			where TKey : struct, IFdbKey
			where TTuple : IVarTuple
		{
			Contract.NotNull(from);
			Contract.NotNull(to);
			return new(key, from, fromInclusive: true, to, toInclusive: false);
		}

		/// <summary>Returns a sub-range that will match all keys that are children of <paramref name="key"/> and between the two given bounds</summary>
		/// <typeparam name="TKey">Type of the key</typeparam>
		/// <typeparam name="TTuple">Type of the cursor</typeparam>
		/// <param name="key">Parent key</param>
		/// <param name="from">Lower bound (relative to this key), or <c>null</c> to read from the start of the range</param>
		/// <param name="fromInclusive">Specified whether the lower bound is included in the range</param>
		/// <param name="to">Exclusive upper bound (relative to this key), or <c>null</c> to read to the end of the range</param>
		/// <param name="toInclusive">Specified whether the upper bound is included in the range</param>
		/// <returns></returns>
		public static FdbBetweenRange<TKey, TTuple> Between<TKey, TTuple>(this TKey key, TTuple from, bool fromInclusive, TTuple to, bool toInclusive)
			where TKey : struct, IFdbKey
			where TTuple : IVarTuple
		{
			Contract.NotNull(from);
			Contract.NotNull(to);
			return new(key, from, fromInclusive, to, toInclusive);
		}

		/// <summary>Returns a sub-range that will match all keys that are children of <paramref name="key"/> and located between the given cursor and the end of the range.</summary>
		/// <typeparam name="TKey">Type of the key</typeparam>
		/// <typeparam name="TTuple">Type of the cursor</typeparam>
		/// <param name="key">Parent key</param>
		/// <param name="from">Lower bound (relative to this key)</param>
		/// <param name="fromInclusive">Specifies if the lower bound is included (default) or excluded from the range</param>
		public static FdbTailRange<TKey, TTuple> Tail<TKey, TTuple>(this TKey key, TTuple from, bool fromInclusive = true)
			where TKey : struct, IFdbKey
			where TTuple : IVarTuple
		{
			Contract.NotNull(from);
			return new(key, from, fromInclusive);
		}

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static FdbTailRange<TKey, STuple<T1>> TailKey<TKey, T1>(this TKey key, T1 cursor1)
			where TKey : struct, IFdbKey
			=> new(key, new(cursor1), fromInclusive: true);

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static FdbTailRange<TKey, STuple<T1, T2>> TailKey<TKey, T1, T2>(this TKey key, T1 cursor1, T2 cursor2)
			where TKey : struct, IFdbKey
			=> new(key, new(cursor1, cursor2), fromInclusive: true);

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static FdbTailRange<TKey, STuple<T1, T2, T3>> TailKey<TKey, T1, T2, T3>(this TKey key, T1 cursor1, T2 cursor2, T3 cursor3)
			where TKey : struct, IFdbKey
			=> new(key, new(cursor1, cursor2, cursor3), fromInclusive: true);

		/// <summary>Returns a sub-range that will match all keys that are children of <paramref name="key"/> and located between the start of the range and given cursor.</summary>
		/// <typeparam name="TKey"></typeparam>
		/// <typeparam name="TTuple">Type of the cursor</typeparam>
		/// <param name="key">Parent key</param>
		/// <param name="to">Upper bound (relative to this key)</param>
		/// <param name="toInclusive">Specifies if the upper bound is included or excluded (default) from the range</param>
		public static FdbHeadRange<TKey, TTuple> Head<TKey, TTuple>(this TKey key, TTuple to, bool toInclusive = false)
			where TKey : struct, IFdbKey
			where TTuple : IVarTuple
		{
			Contract.NotNull(to);
			return new(key, to, toInclusive);
		}

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static FdbHeadRange<TKey, STuple<T1>> HeadKey<TKey, T1>(this TKey key, T1 cursor1)
			where TKey : struct, IFdbKey
			=> new(key, new(cursor1), toInclusive: false);

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static FdbHeadRange<TKey, STuple<T1, T2>> HeadKey<TKey, T1, T2>(this TKey key, T1 cursor1, T2 cursor2)
			where TKey : struct, IFdbKey
			=> new(key, new(cursor1, cursor2), toInclusive: false);

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static FdbHeadRange<TKey, STuple<T1, T2, T3>> HeadKey<TKey, T1, T2, T3>(this TKey key, T1 cursor1, T2 cursor2, T3 cursor3)
			where TKey : struct, IFdbKey
			=> new(key, new(cursor1, cursor2, cursor3), toInclusive: false);

		/// <summary>Returns a range that will match the key, and all of its children</summary>
		/// <typeparam name="TKey">Type of the key</typeparam>
		/// <param name="key">Key that will be used as a prefix</param>
		/// <returns>Range that matches all keys that start with <paramref name="key"/> (included)</returns>
		/// <para>Ex: <c>subspace.GetKey(123).StartsWith()</c> will match <c>(..., 123)</c> as well as all the keys of the form <c>(..., 123, ...)</c>.</para>
		/// <para>Please be careful when using this with tuples where the last elements is a <see cref="string"/> or <see cref="Slice"/>: the encoding adds an extra <c>0x00</c> bytes after the element (ex: <c>"hello"</c> => <c>`\x02hello\x00`</c>), which means that <c>(..., "abc")</c> is <b>NOT</b> a child of <c>(..., "ab")</c></para>
		/// <seealso cref="PrefixedBy{TKey}"/>
		public static FdbKeyPrefixRange<TKey> StartsWith<TKey>(this TKey key)
			where TKey : struct, IFdbKey
			=> new(key, excluded: false);

		/// <summary>Returns a range that will match the all the key's children, but not the key itself</summary>
		/// <typeparam name="TKey">Type of the key</typeparam>
		/// <param name="key">Key that will be used as a prefix</param>
		/// <returns>Range that matches all keys that start with the prefix <paramref name="key"/> (excluded)</returns>
		/// <remarks>
		/// <para>Ex: <c>subspace.GetKey(123).PrefixedBy()</c> will match all keys of the form <c>(..., 123, ...)</c>, but will not include <c>(..., 123)</c> itself.</para>
		/// </remarks>
		/// <seealso cref="StartsWith{TKey}"/>
		public static FdbKeyPrefixRange<TKey> PrefixedBy<TKey>(this TKey key)
			where TKey : struct, IFdbKey
			=> new(key, excluded: false);

		#endregion

		#region Selectors...

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static FdbKeySelector<TKey> LastLessThan<TKey>(this TKey key)
			where TKey : struct, IFdbKey =>
			FdbKeySelector.LastLessThan(in key);

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static FdbKeySelector<TKey> LastLessOrEqual<TKey>(this TKey key)
			where TKey : struct, IFdbKey =>
			FdbKeySelector.LastLessOrEqual(in key);

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static FdbKeySelector<TKey> FirstGreaterThan<TKey>(this TKey key)
			where TKey : struct, IFdbKey =>
			FdbKeySelector.FirstGreaterThan(in key);

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static FdbKeySelector<TKey> FirstGreaterOrEqual<TKey>(this TKey key)
			where TKey : struct, IFdbKey =>
			FdbKeySelector.FirstGreaterOrEqual(in key);

		#endregion

		#region TSubspace Keys...

		/// <summary>Returns a new subspace that will use this key as its prefix</summary>
		/// <typeparam name="TKey"></typeparam>
		/// <param name="key"></param>
		/// <returns></returns>
		public static IDynamicKeySubspace ToSubspace<TKey>(this TKey key)
			where TKey : struct, IFdbKey
		{
			var parent = key.GetSubspace();

			// the key is tied to the subspace context
			var context = parent?.Context ?? SubspaceContext.Default;
			context.EnsureIsValid();

			return new DynamicKeySubspace(FdbKeyHelpers.ToSlice(in key), context);
		}

		/// <summary>Returns a new subspace with an additional prefix</summary>
		/// <param name="subspace">Parent subspace</param>
		/// <param name="prefix">Prefix to be added to the parent subspace prefix</param>
		/// <returns>Subspace that will append both prefixes to all the keys</returns>
		/// <remarks>
		/// <para>For example, if the <paramref name="subspace "/> has the prefix <c>(42,)</c> (<c>`\x15\x2A`</c>) and <paramref name="prefix"/> is <c>`\x15\x7B`</c>),
		/// then the new subspace will have the prefix <c>(42, 123)</c> (<c>`\x15\x2A\x15\x7B`</c>)</para>
		/// <para>The generated subspace will use the same <see cref="IKeySubspace.Context">context</see> as <paramref name="subspace"/> and will be tied to its parent's lifetime.</para>
		/// </remarks>
		public static IDynamicKeySubspace ToSubspace(this IDynamicKeySubspace subspace, ReadOnlySpan<byte> prefix)
		{
			Contract.NotNull(subspace);
			subspace.Context.EnsureIsValid();
			return new DynamicKeySubspace(subspace.GetPrefix().Concat(prefix), subspace.Context);
		}

		/// <summary>Returns a new subspace with an additional prefix</summary>
		/// <param name="subspace">Parent subspace</param>
		/// <param name="prefix">Prefix to be added to the parent subspace prefix</param>
		/// <returns>Subspace that will append both prefixes to all the keys</returns>
		/// <remarks>
		/// <para>For example, if the <paramref name="subspace "/> has the prefix <c>(42,)</c> (<c>`\x15\x2A`</c>) and <paramref name="prefix"/> is <c>`\x15\x7B`</c>),
		/// then the new subspace will have the prefix <c>(42, 123)</c> (<c>`\x15\x2A\x15\x7B`</c>)</para>
		/// <para>The generated subspace will use the same <see cref="IKeySubspace.Context">context</see> as <paramref name="subspace"/> and will be tied to its parent's lifetime.</para>
		/// </remarks>
		public static IDynamicKeySubspace ToSubspace(this IDynamicKeySubspace subspace, Slice prefix)
		{
			Contract.NotNull(subspace);
			subspace.Context.EnsureIsValid();
			return new DynamicKeySubspace(subspace.GetPrefix() + prefix, subspace.Context);
		}

		#region IDynamicKeySubspace.GetKey(...)...

		/// <summary>Returns the key for this subspace</summary>
		/// <param name="subspace">Subspace that contains the key</param>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static FdbSuffixKey GetKey(this IDynamicKeySubspace subspace) => new(subspace, Slice.Empty);

		/// <summary>Returns a key under this subspace</summary>
		/// <param name="subspace">Subspace that contains the key</param>
		/// <param name="item1">value of the single element in the key</param>
		/// <remarks>
		/// <para>Example:<code>
		/// // reads the document for 'user123' under this subspace
		/// var value = await tr.GetAsync(subspace.GetKey("user123"));</code></para>
		/// <para>Please remember that if you pass a tuple as argument to this method, it will be added as an <i>embedded</i> tuple, which may not be what you expect! To generate a key from items already stored in a tuple, use <see cref="Append{T1}(FoundationDB.Client.IDynamicKeySubspace,SnowBank.Data.Tuples.STuple{T1})"/> instead.</para>
		/// </remarks>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static FdbTupleKey<T1> GetKey<T1>(this IDynamicKeySubspace subspace, T1 item1) => new(subspace, item1);

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
		/// <para>Please remember that if you pass a tuple as argument to this method, it will be added as an <i>embedded</i> tuple, which may not be what you expect! To generate a key from items already stored in a tuple, use <see cref="Append{T1,T2}(FoundationDB.Client.IDynamicKeySubspace,in SnowBank.Data.Tuples.STuple{T1,T2})"/> instead.</para>
		/// </remarks>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static FdbTupleKey<T1, T2> GetKey<T1, T2>(this IDynamicKeySubspace subspace, T1 item1, T2 item2) => new(subspace, item1, item2);

		/// <summary>Returns a key with 3 elements under this subspace</summary>
		/// <param name="subspace">Subspace that contains the key</param>
		/// <param name="item1">value of the 1st element in the key</param>
		/// <param name="item2">value of the 2nd element in the key</param>
		/// <param name="item3">value of the 3rd element in the key</param>
		/// <remarks>
		/// <para>Example: <code>
		/// const INDEX_BY_CITY = 2; // subsection that stores a non-unique index of users by their city name.
		/// tr.Set(subspace.GetKey(INDEX_BY_CITY, "Tokyo", "user123"), Slice.Empty)</code></para>
		/// <para>Please remember that if you pass a tuple as argument to this method, it will be added as an <i>embedded</i> tuple, which may not be what you expect! To generate a key from items already stored in a tuple, use <see cref="Append{T1,T2,T3}(FoundationDB.Client.IDynamicKeySubspace,in SnowBank.Data.Tuples.STuple{T1,T2,T3})"/> instead.</para>
		/// </remarks>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static FdbTupleKey<T1, T2, T3> GetKey<T1, T2, T3>(this IDynamicKeySubspace subspace, T1 item1, T2 item2, T3 item3) => new(subspace, item1, item2, item3);

		/// <summary>Returns a key with 4 elements under this subspace</summary>
		/// <param name="subspace">Subspace that contains the key</param>
		/// <param name="item1">value of the 1st element in the key</param>
		/// <param name="item2">value of the 2nd element in the key</param>
		/// <param name="item3">value of the 3rd element in the key</param>
		/// <param name="item4">value of the 4th element in the key</param>
		/// <remarks>
		/// <para>Please remember that if you pass a tuple as argument to this method, it will be added as an <i>embedded</i> tuple, which may not be what you expect! To generate a key from items already stored in a tuple, use <see cref="Append{T1,T2,T3,T4}(FoundationDB.Client.IDynamicKeySubspace,in SnowBank.Data.Tuples.STuple{T1,T2,T3,T4})"/> instead.</para>
		/// </remarks>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static FdbTupleKey<T1, T2, T3, T4> GetKey<T1, T2, T3, T4>(this IDynamicKeySubspace subspace, T1 item1, T2 item2, T3 item3, T4 item4) => new(subspace, item1, item2, item3, item4);

		/// <summary>Returns a key with 5 elements under this subspace</summary>
		/// <param name="subspace">Subspace that contains the key</param>
		/// <param name="item1">value of the 1st element in the key</param>
		/// <param name="item2">value of the 2nd element in the key</param>
		/// <param name="item3">value of the 3rd element in the key</param>
		/// <param name="item4">value of the 4th element in the key</param>
		/// <param name="item5">value of the 5th element in the key</param>
		/// <remarks>
		/// <para>Please remember that if you pass a tuple as argument to this method, it will be added as an <i>embedded</i> tuple, which may not be what you expect! To generate a key from items already stored in a tuple, use <see cref="Append{T1,T2,T3,T4,T5}(FoundationDB.Client.IDynamicKeySubspace,in SnowBank.Data.Tuples.STuple{T1,T2,T3,T4,T5})"/> instead.</para>
		/// </remarks>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static FdbTupleKey<T1, T2, T3, T4, T5> GetKey<T1, T2, T3, T4, T5>(this IDynamicKeySubspace subspace, T1 item1, T2 item2, T3 item3, T4 item4, T5 item5) => new(subspace, item1, item2, item3, item4, item5);

		/// <summary>Returns a key with 6 elements under this subspace</summary>
		/// <param name="subspace">Subspace that contains the key</param>
		/// <param name="item1">value of the 1st element in the key</param>
		/// <param name="item2">value of the 2nd element in the key</param>
		/// <param name="item3">value of the 3rd element in the key</param>
		/// <param name="item4">value of the 4th element in the key</param>
		/// <param name="item5">value of the 5th element in the key</param>
		/// <param name="item6">value of the 6th element in the key</param>
		/// <remarks>
		/// <para>Please remember that if you pass a tuple as argument to this method, it will be added as an <i>embedded</i> tuple, which may not be what you expect! To generate a key from items already stored in a tuple, use <see cref="Append{T1,T2,T3,T4,T5,T6}(FoundationDB.Client.IDynamicKeySubspace,in SnowBank.Data.Tuples.STuple{T1,T2,T3,T4,T5,T6})"/> instead.</para>
		/// </remarks>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static FdbTupleKey<T1, T2, T3, T4, T5, T6> GetKey<T1, T2, T3, T4, T5, T6>(this IDynamicKeySubspace subspace, T1 item1, T2 item2, T3 item3, T4 item4, T5 item5, T6 item6) => new(subspace, item1, item2, item3, item4, item5, item6);

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
		/// <para>Please remember that if you pass a tuple as argument to this method, it will be added as an <i>embedded</i> tuple, which may not be what you expect! To generate a key from items already stored in a tuple, use <see cref="Append{T1,T2,T3,T4,T5,T6,T7}(FoundationDB.Client.IDynamicKeySubspace,in SnowBank.Data.Tuples.STuple{T1,T2,T3,T4,T5,T6,T7})"/> instead.</para>
		/// </remarks>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static FdbTupleKey<T1, T2, T3, T4, T5, T6, T7> GetKey<T1, T2, T3, T4, T5, T6, T7>(this IDynamicKeySubspace subspace, T1 item1, T2 item2, T3 item3, T4 item4, T5 item5, T6 item6, T7 item7) => new(subspace, item1, item2, item3, item4, item5, item6, item7);

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
		/// <para>Please remember that if you pass a tuple as argument to this method, it will be added as an <i>embedded</i> tuple, which may not be what you expect! To generate a key from items already stored in a tuple, use <see cref="Append{T1,T2,T3,T4,T5,T6,T7,T8}(FoundationDB.Client.IDynamicKeySubspace,in SnowBank.Data.Tuples.STuple{T1,T2,T3,T4,T5,T6,T7,T8})"/> instead.</para>
		/// </remarks>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static FdbTupleKey<T1, T2, T3, T4, T5, T6, T7, T8> GetKey<T1, T2, T3, T4, T5, T6, T7, T8>(this IDynamicKeySubspace subspace, T1 item1, T2 item2, T3 item3, T4 item4, T5 item5, T6 item6, T7 item7, T8 item8) => new(subspace, item1, item2, item3, item4, item5, item6, item7, item8);

		#endregion

		#region IDynamicKeySubspace.Append(ValueTuple<...>)...

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static FdbTupleKey<T1> Append<T1>(this IDynamicKeySubspace subspace, ValueTuple<T1> key) => new(subspace, key.Item1);

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static FdbTupleKey<T1, T2> Append<T1, T2>(this IDynamicKeySubspace subspace, in ValueTuple<T1, T2> key) => new(subspace, in key);

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static FdbTupleKey<T1, T2, T3> Append<T1, T2, T3>(this IDynamicKeySubspace subspace, in ValueTuple<T1, T2, T3> key) => new(subspace, in key);

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static FdbTupleKey<T1, T2, T3, T4> Append<T1, T2, T3, T4>(this IDynamicKeySubspace subspace, in ValueTuple<T1, T2, T3, T4> key) => new(subspace, in key);

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static FdbTupleKey<T1, T2, T3, T4, T5> Append<T1, T2, T3, T4, T5>(this IDynamicKeySubspace subspace, in ValueTuple<T1, T2, T3, T4, T5> key) => new(subspace, in key);

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static FdbTupleKey<T1, T2, T3, T4, T5, T6> Append<T1, T2, T3, T4, T5, T6>(this IDynamicKeySubspace subspace, in ValueTuple<T1, T2, T3, T4, T5, T6> key) => new(subspace, in key);

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static FdbTupleKey<T1, T2, T3, T4, T5, T6, T7> Append<T1, T2, T3, T4, T5, T6, T7>(this IDynamicKeySubspace subspace, in ValueTuple<T1, T2, T3, T4, T5, T6, T7> key) => new(subspace, in key);

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static FdbTupleKey<T1, T2, T3, T4, T5, T6, T7, T8> Append<T1, T2, T3, T4, T5, T6, T7, T8>(this IDynamicKeySubspace subspace, in ValueTuple<T1, T2, T3, T4, T5, T6, T7, ValueTuple<T8>> key) => new(subspace, in key);

		#endregion

		#region IDynamicKeySubspace.PackKey(STuple<...>)...

		public static FdbTupleSuffixKey<TKey, TTuple> Append<TKey, TTuple>(this TKey key, TTuple items)
			where TKey : struct, IFdbKey
			where TTuple : IVarTuple
		{
			return new(key, items);
		}

		/// <summary>Returns a key that packs the given items under this subspace</summary>
		/// <param name="subspace">Subspace that contains the key</param>
		/// <param name="items">elements of the key</param>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static FdbTupleKey Append(this IDynamicKeySubspace subspace, IVarTuple items) => new(subspace, items);

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static FdbTupleKey<T1> Append<T1>(this IDynamicKeySubspace subspace, STuple<T1> key) => new(subspace, key.Item1);

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static FdbTupleKey<T1, T2> Append<T1, T2>(this IDynamicKeySubspace subspace, in STuple<T1, T2> key) => new(subspace, in key);

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static FdbTupleKey<T1, T2, T3> Append<T1, T2, T3>(this IDynamicKeySubspace subspace, in STuple<T1, T2, T3> key) => new(subspace, in key);

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static FdbTupleKey<T1, T2, T3, T4> Append<T1, T2, T3, T4>(this IDynamicKeySubspace subspace, in STuple<T1, T2, T3, T4> key) => new(subspace, in key);

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static FdbTupleKey<T1, T2, T3, T4, T5> Append<T1, T2, T3, T4, T5>(this IDynamicKeySubspace subspace, in STuple<T1, T2, T3, T4, T5> key) => new(subspace, in key);

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static FdbTupleKey<T1, T2, T3, T4, T5, T6> Append<T1, T2, T3, T4, T5, T6>(this IDynamicKeySubspace subspace, in STuple<T1, T2, T3, T4, T5, T6> key) => new(subspace, in key);

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static FdbTupleKey<T1, T2, T3, T4, T5, T6, T7> Append<T1, T2, T3, T4, T5, T6, T7>(this IDynamicKeySubspace subspace, in STuple<T1, T2, T3, T4, T5, T6, T7> key) => new(subspace, in key);

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static FdbTupleKey<T1, T2, T3, T4, T5, T6, T7, T8> Append<T1, T2, T3, T4, T5, T6, T7, T8>(this IDynamicKeySubspace subspace, in STuple<T1, T2, T3, T4, T5, T6, T7, T8> key) => new(subspace, in key);

		#endregion

		#region IDynamicKeySubspace.GetRange(...)...

		/// <summary>Returns a range that matches all the keys under this subspace that start with the given first element</summary>
		/// <param name="subspace">Subspace that contains the key</param>
		/// <param name="item1">Value of the 1st element of the matched keys</param>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static FdbKeyPrefixRange<FdbTupleKey<T1>> ToRange<T1>(this IDynamicKeySubspace subspace, T1 item1)
			=> new(new(subspace, item1), excluded: true);

		/// <summary>Returns a range that matches all the keys under this subspace that start with the given first two elements</summary>
		/// <param name="subspace">Subspace that contains the key</param>
		/// <param name="item1">Value of the 1st element of the matched keys</param>
		/// <param name="item2">Value of the 2nd element of the matched keys</param>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static FdbKeyPrefixRange<FdbTupleKey<T1, T2>> ToRange<T1, T2>(this IDynamicKeySubspace subspace, T1 item1, T2 item2)
			=> new(new(subspace, item1, item2), excluded: true);

		/// <summary>Returns a range that matches all the keys under this subspace that start with the given first three elements</summary>
		/// <param name="subspace">Subspace that contains the key</param>
		/// <param name="item1">Value of the 1st element of the matched keys</param>
		/// <param name="item2">Value of the 2nd element of the matched keys</param>
		/// <param name="item3">value of the 3rd element of the matched keys</param>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static FdbKeyPrefixRange<FdbTupleKey<T1, T2, T3>> ToRange<T1, T2, T3>(this IDynamicKeySubspace subspace, T1 item1, T2 item2, T3 item3)
			=> new(new(subspace, item1, item2, item3), excluded: true);

		/// <summary>Returns a range that matches all the keys under this subspace that start with the given first four elements</summary>
		/// <param name="subspace">Subspace that contains the key</param>
		/// <param name="item1">Value of the 1st element of the matched keys</param>
		/// <param name="item2">Value of the 2nd element of the matched keys</param>
		/// <param name="item3">value of the 3rd element of the matched keys</param>
		/// <param name="item4">value of the 4th element of the matched keys</param>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static FdbKeyPrefixRange<FdbTupleKey<T1, T2, T3, T4>> ToRange<T1, T2, T3, T4>(this IDynamicKeySubspace subspace, T1 item1, T2 item2, T3 item3, T4 item4)
			=> new(new(subspace, item1, item2, item3, item4), excluded: true);

		/// <summary>Returns a range that matches all the keys under this subspace that start with the given first five elements</summary>
		/// <param name="subspace">Subspace that contains the key</param>
		/// <param name="item1">Value of the 1st element of the matched keys</param>
		/// <param name="item2">Value of the 2nd element of the matched keys</param>
		/// <param name="item3">value of the 3rd element of the matched keys</param>
		/// <param name="item4">value of the 4th element of the matched keys</param>
		/// <param name="item5">value of the 5th element of the matched keys</param>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static FdbKeyPrefixRange<FdbTupleKey<T1, T2, T3, T4, T5>> ToRange<T1, T2, T3, T4, T5>(this IDynamicKeySubspace subspace, T1 item1, T2 item2, T3 item3, T4 item4, T5 item5)
			=> new(new(subspace, item1, item2, item3, item4, item5), excluded: true);

		/// <summary>Returns a range that matches all the keys under this subspace that start with the given first six elements</summary>
		/// <param name="subspace">Subspace that contains the key</param>
		/// <param name="item1">Value of the 1st element of the matched keys</param>
		/// <param name="item2">Value of the 2nd element of the matched keys</param>
		/// <param name="item3">value of the 3rd element of the matched keys</param>
		/// <param name="item4">value of the 4th element of the matched keys</param>
		/// <param name="item5">value of the 5th element of the matched keys</param>
		/// <param name="item6">value of the 6th element of the matched keys</param>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static FdbKeyPrefixRange<FdbTupleKey<T1, T2, T3, T4, T5, T6>> ToRange<T1, T2, T3, T4, T5, T6>(this IDynamicKeySubspace subspace, T1 item1, T2 item2, T3 item3, T4 item4, T5 item5, T6 item6)
			=> new(new(subspace, item1, item2, item3, item4, item5, item6), excluded: true);

		/// <summary>Returns a range that matches all the keys under this subspace that start with the given first seven elements</summary>
		/// <param name="subspace">Subspace that contains the key</param>
		/// <param name="item1">Value of the 1st element of the matched keys</param>
		/// <param name="item2">Value of the 2nd element of the matched keys</param>
		/// <param name="item3">value of the 3rd element of the matched keys</param>
		/// <param name="item4">value of the 4th element of the matched keys</param>
		/// <param name="item5">value of the 5th element of the matched keys</param>
		/// <param name="item6">value of the 6th element of the matched keys</param>
		/// <param name="item7">value of the 7th element of the matched keys</param>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static FdbKeyPrefixRange<FdbTupleKey<T1, T2, T3, T4, T5, T6, T7>> ToRange<T1, T2, T3, T4, T5, T6, T7>(this IDynamicKeySubspace subspace, T1 item1, T2 item2, T3 item3, T4 item4, T5 item5, T6 item6, T7 item7)
			=> new(new(subspace, item1, item2, item3, item4, item5, item6, item7), excluded: true);

		/// <summary>Returns a range that matches all the keys under this subspace that start with the given first eight elements</summary>
		/// <param name="subspace">Subspace that contains the key</param>
		/// <param name="item1">Value of the 1st element of the matched keys</param>
		/// <param name="item2">Value of the 2nd element of the matched keys</param>
		/// <param name="item3">value of the 3rd element of the matched keys</param>
		/// <param name="item4">value of the 4th element of the matched keys</param>
		/// <param name="item5">value of the 5th element of the matched keys</param>
		/// <param name="item6">value of the 6th element of the matched keys</param>
		/// <param name="item7">value of the 7th element of the matched keys</param>
		/// <param name="item8">value of the 8th element of the matched keys</param>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static FdbKeyPrefixRange<FdbTupleKey<T1, T2, T3, T4, T5, T6, T7, T8>> ToRange<T1, T2, T3, T4, T5, T6, T7, T8>(this IDynamicKeySubspace subspace, T1 item1, T2 item2, T3 item3, T4 item4, T5 item5, T6 item6, T7 item7, T8 item8)
			=> new(new(subspace, item1, item2, item3, item4, item5, item6, item7, item8), excluded: true);

		#endregion

		#region ITypedKeySubspace<...>.GetKey(...)

		// T1

		/// <summary>Returns a key in this subspace</summary>
		/// <param name="subspace">Parent subspace</param>
		/// <param name="item1">First part of the key</param>
		[Obsolete]
		public static FdbTupleKey<T1> GetKey<T1>(this ITypedKeySubspace<T1> subspace, T1 item1) => new(subspace, item1);

		// T1, T2

		/// <summary>Returns a key in this subspace</summary>
		/// <param name="subspace">Parent subspace</param>
		/// <param name="item1">First part of the key</param>
		/// <param name="item2">Second part of the key</param>
		[Obsolete]
		public static FdbTupleKey<T1, T2> GetKey<T1, T2>(this ITypedKeySubspace<T1, T2> subspace, T1 item1, T2 item2) => new(subspace, item1, item2);

		/// <summary>Returns a key in this subspace</summary>
		/// <param name="subspace">Parent subspace</param>
		/// <param name="items">Parts of the key</param>
		[Obsolete]
		public static FdbTupleKey<T1, T2> GetKey<T1, T2>(this ITypedKeySubspace<T1, T2> subspace, in ValueTuple<T1, T2> items) => new(subspace, in items);

		/// <summary>Returns a key in this subspace</summary>
		/// <param name="subspace">Parent subspace</param>
		/// <param name="items">Parts of the key</param>
		[Obsolete]
		public static FdbTupleKey<T1, T2> GetKey<T1, T2>(this ITypedKeySubspace<T1, T2> subspace, in STuple<T1, T2> items) => new(subspace, in items);

		// T1, T2, T3

		/// <summary>Returns a key in this subspace</summary>
		/// <param name="subspace">Parent subspace</param>
		/// <param name="item1">First part of the key</param>
		/// <param name="item2">Second part of the key</param>
		/// <param name="item3">Third part of the key</param>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		[Obsolete]
		public static FdbTupleKey<T1, T2, T3> GetKey<T1, T2, T3>(this ITypedKeySubspace<T1, T2, T3> subspace, T1 item1, T2 item2, T3 item3) => new(subspace, item1, item2, item3);

		/// <summary>Returns a key in this subspace</summary>
		/// <param name="subspace">Parent subspace</param>
		/// <param name="items">Parts of the key</param>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		[Obsolete]
		public static FdbTupleKey<T1, T2, T3> GetKey<T1, T2, T3>(this ITypedKeySubspace<T1, T2, T3> subspace, in ValueTuple<T1, T2, T3> items) => new(subspace, in items);

		/// <summary>Returns a key in this subspace</summary>
		/// <param name="subspace">Parent subspace</param>
		/// <param name="items">Parts of the key</param>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		[Obsolete]
		public static FdbTupleKey<T1, T2, T3> GetKey<T1, T2, T3>(this ITypedKeySubspace<T1, T2, T3> subspace, in STuple<T1, T2, T3> items) => new(subspace, in items);

		// T1, T2, T3, T4

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		[Obsolete]
		public static FdbTupleKey<T1, T2, T3, T4> GetKey<T1, T2, T3, T4>(this ITypedKeySubspace<T1, T2, T3, T4> subspace, T1 item1, T2 item2, T3 item3, T4 item4) => new(subspace, item1, item2, item3, item4);

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		[Obsolete]
		public static FdbTupleKey<T1, T2, T3, T4> GetKey<T1, T2, T3, T4>(this ITypedKeySubspace<T1, T2, T3, T4> subspace, in ValueTuple<T1, T2, T3, T4> items) => new(subspace, in items);

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		[Obsolete]
		public static FdbTupleKey<T1, T2, T3, T4> GetKey<T1, T2, T3, T4>(this ITypedKeySubspace<T1, T2, T3, T4> subspace, in STuple<T1, T2, T3, T4> items) => new(subspace, in items);

		#endregion

		#region ITypedKeySubspace<...>.ToRange(...)

		// T1, T2

		/// <summary>Returns a range that matches all the elements in this subspace that starts with the given <typeparamref name="T1"/> value</summary>
		/// <param name="subspace">Parent subspace</param>
		/// <param name="item1">First part of the key</param>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		[Obsolete]
		public static FdbKeyPrefixRange<FdbTupleKey<T1>> ToRange<T1, T2>(this ITypedKeySubspace<T1, T2> subspace, T1 item1) => new(new(subspace, item1), excluded: true);

		// T1, T2, T3

		/// <summary>Returns a range that matches all the elements in this subspace that starts with the given <typeparamref name="T1"/> value</summary>
		/// <param name="subspace">Parent subspace</param>
		/// <param name="item1">First part of the key</param>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		[Obsolete]
		public static FdbKeyPrefixRange<FdbTupleKey<T1>> ToRange<T1, T2, T3>(this ITypedKeySubspace<T1, T2, T3> subspace, T1 item1) => new(new(subspace, item1), excluded: true);

		/// <summary>Returns a range that matches all the elements in this subspace that starts with the given <typeparamref name="T1"/> and <typeparamref name="T2"/> values</summary>
		/// <param name="subspace">Parent subspace</param>
		/// <param name="item1">First part of the key</param>
		/// <param name="item2">Second part of the key</param>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		[Obsolete]
		public static FdbKeyPrefixRange<FdbTupleKey<T1, T2>> ToRange<T1, T2, T3>(this ITypedKeySubspace<T1, T2, T3> subspace, T1 item1, T2 item2) => new(new(subspace, item1, item2), excluded: true);

		// T1, T2, T3, T4

		/// <summary>Returns a range that matches all the elements in this subspace that starts with the given <typeparamref name="T1"/> value</summary>
		/// <param name="subspace">Parent subspace</param>
		/// <param name="item1">First part of the key</param>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		[Obsolete]
		public static FdbKeyPrefixRange<FdbTupleKey<T1>> ToRange<T1, T2, T3, T4>(this ITypedKeySubspace<T1, T2, T3, T4> subspace, T1 item1) => new(new(subspace, item1), excluded: true);

		/// <summary>Returns a range that matches all the elements in this subspace that starts with the given <typeparamref name="T1"/> and <typeparamref name="T2"/> values</summary>
		/// <param name="subspace">Parent subspace</param>
		/// <param name="item1">First part of the key</param>
		/// <param name="item2">Second part of the key</param>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		[Obsolete]
		public static FdbKeyPrefixRange<FdbTupleKey<T1, T2>> ToRange<T1, T2, T3, T4>(this ITypedKeySubspace<T1, T2, T3, T4> subspace, T1 item1, T2 item2) => new(new(subspace, item1, item2), excluded: true);

		/// <summary>Returns a range that matches all the elements in this subspace that starts with the given <typeparamref name="T1"/>, <typeparamref name="T2"/> and <typeparamref name="T3"/> values</summary>
		/// <param name="subspace">Parent subspace</param>
		/// <param name="item1">First part of the key</param>
		/// <param name="item2">Second part of the key</param>
		/// <param name="item3">Third part of the key</param>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		[Obsolete]
		public static FdbKeyPrefixRange<FdbTupleKey<T1, T2, T3>> ToRange<T1, T2, T3, T4>(this ITypedKeySubspace<T1, T2, T3, T4> subspace, T1 item1, T2 item2, T3 item3) => new(new(subspace, item1, item2, item3), excluded: true);

		#endregion

		#region IKeySubspace.AppendBytes(...)

		/// <summary>Returns a key that adds a binary suffix to the subspace's prefix</summary>
		/// <param name="subspace">Parent subspace</param>
		/// <param name="relativeKey">Binary suffix</param>
		/// <returns>Key that will output the subspace prefix, followed by the binary suffix</returns>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static FdbSuffixKey AppendBytes(this IKeySubspace subspace, ReadOnlySpan<byte> relativeKey)
		{
			Contract.NotNull(subspace);

			return new(subspace, Slice.FromBytes(relativeKey));
		}

		/// <summary>Returns a key that adds a binary suffix to the subspace's prefix</summary>
		/// <param name="subspace">Parent subspace</param>
		/// <param name="relativeKey">Binary suffix</param>
		/// <returns>Key that will output the subspace prefix, followed by the binary suffix</returns>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static FdbSuffixKey AppendBytes(this IKeySubspace subspace, Slice relativeKey)
		{
			Contract.NotNull(subspace);

			return new(subspace, relativeKey);
		}

		/// <summary>Returns a key that adds a binary suffix to the subspace's prefix</summary>
		/// <param name="subspace">Parent subspace</param>
		/// <param name="relativeKey">Binary suffix</param>
		/// <returns>Key that will output the subspace prefix, followed by the binary suffix</returns>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static FdbSuffixKey AppendBytes(this IKeySubspace subspace, byte[]? relativeKey)
		{
			Contract.NotNull(subspace);

			return new(subspace, relativeKey.AsSlice());
		}

		/// <summary>Returns a key that adds a tuple as a binary suffix to the subspace's prefix</summary>
		/// <param name="subspace">Parent subspace</param>
		/// <param name="tuple">Tuple that will be added as a suffix</param>
		/// <returns>Key that will output the subspace prefix, followed by the binary suffix</returns>
		[Pure]
		public static FdbTupleKey AppendBytes(this IKeySubspace subspace, IVarTuple tuple)
		{
			Contract.NotNull(subspace);
			Contract.NotNull(tuple);

			return new(subspace, tuple);
		}

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static FdbSuffixKey<TKey> AppendBytes<TKey>(this TKey key, Slice suffix)
			where TKey : struct, IFdbKey
			=> new(key, suffix);

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static FdbSuffixKey<TKey> AppendBytes<TKey>(this TKey key, byte[] suffix)
			where TKey : struct, IFdbKey
			=> new(key, suffix.AsSlice());

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static FdbSuffixKey<TKey> AppendBytes<TKey>(this TKey key, ReadOnlySpan<byte> suffix)
			where TKey : struct, IFdbKey
			=> new(key, Slice.FromBytes(suffix));

		#endregion

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static FdbTupleKey[] PackKeys<TItem>(this IDynamicKeySubspace subspace, ReadOnlySpan<TItem> items, Func<TItem, IVarTuple> selector)
		{
			if (items.Length == 0)
			{
				return [ ];
			}

			var res = new FdbTupleKey[items.Length];
			for(int i = 0; i < items.Length; i++)
			{
				res[i] = new(subspace, selector(items[i]));
			}
			return res;
		}

		#endregion

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

	}

	internal static class FdbKeyHelpers
	{

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
