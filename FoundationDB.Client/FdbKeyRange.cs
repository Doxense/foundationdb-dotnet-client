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

	public static class FdbKeyRange
	{

		public static FdbRawKeyRange Create(Slice begin, Slice end) => new(begin, end);

		[OverloadResolutionPriority(1)]
		public static FdbKeyRange<TKey> Create<TKey>(TKey begin, TKey end)
			where TKey : struct, IFdbKey
			=> new(begin, end);

		public static FdbKeyRange<TBegin, TEnd> Create<TBegin, TEnd>(TBegin begin, TEnd end)
			where TBegin : struct, IFdbKey
			where TEnd : struct, IFdbKey
			=> new(begin, end);

		public static FdbKeyPrefixRange<TKey> StartsWith<TKey>(in TKey prefix, bool inclusive = false)
			where TKey : struct, IFdbKey
			=> new(prefix, inclusive);

		public static FdbSingleKeyRange<TKey> Single<TKey>(in TKey cursor)
			where TKey : struct, IFdbKey
			=> new(cursor);

		#region Between...

		/// <summary>Returns a range that will match all keys between this lower bound key and the given upper bound key</summary>
		/// <typeparam name="TFromKey">Type of the lower bound key</typeparam>
		/// <typeparam name="TToKey">Type of the upper bound key</typeparam>
		/// <param name="fromKey">Inclusive lower bound (relative to this key), or <c>null</c> to read from the start of the range</param>
		/// <param name="toKey">Exclusive upper bound (relative to this key)</param>
		public static FdbBetweenRange<TFromKey, TToKey> Between<TFromKey, TToKey>(TFromKey fromKey, in TToKey toKey)
			where TFromKey : struct, IFdbKey
			where TToKey : struct, IFdbKey
		{
			return new(fromKey, fromInclusive: true, toKey, toInclusive: false);
		}

		/// <summary>Returns a range that will match all keys between this lower bound key and the given upper bound key</summary>
		/// <typeparam name="TFromKey">Type of the lower bound key</typeparam>
		/// <typeparam name="TToKey">Type of the upper bound key</typeparam>
		/// <param name="fromKey">Lower bound key</param>
		/// <param name="fromInclusive">Specifies whether the lower bound is included in the range</param>
		/// <param name="toKey">Upper bound key</param>
		/// <param name="toInclusive">Specifies whether the upper bound is included in the range</param>
		public static FdbBetweenRange<TFromKey, TToKey> Between<TFromKey, TToKey>(in TFromKey fromKey, bool fromInclusive, TToKey toKey, bool toInclusive)
			where TFromKey : struct, IFdbKey
			where TToKey : struct, IFdbKey
		{
			return new(fromKey, fromInclusive, toKey, toInclusive);
		}

		#endregion

	}

	public interface IFdbKeyRange
	{

		[Pure]
		IKeySubspace? GetSubspace();

		[Pure]
		KeyRange ToKeyRange(); //REVIEW: change this to an extension?

	}

	public readonly struct FdbRawKeyRange : IFdbKeyRange
	{

		[SkipLocalsInit, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public FdbRawKeyRange(FdbRawKey begin, FdbRawKey end)
		{
			this.Begin = begin;
			this.End = end;
		}

		public readonly FdbRawKey Begin;

		public readonly FdbRawKey End;

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public KeyRange ToKeyRange() => new(this.Begin.Data, this.End.Data);

		/// <inheritdoc />
		IKeySubspace? IFdbKeyRange.GetSubspace() => null;

		/// <summary>Returns a <see cref="FdbKeySelector{FdbRawKey}"/> that will match the first key in the range (inclusive)</summary>
		/// <remarks>This can be passed as the "begin" selector to <see cref="IFdbReadOnlyTransaction.GetRange"/>.</remarks>
		public FdbKeySelector<FdbRawKey> GetBeginSelector() => FdbKeySelector.FirstGreaterOrEqual(this.Begin);

		/// <summary>Returns a <see cref="FdbKeySelector{FdbRawKey}"/> that will match the last key in the range (exclusive)</summary>
		/// <remarks>This can be passed as the "end" selector to <see cref="IFdbReadOnlyTransaction.GetRange"/>.</remarks>
		public FdbKeySelector<FdbRawKey> GetEndSelector() => FdbKeySelector.FirstGreaterThan(this.End);

		/// <summary>Returns a <see cref="KeySelector"/> that will match the first key in the range (inclusive)</summary>
		/// <remarks>This can be passed as the "begin" selector to <see cref="IFdbReadOnlyTransaction.GetRange"/>.</remarks>
		public KeySelector ToBeginSelector() => KeySelector.FirstGreaterOrEqual(this.Begin.Data);

		/// <summary>Returns a <see cref="KeySelector"/> that will match the last key in the range (exclusive)</summary>
		/// <remarks>This can be passed as the "end" selector to <see cref="IFdbReadOnlyTransaction.GetRange"/>.</remarks>
		public KeySelector ToEndSelector() => KeySelector.FirstGreaterThan(this.End.Data);

	}

	public readonly struct FdbKeyRange<TKey> : IFdbKeyRange
		where TKey : struct, IFdbKey
	{

		[SkipLocalsInit, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public FdbKeyRange(TKey begin, TKey end)
		{
			this.Begin = begin;
			this.End = end;
		}

		public readonly TKey Begin;

		public readonly TKey End;

		/// <inheritdoc />
		IKeySubspace? IFdbKeyRange.GetSubspace() => this.Begin.GetSubspace() ?? this.End.GetSubspace();

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public KeyRange ToKeyRange()
			=> new(FdbKeyHelpers.ToSlice(in this.Begin), FdbKeyHelpers.ToSlice(in this.End));

		/// <summary>Returns a <see cref="FdbKeySelector{FdbRawKey}"/> that will match the first key in the range (inclusive)</summary>
		/// <remarks>This can be passed as the "begin" selector to <see cref="IFdbReadOnlyTransaction.GetRange"/>.</remarks>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public FdbKeySelector<TKey> GetBeginSelector()
			=> FdbKeySelector.FirstGreaterOrEqual(this.Begin);

		/// <summary>Returns a <see cref="FdbKeySelector{FdbRawKey}"/> that will match the last key in the range (exclusive)</summary>
		/// <remarks>This can be passed as the "end" selector to <see cref="IFdbReadOnlyTransaction.GetRange"/>.</remarks>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public FdbKeySelector<TKey> GetEndSelector()
			=> FdbKeySelector.FirstGreaterOrEqual(this.End);

		/// <summary>Returns a <see cref="KeySelector"/> that will match the first key in the range (inclusive)</summary>
		/// <remarks>This can be passed as the "begin" selector to <see cref="IFdbReadOnlyTransaction.GetRange"/>.</remarks>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public KeySelector ToBeginSelector()
			=> FdbKeySelector.FirstGreaterOrEqual(this.Begin).ToSelector();

		/// <summary>Returns a <see cref="KeySelector"/> that will match the last key in the range (exclusive)</summary>
		/// <remarks>This can be passed as the "end" selector to <see cref="IFdbReadOnlyTransaction.GetRange"/>.</remarks>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public KeySelector ToEndSelector()
			=> FdbKeySelector.FirstGreaterOrEqual(this.End).ToSelector();

	}

	public readonly struct FdbKeyRange<TBegin, TEnd> : IFdbKeyRange
		where TBegin : struct, IFdbKey
		where TEnd : struct, IFdbKey
	{

		[SkipLocalsInit, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public FdbKeyRange(TBegin begin, TEnd end)
		{
			this.Begin = begin;
			this.End = end;
		}

		public readonly TBegin Begin;

		public readonly TEnd End;

		/// <inheritdoc />
		IKeySubspace? IFdbKeyRange.GetSubspace() => this.Begin.GetSubspace() ?? this.End.GetSubspace();

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public KeyRange ToKeyRange()
			=> new(FdbKeyHelpers.ToSlice(in this.Begin), FdbKeyHelpers.ToSlice(in this.End));

		/// <summary>Returns a <see cref="FdbKeySelector{FdbRawKey}"/> that will match the first key in the range (inclusive)</summary>
		/// <remarks>This can be passed as the "begin" selector to <see cref="IFdbReadOnlyTransaction.GetRange"/>.</remarks>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public FdbKeySelector<TBegin> GetBeginSelector()
			=> FdbKeySelector.FirstGreaterOrEqual(this.Begin);

		/// <summary>Returns a <see cref="FdbKeySelector{FdbRawKey}"/> that will match the last key in the range (exclusive)</summary>
		/// <remarks>This can be passed as the "end" selector to <see cref="IFdbReadOnlyTransaction.GetRange"/>.</remarks>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public FdbKeySelector<TEnd> GetEndSelector()
			=> FdbKeySelector.FirstGreaterThan(this.End);

		/// <summary>Returns a <see cref="KeySelector"/> that will match the first key in the range (inclusive)</summary>
		/// <remarks>This can be passed as the "begin" selector to <see cref="IFdbReadOnlyTransaction.GetRange"/>.</remarks>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public KeySelector ToBeginSelector()
			=> FdbKeySelector.FirstGreaterOrEqual(this.Begin).ToSelector();

		/// <summary>Returns a <see cref="KeySelector"/> that will match the last key in the range (exclusive)</summary>
		/// <remarks>This can be passed as the "end" selector to <see cref="IFdbReadOnlyTransaction.GetRange"/>.</remarks>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public KeySelector ToEndSelector()
			=> FdbKeySelector.FirstGreaterThan(this.End).ToSelector();

	}
	
	/// <summary>Range that captures all the keys that have a common prefix</summary>
	/// <typeparam name="TKey">Type of the key that act as the prefix</typeparam>
	public readonly struct FdbKeyPrefixRange<TKey> : IFdbKeyRange
		where TKey : struct, IFdbKey
	{

		[SkipLocalsInit, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public FdbKeyPrefixRange(in TKey prefix, bool inclusive = false)
		{
			this.Prefix = prefix;
			this.IsInclusive = inclusive;
		}

		/// <summary>Common prefix for all the keys in this range</summary>
		public readonly TKey Prefix;

		/// <summary>If <c>true</c>, the prefix itself is included in the range</summary>
		public readonly bool IsInclusive;

		/// <inheritdoc />
		IKeySubspace? IFdbKeyRange.GetSubspace() => this.Prefix.GetSubspace();

		/// <summary>Returns a version of this range that includes the <see cref="Prefix"/> in the results</summary>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public FdbKeyPrefixRange<TKey> Inclusive() => new(this.Prefix, inclusive: true);

		/// <summary>Returns a version of this range that excludes the <see cref="Prefix"/> from the results</summary>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public FdbKeyPrefixRange<TKey> Exclusive() => new(this.Prefix, inclusive: false);

		/// <summary>Tests if a key is contained in this range</summary>
		/// <param name="key">Key to test</param>
		/// <returns><c>true</c> if the key would be matched by this range</returns>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool Contains(Slice key) => Contains(key.Span);

		/// <summary>Tests if a key is contained in this range</summary>
		/// <param name="key">Key to test</param>
		/// <returns><c>true</c> if the key would be matched by this range</returns>
		[Pure]
		public bool Contains(ReadOnlySpan<byte> key)
		{
			if (this.Prefix.TryGetSpan(out var span))
			{
				return key.StartsWith(span) && (this.IsInclusive || key.Length != span.Length);
			}
			using var prefixBytes = FdbKeyHelpers.Encode(in Prefix, ArrayPool<byte>.Shared);
			return key.StartsWith(prefixBytes.Span) && (this.IsInclusive || key.Length != prefixBytes.Count);
		}

		/// <summary>Tests if a key is strictly before this range</summary>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool IsBefore(Slice key) => IsBefore(key.Span);

		/// <summary>Tests if a key is strictly before this range</summary>
		[Pure]
		public bool IsBefore(ReadOnlySpan<byte> key)
		{
			if (this.Prefix.TryGetSpan(out var span))
			{
				int cmp = span.SequenceCompareTo(key);
				return (cmp > 0) || (cmp == 0 && !this.IsInclusive);
			}
			else
			{
				using var prefixBytes = FdbKeyHelpers.Encode(in Prefix, ArrayPool<byte>.Shared);
				int cmp = prefixBytes.Span.SequenceCompareTo(key);
				return (cmp > 0) || (cmp == 0 && !this.IsInclusive);
			}
		}

		/// <summary>Tests if a key is after this range</summary>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool IsAfter(Slice key) => IsAfter(key.Span);

		/// <summary>Tests if a key is after this range</summary>
		[Pure]
		public bool IsAfter(ReadOnlySpan<byte> key)
		{
			if (this.Prefix.TryGetSpan(out var span))
			{
				int cmp = span.SequenceCompareTo(key);
				return cmp < 0 && (key.Length <= span.Length || !key[..span.Length].SequenceEqual(span));
			}
			else
			{
				using var prefixBytes = FdbKeyHelpers.Encode(in Prefix, ArrayPool<byte>.Shared);
				int cmp = prefixBytes.Span.SequenceCompareTo(key);
				return cmp < 0 && (key.Length <= prefixBytes.Count || !key[..prefixBytes.Count].SequenceEqual(prefixBytes.Span));
			}
		}

		/// <summary>Converts this range into a binary encoded <see cref="KeyRange"/></summary>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public KeyRange ToKeyRange() => this.IsInclusive
			//PERF: TODO: optimize this!
			? KeyRange.StartsWith(FdbKeyHelpers.ToSlice(in this.Prefix))
			: KeyRange.PrefixedBy(FdbKeyHelpers.ToSlice(in this.Prefix));

		/// <summary>Returns a <see cref="FdbKeySelector{FdbRawKey}"/> that will match the first key in the range (inclusive)</summary>
		/// <remarks>This can be passed as the "begin" selector to <see cref="IFdbReadOnlyTransaction.GetRange"/>.</remarks>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public FdbKeySelector<TKey> GetBeginSelector()
			=> this.IsInclusive
				? FdbKeySelector.FirstGreaterOrEqual(this.Prefix)
				: FdbKeySelector.FirstGreaterThan(this.Prefix);

		/// <summary>Returns a <see cref="FdbKeySelector{FdbRawKey}"/> that will match the last key in the range (exclusive)</summary>
		/// <remarks>This can be passed as the "end" selector to <see cref="IFdbReadOnlyTransaction.GetRange"/>.</remarks>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public FdbKeySelector<FdbNextSiblingKey<TKey>> GetEndSelector()
			=> FdbKeySelector.FirstGreaterThan(this.Prefix.GetNextSibling());

		/// <summary>Returns a <see cref="KeySelector"/> that will match the first key in the range (inclusive)</summary>
		/// <remarks>This can be passed as the "begin" selector to <see cref="IFdbReadOnlyTransaction.GetRange"/>.</remarks>
		public KeySelector ToBeginSelector() => this.IsInclusive
			? FdbKeySelector.FirstGreaterOrEqual(this.Prefix).ToSelector()
			: FdbKeySelector.FirstGreaterThan(this.Prefix).ToSelector();

		/// <summary>Returns a <see cref="KeySelector"/> that will match the last key in the range (exclusive)</summary>
		/// <remarks>This can be passed as the "end" selector to <see cref="IFdbReadOnlyTransaction.GetRange"/>.</remarks>
		public KeySelector ToEndSelector()
			=> FdbKeySelector.FirstGreaterThan(this.Prefix.GetNextSibling()).ToSelector();

	}

	/// <summary>Range that splits a range of keys into two parts using a cursor</summary>
	/// <typeparam name="TFromKey">Type of the lower bound key</typeparam>
	/// <typeparam name="TToKey">Type of the upper bound key</typeparam>
	/// <remarks>This can be used to read either the <i>head</i> of the <i>tail</i> of a queue or log of events.</remarks>
	public readonly struct FdbBetweenRange<TFromKey, TToKey> : IFdbKeyRange
		where TFromKey : struct, IFdbKey
		where TToKey : struct, IFdbKey
	{

		public FdbBetweenRange(in TFromKey from, bool fromInclusive, in TToKey to, bool toInclusive)
		{
			this.From = from;
			this.To = to;
			this.FromInclusive = fromInclusive;
			this.ToInclusive = toInclusive;
		}

		public readonly TFromKey From;
		public readonly TToKey To;

		public readonly bool FromInclusive;
		public readonly bool ToInclusive;

		/// <inheritdoc />
		IKeySubspace? IFdbKeyRange.GetSubspace() => this.From.GetSubspace() ?? this.To.GetSubspace();

		/// <summary>Returns the encoded Begin key (inclusive) for this range</summary>
		[Pure]
		public Slice GetBegin() => this.FromInclusive
			? this.From.ToSlice() //PERF: TODO: Optimize!
			: this.From.GetSuccessor().ToSlice(); //PERF: TODO: Optimize!

		/// <summary>Returns the encoded Begin key (inclusive) for this range</summary>
		[Pure]
		public KeySelector GetBeginSelector() => this.FromInclusive
			? this.From.FirstGreaterOrEqual().ToSelector() //PERF: TODO: Optimize!
			: this.From.FirstGreaterThan().ToSelector(); //PERF: TODO: Optimize!

		/// <summary>Returns the encoded End key (exclusive) for this range</summary>
		[Pure]
		public Slice GetEnd() => this.ToInclusive
			? this.To.GetSuccessor().ToSlice() //PERF: TODO: Optimize!
			: this.To.ToSlice(); //PERF: TODO: Optimize!

		/// <summary>Returns the encoded End key (exclusive) for this range</summary>
		[Pure]
		public KeySelector GetEndSelector() => this.ToInclusive
			? this.To.FirstGreaterThan().ToSelector() //PERF: TODO: Optimize!
			: this.To.FirstGreaterOrEqual().ToSelector(); //PERF: TODO: Optimize!

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public KeyRange ToKeyRange() => KeyRange.Create(GetBegin(), GetEnd());

	}

	public readonly struct FdbSingleKeyRange<TKey> : IFdbKeyRange
		where TKey : struct, IFdbKey
	{

		public FdbSingleKeyRange(in TKey key)
		{
			this.Key = key;
		}

		public readonly TKey Key;


		/// <inheritdoc />
		IKeySubspace? IFdbKeyRange.GetSubspace() => this.Key.GetSubspace();

		/// <inheritdoc />
		public KeyRange ToKeyRange()
		{
			//PERF: TODO: optimize this!
			return KeyRange.FromKey(FdbKeyHelpers.ToSlice(in this.Key));
		}

	}

}
