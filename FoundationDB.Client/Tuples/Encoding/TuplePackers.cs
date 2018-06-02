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

namespace Doxense.Collections.Tuples.Encoding
{
	using System;
	using System.Collections.Concurrent;
	using System.Collections.Generic;
	using System.Globalization;
	using System.Linq;
	using System.Linq.Expressions;
	using System.Reflection;
	using System.Runtime.CompilerServices;
	using Doxense.Collections.Tuples;
	using Doxense.Diagnostics.Contracts;
	using Doxense.Runtime.Converters;
	using FoundationDB.Client;
	using JetBrains.Annotations;

	/// <summary>Helper methods used during serialization of values to the tuple binary format</summary>
	public static class TuplePackers
	{

		#region Serializers...

		public delegate void Encoder<in T>(ref TupleWriter writer, T value);

		/// <summary>Returns a lambda that will be able to serialize values of type <typeparamref name="T"/></summary>
		/// <typeparam name="T">Type of values to serialize</typeparam>
		/// <returns>Reusable action that knows how to serialize values of type <typeparamref name="T"/> into binary buffers, or that throws an exception if the type is not supported</returns>
		[CanBeNull, ContractAnnotation("true => notnull")]
		internal static Encoder<T> GetSerializer<T>(bool required)
		{
			//note: this method is only called once per initializing of TuplePackers<T> to create the cached delegate.

			var encoder = (Encoder<T>) GetSerializerFor(typeof(T));
			if (encoder == null && required)
			{
				encoder = delegate { throw new InvalidOperationException($"Does not know how to serialize values of type '{typeof(T).Name}' into keys"); };
			}
			return encoder;
		}

		[CanBeNull]
		private static Delegate GetSerializerFor([NotNull] Type type)
		{
			Contract.NotNull(type, nameof(type));

			if (type == typeof(object))
			{ // return a generic serializer that will inspect the runtime type of the object
				return new Encoder<object>(SerializeObjectTo);
			}

			// look for well-known types that have their own (non-generic) TuplePackers.SerializeTo(...) method
			var method = typeof(TuplePackers).GetMethod(nameof(SerializeTo), BindingFlags.Static | BindingFlags.Public, binder: null, types: new[] { typeof(TupleWriter).MakeByRefType(), type }, modifiers: null);
			if (method != null)
			{ // we have a direct serializer
				try
				{
					return method.CreateDelegate(typeof(Encoder<>).MakeGenericType(type));
				}
				catch (Exception e)
				{
					throw new InvalidOperationException($"Failed to compile fast tuple serializer {method.Name} for type '{type.Name}'.", e);
				}
			}

			// maybe it is a nullable type ?
			var nullableType = Nullable.GetUnderlyingType(type);
			if (nullableType != null)
			{ // nullable types can reuse the underlying type serializer
				method = typeof(TuplePackers).GetMethod(nameof(SerializeNullableTo), BindingFlags.Static | BindingFlags.Public);
				if (method != null)
				{
					try
					{
						return method.MakeGenericMethod(nullableType).CreateDelegate(typeof(Encoder<>).MakeGenericType(type));
					}
					catch (Exception e)
					{
						throw new InvalidOperationException($"Failed to compile fast tuple serializer {method.Name} for type 'Nullable<{nullableType.Name}>'.", e);
					}
				}
			}

			// maybe it is a tuple ?
			if (typeof(IVarTuple).IsAssignableFrom(type))
			{
				if (type == typeof(STuple) || (type.Name.StartsWith(nameof(STuple) + "`", StringComparison.Ordinal) && type.Namespace == typeof(STuple).Namespace))
				{ // well-known STuple<T...> struct
					var typeArgs = type.GetGenericArguments();
					method = FindSTupleSerializerMethod(typeArgs);
					if (method != null)
					{
						try
						{
							return method.MakeGenericMethod(typeArgs).CreateDelegate(typeof(Encoder<>).MakeGenericType(type));
						}
						catch (Exception e)
						{
							throw new InvalidOperationException($"Failed to compile fast tuple serializer {method.Name} for Tuple type '{type.Name}'.", e);
						}
					}
				}

				// will use the default ITuple implementation
				method = typeof(TuplePackers).GetMethod(nameof(SerializeTupleTo), BindingFlags.Static | BindingFlags.Public);
				if (method != null)
				{
					try
					{
						return method.MakeGenericMethod(type).CreateDelegate(typeof(Encoder<>).MakeGenericType(type));
					}
					catch (Exception e)
					{
						throw new InvalidOperationException($"Failed to compile fast tuple serializer {method.Name} for Tuple type '{type.Name}'.", e);
					}
				}
			}

			// ValueTuple<T..>
			if (type == typeof(ValueTuple) || (type.Name.StartsWith(nameof(System.ValueTuple) + "`", StringComparison.Ordinal) && type.Namespace == "System"))
			{
				var typeArgs = type.GetGenericArguments();
				method = FindValueTupleSerializerMethod(typeArgs);
				if (method != null)
				{
					try
					{
						return method.MakeGenericMethod(typeArgs).CreateDelegate(typeof(Encoder<>).MakeGenericType(type));
					}
					catch (Exception e)
					{
						throw new InvalidOperationException($"Failed to compile fast tuple serializer {method.Name} for Tuple type '{type.Name}'.", e);
					}
				}
			}

			// TODO: look for a static SerializeTo(ref TupleWriter, T) method on the type itself ?

			// no luck..
			return null;
		}

		private static MethodInfo FindSTupleSerializerMethod(Type[] args)
		{
			//note: we want to find the correct SerializeSTuple<...>(ref TupleWriter, (...,), but this cannot be done with Type.GetMethod(...) directly
			// => we have to scan for all methods with the correct name, and the same number of Type Arguments than the ValueTuple.
			return typeof(TuplePackers)
				   .GetMethods(BindingFlags.Static | BindingFlags.Public)
				   .SingleOrDefault(m => m.Name == nameof(SerializeSTupleTo) && m.GetGenericArguments().Length == args.Length);
		}

		private static MethodInfo FindValueTupleSerializerMethod(Type[] args)
		{
			//note: we want to find the correct SerializeValueTuple<...>(ref TupleWriter, (...,), but this cannot be done with Type.GetMethod(...) directly
			// => we have to scan for all methods with the correct name, and the same number of Type Arguments than the ValueTuple.
			return typeof(TuplePackers)
				.GetMethods(BindingFlags.Static | BindingFlags.Public)
				.SingleOrDefault(m => m.Name == nameof(SerializeValueTupleTo) && m.GetGenericArguments().Length == args.Length);
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		internal static void SerializeTo<T>(ref TupleWriter writer, T value)
		{
			//<JIT_HACK>
			// - In Release builds, this will be cleaned up and inlined by the JIT as a direct invokatino of the correct WriteXYZ method
			// - In Debug builds, we have to disabled this, because it would be too slow
			//IMPORTANT: only ValueTypes and they must have a corresponding Write$TYPE$(ref TupleWriter, $TYPE) in TupleParser!
#if !DEBUG
			if (typeof(T) == typeof(bool)) { TupleParser.WriteBool(ref writer, (bool) (object) value); return; }
			if (typeof(T) == typeof(int)) { TupleParser.WriteInt32(ref writer, (int) (object) value); return; }
			if (typeof(T) == typeof(long)) { TupleParser.WriteInt64(ref writer, (long) (object) value); return; }
			if (typeof(T) == typeof(uint)) { TupleParser.WriteUInt32(ref writer, (uint) (object) value); return; }
			if (typeof(T) == typeof(ulong)) { TupleParser.WriteUInt64(ref writer, (ulong) (object) value); return; }
			if (typeof(T) == typeof(short)) { TupleParser.WriteInt32(ref writer, (short) (object) value); return; }
			if (typeof(T) == typeof(ushort)) { TupleParser.WriteUInt32(ref writer, (ushort) (object) value); return; }
			if (typeof(T) == typeof(sbyte)) { TupleParser.WriteInt32(ref writer, (sbyte) (object) value); return; }
			if (typeof(T) == typeof(byte)) { TupleParser.WriteUInt32(ref writer, (byte) (object) value); return; }
			if (typeof(T) == typeof(float)) { TupleParser.WriteSingle(ref writer, (float) (object) value); return; }
			if (typeof(T) == typeof(double)) { TupleParser.WriteDouble(ref writer, (double) (object) value); return; }
			if (typeof(T) == typeof(decimal)) { TupleParser.WriteDecimal(ref writer, (decimal) (object) value); return; }
			if (typeof(T) == typeof(char)) { TupleParser.WriteChar(ref writer, (char) (object) value); return; }
			if (typeof(T) == typeof(Guid)) { TupleParser.WriteGuid(ref writer, (Guid) (object) value); return; }
			if (typeof(T) == typeof(Uuid128)) { TupleParser.WriteUuid128(ref writer, (Uuid128) (object) value); return; }
			if (typeof(T) == typeof(Uuid96)) { TupleParser.WriteUuid96(ref writer, (Uuid96) (object) value); return; }
			if (typeof(T) == typeof(Uuid80)) { TupleParser.WriteUuid80(ref writer, (Uuid80) (object) value); return; }
			if (typeof(T) == typeof(Uuid64)) { TupleParser.WriteUuid64(ref writer, (Uuid64) (object) value); return; }
			if (typeof(T) == typeof(VersionStamp)) { TupleParser.WriteVersionStamp(ref writer, (VersionStamp) (object) value); return; }
			if (typeof(T) == typeof(Slice)) { TupleParser.WriteBytes(ref writer, (Slice) (object) value); return; }

			if (typeof(T) == typeof(bool?)) { TupleParser.WriteBool(ref writer, (bool?) (object) value); return; }
			if (typeof(T) == typeof(int?)) { TupleParser.WriteInt32(ref writer, (int?) (object) value); return; }
			if (typeof(T) == typeof(long?)) { TupleParser.WriteInt64(ref writer, (long?) (object) value); return; }
			if (typeof(T) == typeof(uint?)) { TupleParser.WriteUInt32(ref writer, (uint?) (object) value); return; }
			if (typeof(T) == typeof(ulong?)) { TupleParser.WriteUInt64(ref writer, (ulong?) (object) value); return; }
			if (typeof(T) == typeof(short?)) { TupleParser.WriteInt32(ref writer, (short?) (object) value); return; }
			if (typeof(T) == typeof(ushort?)) { TupleParser.WriteUInt32(ref writer, (ushort?) (object) value); return; }
			if (typeof(T) == typeof(sbyte?)) { TupleParser.WriteInt32(ref writer, (sbyte?) (object) value); return; }
			if (typeof(T) == typeof(byte?)) { TupleParser.WriteUInt32(ref writer, (byte?) (object) value); return; }
			if (typeof(T) == typeof(float?)) { TupleParser.WriteSingle(ref writer, (float?) (object) value); return; }
			if (typeof(T) == typeof(double?)) { TupleParser.WriteDouble(ref writer, (double?) (object) value); return; }
			if (typeof(T) == typeof(decimal?)) { TupleParser.WriteDecimal(ref writer, (decimal?) (object) value); return; }
			if (typeof(T) == typeof(char?)) { TupleParser.WriteChar(ref writer, (char?) (object) value); return; }
			if (typeof(T) == typeof(Guid?)) { TupleParser.WriteGuid(ref writer, (Guid?) (object) value); return; }
			if (typeof(T) == typeof(Uuid128?)) { TupleParser.WriteUuid128(ref writer, (Uuid128?) (object) value); return; }
			if (typeof(T) == typeof(Uuid96?)) { TupleParser.WriteUuid96(ref writer, (Uuid96?) (object) value); return; }
			if (typeof(T) == typeof(Uuid80?)) { TupleParser.WriteUuid80(ref writer, (Uuid80?) (object) value); return; }
			if (typeof(T) == typeof(Uuid64?)) { TupleParser.WriteUuid64(ref writer, (Uuid64?) (object) value); return; }
			if (typeof(T) == typeof(VersionStamp?)) { TupleParser.WriteVersionStamp(ref writer, (VersionStamp?) (object) value); return; }
#endif
			//</JIT_HACK>

			// invoke the encoder directly
			TuplePacker<T>.Encoder(ref writer, value);
		}

		/// <summary>Serialize a nullable value, by checking for null at runtime</summary>
		/// <typeparam name="T">Underling type of the nullable type</typeparam>
		/// <param name="writer">Target buffer</param>
		/// <param name="value">Nullable value to serialize</param>
		/// <remarks>Uses the underlying type's serializer if the value is not null</remarks>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void SerializeNullableTo<T>(ref TupleWriter writer, T? value)
			where T : struct
		{
			if (value == null)
				TupleParser.WriteNil(ref writer);
			else
				SerializeTo(ref writer, value.Value);
		}

		/// <summary>Serialize an untyped object, by checking its type at runtime [VERY SLOW]</summary>
		/// <param name="writer">Target buffer</param>
		/// <param name="value">Untyped value whose type will be inspected at runtime</param>
		/// <remarks>
		/// May throw at runtime if the type is not supported.
		/// This method will be very slow! Please consider using typed tuples instead!
		/// </remarks>
		public static void SerializeObjectTo(ref TupleWriter writer, object value)
		{
			if (value == null)
			{ // null value
				// includes all null references to ref types, as nullables where HasValue == false
				TupleParser.WriteNil(ref writer);
				return;
			}
			GetBoxedEncoder(value.GetType())(ref writer, value);
		}

		private static Encoder<object> GetBoxedEncoder(Type type)
		{
			if (!BoxedEncoders.TryGetValue(type, out var encoder))
			{
				encoder = CreateBoxedEncoder(type);
				BoxedEncoders.TryAdd(type, encoder);
			}
			return encoder;
		}

		private static ConcurrentDictionary<Type, Encoder<object>> BoxedEncoders { get; } = GetDefaultBoxedEncoders();

		private static ConcurrentDictionary<Type, Encoder<object>> GetDefaultBoxedEncoders()
		{
			var encoders = new ConcurrentDictionary<Type, Encoder<object>>
			{
				[typeof(bool)] = (ref TupleWriter writer, object value) => SerializeTo(ref writer, (bool) value),
				[typeof(char)] = (ref TupleWriter writer, object value) => SerializeTo(ref writer, (char) value),
				[typeof(string)] = (ref TupleWriter writer, object value) => SerializeTo(ref writer, (string) value),
				[typeof(sbyte)] = (ref TupleWriter writer, object value) => SerializeTo(ref writer, (sbyte) value),
				[typeof(short)] = (ref TupleWriter writer, object value) => SerializeTo(ref writer, (short) value),
				[typeof(int)] = (ref TupleWriter writer, object value) => SerializeTo(ref writer, (int) value),
				[typeof(long)] = (ref TupleWriter writer, object value) => SerializeTo(ref writer, (long) value),
				[typeof(byte)] = (ref TupleWriter writer, object value) => SerializeTo(ref writer, (byte) value),
				[typeof(ushort)] = (ref TupleWriter writer, object value) => SerializeTo(ref writer, (ushort) value),
				[typeof(uint)] = (ref TupleWriter writer, object value) => SerializeTo(ref writer, (uint) value),
				[typeof(ulong)] = (ref TupleWriter writer, object value) => SerializeTo(ref writer, (ulong) value),
				[typeof(float)] = (ref TupleWriter writer, object value) => SerializeTo(ref writer, (float) value),
				[typeof(double)] = (ref TupleWriter writer, object value) => SerializeTo(ref writer, (double) value),
				[typeof(decimal)] = (ref TupleWriter writer, object value) => SerializeTo(ref writer, (decimal) value),
				[typeof(Slice)] = (ref TupleWriter writer, object value) => SerializeTo(ref writer, (Slice) value),
				[typeof(byte[])] = (ref TupleWriter writer, object value) => SerializeTo(ref writer, (byte[]) value),
				[typeof(Guid)] = (ref TupleWriter writer, object value) => SerializeTo(ref writer, (Guid) value),
				[typeof(Uuid128)] = (ref TupleWriter writer, object value) => SerializeTo(ref writer, (Uuid128) value),
				[typeof(Uuid96)] = (ref TupleWriter writer, object value) => SerializeTo(ref writer, (Uuid96) value),
				[typeof(Uuid80)] = (ref TupleWriter writer, object value) => SerializeTo(ref writer, (Uuid80) value),
				[typeof(Uuid64)] = (ref TupleWriter writer, object value) => SerializeTo(ref writer, (Uuid64) value),
				[typeof(VersionStamp)] = (ref TupleWriter writer, object value) => SerializeTo(ref writer, (VersionStamp) value),
				[typeof(TimeSpan)] = (ref TupleWriter writer, object value) => SerializeTo(ref writer, (TimeSpan) value),
				[typeof(DateTime)] = (ref TupleWriter writer, object value) => SerializeTo(ref writer, (DateTime) value),
				[typeof(DateTimeOffset)] = (ref TupleWriter writer, object value) => SerializeTo(ref writer, (DateTimeOffset) value),
				[typeof(IVarTuple)] = (ref TupleWriter writer, object value) => SerializeTupleTo(ref writer, (IVarTuple) value),
				//TODO: add System.Runtime.CompilerServices.ITuple for net471+
				[typeof(DBNull)] = (ref TupleWriter writer, object value) => TupleParser.WriteNil(ref writer)
			};

			return encoders;
		}

		private static Encoder<object> CreateBoxedEncoder(Type type)
		{
			var m = typeof(TuplePacker<>).MakeGenericType(type).GetMethod(nameof(TuplePacker<int>.SerializeBoxedTo));
			Contract.Assert(m != null);

			var writer = Expression.Parameter(typeof(TupleWriter).MakeByRefType(), "writer");
			var value = Expression.Parameter(typeof(object), "value");

			var body = Expression.Call(m, writer, value);
			return Expression.Lambda<Encoder<object>>(body, writer, value).Compile();
		}

		/// <summary>Writes a slice as a byte[] array</summary>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void SerializeTo(ref TupleWriter writer, Slice value)
		{
			TupleParser.WriteBytes(ref writer, value);
		}

		/// <summary>Writes a byte[] array</summary>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void SerializeTo(ref TupleWriter writer, byte[] value)
		{
			TupleParser.WriteBytes(ref writer, value);
		}

		/// <summary>Writes an array segment as a byte[] array</summary>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void SerializeTo(ref TupleWriter writer, ArraySegment<byte> value)
		{
			TupleParser.WriteBytes(ref writer, value);
		}

		/// <summary>Writes a char as Unicode string</summary>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void SerializeTo(ref TupleWriter writer, char value)
		{
			TupleParser.WriteChar(ref writer, value);
		}

		/// <summary>Writes a boolean as an integer</summary>
		/// <remarks>Uses 0 for false, and -1 for true</remarks>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void SerializeTo(ref TupleWriter writer, bool value)
		{
			TupleParser.WriteBool(ref writer, value);
		}

		/// <summary>Writes a boolean as an integer or null</summary>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void SerializeTo(ref TupleWriter writer, bool? value)
		{
			//REVIEW: only method for a nullable type? add others? or remove this one?
			TupleParser.WriteBool(ref writer, value);
		}

		/// <summary>Writes a signed byte as an integer</summary>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void SerializeTo(ref TupleWriter writer, sbyte value)
		{
			TupleParser.WriteInt32(ref writer, value);
		}

		/// <summary>Writes an unsigned byte as an integer</summary>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void SerializeTo(ref TupleWriter writer, byte value)
		{
			TupleParser.WriteByte(ref writer, value);
		}

		/// <summary>Writes a signed word as an integer</summary>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void SerializeTo(ref TupleWriter writer, short value)
		{
			TupleParser.WriteInt32(ref writer, value);
		}

		/// <summary>Writes an unsigned word as an integer</summary>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void SerializeTo(ref TupleWriter writer, ushort value)
		{
			TupleParser.WriteUInt32(ref writer, value);
		}

		/// <summary>Writes a signed int as an integer</summary>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void SerializeTo(ref TupleWriter writer, int value)
		{
			TupleParser.WriteInt32(ref writer, value);
		}

		/// <summary>Writes a signed int as an integer</summary>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void SerializeTo(ref TupleWriter writer, int? value)
		{
			TupleParser.WriteInt32(ref writer, value);
		}

		/// <summary>Writes an unsigned int as an integer</summary>
		public static void SerializeTo(ref TupleWriter writer, uint value)
		{
			TupleParser.WriteUInt32(ref writer, value);
		}

		/// <summary>Writes an unsigned int as an integer</summary>
		public static void SerializeTo(ref TupleWriter writer, uint? value)
		{
			TupleParser.WriteUInt32(ref writer, value);
		}

		/// <summary>Writes a signed long as an integer</summary>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void SerializeTo(ref TupleWriter writer, long value)
		{
			TupleParser.WriteInt64(ref writer, value);
		}

		/// <summary>Writes a signed long as an integer</summary>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void SerializeTo(ref TupleWriter writer, long? value)
		{
			TupleParser.WriteInt64(ref writer, value);
		}

		/// <summary>Writes an unsigned long as an integer</summary>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void SerializeTo(ref TupleWriter writer, ulong value)
		{
			TupleParser.WriteUInt64(ref writer, value);
		}

		/// <summary>Writes an unsigned long as an integer</summary>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void SerializeTo(ref TupleWriter writer, ulong? value)
		{
			TupleParser.WriteUInt64(ref writer, value);
		}

		/// <summary>Writes a 32-bit IEEE floating point number</summary>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void SerializeTo(ref TupleWriter writer, float value)
		{
			TupleParser.WriteSingle(ref writer, value);
		}

		/// <summary>Writes a 32-bit IEEE floating point number</summary>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void SerializeTo(ref TupleWriter writer, float? value)
		{
			TupleParser.WriteSingle(ref writer, value);
		}

		/// <summary>Writes a 64-bit IEEE floating point number</summary>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void SerializeTo(ref TupleWriter writer, double value)
		{
			TupleParser.WriteDouble(ref writer, value);
		}

		/// <summary>Writes a 64-bit IEEE floating point number</summary>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void SerializeTo(ref TupleWriter writer, double? value)
		{
			TupleParser.WriteDouble(ref writer, value);
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void SerializeTo(ref TupleWriter writer, decimal value)
		{
			TupleParser.WriteDecimal(ref writer, value);
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void SerializeTo(ref TupleWriter writer, decimal? value)
		{
			TupleParser.WriteDecimal(ref writer, value);
		}

		/// <summary>Writes a string as an Unicode string</summary>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void SerializeTo(ref TupleWriter writer, string value)
		{
			TupleParser.WriteString(ref writer, value);
		}

		/// <summary>Writes a DateTime converted to the number of days since the Unix Epoch and stored as a 64-bit decimal</summary>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
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
			TupleParser.WriteDouble(ref writer, (value.ToUniversalTime().Ticks - UNIX_EPOCH_EPOCH) / (double)TimeSpan.TicksPerDay);
		}

		/// <summary>Writes a TimeSpan converted to to a number seconds encoded as a 64-bit decimal</summary>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
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
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void SerializeTo(ref TupleWriter writer, Guid value)
		{
			//REVIEW: should we consider serializing Guid.Empty as <14> (integer 0) ? or maybe <01><00> (empty bytestring) ?
			// => could spare 17 bytes per key in indexes on GUID properties that are frequently missing or empty (== default(Guid))
			TupleParser.WriteGuid(ref writer, in value);
		}

		/// <summary>Writes a Guid as a 128-bit UUID</summary>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void SerializeTo(ref TupleWriter writer, Guid? value)
		{
			TupleParser.WriteGuid(ref writer, value);
		}

		/// <summary>Writes a Uuid as a 128-bit UUID</summary>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void SerializeTo(ref TupleWriter writer, Uuid128 value)
		{
			TupleParser.WriteUuid128(ref writer, in value);
		}

		/// <summary>Writes a Uuid as a 128-bit UUID</summary>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void SerializeTo(ref TupleWriter writer, Uuid128? value)
		{
			TupleParser.WriteUuid128(ref writer, value);
		}

		/// <summary>Writes a Uuid as a 96-bit UUID</summary>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void SerializeTo(ref TupleWriter writer, Uuid96 value)
		{
			TupleParser.WriteUuid96(ref writer, in value);
		}

		/// <summary>Writes a Uuid as a 96-bit UUID</summary>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void SerializeTo(ref TupleWriter writer, Uuid96? value)
		{
			TupleParser.WriteUuid96(ref writer, value);
		}

		/// <summary>Writes a Uuid as a 80-bit UUID</summary>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void SerializeTo(ref TupleWriter writer, Uuid80 value)
		{
			TupleParser.WriteUuid80(ref writer, in value);
		}

		/// <summary>Writes a Uuid as a 80-bit UUID</summary>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void SerializeTo(ref TupleWriter writer, Uuid80? value)
		{
			TupleParser.WriteUuid80(ref writer, value);
		}

		/// <summary>Writes a Uuid as a 64-bit UUID</summary>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void SerializeTo(ref TupleWriter writer, Uuid64 value)
		{
			TupleParser.WriteUuid64(ref writer, value);
		}

		/// <summary>Writes a Uuid as a 64-bit UUID</summary>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void SerializeTo(ref TupleWriter writer, Uuid64? value)
		{
			TupleParser.WriteUuid64(ref writer, value);
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void SerializeTo(ref TupleWriter writer, VersionStamp value)
		{
			TupleParser.WriteVersionStamp(ref writer, in value);
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void SerializeTo(ref TupleWriter writer, VersionStamp? value)
		{
			TupleParser.WriteVersionStamp(ref writer, value);
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void SerializeTo(ref TupleWriter writer, TuPackUserType value)
		{
			TupleParser.WriteUserType(ref writer, value);
		}

		/// <summary>Writes an IPaddress as a 32-bit (IPv4) or 128-bit (IPv6) byte array</summary>
		public static void SerializeTo(ref TupleWriter writer, System.Net.IPAddress value)
		{
			TupleParser.WriteBytes(ref writer, value?.GetAddressBytes());
		}

		/// <summary>Serialize an embedded tuples</summary>
		public static void SerializeTupleTo<TTuple>(ref TupleWriter writer, TTuple tuple)
			where TTuple : IVarTuple
		{
			Contract.Requires(tuple != null);

			TupleParser.BeginTuple(ref writer);
			TupleEncoder.WriteTo(ref writer, tuple);
			TupleParser.EndTuple(ref writer);
		}

		public static void SerializeSTupleTo<T1>(ref TupleWriter writer, STuple<T1> tuple)
		{
			TupleParser.BeginTuple(ref writer);
			SerializeTo(ref writer, tuple.Item1);
			TupleParser.EndTuple(ref writer);
		}

		public static void SerializeSTupleTo<T1, T2>(ref TupleWriter writer, STuple<T1, T2> tuple)
		{
			TupleParser.BeginTuple(ref writer);
			SerializeTo(ref writer, tuple.Item1);
			SerializeTo(ref writer, tuple.Item2);
			TupleParser.EndTuple(ref writer);
		}

		public static void SerializeSTupleTo<T1, T2, T3>(ref TupleWriter writer, STuple<T1, T2, T3> tuple)
		{
			TupleParser.BeginTuple(ref writer);
			SerializeTo(ref writer, tuple.Item1);
			SerializeTo(ref writer, tuple.Item2);
			SerializeTo(ref writer, tuple.Item3);
			TupleParser.EndTuple(ref writer);
		}

		public static void SerializeSTupleTo<T1, T2, T3, T4>(ref TupleWriter writer, STuple<T1, T2, T3, T4> tuple)
		{
			TupleParser.BeginTuple(ref writer);
			SerializeTo(ref writer, tuple.Item1);
			SerializeTo(ref writer, tuple.Item2);
			SerializeTo(ref writer, tuple.Item3);
			SerializeTo(ref writer, tuple.Item4);
			TupleParser.EndTuple(ref writer);
		}

		public static void SerializeSTupleTo<T1, T2, T3, T4, T5>(ref TupleWriter writer, STuple<T1, T2, T3, T4, T5> tuple)
		{
			TupleParser.BeginTuple(ref writer);
			SerializeTo(ref writer, tuple.Item1);
			SerializeTo(ref writer, tuple.Item2);
			SerializeTo(ref writer, tuple.Item3);
			SerializeTo(ref writer, tuple.Item4);
			SerializeTo(ref writer, tuple.Item5);
			TupleParser.EndTuple(ref writer);
		}

		public static void SerializeSTupleTo<T1, T2, T3, T4, T5, T6>(ref TupleWriter writer, STuple<T1, T2, T3, T4, T5, T6> tuple)
		{
			TupleParser.BeginTuple(ref writer);
			SerializeTo(ref writer, tuple.Item1);
			SerializeTo(ref writer, tuple.Item2);
			SerializeTo(ref writer, tuple.Item3);
			SerializeTo(ref writer, tuple.Item4);
			SerializeTo(ref writer, tuple.Item5);
			SerializeTo(ref writer, tuple.Item6);
			TupleParser.EndTuple(ref writer);
		}

		public static void SerializeValueTupleTo<T1>(ref TupleWriter writer, ValueTuple<T1> tuple)
		{
			TupleParser.BeginTuple(ref writer);
			SerializeTo(ref writer, tuple.Item1);
			TupleParser.EndTuple(ref writer);
		}

		public static void SerializeValueTupleTo<T1, T2>(ref TupleWriter writer, (T1, T2) tuple)
		{
			TupleParser.BeginTuple(ref writer);
			SerializeTo(ref writer, tuple.Item1);
			SerializeTo(ref writer, tuple.Item2);
			TupleParser.EndTuple(ref writer);
		}

		public static void SerializeValueTupleTo<T1, T2, T3>(ref TupleWriter writer, (T1, T2, T3) tuple)
		{
			TupleParser.BeginTuple(ref writer);
			SerializeTo(ref writer, tuple.Item1);
			SerializeTo(ref writer, tuple.Item2);
			SerializeTo(ref writer, tuple.Item3);
			TupleParser.EndTuple(ref writer);
		}

		public static void SerializeValueTupleTo<T1, T2, T3, T4>(ref TupleWriter writer, (T1, T2, T3, T4) tuple)
		{
			TupleParser.BeginTuple(ref writer);
			SerializeTo(ref writer, tuple.Item1);
			SerializeTo(ref writer, tuple.Item2);
			SerializeTo(ref writer, tuple.Item3);
			SerializeTo(ref writer, tuple.Item4);
			TupleParser.EndTuple(ref writer);
		}

		public static void SerializeValueTupleTo<T1, T2, T3, T4, T5>(ref TupleWriter writer, (T1, T2, T3, T4, T5) tuple)
		{
			TupleParser.BeginTuple(ref writer);
			SerializeTo(ref writer, tuple.Item1);
			SerializeTo(ref writer, tuple.Item2);
			SerializeTo(ref writer, tuple.Item3);
			SerializeTo(ref writer, tuple.Item4);
			SerializeTo(ref writer, tuple.Item5);
			TupleParser.EndTuple(ref writer);
		}

		public static void SerializeValueTupleTo<T1, T2, T3, T4, T5, T6>(ref TupleWriter writer, (T1, T2, T3, T4, T5, T6) tuple)
		{
			TupleParser.BeginTuple(ref writer);
			SerializeTo(ref writer, tuple.Item1);
			SerializeTo(ref writer, tuple.Item2);
			SerializeTo(ref writer, tuple.Item3);
			SerializeTo(ref writer, tuple.Item4);
			SerializeTo(ref writer, tuple.Item5);
			SerializeTo(ref writer, tuple.Item6);
			TupleParser.EndTuple(ref writer);
		}

		#endregion

		#region Deserializers...

		private static readonly Dictionary<Type, Delegate> WellKnownUnpackers = InitializeDefaultUnpackers();

		[NotNull]
		private static Dictionary<Type, Delegate> InitializeDefaultUnpackers()
		{
			var map = new Dictionary<Type, Delegate>
			{
				[typeof(Slice)] = new Func<Slice, Slice>(TuplePackers.DeserializeSlice),
				[typeof(byte[])] = new Func<Slice, byte[]>(TuplePackers.DeserializeBytes),
				[typeof(bool)] = new Func<Slice, bool>(TuplePackers.DeserializeBoolean),
				[typeof(string)] = new Func<Slice, string>(TuplePackers.DeserializeString),
				[typeof(char)] = new Func<Slice, char>(TuplePackers.DeserializeChar),
				[typeof(sbyte)] = new Func<Slice, sbyte>(TuplePackers.DeserializeSByte),
				[typeof(short)] = new Func<Slice, short>(TuplePackers.DeserializeInt16),
				[typeof(int)] = new Func<Slice, int>(TuplePackers.DeserializeInt32),
				[typeof(long)] = new Func<Slice, long>(TuplePackers.DeserializeInt64),
				[typeof(byte)] = new Func<Slice, byte>(TuplePackers.DeserializeByte),
				[typeof(ushort)] = new Func<Slice, ushort>(TuplePackers.DeserializeUInt16),
				[typeof(uint)] = new Func<Slice, uint>(TuplePackers.DeserializeUInt32),
				[typeof(ulong)] = new Func<Slice, ulong>(TuplePackers.DeserializeUInt64),
				[typeof(float)] = new Func<Slice, float>(TuplePackers.DeserializeSingle),
				[typeof(double)] = new Func<Slice, double>(TuplePackers.DeserializeDouble),
				[typeof(decimal)] = new Func<Slice, decimal>(TuplePackers.DeserializeDecimal),
				[typeof(Guid)] = new Func<Slice, Guid>(TuplePackers.DeserializeGuid),
				[typeof(Uuid128)] = new Func<Slice, Uuid128>(TuplePackers.DeserializeUuid128),
				[typeof(Uuid96)] = new Func<Slice, Uuid96>(TuplePackers.DeserializeUuid96),
				[typeof(Uuid80)] = new Func<Slice, Uuid80>(TuplePackers.DeserializeUuid80),
				[typeof(Uuid64)] = new Func<Slice, Uuid64>(TuplePackers.DeserializeUuid64),
				[typeof(TimeSpan)] = new Func<Slice, TimeSpan>(TuplePackers.DeserializeTimeSpan),
				[typeof(DateTime)] = new Func<Slice, DateTime>(TuplePackers.DeserializeDateTime),
				[typeof(System.Net.IPAddress)] = new Func<Slice, System.Net.IPAddress>(TuplePackers.DeserializeIpAddress),
				[typeof(VersionStamp)] = new Func<Slice, VersionStamp>(TuplePackers.DeserializeVersionStamp),
				[typeof(IVarTuple)] = new Func<Slice, IVarTuple>(TuplePackers.DeserializeTuple),
				[typeof(TuPackUserType)] = new Func<Slice, TuPackUserType>(TuplePackers.DeserializeUserType)
			};

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

			if (WellKnownUnpackers.TryGetValue(type, out var decoder))
			{ // We already know how to decode this type
				return (Func<Slice, T>) decoder;
			}

			// Nullable<T>
			var underlyingType = Nullable.GetUnderlyingType(typeof(T));
			if (underlyingType != null && WellKnownUnpackers.TryGetValue(underlyingType, out decoder))
			{ 
				return (Func<Slice, T>) MakeNullableDeserializer(type, underlyingType, decoder);
			}

			// STuple<...>
			if (typeof(IVarTuple).IsAssignableFrom(type))
			{
				if (type.IsValueType && type.IsGenericType && type.Name.StartsWith(nameof(STuple) + "`", StringComparison.Ordinal))
				return (Func<Slice, T>) MakeSTupleDeserializer(type);
			}

			if ((type.Name == nameof(ValueTuple) || type.Name.StartsWith(nameof(ValueTuple) + "`", StringComparison.Ordinal)) && type.Namespace == "System")
			{
				return (Func<Slice, T>) MakeValueTupleDeserializer(type);
			}

			if (required)
			{ // will throw at runtime
				return MakeNotSupportedDeserializer<T>();
			}
			// when all else fails...
			return MakeConvertBoxedDeserializer<T>();
		}

		[Pure, NotNull]
		private static Func<Slice, T> MakeNotSupportedDeserializer<T>()
		{
			return (_) => throw new InvalidOperationException($"Does not know how to deserialize keys into values of type {typeof(T).Name}");
		}

		[Pure, NotNull]
		private static Func<Slice, T> MakeConvertBoxedDeserializer<T>()
		{
			return (value) => TypeConverters.ConvertBoxed<T>(DeserializeBoxed(value));
		}

		/// <summary>Check if a tuple segment is the equivalent of 'Nil'</summary>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		internal static bool IsNilSegment(Slice slice)
		{
			return slice.IsNullOrEmpty || slice[0] == TupleTypes.Nil;
		}

		[Pure, NotNull]
		private static Delegate MakeNullableDeserializer([NotNull] Type nullableType, [NotNull] Type type, [NotNull] Delegate decoder)
		{
			Contract.Requires(nullableType != null && type != null && decoder != null);
			// We have a Decoder of T, but we have to transform it into a Decoder for Nullable<T>, which returns null if the slice is "nil", or falls back to the underlying decoder if the slice contains something

			var prmSlice = Expression.Parameter(typeof(Slice), "slice");
			var body = Expression.Condition(
				// IsNilSegment(slice) ?
				Expression.Call(typeof(TuplePackers).GetMethod(nameof(IsNilSegment), BindingFlags.Static | BindingFlags.NonPublic), prmSlice),
				// True => default(Nullable<T>)
				Expression.Default(nullableType),
				// False => decoder(slice)
				Expression.Convert(Expression.Invoke(Expression.Constant(decoder), prmSlice), nullableType)
			);

			return Expression.Lambda(body, prmSlice).Compile();
		}

		[Pure, NotNull]
		private static Delegate MakeSTupleDeserializer(Type type)
		{
			Contract.Requires(type != null);

			// (slice) => TuPack.DeserializeTuple<T...>(slice)

			var targs = type.GetGenericArguments();
			var method = typeof(TuplePackers)
				.GetMethods()
				.Single(m =>
				{ // find the matching "DeserializeTuple<Ts..>(Slice)" method that we want to call
					if (m.Name != nameof(DeserializeTuple)) return false;
					if (!m.IsGenericMethod || m.GetGenericArguments().Length != targs.Length) return false;
					var args = m.GetParameters();
					if (args.Length != 1 && args[0].ParameterType != typeof(Slice)) return false;
					return true;
				})
				.MakeGenericMethod(targs);

			var prmSlice = Expression.Parameter(typeof(Slice), "slice");
			var body = Expression.Call(method, prmSlice);

			return Expression.Lambda(body, prmSlice).Compile();
		}

		[Pure, NotNull]
		private static Delegate MakeValueTupleDeserializer(Type type)
		{
			Contract.Requires(type != null);

			// (slice) => TuPack.DeserializeValueTuple<T...>(slice)

			var targs = type.GetGenericArguments();
			var method = typeof(TuplePackers)
				.GetMethods()
				.Single(m =>
				{ // find the matching "DeserializeValueTuple<Ts..>(Slice)" method that we want to call
					if (m.Name != nameof(DeserializeValueTuple)) return false;
					if (!m.IsGenericMethod || m.GetGenericArguments().Length != targs.Length) return false;
					var args = m.GetParameters();
					if (args.Length != 1 && args[0].ParameterType != typeof(Slice)) return false;
					return true;
				})
				.MakeGenericMethod(targs);

			var prmSlice = Expression.Parameter(typeof(Slice), "slice");
			var body = Expression.Call(method, prmSlice);

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
					case TupleTypes.LegacyTupleStart: throw TupleParser.FailLegacyTupleNotSupported();
					case TupleTypes.EmbeddedTuple: return TupleParser.ParseTuple(slice);
				}
			}
			else
			{
				switch (type)
				{
					case TupleTypes.Single: return TupleParser.ParseSingle(slice);
					case TupleTypes.Double: return TupleParser.ParseDouble(slice);
					//TODO: Triple
					case TupleTypes.Decimal: return TupleParser.ParseDecimal(slice);
					case TupleTypes.False: return false;
					case TupleTypes.True: return true;
					case TupleTypes.Uuid128: return TupleParser.ParseGuid(slice);
					case TupleTypes.Uuid64: return TupleParser.ParseUuid64(slice);
					case TupleTypes.VersionStamp80: return TupleParser.ParseVersionStamp(slice);
					case TupleTypes.VersionStamp96: return TupleParser.ParseVersionStamp(slice);

					case TupleTypes.Directory:
					{
						if (slice.Count == 1) return TuPackUserType.Directory;
						break;
					}
					case TupleTypes.Escape:
					{
						if (slice.Count == 1) return TuPackUserType.System;
						break;
					}
				}
			}

			throw new FormatException($"Cannot convert tuple segment with unknown type code 0x{type:X}");
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
				//TODO: triple
				case TupleTypes.Decimal: return Slice.FromDecimal(TupleParser.ParseDecimal(slice));

				case TupleTypes.Uuid128: return Slice.FromGuid(TupleParser.ParseGuid(slice));
				case TupleTypes.Uuid64: return Slice.FromUuid64(TupleParser.ParseUuid64(slice));
			}

			if (type <= TupleTypes.IntPos8 && type >= TupleTypes.IntNeg8)
			{
				if (type >= TupleTypes.IntZero) return Slice.FromInt64(DeserializeInt64(slice));
				return Slice.FromUInt64(DeserializeUInt64(slice));
			}

			throw new FormatException($"Cannot convert tuple segment of type 0x{type:X} into a Slice");
		}

		/// <summary>Deserialize a tuple segment into a byte array</summary>
		[CanBeNull, MethodImpl(MethodImplOptions.AggressiveInlining)] //REVIEW: because of Slice.GetBytes()
		public static byte[] DeserializeBytes(Slice slice)
		{
			return DeserializeSlice(slice).GetBytes();
		}

		public static TuPackUserType DeserializeUserType(Slice slice)
		{
			if (slice.IsNullOrEmpty) return null; //TODO: fail ?

			int type = slice[0];
			if (slice.Count == 1)
			{
				switch (type)
				{
					case 0xFE: return TuPackUserType.Directory;
					case 0xFF: return TuPackUserType.System;
				}
				return new TuPackUserType(type);
			}

			return new TuPackUserType(type, slice.Substring(1));
		}

		/// <summary>Deserialize a tuple segment into a tuple</summary>
		[CanBeNull]
		public static IVarTuple DeserializeTuple(Slice slice)
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
					return TupleEncoder.Unpack(TupleParser.ParseBytes(slice));
				}
				case TupleTypes.LegacyTupleStart: throw TupleParser.FailLegacyTupleNotSupported();
				case TupleTypes.EmbeddedTuple:
				{
					return TupleParser.ParseTuple(slice);
				}
				default:
				{
					throw new FormatException("Cannot convert tuple segment into a Tuple");
				}
			}
		}

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static STuple<T1> DeserializeTuple<T1>(Slice slice)
		{
			return DeserializeValueTuple<T1>(slice);
		}

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static STuple<T1, T2> DeserializeTuple<T1, T2>(Slice slice)
		{
			return DeserializeValueTuple<T1, T2>(slice);
		}

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static STuple<T1, T2, T3> DeserializeTuple<T1, T2, T3>(Slice slice)
		{
			return DeserializeValueTuple<T1, T2, T3>(slice);
		}

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static STuple<T1, T2, T3, T4> DeserializeTuple<T1, T2, T3, T4>(Slice slice)
		{
			return DeserializeValueTuple<T1, T2, T3, T4>(slice);
		}

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static STuple<T1, T2, T3, T4, T5> DeserializeTuple<T1, T2, T3, T4, T5>(Slice slice)
		{
			return DeserializeValueTuple<T1, T2, T3, T4, T5>(slice);
		}

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static STuple<T1, T2, T3, T4, T5, T6> DeserializeTuple<T1, T2, T3, T4, T5, T6>(Slice slice)
		{
			return DeserializeValueTuple<T1, T2, T3, T4, T5, T6>(slice);
		}

		[Pure]
		public static ValueTuple<T1> DeserializeValueTuple<T1>(Slice slice)
		{
			ValueTuple<T1> res = default;
			if (slice.IsPresent)
			{
				byte type = slice[0];
				switch (type)
				{
					case TupleTypes.Nil:
					{
						break;
					}
					case TupleTypes.Bytes:
					{
						TupleEncoder.DecodeKey(TupleParser.ParseBytes(slice), out res);
						break;
					}
					case TupleTypes.LegacyTupleStart: throw TupleParser.FailLegacyTupleNotSupported();
					case TupleTypes.EmbeddedTuple:
					{
						var reader = TupleReader.Embedded(slice);
						TupleEncoder.DecodeKey(ref reader, out res);
						break;
					}
					default:
					{
						throw new FormatException($"Cannot convert tuple segment into a {res.GetType().Name}");
					}
				}
			}
			return res;
		}

		[Pure]
		public static ValueTuple<T1, T2> DeserializeValueTuple<T1, T2>(Slice slice)
		{
			var res = default(ValueTuple<T1, T2>);
			if (slice.IsPresent)
			{
				byte type = slice[0];
				switch (type)
				{
					case TupleTypes.Nil:
					{
						break;
					}
					case TupleTypes.Bytes:
					{
						TupleEncoder.DecodeKey(TupleParser.ParseBytes(slice), out res);
						break;
					}
					case TupleTypes.LegacyTupleStart: throw TupleParser.FailLegacyTupleNotSupported();
					case TupleTypes.EmbeddedTuple:
					{
						var reader = TupleReader.Embedded(slice);
						TupleEncoder.DecodeKey(ref reader, out res);
						break;
					}
					default:
					{
						throw new FormatException($"Cannot convert tuple segment into a {res.GetType().Name}");
					}
				}
			}
			return res;
		}

		[Pure]
		public static ValueTuple<T1, T2, T3> DeserializeValueTuple<T1, T2, T3>(Slice slice)
		{
			var res = default(ValueTuple<T1, T2, T3>);
			if (slice.IsPresent)
			{
				byte type = slice[0];
				switch (type)
				{
					case TupleTypes.Nil:
					{
						break;
					}
					case TupleTypes.Bytes:
					{
						TupleEncoder.DecodeKey(TupleParser.ParseBytes(slice), out res);
						break;
					}
					case TupleTypes.LegacyTupleStart: throw TupleParser.FailLegacyTupleNotSupported();
					case TupleTypes.EmbeddedTuple:
					{
						var reader = TupleReader.Embedded(slice);
						TupleEncoder.DecodeKey(ref reader, out res);
						break;
					}
					default:
					{
						throw new FormatException($"Cannot convert tuple segment into a {res.GetType().Name}");
					}
				}
			}
			return res;

		}

		[Pure]
		public static ValueTuple<T1, T2, T3, T4> DeserializeValueTuple<T1, T2, T3, T4>(Slice slice)
		{
			var res = default(ValueTuple<T1, T2, T3, T4>);
			if (slice.IsPresent)
			{
				byte type = slice[0];
				switch (type)
				{
					case TupleTypes.Nil:
					{
						break;
					}
					case TupleTypes.Bytes:
					{
						TupleEncoder.DecodeKey(TupleParser.ParseBytes(slice), out res);
						break;
					}
					case TupleTypes.LegacyTupleStart: throw TupleParser.FailLegacyTupleNotSupported();
					case TupleTypes.EmbeddedTuple:
					{
						var reader = TupleReader.Embedded(slice);
						TupleEncoder.DecodeKey(ref reader, out res);
						break;
					}
					default:
					{
						throw new FormatException($"Cannot convert tuple segment into a {res.GetType().Name}");
					}
				}
			}
			return res;

		}

		[Pure]
		public static ValueTuple<T1, T2, T3, T4, T5> DeserializeValueTuple<T1, T2, T3, T4, T5>(Slice slice)
		{
			var res = default(ValueTuple<T1, T2, T3, T4, T5>);
			if (slice.IsPresent)
			{
				byte type = slice[0];
				switch (type)
				{
					case TupleTypes.Nil:
					{
						break;
					}
					case TupleTypes.Bytes:
					{
						TupleEncoder.DecodeKey(TupleParser.ParseBytes(slice), out res);
						break;
					}
					case TupleTypes.LegacyTupleStart: throw TupleParser.FailLegacyTupleNotSupported();
					case TupleTypes.EmbeddedTuple:
					{
						var reader = TupleReader.Embedded(slice);
						TupleEncoder.DecodeKey(ref reader, out res);
						break;
					}
					default:
					{
						throw new FormatException($"Cannot convert tuple segment into a {res.GetType().Name}");
					}
				}
			}
			return res;
		}

		[Pure]
		public static ValueTuple<T1, T2, T3, T4, T5, T6> DeserializeValueTuple<T1, T2, T3, T4, T5, T6>(Slice slice)
		{
			var res = default(ValueTuple<T1, T2, T3, T4, T5, T6>);
			if (slice.IsPresent)
			{
				byte type = slice[0];
				switch (type)
				{
					case TupleTypes.Nil:
					{
						break;
					}
					case TupleTypes.Bytes:
					{
						TupleEncoder.DecodeKey(TupleParser.ParseBytes(slice), out res);
						break;
					}
					case TupleTypes.LegacyTupleStart: throw TupleParser.FailLegacyTupleNotSupported();
					case TupleTypes.EmbeddedTuple:
					{
						var reader = TupleReader.Embedded(slice);
						TupleEncoder.DecodeKey(ref reader, out res);
						break;
					}
					default:
					{
						throw new FormatException($"Cannot convert tuple segment into a {res.GetType().Name}");
					}
				}
			}
			return res;
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
					//=> it is the "null" of the floats, so if we do, 'null' should also be considered false
					// ReSharper disable once CompareOfFloatsByEqualityOperator
					return 0f != TupleParser.ParseSingle(slice);
				}
				case TupleTypes.Double:
				{
					//TODO: should NaN considered to be false ?
					//=> it is the "null" of the floats, so if we do, 'null' should also be considered false
					// ReSharper disable once CompareOfFloatsByEqualityOperator
					return 0d != TupleParser.ParseDouble(slice);
				}
				//TODO: triple
				case TupleTypes.Decimal:
				{
					return 0m != TupleParser.ParseDecimal(slice);
				}
				case TupleTypes.False:
				{
					return false;
				}
				case TupleTypes.True:
				{
					return true;
				}
			}

			//TODO: should we handle weird cases like strings "True" and "False"?

			throw new FormatException($"Cannot convert tuple segment of type 0x{type:X} into a boolean");
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

			throw new FormatException($"Cannot convert tuple segment of type 0x{type:X} into a signed integer");
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

			throw new FormatException($"Cannot convert tuple segment of type 0x{type:X} into an unsigned integer");
		}

		public static float DeserializeSingle(Slice slice)
		{
			if (slice.IsNullOrEmpty) return 0;

			byte type = slice[0];
			switch (type)
			{
				case TupleTypes.Nil:
				{
					//REVIEW: or should we retourne NaN?
					return 0;
				}
				case TupleTypes.Utf8:
				{
					return float.Parse(TupleParser.ParseUnicode(slice), CultureInfo.InvariantCulture);
				}
				case TupleTypes.Single:
				{
					return TupleParser.ParseSingle(slice);
				}
				case TupleTypes.Double:
				{
					return (float) TupleParser.ParseDouble(slice);
				}
				case TupleTypes.Decimal:
				{
					return (float) TupleParser.ParseDecimal(slice);
				}
			}

			if (type <= TupleTypes.IntPos8 && type >= TupleTypes.IntNeg8)
			{
				return DeserializeInt64(slice);
			}

			throw new FormatException($"Cannot convert tuple segment of type 0x{type:X} into a Single");
		}

		public static double DeserializeDouble(Slice slice)
		{
			if (slice.IsNullOrEmpty) return 0;

			byte type = slice[0];
			switch(type)
			{
				case TupleTypes.Nil:
				{
					//REVIEW: or should we retourne NaN?
					return 0;
				}
				case TupleTypes.Utf8:
				{
					return double.Parse(TupleParser.ParseUnicode(slice), CultureInfo.InvariantCulture);
				}
				case TupleTypes.Single:
				{
					return TupleParser.ParseSingle(slice);
				}
				case TupleTypes.Double:
				{
					return TupleParser.ParseDouble(slice);
				}
				case TupleTypes.Decimal:
				{
					return (double) TupleParser.ParseDecimal(slice);
				}
			}

			if (type <= TupleTypes.IntPos8 && type >= TupleTypes.IntNeg8)
			{
				return DeserializeInt64(slice);
			}

			throw new FormatException($"Cannot convert tuple segment of type 0x{type:X} into a Double");
		}

		public static decimal DeserializeDecimal(Slice slice)
		{
			throw new NotImplementedException();
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

				case TupleTypes.Decimal:
				{
					const long UNIX_EPOCH_TICKS = 621355968000000000L;
					//note: we can't user TimeSpan.FromDays(...) because it rounds to the nearest millisecond!
					long ticks = UNIX_EPOCH_TICKS + (long)(TupleParser.ParseDecimal(slice) * TimeSpan.TicksPerDay);
					return new DateTime(ticks, DateTimeKind.Utc);
				}
			}

			// If we have an integer, we consider it to be a number of Ticks (Windows Only)
			if (type <= TupleTypes.IntPos8 && type >= TupleTypes.IntNeg8)
			{
				return new DateTime(DeserializeInt64(slice), DateTimeKind.Utc);
			}

			throw new FormatException($"Cannot convert tuple segment of type 0x{type:X} into a DateTime");
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
				case TupleTypes.Single:
				{ // Number of seconds
					//note: We can't use TimeSpan.FromSeconds(...) because it rounds to the nearest millisecond!
					return new TimeSpan((long) (TupleParser.ParseSingle(slice) * TimeSpan.TicksPerSecond));
				}
				case TupleTypes.Double:
				{ // Number of seconds
					//note: We can't use TimeSpan.FromSeconds(...) because it rounds to the nearest millisecond!
					return new TimeSpan((long) (TupleParser.ParseDouble(slice) * TimeSpan.TicksPerSecond));
				}
				case TupleTypes.Decimal:
				{ // Number of seconds
					//note: We can't use TimeSpan.FromSeconds(...) because it rounds to the nearest millisecond!
					return new TimeSpan((long) (TupleParser.ParseDecimal(slice) * TimeSpan.TicksPerSecond));
				}
			}

			// If we have an integer, we consider it to be a number of Ticks (Windows Only)
			if (type <= TupleTypes.IntPos8 && type >= TupleTypes.IntNeg8)
			{
				return new TimeSpan(DeserializeInt64(slice));
			}

			throw new FormatException($"Cannot convert tuple segment of type 0x{type:X} into a TimeSpan");
		}

		/// <summary>Deserialize a tuple segment into a Unicode character</summary>
		/// <param name="slice">Slice that contains a single packed element</param>
		public static char DeserializeChar(Slice slice)
		{
			if (slice.IsNullOrEmpty) return '\0';

			byte type = slice[0];
			switch (type)
			{
				case TupleTypes.Nil:
				{
					return '\0';
				}
				case TupleTypes.Bytes:
				{
					var s = TupleParser.ParseBytes(slice);
					if (s.Count == 0) return '\0';
					if (s.Count == 1) return (char) s[0];
					throw new FormatException($"Cannot convert buffer of size {s.Count} into a Char");
				}
				case TupleTypes.Utf8:
				{
					var s = TupleParser.ParseUnicode(slice);
					if (s.Length == 0) return '\0';
					if (s.Length == 1) return s[0];
					throw new FormatException($"Cannot convert string of size {s.Length} into a Char");
				}
			}

			if (type <= TupleTypes.IntPos8 && type >= TupleTypes.IntNeg8)
			{
				return (char) TupleParser.ParseInt64(type, slice);
			}

			throw new FormatException($"Cannot convert tuple segment of type 0x{type:X} into a Char");
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
				case TupleTypes.Decimal:
				{
					return TupleParser.ParseDecimal(slice).ToString(CultureInfo.InvariantCulture);
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

			throw new FormatException($"Cannot convert tuple segment of type 0x{type:X} into a String");
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

			throw new FormatException($"Cannot convert tuple segment of type 0x{type:X} into a System.Guid");
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

			throw new FormatException($"Cannot convert tuple segment of type 0x{type:X} into an Uuid128");
		}

		/// <summary>Deserialize a tuple segment into a 96-bit UUID</summary>
		/// <param name="slice">Slice that contains a single packed element</param>
		public static Uuid96 DeserializeUuid96(Slice slice)
		{
			if (slice.IsNullOrEmpty) return Uuid96.Empty;

			int type = slice[0];

			switch (type)
			{
				case TupleTypes.Bytes:
				{ // expect binary representation as a 12-byte array
					return Uuid96.Read(TupleParser.ParseBytes(slice));
				}
				case TupleTypes.Utf8:
				{ // expect text representation
					return Uuid96.Parse(TupleParser.ParseUnicode(slice));
				}
				case TupleTypes.VersionStamp96:
				{
					return TupleParser.ParseVersionStamp(slice).ToUuid96();
				}
			}

			throw new FormatException($"Cannot convert tuple segment of type 0x{type:X} into an Uuid96");
		}

		/// <summary>Deserialize a tuple segment into a 80-bit UUID</summary>
		/// <param name="slice">Slice that contains a single packed element</param>
		public static Uuid80 DeserializeUuid80(Slice slice)
		{
			if (slice.IsNullOrEmpty) return Uuid80.Empty;

			int type = slice[0];

			switch (type)
			{
				case TupleTypes.Bytes:
				{ // expect binary representation as a 10-byte array
					return Uuid80.Read(TupleParser.ParseBytes(slice));
				}
				case TupleTypes.Utf8:
				{ // expect text representation
					return Uuid80.Parse(TupleParser.ParseUnicode(slice));
				}
				case TupleTypes.VersionStamp80:
				{
					return TupleParser.ParseVersionStamp(slice).ToUuid80();
				}
			}

			throw new FormatException($"Cannot convert tuple segment of type 0x{type:X} into an Uuid80");
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
					return Uuid64.Read(TupleParser.ParseBytes(slice));
				}
				case TupleTypes.Utf8:
				{ // expect text representation
					return Uuid64.Parse(TupleParser.ParseUnicode(slice));
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

			throw new FormatException($"Cannot convert tuple segment of type 0x{type:X} into an Uuid64");
		}

		public static VersionStamp DeserializeVersionStamp(Slice slice)
		{
			if (slice.IsNullOrEmpty) return default;

			int type = slice[0];

			if (type == TupleTypes.VersionStamp80 || type == TupleTypes.VersionStamp96)
			{
				if (VersionStamp.TryParse(slice.Substring(1), out var stamp))
				{
					return stamp;
				}
				throw new FormatException("Cannot convert malformed tuple segment into a VersionStamp");
			}

			throw new FormatException($"Cannot convert tuple segment of type 0x{type:X} into a VersionStamp");
		}

		/// <summary>Deserialize a tuple segment into Guid</summary>
		/// <param name="slice">Slice that contains a single packed element</param>
		[CanBeNull]
		public static System.Net.IPAddress DeserializeIpAddress(Slice slice)
		{
			if (slice.IsNullOrEmpty) return null;

			int type = slice[0];

			switch (type)
			{
				case TupleTypes.Bytes:
				{
					return new System.Net.IPAddress(TupleParser.ParseBytes(slice).GetBytesOrEmpty());
				}
				case TupleTypes.Utf8:
				{
					return System.Net.IPAddress.Parse(TupleParser.ParseUnicode(slice));
				}
				case TupleTypes.Uuid128:
				{ // could be an IPv6 encoded as a 128-bits UUID
					return new System.Net.IPAddress(slice.GetBytesOrEmpty());
				}
			}

			if (type >= TupleTypes.IntPos1 && type <= TupleTypes.IntPos4)
			{ // could be an IPv4 encoded as a 32-bit unsigned integer
				var value = TupleParser.ParseInt64(type, slice);
				Contract.Assert(value >= 0 && value <= uint.MaxValue);
				return new System.Net.IPAddress(value);
			}

			throw new FormatException($"Cannot convert tuple segment of type 0x{type:X} into System.Net.IPAddress");
		}

		/// <summary>Unpack a tuple from a buffer</summary>
		/// <param name="buffer">Slice that contains the packed representation of a tuple with zero or more elements</param>
		/// <param name="embedded"></param>
		/// <returns>Decoded tuple</returns>
		[NotNull]
		internal static SlicedTuple Unpack(Slice buffer, bool embedded)
		{
			var reader = new TupleReader(buffer);
			if (embedded) reader.Depth = 1;
			return Unpack(ref reader);
		}

		/// <summary>Unpack a tuple from a buffer</summary>
		/// <param name="reader">Reader positionned on the start of the packed representation of a tuple with zero or more elements</param>
		/// <returns>Decoded tuple</returns>
		internal static SlicedTuple Unpack(ref TupleReader reader)
		{
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
			return new SlicedTuple(p == 0 ? Array.Empty<Slice>() : items, 0, p);
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
