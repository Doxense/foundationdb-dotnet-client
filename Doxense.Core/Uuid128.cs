#region Copyright (c) 2023-2024 SnowBank SAS, (c) 2005-2023 Doxense SAS
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

namespace System
{
	using System.Buffers.Binary;
	using System.ComponentModel;
	using System.Runtime.InteropServices;
	using Doxense.Memory;
	using Doxense.Serialization;
#if NET8_0_OR_GREATER
	using System.Buffers.Text;
#endif

	/// <summary>Represents an RFC 4122 compliant 128-bit UUID</summary>
	/// <remarks>You should use this type if you are primarily exchanging UUIDs with non-.NET platforms, that use the RFC 4122 byte ordering (big endian). The type System.Guid uses the Microsoft encoding (little endian) and is not compatible.</remarks>
	[DebuggerDisplay("[{ToString(),nq}]")]
	[ImmutableObject(true), StructLayout(LayoutKind.Explicit), PublicAPI, Serializable]
	public readonly struct Uuid128 : IComparable, IEquatable<Uuid128>, IComparable<Uuid128>, IEquatable<Guid>, ISliceSerializable, ISpanFormattable
#if NET8_0_OR_GREATER
		, ISpanParsable<Uuid128>
		, IUtf8SpanFormattable
		, IUtf8SpanParsable<Uuid128>
#endif
	{
		// This is just a wrapper struct on System.Guid that makes sure that ToByteArray() and Parse(byte[]) and new(byte[]) will parse according to RFC 4122 (http://www.ietf.org/rfc/rfc4122.txt)
		// For performance reasons, we will store the UUID as a System.GUID (Microsoft in-memory format), and swap the bytes when needed.

		// cf 4.1.2. Layout and Byte Order

		//    The fields are encoded as 16 octets, with the sizes and order of the
		//    fields defined above, and with each field encoded with the Most
		//    Significant Byte first (known as network byte order).  Note that the
		//    field names, particularly for multiplexed fields, follow historical
		//    practice.

		//    0                   1                   2                   3
		//    0 1 2 3 4 5 6 7 8 9 0 1 2 3 4 5 6 7 8 9 0 1 2 3 4 5 6 7 8 9 0 1
		//    +-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+
		//    |                          time_low                             |
		//    +-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+
		//    |       time_mid                |         time_hi_and_version   |
		//    +-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+
		//    |clk_seq_hi_res |  clk_seq_low  |         node (0-1)            |
		//    +-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+
		//    |                         node (2-5)                            |
		//    +-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+

		// UUID "view"

		[FieldOffset(0)]
		private readonly uint m_timeLow;
		[FieldOffset(4)]
		private readonly ushort m_timeMid;
		[FieldOffset(6)]
		private readonly ushort m_timeHiAndVersion;
		[FieldOffset(8)]
		private readonly byte m_clkSeqHiRes;
		[FieldOffset(9)]
		private readonly byte m_clkSeqLow;
		[FieldOffset(10)]
		private readonly byte m_node0;
		[FieldOffset(11)]
		private readonly byte m_node1;
		[FieldOffset(12)]
		private readonly byte m_node2;
		[FieldOffset(13)]
		private readonly byte m_node3;
		[FieldOffset(14)]
		private readonly byte m_node4;
		[FieldOffset(15)]
		private readonly byte m_node5;

		// packed "view"

		[FieldOffset(0)]
		private readonly Guid m_packed;

		#region Constructors...

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public Uuid128(Guid guid) : this() => m_packed = guid;

		public Uuid128(string value) : this(new Guid(value)) { }

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public Uuid128(Slice slice) : this() => m_packed = Convert(slice.Span);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public Uuid128(byte[] bytes) : this() => m_packed = Convert(bytes.AsSpan());

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public Uuid128(ReadOnlySpan<byte> bytes) : this() => m_packed = Convert(bytes);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public Uuid128(int a, short b, short c, byte[] d)
			: this(new Guid(a, b, c, d))
		{ }

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public Uuid128(int a, short b, short c, byte d, byte e, byte f, byte g, byte h, byte i, byte j, byte k)
			: this(new Guid(a, b, c, d, e, f, g, h, i, j, k))
		{ }

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public Uuid128(uint a, ushort b, ushort c, byte d, byte e, byte f, byte g, byte h, byte i, byte j, byte k)
			: this(new Guid(a, b, c, d, e, f, g, h, i, j, k))
		{ }

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public Uuid128(Uuid64 a, Uuid64 b) : this() => m_packed = Convert(a, b);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public Uuid128(Uuid64 a, uint b, uint c) : this() => m_packed = Convert(a, b, c);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public Uuid128(ulong a, ulong b) : this() => m_packed = Convert(a, b);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static explicit operator Guid(Uuid128 uuid) => uuid.m_packed;

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static explicit operator Uuid128(Guid guid) => new(guid);

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static Uuid128 FromUInt32(uint low) => new(0, low);

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static Uuid128 FromUInt64(ulong low) => new(0, low);

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static Uuid128 FromUInt64(ulong high, ulong low) => new(high, low);

#if NET8_0_OR_GREATER

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public Uuid128(Int128 a) : this() => m_packed = Convert((UInt128) a);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public Uuid128(UInt128 a) : this() => m_packed = Convert(a);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static explicit operator Uuid128(Int128 a) => new(a);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static explicit operator Uuid128(UInt128 a) => new(a);

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static Uuid128 FromUInt128(UInt128 value) => new(value);

#endif

		/// <summary>Uuid128 with all bits set to zero: <c>"00000000-0000-0000-0000-000000000000"</c></summary>
		[EditorBrowsable(EditorBrowsableState.Always)]
		public static readonly Uuid128 Empty;

		/// <summary>Uuid128 with all bits set to one: <c>"FFFFFFFF-FFFF-FFFF-FFFF-FFFFFFFFFFFF"</c></summary>
		[EditorBrowsable(EditorBrowsableState.Always)]
		public static readonly Uuid128 AllBitsSet = new(new Guid(-1, -1, -1, byte.MaxValue, byte.MaxValue, byte.MaxValue, byte.MaxValue, byte.MaxValue, byte.MaxValue, byte.MaxValue, byte.MaxValue));

		/// <summary>Uuid128 with all bits set to one: <c>"FFFFFFFF-FFFF-FFFF-FFFF-FFFFFFFFFFFF"</c></summary>
		[EditorBrowsable(EditorBrowsableState.Never)]
		public static readonly Uuid128 MinValue;

		/// <summary>Uuid128 with all bits set to one: <c>"FFFFFFFF-FFFF-FFFF-FFFF-FFFFFFFFFFFF"</c></summary>
		[EditorBrowsable(EditorBrowsableState.Never)]
		public static readonly Uuid128 MaxValue = new(new Guid(-1, -1, -1, byte.MaxValue, byte.MaxValue, byte.MaxValue, byte.MaxValue, byte.MaxValue, byte.MaxValue, byte.MaxValue, byte.MaxValue));

		/// <summary>Size is 16 bytes</summary>
		public const int SizeOf = 16;

		[Pure, MethodImpl(MethodImplOptions.NoInlining)]
		private static Exception FailInvalidFormat() => ThrowHelper.FormatException($"Invalid {nameof(Uuid128)} format");


		/// <summary>Generate a new random 128-bit UUID.</summary>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static Uuid128 NewUuid()
		{
			return new Uuid128(Guid.NewGuid());
		}

		public static Guid Convert(Slice input)
		{
			if (input.Count == 0) return default;
			if (input.Count != 16) throw ThrowHelper.ArgumentException(nameof(input), "Slice for UUID must be exactly 16 bytes long");
			return Read(input.Span);
		}

		public static Guid Convert(ReadOnlySpan<byte> input)
		{
			if (input.Length == 0) return default;
			if (input.Length != 16) throw new ArgumentException("Slice for UUID must be exactly 16 bytes long");
			return Read(input);
		}

		public static Guid Convert(Uuid64 a, Uuid64 b)
		{
			unsafe
			{
				Span<byte> buf = stackalloc byte[SizeOf];
				BinaryPrimitives.WriteUInt64BigEndian(buf, a.ToUInt64());
				BinaryPrimitives.WriteUInt64BigEndian(buf[8..], b.ToUInt64());
				return Read(buf);
			}
		}

		public static Guid Convert(ulong a, ulong b)
		{
			unsafe
			{
				Span<byte> buf = stackalloc byte[SizeOf];
				BinaryPrimitives.WriteUInt64BigEndian(buf, a);
				BinaryPrimitives.WriteUInt64BigEndian(buf[8..], b);
				return Read(buf);
			}
		}

		public static Guid Convert(Uuid64 a, uint b, uint c)
		{
			unsafe
			{
				Span<byte> buf = stackalloc byte[16];
				a.WriteToUnsafe(buf);

				buf[8] = (byte) b;
				buf[9] = (byte)(b >> 8);
				buf[10] = (byte)(b >> 16);
				buf[11] = (byte)(b >> 24);

				buf[12] = (byte) c;
				buf[13] = (byte)(c >> 8);
				buf[14] = (byte)(c >> 16);
				buf[15] = (byte)(c >> 24);

				return Read(buf);
			}
		}

#if NET8_0_OR_GREATER

		public static Guid Convert(UInt128 a)
		{
			Span<byte> tmp = stackalloc byte[16];
			BinaryPrimitives.WriteUInt128BigEndian(tmp, a);
			return Convert(tmp);
		}

#endif

		#endregion

		#region Parsing...

		/// <summary>Parses a string into a <see cref="Uuid128"/></summary>
		/// <param name="input">The string to parse.</param>
		/// <exception cref="T:System.ArgumentNullException"><paramref name="input" /> is <see langword="null" />.</exception>
		/// <exception cref="T:System.FormatException"><paramref name="input" /> is not in the correct format.</exception>
		/// <exception cref="T:System.OverflowException"><paramref name="input" /> is not representable by a <see cref="Uuid128" />.</exception>
		/// <returns>The result of parsing <paramref name="input" />.</returns>
		public static Uuid128 Parse(string input) => new(Guid.Parse(input));

		/// <summary>Parses a string into a <see cref="Uuid128"/></summary>
		/// <param name="input">The string to parse.</param>
		/// <param name="provider">This argument is ignored.</param>
		/// <exception cref="T:System.ArgumentNullException"><paramref name="input" /> is <see langword="null" />.</exception>
		/// <exception cref="T:System.FormatException"><paramref name="input" /> is not in the correct format.</exception>
		/// <exception cref="T:System.OverflowException"><paramref name="input" /> is not representable by a <see cref="Uuid128" />.</exception>
		/// <returns>The result of parsing <paramref name="input" />.</returns>
		[EditorBrowsable(EditorBrowsableState.Never)]
		public static Uuid128 Parse(string input, IFormatProvider? provider) => new(Guid.Parse(input));

		/// <summary>Parses a span of characters into a <see cref="Uuid128"/></summary>
		/// <param name="input">The span of characters to parse.</param>
		/// <exception cref="T:System.FormatException"><paramref name="input" /> is not in the correct format.</exception>
		/// <exception cref="T:System.OverflowException"><paramref name="input" /> is not representable by a <see cref="Uuid128" />.</exception>
		/// <returns>The result of parsing <paramref name="input" />.</returns>
		public static Uuid128 Parse(ReadOnlySpan<char> input) => new(Guid.Parse(input));

		/// <summary>Parses a span of characters into a <see cref="Uuid128"/></summary>
		/// <param name="input">The span of characters to parse.</param>
		/// <param name="provider">This argument is ignored.</param>
		/// <exception cref="T:System.FormatException"><paramref name="input" /> is not in the correct format.</exception>
		/// <exception cref="T:System.OverflowException"><paramref name="input" /> is not representable by a <see cref="Uuid128" />.</exception>
		/// <returns>The result of parsing <paramref name="input" />.</returns>
		[EditorBrowsable(EditorBrowsableState.Never)]
		public static Uuid128 Parse(ReadOnlySpan<char> input, IFormatProvider? provider) => new(Guid.Parse(input));

#if NET8_0_OR_GREATER

		/// <summary>Parse a Base62 encoded string representation of an UUid64</summary>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static Uuid128 FromBase62(string buffer)
		{
			Contract.NotNull(buffer);
			return TryParseBase62(buffer.AsSpan(), out var value) ? value : throw FailInvalidFormat();
		}

		/// <summary>Parse a Base62 encoded string representation of an UUid64</summary>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static Uuid128 FromBase62(ReadOnlySpan<char> buffer)
		{
			return TryParseBase62(buffer, out var value) ? value : throw FailInvalidFormat();
		}

#endif

		/// <summary>Parses a span of UTF-8 characters into a <see cref="Uuid128"/></summary>
		/// <param name="utf8Text">The span of UTF-8 characters to parse.</param>
		/// <param name="provider">This argument is ignored.</param>
		/// <exception cref="T:System.FormatException"><paramref name="utf8Text" /> is not in the correct format.</exception>
		/// <exception cref="T:System.OverflowException"><paramref name="utf8Text" /> is not representable by a <see cref="Uuid128" />.</exception>
		/// <returns>The result of parsing <paramref name="utf8Text" />.</returns>
		public static Uuid128 Parse(ReadOnlySpan<byte> utf8Text, IFormatProvider? provider = null)
		{
			//TODO: REVIEW: there is currently (as of .NET 8) no overload for Guid.TryParse(RoS<byte>,...), so we have to use Utf8Parser.TryParse
			// => the issue is that is only returns false, without any hint on the actual error.

			// Guid.Parse/TryParse accept extra whitespaces (on both sides), so we have to trim...
			utf8Text = utf8Text.Trim(" \t\r\n"u8);

			if (utf8Text.Length == 38 && utf8Text[0] == '{' && utf8Text[37] == '}')
			{
				utf8Text = utf8Text[1..^1];
			}
			if (utf8Text.Length == 36)
			{
#if NET8_0_OR_GREATER
				if (!Utf8Parser.TryParse(utf8Text, out Guid g, out int consumed) || consumed != 36)
				{
					throw ThrowHelper.FormatException("Input is not a valid Uuid128 literal.");
				}
				return new(g);
#else
				// copy to a tmp buf and parse as string!
				if (UnsafeHelpers.IsAsciiBytes(utf8Text))
				{
					Span<char> tmp = stackalloc char[36];
					System.Text.Encoding.ASCII.GetChars(utf8Text, tmp);
					if (Guid.TryParse(tmp, out Guid g))
					{
						return new(g);
					}
				}
				throw ThrowHelper.FormatException("Input is not a valid Uuid128 literal.");
#endif
			}
			throw ThrowHelper.FormatException("Unrecognized Uuid128 format");
		}

		/// <summary>Tries to parse a span of UTF-8 characters into a <see cref="Uuid128"/>.</summary>
		/// <param name="utf8Text">The span of UTF-8 characters to parse.</param>
		/// <param name="provider">An object that provides culture-specific formatting information about <paramref name="utf8Text" />.</param>
		/// <param name="result">On return, contains the result of successfully parsing <paramref name="utf8Text" /> or an undefined value on failure.</param>
		/// <returns> <see langword="true" /> if <paramref name="utf8Text" /> was successfully parsed; otherwise, <see langword="false" />.</returns>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool TryParse(ReadOnlySpan<byte> utf8Text, IFormatProvider? provider, out Uuid128 result)
			=> TryParse(utf8Text, out result);

		/// <summary>Tries to parse a span of UTF-8 characters into a <see cref="Uuid128"/>.</summary>
		/// <param name="utf8Text">The span of UTF-8 characters to parse.</param>
		/// <param name="result">On return, contains the result of successfully parsing <paramref name="utf8Text" /> or an undefined value on failure.</param>
		/// <returns> <see langword="true" /> if <paramref name="utf8Text" /> was successfully parsed; otherwise, <see langword="false" />.</returns>
		public static bool TryParse(ReadOnlySpan<byte> utf8Text, out Uuid128 result)
		{
			//TODO: REVIEW: there is currently (as of .NET 8) no overload for Guid.TryParse(RoS<byte>,...), so we have to use Utf8Parser.TryParse
			// => the issue is that is only returns false, without any hint on the actual error.

			// Guid.Parse/TryParse accept extra whitespaces (on both sides), so we have to trim...
			utf8Text = utf8Text.Trim(" \t\r\n"u8);

			if (utf8Text.Length == 38 && utf8Text[0] == '{' && utf8Text[37] == '}')
			{
				utf8Text = utf8Text[1..^1];
			}
			if (utf8Text.Length == 36)
			{
#if NET8_0_OR_GREATER
				if (Utf8Parser.TryParse(utf8Text, out Guid g, out int consumed) && consumed == 36)
				{
					result = new(g);
					return true;
				}
#else
				// copy to a tmp buf and parse as string!
				if (UnsafeHelpers.IsAsciiBytes(utf8Text))
				{
					Span<char> tmp = stackalloc char[36];
					System.Text.Encoding.ASCII.GetChars(utf8Text, tmp);
					if (Guid.TryParse(tmp, out Guid g))
					{
						result = new(g);
						return true;
					}
				}
#endif
			}
			result = default;
			return false;
		}

		/// <summary>Parses a string representation of an UUid128</summary>
		public static Uuid128 ParseExact(
			string input,
#if NET8_0_OR_GREATER
			[StringSyntax(StringSyntaxAttribute.GuidFormat)]
#endif
			string format
		)
		{
#if NET8_0_OR_GREATER
			if (format.Length == 1)
			{
				var c = format[0] | 0x20;
				if (c == 'd') goto parse_guid;
				if (c == 'c') return input.Length is > 0 and <= 22 ? FromBase62(input) : throw FailInvalidFormat();
				if (c == 'z') return input.Length == 22 ? FromBase62(input) : throw FailInvalidFormat();
			}
		parse_guid:
#endif
			return new(Guid.ParseExact(input, format));
		}

		/// <summary>Parses a string representation of an UUid128</summary>
		public static Uuid128 ParseExact(
			ReadOnlySpan<char> input,
#if NET8_0_OR_GREATER
			[StringSyntax(StringSyntaxAttribute.GuidFormat)]
#endif
			ReadOnlySpan<char> format
		) => new(Guid.ParseExact(input, format));

		/// <summary>Tries to parse a string into a <see cref="Uuid128"/></summary>
		/// <param name="input">The string to parse.</param>
		/// <param name="result">When this method returns, contains the result of successfully parsing <paramref name="input" />, or an undefined value on failure.</param>
		/// <returns> <see langword="true" /> if <paramref name="input" /> was successfully parsed; otherwise, <see langword="false" />.</returns>
		public static bool TryParse(string? input, out Uuid128 result)
		{
			if (!Guid.TryParse(input, out Guid guid))
			{
				result = default;
				return false;
			}
			result = new Uuid128(guid);
			return true;
		}

		/// <summary>Tries to parse a string into a <see cref="Uuid128"/></summary>
		/// <param name="input">The string to parse.</param>
		/// <param name="provider">This argument is ignored.</param>
		/// <param name="result">When this method returns, contains the result of successfully parsing <paramref name="input" />, or an undefined value on failure.</param>
		/// <returns> <see langword="true" /> if <paramref name="input" /> was successfully parsed; otherwise, <see langword="false" />.</returns>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		[EditorBrowsable(EditorBrowsableState.Never)]
		public static bool TryParse(string? input, IFormatProvider? provider, out Uuid128 result)
			=> TryParse(input, out result);

		/// <summary>Tries to parse a span of characters into a <see cref="Uuid128"/></summary>
		/// <param name="input">The span of characters to parse.</param>
		/// <param name="result">When this method returns, contains the result of successfully parsing <paramref name="input" />, or an undefined value on failure.</param>
		/// <returns> <see langword="true" /> if <paramref name="input" /> was successfully parsed; otherwise, <see langword="false" />.</returns>
		public static bool TryParse(ReadOnlySpan<char> input, out Uuid128 result)
		{
			if (!Guid.TryParse(input, out var g))
			{
				result = default;
				return false;
			}

			result = new(g);
			return true;
		}

		/// <summary>Tries to parse a span of characters into a <see cref="Uuid128"/></summary>
		/// <param name="input">The span of characters to parse.</param>
		/// <param name="provider">This argument is ignored.</param>
		/// <param name="result">When this method returns, contains the result of successfully parsing <paramref name="input" />, or an undefined value on failure.</param>
		/// <returns> <see langword="true" /> if <paramref name="input" /> was successfully parsed; otherwise, <see langword="false" />.</returns>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		[EditorBrowsable(EditorBrowsableState.Never)]
		public static bool TryParse(ReadOnlySpan<char> input, IFormatProvider? provider, out Uuid128 result)
			=> TryParse(input, out result);

		/// <summary>Parse a string representation of an UUid128</summary>
		public static bool TryParseExact(string input, string format, out Uuid128 result)
		{
			if (!Guid.TryParseExact(input, format, out var guid))
			{
				result = default;
				return false;
			}
			result = new(guid);
			return true;
		}

		/// <summary>Parse a string representation of an UUid128</summary>
		public static bool TryParseExact(ReadOnlySpan<char> input, ReadOnlySpan<char> format, out Uuid128 result)
		{
			if (!Guid.TryParseExact(input, format, out Guid guid))
			{
				result = default;
				return false;
			}
			result = new Uuid128(guid);
			return true;
		}

#if NET8_0_OR_GREATER

		public static bool TryParseBase62(string? input, out Uuid128 result)
		{
			if (input == null)
			{
				result = default;
				return false;
			}
			return TryParseBase62(input.AsSpan(), out result);
		}

		public static bool TryParseBase62(ReadOnlySpan<char> input, out Uuid128 result)
		{
			if (input.Length == 0)
			{
				result = default;
				return true;
			}

			if (input.Length <= 22 && Base62Encoding.TryDecodeUInt128(input, out UInt128 x, Base62FormattingOptions.Lexicographic))
			{
				result = new Uuid128(x);
				return true;
			}

			result = default;
			return false;
		}

#endif

#endregion

		public long Timestamp
		{
			[Pure]
			get
			{
				long ts = m_timeLow;
				ts |= ((long) m_timeMid) << 32;
				ts |= ((long) (m_timeHiAndVersion & 0x0FFF)) << 48;
				return ts;
			}
		}

		public int Version
		{
			[Pure]
			get => m_timeHiAndVersion >> 12;
		}

		public int ClockSequence
		{
			[Pure]
			get
			{
				int clk = m_clkSeqLow;
				clk |= (m_clkSeqHiRes & 0x3F) << 8;
				return clk;
			}
		}

		public long Node
		{
			[Pure]
			get
			{
				long node;
				node = ((long)m_node0) << 40;
				node |= ((long)m_node1) << 32;
				node |= ((long)m_node2) << 24;
				node |= ((long)m_node3) << 16;
				node |= ((long)m_node4) << 8;
				node |= m_node5;
				return node;
			}
		}

		#region Unsafe I/O...

		[Pure]
		public static bool TryRead(ReadOnlySpan<byte> source, out Guid result)
		{
			if (source.Length < 16)
			{
				result = default;
				return false;
			}
			result = Read(source);
			return true;
		}

		[Pure]
		public static unsafe Guid Read(ReadOnlySpan<byte> source)
		{
			Contract.Debug.Requires(source.Length >= 16);
			if (source.Length < 16) throw new ArgumentException("The source buffer is too small", nameof(source));
			Guid tmp;
			fixed (byte* src = &MemoryMarshal.GetReference(source))
			{
				if (BitConverter.IsLittleEndian)
				{
					byte* ptr = (byte*) &tmp;

					// Data1: 32 bits, must swap
					ptr[0] = src[3];
					ptr[1] = src[2];
					ptr[2] = src[1];
					ptr[3] = src[0];
					// Data2: 16 bits, must swap
					ptr[4] = src[5];
					ptr[5] = src[4];
					// Data3: 16 bits, must swap
					ptr[6] = src[7];
					ptr[7] = src[6];
					// Data4: 64 bits, no swap required
					*(long*) (ptr + 8) = *(long*) (src + 8);
				}
				else
				{
					long* ptr = (long*) &tmp;
					ptr[0] = *(long*) (src);
					ptr[1] = *(long*) (src + 8);
				}
			}
			return tmp;
		}

		public static bool TryWrite(in Guid value, Span<byte> buffer)
		{
			if (buffer.Length < 16)
			{
				return false;
			}
			Write(in value, buffer);
			return true;
		}

		// ReSharper disable once NotResolvedInText
		private static ArgumentException ErrorDestinationBufferTooSmall() => new("The destination buffer is too small", "buffer");

		public static unsafe void Write(in Guid value, Span<byte> buffer)
		{
			if (buffer.Length < 16) throw ErrorDestinationBufferTooSmall();

			fixed (Guid* inp = &value)
			fixed (byte* outp = &MemoryMarshal.GetReference(buffer))
			{
				if (BitConverter.IsLittleEndian)
				{
					byte* src = (byte*) inp;

					// Data1: 32 bits, must swap
					outp[0] = src[3];
					outp[1] = src[2];
					outp[2] = src[1];
					outp[3] = src[0];
					// Data2: 16 bits, must swap
					outp[4] = src[5];
					outp[5] = src[4];
					// Data3: 16 bits, must swap
					outp[6] = src[7];
					outp[7] = src[6];
					// Data4: 64 bits, no swap required
					*(long*) (outp + 8) = *(long*) (src + 8);
				}
				else
				{
					long* src = (long*) inp;
					*(long*) (outp) = src[0];
					*(long*) (outp + 8) = src[1];
				}
			}
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		[Obsolete("Renamed to WriteTo(..)")]
		public void WriteToUnsafe(Span<byte> buffer) => Write(in m_packed, buffer);

		/// <summary>Write the bytes of this instance to the specified <paramref name="buffer"/></summary>
		/// <param name="buffer">Buffer where the bytes will be written to, with a capacity of at least 16 bytes</param>
		/// <exception cref="ArgumentException">If <paramref name="buffer"/> is smaller than 16 bytes</exception>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void WriteTo(Span<byte> buffer) => Write(in m_packed, buffer);

		/// <summary>Write the bytes of this instance to the specified <paramref name="buffer"/>, if it is large enough</summary>
		/// <param name="buffer">Buffer where the bytes will be written to, with a capacity of at least 16 bytes</param>
		/// <returns><see langword="true"/> if <paramref name="buffer"/> was large enough; otherwise, <see langword="false"/>.</returns>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool TryWriteTo(Span<byte> buffer)
		{
			if (buffer.Length < 16)
			{
				return false;
			}
			Write(in m_packed, buffer);
			return true;
		}

#if NET8_0_OR_GREATER

		/// <summary>Return the equivalent <see cref="UInt128"/></summary>
		/// <remarks>The integer correspond to the big-endian version of this instance serialized as a byte array</remarks>
		public UInt128 ToUInt128()
		{
			Span<byte> tmp = stackalloc byte[16];
			Write(in m_packed, tmp);
			return BinaryPrimitives.ReadUInt128BigEndian(tmp);
		}

		/// <summary>Return the equivalent <see cref="Int128"/></summary>
		/// <remarks>The integer correspond to the big-endian version of this instance serialized as a byte array</remarks>
		public Int128 ToInt128()
		{
			Span<byte> tmp = stackalloc byte[16];
			Write(in m_packed, tmp);
			return BinaryPrimitives.ReadInt128BigEndian(tmp);
		}

#endif

		#endregion

		#region Decomposition...

		/// <summary>Split this 128-bit UUID into two 64-bit UUIDs</summary>
		/// <param name="high">Receives the first 8 bytes (in network order) of this UUID</param>
		/// <param name="low">Receives the last 8 bytes (in network order) of this UUID</param>
		public void Split(out Uuid64 high, out Uuid64 low)
		{
			Deconstruct(out high, out low);
		}

		/// <summary>Split this 128-bit UUID into two 64-bit numbers</summary>
		/// <param name="a">xxxxxxxx-xxxx-xxxx-....-............</param>
		/// <param name="b">........-....-....-xxxx-xxxxxxxxxxxx</param>
		public void Split(out ulong a, out ulong b)
		{
			unsafe
			{
				byte* buffer = stackalloc byte[SizeOf];
				Write(in m_packed, new Span<byte>(buffer, SizeOf));
				a = UnsafeHelpers.LoadUInt64BE(buffer + 0);
				b = UnsafeHelpers.LoadUInt64BE(buffer + 8);
			}
		}

		/// <summary>Split this 128-bit UUID into two 64-bit UUIDs</summary>
		/// <param name="high">Receives the first 8 bytes (in network order) of this UUID</param>
		/// <param name="low">Receives the last 8 bytes (in network order) of this UUID</param>
		public void Deconstruct(out Uuid64 high, out Uuid64 low)
		{
			unsafe
			{
				Span<byte> buffer = stackalloc byte[SizeOf];
				Write(in m_packed, buffer);
				high = new Uuid64(BinaryPrimitives.ReadInt64BigEndian(buffer));
				low = new Uuid64(BinaryPrimitives.ReadInt64BigEndian(buffer.Slice(8)));
			}
		}

		/// <summary>Split this 128-bit UUID into two 64-bit numbers</summary>
		/// <param name="a">xxxxxxxx-xxxx-xxxx-....-............</param>
		/// <param name="b">........-....-....-xxxx-xxxx........</param>
		/// <param name="c">........-....-....-....-....xxxxxxxx</param>
		public void Deconstruct(out ulong a, out uint b, out uint c)
		{
			unsafe
			{
				byte* buffer = stackalloc byte[SizeOf];
				Write(in m_packed, new Span<byte>(buffer, SizeOf));
				a = UnsafeHelpers.LoadUInt64BE(buffer + 0);
				b = UnsafeHelpers.LoadUInt32BE(buffer + 8);
				c = UnsafeHelpers.LoadUInt32BE(buffer + 12);
			}
		}

		/// <summary>Split this 128-bit UUID into two 64-bit numbers</summary>
		/// <param name="a">xxxxxxxx-....-....-....-............</param>
		/// <param name="b">........-xxxx-xxxx-....-............</param>
		/// <param name="c">........-....-....-xxxx-xxxx........</param>
		/// <param name="d">........-....-....-....-....xxxxxxxx</param>
		public void Deconstruct(out uint a, out uint b, out uint c, out uint d)
		{
			unsafe
			{
				byte* buffer = stackalloc byte[SizeOf];
				Write(in m_packed, new Span<byte>(buffer, SizeOf));
				a = UnsafeHelpers.LoadUInt32BE(buffer + 0);
				b = UnsafeHelpers.LoadUInt32BE(buffer + 4);
				c = UnsafeHelpers.LoadUInt32BE(buffer + 8);
				d = UnsafeHelpers.LoadUInt32BE(buffer + 12);
			}
		}

		#endregion

		#region Conversion...

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public Guid ToGuid()
		{
			return m_packed;
		}

		[Pure]
		public byte[] ToByteArray()
		{
			// We must use Big Endian when serializing the UUID
			var res = new byte[SizeOf];
			Write(in m_packed, res.AsSpan());
			return res;
		}

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public Slice ToSlice()
			=> new(ToByteArray()); //TODO: OPTIMIZE: optimize this ?

		public void WriteTo(ref SliceWriter writer)
		{
			WriteTo(writer.AllocateSpan(SizeOf));
		}

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public override string ToString() => m_packed.ToString();

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public string ToString(
#if NET8_0_OR_GREATER
			[StringSyntax(StringSyntaxAttribute.GuidFormat)]
#endif
			string? format
		)
		{
#if NET8_0_OR_GREATER
			return format switch
			{
				null or "D" => m_packed.ToString(),
				"C" or "c" => ToBase62(padded: false), // base 62, compact, up to 22 characters
				"Z" or "z" => ToBase62(padded: true),  // base 62, padded to 22 characters
				_ => m_packed.ToString(format)
			};
#else
			return m_packed.ToString(format);
#endif
		}

#if NET8_0_OR_GREATER

		public string ToBase62(bool padded = false)
		{
			return Base62Encoding.Encode(this.ToUInt128(), padded ? Base62FormattingOptions.Lexicographic | Base62FormattingOptions.Padded : Base62FormattingOptions.Lexicographic);
		}

		private bool TryFormatToBase62(Span<char> destination, out int charsWritten, bool padded = false)
		{
			return Base62Encoding.TryEncodeTo(destination, out charsWritten, this.ToUInt128(), padded ? Base62FormattingOptions.Lexicographic | Base62FormattingOptions.Padded : Base62FormattingOptions.Lexicographic);
		}

#endif

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public string ToString(
#if NET8_0_OR_GREATER
			[StringSyntax("GuidFormat")]
#endif
			string? format,
			IFormatProvider? provider
		) => m_packed.ToString(format, provider);

		/// <summary>Tries to format the value of the current instance into the provided span of characters.</summary>
		/// <param name="destination">The span in which to write this instance's value formatted as a span of characters.</param>
		/// <param name="charsWritten">When this method returns, contains the number of characters that were written in <paramref name="destination" />.</param>
		/// <param name="format">A span containing the characters that represent a standard or custom format string that defines the acceptable format for <paramref name="destination" />.</param>
		/// <param name="provider">This parameter is ignored.</param>
		/// <returns>
		/// <see langword="true" /> if the formatting was successful; otherwise, <see langword="false" />.</returns>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool TryFormat(
			Span<char> destination,
			out int charsWritten,
#if NET8_0_OR_GREATER
			[StringSyntax("GuidFormat")]
#endif
			ReadOnlySpan<char> format = default,
			IFormatProvider? provider = null
		)
		{
#if NET8_0_OR_GREATER
			return format switch
			{
				"" or "D" => m_packed.TryFormat(destination, out charsWritten),
				"C" or "c" => TryFormatToBase62(destination, out charsWritten, padded: false), // base 62, compact, up to 22 characters
				"Z" or "z" => TryFormatToBase62(destination, out charsWritten, padded: true), // base 62, padded to 22 characters
				_ => m_packed.TryFormat(destination, out charsWritten, format)
			};
#else
			return m_packed.TryFormat(destination, out charsWritten, format);
#endif
		}

#if NET8_0_OR_GREATER

		/// <summary>Tries to format the value of the current instance UTF-8 into the provided span of bytes.</summary>
		/// <param name="utf8Destination">The span in which to write this instance's value formatted as a span of bytes.</param>
		/// <param name="bytesWritten">When this method returns, contains the number of bytes that were written in <paramref name="utf8Destination" />.</param>
		/// <param name="format">A span containing the characters that represent a standard or custom format string that defines the acceptable format for <paramref name="utf8Destination" />.</param>
		/// <param name="provider">This parameter is ignored.</param>
		/// <returns>
		/// <see langword="true" /> if the formatting was successful; otherwise, <see langword="false" />.</returns>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool TryFormat(
			Span<byte> utf8Destination,
			out int bytesWritten,
#if NET8_0_OR_GREATER
			[StringSyntax("GuidFormat")]
#endif
			ReadOnlySpan<char> format = default,
			IFormatProvider? provider = null
		) => m_packed.TryFormat(utf8Destination, out bytesWritten, format);

#endif

		/// <summary>Increment the value of this UUID</summary>
		/// <param name="value">Positive value</param>
		/// <returns>Incremented UUID</returns>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public Uuid128 Increment([Positive] int value)
		{
			Contract.Debug.Requires(value >= 0);
			return Increment(checked((ulong) value));
		}

		/// <summary>Increment the value of this UUID</summary>
		/// <param name="value">Positive value</param>
		/// <returns>Incremented UUID</returns>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public Uuid128 Increment([Positive] long value)
		{
			Contract.Debug.Requires(value >= 0);
			return Increment(checked((ulong) value));
		}

		/// <summary>Increment the value of this UUID</summary>
		/// <param name="value">Value to add to this UUID</param>
		/// <returns>Incremented UUID</returns>
		[Pure]
		public Uuid128 Increment(ulong value)
		{
			unsafe
			{
				// serialize GUID into High Endian format
				byte* buf = stackalloc byte[SizeOf];
				Write(in m_packed, new Span<byte>(buf, SizeOf));

				// Add the low 64 bits (in HE)
				ulong sum = unchecked(UnsafeHelpers.LoadUInt64BE(buf + 8) + value);
				if (sum < value)
				{ // overflow occured, we must carry to the high 64 bits (in HE)
					UnsafeHelpers.StoreUInt64BE(buf, unchecked(UnsafeHelpers.LoadUInt64BE(buf) + 1));
				}
				UnsafeHelpers.StoreUInt64BE(buf + 8, sum);
				// deserialize back to GUID
				return new Uuid128(Read(new ReadOnlySpan<byte>(buf, SizeOf)));
			}
		}

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static Uuid128 operator +(Uuid128 left, long right)
		{
			return left.Increment(right);
		}

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static Uuid128 operator +(Uuid128 left, ulong right)
		{
			return left.Increment(right);
		}

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static Uuid128 operator ++(Uuid128 left)
		{
			return left.Increment(1);
		}

		//TODO: Decrement

		#endregion

		#region Equality / Comparison ...

		public override bool Equals(object? obj)
		{
			if (obj == null) return false;
			if (obj is Uuid128 u128) return m_packed == u128.m_packed;
			if (obj is Guid g) return m_packed == g;
			//TODO: Slice? string?
			return false;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool Equals(Uuid128 other)
		{
			return m_packed == other.m_packed;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool Equals(Guid other)
		{
			return m_packed == other;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool operator ==(Uuid128 a, Uuid128 b)
		{
			return a.m_packed == b.m_packed;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool operator !=(Uuid128 a, Uuid128 b)
		{
			return a.m_packed != b.m_packed;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool operator ==(Uuid128 a, Guid b)
		{
			return a.m_packed == b;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool operator !=(Uuid128 a, Guid b)
		{
			return a.m_packed != b;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool operator ==(Guid a, Uuid128 b)
		{
			return a == b.m_packed;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool operator !=(Guid a, Uuid128 b)
		{
			return a != b.m_packed;
		}

		public override int GetHashCode()
		{
			return m_packed.GetHashCode();
		}

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public int CompareTo(Uuid128 other)
		{
			return m_packed.CompareTo(other.m_packed);
		}

		public int CompareTo(object? obj)
		{
			switch (obj)
			{
				case null: return 1;
				case Uuid128 u128: return m_packed.CompareTo(u128.m_packed);
				case Guid g: return m_packed.CompareTo(g);
			}
			return m_packed.CompareTo(obj);
		}

		#endregion

		/// <summary>Instance of this times can be used to test Uuid128 for equality and ordering</summary>
		public sealed class Comparer : IEqualityComparer<Uuid128>, IComparer<Uuid128>
		{

			public static readonly Comparer Default = new Comparer();

			private Comparer()
			{ }

			public bool Equals(Uuid128 x, Uuid128 y)
			{
				return x.m_packed.Equals(y.m_packed);
			}

			public int GetHashCode(Uuid128 obj)
			{
				return obj.m_packed.GetHashCode();
			}

			public int Compare(Uuid128 x, Uuid128 y)
			{
				return x.m_packed.CompareTo(y.m_packed);
			}
		}

	}

}
