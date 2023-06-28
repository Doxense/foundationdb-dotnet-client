#region Copyright Doxense 2005-2018
//
// All rights are reserved. Reproduction or transmission in whole or in part, in
// any form or by any means, electronic, mechanical or otherwise, is prohibited
// without the prior written consent of the copyright owner.
//
#endregion

namespace Doxense.IO.Hashing
{
	using System;
	using System.Runtime.CompilerServices;
	using System.Runtime.InteropServices;
	using Doxense.Diagnostics.Contracts;
	using JetBrains.Annotations;

	/// <summary>Calcul de hash xxHash sur 64 bits</summary>
	/// <remarks>IMPORTANT: Ce hash n'est PAS cryptographique ! Il peut leaker des informations sur les données hashées, et ne doit donc pas être utilisé publiquement dans un scenario de protection de données! (il faut plutot utiliser SHA ou HMAC pour ce genre de choses)</remarks>
	public static class XxHash64
	{
		// From https://code.google.com/p/xxhash/

		// En théorie, 2x plus rapide que XXH32 sur x64, par contre beaucoup plus lent (~3x) sur x86
		// See http://fastcompression.blogspot.fr/2014/07/xxhash-wider-64-bits.html

		// Implémentation basée sur le patch r35: https://code.google.com/p/xxhash/source/detail?r=35

		private const ulong INITIAL_SEED = 0;

		private const ulong PRIME64_1 = 11400714785074694791UL;
		private const ulong PRIME64_2 = 14029467366897019727UL;
		private const ulong PRIME64_3 = 1609587929392839161UL;
		private const ulong PRIME64_4 = 9650029242287828579UL;
		private const ulong PRIME64_5 = 2870177450012600261UL;

		/// <summary>Hash of the empty buffer</summary>
		public const ulong HASH0 = 0xEF46DB3751D8E999UL;

		/// <summary>Calcul le xxHash 64-bit d'un chaîne (a partir de sa représentation UTF-16 en mémoire)</summary>
		/// <param name="text">Chaîne de texte à convertir</param>
		/// <returns>Code xxHash 64 bit calculé sur la représentation UTF-16 de la chaîne. Attention: N'est pas garantit unique!</returns>
		public static ulong FromText(string text)
		{
			Contract.NotNull(text);
			unsafe
			{
				if (text.Length == 0) return ContinueUnsafe(INITIAL_SEED, null, 0);
				fixed (char* chars = text)
				{
					return ContinueUnsafe(INITIAL_SEED, (byte*) chars, checked((uint) text.Length * sizeof(char)));
				}
			}
		}

		/// <summary>Calcul le xxHash 64-bit d'un chaîne (a partir de sa représentation UTF-16 en mémoire)</summary>
		/// <returns>Code xxHash 64 bit calculé sur la représentation UTF-16 de la chaîne. Attention: N'est pas garantit unique!</returns>
		public static ulong FromText(ReadOnlySpan<char> buffer)
		{
			unsafe
			{
				if (buffer.Length == 0) return ContinueUnsafe(INITIAL_SEED, null, 0);
				fixed (char* chars = &MemoryMarshal.GetReference(buffer))
				{
					return ContinueUnsafe(INITIAL_SEED, (byte*) chars, checked((uint) buffer.Length * sizeof(char)));
				}
			}
		}

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static ulong FromBytes(byte[] input)
		{
			Contract.NotNull(input);
			return Continue(INITIAL_SEED, input.AsSlice());
		}

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static ulong FromBytes(Slice input)
		{
			return Continue(INITIAL_SEED, input);
		}

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static ulong FromBytes(ReadOnlySpan<byte> input)
		{
			return Continue(INITIAL_SEED, input);
		}

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static unsafe ulong FromBytesUnsafe(byte* input, uint len)
		{
			Contract.PointerNotNull(input);
			return ContinueUnsafe(INITIAL_SEED, input, len);
		}

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static ulong Continue(ulong seed, byte[] input)
		{
			Contract.NotNull(input);
			return Continue(seed, input.AsSlice());
		}

		[Pure]
		public static ulong Continue(ulong seed, Slice input)
		{
			Contract.NotNull(input.Array, paramName: nameof(input));
			unsafe
			{
				if (input.Count == 0) return ContinueUnsafe(seed, null, 0);
				fixed (byte* ptr = &input.DangerousGetPinnableReference())
				{
					return ContinueUnsafe(seed, ptr, (uint) input.Count);
				}
			}
		}

		[Pure]
		public static ulong Continue(ulong seed, ReadOnlySpan<byte> input)
		{
			unsafe
			{
				if (input.Length == 0) return ContinueUnsafe(seed, null, 0);
				fixed (byte* pBytes = &MemoryMarshal.GetReference(input))
				{
					return ContinueUnsafe(seed, pBytes, (uint) input.Length);
				}
			}
		}

		[Pure]
		public static unsafe ulong ContinueUnsafe(ulong seed, byte* input, uint len)
		{
			Contract.Debug.Requires(input != null || len == 0);
			Contract.Debug.Requires(BitConverter.IsLittleEndian, "Not implemented for Big Endian architectures !");

			if (len == 0 && seed == INITIAL_SEED) return HASH0;

			byte* p = input;
			if (p == null)
			{
				// autorise le null pointer (UNIQUEMENT si len == 0)!
				if (len > 0) throw new ArgumentNullException(nameof(input));
				p = (byte*) 16;
			}

			byte* bEnd = p + len;
			ulong h64;

			if (len >= 32)
			{
				ulong* p64 = (ulong*) p;
				ulong* limit = (ulong*) (bEnd - 32);
				ulong v1 = seed + PRIME64_1 + PRIME64_2;
				ulong v2 = seed + PRIME64_2;
				ulong v3 = seed + 0;
				ulong v4 = seed - PRIME64_1;

				do
				{
					// for(# in 1..4) => v# += XXH_get64bits(p) * PRIME64_2; p+=8; v# = XXH_rotl64(v#, 31); v# *= PRIME64_1;
					v1 += *(p64 + 0) * PRIME64_2; v1 = ((v1 << 31) | (v1 >> (64 - 31))) * PRIME64_1;
					v2 += *(p64 + 1) * PRIME64_2; v2 = ((v2 << 31) | (v2 >> (64 - 31))) * PRIME64_1;
					v3 += *(p64 + 2) * PRIME64_2; v3 = ((v3 << 31) | (v3 >> (64 - 31))) * PRIME64_1;
					v4 += *(p64 + 3) * PRIME64_2; v4 = ((v4 << 31) | (v4 >> (64 - 31))) * PRIME64_1;
					p64 += 4; // note: 4 x sizeof(ulong) == 32
				}
				while (p64 <= limit);

				// h64 = XXH_rotl64(v1, 1) + XXH_rotl64(v2, 7) + XXH_rotl64(v3, 12) + XXH_rotl64(v4, 18)
				h64 = ((v1 << 1) | (v1 >> (64 - 1))) + ((v2 << 7) | (v2 >> (64 - 7))) + ((v3 << 12) | (v3 >> (64 - 12))) + ((v4 << 18) | (v4 >> (64 - 18)));

				// for(# in 1..4) =>
				//	v# *= PRIME64_2; v# = XXH_rotl64(v#, 31); v# *= PRIME64_1; h64 ^= v#;
				//	h64 = h64 * PRIME64_1 + PRIME64_4;

				v1 *= PRIME64_2; v1 = ((v1 << 31) | (v1 >> (64 - 31))) * PRIME64_1; h64 ^= v1; h64 = h64 * PRIME64_1 + PRIME64_4;
				v2 *= PRIME64_2; v2 = ((v2 << 31) | (v2 >> (64 - 31))) * PRIME64_1; h64 ^= v2; h64 = h64 * PRIME64_1 + PRIME64_4;
				v3 *= PRIME64_2; v3 = ((v3 << 31) | (v3 >> (64 - 31))) * PRIME64_1; h64 ^= v3; h64 = h64 * PRIME64_1 + PRIME64_4;
				v4 *= PRIME64_2; v4 = ((v4 << 31) | (v4 >> (64 - 31))) * PRIME64_1; h64 ^= v4; h64 = h64 * PRIME64_1 + PRIME64_4;

				p = (byte*) p64;
			}
			else
			{
				h64 = seed + PRIME64_5;
			}

			h64 += len;

			while (p <= bEnd - 8)
			{
				ulong k1;
				k1 = *((ulong*) p) * PRIME64_2; k1 = ((k1 << 31) | (k1 >> (64 - 31))) * PRIME64_1; h64 ^= k1;
				h64 = ((h64 << 27) | (h64 >> (64 - 27))) * PRIME64_1 + PRIME64_4;
				p += 8;
			}

			if (p <= bEnd - 4)
			{
				h64 ^= *((uint*) p) * PRIME64_1;
				h64 = ((h64 << 23) | (h64 >> (64 - 23))) * PRIME64_2 + PRIME64_3;
				p += 4;
			}

			while (p < bEnd)
			{
				h64 ^= (*p) * PRIME64_5;
				h64 = ((h64 << 11) | (h64 >> (64 - 11))) * PRIME64_1;
				p++;
			}

			h64 ^= h64 >> 33;
			h64 *= PRIME64_2;
			h64 ^= h64 >> 29;
			h64 *= PRIME64_3;
			h64 ^= h64 >> 32;
			return h64;
		}

#if FULL_DEBUG
		public static void Test()
		{

			var bytes = Encoding.UTF8.GetBytes("test");
			ulong h = XxHash64.Continue(123, bytes, 0, bytes.Length);
			Console.WriteLine(h + " : " + h.ToString("X16"));

			const string TEXT = "Il était une fois un petit chaperon rouge qui s'en allait au bois, et c'est alors que sa mère prit peur et l'envoya chez sa tante et son oncle à Bel Air";
			bytes = Encoding.UTF8.GetBytes(TEXT);

			h = XxHash64.Compute(bytes);
			Console.WriteLine(h + " : " + h.ToString("X16"));

			bytes = Encoding.UTF8.GetBytes("12345345234572");
			h = XxHash64.Continue(0x9747b28c, bytes, 0, bytes.Length);
			Console.WriteLine(h);
			Console.WriteLine(h.ToString("X8"));
			Console.WriteLine(h + " : " + h.ToString("X16"));
		}
#endif

	}

}
