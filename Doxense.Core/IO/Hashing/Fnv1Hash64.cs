#region Copyright (c) 2005-2023 Doxense SAS
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
	using Doxense.Diagnostics.Contracts;
	using JetBrains.Annotations;

	/// <summary>Calcul de hash FNV-1 sur 64 bits (Fowler�Noll�Vo)</summary>
	/// <remarks>IMPORTANT: Ce hash n'est PAS cryptographique ! Il peut leaker des informations sur les donn�es hash�es, et ne doit donc pas �tre utilis� publiquement dans un scenario de protection de donn�es! (il faut plutot utiliser SHA ou HMAC pour ce genre de choses)</remarks>
	[PublicAPI]
	public static class Fnv1Hash64
	{
		// cf http://en.wikipedia.org/wiki/Fowler%E2%80%93Noll%E2%80%93Vo_hash_function
		// cf http://isthe.com/chongo/tech/comp/fnv/

		// benchs: je tourne a environ 830 Mo/sec sur un thread de mon Core i7 @3.4 Ghz (environ 4 cycles cpu par octets)

		public const ulong FNV1_64_OFFSET_BASIS = 14695981039346656037;
		public const ulong FNV1_64_PRIME = 1099511628211;

		/*
			hash = FNV_offset_basis
			for each octet_of_data to be hashed
				hash = hash * FNV_prime
				hash = hash XOR octet_of_data
			return hash
		 */

		/// <summary>Calcul le Hash FNV-1 64-bit d'un cha�ne</summary>
		/// <param name="text">Cha�ne de texte � convertir</param>
		/// <param name="ignoreCase">Si true, la cha�ne est convertie en minuscule avant le calcul</param>
		/// <returns>Code FNV-1 64 bit calcul� sur la repr�sentation UTF-8 de la cha�ne. Attention: N'est pas garantit unique!</returns>
		public static ulong FromString(string text, bool ignoreCase)
		{
			if (string.IsNullOrEmpty(text)) return 0;
			if (ignoreCase) text = text.ToLowerInvariant();
			byte[] bytes = Encoding.UTF8.GetBytes(text);
			return FromBytes(bytes);
		}

		/// <summary>Calcul le FNV-1 Hash 64-bit d'un cha�ne (encod�e en UTF-8 par d�faut)</summary>
		/// <returns>Code FNV-1 64 bit calcul� sur la repr�sentation UTF-8 (par d�faut) de la cha�ne. Attention: N'est pas garantit unique!</returns>
		public static ulong FromChars(char[] buffer, int offset, int count, Encoding? encoding = null)
		{
			Contract.DoesNotOverflow(buffer, offset, count);
			if (count == 0) return FNV1_64_OFFSET_BASIS;
			var bytes = (encoding ?? Encoding.UTF8).GetBytes(buffer, offset, count);
			return Continue(FNV1_64_OFFSET_BASIS, bytes);
		}

		/// <summary>Calcul le Hash FNV-1 64-bit d'un bloc de donn�es</summary>
		/// <param name="bytes">Bloc de donn�es</param>
		/// <returns>Hash 64 bit calcul� sur l'int�gralit� du tableau</returns>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static ulong FromBytes(byte[] bytes)
		{
			return Continue(FNV1_64_OFFSET_BASIS, bytes);
		}

		/// <summary>Calcul le Hash FNV-1 64-bit d'un bloc de donn�es</summary>
		/// <param name="bytes">Bloc de donn�es</param>
		/// <returns>Hash 64 bit calcul� sur l'int�gralit� du tableau</returns>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static ulong FromBytes(Slice bytes)
		{
			return Continue(FNV1_64_OFFSET_BASIS, bytes);
		}

		/// <summary>Calcul le Hash FNV-1 64 bit sur un segment de donn�es</summary>
		/// <param name="bytes">Buffer contenant des donn�es � hasher</param>
		/// <param name="offset">Offset de d�part dans le buffer</param>
		/// <param name="count">Nombre de donn�es du buffer � hasher</param>
		/// <returns>Hash de la section du buffer</returns>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static ulong FromBytes(byte[] bytes, int offset, int count)
		{
			return Continue(FNV1_64_OFFSET_BASIS, bytes, offset, count);
		}

		/// <summary>Continue le calcul du Hash FNV-1 64-bit</summary>
		/// <param name="hash">Valeur pr�c�dente du hash calcul� jusqu'a pr�sent</param>
		/// <param name="bytes">Nouveau bloc de donn�es</param>
		/// <returns>Nouvelle valeur du hash incluant le dernier bloc</returns>
		/// <remarks>Le premier bloc doit �tre calcul� avec Fnv1Hash64.FromBytes (pour d�marrer la chaine)</remarks>
		[Pure]
		public static ulong Continue(ulong hash, byte[] bytes)
		{
			Contract.Debug.Requires(bytes != null);
			if (bytes.Length == 0) return hash;

			// apr�s pas mal de bench, le bon vieux foreach(...) est presque aussi rapide qu'unroller des boucles en mode unsafe, car il est optimis� par le JIT (qui semble unroller lui meme)
			// sur mon Core i7 @ 3.4 Ghz, je suis a une moyenne de 3.1-3.4 cycles/byte en unrolled, versus 3.7-3.9 cycles/byte avec le foreach...

			foreach(var b in bytes)
			{
				hash = (hash * FNV1_64_PRIME) ^ b;
			}
			return hash;
		}

		/// <summary>Continue le calcul du Hash FNV-1 64-bit</summary>
		/// <param name="hash">Valeur pr�c�dente du hash calcul� jusqu'a pr�sent</param>
		/// <param name="bytes">Nouveau bloc de donn�es</param>
		/// <returns>Nouvelle valeur du hash incluant le dernier bloc</returns>
		/// <remarks>Le premier bloc doit �tre calcul� avec Fnv1Hash64.FromBytes (pour d�marrer la chaine)</remarks>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static ulong Continue(ulong hash, Slice bytes)
		{
			return Continue(hash, bytes.Array, bytes.Offset, bytes.Count);
		}

		/// <summary>Continue le calcul d'un Hash FNV-1 64-bit sur un nouveau segment de donn�es</summary>
		/// <param name="hash">Valeur pr�c�d�nte du hash</param>
		/// <param name="buffer">Buffer contenant des donn�es � hasher</param>
		/// <param name="offset">Offset de d�part dans le buffer</param>
		/// <param name="count">Nombre de donn�es du buffer � hasher</param>
		/// <returns>Nouvelle valeur du hash</returns>
		[Pure]
		public static ulong Continue(ulong hash, byte[] buffer, int offset, int count)
		{
			Contract.DoesNotOverflow(buffer, offset, count);
			if (count == 0) return hash;

			// note: ces tests sont la pour convaincre le JIT de d�sactiver les checks et unroller la boucle plus bas !!!
			if (offset < 0 || offset >= buffer.Length) ThrowHelper.ThrowArgumentException(nameof(offset), "Offset must be within the buffer");
			int end = offset + count;
			if (end < 0 || end > buffer.Length) ThrowHelper.ThrowArgumentException(nameof(buffer), "The buffer does not have enough data for the specified byte count");

			for (int i = offset; i < end; i++)
			{
				hash = (hash * FNV1_64_PRIME) ^ buffer[i];
			}

			return hash;
		}

		/// <summary>Calcul le Hash FNV-1 64 bit d'un Byte</summary>
		/// <param name="x">unsigned byte</param>
		/// <returns>Hash de la valeur (sur 64 bits)</returns>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static ulong FromByte(byte x)
		{
			return Continue(FNV1_64_OFFSET_BASIS, x);
		}

		/// <summary>Ajoute un byte au hash</summary>
		/// <param name="hash">Valeur pr�c�dente du hash</param>
		/// <param name="x">signed int16 (little endian)</param>
		/// <returns>Nouvelle valeur du hash</returns>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static ulong Continue(ulong hash, byte x)
		{
			return (hash * FNV1_64_PRIME) ^ x;
		}

		/// <summary>Calcul le Hash FNV-1 64 bit d'un Int16</summary>
		/// <param name="x">signed int16 (little endian)</param>
		/// <returns>Hash de la valeur (sur 64 bits)</returns>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static ulong FromInt16(short x)
		{
			return Continue(FNV1_64_OFFSET_BASIS, x);
		}

		/// <summary>Ajoute un Int16 au hash</summary>
		/// <param name="hash">Valeur pr�c�dente du hash</param>
		/// <param name="x">signed int16 (little endian)</param>
		/// <returns>Nouvelle valeur du hash</returns>
		public static ulong Continue(ulong hash, short x)
		{
			hash = (hash * FNV1_64_PRIME) ^ ((byte)x);
			hash = (hash * FNV1_64_PRIME) ^ ((byte)(x >> 8));
			return hash;
		}

		/// <summary>Calcul le Hash FNV-1 64 bit d'un UInt16</summary>
		/// <param name="x">unsigned int16 (little endian)</param>
		/// <returns>Hash de la valeur (sur 64 bits)</returns>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static ulong FromUInt16(ushort x)
		{
			return Continue(FNV1_64_OFFSET_BASIS, x);
		}

		/// <summary>Ajoute un UInt16 au hash</summary>
		/// <param name="hash">Valeur pr�c�dente du hash</param>
		/// <param name="x">unsigned int16 (little endian)</param>
		/// <returns>Nouvelle valeur du hash</returns>
		public static ulong Continue(ulong hash, ushort x)
		{
			hash = (hash * FNV1_64_PRIME) ^ ((byte)x);
			hash = (hash * FNV1_64_PRIME) ^ ((byte)(x >> 8));
			return hash;
		}

		/// <summary>Calcul le Hash FNV-1 64 bit d'un Int32</summary>
		/// <param name="x">signed int32 (little endian)</param>
		/// <returns>Hash de la valeur (sur 64 bits)</returns>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static ulong FromInt32(int x)
		{
			return Continue(FNV1_64_OFFSET_BASIS, x);
		}

		/// <summary>Ajoute un Int32 au hash</summary>
		/// <param name="hash">Valeur pr�c�dente du hash</param>
		/// <param name="x">signed int32 (little endian)</param>
		/// <returns>Nouvelle valeur du hash</returns>
		public static ulong Continue(ulong hash, int x)
		{
			hash = (hash * FNV1_64_PRIME) ^ ((byte)x);
			x >>= 8;
			hash = (hash * FNV1_64_PRIME) ^ ((byte)x);
			x >>= 8;
			hash = (hash * FNV1_64_PRIME) ^ ((byte)x);
			x >>= 8;
			hash = (hash * FNV1_64_PRIME) ^ ((byte)x);
			return hash;
		}

		/// <summary>Calcul le Hash FNV-1 64 bit d'un UInt32</summary>
		/// <param name="x">unsigned int32 (little endian)</param>
		/// <returns>Hash de la valeur (sur 64 bits)</returns>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static ulong FromUInt32(uint x)
		{
			return Continue(FNV1_64_OFFSET_BASIS, x);
		}

		/// <summary>Ajoute un UInt32 au hash</summary>
		/// <param name="hash">Valeur pr�c�dente du hash</param>
		/// <param name="x">unsigned int32 (little endian)</param>
		/// <returns>Nouvelle valeur du hash</returns>
		public static ulong Continue(ulong hash, uint x)
		{
			hash = (hash * FNV1_64_PRIME) ^ ((byte)x);
			x >>= 8;
			hash = (hash * FNV1_64_PRIME) ^ ((byte)x);
			x >>= 8;
			hash = (hash * FNV1_64_PRIME) ^ ((byte)x);
			x >>= 8;
			hash = (hash * FNV1_64_PRIME) ^ ((byte)x);
			return hash;
		}

		/// <summary>Calcul le Hash FNV-1 64 bit d'un Int64</summary>
		/// <param name="x">signed int64 (little endian)</param>
		/// <returns>Hash de la valeur (sur 64 bits)</returns>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static ulong FromInt64(long x)
		{
			return Continue(FNV1_64_OFFSET_BASIS, x);
		}

		/// <summary>Ajoute un Int64 au hash</summary>
		/// <param name="hash">Valeur pr�c�dente du hash</param>
		/// <param name="x">signed int64 (little endian)</param>
		/// <returns>Nouvelle valeur du hash</returns>
		public static ulong Continue(ulong hash, long x)
		{
			hash = (hash * FNV1_64_PRIME) ^ ((byte)x);
			x >>= 8;
			hash = (hash * FNV1_64_PRIME) ^ ((byte)x);
			x >>= 8;
			hash = (hash * FNV1_64_PRIME) ^ ((byte)x);
			x >>= 8;
			hash = (hash * FNV1_64_PRIME) ^ ((byte)x);
			x >>= 8;
			hash = (hash * FNV1_64_PRIME) ^ ((byte)x);
			x >>= 8;
			hash = (hash * FNV1_64_PRIME) ^ ((byte)x);
			x >>= 8;
			hash = (hash * FNV1_64_PRIME) ^ ((byte)x);
			x >>= 8;
			hash = (hash * FNV1_64_PRIME) ^ ((byte)x);
			return hash;
		}

		/// <summary>Calcul le Hash FNV-1 64 bit d'un UInt64</summary>
		/// <param name="x">unsigned int64 (little endian)</param>
		/// <returns>Hash de la valeur (sur 64 bits)</returns>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static ulong FromUInt64(ulong x)
		{
			return Continue(FNV1_64_OFFSET_BASIS, x);
		}

		/// <summary>Ajoute un UInt64 au hash</summary>
		/// <param name="hash">Valeur pr�c�dente du hash</param>
		/// <param name="x">unsigned int64 (little endian)</param>
		/// <returns>Nouvelle valeur du hash</returns>
		public static ulong Continue(ulong hash, ulong x)
		{
			hash = (hash * FNV1_64_PRIME) ^ ((byte)x);
			x >>= 8;
			hash = (hash * FNV1_64_PRIME) ^ ((byte)x);
			x >>= 8;
			hash = (hash * FNV1_64_PRIME) ^ ((byte)x);
			x >>= 8;
			hash = (hash * FNV1_64_PRIME) ^ ((byte)x);
			x >>= 8;
			hash = (hash * FNV1_64_PRIME) ^ ((byte)x);
			x >>= 8;
			hash = (hash * FNV1_64_PRIME) ^ ((byte)x);
			x >>= 8;
			hash = (hash * FNV1_64_PRIME) ^ ((byte)x);
			x >>= 8;
			hash = (hash * FNV1_64_PRIME) ^ ((byte)x);
			return hash;
		}

		/// <summary>Calcul le Hash FNV-1 64 bit d'un GUID</summary>
		/// <param name="value">GUID � hasher</param>
		/// <returns>Hash du GUID</returns>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static ulong FromGuid(Guid value)
		{
			return Continue(FNV1_64_OFFSET_BASIS, value);
		}

		/// <summary>Ajoute un GUID au hash</summary>
		/// <param name="hash">Valeur pr�c�dente du hash</param>
		/// <param name="x">GUID (128 bits)</param>
		/// <returns>Nouvelle valeur du hash</returns>
		public static ulong Continue(ulong hash, Guid x)
		{
			unsafe
			{
				ulong* q = (ulong*)(&x);
				hash = Continue(hash, q[0]);
				return Continue(hash, q[1]);
			}
		}

#if DEPRECATED
		/// <summary>Retourne un stream qui calcule le Hash FNV-1 64 bit sur les donn�es lues ou �crites sur un stream</summary>
		/// <param name="stream">Stream sur lequel les donn�es seront lues ou �crites</param>
		/// <param name="leaveOpen">Si true, laisse le stream ouvert lorsque ce stream est Disposed</param>
		/// <returns>Stream qui transmet toutes les op�rations au stream sp�cifi�, et met � jour le hash � chaque op�ration de lecture ou �criture</returns>
		/// <remarks>Note: ne pas oublier d'appeler Reset() apr�s chaque op�ration de Seek en mode lecture, pour reset le hash !</remarks>
		public static RollingCrcStream<ulong> WrapStream(Stream stream, bool leaveOpen = false)
		{
			return new RollingCrcStream<ulong>(
				stream,
				leaveOpen,
				reset: () => FNV1_64_OFFSET_BASIS,
				byteAdder: Fnv1aHash64.Continue, // (byte)
				chunkAdder: Fnv1aHash64.Continue // (byte[], int, int)
			);
		}
#endif

	}

}
