#region BSD Licence
/* Copyright (c) 2013, Doxense SARL
All rights reserved.

Redistribution and use in source and binary forms, with or without
modification, are permitted provided that the following conditions are met:
	* Redistributions of source code must retain the above copyright
	  notice, this list of conditions and the following disclaimer.
	* Redistributions in binary form must reproduce the above copyright
	  notice, this list of conditions and the following disclaimer in the
	  documentation and/or other materials provided with the distribution.
	* Neither the name of Doxense nor the
	  names of its contributors may be used to endorse or promote products
	  derived from this software without specific prior written permission.

THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS "AS IS" AND
ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED
WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE
DISCLAIMED. IN NO EVENT SHALL <COPYRIGHT HOLDER> BE LIABLE FOR ANY
DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES
(INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES;
LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND
ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT
(INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS
SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
 */
#endregion

namespace FoundationDB.Layers.Tuples
{
	using FoundationDB.Client;
	using FoundationDB.Client.Utils;
	using System;
	using System.Globalization;
	using System.Reflection;
	using System.Text;

	public static class FdbTuplePackers
	{
		private static readonly Slice[] NoSlices = new Slice[0];

		#region Serializers...

		public static Action<FdbBufferWriter, T> GetSerializer<T>()
		{
			return (Action<FdbBufferWriter, T>)GetSerializerFor(typeof(T));
		}

		internal static Delegate GetSerializerFor(Type type)
		{
			if (type == null) throw new ArgumentNullException("type");

			var typeArgs = new[] { typeof(FdbBufferWriter), type };
			var method = typeof(FdbTuplePackers).GetMethod("SerializeTo", BindingFlags.Static | BindingFlags.Public, null, typeArgs, null);
			if (method != null)
			{ // we have a direct serializer
				return method.CreateDelegate(typeof(Action<,>).MakeGenericType(typeArgs));
			}

			// maybe if it is a tuple ?
			if (typeof(IFdbTuple).IsAssignableFrom(type))
			{
				method = typeof(FdbTuplePackers).GetMethod("SerializeTupleTo", BindingFlags.Static | BindingFlags.Public);
				if (method != null)
				{
					return method.MakeGenericMethod(type).CreateDelegate(typeof(Action<,>).MakeGenericType(typeArgs));
				}
			}

			if (typeof(ITupleFormattable).IsAssignableFrom(type))
			{
				method = typeof(FdbTuplePackers).GetMethod("SerializeFormattableTo", BindingFlags.Static | BindingFlags.Public);
				if (method != null)
				{
					return method.CreateDelegate(typeof(Action<,>).MakeGenericType(typeArgs));
				}
			}

			// TODO: look for a static SerializeTo(BWB, T) method on the type itself ?

			// no luck..
			return null;

		}

		public static void SerializeObjectTo(FdbBufferWriter writer, object value)
		{
			if (writer == null) throw new ArgumentNullException("writer");

			var type = value != null ? value.GetType() : null;
			switch (Type.GetTypeCode(type))
			{
				case TypeCode.Empty:
				{ // null value
					// includes all null references to ref types, as nullables where HasValue == false
					FdbTupleParser.WriteNil(writer);
					return;
				}
				case TypeCode.Object:
				{
					byte[] bytes = value as byte[];
					if (bytes != null)
					{
						SerializeTo(writer, bytes);
						return;
					}

					if (value is Slice)
					{
						SerializeTo(writer, (Slice)value);
						return;
					}

					if (value is Guid)
					{
						SerializeTo(writer, (Guid)value);
						return;
					}

					if (value is TimeSpan)
					{
						SerializeTo(writer, (TimeSpan)value);
						return;
					}

					if (value is FdbTupleAlias)
					{
						SerializeTo(writer, (FdbTupleAlias)value);
						return;
					}

					break;
				}
				case TypeCode.DBNull:
				{ // same as null
					FdbTupleParser.WriteNil(writer);
					return;
				}
				case TypeCode.Boolean:
				{
					SerializeTo(writer, (bool)value);
					return;
				}
				case TypeCode.Char:
				{
					// should be treated as a string with only one char
					SerializeTo(writer, (char)value);
					return;
				}
				case TypeCode.SByte:
				{
					SerializeTo(writer, (sbyte)value);
					return;
				}
				case TypeCode.Byte:
				{
					SerializeTo(writer, (byte)value);
					return;
				}
				case TypeCode.Int16:
				{
					SerializeTo(writer, (short)value);
					return;
				}
				case TypeCode.UInt16:
				{
					SerializeTo(writer, (ushort)value);
					return;
				}
				case TypeCode.Int32:
				{
					SerializeTo(writer, (int)value);
					return;
				}
				case TypeCode.UInt32:
				{
					SerializeTo(writer, (uint)value);
					return;
				}
				case TypeCode.Int64:
				{
					SerializeTo(writer, (long)value);
					return;
				}
				case TypeCode.UInt64:
				{
					SerializeTo(writer, (ulong)value);
					return;
				}
				case TypeCode.String:
				{
					SerializeTo(writer, value as string);
					return;
				}
				case TypeCode.DateTime:
				{
					SerializeTo(writer, (DateTime)value);
					return;
				}
			}

			var fmt = value as ITupleFormattable;
			if (fmt != null)
			{
				var tuple = fmt.ToTuple();
				tuple.PackTo(writer);
				return;
			}

			// Not Supported ?
			throw new NotSupportedException(String.Format("Doesn't know how to serialize objects of type {0}", type.Name));
		}

		public static void SerializeTo(FdbBufferWriter writer, Slice value)
		{
			Contract.Requires(writer != null);
			if (!value.HasValue)
			{
				FdbTupleParser.WriteNil(writer);
			}
			else if (value.Offset == 0 && value.Count == value.Array.Length)
			{
				FdbTupleParser.WriteBytes(writer, value.Array);
			}
			else
			{
				FdbTupleParser.WriteBytes(writer, value.Array, value.Offset, value.Count);
			}
		}

		public static void SerializeTo(FdbBufferWriter writer, byte[] value)
		{
			Contract.Requires(writer != null);
			FdbTupleParser.WriteBytes(writer, value);
		}

		public static void SerializeTo(FdbBufferWriter writer, ArraySegment<byte> value)
		{
			Contract.Requires(writer != null);
			SerializeTo(writer, Slice.Create(value.Array, value.Offset, value.Count));
		}

		public static void SerializeTo(FdbBufferWriter writer, char value)
		{
			Contract.Requires(writer != null);

			if (value == 0)
			{ // NUL => "00 0F"
				writer.WriteByte4(FdbTupleTypes.Utf8, 0x00, 0xFF, 0x00);
			}
			else if (value < 128)
			{ // ASCII => single byte
				writer.WriteByte3(FdbTupleTypes.Utf8, (byte)value, 0x00);
			}
			else
			{ // Unicode ?
				var tmp = Slice.FromChar(value);
				FdbTupleParser.WriteNulEscapedBytes(writer, FdbTupleTypes.Utf8, tmp.Array, tmp.Offset, tmp.Count);
			}
		}

		public static void SerializeTo(FdbBufferWriter writer, bool value)
		{
			Contract.Requires(writer != null);

			// false is encoded as 0 (\x14)
			// true is encoded as -1 (\x13\xfe)
			FdbTupleParser.WriteInt32(writer, value ? -1 : 0);
		}

		public static void SerializeTo(FdbBufferWriter writer, bool? value)
		{
			Contract.Requires(writer != null);

			if (value.HasValue)
				FdbTupleParser.WriteInt32(writer, value.Value ? -1 : 0);
			else
				FdbTupleParser.WriteNil(writer);
		}

		public static void SerializeTo(FdbBufferWriter writer, sbyte value)
		{
			Contract.Requires(writer != null);

			FdbTupleParser.WriteInt32(writer, value);
		}

		public static void SerializeTo(FdbBufferWriter writer, byte value)
		{
			Contract.Requires(writer != null);

			if (value == 0)
			{ // 0
				writer.WriteByte(FdbTupleTypes.IntZero);
			}
			else
			{ // 1..255
				writer.WriteByte2(FdbTupleTypes.IntPos1, value);
			}
		}

		public static void SerializeTo(FdbBufferWriter writer, short value)
		{
			Contract.Requires(writer != null);

			FdbTupleParser.WriteInt32(writer, value);
		}

		public static void SerializeTo(FdbBufferWriter writer, ushort value)
		{
			Contract.Requires(writer != null);

			FdbTupleParser.WriteUInt32(writer, value);
		}

		public static void SerializeTo(FdbBufferWriter writer, int value)
		{
			Contract.Requires(writer != null);

			FdbTupleParser.WriteInt32(writer, value);
		}

		public static void SerializeTo(FdbBufferWriter writer, uint value)
		{
			Contract.Requires(writer != null);

			FdbTupleParser.WriteUInt32(writer, value);
		}

		public static void SerializeTo(FdbBufferWriter writer, long value)
		{
			FdbTupleParser.WriteInt64(writer, value);
		}

		public static void SerializeTo(FdbBufferWriter writer, ulong value)
		{
			Contract.Requires(writer != null);

			FdbTupleParser.WriteUInt64(writer, value);
		}

		public static void SerializeTo(FdbBufferWriter writer, string value)
		{
			Contract.Requires(writer != null);

			if (value == null)
			{ // <00>
				writer.WriteByte(FdbTupleTypes.Nil);
			}
			else if (value.Length == 0)
			{ // <02><00>
				writer.WriteByte2(FdbTupleTypes.Utf8, 0x00);
			}
			else
			{ // <02>...utf8...<00>
				FdbTupleParser.WriteString(writer, value);
			}
		}

		public static void SerializeTo(FdbBufferWriter writer, DateTime value)
		{
			Contract.Requires(writer != null);

			//TODO: how to deal with negative values ?
			FdbTupleParser.WriteInt64(writer, value.Ticks);
		}

		public static void SerializeTo(FdbBufferWriter writer, TimeSpan value)
		{
			Contract.Requires(writer != null);

			//TODO: how to deal with negative values ?
			FdbTupleParser.WriteInt64(writer, value.Ticks);
		}

		public static void SerializeTo(FdbBufferWriter writer, Guid value)
		{
			Contract.Requires(writer != null);

			FdbTupleParser.WriteGuid(writer, value);
		}

		public static void SerializeTo(FdbBufferWriter writer, FdbTupleAlias value)
		{
			Contract.Requires(Enum.IsDefined(typeof(FdbTupleAlias), value));

			writer.WriteByte((byte)value);
		}

		public static void SerializeTupleTo<TTuple>(FdbBufferWriter writer, TTuple tuple)
			where TTuple : IFdbTuple
		{
			Contract.Requires(writer != null && tuple != null);

			tuple.PackTo(writer);
		}

		public static void SerializeFormattableTo(FdbBufferWriter writer, ITupleFormattable formattable)
		{
			Contract.Requires(writer != null && formattable != null);

			var tuple = formattable.ToTuple();
			if (tuple == null) throw new InvalidOperationException(String.Format("Custom formatter {0}.ToTuple() cannot return null", formattable.GetType().Name));
			tuple.PackTo(writer);
		}

		#endregion

		#region Deserializers...

		private static long ParseInt64(int type, Slice slice)
		{
			int bytes = type - FdbTupleTypes.IntBase;
			if (bytes == 0) return 0L;

			bool neg = false;
			if (bytes < 0)
			{
				bytes = -bytes;
				neg = true;
			}

			if (bytes > 8) throw new FormatException("Invalid size for tuple integer");
			long value = (long)slice.ReadUInt64(1, bytes);

			if (neg)
			{ // the value is encoded as the one's complement of the absolute value
				value = (-(~value));
				if (bytes < 8) value |= (-1L << (bytes << 3)); 
				return value;
			}

			return value;
		}

		private static ArraySegment<byte> UnescapeByteString(byte[] buffer, int offset, int count)
		{
			Contract.Requires(buffer != null && offset >= 0 && count >= 0);

			// check for nulls
			int p = offset;
			int end = offset + count;

			while (p < end)
			{
				if (buffer[p] == 0)
				{ // found a 0, switch to slow path
					return UnescapeByteStringSlow(buffer, offset, count, p - offset);
				}
				++p;
			}
			// buffer is clean, we can return it as-is
			return new ArraySegment<byte>(buffer, offset, count);
		}

		private static ArraySegment<byte> UnescapeByteStringSlow(byte[] buffer, int offset, int count, int offsetOfFirstZero = 0)
		{
			Contract.Requires(buffer != null && offset >= 0 && count >= 0);

			var tmp = new byte[count];

			int p = offset;
			int end = offset + count;
			int i = 0;
			if (offsetOfFirstZero > 0)
			{
				Buffer.BlockCopy(buffer, offset, tmp, 0, offsetOfFirstZero);
				p += offsetOfFirstZero;
				i = offsetOfFirstZero;
			}

			while (p < end)
			{
				byte b = buffer[p++];
				if (b == 0)
				{ // skip next FF
					//TODO: check that next byte really is 0xFF
					++p;
				}
				tmp[i++] = b;
			}

			return new ArraySegment<byte>(tmp, 0, i);
		}

		private static Slice ParseBytes(Slice slice)
		{
			Contract.Requires(slice.HasValue && slice[0] == FdbTupleTypes.Bytes && slice[slice.Count - 1] == 0);
			if (slice.Count <= 2) return Slice.Empty;

			var decoded = UnescapeByteString(slice.Array, slice.Offset + 1, slice.Count - 2);

			return new Slice(decoded.Array, decoded.Offset, decoded.Count);
		}

		private static string ParseAscii(Slice slice)
		{
			Contract.Requires(slice.HasValue && slice[0] == FdbTupleTypes.Bytes && slice[slice.Count - 1] == 0);

			if (slice.Count <= 2) return String.Empty;

			var decoded = UnescapeByteString(slice.Array, slice.Offset + 1, slice.Count - 2);

			return Encoding.Default.GetString(decoded.Array, decoded.Offset, decoded.Count);
		}

		private static string ParseUnicode(Slice slice)
		{
			Contract.Requires(slice.HasValue && slice[0] == FdbTupleTypes.Utf8);

			if (slice.Count <= 2) return String.Empty;
			//TODO: check args
			var decoded = UnescapeByteString(slice.Array, slice.Offset + 1, slice.Count - 2);
			return Encoding.UTF8.GetString(decoded.Array, decoded.Offset, decoded.Count);
		}

		private static Guid ParseGuid(Slice slice)
		{
			Contract.Requires(slice.HasValue && slice[0] == FdbTupleTypes.Guid);

			if (slice.Count != 17)
			{
				//TODO: usefull! error message! 
				throw new FormatException("Slice has invalid size for a guid");
			}

			//TODO: optimize !
			return new Guid(slice.GetBytes(1, 16));
		}

		public static object DeserializeObject(Slice slice)
		{
			if (slice.IsNullOrEmpty) return null;

			int type = slice[0];
			if (type <= FdbTupleTypes.IntPos8)
			{
				if (type >= FdbTupleTypes.IntNeg8) return ParseInt64(type, slice);

				switch (type)
				{
					case FdbTupleTypes.Nil: return null;
					case FdbTupleTypes.Bytes: return ParseBytes(slice);
					case FdbTupleTypes.Utf8: return ParseUnicode(slice);
					case FdbTupleTypes.Guid: return ParseGuid(slice);
				}
			}

			if (type >= FdbTupleTypes.AliasDirectory)
			{
				if (type == FdbTupleTypes.AliasSystem) return FdbTupleAlias.System;
				return FdbTupleAlias.Directory;
			}

			throw new FormatException("Cannot convert slice into this type");
		}

		public static T DeserializeFormattable<T>(Slice slice)
			where T : ITupleFormattable, new()
		{
			var tuple = FdbTuple.Unpack(slice);
			var value = new T();
			value.FromTuple(tuple);
			return value;
		}

		public static T DeserializeFormattable<T>(Slice slice, Func<T> factory)
			where T : ITupleFormattable
		{
			var tuple = FdbTuple.Unpack(slice);
			var value = factory();
			value.FromTuple(tuple);
			return value;
		}

		public static int DeserializeInt32(Slice slice)
		{
			checked
			{
				return (int)DeserializeInt64(slice);
			}
		}

		public static long DeserializeInt64(Slice slice)
		{
			if (slice.IsNullOrEmpty) return 0L; //TODO: fail ?

			int type = slice[0];
			if (type <= FdbTupleTypes.IntPos8)
			{
				if (type >= FdbTupleTypes.IntNeg8) return ParseInt64(type, slice);

				switch (type)
				{
					case FdbTupleTypes.Nil: return 0;
					case FdbTupleTypes.Bytes: return long.Parse(ParseAscii(slice), CultureInfo.InvariantCulture);
					case FdbTupleTypes.Utf8: return long.Parse(ParseUnicode(slice), CultureInfo.InvariantCulture);
				}
			}

			throw new FormatException("Cannot convert slice into this type");
		}

		public static uint DeserializeUInt32(Slice slice)
		{
			checked
			{
				return (uint)DeserializeUInt64(slice);
			}
		}

		public static ulong DeserializeUInt64(Slice slice)
		{
			if (slice.IsNullOrEmpty) return 0UL; //TODO: fail ?

			int type = slice[0];
			if (type <= FdbTupleTypes.IntPos8)
			{
				if (type >= FdbTupleTypes.IntNeg8) return (ulong)ParseInt64(type, slice);

				switch (type)
				{
					case FdbTupleTypes.Nil: return 0;
					case FdbTupleTypes.Bytes: return ulong.Parse(ParseAscii(slice), CultureInfo.InvariantCulture);
					case FdbTupleTypes.Utf8: return ulong.Parse(ParseUnicode(slice), CultureInfo.InvariantCulture);
				}
			}

			throw new FormatException("Cannot convert slice into this type");
		}

		public static string DeserializeString(Slice slice)
		{
			if (slice.IsNullOrEmpty) return null;

			int type = slice[0];
			if (type <= FdbTupleTypes.IntPos8)
			{
				if (type >= FdbTupleTypes.IntNeg8) return ParseInt64(type, slice).ToString(CultureInfo.InvariantCulture);

				switch (type)
				{
					case FdbTupleTypes.Nil: return null;
					case FdbTupleTypes.Bytes: return ParseAscii(slice);
					case FdbTupleTypes.Utf8: return ParseUnicode(slice);
					case FdbTupleTypes.Guid: return ParseGuid(slice).ToString();
				}
			}

			throw new FormatException("Cannot convert slice into this type");

		}

		public static Guid DeserializeGuid(Slice slice)
		{
			if (slice.IsNullOrEmpty) return Guid.Empty;

			int type = slice[0];

			switch (type)
			{
				case FdbTupleTypes.Bytes:
				{
					return Guid.Parse(ParseAscii(slice));
				}
				case FdbTupleTypes.Utf8:
				{
					return Guid.Parse(ParseUnicode(slice));
				}
				case FdbTupleTypes.Guid:
				{
					return ParseGuid(slice);
				}
			}

			throw new FormatException("Cannot convert slice into this type");
		}

		public static FdbTupleAlias DeserializeAlias(Slice slice)
		{
			if (slice.Count != 1) throw new FormatException("Cannot convert slice into this type");
			return (FdbTupleAlias)slice[0];
		}

		internal static FdbSlicedTuple Unpack(Slice buffer)
		{
			var slicer = new Slicer(buffer);

			// most tuples will probably fit within (prefix, sub-prefix, id, key) so pre-allocating with 4 should be ok...
			var items = new Slice[4];

			Slice item;
			int p = 0;
			while ((item = ParseNext(ref slicer)).HasValue)
			{
				if (p >= items.Length)
				{
					// note: do not grow exponentially, because tuples will never but very large...
					Array.Resize(ref items, p + 4);
				}
				items[p++] = item;
			}

			if (slicer.HasMore) throw new FormatException("Parsing of tuple failed failed before reaching the end of the key");
			return new FdbSlicedTuple(p == 0 ? NoSlices : items, 0, p);
		}

		/// <summary>Only returns the last item of the tuple</summary>
		internal static Slice UnpackLast(Slice buffer)
		{
			var slicer = new Slicer(buffer);

			Slice item = Slice.Nil;

			Slice current;
			while ((current = ParseNext(ref slicer)).HasValue)
			{
				item = current;
			}

			if (slicer.HasMore) throw new FormatException("Parsing of tuple failed failed before reaching the end of the key");
			return item;
		}

		internal static Slice ParseNext(ref Slicer reader)
		{
			if (!reader.HasMore) return Slice.Nil;

			int type = reader.PeekByte();
			switch (type)
			{
				case -1:
				{ // End of Stream
					return Slice.Nil;
				}

				case FdbTupleTypes.Nil:
				{ // <00> => null
					reader.Skip(1);
					return Slice.Empty;
				}

				case FdbTupleTypes.Bytes:
				{ // <01>(bytes)<00>
					return reader.ReadByteString();
				}

				case FdbTupleTypes.Utf8:
				{ // <02>(utf8 bytes)<00>
					return reader.ReadByteString();
				}
				case FdbTupleTypes.Guid:
				{ // <03>(16 bytes)
					return reader.ReadBytes(17);
				}
			}

			if (type <= FdbTupleTypes.IntPos8 && type >= FdbTupleTypes.IntNeg8)
			{
				int bytes = type - FdbTupleTypes.IntZero;
				if (bytes < 0) bytes = -bytes;

				return reader.ReadBytes(1 + bytes);
			}

			throw new FormatException(String.Format("Invalid tuple type byte {0} at index {1}/{2}", type, reader.Position, reader.Buffer.Count));
		}

		#endregion

	}

}
