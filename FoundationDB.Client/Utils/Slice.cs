#region BSD Licence
/* Copyright (c) 2013, Doxense SARL
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

namespace FoundationDB.Client
{
	using FoundationDB.Async;
	using FoundationDB.Client.Utils;
	using System;
	using System.ComponentModel;
	using System.IO;
	using System.Runtime.InteropServices;
	using System.Text;
	using System.Threading;
	using System.Threading.Tasks;

	/// <summary>Delimits a section of a byte array</summary>
	[ImmutableObject(true)]
	public struct Slice : IEquatable<Slice>, IEquatable<ArraySegment<byte>>, IEquatable<byte[]>, IComparable<Slice>
	{
		#region Static Members...

		internal static readonly byte[] EmptyArray = new byte[0];

		/// <summary>Null slice ("no segment")</summary>
		public static readonly Slice Nil = default(Slice);

		/// <summary>Empty slice ("segment of 0 bytes")</summary>
		public static readonly Slice Empty = new Slice(EmptyArray, 0, 0);

		private static readonly byte[] ByteSprite;

		static Slice()
		{
			var tmp = new byte[256];
			for (int i = 0; i < tmp.Length; i++) tmp[i] = (byte)i;
			ByteSprite = tmp;
		}

		#endregion

		/// <summary>Pointer to the buffer (or null for Slice.Nil)</summary>
		public readonly byte[] Array;

		/// <summary>Offset of the first byte of the slice in the parent buffer</summary>
		public readonly int Offset;

		/// <summary>Number of bytes in the slice</summary>
		public readonly int Count;

		internal Slice(byte[] array)
		{
			Contract.Requires(array != null);

			this.Array = array;
			this.Offset = 0;
			this.Count = array != null ? array.Length : 0;
		}

		internal Slice(byte[] array, int offset, int count)
		{
			Contract.Requires(array != null);
			Contract.Requires(offset >= 0 && offset <= array.Length);
			Contract.Requires(count >= 0 && offset + count <= array.Length);

			this.Array = array;
			this.Offset = offset;
			this.Count = count;
		}

		/// <summary>Creates a slice mapping an entire buffer</summary>
		/// <param name="bytes"></param>
		/// <returns></returns>
		public static Slice Create(byte[] bytes)
		{
			return 
				bytes == null ? Slice.Nil :
				bytes.Length == 0 ? Slice.Empty :
				new Slice(bytes, 0, bytes.Length);
		}

		/// <summary>Creates a slice from an Array Segment</summary>
		/// <param name="arraySegment">Segment of buffer to convert</param>
		public static Slice Create(ArraySegment<byte> arraySegment)
		{
			return Create(arraySegment.Array, arraySegment.Offset, arraySegment.Count);
		}

		/// <summary>Creates a slice mapping a section of a buffer</summary>
		/// <param name="buffer">Original buffer</param>
		/// <param name="offset">Offset into buffer</param>
		/// <param name="count">Number of bytes</param>
		public static Slice Create(byte[] buffer, int offset, int count)
		{
			if (buffer == null) return Nil;
			if (count == 0) return Empty;
			if (offset < 0 || offset >= buffer.Length) throw new ArgumentException("offset");
			if (count < 0 || offset + count > buffer.Length) throw new ArgumentException("count");
			return new Slice(buffer, offset, count);
		}

		/// <summary>Create a new empty slice of a specified size containing all zeroes</summary>
		/// <param name="size"></param>
		/// <returns></returns>
		public static Slice Create(int size)
		{
			if (size < 0) throw new ArgumentException("size");
			return size == 0 ? Slice.Empty : new Slice(new byte[size], 0, size);
		}

		/// <summary>Creates a new slice with a copy of an unmanaged memory buffer</summary>
		/// <param name="ptr">Pointer to unmanaged buffer</param>
		/// <param name="count">Number of bytes in the buffer</param>
		/// <returns>Slice with a managed copy of the data</returns>
		internal static unsafe Slice Create(byte* ptr, int count)
		{
			if (ptr == null) return Slice.Nil;
			if (count <= 0) return Slice.Empty;

			if (count == 1)
			{
				return Slice.FromByte(*ptr);
			}

			var bytes = new byte[count];
			Marshal.Copy(new IntPtr(ptr), bytes, 0, count);
			return new Slice(bytes, 0, count);
		}

		/// <summary>Create a new slice fille with random bytes taken from a random number generator</summary>
		/// <param name="rnd">Random generator to use (needs locking if instance is shared)</param>
		/// <param name="size">Number of random bytes to generate</param>
		/// <returns>Slice of <paramref name="size"/> bytes takent from <paramref name="rnd"/></returns>
		/// <remarks>If the <paramref name="rnd"/> instance is shared, then it needs to be locked before calling this method.</remarks>
		public static Slice Random(Random rnd, int size)
		{
			if (rnd == null) throw new ArgumentNullException("rnd");
			if (size <= 0) return Slice.Empty;

			var bytes = new byte[size];
			rnd.NextBytes(bytes);
			return new Slice(bytes, 0, size);
		}

		/// <summary>Decode a Base64 encoded string into a slice</summary>
		public static Slice FromBase64(string base64String)
		{
			return base64String == null ? Slice.Nil : base64String.Length == 0 ? Slice.Empty : Slice.Create(Convert.FromBase64String(base64String));
		}

		public static Slice FromByte(byte value)
		{
			return new Slice(ByteSprite, value, 1);
		}

		/// <summary>Encode a signed 32-bit integer into a variable size slice (1, 2 or 4 bytes) in little-endian</summary>
		public static Slice FromInt32(int value)
		{
			if (value >= 0)
			{
				if (value <= 255)
				{
					return Slice.FromByte((byte)value);
				}
				if (value <= 65535)
				{
					return new Slice(new byte[] { (byte)value, (byte)(value >> 8) }, 0, 2);
				}
			}

			return FromFixed32(value);
		}

		/// <summary>Encode a signed 32-bit integer into a 4-byte slice in little-endian</summary>
		public static Slice FromFixed32(int value)
		{
			return new Slice(
				new byte[]
				{ 
					(byte)value,
					(byte)(value >> 8),
					(byte)(value >> 16),
					(byte)(value >> 24)
				},
				0,
				4
			);
		}

		/// <summary>Encode a signed 64-bit integer into a variable size slice (1, 2, 4 or 8 bytes) in little-endian</summary>
		public static Slice FromInt64(long value)
		{
			if (value >= 0 && value <= int.MaxValue)
			{
				return FromInt32((int)value);
			}
			return FromFixed64(value);
		}

		/// <summary>Encode a signed 64-bit integer into a 8-byte slice in little-endian</summary>
		public static Slice FromFixed64(long value)
		{
			return new Slice(
				new byte[]
				{ 
					(byte)value,
					(byte)(value >> 8),
					(byte)(value >> 16),
					(byte)(value >> 24),
					(byte)(value >> 32),
					(byte)(value >> 40),
					(byte)(value >> 48),
					(byte)(value >> 56)
				}, 
				0, 
				8
			);
		}

		/// <summary>Encode an unsigned 64-bit integer into a variable size slice (1, 2, 4 or 8 bytes) in little-endian</summary>
		public static Slice FromUInt64(ulong value)
		{
			if (value <= 255)
			{
				//TODO: use a cache for these ?
				return Slice.FromByte((byte)value);
			}
			if (value <= 65535)
			{
				return new Slice(new byte[] { (byte)value, (byte)(value >> 8) }, 0, 2);
			}

			if (value <= uint.MaxValue)
			{
				return new Slice(
					new byte[]
					{ 
						(byte)value,
						(byte)(value >> 8),
						(byte)(value >> 16),
						(byte)(value >> 24)
					},
					0,
					4
				);
			}

			return FromFixedU64(value);
		}

		/// <summary>Encode an unsigned 64-bit integer into a 8-byte slice in little-endian</summary>
		public static Slice FromFixedU64(ulong value)
		{
			return new Slice(
				new byte[]
				{ 
					(byte)(value >> 56),
					(byte)(value >> 48),
					(byte)(value >> 40),
					(byte)(value >> 32),
					(byte)(value >> 24),
					(byte)(value >> 16),
					(byte)(value >> 8),
					(byte)value
				},
				0,
				8
			);
		}

		public static Slice FromGuid(Guid value)
		{
			// UUID are stored using the RFC4122 format (Big Endian), while .NET's System.GUID use Little Endian
			// => we will convert the GUID into a UUID under the hood, and hope that it gets converted back when read from the db

			return new Uuid(value).ToSlice();
		}

		/// <summary>Create a 16-byte slice containing an RFC 4122 compliant 128-bit UUID</summary>
		/// <param name="value"></param>
		/// <returns></returns>
		public static Slice FromUuid(Uuid value)
		{
			// UUID should already be in the RFC 4122 ordering
			return value.ToSlice();
		}

		/// <summary>Dangerously create a slice containing string converted to ASCII. All non-ASCII characters may be corrupted or converted to '?'</summary>
		/// <remarks>WARNING: if you put a string that contains non-ASCII chars, it will be silently corrupted! This should only be used to store keywords or 'safe' strings.
		/// Note: depending on your default codepage, chars from 128 to 255 may be preserved, but only if they are decoded using the same codepage at the other end !</remarks>
		public static Slice FromAscii(string text)
		{
			return text == null ? Slice.Nil : text.Length == 0 ? Slice.Empty : Slice.Create(Encoding.Default.GetBytes(text));
		}

		/// <summary>Create a slice containing the UTF-8 bytes of the string <paramref name="value"/></summary>
		public static Slice FromString(string value)
		{
			return value == null ? Slice.Nil : value.Length == 0 ? Slice.Empty : Slice.Create(Encoding.UTF8.GetBytes(value));
		}

		public static Slice FromChar(char value)
		{
			if (value < 128)
			{ // ASCII
				return Slice.FromByte((byte)value);
			}

			// note: Encoding.UTF8.GetMaxByteCount(1) returns 6, but allocate 8 to stay aligned
			var tmp = new byte[8];
			int n = Encoding.UTF8.GetBytes(new char[] { value }, 0, 1, tmp, 0);
			return new Slice(tmp, 0, n);
		}

		private static int NibbleToDecimal(char c)
		{
			int x = c - 48;
			if (x < 10) return x;
			if (x >= 17 && x <= 42) return x - 7;
			if (x >= 49 && x <= 74) return x - 39;
			throw new FormatException("Input is not valid hexadecimal");
		}

		public static Slice FromHexa(string hexaString)
		{
			if (string.IsNullOrEmpty(hexaString)) return hexaString == null ? Slice.Nil : Slice.Empty;

			if ((hexaString.Length & 1) != 0) throw new ArgumentException("Hexadecimal string must be of even length", "hexaString");

			var buffer = new byte[hexaString.Length >> 1];
			for (int i = 0; i < hexaString.Length; i += 2)
			{
				buffer[i >> 1] = (byte) ((NibbleToDecimal(hexaString[i]) << 4) | NibbleToDecimal(hexaString[i + 1]));
			}
			return new Slice(buffer, 0, buffer.Length);
		}

		/// <summary>Returns true is the slice is not null</summary>
		/// <remarks>An empty slice is NOT considered null</remarks>
		public bool HasValue { get { return this.Array != null; } }

		/// <summary>Return true if the slice is not null but contains 0 bytes</summary>
		/// <remarks>A null slice is NOT empty</remarks>
		public bool IsEmpty { get { return this.Count == 0 && this.Array != null; } }

		/// <summary>Returns true if the slice does not contain at least 1 byte</summary>
		public bool IsNullOrEmpty { get { return this.Count == 0; } }

		/// <summary>Return a byte array containing all the bytes of the slice, or null if the slice is null</summary>
		/// <returns>Byte array with a copy of the slice, or null</returns>
		public byte[] GetBytes()
		{
			if (this.IsNullOrEmpty) return this.Array == null ? null : Slice.EmptyArray;
			var bytes = new byte[this.Count];
			Buffer.BlockCopy(this.Array, this.Offset, bytes, 0, bytes.Length);
			return bytes;
		}

		/// <summary>Return a byte array containing a subset of the bytes of the slice, or null if the slice is null</summary>
		/// <returns>Byte array with a copy of a subset of the slice, or null</returns>
		public byte[] GetBytes(int offset, int count)
		{
			//TODO: throw if this.Array == null ? (what does "Slice.Nil.GetBytes(..., 0)" mean ?)

			if (offset < 0) throw new ArgumentOutOfRangeException("offset");
			if (count < 0) throw new ArgumentOutOfRangeException("count");
			if (offset + count > this.Count) throw new ArgumentOutOfRangeException("count");

			if (count == 0) return this.Array == null ? null : Slice.EmptyArray;

			var bytes = new byte[count];
			Buffer.BlockCopy(this.Array, this.Offset + offset, bytes, 0, count);
			return bytes;
		}

		public SliceStream AsStream()
		{
			return new SliceStream(this);
		}

		/// <summary>Stringify a slice containing only ASCII chars</summary>
		/// <returns>ASCII string, or null if the slice is null</returns>
		public string ToAscii()
		{
			if (this.IsNullOrEmpty) return this.HasValue ? String.Empty : default(string);

			return Encoding.Default.GetString(this.Array, this.Offset, this.Count);
		}

		/// <summary>Stringify a slice containing only ASCII chars</summary>
		/// <returns>ASCII string, or null if the slice is null</returns>
		public string ToAscii(int offset, int count)
		{
			//TODO: check args
			if (count == 0) return String.Empty;

			return Encoding.Default.GetString(this.Array, this.Offset + offset, count);
		}

		/// <summary>Stringify a slice containing an UTF-8 encoded string</summary>
		/// <returns>Unicode string, or null if the slice is null</returns>
		public string ToUnicode()
		{
			if (this.IsNullOrEmpty) return this.HasValue ? String.Empty : default(string);

			return Encoding.UTF8.GetString(this.Array, this.Offset, this.Count);
		}

		/// <summary>Stringify a slice containing an UTF-8 encoded string</summary>
		/// <returns>Unicode string, or null if the slice is null</returns>
		public string ToUnicode(int offset, int count)
		{
			//TODO: check args
			if (count == 0) return String.Empty;

			return Encoding.UTF8.GetString(this.Array, this.Offset + offset, count);
		}

		private static ArraySegment<byte> UnescapeByteString(byte[] buffer, int offset, int count)
		{
			// check for nulls
			int p = offset;
			int end = offset + count;

			while (p < end)
			{
				if (buffer[p] == 0)
				{ // found a 0, switch to slow path
					return UnescapeByteStringSlow(buffer, offset, count, p - offset);
				}
				++p;
			}
			// buffer is clean, we can return it as-is
			return new ArraySegment<byte>(buffer, offset, count);
		}

		private static ArraySegment<byte> UnescapeByteStringSlow(byte[] buffer, int offset, int count, int offsetOfFirstZero = 0)
		{
			var tmp = new byte[count];

			int p = offset;
			int end = offset + count;
			int i = 0;

			if (offsetOfFirstZero > 0)
			{
				Buffer.BlockCopy(buffer, offset, tmp, 0, offsetOfFirstZero);
				p += offsetOfFirstZero;
			}

			while (p < end)
			{
				byte b = buffer[p++];
				if (b == 0)
				{ // skip next FF
					//TODO: check that next byte really is 0xFF
					++p;
				}
				tmp[i++] = b;
			}

			return new ArraySegment<byte>(tmp, 0, p - offset);
		}

		/// <summary>Converts a slice using Base64 encoding</summary>
		public string ToBase64()
		{
			if (this.IsNullOrEmpty) return this.Array == null ? null : String.Empty;
			return Convert.ToBase64String(this.Array, this.Offset, this.Count);
		}

		/// <summary>Converts a slice into a string with each byte encoded into hexadecimal (lowercase)</summary>
		/// <returns>"0123456789abcdef"</returns>
		public string ToHexaString()
		{
			if (this.IsNullOrEmpty) return this.Array == null ? null : String.Empty;
			var buffer = this.Array;
			int p = this.Offset;
			int n = this.Count;
			var sb = new StringBuilder(n * 2);
			while (n-- > 0)
			{
				byte b = buffer[p++];
				int x = b >> 4;
				sb.Append((char)(x + (x < 10 ? 48 : 87)));
				x = b & 0xF;
				sb.Append((char)(x + (x < 10 ? 48 : 87)));
			}
			return sb.ToString();
		}

		/// <summary>Converts a slice into a string with each byte encoded into hexadecimal (uppercase) separated by a char</summary>
		/// <returns>"0123456789abcdef"</returns>
		public string ToHexaString(char sep)
		{
			if (this.IsNullOrEmpty) return this.Array == null ? null : String.Empty;
			var buffer = this.Array;
			int p = this.Offset;
			int n = this.Count;
			var sb = new StringBuilder(n * 3);
			while (n-- > 0)
			{
				if (sb.Length > 0) sb.Append(sep);
				byte b = buffer[p++];
				int x = b >> 4;
				sb.Append((char)(x + (x < 10 ? 48 : 55)));
				x = b & 0xF;
				sb.Append((char)(x + (x < 10 ? 48 : 55)));
			}
			return sb.ToString();
		}

		/// <summary>Helper method that dumps the slice as a string (if it contains only printable ascii chars) or an hex array if it contains non printable chars. It should only be used for logging and troubleshooting !</summary>
		/// <returns>Returns either "'abc'" or "&lt;00 42 7F&gt;". Returns "''" for Slice.Empty, and "" for Slice.Nil</returns>
		public string ToAsciiOrHexaString()
		{
			//REVIEW: rename this to ToFriendlyString() ? or ToLoggableString() ?
			if (this.Count == 0) return this.HasValue ? "''" : String.Empty;

			var buffer = this.Array;
			int n = this.Count;
			int p = this.Offset;
			var chars = new char[n + 2];
			int i = 1;
			while(n-- > 0)
			{
				byte b = buffer[p++];
				if (b < 32 || b > 127) return "<" + ToHexaString(' ') + ">";
				chars[i++] = (char)b;
			}
			chars[0] = '\'';
			chars[chars.Length - 1] = '\'';
			return new string(chars, 0, chars.Length);
		}

		public byte ToByte()
		{
			if (this.Count == 0) return 0;
			if (this.Count > 1) throw new FormatException("Cannot convert slice into a Byte because it is larger than 1 byte");
			return this.Array[this.Offset];
		}

		public int ToInt32()
		{
			if (this.IsNullOrEmpty) return 0;

			if (this.Count > 4) throw new FormatException("Cannot convert slice into an Int32 because it is larger than 4 bytes");

			var buffer = this.Array;
			int n = this.Count;
			int p = this.Offset + n - 1;
			int value = 0;

			while (n-- > 0)
			{
				value = (value << 8) | buffer[p--];
			}
			return value;
		}

		public uint ToUInt32()
		{
			if (this.IsNullOrEmpty) return 0;

			if (this.Count > 4) throw new FormatException("Cannot convert slice into an UInt32 because it is larger than 4 bytes");

			var buffer = this.Array;
			int n = this.Count;
			int p = this.Offset + n - 1;
			uint value = 0;

			while (n-- > 0)
			{
				value = (value << 8) | buffer[p--];
			}
			return value;
		}

		public long ToInt64()
		{
			if (this.IsNullOrEmpty) return 0L;

			if (this.Count > 8) throw new FormatException("Cannot convert slice into an Int64 because it is larger than 8 bytes");

			var buffer = this.Array;
			int n = this.Count;
			int p = this.Offset + n - 1;
			long value = 0;

			while (n-- > 0)
			{
				value = (value << 8) | buffer[p--];
			}

			return value;
		}

		public ulong ToUInt64()
		{
			if (this.IsNullOrEmpty) return 0L;

			if (this.Count > 8) throw new FormatException("Cannot convert slice into an UInt64 because it is larger than 8 bytes");

			var buffer = this.Array;
			int n = this.Count;
			int p = this.Offset + n - 1;
			ulong value = 0;

			while (n-- > 0)
			{
				value = (value << 8) | buffer[p--];
			}

			return value;
		}

		public ulong ReadUInt64(int offset, int bytes)
		{
			ulong value = 0;
			var buffer = this.Array;
			int p = UnsafeMapToOffset(offset);
			if (bytes > 0)
			{
				value = buffer[p++];
				--bytes;

				while (bytes-- > 0)
				{
					value <<= 8;
					value |= buffer[p++];
				}
			}
			return value;
		}

		public Guid ToGuid()
		{
			if (this.IsNullOrEmpty) return default(Guid);

			if (this.Count == 16)
			{ // direct byte array

				// UUID are stored using the RFC4122 format (Big Endian), while .NET's System.GUID use Little Endian
				// we need to swap the byte order of the Data1, Data2 and Data3 chunks, to ensure that Guid.ToString() will return the proper value.

				return new Uuid(this).ToGuid();
			}

			if (this.Count == 44)
			{ // string representation (ex: "da846709-616d-4e82-bf55-d1d3e9cde9b1")
				return Guid.Parse(this.ToAscii());
			}

			throw new FormatException("Cannot convert slice into a Guid because it has an incorrect size");
		}

		public Uuid ToUuid()
		{
			if (this.IsNullOrEmpty) return default(Uuid);

			if (this.Count == 16)
			{
				return new Uuid(this);
			}

			if (this.Count == 44)
			{
				return Uuid.Parse(this.ToAscii());
			}

			throw new FormatException("Cannot convert slice into Uuid because it has an incorrect size");
		}

		/// <summary>Returns a new slice that contains an isolated copy of the buffer</summary>
		/// <returns>Slice that is equivalent, but is isolated from any changes to the buffer</returns>
		internal Slice Memoize()
		{
			if (this.IsNullOrEmpty) return this.Array == null ? Slice.Nil : Slice.Empty;
			return new Slice(GetBytes(), 0, this.Count);
		}

		/// <summary>Map an offset in the slice into the absolute offset in the buffer, without any bound checking</summary>
		/// <param name="index">Relative offset (negative values mean from the end)</param>
		/// <returns>Absolute offset in the buffer</returns>
		private int UnsafeMapToOffset(int index)
		{
			int p = NormalizeIndex(index);
			Contract.Requires(p >= 0 & p < this.Count);
			return this.Offset + p;
		}

		/// <summary>Map an offset in the slice into the absolute offset in the buffer</summary>
		/// <param name="index">Relative offset (negative values mean from the end)</param>
		/// <returns>Absolute offset in the buffer</returns>
		/// <exception cref="IndexOutOfRangeException">If the index is outside the slice</exception>
		private int MapToOffset(int index)
		{
			int p = NormalizeIndex(index);
			if (p < 0 || p >= this.Count) FailIndexOutOfBound(index);
			checked { return this.Offset + p; }
		}

		/// <summary>Normalize negative index values into offset from the start</summary>
		/// <param name="index">Relative offset (negative values mean from the end)</param>
		/// <returns>Relative offset from the start of the slice</returns>
		private int NormalizeIndex(int index)
		{
			checked { return index < 0 ? index + this.Count : index; }
		}

		/// <summary>Returns the value of one byte in the slice</summary>
		/// <param name="index">Offset of the byte (negative values means start from the end)</param>
		public byte this[int index]
		{
			get { return this.Array[MapToOffset(index)]; }
		}

		/// <summary>Returns a substring of the current slice that fits withing the specified index range</summary>
		/// <param name="start">The starting position of the substring. Positive values means from the start, negative values means from the end</param>
		/// <param name="end">The end position (exlucded) of the substring. Positive values means from the start, negative values means from the end</param>
		/// <returns>Subslice</returns>
		public Slice this[int start, int end]
		{
			get
			{
				start = NormalizeIndex(start);
				end = NormalizeIndex(end);

				// bound check
				if (start < 0) start = 0;
				if (end > this.Count) end = this.Count;

				if (start >= end) return Slice.Empty;
				if (start == 0 && end == this.Count) return this;

				checked { return new Slice(this.Array, this.Offset + start, end - start); }
			}
		}

		private static void FailIndexOutOfBound(int index)
		{
			throw new IndexOutOfRangeException("Index is outside the slice");
		}

		internal byte GetByte(int index)
		{
			return this.Array[UnsafeMapToOffset(index)];
		}

		/// <summary>Copy this slice into another buffer</summary>
		/// <param name="buffer">Buffer where to copy this slice</param>
		/// <param name="offset">Offset into the destination buffer</param>
		public void Append(byte[] buffer, ref int cursor)
		{
			if (buffer == null) throw new ArgumentNullException("buffer");
			if (cursor < 0) throw new ArgumentOutOfRangeException("offset");

			if (this.Count > 0)
			{
				Buffer.BlockCopy(this.Array, this.Offset, buffer, cursor, this.Count);
			}
			cursor += this.Count;
		}

		/// <summary>Copy this slice into another buffer</summary>
		/// <param name="buffer">Buffer where to copy this slice</param>
		/// <param name="offset">Offset into the destination buffer</param>
		public void CopyTo(byte[] buffer, int offset)
		{
			if (buffer == null) throw new ArgumentNullException("buffer");
			if (offset < 0) throw new ArgumentOutOfRangeException("offset");

			if (this.Count > 0)
			{
				Buffer.BlockCopy(this.Array, this.Offset, buffer, offset, this.Count);
			}
		}

		/// <summary>Retrieves a substring from this instance. The substring starts at a specified character position.</summary>
		/// <param name="offset">The starting position of the substring. Positive values mmeans from the start, negative values means from the end</param>
		/// <returns>A slice that is equivalent to the substring that begins at <paramref name="offset"/> (from the start or the end depending on the sign) in this instance, or Slice.Empty if <paramref name="offset"/> is equal to the length of the slice.</returns>
		/// <remarks>The substring does not copy the original data, and refers to the same buffer as the original slice. Any change to the parent slice's buffer will be seen by the substring. You must call Memoize() on the resulting substring if you want a copy</remarks>
		/// <example>{"ABCDE"}.Substring(0) => {"ABC"}
		/// {"ABCDE"}.Substring(1} => {"BCDE"}
		/// {"ABCDE"}.Substring(-2} => {"DE"}
		/// {"ABCDE"}.Substring(5} => Slice.Empty
		/// Slice.Empty.Substring(0) => Slice.Empty
		/// Slice.Nil.Substring(0) => Slice.Emtpy
		/// </example>
		/// <exception cref="System.ArgumentOutOfRangeException"><paramref name="offset"/> indicates a position not within this instance, or <paramref name="offset"/> is less than zero</exception>
		public Slice Substring(int offset)
		{
			// negative values means from the end
			if (offset < 0) offset = this.Count + offset;

			if (offset < 0) throw new ArgumentOutOfRangeException("offset", "Offset cannot be less then start of the slice");
			if (offset > this.Count) throw new ArgumentOutOfRangeException("offset", "Offset cannot be larger than end of slice");

			return offset == 0 ? this : this.Count == offset ? Slice.Empty : new Slice(this.Array, this.Offset + offset, this.Count - offset);
		}

		/// <summary>Retrieves a substring from this instance. The substring starts at a specified character position and has a specified length.</summary>
		/// <param name="offset">The starting position of the substring. Positive values means from the start, negative values means from the end</param>
		/// <param name="count">Number of bytes in the substring</param>
		/// <returns>A slice that is equivalent to the substring of length <paramref name="count"/> that begins at <paramref name="offset"/> (from the start or the end depending on the sign) in this instance, or Slice.Empty if count is zero.</returns>
		/// <remarks>The substring does not copy the original data, and refers to the same buffer as the original slice. Any change to the parent slice's buffer will be seen by the substring. You must call Memoize() on the resulting substring if you want a copy</remarks>
		/// <example>{"ABCDE"}.Substring(0, 3) => {"ABC"}
		/// {"ABCDE"}.Substring(1, 3} => {"BCD"}
		/// {"ABCDE"}.Substring(-2, 2} => {"DE"}
		/// Slice.Empty.Substring(0, 0) => Slice.Empty
		/// Slice.Nil.Substring(0, 0) => Slice.Emtpy
		/// </example>
		/// <exception cref="System.ArgumentOutOfRangeException"><paramref name="offset"/> plus <paramref name="count"/> indicates a position not within this instance, or <paramref name="offset"/> or <paramref name="count"/> is less than zero</exception>
		public Slice Substring(int offset, int count)
		{
			// negative values means from the end
			if (offset < 0) offset = this.Count + offset;

			if (offset < 0) throw new ArgumentOutOfRangeException("offset", "Offset cannot be less then start of the slice");
			if (offset > this.Count) throw new ArgumentOutOfRangeException("offset", "Offset cannot be larger than end of slice");
			if (count < 0) throw new ArgumentOutOfRangeException("count", "Count must be a positive integer");
			if (offset > this.Count - count) throw new ArgumentOutOfRangeException("count", "Offset and count must refer to a location within the slice");

			if (count == 0) return Slice.Empty;

			return new Slice(this.Array, this.Offset + offset, count);
		}

		/// <summary>Determines whether the beginning of this slice instance matches a specified slice.</summary>
		/// <param name="value">The slice to compare</param>
		/// <returns><b>true</b> if <paramref name="value"/> matches the beginning of this slice; otherwise, <b>false</b></returns>
		public bool StartsWith(Slice value)
		{
			if (!value.HasValue) throw new ArgumentNullException("value");

			// any strings starts with the empty string
			if (value.IsEmpty) return true;

			// prefix cannot be bigger
			if (value.Count > this.Count) return false;

			return Slice.SameBytes(this.Array, this.Offset, value.Array, value.Offset, value.Count);
		}

		/// <summary>Determines whether the end of this slice instance matches a specified slice.</summary>
		/// <param name="value">The slice to compare to the substring at the end of this instance.</param>
		/// <returns><b>true</b> if <paramref name="value"/> matches the end of this slice; otherwise, <b>false</b></returns>
		public bool EndsWith(Slice value)
		{
			if (!value.HasValue) throw new ArgumentNullException("value");

			// any strings ends with the empty string
			if (value.IsEmpty) return true;

			// suffix cannot be bigger
			if (value.Count > this.Count) return false;

			return Slice.SameBytes(this.Array, this.Offset + this.Count - value.Count, value.Array, value.Offset, value.Count);
		}

		/// <summary>Equivalent of StartsWith, but the returns false if both slices are identical</summary>
		public bool PrefixedBy(Slice parent)
		{
			// empty is a parent of everyone
			if (parent.IsNullOrEmpty) return true;
			// empty is not a child of anything
			if (this.IsNullOrEmpty) return false;

			// we must have at least one more byte then the parent
			if (this.Count <= parent.Count) return false;

			// must start with the same bytes
			return SameBytes(parent.Array, parent.Offset, this.Array, this.Offset, parent.Count);
		}

		/// <summary>Equivalent of EndsWith, but the returns false if both slices are identical</summary>
		public bool SuffixedBy(Slice parent)
		{
			// empty is a parent of everyone
			if (parent.IsNullOrEmpty) return true;
			// empty is not a child of anything
			if (this.IsNullOrEmpty) return false;

			// we must have at least one more byte then the parent
			if (this.Count <= parent.Count) return false;

			// must start with the same bytes
			return SameBytes(parent.Array, parent.Offset + this.Count - parent.Count, this.Array, this.Offset, parent.Count);
		}

		/// <summary>Append/Merge a slice at the end of the current slice</summary>
		/// <param name="tail">Slice that must be appended</param>
		/// <returns>Merged slice if both slices are contigous, or a new slice containg the content of the current slice, followed by the tail slice</returns>
		public Slice Concat(Slice tail)
		{
			if (tail.IsNullOrEmpty) return this;
			if (this.IsNullOrEmpty) return tail;

			// special case: adjacent segments ?
			if (this.Array == tail.Array && this.Offset + this.Count == tail.Offset)
			{
				return new Slice(this.Array, this.Offset, this.Count + tail.Count);
			}

			byte[] tmp = new byte[this.Count + tail.Count];
			this.CopyTo(tmp, 0);
			tail.CopyTo(tmp, this.Count);
			return new Slice(tmp, 0, tmp.Length);
		}

		/// <summary>Implicitly converts a Slice into an ArraySegment&lt;byte&gt;</summary>
		public static implicit operator ArraySegment<byte>(Slice value)
		{
			if (!value.HasValue)
				return default(ArraySegment<byte>);
			return new ArraySegment<byte>(value.Array, value.Offset, value.Count);
		}

		/// <summary>Implicitly converts an ArraySegment&lt;byte&gt; into a Slice</summary>
		public static implicit operator Slice(ArraySegment<byte> value)
		{
			return new Slice(value.Array, value.Offset, value.Count);
		}

		/// <summary>Compare two slices for equality</summary>
		/// <returns>True if the slice contains the same bytes</returns>
		public static bool operator ==(Slice a, Slice b)
		{
			return a.Equals(b);
		}

		/// <summary>Compare two slices for inequality</summary>
		/// <returns>True if the slice do not contain the same bytes</returns>
		public static bool operator !=(Slice a, Slice b)
		{
			return !a.Equals(b);
		}

		public static bool operator <(Slice a, Slice b)
		{
			return a.CompareTo(b) < 0;
		}

		public static bool operator <=(Slice a, Slice b)
		{
			return a.CompareTo(b) <= 0;
		}

		public static bool operator >(Slice a, Slice b)
		{
			return a.CompareTo(b) > 0;
		}

		public static bool operator >=(Slice a, Slice b)
		{
			return a.CompareTo(b) >= 0;
		}

		/// <summary>Append/Merge two slices together</summary>
		/// <param name="a">First slice</param>
		/// <param name="b">Second slice</param>
		/// <returns>Merged slices if both slices are contigous, or a new slice containg the content of the first slice, followed by the second</returns>
		public static Slice operator +(Slice a, Slice b)
		{
			return a.Concat(b);
		}

		/// <summary>Remove <paramref name="n"/> bytes at the end of slice <param name="s"/></summary>
		/// <returns>Smaller slice</returns>
		public static Slice operator -(Slice s, int n)
		{
			if (n < 0) throw new ArgumentOutOfRangeException("count", "Cannot subtract a negative number from a slice");
			if (n > s.Count) throw new ArgumentOutOfRangeException("count", "Cannout substract more bytes than the slice contains");

			if (n == 0) return s;
			if (n == s.Count) return Slice.Empty;

			return new Slice(s.Array, s.Offset, s.Count - n);
		}

		public override string ToString()
		{
			//TODO: add a protection against keys that are too big ?
			return Slice.Dump(this);
		}

		public static string Dump(Slice value)
		{
			if (value.IsNullOrEmpty) return value.HasValue ? "<empty>" : "<null>";

			var buffer = value.Array;
			int n = value.Count;
			int p = value.Offset;
			var sb = new StringBuilder(n + 16);
			while (n-- > 0)
			{
				int c = buffer[p++];
				if (c < 32 || c >= 127 || c == 60) sb.Append('<').Append(c.ToString("X2")).Append('>'); else sb.Append((char)c);
			}
			return sb.ToString();
		}

		public static Slice Unescape(string value)
		{
			var writer = new FdbBufferWriter();
			for (int i = 0; i < value.Length; i++)
			{
				char c = value[i];
				if (c == '<')
				{
					if (value[i + 3] != '>') throw new FormatException("Invalid escape slice string");
					c = (char)(NibbleToDecimal(value[i + 1]) << 4 | NibbleToDecimal(value[i + 2]));
					i += 3;
				}
				writer.WriteByte((byte)c);
			}
			return writer.ToSlice();
		}


		#region Streams...

		/// <summary>Read the content of a stream into a slice</summary>
		/// <param name="data">Source stream, that must be in a readable state</param>
		/// <returns>Slice containing the stream content (or Slice.Nil if the stream is Stream.Nul)</returns>
		public static Slice FromStream(Stream data)
		{
			if (data == null) throw new ArgumentNullException("data");

			// special case for empty values
			if (data == Stream.Null) return Slice.Nil;
			if (data.Length == 0) return Slice.Empty;

			if (!data.CanRead) throw new ArgumentException("Cannot read from provided stream", "data");
			if (data.Length > int.MaxValue) throw new InvalidOperationException("Streams of more than 2GB are not supported");
			//TODO: other checks?

			int length;
			checked { length = (int)data.Length; }

			if (data is MemoryStream || data is UnmanagedMemoryStream) // other types of already completed streams ?
			{ // read synchronously
				return LoadFromNonBlockingStream(data, length);
			}

			// read asynchronoulsy
			return LoadFromBlockingStream(data, length);
		}

		/// <summary>Asynchronously read the content of a stream into a slice</summary>
		/// <param name="data">Source stream, that must be in a readable state</param>
		/// <returns>Slice containing the stream content (or Slice.Nil if the stream is Stream.Nul)</returns>
		public static Task<Slice> FromStreamAsync(Stream data, CancellationToken ct = default(CancellationToken))
		{
			if (data == null) throw new ArgumentNullException("data");
			// special case for empty values
			if (data == Stream.Null) return Task.FromResult(Slice.Nil);
			if (data.Length == 0) return Task.FromResult(Slice.Empty);

			if (!data.CanRead) throw new ArgumentException("Cannot read from provided stream", "data");
			if (data.Length > int.MaxValue) throw new InvalidOperationException("Streams of more than 2GB are not supported");
			//TODO: other checks?

			if (ct.IsCancellationRequested) return TaskHelpers.FromCancellation<Slice>(ct);

			int length;
			checked { length = (int)data.Length; }

			if (data is MemoryStream || data is UnmanagedMemoryStream) // other types of already completed streams ?
			{ // read synchronously
				return Task.FromResult(LoadFromNonBlockingStream(data, length));
			}

			// read asynchronoulsy
			return LoadFromBlockingStreamAsync(data, length, 0, ct);
		}

		/// <summary>Read from a non-blocking stream that already contains all the data in memory (MemoryStream, UnmanagedStream, ...)</summary>
		/// <param name="source">Source stream</param>
		/// <param name="length">Number of bytes to read from the stream</param>
		/// <returns>Slice containing the loaded data</returns>
		private static Slice LoadFromNonBlockingStream(Stream source, int length)
		{
			Contract.Requires(source != null && source.CanRead && source.Length <= int.MaxValue);

			var ms = source as MemoryStream;
			if (ms != null)
			{ // Already holds onto a byte[]

				//note: should be use GetBuffer() ? It can throws and is dangerous (could mutate)
				return Slice.Create(ms.ToArray());
			}

			// read it in bulk, without buffering

			var buffer = new byte[length]; //TODO: round up to avoid fragmentation ?

			// note: reading should usually complete with only one big read, but loop until completed, just to be sure
			int p = 0;
			int r = length;
			while (r > 0)
			{
				int n = source.Read(buffer, p, r);
				if (n <= 0) throw new InvalidOperationException(String.Format("Unexpected end of stream at {0} / {1} bytes", p, length));
				p += n;
				r -= n;
			}
			Contract.Assert(r == 0 && p == length);

			return Slice.Create(buffer);
		}

		/// <summary>Synchronously read from a blocking stream (FileStream, NetworkStream, ...)</summary>
		/// <param name="source">Source stream</param>
		/// <param name="length">Number of bytes to read from the stream</param>
		/// <param name="chunkSize">If non zero, max amount of bytes to read in one chunk. If zero, tries to read everything at once</param>
		/// <returns>Slice containing the loaded data</returns>
		private static Slice LoadFromBlockingStream(Stream source, int length, int chunkSize = 0)
		{
			Contract.Requires(source != null && source.CanRead && source.Length <= int.MaxValue && chunkSize >= 0);

			if (chunkSize == 0) chunkSize = int.MaxValue;

			var buffer = new byte[length]; //TODO: round up to avoid fragmentation ?

			// note: reading should usually complete with only one big read, but loop until completed, just to be sure
			int p = 0;
			int r = length;
			while (r > 0)
			{
				int c = Math.Max(r, chunkSize);
				int n = source.Read(buffer, p, c);
				if (n <= 0) throw new InvalidOperationException(String.Format("Unexpected end of stream at {0} / {1} bytes", p, length));
				p += n;
				r -= n;
			}
			Contract.Assert(r == 0 && p == length);

			return Slice.Create(buffer);
		}

		/// <summary>Asynchronously read from a blocking stream (FileStream, NetworkStream, ...)</summary>
		/// <param name="source">Source stream</param>
		/// <param name="length">Number of bytes to read from the stream</param>
		/// <param name="chunkSize">If non zero, max amount of bytes to read in one chunk. If zero, tries to read everything at once</param>
		/// <returns>Slice containing the loaded data</returns>
		private static async Task<Slice> LoadFromBlockingStreamAsync(Stream source, int length, int chunkSize, CancellationToken ct)
		{
			Contract.Requires(source != null && source.CanRead && source.Length <= int.MaxValue && chunkSize >= 0);

			if (chunkSize == 0) chunkSize = int.MaxValue;

			var buffer = new byte[length]; //TODO: round up to avoid fragmentation ?

			// note: reading should usually complete with only one big read, but loop until completed, just to be sure
			int p = 0;
			int r = length;
			while (r > 0)
			{
				int c = Math.Max(r, chunkSize);
				int n = await source.ReadAsync(buffer, p, c, ct);
				if (n <= 0) throw new InvalidOperationException(String.Format("Unexpected end of stream at {0} / {1} bytes", p, length));
				p += n;
				r -= n;
			}
			Contract.Assert(r == 0 && p == length);

			return Slice.Create(buffer);
		}

		#endregion

		#region Equality, Comparison...

		public override bool Equals(object obj)
		{
			if (obj == null) return this.Array == null;
			if (obj is Slice) return Equals((Slice)obj);
			if (obj is ArraySegment<byte>) return Equals((ArraySegment<byte>)obj);
			if (obj is byte[]) return Equals((byte[])obj);
			return false;
		}

		public override int GetHashCode()
		{
			if (this.Array != null)
			{
				//TODO: use a better hash algorithm? (xxHash, CityHash, SipHash, ...?)
				//we don't need a cryptographic hash, just something fast and suitable for use with hashtables...

				// <HACKHACK>: unoptimized 32 bits FNV-1a implementation
				uint h = 2166136261; // FNV1 32 bits offset basis
				var bytes = this.Array;
				int p = this.Offset;
				int count = this.Count;
				while(count-- > 0)
				{
					h = (h ^ bytes[p++]) * 16777619; // FNV1 32 prime
				}
				return (int)h;
				// </HACKHACK>
			}
			return 0;
		}

		public bool Equals(Slice other)
		{
			return this.Count == other.Count && SameBytes(this.Array, this.Offset, other.Array, other.Offset, this.Count);
		}

		/// <summary>Lexicographically compare this slice with another one</summary>
		/// <param name="other">Other slice to compare</param>
		/// <returns>0 for equal, positive if we are greater, negative if we are smaller</returns>
		/// <remarks>Nil/Empty is equal to itself, and smaller than everything else</remarks>
		public int CompareTo(Slice other)
		{
			if (this.IsNullOrEmpty) return other.IsNullOrEmpty ? 0 : -1;
			return other.IsNullOrEmpty ? +1 : CompareBytes(this.Array, this.Offset, this.Count, other.Array, other.Offset, other.Count);
		}

		public bool Equals(ArraySegment<byte> other)
		{
			return this.Count == other.Count && SameBytes(this.Array, this.Offset, other.Array, other.Offset, this.Count);
		}

		public bool Equals(byte[] other)
		{
			if (other == null) return this.Array == null;
			return this.Count == other.Length && SameBytes(this.Array, this.Offset, other, 0, this.Count);
		}

		/// <summary>Compare two byte segments for equalit</summary>
		/// <param name="left">Left buffer</param>
		/// <param name="leftOffset">Start offset in left buffer</param>
		/// <param name="right">Right buffer</param>
		/// <param name="rightOffset">Start offset in right buffer</param>
		/// <param name="count">Number of bytes to compare</param>
		/// <returns>true if all bytes are the same in both segments</returns>
		internal static bool SameBytes(byte[] left, int leftOffset, byte[] right, int rightOffset, int count)
		{
			Contract.Requires(leftOffset >= 0);
			Contract.Requires(rightOffset >= 0);
			Contract.Requires(count >= 0);

			if (left == null) return object.ReferenceEquals(right, null);
			if (object.ReferenceEquals(left, right)) return leftOffset == rightOffset;

			//TODO: ensure that there are enough bytes on both sides

			while (count-- > 0)
			{
				if (left[leftOffset++] != right[rightOffset++]) return false;
			}
			return true;
		}

		/// <summary>Compare two byte segments lexicographically</summary>
		/// <param name="left">Left buffer</param>
		/// <param name="leftOffset">Start offset in left buffer</param>
		/// <param name="leftCount">Number of bytes in left buffer</param>
		/// <param name="right">Right buffer</param>
		/// <param name="rightOffset">Start offset in right buffer</param>
		/// <param name="rightCount">Number of bytes in right buffer</param>
		/// <returns>Returns zero if segments are identical (same bytes), a negative value if left is lexicographically less than right, or a positive value if left is lexicographically greater than right</returns>
		/// <remarks>The comparison algorithm respect the following:
		/// * "A" &lt; "B"
		/// * "A" &lt; "AA"
		/// * "AA" &lt; "B"</remarks>
		internal static int CompareBytes(byte[] left, int leftOffset, int leftCount, byte[] right, int rightOffset, int rightCount)
		{
			Contract.Requires(leftCount >= 0);
			Contract.Requires(leftOffset >= 0);
			Contract.Requires(rightCount >= 0);
			Contract.Requires(rightOffset >= 0);

			if (leftCount == rightCount && leftOffset == rightOffset && object.ReferenceEquals(left, right))
			{ // same segment
				return 0;
			}

			// Compare the common prefix
			int n = Math.Min(leftCount, rightCount);
			while (n-- > 0)
			{
				int d = left[leftOffset++] - right[rightOffset++];
				if (d != 0) return d;
			}

			// Same prefix, compare the lengths
			return leftCount - rightCount;
		}

		#endregion

	}

}
