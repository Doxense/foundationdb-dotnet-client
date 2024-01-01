#region Copyright (c) 2023-2024 SnowBank SAS, (c) 2005-2023 Doxense SAS
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

namespace Doxense.Serialization
{
	using System;
	using System.Text;
	using Doxense.Diagnostics.Contracts;

	public static class Base32Encoding
	{
		//note: c'est une copie de la classe hellper Base32 qui est internal dans la BCL ! :(

		private static readonly string _base32Chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567";

		public static string ToBase32(byte[] input)
		{
			Contract.NotNull(input);
			return ToBase32(input.AsSpan());
		}

		public static string ToBase32(Slice input)
		{
			return ToBase32(input.Span);
		}

		public static string ToBase32(ReadOnlySpan<byte> input)
		{
			var sb = new StringBuilder();
			for (int offset = 0; offset < input.Length;)
			{
				byte a, b, c, d, e, f, g, h;
				int numCharsToOutput = GetNextGroup(input, ref offset, out a, out b, out c, out d, out e, out f, out g, out h);

				sb.Append((numCharsToOutput >= 1) ? _base32Chars[a] : '=');
				sb.Append((numCharsToOutput >= 2) ? _base32Chars[b] : '=');
				sb.Append((numCharsToOutput >= 3) ? _base32Chars[c] : '=');
				sb.Append((numCharsToOutput >= 4) ? _base32Chars[d] : '=');
				sb.Append((numCharsToOutput >= 5) ? _base32Chars[e] : '=');
				sb.Append((numCharsToOutput >= 6) ? _base32Chars[f] : '=');
				sb.Append((numCharsToOutput >= 7) ? _base32Chars[g] : '=');
				sb.Append((numCharsToOutput >= 8) ? _base32Chars[h] : '=');
			}

			return sb.ToString();
		}

		public static byte[] FromBase32(string input)
		{
			Contract.NotNull(input);
			return FromBase32(input.AsSpan());
		}

		public static byte[] FromBase32(ReadOnlySpan<char> input)
		{
			input = input.TrimEnd('=');
			if (input.Length == 0)
			{
				return Array.Empty<byte>();
			}

			var output = new byte[input.Length * 5 / 8];
			var bitIndex = 0;
			var inputIndex = 0;
			var outputBits = 0;
			var outputIndex = 0;
			var base32Chars = _base32Chars;
			while (outputIndex < output.Length)
			{
				var byteIndex = base32Chars.IndexOf(char.ToUpperInvariant(input[inputIndex]));
				if (byteIndex < 0)
				{
					throw new FormatException();
				}

				var bits = Math.Min(5 - bitIndex, 8 - outputBits);
				output[outputIndex] <<= bits;
				output[outputIndex] |= (byte)(byteIndex >> (5 - (bitIndex + bits)));

				bitIndex += bits;
				if (bitIndex >= 5)
				{
					inputIndex++;
					bitIndex = 0;
				}

				outputBits += bits;
				if (outputBits >= 8)
				{
					outputIndex++;
					outputBits = 0;
				}
			}
			return output;
		}

		// returns the number of bytes that were output
		private static int GetNextGroup(ReadOnlySpan<byte> input, ref int offset, out byte a, out byte b, out byte c, out byte d, out byte e, out byte f, out byte g, out byte h)
		{
			uint b1, b2, b3, b4, b5;

			int retVal;
			switch (offset - input.Length)
			{
				case 1:  retVal = 2; break;
				case 2:  retVal = 4; break;
				case 3:  retVal = 5; break;
				case 4:  retVal = 7; break;
				default: retVal = 8; break;
			}

			b1 = (offset < input.Length) ? input[offset++] : 0U;
			b2 = (offset < input.Length) ? input[offset++] : 0U;
			b3 = (offset < input.Length) ? input[offset++] : 0U;
			b4 = (offset < input.Length) ? input[offset++] : 0U;
			b5 = (offset < input.Length) ? input[offset++] : 0U;

			a = (byte)(b1 >> 3);
			b = (byte)(((b1 & 0x07) << 2) | (b2 >> 6));
			c = (byte)((b2 >> 1) & 0x1f);
			d = (byte)(((b2 & 0x01) << 4) | (b3 >> 4));
			e = (byte)(((b3 & 0x0f) << 1) | (b4 >> 7));
			f = (byte)((b4 >> 2) & 0x1f);
			g = (byte)(((b4 & 0x3) << 3) | (b5 >> 5));
			h = (byte)(b5 & 0x1f);

			return retVal;
		}

	}

}
