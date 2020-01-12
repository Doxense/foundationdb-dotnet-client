#region Copyright (c) 2013-2020, Doxense SAS. All rights reserved.
// See License.MD for license information
#endregion

#if !USE_SHARED_FRAMEWORK

namespace System
{
	using System;
	using System.Collections.Generic;
	using System.ComponentModel;
	using System.Diagnostics;
	using System.Runtime.CompilerServices;
	using System.Runtime.InteropServices;
	using System.Security.Cryptography;
	using Doxense.Diagnostics.Contracts;
	using Doxense.Memory;
	using JetBrains.Annotations;

	/// <summary>Represents a 80-bit UUID that is stored in high-endian format on the wire</summary>
	[DebuggerDisplay("[{ToString(),nq}]")]
	[ImmutableObject(true), PublicAPI, Serializable]
	public readonly struct Uuid80 : IFormattable, IEquatable<Uuid80>, IComparable<Uuid80>
	{
		/// <summary>Uuid with all bits set to 0</summary>
		public static readonly Uuid80 Empty = default(Uuid80);

		/// <summary>Uuid with all bits set to 1</summary>
		public static readonly Uuid80 MaxValue = new Uuid80(ushort.MaxValue, ulong.MaxValue);

		/// <summary>Size is 10 bytes</summary>
		public const int SizeOf = 10;

		private readonly ushort Hi;
		private readonly ulong Lo;
		//note: this will be in host order (so probably Little-Endian) in order to simplify parsing and ordering

		#region Constructors...

		/// <summary>Pack components into a 80-bit UUID</summary>
		/// <param name="a">XXXX-........-........</param>
		/// <param name="b">....-XXXXXXXX-XXXXXXXX</param>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public Uuid80(ushort a, ulong b)
		{
			this.Hi = a;
			this.Lo = b;
		}

		/// <summary>Pack components into a 80-bit UUID</summary>
		/// <param name="a">XXXX-........-........</param>
		/// <param name="b">....-XXXXXXXX-XXXXXXXX</param>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public Uuid80(ushort a, long b)
		{
			this.Hi = a;
			this.Lo = (ulong) b;
		}

		/// <summary>Pack components into a 80-bit UUID</summary>
		/// <param name="a">XXXX-........-........</param>
		/// <param name="b">....-XXXXXXXX-XXXXXXXX</param>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public Uuid80(int a, long b)
		{
			Contract.Requires((uint) a <= 0xFFFF);
			this.Hi = (ushort) a;
			this.Lo = (ulong) b;
		}

		/// <summary>Pack components into a 80-bit UUID</summary>
		/// <param name="a">XXXX-........-........</param>
		/// <param name="b">....-XXXXXXXX-........</param>
		/// <param name="c">....-........-XXXXXXXX</param>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public Uuid80(ushort a, uint b, uint c)
		{
			this.Hi = a;
			this.Lo = ((ulong) b) << 32 | c;
		}

		/// <summary>Pack components into a 80-bit UUID</summary>
		/// <param name="a">XXXX-........-........</param>
		/// <param name="b">....-XXXXXXXX-........</param>
		/// <param name="c">....-........-XXXXXXXX</param>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public Uuid80(int a, int b, int c)
		{
			Contract.Requires((uint) a <= 0xFFFF);
			this.Hi = (ushort) a;
			this.Lo = ((ulong) (uint) b) << 32 | (uint) c;
		}

		/// <summary>Pack components into a 80-bit UUID</summary>
		/// <param name="a">XXXX-........-........</param>
		/// <param name="b">....-XXXX....-........</param>
		/// <param name="c">....-....XXXX-........</param>
		/// <param name="d">....-........-XXXX....</param>
		/// <param name="e">....-........-....XXXX</param>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public Uuid80(ushort a, ushort b, ushort c, ushort d, ushort e)
		{
			this.Hi = a;
			this.Lo = ((ulong) b) << 48 | ((ulong) c) << 32 | ((ulong) d) << 16 | ((ulong) e);
		}

		[Pure, NotNull, MethodImpl(MethodImplOptions.NoInlining)]
		private static Exception FailInvalidBufferSize([InvokerParameterName] string arg)
		{
			return ThrowHelper.ArgumentException(arg, "Value must be 10 bytes long");
		}

		[Pure, NotNull, MethodImpl(MethodImplOptions.NoInlining)]
		private static Exception FailInvalidFormat()
		{
			return ThrowHelper.FormatException("Invalid " + nameof(Uuid80) + " format");
		}

		/// <summary>Generate a new random 80-bit UUID.</summary>
		/// <remarks>If you need sequential or cryptographic uuids, you should use a different generator.</remarks>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static Uuid80 NewUuid()
		{
			unsafe
			{
				// we use Guid.NewGuid() as a source of ~128 bits of entropy, and we fold the extra 48 bits onto the first 80 bits
				var x = Guid.NewGuid();
				ushort* p = (ushort*) &x;
				return new Uuid80((ushort) (p[0] ^ p[5]), (ushort) (p[1] ^ p[6]), (ushort) (p[2] ^ p[7]), p[3], p[4]);
			}
		}

		#endregion

		#region Decomposition...

		/// <summary>Split into two fragments</summary>
		/// <param name="a">xxxx-........-........</param>
		/// <param name="b">....-xxxxxxxx-xxxxxxxx</param>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void Deconstruct(out ushort a, out ulong b)
		{
			a = this.Hi;
			b = this.Lo;
		}

		/// <summary>Split into three fragments</summary>
		/// <param name="a">xxxx-........-........</param>
		/// <param name="b">....-xxxxxxxx-........</param>
		/// <param name="c">....-........-xxxxxxxx</param>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void Deconstruct(out ushort a, out uint b, out uint c)
		{
			a = this.Hi;
			b = (uint) (this.Lo >> 32);
			c = (uint) this.Lo;
		}

		#endregion

		#region Reading...

		/// <summary>Read a 80-bit UUID from a byte array</summary>
		/// <param name="value">Array of exactly 0 or 10 bytes</param>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static Uuid80 Read(byte[] value)
		{
			return Read(value.AsSpan());
		}

		/// <summary>Read a 80-bit UUID from slice of memory</summary>
		/// <param name="value">slice of exactly 0 or 10 bytes</param>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static Uuid80 Read(Slice value)
		{
			return Read(value.Span);
		}

		/// <summary>Read a 80-bit UUID from slice of memory</summary>
		/// <param name="value">Span of exactly 0 or 10 bytes</param>
		[Pure]
		public static Uuid80 Read(ReadOnlySpan<byte> value)
		{
			if (value.Length == 0) return default;
			if (value.Length == SizeOf) { ReadUnsafe(value, out var res); return res; }
			throw FailInvalidBufferSize(nameof(value));
		}

		#endregion

		#region Parsing...

		/// <summary>Parse a string representation of an Uuid80</summary>
		/// <paramref name="buffer">String in either formats: "", "badc0ffe-e0ddf00d", "badc0ffee0ddf00d", "{badc0ffe-e0ddf00d}", "{badc0ffee0ddf00d}"</paramref>
		/// <remarks>Parsing is case-insensitive. The empty string is mapped to <see cref="Empty">Uuid80.Empty</see>.</remarks>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static Uuid80 Parse([NotNull] string buffer)
		{
			Contract.NotNull(buffer, nameof(buffer));
			if (!TryParse(buffer, out var value))
			{
				throw FailInvalidFormat();
			}
			return value;
		}

		/// <summary>Parse a string representation of an Uuid80</summary>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static Uuid80 Parse(ReadOnlySpan<char> buffer)
		{
			if (!TryParse(buffer, out var value))
			{
				throw FailInvalidFormat();
			}
			return value;
		}

		/// <summary>Parse a string representation of an Uuid80</summary>
		[Pure]
		[Obsolete("Use Uuid80.Parse(ReadOnlySpan<char>) instead", error: true)] //TODO: remove me!
		public static unsafe Uuid80 Parse(char* buffer, int count)
		{
			if (count == 0) return default(Uuid80);
			if (!TryParse(new ReadOnlySpan<char>(buffer, count), out var value))
			{
				throw FailInvalidFormat();
			}
			return value;
		}

		/// <summary>Try parsing a string representation of an Uuid80</summary>
		public static bool TryParse([NotNull] string buffer, out Uuid80 result)
		{
			Contract.NotNull(buffer, nameof(buffer));
			return TryParse(buffer.AsSpan(), out result);
		}

		/// <summary>Try parsing a string representation of an Uuid80</summary>
		public static bool TryParse(ReadOnlySpan<char> s, out Uuid80 result)
		{
			Contract.Requires(s != null);

			// we support the following formats: "{hex8-hex8}", "{hex16}", "hex8-hex8", "hex16" and "base62"
			// we don't support base10 format, because there is no way to differentiate from hex or base62

			// remove "{...}" if there is any
			if (s.Length > 2 && s[0] == '{' && s[s.Length - 1] == '}')
			{
				s = s.Slice(1, s.Length - 2);
			}

			result = default(Uuid80);
			switch (s.Length)
			{
				case 0:
				{ // empty
					return true;
				}
				case 20:
				{ // xxxxxxxxxxxxxxxxxxxx
					return TryDecode16Unsafe(s, separator: false, out result);
				}
				case 22:
				{ // xxxx-xxxxxxxx-xxxxxxxx
					if (s[4] != '-' || s[13] != '-') return false;
					return TryDecode16Unsafe(s, separator: true, out result);
				}
				default:
				{
					return false;
				}
			}
		}

		#endregion

		#region IFormattable...

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public Slice ToSlice()
		{
			var writer = new SliceWriter(SizeOf);
			writer.WriteFixed16BE(this.Hi);
			writer.WriteFixed64BE(this.Lo);
			return writer.ToSlice();
		}

		[Pure, NotNull]
		public byte[] ToByteArray()
		{
			var tmp = new byte[SizeOf];
			unsafe
			{
				fixed (byte* ptr = &tmp[0])
				{
					UnsafeHelpers.StoreUInt16BE(ptr, this.Hi);
					UnsafeHelpers.StoreUInt64BE(ptr + 2, this.Lo);
				}
			}
			return tmp;
		}

		/// <summary>Returns a string representation of the value of this instance.</summary>
		/// <returns>String using the format "xxxxxxxx-xxxxxxxx", where 'x' is a lower-case hexadecimal digit</returns>
		/// <remarks>Strings returned by this method will always to 17 characters long.</remarks>
		public override string ToString()
		{
			return ToString("D", null);
		}

		/// <summary>Returns a string representation of the value of this <see cref="Uuid80"/> instance, according to the provided format specifier.</summary>
		/// <param name="format">A single format specifier that indicates how to format the value of this Guid. The format parameter can be "D", "B", "X", "G", "Z" or "N". If format is null or an empty string (""), "D" is used.</param>
		/// <returns>The value of this <see cref="Uuid80"/>, using the specified format.</returns>
		/// <remarks>See <see cref="ToString(string, IFormatProvider)"/> for a description of the different formats</remarks>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public string ToString(string format)
		{
			return ToString(format, null);
		}

		/// <summary>Returns a string representation of the value of this instance of the <see cref="Uuid80"/> class, according to the provided format specifier and culture-specific format information.</summary>
		/// <param name="format">A single format specifier that indicates how to format the value of this Guid. The format parameter can be "D", "N", "Z", "R", "X" or "B". If format is null or an empty string (""), "D" is used.</param>
		/// <param name="formatProvider">An object that supplies culture-specific formatting information. Only used for the "R" format.</param>
		/// <returns>The value of this <see cref="Uuid80"/>, using the specified format.</returns>
		/// <example>
		/// <p>The <b>D</b> format encodes the value as three groups of hexadecimal digits, separated by an hyphen: "aaaa-bbbbbbbb-cccccccc" (22 characters).</p>
		/// <p>The <b>X</b> and <b>N</b> format encodes the value as a single group of 20 hexadecimal digits: "aaaabbbbbbbbcccccccc" (20 characters).</p>
		/// <p>The <b>B</b> format is equivalent to the <b>D</b> format, but surrounded with '{' and '}': "{aaaa-bbbbbbbb-cccccccc}" (24 characters).</p>
		/// </example>
		public string ToString(string format, IFormatProvider formatProvider)
		{
			if (string.IsNullOrEmpty(format)) format = "D";

			switch(format)
			{
				case "D":
				{ // Default format is "XXXX-XXXXXXXX-XXXXXXXX"
					return Encode16(this.Hi, this.Lo, separator: true, quotes: false, upper: true);
				}
				case "d":
				{ // Default format is "xxxx-xxxxxxxx-xxxxxxxx"
					return Encode16(this.Hi, this.Lo, separator: true, quotes: false, upper: false);
				}
				case "X": //TODO: Guid.ToString("X") returns "{0x.....,0x.....,...}"
				case "N":
				{ // "XXXXXXXXXXXXXXXXXXXX"
					return Encode16(this.Hi, this.Lo, separator: false, quotes: false, upper: true);
				}
				case "x": //TODO: Guid.ToString("X") returns "{0x.....,0x.....,...}"
				case "n":
				{ // "xxxxxxxxxxxxxxxxxxxx"
					return Encode16(this.Hi, this.Lo, separator: false, quotes: false, upper: false);
				}

				case "B":
				{ // "{XXXX-XXXXXXXX-XXXXXXXX}"
					return Encode16(this.Hi, this.Lo, separator: true, quotes: true, upper: true);
				}
				case "b":
				{ // "{xxxx-xxxxxxxx-xxxxxxxx}"
					return Encode16(this.Hi, this.Lo, separator: true, quotes: true, upper: false);
				}
				default:
				{
					throw new FormatException("Invalid " + nameof(Uuid80) + " format specification.");
				}
			}
		}

		#endregion

		#region IEquatable / IComparable...

		public override bool Equals(object obj)
		{
			switch (obj)
			{
				case Uuid80 uuid: return Equals(uuid);
				//TODO: string format ? Slice ?
			}
			return false;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public override int GetHashCode()
		{
			return this.Hi.GetHashCode() ^ this.Lo.GetHashCode();
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool Equals(Uuid80 other)
		{
			return this.Hi == other.Hi & this.Lo == other.Lo;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public int CompareTo(Uuid80 other)
		{
			int cmp = this.Hi.CompareTo(other.Hi);
			if (cmp == 0) cmp = this.Lo.CompareTo(other.Lo);
			return cmp;
		}

		#endregion

		#region Base16 encoding...

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		private static char HexToLowerChar(uint a)
		{
			a &= 0xF;
			return a > 9 ? (char)(a - 10 + 'a') : (char)(a + '0');
		}

		[NotNull]
		private static unsafe char* HexsToLowerChars([NotNull] char* ptr, ushort a)
		{
			Contract.Requires(ptr != null);
			ptr[0] = HexToLowerChar((uint) a >> 12);
			ptr[1] = HexToLowerChar((uint) a >> 8);
			ptr[2] = HexToLowerChar((uint) a >> 4);
			ptr[3] = HexToLowerChar((uint) a);
			return ptr + 4;
		}

		[NotNull]
		private static unsafe char* HexsToLowerChars([NotNull] char* ptr, uint a)
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

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		private static char HexToUpperChar(uint a)
		{
			a &= 0xF;
			return a > 9 ? (char)(a - 10 + 'A') : (char)(a + '0');
		}

		[NotNull]
		private static unsafe char* Hex16ToUpperChars([NotNull] char* ptr, ushort a)
		{
			Contract.Requires(ptr != null);
			ptr[0] = HexToUpperChar((uint) a >> 12);
			ptr[1] = HexToUpperChar((uint) a >> 8);
			ptr[2] = HexToUpperChar((uint) a >> 4);
			ptr[3] = HexToUpperChar((uint) a);
			return ptr + 4;
		}

		[NotNull]
		private static unsafe char* Hex32ToUpperChars([NotNull] char* ptr, uint a)
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
		private static unsafe string Encode16(ushort hi, ulong lo, bool separator, bool quotes, bool upper)
		{
			int size = 20 + (separator ? 2 : 0) + (quotes ? 2 : 0);
			char* buffer = stackalloc char[24]; // max 24 mais on arrondi a 32

			char* ptr = buffer;
			if (quotes) *ptr++ = '{';
			ptr = upper
				? Hex16ToUpperChars(ptr, hi)
				: HexsToLowerChars(ptr, hi);
			if (separator) *ptr++ = '-';
			ptr = upper
				? Hex32ToUpperChars(ptr, (uint) (lo >> 32))
				: HexsToLowerChars(ptr, (uint) (lo >> 32));
			if (separator) *ptr++ = '-';
			ptr = upper
				? Hex32ToUpperChars(ptr, (uint) lo)
				: HexsToLowerChars(ptr, (uint) lo);
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

		private static bool TryCharsToHex16(ReadOnlySpan<char> chars, out ushort result)
		{
			int word = 0;
			for (int i = 0; i < 4; i++)
			{
				int a = CharToHex(chars[i]);
				if (a == INVALID_CHAR)
				{
					result = 0;
					return false;
				}
				word = (word << 4) | a;
			}
			result = (ushort) word;
			return true;
		}

		private static bool TryCharsToHex32(ReadOnlySpan<char> chars, out uint result)
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

		private static bool TryDecode16Unsafe(ReadOnlySpan<char> chars, bool separator, out Uuid80 result)
		{
			// aaaabbbbbbbbcccccccc
			// aaaa-bbbbbbbb-cccccccc
			if ((!separator || (chars[4] == '-' && chars[13] == '-'))
			 && TryCharsToHex16(chars, out ushort hi)
			 && TryCharsToHex32(chars.Slice(separator ? 5 : 4), out uint med)
			 && TryCharsToHex32(chars.Slice(separator ? 14 : 12), out uint lo))
			{
				result = new Uuid80(hi, med, lo);
				return true;
			}
			result = default(Uuid80);
			return false;
		}

		#endregion

		#region Unsafe I/O...

		internal static unsafe void ReadUnsafe(ReadOnlySpan<byte> src, out Uuid80 result)
		{
			//Paranoid.Requires(src.Length >= 10);
			fixed (byte* ptr = &MemoryMarshal.GetReference(src))
			{
				result = new Uuid80(UnsafeHelpers.LoadUInt16BE(ptr), UnsafeHelpers.LoadUInt64BE(ptr + 2));
			}
		}

		internal static void WriteUnsafe(ushort hi, ulong lo, [NotNull] Span<byte> buffer)
		{
			//Paranoid.Requires(buffer.Length >= 10);
			unsafe
			{
				fixed (byte* ptr = &MemoryMarshal.GetReference(buffer))
				{
					UnsafeHelpers.StoreUInt16BE(ptr, hi);
					UnsafeHelpers.StoreUInt64BE(ptr + 2, lo);
				}
			}
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		internal unsafe void WriteToUnsafe([NotNull] Span<byte> buf)
		{
			WriteUnsafe(this.Hi, this.Lo, buf);
		}

		public void WriteTo(Span<byte> destination)
		{
			if (destination.Length < SizeOf) throw FailInvalidBufferSize(nameof(destination));
			WriteUnsafe(this.Hi, this.Lo, destination);
		}

		public bool TryWriteTo(Span<byte> destination)
		{
			if (destination.Length < SizeOf) return false;
			WriteUnsafe(this.Hi, this.Lo, destination);
			return true;
		}

		#endregion

		#region Operators...

		public static bool operator ==(Uuid80 left, Uuid80 right)
		{
			return left.Hi == right.Hi & left.Lo == right.Lo;
		}

		public static bool operator !=(Uuid80 left, Uuid80 right)
		{
			return left.Hi != right.Hi | left.Lo != right.Lo;
		}

		public static bool operator >(Uuid80 left, Uuid80 right)
		{
			return left.Hi > right.Hi || (left.Hi == right.Hi && left.Lo > right.Lo);
		}

		public static bool operator >=(Uuid80 left, Uuid80 right)
		{
			return left.Hi > right.Hi || (left.Hi == right.Hi && left.Lo >= right.Lo);
		}

		public static bool operator <(Uuid80 left, Uuid80 right)
		{
			return left.Hi < right.Hi || (left.Hi == right.Hi && left.Lo < right.Lo);
		}

		public static bool operator <=(Uuid80 left, Uuid80 right)
		{
			return left.Hi < right.Hi || (left.Hi == right.Hi && left.Lo <= right.Lo);
		}

		/// <summary>Add a value from this instance</summary>
		public static Uuid80 operator +(Uuid80 left, long right)
		{
			//TODO: how to handle overflow ? negative values ?
			unchecked
			{
				ushort hi = left.Hi;
				ulong lo = left.Lo + (ulong) right;
				if (lo < left.Lo) // overflow!
				{
					++hi;
				}
				return new Uuid80(hi, lo);
			}
		}

		/// <summary>Add a value from this instance</summary>
		public static Uuid80 operator +(Uuid80 left, ulong right)
		{
			//TODO: how to handle overflow ?
			unchecked
			{
				ushort hi = left.Hi;
				ulong lo = left.Lo + right;
				if (lo < left.Lo) // overflow!
				{
					++hi;
				}
				return new Uuid80(hi, lo);
			}
		}

		/// <summary>Subtract a value from this instance</summary>
		public static Uuid80 operator -(Uuid80 left, long right)
		{
			//TODO: how to handle overflow ? negative values ?
			unchecked
			{
				ushort hi = left.Hi;
				ulong lo = left.Lo - (ulong) right;
				if (lo > left.Lo) // overflow!
				{
					--hi;
				}
				return new Uuid80(hi, lo);
			}
		}

		/// <summary>Subtract a value from this instance</summary>
		public static Uuid80 operator -(Uuid80 left, ulong right)
		{
			//TODO: how to handle overflow ?
			unchecked
			{
				ushort hi = left.Hi;
				ulong lo = left.Lo - right;
				if (lo > left.Lo) // overflow!
				{
					--hi;
				}
				return new Uuid80(hi, lo);
			}
		}

		/// <summary>Increments the value of this instance</summary>
		public static Uuid80 operator ++(Uuid80 value)
		{
			return value + 1;
		}

		/// <summary>Decrements the value of this instance</summary>
		public static Uuid80 operator --(Uuid80 value)
		{
			return value - 1;
		}

		#endregion

		/// <summary>Instance of this times can be used to test Uuid80 for equality and ordering</summary>
		public sealed class Comparer : IEqualityComparer<Uuid80>, IComparer<Uuid80>
		{

			public static readonly Comparer Default = new Comparer();

			private Comparer()
			{ }

			public bool Equals(Uuid80 x, Uuid80 y)
			{
				return x.Hi == y.Hi & x.Lo == y.Lo;
			}

			public int GetHashCode(Uuid80 obj)
			{
				return obj.GetHashCode();
			}

			public int Compare(Uuid80 x, Uuid80 y)
			{
				return x.CompareTo(y);
			}
		}

		/// <summary>Generates 80-bit UUIDs using a secure random number generator</summary>
		/// <remarks>Methods of this type are thread-safe.</remarks>
		[PublicAPI]
		public sealed class RandomGenerator
		{

			/// <summary>Default instance of a random generator</summary>
			/// <remarks>Using this instance will introduce a global lock in your application. You can create specific instances for worker threads, if you require concurrency.</remarks>
			[NotNull]
			public static readonly Uuid80.RandomGenerator Default = new Uuid80.RandomGenerator();

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

			/// <summary>Return a new random 80-bit UUID</summary>
			/// <returns>Uuid80 that contains 80 bits worth of randomness.</returns>
			/// <remarks>
			/// <p>This methods needs to acquire a lock. If multiple threads needs to generate ids concurrently, you may need to create an instance of this class for each threads.</p>
			/// <p>The uniqueness of the generated uuids depends on the quality of the random number generator. If you cannot tolerate collisions, you either have to check if a newly generated uid already exists, or use a different kind of generator.</p>
			/// </remarks>
			[Pure]
			// ReSharper disable once MemberHidesStaticFromOuterClass
			public Uuid80 NewUuid()
			{
				//REVIEW: OPTIMIZE: use a per-thread instance of the rng and scratch buffer?
				// => right now, NewUuid() is a Global Lock for the whole process!
				lock (this.Rng)
				{
					// get 10 bytes of randomness (0 allowed)
					this.Rng.GetBytes(this.Scratch);
					//note: do *NOT* call GetBytes(byte[], int, int) because it creates creates a temp buffer, calls GetBytes(byte[]) and copy the result back! (as of .NET 4.7.1)
					//TODO: PERF: use Span<byte> APIs once (if?) they become available!
					return Uuid80.Read(this.Scratch);
				}
			}

		}

	}

}

#endif
