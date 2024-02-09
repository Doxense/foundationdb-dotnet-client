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
	using System;
	using System.Diagnostics;
	using System.Runtime.CompilerServices;
	using Doxense.Memory;

	/// <summary>JSON Boolean, that can be either <see langword="true"/> or <see langword="false"/></summary>
	[DebuggerDisplay("JSON Boolean({m_value})")]
	[DebuggerNonUserCode]
	[JetBrains.Annotations.PublicAPI]
	public sealed class JsonBoolean : JsonValue, IEquatable<JsonBoolean>, IComparable<JsonBoolean>, IEquatable<JsonNumber>, IEquatable<JsonString>, IEquatable<bool>
	{

		/// <summary>JSON value that is equal to <see langword="true"/></summary>
		/// <remarks>This singleton is immutable and can be cached</remarks>
		public static readonly JsonBoolean True = new(true);

		/// <summary>JSON value that is equal to <see langword="false"/></summary>
		/// <remarks>This singleton is immutable and can be cached</remarks>
		public static readonly JsonBoolean False = new(false);

		private readonly bool m_value;

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		internal JsonBoolean(bool value) => m_value = value;

		/// <summary>Returns either <see cref="JsonBoolean.True"/> or <see cref="JsonBoolean.False"/></summary>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static JsonBoolean Return(bool value) => value ? True : False;

		/// <summary>Returns either <see cref="JsonBoolean.True"/>, <see cref="JsonBoolean.False"/> or <see cref="JsonNull.Null"/></summary>
		public static JsonValue Return(bool? value) => value == null ? JsonNull.Null : value.Value ? JsonBoolean.True : JsonBoolean.False;

		public bool Value => m_value;

		#region JsonValue Members...

		public override JsonType Type => JsonType.Boolean;

		public override bool IsDefault => !m_value;

		public override bool IsReadOnly => true; //note: booleans are immutable

		public override object ToObject() => m_value;

		public override T? Bind<T>(ICrystalJsonTypeResolver? resolver = null) where T : default
		{
			#region <JIT_HACK>
			// pattern recognized and optimized by the JIT, only in Release build
#if !DEBUG
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
			if (typeof(T) == typeof(Guid)) return (T) (object) ToGuid();
			if (typeof(T) == typeof(Uuid128)) return (T) (object) ToUuid128();
			if (typeof(T) == typeof(Uuid96)) return (T) (object) ToUuid96();
			if (typeof(T) == typeof(Uuid80)) return (T) (object) ToUuid80();
			if (typeof(T) == typeof(Uuid64)) return (T) (object) ToUuid64();
			if (typeof(T) == typeof(NodaTime.Instant)) return (T) (object) ToInstant();
			if (typeof(T) == typeof(NodaTime.Duration)) return (T) (object) ToDuration();
#endif
			#endregion

			return (T?) BindNative<JsonBoolean, bool>(this, m_value, typeof(T), resolver);
		}

		public override object? Bind(Type? type, ICrystalJsonTypeResolver? resolver = null) => BindNative<JsonBoolean, bool>(this, m_value, type, resolver);

		internal override bool IsSmallValue() => true;

		internal override bool IsInlinable() => true;

		#endregion

		#region IEquatable<...>

		public override bool Equals(object? value)
		{
			if (value == null) return false;

			switch (System.Type.GetTypeCode(value.GetType()))
			{
				case TypeCode.Boolean: return m_value == (bool)value;
				case TypeCode.Int32: return m_value == ((int)value != 0);
				case TypeCode.UInt32: return m_value == ((uint)value != 0U);
				case TypeCode.Int64: return m_value == ((long)value != 0L);
				case TypeCode.UInt64: return m_value == ((ulong)value != 0UL);
				//TODO: autres!
			}
			return base.Equals(value);
		}

		public override bool Equals(JsonValue? value) => value switch
		{
			JsonBoolean b => Equals(b),
			JsonNumber n => Equals(n),
			JsonString s => Equals(s),
			_ => false
		};

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool Equals(JsonBoolean? obj) => obj is not null && obj.m_value == m_value;

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool Equals(JsonNumber? obj) => obj is not null && obj.ToBoolean() == m_value;

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool Equals(JsonString? obj) => obj is not null && m_value != string.IsNullOrEmpty(obj.Value);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool Equals(bool value) => m_value == value;

		public override int GetHashCode() => m_value ? 1 : 0;

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static implicit operator bool(JsonBoolean? obj) => obj?.m_value == true;
		//TODO: REVIEW: is this usefull ? when do we have a variable of explicit type JsonBoolean?

		#endregion

		#region IEquatable<...>

		public override int CompareTo(JsonValue? other)
		{
			if (other == null) return +1;
			switch(other.Type)
			{
				case JsonType.Boolean:
				case JsonType.String:
				case JsonType.Number:
					return m_value.CompareTo(other.ToBoolean());
				default:
					return base.CompareTo(other);
			}
		}

		public int CompareTo(JsonBoolean? other)
		{
			return other != null ? m_value.CompareTo(other.Value) : +1;
		}

		#endregion

		#region IJsonConvertible...

		public override string ToJson(CrystalJsonSettings? settings = null) => m_value ? JsonTokens.True : JsonTokens.False;

		public override string ToString() => m_value ? JsonTokens.True : JsonTokens.False;

		public override bool ToBoolean() => m_value;

		public override byte ToByte() => m_value ? (byte) 1 : default(byte);

		public override sbyte ToSByte() => m_value ? (sbyte)1 : default(sbyte);

		public override char ToChar() => m_value ? 'Y' : 'N';

		public override short ToInt16() => m_value ? (short) 1 : default(short);

		public override ushort ToUInt16() => m_value ? (ushort) 1 : default(ushort);

		public override int ToInt32() => m_value ? 1 : 0;

		public override uint ToUInt32() => m_value ? 1U : 0U;

		public override long ToInt64() => m_value ? 1L : 0L;

		public override ulong ToUInt64() => m_value ? 1UL : 0UL;

		public override float ToSingle() => m_value ? 1f : 0f;

		public override double ToDouble() => m_value ? 1d : 0d;

		public override decimal ToDecimal() => m_value ? 1m : 0m;

		private static readonly Guid AllF = new Guid(new byte[16] { 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255 });

		public override Guid ToGuid() => m_value ? AllF : Guid.Empty;

		public override Uuid64 ToUuid64() => m_value ? new Uuid64(-1) : default(Uuid64);

		#endregion

		#region IJsonSerializable

		public override void JsonSerialize(CrystalJsonWriter writer)
		{
			writer.WriteValue(m_value);
		}

		#endregion

		#region ISliceSerializable

		public override void WriteTo(ref SliceWriter writer)
		{
			if (m_value)
			{ // 'true' => 74 72 75 65
				writer.WriteBytes("true"u8);
			}
			else
			{ // 'false' => 66 61 6C 73 65
				writer.WriteBytes("false"u8);
			}
		}

		#endregion

	}

}
