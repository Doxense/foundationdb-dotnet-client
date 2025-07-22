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

	public readonly struct FdbSuccessorKey<TKey> : IFdbKey
		, IEquatable<FdbSuccessorKey<TKey>>, IComparable<FdbSuccessorKey<TKey>>
		where TKey : struct, IFdbKey
	{

		public FdbSuccessorKey(in TKey parent)
		{
			this.Parent = parent;
		}

		public readonly TKey Parent;
		
		/// <inheritdoc />
		public override string ToString() => ToString(null);

		/// <inheritdoc />
		public string ToString(string? format, IFormatProvider? formatProvider = null)
			=> $"SuccessorOf({this.Parent.ToString(format, formatProvider)})";

		/// <inheritdoc />
		public bool TryFormat(Span<char> destination, out int charsWritten, ReadOnlySpan<char> format, IFormatProvider? provider)
			=> destination.TryWrite(provider, $"SuccessorOf({this.Parent})", out charsWritten);

		/// <inheritdoc />
		IKeySubspace? IFdbKey.GetSubspace() => this.Parent.GetSubspace();

		#region Equals(...)

		/// <inheritdoc />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool Equals(FdbSuccessorKey<TKey> other)
			=> FdbKeyHelpers.Equals(in this.Parent, in other.Parent);

		/// <inheritdoc />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool Equals(FdbRawKey other)
			=> FdbKeyHelpers.Equals(in this, other.Data);

		/// <inheritdoc />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool Equals(Slice other)
			=> FdbKeyHelpers.Equals(in this, other);

		/// <inheritdoc cref="Equals(Slice)" />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool Equals(ReadOnlySpan<byte> other)
			=> FdbKeyHelpers.Equals(in this, other);

		/// <inheritdoc />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool Equals<TOtherKey>(in TOtherKey other)
			where TOtherKey : struct, IFdbKey
		{
			if (typeof(TOtherKey) == typeof(FdbSuccessorKey<TKey>))
			{
				return FdbKeyHelpers.Equals(in this.Parent, in Unsafe.As<TOtherKey, FdbSuccessorKey<TKey>>(ref Unsafe.AsRef(in other)).Parent);
			}
			if (typeof(TOtherKey) == typeof(FdbRawKey))
			{
				return FdbKeyHelpers.Equals(in this, ((FdbRawKey) (object) other).Data);
			}
			return FdbKeyHelpers.Equals(in this, in other);
		}

		/// <inheritdoc />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public int CompareTo(FdbSuccessorKey<TKey> other)
			=> FdbKeyHelpers.CompareTo(in this.Parent, in other.Parent);

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
		public bool TryGetSpan(out ReadOnlySpan<byte> span)
		{
			// we cannot modify the original span to append 0x00, so we don't support this operation
			span = default;
			return false;
		}

		/// <inheritdoc />
		public bool TryGetSizeHint(out int sizeHint)
		{
			if (!this.Parent.TryGetSizeHint(out var size))
			{
				sizeHint = 0;
				return false;
			}

			// we always add 1 to the original size
			sizeHint = checked(size + 1);
			return true;
		}

		/// <inheritdoc />
		public bool TryEncode(scoped Span<byte> destination, out int bytesWritten)
		{
			// first generate the wrapped key
			if (!this.Parent.TryEncode(destination, out var len)
			    || destination.Length <= len)
			{
				bytesWritten = 0;
				return false;
			}

			// appends the 0x00 at the end
			destination[len] = 0;
			bytesWritten = checked(len + 1);
			return true;
		}

	}

}
