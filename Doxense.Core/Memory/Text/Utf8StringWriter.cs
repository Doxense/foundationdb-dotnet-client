﻿#region Copyright (c) 2023 SnowBank SAS, (c) 2005-2023 Doxense SAS
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

namespace Doxense.Memory.Text
{
	using System;
	using System.Globalization;
	using System.IO;
	using System.Text;
	using Doxense.Text;
	using JetBrains.Annotations;

	public sealed class Utf8StringWriter : TextWriter
	{
		public override Encoding Encoding => Encoding.UTF8;

		private SliceWriter Writer;

		public Utf8StringWriter(SliceWriter writer)
			: base(CultureInfo.InvariantCulture)
		{
			this.Writer = writer;
		}

		public Utf8StringWriter(int capacity)
		{
			this.Writer = new SliceWriter(capacity);
		}

		public override void Write(char value)
		{
			if (value < 0x80)
			{
				this.Writer.WriteByte((byte) value);
			}
			else if (!Utf8Encoder.TryWriteUnicodeCodePoint(ref this.Writer, (UnicodeCodePoint) value))
			{
				throw new DecoderFallbackException("Failed to encode invalid Unicode CodePoint into UTF-8");
			}
		}

		public override void Write(char[]? buffer)
		{
			if (buffer != null) this.Writer.WriteStringUtf8(buffer);
		}

		public override void Write(string? value)
		{
			if (value != null) this.Writer.WriteStringUtf8(value);
		}

		public override void Write(int value)
		{
			this.Writer.WriteBase10(value);
		}

		public override void Write(long value)
		{
			this.Writer.WriteBase10(value);
		}

		public override void Write(uint value)
		{
			this.Writer.WriteBase10(value);
		}

		public override void Write(ulong value)
		{
			this.Writer.WriteBase10(value);
		}

		[Pure]
		public byte[] ToArray()
		{
			return this.Writer.GetBytes();
		}

		[Pure]
		public Slice GetBuffer()
		{
			return this.Writer.ToSlice();
		}
	}
}
