#region Copyright (c) 2005-2023 Doxense SAS
//
// All rights are reserved. Reproduction or transmission in whole or in part, in
// any form or by any means, electronic, mechanical or otherwise, is prohibited
// without the prior written consent of the copyright owner.
//
#endregion

#define USE_FAST_STRING_ALLOCATOR

namespace Doxense.Serialization
{
	using System;
	using System.Buffers;
	using System.IO;
	using System.Runtime.CompilerServices;
	using System.Runtime.InteropServices;
	using Doxense.Diagnostics.Contracts;
	using Doxense.Memory;
	using JetBrains.Annotations;

	[PublicAPI]
	public static class Base64Encoding
	{
		// Encodage similaire à Base64, mais qui remplace '+' et '/' par '-' et '_', avec padding optionnel où '=' est remplacé par '*'
		// => peut être inséré dans une URL sans nécessiter d'encodage spécifique

		private const int CHARMAP_PAGE_SIZE = 256;

		#region Base64 Generic

		private const char Base64PadChar = '=';

		private static readonly char[] Base64CharMap = new char[CHARMAP_PAGE_SIZE * 2]
		{
			// HI
			'A', 'A', 'A', 'A', 'B', 'B', 'B', 'B', 'C', 'C',
			'C', 'C', 'D', 'D', 'D', 'D', 'E', 'E', 'E', 'E',
			'F', 'F', 'F', 'F', 'G', 'G', 'G', 'G', 'H', 'H',
			'H', 'H', 'I', 'I', 'I', 'I', 'J', 'J', 'J', 'J',
			'K', 'K', 'K', 'K', 'L', 'L', 'L', 'L', 'M', 'M',
			'M', 'M', 'N', 'N', 'N', 'N', 'O', 'O', 'O', 'O',
			'P', 'P', 'P', 'P', 'Q', 'Q', 'Q', 'Q', 'R', 'R',
			'R', 'R', 'S', 'S', 'S', 'S', 'T', 'T', 'T', 'T',
			'U', 'U', 'U', 'U', 'V', 'V', 'V', 'V', 'W', 'W',
			'W', 'W', 'X', 'X', 'X', 'X', 'Y', 'Y', 'Y', 'Y',
			'Z', 'Z', 'Z', 'Z', 'a', 'a', 'a', 'a', 'b', 'b',
			'b', 'b', 'c', 'c', 'c', 'c', 'd', 'd', 'd', 'd',
			'e', 'e', 'e', 'e', 'f', 'f', 'f', 'f', 'g', 'g',
			'g', 'g', 'h', 'h', 'h', 'h', 'i', 'i', 'i', 'i',
			'j', 'j', 'j', 'j', 'k', 'k', 'k', 'k', 'l', 'l',
			'l', 'l', 'm', 'm', 'm', 'm', 'n', 'n', 'n', 'n',
			'o', 'o', 'o', 'o', 'p', 'p', 'p', 'p', 'q', 'q',
			'q', 'q', 'r', 'r', 'r', 'r', 's', 's', 's', 's',
			't', 't', 't', 't', 'u', 'u', 'u', 'u', 'v', 'v',
			'v', 'v', 'w', 'w', 'w', 'w', 'x', 'x', 'x', 'x',
			'y', 'y', 'y', 'y', 'z', 'z', 'z', 'z', '0', '0',
			'0', '0', '1', '1', '1', '1', '2', '2', '2', '2',
			'3', '3', '3', '3', '4', '4', '4', '4', '5', '5',
			'5', '5', '6', '6', '6', '6', '7', '7', '7', '7',
			'8', '8', '8', '8', '9', '9', '9', '9', '+', '+',
			'+', '+', '/', '/', '/', '/',
			// LO
			'A', 'B', 'C', 'D', 'E', 'F', 'G', 'H', 'I', 'J',
			'K', 'L', 'M', 'N', 'O', 'P', 'Q', 'R', 'S', 'T',
			'U', 'V', 'W', 'X', 'Y', 'Z', 'a', 'b', 'c', 'd',
			'e', 'f', 'g', 'h', 'i', 'j', 'k', 'l', 'm', 'n',
			'o', 'p', 'q', 'r', 's', 't', 'u', 'v', 'w', 'x',
			'y', 'z', '0', '1', '2', '3', '4', '5', '6', '7',
			'8', '9', '+', '/', 'A', 'B', 'C', 'D', 'E', 'F',
			'G', 'H', 'I', 'J', 'K', 'L', 'M', 'N', 'O', 'P',
			'Q', 'R', 'S', 'T', 'U', 'V', 'W', 'X', 'Y', 'Z',
			'a', 'b', 'c', 'd', 'e', 'f', 'g', 'h', 'i', 'j',
			'k', 'l', 'm', 'n', 'o', 'p', 'q', 'r', 's', 't',
			'u', 'v', 'w', 'x', 'y', 'z', '0', '1', '2', '3',
			'4', '5', '6', '7', '8', '9', '+', '/', 'A', 'B',
			'C', 'D', 'E', 'F', 'G', 'H', 'I', 'J', 'K', 'L',
			'M', 'N', 'O', 'P', 'Q', 'R', 'S', 'T', 'U', 'V',
			'W', 'X', 'Y', 'Z', 'a', 'b', 'c', 'd', 'e', 'f',
			'g', 'h', 'i', 'j', 'k', 'l', 'm', 'n', 'o', 'p',
			'q', 'r', 's', 't', 'u', 'v', 'w', 'x', 'y', 'z',
			'0', '1', '2', '3', '4', '5', '6', '7', '8', '9',
			'+', '/', 'A', 'B', 'C', 'D', 'E', 'F', 'G', 'H',
			'I', 'J', 'K', 'L', 'M', 'N', 'O', 'P', 'Q', 'R',
			'S', 'T', 'U', 'V', 'W', 'X', 'Y', 'Z', 'a', 'b',
			'c', 'd', 'e', 'f', 'g', 'h', 'i', 'j', 'k', 'l',
			'm', 'n', 'o', 'p', 'q', 'r', 's', 't', 'u', 'v',
			'w', 'x', 'y', 'z', '0', '1', '2', '3', '4', '5',
			'6', '7', '8', '9', '+', '/'
		};

		#endregion

		#region Base64 URL

		private const char Base64UrlPadChar = '*';

		private static readonly char[] Base64UrlCharMap = new char[CHARMAP_PAGE_SIZE * 2]
		{
			// HI
			'A', 'A', 'A', 'A', 'B', 'B', 'B', 'B', 'C', 'C',
			'C', 'C', 'D', 'D', 'D', 'D', 'E', 'E', 'E', 'E',
			'F', 'F', 'F', 'F', 'G', 'G', 'G', 'G', 'H', 'H',
			'H', 'H', 'I', 'I', 'I', 'I', 'J', 'J', 'J', 'J',
			'K', 'K', 'K', 'K', 'L', 'L', 'L', 'L', 'M', 'M',
			'M', 'M', 'N', 'N', 'N', 'N', 'O', 'O', 'O', 'O',
			'P', 'P', 'P', 'P', 'Q', 'Q', 'Q', 'Q', 'R', 'R',
			'R', 'R', 'S', 'S', 'S', 'S', 'T', 'T', 'T', 'T',
			'U', 'U', 'U', 'U', 'V', 'V', 'V', 'V', 'W', 'W',
			'W', 'W', 'X', 'X', 'X', 'X', 'Y', 'Y', 'Y', 'Y',
			'Z', 'Z', 'Z', 'Z', 'a', 'a', 'a', 'a', 'b', 'b',
			'b', 'b', 'c', 'c', 'c', 'c', 'd', 'd', 'd', 'd',
			'e', 'e', 'e', 'e', 'f', 'f', 'f', 'f', 'g', 'g',
			'g', 'g', 'h', 'h', 'h', 'h', 'i', 'i', 'i', 'i',
			'j', 'j', 'j', 'j', 'k', 'k', 'k', 'k', 'l', 'l',
			'l', 'l', 'm', 'm', 'm', 'm', 'n', 'n', 'n', 'n',
			'o', 'o', 'o', 'o', 'p', 'p', 'p', 'p', 'q', 'q',
			'q', 'q', 'r', 'r', 'r', 'r', 's', 's', 's', 's',
			't', 't', 't', 't', 'u', 'u', 'u', 'u', 'v', 'v',
			'v', 'v', 'w', 'w', 'w', 'w', 'x', 'x', 'x', 'x',
			'y', 'y', 'y', 'y', 'z', 'z', 'z', 'z', '0', '0',
			'0', '0', '1', '1', '1', '1', '2', '2', '2', '2',
			'3', '3', '3', '3', '4', '4', '4', '4', '5', '5',
			'5', '5', '6', '6', '6', '6', '7', '7', '7', '7',
			'8', '8', '8', '8', '9', '9', '9', '9', '-', '-',
			'-', '-', '_', '_', '_', '_',
			// LO
			'A', 'B', 'C', 'D', 'E', 'F', 'G', 'H', 'I', 'J',
			'K', 'L', 'M', 'N', 'O', 'P', 'Q', 'R', 'S', 'T',
			'U', 'V', 'W', 'X', 'Y', 'Z', 'a', 'b', 'c', 'd',
			'e', 'f', 'g', 'h', 'i', 'j', 'k', 'l', 'm', 'n',
			'o', 'p', 'q', 'r', 's', 't', 'u', 'v', 'w', 'x',
			'y', 'z', '0', '1', '2', '3', '4', '5', '6', '7',
			'8', '9', '-', '_', 'A', 'B', 'C', 'D', 'E', 'F',
			'G', 'H', 'I', 'J', 'K', 'L', 'M', 'N', 'O', 'P',
			'Q', 'R', 'S', 'T', 'U', 'V', 'W', 'X', 'Y', 'Z',
			'a', 'b', 'c', 'd', 'e', 'f', 'g', 'h', 'i', 'j',
			'k', 'l', 'm', 'n', 'o', 'p', 'q', 'r', 's', 't',
			'u', 'v', 'w', 'x', 'y', 'z', '0', '1', '2', '3',
			'4', '5', '6', '7', '8', '9', '-', '_', 'A', 'B',
			'C', 'D', 'E', 'F', 'G', 'H', 'I', 'J', 'K', 'L',
			'M', 'N', 'O', 'P', 'Q', 'R', 'S', 'T', 'U', 'V',
			'W', 'X', 'Y', 'Z', 'a', 'b', 'c', 'd', 'e', 'f',
			'g', 'h', 'i', 'j', 'k', 'l', 'm', 'n', 'o', 'p',
			'q', 'r', 's', 't', 'u', 'v', 'w', 'x', 'y', 'z',
			'0', '1', '2', '3', '4', '5', '6', '7', '8', '9',
			'-', '_', 'A', 'B', 'C', 'D', 'E', 'F', 'G', 'H',
			'I', 'J', 'K', 'L', 'M', 'N', 'O', 'P', 'Q', 'R',
			'S', 'T', 'U', 'V', 'W', 'X', 'Y', 'Z', 'a', 'b',
			'c', 'd', 'e', 'f', 'g', 'h', 'i', 'j', 'k', 'l',
			'm', 'n', 'o', 'p', 'q', 'r', 's', 't', 'u', 'v',
			'w', 'x', 'y', 'z', '0', '1', '2', '3', '4', '5',
			'6', '7', '8', '9', '-', '_'
		};

		#endregion

		#region Base64 Generic

		public static string ToBase64String(byte[] buffer)
		{
			Contract.NotNull(buffer);
			return EncodeBuffer(buffer.AsSpan(), padded:true, urlSafe: false);
		}

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static string ToBase64String(Slice buffer)
		{
			return EncodeBuffer(buffer.Span, padded: true, urlSafe: false);
		}

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static string ToBase64String(ReadOnlySpan<byte> buffer)
		{
			return EncodeBuffer(buffer, padded: true, urlSafe: false);
		}

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static unsafe string ToBase64String(byte* buffer, int count)
		{
			return EncodeBufferUnsafe(buffer, count, padded: true, urlSafe: false);
		}

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static byte[] FromBase64String(string s)
		{
			Contract.NotNull(s);

			return DecodeBuffer(s.AsSpan(), padded: true, urlSafe: false);
		}

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static byte[] FromBase64String(ReadOnlySpan<char> s)
		{
			return DecodeBuffer(s, padded: true, urlSafe: false);
		}

		#endregion

		#region Base64Url (WebSafe)

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static string ToBase64UrlString(byte[] buffer)
		{
			Contract.NotNull(buffer);
			return EncodeBuffer(buffer.AsSpan(), padded: false, urlSafe: true);
		}

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static string ToBase64UrlString(ReadOnlySpan<byte> buffer)
		{
			return EncodeBuffer(buffer, padded: false, urlSafe: true);
		}

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static string ToBase64UrlString(Slice buffer)
		{
			return EncodeBuffer(buffer.Span, padded: false, urlSafe: true);
		}

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static unsafe string ToBase64UrlString(byte* buffer, int count)
		{
			return EncodeBufferUnsafe(buffer, count, padded: false, urlSafe: true);
		}

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static byte[] FromBase64UrlString(string s)
		{
			Contract.NotNull(s);
			return DecodeBuffer(s.AsSpan(), padded: false, urlSafe: true);
		}

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static byte[] FromBase64UrlString(ReadOnlySpan<char> s)
		{
			return DecodeBuffer(s, padded: false, urlSafe: true);
		}

		#endregion

		#region Common...

		/// <summary>Calcule le nombre exacte de caractères nécessaire pour encoder un buffer en Base64</summary>
		/// <param name="length">Nombre d'octets source à encoder</param>
		/// <param name="padded">True s'il y a du padding (résultat multiple de 4).</param>
		/// <returns>Nombre de caractères nécessaire pour encoder un buffer de taille <paramref name="length"/>, avec ou sans padding</returns>
		[Pure]
		public static int GetCharsCount(int length, bool padded)
		{
			if (length < 0) throw new ArgumentOutOfRangeException(nameof(length), "Buffer length cannot be negative.");
			if (padded)
			{ // 3 octets source sont encodés en 4 chars, arrondi au supérieur
				return checked((length + 2) / 3 * 4);
			}
			else
			{
				int chunks = checked((length / 3) * 4);
				switch (length % 3)
				{
					case 0: return chunks;
					case 1: return checked(chunks + 2);
					default: return checked(chunks + 3);
				}
			}
		}

		[Pure]
		public static int EstimateBytesCount(int length)
		{
			// 4 chars décodés en 3 bytes, arrondi à l'inférieur
			return checked(length / 4 * 3 + 2);
		}

		[Pure]
		public static string EncodeBuffer(ReadOnlySpan<byte> buffer, bool padded = true, bool urlSafe = false)
		{
			if (buffer.Length == 0) return string.Empty;

			// note: cette version est 20-25% plus rapide que Convert.ToBase64String() sur ma machine.
			// La version de la BCL "triche" en pré-allouant la string, et en écrivant directement dedans.
			// => j'ai essayé d'appeler FastAllocateString() directement, et je passe a 35% plus rapide...

			// détermine la taille exacte du résultat (incluant le padding si présent)
			int size = GetCharsCount(buffer.Length, padded);

			unsafe
			{
				fixed (byte* ptr = &MemoryMarshal.GetReference(buffer))
				{
					return EncodeBufferUnsafe(ptr, buffer.Length, size, padded, urlSafe);
				}
			}
		}

		[Pure]
		public static unsafe string EncodeBufferUnsafe(byte* buffer, int count, bool padded = true, bool urlSafe = false)
		{
			if (count == 0) return string.Empty;

			Contract.PointerNotNull(buffer);
			Contract.Positive(count);

			// détermine la taille exacte du résultat (incluant le padding si présent)
			int size = GetCharsCount(count, padded);

			return EncodeBufferUnsafe(buffer, count, size, padded, urlSafe);
		}

		/// <summary>Encode un buffer en base64</summary>
		/// <param name="buffer">Buffer source</param>
		/// <param name="count">Nombre d'octets à convertir</param>
		/// <param name="charCount">Taille du résultat (précalculée via <see cref="GetCharsCount"/>)</param>
		/// <param name="padded">Si true, pad la fin si pas multiple de 3</param>
		/// <param name="urlSafe">Si true, utilise le charset web safe</param>
		/// <returns></returns>
		[Pure]
		private static unsafe string EncodeBufferUnsafe(byte* buffer, int count, int charCount, bool padded, bool urlSafe)
		{
			Contract.Debug.Requires(buffer != null && count >= 0 && charCount >= 0);

			char padChar = padded ? (urlSafe ? Base64UrlPadChar : Base64PadChar) : '\0';
			char[] charMap = urlSafe ? Base64UrlCharMap : Base64CharMap;

			fixed (char* pMap = charMap)
			{
#if USE_FAST_STRING_ALLOCATOR
				// alloue directement la string, avec la bonne taille
				string s = s_fastStringAllocator(charCount);
				fixed (char* ptr = s)
				{
					// on va écrire directement dans la string (!!)
					EncodeBufferUnsafe(ptr, charCount, buffer, count, pMap, padChar);
				}
				return s;
#else
				// arrondi a 8 supérieur, pour garder un alignement correct sur la stack
				size = (size + 7) & (~0x7);

				if (size <= MAX_INLINE_SIZE)
				{ // Allocate on the stack
					char* output = stackalloc char[size];
					int n = EncodeBufferUnsafe(output, size, buffer, count, pMap, padChar);
					return new string(output, 0, n);
				}
				else
				{ // Allocate on the heap
					char[] output = new char[size];
					fixed (char* pOut = output)
					{
						int n = EncodeBufferUnsafe(pOut, size, buffer, count, pMap, padChar);
						return new string(output, 0, n);
					}
				}
#endif
			}
		}

		/// <summary>Ecrit un buffer de taille quelconque dans un TextWriter, encodé en Base64</summary>
		/// <param name="output">Writer où écrire le texte encodé en Base64</param>
		/// <param name="buffer">Buffer source contenant les données à encoder</param>
		/// <param name="padded">Ajoute des caractères de padding (true) ou non (false). Le caractère de padding utilisé dépend de la valeur de <paramref name="urlSafe"/>.</param>
		/// <param name="urlSafe">Si true, remplace les caractères 63 et 63 par des versions Url Safe ('-' et '_'). Sinon, utilise les caractères classiques ('+' et '/')</param>
		/// <remarks>Si <paramref name="padded"/> est true, le nombre de caractères écrit sera toujours un multiple de 4.</remarks>
		public static void EncodeTo(TextWriter output, Slice buffer, bool padded = true, bool urlSafe = false)
		{
			EncodeTo(output, buffer.Span, padded, urlSafe);
		}

		/// <summary>Ecrit un buffer de taille quelconque dans un TextWriter, encodé en Base64</summary>
		/// <param name="output">Writer où écrire le texte encodé en Base64</param>
		/// <param name="buffer">Buffer source contenant les données à encoder</param>
		/// <param name="padded">Ajoute des caractères de padding (true) ou non (false). Le caractère de padding utilisé dépend de la valeur de <paramref name="urlSafe"/>.</param>
		/// <param name="urlSafe">Si true, remplace les caractères 63 et 63 par des versions Url Safe ('-' et '_'). Sinon, utilise les caractères classiques ('+' et '/')</param>
		/// <remarks>Si <paramref name="padded"/> est true, le nombre de caractères écrit sera toujours un multiple de 4.</remarks>
		public static void EncodeTo(TextWriter output, ReadOnlySpan<byte> buffer, bool padded = true, bool urlSafe = false)
		{
			Contract.NotNull(output);

			if (buffer.Length == 0) return;

			int size = GetCharsCount(buffer.Length, padded);
			char padChar = !padded ? '\0' : urlSafe ? Base64UrlPadChar : Base64PadChar;
			char[] charMap = urlSafe ? Base64UrlCharMap : Base64CharMap;

			//TODO: if StringWriter, extraire le StringBuilder, et faire une version qui écrit directement dedans !

			// pour générer 4096 chars, il faut 3 072 bytes
			// pour générer 65536 chars, il faut 49 152 bytes
			const int CHARSIZE = 65536;
			const int BYTESIZE = (CHARSIZE / 4) * 3;

			int bufferSize = Math.Min(size, CHARSIZE);
			bufferSize = (bufferSize + 7) & (~0x7); // arrondi a 8 supérieur, pour garder un alignement correct
			var chars = ArrayPool<char>.Shared.Rent(bufferSize);

			unsafe
			{
				fixed (byte* pIn = &MemoryMarshal.GetReference(buffer))
				fixed (char* pOut = &chars[0])
				fixed (char* pMap = charMap)
				{
					byte* ptr = pIn;
					int remaining = buffer.Length;
					while (remaining > 0)
					{
						int sz = Math.Min(remaining, BYTESIZE);
						int n = EncodeBufferUnsafe(pOut, bufferSize, ptr, sz, pMap, remaining > BYTESIZE ? '\0' : padChar);
						Contract.Debug.Assert(n <= bufferSize);
						if (n <= 0) break; //REVIEW: dans quel cas on peut avoir <= 0 ???
						output.Write(chars, 0, n);
						remaining -= sz;
						ptr += sz;
					}
					ArrayPool<char>.Shared.Return(chars);
					if (remaining > 0) throw new InvalidOperationException(); // ??
				}
			}
		}

		#endregion

		#region Internal Helpers...

		/// <summary>Encode un segment de données vers un buffer</summary>
		/// <param name="output">Buffer où écrire le texte encodée. La capacité doit être suffisante!</param>
		/// <param name="capacity">Capacité du buffer pointé par <paramref name="output"/></param>
		/// <param name="source">Pointeur vers le début octets à encoder.</param>
		/// <param name="len">Nombre d'octets à encoder</param>
		/// <param name="charMap">Pointeur vers la map de conversion Base64 à utiliser (2 x 256 chars)</param>
		/// <param name="padChar">Caractère de padding utilisé, ou '\0' si pas de padding</param>
		/// <returns>Nombre de caractères écrits dans <paramref name="output"/>.</returns>
		internal static unsafe int EncodeBufferUnsafe(char* output, int capacity, byte* source, int len, char* charMap, char padChar)
		{
			Contract.Debug.Requires(output != null && capacity >= 0 && source != null && len >= 0 && charMap != null);

			// portage en C# de modp_b64.c: https://code.google.com/p/stringencoders/source/browse/trunk/src/modp_b64.c

			byte* inp = source;
			char* outp = output;

			char* e0 = charMap + 0;
			char* e1 = charMap + CHARMAP_PAGE_SIZE;
			//note: e2 == e1 ?

			int i, end;
			uint t1, t2, t3;

			// vérifie que le buffer peut contenir au moins les chunks complets
			char* stop = outp + capacity;
			if (outp + (len / 3 * 4) > stop) ThrowOutputBufferTooSmall();

			// 4 bytes chunks...
			for (i = 0, end = len - 2; i < end; i += 3)
			{
				t1 = inp[i]; t2 = inp[i + 1]; t3 = inp[i + 2];
				outp[0] = e0[t1];
				outp[1] = e1[((t1 & 0x03) << 4) | ((t2 >> 4) & 0x0F)];
				outp[2] = e1[((t2 & 0x0F) << 2) | ((t3 >> 6) & 0x03)];
				outp[3] = e1[t3];
				outp += 4;
			}

			// remainder...
			switch (len - i)
			{
				case 0:
				{
					break;
				}
				case 1:
				{
					if (outp + 2 > stop) ThrowOutputBufferTooSmall();
					t1 = inp[i];
					outp[0] = e0[t1];
					outp[1] = e1[(t1 & 0x03) << 4];
					outp += 2;
					if (padChar != '\0')
					{
						if (outp + 2 > stop) ThrowOutputBufferTooSmall();
						outp[0] = padChar;
						outp[1] = padChar;
						outp += 2;
					}
					break;
				}
				default:
				{
					t1 = inp[i]; t2 = inp[i + 1];
					if (outp + 3 > stop) ThrowOutputBufferTooSmall();
					outp[0] = e0[t1];
					outp[1] = e1[((t1 & 0x03) << 4) | ((t2 >> 4) & 0x0F)];
					outp[2] = e1[(t2 & 0x0F) << 2];
					outp += 3;
					if (padChar != '\0')
					{
						if (outp + 1 > stop) ThrowOutputBufferTooSmall();
						*outp++ = padChar;
					}
					break;
				}
			}
			Contract.Debug.Ensures(outp >= output && outp <= stop);
			return (int)(outp - output);
		}

		[ContractAnnotation("buffer:null => halt")]
		private static void EnsureBufferIsValid(byte[] buffer, int offset, int count)
		{
			Contract.NotNull(buffer);
			if (offset < 0) ThrowHelper.ThrowArgumentOutOfRangeException(nameof(offset));
			if (count < 0 || offset + count > buffer.Length) ThrowHelper.ThrowArgumentOutOfRangeException(nameof(count));
		}

		[ContractAnnotation("=> halt")]
		private static void ThrowOutputBufferTooSmall()
		{
			throw new InvalidOperationException("The output buffer is too small");
		}

#if USE_FAST_STRING_ALLOCATOR

		private static readonly Func<int, string> s_fastStringAllocator = GetFastStringAllocator();

		private static Func<int, string> GetFastStringAllocator()
		{
			var method = typeof(string).GetMethod("FastAllocateString", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic);
			if (method != null)
			{
				return (Func<int, string>)method.CreateDelegate(typeof(Func<int, string>));
			}
			else
			{
#if DEBUG
				// Si vous arrivez ici, c'est que la méthode String.FastAllocateString() n'existe plus (nouvelle version de .NET Framework?)
				// => essayez de trouver une solution alternative pour pré-allouer une string à la bonne taille
				if (System.Diagnostics.Debugger.IsAttached) System.Diagnostics.Debugger.Break();
#endif
				return (count) => new string('\0', count);
			}
		}

#endif

		/// <summary>Détermine la taille de buffer nécessaire pour encoder un segment de text en Base64</summary>
		/// <param name="s">Segment de texte</param>
		/// <param name="padChar">Padding character (optionnel)</param>
		public static int GetBytesCount(ReadOnlySpan<char> s, char padChar = '\0')
		{
			unsafe
			{
				fixed (char* chars = &MemoryMarshal.GetReference(s))
				{
					return GetBytesCount(chars, s.Length, padChar);
				}
			}
		}

		/// <summary>Détermine la taille de buffer nécessaire pour encoder un segment de text en Base64</summary>
		/// <param name="src">Pointeur vers le début du segment de texte</param>
		/// <param name="len">Taille du segment (en caractères)</param>
		/// <param name="padChar">Padding character (optionnel)</param>
		public static unsafe int GetBytesCount(char* src, int len, char padChar)
		{
			Contract.Debug.Requires(len >= 0 && (len == 0 || src != null));
			if (len == 0) return 0;

			int padding;
			if (padChar != '\0')
			{
				padding = 0;
				while (len > 0)
				{
					if (src[len - 1] != padChar) break;
					++padding;
					--len;
				}
				if (padding != 0)
				{
					if (padding == 1) { padding = 2; }
					else if (padding == 2) { padding = 1; }
					else
					{
						throw new FormatException("The input is not a valid Base-64 string as it contains a non-base 64 character, more than two padding characters, or an illegal character among the padding characters.");
					}
				}
			}
			else
			{
				switch(len % 4)
				{
					case 0: padding = 0; break;
					case 1: case 2: padding = 1; break;
					default: padding = 2; break;
				}
			}
			return (len / 4) * 3 + padding;
		}

		[Pure]
		public static byte[] DecodeBuffer(ReadOnlySpan<char> encoded, bool padded = true, bool urlSafe = false)
		{
			if (encoded.Length == 0) return Array.Empty<byte>();

			unsafe
			{
				char padChar = padded ? (urlSafe ? Base64UrlPadChar : Base64PadChar) : '\0';

				fixed (char* pSrc = &MemoryMarshal.GetReference(encoded))
				{
					int size = GetBytesCount(pSrc, encoded.Length, padChar); //TODO: padding + urlSafe ?
					var dest = new byte[size];

					fixed (byte* pDst = dest)
					{
						int p = DecodeBufferUnsafe(pDst, pSrc, encoded.Length, padChar);
						if (p < 0) throw new FormatException("Malformed base64 string");
						return dest;
					}
				}
			}
		}

		[Pure]
		public static Slice DecodeBuffer(ReadOnlySpan<char> encoded, ref byte[]? buffer, bool padded = true, bool urlSafe = false)
		{
			if (encoded.Length == 0) return Slice.Empty;

			unsafe
			{
				char padChar = padded ? (urlSafe ? Base64UrlPadChar : Base64PadChar) : '\0';

				fixed (char* pSrc = &MemoryMarshal.GetReference(encoded))
				{
					int size = GetBytesCount(pSrc, encoded.Length, padChar); //TODO: padding + urlSafe ?

					var tmp = UnsafeHelpers.EnsureCapacity(ref buffer, size);

					fixed (byte* pDst = &tmp[0])
					{
						int p = DecodeBufferUnsafe(pDst, pSrc, encoded.Length, padChar);
						if (p < 0) throw new FormatException("Malformed base64 string");
						return new Slice(tmp, 0, p);
					}
				}
			}
		}

		internal static unsafe int DecodeBufferUnsafe(byte* dest, char* src, int len, char padChar)
		{
			if (len == 0) return 0;
			Contract.PointerNotNull(dest);
			Contract.PointerNotNull(src);

			if (padChar != '\0')
			{
				if (len < 4 || len % 4 != 0)
				{ // doit être un multiple de 4
					throw new FormatException("Invalid padding");
				}
				// ne doit pas y avoir plus de 2 pad chars a la fin
				if (src[len - 1] == padChar)
				{
					--len;
					if (src[len - 1] == padChar)
					{
						--len;
					}
				}
			}

			int leftOver = len % 4;
			int chunks = leftOver == 0 ? (len / 4 - 1) : (len / 4);

			byte* p = dest;
			uint x;
			ulong* srcInt = (ulong*)src;
			ulong y = *srcInt++;

			uint[] d0 = DecodeMap0;
			uint[] d1 = DecodeMap1;
			uint[] d2 = DecodeMap2;
			uint[] d3 = DecodeMap3;

			for (int i = 0; i < chunks; ++i)
			{
				x = d0[y & 0xff]
				  | d1[(y >> 16) & 0xff]
				  | d2[(y >> 32) & 0xff]
				  | d3[(y >> 48) & 0xff];

				if (x >= BADCHAR) return -1;
				*((uint*)p) = x;
				p += 3;
				y = *srcInt++;
			}

			switch(leftOver)
			{
				case 0:
				{
					x = d0[y & 0xff]
					  | d1[(y >> 16) & 0xff]
					  | d2[(y >> 32) & 0xff]
					  | d3[(y >> 48) & 0xff];

					if (x >= BADCHAR) return -1;

					p[0] = (byte)x;
					p[1] = (byte)(x >> 8);
					p[2] = (byte)(x >> 16);

					return (chunks + 1) * 3;
				}

				case 1:
				{ // 1 output byte
					if (padChar != '\0') return -1; //impossible avec du padding
					x = d0[y & 0xFF];
					*p = (byte)x;
					break;
				}
				case 2:
				{ // 1 output byte
					x = d0[y & 0xFF]
					  | d1[(y >> 16) & 0xFF];
					*p = (byte)x;
					break;
				}
				default:
				{ // 2 output byte
					x = d0[y & 0xFF]
					  | d1[(y >> 16) & 0xFF]
					  | d2[(y >> 32) & 0xFF];
					p[0] = (byte)x;
					p[1] = (byte)(x >> 8);
					break;
				}
			}

			if (x >= BADCHAR) return -1;
			return (3 * chunks) + ((6 * leftOver) / 8);

		}

		#endregion

		#region Data...

		private const uint BADCHAR = 0x01FFFFFF;

		private static readonly uint[] DecodeMap0 = new uint[256]
		{
			0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff,
			0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff,
			0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff,
			0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff,
			0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff,
			0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff,
			0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff,
			0x01ffffff, 0x000000f8, 0x01ffffff, 0x000000f8, 0x01ffffff, 0x000000fc,
			0x000000d0, 0x000000d4, 0x000000d8, 0x000000dc, 0x000000e0, 0x000000e4,
			0x000000e8, 0x000000ec, 0x000000f0, 0x000000f4, 0x01ffffff, 0x01ffffff,
			0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x00000000,
			0x00000004, 0x00000008, 0x0000000c, 0x00000010, 0x00000014, 0x00000018,
			0x0000001c, 0x00000020, 0x00000024, 0x00000028, 0x0000002c, 0x00000030,
			0x00000034, 0x00000038, 0x0000003c, 0x00000040, 0x00000044, 0x00000048,
			0x0000004c, 0x00000050, 0x00000054, 0x00000058, 0x0000005c, 0x00000060,
			0x00000064, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x000000fc,
			0x01ffffff, 0x00000068, 0x0000006c, 0x00000070, 0x00000074, 0x00000078,
			0x0000007c, 0x00000080, 0x00000084, 0x00000088, 0x0000008c, 0x00000090,
			0x00000094, 0x00000098, 0x0000009c, 0x000000a0, 0x000000a4, 0x000000a8,
			0x000000ac, 0x000000b0, 0x000000b4, 0x000000b8, 0x000000bc, 0x000000c0,
			0x000000c4, 0x000000c8, 0x000000cc, 0x01ffffff, 0x01ffffff, 0x01ffffff,
			0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff,
			0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff,
			0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff,
			0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff,
			0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff,
			0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff,
			0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff,
			0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff,
			0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff,
			0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff,
			0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff,
			0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff,
			0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff,
			0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff,
			0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff,
			0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff,
			0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff,
			0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff,
			0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff,
			0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff,
			0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff,
			0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff
		};

		private static readonly uint[] DecodeMap1 = new uint[256]
		{
			0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff,
			0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff,
			0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff,
			0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff,
			0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff,
			0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff,
			0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff,
			0x01ffffff, 0x0000e003, 0x01ffffff, 0x0000e003, 0x01ffffff, 0x0000f003,
			0x00004003, 0x00005003, 0x00006003, 0x00007003, 0x00008003, 0x00009003,
			0x0000a003, 0x0000b003, 0x0000c003, 0x0000d003, 0x01ffffff, 0x01ffffff,
			0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x00000000,
			0x00001000, 0x00002000, 0x00003000, 0x00004000, 0x00005000, 0x00006000,
			0x00007000, 0x00008000, 0x00009000, 0x0000a000, 0x0000b000, 0x0000c000,
			0x0000d000, 0x0000e000, 0x0000f000, 0x00000001, 0x00001001, 0x00002001,
			0x00003001, 0x00004001, 0x00005001, 0x00006001, 0x00007001, 0x00008001,
			0x00009001, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x0000f003,
			0x01ffffff, 0x0000a001, 0x0000b001, 0x0000c001, 0x0000d001, 0x0000e001,
			0x0000f001, 0x00000002, 0x00001002, 0x00002002, 0x00003002, 0x00004002,
			0x00005002, 0x00006002, 0x00007002, 0x00008002, 0x00009002, 0x0000a002,
			0x0000b002, 0x0000c002, 0x0000d002, 0x0000e002, 0x0000f002, 0x00000003,
			0x00001003, 0x00002003, 0x00003003, 0x01ffffff, 0x01ffffff, 0x01ffffff,
			0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff,
			0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff,
			0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff,
			0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff,
			0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff,
			0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff,
			0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff,
			0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff,
			0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff,
			0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff,
			0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff,
			0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff,
			0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff,
			0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff,
			0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff,
			0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff,
			0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff,
			0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff,
			0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff,
			0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff,
			0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff,
			0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff
		};


		private static readonly uint[] DecodeMap2 = new uint[256]
		{
			0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff,
			0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff,
			0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff,
			0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff,
			0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff,
			0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff,
			0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff,
			0x01ffffff, 0x00800f00, 0x01ffffff, 0x00800f00, 0x01ffffff, 0x00c00f00,
			0x00000d00, 0x00400d00, 0x00800d00, 0x00c00d00, 0x00000e00, 0x00400e00,
			0x00800e00, 0x00c00e00, 0x00000f00, 0x00400f00, 0x01ffffff, 0x01ffffff,
			0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x00000000,
			0x00400000, 0x00800000, 0x00c00000, 0x00000100, 0x00400100, 0x00800100,
			0x00c00100, 0x00000200, 0x00400200, 0x00800200, 0x00c00200, 0x00000300,
			0x00400300, 0x00800300, 0x00c00300, 0x00000400, 0x00400400, 0x00800400,
			0x00c00400, 0x00000500, 0x00400500, 0x00800500, 0x00c00500, 0x00000600,
			0x00400600, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x00c00f00,
			0x01ffffff, 0x00800600, 0x00c00600, 0x00000700, 0x00400700, 0x00800700,
			0x00c00700, 0x00000800, 0x00400800, 0x00800800, 0x00c00800, 0x00000900,
			0x00400900, 0x00800900, 0x00c00900, 0x00000a00, 0x00400a00, 0x00800a00,
			0x00c00a00, 0x00000b00, 0x00400b00, 0x00800b00, 0x00c00b00, 0x00000c00,
			0x00400c00, 0x00800c00, 0x00c00c00, 0x01ffffff, 0x01ffffff, 0x01ffffff,
			0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff,
			0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff,
			0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff,
			0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff,
			0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff,
			0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff,
			0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff,
			0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff,
			0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff,
			0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff,
			0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff,
			0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff,
			0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff,
			0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff,
			0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff,
			0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff,
			0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff,
			0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff,
			0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff,
			0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff,
			0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff,
			0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff
		};

		private static readonly uint[] DecodeMap3 = new uint[256]
		{
			0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff,
			0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff,
			0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff,
			0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff,
			0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff,
			0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff,
			0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff,
			0x01ffffff, 0x003e0000, 0x01ffffff, 0x003e0000, 0x01ffffff, 0x003f0000,
			0x00340000, 0x00350000, 0x00360000, 0x00370000, 0x00380000, 0x00390000,
			0x003a0000, 0x003b0000, 0x003c0000, 0x003d0000, 0x01ffffff, 0x01ffffff,
			0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x00000000,
			0x00010000, 0x00020000, 0x00030000, 0x00040000, 0x00050000, 0x00060000,
			0x00070000, 0x00080000, 0x00090000, 0x000a0000, 0x000b0000, 0x000c0000,
			0x000d0000, 0x000e0000, 0x000f0000, 0x00100000, 0x00110000, 0x00120000,
			0x00130000, 0x00140000, 0x00150000, 0x00160000, 0x00170000, 0x00180000,
			0x00190000, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x003f0000,
			0x01ffffff, 0x001a0000, 0x001b0000, 0x001c0000, 0x001d0000, 0x001e0000,
			0x001f0000, 0x00200000, 0x00210000, 0x00220000, 0x00230000, 0x00240000,
			0x00250000, 0x00260000, 0x00270000, 0x00280000, 0x00290000, 0x002a0000,
			0x002b0000, 0x002c0000, 0x002d0000, 0x002e0000, 0x002f0000, 0x00300000,
			0x00310000, 0x00320000, 0x00330000, 0x01ffffff, 0x01ffffff, 0x01ffffff,
			0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff,
			0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff,
			0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff,
			0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff,
			0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff,
			0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff,
			0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff,
			0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff,
			0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff,
			0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff,
			0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff,
			0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff,
			0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff,
			0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff,
			0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff,
			0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff,
			0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff,
			0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff,
			0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff,
			0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff,
			0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff,
			0x01ffffff, 0x01ffffff, 0x01ffffff, 0x01ffffff
		};
		#endregion

	}

}
