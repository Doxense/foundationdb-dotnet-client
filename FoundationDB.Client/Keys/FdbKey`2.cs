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

	/// <summary>Wraps a value that will be encoded to get the corresponding key, relative to a subspace</summary>
	/// <typeparam name="TKey">Type of the key</typeparam>
	/// <typeparam name="TEncoder">Type of the <see cref="ISpanEncoder{TValue}"/> that can convert this key into a binary representation</typeparam>
	[DebuggerDisplay("Data={Data}")]
	public readonly struct FdbKey<TKey, TEncoder>: IFdbKey
		, IEquatable<FdbKey<TKey, TEncoder>>, IComparable<FdbKey<TKey, TEncoder>>
		where TEncoder: struct, ISpanEncoder<TKey>
	{

		public FdbKey(IKeySubspace subspace, TKey data)
		{
			this.Data = data;
			this.Subspace = subspace;
		}

		/// <summary>Content of the key</summary>
		public readonly TKey Data;

		/// <summary>Optional subspace that contains this key</summary>
		public readonly IKeySubspace Subspace;

		/// <inheritdoc />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public IKeySubspace? GetSubspace() => this.Subspace;

		#region Equals(...)

		/// <inheritdoc />
		public bool Equals(FdbKey<TKey, TEncoder> other)
			=> FdbKeyHelpers.Equals(this.Subspace, other.Subspace) && EqualityComparer<TKey>.Default.Equals(this.Data, other.Data);

		/// <inheritdoc />
		public bool Equals(FdbRawKey other)
			=> FdbKeyHelpers.Equals(in this, other.Data);

		/// <inheritdoc />
		public bool Equals(Slice other)
			=> FdbKeyHelpers.Equals(in this, other);

		/// <inheritdoc cref="Equals(Slice)" />
		public bool Equals(ReadOnlySpan<byte> other)
			=> FdbKeyHelpers.Equals(in this, other);

		/// <inheritdoc />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool Equals<TOtherKey>(in TOtherKey other)
			where TOtherKey : struct, IFdbKey
		{
			if (typeof(TOtherKey) == typeof(FdbKey<TKey, TEncoder>))
			{
				return FdbKeyHelpers.Equals(this.Subspace, ((FdbKey<TKey, TEncoder>) (object) other).Subspace) && EqualityComparer<TKey>.Default.Equals(this.Data, ((FdbKey<TKey, TEncoder>) (object) other).Data);
			}
			if (typeof(TOtherKey) == typeof(FdbRawKey))
			{
				return FdbKeyHelpers.Equals(in this, ((FdbRawKey) (object) other).Data);
			}
			return FdbKeyHelpers.Equals(in this, in other);
		}

		/// <inheritdoc />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public int CompareTo(FdbKey<TKey, TEncoder> other)
			=> FdbKeyHelpers.CompareTo(in this, in other);

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
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool TryGetSpan(out ReadOnlySpan<byte> span)
		{
			//note: we could if the parent subspace did not have any prefix, which is not allowed
			span = default;
			return false;
		}

		/// <inheritdoc />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool TryGetSizeHint(out int sizeHint)
		{
			if (!TEncoder.TryGetSizeHint(this.Data, out var dataSize))
			{
				sizeHint = 0;
				return false;
			}

			sizeHint = checked(dataSize + this.Subspace.GetPrefix().Count);
			return true;
		}

		/// <inheritdoc />
		[MustUseReturnValue, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool TryEncode(Span<byte> destination, out int bytesWritten)
		{
			{
				if (this.Subspace.GetPrefix().TryCopyTo(destination, out var prefixLen)
				 && TEncoder.TryEncode(destination, out var dataLen, in this.Data))
				{
					bytesWritten = prefixLen + dataLen;
					return true;
				}

				bytesWritten = 0;
				return false;
			}
		}

		/// <inheritdoc />
		public override string ToString() => ToString(null);

		/// <inheritdoc />
		public string ToString(string? format, IFormatProvider? provider = null)
		{
			return string.Create(CultureInfo.InvariantCulture, $"{this.Subspace}:{this.Data}");
		}

		/// <inheritdoc />
		public bool TryFormat(Span<char> destination, out int charsWritten, ReadOnlySpan<char> format, IFormatProvider? provider)
		{
			return destination.TryWrite(CultureInfo.InvariantCulture, $"{this.Subspace}:{this.Data}", out charsWritten);
		}

	}

}
