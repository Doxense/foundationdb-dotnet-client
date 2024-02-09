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

namespace Doxense.Serialization.Json.Binary
{
	using System;
	using System.Buffers;
	using System.Collections.Generic;
	using System.Runtime.CompilerServices;
	using System.Runtime.InteropServices;
	using Diagnostics.Contracts;
	using Doxense.Memory;
	using Doxense.Text;
	using JetBrains.Annotations;

	// basé sur:
	// http://git.postgresql.org/gitweb/?p=postgresql.git;a=blob_plain;f=src/include/utils/jsonb.h;hb=def4c28cf9147472ba4cfc5b68a098d7a29fb0fb

	public static class Jsonb
	{
		#region Format Specifications...

		// This is an adaptation of the jsonb (aka hstore2) from PostgreSQL 9.4+, that has been changed according to our needs.
		// The basic principle is the same (structured json, designed so that it is very easy to extract specific values without having to parse everything).

		// All header fields and containers are always aligned to 32 bits. Meaning that the total size of a jsonb document is always divisible by 4 bytes. Padding is used to fill the gaps.
		// Unless specified otherwise, all 32 bits fields are stored in little-endian, so 0x12345678 will be stored as the four bytes { 0x78, 0x56, 0x34, 0x12 }.

		#region Header...

		// The header is a 32 bits prefix with the following format:
		// - bit 31      : EXTRA      : [TBD!] If set, means that the header is immediately followed by an extra 32 bits of flags.
		// - bit 30      : RESERVED   : Reserved for future use. Must be 0 in current version
		// - bit 29      : RESERVED   : Reserved for future use. Must be 0 in current version
		// - bit 28      : RESERVED   : Reserved for future use. Must be 0 in current version
		// - bits 0..27  : TOTAL_SIZE : Total size (including this header, but excluding the padding) of the document. If the value is not a multiple of 4, there should be from 1 to 3 additionnal padding bytes (0) at the end of the document.

		// Since the total size must fit in 28 bits, it means that the maximum allowed data size is 268 435 455 (1 GB minus 1 byte). Chunking must be used to store more data.
		// If multiple jsonb documents are sent via a stream, it is possible to know the size of the block to read by rounding up TOTAL_SIZE to the next multiple of 4, and reading that number of bytes from the stream.

		/// <summary>Flag indiquant la présence d'un extra field contenant 32 bits</summary>
		private const uint HEADER_FLAGS_EXTRA = 0x80_00_00_00U;
		/// <summary>Flags réservés pour un usage future (= 00)</summary>
		private const uint HEADER_FLAGS_RESERVED = 0x70_00_00_00U;

		private const uint HEADER_SIZE_MASK = 0x0F_FF_FF_FFU; // 28 bits

		private const uint JCONTAINER_COUNT_MASK	= 0x0FFFFFFF; /* mask for size */
		private const uint JCONTAINER_FLAG_SCALAR	= 0x10000000; /* flag bits */
		private const uint JCONTAINER_FLAG_OBJECT	= 0x20000000;
		private const uint JCONTAINER_FLAG_ARRAY	= 0x40000000;
		private const uint JCONTAINER_FLAG_HASHED	= 0x80000000;
		private const int JCONTAINER_CHILDREN_OFFSET = 4; // offset (dans le conteneur) du début des JEntries
		private const int JCONTAINER_OFFSET_STRIDE = 32; // tout les combien d'entries faut-il spécifier l'offset (et non pas la length) ?

		private const int JENTRY_SIZEOF = 4;
		private const int JENTRY_OFFLENBITS = 28;
		private const uint JENTRY_OFFLENMASK = (1 << JENTRY_OFFLENBITS) - 1;
		private const uint JENTRY_TYPEMASK = 0x70000000;
		private const uint JENTRY_HAS_OFF = 0x80000000;

		private const uint JENTRY_TYPE_STRING		= 0x00000000;
		private const uint JENTRY_TYPE_INTEGER		= 0x10000000; // [CHANGED] (changed to 64-bit varint)
		private const uint JENTRY_TYPE_FALSE		= 0x20000000;
		private const uint JENTRY_TYPE_TRUE			= 0x30000000;
		private const uint JENTRY_TYPE_NULL			= 0x40000000;
		private const uint JENTRY_TYPE_CONTAINER	= 0x50000000;
		private const uint JENTRY_TYPE_NUMERIC		= 0x60000000; // [ADDITION] (text literal, unaligned)
		private const uint JENTRY_TYPE_RESERVED		= 0x70000000;

		#endregion

		#endregion

		#region Nested Structs...

		private enum JType
		{
			//note: les valeurs doivent être synchro avec les constants JENTRY_TYPE_xxx
			String = 0,		// 000
			Numeric = 6,	// 001
			False = 2,		// 010
			True = 3,		// 011
			Null = 4,		// 100
			Container = 5,	// 101
			Integer = 1,	// 110 [ADDITION]
			Reserved = 7,	// 111
		}

		[StructLayout(LayoutKind.Sequential)]
		private readonly ref struct JContainer
		{
			private readonly ReadOnlySpan<byte> Data;

			public JContainer(ReadOnlySpan<byte> data)
			{
				uint header = data.ReadFixedUInt32(0);

				this.Data = data;
				this.Header = header;
				this.BaseAddress = GetBaseAddress(header);
			}

			private readonly uint Header; // 4 flags + 28 bit count

			/// <summary>Offset (relatif au container) où commencent les données (items, ou key+values)</summary>
			/// <remarks>Dans le cas d'un Hashed Object, pointe directement après la hashmap/idxmap</remarks>
			private readonly int BaseAddress;

			public int Count
			{
				[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
				get => (int) (this.Header & JCONTAINER_COUNT_MASK);
			}

			public bool IsScalar
			{
				[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
				get => (this.Header & JCONTAINER_FLAG_SCALAR) != 0;
			}

			public bool IsObject
			{
				[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
				get => (this.Header & JCONTAINER_FLAG_OBJECT) != 0;
			}

			public bool IsArray
			{
				[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
				get => (this.Header & JCONTAINER_FLAG_ARRAY) != 0;
			}

			public bool IsHashed
			{
				[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
				get => (this.Header & JCONTAINER_FLAG_HASHED) != 0;
			}

			[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
			internal static int GetIndexMapStride(int count)
			{
				if (count < (1 << 8)) return 1;
				if (count < (1 << 16)) return 2;
				Contract.Debug.Assert(count < (1 << 24));
				return 3;
			}

			[Pure]
			private static int GetBaseAddress(uint header)
			{
				int count = (int) (header & JCONTAINER_COUNT_MASK);
				int numEntries = count;
				if ((header & JCONTAINER_FLAG_OBJECT) != 0) numEntries <<= 1;

				int baseAddress = JCONTAINER_CHILDREN_OFFSET + numEntries * JENTRY_SIZEOF;
				if ((header & JCONTAINER_FLAG_HASHED) != 0)
				{ // skip la hashmap + idxmap
					baseAddress += Aligned(count * (4 + GetIndexMapStride(count)));
				}
				return baseAddress;
			}

			/// <summary>Décode les informations d'un item d'une array</summary>
			/// <param name="index">Index de l'item (0 .. N-1)</param>
			/// <param name="numEntries"></param>
			/// <param name="result"></param>
			public void GetContainerEntry(int index, int numEntries, out JValue result)
			{
				// les données démarrent juste après les children
				if (index < 0 || index >= numEntries) throw ThrowHelper.ArgumentOutOfRangeIndex(index);

				int offset = GetJsonbOffset(index);
				int len = GetJsonbLength(index, offset);

				int child = checked(JCONTAINER_CHILDREN_OFFSET + index * JENTRY_SIZEOF);
				uint entry = this.Data.ReadFixedUInt32(child);

				GetEntryAt(index, entry, offset, len, out result);
			}

			/// <summary>Retourne une JValue correspondant à un item à un index spécifique</summary>
			/// <param name="index">Index de l'entry (dans la table des jentries)</param>
			/// <param name="entry">Valeur de l'entrée dans la table des jentries</param>
			/// <param name="dataOffset">Offset (relatif) de début des données (ou 0 si pas de données)</param>
			/// <param name="dataLen">Taille des données (ou 0 si pas de données)</param>
			/// <param name="result">Entry correspondante</param>
			private void GetEntryAt(int index, uint entry, int dataOffset, int dataLen, out JValue result)
			{
				int baseAddr = this.BaseAddress;
				if (!this.Data.Fits(baseAddr + dataOffset, dataLen)) throw ThrowHelper.FormatException($"Malformed jsonb entry: data for entry {index} would be outside of the bounds of the container");

				JType type = GetEntryType(entry);
				switch (type)
				{
					case JType.String: // les chaînes sont encodées en UTF-8
					case JType.Integer:	// les entiers en binary ne sont pas alignés
					case JType.Numeric:	// les nombres réels sont encodés par leur literal en base 10
					{
						result = new JValue(type, this.Data.Slice(baseAddr + dataOffset, dataLen));
						break;
					}
					case JType.True:
					case JType.False:
					case JType.Null:
					{
						result = new JValue(type, default);
						break;
					}
					case JType.Container:
					{
						// les containers sont alignés sur 32 bits
						int padLen = ComputePadding(baseAddr + dataOffset);
						if (padLen > dataLen) throw ThrowHelper.FormatException($"Invalid jsonb entry: padding is larger than reported value size at array index {index}.");
						result = new JValue(JType.Container, this.Data.Slice(baseAddr + dataOffset + padLen, dataLen - padLen));
						break;
					}
					default:
					{
						throw ThrowHelper.FormatException($"Invalid jsonb entry: invalid entry type at array index {index}.");
					}
				}
			}

			/// <summary>
			/// Get the offset of the variable-length portion of a Jsonb node within the variable-length-data part of its container.
			/// The node is identified by index within the container's JEntry array.
			/// </summary>
			/// <param name="index">Index de l'entrée</param>
			/// <returns>Offset de l'entrée (en octets)</returns>
			[Pure]
			private int GetJsonbOffset(int index)
			{
				int offset = 0;
				if (index > 0)
				{
					int ptr = checked(JCONTAINER_CHILDREN_OFFSET + (index - 1) * JENTRY_SIZEOF);
					for (int i = index - 1; i >= 0; i--)
					{
						uint value = this.Data.ReadFixedUInt32(ptr);
						int len = GetEntryOffsetOrLength(value);
						offset = checked(offset + len);
						if (GetEntryHasOffset(value)) break;
						ptr -= JENTRY_SIZEOF;
					}
				}
				return offset;
			}

			/// <summary>
			/// Get the length of the variable-length portion of a Jsonb node.
			/// The node is identified by index within the container's JEntry array.
			/// </summary>
			/// <param name="index">Index de l'entrée</param>
			/// <param name="offset">Offset de l'entrée (calculée via <see cref="GetJsonbOffset"/>)</param>
			/// <returns>Taille de l'entrée (en octets)</returns>
			[Pure]
			private int GetJsonbLength(int index, int offset)
			{
				/*
				 * If the length is stored directly in the JEntry, just return it.
				 * Otherwise, get the begin offset of the entry, and subtract that from
				 * the stored end+1 offset.
				 */

				uint value = this.Data.ReadFixedUInt32(checked(JCONTAINER_CHILDREN_OFFSET + index * JENTRY_SIZEOF));
				int len = GetEntryOffsetOrLength(value);
				if (GetEntryHasOffset(value)) len = checked(len - offset);

				return len;
			}

			private JsonArray DecodeArray(StringTable? table)
			{
				if (!this.IsArray) throw ThrowHelper.InvalidOperationException("Specified jsonb container is not an array.");

				int numElems = this.Count;
				if (this.Data.Length < checked(4 + numElems * 4)) throw ThrowHelper.FormatException($"Json container is too small for an array of size {numElems}.");

				// décode les items un par un
				var arr = new JsonValue[numElems];
				int childOffset = JCONTAINER_CHILDREN_OFFSET;
				int dataOffset = 0;
				for (int i = 0; i < numElems; i++)
				{
					uint entry = this.Data.ReadFixedUInt32(childOffset);

					int dataLen = (int) (entry & JENTRY_OFFLENMASK);
					if ((entry & JENTRY_HAS_OFF) != 0)
					{
						dataLen -= dataOffset;
					}

					GetEntryAt(i, entry, dataOffset, dataLen, out JValue item);
					arr[i] = item.ToJsonValue(table);
					dataOffset += dataLen;
					childOffset += JENTRY_SIZEOF;
				}
				return new JsonArray(arr, numElems, readOnly: false);
			}

			private JsonObject DecodeObject(StringTable? table)
			{
				if (!this.IsObject) throw ThrowHelper.InvalidOperationException("Specified jsonb container is not an object.");

				int numPairs = this.Count;
				if (this.Data.Length  < checked(4 + numPairs * 2 * 4)) throw ThrowHelper.FormatException($"Json container is too small for an object of size {numPairs}.");

				//TODO: utiliser un cache pour les buffers!
				var keys = new string[numPairs];
				var values = new JsonValue[numPairs];

				//TODO: parser key et valeurs séparément
				//TODO: scanner les entries, plutôt que d'appeler GetArrayEntry(..) a chaque fois

				// décodage des clés
				for (int i = 0; i < numPairs; i++)
				{
					GetContainerEntry(i, numPairs * 2, out JValue item);
					keys[i] = item.ToKey(table);
				}

				// décodage des valeurs
				for(int i = 0; i< numPairs; i++)
				{
					GetContainerEntry(i + numPairs, numPairs * 2, out JValue item);
					values[i] = item.ToJsonValue(table);
				}

				//note: by default we will create immutable objects!
				//REVIEW: if we need to make this configurable, we would need to add a way to pass settings to this method!
				var map = new Dictionary<string, JsonValue>(numPairs, StringComparer.Ordinal);
				for (int i = 0; i < numPairs; i++)
				{
					map[keys[i]] = values[i];
				}
				return new JsonObject(map, readOnly: false);
			}

			public JsonValue ToJsonValue(StringTable? table = null)
			{
				return this.IsArray ? DecodeArray(table)
					: this.IsObject ? DecodeObject(table)
					: ThrowInvalidJsonbContainer();
			}

			[ContractAnnotation("=> halt")]
			private static JsonValue ThrowInvalidJsonbContainer()
			{
				throw new InvalidOperationException("Invalid jsonb container");
			}

			public unsafe bool GetEntryByName(JLookupKey* key, out JValue result)
			{
				Contract.Debug.Requires(key != null);

				if (!this.IsObject) throw ThrowHelper.InvalidOperationException("Specified jsonb container is not an object.");

				int numPairs = this.Count;
				if (this.Data.Length < 4 + numPairs * 2 * 4) throw ThrowHelper.FormatException($"Json container is too small for an object of size {numPairs}.");

				// les clés sont de 0 à numParis -1

				int childrenOffset = JCONTAINER_CHILDREN_OFFSET;
				int dataOffset = 0;


				//HACKHACK: TODO: binarysearch? hashmap?
				for (int i = 0; i < numPairs; i++)
				{
					uint entry = this.Data.ReadFixedUInt32(childrenOffset);
					int dataLen = (int) (entry & JENTRY_OFFLENMASK);
					if ((entry & JENTRY_HAS_OFF) != 0)
					{
						dataLen -= dataOffset;
					}

					GetEntryAt(i, entry, dataOffset, dataLen, out JValue item);

					if (item.Equals(key))
					{ // match
						GetContainerEntry(i + numPairs, numPairs * 2, out result);
						return true;
					}

					dataOffset += dataLen;
					childrenOffset += JENTRY_SIZEOF;
				}

				result = default(JValue);
				return false;
			}

			/// <summary>Aligne un offset aux prochains 32 bits</summary>
			/// <param name="offset">7</param>
			/// <returns>8</returns>
			private static int Aligned(int offset)
			{
				// 4 => 4
				// 5 => 8
				// 6 => 8
				// 7 => 8
				// 8 => 8
				return (offset + 3) & ~3;
			}

			/// <summary>Détermine le nombre d'octets nécessaires pour padder jusqu'au prochains 32-bits</summary>
			/// <param name="offset">7</param>
			/// <returns>1 (car 7 + 1 = 8)</returns>
			private static int ComputePadding(int offset)
			{
				// 4 => 0
				// 5 => 3
				// 6 => 2
				// 7 => 1
				// 8 => 0
				return (~offset + 1) & 3;
			}

		}

		[StructLayout(LayoutKind.Sequential)]
		private ref struct JValue
		{
			/// <summary>Type (bool, string, ...)</summary>
			public readonly JType Type;

			/// <summary>Pointeur vers le début des données (correctement aligné) ou null si pas de données</summary>
			public readonly ReadOnlySpan<byte> Value;

			public JValue(JType type, ReadOnlySpan<byte> value)
			{
				this.Type = type;
				this.Value = value;
			}

			public JsonValue ToJsonValue(StringTable? table)
			{
				//TODO: on a besoin d'une StringTable pour le parsing des strings et numerics

				switch (this.Type)
				{
					case JType.String:
					{ // Les chaînes sont encodées en UTF-8
						return JsonString.Return(this.Value.DecodeUtf8String(table));
					}
					case JType.Numeric:
					{ // Les nombres sont encodés sous forme de chaîne ASCII

						var value = this.Value;
						int len = this.Value.Length;
						switch (len)
						{
							case 0:
							{
								return JsonNumber.Zero;
							}
							case 1:
							{
								int b = value[0];
								if (IsDigit(b)) return JsonNumber.Return(b - 48);
								break;
							}
							case 2:
							{
								int b1 = value[0];
								int b2 = value[1];
								if (b1 == '-' && IsDigit(b2)) return JsonNumber.Return(-(b2 - 48));
								if (IsDigit(b1) && IsDigit(b2)) return JsonNumber.Return((b1 - 48) * 10 + (b2 - 48));
								break;
							}
						}
						//TODO: use string table
						return JsonNumber.Parse(value);
					}
					case JType.True:
					{
						return JsonBoolean.True;
					}
					case JType.False:
					{
						return JsonBoolean.False;
					}
					case JType.Null:
					{
						return JsonNull.Null;
					}
					case JType.Container:
					{
						var container = new JContainer(this.Value);
						return container.ToJsonValue(table);
					}
					case JType.Integer:
					{ // entier signé jusqu'à 64 bits
						long x = this.Value.DecodeVarInt64();
						return JsonNumber.Return(x);
					}
					default:
					{
						throw new InvalidOperationException("Invalid jsonb value type");
					}
				}
			}

			public string ToKey(StringTable? table)
			{
				switch (this.Type)
				{
					case JType.String:
					{
						return this.Value.DecodeUtf8String(table);
					}
					case JType.Numeric:
					{
						//TODO: cache pour les petits entiers?
						return this.Value.DecodeAsciiString(table);
					}
					case JType.Integer:
					{ // entier signé jusqu'à 64 bits
						return CrystalJsonFormatter.NumberToString(this.Value.DecodeVarInt64());
					}
					default:
					{
						throw ThrowHelper.FormatException($"Cannot convert jsonb value of type {this.Type} into a key");
					}
				}
			}

			public unsafe bool Equals(JLookupKey* key)
			{
				Contract.Debug.Requires(key != null);
				// pour l'instant, on ne gère que la comparaison avec des strings
				if (this.Type != JType.String) throw ThrowHelper.InvalidOperationException($"Cannot compare objet key with entry of type {this.Type}");

				return new ReadOnlySpan<byte>(key->KeyBytes, key->KeyLength).SequenceEqual(this.Value);
			}

			public bool Equals(JsonValue value)
			{
				//TODO: OPTIMIZE!
				return ToJsonValue(null).Equals(value);
			}

			public bool Equals(string? value)
			{
				if (value == null) return this.Type == JType.Null;
				//TODO: optimize!
				return this.Type == JType.String && this.Value.DecodeUtf8String(null) == value;
			}

			public bool Equals(int value)
			{
				//TODO: optimize!
				return ToJsonValue(null) is JsonNumber num && num.Equals(value);
			}

			public bool Equals(long value)
			{
				//TODO: optimize!
				return ToJsonValue(null) is JsonNumber num && num.Equals(value);
			}

			public bool Equals(float value)
			{
				//TODO: optimize!
				return ToJsonValue(null) is JsonNumber num && num.Equals(value);
			}

			public bool Equals(double value)
			{
				//TODO: optimize!
				return ToJsonValue(null) is JsonNumber num && num.Equals(value);
			}

			public bool Equals(Guid value)
			{
				//TODO: optimize!
				return ToJsonValue(null) is JsonString str && str.Equals(value);
			}

		}

		[StructLayout(LayoutKind.Sequential)]
		private unsafe struct JLookupKey
		{
			/// <summary>Clé sous forme binaire (UTF8)</summary>
			public readonly byte* KeyBytes;
			/// <summary>Taille de la clé sous forme binaire (UTF8)</summary>
			public readonly int KeyLength;
			/// <summary>Hashcode de la clé</summary>
			public readonly int HashCode;

			public JLookupKey(byte* keyBytes, int keyLength, int hashCode)
			{
				this.KeyBytes = keyBytes;
				this.KeyLength = keyLength;
				this.HashCode = hashCode;
			}

		}

		[StructLayout(LayoutKind.Sequential)]
		private struct JDocument
		{
			private readonly ReadOnlyMemory<byte> Data;

			public JDocument(ReadOnlyMemory<byte> data, uint header, uint extraFlags, int offset)
			{
				this.Data = data;
				this.Header = header;
				this.ExtraFlags = extraFlags;
				this.Offset = offset;
			}

			public static JDocument Parse(ReadOnlyMemory<byte> buffer)
			{
				var data = buffer.Span;

				// ROOT_ENTRY + ROOT_HEADER = (empty array or object)
				if (data.Length < 8) throw ThrowHelper.FormatException("Buffer is too small to be a valid jsonb document.");

				int pos = 0;

				uint header = data.ReadFixedUInt32(pos);
				pos += 4;

				int size = (int) (header & HEADER_SIZE_MASK);
				// vérifie que le buffer est assez grand pour contenir le root container
				if ((uint) size > data.Length) throw ThrowHelper.FormatException($"The reported size of the jsonb container ({size}) is larger than the size of the buffer in memory ({data.Length})");

				if ((header & HEADER_FLAGS_RESERVED) != 0)
				{ // les extra flags doivent être à 0 pour le moment
					throw ThrowHelper.FormatException("Unsupported extra flags in header. Either this is not a jsonb document, or it is a newer format version.");
				}

				uint extraFlags = 0;
				if ((header & HEADER_FLAGS_EXTRA) != 0)
				{ // le header est immédiatement suivi de 32 extra flags
					extraFlags = data.ReadFixedUInt32(pos);
					pos += 4;
				}

				return new JDocument(buffer, header, extraFlags, pos);
			}

			private readonly uint Header;

			public readonly uint ExtraFlags;

			public readonly int Offset;

			/// <summary>Taille du document (incluant les headers)</summary>
			public uint Size
			{
				[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
				get => this.Header & HEADER_SIZE_MASK;
			}

			private JContainer GetRoot()
			{
				return new JContainer(this.Data.Span.Slice(this.Offset));
			}

			public JsonValue ToJsonValue(StringTable? table)
			{
				var root = GetRoot();

				if (root.IsScalar)
				{ // Les scalars sont stockés dans une array de de taille 1
					if (!root.IsArray) throw ThrowHelper.FormatException("Jsonb scalar container must be an array");
					if (root.Count != 1) throw ThrowHelper.FormatException("Jsonb scalar container must have exactly one element");
					root.GetContainerEntry(0, 1, out JValue scalar); //TODO: buffer size
					return scalar.ToJsonValue(table);
				}

				return root.ToJsonValue(table);
			}

			public bool TryLookup(string path, out JValue value)
			{
				Contract.NotNull(path);

				var root = GetRoot();

				//HACKHACK: pour l'instant on ne supporte que des path direct property,
				// le conteneur doit être un object
				Contract.Debug.Requires(root.IsObject,"Only object container supported for now!");

				// pour éviter d'allouer de la mémoire, on compare directement la clé (string) avec les bytes du jsonb (utf8)
				//HACKHACK: faire une vraie comparaison string vs byte[] ?
				if (path.Length > 64 * 1024) throw ThrowHelper.InvalidOperationException("Key size is too large (> 64K)");

				// note: Normalize() vérifie si la clé n'est pas en ASCII, et si oui retourne la référence elle-même, donc on ne devrait avoir d'allocation que si path contient une chaîne unicode non canonical (ie: jamais en théorie)
				// de plus, la CLR a un flag dans les headers des COMString, qui est calculé lors du premier appel a COMString::IsASCII(), ce qui fait que Normalize() est virtuellement gratuit.
				path = path.Normalize();

				unsafe
				{
					var enc = CrystalJson.Utf8NoBom;
					int maxKeyBytes = (enc.GetMaxByteCount(path.Length) + 7) & ~7;
					byte* keyBytes = stackalloc byte[maxKeyBytes];
					int keyLen;
					fixed (char* chars = path)
					{
						keyLen = enc.GetBytes(chars, path.Length, keyBytes, maxKeyBytes);
					}
					var key = new JLookupKey(keyBytes, keyLen, 1234); //TODO: hashcode!

					//TODO: utiliser un JPathTokenizer!
					if (!root.GetEntryByName(&key, out value))
					{
						return false;
					}
					return true;
				}
			}

			public JsonValue Select(string path, StringTable? table = null)
			{
				if (!TryLookup(path, out var value))
				{
					return JsonNull.Missing;
				}
				return value.ToJsonValue(table);
			}

		}

		#endregion

		#region Public Methods...

		/// <summary>Parse une section de buffer contenant un document jsonb, en une valeur JSON équivalente</summary>
		[Pure]
		public static JsonValue Decode(Slice buffer, StringTable? table = null)
		{
			if (buffer.Count == 0) return JsonNull.Missing;
			return JDocument.Parse(buffer.Memory).ToJsonValue(table);
		}

		/// <summary>Parse une section de buffer contenant un document jsonb, en une valeur JSON équivalente</summary>
		[Pure]
		public static JsonValue Decode(ReadOnlyMemory<byte> buffer, StringTable? table = null)
		{
			if (buffer.Length == 0) return JsonNull.Missing;
			return JDocument.Parse(buffer).ToJsonValue(table);
		}

		[Pure]
		public static JsonValue Select(Slice buffer, string path, StringTable? table = null)
		{
			if (buffer.Count == 0) return JsonNull.Missing;
			return JDocument.Parse(buffer.Memory).Select(path, table);
		}

		[Pure]
		public static JsonValue Select(ReadOnlyMemory<byte> buffer, string path, StringTable? table = null)
		{
			if (buffer.Length == 0) return JsonNull.Missing;
			return JDocument.Parse(buffer).Select(path, table);
		}

		[Pure]
		public static bool Test(Slice buffer, string path, JsonValue value)
		{
			if (buffer.Count == 0) return false;
			//TODO: implementation that will not allocate and directly compare 'value' with the bytes in the buffer!
			var doc = JDocument.Parse(buffer.Memory);
			return doc.TryLookup(path, out var actual) && actual.Equals(value);
		}

		[Pure]
		public static bool Test(ReadOnlyMemory<byte> buffer, string path, JsonValue value)
		{
			if (buffer.Length == 0) return false;
			//TODO: implementation that will not allocate and directly compare 'value' with the bytes in the buffer!
			var doc = JDocument.Parse(buffer);
			return doc.TryLookup(path, out var actual) && actual.Equals(value);
		}

		[Pure]
		public static bool Test(Slice buffer, string path, string? value) => buffer.Count != 0 && (JDocument.Parse(buffer.Memory).TryLookup(path, out var j) ? j.Equals(value) : value == null);

		[Pure]
		public static bool Test(ReadOnlyMemory<byte> buffer, string path, string? value) => buffer.Length != 0 && (JDocument.Parse(buffer).TryLookup(path, out var j) ? j.Equals(value) : value == null);

		[Pure]
		public static bool Test(Slice buffer, string path, int value) => buffer.Count != 0 && JDocument.Parse(buffer.Memory).TryLookup(path, out var j) && j.Equals(value);

		[Pure]
		public static bool Test(ReadOnlyMemory<byte> buffer, string path, int value) => buffer.Length != 0 && JDocument.Parse(buffer).TryLookup(path, out var j) && j.Equals(value);

		[Pure]
		public static bool Test(Slice buffer, string path, long value) => buffer.Count != 0 && JDocument.Parse(buffer.Memory).TryLookup(path, out var j) && j.Equals(value);

		[Pure]
		public static bool Test(ReadOnlyMemory<byte> buffer, string path, long value) => buffer.Length != 0 && JDocument.Parse(buffer).TryLookup(path, out var j) && j.Equals(value);

		[Pure]
		public static bool Test(Slice buffer, string path, float value) => buffer.Count != 0 && JDocument.Parse(buffer.Memory).TryLookup(path, out var j) && j.Equals(value);

		[Pure]
		public static bool Test(ReadOnlyMemory<byte> buffer, string path, float value) => buffer.Length != 0 && JDocument.Parse(buffer).TryLookup(path, out var j) && j.Equals(value);

		[Pure]
		public static bool Test(Slice buffer, string path, double value) => buffer.Count != 0 && JDocument.Parse(buffer.Memory).TryLookup(path, out var j) && j.Equals(value);

		[Pure]
		public static bool Test(ReadOnlyMemory<byte> buffer, string path, double value) => buffer.Length != 0 && JDocument.Parse(buffer).TryLookup(path, out var j) && j.Equals(value);

		[Pure]
		public static bool Test(Slice buffer, string path, Guid value) => buffer.Count != 0 && JDocument.Parse(buffer.Memory).TryLookup(path, out var j) && j.Equals(value);

		[Pure]
		public static bool Test(ReadOnlyMemory<byte> buffer, string path, Guid value) => buffer.Length != 0 && JDocument.Parse(buffer).TryLookup(path, out var j) && j.Equals(value);

		/// <summary>Encode une valeur JSON en document jsonb</summary>
		/// <returns>Tableau contenant le document jsonb correspondant à <paramref name="value"/></returns>
		public static byte[] Encode(JsonValue value, int capacity = 0)
		{
			Contract.NotNull(value);

			using (var writer = new Writer(capacity <= 0 ? 4096 : capacity))
			{
				writer.WriteDocument(value);
				return writer.GetBytes();
			}
		}

		public static Slice EncodeBuffer(JsonValue value, int capacity = 0)
		{
			Contract.NotNull(value);

			using (var writer = new Writer(capacity <= 0 ? 4096 : capacity))
			{
				writer.WriteDocument(value);
				return writer.GetBuffer();
			}
		}

		public static Slice EncodeBuffer(JsonValue value, ref byte[]? buffer, int capacity = 0)
		{
			Contract.NotNull(value);

			using (var writer = new Writer(capacity <= 0 ? 4096 : capacity, buffer))
			{
				writer.WriteDocument(value);
				var res = writer.GetBuffer();
				buffer = res.Array;
				return res;
			}
		}

		#endregion

		#region Internal Helpers...

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		private static JType GetEntryType(uint value)
		{
			return (JType)((value & JENTRY_TYPEMASK) >> JENTRY_OFFLENBITS);
		}

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		private static int GetEntryOffsetOrLength(uint value)
		{
			return (int) (value & JENTRY_OFFLENMASK);
		}

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		private static bool GetEntryHasOffset(uint value)
		{
			return (value & JENTRY_HAS_OFF) != 0;
		}

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		private static bool IsDigit(int b)
		{
			const int BIAS = '0';
			return (uint)(b - BIAS) <= '9' - BIAS;
		}

		#endregion

		#region Writing Jsonb...

		public sealed class Writer : IDisposable
		{
			const int HEADER_SIZE = 4;

			private SliceWriter m_output;
			//TODO: pour l'instant on utilise un BWB, mais il faudrait implémenter un vrai BufferPool spécialisé pour ce genre de cas...

			public Writer(int capacity, byte[]? buffer = null)
			{
				buffer = buffer?.Length >= capacity ? buffer : new byte[capacity];
				m_output = new SliceWriter(buffer);
			}

			public void Dispose()
			{
				//TODO?
			}

			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			public byte[] GetBytes()
			{
				return m_output.GetBytes();
			}

			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			public Slice GetBuffer()
			{
				return m_output.ToSlice();
			}

			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			public void Clear()
			{
				m_output.Position = 0;
			}

			/// <summary>Aligne un offset aux prochains 32 bits</summary>
			/// <param name="offset">7</param>
			/// <returns>8</returns>
			[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
			private static int Aligned(int offset)
			{
				// 4 => 4
				// 5 => 8
				// 6 => 8
				// 7 => 8
				// 8 => 8
				return (offset + 3) & ~3;
			}

			/// <summary>Encode une valeur JSON en document jsonb</summary>
			/// <returns>Nombre d'octets écrits dans le buffer output</returns>
			public int WriteDocument(JsonValue value)
			{

				Contract.NotNull(value);

				if (JsonValue.IsJsonMissing(value))
				{ // missing => empty
					return 0;
				}

				// skip la place pour le header (écrasé a la fin quand on aura la taille finale
				m_output.Skip(HEADER_SIZE);

				// sérialise la valeur (récursif, retourne quand tout est terminé)
				WriteValue(value, 0);

				/*
				 * Note: the JEntry of the root is discarded. Therefore the root
				 * JsonbContainer struct must contain enough information to tell what kind
				 * of value it is.
				 */
				int len = m_output.Position;

				// varlena, uncompressed
				if (len > HEADER_SIZE_MASK) throw ThrowHelper.InvalidOperationException($"This jsonb document ({len} bytes) exceeds the maximum allowed size ({len} > {HEADER_SIZE_MASK} bytes).");
				m_output.PatchUInt32(0, ((uint)len & HEADER_SIZE_MASK));

				return len;
			}

			public uint WriteValue(JsonValue value, int level)
			{
				Contract.Debug.Requires(value != null && level >= 0);

				JsonType type;
				switch ((type = value.Type))
				{

					case JsonType.Array:
					{
						return WriteArray(value, level);
					}

					case JsonType.Object:
					{
						return WriteObject((JsonObject) value, level);
					}

					default:
					{
						if (level == 0)
						{
							//HACKHACK: simule une array de taille 1
							return WriteArray(value, 0);
						}

						return WriteScalar(value, type);
					}
				}
			}

			public uint WriteScalar(JsonValue value, JsonType type)
			{
				Contract.Debug.Requires(value != null);
				switch (type)
				{
					case JsonType.Null:
					{
						return JENTRY_TYPE_NULL;
					}
					case JsonType.String:
					{
						int len = AppendUtf8String(value.ToString());
						return JENTRY_TYPE_STRING | (uint) len;
					}
					case JsonType.DateTime:
					{
						//note: attention: ca peut être un DateTime ou un DateTimeOffset !
						//REVIEW: PERF: plutot que d'allouer une string, on pourrait sérialiser directement en byte la valeur!
						// les dates sont toujours en ASCII
						int len = AppendAsciiString(value.ToString());
						return JENTRY_TYPE_STRING | (uint)len;
					}
					case JsonType.Boolean:
					{
						return value.ToBoolean() ? JENTRY_TYPE_TRUE : JENTRY_TYPE_FALSE;
					}
					case JsonType.Number:
					{
						var num = (JsonNumber) value;
						if (!num.IsDecimal)
						{
							if (num.IsBetween(long.MinValue, long.MaxValue))
							{ // nombre entier sur 64-bit (signé)
								long x = num.ToInt64();
								int sz = AppendVarInt64(x);
								return JENTRY_TYPE_INTEGER | (uint) sz;
							}
						}

						//note: le literal est déjà calculé dans un JsonNumber, donc on peut directement le copier tel quel
						//BUGBUG: et si le literal est "1.0" ?
						//note: contrairement à hstore2, on ne pad pas les nombres sur 32 bits!
						int len = AppendAsciiString(value.ToString());
						return JENTRY_TYPE_NUMERIC | (uint) len;
					}
					default:
					{
						throw ThrowHelper.NotSupportedException($"Invalid JSON scalar of type {type}");
					}
				}
			}

			public uint WriteArray(JsonValue arrayOrScalar, int level)
			{
				int baseOffset = m_output.Position;

				// les containers sont toujours alignés sur 32 bits
				m_output.Align(4); // 32-bit

				JsonArray? array;
				int numElems;
				if (arrayOrScalar is not JsonArray jsonArr)
				{ // fait comme si c'était une array de taille 1
					if (level > 0 || arrayOrScalar is JsonObject) throw ThrowHelper.InvalidOperationException("Should only be called with a scalar for the top level");
					array = null;
					numElems = 1;
				}
				else
				{
					array = jsonArr;
					numElems = array.Count;
				}
				Contract.Debug.Assert(array != null || numElems == 1);

				if ((uint) numElems > JENTRY_OFFLENMASK) throw FailContainerTooManyElements();

				uint header = (uint) numElems | JCONTAINER_FLAG_ARRAY;
				if (array == null) header |= JCONTAINER_FLAG_SCALAR;
				m_output.WriteFixed32(header);

				// réserve la place pour les entrées de chaque élément
				int jentryOffset = m_output.Skip(numElems * JENTRY_SIZEOF);

				// copie les données, en updatant chaque entrée
				int totalLen = 0;
				for (int i = 0; i < numElems; i++)
				{
					var elem = array != null ? array[i] : arrayOrScalar;

					uint meta = WriteValue(elem, level + 1);

					totalLen = checked(totalLen + GetEntryOffsetOrLength(meta));
					if (totalLen > JENTRY_OFFLENMASK) throw FailContainerSizeTooBig();

					if ((i % JCONTAINER_OFFSET_STRIDE) == 0)
					{ // Convert each JB_OFFSET_STRIDE'th length to an offset.
						meta = (meta & JENTRY_TYPEMASK) | (uint) totalLen | JENTRY_HAS_OFF;
					}

					m_output.PatchUInt32(jentryOffset, meta);
					jentryOffset += JENTRY_SIZEOF;
				}

				//update la taille total (pour inclure le padding éventuel)
				totalLen = m_output.Position - baseOffset;

				if ((uint) totalLen > JENTRY_OFFLENMASK) throw FailContainerSizeTooBig();

				return JENTRY_TYPE_CONTAINER | (uint) totalLen;
			}

			public uint WriteObject(JsonObject map, int level)
			{
				int baseOffset = m_output.Position;

				// Un objet est l'équivalent a une KeyValuePair<K,V>[], mais est stocké comme une array avec lés clés d'abord, puis les valeurs:
				// Object ~= Array<Pair<K,V>> => [ K0, K1, ..., KN-1, V0, V1, ..., VN-1 ]

				// Le nombre maximum d'éléments dans un object est de 2^24 - 1.
				// Les objets contenant beaucoup de clés comportent également une mini-hashtable à la fin, qui permet de retrouver plus rapidement une entrée via son nom.
				// La hashtable comporte la liste triée des hashcode de chaque clés (pour pouvoir faire un binary search), ainsi que la map d'index pour retrouver l'entrée correspondante).
				// - Les hash sont stockés sur 16 bits
				// - La liste des index est stockées sur 1, 2 ou 3 octets, en fonction du nombre de pairs (<= 256 sur 1 octets, <= 65536 sur 2 octets, > 65536 sur 3 octets).

				// les containers sont toujours alignés sur 32 bits
				m_output.Align(4);

				int numPairs = map.Count;
				if ((uint) numPairs >= (1 << 24)) throw FailContainerTooManyElements(); // max 16 777 215 keys per object!

				// les objets ayant 8 éléments ou plus contiennent un hashcode pour chaque clé
				bool hashed = (numPairs >= 8);
				int hashIdxLen = 0, hashMapSize = 0;
				if (hashed)
				{
					// la taille de l'index dépend du nombre d'items
					hashIdxLen = JContainer.GetIndexMapStride(numPairs);
					hashMapSize = numPairs * (4 + hashIdxLen); // 32 bit hashcode + index par entrée
					hashMapSize = Aligned(hashMapSize); // la hashMap est paddée sur 32 bits
				}

				// estimation de la capacité nécessaire pour stocker l'objet
				// - on essaye de resizer le buffer a l'avance pour que le remplissage des jentries + hashmap se fasse le plus vite possible
				// - on part sur une hypothèse de 16 bytes par entrées (key + value)
				int entriesSize = numPairs * 2 * JENTRY_SIZEOF;
				m_output.EnsureBytes(4 + entriesSize + hashMapSize + numPairs * 16);

				// header du container
				uint header = (uint) numPairs | JCONTAINER_FLAG_OBJECT;
				if (hashed) header |= JCONTAINER_FLAG_HASHED;
				m_output.WriteFixed32(header);

				// réserve la place pour les jentries
				int jEntryOffset = m_output.Position;
				m_output.Position += entriesSize;
				Contract.Debug.Assert(m_output.Position <= m_output.Capacity, "Buffer should already be large enough");

				// réserve la place pour la hashmap (optionnelle)
				int hashesOffset = 0;
				if (hashed)
				{ // la hashmap suit directement les jentries (et avant les key+values)
					hashesOffset = m_output.Position;
					m_output.Position += hashMapSize;
					Contract.Debug.Assert(m_output.Position <= m_output.Capacity, "Buffer should already be large enough");
				}

				//TODO: trouver un moyen de trier les hash+idx depuis la stack directement?
				// => pour le moment on doit passer par Array.Sort(..) pour trier l'array de hash et index séparément, donc on est obligé d'allouer deux arrays :(
				int[]? hashes = null, indexes = null;
				if (hashed)
				{
					hashes = ArrayPool<int>.Shared.Rent(numPairs);
					indexes = ArrayPool<int>.Shared.Rent(numPairs);
				}

				// copie d'abord toutes les clés
				int totalLen = 0;
				int index = 0;
				foreach (var key in map.Keys)
				{
					// note: pour pouvoir comparer les clés directement en UTF-8, il est important de les stocker sous forme Unicode canonical!
					// normalement, String.Normalize() retourne la chaîne telle quelle si elle est en ASCII, et la CLR optimize le test via un flag dans le header des COMString.
					// Le coup de la normalization est en théorie nulle dans le cas normal (clés ASCII), ou si la clé est de l'UTF-8 canonical.
					var normalizedKey = key.Normalize();

					// calcul le hashcode de la clé
					if (hashed)
					{
						hashes![index] = StringTable.Hash.GetFnvHashCode(normalizedKey.AsSpan());
					}

					//note: La source est un JsonOjbect arbitraire, qui peut contenir de l'UTF-8
					uint meta = (uint) m_output.WriteString(normalizedKey, CrystalJson.Utf8NoBom);
					totalLen += (int) meta;
					if ((uint) totalLen > JENTRY_OFFLENMASK) throw FailContainerSizeTooBig();

					if ((index % JCONTAINER_OFFSET_STRIDE) == 0)
					{ // Convert each JB_OFFSET_STRIDE'th length to an offset.
						meta = (uint) totalLen | JENTRY_HAS_OFF | JENTRY_TYPE_STRING;
					}
					else
					{
						meta |= JENTRY_TYPE_STRING;
					}

					m_output.PatchUInt32(jEntryOffset, meta);
					jEntryOffset += JENTRY_SIZEOF;

					++index;
				}

				// copie ensuite toutes les valeurs
				foreach (var value in map.Values)
				{
					uint meta = WriteValue(value, level + 1);

					totalLen = checked(totalLen + GetEntryOffsetOrLength(meta));
					if ((uint) totalLen > JENTRY_OFFLENMASK) throw FailContainerSizeTooBig();

					if ((index % JCONTAINER_OFFSET_STRIDE) == 0)
					{ // Convert each JB_OFFSET_STRIDE'th length to an offset.
						meta = (meta & JENTRY_TYPEMASK) | (uint) totalLen | JENTRY_HAS_OFF;
					}

					m_output.PatchUInt32(jEntryOffset, meta);
					jEntryOffset += JENTRY_SIZEOF;

					++index;
				}

				// tri+store la hashmap (optionnel)
				if (hashed)
				{
					Contract.Debug.Assert(hashes != null && indexes != null);

					m_output.EnsureOffsetAndSize(hashesOffset, hashMapSize);

					// tri les hashes (et idxmap)
					for (int i = 0; i < numPairs; i++) indexes[i] = i;
					Array.Sort(hashes, indexes, 0, numPairs, null);

					// hashes (32 bits / entry)
					var hashSpan = hashes.AsSpan(0, numPairs);
					foreach(var h in hashSpan)
					{
						m_output.PatchInt32(hashesOffset, h);
						hashesOffset += 4;
					}

					var indexRange = indexes.AsSpan(0, numPairs);

					// idxmap (8-16-24 bits / entry)
					switch (hashIdxLen)
					{
						case 1:
						{
							var array = m_output.GetBufferUnsafe();
							foreach(var idx in indexRange)
							{
								array[hashesOffset++] = (byte) idx;
							}
							break;
						}
						case 2:
						{
							var array = m_output.GetBufferUnsafe();
							foreach(var idx in indexRange)
							{
								array[hashesOffset] = (byte) idx;
								array[hashesOffset + 1] = (byte) (idx >> 8);
								hashesOffset += 2;
							}
							break;
						}
						default:
						{
							Contract.Debug.Assert(hashIdxLen == 3);
							var array = m_output.GetBufferUnsafe();
							foreach(var idx in indexRange)
							{
								array[hashesOffset] = (byte) idx;
								array[hashesOffset + 1] = (byte) (idx >> 8);
								array[hashesOffset + 2] = (byte) (idx >> 16);
								hashesOffset += 3;
							}
							break;
						}
					}
				}

				//update la taille total (pour inclure le padding éventuel)
				totalLen = m_output.Position - baseOffset;
				if ((uint) totalLen > JENTRY_OFFLENMASK) throw FailContainerSizeTooBig();

				if (indexes != null) ArrayPool<int>.Shared.Return(indexes);
				if (hashes != null) ArrayPool<int>.Shared.Return(hashes);

				//TODO: ajouter le flag HASHED !!!!
				return JENTRY_TYPE_CONTAINER | (uint) totalLen;
			}

			private int AppendVarInt64(long value)
			{
				// on veut stocker les nombre positifs ET négatifs sur le moins de bytes possibles
				// -128..+127 => 1 byte
				// -32768..+32767 => 2 byte
				// etc...
				// Pour chaque valeur, le MSB est expanded automatiquement aux bytes manquants pour former la valeur sur 64 bits
				// exemples:
				//   +1 = 0x0000 0000 0000 0001 =>    01 (1 byte  with MSB 0) => (0000 0000 0000 00) . 01 => +1
				//   -1 = 0xFFFF FFFF FFFF FFFF =>    FF (1 byte  with MSB 1) => (FFFF FFFF FFFF FF) . FF => -1
				// +256 = 0x0000 0000 0000 0100 => 00 01 (2 bytes with MSB 0) => (0000 0000 0000) . 01 00 => +1
				// -257 = 0xFFFF FFFF FFFF FEFF => FF FE (2 bytes with MSB 1) => (FFFF FFFF FFFF) . FE FF => -257

				if (value >= 0)
				{ // positif

					if (value <= (1L << 7) - 1)
					{
						m_output.WriteByte((byte) value);
						return 1;
					}
					if (value <= (1L << 15) - 1)
					{
						m_output.WriteFixed16((short) value);
						return 2;
					}
					if (value <= (1L << 23) - 1)
					{
						m_output.WriteBytes((byte) value, (byte) (value >> 8), (byte) (value >> 16));
						return 3;
					}
					if (value <= (1L << 31) - 1)
					{
						m_output.WriteFixed32((int) value);
						return 4;
					}

					//TODO: version plus optimisée? (pas le courage de la faire maintenant)
					m_output.WriteFixed64(value);
					return 8;
				}
				else
				{ // négatif

					if (value >= -(1L << 7))
					{
						m_output.WriteByte((byte) value);
						return 1;
					}
					if (value >= -(1L << 15))
					{
						m_output.WriteFixed16((short) value);
						return 2;
					}
					if (value >= -(1L << 23))
					{
						m_output.WriteBytes((byte) value, (byte) (value >> 8), (byte) (value >> 16));
						return 3;
					}
					if (value >= -(1L << 31))
					{
						m_output.WriteFixed32((int) value);
						return 4;
					}

					//TODO: version plus optimisée? (pas le courage de la faire maintenant)
					m_output.WriteFixed64(value);
					return 8;
				}
			}

			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			private int AppendUtf8String(string value)
			{
				return m_output.WriteString(value, CrystalJson.Utf8NoBom);
			}

			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			private int AppendAsciiString(string value)
			{
				return m_output.WriteStringAscii(value);
			}

			[Pure, MethodImpl(MethodImplOptions.NoInlining)]
			private static Exception FailContainerTooManyElements()
			{
				return ThrowHelper.InvalidOperationException($"A jsonb container cannot have more than {JENTRY_OFFLENMASK} elements");
			}

			[Pure, MethodImpl(MethodImplOptions.NoInlining)]
			private static Exception FailContainerSizeTooBig()
			{
				return ThrowHelper.InvalidOperationException($"A jsonb container cannot exceed the maximum size of {JENTRY_OFFLENMASK} bytes");
			}
		}

		#endregion

	}

	/// <summary>Provides random access to any part of a Slice</summary>
	/// <remarks>Used to consume a Slice using random "seeks" instead of a linear scan (like <see cref="SliceWriter"/> for example)</remarks>
	internal static class SliceIndexer
	{
		//REVIEW: this is somewhat like a SliceWriter except that it is used in a random seek fashion, instead of linear scanning (like SliceWriter)

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool Fits(this ReadOnlySpan<byte> data, int offset, int count)
		{
			return checked(offset + count) <= data.Length;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private static void BoundCheck(this ReadOnlySpan<byte> data, uint offset, uint count)
		{
			uint end = checked(offset + count);
			if (end > data.Length) throw FailDataOutOfBound(offset, count);
		}

		[Pure, MethodImpl(MethodImplOptions.NoInlining)]
		private static Exception FailDataOutOfBound(uint offset, uint count)
		{
			return new IndexOutOfRangeException($"Attempted to read outside of the data segment (@{offset:N0}+{count:N0})");
		}

		/// <summary>Read the 4 bytes located at the specified <paramref name="offset"/> as an unsigned 32-bit Little-Endian</summary>
		/// <param name="data">Span from which to read</param>
		/// <param name="offset">Offset of the first byte to read</param>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static uint ReadFixedUInt32(this ReadOnlySpan<byte> data, int offset)
		{
			return MemoryMarshal.Read<uint>(data.Slice(offset, 4));
		}

		[Pure]
		public static string DecodeUtf8String(this ReadOnlySpan<byte> data, StringTable? table = null)
		{
			if (data.Length == 0) return string.Empty;
			unsafe
			{
				if (table == null)
				{
					fixed (byte* ptr = data)
					{
						return new string((sbyte*) ptr, 0, data.Length, CrystalJson.Utf8NoBom);
					}
				}
				return table.Add(data);
			}
		}

		[Pure]
		public static string DecodeAsciiString(this ReadOnlySpan<byte> data, StringTable? table = null)
		{
			if (data.Length == 0) return string.Empty;
			unsafe
			{
				if (table == null)
				{
					fixed (byte* ptr = data)
					{
						return new string((sbyte*) ptr, 0, data.Length);
					}
				}
				//note: AddUtf8 détecte si c'est effectivement de l'ASCII
				return table.Add(data);
			}
		}

		/// <summary>Décode une slice contenant un entier 64-bit signé à taille variable (0 à 8 bytes, little-endian)</summary>
		/// <returns>Valeur de l'entier</returns>
		public static long DecodeVarInt64(this ReadOnlySpan<byte> data)
		{
			int len = data.Length;
			if (len == 0) return 0;
			if ((uint) len > 8) throw ThrowHelper.FormatException("Jsonb integer value is too big");

			long value;
			unsafe
			{
				fixed (byte* ptr = data)
				{
					--len;
					value = ptr[len];
					while(len > 0)
					{
						--len;
						value = (value << 8) | ptr[len];
					}
				}
			}

			// consulte le msb pour restaurer les nombres négatifs
			//  length   : msb_mask         : negatif_fill
			//   1 :   8 : --------------80 : FFFFFFFFFFFFFF00
			//   2 :  16 : ------------8000 : FFFFFFFFFFFF0000
			//   3 :  24 : ----------800000 : FFFFFFFFFF000000
			//   4 :  32 : --------80000000 : FFFFFFFF00000000
			//   5 :  40 : ------8000000000 : FFFFFF0000000000
			//   6 :  48 : ----800000000000 : FFFF000000000000
			//   7 :  56 : --80000000000000 : FF00000000000000
			//   8 :  64 : 8000000000000000 : 0000000000000000 (pas besoin d'expand le sign)

			int bits = data.Length << 3;
			if (bits < 64 && (value & (1L << (bits - 1))) != 0)
			{ // negative!
				value |= -1L << bits;
			}
			return value;
		}

	}

}
