#region Copyright Doxense 2005-2015
//
// All rights are reserved. Reproduction or transmission in whole or in part, in
// any form or by any means, electronic, mechanical or otherwise, is prohibited
// without the prior written consent of the copyright owner.
//
#endregion

using System;
using System.Runtime.CompilerServices;
using System.Text;
using Doxense.Diagnostics.Contracts;

namespace Doxense.IO.Hashing
{
	using JetBrains.Annotations;

	/// <summary>Calcul de hash FNV-1 sur 32 bits (Fowler�Noll�Vo)</summary>
	/// <remarks>IMPORTANT: Ce hash n'est PAS cryptographique ! Il peut leaker des informations sur les donn�es hash�es, et ne doit donc pas �tre utilis� publiquement dans un scenario de protection de donn�es! (il faut plutot utiliser SHA ou HMAC pour ce genre de choses)</remarks>
	[PublicAPI]
	public static class Fnv1Hash32
	{
		// cf http://en.wikipedia.org/wiki/Fowler%E2%80%93Noll%E2%80%93Vo_hash_function
		// cf http://isthe.com/chongo/tech/comp/fnv/

		// benchs: je tourne a environ 830 Mo/sec sur un thread de mon Core i7 @3.4 Ghz (environ 4 cycles cpu par octets)

		public const uint FNV1_32_OFFSET_BASIS = 2166136261;
		public const uint FNV1_32_PRIME = 16777619;

		/*
			hash = FNV_offset_basis
			for each octet_of_data to be hashed
				hash = hash * FNV_prime
				hash = hash XOR octet_of_data
			return hash
		 */

		/// <summary>Calcul le FNV-1 Hash 32-bit d'un cha�ne</summary>
		/// <param name="text">Cha�ne de texte � convertir</param>
		/// <param name="ignoreCase">Si true, la cha�ne est convertie en minuscule avant le calcul</param>
		/// <param name="encoding"></param>
		/// <returns>Code FNV-1 32 bit calcul� sur la repr�sentation UTF-8 de la cha�ne. Attention: N'est pas garantit unique!</returns>
		public static uint FromString(string text, bool ignoreCase = false, Encoding? encoding = null)
		{
			if(string.IsNullOrEmpty(text)) return 0;
			if (ignoreCase) text = text.ToLowerInvariant();
			byte[] bytes = (encoding ?? Encoding.UTF8).GetBytes(text);
			return FromBytes(bytes);
		}

		/// <summary>Calcul le FNV-1 Hash 32-bit d'un cha�ne (encod�e en UTF-8 par d�faut)</summary>
		/// <returns>Code FNV-1 32 bit calcul� sur la repr�sentation UTF-8 (par d�faut) de la cha�ne. Attention: N'est pas garantit unique!</returns>
		public static uint FromChars(char[] buffer, int offset, int count, Encoding? encoding = null)
		{
			if (count == 0 || buffer == null) return 0;
			var bytes = (encoding ?? Encoding.UTF8).GetBytes(buffer, offset, count);
			return Continue(FNV1_32_OFFSET_BASIS, bytes);
		}

		/// <summary>Calcul le FNV-1 Hash 32-bit d'un bloc de donn�es</summary>
		/// <param name="bytes">Bloc de donn�es</param>
		/// <returns>Hash 32 bit calcul� sur l'int�gralit� du tableau</returns>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static uint FromBytes(byte[] bytes)
		{
			return Continue(FNV1_32_OFFSET_BASIS, bytes);
		}

		/// <summary>Calcul le FNV-1 Hash 32-bit d'un bloc de donn�es</summary>
		/// <param name="seed">Seed initiale (si diff�rente de 0)</param>
		/// <param name="bytes">Bloc de donn�es</param>
		/// <returns>Hash 32 bit calcul� sur l'int�gralit� du tableau</returns>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static uint FromBytes(uint seed, byte[] bytes)
		{
			return Continue(seed ^ FNV1_32_OFFSET_BASIS, bytes);
		}

		/// <summary>Calcul le FNV-1 Hash 32-bit d'un bloc de donn�es</summary>
		/// <param name="bytes">Buffer contenant des donn�es � hasher</param>
		/// <param name="offset">Offset de d�part dans le buffer</param>
		/// <param name="count">Nombre de donn�es du buffer � hasher</param>
		/// <returns>Hash de la section du buffer</returns>
		public static uint FromBytes(byte[] bytes, int offset, int count)
		{
			if (offset == 0 && count == bytes.Length)
				return Continue(FNV1_32_OFFSET_BASIS, bytes);
			else
				return Continue(FNV1_32_OFFSET_BASIS, bytes, offset, count);
		}

		/// <summary>Continue le calcul du FNV-1 Hash 32-bit</summary>
		/// <param name="hash">Valeur pr�c�dente du hash calcul� jusqu'a pr�sent</param>
		/// <param name="bytes">Nouveau bloc de donn�es</param>
		/// <returns>Nouvelle valeur du hash incluant le dernier bloc</returns>
		/// <remarks>Le premier bloc doit �tre calcul� avec Fnv1Hash32.FromBytes (pour d�marrer la chaine)</remarks>
		public static uint Continue(uint hash, byte[] bytes)
		{
			if (bytes == null || bytes.Length == 0) return hash;

			// apr�s pas mal de bench, le bon vieux foreach(...) est presque aussi rapide qu'unroller des boucles en mode unsafe, car il est optimis� par le JIT (qui semble unroller lui meme)
			// sur mon Core i7 @ 3.4 Ghz, je suis a une moyenne de 3.1-3.4 cycles/byte en unrolled, versus 3.7-3.9 cycles/byte avec le foreach...

			foreach (var b in bytes)
			{
				hash = (hash * FNV1_32_PRIME) ^ b;
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
			Contract.DoesNotOverflow(buffer, offset, count);
			if (count == 0) return hash;

			// note: ces tests sont la pour convaincre le JIT de d�sactiver les checks et unroller la boucle plus bas !!!
			if (offset < 0 || offset >= buffer.Length) throw ThrowHelper.ArgumentException("offset", "Offset must be within the buffer");
			int end = offset + count;
			if (end < 0 || end > buffer.Length) throw ThrowHelper.ArgumentException("buffer", "The buffer does not have enough data for the specified byte count");

			for(int i = offset; i < end; i++)
			{
				hash = (hash * FNV1_32_PRIME) ^ buffer[i];
			}

			return hash;
		}

		/// <summary>Ajoute une cha�ne au hash</summary>
		/// <param name="hash">Valeur pr�c�dente du hash</param>
		/// <param name="text">string</param>
		/// <param name="ignoreCase">Si true, la cha�ne est convertie en minuscule avant le calcul</param>
		/// <param name="encoding"></param>
		/// <returns>Nouvelle valeur du hash</returns>
		[Obsolete]
		public static uint Continue(uint hash, string text, bool ignoreCase = false, Encoding? encoding = null)
		{
			if (string.IsNullOrEmpty(text)) return 0;
			if (ignoreCase) text = text.ToLowerInvariant();
			byte[] bytes = (encoding ?? Encoding.UTF8).GetBytes(text);
			return Continue(hash, bytes);
		}


		/// <summary>Ajoute un byte au hash</summary>
		/// <param name="hash">Valeur pr�c�dente du hash</param>
		/// <param name="x">unsigned int8</param>
		/// <returns>Nouvelle valeur du hash</returns>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static uint Continue(uint hash, byte x)
		{
			return (hash * FNV1_32_PRIME) ^ x;
		}

		/// <summary>Ajoute un Int16 au hash</summary>
		/// <param name="hash">Valeur pr�c�dente du hash</param>
		/// <param name="x">signed int16 (little endian)</param>
		/// <returns>Nouvelle valeur du hash</returns>
		public static uint Continue(uint hash, short x)
		{
			hash = (hash * FNV1_32_PRIME) ^ ((byte)x);
			hash = (hash * FNV1_32_PRIME) ^ ((byte)(x >> 8));
			return hash;
		}

		/// <summary>Ajoute un UInt16 au hash</summary>
		/// <param name="hash">Valeur pr�c�dente du hash</param>
		/// <param name="x">unsigned int16 (little endian)</param>
		/// <returns>Nouvelle valeur du hash</returns>
		public static uint Continue(uint hash, ushort x)
		{
			hash = (hash * FNV1_32_PRIME) ^ ((byte)x);
			hash = (hash * FNV1_32_PRIME) ^ ((byte)(x >> 8));
			return hash;
		}

		/// <summary>Ajoute un Int32 au hash</summary>
		/// <param name="hash">Valeur pr�c�dente du hash</param>
		/// <param name="x">signed int32 (little endian)</param>
		/// <returns>Nouvelle valeur du hash</returns>
		public static uint Continue(uint hash, int x)
		{
			hash = (hash * FNV1_32_PRIME) ^ ((byte)x);
			x >>= 8;
			hash = (hash * FNV1_32_PRIME) ^ ((byte)x);
			x >>= 8;
			hash = (hash * FNV1_32_PRIME) ^ ((byte)x);
			x >>= 8;
			hash = (hash * FNV1_32_PRIME) ^ ((byte)x);
			return hash;
		}

		/// <summary>Ajoute un UInt32 au hash</summary>
		/// <param name="hash">Valeur pr�c�dente du hash</param>
		/// <param name="x">unsigned int32 (little endian)</param>
		/// <returns>Nouvelle valeur du hash</returns>
		public static uint Continue(uint hash, uint x)
		{
			hash = (hash * FNV1_32_PRIME) ^ ((byte)x);
			x >>= 8;
			hash = (hash * FNV1_32_PRIME) ^ ((byte)x);
			x >>= 8;
			hash = (hash * FNV1_32_PRIME) ^ ((byte)x);
			x >>= 8;
			hash = (hash * FNV1_32_PRIME) ^ ((byte)x);
			return hash;
		}

		/// <summary>Ajoute un Int64 au hash</summary>
		/// <param name="hash">Valeur pr�c�dente du hash</param>
		/// <param name="x">signed int64 (little endian)</param>
		/// <returns>Nouvelle valeur du hash</returns>
		public static uint Continue(uint hash, long x)
		{
			hash = (hash * FNV1_32_PRIME) ^ ((byte)x);
			x >>= 8;
			hash = (hash * FNV1_32_PRIME) ^ ((byte)x);
			x >>= 8;
			hash = (hash * FNV1_32_PRIME) ^ ((byte)x);
			x >>= 8;
			hash = (hash * FNV1_32_PRIME) ^ ((byte)x);
			x >>= 8;
			hash = (hash * FNV1_32_PRIME) ^ ((byte)x);
			x >>= 8;
			hash = (hash * FNV1_32_PRIME) ^ ((byte)x);
			x >>= 8;
			hash = (hash * FNV1_32_PRIME) ^ ((byte)x);
			x >>= 8;
			hash = (hash * FNV1_32_PRIME) ^ ((byte)x);
			return hash;
		}

		/// <summary>Ajoute un UInt64 au hash</summary>
		/// <param name="hash">Valeur pr�c�dente du hash</param>
		/// <param name="x">unsigned int64 (little endian)</param>
		/// <returns>Nouvelle valeur du hash</returns>
		public static uint Continue(uint hash, ulong x)
		{
			hash = (hash * FNV1_32_PRIME) ^ ((byte)x);
			x >>= 8;
			hash = (hash * FNV1_32_PRIME) ^ ((byte)x);
			x >>= 8;
			hash = (hash * FNV1_32_PRIME) ^ ((byte)x);
			x >>= 8;
			hash = (hash * FNV1_32_PRIME) ^ ((byte)x);
			x >>= 8;
			hash = (hash * FNV1_32_PRIME) ^ ((byte)x);
			x >>= 8;
			hash = (hash * FNV1_32_PRIME) ^ ((byte)x);
			x >>= 8;
			hash = (hash * FNV1_32_PRIME) ^ ((byte)x);
			x >>= 8;
			hash = (hash * FNV1_32_PRIME) ^ ((byte)x);
			return hash;
		}

#if DEPRECATED
		/// <summary>Retourne un stream qui calcule le hash FNV1 32 bits des donn�es lues ou �crites sur un stream</summary>
		/// <param name="stream">Stream sur lequel les donn�es seront lues ou �crites</param>
		/// <param name="leaveOpen">Si true, laisse le stream ouvert lorsque ce stream est Disposed</param>
		/// <returns>Stream qui transmet toutes les op�rations au stream sp�cifi�, et met � jour le hash � chaque op�ration de lecture ou �criture</returns>
		/// <remarks>Note: ne pas oublier d'appeler Reset() apr�s chaque op�ration de Seek en mode lecture, pour reset le hash !</remarks>
		public static RollingCrcStream<uint> WrapStream(Stream stream, bool leaveOpen = false)
		{
			return new RollingCrcStream<uint>(
				stream,
				leaveOpen,
				reset: () => FNV1_32_OFFSET_BASIS,
				byteAdder: Fnv1Hash32.Continue, // (byte)
				chunkAdder: Fnv1Hash32.Continue // (byte[], int, int)
			);
		}
#endif

	}

}
