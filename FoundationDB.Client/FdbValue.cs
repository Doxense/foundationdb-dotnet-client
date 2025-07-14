#region Copyright (c) 2023-2025 SnowBank SAS
//
// All rights are reserved. Reproduction or transmission in whole or in part, in
// any form or by any means, electronic, mechanical or otherwise, is prohibited
// without the prior written consent of the copyright owner.
//
#endregion

namespace FoundationDB.Client
{

	/// <summary>Factory class for values</summary>
	[PublicAPI]
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
			public static FdbRawValue FromBytes(byte[] value) => new(value.AsSlice());

			/// <summary>Returns a value that wraps a byte array</summary>
			[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
			public static FdbRawValue FromBytes(byte[] value, int start, int length) => new(value.AsSlice(start, length));

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
			public static FdbVarTupleValue Pack(IVarTuple value) => new(value);

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

			public static FdbValue<string, SpanEncoders.Utf8Encoder> FromUtf8(string? value) => new(value);

			public static FdbValue<StringBuilder, SpanEncoders.Utf8Encoder> FromUtf8(StringBuilder? value) => new(value);

			public static FdbValue<ReadOnlyMemory<char>, SpanEncoders.Utf8Encoder> FromUtf8(char[] value) => new(value.AsMemory());

			public static FdbValue<ReadOnlyMemory<char>, SpanEncoders.Utf8Encoder> FromUtf8(char[] value, int start, int length) => new(value.AsMemory(start, length));

			public static FdbValue<ReadOnlyMemory<char>, SpanEncoders.Utf8Encoder> FromUtf8(ReadOnlyMemory<char> value) => new(value);

			public static FdbSpanValue<char> FromUtf8(ReadOnlySpan<char> value) => new(value);

		}

		public static class FixedSize
		{

			#region Little Endian...

			/// <summary>Returns a value that wraps a fixed-size signed 32-bit integer, encoded as little-endian</summary>
			[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
			public static FdbValue<int, SpanEncoders.FixedSizeLittleEndianEncoder> FromInt32LittleEndian(int value) => new(value);

			/// <summary>Returns a value that wraps a fixed-size unsigned 32-bit integer, encoded as little-endian</summary>
			[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
			public static FdbValue<uint, SpanEncoders.FixedSizeLittleEndianEncoder> FromUInt32LittleEndian(uint value) => new(value);

			/// <summary>Returns a value that wraps a fixed-size signed 64-bit integer, encoded as little-endian</summary>
			[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
			public static FdbValue<long, SpanEncoders.FixedSizeLittleEndianEncoder> FromInt64LittleEndian(long value) => new(value);

			/// <summary>Returns a value that wraps a fixed-size unsigned 64-bit integer, encoded as little-endian</summary>
			[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
			public static FdbValue<ulong, SpanEncoders.FixedSizeLittleEndianEncoder> FromUInt64LittleEndian(ulong value) => new(value);

			#endregion

			#region Big Endian...

			/// <summary>Returns a value that wraps a fixed-size signed 32-bit integer, encoded as big-endian</summary>
			[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
			public static FdbValue<int, SpanEncoders.FixedSizeBigEndianEncoder> FromInt32BigEndian(int value) => new(value);

			/// <summary>Returns a value that wraps a fixed-size unsigned 32-bit integer, encoded as big-endian</summary>
			[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
			public static FdbValue<uint, SpanEncoders.FixedSizeBigEndianEncoder> FromUInt32BigEndian(uint value) => new(value);

			/// <summary>Returns a value that wraps a fixed-size signed 64-bit integer, encoded as big-endian</summary>
			[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
			public static FdbValue<long, SpanEncoders.FixedSizeBigEndianEncoder> FromInt64BigEndian(long value) => new(value);

			/// <summary>Returns a value that wraps a fixed-size unsigned 64-bit integer, encoded as big-endian</summary>
			[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
			public static FdbValue<ulong, SpanEncoders.FixedSizeBigEndianEncoder> FromUInt64BigEndian(ulong value) => new(value);

			#endregion

		}

		public static class Compact
		{

			#region Little Endian...

			/// <summary>Returns a key that wraps a byte array</summary>
			[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
			public static FdbValue<int, SpanEncoders.CompactLittleEndianEncoder> FromInt32LittleEndian(int value) => new(value);

			/// <summary>Returns a key that wraps a byte array</summary>
			[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
			public static FdbValue<uint, SpanEncoders.CompactLittleEndianEncoder> FromUInt32LittleEndian(uint value) => new(value);

			/// <summary>Returns a key that wraps a byte array</summary>
			[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
			public static FdbValue<long, SpanEncoders.CompactLittleEndianEncoder> FromInt64LittleEndian(long value) => new(value);

			/// <summary>Returns a key that wraps a byte array</summary>
			[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
			public static FdbValue<ulong, SpanEncoders.CompactLittleEndianEncoder> FromUInt64LittleEndian(ulong value) => new(value);

			#endregion

			#region Big Endian...

			/// <summary>Returns a key that wraps a byte array</summary>
			[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
			public static FdbValue<int, SpanEncoders.CompactBigEndianEncoder> FromInt32BigEndian(int value) => new(value);

			/// <summary>Returns a key that wraps a byte array</summary>
			[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
			public static FdbValue<uint, SpanEncoders.CompactBigEndianEncoder> FromUInt32BigEndian(uint value) => new(value);

			/// <summary>Returns a key that wraps a byte array</summary>
			[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
			public static FdbValue<long, SpanEncoders.CompactBigEndianEncoder> FromInt64BigEndian(long value) => new(value);

			/// <summary>Returns a key that wraps a byte array</summary>
			[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
			public static FdbValue<ulong, SpanEncoders.CompactBigEndianEncoder> FromUInt64BigEndian(ulong value) => new(value);

			#endregion

		}

		public static class Uuids
		{

			public static FdbValue<Guid, SpanEncoders.FixedSizeUuidEncoder> FromUuid128(Guid value) => new(value);

			public static FdbValue<Uuid128, SpanEncoders.FixedSizeUuidEncoder> FromUuid128(Uuid128 value) => new(value);

			public static FdbValue<Uuid96, SpanEncoders.FixedSizeUuidEncoder> FromUuid96(Uuid96 value) => new(value);

			public static FdbValue<Uuid80, SpanEncoders.FixedSizeUuidEncoder> FromUuid80(Uuid80 value) => new(value);

			public static FdbValue<Uuid64, SpanEncoders.FixedSizeUuidEncoder> FromUuid64(Uuid64 value) => new(value);

			public static FdbValue<Uuid48, SpanEncoders.FixedSizeUuidEncoder> FromUuid48(Uuid48 value) => new(value);

			public static FdbValue<VersionStamp, SpanEncoders.FixedSizeUuidEncoder> FromVersionStamp(VersionStamp value) => new(value);

		}

	}

	public static class FdbValueExtensions
	{

		/// <summary>Returns a pre-encoded version of a value</summary>
		/// <typeparam name="TValue">Type of the value to pre-encode</typeparam>
		/// <param name="value">value to pre-encoded</param>
		/// <returns>Value with a cached version of the encoded original</returns>
		/// <remarks>This value can be used multiple times without re-encoding the original</remarks>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static FdbRawValue Memoize<TValue>(this TValue value)
			where TValue : struct, IFdbValue
		{
			if (typeof(TValue) == typeof(FdbRawValue))
			{ // already cached!
				return (FdbRawValue) (object) value;
			}

			return new(value.ToSlice());
		}

		/// <summary>Encodes this value into <see cref="Slice"/></summary>
		/// <param name="value">Value to encode</param>
		/// <returns><see cref="Slice"/> that contains the binary representation of this value</returns>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static Slice ToSlice<TValue>(this TValue value)
			where TValue : struct, IFdbValue
		{
			if (typeof(TValue) == typeof(FdbRawKey))
			{
				return ((FdbRawKey) (object) value).Data;
			}

			if (value.TryGetSpan(out var span))
			{
				return Slice.FromBytes(span);
			}

			return ToSliceSlow(in value);

			[MethodImpl(MethodImplOptions.NoInlining)]
			static Slice ToSliceSlow(in TValue value)
			{
				byte[]? tmp = null;
				if (value.TryGetSizeHint(out var capacity))
				{
					// we will hope for the best, and pre-allocate the slice

					tmp = new byte[capacity];
					if (value.TryEncode(tmp, out var bytesWritten))
					{
						return tmp.AsSlice(0, bytesWritten);
					}

					if (capacity >= FdbValue.MaxSize)
					{
						goto key_too_long;
					}

					capacity *= 2;
				}
				else
				{
					capacity = 256;
				}

				var pool = ArrayPool<byte>.Shared;
				try
				{
					while (true)
					{
						tmp = pool.Rent(capacity);
						if (value.TryEncode(tmp, out int bytesWritten))
						{
							return tmp.AsSlice(0, bytesWritten).Copy();
						}

						pool.Return(tmp);
						tmp = null;

						if (capacity >= FdbValue.MaxSize)
						{
							goto key_too_long;
						}

						capacity *= 2;
					}
				}
				catch (Exception)
				{
					if (tmp is not null)
					{
						pool.Return(tmp);
					}

					throw;
				}

			key_too_long:
				// it would be too large anyway!
				throw new ArgumentException("Cannot encode value because it would exceed the maximum allowed length.");
			}
		}

		/// <summary>Encodes this value into <see cref="Slice"/>, using backing buffer rented from a pool</summary>
		/// <param name="value">Value to encode</param>
		/// <param name="pool">Pool used to rent the buffer (<see cref="ArrayPool{T}.Shared"/> is <c>null</c>)</param>
		/// <returns><see cref="SliceOwner"/> that contains the binary representation of this value</returns>
		[MustDisposeResource, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static SliceOwner ToSlice<TValue>(this TValue value, ArrayPool<byte>? pool)
			where TValue : struct, IFdbValue
		{
			pool ??= ArrayPool<byte>.Shared;

			if (typeof(TValue) == typeof(FdbRawKey))
			{
				return SliceOwner.Wrap(((FdbRawKey) (object) value).Data);
			}

			return value.TryGetSpan(out var span)
				? SliceOwner.Copy(span, pool)
				: Encode(in value, pool);
		}

		[MethodImpl(MethodImplOptions.NoInlining)]
		internal static SliceOwner Encode<TValue>(in TValue value, ArrayPool<byte> pool, int? sizeHint = null)
			where TValue : struct, IFdbValue
		{
			Contract.Debug.Requires(pool is not null);

			int capacity;
			if (sizeHint is not null)
			{
				capacity = sizeHint.Value;
			}
			else if (!value.TryGetSizeHint(out capacity))
			{
				capacity = 128;
			}
			if (capacity <= 0)
			{
				capacity = 256;
			}

			byte[]? tmp = null;
			try
			{
				while (true)
				{
					tmp = pool.Rent(capacity);
					if (value.TryEncode(tmp, out int bytesWritten))
					{
						if (bytesWritten == 0)
						{
							pool.Return(tmp);
							tmp = null;
							return SliceOwner.Empty;
						}

						return SliceOwner.Create(tmp.AsSlice(0, bytesWritten), pool);
					}

					pool.Return(tmp);
					tmp = null;

					if (capacity >= FdbValue.MaxSize)
					{
						// it would be too large anyway!
						throw new ArgumentException("Cannot encode value because it would exceed the maximum allowed length.");
					}
					capacity *= 2;
				}
			}
			catch(Exception)
			{
				if (tmp is not null)
				{
					pool.Return(tmp);
				}
				throw;
			}
		}

		[MustUseReturnValue, MethodImpl(MethodImplOptions.NoInlining)]
		internal static ReadOnlySpan<byte> Encode<TValue>(scoped in TValue value, scoped ref byte[]? buffer, ArrayPool<byte> pool)
			where TValue : struct, IFdbValue
		{
			Contract.Debug.Requires(pool is not null);

			if (!value.TryGetSizeHint(out int capacity))
			{
				capacity = 0;
			}
			if (capacity <= 0)
			{
				capacity = 256;
			}

			while (true)
			{
				if (buffer is null)
				{
					buffer = pool.Rent(capacity);
				}
				else if (buffer.Length < capacity)
				{
					pool.Return(buffer);
					buffer = pool.Rent(capacity);
				}

				if (value.TryEncode(buffer, out int bytesWritten))
				{
					return bytesWritten > 0 ? buffer.AsSpan(0, bytesWritten) : default;
				}

				if (capacity >= FdbKey.MaxSize)
				{
					// it would be too large anyway!
					throw new ArgumentException("Cannot encode value because it would exceed the maximum allowed length.");
				}
				capacity *= 2;
			}
		}
	}

	/// <summary>Value that can be encoded into bytes</summary>
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

	}

	/// <summary>Value that wraps raw bytes</summary>
	[PublicAPI]
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

	}

	/// <summary>Value that will be converted into bytes by a <see cref="ISpanEncoder{TValue}"/></summary>
	/// <typeparam name="TValue">Type of the value</typeparam>
	/// <typeparam name="TEncoder">Type of the encoder for this value</typeparam>
	[DebuggerDisplay("Data={Data}")]
	public readonly struct FdbValue<TValue, TEncoder> : IFdbValue
		where TEncoder: struct, ISpanEncoder<TValue>
	{

		public FdbValue(TValue? data)
		{
			this.Data = data;
		}

		public FdbValue(in TValue data)
		{
			this.Data = data;
		}

		public readonly TValue? Data;

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool TryGetSpan(out ReadOnlySpan<byte> span) => TEncoder.TryGetSpan(in this.Data, out span);

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool TryGetSizeHint(out int sizeHint) => TEncoder.TryGetSizeHint(in this.Data, out sizeHint);

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
		public override string ToString() => ToString(null);

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

	/// <summary>Value that wraps a span of bytes or characters</summary>
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

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private ReadOnlySpan<byte> GetAsBytes()
		{
			if (typeof(TElement) != typeof(byte))
			{
				throw new NotSupportedException();
			}

			ref byte ptr = ref Unsafe.As<TElement, byte>(ref MemoryMarshal.GetReference(this.Data));
			return MemoryMarshal.CreateSpan(ref ptr, this.Data.Length);

		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private ReadOnlySpan<char> GetAsChars()
		{
			if (typeof(TElement) != typeof(char))
			{
				throw new NotSupportedException();
			}

			ref char ptr = ref Unsafe.As<TElement, char>(ref MemoryMarshal.GetReference(this.Data));
			return MemoryMarshal.CreateSpan(ref ptr, this.Data.Length);

		}

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool TryGetSpan(out ReadOnlySpan<byte> span)
		{
			if (typeof(TElement) == typeof(byte))
			{
				span = GetAsBytes();
				return true;
			}

			if (typeof(TElement) == typeof(char))
			{
				span = default;
				return this.Data.Length == 0;
			}

			throw new NotSupportedException();
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

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
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

		[MustDisposeResource, MethodImpl(MethodImplOptions.AggressiveInlining)]
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
		public override string ToString() => ToString(null);

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

	#region Tuples...

	/// <summary>Value that wraps an <see cref="IVarTuple"/> with any number of elements</summary>
	public readonly struct FdbVarTupleValue : IFdbValue
	{

		[SkipLocalsInit]
		public FdbVarTupleValue(IVarTuple items)
		{
			this.Items = items;
		}

		public readonly IVarTuple Items;

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public FdbVarTupleValue Append<T1>(T1 item1) => new(this.Items.Append(item1));

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public FdbVarTupleValue Append<T1, T2>(T1 item1, T2 item2) => new(this.Items.Append(item1, item2));

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public FdbVarTupleValue Append<T1, T2, T3>(T1 item1, T2 item2, T3 item3) => new(this.Items.Append(item1, item2, item3));

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public FdbVarTupleValue Append<T1, T2, T3, T4>(T1 item1, T2 item2, T3 item3, T4 item4) => new(this.Items.Append(item1, item2, item3, item4));

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public FdbVarTupleValue Append<T1, T2, T3, T4, T5>(T1 item1, T2 item2, T3 item3, T4 item4, T5 item5) => new(this.Items.Append(item1, item2, item3, item4, item5));

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public FdbVarTupleValue Append<T1, T2, T3, T4, T5, T6>(T1 item1, T2 item2, T3 item3, T4 item4, T5 item5, T6 item6) => new(this.Items.Append(item1, item2, item3, item4, item5, item6));

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public FdbVarTupleValue Append<T1, T2, T3, T4, T5, T6, T7>(T1 item1, T2 item2, T3 item3, T4 item4, T5 item5, T6 item6, T7 item7) => new(this.Items.Append(item1, item2, item3, item4, item5, item6, item7));

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public FdbVarTupleValue Append<T1, T2, T3, T4, T5, T6, T7, T8>(T1 item1, T2 item2, T3 item3, T4 item4, T5 item5, T6 item6, T7 item7, T8 item8) => new(this.Items.Append(item1, item2, item3, item4, item5, item6, item7, item8));

		/// <inheritdoc />
		public string ToString(string? format, IFormatProvider? formatProvider = null) => this.Items.ToString()!;

		/// <inheritdoc />
		public bool TryFormat(Span<char> destination, out int charsWritten, ReadOnlySpan<char> format, IFormatProvider? provider) => destination.TryWrite($"{this.Items}", out charsWritten);

		/// <inheritdoc />
		public override string ToString() => ToString(null);

		/// <inheritdoc />
		public bool TryGetSpan(out ReadOnlySpan<byte> span) { span = default; return false; }

		/// <inheritdoc />
		public bool TryGetSizeHint(out int sizeHint) { sizeHint = 0; return false; }

		/// <inheritdoc />
		public bool TryEncode(Span<byte> destination, out int bytesWritten) => TuPack.TryPackTo(destination, out bytesWritten, in this.Items);

	}

	/// <summary>Value that wraps a tuple with 1 element</summary>
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
		public override string ToString() => ToString(null);

		/// <inheritdoc />
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool TryGetSpan(out ReadOnlySpan<byte> span) { span = default; return false; }

		/// <inheritdoc />
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool TryGetSizeHint(out int sizeHint)
		{
			return TupleEncoder.TryGetSizeHint(this.Item1, out sizeHint);
		}

		/// <inheritdoc />
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool TryEncode(Span<byte> destination, out int bytesWritten) => TupleEncoder.TryEncodeKey(destination, out bytesWritten, default, this.Item1);

	}

	/// <summary>Value that wraps a tuple with 2 elements</summary>
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
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool TryGetSpan(out ReadOnlySpan<byte> span) { span = default; return false; }

		/// <inheritdoc />
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool TryGetSizeHint(out int sizeHint)
		{
			if (!TupleEncoder.TryGetSizeHint(this.Items.Item1, out var size1)
			 || !TupleEncoder.TryGetSizeHint(this.Items.Item2, out var size2))
			{
				sizeHint = 0;
				return false;
			}

			sizeHint = checked(size1 + size2);
			return true;
		}

		/// <inheritdoc />
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool TryEncode(Span<byte> destination, out int bytesWritten) => TupleEncoder.TryPackTo(destination, out bytesWritten, default, in this.Items);

	}

	/// <summary>Value that wraps a tuple with 3 elements</summary>
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
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool TryGetSpan(out ReadOnlySpan<byte> span) { span = default; return false; }

		/// <inheritdoc />
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool TryGetSizeHint(out int sizeHint)
		{
			if (!TupleEncoder.TryGetSizeHint(this.Items.Item1, out var size1)
			 || !TupleEncoder.TryGetSizeHint(this.Items.Item2, out var size2)
			 || !TupleEncoder.TryGetSizeHint(this.Items.Item3, out var size3))
			{
				sizeHint = 0;
				return false;
			}

			sizeHint = checked(size1 + size2 + size3);
			return true;
		}

		/// <inheritdoc />
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool TryEncode(Span<byte> destination, out int bytesWritten) => TupleEncoder.TryPackTo(destination, out bytesWritten, default, in this.Items);

	}

	/// <summary>Value that wraps a tuple with 4 elements</summary>
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
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool TryGetSpan(out ReadOnlySpan<byte> span) { span = default; return false; }

		/// <inheritdoc />
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool TryGetSizeHint(out int sizeHint)
		{
			if (!TupleEncoder.TryGetSizeHint(this.Items.Item1, out var size1)
			 || !TupleEncoder.TryGetSizeHint(this.Items.Item2, out var size2)
			 || !TupleEncoder.TryGetSizeHint(this.Items.Item3, out var size3)
			 || !TupleEncoder.TryGetSizeHint(this.Items.Item4, out var size4))
			{
				sizeHint = 0;
				return false;
			}

			sizeHint = checked(size1 + size2 + size3 + size4);
			return true;
		}

		/// <inheritdoc />
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool TryEncode(Span<byte> destination, out int bytesWritten) => TupleEncoder.TryPackTo(destination, out bytesWritten, default, in this.Items);

	}

	/// <summary>Value that wraps a tuple with 5 elements</summary>
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
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool TryGetSpan(out ReadOnlySpan<byte> span) { span = default; return false; }

		/// <inheritdoc />
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool TryGetSizeHint(out int sizeHint)
		{
			if (!TupleEncoder.TryGetSizeHint(this.Items.Item1, out var size1)
			 || !TupleEncoder.TryGetSizeHint(this.Items.Item2, out var size2)
			 || !TupleEncoder.TryGetSizeHint(this.Items.Item3, out var size3)
			 || !TupleEncoder.TryGetSizeHint(this.Items.Item4, out var size4)
			 || !TupleEncoder.TryGetSizeHint(this.Items.Item5, out var size5))
			{
				sizeHint = 0;
				return false;
			}

			sizeHint = checked(size1 + size2 + size3 + size4 + size5);
			return true;
		}

		/// <inheritdoc />
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool TryEncode(Span<byte> destination, out int bytesWritten) => TupleEncoder.TryPackTo(destination, out bytesWritten, default, in this.Items);

	}

	/// <summary>Value that wraps a tuple with 6 elements</summary>
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
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool TryGetSpan(out ReadOnlySpan<byte> span) { span = default; return false; }

		/// <inheritdoc />
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool TryGetSizeHint(out int sizeHint)
		{
			if (!TupleEncoder.TryGetSizeHint(this.Items.Item1, out var size1)
			 || !TupleEncoder.TryGetSizeHint(this.Items.Item2, out var size2)
			 || !TupleEncoder.TryGetSizeHint(this.Items.Item3, out var size3)
			 || !TupleEncoder.TryGetSizeHint(this.Items.Item4, out var size4)
			 || !TupleEncoder.TryGetSizeHint(this.Items.Item5, out var size5)
			 || !TupleEncoder.TryGetSizeHint(this.Items.Item6, out var size6))
			{
				sizeHint = 0;
				return false;
			}

			sizeHint = checked(size1 + size2 + size3 + size4 + size5 + size6);
			return true;
		}

		/// <inheritdoc />
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool TryEncode(Span<byte> destination, out int bytesWritten) => TupleEncoder.TryPackTo(destination, out bytesWritten, default, in this.Items);

	}

	/// <summary>Value that wraps a tuple with 7 elements</summary>
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
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool TryGetSpan(out ReadOnlySpan<byte> span) { span = default; return false; }

		/// <inheritdoc />
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool TryGetSizeHint(out int sizeHint)
		{
			if (!TupleEncoder.TryGetSizeHint(this.Items.Item1, out var size1)
			 || !TupleEncoder.TryGetSizeHint(this.Items.Item2, out var size2)
			 || !TupleEncoder.TryGetSizeHint(this.Items.Item3, out var size3)
			 || !TupleEncoder.TryGetSizeHint(this.Items.Item4, out var size4)
			 || !TupleEncoder.TryGetSizeHint(this.Items.Item5, out var size5)
			 || !TupleEncoder.TryGetSizeHint(this.Items.Item6, out var size6)
			 || !TupleEncoder.TryGetSizeHint(this.Items.Item7, out var size7))
			{
				sizeHint = 0;
				return false;
			}

			sizeHint = checked(size1 + size2 + size3 + size4 + size5 + size6 + size7);
			return true;
		}

		/// <inheritdoc />
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool TryEncode(Span<byte> destination, out int bytesWritten) => TupleEncoder.TryPackTo(destination, out bytesWritten, default, in this.Items);

	}

	/// <summary>Value that wraps a tuple with 8 elements</summary>
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
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool TryGetSpan(out ReadOnlySpan<byte> span) { span = default; return false; }

		/// <inheritdoc />
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool TryGetSizeHint(out int sizeHint)
		{
			if (!TupleEncoder.TryGetSizeHint(this.Items.Item1, out var size1)
			 || !TupleEncoder.TryGetSizeHint(this.Items.Item2, out var size2)
			 || !TupleEncoder.TryGetSizeHint(this.Items.Item3, out var size3)
			 || !TupleEncoder.TryGetSizeHint(this.Items.Item4, out var size4)
			 || !TupleEncoder.TryGetSizeHint(this.Items.Item5, out var size5)
			 || !TupleEncoder.TryGetSizeHint(this.Items.Item6, out var size6)
			 || !TupleEncoder.TryGetSizeHint(this.Items.Item7, out var size7)
			 || !TupleEncoder.TryGetSizeHint(this.Items.Item8, out var size8))
			{
				sizeHint = 0;
				return false;
			}

			sizeHint = checked(size1 + size2 + size3 + size4 + size5 + size6 + size7 + size8);
			return true;
		}

		/// <inheritdoc />
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool TryEncode(Span<byte> destination, out int bytesWritten) => TupleEncoder.TryPackTo(destination, out bytesWritten, default, in this.Items);

	}

	#endregion

}
