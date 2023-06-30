#region Copyright (c) 2005-2023 Doxense SAS
//
// All rights are reserved. Reproduction or transmission in whole or in part, in
// any form or by any means, electronic, mechanical or otherwise, is prohibited
// without the prior written consent of the copyright owner.
//
#endregion

namespace Doxense.Serialization.Json
{
	using System;
	using System.Collections.Generic;
	using System.Diagnostics;
	using System.Globalization;
	using System.Runtime.CompilerServices;
	using Doxense.Collections.Caching;
	using Doxense.Diagnostics.Contracts;
	using Doxense.Memory;
	using Doxense.Tools;
	using JetBrains.Annotations;

	/// <summary>Cha�ne de texte JSON</summary>
	[DebuggerDisplay("JSON String({" + nameof(m_value) + "})")]
	[DebuggerNonUserCode]
	public sealed class JsonString : JsonValue, IEquatable<JsonString>, IEquatable<string>, IEquatable<Guid>, IEquatable<DateTime>, IEquatable<DateTimeOffset>, IEquatable<NodaTime.LocalDateTime>, IEquatable<NodaTime.LocalDate>
	{
		public static readonly JsonValue Empty = new JsonString(string.Empty);

		private readonly string m_value;

		internal JsonString(string value)
		{
			Contract.Debug.Requires(value != null);
			m_value = value;
		}

		#region Factory...

		public static JsonValue Return(string? value)
		{
			return value == null ? JsonNull.Null : value.Length == 0 ? JsonString.Empty : new JsonString(value);
		}

		public static JsonValue Return(System.Text.StringBuilder? value)
		{
			return value == null ? JsonNull.Null : value.Length == 0 ? JsonString.Empty : new JsonString(value.ToString());
		}

		public static JsonString Return(char value)
		{
			//note: pas vraiment d'int�r�t � optimiser, je ne pense pas que des cha�nes d'un seul caract�re soit si fr�quentes que �a...
			return new JsonString(new string(value, 1));
		}

		public static JsonValue Return(char? value)
		{
			//note: pas vraiment d'int�r�t � optimiser, je ne pense pas que des cha�nes d'un seul caract�re soit si fr�quentes que �a...
			return value == null ? JsonNull.Null : Return(value.Value);
		}

		public static JsonValue Return(char[]? value, int offset, int count)
		{
			return count == 0 ? (value == null ? JsonNull.Null : JsonString.Empty) : new JsonString(new string(value, offset, count));
		}

		public static JsonValue Return(byte[]? value)
		{
			return value == null ? JsonNull.Null : value.Length == 0 ? JsonString.Empty : new JsonString(Convert.ToBase64String(value));
		}

		public static JsonValue Return(Slice value)
		{
			return value.Count == 0
				? (value.Array == null ? JsonNull.Null : JsonString.Empty)
				: new JsonString(value.ToBase64());
		}

		public static JsonValue Return(ReadOnlySpan<byte> value)
		{
			return value.Length == 0
				? JsonString.Empty
				: new JsonString(Base64Encoding.ToBase64String(value));
		}

		public static JsonValue Return(ArraySegment<byte> value)
		{
			return value.Count == 0
				? (value.Array == null ? JsonNull.Null : JsonString.Empty)
				: new JsonString(Convert.ToBase64String(value.Array, value.Offset, value.Count));
		}

		public static JsonValue Return(Guid value)
		{
			return value != Guid.Empty
				? new JsonString(value.ToString())
				: JsonNull.Null;
		}

		public static JsonValue Return(Guid? value)
		{
			Guid x;
			return (x = value.GetValueOrDefault()) != Guid.Empty
				? new JsonString(x.ToString())
				: JsonNull.Null;
		}

		public static JsonValue Return(Uuid128 value)
		{
			Guid x = value.ToGuid();
			return x != Guid.Empty
				? new JsonString(x.ToString())
				: JsonNull.Null;
		}

		public static JsonValue Return(Uuid128? value)
		{
			Uuid128 x;
			return (x = value.GetValueOrDefault()) != Uuid128.Empty
				? new JsonString(x.ToString())
				: JsonNull.Null;
		}

		public static JsonValue Return(Uuid96 value)
		{
			return value != Uuid96.Empty
				? new JsonString(value.ToString())
				: JsonNull.Null;
		}

		public static JsonValue Return(Uuid96? value)
		{
			Uuid96 x;
			return (x = value.GetValueOrDefault()) != Uuid96.Empty
				? new JsonString(x.ToString())
				: JsonNull.Null;
		}

		public static JsonValue Return(Uuid80 value)
		{
			return value != Uuid80.Empty
				? new JsonString(value.ToString())
				: JsonNull.Null;
		}

		public static JsonValue Return(Uuid80? value)
		{
			Uuid80 x;
			return (x = value.GetValueOrDefault()) != Uuid80.Empty
				? new JsonString(x.ToString())
				: JsonNull.Null;
		}

		public static JsonValue Return(Uuid64 value)
		{
			return value != Uuid64.Empty
				? new JsonString(value.ToString())
				: JsonNull.Null;
		}

		public static JsonValue Return(Uuid64? value)
		{
			Uuid64 x;
			return (x = value.GetValueOrDefault()) != Uuid64.Empty
				? new JsonString(x.ToString())
				: JsonNull.Null;
		}

		public static JsonValue Return(System.Net.IPAddress? value)
		{
			return value != null
				? new JsonString(value.ToString())
				: JsonNull.Null;
		}

		public static JsonValue Return(Uri? value)
		{
			return value != null
				? new JsonString(value.OriginalString)
				: JsonNull.Null;
		}

		public static JsonValue Return(Version? value)
		{
			return value != null
				? new JsonString(value.ToString())
				: JsonNull.Null;
		}

		internal static class TypeNameCache
		{

			private static readonly QuasiImmutableCache<Type, JsonString> Names = CreateDefaultCachedTypes();

			[ContractAnnotation("type:notnull => notnull")]
			public static JsonString? FromType(Type? type)
			{
				if (type == null) return null;
				return Names.TryGetValue(type, out var value)
					? value
					: CreateTypeNameSingleton(type);
			}

			private static QuasiImmutableCache<Type, JsonString> CreateDefaultCachedTypes()
			{
				var map = new Dictionary<Type, JsonString>(TypeEqualityComparer.Default)
				{
					// basic types
					[typeof(object)] = new JsonString("object"),
					[typeof(string)] = new JsonString("string"),
					[typeof(bool)] = new JsonString("bool"),
					[typeof(char)] = new JsonString("char"),
					[typeof(sbyte)] = new JsonString("sbyte"),
					[typeof(short)] = new JsonString("short"),
					[typeof(int)] = new JsonString("int"),
					[typeof(long)] = new JsonString("long"),
					[typeof(byte)] = new JsonString("byte"),
					[typeof(ushort)] = new JsonString("ushort"),
					[typeof(uint)] = new JsonString("uint"),
					[typeof(ulong)] = new JsonString("ulong"),
					[typeof(float)] = new JsonString("float"),
					[typeof(double)] = new JsonString("double"),
					[typeof(decimal)] = new JsonString("decimal"),
					[typeof(TimeSpan)] = new JsonString("TimeSpan"),
					[typeof(DateTime)] = new JsonString("DateTime"),
					[typeof(DateTimeOffset)] = new JsonString("DateTimeOffset"),
					[typeof(Guid)] = new JsonString("Guid"),
					// nullable types
					[typeof(bool?)] = new JsonString("bool?"),
					[typeof(char?)] = new JsonString("char?"),
					[typeof(sbyte?)] = new JsonString("sbyte?"),
					[typeof(short?)] = new JsonString("short?"),
					[typeof(int?)] = new JsonString("int?"),
					[typeof(long?)] = new JsonString("long?"),
					[typeof(byte?)] = new JsonString("byte?"),
					[typeof(ushort?)] = new JsonString("ushort?"),
					[typeof(uint?)] = new JsonString("uint?"),
					[typeof(ulong?)] = new JsonString("ulong?"),
					[typeof(float?)] = new JsonString("float?"),
					[typeof(double?)] = new JsonString("double?"),
					[typeof(decimal?)] = new JsonString("decimal?"),
					[typeof(TimeSpan?)] = new JsonString("TimeSpan?"),
					[typeof(DateTime?)] = new JsonString("DateTime?"),
					[typeof(DateTimeOffset?)] = new JsonString("DateTimeOffset?"),
					[typeof(Guid?)] = new JsonString("Guid?"),
					// system types
					[typeof(Uri)] = new JsonString(typeof(Uri).FullName!),
					[typeof(Version)] = new JsonString(typeof(Version).FullName!),
					[typeof(Exception)] = new JsonString(typeof(Exception).FullName!),
					//
				};
				return new QuasiImmutableCache<Type, JsonString>(map);
			}

			[Pure]
			private static string GetSerializableTypeName(Type type)
			{
				// note: "simple" types (string, Guid, ...) have already been handled by the cache

				// we will omit the assembly name for mscorlib and System.Core because they will always be loaded!
				string assName = type.Assembly.FullName!;
				if (assName.StartsWith("mscorlib, ", StringComparison.Ordinal)
				 || assName.StartsWith("System.Core, ", StringComparison.Ordinal)
				 || assName.StartsWith("System.Private.CoreLib, ", StringComparison.Ordinal))
				{
					return type.FullName ?? type.Name;
				}

				// for everything else, we must add the full qualified name, including the assembly and culture :(
				return type.AssemblyQualifiedName ?? type.FullName ?? type.Name;
			}

			[Pure]
			private static JsonString CreateTypeNameSingleton(Type type)
			{
				return Names.GetOrAdd(type, new JsonString(GetSerializableTypeName(type)));
			}

		}

		[Pure]
		public static JsonValue Return(Type? type)
		{
			return TypeNameCache.FromType(type) ?? JsonNull.Null;
		}


		#region Noda Types...

		public static JsonValue Return(NodaTime.Instant value)
		{
			if (value.ToUnixTimeTicks() == 0) return JsonString.Empty; //REVIEW: retourner "" ou null ?
			return new JsonString(CrystalJsonNodaPatterns.Instants.Format(value));
		}

		public static JsonValue Return(NodaTime.Instant? value)
		{
			if (!value.HasValue) return JsonNull.Null;
			if (value.Value.ToUnixTimeTicks() == 0) return JsonString.Empty; //REVIEW: retourner "" ou null ?
			return new JsonString(CrystalJsonNodaPatterns.Instants.Format(value.Value));
		}

		public static JsonValue Return(NodaTime.ZonedDateTime value)
		{
			return new JsonString(CrystalJsonNodaPatterns.ZonedDateTimes.Format(value));
		}

		public static JsonValue Return(NodaTime.ZonedDateTime? value)
		{
			if (!value.HasValue) return JsonNull.Null;
			return new JsonString(CrystalJsonNodaPatterns.ZonedDateTimes.Format(value.Value));
		}

		public static JsonValue Return(NodaTime.DateTimeZone value)
		{
			return value != null
				? new JsonString(value.Id)
				: JsonNull.Null;
		}

		public static JsonValue Return(NodaTime.LocalDateTime value)
		{
			return new JsonString(CrystalJsonNodaPatterns.LocalDateTimes.Format(value));
		}

		public static JsonValue Return(NodaTime.LocalDateTime? value)
		{
			if (!value.HasValue) return JsonNull.Null;
			return new JsonString(CrystalJsonNodaPatterns.LocalDateTimes.Format(value.Value));
		}

		public static JsonValue Return(NodaTime.LocalDate value)
		{
			return new JsonString(CrystalJsonNodaPatterns.LocalDates.Format(value));
		}

		public static JsonValue Return(NodaTime.LocalDate? value)
		{
			if (!value.HasValue) return JsonNull.Null;
			return new JsonString(CrystalJsonNodaPatterns.LocalDates.Format(value.Value));
		}

		public static JsonValue Return(NodaTime.LocalTime value)
		{
			return new JsonString(CrystalJsonNodaPatterns.LocalTimes.Format(value));
		}

		public static JsonValue Return(NodaTime.LocalTime? value)
		{
			if (!value.HasValue) return JsonNull.Null;
			return new JsonString(CrystalJsonNodaPatterns.LocalTimes.Format(value.Value));
		}

		public static JsonValue Return(NodaTime.OffsetDateTime value)
		{
			return new JsonString(CrystalJsonNodaPatterns.OffsetDateTimes.Format(value));
		}

		public static JsonValue Return(NodaTime.OffsetDateTime? value)
		{
			if (!value.HasValue) return JsonNull.Null;
			return new JsonString(CrystalJsonNodaPatterns.OffsetDateTimes.Format(value.Value));
		}

		public static JsonValue Return(NodaTime.Offset value)
		{
			return new JsonString(CrystalJsonNodaPatterns.Offsets.Format(value));
		}

		public static JsonValue Return(NodaTime.Offset? value)
		{
			if (!value.HasValue) return JsonNull.Null;
			return new JsonString(CrystalJsonNodaPatterns.Offsets.Format(value.Value));
		}

		#endregion

		#endregion

		#region Public Properties...

		public string Value => m_value;

		public int Length => m_value.Length;

		public bool IsNullOrEmpty => string.IsNullOrEmpty(m_value);

		public bool StartsWith(string value)
		{
			//note: JsonString.Null ne commence par rien
			return m_value.StartsWith(value, StringComparison.Ordinal);
		}

		public bool EndsWith(string value)
		{
			//note: JsonString.Null ne fini par rien
			return m_value.EndsWith(value, StringComparison.Ordinal);
		}

		public bool Contains(string value)
		{
			//note: JsonString.Null ne contient rien
			return m_value.IndexOf(value, StringComparison.Ordinal) >= 0;
		}

		public int IndexOf(string value)
		{
			//note: JsonString.Null ne contient rien
			return m_value.IndexOf(value, StringComparison.Ordinal);
		}

		#endregion

		#region JsonValue Members...

		public override JsonType Type => JsonType.String;

		public override bool IsNull => false;

		public override bool IsDefault => false; //note: string empty is NOT "default"

		public byte[] ToBuffer() //REVIEW: => GetBytes()? DecodeBase64() ?
		{
			return m_value.Length != 0 ? Convert.FromBase64String(m_value) : Array.Empty<byte>();
		}

		public override object? ToObject()
		{
			return m_value;
		}

		public override object? Bind(Type? type, ICrystalJsonTypeResolver? resolver = null)
		{
			//TODO: r��crire via une Dictionary<Type, Func<..>> pour �viter le train de if/elseif !

			if (type == null || typeof(string) == type || typeof(object) == type)
			{
				//TODO: si object, heuristics pour convertir en DateTime ?
				return m_value;
			}

			if (type == typeof(JsonValue) || type == typeof(JsonString))
			{
				return this;
			}

			resolver ??= CrystalJson.DefaultResolver;

			try
			{
				if (type.IsPrimitive)
				{
					switch(System.Type.GetTypeCode(type))
					{
						case TypeCode.Int32: return ToInt32();
						case TypeCode.Int64: return ToInt64();
						case TypeCode.Single: return ToSingle();
						case TypeCode.Double: return ToDouble();
						case TypeCode.Boolean: return ToBoolean();
						case TypeCode.Char: return ToChar();
						case TypeCode.UInt32: return ToUInt32();
						case TypeCode.UInt64: return ToUInt64();
						case TypeCode.Int16: return ToInt16();
						case TypeCode.UInt16: return ToUInt16();
						case TypeCode.Byte: return ToByte();
						case TypeCode.SByte: return ToSByte();
						//note: decimal et DateTime ne sont pas IsPrimitive!
					}
				}
				//TODO: utiliser un dictionnaire Type => Func<...> pour speeder le test?
				else if (typeof(DateTime) == type)
				{ // conversion en date ?
					return ToDateTime();
				}
				else if (typeof(DateTimeOffset) == type)
				{ // conversion en DTO ?
					return ToDateTimeOffset();
				}
				else if (type.IsEnum)
				{ // Enumeration
					//TODO: utiliser l'EnumStringTable?
					return Enum.Parse(type, m_value, true);
				}
				else if (typeof(decimal) == type)
				{
					return ToDecimal();
				}
				else if (typeof(Guid) == type)
				{ // conversion en guid ?
					return ToGuid();
				}
				else if (typeof(Uuid128) == type)
				{
					return new Uuid128(ToGuid());
				}
				else if (typeof(Uuid64) == type)
				{
					return Uuid64.Parse(m_value);
				}
				else if (typeof(Uuid96) == type)
				{
					return Uuid96.Parse(m_value);
				}
				else if (typeof(Uuid80) == type)
				{
					return Uuid80.Parse(m_value);
				}
				else if (typeof(TimeSpan) == type)
				{ // conversion en timespan ?
					return ToTimeSpan();
				}
				else if (typeof(NodaTime.Instant) == type)
				{
					return ToInstant();
				}
				else if (typeof(NodaTime.Duration) == type)
				{
					return ToDuration();
				}
				else if (typeof(NodaTime.LocalDate) == type)
				{
					return ToLocalDate();
				}
				else if (typeof(NodaTime.LocalDateTime) == type)
				{
					return ToLocalDateTime();
				}
				else if (typeof(char[]) == type)
				{ // tableau de chars, c'est facile..
					return m_value.ToCharArray();
				}
				else if (typeof(byte[]) == type)
				{ // par convention, la cha�ne doit �tre Base64 encod�e !
					return ToBuffer();
				}
				else if (typeof(ArraySegment<byte>) == type)
				{
					return new ArraySegment<byte>(ToBuffer());
				}
				else if (typeof(Slice) == type)
				{
					return ToBuffer().AsSlice();
				}
				else if (typeof(System.Text.StringBuilder) == type)
				{ // Buffer texte
					return new System.Text.StringBuilder(m_value);
				}
				else if (typeof(System.Net.IPAddress) == type)
				{ // Adresse IP
					return m_value.Length != 0 ? System.Net.IPAddress.Parse(m_value) : null;
				}
				else if (typeof(System.Version) == type)
				{ // Version
					return m_value.Length != 0 ? System.Version.Parse(m_value) : null;
				}
				else if (typeof(System.Uri) == type)
				{
					return ToUri();
				}

				#region NodaTime types...

				if (typeof(NodaTime.Instant) == type)
				{
					return ToInstant();
				}
				if (typeof(NodaTime.Duration) == type)
				{
					return ToDuration();
				}
				if (typeof(NodaTime.LocalDateTime) == type)
				{
					return ToLocalDateTime();
				}
				if (typeof(NodaTime.ZonedDateTime) == type)
				{
					return ToZonedDateTime();
				}
				if (typeof(NodaTime.OffsetDateTime) == type)
				{
					return ToOffsetDateTime();
				}
				if (typeof(NodaTime.DateTimeZone).IsAssignableFrom(type))
				{
					return ToDateTimeZone();
				}
				if (typeof(NodaTime.Offset) == type)
				{
					return ToOffset();
				}
				if (typeof(NodaTime.LocalDate) == type)
				{
					return ToLocalDate();
				}
				if (typeof(NodaTime.LocalTime) == type)
				{
					return CrystalJsonNodaPatterns.LocalTimes.Parse(m_value).Value;
				}

				#endregion

				//TODO: XmlNode ?

				var nullableType = Nullable.GetUnderlyingType(type);
				if (nullableType != null)
				{
					//note: missing/null ou "" retourne default(T?) qui est donc null
					if (string.IsNullOrEmpty(m_value)) return null;
					return Bind(nullableType, resolver);
				}
			}
			catch (Exception e) when (!e.IsFatalError())
			{
				if (e is FormatException) throw new FormatException($"Failed to convert JSON string into {type.GetFriendlyName()}: {e.Message}");
				throw CrystalJson.Errors.Binding_CannotBindJsonStringToThisType(this, type, e);
			}

			// check si impl�mente IJsonBindable
			if (typeof(IJsonBindable).IsAssignableFrom(type))
			{ // HACKHACK: pour les type qui se s�rialisent en string (par ex: Oid)
				var typeDef = resolver.ResolveJsonType(type);
				if (typeDef?.CustomBinder != null)
				{
					return typeDef.CustomBinder(this, type, resolver);
				}
			}

			// passe par un custom binder?
			// => g�re le cas des classes avec un ctor DuckTyping, ou des m�thodes statiques
			var def = resolver.ResolveJsonType(type);
			if (def?.CustomBinder != null)
			{
				return def.CustomBinder(this, type, resolver);
			}

			throw CrystalJson.Errors.Binding_CannotBindJsonStringToThisType(this, type);
		}

		internal override bool IsSmallValue() => m_value.Length <= 36; // guid!

		internal override bool IsInlinable() => m_value.Length <= 80;

		internal override string GetCompactRepresentation(int depth)
		{
			// depth 0, chaine enti�re jusqu'a 128 caracs, ou avec un '[...]' au millieu si plus grand
			// depth 1: chaine enti�re jusqu'a 64 caracs, ou avec un '[...]' au millieu si plus grand
			// depth 2: chaine enti�re jusqu'a 36 caracs, ou avec un '[...]' au millieu si plus grand
			// depth 3: chaine enti�re jusqu'a 16 caracs, ou avec un '[...]' au millieu si plus grand
			// depth 4+: '...'

			var value = m_value;
			if (value.Length == 0) return "''";
			if (depth > 3) return "'\u2026'";
			int allowance = depth == 0 ? 128 : depth == 1 ? 64 : depth == 2 ? 36 : 16;
			return value.Length <= allowance
				? "'" + value + "'"
				: "'" + value.Substring(0, (allowance / 2) - 1) + "[\u2026]" + value.Substring(value.Length - (allowance / 2) + 1) + "'";
		}

		#endregion

		#region IJsonSerializable

		public override void JsonSerialize(CrystalJsonWriter writer)
		{
			writer.WriteValue(m_value);
		}

		#endregion

		#region IEquatable<...>

		public override bool Equals(object? obj)
		{
			if (ReferenceEquals(obj, this)) return true;
			return obj switch
			{
				JsonValue val      => Equals(val),
				string s           => Equals(s),
				null               => false,
				DateTime dt        => Equals(dt),
				DateTimeOffset dto => Equals(dto),
				_                  => false
			};
			//TODO: compare with int, long, ...?
		}

		public override bool Equals(JsonValue? obj)
		{
			if (ReferenceEquals(obj, this)) return true;
			if (obj == null) return false;
			switch (obj)
			{
				case JsonString str:    return Equals(str);
				case JsonNumber num:    return num.Equals(this);
				case JsonDateTime date: return date.Equals(this);
				default:                return false;
			}
		}

		public bool Equals(JsonString? obj)
		{
			return obj != null && string.Equals(m_value, obj.m_value, StringComparison.Ordinal);
		}

		public bool Equals(string? obj)
		{
			return obj != null && string.Equals(m_value, obj, StringComparison.Ordinal);
		}

		public bool Equals(Guid obj)
		{
			return ToGuid() == obj;
		}

		public bool Equals(DateTime obj)
		{
			return ToDateTime() == obj;
		}

		public bool Equals(DateTimeOffset obj)
		{
			return ToDateTimeOffset() == obj;
		}

		public bool Equals(NodaTime.LocalDateTime obj)
		{
			return ToLocalDateTime() == obj;
		}

		public bool Equals(NodaTime.LocalDate obj)
		{
			return ToLocalDate() == obj;
		}

		public override int GetHashCode()
		{
			return m_value?.GetHashCode() ?? 0;
		}

		public override int CompareTo(JsonValue other)
		{
			if (object.ReferenceEquals(other, null)) return +1;
			switch (other.Type)
			{
				case JsonType.String: return CompareTo(other as JsonString);
				case JsonType.Number: return -((JsonNumber)other).CompareTo(this); //note: n�gatif car on inverse le sens de la comparaison!
				default: return base.CompareTo(other);
			}
		}

		public int CompareTo(JsonString? other)
		{
			return other != null ? string.CompareOrdinal(m_value, other.Value) : +1;
		}

		#endregion

		#region IJsonConvertible...

		#region String

		public override string ToJson(CrystalJsonSettings? settings = null)
		{
			return settings?.TargetLanguage != CrystalJsonSettings.Target.JavaScript
				? JsonEncoding.Encode(m_value)
				: CrystalJsonFormatter.EncodeJavaScriptString(m_value);
		}

		public override string ToString()
		{
			return m_value ?? string.Empty;
		}

		[ContractAnnotation("=> notnull")]
		public override string ToStringOrDefault()
		{
			return m_value;
		}

		public bool TryConvertString(out string value)
		{
			value = m_value;
			return m_value != null;
		}

		#endregion

		#region Boolean

		public override bool ToBoolean()
		{
			return !string.IsNullOrEmpty(m_value) && bool.Parse(m_value);
		}

		public override bool? ToBooleanOrDefault()
		{
			return string.IsNullOrEmpty(m_value) ? default(bool?) : bool.Parse(m_value);
		}

		#endregion

		#region Byte

		public override byte ToByte()
		{
			return string.IsNullOrEmpty(m_value) ? default(byte) : byte.Parse(m_value, NumberFormatInfo.InvariantInfo);
		}

		public override byte? ToByteOrDefault()
		{
			return string.IsNullOrEmpty(m_value) ? default(byte?) : byte.Parse(m_value, NumberFormatInfo.InvariantInfo);
		}

		#endregion

		#region SByte

		public override sbyte ToSByte()
		{
			return string.IsNullOrEmpty(m_value) ? default(sbyte) : sbyte.Parse(m_value, NumberFormatInfo.InvariantInfo);
		}

		public override sbyte? ToSByteOrDefault()
		{
			return string.IsNullOrEmpty(m_value) ? default(sbyte?) : sbyte.Parse(m_value, NumberFormatInfo.InvariantInfo);
		}

		#endregion

		#region Char

		public override char ToChar()
		{
			return string.IsNullOrEmpty(m_value) ? default(char) : m_value[0];
		}

		public override char? ToCharOrDefault()
		{
			return string.IsNullOrEmpty(m_value) ? default(char?) : m_value[0];
		}

		#endregion

		#region Int16

		public override short ToInt16()
		{
			return string.IsNullOrEmpty(m_value) ? default(short) : short.Parse(m_value, NumberFormatInfo.InvariantInfo);
		}

		public override short? ToInt16OrDefault()
		{
			return string.IsNullOrEmpty(m_value) ? default(short?) : ToInt16();
		}

		public bool TryConvertInt16(out short value)
		{
			// note: NumberStyles obtenus via Reflector
			return short.TryParse(m_value, NumberStyles.Integer, NumberFormatInfo.InvariantInfo, out value);
		}

		#endregion

		#region UInt16

		public override ushort ToUInt16()
		{
			return string.IsNullOrEmpty(m_value) ? default(ushort) : ushort.Parse(m_value, NumberFormatInfo.InvariantInfo);
		}

		public override ushort? ToUInt16OrDefault()
		{
			return string.IsNullOrEmpty(m_value) ? default(ushort?) : ToUInt16();
		}

		public bool TryConvertUInt16(out ushort value)
		{
			// note: NumberStyles obtenus via Reflector
			return ushort.TryParse(m_value, NumberStyles.Integer, NumberFormatInfo.InvariantInfo, out value);
		}

		#endregion

		#region Int32

		public override int ToInt32()
		{
			if (string.IsNullOrEmpty(m_value)) return 0;
			if (m_value.Length == 1)
			{ // le cas le plus fr�quent est un nombre de 0 � 9
				char c = m_value[0];
				if (c >= '0' & c <= '9') return c - '0';
			}
			//TODO: PERF: faster parsing ?
			return int.Parse(m_value, NumberFormatInfo.InvariantInfo);
		}

		public override int? ToInt32OrDefault()
		{
			return string.IsNullOrEmpty(m_value) ? default(int?) : ToInt32();
		}

		public bool TryConvertInt32(out int value)
		{
			var lit = m_value;
			if (lit == null) { value = 0; return false; }
			if (lit.Length == 1)
			{ // le cas le plus fr�quent est un nombre de 0 � 9
				char c = lit[0];
				if (c >= '0' & c <= '9') { value = c - '0'; return true; }
			}
			// note: NumberStyles obtenus via Reflector
			//TODO: PERF: faster parsing ?
			return int.TryParse(lit, NumberStyles.Integer, NumberFormatInfo.InvariantInfo, out value);
		}

		#endregion

		#region UInt32

		public override uint ToUInt32()
		{
			return string.IsNullOrEmpty(m_value) ? 0U : uint.Parse(m_value, NumberFormatInfo.InvariantInfo);
		}

		public override uint? ToUInt32OrDefault()
		{
			return string.IsNullOrEmpty(m_value) ? default(uint?) : ToUInt32();
		}

		public bool TryConvertUInt32(out uint value)
		{
			// note: NumberStyles obtenus via Reflector
			return uint.TryParse(m_value, NumberStyles.Integer, NumberFormatInfo.InvariantInfo, out value);
		}

		#endregion

		#region Int64

		public override long ToInt64()
		{
			if (string.IsNullOrEmpty(m_value)) return 0L;
			if (m_value.Length == 1)
			{ // le cas le plus fr�quent est un nombre de 0 � 9
				char c = m_value[0];
				if (c >= '0' & c <= '9') return c - '0';
			}
			//TODO: PERF: faster parsing ?
			return long.Parse(m_value, NumberFormatInfo.InvariantInfo);
		}

		public override long? ToInt64OrDefault()
		{
			return string.IsNullOrEmpty(m_value) ? default(long?) : ToInt64();
		}

		public bool TryConvertInt64(out long value)
		{
			var lit = m_value;
			if (lit == null) { value = 0L; return false; }
			if (lit.Length == 1)
			{ // le cas le plus fr�quent est un nombre de 0 � 9
				char c = lit[0];
				if (c >= '0' & c <= '9') { value = c - '0'; return true; }
			}
			// note: NumberStyles obtenus via Reflector
			return long.TryParse(lit, NumberStyles.Integer, NumberFormatInfo.InvariantInfo, out value);
		}

		#endregion

		#region UInt64

		public override ulong ToUInt64()
		{
			return string.IsNullOrEmpty(m_value) ? 0UL : ulong.Parse(m_value, NumberFormatInfo.InvariantInfo);
		}

		public override ulong? ToUInt64OrDefault()
		{
			return string.IsNullOrEmpty(m_value) ? default(ulong?) : ToUInt64();
		}

		public bool TryConvertUInt64(out ulong value)
		{
			// note: NumberStyles obtenus via Reflector
			return ulong.TryParse(m_value, NumberStyles.Integer, NumberFormatInfo.InvariantInfo, out value);
		}

		#endregion

		#region Single

		public override float ToSingle()
		{
			return string.IsNullOrEmpty(m_value) ? 0f : float.Parse(m_value, NumberFormatInfo.InvariantInfo);
		}

		public override float? ToSingleOrDefault()
		{
			return string.IsNullOrEmpty(m_value) ? default(float?) : ToSingle();
		}

		public bool TryConvertSingle(out float value)
		{
			// note: NumberStyles obtenus via Reflector
			return float.TryParse(m_value, NumberStyles.Float | NumberStyles.AllowThousands, NumberFormatInfo.InvariantInfo, out value);
		}

		#endregion

		#region Double

		public override double ToDouble()
		{
			return string.IsNullOrEmpty(m_value) ? 0d : double.Parse(m_value, NumberFormatInfo.InvariantInfo);
		}

		public override double? ToDoubleOrDefault()
		{
			return string.IsNullOrEmpty(m_value) ? default(double?) : ToDouble();
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool TryConvertDouble(out double value)
		{
			return TryConvertDouble(m_value, out value);
		}

		internal static bool TryConvertDouble(string literal, out double value)
		{
			// note: NumberStyles obtenus via Reflector
			return double.TryParse(literal, NumberStyles.Float | NumberStyles.AllowThousands, NumberFormatInfo.InvariantInfo, out value);
		}

		#endregion

		#region Decimal

		public override decimal ToDecimal()
		{
			return string.IsNullOrEmpty(m_value) ? 0m : decimal.Parse(m_value, NumberFormatInfo.InvariantInfo);
		}

		public override decimal? ToDecimalOrDefault()
		{
			return string.IsNullOrEmpty(m_value) ? default(decimal?) : ToDecimal();
		}

		public bool TryConvertDecimal(out decimal value)
		{
			// note: NumberStyles obtenus via Reflector
			return decimal.TryParse(m_value, NumberStyles.Number, NumberFormatInfo.InvariantInfo, out value);
		}

		#endregion

		#region Guid

		public override Guid ToGuid()
		{
			return string.IsNullOrEmpty(m_value) ? default(Guid) : Guid.Parse(m_value);
		}

		public override Guid? ToGuidOrDefault()
		{
			return string.IsNullOrEmpty(m_value) ? default(Guid?) : Guid.Parse(m_value);
		}

		public bool TryConvertGuid(out Guid value)
		{
			if (m_value.Length == 36 /* 00000000-0000-0000-0000-000000000000 */ ||
				m_value.Length == 32 /* 00000000000000000000000000000000 */ ||
				m_value.Length == 38 /* {00000000-0000-0000-0000-000000000000} */)
			{
				return Guid.TryParse(m_value, out value);
			}
			value = default(Guid);
			return false;
		}

		public override Uuid128 ToUuid128()
		{
			return string.IsNullOrEmpty(m_value) ? default(Uuid128) : Uuid128.Parse(m_value);
		}

		public override Uuid128? ToUuid128OrDefault()
		{
			return string.IsNullOrEmpty(m_value) ? default(Uuid128?) : Uuid128.Parse(m_value);
		}

		public override Uuid96 ToUuid96()
		{
			return string.IsNullOrEmpty(m_value) ? default(Uuid96) : Uuid96.Parse(m_value);
		}

		public override Uuid96? ToUuid96OrDefault()
		{
			return string.IsNullOrEmpty(m_value) ? default(Uuid96?) : Uuid96.Parse(m_value);
		}

		public override Uuid80 ToUuid80()
		{
			return string.IsNullOrEmpty(m_value) ? default(Uuid80) : Uuid80.Parse(m_value);
		}

		public override Uuid80? ToUuid80OrDefault()
		{
			return string.IsNullOrEmpty(m_value) ? default(Uuid80?) : Uuid80.Parse(m_value);
		}

		public override Uuid64 ToUuid64()
		{
			return string.IsNullOrEmpty(m_value) ? default(Uuid64) : Uuid64.Parse(m_value);
		}

		public override Uuid64? ToUuid64OrDefault()
		{
			return string.IsNullOrEmpty(m_value) ? default(Uuid64?) : Uuid64.Parse(m_value);
		}

		#endregion

		#region DateTime

		public override DateTime ToDateTime()
		{
			if (string.IsNullOrEmpty(m_value)) return default(DateTime);

			if (CrystalJsonParser.TryParseIso8601DateTime(m_value, out var d))
			{
				return d;
			}

			if (CrystalJsonParser.TryParseMicrosoftDateTime(m_value, out d, out var tz))
			{
				if (tz.HasValue)
				{
					var utcOffset = TimeZoneInfo.Local.GetUtcOffset(d).Subtract(tz.Value);
					return d.ToLocalTime().Add(utcOffset);
				}
				return d;
			}

			// on tente notre chance...
			return StringConverters.ParseDateTime(m_value);
		}

		public override DateTime? ToDateTimeOrDefault()
		{
			return string.IsNullOrEmpty(m_value) ? default(DateTime?) : ToDateTime();
		}

		public bool TryConvertDateTime(out DateTime dt)
		{
			return TryConvertDateTime(m_value, out dt);
		}

		internal static bool TryConvertDateTime(string literal, out DateTime dt)
		{
			if (string.IsNullOrEmpty(literal))
			{
				dt = default;
				return false;
			}

			if (CrystalJsonParser.TryParseIso8601DateTime(literal, out dt))
			{
				return true;
			}

			if (CrystalJsonParser.TryParseMicrosoftDateTime(literal, out dt, out var tz))
			{
				if (tz.HasValue)
				{
					var utcOffset = TimeZoneInfo.Local.GetUtcOffset(dt).Subtract(tz.Value);
					dt = dt.ToLocalTime().Add(utcOffset);
				}
				return true;
			}

			// on tente notre chance...
			return StringConverters.TryParseDateTime(literal, CultureInfo.InvariantCulture, out dt, false);
		}

		#endregion

		#region DateTimeOffset

		public override DateTimeOffset ToDateTimeOffset()
		{
			if (m_value.Length == 0) return default(DateTimeOffset);

			if (CrystalJsonParser.TryParseIso8601DateTimeOffset(m_value, out var dto))
			{
				return dto;
			}

			if (CrystalJsonParser.TryParseMicrosoftDateTime(m_value, out var d, out var tz))
			{
				if (tz.HasValue)
				{
					return new DateTimeOffset(d, tz.Value);
				}
				return new DateTimeOffset(d);
			}

			// on tente notre chance
			return new DateTimeOffset(StringConverters.ParseDateTime(m_value));
		}

		public override DateTimeOffset? ToDateTimeOffsetOrDefault()
		{
			return string.IsNullOrEmpty(m_value) ? default(DateTimeOffset?) : ToDateTimeOffset();
		}

		public bool TryConvertDateTimeOffset(out DateTimeOffset dto)
		{
			return TryConvertDateTimeOffset(m_value, out dto);
		}

		internal static bool TryConvertDateTimeOffset(string literal, out DateTimeOffset dto)
		{
			if (literal.Length == 0)
			{
				dto = default;
				return false;
			}

			if (CrystalJsonParser.TryParseIso8601DateTimeOffset(literal, out dto))
			{
				return true;
			}

			if (CrystalJsonParser.TryParseMicrosoftDateTime(literal, out var dt, out var tz))
			{
				dto = tz.HasValue ? new DateTimeOffset(dt, tz.Value) : new DateTimeOffset(dt);
				return true;
			}

			// on tente notre chance
			if (!StringConverters.TryParseDateTime(literal, CultureInfo.InvariantCulture, out dt, false))
			{
				dto = default;
				return false;
			}
			dto = new DateTimeOffset(dt);
			return true;
		}

		#endregion

		#region TimeSpan

		public override TimeSpan ToTimeSpan()
		{
			return string.IsNullOrEmpty(m_value) ? default(TimeSpan) : TimeSpan.Parse(m_value, CultureInfo.InvariantCulture);
		}

		public override TimeSpan? ToTimeSpanOrDefault()
		{
			return string.IsNullOrEmpty(m_value) ? default(TimeSpan?) : TimeSpan.Parse(m_value, CultureInfo.InvariantCulture);
		}

		#endregion

		#region NodaTime

		public override NodaTime.Instant ToInstant()
		{
			if (string.IsNullOrEmpty(m_value)) return default(NodaTime.Instant);
			var parseResult = CrystalJsonNodaPatterns.Instants.Parse(m_value);
			if (parseResult.TryGetValue(default(NodaTime.Instant), out var instant)) //on est oblig� de lui donner le failureValue (default(Instant)) meme si on l'utilise pas
			{
				return instant;
			}
			//on a pas reussi a le parser en instant
			var dateTimeOffset = ToDateTimeOffset(); //si c'etait au format DateTimeOffset ...
			return NodaTime.Instant.FromDateTimeOffset(dateTimeOffset); //on sait le convertir en Instant !
		}

		public override NodaTime.Instant? ToInstantOrDefault()
		{
			return string.IsNullOrEmpty(m_value) ? default(NodaTime.Instant?) : ToInstant();
		}

		public override NodaTime.Duration ToDuration()
		{
			string value = m_value;
			return string.IsNullOrEmpty(value)
				? NodaTime.Duration.Zero
				: NodaTime.Duration.FromTicks((long) (double.Parse(value) * NodaTime.NodaConstants.TicksPerSecond));
		}

		public override NodaTime.Duration? ToDurationOrDefault()
		{
			string value = m_value;
			return string.IsNullOrEmpty(value)
				? default(NodaTime.Duration?)
				: NodaTime.Duration.FromTicks((long)(double.Parse(value) * NodaTime.NodaConstants.TicksPerSecond));
		}

		public NodaTime.LocalDateTime ToLocalDateTime()
		{
			string value = m_value;
			if (string.IsNullOrEmpty(value)) return default(NodaTime.LocalDateTime);
			if (value.EndsWith("Z", StringComparison.Ordinal))
			{ // c'est un date UTC, probablement un Instant!
				//HACK: c'est crado, mais on se dit que l'intention d'origine �tait de stocker l'heure locale, donc on la remap dans l'heure locale du syst�me
				// => ca ne marchera pas si c'est un autre serveur d'une autre timezone qui a g�n�r� le JSON, mais a ce moment la pourquoi il a choisi un Instant pour un LocalDateTime???
				return NodaTime.LocalDateTime.FromDateTime(ToInstant().ToDateTimeUtc().ToLocalTime());
			}
			return CrystalJsonNodaPatterns.LocalDateTimes.Parse(value).Value;
		}

		public NodaTime.ZonedDateTime ToZonedDateTime()
		{
			string value = m_value;
			return string.IsNullOrEmpty(value)
				? default(NodaTime.ZonedDateTime)
				: CrystalJsonNodaPatterns.ZonedDateTimes.Parse(value).Value;
		}

		public NodaTime.OffsetDateTime ToOffsetDateTime()
		{
			string value = m_value;
			return string.IsNullOrEmpty(value)
				? default(NodaTime.OffsetDateTime)
				: CrystalJsonNodaPatterns.OffsetDateTimes.Parse(value).Value;
		}

		public NodaTime.DateTimeZone? ToDateTimeZone()
		{
			string value = m_value;
			return string.IsNullOrEmpty(value)
				? default(NodaTime.DateTimeZone)
				//note: on utilise toujours tzdb en premier
				: (NodaTime.DateTimeZoneProviders.Tzdb.GetZoneOrNull(value));
		}

		public NodaTime.Offset ToOffset()
		{
			string value = m_value;
			return string.IsNullOrEmpty(value)
				? default(NodaTime.Offset)
				: CrystalJsonNodaPatterns.Offsets.Parse(value).Value;
		}

		public NodaTime.LocalDate ToLocalDate()
		{
			string value = m_value;
			return string.IsNullOrEmpty(value)
				? default(NodaTime.LocalDate)
				: CrystalJsonNodaPatterns.LocalDates.Parse(value).Value;
		}

		#endregion

		#region Enums

		public override TEnum ToEnum<TEnum>()
		{
			return string.IsNullOrEmpty(m_value) ? default(TEnum) : (TEnum) Enum.Parse(typeof (TEnum), m_value, true);
		}

		public override TEnum? ToEnumOrDefault<TEnum>()
		{
			return string.IsNullOrEmpty(m_value) ? default(TEnum?) : (TEnum)Enum.Parse(typeof(TEnum), m_value, true);
		}

		#endregion

		#region Uri

		public Uri? ToUri()
		{
			//note: new Uri("") n'est pas valid, donc on retourne null si c'est le cas...
			return string.IsNullOrEmpty(m_value) ? default(Uri) : new Uri(m_value);
		}

		#endregion

		#endregion

		public override void WriteTo(ref SliceWriter writer)
		{
			var value = m_value;
			if (string.IsNullOrEmpty(value))
			{ // "" => 22 22
				writer.WriteFixed16(0x2222);
				return;
			}

			//TODO: version "optimis�e!"
			if (JsonEncoding.NeedsEscaping(value))
			{
				writer.WriteStringUtf8(JsonEncoding.EncodeSlow(value));
				return;
			}

			writer.WriteByte('"');
			writer.WriteStringUtf8(m_value);
			writer.WriteByte('"');
		}
	}

}