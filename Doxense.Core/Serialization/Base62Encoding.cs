#region Copyright (c) 2005-2023 Doxense SAS
//
// All rights are reserved. Reproduction or transmission in whole or in part, in
// any form or by any means, electronic, mechanical or otherwise, is prohibited
// without the prior written consent of the copyright owner.
//
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
