#region Copyright Doxense 2005-2019
//
// All rights are reserved. Reproduction or transmission in whole or in part, in
// any form or by any means, electronic, mechanical or otherwise, is prohibited
// without the prior written consent of the copyright owner.
//
#endregion

namespace Doxense.Memory
{
	using System;
	using System.Buffers;
	using System.Diagnostics;
	using System.Runtime.CompilerServices;
	using System.Text;
	using Doxense.Diagnostics.Contracts;

	[DebuggerDisplay("Buffered={Position}/{Current.Length}")]
	public ref struct BufferFormatter //TODO: trouver un meilleur nom!
	{
		public IBufferWriter<byte> Writer;

		private int Position; // relative to Current!

		private Span<byte> Current;

		public BufferFormatter(IBufferWriter<byte> writer)
		{
			Contract.Debug.Requires(writer != null);
			this.Writer = writer;
			this.Position = 0;
			this.Current = default;
		}

		public void Flush()
		{
			int pos = this.Position;
			if (pos > 0)
			{
				this.Writer.Advance(pos);
				this.Current = default;
				this.Position = 0;
			}
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public Span<byte> EnsureBytes(int count)
		{
			int pos = this.Position;
			return this.Current.Length - pos >= count ? this.Current.Slice(pos, count) : EnsureBytesSlow(count);
		}

		[MethodImpl(MethodImplOptions.NoInlining)]
		private Span<byte> EnsureBytesSlow(int count)
		{
			this.Writer.Advance(this.Position);
			this.Current = this.Writer.GetSpan(count);
			this.Position = 0;
			return this.Current.Slice(0, count);
		}

		public void Advance(int count)
		{
			int pos = this.Position + count;
			if (pos > this.Current.Length) throw new InvalidOperationException("Cannot advance past buffer size");
			this.Position = pos;
		}

		public SpanWriter GetSpanWriter(int count)
		{
			return new SpanWriter(EnsureBytes(count));
		}

		public void WriteByte(byte value)
		{
			EnsureBytes(1)[0] = value;
			this.Position++;
		}

		public void WriteByte(int value)
		{
			EnsureBytes(1)[0] = (byte) value;
			this.Position++;
		}

		public void WriteByte(char value)
		{
			EnsureBytes(1)[0] = (byte) value;
			this.Position++;
		}

		public void WriteUInt16(ushort value)
		{
			var chunk = EnsureBytes(2);
			chunk[0] = (byte) value;
			chunk[1] = (byte) (value >> 8);
			this.Position += 2;
		}

		public static void WriteUInt16(in Span<byte> span, ushort value)
		{
			if (span.Length < 2) throw new ArgumentException();
			span[0] = (byte) value;
			span[1] = (byte) (value >> 8);
		}

		public void WriteUInt16BE(ushort value)
		{
			var chunk = EnsureBytes(2);
			chunk[0] = (byte) (value >> 8);
			chunk[1] = (byte) value;
			this.Position += 2;
		}

		public static void WriteUInt16BE(in Span<byte> span, ushort value)
		{
			if (span.Length < 2) throw new ArgumentException();
			span[0] = (byte)(value >> 8);
			span[1] = (byte)value;
		}

		public void WriteUInt32(uint value)
		{
			var chunk = EnsureBytes(4);
			chunk[0] = (byte) value;
			chunk[1] = (byte) (value >> 8);
			chunk[2] = (byte) (value >> 16);
			chunk[3] = (byte) (value >> 24);
			this.Position += 4;
		}

		public static void WriteUInt32(in Span<byte> span, uint value)
		{
			if (span.Length < 4) throw new ArgumentException();
			span[0] = (byte)value;
			span[1] = (byte)(value >> 8);
			span[2] = (byte)(value >> 16);
			span[3] = (byte)(value >> 24);
		}

		public void WriteUInt32BE(uint value)
		{
			var chunk = EnsureBytes(4);
			chunk[0] = (byte)(value >> 24);
			chunk[1] = (byte)(value >> 16);
			chunk[2] = (byte)(value >> 8);
			chunk[3] = (byte)value;
			this.Position += 4;
		}

		public static void WriteUInt32BE(in Span<byte> span, uint value)
		{
			if (span.Length < 4) throw new ArgumentException();
			span[0] = (byte) (value >> 24);
			span[1] = (byte) (value >> 16);
			span[2] = (byte) (value >> 8);
			span[3] = (byte) value;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void WriteBytes(in ReadOnlySpan<byte> bytes)
		{
			bytes.CopyTo(EnsureBytes(bytes.Length));
			this.Position += bytes.Length;
		}

		public void WriteUtf8Text(in ReadOnlySpan<char> text)
		{
#if USE_SPAN_API
			int byteCount = Encoding.UTF8.GetByteCount(text);
			var chunk = EnsureBytes(byteCount);
			Encoding.UTF8.GetBytes(text, chunk);
			Advance(byteCount);
#else
			unsafe
			{
				fixed (char* inp = text)
				{
					int byteCount = Encoding.UTF8.GetByteCount(inp, text.Length);
					var chunk = EnsureBytes(byteCount);
					fixed (byte* outp = chunk)
					{
						Encoding.UTF8.GetBytes(inp, text.Length, outp, chunk.Length);
					}
					Advance(byteCount);
				}
			}
#endif
		}

	}

	public ref struct SpanWriter
	{
		public Span<byte> Buffer;

		public int Position;

		public SpanWriter(Span<byte> buffer)
		{
			this.Buffer = buffer;
			this.Position = 0;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public Span<byte> EnsureBytes(int count)
		{
			int pos = this.Position;
			return this.Buffer.Length - pos >= count
				? this.Buffer.Slice(pos, count)
				: throw new InvalidOperationException();
		}

		public void WriteByte(byte value)
		{
			EnsureBytes(1)[0] = value;
			this.Position++;
		}

		public void WriteByte(sbyte value)
		{
			EnsureBytes(1)[0] = (byte) value;
			this.Position++;
		}

		public void WriteByte(int value)
		{
			EnsureBytes(1)[0] = (byte) value;
			this.Position++;
		}

		public void WriteByte(char value)
		{
			EnsureBytes(1)[0] = (byte) value;
			this.Position++;
		}

		#region 16-bits

		public void WriteInt16(short value)
		{
			var chunk = EnsureBytes(2);
			chunk[0] = (byte)value;
			chunk[1] = (byte)(value >> 8);
			this.Position += 2;
		}

		public static void WriteInt16(in Span<byte> span, short value)
		{
			if (span.Length < 2) throw new ArgumentException();
			span[0] = (byte)value;
			span[1] = (byte)(value >> 8);
		}

		public void WriteUInt16(ushort value)
		{
			var chunk = EnsureBytes(2);
			chunk[0] = (byte)value;
			chunk[1] = (byte)(value >> 8);
			this.Position += 2;
		}

		public static void WriteUInt16(in Span<byte> span, ushort value)
		{
			if (span.Length < 2) throw new ArgumentException();
			span[0] = (byte)value;
			span[1] = (byte)(value >> 8);
		}

		public void WriteInt16BE(short value)
		{
			var chunk = EnsureBytes(2);
			chunk[0] = (byte) (value >> 8);
			chunk[1] = (byte) value;
			this.Position += 2;
		}

		public static void WriteInt16BE(in Span<byte> span, short value)
		{
			if (span.Length < 2) throw new ArgumentException();
			span[0] = (byte) (value >> 8);
			span[1] = (byte) value;
		}

		public void WriteUInt16BE(ushort value)
		{
			var chunk = EnsureBytes(2);
			chunk[0] = (byte) (value >> 8);
			chunk[1] = (byte) value;
			this.Position += 2;
		}

		public static void WriteUInt16BE(in Span<byte> span, ushort value)
		{
			if (span.Length < 2) throw new ArgumentException();
			span[0] = (byte) (value >> 8);
			span[1] = (byte) value;
		}

		#endregion

		#region 32-bits...

		public void WriteInt32(int value)
		{
			var chunk = EnsureBytes(4);
			chunk[0] = (byte) value;
			chunk[1] = (byte) (value >> 8);
			chunk[2] = (byte) (value >> 16);
			chunk[3] = (byte) (value >> 24);
			this.Position += 4;
		}

		public void WriteUInt32(uint value)
		{
			var chunk = EnsureBytes(4);
			chunk[0] = (byte) value;
			chunk[1] = (byte) (value >> 8);
			chunk[2] = (byte) (value >> 16);
			chunk[3] = (byte) (value >> 24);
			this.Position += 4;
		}

		public static void WriteInt32(in Span<byte> span, int value)
		{
			if (span.Length < 4) throw new ArgumentException();
			span[0] = (byte) value;
			span[1] = (byte) (value >> 8);
			span[2] = (byte) (value >> 16);
			span[3] = (byte) (value >> 24);
		}

		public static void WriteUInt32(in Span<byte> span, uint value)
		{
			if (span.Length < 4) throw new ArgumentException();
			span[0] = (byte) value;
			span[1] = (byte) (value >> 8);
			span[2] = (byte) (value >> 16);
			span[3] = (byte) (value >> 24);
		}

		public void WriteInt32BE(int value)
		{
			var chunk = EnsureBytes(4);
			chunk[0] = (byte) (value >> 24);
			chunk[1] = (byte) (value >> 16);
			chunk[2] = (byte) (value >> 8);
			chunk[3] = (byte) value;
			this.Position += 4;
		}

		public void WriteUInt32BE(uint value)
		{
			var chunk = EnsureBytes(4);
			chunk[0] = (byte) (value >> 24);
			chunk[1] = (byte) (value >> 16);
			chunk[2] = (byte) (value >> 8);
			chunk[3] = (byte) value;
			this.Position += 4;
		}

		public static void WriteInt32BE(in Span<byte> span, int value)
		{
			if (span.Length < 4) throw new ArgumentException();
			span[0] = (byte) (value >> 24);
			span[1] = (byte) (value >> 16);
			span[2] = (byte) (value >> 8);
			span[3] = (byte) value;
		}

		public static void WriteUInt32BE(in Span<byte> span, uint value)
		{
			if (span.Length < 4) throw new ArgumentException();
			span[0] = (byte) (value >> 24);
			span[1] = (byte) (value >> 16);
			span[2] = (byte) (value >> 8);
			span[3] = (byte) value;
		}

		#endregion

		#region 64-bits...

		public void WriteInt64(long value)
		{
			var chunk = EnsureBytes(8);
			chunk[0] = (byte) value;
			chunk[1] = (byte) (value >> 8);
			chunk[2] = (byte) (value >> 16);
			chunk[3] = (byte) (value >> 24);
			chunk[4] = (byte) (value >> 32);
			chunk[5] = (byte) (value >> 40);
			chunk[6] = (byte) (value >> 48);
			chunk[7] = (byte) (value >> 56);
			this.Position += 8;
		}

		public void WriteUInt64(ulong value)
		{
			var chunk = EnsureBytes(8);
			chunk[0] = (byte) value;
			chunk[1] = (byte) (value >> 8);
			chunk[2] = (byte) (value >> 16);
			chunk[3] = (byte) (value >> 24);
			chunk[4] = (byte) (value >> 32);
			chunk[5] = (byte) (value >> 40);
			chunk[6] = (byte) (value >> 48);
			chunk[7] = (byte) (value >> 56);
			this.Position += 8;
		}

		public static void WriteInt64(in Span<byte> span, long value)
		{
			if (span.Length < 8) throw new ArgumentException();
			span[0] = (byte) value;
			span[1] = (byte) (value >> 8);
			span[2] = (byte) (value >> 16);
			span[3] = (byte) (value >> 24);
			span[4] = (byte) (value >> 32);
			span[5] = (byte) (value >> 40);
			span[6] = (byte) (value >> 48);
			span[7] = (byte) (value >> 56);
		}

		public static void WriteUInt64(in Span<byte> span, ulong value)
		{
			if (span.Length < 8) throw new ArgumentException();
			span[0] = (byte) value;
			span[1] = (byte) (value >> 8);
			span[2] = (byte) (value >> 16);
			span[3] = (byte) (value >> 24);
			span[4] = (byte) (value >> 32);
			span[5] = (byte) (value >> 40);
			span[6] = (byte) (value >> 48);
			span[7] = (byte) (value >> 56);
		}

		public void WriteInt64BE(long value)
		{
			var chunk = EnsureBytes(8);
			chunk[0] = (byte) (value >> 56);
			chunk[1] = (byte) (value >> 48);
			chunk[2] = (byte) (value >> 40);
			chunk[3] = (byte) (value >> 32);
			chunk[4] = (byte) (value >> 24);
			chunk[5] = (byte) (value >> 16);
			chunk[6] = (byte) (value >> 8);
			chunk[7] = (byte) value;
			this.Position += 8;
		}

		public void WriteUInt64BE(ulong value)
		{
			var chunk = EnsureBytes(8);
			chunk[0] = (byte) (value >> 56);
			chunk[1] = (byte) (value >> 48);
			chunk[2] = (byte) (value >> 40);
			chunk[3] = (byte) (value >> 32);
			chunk[4] = (byte) (value >> 24);
			chunk[5] = (byte) (value >> 16);
			chunk[6] = (byte) (value >> 8);
			chunk[7] = (byte) value;
			this.Position += 8;
		}

		public static void WriteInt64BE(in Span<byte> span, long value)
		{
			if (span.Length < 8) throw new ArgumentException();
			span[0] = (byte) (value >> 56);
			span[1] = (byte) (value >> 48);
			span[2] = (byte) (value >> 40);
			span[3] = (byte) (value >> 32);
			span[4] = (byte) (value >> 24);
			span[5] = (byte) (value >> 16);
			span[6] = (byte) (value >> 8);
			span[7] = (byte) value;
		}

		public static void WriteUInt64BE(in Span<byte> span, ulong value)
		{
			if (span.Length < 8) throw new ArgumentException();
			span[0] = (byte) (value >> 56);
			span[1] = (byte) (value >> 48);
			span[2] = (byte) (value >> 40);
			span[3] = (byte) (value >> 32);
			span[4] = (byte) (value >> 24);
			span[5] = (byte) (value >> 16);
			span[6] = (byte) (value >> 8);
			span[7] = (byte) value;
		}

		#endregion

		#region VarInts...

		/// <summary>Writes a 7-bit encoded unsigned int (aka 'Varint16') at the end, and advances the cursor</summary>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void WriteVarInt16(ushort value)
		{
			if (value < (1 << 7))
			{
				WriteByte((byte)value);
			}
			else
			{
				WriteVarInt16Slow(value);
			}
		}

		private void WriteVarInt16Slow(ushort value)
		{
			const uint MASK = 128;
			//note: value is known to be >= 128
			if (value < (1 << 14))
			{
				WriteBytes(
					(byte)(value | MASK),
					(byte)(value >> 7)
				);
			}
			else
			{
				WriteBytes(
					(byte)(value | MASK),
					(byte)((value >> 7) | MASK),
					(byte)(value >> 14)
				);
			}

		}

		/// <summary>Writes a 7-bit encoded unsigned int (aka 'Varint32') at the end, and advances the cursor</summary>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void WriteVarInt32(uint value)
		{
			if (value < (1 << 7))
			{
				WriteByte((byte) value);
			}
			else
			{
				WriteVarInt32Slow(value);
			}
		}

		private void WriteVarInt32Slow(uint value)
		{
			const uint MASK = 128;
			//note: value is known to be >= 128
			if (value < (1 << 14))
			{
				WriteBytes(
					(byte)(value | MASK),
					(byte)(value >> 7)
				);
			}
			else if (value < (1 << 21))
			{
				WriteBytes(
					(byte)(value | MASK),
					(byte)((value >> 7) | MASK),
					(byte)(value >> 14)
				);
			}
			else if (value < (1 << 28))
			{
				WriteBytes(
					(byte)(value | MASK),
					(byte)((value >> 7) | MASK),
					(byte)((value >> 14) | MASK),
					(byte)(value >> 21)
				);
			}
			else
			{
				WriteBytes(
					(byte)(value | MASK),
					(byte)((value >> 7) | MASK),
					(byte)((value >> 14) | MASK),
					(byte)((value >> 21) | MASK),
					(byte)(value >> 28)
				);
			}
		}

		/// <summary>Writes a 7-bit encoded unsigned long (aka 'Varint64') at the end, and advances the cursor</summary>
		public void WriteVarInt64(ulong value)
		{
			//note: if the size if 64-bits, we probably expect values to always be way above 128 so no need to optimize for this case here

			const uint MASK = 128;
			// max encoded size is 10 bytes
			var buffer = EnsureBytes((int) UnsafeHelpers.SizeOfVarInt(value));
			int p = this.Position;
			while (value >= MASK)
			{
				buffer[p++] = (byte) ((value & (MASK - 1)) | MASK);
				value >>= 7;
			}
			buffer[p++] = (byte) value;
			this.Position = p;
		}

		#endregion

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void WriteBytes(in ReadOnlySpan<byte> bytes)
		{
			bytes.CopyTo(EnsureBytes(bytes.Length));
			this.Position += bytes.Length;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void WriteBytes(in ReadOnlyMemory<byte> bytes)
		{
			bytes.Span.CopyTo(EnsureBytes(bytes.Length));
			this.Position += bytes.Length;
		}

		public void WriteBytes(byte value1, byte value2)
		{
			var chunk = EnsureBytes(2);
			chunk[0] = value1;
			chunk[1] = value2;
			this.Position += 2;
		}

		public void WriteBytes(byte value1, byte value2, byte value3)
		{
			var chunk = EnsureBytes(3);
			chunk[0] = value1;
			chunk[1] = value2;
			chunk[2] = value3;
			this.Position += 3;
		}

		public void WriteBytes(byte value1, byte value2, byte value3, byte value4)
		{
			var chunk = EnsureBytes(4);
			chunk[0] = value1;
			chunk[1] = value2;
			chunk[2] = value3;
			chunk[3] = value4;
			this.Position += 4;
		}

		public void WriteBytes(byte value1, byte value2, byte value3, byte value4, byte value5)
		{
			var chunk = EnsureBytes(5);
			chunk[0] = value1;
			chunk[1] = value2;
			chunk[2] = value3;
			chunk[3] = value4;
			chunk[4] = value5;
			this.Position += 5;
		}

	}

}
