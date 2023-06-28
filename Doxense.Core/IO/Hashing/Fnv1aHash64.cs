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
	using System.Text;
	using Doxense.Diagnostics.Contracts;
	using Doxense.Memory;
	using JetBrains.Annotations;

	/// <summary>Calcul de hash FNV-1a sur 64 bits (Fowler–Noll–Vo "Alternatif")</summary>
	/// <remarks>IMPORTANT: Ce hash n'est PAS cryptographique ! Il peut leaker des informations sur les données hashées, et ne doit donc pas être utilisé publiquement dans un scenario de protection de données! (il faut plutot utiliser SHA ou HMAC pour ce genre de choses)</remarks>
	[PublicAPI]
	// ReSharper disable once InconsistentNaming
	public static class Fnv1aHash64
	{

		// La différence entre FNV-1a et FNV-1 c'est que l'ordre des opérations (MUL et XOR) est inversé !

		/* 
			hash = FNV_offset_basis
			for each octet_of_data to be hashed
				hash = hash XOR octet_of_data
				hash = hash * FNV_prime
			return hash
		 */

		public const ulong FNV1_64_OFFSET_BASIS = 14695981039346656037;
		public const ulong FNV1_64_PRIME = 1099511628211;

		/// <summary>Calcul le FNV-1a ("Alternative") Hash 64-bit d'un chaîne</summary>
		/// <param name="text">Chaîne de texte à convertir</param>
		/// <param name="ignoreCase">Si true, la chaîne est convertie en minuscule avant le calcul</param>
		/// <param name="encoding">Encoding (optionel, sinon utilise UTF-8 par défaut)</param>
		/// <returns>Code FNV-1a 64 bit calculé sur la représentation binaire de la chaîne. Attention: N'est pas garantit unique!</returns>
		public static ulong FromString(string text, bool ignoreCase, Encoding? encoding = null)
		{
			if (string.IsNullOrEmpty(text)) return 0;
			if (ignoreCase) text = text.ToLowerInvariant();

			encoding = encoding ?? Encoding.UTF8;

			int count = encoding.GetByteCount(text);
			if (count <= 4096)
			{ // use the stack for the temporary buffer
				unsafe
				{
					//REVIEW: TODO: use Span<char>!
					byte* tmp = stackalloc byte[count];
					fixed (char* chars = text)
					{
						if (Encoding.UTF8.GetBytes(chars, text.Length, tmp, count) != count) throw new InvalidOperationException();
						return FromBytesUnsafe(tmp, count);
					}
				}
			}
			else
			{ // use the heap
				byte[] bytes = Encoding.UTF8.GetBytes(text);
				return FromBytes(bytes);
			}
		}

		/// <summary>Calcul le Hash FNV-1a ("Alternative") sur 64 bit d'un bloc de données</summary>
		/// <param name="bytes">Bloc de données</param>
		/// <returns>Hash 64 bit calculé sur l'intégralité du tableau</returns>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static ulong FromBytes(byte[] bytes)
		{
			return Continue(FNV1_64_OFFSET_BASIS, bytes);
		}

		/// <summary>Calcul le Hash FNV-1a ("Alternative") sur 64 bit d'un bloc de données</summary>
		/// <param name="bytes">Bloc de données</param>
		/// <returns>Hash 64 bit calculé sur l'intégralité du tableau</returns>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static ulong FromBytes(ReadOnlySpan<byte> bytes)
		{
			return Continue(FNV1_64_OFFSET_BASIS, bytes);
		}

		/// <summary>Calcul le Hash FNV-1a ("Alternative") sur 64 bit d'un bloc de données</summary>
		/// <param name="bytes">Bloc de données</param>
		/// <returns>Hash 64 bit calculé sur l'intégralité du tableau</returns>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static ulong FromBytes(Slice bytes)
		{
			return Continue(FNV1_64_OFFSET_BASIS, bytes);
		}

		/// <summary>Calcul le Hash FNV-1a sur 64 bit sur un segment de données</summary>
		/// <param name="buffer">Pointeur vers des données en mémoire</param>
		/// <param name="count">Nombre de données du buffer à hasher</param>
		/// <returns>Hash de la section du buffer</returns>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static unsafe ulong FromBytesUnsafe(byte* buffer, int count)
		{
			return ContinueUnsafe(FNV1_64_OFFSET_BASIS, buffer, count);
		}

		/// <summary>Continue le calcul du Hash FNV-1a ("Alternative") sur 64 bit avec un nouveau bloc de données</summary>
		/// <param name="hash">Valeur précédente du hash calculé jusqu'a présent</param>
		/// <param name="bytes">Nouveau bloc de données</param>
		/// <returns>Nouvelle valeur du hash incluant le dernier bloc</returns>
		/// <remarks>Le premier bloc doit être calculé avec Fnv1Hash64.FromBytesAlternative (pour démarrer la chaine)</remarks>
		[Pure]
		public static ulong Continue(ulong hash, byte[] bytes)
		{
			if (bytes == null || bytes.Length == 0) return hash;

			// après pas mal de bench, le bon vieux foreach(...) est presque aussi rapide qu'unroller des boucles en mode unsafe, car il est optimisé par le JIT (qui semble unroller lui meme)
			// sur mon Core i7 @ 3.4 Ghz, je suis a une moyenne de 3.1-3.4 cycles/byte en unrolled, versus 3.7-3.9 cycles/byte avec le foreach...

			foreach (var b in bytes)
			{
				hash = (hash ^ b) * FNV1_64_PRIME;
			}
			return hash;
		}

		/// <summary>Continue le calcul d'un FNV-1 Hash 64-bit sur un nouveau segment de données</summary>
		/// <param name="hash">Valeur précédénte du hash</param>
		/// <param name="bytes">Segment de données en mémoire</param>
		/// <returns>Nouvelle valeur du hash</returns>
		[Pure]
		public static ulong Continue(ulong hash, ReadOnlySpan<byte> bytes)
		{
			unsafe
			{
				fixed (byte* pBytes = &MemoryMarshal.GetReference(bytes))
				{
					return ContinueUnsafe(hash, pBytes, bytes.Length);
				}
			}
		}

		/// <summary>Continue le calcul d'un FNV-1 Hash 64-bit sur un nouveau segment de données</summary>
		/// <param name="hash">Valeur précédénte du hash</param>
		/// <param name="bytes">Segment de données en mémoire</param>
		/// <returns>Nouvelle valeur du hash</returns>
		[Pure]
		public static ulong Continue(ulong hash, Slice bytes)
		{
			unsafe
			{
				fixed (byte* pBytes = &bytes.DangerousGetPinnableReference())
				{
					return ContinueUnsafe(hash, pBytes, bytes.Count);
				}
			}
		}

		/// <summary>Continue le calcul d'un FNV-1 Hash 64-bit sur un nouveau segment de données</summary>
		/// <param name="hash">Valeur précédénte du hash</param>
		/// <param name="bytes">Pointeur vers des données en mémoire</param>
		/// <param name="count">Nombre de données du buffer à hasher</param>
		/// <returns>Nouvelle valeur du hash</returns>
		[Pure]
		public static unsafe ulong ContinueUnsafe(ulong hash, byte* bytes, int count)
		{
			if (count == 0 || bytes == null) return hash;
			if (count < 0) throw ThrowHelper.ArgumentException(nameof(count), "Count must be a positive integer");

			byte* bp = bytes;
			byte* be = bytes + count;
			//TODO: unroll?
			while (bp < be)
			{
				hash = (hash ^ (*bp++)) * FNV1_64_PRIME;
			}

			return hash;
		}

	}

}
