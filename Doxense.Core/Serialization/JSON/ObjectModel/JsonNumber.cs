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

// ReSharper disable CompareOfFloatsByEqualityOperator
//#define ENABLE_GRISU3_STRING_CONVERTER

namespace Doxense.Serialization.Json
{
	using System;
	using System.Diagnostics;
	using System.Globalization;
	using System.Runtime.CompilerServices;
	using System.Runtime.InteropServices;
	using Doxense.Diagnostics.Contracts;
	using Doxense.Memory;
	using JetBrains.Annotations;
	using NodaTime;

	//REVIEW: on a besoin de séparer les JsonNumber en plusieurs sous-types:
	// - les JsonNumber "parsed": résultat d'un parsing, contient le littéral tel qu'il était dans le source (l'équivalent du JsonNumber actuel)
	// - les JsonNumber "boxed": résultat d'un JsonValue.FromValue<int>(123) ou d'un JsonNumber.Return(123), ou le littéral est lazily generated (ou interned pour les singletons)
	// - les JsonNumber "big": qui wrappent un System.Decimal ou un BigInteger (128bits ou plus) afin de ne pas alourdir pour rien les JsonNumber classiques

	/// <summary>Nombre JSON</summary>
	[DebuggerDisplay("JSON Number({" + nameof(m_literal) + ",nq})")]
	[DebuggerNonUserCode]
	public sealed class JsonNumber : JsonValue, IEquatable<JsonNumber>, IComparable<JsonNumber>, IEquatable<JsonString>, IEquatable<JsonBoolean>, IEquatable<JsonDateTime>, IEquatable<int>, IEquatable<long>, IEquatable<uint>, IEquatable<ulong>, IEquatable<float>, IEquatable<double>, IEquatable<decimal>, IEquatable<TimeSpan>
	{
		/// <summary>Cache de petits nombres, de <see cref="CACHED_SIGNED_MIN"/> à <see cref="CACHED_SIGNED_MAX"/> (inclus)</summary>
		private static readonly JsonNumber[] SmallNumbers = PreGenSmallNumbers();
		//NOTE: SmallNumbers doit être initialisé AVANT les autres champs static, sinon nullref au runtime!

		internal const int CACHED_SIGNED_MIN = -128;
		internal const int CACHED_SIGNED_MAX = 999; //note: doit être AU MOINS 255 car les 'byte' doivent passer dans le cache
		/// <summary>Position du nombre 0 dans le cache</summary>
		private const uint CACHED_OFFSET_ZERO = -CACHED_SIGNED_MIN;

		private static JsonNumber[] PreGenSmallNumbers()
		{
			var cache = new JsonNumber[CACHED_SIGNED_MAX - CACHED_SIGNED_MIN + 1];
			for (int i = 0; i < cache.Length; i++)
			{
				cache[i] = new JsonNumber(new Number(i + CACHED_SIGNED_MIN), Kind.Signed, StringConverters.ToString(i + CACHED_SIGNED_MIN));
			}
			return cache;
		}

		/// <summary>0 (signed int)</summary>
		public static readonly JsonNumber Zero = SmallNumbers[0 + CACHED_OFFSET_ZERO];
		/// <summary>1 (signed int)</summary>
		public static readonly JsonNumber One = SmallNumbers[1 + CACHED_OFFSET_ZERO];
		/// <summary>-1 (signed int)</summary>
		public static readonly JsonNumber MinusOne = SmallNumbers[-1 + CACHED_OFFSET_ZERO];
		/// <summary>0.0 (double)</summary>
		public static readonly JsonNumber DecimalZero = new JsonNumber(new Number(0d), Kind.Double, "0"); //REVIEW: "0.0" ?
		/// <summary>1.0 (double)</summary>
		public static readonly JsonNumber DecimalOne = new JsonNumber(new Number(1d), Kind.Double, "1"); //REVIEW: "1.0" ?

		public static readonly JsonNumber NaN = new JsonNumber(new Number(double.NaN), Kind.Double, "NaN");

		public static readonly JsonNumber PositiveInfinity = new JsonNumber(new Number(double.PositiveInfinity), Kind.Double, "Infinity");

		public static readonly JsonNumber NegativeInfinity = new JsonNumber(new Number(double.NegativeInfinity), Kind.Double, "-Infinity");

		//REVIEW: Signed vs Unsigned ?
		// 1) est-ce qu'on marque signed si ca vient d'un Int32, et unsigned si UInt32?
		//    => (42).IsSigned != (42U).IsSigned
		// 2) ou alors uniquement si c'est un UInt32 qui est > int.MaxValue ?
		//    => (42).IsSigned == (45U).IsSigned, mais (int.MaxValue).IsSigned != ((uint)int.MaxValue + 1).IsSigned
		// 3) ou alors uniquement basé sur le signe?
		//    => (42).IsSigned == true; (-42).IsSigned == false

		#region Nested Types...

		private enum Kind
		{
			/// <summary>Nombre entier qui peut être casté en un Int64 (long.MinValue &lt;= x &lt;= long.MaxValue)</summary>
			Signed = 0,
			/// <summary>Nombre entier positif qui nécessite un UInt64 (x > long.MaxValue)</summary>
			Unsigned,
			/// <summary>128-bits Decimals</summary>
			Decimal,
			/// <summary>64-bits IEEE </summary>
			Double,
		}

		[StructLayout(LayoutKind.Explicit)]
		private readonly struct Number
		{
			//REVIEW: a cause de Decimal, la struct prend 16 octets, alors qu'on pourrait baisser a 8 ?

			#region Fields...

			[FieldOffset(0)]
			public readonly decimal Decimal;

			[FieldOffset(0)]
			public readonly double Double;

			[FieldOffset(0)]
			public readonly long Signed;

			[FieldOffset(0)]
			public readonly ulong Unsigned;

			#endregion

			#region Constructors...

			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			public Number(decimal value)
				: this()
			{
				this.Decimal = value;
			}

			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			public Number(double value)
				: this()
			{
				this.Double = value;
			}

			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			public Number(long value)
				: this()
			{
				this.Signed = value;
			}

			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			public Number(ulong value)
				: this()
			{
				this.Unsigned = value;
			}

			#endregion

			[Pure]
			public bool IsDefault(Kind kind)
			{
				switch(kind)
				{
					case Kind.Decimal: return this.Decimal == 0m;
					case Kind.Double: return this.Double == 0d;
					case Kind.Signed: return this.Signed == 0L;
					default: return this.Unsigned == 0UL;
				}
			}

			[Pure]
			public bool IsNegative(Kind kind)
			{
				switch (kind)
				{
					case Kind.Decimal: return this.Decimal < 0m;
					case Kind.Double: return this.Double < 0d;
					case Kind.Signed: return this.Signed < 0L;
					default: return false;
				}
			}

			public static bool Equals(in Number x, Kind xKind, in Number y, Kind yKind)
			{
				if (xKind == yKind)
				{ // same kind, direct comparison
					switch (xKind)
					{
						case Kind.Decimal: return x.Decimal == y.Decimal;
						default: return x.Unsigned == y.Unsigned; //note: works for all other cases
					}
				}
				// need to adapt one or the other to the "largest" type
				if (xKind == Kind.Decimal) return x.Decimal == y.ToDecimal(yKind);
				if (yKind == Kind.Decimal) return y.Decimal == x.ToDecimal(xKind);
				if (xKind == Kind.Double) return x.Double == y.ToDouble(yKind);
				if (yKind == Kind.Double) return y.Double == x.ToDouble(xKind);
				if (xKind == Kind.Signed) return (x.Signed == y.Signed) & (y.Signed >= 0); // x signed, y unsigned
				return (x.Signed == y.Signed) & (x.Signed >= 0); // x unsigned, y signed
			}

			public static int CompareTo(in Number x, Kind xKind, in Number y, Kind yKind)
			{
				switch (yKind)
				{
					case Kind.Decimal: return x.CompareTo(xKind, y.ToDecimal(yKind));
					case Kind.Double:  return x.CompareTo(xKind, y.ToDouble(yKind));
					case Kind.Signed:  return x.CompareTo(xKind, y.ToInt64(yKind));
					default:           return x.CompareTo(xKind, y.ToUInt64(yKind));
				}
			}

			/// <summary>Compare ce nombre avec un entier signé</summary>
			/// <returns>+1 si on est plus grand que <paramref name="value"/>. -1 si on est plus petit que <paramref name="value"/>. 0 si on est égal à <paramref name="value"/></returns>
			[Pure]
			public int CompareTo(Kind kind, long value)
			{
				switch (kind)
				{
					case Kind.Decimal:
					{
						return this.Decimal.CompareTo(value);
					}
					case Kind.Double:
					{
						return this.Double.CompareTo(value);
					}
					case Kind.Signed:
					{
						return this.Signed.CompareTo(value);
					}
					default: //Unsigned
					{
						return value < 0 ? +1 : this.Unsigned.CompareTo((ulong)value);
					}
				}
			}

			/// <summary>Compare ce nombre avec un entier signé</summary>
			/// <returns>+1 si on est plus grand que <paramref name="value"/>. -1 si on est plus petit que <paramref name="value"/>. 0 si on est égal à <paramref name="value"/></returns>
			[Pure]
			public int CompareTo(Kind kind, ulong value)
			{
				switch (kind)
				{
					case Kind.Decimal:
					{
						return this.Decimal.CompareTo(value);
					}
					case Kind.Double:
					{
						return this.Double.CompareTo(value);
					}
					case Kind.Signed:
					{
						long x = this.Signed;
						return x < 0 ? -1 :  ((ulong)x).CompareTo(value);
					}
					default: //Unsigned
					{
						return this.Unsigned.CompareTo(value);
					}
				}
			}

			/// <summary>Compare ce nombre avec un entier signé</summary>
			/// <returns>+1 si on est plus grand que <paramref name="value"/>. -1 si on est plus petit que <paramref name="value"/>. 0 si on est égal à <paramref name="value"/></returns>
			[Pure]
			public int CompareTo(Kind kind, double value)
			{
				switch (kind)
				{
					case Kind.Decimal:
					{
						return this.Decimal.CompareTo((decimal)value);
					}
					case Kind.Double:
					{
						return this.Double.CompareTo(value);
					}
					case Kind.Signed:
					{
						return ((double)this.Signed).CompareTo(value);
					}
					default: //Unsigned
					{
						return ((double)this.Unsigned).CompareTo(value);
					}
				}
			}

			/// <summary>Compare ce nombre avec un entier signé</summary>
			/// <returns>+1 si on est plus grand que <paramref name="value"/>. -1 si on est plus petit que <paramref name="value"/>. 0 si on est égal à <paramref name="value"/></returns>
			[Pure]
			public int CompareTo(Kind kind, Decimal value)
			{
				switch (kind)
				{
					case Kind.Decimal:
					{
						return this.Decimal.CompareTo(value);
					}
					case Kind.Double:
					{
						return new Decimal(this.Double).CompareTo(value);
					}
					case Kind.Signed:
					{
						return new Decimal((double) this.Signed).CompareTo(value);
					}
					default: //Unsigned
					{
						return new Decimal((double) this.Unsigned).CompareTo(value);
					}
				}
			}

			#region Conversion...

			[Pure]
			public object? ToObject(Kind kind)
			{
				switch(kind)
				{
					case Kind.Decimal: return this.Decimal;
					case Kind.Double: return this.Double;
					case Kind.Signed: return this.Signed;
					case Kind.Unsigned: return this.Unsigned;
					default: return null; //TODO: throw ?
				}
			}

			[Pure]
			public bool ToBoolean(Kind kind)
			{
				switch (kind)
				{
					case Kind.Decimal: return this.Decimal != 0;
					case Kind.Double: return this.Double != 0;
					case Kind.Signed: return this.Signed != 0;
					case Kind.Unsigned: return this.Unsigned != 0;
					default: return false;
				}
			}

			[Pure]
			public byte ToByte(Kind kind)
			{
				switch (kind)
				{
					case Kind.Decimal: return decimal.ToByte(this.Decimal);
					case Kind.Double: return checked((byte)this.Double);
					case Kind.Signed: return checked((byte)this.Signed);
					case Kind.Unsigned: return checked((byte)this.Unsigned);
					default: return 0;
				}
			}

			[Pure]
			public sbyte ToSByte(Kind kind)
			{
				switch (kind)
				{
					case Kind.Decimal: return decimal.ToSByte(this.Decimal);
					case Kind.Double: return checked((sbyte)this.Double);
					case Kind.Signed: return checked((sbyte)this.Signed);
					case Kind.Unsigned: return checked((sbyte)this.Unsigned);
					default: return 0;
				}
			}

			[Pure]
			public short ToInt16(Kind kind)
			{
				switch (kind)
				{
					case Kind.Decimal: return decimal.ToInt16(this.Decimal);
					case Kind.Double: return checked((short)this.Double);
					case Kind.Signed: return checked((short)this.Signed);
					case Kind.Unsigned: return checked((short)this.Unsigned);
					default: return 0;
				}
			}

			[Pure]
			public ushort ToUInt16(Kind kind)
			{
				switch (kind)
				{
					case Kind.Decimal: return decimal.ToUInt16(this.Decimal);
					case Kind.Double: return checked((ushort)this.Double);
					case Kind.Signed: return checked((ushort)this.Signed);
					case Kind.Unsigned: return checked((ushort)this.Unsigned);
					default: return 0;
				}
			}

			[Pure]
			public int ToInt32(Kind kind)
			{
				switch (kind)
				{
					case Kind.Decimal: return decimal.ToInt32(this.Decimal);
					case Kind.Double: return checked((int)this.Double);
					case Kind.Signed: return checked((int)this.Signed);
					case Kind.Unsigned: return checked((int)this.Unsigned);
					default: return 0;
				}
			}

			[Pure]
			public uint ToUInt32(Kind kind)
			{
				switch (kind)
				{
					case Kind.Decimal: return decimal.ToUInt32(this.Decimal);
					case Kind.Double: return checked((uint)this.Double);
					case Kind.Signed: return checked((uint)this.Signed);
					case Kind.Unsigned: return checked((uint)this.Unsigned);
					default: return 0;
				}
			}

			[Pure]
			public long ToInt64(Kind kind)
			{
				switch (kind)
				{
					case Kind.Decimal: return decimal.ToInt64(this.Decimal);
					case Kind.Double: return checked((long)this.Double);
					case Kind.Signed: return this.Signed;
					case Kind.Unsigned: return checked((long)this.Unsigned);
					default: return 0;
				}
			}

			[Pure]
			public ulong ToUInt64(Kind kind)
			{
				switch (kind)
				{
					case Kind.Decimal: return decimal.ToUInt64(this.Decimal);
					case Kind.Double: return checked((ulong)this.Double);
					case Kind.Signed: return checked((ulong)this.Signed);
					case Kind.Unsigned: return this.Unsigned;
					default: return 0;
				}
			}

			[Pure]
			public float ToSingle(Kind kind)
			{
				switch (kind)
				{
					case Kind.Decimal: return decimal.ToSingle(this.Decimal);
					case Kind.Double: return (float)this.Double;
					case Kind.Signed: return (float)this.Signed;
					case Kind.Unsigned: return (float)this.Unsigned;
					default: return 0;
				}
			}

			[Pure]
			public double ToDouble(Kind kind)
			{
				switch (kind)
				{
					case Kind.Decimal: return decimal.ToDouble(this.Decimal);
					case Kind.Double: return this.Double;
					case Kind.Signed: return (double)this.Signed;
					case Kind.Unsigned: return (double)this.Unsigned;
					default: return 0;
				}
			}

			[Pure]
			public decimal ToDecimal(Kind kind)
			{
				switch (kind)
				{
					case Kind.Decimal: return this.Decimal;
					case Kind.Double: return new Decimal(this.Double);
					case Kind.Signed: return new Decimal(this.Signed);
					case Kind.Unsigned: return new Decimal(this.Unsigned);
					default: return 0;
				}
			}

			#endregion

			#region Arithmetric...

			#region Addition...

			/// <summary>Add a number to another number</summary>
			public static void Add(ref Number xValue, ref Kind xKind, in Number yValue, Kind yKind)
			{
				// We have to handle all combinations of kind for both x and y :(
				// to keep our sanity, we will reuse some methods by swaping the arguments, for ex: long + ulong <=> ulong + long
				switch (xKind)
				{
					case Kind.Signed:
					{
						switch (yKind)
						{
							case Kind.Signed:   AddSignedSigned  (xValue.Signed, yValue.Signed,   out xKind, out xValue); return;
							case Kind.Unsigned: AddSignedUnsigned(xValue.Signed, yValue.Unsigned, out xKind, out xValue); return;
							case Kind.Double:   AddSignedDouble  (xValue.Signed, yValue.Double,   out xKind, out xValue); return;
							case Kind.Decimal:  AddSignedDecimal (xValue.Signed, yValue.Decimal,  out xKind, out xValue); return;
						}
						break;
					}
					case Kind.Unsigned:
					{
						switch (yKind)
						{
							case Kind.Signed:   AddSignedUnsigned  (yValue.Signed,   xValue.Unsigned, out xKind, out xValue); return;
							case Kind.Unsigned: AddUnsignedUnsigned(xValue.Unsigned, yValue.Unsigned, out xKind, out xValue); return;
							case Kind.Double:   AddUnsignedDouble  (xValue.Unsigned, yValue.Double,   out xKind, out xValue); return;
							case Kind.Decimal:  AddUnsignedDecimal (xValue.Unsigned, yValue.Decimal,  out xKind, out xValue); return;
						}
						break;
					}
					case Kind.Double:
					{
						switch (yKind)
						{
							case Kind.Signed:   AddSignedDouble  (yValue.Signed,   xValue.Double,  out xKind, out xValue); return;
							case Kind.Unsigned: AddUnsignedDouble(yValue.Unsigned, xValue.Double,  out xKind, out xValue); return;
							case Kind.Double:   AddDoubleDouble  (xValue.Double,   yValue.Double,  out xKind, out xValue); return;
							case Kind.Decimal:  AddDoubleDecimal (xValue.Double,   yValue.Decimal, out xKind, out xValue); return;
						}
						break;
					}
					case Kind.Decimal:
					{
						switch (yKind)
						{
							case Kind.Signed:   AddSignedDecimal  (yValue.Signed,   xValue.Decimal, out xKind, out xValue); return;
							case Kind.Unsigned: AddUnsignedDecimal(yValue.Unsigned, xValue.Decimal, out xKind, out xValue); return;
							case Kind.Double:   AddDoubleDecimal  (yValue.Double,   xValue.Decimal, out xKind, out xValue); return;
							case Kind.Decimal:  AddDecimalDecimal (xValue.Decimal,  yValue.Decimal, out xKind, out xValue); return;
						}
						break;
					}
				}
				throw new NotSupportedException();
			}

			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			private static void AddSignedSigned(long x, long y, out Kind kind, out Number result)
			{
				kind = Kind.Signed;
				result = new Number(checked(x + y));
			}

			private static void AddSignedUnsigned(long x, ulong y, out Kind kind, out Number result)
			{
				ulong val;
				if (x < 0)
				{ // inverse the order of the arguments, to simplify dealing with negative numbers
					if (x == long.MinValue)
						val = checked(y - 0x8000000000000000UL); // cannot do -(long.MinValue), so special case this one
					else
						val = checked(y - (ulong)(-x)); // (-X) + Y == Y - X
				}
				else
				{
					val = checked((ulong)x + y);
				}

				if (val <= long.MaxValue)
				{ // fits in a signed
					kind = Kind.Signed;
					result = new Number((long)val);
				}
				else
				{ // upgrade to unsigned
					kind = Kind.Unsigned;
					result = new Number(val);
				}
			}

			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			private static void AddSignedDouble(long x, double y, out Kind kind, out Number result)
			{
				kind = Kind.Double;
				result = new Number(x + y);
			}

			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			private static void AddSignedDecimal(long x, decimal y, out Kind kind, out Number result)
			{
				kind = Kind.Decimal;
				result = new Number(x + y);
			}

			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			private static void AddUnsignedUnsigned(ulong x, ulong y, out Kind rKind, out Number result)
			{
				rKind = Kind.Unsigned;
				result = new Number(checked(x + y));
			}

			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			private static void AddUnsignedDouble(ulong x, double y, out Kind rKind, out Number result)
			{
				rKind = Kind.Double;
				result = new Number(x + y);
			}

			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			private static void AddUnsignedDecimal(ulong x, decimal y, out Kind rKind, out Number result)
			{
				rKind = Kind.Double;
				result = new Number(x + y);
			}

			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			private static void AddDoubleDouble(double x, double y, out Kind rKind, out Number result)
			{
				rKind = Kind.Double;
				result = new Number(x + y);
			}

			private static void AddDoubleDecimal(double x, decimal y, out Kind rKind, out Number result)
			{
				rKind = Kind.Decimal;
				result = new Number((Decimal) x + y);
			}

			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			private static void AddDecimalDecimal(decimal x, decimal y, out Kind rKind, out Number result)
			{
				rKind = Kind.Decimal;
				result = new Number(x + y);
			}

			#endregion

			#region Addition...

			/// <summary>Add a number to another number</summary>
			public static void Multiply(ref Number xValue, ref Kind xKind, in Number yValue, Kind yKind)
			{
				// We have to handle all combinations of kind for both x and y :(
				// to keep our sanity, we will reuse some methods by swaping the arguments, for ex: long + ulong <=> ulong + long
				switch (xKind)
				{
					case Kind.Signed:
					{
						switch (yKind)
						{
							case Kind.Signed:   MultiplySignedSigned  (xValue.Signed, yValue.Signed,   out xKind, out xValue); return;
							case Kind.Unsigned: MultiplySignedUnsigned(xValue.Signed, yValue.Unsigned, out xKind, out xValue); return;
							case Kind.Double:   MultiplySignedDouble  (xValue.Signed, yValue.Double,   out xKind, out xValue); return;
							case Kind.Decimal:  MultiplySignedDecimal (xValue.Signed, yValue.Decimal,  out xKind, out xValue); return;
						}
						break;
					}
					case Kind.Unsigned:
					{
						switch (yKind)
						{
							case Kind.Signed:   MultiplySignedUnsigned  (yValue.Signed,   xValue.Unsigned, out xKind, out xValue); return;
							case Kind.Unsigned: MultiplyUnsignedUnsigned(xValue.Unsigned, yValue.Unsigned, out xKind, out xValue); return;
							case Kind.Double:   MultiplyUnsignedDouble  (xValue.Unsigned, yValue.Double,   out xKind, out xValue); return;
							case Kind.Decimal:  MultiplyUnsignedDecimal (xValue.Unsigned, yValue.Decimal,  out xKind, out xValue); return;
						}
						break;
					}
					case Kind.Double:
					{
						switch (yKind)
						{
							case Kind.Signed:   MultiplySignedDouble  (yValue.Signed,   xValue.Double,  out xKind, out xValue); return;
							case Kind.Unsigned: MultiplyUnsignedDouble(yValue.Unsigned, xValue.Double,  out xKind, out xValue); return;
							case Kind.Double:   MultiplyDoubleDouble  (xValue.Double,   yValue.Double,  out xKind, out xValue); return;
							case Kind.Decimal:  MultiplyDoubleDecimal (xValue.Double,   yValue.Decimal, out xKind, out xValue); return;
						}
						break;
					}
					case Kind.Decimal:
					{
						switch (yKind)
						{
							case Kind.Signed:   MultiplySignedDecimal  (yValue.Signed,   xValue.Decimal, out xKind, out xValue); return;
							case Kind.Unsigned: MultiplyUnsignedDecimal(yValue.Unsigned, xValue.Decimal, out xKind, out xValue); return;
							case Kind.Double:   MultiplyDoubleDecimal  (yValue.Double,   xValue.Decimal, out xKind, out xValue); return;
							case Kind.Decimal:  MultiplyDecimalDecimal (xValue.Decimal,  yValue.Decimal, out xKind, out xValue); return;
						}
						break;
					}
				}
				throw new NotSupportedException();
			}

			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			private static void MultiplySignedSigned(long x, long y, out Kind kind, out Number result)
			{
				kind = Kind.Signed;
				result = new Number(checked(x * y));
			}

			private static void MultiplySignedUnsigned(long x, ulong y, out Kind kind, out Number result)
			{
				if (x < 0)
				{ // the result must be signed!
					kind = Kind.Signed;
					result = new Number(checked(x * (long) y));
					return;
				}
				ulong val = checked((ulong) x * y);

				if (val <= long.MaxValue)
				{ // fits in a signed
					kind = Kind.Signed;
					result = new Number((long) val);
				}
				else
				{ // upgrade to unsigned
					kind = Kind.Unsigned;
					result = new Number(val);
				}
			}

			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			private static void MultiplySignedDouble(long x, double y, out Kind kind, out Number result)
			{
				kind = Kind.Double;
				result = new Number(x * y);
			}

			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			private static void MultiplySignedDecimal(long x, decimal y, out Kind kind, out Number result)
			{
				kind = Kind.Decimal;
				result = new Number(x * y);
			}

			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			private static void MultiplyUnsignedUnsigned(ulong x, ulong y, out Kind rKind, out Number result)
			{
				rKind = Kind.Unsigned;
				result = new Number(checked(x * y));
			}

			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			private static void MultiplyUnsignedDouble(ulong x, double y, out Kind rKind, out Number result)
			{
				rKind = Kind.Double;
				result = new Number(x * y);
			}

			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			private static void MultiplyUnsignedDecimal(ulong x, decimal y, out Kind rKind, out Number result)
			{
				rKind = Kind.Double;
				result = new Number(x * y);
			}

			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			private static void MultiplyDoubleDouble(double x, double y, out Kind rKind, out Number result)
			{
				rKind = Kind.Double;
				result = new Number(x * y);
			}

			private static void MultiplyDoubleDecimal(double x, decimal y, out Kind rKind, out Number result)
			{
				rKind = Kind.Decimal;
				result = new Number((Decimal) x * y);
			}

			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			private static void MultiplyDecimalDecimal(decimal x, decimal y, out Kind rKind, out Number result)
			{
				rKind = Kind.Decimal;
				result = new Number(x * y);
			}

			#endregion

			#endregion
		}

		#endregion

		#region Private Fields...

		// instance = 16 bytes
		private readonly Number m_value;   // 16 bytes
		private readonly Kind m_kind;      //  4 bytes

		private string? m_literal; //  8 + (26 + length * 2) **** OMG!!! ****
		// note: il reste 4 bytes de padding dans l'objet lui-même!

		// Memory Footprint:
		// -----------------
		//
		// MEM = (16 + 16 + 4 + 8 + (4)) + (26 + length*2)
		//     = 74 + length*2
		//
		// -> "1"            =>  76 bytes !
		// -> "1.0"          =>  80 bytes !
		// -> "1234"         =>  82 bytes !
		// -> systime        => 102 bytes !!
		// -> Math.PI        => 114 bytes !!
		// Pour comparaison:
		// -> long, double	 =>  8 bytes
		// -> decimal        => 16 bytes
		// -> boxed(long)    => 24 bytes
		// -> boxed(double)  => 24 bytes
		// -> boxed(decimal) => 32 bytes

		// int[60] => 264 bytes
		// long[60] => 504 bytes
		// JsonArray[60 x JsonNumber]=> 64 + 60*8 + 60*~82 => 5,464 bytes => x10 !!!

		#endregion

		#region Constructors...

		private JsonNumber(Number value, Kind kind, string? literal)
		{
			m_value = value;
			m_kind = kind;
			m_literal = literal;
		}

		[Pure]
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static JsonNumber Return(sbyte value)
		{
			// note: un sbyte est forcément dans le cache.
			return SmallNumbers[(int)value - CACHED_SIGNED_MIN];
		}

		[Pure]
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static JsonValue Return(sbyte? value)
		{
			return value.HasValue ? Return(value.Value) : JsonNull.Null;
		}

		[Pure]
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static JsonNumber Return(string value)
		{
			return CrystalJsonParser.ParseJsonNumber(value) ?? JsonNumber.Zero;
		}

		[Pure]
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static JsonNumber Return(byte value)
		{
			// note: un byte est forcément dans le cache.
			return SmallNumbers[value + CACHED_OFFSET_ZERO];
		}

		[Pure]
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static JsonValue Return(byte? value)
		{
			return value.HasValue ? SmallNumbers[value.Value + CACHED_OFFSET_ZERO] : JsonNull.Null;
		}

		[Pure]
		public static JsonNumber Return(short value)
		{
			return value >= CACHED_SIGNED_MIN && value <= CACHED_SIGNED_MAX
				? SmallNumbers[value - CACHED_SIGNED_MIN]
				: new JsonNumber(new Number(value), Kind.Signed, CrystalJsonFormatter.NumberToString(value));
		}

		[Pure]
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static JsonValue Return(short? value)
		{
			return value.HasValue ? Return(value.Value) : JsonNull.Null;
		}

		[Pure]
		public static JsonNumber Return(ushort value)
		{
			return value <= CACHED_SIGNED_MAX
				? SmallNumbers[value + CACHED_OFFSET_ZERO]
				: new JsonNumber(new Number(value), Kind.Signed, CrystalJsonFormatter.NumberToString(value));
		}

		[Pure]
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static JsonValue Return(ushort? value)
		{
			return value.HasValue ? Return(value.Value) : JsonNull.Null;
		}

		[Pure]
		public static JsonNumber Return(int value)
		{
			return value >= CACHED_SIGNED_MIN && value <= CACHED_SIGNED_MAX
				? SmallNumbers[value - CACHED_SIGNED_MIN]
				: new JsonNumber(new Number(value), Kind.Signed, CrystalJsonFormatter.NumberToString(value));
		}

		/// <summary>Retourne un petit nombre en cache</summary>
		/// <param name="value">Valeur qui doit être comprise dans l'interval [-128, +255]</param>
		/// <returns>JsonNumber en cache correspondant</returns>
		[Pure]
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		internal static JsonNumber GetCachedSmallNumber(int value)
		{
			Contract.Debug.Requires(value >= CACHED_SIGNED_MIN && value <= CACHED_SIGNED_MAX);
			return SmallNumbers[value - CACHED_SIGNED_MIN];
		}

		[Pure]
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static JsonValue Return(int? value)
		{
			return value.HasValue ? Return(value.Value) : JsonNull.Null;
		}

		[Pure]
		public static JsonNumber Return(uint value)
		{
			return value <= CACHED_SIGNED_MAX
				? SmallNumbers[value + CACHED_OFFSET_ZERO]
				: new JsonNumber(new Number(value), Kind.Signed, CrystalJsonFormatter.NumberToString(value));
		}

		[Pure]
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static JsonValue Return(uint? value)
		{
			return value.HasValue ? Return(value.Value) : JsonNull.Null;
		}

		[Pure]
		public static JsonNumber Return(long value)
		{
			return value >= CACHED_SIGNED_MIN && value <= CACHED_SIGNED_MAX
				? SmallNumbers[value - CACHED_SIGNED_MIN]
				: new JsonNumber(new Number(value), Kind.Signed, CrystalJsonFormatter.NumberToString(value));
		}

		[Pure]
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static JsonValue Return(long? value)
		{
			return value.HasValue ? Return(value.Value) : JsonNull.Null;
		}

		[Pure]
		public static JsonNumber Return(ulong value)
		{
			return value <= CACHED_SIGNED_MAX
				? SmallNumbers[value + CACHED_OFFSET_ZERO]
				: new JsonNumber(new Number(value), value <= long.MaxValue ? Kind.Signed : Kind.Unsigned, CrystalJsonFormatter.NumberToString(value));
		}

		[Pure]
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static JsonValue Return(ulong? value)
		{
			return value.HasValue ? Return(value.Value) : JsonNull.Null;
		}

		[Pure]
		public static JsonNumber Return(double value)
		{
			return value == 0d ? DecimalZero
				: value == 1d ? DecimalOne
				: double.IsNaN(value) ? NaN
				: new JsonNumber(new Number(value), Kind.Double, CrystalJsonFormatter.NumberToString(value));
		}

		[Pure]
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static JsonValue Return(double? value)
		{
			return value.HasValue ? Return(value.Value) : JsonNull.Null;
		}

		[Pure]
		public static JsonNumber Return(float value)
		{
			return value == 0f ? DecimalZero
				: value == 1f ? DecimalOne
				: float.IsNaN(value) ? NaN
				: new JsonNumber(new Number(value), Kind.Double, CrystalJsonFormatter.NumberToString(value));
		}

		[Pure]
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static JsonValue Return(float? value)
		{
			return value.HasValue ? Return(value.Value) : JsonNull.Null;
		}

		[Pure]
		public static JsonNumber Return(decimal value)
		{
			return new JsonNumber(new Number(value), Kind.Decimal, CrystalJsonFormatter.NumberToString(value));
		}

		[Pure]
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static JsonValue Return(decimal? value)
		{
			return value.HasValue ? Return(value.Value) : JsonNull.Null;
		}

		[Pure]
		public static JsonNumber Return(TimeSpan value)
		{
			return value == TimeSpan.Zero ? DecimalZero : Return(value.TotalSeconds);
		}

		[Pure]
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static JsonValue Return(TimeSpan? value)
		{
			return value.HasValue ? Return(value.Value) : JsonNull.Null;
		}

		[Pure]
		public static JsonNumber Return(DateTime value)
		{
			// Nb de secondes écoulées depuis le 1970-01-01Z, MinValue -> 0, MaxValue -> NaN
			const long UNIX_EPOCH_TICKS = 621355968000000000L;
			return value == DateTime.MinValue ? DecimalZero
			     : value == DateTime.MaxValue ? NaN
			     : Return((double) (value.ToUniversalTime().Ticks - UNIX_EPOCH_TICKS) / TimeSpan.TicksPerSecond);
		}

		[Pure]
		public static JsonValue Return(DateTime? value)
		{
			return value.HasValue ? Return(value.Value) : JsonNull.Null;
		}

		[Pure]
		public static JsonNumber Return(NodaTime.Instant value)
		{
			return value != default(NodaTime.Instant) ? Return((value - default(NodaTime.Instant)).TotalSeconds) : DecimalZero;
		}

		[Pure]
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static JsonValue Return(NodaTime.Instant? value)
		{
			return value.HasValue ? Return(value.Value) : JsonNull.Null;
		}

		[Pure]
		public static JsonNumber Return(NodaTime.Duration value)
		{
			if (value == NodaTime.Duration.Zero)
				return DecimalZero;
			else
				return Return((double)value.BclCompatibleTicks / NodaTime.NodaConstants.TicksPerSecond);
		}

		[Pure]
		public static JsonValue Return(NodaTime.Duration? value)
		{
			return value.HasValue ? Return(value.Value) : JsonNull.Null;
		}

		[Pure]
		internal static JsonNumber ParseSigned(long value, string? literal)
		{
			if (value >= CACHED_SIGNED_MIN && value <= CACHED_SIGNED_MAX)
			{ // interning du pauvre
				var num = SmallNumbers[value - CACHED_SIGNED_MIN];
				if (literal == null || num.Literal == literal) return num;
			}

			return new JsonNumber(new Number(value), Kind.Signed, literal);
		}

		[Pure]
		internal static JsonNumber ParseUnsigned(ulong value, string? literal)
		{
			if (value <= CACHED_SIGNED_MAX)
			{ // interning du pauvre
				var num = SmallNumbers[value + CACHED_OFFSET_ZERO];
				if (literal == null || num.Literal == literal) return num;
			}

			return new JsonNumber(new Number(value), value <= long.MaxValue ? Kind.Signed : Kind.Unsigned, literal);
		}

		[Pure]
		internal static JsonNumber Parse(double value, string? literal)
		{
			long l = (long)value;
			if (l == value)
			{
				return l == 0 ? Zero : l == 1 ? One : new JsonNumber(new Number(l), Kind.Signed, literal);
			}
			else
				return new JsonNumber(new Number(value), Kind.Double, literal);
		}

		[Pure]
		internal static JsonNumber Parse(decimal value, string literal)
		{
			//TODO: check for zero, integers, ...
			return new JsonNumber(new Number(value), Kind.Decimal, literal);
		}

		internal static JsonNumber Parse(ReadOnlySpan<byte> value)
		{
			if (value.Length <= 0) throw ThrowHelper.ArgumentException(nameof(value), "Size must be at least one");
			unsafe
			{
				fixed(byte* ptr = value)
				{
					return Parse(ptr, value.Length);
				}
			}
		}

		[Pure]
		internal static unsafe JsonNumber Parse(byte* ptr, int size)
		{
			Contract.PointerNotNull(ptr);
			if (size <= 0) throw ThrowHelper.ArgumentOutOfRangeException(nameof(size), "Size must be at least one");

			string literal = new string((sbyte*)ptr, 0, size); //ASCII is ok

			var num = CrystalJsonParser.ParseJsonNumber(literal);
			if (num == null) throw ThrowHelper.FormatException("Invalid number literal '{0}'.", literal);
			return num;
		}

		#endregion

		/// <summary>Indique s'il s'agit d'un nombre à virgule (true), ou d'un entier (false)</summary>
		/// <remarks>Il est possible que "2.0" soit considéré comme décimal!</remarks>
		public bool IsDecimal => m_kind >= Kind.Double;

		/// <summary>Indique que le nombre est un entier positif supérieur à long.MaxValue</summary>
		public bool IsUnsigned => m_kind == Kind.Unsigned;

		/// <summary>Indique si le nombre est négative</summary>
		/// <returns>True si le nombre est inférieur à 0, ou false s'il est supérieur ou égal à 0</returns>
		public bool IsNegative => m_value.IsNegative(m_kind);

		/// <summary>Literal representation of the number (as it appeared in the original JSON document)</summary>
		public string Literal => m_literal ?? ComputeLiteral();

		[Pure, MethodImpl(MethodImplOptions.NoInlining)]
		private string ComputeLiteral()
		{
			string literal = ComputeLiteral(in m_value, m_kind);
			m_literal = literal;
			return literal;
		}

		private static string ComputeLiteral(in Number number, Kind kind)
		{
			switch (kind)
			{
				case Kind.Signed: return CrystalJsonFormatter.NumberToString(number.Signed);
				case Kind.Unsigned: return CrystalJsonFormatter.NumberToString(number.Unsigned);
				case Kind.Double: return CrystalJsonFormatter.NumberToString(number.Double);
				case Kind.Decimal: return CrystalJsonFormatter.NumberToString(number.Decimal);
				default: throw new ArgumentException(nameof(kind));
			}
		}

		/// <summary>Test si le nombre est compris entre deux bornes entières</summary>
		/// <param name="minInclusive">Valeur minimum (incluse)</param>
		/// <param name="maxInclusive">Valeur maximum (incluse)</param>
		/// <returns>True si <paramref name="minInclusive"/> &lt;= x &lt;= <paramref name="maxInclusive"/></returns>
		public bool IsBetween(long minInclusive, long maxInclusive)
		{
			return (m_value.CompareTo(m_kind, minInclusive) * -m_value.CompareTo(m_kind, maxInclusive)) >= 0;
		}

		/// <summary>Test si le nombre est compris entre deux bornes entières</summary>
		/// <param name="minInclusive">Valeur minimum (incluse)</param>
		/// <param name="maxInclusive">Valeur maximum (incluse)</param>
		/// <returns>True si <paramref name="minInclusive"/> &lt;= x &lt;= <paramref name="maxInclusive"/></returns>
		public bool IsBetween(ulong minInclusive, ulong maxInclusive)
		{
			return (m_value.CompareTo(m_kind, minInclusive) * -m_value.CompareTo(m_kind, maxInclusive)) >= 0;
		}

		/// <summary>Test si le nombre est un entier compris entre deux bornes</summary>
		/// <param name="minInclusive">Valeur minimum (incluse)</param>
		/// <param name="maxInclusive">Valeur maximum (incluse)</param>
		/// <returns>True si <paramref name="minInclusive"/> &lt;= x &lt;= <paramref name="maxInclusive"/></returns>
		public bool IsBetween(double minInclusive, double maxInclusive)
		{
			return (m_value.CompareTo(m_kind, minInclusive) * -m_value.CompareTo(m_kind, maxInclusive)) >= 0;
		}

		#region JsonValue Members...

		public override JsonType Type => JsonType.Number;

		public override bool IsDefault => m_value.IsDefault(m_kind);

		/// <summary>Retourne la valeur de l'objet en utilisant le type le plus adapté</summary>
		/// <returns>Retourne un int/long pour des entiers, ou un decimal pour les nombres à virgules</returns>
		/// <remarks>Pour les entiers: si la valeur est entre int.MinValue et int.MaxValue, elle sera castée en int. Sinon elle sera castée en long.</remarks>
		public override object? ToObject()
		{
			return m_value.ToObject(m_kind);
		}

		public override object? Bind(Type? type, ICrystalJsonTypeResolver? resolver = null)
		{
			if (type == null || type == typeof(object))
			{
				return ToObject();
			}

			// On utilise le TypeCode
			// ATTENTION: les enums ont le typecode de leur underlying type (souvent int32)
			if (type.IsPrimitive)
			{
				//attention: decimal et DateTime ne sont pas IsPrimitive !
				switch (System.Type.GetTypeCode(type))
				{
					case TypeCode.Boolean: return ToBoolean();
					case TypeCode.Char: return ToChar();
					case TypeCode.SByte: return ToSByte();
					case TypeCode.Byte: return ToByte();
					case TypeCode.Int16: return ToInt16();
					case TypeCode.UInt16: return ToUInt16();
					case TypeCode.Int32: return ToInt32();
					case TypeCode.UInt32: return ToUInt32();
					case TypeCode.Int64: return ToInt64();
					case TypeCode.UInt64: return ToUInt64();
					case TypeCode.Single: return ToSingle();
					case TypeCode.Double: return ToDouble();
					case TypeCode.Object:
					{
						if (type == typeof(IntPtr))
						{
							return new IntPtr(ToInt64());
						}
					}
					// on tente notre chance
					return Convert.ChangeType(m_value, type, NumberFormatInfo.InvariantInfo);
				}
			}

			if (type == typeof(string))
			{ // conversion direct en string
				return ToString();
			}

			if (type.IsEnum)
			{ // Enumeration
				// on convertit en int d'abord, car decimal=>enum n'est pas supporté...
				// note: une enum n'est pas forcément un Int32, donc on est obligé d'abord de convertir vers le UnderlyingType (récursivement)
				return Enum.ToObject(type, Bind(type.GetEnumUnderlyingType(), resolver));
			}

			if (type == typeof(DateTime))
			{ // nombre de jours écoulés depuis Unix Epoch
				return ToDateTime();
			}

			if (type == typeof(TimeSpan))
			{ // c'est le nombre de secondes écoulées qui est stocké
				return ToTimeSpan();
			}

			if (typeof(NodaTime.Duration) == type)
			{
				return ToDuration();
			}

			if (typeof(NodaTime.Instant) == type)
			{
				return ToInstant();
			}

			if (type == typeof(decimal))
			{ // note: n'est pas un primitive
				return ToDecimal();
			}

			if (type == typeof(JsonValue) || type == typeof(JsonNumber))
			{
				return this;
			}

			var nullableType = Nullable.GetUnderlyingType(type);
			if (nullableType != null)
			{ // si on est dans un JsonNumber c'est qu'on n'est pas null, donc traite les Nullable<T> comme des T
				// cas les plus fréquents...

				// rappel recursivement avec le type de base
				return Bind(nullableType, resolver);
			}

			// autre ??

			if (typeof(IJsonBindable).IsAssignableFrom(type))
			{ // on tente notre chance...
				var obj = (IJsonBindable) Activator.CreateInstance(type);
				obj.JsonUnpack(this, resolver ?? CrystalJson.DefaultResolver);
				return obj;
			}

			resolver = resolver ?? CrystalJson.DefaultResolver;

			// passe par un custom binder?
			// => gère le cas des classes avec un ctor DuckTyping, ou des méthodes statiques
			var def = resolver.ResolveJsonType(type);
			if (def?.CustomBinder != null)
			{
				return def.CustomBinder(this, type, resolver);
			}

#if DEBUG
			throw new InvalidOperationException($"Doesn't know how to bind a JsonNumber into a value of type {type.GetFriendlyName()}");
#else
			//TODO!?
			return null;
#endif
		}

		internal override bool IsSmallValue() => true;

		internal override bool IsInlinable() => true;

		#endregion

		#region IJsonSerializable

		public override void JsonSerialize(CrystalJsonWriter writer)
		{
			if (m_literal != null)
			{
				writer.WriteRaw(m_literal);
			}
			else
			{
				switch (m_kind)
				{
					case Kind.Signed: writer.WriteValue(m_value.Signed); break;
					case Kind.Unsigned: writer.WriteValue(m_value.Unsigned); break;
					case Kind.Double: writer.WriteValue(m_value.Double); break;
					case Kind.Decimal: writer.WriteValue(m_value.Decimal); break;
				}
			}
		}

		#endregion

		#region ToXXX() ...

		public override bool ToBoolean()
		{
			return m_value.ToBoolean(m_kind);
		}

		public override byte ToByte()
		{
			return m_value.ToByte(m_kind);
		}

		public override sbyte ToSByte()
		{
			return m_value.ToSByte(m_kind);
		}

		public override short ToInt16()
		{
			return m_value.ToInt16(m_kind);
		}

		public override ushort ToUInt16()
		{
			return m_value.ToUInt16(m_kind);
		}

		public override int ToInt32()
		{
			return m_value.ToInt32(m_kind);
		}

		public override uint ToUInt32()
		{
			return m_value.ToUInt32(m_kind);
		}

		public override long ToInt64()
		{
			return m_value.ToInt64(m_kind);
		}

		public override ulong ToUInt64()
		{
			return m_value.ToUInt64(m_kind);
		}

		public override float ToSingle()
		{
			return m_value.ToSingle(m_kind);
		}
		public override double ToDouble()
		{
			return m_value.ToDouble(m_kind);
		}

		public override decimal ToDecimal()
		{
			return m_value.ToDecimal(m_kind);
		}

		/// <summary>Convertit un JSON Number, correspondant au nombre de secondes écoulés depuis Unix Epoch, en DateTime UTC</summary>
		/// <returns>DateTime (UTC) égale à epoch(1970-1-1Z) + seconds(value)</returns>
		public override DateTime ToDateTime()
		{
			// NaN est considéré comme MaxValue
			double value = ToDouble();
			if (double.IsNaN(value)) return DateTime.MaxValue;

			// les DateTime sont stockés en nombre de secondes écoulées depuis Unix Epoch
			const long UNIX_EPOCH_TICKS = 621355968000000000L;
			double ticks = Math.Round(value * NodaTime.NodaConstants.TicksPerSecond, MidpointRounding.AwayFromZero) + UNIX_EPOCH_TICKS;

			// bound checking
			if (ticks >= long.MaxValue) return DateTime.MaxValue;
			if (ticks <= 0) return DateTime.MinValue;

			return new DateTime((long) ticks, DateTimeKind.Utc);
		}

		public override DateTimeOffset ToDateTimeOffset()
		{
			//REVIEW: comment gérer proprement ce cas? Les dates numériques sont en nb de jours depuis Unix Epoch (UTC), et n'ont pas d'informations sur la TimeZone.
			// => pour le moment, on retourne un DateTimeOffset en GMT...
			return new DateTimeOffset(this.ToDateTime());
		}

		private const double TimeSpanMaxValueInTicks = 9.2233720368547758E+18d;
		private const double TimeSpanMinValueInTicks = -9.2233720368547758E+18d;

		public override TimeSpan ToTimeSpan()
		{
			// Les timespan correspondent au nombre de secondes écoulées

			// on convertit d'abord en Ticks
			double ticks = Math.Round(this.ToDouble() * TimeSpan.TicksPerSecond, MidpointRounding.AwayFromZero);

			// attention: le nombre de secondes peut dépasser TimeSpan.MaxValue ou TimeSpan.MinValue!
			if (ticks >= TimeSpanMaxValueInTicks) return TimeSpan.MaxValue;
			if (ticks <= TimeSpanMinValueInTicks) return TimeSpan.MinValue;

			return new TimeSpan((long)ticks);
		}

		private NodaTime.Duration ConvertSecondsToDurationUnsafe(double seconds)
		{
			if (seconds < 100_000_000) // ~1157 jours
			{
				// on peut a priori garder la précision en nanoseconds
				return Duration.FromSeconds(seconds);
			}
			// BUGBUG: il y a un problème de précision quand on représente un nombre trop grand en nanoseconds
			// => double n'a que 56bits de précision sur la mantisse, qui faut qu'on ne peut pas le convertir
			//    en nanosecondes sans introduire des erreurs sur les derniers digits
			// La seule solution ici est de rester en précision 1/10_000_000 sec (comme BCL) mais du coup
			// cela veut dire qu'un Instant précis en nanosecondes ne va pas roundtrip correctement en JSON!!!
			//TODO: est-ce qu'il y a une manière plus propre?

			// on ne peut PAS utiliser 'double' comme type intermédiaire car il va introduire de la corruption dans les derniers digits
			// => c'est lié au fait qu'un double est une fraction binaire alors qu'ici on est en base 10

			// A la place, on va être obligé de reparser le literal avec "decimal.Parse" qui lui travaille en interne en base 10

			decimal sec2 = decimal.Parse(m_literal, CultureInfo.InvariantCulture);
			decimal sec = Math.Truncate(sec2);
			decimal ns = sec2 - sec;
			ns *= 1_000_000_000;
			return Duration.FromSeconds((long) sec2).Plus(Duration.FromNanoseconds((long) ns));
		}

		public override NodaTime.Duration ToDuration()
		{
			// les Duration sont stockés en nombre de secondes écoulées

			var seconds = ToDouble();

			// NaN veut dire "MaxValue"
			if (double.IsNaN(seconds)) return NodaTime.Duration.MaxValue;

			// bound checking
			if (seconds <= -1449551462400d /* MinValue en secondes */) return NodaTime.Duration.MinValue;
			if (seconds >= 1449551462400 /* MaxValue en secondes */) return NodaTime.Duration.MaxValue;

			return ConvertSecondsToDurationUnsafe(seconds);
		}

		/// <summary>Convertit un JSON Number, correspondant au nombre de secondes écoulés depuis Unix Epoch, en Instant</summary>
		/// <returns>Instant (UTC) égale à epoch(1970-1-1Z) + seconds(value)</returns>
		public override NodaTime.Instant ToInstant()
		{
			// les Instants sont stockés en nombre de secondes écoulées depuis Unix Epoch
			var secondsSinceEpoch = ToDouble();

			// NaN veut dire "MaxValue"
			if (double.IsNaN(secondsSinceEpoch)) return NodaTime.Instant.MaxValue;

			// bound checking
			if (secondsSinceEpoch <= -1449551462400d /* MinValue en secondes */) return NodaTime.Instant.MinValue;
			if (secondsSinceEpoch >= 1449551462400 /* MaxValue en secondes */) return NodaTime.Instant.MaxValue;

			return default(NodaTime.Instant).Plus(ConvertSecondsToDurationUnsafe(secondsSinceEpoch));
		}

		public override char ToChar()
		{
			return (char)ToInt16();
		}

		public override TEnum ToEnum<TEnum>()
		{
			//BUGBUG: il faudrait normalement tester si typeof(TEnum).GetEnumUnderlyingType() == int32 !!
			int value = ToInt32();
			var o = Convert.ChangeType(value, typeof(TEnum));
			//note: pas de test Enum.IsDefined, car on pourrait écrire "return (FooEnum)42;" en code meme si 42 n'existe pas dans l'enum
			return (TEnum)o;
		}

		#endregion

		#region IEquatable<...>

		public override bool Equals(object? value)
		{
			if (value == null) return false;
			switch (System.Type.GetTypeCode(value.GetType()))
			{
				case TypeCode.Int32: return Equals((int)value);
				case TypeCode.Int64: return Equals((long)value);
				case TypeCode.UInt32: return Equals((int)value);
				case TypeCode.UInt64: return Equals((long)value);
				case TypeCode.Single: return Equals((float)value);
				case TypeCode.Double: return Equals((double)value);
				case TypeCode.Decimal: return Equals((decimal)value);
				case TypeCode.Object:
				{
					if (value is TimeSpan) return Equals((TimeSpan)value);
					break;
				}
			}
			return base.Equals(value);
		}

		public override bool Equals(JsonValue? value)
		{
			if (value == null) return false;
			switch (value.Type)
			{
				case JsonType.Number: return Equals(value as JsonNumber);
				case JsonType.String: return Equals(value as JsonString);
				case JsonType.Boolean: return Equals(value as JsonBoolean);
				case JsonType.DateTime: return Equals(value as JsonDateTime);
				default: return false;
			}
		}

		public bool Equals(JsonNumber? value)
		{
			if (value == null) return false;
			return Number.Equals(in m_value, m_kind, in value.m_value, value.m_kind);
		}

		public bool Equals(JsonString? value)
		{
			if (value == null) return false;

			var text = value.Value;
			if (string.IsNullOrEmpty(text)) return false;

			// shortcut si le premier char n'est pas un nombre
			var c = text[0];
			if (!char.IsDigit(c) && c != '-' && c != '+') return false;

			switch (m_kind)
			{
				case Kind.Decimal:
				{
					return value.TryConvertDecimal(out var x) && m_value.Decimal == x;
				}
				case Kind.Double:
				{
					return value.TryConvertDouble(out var x) && m_value.Double == x;
				}
				case Kind.Signed:
				{
					return value.TryConvertInt64(out var x) && m_value.Signed == x;
				}
				case Kind.Unsigned:
				{
					return value.TryConvertUInt64(out var x) && m_value.Unsigned == x;
				}
			}

			return false;
		}

		public bool Equals(JsonBoolean? value)
		{
			return value != null && ToBoolean() == value.Value;
		}

		public bool Equals(JsonDateTime? value)
		{
			return value != null && ToDouble() == value.ToDouble();
		}

		public bool Equals(int value)
		{
			switch (m_kind)
			{
				case Kind.Decimal: return m_value.Decimal == new Decimal(value);
				case Kind.Double: return m_value.Double == (double)value;
				case Kind.Signed: return m_value.Signed == value;
				case Kind.Unsigned: return value >= 0 && m_value.Unsigned == (ulong)value;
				default: return false;
			}
		}

		public bool Equals(uint value)
		{
			switch (m_kind)
			{
				case Kind.Decimal: return m_value.Decimal == new Decimal(value);
				case Kind.Double: return m_value.Double == (double)value;
				case Kind.Signed: return m_value.Signed == value;
				case Kind.Unsigned: return m_value.Unsigned == value;
				default: return false;
			}
		}

		public bool Equals(long value)
		{
			switch (m_kind)
			{
				case Kind.Decimal: return m_value.Decimal == new Decimal(value);
				case Kind.Double: return m_value.Double == (double)value;
				case Kind.Signed: return m_value.Signed == value;
				case Kind.Unsigned: return value >= 0 && m_value.Unsigned == (ulong)value;
				default: return false;
			}
		}

		public bool Equals(ulong value)
		{
			switch (m_kind)
			{
				case Kind.Decimal: return m_value.Decimal == new Decimal(value);
				case Kind.Double: return m_value.Double == (double)value;
				case Kind.Signed: return m_value.Signed == (long)value;
				case Kind.Unsigned: return m_value.Unsigned == value;
				default: return false;
			}
		}

		public bool Equals(float value)
		{
			switch (m_kind)
			{
				case Kind.Decimal: return m_value.Decimal == new Decimal(value);
				case Kind.Double: return m_value.Double == (double)value;
				case Kind.Signed: return m_value.Signed == value;
				case Kind.Unsigned: return value >= 0 && (float)m_value.Unsigned == value;
				default: return false;
			}
		}

		public bool Equals(double value)
		{
			switch (m_kind)
			{
				case Kind.Decimal: return m_value.Decimal == new Decimal(value);
				case Kind.Double: return m_value.Double == value;
				case Kind.Signed: return (double)m_value.Signed == value;
				case Kind.Unsigned: return value >= 0 && (double)m_value.Unsigned == value;
				default: return false;
			}
		}

		public bool Equals(decimal value)
		{
			switch (m_kind)
			{
				case Kind.Decimal: return m_value.Decimal == value;
				case Kind.Double: return new Decimal(m_value.Double) == value;
				case Kind.Signed: return new Decimal(m_value.Signed) == value;
				case Kind.Unsigned: return value >= 0 && new Decimal(m_value.Unsigned) == value;
				default: return false;
			}
		}

		public bool Equals(TimeSpan value)
		{
			return ToDouble() == value.TotalSeconds;
		}

		public override int GetHashCode()
		{
			// pb: que ce soit 1, 1UL, 1.0d ou 1m, il faut que le hashcode soit égal !
			switch(m_kind)
			{
				case Kind.Decimal:
				{
					decimal d = m_value.Decimal;
					long l = (long)d;
					return ((decimal)l == d) ? l.GetHashCode() : d.GetHashCode();
				}
				case Kind.Double:
				{
					double d = m_value.Double;
					if (d < 0)
					{
						long x = (long)d;
						if ((double)x == d) return x.GetHashCode();
					}
					else
					{
						ulong x = (ulong)d;
						if ((double)x == d) return x.GetHashCode();
					}
					return d.GetHashCode();
				}
				case Kind.Signed: return m_value.Signed.GetHashCode();
				case Kind.Unsigned: return m_value.Unsigned.GetHashCode();
				default: return -1;
			}
		}

		#endregion

		#region IComparable<...>

		public override int CompareTo(JsonValue other)
		{
			if (other is JsonNumber jn) return CompareTo(jn);
			if (other is JsonString js) return CompareTo(js);
			return base.CompareTo(other);
		}

		public int CompareTo(JsonNumber other)
		{
			if (other == null) return +1;
			return Number.CompareTo(in m_value, m_kind, in other.m_value, other.m_kind);
		}

		public int CompareTo(long value)
		{
			return m_value.CompareTo(m_kind, value);
		}

		public int CompareTo(ulong value)
		{
			return m_value.CompareTo(m_kind, value);
		}

		public int CompareTo(float value)
		{
			return m_value.CompareTo(m_kind, value);
		}

		public int CompareTo(double value)
		{
			return m_value.CompareTo(m_kind, value);
		}

		public int CompareTo(JsonString other)
		{
			if (other == null) return +1;

			var text = other.Value;
			if (string.IsNullOrEmpty(text)) return +1; //REVIEW: ??

			// shortcut si le premier char n'est pas un nombre
			var c = text[0];
			if (char.IsDigit(c) || c == '-' || c == '+')
			{
				switch (m_kind)
				{
					case Kind.Decimal:
					{
						if (other.TryConvertDecimal(out var x)) return m_value.Decimal.CompareTo(x);
						break;
					}
					case Kind.Double:
					{
						if (other.TryConvertDouble(out var x)) return m_value.Double.CompareTo(x);
						break;
					}
					case Kind.Signed:
					{
						if (other.TryConvertInt64(out var x)) return m_value.Signed.CompareTo(x);
						break;
					}
					case Kind.Unsigned:
					{
						if (other.TryConvertUInt64(out var x)) return m_value.Unsigned.CompareTo(x);
						break;
					}
				}
			}

			// compare using string representation
			return string.Compare(this.ToString(), other.Value, StringComparison.Ordinal);
		}

		#endregion

		#region Arithmetic operators

		// uniquement disponible si la valeur est castée en JsonNumber
		// (pas exposés sur JsonValue)

		public static bool operator ==(JsonNumber number, long value)
		{
			return number != null && number.Equals(value);
		}
		public static bool operator !=(JsonNumber number, long value)
		{
			return number == null || !number.Equals(value);
		}
		public static bool operator <(JsonNumber number, long value)
		{
			return number != null && number.CompareTo(value) < 0;
		}
		public static bool operator <=(JsonNumber number, long value)
		{
			return number != null && number.CompareTo(value) <= 0;
		}
		public static bool operator >(JsonNumber number, long value)
		{
			return number != null && number.CompareTo(value) > 0;
		}
		public static bool operator >=(JsonNumber number, long value)
		{
			return number != null && number.CompareTo(value) >= 0;
		}

		public static bool operator ==(JsonNumber number, ulong value)
		{
			return number != null && number.Equals(value);
		}
		public static bool operator !=(JsonNumber number, ulong value)
		{
			return number == null || !number.Equals(value);
		}
		public static bool operator <(JsonNumber number, ulong value)
		{
			return number != null && number.CompareTo(value) < 0;
		}
		public static bool operator <=(JsonNumber number, ulong value)
		{
			return number != null && number.CompareTo(value) <= 0;
		}
		public static bool operator >(JsonNumber number, ulong value)
		{
			return number != null && number.CompareTo(value) > 0;
		}
		public static bool operator >=(JsonNumber number, ulong value)
		{
			return number != null && number.CompareTo(value) >= 0;
		}

		public static bool operator ==(JsonNumber number, float value)
		{
			return number != null && number.Equals(value);
		}
		public static bool operator !=(JsonNumber number, float value)
		{
			return number == null || !number.Equals(value);
		}
		public static bool operator <(JsonNumber number, float value)
		{
			return number != null && number.CompareTo(value) < 0;
		}
		public static bool operator <=(JsonNumber number, float value)
		{
			return number != null && number.CompareTo(value) <= 0;
		}
		public static bool operator >(JsonNumber number, float value)
		{
			return number != null && number.CompareTo(value) > 0;
		}
		public static bool operator >=(JsonNumber number, float value)
		{
			return number != null && number.CompareTo(value) >= 0;
		}

		public static bool operator ==(JsonNumber number, double value)
		{
			return number != null && number.Equals(value);
		}
		public static bool operator !=(JsonNumber number, double value)
		{
			return number == null || !number.Equals(value);
		}
		public static bool operator <(JsonNumber number, double value)
		{
			return number != null && number.CompareTo(value) < 0;
		}
		public static bool operator <=(JsonNumber number, double value)
		{
			return number != null && number.CompareTo(value) <= 0;
		}
		public static bool operator >(JsonNumber number, double value)
		{
			return number != null && number.CompareTo(value) > 0;
		}
		public static bool operator >=(JsonNumber number, double value)
		{
			return number != null && number.CompareTo(value) >= 0;
		}

		[Pure]
		public JsonNumber Plus(JsonNumber number)
		{
			// x + 0 == x; 0 + y == y
			if (number.m_value.IsDefault(m_kind)) return this;
			if (m_value.IsDefault(m_kind)) return number;

			var kind = m_kind;
			var value = m_value;
			Number.Add(ref value, ref kind, in number.m_value, number.m_kind);
			return Return(value, kind);
		}

		[Pure]
		public JsonNumber Multiply(JsonNumber number)
		{
			var kind = m_kind;
			var value = m_value;
			Number.Multiply(ref value, ref kind, in number.m_value, number.m_kind);
			return Return(value, kind);
		}

		/// <summary>Special helper to create a number from its constituents</summary>
		private static JsonNumber Return(Number value, Kind kind)
		{
			switch (kind)
			{
				case Kind.Signed:   return Return(value.Signed);
				case Kind.Unsigned: return Return(value.Unsigned);
				case Kind.Double:   return Return(value.Double);
				case Kind.Decimal:  return Return(value.Decimal);
				default: throw new NotSupportedException();
			}
		}

		#endregion

		public override string ToString()
		{
			return this.Literal;
		}

		public override string ToJson(CrystalJsonSettings? settings = null)
		{
			return this.Literal;
		}

		public override void WriteTo(ref SliceWriter writer)
		{
			writer.WriteStringAscii(m_literal);
		}

	}

}
