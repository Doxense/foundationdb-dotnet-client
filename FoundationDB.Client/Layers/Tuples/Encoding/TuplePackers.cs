#region BSD Licence
/* Copyright (c) 2013-2018, Doxense SAS
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
	using System;
	using System.Collections.Generic;
	using System.Globalization;
	using System.Linq.Expressions;
	using System.Reflection;
	using Doxense.Diagnostics.Contracts;
	using FoundationDB.Client;
	using FoundationDB.Client.Converters;
	using JetBrains.Annotations;

	/// <summary>Helper methods used during serialization of values to the tuple binary format</summary>
	public static class TuplePackers
	{

		#region Serializers...

		public delegate void Encoder<in T>(ref TupleWriter writer, T value);

		/// <summary>Returns a lambda that will be able to serialize values of type <typeparamref name="T"/></summary>
		/// <typeparam name="T">Type of values to serialize</typeparam>
		/// <returns>Reusable action that knows how to serialize values of type <typeparamref name="T"/> into binary buffers, or an exception if the type is not supported</returns>
		[ContractAnnotation("true => notnull")]
		internal static Encoder<T> GetSerializer<T>(bool required)
		{
			var encoder = (Encoder<T>)GetSerializerFor(typeof(T));
			if (encoder == null && required)
			{
				encoder = delegate { throw new InvalidOperationException(String.Format("Does not know how to serialize values of type {0} into keys", typeof(T).Name)); };
			}
			return encoder;
		}

		private static Delegate GetSerializerFor([NotNull] Type type)
		{
			if (type == null) throw new ArgumentNullException("type");

			if (type == typeof(object))
			{ // return a generic serializer that will inspect the runtime type of the object
				return new Encoder<object>(TuplePackers.SerializeObjectTo);
			}

			var typeArgs = new[] { typeof(TupleWriter).MakeByRefType(), type };
			var method = typeof(TuplePackers).GetMethod("SerializeTo", BindingFlags.Static | BindingFlags.Public, null, typeArgs, null);
			if (method != null)
			{ // we have a direct serializer
				return method.CreateDelegate(typeof(Encoder<>).MakeGenericType(type));
			}

			// maybe if it is a tuple ?
			if (typeof(ITuple).IsAssignableFrom(type))
			{
				method = typeof(TuplePackers).GetMethod("SerializeTupleTo", BindingFlags.Static | BindingFlags.Public);
				if (method != null)
				{
					return method.MakeGenericMethod(type).CreateDelegate(typeof(Encoder<>).MakeGenericType(type));
				}
			}

			if (typeof(ITupleFormattable).IsAssignableFrom(type))
			{
				method = typeof(TuplePackers).GetMethod("SerializeFormattableTo", BindingFlags.Static | BindingFlags.Public);
				if (method != null)
				{
					return method.CreateDelegate(typeof(Encoder<>).MakeGenericType(type));
				}
			}

			var nullableType = Nullable.GetUnderlyingType(type);
			if (nullableType != null)
			{ // nullable types can reuse the underlying type serializer
				method = typeof(TuplePackers).GetMethod("SerializeNullableTo", BindingFlags.Static | BindingFlags.Public);
				if (method != null)
				{
					return method.MakeGenericMethod(nullableType).CreateDelegate(typeof(Encoder<>).MakeGenericType(type));
				}
			}

			// TODO: look for a static SerializeTo(BWB, T) method on the type itself ?

			// no luck..
			return null;
		}

		/// <summary>Serialize a nullable value, by checking for null at runtime</summary>
		/// <typeparam name="T">Underling type of the nullable type</typeparam>
		/// <param name="writer">Target buffer</param>
		/// <param name="value">Nullable value to serialize</param>
		/// <remarks>Uses the underlying type's serializer if the value is not null</remarks>
		public static void SerializeNullableTo<T>(ref TupleWriter writer, T? value)
			where T : struct
		{
			if (value == null)
				TupleParser.WriteNil(ref writer);
			else
				TuplePacker<T>.Encoder(ref writer, value.Value);
		}

		/// <summary>Serialize an untyped object, by checking its type at runtime</summary>
		/// <param name="writer">Target buffer</param>
		/// <param name="value">Untyped value whose type will be inspected at runtime</param>
		/// <remarks>May throw at runtime if the type is not supported</remarks>
		public static void SerializeObjectTo(ref TupleWriter writer, object value)
		{
			if (value == null)
			{ // null value
				// includes all null references to ref types, as nullables where HasValue == false
				TupleParser.WriteNil(ref writer);
				return;
			}

			switch (Type.GetTypeCode(value.GetType()))
			{
				case TypeCode.Empty:
				case TypeCode.Object:
				{
					byte[] bytes = value as byte[];
					if (bytes != null)
					{
						SerializeTo(ref writer, bytes);
						return;
					}

					if (value is Slice)
					{
						SerializeTo(ref writer, (Slice)value);
						return;
					}

					if (value is Guid)
					{
						SerializeTo(ref writer, (Guid)value);
						return;
					}

					if (value is Uuid128)
					{
						SerializeTo(ref writer, (Uuid128)value);
						return;
					}

					if (value is Uuid64)
					{
						SerializeTo(ref writer, (Uuid64)value);
						return;
					}

					if (value is TimeSpan)
					{
						SerializeTo(ref writer, (TimeSpan)value);
						return;
					}

					if (value is FdbTupleAlias)
					{
						SerializeTo(ref writer, (FdbTupleAlias)value);
						return;
					}

					break;
				}
				case TypeCode.DBNull:
				{ // same as null
					TupleParser.WriteNil(ref writer);
					return;
				}
				case TypeCode.Boolean:
				{
					SerializeTo(ref writer, (bool)value);
					return;
				}
				case TypeCode.Char:
				{
					// should be treated as a string with only one char
					SerializeTo(ref writer, (char)value);
					return;
				}
				case TypeCode.SByte:
				{
					SerializeTo(ref writer, (sbyte)value);
					return;
				}
				case TypeCode.Byte:
				{
					SerializeTo(ref writer, (byte)value);
					return;
				}
				case TypeCode.Int16:
				{
					SerializeTo(ref writer, (short)value);
					return;
				}
				case TypeCode.UInt16:
				{
					SerializeTo(ref writer, (ushort)value);
					return;
				}
				case TypeCode.Int32:
				{
					SerializeTo(ref writer, (int)value);
					return;
				}
				case TypeCode.UInt32:
				{
					SerializeTo(ref writer, (uint)value);
					return;
				}
				case TypeCode.Int64:
				{
					SerializeTo(ref writer, (long)value);
					return;
				}
				case TypeCode.UInt64:
				{
					SerializeTo(ref writer, (ulong)value);
					return;
				}
				case TypeCode.String:
				{
					SerializeTo(ref writer, value as string);
					return;
				}
				case TypeCode.DateTime:
				{
					SerializeTo(ref writer, (DateTime)value);
					return;
				}
				case TypeCode.Double:
				{
					SerializeTo(ref writer, (double)value);
					return;
				}
				case TypeCode.Single:
				{
					SerializeTo(ref writer, (float)value);
					return;
				}
			}

			var tuple = value as ITuple;
			if (tuple != null)
			{
				SerializeTupleTo(ref writer, tuple);
				return;
			}

			var fmt = value as ITupleFormattable;
			if (fmt != null)
			{
				tuple = fmt.ToTuple();
				if (tuple == null) throw new InvalidOperationException(String.Format("An instance of type {0} returned a null Tuple while serialiazing", value.GetType().Name));
				SerializeTupleTo(ref writer, tuple);
				return;
			}

			// Not Supported ?
			throw new NotSupportedException(String.Format("Doesn't know how to serialize objects of type {0} into Tuple Encoding format", value.GetType().Name));
		}

		/// <summary>Writes a slice as a byte[] array</summary>
		public static void SerializeTo(ref TupleWriter writer, Slice value)
		{
			if (value.IsNull)
			{
				TupleParser.WriteNil(ref writer);
			}
			else if (value.Offset == 0 && value.Count == value.Array.Length)
			{
				TupleParser.WriteBytes(ref writer, value.Array);
			}
			else
			{
				TupleParser.WriteBytes(ref writer, value.Array, value.Offset, value.Count);
			}
		}

		/// <summary>Writes a byte[] array</summary>
		public static void SerializeTo(ref TupleWriter writer, byte[] value)
		{
			TupleParser.WriteBytes(ref writer, value);
		}

		/// <summary>Writes an array segment as a byte[] array</summary>
		public static void SerializeTo(ref TupleWriter writer, ArraySegment<byte> value)
		{
			SerializeTo(ref writer, Slice.Create(value));
		}

		/// <summary>Writes a char as Unicode string</summary>
		public static void SerializeTo(ref TupleWriter writer, char value)
		{
			TupleParser.WriteChar(ref writer, value);
		}

		/// <summary>Writes a boolean as an integer</summary>
		/// <remarks>Uses 0 for false, and -1 for true</remarks>
		public static void SerializeTo(ref TupleWriter writer, bool value)
		{
			TupleParser.WriteBool(ref writer, value);
		}

		/// <summary>Writes a boolean as an integer or null</summary>
		public static void SerializeTo(ref TupleWriter writer, bool? value)
		{
			if (value == null)
			{ // null => 00
				TupleParser.WriteNil(ref writer);
			}
			else
			{
				TupleParser.WriteBool(ref writer, value.Value);
			}
		}

		/// <summary>Writes a signed byte as an integer</summary>
		public static void SerializeTo(ref TupleWriter writer, sbyte value)
		{
			TupleParser.WriteInt32(ref writer, value);
		}

		/// <summary>Writes an unsigned byte as an integer</summary>
		public static void SerializeTo(ref TupleWriter writer, byte value)
		{
			TupleParser.WriteByte(ref writer, value);
		}

		/// <summary>Writes a signed word as an integer</summary>
		public static void SerializeTo(ref TupleWriter writer, short value)
		{
			TupleParser.WriteInt32(ref writer, value);
		}

		/// <summary>Writes an unsigned word as an integer</summary>
		public static void SerializeTo(ref TupleWriter writer, ushort value)
		{
			TupleParser.WriteUInt32(ref writer, value);
		}

		/// <summary>Writes a signed int as an integer</summary>
		public static void SerializeTo(ref TupleWriter writer, int value)
		{
			TupleParser.WriteInt32(ref writer, value);
		}

		/// <summary>Writes an unsigned int as an integer</summary>
		public static void SerializeTo(ref TupleWriter writer, uint value)
		{
			TupleParser.WriteUInt32(ref writer, value);
		}

		/// <summary>Writes a signed long as an integer</summary>
		public static void SerializeTo(ref TupleWriter writer, long value)
		{
			TupleParser.WriteInt64(ref writer, value);
		}

		/// <summary>Writes an unsigned long as an integer</summary>
		public static void SerializeTo(ref TupleWriter writer, ulong value)
		{
			TupleParser.WriteUInt64(ref writer, value);
		}

		/// <summary>Writes a 32-bit IEEE floating point number</summary>
		public static void SerializeTo(ref TupleWriter writer, float value)
		{
			TupleParser.WriteSingle(ref writer, value);
		}

		/// <summary>Writes a 64-bit IEEE floating point number</summary>
		public static void SerializeTo(ref TupleWriter writer, double value)
		{
			TupleParser.WriteDouble(ref writer, value);
		}

		/// <summary>Writes a string as an Unicode string</summary>
		public static void SerializeTo(ref TupleWriter writer, string value)
		{
			TupleParser.WriteString(ref writer, value);
		}

		/// <summary>Writes a DateTime converted to the number of days since the Unix Epoch and stored as a 64-bit decimal</summary>
		public static void SerializeTo(ref TupleWriter writer, DateTime value)
		{
			// The problem of serializing DateTime: TimeZone? Precision?
			// - Since we are going to lose the TimeZone infos anyway, we can just store everything in UTC and let the caller deal with it
			// - DateTime in .NET uses Ticks which produce numbers too large to fit in the 56 bits available in JavaScript
			// - Most other *nix uses the number of milliseconds since 1970-Jan-01 UTC, but if we store as an integer we will lose some precision (rounded to nearest millisecond)
			// - We could store the number of milliseconds as a floating point value, which would require support of Floating Points in the Tuple Encoding (currently a Draft)
			// - Other database engines store dates as a number of DAYS since Epoch, using a floating point number. This allows for quickly extracting the date by truncating the value, and the time by using the decimal part

			// Right now, we will store the date as the number of DAYS since Epoch, using a 64-bit float.
			// => storing a number of ticks would be MS-only anyway (56-bit limit in JS)
			// => JS binding MAY support decoding of 64-bit floats in the future, in which case the value would be preserved exactly.

			const long UNIX_EPOCH_EPOCH = 621355968000000000L;
			double ms = (value.ToUniversalTime().Ticks - UNIX_EPOCH_EPOCH) / (double)TimeSpan.TicksPerDay;

			TupleParser.WriteDouble(ref writer, ms);
		}

		/// <summary>Writes a TimeSpan converted to to a number seconds encoded as a 64-bit decimal</summary>
		public static void SerializeTo(ref TupleWriter writer, TimeSpan value)
		{
			// We have the same precision problem with storing DateTimes:
			// - Storing the number of ticks keeps the exact value, but is Windows-centric
			// - Storing the number of milliseconds as an integer will round the precision to 1 millisecond, which is not acceptable
			// - We could store the the number of milliseconds as a floating point value, which would require support of Floating Points in the Tuple Encoding (currently a Draft)
			// - It is frequent for JSON APIs and other database engines to represent durations as a number of SECONDS, using a floating point number.

			// Right now, we will store the duration as the number of seconds, using a 64-bit float

			TupleParser.WriteDouble(ref writer, value.TotalSeconds);
		}

		/// <summary>Writes a Guid as a 128-bit UUID</summary>
		public static void SerializeTo(ref TupleWriter writer, Guid value)
		{
			//REVIEW: should we consider serializing Guid.Empty as <14> (integer 0) ? or maybe <01><00> (empty bytestring) ?
			// => could spare ~16 bytes per key in indexes on GUID properties that are frequently missing or empty (== default(Guid))
			TupleParser.WriteGuid(ref writer, value);
		}

		/// <summary>Writes a Uuid as a 128-bit UUID</summary>
		public static void SerializeTo(ref TupleWriter writer, Uuid128 value)
		{
			TupleParser.WriteUuid128(ref writer, value);
		}

		/// <summary>Writes a Uuid as a 64-bit UUID</summary>
		public static void SerializeTo(ref TupleWriter writer, Uuid64 value)
		{
			TupleParser.WriteUuid64(ref writer, value);
		}

		/// <summary>Writes an IPaddress as a 32-bit (IPv4) or 128-bit (IPv6) byte array</summary>
		public static void SerializeTo(ref TupleWriter writer, System.Net.IPAddress value)
		{
			TupleParser.WriteBytes(ref writer, value != null ? value.GetAddressBytes() : null);
		}

		public static void SerializeTo(ref TupleWriter writer, FdbTupleAlias value)
		{
			Contract.Requires(Enum.IsDefined(typeof(FdbTupleAlias), value));

			writer.Output.WriteByte((byte)value);
		}

		public static void SerializeTupleTo<TTuple>(ref TupleWriter writer, TTuple tuple)
			where TTuple : ITuple
		{
			Contract.Requires(tuple != null);

			TupleParser.BeginTuple(ref writer);
			tuple.PackTo(ref writer);
			TupleParser.EndTuple(ref writer);
		}

		public static void SerializeFormattableTo(ref TupleWriter writer, ITupleFormattable formattable)
		{
			if (formattable == null)
			{
				TupleParser.WriteNil(ref writer);
				return;
			}

			var tuple = formattable.ToTuple();
			if (tuple == null) throw new InvalidOperationException(String.Format("Custom formatter {0}.ToTuple() cannot return null", formattable.GetType().Name));

			TupleParser.BeginTuple(ref writer);
			tuple.PackTo(ref writer);
			TupleParser.EndTuple(ref writer);
		}

		#endregion

		#region Deserializers...

		private static readonly Dictionary<Type, Delegate> s_sliceUnpackers = InitializeDefaultUnpackers();

		[NotNull]
		private static Dictionary<Type, Delegate> InitializeDefaultUnpackers()
		{
			var map = new Dictionary<Type, Delegate>();

			map[typeof(Slice)] = new Func<Slice, Slice>(TuplePackers.DeserializeSlice);
			map[typeof(byte[])] = new Func<Slice, byte[]>(TuplePackers.DeserializeBytes);
			map[typeof(bool)] = new Func<Slice, bool>(TuplePackers.DeserializeBoolean);
			map[typeof(string)] = new Func<Slice, string>(TuplePackers.DeserializeString);
			map[typeof(sbyte)] = new Func<Slice, sbyte>(TuplePackers.DeserializeSByte);
			map[typeof(short)] = new Func<Slice, short>(TuplePackers.DeserializeInt16);
			map[typeof(int)] = new Func<Slice, int>(TuplePackers.DeserializeInt32);
			map[typeof(long)] = new Func<Slice, long>(TuplePackers.DeserializeInt64);
			map[typeof(byte)] = new Func<Slice, byte>(TuplePackers.DeserializeByte);
			map[typeof(ushort)] = new Func<Slice, ushort>(TuplePackers.DeserializeUInt16);
			map[typeof(uint)] = new Func<Slice, uint>(TuplePackers.DeserializeUInt32);
			map[typeof(ulong)] = new Func<Slice, ulong>(TuplePackers.DeserializeUInt64);
			map[typeof(float)] = new Func<Slice, float>(TuplePackers.DeserializeSingle);
			map[typeof(double)] = new Func<Slice, double>(TuplePackers.DeserializeDouble);
			map[typeof(Guid)] = new Func<Slice, Guid>(TuplePackers.DeserializeGuid);
			map[typeof(Uuid128)] = new Func<Slice, Uuid128>(TuplePackers.DeserializeUuid128);
			map[typeof(Uuid64)] = new Func<Slice, Uuid64>(TuplePackers.DeserializeUuid64);
			map[typeof(TimeSpan)] = new Func<Slice, TimeSpan>(TuplePackers.DeserializeTimeSpan);
			map[typeof(DateTime)] = new Func<Slice, DateTime>(TuplePackers.DeserializeDateTime);
			map[typeof(System.Net.IPAddress)] = new Func<Slice, System.Net.IPAddress>(TuplePackers.DeserializeIPAddress);

			// add Nullable versions for all these types
			return map;
		}

		/// <summary>Returns a lambda that will be able to serialize values of type <typeparamref name="T"/></summary>
		/// <typeparam name="T">Type of values to serialize</typeparam>
		/// <returns>Reusable action that knows how to serialize values of type <typeparamref name="T"/> into binary buffers, or an exception if the type is not supported</returns>
		[NotNull]
		internal static Func<Slice, T> GetDeserializer<T>(bool required)
		{
			Type type = typeof(T);

			Delegate decoder;
			if (s_sliceUnpackers.TryGetValue(type, out decoder))
			{
				return (Func<Slice, T>)decoder;
			}

			//TODO: handle nullable types?
			var underlyingType = Nullable.GetUnderlyingType(typeof(T));
			if (underlyingType != null && s_sliceUnpackers.TryGetValue(underlyingType, out decoder))
			{
				decoder = MakeNullableDeserializer(type, underlyingType, decoder);
				if (decoder != null) return (Func<Slice, T>)decoder;
			}

			if (required)
			{
				return (_) => { throw new InvalidOperationException(String.Format("Does not know how to deserialize keys into values of type {0}", typeof(T).Name)); };
			}
			else
			{ // when all else fails...
				return (value) => FdbConverters.ConvertBoxed<T>(DeserializeBoxed(value));
			}
		}

		/// <summary>Check if a tuple segment is the equivalent of 'Nil'</summary>
		internal static bool IsNilSegment(Slice slice)
		{
			return slice.IsNullOrEmpty || slice[0] == TupleTypes.Nil;
		}

		private static Delegate MakeNullableDeserializer([NotNull] Type nullableType, [NotNull] Type type, [NotNull] Delegate decoder)
		{
			Contract.Requires(nullableType != null && type != null && decoder != null);
			// We have a Decoder of T, but we have to transform it into a Decoder for Nullable<T>, which returns null if the slice is "nil", or falls back to the underlying decoder if the slice contains something

			var prmSlice = Expression.Parameter(typeof(Slice), "slice");
			var body = Expression.Condition(
				// IsNilSegment(slice) ?
				Expression.Call(typeof(TuplePackers).GetMethod("IsNilSegment", BindingFlags.Static | BindingFlags.NonPublic), prmSlice),
				// True => default(Nullable<T>)
				Expression.Default(nullableType),
				// False => decoder(slice)
				Expression.Convert(Expression.Invoke(Expression.Constant(decoder), prmSlice), nullableType)
			);

			return Expression.Lambda(body, prmSlice).Compile();
		}

		/// <summary>Deserialize a packed element into an object by choosing the most appropriate type at runtime</summary>
		/// <param name="slice">Slice that contains a single packed element</param>
		/// <returns>Decoded element, in the type that is the best fit.</returns>
		/// <remarks>You should avoid working with untyped values as much as possible! Blindly casting the returned object may be problematic because this method may need to return very large intergers as Int64 or even UInt64.</remarks>
		[CanBeNull]
		public static object DeserializeBoxed(Slice slice)
		{
			if (slice.IsNullOrEmpty) return null;

			int type = slice[0];
			if (type <= TupleTypes.IntPos8)
			{
				if (type >= TupleTypes.IntNeg8) return TupleParser.ParseInt64(type, slice);

				switch (type)
				{
					case TupleTypes.Nil: return null;
					case TupleTypes.Bytes: return TupleParser.ParseBytes(slice);
					case TupleTypes.Utf8: return TupleParser.ParseUnicode(slice);
					case TupleTypes.TupleStart: return TupleParser.ParseTuple(slice);
				}
			}
			else
			{
				switch (type)
				{
					case TupleTypes.Single: return TupleParser.ParseSingle(slice);
					case TupleTypes.Double: return TupleParser.ParseDouble(slice);
					case TupleTypes.Uuid128: return TupleParser.ParseGuid(slice);
					case TupleTypes.Uuid64: return TupleParser.ParseUuid64(slice);
					case TupleTypes.AliasDirectory: return FdbTupleAlias.Directory;
					case TupleTypes.AliasSystem: return FdbTupleAlias.System;
				}
			}

			throw new FormatException(String.Format("Cannot convert tuple segment with unknown type code {0}", type));
		}

		/// <summary>Deserialize a slice into a type that implements ITupleFormattable</summary>
		/// <typeparam name="T">Type of a class that must implement ITupleFormattable and have a default constructor</typeparam>
		/// <param name="slice">Slice that contains a single packed element</param>
		/// <returns>Decoded value of type <typeparamref name="T"/></returns>
		/// <remarks>The type must have a default parameter-less constructor in order to be created.</remarks>
		public static T DeserializeFormattable<T>(Slice slice)
			where T : ITupleFormattable, new()
		{
			if (TuplePackers.IsNilSegment(slice))
			{
				return default(T);
			}

			var tuple = TupleParser.ParseTuple(slice);
			var value = new T();
			value.FromTuple(tuple);
			return value;
		}

		/// <summary>Deserialize a slice into a type that implements ITupleFormattable, using a custom factory method</summary>
		/// <typeparam name="T">Type of a class that must implement ITupleFormattable</typeparam>
		/// <param name="slice">Slice that contains a single packed element</param>
		/// <param name="factory">Lambda that will be called to construct a new instance of values of type <typeparamref name="T"/></param>
		/// <returns>Decoded value of type <typeparamref name="T"/></returns>
		public static T DeserializeFormattable<T>(Slice slice, [NotNull] Func<T> factory)
			where T : ITupleFormattable
		{
			var tuple = TupleParser.ParseTuple(slice);
			var value = factory();
			value.FromTuple(tuple);
			return value;
		}

		/// <summary>Deserialize a tuple segment into a Slice</summary>
		public static Slice DeserializeSlice(Slice slice)
		{
			// Convert the tuple value into a sensible Slice representation.
			// The behavior should be equivalent to calling the corresponding Slice.From{TYPE}(TYPE value)

			if (slice.IsNullOrEmpty) return Slice.Nil; //TODO: fail ?

			byte type = slice[0];
			switch(type)
			{
				case TupleTypes.Nil: return Slice.Nil;
				case TupleTypes.Bytes: return TupleParser.ParseBytes(slice);
				case TupleTypes.Utf8: return Slice.FromString(TupleParser.ParseUnicode(slice));

				case TupleTypes.Single: return Slice.FromSingle(TupleParser.ParseSingle(slice));
				case TupleTypes.Double: return Slice.FromDouble(TupleParser.ParseDouble(slice));

				case TupleTypes.Uuid128: return Slice.FromGuid(TupleParser.ParseGuid(slice));
				case TupleTypes.Uuid64: return Slice.FromUuid64(TupleParser.ParseUuid64(slice));
			}

			if (type <= TupleTypes.IntPos8 && type >= TupleTypes.IntNeg8)
			{
				if (type >= TupleTypes.IntBase) return Slice.FromInt64(DeserializeInt64(slice));
				return Slice.FromUInt64(DeserializeUInt64(slice));
			}

			throw new FormatException(String.Format("Cannot convert tuple segment of type 0x{0:X} into a Slice", type));
		}

		/// <summary>Deserialize a tuple segment into a byte array</summary>
		[CanBeNull] //REVIEW: because of Slice.GetBytes()
		public static byte[] DeserializeBytes(Slice slice)
		{
			return DeserializeSlice(slice).GetBytes();
		}

		/// <summary>Deserialize a tuple segment into a tuple</summary>
		[CanBeNull]
		public static ITuple DeserializeTuple(Slice slice)
		{
			if (slice.IsNullOrEmpty) return null;

			byte type = slice[0];
			switch(type)
			{
				case TupleTypes.Nil:
				{
					return null;
				}
				case TupleTypes.Bytes:
				{
					return STuple.Unpack(TupleParser.ParseBytes(slice));
				}
				case TupleTypes.TupleStart:
				{
					return TupleParser.ParseTuple(slice);
				}
			}

			throw new FormatException("Cannot convert tuple segment into a Tuple");
		}

		/// <summary>Deserialize a tuple segment into a Boolean</summary>
		/// <param name="slice">Slice that contains a single packed element</param>
		public static bool DeserializeBoolean(Slice slice)
		{
			if (slice.IsNullOrEmpty) return false; //TODO: fail ?

			byte type = slice[0];

			// Booleans are usually encoded as integers, with 0 for False (<14>) and 1 for True (<15><01>)
			if (type <= TupleTypes.IntPos8 && type >= TupleTypes.IntNeg8)
			{
				//note: DeserializeInt64 handles most cases
				return 0 != DeserializeInt64(slice);
			}

			switch (type)
			{
				case TupleTypes.Bytes:
				{ // empty is false, all other is true
					return slice.Count != 2; // <01><00>
				}
				case TupleTypes.Utf8:
				{// empty is false, all other is true
					return slice.Count != 2; // <02><00>
				}
				case TupleTypes.Single:
				{
					//TODO: should NaN considered to be false ?
					return 0f != TupleParser.ParseSingle(slice);
				}
				case TupleTypes.Double:
				{
					//TODO: should NaN considered to be false ?
					return 0f != TupleParser.ParseDouble(slice);
				}
			}

			//TODO: should we handle weird cases like strings "True" and "False"?

			throw new FormatException(String.Format("Cannot convert tuple segment of type 0x{0:X} into a boolean", type));
		}

		/// <summary>Deserialize a tuple segment into an Int16</summary>
		/// <param name="slice">Slice that contains a single packed element</param>
		public static sbyte DeserializeSByte(Slice slice)
		{
			return checked((sbyte)DeserializeInt64(slice));
		}

		/// <summary>Deserialize a tuple segment into an Int16</summary>
		/// <param name="slice">Slice that contains a single packed element</param>
		public static short DeserializeInt16(Slice slice)
		{
			return checked((short)DeserializeInt64(slice));
		}

		/// <summary>Deserialize a tuple segment into an Int32</summary>
		/// <param name="slice">Slice that contains a single packed element</param>
		public static int DeserializeInt32(Slice slice)
		{
			return checked((int)DeserializeInt64(slice));
		}

		/// <summary>Deserialize a tuple segment into an Int64</summary>
		/// <param name="slice">Slice that contains a single packed element</param>
		public static long DeserializeInt64(Slice slice)
		{
			if (slice.IsNullOrEmpty) return 0L; //TODO: fail ?

			int type = slice[0];
			if (type <= TupleTypes.IntPos8)
			{
				if (type >= TupleTypes.IntNeg8) return TupleParser.ParseInt64(type, slice);

				switch (type)
				{
					case TupleTypes.Nil: return 0;
					case TupleTypes.Bytes: return long.Parse(TupleParser.ParseAscii(slice), CultureInfo.InvariantCulture);
					case TupleTypes.Utf8: return long.Parse(TupleParser.ParseUnicode(slice), CultureInfo.InvariantCulture);
				}
			}

			throw new FormatException(String.Format("Cannot convert tuple segment of type 0x{0:X} into a signed integer", type));
		}

		/// <summary>Deserialize a tuple segment into an UInt32</summary>
		/// <param name="slice">Slice that contains a single packed element</param>
		public static byte DeserializeByte(Slice slice)
		{
			return checked((byte)DeserializeUInt64(slice));
		}

		/// <summary>Deserialize a tuple segment into an UInt32</summary>
		/// <param name="slice">Slice that contains a single packed element</param>
		public static ushort DeserializeUInt16(Slice slice)
		{
			return checked((ushort)DeserializeUInt64(slice));
		}

		/// <summary>Deserialize a slice into an UInt32</summary>
		/// <param name="slice">Slice that contains a single packed element</param>
		public static uint DeserializeUInt32(Slice slice)
		{
			return checked((uint)DeserializeUInt64(slice));
		}

		/// <summary>Deserialize a tuple segment into an UInt64</summary>
		/// <param name="slice">Slice that contains a single packed element</param>
		public static ulong DeserializeUInt64(Slice slice)
		{
			if (slice.IsNullOrEmpty) return 0UL; //TODO: fail ?

			int type = slice[0];
			if (type <= TupleTypes.IntPos8)
			{
				if (type >= TupleTypes.IntZero) return (ulong)TupleParser.ParseInt64(type, slice);
				if (type < TupleTypes.IntZero) throw new OverflowException(); // negative values

				switch (type)
				{
					case TupleTypes.Nil: return 0;
					case TupleTypes.Bytes: return ulong.Parse(TupleParser.ParseAscii(slice), CultureInfo.InvariantCulture);
					case TupleTypes.Utf8: return ulong.Parse(TupleParser.ParseUnicode(slice), CultureInfo.InvariantCulture);
				}
			}

			throw new FormatException(String.Format("Cannot convert tuple segment of type 0x{0:X} into an unsigned integer", type));
		}

		public static float DeserializeSingle(Slice slice)
		{
			if (slice.IsNullOrEmpty) return 0;

			byte type = slice[0];
			switch (type)
			{
				case TupleTypes.Nil:
				{
					return 0;
				}
				case TupleTypes.Utf8:
				{
					return Single.Parse(TupleParser.ParseUnicode(slice), CultureInfo.InvariantCulture);
				}
				case TupleTypes.Single:
				{
					return TupleParser.ParseSingle(slice);
				}
				case TupleTypes.Double:
				{
					return (float)TupleParser.ParseDouble(slice);
				}
			}

			if (type <= TupleTypes.IntPos8 && type >= TupleTypes.IntNeg8)
			{
				return checked((float)DeserializeInt64(slice));
			}

			throw new FormatException(String.Format("Cannot convert tuple segment of type 0x{0:X} into a Single", type));
		}

		public static double DeserializeDouble(Slice slice)
		{
			if (slice.IsNullOrEmpty) return 0;

			byte type = slice[0];
			switch(type)
			{
				case TupleTypes.Nil:
				{
					return 0;
				}
				case TupleTypes.Utf8:
				{
					return Double.Parse(TupleParser.ParseUnicode(slice), CultureInfo.InvariantCulture);
				}
				case TupleTypes.Single:
				{
					return (double)TupleParser.ParseSingle(slice);
				}
				case TupleTypes.Double:
				{
					return TupleParser.ParseDouble(slice);
				}
			}

			if (type <= TupleTypes.IntPos8 && type >= TupleTypes.IntNeg8)
			{
				return checked((double)DeserializeInt64(slice));
			}

			throw new FormatException(String.Format("Cannot convert tuple segment of type 0x{0:X} into a Double", type));
		}

		/// <summary>Deserialize a tuple segment into a DateTime (UTC)</summary>
		/// <param name="slice">Slice that contains a single packed element</param>
		/// <returns>DateTime in UTC</returns>
		/// <remarks>The returned DateTime will be in UTC, because the original TimeZone details are lost.</remarks>
		public static DateTime DeserializeDateTime(Slice slice)
		{
			if (slice.IsNullOrEmpty) return DateTime.MinValue; //TODO: fail ?

			byte type = slice[0];

			switch(type)
			{
				case TupleTypes.Nil:
				{
					return DateTime.MinValue;
				}

				case TupleTypes.Utf8:
				{ // we only support ISO 8601 dates. For ex: YYYY-MM-DDTHH:MM:SS.fffff"
					string str = TupleParser.ParseUnicode(slice);
					return DateTime.Parse(str, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);
				}

				case TupleTypes.Double:
				{ // Number of days since Epoch
					const long UNIX_EPOCH_TICKS = 621355968000000000L;
					//note: we can't user TimeSpan.FromDays(...) because it rounds to the nearest millisecond!
					long ticks = UNIX_EPOCH_TICKS + (long)(TupleParser.ParseDouble(slice) * TimeSpan.TicksPerDay);
					return new DateTime(ticks, DateTimeKind.Utc);
				}
			}

			// If we have an integer, we consider it to be a number of Ticks (Windows Only)
			if (type <= TupleTypes.IntPos8 && type >= TupleTypes.IntNeg8)
			{
				return new DateTime(DeserializeInt64(slice), DateTimeKind.Utc);
			}

			throw new FormatException(String.Format("Cannot convert tuple segment of type 0x{0:X} into a DateTime", type));
		}

		/// <summary>Deserialize a tuple segment into a TimeSpan</summary>
		/// <param name="slice">Slice that contains a single packed element</param>
		public static TimeSpan DeserializeTimeSpan(Slice slice)
		{
			if (slice.IsNullOrEmpty) return TimeSpan.Zero; //TODO: fail ?

			byte type = slice[0];

			// We serialize TimeSpans as number of seconds in a 64-bit float.

			switch(type)
			{
				case TupleTypes.Nil:
				{
					return TimeSpan.Zero;
				}
				case TupleTypes.Utf8:
				{ // "HH:MM:SS.fffff"
					return TimeSpan.Parse(TupleParser.ParseUnicode(slice), CultureInfo.InvariantCulture);
				}
				case TupleTypes.Double:
				{ // Number of seconds
					//note: We can't use TimeSpan.FromSeconds(...) because it rounds to the nearest millisecond!
					return new TimeSpan((long)(TupleParser.ParseDouble(slice) * (double)TimeSpan.TicksPerSecond));
				}
			}

			// If we have an integer, we consider it to be a number of Ticks (Windows Only)
			if (type <= TupleTypes.IntPos8 && type >= TupleTypes.IntNeg8)
			{
				return new TimeSpan(DeserializeInt64(slice));
			}

			throw new FormatException(String.Format("Cannot convert tuple segment of type 0x{0:X} into a TimeSpan", type));
		}

		/// <summary>Deserialize a tuple segment into a Unicode string</summary>
		/// <param name="slice">Slice that contains a single packed element</param>
		[CanBeNull]
		public static string DeserializeString(Slice slice)
		{
			if (slice.IsNullOrEmpty) return null;

			byte type = slice[0];
			switch (type)
			{
				case TupleTypes.Nil:
				{
					return null;
				}
				case TupleTypes.Bytes:
				{
					return TupleParser.ParseAscii(slice);
				}
				case TupleTypes.Utf8:
				{
					return TupleParser.ParseUnicode(slice);
				}
				case TupleTypes.Single:
				{
					return TupleParser.ParseSingle(slice).ToString(CultureInfo.InvariantCulture);
				}
				case TupleTypes.Double:
				{
					return TupleParser.ParseDouble(slice).ToString(CultureInfo.InvariantCulture);
				}
				case TupleTypes.Uuid128:
				{
					return TupleParser.ParseGuid(slice).ToString();
				}
				case TupleTypes.Uuid64:
				{
					return TupleParser.ParseUuid64(slice).ToString();
				}
			}

			if (type <= TupleTypes.IntPos8 && type >= TupleTypes.IntNeg8)
			{
				return TupleParser.ParseInt64(type, slice).ToString(CultureInfo.InvariantCulture);
			}

			throw new FormatException(String.Format("Cannot convert tuple segment of type 0x{0:X} into a String", type));
		}

		/// <summary>Deserialize a tuple segment into Guid</summary>
		/// <param name="slice">Slice that contains a single packed element</param>
		public static Guid DeserializeGuid(Slice slice)
		{
			if (slice.IsNullOrEmpty) return Guid.Empty;

			int type = slice[0];

			switch (type)
			{
				case TupleTypes.Bytes:
				{
					return Guid.Parse(TupleParser.ParseAscii(slice));
				}
				case TupleTypes.Utf8:
				{
					return Guid.Parse(TupleParser.ParseUnicode(slice));
				}
				case TupleTypes.Uuid128:
				{
					return TupleParser.ParseGuid(slice);
				}
				//REVIEW: should we allow converting a Uuid64 into a Guid? This looks more like a bug than an expected behavior...
			}

			throw new FormatException(String.Format("Cannot convert tuple segment of type 0x{0:X} into a System.Guid", type));
		}

		/// <summary>Deserialize a tuple segment into 128-bit UUID</summary>
		/// <param name="slice">Slice that contains a single packed element</param>
		public static Uuid128 DeserializeUuid128(Slice slice)
		{
			if (slice.IsNullOrEmpty) return Uuid128.Empty;

			int type = slice[0];

			switch (type)
			{
				case TupleTypes.Bytes:
				{ // expect binary representation as a 16-byte array
					return new Uuid128(TupleParser.ParseBytes(slice));
				}
				case TupleTypes.Utf8:
				{ // expect text representation
					return new Uuid128(TupleParser.ParseUnicode(slice));
				}
				case TupleTypes.Uuid128:
				{
					return TupleParser.ParseUuid128(slice);
				}
				//REVIEW: should we allow converting a Uuid64 into a Uuid128? This looks more like a bug than an expected behavior...
			}

			throw new FormatException(String.Format("Cannot convert tuple segment of type 0x{0:X} into an Uuid128", type));
		}

		/// <summary>Deserialize a tuple segment into 64-bit UUID</summary>
		/// <param name="slice">Slice that contains a single packed element</param>
		public static Uuid64 DeserializeUuid64(Slice slice)
		{
			if (slice.IsNullOrEmpty) return Uuid64.Empty;

			int type = slice[0];

			switch (type)
			{
				case TupleTypes.Bytes:
				{ // expect binary representation as a 16-byte array
					return new Uuid64(TupleParser.ParseBytes(slice));
				}
				case TupleTypes.Utf8:
				{ // expect text representation
					return new Uuid64(TupleParser.ParseUnicode(slice));
				}
				case TupleTypes.Uuid64:
				{
					return TupleParser.ParseUuid64(slice);
				}
			}

			if (type >= TupleTypes.IntZero && type <= TupleTypes.IntPos8)
			{ // expect 64-bit number
				return new Uuid64(TupleParser.ParseInt64(type, slice));
			}
			// we don't support negative numbers!

			throw new FormatException(String.Format("Cannot convert tuple segment of type 0x{0:X} into an Uuid64", type));
		}

		/// <summary>Deserialize a tuple segment into Guid</summary>
		/// <param name="slice">Slice that contains a single packed element</param>
		[CanBeNull]
		public static System.Net.IPAddress DeserializeIPAddress(Slice slice)
		{
			if (slice.IsNullOrEmpty) return null;

			int type = slice[0];

			switch (type)
			{
				case TupleTypes.Bytes:
				{
					return new System.Net.IPAddress(TupleParser.ParseBytes(slice).GetBytes());
				}
				case TupleTypes.Utf8:
				{
					return System.Net.IPAddress.Parse(TupleParser.ParseUnicode(slice));
				}
				case TupleTypes.Uuid128:
				{ // could be an IPv6 encoded as a 128-bits UUID
					return new System.Net.IPAddress(slice.GetBytes());
				}
			}

			if (type >= TupleTypes.IntPos1 && type <= TupleTypes.IntPos4)
			{ // could be an IPv4 encoded as a 32-bit unsigned integer
				var value = TupleParser.ParseInt64(type, slice);
				Contract.Assert(value >= 0 && value <= uint.MaxValue);
				return new System.Net.IPAddress(value);
			}

			throw new FormatException(String.Format("Cannot convert tuple segment of type 0x{0:X} into System.Net.IPAddress", type));
		}

		public static FdbTupleAlias DeserializeAlias(Slice slice)
		{
			if (slice.Count != 1) throw new FormatException("Cannot convert tuple segment into this type");
			return (FdbTupleAlias)slice[0];
		}

		/// <summary>Unpack a tuple from a buffer</summary>
		/// <param name="buffer">Slice that contains the packed representation of a tuple with zero or more elements</param>
		/// <returns>Decoded tuple</returns>
		[NotNull]
		internal static SlicedTuple Unpack(Slice buffer, bool embedded)
		{
			var reader = new TupleReader(buffer);
			if (embedded) reader.Depth = 1;

			// most tuples will probably fit within (prefix, sub-prefix, id, key) so pre-allocating with 4 should be ok...
			var items = new Slice[4];

			Slice item;
			int p = 0;
			while ((item = TupleParser.ParseNext(ref reader)).HasValue)
			{
				if (p >= items.Length)
				{
					// note: do not grow exponentially, because tuples will never but very large...
					Array.Resize(ref items, p + 4);
				}
				items[p++] = item;
			}

			if (reader.Input.HasMore) throw new FormatException("Parsing of tuple failed failed before reaching the end of the key");
			return new SlicedTuple(p == 0 ? Slice.EmptySliceArray : items, 0, p);
		}

		/// <summary>Ensure that a slice is a packed tuple that contains a single and valid element</summary>
		/// <param name="buffer">Slice that should contain the packed representation of a singleton tuple</param>
		/// <returns>Decoded slice of the single element in the singleton tuple</returns>
		public static Slice UnpackSingle(Slice buffer)
		{
			var slicer = new TupleReader(buffer);

			var current = TupleParser.ParseNext(ref slicer);
			if (slicer.Input.HasMore) throw new FormatException("Parsing of singleton tuple failed before reaching the end of the key");

			return current;
		}

		/// <summary>Only returns the first item of a packed tuple</summary>
		/// <param name="buffer">Slice that contains the packed representation of a tuple with one or more elements</param>
		/// <returns>Raw slice corresponding to the first element of the tuple</returns>
		public static Slice UnpackFirst(Slice buffer)
		{
			var slicer = new TupleReader(buffer);

			return TupleParser.ParseNext(ref slicer);
		}

		/// <summary>Only returns the last item of a packed tuple</summary>
		/// <param name="buffer">Slice that contains the packed representation of a tuple with one or more elements</param>
		/// <returns>Raw slice corresponding to the last element of the tuple</returns>
		public static Slice UnpackLast(Slice buffer)
		{
			var slicer = new TupleReader(buffer);

			Slice item = Slice.Nil;

			Slice current;
			while ((current = TupleParser.ParseNext(ref slicer)).HasValue)
			{
				item = current;
			}

			if (slicer.Input.HasMore) throw new FormatException("Parsing of tuple failed failed before reaching the end of the key");
			return item;
		}

		#endregion

	}

}
