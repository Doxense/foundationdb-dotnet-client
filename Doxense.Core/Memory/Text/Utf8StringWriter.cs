#region Copyright (c) 2005-2023 Doxense SAS
//
// All rights are reserved. Reproduction or transmission in whole or in part, in
// any form or by any means, electronic, mechanical or otherwise, is prohibited
// without the prior written consent of the copyright owner.
//
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
