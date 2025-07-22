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

	/// <summary>Wraps the key prefix of a subspace</summary>
	public readonly struct FdbSubspaceKey : IFdbKey
		, IEquatable<FdbSubspaceKey>, IComparable<FdbSubspaceKey>
	{

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		[SkipLocalsInit]
		internal FdbSubspaceKey(IKeySubspace subspace)
		{
			this.Subspace = subspace;
		}

		public readonly IKeySubspace Subspace;

		/// <inheritdoc />
		IKeySubspace? IFdbKey.GetSubspace() => this.Subspace;

		#region Equals(...)

		/// <inheritdoc />
		public override bool Equals([NotNullWhen(true)] object? other)
		{
			return other switch
			{
				FdbSubspaceKey key => this.Equals(key),
				FdbRawKey key => this.Equals(key),
				Slice bytes => this.Equals(bytes),
				IFdbKey key => FdbKeyHelpers.Equals(in this, key),
				_ => false,
			};
		}

		/// <inheritdoc />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public override int GetHashCode() => this.Subspace.GetHashCode(); //BUGBUG: TODO: this breaks the contracts for equality

		/// <inheritdoc />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool Equals(FdbSubspaceKey other) => FdbKeyHelpers.Equals(this.Subspace, other.Subspace);

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
			if (typeof(TOtherKey) == typeof(FdbSubspaceKey))
			{
				return FdbKeyHelpers.Equals(this.Subspace, ((FdbSubspaceKey) (object) other).Subspace);
			}
			if (typeof(TOtherKey) == typeof(FdbRawKey))
			{
				return FdbKeyHelpers.Equals(in this, ((FdbRawKey) (object) other).Data);
			}
			return FdbKeyHelpers.Equals(in this, in other);
		}

		/// <inheritdoc />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public int CompareTo(FdbSubspaceKey other)
			=> FdbKeyHelpers.CompareTo(this.Subspace, other.Subspace);

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
			=> destination.TryWrite(provider, $"{this.Subspace}", out charsWritten);

		/// <inheritdoc />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool TryGetSpan(out ReadOnlySpan<byte> span)
			=> this.Subspace.TryGetSpan(out span);

		/// <inheritdoc />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool TryGetSizeHint(out int sizeHint)
			=> this.Subspace.TryGetSizeHint(out sizeHint);

		/// <inheritdoc />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool TryEncode(Span<byte> destination, out int bytesWritten)
			=> this.Subspace.TryEncode(destination, out bytesWritten);

	}

}
