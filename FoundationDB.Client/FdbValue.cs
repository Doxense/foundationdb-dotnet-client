#region Copyright (c) 2023-2025 SnowBank SAS
//
// All rights are reserved. Reproduction or transmission in whole or in part, in
// any form or by any means, electronic, mechanical or otherwise, is prohibited
// without the prior written consent of the copyright owner.
//
#endregion

namespace FoundationDB.Client
{
	using System;

	/// <summary>Factory class for values</summary>
	[PublicAPI]
	public static class FdbValue
	{

		public const int MaxSize = Fdb.MaxValueSize;

		public static readonly Slice Empty = Slice.Empty;

		#region Generic...

		/// <summary>Returns a value that encodes an instance of <typeparamref name="TValue"/> with the given encoder</summary>
		/// <typeparam name="TValue">Type of the encoded value</typeparam>
		/// <typeparam name="TEncoder">Type of the encoder, that implements <see cref="ISpanEncoder{TValue}"/></typeparam>
		/// <param name="value">Value to encode</param>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static FdbValue<TValue, TEncoder> Create<TValue, TEncoder>(TValue value)
			where TEncoder : struct, ISpanEncoder<TValue>
		{
			return new(value);
		}

		#endregion

		#region Binary...

		/// <summary>Returns a value that wraps a <see cref="Slice"/></summary>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static FdbRawValue ToBytes(Slice value) => new(value);

		/// <summary>Returns a value that wraps a byte array</summary>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static FdbRawValue ToBytes(byte[] value) => new(value.AsSlice());

		/// <summary>Returns a value that wraps a byte array</summary>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static FdbRawValue ToBytes(byte[] value, int start, int length) => new(value.AsSlice(start, length));

#if NET9_0_OR_GREATER
		/// <summary>Returns a value that wraps a span of bytes</summary>
		public static FdbSpanValue<byte> ToBytes(ReadOnlySpan<byte> value)
			=> new(value);
#else
		/// <summary>Returns a value that wraps a span of bytes</summary>
		/// <remarks>
		/// <para><b>WARNING:</b> on .NET 8.0 (or lower) this method has to copy the span into a Slice, which causes memory allocations.</para>
		/// <para>Please consider using <see cref="Slice"/> instead, or upgrade to .NET 9.0 or higher.</para>
		/// </remarks>
		[OverloadResolutionPriority(-1)]
		public static FdbRawValue ToBytes(ReadOnlySpan<byte> value)
			=> new(Slice.FromBytes(value));
#endif

		/// <summary>Returns a value that wraps the content of a <see cref="MemoryStream"/></summary>
		/// <remarks>The stream will be written from the start, and NOT the current position.</remarks>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static FdbValue<MemoryStream, SpanEncoders.RawEncoder> ToBytes(MemoryStream value) => new(value);

		#endregion

		#region Tuples...

		/// <summary>Returns a value that wraps a tuple</summary>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static FdbVarTupleValue FromTuple(IVarTuple value) => new(value);

		/// <summary>Returns a value that wraps a tuple with a single element</summary>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static STuple<T1> FromTuple<T1>(in ValueTuple<T1> tuple)
			=> new(tuple.Item1);

		/// <summary>Returns a value that wraps a tuple with 2 elements</summary>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static STuple<T1, T2> FromTuple<T1, T2>(in ValueTuple<T1, T2> tuple)
			=> new(tuple.Item1, tuple.Item2);

		/// <summary>Returns a value that wraps a tuple with 3 elements</summary>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static STuple<T1, T2, T3> FromTuple<T1, T2, T3>(in ValueTuple<T1, T2, T3> tuple)
			=> new(tuple.Item1, tuple.Item2, tuple.Item3);

		/// <summary>Returns a value that wraps a tuple with 4 elements</summary>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static STuple<T1, T2, T3, T4> FromTuple<T1, T2, T3, T4>(in ValueTuple<T1, T2, T3, T4> tuple)
			=> new(tuple.Item1, tuple.Item2, tuple.Item3, tuple.Item4);

		/// <summary>Returns a value that wraps a tuple with 5 elements</summary>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static STuple<T1, T2, T3, T4, T5> FromTuple<T1, T2, T3, T4, T5>(in ValueTuple<T1, T2, T3, T4, T5> tuple)
			=> new(tuple.Item1, tuple.Item2, tuple.Item3, tuple.Item4, tuple.Item5);

		/// <summary>Returns a value that wraps a tuple with 6 elements</summary>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static STuple<T1, T2, T3, T4, T5, T6> FromTuple<T1, T2, T3, T4, T5, T6>(in ValueTuple<T1, T2, T3, T4, T5, T6> tuple)
			=> new(tuple.Item1, tuple.Item2, tuple.Item3, tuple.Item4, tuple.Item5, tuple.Item6);

		/// <summary>Returns a value that wraps a tuple with 7 elements</summary>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static STuple<T1, T2, T3, T4, T5, T6, T7> FromTuple<T1, T2, T3, T4, T5, T6, T7>(in ValueTuple<T1, T2, T3, T4, T5, T6, T7> tuple)
			=> new(tuple.Item1, tuple.Item2, tuple.Item3, tuple.Item4, tuple.Item5, tuple.Item6, tuple.Item7);

		/// <summary>Returns a value that wraps a tuple with 8 elements</summary>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static STuple<T1, T2, T3, T4, T5, T6, T7, T8> FromTuple<T1, T2, T3, T4, T5, T6, T7, T8>(in ValueTuple<T1, T2, T3, T4, T5, T6, T7, ValueTuple<T8>> tuple)
			=> new(tuple.Item1, tuple.Item2, tuple.Item3, tuple.Item4, tuple.Item5, tuple.Item6, tuple.Item7, tuple.Item8);

		/// <summary>Returns a value that encodes as a tuple with a single element</summary>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static STuple<T1> ToTuple<T1>(T1 item1) => new(item1);

		/// <summary>Returns a value that encodes as a tuple with 2 elements</summary>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static STuple<T1, T2> ToTuple<T1, T2>(T1 item1, T2 item2) => new(item1, item2);

		/// <summary>Returns a value that encodes as a tuple with 3 elements</summary>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static STuple<T1, T2, T3> ToTuple<T1, T2, T3>(T1 item1, T2 item2, T3 item3) => new(item1, item2, item3);

		/// <summary>Returns a value that encodes as a tuple with 4 elements</summary>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static STuple<T1, T2, T3, T4> ToTuple<T1, T2, T3, T4>(T1 item1, T2 item2, T3 item3, T4 item4) => new(item1, item2, item3, item4);

		/// <summary>Returns a value that encodes as a tuple with 5 elements</summary>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static STuple<T1, T2, T3, T4, T5> ToTuple<T1, T2, T3, T4, T5>(T1 item1, T2 item2, T3 item3, T4 item4, T5 item5) => new(item1, item2, item3, item4, item5);

		/// <summary>Returns a value that encodes as a tuple with 6 elements</summary>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static STuple<T1, T2, T3, T4, T5, T6> ToTuple<T1, T2, T3, T4, T5, T6>(T1 item1, T2 item2, T3 item3, T4 item4, T5 item5, T6 item6) => new(item1, item2, item3, item4, item5, item6);

		/// <summary>Returns a value that encodes as a tuple with 7 elements</summary>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static STuple<T1, T2, T3, T4, T5, T6, T7> ToTuple<T1, T2, T3, T4, T5, T6, T7>(T1 item1, T2 item2, T3 item3, T4 item4, T5 item5, T6 item6, T7 item7) => new(item1, item2, item3, item4, item5, item6, item7);

		/// <summary>Returns a value that encodes as a tuple with 8 elements</summary>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static STuple<T1, T2, T3, T4, T5, T6, T7, T8> ToTuple<T1, T2, T3, T4, T5, T6, T7, T8>(T1 item1, T2 item2, T3 item3, T4 item4, T5 item5, T6 item6, T7 item7, T8 item8) => new(item1, item2, item3, item4, item5, item6, item7, item8);

		#endregion

		#region Text...

		#region UTF-8...

		/// <summary>Returns a value that wraps a string, encoded as UTF-8 bytes</summary>
		public static FdbUtf8Value ToTextUtf8(string? value) => new(value.AsMemory());

		/// <summary>Returns a value that wraps a StringBuilder, encoded as UTF-8 bytes</summary>
		public static FdbValue<StringBuilder, SpanEncoders.Utf8Encoder> ToTextUtf8(StringBuilder? value) => new(value);

		/// <summary>Returns a value that wraps a char array, encoded as UTF-8 bytes</summary>
		public static FdbUtf8Value ToTextUtf8(char[] value) => new(value.AsMemory());

		/// <summary>Returns a value that wraps a segment of a char array, encoded as UTF-8 bytes</summary>
		public static FdbUtf8Value ToTextUtf8(char[] value, int start, int length) => new(value.AsMemory(start, length));

		/// <summary>Returns a value that wraps a span of char, encoded as UTF-8 bytes</summary>
		public static FdbUtf8Value ToTextUtf8(ReadOnlyMemory<char> value) => new(value);

#if NET9_0_OR_GREATER
		/// <summary>Returns a value that wraps a span of char, encoded as UTF-8 bytes</summary>
		public static FdbUtf8SpanValue ToTextUtf8(ReadOnlySpan<char> value) => new(value);
#else
		/// <summary>Returns a value that copies the contents of a span of char, encoded as UTF-8 bytes</summary>
		/// <remarks>
		/// <para><b>WARNING:</b> on .NET 8.0 (or lower) this method has to copy the span into a string, which causes memory allocations.</para>
		/// <para>Please consider using <see cref="ReadOnlyMemory{char}"/> instead, or upgrade to .NET 9.0 or higher.</para>
		/// </remarks>
		[OverloadResolutionPriority(-1)]
		public static FdbUtf8Value ToTextUtf8(ReadOnlySpan<char> value) => new(value.ToArray().AsMemory());
#endif

		#endregion

		#region UTF-16...

		/// <summary>Returns a value that wraps a string, encoded as UTF-16 bytes</summary>
		public static FdbUtf16Value ToTextUtf16(string? value) => new(value.AsMemory());

		/// <summary>Returns a value that wraps a StringBuilder, encoded as UTF-16 bytes</summary>
		public static FdbValue<StringBuilder, SpanEncoders.Utf16Encoder> ToTextUtf16(StringBuilder? value) => new(value);

		/// <summary>Returns a value that wraps a char array, encoded as UTF-16 bytes</summary>
		public static FdbUtf16Value ToTextUtf16(char[] value) => new(value.AsMemory());

		/// <summary>Returns a value that wraps a segment of a char array, encoded as UTF-16 bytes</summary>
		public static FdbUtf16Value ToTextUtf16(char[] value, int start, int length) => new(value.AsMemory(start, length));

		/// <summary>Returns a value that wraps a span of char, encoded as UTF-16 bytes</summary>
		public static FdbUtf16Value ToTextUtf16(ReadOnlyMemory<char> value) => new(value);

#if NET9_0_OR_GREATER
		/// <summary>Returns a value that wraps a span of char, encoded as UTF-16 bytes</summary>
		public static FdbUtf16SpanValue ToTextUtf16(ReadOnlySpan<char> value) => new(value);
#else
		/// <summary>Returns a value that copies the contents of a span of char, encoded as UTF-16 bytes</summary>
		/// <remarks>
		/// <para><b>WARNING:</b> on .NET 8.0 (or lower) this method has to copy the span into a string, which causes memory allocations.</para>
		/// <para>Please consider using <see cref="ReadOnlyMemory{char}"/> instead, or upgrade to .NET 9.0 or higher.</para>
		/// </remarks>
		[OverloadResolutionPriority(-1)]
		public static FdbUtf16Value ToTextUtf16(ReadOnlySpan<char> value) => new(value.ToArray().AsMemory());
#endif

		#endregion

		#endregion

		#region Fixed Size...

		/// <summary>Four 0x00 bytes (<c>`00 00 00 00`</c>)</summary>
		public static readonly FdbValue<uint, SpanEncoders.FixedSizeLittleEndianEncoder> Zero32;

		/// <summary>Eight 0x00 bytes (<c>`00 00 00 00 00 00 00 00`</c>)</summary>
		public static readonly FdbValue<ulong, SpanEncoders.FixedSizeLittleEndianEncoder> Zero64;

		/// <summary>Four 0xFF bytes (<c>`FF FF FF FF`</c>)</summary>
		public static readonly FdbValue<uint, SpanEncoders.FixedSizeLittleEndianEncoder> MaxValue32 = new(uint.MaxValue);

		/// <summary>Eight 0xFF bytes (<c>`FF FF FF FF FF FF FF FF`</c>)</summary>
		public static readonly FdbValue<ulong, SpanEncoders.FixedSizeLittleEndianEncoder> MaxValue64 = new(ulong.MaxValue);

		#region Little Endian...

		/// <summary>Returns a value that wraps a fixed-size signed 32-bit integer, encoded as little-endian</summary>
		/// <remarks>Ex: <c>0x1234</c> will be encoded as <c>00 00 12 34</c></remarks>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static FdbValue<int, SpanEncoders.FixedSizeLittleEndianEncoder> ToFixed32LittleEndian(int value) => new(value);

		/// <summary>Returns a value that wraps a fixed-size unsigned 32-bit integer, encoded as little-endian</summary>
		/// <remarks>Ex: <c>0x1234</c> will be encoded as <c>00 00 12 34</c></remarks>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static FdbValue<uint, SpanEncoders.FixedSizeLittleEndianEncoder> ToFixed32LittleEndian(uint value) => new(value);

		/// <summary>Returns a value that wraps a fixed-size signed 64-bit integer, encoded as little-endian</summary>
		/// <remarks>Ex: <c>0x1234</c> will be encoded as <c>00 00 00 00 00 00 12 34</c></remarks>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static FdbValue<long, SpanEncoders.FixedSizeLittleEndianEncoder> ToFixed64LittleEndian(long value) => new(value);

		/// <summary>Returns a value that wraps a fixed-size unsigned 64-bit integer, encoded as little-endian</summary>
		/// <remarks>Ex: <c>0x1234</c> will be encoded as <c>00 00 00 00 00 00 12 34</c></remarks>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static FdbValue<ulong, SpanEncoders.FixedSizeLittleEndianEncoder> ToFixed64LittleEndian(ulong value) => new(value);

		#endregion

		#region Big Endian...

		/// <summary>Returns a value that wraps a fixed-size signed 32-bit integer, encoded as big-endian</summary>
		/// <remarks>Ex: <c>0x1234</c> will be encoded as <c>34 12 00 00</c></remarks>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static FdbValue<int, SpanEncoders.FixedSizeBigEndianEncoder> ToFixed32BigEndian(int value) => new(value);

		/// <summary>Returns a value that wraps a fixed-size unsigned 32-bit integer, encoded as big-endian</summary>
		/// <remarks>Ex: <c>0x1234</c> will be encoded as <c>34 12 00 00</c></remarks>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static FdbValue<uint, SpanEncoders.FixedSizeBigEndianEncoder> ToFixed32BigEndian(uint value) => new(value);

		/// <summary>Returns a value that wraps a fixed-size signed 64-bit integer, encoded as big-endian</summary>
		/// <remarks>Ex: <c>0x1234</c> will be encoded as <c>34 12 00 00 00 00 00 00</c></remarks>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static FdbValue<long, SpanEncoders.FixedSizeBigEndianEncoder> ToFixed64BigEndian(long value) => new(value);

		/// <summary>Returns a value that wraps a fixed-size unsigned 64-bit integer, encoded as big-endian</summary>
		/// <remarks>Ex: <c>0x1234</c> will be encoded as <c>34 12 00 00 00 00 00 00</c></remarks>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static FdbValue<ulong, SpanEncoders.FixedSizeBigEndianEncoder> ToFixed64BigEndian(ulong value) => new(value);

		#endregion

		#endregion

		
		#region Compact...

		#region Little Endian...

		/// <summary>Returns a key that wraps a signed 32-bit integer, encoded as little endian using as few bytes as possible</summary>
		/// <remarks>Ex: <c>0x1234</c> will be encoded as <c>34 12</c></remarks>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static FdbValue<int, SpanEncoders.CompactLittleEndianEncoder> ToCompactLittleEndian(int value) => new(value);

		/// <summary>Returns a key that wraps an unsigned 32-bit integer, encoded as little endian using as few bytes as possible</summary>
		/// <remarks>Ex: <c>0x1234</c> will be encoded as <c>34 12</c></remarks>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static FdbValue<uint, SpanEncoders.CompactLittleEndianEncoder> ToCompactLittleEndian(uint value) => new(value);

		/// <summary>Returns a key that wraps a signed 64-bit integer, encoded as little endian using as few bytes as possible</summary>
		/// <remarks>Ex: <c>0x1234</c> will be encoded as <c>34 12</c></remarks>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static FdbValue<long, SpanEncoders.CompactLittleEndianEncoder> ToCompactLittleEndian(long value) => new(value);

		/// <summary>Returns a key that wraps an unsigned 64-bit integer, encoded as little endian using as few bytes as possible</summary>
		/// <remarks>Ex: <c>0x1234</c> will be encoded as <c>34 12</c></remarks>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static FdbValue<ulong, SpanEncoders.CompactLittleEndianEncoder> ToCompactLittleEndian(ulong value) => new(value);

		#endregion

		#region Big Endian...

		/// <summary>Returns a key that wraps an integer, encoded in big endian, using as few bytes as possible</summary>
		/// <remarks>Ex: <c>0x1234</c> will be encoded as <c>12 34</c></remarks>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static FdbValue<int, SpanEncoders.CompactBigEndianEncoder> ToCompactBigEndian(int value) => new(value);

		/// <summary>Returns a key that wraps an integer, encoded in big endian, using as few bytes as possible</summary>
		/// <remarks>Ex: <c>0x1234</c> will be encoded as <c>12 34</c></remarks>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static FdbValue<uint, SpanEncoders.CompactBigEndianEncoder> ToCompactBigEndian(uint value) => new(value);

		/// <summary>Returns a key that wraps an integer, encoded in big endian, using as few bytes as possible</summary>
		/// <remarks>Ex: <c>0x1234</c> will be encoded as <c>12 34</c></remarks>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static FdbValue<long, SpanEncoders.CompactBigEndianEncoder> ToCompactBigEndian(long value) => new(value);

		/// <summary>Returns a key that wraps an integer, encoded in big endian, using as few bytes as possible</summary>
		/// <remarks>Ex: <c>0x1234</c> will be encoded as <c>12 34</c></remarks>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static FdbValue<ulong, SpanEncoders.CompactBigEndianEncoder> ToCompactBigEndian(ulong value) => new(value);

		#endregion

		#endregion

		#region Uuids...

		/// <summary>Returns a key that wraps a 128-bit UUID, encoded as 16 bytes</summary>
		public static FdbValue<Guid, SpanEncoders.FixedSizeUuidEncoder> ToUuid128(Guid value) => new(value);

		#endregion

	}

	public static class FdbValueExtensions
	{

		/// <summary>Creates a pre-encoded version of a value that can be reused multiple times</summary>
		/// <typeparam name="TValue">Type of the value to pre-encode</typeparam>
		/// <param name="value">value to pre-encoded</param>
		/// <returns>Value with a cached version of the encoded original</returns>
		/// <remarks>This value can be used multiple times without re-encoding the original</remarks>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static FdbRawValue Memoize<TValue>(this TValue value)
#if NET9_0_OR_GREATER
			where TValue : struct, IFdbValue, allows ref struct
#else
			where TValue : struct, IFdbValue
#endif
		{
			if (typeof(TValue) == typeof(FdbRawValue))
			{ // already cached!
				return Unsafe.As<TValue, FdbRawValue>(ref value);
			}
			return new(ToSlice(in value));
		}

		/// <summary>Creates a pre-encoded version of a value that can be reused multiple times</summary>
		/// <typeparam name="TValue">Type of the value to pre-encode</typeparam>
		/// <param name="value">value to pre-encoded</param>
		/// <returns>Value with a cached version of the encoded original</returns>
		/// <remarks>This value can be used multiple times without re-encoding the original</remarks>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static FdbRawValue Memoize<TValue>(in TValue value)
#if NET9_0_OR_GREATER
			where TValue : struct, ISpanEncodable, allows ref struct
#else
			where TValue : struct, ISpanEncodable
#endif
		{
			if (typeof(TValue) == typeof(FdbRawValue))
			{ // already cached!
				return Unsafe.As<TValue, FdbRawValue>(ref Unsafe.AsRef(in value));
			}
			return new(ToSlice(in value));
		}

		/// <summary>Encodes this value into <see cref="Slice"/></summary>
		/// <param name="value">Value to encode</param>
		/// <returns><see cref="Slice"/> that contains the binary representation of this value</returns>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static Slice ToSlice<TValue>(this TValue value)
#if NET9_0_OR_GREATER
			where TValue : struct, IFdbValue, allows ref struct
#else
			where TValue : struct, IFdbValue
#endif
			=> ToSlice(in value);

		/// <summary>Encodes this value into <see cref="Slice"/></summary>
		/// <param name="value">Value to encode</param>
		/// <returns><see cref="Slice"/> that contains the binary representation of this value</returns>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static Slice ToSlice<TValue>(in TValue value)
#if NET9_0_OR_GREATER
			where TValue : struct, ISpanEncodable, allows ref struct
#else
			where TValue : struct, ISpanEncodable
#endif
		{
			if (typeof(TValue) == typeof(FdbRawValue))
			{
				return Unsafe.As<TValue, FdbRawValue>(ref Unsafe.AsRef(in value)).Data;
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
#if NET9_0_OR_GREATER
			where TValue : struct, IFdbValue, allows ref struct
#else
			where TValue : struct, IFdbValue
#endif
		{
			pool ??= ArrayPool<byte>.Shared;

			if (typeof(TValue) == typeof(FdbRawValue))
			{
				return SliceOwner.Wrap(Unsafe.As<TValue, FdbRawValue>(ref value).Data);
			}

			return value.TryGetSpan(out var span)
				? SliceOwner.Copy(span, pool)
				: Encode(in value, pool);
		}

		[MethodImpl(MethodImplOptions.NoInlining)]
		internal static SliceOwner Encode<TValue>(in TValue value, ArrayPool<byte> pool, int? sizeHint = null)
#if NET9_0_OR_GREATER
			where TValue : struct, ISpanEncodable, allows ref struct
#else
			where TValue : struct, ISpanEncodable
#endif
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
#if NET9_0_OR_GREATER
			where TValue : struct, ISpanEncodable, allows ref struct
#else
			where TValue : struct, ISpanEncodable
#endif
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
	public interface IFdbValue : ISpanEncodable, ISpanFormattable
	{

		//TODO: add a method to return a hint on the "type" of value? could help tools/loggers to properly format this value

	}

	/// <summary>Value that wraps raw bytes</summary>
	[PublicAPI]
	public readonly struct FdbRawValue : IFdbValue
	{

		public static readonly FdbRawValue Nil;

		public static readonly FdbRawValue Empty = new(Slice.Empty);

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static FdbRawValue Return(Slice slice) => new(slice);

		[SkipLocalsInit, MethodImpl(MethodImplOptions.AggressiveInlining)]
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

	}

	/// <summary>Value that wraps raw bytes</summary>
	[PublicAPI]
	public readonly struct FdbRawMemoryValue : IFdbValue
	{

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static FdbRawMemoryValue Return(ReadOnlyMemory<byte> slice) => new(slice);

		[SkipLocalsInit, MethodImpl(MethodImplOptions.AggressiveInlining)]
		internal FdbRawMemoryValue(ReadOnlyMemory<byte> data)
		{
			this.Data = data;
		}

		public readonly ReadOnlyMemory<byte> Data;

		/// <inheritdoc />
		public override string ToString() => this.Data.ToString();

		private Slice GetSliceOrCopy()
		{
			return MemoryMarshal.TryGetArray(Data, out var seg) ? seg.AsSlice() : Slice.FromBytes(this.Data.Span);
		}

		/// <inheritdoc />
		public string ToString(string? format, IFormatProvider? formatProvider)
			=> GetSliceOrCopy().ToString(format, formatProvider);

		/// <inheritdoc />
		public bool TryFormat(Span<char> destination, out int charsWritten, ReadOnlySpan<char> format, IFormatProvider? provider)
			=> GetSliceOrCopy().TryFormat(destination, out charsWritten, format, provider);

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
			sizeHint = this.Data.Length;
			return true;
		}

		/// <inheritdoc />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool TryEncode(Span<byte> destination, out int bytesWritten)
		{
			return this.Data.Span.TryCopyTo(destination, out bytesWritten);
		}

	}

	/// <summary>Value that wraps text that is encoded as UTF-8 bytes</summary>
	public readonly struct FdbUtf8Value : IFdbValue
	{

		[SkipLocalsInit, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public FdbUtf8Value(string text)
		{
			this.Text = text.AsMemory();
		}

		[SkipLocalsInit, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public FdbUtf8Value(ReadOnlyMemory<char> text)
		{
			this.Text = text;
		}

		public readonly ReadOnlyMemory<char> Text;

		/// <inheritdoc />
		public override string ToString() => ToString(null);

		/// <inheritdoc />
		public string ToString(string? format, IFormatProvider? formatProvider = null) => $"\"{this.Text.Span}\""; //TODO: escape?

		/// <inheritdoc />
		public bool TryFormat(Span<char> destination, out int charsWritten, ReadOnlySpan<char> format, IFormatProvider? provider)
		{
			return destination.TryWrite($"\"{this.Text.Span}\"", out charsWritten); //TODO: escape?
		}

		/// <inheritdoc />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool TryGetSpan(out ReadOnlySpan<byte> span) => SpanEncoders.Utf8Encoder.TryGetSpan(in this.Text, out span);

		/// <inheritdoc />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool TryGetSizeHint(out int sizeHint) => SpanEncoders.Utf8Encoder.TryGetSizeHint(in this.Text, out sizeHint);

		/// <inheritdoc />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool TryEncode(scoped Span<byte> destination, out int bytesWritten) => SpanEncoders.Utf8Encoder.TryEncode(destination, out bytesWritten, in this.Text);

	}

	/// <summary>Value that wraps text that is encoded as UTF-8 bytes</summary>
	public readonly ref struct FdbUtf8SpanValue : IFdbValue
	{

		[SkipLocalsInit, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public FdbUtf8SpanValue(ReadOnlySpan<char> text)
		{
			this.Text = text;
		}

		public readonly ReadOnlySpan<char> Text;

		/// <inheritdoc />
		public override string ToString() => ToString(null);

		/// <inheritdoc />
		public string ToString(string? format, IFormatProvider? formatProvider = null) => $"\"{this.Text}\""; //TODO: escape?

		/// <inheritdoc />
		public bool TryFormat(Span<char> destination, out int charsWritten, ReadOnlySpan<char> format, IFormatProvider? provider)
		{
			return destination.TryWrite($"\"{this.Text}\"", out charsWritten); //TODO: escape?
		}

		/// <inheritdoc />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool TryGetSpan(out ReadOnlySpan<byte> span) => SpanEncoders.Utf8Encoder.TryGetSpan(in this.Text, out span);

		/// <inheritdoc />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool TryGetSizeHint(out int sizeHint) => SpanEncoders.Utf8Encoder.TryGetSizeHint(in this.Text, out sizeHint);

		/// <inheritdoc />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool TryEncode(scoped Span<byte> destination, out int bytesWritten) => SpanEncoders.Utf8Encoder.TryEncode(destination, out bytesWritten, in this.Text);

	}

	/// <summary>Value that wraps text that is encoded as UTF-16 bytes</summary>
	public readonly struct FdbUtf16Value : IFdbValue
	{

		[SkipLocalsInit, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public FdbUtf16Value(string text)
		{
			this.Text = text.AsMemory();
		}

		[SkipLocalsInit, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public FdbUtf16Value(ReadOnlyMemory<char> text)
		{
			this.Text = text;
		}

		public readonly ReadOnlyMemory<char> Text;

		/// <inheritdoc />
		public override string ToString() => ToString(null);

		/// <inheritdoc />
		public string ToString(string? format, IFormatProvider? formatProvider = null) => $"\"{this.Text.Span}\""; //TODO: escape?

		/// <inheritdoc />
		public bool TryFormat(Span<char> destination, out int charsWritten, ReadOnlySpan<char> format, IFormatProvider? provider)
		{
			return destination.TryWrite($"\"{this.Text.Span}\"", out charsWritten); //TODO: escape?
		}

		/// <inheritdoc />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool TryGetSpan(out ReadOnlySpan<byte> span) => SpanEncoders.Utf16Encoder.TryGetSpan(in this.Text, out span);

		/// <inheritdoc />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool TryGetSizeHint(out int sizeHint) => SpanEncoders.Utf16Encoder.TryGetSizeHint(in this.Text, out sizeHint);

		/// <inheritdoc />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool TryEncode(scoped Span<byte> destination, out int bytesWritten) => SpanEncoders.Utf16Encoder.TryEncode(destination, out bytesWritten, in this.Text);

	}

	/// <summary>Value that wraps text that is encoded as UTF-16 bytes</summary>
	public readonly ref struct FdbUtf16SpanValue : IFdbValue
	{

		[SkipLocalsInit, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public FdbUtf16SpanValue(ReadOnlySpan<char> text)
		{
			this.Text = text;
		}

		public readonly ReadOnlySpan<char> Text;

		/// <inheritdoc />
		public override string ToString() => ToString(null);

		/// <inheritdoc />
		public string ToString(string? format, IFormatProvider? formatProvider = null) => $"\"{this.Text}\""; //TODO: escape?

		/// <inheritdoc />
		public bool TryFormat(Span<char> destination, out int charsWritten, ReadOnlySpan<char> format, IFormatProvider? provider)
		{
			return destination.TryWrite($"\"{this.Text}\"", out charsWritten); //TODO: escape?
		}

		/// <inheritdoc />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool TryGetSpan(out ReadOnlySpan<byte> span) => SpanEncoders.Utf16Encoder.TryGetSpan(in this.Text, out span);

		/// <inheritdoc />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool TryGetSizeHint(out int sizeHint) => SpanEncoders.Utf16Encoder.TryGetSizeHint(in this.Text, out sizeHint);

		/// <inheritdoc />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool TryEncode(scoped Span<byte> destination, out int bytesWritten) => SpanEncoders.Utf16Encoder.TryEncode(destination, out bytesWritten, in this.Text);

	}

	/// <summary>Value that will be converted into bytes by a <see cref="ISpanEncoder{TValue}"/></summary>
	/// <typeparam name="TValue">Type of the value</typeparam>
	/// <typeparam name="TEncoder">Type of the encoder for this value</typeparam>
	[DebuggerDisplay("Data={Data}")]
	public readonly struct FdbValue<TValue, TEncoder> : IFdbValue
		where TEncoder : struct, ISpanEncoder<TValue>
	{

		[SkipLocalsInit, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public FdbValue(TValue? data)
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

		/// <inheritdoc />
		public override string ToString()
			=> ToString(null);

		/// <inheritdoc />
		public string ToString(string? format, IFormatProvider? provider = null)
			=> STuple.Formatter.Stringify(this.Data);

		/// <inheritdoc />
		public bool TryFormat(Span<char> destination, out int charsWritten, ReadOnlySpan<char> format, IFormatProvider? provider)
			=> STuple.Formatter.TryStringifyTo(destination, out charsWritten, this.Data);

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

	#endregion

}
