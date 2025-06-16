#region Copyright (c) 2023-2025 SnowBank SAS
//
// All rights are reserved. Reproduction or transmission in whole or in part, in
// any form or by any means, electronic, mechanical or otherwise, is prohibited
// without the prior written consent of the copyright owner.
//
#endregion

namespace SnowBank.Text
{
	using System.Buffers;

	public static class Base1024Encoding
	{

		/// <summary>Offset from the 0-byte to the corresponding character</summary>
		private const int CHAR_OFFSET = 48; // '0'


		/// <summary>Computes the number of characters required to encode a given number of bytes into Base-1024</summary>
		/// <param name="byteCount">Size in bytes of the input</param>
		/// <returns>Size in characters of the encoded output</returns>
		public static int GetCharCount(int byteCount)
		{
			int completeChunks = byteCount / 5;
			int extraBytes = byteCount % 5;
			int minimumChars = checked(completeChunks * 4 + (((extraBytes * 8) + 9) / 10) + (extraBytes == 4 ? 1 : 0));
			return minimumChars;
		}

		public static string Encode(byte[] input) => Encode(input.AsSpan());

		public static string Encode(Slice input) => Encode(input.Span);

		public static string Encode(ReadOnlySpan<byte> input)
		{
			// compute the required capacity, and allocate a buffer
			int capacity = GetCharCount(input.Length);

			// => use the stack for small buffers, otherwise allocate from a pool
			char[]? buffer = null;
			Span<char> output = capacity <= 128 ? stackalloc char[capacity] : (buffer = ArrayPool<char>.Shared.Rent(capacity));

			if (!TryEncodeTo(input, output, out var charsWritten) || charsWritten > capacity)
			{ // should not happen, unless the capacity computation is broken!
#if DEBUG
				if (System.Diagnostics.Debugger.IsAttached) System.Diagnostics.Debugger.Break();
#endif
				throw new InvalidOperationException();
			}

			var result = output[..charsWritten].ToString();

			if (buffer is not null)
			{ // return rented buffer to the pool!
				ArrayPool<char>.Shared.Return(buffer);
			}

			return result;
		}

		public static bool TryEncodeTo(Slice source, Span<char> destination, out int charsWritten)
			=> TryEncodeTo(source.Span, destination, out charsWritten);

		public static bool TryEncodeTo(ReadOnlySpan<byte> source, Span<char> destination, out int charsWritten)
		{
			charsWritten = 0;

			// Incomplete chunks will use padding
			// > 1 extra byte
			//   - 0: 00000000__
			// > 2 extra bytes
			//   - 0: 0000000011
			//   - 1:           111111____
			// > 3 extra bytes
			//   - 0: 0000000011
			//   - 1:           1111112222
			//   - 2:                     2222______
			// > 4 extra bytes
			//   - 0: 0000000011
			//   - 1:           1111112222
			//   - 2:                     2222333333
			//   - 3:                               33________
			// Chunks of 5 inputs bytes will be encoded into 4 output bytes
			// > 5 bytes
			//   - 0: 0000000011
			//   - 1:           1111112222
			//   - 2:                     2222333333
			//   - 3:                               3344444444

			int minimumChars = GetCharCount(source.Length);
			if (destination.Length < minimumChars)
			{
				return false;
			}

			while (source.Length >= 5)
			{
				// first chunk
				destination[0] =  (char) ((((source[0] & 0xFF) << 2) | (source[1] >> 6)) + CHAR_OFFSET);
				destination[1]  = (char) ((((source[1] & 0x3F) << 4) | (source[2] >> 4)) + CHAR_OFFSET);
				destination[2]  = (char) ((((source[2] & 0x0F) << 6) | (source[3] >> 2)) + CHAR_OFFSET);
				destination[3]  = (char) ((((source[3] & 0x03) << 8) | (source[4] >> 0)) + CHAR_OFFSET);

				source = source[5..];
				destination = destination[4..];
				charsWritten += 4;
			}

			switch (source.Length)
			{
				case 1:
				{
					Contract.Debug.Assert(destination.Length >= 1);
					destination[0]  = (char) ((((source[0] & 0xFF) << 2)) + CHAR_OFFSET);
					charsWritten += 1;
					break;
				}
				case 2:
				{
					Contract.Debug.Assert(destination.Length >= 2);
					destination[0] =  (char) ((((source[0] & 0xFF) << 2) | (source[1] >> 6)) + CHAR_OFFSET);
					destination[1]  = (char) ((((source[1] & 0x3F) << 4)) + CHAR_OFFSET);
					charsWritten += 2;
					break;
				}
				case 3:
				{
					Contract.Debug.Assert(destination.Length >= 3);
					destination[0] =  (char) ((((source[0] & 0xFF) << 2) | (source[1] >> 6)) + CHAR_OFFSET);
					destination[1]  = (char) ((((source[1] & 0x3F) << 4) | (source[2] >> 4)) + CHAR_OFFSET);
					destination[2]  = (char) ((((source[2] & 0x0F) << 6)) + CHAR_OFFSET);
					charsWritten += 3;
					break;
				}
				case 4:
				{
					Contract.Debug.Assert(destination.Length >= 4);
					destination[0] =  (char) ((((source[0] & 0xFF) << 2) | (source[1] >> 6)) + CHAR_OFFSET);
					destination[1]  = (char) ((((source[1] & 0x3F) << 4) | (source[2] >> 4)) + CHAR_OFFSET);
					destination[2]  = (char) ((((source[2] & 0x0F) << 6) | (source[3] >> 2)) + CHAR_OFFSET);
					destination[3]  = (char) ((((source[3] & 0x03) << 8)) + CHAR_OFFSET);
					destination[4] = '!'; // add the special marker to mark the last chunk as 'incomplete'
					charsWritten += 5;
					break;
				}
			}

			Contract.Debug.Ensures(charsWritten == minimumChars);

			return true;
		}

		public static int GetBytesCount(ReadOnlySpan<char> source)
		{
			bool hasEndMarker = source[^1] == '!';
			int len = source.Length;
			if (hasEndMarker)
			{
				--len;
			}
			if (len <= 0)
			{
				return 0;
			}

			// each chunk of 4 characters is decoded as 5 output bytes, except if this is the last chunk and 'hasEndMarker' is true, in which case only 4 output bytes will be emitted.
			int completeChunks = len / 4;
			int extraBytes = len % 4;
			if (hasEndMarker && extraBytes == 0)
			{
				--completeChunks;
				extraBytes = 4;
			}

			return checked(completeChunks * 5 + extraBytes);
		}

		public static Slice Decode(string source) => Decode(source.AsSpan());

		public static Slice Decode(ReadOnlySpan<char> source)
		{
			// compute the required capacity, and allocate a buffer
			int capacity = GetBytesCount(source);

			// => use the stack for small buffers, otherwise allocate from a pool
			var buffer = new byte[capacity];

			if (!TryDecodeTo(source, buffer, out var bytesWritten) || bytesWritten > capacity)
			{ // should not happen, unless the capacity computation is broken!
#if DEBUG
				if (System.Diagnostics.Debugger.IsAttached) System.Diagnostics.Debugger.Break();
#endif
				throw new InvalidOperationException();
			}

			return buffer.AsSlice(0, bytesWritten);
		}

		public static bool TryDecodeTo(ReadOnlySpan<char> source, Span<byte> output, out int bytesWritten)
		{
			bytesWritten = 0;

			// If the last character is '!', then it means the last chunk has only 3 bytes
			// => the '!' should be then be discarded in the rest of the computation

			bool hasEndMarker = source[^1] == '!';
			if (hasEndMarker)
			{
				source = source[..^1];
			}

			// each chunk of 4 characters is decoded as 5 output bytes, except if this is the last chunk and 'hasEndMarker' is true, in which case only 4 output bytes will be emitted.
			int completeChunks = source.Length / 4;
			int extraBytes = source.Length % 4;
			if (hasEndMarker && extraBytes == 0)
			{
				--completeChunks;
				extraBytes = 4;
			}

			int minimumOutputSize = checked(completeChunks * 5 + extraBytes);
			if (output.Length < minimumOutputSize)
			{ // output buffer is too small!
				return false;
			}

			// Incomplete chunks have internal padding ('x' are discarded output bytes, '_' are padding bits that must be 0)
			// > 1 character
			//   - 0: 00000000
			//   - x:         __
			// > 2 characters
			//   - 0: 00000000
			//   - 1:         00111111
			//   - x:                 ____
			// > 3 characters
			//   - 0: 00000000
			//   - 1:         00111111
			//   - 2:                 11112222
			//   - x:                         ______
			// Chunks of 4 inputs characters will be decoded into 4 output bytes
			// > last chunk AND last character equal to '!'
			//   - 0: 00000000
			//   - 1:         00111111
			//   - 2:                 11112222
			//   - 3:                         22222233
			//   - x:                                 ________
			// > otherwise
			//   - 0: 00000000
			//   - 1:         00111111
			//   - 2:                 11112222
			//   - 3:                         22222233
			//   - 4:                                 33333333

			while (source.Length > 4)
			{ // full chunk

				int s0 = (source[0] - CHAR_OFFSET);
				int s1 = (source[1] - CHAR_OFFSET);
				int s2 = (source[2] - CHAR_OFFSET);
				int s3 = (source[3] - CHAR_OFFSET);

				output[0]  = (byte)                       (s0 >> 2);
				output[1]  = (byte) (((s0 & 0x03) << 6) | (s1 >> 4));
				output[2]  = (byte) (((s1 & 0x0F) << 4) | (s2 >> 6));
				output[3]  = (byte) (((s2 & 0x3F) << 2) | (s3 >> 8));
				output[4]  = (byte)   (s3 & 0xFF);

				source = source[4..];
				output = output[5..];
				bytesWritten += 5;
			}

			switch (source.Length)
			{
				case 1:
				{
					int s0 = (source[0] - CHAR_OFFSET);
					output[0]  = (byte) (s0 >> 2);
					if ((s0 & 0x03) != 0) goto invalid_padding;
					bytesWritten += 1;
					break;
				}
				case 2:
				{
					int s0 = (source[0] - CHAR_OFFSET);
					int s1 = (source[1] - CHAR_OFFSET);
					output[0]  = (byte)                       (s0 >> 2);
					output[1]  = (byte) (((s0 & 0x03) << 6) | (s1 >> 4));
					if ((s1 & 0x0F) != 0) goto invalid_padding;
					bytesWritten += 2;
					break;
				}
				case 3:
				{
					int s0 = (source[0] - CHAR_OFFSET);
					int s1 = (source[1] - CHAR_OFFSET);
					int s2 = (source[2] - CHAR_OFFSET);
					output[0]  = (byte)                       (s0 >> 2);
					output[1]  = (byte) (((s0 & 0x03) << 6) | (s1 >> 4));
					output[2]  = (byte) (((s1 & 0x0F) << 4) | (s2 >> 6));
					if ((s2 & 0x3F) != 0) goto invalid_padding;
					bytesWritten += 3;
					break;
				}
				case 4 when(hasEndMarker):
				{
					int s0 = (source[0] - CHAR_OFFSET);
					int s1 = (source[1] - CHAR_OFFSET);
					int s2 = (source[2] - CHAR_OFFSET);
					int s3 = (source[3] - CHAR_OFFSET);
					output[0]  = (byte)                       (s0 >> 2);
					output[1]  = (byte) (((s0 & 0x03) << 6) | (s1 >> 4));
					output[2]  = (byte) (((s1 & 0x0F) << 4) | (s2 >> 6));
					output[3]  = (byte) (((s2 & 0x3F) << 2) | (s3 >> 8));
					if ((s3 & 0xFF) != 0) goto invalid_padding;
					bytesWritten += 4;
					break;
				}
				case 4:
				{ // fullchunk
					int s0 = (source[0] - CHAR_OFFSET);
					int s1 = (source[1] - CHAR_OFFSET);
					int s2 = (source[2] - CHAR_OFFSET);
					int s3 = (source[3] - CHAR_OFFSET);
					output[0]  = (byte)                       (s0 >> 2);
					output[1]  = (byte) (((s0 & 0x03) << 6) | (s1 >> 4));
					output[2]  = (byte) (((s1 & 0x0F) << 4) | (s2 >> 6));
					output[3]  = (byte) (((s2 & 0x3F) << 2) | (s3 >> 8));
					output[4]  = (byte)   (s3 & 0xFF);
					bytesWritten += 5;
					break;
				}
			}

			return true;

		invalid_padding:
			bytesWritten = 0;
			return false;
		}

	}

}
