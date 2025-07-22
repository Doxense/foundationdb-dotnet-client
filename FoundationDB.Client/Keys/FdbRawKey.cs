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

	using System.Numerics;

	/// <summary>Wraps a <see cref="Slice"/> that contains a pre-encoded key in the database</summary>
	/// <remarks>
	/// <para>This can help in situations where the same key will be frequently used in the same transaction, and where the cost of allocating on the heap will be less than re-encoding the key over and over.</para>
	/// <para>Please refrain from capturing a key in once transaction and reusing it in another. It is possible that the original subspace would be moved or re-created between both transactions, with a different prefix, which could create silent data corruption!</para>
	/// </remarks>
	public readonly struct FdbRawKey : IFdbKey
		, IComparisonOperators<FdbRawKey, FdbRawKey, bool>
		, IComparisonOperators<FdbRawKey, Slice, bool>
	{

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		[SkipLocalsInit]
		internal FdbRawKey(Slice data)
		{
			this.Data = data;
		}

		/// <summary>Pre-encoded bytes for this key</summary>
		public readonly Slice Data;

		[Pure]
		public ReadOnlySpan<byte> Span => this.Data.Span;

		/// <inheritdoc />
		IKeySubspace? IFdbKey.GetSubspace() => null;

		#region Equals(...)

		/// <inheritdoc />
		public override bool Equals([NotNullWhen(true)] object? other)
		{
			return other switch
			{
				FdbRawKey key => this.Equals(key.Data),
				FdbTupleKey key => key.Equals(this),
				Slice bytes => this.Equals(bytes),
				IFdbKey key => FdbKeyHelpers.Equals(in this, key),
				_ => false,
			};
		}

		/// <inheritdoc />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public override int GetHashCode() => this.Data.GetHashCode();

		/// <inheritdoc />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool Equals(FdbRawKey other) => this.Data.Equals(other.Data);

		/// <inheritdoc />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool Equals(Slice other) => this.Data.Equals(other);

		/// <inheritdoc cref="Equals(Slice)"/>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool Equals(ReadOnlySpan<byte> other) => this.Data.Equals(other);

		/// <inheritdoc />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool Equals<TOtherKey>(in TOtherKey other)
			where TOtherKey : struct, IFdbKey
		{
			if (typeof(TOtherKey) == typeof(FdbRawKey))
			{
				return this.Data.Equals(((FdbRawKey) (object) other).Data);
			}
			return FdbKeyHelpers.Equals(in this, in other);
		}

		/// <inheritdoc />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public int CompareTo(FdbRawKey other) => this.Data.CompareTo(other.Data);

		/// <inheritdoc />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public int CompareTo(Slice other) => this.Data.CompareTo(other);

		/// <inheritdoc cref="CompareTo(Slice)" />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public int CompareTo(ReadOnlySpan<byte> other) => this.Data.Span.SequenceCompareTo(other);

		/// <inheritdoc />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public int CompareTo<TOtherKey>(in TOtherKey other)
			where TOtherKey : struct, IFdbKey
		{
			if (typeof(TOtherKey) == typeof(FdbRawKey))
			{
				return this.Data.CompareTo(((FdbRawKey) (object) other).Data);
			}
			return FdbKeyHelpers.CompareTo(in this, in other);
		}

		#region Operators...

		/// <inheritdoc />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool operator ==(FdbRawKey left, FdbRawKey right) => left.Data.Equals(right.Data);

		/// <inheritdoc />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool operator ==(FdbRawKey left, Slice right) => left.Data.Equals(right);

		/// <inheritdoc />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool operator !=(FdbRawKey left, FdbRawKey right) => !left.Data.Equals(right.Data);

		/// <inheritdoc />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool operator !=(FdbRawKey left, Slice right) => !left.Data.Equals(right);

		/// <inheritdoc />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool operator <(FdbRawKey left, FdbRawKey right) => left.Data.CompareTo(right.Data) < 0;

		/// <inheritdoc />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool operator <(FdbRawKey left, Slice right) => left.Data.CompareTo(right) < 0;

		/// <inheritdoc />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool operator <=(FdbRawKey left, FdbRawKey right) => left.Data.CompareTo(right.Data) <= 0;

		/// <inheritdoc />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool operator <=(FdbRawKey left, Slice right) => left.Data.CompareTo(right) <= 0;

		/// <inheritdoc />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool operator >(FdbRawKey left, FdbRawKey right) => left.Data.CompareTo(right.Data) > 0;

		/// <inheritdoc />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool operator >(FdbRawKey left, Slice right) => left.Data.CompareTo(right) > 0;

		/// <inheritdoc />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool operator >=(FdbRawKey left, FdbRawKey right) => left.Data.CompareTo(right.Data) >= 0;

		/// <inheritdoc />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool operator >=(FdbRawKey left, Slice right) => left.Data.CompareTo(right) >= 0;

		#endregion

		#endregion

		/// <inheritdoc />
		public override string ToString() => ToString(null);

		/// <inheritdoc />
		public string ToString(string? format, IFormatProvider? formatProvider = null)
			=> string.Create(formatProvider, $"{this}");

		/// <inheritdoc />
		public bool TryFormat(Span<char> destination, out int charsWritten, ReadOnlySpan<char> format = default, IFormatProvider? provider = null)
			=> this.Data.Count > 0
				? destination.TryWrite(provider, $"`{this.Data}`", out charsWritten)
				: (this.Data.IsNull ? "<null>" : "``").TryCopyTo(destination, out charsWritten);

		/// <inheritdoc />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool TryGetSpan(out ReadOnlySpan<byte> span)
		{
			span = this.Data.Span;
			return true;
		}

		/// <inheritdoc />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool TryGetSizeHint(out int sizeHint)
		{
			sizeHint = this.Data.Count;
			return true;
		}

		/// <inheritdoc />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool TryEncode(Span<byte> destination, out int bytesWritten)
		{
			return this.Data.TryCopyTo(destination, out bytesWritten);
		}

		/// <summary>Converts a slice into a <see cref="FdbRawKey"/></summary>
		public static implicit operator FdbRawKey(Slice key) => new(key);

	}

}
