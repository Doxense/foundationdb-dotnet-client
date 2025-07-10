#region Copyright (c) 2023-2025 SnowBank SAS
//
// All rights are reserved. Reproduction or transmission in whole or in part, in
// any form or by any means, electronic, mechanical or otherwise, is prohibited
// without the prior written consent of the copyright owner.
//
#endregion

namespace FoundationDB.Client
{

	public static partial class FdbValue
	{

		public const int MaxSize = Fdb.MaxValueSize;

		/// <summary>Returns a value that wraps a <see cref="Slice"/></summary>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static FdbValue<Slice, SpanEncoders.RawEncoder> Create(Slice value) => new(value);

		/// <summary>Returns a value that wraps a byte array</summary>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static FdbValue<byte[], SpanEncoders.RawEncoder> Create(byte[] value) => new(value);

		/// <summary>Returns a value that wraps the content of a <see cref="MemoryStream"/></summary>
		/// <remarks>The stream will be written from the start, and NOT the current position.</remarks>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static FdbValue<MemoryStream, SpanEncoders.RawEncoder> Create(MemoryStream value) => new(value);

		public static class Tuples
		{

			/// <summary>Returns a value that wraps a tuple</summary>
			[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
			public static FdbValue<TTuple, SpanEncoders.TupleEncoder<TTuple>> Pack<TTuple>(in TTuple value)
				where TTuple : IVarTuple
				=> new(in value);

			/// <summary>Returns a value that wraps a tuple</summary>
			[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
			public static FdbValue<STuple<T1>, SpanEncoders.TupleEncoder<STuple<T1>>> Key<T1>(T1 item1)
				=> new(new(item1));

			/// <summary>Returns a value that wraps a tuple</summary>
			[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
			public static FdbValue<STuple<T1, T2>, SpanEncoders.TupleEncoder<STuple<T1, T2>>> Key<T1, T2>(T1 item1, T2 item2)
				=> new(new(item1, item2));

			/// <summary>Returns a value that wraps a tuple</summary>
			[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
			public static FdbValue<STuple<T1, T2, T3>, SpanEncoders.TupleEncoder<STuple<T1, T2, T3>>> Key<T1, T2, T3>(T1 item1, T2 item2, T3 item3)
				=> new(new(item1, item2, item3));

		}

		public static class FixedSize
		{

			public static class LittleEndian
			{
				/// <summary>Returns a value that wraps a fixed-size signed 32-bit integer, encoded as little-endian</summary>
				[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
				public static FdbValue<int, SpanEncoders.FixedSizeLittleEndianEncoder> CreateInt32(int value) => new(value);

				/// <summary>Returns a value that wraps a fixed-size unsigned 32-bit integer, encoded as little-endian</summary>
				[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
				public static FdbValue<uint, SpanEncoders.FixedSizeLittleEndianEncoder> CreateUInt32(uint value) => new(value);

				/// <summary>Returns a value that wraps a fixed-size signed 64-bit integer, encoded as little-endian</summary>
				[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
				public static FdbValue<long, SpanEncoders.FixedSizeLittleEndianEncoder> CreateInt64(long value) => new(value);

				/// <summary>Returns a value that wraps a fixed-size unsigned 64-bit integer, encoded as little-endian</summary>
				[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
				public static FdbValue<ulong, SpanEncoders.FixedSizeLittleEndianEncoder> CreateUInt64(ulong value) => new(value);

			}

			public static class BigEndian
			{
				/// <summary>Returns a value that wraps a fixed-size signed 32-bit integer, encoded as big-endian</summary>
				[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
				public static FdbValue<int, SpanEncoders.FixedSizeBigEndianEncoder> CreateInt32(int value) => new(value);

				/// <summary>Returns a value that wraps a fixed-size unsigned 32-bit integer, encoded as big-endian</summary>
				[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
				public static FdbValue<uint, SpanEncoders.FixedSizeBigEndianEncoder> CreateUInt32(uint value) => new(value);

				/// <summary>Returns a value that wraps a fixed-size signed 64-bit integer, encoded as big-endian</summary>
				[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
				public static FdbValue<long, SpanEncoders.FixedSizeBigEndianEncoder> CreateInt64(long value) => new(value);

				/// <summary>Returns a value that wraps a fixed-size unsigned 64-bit integer, encoded as big-endian</summary>
				[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
				public static FdbValue<ulong, SpanEncoders.FixedSizeBigEndianEncoder> CreateUInt64(ulong value) => new(value);

			}

		}

		public static class Compact
		{

			public static class LittleEndian
			{
				/// <summary>Returns a key that wraps a byte array</summary>
				[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
				public static FdbValue<int, SpanEncoders.CompactLittleEndianEncoder> CreateInt32(int value) => new(value);

				/// <summary>Returns a key that wraps a byte array</summary>
				[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
				public static FdbValue<uint, SpanEncoders.CompactLittleEndianEncoder> CreateUInt32(uint value) => new(value);

				/// <summary>Returns a key that wraps a byte array</summary>
				[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
				public static FdbValue<long, SpanEncoders.CompactLittleEndianEncoder> CreateInt64(long value) => new(value);

				/// <summary>Returns a key that wraps a byte array</summary>
				[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
				public static FdbValue<ulong, SpanEncoders.CompactLittleEndianEncoder> CreateUInt64(ulong value) => new(value);

			}

			public static class BigEndian
			{
				/// <summary>Returns a key that wraps a byte array</summary>
				[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
				public static FdbValue<int, SpanEncoders.CompactBigEndianEncoder> CreateInt32(int value) => new(value);

				/// <summary>Returns a key that wraps a byte array</summary>
				[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
				public static FdbValue<uint, SpanEncoders.CompactBigEndianEncoder> CreateUInt32(uint value) => new(value);

				/// <summary>Returns a key that wraps a byte array</summary>
				[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
				public static FdbValue<long, SpanEncoders.CompactBigEndianEncoder> CreateInt64(long value) => new(value);

				/// <summary>Returns a key that wraps a byte array</summary>
				[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
				public static FdbValue<ulong, SpanEncoders.CompactBigEndianEncoder> CreateUInt64(ulong value) => new(value);

			}

		}

	}

	[DebuggerDisplay("Data={Data}")]
	public readonly struct FdbValue<TValue, TEncoder>
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
					if (TEncoder.TryEncode(tmp, out int keySize, in this.Data))
					{
						if (keySize == 0)
						{
							buffer = null;
							span = default;
							range = default;
							tmp = null;
							return;
						}

						buffer = tmp;
						span = tmp.AsSpan(0, keySize);
						range = default;
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

			if (buffer is null)
			{
				return SliceOwner.Copy(span, pool);
			}
			else
			{
				return SliceOwner.Create(buffer.AsSlice(range));
			}
		}

	}

}
