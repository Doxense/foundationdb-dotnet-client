#region Copyright (c) 2023 SnowBank SAS, (c) 2005-2023 Doxense SAS
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
	using System;
	using System.Buffers.Binary;
	using System.Collections.Generic;
	using System.ComponentModel;
	using System.Diagnostics;
	using System.Runtime.CompilerServices;
	using System.Runtime.InteropServices;
	using Doxense.Diagnostics.Contracts;
	using Doxense.Memory;
	using JetBrains.Annotations;

	/// <summary>Represents an RFC 4122 compliant 128-bit UUID</summary>
	/// <remarks>You should use this type if you are primarily exchanging UUIDs with non-.NET platforms, that use the RFC 4122 byte ordering (big endian). The type System.Guid uses the Microsoft encoding (little endian) and is not compatible.</remarks>
	[DebuggerDisplay("[{ToString(),nq}]")]
	[ImmutableObject(true), StructLayout(LayoutKind.Explicit), Serializable]
	public readonly struct Uuid128 : IFormattable, IComparable, IEquatable<Uuid128>, IComparable<Uuid128>, IEquatable<Guid>
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
		public Uuid128(Guid guid)
			: this()
		{
			m_packed = guid;
		}

		public Uuid128(string value)
			: this(new Guid(value))
		{ }

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public Uuid128(Slice slice)
			: this()
		{
			m_packed = Convert(slice.Span);
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public Uuid128(byte[] bytes)
			: this()
		{
			m_packed = Convert(bytes.AsSpan());
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public Uuid128(ReadOnlySpan<byte> bytes)
			: this()
		{
			m_packed = Convert(bytes);
		}

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
		public Uuid128(Uuid64 a, Uuid64 b)
			: this()
		{
			m_packed = Convert(a, b);
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public Uuid128(Uuid64 a, uint b, uint c)
			: this()
		{
			m_packed = Convert(a, b, c);
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static explicit operator Guid(Uuid128 uuid)
		{
			return uuid.m_packed;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static explicit operator Uuid128(Guid guid)
		{
			return new Uuid128(guid);
		}

		public static readonly Uuid128 Empty = default;

		/// <summary>Size is 16 bytes</summary>
		public const int SizeOf = 16;

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
				a.WriteToUnsafe(buf);
				b.WriteToUnsafe(buf.Slice(8));
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

		public static Uuid128 Parse(string input)
		{
			return new Uuid128(Guid.Parse(input));
		}

		public static Uuid128 Parse(ReadOnlySpan<char> input)
		{
			return new Uuid128(Guid.Parse(input));
		}

		public static Uuid128 ParseExact(string input, string format)
		{
			return new Uuid128(Guid.ParseExact(input, format));
		}

		public static bool TryParse(string input, out Uuid128 result)
		{
			if (!Guid.TryParse(input, out Guid guid))
			{
				result = default;
				return false;
			}
			result = new Uuid128(guid);
			return true;
		}

		public static bool TryParseExact(string input, string format, out Uuid128 result)
		{
			if (!Guid.TryParseExact(input, format, out Guid guid))
			{
				result = default;
				return false;
			}
			result = new Uuid128(guid);
			return true;
		}

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

		public static unsafe void Write(in Guid value, Span<byte> buffer)
		{
			if (buffer.Length < 16) throw new ArgumentException("The destination buffer is too small", nameof(buffer));
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
		public void WriteToUnsafe(Span<byte> buffer)
		{
			Write(in m_packed, buffer);
		}

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
		{
			//TODO: optimize this ?
			return new Slice(ToByteArray());
		}

		public override string ToString()
		{
			return m_packed.ToString("D", null);
		}

		public string ToString(string? format)
		{
			return m_packed.ToString(format);
		}

		public string ToString(string? format, IFormatProvider? provider)
		{
			return m_packed.ToString(format, provider);
		}

		/// <summary>Increment the value of this UUID</summary>
		/// <param name="value">Positive value</param>
		/// <returns>Incremented UUID</returns>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public Uuid128 Increment([Positive] int value)
		{
			Contract.Debug.Requires(value >= 0);
			return Increment(checked((ulong)value));
		}

		/// <summary>Increment the value of this UUID</summary>
		/// <param name="value">Positive value</param>
		/// <returns>Incremented UUID</returns>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public Uuid128 Increment([Positive] long value)
		{
			Contract.Debug.Requires(value >= 0);
			return Increment(checked((ulong)value));
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
