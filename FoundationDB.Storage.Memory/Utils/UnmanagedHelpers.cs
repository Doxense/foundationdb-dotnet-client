#region Copyright (c) 2013-2014, Doxense SAS. All rights reserved.
// See License.MD for license information
#endregion

#define USE_NATIVE_MEMORY_OPERATORS

namespace FoundationDB.Storage.Memory.Core
{
	using FoundationDB.Client;
	using System;
	using System.Diagnostics.Contracts;
	using System.Runtime.ConstrainedExecution;
	using System.Runtime.InteropServices;
	using System.Security;

	internal static unsafe class UnmanagedHelpers
	{

		/// <summary>Round a number to the next power of 2</summary>
		/// <param name="x">Positive integer that will be rounded up (if not already a power of 2)</param>
		/// <returns>Smallest power of 2 that is greater than or equal to <paramref name="x"/></returns>
		/// <remarks>Will return 1 for <paramref name="x"/> = 0 (because 0 is not a power of 2 !), and will throw for <paramref name="x"/> &lt; 0</remarks>
		/// <exception cref="System.ArgumentOutOfRangeException">If <paramref name="x"/> is a negative number</exception>
		public static uint NextPowerOfTwo(uint x)
		{
			// cf http://en.wikipedia.org/wiki/Power_of_two#Algorithm_to_round_up_to_power_of_two

			// special case
			if (x == 0) return 1;

			--x;
			x |= (x >> 1);
			x |= (x >> 2);
			x |= (x >> 4);
			x |= (x >> 8);
			x |= (x >> 16);
			return x + 1;
		}

		/// <summary>Copy a managed slice to the specified memory location</summary>
		/// <param name="dest">Where to copy the bytes</param>
		/// <param name="src">Slice of managed memory that will be copied to the destination</param>
		[SecurityCritical, ReliabilityContract(Consistency.WillNotCorruptState, Cer.Success)]
		public static void CopyUnsafe(byte* dest, Slice src)
		{
			if (src.Count > 0)
			{
				Contract.Requires(dest != null && src.Array != null && src.Offset >= 0 && src.Count >= 0);
				fixed (byte* ptr = src.Array)
				{
					CopyUnsafe(dest, ptr + src.Offset, (uint)src.Count);
				}
			}
		}

		public static void CopyUnsafe(Slice dest, byte* src, uint count)
		{
			if (count > 0)
			{
				Contract.Requires(dest.Array != null && dest.Offset >= 0 && dest.Count >= 0 && src != null);
				fixed (byte* ptr = dest.Array)
				{
					NativeMethods.memmove(ptr + dest.Offset, src, count);
				}
			}
		}

		/// <summary>Copy an unmanaged slice to the specified memory location</summary>
		/// <param name="dest">Where to copy the bytes</param>
		/// <param name="src">Slice un unmanaged memory that will be copied to the destination</param>
		[SecurityCritical, ReliabilityContract(Consistency.WillNotCorruptState, Cer.Success)]
		public static void CopyUnsafe(byte* dest, USlice src)
		{
			if (src.Count > 0)
			{
				Contract.Requires(dest != null && src.Data != null);
				CopyUnsafe(dest, src.Data, src.Count);
			}
		}

		public static void CopyUnsafe(USlice dest, byte* src, uint count)
		{
			if (count > 0)
			{
				Contract.Requires(dest.Data != null && src != null);
				CopyUnsafe(dest.Data, src, count);
			}
		}

		/// <summary>Dangerously copy native memory from one location to another</summary>
		/// <param name="dest">Where to copy the bytes</param>
		/// <param name="src">Where to read the bytes</param>
		/// <param name="count">Number of bytes to copy</param>
		[SecurityCritical, ReliabilityContract(Consistency.WillNotCorruptState, Cer.Success)]
		public static void CopyUnsafe(byte* dest, byte* src, uint count)
		{
			Contract.Requires(dest != null && src != null);

#if USE_NATIVE_MEMORY_OPERATORS
			NativeMethods.memmove(dest, src, count);
#else
			if (count >= 16)
			{
				do
				{
					*((int*)(dest + 0)) = *((int*)(src + 0));
					*((int*)(dest + 4)) = *((int*)(src + 4));
					*((int*)(dest + 8)) = *((int*)(src + 8));
					*((int*)(dest + 12)) = *((int*)(src + 12));
					dest += 16;
					src += 16;
				}
				while ((count -= 16) >= 16);
			}
			if (count > 0)
			{
				if ((count & 8) != 0)
				{
					*((int*)(dest + 0)) = *((int*)(src + 0));
					*((int*)(dest + 4)) = *((int*)(src + 4));
					dest += 8;
					src += 8;
				}
				if ((count & 4) != 0)
				{
					*((int*)dest) = *((int*)src);
					dest += 4;
					src += 4;
				}
				if ((count & 2) != 0)
				{
					*((short*)dest) = *((short*)src);
					dest += 2;
					src += 2;
				}
				if ((count & 1) != 0)
				{
					*dest = *src;
				}
			}
#endif
		}

		/// <summary>Retourne l'offset de la première différence trouvée entre deux buffers de même taille</summary>
		/// <param name="left">Pointeur sur le premier buffer (de taille égale à 'count')</param>
		/// <param name="right">Pointeur sur le deuxième buffer (de taille égale à 'count')</param>
		/// <param name="count">Nombre d'éléments à comparer</param>
		/// <returns>Offset vers le premier élément qui diffère, ou -1 si les deux buffers sont identiques</returns>
		[SecurityCritical, ReliabilityContract(Consistency.WillNotCorruptState, Cer.Success)]
		public static int CompareUnsafe(byte* left, uint leftCount, byte* right, uint rightCount)
		{
			if (leftCount == 0) return rightCount == 0 ? 0 : -1;
			if (rightCount == 0) return +1;

			Contract.Requires(left != null && right != null);

#if USE_NATIVE_MEMORY_OPERATORS
			int c = NativeMethods.memcmp(left, right, leftCount < rightCount ? leftCount : rightCount);
			if (c != 0) return c;
			return (int)leftCount - (int)rightCount;
#else

			// On va scanner par segments de 8, en continuant tant qu'ils sont identiques.
			// Dés qu'on tombe sur un segment de 8 différent, on backtrack au début du segment, et on poursuit en mode octet par octet
			// Recherche la première position où les octets diffèrent, et retourne left[POS] - right[POS].
			// Si tous les octets sont identiques, retourne 0

			byte* start = left;


			// OPTIMISATION DE LA MORT QUI TUE
			// Si on calcul le XOR entre les blocs de 8 bytes, chaque byte identique deviendra 0.
			// Si le XOR total n'est pas 0, on regarde a quel endroit se trouve le premier byte non-0, et cela nous donne l'offset de la différence

			// Données identiques:
			//	left : "11 22 33 44 55 66 77 88" => 0x8877665544332211
			//	right: "11 22 33 44 55 66 77 88" => 0x8877665544332211
			//	left XOR right => 0x8877665544332211 ^ 0x8877665544332211 = 0

			// Différence
			//	left : "11 22 33 44 55 66 77 88" => 0x8877665544332211
			//	right: "11 22 33 44 55 AA BB CC" => 0xCCBBAA5544332211
			//	left XOR right =0x8877665544332211 ^ 0xCCBBAA5544332211 = 0x44CCCC0000000000
			//  le premier byte différent de 0 est le byte 5 (note: on part de la fin, offset 0 !) qui est 0xCC

			// d'abord, on compare 8 bytes par 8 bytes
			while (count >= 8)
			{
				// XOR les deux segments
				// => s'il sont identiques, on obtient 0
				// => sinon, le premier byte non 0 (big-endian) indiquera l'offset de la différence
				ulong k = *((ulong*)left) ^ *((ulong*)right);

				if (k != 0)
				{ // on a trouvé une différence, mais cela pourrait être n'importe quel byte
					//System.Diagnostics.Trace.WriteLine("Found mistmatch\n\t\t0x" + k.ToString("x16") + " between\n\t\t0x" + ((ulong*)left)[0].ToString("x16") + " and\n\t\t0x" + ((ulong*)right)[0].ToString("x16"));
					int p = 0;
					while ((k & 0xFF) == 0)
					{
						++p;
						k >>= 8;
					}
					//System.Diagnostics.Trace.WriteLine("First differing byte at +" + p + " => " + left[p] + " != " + right[p]);
					return left[p] - right[p];
				}
				left += 8;
				right += 8;
				count -= 8;
			}

			// la taille restante est forcément entre 0 et 7
			if (count >= 4)
			{
				if (*((uint*)left) != *((uint*)right))
				{ // on a trouvé une différence, mais cela pourrait être n'importe quel byte
					goto compare_tail;
				}
				left += 4;
				right += 4;
				count -= 4;
			}

			// la taille restante est forcément entre 0 et 3

		compare_tail:
			while (count-- > 0)
			{
				int n = *(left++) - *(right++);
				if (n != 0) return n;
			}
			return 0;
#endif
		}

		public static void FillUnsafe(byte* ptr, uint count, byte filler)
		{
			if (count == 0) return;
			if (ptr == null) throw new ArgumentNullException("ptr");

#if USE_NATIVE_MEMORY_OPERATORS
			NativeMethods.memset(ptr, filler, count);

#else
			if (filler == 0)
			{
				while (count-- > 0) *ptr++ = 0;
			}
			else
			{
				while (count-- > 0) *ptr++ = filler;
			}
#endif
		}

#if USE_NATIVE_MEMORY_OPERATORS

		[SuppressUnmanagedCodeSecurity]
		private static unsafe class NativeMethods
		{

			[DllImport("msvcrt.dll", CallingConvention = CallingConvention.Cdecl, SetLastError = false)]
			public static extern int memcmp(byte* lhs, byte* rhs, uint count);

			[DllImport("msvcrt.dll", CallingConvention = CallingConvention.Cdecl, SetLastError = false)]
			public static extern int memmove(byte* dest, byte* src, uint count);

			[DllImport("msvcrt.dll", CallingConvention = CallingConvention.Cdecl, SetLastError = false)]
			public static extern int memset(byte* dest, byte ch, uint count);
		}

#endif

	}
}
