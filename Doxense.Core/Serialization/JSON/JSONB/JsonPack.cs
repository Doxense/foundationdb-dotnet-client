#region Copyright (c) 2005-2023 Doxense SAS
//
// All rights are reserved. Reproduction or transmission in whole or in part, in
// any form or by any means, electronic, mechanical or otherwise, is prohibited
// without the prior written consent of the copyright owner.
//
#endregion

namespace Doxense.Serialization.Json.Binary
{
	using System;
	using System.Runtime.CompilerServices;
	using Diagnostics.Contracts;
	using Doxense.Memory;
	using Doxense.Text;
	using JetBrains.Annotations;

	public static class JsonPack
	{

		#region Format Specifications...

		// JSON is a very simple compact binary representation of a JSON documents.
		// 
		// The objective is to generate the smallest payload possible (compared to the regular UTF-8 text representation) and make it as fast as possible for the encoder and decoder.
		// This means that there are multiple representations possible for each value, and the encoder is free to select the easiest one, depending on the situation.

		// Note: this format differs from JSONB (which is also binary): The goal of JSONB is to support very fast "partial" decoding (ie: "random acess") while JSONPack does not (requires complete parsing of document)
		// The choice depends on the use case;
		// - Transmit payloads that will be fully consumed by the other side: use JSONPack
		// - Store documents that will be frequently partially or randomly accessed by the ther side: use JSONB
		//
		// FORMAT:
		//
		// - All "segments" start with a type header which either encode a directly inline value (small intergers, null, true/false, ...) or a type prefix (with size either inline, or encoded as a VarInt immediately following the type prefix).
		// - Strings are always encoded as UTF-8 bytes, and the size always represent the number of BYTES (and *not* the number of chars)
		// - Arrays and Objects do not specify their length, and end with a "Stop" marker
		// - Small integers (-64 <= x <= 127), null, false/true are stored as single byte.
		// - Small strings are stored with a single type byte (which also encode the size)
		// - Floating point numbers can either be stored as a 64-bits IEEE float, OR a "LITERAL" if it is smaller (ex: "1.1" takes 3 bytes instead of 8 when encoded as a float64, same for "NaN")
		// - There is a dedicated type for strings of 36 characters, because a 128-bit GUID uses 36 characters when stored as text
		// 
		// Type Headers:
		// - 0x00 - 0x7F : SMALL POSITIVE INTEGERS (0..127)
		// - 0x80 NULL
		// - 0x81 FALSE
		// - 0x82 TRUE
		// - 0x83 ARRAY_START
		// - 0x84 ARRAY_STOP
		// - 0x85 ARRAY_EMPTY
		// - 0x86 OBJECT_START
		// - 0x87 OBJECT_STOP
		// - 0x88 OBJECT_EMPTY

		// - 0x90 FIXED_INT_1
		// - 0x91 FIXED_INT_2
		// - 0x92 FIXED_INT_3
		// - 0x93 FIXED_INT_4
		// - 0x94 FIXED_INT_8
		// - 0x95 FIXED_UINT_4
		// - 0x96 FIXED_UINT_8
		// - 0x97 FIXED_SINGLE_4
		// - 0x98 FIXED_DOUBLE_4
		// - 0x99 VAR_NUMBER_LITERAL (VarSize + ASCII_BYTES)
		// - 0x8D (reserved)
		// - 0x8E UUID 128 (16 bytes)
		// - 0x8F VAR_BINARY (followed by VarInt Size, then bytes)
		// - 0xA0 - 0xDC : SMALL_STRING (inlined size 0 .. 60)
		// - 0xDD STRING_SZ1
		// - 0xDE STRING_SZ2
		// - 0xDF STRING_SZ4
		// - 0xE0 - 0xFF : SMALL_NEGATIVE_INTEGERS (-32..-1)

		#endregion
		
		#region Constants...

		private enum TypeTokens
		{
			Invalid = -1,

			#region Small Positive Integers (0 .. 0x7F)

			Zero = 0,
			IntPos1  = 0x01,
			IntPos2  = 0x02,
			IntPos3  = 0x03,
			IntPos4  = 0x04,
			IntPos5  = 0x05,
			IntPos6  = 0x06,
			IntPos7  = 0x07,
			IntPos8  = 0x08,
			IntPos9  = 0x09,
			IntPos10 = 0x0A,
			IntPos11 = 0x0B,
			IntPos12 = 0x0C,
			IntPos13 = 0x0D,
			IntPos14 = 0x0E,
			IntPos15 = 0x0F,
			//TODO: all reamining ints!
			IntPos127 = 0x7F,

			#endregion

			#region Json Types
			Null = 0x80,
			False = 0x81,
			True = 0x82,
			ArrayStart = 0x83,
			ArrayStop = 0x84,
			ArrayEmpty = 0x85,
			ObjectStart = 0x86,
			ObjectStop = 0x87,
			ObjectEmpty = 0x88,
			//reserved: 0x89..0x8F
			#endregion

			#region Fixed Size Numbers (0x90..0x9F)
			// FixedInt uses expansion of the MSB: ie: '-1' => FF, -65536 => FFFF
			FixedInt1 = 0x90, // -128 .. +127
			FixedInt2 = 0x91, // -32_768 .. +32_767
			FixedInt3 = 0x92, // -8_388_608 .. +8_388_607
			FixedInt4 = 0x93, //  int.MinValue .. int.MaxValue
			FixedInt8 = 0x94, //  long.MinValue .. long.MaxValue
			FixedUInt4 = 0x95, //  0 .. uint.MaxValue
			FixedUInt8 = 0x96, //  0 .. ulong.MaxValue
			FixedSingle = 0x97, // 32-bits single
			FixedDouble = 0x98, // 64-bits double
			VarNumberLiteral = 0x99, // Number stored as String Literal (ex: "1.1", "NaN", "-42.0", "1.23E-42")
			//reserved: 0x9A..0x9F
			#endregion

			#region Small String (0xA0 .. 0xDF)

			StringEmpty  = 0xA0,
			SmlString1  = 0xA1,
			SmlString2  = 0xA2,
			SmlString3  = 0xA3,
			SmlString4  = 0xA4,
			SmlString5  = 0xA5,
			SmlString6  = 0xA6,
			SmlString7  = 0xA7,
			SmlString8  = 0xA8,
			SmlString9  = 0xA9,
			SmlString10 = 0xAA,
			SmlString11 = 0xAB,
			SmlString12 = 0xAC,
			SmlString13 = 0xAD,
			SmlString14 = 0xAE,
			SmlString15 = 0xAF,
			SmlString16 = 0xB0,
			SmlString17 = 0xB1,
			SmlString18 = 0xB2,
			SmlString19 = 0xB3,
			SmlString20 = 0xB4,
			SmlString21 = 0xB5,
			SmlString22 = 0xB6,
			SmlString23 = 0xB7,
			SmlString24 = 0xB8,
			SmlString25 = 0xB9,
			SmlString26 = 0xBA,
			SmlString27 = 0xBB,
			SmlString28 = 0xBC,
			SmlString29 = 0xBD,
			SmlString30 = 0xBE,
			SmlString31 = 0xBF,
			SmlString32 = 0xC0,
			SmlString33 = 0xC1,
			SmlString34 = 0xC2,
			SmlString35 = 0xC3,
			SmlString36 = 0xC4,
			SmlString37 = 0xC5,
			SmlString38 = 0xC6,
			SmlString39 = 0xC7,
			SmlString40 = 0xC8,
			SmlString41 = 0xC9,
			SmlString42 = 0xCA,
			SmlString43 = 0xCB,
			SmlString44 = 0xCC,
			SmlString45 = 0xCD,
			SmlString46 = 0xCE,
			SmlString47 = 0xCF,
			SmlString48 = 0xD0,
			SmlString49 = 0xD1,
			SmlString50 = 0xD2,
			SmlString51 = 0xD3,
			SmlString52 = 0xD4,
			SmlString53 = 0xD5,
			SmlString54 = 0xD6,
			SmlString55 = 0xD7,
			SmlString56 = 0xD8,
			SmlString57 = 0xD9,
			SmlString58 = 0xDA,
			SmlString59 = 0xDB,
			SmlString60 = 0xDC,

			/// <summary>String with (byte) size stored into the following 1 byte</summary>
			StringSize1 = 0xDD,
			/// <summary>String with (byte) size stored into the following 2 bytes</summary>
			StringSize2 = 0xDE,
			/// <summary>String with (byte) size stored into the following 4 bytes</summary>
			StringSize4 = 0xDF,

			#endregion

			#region Small Negative Integers (0xE0 .. 0xFF)
			
			IntNeg32 = 0xE0,
			IntNeg31 = 0xE1,
			IntNeg30 = 0xE2,
			IntNeg29 = 0xE3,
			IntNeg28 = 0xE4,
			IntNeg27 = 0xE5,
			IntNeg26 = 0xE6,
			IntNeg25 = 0xE7,
			IntNeg24 = 0xE8,
			IntNeg23 = 0xE9,
			IntNeg22 = 0xEA,
			IntNeg21 = 0xEB,
			IntNeg20 = 0xEC,
			IntNeg19 = 0xED,
			IntNeg18 = 0xEE,
			IntNeg17 = 0xEF,
			IntNeg16 = 0xF0,
			IntNeg15 = 0xF1,
			IntNeg14 = 0xF2,
			IntNeg13 = 0xF3,
			IntNeg12 = 0xF4,
			IntNeg11 = 0xF5,
			IntNeg10 = 0xF6,
			IntNeg9 = 0xF7,
			IntNeg8 = 0xF8,
			IntNeg7 = 0xF9,
			IntNeg6 = 0xFA,
			IntNeg5 = 0xFB,
			IntNeg4 = 0xFC,
			IntNeg3 = 0xFD,
			IntNeg2 = 0xFE,
			IntNeg1 = 0xFF,

			#endregion
		}

		private const byte JENTRY_SMALL_STRING_BASE = (byte) TypeTokens.StringEmpty;
		private const int  JENTRY_SMALL_STRING_MAX = 60;   // 0..60

		private const byte JENTRY_SMALL_NEG_INT_BASE = (byte) TypeTokens.IntNeg32;
		private const int  JENTRY_SMALL_NEG_INT_MAX = 32;  // -1..-32

		private const byte JENTRY_SMALL_POS_INT_BASE = (byte) TypeTokens.Zero;
		private const int  JENTRY_SMALL_POS_INT_MAX = 127; // 0..127

		#endregion

		#region Public Methods...

		/// <summary>Parse un buffer contenant un document jsonb, en une valeur JSON équivalente</summary>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static JsonValue Decode(byte[] buffer, CrystalJsonSettings? settings = null, StringTable? table = null)
		{
			Contract.NotNull(buffer);
			return Decode(buffer, 0, buffer.Length, settings, table);
		}

		/// <summary>Parse un buffer contenant un document jsonb, en une valeur JSON équivalente</summary>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static JsonValue Decode(byte[] buffer, int offset, int count, CrystalJsonSettings? settings = null, StringTable? table = null)
		{
			return Decode(buffer.AsSlice(offset, count), settings, table);
		}

		/// <summary>Parse une section de buffer contenant un document jsonb, en une valeur JSON équivalente</summary>
		[Pure]
		public static JsonValue Decode(Slice buffer, CrystalJsonSettings? settings = null, StringTable? table = null)
		{
			settings ??= CrystalJsonSettings.JsonCompact;

			var reader = buffer.ToSliceReader();
			int token = reader.ReadByte();
			if (token < 0) return JsonNull.Missing;
			var val = ReadValue(ref reader, token, settings);
			if (reader.HasMore) throw new FormatException($"Found {reader.Remaining:N0} extra byte(s) at end of encoded JSONPack value.");
			return val ?? JsonNull.Null;
		}

		private static TypeTokens TryEncodeFastPath(JsonValue value)
		{
			// fast path for generic values that encodes into a single value
			switch (value)
			{
				case JsonNull _:    return TypeTokens.Null;
				case JsonBoolean b: return b.Value ? TypeTokens.True : TypeTokens.False;
				case JsonNumber num:
				{
					if (!num.IsDecimal)
					{
						//TODO: PERF: need a "num.Fits_In_An_Int32()" or "num.Fits.In_An_Int64()" to simplify all this logic!
						if (num.IsBetween(0, JENTRY_SMALL_POS_INT_MAX))
						{ // 0..127
							return (TypeTokens) (JENTRY_SMALL_POS_INT_BASE + num.ToInt32());
						}
						if (num.IsBetween(-JENTRY_SMALL_NEG_INT_MAX, -1))
						{ // -32..-1
							return (TypeTokens) (JENTRY_SMALL_NEG_INT_BASE + JENTRY_SMALL_NEG_INT_MAX + num.ToInt32());
						}
					}
					break;
				}
				case JsonArray arr:
				{
					if (arr.Count == 0) return TypeTokens.ArrayEmpty;
					break;
				}
				case JsonObject obj:
				{
					if (obj.Count == 0) return TypeTokens.ObjectEmpty;
					break;
				}
				case JsonString str:
				{
					if (str.Length == 0) return TypeTokens.StringEmpty;
					break;
				}
			}

			return TypeTokens.Invalid;
		}

		/// <summary>Encode une valeur JSON en document jsonb</summary>
		/// <returns>Tableau contenant le document jsonb correspondant à <paramref name="value"/></returns>
		public static Slice Encode(JsonValue value, CrystalJsonSettings? settings = null)
		{
			Contract.NotNull(value);

			TypeTokens header = TryEncodeFastPath(value);
			if (header != TypeTokens.Invalid)
			{ // single byte value
				return Slice.FromByte((byte) header);
			}

			var writer = new SliceWriter();
			return EncodeTo(ref writer, value, settings);
		}

		public static Slice EncodeTo(ref SliceWriter writer, JsonValue value, CrystalJsonSettings? settings = null)
		{
			settings ??= CrystalJsonSettings.JsonCompact;
			WriteValue(ref writer, value, settings);
			return writer.ToSlice();
		}

		#endregion


		#region Writing JsonPack...

		public static void WriteValue(ref SliceWriter writer, JsonValue value, CrystalJsonSettings settings)
		{
			Contract.Debug.Requires(value != null);

			switch (value.Type)
			{
				case JsonType.Null:
				{
					writer.WriteByte((byte) TypeTokens.Null);
					break;
				}
				case JsonType.Boolean:
				{
					writer.WriteByte(value.ToBoolean() ? (byte) TypeTokens.True : (byte) TypeTokens.False);
					break;
				}
				case JsonType.DateTime:
				{
					WriteSmallString(ref writer, value.ToString());
					break;
				}
				case JsonType.String:
				{
					var str = (JsonString) value;
					WriteSmallString(ref writer, str.Value);
					break;
				}
				case JsonType.Number:
				{
					var num = (JsonNumber) value;
					if (!num.IsDecimal)
					{
						if (num.IsBetween(long.MinValue, long.MaxValue))
						{
							long x = num.ToInt64();
							WriteInteger(ref writer, x);
							break;
						}
					}

					if (num.Literal.Length > 8)
					{ // literal is larger than 64-bit double 
						writer.WriteByte((byte) TypeTokens.FixedDouble);
						writer.WriteDouble(num.ToDouble());
					}
					else
					{
						writer.WriteByte((byte) TypeTokens.VarNumberLiteral);
						writer.WriteVarStringAscii(num.Literal);
					}
					break;
				}
				case JsonType.Array:
				{
					WriteArray(ref writer, (JsonArray) value, settings);
					break;
				}
				case JsonType.Object:
				{
					WriteObject(ref writer, (JsonObject) value, settings);
					break;
				}
				default:
				{
					throw ThrowHelper.NotSupportedException($"Invalid JSON scalar of type '{value.Type}'.");
				}
			}
		}

		private static void WriteArray(ref SliceWriter writer, JsonArray array, CrystalJsonSettings settings)
		{
			if (array.Count == 0)
			{
				writer.WriteByte((byte) TypeTokens.ArrayEmpty);
				return;
			}

			writer.WriteByte((byte) TypeTokens.ArrayStart);
			foreach (var item in array)
			{
				WriteValue(ref writer, item, settings);
			}
			writer.WriteByte((byte) TypeTokens.ArrayStop);
		}

		private static void WriteObject(ref SliceWriter writer, JsonObject map, CrystalJsonSettings settings)
		{
			if (map.Count == 0)
			{
				writer.WriteByte((byte) TypeTokens.ObjectEmpty);
				return;
			}

			writer.WriteByte((byte) TypeTokens.ObjectStart);
			foreach (var item in map)
			{
				WriteSmallString(ref writer, item.Key);
				WriteValue(ref writer, item.Value, settings);
			}
			writer.WriteByte((byte) TypeTokens.ObjectStop);
		}

		private static void WriteInteger(ref SliceWriter writer, long value)
		{
			if (value >= 0)
			{
				if (value <= JENTRY_SMALL_POS_INT_MAX)
				{ // 0..127: small positive integer, inlined with type
					writer.WriteByte(JENTRY_SMALL_POS_INT_BASE + (byte) (value & 0x7F));
				}
				//note: FixedInt1 is already covered by above case!
				else if (value < (1 << 15))
				{ // 128..32767: stored as two bytes
					writer.WriteByte((byte) TypeTokens.FixedInt2);
					writer.WriteFixed16((short) value);
				}
				else if (value < (1 << 23))
				{ // stored as three bytes
					writer.WriteByte((byte) TypeTokens.FixedInt3);
					writer.WriteFixed24((uint) value & 0x7FFFFF);
				}
				else if (value < (1 << 31))
				{ // stored as three bytes
					writer.WriteByte((byte) TypeTokens.FixedInt4);
					writer.WriteFixed32((uint) value);
				}
				else
				{
					writer.WriteByte((byte) TypeTokens.FixedInt8);
					writer.WriteFixed64((ulong) value);
				}
			}
			else
			{
				if (value >= -JENTRY_SMALL_NEG_INT_MAX)
				{ // -1 .. -32 : small negative integer, inlined with type
					writer.WriteByte(JENTRY_SMALL_NEG_INT_BASE + (byte) (value & 0x1F));
				}
				else if (value >= -128)
				{ // -33 .. -128: stored as single byte
					writer.WriteByte((byte) TypeTokens.FixedInt1);
					writer.WriteByte((byte) value);
				}
				else if (value >= -32_768)
				{ // -129 .. -32768: stored as two bytes
					writer.WriteByte((byte) TypeTokens.FixedInt2);
					writer.WriteFixed16((ushort) value);
				}
				else if (value >= -8_388_608)
				{ // -129 .. -32768: stored as two bytes
					writer.WriteByte((byte) TypeTokens.FixedInt3);
					writer.WriteFixed24(((uint) value) & 0xFFFFFF);
				}
				else if (value >= -2147483648)
				{
					writer.WriteByte((byte) TypeTokens.FixedInt4);
					writer.WriteFixed32((uint) value);
				}
				else
				{
					writer.WriteByte((byte) TypeTokens.FixedInt8);
					writer.WriteFixed64((ulong) value);
				}
			}
		}

		private static void WriteSmallString(ref SliceWriter writer, string value)
		{
			Contract.Debug.Requires(value != null);
			int n = value.Length;
			if (n == 0)
			{
				writer.WriteByte(JENTRY_SMALL_STRING_BASE | 0);
				return;
			}

			//note: we store the number of BYTES, and not the number of CHARs!!
			int count = Utf8Encoder.GetByteCount(value);
			Contract.Debug.Assert(count >= 0);

			if (count <= JENTRY_SMALL_STRING_MAX)
			{ // small string, size can be inlined with type
				writer.WriteByte(JENTRY_SMALL_STRING_BASE + count);
			}
			else if (count <= 0xFF)
			{ // string size 60 .. 255
				writer.WriteByte((byte) TypeTokens.StringSize1);
				writer.WriteByte((byte) count);
			}
			else if (count <= 0xFFFF)
			{ // string size 256..65535
				writer.WriteByte((byte) TypeTokens.StringSize2);
				writer.WriteFixed16((ushort) count);
			}
			else
			{ // string size >= 65536
				writer.WriteByte((byte) TypeTokens.StringSize4);
				writer.WriteFixed32((uint) count);
			}

			if (count == n)
			{ // fast path for ASCII
				writer.WriteStringAscii(value);
			}
			else
			{ // will need to be utf8-encoded
				writer.WriteStringUtf8(value);
			}
		}

		#endregion

		#region Reading JsonPack...

		private delegate JsonValue? ParseTokenDelegate(ref SliceReader reader, int token, CrystalJsonSettings settings);

		// REVIEW: either a JsonValue (singleton) or a delegate
		private static readonly object[] DecodeMap = CreateDecodeMap();

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private static void Mark(object[] map, TypeTokens token, ParseTokenDelegate handler)
		{
			Contract.Debug.Assert(map[(int) token] == null, "oh noes "  + token);
			map[(int) token] = handler;
		}

		private static object[] CreateDecodeMap()
		{
			var map = new object[256];
			// 0x00 - 0x7F = Positive Integers
			for (int i = 0; i <= JENTRY_SMALL_POS_INT_MAX; i++)
			{
				map[(int) TypeTokens.Zero + i] = JsonNumber.Return(i);
			}
			map[(int) TypeTokens.Null] = JsonNull.Null;
			map[(int) TypeTokens.False] = JsonBoolean.False;
			map[(int) TypeTokens.True] = JsonBoolean.True;
			// 0xC0 - 0xFF = Negative Integers
			for (int i = 0; i < JENTRY_SMALL_NEG_INT_MAX; i++)
			{
				map[(int) TypeTokens.IntNeg1 - i] = JsonNumber.Return(-1 - i);
			}
			//note: we don't cache empty array and object because the instances are mutable!

			Mark(map, TypeTokens.ArrayStart, ParseArray);
			Mark(map, TypeTokens.ArrayEmpty, ParseArray);
			Mark(map, TypeTokens.ArrayStop, MakeError());
			Mark(map, TypeTokens.ObjectStart, ParseObject);
			Mark(map, TypeTokens.ObjectEmpty, ParseObject);
			Mark(map, TypeTokens.ObjectStop, MakeError());

			// 89..8F: undefined
			for(int i = 0x89; i <= 0x8F; i++) Mark(map, (TypeTokens) i, MakeError());

			// numbers
			Mark(map, TypeTokens.FixedInt1, ParseFixedS1);
			Mark(map, TypeTokens.FixedInt2, ParseFixedS2);
			Mark(map, TypeTokens.FixedInt3, ParseFixedS3);
			Mark(map, TypeTokens.FixedInt4, ParseFixedS4);
			Mark(map, TypeTokens.FixedInt8, ParseFixedS8);
			Mark(map, TypeTokens.FixedUInt4, ParseFixedU4);
			Mark(map, TypeTokens.FixedUInt8, ParseFixedU8);
			Mark(map, TypeTokens.FixedSingle, ParseFixedSingle);
			Mark(map, TypeTokens.FixedDouble, ParseFixedDouble);
			Mark(map, TypeTokens.VarNumberLiteral, ParseNumberLiteral);

			// 9A..9F: undefined
			for(int i = 0x9A; i <= 0x9F; i++) Mark(map, (TypeTokens) i, MakeError());

			// Strings
			map[(int) TypeTokens.StringEmpty] = JsonString.Empty;
			for (int i = 1; i <= JENTRY_SMALL_STRING_MAX; i++)
			{
				Mark(map, TypeTokens.StringEmpty + i, ParseInlineString);
			}

			Mark(map, TypeTokens.StringSize1, ParseString1);
			Mark(map, TypeTokens.StringSize2, ParseString2);
			Mark(map, TypeTokens.StringSize4, ParseString4);

#if DEBUG
			for (int i = 0; i < map.Length; i++)
			{
				if (!(map[i] is JsonValue || map[i] is ParseTokenDelegate)) Contract.Debug.Ensures(false, $"Empty slot {(TypeTokens) i} (0x{i:X02}) in JSONPack decode map!");
			}
#endif
			return map;
		}

		private static ParseTokenDelegate MakeError(string? text = null)
		{
			text ??= "Unexpected {0} token (0x{1:X02}) at offset {2} in JSONPack value.";
			return (ref SliceReader reader, int token, CrystalJsonSettings settings) => throw new FormatException(String.Format(text, (TypeTokens) token, token, reader.Position));
		}

		private static JsonValue? ReadValue(ref SliceReader reader, int token, CrystalJsonSettings settings)
		{
			if (token < 0) throw new FormatException("Cannot parse empty buffer.");

			var val = DecodeMap[token];
			if (val is JsonValue json) return json;
			return ((ParseTokenDelegate) val)(ref reader, token, settings);
		}

		private static JsonValue ParseFixedS1(ref SliceReader reader, int token, CrystalJsonSettings settings)
		{
			Contract.Debug.Requires(token == (int) TypeTokens.FixedInt1);
			int x = reader.ReadByte();
			return x < (1 << 7) ? x : (x | unchecked((int) 0xFFFFFF00));
		}

		private static JsonValue ParseFixedS2(ref SliceReader reader, int token, CrystalJsonSettings settings)
		{
			Contract.Debug.Requires(token == (int) TypeTokens.FixedInt2);
			int x = reader.ReadFixed16();
			return x < (1 << 15) ? x : (x | unchecked((int) 0xFFFF0000));
		}

		private static JsonValue ParseFixedS3(ref SliceReader reader, int token, CrystalJsonSettings settings)
		{
			Contract.Debug.Requires(token == (int) TypeTokens.FixedInt3);
			int x = (int) reader.ReadFixed24();
			return x < (1 << 23) ? x : (x | unchecked((int) 0xFF000000));
		}

		private static JsonValue ParseFixedS4(ref SliceReader reader, int token, CrystalJsonSettings settings)
		{
			Contract.Debug.Requires(token == (int) TypeTokens.FixedInt4);
			return (int) reader.ReadFixed32();
		}

		private static JsonValue ParseFixedS8(ref SliceReader reader, int token, CrystalJsonSettings settings)
		{
			Contract.Debug.Requires(token == (int) TypeTokens.FixedInt8);
			return (long) reader.ReadFixed64();
		}

		private static JsonValue ParseFixedU4(ref SliceReader reader, int token, CrystalJsonSettings settings)
		{
			Contract.Debug.Requires(token == (int) TypeTokens.FixedUInt4);
			return (int) reader.ReadFixed32();
		}

		private static JsonValue ParseFixedU8(ref SliceReader reader, int token, CrystalJsonSettings settings)
		{
			Contract.Debug.Requires(token == (int) TypeTokens.FixedUInt8);
			return (long) reader.ReadFixed64();
		}

		private static JsonValue ParseFixedSingle(ref SliceReader reader, int token, CrystalJsonSettings settings)
		{
			Contract.Debug.Requires(token == (int) TypeTokens.FixedSingle);
			return reader.ReadSingle();
		}

		private static JsonValue ParseFixedDouble(ref SliceReader reader, int token, CrystalJsonSettings settings)
		{
			Contract.Debug.Requires(token == (int) TypeTokens.FixedDouble);
			return reader.ReadDouble();
		}

		private static JsonValue? ParseNumberLiteral(ref SliceReader reader, int token, CrystalJsonSettings settings)
		{
			Contract.Debug.Requires(token == (int) TypeTokens.VarNumberLiteral);
			int count = reader.ReadByte();
			var lit = reader.ReadBytes(count);
			//TODO: optimize!
			return CrystalJsonParser.ParseJsonNumber(lit.ToStringAscii());
		}

		private static JsonValue? ParseInlineString(ref SliceReader reader, int token, CrystalJsonSettings settings)
		{
			Contract.Debug.Requires(token >= (int) TypeTokens.StringEmpty && token <= (int) TypeTokens.SmlString60);
			int count = token - JENTRY_SMALL_STRING_BASE;
			return reader.ReadBytes(count).ToStringUtf8();
		}

		private static JsonValue? ParseString1(ref SliceReader reader, int token, CrystalJsonSettings settings)
		{
			Contract.Debug.Requires(token == (int) TypeTokens.StringSize1);
			int count = reader.ReadByte();
			return reader.ReadBytes(count).ToStringUtf8();
		}

		private static JsonValue? ParseString2(ref SliceReader reader, int token, CrystalJsonSettings settings)
		{
			Contract.Debug.Requires(token == (int) TypeTokens.StringSize2);
			int count = reader.ReadFixed16();
			return reader.ReadBytes(count).ToStringUtf8();
		}

		private static JsonValue? ParseString4(ref SliceReader reader, int token, CrystalJsonSettings settings)
		{
			Contract.Debug.Requires(token == (int) TypeTokens.StringSize2);
			uint count = reader.ReadFixed32();
			if (count > int.MaxValue) throw new FormatException("String size is too large");
			return reader.ReadBytes(count).ToStringUtf8();
		}

		private static JsonArray ParseArray(ref SliceReader reader, int token, CrystalJsonSettings settings)
		{
			if (token == (int) TypeTokens.ArrayEmpty) return JsonArray.Empty;

			//note: ARRAY_START has already been parsed
			int next;
			JsonArray? arr = null;
			while ((next = reader.ReadByte()) != (int) TypeTokens.ArrayStop)
			{
				if (next < 0) throw new FormatException("Unexpected end of JSONPack Array.");
				var val = ReadValue(ref reader, next, settings);
				if (arr == null) arr = new JsonArray();
				arr.Add(val ?? JsonNull.Null);
			}
			return arr ?? JsonArray.Empty;
		}

		private static string? ParseSmallString(ref SliceReader reader, int token)
		{
			if (token >= JENTRY_SMALL_STRING_BASE && token <= JENTRY_SMALL_STRING_BASE + JENTRY_SMALL_STRING_MAX)
			{
				return reader.ReadBytes(token - JENTRY_SMALL_STRING_BASE).ToStringUtf8();
			}

			int count;
			switch (token)
			{
				case (int) TypeTokens.StringSize1:
				{ // "long" string
					count = reader.ReadByte();
					break;
				}
				default:
				{
					throw new FormatException($"Unexpected '{(TypeTokens) token}' token (0x{token:X02}) instead of identifier while parsing JSONPack Object.");
				}
			}
			return reader.ReadBytes(count).ToStringUtf8();
		}

		public static JsonObject ParseObject(ref SliceReader reader, int token, CrystalJsonSettings settings)
		{
			if (token == (int) TypeTokens.ObjectEmpty) return JsonObject.Empty;

			//note: OBJECT_START has already been parsed
			int next;
			JsonObject? obj = null;
			while ((next = reader.ReadByte()) != (int) TypeTokens.ObjectStop)
			{
				if (next < 0) throw new FormatException("Unexpected end of JSONPack Object: missing key.");

				// next must be an identifier: either SMALL_STRING or VAR_STRING
				string? key = ParseSmallString(ref reader, next);
				if (key == null) throw new FormatException("Object key cannot be null.");

				next = reader.ReadByte();
				if (next < 0) throw new FormatException("Unexpected end of JSONPack Object: missing value.");

				// next is the value for this key
				var val = ReadValue(ref reader, next, settings);
				if (obj == null) obj = new JsonObject();
				obj.Add(key, val ?? JsonNull.Null);
			}
			// skip the OBJECT_STOP token
			return obj ?? JsonObject.Empty;
		}

		#endregion

	}

}

