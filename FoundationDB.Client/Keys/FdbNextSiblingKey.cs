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

	/// <summary>Key that increments the last byte (with carry) of another key</summary>
	/// <typeparam name="TKey">Type of the parent key</typeparam>
	/// <remarks>
	/// <para>This key is the first key that comes after all the children of its previous sibling</para>
	/// <para>It is frequently used as the end (exclusive) of a range read.</para>
	/// </remarks>
	public readonly struct FdbNextSiblingKey<TKey> : IFdbKey
		, IEquatable<FdbNextSiblingKey<TKey>>, IComparable<FdbNextSiblingKey<TKey>>
		where TKey : struct, IFdbKey
	{

		public FdbNextSiblingKey(in TKey parent)
		{
			this.Parent = parent;
		}

		public readonly TKey Parent;
		
		/// <inheritdoc />
		public override string ToString() => ToString(null);

		/// <inheritdoc />
		public string ToString(string? format, IFormatProvider? formatProvider = null)
			=> string.Create(formatProvider, $"{this}");

		/// <inheritdoc />
		public bool TryFormat(Span<char> destination, out int charsWritten, ReadOnlySpan<char> format, IFormatProvider? provider)
			=> destination.TryWrite(provider, $"NextSibling({this.Parent})", out charsWritten);

		/// <inheritdoc />
		IKeySubspace? IFdbKey.GetSubspace() => this.Parent.GetSubspace();

		#region Equals(...)

		/// <inheritdoc />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool Equals(FdbNextSiblingKey<TKey> other)
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
			if (typeof(TOtherKey) == typeof(FdbNextSiblingKey<TKey>))
			{
				return FdbKeyHelpers.Equals(in this.Parent, in Unsafe.As<TOtherKey, FdbNextSiblingKey<TKey>>(ref Unsafe.AsRef(in other)).Parent);
			}
			if (typeof(TOtherKey) == typeof(FdbRawKey))
			{
				return FdbKeyHelpers.Equals(in this, ((FdbRawKey) (object) other).Data);
			}
			return FdbKeyHelpers.Equals(in this, in other);
		}

		/// <inheritdoc />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public int CompareTo(FdbNextSiblingKey<TKey> other)
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
			// we cannot modify the original span, so we don't support this operation
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

			// incrementing the key can only produce a key of the same size or smaller, except when the key is empty in which case the successor has size 1
			sizeHint = Math.Max(size, 1);
			return true;
		}

		/// <inheritdoc />
		public bool TryEncode(scoped Span<byte> destination, out int bytesWritten)
		{
			// first generate the wrapped key
			if (!this.Parent.TryEncode(destination, out var len))
			{
				goto too_small;
			}

			// if the key is empty (??) we need to return 'x00'
			if (len == 0)
			{
				goto empty_key;
			}

			// increment the buffer in-place
			// => throws if the key is all FF
			FdbKey.Increment(destination[..len], out len);
			bytesWritten = len;
			return true;

		empty_key:
			if (destination.Length == 0)
			{
				goto too_small;
			}
			destination[0] = 0;
			bytesWritten = 1;
			return true;

		too_small:
			bytesWritten = 0;
			return false;

		}

	}

}
