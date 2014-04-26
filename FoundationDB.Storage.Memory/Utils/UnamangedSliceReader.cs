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

namespace FoundationDB.Storage.Memory.Utils
{
	using System;
	using System.Diagnostics.Contracts;



	/// <summary>Helper class that holds the internal state used to parse tuples from slices</summary>
	public unsafe class UnamangedSliceReader
	{

		/// <summary>Creates a reader on a byte array</summary>
		public static UnamangedSliceReader FromSlice(USlice slice)
		{
			return new UnamangedSliceReader(slice.Data, slice.Count);
		}

		/// <summary>Creates a reader on a segment of a byte array</summary>
		public static UnamangedSliceReader FromAddress(byte* address, ulong count)
		{
			if (address == null && count != 0) throw new ArgumentException("Address cannot be null");
			return new UnamangedSliceReader(address, count);
		}

		/// <summary>Buffer containing the tuple being parsed</summary>
		public readonly byte* Base;

		/// <summary>Current position inside the buffer</summary>
		public byte* Position;

		/// <summary>Memory address just after the end of the buffer</summary>
		public readonly byte* End;

		private UnamangedSliceReader(byte* address, ulong count)
		{
			Contract.Requires(address != null || count == 0);

			this.Base = address;
			this.Position = address;
			this.End = address + count;

			Contract.Ensures(this.End >= this.Base && this.Position >= this.Base && this.Position <= this.End);
		}

		public ulong Offset { get { return this.Position > this.Base ? (ulong)(this.Position - this.Base) : 0UL; } }

		public ulong Length { get { return (ulong)(this.End - this.Base); } }

		/// <summary>Returns true if there are more bytes to parse</summary>
		public bool HasMore { get { return this.Position < this.End; } }

		/// <summary>Returns the number of bytes remaining</summary>
		public ulong Remaining { get { return this.Position < this.End ? (ulong)(this.End - this.Position) : 0UL; } }

		/// <summary>Ensure that there are at least <paramref name="count"/> bytes remaining in the buffer</summary>
		public void EnsureBytes(uint count)
		{
			if (checked(this.Position + count) > this.End) throw new ArgumentOutOfRangeException("count");
		}

		/// <summary>Return the value of the next byte in the buffer, or -1 if we reached the end</summary>
		public int PeekByte()
		{
			byte* p = this.Position;
			return p < this.End ? (*p) : -1;
		}

		/// <summary>Skip the next <paramref name="count"/> bytes of the buffer</summary>
		public void Skip(uint count)
		{
			EnsureBytes(count);

			this.Position += count;
		}

		/// <summary>Read the next byte from the buffer</summary>
		public byte ReadByte()
		{
			EnsureBytes(1);

			byte* p = this.Position;
			byte b = *p;
			this.Position = checked(p + 1);
			return b;
		}

		/// <summary>Read the next <paramref name="count"/> bytes from the buffer</summary>
		public USlice ReadBytes(uint count)
		{
			EnsureBytes(count);

			byte* p = this.Position;
			this.Position = checked(p + count);
			return new USlice(p, count);
		}

		/// <summary>Read the next 2 bytes as an unsigned 16-bit integer, encoded in little-endian</summary>
		public ushort ReadFixed16()
		{
			EnsureBytes(2);
			byte* p = this.Position;
			this.Position = checked(p + 2);
			return (ushort)(p[0] | p[1] << 8);
		}

		/// <summary>Read the next 4 bytes as an unsigned 32-bit integer, encoded in little-endian</summary>
		public uint ReadFixed32()
		{
			EnsureBytes(4);
			byte* p = this.Position;
			this.Position = checked(p + 4);
			return p[0] | (uint)p[1] << 8 | (uint)p[2] << 16 | (uint)p[3] << 24;
		}

		/// <summary>Read the next 8 bytes as an unsigned 64-bit integer, encoded in little-endian</summary>
		public ulong ReadFixed64()
		{
			EnsureBytes(8);
			byte* p = this.Position;
			this.Position = checked(p + 8);
			return p[0] | (ulong)p[1] << 8 | (ulong)p[2] << 16 | (ulong)p[3] << 24 | (ulong)p[4] << 32 | (ulong)p[5] << 40 | (ulong)p[6] << 48 | (ulong)p[7] << 56;
		}

		/// <summary>Reads a 7-bit encoded unsigned int (aka 'Varint16') from the buffer, and advances the cursor</summary>
		/// <remarks>Can read up to 3 bytes from the input</remarks>
		public ushort ReadVarint16()
		{
			byte* p = this.Position;
			byte* end = this.End;
			uint n = 1;

			if (p >= end) goto overflow;
			uint b = p[0];
			uint res = b & 0x7F;
			if (res < 0x80) { goto done; }

			if (p >= end) goto overflow;
			b = p[1];
			res |= (b & 0x7F) << 7;
			if (b < 0x80) { n = 2; goto done; }

			// the third byte should only have 2 bits worth of data
			if (p >= end) goto overflow;
			b = p[2];
			if (b >= 0x4) throw new FormatException("Varint is bigger than 16 bits");
			res |= (b & 0x2) << 14;
			n = 3;

		done:
			this.Position = checked(p + n);
			return (ushort)res;

		overflow:
			throw new FormatException("Truncated Varint");
		}

		/// <summary>Reads a 7-bit encoded unsigned int (aka 'Varint32') from the buffer, and advances the cursor</summary>
		/// <remarks>Can read up to 5 bytes from the input</remarks>
		public uint ReadVarint32()
		{
			byte* p = this.Position;
			byte* end = this.End;
			uint n = 1;

			if (p >= end) goto overflow;
			uint b = p[0];
			uint res = b & 0x7F;
			if (res < 0x80) { goto done; }

			if (p >= end) goto overflow;
			b = p[1];
			res |= (b & 0x7F) << 7;
			if (b < 0x80) { n = 2; goto done; }

			if (p >= end) goto overflow;
			b = p[2];
			res |= (b & 0x7F) << 14;
			if (b < 0x80) { n = 3; goto done; }

			if (p >= end) goto overflow;
			b = p[3];
			res |= (b & 0x7F) << 21;
			if (b < 0x80) { n = 4; goto done; }

			// the fifth byte should only have 4 bits worth of data
			if (p >= end) goto overflow;
			b = p[4];
			if (b >= 0x20) throw new FormatException("Varint is bigger than 32 bits");
			res |= (b & 0x1F) << 28;
			n = 5;

		done:
			this.Position = checked(p + n);
			return res;

		overflow:
			throw new FormatException("Truncated Varint");
		}

		/// <summary>Reads a 7-bit encoded unsigned long (aka 'Varint32') from the buffer, and advances the cursor</summary>
		/// <remarks>Can read up to 10 bytes from the input</remarks>
		public ulong ReadVarint64()
		{
			byte* p = this.Position;
			byte* end = this.End;
			uint n = 1;

			if (p >= end) goto overflow;
			uint b = p[0];
			ulong res = b & 0x7F;
			if (res < 0x80) { goto done; }

			if (p >= end) goto overflow;
			b = p[1];
			res |= (b & 0x7F) << 7;
			if (b < 0x80) { n = 2; goto done; }

			if (p >= end) goto overflow;
			b = p[2];
			res |= (b & 0x7F) << 14;
			if (b < 0x80) { n = 3; goto done; }

			if (p >= end) goto overflow;
			b = p[3];
			res |= (b & 0x7F) << 21;
			if (b < 0x80) { n = 4; goto done; }

			if (p >= end) goto overflow;
			b = p[4];
			res |= (b & 0x7F) << 28;
			if (b < 0x80) { n = 5; goto done; }

			if (p >= end) goto overflow;
			b = p[5];
			res |= (b & 0x7F) << 35;
			if (b < 0x80) { n = 6; goto done; }

			if (p >= end) goto overflow;
			b = p[6];
			res |= (b & 0x7F) << 42;
			if (b < 0x80) { n = 7; goto done; }

			if (p >= end) goto overflow;
			b = p[7];
			res |= (b & 0x7F) << 49;
			if (b < 0x80) { n = 8; goto done; }

			if (p >= end) goto overflow;
			b = p[8];
			res |= (b & 0x7F) << 56;
			if (b < 0x80) { n = 9; goto done; }

			// the tenth byte should only have 1 bit worth of data
			if (p >= end) goto overflow;
			b = p[4];
			if (b > 1) throw new FormatException("Varint is bigger than 64 bits");
			res |= (b & 0x1) << 63;
			n = 10;

		done:
			this.Position = checked(p + n);
			return res;

		overflow:
			throw new FormatException("Truncated Varint");
		}

		/// <summary>Reads a variable sized slice, by first reading its size (stored as a Varint32) and then the data</summary>
		public USlice ReadVarbytes()
		{
			uint size = ReadVarint32();
			if (size > uint.MaxValue) throw new FormatException("Malformed variable size");
			if (size == 0) return USlice.Nil;
			return ReadBytes(size);
		}

	}

}
