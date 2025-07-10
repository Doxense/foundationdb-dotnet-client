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

	public static partial class FdbKey
	{

		public const int MaxSize = Fdb.MaxKeySize;

		/// <summary>Returns a key that wraps a <see cref="Slice"/></summary>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static FdbKey<Slice, SpanEncoders.RawEncoder> Create(Slice key) => new(key);

		/// <summary>Returns a key that wraps a byte array</summary>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static FdbKey<byte[], SpanEncoders.RawEncoder> Create(byte[] key) => new(key);

		/// <summary>Returns a key that wraps a tuple inside a <see cref="IDynamicKeySubspace"/></summary>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static FdbKey<(TSubspace Subspace, TTuple Items), SubspaceTupleEncoder<TSubspace, TTuple>> Create<TSubspace, TTuple>(TSubspace subspace, in TTuple key)
			where TSubspace : IDynamicKeySubspace
			where TTuple : IVarTuple
			=> new((subspace, key), subspace);

		public readonly struct SubspaceTupleEncoder<TSubspace, TTuple> : ISpanEncoder<(TSubspace Subspace, TTuple Items)>
			where TSubspace : IDynamicKeySubspace
			where TTuple : IVarTuple
		{
			/// <inheritdoc />
			public static bool TryGetSpan(scoped in (TSubspace Subspace, TTuple Items) value, out ReadOnlySpan<byte> span)
			{
				span = default;
				return false;
			}

			/// <inheritdoc />
			public static bool TryGetSizeHint(scoped in (TSubspace Subspace, TTuple Items) value, out int sizeHint)
			{
				sizeHint = 0;
				return false;
			}

			/// <inheritdoc />
			public static bool TryEncode(Span<byte> destination, out int bytesWritten, scoped in (TSubspace Subspace, TTuple Items) value)
			{
				return value.Subspace.TryPack(destination, out bytesWritten, value.Items);
			}
		}

		/// <summary>Returns a key that wraps a relative key inside a <see cref="IBinaryKeySubspace"/></summary>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static FdbKey<(TSubspace Subspace, Slice Suffix), SubspaceRawEncoder<TSubspace>> Create<TSubspace, TTuple>(TSubspace subspace, Slice relativeKey)
			where TSubspace : IBinaryKeySubspace
			=> new((subspace, relativeKey), subspace);

		public readonly struct SubspaceRawEncoder<TSubspace> : ISpanEncoder<(TSubspace Subspace, Slice Suffix)>
			where TSubspace : IBinaryKeySubspace
		{
			/// <inheritdoc />
			public static bool TryGetSpan(scoped in (TSubspace Subspace, Slice Suffix) value, out ReadOnlySpan<byte> span)
			{
				span = default;
				return false;
			}

			/// <inheritdoc />
			public static bool TryGetSizeHint(scoped in (TSubspace Subspace, Slice Suffix) value, out int sizeHint)
			{
				sizeHint = 0;
				return false;
			}

			/// <inheritdoc />
			public static bool TryEncode(Span<byte> destination, out int bytesWritten, scoped in (TSubspace Subspace, Slice Suffix) value)
			{
				return value.Subspace.TryEncode(destination, out bytesWritten, value.Suffix.Span);
			}

		}

	}

	/// <summary>Represents a typed key in the database</summary>
	/// <typeparam name="TKey">Type of the key</typeparam>
	/// <typeparam name="TEncoder">Type of the <see cref="ISpanEncoder{TValue}"/> that can convert this key into a binary representation</typeparam>
	[DebuggerDisplay("Data={Data}")]
	public readonly struct FdbKey<TKey, TEncoder> : ISpanFormattable
		where TEncoder: struct, ISpanEncoder<TKey>
	{

		public FdbKey(TKey data, IKeySubspace? subspace = null)
		{
			this.Data = data;
			this.Subspace = subspace;
		}

		/// <summary>Content of the key</summary>
		public readonly TKey Data;

		/// <summary>Optional subspace that contains this key</summary>
		public readonly IKeySubspace? Subspace;

		/// <summary>Returns the already encoded binary representation of the key, if available.</summary>
		/// <param name="span">Points to the in-memory binary representation of the encoded key</param>
		/// <returns><c>true</c> if the key has already been encoded, or it its in-memory layout is the same; otherwise, <c>false</c></returns>
		/// <remarks>
		/// <para>This is only intended to support pass-through or already encoded keys (via <see cref="Slice"/>), or when the binary representation has been cached to allow multiple uses.</para>
		/// </remarks>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool TryGetSpan(out ReadOnlySpan<byte> span) => TEncoder.TryGetSpan(this.Data, out span);

		/// <summary>Returns a quick estimate of the initial buffer size that would be large enough for the vast majority of values, in a single call to <see cref="TryEncode"/></summary>
		/// <param name="sizeHint">Receives an initial capacity that will satisfy almost all values. There is no guarantees that the size will be exact, and the value may be smaller or larger than the actual encoded size.</param>
		/// <returns><c>true</c> if a size could be quickly computed; otherwise, <c>false</c></returns>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool TryGetSizeHint(out int sizeHint) => TEncoder.TryGetSizeHint(this.Data, out sizeHint);

		/// <summary>Encodes a <typeparam name="TKey"> into its equivalent binary representation in the database</typeparam></summary>
		/// <param name="destination">Destination buffer</param>
		/// <param name="bytesWritten">Number of bytes written to the buffer</param>
		/// <returns><c>true</c> if the operation was successful and the buffer was large enough, or <c>false</c> if it was too small</returns>
		[MustUseReturnValue, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool TryEncode(Span<byte> destination, out int bytesWritten) => TEncoder.TryEncode(destination, out bytesWritten, in this.Data);

		/// <summary>Encodes this key into <see cref="Slice"/></summary>
		/// <returns>Slice that contains the binary representation of this key</returns>
		[Pure]
		public Slice ToSlice()
		{
			var pool = ArrayPool<byte>.Shared;
			SpanEncoders.Encode<TEncoder, TKey>(in this.Data, pool, Fdb.MaxKeySize, out var buffer, out var span, out _);
			var slice = span.ToSlice();
			if (buffer is not null)
			{
				pool.Return(buffer);
			}
			return slice;
		}

		/// <summary>Encodes this key into <see cref="Slice"/>, using backing buffer rented from a pool</summary>
		/// <param name="pool">Pool used to rent the buffer (<see cref="ArrayPool{T}.Shared"/> is <c>null</c>)</param>
		/// <returns><see cref="SliceOwner"/> that contains the binary representation of this key</returns>
		[Pure, MustDisposeResource]
		public SliceOwner ToSlice(ArrayPool<byte>? pool)
		{
			pool ??= ArrayPool<byte>.Shared;

			SpanEncoders.Encode<TEncoder, TKey>(in this.Data, pool, Fdb.MaxKeySize, out var buffer, out var span, out var range);

			return buffer is null
				? SliceOwner.Copy(span, pool)
				: SliceOwner.Create(buffer.AsSlice(range));
		}

		/// <inheritdoc />
		public override string ToString() => ToString(null, null);

		/// <inheritdoc />
		public string ToString(string? format, IFormatProvider? provider = null)
		{
			return this.Subspace is null
				? string.Create(CultureInfo.InvariantCulture, $"{this.Data}")
				: string.Create(CultureInfo.InvariantCulture, $"{this.Subspace}:{this.Data}");
		}

		/// <inheritdoc />
		public bool TryFormat(Span<char> destination, out int charsWritten, ReadOnlySpan<char> format, IFormatProvider? provider)
		{
			return this.Subspace is null
				? destination.TryWrite(CultureInfo.InvariantCulture, $"{this.Data}", out charsWritten)
				: destination.TryWrite(CultureInfo.InvariantCulture, $"{this.Subspace}:{this.Data}", out charsWritten);
		}

	}

}
