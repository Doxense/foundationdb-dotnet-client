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
	using System.Globalization;
	using Doxense.Collections.Caching;
	using Doxense.Memory;
	using Doxense.Tools;

	/// <summary>JSON string literal</summary>
	[DebuggerDisplay("JSON String({" + nameof(m_value) + "})")]
	[DebuggerNonUserCode]
	[PublicAPI]
	public sealed class JsonString : JsonValue,
		IEquatable<JsonString>,
		IEquatable<string>,
		IEquatable<Guid>,
		IEquatable<Uuid128>,
		IEquatable<Uuid96>,
		IEquatable<Uuid80>,
		IEquatable<Uuid64>,
		IEquatable<DateTime>,
		IEquatable<DateTimeOffset>,
		IEquatable<DateOnly>,
		IEquatable<TimeOnly>,
		IEquatable<NodaTime.LocalDateTime>,
		IEquatable<NodaTime.LocalDate>
	{
		public static readonly JsonValue Empty = new JsonString(string.Empty);

		private readonly string m_value;

		internal JsonString(string value)
		{
			Contract.Debug.Requires(value is not null);
			m_value = value;
		}

		#region Factory...

		public static JsonValue Return(string? value)
		{
			return value is null ? JsonNull.Null : value.Length == 0 ? JsonString.Empty : new JsonString(value);
		}

		public static JsonValue Return(ReadOnlySpan<char> value)
		{
			return value.Length == 0 ? JsonString.Empty : new JsonString(value.ToString());
		}

		public static JsonValue Return(System.Text.StringBuilder? value)
		{
			return value is null ? JsonNull.Null : value.Length == 0 ? JsonString.Empty : new JsonString(value.ToString());
		}

		public static JsonString Return(char value)
		{
			//note: pas vraiment d'intérêt à optimiser, je ne pense pas que des chaînes d'un seul caractère soit si fréquentes que ça...
			return new JsonString(new string(value, 1));
		}

		public static JsonValue Return(char? value)
		{
			//note: pas vraiment d'intérêt à optimiser, je ne pense pas que des chaînes d'un seul caractère soit si fréquentes que ça...
			return value is null ? JsonNull.Null : Return(value.Value);
		}

		public static JsonValue Return(char[]? value, int offset, int count)
		{
			return count == 0 ? (value is null ? JsonNull.Null : JsonString.Empty) : new JsonString(new string(value!, offset, count));
		}

		public static JsonValue Return(byte[]? value)
		{
			return value is null ? JsonNull.Null : value.Length == 0 ? JsonString.Empty : new JsonString(Convert.ToBase64String(value));
		}

		public static JsonValue Return(Slice value)
		{
			return value.Count == 0
				// ReSharper disable once ConditionIsAlwaysTrueOrFalseAccordingToNullableAPIContract
				? (value.Array is null ? JsonNull.Null : JsonString.Empty)
				: new JsonString(value.ToBase64()!);
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
				? (value.Array is null ? JsonNull.Null : JsonString.Empty)
				: new JsonString(Convert.ToBase64String(value.Array!, value.Offset, value.Count));
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
			return value is not null
				? new JsonString(value.ToString())
				: JsonNull.Null;
		}

		public static JsonValue Return(Uri? value)
		{
			return value is not null
				? new JsonString(value.OriginalString)
				: JsonNull.Null;
		}

		public static JsonValue Return(Version? value)
		{
			return value is not null
				? new JsonString(value.ToString())
				: JsonNull.Null;
		}

		internal static class TypeNameCache
		{

			private static readonly QuasiImmutableCache<Type, JsonString> Names = CreateDefaultCachedTypes();

			[ContractAnnotation("type:notnull => notnull")]
			public static JsonString? FromType(Type? type)
			{
				if (type is null) return null;
				return Names.TryGetValue(type, out var value)
					? value
					: CreateTypeNameSingleton(type);
			}

			private static QuasiImmutableCache<Type, JsonString> CreateDefaultCachedTypes()
			{
				var map = new Dictionary<Type, JsonString>(TypeEqualityComparer.Default)
				{
					// basic types
					[typeof(object)] = new("object"),
					[typeof(string)] = new("string"),
					[typeof(bool)] = new("bool"),
					[typeof(char)] = new("char"),
					[typeof(sbyte)] = new("sbyte"),
					[typeof(short)] = new("short"),
					[typeof(int)] = new("int"),
					[typeof(long)] = new("long"),
					[typeof(byte)] = new("byte"),
					[typeof(ushort)] = new("ushort"),
					[typeof(uint)] = new("uint"),
					[typeof(ulong)] = new("ulong"),
					[typeof(float)] = new("float"),
					[typeof(double)] = new("double"),
					[typeof(decimal)] = new("decimal"),
					[typeof(TimeSpan)] = new("TimeSpan"),
					[typeof(DateTime)] = new("DateTime"),
					[typeof(DateTimeOffset)] = new("DateTimeOffset"),
					[typeof(DateOnly)] = new("DateOnly"),
					[typeof(TimeOnly)] = new("TimeOnly"),
					[typeof(Guid)] = new("Guid"),
					// nullable types
					[typeof(bool?)] = new("bool?"),
					[typeof(char?)] = new("char?"),
					[typeof(sbyte?)] = new("sbyte?"),
					[typeof(short?)] = new("short?"),
					[typeof(int?)] = new("int?"),
					[typeof(long?)] = new("long?"),
					[typeof(byte?)] = new("byte?"),
					[typeof(ushort?)] = new("ushort?"),
					[typeof(uint?)] = new("uint?"),
					[typeof(ulong?)] = new("ulong?"),
					[typeof(float?)] = new("float?"),
					[typeof(double?)] = new("double?"),
					[typeof(decimal?)] = new("decimal?"),
					[typeof(TimeSpan?)] = new("TimeSpan?"),
					[typeof(DateTime?)] = new("DateTime?"),
					[typeof(DateTimeOffset?)] = new("DateTimeOffset?"),
					[typeof(DateOnly?)] = new("DateOnly?"),
					[typeof(TimeOnly?)] = new("TimeOnly?"),
					[typeof(Guid?)] = new("Guid?"),
					// system types
					[typeof(Uri)] = new(typeof(Uri).FullName!),
					[typeof(Version)] = new(typeof(Version).FullName!),
					[typeof(Exception)] = new(typeof(Exception).FullName!),
					//
				};
				return new(map);
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

		[Pure, MethodImpl(MethodImplOptions.NoInlining)]
		public static JsonValue Return(DateTime value)
		{
			return value == DateTime.MinValue ? JsonString.Empty : new JsonString(value.ToString("O"));
		}

		[Pure, MethodImpl(MethodImplOptions.NoInlining)]
		public static JsonValue Return(DateTime? value)
		{
			return value is null ? JsonNull.Null : value.Value == DateTime.MinValue ? JsonString.Empty : new JsonString(value.Value.ToString("O"));
		}

		[Pure, MethodImpl(MethodImplOptions.NoInlining)]
		public static JsonValue Return(DateTimeOffset value)
		{
			return value == DateTime.MinValue ? JsonString.Empty : new JsonString(value.ToString("O"));
		}

		[Pure, MethodImpl(MethodImplOptions.NoInlining)]
		public static JsonValue Return(DateTimeOffset? value)
		{
			return value is null ? JsonNull.Null : value.Value == DateTime.MinValue ? JsonString.Empty : new JsonString(value.Value.ToString("O"));
		}

		[Pure, MethodImpl(MethodImplOptions.NoInlining)]
		public static JsonValue Return(DateOnly value)
		{
			return value == DateOnly.MinValue ? JsonString.Empty : new JsonString(value.ToString("O"));
		}

		[Pure, MethodImpl(MethodImplOptions.NoInlining)]
		public static JsonValue Return(DateOnly? value)
		{
			return value is null ? JsonNull.Null : value.Value == DateOnly.MinValue ? JsonString.Empty : new JsonString(value.Value.ToString("O"));
		}

		[Pure, MethodImpl(MethodImplOptions.NoInlining)]
		public static JsonValue Return(TimeOnly value)
		{
			return value == TimeOnly.MinValue ? JsonString.Empty : new JsonString(value.ToString("O"));
		}

		[Pure, MethodImpl(MethodImplOptions.NoInlining)]
		public static JsonValue Return(TimeOnly? value)
		{
			return value is null ? JsonNull.Null : value.Value == TimeOnly.MinValue ? JsonString.Empty : new JsonString(value.Value.ToString("O"));
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

		public static JsonValue Return(NodaTime.DateTimeZone? value)
		{
			return value is not null
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

		public override bool IsReadOnly => true; //note: strings are immutable

		public byte[] ToBuffer() //REVIEW: => GetBytes()? DecodeBase64() ?
		{
			return m_value.Length != 0 ? Convert.FromBase64String(m_value) : [ ];
		}

		public override object ToObject()
		{
			return m_value;
		}

		public override T? Bind<T>(T? defaultValue = default, ICrystalJsonTypeResolver? resolver = null) where T : default
		{
			#region <JIT_HACK>
			// pattern recognized and optimized by the JIT, only in Release build

			if (default(T) is not null)
			{
				if (typeof(T) == typeof(bool)) return (T) (object) ToBoolean();
				if (typeof(T) == typeof(byte)) return (T) (object) ToByte();
				if (typeof(T) == typeof(sbyte)) return (T) (object) ToSByte();
				if (typeof(T) == typeof(char)) return (T) (object) ToChar();
				if (typeof(T) == typeof(short)) return (T) (object) ToInt16();
				if (typeof(T) == typeof(ushort)) return (T) (object) ToUInt16();
				if (typeof(T) == typeof(int)) return (T) (object) ToInt32();
				if (typeof(T) == typeof(uint)) return (T) (object) ToUInt32();
				if (typeof(T) == typeof(ulong)) return (T) (object) ToUInt64();
				if (typeof(T) == typeof(long)) return (T) (object) ToInt64();
				if (typeof(T) == typeof(float)) return (T) (object) ToSingle();
				if (typeof(T) == typeof(double)) return (T) (object) ToDouble();
				if (typeof(T) == typeof(decimal)) return (T) (object) ToDecimal();
				if (typeof(T) == typeof(TimeSpan)) return (T) (object) ToTimeSpan();
				if (typeof(T) == typeof(DateTime)) return (T) (object) ToDateTime();
				if (typeof(T) == typeof(DateTimeOffset)) return (T) (object) ToDateTimeOffset();
				if (typeof(T) == typeof(DateOnly)) return (T) (object) ToDateOnly();
				if (typeof(T) == typeof(TimeOnly)) return (T) (object) ToTimeOnly();
				if (typeof(T) == typeof(Guid)) return (T) (object) ToGuid();
				if (typeof(T) == typeof(Uuid128)) return (T) (object) ToUuid128();
				if (typeof(T) == typeof(Uuid96)) return (T) (object) ToUuid96();
				if (typeof(T) == typeof(Uuid80)) return (T) (object) ToUuid80();
				if (typeof(T) == typeof(Uuid64)) return (T) (object) ToUuid64();
				if (typeof(T) == typeof(NodaTime.Instant)) return (T) (object) ToInstant();
				if (typeof(T) == typeof(NodaTime.Duration)) return (T) (object) ToDuration();
				if (typeof(T) == typeof(NodaTime.LocalDateTime)) return (T) (object) ToLocalDateTime();
				if (typeof(T) == typeof(NodaTime.LocalDate)) return (T) (object) ToLocalDate();
				if (typeof(T) == typeof(NodaTime.Offset)) return (T) (object) ToOffset();
				if (typeof(T) == typeof(Half)) return (T) (object) ToHalf();
#if NET8_0_OR_GREATER
				if (typeof(T) == typeof(Int128)) return (T) (object) ToInt128();
				if (typeof(T) == typeof(UInt128)) return (T) (object) ToUInt128();
#endif

				return (T?) Bind(typeof(T), resolver);
			}
			else
			{
				if (typeof(T) == typeof(string)) return (T) (object) m_value;

				if (typeof(T) == typeof(bool?)) return (T?) (object?) ToBooleanOrDefault((bool?) (object?) defaultValue);
				if (typeof(T) == typeof(byte?)) return (T?) (object?) ToByteOrDefault((byte?) (object?) defaultValue);
				if (typeof(T) == typeof(sbyte?)) return (T?) (object?) ToSByteOrDefault((sbyte?) (object?) defaultValue);
				if (typeof(T) == typeof(char?)) return (T?) (object?) ToCharOrDefault((char?) (object?) defaultValue);
				if (typeof(T) == typeof(short?)) return (T?) (object?) ToInt16OrDefault((short?) (object?) defaultValue);
				if (typeof(T) == typeof(ushort?)) return (T?) (object?) ToUInt16OrDefault((ushort?) (object?) defaultValue);
				if (typeof(T) == typeof(int?)) return (T?) (object?) ToInt32OrDefault((int?) (object?) defaultValue);
				if (typeof(T) == typeof(uint?)) return (T?) (object?) ToUInt32OrDefault((uint?) (object?) defaultValue);
				if (typeof(T) == typeof(ulong?)) return (T?) (object?) ToUInt64OrDefault((ulong?) (object?) defaultValue);
				if (typeof(T) == typeof(long?)) return (T?) (object?) ToInt64OrDefault((long?) (object?) defaultValue);
				if (typeof(T) == typeof(float?)) return (T?) (object?) ToSingleOrDefault((float?) (object?) defaultValue);
				if (typeof(T) == typeof(double?)) return (T?) (object?) ToDoubleOrDefault((double?) (object?) defaultValue);
				if (typeof(T) == typeof(decimal?)) return (T?) (object?) ToDecimalOrDefault((decimal?) (object?) defaultValue);
				if (typeof(T) == typeof(TimeSpan?)) return (T?) (object?) ToTimeSpanOrDefault((TimeSpan?) (object?) defaultValue);
				if (typeof(T) == typeof(DateTime?)) return (T?) (object?) ToDateTimeOrDefault((DateTime?) (object?) defaultValue);
				if (typeof(T) == typeof(DateTimeOffset?)) return (T?) (object?) ToDateTimeOffsetOrDefault((DateTimeOffset?) (object?) defaultValue);
				if (typeof(T) == typeof(DateOnly?)) return (T?) (object?) ToDateOnlyOrDefault((DateOnly?) (object?) defaultValue);
				if (typeof(T) == typeof(TimeOnly?)) return (T?) (object?) ToTimeOnlyOrDefault((TimeOnly?) (object?) defaultValue);
				if (typeof(T) == typeof(Guid?)) return (T?) (object?) ToGuidOrDefault((Guid?) (object?) defaultValue);
				if (typeof(T) == typeof(Uuid128?)) return (T?) (object?) ToUuid128OrDefault((Uuid128?) (object?) defaultValue);
				if (typeof(T) == typeof(Uuid96?)) return (T?) (object?) ToUuid96OrDefault((Uuid96?) (object?) defaultValue);
				if (typeof(T) == typeof(Uuid80?)) return (T?) (object?) ToUuid80OrDefault((Uuid80?) (object?) defaultValue);
				if (typeof(T) == typeof(Uuid64?)) return (T?) (object?) ToUuid64OrDefault((Uuid64?) (object?) defaultValue);
				if (typeof(T) == typeof(NodaTime.Instant?)) return (T?) (object?) ToInstantOrDefault((NodaTime.Instant?) (object?) defaultValue);
				if (typeof(T) == typeof(NodaTime.Duration?)) return (T?) (object?) ToDurationOrDefault((NodaTime.Duration?) (object?) defaultValue);
				if (typeof(T) == typeof(NodaTime.LocalDateTime?)) return (T?) (object?) ToLocalDateTime();
				if (typeof(T) == typeof(NodaTime.LocalDate?)) return (T?) (object?) ToLocalDate();
				if (typeof(T) == typeof(NodaTime.Offset?)) return (T?) (object?) ToOffset();
				if (typeof(T) == typeof(Half?)) return (T?) (object?) ToHalfOrDefault((Half?) (object?) defaultValue);
#if NET8_0_OR_GREATER
				if (typeof(T) == typeof(Int128?)) return (T?) (object?) ToInt128OrDefault((Int128?) (object?) defaultValue);
				if (typeof(T) == typeof(UInt128?)) return (T?) (object?) ToUInt128OrDefault((UInt128?) (object?) defaultValue);
#endif

				return (T?) (Bind(typeof(T), resolver) ?? defaultValue);
			}

			#endregion

		}

		public override object? Bind(Type? type, ICrystalJsonTypeResolver? resolver = null)
		{
			if (type is null || typeof(string) == type || typeof(object) == type)
			{
				//TODO: si object, heuristics pour convertir en DateTime ?
				return m_value;
			}

			if (type.IsAssignableTo(typeof(JsonValue)))
			{
				if (type == typeof(JsonValue) || type == typeof(JsonString))
				{
					return this;
				}
				throw JsonBindingException.CannotBindJsonStringToThisType(this, type);
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
						//note: decimal and DateTime are not 'IsPrimitive' !
					}
				}
				//TODO: utiliser un dictionnaire Type => Func<...> pour speeder le test?
				else if (typeof(DateTime) == type)
				{
					return ToDateTime();
				}
				else if (typeof(DateTimeOffset) == type)
				{
					return ToDateTimeOffset();
				}
				else if (type.IsEnum)
				{
					return Enum.Parse(type, m_value, true);
				}
				else if (typeof(decimal) == type)
				{
					return ToDecimal();
				}
				else if (typeof(Guid) == type)
				{
					return ToGuid();
				}
				else if (typeof(Uuid128) == type)
				{
					return ToUuid128();
				}
				else if (typeof(Uuid64) == type)
				{
					return ToUInt64();
				}
				else if (typeof(Uuid96) == type)
				{
					return ToUuid96();
				}
				else if (typeof(Uuid80) == type)
				{
					return ToUuid80();
				}
				else if (typeof(TimeSpan) == type)
				{
					return ToTimeSpan();
				}
				else if (typeof(DateOnly) == type)
				{
					return ToDateOnly();
				}
				else if (typeof(TimeOnly) == type)
				{
					return ToTimeOnly();
				}
				else if (typeof(char[]) == type)
				{
					return m_value.ToCharArray();
				}
				else if (typeof(byte[]) == type)
				{ // by convention should be a Base64 encoded string
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
				{
					return new System.Text.StringBuilder(m_value);
				}
				else if (typeof(System.Net.IPAddress) == type)
				{
					return m_value.Length != 0 ? System.Net.IPAddress.Parse(m_value) : null;
				}
				else if (typeof(Version) == type)
				{
					return m_value.Length != 0 ? Version.Parse(m_value) : null;
				}
				else if (typeof(Uri) == type)
				{
					return ToUri();
				}

				#region NodaTime types...

				if (typeof(NodaTime.Instant) == type) return ToInstant();
				if (typeof(NodaTime.Duration) == type) return ToDuration();
				if (typeof(NodaTime.LocalDate) == type) return ToLocalDate();
				if (typeof(NodaTime.LocalDateTime) == type) return ToLocalDateTime();
				if (typeof(NodaTime.ZonedDateTime) == type) return ToZonedDateTime();
				if (typeof(NodaTime.OffsetDateTime) == type) return ToOffsetDateTime();
				if (typeof(NodaTime.Offset) == type) return ToOffset();
				if (typeof(NodaTime.LocalTime) == type) return CrystalJsonNodaPatterns.LocalTimes.Parse(m_value).Value;
				if (typeof(NodaTime.DateTimeZone).IsAssignableFrom(type)) return ToDateTimeZone();

				#endregion

				//TODO: XmlNode ?

				var nullableType = Nullable.GetUnderlyingType(type);
				if (nullableType is not null)
				{
					//note: missing/null or "" returns default(T?), which is already null
					if (string.IsNullOrEmpty(m_value)) return null;
					return Bind(nullableType, resolver);
				}
			}
			catch (Exception e) when (!e.IsFatalError())
			{
				if (e is FormatException) throw new FormatException($"Failed to convert JSON string into {type.GetFriendlyName()}: {e.Message}");
				throw JsonBindingException.CannotBindJsonStringToThisType(this, type, e);
			}

			// check if implements IJsonBindable
#pragma warning disable CS0618 // Type or member is obsolete
			if (typeof(IJsonBindable).IsAssignableFrom(type))
			{ // HACKHACK: pour les type qui se sérialisent en string (par ex: Oid)
				var typeDef = resolver.ResolveJsonType(type);
				if (typeDef?.CustomBinder is not null)
				{
					return typeDef.CustomBinder(this, type, resolver);
				}
			}
#pragma warning restore CS0618 // Type or member is obsolete

			// does it use a custom binder?
			// => for classes with a ducktyped ctor, or static factory methods
			var def = resolver.ResolveJsonType(type);
			if (def?.CustomBinder is not null)
			{
				return def.CustomBinder(this, type, resolver);
			}

			throw JsonBindingException.CannotBindJsonStringToThisType(this, type);
		}

		internal override bool IsSmallValue() => m_value.Length <= 36; // guid!

		internal override bool IsInlinable() => m_value.Length <= 80;

		internal override string GetCompactRepresentation(int depth)
		{
			// depth 0, complete string up to 128 chars, then truncated with '[...]' in the middle if larger
			// depth 1: complete string up to  64 chars, then truncated with '[...]' in the middle if larger
			// depth 2: complete string up to  36 chars, then truncated with '[...]' in the middle if larger
			// depth 3: complete string up to  16 chars, then truncated with '[...]' in the middle if larger
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

		#region IJsonSerializable...

		public override void JsonSerialize(CrystalJsonWriter writer)
		{
			writer.WriteValue(m_value);
		}

		/// <inheritdoc />
		public override bool TryFormat(Span<char> destination, out int charsWritten, ReadOnlySpan<char> format, IFormatProvider? provider)
		{
			return JsonEncoding.TryEncodeTo(destination, m_value, out charsWritten);
		}

#if NET8_0_OR_GREATER

		/// <inheritdoc />
		public override bool TryFormat(Span<byte> destination, out int bytesWritten, ReadOnlySpan<char> format, IFormatProvider? provider)
		{
			return JsonEncoding.TryEncodeTo(destination, m_value, out bytesWritten);
		}

#endif



		#endregion

		#region IEquatable<...>...

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
				NodaTime.Instant t => Equals(t),
				_                  => false
			};
			//TODO: compare with int, long, ...?
		}

		public override bool Equals(JsonValue? obj)
		{
			if (ReferenceEquals(obj, this)) return true;
			if (obj is null) return false;
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
			return obj is not null && string.Equals(m_value, obj.m_value, StringComparison.Ordinal);
		}

		/// <inheritdoc />
		public override bool ValueEquals<TValue>(TValue? value, IEqualityComparer<TValue>? comparer = null) where TValue : default
		{
			if (default(TValue) is null)
			{
				if (value is null) return false;

				if (typeof(TValue) == typeof(DateTime?)) return Equals((DateTime) (object) value);
				if (typeof(TValue) == typeof(DateTimeOffset?)) return Equals((DateTimeOffset) (object) value);
				if (typeof(TValue) == typeof(DateOnly?)) return Equals((DateOnly) (object) value);
				if (typeof(TValue) == typeof(TimeOnly?)) return Equals((TimeOnly) (object) value);
				if (typeof(TValue) == typeof(Guid?)) return Equals((Guid) (object) value);
				if (typeof(TValue) == typeof(Uuid128?)) return Equals((Uuid128) (object) value);
				if (typeof(TValue) == typeof(Uuid96?)) return Equals((Uuid96) (object) value);
				if (typeof(TValue) == typeof(Uuid80?)) return Equals((Uuid80) (object) value);
				if (typeof(TValue) == typeof(Uuid64?)) return Equals((Uuid64) (object) value);
				if (typeof(TValue) == typeof(NodaTime.Instant?)) return Equals((NodaTime.Instant) (object) value);
				if (typeof(TValue) == typeof(NodaTime.LocalDate?)) return Equals((NodaTime.LocalDate) (object) value);

				if (typeof(TValue) == typeof(string)) return Equals(Unsafe.As<string>(value));

				if (value is JsonValue j) return Equals(j);
			}
			else
			{
				if (typeof(TValue) == typeof(DateTime)) return Equals((DateTime) (object) value!);
				if (typeof(TValue) == typeof(DateTimeOffset)) return Equals((DateTimeOffset) (object) value!);
				if (typeof(TValue) == typeof(DateOnly)) return Equals((DateOnly) (object) value!);
				if (typeof(TValue) == typeof(TimeOnly)) return Equals((TimeOnly) (object) value!);
				if (typeof(TValue) == typeof(Guid)) return Equals((Guid) (object) value!);
				if (typeof(TValue) == typeof(Uuid128)) return Equals((Uuid128) (object) value!);
				if (typeof(TValue) == typeof(Uuid96)) return Equals((Uuid96) (object) value!);
				if (typeof(TValue) == typeof(Uuid80)) return Equals((Uuid80) (object) value!);
				if (typeof(TValue) == typeof(Uuid64)) return Equals((Uuid64) (object) value!);
				if (typeof(TValue) == typeof(NodaTime.Instant)) return Equals((NodaTime.Instant) (object) value!);
				if (typeof(TValue) == typeof(NodaTime.LocalDate)) return Equals((NodaTime.LocalDate) (object) value!);
			}

			return false;
		}

		public bool Equals(string? obj)
		{
			return obj is not null && string.Equals(m_value, obj, StringComparison.Ordinal);
		}

		public bool Equals(Guid obj) => ToGuid() == obj;

		public bool Equals(Uuid128 obj) => ToUuid128() == obj;

		public bool Equals(Uuid96 obj) => ToUuid96() == obj;

		public bool Equals(Uuid80 obj) => ToUuid80() == obj;

		public bool Equals(Uuid64 obj) => ToUuid64() == obj;

		public bool Equals(DateTime obj) => ToDateTime() == obj;

		public bool Equals(DateTimeOffset obj) => ToDateTimeOffset() == obj;

		public bool Equals(DateOnly obj) => ToDateOnly() == obj;

		public bool Equals(TimeOnly obj) => ToTimeOnly() == obj;

		public bool Equals(NodaTime.LocalDateTime obj) => ToLocalDateTime() == obj;

		public bool Equals(NodaTime.LocalDate obj) => ToLocalDate() == obj;

		public override int GetHashCode() => m_value.GetHashCode();

		public override int CompareTo(JsonValue? other)
		{
			if (other is null) return +1;
			switch (other.Type)
			{
				case JsonType.String: return CompareTo(other as JsonString);
				case JsonType.Number: return -((JsonNumber)other).CompareTo(this);
				default: return base.CompareTo(other);
			}
		}

		public int CompareTo(JsonString? other)
		{
			return other is not null ? string.CompareOrdinal(m_value, other.Value) : +1;
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

		public override string ToString() => m_value;

		[ContractAnnotation("=> notnull")]
		public override string ToStringOrDefault(string? defaultValue = null) => m_value;

		public bool TryConvertString(out string value)
		{
			value = m_value;
			return true;
		}

		#endregion

		#region Boolean

		public override bool ToBoolean(bool defaultValue = false)
		{
			return !string.IsNullOrEmpty(m_value) ? bool.Parse(m_value) : defaultValue;
		}

		public override bool? ToBooleanOrDefault(bool? defaultValue = null)
		{
			return string.IsNullOrEmpty(m_value) ? defaultValue : bool.Parse(m_value);
		}

		#endregion

		#region Byte

		public override byte ToByte(byte defaultValue = default) => string.IsNullOrEmpty(m_value) ? defaultValue : byte.Parse(m_value, NumberFormatInfo.InvariantInfo);

		public override byte? ToByteOrDefault(byte? defaultValue = null) => string.IsNullOrEmpty(m_value) ? defaultValue : byte.Parse(m_value, NumberFormatInfo.InvariantInfo);

		#endregion

		#region SByte

		public override sbyte ToSByte(sbyte defaultValue = default)
		{
			return string.IsNullOrEmpty(m_value) ? defaultValue : sbyte.Parse(m_value, NumberFormatInfo.InvariantInfo);
		}

		public override sbyte? ToSByteOrDefault(sbyte? defaultValue = null)
		{
			return string.IsNullOrEmpty(m_value) ? defaultValue : sbyte.Parse(m_value, NumberFormatInfo.InvariantInfo);
		}

		#endregion

		#region Char

		public override char ToChar(char defaultValue = default)
		{
			return string.IsNullOrEmpty(m_value) ? defaultValue : m_value[0];
		}

		public override char? ToCharOrDefault(char? defaultValue = null)
		{
			return string.IsNullOrEmpty(m_value) ? defaultValue : m_value[0];
		}

		#endregion

		#region Int16

		public override short ToInt16(short defaultValue = default)
		{
			return string.IsNullOrEmpty(m_value) ? defaultValue : short.Parse(m_value, NumberFormatInfo.InvariantInfo);
		}

		public override short? ToInt16OrDefault(short? defaultValue = null)
		{
			return string.IsNullOrEmpty(m_value) ? defaultValue : ToInt16();
		}

		public bool TryConvertInt16(out short value)
		{
			return short.TryParse(m_value, NumberStyles.Integer, NumberFormatInfo.InvariantInfo, out value);
		}

		#endregion

		#region UInt16

		public override ushort ToUInt16(ushort defaultValue = default)
		{
			return string.IsNullOrEmpty(m_value) ? defaultValue : ushort.Parse(m_value, NumberFormatInfo.InvariantInfo);
		}

		public override ushort? ToUInt16OrDefault(ushort? defaultValue = null)
		{
			return string.IsNullOrEmpty(m_value) ? defaultValue : ToUInt16();
		}

		public bool TryConvertUInt16(out ushort value)
		{
			return ushort.TryParse(m_value, NumberStyles.Integer, NumberFormatInfo.InvariantInfo, out value);
		}

		#endregion

		#region Int32

		public override int ToInt32(int defaultValue = default)
		{
			var value = m_value;
			if (string.IsNullOrEmpty(value))
			{
				return defaultValue;
			}

			if (value.Length == 1)
			{ // the most frequent case is a number between 0 and 9
				char c = value[0];
				if (c >= '0' & c <= '9') return c - '0';
			}
			//TODO: PERF: faster parsing ?
			return int.Parse(value, NumberFormatInfo.InvariantInfo);
		}

		public override int? ToInt32OrDefault(int? defaultValue = null) => string.IsNullOrEmpty(m_value) ? defaultValue : ToInt32();

		public bool TryConvertInt32(out int value)
		{
			var lit = m_value;
			if (lit.Length == 0) { value = 0; return false; }
			if (lit.Length == 1)
			{ // the most frequent case is a number between 0 and 9
				char c = lit[0];
				if (c >= '0' & c <= '9') { value = c - '0'; return true; }
			}
			//TODO: PERF: faster parsing ?
			return int.TryParse(lit, NumberStyles.Integer, NumberFormatInfo.InvariantInfo, out value);
		}

		#endregion

		#region UInt32

		public override uint ToUInt32(uint defaultValue = default)
		{
			return string.IsNullOrEmpty(m_value) ? defaultValue : uint.Parse(m_value, NumberFormatInfo.InvariantInfo);
		}

		public override uint? ToUInt32OrDefault(uint? defaultValue = null)
		{
			return string.IsNullOrEmpty(m_value) ? defaultValue : ToUInt32();
		}

		public bool TryConvertUInt32(out uint value)
		{
			return uint.TryParse(m_value, NumberStyles.Integer, NumberFormatInfo.InvariantInfo, out value);
		}

		#endregion

		#region Int64

		public override long ToInt64(long defaultValue = default)
		{
			var value = m_value;
			if (string.IsNullOrEmpty(value))
			{
				return defaultValue;
			}

			if (value.Length == 1)
			{ // the most frequent case is a number between 0 and 9
				char c = value[0];
				if (c >= '0' & c <= '9') return c - '0';
			}

			//TODO: PERF: faster parsing ?
			return long.Parse(value, NumberFormatInfo.InvariantInfo);
		}

		public override long? ToInt64OrDefault(long? defaultValue = null)
		{
			return string.IsNullOrEmpty(m_value) ? defaultValue : ToInt64();
		}

		public bool TryConvertInt64(out long value)
		{
			var lit = m_value;
			if (lit.Length == 0) { value = 0L; return false; }
			if (lit.Length == 1)
			{ // le cas le plus fréquent est un nombre de 0 à 9
				char c = lit[0];
				if (c >= '0' & c <= '9') { value = c - '0'; return true; }
			}
			// note: NumberStyles obtenus via Reflector
			return long.TryParse(lit, NumberStyles.Integer, NumberFormatInfo.InvariantInfo, out value);
		}

		#endregion

		#region UInt64

		public override ulong ToUInt64(ulong defaultValue = default)
		{
			return string.IsNullOrEmpty(m_value) ? defaultValue : ulong.Parse(m_value, NumberFormatInfo.InvariantInfo);
		}

		public override ulong? ToUInt64OrDefault(ulong? defaultValue = null)
		{
			return string.IsNullOrEmpty(m_value) ? defaultValue : ToUInt64();
		}

		public bool TryConvertUInt64(out ulong value)
		{
			return ulong.TryParse(m_value, NumberStyles.Integer, NumberFormatInfo.InvariantInfo, out value);
		}

		#endregion

		#region Int128

#if NET8_0_OR_GREATER

		public override Int128 ToInt128(Int128 defaultValue = default)
		{
			return string.IsNullOrEmpty(m_value) ? defaultValue : Int128.Parse(m_value, NumberFormatInfo.InvariantInfo);
		}

		public override Int128? ToInt128OrDefault(Int128? defaultValue = null)
		{
			return string.IsNullOrEmpty(m_value) ? defaultValue : ToInt128();
		}

		public bool TryConvertInt128(out Int128 value)
		{
			return Int128.TryParse(m_value, NumberStyles.Integer, NumberFormatInfo.InvariantInfo, out value);
		}

#endif

		#endregion

		#region UInt128

#if NET8_0_OR_GREATER


		public override UInt128 ToUInt128(UInt128 defaultValue = default)
		{
			return string.IsNullOrEmpty(m_value) ? defaultValue : UInt128.Parse(m_value, NumberFormatInfo.InvariantInfo);
		}

		public override UInt128? ToUInt128OrDefault(UInt128? defaultValue = null)
		{
			return string.IsNullOrEmpty(m_value) ? defaultValue : ToUInt128();
		}

		public bool TryConvertUInt128(out UInt128 value)
		{
			return UInt128.TryParse(m_value, NumberStyles.Integer, NumberFormatInfo.InvariantInfo, out value);
		}

#endif

		#endregion

		#region Single

		public override float ToSingle(float defaultValue = default)
		{
			return string.IsNullOrEmpty(m_value) ? defaultValue : float.Parse(m_value, NumberFormatInfo.InvariantInfo);
		}

		public override float? ToSingleOrDefault(float? defaultValue = null)
		{
			return string.IsNullOrEmpty(m_value) ? defaultValue : ToSingle();
		}

		public bool TryConvertSingle(out float value)
		{
			return float.TryParse(m_value, NumberStyles.Float | NumberStyles.AllowThousands, NumberFormatInfo.InvariantInfo, out value);
		}

		#endregion

		#region Double

		public override double ToDouble(double defaultValue = default)
		{
			return string.IsNullOrEmpty(m_value) ? defaultValue : double.Parse(m_value, NumberFormatInfo.InvariantInfo);
		}

		public override double? ToDoubleOrDefault(double? defaultValue = null)
		{
			return string.IsNullOrEmpty(m_value) ? defaultValue : ToDouble();
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool TryConvertDouble(out double value)
		{
			return TryConvertDouble(m_value, out value);
		}

		internal static bool TryConvertDouble(string literal, out double value)
		{
			return double.TryParse(literal, NumberStyles.Float | NumberStyles.AllowThousands, NumberFormatInfo.InvariantInfo, out value);
		}

		#endregion

		#region Half

		public override Half ToHalf(Half defaultValue = default)
		{
			return string.IsNullOrEmpty(m_value) ? defaultValue : Half.Parse(m_value, NumberFormatInfo.InvariantInfo);
		}

		public override Half? ToHalfOrDefault(Half? defaultValue = null)
		{
			return string.IsNullOrEmpty(m_value) ? defaultValue : Half.Parse(m_value, NumberFormatInfo.InvariantInfo);
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool TryConvertHalf(out Half value)
		{
			return TryConvertHalf(m_value, out value);
		}

		internal static bool TryConvertHalf(string literal, out Half value)
		{
			return Half.TryParse(literal, NumberStyles.Float | NumberStyles.AllowThousands, NumberFormatInfo.InvariantInfo, out value);
		}

		#endregion

		#region Decimal

		public override decimal ToDecimal(decimal defaultValue = default)
		{
			return string.IsNullOrEmpty(m_value) ? defaultValue : decimal.Parse(m_value, NumberFormatInfo.InvariantInfo);
		}

		public override decimal? ToDecimalOrDefault(decimal? defaultValue = null)
		{
			return string.IsNullOrEmpty(m_value) ? defaultValue : ToDecimal();
		}

		public bool TryConvertDecimal(out decimal value)
		{
			return decimal.TryParse(m_value, NumberStyles.Number, NumberFormatInfo.InvariantInfo, out value);
		}

		#endregion

		#region Guid

		public override Guid ToGuid(Guid defaultValue = default)
		{
			return string.IsNullOrEmpty(m_value) ? defaultValue : Guid.Parse(m_value);
		}

		public override Guid? ToGuidOrDefault(Guid? defaultValue = null)
		{
			return string.IsNullOrEmpty(m_value) ? defaultValue : Guid.Parse(m_value);
		}

		public bool TryConvertGuid(out Guid value)
		{
			if (m_value.Length == 36 /* 00000000-0000-0000-0000-000000000000 */ ||
				m_value.Length == 32 /* 00000000000000000000000000000000 */ ||
				m_value.Length == 38 /* {00000000-0000-0000-0000-000000000000} */)
			{
				return Guid.TryParse(m_value, out value);
			}
			value = default;
			return false;
		}

		public override Uuid128 ToUuid128(Uuid128 defaultValue = default) => string.IsNullOrEmpty(m_value) ? defaultValue : Uuid128.Parse(m_value);

		public override Uuid128? ToUuid128OrDefault(Uuid128? defaultValue = null) => string.IsNullOrEmpty(m_value) ? defaultValue : Uuid128.Parse(m_value);

		public override Uuid96 ToUuid96(Uuid96 defaultValue = default) => string.IsNullOrEmpty(m_value) ? defaultValue : Uuid96.Parse(m_value);

		public override Uuid96? ToUuid96OrDefault(Uuid96? defaultValue = null) => string.IsNullOrEmpty(m_value) ? defaultValue : Uuid96.Parse(m_value);

		public override Uuid80 ToUuid80(Uuid80 defaultValue = default) => string.IsNullOrEmpty(m_value) ? defaultValue : Uuid80.Parse(m_value);

		public override Uuid80? ToUuid80OrDefault(Uuid80? defaultValue = null) => string.IsNullOrEmpty(m_value) ? defaultValue : Uuid80.Parse(m_value);

		public override Uuid64 ToUuid64(Uuid64 defaultValue = default) => string.IsNullOrEmpty(m_value) ? defaultValue : Uuid64.Parse(m_value);

		public override Uuid64? ToUuid64OrDefault(Uuid64? defaultValue = null) => string.IsNullOrEmpty(m_value) ? defaultValue : Uuid64.Parse(m_value);

		#endregion

		#region DateTime

		public override DateTime ToDateTime(DateTime defaultValue = default)
		{
			if (string.IsNullOrEmpty(m_value))
			{
				return defaultValue;
			}

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

			return StringConverters.ParseDateTime(m_value);
		}

		public override DateTime? ToDateTimeOrDefault(DateTime? defaultValue = null)
		{
			return string.IsNullOrEmpty(m_value) ? defaultValue : ToDateTime();
		}

		public override DateOnly ToDateOnly(DateOnly defaultValue = default)
		{
			return string.IsNullOrEmpty(m_value) ? defaultValue : DateOnly.FromDateTime(ToDateTime());
		}

		public override DateOnly? ToDateOnlyOrDefault(DateOnly? defaultValue = null)
		{
			return string.IsNullOrEmpty(m_value) ? defaultValue : ToDateOnly();
		}

		public override TimeOnly ToTimeOnly(TimeOnly defaultValue = default)
		{
			return string.IsNullOrEmpty(m_value) ? defaultValue : TimeOnly.FromTimeSpan(ToTimeSpan());
		}

		public override TimeOnly? ToTimeOnlyOrDefault(TimeOnly? defaultValue = null)
		{
			return string.IsNullOrEmpty(m_value) ? defaultValue : ToTimeOnly();
		}

		public bool TryConvertDateTime(out DateTime dt)
		{
			return TryConvertDateTime(m_value, out dt);
		}

		internal static bool TryConvertDateTime(ReadOnlySpan<char> literal, out DateTime dt)
		{
			if (literal.Length == 0)
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

			return StringConverters.TryParseDateTime(literal, CultureInfo.InvariantCulture, out dt, false);
		}

		#endregion

		#region DateTimeOffset

		public override DateTimeOffset ToDateTimeOffset(DateTimeOffset  defaultValue = default)
		{
			if (m_value.Length == 0)
			{
				return defaultValue;
			}

			if (CrystalJsonParser.TryParseIso8601DateTimeOffset(m_value, out var dto))
			{
				return dto;
			}

			if (CrystalJsonParser.TryParseMicrosoftDateTime(m_value, out var d, out var tz))
			{
				if (tz.HasValue)
				{
					return new(d, tz.Value);
				}
				return new(d);
			}

			return new(StringConverters.ParseDateTime(m_value));
		}

		public override DateTimeOffset? ToDateTimeOffsetOrDefault(DateTimeOffset? defaultValue = null)
		{
			return string.IsNullOrEmpty(m_value) ? defaultValue : ToDateTimeOffset();
		}

		public bool TryConvertDateTimeOffset(out DateTimeOffset dto)
		{
			return TryConvertDateTimeOffset(m_value, out dto);
		}

		internal static bool TryConvertDateTimeOffset(ReadOnlySpan<char> literal, out DateTimeOffset dto)
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

		public override TimeSpan ToTimeSpan(TimeSpan defaultValue = default)
		{
			return string.IsNullOrEmpty(m_value) ? defaultValue : TimeSpan.Parse(m_value, CultureInfo.InvariantCulture);
		}

		public override TimeSpan? ToTimeSpanOrDefault(TimeSpan? defaultValue = null) => string.IsNullOrEmpty(m_value) ? defaultValue : ToTimeSpan();

		#endregion

		#region NodaTime

		public override NodaTime.Instant ToInstant(NodaTime.Instant defaultValue = default)
		{
			if (string.IsNullOrEmpty(m_value))
			{
				return defaultValue;
			}

			var parseResult = CrystalJsonNodaPatterns.Instants.Parse(m_value);
			if (parseResult.TryGetValue(default(NodaTime.Instant), out var instant))
			{
				return instant;
			}

			// this does not look like an "Instant", try going the DateTimeOffset route...
			var dateTimeOffset = ToDateTimeOffset();
			return NodaTime.Instant.FromDateTimeOffset(dateTimeOffset);
		}

		public override NodaTime.Instant? ToInstantOrDefault(NodaTime.Instant? defaultValue = null) => string.IsNullOrEmpty(m_value) ? defaultValue : ToInstant();

		public override NodaTime.Duration ToDuration(NodaTime.Duration defaultValue = default)
		{
			string value = m_value;
			return string.IsNullOrEmpty(value)
				? defaultValue
				: NodaTime.Duration.FromTicks((long) (double.Parse(value) * NodaTime.NodaConstants.TicksPerSecond));
		}

		public override NodaTime.Duration? ToDurationOrDefault(NodaTime.Duration? defaultValue = null) => string.IsNullOrEmpty(m_value) ? defaultValue : ToDuration();

		public NodaTime.LocalDateTime ToLocalDateTime(NodaTime.LocalDateTime defaultValue = default)
		{
			string value = m_value;
			if (string.IsNullOrEmpty(value))
			{
				return defaultValue;
			}

			if (value[^1] == 'Z')
			{ // this is a UTC date, probably an Instant
				//HACK: not pretty, but we assume the original intention was to store a local time, so we to a local time as well
				// => this will NOT work as intended if the server that serialized the JSON value is in a different timezone, but then the app should have used Instant, or a ZonedDateTime instead!
				return NodaTime.LocalDateTime.FromDateTime(ToInstant().ToDateTimeUtc().ToLocalTime());
			}

			return CrystalJsonNodaPatterns.LocalDateTimes.Parse(value).Value;
		}

		public NodaTime.LocalDateTime? ToLocalDateTimeOrDefault(NodaTime.LocalDateTime? defaultValue = null) => string.IsNullOrEmpty(m_value) ? defaultValue : ToLocalDateTime();

		public NodaTime.ZonedDateTime ToZonedDateTime(NodaTime.ZonedDateTime defaultValue = default)
		{
			string value = m_value;
			return string.IsNullOrEmpty(value) ? defaultValue : CrystalJsonNodaPatterns.ZonedDateTimes.Parse(value).Value;
		}

		public NodaTime.ZonedDateTime? ToZonedDateTimeOrDefault(NodaTime.ZonedDateTime? defaultValue = null) => string.IsNullOrEmpty(m_value) ? defaultValue : ToZonedDateTime();

		public NodaTime.OffsetDateTime ToOffsetDateTime(NodaTime.OffsetDateTime defaultValue = default)
		{
			string value = m_value;
			return string.IsNullOrEmpty(value) ? defaultValue : CrystalJsonNodaPatterns.OffsetDateTimes.Parse(value).Value;
		}

		public NodaTime.OffsetDateTime? ToOffsetDateTimeOrDefault(NodaTime.OffsetDateTime? defaultValue = null) => string.IsNullOrEmpty(m_value) ? defaultValue : ToOffsetDateTime();

		public NodaTime.DateTimeZone? ToDateTimeZone(NodaTime.DateTimeZone? defaultValue = default)
		{
			string value = m_value;
			return string.IsNullOrEmpty(value) ? defaultValue : (NodaTime.DateTimeZoneProviders.Tzdb.GetZoneOrNull(value));
		}

		public NodaTime.Offset ToOffset(NodaTime.Offset defaultValue = default)
		{
			string value = m_value;
			return string.IsNullOrEmpty(value) ? defaultValue : CrystalJsonNodaPatterns.Offsets.Parse(value).Value;
		}

		public NodaTime.Offset? ToOffsetOrDefault(NodaTime.Offset? defaultValue = null) => string.IsNullOrEmpty(m_value) ? defaultValue : ToOffset();

		public NodaTime.LocalDate ToLocalDate(NodaTime.LocalDate defaultValue = default)
		{
			string value = m_value;
			return string.IsNullOrEmpty(value) ? defaultValue : CrystalJsonNodaPatterns.LocalDates.Parse(value).Value;
		}

		public NodaTime.LocalDate? ToLocalDateOrDefault(NodaTime.LocalDate? defaultValue = null) => string.IsNullOrEmpty(m_value) ? defaultValue : ToLocalDate();

		#endregion

		#region Enums

		public override TEnum ToEnum<TEnum>(TEnum defaultValue = default) => string.IsNullOrEmpty(m_value) ? defaultValue : Enum.Parse<TEnum>(m_value, ignoreCase: true);

		public override TEnum? ToEnumOrDefault<TEnum>(TEnum? defaultValue = null) => string.IsNullOrEmpty(m_value) ? defaultValue : Enum.Parse<TEnum>(m_value, ignoreCase: true);

		#endregion

		#region Uri

		public Uri? ToUri()
		{
			//note: new Uri("") is not valid, so we return null if this is the case
			return string.IsNullOrEmpty(m_value) ? null : new Uri(m_value);
		}

		#endregion

		#endregion

		#region ISliceSerializable...

		public override void WriteTo(ref SliceWriter writer)
		{
			var value = m_value;
			if (string.IsNullOrEmpty(value))
			{ // "" => 22 22
				writer.WriteFixed16(0x2222);
				return;
			}

			//TODO: version "optimisée!"
			if (JsonEncoding.NeedsEscaping(value))
			{
				writer.WriteStringUtf8(JsonEncoding.EncodeSlow(value));
				return;
			}

			writer.WriteByte('"');
			writer.WriteStringUtf8(m_value);
			writer.WriteByte('"');
		}

		#endregion

		public static class WellKnown
		{
			/// <summary>PascalCase</summary>
			public static class PascalCase
			{
				/// <summary><c>"Id"</c></summary>
				public static readonly JsonString Id = new("Id");
				/// <summary><c>"Name"</c></summary>
				public static readonly JsonString Name = new("Name");
				/// <summary><c>"Error"</c></summary>
				public static readonly JsonString Error = new("Error");
			}

			/// <summary>camelCase</summary>
			public static class CamelCase
			{
				/// <summary><c>"id"</c></summary>
				public static readonly JsonString Id = new("id");
				/// <summary><c>"name"</c></summary>
				public static readonly JsonString Name = new("name");
				/// <summary><c>"error"</c></summary>
				public static readonly JsonString Error = new("error");
			}

		}

	}

}
