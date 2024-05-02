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

//#define ENABLE_GRISU3_STRING_CONVERTER

// ReSharper disable CompareOfFloatsByEqualityOperator
// ReSharper disable RedundantNameQualifier

namespace Doxense.Serialization.Json
{
	using System.Diagnostics;
	using System.Diagnostics.CodeAnalysis;
	using System.Globalization;
	using System.Runtime.CompilerServices;
	using System.Runtime.InteropServices;
	using Doxense.Memory;
	using NodaTime;
	using PureAttribute = System.Diagnostics.Contracts.PureAttribute;

	/// <summary>JSON number</summary>
	[DebuggerDisplay("JSON Number({" + nameof(m_literal) + ",nq})")]
	[DebuggerNonUserCode]
	[JetBrains.Annotations.PublicAPI]
	public sealed class JsonNumber : JsonValue, IEquatable<JsonNumber>, IComparable<JsonNumber>, IEquatable<JsonString>, IEquatable<JsonBoolean>, IEquatable<JsonDateTime>, IEquatable<int>, IEquatable<long>, IEquatable<uint>, IEquatable<ulong>, IEquatable<float>, IEquatable<double>, IEquatable<decimal>, IEquatable<TimeSpan>, IEquatable<Half>
#if NET8_0_OR_GREATER
		, System.Numerics.INumberBase<JsonNumber>
		, System.Numerics.IComparisonOperators<JsonNumber, long, bool>
		, System.Numerics.IComparisonOperators<JsonNumber, ulong, bool>
		, System.Numerics.IComparisonOperators<JsonNumber, float, bool>
		, System.Numerics.IComparisonOperators<JsonNumber, double, bool>
		, System.Numerics.IComparisonOperators<JsonNumber, decimal, bool>
		, System.Numerics.IAdditionOperators<JsonNumber, long, JsonNumber>
		, System.Numerics.IAdditionOperators<JsonNumber, ulong, JsonNumber>
		, System.Numerics.IAdditionOperators<JsonNumber, float, JsonNumber>
		, System.Numerics.IAdditionOperators<JsonNumber, double, JsonNumber>
		, System.Numerics.IAdditionOperators<JsonNumber, decimal, JsonNumber>
		, System.Numerics.ISubtractionOperators<JsonNumber, long, JsonNumber>
		, System.Numerics.ISubtractionOperators<JsonNumber, ulong, JsonNumber>
		, System.Numerics.ISubtractionOperators<JsonNumber, float, JsonNumber>
		, System.Numerics.ISubtractionOperators<JsonNumber, double, JsonNumber>
		, System.Numerics.ISubtractionOperators<JsonNumber, decimal, JsonNumber>
		, System.Numerics.IMultiplyOperators<JsonNumber, long, JsonNumber>
		, System.Numerics.IMultiplyOperators<JsonNumber, ulong, JsonNumber>
		, System.Numerics.IMultiplyOperators<JsonNumber, float, JsonNumber>
		, System.Numerics.IMultiplyOperators<JsonNumber, double, JsonNumber>
		, System.Numerics.IMultiplyOperators<JsonNumber, decimal, JsonNumber>
		, System.Numerics.IDivisionOperators<JsonNumber, long, JsonNumber>
		, System.Numerics.IDivisionOperators<JsonNumber, ulong, JsonNumber>
		, System.Numerics.IDivisionOperators<JsonNumber, float, JsonNumber>
		, System.Numerics.IDivisionOperators<JsonNumber, double, JsonNumber>
		, System.Numerics.IDivisionOperators<JsonNumber, decimal, JsonNumber>
#endif
	{
		/// <summary>Cache of all small numbers, from <see cref="CACHED_SIGNED_MIN"/> to <see cref="CACHED_SIGNED_MAX"/> (included)</summary>
		private static readonly JsonNumber[] SmallNumbers = PreGenSmallNumbers();
		//NOTE: SmallNumbers must be initialized before ALL the other static fields that rely on it!

		internal const int CACHED_SIGNED_MIN = -128;
		internal const int CACHED_SIGNED_MAX = 999; //note: must be at least 255 because all bytes must be in the cache
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

		/// <summary><see langword="0"/> (signed int)</summary>

		public static JsonNumber Zero { get; } = SmallNumbers[0 + CACHED_OFFSET_ZERO];

		/// <summary><see langword="1"/> (signed int)</summary>
		public static JsonNumber One { get; } = SmallNumbers[1 + CACHED_OFFSET_ZERO];

		/// <summary><see langword="-1"/> (signed int)</summary>
		public static JsonNumber MinusOne { get; } = SmallNumbers[-1 + CACHED_OFFSET_ZERO];

		/// <summary><see langword="0.0"/> (double)</summary>
		public static readonly JsonNumber DecimalZero = new(new Number(0d), Kind.Double, "0"); //REVIEW: "0.0" ?

		/// <summary><see langword="1.0"/> (double)</summary>
		public static readonly JsonNumber DecimalOne = new(new Number(1d), Kind.Double, "1"); //REVIEW: "1.0" ?

		/// <summary><see langword="NaN"/> (double)</summary>
		public static readonly JsonNumber NaN = new(new Number(double.NaN), Kind.Double, "NaN");

		/// <summary><see langword="+∞"/> (double)</summary>
		public static readonly JsonNumber PositiveInfinity = new(new Number(double.PositiveInfinity), Kind.Double, "Infinity");

		/// <summary><see langword="-∞"/> (double)</summary>
		public static readonly JsonNumber NegativeInfinity = new(new Number(double.NegativeInfinity), Kind.Double, "-Infinity");

		/// <summary><see langword="π"/> ~= <see langword="3.1415926535897931"/> (double)</summary>
		// ReSharper disable once InconsistentNaming
		public static readonly JsonNumber PI = new(new Number(Math.PI), Kind.Double, "3.1415926535897931");

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
			/// <summary>Integer that can be casted to Int64 (long.MinValue &lt;= x &lt;= long.MaxValue)</summary>
			Signed = 0,
			/// <summary>Positive integer that requires an UInt64 (x > long.MaxValue)</summary>
			Unsigned,
			/// <summary>128-bits decimal floating point number</summary>
			Decimal,
			/// <summary>64-bits IEEE floating point number</summary>
			Double,
		}

		[DebuggerDisplay("Signed={Signed}, Unsigned={Unsigned}, Double={Double}, Decimal={Decimal}")]
		[StructLayout(LayoutKind.Explicit)]
		private readonly struct Number
		{
			//REVIEW: Because we need to support Decimal, the struct takes 16 octets, instead of 8.

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
			public bool IsZero(Kind kind) => kind switch
			{
				Kind.Signed => this.Signed == 0L,
				Kind.Unsigned => this.Unsigned == 0UL,
				Kind.Decimal => this.Decimal == 0m,
				Kind.Double => this.Double == 0d,
				_ => false,
			};

			[Pure]
			public bool IsOne(Kind kind) => kind switch
			{
				Kind.Signed => this.Signed == 1L,
				Kind.Unsigned => this.Unsigned == 1UL,
				Kind.Decimal => this.Decimal == 1m,
				Kind.Double => this.Double == 1d,
				_ => false,
			};

			[Pure]
			public bool IsNegative(Kind kind) => kind switch
			{
				Kind.Signed => this.Signed < 0L,
				Kind.Unsigned => false,
				Kind.Decimal => this.Decimal < 0m,
				Kind.Double => this.Double < 0d,
				_ => false
			};

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

			public static int CompareTo(in Number x, Kind xKind, in Number y, Kind yKind) => yKind switch
			{
				Kind.Decimal => x.CompareTo(xKind, y.ToDecimal(yKind)),
				Kind.Double => x.CompareTo(xKind, y.ToDouble(yKind)),
				Kind.Signed => x.CompareTo(xKind, y.ToInt64(yKind)),
				_ => x.CompareTo(xKind, y.ToUInt64(yKind))
			};

			/// <summary>Compare ce nombre avec un entier signé</summary>
			/// <returns>+1 si on est plus grand que <paramref name="value"/>. -1 si on est plus petit que <paramref name="value"/>. 0 si on est égal à <paramref name="value"/></returns>
			[Pure]
			public int CompareTo(Kind kind, long value) => kind switch
			{
				Kind.Decimal => this.Decimal.CompareTo(value),
				Kind.Double => this.Double.CompareTo(value),
				Kind.Signed => this.Signed.CompareTo(value),
				_ => value < 0 ? +1 : this.Unsigned.CompareTo((ulong) value)
			};

			/// <summary>Compare ce nombre avec un entier signé</summary>
			/// <returns>+1 si on est plus grand que <paramref name="value"/>. -1 si on est plus petit que <paramref name="value"/>. 0 si on est égal à <paramref name="value"/></returns>
			[Pure]
			public int CompareTo(Kind kind, ulong value) => kind switch
			{
				Kind.Decimal => this.Decimal.CompareTo(value),
				Kind.Double => this.Double.CompareTo(value),
				Kind.Signed => this.Signed < 0 ? -1 : ((ulong) this.Signed).CompareTo(value),
				_ => this.Unsigned.CompareTo(value)
			};

			/// <summary>Compare ce nombre avec un entier signé</summary>
			/// <returns>+1 si on est plus grand que <paramref name="value"/>. -1 si on est plus petit que <paramref name="value"/>. 0 si on est égal à <paramref name="value"/></returns>
			[Pure]
			public int CompareTo(Kind kind, double value) => kind switch
			{
				Kind.Decimal => this.Decimal.CompareTo((decimal) value),
				Kind.Double => this.Double.CompareTo(value),
				Kind.Signed => ((double) this.Signed).CompareTo(value),
				_ => ((double) this.Unsigned).CompareTo(value)
			};

			/// <summary>Compare ce nombre avec un entier signé</summary>
			/// <returns>+1 si on est plus grand que <paramref name="value"/>. -1 si on est plus petit que <paramref name="value"/>. 0 si on est égal à <paramref name="value"/></returns>
			[Pure]
			public int CompareTo(Kind kind, decimal value) => kind switch
			{
				Kind.Decimal => this.Decimal.CompareTo(value),
				Kind.Double => new decimal(this.Double).CompareTo(value),
				Kind.Signed => new decimal((double) this.Signed).CompareTo(value),
				_ => new decimal((double) this.Unsigned).CompareTo(value)
			};

			#region Conversion...

			[Pure]
			public object? ToObject(Kind kind) => kind switch
			{
				Kind.Decimal => this.Decimal,
				Kind.Double => this.Double,
				Kind.Signed => this.Signed,
				Kind.Unsigned => this.Unsigned,
				_ => null
			};

			[Pure]
			public bool ToBoolean(Kind kind) => kind switch
			{
				Kind.Decimal => this.Decimal != 0,
				Kind.Double => this.Double != 0,
				Kind.Signed => this.Signed != 0,
				Kind.Unsigned => this.Unsigned != 0,
				_ => false
			};

			[Pure]
			public byte ToByte(Kind kind) => kind switch
			{
				Kind.Decimal => decimal.ToByte(this.Decimal),
				Kind.Double => checked((byte) this.Double),
				Kind.Signed => checked((byte) this.Signed),
				Kind.Unsigned => checked((byte) this.Unsigned),
				_ => 0
			};

			[Pure]
			public sbyte ToSByte(Kind kind) => kind switch
			{
				Kind.Decimal => decimal.ToSByte(this.Decimal),
				Kind.Double => checked((sbyte) this.Double),
				Kind.Signed => checked((sbyte) this.Signed),
				Kind.Unsigned => checked((sbyte) this.Unsigned),
				_ => 0
			};

			[Pure]
			public short ToInt16(Kind kind) => kind switch
			{
				Kind.Decimal => decimal.ToInt16(this.Decimal),
				Kind.Double => checked((short) this.Double),
				Kind.Signed => checked((short) this.Signed),
				Kind.Unsigned => checked((short) this.Unsigned),
				_ => 0
			};

			[Pure]
			public ushort ToUInt16(Kind kind) => kind switch
			{
				Kind.Decimal => decimal.ToUInt16(this.Decimal),
				Kind.Double => checked((ushort) this.Double),
				Kind.Signed => checked((ushort) this.Signed),
				Kind.Unsigned => checked((ushort) this.Unsigned),
				_ => 0
			};

			[Pure]
			public int ToInt32(Kind kind) => kind switch
			{
				Kind.Decimal => decimal.ToInt32(this.Decimal),
				Kind.Double => checked((int) this.Double),
				Kind.Signed => checked((int) this.Signed),
				Kind.Unsigned => checked((int) this.Unsigned),
				_ => 0
			};

			[Pure]
			public uint ToUInt32(Kind kind) => kind switch
			{
				Kind.Decimal => decimal.ToUInt32(this.Decimal),
				Kind.Double => checked((uint) this.Double),
				Kind.Signed => checked((uint) this.Signed),
				Kind.Unsigned => checked((uint) this.Unsigned),
				_ => 0
			};

			[Pure]
			public long ToInt64(Kind kind) => kind switch
			{
				Kind.Decimal => decimal.ToInt64(this.Decimal),
				Kind.Double => checked((long) this.Double),
				Kind.Signed => this.Signed,
				Kind.Unsigned => checked((long) this.Unsigned),
				_ => 0
			};

			[Pure]
			public ulong ToUInt64(Kind kind) => kind switch
			{
				Kind.Decimal => decimal.ToUInt64(this.Decimal),
				Kind.Double => checked((ulong) this.Double),
				Kind.Signed => checked((ulong) this.Signed),
				Kind.Unsigned => this.Unsigned,
				_ => 0
			};

			[Pure]
			public float ToSingle(Kind kind) => kind switch
			{
				Kind.Decimal => decimal.ToSingle(this.Decimal),
				Kind.Double => (float) this.Double,
				Kind.Signed => this.Signed,
				Kind.Unsigned => this.Unsigned,
				_ => 0
			};

			[Pure]
			public double ToDouble(Kind kind) => kind switch
			{
				Kind.Decimal => decimal.ToDouble(this.Decimal),
				Kind.Double => this.Double,
				Kind.Signed => this.Signed,
				Kind.Unsigned => this.Unsigned,
				_ => 0
			};

			[Pure]
			public Half ToHalf(Kind kind) => kind switch
			{
				Kind.Decimal => (Half) decimal.ToDouble(this.Decimal),
				Kind.Double => (Half) this.Double,
				Kind.Signed => (Half) this.Signed,
				Kind.Unsigned => (Half) this.Unsigned,
				_ => default
			};

			[Pure]
			public decimal ToDecimal(Kind kind) => kind switch
			{
				Kind.Decimal => this.Decimal,
				Kind.Double => new decimal(this.Double),
				Kind.Signed => new decimal(this.Signed),
				Kind.Unsigned => new decimal(this.Unsigned),
				_ => 0
			};

			#endregion

			#region Arithmetic...

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

				// unsupported type of combination?
				Contract.Debug.Fail("Unsupported number types");
				throw new NotSupportedException();

				static void AddSignedSigned(long x, long y, out Kind kind, out Number result)
				{
					//note: adding large numbers could overflow signed but still be valid for unsigned
					if (x >= 0 && y >= 0)
					{ // result will be positive
						if (x > 4611686018427387903 || y > 4611686018427387903)
						{ // result may overflow signed
							ulong r = checked((ulong) x + (ulong) y);
							kind = r <= long.MaxValue ? Kind.Signed : Kind.Unsigned;
							result = new Number(r);
							return;
						}
					}

					kind = Kind.Signed;
					result = new Number(checked(x + y));
				}

				static void AddSignedUnsigned(long x, ulong y, out Kind kind, out Number result)
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
						val = checked((ulong) x + y);
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
				static void AddSignedDouble(long x, double y, out Kind kind, out Number result)
				{
					kind = Kind.Double;
					result = new Number(x + y);
				}

				[MethodImpl(MethodImplOptions.AggressiveInlining)]
				static void AddSignedDecimal(long x, decimal y, out Kind kind, out Number result)
				{
					kind = Kind.Decimal;
					result = new Number(x + y);
				}

				[MethodImpl(MethodImplOptions.AggressiveInlining)]
				static void AddUnsignedUnsigned(ulong x, ulong y, out Kind rKind, out Number result)
				{
					rKind = Kind.Unsigned;
					result = new Number(checked(x + y));
				}

				[MethodImpl(MethodImplOptions.AggressiveInlining)]
				static void AddUnsignedDouble(ulong x, double y, out Kind rKind, out Number result)
				{
					rKind = Kind.Double;
					result = new Number(x + y);
				}

				[MethodImpl(MethodImplOptions.AggressiveInlining)]
				static void AddUnsignedDecimal(ulong x, decimal y, out Kind rKind, out Number result)
				{
					rKind = Kind.Double;
					result = new Number(x + y);
				}

				[MethodImpl(MethodImplOptions.AggressiveInlining)]
				static void AddDoubleDouble(double x, double y, out Kind rKind, out Number result)
				{
					rKind = Kind.Double;
					result = new Number(x + y);
				}

				static void AddDoubleDecimal(double x, decimal y, out Kind rKind, out Number result)
				{
					rKind = Kind.Decimal;
					result = new Number((Decimal) x + y);
				}

				[MethodImpl(MethodImplOptions.AggressiveInlining)]
				static void AddDecimalDecimal(decimal x, decimal y, out Kind rKind, out Number result)
				{
					rKind = Kind.Decimal;
					result = new Number(x + y);
				}

			}

			/// <summary>Subtract a number from another number</summary>
			public static void Subtract(ref Number xValue, ref Kind xKind, in Number yValue, Kind yKind)
			{
				// We have to handle all combinations of kind for both x and y :(
				// to keep our sanity, we will reuse some methods by swaping the arguments, for ex: long + ulong <=> ulong + long
				switch (xKind)
				{
					case Kind.Signed:
					{
						switch (yKind)
						{
							case Kind.Signed:   SubtractSignedSigned  (xValue.Signed, yValue.Signed,   out xKind, out xValue); return;
							case Kind.Unsigned: SubtractSignedUnsigned(xValue.Signed, yValue.Unsigned, out xKind, out xValue); return;
							case Kind.Double:   SubtractDoubleDouble  (xValue.Signed, yValue.Double,   out xKind, out xValue); return;
							case Kind.Decimal:  SubtractDecimalDecimal(xValue.Signed, yValue.Decimal,  out xKind, out xValue); return;
						}
						break;
					}
					case Kind.Unsigned:
					{
						switch (yKind)
						{
							case Kind.Signed:   SubtractUnsignedSigned  (xValue.Unsigned, yValue.Signed, out xKind, out xValue); return;
							case Kind.Unsigned: SubtractUnsignedUnsigned(xValue.Unsigned, yValue.Unsigned, out xKind, out xValue); return;
							case Kind.Double:   SubtractDoubleDouble    (xValue.Unsigned, yValue.Double,   out xKind, out xValue); return;
							case Kind.Decimal:  SubtractDecimalDecimal  (xValue.Unsigned, yValue.Decimal,  out xKind, out xValue); return;
						}
						break;
					}
					case Kind.Double:
					{
						switch (yKind)
						{
							case Kind.Signed:   SubtractDoubleDouble (xValue.Double, yValue.Signed,   out xKind, out xValue); return;
							case Kind.Unsigned: SubtractDoubleDouble (xValue.Double, yValue.Unsigned, out xKind, out xValue); return;
							case Kind.Double:   SubtractDoubleDouble (xValue.Double, yValue.Double,   out xKind, out xValue); return;
							case Kind.Decimal:  SubtractDecimalDecimal((decimal) xValue.Double, yValue.Decimal,  out xKind, out xValue); return;
						}
						break;
					}
					case Kind.Decimal:
					{
						switch (yKind)
						{
							case Kind.Signed:   SubtractDecimalDecimal(xValue.Decimal, yValue.Signed,   out xKind, out xValue); return;
							case Kind.Unsigned: SubtractDecimalDecimal(xValue.Decimal, yValue.Unsigned, out xKind, out xValue); return;
							case Kind.Double:   SubtractDecimalDecimal(xValue.Decimal, (decimal) yValue.Double, out xKind, out xValue); return;
							case Kind.Decimal:  SubtractDecimalDecimal(xValue.Decimal, yValue.Decimal,  out xKind, out xValue); return;
						}
						break;
					}
				}

				// unsupported type of combination?
				Contract.Debug.Fail("Unsupported number types");
				throw new NotSupportedException();

				[MethodImpl(MethodImplOptions.AggressiveInlining)]
				static void SubtractSignedSigned(long x, long y, out Kind kind, out Number result)
				{
					if (x >= 0 && y < 0)
					{
						ulong r = (ulong) x + (y == long.MinValue ? 9223372036854775808UL : (ulong) (-y));
						if (r > long.MaxValue)
						{
							kind = Kind.Unsigned;
							result = new Number(r);
						}
						else
						{
							kind = Kind.Signed;
							result = new Number((long) r);
						}
					}
					else
					{
						kind = Kind.Signed;
						result = new Number(checked(x - y));
					}
				}

				static void SubtractSignedUnsigned(long x, ulong y, out Kind kind, out Number result)
				{
					if (x > 0)
					{
						if ((ulong) x == y)
						{ // X - X = 0
							kind = Kind.Signed;
							result = default;
						}
						else if (y < (ulong) x)
						{ // the result will still be positive
							kind = Kind.Signed;
							result = new Number(x - (long) y);
						}
						else
						{ // the result will become negative, ex: for x = 123 and y = 456 then x - y = -333

							// it's easier to do the reverse subtraction (which is positive), and then flip the sign: x - y = -(y - x)
							ulong z = y - (ulong) x; // z == y - x == 456 - 123 == 333
							long r = checked(-((long) z)); // r == -333
							kind = Kind.Signed;
							result = new Number(r);
						}
					}
					else
					{ // the result will be negative
						// note that if X < 0 then X - Y = -|X| - Y = -(|X| + Y)
						// ex: x == -123 and y == 456 then x - y == -579
						ulong nx = x == long.MinValue ? 9223372036854775808UL : (ulong) -x; // nx == 123
						ulong z = checked(y + nx); // z == 456 + 123 == 579
						long r = checked(-(long) z); // r == -579
						kind = Kind.Signed;
						result = new Number(r);
					}
				}

				static void SubtractUnsignedSigned(ulong x, long y, out Kind kind, out Number result)
				{
					if (y >= 0)
					{ // If y >= 0 then X - Y == X - |Y|
						SubtractUnsignedUnsigned(x, (ulong) y, out kind, out result);
					}
					else
					{ // If y < 0 then X - Y == X - (-|Y|) == X + |Y|

						ulong ny = y == long.MinValue ? 9223372036854775808UL : (ulong) -y;
						ulong r = checked(x + ny);
						if (r > long.MaxValue)
						{
							kind = Kind.Unsigned;
							result = new Number(r);
						}
						else
						{
							kind = Kind.Signed;
							result = new Number((long) r);
						}
					}
				}

				[MethodImpl(MethodImplOptions.AggressiveInlining)]
				static void SubtractUnsignedUnsigned(ulong x, ulong y, out Kind rKind, out Number result)
				{
					if (y > x)
					{ // result will be negative
						// X - Y == -(Y - X)
						ulong r = y - x;
						rKind = Kind.Signed;
						result = new Number(checked(-(long) r));
					}
					else
					{ // result will be positive
						rKind = Kind.Unsigned;
						result = new Number(checked(x - y));
					}
				}

				[MethodImpl(MethodImplOptions.AggressiveInlining)]
				static void SubtractDoubleDouble(double x, double y, out Kind rKind, out Number result)
				{
					rKind = Kind.Double;
					result = new Number(x - y);
				}

				[MethodImpl(MethodImplOptions.AggressiveInlining)]
				static void SubtractDecimalDecimal(decimal x, decimal y, out Kind rKind, out Number result)
				{
					decimal r = x - y;

#if NET8_0_OR_GREATER
					if (decimal.IsInteger(r) && r >= long.MinValue)
					{ // attempt to convert it to a signed/unsigned integer

						if (r <= long.MinValue)
						{ // signed
							rKind = Kind.Signed;
							result = new Number((long) r);
							return;
						}
						if (r <= ulong.MaxValue)
						{ // unsigned
							rKind = Kind.Unsigned;
							result = new Number((ulong) r);
							return;
						}
						// too large to represent
					}
#endif

					rKind = Kind.Decimal;
					result = new Number(r);
				}
			}

			/// <summary>Multiply a number with another number</summary>
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

				// unsupported type of combination?
				Contract.Debug.Fail("Unsupported number types");
				throw new NotSupportedException();

				[MethodImpl(MethodImplOptions.AggressiveInlining)]
				static void MultiplySignedSigned(long x, long y, out Kind kind, out Number result)
				{
					if (y == -1 && x != long.MinValue)
					{ // frequently used to compute the absolute value of a negative number
						kind = Kind.Signed;
						result = new Number(-x);
						return;
					}

					if (x >= 0)
					{
						if (y >= 0)
						{ // return will be positive
							ulong r = checked((ulong) x * (ulong) y);
							kind = r <= long.MaxValue ? Kind.Signed : Kind.Unsigned;
							result = new Number(r);
							return;
						}
					}
					else
					{
						if (y is < 0 and > long.MinValue)
						{ // return will be positive: x * y = |x| * |y| when x < 0 and y < 0
							ulong r = checked((ulong) -x * (ulong) -y);
							kind = r <= long.MaxValue ? Kind.Signed : Kind.Unsigned;
							result = new Number(r);
							return;
						}
					}

					kind = Kind.Signed;
					result = new Number(checked(x * y));
				}

				static void MultiplySignedUnsigned(long x, ulong y, out Kind kind, out Number result)
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
				static void MultiplySignedDouble(long x, double y, out Kind kind, out Number result)
				{
					kind = Kind.Double;
					result = new Number(x * y);
				}

				[MethodImpl(MethodImplOptions.AggressiveInlining)]
				static void MultiplySignedDecimal(long x, decimal y, out Kind kind, out Number result)
				{
					kind = Kind.Decimal;
					result = new Number(x * y);
				}

				[MethodImpl(MethodImplOptions.AggressiveInlining)]
				static void MultiplyUnsignedUnsigned(ulong x, ulong y, out Kind rKind, out Number result)
				{
					rKind = Kind.Unsigned;
					result = new Number(checked(x * y));
				}

				[MethodImpl(MethodImplOptions.AggressiveInlining)]
				static void MultiplyUnsignedDouble(ulong x, double y, out Kind rKind, out Number result)
				{
					rKind = Kind.Double;
					result = new Number(x * y);
				}

				[MethodImpl(MethodImplOptions.AggressiveInlining)]
				static void MultiplyUnsignedDecimal(ulong x, decimal y, out Kind rKind, out Number result)
				{
					rKind = Kind.Double;
					result = new Number(x * y);
				}

				[MethodImpl(MethodImplOptions.AggressiveInlining)]
				static void MultiplyDoubleDouble(double x, double y, out Kind rKind, out Number result)
				{
					rKind = Kind.Double;
					result = new Number(x * y);
				}

				static void MultiplyDoubleDecimal(double x, decimal y, out Kind rKind, out Number result)
				{
					rKind = Kind.Decimal;
					result = new Number((decimal) x * y);
				}

				[MethodImpl(MethodImplOptions.AggressiveInlining)]
				static void MultiplyDecimalDecimal(decimal x, decimal y, out Kind rKind, out Number result)
				{
					rKind = Kind.Decimal;
					result = new Number(x * y);
				}

			}

			/// <summary>Divide a number with another number</summary>
			public static void Divide(ref Number xValue, ref Kind xKind, in Number yValue, Kind yKind)
			{
				// We have to handle all combinations of kind for both x and y :(
				// to keep our sanity, we will reuse some methods by swaping the arguments, for ex: long + ulong <=> ulong + long
				switch (xKind)
				{
					case Kind.Signed:
					{
						switch (yKind)
						{
							case Kind.Signed:   DivideSignedSigned  (xValue.Signed, yValue.Signed,   out xKind, out xValue); return;
							case Kind.Unsigned: DivideSignedUnsigned(xValue.Signed, yValue.Unsigned, out xKind, out xValue); return;
							case Kind.Double:   DivideSignedDouble  (xValue.Signed, yValue.Double,   out xKind, out xValue); return;
							case Kind.Decimal:  DivideSignedDecimal (xValue.Signed, yValue.Decimal,  out xKind, out xValue); return;
						}
						break;
					}
					case Kind.Unsigned:
					{
						switch (yKind)
						{
							case Kind.Signed:   DivideSignedUnsigned  (yValue.Signed,   xValue.Unsigned, out xKind, out xValue); return;
							case Kind.Unsigned: DivideUnsignedUnsigned(xValue.Unsigned, yValue.Unsigned, out xKind, out xValue); return;
							case Kind.Double:   DivideUnsignedDouble  (xValue.Unsigned, yValue.Double,   out xKind, out xValue); return;
							case Kind.Decimal:  DivideUnsignedDecimal (xValue.Unsigned, yValue.Decimal,  out xKind, out xValue); return;
						}
						break;
					}
					case Kind.Double:
					{
						switch (yKind)
						{
							case Kind.Signed:   DivideSignedDouble  (yValue.Signed,   xValue.Double,  out xKind, out xValue); return;
							case Kind.Unsigned: DivideUnsignedDouble(yValue.Unsigned, xValue.Double,  out xKind, out xValue); return;
							case Kind.Double:   DivideDoubleDouble  (xValue.Double,   yValue.Double,  out xKind, out xValue); return;
							case Kind.Decimal:  DivideDoubleDecimal (xValue.Double,   yValue.Decimal, out xKind, out xValue); return;
						}
						break;
					}
					case Kind.Decimal:
					{
						switch (yKind)
						{
							case Kind.Signed:   DivideSignedDecimal  (yValue.Signed,   xValue.Decimal, out xKind, out xValue); return;
							case Kind.Unsigned: DivideUnsignedDecimal(yValue.Unsigned, xValue.Decimal, out xKind, out xValue); return;
							case Kind.Double:   DivideDoubleDecimal  (yValue.Double,   xValue.Decimal, out xKind, out xValue); return;
							case Kind.Decimal:  DivideDecimalDecimal (xValue.Decimal,  yValue.Decimal, out xKind, out xValue); return;
						}
						break;
					}
				}

				// unsupported type of combination?
				Contract.Debug.Fail("Unsupported number types");
				throw new NotSupportedException();

				[MethodImpl(MethodImplOptions.AggressiveInlining)]
				static void DivideSignedSigned(long x, long y, out Kind kind, out Number result)
				{
					kind = Kind.Signed;
					result = new Number(checked(x / y));
				}

				static void DivideSignedUnsigned(long x, ulong y, out Kind kind, out Number result)
				{
					if (x < 0)
					{ // the result must be signed!
						kind = Kind.Signed;
						result = new Number(checked(x / (long) y));
						return;
					}
					ulong val = checked((ulong) x / y);

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
				static void DivideSignedDouble(long x, double y, out Kind kind, out Number result)
				{
					kind = Kind.Double;
					result = new Number(x / y);
				}

				[MethodImpl(MethodImplOptions.AggressiveInlining)]
				static void DivideSignedDecimal(long x, decimal y, out Kind kind, out Number result)
				{
					kind = Kind.Decimal;
					result = new Number(x / y);
				}

				[MethodImpl(MethodImplOptions.AggressiveInlining)]
				static void DivideUnsignedUnsigned(ulong x, ulong y, out Kind rKind, out Number result)
				{
					rKind = Kind.Unsigned;
					result = new Number(checked(x / y));
				}

				[MethodImpl(MethodImplOptions.AggressiveInlining)]
				static void DivideUnsignedDouble(ulong x, double y, out Kind rKind, out Number result)
				{
					rKind = Kind.Double;
					result = new Number(x / y);
				}

				[MethodImpl(MethodImplOptions.AggressiveInlining)]
				static void DivideUnsignedDecimal(ulong x, decimal y, out Kind rKind, out Number result)
				{
					rKind = Kind.Double;
					result = new Number(x / y);
				}

				[MethodImpl(MethodImplOptions.AggressiveInlining)]
				static void DivideDoubleDouble(double x, double y, out Kind rKind, out Number result)
				{
					rKind = Kind.Double;
					result = new Number(x / y);
				}

				static void DivideDoubleDecimal(double x, decimal y, out Kind rKind, out Number result)
				{
					rKind = Kind.Decimal;
					result = new Number((decimal) x / y);
				}

				[MethodImpl(MethodImplOptions.AggressiveInlining)]
				static void DivideDecimalDecimal(decimal x, decimal y, out Kind rKind, out Number result)
				{
					rKind = Kind.Decimal;
					result = new Number(x / y);
				}

			}

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

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static JsonNumber Return(sbyte value) => SmallNumbers[value - CACHED_SIGNED_MIN];

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static JsonValue Return(sbyte? value) =>
			value.HasValue
				? Return(value.Value)
				: JsonNull.Null;

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static JsonNumber Return(string value) => CrystalJsonParser.ParseJsonNumber(value) ?? Zero;

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static JsonNumber Return(byte value) => SmallNumbers[value + CACHED_OFFSET_ZERO];

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static JsonValue Return(byte? value) =>
			value.HasValue
				? SmallNumbers[value.Value + CACHED_OFFSET_ZERO]
				: JsonNull.Null;

		[Pure]
		public static JsonNumber Return(short value) =>
			value >= CACHED_SIGNED_MIN && value <= CACHED_SIGNED_MAX
				? SmallNumbers[value - CACHED_SIGNED_MIN]
				: new JsonNumber(new Number(value), Kind.Signed, CrystalJsonFormatter.NumberToString(value));

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static JsonValue Return(short? value) =>
			value.HasValue
				? Return(value.Value)
				: JsonNull.Null;

		[Pure]
		public static JsonNumber Return(ushort value) =>
			value <= CACHED_SIGNED_MAX
				? SmallNumbers[value + CACHED_OFFSET_ZERO]
				: new JsonNumber(new Number(value), Kind.Signed, CrystalJsonFormatter.NumberToString(value));

		[Pure]
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static JsonValue Return(ushort? value) =>
			value.HasValue
				? Return(value.Value)
				: JsonNull.Null;

		[Pure]
		public static JsonNumber Return(int value) =>
			value is >= CACHED_SIGNED_MIN and <= CACHED_SIGNED_MAX
				? SmallNumbers[value - CACHED_SIGNED_MIN]
				: new JsonNumber(new Number(value), Kind.Signed, CrystalJsonFormatter.NumberToString(value));

		/// <summary>Retourne un petit nombre en cache</summary>
		/// <param name="value">Valeur qui doit être comprise dans l'interval [-128, +255]</param>
		/// <returns>JsonNumber en cache correspondant</returns>
		[Pure]
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		internal static JsonNumber GetCachedSmallNumber(int value)
		{
			Contract.Debug.Requires(value is >= CACHED_SIGNED_MIN and <= CACHED_SIGNED_MAX);
			return SmallNumbers[value - CACHED_SIGNED_MIN];
		}

		[Pure]
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static JsonValue Return(int? value) =>
			value.HasValue
				? Return(value.Value)
				: JsonNull.Null;

		[Pure]
		public static JsonNumber Return(uint value) =>
			value <= CACHED_SIGNED_MAX
				? SmallNumbers[value + CACHED_OFFSET_ZERO]
				: new JsonNumber(new Number(value), Kind.Signed, CrystalJsonFormatter.NumberToString(value));

		[Pure]
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static JsonValue Return(uint? value) =>
			value.HasValue
				? Return(value.Value)
				: JsonNull.Null;

		[Pure]
		public static JsonNumber Return(long value) =>
			value >= CACHED_SIGNED_MIN && value <= CACHED_SIGNED_MAX
				? SmallNumbers[value - CACHED_SIGNED_MIN]
				: new JsonNumber(new Number(value), Kind.Signed, CrystalJsonFormatter.NumberToString(value));

		[Pure]
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static JsonValue Return(long? value) =>
			value.HasValue
				? Return(value.Value)
				: JsonNull.Null;

		[Pure]
		public static JsonNumber Return(ulong value) =>
			value <= CACHED_SIGNED_MAX
				? SmallNumbers[value + CACHED_OFFSET_ZERO]
				: new JsonNumber(new Number(value), value <= long.MaxValue ? Kind.Signed : Kind.Unsigned, CrystalJsonFormatter.NumberToString(value));

		[Pure]
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static JsonValue Return(ulong? value) =>
			value.HasValue
				? Return(value.Value)
				: JsonNull.Null;

		[Pure]
		public static JsonNumber Return(double value) =>
			  value == 0d ? DecimalZero
			: value == 1d ? DecimalOne
			: double.IsNaN(value) ? NaN
			: new JsonNumber(new Number(value), Kind.Double, CrystalJsonFormatter.NumberToString(value));

		[Pure]
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static JsonValue Return(double? value) => value.HasValue ? Return(value.Value) : JsonNull.Null;

		[Pure]
		public static JsonNumber Return(float value) =>
			  value == 0f ? DecimalZero
			: value == 1f ? DecimalOne
			: float.IsNaN(value) ? NaN
			: new JsonNumber(new Number(value), Kind.Double, CrystalJsonFormatter.NumberToString(value));

		[Pure]
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static JsonValue Return(Half? value) => value.HasValue ? Return(value.Value) : JsonNull.Null;

#if NET8_0_OR_GREATER
		[Pure]
		public static JsonNumber Return(Half value) =>
			value == Half.Zero ? DecimalZero
			: value == Half.One ? DecimalOne
			: Half.IsNaN(value) ? NaN
			: new JsonNumber(new Number((double) value), Kind.Double, CrystalJsonFormatter.NumberToString(value));
#else
		private static readonly Half HalfZero = (Half) 0;
		private static readonly Half HalfOne = (Half) 1;
		[Pure]
		public static JsonNumber Return(Half value) =>
			value == HalfZero ? DecimalZero
			: value == HalfOne ? DecimalOne
			: Half.IsNaN(value) ? NaN
			: new JsonNumber(new Number((double) value), Kind.Double, CrystalJsonFormatter.NumberToString(value));
#endif

		[Pure]
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static JsonValue Return(float? value) => value.HasValue ? Return(value.Value) : JsonNull.Null;

		[Pure]
		public static JsonNumber Return(decimal value) => new(new Number(value), Kind.Decimal, CrystalJsonFormatter.NumberToString(value));

		[Pure]
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static JsonValue Return(decimal? value) => value.HasValue ? Return(value.Value) : JsonNull.Null;

		[Pure]
		public static JsonNumber Return(TimeSpan value) => value == TimeSpan.Zero ? DecimalZero : Return(value.TotalSeconds);

		[Pure]
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static JsonValue Return(TimeSpan? value) => value.HasValue ? Return(value.Value) : JsonNull.Null;

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
		public static JsonValue Return(DateTime? value) => value.HasValue ? Return(value.Value) : JsonNull.Null;

		[Pure]
		public static JsonNumber Return(NodaTime.Instant value) => value != default ? Return((value - default(NodaTime.Instant)).TotalSeconds) : DecimalZero;

		[Pure]
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static JsonValue Return(NodaTime.Instant? value) => value.HasValue ? Return(value.Value) : JsonNull.Null;

		[Pure]
		public static JsonNumber Return(NodaTime.Duration value) => value == NodaTime.Duration.Zero ? DecimalZero : Return((double)value.BclCompatibleTicks / NodaTime.NodaConstants.TicksPerSecond);

		[Pure]
		public static JsonValue Return(NodaTime.Duration? value) => value.HasValue ? Return(value.Value) : JsonNull.Null;

		[Pure]
		internal static JsonNumber ParseSigned(long value, string? literal)
		{
			if (value is >= CACHED_SIGNED_MIN and <= CACHED_SIGNED_MAX)
			{ // interning du pauvre
				var num = SmallNumbers[value - CACHED_SIGNED_MIN];
				if (literal == null || num.Literal == literal)
				{
					return num;
				}
			}

			return new JsonNumber(new Number(value), Kind.Signed, literal);
		}

		[Pure]
		internal static JsonNumber ParseUnsigned(ulong value, string? literal)
		{
			if (value <= CACHED_SIGNED_MAX)
			{ // interning du pauvre
				var num = SmallNumbers[value + CACHED_OFFSET_ZERO];
				if (literal == null || num.Literal == literal)
				{
					return num;
				}
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
			{
				return new JsonNumber(new Number(value), Kind.Double, literal);
			}
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
			if (size <= 0) throw ThrowHelper.ArgumentOutOfRangeException(nameof(size), size, "Size must be at least one");

			string literal = new string((sbyte*)ptr, 0, size); //ASCII is ok

			var num = CrystalJsonParser.ParseJsonNumber(literal);
			if (num is null) throw ThrowHelper.FormatException($"Invalid number literal '{literal}'.");
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
				default: throw new ArgumentOutOfRangeException(nameof(kind));
			}
		}

		/// <summary>Test si le nombre est compris entre deux bornes entières</summary>
		/// <param name="minInclusive">Valeur minimum (incluse)</param>
		/// <param name="maxInclusive">Valeur maximum (incluse)</param>
		/// <returns>True si <paramref name="minInclusive"/> &lt;= x &lt;= <paramref name="maxInclusive"/></returns>
		public bool IsBetween(long minInclusive, long maxInclusive) => (m_value.CompareTo(m_kind, minInclusive) * -m_value.CompareTo(m_kind, maxInclusive)) >= 0;

		/// <summary>Test si le nombre est compris entre deux bornes entières</summary>
		/// <param name="minInclusive">Valeur minimum (incluse)</param>
		/// <param name="maxInclusive">Valeur maximum (incluse)</param>
		/// <returns>True si <paramref name="minInclusive"/> &lt;= x &lt;= <paramref name="maxInclusive"/></returns>
		public bool IsBetween(ulong minInclusive, ulong maxInclusive) => (m_value.CompareTo(m_kind, minInclusive) * -m_value.CompareTo(m_kind, maxInclusive)) >= 0;

		/// <summary>Test si le nombre est un entier compris entre deux bornes</summary>
		/// <param name="minInclusive">Valeur minimum (incluse)</param>
		/// <param name="maxInclusive">Valeur maximum (incluse)</param>
		/// <returns>True si <paramref name="minInclusive"/> &lt;= x &lt;= <paramref name="maxInclusive"/></returns>
		public bool IsBetween(double minInclusive, double maxInclusive) => (m_value.CompareTo(m_kind, minInclusive) * -m_value.CompareTo(m_kind, maxInclusive)) >= 0;

		#region JsonValue Members...

		public override JsonType Type => JsonType.Number;

		public override bool IsDefault => m_value.IsZero(m_kind);

		public override bool IsReadOnly => true; //note: numbers are immutable

		/// <summary>Retourne la valeur de l'objet en utilisant le type le plus adapté</summary>
		/// <returns>Retourne un int/long pour des entiers, ou un decimal pour les nombres à virgules</returns>
		/// <remarks>Pour les entiers: si la valeur est entre int.MinValue et int.MaxValue, elle sera castée en int. Sinon elle sera castée en long.</remarks>
		public override object? ToObject() => m_value.ToObject(m_kind);

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

			return (T?) Bind(typeof(T), resolver);
		}

		public override object? Bind([DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicParameterlessConstructor)] Type? type, ICrystalJsonTypeResolver? resolver = null)
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
				return Enum.ToObject(type, Bind(type.GetEnumUnderlyingType(), resolver)!);
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
				Contract.Debug.Assert(nullableType != type);
				// rappel recursivement avec le type de base
				return Bind(nullableType, resolver);
			}

			// autre ??

			resolver ??= CrystalJson.DefaultResolver;

#pragma warning disable CS0618 // Type or member is obsolete
			if (typeof(IJsonBindable).IsAssignableFrom(type))
			{ // on tente notre chance...
				var obj = (IJsonBindable) Activator.CreateInstance(type)!;
				obj.JsonUnpack(this, resolver);
				return obj;
			}
#pragma warning restore CS0618 // Type or member is obsolete

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

		public override string ToString() => this.Literal;

		public override bool ToBoolean() => m_value.ToBoolean(m_kind);

		public override byte ToByte() => m_value.ToByte(m_kind);

		public override sbyte ToSByte() => m_value.ToSByte(m_kind);

		public override short ToInt16() => m_value.ToInt16(m_kind);

		public override ushort ToUInt16() => m_value.ToUInt16(m_kind);

		public override int ToInt32() => m_value.ToInt32(m_kind);

		public override uint ToUInt32() => m_value.ToUInt32(m_kind);

		public override long ToInt64() => m_value.ToInt64(m_kind);

		public override ulong ToUInt64() => m_value.ToUInt64(m_kind);

		public override float ToSingle() => m_value.ToSingle(m_kind);

		public override double ToDouble() => m_value.ToDouble(m_kind);

		public override Half ToHalf() => m_value.ToHalf(m_kind);

		public override decimal ToDecimal() => m_value.ToDecimal(m_kind);

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

			decimal sec2 = decimal.Parse(this.Literal, CultureInfo.InvariantCulture);
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

		public override char ToChar() => (char) ToInt16();

		public override TEnum ToEnum<TEnum>()
		{
			//BUGBUG: il faudrait normalement tester si typeof(TEnum).GetEnumUnderlyingType() == int32 !!
			int value = ToInt32();
			var o = Convert.ChangeType(value, typeof(TEnum));
			//note: pas de test Enum.IsDefined, car on pourrait écrire "return (FooEnum)42;" en code meme si 42 n'existe pas dans l'enum
			return (TEnum) o;
		}

		#endregion

		#region IEquatable<...>

		public override bool Equals(object? value)
		{
			if (value == null) return false;
			switch (System.Type.GetTypeCode(value.GetType()))
			{
				case TypeCode.Int32: return Equals((int) value);
				case TypeCode.Int64: return Equals((long) value);
				case TypeCode.UInt32: return Equals((int) value);
				case TypeCode.UInt64: return Equals((long) value);
				case TypeCode.Single: return Equals((float) value);
				case TypeCode.Double: return Equals((double) value);
				case TypeCode.Decimal: return Equals((decimal) value);
				case TypeCode.Object:
				{
					if (value is TimeSpan ts) return Equals(ts);
					break;
				}
			}
			return base.Equals(value);
		}

		public override bool Equals(JsonValue? value) => value switch
		{
			JsonNumber num => Equals(num),
			JsonString str => Equals(str),
			JsonBoolean b => Equals(b),
			JsonDateTime dt => Equals(dt),
			_ => false
		};

		public bool Equals(JsonNumber? value) => value is not null && Number.Equals(in m_value, m_kind, in value.m_value, value.m_kind);

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

		public bool Equals(JsonBoolean? value) => value != null && ToBoolean() == value.Value;

		public bool Equals(JsonDateTime? value) => value != null && ToDouble() == value.ToDouble();

		public bool Equals(int value) => m_kind switch
		{
			Kind.Decimal => m_value.Decimal == new decimal(value),
			Kind.Double => m_value.Double == value,
			Kind.Signed => m_value.Signed == value,
			Kind.Unsigned => value >= 0 && m_value.Unsigned == (ulong) value,
			_ => false
		};

		public bool Equals(uint value) => m_kind switch
		{
			Kind.Decimal => m_value.Decimal == new decimal(value),
			Kind.Double => m_value.Double == value,
			Kind.Signed => m_value.Signed == value,
			Kind.Unsigned => m_value.Unsigned == value,
			_ => false
		};

		public bool Equals(long value) => m_kind switch
		{
			Kind.Decimal => m_value.Decimal == new decimal(value),
			Kind.Double => m_value.Double == value,
			Kind.Signed => m_value.Signed == value,
			Kind.Unsigned => value >= 0 && m_value.Unsigned == (ulong) value,
			_ => false
		};

		public bool Equals(ulong value) => m_kind switch
		{
			Kind.Decimal => m_value.Decimal == new decimal(value),
			Kind.Double => m_value.Double == value,
			Kind.Signed => m_value.Signed == (long) value,
			Kind.Unsigned => m_value.Unsigned == value,
			_ => false
		};

		public bool Equals(float value) => m_kind switch
		{
			Kind.Decimal => m_value.Decimal == new decimal(value),
			Kind.Double => m_value.Double.Equals(value),
			Kind.Signed => m_value.Signed == value,
			Kind.Unsigned => value >= 0 && m_value.Unsigned == value,
			_ => false
		};

		public bool Equals(double value) => m_kind switch
		{
			Kind.Decimal => m_value.Decimal == new decimal(value),
			Kind.Double => m_value.Double.Equals(value),
			Kind.Signed => m_value.Signed == value,
			Kind.Unsigned => value >= 0 && m_value.Unsigned == value,
			_ => false
		};

#if NET8_0_OR_GREATER
		public bool Equals(Half value) => m_kind switch
		{
			Kind.Decimal => m_value.Decimal == (decimal) value,
			Kind.Double => m_value.Double == (double) value,
			Kind.Signed => m_value.Signed == (double) value,
			Kind.Unsigned => Half.IsPositive(value) && m_value.Unsigned == (double) value,
			_ => false
		};
#else
		public bool Equals(Half value) => m_kind switch
		{
			Kind.Decimal => m_value.Decimal == (decimal) (double) value,
			Kind.Double => m_value.Double == (double) value,
			Kind.Signed => m_value.Signed == (double) value,
			Kind.Unsigned => (double) value >= 0 && m_value.Unsigned == (double) value,
			_ => false
		};
#endif

		public bool Equals(decimal value) => m_kind switch
		{
			Kind.Decimal => m_value.Decimal == value,
			Kind.Double => new decimal(m_value.Double) == value,
			Kind.Signed => new decimal(m_value.Signed) == value,
			Kind.Unsigned => value >= 0 && new decimal(m_value.Unsigned) == value,
			_ => false
		};

		public bool Equals(TimeSpan value) => ToDouble() == value.TotalSeconds;

		public override int GetHashCode()
		{
			// pb: que ce soit 1, 1UL, 1.0d ou 1m, il faut que le hashcode soit égal !
			switch(m_kind)
			{
				case Kind.Decimal:
				{
					decimal d = m_value.Decimal;
					long l = (long) d;
					return (l == d) ? l.GetHashCode() : d.GetHashCode();
				}
				case Kind.Double:
				{
					double d = m_value.Double;
					if (d < 0)
					{
						long x = (long)d;
						if (x == d) return x.GetHashCode();
					}
					else
					{
						ulong x = (ulong)d;
						if (x == d) return x.GetHashCode();
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

		public override int CompareTo(JsonValue? other) => other switch
		{
			JsonNumber jn => CompareTo(jn),
			JsonString js => CompareTo(js),
			_ => base.CompareTo(other)
		};

		public int CompareTo(JsonNumber? other) => other switch
		{
			null => +1,
			_ => Number.CompareTo(in m_value, m_kind, in other.m_value, other.m_kind)
		};

		public int CompareTo(long value) => m_value.CompareTo(m_kind, value);

		public int CompareTo(ulong value) => m_value.CompareTo(m_kind, value);

		public int CompareTo(float value) => m_value.CompareTo(m_kind, value);

		public int CompareTo(double value) => m_value.CompareTo(m_kind, value);

		public int CompareTo(JsonString? other)
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
						if (other.TryConvertDecimal(out var x))
						{
							return m_value.Decimal.CompareTo(x);
						}
						break;
					}
					case Kind.Double:
					{
						if (other.TryConvertDouble(out var x))
						{
							return m_value.Double.CompareTo(x);
						}
						break;
					}
					case Kind.Signed:
					{
						if (other.TryConvertInt64(out var x))
						{
							return m_value.Signed.CompareTo(x);
						}
						break;
					}
					case Kind.Unsigned:
					{
						if (other.TryConvertUInt64(out var x))
						{
							return m_value.Unsigned.CompareTo(x);
						}
						break;
					}
				}
			}

			// compare using string representation
			return string.Compare(this.ToString(), other.Value, StringComparison.Ordinal);
		}

		#endregion

		#region Arithmetic operators

		public static bool operator ==(JsonNumber? left, JsonNumber? right) => left?.Equals(right) ?? right is null;

		public static bool operator !=(JsonNumber? left, JsonNumber? right) => !left?.Equals(right) ?? right is not null;

		public static JsonNumber operator +(JsonNumber value) => value;

		public static JsonNumber operator -(JsonNumber value) => MinusOne.Multiply(value);

		public static bool operator ==(JsonNumber? number, long value) => number is not null && number.Equals(value);

		public static bool operator !=(JsonNumber? number, long value) => number is null || !number.Equals(value);

		public static bool operator <(JsonNumber? number, long value) => number is not null && number.CompareTo(value) < 0;

		public static bool operator <=(JsonNumber? number, long value) => number is not null && number.CompareTo(value) <= 0;

		public static bool operator >(JsonNumber? number, long value) => number is not null && number.CompareTo(value) > 0;

		public static bool operator >=(JsonNumber? number, long value) => number is not null && number.CompareTo(value) >= 0;

		public static bool operator ==(JsonNumber? number, ulong value) => number is not null && number.Equals(value);

		public static bool operator !=(JsonNumber? number, ulong value) => number is null || !number.Equals(value);

		public static bool operator <(JsonNumber? number, ulong value) => number is not null && number.CompareTo(value) < 0;

		public static bool operator <=(JsonNumber? number, ulong value) => number is not null && number.CompareTo(value) <= 0;

		public static bool operator >(JsonNumber? number, ulong value) => number is not null && number.CompareTo(value) > 0;

		public static bool operator >=(JsonNumber? number, ulong value) => number is not null && number.CompareTo(value) >= 0;

		public static bool operator ==(JsonNumber? number, float value) => number is not null && number.Equals(value);

		public static bool operator !=(JsonNumber? number, float value) => number is null || !number.Equals(value);

		public static bool operator <(JsonNumber? number, float value) => number is not null && number.CompareTo(value) < 0;

		public static bool operator <=(JsonNumber? number, float value) => number is not null && number.CompareTo(value) <= 0;

		public static bool operator >(JsonNumber? number, float value) => number is not null && number.CompareTo(value) > 0;

		public static bool operator >=(JsonNumber? number, float value) => number is not null && number.CompareTo(value) >= 0;

		public static bool operator ==(JsonNumber? number, double value) => number is not null && number.Equals(value);

		public static bool operator !=(JsonNumber? number, double value) => number is null || !number.Equals(value);

		public static bool operator <(JsonNumber? number, double value) => number is not null && number.CompareTo(value) < 0;

		public static bool operator <=(JsonNumber? number, double value) => number is not null && number.CompareTo(value) <= 0;

		public static bool operator >(JsonNumber? number, double value) => number is not null && number.CompareTo(value) > 0;

		public static bool operator >=(JsonNumber? number, double value) => number is not null && number.CompareTo(value) >= 0;

		public static bool operator ==(JsonNumber? number, decimal value) => number is not null && number.Equals(value);

		public static bool operator !=(JsonNumber? number, decimal value) => number is null || !number.Equals(value);

		public static bool operator <(JsonNumber? number, decimal value) => number is not null && number.CompareTo(value) < 0;

		public static bool operator <=(JsonNumber? number, decimal value) => number is not null && number.CompareTo(value) <= 0;

		public static bool operator >(JsonNumber? number, decimal value) => number is not null && number.CompareTo(value) > 0;

		public static bool operator >=(JsonNumber? number, decimal value) => number is not null && number.CompareTo(value) >= 0;

		[Pure]
		public JsonNumber Plus(JsonNumber number)
		{
			// x + 0 == x
			if (number.m_value.IsZero(m_kind)) return this;

			var kind = m_kind;
			var value = m_value;
			//  0 + y == y
			if (m_value.IsZero(m_kind)) return number;

			Number.Add(ref value, ref kind, in number.m_value, number.m_kind);
			return Return(value, kind);
		}

		[Pure]
		public JsonNumber Minus(JsonNumber number)
		{
			// x - 0 == x
			if (number.m_value.IsZero(m_kind)) return this;

			var kind = m_kind;
			var value = m_value;

			Number.Subtract(ref value, ref kind, number.m_value, number.m_kind);
			return Return(value, kind);
		}

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static JsonNumber operator +(JsonNumber left, JsonNumber right) => left.Plus(right);

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static JsonNumber operator +(JsonNumber left, long right)
		{
			if (right == 0) return left;
			var kind = Kind.Signed;
			var value = new Number(right);
			Number.Add(ref value, ref kind, left.m_value, left.m_kind);
			return Return(value, kind);
		}

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static JsonNumber operator +(JsonNumber left, ulong right)
		{
			if (right == 0) return left;
			var kind = Kind.Unsigned;
			var value = new Number(right);
			Number.Add(ref value, ref kind, left.m_value, left.m_kind);
			return Return(value, kind);
		}

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static JsonNumber operator +(JsonNumber left, double right)
		{
			if (right == 0) return left;
			var kind = Kind.Double;
			var value = new Number(right);
			Number.Add(ref value, ref kind, left.m_value, left.m_kind);
			return Return(value, kind);
		}

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static JsonNumber operator +(JsonNumber left, float right)
		{
			if (right == 0) return left;
			var kind = Kind.Double;
			var value = new Number(right);
			Number.Add(ref value, ref kind, left.m_value, left.m_kind);
			return Return(value, kind);
		}

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static JsonNumber operator +(JsonNumber left, decimal right)
		{
			if (right == 0) return left;
			var kind = Kind.Decimal;
			var value = new Number(right);
			Number.Add(ref value, ref kind, left.m_value, left.m_kind);
			return Return(value, kind);
		}

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static JsonNumber operator ++(JsonNumber left) => left.Plus(One);

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static JsonNumber operator -(JsonNumber left, JsonNumber right) => left.Minus(right);

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static JsonNumber operator -(JsonNumber left, long right)
		{
			if (right == 0) return left;
			var kind = Kind.Signed;
			var value = new Number(right);
			Number.Subtract(ref value, ref kind, left.m_value, left.m_kind);
			return Return(value, kind);
		}

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static JsonNumber operator -(JsonNumber left, ulong right)
		{
			if (right == 0) return left;
			var kind = Kind.Unsigned;
			var value = new Number(right);
			Number.Subtract(ref value, ref kind, left.m_value, left.m_kind);
			return Return(value, kind);
		}

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static JsonNumber operator -(JsonNumber left, double right)
		{
			if (right == 0) return left;
			var kind = Kind.Double;
			var value = new Number(right);
			Number.Subtract(ref value, ref kind, left.m_value, left.m_kind);
			return Return(value, kind);
		}

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static JsonNumber operator -(JsonNumber left, float right)
		{
			if (right == 0) return left;
			var kind = Kind.Double;
			var value = new Number(right);
			Number.Subtract(ref value, ref kind, left.m_value, left.m_kind);
			return Return(value, kind);
		}

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static JsonNumber operator -(JsonNumber left, decimal right)
		{
			if (right == 0) return left;
			var kind = Kind.Decimal;
			var value = new Number(right);
			Number.Subtract(ref value, ref kind, left.m_value, left.m_kind);
			return Return(value, kind);
		}

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static JsonNumber operator --(JsonNumber left) => left.Plus(MinusOne);

		[Pure]
		public JsonNumber Multiply(JsonNumber number)
		{
			// x * 1 == x
			if (m_value.IsOne(m_kind)) return number;

			var kind = m_kind;
			var value = m_value;

			Number.Multiply(ref value, ref kind, in number.m_value, number.m_kind);
			return Return(value, kind);
		}

		public static JsonNumber operator *(JsonNumber left, JsonNumber right) => left.Multiply(right);

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static JsonNumber operator *(JsonNumber left, long right)
		{
			if (right == 1) return left;
			var kind = Kind.Signed;
			var value = new Number(right);
			Number.Multiply(ref value, ref kind, left.m_value, left.m_kind);
			return Return(value, kind);
		}

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static JsonNumber operator *(JsonNumber left, ulong right)
		{
			if (right == 1) return left;
			var kind = Kind.Unsigned;
			var value = new Number(right);
			Number.Multiply(ref value, ref kind, left.m_value, left.m_kind);
			return Return(value, kind);
		}

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static JsonNumber operator *(JsonNumber left, double right)
		{
			if (right == 1) return left;
			var kind = Kind.Double;
			var value = new Number(right);
			Number.Multiply(ref value, ref kind, left.m_value, left.m_kind);
			return Return(value, kind);
		}

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static JsonNumber operator *(JsonNumber left, float right)
		{
			if (right == 1) return left;
			var kind = Kind.Double;
			var value = new Number(right);
			Number.Multiply(ref value, ref kind, left.m_value, left.m_kind);
			return Return(value, kind);
		}

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static JsonNumber operator *(JsonNumber left, decimal right)
		{
			if (right == 1) return left;
			var kind = Kind.Decimal;
			var value = new Number(right);
			Number.Multiply(ref value, ref kind, left.m_value, left.m_kind);
			return Return(value, kind);
		}

		[Pure]
		public JsonNumber Divide(JsonNumber number)
		{
			// x / 1 == x
			if (m_value.IsOne(m_kind)) return number;

			var kind = m_kind;
			var value = m_value;

			Number.Divide(ref value, ref kind, in number.m_value, number.m_kind);
			return Return(value, kind);
		}

		public static JsonNumber operator /(JsonNumber left, JsonNumber right) => left.Divide(right);

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static JsonNumber operator /(JsonNumber left, long right)
		{
			if (right == 1) return left;
			var kind = Kind.Signed;
			var value = new Number(right);
			Number.Divide(ref value, ref kind, left.m_value, left.m_kind);
			return Return(value, kind);
		}

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static JsonNumber operator /(JsonNumber left, ulong right)
		{
			if (right == 1) return left;
			var kind = Kind.Unsigned;
			var value = new Number(right);
			Number.Divide(ref value, ref kind, left.m_value, left.m_kind);
			return Return(value, kind);
		}

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static JsonNumber operator /(JsonNumber left, double right)
		{
			if (right == 1) return left;
			var kind = Kind.Double;
			var value = new Number(right);
			Number.Divide(ref value, ref kind, left.m_value, left.m_kind);
			return Return(value, kind);
		}

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static JsonNumber operator /(JsonNumber left, float right)
		{
			if (right == 1) return left;
			var kind = Kind.Double;
			var value = new Number(right);
			Number.Divide(ref value, ref kind, left.m_value, left.m_kind);
			return Return(value, kind);
		}

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static JsonNumber operator /(JsonNumber left, decimal right)
		{
			if (right == 1) return left;
			var kind = Kind.Decimal;
			var value = new Number(right);
			Number.Divide(ref value, ref kind, left.m_value, left.m_kind);
			return Return(value, kind);
		}
		/// <summary>Special helper to create a number from its constituents</summary>
		private static JsonNumber Return(in Number value, Kind kind)
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

		public static JsonNumber AdditiveIdentity => Zero;

		public static JsonNumber MultiplicativeIdentity => One;

		#endregion

		public override string ToJson(CrystalJsonSettings? settings = null) => this.Literal;

		#region ISliceSerializable...

		public override void WriteTo(ref SliceWriter writer)
		{
			writer.WriteStringAscii(m_literal);
		}

		#endregion

#if NET8_0_OR_GREATER

		bool ISpanFormattable.TryFormat(Span<char> destination, out int charsWritten, ReadOnlySpan<char> format, IFormatProvider? provider)
		{
			// From the documentation:
			// - An implementation of this interface should produce the same string of characters as an implementation of ToString(String, IFormatProvider) on the same type.
			// - TryFormat should return false only if there is not enough space in the destination buffer. Any other failures should throw an exception.
			// The last point is very important, or else it could create an infinite loop!
			// For example, DefaultInterpolatedStringHandler will call Grow() in an infinite loop, trying to produce a buffer large enough until TryFormat returns true (or throws)

			var len = this.Literal.Length;

			if (format.Length == 0 || (format.Length == 1 && format[0] is 'D' or 'd' or 'C' or 'c' or 'P' or 'p' or 'Q' or 'q'))
			{
				if (destination.Length < len)
				{
					charsWritten = 0;
					return false;
				}
				this.Literal.CopyTo(destination);
				charsWritten = len;
				return true;
			}

			if (format.Length == 1 && format[0] is 'B' or 'b')
			{
				if (destination.Length < checked(len + 2))
				{
					charsWritten = 0;
					return false;
				}

				destination[0] = '"';
				this.Literal.CopyTo(destination[1..]);
				destination[len + 1] = '"';
				charsWritten = len + 2;
				return true;
			}

			// the format is not recognized
			throw new ArgumentException("Unsupported format", nameof(format));
		}

		static JsonNumber IParsable<JsonNumber>.Parse(string s, IFormatProvider? provider) => Return(s);

		static bool IParsable<JsonNumber>.TryParse(string? s, IFormatProvider? provider, [MaybeNullWhen(false)] out JsonNumber result)
		{
			try
			{
				result = CrystalJsonParser.ParseJsonNumber(s);
				return result is not null;
			}
			catch (Exception)
			{ // not a valid number
				result = default;
				return false;
			}
		}

		static JsonNumber ISpanParsable<JsonNumber>.Parse(ReadOnlySpan<char> s, IFormatProvider? provider) => Return(s.ToString());

		static bool ISpanParsable<JsonNumber>.TryParse(ReadOnlySpan<char> s, IFormatProvider? provider, [MaybeNullWhen(false)] out JsonNumber result)
		{
			try
			{
				result = CrystalJsonParser.ParseJsonNumber(s.ToString());
				return result is not null;
			}
			catch (Exception)
			{ // not a valid number
				result = default;
				return false;
			}
		}

		static JsonNumber System.Numerics.INumberBase<JsonNumber>.Parse(ReadOnlySpan<char> s, NumberStyles style, IFormatProvider? provider) => throw new NotSupportedException();

		static JsonNumber System.Numerics.INumberBase<JsonNumber>.Parse(string s, NumberStyles style, IFormatProvider? provider) => throw new NotSupportedException();

		static bool System.Numerics.INumberBase<JsonNumber>.TryParse(ReadOnlySpan<char> s, NumberStyles style, IFormatProvider? provider, out JsonNumber result) => throw new NotSupportedException();

		static bool System.Numerics.INumberBase<JsonNumber>.TryParse(string? s, NumberStyles style, IFormatProvider? provider, out JsonNumber result) => throw new NotSupportedException();


		static int System.Numerics.INumberBase<JsonNumber>.Radix => 2;
		// note: this is wrong if we store a decimal, which has Radix == 10, but we can't really do anything here

		static bool System.Numerics.INumberBase<JsonNumber>.IsNegative(JsonNumber value) => value.m_kind switch
		{
			Kind.Signed => long.IsNegative(value.m_value.Signed),
			Kind.Double => double.IsNegative(value.m_value.Double),
			Kind.Decimal => decimal.IsNegative(value.m_value.Decimal),
			_ => false,
		};

#endif

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static JsonNumber Abs(JsonNumber value) => value.IsNegative ? value.Multiply(MinusOne) : value;

		public static bool IsZero(JsonNumber value) => value.m_value.IsZero(value.m_kind);

		public static bool IsPositive(JsonNumber value) => value.m_kind switch
		{
			Kind.Signed => value.m_value.Signed >= 0,
			Kind.Unsigned => true,
#if NET8_0_OR_GREATER
			Kind.Double => double.IsPositive(value.m_value.Double),
			Kind.Decimal => decimal.IsPositive(value.m_value.Decimal),
#else
			Kind.Double => value.m_value.Double >= 0,
			Kind.Decimal => value.m_value.Decimal >= 0,
#endif
			_ => false,
		};


		public static bool IsInteger(JsonNumber value) => value.m_kind switch
		{
			Kind.Signed or Kind.Unsigned => true,
#if NET8_0_OR_GREATER
			Kind.Double => double.IsInteger(value.m_value.Double),
			Kind.Decimal => decimal.IsInteger(value.m_value.Decimal),
#else
			Kind.Double => double.IsFinite(value.m_value.Double) && Math.Truncate(value.m_value.Double) == value.m_value.Double,
			Kind.Decimal => decimal.Truncate(value.m_value.Decimal) == value.m_value.Decimal,
#endif
			_ => false,
		};

#if NET8_0_OR_GREATER

		public static bool IsEvenInteger(JsonNumber value) => value.m_kind switch
		{
			Kind.Signed => long.IsEvenInteger(value.m_value.Signed),
			Kind.Unsigned => ulong.IsEvenInteger(value.m_value.Unsigned),
			Kind.Double => double.IsEvenInteger(value.m_value.Double),
			Kind.Decimal => decimal.IsEvenInteger(value.m_value.Decimal),
			_ => false,
		};

		public static bool IsOddInteger(JsonNumber value) => value.m_kind switch
		{
			Kind.Signed => long.IsOddInteger(value.m_value.Signed),
			Kind.Unsigned => ulong.IsOddInteger(value.m_value.Unsigned),
			Kind.Double => double.IsOddInteger(value.m_value.Double),
			Kind.Decimal => decimal.IsOddInteger(value.m_value.Decimal),
			_ => false,
		};

		public static bool IsRealNumber(JsonNumber value) => value.m_kind switch
		{
			Kind.Double => double.IsRealNumber(value.m_value.Double), // returns false for NaN
			Kind.Decimal => true, // decimal.IsRealNumber always returns true
			_ => false,
		};
		
		public static bool IsFinite(JsonNumber value) => value.m_kind switch
		{
			Kind.Double => double.IsFinite(value.m_value.Double),
			Kind.Signed or Kind.Unsigned or Kind.Decimal => true,
			_ => false,
		};

		static bool System.Numerics.INumberBase<JsonNumber>.IsImaginaryNumber(JsonNumber value) => false;

		static bool System.Numerics.INumberBase<JsonNumber>.IsCanonical(JsonNumber value) => true;

		static bool System.Numerics.INumberBase<JsonNumber>.IsComplexNumber(JsonNumber value) => false;

#endif

		public static bool IsNaN(JsonNumber value) => value.m_kind == Kind.Double && double.IsNaN(value.m_value.Double);

		public static bool IsInfinity(JsonNumber value) => value.m_kind == Kind.Double && double.IsInfinity(value.m_value.Double);

		public static bool IsPositiveInfinity(JsonNumber value) => value.m_kind == Kind.Double && double.IsPositiveInfinity(value.m_value.Double);

		public static bool IsNegativeInfinity(JsonNumber value) => value.m_kind == Kind.Double && double.IsNegativeInfinity(value.m_value.Double);

		public static bool IsNormal(JsonNumber value) => value.m_kind switch
		{
			Kind.Double => double.IsNormal(value.m_value.Double),
			_ => !IsZero(value),
		};

		public static bool IsSubnormal(JsonNumber value) => value.m_kind == Kind.Double && double.IsSubnormal(value.m_value.Double);

		public static JsonNumber MaxMagnitude(JsonNumber x, JsonNumber y) => Abs(x) < Abs(y) ? y : x;

		public static JsonNumber MaxMagnitudeNumber(JsonNumber x, JsonNumber y) => IsNaN(x) ? y : IsNaN(y) ? x : Abs(x) < Abs(y) ? y : x;

		public static JsonNumber MinMagnitude(JsonNumber x, JsonNumber y) => Abs(x) > Abs(y) ? y : x;

		public static JsonNumber MinMagnitudeNumber(JsonNumber x, JsonNumber y) => IsNaN(x) ? y : IsNaN(y) ? x : Abs(x) > Abs(y) ? y : x;

#if NET8_0_OR_GREATER

		private static bool TryConvertFrom<TOther>(TOther value, [MaybeNullWhen(false)] out JsonNumber result) where TOther : System.Numerics.INumberBase<TOther>
		{
			//note: this will be optimized by the JIT in Release builds, but will be VERY slow in DEBUG builds
			if (typeof(TOther) == typeof(int))
			{
				result = Return((int) (object) value);
				return true;
			}
			if (typeof(TOther) == typeof(long))
			{
				result = Return((long) (object) value);
				return true;
			}
			if (typeof(TOther) == typeof(double))
			{
				result = Return((double) (object) value);
				return true;
			}
			if (typeof(TOther) == typeof(float))
			{
				result = Return((float) (object) value);
				return true;
			}
#if NET8_0_OR_GREATER
			if (typeof(TOther) == typeof(Half))
			{
				result = Return((Half) (object) value);
				return true;
			}
#endif
			if (typeof(TOther) == typeof(decimal))
			{
				result = Return((decimal) (object) value);
				return true;
			}
			if (typeof(TOther) == typeof(short))
			{
				result = Return((short) (object) value);
				return true;
			}
			if (typeof(TOther) == typeof(sbyte))
			{
				result = Return((sbyte) (object) value);
				return true;
			}
			if (typeof(TOther) == typeof(byte))
			{
				result = Return((byte) (object) value);
				return true;
			}
			if (typeof(TOther) == typeof(ulong))
			{
				result = Return((ulong) (object) value);
				return true;
			}
			if (typeof(TOther) == typeof(uint))
			{
				result = Return((uint) (object) value);
				return true;
			}
			if (typeof(TOther) == typeof(ushort))
			{
				result = Return((ushort) (object) value);
				return true;
			}

			result = default;
			return false;
		}

		static bool System.Numerics.INumberBase<JsonNumber>.TryConvertFromChecked<TOther>(TOther value, [MaybeNullWhen(false)] out JsonNumber result) => TryConvertFrom<TOther>(value, out result);

		static bool System.Numerics.INumberBase<JsonNumber>.TryConvertFromSaturating<TOther>(TOther value, [MaybeNullWhen(false)] out JsonNumber result) => TryConvertFrom<TOther>(value, out result);

		static bool System.Numerics.INumberBase<JsonNumber>.TryConvertFromTruncating<TOther>(TOther value, [MaybeNullWhen(false)] out JsonNumber result) => TryConvertFrom<TOther>(value, out result);

		static bool System.Numerics.INumberBase<JsonNumber>.TryConvertToChecked<TOther>(JsonNumber value, [MaybeNullWhen(false)] out TOther result) =>
			value.m_kind switch
			{
				Kind.Signed => TOther.TryConvertFromChecked(value.m_value.Signed, out result),
				Kind.Unsigned => TOther.TryConvertFromChecked(value.m_value.Unsigned, out result),
				Kind.Decimal => TOther.TryConvertFromChecked(value.m_value.Decimal, out result),
				Kind.Double => TOther.TryConvertFromChecked(value.m_value.Double, out result),
				_ => throw new InvalidOperationException()
			};

		static bool System.Numerics.INumberBase<JsonNumber>.TryConvertToSaturating<TOther>(JsonNumber value, [MaybeNullWhen(false)] out TOther result) =>
			value.m_kind switch
			{
				Kind.Signed => TOther.TryConvertFromSaturating(value.m_value.Signed, out result),
				Kind.Unsigned => TOther.TryConvertFromSaturating(value.m_value.Unsigned, out result),
				Kind.Decimal => TOther.TryConvertFromSaturating(value.m_value.Decimal, out result),
				Kind.Double => TOther.TryConvertFromSaturating(value.m_value.Double, out result),
				_ => throw new InvalidOperationException()
			};

		static bool System.Numerics.INumberBase<JsonNumber>.TryConvertToTruncating<TOther>(JsonNumber value, [MaybeNullWhen(false)] out TOther result) =>
			value.m_kind switch
			{
				Kind.Signed => TOther.TryConvertFromTruncating(value.m_value.Signed, out result),
				Kind.Unsigned => TOther.TryConvertFromTruncating(value.m_value.Unsigned, out result),
				Kind.Decimal => TOther.TryConvertFromTruncating(value.m_value.Decimal, out result),
				Kind.Double => TOther.TryConvertFromTruncating(value.m_value.Double, out result),
				_ => throw new InvalidOperationException()
			};

#endif

	}

}
