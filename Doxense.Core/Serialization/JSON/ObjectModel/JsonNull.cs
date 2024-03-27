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

namespace Doxense.Serialization.Json
{
	using JetBrains.Annotations;
	using System;
	using System.Diagnostics;
	using System.Runtime.CompilerServices;
	using Doxense.Memory;
	using System.Diagnostics.CodeAnalysis;

	/// <summary>Valeur JSON null</summary>
	[DebuggerDisplay("JSON Null({m_kind})")]
	[DebuggerNonUserCode]
	[PublicAPI]
	public sealed class JsonNull : JsonValue, IEquatable<JsonNull>
	{
		//REVIEW: il faudrait soit renommer JsonNull en JsonNil, ou alors .Null en .Nil, pour éviter l'ambiguité "JsonNull.Null" et aussi "get_IsNull" qui retourne true aussi pour missing/error

		internal enum NullKind
		{
			Null = 0,
			Missing,
			Error
		}

		/// <summary>Explicit null value</summary>
		/// <remarks>This singleton is used to represent values that are present in the JSON document, usually represents by the <c>null</c> token.</remarks>
		public static readonly JsonValue Null = new JsonNull(NullKind.Null);

		/// <summary>Missing value</summary>
		/// <remarks>This singleton is returned when </remarks>
		public static readonly JsonValue Missing = new JsonNull(NullKind.Missing);

		/// <summary>Result of an invalid operation</summary>
		/// <remarks>
		/// <para>This singleton is used to represent a null or missing value, which is due to some sort of error condition</para>
		/// <para>For example, attempting to index an object (ex: obj[123]) or access the field of an array (ex: arr["foo"]) are 'soft errors'</para>
		/// </remarks>
		public static readonly JsonValue Error = new JsonNull(NullKind.Error);

		/// <summary>Type of null</summary>
		private readonly NullKind m_kind;

		private JsonNull(NullKind kind)
		{
			m_kind = kind;
		}

		public override string ToString() => string.Empty;

		[ContractAnnotation("=> null")]
		public override string? ToStringOrDefault(string? defaultValue = null) => defaultValue;

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		internal static T? Default<T>() => DefaultCache<T>.Instance;

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		internal static T? Default<T>(JsonValue? value) => DefaultCache<T>.CanBeJsonNull && value is JsonNull jn ? (T) (object) jn : DefaultCache<T>.Instance;

		[Pure, ContractAnnotation("=> null"), MethodImpl(MethodImplOptions.AggressiveInlining)]
		internal static object? Default([DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicParameterlessConstructor)] Type type)
		{
			return type.IsValueType ? ValueTypeDefault(type)
				: typeof(JsonValue) == type || typeof(JsonNull) == type ? JsonNull.Null
				: null;
		}

		[Pure, MethodImpl(MethodImplOptions.NoInlining)]
		internal static object? ValueTypeDefault([DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicParameterlessConstructor)] Type type)
		{
			// on ne peut retourner de singletons que pour des "structs" immutable!
			if (type == typeof(int)) return BoxedZeroInt32;
			if (type == typeof(long)) return BoxedZeroInt64;
			if (type == typeof(bool)) return BoxedFalse;
			if (type == typeof(Guid)) return BoxedEmptyGuid;
			if (type == typeof(double)) return BoxedZeroDouble;
			if (type == typeof(float)) return BoxedZeroSingle;
			// dans tous les autres cas, un appelant pourrait muter par erreur la struct et impacter tout le monde!
			return Activator.CreateInstance(type);
		}

		private static class DefaultCache<T>
		{
			// ReSharper disable once ExpressionIsAlwaysNull
			public static readonly T? Instance = (T?) Default(typeof(T))!;

			public static readonly bool CanBeJsonNull = default(T) == null && typeof(T) == typeof(JsonValue) || typeof(T) == typeof(JsonNull);
		}

		#region JsonValue Members

		public override JsonType Type => JsonType.Null;

		public override object? ToObject() => null;

		public override T? Bind<T>(ICrystalJsonTypeResolver? resolver = null) where T : default
		{
			if (default(T) == null && (typeof(T) == typeof(JsonValue) || typeof(T) == typeof(JsonNull)))
			{
				return (T?) (object) this;
			}
			return default;
		}

		public override object? Bind([DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicParameterlessConstructor)] Type? type, ICrystalJsonTypeResolver? resolver = null)
		{
			// Si on bind vers JsonValue (ou JsonNull) on doit garder le singleton JsonNull.Null
			if (type == typeof(JsonValue) || type == typeof(JsonNull))
			{
				return this;
			}

			// dans tous les cas, on doit retourner le default du type!
			// - si c'est un ValueType, on doit créer une version boxée du default(T) sinon on aura une null ref si l'appelant veut le caster en (T) !
			if (type?.IsValueType ?? false)
			{
				return ValueTypeDefault(type);
			}
			return null;
		}

		private static readonly object BoxedZeroInt32 = default(int);
		private static readonly object BoxedZeroInt64 = default(long);
		private static readonly object BoxedFalse = default(bool);
		private static readonly object BoxedEmptyGuid = default(Guid);
		private static readonly object BoxedZeroSingle = default(float);
		private static readonly object BoxedZeroDouble = default(double);

		public override bool IsNull => true;

		public override bool IsDefault => true;

		public override bool IsReadOnly => true; //note: null is immutable

		public bool IsMissing
		{
			[Pure]
			get => m_kind == NullKind.Missing;
		}

		public bool IsError
		{
			[Pure]
			get => m_kind == NullKind.Error;
		}

		[AllowNull] // setter only
		public override JsonValue this[int index]
		{
			get => ReferenceEquals(this, JsonNull.Error) ? this : JsonNull.Missing;
			set => throw FailCannotMutateImmutableValue(this);
		}

		[AllowNull] // setter only
		public override JsonValue this[Index key]
		{
			get => ReferenceEquals(this, JsonNull.Error) ? this : JsonNull.Missing;
			set => throw FailCannotMutateImmutableValue(this);
		}

		[AllowNull] // setter only
		public override JsonValue this[string key]
		{
			get => ReferenceEquals(this, JsonNull.Error) ? this : JsonNull.Missing;
			set => throw FailCannotMutateImmutableValue(this);
		}

		public override JsonValue GetValueOrDefault(string key, JsonValue? defaultValue = null) => defaultValue ?? JsonNull.Missing;

		public override JsonValue GetValueOrDefault(int index, JsonValue? defaultValue = null) => defaultValue ?? JsonNull.Missing;

		public override JsonValue GetValueOrDefault(Index index, JsonValue? defaultValue = null) => defaultValue ?? JsonNull.Missing;

		public override JsonValue GetValue(string key) => JsonValueExtensions.FailFieldIsNullOrMissing(key);

		public override JsonValue GetValue(int index) => JsonValueExtensions.FailIndexIsNullOrMissing(index, JsonNull.Error);

		public override JsonValue GetValue(Index index) => JsonValueExtensions.FailIndexIsNullOrMissing(index, JsonNull.Error);

		public override bool Contains(JsonValue? value) => false;

		internal override bool IsSmallValue() => true;

		internal override bool IsInlinable() => true;

		#endregion

		#region IJsonSerializable...

		public override void JsonSerialize(CrystalJsonWriter writer)
		{
			writer.WriteNull(); // "null"
		}

		#endregion

		#region Equality Operators...

		public override bool Equals(object? obj)
		{
			// default(object) et DBNull sont considérés comme égal à JsonNull
			if (obj == null || obj is DBNull) return true;

			return base.Equals(obj);
		}

		public override bool Equals(JsonValue? value)
		{
			// default(object) ('null') est considéré comme égal à JsonNull
			if (value == null) return true;

			// si c'est un JsonNull, le type doit matcher
			if (value is JsonNull jn)
			{
				return m_kind == jn.m_kind;
			}

			// sinon, il faut que ce soit un IsNull
			return value.IsNull;
		}

		public bool Equals(JsonNull? value)
		{
			return value == null || m_kind == value.m_kind;
		}

		public override int GetHashCode()
		{
			return 0;
		}

		public override int CompareTo(JsonValue? value)
		{
			// null est toujours plus petit que le reste, sauf lui même
			return value == null || value.IsNull ? 0 : -1;
		}

		public static explicit operator bool(JsonNull obj)
		{
			return false;
		}

		#endregion

		#region IJsonConvertible...

		public override bool ToBoolean() => false;

		public override bool? ToBooleanOrDefault(bool? defaultValue = null) => defaultValue;

		public override byte ToByte() => default;

		public override byte? ToByteOrDefault(byte? defaultValue = null) => defaultValue;

		public override sbyte ToSByte() => default;

		public override sbyte? ToSByteOrDefault(sbyte? defaultValue = null) => defaultValue;

		public override char ToChar() => '\0';

		public override char? ToCharOrDefault(char? defaultValue = null) => defaultValue;

		public override short ToInt16() => default;

		public override short? ToInt16OrDefault(short? defaultValue = null) => defaultValue;

		public override ushort ToUInt16() => default;

		public override ushort? ToUInt16OrDefault(ushort? defaultValue = null) => defaultValue;

		public override int ToInt32() => 0;

		public override int? ToInt32OrDefault(int? defaultValue = null) => defaultValue;

		public override uint ToUInt32() => 0U;

		public override uint? ToUInt32OrDefault(uint? defaultValue = null) => defaultValue;

		public override long ToInt64() => 0L;

		public override long? ToInt64OrDefault(long? defaultValue = null) => defaultValue;

		public override ulong ToUInt64() => 0UL;

		public override ulong? ToUInt64OrDefault(ulong? defaultValue = null) => defaultValue;

		public override float ToSingle() => 0f;

		public override float? ToSingleOrDefault(float? defaultValue = null) => defaultValue;

		public override double ToDouble() => 0d;

		public override double? ToDoubleOrDefault(double? defaultValue = null) => defaultValue;

		public override Half ToHalf() => default;

		public override Half? ToHalfOrDefault(Half? defaultValue = null) => defaultValue;

		public override decimal ToDecimal() => 0m;

		public override decimal? ToDecimalOrDefault(decimal? defaultValue = null) => defaultValue;

		public override Guid ToGuid() => default;

		public override Guid? ToGuidOrDefault(Guid? defaultValue = null) => defaultValue;

		public override Uuid128 ToUuid128() => Uuid128.Empty;

		public override Uuid128? ToUuid128OrDefault(Uuid128? defaultValue = null) => defaultValue;

		public override Uuid96 ToUuid96() => Uuid96.Empty;

		public override Uuid96? ToUuid96OrDefault(Uuid96? defaultValue = null) => defaultValue;

		public override Uuid80 ToUuid80() => Uuid80.Empty;

		public override Uuid80? ToUuid80OrDefault(Uuid80? defaultValue = null) => defaultValue;

		public override Uuid64 ToUuid64() => Uuid64.Empty;

		public override Uuid64? ToUuid64OrDefault(Uuid64? defaultValue = null) => defaultValue;

		public override DateTime ToDateTime() => default;

		public override DateTime? ToDateTimeOrDefault(DateTime? defaultValue = null) => defaultValue;

		public override DateTimeOffset ToDateTimeOffset() => default;

		public override DateTimeOffset? ToDateTimeOffsetOrDefault(DateTimeOffset? defaultValue = null) => defaultValue;

		public override TimeSpan ToTimeSpan() => default;

		public override TimeSpan? ToTimeSpanOrDefault(TimeSpan? defaultValue = null) => defaultValue;

		public override TEnum ToEnum<TEnum>() => default;

		public override TEnum? ToEnumOrDefault<TEnum>(TEnum? defaultValue = null) => defaultValue;

		public override NodaTime.Instant ToInstant() => default;

		public override NodaTime.Instant? ToInstantOrDefault(NodaTime.Instant? defaultValue = null) => defaultValue;

		public override NodaTime.Duration ToDuration() => NodaTime.Duration.Zero;

		public override NodaTime.Duration? ToDurationOrDefault(NodaTime.Duration? defaultValue = null) => defaultValue;

		#endregion

		#region ISliceSerializable...

		public override void WriteTo(ref SliceWriter writer)
		{
			// 'null' => 6E 75 6C 6C
			writer.WriteFixed32(0x6C6C756E);
		}

		#endregion

	}

}
