#region Copyright Doxense 2010-2021
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
	using System.Runtime.CompilerServices;
	using Doxense.Memory;

	/// <summary>Booléen JSON</summary>
	[DebuggerDisplay("JSON Boolean({m_value})")]
	[DebuggerNonUserCode]
	public sealed class JsonBoolean : JsonValue, IEquatable<JsonBoolean>, IComparable<JsonBoolean>, IEquatable<JsonNumber>, IEquatable<JsonString>, IEquatable<bool>
	{
		/// <summary>True</summary>
		public static readonly JsonBoolean True = new JsonBoolean(true);

		/// <summary>False</summary>
		public static readonly JsonBoolean False = new JsonBoolean(false);

		private readonly bool m_value;

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		internal JsonBoolean(bool value)
		{
			m_value = value;
		}

		public static JsonBoolean Return(bool value) => value ? True : False;

		public static JsonValue Return(bool? value)
		{
			return !value.HasValue ? JsonNull.Null : value.Value ? JsonBoolean.True : JsonBoolean.False;
		}

		public bool Value => m_value;

		#region JsonValue Members...

		public override JsonType Type => JsonType.Boolean;

		public override bool IsDefault => !m_value;

		public override object? ToObject() => m_value;

		public override object? Bind(Type? type, ICrystalJsonTypeResolver? resolver = null) => JsonValue.BindNative<JsonBoolean, bool>(this, m_value, type, resolver);

		internal override bool IsSmallValue() => true;

		internal override bool IsInlinable() => true;

		#endregion

		#region IJsonSerializable

		public override void JsonSerialize(CrystalJsonWriter writer)
		{
			writer.WriteValue(m_value);
		}

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

		public override bool Equals(JsonValue? value)
		{
			if (object.ReferenceEquals(value, null)) return false;
			switch(value)
			{
				case JsonBoolean b:
					return Equals(b);
				case JsonNumber n:
					return Equals(n);
				case JsonString s:
					return Equals(s);
				default:
					return false;
			}
		}

		public bool Equals(JsonBoolean? obj)
		{
			return !object.ReferenceEquals(obj, null) && obj.m_value == m_value;
		}

		public bool Equals(JsonNumber? obj)
		{
			return !object.ReferenceEquals(obj, null) && obj.ToBoolean() == m_value;
		}

		public bool Equals(JsonString? obj)
		{
			return !object.ReferenceEquals(obj, null) && m_value != string.IsNullOrEmpty(obj.Value);
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool Equals(bool value)
		{
			return m_value == value;
		}

		public override int GetHashCode()
		{
			// false.GetHashcode() => 0
			// true.GetHashcode() => 1
			return m_value ? 1 : 0;
		}

		public static implicit operator bool(JsonBoolean? obj)
		{
			return obj?.m_value == true;
		}

		#endregion

		#region IEquatable<...>

		public override int CompareTo(JsonValue other)
		{
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

		public int CompareTo(JsonBoolean other)
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

		public override void WriteTo(ref SliceWriter writer)
		{
			if (m_value)
			{ // 'true' => 74 72 75 65
				writer.WriteFixed32(0x65757274);
			}
			else
			{ // 'false' => 66 61 6C 73 65
				writer.WriteFixed32(0x736C6166);
				writer.WriteByte(0x65);
			}
		}
	}

}
