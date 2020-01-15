#region BSD License
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

namespace Doxense.Memory
{
	using System;
	using System.Diagnostics;
	using System.Runtime.CompilerServices;
	using System.Text;
	using Doxense.Diagnostics.Contracts;
	using JetBrains.Annotations;

	/// <summary>Helper class that holds the internal state used to parse tuples from slices</summary>
	/// <remarks>This struct MUST be passed by reference!</remarks>
	[PublicAPI, DebuggerDisplay("{Position}/{Buffer.Count}, NextByte={PeekByte()}")]
	[DebuggerNonUserCode] //remove this when you need to troubleshoot this class!
	public struct SliceReader
	{

		/// <summary>Buffer containing the tuple being parsed</summary>
		public readonly Slice Buffer;

		/// <summary>Current position inside the buffer</summary>
		public int Position;

		/// <summary>Creates a new reader over a slice</summary>
		/// <param name="buffer">Slice that will be used as the underlying buffer</param>
		public SliceReader(Slice buffer)
		{
			buffer.EnsureSliceIsValid();
			this.Buffer = buffer;
			this.Position = 0;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public SliceReader(Slice buffer, int offset)
		{
			buffer.EnsureSliceIsValid();
			this.Buffer = buffer.Substring(offset);
			this.Position = 0;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public SliceReader(byte[] buffer)
		{
			this.Buffer = new Slice(buffer, 0, buffer.Length);
			this.Position = 0;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public SliceReader(byte[] buffer, int offset, int count)
		{
			this.Buffer = new Slice(buffer, offset, count);
			this.Position = 0;
		}

		/// <summary>Returns true if there are more bytes to parse</summary>
		public bool HasMore => this.Position < this.Buffer.Count;

		/// <summary>Returns the number of bytes remaining</summary>
		public int Remaining => Math.Max(0, this.Buffer.Count - this.Position);

		/// <summary>Returns a slice with all the bytes read so far in the buffer</summary>
		public Slice Head => this.Buffer.Substring(0, this.Position);

		/// <summary>Returns a slice with all the remaining bytes in the buffer</summary>
		public Slice Tail => this.Buffer.Substring(this.Position);

		/// <summary>Ensure that there are at least <paramref name="count"/> bytes remaining in the buffer</summary>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		[DebuggerNonUserCode]
		public void EnsureBytes(int count)
		{
			if (count < 0 || checked(this.Position + count) > this.Buffer.Count) throw ThrowNotEnoughBytes(count);
		}

		[Pure, NotNull, MethodImpl(MethodImplOptions.NoInlining)]
		[DebuggerNonUserCode]
		private static Exception ThrowNotEnoughBytes(int count)
		{
			return ThrowHelper.FormatException($"The buffer does not have enough data to satisfy a read of {count} byte(s)");
		}

		/// <summary>Return the value of the next byte in the buffer, or -1 if we reached the end</summary>
		[Pure]
		public int PeekByte()
		{
			int p = this.Position;
			return p < this.Buffer.Count ? this.Buffer[p] : -1;
		}

		/// <summary>Return the value of the byte at a specified offset from the current position, or -1 if this is after the end, or before the start</summary>
		[Pure]
		public int PeekByteAt(int offset)
		{
			int p = this.Position + offset;
			return p < this.Buffer.Count && p >= 0 ? this.Buffer[p] : -1;
		}

		public Slice PeekBytes(int count)
		{
			return this.Buffer.Substring(this.Position, count);
		}

		/// <summary>Attempt to peek at the next <paramref name="count"/> bytes from the reader, without advancing the pointer</summary>
		/// <param name="count">Number of bytes to peek</param>
		/// <param name="bytes">Receives the corresponding slice if there are enough bytes remaining.</param>
		/// <returns>If <c>true</c>, the next <paramref name="count"/> are available in <paramref name="bytes"/>. If <c>false</c>, there are not enough bytes remaining in the buffer.</returns>
		public bool TryPeekBytes(int count, out Slice bytes)
		{
			if (this.Remaining < count)
			{
				bytes = default(Slice);
				return false;
			}
			bytes = this.Buffer.Substring(this.Position, count);
			return true;
		}

		/// <summary>Skip the next <paramref name="count"/> bytes of the buffer</summary>
		public void Skip(int count)
		{
			EnsureBytes(count);

			this.Position += count;
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
			if (count == 0) return Slice.Empty;

			EnsureBytes(count);
			int p = this.Position;
			this.Position = p + count;
			return this.Buffer.Substring(p, count);
		}

		/// <summary>Read the next <paramref name="count"/> bytes from the buffer</summary>
		public Slice ReadBytes(uint count)
		{
			int n = checked((int) count);
			EnsureBytes(n);

			int p = this.Position;
			this.Position = p + n;
			return this.Buffer.Substring(p, n);
		}

		/// <summary>Read until <paramref name="handler"/> returns true, or we reach the end of the buffer</summary>
		[Pure]
		public Slice ReadWhile(Func<byte, int, bool> handler)
		{
			unsafe
			{
				int start = this.Position;
				int count = 0;
				fixed (byte* bytes = &this.Buffer.DangerousGetPinnableReference())
				{
					byte* ptr = bytes;
					byte* end = bytes + this.Remaining;
					while (ptr < end)
					{
						if (!handler(*ptr, count))
						{
							break;
						}
						++ptr;
						++count;
					}
					this.Position = start + count;
					return this.Buffer.Substring(start, count);
				}
			}
		}

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public Slice ReadToEnd()
		{
			return ReadBytes(this.Remaining);
		}

		/// <summary>Read the next 2 bytes as an unsigned 16-bit integer, encoded in little-endian</summary>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public ushort ReadFixed16()
		{
			return ReadBytes(2).ToUInt16();
		}

		/// <summary>Read the next 3 bytes as an unsigned 24-bit integer, encoded in little-endian</summary>
		/// <remarks>Bits 24 to 31 will always be zero</remarks>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public uint ReadFixed24()
		{
			return ReadBytes(3).ToUInt24();
		}


		/// <summary>Read the next 4 bytes as an unsigned 32-bit integer, encoded in little-endian</summary>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public uint ReadFixed32()
		{
			return ReadBytes(4).ToUInt32();
		}

		/// <summary>Read the next 8 bytes as an unsigned 64-bit integer, encoded in little-endian</summary>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public ulong ReadFixed64()
		{
			return ReadBytes(8).ToUInt64();
		}

		/// <summary>Read the next 2 bytes as an unsigned 16-bit integer, encoded in big-endian</summary>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public ushort ReadFixed16BE()
		{
			return ReadBytes(2).ToUInt16BE();
		}

		/// <summary>Read the next 3 bytes as an unsigned 24-bit integer, encoded in big-endian</summary>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public uint ReadFixed24BE()
		{
			return ReadBytes(3).ToUInt24BE();
		}

		/// <summary>Read the next 4 bytes as an unsigned 32-bit integer, encoded in big-endian</summary>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public uint ReadFixed32BE()
		{
			return ReadBytes(4).ToUInt32BE();
		}

		/// <summary>Read the next 8 bytes as an unsigned 64-bit integer, encoded in big-endian</summary>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public ulong ReadFixed64BE()
		{
			return ReadBytes(8).ToUInt64BE();
		}

		/// <summary>Read the next 4 bytes as an IEEE 32-bit floating point number</summary>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public float ReadSingle()
		{
			return ReadBytes(4).ToSingle();
		}

		/// <summary>Read the next 8 bytes as an IEEE 64-bit floating point number</summary>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public double ReadDouble()
		{
			return ReadBytes(8).ToDouble();
		}

		/// <summary>Read an encoded nul-terminated byte array from the buffer</summary>
		[Pure]
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

			throw ThrowHelper.FormatException("Truncated byte string (expected terminal NUL not found)");
		}

		/// <summary>Reads a 7-bit encoded unsigned int (aka 'Varint16') from the buffer, and advances the cursor</summary>
		/// <remarks>Can Read up to 3 bytes from the input</remarks>
		[Pure]
		public ushort ReadVarInt16()
		{
			//note: this could read up to 21 bits of data, so we check for overflow
			return checked((ushort)ReadVarInt(3));
		}

		/// <summary>Reads a 7-bit encoded unsigned int (aka 'Varint32') from the buffer, and advances the cursor</summary>
		/// <remarks>Can Read up to 5 bytes from the input</remarks>
		[Pure]
		public uint ReadVarInt32()
		{
			//note: this could read up to 35 bits of data, so we check for overflow
			return checked((uint)ReadVarInt(5));
		}

		/// <summary>Reads a 7-bit encoded unsigned long (aka 'Varint32') from the buffer, and advances the cursor</summary>
		/// <remarks>Can Read up to 10 bytes from the input</remarks>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public ulong ReadVarInt64()
		{
			return ReadVarInt(10);
		}

		/// <summary>Reads a Base 128 Varint from the input</summary>
		/// <param name="count">Maximum number of bytes allowed (5 for 32 bits, 10 for 64 bits)</param>
		private ulong ReadVarInt(int count)
		{
			var buffer = this.Buffer.Array;
			int p = this.Buffer.Offset + this.Position;
			int end = this.Buffer.Offset + this.Buffer.Count;

			ulong x = 0;
			int s = 0;

			// read bytes until the MSB is unset
			while (count-- > 0)
			{
				if (p > end) throw ThrowHelper.FormatException("Truncated Varint");
				byte b = buffer[p++];

				x |= (b & 0x7FUL) << s;
				if (b < 0x80)
				{
					this.Position = p - this.Buffer.Offset;
					return x;
				}
				s += 7;
			}
			throw ThrowHelper.FormatException("Malformed Varint");
		}

		/// <summary>Reads a variable sized slice, by first reading its size (stored as a Varint32) and then the data</summary>
		[Pure]
		public Slice ReadVarBytes()
		{
			uint size = ReadVarInt32();
			if (size > int.MaxValue) throw ThrowHelper.FormatException("Malformed variable-sized array");
			if (size == 0) return Slice.Empty;
			return ReadBytes((int)size);
		}

		/// <summary>Reads an utf-8 encoded string prefixed by a variable-sized length</summary>
		[Pure, NotNull]
		public string ReadVarString()
		{
			var str = ReadVarBytes();
			return str.ToStringUtf8();
		}

		/// <summary>Reads a string prefixed by a variable-sized length, using the specified encoding</summary>
		/// <remarks>Encoding used for this string (or UTF-8 if null)</remarks>
		[Pure, NotNull]
		public string ReadVarString([CanBeNull] Encoding encoding)
		{
			if (encoding == null || encoding.Equals(Encoding.UTF8))
			{ // optimized path for utf-8
				return ReadVarString();
			}
			// generic decoding
			var bytes = ReadVarBytes();
			return bytes.Count > 0 ? encoding.GetString(bytes.Array, bytes.Offset, bytes.Count) : string.Empty;
		}

		/// <summary>Reads a 128-bit UUID</summary>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public Uuid128 ReadUuid128()
		{
			return ReadBytes(16).ToUuid128();
		}

		/// <summary>Reads a 64-bit UUID</summary>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public Uuid64 ReadUuid64()
		{
			return ReadBytes(8).ToUuid64();
		}
	}

}

#endif
