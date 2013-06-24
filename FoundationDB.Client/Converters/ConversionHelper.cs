#region BSD Licence
/* Copyright (c) 2013, Doxense SARL
All rights reserved.

Redistribution and use in source and binary forms, with or without
modification, are permitted provided that the following conditions are met:
	* Redistributions of source code must retain the above copyright
	  notice, this list of conditions and the following disclaimer.
	* Redistributions in binary form must reproduce the above copyright
	  notice, this list of conditions and the following disclaimer in the
	  documentation and/or other materials provided with the distribution.
	* Neither the name of Doxense nor the
	  names of its contributors may be used to endorse or promote products
	  derived from this software without specific prior written permission.

THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS "AS IS" AND
ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED
WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE
DISCLAIMED. IN NO EVENT SHALL <COPYRIGHT HOLDER> BE LIABLE FOR ANY
DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES
(INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES;
LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND
ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT
(INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS
SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
 */
#endregion

namespace FoundationDB.Client.Converters
{
	using FoundationDB.Client.Utils;
	using System;
	using System.Collections.Concurrent;
	using System.Collections.Generic;
	using System.Globalization;

	/// <summary>Helper classe used to compare object of "compatible" types</summary>
	internal static class ComparisonHelper
	{

		/// <summary>Pair of types that can be used as a key in a dictionary</summary>
		private struct TypePair
		{
			public readonly Type Left;
			public readonly Type Right;

			public TypePair(Type left, Type right)
			{
				this.Left = left;
				this.Right = right;
			}

			public override bool Equals(object obj)
			{
				if (obj == null) return false;
				TypePair other = (TypePair)obj;
				return this.Left == other.Left && this.Right == other.Right;
			}

			public override int GetHashCode()
			{
				// note: we cannot just xor both hash codes, because if left and right are the same, we will return 0
				int h = this.Left.GetHashCode();
				h = (h >> 13) | (h << 19);
				h ^= this.Right.GetHashCode();
				return h;
			}
		}

		/// <summary>Helper class to use TypePair as keys in a dictionnary</summary>
		private sealed class TypePairComparer : IEqualityComparer<TypePair>
		{
			public bool Equals(TypePair x, TypePair y)
			{
				return x.Left == y.Left && x.Right == y.Right;
			}

			public int GetHashCode(TypePair obj)
			{
				return obj.GetHashCode();
			}
		}

		/// <summary>Cache of all the comparison lambda for a pair of types</summary>
		/// <remarks>Contains lambda that can compare two objects (of different types) for "similarity"</remarks>
		private static readonly ConcurrentDictionary<TypePair, Func<object, object, bool>> EqualityComparers = new ConcurrentDictionary<TypePair, Func<object, object, bool>>(new TypePairComparer());

		/// <summary>Tries to convert an object into an equivalent string representation (for equality comparison)</summary>
		/// <param name="value">Object to adapt</param>
		/// <returns>String equivalent of the object</returns>
		public static string TryAdaptToString(object value, Type t)
		{
			if (value == null) return null;

			var s = value as string;
			if (s != null) return s;

			if (value is char) return new string((char)value, 1);

			if (value is Slice) return ((Slice) value).ToAscii();
			if (value is byte[]) return Slice.Create(value as byte[]).ToAscii();

			var fmt = value as IFormattable;
			if (fmt != null) return fmt.ToString(null, CultureInfo.InvariantCulture);

			return null;
		}

		/// <summary>Tries to convert an object into an equivalent double representation (for equality comparison)</summary>
		/// <param name="value">Object to adapt</param>
		/// <returns>Double equivalent of the object</returns>
		public static double? TryAdaptToDecimal(object value, Type t)
		{
			if (value != null)
			{
				switch (Type.GetTypeCode(t))
				{
					case TypeCode.Int16: return (double?)(short)value;
					case TypeCode.UInt16: return (double?)(ushort)value;
					case TypeCode.Int32: return (double?)(int)value;
					case TypeCode.UInt32: return (double?)(uint)value;
					case TypeCode.Int64: return (double?)(long)value;
					case TypeCode.UInt64: return (double?)(ulong)value;
					case TypeCode.Single: return (double?)(float)value;
					case TypeCode.Double: return (double)value;
				}
			}
			return null;
		}

		/// <summary>Tries to convert an object into an equivalent Int64 representation (for equality comparison)</summary>
		/// <param name="value">Object to adapt</param>
		/// <returns>Int64 equivalent of the object</returns>
		public static long? TryAdaptToInteger(object value, Type t)
		{
			if (value != null)
			{
				switch (Type.GetTypeCode(t))
				{
					case TypeCode.Int16: return (long?)(short)value;
					case TypeCode.UInt16: return (long?)(ushort)value;
					case TypeCode.Int32: return (long?)(int)value;
					case TypeCode.UInt32: return (long?)(uint)value;
					case TypeCode.Int64: return (long)value;
					case TypeCode.UInt64: return (long?)(ulong)value;
					case TypeCode.Single: return (long?)(float)value;
					case TypeCode.Double: return (long?)(double)value;
				}
			}
			return null;
		}

		private static Func<object, object, bool> CreateTypeComparator(Type t1, Type t2)
		{
			Contract.Requires(t1 != null && t2 != null);

			// note: the most common scenarios will be when we compare 'A' to "A", or (int)123 to (long)123, Guids in string or System.Guid form, ...
			// We should not try too hard to compare complex objects (what about dates ? timespans? Guids?)

			// first, handle the easy cases
			if (AreEquivalentTypes(t1, t2))
			{
				switch (Type.GetTypeCode(t1))
				{
					case TypeCode.Char: return (x, y) => (char)x == (char)y;
					case TypeCode.Byte: return (x, y) => (byte)x == (byte)y;
					case TypeCode.SByte: return (x, y) => (sbyte)x == (sbyte)y;
					case TypeCode.Int16: return (x, y) => (short)x == (short)y;
					case TypeCode.UInt16: return (x, y) => (ushort)x == (ushort)y;
					case TypeCode.Int32: return (x, y) => (int)x == (int)y;
					case TypeCode.UInt32: return (x, y) => (uint)x == (uint)y;
					case TypeCode.Int64: return (x, y) => (long)x == (long)y;
					case TypeCode.UInt64: return (x, y) => (ulong)x == (ulong)y;
					case TypeCode.Single: return (x, y) => (float)x == (float)y;
					case TypeCode.Double: return (x, y) => (double)x == (double)y;
					case TypeCode.String: return (x, y) => (string)x == (string)y;
				}

				return (x, y) =>
				{
					if (object.ReferenceEquals(x, null)) return object.ReferenceEquals(y, null);
					return object.ReferenceEquals(x, y) || x.Equals(y);
				};
			}

			if (IsStringType(t1) || IsStringType(t2))
			{
				return (x, y) =>
				{
					if (x == null) return y == null;
					if (y == null) return false;
					return object.ReferenceEquals(x, y) || (TryAdaptToString(x, t1) == TryAdaptToString(y, t2));
				};
			}

			if (IsNumericType(t1) || IsNumericType(t2))
			{
				if (IsDecimalType(t1) || IsDecimalType(t2))
				{
					return (x, y) =>
					{
						return x == null ? y == null : y != null && TryAdaptToDecimal(x, t1) == TryAdaptToDecimal(y, t2);
					};
				}

				//TODO: handle UInt64 with values > long.MaxValue that will overflow to negative values when casted down to Int64

				return (x, y) =>
				{
					return x == null ? y == null : y != null && TryAdaptToInteger(x, t1) == TryAdaptToInteger(y, t2);
				};
			}

			//TODO: some other way to compare ?
			return (x, y) => false;
		}

		internal static Func<object, object, bool> GetTypeComparator(Type t1, Type t2)
		{
			var pair = new TypePair(t1, t2);
			Func<object, object, bool> comparator;

			if (!EqualityComparers.TryGetValue(pair, out comparator))
			{
				comparator = CreateTypeComparator(t1, t2);
				EqualityComparers.TryAdd(pair, comparator);
			}
			return comparator;
		}

		/// <summary>Tries to compare any two object for "equality", where string "123" is considered equal to integer 123</summary>
		/// <param name="x">Left object to compare</param>
		/// <param name="y">Right object to compare</param>
		/// <returns>True if both objects are "similar" (ie: the represent the same logical value)</returns>
		/// <example>
		/// AreSimilar("123", 123) => true
		/// AreSimilar('A', "A") => true
		/// AreSimilar(false, 0) => true
		/// AreSimilar(true, 1) => true
		/// </example>
		internal static bool AreSimilar(object x, object y)
		{
			if (x == null) return y == null;
			if (y == null) return false;
			if (object.ReferenceEquals(x, y)) return true;

			var comparator = GetTypeComparator(x.GetType(), y.GetType());
			Contract.Requires(comparator != null);
			return comparator(x, y);
		}

		internal static bool AreSimilar<T1, T2>(T1 x, T2 y)
		{
			var comparator = GetTypeComparator(typeof(T1), typeof(T2));
			Contract.Requires(comparator != null);
			return comparator(x, y);
		}

		/// <summary>Returns true if both types are considered "the same"</summary>
		/// <returns>Returns true if t1 is equal to t2</returns>
		private static bool AreEquivalentTypes(Type t1, Type t2)
		{
			return t1 == t2 || t1.IsEquivalentTo(t2);
		}

		/// <summary>Return true if the type is considered to be a "string" (string, char)</summary>
		/// <returns>True for <see cref="System.String">string</see> and <see cref="System.Char">char</see>.</returns>
		public static bool IsStringType(Type t)
		{
			return t == typeof(string) || t == typeof(char);
		}

		/// <summary>Returns true if the specified type is considered to be a "number"</summary>
		/// <returns>True for integers (8, 16, 32 or 64 bits, signed or unsigned) and their Nullable versions. Bytes and Chars are not considered to be numbers because they a custom serialized</returns>
		private static bool IsNumericType(Type t)
		{
			// Return true for valuetypes that are considered numbers.
			// Notes:
			// - We do not consider 'byte' to be a number, because we will serialize it as a byte[1]
			// - We do not consider 'char' to be a number, because we will serialize it as a string
			// - For nullable types, we consider the underlying type, so "int?" is considered to be a number"

			switch (Type.GetTypeCode(t))
			{
				case TypeCode.SByte:
				case TypeCode.Int16:
				case TypeCode.UInt16:
				case TypeCode.Int32:
				case TypeCode.UInt32:
				case TypeCode.Int64:
				case TypeCode.UInt64:
				case TypeCode.Single:
				case TypeCode.Double:
					return true;

				case TypeCode.Object:
				{
					// Could be a Nullable<T>
					var nullableType = Nullable.GetUnderlyingType(t);
					return t != null && IsNumericType(nullableType);
				}
				default:
				{
					return false;
				}
			}
		}

		/// <summary>Returns true if the specified type is considered to be a "decimal"</summary>
		/// <returns>True for <see cref="System.Double">double</see> and <see cref="System.Single">float</see>.</returns>
		private static bool IsDecimalType(Type t)
		{
			return t == typeof(double) || t == typeof(float);
		}
	}

}
