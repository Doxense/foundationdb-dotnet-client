#region Copyright Doxense 2010-2022
//
// All rights are reserved. Reproduction or transmission in whole or in part, in
// any form or by any means, electronic, mechanical or otherwise, is prohibited
// without the prior written consent of the copyright owner.
//
#endregion

namespace Doxense.Serialization.Json
{
	using System;
	using System.Diagnostics;
	using Doxense.Memory;

	/// <summary>Date JSON</summary>
	[DebuggerDisplay("JSON DateTime({m_value}, {m_value}+{m_offset})")]
	public class JsonDateTime : JsonValue, IEquatable<JsonDateTime>, IEquatable<DateTime>, IEquatable<DateTimeOffset>, IEquatable<NodaTime.LocalDateTime>, IEquatable<NodaTime.LocalDate>
	{
		private const long UNIX_EPOCH_TICKS = 621355968000000000L;
		private const short NO_TIMEZONE = short.MinValue;

		#region Static Helpers...

		public static readonly JsonDateTime MinValue = new JsonDateTime(DateTime.MinValue, NO_TIMEZONE);
		public static readonly JsonDateTime MaxValue = new JsonDateTime(DateTime.MaxValue, NO_TIMEZONE);

		public static JsonDateTime UtcNow { get { return new JsonDateTime(Truncate(DateTime.UtcNow)); } }
		public static JsonDateTime Now { get { return new JsonDateTime(Truncate(DateTime.Now)); } }

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

		/// <summary>Copie du membre "dateData" de DateTime (ou de m_dateTime pour un DateTimeOffset)</summary>
		private readonly DateTime m_value;
		/// <summary>Copie du membre "m_offsetMinutes" de DateTimeOffset (ou int.MinValue pour un DateTime)</summary>
		private readonly short m_offset;

		// un DateTimeOffset est stocké sous forme d'une DateTime en UTC, accompagnée d'un offset
		// un DateTime de source locale (UTC ou Local) aura m_offset == NO_TIMEZONE
		// un DateTime de source distante (parsé) aura m_offset = NO_TIMEZONE s'il n'y avait pas d'infos dans le source, ou l'offset spécifié dans la source
		// => si on demande un DateTime dans une autre TZ on aura la date convertie en LocalTime du serveur courrant.

		#endregion

		#region Constructors...

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
			m_offset = (short)value.Offset.TotalMinutes;
		}

		public JsonDateTime(long ticks, DateTimeKind kind) : this(new DateTime(ticks, kind)) { }

		public JsonDateTime(int year, int month, int day) : this(new DateTime(year, month, day)) { }

		public JsonDateTime(int year, int month, int day, int hour, int minute, int second, DateTimeKind kind) : this(new DateTime(year, month, day, hour, minute, second, kind)) { }

		public JsonDateTime(int year, int month, int day, int hour, int minute, int second, int millisecond, DateTimeKind kind) : this(new DateTime(year, month, day, hour, minute, second, millisecond, kind)) { }

		public static JsonDateTime Return(DateTime value)
		{
			if (value == DateTime.MinValue) return JsonDateTime.MinValue;
			if (value == DateTime.MaxValue) return JsonDateTime.MaxValue;
			return new JsonDateTime(value);
		}

		public static JsonValue Return(DateTime? value)
		{
			return value.HasValue ? Return(value.Value) : JsonNull.Null;
		}

		public static JsonDateTime Return(DateTimeOffset value)
		{
			if (value == DateTimeOffset.MinValue) return JsonDateTime.MinValue;
			if (value == DateTimeOffset.MaxValue) return JsonDateTime.MaxValue;
			return new JsonDateTime(value);
		}

		public static JsonValue Return(DateTimeOffset? value)
		{
			return value.HasValue ? Return(value.Value) : JsonNull.Null;
		}

		#endregion

		#region Public Members...

		public long Ticks
		{
			get { return m_value.Ticks; }
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

		public DateTime LocalDateTime => this.DateWithOffset.LocalDateTime;

		public DateTime UtcDateTime => this.DateWithOffset.UtcDateTime;

		public TimeSpan UtcOffset => m_offset != NO_TIMEZONE ? TimeSpan.FromMinutes(m_offset) : m_value.Kind == DateTimeKind.Utc ? TimeSpan.Zero : TimeZoneInfo.Local.GetUtcOffset(m_value);

		public bool HasOffset => m_offset != NO_TIMEZONE;

		/// <summary>Nombre de millisecondes écoulées depuis le 1er Janvier 1970 UTC</summary>
		public long UnixTime
		{
			//note: c'est un long pour ne pas avoir de problème avec le Y2038 bug (Unix Time Epoch Bug)
			get { return (this.UtcDateTime.Ticks - UNIX_EPOCH_TICKS) / TimeSpan.TicksPerMillisecond; }
		}

		/// <summary>Nombre de jours écoulés depuis le 1er Janvier 1970 UTC</summary>
		public double UnixTimeDays
		{
			//note: normalement pas affecté par le Y2038 bug
			get { return (double)(UtcTicks - UNIX_EPOCH_TICKS) / (double)TimeSpan.TicksPerDay; }
		}

		public bool IsLocalTime { get { return m_offset == NO_TIMEZONE ? m_value.Kind == DateTimeKind.Local : m_offset != 0 /*TODO: comparer avec la TZ courrante ? */; } }

		public bool IsUtc { get { return m_offset == NO_TIMEZONE ? m_value.Kind == DateTimeKind.Utc : m_offset == 0; } }

		public DateTime ToUniversalTime()
		{
			return this.UtcDateTime;
		}

		public DateTime ToLocalTime()
		{
			return this.LocalDateTime;
		}

		#endregion

		#region JsonValue Members...

		public override JsonType Type => JsonType.DateTime;

		public override bool IsDefault => m_value == DateTime.MinValue;

		public override object? ToObject()
		{
			return m_value;
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
			if (type == typeof(NodaTime.Instant))
			{
				return NodaTime.Instant.FromDateTimeOffset(this.DateWithOffset);
			}
			return JsonValue.BindNative<JsonDateTime, long>(this, this.Ticks, type, resolver);
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
			if (object.ReferenceEquals(obj, null)) return false;
			switch (obj)
			{
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
			return !object.ReferenceEquals(obj, null) && m_value == obj.m_value && m_offset == obj.m_offset;
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

		/// <summary>Retourne le temps Unix (ms depuis le 1er Janvier 1970 UTC)</summary>
		public override int ToInt32()
		{
			//BUGBUG: va throw en 2038 (cf Y2038 bug) !
			return checked((int)this.UnixTime);
		}

		/// <summary>Retourne le temps Unix (ms depuis le 1er Janvier 1970 UTC)</summary>
		public override uint ToUInt32()
		{
			//BUGBUG: ne va pas throw en 2038, mais en 2100 et quelques (cf Y2038 bug) !
			return checked((uint)this.UnixTime);
		}

		/// <summary>Retourne le nombre ticks UTC (DateTime.Ticks)</summary>
		public override long ToInt64()
		{
			return this.UtcTicks;
		}

		public override ulong ToUInt64()
		{
			return (ulong) this.UtcTicks;
		}

		/// <summary>Retourne le nombre de jours écoulés depuis le 1er Janvier 1970 UTC)</summary>
		public override float ToSingle()
		{
			return (float)this.UnixTimeDays;
		}

		/// <summary>Retourne le nombre de jours écoulés depuis le 1er Janvier 1970 UTC)</summary>
		public override double ToDouble()
		{
			return this.UnixTimeDays;
		}

		/// <summary>Retourne le nombre de jours écoulés depuis le 1er Janvier 1970 UTC)</summary>
		public override decimal ToDecimal()
		{
			return (decimal) this.UnixTimeDays;
		}

		public override DateTime ToDateTime()
		{
			return this.Date;
		}

		public override DateTimeOffset ToDateTimeOffset()
		{
			return this.DateWithOffset;
		}

		public NodaTime.LocalDateTime ToLocalDateTime()
		{
			return NodaTime.LocalDateTime.FromDateTime(this.Date);
		}

		public NodaTime.LocalDate ToLocalDate()
		{
			return NodaTime.LocalDate.FromDateTime(this.Date);
		}

		/// <summary>Retourne le temps écoulé depuis le 1er Janvier 1970 UTC</summary>
		/// <returns></returns>
		public override TimeSpan ToTimeSpan()
		{
			return new TimeSpan(this.UtcTicks - UNIX_EPOCH_TICKS);
		}

		/// <summary>Retourne le temps écoulé depuis le 1er Janvier 1970 UTC</summary>
		/// <returns></returns>
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
