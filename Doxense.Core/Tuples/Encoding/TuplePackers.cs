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

namespace Doxense.Collections.Tuples.Encoding
{
	using System;
	using System.Collections.Frozen;
	using System.Diagnostics.CodeAnalysis;
	using System.Globalization;
	using System.Linq.Expressions;
	using System.Reflection;
	using System.Runtime.CompilerServices;
	using Doxense.Collections.Tuples;
	using Doxense.Runtime.Converters;
	using Doxense.Serialization;

	/// <summary>Helper methods used during serialization of values to the tuple binary format</summary>
	[PublicAPI]
	public static class TuplePackers
	{

		#region Serializers...

		public delegate void Encoder<in T>(ref TupleWriter writer, T? value);

		/// <summary>Returns a lambda that will be able to serialize values of type <typeparamref name="T"/></summary>
		/// <typeparam name="T">Type of values to serialize</typeparam>
		/// <returns>Reusable action that knows how to serialize values of type <typeparamref name="T"/> into binary buffers, or that throws an exception if the type is not supported</returns>
		[ContractAnnotation("required:true => notnull")]
		internal static Encoder<T>? GetSerializer<T>(bool required)
		{
			//note: this method is only called once per initializing of TuplePackers<T> to create the cached delegate.

			var encoder = (Encoder<T>?) GetSerializerFor(typeof(T));

			return encoder ?? (required ? MakeNotSupportedSerializer<T>() : null);
		}

		[Pure]
		private static Encoder<T> MakeNotSupportedSerializer<T>()
		{
			return (ref TupleWriter _, T? _) => throw new InvalidOperationException($"Does not know how to serialize values of type '{typeof(T).Name}' into keys");
		}

		private static Delegate? GetSerializerFor(Type type)
		{
			Contract.NotNull(type);

			if (type == typeof(object))
			{ // return a generic serializer that will inspect the runtime type of the object
				return new Encoder<object>(SerializeObjectTo);
			}

			// look for well-known types that have their own (non-generic) TuplePackers.SerializeTo(...) method
			var method = typeof(TuplePackers).GetMethod(nameof(SerializeTo), BindingFlags.Static | BindingFlags.Public, binder: null, types: [ typeof(TupleWriter).MakeByRefType(), type ], modifiers: null);
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

		private static MethodInfo? FindSTupleSerializerMethod(Type[] args)
		{
			//note: we want to find the correct SerializeSTuple<...>(ref TupleWriter, (...,), but this cannot be done with Type.GetMethod(...) directly
			// => we have to scan for all methods with the correct name, and the same number of Type Arguments than the ValueTuple.
			return typeof(TuplePackers)
				   .GetMethods(BindingFlags.Static | BindingFlags.Public)
				   .SingleOrDefault(m => m.Name == nameof(SerializeSTupleTo) && m.GetGenericArguments().Length == args.Length);
		}

		private static MethodInfo? FindValueTupleSerializerMethod(Type[] args)
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
			// - In Release builds, this will be cleaned up and inlined by the JIT as a direct invocation of the correct WriteXYZ method
			// - In Debug builds, we have to disabled this, because it would be too slow
			//IMPORTANT: only ValueTypes and they must have a corresponding Write$TYPE$(ref TupleWriter, $TYPE) in TupleParser!
#if !DEBUG
			if (typeof(T) == typeof(bool)) { TupleParser.WriteBool(ref writer, (bool) (object) value!); return; }
			if (typeof(T) == typeof(int)) { TupleParser.WriteInt32(ref writer, (int) (object) value!); return; }
			if (typeof(T) == typeof(long)) { TupleParser.WriteInt64(ref writer, (long) (object) value!); return; }
			if (typeof(T) == typeof(uint)) { TupleParser.WriteUInt32(ref writer, (uint) (object) value!); return; }
			if (typeof(T) == typeof(ulong)) { TupleParser.WriteUInt64(ref writer, (ulong) (object) value!); return; }
			if (typeof(T) == typeof(short)) { TupleParser.WriteInt32(ref writer, (short) (object) value!); return; }
			if (typeof(T) == typeof(ushort)) { TupleParser.WriteUInt32(ref writer, (ushort) (object) value!); return; }
			if (typeof(T) == typeof(sbyte)) { TupleParser.WriteInt32(ref writer, (sbyte) (object) value!); return; }
			if (typeof(T) == typeof(byte)) { TupleParser.WriteUInt32(ref writer, (byte) (object) value!); return; }
			if (typeof(T) == typeof(float)) { TupleParser.WriteSingle(ref writer, (float) (object) value!); return; }
			if (typeof(T) == typeof(double)) { TupleParser.WriteDouble(ref writer, (double) (object) value!); return; }
			if (typeof(T) == typeof(decimal)) { TupleParser.WriteDecimal(ref writer, (decimal) (object) value!); return; }
			if (typeof(T) == typeof(char)) { TupleParser.WriteChar(ref writer, (char) (object) value!); return; }
			if (typeof(T) == typeof(TimeSpan)) { TupleParser.WriteTimeSpan(ref writer, (TimeSpan) (object) value!); return; }
			if (typeof(T) == typeof(DateTime)) { TupleParser.WriteDateTime(ref writer, (DateTime) (object) value!); return; }
			if (typeof(T) == typeof(DateTimeOffset)) { TupleParser.WriteDateTimeOffset(ref writer, (DateTimeOffset) (object) value!); return; }
			if (typeof(T) == typeof(Guid)) { TupleParser.WriteGuid(ref writer, (Guid) (object) value!); return; }
			if (typeof(T) == typeof(Uuid128)) { TupleParser.WriteUuid128(ref writer, (Uuid128) (object) value!); return; }
			if (typeof(T) == typeof(Uuid96)) { TupleParser.WriteUuid96(ref writer, (Uuid96) (object) value!); return; }
			if (typeof(T) == typeof(Uuid80)) { TupleParser.WriteUuid80(ref writer, (Uuid80) (object) value!); return; }
			if (typeof(T) == typeof(Uuid64)) { TupleParser.WriteUuid64(ref writer, (Uuid64) (object) value!); return; }
			if (typeof(T) == typeof(VersionStamp)) { TupleParser.WriteVersionStamp(ref writer, (VersionStamp) (object) value!); return; }
			if (typeof(T) == typeof(Slice)) { TupleParser.WriteBytes(ref writer, (Slice) (object) value!); return; }
			if (typeof(T) == typeof(MutableSlice)) { TupleParser.WriteBytes(ref writer, (MutableSlice) (object) value!); return; }
			if (typeof(T) == typeof(ArraySegment<byte>)) { TupleParser.WriteBytes(ref writer, (ArraySegment<byte>) (object) value!); return; }

			if (typeof(T) == typeof(bool?)) { TupleParser.WriteBool(ref writer, (bool?) (object) value!); return; }
			if (typeof(T) == typeof(int?)) { TupleParser.WriteInt32(ref writer, (int?) (object) value!); return; }
			if (typeof(T) == typeof(long?)) { TupleParser.WriteInt64(ref writer, (long?) (object) value!); return; }
			if (typeof(T) == typeof(uint?)) { TupleParser.WriteUInt32(ref writer, (uint?) (object) value!); return; }
			if (typeof(T) == typeof(ulong?)) { TupleParser.WriteUInt64(ref writer, (ulong?) (object) value!); return; }
			if (typeof(T) == typeof(short?)) { TupleParser.WriteInt32(ref writer, (short?) (object) value!); return; }
			if (typeof(T) == typeof(ushort?)) { TupleParser.WriteUInt32(ref writer, (ushort?) (object) value!); return; }
			if (typeof(T) == typeof(sbyte?)) { TupleParser.WriteInt32(ref writer, (sbyte?) (object) value!); return; }
			if (typeof(T) == typeof(byte?)) { TupleParser.WriteUInt32(ref writer, (byte?) (object) value!); return; }
			if (typeof(T) == typeof(float?)) { TupleParser.WriteSingle(ref writer, (float?) (object) value!); return; }
			if (typeof(T) == typeof(double?)) { TupleParser.WriteDouble(ref writer, (double?) (object) value!); return; }
			if (typeof(T) == typeof(decimal?)) { TupleParser.WriteDecimal(ref writer, (decimal?) (object) value!); return; }
			if (typeof(T) == typeof(char?)) { TupleParser.WriteChar(ref writer, (char?) (object) value!); return; }
			if (typeof(T) == typeof(TimeSpan?)) { TupleParser.WriteTimeSpan(ref writer, (TimeSpan?) (object) value!); return; }
			if (typeof(T) == typeof(DateTime?)) { TupleParser.WriteDateTime(ref writer, (DateTime?) (object) value!); return; }
			if (typeof(T) == typeof(DateTimeOffset?)) { TupleParser.WriteDateTimeOffset(ref writer, (DateTimeOffset?) (object) value!); return; }
			if (typeof(T) == typeof(Guid?)) { TupleParser.WriteGuid(ref writer, (Guid?) (object) value!); return; }
			if (typeof(T) == typeof(Uuid128?)) { TupleParser.WriteUuid128(ref writer, (Uuid128?) (object) value!); return; }
			if (typeof(T) == typeof(Uuid96?)) { TupleParser.WriteUuid96(ref writer, (Uuid96?) (object) value!); return; }
			if (typeof(T) == typeof(Uuid80?)) { TupleParser.WriteUuid80(ref writer, (Uuid80?) (object) value!); return; }
			if (typeof(T) == typeof(Uuid64?)) { TupleParser.WriteUuid64(ref writer, (Uuid64?) (object) value!); return; }
			if (typeof(T) == typeof(VersionStamp?)) { TupleParser.WriteVersionStamp(ref writer, (VersionStamp?) (object) value!); return; }
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
			{
				TupleParser.WriteNil(ref writer);
			}
			else
			{
				SerializeTo(ref writer, value.Value);
			}
		}

		/// <summary>Serialize an untyped object, by checking its type at runtime [VERY SLOW]</summary>
		/// <param name="writer">Target buffer</param>
		/// <param name="value">Untyped value whose type will be inspected at runtime</param>
		/// <remarks>
		/// May throw at runtime if the type is not supported.
		/// This method will be very slow! Please consider using typed tuples instead!
		/// </remarks>
		public static void SerializeObjectTo(ref TupleWriter writer, object? value)
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
			var encoders = TuplePackers.BoxedEncoders;
			if (!encoders.TryGetValue(type, out var encoder))
			{
				return GetBoxedEncoderSlow(type);
			}
			return encoder;

			static Encoder<object> GetBoxedEncoderSlow(Type type)
			{
				var encoder = CreateBoxedEncoder(type);
				while (true)
				{
					var encoders = TuplePackers.BoxedEncoders;
					var updated = new Dictionary<Type, Encoder<object>>(encoders) { [type] = encoder };

					if (Interlocked.CompareExchange(ref TuplePackers.BoxedEncoders, updated, encoders) == encoders)
					{
						break;
					}
				}
				return encoder;
			}
		}

		private static Dictionary<Type, Encoder<object>> BoxedEncoders = GetDefaultBoxedEncoders();

		private static Dictionary<Type, Encoder<object>> GetDefaultBoxedEncoders()
		{
			var encoders = new Dictionary<Type, Encoder<object>>(TypeEqualityComparer.Default)
			{
				[typeof(bool)] = (ref TupleWriter writer, object? value) => TupleParser.WriteBool(ref writer, (bool) value!),
				[typeof(bool?)] = (ref TupleWriter writer, object? value) => TupleParser.WriteBool(ref writer, (bool?) value),
				[typeof(char)] = (ref TupleWriter writer, object? value) => TupleParser.WriteChar(ref writer, (char) value!),
				[typeof(char?)] = (ref TupleWriter writer, object? value) => TupleParser.WriteChar(ref writer, (char?) value),
				[typeof(string)] = (ref TupleWriter writer, object? value) => TupleParser.WriteString(ref writer, (string?) value),
				[typeof(sbyte)] = (ref TupleWriter writer, object? value) => TupleParser.WriteInt32(ref writer, (sbyte) value!),
				[typeof(sbyte?)] = (ref TupleWriter writer, object? value) => TupleParser.WriteInt32(ref writer, (sbyte?) value),
				[typeof(short)] = (ref TupleWriter writer, object? value) => TupleParser.WriteInt32(ref writer, (short) value!),
				[typeof(short?)] = (ref TupleWriter writer, object? value) => TupleParser.WriteInt32(ref writer, (short?) value),
				[typeof(int)] = (ref TupleWriter writer, object? value) => TupleParser.WriteInt32(ref writer, (int) value!),
				[typeof(int?)] = (ref TupleWriter writer, object? value) => TupleParser.WriteInt32(ref writer, (int?) value),
				[typeof(long)] = (ref TupleWriter writer, object? value) => TupleParser.WriteInt64(ref writer, (long) value!),
				[typeof(long?)] = (ref TupleWriter writer, object? value) => TupleParser.WriteInt64(ref writer, (long?) value),
				[typeof(byte)] = (ref TupleWriter writer, object? value) => TupleParser.WriteByte(ref writer, (byte) value!),
				[typeof(byte?)] = (ref TupleWriter writer, object? value) => TupleParser.WriteByte(ref writer, (byte?) value),
				[typeof(ushort)] = (ref TupleWriter writer, object? value) => TupleParser.WriteUInt32(ref writer, (ushort) value!),
				[typeof(ushort?)] = (ref TupleWriter writer, object? value) => TupleParser.WriteUInt32(ref writer, (ushort?) value),
				[typeof(uint)] = (ref TupleWriter writer, object? value) => TupleParser.WriteUInt32(ref writer, (uint) value!),
				[typeof(uint?)] = (ref TupleWriter writer, object? value) => TupleParser.WriteUInt32(ref writer, (uint?) value),
				[typeof(ulong)] = (ref TupleWriter writer, object? value) => TupleParser.WriteUInt64(ref writer, (ulong) value!),
				[typeof(ulong?)] = (ref TupleWriter writer, object? value) => TupleParser.WriteUInt64(ref writer, (ulong?) value),
				[typeof(float)] = (ref TupleWriter writer, object? value) => TupleParser.WriteSingle(ref writer, (float) value!),
				[typeof(float?)] = (ref TupleWriter writer, object? value) => TupleParser.WriteSingle(ref writer, (float?) value),
				[typeof(double)] = (ref TupleWriter writer, object? value) => TupleParser.WriteDouble(ref writer, (double) value!),
				[typeof(double?)] = (ref TupleWriter writer, object? value) => TupleParser.WriteDouble(ref writer, (double?) value),
				[typeof(decimal)] = (ref TupleWriter writer, object? value) => TupleParser.WriteDecimal(ref writer, (decimal) value!),
				[typeof(decimal?)] = (ref TupleWriter writer, object? value) => TupleParser.WriteDecimal(ref writer, (decimal?) value),
				[typeof(Slice)] = (ref TupleWriter writer, object? value) => TupleParser.WriteBytes(ref writer, (Slice) value!),
				[typeof(MutableSlice)] = (ref TupleWriter writer, object? value) => TupleParser.WriteBytes(ref writer, (MutableSlice) value!),
				[typeof(byte[])] = (ref TupleWriter writer, object? value) => TupleParser.WriteBytes(ref writer, (byte[]?) value),
				[typeof(Guid)] = (ref TupleWriter writer, object? value) => TupleParser.WriteGuid(ref writer, (Guid) value!),
				[typeof(Guid?)] = (ref TupleWriter writer, object? value) => TupleParser.WriteGuid(ref writer, (Guid?) value),
				[typeof(Uuid128)] = (ref TupleWriter writer, object? value) => TupleParser.WriteUuid128(ref writer, (Uuid128) value!),
				[typeof(Uuid128?)] = (ref TupleWriter writer, object? value) => TupleParser.WriteUuid128(ref writer, (Uuid128?) value),
				[typeof(Uuid96)] = (ref TupleWriter writer, object? value) => TupleParser.WriteUuid96(ref writer, (Uuid96) value!),
				[typeof(Uuid96?)] = (ref TupleWriter writer, object? value) => TupleParser.WriteUuid96(ref writer, (Uuid96?) value),
				[typeof(Uuid80)] = (ref TupleWriter writer, object? value) => TupleParser.WriteUuid80(ref writer, (Uuid80) value!),
				[typeof(Uuid80?)] = (ref TupleWriter writer, object? value) => TupleParser.WriteUuid80(ref writer, (Uuid80?) value),
				[typeof(Uuid64)] = (ref TupleWriter writer, object? value) => TupleParser.WriteUuid64(ref writer, (Uuid64) value!),
				[typeof(Uuid64?)] = (ref TupleWriter writer, object? value) => TupleParser.WriteUuid64(ref writer, (Uuid64?) value),
				[typeof(VersionStamp)] = (ref TupleWriter writer, object? value) => TupleParser.WriteVersionStamp(ref writer, (VersionStamp) value!),
				[typeof(VersionStamp?)] = (ref TupleWriter writer, object? value) => TupleParser.WriteVersionStamp(ref writer, (VersionStamp?) value),
				[typeof(TimeSpan)] = (ref TupleWriter writer, object? value) => TupleParser.WriteTimeSpan(ref writer, (TimeSpan) value!),
				[typeof(TimeSpan?)] = (ref TupleWriter writer, object? value) => TupleParser.WriteTimeSpan(ref writer, (TimeSpan?) value),
				[typeof(DateTime)] = (ref TupleWriter writer, object? value) => TupleParser.WriteDateTime(ref writer, (DateTime) value!),
				[typeof(DateTime?)] = (ref TupleWriter writer, object? value) => TupleParser.WriteDateTime(ref writer, (DateTime?) value),
				[typeof(DateTimeOffset)] = (ref TupleWriter writer, object? value) => TupleParser.WriteDateTimeOffset(ref writer, (DateTimeOffset) value!),
				[typeof(DateTimeOffset?)] = (ref TupleWriter writer, object? value) => TupleParser.WriteDateTimeOffset(ref writer, (DateTimeOffset?) value),
				[typeof(IVarTuple)] = (ref TupleWriter writer, object? value) => SerializeTupleTo(ref writer, (IVarTuple) value!),
				//TODO: add System.Runtime.CompilerServices.ITuple for net471+
				[typeof(DBNull)] = (ref TupleWriter writer, object? _) => TupleParser.WriteNil(ref writer)
			};

			return encoders;
		}

		private static Encoder<object> CreateBoxedEncoder(Type type)
		{
			var m = typeof(TuplePacker<>).MakeGenericType(type).GetMethod(nameof(TuplePacker<int>.SerializeBoxedTo));
			Contract.Debug.Assert(m != null);

			var writer = Expression.Parameter(typeof(TupleWriter).MakeByRefType(), "writer");
			var value = Expression.Parameter(typeof(object), "value");

			var body = Expression.Call(m, writer, value);
			return Expression.Lambda<Encoder<object>>(body, writer, value).Compile();
		}

		/// <summary>Writes a slice as a byte[] array</summary>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void SerializeTo(ref TupleWriter writer, Slice value) => TupleParser.WriteBytes(ref writer, value);

		/// <summary>Writes a slice as a byte[] array</summary>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void SerializeTo(ref TupleWriter writer, MutableSlice value) => TupleParser.WriteBytes(ref writer, value);

		/// <summary>Writes a byte[] array</summary>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void SerializeTo(ref TupleWriter writer, byte[] value) => TupleParser.WriteBytes(ref writer, value);

		/// <summary>Writes an array segment as a byte[] array</summary>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void SerializeTo(ref TupleWriter writer, ArraySegment<byte> value) => TupleParser.WriteBytes(ref writer, value);

		/// <summary>Writes a char as Unicode string</summary>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void SerializeTo(ref TupleWriter writer, char value) => TupleParser.WriteChar(ref writer, value);

		/// <summary>Writes a boolean as an integer</summary>
		/// <remarks>Uses 0 for false, and -1 for true</remarks>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void SerializeTo(ref TupleWriter writer, bool value) => TupleParser.WriteBool(ref writer, value);

		/// <summary>Writes a boolean as an integer or null</summary>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void SerializeTo(ref TupleWriter writer, bool? value) => TupleParser.WriteBool(ref writer, value);
		//REVIEW: only method for a nullable type? add others? or remove this one?

		/// <summary>Writes a signed byte as an integer</summary>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void SerializeTo(ref TupleWriter writer, sbyte value) => TupleParser.WriteInt32(ref writer, value);

		/// <summary>Writes an unsigned byte as an integer</summary>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void SerializeTo(ref TupleWriter writer, byte value) => TupleParser.WriteByte(ref writer, value);

		/// <summary>Writes a signed word as an integer</summary>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void SerializeTo(ref TupleWriter writer, short value) => TupleParser.WriteInt32(ref writer, value);

		/// <summary>Writes an unsigned word as an integer</summary>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void SerializeTo(ref TupleWriter writer, ushort value) => TupleParser.WriteUInt32(ref writer, value);

		/// <summary>Writes a signed int as an integer</summary>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void SerializeTo(ref TupleWriter writer, int value) => TupleParser.WriteInt32(ref writer, value);

		/// <summary>Writes a signed int as an integer</summary>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void SerializeTo(ref TupleWriter writer, int? value) => TupleParser.WriteInt32(ref writer, value);

		/// <summary>Writes an unsigned int as an integer</summary>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void SerializeTo(ref TupleWriter writer, uint value) => TupleParser.WriteUInt32(ref writer, value);

		/// <summary>Writes an unsigned int as an integer</summary>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void SerializeTo(ref TupleWriter writer, uint? value) => TupleParser.WriteUInt32(ref writer, value);

		/// <summary>Writes a signed long as an integer</summary>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void SerializeTo(ref TupleWriter writer, long value) => TupleParser.WriteInt64(ref writer, value);

		/// <summary>Writes a signed long as an integer</summary>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void SerializeTo(ref TupleWriter writer, long? value) => TupleParser.WriteInt64(ref writer, value);

		/// <summary>Writes an unsigned long as an integer</summary>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void SerializeTo(ref TupleWriter writer, ulong value) => TupleParser.WriteUInt64(ref writer, value);

		/// <summary>Writes an unsigned long as an integer</summary>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void SerializeTo(ref TupleWriter writer, ulong? value) => TupleParser.WriteUInt64(ref writer, value);

		/// <summary>Writes a 32-bit IEEE floating point number</summary>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void SerializeTo(ref TupleWriter writer, float value) => TupleParser.WriteSingle(ref writer, value);

		/// <summary>Writes a 32-bit IEEE floating point number</summary>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void SerializeTo(ref TupleWriter writer, float? value) => TupleParser.WriteSingle(ref writer, value);

		/// <summary>Writes a 64-bit IEEE floating point number</summary>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void SerializeTo(ref TupleWriter writer, double value) => TupleParser.WriteDouble(ref writer, value);

		/// <summary>Writes a 64-bit IEEE floating point number</summary>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void SerializeTo(ref TupleWriter writer, double? value) => TupleParser.WriteDouble(ref writer, value);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void SerializeTo(ref TupleWriter writer, decimal value) => TupleParser.WriteDecimal(ref writer, value);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void SerializeTo(ref TupleWriter writer, decimal? value) => TupleParser.WriteDecimal(ref writer, value);

		/// <summary>Writes a string as an Unicode string</summary>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void SerializeTo(ref TupleWriter writer, string value) => TupleParser.WriteString(ref writer, value);

		/// <summary>Writes a TimeSpan converted to to a number seconds encoded as a 64-bit decimal</summary>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void SerializeTo(ref TupleWriter writer, TimeSpan value) => TupleParser.WriteTimeSpan(ref writer, value);

		/// <summary>Writes a TimeSpan converted to to a number seconds encoded as a 64-bit decimal</summary>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void SerializeTo(ref TupleWriter writer, TimeSpan? value) => TupleParser.WriteTimeSpan(ref writer, value);

		/// <summary>Writes a DateTime converted to the number of days since the Unix Epoch and stored as a 64-bit decimal</summary>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void SerializeTo(ref TupleWriter writer, DateTime value) => TupleParser.WriteDateTime(ref writer, value);

		/// <summary>Writes a DateTime converted to the number of days since the Unix Epoch and stored as a 64-bit decimal</summary>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void SerializeTo(ref TupleWriter writer, DateTime? value) => TupleParser.WriteDateTime(ref writer, value);

		/// <summary>Writes a DateTimeOffset converted to the number of days since the Unix Epoch and stored as a 64-bit decimal</summary>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void SerializeTo(ref TupleWriter writer, DateTimeOffset value) => TupleParser.WriteDateTimeOffset(ref writer, value);

		/// <summary>Writes a DateTimeOffset converted to the number of days since the Unix Epoch and stored as a 64-bit decimal</summary>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void SerializeTo(ref TupleWriter writer, DateTimeOffset? value) => TupleParser.WriteDateTimeOffset(ref writer, value);

		/// <summary>Writes a Guid as a 128-bit UUID</summary>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void SerializeTo(ref TupleWriter writer, Guid value) => TupleParser.WriteGuid(ref writer, value);
		//REVIEW: should we consider serializing Guid.Empty as <14> (integer 0) ? or maybe <01><00> (empty byte string) ?
		// => could spare 17 bytes per key in indexes on GUID properties that are frequently missing or empty (== default(Guid))

		/// <summary>Writes a Guid as a 128-bit UUID</summary>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void SerializeTo(ref TupleWriter writer, Guid? value) => TupleParser.WriteGuid(ref writer, value);

		/// <summary>Writes a Uuid as a 128-bit UUID</summary>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void SerializeTo(ref TupleWriter writer, Uuid128 value) => TupleParser.WriteUuid128(ref writer, value);

		/// <summary>Writes a Uuid as a 128-bit UUID</summary>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void SerializeTo(ref TupleWriter writer, Uuid128? value) => TupleParser.WriteUuid128(ref writer, value);

		/// <summary>Writes a Uuid as a 96-bit UUID</summary>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void SerializeTo(ref TupleWriter writer, Uuid96 value) => TupleParser.WriteUuid96(ref writer,  value);

		/// <summary>Writes a Uuid as a 96-bit UUID</summary>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void SerializeTo(ref TupleWriter writer, Uuid96? value) => TupleParser.WriteUuid96(ref writer, value);

		/// <summary>Writes a Uuid as a 80-bit UUID</summary>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void SerializeTo(ref TupleWriter writer, Uuid80 value) => TupleParser.WriteUuid80(ref writer, value);

		/// <summary>Writes a Uuid as a 80-bit UUID</summary>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void SerializeTo(ref TupleWriter writer, Uuid80? value) => TupleParser.WriteUuid80(ref writer, value);

		/// <summary>Writes a Uuid as a 64-bit UUID</summary>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void SerializeTo(ref TupleWriter writer, Uuid64 value) => TupleParser.WriteUuid64(ref writer, value);

		/// <summary>Writes a Uuid as a 64-bit UUID</summary>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void SerializeTo(ref TupleWriter writer, Uuid64? value) => TupleParser.WriteUuid64(ref writer, value);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void SerializeTo(ref TupleWriter writer, VersionStamp value) => TupleParser.WriteVersionStamp(ref writer, value);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void SerializeTo(ref TupleWriter writer, VersionStamp? value) => TupleParser.WriteVersionStamp(ref writer, value);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void SerializeTo(ref TupleWriter writer, TuPackUserType value) => TupleParser.WriteUserType(ref writer, value);

		/// <summary>Writes an IP Address as a 32-bit (IPv4) or 128-bit (IPv6) byte array</summary>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void SerializeTo(ref TupleWriter writer, System.Net.IPAddress? value) => TupleParser.WriteBytes(ref writer, value?.GetAddressBytes());

		/// <summary>Serialize an embedded tuples</summary>
		public static void SerializeTupleTo<TTuple>(ref TupleWriter writer, TTuple tuple)
			where TTuple : IVarTuple
		{
			Contract.Debug.Requires(tuple != null);

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

		public delegate T Decoder<out T>(ReadOnlySpan<byte> packed);

		private static readonly FrozenDictionary<Type, Delegate> WellKnownUnpackers = InitializeDefaultUnpackers();

		private static FrozenDictionary<Type, Delegate> InitializeDefaultUnpackers()
		{
			var map = new Dictionary<Type, Delegate>
			{
				[typeof(Slice)] = new Decoder<Slice>(TuplePackers.DeserializeSlice),
				[typeof(byte[])] = new Decoder<byte[]?>(TuplePackers.DeserializeBytes),
				[typeof(bool)] = new Decoder<bool>(TuplePackers.DeserializeBoolean),
				[typeof(string)] = new Decoder<string?>(TuplePackers.DeserializeString),
				[typeof(char)] = new Decoder<char>(TuplePackers.DeserializeChar),
				[typeof(sbyte)] = new Decoder<sbyte>(TuplePackers.DeserializeSByte),
				[typeof(short)] = new Decoder<short>(TuplePackers.DeserializeInt16),
				[typeof(int)] = new Decoder<int>(TuplePackers.DeserializeInt32),
				[typeof(long)] = new Decoder<long>(TuplePackers.DeserializeInt64),
				[typeof(byte)] = new Decoder<byte>(TuplePackers.DeserializeByte),
				[typeof(ushort)] = new Decoder<ushort>(TuplePackers.DeserializeUInt16),
				[typeof(uint)] = new Decoder<uint>(TuplePackers.DeserializeUInt32),
				[typeof(ulong)] = new Decoder<ulong>(TuplePackers.DeserializeUInt64),
				[typeof(float)] = new Decoder<float>(TuplePackers.DeserializeSingle),
				[typeof(double)] = new Decoder<double>(TuplePackers.DeserializeDouble),
				//[typeof(decimal)] = new Decoder<decimal>(TuplePackers.DeserializeDecimal), //TODO: not implemented
				[typeof(Guid)] = new Decoder<Guid>(TuplePackers.DeserializeGuid),
				[typeof(Uuid128)] = new Decoder<Uuid128>(TuplePackers.DeserializeUuid128),
				[typeof(Uuid96)] = new Decoder<Uuid96>(TuplePackers.DeserializeUuid96),
				[typeof(Uuid80)] = new Decoder<Uuid80>(TuplePackers.DeserializeUuid80),
				[typeof(Uuid64)] = new Decoder<Uuid64>(TuplePackers.DeserializeUuid64),
				[typeof(TimeSpan)] = new Decoder<TimeSpan>(TuplePackers.DeserializeTimeSpan),
				[typeof(DateTime)] = new Decoder<DateTime>(TuplePackers.DeserializeDateTime),
				[typeof(DateTimeOffset)] = new Decoder<DateTimeOffset>(TuplePackers.DeserializeDateTimeOffset),
				[typeof(System.Net.IPAddress)] = new Decoder<System.Net.IPAddress?>(TuplePackers.DeserializeIpAddress),
				[typeof(VersionStamp)] = new Decoder<VersionStamp>(TuplePackers.DeserializeVersionStamp),
				[typeof(IVarTuple)] = new Decoder<IVarTuple?>(TuplePackers.DeserializeTuple),
				[typeof(TuPackUserType)] = new Decoder<TuPackUserType?>(TuplePackers.DeserializeUserType),

				// nullables

				[typeof(bool?)] = new Decoder<bool?>(TuplePackers.DeserializeBooleanNullable),
				[typeof(char?)] = new Decoder<char?>(TuplePackers.DeserializeCharNullable),
				[typeof(sbyte?)] = new Decoder<sbyte?>(TuplePackers.DeserializeSByteNullable),
				[typeof(short?)] = new Decoder<short?>(TuplePackers.DeserializeInt16Nullable),
				[typeof(int?)] = new Decoder<int?>(TuplePackers.DeserializeInt32Nullable),
				[typeof(long?)] = new Decoder<long?>(TuplePackers.DeserializeInt64Nullable),
				[typeof(byte?)] = new Decoder<byte?>(TuplePackers.DeserializeByteNullable),
				[typeof(ushort?)] = new Decoder<ushort?>(TuplePackers.DeserializeUInt16Nullable),
				[typeof(uint?)] = new Decoder<uint?>(TuplePackers.DeserializeUInt32Nullable),
				[typeof(ulong?)] = new Decoder<ulong?>(TuplePackers.DeserializeUInt64Nullable),
				[typeof(float?)] = new Decoder<float?>(TuplePackers.DeserializeSingleNullable),
				[typeof(double?)] = new Decoder<double?>(TuplePackers.DeserializeDoubleNullable),
				//[typeof(decimal?)] = new Decoder<decimal?>(TuplePackers.DeserializeDecimalNullable), //TODO: not implemented
				[typeof(Guid?)] = new Decoder<Guid?>(TuplePackers.DeserializeGuidNullable),
				[typeof(Uuid128?)] = new Decoder<Uuid128?>(TuplePackers.DeserializeUuid128Nullable),
				[typeof(Uuid96?)] = new Decoder<Uuid96?>(TuplePackers.DeserializeUuid96Nullable),
				[typeof(Uuid80?)] = new Decoder<Uuid80?>(TuplePackers.DeserializeUuid80Nullable),
				[typeof(Uuid64?)] = new Decoder<Uuid64?>(TuplePackers.DeserializeUuid64Nullable),
				[typeof(TimeSpan?)] = new Decoder<TimeSpan?>(TuplePackers.DeserializeTimeSpanNullable),
				[typeof(DateTime?)] = new Decoder<DateTime?>(TuplePackers.DeserializeDateTimeNullable),
				[typeof(DateTimeOffset?)] = new Decoder<DateTimeOffset?>(TuplePackers.DeserializeDateTimeOffsetNullable),
				[typeof(VersionStamp?)] = new Decoder<VersionStamp?>(TuplePackers.DeserializeVersionStampNullable),

			};

			return map.ToFrozenDictionary();
		}

		/// <summary>Returns a lambda that will be able to serialize values of type <typeparamref name="T"/></summary>
		/// <typeparam name="T">Type of values to serialize</typeparam>
		/// <returns>Reusable action that knows how to serialize values of type <typeparamref name="T"/> into binary buffers, or an exception if the type is not supported</returns>
		internal static Decoder<T> GetDeserializer<T>(bool required)
		{
			Type type = typeof(T);

			if (WellKnownUnpackers.TryGetValue(type, out var decoder))
			{ // We already know how to decode this type
				return (Decoder<T>) decoder;
			}

			// Nullable<T>
			var underlyingType = Nullable.GetUnderlyingType(typeof(T));
			if (underlyingType != null && WellKnownUnpackers.TryGetValue(underlyingType, out decoder))
			{ 
				return (Decoder<T>) MakeNullableDeserializer(type, underlyingType, decoder);
			}

			// STuple<...>
			if (typeof(IVarTuple).IsAssignableFrom(type))
			{
				if (type.IsValueType && type.IsGenericType && type.Name.StartsWith(nameof(STuple) + "`", StringComparison.Ordinal))
				{
					return (Decoder<T>) MakeSTupleDeserializer(type);
				}
			}

			if ((type.Name == nameof(ValueTuple) || type.Name.StartsWith(nameof(ValueTuple) + "`", StringComparison.Ordinal)) && type.Namespace == "System")
			{
				return (Decoder<T>) MakeValueTupleDeserializer(type);
			}

			if (required)
			{ // will throw at runtime
				return MakeNotSupportedDeserializer<T>();
			}
			// when all else fails...
			return MakeConvertBoxedDeserializer<T>();
		}

		[Pure]
		private static Decoder<T> MakeNotSupportedDeserializer<T>()
		{
			return (_) => throw new InvalidOperationException($"Does not know how to deserialize keys into values of type {typeof(T).GetFriendlyName()}");
		}

		[Pure]
		private static Decoder<T> MakeConvertBoxedDeserializer<T>()
		{
			return (value) => TypeConverters.ConvertBoxed<T>(DeserializeBoxed(value))!;
		}

		/// <summary>Check if a tuple segment is the equivalent of 'Nil'</summary>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		internal static bool IsNilSegment(Slice slice)
		{
			return slice.IsNullOrEmpty || slice[0] == TupleTypes.Nil;
		}

		/// <summary>Check if a tuple segment is the equivalent of 'Nil'</summary>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		internal static bool IsNilSegment(ReadOnlySpan<byte> slice)
		{
			return slice.Length == 0 || slice[0] == TupleTypes.Nil;
		}

		[Pure]
		private static Delegate MakeNullableDeserializer(Type nullableType, Type type, Delegate decoder)
		{
			Contract.Debug.Requires(nullableType != null && type != null && decoder != null);
			// We have a Decoder of T, but we have to transform it into a Decoder for Nullable<T>, which returns null if the slice is "nil", or falls back to the underlying decoder if the slice contains something

			var prmSlice = Expression.Parameter(typeof(Slice), "slice");
			var body = Expression.Condition(
				// IsNilSegment(slice) ?
				Expression.Call(typeof(TuplePackers).GetMethod(nameof(IsNilSegment), BindingFlags.Static | BindingFlags.NonPublic)!, prmSlice),
				// True => default(Nullable<T>)
				Expression.Default(nullableType),
				// False => decoder(slice)
				Expression.Convert(Expression.Invoke(Expression.Constant(decoder), prmSlice), nullableType)
			);

			return Expression.Lambda(body, prmSlice).Compile();
		}


		private static Dictionary<int, MethodInfo> STupleCandidateMethods = ComputeSTupleCandidateMethods();

		private static Dictionary<int, MethodInfo> ComputeSTupleCandidateMethods() => typeof(TuplePackers)
			.GetMethods()
			.Where(m => m.Name == nameof(DeserializeTuple) && m.IsGenericMethod && m.GetParameters().Length == 1 && m.GetParameters()[0].ParameterType == typeof(ReadOnlySpan<byte>))
			.ToDictionary(m => m.GetGenericArguments().Length);

		[Pure]
		private static Delegate MakeSTupleDeserializer(Type type)
		{
			Contract.Debug.Requires(type != null);

			// (slice) => TuPack.DeserializeTuple<T...>(slice)

			var targs = type.GetGenericArguments();
			if (!STupleCandidateMethods.TryGetValue(targs.Length, out var method))
			{
				throw new InvalidOperationException($"There is no method able to deserialize a tuple with {targs.Length} arguments!");
			}
			method = method.MakeGenericMethod(targs);

			var prmSlice = Expression.Parameter(typeof(ReadOnlySpan<byte>), "slice");
			var body = Expression.Call(method, prmSlice);

			var fnType = typeof(Decoder<>).MakeGenericType(type);

			return Expression.Lambda(fnType, body, prmSlice).Compile();
		}

		private static Dictionary<int, MethodInfo> ValueTupleCandidateMethods = ComputeValueTupleCandidateMethods();

		private static Dictionary<int, MethodInfo> ComputeValueTupleCandidateMethods() => typeof(TuplePackers)
			.GetMethods()
			.Where(m => m.Name == nameof(DeserializeValueTuple) && m.IsGenericMethod && m.GetParameters().Length == 1 && m.GetParameters()[0].ParameterType == typeof(ReadOnlySpan<byte>))
			.ToDictionary(m => m.GetGenericArguments().Length);

		[Pure]
		private static Delegate MakeValueTupleDeserializer(Type type)
		{
			Contract.Debug.Requires(type != null);

			// (slice) => TuPack.DeserializeValueTuple<T...>(slice)

			var targs = type.GetGenericArguments();
			if (!ValueTupleCandidateMethods.TryGetValue(targs.Length, out var method))
			{
				throw new InvalidOperationException($"There is no method able to deserialize a tuple with {targs.Length} arguments!");
			}
			method = method.MakeGenericMethod(targs);

			var prmSlice = Expression.Parameter(typeof(ReadOnlySpan<byte>), "slice");
			var body = Expression.Call(method, prmSlice);

			var fnType = typeof(Decoder<>).MakeGenericType(type);

			return Expression.Lambda(fnType, body, prmSlice).Compile();
		}

		/// <summary>Deserialize a packed element into an object by choosing the most appropriate type at runtime</summary>
		/// <param name="slice">Slice that contains a single packed element</param>
		/// <returns>Decoded element, in the type that is the best fit.</returns>
		/// <remarks>You should avoid working with untyped values as much as possible! Blindly casting the returned object may be problematic because this method may need to return very large integers as Int64 or even UInt64.</remarks>
		public static object? DeserializeBoxed(Slice slice)
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

		/// <summary>Deserialize a packed element into an object by choosing the most appropriate type at runtime</summary>
		/// <param name="slice">Slice that contains a single packed element</param>
		/// <returns>Decoded element, in the type that is the best fit.</returns>
		/// <remarks>You should avoid working with untyped values as much as possible! Blindly casting the returned object may be problematic because this method may need to return very large integers as Int64 or even UInt64.</remarks>
		public static object? DeserializeBoxed(ReadOnlySpan<byte> slice)
		{
			if (slice.Length == 0) return null;

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
					case TupleTypes.EmbeddedTuple: return TupleParser.ParseTuple(slice).ToTuple();
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
						if (slice.Length == 1) return TuPackUserType.Directory;
						break;
					}
					case TupleTypes.Escape:
					{
						if (slice.Length == 1) return TuPackUserType.System;
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

		/// <summary>Deserialize a tuple segment into a Slice</summary>
		public static Slice DeserializeSlice(ReadOnlySpan<byte> slice)
		{
			// Convert the tuple value into a sensible Slice representation.
			// The behavior should be equivalent to calling the corresponding Slice.From{TYPE}(TYPE value)

			if (slice.Length == 0) return Slice.Nil; //TODO: fail ?

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
		[MethodImpl(MethodImplOptions.AggressiveInlining)] //REVIEW: because of Slice.GetBytes()
		public static byte[]? DeserializeBytes(Slice slice)
		{
			return DeserializeSlice(slice).GetBytes();
		}

		/// <summary>Deserialize a tuple segment into a byte array</summary>
		[MethodImpl(MethodImplOptions.AggressiveInlining)] //REVIEW: because of Slice.GetBytes()
		public static byte[]? DeserializeBytes(ReadOnlySpan<byte> slice)
		{
			//note: DeserializeSlice(RoS<byte>) already creates a copy, hopefully with the correct size, so we can expose it safely
			var decoded = DeserializeSlice(slice);
			return decoded.TryGetBytesUnsafe(out var bytes) ? bytes : decoded.GetBytes();
		}

		public static TuPackUserType? DeserializeUserType(Slice slice)
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

		public static TuPackUserType? DeserializeUserType(ReadOnlySpan<byte> slice)
		{
			if (slice.Length == 0) return null; //TODO: fail ?

			int type = slice[0];
			if (slice.Length == 1)
			{
				switch (type)
				{
					case TupleTypes.Nil: return null;
					case TupleTypes.Directory: return TuPackUserType.Directory;
					case TupleTypes.Escape: return TuPackUserType.System;
				}
				return new TuPackUserType(type);
			}

			return new TuPackUserType(type, Slice.Copy(slice.Slice(1)));
		}

		/// <summary>Deserialize a tuple segment into a tuple</summary>
		public static IVarTuple? DeserializeTuple(Slice slice)
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

		/// <summary>Deserialize a tuple segment into a tuple</summary>
		public static IVarTuple? DeserializeTuple(ReadOnlySpan<byte> slice)
		{
			if (slice.Length == 0) return null;

			byte type = slice[0];
			return type switch
			{
				TupleTypes.Nil => null,
				TupleTypes.Bytes => TupleEncoder.Unpack(TupleParser.ParseBytes(slice)),
				TupleTypes.LegacyTupleStart => throw TupleParser.FailLegacyTupleNotSupported(),
				TupleTypes.EmbeddedTuple => TupleParser.ParseTuple(slice).ToTuple(),
				_ => throw new FormatException("Cannot convert tuple segment into a Tuple")
			};
		}

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static STuple<T1?> DeserializeTuple<T1>(ReadOnlySpan<byte> slice) => DeserializeValueTuple<T1>(slice);

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static STuple<T1?, T2?> DeserializeTuple<T1, T2>(ReadOnlySpan<byte> slice) => DeserializeValueTuple<T1, T2>(slice);

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static STuple<T1?, T2?, T3?> DeserializeTuple<T1, T2, T3>(ReadOnlySpan<byte> slice) => DeserializeValueTuple<T1, T2, T3>(slice);

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static STuple<T1?, T2?, T3?, T4?> DeserializeTuple<T1, T2, T3, T4>(ReadOnlySpan<byte> slice) => DeserializeValueTuple<T1, T2, T3, T4>(slice);

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static STuple<T1?, T2?, T3?, T4?, T5?> DeserializeTuple<T1, T2, T3, T4, T5>(ReadOnlySpan<byte> slice) => DeserializeValueTuple<T1, T2, T3, T4, T5>(slice);

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static STuple<T1?, T2?, T3?, T4?, T5?, T6?> DeserializeTuple<T1, T2, T3, T4, T5, T6>(ReadOnlySpan<byte> slice) => DeserializeValueTuple<T1, T2, T3, T4, T5, T6>(slice);

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static STuple<T1?, T2?, T3?, T4?, T5?, T6?, T7?> DeserializeTuple<T1, T2, T3, T4, T5, T6, T7>(ReadOnlySpan<byte> slice) => DeserializeValueTuple<T1, T2, T3, T4, T5, T6, T7>(slice);

		//TODO: there is not STuple with 8 arguments !

		[Pure]
		public static ValueTuple<T1?> DeserializeValueTuple<T1>(ReadOnlySpan<byte> slice)
		{
			ValueTuple<T1?> res = default;
			if (slice.Length != 0)
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
						var bytes = TupleParser.ParseBytes(slice);
						var reader = new TupleReader(bytes.Span);
						//note: depth is 0 since the embedded tuple was encoded as top-level
						TupleEncoder.DecodeKey(ref reader, out res);
						break;
					}
					case TupleTypes.LegacyTupleStart: throw TupleParser.FailLegacyTupleNotSupported();
					case TupleTypes.EmbeddedTuple:
					{
						// extract the embedded tuple, and resume parsing
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
		public static ValueTuple<T1?, T2?> DeserializeValueTuple<T1, T2>(ReadOnlySpan<byte> slice)
		{
			var res = default(ValueTuple<T1?, T2?>);
			if (slice.Length != 0)
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
						var bytes = TupleParser.ParseBytes(slice);
						var reader = new TupleReader(bytes.Span);
						//note: depth is 0 since the embedded tuple was encoded as top-level
						TupleEncoder.DecodeKey(ref reader, out res);
						break;
					}
					case TupleTypes.LegacyTupleStart: throw TupleParser.FailLegacyTupleNotSupported();
					case TupleTypes.EmbeddedTuple:
					{
						// extract the embedded tuple, and resume parsing
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
		public static ValueTuple<T1?, T2?, T3?> DeserializeValueTuple<T1, T2, T3>(ReadOnlySpan<byte> slice)
		{
			var res = default(ValueTuple<T1?, T2?, T3?>);
			if (slice.Length != 0)
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
						var bytes = TupleParser.ParseBytes(slice);
						var reader = new TupleReader(bytes.Span);
						//note: depth is 0 since the embedded tuple was encoded as top-level
						TupleEncoder.DecodeKey(ref reader, out res);
						break;
					}
					case TupleTypes.LegacyTupleStart: throw TupleParser.FailLegacyTupleNotSupported();
					case TupleTypes.EmbeddedTuple:
					{
						// extract the embedded tuple, and resume parsing
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
		public static ValueTuple<T1?, T2?, T3?, T4?> DeserializeValueTuple<T1, T2, T3, T4>(ReadOnlySpan<byte> slice)
		{
			var res = default(ValueTuple<T1?, T2?, T3?, T4?>);
			if (slice.Length != 0)
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
						var bytes = TupleParser.ParseBytes(slice);
						var reader = new TupleReader(bytes.Span);
						//note: depth is 0 since the embedded tuple was encoded as top-level
						TupleEncoder.DecodeKey(ref reader, out res);
						break;
					}
					case TupleTypes.LegacyTupleStart: throw TupleParser.FailLegacyTupleNotSupported();
					case TupleTypes.EmbeddedTuple:
					{
						// extract the embedded tuple, and resume parsing
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
		public static ValueTuple<T1?, T2?, T3?, T4?, T5?> DeserializeValueTuple<T1, T2, T3, T4, T5>(ReadOnlySpan<byte> slice)
		{
			var res = default(ValueTuple<T1?, T2?, T3?, T4?, T5?>);
			if (slice.Length != 0)
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
						var bytes = TupleParser.ParseBytes(slice);
						var reader = new TupleReader(bytes.Span);
						//note: depth is 0 since the embedded tuple was encoded as top-level
						TupleEncoder.DecodeKey(ref reader, out res);
						break;
					}
					case TupleTypes.LegacyTupleStart: throw TupleParser.FailLegacyTupleNotSupported();
					case TupleTypes.EmbeddedTuple:
					{
						// extract the embedded tuple, and resume parsing
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
		public static (T1?, T2?, T3?, T4?, T5?, T6?) DeserializeValueTuple<T1, T2, T3, T4, T5, T6>(ReadOnlySpan<byte> slice)
		{
			var res = default((T1?, T2?, T3?, T4?, T5?, T6?));
			if (slice.Length != 0)
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
						var bytes = TupleParser.ParseBytes(slice);
						var reader = new TupleReader(bytes.Span);
						//note: depth is 0 since the embedded tuple was encoded as top-level
						TupleEncoder.DecodeKey(ref reader, out res);
						break;
					}
					case TupleTypes.LegacyTupleStart: throw TupleParser.FailLegacyTupleNotSupported();
					case TupleTypes.EmbeddedTuple:
					{
						// extract the embedded tuple, and resume parsing
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
		public static (T1?, T2?, T3?, T4?, T5?, T6?, T7?) DeserializeValueTuple<T1, T2, T3, T4, T5, T6, T7>(ReadOnlySpan<byte> slice)
		{
			var res = default((T1?, T2?, T3?, T4?, T5?, T6?, T7?));
			if (slice.Length != 0)
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
						var bytes = TupleParser.ParseBytes(slice);
						var reader = new TupleReader(bytes.Span);
						//note: depth is 0 since the embedded tuple was encoded as top-level
						TupleEncoder.DecodeKey(ref reader, out res);
						break;
					}
					case TupleTypes.LegacyTupleStart: throw TupleParser.FailLegacyTupleNotSupported();
					case TupleTypes.EmbeddedTuple:
					{
						// extract the embedded tuple, and resume parsing
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
		public static (T1?, T2?, T3?, T4?, T5?, T6?, T7?, T8?) DeserializeValueTuple<T1, T2, T3, T4, T5, T6, T7, T8>(ReadOnlySpan<byte> slice)
		{
			var res = default((T1?, T2?, T3?, T4?, T5?, T6?, T7?, T8?));
			if (slice.Length != 0)
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
						var bytes = TupleParser.ParseBytes(slice);
						var reader = new TupleReader(bytes.Span);
						//note: depth is 0 since the embedded tuple was encoded as top-level
						TupleEncoder.DecodeKey(ref reader, out res);
						break;
					}
					case TupleTypes.LegacyTupleStart: throw TupleParser.FailLegacyTupleNotSupported();
					case TupleTypes.EmbeddedTuple:
					{
						// extract the embedded tuple, and resume parsing
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

		/// <summary>Deserialize a tuple segment into a Boolean</summary>
		/// <param name="slice">Slice that contains a single packed element</param>
		public static bool DeserializeBoolean(ReadOnlySpan<byte> slice)
		{
			if (slice.Length == 0) return false; //TODO: fail ?

			byte type = slice[0];

			// Booleans are usually encoded as integers, with 0 for False (<14>) and 1 for True (<15><01>)
			if (type is <= TupleTypes.IntPos8 and >= TupleTypes.IntNeg8)
			{
				//note: DeserializeInt64 handles most cases
				return 0 != DeserializeInt64(slice);
			}

			switch (type)
			{
				case TupleTypes.Nil:
				{ // null is false
					return false;
				}
				case TupleTypes.Bytes:
				{ // empty is false, all other is true
					return slice.Length != 2; // <01><00>
				}
				case TupleTypes.Utf8:
				{// empty is false, all other is true
					return slice.Length != 2; // <02><00>
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

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool? DeserializeBooleanNullable(Slice slice) => !IsNilSegment(slice) ? DeserializeBoolean(slice) : null;

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool? DeserializeBooleanNullable(ReadOnlySpan<byte> slice) => !IsNilSegment(slice) ? DeserializeBoolean(slice) : null;

		/// <summary>Deserialize a tuple segment into an Int16</summary>
		/// <param name="slice">Slice that contains a single packed element</param>
		public static sbyte DeserializeSByte(Slice slice) => checked((sbyte) DeserializeInt64(slice));

		/// <summary>Deserialize a tuple segment into an Int16</summary>
		/// <param name="slice">Slice that contains a single packed element</param>
		public static sbyte DeserializeSByte(ReadOnlySpan<byte> slice) => checked((sbyte) DeserializeInt64(slice));

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static sbyte? DeserializeSByteNullable(Slice slice) => !IsNilSegment(slice) ? DeserializeSByte(slice) : null;

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static sbyte? DeserializeSByteNullable(ReadOnlySpan<byte> slice) => !IsNilSegment(slice) ? DeserializeSByte(slice) : null;

		/// <summary>Deserialize a tuple segment into an Int16</summary>
		/// <param name="slice">Slice that contains a single packed element</param>
		public static short DeserializeInt16(Slice slice) => checked((short) DeserializeInt64(slice));

		/// <summary>Deserialize a tuple segment into an Int16</summary>
		/// <param name="slice">Slice that contains a single packed element</param>
		public static short DeserializeInt16(ReadOnlySpan<byte> slice) => checked((short) DeserializeInt64(slice));

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static short? DeserializeInt16Nullable(Slice slice) => !IsNilSegment(slice) ? DeserializeInt16(slice) : null;

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static short? DeserializeInt16Nullable(ReadOnlySpan<byte> slice) => !IsNilSegment(slice) ? DeserializeInt16(slice) : null;

		/// <summary>Deserialize a tuple segment into an Int32</summary>
		/// <param name="slice">Slice that contains a single packed element</param>
		public static int DeserializeInt32(Slice slice) => checked((int) DeserializeInt64(slice));

		/// <summary>Deserialize a tuple segment into an Int32</summary>
		/// <param name="slice">Slice that contains a single packed element</param>
		public static int DeserializeInt32(ReadOnlySpan<byte> slice) => checked((int) DeserializeInt64(slice));

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static int? DeserializeInt32Nullable(Slice slice) => !IsNilSegment(slice) ? DeserializeInt32(slice) : null;

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static int? DeserializeInt32Nullable(ReadOnlySpan<byte> slice) => !IsNilSegment(slice) ? DeserializeInt32(slice) : null;

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

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static long? DeserializeInt64Nullable(Slice slice) => !IsNilSegment(slice) ? DeserializeInt64(slice) : null;

		/// <summary>Deserialize a tuple segment into an Int64</summary>
		/// <param name="slice">Slice that contains a single packed element</param>
		public static long DeserializeInt64(ReadOnlySpan<byte> slice)
		{
			if (slice.Length == 0) return 0L; //TODO: fail ?

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

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static long? DeserializeInt64Nullable(ReadOnlySpan<byte> slice) => !IsNilSegment(slice) ? DeserializeInt64(slice) : null;

		/// <summary>Deserialize a tuple segment into an UInt32</summary>
		/// <param name="slice">Slice that contains a single packed element</param>
		public static byte DeserializeByte(Slice slice) => checked((byte) DeserializeUInt64(slice));

		/// <summary>Deserialize a tuple segment into an UInt32</summary>
		/// <param name="slice">Slice that contains a single packed element</param>
		public static byte DeserializeByte(ReadOnlySpan<byte> slice) => checked((byte) DeserializeUInt64(slice));

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static byte? DeserializeByteNullable(Slice slice) => !IsNilSegment(slice) ? DeserializeByte(slice) : null;

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static byte? DeserializeByteNullable(ReadOnlySpan<byte> slice) => !IsNilSegment(slice) ? DeserializeByte(slice) : null;

		/// <summary>Deserialize a tuple segment into an UInt32</summary>
		/// <param name="slice">Slice that contains a single packed element</param>
		public static ushort DeserializeUInt16(Slice slice) => checked((ushort) DeserializeUInt64(slice));

		/// <summary>Deserialize a tuple segment into an UInt32</summary>
		/// <param name="slice">Slice that contains a single packed element</param>
		public static ushort DeserializeUInt16(ReadOnlySpan<byte> slice) => checked((ushort) DeserializeUInt64(slice));

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static ushort? DeserializeUInt16Nullable(Slice slice) => !IsNilSegment(slice) ? DeserializeUInt16(slice) : null;

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static ushort? DeserializeUInt16Nullable(ReadOnlySpan<byte> slice) => !IsNilSegment(slice) ? DeserializeUInt16(slice) : null;

		/// <summary>Deserialize a slice into an UInt32</summary>
		/// <param name="slice">Slice that contains a single packed element</param>
		public static uint DeserializeUInt32(Slice slice) => checked((uint) DeserializeUInt64(slice));

		/// <summary>Deserialize a slice into an UInt32</summary>
		/// <param name="slice">Slice that contains a single packed element</param>
		public static uint DeserializeUInt32(ReadOnlySpan<byte> slice) => checked((uint) DeserializeUInt64(slice));

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static uint? DeserializeUInt32Nullable(Slice slice) => !IsNilSegment(slice) ? DeserializeUInt32(slice) : null;

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static uint? DeserializeUInt32Nullable(ReadOnlySpan<byte> slice) => !IsNilSegment(slice) ? DeserializeUInt32(slice) : null;

		/// <summary>Deserialize a tuple segment into an UInt64</summary>
		/// <param name="slice">Slice that contains a single packed element</param>
		public static ulong DeserializeUInt64(Slice slice)
		{
			if (slice.IsNullOrEmpty) return 0UL; //TODO: fail ?

			int type = slice[0];
			if (type <= TupleTypes.IntPos8)
			{
				if (type >= TupleTypes.IntZero) return checked((ulong) TupleParser.ParseInt64(type, slice));
				if (type >= TupleTypes.IntNeg8) throw new OverflowException(); // negative values

				switch (type)
				{
					case TupleTypes.Nil: return 0;
					case TupleTypes.Bytes: return ulong.Parse(TupleParser.ParseAscii(slice), CultureInfo.InvariantCulture);
					case TupleTypes.Utf8: return ulong.Parse(TupleParser.ParseUnicode(slice), CultureInfo.InvariantCulture);
				}
			}

			throw new FormatException($"Cannot convert tuple segment of type 0x{type:X} into an unsigned integer");
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static ulong? DeserializeUInt64Nullable(Slice slice) => !IsNilSegment(slice) ? DeserializeUInt64(slice) : null;

		/// <summary>Deserialize a tuple segment into an <see cref="UInt64"/></summary>
		/// <param name="slice">Slice that contains a single packed element</param>
		public static ulong DeserializeUInt64(ReadOnlySpan<byte> slice)
		{
			if (slice.Length == 0) return 0UL; //TODO: fail ?

			int type = slice[0];
			if (type <= TupleTypes.IntPos8)
			{
				if (type >= TupleTypes.IntZero) return checked((ulong) TupleParser.ParseInt64(type, slice));
				if (type >= TupleTypes.IntNeg8) throw new OverflowException(); // negative values

				switch (type)
				{
					case TupleTypes.Nil: return 0;
					case TupleTypes.Bytes: return ulong.Parse(TupleParser.ParseAscii(slice), CultureInfo.InvariantCulture);
					case TupleTypes.Utf8: return ulong.Parse(TupleParser.ParseUnicode(slice), CultureInfo.InvariantCulture);
				}
			}

			throw new FormatException($"Cannot convert tuple segment of type 0x{type:X} into an unsigned integer");
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static ulong? DeserializeUInt64Nullable(ReadOnlySpan<byte> slice) => !IsNilSegment(slice) ? DeserializeUInt64(slice) : null;

		/// <summary>Deserialize a tuple segment into a <see cref="Single"/></summary>
		/// <param name="slice">Slice that contains a single packed element</param>
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

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static float? DeserializeSingleNullable(Slice slice) => !IsNilSegment(slice) ? DeserializeSingle(slice) : null;

		/// <summary>Deserialize a tuple segment into a <see cref="Single"/></summary>
		/// <param name="slice">Slice that contains a single packed element</param>
		public static float DeserializeSingle(ReadOnlySpan<byte> slice)
		{
			if (slice.Length == 0) return 0;

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

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static float? DeserializeSingleNullable(ReadOnlySpan<byte> slice) => !IsNilSegment(slice) ? DeserializeSingle(slice) : null;

		/// <summary>Deserialize a tuple segment into a <see cref="Double"/></summary>
		/// <param name="slice">Slice that contains a single packed element</param>
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

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static double? DeserializeDoubleNullable(Slice slice) => !IsNilSegment(slice) ? DeserializeDouble(slice) : null;

		/// <summary>Deserialize a tuple segment into a <see cref="Double"/></summary>
		/// <param name="slice">Slice that contains a single packed element</param>
		public static double DeserializeDouble(ReadOnlySpan<byte> slice)
		{
			if (slice.Length == 0) return 0;

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

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static double? DeserializeDoubleNullable(ReadOnlySpan<byte> slice) => !IsNilSegment(slice) ? DeserializeDouble(slice) : null;

		/// <summary>Deserialize a tuple segment into a <see cref="DateTime"/> (UTC)</summary>
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

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static DateTime? DeserializeDateTimeNullable(Slice slice) => !IsNilSegment(slice) ? DeserializeDateTime(slice) : null;

		/// <summary>Deserialize a tuple segment into a <see cref="DateTime"/> (UTC)</summary>
		/// <param name="slice">Slice that contains a single packed element</param>
		/// <returns>DateTime in UTC</returns>
		/// <remarks>The returned DateTime will be in UTC, because the original TimeZone details are lost.</remarks>
		public static DateTime DeserializeDateTime(ReadOnlySpan<byte> slice)
		{
			if (slice.Length == 0) return DateTime.MinValue; //TODO: fail ?

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

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static DateTime? DeserializeDateTimeNullable(ReadOnlySpan<byte> slice) => !IsNilSegment(slice) ? DeserializeDateTime(slice) : null;

		/// <summary>Deserialize a tuple segment into a <see cref="DateTimeOffset"/></summary>
		/// <param name="slice">Slice that contains a single packed element</param>
		/// <returns>DateTime in UTC</returns>
		/// <remarks>The returned DateTimeOffset will be in UTC if converted value did not specify any offset.</remarks>
		public static DateTimeOffset DeserializeDateTimeOffset(Slice slice)
		{
			if (slice.IsNullOrEmpty) return DateTime.MinValue; //TODO: fail ?

			byte type = slice[0];

			switch(type)
			{
				case TupleTypes.Nil:
				{
					return DateTimeOffset.MinValue;
				}

				case TupleTypes.Utf8:
				{ // we only support ISO 8601 dates. For ex: YYYY-MM-DDTHH:MM:SS.fffff+xxxx"
					string str = TupleParser.ParseUnicode(slice);
					return DateTimeOffset.Parse(str, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);
				}

				case TupleTypes.Double:
				{ // Number of days since Epoch
					const long UNIX_EPOCH_TICKS = 621355968000000000L;
					//note: we can't user TimeSpan.FromDays(...) because it rounds to the nearest millisecond!
					long ticks = UNIX_EPOCH_TICKS + (long)(TupleParser.ParseDouble(slice) * TimeSpan.TicksPerDay);
					return new DateTimeOffset(new DateTime(ticks, DateTimeKind.Utc));
				}

				case TupleTypes.Decimal:
				{
					const long UNIX_EPOCH_TICKS = 621355968000000000L;
					//note: we can't user TimeSpan.FromDays(...) because it rounds to the nearest millisecond!
					long ticks = UNIX_EPOCH_TICKS + (long)(TupleParser.ParseDecimal(slice) * TimeSpan.TicksPerDay);
					return new DateTimeOffset(new DateTime(ticks, DateTimeKind.Utc));
				}
			}

			// If we have an integer, we consider it to be a number of Ticks (Windows Only)
			if (type <= TupleTypes.IntPos8 && type >= TupleTypes.IntNeg8)
			{
				return new DateTimeOffset(new DateTime(DeserializeInt64(slice), DateTimeKind.Utc));
			}

			throw new FormatException($"Cannot convert tuple segment of type 0x{type:X} into a DateTimeOffset");
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static DateTimeOffset? DeserializeDateTimeOffsetNullable(Slice slice) => !IsNilSegment(slice) ? DeserializeDateTimeOffset(slice) : null;

		/// <summary>Deserialize a tuple segment into a <see cref="DateTimeOffset"/></summary>
		/// <param name="slice">Slice that contains a single packed element</param>
		/// <returns>DateTime in UTC</returns>
		/// <remarks>The returned DateTimeOffset will be in UTC if converted value did not specify any offset.</remarks>
		public static DateTimeOffset DeserializeDateTimeOffset(ReadOnlySpan<byte> slice)
		{
			if (slice.Length == 0) return DateTime.MinValue; //TODO: fail ?

			byte type = slice[0];

			switch(type)
			{
				case TupleTypes.Nil:
				{
					return DateTimeOffset.MinValue;
				}

				case TupleTypes.Utf8:
				{ // we only support ISO 8601 dates. For ex: YYYY-MM-DDTHH:MM:SS.fffff+xxxx"
					string str = TupleParser.ParseUnicode(slice);
					return DateTimeOffset.Parse(str, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);
				}

				case TupleTypes.Double:
				{ // Number of days since Epoch
					const long UNIX_EPOCH_TICKS = 621355968000000000L;
					//note: we can't user TimeSpan.FromDays(...) because it rounds to the nearest millisecond!
					long ticks = UNIX_EPOCH_TICKS + (long)(TupleParser.ParseDouble(slice) * TimeSpan.TicksPerDay);
					return new DateTimeOffset(new DateTime(ticks, DateTimeKind.Utc));
				}

				case TupleTypes.Decimal:
				{
					const long UNIX_EPOCH_TICKS = 621355968000000000L;
					//note: we can't user TimeSpan.FromDays(...) because it rounds to the nearest millisecond!
					long ticks = UNIX_EPOCH_TICKS + (long)(TupleParser.ParseDecimal(slice) * TimeSpan.TicksPerDay);
					return new DateTimeOffset(new DateTime(ticks, DateTimeKind.Utc));
				}
			}

			// If we have an integer, we consider it to be a number of Ticks (Windows Only)
			if (type <= TupleTypes.IntPos8 && type >= TupleTypes.IntNeg8)
			{
				return new DateTimeOffset(new DateTime(DeserializeInt64(slice), DateTimeKind.Utc));
			}

			throw new FormatException($"Cannot convert tuple segment of type 0x{type:X} into a DateTimeOffset");
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static DateTimeOffset? DeserializeDateTimeOffsetNullable(ReadOnlySpan<byte> slice) => !IsNilSegment(slice) ? DeserializeDateTimeOffset(slice) : null;

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

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static TimeSpan? DeserializeTimeSpanNullable(Slice slice) => !IsNilSegment(slice) ? DeserializeTimeSpan(slice) : null;

		/// <summary>Deserialize a tuple segment into a TimeSpan</summary>
		/// <param name="slice">Slice that contains a single packed element</param>
		public static TimeSpan DeserializeTimeSpan(ReadOnlySpan<byte> slice)
		{
			if (slice.Length == 0) return TimeSpan.Zero; //TODO: fail ?

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

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static TimeSpan? DeserializeTimeSpanNullable(ReadOnlySpan<byte> slice) => !IsNilSegment(slice) ? DeserializeTimeSpan(slice) : null;

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

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static char? DeserializeCharNullable(Slice slice) => !IsNilSegment(slice) ? DeserializeChar(slice) : null;

		/// <summary>Deserialize a tuple segment into a Unicode character</summary>
		/// <param name="slice">Slice that contains a single packed element</param>
		public static char DeserializeChar(ReadOnlySpan<byte> slice)
		{
			if (slice.Length == 0) return '\0';

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

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static char? DeserializeCharNullable(ReadOnlySpan<byte> slice) => !IsNilSegment(slice) ? DeserializeChar(slice) : null;

		/// <summary>Deserialize a tuple segment into a Unicode string</summary>
		/// <param name="slice">Slice that contains a single packed element</param>
		public static string? DeserializeString(Slice slice)
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

		/// <summary>Deserialize a tuple segment into a Unicode string</summary>
		/// <param name="slice">Slice that contains a single packed element</param>
		public static string? DeserializeString(ReadOnlySpan<byte> slice)
		{
			if (slice.Length == 0) return null;

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

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static Guid? DeserializeGuidNullable(Slice slice) => !IsNilSegment(slice) ? DeserializeGuid(slice) : null;

		/// <summary>Deserialize a tuple segment into Guid</summary>
		/// <param name="slice">Slice that contains a single packed element</param>
		public static Guid DeserializeGuid(ReadOnlySpan<byte> slice)
		{
			if (slice.Length == 0) return Guid.Empty;

			return slice[0] switch
			{
				TupleTypes.Nil => Guid.Empty,
				TupleTypes.Bytes => Guid.Parse(TupleParser.ParseAscii(slice)),
				TupleTypes.Utf8 => Guid.Parse(TupleParser.ParseUnicode(slice)),
				TupleTypes.Uuid128 => TupleParser.ParseGuid(slice),
				//REVIEW: should we allow converting a Uuid64 into a Guid? This looks more like a bug than an expected behavior...
				_ => throw new FormatException($"Cannot convert tuple segment of type 0x{slice[0]:X02} into a System.Guid")
			};
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static Guid? DeserializeGuidNullable(ReadOnlySpan<byte> slice) => !IsNilSegment(slice) ? DeserializeGuid(slice) : null;

		/// <summary>Deserialize a tuple segment into 128-bit UUID</summary>
		/// <param name="slice">Slice that contains a single packed element</param>
		public static Uuid128 DeserializeUuid128(Slice slice)
		{
			if (slice.IsNullOrEmpty) return Uuid128.Empty;

			int type = slice[0];
			switch (type)
			{
				case TupleTypes.Nil:
				{ // null is empty
					return Uuid128.Empty;
				}
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

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static Uuid128? DeserializeUuid128Nullable(Slice slice) => !IsNilSegment(slice) ? DeserializeUuid128(slice) : null;

		/// <summary>Deserialize a tuple segment into 128-bit UUID</summary>
		/// <param name="slice">Slice that contains a single packed element</param>
		public static Uuid128 DeserializeUuid128(ReadOnlySpan<byte> slice)
		{
			if (slice.Length == 0) return Uuid128.Empty;

			int type = slice[0];
			switch (type)
			{
				case TupleTypes.Nil:
				{
					return Uuid128.Empty;
				}
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

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static Uuid128? DeserializeUuid128Nullable(ReadOnlySpan<byte> slice) => !IsNilSegment(slice) ? DeserializeUuid128(slice) : null;

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

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static Uuid96? DeserializeUuid96Nullable(Slice slice) => !IsNilSegment(slice) ? DeserializeUuid96(slice) : null;

		/// <summary>Deserialize a tuple segment into a 96-bit UUID</summary>
		/// <param name="slice">Slice that contains a single packed element</param>
		public static Uuid96 DeserializeUuid96(ReadOnlySpan<byte> slice)
		{
			if (slice.Length == 0) return Uuid96.Empty;

			int type = slice[0];
			switch (type)
			{
				case TupleTypes.Nil:
				{
					return Uuid96.Empty;
				}
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

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static Uuid96? DeserializeUuid96Nullable(ReadOnlySpan<byte> slice) => !IsNilSegment(slice) ? DeserializeUuid96(slice) : null;

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

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static Uuid80? DeserializeUuid80Nullable(Slice slice) => !IsNilSegment(slice) ? DeserializeUuid80(slice) : null;

		/// <summary>Deserialize a tuple segment into a 80-bit UUID</summary>
		/// <param name="slice">Slice that contains a single packed element</param>
		public static Uuid80 DeserializeUuid80(ReadOnlySpan<byte> slice)
		{
			if (slice.Length == 0) return Uuid80.Empty;

			int type = slice[0];

			switch (type)
			{
				case TupleTypes.Nil:
				{
					return Uuid80.Empty;
				}
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

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static Uuid80? DeserializeUuid80Nullable(ReadOnlySpan<byte> slice) => !IsNilSegment(slice) ? DeserializeUuid80(slice) : null;

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

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static Uuid64? DeserializeUuid64Nullable(Slice slice) => !IsNilSegment(slice) ? DeserializeUuid64(slice) : null;

		/// <summary>Deserialize a tuple segment into 64-bit UUID</summary>
		/// <param name="slice">Slice that contains a single packed element</param>
		public static Uuid64 DeserializeUuid64(ReadOnlySpan<byte> slice)
		{
			if (slice.Length == 0) return Uuid64.Empty;

			int type = slice[0];

			switch (type)
			{
				case TupleTypes.Nil:
				{
					return Uuid64.Empty;
				}
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

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static Uuid64? DeserializeUuid64Nullable(ReadOnlySpan<byte> slice) => !IsNilSegment(slice) ? DeserializeUuid64(slice) : null;

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

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static VersionStamp? DeserializeVersionStampNullable(Slice slice) => !IsNilSegment(slice) ? DeserializeVersionStamp(slice) : null;

		public static VersionStamp DeserializeVersionStamp(ReadOnlySpan<byte> slice)
		{
			if (slice.Length == 0) return default;

			int type = slice[0];
			return type switch
			{
				TupleTypes.Nil => default,
				TupleTypes.VersionStamp80 or TupleTypes.VersionStamp96 => VersionStamp.TryParse(slice.Slice(1), out var stamp) ? stamp : throw new FormatException("Cannot convert malformed tuple segment into a VersionStamp"),
				_ => throw new FormatException($"Cannot convert tuple segment of type 0x{type:X} into a VersionStamp")
			};
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static VersionStamp? DeserializeVersionStampNullable(ReadOnlySpan<byte> slice) => !IsNilSegment(slice) ? DeserializeVersionStamp(slice) : null;

		/// <summary>Deserialize a tuple segment into an <see cref="System.Net.IPAddress"/></summary>
		/// <param name="slice">Slice that contains a single packed element</param>
		public static System.Net.IPAddress? DeserializeIpAddress(Slice slice)
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
				Contract.Debug.Assert(value >= 0 && value <= uint.MaxValue);
				return new System.Net.IPAddress(value);
			}

			throw new FormatException($"Cannot convert tuple segment of type 0x{type:X} into System.Net.IPAddress");
		}

		/// <summary>Deserialize a tuple segment into an <see cref="System.Net.IPAddress"/></summary>
		/// <param name="slice">Slice that contains a single packed element</param>
		public static System.Net.IPAddress? DeserializeIpAddress(ReadOnlySpan<byte> slice)
		{
			if (slice.Length == 0) return null;

			int type = slice[0];

			switch (type)
			{
				case TupleTypes.Nil:
				{
					return null;
				}
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
					// we have a RoS<byte> but IPAddress.Parse wants a RoS<char>
					// => we assume that the slice contains an ASCII-encoded address, so we will simply convert it into span "as is"
					return new System.Net.IPAddress(slice.Slice(1).ToArray());
				}
			}

			if (type >= TupleTypes.IntPos1 && type <= TupleTypes.IntPos4)
			{ // could be an IPv4 encoded as a 32-bit unsigned integer
				var value = TupleParser.ParseInt64(type, slice);
				Contract.Debug.Assert(value >= 0 && value <= uint.MaxValue);
				return new System.Net.IPAddress(value);
			}

			throw new FormatException($"Cannot convert tuple segment of type 0x{type:X} into System.Net.IPAddress");
		}

		/// <summary>Unpack a tuple from a buffer</summary>
		/// <param name="buffer">Slice that contains the packed representation of a tuple with zero or more elements</param>
		/// <param name="embedded"></param>
		/// <returns>Decoded tuple</returns>
		internal static SlicedTuple Unpack(Slice buffer, bool embedded)
		{
			var reader = new TupleReader(buffer.Span, embedded ? 1 : 0);
			if (!TryUnpack(ref reader, out var tuple, out var error))
			{
				if (error != null) throw error;
				return SlicedTuple.Empty;
			}
			return tuple.ToTuple(buffer);
		}

		/// <summary>Unpack a tuple from a buffer</summary>
		/// <param name="buffer">Slice that contains the packed representation of a tuple with zero or more elements</param>
		/// <param name="embedded"></param>
		/// <param name="tuple"></param>
		/// <returns>Decoded tuple</returns>
		internal static bool TryUnpack(Slice buffer, bool embedded, [MaybeNullWhen(false)] out SlicedTuple tuple, out Exception? error)
		{
			var reader = new TupleReader(buffer.Span, embedded ? 1 : 0);
			if (!TryUnpack(ref reader, out var st, out error))
			{
				tuple = null;
				return false;
			}

			tuple = st.ToTuple(buffer);
			error = null;
			return true;
		}

		/// <summary>Unpack a tuple from a buffer</summary>
		/// <param name="reader">Reader positioned on the start of the packed representation of a tuple with zero or more elements</param>
		/// <returns>Decoded tuple</returns>
		internal static SpanTuple Unpack(scoped ref TupleReader reader)
		{
			// most tuples will probably fit within (prefix, sub-prefix, id, key) so pre-allocating with 4 should be ok...
			var items = new Range[4];

			int p = 0;
			while (true)
			{
				if (!TupleParser.TryParseNext(ref reader, out var item, out var error))
				{
					if (error != null) throw error;
					break;
				}

				if (p >= items.Length)
				{
					// note: do not grow exponentially, because tuples will never but very large...
					Array.Resize(ref items, p + 4);
				}
				items[p++] = item;
			}

			if (reader.HasMore)
			{
				throw new FormatException("Parsing of tuple failed failed before reaching the end of the key");
			}

			return new SpanTuple(reader.Input, p == 0 ? [] : items.AsSpan(0, p));
		}

		internal static bool TryUnpack(scoped ref TupleReader reader, out SpanTuple tuple, out Exception? error)
		{
			// most tuples will probably fit within (prefix, sub-prefix, id, key) so pre-allocating with 4 should be ok...
			var items = new Range[4];

			int p = 0;
			while (true)
			{
				if (!TupleParser.TryParseNext(ref reader, out var token, out error))
				{
					if (error != null)
					{
						tuple = default;
						return false;
					}
					break;
				}

				if (p >= items.Length)
				{
					// note: do not grow exponentially, because tuples will never but very large...
					Array.Resize(ref items, p + 4);
				}
				items[p++] = token;
			}

			if (reader.HasMore)
			{
				tuple = default;
				return false;
			}

			tuple = new SpanTuple(reader.Input, p == 0 ? Array.Empty<Range>() : items.AsSpan(0, p));
			error = null;
			return true;
		}

		/// <summary>Ensure that a slice is a packed tuple that contains a single and valid element</summary>
		/// <param name="buffer">Slice that should contain the packed representation of a singleton tuple</param>
		/// <returns>Decoded slice of the single element in the singleton tuple</returns>
		public static Range UnpackSingle(ReadOnlySpan<byte> buffer)
		{
			var reader = new TupleReader(buffer);
			if (!TupleParser.TryParseNext(ref reader, out var token, out var error))
			{
				if (error != null) throw error;
			}
			if (token.Equals(default) || reader.HasMore)
			{
				throw new FormatException("Parsing of singleton tuple failed before reaching the end of the key");
			}
			return token;
		}

		/// <summary>Ensure that a slice is a packed tuple that contains a single and valid element</summary>
		/// <param name="buffer">Slice that should contain the packed representation of a singleton tuple</param>
		/// <returns>Decoded slice of the single element in the singleton tuple</returns>
		public static bool TryUnpackSingle(ReadOnlySpan<byte> buffer, out Range token)
		{
			var reader = new TupleReader(buffer);
			if (!TupleParser.TryParseNext(ref reader, out token, out _))
			{ // failed to parse
				return false;
			}
			if (reader.HasMore)
			{ // there are more than one item in the tuple
				return false;
			}

			return true;
		}

		/// <summary>Only returns the first item of a packed tuple</summary>
		/// <param name="buffer">Slice that contains the packed representation of a tuple with one or more elements</param>
		/// <returns>Raw slice corresponding to the first element of the tuple</returns>
		public static Range UnpackFirst(ReadOnlySpan<byte> buffer)
		{
			var reader = new TupleReader(buffer);

			if (!TupleParser.TryParseNext(ref reader, out var token, out var error))
			{
				if (error != null) throw error;
				throw new FormatException("Parsing of tuple failed failed before reaching the end of the key");
			}
			Contract.Debug.Ensures(!token.Equals(default));
			return token;
		}

		/// <summary>Only returns the first N items of a packed tuple, without decoding them.</summary>
		/// <param name="buffer">Slice that contains the packed representation of a tuple with at least 3 elements</param>
		/// <param name="tokens">Raw slice corresponding to the third element from the end of the tuple</param>
		/// <param name="error">Receives an exception if the parsing failed</param>
		/// <returns><c>true</c> if the buffer was successfully unpacked</returns>
		public static bool TryUnpackFirst(ReadOnlySpan<byte> buffer, Span<Range> tokens, out Exception? error)
		{
			var reader = new TupleReader(buffer);

			for (int i = 0; i < tokens.Length; i++)
			{
				if (!TupleParser.TryParseNext(ref reader, out tokens[i], out error))
				{
					tokens.Clear();
					error ??= new InvalidOperationException("Tuple has less elements than expected.");
					return false;
				}
			}

			error = null;
			return true; 
		}

		/// <summary>Only returns the last item of a packed tuple</summary>
		/// <param name="buffer">Slice that contains the packed representation of a tuple with one or more elements</param>
		/// <returns>Raw slice corresponding to the last element of the tuple</returns>
		public static Range UnpackLast(ReadOnlySpan<byte> buffer)
		{
			Range item = default;
			var reader = new TupleReader(buffer);
			while (true)
			{
				if (!TupleParser.TryParseNext(ref reader, out var token, out var error))
				{
					if (error != null) throw error;
					break;
				}
				item = token;
			}

			if (item.Equals(default) || reader.HasMore)
			{
				throw new FormatException("Parsing of tuple failed failed before reaching the end of the key");
			}

			return item;
		}

		/// <summary>Only returns the last N items of a packed tuple, without decoding them.</summary>
		/// <param name="buffer">Slice that contains the packed representation of a tuple with at least 3 elements</param>
		/// <param name="tokens">Array that will receive the last N raw slice corresponding to the each of the last N elements</param>
		/// <param name="error">Receive an exception if the parsing failed</param>
		/// <returns><c>true</c> if the buffer was successfully unpacked</returns>
		public static bool TryUnpackLast(ReadOnlySpan<byte> buffer, Span<Range> tokens, out Exception? error)
		{
			var reader = new TupleReader(buffer);

			int n = 0;
			var tail = tokens.Slice(1);

			while (true)
			{
				if (!TupleParser.TryParseNext(ref reader, out var token, out error))
				{
					if (error != null)
					{ // malformed token
						tokens.Clear();
						return false;
					}
					// no more tokens
					break;
				}

				if (n < tokens.Length)
				{
					tokens[n] = token;
				}
				else
				{
					// slide to the left
					tail.CopyTo(tokens);
					// append last
					tokens[^1] = token;
				}
				++n;
			}

			if (n < tokens.Length || reader.HasMore)
			{ // tuple has less elements than expected or has extra bytes
				error = new InvalidOperationException("Tuple has less elements than expected.");
				tokens.Clear();
				return false;
			}

			error = null;
			return true;
		}

		#endregion

	}

}
