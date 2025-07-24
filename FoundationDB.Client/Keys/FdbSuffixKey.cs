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

	/// <summary>Wraps a <see cref="Slice"/> that wraps a pre-encoded binary suffix, relative to a subspace</summary>
	public readonly struct FdbTupleSuffixKey<TKey, TTuple> : IFdbKey
		, IEquatable<FdbTupleSuffixKey<TKey, TTuple>>, IComparable<FdbTupleSuffixKey<TKey, TTuple>>
		where TKey : struct, IFdbKey
		where TTuple : IVarTuple
	{

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		[SkipLocalsInit]
		internal FdbTupleSuffixKey(TKey parent, in TTuple suffix)
		{
			this.Parent = parent;
			this.Suffix = suffix;
		}

		public readonly TKey Parent;

		public readonly TTuple Suffix;

		/// <inheritdoc />
		IKeySubspace? IFdbKey.GetSubspace() => this.Parent.GetSubspace();

		#region Equals(...)

		/// <inheritdoc />
		public override bool Equals([NotNullWhen(true)] object? other)
		{
			return other switch
			{
				FdbTupleSuffixKey<TKey, TTuple> key => this.Equals(key),
				FdbRawKey key => this.Equals(key),
				Slice bytes => this.Equals(bytes),
				IFdbKey key => FdbKeyHelpers.Equals(in this, key),
				_ => false,
			};
		}

		/// <inheritdoc />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public override int GetHashCode() => this.Suffix.GetHashCode(); //BUGBUG: TODO: this breaks the contracts for equality

		/// <inheritdoc />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool Equals(FdbTupleSuffixKey<TKey, TTuple> other) => FdbKeyHelpers.Equals(in this.Parent, in other.Parent) && this.Suffix.Equals(other.Suffix);

		/// <inheritdoc />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool Equals(FdbRawKey other) => FdbKeyHelpers.Equals(in this, other.Data);

		/// <inheritdoc />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool Equals(Slice other) => FdbKeyHelpers.Equals(in this, other);

		/// <inheritdoc cref="Equals(Slice)"/>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool Equals(ReadOnlySpan<byte> other) => FdbKeyHelpers.Equals(in this, other);

		/// <inheritdoc />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool Equals<TOtherKey>(in TOtherKey other)
			where TOtherKey : struct, IFdbKey
		{
			if (typeof(TOtherKey) == typeof(FdbTupleSuffixKey<TKey, TTuple>))
			{
				return FdbKeyHelpers.Equals(in this.Parent, in Unsafe.As<TOtherKey, FdbTupleSuffixKey<TKey, TTuple>>(ref Unsafe.AsRef(in other)).Parent) && this.Suffix.Equals(((FdbTupleSuffixKey<TKey, TTuple>) (object) other).Suffix);
			}
			if (typeof(TOtherKey) == typeof(FdbRawKey))
			{
				return FdbKeyHelpers.Equals(in this, ((FdbRawKey) (object) other).Data);
			}
			return FdbKeyHelpers.Equals(in this, in other);
		}

		/// <inheritdoc />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public int CompareTo(FdbTupleSuffixKey<TKey, TTuple> other)
		{
			var cmp = FdbKeyHelpers.CompareTo(in this.Parent, in other.Parent);
			if (cmp == 0) cmp = this.Suffix.CompareTo(other.Suffix);
			return cmp;
		}

		/// <inheritdoc />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public int CompareTo(FdbRawKey other)
			=> FdbKeyHelpers.CompareTo(in this, other.Data);

		/// <inheritdoc />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public int CompareTo(Slice other)
			=> FdbKeyHelpers.CompareTo(in this, other);

		/// <inheritdoc cref="CompareTo(Slice)" />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public int CompareTo(ReadOnlySpan<byte> other)
			=> FdbKeyHelpers.CompareTo(in this, other);

		/// <inheritdoc />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public int CompareTo<TOtherKey>(in TOtherKey other)
			where TOtherKey : struct, IFdbKey
			=> FdbKeyHelpers.CompareTo(in this, in other);

		#endregion

		/// <inheritdoc />
		public override string ToString() => ToString(null);

		/// <inheritdoc />
		public string ToString(string? format, IFormatProvider? formatProvider = null)
			=> string.Create(formatProvider, $"{this}");

		/// <inheritdoc />
		public bool TryFormat(Span<char> destination, out int charsWritten, ReadOnlySpan<char> format, IFormatProvider? provider)
			=> destination.TryWrite(provider, $"{this.Parent}+{this.Suffix}", out charsWritten);

		/// <inheritdoc />
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool TryGetSpan(out ReadOnlySpan<byte> span)
		{
			span = default;
			return false;
		}

		/// <inheritdoc />
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool TryGetSizeHint(out int sizeHint)
		{
			//PERF: TODO: we don't have an easy way to estimate the size of a TTuple yet!
			sizeHint = 0;
			return false;
		}

		/// <inheritdoc />
		public bool TryEncode(Span<byte> destination, out int bytesWritten)
		{
			if (this.Parent.TryEncode(destination, out var parentLen)
			 && TupleEncoder.TryPackTo(destination[parentLen..], out var suffixLen, default, this.Suffix))
			{
				bytesWritten = parentLen + suffixLen;
				return true;
			}

			bytesWritten = 0;
			return false;
		}

	}
	
	/// <summary>Wraps a <see cref="Slice"/> that wraps a pre-encoded binary suffix, relative to a subspace</summary>
	public readonly struct FdbSuffixKey : IFdbKey
		, IEquatable<FdbSuffixKey>, IComparable<FdbSuffixKey>
	{

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		[SkipLocalsInit]
		internal FdbSuffixKey(IKeySubspace subspace, Slice suffix)
		{
			this.Suffix = suffix;
			this.Subspace = subspace;
		}

		public readonly IKeySubspace Subspace;

		public readonly Slice Suffix;

		/// <inheritdoc />
		IKeySubspace? IFdbKey.GetSubspace() => this.Subspace;

		#region Equals(...)

		/// <inheritdoc />
		public override bool Equals([NotNullWhen(true)] object? other)
		{
			return other switch
			{
				FdbRawKey key => this.Equals(key),
				FdbSuffixKey key => this.Equals(key),
				Slice bytes => this.Equals(bytes),
				IFdbKey key => FdbKeyHelpers.Equals(in this, key),
				_ => false,
			};
		}

		/// <inheritdoc />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public override int GetHashCode() => this.Suffix.GetHashCode(); //BUGBUG: TODO: this breaks the contracts for equality

		/// <inheritdoc />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool Equals(FdbSuffixKey other) => FdbKeyHelpers.Equals(this.Subspace, other.Subspace) && this.Suffix.Equals(other.Suffix);

		/// <inheritdoc />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool Equals(FdbRawKey other)
		{
			var otherSpan = other.Span;
			var suffixSpan = this.Subspace.GetPrefix().Span;
			return otherSpan.StartsWith(suffixSpan) && otherSpan[suffixSpan.Length..].SequenceEqual(suffixSpan);
		}

		/// <inheritdoc />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool Equals(Slice other)
		{
			var otherSpan = other.Span;
			var suffixSpan = this.Subspace.GetPrefix().Span;
			return otherSpan.StartsWith(suffixSpan) && otherSpan[suffixSpan.Length..].SequenceEqual(suffixSpan);
		}

		/// <inheritdoc cref="Equals(Slice)"/>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool Equals(ReadOnlySpan<byte> other)
		{
			var suffixSpan = this.Subspace.GetPrefix().Span;
			return other.StartsWith(suffixSpan) && other[suffixSpan.Length..].SequenceEqual(suffixSpan);
		}

		/// <inheritdoc />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool Equals<TOtherKey>(in TOtherKey other)
			where TOtherKey : struct, IFdbKey
		{
			if (typeof(TOtherKey) == typeof(FdbSuffixKey))
			{
				return Equals((FdbSuffixKey) (object) other);
			}
			if (typeof(TOtherKey) == typeof(FdbRawKey))
			{
				return Equals((FdbRawKey) (object) other);
			}
			return FdbKeyHelpers.Equals(in this, in other);
		}

		/// <inheritdoc />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public int CompareTo(FdbSuffixKey other)
		{
			var cmp = FdbKeyHelpers.CompareTo(this.Subspace, other.Subspace);
			if (cmp == 0) cmp = this.Suffix.CompareTo(other.Suffix);
			return cmp;
		}

		/// <inheritdoc />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public int CompareTo(FdbRawKey other)
			=> FdbKeyHelpers.CompareTo(in this, other.Data);

		/// <inheritdoc />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public int CompareTo(Slice other)
			=> FdbKeyHelpers.CompareTo(in this, other);

		/// <inheritdoc cref="CompareTo(Slice)" />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public int CompareTo(ReadOnlySpan<byte> other)
			=> FdbKeyHelpers.CompareTo(in this, other);

		/// <inheritdoc />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public int CompareTo<TOtherKey>(in TOtherKey other)
			where TOtherKey : struct, IFdbKey
			=> FdbKeyHelpers.CompareTo(in this, in other);

		#endregion

		#region Key...

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public FdbTupleSuffixKey<FdbSuffixKey, STuple<T1>> Key<T1>(T1 item1) => new(this, new(item1));

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public FdbTupleSuffixKey<FdbSuffixKey, STuple<T1, T2>> Key<T1, T2>(T1 item1, T2 item2) => new(this, new(item1, item2));

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public FdbTupleSuffixKey<FdbSuffixKey, STuple<T1, T2, T3>> Key<T1, T2, T3>(T1 item1, T2 item2, T3 item3) => new(this, new(item1, item2, item3));

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public FdbTupleSuffixKey<FdbSuffixKey, STuple<T1, T2, T3, T4>> Key<T1, T2, T3, T4>(T1 item1, T2 item2, T3 item3, T4 item4) => new(this, new(item1, item2, item3, item4));

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public FdbTupleSuffixKey<FdbSuffixKey, STuple<T1, T2, T3, T4, T5>> Key<T1, T2, T3, T4, T5>(T1 item1, T2 item2, T3 item3, T4 item4, T5 item5) => new(this, new(item1, item2, item3, item4, item5));

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public FdbTupleSuffixKey<FdbSuffixKey, STuple<T1, T2, T3, T4, T5, T6>> Key<T1, T2, T3, T4, T5, T6>(T1 item1, T2 item2, T3 item3, T4 item4, T5 item5, T6 item6) => new(this, new(item1, item2, item3, item4, item5, item6));

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public FdbTupleSuffixKey<FdbSuffixKey, STuple<T1, T2, T3, T4, T5, T6, T7>> Key<T1, T2, T3, T4, T5, T6, T7>(T1 item1, T2 item2, T3 item3, T4 item4, T5 item5, T6 item6, T7 item7) => new(this, new(item1, item2, item3, item4, item5, item6, item7));

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public FdbTupleSuffixKey<FdbSuffixKey, STuple<T1, T2, T3, T4, T5, T6, T7, T8>> Key<T1, T2, T3, T4, T5, T6, T7, T8>(T1 item1, T2 item2, T3 item3, T4 item4, T5 item5, T6 item6, T7 item7, T8 item8) => new(this, new(item1, item2, item3, item4, item5, item6, item7, item8));

		#endregion

		/// <inheritdoc />
		public override string ToString() => ToString(null);

		/// <inheritdoc />
		public string ToString(string? format, IFormatProvider? formatProvider = null)
			=> string.Create(formatProvider, $"{this}");

		/// <inheritdoc />
		public bool TryFormat(Span<char> destination, out int charsWritten, ReadOnlySpan<char> format, IFormatProvider? provider)
			=> destination.TryWrite(provider, $"[{this.Subspace}] `{this.Suffix}`", out charsWritten);

		/// <inheritdoc />
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool TryGetSpan(out ReadOnlySpan<byte> span)
		{
			span = default;
			return false;
		}

		/// <inheritdoc />
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool TryGetSizeHint(out int sizeHint)
		{
			sizeHint = this.Subspace.GetPrefix().Count + this.Suffix.Count;
			return true;
		}

		/// <inheritdoc />
		public bool TryEncode(Span<byte> destination, out int bytesWritten)
		{
			if (this.Subspace.GetPrefix().TryCopyTo(destination, out var prefixLen)
			    && this.Suffix.TryCopyTo(destination[prefixLen..], out var dataLen))
			{
				bytesWritten = prefixLen + dataLen;
				return true;
			}

			bytesWritten = 0;
			return false;
		}

	}

	/// <summary>Wraps another <see cref="IFdbKey"/>, with an added binary suffix</summary>
	/// <typeparam name="TKey">Type of the parent key</typeparam>
	public readonly struct FdbSuffixKey<TKey> : IFdbKey
		, IEquatable<FdbSuffixKey<TKey>>, IComparable<FdbSuffixKey<TKey>>
		where TKey : struct, IFdbKey
	{

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		[SkipLocalsInit]
		internal FdbSuffixKey(in TKey parent, Slice suffix)
		{
			this.Parent = parent;
			this.Suffix = suffix;
		}

		/// <summary>Parent key</summary>
		public readonly TKey Parent;

		/// <summary>Suffix added after the key</summary>
		public readonly Slice Suffix;

		/// <inheritdoc />
		IKeySubspace? IFdbKey.GetSubspace() => this.Parent.GetSubspace();

		#region Equals(...)

		/// <inheritdoc />
		public override bool Equals([NotNullWhen(true)] object? other)
		{
			return other switch
			{
				FdbSuffixKey<TKey> key => this.Equals(key),
				FdbRawKey key => this.Equals(key),
				Slice bytes => this.Equals(bytes),
				IFdbKey key => FdbKeyHelpers.Equals(in this, key),
				_ => false,
			};
		}

		/// <inheritdoc />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public override int GetHashCode() => this.Suffix.GetHashCode(); //BUGBUG: TODO: this breaks the contracts for equality

		/// <inheritdoc />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool Equals(FdbSuffixKey<TKey> other) => this.Parent.Equals(other.Parent) && this.Suffix.Equals(other.Suffix);

		/// <inheritdoc />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool Equals(FdbRawKey other) => FdbKeyHelpers.Equals(in this, other.Data);

		/// <inheritdoc />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool Equals(Slice other) => FdbKeyHelpers.Equals(in this, other);

		/// <inheritdoc cref="Equals(Slice)"/>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool Equals(ReadOnlySpan<byte> other) => FdbKeyHelpers.Equals(in this, other);

		/// <inheritdoc />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool Equals<TOtherKey>(in TOtherKey other)
			where TOtherKey : struct, IFdbKey
		{
			if (typeof(TOtherKey) == typeof(FdbSuffixKey<TKey>))
			{
				return this.Suffix.Equals(((FdbSuffixKey<TKey>) (object) other).Suffix) && this.Parent.Equals(((FdbSuffixKey<TKey>) (object) other));
			}
			if (typeof(TOtherKey) == typeof(FdbRawKey))
			{
				return FdbKeyHelpers.Equals(in this, ((FdbRawKey) (object) other).Data);
			}
			return FdbKeyHelpers.Equals(in this, in other);
		}

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public int CompareTo(FdbSuffixKey<TKey> other)
		{
			int cmp = FdbKeyHelpers.CompareTo(in this.Parent, in other.Parent);
			if (cmp == 0) cmp = this.Suffix.Span.SequenceCompareTo(other.Suffix.Span);
			return cmp;
		}

		/// <inheritdoc />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public int CompareTo(FdbRawKey other)
			=> FdbKeyHelpers.CompareTo(in this, other.Data.Span);

		/// <inheritdoc />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public int CompareTo(Slice other)
			=> FdbKeyHelpers.CompareTo(in this, other);

		/// <inheritdoc cref="CompareTo(Slice)" />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public int CompareTo(ReadOnlySpan<byte> other)
			=> FdbKeyHelpers.CompareTo(in this, other);

		/// <inheritdoc />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public int CompareTo<TOtherKey>(in TOtherKey other)
			where TOtherKey : struct, IFdbKey
			=> FdbKeyHelpers.CompareTo(in this, in other);

		#endregion

		/// <inheritdoc />
		public override string ToString() => ToString(null);

		/// <inheritdoc />
		public string ToString(string? format, IFormatProvider? formatProvider = null)
			=> string.Create(formatProvider, $"{this}");

		/// <inheritdoc />
		public bool TryFormat(Span<char> destination, out int charsWritten, ReadOnlySpan<char> format, IFormatProvider? provider)
			=> destination.TryWrite(provider, $"{this.Parent} + `{this.Suffix}`", out charsWritten);

		/// <inheritdoc />
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool TryGetSpan(out ReadOnlySpan<byte> span)
		{
			span = default;
			return false;
		}

		/// <inheritdoc />
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool TryGetSizeHint(out int sizeHint)
		{
			if (!this.Parent.TryGetSizeHint(out var size))
			{
				sizeHint = 0;
				return false;
			}
			sizeHint = checked(size  + this.Suffix.Count);
			return true;
		}

		/// <inheritdoc />
		public bool TryEncode(Span<byte> destination, out int bytesWritten)
		{
			if (this.Parent.TryEncode(destination, out var parentLen)
			 && this.Suffix.TryCopyTo(destination[parentLen..], out var dataLen))
			{
				bytesWritten = parentLen + dataLen;
				return true;
			}

			bytesWritten = 0;
			return false;
		}

	}

}
