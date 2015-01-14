#region BSD Licence
/* Copyright (c) 2013-2014, Doxense SAS
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
	using FoundationDB.Client.Utils;
	using JetBrains.Annotations;
	using System;
	using System.ComponentModel;
	using System.Runtime.InteropServices;

	/// <summary>RFC 4122 compliant 128-bit UUID</summary>
	/// <remarks>You should use this type if you are primarily exchanged UUIDs with non-.NET platforms, that use the RFC 4122 byte ordering (big endian). The type System.Guid uses the Microsoft encoding (little endian) and is not compatible.</remarks>
	[ImmutableObject(true), StructLayout(LayoutKind.Explicit), Serializable]
	public struct Uuid128 : IFormattable, IComparable, IEquatable<Uuid128>, IComparable<Uuid128>, IEquatable<Guid>
	{
		// This is just a wrapper struct on System.Guid that makes sure that ToByteArray() and Parse(byte[]) and new(byte[]) will parse according to RFC 4122 (http://www.ietf.org/rfc/rfc4122.txt)
		// For performance reasons, we will store the UUID as a System.GUID (Microsoft in-memory format), and swap the bytes when needed.

		// cf 4.1.2. Layeout and Byte Order

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

		public Uuid128(Guid guid)
			: this()
		{
			m_packed = guid;
		}

		public Uuid128(string value)
			: this(new Guid(value))
		{ }

		public Uuid128(Slice slice)
			: this()
		{
			m_packed = Convert(slice);
		}

		public Uuid128(byte[] bytes)
			: this(Slice.Create(bytes))
		{ }

		public Uuid128(int a, short b, short c, byte[] d)
			: this(new Guid(a, b, c, d))
		{ }

		public Uuid128(int a, short b, short c, byte d, byte e, byte f, byte g, byte h, byte i, byte j, byte k)
			: this(new Guid(a, b, c, d, e, f, g, h, i, j, k))
		{ }

		public Uuid128(uint a, ushort b, ushort c, byte d, byte e, byte f, byte g, byte h, byte i, byte j, byte k)
			: this(new Guid(a, b, c, d, e, f, g, h, i, j, k))
		{ }

		public static explicit operator Guid(Uuid128 uuid)
		{
			return uuid.m_packed;
		}

		public static explicit operator Uuid128(Guid guid)
		{
			return new Uuid128(guid);
		}

		public static readonly Uuid128 Empty = default(Uuid128);

		public static Uuid128 NewUuid()
		{
			return new Uuid128(Guid.NewGuid());
		}

		internal static Guid Convert(Slice input)
		{
			if (input.Count <= 0) return default(Guid);

			if (input.Array == null) throw new ArgumentNullException("input");
			if (input.Count == 16)
			{
				unsafe
				{
					fixed (byte* buf = input.Array)
					{
						return Read(buf + input.Offset);
					}
				}
			}

			throw new ArgumentException("Slice for UUID must be exactly 16 bytes long");
		}

		public static Uuid128 Parse([NotNull] string input)
		{
			return new Uuid128(Guid.Parse(input));
		}

		public static Uuid128 ParseExact([NotNull] string input, string format)
		{
			return new Uuid128(Guid.ParseExact(input, format));
		}

		public static bool TryParse(string input, out Uuid128 result)
		{
			Guid guid;
			if (!Guid.TryParse(input, out guid))
			{
				result = default(Uuid128);
				return false;
			}
			result = new Uuid128(guid);
			return true;
		}

		public static bool TryParseExact(string input, string format, out Uuid128 result)
		{
			Guid guid;
			if (!Guid.TryParseExact(input, format, out guid))
			{
				result = default(Uuid128);
				return false;
			}
			result = new Uuid128(guid);
			return true;
		}

		#endregion

		public long Timestamp
		{
			get
			{
				long ts = m_timeLow;
				ts |= ((long)m_timeMid) << 32;
				ts |= ((long)(m_timeHiAndVersion & 0x0FFF)) << 48;
				return ts;
			}
		}

		public int Version
		{
			get
			{
				return m_timeHiAndVersion >> 12;
			}
		}

		public int ClockSequence
		{
			get
			{
				int clk = m_clkSeqLow;
				clk |= (m_clkSeqHiRes & 0x3F) << 8;
				return clk;
			}
		}

		public long Node
		{
			get
			{
				long node;
				node = ((long)m_node0) << 40;
				node |= ((long)m_node1) << 32;
				node |= ((long)m_node2) << 24;
				node |= ((long)m_node3) << 16;
				node |= ((long)m_node4) << 8;
				node |= (long)m_node5;
				return node;
			}
		}

		#region Conversion...

		internal unsafe static Guid Read(byte* src)
		{
			Guid tmp;

			if (BitConverter.IsLittleEndian)
			{
				byte* ptr = (byte*)&tmp;

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
				*(long*)(ptr + 8) = *(long*)(src + 8);
			}
			else
			{
				long* ptr = (long*)&tmp;
				ptr[0] = *(long*)(src);
				ptr[1] = *(long*)(src + 8);
			}

			return tmp;
		}

		internal unsafe static void Write(Guid value, byte* ptr)
		{
			if (BitConverter.IsLittleEndian)
			{
				byte* src = (byte*)&value;

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
				*(long*)(ptr + 8) = *(long*)(src + 8);
			}
			else
			{
				long* src = (long*)&value;
				*(long*)(ptr) = src[0];
				*(long*)(ptr + 8) = src[1];
			}

		}

		internal unsafe void WriteTo(byte* ptr)
		{
			Write(m_packed, ptr);
		}

		[Pure]
		public Guid ToGuid()
		{
			return m_packed;
		}

		[Pure, NotNull]
		public byte[] ToByteArray()
		{
			// We must use Big Endian when serializing the UUID

			var res = new byte[16];
			unsafe
			{
				fixed (byte* ptr = res)
				{
					Write(m_packed, ptr);
				}
			}
			return res;
		}

		[Pure]
		public Slice ToSlice()
		{
			//TODO: optimize this ?
			return new Slice(ToByteArray(), 0, 16);
		}

		public override string ToString()
		{
			return m_packed.ToString("D", null);
		}

		public string ToString(string format)
		{
			return m_packed.ToString(format);
		}

		public string ToString(string format, IFormatProvider provider)
		{
			return m_packed.ToString(format, provider);
		}

		#endregion

		#region Equality / Comparison ...

		public override bool Equals(object obj)
		{
			if (obj == null) return false;
			if (obj is Uuid128) return m_packed == ((Uuid128)obj);
			if (obj is Guid) return m_packed == ((Guid)obj);
			return false;
		}

		public bool Equals(Uuid128 other)
		{
			return m_packed == other.m_packed;
		}

		public bool Equals(Guid other)
		{
			return m_packed == other;
		}

		public static bool operator ==(Uuid128 a, Uuid128 b)
		{
			return a.m_packed == b.m_packed;
		}

		public static bool operator !=(Uuid128 a, Uuid128 b)
		{
			return a.m_packed != b.m_packed;
		}

		public static bool operator ==(Uuid128 a, Guid b)
		{
			return a.m_packed == b;
		}

		public static bool operator !=(Uuid128 a, Guid b)
		{
			return a.m_packed != b;
		}

		public static bool operator ==(Guid a, Uuid128 b)
		{
			return a == b.m_packed;
		}

		public static bool operator !=(Guid a, Uuid128 b)
		{
			return a != b.m_packed;
		}

		public override int GetHashCode()
		{
			return m_packed.GetHashCode();
		}

		public int CompareTo(Uuid128 other)
		{
			return m_packed.CompareTo(other.m_packed);
		}

		public int CompareTo(object obj)
		{
			if (obj == null) return 1;

			if (obj is Uuid128)
				return m_packed.CompareTo(((Uuid128)obj).m_packed);
			else
				return m_packed.CompareTo(obj);
		}

		#endregion

	}

}
