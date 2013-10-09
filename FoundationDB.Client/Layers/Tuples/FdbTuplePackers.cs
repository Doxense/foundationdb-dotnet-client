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
	using System.Runtime.CompilerServices;
	using System.Text;

	/// <summary>Helper methods used during serialization of values to the tuple binary format</summary>
	public static class FdbTuplePackers
	{
		private static readonly Slice[] NoSlices = new Slice[0];

		#region Serializers...

		/// <summary>Returns a lambda that will be abl to serialize values of type <typeparamref name="T"/></summary>
		/// <typeparam name="T">Type of values to serialize</typeparam>
		/// <returns>Reusable action that knows how to serialize values of type <typeparamref name="T"/> into binary buffers, or an exception if the type is not supported</returns>
		public static Action<FdbBufferWriter, T> GetSerializer<T>()
		{
			return (Action<FdbBufferWriter, T>)GetSerializerFor(typeof(T));
		}

		internal static Delegate GetSerializerFor(Type type)
		{
			if (type == null) throw new ArgumentNullException("type");

			if (type == typeof(object))
			{ // return a generic serializer that will inspect the runtime type of the object
				return new Action<FdbBufferWriter, object>(FdbTuplePackers.SerializeObjectTo);
			}

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

		/// <summary>Serialize an untyped object, by checking its type at runtime</summary>
		/// <param name="writer">Target buffer</param>
		/// <param name="value">Untyped value whose type will be inspected at runtime</param>
		/// <remarks>May throw at runtime if the type is not supported</remarks>
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

					if (value is Uuid)
					{
						SerializeTo(writer, (Uuid)value);
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

		/// <summary>Writes a slice as a byte[] array</summary>
		public static void SerializeTo(FdbBufferWriter writer, Slice value)
		{
			Contract.Requires(writer != null);
			if (value.IsNull)
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

		/// <summary>Writes a byte[] array</summary>
		public static void SerializeTo(FdbBufferWriter writer, byte[] value)
		{
			Contract.Requires(writer != null);
			FdbTupleParser.WriteBytes(writer, value);
		}

		/// <summary>Writes an array segment as a byte[] array</summary>
		public static void SerializeTo(FdbBufferWriter writer, ArraySegment<byte> value)
		{
			Contract.Requires(writer != null);
			SerializeTo(writer, Slice.Create(value.Array, value.Offset, value.Count));
		}

		/// <summary>Writes a char as Unicode string</summary>
		public static void SerializeTo(FdbBufferWriter writer, char value)
		{
			Contract.Requires(writer != null);

			FdbTupleParser.WriteChar(writer, value);
		}

		/// <summary>Writes a boolean as an integer</summary>
		/// <remarks>Uses 0 for false, and -1 for true</remarks>
		public static void SerializeTo(FdbBufferWriter writer, bool value)
		{
			Contract.Requires(writer != null);

			// To be compatible with other bindings, we will encode False as the number 0, and True as the number 1

			if (value)
			{ // true => 15 01
				writer.WriteByte2(FdbTupleTypes.IntPos1, (byte)1);
			}
			else
			{ // false => 14
				writer.WriteByte(FdbTupleTypes.IntZero);
			}
		}

		/// <summary>Writes a boolean as an integer or null</summary>
		public static void SerializeTo(FdbBufferWriter writer, bool? value)
		{
			Contract.Requires(writer != null);

			if (value == null)
			{ // null => 00
				FdbTupleParser.WriteNil(writer);
			}
			else if (value.Value)
			{ // true => 15 01
				writer.WriteByte2(FdbTupleTypes.IntPos1, (byte)1);
			}
			else
			{ // false => 14
				writer.WriteByte(FdbTupleTypes.IntZero);
			}
		}

		/// <summary>Writes a signed byte as an integer</summary>
		public static void SerializeTo(FdbBufferWriter writer, sbyte value)
		{
			Contract.Requires(writer != null);

			FdbTupleParser.WriteInt32(writer, value);
		}

		/// <summary>Writes an unsigned byte as an integer</summary>
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

		/// <summary>Writes a signed word as an integer</summary>
		public static void SerializeTo(FdbBufferWriter writer, short value)
		{
			Contract.Requires(writer != null);

			FdbTupleParser.WriteInt32(writer, value);
		}

		/// <summary>Writes an unsigned word as an integer</summary>
		public static void SerializeTo(FdbBufferWriter writer, ushort value)
		{
			Contract.Requires(writer != null);

			FdbTupleParser.WriteUInt32(writer, value);
		}

		/// <summary>Writes a signed int as an integer</summary>
		public static void SerializeTo(FdbBufferWriter writer, int value)
		{
			Contract.Requires(writer != null);

			FdbTupleParser.WriteInt32(writer, value);
		}

		/// <summary>Writes an unsigned int as an integer</summary>
		public static void SerializeTo(FdbBufferWriter writer, uint value)
		{
			Contract.Requires(writer != null);

			FdbTupleParser.WriteUInt32(writer, value);
		}

		/// <summary>Writes a signed long as an integer</summary>
		public static void SerializeTo(FdbBufferWriter writer, long value)
		{
			FdbTupleParser.WriteInt64(writer, value);
		}

		/// <summary>Writes an unsigned long as an integer</summary>
		public static void SerializeTo(FdbBufferWriter writer, ulong value)
		{
			Contract.Requires(writer != null);

			FdbTupleParser.WriteUInt64(writer, value);
		}

		/// <summary>Writes a string as an Unicode string</summary>
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

		/// <summary>Writes a DateTime converted to a number of ticks encoded as an integer</summary>
		public static void SerializeTo(FdbBufferWriter writer, DateTime value)
		{
			Contract.Requires(writer != null);

			//TODO: how to deal with negative values ?
			FdbTupleParser.WriteInt64(writer, value.Ticks);
		}

		/// <summary>Writes a TimeSpan converted to a number of ticks encoded as an integer</summary>
		public static void SerializeTo(FdbBufferWriter writer, TimeSpan value)
		{
			Contract.Requires(writer != null);

			//TODO: how to deal with negative values ?
			FdbTupleParser.WriteInt64(writer, value.Ticks);
		}

		/// <summary>Writes a Guid as a 128-bit UUID</summary>
		public static void SerializeTo(FdbBufferWriter writer, Guid value)
		{
			Contract.Requires(writer != null);

			FdbTupleParser.WriteGuid(writer, value);
		}

		/// <summary>Writes a Uuid as a 128-bit UUID</summary>
		public static void SerializeTo(FdbBufferWriter writer, Uuid value)
		{
			Contract.Requires(writer != null);

			FdbTupleParser.WriteUuid(writer, value);
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

		/// <summary>Deserialize a packed element into an object by choosing the most appropriate type at runtime</summary>
		/// <param name="slice">Slice that contains a single packed element</param>
		/// <returns>Decoded element, in the type that is the best fit.</returns>
		/// <remarks>You should avoid working with untyped values as much as possible! Blindly casting the returned object may be problematic because this method may need to return very large intergers as Int64 or even UInt64.</remarks>
		public static object DeserializeBoxed(Slice slice)
		{
			if (slice.IsNullOrEmpty) return null;

			int type = slice[0];
			if (type <= FdbTupleTypes.IntPos8)
			{
				if (type >= FdbTupleTypes.IntNeg8) return FdbTupleParser.ParseInt64(type, slice);

				switch (type)
				{
					case FdbTupleTypes.Nil: return null;
					case FdbTupleTypes.Bytes: return FdbTupleParser.ParseBytes(slice);
					case FdbTupleTypes.Utf8: return FdbTupleParser.ParseUnicode(slice);
					case FdbTupleTypes.Guid: return FdbTupleParser.ParseGuid(slice);
				}
			}

			if (type >= FdbTupleTypes.AliasDirectory)
			{
				if (type == FdbTupleTypes.AliasSystem) return FdbTupleAlias.System;
				return FdbTupleAlias.Directory;
			}

			throw new FormatException("Cannot convert slice into this type");
		}

		/// <summary>Deserialize a slice into a type that implements ITupleFormattable</summary>
		/// <typeparam name="T">Type of a class that must implement ITupleFormattable and have a default constructor</typeparam>
		/// <param name="slice">Slice that contains a single packed element</param>
		/// <returns>Decoded value of type <typeparamref name="T"/></returns>
		/// <remarks>The type must have a default parameter-less constructor in order to be created.</remarks>
		public static T DeserializeFormattable<T>(Slice slice)
			where T : ITupleFormattable, new()
		{
			var tuple = FdbTuple.Unpack(slice);
			var value = new T();
			value.FromTuple(tuple);
			return value;
		}

		/// <summary>Deserialize a slice into a type that implements ITupleFormattable, using a custom factory method</summary>
		/// <typeparam name="T">Type of a class that must implement ITupleFormattable</typeparam>
		/// <param name="slice">Slice that contains a single packed element</param>
		/// <param name="factory">Lambda that will be called to construct a new instance of values of type <typeparamref name="T"/></param>
		/// <returns>Decoded value of type <typeparamref name="T"/></returns>
		public static T DeserializeFormattable<T>(Slice slice, Func<T> factory)
			where T : ITupleFormattable
		{
			var tuple = FdbTuple.Unpack(slice);
			var value = factory();
			value.FromTuple(tuple);
			return value;
		}

		/// <summary>Deserialize a slice into an Int32</summary>
		/// <param name="slice">Slice that contains a single packed element</param>
		/// <returns></returns>
		public static int DeserializeInt32(Slice slice)
		{
			checked
			{
				return (int)DeserializeInt64(slice);
			}
		}

		/// <summary>Deserialize a slice into an Int64</summary>
		/// <param name="slice">Slice that contains a single packed element</param>
		public static long DeserializeInt64(Slice slice)
		{
			if (slice.IsNullOrEmpty) return 0L; //TODO: fail ?

			int type = slice[0];
			if (type <= FdbTupleTypes.IntPos8)
			{
				if (type >= FdbTupleTypes.IntNeg8) return FdbTupleParser.ParseInt64(type, slice);

				switch (type)
				{
					case FdbTupleTypes.Nil: return 0;
					case FdbTupleTypes.Bytes: return long.Parse(FdbTupleParser.ParseAscii(slice), CultureInfo.InvariantCulture);
					case FdbTupleTypes.Utf8: return long.Parse(FdbTupleParser.ParseUnicode(slice), CultureInfo.InvariantCulture);
				}
			}

			throw new FormatException("Cannot convert slice into this type");
		}

		/// <summary>Deserialize a slice into an UInt32</summary>
		/// <param name="slice">Slice that contains a single packed element</param>
		public static uint DeserializeUInt32(Slice slice)
		{
			checked
			{
				return (uint)DeserializeUInt64(slice);
			}
		}

		/// <summary>Deserialize a slice into an UInt64</summary>
		/// <param name="slice">Slice that contains a single packed element</param>
		public static ulong DeserializeUInt64(Slice slice)
		{
			if (slice.IsNullOrEmpty) return 0UL; //TODO: fail ?

			int type = slice[0];
			if (type <= FdbTupleTypes.IntPos8)
			{
				if (type >= FdbTupleTypes.IntNeg8) return (ulong)FdbTupleParser.ParseInt64(type, slice);

				switch (type)
				{
					case FdbTupleTypes.Nil: return 0;
					case FdbTupleTypes.Bytes: return ulong.Parse(FdbTupleParser.ParseAscii(slice), CultureInfo.InvariantCulture);
					case FdbTupleTypes.Utf8: return ulong.Parse(FdbTupleParser.ParseUnicode(slice), CultureInfo.InvariantCulture);
				}
			}

			throw new FormatException("Cannot convert slice into this type");
		}

		/// <summary>Deserialize a slice into a Unicode string</summary>
		/// <param name="slice">Slice that contains a single packed element</param>
		public static string DeserializeString(Slice slice)
		{
			if (slice.IsNullOrEmpty) return null;

			int type = slice[0];
			if (type <= FdbTupleTypes.IntPos8)
			{
				if (type >= FdbTupleTypes.IntNeg8) return FdbTupleParser.ParseInt64(type, slice).ToString(CultureInfo.InvariantCulture);

				switch (type)
				{
					case FdbTupleTypes.Nil: return null;
					case FdbTupleTypes.Bytes: return FdbTupleParser.ParseAscii(slice);
					case FdbTupleTypes.Utf8: return FdbTupleParser.ParseUnicode(slice);
					case FdbTupleTypes.Guid: return FdbTupleParser.ParseGuid(slice).ToString();
				}
			}

			throw new FormatException("Cannot convert slice into this type");

		}

		/// <summary>Deserialize a slice into Guid</summary>
		/// <param name="slice">Slice that contains a single packed element</param>
		public static Guid DeserializeGuid(Slice slice)
		{
			if (slice.IsNullOrEmpty) return Guid.Empty;

			int type = slice[0];

			switch (type)
			{
				case FdbTupleTypes.Bytes:
				{
					return Guid.Parse(FdbTupleParser.ParseAscii(slice));
				}
				case FdbTupleTypes.Utf8:
				{
					return Guid.Parse(FdbTupleParser.ParseUnicode(slice));
				}
				case FdbTupleTypes.Guid:
				{
					return FdbTupleParser.ParseGuid(slice);
				}
			}

			throw new FormatException("Cannot convert slice into this type");
		}

		public static FdbTupleAlias DeserializeAlias(Slice slice)
		{
			if (slice.Count != 1) throw new FormatException("Cannot convert slice into this type");
			return (FdbTupleAlias)slice[0];
		}

		/// <summary>Unpack a tuple from a buffer</summary>
		/// <param name="buffer">Slice that contains the packed representation of a tuple with zero or more elements</param>
		/// <returns>Decoded tuple</returns>
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

		/// <summary>Ensure that a slice is a packed tuple that contains a single and valid element</summary>
		/// <param name="buffer">Slice that should contain the packed representation of a singleton tuple</param>
		/// <returns>Decoded slice of the single element in the singleton tuple</returns>
		internal static Slice UnpackSingle(Slice buffer)
		{
			var slicer = new Slicer(buffer);

			var current = ParseNext(ref slicer);
			if (slicer.HasMore) throw new FormatException("Parsing of singleton tuple failed before reaching the end of the key");

			return current;
		}

		/// <summary>Only returns the last item of a packed tuple</summary>
		/// <param name="buffer">Slice that contains the packed representation of a tuple with one or more elements</param>
		/// <returns>Raw slice corresponding to the last element of the tuple</returns>
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

		/// <summary>Decode the next token from a packed tuple</summary>
		/// <param name="reader">Parser from wich to read the next token</param>
		/// <returns>Token decoded, or Slice.Nil if there was no more data in the buffer</returns>
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

				case FdbTupleTypes.AliasDirectory:
				case FdbTupleTypes.AliasSystem:
				{ // <FE> or <FF>
					return reader.ReadBytes(1);
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
