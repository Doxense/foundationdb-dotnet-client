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
		public static FdbKey<(TSubspace Subspace, TTuple Items), DynamicSubspaceTupleEncoder<TSubspace, TTuple>> Create<TSubspace, TTuple>(TSubspace subspace, in TTuple key)
			where TSubspace : IDynamicKeySubspace
			where TTuple : IVarTuple
			=> new((subspace, key), subspace);

		public readonly struct DynamicSubspaceTupleEncoder<TSubspace, TTuple> : ISpanEncoder<(TSubspace Subspace, TTuple Items)>
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
		public static FdbKey<(TSubspace Subspace, Slice Suffix), BinarySubspaceEncoder<TSubspace>> Create<TSubspace, TTuple>(TSubspace subspace, Slice relativeKey)
			where TSubspace : IBinaryKeySubspace
			=> new((subspace, relativeKey), subspace);

		public readonly struct BinarySubspaceEncoder<TSubspace> : ISpanEncoder<(TSubspace Subspace, Slice Suffix)>
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

		public readonly struct TypedSubspaceEncoder<T1> : ISpanEncoder<(ITypedKeySubspace<T1> Subspace, T1 Key)>
		{
			/// <inheritdoc />
			public static bool TryGetSpan(scoped in (ITypedKeySubspace<T1> Subspace, T1 Key) value, out ReadOnlySpan<byte> span)
			{
				span = default;
				return false;
			}

			/// <inheritdoc />
			public static bool TryGetSizeHint(scoped in (ITypedKeySubspace<T1> Subspace, T1 Key) value, out int sizeHint)
			{
				sizeHint = 0;
				return false;
			}

			/// <inheritdoc />
			public static bool TryEncode(Span<byte> destination, out int bytesWritten, scoped in (ITypedKeySubspace<T1> Subspace, T1 Key) value)
			{
				return value.Subspace.TryEncode(destination, out bytesWritten, value.Key);
			}

		}

		public readonly struct TypedSubspaceEncoder<T1, T2> : ISpanEncoder<(ITypedKeySubspace<T1, T2> Subspace, ValueTuple<T1, T2> Key)>
		{
			/// <inheritdoc />
			public static bool TryGetSpan(scoped in (ITypedKeySubspace<T1, T2> Subspace, ValueTuple<T1, T2> Key) value, out ReadOnlySpan<byte> span)
			{
				span = default;
				return false;
			}

			/// <inheritdoc />
			public static bool TryGetSizeHint(scoped in (ITypedKeySubspace<T1, T2> Subspace, ValueTuple<T1, T2> Key) value, out int sizeHint)
			{
				sizeHint = 0;
				return false;
			}

			/// <inheritdoc />
			public static bool TryEncode(Span<byte> destination, out int bytesWritten, scoped in (ITypedKeySubspace<T1, T2> Subspace, ValueTuple<T1, T2> Key) value)
			{
				return value.Subspace.TryEncode(destination, out bytesWritten, value.Key.Item1, value.Key.Item2);
			}

		}

	}

	/// <summary>Represents a key in the database</summary>
	public interface IFdbKey : ISpanFormattable
	{

		/// <summary>Optional subspace that contains this key</summary>
		IKeySubspace? GetSubspace();

		/// <summary>Returns the already encoded binary representation of the key, if available.</summary>
		/// <param name="span">Points to the in-memory binary representation of the encoded key</param>
		/// <returns><c>true</c> if the key has already been encoded, or it its in-memory layout is the same; otherwise, <c>false</c></returns>
		/// <remarks>
		/// <para>This is only intended to support pass-through or already encoded keys (via <see cref="Slice"/>), or when the binary representation has been cached to allow multiple uses.</para>
		/// </remarks>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		bool TryGetSpan(out ReadOnlySpan<byte> span);

		/// <summary>Returns a quick estimate of the initial buffer size that would be large enough for the vast majority of values, in a single call to <see cref="TryEncode"/></summary>
		/// <param name="sizeHint">Receives an initial capacity that will satisfy almost all values. There is no guarantees that the size will be exact, and the value may be smaller or larger than the actual encoded size.</param>
		/// <returns><c>true</c> if a size could be quickly computed; otherwise, <c>false</c></returns>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		bool TryGetSizeHint(out int sizeHint);

		/// <summary>Encodes a key into its equivalent binary representation in the database</summary>
		/// <param name="destination">Destination buffer</param>
		/// <param name="bytesWritten">Number of bytes written to the buffer</param>
		/// <returns><c>true</c> if the operation was successful and the buffer was large enough, or <c>false</c> if it was too small</returns>
		[Pure, MustUseReturnValue, MethodImpl(MethodImplOptions.AggressiveInlining)]
		bool TryEncode(Span<byte> destination, out int bytesWritten);

		/// <summary>Encodes this key into <see cref="Slice"/></summary>
		/// <returns>Slice that contains the binary representation of this key</returns>
		[Pure]
		Slice ToSlice();

		/// <summary>Encodes this key into <see cref="Slice"/>, using backing buffer rented from a pool</summary>
		/// <param name="pool">Pool used to rent the buffer (<see cref="ArrayPool{T}.Shared"/> is <c>null</c>)</param>
		/// <returns><see cref="SliceOwner"/> that contains the binary representation of this key</returns>
		[Pure, MustDisposeResource]
		SliceOwner ToSlice(ArrayPool<byte>? pool);

	}

	/// <summary>Represents a typed key in the database</summary>
	/// <typeparam name="TKey">Type of the key</typeparam>
	/// <typeparam name="TEncoder">Type of the <see cref="ISpanEncoder{TValue}"/> that can convert this key into a binary representation</typeparam>
	[DebuggerDisplay("Data={Data}")]
	public readonly struct FdbKey<TKey, TEncoder>: IFdbKey
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

		/// <inheritdoc />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public IKeySubspace? GetSubspace() => this.Subspace;

		/// <inheritdoc />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool TryGetSpan(out ReadOnlySpan<byte> span) => TEncoder.TryGetSpan(this.Data, out span);

		/// <inheritdoc />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool TryGetSizeHint(out int sizeHint) => TEncoder.TryGetSizeHint(this.Data, out sizeHint);

		/// <inheritdoc />
		[MustUseReturnValue, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool TryEncode(Span<byte> destination, out int bytesWritten) => TEncoder.TryEncode(destination, out bytesWritten, in this.Data);

		/// <inheritdoc />
		[Pure]
		public Slice ToSlice()
		{
			var pool = ArrayPool<byte>.Shared;
			SpanEncoders.Encode<TEncoder, TKey>(in this.Data, pool, Fdb.MaxKeySize, out var span, out var buffer, out _);
			var slice = span.ToSlice();
			if (buffer is not null)
			{
				pool.Return(buffer);
			}
			return slice;
		}

		/// <inheritdoc />
		[Pure, MustDisposeResource]
		public SliceOwner ToSlice(ArrayPool<byte>? pool)
		{
			pool ??= ArrayPool<byte>.Shared;

			SpanEncoders.Encode<TEncoder, TKey>(in this.Data, pool, Fdb.MaxKeySize, out var span, out var buffer, out var range);

			if (buffer is null)
			{
				return SliceOwner.Copy(span, pool);
			}
			return SliceOwner.Create(buffer.AsSlice(range), pool);
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

	public readonly struct FdbTupleKey<T1> : IFdbKey
	{

		[SkipLocalsInit]
		public FdbTupleKey(IDynamicKeySubspace subspace, T1 item1)
		{
			this.Subspace = subspace;
			this.Item1 = item1;
		}

		public readonly IDynamicKeySubspace Subspace;

		public readonly T1 Item1;

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public FdbTupleKey<T1, T2> Append<T2>(T2 item2) => new(Subspace, this.Item1, item2);

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public FdbTupleKey<T1, T2, T3> Append<T2, T3>(T2 item2, T3 item3) => new(this.Subspace, STuple.Create(this.Item1, item2, item3));

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public FdbTupleKey<T1, T2, T3, T4, T5> Append<T2, T3, T4, T5>(T2 item2, T3 item3, T4 item4, T5 item5) => new(this.Subspace, STuple.Create(this.Item1, item2, item3, item4, item5));

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public FdbTupleKey<T1, T2, T3, T4, T5, T6> Append<T2, T3, T4, T5, T6>(T2 item2, T3 item3, T4 item4, T5 item5, T6 item6) => new(this.Subspace, STuple.Create(this.Item1, item2, item3, item4, item5, item6));

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public FdbTupleKey<T1, T2, T3, T4, T5, T6, T7> Append<T2, T3, T4, T5, T6, T7>(T2 item2, T3 item3, T4 item4, T5 item5, T6 item6, T7 item7) => new(this.Subspace, STuple.Create(this.Item1, item2, item3, item4, item5, item6, item7));

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public FdbTupleKey<T1, T2, T3, T4, T5, T6, T7, T8> Append<T2, T3, T4, T5, T6, T7, T8>(T2 item2, T3 item3, T4 item4, T5 item5, T6 item6, T7 item7, T8 item8) => new(this.Subspace, STuple.Create(this.Item1, item2, item3, item4, item5, item6, item7, item8));

		/// <inheritdoc />
		public string ToString(string? format, IFormatProvider? formatProvider = null) => $"[{this.Subspace}] {STuple.Create(this.Item1)}";

		/// <inheritdoc />
		public bool TryFormat(Span<char> destination, out int charsWritten, ReadOnlySpan<char> format, IFormatProvider? provider) => destination.TryWrite($"[{this.Subspace}] {STuple.Create(this.Item1)}", out charsWritten);

		/// <inheritdoc />
		public override string ToString() => ToString(null, null);

		/// <inheritdoc />
		IKeySubspace? IFdbKey.GetSubspace() => this.Subspace;

		/// <inheritdoc />
		public bool TryGetSpan(out ReadOnlySpan<byte> span) { span = default; return false; }

		/// <inheritdoc />
		public bool TryGetSizeHint(out int sizeHint) { sizeHint = 0; return false; }

		/// <inheritdoc />
		public bool TryEncode(Span<byte> destination, out int bytesWritten) => TupleEncoder.TryEncodeKey(destination, out bytesWritten, this.Subspace.GetPrefix().Span, this.Item1);

		/// <inheritdoc />
		public Slice ToSlice() => TupleEncoder.EncodeKey(this.Subspace.GetPrefix().Span, this.Item1);

		/// <inheritdoc />
		public SliceOwner ToSlice(ArrayPool<byte>? pool) => TupleEncoder.Pack(pool ?? ArrayPool<byte>.Shared, this.Subspace.GetPrefix().Span, STuple.Create(this.Item1));

	}

	public readonly struct FdbTupleKey<T1, T2> : IFdbKey
	{

		[SkipLocalsInit, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public FdbTupleKey(IDynamicKeySubspace subspace, in STuple<T1, T2> items)
		{
			this.Subspace = subspace;
			this.Items = items;
		}

		[SkipLocalsInit, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public FdbTupleKey(IDynamicKeySubspace subspace, in ValueTuple<T1, T2> items)
		{
			this.Subspace = subspace;
			this.Items = items.ToSTuple();
		}

		[SkipLocalsInit, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public FdbTupleKey(IDynamicKeySubspace subspace, T1 item1, T2 item2)
		{
			this.Subspace = subspace;
			this.Items = STuple.Create(item1, item2);
		}

		public readonly IDynamicKeySubspace Subspace;

		public readonly STuple<T1, T2> Items;

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public FdbTupleKey<T1, T2, T3> Append<T3>(T3 item3) => new(this.Subspace, STuple.Create(this.Items.Item1, this.Items.Item2, item3));

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public FdbTupleKey<T1, T2, T3, T4, T5> Append<T3, T4, T5>(T3 item3, T4 item4, T5 item5) => new(this.Subspace, STuple.Create(this.Items.Item1, this.Items.Item2, item3, item4, item5));

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public FdbTupleKey<T1, T2, T3, T4, T5, T6> Append<T3, T4, T5, T6>(T3 item3, T4 item4, T5 item5, T6 item6) => new(this.Subspace, STuple.Create(this.Items.Item1, this.Items.Item2, item3, item4, item5, item6));

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public FdbTupleKey<T1, T2, T3, T4, T5, T6, T7> Append<T3, T4, T5, T6, T7>(T3 item3, T4 item4, T5 item5, T6 item6, T7 item7) => new(this.Subspace, STuple.Create(this.Items.Item1, this.Items.Item2, item3, item4, item5, item6, item7));

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public FdbTupleKey<T1, T2, T3, T4, T5, T6, T7, T8> Append<T3, T4, T5, T6, T7, T8>(T3 item3, T4 item4, T5 item5, T6 item6, T7 item7, T8 item8) => new(this.Subspace, STuple.Create(this.Items.Item1, this.Items.Item2, item3, item4, item5, item6, item7, item8));

		/// <inheritdoc />
		public string ToString(string? format, IFormatProvider? formatProvider) => $"[{this.Subspace}] {this.Items}";

		/// <inheritdoc />
		public bool TryFormat(Span<char> destination, out int charsWritten, ReadOnlySpan<char> format, IFormatProvider? provider) => destination.TryWrite($"[{this.Subspace}] {this.Items}", out charsWritten);

		/// <inheritdoc />
		public override string ToString() => ToString(null, null);

		/// <inheritdoc />
		IKeySubspace? IFdbKey.GetSubspace() => this.Subspace;

		/// <inheritdoc />
		public bool TryGetSpan(out ReadOnlySpan<byte> span) { span = default; return false; }

		/// <inheritdoc />
		public bool TryGetSizeHint(out int sizeHint) { sizeHint = 0; return false; }

		/// <inheritdoc />
		public bool TryEncode(Span<byte> destination, out int bytesWritten) => TupleEncoder.TryPackTo(destination, out bytesWritten, this.Subspace.GetPrefix().Span, in this.Items);

		/// <inheritdoc />
		public Slice ToSlice() => TupleEncoder.Pack(this.Subspace.GetPrefix().Span, in this.Items);

		/// <inheritdoc />
		public SliceOwner ToSlice(ArrayPool<byte>? pool) => TupleEncoder.Pack(pool ?? ArrayPool<byte>.Shared, this.Subspace.GetPrefix().Span, in this.Items);

	}

	public readonly struct FdbTupleKey<T1, T2, T3> : IFdbKey
	{

		[SkipLocalsInit, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public FdbTupleKey(IDynamicKeySubspace subspace, in STuple<T1, T2, T3> items)
		{
			this.Subspace = subspace;
			this.Items = items;
		}

		[SkipLocalsInit, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public FdbTupleKey(IDynamicKeySubspace subspace, in ValueTuple<T1, T2, T3> items)
		{
			this.Subspace = subspace;
			this.Items = items.ToSTuple();
		}

		[SkipLocalsInit, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public FdbTupleKey(IDynamicKeySubspace subspace, T1 item1, T2 item2, T3 item3)
		{
			this.Subspace = subspace;
			this.Items = STuple.Create(item1, item2, item3);
		}

		public readonly IDynamicKeySubspace Subspace;

		public readonly STuple<T1, T2, T3> Items;

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public FdbTupleKey<T1, T2, T3, T4> Append<T4>(T4 item4) => new(this.Subspace, STuple.Create(this.Items.Item1, this.Items.Item2, this.Items.Item3, item4));

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public FdbTupleKey<T1, T2, T3, T4, T5> Append<T4, T5>(T4 item4, T5 item5) => new(this.Subspace, STuple.Create(this.Items.Item1, this.Items.Item2, this.Items.Item3, item4, item5));

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public FdbTupleKey<T1, T2, T3, T4, T5, T6> Append<T4, T5, T6>(T4 item4, T5 item5, T6 item6) => new(this.Subspace, STuple.Create(this.Items.Item1, this.Items.Item2, this.Items.Item3, item4, item5, item6));

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public FdbTupleKey<T1, T2, T3, T4, T5, T6, T7> Append<T4, T5, T6, T7>(T4 item4, T5 item5, T6 item6, T7 item7) => new(this.Subspace, STuple.Create(this.Items.Item1, this.Items.Item2, this.Items.Item3, item4, item5, item6, item7));

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public FdbTupleKey<T1, T2, T3, T4, T5, T6, T7, T8> Append<T4, T5, T6, T7, T8>(T4 item4, T5 item5, T6 item6, T7 item7, T8 item8) => new(this.Subspace, STuple.Create(this.Items.Item1, this.Items.Item2, this.Items.Item3, item4, item5, item6, item7, item8));

		/// <inheritdoc />
		public string ToString(string? format, IFormatProvider? formatProvider) => $"[{this.Subspace}] {this.Items}";

		/// <inheritdoc />
		public bool TryFormat(Span<char> destination, out int charsWritten, ReadOnlySpan<char> format, IFormatProvider? provider) => destination.TryWrite($"[{this.Subspace}] {this.Items}", out charsWritten);

		/// <inheritdoc />
		public override string ToString() => ToString(null, null);

		/// <inheritdoc />
		IKeySubspace? IFdbKey.GetSubspace() => this.Subspace;

		/// <inheritdoc />
		public bool TryGetSpan(out ReadOnlySpan<byte> span) { span = default; return false; }

		/// <inheritdoc />
		public bool TryGetSizeHint(out int sizeHint) { sizeHint = 0; return false; }

		/// <inheritdoc />
		public bool TryEncode(Span<byte> destination, out int bytesWritten) => TupleEncoder.TryPackTo(destination, out bytesWritten, this.Subspace.GetPrefix().Span, in this.Items);

		/// <inheritdoc />
		public Slice ToSlice() => TupleEncoder.Pack(this.Subspace.GetPrefix().Span, in this.Items);

		/// <inheritdoc />
		public SliceOwner ToSlice(ArrayPool<byte>? pool) => TupleEncoder.Pack(pool ?? ArrayPool<byte>.Shared, this.Subspace.GetPrefix().Span, in this.Items);

	}

	public readonly struct FdbTupleKey<T1, T2, T3, T4> : IFdbKey
	{

		[SkipLocalsInit, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public FdbTupleKey(IDynamicKeySubspace subspace, in STuple<T1, T2, T3, T4> items)
		{
			this.Subspace = subspace;
			this.Items = items;
		}

		[SkipLocalsInit, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public FdbTupleKey(IDynamicKeySubspace subspace, in ValueTuple<T1, T2, T3, T4> items)
		{
			this.Subspace = subspace;
			this.Items = items.ToSTuple();
		}

		[SkipLocalsInit, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public FdbTupleKey(IDynamicKeySubspace subspace, T1 item1, T2 item2, T3 item3, T4 item4)
		{
			this.Subspace = subspace;
			this.Items = STuple.Create(item1, item2, item3, item4);
		}

		public readonly IDynamicKeySubspace Subspace;

		public readonly STuple<T1, T2, T3, T4> Items;

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public FdbTupleKey<T1, T2, T3, T4, T5> Append<T5>(T5 item5) => new(this.Subspace, STuple.Create(this.Items.Item1, this.Items.Item2, this.Items.Item3, this.Items.Item4, item5));

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public FdbTupleKey<T1, T2, T3, T4, T5, T6> Append<T5, T6>(T5 item5, T6 item6) => new(this.Subspace, STuple.Create(this.Items.Item1, this.Items.Item2, this.Items.Item3, this.Items.Item4, item5, item6));

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public FdbTupleKey<T1, T2, T3, T4, T5, T6, T7> Append<T5, T6, T7>(T5 item5, T6 item6, T7 item7) => new(this.Subspace, STuple.Create(this.Items.Item1, this.Items.Item2, this.Items.Item3, this.Items.Item4, item5, item6, item7));

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public FdbTupleKey<T1, T2, T3, T4, T5, T6, T7, T8> Append<T5, T6, T7, T8>(T5 item5, T6 item6, T7 item7, T8 item8) => new(this.Subspace, STuple.Create(this.Items.Item1, this.Items.Item2, this.Items.Item3, this.Items.Item4, item5, item6, item7, item8));

		/// <inheritdoc />
		public string ToString(string? format, IFormatProvider? formatProvider) => $"[{this.Subspace}] {this.Items}";

		/// <inheritdoc />
		public bool TryFormat(Span<char> destination, out int charsWritten, ReadOnlySpan<char> format, IFormatProvider? provider) => destination.TryWrite($"[{this.Subspace}] {this.Items}", out charsWritten);

		/// <inheritdoc />
		public override string ToString() => ToString(null, null);

		/// <inheritdoc />
		IKeySubspace? IFdbKey.GetSubspace() => this.Subspace;

		/// <inheritdoc />
		public bool TryGetSpan(out ReadOnlySpan<byte> span) { span = default; return false; }

		/// <inheritdoc />
		public bool TryGetSizeHint(out int sizeHint) { sizeHint = 0; return false; }

		/// <inheritdoc />
		public bool TryEncode(Span<byte> destination, out int bytesWritten) => TupleEncoder.TryPackTo(destination, out bytesWritten, this.Subspace.GetPrefix().Span, in this.Items);

		/// <inheritdoc />
		public Slice ToSlice() => TupleEncoder.Pack(this.Subspace.GetPrefix().Span, in this.Items);

		/// <inheritdoc />
		public SliceOwner ToSlice(ArrayPool<byte>? pool) => TupleEncoder.Pack(pool ?? ArrayPool<byte>.Shared, this.Subspace.GetPrefix().Span, in this.Items);

	}

	public readonly struct FdbTupleKey<T1, T2, T3, T4, T5> : IFdbKey
	{

		[SkipLocalsInit, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public FdbTupleKey(IDynamicKeySubspace subspace, in STuple<T1, T2, T3, T4, T5> items)
		{
			this.Subspace = subspace;
			this.Items = items;
		}

		[SkipLocalsInit, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public FdbTupleKey(IDynamicKeySubspace subspace, in ValueTuple<T1, T2, T3, T4, T5> items)
		{
			this.Subspace = subspace;
			this.Items = items.ToSTuple();
		}

		[SkipLocalsInit, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public FdbTupleKey(IDynamicKeySubspace subspace, T1 item1, T2 item2, T3 item3, T4 item4, T5 item5)
		{
			this.Subspace = subspace;
			this.Items = STuple.Create(item1, item2, item3, item4, item5);
		}

		public readonly IDynamicKeySubspace Subspace;

		public readonly STuple<T1, T2, T3, T4, T5> Items;

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public FdbTupleKey<T1, T2, T3, T4, T5, T6> Append<T6>(T6 item6) => new(this.Subspace, STuple.Create(this.Items.Item1, this.Items.Item2, this.Items.Item3, this.Items.Item4, this.Items.Item5, item6));

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public FdbTupleKey<T1, T2, T3, T4, T5, T6, T7> Append<T6, T7>(T6 item6, T7 item7) => new(this.Subspace, STuple.Create(this.Items.Item1, this.Items.Item2, this.Items.Item3, this.Items.Item4, this.Items.Item5, item6, item7));

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public FdbTupleKey<T1, T2, T3, T4, T5, T6, T7, T8> Append<T6, T7, T8>(T6 item6, T7 item7, T8 item8) => new(this.Subspace, STuple.Create(this.Items.Item1, this.Items.Item2, this.Items.Item3, this.Items.Item4, this.Items.Item5, item6, item7, item8));

		/// <inheritdoc />
		public string ToString(string? format, IFormatProvider? formatProvider) => $"[{this.Subspace}] {this.Items}";

		/// <inheritdoc />
		public bool TryFormat(Span<char> destination, out int charsWritten, ReadOnlySpan<char> format, IFormatProvider? provider) => destination.TryWrite($"[{this.Subspace}] {this.Items}", out charsWritten);

		/// <inheritdoc />
		public override string ToString() => ToString(null, null);

		/// <inheritdoc />
		IKeySubspace? IFdbKey.GetSubspace() => this.Subspace;

		/// <inheritdoc />
		public bool TryGetSpan(out ReadOnlySpan<byte> span) { span = default; return false; }

		/// <inheritdoc />
		public bool TryGetSizeHint(out int sizeHint) { sizeHint = 0; return false; }

		/// <inheritdoc />
		public bool TryEncode(Span<byte> destination, out int bytesWritten) => TupleEncoder.TryPackTo(destination, out bytesWritten, this.Subspace.GetPrefix().Span, in this.Items);

		/// <inheritdoc />
		public Slice ToSlice() => TupleEncoder.Pack(this.Subspace.GetPrefix().Span, in this.Items);

		/// <inheritdoc />
		public SliceOwner ToSlice(ArrayPool<byte>? pool) => TupleEncoder.Pack(pool ?? ArrayPool<byte>.Shared, this.Subspace.GetPrefix().Span, in this.Items);

	}

	public readonly struct FdbTupleKey<T1, T2, T3, T4, T5, T6> : IFdbKey
	{

		[SkipLocalsInit, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public FdbTupleKey(IDynamicKeySubspace subspace, in STuple<T1, T2, T3, T4, T5, T6> items)
		{
			this.Subspace = subspace;
			this.Items = items;
		}

		[SkipLocalsInit, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public FdbTupleKey(IDynamicKeySubspace subspace, in ValueTuple<T1, T2, T3, T4, T5, T6> items)
		{
			this.Subspace = subspace;
			this.Items = items.ToSTuple();
		}

		[SkipLocalsInit, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public FdbTupleKey(IDynamicKeySubspace subspace, T1 item1, T2 item2, T3 item3, T4 item4, T5 item5, T6 item6)
		{
			this.Subspace = subspace;
			this.Items = STuple.Create(item1, item2, item3, item4, item5, item6);
		}

		public readonly IDynamicKeySubspace Subspace;

		public readonly STuple<T1, T2, T3, T4, T5, T6> Items;

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public FdbTupleKey<T1, T2, T3, T4, T5, T6, T7> Append<T7>(T7 item7) => new(this.Subspace, this.Items.Item1, this.Items.Item2, this.Items.Item3, this.Items.Item4, this.Items.Item5, this.Items.Item6, item7);

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public FdbTupleKey<T1, T2, T3, T4, T5, T6, T7, T8> Append<T7, T8>(T7 item7, T8 item8) => new(this.Subspace, this.Items.Item1, this.Items.Item2, this.Items.Item3, this.Items.Item4, this.Items.Item5, this.Items.Item6, item7, item8);

		/// <inheritdoc />
		public string ToString(string? format, IFormatProvider? formatProvider) => $"[{this.Subspace}] {this.Items}";

		/// <inheritdoc />
		public bool TryFormat(Span<char> destination, out int charsWritten, ReadOnlySpan<char> format, IFormatProvider? provider) => destination.TryWrite($"[{this.Subspace}] {this.Items}", out charsWritten);

		/// <inheritdoc />
		public override string ToString() => ToString(null, null);

		/// <inheritdoc />
		IKeySubspace? IFdbKey.GetSubspace() => this.Subspace;

		/// <inheritdoc />
		public bool TryGetSpan(out ReadOnlySpan<byte> span) { span = default; return false; }

		/// <inheritdoc />
		public bool TryGetSizeHint(out int sizeHint) { sizeHint = 0; return false; }

		/// <inheritdoc />
		public bool TryEncode(Span<byte> destination, out int bytesWritten) => TupleEncoder.TryPackTo(destination, out bytesWritten, this.Subspace.GetPrefix().Span, in this.Items);

		/// <inheritdoc />
		public Slice ToSlice() => TupleEncoder.Pack(this.Subspace.GetPrefix().Span, in this.Items);

		/// <inheritdoc />
		public SliceOwner ToSlice(ArrayPool<byte>? pool) => TupleEncoder.Pack(pool ?? ArrayPool<byte>.Shared, this.Subspace.GetPrefix().Span, in this.Items);

	}

	public readonly struct FdbTupleKey<T1, T2, T3, T4, T5, T6, T7> : IFdbKey
	{

		[SkipLocalsInit, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public FdbTupleKey(IDynamicKeySubspace subspace, in STuple<T1, T2, T3, T4, T5, T6, T7> items)
		{
			this.Subspace = subspace;
			this.Items = items;
		}

		[SkipLocalsInit, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public FdbTupleKey(IDynamicKeySubspace subspace, in ValueTuple<T1, T2, T3, T4, T5, T6, T7> items)
		{
			this.Subspace = subspace;
			this.Items = items.ToSTuple();
		}

		[SkipLocalsInit, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public FdbTupleKey(IDynamicKeySubspace subspace, T1 item1, T2 item2, T3 item3, T4 item4, T5 item5, T6 item6, T7 item7)
		{
			this.Subspace = subspace;
			this.Items = STuple.Create(item1, item2, item3, item4, item5, item6, item7);
		}

		public readonly IDynamicKeySubspace Subspace;

		public readonly STuple<T1, T2, T3, T4, T5, T6, T7> Items;

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public FdbTupleKey<T1, T2, T3, T4, T5, T6, T7, T8> Append<T8>(T8 item8) => new(this.Subspace, this.Items.Item1, this.Items.Item2, this.Items.Item3, this.Items.Item4, this.Items.Item5, this.Items.Item6, this.Items.Item7, item8);

		/// <inheritdoc />
		public string ToString(string? format, IFormatProvider? formatProvider) => $"[{this.Subspace}] {this.Items}";

		/// <inheritdoc />
		public bool TryFormat(Span<char> destination, out int charsWritten, ReadOnlySpan<char> format, IFormatProvider? provider) => destination.TryWrite($"[{this.Subspace}] {this.Items}", out charsWritten);

		/// <inheritdoc />
		public override string ToString() => ToString(null, null);

		/// <inheritdoc />
		IKeySubspace? IFdbKey.GetSubspace() => this.Subspace;

		/// <inheritdoc />
		public bool TryGetSpan(out ReadOnlySpan<byte> span) { span = default; return false; }

		/// <inheritdoc />
		public bool TryGetSizeHint(out int sizeHint) { sizeHint = 0; return false; }

		/// <inheritdoc />
		public bool TryEncode(Span<byte> destination, out int bytesWritten) => TupleEncoder.TryPackTo(destination, out bytesWritten, this.Subspace.GetPrefix().Span, in this.Items);

		/// <inheritdoc />
		public Slice ToSlice() => TupleEncoder.Pack(this.Subspace.GetPrefix().Span, in this.Items);

		/// <inheritdoc />
		public SliceOwner ToSlice(ArrayPool<byte>? pool) => TupleEncoder.Pack(pool ?? ArrayPool<byte>.Shared, this.Subspace.GetPrefix().Span, in this.Items);

	}

	public readonly struct FdbTupleKey<T1, T2, T3, T4, T5, T6, T7, T8> : IFdbKey
	{

		[SkipLocalsInit, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public FdbTupleKey(IDynamicKeySubspace subspace, in STuple<T1, T2, T3, T4, T5, T6, T7, T8> items)
		{
			this.Subspace = subspace;
			this.Items = items;
		}

		[SkipLocalsInit, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public FdbTupleKey(IDynamicKeySubspace subspace, in ValueTuple<T1, T2, T3, T4, T5, T6, T7, ValueTuple<T8>> items)
		{
			this.Subspace = subspace;
			this.Items = items.ToSTuple();
		}

		[SkipLocalsInit, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public FdbTupleKey(IDynamicKeySubspace subspace, T1 item1, T2 item2, T3 item3, T4 item4, T5 item5, T6 item6, T7 item7, T8 item8)
		{
			this.Subspace = subspace;
			this.Items = STuple.Create(item1, item2, item3, item4, item5, item6, item7, item8);
		}

		public readonly IDynamicKeySubspace Subspace;

		public readonly STuple<T1, T2, T3, T4, T5, T6, T7, T8> Items;

		/// <inheritdoc />
		public string ToString(string? format, IFormatProvider? formatProvider) => $"[{this.Subspace}] {this.Items}";

		/// <inheritdoc />
		public bool TryFormat(Span<char> destination, out int charsWritten, ReadOnlySpan<char> format, IFormatProvider? provider) => destination.TryWrite($"[{this.Subspace}] {this.Items}", out charsWritten);

		/// <inheritdoc />
		public override string ToString() => ToString(null, null);

		/// <inheritdoc />
		IKeySubspace? IFdbKey.GetSubspace() => this.Subspace;

		/// <inheritdoc />
		public bool TryGetSpan(out ReadOnlySpan<byte> span) { span = default; return false; }

		/// <inheritdoc />
		public bool TryGetSizeHint(out int sizeHint) { sizeHint = 0; return false; }

		/// <inheritdoc />
		public bool TryEncode(Span<byte> destination, out int bytesWritten) => TupleEncoder.TryPackTo(destination, out bytesWritten, this.Subspace.GetPrefix().Span, in this.Items);

		/// <inheritdoc />
		public Slice ToSlice() => TupleEncoder.Pack(this.Subspace.GetPrefix().Span, in this.Items);

		/// <inheritdoc />
		public SliceOwner ToSlice(ArrayPool<byte>? pool) => TupleEncoder.Pack(pool ?? ArrayPool<byte>.Shared, this.Subspace.GetPrefix().Span, in this.Items);

	}

}
