// adapted from https://github.com/dotnet/corefxlab/tree/master/src/System.Text.Utf8

namespace Doxense.Text
{
	using System.Runtime.CompilerServices;
	using System.Text;
	using Doxense.Memory;

	[PublicAPI]
	public static unsafe class Utf8Encoder
	{

		public static readonly UTF8Encoding Encoding = new(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private static bool TryGetNumberOfEncodedBytesFromFirstByte(byte first, out int numberOfBytes)
		{
			if ((first & 0x80) == 0)
			{
				numberOfBytes = 1;
				return true;
			}

			if ((first & 0xE0) == 0xC0)
			{
				numberOfBytes = 2;
				return true;
			}

			if ((first & 0xF0) == 0xE0)
			{
				numberOfBytes = 3;
				return true;
			}

			if ((first & 0xF8) == 0xF0)
			{
				numberOfBytes = 4;
				return true;
			}

			numberOfBytes = default(int);
			return false;
		}


		/// <summary>Compute the length an UTF-8 encoded buffer</summary>
		/// <param name="buffer">Bytes of the UTF-8 encoded string</param>
		/// <param name="length">Receives the length (in codepoints) of the string</param>
		/// <returns>True if the buffer contains a valid UTF-8 encoded string.</returns>
		public static bool TryGetLength(Slice buffer, out int length)
		{
			if (buffer.Count == 0)
			{
				length = 0;
				return true;
			}
			fixed (byte* ptr = &buffer.DangerousGetPinnableReference())
			{
				return TryGetLength(ptr, ptr + buffer.Count, out length);
			}
		}

		/// <summary>Compute the length an UTF-8 encoded buffer</summary>
		/// <param name="buffer">Pointer to the start of the buffer</param>
		/// <param name="stop">Pointer to the next byte after the end of the buffer</param>
		/// <param name="length">Receives the length (in codepoints) of the string</param>
		/// <returns>True if the buffer contains a valid UTF-8 encoded string.</returns>
		public static bool TryGetLength(byte* buffer, byte* stop, out int length)
		{
			byte* ptr = buffer;
			int len = 0;
			while (ptr < stop)
			{
				if (!TryGetCodePointLength(ptr, stop, out var charLen)
				 || ptr + charLen > stop)
				{
					length = 0;
					return false;
				}
				ptr += charLen;
				len++;
			}
			length = len;
			return true;
		}

		/// <summary>Compute the length and hashcode of an UTF-8 encoded buffer</summary>
		/// <param name="buffer">Pointer to the start of the buffer</param>
		/// <param name="stop">Pointer to the next byte after the end of the buffer</param>
		/// <param name="length">Receives the length (in codepoints) of the string</param>
		/// <param name="hashCode">Receives the hashcode of the string</param>
		/// <returns>True if the buffer contains a valid UTF-8 encoded string.</returns>
		public static bool TryGetLengthAndHashCode(byte* buffer, byte* stop, out int length, out int hashCode)
		{
			byte* ptr = buffer;
			int len = 0;
			uint h = 0;
			while (ptr < stop)
			{
				if (!TryDecodeCodePoint(ptr, stop, out var cp, out var charLen)
				  || ptr + charLen > stop)
				{
					length = 0;
					hashCode = 0;
					return false;
				}
				h = UnicodeCodePoint.ContinueHashCode(h, cp);
				ptr += charLen;
				len++;
			}
			length = len;
			hashCode = UnicodeCodePoint.CompleteHashCode(h, len);
			return true;
		}

		/// <summary>Compute the maximum size (in bytes) required for a string of a specific length in the worst case scenario</summary>
		[Pure]
		public static int GetMaxByteCount(int length)
		{
			Contract.Debug.Requires(length >= 0);

			// We do the same thing as Utf8Encoding: 3 x (chars + 1)
			long byteCount = length + 1L;
			byteCount *= 3;

			//REVIEW: some codepoints encode into 4 bytes, but I think it's only for surrogates... maybe we should *4 instead of *3, juste to be sure??

			if (byteCount > int.MaxValue) throw FailMaxByteCountWouldOverflow();
			return (int) byteCount;
		}

		[Pure, MethodImpl(MethodImplOptions.NoInlining)]
		private static Exception FailMaxByteCountWouldOverflow()
		{
			// ReSharper disable once NotResolvedInText
			return new ArgumentOutOfRangeException("length", "Max byte count for UTF-8 buffer would overflow");
		}

		/// <summary>Compute the size (in bytes) required to encode a string into UTF-8</summary>
		/// <param name="text">String that we want to encode</param>
		/// <returns>Number of bytes required to encode the string in UTF-8</returns>
		[Pure]
		public static int GetByteCount(string text)
		{
			if (string.IsNullOrEmpty(text)) return 0;
			fixed (char* chars = text)
			{
				return GetByteCount(chars, text.Length);
			}
		}

		/// <summary>Compute the size (in bytes) required to encode a section of a string into UTF-8</summary>
		/// <param name="text">Complete string</param>
		/// <param name="offset">Offset of the first character in the string</param>
		/// <param name="count">Number of characters in the string</param>
		/// <returns>Number of bytes required to encode the substring in UTF-8</returns>
		[Pure]
		public static int GetByteCount(string text, int offset, int count)
		{
			Contract.DoesNotOverflow(text, offset, count);
			fixed (char* chars = text)
			{
				return GetByteCount(chars + offset, count);
			}
		}

		/// <summary>Compute the size (in bytes) required to encode a section of a string into UTF-8</summary>
		/// <param name="chars">Buffer containing the characters</param>
		/// <param name="offset">Offset of the first character in the string</param>
		/// <param name="count">Number of characters in the string</param>
		/// <returns>Number of bytes required to encode the substring in UTF-8</returns>
		[Pure]
		public static int GetByteCount(char[] chars, int offset, int count)
		{
			Contract.DoesNotOverflow(chars, offset, count);
			fixed (char* ptr = &chars[offset])
			{
				return GetByteCount(ptr, count);
			}
		}

		/// <summary>Compute the size (in bytes) required to encode a string into UTF-8</summary>
		/// <param name="chars">Pointer to the start of the string</param>
		/// <param name="length">Length (in characters) of the string</param>
		/// <returns>Number of bytes required to encode the string in UTF-8</returns>
		[Pure]
		public static int GetByteCount(char* chars, int length)
		{
			Contract.PointerNotNull(chars);
			Contract.Positive(length);

			if (length == 0) return 0;

			// scan until the end, or first non-ASCII character
			char* ptr = chars;
			char* stop = chars + length;
			while (ptr < stop)
			{
				char c = *ptr++;
				//TODO: optimze this to scan multiple chars at once!
				if (c >= 0x80)
				{ // the string contains UNICODE
					--ptr;
					goto non_ascii; // => slow path
				}
			}
			// all ascii, size is same as length
			return length;
		non_ascii:
			// resume couting from the first non-ASCII character
			long count = ptr - chars;
			while (ptr < stop)
			{
				count += GetNumberOfEncodedBytes(new UnicodeCodePoint(*ptr++));
			}
			return checked((int) count);
		}

		public static bool TryValidateFirstByteCodePointValue(byte* buffer, byte* stop, out int length)
		{
			if (buffer < stop
			 && TryGetNumberOfEncodedBytesFromFirstByte(buffer[0], out var len)
			 && buffer + len <= stop)
			{
				length = len;
				return true;
			}
			length = 0;
			return false;
		}

		public static bool TryGetFirstByteCodePointValue(byte* buffer, byte* stop, out UnicodeCodePoint cp, out int length)
		{
			byte first;
			if (buffer < stop
			 && TryGetNumberOfEncodedBytesFromFirstByte((first = buffer[0]), out var len)
			 && buffer + len <= stop)
			{
				length = len;
				switch (len)
				{
					case 1:
					{
						cp = (UnicodeCodePoint) (first & 0x7F);
						break;
					}
					case 2:
					{
						cp = (UnicodeCodePoint) (first & 0x1F);
						break;
					}
					case 3:
					{
						cp = (UnicodeCodePoint) (first & 0x0F);
						break;
					}
					default: // 4
					{
						cp = (UnicodeCodePoint) (first & 0x07);
						break;
					}
				}
				return true;
			}
			length = 0;
			cp = default;
			return false;
		}

		public static bool TryGetFirstByteCodePointValue(byte first, int count, out UnicodeCodePoint cp, out int length)
		{
			if (TryGetNumberOfEncodedBytesFromFirstByte(first, out var len)
			 && len <= count)
			{
				length = len;
				switch (len)
				{
					case 1:
					{
						cp = (UnicodeCodePoint) (first & 0x7F);
						break;
					}
					case 2:
					{
						cp = (UnicodeCodePoint) (first & 0x1F);
						break;
					}
					case 3:
					{
						cp = (UnicodeCodePoint) (first & 0x0F);
						break;
					}
					default: // 4
					{
						cp = (UnicodeCodePoint) (first & 0x07);
						break;
					}
				}
				return true;
			}
			length = 0;
			cp = default(UnicodeCodePoint);
			return false;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private static bool ValidateCodePointByte(byte nextByte)
		{
			return (nextByte & 0xC0U) == 0x80U;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private static bool TryReadCodePointByte(byte nextByte, ref UnicodeCodePoint codePoint)
		{
			uint current = nextByte;
			if ((current & 0xC0U) != 0x80U)
				return false;

			codePoint = new UnicodeCodePoint((codePoint.Value << 6) | (0x3FU & current));
			return true;
		}

		public static bool TryGetCodePointLength(byte* buffer, byte* stop, out int length)
		{
			if (!TryValidateFirstByteCodePointValue(buffer, stop, out length))
			{
				length = 0;
				return false;
			}

			for (int i = 1; i < length; i++)
			{
				if (!ValidateCodePointByte(buffer[i]))
				{
					return false;
				}
			}
			return true;
		}

		public static bool TryDecodeCodePoint(ReadOnlySpan<byte> buffer, out UnicodeCodePoint codePoint, out int length)
		{
			fixed (byte* ptr = buffer)
			{
				return TryDecodeCodePoint(ptr, ptr + buffer.Length, out codePoint, out length);
			}
		}

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool TryDecodeCodePoint(Slice buffer, out UnicodeCodePoint codePoint, out int length)
		{
			fixed (byte* ptr = &buffer.DangerousGetPinnableReference())
			{
				return TryDecodeCodePoint(ptr, ptr + buffer.Count, out codePoint, out length);
			}
		}

		[Pure]
		public static bool TryDecodeCodePoint(byte[] buffer, int offset, int count, out UnicodeCodePoint codePoint, out int length)
		{
			Contract.Debug.Requires(buffer != null && offset >= 0 && count >= 0);
			if (!TryGetFirstByteCodePointValue(buffer[offset], count, out codePoint, out length))
			{
				codePoint = default(UnicodeCodePoint);
				length = 0;
				return false;
			}

			for (int i = 1; i < length; i++)
			{
				if (!TryReadCodePointByte(buffer[offset + i], ref codePoint))
				{
					return false;
				}
			}
			return true;
		}

		public static bool TryDecodeCodePoint(byte* buffer, byte* stop, out UnicodeCodePoint codePoint, out int length)
		{
			if (!TryGetFirstByteCodePointValue(buffer, stop, out codePoint, out length))
			{
				return false;
			}

			for (int i = 1; i < length; i++)
			{
				if (!TryReadCodePointByte(buffer[i], ref codePoint))
				{
					return false;
				}
			}
			return true;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		internal static int GetNumberOfEncodedBytes(UnicodeCodePoint codePoint)
		{
			if (codePoint.Value <= 0x7F)
			{
				return 1;
			}

			if (codePoint.Value <= 0x7FF)
			{
				return 2;
			}

			if (codePoint.Value <= 0xFFFF)
			{
				return 3;
			}

			if (codePoint.Value <= 0x1FFFFF)
			{
				return 4;
			}

			return 0;
		}

		public static bool TryEncodeEncodePoint(UnicodeCodePoint cp, byte* buffer, byte* stop, out int length)
		{
			Contract.Debug.Requires(buffer != null);

			length = GetNumberOfEncodedBytes(cp);
			if (buffer + length > stop) goto fail;
			switch (length)
			{
				case 1:
					buffer[0] = (byte) (cp.Value & 0x7F);
					return true;
				case 2:
					byte b0 = (byte) (((cp.Value >> 6) & 0x1F) | 0xC0);
					byte b1 = (byte) (((cp.Value >> 0) & 0x3F) | 0x80);
					*((ushort*) buffer) = (ushort) (b0 | (b1 << 8));
					return true;
				case 3:
					b0 = (byte) (((cp.Value >> 12) & 0xF) | 0xE0);
					b1 = (byte) (((cp.Value >> 6) & 0x3F) | 0x80);
					*((ushort*) buffer) = (ushort) (b0 | (b1 << 8));
					buffer[2] = (byte) (((cp.Value >> 0) & 0x3F) | 0x80);
					return true;
				case 4:
					b0 = (byte) (((cp.Value >> 18) & 0x7) | 0xE0);
					b1 = (byte) (((cp.Value >> 12) & 0x3F) | 0x80);
					byte b2 = (byte) (((cp.Value >> 6) & 0x3F) | 0x80);
					byte b3 = (byte) (((cp.Value >> 0) & 0x3F) | 0x80);
					*((ushort*) buffer) = (ushort) (b0 | (b1 << 8) | (b2 << 16) | (b3 << 24));
					return true;
				default:
					return false;
			}
		fail:
			throw new IndexOutOfRangeException(); //TODO: better error msg (BufferOverflowException ?)
		}

		public static bool TryWriteUnicodeCodePoint(ref SliceWriter writer, UnicodeCodePoint cp)
		{
			uint value = cp.Value;
			switch (GetNumberOfEncodedBytes(cp))
			{
				case 1:
				{
					writer.WriteByte((byte) (value & 0x7F));
					return true;
				}
				case 2:
				{
					writer.WriteBytes(
						(byte) (((value >> 6) & 0x1F) | 0xC0),
						(byte) (((value >> 0) & 0x3F) | 0x80)
					);
					return true;
				}
				case 3:
				{
					writer.WriteBytes(
						(byte) (((value >> 12) & 0xF) | 0xE0),
						(byte) (((value >> 6) & 0x3F) | 0x80),
						(byte) (((value >> 0) & 0x3F) | 0x80)
					);
					return true;
				}
				case 4:
				{
					writer.WriteBytes(
						(byte) (((value >> 18) & 0x7) | 0xE0),
						(byte) (((value >> 12) & 0x3F) | 0x80),
						(byte) (((value >> 6) & 0x3F) | 0x80),
						(byte) (((value >> 0) & 0x3F) | 0x80)
					);
					return true;
				}
				default:
				{
					return false;
				}
			}
		}

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static Slice GetBuffer(string text)
		{
			return Slice.FromStringUtf8(text);
		}

		[Pure]
		public static byte[] GetBytes(string text)
		{
			Contract.NotNull(text);
			//TODO: optimize this?
			return text.Length != 0 ? Slice.Utf8NoBomEncoding.GetBytes(text) : [ ];
		}
	}
}
