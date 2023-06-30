#region Copyright (c) 2005-2023 Doxense SAS
// All rights reserved.
// 
// Redistribution and use in source and binary forms, with or without
// modification, are permitted provided that the following conditions are met:
// 	* Redistributions of source code must retain the above copyright
// 	  notice, this list of conditions and the following disclaimer.
// 	* Redistributions in binary form must reproduce the above copyright
// 	  notice, this list of conditions and the following disclaimer in the
// 	  documentation and/or other materials provided with the distribution.
// 	* Neither the name of Doxense nor the
// 	  names of its contributors may be used to endorse or promote products
// 	  derived from this software without specific prior written permission.
// 
// THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS "AS IS" AND
// ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED
// WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE
// DISCLAIMED. IN NO EVENT SHALL DOXENSE BE LIABLE FOR ANY
// DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES
// (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES;
// LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND
// ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT
// (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS
// SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
#endregion

namespace Doxense.Serialization.Json
{
	using System;
	using System.Runtime.CompilerServices;
	using System.Text;
	using JetBrains.Annotations;

	public abstract partial class JsonValue
	{

		#region String...

		//REVIEW: est-ce qu'on ne devrait pas caster en JsonString, plutot que JsonValue?

		[Pure]
		public static implicit operator JsonValue(string? value)
		{
			return JsonString.Return(value);
		}

		[Pure]
		public static explicit operator string?(JsonValue? value)
		{
			//note: JsonNull.Null.ToString() retourne String.Empty, ce qui ne va pas dans notre cas!
			// on est donc obligé d'utiliser ToStringOrDefault() qui peut retourner null
			return (value ?? JsonNull.Null).ToStringOrDefault();
		}

		[Pure]
		public static implicit operator JsonValue(StringBuilder? value)
		{
			return JsonString.Return(value);
		}

		[Pure]
		public static implicit operator JsonValue(char[]? value)
		{
			if (value == null) return JsonNull.Null;
			return JsonString.Return(value, 0, value.Length);
		}

		//TODO: ajouter aussi Utf8String et AsciiString?

		#endregion

		#region Byte[]

		[Pure]
		public static implicit operator JsonValue(byte[]? value)
		{
			return value == null ? JsonNull.Null : JsonString.Return(value);
		}

		[ Pure]
		public static explicit operator byte[]?(JsonValue? value)
		{
			if (value == null || value.IsNull) return default(byte[]);

			if (value.Type == JsonType.String)
			{
				return Convert.FromBase64String(((JsonString)value).Value);
			}

			throw Errors.JsonConversionNotSupported(value, typeof(byte[]));
		}

		#endregion

		#region Slice

		[Pure]
		public static implicit operator JsonValue(Slice value)
		{
			return JsonString.Return(value);
		}

		[Pure]
		public static explicit operator Slice(JsonValue? value)
		{
			if (value.IsNullOrMissing()) return default(Slice);

			if (value.Type == JsonType.String)
			{
				return Slice.FromBase64(((JsonString)value).Value);
			}

			throw Errors.JsonConversionNotSupported(value, typeof(Slice));
		}

		#endregion

		#region ArraySegment<byte>

		[Pure]
		public static implicit operator JsonValue(ArraySegment<byte> value)
		{
			return JsonString.Return(value);
		}

		[Pure]
		public static explicit operator ArraySegment<byte>(JsonValue? value)
		{
			if (value.IsNullOrMissing()) return default(ArraySegment<byte>);

			if (value.Type == JsonType.String)
			{
				return new ArraySegment<byte>(Convert.FromBase64String(((JsonString)value).Value));
			}

			throw Errors.JsonConversionNotSupported(value, typeof(ArraySegment<byte>));
		}

		#endregion

		#region Boolean

		[Pure]
		public static implicit operator JsonValue(bool value)
		{
			return JsonBoolean.Return(value);
		}

		[Pure]
		public static explicit operator bool(JsonValue? value)
		{
			return (value ?? JsonNull.Null).ToBoolean();
		}

		[Pure]
		public static implicit operator JsonValue(bool? value)
		{
			return JsonBoolean.Return(value);
		}

		[Pure]
		public static explicit operator bool?(JsonValue? value)
		{
			return (value ?? JsonNull.Null).ToBooleanOrDefault();
		}

		#endregion

		#region Char

		[Pure]
		public static implicit operator JsonValue(char value)
		{
			return JsonString.Return(value);
		}

		[Pure]
		public static explicit operator char(JsonValue? value)
		{
			return (value ?? JsonNull.Null).ToChar();
		}

		[Pure]
		public static implicit operator JsonValue(char? value)
		{
			return JsonString.Return(value);
		}

		[Pure]
		public static explicit operator char?(JsonValue? value)
		{
			return (value ?? JsonNull.Null).ToCharOrDefault();
		}

		#endregion

		#region SByte

		[Pure]
		public static implicit operator JsonValue(sbyte value)
		{
			return JsonNumber.Return(value);
		}

		[Pure]
		public static explicit operator sbyte(JsonValue? value)
		{
			return (value ?? JsonNull.Null).ToSByte();
		}

		[Pure]
		public static implicit operator JsonValue(sbyte? value)
		{
			return JsonNumber.Return(value);
		}

		[Pure]
		public static explicit operator sbyte?(JsonValue? value)
		{
			return (value ?? JsonNull.Null).ToSByteOrDefault();
		}

		#endregion

		#region Byte

		[Pure]
		public static implicit operator JsonValue(byte value)
		{
			return JsonNumber.Return(value);
		}

		[Pure]
		public static explicit operator byte(JsonValue? value)
		{
			return (value ?? JsonNull.Null).ToByte();
		}

		[Pure]
		public static implicit operator JsonValue(byte? value)
		{
			return JsonNumber.Return(value);
		}

		[Pure]
		public static explicit operator byte?(JsonValue? value)
		{
			return (value ?? JsonNull.Null).ToByteOrDefault();
		}

		#endregion

		#region Int16

		[Pure]
		public static implicit operator JsonValue(short value)
		{
			return JsonNumber.Return(value);
		}

		[Pure]
		public static explicit operator short(JsonValue? value)
		{
			return (value ?? JsonNull.Null).ToInt16();
		}

		[Pure]
		public static implicit operator JsonValue(short? value)
		{
			return JsonNumber.Return(value);
		}

		[Pure]
		public static explicit operator short?(JsonValue? value)
		{
			return (value ?? JsonNull.Null).ToInt16OrDefault();
		}

		#endregion

		#region UInt16

		[Pure]
		public static implicit operator JsonValue(ushort value)
		{
			return JsonNumber.Return(value);
		}

		[Pure]
		public static explicit operator ushort(JsonValue? value)
		{
			return (value ?? JsonNull.Null).ToUInt16();
		}

		[Pure]
		public static implicit operator JsonValue(ushort? value)
		{
			return JsonNumber.Return(value);
		}

		[Pure]
		public static explicit operator ushort?(JsonValue? value)
		{
			return (value ?? JsonNull.Null).ToUInt16OrDefault();
		}

		#endregion

		#region Int32

		//REVIEW: est-ce qu'on ne devrait pas caster en JsonNumber, plutot que JsonValue?
		// même remarque pour les autres types integers/float

		[Pure]
		public static implicit operator JsonValue(int value)
		{
			return JsonNumber.Return(value);
		}

		[Pure]
		public static explicit operator int(JsonValue? value)
		{
			return (value ?? JsonNull.Null).ToInt32();
		}

		[Pure]
		public static implicit operator JsonValue(int? value)
		{
			return JsonNumber.Return(value);
		}

		[Pure]
		public static explicit operator int?(JsonValue? value)
		{
			return (value ?? JsonNull.Null).ToInt32OrDefault();
		}

		#endregion

		#region UInt32

		[Pure]
		public static implicit operator JsonValue(uint value)
		{
			return JsonNumber.Return(value);
		}

		[Pure]
		public static explicit operator uint(JsonValue? value)
		{
			return (value ?? JsonNull.Null).ToUInt32();
		}

		[Pure]
		public static implicit operator JsonValue(uint? value)
		{
			return JsonNumber.Return(value);
		}

		[Pure]
		public static explicit operator uint?(JsonValue? value)
		{
			return (value ?? JsonNull.Null).ToUInt32OrDefault();
		}

		#endregion

		#region Int64

		[Pure]
		public static implicit operator JsonValue(long value)
		{
			return JsonNumber.Return(value);
		}

		[Pure]
		public static explicit operator long(JsonValue? value)
		{
			return (value ?? JsonNull.Null).ToInt64();
		}

		[Pure]
		public static implicit operator JsonValue(long? value)
		{
			return JsonNumber.Return(value);
		}

		[Pure]
		public static explicit operator long?(JsonValue? value)
		{
			return (value ?? JsonNull.Null).ToInt64OrDefault();
		}

		#endregion

		#region UInt64

		[Pure]
		public static implicit operator JsonValue(ulong value)
		{
			return JsonNumber.Return(value);
		}

		[Pure]
		public static explicit operator ulong(JsonValue? value)
		{
			return (value ?? JsonNull.Null).ToUInt64();
		}

		[Pure]
		public static implicit operator JsonValue(ulong? value)
		{
			return JsonNumber.Return(value);
		}

		[Pure]
		public static explicit operator ulong?(JsonValue? value)
		{
			return (value ?? JsonNull.Null).ToUInt64OrDefault();
		}

		#endregion

		#region Single

		[Pure]
		public static implicit operator JsonValue(float value)
		{
			return JsonNumber.Return(value);
		}

		[Pure]
		public static explicit operator float(JsonValue? value)
		{
			return (value ?? JsonNull.Null).ToSingle();
		}

		[Pure]
		public static implicit operator JsonValue(float? value)
		{
			return JsonNumber.Return(value);
		}

		[Pure]
		public static explicit operator float?(JsonValue? value)
		{
			return (value ?? JsonNull.Null).ToSingleOrDefault();
		}

		#endregion

		#region Double

		[Pure]
		public static implicit operator JsonValue(double value)
		{
			return JsonNumber.Return(value);
		}

		[Pure]
		public static explicit operator double(JsonValue? value)
		{
			return (value ?? JsonNull.Null).ToDouble();
		}

		[Pure]
		public static implicit operator JsonValue(double? value)
		{
			return JsonNumber.Return(value);
		}

		[Pure]
		public static explicit operator double?(JsonValue? value)
		{
			return (value ?? JsonNull.Null).ToDoubleOrDefault();
		}

		#endregion

		#region Decimal

		[Pure]
		public static implicit operator JsonValue(decimal value)
		{
			return JsonNumber.Return(value);
		}

		[Pure]
		public static explicit operator decimal(JsonValue? value)
		{
			return (value ?? JsonNull.Null).ToDecimal();
		}

		[Pure]
		public static implicit operator JsonValue(decimal? value)
		{
			return JsonNumber.Return(value);
		}

		[Pure]
		public static explicit operator decimal?(JsonValue? value)
		{
			return (value ?? JsonNull.Null).ToDecimalOrDefault();
		}

		#endregion

		#region Guid...

		[Pure]
		public static implicit operator JsonValue(Guid value)
		{
			return JsonString.Return(value);
		}

		[Pure]
		public static explicit operator Guid(JsonValue? value)
		{
			return (value ?? JsonNull.Null).ToGuid();
		}

		[Pure]
		public static implicit operator JsonValue(Guid? value)
		{
			return JsonString.Return(value);
		}

		[Pure]
		public static explicit operator Guid?(JsonValue? value)
		{
			return (value ?? JsonNull.Null).ToGuidOrDefault();
		}

		[Pure]
		public static implicit operator JsonValue(Uuid128 value)
		{
			return JsonString.Return(value);
		}

		[Pure]
		public static explicit operator Uuid128(JsonValue? value)
		{
			return (value ?? JsonNull.Null).ToUuid128();
		}

		[Pure]
		public static implicit operator JsonValue(Uuid128? value)
		{
			return JsonString.Return(value);
		}

		[Pure]
		public static explicit operator Uuid128? (JsonValue? value)
		{
			return (value ?? JsonNull.Null).ToUuid128OrDefault();
		}

		[Pure]
		public static implicit operator JsonValue(Uuid96 value)
		{
			return JsonString.Return(value);
		}

		[Pure]
		public static explicit operator Uuid96(JsonValue? value)
		{
			return (value ?? JsonNull.Null).ToUuid96();
		}

		[Pure]
		public static implicit operator JsonValue(Uuid96? value)
		{
			return JsonString.Return(value);
		}

		[Pure]
		public static explicit operator Uuid96? (JsonValue? value)
		{
			return (value ?? JsonNull.Null).ToUuid96OrDefault();
		}

		[Pure]
		public static implicit operator JsonValue(Uuid80 value)
		{
			return JsonString.Return(value);
		}

		[Pure]
		public static explicit operator Uuid80(JsonValue? value)
		{
			return (value ?? JsonNull.Null).ToUuid80();
		}

		[Pure]
		public static implicit operator JsonValue(Uuid80? value)
		{
			return JsonString.Return(value);
		}

		[Pure]
		public static explicit operator Uuid80? (JsonValue? value)
		{
			return (value ?? JsonNull.Null).ToUuid80OrDefault();
		}

		[Pure]
		public static implicit operator JsonValue(Uuid64 value)
		{
			return JsonString.Return(value);
		}

		[Pure]
		public static explicit operator Uuid64(JsonValue? value)
		{
			return (value ?? JsonNull.Null).ToUuid64();
		}

		[Pure]
		public static implicit operator JsonValue(Uuid64? value)
		{
			return JsonString.Return(value);
		}

		[Pure]
		public static explicit operator Uuid64? (JsonValue? value)
		{
			return (value ?? JsonNull.Null).ToUuid64OrDefault();
		}

		#endregion

		#region Uri...

		[Pure]
		public static implicit operator JsonValue(Uri? value)
		{
			return JsonString.Return(value);
		}

		[Pure]
		public static explicit operator Uri?(JsonValue? value)
		{
			return value.IsNullOrMissing() ? default(Uri) : new Uri(value.ToString());
		}

		#endregion

		#region TimeSpan

		[Pure]
		public static implicit operator JsonValue(TimeSpan value)
		{
			return JsonNumber.Return(value);
		}

		[Pure]
		public static explicit operator TimeSpan(JsonValue? value)
		{
			return (value ?? JsonNull.Null).ToTimeSpan();
		}

		[Pure]
		public static implicit operator JsonValue(TimeSpan? value)
		{
			return JsonNumber.Return(value);
		}

		[Pure]
		public static explicit operator TimeSpan?(JsonValue? value)
		{
			return (value ?? JsonNull.Null).ToTimeSpanOrDefault();
		}

		#endregion

		#region DateTime

		[Pure]
		public static implicit operator JsonValue(DateTime value)
		{
			return JsonDateTime.Return(value);
		}

		[Pure]
		public static explicit operator DateTime(JsonValue? value)
		{
			return (value ?? JsonNull.Null).ToDateTime();
		}

		[Pure]
		public static implicit operator JsonValue(DateTime? value)
		{
			return JsonDateTime.Return(value);
		}

		[Pure]
		public static explicit operator DateTime?(JsonValue? value)
		{
			return (value ?? JsonNull.Null).ToDateTimeOrDefault();
		}

		#endregion

		#region DateTimeOffset

		[Pure]
		public static implicit operator JsonValue(DateTimeOffset value)
		{
			return JsonDateTime.Return(value);
		}

		[Pure]
		public static explicit operator DateTimeOffset(JsonValue? value)
		{
			return (value ?? JsonNull.Null).ToDateTimeOffset();
		}

		[Pure]
		public static implicit operator JsonValue(DateTimeOffset? value)
		{
			return JsonDateTime.Return(value);
		}

		[Pure]
		public static explicit operator DateTimeOffset?(JsonValue? value)
		{
			return (value ?? JsonNull.Null).ToDateTimeOffsetOrDefault();
		}

		#endregion

		#region NodaTime.Duration

		[Pure]
		public static implicit operator JsonValue(NodaTime.Duration value)
		{
			return JsonNumber.Return(value);
		}

		[Pure]
		public static explicit operator NodaTime.Duration(JsonValue? value)
		{
			return (value ?? JsonNull.Null).ToDuration();
		}

		[Pure]
		public static implicit operator JsonValue(NodaTime.Duration? value)
		{
			return JsonNumber.Return(value);
		}

		[Pure]
		public static explicit operator NodaTime.Duration?(JsonValue? value)
		{
			return (value ?? JsonNull.Null).ToDurationOrDefault();
		}

		#endregion

		#region NodaTime.Instant

		[Pure]
		public static implicit operator JsonValue(NodaTime.Instant value)
		{
			//TODO: type dédié pour Noda?
			return JsonString.Return(value);
		}

		[Pure]
		public static explicit operator NodaTime.Instant(JsonValue? value)
		{
			return (value ?? JsonNull.Null).ToInstant();
		}

		[Pure]
		public static implicit operator JsonValue(NodaTime.Instant? value)
		{
			//TODO: type dédié pour Noda?
			return JsonString.Return(value);
		}

		[Pure]
		public static explicit operator NodaTime.Instant?(JsonValue? value)
		{
			return (value ?? JsonNull.Null).ToInstantOrDefault();
		}

		#endregion

		#region NodaTime.OffsetDateTime

		[Pure]
		public static implicit operator JsonValue(NodaTime.OffsetDateTime value)
		{
			//TODO: type dédié pour Noda?
			return JsonString.Return(value);
		}

		[Pure]
		public static explicit operator NodaTime.OffsetDateTime(JsonValue? value)
		{
			return (value ?? JsonNull.Null).As<NodaTime.OffsetDateTime>();
		}

		[Pure]
		public static implicit operator JsonValue(NodaTime.OffsetDateTime? value)
		{
			//TODO: type dédié pour Noda?
			return JsonString.Return(value);
		}

		[Pure]
		public static explicit operator NodaTime.OffsetDateTime?(JsonValue? value)
		{
			return value.IsNullOrMissing() ? default(NodaTime.OffsetDateTime?) : value.As<NodaTime.OffsetDateTime>();
		}

		#endregion

		#region NodaTime.ZonedDateTime

		[Pure]
		public static implicit operator JsonValue(NodaTime.ZonedDateTime value)
		{
			//TODO: type dédié pour Noda?
			return JsonString.Return(value);
		}

		[Pure]
		public static explicit operator NodaTime.ZonedDateTime(JsonValue? value)
		{
			return (value ?? JsonNull.Null).As<NodaTime.ZonedDateTime>();
		}

		[Pure]
		public static implicit operator JsonValue(NodaTime.ZonedDateTime? value)
		{
			//TODO: type dédié pour Noda?
			return JsonString.Return(value);
		}

		[Pure]
		public static explicit operator NodaTime.ZonedDateTime?(JsonValue? value)
		{
			return value.IsNullOrMissing() ? default(NodaTime.ZonedDateTime?) : value.As<NodaTime.ZonedDateTime>();
		}

		#endregion

		#region NodaTime.LocalDateTime

		[Pure]
		public static implicit operator JsonValue(NodaTime.LocalDateTime value)
		{
			//TODO: type dédié pour Noda?
			return JsonString.Return(value);
		}

		[Pure]
		public static explicit operator NodaTime.LocalDateTime(JsonValue? value)
		{
			return (value ?? JsonNull.Null).As<NodaTime.LocalDateTime>();
		}

		[Pure]
		public static implicit operator JsonValue(NodaTime.LocalDateTime? value)
		{
			//TODO: type dédié pour Noda?
			return JsonString.Return(value);
		}

		public static explicit operator NodaTime.LocalDateTime?(JsonValue? value)
		{
			return value.IsNullOrMissing() ? default(NodaTime.LocalDateTime?) : value.As<NodaTime.LocalDateTime>();
		}

		#endregion

		#region NodaTime.LocalDate

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static implicit operator JsonValue(NodaTime.LocalDate value)
		{
			//TODO: type dédié pour Noda?
			return JsonString.Return(value);
		}

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static explicit operator NodaTime.LocalDate(JsonValue? value)
		{
			return (value ?? JsonNull.Null).As<NodaTime.LocalDate>();
		}

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static implicit operator JsonValue(NodaTime.LocalDate? value)
		{
			//TODO: type dédié pour Noda?
			return JsonString.Return(value);
		}

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static explicit operator NodaTime.LocalDate?(JsonValue? value)
		{
			return value.IsNullOrMissing() ? default(NodaTime.LocalDate?) : value.As<NodaTime.LocalDate>();
		}

		#endregion

		//TODO: les autres NodaTime.XXX

		#region Common Array Types
		
		//REVIEW: bouger les implicit cast de T[] dans JsonArray pour retourner directement une array??

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static implicit operator JsonValue(string?[]? values)
		{
			return values == null ? JsonNull.Null : new JsonArray().AddRange(values);
		}

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static explicit operator string?[]?(JsonValue? value)
		{
			return value.AsArray(required: false)?.ToStringArray();
		}

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static implicit operator JsonValue(bool[]? values)
		{
			return values == null ? JsonNull.Null : new JsonArray().AddRange(values);
		}

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static explicit operator bool[]? (JsonValue? value)
		{
			return value.AsArray(required: false)?.ToBoolArray();
		}

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static implicit operator JsonValue(int[]? values)
		{
			return values == null ? JsonNull.Null : new JsonArray().AddRange(values);
		}

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static explicit operator int[]? (JsonValue? value)
		{
			return value.AsArray(required: false)?.ToInt32Array();
		}

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static implicit operator JsonValue(uint[]? values)
		{
			return values == null ? JsonNull.Null : new JsonArray().AddRange(values);
		}

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static explicit operator uint[]? (JsonValue? value)
		{
			return value.AsArray(required: false)?.ToUInt32Array();
		}

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static implicit operator JsonValue(long[]? values)
		{
			return values == null ? JsonNull.Null : new JsonArray().AddRange(values);
		}

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static explicit operator long[]? (JsonValue? value)
		{
			return value.AsArray(required: false)?.ToInt64Array();
		}

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static implicit operator JsonValue(ulong[]? values)
		{
			return values == null ? JsonNull.Null : new JsonArray().AddRange(values);
		}

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static explicit operator ulong[]?(JsonValue? value)
		{
			return value.AsArray(required: false)?.ToUInt64Array();
		}

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static implicit operator JsonValue(float[]? values)
		{
			return values == null ? JsonNull.Null : new JsonArray().AddRange(values);
		}

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static explicit operator float[]? (JsonValue? value)
		{
			return value.AsArray(required: false)?.ToSingleArray();
		}

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static implicit operator JsonValue(double[]? values)
		{
			return values == null ? JsonNull.Null : new JsonArray().AddRange(values);
		}

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static explicit operator double[]? (JsonValue? value)
		{
			return value.AsArray(required: false)?.ToDoubleArray();
		}

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static implicit operator JsonValue(Guid[]? values)
		{
			return values == null ? JsonNull.Null : JsonArray.FromValues(values);
		}

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static explicit operator Guid[]? (JsonValue? value)
		{
			return value.AsArray(required: false)?.ToGuidArray();
		}

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static implicit operator JsonValue(NodaTime.Instant[]? values)
		{
			return values == null ? JsonNull.Null : JsonArray.FromValues(values);
		}

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static explicit operator NodaTime.Instant[]? (JsonValue? value)
		{
			return value.AsArray(required: false)?.ToInstantArray();
		}


		#endregion
	}

}
