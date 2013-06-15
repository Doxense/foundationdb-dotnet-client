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
	* Neither the name of the <organization> nor the
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

using FoundationDb.Client;
using FoundationDb.Client.Utils;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace FoundationDb.Layers.Tuples
{

	public static class FdbTuplePackers
	{

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

		public static void SerializeTo(FdbBufferWriter writer, char value)
		{
			Contract.Requires(writer != null);

			// Layers usually use a single ASCII letter to create subpaces, and since python has 'ascii' strings, they will be serialized as '01 XX 00'

			// So, if the char is in the ASCII range (0..127), save it as if it was a byte[], else save it has a string
			if (value >= 0 && value < 128)
			{ // ASCII
				writer.EnsureBytes(3);
				writer.UnsafeWriteByte(FdbTupleTypes.Bytes);
				writer.UnsafeWriteByte((byte)value);
				writer.UnsafeWriteByte(0);
			}
			else
			{ // Unicode ?
				byte[] tmp = Encoding.UTF8.GetBytes(new char[] { value });
				FdbTupleParser.WriteBytes(writer, tmp);
			}
		}

		public static void SerializeTo(FdbBufferWriter writer, bool value)
		{
			Contract.Requires(writer != null);

			// false is encoded as 0 (\x14)
			// true is encoded as -1 (\x13\xfe)
			FdbTupleParser.WriteInt64(writer, value ? -1L : 0L);
		}

		public static void SerializeTo(FdbBufferWriter writer, int value)
		{
			Contract.Requires(writer != null);

			FdbTupleParser.WriteInt64(writer, value);
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
				writer.WriteByte(FdbTupleTypes.Utf8);
				writer.WriteByte(0);
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

		public static void SerializeTupleTo<TTuple>(FdbBufferWriter writer, TTuple tuple)
			where TTuple : IFdbTuple
		{
			Contract.Requires(writer != null && tuple != null);

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

			return new ArraySegment<byte>(tmp, 0, p - offset);
		}

		private static string ParseAscii(Slice slice)
		{
			Contract.Requires(slice.HasValue && slice[0] == FdbTupleTypes.Bytes);

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
					case FdbTupleTypes.Bytes: return ParseAscii(slice);
					case FdbTupleTypes.Utf8: return ParseUnicode(slice);
					case FdbTupleTypes.Guid: return ParseGuid(slice);
				}
			}

			throw new FormatException("Cannot convert slice into this type");
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

		internal static IFdbTuple Unpack(Slice buffer)
		{
			var slicer = new Slicer(buffer);
			var items = new List<Slice>();

			Slice item;
			while ((item = ParseNext(ref slicer)).HasValue)
			{
				items.Add(item);
			}

			if (slicer.HasMore) throw new FormatException("Parsing of tuple failed failed before reaching the end of the key");

			if (items.Count == 0) return FdbTuple.Empty;
			return new FdbSlicedTuple(buffer, items);
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
