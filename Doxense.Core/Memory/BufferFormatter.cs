#region Copyright (c) 2005-2023 Doxense SAS
// All rights reserved.
// 
// Redistribution and use in source and binary forms, with or without
// modification, are permitted provided that the following conditions are met:
// 	* Redistributions of source code must retain the above copyright
// 	  notice, this list of conditions and the following disclaimer.
// 	* Redistributions in binary form must reproduce the above copyright
// 	  notice, this list of conditions and the following disclaimer in the
// 	  documentation and/or other materials provided with the distribution.
// 	* Neither the name of Doxense nor the
// 	  names of its contributors may be used to endorse or promote products
// 	  derived from this software without specific prior written permission.
// 
// THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS "AS IS" AND
// ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED
// WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE
// DISCLAIMED. IN NO EVENT SHALL DOXENSE BE LIABLE FOR ANY
// DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES
// (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES;
// LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND
// ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT
// (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS
// SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
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
}
