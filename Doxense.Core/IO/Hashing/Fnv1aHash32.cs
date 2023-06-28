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
	using System.Text;
	using System.Runtime.InteropServices;
	using Doxense.Diagnostics.Contracts;
	using Doxense.Memory;
	using JetBrains.Annotations;

	/// <summary>Calcul de hash FNV-1a sur 32 bits (Fowler�Noll�Vo "Alternatif")</summary>
	/// <remarks>IMPORTANT: Ce hash n'est PAS cryptographique ! Il peut leaker des informations sur les donn�es hash�es, et ne doit donc pas �tre utilis� publiquement dans un scenario de protection de donn�es! (il faut plutot utiliser SHA ou HMAC pour ce genre de choses)</remarks>
	[PublicAPI]
	// ReSharper disable once InconsistentNaming
	public static class Fnv1aHash32
	{

		// La diff�rence entre FNV-1a et FNV-1 c'est que l'ordre des op�rations (MUL et XOR) est invers� !

		/*
			hash = FNV_offset_basis
			for each octet_of_data to be hashed
				hash = hash XOR octet_of_data
				hash = hash * FNV_prime
			return hash
		 */

		public const uint FNV1_32_OFFSET_BASIS = 2166136261;
		public const uint FNV1_32_PRIME = 16777619;

		/// <summary>Calcul le FNV-1a Hash 32-bit d'un cha�ne (encod�e en UTF-8 par d�faut)</summary>
		/// <param name="text">Cha�ne de texte � convertir</param>
		/// <param name="ignoreCase">Si true, la cha�ne est convertie en minuscule avant le calcul</param>
		/// <param name="encoding"></param>
		/// <returns>Code FNV-1a 32 bit calcul� sur la repr�sentation UTF-8 de la cha�ne. Attention: N'est pas garantit unique!</returns>
		public static uint FromString(string text, bool ignoreCase = false, Encoding? encoding = null)
		{
			if (string.IsNullOrEmpty(text)) return 0;
			if (ignoreCase) text = text.ToLowerInvariant();
			byte[] bytes = (encoding ?? Encoding.UTF8).GetBytes(text);
			return Continue(FNV1_32_OFFSET_BASIS, bytes);
		}

		/// <summary>Calcul le FNV-1a Hash 32-bit d'un bloc de donn�es</summary>
		/// <param name="bytes">Bloc de donn�es</param>
		/// <returns>Hash 32 bit calcul� sur l'int�gralit� du tableau</returns>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static uint FromBytes(byte[] bytes)
		{
			return Continue(FNV1_32_OFFSET_BASIS, bytes);
		}

		/// <summary>Calcul le FNV-1a Hash 32-bit d'un bloc de donn�es</summary>
		/// <param name="bytes">Bloc de donn�es</param>
		/// <returns>Hash 32 bit calcul� sur l'int�gralit� du tableau</returns>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static uint FromBytes(ReadOnlySpan<byte> bytes)
		{
			return Continue(FNV1_32_OFFSET_BASIS, bytes);
		}

		/// <summary>Calcul le FNV-1a Hash 32-bit d'un bloc de donn�es</summary>
		/// <param name="bytes">Bloc de donn�es</param>
		/// <returns>Hash 32 bit calcul� sur l'int�gralit� du tableau</returns>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static uint FromBytes(Slice bytes)
		{
			return Continue(FNV1_32_OFFSET_BASIS, bytes);
		}

		/// <summary>Calcul le FNV-1a Hash 32-bit d'un bloc de donn�es</summary>
		/// <param name="seed">Seed initiale (si diff�rente de 0)</param>
		/// <param name="bytes">Bloc de donn�es</param>
		/// <returns>Hash 32 bit calcul� sur l'int�gralit� du tableau</returns>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static uint FromBytes(uint seed, byte[] bytes)
		{
			return Continue(seed ^ FNV1_32_OFFSET_BASIS, bytes);
		}

		/// <summary>Calcul le Hash FNV-1a sur 32 bit sur un segment de donn�es</summary>
		/// <param name="buffer">Pointeur vers des donn�es en m�moire</param>
		/// <param name="count">Nombre de donn�es du buffer � hasher</param>
		/// <returns>Hash de la section du buffer</returns>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static unsafe uint FromBytesUnsafe(byte* buffer, int count)
		{
			return ContinueUnsafe(FNV1_32_OFFSET_BASIS, buffer, count);
		}

		/// <summary>Continue le calcul du FNV-1a Hash 32-bit</summary>
		/// <param name="bytes">Nouveau bloc de donn�es</param>
		/// <param name="hash">Valeur pr�c�dente du hash calcul� jusqu'a pr�sent</param>
		/// <returns>Nouvelle valeur du hash incluant le dernier bloc</returns>
		/// <remarks>Le premier bloc doit �tre calcul� avec Fnv1Hash32.FromBytesAlternative (pour d�marrer la chaine)</remarks>
		[Pure]
		public static uint Continue(uint hash, byte[] bytes)
		{
			if (bytes == null || bytes.Length == 0) return hash;

			// apr�s pas mal de bench, le bon vieux foreach(...) est presque aussi rapide qu'unroller des boucles en mode unsafe, car il est optimis� par le JIT (qui semble unroller lui meme)
			// sur mon Core i7 @ 3.4 Ghz, je suis a une moyenne de 3.1-3.4 cycles/byte en unrolled, versus 3.7-3.9 cycles/byte avec le foreach...

			foreach (var b in bytes)
			{
				hash = (hash ^ b) * FNV1_32_PRIME;
			}
			return hash;
		}

		[Pure]
		public static uint Continue(uint hash, ReadOnlySpan<byte> bytes)
		{
			if (bytes.Length == 0) return hash;
			unsafe
			{
				fixed (byte* pBytes = &MemoryMarshal.GetReference(bytes))
				{
					return ContinueUnsafe(hash, pBytes, bytes.Length);
				}
			}
		}

		[Pure]
		public static uint Continue(uint hash, Slice bytes)
		{
			if (bytes.Count == 0) return hash;
			unsafe
			{
				fixed (byte* pBytes = &bytes.DangerousGetPinnableReference())
				{
					return ContinueUnsafe(hash, pBytes, bytes.Count);
				}
			}
		}

		[Pure]
		public static unsafe uint ContinueUnsafe(uint hash, byte* bytes, int count)
		{
			if (count == 0 || bytes == null) return hash;
			if (count < 0) throw Contract.FailArgumentNotPositive(nameof(count));

			byte* bp = bytes;
			byte* be = bytes + count;
			//TODO: unroll?
			while (bp < be)
			{
				hash = (hash ^ (*bp++)) * FNV1_32_PRIME;
			}
			return hash;
		}

		/// <summary>Continue le calcul d'un FNV-1 Hash 32-bit sur une section d'un buffer</summary>
		/// <param name="hash">Valeur pr�c�d�nte du hash</param>
		/// <param name="buffer">Buffer contenant des donn�es � hasher</param>
		/// <param name="offset">Offset de d�part dans le buffer</param>
		/// <param name="count">Nombre de donn�es du buffer � hasher</param>
		/// <returns>Nouvelle valeur du hash</returns>
		public static uint Continue(uint hash, byte[] buffer, int offset, int count)
		{
			if (count == 0 || buffer == null) return hash;

			// note: ces tests sont la pour convaincre le JIT de d�sactiver les checks et unroller la boucle plus bas !!!
			if (offset < 0 || offset >= buffer.Length) throw ThrowHelper.ArgumentException(nameof(offset), "Offset must be within the buffer");
			int end = offset + count;
			if (end < 0 || end > buffer.Length) throw ThrowHelper.ArgumentException(nameof(buffer), "The buffer does not have enough data for the specified byte count");

			for (int i = offset; i < end; i++)
			{
				hash = (hash ^ buffer[i]) * FNV1_32_PRIME;
			}

			return hash;
		}

		/// <summary>Ajoute un byte au hash</summary>
		/// <param name="hash">Valeur pr�c�dente du hash</param>
		/// <param name="x">unsigned int8</param>
		/// <returns>Nouvelle valeur du hash</returns>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static uint Continue(uint hash, byte x)
		{
			return (hash ^ x) * FNV1_32_PRIME;
		}

		/// <summary>G�n�re le HashCode correspondant � un tableau de bytes</summary>
		/// <returns>Hashcode calcul� sur la base du hash FNV1 du buffer, et m�lang� pour assurer le plus de diffusion possible des bits</returns>
		public static int GetHashCode(byte[] buffer)
		{
			if (buffer == null) return -1;
			if (buffer.Length == 0) return 0;

			// cf : http://bretm.home.comcast.net/~bretm/hash/6.html
			// on peut utiliser un hash FNV1, a condition de mixer les bits � la fin

			return Diffuse(Continue(FNV1_32_PRIME, buffer));
		}

		/// <summary>G�n�re le HashCode correspondant � un tableau de bytes</summary>
		/// <returns>Hashcode calcul� sur la base du hash FNV1 du buffer, et m�lang� pour assurer le plus de diffusion possible des bits</returns>
		[Pure]
		public static int GetHashCode(byte[] buffer, int offset, int count)
		{
			Contract.NotNull(buffer);
			if (count == 0) return 0;

			// on peut utiliser un hash FNV1, a condition de mixer les bits � la fin
			// cf : http://bretm.home.comcast.net/~bretm/hash/6.html

			return Diffuse(Continue(FNV1_32_PRIME, buffer, offset, count));
		}

		/// <summary>Convertit un FNV-1 Hash 32-bit en un Hashcode utilisable avec un Dictionary, ou un IEqualityComparer</summary>
		/// <param name="hash">FNV-1 Hash 32 bit (calcul� avec FromBytes, par exemple)</param>
		/// <returns>Version utilisable dans un algorithme de type Hashtable</returns>
		[Pure]
		public static int Diffuse(uint hash)
		{
			// cf Figure 4: http://bretm.home.comcast.net/~bretm/hash/6.html
			hash += hash << 13;
			hash ^= hash >> 7;
			hash += hash << 3;
			hash ^= hash >> 17;
			hash += hash << 5;
			return (int)hash;
		}

	}

}
