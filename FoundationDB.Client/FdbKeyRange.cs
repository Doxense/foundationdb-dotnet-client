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

		public static FdbKeyRange<TBegin, TEnd> Create<TBegin, TEnd>(TBegin begin, TEnd end)
			where TBegin : struct, IFdbKey
			where TEnd : struct, IFdbKey
			=> new(begin, end);

		public static FdbKeyRange<TKey> StartsWith<TKey>(in TKey prefix)
			where TKey : struct, IFdbKey
			=> new(prefix, excluded: false);

		public static FdbKeyRange<TKey> PrefixedBy<TKey>(in TKey prefix)
			where TKey : struct, IFdbKey
			=> new(prefix, excluded: true);

		public static FdbHeadKeyRange<TKey> Head<TKey>(in TKey cursor, bool excluded)
			where TKey : struct, IFdbKey
			=> new(cursor, excluded);

		public static FdbTailKeyRange<TKey> Tail<TKey>(in TKey cursor, bool excluded)
			where TKey : struct, IFdbKey
			=> new(cursor, excluded);

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
			=> new(this.Begin.ToSlice(), this.End.ToSlice());

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

	public readonly struct FdbKeyRange<TKey> : IFdbKeyRange
		where TKey : struct, IFdbKey
	{

		public FdbKeyRange(TKey prefix, bool excluded)
		{
			this.Prefix = prefix;
			this.Excluded = excluded;
		}

		public readonly TKey Prefix;

		public readonly bool Excluded;

		/// <inheritdoc />
		IKeySubspace? IFdbKeyRange.GetSubspace() => this.Prefix.GetSubspace();

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public KeyRange ToKeyRange() => this.Excluded
			//PERF: TODO: optimize this!
			? KeyRange.PrefixedBy(this.Prefix.ToSlice())
			: KeyRange.StartsWith(this.Prefix.ToSlice());

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
		public FdbKeySelector<FdbNextKey<TKey>> GetEndSelector()
			=> FdbKeySelector.FirstGreaterThan(this.Prefix.Increment());

		/// <summary>Returns a <see cref="KeySelector"/> that will match the first key in the range (inclusive)</summary>
		/// <remarks>This can be passed as the "begin" selector to <see cref="IFdbReadOnlyTransaction.GetRange"/>.</remarks>
		public KeySelector ToBeginSelector() => this.Excluded
			? FdbKeySelector.FirstGreaterThan(this.Prefix).ToSelector()
			: FdbKeySelector.FirstGreaterOrEqual(this.Prefix).ToSelector();

		/// <summary>Returns a <see cref="KeySelector"/> that will match the last key in the range (exclusive)</summary>
		/// <remarks>This can be passed as the "end" selector to <see cref="IFdbReadOnlyTransaction.GetRange"/>.</remarks>
		public KeySelector ToEndSelector()
			=> FdbKeySelector.FirstGreaterThan(this.Prefix.Increment()).ToSelector();

	}

	/// <summary>Range that maps all the keys from a cursor key, up to the end of its subspace</summary>
	/// <typeparam name="TKey">Type of the key</typeparam>
	public readonly struct FdbTailKeyRange<TKey> : IFdbKeyRange
		where TKey : struct, IFdbKey
	{

		public FdbTailKeyRange(TKey cursor, bool excluded)
		{
			Contract.Debug.Requires(cursor.GetSubspace() is not null);
			this.Cursor = cursor;
			this.Excluded = excluded;
		}

		public readonly TKey Cursor;

		public readonly bool Excluded;

		/// <inheritdoc />
		IKeySubspace? IFdbKeyRange.GetSubspace() => this.Cursor.GetSubspace();

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public KeyRange ToKeyRange()
		{
			var cursorBytes = this.Cursor.ToSlice();
			var endBytes = FdbKey.Increment(this.Cursor.GetSubspace()!.GetPrefix());
			return new(cursorBytes, endBytes);
		}

	}

	/// <summary>Range that maps all the keys from the start of a subspace up to the specified cursor</summary>
	/// <typeparam name="TKey">Type of the key</typeparam>
	public readonly struct FdbHeadKeyRange<TKey> : IFdbKeyRange
		where TKey : struct, IFdbKey
	{

		public FdbHeadKeyRange(TKey cursor, bool excluded)
		{
			Contract.Debug.Requires(cursor.GetSubspace() is not null);
			this.Cursor = cursor;
			this.Excluded = excluded;
		}

		public readonly TKey Cursor;

		public readonly bool Excluded;

		/// <inheritdoc />
		IKeySubspace? IFdbKeyRange.GetSubspace() => this.Cursor.GetSubspace();

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public KeyRange ToKeyRange()
		{
			var beginBytes = this.Cursor.GetSubspace()!.GetPrefix();

			var cursorBytes = this.Cursor.ToSlice();
			if (!this.Excluded) cursorBytes = FdbKey.Increment(cursorBytes);

			return new(beginBytes, cursorBytes);
		}

	}

}

