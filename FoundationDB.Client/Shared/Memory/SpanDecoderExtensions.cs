﻿#region BSD License
/* Copyright (c) 2013-2020, Doxense SAS
All rights reserved.

Redistribution and use in source and binary forms, with or without
modification, are permitted provided that the following conditions are met:
	* Redistributions of source code must retain the above copyright
	  notice, this list of conditions and the following disclaimer.
	* Redistributions in binary form must reproduce the above copyright
	  notice, this list of conditions and the following disclaimer in the
	  documentation and/or other materials provided with the distribution.
	* Neither the name of Doxense nor the
	  names of its contributors may be used to endorse or promote products
	  derived from this software without specific prior written permission.

THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS "AS IS" AND
ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED
WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE
DISCLAIMED. IN NO EVENT SHALL <COPYRIGHT HOLDER> BE LIABLE FOR ANY
DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES
(INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES;
LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND
ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT
(INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS
SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
 */
#endregion

#if !USE_SHARED_FRAMEWORK

namespace System
{
	using System;
	using System.Diagnostics;
	using System.Globalization;
	using System.Runtime.CompilerServices;
	using System.Runtime.InteropServices;
	using System.Text;
	using Doxense.Memory;
	using JetBrains.Annotations;

	/// <summary>Helper methods to help decode the content of spans</summary>
	[PublicAPI]
	[DebuggerNonUserCode] //remove this when you need to troubleshoot this class!
	public static class SpanDecoderExtensions
	{

		/// <summary>Stringify a span containing characters in the operating system's current ANSI codepage</summary>
		/// <returns>Decoded string, or string.Empty if the span is empty</returns>
		/// <remarks>
		/// Calling this method on a span that is not ANSI, or was generated with different codepage than the current process, will return a corrupted string!
		/// This method should ONLY be used to interop with the Win32 API or unmanaged libraries that require the ANSI codepage!
		/// You SHOULD *NOT* use this to expose data to other systems or locale (via sockets, files, ...)
		/// If you are decoding natural text, you should probably change the encoding at the source to be UTF-8!
		/// If you are decoding identifiers or keywords that are known to be ASCII only, you should use <see cref="ToStringAscii(System.ReadOnlySpan{byte})"/> instead (safe).
		/// If these identifiers can contain 'special' bytes (like \xFF or \xFE), you should use <see cref="ToByteString(System.ReadOnlySpan{byte})"/> instead (unsafe).
		/// </remarks>
		[Pure]
		public static string ToStringAnsi(this ReadOnlySpan<byte> span)
		{
			if (span.Length == 0) return string.Empty;
			//note: Encoding.GetString() will do the bound checking for us
#if USE_SPAN_API
			return Encoding.Default.GetString(span);
#else
			unsafe
			{
				fixed(byte* ptr = span)
				{
					return Encoding.Default.GetString(ptr, span.Length);
				}
			}
#endif
		}

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static string ToStringAnsi(this Span<byte> span) => ToStringAnsi((ReadOnlySpan<byte>) span);

		/// <summary>Stringify a span containing 7-bit ASCII characters only</summary>
		/// <returns>Decoded string, or string.Empty if the span is empty</returns>
		/// <remarks>
		/// This method should ONLY be used to decoded data that is GUARANTEED to be in the range 0..127.
		/// This method will THROW if any byte in the span has bit 7 set to 1 (ie: >= 0x80)
		/// If you are decoding identifiers or keywords with 'special' bytes (like \xFF or \xFE), you should use <see cref="ToByteString(System.ReadOnlySpan{byte})"/> instead.
		/// If you are decoding natural text, or text from unknown origin, you should use <see cref="ToStringUtf8(System.ReadOnlySpan{byte})"/> or <see cref="ToStringUnicode(System.ReadOnlySpan{byte})"/> instead.
		/// If you are attempting to decode a string obtain from a Win32 or unmanaged library call, you should use <see cref="ToStringAnsi(System.ReadOnlySpan{byte})"/> instead.
		/// </remarks>
		[Pure]
		public static string ToStringAscii(this ReadOnlySpan<byte> span)
		{
			if (span.Length == 0) return string.Empty;

			if (UnsafeHelpers.IsAsciiBytes(span))
			{
				return UnsafeHelpers.ConvertToByteString(span);
			}
			throw new DecoderFallbackException("The span contains at least one non-ASCII character");
		}

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static string ToStringAscii(this Span<byte> span) => ToStringAscii((ReadOnlySpan<byte>) span);

		/// <summary>Stringify a span containing only ASCII chars</summary>
		/// <returns>ASCII string, or string.Empty if the span is empty</returns>
		[Pure]
		public static string ToByteString(this ReadOnlySpan<byte> span) //REVIEW: rename to ToStringSOMETHING(): ToStringByte()? ToStringRaw()?
		{
			return span.Length == 0
				? string.Empty
				: UnsafeHelpers.ConvertToByteString(span);
		}

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static string ToByteString(this Span<byte> span) => ToByteString((ReadOnlySpan<byte>) span);

		/// <summary>Stringify a span containing either 7-bit ASCII, or UTF-8 characters</summary>
		/// <returns>Decoded string, or string.Empty if the span is empty. The encoding will be automatically detected</returns>
		/// <remarks>
		/// This should only be used for spans encoded using UTF8 or ASCII.
		/// This is NOT compatible with spans encoded using ANSI (<see cref="Encoding.Default"/> encoding) or with a specific encoding or code page.
		/// This method will NOT automatically remove the UTF-8 BOM if present (use <see cref="ToStringUtf8(System.ReadOnlySpan{byte})"/> if you need this)
		/// </remarks>
		[Pure]
		public static string ToStringUnicode(this ReadOnlySpan<byte> span)
		{
			if (span.Length == 0) return string.Empty;
			if (UnsafeHelpers.IsAsciiBytes(span)) return UnsafeHelpers.ConvertToByteString(span);
#if USE_SPAN_API
			return Slice.Utf8NoBomEncoding.GetString(span);
#else
			unsafe
			{
				fixed (byte* ptr = span)
				{
					return Slice.Utf8NoBomEncoding.GetString(ptr, span.Length);
				}
			}
#endif
		}

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static string ToStringUnicode(this Span<byte> span) => ToStringUnicode((ReadOnlySpan<byte>) span);

		[Pure]
		private static bool HasUtf8Bom(ReadOnlySpan<byte> span)
		{
			return span.Length >= 3
				&& span[0] == 0xEF
				&& span[1] == 0xBB
				&& span[2] == 0xBF;
		}

		/// <summary>Decode a span that is known to contain an UTF-8 encoded string with an optional UTF-8 BOM</summary>
		/// <returns>Decoded string, or string.Empty if the span is empty</returns>
		/// <exception cref="DecoderFallbackException">If the span contains one or more invalid UTF-8 sequences</exception>
		/// <remarks>
		/// This method will THROW if the span does not contain valid UTF-8 sequences.
		/// This method will remove any UTF-8 BOM if present. If you need to keep the BOM as the first character of the string, use <see cref="ToStringUnicode(System.ReadOnlySpan{byte})"/>
		/// </remarks>
		[Pure]
		public static string ToStringUtf8(this ReadOnlySpan<byte> span)
		{
			if (span.Length == 0) return string.Empty;

			// detect BOM
			if (HasUtf8Bom(span))
			{ // skip it!
				if (span.Length == 3) return string.Empty;
				span = span.Slice(3);
			}

#if USE_SPAN_API
			return Slice.Utf8NoBomEncoding.GetString(span);
#else
			unsafe
			{
				fixed (byte* ptr = span)
				{
					return Slice.Utf8NoBomEncoding.GetString(ptr, span.Length);
				}
			}
#endif
		}

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static string ToStringUtf8(this Span<byte> span) => ToStringUtf8((ReadOnlySpan<byte>) span);

		/// <summary>Converts a span into a string with each byte encoded into hexadecimal (lowercase)</summary>
		/// <param name="lowerCase">If true, produces lowercase hexadecimal (a-f); otherwise, produces uppercase hexadecimal (A-F)</param>
		/// <returns>"0123456789abcdef"</returns>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static string ToHexaString(this ReadOnlySpan<byte> span, bool lowerCase = false) => FormatHexaString(span, '\0', lowerCase);

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static string? ToHexaString(this Span<byte> span, bool lowerCase = false) => FormatHexaString(span, '\0', lowerCase);

		/// <summary>Converts a span into a string with each byte encoded into hexadecimal (uppercase) separated by a char</summary>
		/// <param name="sep">Character used to separate the hexadecimal pairs (ex: ' ')</param>
		/// <param name="lowerCase">If true, produces lowercase hexadecimal (a-f); otherwise, produces uppercase hexadecimal (A-F)</param>
		/// <returns>"01 23 45 67 89 ab cd ef"</returns>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static string ToHexaString(this ReadOnlySpan<byte> span, char sep, bool lowerCase = false) => FormatHexaString(span, sep, lowerCase);

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static string ToHexaString(this Span<byte> span, char sep, bool lowerCase = false) => FormatHexaString(span, sep, lowerCase);

		[Pure, MethodImpl(MethodImplOptions.NoInlining)]
		internal static string FormatHexaString(ReadOnlySpan<byte> buffer, char sep, bool lower)
		{
			if (buffer.Length == 0) return string.Empty;

			var sb = new StringBuilder(buffer.Length * (sep == '\0' ? 2 : 3));
			int letters = lower ? 87 : 55;
			unsafe
			{
				fixed (byte* ptr = buffer)
				{
					byte* inp = ptr;
					byte* stop = ptr + buffer.Length;
					while (inp < stop)
					{
						if ((sep != '\0') & (sb.Length > 0)) sb.Append(sep);
						byte b = *inp++;
						int h = b >> 4;
						int l = b & 0xF;
						h += h < 10 ? 48 : letters;
						l += l < 10 ? 48 : letters;
						sb.Append((char) h).Append((char) l);
					}
				}
			}

			return sb.ToString();
		}

		internal static StringBuilder EscapeString(StringBuilder sb, ReadOnlySpan<byte> buffer, Encoding encoding)
		{
			if (sb == null) sb = new StringBuilder(buffer.Length + 16);
#if USE_SPAN_API
			int charCount = encoding.GetCharCount(buffer);
			if (charCount == 0) return sb;

			//TODO: allocate on heap if too large?
			Span<char> tmp = stackalloc char[charCount];
			if (encoding.GetChars(buffer, tmp) != buffer.Length) throw new InvalidOperationException();
#else
			int charCount;
			unsafe
			{
				fixed (byte* ptr = buffer)
				{
					charCount = encoding.GetCharCount(ptr, buffer.Length);
					if (charCount == 0) return sb;
				}
			}

			//TODO: allocate on heap if too large?
			Span<char> tmp = stackalloc char[charCount];
			unsafe
			{
				fixed (byte* ptr = buffer)
				fixed (char* chars = tmp)
				{
					if (encoding.GetChars(ptr, buffer.Length, chars, charCount) != buffer.Length) throw new InvalidOperationException();
				}
			}
#endif

			foreach (var c in tmp)
			{
				if ((c >= ' ' && c <= '~') || (c >= 880 && c <= 2047) || (c >= 12352 && c <= 12591))
					sb.Append(c);
				else if (c == 0)
					sb.Append(@"\0");
				else if (c == '\n')
					sb.Append(@"\n");
				else if (c == '\r')
					sb.Append(@"\r");
				else if (c == '\t')
					sb.Append(@"\t");
				else if (c > 127)
					sb.Append(@"\u").Append(((int) c).ToString("x4", CultureInfo.InvariantCulture));
				else // pas clean!
					sb.Append(@"\x").Append(((int) c).ToString("x2", CultureInfo.InvariantCulture));
			}
			return sb;
		}

		public static string ToString(this ReadOnlySpan<byte> span, string? format)
		{
			return ToString(span, format, null);
		}

		public static string ToString(this ReadOnlySpan<byte> span, string? format, IFormatProvider? provider)
		{
			switch (format ?? "D")
			{
				case "P": return PrettyPrint(span);
				case "N": return span.ToHexaString('\0', false);
				case "n": return span.ToHexaString('\0', true);
				case "X": return span.ToHexaString(' ', false);
				case "x": return span.ToHexaString(' ', true);
				case "D": case "d": return Slice.Dump(span);
				default:
					throw new FormatException("Format is invalid or not supported");
			}
		}

		/// <summary>Helper method that dumps the span as a string (if it contains only printable ascii chars) or an hex array if it contains non printable chars. It should only be used for logging and troubleshooting !</summary>
		/// <returns>Returns either "'abc'", "&lt;00 42 7F&gt;", or "{ ...JSON... }". Returns "''" for the empty span</returns>
		[Pure]
		public static string PrettyPrint(this ReadOnlySpan<byte> span)
		{
			return PrettyPrintInternal(span, 1024); //REVIEW: constant for max size!
		}

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static string PrettyPrint(this Span<byte> span) => PrettyPrintInternal(span, 1024);

		/// <summary>Helper method that dumps the span as a string (if it contains only printable ascii chars) or an hex array if it contains non printable chars. It should only be used for logging and troubleshooting !</summary>
		/// <param name="maxLen">Truncate the span if it exceeds this size</param>
		/// <returns>Returns either "'abc'", "&lt;00 42 7F&gt;", or "{ ...JSON... }". Returns "''" for the empty span</returns>
		[Pure]
		public static string PrettyPrint(this ReadOnlySpan<byte> span, int maxLen) => PrettyPrintInternal(span, maxLen);

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static string PrettyPrint(this Span<byte> span, int maxLen) => PrettyPrintInternal(span, maxLen);

		[Pure]
		internal static string PrettyPrintInternal(ReadOnlySpan<byte> buffer, int maxLen)
		{
			if (buffer.Length == 0) return "''";

			// look for UTF-8 BOM
			if (HasUtf8Bom(buffer))
			{ // this is supposed to be an UTF-8 string
				return EscapeString(new StringBuilder(buffer.Length).Append('\''), buffer.Slice(3), Slice.Utf8NoBomEncoding).Append('\'').ToString();
			}

			if (buffer.Length >= 2)
			{
				// look for JSON objets or arrays
				if ((buffer[0] == '{' && buffer[buffer.Length - 1] == '}') 
				 || (buffer[0] == '[' && buffer[buffer.Length - 1] == ']'))
				{
					try
					{
						if (buffer.Length <= maxLen)
						{
							return EscapeString(new StringBuilder(buffer.Length + 16), buffer, Slice.Utf8NoBomEncoding).ToString();
						}
						else
						{
							return
								EscapeString(new StringBuilder(buffer.Length + 16), buffer.Slice(0, maxLen), Slice.Utf8NoBomEncoding)
									.Append("[\u2026]")
									.Append(buffer[buffer.Length - 1])
									.ToString();
						}
					}
					catch (System.Text.DecoderFallbackException)
					{
						// sometimes, binary data "looks" like valid JSON but is not, so we just ignore it (even if we may have done a bunch of work for nothing)
					}
				}
			}

			// do a first path on the span to look for binary of possible text
			bool mustEscape = false;
			int n = buffer.Length;
			int p = 0;
			while (n-- > 0)
			{
				byte b = buffer[p++];
				if (b >= 32 && b < 127) continue;

				// we accept via escaping the following special chars: CR, LF, TAB
				if (b == 0 || b == 10 || b == 13 || b == 9)
				{
					mustEscape = true;
					continue;
				}

				//TODO: are there any chars above 128 that could be accepted ?

				// this looks like binary
				return Slice.Dump(buffer, maxLen);
			}

			if (!mustEscape)
			{ // only printable chars found
#if USE_SPAN_API
				if (buffer.Length <= maxLen)
				{
					return "'" + Encoding.ASCII.GetString(buffer) + "'";
				}
				else
				{
					return "'" + Encoding.ASCII.GetString(buffer.Slice(0, maxLen)) + "[\u2026]'"; // Unicode for '...'
				}
#else
				unsafe
				{
					fixed (byte* ptr = buffer)
					{
						if (buffer.Length <= maxLen)
						{
							return "'" + Encoding.ASCII.GetString(ptr, buffer.Length) + "'";
						}
						else
						{
							return "'" + Encoding.ASCII.GetString(ptr, maxLen) + "[\u2026]'"; // Unicode for '...'
						}
					}
				}
#endif
			}
			// some escaping required
			if (buffer.Length <= maxLen)
			{
				return EscapeString(new StringBuilder(buffer.Length + 2).Append('\''), buffer, Slice.Utf8NoBomEncoding).Append('\'').ToString();
			}
			else
			{
				return EscapeString(new StringBuilder(buffer.Length + 2).Append('\''), buffer.Slice(0, maxLen), Slice.Utf8NoBomEncoding).Append("[\u2026]'").ToString();
			}
		}

		/// <summary>Converts a span into a byte</summary>
		/// <returns>Value of the first and only byte of the span, or 0 if the span is empty.</returns>
		/// <exception cref="System.FormatException">If the span has more than one byte</exception>
		[Pure]
		public static byte ToByte(this ReadOnlySpan<byte> span)
		{
			switch (span.Length)
			{
				case 0: return 0;
				case 1: return span[0];
				default:
				if (span.Length < 0) throw UnsafeHelpers.Errors.SliceCountNotNeg();
				return UnsafeHelpers.Errors.ThrowSliceTooLargeForConversion<byte>(1);
			}
		}

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static byte ToByte(this Span<byte> span) => ToByte((ReadOnlySpan<byte>) span);

		/// <summary>Converts a span into a signed byte (-128..+127)</summary>
		/// <returns>Value of the first and only byte of the span, or 0 if the span is empty.</returns>
		/// <exception cref="System.FormatException">If the span has more than one byte</exception>
		[Pure]
		public static sbyte ToSByte(this ReadOnlySpan<byte> span)
		{
			switch (span.Length)
			{
				case 0: return 0;
				case 1: return (sbyte) span[0];
				default:
				if (span.Length < 0) throw UnsafeHelpers.Errors.SliceCountNotNeg();
				return UnsafeHelpers.Errors.ThrowSliceTooLargeForConversion<sbyte>(1);
			}
		}

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static sbyte ToSByte(this Span<byte> span) => ToSByte((ReadOnlySpan<byte>) span);

		/// <summary>Converts a span into a boolean.</summary>
		/// <returns>False if the span is empty, or is equal to the byte 0; otherwise, true.</returns>
		[Pure]
		public static bool ToBool(this ReadOnlySpan<byte> span)
		{
			// Anything appart from nil/empty, or the byte 0 itself is considered truthy.
			return span.Length > 1 || (span.Length == 1 && span[0] != 0);
			//TODO: consider checking if the span consist of only zeroes ? (ex: Slice.FromFixed32(0) could be considered falsy ...)
		}

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool ToBool(this Span<byte> span) => ToBool((ReadOnlySpan<byte>) span);

		#region 16 bits...

		/// <summary>Converts a span into a little-endian encoded, signed 16-bit integer.</summary>
		/// <returns>0 of the span is empty, a signed integer, or an error if the span has more than 2 bytes</returns>
		/// <exception cref="System.FormatException">If there are more than 2 bytes in the span</exception>
		[Pure]
		public static short ToInt16(this ReadOnlySpan<byte> span)
		{
			switch (span.Length)
			{
				case 0: return 0;
				case 1: return span[0];
				case 2: return MemoryMarshal.Read<short>(span);
				default:
				if (span.Length < 0) throw UnsafeHelpers.Errors.SliceCountNotNeg();
				return UnsafeHelpers.Errors.ThrowSliceTooLargeForConversion<short>(2);
			}
		}

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static short ToInt16(this Span<byte> span) => ToInt16((ReadOnlySpan<byte>) span);

		/// <summary>Converts a span into a big-endian encoded, signed 16-bit integer.</summary>
		/// <returns>0 of the span is empty, a signed integer, or an error if the span has more than 2 bytes</returns>
		/// <exception cref="System.FormatException">If there are more than 2 bytes in the span</exception>
		[Pure]
		public static short ToInt16BE(this ReadOnlySpan<byte> span)
		{
			switch (span.Length)
			{
				case 0: return 0;
				case 1: return span[0];
				case 2: return (short) (span[1] | (span[0] << 8));
				default:
				if (span.Length < 0) throw UnsafeHelpers.Errors.SliceCountNotNeg();
				return UnsafeHelpers.Errors.ThrowSliceTooLargeForConversion<short>(2);

			}
		}

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static short ToInt16BE(this Span<byte> span) => ToInt16BE((ReadOnlySpan<byte>) span);

		/// <summary>Converts a span into a little-endian encoded, unsigned 16-bit integer.</summary>
		/// <returns>0 of the span is empty, an unsigned integer, or an error if the span has more than 2 bytes</returns>
		/// <exception cref="System.FormatException">If there are more than 2 bytes in the span</exception>
		[Pure]
		public static ushort ToUInt16(this ReadOnlySpan<byte> span)
		{
			switch (span.Length)
			{
				case 0: return 0;
				case 1: return span[0];
				case 2: return MemoryMarshal.Read<ushort>(span);
				default:
				if (span.Length < 0) throw UnsafeHelpers.Errors.SliceCountNotNeg();
				return UnsafeHelpers.Errors.ThrowSliceTooLargeForConversion<ushort>(2);
			}
		}

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static ushort ToUInt16(this Span<byte> span) => ToUInt16((ReadOnlySpan<byte>) span);

		/// <summary>Converts a span into a little-endian encoded, unsigned 16-bit integer.</summary>
		/// <returns>0 of the span is empty, an unsigned integer, or an error if the span has more than 2 bytes</returns>
		/// <exception cref="System.FormatException">If there are more than 2 bytes in the span</exception>
		[Pure]
		public static ushort ToUInt16BE(this ReadOnlySpan<byte> span)
		{
			switch (span.Length)
			{
				case 0: return 0;
				case 1: return span[0];
				case 2: return (ushort) (span[1] | (span[0] << 8));
				default:
				if (span.Length < 0) throw UnsafeHelpers.Errors.SliceCountNotNeg();
				return UnsafeHelpers.Errors.ThrowSliceTooLargeForConversion<ushort>(2);

			}
		}

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static ushort ToUInt16BE(this Span<byte> span) => ToUInt16BE((ReadOnlySpan<byte>) span);

		#endregion

		#region 24 bits...

		//note: all 'Int24' and 'UInt24' are represented in memory as Int32/UInt32 using only the lowest 24 bits (upper 8 bits will be IGNORED)
		//note: 'FF FF' is equivalent to '00 FF FF', so is considered to be positive (= 65535)

		/// <summary>Converts a span into a little-endian encoded, signed 24-bit integer.</summary>
		/// <returns>0 of the span is empty, a signed integer, or an error if the span has more than 3 bytes</returns>
		/// <exception cref="System.FormatException">If there are more than 3 bytes in the span</exception>
		[Pure]
		public static int ToInt24(this ReadOnlySpan<byte> span)
		{
			int count = span.Length;
			if (count == 0) return 0;
			unsafe
			{
				fixed (byte* ptr = span)
				{
					switch (count)
					{
						case 1: return *ptr;
						case 2: return UnsafeHelpers.LoadUInt16LE(ptr); // cannot be negative
						case 3: return UnsafeHelpers.LoadInt24LE(ptr);
					}
				}
			}
			if (count < 0) UnsafeHelpers.Errors.ThrowSliceCountNotNeg();
			return UnsafeHelpers.Errors.ThrowSliceTooLargeForConversion<int>(3);
		}

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static int ToInt24(this Span<byte> span) => ToInt24((ReadOnlySpan<byte>) span);

		/// <summary>Converts a span into a big-endian encoded, signed 24-bit integer.</summary>
		/// <returns>0 of the span is empty, a signed integer, or an error if the span has more than 3 bytes</returns>
		/// <exception cref="System.FormatException">If there are more than 3 bytes in the span</exception>
		[Pure]
		public static int ToInt24BE(this ReadOnlySpan<byte> span)
		{
			int count = span.Length;
			if (count == 0) return 0;
			unsafe
			{
				fixed (byte* ptr = span)
				{
					switch (count)
					{
						case 1: return *ptr;
						case 2: return UnsafeHelpers.LoadUInt16BE(ptr);
						case 3: return UnsafeHelpers.LoadInt24BE(ptr);
					}
				}
			}
			if (count < 0) UnsafeHelpers.Errors.ThrowSliceCountNotNeg();
			return UnsafeHelpers.Errors.ThrowSliceTooLargeForConversion<int>(3);
		}

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static int ToInt24BE(this Span<byte> span) => ToInt24BE((ReadOnlySpan<byte>) span);

		/// <summary>Converts a span into a little-endian encoded, unsigned 24-bit integer.</summary>
		/// <returns>0 of the span is empty, an unsigned integer, or an error if the span has more than 3 bytes</returns>
		/// <exception cref="System.FormatException">If there are more than 3 bytes in the span</exception>
		[Pure]
		public static uint ToUInt24(this ReadOnlySpan<byte> span)
		{
			int count = span.Length;
			if (count == 0) return 0;
			unsafe
			{
				fixed (byte* ptr = span)
				{
					switch (count)
					{
						case 1: return *ptr;
						case 2: return UnsafeHelpers.LoadUInt16LE(ptr);
						case 3: return UnsafeHelpers.LoadUInt24LE(ptr);
					}
				}
			}
			if (count < 0) UnsafeHelpers.Errors.ThrowSliceCountNotNeg();
			return UnsafeHelpers.Errors.ThrowSliceTooLargeForConversion<uint>(3);
		}

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static uint ToUInt24(this Span<byte> span) => ToUInt24((ReadOnlySpan<byte>) span);

		/// <summary>Converts a span into a little-endian encoded, unsigned 24-bit integer.</summary>
		/// <returns>0 of the span is empty, an unsigned integer, or an error if the span has more than 3 bytes</returns>
		/// <exception cref="System.FormatException">If there are more than 3 bytes in the span</exception>
		[Pure]
		public static uint ToUInt24BE(this ReadOnlySpan<byte> span)
		{
			int count = span.Length;
			if (count == 0) return 0;
			unsafe
			{
				fixed (byte* ptr = span)
				{
					switch (count)
					{
						case 1: return *ptr;
						case 2: return UnsafeHelpers.LoadUInt16BE(ptr);
						case 3: return UnsafeHelpers.LoadUInt24BE(ptr);
					}
				}
			}
			if (count < 0) UnsafeHelpers.Errors.ThrowSliceCountNotNeg();
			return UnsafeHelpers.Errors.ThrowSliceTooLargeForConversion<uint>(3);
		}

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static uint ToUInt24BE(this Span<byte> span) => ToUInt24BE((ReadOnlySpan<byte>) span);

		#endregion

		#region 32 bits...

		/// <summary>Converts a span into a little-endian encoded, signed 32-bit integer.</summary>
		/// <returns>0 of the span is empty, a signed integer, or an error if the span has more than 4 bytes</returns>
		/// <exception cref="System.FormatException">If there are more than 4 bytes in the span</exception>
		[Pure]
		public static int ToInt32(this ReadOnlySpan<byte> span)
		{
			switch (span.Length) // if negative, will throw in the default case below
			{
				case 0: return 0;
				case 1: return span[0];
				case 2: return MemoryMarshal.Read<ushort>(span);
				case 3: return span[0] | (span[1] << 8) | (span[2] << 16);
				case 4: return MemoryMarshal.Read<int>(span);
				default:
				{
					if (span.Length < 0) throw UnsafeHelpers.Errors.SliceCountNotNeg();
					return UnsafeHelpers.Errors.ThrowSliceTooLargeForConversion<int>(4);
				}
			}
		}

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static int ToInt32(this Span<byte> span) => ToInt32((ReadOnlySpan<byte>) span);

		/// <summary>Converts a span into a big-endian encoded, signed 32-bit integer.</summary>
		/// <returns>0 of the span is empty, a signed integer, or an error if the span has more than 4 bytes</returns>
		/// <exception cref="System.FormatException">If there are more than 4 bytes in the span</exception>
		[Pure]
		public static int ToInt32BE(this ReadOnlySpan<byte> span)
		{
			switch (span.Length) // if negative, will throw in the default case below
			{
				case 0: return 0;
				case 1: return span[0];
				case 2: return (span[0] << 8) | span[1];
				case 3: return (span[0] << 16) | (span[1] << 8) | span[2];
				case 4: return (span[0] << 24) | (span[1] << 16) | (span[2] << 8) | span[3];
				default:
				{
					if (span.Length < 0) UnsafeHelpers.Errors.ThrowSliceCountNotNeg();
					return UnsafeHelpers.Errors.ThrowSliceTooLargeForConversion<int>(4);
				}
			}
		}

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static int ToInt32BE(this Span<byte> span) => ToInt32BE((ReadOnlySpan<byte>) span);

		/// <summary>Converts a span into a little-endian encoded, unsigned 32-bit integer.</summary>
		/// <returns>0 of the span is empty, an unsigned integer, or an error if the span has more than 4 bytes</returns>
		/// <exception cref="System.FormatException">If there are more than 4 bytes in the span</exception>
		[Pure]
		public static uint ToUInt32(this ReadOnlySpan<byte> span)
		{
			switch (span.Length) // if negative, will throw in the default case below
			{
				case 0: return 0;
				case 1: return span[0];
				case 2: return MemoryMarshal.Read<ushort>(span);
				case 3: return (uint) (span[0] | (span[1] << 8) | (span[2] << 16));
				case 4: return MemoryMarshal.Read<uint>(span);
				default:
				{
					if (span.Length < 0) UnsafeHelpers.Errors.ThrowSliceCountNotNeg();
					return UnsafeHelpers.Errors.ThrowSliceTooLargeForConversion<uint>(4);
				}
			}
		}

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static uint ToUInt32(this Span<byte> span) => ToUInt32((ReadOnlySpan<byte>) span);

		/// <summary>Converts a span into a big-endian encoded, unsigned 32-bit integer.</summary>
		/// <returns>0 of the span is empty, an unsigned integer, or an error if the span has more than 4 bytes</returns>
		/// <exception cref="System.FormatException">If there are more than 4 bytes in the span</exception>
		[Pure]
		public static uint ToUInt32BE(this ReadOnlySpan<byte> span)
		{
			switch (span.Length) // if negative, will throw in the default case below
			{
				case 0: return 0;
				case 1: return span[0];
				case 2: return (uint) ((span[0] << 8) | span[1]);
				case 3: return (uint) ((span[0] << 16) | (span[1] << 8) | span[2]);
				case 4: return (uint) ((span[0] << 24) | (span[1] << 16) | (span[2] << 8) | span[3]);
				default:
				{
					if (span.Length < 0) UnsafeHelpers.Errors.ThrowSliceCountNotNeg();
					return UnsafeHelpers.Errors.ThrowSliceTooLargeForConversion<uint>(4);
				}
			}
		}

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static uint ToUInt32BE(this Span<byte> span) => ToUInt32BE((ReadOnlySpan<byte>) span);

		#endregion

		#region 64 bits...

		/// <summary>Converts a span into a little-endian encoded, signed 64-bit integer.</summary>
		/// <returns>0 of the span is empty, a signed integer, or an error if the span has more than 8 bytes</returns>
		/// <exception cref="System.FormatException">If there are more than 8 bytes in the span</exception>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static long ToInt64(this ReadOnlySpan<byte> span)
		{
			switch (span.Length)
			{
				case 0: return 0;
				case 1: return span[0];
				case 2: return MemoryMarshal.Read<ushort>(span);
				case 3: return span[0] | (span[1] << 8) | (span[2] << 16);
				case 4: return MemoryMarshal.Read<uint>(span);
				case 8: return MemoryMarshal.Read<long>(span);
				default: return ToInt64Slow(span);
			}
		}

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static long ToInt64(this Span<byte> span) => ToInt64((ReadOnlySpan<byte>) span);

		[Pure]
		private static long ToInt64Slow(ReadOnlySpan<byte> span)
		{
			int n = span.Length;
			if ((uint) n > 8) goto fail;

			int p = n - 1;
			long value = span[p--];
			while (--n > 0)
			{
				value = (value << 8) | span[p--];
			}

			return value;
		fail:
			throw new FormatException("Cannot convert span into an Int64 because it is larger than 8 bytes.");
		}

		/// <summary>Converts a span into a big-endian encoded, signed 64-bit integer.</summary>
		/// <returns>0 of the span is empty, a signed integer, or an error if the span has more than 8 bytes</returns>
		/// <exception cref="System.FormatException">If there are more than 8 bytes in the span</exception>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static long ToInt64BE(this ReadOnlySpan<byte> span)
		{
			return span.Length <= 4 ? ToInt32BE(span) : ToInt64BESlow(span);
		}

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static long ToInt64BE(this Span<byte> span) => ToInt64BE((ReadOnlySpan<byte>) span);

		[Pure]
		private static long ToInt64BESlow(ReadOnlySpan<byte> span)
		{
			int n = span.Length;
			if (n == 0) return 0L;
			if ((uint) n > 8) goto fail;

			int p = 0;
			long value = span[p++];
			while (--n > 0)
			{
				value = (value << 8) | span[p++];
			}
			return value;
		fail:
			throw new FormatException("Cannot convert span into an Int64 because it is larger than 8 bytes.");
		}

		/// <summary>Converts a span into a little-endian encoded, unsigned 64-bit integer.</summary>
		/// <returns>0 of the span is empty, an unsigned integer, or an error if the span has more than 8 bytes</returns>
		/// <exception cref="System.FormatException">If there are more than 8 bytes in the span</exception>
		[Pure]
		public static ulong ToUInt64(this ReadOnlySpan<byte> span)
		{
			switch (span.Length)
			{
				case 0:  return 0;
				case 1:  return span[0];
				case 2:  return MemoryMarshal.Read<ushort>(span);
				case 3:  return (ulong) (span[0] | (span[1] << 8) | (span[2] << 16));
				case 4:  return MemoryMarshal.Read<uint>(span);
				case 8:  return MemoryMarshal.Read<ulong>(span);
				default: return ToUInt64Slow(span);
			}
		}

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static ulong ToUInt64(this Span<byte> span) => ToUInt64((ReadOnlySpan<byte>) span);

		private static ulong ToUInt64Slow(ReadOnlySpan<byte> span)
		{
			int n = span.Length;
			if (n == 0) return 0L;
			if ((uint) n > 8) goto fail;

			int p = n - 1;
			ulong value = span[p--];
			while (--n > 0)
			{
				value = (value << 8) | span[p--];
			}
			return value;
		fail:
			throw new FormatException("Cannot convert span into an UInt64 because it is larger than 8 bytes.");
		}

		/// <summary>Converts a span into a little-endian encoded, unsigned 64-bit integer.</summary>
		/// <returns>0 of the span is empty, an unsigned integer, or an error if the span has more than 8 bytes</returns>
		/// <exception cref="System.FormatException">If there are more than 8 bytes in the span</exception>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static ulong ToUInt64BE(this ReadOnlySpan<byte> span) => span.Length <= 4 ? ToUInt32BE(span) : ToUInt64BESlow(span);

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static ulong ToUInt64BE(this Span<byte> span) => ToUInt64BE((ReadOnlySpan<byte>) span);

		private static ulong ToUInt64BESlow(ReadOnlySpan<byte> span)
		{
			int n = span.Length;
			if (n == 0) return 0L;
			if ((uint) n > 8) goto fail;

			int p = 0;
			ulong value = span[p++];
			while (--n > 0)
			{
				value = (value << 8) | span[p++];
			}
			return value;
		fail:
			throw new FormatException("Cannot convert span into an UInt64 because it is larger than 8 bytes.");
		}

		/// <summary>Converts a span into a 64-bit UUID.</summary>
		/// <returns>Uuid decoded from the span.</returns>
		/// <remarks>The span can either be an 8-byte array, or an ASCII string of 16, 17 or 19 chars</remarks>
		[Pure]
		public static Uuid64 ToUuid64(this ReadOnlySpan<byte> span)
		{
			if (span.Length == 0) return default;

			switch (span.Length)
			{
				case 8:
				{ // binary (8 bytes)
					return Uuid64.Read(span);
				}

				case 16: // hex16
				case 17: // hex8-hex8
				case 19: // {hex8-hex8}
				{
					// ReSharper disable once AssignNullToNotNullAttribute
					return Uuid64.Parse(ToByteString(span)!);
				}
			}

			throw new FormatException("Cannot convert span into an Uuid64 because it has an incorrect size.");
		}

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static Uuid64 ToUuid64(this Span<byte> span) => ToUuid64((ReadOnlySpan<byte>) span);

		#endregion

		#region Floating Point...

		/// <summary>Converts a span into a 32-bit IEEE floating point.</summary>
		/// <returns>0 of the span is empty, an unsigned integer, or an error if the span has more than 4 bytes</returns>
		/// <exception cref="System.FormatException">If there are less or more than 4 bytes in the span</exception>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static float ToSingle(this ReadOnlySpan<byte> span) => span.Length == 0 ? 0f : span.Length == 4 ? MemoryMarshal.Read<float>(span) : FailSingleInvalidSize();

		/// <summary>Converts a span into a 32-bit IEEE floating point.</summary>
		/// <returns>0 of the span is empty, an unsigned integer, or an error if the span has more than 4 bytes</returns>
		/// <exception cref="System.FormatException">If there are less or more than 4 bytes in the span</exception>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static float ToSingle(this Span<byte> span) => span.Length == 0 ? 0f : span.Length == 4 ? MemoryMarshal.Read<float>(span) : FailSingleInvalidSize();

		/// <summary>Converts a span into a 32-bit IEEE floating point (in network order).</summary>
		/// <returns>0 of the span is empty, an unsigned integer, or an error if the span has more than 4 bytes</returns>
		/// <exception cref="System.FormatException">If there are less or more than 4 bytes in the span</exception>
		[Pure]
		public static float ToSingleBE(this ReadOnlySpan<byte> span)
		{
			if (span.Length == 0) return 0f;
			if (span.Length != 4) return FailSingleInvalidSize();

			unsafe
			{
				fixed (byte* ptr = span)
				{
					uint tmp = UnsafeHelpers.ByteSwap32(*(uint*) ptr);
					return *((float*) &tmp);
				}
			}
		}

		/// <summary>Converts a span into a 32-bit IEEE floating point (in network order).</summary>
		/// <returns>0 of the span is empty, an unsigned integer, or an error if the span has more than 4 bytes</returns>
		/// <exception cref="System.FormatException">If there are less or more than 4 bytes in the span</exception>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static float ToSingleBE(this Span<byte> span) => ToSingleBE((ReadOnlySpan<byte>) span);

		[MethodImpl(MethodImplOptions.NoInlining)]
		private static float FailSingleInvalidSize() => throw new FormatException("Cannot convert span into a Single because it is not exactly 4 bytes long.");

		/// <summary>Converts a span into a 64-bit IEEE floating point.</summary>
		/// <returns>0 of the span is empty, an unsigned integer, or an error if the span has more than 8 bytes</returns>
		/// <exception cref="System.FormatException">If there are less or more than 8 bytes in the span</exception>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static double ToDouble(this ReadOnlySpan<byte> span) => span.Length == 0 ? 0d : span.Length == 8 ? MemoryMarshal.Read<double>(span) : FailDoubleInvalidSize();

		/// <summary>Converts a span into a 64-bit IEEE floating point.</summary>
		/// <returns>0 of the span is empty, an unsigned integer, or an error if the span has more than 8 bytes</returns>
		/// <exception cref="System.FormatException">If there are less or more than 8 bytes in the span</exception>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static double ToDouble(this Span<byte> span) => span.Length == 0 ? 0d : span.Length == 8 ? MemoryMarshal.Read<double>(span) : FailDoubleInvalidSize();

		/// <summary>Converts a span into a 64-bit IEEE floating point (in network order).</summary>
		/// <returns>0 of the span is empty, an unsigned integer, or an error if the span has more than 8 bytes</returns>
		/// <exception cref="System.FormatException">If there are less or more than 8 bytes in the span</exception>
		[Pure]
		public static double ToDoubleBE(this ReadOnlySpan<byte> span)
		{
			if (span.Length == 0) return 0d;
			if (span.Length != 8) return FailDoubleInvalidSize();

			unsafe
			{
				fixed (byte* ptr = span)
				{
					ulong tmp = UnsafeHelpers.ByteSwap64(*(ulong*) ptr);
					return *((double*) &tmp);
				}
			}
		}

		/// <summary>Converts a span into a 64-bit IEEE floating point (in network order).</summary>
		/// <returns>0 of the span is empty, an unsigned integer, or an error if the span has more than 8 bytes</returns>
		/// <exception cref="System.FormatException">If there are less or more than 8 bytes in the span</exception>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static double ToDoubleBE(this Span<byte> span) => ToDoubleBE((ReadOnlySpan<byte>) span);

		[MethodImpl(MethodImplOptions.NoInlining)]
		private static double FailDoubleInvalidSize() => throw new FormatException("Cannot convert span into a Double because it is not exactly 8 bytes long.");

		/// <summary>Converts a span into a 128-bit IEEE floating point.</summary>
		/// <returns>0 of the span is empty, an unsigned integer, or an error if the span has more than 8 bytes</returns>
		/// <exception cref="System.FormatException">If there are less or more than 8 bytes in the span</exception>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static decimal ToDecimal(this ReadOnlySpan<byte> span) => span.Length == 0 ? 0m : span.Length == 16 ? MemoryMarshal.Read<decimal>(span) : FailDecimalInvalidSize();

		/// <summary>Converts a span into a 128-bit IEEE floating point.</summary>
		/// <returns>0 of the span is empty, an unsigned integer, or an error if the span has more than 8 bytes</returns>
		/// <exception cref="System.FormatException">If there are less or more than 8 bytes in the span</exception>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static decimal ToDecimal(this Span<byte> span) => span.Length == 0 ? 0m : span.Length == 16 ? MemoryMarshal.Read<decimal>(span) : FailDecimalInvalidSize();

		[MethodImpl(MethodImplOptions.NoInlining)]
		private static decimal FailDecimalInvalidSize() => throw new FormatException("Cannot convert span into a Decimal because it is not exactly 16 bytes long.");

		//TODO: Decimal big-endian

		#endregion

		#region 128 bits...

		/// <summary>Converts a span into a Guid.</summary>
		/// <returns>Native Guid decoded from the span.</returns>
		/// <remarks>The span can either be a 16-byte RFC4122 GUID, or an ASCII string of 36 chars</remarks>
		[Pure]
		public static Guid ToGuid(this ReadOnlySpan<byte> span)
		{
			switch (span.Length)
			{
				case 0:
				{
					return default;
				}
				case 16:
				{ // direct byte array

					// UUID are stored using the RFC4122 format (Big Endian), while .NET's System.GUID use Little Endian
					// we need to swap the byte order of the Data1, Data2 and Data3 chunks, to ensure that Guid.ToString() will return the proper value.

					return new Uuid128(span).ToGuid();
				}
				case 36:
				{ // string representation (ex: "da846709-616d-4e82-bf55-d1d3e9cde9b1")
					// ReSharper disable once AssignNullToNotNullAttribute
					return Guid.Parse(ToByteString(span));
				}
				default:
				{
					throw new FormatException("Cannot convert span into a Guid because it has an incorrect size.");
				}
			}
		}

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static Guid ToGuid(this Span<byte> span) => ToGuid((ReadOnlySpan<byte>) span);

		/// <summary>Converts a span into a 128-bit UUID.</summary>
		/// <returns>Uuid decoded from the span.</returns>
		/// <remarks>The span can either be a 16-byte RFC4122 GUID, or an ASCII string of 36 chars</remarks>
		[Pure]
		public static Uuid128 ToUuid128(this ReadOnlySpan<byte> span)
		{
			return span.Length switch
			{
				0 => default,
				16 => new Uuid128(span),
				36 => Uuid128.Parse(ToByteString(span)!),
				_ => throw new FormatException("Cannot convert span into an Uuid128 because it has an incorrect size.")
			};
		}

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static Uuid128 ToUuid128(this Span<byte> span) => ToUuid128((ReadOnlySpan<byte>) span);

		#endregion

		#region 80 bits...

		/// <summary>Converts a span into a 64-bit UUID.</summary>
		/// <returns>Uuid decoded from the span.</returns>
		/// <remarks>The span can either be an 10-byte array, or an ASCII string of 20, 22 or 24 chars</remarks>
		[Pure]
		public static Uuid80 ToUuid80(this ReadOnlySpan<byte> span)
		{
			return span.Length switch
			{
				0 => default,

				// binary (10 bytes)
				10 => Uuid80.Read(span),
				// XXXXXXXXXXXXXXXXXXXX
				20 => Uuid80.Parse(ToByteString(span)!),
				// XXXX-XXXXXXXX-XXXXXXXX
				22 => Uuid80.Parse(ToByteString(span)!),
				// {XXXX-XXXXXXXX-XXXXXXXX}
				24 => Uuid80.Parse(ToByteString(span)!),

				_ => throw new FormatException("Cannot convert span into an Uuid80 because it has an incorrect size.")
			};
		}

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static Uuid80 ToUuid80(this Span<byte> span) => ToUuid80((ReadOnlySpan<byte>) span);

		#endregion

		#region 96 bits...

		/// <summary>Converts a span into a 64-bit UUID.</summary>
		/// <returns>Uuid decoded from the span.</returns>
		/// <remarks>The span can either be an 12-byte array, or an ASCII string of 24, 26 or 28 chars</remarks>
		[Pure]
		public static Uuid96 ToUuid96(this ReadOnlySpan<byte> span)
		{
			if (span.Length == 0) return default;

			switch (span.Length)
			{
				case 12:
				{ // binary (12 bytes)
					return Uuid96.Read(span);
				}

				case 24: // XXXXXXXXXXXXXXXXXXXXXXXX
				case 26: // XXXXXXXX-XXXXXXXX-XXXXXXXX
				case 28: // {XXXXXXXX-XXXXXXXX-XXXXXXXX}
				{
					// ReSharper disable once AssignNullToNotNullAttribute
					return Uuid96.Parse(ToByteString(span)!);
				}
			}

			throw new FormatException("Cannot convert span into an Uuid96 because it has an incorrect size.");
		}

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static Uuid96 ToUuid96(this Span<byte> span) => ToUuid96((ReadOnlySpan<byte>) span);

		#endregion

	}

}

#endif
