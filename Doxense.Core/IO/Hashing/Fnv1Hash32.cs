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

namespace Doxense.IO.Hashing
{
	using System.Text;

	/// <summary>Calcul de hash FNV-1 sur 32 bits (FowlerNollVo)</summary>
	/// <remarks>IMPORTANT: Ce hash n'est PAS cryptographique ! Il peut leaker des informations sur les données hashées, et ne doit donc pas être utilisé publiquement dans un scenario de protection de données! (il faut plutot utiliser SHA ou HMAC pour ce genre de choses)</remarks>
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

		/// <summary>Calcul le FNV-1 Hash 32-bit d'un chaîne</summary>
		/// <param name="text">Chaîne de texte à convertir</param>
		/// <param name="ignoreCase">Si true, la chaîne est convertie en minuscule avant le calcul</param>
		/// <param name="encoding"></param>
		/// <returns>Code FNV-1 32 bit calculé sur la représentation UTF-8 de la chaîne. Attention: N'est pas garantit unique!</returns>
		public static uint FromString(string text, bool ignoreCase = false, Encoding? encoding = null)
		{
			if(string.IsNullOrEmpty(text)) return 0;
			if (ignoreCase) text = text.ToLowerInvariant();
			byte[] bytes = (encoding ?? Encoding.UTF8).GetBytes(text);
			return FromBytes(bytes);
		}

		/// <summary>Calcul le FNV-1 Hash 32-bit d'un chaîne (encodée en UTF-8 par défaut)</summary>
		/// <returns>Code FNV-1 32 bit calculé sur la représentation UTF-8 (par défaut) de la chaîne. Attention: N'est pas garantit unique!</returns>
		public static uint FromChars(char[]? buffer, int offset, int count, Encoding? encoding = null)
		{
			if (count == 0 || buffer == null) return 0;
			var bytes = (encoding ?? Encoding.UTF8).GetBytes(buffer, offset, count);
			return Continue(FNV1_32_OFFSET_BASIS, bytes);
		}

		/// <summary>Calcul le FNV-1 Hash 32-bit d'un bloc de données</summary>
		/// <param name="bytes">Bloc de données</param>
		/// <returns>Hash 32 bit calculé sur l'intégralité du tableau</returns>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static uint FromBytes(byte[] bytes)
		{
			return Continue(FNV1_32_OFFSET_BASIS, bytes);
		}

		/// <summary>Calcul le FNV-1 Hash 32-bit d'un bloc de données</summary>
		/// <param name="seed">Seed initiale (si différente de 0)</param>
		/// <param name="bytes">Bloc de données</param>
		/// <returns>Hash 32 bit calculé sur l'intégralité du tableau</returns>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static uint FromBytes(uint seed, byte[] bytes)
		{
			return Continue(seed ^ FNV1_32_OFFSET_BASIS, bytes);
		}

		/// <summary>Calcul le FNV-1 Hash 32-bit d'un bloc de données</summary>
		/// <param name="bytes">Buffer contenant des données à hasher</param>
		/// <param name="offset">Offset de départ dans le buffer</param>
		/// <param name="count">Nombre de données du buffer à hasher</param>
		/// <returns>Hash de la section du buffer</returns>
		public static uint FromBytes(byte[] bytes, int offset, int count)
		{
			if (offset == 0 && count == bytes.Length)
				return Continue(FNV1_32_OFFSET_BASIS, bytes);
			else
				return Continue(FNV1_32_OFFSET_BASIS, bytes, offset, count);
		}

		/// <summary>Continue le calcul du FNV-1 Hash 32-bit</summary>
		/// <param name="hash">Valeur précédente du hash calculé jusqu'a présent</param>
		/// <param name="bytes">Nouveau bloc de données</param>
		/// <returns>Nouvelle valeur du hash incluant le dernier bloc</returns>
		/// <remarks>Le premier bloc doit être calculé avec Fnv1Hash32.FromBytes (pour démarrer la chaine)</remarks>
		public static uint Continue(uint hash, byte[]? bytes)
		{
			if (bytes == null || bytes.Length == 0) return hash;

			// après pas mal de bench, le bon vieux foreach(...) est presque aussi rapide qu'unroller des boucles en mode unsafe, car il est optimisé par le JIT (qui semble unroller lui meme)
			// sur mon Core i7 @ 3.4 Ghz, je suis a une moyenne de 3.1-3.4 cycles/byte en unrolled, versus 3.7-3.9 cycles/byte avec le foreach...

			foreach (var b in bytes)
			{
				hash = (hash * FNV1_32_PRIME) ^ b;
			}
			return hash;
		}

		/// <summary>Continue le calcul d'un FNV-1 Hash 32-bit sur une section d'un buffer</summary>
		/// <param name="hash">Valeur précédénte du hash</param>
		/// <param name="buffer">Buffer contenant des données à hasher</param>
		/// <param name="offset">Offset de départ dans le buffer</param>
		/// <param name="count">Nombre de données du buffer à hasher</param>
		/// <returns>Nouvelle valeur du hash</returns>
		public static uint Continue(uint hash, byte[] buffer, int offset, int count)
		{
			Contract.DoesNotOverflow(buffer, offset, count);
			if (count == 0) return hash;

			// note: ces tests sont la pour convaincre le JIT de désactiver les checks et unroller la boucle plus bas !!!
			if (offset < 0 || offset >= buffer.Length) throw ThrowHelper.ArgumentException(nameof(offset), "Offset must be within the buffer");
			int end = offset + count;
			if (end < 0 || end > buffer.Length) throw ThrowHelper.ArgumentException(nameof(buffer), "The buffer does not have enough data for the specified byte count");

			for(int i = offset; i < end; i++)
			{
				hash = (hash * FNV1_32_PRIME) ^ buffer[i];
			}

			return hash;
		}

		/// <summary>Ajoute une chaîne au hash</summary>
		/// <param name="hash">Valeur précédente du hash</param>
		/// <param name="text">string</param>
		/// <param name="ignoreCase">Si true, la chaîne est convertie en minuscule avant le calcul</param>
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
		/// <param name="hash">Valeur précédente du hash</param>
		/// <param name="x">unsigned int8</param>
		/// <returns>Nouvelle valeur du hash</returns>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static uint Continue(uint hash, byte x)
		{
			return (hash * FNV1_32_PRIME) ^ x;
		}

		/// <summary>Ajoute un Int16 au hash</summary>
		/// <param name="hash">Valeur précédente du hash</param>
		/// <param name="x">signed int16 (little endian)</param>
		/// <returns>Nouvelle valeur du hash</returns>
		public static uint Continue(uint hash, short x)
		{
			hash = (hash * FNV1_32_PRIME) ^ ((byte)x);
			hash = (hash * FNV1_32_PRIME) ^ ((byte)(x >> 8));
			return hash;
		}

		/// <summary>Ajoute un UInt16 au hash</summary>
		/// <param name="hash">Valeur précédente du hash</param>
		/// <param name="x">unsigned int16 (little endian)</param>
		/// <returns>Nouvelle valeur du hash</returns>
		public static uint Continue(uint hash, ushort x)
		{
			hash = (hash * FNV1_32_PRIME) ^ ((byte)x);
			hash = (hash * FNV1_32_PRIME) ^ ((byte)(x >> 8));
			return hash;
		}

		/// <summary>Ajoute un Int32 au hash</summary>
		/// <param name="hash">Valeur précédente du hash</param>
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
		/// <param name="hash">Valeur précédente du hash</param>
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
		/// <param name="hash">Valeur précédente du hash</param>
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
		/// <param name="hash">Valeur précédente du hash</param>
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
		/// <summary>Retourne un stream qui calcule le hash FNV1 32 bits des données lues ou écrites sur un stream</summary>
		/// <param name="stream">Stream sur lequel les données seront lues ou écrites</param>
		/// <param name="leaveOpen">Si true, laisse le stream ouvert lorsque ce stream est Disposed</param>
		/// <returns>Stream qui transmet toutes les opérations au stream spécifié, et met à jour le hash à chaque opération de lecture ou écriture</returns>
		/// <remarks>Note: ne pas oublier d'appeler Reset() après chaque opération de Seek en mode lecture, pour reset le hash !</remarks>
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
