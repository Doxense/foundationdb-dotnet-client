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

namespace Doxense.Serialization
{
	using System;
	using Doxense.Memory;

	[Flags]
	public enum Base62FormattingOptions
	{
		None = 0,
		Padded = 0x1,
		Separator = 0x2,
		Lexicographic = 0x4,
	}

	public static class Base62Encoding
	{

		private static readonly char[] Base62Chars = "abcdefghijklmnopqrstuvwxyz0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZ".ToCharArray();
		private static readonly char[] Base62LexicographicChars = "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz".ToCharArray();
		private static readonly int[] BitSizes = ComputeBitSizes();

		private static int[] ComputeBitSizes()
		{
			const double Base62Log = 4.1271343850450915553463964460005;

			var sizes = new int[65]; // 0 .. 64
			sizes[0] = 1; // "a"
			for (int i = 1; i < sizes.Length; i++)
			{
				sizes[i] = (int) Math.Ceiling(Math.Log(Math.Pow(2, i)) / Base62Log);
			}
			return sizes;
		}

		public static string Encode(this Guid value)
		{
			return Encode(value, Base62FormattingOptions.Separator);
		}

		/// <summary>Encode un Guid en base62 (alpha+num)</summary>
		public static string Encode(this Guid value, Base62FormattingOptions options)
		{
			//REVIEW: recoder en utilisant Slice/Uuid128?
			var buffer = new SliceReader(value.ToByteArray());
			string hi = Encode(buffer.ReadFixed64BE(), 64, options);
			string lo = Encode(buffer.ReadFixed64BE(), 64, options);

			if ((options & Base62FormattingOptions.Separator) == Base62FormattingOptions.Separator)
			{
				return hi + "-" + lo;
			}
			else
			{
				return hi + lo;
			}
		}

		public static string Encode(ulong high, ulong low, Base62FormattingOptions options)
		{
			string hi = Encode(high, 64, options);
			string lo = Encode(low, 64, options);

			if ((options & Base62FormattingOptions.Separator) == Base62FormattingOptions.Separator)
			{
				return hi + "-" + lo;
			}
			else
			{
				return hi + lo;
			}
		}

		public static string Encode(int value, Base62FormattingOptions options = Base62FormattingOptions.None)
		{
			return Encode((ulong) ((uint) value), 32, options);
		}

		public static string Encode(long value, Base62FormattingOptions options = Base62FormattingOptions.None)
		{
			return Encode((ulong) value, 64, options);
		}

		public static string EncodeSortable(int value)
		{
			return Encode((ulong) ((uint) value), 32, Base62FormattingOptions.Padded | Base62FormattingOptions.Lexicographic);
		}

		public static string EncodeSortable(long value)
		{
			return Encode((ulong) value, 64, Base62FormattingOptions.Padded | Base62FormattingOptions.Lexicographic);
		}

		/// <summary>Encode une section de buffer en base 62 (alpha+num)</summary>
		/// <returns>Section encodée en base 62</returns>
		private static string Encode(ulong value, int bits, Base62FormattingOptions options)
		{
			if (bits < 0 || bits >= BitSizes.Length) throw new ArgumentOutOfRangeException(nameof(bits), bits, "Bits number not supported");

			bool fast = (options & Base62FormattingOptions.Padded) != Base62FormattingOptions.Padded;
			int max = BitSizes[bits];
			if (max > 16) throw new InvalidOperationException("Internal error: max is too big");

			unsafe
			{
				// pour connaître la taille max en char, on prend comme approximation ceil(prend length * 1.4)
				char* chars = stackalloc char[16]; // note: ulong.MaxValue prend 11 chars maximum, mais on alloue 16 pour rester aligné

				char* pc = chars + (max - 1);
				var bc = (options & Base62FormattingOptions.Lexicographic) == Base62FormattingOptions.Lexicographic
					? Base62LexicographicChars
					: Base62Chars;

				while (pc >= chars) // protection anti underflow
				{
					ulong r = value % 62L;
					value /= 62L;
					// ATTENTION: le remainder peut être négatif si value est négatif !
					*pc-- = bc[(int)r];
					if (fast && value == 0) break;
				}

				++pc;
				int count = max - (int)(pc - chars);
				if (count <= 0) return string.Empty;
				return new string(pc, 0, count);
			}
		}

	}

}
