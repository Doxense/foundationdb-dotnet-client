#region BSD Licence
/* Copyright (c) 2013-2018, Doxense SAS
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

namespace System
{
	using System;
	using System.Collections.Generic;
	using System.ComponentModel;
	using System.Diagnostics;
	using System.Globalization;
	using System.Runtime.CompilerServices;
	using System.Runtime.InteropServices;
	using System.Security.Cryptography;
	using Doxense.Diagnostics.Contracts;
	using Doxense.Memory;
	using JetBrains.Annotations;

	/// <summary>Represents a 64-bit UUID that is stored in high-endian format on the wire</summary>
	[DebuggerDisplay("[{ToString(),nq}]")]
	[ImmutableObject(true), Serializable]
	public readonly struct Uuid64 : IFormattable, IEquatable<Uuid64>, IComparable<Uuid64>
	{
		public static readonly Uuid64 Empty = default(Uuid64);

		/// <summary>Size is 8 bytes</summary>
		public const int SizeOf = 8;

		private readonly ulong m_value;
		//note: this will be in host order (so probably Little-Endian) in order to simplify parsing and ordering

		#region Constructors...

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public Uuid64(ulong value)
		{
			m_value = value;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public Uuid64(long value)
		{
			m_value = (ulong)value;
		}

		/// <summary>Pack two 32-bits components into a 64-bit UUID</summary>
		/// <param name="a">Upper 32 bits (XXXXXXXX-........)</param>
		/// <param name="b">Lower 32 bits (........-XXXXXXXX)</param>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public Uuid64(uint a, uint b)
		{
			m_value = ((ulong) a << 32) | b;
		}

		/// <summary>Pack two components into a 64-bit UUID</summary>
		/// <param name="a">Upper 16 bits (XXXX....-........)</param>
		/// <param name="b">Lower 48 bits (....XXXX-XXXXXXXX)</param>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public Uuid64(ushort a, long b)
		{
			//Contract.Requires((ulong) b < (1UL << 48));
			m_value = ((ulong) a << 48) | ((ulong) b & ((1UL << 48) - 1));
		}

		[Pure, NotNull, MethodImpl(MethodImplOptions.NoInlining)]
		private static Exception FailInvalidBufferSize([InvokerParameterName] string arg)
		{
			return ThrowHelper.ArgumentException(arg, "Value must be 8 bytes long");
		}

		[Pure, NotNull, MethodImpl(MethodImplOptions.NoInlining)]
		private static Exception FailInvalidFormat()
		{
			return ThrowHelper.FormatException("Invalid " + nameof(Uuid64) + " format");
		}

		/// <summary>Generate a new random 64-bit UUID.</summary>
		/// <remarks>If you need sequential or cryptographic uuids, you should use a different generator.</remarks>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static Uuid64 NewUuid()
		{
			unsafe
			{
				// we use Guid.NewGuid() as a source of ~128 bits of entropy, and we fold both 64-bits parts into a single value
				var x = Guid.NewGuid();
				ulong* p = (ulong*) &x;
				return new Uuid64(p[0] ^ p[1]);
			}
		}

		#endregion

		#region Decomposition...

		/// <summary>Split into two 32-bit halves</summary>
		/// <param name="a">xxxxxxxx-........</param>
		/// <param name="b">........-xxxxxxxx</param>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void Deconstruct(out uint a, out uint b)
		{
			ulong value = m_value;
			a = (uint) (value >> 32);
			b = (uint) value;
		}

		/// <summary>Split into two halves</summary>
		/// <param name="a">xxxx....-........</param>
		/// <param name="b">....xxxx-........</param>
		/// <param name="c">........-xxxx....</param>
		/// <param name="d">........-....xxxx</param>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void Deconstruct(out ushort a, out ushort b, out ushort c, out ushort d)
		{
			ulong value = m_value;
			a = (ushort) (value >> 48);
			b = (ushort) (value >> 32);
			c = (ushort) (value >> 16);
			d = (ushort) value;
		}

		#endregion

		#region Reading...

		/// <summary>Read a 64-bit UUID from a byte array</summary>
		/// <param name="value">Array of exactly 0 or 8 bytes</param>
		[Pure]
		public static Uuid64 Read(byte[] value)
		{
			Contract.NotNull(value, nameof(value));
			if (value.Length == 0) return default;
			if (value.Length == 8) return new Uuid64(ReadUnsafe(value, 0));
			throw FailInvalidBufferSize(nameof(value));
		}

		/// <summary>Read a 64-bit UUID from part of a byte array</summary>
		[Pure]
		[Obsolete("Use Uuid64.Read(ReadOnlySpan<byte>) instead!")]
		public static Uuid64 Read(byte[] value, int offset, int count)
		{
			Contract.DoesNotOverflow(value, offset, count, nameof(value));
			if (count == 0) return default;
			if (count == 8) return new Uuid64(ReadUnsafe(value, 0));
			throw FailInvalidBufferSize(nameof(count));
		}

		/// <summary>Read a 64-bit UUID from slice of memory</summary>
		/// <param name="value">slice of exactly 0 or 8 bytes</param>
		[Pure]
		public static Uuid64 Read(Slice value)
		{
			Contract.NotNull(value.Array, nameof(value));
			if (value.Count == 0) return default;
			if (value.Count == 8) return new Uuid64(ReadUnsafe(value.Array, value.Offset));
			throw FailInvalidBufferSize(nameof(value));
		}

		/// <summary>Read a 64-bit UUID from slice of memory</summary>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		internal static unsafe Uuid64 Read(byte* ptr, uint count)
		{
			if (count == 0) return default;
			if (count == 8) return new Uuid64(ReadUnsafe(ptr));
			throw FailInvalidBufferSize(nameof(count));
		}

		#endregion

		#region Parsing...

#if ENABLE_SPAN

		/// <summary>Parse a string representation of an UUid64</summary>
		/// <paramref name="buffer">String in either formats: "", "badc0ffe-e0ddf00d", "badc0ffee0ddf00d", "{badc0ffe-e0ddf00d}", "{badc0ffee0ddf00d}"</paramref>
		/// <remarks>Parsing is case-insensitive. The empty string is mapped to <see cref="Empty">Uuid64.Empty</see>.</remarks>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static Uuid64 Parse([NotNull] string buffer)
		{
			Contract.NotNull(buffer, nameof(buffer));
			if (!TryParse(buffer.AsSpan(), out var value))
			{
				throw FailInvalidFormat();
			}
			return value;
		}

		/// <summary>Parse a string representation of an UUid64</summary>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static Uuid64 Parse(ReadOnlySpan<char> buffer)
		{
			if (!TryParse(buffer, out var value))
			{
				throw FailInvalidFormat();
			}
			return value;
		}

		/// <summary>Parse a string representation of an UUid64</summary>
		[Pure]
		[Obsolete("Use Uuid64.Parse(ReadOnlySpan<char>) instead", error: true)] //TODO: remove me!
		public static unsafe Uuid64 Parse(char* buffer, int count)
		{
			if (count == 0) return default(Uuid64);
			if (!TryParse(new ReadOnlySpan<char>(buffer, count), out var value))
			{
				throw FailInvalidFormat();
			}
			return value;
		}

		/// <summary>Parse a Base62 encoded string representation of an UUid64</summary>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static Uuid64 FromBase62([NotNull] string buffer)
		{
			Contract.NotNull(buffer, nameof(buffer));
			if (!TryParseBase62(buffer.AsSpan(), out var value))
			{
				throw FailInvalidFormat();
			}
			return value;
		}

		/// <summary>Try parsing a string representation of an UUid64</summary>
		public static bool TryParse([NotNull] string buffer, out Uuid64 result)
		{
			Contract.NotNull(buffer, nameof(buffer));
			return TryParse(buffer.AsSpan(), out result);
		}

		/// <summary>Try parsing a string representation of an UUid64</summary>
		public static bool TryParse(ReadOnlySpan<char> s, out Uuid64 result)
		{
			Contract.Requires(s != null);

			// we support the following formats: "{hex8-hex8}", "{hex16}", "hex8-hex8", "hex16" and "base62"
			// we don't support base10 format, because there is no way to differentiate from hex or base62

			result = default(Uuid64);
			switch (s.Length)
			{
				case 0:
				{ // empty
					return true;
				}
				case 16:
				{ // xxxxxxxxxxxxxxxx
					return TryDecode16Unsafe(s, separator: false, out result);
				}
				case 17:
				{ // xxxxxxxx-xxxxxxxx
					if (s[8] != '-') return false;
					return TryDecode16Unsafe(s, separator: true, out result);
				}
				case 18:
				{ // {xxxxxxxxxxxxxxxx}
					if (s[0] != '{' || s[17] != '}')
					{
						return false;
					}
					return TryDecode16Unsafe(s.Slice(1, s.Length - 2), separator: false, out result);
				}
				case 19:
				{ // {xxxxxxxx-xxxxxxxx}
					if (s[0] != '{' || s[18] != '}')
					{
						return false;
					}
					return TryDecode16Unsafe(s.Slice(1, s.Length - 2), separator: true, out result);
				}
				default:
				{
					return false;
				}
			}
		}

		public static bool TryParseBase62(ReadOnlySpan<char> s, out Uuid64 result)
		{
			if (s.Length == 0)
			{
				result = default;
				return true;
			}

			if (s.Length <= 11 && Base62.TryDecode(s, out ulong x))
			{
				result = new Uuid64(x);
				return true;
			}

			result = default;
			return false;
		}

#else

		/// <summary>Parse a string representation of an UUid64</summary>
		/// <paramref name="buffer">String in either formats: "", "badc0ffe-e0ddf00d", "badc0ffee0ddf00d", "{badc0ffe-e0ddf00d}", "{badc0ffee0ddf00d}"</paramref>
		/// <remarks>Parsing is case-insensitive. The empty string is mapped to <see cref="Empty">Uuid64.Empty</see>.</remarks>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static Uuid64 Parse([NotNull] string buffer)
		{
			Contract.NotNull(buffer, nameof(buffer));
			unsafe
			{
				fixed (char* chars = buffer)
				{
					if (!TryParse(chars, buffer.Length, out var value))
					{
						throw FailInvalidFormat();
					}

					return value;
				}
			}
		}

		/// <summary>Parse a string representation of an UUid64</summary>
		[Pure]
		[Obsolete("Use Uuid64.Parse(ReadOnlySpan<char>) instead", error: true)] //TODO: remove me!
		public static unsafe Uuid64 Parse(char* chars, int numChars)
		{
			if (numChars == 0) return default(Uuid64);
			if (!TryParse(chars, numChars, out var value))
			{
				throw FailInvalidFormat();
			}
			return value;
		}

		/// <summary>Parse a Base62 encoded string representation of an UUid64</summary>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static Uuid64 FromBase62([NotNull] string buffer)
		{
			Contract.NotNull(buffer, nameof(buffer));
			unsafe
			{
				fixed (char* chars = buffer)
				{
					if (!TryParseBase62(chars, buffer.Length, out var value))
					{
						throw FailInvalidFormat();
					}

					return value;
				}
			}
		}

		/// <summary>Try parsing a string representation of an UUid64</summary>
		public static bool TryParse([NotNull] string buffer, out Uuid64 result)
		{
			Contract.NotNull(buffer, nameof(buffer));
			unsafe
			{
				fixed (char* chars = buffer)
				{
					return TryParse(chars, buffer.Length, out result);
				}
			}
		}

		/// <summary>Try parsing a string representation of an UUid64</summary>
		public static unsafe bool TryParse(char* chars, int numChars, out Uuid64 result)
		{
			Contract.Requires(chars != null && numChars >= 0);

			// we support the following formats: "{hex8-hex8}", "{hex16}", "hex8-hex8", "hex16" and "base62"
			// we don't support base10 format, because there is no way to differentiate from hex or base62

			result = default(Uuid64);
			switch (numChars)
			{
				case 0:
				{ // empty
					return true;
				}
				case 16:
				{ // xxxxxxxxxxxxxxxx
					return TryDecode16Unsafe(chars, numChars, false, out result);
				}
				case 17:
				{ // xxxxxxxx-xxxxxxxx
					if (chars[8] != '-') return false;
					return TryDecode16Unsafe(chars, numChars, true, out result);
				}
				case 18:
				{ // {xxxxxxxxxxxxxxxx}
					if (chars[0] != '{' || chars[17] != '}')
					{
						return false;
					}
					return TryDecode16Unsafe(chars + 1, numChars - 2, false, out result);
				}
				case 19:
				{ // {xxxxxxxx-xxxxxxxx}
					if (chars[0] != '{' || chars[18] != '}')
					{
						return false;
					}
					return TryDecode16Unsafe(chars + 1, numChars - 2, true, out result);
				}
				default:
				{
					return false;
				}
			}
		}

		public static unsafe bool TryParseBase62(char* chars, int numChars, out Uuid64 result)
		{
			if (numChars == 0)
			{
				result = default(Uuid64);
				return true;
			}

			if (numChars <= 11 && Base62.TryDecode(chars, numChars, out ulong x))
			{
				result = new Uuid64(x);
				return true;
			}

			result = default(Uuid64);
			return false;

		}
#endif


		#endregion

		#region Casting...

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static implicit operator Uuid64(ulong value)
		{
			return new Uuid64(value);
		}

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static explicit operator ulong(Uuid64 value)
		{
			return value.m_value;
		}

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static implicit operator Uuid64(long value)
		{
			return new Uuid64(value);
		}

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static explicit operator long(Uuid64 value)
		{
			return (long) value.m_value;
		}

		#endregion

		#region IFormattable...

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public long ToInt64()
		{
			return (long) m_value;
		}

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public ulong ToUInt64()
		{
			return m_value;
		}

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public Slice ToSlice()
		{
			return Slice.FromFixedU64BE(m_value);
		}

		[Pure, NotNull]
		public byte[] ToByteArray()
		{
			var bytes = Slice.FromFixedU64BE(m_value).Array;
			Contract.Ensures(bytes != null && bytes.Length == 8); // HACKHACK: for perf reasons, we rely on the fact that Slice.FromFixedU64BE() allocates a new 8-byte array that we can return without copying
			return bytes;
		}

		/// <summary>Returns a string representation of the value of this instance.</summary>
		/// <returns>String using the format "xxxxxxxx-xxxxxxxx", where 'x' is a lower-case hexadecimal digit</returns>
		/// <remarks>Strings returned by this method will always to 17 characters long.</remarks>
		public override string ToString()
		{
			return ToString("D", null);
		}

		/// <summary>Returns a string representation of the value of this <see cref="Uuid64"/> instance, according to the provided format specifier.</summary>
		/// <param name="format">A single format specifier that indicates how to format the value of this Guid. The format parameter can be "D", "B", "X", "G", "Z" or "N". If format is null or an empty string (""), "D" is used.</param>
		/// <returns>The value of this <see cref="Uuid64"/>, using the specified format.</returns>
		/// <remarks>See <see cref="ToString(string, IFormatProvider)"/> for a description of the different formats</remarks>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public string ToString(string format)
		{
			return ToString(format, null);
		}

		/// <summary>Returns a string representation of the value of this instance of the <see cref="Uuid64"/> class, according to the provided format specifier and culture-specific format information.</summary>
		/// <param name="format">A single format specifier that indicates how to format the value of this Guid. The format parameter can be "D", "N", "Z", "R", "X" or "B". If format is null or an empty string (""), "D" is used.</param>
		/// <param name="formatProvider">An object that supplies culture-specific formatting information. Only used for the "R" format.</param>
		/// <returns>The value of this <see cref="Uuid64"/>, using the specified format.</returns>
		/// <example>
		/// <p>The <b>D</b> format encodes the value as two groups of 8 hexadecimal digits, separated by an hyphen: "01234567-89abcdef" (17 characters).</p>
		/// <p>The <b>X</b> format encodes the value as a single group of 16 hexadecimal digits: "0123456789abcdef" (16 characters).</p>
		/// <p>The <b>B</b> format is equivalent to the <b>D</b> format, but surrounded with '{' and '}': "{01234567-89abcdef}" (19 characters).</p>
		/// <p>The <b>R</b> format encodes the value as a decimal number "1234567890" (1 to 20 characters) which can be parsed as an UInt64 without loss.</p>
		/// <p>The <b>C</b> format uses a compact base-62 encoding that preserves lexicographical ordering, composed of digits, uppercase alpha and lowercase alpha, suitable for compact representation that can fit in a querystring.</p>
		/// <p>The <b>Z</b> format is equivalent to the <b>C</b> format, but with extra padding so that the string is always 11 characters long.</p>
		/// </example>
		public string ToString(string format, IFormatProvider formatProvider)
		{
			if (string.IsNullOrEmpty(format)) format = "D";

			switch(format)
			{
				case "D":
				{ // Default format is "xxxxxxxx-xxxxxxxx"
					return Encode16(m_value, separator: true, quotes: false, upper: true);
				}
				case "d":
				{ // Default format is "xxxxxxxx-xxxxxxxx"
					return Encode16(m_value, separator: true, quotes: false, upper: false);
				}

				case "C":
				case "c":
				{ // base 62, compact, no padding
					return Base62.Encode(m_value, padded: false);
				}
				case "Z":
				case "z":
				{ // base 62, padded with '0' up to 11 chars
					return Base62.Encode(m_value, padded: true);
				}

				case "R":
				case "r":
				{ // Integer: "1234567890"
					return m_value.ToString(null, formatProvider ?? CultureInfo.InvariantCulture);
				}

				case "X": //TODO: Guid.ToString("X") returns "{0x.....,0x.....,...}"
				case "N":
				{ // "XXXXXXXXXXXXXXXX"
					return Encode16(m_value, separator: false, quotes: false, upper: true);
				}
				case "x": //TODO: Guid.ToString("X") returns "{0x.....,0x.....,...}"
				case "n":
				{ // "xxxxxxxxxxxxxxxx"
					return Encode16(m_value, separator: false, quotes: false, upper: false);
				}

				case "B":
				{ // "{xxxxxxxx-xxxxxxxx}"
					return Encode16(m_value, separator: true, quotes: true, upper: true);
				}
				case "b":
				{ // "{xxxxxxxx-xxxxxxxx}"
					return Encode16(m_value, separator: true, quotes: true, upper: false);
				}
				default:
				{
					throw new FormatException("Invalid " + nameof(Uuid64) + " format specification.");
				}
			}
		}

		#endregion

		#region IEquatable / IComparable...

		public override bool Equals(object obj)
		{
			switch (obj)
			{
				case Uuid64 u64: return Equals(u64);
				case ulong ul: return m_value == ul;
				case long l: return m_value == (ulong) l;
				//TODO: string format ? Slice ?
			}
			return false;
		}

		public override int GetHashCode()
		{
			return ((int) m_value) ^ (int) (m_value >> 32);
		}

		public bool Equals(Uuid64 other)
		{
			return m_value == other.m_value;
		}

		public int CompareTo(Uuid64 other)
		{
			return m_value.CompareTo(other.m_value);
		}

		#endregion

		#region Base16 encoding...

		[Pure]
		private static char HexToLowerChar(int a)
		{
			a &= 0xF;
			return a > 9 ? (char)(a - 10 + 'a') : (char)(a + '0');
		}

		[NotNull]
		private static unsafe char* HexsToLowerChars([NotNull] char* ptr, int a)
		{
			Contract.Requires(ptr != null);
			ptr[0] = HexToLowerChar(a >> 28);
			ptr[1] = HexToLowerChar(a >> 24);
			ptr[2] = HexToLowerChar(a >> 20);
			ptr[3] = HexToLowerChar(a >> 16);
			ptr[4] = HexToLowerChar(a >> 12);
			ptr[5] = HexToLowerChar(a >> 8);
			ptr[6] = HexToLowerChar(a >> 4);
			ptr[7] = HexToLowerChar(a);
			return ptr + 8;
		}

		[Pure]
		private static char HexToUpperChar(int a)
		{
			a &= 0xF;
			return a > 9 ? (char)(a - 10 + 'A') : (char)(a + '0');
		}

		[NotNull]
		private static unsafe char* HexsToUpperChars([NotNull] char* ptr, int a)
		{
			Contract.Requires(ptr != null);
			ptr[0] = HexToUpperChar(a >> 28);
			ptr[1] = HexToUpperChar(a >> 24);
			ptr[2] = HexToUpperChar(a >> 20);
			ptr[3] = HexToUpperChar(a >> 16);
			ptr[4] = HexToUpperChar(a >> 12);
			ptr[5] = HexToUpperChar(a >> 8);
			ptr[6] = HexToUpperChar(a >> 4);
			ptr[7] = HexToUpperChar(a);
			return ptr + 8;
		}

		[Pure, NotNull]
		private static unsafe string Encode16(ulong value, bool separator, bool quotes, bool upper)
		{
			int size = 16 + (separator ? 1 : 0) + (quotes ? 2 : 0);
			char* buffer = stackalloc char[24]; // max 19 mais on arrondi a 24

			char* ptr = buffer;
			if (quotes) *ptr++ = '{';
			ptr = upper
				? HexsToUpperChars(ptr, (int)(value >> 32))
				: HexsToLowerChars(ptr, (int)(value >> 32));
			if (separator) *ptr++ = '-';
			ptr = upper
				? HexsToUpperChars(ptr, (int)(value & 0xFFFFFFFF))
				: HexsToLowerChars(ptr, (int)(value & 0xFFFFFFFF));
			if (quotes) *ptr++ = '}';

			Contract.Ensures(ptr == buffer + size);
			return new string(buffer, 0, size);
		}

		private const int INVALID_CHAR = -1;

		[Pure]
		private static int CharToHex(char c)
		{
			if (c <= '9')
			{
				return c >= '0' ? (c - 48) : INVALID_CHAR;
			}
			if (c <= 'F')
			{
				return c >= 'A' ? (c - 55) : INVALID_CHAR;
			}
			if (c <= 'f')
			{
				return c >= 'a' ? (c - 87) : INVALID_CHAR;
			}
			return INVALID_CHAR;
		}

#if ENABLE_SPAN

		private static bool TryCharsToHexsUnsafe(ReadOnlySpan<char> chars, out uint result)
		{
			int word = 0;
			for (int i = 0; i < 8; i++)
			{
				int a = CharToHex(chars[i]);
				if (a == INVALID_CHAR)
				{
					result = 0;
					return false;
				}
				word = (word << 4) | a;
			}
			result = (uint)word;
			return true;
		}

		private static bool TryDecode16Unsafe(ReadOnlySpan<char> chars, bool separator, out Uuid64 result)
		{
			if ((!separator || chars[8] == '-')
			 && TryCharsToHexsUnsafe(chars, out uint hi)
			 && TryCharsToHexsUnsafe(chars.Slice(separator ? 9 : 8), out uint lo))
			{
				result = new Uuid64(((ulong)hi << 32) | lo);
				return true;
			}
			result = default(Uuid64);
			return false;
		}

#else

		private static unsafe bool TryCharsToHexsUnsafe(char* chars, int numChars, out uint result)
		{
			int word = 0;
			for (int i = 0; i < 8; i++)
			{
				int a = CharToHex(chars[i]);
				if (a == INVALID_CHAR)
				{
					result = 0;
					return false;
				}
				word = (word << 4) | a;
			}
			result = (uint)word;
			return true;
		}

		private static unsafe bool TryDecode16Unsafe(char* chars, int numChars, bool separator, out Uuid64 result)
		{
			if ((!separator || chars[8] == '-')
			&& TryCharsToHexsUnsafe(chars, numChars, out uint hi)
			&& TryCharsToHexsUnsafe(chars + (separator ? 9 : 8), numChars - (separator ? 9 : 8), out uint lo))
			{
				result = new Uuid64(((ulong)hi << 32) | lo);
				return true;
			}
			result = default(Uuid64);
			return false;
		}

#endif

		#endregion

		#region Base62 encoding...

		//NOTE: this version of base62 encoding puts the digits BEFORE the letters, to ensure that the string representation of a UUID64 is in the same order as its byte[] or ulong version.
		// => This scheme use the "0-9A-Za-z" ordering, while most other base62 encoder use "a-zA-Z0-9"

		private static class Base62
		{
			//note: nested static class, so that we only allocate the internal buffers if Base62 encoding is actually used

			private static readonly char[] Base62LexicographicChars = "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz".ToCharArray();

			private static readonly int[] Base62Values = new int[3 * 32]
			{
				/* 32.. 63 */ -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, 0, 1, 2, 3, 4, 5, 6, 7, 8, 9, -1, -1, -1, -1, -1, -1,
				/* 64.. 95 */ -1, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20, 21, 22, 23, 24, 25, 26, 27, 28, 29, 30, 31, 32, 33, 34, 35, -1, -1, -1, -1, -1,
				/* 96..127 */ -1, 36, 37, 38, 39, 40, 41, 42, 43, 44, 45, 46, 47, 48, 49, 50, 51, 52, 53, 54, 55, 56, 57, 58, 59, 60, 61, -1, -1, -1, -1, -1,
			};

			/// <summary>Encode a 64-bit value into a base-62 string</summary>
			/// <param name="value">64-bit value to encode</param>
			/// <param name="padded">If true, keep the leading '0' to return a string of length 11. If false, discards all extra leading '0' digits.</param>
			/// <returns>String that contains only digits, lower and upper case letters. The string will be lexicographically ordered, which means that sorting by string will give the same order as sorting by value.</returns>
			/// <sample>
			/// Encode62(0, false) => "0"
			/// Encode62(0, true) => "00000000000"
			/// Encode62(0xDEADBEEF) => ""
			/// </sample>
			public static string Encode(ulong value, bool padded)
			{
				// special case for default(Uuid64) which may be more frequent than others
				if (value == 0) return padded ? "00000000000" : "0";

				// encoding a 64 bits value in Base62 yields 10.75 "digits", which is rounded up to 11 chars.
				const int MAX_SIZE = 11;

				unsafe
				{
					// The maximum size is 11 chars, but we will allocate 64 bytes on the stack to keep alignment.
					char* chars = stackalloc char[16];
					char[] bc = Base62LexicographicChars;

					// start from the last "digit"
					char* pc = chars + (MAX_SIZE - 1);

					while (pc >= chars)
					{
						ulong r = value % 62L;
						value /= 62L;
						*pc-- = bc[(int) r];
						if (!padded && value == 0)
						{ // the rest will be all zeroes
							break;
						}
					}

					++pc;
					int count = MAX_SIZE - (int) (pc - chars);
					Contract.Assert(count > 0 && count <= 11);
					return count <= 0 ? String.Empty : new string(pc, 0, count);
				}
			}

#if ENABLE_SPAN

			public static bool TryDecode(char[] s, out ulong value)
			{
				if (s == null) { value = 0; return false; }
				return TryDecode(new ReadOnlySpan<char>(s), out value);
			}

			public static bool TryDecode(ReadOnlySpan<char> s, out ulong value)
			{
				if (s == null || s.Length == 0 || s.Length > 11)
				{ // fail: too small/too big
					value = 0;
					return false;
				}

				// we know that the original value is exactly 64bits, and any missing digit is '0'
				ulong factor = 1UL;
				ulong acc = 0UL;
				int p = s.Length - 1;
				int[] bv = Base62Values;
				while (p >= 0)
				{
					// read digit
					int a = s[p];
					// decode base62 digit
					a = a >= 32 && a < 128 ? bv[a - 32] : -1;
					if (a == -1)
					{ // fail: invalid character
						value = 0;
						return false;
					}
					// accumulate, while checking for overflow
					acc = checked(acc + ((ulong) a * factor));
					if (p-- > 0) factor *= 62;
				}
				value = acc;
				return true;
			}

#else


			public static bool TryDecode(char[] s, out ulong value)
			{
				if (s == null) { value = 0; return false; }

				unsafe
				{
					fixed (char* chars = s)
					{
						return TryDecode(chars, s.Length, out value);
					}
				}
			}

			public static unsafe bool TryDecode(char* chars, int numChars, out ulong value)
			{
				if (chars == null || numChars == 0 || numChars > 11)
				{ // fail: too small/too big
					value = 0;
					return false;
				}

				// we know that the original value is exactly 64bits, and any missing digit is '0'
				ulong factor = 1UL;
				ulong acc = 0UL;
				int p = numChars - 1;
				int[] bv = Base62Values;
				while (p >= 0)
				{
					// read digit
					int a = chars[p];
					// decode base62 digit
					a = a >= 32 && a < 128 ? bv[a - 32] : -1;
					if (a == -1)
					{ // fail: invalid character
						value = 0;
						return false;
					}
					// accumulate, while checking for overflow
					acc = checked(acc + ((ulong) a * factor));
					if (p-- > 0) factor *= 62;
				}
				value = acc;
				return true;
			}

#endif

		}

		#endregion

		#region Unsafe I/O...

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		internal static unsafe ulong ReadUnsafe([NotNull] byte* src)
		{
			//Contract.Requires(src != null);
			return UnsafeHelpers.LoadUInt64BE(src);
		}

#if ENABLE_SPAN
		internal static unsafe ulong ReadUnsafe(ReadOnlySpan<byte> src)
		{
			//Contract.Requires(src.Length >= 0);
			fixed (byte* ptr = &MemoryMarshal.GetReference(src))
			{
				return UnsafeHelpers.LoadUInt64BE(ptr);
			}
		}
#endif

		[Pure]
		public static ulong ReadUnsafe([NotNull] byte[] buffer, int offset)
		{
			//Contract.Requires(buffer != null && offset >= 0 && offset + 7 < buffer.Length);
			// buffer contains the bytes in Big Endian
			unsafe
			{
				fixed (byte* ptr = &buffer[offset])
				{
					return UnsafeHelpers.LoadUInt64BE(ptr);
				}
			}
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static unsafe void WriteUnsafe(ulong value, byte* ptr)
		{
			//Contract.Requires(ptr != null);
			UnsafeHelpers.StoreUInt64BE(ptr, value);
		}

		public static void WriteUnsafe(ulong value, [NotNull] byte[] buffer, int offset)
		{
			//Contract.Requires(buffer != null && offset >= 0 && offset + 7 < buffer.Length);
			unsafe
			{
				fixed (byte* ptr = &buffer[offset])
				{
					UnsafeHelpers.StoreUInt64BE(ptr, value);
				}
			}
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public unsafe void WriteToUnsafe([NotNull] byte* ptr)
		{
			WriteUnsafe(m_value, ptr);
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void WriteToUnsafe([NotNull] byte[] buffer, int offset)
		{
			WriteUnsafe(m_value, buffer, offset);
		}

#if ENABLE_SPAN
		public void WriteTo(byte[] buffer, int offset)
		{
			WriteTo(buffer.AsSpan(offset));
		}

		public void WriteTo(Span<byte> destination)
		{
			if (destination.Length < 8) throw FailInvalidBufferSize(nameof(destination));
			unsafe
			{
				fixed (byte* ptr = &MemoryMarshal.GetReference(destination))
				{
					WriteUnsafe(m_value, ptr);
				}
			}
		}

		public bool TryWriteTo(Span<byte> destination)
		{
			if (destination.Length < 8) return false;
			unsafe
			{
				fixed (byte* ptr = &MemoryMarshal.GetReference(destination))
				{
					WriteUnsafe(m_value, ptr);
					return true;
				}
			}
		}
#else
		public void WriteTo(byte[] buffer, int offset)
		{
			WriteTo(buffer.AsSlice(offset));
		}

		public void WriteTo(Slice destination)
		{
			if (destination.Count < 8) throw FailInvalidBufferSize(nameof(destination));
			unsafe
			{
				fixed (byte* ptr = &destination.DangerousGetPinnableReference())
				{
					WriteUnsafe(m_value, ptr);
				}
			}
		}

		public bool TryWriteTo(Slice destination)
		{
			if (destination.Count < 8) return false;
			unsafe
			{
				fixed (byte* ptr = &destination.DangerousGetPinnableReference())
				{
					WriteUnsafe(m_value, ptr);
					return true;
				}
			}
		}
#endif

		#endregion

		#region Operators...

		public static bool operator ==(Uuid64 left, Uuid64 right)
		{
			return left.m_value == right.m_value;
		}

		public static bool operator !=(Uuid64 left, Uuid64 right)
		{
			return left.m_value != right.m_value;
		}

		public static bool operator >(Uuid64 left, Uuid64 right)
		{
			return left.m_value > right.m_value;
		}

		public static bool operator >=(Uuid64 left, Uuid64 right)
		{
			return left.m_value >= right.m_value;
		}

		public static bool operator <(Uuid64 left, Uuid64 right)
		{
			return left.m_value < right.m_value;
		}

		public static bool operator <=(Uuid64 left, Uuid64 right)
		{
			return left.m_value <= right.m_value;
		}

		// Comparing an Uuid64 to a 64-bit integer can have sense for "if (id == 0)" or "if (id != 0)" ?

		public static bool operator ==(Uuid64 left, long right)
		{
			return left.m_value == (ulong)right;
		}

		public static bool operator ==(Uuid64 left, ulong right)
		{
			return left.m_value == right;
		}

		public static bool operator !=(Uuid64 left, long right)
		{
			return left.m_value != (ulong)right;
		}

		public static bool operator !=(Uuid64 left, ulong right)
		{
			return left.m_value != right;
		}

		/// <summary>Add a value from this instance</summary>
		public static Uuid64 operator +(Uuid64 left, long right)
		{
			//TODO: how to handle overflow ? negative values ?
			ulong v = (ulong)right;
			return new Uuid64(checked(left.m_value + v));
		}

		/// <summary>Add a value from this instance</summary>
		public static Uuid64 operator +(Uuid64 left, ulong right)
		{
			return new Uuid64(checked(left.m_value + right));
		}

		/// <summary>Subtract a value from this instance</summary>
		public static Uuid64 operator -(Uuid64 left, long right)
		{
			//TODO: how to handle overflow ? negative values ?
			ulong v = (ulong)right;
			return new Uuid64(checked(left.m_value - v));
		}

		/// <summary>Subtract a value from this instance</summary>
		public static Uuid64 operator -(Uuid64 left, ulong right)
		{
			return new Uuid64(checked(left.m_value - right));
		}

		/// <summary>Increments the value of this instance</summary>
		public static Uuid64 operator ++(Uuid64 value)
		{
			return new Uuid64(checked(value.m_value + 1));
		}

		/// <summary>Decrements the value of this instance</summary>
		public static Uuid64 operator --(Uuid64 value)
		{
			return new Uuid64(checked(value.m_value - 1));
		}

		#endregion

		/// <summary>Instance of this times can be used to test Uuid64 for equality and ordering</summary>
		public sealed class Comparer : IEqualityComparer<Uuid64>, IComparer<Uuid64>
		{

			public static readonly Comparer Default = new Comparer();

			private Comparer()
			{ }

			public bool Equals(Uuid64 x, Uuid64 y)
			{
				return x.m_value == y.m_value;
			}

			public int GetHashCode(Uuid64 obj)
			{
				return obj.m_value.GetHashCode();
			}

			public int Compare(Uuid64 x, Uuid64 y)
			{
				return x.m_value.CompareTo(y.m_value);
			}
		}

		/// <summary>Generates 64-bit UUIDs using a secure random number generator</summary>
		/// <remarks>Methods of this type are thread-safe.</remarks>
		[PublicAPI]
		public sealed class RandomGenerator
		{

			/// <summary>Default instance of a random generator</summary>
			/// <remarks>Using this instance will introduce a global lock in your application. You can create specific instances for worker threads, if you require concurrency.</remarks>
			[NotNull]
			public static readonly Uuid64.RandomGenerator Default = new Uuid64.RandomGenerator();

			[NotNull] 
			private RandomNumberGenerator Rng { get; }

			[NotNull] 
			private readonly byte[] Scratch = new byte[SizeOf];

			/// <summary>Create a new instance of a random UUID generator</summary>
			public RandomGenerator()
				: this(null)
			{ }

			/// <summary>Create a new instance of a random UUID generator, using a specific random number generator</summary>
			public RandomGenerator(RandomNumberGenerator generator)
			{
				this.Rng = generator ?? RandomNumberGenerator.Create();
			}

			/// <summary>Return a new random 64-bit UUID</summary>
			/// <returns>Uuid64 that contains 64 bits worth of randomness.</returns>
			/// <remarks>
			/// <p>This methods needs to acquire a lock. If multiple threads needs to generate ids concurrently, you may need to create an instance of this class for each threads.</p>
			/// <p>The uniqueness of the generated uuids depends on the quality of the random number generator. If you cannot tolerate collisions, you either have to check if a newly generated uid already exists, or use a different kind of generator.</p>
			/// </remarks>
			[Pure]
			// ReSharper disable once MemberHidesStaticFromOuterClass
			public Uuid64 NewUuid()
			{
				//REVIEW: OPTIMIZE: use a per-thread instance of the rng and scratch buffer?
				// => right now, NewUuid() is a Global Lock for the whole process!
				lock (this.Rng)
				{
					// get 8 bytes of randomness (0 allowed)
					this.Rng.GetBytes(this.Scratch);
					//note: do *NOT* call GetBytes(byte[], int, int) because it creates creates a temp buffer, calls GetBytes(byte[]) and copy the result back! (as of .NET 4.7.1)
					//TODO: PERF: use Span<byte> APIs once (if?) they become available!
					return Uuid64.Read(this.Scratch);
				}
			}

		}
	}

}
