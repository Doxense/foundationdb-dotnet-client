#region Copyright (c) 2023-2025 SnowBank SAS
//
// All rights are reserved. Reproduction or transmission in whole or in part, in
// any form or by any means, electronic, mechanical or otherwise, is prohibited
// without the prior written consent of the copyright owner.
//
#endregion

namespace FoundationDB.Client
{

	public static class FdbValue
	{

		public const int MaxSize = Fdb.MaxValueSize;

		public static readonly FdbRawValue Empty = new(Slice.Empty);

		public static class Binary
		{

			/// <summary>Returns a value that wraps a <see cref="Slice"/></summary>
			[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
			public static FdbRawValue FromBytes(Slice value) => new(value);

			/// <summary>Returns a value that wraps a byte array</summary>
			[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
			public static FdbRawValue FromBytes(byte[] value) => new(Slice.FromBytes(value));

			/// <summary>Returns a value that wraps a span of bytes</summary>
			public static FdbSpanValue<byte> FromBytes(ReadOnlySpan<byte> value)
				=> new(value);

			/// <summary>Returns a value that wraps the content of a <see cref="MemoryStream"/></summary>
			/// <remarks>The stream will be written from the start, and NOT the current position.</remarks>
			[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
			public static FdbValue<MemoryStream, SpanEncoders.RawEncoder> FromStream(MemoryStream value) => new(value);

		}

		public static class Tuples
		{

			/// <summary>Returns a value that wraps a tuple</summary>
			[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
			public static FdbValue<TTuple, SpanEncoders.TupleEncoder<TTuple>> Pack<TTuple>(in TTuple value)
				where TTuple : IVarTuple
				=> new(in value);

			/// <summary>Returns a value that wraps a tuple</summary>
			[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
			public static FdbTupleValue<T1> Key<T1>(T1 item1) => new(item1);

			/// <summary>Returns a value that wraps a tuple</summary>
			[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
			public static FdbTupleValue<T1, T2> Key<T1, T2>(T1 item1, T2 item2) => new(item1, item2);

			/// <summary>Returns a value that wraps a tuple</summary>
			[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
			public static FdbTupleValue<T1, T2, T3> Key<T1, T2, T3>(T1 item1, T2 item2, T3 item3) => new(item1, item2, item3);

			/// <summary>Returns a value that wraps a tuple</summary>
			[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
			public static FdbTupleValue<T1, T2, T3, T4> Key<T1, T2, T3, T4>(T1 item1, T2 item2, T3 item3, T4 item4) => new(item1, item2, item3, item4);

			/// <summary>Returns a value that wraps a tuple</summary>
			[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
			public static FdbTupleValue<T1, T2, T3, T4, T5> Key<T1, T2, T3, T4, T5>(T1 item1, T2 item2, T3 item3, T4 item4, T5 item5) => new(item1, item2, item3, item4, item5);

			/// <summary>Returns a value that wraps a tuple</summary>
			[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
			public static FdbTupleValue<T1, T2, T3, T4, T5, T6> Key<T1, T2, T3, T4, T5, T6>(T1 item1, T2 item2, T3 item3, T4 item4, T5 item5, T6 item6) => new(item1, item2, item3, item4, item5, item6);

			/// <summary>Returns a value that wraps a tuple</summary>
			[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
			public static FdbTupleValue<T1, T2, T3, T4, T5, T6, T7> Key<T1, T2, T3, T4, T5, T6, T7>(T1 item1, T2 item2, T3 item3, T4 item4, T5 item5, T6 item6, T7 item7) => new(item1, item2, item3, item4, item5, item6, item7);

			/// <summary>Returns a value that wraps a tuple</summary>
			[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
			public static FdbTupleValue<T1, T2, T3, T4, T5, T6, T7, T8> Key<T1, T2, T3, T4, T5, T6, T7, T8>(T1 item1, T2 item2, T3 item3, T4 item4, T5 item5, T6 item6, T7 item7, T8 item8) => new(item1, item2, item3, item4, item5, item6, item7, item8);

		}

		public static class Text
		{

			public static FdbValue<string, SpanEncoders.Utf8Encoder> FromUtf8(string? value) => new(value ?? "");

			public static FdbValue<ReadOnlyMemory<char>, SpanEncoders.Utf8Encoder> FromUtf8(ReadOnlyMemory<char> value) => new(value);

			public static FdbSpanValue<char> FromUtf8(ReadOnlySpan<char> value) => new(value);

		}

		public static class FixedSize
		{

			public static class LittleEndian
			{
				/// <summary>Returns a value that wraps a fixed-size signed 32-bit integer, encoded as little-endian</summary>
				[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
				public static FdbValue<int, SpanEncoders.FixedSizeLittleEndianEncoder> FromInt32(int value) => new(value);

				/// <summary>Returns a value that wraps a fixed-size unsigned 32-bit integer, encoded as little-endian</summary>
				[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
				public static FdbValue<uint, SpanEncoders.FixedSizeLittleEndianEncoder> FromUInt32(uint value) => new(value);

				/// <summary>Returns a value that wraps a fixed-size signed 64-bit integer, encoded as little-endian</summary>
				[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
				public static FdbValue<long, SpanEncoders.FixedSizeLittleEndianEncoder> FromInt64(long value) => new(value);

				/// <summary>Returns a value that wraps a fixed-size unsigned 64-bit integer, encoded as little-endian</summary>
				[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
				public static FdbValue<ulong, SpanEncoders.FixedSizeLittleEndianEncoder> FromUInt64(ulong value) => new(value);

			}

			public static class BigEndian
			{
				/// <summary>Returns a value that wraps a fixed-size signed 32-bit integer, encoded as big-endian</summary>
				[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
				public static FdbValue<int, SpanEncoders.FixedSizeBigEndianEncoder> FromInt32(int value) => new(value);

				/// <summary>Returns a value that wraps a fixed-size unsigned 32-bit integer, encoded as big-endian</summary>
				[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
				public static FdbValue<uint, SpanEncoders.FixedSizeBigEndianEncoder> FromUInt32(uint value) => new(value);

				/// <summary>Returns a value that wraps a fixed-size signed 64-bit integer, encoded as big-endian</summary>
				[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
				public static FdbValue<long, SpanEncoders.FixedSizeBigEndianEncoder> FromInt64(long value) => new(value);

				/// <summary>Returns a value that wraps a fixed-size unsigned 64-bit integer, encoded as big-endian</summary>
				[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
				public static FdbValue<ulong, SpanEncoders.FixedSizeBigEndianEncoder> FromUInt64(ulong value) => new(value);

			}

		}

		public static class Compact
		{

			public static class LittleEndian
			{
				/// <summary>Returns a key that wraps a byte array</summary>
				[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
				public static FdbValue<int, SpanEncoders.CompactLittleEndianEncoder> FromInt32(int value) => new(value);

				/// <summary>Returns a key that wraps a byte array</summary>
				[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
				public static FdbValue<uint, SpanEncoders.CompactLittleEndianEncoder> FromUInt32(uint value) => new(value);

				/// <summary>Returns a key that wraps a byte array</summary>
				[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
				public static FdbValue<long, SpanEncoders.CompactLittleEndianEncoder> FromInt64(long value) => new(value);

				/// <summary>Returns a key that wraps a byte array</summary>
				[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
				public static FdbValue<ulong, SpanEncoders.CompactLittleEndianEncoder> FromUInt64(ulong value) => new(value);

			}

			public static class BigEndian
			{
				/// <summary>Returns a key that wraps a byte array</summary>
				[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
				public static FdbValue<int, SpanEncoders.CompactBigEndianEncoder> FromInt32(int value) => new(value);

				/// <summary>Returns a key that wraps a byte array</summary>
				[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
				public static FdbValue<uint, SpanEncoders.CompactBigEndianEncoder> FromUInt32(uint value) => new(value);

				/// <summary>Returns a key that wraps a byte array</summary>
				[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
				public static FdbValue<long, SpanEncoders.CompactBigEndianEncoder> FromInt64(long value) => new(value);

				/// <summary>Returns a key that wraps a byte array</summary>
				[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
				public static FdbValue<ulong, SpanEncoders.CompactBigEndianEncoder> FromUInt64(ulong value) => new(value);

			}

		}

		public static class Uuids
		{

			public static FdbValue<Guid, SpanEncoders.FixedSizeUuidEncoder> FromUuid128(Guid value) => new(value);

			public static FdbValue<Uuid128, SpanEncoders.FixedSizeUuidEncoder> FromUuid128(Uuid128 value) => new(value);

			public static FdbValue<VersionStamp, SpanEncoders.FixedSizeUuidEncoder> FromVersionStamp(VersionStamp value) => new(value);

		}

	}

	public static class FdbValueExtensions
	{

		/// <summary>Returns a pre-encoded version of a key</summary>
		/// <typeparam name="TKey">Type of the key to pre-encode</typeparam>
		/// <param name="key">Key to pre-encoded</param>
		/// <returns>Key with a cached version of the encoded original</returns>
		/// <remarks>This key can be used multiple times without re-encoding the original</remarks>
		[Pure]
		public static FdbRawValue Memoize<TKey>(this TKey key)
			where TKey : struct, IFdbKey
		{
			if (typeof(TKey) == typeof(FdbRawValue))
			{ // already cached!
				return (FdbRawValue) (object) key;
			}

			return new(key.ToSlice());
		}

	}

	public interface IFdbValue : ISpanFormattable
	{

		/// <summary>Returns the already encoded binary representation of the value, if available.</summary>
		/// <param name="span">Points to the in-memory binary representation of the encoded value</param>
		/// <returns><c>true</c> if the value has already been encoded, or it its in-memory layout is the same; otherwise, <c>false</c></returns>
		/// <remarks>
		/// <para>This is only intended to support pass-through or already encoded values (via <see cref="Slice"/>), or when the binary representation has been cached to allow multiple uses.</para>
		/// </remarks>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		bool TryGetSpan(out ReadOnlySpan<byte> span);

		/// <summary>Returns a quick estimate of the initial buffer size that would be large enough for the vast majority of values, in a single call to <see cref="TryEncode"/></summary>
		/// <param name="sizeHint">Receives an initial capacity that will satisfy almost all values. There is no guarantees that the size will be exact, and the value may be smaller or larger than the actual encoded size.</param>
		/// <returns><c>true</c> if a size could be quickly computed; otherwise, <c>false</c></returns>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		bool TryGetSizeHint(out int sizeHint);

		/// <summary>Encodes a value into its equivalent binary representation in the database</summary>
		/// <param name="destination">Destination buffer</param>
		/// <param name="bytesWritten">Number of bytes written to the buffer</param>
		/// <returns><c>true</c> if the operation was successful and the buffer was large enough, or <c>false</c> if it was too small</returns>
		[Pure, MustUseReturnValue, MethodImpl(MethodImplOptions.AggressiveInlining)]
		bool TryEncode(Span<byte> destination, out int bytesWritten);

		/// <summary>Encodes this value into <see cref="Slice"/></summary>
		/// <returns>Slice that contains the binary representation of this value</returns>
		[Pure]
		Slice ToSlice();

		/// <summary>Encodes this value into <see cref="Slice"/>, using backing buffer rented from a pool</summary>
		/// <param name="pool">Pool used to rent the buffer (<see cref="ArrayPool{T}.Shared"/> is <c>null</c>)</param>
		/// <returns><see cref="SliceOwner"/> that contains the binary representation of this key</returns>
		[Pure, MustDisposeResource]
		SliceOwner ToSlice(ArrayPool<byte>? pool);

	}

	public readonly struct FdbRawValue : IFdbValue
	{

		public static readonly FdbRawValue Nil;

		public static readonly FdbRawValue Empty = new(Slice.Empty);

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static FdbRawValue Return(Slice slice) => new(slice);

		internal FdbRawValue(Slice data)
		{
			this.Data = data;
		}

		public readonly Slice Data;

		/// <inheritdoc />
		public override string ToString() => this.Data.ToString();

		/// <inheritdoc />
		public string ToString(string? format, IFormatProvider? formatProvider)
			=> this.Data.ToString(format, formatProvider);

		/// <inheritdoc />
		public bool TryFormat(Span<char> destination, out int charsWritten, ReadOnlySpan<char> format, IFormatProvider? provider)
			=> this.Data.TryFormat(destination, out charsWritten, format, provider);

		/// <inheritdoc />
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool TryGetSpan(out ReadOnlySpan<byte> span)
		{
			span = this.Data.Span;
			return true;
		}

		/// <inheritdoc />
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool TryGetSizeHint(out int sizeHint)
		{
			sizeHint = this.Data.Count;
			return true;
		}

		/// <inheritdoc />
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool TryEncode(Span<byte> destination, out int bytesWritten)
		{
			return this.Data.TryCopyTo(destination, out bytesWritten);
		}

		/// <inheritdoc />
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public Slice ToSlice()
		{
			return this.Data;
		}

		/// <inheritdoc />
		public SliceOwner ToSlice(ArrayPool<byte>? pool)
		{
			return SliceOwner.Wrap(this.Data);
		}

	}

	[DebuggerDisplay("Data={Data}")]
	public readonly struct FdbValue<TValue, TEncoder> : IFdbValue
		where TEncoder: struct, ISpanEncoder<TValue>
	{

		public FdbValue(TValue data)
		{
			this.Data = data;
		}

		public FdbValue(in TValue data)
		{
			this.Data = data;
		}

		public readonly TValue Data;

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool TryGetSpan(out ReadOnlySpan<byte> span) => TEncoder.TryGetSpan(in this.Data, out span);

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool TryGetSizeHint(out int sizeHint) => TEncoder.TryGetSizeHint(this.Data, out sizeHint);

		[MustUseReturnValue, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool TryEncode(Span<byte> destination, out int bytesWritten) => TEncoder.TryEncode(destination, out bytesWritten, in this.Data);

		public void Encode(ArrayPool<byte> pool, out byte[]? buffer, out ReadOnlySpan<byte> span, out Range range)
		{
			// maybe this is already encoded?
			if (TEncoder.TryGetSpan(this.Data, out span))
			{
				buffer = null;
				range = default;
				return;
			}

			if (!TEncoder.TryGetSizeHint(this.Data, out var capacity))
			{
				capacity = 256;
			}

			byte[]? tmp = null;
			try
			{
				while (true)
				{
					tmp = pool.Rent(capacity);
					if (TEncoder.TryEncode(tmp, out int valueSize, in this.Data))
					{
						if (valueSize == 0)
						{
							buffer = null;
							span = default;
							range = default;
							tmp = null;
							return;
						}

						buffer = tmp;
						span = tmp.AsSpan(0, valueSize);
						range = new(0, valueSize);
						tmp = null;
						break;
					}

					// double the buffer capacity, if possible
					pool.Return(tmp);
					tmp = null;

					if (capacity >= Fdb.MaxValueSize)
					{
						// it would be too large anyway!
						throw new ArgumentException("Cannot format value because it would exceed the maximum allowed length");
					}

					capacity *= 2;
				}
			}
			finally
			{
				if (tmp is not null)
				{
					buffer = null;
					span = default;
					range = default;
					pool.Return(tmp);
				}
			}
		}

		public Slice ToSlice()
		{
			var pool = ArrayPool<byte>.Shared;
			Encode(pool, out var buffer, out var span, out _);
			var slice = span.ToSlice();
			if (buffer is not null)
			{
				pool.Return(buffer);
			}
			return slice;
		}

		public SliceOwner ToSlice(ArrayPool<byte>? pool)
		{
			pool ??= ArrayPool<byte>.Shared;

			Encode(pool, out var buffer, out var span, out var range);

			return buffer is null
				? SliceOwner.Copy(span, pool)
				: SliceOwner.Create(buffer.AsSlice(range));
		}


		/// <inheritdoc />
		public override string ToString() => ToString(null, null);

		/// <inheritdoc />
		public string ToString(string? format, IFormatProvider? provider = null)
		{
			return string.Create(CultureInfo.InvariantCulture, $"{this.Data}");
		}

		/// <inheritdoc />
		public bool TryFormat(Span<char> destination, out int charsWritten, ReadOnlySpan<char> format, IFormatProvider? provider)
		{
			return destination.TryWrite(CultureInfo.InvariantCulture, $"{this.Data}", out charsWritten);
		}

	}

	/// <summary>Wraps a span of bytes or characters</summary>
	/// <typeparam name="TElement">Type of the elements. Can only be <see cref="byte"/> or <see cref="char"/></typeparam>
	[DebuggerDisplay("Data={Data}")]
	public readonly ref struct FdbSpanValue<TElement> : IFdbValue
	{

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public FdbSpanValue(ReadOnlySpan<TElement> data)
		{
			if (typeof(TElement) != typeof(byte) && typeof(TElement) != typeof(char))
			{
				throw new NotSupportedException("Can only store bytes of characters");
			}
			this.Data = data;
		}

		/// <summary>Wrapped span</summary>
		public readonly ReadOnlySpan<TElement> Data;

		private ReadOnlySpan<byte> GetAsBytes()
		{
			if (typeof(TElement) == typeof(byte))
			{
				return new(ref Unsafe.As<TElement, byte>(ref MemoryMarshal.GetReference(this.Data)));
			}
			throw new NotSupportedException();
		}

		private ReadOnlySpan<char> GetAsChars()
		{
			if (typeof(TElement) == typeof(char))
			{
				return new(ref Unsafe.As<TElement, char>(ref MemoryMarshal.GetReference(this.Data)));
			}
			throw new NotSupportedException();
		}

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool TryGetSpan(out ReadOnlySpan<byte> span)
		{
			if (typeof(TElement) == typeof(byte))
			{
				span = GetAsBytes();
				return true;
			}

			span = default;
			return false;
		}

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool TryGetSizeHint(out int sizeHint)
		{
			if (typeof(TElement) == typeof(byte))
			{
				sizeHint = this.Data.Length;
				return true;
			}

			if (typeof(TElement) == typeof(char))
			{
				return SpanEncoders.Utf8Encoder.TryGetSizeHint(GetAsChars(), out sizeHint);
			}

			sizeHint = 0;
			return false;
		}

		[MustUseReturnValue, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool TryEncode(Span<byte> destination, out int bytesWritten)
		{
			if (typeof(TElement) == typeof(byte))
			{
				return GetAsBytes().TryCopyTo(destination, out bytesWritten);
			}
			if (typeof(TElement) == typeof(char))
			{
				return SpanEncoders.Utf8Encoder.TryEncode(destination, out bytesWritten, GetAsChars());
			}
			throw new NotSupportedException();
		}

		public Slice ToSlice()
		{
			if (typeof(TElement) == typeof(byte))
			{
				return Slice.FromBytes(GetAsBytes());
			}
			if (typeof(TElement) == typeof(char))
			{
				return Slice.FromStringUtf8(GetAsChars());
			}
			throw new NotSupportedException();
		}

		public SliceOwner ToSlice(ArrayPool<byte>? pool)
		{
			if (typeof(TElement) == typeof(byte))
			{
				return Slice.FromBytes(GetAsBytes(), pool ?? ArrayPool<byte>.Shared);
			}

			if (typeof(TElement) == typeof(char))
			{
				return Slice.FromStringUtf8(GetAsChars(), pool ?? ArrayPool<byte>.Shared);
			}

			throw new NotSupportedException();
		}

		/// <inheritdoc />
		public override string ToString() => ToString(null, null);

		/// <inheritdoc />
		public string ToString(string? format, IFormatProvider? provider = null)
		{
			if (typeof(TElement) == typeof(byte))
			{
				return $"`{Slice.Dump(GetAsBytes())}`";
			}

			if (typeof(TElement) == typeof(char))
			{
				return $"\"{GetAsChars()}\"";
			}

			throw new NotSupportedException();
		}

		/// <inheritdoc />
		public bool TryFormat(Span<char> destination, out int charsWritten, ReadOnlySpan<char> format, IFormatProvider? provider)
		{
			if (typeof(TElement) == typeof(byte))
			{
				return destination.TryWrite($"`{Slice.Dump(GetAsBytes())}`", out charsWritten);
			}

			if (typeof(TElement) == typeof(char))
			{
				return destination.TryWrite($"\"{GetAsChars()}\"", out charsWritten);
			}

			throw new NotSupportedException();
		}

	}

	public readonly struct FdbTupleValue<T1> : IFdbValue
	{

		[SkipLocalsInit]
		public FdbTupleValue(T1 item1)
		{
			this.Item1 = item1;
		}

		public readonly T1 Item1;

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public FdbTupleValue<T1, T2> Append<T2>(T2 item2) => new(this.Item1, item2);

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public FdbTupleValue<T1, T2, T3> Append<T2, T3>(T2 item2, T3 item3) => new(STuple.Create(this.Item1, item2, item3));

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public FdbTupleValue<T1, T2, T3, T4, T5> Append<T2, T3, T4, T5>(T2 item2, T3 item3, T4 item4, T5 item5) => new(STuple.Create(this.Item1, item2, item3, item4, item5));

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public FdbTupleValue<T1, T2, T3, T4, T5, T6> Append<T2, T3, T4, T5, T6>(T2 item2, T3 item3, T4 item4, T5 item5, T6 item6) => new(STuple.Create(this.Item1, item2, item3, item4, item5, item6));

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public FdbTupleValue<T1, T2, T3, T4, T5, T6, T7> Append<T2, T3, T4, T5, T6, T7>(T2 item2, T3 item3, T4 item4, T5 item5, T6 item6, T7 item7) => new(STuple.Create(this.Item1, item2, item3, item4, item5, item6, item7));

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public FdbTupleValue<T1, T2, T3, T4, T5, T6, T7, T8> Append<T2, T3, T4, T5, T6, T7, T8>(T2 item2, T3 item3, T4 item4, T5 item5, T6 item6, T7 item7, T8 item8) => new(STuple.Create(this.Item1, item2, item3, item4, item5, item6, item7, item8));

		/// <inheritdoc />
		public string ToString(string? format, IFormatProvider? formatProvider = null) => STuple.Create(this.Item1).ToString();

		/// <inheritdoc />
		public bool TryFormat(Span<char> destination, out int charsWritten, ReadOnlySpan<char> format, IFormatProvider? provider) => destination.TryWrite($"{STuple.Create(this.Item1)}", out charsWritten);

		/// <inheritdoc />
		public override string ToString() => ToString(null, null);

		/// <inheritdoc />
		public bool TryGetSpan(out ReadOnlySpan<byte> span) { span = default; return false; }

		/// <inheritdoc />
		public bool TryGetSizeHint(out int sizeHint) { sizeHint = 0; return false; }

		/// <inheritdoc />
		public bool TryEncode(Span<byte> destination, out int bytesWritten) => TupleEncoder.TryEncodeKey(destination, out bytesWritten, default, this.Item1);

		/// <inheritdoc />
		public Slice ToSlice() => TupleEncoder.EncodeKey(default, this.Item1);

		/// <inheritdoc />
		public SliceOwner ToSlice(ArrayPool<byte>? pool) => TupleEncoder.Pack(pool ?? ArrayPool<byte>.Shared, default, STuple.Create(this.Item1));

	}

	public readonly struct FdbTupleValue<T1, T2> : IFdbValue
	{

		[SkipLocalsInit, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public FdbTupleValue(in STuple<T1, T2> items)
		{
			this.Items = items;
		}

		[SkipLocalsInit, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public FdbTupleValue(in ValueTuple<T1, T2> items)
		{
			this.Items = items.ToSTuple();
		}

		[SkipLocalsInit, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public FdbTupleValue(T1 item1, T2 item2)
		{
			this.Items = STuple.Create(item1, item2);
		}

		public readonly STuple<T1, T2> Items;

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public FdbTupleValue<T1, T2, T3> Append<T3>(T3 item3) => new(STuple.Create(this.Items.Item1, this.Items.Item2, item3));

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public FdbTupleValue<T1, T2, T3, T4, T5> Append<T3, T4, T5>(T3 item3, T4 item4, T5 item5) => new(STuple.Create(this.Items.Item1, this.Items.Item2, item3, item4, item5));

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public FdbTupleValue<T1, T2, T3, T4, T5, T6> Append<T3, T4, T5, T6>(T3 item3, T4 item4, T5 item5, T6 item6) => new(STuple.Create(this.Items.Item1, this.Items.Item2, item3, item4, item5, item6));

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public FdbTupleValue<T1, T2, T3, T4, T5, T6, T7> Append<T3, T4, T5, T6, T7>(T3 item3, T4 item4, T5 item5, T6 item6, T7 item7) => new(STuple.Create(this.Items.Item1, this.Items.Item2, item3, item4, item5, item6, item7));

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public FdbTupleValue<T1, T2, T3, T4, T5, T6, T7, T8> Append<T3, T4, T5, T6, T7, T8>(T3 item3, T4 item4, T5 item5, T6 item6, T7 item7, T8 item8) => new(STuple.Create(this.Items.Item1, this.Items.Item2, item3, item4, item5, item6, item7, item8));

		/// <inheritdoc />
		public string ToString(string? format, IFormatProvider? formatProvider) => this.Items.ToString();

		/// <inheritdoc />
		public bool TryFormat(Span<char> destination, out int charsWritten, ReadOnlySpan<char> format, IFormatProvider? provider) => this.Items.TryFormat(destination, out charsWritten);

		/// <inheritdoc />
		public override string ToString() => ToString(null, null);

		/// <inheritdoc />
		public bool TryGetSpan(out ReadOnlySpan<byte> span) { span = default; return false; }

		/// <inheritdoc />
		public bool TryGetSizeHint(out int sizeHint) { sizeHint = 0; return false; }

		/// <inheritdoc />
		public bool TryEncode(Span<byte> destination, out int bytesWritten) => TupleEncoder.TryPackTo(destination, out bytesWritten, default, in this.Items);

		/// <inheritdoc />
		public Slice ToSlice() => TupleEncoder.Pack(default, in this.Items);

		/// <inheritdoc />
		public SliceOwner ToSlice(ArrayPool<byte>? pool) => TupleEncoder.Pack(pool ?? ArrayPool<byte>.Shared, default, in this.Items);

	}

	public readonly struct FdbTupleValue<T1, T2, T3> : IFdbValue
	{

		[SkipLocalsInit, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public FdbTupleValue(in STuple<T1, T2, T3> items)
		{
			this.Items = items;
		}

		[SkipLocalsInit, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public FdbTupleValue(in ValueTuple<T1, T2, T3> items)
		{
			this.Items = items.ToSTuple();
		}

		[SkipLocalsInit, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public FdbTupleValue(T1 item1, T2 item2, T3 item3)
		{
			this.Items = STuple.Create(item1, item2, item3);
		}

		public readonly STuple<T1, T2, T3> Items;

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public FdbTupleValue<T1, T2, T3, T4> Append<T4>(T4 item4) => new(STuple.Create(this.Items.Item1, this.Items.Item2, this.Items.Item3, item4));

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public FdbTupleValue<T1, T2, T3, T4, T5> Append<T4, T5>(T4 item4, T5 item5) => new(STuple.Create(this.Items.Item1, this.Items.Item2, this.Items.Item3, item4, item5));

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public FdbTupleValue<T1, T2, T3, T4, T5, T6> Append<T4, T5, T6>(T4 item4, T5 item5, T6 item6) => new(STuple.Create(this.Items.Item1, this.Items.Item2, this.Items.Item3, item4, item5, item6));

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public FdbTupleValue<T1, T2, T3, T4, T5, T6, T7> Append<T4, T5, T6, T7>(T4 item4, T5 item5, T6 item6, T7 item7) => new(STuple.Create(this.Items.Item1, this.Items.Item2, this.Items.Item3, item4, item5, item6, item7));

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public FdbTupleValue<T1, T2, T3, T4, T5, T6, T7, T8> Append<T4, T5, T6, T7, T8>(T4 item4, T5 item5, T6 item6, T7 item7, T8 item8) => new(STuple.Create(this.Items.Item1, this.Items.Item2, this.Items.Item3, item4, item5, item6, item7, item8));

		/// <inheritdoc />
		public string ToString(string? format, IFormatProvider? formatProvider) => this.Items.ToString();

		/// <inheritdoc />
		public bool TryFormat(Span<char> destination, out int charsWritten, ReadOnlySpan<char> format, IFormatProvider? provider) => this.Items.TryFormat(destination, out charsWritten);

		/// <inheritdoc />
		public override string ToString() => ToString(null, null);

		/// <inheritdoc />
		public bool TryGetSpan(out ReadOnlySpan<byte> span) { span = default; return false; }

		/// <inheritdoc />
		public bool TryGetSizeHint(out int sizeHint) { sizeHint = 0; return false; }

		/// <inheritdoc />
		public bool TryEncode(Span<byte> destination, out int bytesWritten) => TupleEncoder.TryPackTo(destination, out bytesWritten, default, in this.Items);

		/// <inheritdoc />
		public Slice ToSlice() => TupleEncoder.Pack(default, in this.Items);

		/// <inheritdoc />
		public SliceOwner ToSlice(ArrayPool<byte>? pool) => TupleEncoder.Pack(pool ?? ArrayPool<byte>.Shared, default, in this.Items);

	}

	public readonly struct FdbTupleValue<T1, T2, T3, T4> : IFdbValue
	{

		[SkipLocalsInit, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public FdbTupleValue(in STuple<T1, T2, T3, T4> items)
		{
			this.Items = items;
		}

		[SkipLocalsInit, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public FdbTupleValue(in ValueTuple<T1, T2, T3, T4> items)
		{
			this.Items = items.ToSTuple();
		}

		[SkipLocalsInit, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public FdbTupleValue(T1 item1, T2 item2, T3 item3, T4 item4)
		{
			this.Items = STuple.Create(item1, item2, item3, item4);
		}

		public readonly STuple<T1, T2, T3, T4> Items;

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public FdbTupleValue<T1, T2, T3, T4, T5> Append<T5>(T5 item5) => new(STuple.Create(this.Items.Item1, this.Items.Item2, this.Items.Item3, this.Items.Item4, item5));

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public FdbTupleValue<T1, T2, T3, T4, T5, T6> Append<T5, T6>(T5 item5, T6 item6) => new(STuple.Create(this.Items.Item1, this.Items.Item2, this.Items.Item3, this.Items.Item4, item5, item6));

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public FdbTupleValue<T1, T2, T3, T4, T5, T6, T7> Append<T5, T6, T7>(T5 item5, T6 item6, T7 item7) => new(STuple.Create(this.Items.Item1, this.Items.Item2, this.Items.Item3, this.Items.Item4, item5, item6, item7));

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public FdbTupleValue<T1, T2, T3, T4, T5, T6, T7, T8> Append<T5, T6, T7, T8>(T5 item5, T6 item6, T7 item7, T8 item8) => new(STuple.Create(this.Items.Item1, this.Items.Item2, this.Items.Item3, this.Items.Item4, item5, item6, item7, item8));

		/// <inheritdoc />
		public string ToString(string? format, IFormatProvider? formatProvider) => this.Items.ToString();

		/// <inheritdoc />
		public bool TryFormat(Span<char> destination, out int charsWritten, ReadOnlySpan<char> format, IFormatProvider? provider) => this.Items.TryFormat(destination, out charsWritten);

		/// <inheritdoc />
		public override string ToString() => ToString(null, null);

		/// <inheritdoc />
		public bool TryGetSpan(out ReadOnlySpan<byte> span) { span = default; return false; }

		/// <inheritdoc />
		public bool TryGetSizeHint(out int sizeHint) { sizeHint = 0; return false; }

		/// <inheritdoc />
		public bool TryEncode(Span<byte> destination, out int bytesWritten) => TupleEncoder.TryPackTo(destination, out bytesWritten, default, in this.Items);

		/// <inheritdoc />
		public Slice ToSlice() => TupleEncoder.Pack(default, in this.Items);

		/// <inheritdoc />
		public SliceOwner ToSlice(ArrayPool<byte>? pool) => TupleEncoder.Pack(pool ?? ArrayPool<byte>.Shared, default, in this.Items);

	}

	public readonly struct FdbTupleValue<T1, T2, T3, T4, T5> : IFdbValue
	{

		[SkipLocalsInit, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public FdbTupleValue(in STuple<T1, T2, T3, T4, T5> items)
		{
			this.Items = items;
		}

		[SkipLocalsInit, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public FdbTupleValue(in ValueTuple<T1, T2, T3, T4, T5> items)
		{
			this.Items = items.ToSTuple();
		}

		[SkipLocalsInit, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public FdbTupleValue(T1 item1, T2 item2, T3 item3, T4 item4, T5 item5)
		{
			this.Items = STuple.Create(item1, item2, item3, item4, item5);
		}

		public readonly STuple<T1, T2, T3, T4, T5> Items;

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public FdbTupleValue<T1, T2, T3, T4, T5, T6> Append<T6>(T6 item6) => new(STuple.Create(this.Items.Item1, this.Items.Item2, this.Items.Item3, this.Items.Item4, this.Items.Item5, item6));

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public FdbTupleValue<T1, T2, T3, T4, T5, T6, T7> Append<T6, T7>(T6 item6, T7 item7) => new(STuple.Create(this.Items.Item1, this.Items.Item2, this.Items.Item3, this.Items.Item4, this.Items.Item5, item6, item7));

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public FdbTupleValue<T1, T2, T3, T4, T5, T6, T7, T8> Append<T6, T7, T8>(T6 item6, T7 item7, T8 item8) => new(STuple.Create(this.Items.Item1, this.Items.Item2, this.Items.Item3, this.Items.Item4, this.Items.Item5, item6, item7, item8));

		/// <inheritdoc />
		public string ToString(string? format, IFormatProvider? formatProvider) => this.Items.ToString();

		/// <inheritdoc />
		public bool TryFormat(Span<char> destination, out int charsWritten, ReadOnlySpan<char> format, IFormatProvider? provider) => this.Items.TryFormat(destination, out charsWritten);

		/// <inheritdoc />
		public override string ToString() => ToString(null, null);

		/// <inheritdoc />
		public bool TryGetSpan(out ReadOnlySpan<byte> span) { span = default; return false; }

		/// <inheritdoc />
		public bool TryGetSizeHint(out int sizeHint) { sizeHint = 0; return false; }

		/// <inheritdoc />
		public bool TryEncode(Span<byte> destination, out int bytesWritten) => TupleEncoder.TryPackTo(destination, out bytesWritten, default, in this.Items);

		/// <inheritdoc />
		public Slice ToSlice() => TupleEncoder.Pack(default, in this.Items);

		/// <inheritdoc />
		public SliceOwner ToSlice(ArrayPool<byte>? pool) => TupleEncoder.Pack(pool ?? ArrayPool<byte>.Shared, default, in this.Items);

	}

	public readonly struct FdbTupleValue<T1, T2, T3, T4, T5, T6> : IFdbValue
	{

		[SkipLocalsInit, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public FdbTupleValue(in STuple<T1, T2, T3, T4, T5, T6> items)
		{
			this.Items = items;
		}

		[SkipLocalsInit, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public FdbTupleValue(in ValueTuple<T1, T2, T3, T4, T5, T6> items)
		{
			this.Items = items.ToSTuple();
		}

		[SkipLocalsInit, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public FdbTupleValue(T1 item1, T2 item2, T3 item3, T4 item4, T5 item5, T6 item6)
		{
			this.Items = STuple.Create(item1, item2, item3, item4, item5, item6);
		}

		public readonly STuple<T1, T2, T3, T4, T5, T6> Items;

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public FdbTupleValue<T1, T2, T3, T4, T5, T6, T7> Append<T7>(T7 item7) => new(this.Items.Item1, this.Items.Item2, this.Items.Item3, this.Items.Item4, this.Items.Item5, this.Items.Item6, item7);

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public FdbTupleValue<T1, T2, T3, T4, T5, T6, T7, T8> Append<T7, T8>(T7 item7, T8 item8) => new(this.Items.Item1, this.Items.Item2, this.Items.Item3, this.Items.Item4, this.Items.Item5, this.Items.Item6, item7, item8);

		/// <inheritdoc />
		public string ToString(string? format, IFormatProvider? formatProvider) => this.Items.ToString();

		/// <inheritdoc />
		public bool TryFormat(Span<char> destination, out int charsWritten, ReadOnlySpan<char> format, IFormatProvider? provider) => this.Items.TryFormat(destination, out charsWritten);

		/// <inheritdoc />
		public override string ToString() => ToString(null, null);

		/// <inheritdoc />
		public bool TryGetSpan(out ReadOnlySpan<byte> span) { span = default; return false; }

		/// <inheritdoc />
		public bool TryGetSizeHint(out int sizeHint) { sizeHint = 0; return false; }

		/// <inheritdoc />
		public bool TryEncode(Span<byte> destination, out int bytesWritten) => TupleEncoder.TryPackTo(destination, out bytesWritten, default, in this.Items);

		/// <inheritdoc />
		public Slice ToSlice() => TupleEncoder.Pack(default, in this.Items);

		/// <inheritdoc />
		public SliceOwner ToSlice(ArrayPool<byte>? pool) => TupleEncoder.Pack(pool ?? ArrayPool<byte>.Shared, default, in this.Items);

	}

	public readonly struct FdbTupleValue<T1, T2, T3, T4, T5, T6, T7> : IFdbValue
	{

		[SkipLocalsInit, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public FdbTupleValue(in STuple<T1, T2, T3, T4, T5, T6, T7> items)
		{
			this.Items = items;
		}

		[SkipLocalsInit, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public FdbTupleValue(in ValueTuple<T1, T2, T3, T4, T5, T6, T7> items)
		{
			this.Items = items.ToSTuple();
		}

		[SkipLocalsInit, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public FdbTupleValue(T1 item1, T2 item2, T3 item3, T4 item4, T5 item5, T6 item6, T7 item7)
		{
			this.Items = STuple.Create(item1, item2, item3, item4, item5, item6, item7);
		}


		public readonly STuple<T1, T2, T3, T4, T5, T6, T7> Items;

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public FdbTupleValue<T1, T2, T3, T4, T5, T6, T7, T8> Append<T8>(T8 item8) => new(this.Items.Item1, this.Items.Item2, this.Items.Item3, this.Items.Item4, this.Items.Item5, this.Items.Item6, this.Items.Item7, item8);

		/// <inheritdoc />
		public string ToString(string? format, IFormatProvider? formatProvider) => this.Items.ToString();

		/// <inheritdoc />
		public bool TryFormat(Span<char> destination, out int charsWritten, ReadOnlySpan<char> format, IFormatProvider? provider) => this.Items.TryFormat(destination, out charsWritten);

		/// <inheritdoc />
		public override string ToString() => ToString(null, null);

		/// <inheritdoc />
		public bool TryGetSpan(out ReadOnlySpan<byte> span) { span = default; return false; }

		/// <inheritdoc />
		public bool TryGetSizeHint(out int sizeHint) { sizeHint = 0; return false; }

		/// <inheritdoc />
		public bool TryEncode(Span<byte> destination, out int bytesWritten) => TupleEncoder.TryPackTo(destination, out bytesWritten, default, in this.Items);

		/// <inheritdoc />
		public Slice ToSlice() => TupleEncoder.Pack(default, in this.Items);

		/// <inheritdoc />
		public SliceOwner ToSlice(ArrayPool<byte>? pool) => TupleEncoder.Pack(pool ?? ArrayPool<byte>.Shared, default, in this.Items);

	}

	public readonly struct FdbTupleValue<T1, T2, T3, T4, T5, T6, T7, T8> : IFdbValue
	{

		[SkipLocalsInit, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public FdbTupleValue(in STuple<T1, T2, T3, T4, T5, T6, T7, T8> items)
		{
			this.Items = items;
		}

		[SkipLocalsInit, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public FdbTupleValue(in ValueTuple<T1, T2, T3, T4, T5, T6, T7, ValueTuple<T8>> items)
		{
			this.Items = items.ToSTuple();
		}

		[SkipLocalsInit, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public FdbTupleValue(T1 item1, T2 item2, T3 item3, T4 item4, T5 item5, T6 item6, T7 item7, T8 item8)
		{
			this.Items = STuple.Create(item1, item2, item3, item4, item5, item6, item7, item8);
		}

		public readonly STuple<T1, T2, T3, T4, T5, T6, T7, T8> Items;

		/// <inheritdoc />
		public string ToString(string? format, IFormatProvider? formatProvider) => this.Items.ToString();

		/// <inheritdoc />
		public bool TryFormat(Span<char> destination, out int charsWritten, ReadOnlySpan<char> format, IFormatProvider? provider) => this.Items.TryFormat(destination, out charsWritten);

		/// <inheritdoc />
		public override string ToString() => ToString(null, null);

		/// <inheritdoc />
		public bool TryGetSpan(out ReadOnlySpan<byte> span) { span = default; return false; }

		/// <inheritdoc />
		public bool TryGetSizeHint(out int sizeHint) { sizeHint = 0; return false; }

		/// <inheritdoc />
		public bool TryEncode(Span<byte> destination, out int bytesWritten) => TupleEncoder.TryPackTo(destination, out bytesWritten, default, in this.Items);

		/// <inheritdoc />
		public Slice ToSlice() => TupleEncoder.Pack(default, in this.Items);

		/// <inheritdoc />
		public SliceOwner ToSlice(ArrayPool<byte>? pool) => TupleEncoder.Pack(pool ?? ArrayPool<byte>.Shared, default, in this.Items);

	}

}
