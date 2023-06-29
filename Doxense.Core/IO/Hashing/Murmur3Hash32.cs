#region Copyright (c) 2005-2023 Doxense SAS
//
// All rights are reserved. Reproduction or transmission in whole or in part, in
// any form or by any means, electronic, mechanical or otherwise, is prohibited
// without the prior written consent of the copyright owner.
//
// Based on Murmur3 by Austin Appleby
//-----------------------------------------------------------------------------
// MurmurHash3 was written by Austin Appleby, and is placed in the public
// domain. The author hereby disclaims copyright to this source code.

#endregion

namespace Doxense.IO.Hashing
{
	using System;
	using System.Runtime.CompilerServices;
	using System.Runtime.InteropServices;
	using Doxense.Diagnostics.Contracts;
	using JetBrains.Annotations;

	/// <summary>Calcul d'un hash "Murmur3" sur 32 bits</summary>
	/// <remarks>IMPORTANT: Ce hash n'est PAS cryptographique ! Il peut leaker des informations sur les données hashées, et ne doit donc pas être utilisé publiquement dans un scenario de protection de données! (il faut plutot utiliser SHA ou HMAC pour ce genre de choses)</remarks>
	[PublicAPI]
	public static class Murmur3Hash32
	{
		// Version 32 bits de "MurmurHash3", créé par Austin Appleby. Cet algo de hashing est idéal pour calcule un hashcode pour utilisation avec une table de hashage.
		// ATTENTION: Ce n'est *PAS* un hash cryptographique (il peut leaker des informations sur les données sources), et ne doit donc être calculé que sur les données déja cryptées.

		// "MurmurHash3 is the successor to MurmurHash2. It comes in 3 variants - a 32-bit version that targets low latency for hash table use and two 128-bit versions for generating unique identifiers for large blocks of data, one each for x86 and x64 platforms."

		//TODO: Google Code redirect!
		// Details: http://code.google.com/p/smhasher/wiki/MurmurHash3
		// Reference Implementation: http://code.google.com/p/smhasher/source/browse/trunk/MurmurHash3.cpp
		// Benchs & Collisions: http://code.google.com/p/smhasher/wiki/MurmurHash3_x86_32

		private const uint C1 = 0xCC9E2D51;
		private const uint C2 = 0x1B873593;

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static uint FromBytes(byte[] bytes)
		{
			unsafe
			{
				//note: si bytes.Length == 0, pBytes sera null!
				fixed (byte* pBytes = bytes)
				{
					return ContinueUnsafe(0U, pBytes, bytes.Length);
				}
			}
		}

		[Pure]
		public static uint FromBytes(ReadOnlySpan<byte> bytes)
		{
			unsafe
			{
				fixed (byte* pBytes = &MemoryMarshal.GetReference(bytes))
				{
					return ContinueUnsafe(0U, pBytes, bytes.Length);
				}
			}
		}

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static uint FromBytes(Slice bytes)
		{
			unsafe
			{
				fixed (byte* pBytes = &bytes.DangerousGetPinnableReference())
				{
					return ContinueUnsafe(0U, pBytes, bytes.Count);
				}
			}
		}

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static unsafe uint FromBytesUnsafe(byte* bytes, int count)
		{
			return ContinueUnsafe(0U, bytes, count);
		}

		public static uint Continue(uint hash, byte[] bytes)
		{
			unsafe
			{
				//note: si bytes.Length == 0, pBytes sera null!
				fixed (byte* pBytes = bytes)
				{
					return ContinueUnsafe(hash, pBytes, bytes.Length);
				}
			}
		}

		public static uint Continue(uint hash, ReadOnlySpan<byte> bytes)
		{
			unsafe
			{
				fixed (byte* pBytes = &MemoryMarshal.GetReference(bytes))
				{
					return ContinueUnsafe(hash, pBytes, bytes.Length);
				}
			}
		}

		public static uint Continue(uint hash, Slice buffer)
		{
			unsafe
			{
				fixed (byte* pBytes = &buffer.DangerousGetPinnableReference())
				{
					return ContinueUnsafe(hash, pBytes, buffer.Count);
				}
			}
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static unsafe uint ContinueUnsafe(uint hash, byte* bytes, int count)
		{
			if (bytes == null && count != 0) ThrowHelper.ThrowArgumentNullException(nameof(bytes));
			if (count < 0) ThrowHelper.ThrowArgumentOutOfRangeException(nameof(count));
			// BODY

			if (count > 0)
			{
				Contract.Debug.Assert(bytes != null);
				uint* bp = (uint*)bytes;
				uint k;

				int n = count >> 2;
				while (n-- > 0)
				{
					k = (*bp++) * C1;
					k = (k << 15) | (k >> (32 - 15)); // ROTL32(k1, 15)
					k *= C2;

					hash ^= k;
					hash = (hash << 13) | (hash >> (32 - 13)); // ROTL32(hash, 13)
					hash = (hash * 5) + 0xE6546B64;
				}

				// TAIL

				n = count & 3;
				if (n > 0)
				{
					byte* tail = (byte*)bp;

					k = tail[0];
					k |= (n >= 2) ? (uint)(tail[1] << 8) : 0U;
					k |= (n == 3) ? (uint)(tail[2] << 16) : 0U;

					k *= C1;
					k = (k << 15) | (k >> (32 - 15)); //ROTL32(k1, 15);
					k *= C2;

					hash ^= k;
				}
			}

			// finalization
			hash ^= (uint)count;

			// Finalization mix - force all bits of a hash block to avalanche
			hash ^= hash >> 16;
			hash *= 0x85EBCA6B;
			hash ^= hash >> 13;
			hash *= 0xC2B2AE35;
			hash ^= hash >> 16;

			return hash;
		}

		public static int GetHashCode(byte[] bytes)
		{
			return bytes != null ? (int) (Continue(0U, bytes) & int.MaxValue) : -1;
		}

		public static int GetHashCode(Slice bytes)
		{
			return !bytes.IsNull ? (int) (Continue(0U, bytes) & int.MaxValue) : -1;
		}

		public static int GetHashCode(ReadOnlySpan<byte> bytes)
		{
			return (int) (Continue(0U, bytes) & int.MaxValue);
		}

	}

}
