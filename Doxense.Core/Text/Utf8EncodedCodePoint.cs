// adapted from https://github.com/dotnet/corefxlab/tree/master/src/System.Text.Utf8

namespace Doxense.Text
{
	using System;
	using System.Runtime.InteropServices;

	[StructLayout(LayoutKind.Explicit)]
	public readonly struct Utf8EncodedCodePoint
	{
		[FieldOffset(0)]
		public readonly int Length;

		[FieldOffset(4)]
		public readonly byte Byte0;

		[FieldOffset(5)]
		public readonly byte Byte1;

		[FieldOffset(6)]
		public readonly byte Byte2;

		[FieldOffset(7)]
		public readonly byte Byte3;

		public Utf8EncodedCodePoint(char character)
			: this()
		{
			unsafe
			{
				fixed (byte* ptr = &this.Byte0)
				{
					if (!Utf8Encoder.TryEncodeEncodePoint((UnicodeCodePoint) character, ptr, ptr + 4, out this.Length))
					{
						throw new InvalidOperationException("TODO: Invalid code point");
					}
				}
			}
		}

		public Utf8EncodedCodePoint(char highSurrogate, char lowSurrogate)
			: this()
		{
			unsafe
			{
				fixed (byte* ptr = &this.Byte0)
				{
					if (!Utf8Encoder.TryEncodeEncodePoint((UnicodeCodePoint) char.ConvertToUtf32(highSurrogate, lowSurrogate), ptr, ptr + 4, out this.Length))
					{
						throw new InvalidOperationException("TODO: Invalid code point");
					}
				}
			}
		}

		public static explicit operator Utf8EncodedCodePoint(char c) => new Utf8EncodedCodePoint(c);

	}
}
