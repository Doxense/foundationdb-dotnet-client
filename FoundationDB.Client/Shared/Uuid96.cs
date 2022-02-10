#region Copyright (c) 2013-2022, Doxense SAS. All rights reserved.
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

	/// <summary>Represents a 96-bit UUID that is stored in high-endian format on the wire</summary>
	[DebuggerDisplay("[{ToString(),nq}]")]
	[ImmutableObject(true), PublicAPI, Serializable]
	public readonly struct Uuid96 : IFormattable, IEquatable<Uuid96>, IComparable<Uuid96>
	{

		/// <summary>Uuid with all bits set to 0</summary>
		public static readonly Uuid96 Empty = default;

		/// <summary>Uuid with all bits set to 1</summary>
		public static readonly Uuid96 MaxValue = new Uuid96(uint.MaxValue, ulong.MaxValue);

		/// <summary>Size is 12 bytes</summary>
		public const int SizeOf = 12;

		private readonly uint Hi;
		private readonly ulong Lo;
		//note: this will be in host order (so probably Little-Endian) in order to simplify parsing and ordering

		#region Constructors...

		/// <summary>Pack components into a 96-bit UUID</summary>
		/// <param name="a">XXXXXXXX-........-........</param>
		/// <param name="b">........-XXXXXXXX-XXXXXXXX</param>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public Uuid96(uint a, ulong b)
		{
			this.Hi = a;
			this.Lo = b;
		}

		/// <summary>Pack components into a 96-bit UUID</summary>
		/// <param name="a">XXXXXXXX-........-........</param>
		/// <param name="b">........-XXXXXXXX-XXXXXXXX</param>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public Uuid96(uint a, long b)
		{
			this.Hi = a;
			this.Lo = (ulong) b;
		}

		/// <summary>Pack components into a 96-bit UUID</summary>
		/// <param name="a">XXXXXXXX-........-........</param>
		/// <param name="b">........-XXXXXXXX-XXXXXXXX</param>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public Uuid96(int a, long b)
		{
			this.Hi = (uint) a;
			this.Lo = (ulong) b;
		}

		/// <summary>Pack components into a 96-bit UUID</summary>
		/// <param name="a">XXXXXXXX-........-........</param>
		/// <param name="b">........-XXXXXXXX-........</param>
		/// <param name="c">........-........-XXXXXXXX</param>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public Uuid96(uint a, uint b, uint c)
		{
			this.Hi = a;
			this.Lo = ((ulong) b) << 32 | c;
		}

		/// <summary>Pack components into a 96-bit UUID</summary>
		/// <param name="a">XXXXXXXX-........-........</param>
		/// <param name="b">........-XXXXXXXX-........</param>
		/// <param name="c">........-........-XXXXXXXX</param>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public Uuid96(int a, int b, int c)
		{
			this.Hi = (uint) a;
			this.Lo = ((ulong) (uint) b) << 32 | (uint) c;
		}

		[Pure, MethodImpl(MethodImplOptions.NoInlining)]
		private static Exception FailInvalidBufferSize([InvokerParameterName] string arg)
		{
			return ThrowHelper.ArgumentException(arg, "Value must be 12 bytes long");
		}

		[Pure, MethodImpl(MethodImplOptions.NoInlining)]
		private static Exception FailInvalidFormat()
		{
			return ThrowHelper.FormatException("Invalid " + nameof(Uuid96) + " format");
		}

		/// <summary>Generate a new random 96-bit UUID.</summary>
		/// <remarks>If you need sequential or cryptographic uuids, you should use a different generator.</remarks>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static Uuid96 NewUuid()
		{
			unsafe
			{
				// we use Guid.NewGuid() as a source of ~128 bits of entropy, and we fold the extra 32 bits into the first 96 bits
				var x = Guid.NewGuid();
				uint* p = (uint*) &x;
				return new Uuid96(p[0], p[1] ^ p[3], p[2]);
			}
		}

		#endregion

		#region Decomposition...

		/// <summary>Split into two fragments</summary>
		/// <param name="a">xxxxxxxx-........-........</param>
		/// <param name="b">........-xxxxxxxx-xxxxxxxx</param>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void Deconstruct(out uint a, out ulong b)
		{
			a = this.Hi;
			b = this.Lo;
		}
		/// <summary>Split into three fragments</summary>
		/// <param name="a">xxxxxxxx-........-........</param>
		/// <param name="b">........-xxxxxxxx-........</param>
		/// <param name="c">........-........-xxxxxxxx</param>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void Deconstruct(out uint a, out uint b, out uint c)
		{
			a = this.Hi;
			b = (uint) (this.Lo >> 32);
			c = (uint) this.Lo;
		}

		#endregion

		#region Reading...

		/// <summary>Read a 96-bit UUID from a byte array</summary>
		/// <param name="value">Array of exactly 0 or 12 bytes</param>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static Uuid96 Read(byte[] value)
		{
			return Read(value.AsSpan());
		}

		/// <summary>Read a 96-bit UUID from slice of memory</summary>
		/// <param name="value">slice of exactly 0 or 12 bytes</param>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static Uuid96 Read(Slice value)
		{
			return Read(value.Span);
		}

		/// <summary>Read a 96-bit UUID from slice of memory</summary>
		/// <param name="value">Span of exactly 0 or 12 bytes</param>
		[Pure]
		public static Uuid96 Read(ReadOnlySpan<byte> value)
		{
			if (value.Length == 0) return default;
			if (value.Length == SizeOf) { ReadUnsafe(value, out var res); return res; }
			throw FailInvalidBufferSize(nameof(value));
		}

		#endregion

		#region Parsing...

		/// <summary>Parse a string representation of an Uuid96</summary>
		/// <paramref name="buffer">String in either formats: "", "badc0ffe-e0ddf00d", "badc0ffee0ddf00d", "{badc0ffe-e0ddf00d}", "{badc0ffee0ddf00d}"</paramref>
		/// <remarks>Parsing is case-insensitive. The empty string is mapped to <see cref="Empty">Uuid96.Empty</see>.</remarks>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static Uuid96 Parse(string buffer)
		{
			Contract.NotNull(buffer);
			if (!TryParse(buffer, out var value))
			{
				throw FailInvalidFormat();
			}
			return value;
		}

		/// <summary>Parse a string representation of an Uuid96</summary>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static Uuid96 Parse(ReadOnlySpan<char> buffer)
		{
			if (!TryParse(buffer, out var value))
			{
				throw FailInvalidFormat();
			}
			return value;
		}

		/// <summary>Try parsing a string representation of an Uuid96</summary>
		public static bool TryParse(string buffer, out Uuid96 result)
		{
			Contract.NotNull(buffer);
			return TryParse(buffer.AsSpan(), out result);
		}

		/// <summary>Try parsing a string representation of an Uuid96</summary>
		public static bool TryParse(ReadOnlySpan<char> s, out Uuid96 result)
		{
			Contract.Debug.Requires(s != null);

			// we support the following formats: "{hex8-hex8}", "{hex16}", "hex8-hex8", "hex16" and "base62"
			// we don't support base10 format, because there is no way to differentiate from hex or base62

			// remove "{...}" if there is any
			if (s.Length > 2 && s[0] == '{' && s[s.Length - 1] == '}')
			{
				s = s.Slice(1, s.Length - 2);
			}

			result = default(Uuid96);
			switch (s.Length)
			{
				case 0:
				{ // empty
					return true;
				}
				case 24:
				{ // xxxxxxxxxxxxxxxxxxxxxxxx
					return TryDecode16Unsafe(s, separator: false, out result);
				}
				case 26:
				{ // xxxxxxxx-xxxxxxxx-xxxxxxxx
					if (s[8] != '-' || s[17] != '-') return false;
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
			writer.WriteFixed32BE(this.Hi);
			writer.WriteFixed64BE(this.Lo);
			return writer.ToSlice();
		}

		[Pure]
		public byte[] ToByteArray()
		{
			var tmp = new byte[SizeOf];
			unsafe
			{
				fixed (byte* ptr = &tmp[0])
				{
					UnsafeHelpers.StoreUInt32BE(ptr, this.Hi);
					UnsafeHelpers.StoreUInt64BE(ptr + 4, this.Lo);
				}
			}
			return tmp;
		}

		/// <summary>Returns a string representation of the value of this instance.</summary>
		/// <returns>String using the format "XXXXXXXX-XXXXXXXX-XXXXXXXX", where 'X' is an upper-case hexadecimal digit</returns>
		/// <remarks>Strings returned by this method will always to 17 characters long.</remarks>
		public override string ToString()
		{
			return ToString("D", null);
		}

		/// <summary>Returns a string representation of the value of this <see cref="Uuid96"/> instance, according to the provided format specifier.</summary>
		/// <param name="format">A single format specifier that indicates how to format the value of this Guid. The format parameter can be "D", "B", "X", "G", "Z" or "N". If format is null or an empty string (""), "D" is used.</param>
		/// <returns>The value of this <see cref="Uuid96"/>, using the specified format.</returns>
		/// <remarks>See <see cref="ToString(string, IFormatProvider)"/> for a description of the different formats</remarks>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public string ToString(string? format)
		{
			return ToString(format, null);
		}

		/// <summary>Returns a string representation of the value of this instance of the <see cref="Uuid96"/> class, according to the provided format specifier and culture-specific format information.</summary>
		/// <param name="format">A single format specifier that indicates how to format the value of this Guid. The format parameter can be "D", "N", "Z", "R", "X" or "B". If format is null or an empty string (""), "D" is used.</param>
		/// <param name="formatProvider">An object that supplies culture-specific formatting information. Only used for the "R" format.</param>
		/// <returns>The value of this <see cref="Uuid96"/>, using the specified format.</returns>
		/// <example>
		/// <p>The <b>D</b> format encodes the value as three groups of hexadecimal digits, separated by an hyphen: "aaaaaaaa-bbbbbbbb-cccccccc" (26 characters).</p>
		/// <p>The <b>X</b> and <b>N</b> format encodes the value as a single group of 24 hexadecimal digits: "aaaaaaaabbbbbbbbcccccccc" (24 characters).</p>
		/// <p>The <b>B</b> format is equivalent to the <b>D</b> format, but surrounded with '{' and '}': "{aaaaaaaa-bbbbbbbb-cccccccc}" (28 characters).</p>
		/// </example>
		public string ToString(string? format, IFormatProvider? formatProvider)
		{
			if (string.IsNullOrEmpty(format)) format = "D";

			switch(format)
			{
				case "D":
				{ // Default format is "XXXXXXXX-XXXXXXXX-XXXXXXXX"
					return Encode16(this.Hi, this.Lo, separator: true, quotes: false, upper: true);
				}
				case "d":
				{ // Default format is "xxxxxxxx-xxxxxxxx-xxxxxxxx"
					return Encode16(this.Hi, this.Lo, separator: true, quotes: false, upper: false);
				}
				case "X": //TODO: Guid.ToString("X") returns "{0x.....,0x.....,...}"
				case "N":
				{ // "XXXXXXXXXXXXXXXXXXXXXXXX"
					return Encode16(this.Hi, this.Lo, separator: false, quotes: false, upper: true);
				}
				case "x": //TODO: Guid.ToString("X") returns "{0x.....,0x.....,...}"
				case "n":
				{ // "xxxxxxxxxxxxxxxxxxxxxxxx"
					return Encode16(this.Hi, this.Lo, separator: false, quotes: false, upper: false);
				}

				case "B":
				{ // "{XXXXXXXX-XXXXXXXX-XXXXXXXX}"
					return Encode16(this.Hi, this.Lo, separator: true, quotes: true, upper: true);
				}
				case "b":
				{ // "{xxxxxxxx-xxxxxxxx-xxxxxxxx}"
					return Encode16(this.Hi, this.Lo, separator: true, quotes: true, upper: false);
				}
				default:
				{
					throw new FormatException("Invalid " + nameof(Uuid96) + " format specification.");
				}
			}
		}

		#endregion

		#region IEquatable / IComparable...

		public override bool Equals(object? obj)
		{
			switch (obj)
			{
				case Uuid96 uuid: return Equals(uuid);
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
		public bool Equals(Uuid96 other)
		{
			return this.Hi == other.Hi & this.Lo == other.Lo;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public int CompareTo(Uuid96 other)
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

		private static unsafe char* Hex32ToLowerChars(char* ptr, uint a)
		{
			Contract.Debug.Requires(ptr != null);
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

		private static unsafe char* Hex32ToUpperChars(char* ptr, uint a)
		{
			Contract.Debug.Requires(ptr != null);
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

		[Pure]
		private static unsafe string Encode16(uint hi, ulong lo, bool separator, bool quotes, bool upper)
		{
			int size = SizeOf * 2 + (separator ? 2 : 0) + (quotes ? 2 : 0);
			char* buffer = stackalloc char[32]; // max 26 mais on arrondi a 32

			char* ptr = buffer;
			if (quotes) *ptr++ = '{';
			if (upper)
			{
				ptr = Hex32ToUpperChars(ptr, hi);
				if (separator) *ptr++ = '-';
				ptr = Hex32ToUpperChars(ptr, (uint) (lo >> 32));
				if (separator) *ptr++ = '-';
				ptr = Hex32ToUpperChars(ptr, (uint) lo);
			}
			else
			{
				ptr = Hex32ToLowerChars(ptr, hi);
				if (separator) *ptr++ = '-';
				ptr = Hex32ToLowerChars(ptr, (uint) (lo >> 32));
				if (separator) *ptr++ = '-';
				ptr = Hex32ToLowerChars(ptr, (uint) lo);
			}
			if (quotes) *ptr++ = '}';

			Contract.Debug.Ensures(ptr == buffer + size);
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

		private static bool TryDecode16Unsafe(ReadOnlySpan<char> chars, bool separator, out Uuid96 result)
		{
			// aaaaaaaabbbbbbbbcccccccc
			// aaaaaaaa-bbbbbbbb-cccccccc
			if ((!separator || (chars[8] == '-' && chars[17] == '-'))
			 && TryCharsToHex32(chars, out uint hi)
			 && TryCharsToHex32(chars.Slice(separator ? 9 : 8), out uint med)
			 && TryCharsToHex32(chars.Slice(separator ? 18 : 16), out uint lo))
			{
				result = new Uuid96(hi, med, lo);
				return true;
			}
			result = default(Uuid96);
			return false;
		}

		#endregion

		#region Unsafe I/O...

		internal static void ReadUnsafe(ReadOnlySpan<byte> source, out Uuid96 result)
		{
			//Paranoid.Requires(source.Length >= SizeOf);
			unsafe
			{
				fixed (byte* ptr = &MemoryMarshal.GetReference(source))
				{
					result = new Uuid96(UnsafeHelpers.LoadUInt32BE(ptr), UnsafeHelpers.LoadUInt64BE(ptr + 4));
				}
			}
		}

		internal static void WriteUnsafe(uint hi, ulong lo, Span<byte> destination)
		{
			//Paranoid.Requires(destination.Length >= SizeOf);
			unsafe
			{
				fixed (byte* ptr = &MemoryMarshal.GetReference(destination))
				{
					UnsafeHelpers.StoreUInt32BE(ptr, hi);
					UnsafeHelpers.StoreUInt64BE(ptr + 4, lo);
				}
			}
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		internal unsafe void WriteToUnsafe(Span<byte> destination)
		{
			WriteUnsafe(this.Hi, this.Lo, destination);
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

		public static bool operator ==(Uuid96 left, Uuid96 right)
		{
			return left.Hi == right.Hi & left.Lo == right.Lo;
		}

		public static bool operator !=(Uuid96 left, Uuid96 right)
		{
			return left.Hi != right.Hi | left.Lo != right.Lo;
		}

		public static bool operator >(Uuid96 left, Uuid96 right)
		{
			return left.Hi > right.Hi || (left.Hi == right.Hi && left.Lo > right.Lo);
		}

		public static bool operator >=(Uuid96 left, Uuid96 right)
		{
			return left.Hi > right.Hi || (left.Hi == right.Hi && left.Lo >= right.Lo);
		}

		public static bool operator <(Uuid96 left, Uuid96 right)
		{
			return left.Hi < right.Hi || (left.Hi == right.Hi && left.Lo < right.Lo);
		}

		public static bool operator <=(Uuid96 left, Uuid96 right)
		{
			return left.Hi < right.Hi || (left.Hi == right.Hi && left.Lo <= right.Lo);
		}

		/// <summary>Add a value from this instance</summary>
		public static Uuid96 operator +(Uuid96 left, long right)
		{
			//TODO: how to handle overflow ? negative values ?
			unchecked
			{
				uint hi = left.Hi;
				ulong lo = left.Lo + (ulong) right;
				if (lo < left.Lo) // overflow!
				{
					++hi;
				}
				return new Uuid96(hi, lo);
			}
		}

		/// <summary>Add a value from this instance</summary>
		public static Uuid96 operator +(Uuid96 left, ulong right)
		{
			//TODO: how to handle overflow ?
			unchecked
			{
				uint hi = left.Hi;
				ulong lo = left.Lo + right;
				if (lo < left.Lo) // overflow!
				{
					++hi;
				}
				return new Uuid96(hi, lo);
			}
		}

		/// <summary>Subtract a value from this instance</summary>
		public static Uuid96 operator -(Uuid96 left, long right)
		{
			//TODO: how to handle overflow ? negative values ?
			unchecked
			{
				uint hi = left.Hi;
				ulong lo = left.Lo - (ulong) right;
				if (lo > left.Lo) // overflow!
				{
					--hi;
				}
				return new Uuid96(hi, lo);
			}
		}

		/// <summary>Subtract a value from this instance</summary>
		public static Uuid96 operator -(Uuid96 left, ulong right)
		{
			//TODO: how to handle overflow ?
			unchecked
			{
				uint hi = left.Hi;
				ulong lo = left.Lo - right;
				if (lo > left.Lo) // overflow!
				{
					--hi;
				}
				return new Uuid96(hi, lo);
			}
		}

		/// <summary>Increments the value of this instance</summary>
		public static Uuid96 operator ++(Uuid96 value)
		{
			return value + 1;
		}

		/// <summary>Decrements the value of this instance</summary>
		public static Uuid96 operator --(Uuid96 value)
		{
			return value - 1;
		}

		#endregion

		/// <summary>Instance of this times can be used to test Uuid96 for equality and ordering</summary>
		public sealed class Comparer : IEqualityComparer<Uuid96>, IComparer<Uuid96>
		{

			public static readonly Comparer Default = new Comparer();

			private Comparer()
			{ }

			public bool Equals(Uuid96 x, Uuid96 y)
			{
				return x.Hi == y.Hi & x.Lo == y.Lo;
			}

			public int GetHashCode(Uuid96 obj)
			{
				return obj.GetHashCode();
			}

			public int Compare(Uuid96 x, Uuid96 y)
			{
				return x.CompareTo(y);
			}
		}

		/// <summary>Generates 96-bit UUIDs using a secure random number generator</summary>
		/// <remarks>Methods of this type are thread-safe.</remarks>
		[PublicAPI]
		public sealed class RandomGenerator
		{

			/// <summary>Default instance of a random generator</summary>
			/// <remarks>Using this instance will introduce a global lock in your application. You can create specific instances for worker threads, if you require concurrency.</remarks>
			public static readonly Uuid96.RandomGenerator Default = new Uuid96.RandomGenerator();

			private RandomNumberGenerator Rng { get; }

			private readonly byte[] Scratch = new byte[SizeOf];

			/// <summary>Create a new instance of a random UUID generator</summary>
			public RandomGenerator()
				: this(null)
			{ }

			/// <summary>Create a new instance of a random UUID generator, using a specific random number generator</summary>
			public RandomGenerator(RandomNumberGenerator? generator)
			{
				this.Rng = generator ?? RandomNumberGenerator.Create();
			}

			/// <summary>Return a new random 96-bit UUID</summary>
			/// <returns>Uuid96 that contains 96 bits worth of randomness.</returns>
			/// <remarks>
			/// <p>This methods needs to acquire a lock. If multiple threads needs to generate ids concurrently, you may need to create an instance of this class for each threads.</p>
			/// <p>The uniqueness of the generated uuids depends on the quality of the random number generator. If you cannot tolerate collisions, you either have to check if a newly generated uid already exists, or use a different kind of generator.</p>
			/// </remarks>
			[Pure]
			// ReSharper disable once MemberHidesStaticFromOuterClass
			public Uuid96 NewUuid()
			{
				//REVIEW: OPTIMIZE: use a per-thread instance of the rng and scratch buffer?
				// => right now, NewUuid() is a Global Lock for the whole process!
				lock (this.Rng)
				{
					// get 10 bytes of randomness (0 allowed)
					this.Rng.GetBytes(this.Scratch);
					//note: do *NOT* call GetBytes(byte[], int, int) because it creates creates a temp buffer, calls GetBytes(byte[]) and copy the result back! (as of .NET 4.7.1)
					//TODO: PERF: use Span<byte> APIs once (if?) they become available!
					return Uuid96.Read(this.Scratch);
				}
			}

		}

	}

}

#endif
