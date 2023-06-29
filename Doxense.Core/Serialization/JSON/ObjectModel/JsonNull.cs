#region Copyright (c) 2005-2023 Doxense SAS
//
// All rights are reserved. Reproduction or transmission in whole or in part, in
// any form or by any means, electronic, mechanical or otherwise, is prohibited
// without the prior written consent of the copyright owner.
//
#endregion

namespace Doxense.Serialization.Json
{
	using JetBrains.Annotations;
	using System;
	using System.Diagnostics;
	using System.Runtime.CompilerServices;
	using Doxense.Diagnostics.Contracts;
	using Doxense.Memory;

	/// <summary>Valeur JSON null</summary>
	[DebuggerDisplay("JSON Null({m_kind})")]
	[DebuggerNonUserCode]
	public sealed class JsonNull : JsonValue, IEquatable<JsonNull>
	{
		//REVIEW: il faudrait soit renommer JsonNull en JsonNil, ou alors .Null en .Nil, pour �viter l'ambiguit� "JsonNull.Null" et aussi "get_IsNull" qui retourne true aussi pour missing/error

		internal enum NullKind
		{
			Null = 0,
			Missing,
			Error
		}

		/// <summary>Valeur null (explicitement)</summary>
		public static readonly JsonValue Null = new JsonNull(NullKind.Null);

		/// <summary>Valeur null (manquante)</summary>
		public static readonly JsonValue Missing = new JsonNull(NullKind.Missing);

		/// <summary>Valeur null (error)</summary>
		public static readonly JsonValue Error = new JsonNull(NullKind.Error);

		private readonly NullKind m_kind;

		private JsonNull(NullKind kind)
		{
			m_kind = kind;
		}

		public override string ToString() => string.Empty;

		[ContractAnnotation("=> null")]
		public override string? ToStringOrDefault() => null;

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static T? Default<T>()
		{
			return DefaultCache<T>.Instance;
		}

		[Pure, ContractAnnotation("=> null"), MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static object? Default(Type type)
		{
			if (type.IsValueType) return ValueTypeDefault(type);
			if (typeof(JsonValue) == type || typeof(JsonNull) == type)
			{
				return JsonNull.Null;
			}
			return null;
		}

		[Pure, MethodImpl(MethodImplOptions.NoInlining)]
		internal static object ValueTypeDefault(Type type)
		{
			// on ne peut retourner de singletons que pour des "structs" immutable!
			if (type == typeof(int)) return BoxedZeroInt32;
			if (type == typeof(long)) return BoxedZeroInt64;
			if (type == typeof(bool)) return BoxedFalse;
			if (type == typeof(Guid)) return BoxedEmptyGuid;
			// dans tous les autres cas, un appelant pourrait muter par erreur la struct et impacter tout le monde!
			return Activator.CreateInstance(type);
		}

		private static class DefaultCache<T>
		{
			//note: je suis quasi certain que j'ai d�ja un truc �quivalent quelquepart! a refactoriser d�s que possible!

			// ReSharper disable once ExpressionIsAlwaysNull
			public static readonly T? Instance = (T?) Default(typeof(T))!;
		}

		#region JsonValue Members

		public override JsonType Type => JsonType.Null;

		public override object? ToObject() => null;

		public override object? Bind(Type? type, ICrystalJsonTypeResolver? resolver = null)
		{
			// Si on bind vers JsonValue (ou JsonNull) on doit garder le singleton JsonNull.Null
			if (type == typeof(JsonValue) || type == typeof(JsonNull))
			{
				return this;
			}

			// dans tous les cas, on doit retourner le default du type!
			// - si c'est un ValueType, on doit cr�er une version box�e du default(T) sinon on aura une null ref si l'appelant veut le caster en (T) !
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

		public override bool IsNull
		{
			[ContractAnnotation("=> true")]
			get => true;
		}

		public override bool IsDefault
		{
			[ContractAnnotation("=> true")]
			get => true;
		}

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

		public override JsonValue this[int index]
		{
			get => JsonNull.Missing;
			set => throw ThrowHelper.InvalidOperationException("Cannot change the content of a null JSON value");
		}

		public override JsonValue this[string key]
		{
			get => JsonNull.Missing;
			set => throw ThrowHelper.InvalidOperationException("Cannot change the content of a null JSON value");
		}

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
			// default(object) et DBNull sont consid�r�s comme �gal � JsonNull
			if (obj == null || obj is DBNull) return true;

			return base.Equals(obj);
		}

		public override bool Equals(JsonValue value)
		{
			// default(object) ('null') est consid�r� comme �gal � JsonNull
			if (value == null) return true;

			// si c'est un JsonNull, le type doit matcher
			if (value is JsonNull jn)
			{
				return m_kind == jn.m_kind;
			}

			// sinon, il faut que ce soit un IsNull
			return value.IsNull;
		}

		public bool Equals(JsonNull value)
		{
			return value == null || m_kind == value.m_kind;
		}

		public override int GetHashCode()
		{
			return 0;
		}

		public override int CompareTo(JsonValue value)
		{
			// null est toujours plus petit que le reste, sauf lui m�me
			return value == null || value.IsNull ? 0 : -1;
		}

		public static explicit operator bool(JsonNull obj)
		{
			return false;
		}

		#endregion

		#region IJsonConvertible...

		public override bool ToBoolean() => false;

		public override bool? ToBooleanOrDefault() => null;

		public override byte ToByte() => default(byte);

		public override byte? ToByteOrDefault() => null;

		public override sbyte ToSByte() => default(sbyte);

		public override sbyte? ToSByteOrDefault() => null;

		public override char ToChar() => '\0';

		public override char? ToCharOrDefault() => null;

		public override short ToInt16() => default(short);

		public override short? ToInt16OrDefault() => null;

		public override ushort ToUInt16() => default(ushort);

		public override ushort? ToUInt16OrDefault() => null;

		public override int ToInt32() => 0;

		public override int? ToInt32OrDefault() => null;

		public override uint ToUInt32() => 0U;

		public override uint? ToUInt32OrDefault() => null;

		public override long ToInt64() => 0L;

		public override long? ToInt64OrDefault() => null;

		public override ulong ToUInt64() => 0UL;

		public override ulong? ToUInt64OrDefault() => null;

		public override float ToSingle() => 0f;

		public override float? ToSingleOrDefault() => null;

		public override double ToDouble() => 0d;

		public override double? ToDoubleOrDefault() => null;

		public override decimal ToDecimal() => 0m;

		public override decimal? ToDecimalOrDefault() => null;

		public override Guid ToGuid() => default(Guid);

		public override Guid? ToGuidOrDefault() => null;

		public override Uuid128 ToUuid128() => Uuid128.Empty;

		public override Uuid128? ToUuid128OrDefault() => null;

		public override Uuid96 ToUuid96() => Uuid96.Empty;

		public override Uuid96? ToUuid96OrDefault() => null;

		public override Uuid80 ToUuid80() => Uuid80.Empty;

		public override Uuid80? ToUuid80OrDefault() => null;

		public override Uuid64 ToUuid64() => Uuid64.Empty;

		public override Uuid64? ToUuid64OrDefault() => null;

		public override DateTime ToDateTime() => default(DateTime);

		public override DateTime? ToDateTimeOrDefault() => null;

		public override DateTimeOffset ToDateTimeOffset() => default(DateTimeOffset);

		public override DateTimeOffset? ToDateTimeOffsetOrDefault() => null;

		public override TimeSpan ToTimeSpan() => default(TimeSpan);

		public override TimeSpan? ToTimeSpanOrDefault() => null;

		public override TEnum ToEnum<TEnum>() => default(TEnum);

		public override TEnum? ToEnumOrDefault<TEnum>() => null;

		public override NodaTime.Instant ToInstant() => default(NodaTime.Instant);

		public override NodaTime.Instant? ToInstantOrDefault() => null;

		public override NodaTime.Duration ToDuration() => NodaTime.Duration.Zero;

		public override NodaTime.Duration? ToDurationOrDefault() => null;

		#endregion

		public override void WriteTo(ref SliceWriter writer)
		{
			// 'null' => 6E 75 6C 6C
			writer.WriteFixed32(0x6C6C756E);
		}
	}

}
