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
	using System.Diagnostics;
	using System.Globalization;
	using System.Runtime.CompilerServices;
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

		public static readonly JsonDateTime MinValue = new(DateTime.MinValue, NO_TIMEZONE);
		public static readonly JsonDateTime MaxValue = new(DateTime.MaxValue, NO_TIMEZONE);
		private static readonly DateTime MaxValueDate = new DateTime(3155378112000000000);
		public static readonly JsonDateTime DateOnlyMaxValue = new(MaxValueDate, NO_TIMEZONE);

		public static JsonDateTime UtcNow => new(Truncate(DateTime.UtcNow));

		public static JsonDateTime Now => new(Truncate(DateTime.Now));

		/// <summary>Précision des dates JSON (lié à la façon dont elles sont sérialisées)</summary>
		public static readonly long PrecisionTicks = TimeSpan.TicksPerMillisecond;

		/// <summary>Arrondit une date en millisecondes</summary>
		/// <param name="date"></param>
		/// <returns></returns>
		public static DateTime Truncate(DateTime date)
		{
			if (date == DateTime.MinValue || date == DateTime.MaxValue) return date;
			return new DateTime((date.Ticks / PrecisionTicks) * PrecisionTicks, date.Kind);
		}

		/// <summary>Indique si deux dates sont identiques, à la précision près</summary>
		public static bool AreEqual(DateTime left, DateTime right)
		{
			return Math.Abs(left.Ticks - right.Ticks) <= PrecisionTicks;
		}

		/// <summary>Indique si deux dates sont identiques, à la précision près</summary>
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

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static JsonDateTime Return(DateTime value)
		{
			if (value == DateTime.MinValue) return JsonDateTime.MinValue;
			if (value == DateTime.MaxValue) return JsonDateTime.MaxValue;
			return new JsonDateTime(value);
		}

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static JsonValue Return(DateTime? value)
		{
			return value.HasValue ? Return(value.Value) : JsonNull.Null;
		}

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static JsonDateTime Return(DateTimeOffset value)
		{
			if (value == DateTimeOffset.MinValue) return JsonDateTime.MinValue;
			if (value == DateTimeOffset.MaxValue) return JsonDateTime.MaxValue;
			return new JsonDateTime(value);
		}

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static JsonValue Return(DateTimeOffset? value)
		{
			return value.HasValue ? Return(value.Value) : JsonNull.Null;
		}

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static JsonDateTime Return(DateOnly value)
		{
			if (value == DateOnly.MinValue) return JsonDateTime.MinValue;
			if (value == DateOnly.MaxValue) return JsonDateTime.DateOnlyMaxValue;
			return new JsonDateTime(value);
		}

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static JsonValue Return(DateOnly? value)
		{
			return value.HasValue ? Return(value.Value) : JsonNull.Null;
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

		public bool IsLocalTime => m_offset == NO_TIMEZONE ? m_value.Kind == DateTimeKind.Local : m_offset != 0 /*TODO: comparer avec la TZ courrante ? */;

		public bool IsUtc => m_offset == NO_TIMEZONE ? m_value.Kind == DateTimeKind.Utc : m_offset == 0;

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public DateTime ToUniversalTime() => this.UtcDateTime;

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public DateTime ToLocalTime() => this.LocalDateTime;

		#endregion

		#region JsonValue Members...

		public override JsonType Type => JsonType.DateTime;

		public override bool IsDefault => m_value == DateTime.MinValue;

		public override bool IsReadOnly => true; //note: dates are immutable

		public override object ToObject() => m_value;

		public override T? Bind<T>(T? defaultValue = default, ICrystalJsonTypeResolver? resolver = null) where T : default
		{
			#region <JIT_HACK>
			// pattern recognized and optimized by the JIT, only in Release build
#if !DEBUG
			if (typeof(T) == typeof(DateTime)) return (T) (object) ToDateTime();
			if (typeof(T) == typeof(DateTimeOffset)) return (T) (object) ToDateTimeOffset();
			if (typeof(T) == typeof(DateOnly)) return (T) (object) ToDateOnly();
			if (typeof(T) == typeof(NodaTime.Instant)) return (T) (object) ToInstant();
#endif
			#endregion

			return (T?) BindNative(this, this.Ticks, typeof(T), resolver) ?? defaultValue;
		}

		public override object? Bind(Type? type, ICrystalJsonTypeResolver? resolver = null)
		{
			if (type == typeof(DateTimeOffset))
			{
				return this.DateWithOffset;
			}
			if (type == typeof(DateTime))
			{
				return this.Date;
			}
			if (type == typeof(DateOnly))
			{
				return this.DateWithoutTime;
			}
			if (type == typeof(NodaTime.Instant))
			{
				return NodaTime.Instant.FromDateTimeOffset(this.DateWithOffset);
			}
			return BindNative(this, this.Ticks, type, resolver);
		}

		internal override bool IsSmallValue() => true;

		internal override bool IsInlinable() => true;

		#endregion

		#region IJsonSerializable

		public override void JsonSerialize(CrystalJsonWriter writer)
		{
#if DISABLED
			if (m_literal != null)
			{ // retransforme sous forme '\/Date(...)\/'
				if (m_literal.StartsWith("/") && m_literal.EndsWith("/"))
					writer.WriteLiteral("\"" + m_literal.Replace("/", @"\/") + "\"");
				else
					writer.WriteString(m_literal);
			}
			else
#endif
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

		#endregion

		#region IEquatable<...>

		public override bool Equals(object? obj)
		{
			if (obj is DateTime dt) return AreEqual(this.Date, dt);
			if (obj is DateTimeOffset dto) return AreEqual(this.DateWithOffset, dto);
			if (obj is string s)
			{
				return this.HasOffset
					? JsonString.TryConvertDateTimeOffset(s, out dto) && this.DateWithOffset == dto
					: JsonString.TryConvertDateTime(s, out dt) && this.Date == dt;
			}
			//TODO: NodaTime?
			return base.Equals(obj);
		}

		public override bool Equals(JsonValue? obj)
		{
			switch (obj)
			{
				case null: return false;
				case JsonDateTime date: return Equals(date);
				case JsonNumber num: return num.Equals(this);
				case JsonString str: return this.HasOffset
					? str.TryConvertDateTimeOffset(out var dto) && dto == this.DateWithOffset
					: str.TryConvertDateTime(out var dt) && dt == this.DateWithOffset;
				//TODO: other?
				default: return false;
			}
		}

		public bool Equals(JsonDateTime? obj)
		{
			return obj is not null && m_value == obj.m_value && m_offset == obj.m_offset;
		}

		public bool Equals(DateTime value)
		{
			//TODO: gérer le cas ou on wrap un DateTimeOffset
			return AreEqual(this.Date, value);
		}

		public bool Equals(DateTimeOffset value)
		{
			//TODO: gérer le cas ou on wrap un DateTimeOffset
			return AreEqual(this.DateWithOffset, value);
		}

		public bool Equals(NodaTime.LocalDateTime value)
		{
			//TODO: gérer le cas ou on wrap un DateTimeOffset
			return ToLocalDateTime() == value;
		}

		public bool Equals(NodaTime.LocalDate value)
		{
			//TODO: gérer le cas ou on wrap un DateTimeOffset
			return ToLocalDate() == value;
		}

		public override int GetHashCode()
		{
			return m_value.GetHashCode();
		}

		#endregion

		public override string ToString()
		{
			return this.HasOffset ? this.DateWithOffset.ToString("O") : this.Date.ToString("O");
		}

		public override bool ToBoolean()
		{
			return m_value != DateTime.MinValue;
		}

		/// <summary>Returns the number of milliseconds elapsed since Unix Epoch (1970-01-01 00:00:00.000 UTC)</summary>
		/// <remarks>Note: will throw after 2038 instead of returning a negative number. Please use <see cref="UnixTime"/> instead, wich returns a <see langword="long"/>.</remarks>
		[Obsolete("This method is subject to the Y2038 bug, and will throw for dates that are after 2^31 milliseconds since Unix Epoch.")]
#pragma warning disable CS0809 // Obsolete member overrides non-obsolete member
		public override int ToInt32()
#pragma warning restore CS0809 // Obsolete member overrides non-obsolete member
		{
			//BUGBUG: will throw in 2038 (cf Y2038 bug) !
			return checked((int)this.UnixTime);
		}

		/// <summary>Returns the number of milliseconds elapsed since Unix Epoch (1970-01-01 00:00:00.000 UTC)</summary>
		/// <remarks>Note: will throw for years after 2100, for reasons similar to the Y2038 bug</remarks>
		public override uint ToUInt32()
		{
			//BUGBUG: will not throw in 2038 since it is unsigned, but will still throw in ~2100 for similar reasons!
			return checked((uint) this.UnixTime);
		}

		/// <summary>Returns the number of ticks</summary>
		/// <remarks>Similar to <see cref="DateTime.Ticks"/></remarks>
		public override long ToInt64()
		{
			return this.UtcTicks;
		}

		/// <summary>Returns the number of ticks</summary>
		/// <remarks>Similar to <see cref="DateTime.Ticks"/></remarks>
		public override ulong ToUInt64()
		{
			return (ulong) this.UtcTicks;
		}

		/// <summary>Returns the number of days elapsed since January 1st 1970 UTC</summary>
		public override float ToSingle()
		{
			return (float) this.UnixTimeDays;
		}

		/// <summary>Returns the number of days elapsed since January 1st 1970 UTC</summary>
		public override double ToDouble()
		{
			return this.UnixTimeDays;
		}

		/// <summary>Returns the number of days elapsed since January 1st 1970 UTC</summary>
		public override decimal ToDecimal()
		{
			return (decimal) this.UnixTimeDays;
		}

		/// <inheritdoc />
		public override DateTime ToDateTime()
		{
			return this.Date;
		}

		/// <inheritdoc />
		public override DateTimeOffset ToDateTimeOffset()
		{
			return this.DateWithOffset;
		}

		/// <inheritdoc />
		public override DateOnly ToDateOnly()
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
		public override TimeSpan ToTimeSpan()
		{
			return new TimeSpan(this.UtcTicks - UNIX_EPOCH_TICKS);
		}

		/// <summary>Returns the elapsed time since January 1st 1970 UTC</summary>
		public override NodaTime.Duration ToDuration()
		{
			return NodaTime.Duration.FromTicks(this.UtcTicks - UNIX_EPOCH_TICKS);
		}

		public override void WriteTo(ref SliceWriter writer)
		{
			//TODO: optimize!
			writer.WriteStringUtf8(ToJson());
		}
	}

}
