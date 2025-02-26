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
	using System.ComponentModel;
	using System.Globalization;
	using Doxense.Memory;

	/// <summary>JSON DateTime</summary>
	[DebuggerDisplay("JSON DateTime({m_value}, {m_value}+{m_offset})")]
	[DebuggerNonUserCode]
	[PublicAPI]
	public class JsonDateTime : JsonValue, IEquatable<JsonDateTime>, IEquatable<DateTime>, IEquatable<DateTimeOffset>, IEquatable<NodaTime.LocalDateTime>, IEquatable<NodaTime.LocalDate>
	{
		private const long UNIX_EPOCH_TICKS = 621355968000000000L;
		private const short NO_TIMEZONE = short.MinValue;

		#region Static Helpers...

		/// <summary>Singleton equivalent to <see cref="DateTime.MinValue"/></summary>
		public static readonly JsonDateTime MinValue = new(DateTime.MinValue, NO_TIMEZONE);
		
		/// <summary>Singleton equivalent to <see cref="DateTime.MinValue"/></summary>
		public static readonly JsonDateTime MaxValue = new(DateTime.MaxValue, NO_TIMEZONE);
		
		private static readonly DateTime MaxValueDate = new DateTime(3155378112000000000);
		
		/// <summary>Singleton equivalent to <see cref="DateOnly.MinValue"/></summary>
		public static readonly JsonDateTime DateOnlyMaxValue = new(MaxValueDate, NO_TIMEZONE);

		/// <summary>Précision des dates JSON (lié à la façon dont elles sont sérialisées)</summary>
		public static readonly long PrecisionTicks = TimeSpan.TicksPerMillisecond;

		/// <summary>Tests if two dates are considered equal within the minimum supported precision</summary>
		public static bool AreEqual(DateTime left, DateTime right)
		{
			return Math.Abs(left.Ticks - right.Ticks) <= PrecisionTicks;
		}

		/// <summary>Tests if two dates are considered equal within the minimum supported precision</summary>
		public static bool AreEqual(DateTimeOffset left, DateTimeOffset right)
		{
			return Math.Abs(left.Ticks - right.Ticks) <= PrecisionTicks;
		}

		#endregion

		#region Private Members...

		// Problème: DateTimeOffset "perd" l'information du DateTimeKind d'origine
		// cad que si le serveur est dans la TimeZone de Greenwith (== UTC), alors on ne saura pas dire si la DateTime d'origine était Kind.Utc ou Kind.Local

		/// <summary>Copy of the "m_dateTime" field of a DateTimeOffset, or the original DateTime value</summary>
		private readonly DateTime m_value;

		/// <summary>Copy of the "m_offsetMinutes" of a DateTimeOffset, or int.MinValue for DateTime</summary>
		private readonly short m_offset;

		// un DateTimeOffset est stocké sous forme d'une DateTime en UTC, accompagnée d'un offset
		// un DateTime de source locale (UTC ou Local) aura m_offset == NO_TIMEZONE
		// un DateTime de source distante (parsé) aura m_offset = NO_TIMEZONE s'il n'y avait pas d'infos dans le source, ou l'offset spécifié dans la source
		// => si on demande un DateTime dans une autre TZ on aura la date convertie en LocalTime du serveur courrant.

		#endregion

		#region Constructors...

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		internal JsonDateTime(DateTime value, short offset)
		{
			m_value = value;
			m_offset = offset;
		}

		/// <summary>Wrap un valeur de type DateTime</summary>
		/// <param name="value"></param>
		public JsonDateTime(DateTime value)
		{
			m_value = value;
			m_offset = NO_TIMEZONE;
		}

		/// <summary>Wrap une valeur de type DateTimeOffset</summary>
		/// <param name="value"></param>
		public JsonDateTime(DateTimeOffset value)
		{
			m_value = value.DateTime;
			m_offset = (short) value.Offset.TotalMinutes;
		}

		/// <summary>Wrap un valeur de type DateTime</summary>
		/// <param name="value"></param>
		public JsonDateTime(DateOnly value)
		{
			m_value = value.ToDateTime(default, DateTimeKind.Local);
			m_offset = NO_TIMEZONE;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public JsonDateTime(long ticks, DateTimeKind kind) : this(new DateTime(ticks, kind)) { }

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public JsonDateTime(int year, int month, int day) : this(new DateTime(year, month, day)) { }

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public JsonDateTime(int year, int month, int day, int hour, int minute, int second, DateTimeKind kind) : this(new DateTime(year, month, day, hour, minute, second, kind)) { }

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public JsonDateTime(int year, int month, int day, int hour, int minute, int second, int millisecond, DateTimeKind kind) : this(new DateTime(year, month, day, hour, minute, second, millisecond, kind)) { }

		/// <summary>Returns the equivalent <see cref="JsonDateTime"/></summary>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static JsonDateTime Return(DateTime value)
		{
			if (value == DateTime.MinValue) return JsonDateTime.MinValue;
			if (value == DateTime.MaxValue) return JsonDateTime.MaxValue;
			return new JsonDateTime(value);
		}

		/// <summary>Returns the equivalent <see cref="JsonDateTime"/></summary>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static JsonValue Return(DateTime? value)
		{
			return value.HasValue ? Return(value.Value) : JsonNull.Null;
		}

		/// <summary>Returns the equivalent <see cref="JsonDateTime"/></summary>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static JsonDateTime Return(DateTimeOffset value)
		{
			if (value == DateTimeOffset.MinValue) return JsonDateTime.MinValue;
			if (value == DateTimeOffset.MaxValue) return JsonDateTime.MaxValue;
			return new JsonDateTime(value);
		}

		/// <summary>Returns the equivalent <see cref="JsonDateTime"/></summary>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static JsonValue Return(DateTimeOffset? value)
		{
			return value.HasValue ? Return(value.Value) : JsonNull.Null;
		}

		/// <summary>Returns the equivalent <see cref="JsonDateTime"/></summary>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static JsonDateTime Return(DateOnly value)
		{
			if (value == DateOnly.MinValue) return JsonDateTime.MinValue;
			if (value == DateOnly.MaxValue) return JsonDateTime.DateOnlyMaxValue;
			return new JsonDateTime(value);
		}

		/// <summary>Returns the equivalent <see cref="JsonDateTime"/></summary>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static JsonValue Return(DateOnly? value)
		{
			return value.HasValue ? Return(value.Value) : JsonNull.Null;
		}

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static JsonValue Return(NodaTime.Instant value)
		{
			//TODO: support this type natively as well!
			return JsonString.Return(value);
		}

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static JsonValue Return(NodaTime.Instant? value)
		{
			//TODO: support this type natively as well!
			return JsonString.Return(value);
		}

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static JsonValue Return(NodaTime.ZonedDateTime value)
		{
			//TODO: support this type natively as well!
			return JsonString.Return(value);
		}

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static JsonValue Return(NodaTime.ZonedDateTime? value)
		{
			//TODO: support this type natively as well!
			return JsonString.Return(value);
		}

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static JsonValue Return(NodaTime.LocalDateTime value)
		{
			//TODO: support this type natively as well!
			return JsonString.Return(value);
		}

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static JsonValue Return(NodaTime.LocalDateTime? value)
		{
			//TODO: support this type natively as well!
			return JsonString.Return(value);
		}

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static JsonValue Return(NodaTime.LocalDate value)
		{
			//TODO: support this type natively as well!
			return JsonString.Return(value);
		}

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static JsonValue Return(NodaTime.LocalDate? value)
		{
			//TODO: support this type natively as well!
			return JsonString.Return(value);
		}

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static JsonValue Return(NodaTime.LocalTime value)
		{
			//TODO: support this type natively as well!
			return JsonString.Return(value);
		}

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static JsonValue Return(NodaTime.LocalTime? value)
		{
			//TODO: support this type natively as well!
			return JsonString.Return(value);
		}

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static JsonValue Return(NodaTime.OffsetDateTime value)
		{
			//TODO: support this type natively as well!
			return JsonString.Return(value);
		}

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static JsonValue Return(NodaTime.OffsetDateTime? value)
		{
			//TODO: support this type natively as well!
			return JsonString.Return(value);
		}

		#endregion

		#region Public Members...

		public long Ticks
		{
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			get => m_value.Ticks;
		}

		public long UtcTicks
		{
			get
			{
				if (m_offset == NO_TIMEZONE && m_value.Kind != DateTimeKind.Utc)
				{ // Date local, il faut la convertir
					return m_value.ToUniversalTime().Ticks;
				}
				//DTO: déja en UTC
				return m_value.Ticks;
			}
		}

		public DateTime Date
		{
			get
			{
				if (m_offset != NO_TIMEZONE)
				{ // DateTimeOffset, ou DateTime local à convertir en UTC
					return DateTime.SpecifyKind(m_value, DateTimeKind.Utc);
				}
				else
				{ // DateTime retournée telle quelle
					return m_value;
				}
			}
		}

		public DateTimeOffset DateWithOffset
		{
			get
			{
				if (m_offset == NO_TIMEZONE)
				{ // convert
					return new DateTimeOffset(m_value);
				}
				return new DateTimeOffset(m_value, TimeSpan.FromMinutes(m_offset));
			}
		}

		public DateOnly DateWithoutTime
		{
			get
			{
				if (m_offset != NO_TIMEZONE)
				{ // DateTimeOffset, ou DateTime local à convertir en UTC
					return DateOnly.FromDateTime(DateTime.SpecifyKind(m_value, DateTimeKind.Utc));
				}
				else
				{ // DateTime retournée telle quelle
					return DateOnly.FromDateTime(m_value);
				}
			}
		}

		public DateTime LocalDateTime => this.DateWithOffset.LocalDateTime;

		public DateTime UtcDateTime => this.DateWithOffset.UtcDateTime;

		public TimeSpan UtcOffset => m_offset != NO_TIMEZONE ? TimeSpan.FromMinutes(m_offset) : m_value.Kind == DateTimeKind.Utc ? TimeSpan.Zero : TimeZoneInfo.Local.GetUtcOffset(m_value);

		public bool HasOffset => m_offset != NO_TIMEZONE;

		/// <summary>Number of milliseconds since Unix Epoch  (1970-01-01 00:00:00.000 UTC)</summary>
		public long UnixTime => (this.UtcDateTime.Ticks - UNIX_EPOCH_TICKS) / TimeSpan.TicksPerMillisecond;
		//note: c'est un long pour ne pas avoir de problème avec le Y2038 bug (Unix Time Epoch Bug)

		/// <summary>Number of days since Unix Epoch (1970-01-01 00:00:00.000 UTC)</summary>
		public double UnixTimeDays => (this.UtcTicks - UNIX_EPOCH_TICKS) / (double) TimeSpan.TicksPerDay;
		//note: this should be safe from the Y2038 bug

		public bool IsLocalTime => m_offset == NO_TIMEZONE ? m_value.Kind == DateTimeKind.Local : m_offset != 0 /*TODO: compare with the local TZ ? */;

		public bool IsUtc => m_offset == NO_TIMEZONE ? m_value.Kind == DateTimeKind.Utc : m_offset == 0;

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public DateTime ToUniversalTime() => this.UtcDateTime;

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public DateTime ToLocalTime() => this.LocalDateTime;

		#endregion

		#region JsonValue Members...

		/// <inheritdoc />
		public override JsonType Type => JsonType.DateTime;

		/// <inheritdoc />
		public override bool IsDefault => m_value == DateTime.MinValue;

		/// <inheritdoc />
		public override bool IsReadOnly => true; //note: dates are immutable

		/// <inheritdoc />
		[RequiresUnreferencedCode("The type might be removed")]
		public override object ToObject() => m_value;

		/// <inheritdoc />
		[EditorBrowsable(EditorBrowsableState.Never)]
		[RequiresUnreferencedCode("The type might be removed")]
		[return: NotNullIfNotNull(nameof(defaultValue))]
		public override TValue? Bind<
			[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicParameterlessConstructor)] TValue>
			(TValue? defaultValue = default, ICrystalJsonTypeResolver? resolver = null) where TValue : default
		{
			#region <JIT_HACK>
			// pattern recognized and optimized by the JIT, only in Release build
			if (default(TValue) is not null)
			{
				if (typeof(TValue) == typeof(DateTime)) return (TValue) (object) ToDateTime((DateTime) (object) defaultValue!);
				if (typeof(TValue) == typeof(DateTimeOffset)) return (TValue) (object) ToDateTimeOffset((DateTimeOffset) (object) defaultValue!);
				if (typeof(TValue) == typeof(NodaTime.Instant)) return (TValue) (object) ToInstant((NodaTime.Instant) (object) defaultValue!);
				if (typeof(TValue) == typeof(DateOnly)) return (TValue) (object) ToDateOnly((DateOnly) (object) defaultValue!);
			}
			else
			{
				if (typeof(TValue) == typeof(DateTime?)) return (TValue?) (object?) ToDateTimeOrDefault((DateTime?) (object?) defaultValue);
				if (typeof(TValue) == typeof(DateTimeOffset?)) return (TValue?) (object?) ToDateTimeOffsetOrDefault((DateTimeOffset?) (object?) defaultValue);
				if (typeof(TValue) == typeof(NodaTime.Instant?)) return (TValue?) (object?) ToInstantOrDefault((NodaTime.Instant?) (object?) defaultValue);
				if (typeof(TValue) == typeof(DateOnly?)) return (TValue?) (object?) ToDateOnlyOrDefault((DateOnly?) (object?) defaultValue);

				if (typeof(TValue) == typeof(string)) return (TValue?) (object?) ToStringOrDefault((string?) (object?) defaultValue);
			}

			#endregion

			return (TValue?) BindNative(this, this.Ticks, typeof(TValue), resolver) ?? defaultValue;
		}

		/// <inheritdoc />
		[RequiresUnreferencedCode("The type might be removed")]
		public override object? Bind(
			[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicParameterlessConstructor)] Type? type,
			ICrystalJsonTypeResolver? resolver = null)
		{
			if (type == typeof(DateTimeOffset)) return this.DateWithOffset;
			if (type == typeof(DateTime)) return this.Date;
			if (type == typeof(NodaTime.Instant)) return NodaTime.Instant.FromDateTimeOffset(this.DateWithOffset);
			if (type == typeof(DateOnly)) return this.DateWithoutTime;
			if (type == typeof(string)) return ToStringOrDefault();
			if (type == typeof(DateTimeOffset?)) return this.DateWithOffset;
			if (type == typeof(DateTime?)) return this.Date;
			if (type == typeof(NodaTime.Instant?)) return NodaTime.Instant.FromDateTimeOffset(this.DateWithOffset);
			if (type == typeof(DateOnly?)) return this.DateWithoutTime;

			return BindNative(this, this.Ticks, type, resolver);
		}

		/// <inheritdoc />
		internal override bool IsSmallValue() => true;

		/// <inheritdoc />
		internal override bool IsInlinable() => true;

		#endregion

		#region IJsonSerializable

		/// <inheritdoc />
		public override string ToJson(CrystalJsonSettings? settings = null)
		{
			if (m_offset == NO_TIMEZONE)
			{ // DateTime
				if (m_value == DateTime.MinValue) return "\"\"";
				return "\"" + CrystalJsonFormatter.ToIso8601String(m_value) + "\"";
			}
			else
			{ // DateTimeOffset
				if (m_value == DateTime.MinValue) return "''";
				return "'" + CrystalJsonFormatter.ToIso8601String(this.DateWithOffset) + "'";
			}
		}

		/// <inheritdoc />
		public override void JsonSerialize(CrystalJsonWriter writer)
		{
			if (m_offset == NO_TIMEZONE)
			{ // DateTime
				writer.WriteValue(m_value);
			}
			else
			{ // DateTimeOffset
				writer.WriteValue(this.DateWithOffset);
			}
		}

		/// <inheritdoc />
		public override bool TryFormat(Span<char> destination, out int charsWritten, ReadOnlySpan<char> format, IFormatProvider? provider)
		{
			if (m_offset == NO_TIMEZONE)
			{ // DateTime


				if (m_value == DateTime.MinValue)
				{
					if (destination.Length < 2)
					{
						goto too_small;
					}
					destination[0] = '"';
					destination[1] = '"';
					charsWritten = 2;
					return true;
				}
				
				if (m_value == DateTime.MaxValue)
				{ // DateTime.MaxValue
					if (destination.Length < JsonTokens.Iso8601DateTimeMaxValue.Length)
					{
						goto too_small;
					}
					JsonTokens.Iso8601DateTimeMaxValue.CopyTo(destination);
					charsWritten = JsonTokens.Iso8601DateTimeMaxValue.Length;
					return true;
				}

				if (m_value == JsonDateTime.MaxValueDate)
				{ // DateOnly.MaxValue
					if (destination.Length < JsonTokens.Iso8601DateOnlyMaxValue.Length)
					{
						goto too_small;
					}
					JsonTokens.Iso8601DateOnlyMaxValue.CopyTo(destination);
					charsWritten = JsonTokens.Iso8601DateOnlyMaxValue.Length;
					return true;
				}

				if (destination.Length < 2 || !CrystalJsonFormatter.TryFormatIso8601DateTime(destination.Slice(1), out int n, m_value, m_value.Kind, null))
				{
					goto too_small;
				}
				destination[0] = '"';
				destination[n + 1] = '"';
				charsWritten = n + 2;
				return true;

			}
			else
			{ // DateTimeOffset
				var dto = new DateTimeOffset(m_value, TimeSpan.FromMinutes(m_offset));

				if (dto == DateTime.MinValue)
				{
					return JsonString.Empty.TryFormat(destination, out charsWritten, format, provider);
				}

				if (destination.Length < 2)
				{
					goto too_small;
				}

				destination[0] = '"';
				if (!dto.TryFormat(destination.Slice(1), out int n, "O", CultureInfo.InvariantCulture))
				{
					goto too_small;
				}

				destination[n + 1] = '"';
				charsWritten = n + 2;
				return true;
			}

		too_small:
			charsWritten = 0;
			return false;
		}

#if NET8_0_OR_GREATER

		/// <inheritdoc />
		public override bool TryFormat(Span<byte> destination, out int bytesWritten, ReadOnlySpan<char> format, IFormatProvider? provider)
		{
			// we first convert to chars, then to utf-8

			Span<char> chars = stackalloc char[48]; // "xxxx-xx-xxTxx:xx:xx.xxxxxxx+xx:xx"
			if (!TryFormat(chars, out int charsWritten, format, provider) 
			 || !CrystalJsonFormatter.Utf8NoBom.TryGetBytes(chars[..charsWritten], destination, out bytesWritten))
			{
				bytesWritten = 0;
				return false;
			}
			return true;
		}

#endif


		#endregion

		#region IEquatable<...>

		/// <inheritdoc />
		public override bool Equals(object? obj)
		{
			if (obj is DateTime dt) return AreEqual(this.Date, dt);
			if (obj is DateTimeOffset dto) return AreEqual(this.DateWithOffset, dto);
			if (obj is string s)
			{
				return this.HasOffset
					? JsonString.TryConvertToDateTimeOffset(s, out dto) && this.DateWithOffset == dto
					: JsonString.TryConvertToDateTime(s, out dt) && this.Date == dt;
			}
			//TODO: NodaTime?
			return base.Equals(obj);
		}

		/// <inheritdoc />
		public override bool Equals(JsonValue? obj)
		{
			switch (obj)
			{
				case null: return false;
				case JsonDateTime date: return Equals(date);
				case JsonNumber num: return num.Equals(this);
				case JsonString str: return this.HasOffset
					? str.TryConvertToDateTimeOffset(out var dto) && dto == this.DateWithOffset
					: str.TryConvertToDateTime(out var dt) && dt == this.DateWithOffset;
				//TODO: other?
				default: return false;
			}
		}

		/// <inheritdoc />
		public override bool StrictEquals(JsonValue? other) => other switch
		{
			JsonDateTime dt => Equals(dt),
			JsonString s => Equals(s),
			_ => false
		};

		public bool StrictEquals(JsonDateTime? other) => other is not null && Equals(other);
	
		public bool StrictEquals(JsonString? other) => other is not null && Equals(other);

		/// <inheritdoc />
		public bool Equals(JsonDateTime? obj)
		{
			return obj is not null && m_value == obj.m_value && m_offset == obj.m_offset;
		}

		/// <inheritdoc />
		public bool Equals(DateTime value)
		{
			return AreEqual(this.Date, value);
		}

		/// <inheritdoc />
		public bool Equals(DateTimeOffset value)
		{
			return AreEqual(this.DateWithOffset, value);
		}

		/// <inheritdoc />
		public bool Equals(NodaTime.LocalDateTime value)
		{
			return ToLocalDateTime() == value;
		}

		/// <inheritdoc />
		public bool Equals(NodaTime.LocalDate value)
		{
			return ToLocalDate() == value;
		}

		/// <inheritdoc />
		public override int GetHashCode()
		{
			return m_value.GetHashCode();
		}

		#endregion

		/// <inheritdoc />
		public override string ToString()
		{
			return this.HasOffset ? this.DateWithOffset.ToString("O") : this.Date.ToString("O");
		}

		/// <inheritdoc />
		public override bool ToBoolean(bool _ = false)
		{
			return m_value != DateTime.MinValue;
		}

		/// <summary>Returns the number of milliseconds elapsed since Unix Epoch (1970-01-01 00:00:00.000 UTC)</summary>
		/// <remarks>Note: will throw after 2038 instead of returning a negative number. Please use <see cref="UnixTime"/> instead, wich returns a <see langword="long"/>.</remarks>
		[Obsolete("This method is subject to the Y2038 bug, and will throw for dates that are after 2^31 milliseconds since Unix Epoch.")]
#pragma warning disable CS0809 // Obsolete member overrides non-obsolete member
		public override int ToInt32(int _ = 0)
#pragma warning restore CS0809 // Obsolete member overrides non-obsolete member
		{
			//BUGBUG: will throw in 2038 (cf Y2038 bug) !
			return checked((int)this.UnixTime);
		}

		/// <summary>Returns the number of milliseconds elapsed since Unix Epoch (1970-01-01 00:00:00.000 UTC)</summary>
		/// <remarks>Note: will throw for years after 2100, for reasons similar to the Y2038 bug</remarks>
		public override uint ToUInt32(uint _ = default)
		{
			//BUGBUG: will not throw in 2038 since it is unsigned, but will still throw in ~2100 for similar reasons!
			return checked((uint) this.UnixTime);
		}

		/// <summary>Returns the number of ticks</summary>
		/// <remarks>Similar to <see cref="DateTime.Ticks"/></remarks>
		public override long ToInt64(long _ = default)
		{
			return this.UtcTicks;
		}

		/// <summary>Returns the number of ticks</summary>
		/// <remarks>Similar to <see cref="DateTime.Ticks"/></remarks>
		public override ulong ToUInt64(ulong _ = default)
		{
			return (ulong) this.UtcTicks;
		}

		/// <summary>Returns the number of days elapsed since January 1st 1970 UTC</summary>
		public override float ToSingle(float _ = default)
		{
			return (float) this.UnixTimeDays;
		}

		/// <summary>Returns the number of days elapsed since January 1st 1970 UTC</summary>
		public override double ToDouble(double _ = default)
		{
			return this.UnixTimeDays;
		}

		/// <summary>Returns the number of days elapsed since January 1st 1970 UTC</summary>
		public override decimal ToDecimal(decimal _ = default)
		{
			return (decimal) this.UnixTimeDays;
		}

		/// <inheritdoc />
		public override DateTime ToDateTime(DateTime _ = default)
		{
			return this.Date;
		}

		/// <inheritdoc />
		public override DateTimeOffset ToDateTimeOffset(DateTimeOffset _ = default)
		{
			return this.DateWithOffset;
		}

		/// <inheritdoc />
		public override DateOnly ToDateOnly(DateOnly _ = default)
		{
			return DateOnly.FromDateTime(this.Date);
		}

		public NodaTime.LocalDateTime ToLocalDateTime()
		{
			return NodaTime.LocalDateTime.FromDateTime(this.Date);
		}

		public NodaTime.LocalDate ToLocalDate()
		{
			return NodaTime.LocalDate.FromDateTime(this.Date);
		}

		/// <summary>Returns the elapsed time since January 1st 1970 UTC</summary>
		public override TimeSpan ToTimeSpan(TimeSpan _ = default)
		{
			return new TimeSpan(this.UtcTicks - UNIX_EPOCH_TICKS);
		}

		/// <summary>Returns the elapsed time since January 1st 1970 UTC</summary>
		public override NodaTime.Duration ToDuration(NodaTime.Duration _ = default)
		{
			return NodaTime.Duration.FromTicks(this.UtcTicks - UNIX_EPOCH_TICKS);
		}

		/// <inheritdoc />
		public override NodaTime.Instant ToInstant(NodaTime.Instant _ = default)
		{
			if (m_offset == NO_TIMEZONE)
			{
				return NodaTime.Instant.FromDateTimeUtc(this.Date.ToUniversalTime());
			}

			return NodaTime.Instant.FromDateTimeOffset(this.DateWithOffset);
		}

		/// <inheritdoc />
		public override void WriteTo(ref SliceWriter writer)
		{
			//TODO: optimize!
			writer.WriteStringUtf8(ToJson());
		}
		
	}

}
