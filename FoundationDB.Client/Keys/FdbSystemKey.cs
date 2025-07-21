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
	public readonly struct FdbSystemKey : IFdbKey
		, IEquatable<FdbSystemKey>, IComparable<FdbSystemKey>
	{

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		[SkipLocalsInit]
		internal FdbSystemKey(bool special, Slice suffix)
		{
			this.IsSpecial = special;
			this.Suffix = suffix;
		}

		public readonly bool IsSpecial;

		public readonly Slice Suffix;

		/// <inheritdoc />
		IKeySubspace? IFdbKey.GetSubspace() => null;

		#region Equals(...)

		/// <inheritdoc />
		public override bool Equals([NotNullWhen(true)] object? other)
		{
			return other switch
			{
				FdbSystemKey key => this.Equals(key),
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
		public bool Equals(FdbSystemKey other) => this.IsSpecial == other.IsSpecial && this.Suffix.Equals(other.Suffix);

		/// <inheritdoc />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool Equals(FdbRawKey other) => Equals(other.Data);

		/// <inheritdoc />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool Equals(Slice other) => !other.IsNull && Equals(other.Span);

		/// <inheritdoc cref="Equals(Slice)"/>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool Equals(ReadOnlySpan<byte> other)
		{
			return this.IsSpecial
				? other.Length >= 2 && other[0] == 0xFF && other[1] == 0xFF && other[2..].SequenceEqual(this.Suffix.Span)
				: other.Length >= 1 && other[0] == 0xFF && other[1..].SequenceEqual(this.Suffix.Span);
		}

		/// <inheritdoc />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool Equals<TOtherKey>(in TOtherKey other)
			where TOtherKey : struct, IFdbKey
		{
			if (typeof(TOtherKey) == typeof(FdbSystemKey))
			{
				return this.IsSpecial == ((FdbSystemKey) (object) other).IsSpecial && this.Suffix.Equals(((FdbSystemKey) (object) other).Suffix);
			}
			if (typeof(TOtherKey) == typeof(FdbRawKey))
			{
				return Equals(((FdbRawKey) (object) other).Data);
			}
			return FdbKeyHelpers.Equals(in this, in other);
		}

		/// <inheritdoc />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public int CompareTo(FdbSystemKey other) => this.IsSpecial == other.IsSpecial
			? this.Suffix.CompareTo(other.Suffix)
			: FdbKeyHelpers.CompareTo(in this, in other);

		/// <inheritdoc />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public int CompareTo(FdbRawKey other)
			=> !this.IsSpecial && other.Data.StartsWith(0xFF)
				? Suffix.CompareTo(other.Data.Span[1..])
				: FdbKeyHelpers.CompareTo(in this, other.Data);

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
			=> destination.TryWrite(provider, $"`{(this.IsSpecial ? "<FF><FF>" : "<FF>")}{this.Suffix}`", out charsWritten);

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
			sizeHint = (this.IsSpecial ? 2 : 1) + this.Suffix.Count;
			return true;
		}

		/// <inheritdoc />
		public bool TryEncode(Span<byte> destination, out int bytesWritten)
		{
			if (this.IsSpecial)
			{
				if (destination.Length > 2 && this.Suffix.TryCopyTo(destination[2..], out var dataLen))
				{
					destination[0] = 0xFF;
					destination[1] = 0xFF;
					bytesWritten = 2 + dataLen;
					return true;
				}
			}
			else
			{
				if (destination.Length > 1 && this.Suffix.TryCopyTo(destination[1..], out var dataLen))
				{
					destination[0] = 0xFF;
					bytesWritten = 1 + dataLen;
					return true;
				}
			}
			bytesWritten = 0;
			return false;
		}

	}

}
