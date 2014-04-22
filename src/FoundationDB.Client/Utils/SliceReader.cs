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
	using System;

	/// <summary>Helper class that holds the internal state used to parse tuples from slices</summary>
	public struct SliceReader
	{

		/// <summary>Creates a reader on a byte array</summary>
		public static SliceReader FromBuffer(byte[] buffer)
		{
			return new SliceReader(Slice.Create(buffer));
		}

		/// <summary>Creates a reader on a segment of a byte array</summary>
		public static SliceReader FromBuffer(byte[] buffer, int offset, int count)
		{
			return new SliceReader(Slice.Create(buffer, offset, count));
		}

		/// <summary>Creates a reader on a segment of a byte array</summary>
		public static SliceReader FromBuffer(byte[] buffer, uint offset, uint count)
		{
			return new SliceReader(Slice.Create(buffer, (int)offset, (int)count));
		}

		/// <summary>Buffer containing the tuple being parsed</summary>
		public readonly Slice Buffer;

		/// <summary>Current position inside the buffer</summary>
		public int Position;

		public SliceReader(Slice buffer)
		{
			this.Buffer = buffer;
			this.Position = 0;
		}

		/// <summary>Returns true if there are more bytes to parse</summary>
		public bool HasMore { get { return this.Position < this.Buffer.Count; } }

		/// <summary>Returns the number of bytes remaining</summary>
		public int Remaining { get { return Math.Max(0, this.Buffer.Count - this.Position); } }

		/// <summary>Returns a slice with all the bytes read so far in the buffer</summary>
		public Slice Head
		{
			get { return this.Buffer.Substring(0, this.Position); }
		}

		/// <summary>Returns a slice with all the remaining bytes in the buffer</summary>
		public Slice Tail
		{
			get { return this.Buffer.Substring(this.Position); }
		}

		/// <summary>Ensure that there are at least <paramref name="count"/> bytes remaining in the buffer</summary>
		public void EnsureBytes(int count)
		{
			if (count < 0 || checked(this.Position + count) > this.Buffer.Count) throw new ArgumentOutOfRangeException("count");
		}

		/// <summary>Ensure that there are at least <paramref name="count"/> bytes remaining in the buffer</summary>
		public void EnsureBytes(uint count)
		{
			EnsureBytes(checked((int)count));
		}

		/// <summary>Return the value of the next byte in the buffer, or -1 if we reached the end</summary>
		public int PeekByte()
		{
			int p = this.Position;
			return p < this.Buffer.Count ? this.Buffer[p] : -1;
		}

		/// <summary>Skip the next <paramref name="count"/> bytes of the buffer</summary>
		public void Skip(int count)
		{
			EnsureBytes(count);

			this.Position += count;
		}

		public void Skip(uint count)
		{
			Skip(checked((int)count));
		}

		/// <summary>Read the next byte from the buffer</summary>
		public byte ReadByte()
		{
			EnsureBytes(1);

			int p = this.Position;
			byte b = this.Buffer[p];
			this.Position = p + 1;
			return b;
		}

		/// <summary>Read the next <paramref name="count"/> bytes from the buffer</summary>
		public Slice ReadBytes(int count)
		{
			EnsureBytes(count);

			int p = this.Position;
			this.Position = p + count;
			return this.Buffer.Substring(p, count);
		}

		public Slice ReadBytes(uint count)
		{
			return ReadBytes(checked((int)count));
		}

		/// <summary>Read the next 2 bytes as an unsigned 16-bit integer, encoded in little-endian</summary>
		public ushort ReadFixed16()
		{
			return ReadBytes(2).ToUInt16();
		}

		/// <summary>Read the next 4 bytes as an unsigned 32-bit integer, encoded in little-endian</summary>
		public uint ReadFixed32()
		{
			return ReadBytes(4).ToUInt32();
		}

		/// <summary>Read the next 8 bytes as an unsigned 64-bit integer, encoded in little-endian</summary>
		public ulong ReadFixed64()
		{
			return ReadBytes(8).ToUInt64();
		}

		/// <summary>Read an encoded nul-terminated byte array from the buffer</summary>
		public Slice ReadByteString()
		{
			var buffer = this.Buffer.Array;
			int start = this.Buffer.Offset + this.Position;
			int p = start;
			int end = this.Buffer.Offset + this.Buffer.Count;

			while (p < end)
			{
				byte b = buffer[p++];
				if (b == 0)
				{
					//TODO: decode \0\xFF ?
					if (p < end && buffer[p] == 0xFF)
					{
						// skip the next byte and continue
						p++;
						continue;
					}

					this.Position = p - this.Buffer.Offset;
					return new Slice(buffer, start, p - start);
				}
			}

			throw new FormatException("Truncated byte string (expected terminal NUL not found)");
		}

		/// <summary>Reads a 7-bit encoded unsigned int (aka 'Varint16') from the buffer, and advances the cursor</summary>
		/// <remarks>Can Read up to 3 bytes from the input</remarks>
		public ushort ReadVarint16()
		{
			//note: this could read up to 21 bits of data, so we check for overflow
			return checked((ushort)ReadVarint(3));
		}

		/// <summary>Reads a 7-bit encoded unsigned int (aka 'Varint32') from the buffer, and advances the cursor</summary>
		/// <remarks>Can Read up to 5 bytes from the input</remarks>
		public uint ReadVarint32()
		{
			//note: this could read up to 35 bits of data, so we check for overflow
			return checked((uint)ReadVarint(5));
		}

		/// <summary>Reads a 7-bit encoded unsigned long (aka 'Varint32') from the buffer, and advances the cursor</summary>
		/// <remarks>Can Read up to 10 bytes from the input</remarks>
		public ulong ReadVarint64()
		{
			return ReadVarint(10);
		}

		/// <summary>Reads a Base 128 Varint from the input</summary>
		/// <param name="count">Maximum number of bytes allowed (5 for 32 bits, 10 for 64 bits)</param>
		private ulong ReadVarint(int count)
		{
			var buffer = this.Buffer.Array;
			int p = this.Buffer.Offset + this.Position;
			int end = this.Buffer.Offset + this.Buffer.Count;

			ulong x = 0;
			int s = 0;

			// read bytes until the MSB is unset
			while (count-- > 0)
			{
				if (p > end) throw new FormatException("Truncated Varint");
				byte b = buffer[p++];

				x |= (b & 0x7FUL) << s;
				if (b < 0x80)
				{
					this.Position = p - this.Buffer.Offset;
					return x;
				}
				s += 7;
			}
			throw new FormatException("Malformed Varint");
		}

		/// <summary>Reads a variable sized slice, by first reading its size (stored as a Varint32) and then the data</summary>
		public Slice ReadVarbytes()
		{
			uint size = ReadVarint32();
			if (size > int.MaxValue) throw new FormatException("Malformed variable size");
			if (size == 0) return Slice.Empty;
			return ReadBytes((int)size);
		}

	}

}
