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

		public static FdbKeyPrefixRange<TKey> StartsWith<TKey>(in TKey prefix)
			where TKey : struct, IFdbKey
			=> new(prefix, excluded: false);

		public static FdbKeyPrefixRange<TKey> PrefixedBy<TKey>(in TKey prefix)
			where TKey : struct, IFdbKey
			=> new(prefix, excluded: true);

		public static FdbSingleKeyRange<TKey> Single<TKey>(in TKey cursor)
			where TKey : struct, IFdbKey
			=> new(cursor);

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

	public readonly struct FdbKeyPrefixRange<TKey> : IFdbKeyRange
		where TKey : struct, IFdbKey
	{

		public FdbKeyPrefixRange(in TKey prefix, bool excluded)
		{
			this.Prefix = prefix;
			this.Excluded = excluded;
		}

		public readonly TKey Prefix;

		public readonly bool Excluded;

		/// <inheritdoc />
		IKeySubspace? IFdbKeyRange.GetSubspace() => this.Prefix.GetSubspace();

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
				return key.StartsWith(span) && (!this.Excluded || key.Length != span.Length);
			}
			using var prefixBytes = FdbKeyHelpers.Encode(in Prefix, ArrayPool<byte>.Shared);
			return key.StartsWith(prefixBytes.Span) && (!this.Excluded || key.Length != prefixBytes.Count);
		}

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool IsBefore(Slice key) => IsBefore(key.Span);

		[Pure]
		public bool IsBefore(ReadOnlySpan<byte> key)
		{
			if (this.Prefix.TryGetSpan(out var span))
			{
				int cmp = span.SequenceCompareTo(key);
				return (cmp > 0) || (cmp == 0 && this.Excluded);
			}
			else
			{
				using var prefixBytes = FdbKeyHelpers.Encode(in Prefix, ArrayPool<byte>.Shared);
				int cmp = prefixBytes.Span.SequenceCompareTo(key);
				return (cmp > 0) || (cmp == 0 && this.Excluded);
			}
		}

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool IsAfter(Slice key) => IsAfter(key.Span);

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

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public KeyRange ToKeyRange() => this.Excluded
			//PERF: TODO: optimize this!
			? KeyRange.PrefixedBy(FdbKeyHelpers.ToSlice(in this.Prefix))
			: KeyRange.StartsWith(FdbKeyHelpers.ToSlice(in this.Prefix));

		/// <summary>Returns a <see cref="FdbKeySelector{FdbRawKey}"/> that will match the first key in the range (inclusive)</summary>
		/// <remarks>This can be passed as the "begin" selector to <see cref="IFdbReadOnlyTransaction.GetRange"/>.</remarks>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public FdbKeySelector<TKey> GetBeginSelector()
			=> this.Excluded
				? FdbKeySelector.FirstGreaterThan(this.Prefix)
				: FdbKeySelector.FirstGreaterOrEqual(this.Prefix);

		/// <summary>Returns a <see cref="FdbKeySelector{FdbRawKey}"/> that will match the last key in the range (exclusive)</summary>
		/// <remarks>This can be passed as the "end" selector to <see cref="IFdbReadOnlyTransaction.GetRange"/>.</remarks>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public FdbKeySelector<FdbNextSiblingKey<TKey>> GetEndSelector()
			=> FdbKeySelector.FirstGreaterThan(this.Prefix.GetNextSibling());

		/// <summary>Returns a <see cref="KeySelector"/> that will match the first key in the range (inclusive)</summary>
		/// <remarks>This can be passed as the "begin" selector to <see cref="IFdbReadOnlyTransaction.GetRange"/>.</remarks>
		public KeySelector ToBeginSelector() => this.Excluded
			? FdbKeySelector.FirstGreaterThan(this.Prefix).ToSelector()
			: FdbKeySelector.FirstGreaterOrEqual(this.Prefix).ToSelector();

		/// <summary>Returns a <see cref="KeySelector"/> that will match the last key in the range (exclusive)</summary>
		/// <remarks>This can be passed as the "end" selector to <see cref="IFdbReadOnlyTransaction.GetRange"/>.</remarks>
		public KeySelector ToEndSelector()
			=> FdbKeySelector.FirstGreaterThan(this.Prefix.GetNextSibling()).ToSelector();

	}

	/// <summary>Range that splits a range of keys into two parts using a cursor</summary>
	/// <typeparam name="TKey">Type of the key</typeparam>
	/// <typeparam name="TTuple">Type of the cursor</typeparam>
	/// <remarks>This can be used to read either the <i>head</i> of the <i>tail</i> of a queue or log of events.</remarks>
	public readonly struct FdbBetweenRange<TKey, TTuple> : IFdbKeyRange
		where TKey : struct, IFdbKey
		where TTuple : IVarTuple
	{

		public FdbBetweenRange(in TKey parent, TTuple from, bool fromInclusive, TTuple to, bool toInclusive)
		{
			this.Parent = parent;
			this.From = from;
			this.To = to;
			this.FromInclusive = fromInclusive;
			this.ToInclusive = toInclusive;
		}

		/// <summary>Parent key, that is used as the prefix for all eligible keys</summary>
		public readonly TKey Parent;

		public readonly TTuple From;
		public readonly TTuple To;

		public readonly bool FromInclusive;
		public readonly bool ToInclusive;

		/// <inheritdoc />
		IKeySubspace? IFdbKeyRange.GetSubspace() => this.Parent.GetSubspace();

		/// <summary>Returns the encoded Begin key (inclusive) for this range</summary>
		[Pure]
		public Slice GetBegin() => (this.From, this.FromInclusive) switch
		{
			(null, _) => this.Parent.ToSlice(),
			(_, true) => this.Parent.Append(this.From).ToSlice(), //PERF: TODO: Optimize!
			(_, false) => this.Parent.Append(this.From).GetSuccessor().ToSlice(), //PERF: TODO: Optimize!
		};

		/// <summary>Returns the encoded Begin key (inclusive) for this range</summary>
		[Pure]
		public KeySelector GetBeginSelector() => (this.From, this.FromInclusive) switch
		{
			(null, _) => this.Parent.FirstGreaterOrEqual().ToSelector(),
			(_, true) => this.Parent.Append(this.From).FirstGreaterOrEqual().ToSelector(), //PERF: TODO: Optimize!
			(_, false) => this.Parent.Append(this.From).FirstGreaterThan().ToSelector(), //PERF: TODO: Optimize!
		};

		/// <summary>Returns the encoded End key (exclusive) for this range</summary>
		[Pure]
		public Slice GetEnd() => (this.To, this.ToInclusive) switch
		{
			(null, _) => this.Parent.GetNextSibling().ToSlice(),
			(_, true) => this.Parent.Append(this.To).GetSuccessor().ToSlice(), //PERF: TODO: Optimize!
			(_, false) => this.Parent.Append(this.To).ToSlice(), //PERF: TODO: Optimize!
		};

		/// <summary>Returns the encoded End key (exclusive) for this range</summary>
		[Pure]
		public KeySelector GetEndSelector() => (this.To, this.ToInclusive) switch
		{
			(null, _) => this.Parent.GetNextSibling().FirstGreaterOrEqual().ToSelector(),
			(_, true) => this.Parent.Append(this.To).FirstGreaterThan().ToSelector(), //PERF: TODO: Optimize!
			(_, false) => this.Parent.Append(this.To).FirstGreaterOrEqual().ToSelector(), //PERF: TODO: Optimize!
		};

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public KeyRange ToKeyRange() => KeyRange.Create(GetBegin(), GetEnd());

	}

	/// <summary>Range that splits a range of keys into two parts using a cursor</summary>
	/// <typeparam name="TKey">Type of the key</typeparam>
	/// <typeparam name="TTuple">Type of the cursor</typeparam>
	/// <remarks>This can be used to read either the <i>head</i> of the <i>tail</i> of a queue or log of events.</remarks>
	public readonly struct FdbTailRange<TKey, TTuple> : IFdbKeyRange
		where TKey : struct, IFdbKey
		where TTuple : IVarTuple
	{

		[SkipLocalsInit, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public FdbTailRange(in TKey parent, TTuple from, bool fromInclusive)
		{
			this.Parent = parent;
			this.From = from;
			this.FromInclusive = fromInclusive;
		}

		/// <summary>Parent key, that is used as the prefix for all eligible keys</summary>
		public readonly TKey Parent;

		public readonly TTuple From;

		public readonly bool FromInclusive;

		/// <inheritdoc />
		IKeySubspace? IFdbKeyRange.GetSubspace() => this.Parent.GetSubspace();

		/// <summary>Includes the cursor in the range</summary>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public FdbHeadRange<TKey, TTuple> Inclusive() => new(this.Parent, this.From, true);

		/// <summary>Excludes the cursor in the range</summary>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public FdbHeadRange<TKey, TTuple> Exclusive() => new(this.Parent, this.From, true);

		/// <summary>Returns the encoded Begin key (inclusive) for this range</summary>
		[Pure]
		public Slice GetBegin() => this.FromInclusive switch
		{
			true => this.Parent.Append(this.From).ToSlice(), //PERF: TODO: Optimize!
			false => this.Parent.Append(this.From).GetSuccessor().ToSlice(), //PERF: TODO: Optimize!
		};

		public KeySelector GetBeginSelector() => this.FromInclusive
			? this.Parent.Append(this.From).FirstGreaterOrEqual().ToSelector() //PERF: TODO: Optimize!
			: this.Parent.Append(this.From).FirstGreaterThan().ToSelector(); //PERF: TODO: Optimize!

		/// <summary>Returns the encoded End key (exclusive) for this range</summary>
		[Pure]
		public Slice GetEnd() => this.Parent.GetNextSibling().ToSlice();

		[Pure]
		public KeySelector GetEndSelector() => this.Parent.GetNextSibling().FirstGreaterOrEqual().ToSelector();

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public KeyRange ToKeyRange() => KeyRange.Create(GetBegin(), GetEnd());

	}

	/// <summary>Range that splits a range of keys into two parts using a cursor</summary>
	/// <typeparam name="TKey">Type of the key</typeparam>
	/// <typeparam name="TTuple">Type of the cursor</typeparam>
	/// <remarks>This can be used to read either the <i>head</i> of the <i>tail</i> of a queue or log of events.</remarks>
	public readonly struct FdbHeadRange<TKey, TTuple> : IFdbKeyRange
		where TKey : struct, IFdbKey
		where TTuple : IVarTuple
	{

		[SkipLocalsInit, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public FdbHeadRange(in TKey parent, TTuple to, bool toInclusive)
		{
			this.Parent = parent;
			this.To = to;
			this.ToInclusive = toInclusive;
		}

		/// <summary>Parent key, that is used as the prefix for all eligible keys</summary>
		public readonly TKey Parent;

		public readonly TTuple To;

		public readonly bool ToInclusive;

		/// <inheritdoc />
		IKeySubspace? IFdbKeyRange.GetSubspace() => this.Parent.GetSubspace();

		/// <summary>Includes the cursor in the range</summary>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public FdbHeadRange<TKey, TTuple> Inclusive() => new(this.Parent, this.To, true);

		/// <summary>Excludes the cursor in the range</summary>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public FdbHeadRange<TKey, TTuple> Exclusive() => new(this.Parent, this.To, true);

		/// <summary>Returns the encoded Begin key (inclusive) for this range</summary>
		[Pure]
		public Slice GetBegin() => this.Parent.ToSlice();

		/// <summary>Returns the encoded Begin key (inclusive) for this range</summary>
		[Pure]
		public KeySelector GetBeginSelector() => this.Parent.FirstGreaterOrEqual().ToSelector();

		/// <summary>Returns the encoded End key (exclusive) for this range</summary>
		[Pure]
		public Slice GetEnd() => this.ToInclusive
			? this.Parent.Append(this.To).GetSuccessor().ToSlice() //PERF: TODO: Optimize!
			: this.Parent.Append(this.To).ToSlice(); //PERF: TODO: Optimize!

		/// <summary>Returns the encoded End key (exclusive) for this range</summary>
		[Pure]
		public KeySelector GetEndSelector() => this.ToInclusive
			? this.Parent.Append(this.To).FirstGreaterThan().ToSelector() //PERF: TODO: Optimize!
			: this.Parent.Append(this.To).FirstGreaterOrEqual().ToSelector(); //PERF: TODO: Optimize!

		[Pure]
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
