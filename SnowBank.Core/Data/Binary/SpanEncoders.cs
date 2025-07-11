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

namespace SnowBank.Data.Binary
{
	using System;
	using System.Buffers;
	using System.Buffers.Binary;
	using System.Text;
	using SnowBank.Data.Tuples;

	/// <summary>Encoders that can encode primitive types into binary form</summary>
	public static class SpanEncoders
	{

		#region Encoding Helper Methods...

		/// <summary>Encodes a value, using a pool if necessary</summary>
		/// <typeparam name="TEncoder">Encoder used for this operation</typeparam>
		/// <typeparam name="TValue">Type of the encoded value</typeparam>
		/// <param name="value">Value to encode</param>
		/// <param name="pool">Pool used to allocate buffers</param>
		/// <param name="maxSize">If specified, maximum allowed size for the buffer. If 0, allows the buffer to grow up to the maximum size supported by the CLR.</param>
		/// <param name="result">Receives the span that points to the encoded result</param>
		/// <param name="buffer">Receives the buffer allocated from the pool to hold the result, or <c>null</c> if the result is already hosted somewhere else in memory.</param>
		/// <param name="range">Receives the range in <paramref name="buffer"/> that contains <paramref name="result"/> (only if buffer is not <c>null</c>)</param>
		/// <exception cref="ArgumentException">If the buffer size would exceed the maximum allowed size (or maximum limit supported by the CLR)</exception>
		/// <remarks>
		/// <para>The method will call <see cref="ISpanEncoder{TValue}.TryGetSpan"/> to extract any already encoded result.</para>
		/// <para>If no span could be extracted, then it will call <see cref="ISpanEncoder{TValue}.TryGetSizeHint"/>, and rent a buffer from the pool with a safe initial size.</para>
		/// <para>The method will call <see cref="ISpanEncoder{TValue}.TryEncode"/> repeatedly, with a larger and larger buffer, until it returns <c>true</c>, throws, or the buffer reaches the maximum allowed size.</para>
		/// </remarks>
		public static void Encode<TEncoder, TValue>(in TValue? value, ArrayPool<byte> pool, int maxSize, out ReadOnlySpan<byte> result, out byte[]? buffer, out Range range)
#if NET9_0_OR_GREATER
			where TValue : allows ref struct
#endif
			where TEncoder : ISpanEncoder<TValue>
		{
			if (TEncoder.TryGetSpan(value, out result))
			{
				buffer = null;
				range = default;
				return;
			}

			int size = TEncoder.TryGetSizeHint(value, out var keySizeHint) ? keySizeHint : 128;
			if (maxSize <= 0) maxSize = 1 << 30;

			byte[]? tmp = null;
			try
			{
				while (true)
				{
					tmp = pool.Rent(size);
					if (TEncoder.TryEncode(tmp, out int bytesWritten, in value))
					{
						if (bytesWritten == 0)
						{
							pool.Return(tmp);
							tmp = null;
							buffer = null;
							result = default;
							range = default;
							return;
						}

						buffer = tmp;
						result = tmp.AsSpan(0, bytesWritten);
						range = new(0, bytesWritten);
						tmp = null;
						break;
					}

					pool.Return(tmp);

					if (size >= maxSize)
					{
						// it would be too large anyway!
						throw new ArgumentException("Cannot format item because it would exceed the maximum allowed length.");
					}

					size *= 2;
				}
			}
			finally
			{
				if (tmp is not null)
				{
					buffer = null;
					result = default;
					range = default;
					pool.Return(tmp);
				}
			}
		}

		/// <summary>Returns a <see cref="Slice"/> that contains the encoded binary form of the value, using the specified encoder</summary>
		/// <typeparam name="TEncoder">Encoder used by the operation</typeparam>
		/// <typeparam name="TValue">Type of the encoded value</typeparam>
		/// <param name="value">Value to encode</param>
		/// <returns><see cref="Slice"/> that contains the encoded value.</returns>
		public static Slice ToSlice<TEncoder, TValue>(in TValue? value)
#if NET9_0_OR_GREATER
			where TValue : allows ref struct
#endif
			where TEncoder : ISpanEncoder<TValue>
		{
			var pool = ArrayPool<byte>.Shared;
			Encode<TEncoder, TValue>(in value, pool, 0, out var span, out var buffer, out _);
			var slice = span.ToSlice();
			if (buffer is not null)
			{
				pool.Return(buffer);
			}

			return slice;
		}


		/// <summary>Returns a <see cref="SliceOwner"/> that contains the encoded binary form of the value, using the specified encoder and a buffer rented from a pool.</summary>
		/// <typeparam name="TEncoder">Encoder used by the operation</typeparam>
		/// <typeparam name="TValue">Type of the encoded value</typeparam>
		/// <param name="value">Value to encode</param>
		/// <param name="pool">Pool used to rent buffers (use the shared pool if <c>null</c>)</param>
		/// <returns><see cref="SliceOwner"/> that contains the encoded value stored in buffer rented from the pool.</returns>
		/// <remarks>
		/// <para>The caller <b>MUST</b> dispose the result once done, otherwise the rented buffer will not be returned to the pool.</para>
		/// </remarks>
		public static SliceOwner ToSlice<TEncoder, TValue>(in TValue? value, ArrayPool<byte>? pool)
#if NET9_0_OR_GREATER
			where TValue : allows ref struct
#endif
			where TEncoder : ISpanEncoder<TValue>
		{
			pool ??= ArrayPool<byte>.Shared;

			Encode<TEncoder, TValue>(in value, pool, 0, out var span, out var buffer, out var range);

			return buffer is null
				? SliceOwner.Copy(span, pool)
				: SliceOwner.Create(buffer.AsSlice(range));
		}

		/// <summary>Writes a byte at the start of the destination buffer, if it is large enough.</summary>
		/// <param name="destination">Destination buffer</param>
		/// <param name="bytesWritten">Receives <c>1</c> if the operation was successful; otherwise, <c>0</c></param>
		/// <param name="byte1">Byte to write</param>
		/// <returns><c>true</c> if the buffer was large enough; otherwise, <c>false</c>.</returns>
		internal static bool TryWriteByte(Span<byte> destination, out int bytesWritten, byte byte1)
		{
			if (destination.Length < 1)
			{
				bytesWritten = 0;
				return false;
			}

			destination[0] = byte1;
			bytesWritten = 1;
			return true;
		}

		/// <summary>Writes two bytes at the start of the destination buffer, if it is large enough.</summary>
		/// <param name="destination">Destination buffer</param>
		/// <param name="bytesWritten">Receives <c>2</c> if the operation was successful; otherwise, <c>0</c></param>
		/// <param name="byte1">First byte to write</param>
		/// <param name="byte2">Second byte to write</param>
		/// <returns><c>true</c> if the buffer was large enough; otherwise, <c>false</c>.</returns>
		internal static bool TryWriteBytes(Span<byte> destination, out int bytesWritten, byte byte1, byte byte2)
		{
			if (destination.Length < 2)
			{
				bytesWritten = 0;
				return false;
			}

			destination[0] = byte1;
			destination[1] = byte2;
			bytesWritten = 2;
			return true;
		}

		/// <summary>Writes three bytes at the start of the destination buffer, if it is large enough.</summary>
		/// <param name="destination">Destination buffer</param>
		/// <param name="bytesWritten">Receives <c>3</c> if the operation was successful; otherwise, <c>0</c></param>
		/// <param name="byte1">First byte to write</param>
		/// <param name="byte2">Second byte to write</param>
		/// <param name="byte3">Third byte to write</param>
		/// <returns><c>true</c> if the buffer was large enough; otherwise, <c>false</c>.</returns>
		internal static bool TryWriteBytes(Span<byte> destination, out int bytesWritten, byte byte1, byte byte2, byte byte3)
		{
			if (destination.Length < 3)
			{
				bytesWritten = 0;
				return false;
			}

			destination[0] = byte1;
			destination[1] = byte2;
			destination[2] = byte3;
			bytesWritten = 3;
			return true;
		}

		/// <summary>Writes four bytes at the start of the destination buffer, if it is large enough.</summary>
		/// <param name="destination">Destination buffer</param>
		/// <param name="bytesWritten">Receives <c>4</c> if the operation was successful; otherwise, <c>0</c></param>
		/// <param name="byte1">First byte to write</param>
		/// <param name="byte2">Second byte to write</param>
		/// <param name="byte3">Third byte to write</param>
		/// <param name="byte4">Third byte to write</param>
		/// <returns><c>true</c> if the buffer was large enough; otherwise, <c>false</c>.</returns>
		internal static bool TryWriteBytes(Span<byte> destination, out int bytesWritten, byte byte1, byte byte2, byte byte3, byte byte4)
		{
			if (destination.Length < 4)
			{
				bytesWritten = 0;
				return false;
			}

			destination[0] = byte1;
			destination[1] = byte2;
			destination[2] = byte3;
			destination[3] = byte4;
			bytesWritten = 4;
			return true;
		}

		/// <summary>Writes a span of bytes at the start of the destination buffer, if it is large enough.</summary>
		/// <param name="destination">Destination buffer</param>
		/// <param name="bytesWritten">Receives the length of <paramref name="bytes"/> if the operation was successful; otherwise, <c>0</c></param>
		/// <param name="bytes">Span of bytes to write</param>
		/// <returns><c>true</c> if the buffer was large enough; otherwise, <c>false</c>.</returns>
		internal static bool TryWriteBytes(Span<byte> destination, out int bytesWritten, ReadOnlySpan<byte> bytes)
		{
			if (!bytes.TryCopyTo(destination))
			{
				bytesWritten = 0;
				return false;
			}
			bytesWritten = bytes.Length;
			return true;
		}

		#endregion

		/// <summary>Encodes raw binary values (<see cref="Slice"/>, spans or arrays of bytes, ...)</summary>
		public readonly struct RawEncoder : ISpanEncoder<Slice>, ISpanEncoder<byte[]>, ISpanEncoder<ReadOnlyMemory<byte>>, ISpanEncoder<MemoryStream>
#if NET9_0_OR_GREATER
			, ISpanEncoder<ReadOnlySpan<byte>>
#endif
		{

			#region Slice...

			/// <inheritdoc />
			public static bool TryGetSpan(scoped in Slice value, out ReadOnlySpan<byte> span)
			{
				span = value.Span;
				return true;
			}

			/// <inheritdoc />
			public static bool TryGetSizeHint(in Slice value, out int sizeHint)
			{
				sizeHint = value.Count;
				return true;
			}

			/// <inheritdoc />
			public static bool TryEncode(Span<byte> destination, out int bytesWritten, in Slice value)
			{
				return value.TryCopyTo(destination, out bytesWritten);
			}

			#endregion

			#region ReadOnlySpan<byte>...

			/// <inheritdoc />
			public static bool TryGetSpan(scoped in ReadOnlySpan<byte> value, out ReadOnlySpan<byte> span)
			{
				span = value;
				return true;
			}

			/// <inheritdoc />
			public static bool TryGetSizeHint(in ReadOnlySpan<byte> value, out int sizeHint)
			{
				sizeHint = value.Length;
				return true;
			}

			/// <inheritdoc />
			public static bool TryEncode(Span<byte> destination, out int bytesWritten, in ReadOnlySpan<byte> value)
			{
				return value.TryCopyTo(destination, out bytesWritten);
			}

			#endregion

			#region ReadOnlyMemory<byte>...

			/// <inheritdoc />
			public static bool TryGetSpan(scoped in ReadOnlyMemory<byte> value, out ReadOnlySpan<byte> span)
			{
				span = value.Span;
				return true;
			}

			/// <inheritdoc />
			public static bool TryGetSizeHint(in ReadOnlyMemory<byte> value, out int sizeHint)
			{
				sizeHint = value.Length;
				return true;
			}

			/// <inheritdoc />
			public static bool TryEncode(Span<byte> destination, out int bytesWritten, in ReadOnlyMemory<byte> value)
			{
				return value.Span.TryCopyTo(destination, out bytesWritten);
			}

			#endregion

			#region byte[]...

			/// <inheritdoc />
			public static bool TryGetSpan(scoped in byte[]? value, out ReadOnlySpan<byte> span)
			{
				span = value;
				return true;
			}

			/// <inheritdoc />
			public static bool TryGetSizeHint(in byte[]? value, out int sizeHint)
			{
				sizeHint = value?.Length ?? 0;
				return true;
			}

			/// <inheritdoc />
			public static bool TryEncode(Span<byte> destination, out int bytesWritten, in byte[]? value)
			{
				if (value is null || value.Length == 0)
				{
					bytesWritten = 0;
					return true;
				}
				return value.TryCopyTo(destination, out bytesWritten);
			}

			#endregion

			#region MemoryStream...

			/// <inheritdoc />
			public static bool TryGetSpan(scoped in MemoryStream? value, out ReadOnlySpan<byte> span)
			{
				if (value is null)
				{
					span = default;
					return true;
				}

				if (value.TryGetBuffer(out var buffer))
				{
					span = buffer.Array.AsSpan(buffer.Offset, buffer.Count);
					return true;
				}

				span = default;
				return false;
			}

			/// <inheritdoc />
			public static bool TryGetSizeHint(in MemoryStream? value, out int sizeHint)
			{
				if (value is null)
				{
					sizeHint = 0;
					return true;
				}

				if (value.TryGetBuffer(out var buffer))
				{
					sizeHint = buffer.Count;
					return true;
				}

				sizeHint = 0;
				return false;
			}

			/// <inheritdoc />
			public static bool TryEncode(Span<byte> destination, out int bytesWritten, in MemoryStream? value)
			{
				if (value is null)
				{
					bytesWritten = 0;
					return true;
				}

				long longLength = value.Length;
				if (longLength > int.MaxValue)
				{ // this should never happen, but if it does, we have to throw!
					throw new InvalidOperationException("Stream cannot exceed 2GiB.");
				}

				int length = unchecked((int) longLength);
				if (destination.Length < length)
				{
					bytesWritten = 0;
					return false;
				}

				if (value.TryGetBuffer(out var buffer))
				{
					buffer.Array.AsSpan(buffer.Offset, buffer.Count).CopyTo(destination);
				}
				else
				{
					var remaining = destination[..length];
					while (remaining.Length > 0)
					{
						int n = value.Read(remaining);
						if (n == 0) throw new InvalidOperationException("Failed to read the stream.");
						remaining = remaining[n..];
					}
				}

				bytesWritten = length;
				return true;
			}
			#endregion

		}

		/// <summary>Encodes tuples using the Tuple Layer Encoding</summary>
		/// <typeparam name="TTuple">Type of the tuple to encode (must implement <see cref="IVarTuple"/>)</typeparam>
		/// <seealso cref="TuPack"/>
		public readonly struct TupleEncoder<TTuple> : ISpanEncoder<TTuple>
			where TTuple : IVarTuple
		{

			#region IVarTuple...

			/// <inheritdoc />
			public static bool TryGetSpan(scoped in TTuple? value, out ReadOnlySpan<byte> span)
			{
				span = default;
				return false;
			}

			/// <inheritdoc />
			public static bool TryGetSizeHint(in TTuple? value, out int sizeHint)
			{
				sizeHint = 0;
				return false;
			}

			/// <inheritdoc />
			public static bool TryEncode(Span<byte> destination, out int bytesWritten, in TTuple? value)
			{
				if (value is null or STuple)
				{
					bytesWritten = 0;
					return true;
				}

				return TuPack.TryPackTo(destination, out bytesWritten, in value);
			}

			#endregion

			//TODO: maybe support ValueTuple<...>?
		}

		/// <summary>Encodes strings as UTF-8 bytes</summary>
		public readonly struct Utf8Encoder : ISpanEncoder<string>, ISpanEncoder<StringBuilder>, ISpanEncoder<ReadOnlyMemory<char>>
#if NET9_0_OR_GREATER
			, ISpanEncoder<ReadOnlySpan<char>>
#endif
		{

			#region String...

			/// <inheritdoc />
			public static bool TryGetSpan(scoped in string? value, out ReadOnlySpan<byte> span)
			{
				span = default;
				return string.IsNullOrEmpty(value);
			}

			/// <inheritdoc />
			public static bool TryGetSizeHint(in string? value, out int sizeHint)
			{
				sizeHint = Encoding.UTF8.GetByteCount(value ?? "");
				return true;
			}

			/// <inheritdoc />
			public static bool TryEncode(Span<byte> destination, out int bytesWritten, in string? value)
			{
				return Encoding.UTF8.TryGetBytes(value, destination, out bytesWritten);
			}

			#endregion

			#region ReadOnlySpan<char>...

			/// <inheritdoc />
			public static bool TryGetSpan(scoped in ReadOnlySpan<char> value, out ReadOnlySpan<byte> span)
			{
				span = default;
				return value.Length == 0;
			}

			/// <inheritdoc />
			public static bool TryGetSizeHint(in ReadOnlySpan<char> value, out int sizeHint)
			{
				sizeHint = Encoding.UTF8.GetByteCount(value);
				return true;
			}

			/// <inheritdoc />
			public static bool TryEncode(Span<byte> destination, out int bytesWritten, in ReadOnlySpan<char> value)
			{
				return Encoding.UTF8.TryGetBytes(value, destination, out bytesWritten);
			}

			#endregion

			#region ReadOnlyMemory<char>...

			/// <inheritdoc />
			public static bool TryGetSpan(scoped in ReadOnlyMemory<char> value, out ReadOnlySpan<byte> span)
			{
				span = default;
				return value.Length == 0;
			}

			/// <inheritdoc />
			public static bool TryGetSizeHint(in ReadOnlyMemory<char> value, out int sizeHint)
			{
				sizeHint = Encoding.UTF8.GetByteCount(value.Span);
				return true;
			}

			/// <inheritdoc />
			public static bool TryEncode(Span<byte> destination, out int bytesWritten, in ReadOnlyMemory<char> value)
			{
				return Encoding.UTF8.TryGetBytes(value.Span, destination, out bytesWritten);
			}

			#endregion

			#region StringBuilder...

			/// <inheritdoc />
			public static bool TryGetSpan(scoped in StringBuilder? value, out ReadOnlySpan<byte> span)
			{
				span = default;
				return value is null || value.Length == 0;
			}

			/// <inheritdoc />
			public static bool TryGetSizeHint(in StringBuilder? value, out int sizeHint)
			{
				sizeHint = Encoding.UTF8.GetMaxByteCount(value?.Length ?? 0);
				return true;
			}

			/// <inheritdoc />
			public static bool TryEncode(Span<byte> destination, out int bytesWritten, in StringBuilder? value)
			{
				if (value is null || value.Length == 0)
				{
					bytesWritten = 0;
					return true;
				}

				int cursor = 0;
				foreach (var chunk in value.GetChunks())
				{
					if (!Encoding.UTF8.TryGetBytes(chunk.Span, destination[cursor..], out int len))
					{
						bytesWritten = 0;
						return false;
					}
					cursor = checked(cursor + len);
				}

				bytesWritten = cursor;
				return true;
			}

			#endregion

		}

		/// <summary>Encodes primitive integers into little-endian, using a fixed size</summary>
		public readonly struct FixedSizeLittleEndianEncoder : ISpanEncoder<int>, ISpanEncoder<uint>, ISpanEncoder<long>, ISpanEncoder<ulong>, ISpanEncoder<float>, ISpanEncoder<double>, ISpanEncoder<Half>
		{

			#region Int32...

			/// <inheritdoc />
			public static bool TryGetSpan(scoped in int value, out ReadOnlySpan<byte> span)
			{
				span = default;
				return false;
			}

			/// <inheritdoc />
			public static bool TryGetSizeHint(in int value, out int sizeHint)
			{
				sizeHint = Unsafe.SizeOf<int>();
				return true;
			}

			/// <inheritdoc />
			public static bool TryEncode(Span<byte> destination, out int bytesWritten, in int value)
			{
				if (destination.Length < Unsafe.SizeOf<int>())
				{
					bytesWritten = 0;
					return false;
				}

				BinaryPrimitives.WriteInt32LittleEndian(destination, value);
				bytesWritten = Unsafe.SizeOf<int>();
				return true;
			}

			#endregion

			#region UInt32...

			/// <inheritdoc />
			public static bool TryGetSpan(scoped in uint value, out ReadOnlySpan<byte> span)
			{
				span = default;
				return false;
			}

			/// <inheritdoc />
			public static bool TryGetSizeHint(in uint value, out int sizeHint)
			{
				sizeHint = Unsafe.SizeOf<uint>();
				return true;
			}

			/// <inheritdoc />
			public static bool TryEncode(Span<byte> destination, out int bytesWritten, in uint value)
			{
				if (destination.Length < Unsafe.SizeOf<uint>())
				{
					bytesWritten = 0;
					return false;
				}

				BinaryPrimitives.WriteUInt32LittleEndian(destination, value);
				bytesWritten = Unsafe.SizeOf<uint>();
				return true;
			}

			#endregion

			#region Int64...

			/// <inheritdoc />
			public static bool TryGetSpan(scoped in long value, out ReadOnlySpan<byte> span)
			{
				span = default;
				return false;
			}

			/// <inheritdoc />
			public static bool TryGetSizeHint(in long value, out int sizeHint)
			{
				sizeHint = Unsafe.SizeOf<long>();
				return true;
			}

			/// <inheritdoc />
			public static bool TryEncode(Span<byte> destination, out int bytesWritten, in long value)
			{
				if (destination.Length < Unsafe.SizeOf<long>())
				{
					bytesWritten = 0;
					return false;
				}

				BinaryPrimitives.WriteInt64LittleEndian(destination, value);
				bytesWritten = Unsafe.SizeOf<long>();
				return true;
			}

			#endregion

			#region UInt64...

			/// <inheritdoc />
			public static bool TryGetSpan(scoped in ulong value, out ReadOnlySpan<byte> span)
			{
				span = default;
				return false;
			}

			/// <inheritdoc />
			public static bool TryGetSizeHint(in ulong value, out int sizeHint)
			{
				sizeHint = Unsafe.SizeOf<ulong>();
				return true;
			}

			/// <inheritdoc />
			public static bool TryEncode(Span<byte> destination, out int bytesWritten, in ulong value)
			{
				if (destination.Length < Unsafe.SizeOf<ulong>())
				{
					bytesWritten = 0;
					return false;
				}

				BinaryPrimitives.WriteUInt64LittleEndian(destination, value);
				bytesWritten = Unsafe.SizeOf<ulong>();
				return true;
			}

			#endregion

			#region Single...

			/// <inheritdoc />
			public static bool TryGetSpan(scoped in float value, out ReadOnlySpan<byte> span)
			{
				span = default;
				return false;
			}

			/// <inheritdoc />
			public static bool TryGetSizeHint(in float value, out int sizeHint)
			{
				sizeHint = Unsafe.SizeOf<float>();
				return true;
			}

			/// <inheritdoc />
			public static bool TryEncode(Span<byte> destination, out int bytesWritten, in float value)
			{
				if (destination.Length < Unsafe.SizeOf<float>())
				{
					bytesWritten = 0;
					return false;
				}

				BinaryPrimitives.WriteSingleLittleEndian(destination, value);
				bytesWritten = Unsafe.SizeOf<float>();
				return true;
			}

			#endregion

			#region Double...

			/// <inheritdoc />
			public static bool TryGetSpan(scoped in double value, out ReadOnlySpan<byte> span)
			{
				span = default;
				return false;
			}

			/// <inheritdoc />
			public static bool TryGetSizeHint(in double value, out int sizeHint)
			{
				sizeHint = Unsafe.SizeOf<double>();
				return true;
			}

			/// <inheritdoc />
			public static bool TryEncode(Span<byte> destination, out int bytesWritten, in double value)
			{
				if (destination.Length < Unsafe.SizeOf<double>())
				{
					bytesWritten = 0;
					return false;
				}

				BinaryPrimitives.WriteDoubleLittleEndian(destination, value);
				bytesWritten = Unsafe.SizeOf<double>();
				return true;
			}

			#endregion

			#region Half...

			/// <inheritdoc />
			public static bool TryGetSpan(scoped in Half value, out ReadOnlySpan<byte> span)
			{
				span = default;
				return false;
			}

			/// <inheritdoc />
			public static bool TryGetSizeHint(in Half value, out int sizeHint)
			{
				sizeHint = Unsafe.SizeOf<Half>();
				return true;
			}

			/// <inheritdoc />
			public static bool TryEncode(Span<byte> destination, out int bytesWritten, in Half value)
			{
				if (destination.Length < Unsafe.SizeOf<Half>())
				{
					bytesWritten = 0;
					return false;
				}

				BinaryPrimitives.WriteHalfLittleEndian(destination, value);
				bytesWritten = Unsafe.SizeOf<Half>();
				return true;
			}

			#endregion

		}

		/// <summary>Encodes primitive integers into big-endian, using a fixed size</summary>
		public readonly struct FixedSizeBigEndianEncoder : ISpanEncoder<int>, ISpanEncoder<uint>, ISpanEncoder<long>, ISpanEncoder<ulong>, ISpanEncoder<float>, ISpanEncoder<double>, ISpanEncoder<Half>
		{

			#region Int32...

			/// <inheritdoc />
			public static bool TryGetSpan(scoped in int value, out ReadOnlySpan<byte> span)
			{
				span = default;
				return false;
			}

			/// <inheritdoc />
			public static bool TryGetSizeHint(in int value, out int sizeHint)
			{
				sizeHint = Unsafe.SizeOf<int>();
				return true;
			}

			/// <inheritdoc />
			public static bool TryEncode(Span<byte> destination, out int bytesWritten, in int value)
			{
				if (destination.Length < Unsafe.SizeOf<int>())
				{
					bytesWritten = 0;
					return false;
				}

				BinaryPrimitives.WriteInt32BigEndian(destination, value);
				bytesWritten = Unsafe.SizeOf<int>();
				return true;
			}

			#endregion

			#region UInt32...

			/// <inheritdoc />
			public static bool TryGetSpan(scoped in uint value, out ReadOnlySpan<byte> span)
			{
				span = default;
				return false;
			}

			/// <inheritdoc />
			public static bool TryGetSizeHint(in uint value, out int sizeHint)
			{
				sizeHint = Unsafe.SizeOf<uint>();
				return true;
			}

			/// <inheritdoc />
			public static bool TryEncode(Span<byte> destination, out int bytesWritten, in uint value)
			{
				if (destination.Length < Unsafe.SizeOf<uint>())
				{
					bytesWritten = 0;
					return false;
				}

				BinaryPrimitives.WriteUInt32BigEndian(destination, value);
				bytesWritten = Unsafe.SizeOf<uint>();
				return true;
			}

			#endregion

			#region Int64...

			/// <inheritdoc />
			public static bool TryGetSpan(scoped in long value, out ReadOnlySpan<byte> span)
			{
				span = default;
				return false;
			}

			/// <inheritdoc />
			public static bool TryGetSizeHint(in long value, out int sizeHint)
			{
				sizeHint = Unsafe.SizeOf<long>();
				return true;
			}

			/// <inheritdoc />
			public static bool TryEncode(Span<byte> destination, out int bytesWritten, in long value)
			{
				if (destination.Length < Unsafe.SizeOf<long>())
				{
					bytesWritten = 0;
					return false;
				}

				BinaryPrimitives.WriteInt64BigEndian(destination, value);
				bytesWritten = Unsafe.SizeOf<long>();
				return true;
			}

			#endregion

			#region UInt64...

			/// <inheritdoc />
			public static bool TryGetSpan(scoped in ulong value, out ReadOnlySpan<byte> span)
			{
				span = default;
				return false;
			}

			/// <inheritdoc />
			public static bool TryGetSizeHint(in ulong value, out int sizeHint)
			{
				sizeHint = Unsafe.SizeOf<ulong>();
				return true;
			}

			/// <inheritdoc />
			public static bool TryEncode(Span<byte> destination, out int bytesWritten, in ulong value)
			{
				if (destination.Length < Unsafe.SizeOf<ulong>())
				{
					bytesWritten = 0;
					return false;
				}

				BinaryPrimitives.WriteUInt64BigEndian(destination, value);
				bytesWritten = Unsafe.SizeOf<ulong>();
				return true;
			}

			#endregion

			#region Single...

			/// <inheritdoc />
			public static bool TryGetSpan(scoped in float value, out ReadOnlySpan<byte> span)
			{
				span = default;
				return false;
			}

			/// <inheritdoc />
			public static bool TryGetSizeHint(in float value, out int sizeHint)
			{
				sizeHint = Unsafe.SizeOf<float>();
				return true;
			}

			/// <inheritdoc />
			public static bool TryEncode(Span<byte> destination, out int bytesWritten, in float value)
			{
				if (destination.Length < Unsafe.SizeOf<float>())
				{
					bytesWritten = 0;
					return false;
				}

				BinaryPrimitives.WriteSingleBigEndian(destination, value);
				bytesWritten = Unsafe.SizeOf<float>();
				return true;
			}

			#endregion

			#region Double...

			/// <inheritdoc />
			public static bool TryGetSpan(scoped in double value, out ReadOnlySpan<byte> span)
			{
				span = default;
				return false;
			}

			/// <inheritdoc />
			public static bool TryGetSizeHint(in double value, out int sizeHint)
			{
				sizeHint = Unsafe.SizeOf<double>();
				return true;
			}

			/// <inheritdoc />
			public static bool TryEncode(Span<byte> destination, out int bytesWritten, in double value)
			{
				if (destination.Length < Unsafe.SizeOf<double>())
				{
					bytesWritten = 0;
					return false;
				}

				BinaryPrimitives.WriteDoubleBigEndian(destination, value);
				bytesWritten = Unsafe.SizeOf<double>();
				return true;
			}

			#endregion

			#region Half...

			/// <inheritdoc />
			public static bool TryGetSpan(scoped in Half value, out ReadOnlySpan<byte> span)
			{
				span = default;
				return false;
			}

			/// <inheritdoc />
			public static bool TryGetSizeHint(in Half value, out int sizeHint)
			{
				sizeHint = Unsafe.SizeOf<Half>();
				return true;
			}

			/// <inheritdoc />
			public static bool TryEncode(Span<byte> destination, out int bytesWritten, in Half value)
			{
				if (destination.Length < Unsafe.SizeOf<Half>())
				{
					bytesWritten = 0;
					return false;
				}

				BinaryPrimitives.WriteHalfBigEndian(destination, value);
				bytesWritten = Unsafe.SizeOf<Half>();
				return true;
			}

			#endregion

		}

		/// <summary>Encodes UUID values into big-endian, using a fixed size</summary>
		public readonly struct FixedSizeUuidEncoder : ISpanEncoder<Guid>, ISpanEncoder<Uuid128>, ISpanEncoder<Uuid96>, ISpanEncoder<Uuid80>, ISpanEncoder<Uuid64>, ISpanEncoder<Uuid48>, ISpanEncoder<VersionStamp>
		{

			#region Guid...

			/// <inheritdoc />
			public static bool TryGetSpan(scoped in Guid value, out ReadOnlySpan<byte> span)
			{
				span = default;
				return false;
			}

			/// <inheritdoc />
			public static bool TryGetSizeHint(in Guid value, out int sizeHint)
			{
				sizeHint = Unsafe.SizeOf<Guid>();
				return true;
			}

			/// <inheritdoc />
			public static bool TryEncode(Span<byte> destination, out int bytesWritten, in Guid value)
			{
				return new Uuid128(value).TryWriteTo(destination, out bytesWritten);
			}

			#endregion

			#region Uuid128...

			/// <inheritdoc />
			public static bool TryGetSpan(scoped in Uuid128 value, out ReadOnlySpan<byte> span)
			{
				span = default;
				return false;
			}

			/// <inheritdoc />
			public static bool TryGetSizeHint(in Uuid128 value, out int sizeHint)
			{
				sizeHint = Uuid128.SizeOf;
				return true;
			}

			/// <inheritdoc />
			public static bool TryEncode(Span<byte> destination, out int bytesWritten, in Uuid128 value)
			{
				return value.TryWriteTo(destination, out bytesWritten);
			}

			#endregion

			#region Uuid96...

			/// <inheritdoc />
			public static bool TryGetSpan(scoped in Uuid96 value, out ReadOnlySpan<byte> span)
			{
				span = default;
				return false;
			}

			/// <inheritdoc />
			public static bool TryGetSizeHint(in Uuid96 value, out int sizeHint)
			{
				sizeHint = Uuid96.SizeOf;
				return true;
			}

			/// <inheritdoc />
			public static bool TryEncode(Span<byte> destination, out int bytesWritten, in Uuid96 value)
			{
				return value.TryWriteTo(destination, out bytesWritten);
			}

			#endregion

			#region Uuid80...

			/// <inheritdoc />
			public static bool TryGetSpan(scoped in Uuid80 value, out ReadOnlySpan<byte> span)
			{
				span = default;
				return false;
			}

			/// <inheritdoc />
			public static bool TryGetSizeHint(in Uuid80 value, out int sizeHint)
			{
				sizeHint = Uuid80.SizeOf;
				return true;
			}

			/// <inheritdoc />
			public static bool TryEncode(Span<byte> destination, out int bytesWritten, in Uuid80 value)
			{
				return value.TryWriteTo(destination, out bytesWritten);
			}

			#endregion

			#region Uuid64...

			/// <inheritdoc />
			public static bool TryGetSpan(scoped in Uuid64 value, out ReadOnlySpan<byte> span)
			{
				span = default;
				return false;
			}

			/// <inheritdoc />
			public static bool TryGetSizeHint(in Uuid64 value, out int sizeHint)
			{
				sizeHint = Uuid64.SizeOf;
				return true;
			}

			/// <inheritdoc />
			public static bool TryEncode(Span<byte> destination, out int bytesWritten, in Uuid64 value)
			{
				return value.TryWriteTo(destination, out bytesWritten);
			}

			#endregion

			#region Uuid48...

			/// <inheritdoc />
			public static bool TryGetSpan(scoped in Uuid48 value, out ReadOnlySpan<byte> span)
			{
				span = default;
				return false;
			}

			/// <inheritdoc />
			public static bool TryGetSizeHint(in Uuid48 value, out int sizeHint)
			{
				sizeHint = Uuid48.SizeOf;
				return true;
			}

			/// <inheritdoc />
			public static bool TryEncode(Span<byte> destination, out int bytesWritten, in Uuid48 value)
			{
				return value.TryWriteTo(destination, out bytesWritten);
			}

			#endregion

			#region VersionStamp...

			/// <inheritdoc />
			public static bool TryGetSpan(scoped in VersionStamp value, out ReadOnlySpan<byte> span)
			{
				span = default;
				return false;
			}

			/// <inheritdoc />
			public static bool TryGetSizeHint(in VersionStamp value, out int sizeHint)
			{
				sizeHint = value.HasUserVersion ? 12 : 10;
				return true;
			}

			/// <inheritdoc />
			public static bool TryEncode(Span<byte> destination, out int bytesWritten, in VersionStamp value)
			{
				return value.TryWriteTo(destination, out bytesWritten);
			}

			#endregion

		}

		/// <summary>Encodes primitive integers into little-endian, using as few bytes as possible</summary>
		public readonly struct CompactLittleEndianEncoder : ISpanEncoder<short>, ISpanEncoder<ushort>, ISpanEncoder<int>, ISpanEncoder<uint>, ISpanEncoder<long>, ISpanEncoder<ulong>
		{

			#region Int16...

			/// <inheritdoc />
			public static bool TryGetSpan(scoped in short value, out ReadOnlySpan<byte> span)
			{
				span = default;
				return false;
			}

			/// <inheritdoc />
			public static bool TryGetSizeHint(in short value, out int sizeHint)
			{
				sizeHint = Unsafe.SizeOf<short>();
				return true;
			}

			/// <inheritdoc />
			public static bool TryEncode(Span<byte> destination, out int bytesWritten, in short value)
			{
				//note: negative values will be encoded as 4 bytes
				return TryEncode(destination, out bytesWritten, unchecked((ushort) value));
			}

			#endregion

			#region UInt16...

			/// <inheritdoc />
			public static bool TryGetSpan(scoped in ushort value, out ReadOnlySpan<byte> span)
			{
				span = default;
				return false;
			}

			/// <inheritdoc />
			public static bool TryGetSizeHint(in ushort value, out int sizeHint)
			{
				sizeHint = Unsafe.SizeOf<ushort>();
				return true;
			}

			/// <inheritdoc />
			public static bool TryEncode(Span<byte> destination, out int bytesWritten, in ushort value)
			{
				switch (value)
				{
					case <= 0xFF:
					{
						if (destination.Length < 1) goto too_small;
						destination[0] = unchecked((byte) value);
						bytesWritten = 1;
						return true;
					}
					default:
					{
						if (destination.Length < 2) goto too_small;
						BinaryPrimitives.WriteUInt16LittleEndian(destination, value);
						bytesWritten = 2;
						return true;
					}
				}

			too_small:
				bytesWritten = 0;
				return false;
			}

			#endregion

			#region Int32...

			/// <inheritdoc />
			public static bool TryGetSpan(scoped in int value, out ReadOnlySpan<byte> span)
			{
				span = default;
				return false;
			}

			/// <inheritdoc />
			public static bool TryGetSizeHint(in int value, out int sizeHint)
			{
				sizeHint = Unsafe.SizeOf<int>();
				return true;
			}

			/// <inheritdoc />
			public static bool TryEncode(Span<byte> destination, out int bytesWritten, in int value)
			{
				//note: negative values will be encoded as 4 bytes
				return TryEncode(destination, out bytesWritten, unchecked((uint) value));
			}

			#endregion

			#region UInt32...

			/// <inheritdoc />
			public static bool TryGetSpan(scoped in uint value, out ReadOnlySpan<byte> span)
			{
				span = default;
				return false;
			}

			/// <inheritdoc />
			public static bool TryGetSizeHint(in uint value, out int sizeHint)
			{
				sizeHint = Unsafe.SizeOf<uint>();
				return true;
			}

			/// <inheritdoc />
			public static bool TryEncode(Span<byte> destination, out int bytesWritten, in uint value)
			{
				switch (value)
				{
					case <= 0xFF:
					{
						if (destination.Length < 1) goto too_small;
						destination[0] = unchecked((byte) value);
						bytesWritten = 1;
						return true;
					}
					case <= 0xFFFF:
					{
						if (destination.Length < 2) goto too_small;
						destination[0] = unchecked((byte) value);
						destination[1] = (byte) (value >> 8);
						bytesWritten = 2;
						return true;
					}
					case <= 0xFFFFFF:
					{
						if (destination.Length < 3) goto too_small;
						destination[0] = unchecked((byte) value);
						destination[1] = unchecked((byte) (value >> 8));
						destination[2] = unchecked((byte) (value >> 16));
						bytesWritten = 3;
						return true;
					}
					default:
					{
						if (destination.Length < 4) goto too_small;
						BinaryPrimitives.WriteUInt32LittleEndian(destination, value);
						bytesWritten = 4;
						return true;
					}
				}

			too_small:
				bytesWritten = 0;
				return false;
			}

			#endregion

			#region Int64...

			/// <inheritdoc />
			public static bool TryGetSpan(scoped in long value, out ReadOnlySpan<byte> span)
			{
				span = default;
				return false;
			}

			/// <inheritdoc />
			public static bool TryGetSizeHint(in long value, out int sizeHint)
			{
				sizeHint = Unsafe.SizeOf<long>();
				return true;
			}

			/// <inheritdoc />
			public static bool TryEncode(Span<byte> destination, out int bytesWritten, in long value)
			{
				//note: negative values will be written as 8 bytes
				return TryEncode(destination, out bytesWritten, unchecked((ulong) value));
			}

			#endregion

			#region UInt64...

			/// <inheritdoc />
			public static bool TryGetSpan(scoped in ulong value, out ReadOnlySpan<byte> span)
			{
				span = default;
				return false;
			}

			/// <inheritdoc />
			public static bool TryGetSizeHint(in ulong value, out int sizeHint)
			{
				sizeHint = Unsafe.SizeOf<ulong>();
				return true;
			}

			/// <inheritdoc />
			public static bool TryEncode(Span<byte> destination, out int bytesWritten, in ulong value)
			{
				if (value <= uint.MaxValue)
				{ // smaller than 32 bits
					return TryEncode(destination, out bytesWritten, unchecked((uint) value));
				}

				// first write the 32 lower bits
				if (destination.Length < 5)
				{
					goto too_small;
				}
				BinaryPrimitives.WriteUInt32LittleEndian(destination, unchecked((uint) value));

				// then write the 32 upper bits
				if (!TryEncode(destination[4..], out int len, (uint) (value >> 32)))
				{
					goto too_small;
				}

				bytesWritten = 4 + len;
				return true;

			too_small:
				bytesWritten = 0;
				return false;
			}

			#endregion

		}

		/// <summary>Encodes primitive integers into little-endian, using as few bytes as possible</summary>
		public readonly struct CompactBigEndianEncoder : ISpanEncoder<short>, ISpanEncoder<ushort>, ISpanEncoder<int>, ISpanEncoder<uint>, ISpanEncoder<long>, ISpanEncoder<ulong>
		{

			#region Int16...

			/// <inheritdoc />
			public static bool TryGetSpan(scoped in short value, out ReadOnlySpan<byte> span)
			{
				span = default;
				return false;
			}

			/// <inheritdoc />
			public static bool TryGetSizeHint(in short value, out int sizeHint)
			{
				sizeHint = Unsafe.SizeOf<short>();
				return true;
			}

			/// <inheritdoc />
			public static bool TryEncode(Span<byte> destination, out int bytesWritten, in short value)
			{
				//note: negative values will be encoded as 4 bytes
				return TryEncode(destination, out bytesWritten, unchecked((ushort) value));
			}

			#endregion

			#region UInt16...

			/// <inheritdoc />
			public static bool TryGetSpan(scoped in ushort value, out ReadOnlySpan<byte> span)
			{
				span = default;
				return false;
			}

			/// <inheritdoc />
			public static bool TryGetSizeHint(in ushort value, out int sizeHint)
			{
				sizeHint = Unsafe.SizeOf<ushort>();
				return true;
			}

			/// <inheritdoc />
			public static bool TryEncode(Span<byte> destination, out int bytesWritten, in ushort value)
			{
				switch (value)
				{
					case <= 0xFF:
					{
						if (destination.Length < 1) goto too_small;
						destination[0] = unchecked((byte) value);
						bytesWritten = 1;
						return true;
					}
					default:
					{
						if (destination.Length < 2) goto too_small;
						BinaryPrimitives.WriteUInt16BigEndian(destination, value);
						bytesWritten = 2;
						return true;
					}
				}

			too_small:
				bytesWritten = 0;
				return false;
			}

			#endregion

			#region Int32...

			/// <inheritdoc />
			public static bool TryGetSpan(scoped in int value, out ReadOnlySpan<byte> span)
			{
				span = default;
				return false;
			}

			/// <inheritdoc />
			public static bool TryGetSizeHint(in int value, out int sizeHint)
			{
				sizeHint = Unsafe.SizeOf<int>();
				return true;
			}

			/// <inheritdoc />
			public static bool TryEncode(Span<byte> destination, out int bytesWritten, in int value)
			{
				//note: negative values will be encoded as 4 bytes
				return TryEncode(destination, out bytesWritten, unchecked((uint) value));
			}

			#endregion

			#region UInt32...

			/// <inheritdoc />
			public static bool TryGetSpan(scoped in uint value, out ReadOnlySpan<byte> span)
			{
				span = default;
				return false;
			}

			/// <inheritdoc />
			public static bool TryGetSizeHint(in uint value, out int sizeHint)
			{
				sizeHint = Unsafe.SizeOf<uint>();
				return true;
			}

			/// <inheritdoc />
			public static bool TryEncode(Span<byte> destination, out int bytesWritten, in uint value)
			{
				switch (value)
				{
					case <= 0xFF:
					{
						if (destination.Length < 1) goto too_small;
						destination[0] = unchecked((byte) value);
						bytesWritten = 1;
						return true;
					}
					case <= 0xFFFF:
					{
						if (destination.Length < 2) goto too_small;
						destination[0] = unchecked((byte) (value >> 8));
						destination[1] = unchecked((byte) value);
						bytesWritten = 2;
						return true;
					}
					case <= 0xFFFFFF:
					{
						if (destination.Length < 3) goto too_small;
						destination[0] = unchecked((byte) (value >> 16));
						destination[1] = unchecked((byte) (value >> 8));
						destination[2] = unchecked((byte) value);
						bytesWritten = 3;
						return true;
					}
					default:
					{
						if (destination.Length < 4) goto too_small;
						BinaryPrimitives.WriteUInt32BigEndian(destination, value);
						bytesWritten = 4;
						return true;
					}
				}

			too_small:
				bytesWritten = 0;
				return false;
			}

			#endregion

			#region Int64...

			/// <inheritdoc />
			public static bool TryGetSpan(scoped in long value, out ReadOnlySpan<byte> span)
			{
				span = default;
				return false;
			}

			/// <inheritdoc />
			public static bool TryGetSizeHint(in long value, out int sizeHint)
			{
				sizeHint = Unsafe.SizeOf<long>();
				return true;
			}

			/// <inheritdoc />
			public static bool TryEncode(Span<byte> destination, out int bytesWritten, in long value)
			{
				//note: negative values will be written as 8 bytes
				return TryEncode(destination, out bytesWritten, unchecked((ulong) value));
			}

			#endregion

			#region UInt64...

			/// <inheritdoc />
			public static bool TryGetSpan(scoped in ulong value, out ReadOnlySpan<byte> span)
			{
				span = default;
				return false;
			}

			/// <inheritdoc />
			public static bool TryGetSizeHint(in ulong value, out int sizeHint)
			{
				sizeHint = Unsafe.SizeOf<ulong>();
				return true;
			}

			/// <inheritdoc />
			public static bool TryEncode(Span<byte> destination, out int bytesWritten, in ulong value)
			{
				if (value <= uint.MaxValue)
				{ // smaller than 32 bits
					return TryEncode(destination, out bytesWritten, unchecked((uint) value));
				}

				// first write the 32 upper bits
				if (!TryEncode(destination, out int len, unchecked((uint) (value >> 32))))
				{
					goto too_small;
				}

				// then write the 32 lower bits (fixed size)
				destination = destination[len..];
				if (destination.Length < Unsafe.SizeOf<uint>())
				{
					goto too_small;
				}
				BinaryPrimitives.WriteUInt32BigEndian(destination, unchecked((uint) value));

				bytesWritten = len + Unsafe.SizeOf<uint>();
				return true;

			too_small:
				bytesWritten = 0;
				return false;

			}

			#endregion

		}

		/// <summary>Encodes primitive integers using the VarInt compact representation</summary>
		public readonly struct VarIntEncoder : ISpanEncoder<ushort>, ISpanEncoder<uint>, ISpanEncoder<ulong>
		{

			#region Int16...

			/// <inheritdoc />
			public static bool TryGetSpan(scoped in ushort value, out ReadOnlySpan<byte> span)
			{
				span = default;
				return false;
			}

			/// <inheritdoc />
			public static bool TryGetSizeHint(in ushort value, out int sizeHint)
			{
				sizeHint = (uint) value switch
				{
					< 1U << 7  => 1,
					< 1U << 14 => 2,
					_ => 3,
				};
				return true;
			}

			/// <inheritdoc />
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			public static bool TryEncode(Span<byte> destination, out int bytesWritten, in ushort value)
			{
				return value <= 127
					? TryWriteByte(destination, out bytesWritten, (byte) value)
					: TryEncodeSlow(destination, out bytesWritten, value);

				[MethodImpl(MethodImplOptions.NoInlining)]
				static bool TryEncodeSlow(Span<byte> destination, out int bytesWritten, uint value)
				{
					const uint MASK = 128;
					//note: value is known to be >= 128

					if (value < (1 << 14))
					{
						return TryWriteBytes(
							destination,
							out bytesWritten,
							(byte)(value | MASK),
							(byte)(value >> 7)
						);
					}

					return TryWriteBytes(
						destination,
						out bytesWritten,
						(byte)(value | MASK),
						(byte)((value >> 7) | MASK),
						(byte)(value >> 14)
					);
				}
			}

			#endregion

			#region Int32...

			/// <inheritdoc />
			public static bool TryGetSpan(scoped in uint value, out ReadOnlySpan<byte> span)
			{
				span = default;
				return false;
			}

			/// <inheritdoc />
			public static bool TryGetSizeHint(in uint value, out int sizeHint)
			{
				sizeHint = value switch
				{
					< 1U << 7  => 1,
					< 1U << 14 => 2,
					< 1U << 21 => 3,
					< 1U << 28 => 4,
					_ => 5
				};
				return true;
			}

			/// <inheritdoc />
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			public static bool TryEncode(Span<byte> destination, out int bytesWritten, in uint value)
			{
				return value <= 127
					? TryWriteByte(destination, out bytesWritten, (byte) value)
					: TryEncodeSlow(destination, out bytesWritten, value);

				[MethodImpl(MethodImplOptions.NoInlining)]
				static bool TryEncodeSlow(Span<byte> destination, out int bytesWritten, uint value)
				{
					const uint MASK = 128;
					//note: value is known to be >= 128

					if (value < (1 << 14))
					{
						return TryWriteBytes(
							destination,
							out bytesWritten,
							(byte)(value | MASK),
							(byte)(value >> 7)
						);
					}

					if (value < (1 << 21))
					{
						return TryWriteBytes(
							destination,
							out bytesWritten,
							(byte)(value | MASK),
							(byte)((value >> 7) | MASK),
							(byte)(value >> 14)
						);
					}

					if (value < (1 << 28))
					{
						return TryWriteBytes(
							destination,
							out bytesWritten,
							(byte)(value | MASK),
							(byte)((value >> 7) | MASK),
							(byte)((value >> 14) | MASK),
							(byte)(value >> 21)
						);
					}

					return TryWriteBytes(
						destination,
						out bytesWritten,
						[
							(byte) (value | MASK),
							(byte) ((value >> 7) | MASK),
							(byte) ((value >> 14) | MASK),
							(byte) ((value >> 21) | MASK),
							(byte) (value >> 28)
						]
					);
				}
			}

			#endregion

			#region Int64...

			/// <inheritdoc />
			public static bool TryGetSpan(scoped in ulong value, out ReadOnlySpan<byte> span)
			{
				span = default;
				return false;
			}

			/// <inheritdoc />
			public static bool TryGetSizeHint(in ulong value, out int sizeHint)
			{
				sizeHint = value switch
				{
					< 1UL << 7  => 1,
					< 1UL << 14 => 2,
					< 1UL << 21 => 3,
					< 1UL << 28 => 4,
					< 1UL << 35 => 5,
					< 1UL << 42 => 6,
					< 1UL << 49 => 7,
					< 1UL << 56 => 8,
					< 1UL << 63 => 9,
					_ => 10
				};
				return true;
			}

			/// <inheritdoc />
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			public static bool TryEncode(Span<byte> destination, out int bytesWritten, in ulong value)
			{
				if (value <= uint.MaxValue)
				{
					return TryEncode(destination, out bytesWritten, (uint) value);
				}

				return TryEncodeSlow(destination, out bytesWritten, value);

				[MethodImpl(MethodImplOptions.NoInlining)]
				static bool TryEncodeSlow(Span<byte> destination, out int bytesWritten, ulong value)
				{
					const uint MASK = 128;

					int p = 0;
					while (value >= MASK)
					{
						if (p >= destination.Length) goto too_small;
						destination[p++] = (byte) ((value & (MASK - 1)) | MASK);
						value >>= 7;
					}

					if (p >= destination.Length) goto too_small;
					destination[p++] = (byte) value;

					bytesWritten = p;
					return true;

				too_small:
					bytesWritten = 0;
					return false;
				}

			}

			#endregion

		}

	}

}
