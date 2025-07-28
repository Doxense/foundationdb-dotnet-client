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

	public static class FdbKeyRange
	{

		public static FdbKeyRange<TKey, TKey> Single<TKey>(in TKey prefix)
			where TKey : struct, IFdbKey
			=> new(prefix, KeyRangeMode.Inclusive, prefix, KeyRangeMode.Inclusive);

		public static FdbRawKeyRange Between(Slice beginInclusive, Slice endExclusive) => new(beginInclusive, endExclusive);

		/// <summary>Returns a range that will match all keys between this lower bound key and the given upper bound key</summary>
		/// <typeparam name="TFromKey">Type of the lower bound key</typeparam>
		/// <typeparam name="TToKey">Type of the upper bound key</typeparam>
		/// <param name="fromInclusive">Inclusive lower bound (relative to this key), or <c>null</c> to read from the start of the range</param>
		/// <param name="toExclusive">Exclusive upper bound (relative to this key)</param>
		public static FdbKeyRange<TFromKey, TToKey> Between<TFromKey, TToKey>(TFromKey fromInclusive, in TToKey toExclusive)
			where TFromKey : struct, IFdbKey
			where TToKey : struct, IFdbKey
		{
			return new(fromInclusive, lowerMode: KeyRangeMode.Inclusive, toExclusive, upperMode: KeyRangeMode.Exclusive);
		}

		/// <summary>Returns a range that will match all keys between this lower bound key and the given upper bound key</summary>
		/// <typeparam name="TLowerKey">Type of the lower bound key</typeparam>
		/// <typeparam name="TUpperKey">Type of the upper bound key</typeparam>
		/// <param name="lowerKey">Key used to compute the lower bound of the range</param>
		/// <param name="lowerMode">Transformation applied to <paramref name="lowerKey"/></param>
		/// <param name="upperKey">Key used to compute the upper bound of the range</param>
		/// <param name="upperMode">Transformation applied to <paramref name="upperKey"/></param>
		public static FdbKeyRange<TLowerKey, TUpperKey> Between<TLowerKey, TUpperKey>(in TLowerKey lowerKey, KeyRangeMode lowerMode, TUpperKey upperKey, KeyRangeMode upperMode)
			where TLowerKey : struct, IFdbKey
			where TUpperKey : struct, IFdbKey
		{
			return new(lowerKey, lowerMode, upperKey, upperMode);
		}

	}

	/// <summary>Defines how a boundary key should be considered in a <see cref="FdbKeyRange"/></summary>
	[PublicAPI]
	public enum KeyRangeMode
	{
		/// <summary>Use the default behavior for this parameter</summary>
		/// <remarks>This is usually <see cref="Inclusive"/> for Begin keys, and <see cref="Exclusive"/> for End keys</remarks>
		Default = 0,

		/// <summary>The key is included in the range</summary>
		/// <remarks>
		/// <para>If this is the Begin key, it is used as-is.</para>
		/// <para>If this is the End key, its Successor will be used (<c>key.`\x00`)</c></para>
		/// </remarks>
		Inclusive,

		/// <summary>The key is excluded from the range</summary>
		/// <remarks>
		/// <para>If this is the Begin key, its successor will be used (<c>key.`\x00`</c>).</para>
		/// <para>If this is the End key, it is used as-is.</para>
		/// </remarks>
		Exclusive,

		/// <summary>The key and all of its children is not included in the range</summary>
		/// <remarks>
		/// <para>The next sibling will be used for both Begin and End Key (<c>increment(key)</c>)</para>
		/// </remarks>
		NextSibling,

		/// <summary>The key and all of its children that can be represented by tuples are included in the range</summary>
		/// <remarks>
		/// <para>This is not allowed for Begin keys.</para>
		/// <para>If this is the End key, its last valid element will be used (<c>key.`\xFF`</c>)</para>
		/// </remarks>
		Last,
	}

	/// <summary>Range of keys, defined by a lower and upper bound.</summary>
	[PublicAPI]
	public interface IFdbKeyRange : ISpanFormattable
	{

		/// <summary>Encode this range into a binary <see cref="KeyRange"/></summary>
		[Pure]
		KeyRange ToKeyRange();

		/// <summary>Returns the encoded "Begin" key of this range</summary>
		[Pure]
		Slice ToBeginKey();

		/// <summary>Returns the encoded "End" key of this range</summary>
		[Pure]
		Slice ToEndKey();

		/// <summary>Returns a <see cref="KeySelector"/> that will resolve the first key in the range (inclusive)</summary>
		/// <remarks>This can be passed as the "begin" selector to <see cref="IFdbReadOnlyTransaction.GetRange"/>.</remarks>
		[Pure]
		KeySelector ToBeginSelector();

		/// <summary>Returns a <see cref="KeySelector"/> that will resolve the last key in the range (exclusive)</summary>
		/// <remarks>This can be passed as the "end" selector to <see cref="IFdbReadOnlyTransaction.GetRange"/>.</remarks>
		[Pure]
		KeySelector ToEndSelector();

		/// <summary>Tests if this range contains the given key</summary>
		/// <param name="key">Key that is being tested</param>
		/// <returns><c>true</c> if the key would be matched by this range.</returns>
		[Pure]
		bool Contains(ReadOnlySpan<byte> key);

		/// <summary>Tests if this range contains the given key</summary>
		/// <param name="key">Key that is being tested</param>
		/// <returns><c>true</c> if the key would be matched by this range.</returns>
		[Pure]
		bool Contains(Slice key);

		/// <summary>Tests if this range contains the given key</summary>
		/// <param name="key">Key that is being tested</param>
		/// <returns><c>true</c> if the key would be matched by this range.</returns>
		[Pure]
		bool Contains<TKey>(in TKey key) where TKey : struct, IFdbKey;

	}

	/// <summary>Range where the "Begin" and "End" key are already encoded</summary>
	public readonly struct FdbRawKeyRange : IFdbKeyRange
	{

		[SkipLocalsInit, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public FdbRawKeyRange(Slice begin, Slice end)
		{
			this.Begin = begin;
			this.End = end;
		}

		/// <summary>Begin key (inclusive)</summary>
		public readonly Slice Begin;

		/// <summary>End key (exclusive)</summary>
		public readonly Slice End;

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public KeyRange ToKeyRange() => new(this.Begin, this.End);

		/// <inheritdoc />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public Slice ToBeginKey() => this.Begin;

		/// <inheritdoc />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public Slice ToEndKey() => this.End;

		/// <inheritdoc />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public KeySelector ToBeginSelector() => KeySelector.FirstGreaterOrEqual(this.Begin);

		/// <inheritdoc />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public KeySelector ToEndSelector() => KeySelector.FirstGreaterOrEqual(this.End);

		/// <inheritdoc />
		[Pure]
		public bool Contains(Slice key) => key.CompareTo(this.Begin) >= 0 && key.CompareTo(this.End) < 0;

		/// <inheritdoc />
		[Pure]
		public bool Contains(ReadOnlySpan<byte> key) => key.SequenceCompareTo(this.Begin.Span) >= 0 && key.SequenceCompareTo(this.End.Span) < 0;

		/// <inheritdoc />
		[Pure]
		public bool Contains<TKey>(in TKey key)
			where TKey : struct, IFdbKey
		{
			return key.CompareTo(this.Begin) >= 0 && key.CompareTo(this.End) < 0;
		}

		/// <inheritdoc />
		public override string ToString() => ToString(null);

		/// <inheritdoc />
		public string ToString(string? format, IFormatProvider? formatProvider = null)
			=> ToKeyRange().ToString(format, formatProvider);

		/// <inheritdoc />
		public bool TryFormat(Span<char> destination, out int charsWritten, ReadOnlySpan<char> format = default, IFormatProvider? provider = null)
			=> ToKeyRange().TryFormat(destination, out charsWritten, format, provider);

	}

	/// <summary>Range that matches all keys between a lower and upper bound</summary>
	/// <typeparam name="TLowerKey">Type of the lower bound key</typeparam>
	/// <typeparam name="TUpperKey">Type of the upper bound key</typeparam>
	/// <seealso cref="IFdbKeyRange"/>
	/// <seealso cref="KeyRangeMode"/>
	public readonly struct FdbKeyRange<TLowerKey, TUpperKey> : IFdbKeyRange, IEquatable<FdbKeyRange<TLowerKey, TUpperKey>>
		where TLowerKey : struct, IFdbKey
		where TUpperKey : struct, IFdbKey
	{

		/// <summary>Constructs a new <see cref="FdbKeyRange{TLowerKey,TUpperKey}"/></summary>
		/// <param name="lowerKey">Key that will be used to compute the lower bound</param>
		/// <param name="lowerMode">Transformation that will be applied to <paramref name="lowerKey"/></param>
		/// <param name="upperKey">Key that will be used to compute the upper bound</param>
		/// <param name="upperMode">Transformation that will be applied to <paramref name="upperKey"/></param>
		[SkipLocalsInit, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public FdbKeyRange(in TLowerKey lowerKey, KeyRangeMode lowerMode, in TUpperKey upperKey, KeyRangeMode upperMode)
		{
			this.LowerKey = lowerKey;
			this.UpperKey = upperKey;
			this.LowerMode = lowerMode;
			this.UpperMode = upperMode;
		}

		/// <summary>Key that is used to compute the lower bound of this range</summary>
		public readonly TLowerKey LowerKey;

		/// <summary>Transformation applied to the lower key to produce the effective lower bound (Inclusive by default)</summary>
		public readonly KeyRangeMode LowerMode;

		/// <summary>Key that is used to compute the upper bound of this range</summary>
		public readonly TUpperKey UpperKey;

		/// <summary>Transformation applied to the upper key to produce the effective upper bound (Exclusive by default)</summary>
		public readonly KeyRangeMode UpperMode;

		/// <summary>Returns the inclusive "Begin" key of this range</summary>
		public BeginKey Begin() => new(in this);

		/// <summary>Returns the exclusive "End" key of this range</summary>
		public EndKey End() => new(in this);

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public KeyRange ToKeyRange() => KeyRange.Create(ToBeginKey(), ToEndKey());

		/// <summary>Returns the encoded Begin key (inclusive) for this range</summary>
		[Pure]
		public Slice ToBeginKey() => this.LowerMode switch
		{
			KeyRangeMode.Default or KeyRangeMode.Inclusive => FdbKeyHelpers.ToSlice(in this.LowerKey),
			KeyRangeMode.Exclusive => FdbKeyHelpers.ToSlice(this.LowerKey.Successor()),
			KeyRangeMode.NextSibling => FdbKeyHelpers.ToSlice(this.LowerKey.NextSibling()),
			_ => throw new NotSupportedException(),
		};

		/// <summary>Returns the encoded End key (exclusive) for this range</summary>
		[Pure]
		public Slice ToEndKey() => this.UpperMode switch
		{
			KeyRangeMode.Default or KeyRangeMode.Exclusive => FdbKeyHelpers.ToSlice(this.UpperKey),
			KeyRangeMode.Inclusive => FdbKeyHelpers.ToSlice(this.UpperKey.Successor()),
			KeyRangeMode.NextSibling => FdbKeyHelpers.ToSlice(this.UpperKey.NextSibling()),
			KeyRangeMode.Last => FdbKeyHelpers.ToSlice(this.UpperKey.Last()),
			_ => throw new NotSupportedException(),
		};

		/// <summary>Returns the encoded Begin key (inclusive) for this range</summary>
		[Pure]
		public KeySelector ToBeginSelector() => this.LowerMode switch
		{
			KeyRangeMode.Default or KeyRangeMode.Inclusive => this.LowerKey.FirstGreaterOrEqual().ToSelector(),
			KeyRangeMode.Exclusive => this.LowerKey.FirstGreaterThan().ToSelector(),
			KeyRangeMode.NextSibling => this.LowerKey.NextSibling().FirstGreaterOrEqual().ToSelector(),
			_ => throw new NotSupportedException(),
		};

		/// <summary>Returns the encoded End key (exclusive) for this range</summary>
		[Pure]
		public KeySelector ToEndSelector() => this.UpperMode switch
		{
			KeyRangeMode.Default or KeyRangeMode.Exclusive => this.UpperKey.FirstGreaterOrEqual().ToSelector(),
			KeyRangeMode.Inclusive => this.UpperKey.FirstGreaterThan().ToSelector(),
			KeyRangeMode.NextSibling => this.UpperKey.NextSibling().FirstGreaterOrEqual().ToSelector(),
			KeyRangeMode.Last => this.UpperKey.Last().FirstGreaterOrEqual().ToSelector(),
			_ => throw new NotSupportedException(),
		};

		/// <summary>Tests if the key is before (<c>-1</c>), after (<c>+1</c>) or contained in the range (<c>0</c>)</summary>
		/// <typeparam name="TKey">Type of the key</typeparam>
		/// <param name="key">Key that is being tested</param>
		/// <returns><c>-1</c> if the key is before the range, <c>0</c> if the key is in the range, and <c>+1</c> if the key is after the range.</returns>
		[Pure]
		public int Test<TKey>(in TKey key)
			where TKey : struct, IFdbKey
			=> this.Begin().FastCompareTo(in key) > 0 ? -1
			 : this.End().FastCompareTo(in key) <= 0 ? +1
			 : 0;

		/// <inheritdoc />
		[Pure]
		public bool Contains(Slice key)
			=> this.Begin().CompareTo(key.Span) <= 0 && this.End().CompareTo(key.Span) > 0;

		/// <inheritdoc />
		[Pure]
		public bool Contains(ReadOnlySpan<byte> key)
			=> this.Begin().CompareTo(key) <= 0 && this.End().CompareTo(key) > 0;

		/// <inheritdoc />
		[Pure]
		public bool Contains<TKey>(in TKey key)
			where TKey : struct, IFdbKey
			=> this.Begin().FastCompareTo(in key) <= 0 && this.End().FastCompareTo(in key) > 0;

		/// <summary>Tests if the given key is strictly before this range</summary>
		/// <typeparam name="TKey">Type of the key</typeparam>
		/// <param name="key">Key that is being tested</param>
		/// <returns><c>true</c> if the key is less than the effective lower bound of the range.</returns>
		[Pure]
		public bool IsBefore<TKey>(in TKey key)
			where TKey : struct, IFdbKey
			=> this.Begin().FastCompareTo(in key) > 0;

		/// <summary>Tests if the given key is strictly before this range</summary>
		/// <param name="key">Key that is being tested</param>
		/// <returns><c>true</c> if the key is less than the effective lower bound of the range.</returns>
		[Pure]
		public bool IsBefore(Slice key)
			=> this.Begin().CompareTo(key) > 0;

		/// <summary>Tests if the given key is strictly after this range</summary>
		/// <typeparam name="TKey">Type of the key</typeparam>
		/// <param name="key">Key that is being tested</param>
		/// <returns><c>true</c> if the key is greater than or equal to the effective upper bound of the range.</returns>
		[Pure]
		public bool IsAfter<TKey>(in TKey key)
			where TKey : struct, IFdbKey
			=> this.End().FastCompareTo(in key) <= 0;

		/// <summary>Tests if the given key is strictly after this range</summary>
		/// <param name="key">Key that is being tested</param>
		/// <returns><c>true</c> if the key is greater than or equal to the effective upper bound of the range.</returns>
		[Pure]
		public bool IsAfter(Slice key)
			=> this.End().CompareTo(key) <= 0;

		/// <inheritdoc />
		public override string ToString() => ToString(null);

		public string ToString(string? format, IFormatProvider? provider = null) => format switch
		{
			null or "" or "D" or "d" => string.Create(provider ?? CultureInfo.InvariantCulture, $"{{ {Begin()} <= k < {End()} }}"),
			"K" or "k" => string.Create(provider ?? CultureInfo.InvariantCulture, $"{{ {Begin():B} <= k < {End():E} }}"),
			"P" or "p" => string.Create(provider ?? CultureInfo.InvariantCulture, $"{{ {Begin():P} <= k < {End():P} }}"),
			_ => throw new FormatException("Unsupported format."),
		};

		public bool TryFormat(Span<char> destination, out int charsWritten, ReadOnlySpan<char> format = default, IFormatProvider? provider = null) => format switch
		{
			"" or "D" or "d" => destination.TryWrite(provider ?? CultureInfo.InvariantCulture, $"{{ {Begin()} <= k < {End()} }}", out charsWritten),
			"K" or "k" => destination.TryWrite(provider ?? CultureInfo.InvariantCulture, $"{{ {Begin():B} <= k < {End():E} }}", out charsWritten),
			"P" or "p" => destination.TryWrite(provider ?? CultureInfo.InvariantCulture, $"{{ {Begin():P} <= k < {End():P} }}", out charsWritten),
			_ => throw new FormatException("Unsupported format."),
		};

		public bool Equals(FdbKeyRange<TLowerKey, TUpperKey> other)
		{
			return this.LowerMode == other.LowerMode && this.UpperMode == other.UpperMode && this.LowerKey.FastEqualTo(in other.LowerKey) && this.UpperKey.FastEqualTo(in other.UpperKey);
		}

		/// <summary>Represents the "Begin" key of a key range</summary>
		public readonly struct BeginKey : IFdbKey
			, IEquatable<BeginKey>, IComparable<BeginKey>
			, IEquatable<FdbSuccessorKey<TLowerKey>>
		{

			[SkipLocalsInit, MethodImpl(MethodImplOptions.AggressiveInlining)]
			public BeginKey(in FdbKeyRange<TLowerKey, TUpperKey> range)
			{
				this.Range = range;
			}

			/// <summary>Parent range</summary>
			public readonly FdbKeyRange<TLowerKey, TUpperKey> Range;

			/// <inheritdoc />
			public IKeySubspace? GetSubspace() => this.Range.LowerKey.GetSubspace();

			#region Equals()...

			/// <inheritdoc />
			public override int GetHashCode() => this.Range.GetHashCode();

			/// <inheritdoc />
			public override bool Equals([NotNullWhen(true)] object? other) => other switch
			{
				BeginKey key => this.Equals(key),
				FdbRawKey key => this.Equals(key.Span),
				Slice bytes => this.Equals(bytes.Span),
				IFdbKey key => FdbKeyHelpers.AreEqual(in this, key),
				_ => false,
			};

			/// <inheritdoc />
			[Obsolete(FdbKeyHelpers.PreferFastEqualToForKeysMessage)]
			public bool Equals([NotNullWhen(true)] IFdbKey? other) => other switch
			{
				null => false,
				BeginKey key => this.Equals(key),
				FdbRawKey key => this.Equals(key.Span),
				_ => FdbKeyHelpers.AreEqual(in this, other),
			};

			/// <inheritdoc />
			public bool Equals(BeginKey other) => this.Range.Equals(other.Range);

			/// <inheritdoc />
			public bool Equals(FdbRawKey other) => Equals(other.Span);

			/// <inheritdoc />
			public bool Equals(FdbSuccessorKey<TLowerKey> other)
				=> this.Range.LowerMode == KeyRangeMode.Exclusive
					? this.Range.LowerKey.Equals(other.Parent)
					: FdbKeyHelpers.AreEqual(in this, other);

			/// <inheritdoc />
			public bool Equals(Slice other) => Equals(other.Span);

			/// <inheritdoc cref="Equals(Slice)" />
			public bool Equals(ReadOnlySpan<byte> other) => this.Range.LowerMode switch
			{
				KeyRangeMode.Default or KeyRangeMode.Inclusive => this.Range.LowerKey.Equals(other),
				KeyRangeMode.Exclusive => this.Range.LowerKey.Successor().Equals(other),
				KeyRangeMode.NextSibling => this.Range.LowerKey.NextSibling().Equals(other),
				_ => throw new NotSupportedException(),
			};

			/// <inheritdoc />
			public bool FastEqualTo<TOtherKey>(in TOtherKey other)
				where TOtherKey : struct, IFdbKey
			{
				if (typeof(TOtherKey) == typeof(BeginKey))
				{
					return Equals((BeginKey) (object) other);
				}
				if (typeof(TOtherKey) == typeof(FdbRawKey))
				{
					return Equals(((FdbRawKey) (object) other).Span);
				}
				return FdbKeyHelpers.AreEqual(in this, in other);
			}

			/// <inheritdoc />
			public int CompareTo(BeginKey other) => this.Range.LowerMode switch
			{
				KeyRangeMode.Default or KeyRangeMode.Inclusive => this.Range.LowerKey.FastCompareTo(in other),
				KeyRangeMode.Exclusive => this.Range.LowerKey.Successor().FastCompareTo(in other),
				KeyRangeMode.NextSibling => this.Range.LowerKey.NextSibling().FastCompareTo(in other),
				_ => throw new NotSupportedException(),
			};

			/// <inheritdoc />
			public int CompareTo(FdbRawKey other) => CompareTo(other.Span);

			/// <inheritdoc />
			public int CompareTo(Slice other) => CompareTo(other.Span);

			/// <inheritdoc cref="CompareTo(Slice)" />
			public int CompareTo(ReadOnlySpan<byte> other) => this.Range.LowerMode switch
			{
				KeyRangeMode.Default or KeyRangeMode.Inclusive => this.Range.LowerKey.CompareTo(other),
				KeyRangeMode.Exclusive => this.Range.LowerKey.Successor().CompareTo(other),
				KeyRangeMode.NextSibling => this.Range.LowerKey.NextSibling().CompareTo(other),
				_ => throw new NotSupportedException(),
			};

			/// <inheritdoc />
			public int FastCompareTo<TOtherKey>(in TOtherKey other)
				where TOtherKey : struct, IFdbKey
			{
				if (typeof(TOtherKey) == typeof(BeginKey))
				{
					return CompareTo((BeginKey) (object) other);
				}
				if (typeof(TOtherKey) == typeof(FdbRawKey))
				{
					return CompareTo(((FdbRawKey) (object) other).Span);
				}
				return FdbKeyHelpers.Compare(in this, in other);
			}

			#endregion

			#region Formatting...

			/// <inheritdoc />
			public override string ToString() => ToString(null);

			/// <inheritdoc />
			public string ToString(string? format, IFormatProvider? formatProvider = null) => this.Range.LowerMode switch
			{
				KeyRangeMode.Default or KeyRangeMode.Inclusive => this.Range.LowerKey.ToString(format, formatProvider),
				KeyRangeMode.Exclusive => this.Range.LowerKey.Successor().ToString(format, formatProvider),
				KeyRangeMode.NextSibling => this.Range.LowerKey.NextSibling().ToString(format, formatProvider),
				_ => throw new NotSupportedException(),
			};

			/// <inheritdoc />
			public bool TryFormat(Span<char> destination, out int charsWritten, ReadOnlySpan<char> format, IFormatProvider? provider) => this.Range.LowerMode switch
			{
				KeyRangeMode.Default or KeyRangeMode.Inclusive => this.Range.LowerKey.TryFormat(destination, out charsWritten, format, provider),
				KeyRangeMode.Exclusive => this.Range.LowerKey.Successor().TryFormat(destination, out charsWritten, format, provider),
				KeyRangeMode.NextSibling => this.Range.LowerKey.NextSibling().TryFormat(destination, out charsWritten, format, provider),
				_ => throw new NotSupportedException(),
			};

			#endregion

			#region ISpanEncodable...

			/// <inheritdoc />
			public bool TryGetSpan(out ReadOnlySpan<byte> span) { span = default; return false; }

			/// <inheritdoc />
			public bool TryGetSizeHint(out int sizeHint) { sizeHint = 0; return false; }

			/// <inheritdoc />
			public bool TryEncode(scoped Span<byte> destination, out int bytesWritten) => this.Range.LowerMode switch
			{
				KeyRangeMode.Default or KeyRangeMode.Inclusive => this.Range.LowerKey.TryEncode(destination, out bytesWritten),
				KeyRangeMode.Exclusive => this.Range.LowerKey.Successor().TryEncode(destination, out bytesWritten),
				KeyRangeMode.NextSibling => this.Range.LowerKey.NextSibling().TryEncode(destination, out bytesWritten),
				_ => throw new NotSupportedException(),
			};

			#endregion

		}

		/// <summary>Represents the "End" key of a key range</summary>
		public readonly struct EndKey : IFdbKey
			, IEquatable<EndKey>, IComparable<EndKey>
			, IEquatable<FdbNextSiblingKey<TUpperKey>>
			, IEquatable<FdbLastKey<TUpperKey>>
		{

			public EndKey(in FdbKeyRange<TLowerKey, TUpperKey> range)
			{
				this.Range = range;
			}

			public readonly FdbKeyRange<TLowerKey, TUpperKey> Range;

			/// <inheritdoc />
			public IKeySubspace? GetSubspace() => this.Range.UpperKey.GetSubspace();

			#region Equals()...

			/// <inheritdoc />
			[EditorBrowsable(EditorBrowsableState.Never)]
			public override int GetHashCode() => throw FdbKeyHelpers.ErrorCannotComputeHashCodeMessage();

			/// <inheritdoc />
			public override bool Equals([NotNullWhen(true)] object? other) => other switch
			{
				EndKey key => this.Equals(key),
				FdbRawKey key => this.Equals(key.Span),
				Slice bytes => this.Equals(bytes.Span),
				IFdbKey key => FdbKeyHelpers.AreEqual(in this, key),
				_ => false,
			};

			/// <inheritdoc />
			[Obsolete(FdbKeyHelpers.PreferFastEqualToForKeysMessage)]
			public bool Equals([NotNullWhen(true)] IFdbKey? other) => other switch
			{
				null => false,
				EndKey key => this.Equals(key),
				FdbRawKey key => this.Equals(key.Span),
				_ => FdbKeyHelpers.AreEqual(in this, other),
			};

			/// <inheritdoc />
			public bool Equals(EndKey other) => this.Range.Equals(other.Range);

			/// <inheritdoc />
			public bool Equals(FdbRawKey other) => Equals(other.Span);

			/// <inheritdoc />
			public bool Equals(FdbNextSiblingKey<TUpperKey> other)
				=> this.Range.UpperMode == KeyRangeMode.NextSibling
					? this.Range.UpperKey.Equals(other.Parent)
					: FdbKeyHelpers.AreEqual(in this, other);

			/// <inheritdoc />
			public bool Equals(FdbLastKey<TUpperKey> other)
				=> this.Range.UpperMode == KeyRangeMode.Last
					? this.Range.UpperKey.Equals(other.Parent)
					: FdbKeyHelpers.AreEqual(in this, other);

			/// <inheritdoc />
			public bool Equals(Slice other) => Equals(other.Span);

			/// <inheritdoc cref="Equals(Slice)" />
			public bool Equals(ReadOnlySpan<byte> other) => this.Range.UpperMode switch
			{
				KeyRangeMode.Default or KeyRangeMode.Exclusive => this.Range.UpperKey.Equals(other),
				KeyRangeMode.Inclusive => this.Range.UpperKey.Successor().Equals(other),
				KeyRangeMode.NextSibling => this.Range.UpperKey.NextSibling().Equals(other),
				KeyRangeMode.Last => this.Range.UpperKey.Last().Equals(other),
				_ => throw new NotSupportedException(),
			};

			/// <inheritdoc />
			public bool FastEqualTo<TOtherKey>(in TOtherKey other)
				where TOtherKey : struct, IFdbKey
			{
				if (typeof(TOtherKey) == typeof(EndKey))
				{
					return Equals((EndKey) (object) other);
				}
				if (typeof(TOtherKey) == typeof(FdbRawKey))
				{
					return Equals(((FdbRawKey) (object) other).Span);
				}
				return FdbKeyHelpers.AreEqual(in this, in other);
			}

			/// <inheritdoc />
			public int CompareTo(EndKey other) => this.Range.UpperMode switch
			{
				KeyRangeMode.Default or KeyRangeMode.Exclusive => this.Range.UpperKey.FastCompareTo(in other),
				KeyRangeMode.Inclusive => this.Range.UpperKey.Successor().FastCompareTo(in other),
				KeyRangeMode.NextSibling => this.Range.UpperKey.NextSibling().FastCompareTo(in other),
				KeyRangeMode.Last => this.Range.UpperKey.Last().FastCompareTo(in other),
				_ => throw new NotSupportedException(),
			};

			/// <inheritdoc />
			public int CompareTo(FdbRawKey other) => CompareTo(other.Span);

			/// <inheritdoc />
			public int CompareTo(Slice other) => CompareTo(other.Span);

			/// <inheritdoc cref="CompareTo(Slice)" />
			public int CompareTo(ReadOnlySpan<byte> other) => this.Range.UpperMode switch
			{
				KeyRangeMode.Default or KeyRangeMode.Exclusive => this.Range.UpperKey.CompareTo(other),
				KeyRangeMode.Inclusive => this.Range.UpperKey.Successor().CompareTo(other),
				KeyRangeMode.NextSibling => this.Range.UpperKey.NextSibling().CompareTo(other),
				KeyRangeMode.Last => this.Range.UpperKey.Last().CompareTo(other),
				_ => throw new NotSupportedException(),
			};

			/// <inheritdoc />
			public int FastCompareTo<TOtherKey>(in TOtherKey other)
				where TOtherKey : struct, IFdbKey
			{
				if (typeof(TOtherKey) == typeof(EndKey))
				{
					return CompareTo((EndKey) (object) other);
				}
				if (typeof(TOtherKey) == typeof(FdbRawKey))
				{
					return CompareTo(((FdbRawKey) (object) other).Span);
				}
				return FdbKeyHelpers.Compare(in this, in other);
			}

			#endregion

			#region Formatting...

			/// <inheritdoc />
			public override string ToString() => ToString(null);

			/// <inheritdoc />
			public string ToString(string? format, IFormatProvider? formatProvider = null) => this.Range.UpperMode switch
			{
				KeyRangeMode.Default or KeyRangeMode.Exclusive => this.Range.UpperKey.ToString(format, formatProvider),
				KeyRangeMode.Inclusive => this.Range.UpperKey.Successor().ToString(format, formatProvider),
				KeyRangeMode.NextSibling => this.Range.UpperKey.NextSibling().ToString(format, formatProvider),
				KeyRangeMode.Last => this.Range.UpperKey.Last().ToString(format, formatProvider),
				_ => throw new NotSupportedException(),
			};

			/// <inheritdoc />
			public bool TryFormat(Span<char> destination, out int charsWritten, ReadOnlySpan<char> format, IFormatProvider? provider) => this.Range.UpperMode switch
			{
				KeyRangeMode.Default or KeyRangeMode.Exclusive => this.Range.UpperKey.TryFormat(destination, out charsWritten, format, provider),
				KeyRangeMode.Inclusive => this.Range.UpperKey.Successor().TryFormat(destination, out charsWritten, format, provider),
				KeyRangeMode.NextSibling => this.Range.UpperKey.NextSibling().TryFormat(destination, out charsWritten, format, provider),
				KeyRangeMode.Last => this.Range.UpperKey.Last().TryFormat(destination, out charsWritten, format, provider),
				_ => throw new NotSupportedException(),
			};

			#endregion

			#region ISpanEncodable...

			/// <inheritdoc />
			public bool TryGetSpan(out ReadOnlySpan<byte> span) { span = default; return false; }

			/// <inheritdoc />
			public bool TryGetSizeHint(out int sizeHint) { sizeHint = 0; return false; }

			/// <inheritdoc />
			public bool TryEncode(scoped Span<byte> destination, out int bytesWritten) => this.Range.UpperMode switch
			{
				KeyRangeMode.Default or KeyRangeMode.Exclusive => this.Range.UpperKey.TryEncode(destination, out bytesWritten),
				KeyRangeMode.Inclusive => this.Range.UpperKey.Successor().TryEncode(destination, out bytesWritten),
				KeyRangeMode.NextSibling => this.Range.UpperKey.NextSibling().TryEncode(destination, out bytesWritten),
				KeyRangeMode.Last => this.Range.UpperKey.Last().TryEncode(destination, out bytesWritten),
				_ => throw new NotSupportedException(),
			};

			#endregion

		}

	}

	/// <summary>Range that matches all the keys inside a subspace</summary>
	public readonly struct FdbSubspaceKeyRange : IFdbKeyRange
	{

		[SkipLocalsInit, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public FdbSubspaceKeyRange(IKeySubspace subspace, bool inclusive = false)
		{
			this.Subspace = subspace;
			this.IsInclusive = inclusive;
		}

		/// <summary>Subspace that contains the keys</summary>
		public readonly IKeySubspace Subspace;

		/// <summary>If <c>true</c>, the subspace prefix is also included in the range</summary>
		public readonly bool IsInclusive;

		/// <summary>Returns a version of this range that includes the subspace prefix in the results</summary>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public FdbSubspaceKeyRange Inclusive() => new(this.Subspace, inclusive: true);

		/// <summary>Returns a version of this range that excludes the subspace prefix from the results</summary>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public FdbSubspaceKeyRange Exclusive() => new(this.Subspace, inclusive: false);

		/// <inheritdoc />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool Contains(Slice key) => Contains(key.Span);

		/// <inheritdoc />
		[Pure]
		public bool Contains(ReadOnlySpan<byte> key)
		{
			var prefix = this.Subspace.GetPrefix();
			return key.StartsWith(prefix.Span) && (this.IsInclusive || key.Length > prefix.Count);
		}

		/// <inheritdoc />
		[Pure]
		public bool Contains<TKey>(in TKey key)
			where TKey : struct, IFdbKey
		{
			if (typeof(TKey) == typeof(FdbRawKey))
			{
				return Contains(((FdbRawKey)(object)key).Data);
			}
			if (typeof(TKey) == typeof(FdbSubspaceKey))
			{
				return Contains(((FdbSubspaceKey)(object)key).Subspace.GetPrefix().Span);
			}

			int cmp = key.CompareTo(this.Subspace.GetPrefix());
			if (this.IsInclusive)
			{
				if (cmp < 0) return false;
			}
			else
			{
				if (cmp <= 0) return false;
			}
			return this.Subspace.NextSibling().FastCompareTo(in key) > 0;
		}

		/// <inheritdoc />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public Slice ToBeginKey() => this.IsInclusive ? this.Subspace.GetPrefix() : FdbKeyHelpers.ToSlice(this.Subspace.First());

		/// <inheritdoc />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public Slice ToEndKey() => FdbKeyHelpers.ToSlice(Subspace.NextSibling());

		/// <inheritdoc />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public KeyRange ToKeyRange() => this.IsInclusive
			//PERF: TODO: optimize this!
			? KeyRange.StartsWith(this.Subspace.GetPrefix())
			: KeyRange.PrefixedBy(this.Subspace.GetPrefix());

		/// <inheritdoc />
		public KeySelector ToBeginSelector() => this.IsInclusive
			? FdbKeySelector.FirstGreaterOrEqual(this.Subspace.GetPrefix()).ToSelector()
			: FdbKeySelector.FirstGreaterThan(this.Subspace.GetPrefix()).ToSelector();

		/// <inheritdoc />
		public KeySelector ToEndSelector()
			=> FdbKeySelector.FirstGreaterOrEqual(this.Subspace.NextSibling()).ToSelector();

		/// <inheritdoc />
		public override string ToString() => ToString(null);

		/// <inheritdoc />
		public string ToString(string? format, IFormatProvider? formatProvider = null) => format switch
		{
			
			null or "" or "D" or "d" => string.Create(formatProvider ?? CultureInfo.InvariantCulture, $"{this:D}"),
			"K" or "k" => string.Create(formatProvider ?? CultureInfo.InvariantCulture, $"{this:K}"),
			"P" or "p" => string.Create(formatProvider ?? CultureInfo.InvariantCulture, $"{this:P}"),
			_ => throw new FormatException(),
		};

		/// <inheritdoc />
		public bool TryFormat(Span<char> destination, out int charsWritten, ReadOnlySpan<char> format = default, IFormatProvider? provider = null) => format switch
		{
			"" or "D" or "d" => destination.TryWrite(provider ?? CultureInfo.InvariantCulture, $"{{ {this.Subspace}{(this.IsInclusive ? "" : ".<00>")} <= k < {this.Subspace}+1 }}", out charsWritten),
			"K" or "k" => destination.TryWrite(provider ?? CultureInfo.InvariantCulture, $"{{ {this.Subspace:K}{(this.IsInclusive ? "" : ".<00>")} <= k < {this.Subspace:K}+1 }}", out charsWritten),
			"P" or "p" => destination.TryWrite(provider ?? CultureInfo.InvariantCulture, $"{{ {this.Subspace:P}{(this.IsInclusive ? "" : ".<00>")} <= k < {this.Subspace:P}+1 }}", out charsWritten),
			_ => throw new FormatException(),
		};

	}

}
