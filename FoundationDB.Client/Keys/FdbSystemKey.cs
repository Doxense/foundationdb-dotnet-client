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
	using System.ComponentModel;

	/// <summary>Wraps a <see cref="Slice"/> that wraps a key in either the System (<c>`\xFF`</c>) or Special Key (<c>`\xFF\xFF`</c>) subspaces</summary>
	public readonly struct FdbSystemKey : IFdbKey
		, IEquatable<FdbSystemKey>, IComparable<FdbSystemKey>
	{

		public static readonly FdbSystemKey System = new(Slice.Empty, special: false);

		public static readonly FdbSystemKey Special = new(Slice.Empty, special: true);

		/// <summary><c>`\xFF/metadataVersion`</c>: contains the global metadata version for the database</summary>
		/// <remarks>See <see cref="IFdbReadOnlyTransaction.GetMetadataVersionKeyAsync"/> and <see cref="IFdbTransaction.TouchMetadataVersionKey"/> for more details</remarks>
		public static readonly FdbSystemKey MetadataVersion = new("/metadataVersion", special: false);

		/// <summary><c>`\xFF\xFF/status/json</c>: returns a JSON document describing the current status of the client and cluster.</summary>
		/// <remarks>See <see cref="Fdb.System.GetStatusAsync(IFdbReadOnlyTransaction)"/> for more details.</remarks>
		public static readonly FdbSystemKey StatusJson = new("/status/json", special: true);

		public static readonly FdbSystemKey TransactionConflictingKeys = new("/transaction/conflicting_keys/", special: true);

		[SkipLocalsInit, MethodImpl(MethodImplOptions.AggressiveInlining)]
		internal FdbSystemKey(Slice suffix, bool special)
		{
			this.IsSpecial = special;
			this.SuffixBytes = suffix;
			this.SuffixString = null;
		}

		[SkipLocalsInit, MethodImpl(MethodImplOptions.AggressiveInlining)]
		internal FdbSystemKey(string suffix, bool special)
		{
			this.IsSpecial = special;
			this.SuffixBytes = default;
			this.SuffixString = suffix;
		}

		/// <summary>If <c>true</c>, uses the <c>`\xFF\xFF`</c> prefix (for special keys); otherwise, uses the <c>`\xFF`</c> prefix (for regular system keys)</summary>
		public readonly bool IsSpecial;

		/// <summary>Rest of the key (as bytes)</summary>
		public readonly Slice SuffixBytes;

		/// <summary>Rest of the key (as raw string)</summary>
		public readonly string? SuffixString;

		/// <inheritdoc />
		IKeySubspace? IFdbKey.GetSubspace() => null;

		public FdbSystemKey Bytes(FdbRawKey key) => Bytes(key.Data);

		[Pure]
		public FdbSystemKey Bytes(Slice suffix)
		{
			if (suffix.Count == 0) return this;

			int count = this.SuffixString?.Length ?? this.SuffixBytes.Count;

			if (count == 0)
			{
				if (!this.IsSpecial && suffix[0] == 0xFF)
				{
					return suffix.Count > 1 ? new(suffix[1..], special: true) : Special;
				}
				return new(suffix, this.IsSpecial);
			}

			return this.SuffixString is null
				? new(this.SuffixBytes + suffix, special: IsSpecial)
				: new(Slice.FromStringAscii(this.SuffixString) + suffix, special: IsSpecial);
		}

		[Pure]
		public FdbSystemKey Bytes(ReadOnlySpan<byte> suffix)
		{
			if (suffix.Length == 0) return this;

			int count = this.SuffixString?.Length ?? this.SuffixBytes.Count;

			if (count == 0)
			{
				if (!this.IsSpecial && suffix[0] == 0xFF)
				{
					return suffix.Length > 1 ? new(Slice.FromBytes(suffix[1..]), special: true) : Special;
				}
				return new(Slice.FromBytes(suffix), this.IsSpecial);
			}

			return this.SuffixString is null
				? new(this.SuffixBytes.Concat(suffix), special: IsSpecial)
				: new(Slice.FromStringAscii(this.SuffixString).Concat(suffix), special: IsSpecial);
		}

		[Pure]
		public FdbSystemKey Bytes(string suffix)
		{
			if (this.SuffixString is not null)
			{
				return new(this.SuffixString + suffix, special: this.IsSpecial);
			}
			return Bytes(Slice.FromStringAscii(suffix));
		}

		[Pure]
		public FdbSystemKey Bytes(ReadOnlySpan<char> suffix)
		{
			if (suffix.Length == 0) return this;
			if (this.SuffixString is not null)
			{
				return new(string.Concat(this.SuffixString, suffix), special: this.IsSpecial);
			}
			return Bytes(Slice.FromStringAscii(suffix));
		}

		#region Equals(...)

		/// <inheritdoc />
		[EditorBrowsable(EditorBrowsableState.Never)]
		public override int GetHashCode() => throw FdbKeyHelpers.ErrorCannotComputeHashCodeMessage();

		/// <inheritdoc />
		public override bool Equals([NotNullWhen(true)] object? other) => other switch
		{
			Slice bytes => this.Equals(bytes),
			FdbSystemKey key => this.Equals(key),
			FdbRawKey key => this.Equals(key),
			IFdbKey key => FdbKeyHelpers.AreEqual(in this, key),
			_ => false,
		};

		/// <inheritdoc />
		public bool Equals([NotNullWhen(true)] IFdbKey? other) => other switch
		{
			null => false,
			FdbSystemKey key => this.Equals(key),
			FdbRawKey key => this.Equals(key),
			_ => FdbKeyHelpers.AreEqual(in this, other),
		};

		/// <inheritdoc />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool FastEqualTo<TOtherKey>(in TOtherKey other)
			where TOtherKey : struct, IFdbKey
		{
			if (typeof(TOtherKey) == typeof(FdbSystemKey))
			{
				return Equals((FdbSystemKey) (object) other);
			}
			if (typeof(TOtherKey) == typeof(FdbRawKey))
			{
				return Equals(((FdbRawKey) (object) other).Data);
			}
			return FdbKeyHelpers.AreEqual(in this, in other);
		}

		/// <inheritdoc />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool Equals(FdbSystemKey other)
		{
			if (this.IsSpecial != other.IsSpecial)
			{
				//BUGBUG: we need to check if one has \xFF in its suffix !
				return false;
			}

			return (this.SuffixString, other.SuffixString) switch
			{
				(not null, not null) => this.SuffixString == other.SuffixString,
				(null, null) => this.SuffixBytes.Equals(other.SuffixBytes),
				_ => FdbKeyHelpers.AreEqual(in this, in other)
			};
		}

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
			if (this.SuffixString is not null)
			{ // no easy fast path
				return FdbKeyHelpers.AreEqual(in this, other);
			}
			return this.IsSpecial
				? other.Length >= 2 && other[0] == 0xFF && other[1] == 0xFF && other[2..].SequenceEqual(this.SuffixBytes.Span)
				: other.Length >= 1 && other[0] == 0xFF && other[1..].SequenceEqual(this.SuffixBytes.Span);
		}

		/// <inheritdoc />
		public int CompareTo(object? obj) => obj switch
		{
			Slice key => CompareTo(key.Span),
			FdbRawKey key => CompareTo(key.Span),
			FdbSystemKey key => CompareTo(key),
			IFdbKey other => FdbKeyHelpers.Compare(in this, other),
			_ => throw new NotSupportedException()
		};

		/// <inheritdoc />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public int CompareTo(FdbSystemKey other)
		{
			if (this.IsSpecial == other.IsSpecial)
			{
				switch (this.SuffixString, other.SuffixString)
				{
					case (not null, not null): return string.CompareOrdinal(this.SuffixString, other.SuffixString);
					case (null, null): return this.SuffixBytes.CompareTo(other.SuffixBytes);
				}
			}
			// fallback: slow path
			return FdbKeyHelpers.Compare(in this, in other);
		}

		/// <inheritdoc />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public int CompareTo(FdbRawKey other) => CompareTo(other.Span);

		/// <inheritdoc />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public int CompareTo(Slice other) => CompareTo(other.Span);

		/// <inheritdoc cref="CompareTo(Slice)" />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public int CompareTo(ReadOnlySpan<byte> other)
		{
			if (other.Length == 0 || other[0] != 0xFF)
			{ // the key is by definition smaller
				return +1;
			}

			if (this.IsSpecial)
			{ // \xFF \xFF ...
				if (other.Length > 1 && other[1] != 0xFF)
				{
					return +1;
				}

				if (this.SuffixString is null)
				{
					return this.SuffixBytes.CompareTo(other[2..]);
				}
			}
			else
			{ // \xFF ...
				if (this.SuffixString is null)
				{
					return this.SuffixBytes.CompareTo(other[1..]);
				}
			}

			// fallback to slow path
			return FdbKeyHelpers.Compare(in this, other);
		}

		/// <inheritdoc />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public int FastCompareTo<TOtherKey>(in TOtherKey other)
			where TOtherKey : struct, IFdbKey
			=> FdbKeyHelpers.Compare(in this, in other);

		#endregion

		/// <inheritdoc />
		public override string ToString() => ToString(null);

		/// <inheritdoc />
		public string ToString(string? format, IFormatProvider? formatProvider = null)
			=> string.Create(formatProvider, $"{this}");

		/// <inheritdoc />
		public bool TryFormat(Span<char> destination, out int charsWritten, ReadOnlySpan<char> format, IFormatProvider? provider)
			=> this.SuffixString is not null
				? (this.SuffixString.Length == 0
					? (this.IsSpecial ? "<FF><FF>" : "<FF>").TryCopyTo(destination, out charsWritten)
					: destination.TryWrite(provider, $"{(this.IsSpecial ? "<FF><FF>" : "<FF>")}{Slice.FromStringAscii(this.SuffixString):R}", out charsWritten)) //PERF: optimize if string is "clean" (nothing encoded as <XX>)
				: (this.SuffixBytes.Count == 0
					? (this.IsSpecial ? "<FF><FF>" : "<FF>").TryCopyTo(destination, out charsWritten)
					: destination.TryWrite(provider, $"{(this.IsSpecial ? "<FF><FF>" : "<FF>")}{this.SuffixBytes:R}", out charsWritten));

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
			sizeHint = checked((this.IsSpecial ? 2 : 1) + (this.SuffixString?.Length ?? this.SuffixBytes.Count));
			return true;
		}

		/// <inheritdoc />
		public bool TryEncode(Span<byte> destination, out int bytesWritten)
		{
			if (this.SuffixString is null)
			{ // we already have the encoded bytes

				if (this.IsSpecial)
				{
					if (destination.Length <= 2 || !this.SuffixBytes.TryCopyTo(destination[2..], out var dataLen))
					{
						goto too_small;
					}

					destination[0] = 0xFF;
					destination[1] = 0xFF;
					bytesWritten = 2 + dataLen;
					return true;
				}
				else
				{
					if (destination.Length <= 1 || !this.SuffixBytes.TryCopyTo(destination[1..], out var dataLen))
					{
						goto too_small;
					}

					destination[0] = 0xFF;
					bytesWritten = 1 + dataLen;
					return true;
				}
			}

			// we need to encode the string "in place"
			var tail = destination;
			if (this.IsSpecial)
			{
				if (tail.Length < 2) goto too_small;
				tail[0] = 0xFF;
				tail[1] = 0xFF;
				tail = tail[2..];
			}
			else
			{
				if (tail.Length < 1) goto too_small;
				tail[0] = 0xFF;
				tail = tail[1..];
			}

			var chars = SuffixString.AsSpan();
			if (chars.Length > tail.Length) goto too_small;
			//PERF: use SIMD? or some hack by casting to RoS<byte> and copying only one every two bytes?
			for(int i = 0; i < chars.Length; i++)
			{
				tail[i] = unchecked((byte) chars[i]); //REVIEW: overflow or truncate?
			}
			tail = tail[chars.Length..];

			bytesWritten = destination.Length - tail.Length;
			return true;

		too_small:
			bytesWritten = 0;
			return false;
		}

	}

}
