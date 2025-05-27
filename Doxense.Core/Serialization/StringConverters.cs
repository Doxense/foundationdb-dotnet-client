#region Copyright (c) 2023-2025 SnowBank SAS, (c) 2005-2023 Doxense SAS
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

namespace SnowBank.Runtime.Converters
{
	using System.Globalization;
	using System.Numerics;
	using System.Runtime.InteropServices;
	using NodaTime;

	[PublicAPI]
	public static class StringConverters
	{

		#region Formatting...

		[DoesNotReturn, MethodImpl(MethodImplOptions.NoInlining)]
		internal static void ReportInternalFormattingError()
		{
#if DEBUG
			if (System.Diagnostics.Debugger.IsAttached) System.Diagnostics.Debugger.Break();
#endif
			throw ThrowHelper.InvalidOperationException("Internal formatting error");
		}

		#region 8-bits...

		/// <summary>Converts a 16-bit signed integer into a decimal string literal, using the Invariant culture</summary>
		/// <param name="value">Value to convert</param>
		/// <returns>Corresponding string literal</returns>
		[Pure]
		public static string ToString(sbyte value)
		{
			//perf: as of .NET 8, Int32.ToString(null) calls Number.UInt32ToDecStr(...) which already manage a cache for small numbers (less than 300)
			if (value >= 0)
			{
				return value.ToString(default(IFormatProvider));
			}
			// for negative numbers, we have to pass an invariant culture, otherwise it will use the NegativeSign of the current culture
			return value.ToString(NumberFormatInfo.InvariantInfo);
		}

		/// <summary>Writes the text representation of a 16-bit signed integer, using the Invariant culture</summary>
		/// <param name="output">Destination</param>
		/// <param name="value">Value to write</param>
		public static void WriteTo(TextWriter output, sbyte value)
		{
#if NET9_0_OR_GREATER
			if (value >= 0)
			{
				// small integers from 0 to 299 are cached by the runtime, so there will be no string allocation
				output.Write(value.ToString(default(IFormatProvider)));
				return;
			}
#endif

			Span<char> buf = stackalloc char[Base10MaxCapacityInt8];

			bool success = value.TryFormat(buf, out int written, default, NumberFormatInfo.InvariantInfo); // will be inlined as Number.TryNegativeInt32ToDecStr
			if (!success) ReportInternalFormattingError();

			output.Write(buf.Slice(0, written));
		}

		/// <summary>Converts a 16-bit unsigned integer into a decimal string literal, using the Invariant culture</summary>
		/// <param name="value">Value to convert</param>
		/// <returns>Corresponding string literal</returns>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static string ToString(byte value)
		{
			//perf: as of .NET 8, UInt32.ToString(null) calls Number.UInt32ToDecStr(...) which already manage a cache for small numbers (less than 300)
			return value.ToString(default(IFormatProvider));
		}

		/// <summary>Writes the text representation of a 16-bit unsigned integer, using the Invariant culture</summary>
		/// <param name="output">Destination</param>
		/// <param name="value">Value to write</param>
		public static void WriteTo(TextWriter output, byte value)
		{
#if NET9_0_OR_GREATER
			// small integers from 0 to 299 are cached by the runtime, so there will be no string allocation
			output.Write(value.ToString(default(IFormatProvider)));
#else

			Span<char> buf = stackalloc char[Base10MaxCapacityUInt8];

			bool success = value.TryFormat(buf, out int written); // will be inlined as Number.TryUInt32ToDecStr
			if (!success) ReportInternalFormattingError();

			output.Write(buf.Slice(0, written));
#endif
		}

		#endregion

		#region 16-bits...

		/// <summary>Converts a 16-bit signed integer into a decimal string literal, using the Invariant culture</summary>
		/// <param name="value">Value to convert</param>
		/// <returns>Corresponding string literal</returns>
		[Pure]
		public static string ToString(short value)
		{
			//perf: as of .NET 8, Int32.ToString(null) calls Number.UInt32ToDecStr(...) which already manage a cache for small numbers (less than 300)
			if (value >= 0)
			{
				return value.ToString(default(IFormatProvider));
			}
			// for negative numbers, we have to pass an invariant culture, otherwise it will use the NegativeSign of the current culture
			return value.ToString(NumberFormatInfo.InvariantInfo);
		}

		/// <summary>Writes the text representation of a 16-bit signed integer, using the Invariant culture</summary>
		/// <param name="output">Destination</param>
		/// <param name="value">Value to write</param>
		public static void WriteTo(TextWriter output, short value)
		{
#if NET9_0_OR_GREATER
			if ((uint) value < 300U)
			{
				// small integers from 0 to 299 are cached by the runtime, so there will be no string allocation
				output.Write(value.ToString(default(IFormatProvider)));
				return;
			}
#endif

			Span<char> buf = stackalloc char[Base10MaxCapacityInt16];

			bool success = value >= 0
				? value.TryFormat(buf, out var written) // will be inlined as Number.TryUInt32ToDecStr
				: value.TryFormat(buf, out written, default, NumberFormatInfo.InvariantInfo); // will be inlined as Number.TryNegativeInt32ToDecStr
			if (!success) ReportInternalFormattingError();

			output.Write(buf.Slice(0, written));
		}

		/// <summary>Converts a 16-bit unsigned integer into a decimal string literal, using the Invariant culture</summary>
		/// <param name="value">Value to convert</param>
		/// <returns>Corresponding string literal</returns>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static string ToString(ushort value)
		{
			//perf: as of .NET 8, UInt32.ToString(null) calls Number.UInt32ToDecStr(...) which already manage a cache for small numbers (less than 300)
			return value.ToString(default(IFormatProvider));
		}

		/// <summary>Writes the text representation of a 16-bit unsigned integer, using the Invariant culture</summary>
		/// <param name="output">Destination</param>
		/// <param name="value">Value to write</param>
		public static void WriteTo(TextWriter output, ushort value)
		{
#if NET9_0_OR_GREATER
			if (value < 300U)
			{
				// small integers from 0 to 299 are cached by the runtime, so there will be no string allocation
				output.Write(value.ToString(default(IFormatProvider)));
				return;
			}
#endif

			Span<char> buf = stackalloc char[Base10MaxCapacityUInt16];

			bool success = value.TryFormat(buf, out int written); // will be inlined as Number.TryUInt32ToDecStr
			if (!success) ReportInternalFormattingError();

			output.Write(buf.Slice(0, written));
		}

		#endregion

		#region 32-bits...

		/// <summary>Converts a 32-bit signed integer into a decimal string literal, using the Invariant culture</summary>
		/// <param name="value">Value to convert</param>
		/// <returns>Corresponding string literal</returns>
		[Pure]
		public static string ToString(int value)
		{
			//perf: as of .NET 8, Int32.ToString(null) calls Number.UInt32ToDecStr(...) which already manage a cache for small numbers (less than 300)
			if (value >= 0)
			{
				return value.ToString(default(IFormatProvider));
			}
			// for negative numbers, we have to pass an invariant culture, otherwise it will use the NegativeSign of the current culture
			return value.ToString(NumberFormatInfo.InvariantInfo);
		}

		/// <summary>Writes the text representation of a 32-bit signed integer, using the Invariant culture</summary>
		/// <param name="output">Destination</param>
		/// <param name="value">Value to write</param>
		public static void WriteTo(TextWriter output, int value)
		{
#if NET9_0_OR_GREATER
			if ((uint) value < 300U)
			{
				// small integers from 0 to 299 are cached by the runtime, so there will be no string allocation
				output.Write(value.ToString(default(IFormatProvider)));
				return;
			}
#endif

			Span<char> buf = stackalloc char[Base10MaxCapacityInt32];

			bool success = value >= 0
				? value.TryFormat(buf, out var written) // will be inlined as Number.TryUInt32ToDecStr
				: value.TryFormat(buf, out written, default, NumberFormatInfo.InvariantInfo); // will be inlined as Number.TryNegativeInt32ToDecStr
			if (!success) ReportInternalFormattingError();

			output.Write(buf.Slice(0, written));
		}

		/// <summary>Converts a 32-bit unsigned integer into a decimal string literal, using the Invariant culture</summary>
		/// <param name="value">Value to convert</param>
		/// <returns>Corresponding string literal</returns>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static string ToString(uint value)
		{
			//perf: as of .NET 8, UInt32.ToString(null) calls Number.UInt32ToDecStr(...) which already manage a cache for small numbers (less than 300)
			return value.ToString(default(IFormatProvider));
		}

		/// <summary>Writes the text representation of a 32-bit unsigned integer, using the Invariant culture</summary>
		/// <param name="output">Destination</param>
		/// <param name="value">Value to write</param>
		public static void WriteTo(TextWriter output, uint value)
		{
#if NET9_0_OR_GREATER
			if (value < 300U)
			{
				// small integers from 0 to 299 are cached by the runtime, so there will be no string allocation
				output.Write(value.ToString(default(IFormatProvider)));
				return;
			}
#endif

			Span<char> buf = stackalloc char[Base10MaxCapacityUInt32];

			bool success = value.TryFormat(buf, out int written); // will be inlined as Number.TryUInt32ToDecStr
			if (!success) ReportInternalFormattingError();

			output.Write(buf.Slice(0, written));
		}

		#endregion

		#region 64-bits...

		/// <summary>Converts a 64-bit signed integer into a decimal string literal, using the Invariant culture</summary>
		/// <param name="value">Value to convert</param>
		/// <returns>Corresponding string literal</returns>
		[Pure]
		public static string ToString(long value)
		{
			//perf: as of .NET 8, Int64.ToString(null) calls Number.UInt64ToDecStr(...) which already manage a cache for small numbers (less than 300)
			if (value >= 0)
			{
				return value.ToString(default(IFormatProvider));
			}

			// for negative numbers, we have to pass an invariant culture, otherwise it will use the NegativeSign of the current culture
			return value.ToString(NumberFormatInfo.InvariantInfo);
		}

		/// <summary>Writes the text representation of a 64-bit signed integer, using the Invariant culture</summary>
		/// <param name="output">Destination</param>
		/// <param name="value">Value to write</param>
		public static void WriteTo(TextWriter output, long value)
		{
#if NET9_0_OR_GREATER
			if ((ulong) value < 300UL)
			{
				// small integers from 0 to 299 are cached by the runtime, so there will be no string allocation
				output.Write(value.ToString(default(IFormatProvider)));
				return;
			}
#endif

			Span<char> buf = stackalloc char[Base10MaxCapacityInt64];

			bool success = value >= 0
				? value.TryFormat(buf, out var written) // will be inlined as Number.TryUInt64ToDecStr
				: value.TryFormat(buf, out written, default, NumberFormatInfo.InvariantInfo); // will be inlined as Number.TryNegativeInt64ToDecStr
			if (!success) ReportInternalFormattingError();

			output.Write(buf.Slice(0, written));
		}

		/// <summary>Converts a 64-bit unsigned integer into a decimal string literal, using the Invariant culture</summary>
		/// <param name="value">Value to convert</param>
		/// <returns>Corresponding string literal</returns>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static string ToString(ulong value)
		{
			//perf: as of .NET 8, UInt64.ToString(null) calls Number.UInt64ToDecStr(...) which already manage a cache for small numbers (less than 300)
			return value.ToString(default(IFormatProvider));
		}

		/// <summary>Writes the text representation of a 64-bit unsigned integer, using the Invariant culture</summary>
		/// <param name="output">Destination</param>
		/// <param name="value">Value to write</param>
		public static void WriteTo(TextWriter output, ulong value)
		{
#if NET9_0_OR_GREATER
			if (value < 300UL)
			{
				// small integers from 0 to 299 are cached by the runtime, so there will be no string allocation
				output.Write(value.ToString(default(IFormatProvider)));
				return;
			}
#endif

			Span<char> buf = stackalloc char[Base10MaxCapacityUInt64];

			bool success = value.TryFormat(buf, out int written); // will be inlined as Number.TryUInt64ToDecStr
			if (!success) ReportInternalFormattingError();

			output.Write(buf.Slice(0, written));
		}

		#endregion

		#region 128-bits...

#if NET8_0_OR_GREATER

		/// <summary>Converts a 64-bit signed integer into a decimal string literal, using the Invariant culture</summary>
		/// <param name="value">Value to convert</param>
		/// <returns>Corresponding string literal</returns>
		[Pure]
		public static string ToString(Int128 value)
		{
			//perf: for positive numbers, Int128.ToString(null) calls Number.UInt128ToDecStr(...) which already manage a cache for small numbers (less than 300)
			return value.ToString(NumberFormatInfo.InvariantInfo);
		}

		/// <summary>Writes the text representation of a 64-bit signed integer, using the Invariant culture</summary>
		/// <param name="output">Destination</param>
		/// <param name="value">Value to write</param>
		public static void WriteTo(TextWriter output, Int128 value)
		{
			Span<char> buf = stackalloc char[Base10MaxCapacityInt128];

			bool success = value >= 0
				? value.TryFormat(buf, out int written)
				: value.TryFormat(buf, out written, default, NumberFormatInfo.InvariantInfo);
			if (!success) ReportInternalFormattingError();
			
			output.Write(buf.Slice(0, written));
		}

		/// <summary>Converts a 64-bit unsigned integer into a decimal string literal, using the Invariant culture</summary>
		/// <param name="value">Value to convert</param>
		/// <returns>Corresponding string literal</returns>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static string ToString(UInt128 value)
		{
			//perf: UInt128.ToString(null) calls Number.UInt128ToDecStr(...) which already manage a cache for small numbers (less than 300)
			return value.ToString(default(IFormatProvider));
		}

		/// <summary>Writes the text representation of a 64-bit unsigned integer, using the Invariant culture</summary>
		/// <param name="output">Destination</param>
		/// <param name="value">Value to write</param>
		public static void WriteTo(TextWriter output, UInt128 value)
		{
			Span<char> buf = stackalloc char[Base10MaxCapacityUInt128];

			bool success = value.TryFormat(buf, out int written); // will be inlined as Number.TryUInt128ToDecStr
			if (!success) ReportInternalFormattingError();

			output.Write(buf.Slice(0, written));
		}

#endif

		#endregion

		#region Single...

		/// <summary>Converts a 32-bit IEEE floating point number into a decimal string literal, using the Invariant culture</summary>
		/// <param name="value">Value to convert</param>
		/// <returns>Corresponding string literal</returns>
		[Pure]
		public static string ToString(float value)
		{
			long x = unchecked((long) value);
			// ReSharper disable once CompareOfFloatsByEqualityOperator
			return x != value
				? value.ToString("R", NumberFormatInfo.InvariantInfo)
				: x >= 0 ? x.ToString(default(IFormatProvider))
				: x.ToString(NumberFormatInfo.InvariantInfo);
		}

		/// <summary>Writes the text representation of a 32-bit IEEE floating point number, using the Invariant culture</summary>
		/// <param name="output">Destination</param>
		/// <param name="value">Value to write</param>
		public static void WriteTo(TextWriter output, float value)
		{

			Span<char> buf = stackalloc char[Base10MaxCapacitySingle];

			long x = unchecked((long) value);
			// ReSharper disable once CompareOfFloatsByEqualityOperator
			bool success = x != value
				? value.TryFormat(buf, out var written, "R", NumberFormatInfo.InvariantInfo)
				: x >= 0 ? x.TryFormat(buf, out written) // will be inlined as Number.TryUInt64ToDecStr
					: x.TryFormat(buf, out written, default, NumberFormatInfo.InvariantInfo); // will be inlined as Number.TryNegativeInt64ToDecStr
			if (!success) ReportInternalFormattingError();

			output.Write(buf.Slice(0, written));
		}

		#endregion

		#region Double...

		/// <summary>Converts a 64-bit IEEE floating point number into a decimal string literal, using the Invariant culture</summary>
		/// <param name="value">Value to convert</param>
		/// <returns>Corresponding string literal</returns>
		[Pure]
		public static string ToString(double value)
		{
			long x = unchecked((long) value);
			// ReSharper disable once CompareOfFloatsByEqualityOperator
			return x != value
				? value.ToString("R", NumberFormatInfo.InvariantInfo)
				: x >= 0 ? x.ToString(default(IFormatProvider))
				: x.ToString(NumberFormatInfo.InvariantInfo);
		}

		/// <summary>Writes the text representation of a 64-bit IEEE floating point number, using the Invariant culture</summary>
		/// <param name="output">Destination</param>
		/// <param name="value">Value to write</param>
		public static void WriteTo(TextWriter output, double value)
		{
			Span<char> buf = stackalloc char[Base10MaxCapacityDouble];

			long x = unchecked((long) value);
			// ReSharper disable once CompareOfFloatsByEqualityOperator
			bool success = x != value
				? value.TryFormat(buf, out var written, "R", NumberFormatInfo.InvariantInfo)
				: x >= 0 ? x.TryFormat(buf, out written) // will be inlined as Number.TryUInt64ToDecStr
					: x.TryFormat(buf, out written, default, NumberFormatInfo.InvariantInfo); // will be inlined as Number.TryNegativeInt64ToDecStr
			if (!success) ReportInternalFormattingError();

			output.Write(buf.Slice(0, written));
		}

		#endregion

		#region Decimal...

		/// <summary>Converts a 64-bit IEEE floating point number into a decimal string literal, using the Invariant culture</summary>
		/// <param name="value">Value to convert</param>
		/// <returns>Corresponding string literal</returns>
		[Pure]
		public static string ToString(decimal value)
		{
#if NET8_0_OR_GREATER
			return value.ToString("R", NumberFormatInfo.InvariantInfo);
#else
			return value.ToString("G", NumberFormatInfo.InvariantInfo);
#endif
		}

		/// <summary>Writes the text representation of a 128-bit decimal floating point number, using the Invariant culture</summary>
		/// <param name="output">Destination</param>
		/// <param name="value">Value to write</param>
		public static void WriteTo(TextWriter output, decimal value)
		{
			Span<char> buf = stackalloc char[Base10MaxCapacityDecimal];

			bool success = value.TryFormat(buf, out var written, default, NumberFormatInfo.InvariantInfo);
			if (!success) ReportInternalFormattingError();

			output.Write(buf.Slice(0, written));
		}

		#endregion

		#region Half...

		/// <summary>Converts a 16-bit IEEE floating point number into a decimal string literal, using the Invariant culture</summary>
		/// <param name="value">Value to convert</param>
		/// <returns>Corresponding string literal</returns>
		[Pure]
		public static string ToString(Half value)
		{
			//note: I'm not sure how to optimize for this type...
			return value.ToString(null, NumberFormatInfo.InvariantInfo);
		}

		/// <summary>Writes the text representation of a 16-bit IEEE floating point number, using the Invariant culture</summary>
		/// <param name="output">Destination</param>
		/// <param name="value">Value to write</param>
		public static void WriteTo(TextWriter output, Half value)
		{
			Span<char> buf = stackalloc char[StringConverters.Base10MaxCapacityHalf];

			//note: I'm not sure how to optimize for this type...
			bool success = value.TryFormat(buf, out var written, null, NumberFormatInfo.InvariantInfo);
			if (!success) ReportInternalFormattingError();

			output.Write(buf.Slice(0, written));
		}

		#endregion

		#region DateTime...

		/// <summary>Writes the text representation of a <see cref="DateTimeOffset"/> using the ISO 8601 format</summary>
		/// <param name="value">Value to convert</param>
		/// <returns>Corresponding string literal</returns>
		[Pure]
		public static string ToString(DateTime value)
		{
			//note: I'm not sure how to optimize for this type...
			return value.ToString(null, NumberFormatInfo.InvariantInfo);
		}

		/// <summary>Writes the text representation of a <see cref="DateTimeOffset"/> using the ISO 8601 format</summary>
		/// <param name="output">Destination</param>
		/// <param name="value">Value to write</param>
		public static void WriteTo(TextWriter output, DateTime value)
		{
			// "YYYY-MM-DDTHH:MM:SS.FFFFFFFZ" = 28
			Span<char> buf = stackalloc char[32];

			bool success = value.TryFormat(buf, out var written, "O", CultureInfo.InvariantCulture);
			if (!success) ReportInternalFormattingError();

			output.Write(buf.Slice(0, written));
		}

		#endregion

		#region DateTimeOffset...

		/// <summary>Writes the text representation of a <see cref="DateTimeOffset"/> using the ISO 8601 format</summary>
		/// <param name="value">Value to convert</param>
		/// <returns>Corresponding string literal</returns>
		[Pure]
		public static string ToString(DateTimeOffset value)
		{
			//note: I'm not sure how to optimize for this type...
			return value.ToString(null, NumberFormatInfo.InvariantInfo);
		}

		/// <summary>Writes the text representation of a <see cref="DateTimeOffset"/> using the ISO 8601 format</summary>
		/// <param name="output">Destination</param>
		/// <param name="value">Value to write</param>
		public static void WriteTo(TextWriter output, DateTimeOffset value)
		{
			// "YYYY-MM-DDTHH:MM:SS.FFFFFFF+HH:MM" = 33
			Span<char> buf = stackalloc char[36];

			bool success = value.TryFormat(buf, out var written, "O", CultureInfo.InvariantCulture);
			if (!success) ReportInternalFormattingError();

			output.Write(buf.Slice(0, written));
		}

		#endregion

		#endregion

		#region Parsing...

		/// <summary>Converts a string literal into a boolean, using relaxed rules</summary>
		/// <param name="value">String literal containing either a "truthy" or "falsy" boolean (ex: "true", "on", "1" vs "false", "off", "0", ...)</param>
		/// <param name="dflt">Default value, if the string is empty or not recognized</param>
		/// <returns>Corresponding boolean value if it matches the set of recognized tokens; otherwise, <paramref name="dflt"/>.</returns>
		/// <remarks>
		/// <para>The recognized "truthy" literals are: <c>"true"</c>, <c>"yes"</c>, <c>"on"</c>, <c>"1"</c>.</para>
		/// <para>The recognized "falsy" literals are: <c>"false"</c>, <c>"no"</c>, <c>"off"</c>, <c>"0"</c>, the null or empty string.</para>
		/// </remarks>
		[Pure]
		public static bool ToBoolean(string? value, bool dflt)
		{
			if (string.IsNullOrEmpty(value))
			{
				return dflt;
			}

			return char.ToLowerInvariant(value[0]) switch
			{
				't' => true,  // "true"
				'f' => false, // "false"
				'y' => true,  // "yes"
				'n' => false, // "no"
				'o' => value.Length switch
				{
					2 => (char.ToLowerInvariant(value[1]) == 'n' || dflt), // "on"
					3 => ((char.ToLowerInvariant(value[1]) != 'f' || char.ToLowerInvariant(value[2]) != 'f') && dflt), // "off"
					_ => dflt
				},
				'1' => true, // "1"
				'0' => false, // "0"
				_ => dflt
			};
		}

		/// <summary>Converts a string literal into its boolean equivalent, using relaxed rules</summary>
		/// <param name="value">String literal containing either a "truthy" or "falsy" boolean (ex: "true", "on", "1" vs "false", "off", "0", ...)</param>
		/// <returns>Corresponding boolean value if it matches the set of recognized tokens; otherwise, <see langword="null"/>.</returns>
		/// <remarks>
		/// <para>The recognized "truthy" literals are: <c>"true"</c>, <c>"yes"</c>, <c>"on"</c>, <c>"1"</c>.</para>
		/// <para>The recognized "falsy" literals are: <c>"false"</c>, <c>"no"</c>, <c>"off"</c>, <c>"0"</c>.</para>
		/// <para>The null and empty strings will return <see langword="null"/>.</para>
		/// </remarks>
		/// <example>
		/// <code>StringConverters.ToBoolean("true", false) == true</code>
		/// <code>StringConverters.ToBoolean("false", true) == false</code>
		/// <code>StringConverters.ToBoolean("hello", false) == false</code>
		/// </example>
		[Pure]
		public static bool? ToBoolean(string? value)
		{
			if (string.IsNullOrEmpty(value)) return null;
			char c = value[0];
			return char.ToLowerInvariant(c) switch
			{
				't' => true,  // "true"
				'f' => false, // "false"
				'y' => true,  // "yes"
				'n' => false, // "no"
				'o' => value.Length switch
				{
					2 => (char.ToLowerInvariant(value[1]) == 'n' ? true : null), // "on"
					3 => (char.ToLowerInvariant(value[1]) == 'f' && char.ToLowerInvariant(value[2]) == 'f' ? false : null), // "off"
					_ => null
				},
				'1' => true,
				'0' => false,
				_ => null
			};
		}

		/// <summary>Converts a string literal into its 32-bit signed integer equivalent, using invariant-culture format</summary>
		/// <param name="value">string literal to convert (ex: "1234" or "-5")</param>
		/// <param name="defaultValue">Fallback value returned if the string literal is empty or not a valid integer</param>
		/// <returns>Corresponding integer value, or <paramref name="defaultValue"/> if could not be decoded</returns>
		/// <example>
		/// <code>StringConverters.ToInt32("1234", 0) == 1234</code>
		/// <code>StringConverters.ToInt32("hello", 0) == 0</code>
		/// </example>
		[Pure]
		public static int ToInt32(string? value, int defaultValue)
		{
			if (string.IsNullOrEmpty(value))
			{
				return defaultValue;
			}

			char c = value[0];
			if (value.Length == 1)
			{
				return char.IsDigit(c) ? c - 48 : defaultValue;
			}

			if (!char.IsDigit(c) && c != '-' && c != '+' && c != ' ')
			{
				return defaultValue;
			}
			
			return int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int res) ? res : defaultValue;
		}

		/// <summary>Converts a string literal into its 32-bit signed integer equivalent, using invariant-culture format</summary>
		/// <param name="value">string literal to convert (ex: "1234" or "-5")</param>
		/// <returns>Corresponding integer value, or <see langword="null"/> if could not be decoded</returns>
		/// <example>
		/// <code>StringConverters.ToInt32("1234") == 1234</code>
		/// <code>StringConverters.ToInt32("hello") == null</code>
		/// </example>
		[Pure]
		public static int? ToInt32(string? value)
		{
			if (string.IsNullOrEmpty(value)) return default;
			// optimisation: si premier caractère pas chiffre, exit
			char c = value[0];
			if (value.Length == 1) return char.IsDigit(c) ? (c - 48) : default(int?);
			if (!char.IsDigit(c) && c != '-' && c != '+' && c != ' ') return default(int?);
			return int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int res) ? res : default(int?);
		}

		/// <summary>Converts a string literal into its 64-bit signed integer equivalent, using invariant-culture format</summary>
		/// <param name="value">string literal to convert (ex: "1234" or "-5")</param>
		/// <param name="defaultValue">Fallback value returned if the string literal is empty or not a valid integer</param>
		/// <returns>Corresponding integer value, or <paramref name="defaultValue"/> if could not be decoded</returns>
		/// <example>
		/// <code>StringConverters.ToInt64("1234", 0) == 1234</code>
		/// <code>StringConverters.ToInt64("hello", 0) == 0</code>
		/// </example>
		[Pure]
		public static long ToInt64(string? value, long defaultValue)
		{
			if (string.IsNullOrEmpty(value)) return defaultValue;
			// optimisation: si premier caractère pas chiffre, exit
			char c = value[0];
			if (value.Length == 1) return char.IsDigit(c) ? ((long) c - 48) : defaultValue;
			if (!char.IsDigit(c) && c != '-' && c != '+' && c != ' ') return defaultValue;
			return long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out long res) ? res : defaultValue;
		}

		/// <summary>Converts a string literal into its 64-bit signed integer equivalent, using invariant-culture format</summary>
		/// <param name="value">string literal to convert (ex: "1234" or "-5")</param>
		/// <returns>Corresponding integer value, or <see langword="null"/> if could not be decoded</returns>
		/// <example>
		/// <code>StringConverters.ToInt64("1234") == 1234</code>
		/// <code>StringConverters.ToInt64("hello") == null</code>
		/// </example>
		[Pure]
		public static long? ToInt64(string? value)
		{
			if (string.IsNullOrEmpty(value))
			{
				return default;
			}

			char c = value[0];
			if (value.Length == 1)
			{
				return char.IsDigit(c) ? ((long) c - 48) : default(long?);
			}

			if (!char.IsDigit(c) && c != '-' && c != '+' && c != ' ')
			{
				return default;
			}

			return long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out long res) ? res : default(long?);
		}

		/// <summary>Converts a string literal into its 64-bit floating-point number equivalent, using invariant-culture format</summary>
		/// <param name="value">string literal to convert (ex: "1.0" or "123" or "123.456e7")</param>
		/// <param name="defaultValue">Fallback value returned if the string literal is empty or not a valid decimal number</param>
		/// <param name="provider">Optional Format provider</param>
		/// <returns>Corresponding double value, or <paramref name="defaultValue"/> if could not be decoded</returns>
		/// <example>
		/// <code>StringConverters.ToDouble("1.23", 0) => 1.23d</code>
		/// <code>StringConverters.ToDouble("hello", 0) => 0</code>
		/// <code>StringConverters.ToDouble("NaN", 0)) => double.NaN</code>
		/// <code>StringConverters.ToDouble("∞", 0)) => double.PositiveInfinity</code>
		/// <code>StringConverters.ToDouble("-∞", 0)) => double.NegativeInfinity</code>
		/// </example>
		[Pure]
		public static double ToDouble(string? value, double defaultValue, IFormatProvider? provider = null)
		{
			if (string.IsNullOrEmpty(value))
			{ // empty
				return defaultValue;
			}

			char c = value[0];
			if (value.Length == 1)
			{ // single-digit number
				return char.IsDigit(c) ? c - '0' : c == '∞' ? double.PositiveInfinity : defaultValue;
			}

			// note: TryParse with InvariantCulture will handle "NaN" but not "∞", "+∞", "-∞"
			return double.TryParse(value, NumberStyles.Float, provider ?? CultureInfo.InvariantCulture, out double result)
				? result
				: value == "-∞" ? double.NegativeInfinity
				: defaultValue;
		}

		/// <summary>Converts a string literal into its 64-bit floating-point number equivalent, using invariant-culture format</summary>
		/// <param name="value">string literal to convert (ex: "1.0" or "123" or "123.456e7")</param>
		/// <returns>Corresponding double value, or <see langword="null"/> if could not be decoded</returns>
		/// <example>
		/// <code>StringConverters.ToDouble("1.23") => 1.23d</code>
		/// <code>StringConverters.ToDouble("hello") => null</code>
		/// <code>StringConverters.ToDouble("NaN")) => double.NaN</code>
		/// <code>StringConverters.ToDouble("∞")) => double.PositiveInfinity</code>
		/// <code>StringConverters.ToDouble("-∞")) => double.NegativeInfinity</code>
		/// </example>
		[Pure]
		public static double? ToDouble(string? value)
		{
			if (string.IsNullOrEmpty(value))
			{ // empty
				return null;
			}

			char c = value[0];
			if (value.Length == 1)
			{ // single-digit number
				return char.IsDigit(c) ? c - '0' : c == '∞' ? double.PositiveInfinity : null;
			}

			// note: TryParse with InvariantCulture will handle "NaN" but not "∞", "+∞", "-∞"
			return double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out double result)
				? result
				: value == "-∞" ? double.NegativeInfinity
				: null;
		}

		/// <summary>Converts a string literal into its 32-bit floating-point number equivalent, using invariant-culture format</summary>
		/// <param name="value">string literal to convert (ex: "1.0" or "123" or "123.456e7")</param>
		/// <param name="defaultValue">Fallback value returned if the string literal is empty or not a valid decimal number</param>
		/// <param name="provider">Optional Format provider</param>
		/// <returns>Corresponding single value, or <paramref name="defaultValue"/> if could not be decoded</returns>
		/// <example>
		/// <code>StringConverters.ToSingle("1.23", 0) => 1.23d</code>
		/// <code>StringConverters.ToSingle("hello", 0) => 0</code>
		/// <code>StringConverters.ToSingle("NaN", 0)) => float.NaN</code>
		/// <code>StringConverters.ToSingle("∞", 0)) => float.PositiveInfinity</code>
		/// <code>StringConverters.ToSingle("-∞", 0)) => float.NegativeInfinity</code>
		/// </example>
		[Pure]
		public static float ToSingle(string? value, float defaultValue, IFormatProvider? provider = null)
		{
			if (string.IsNullOrEmpty(value))
			{ // empty
				return defaultValue;
			}

			char c = value[0];
			if (value.Length == 1)
			{ // single-digit number
				return char.IsDigit(c) ? c - '0' : c == '∞' ? float.PositiveInfinity : defaultValue;
			}

			// note: TryParse with InvariantCulture will handle "NaN" but not "∞", "+∞", "-∞"
			return float.TryParse(value, NumberStyles.Float, provider ?? CultureInfo.InvariantCulture, out float result)
				? result
				: value == "-∞" ? float.NegativeInfinity
				: defaultValue;
		}

		/// <summary>Converts a string literal into its 32-bit floating-point number equivalent, using invariant-culture format</summary>
		/// <param name="value">string literal to convert (ex: "1.0" or "123" or "123.456e7")</param>
		/// <returns>Corresponding single value, or <see langword="null"/> if could not be decoded</returns>
		/// <example>
		/// <code>StringConverters.ToSingle("1.23") => 1.23d</code>
		/// <code>StringConverters.ToSingle("hello") => null</code>
		/// <code>StringConverters.ToSingle("NaN")) => float.NaN</code>
		/// <code>StringConverters.ToSingle("∞")) => float.PositiveInfinity</code>
		/// <code>StringConverters.ToSingle("-∞")) => float.NegativeInfinity</code>
		/// </example>
		[Pure]
		public static float? ToSingle(string? value)
		{
			if (string.IsNullOrEmpty(value))
			{ // empty
				return null;
			}

			char c = value[0];
			if (value.Length == 1)
			{ // single-digit number
				return char.IsDigit(c) ? c - '0' : c == '∞' ? float.PositiveInfinity : null;
			}

			// note: TryParse with InvariantCulture will handle "NaN" but not "∞", "+∞", "-∞"
			return float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out float result)
				? result
				: value == "-∞" ? float.NegativeInfinity
				: null;
		}

		/// <summary>Convertit une chaîne de caractère en double, quelque soit la langue locale (utilise le '.' comme séparateur décimal)</summary>
		/// <param name="value">Chaîne (ex: "1.0", "123.456e7")</param>
		/// <param name="defaultValue">Valeur par défaut si problème de conversion ou null</param>
		/// <param name="culture">Culture (par défaut InvariantCulture)</param>
		/// <returns>Décimal correspondant</returns>
		[Pure]
		public static decimal ToDecimal(string? value, decimal defaultValue, IFormatProvider? culture = null)
		{
			if (string.IsNullOrEmpty(value)) return defaultValue;
			char c = value[0];
			if (!char.IsDigit(c) && c != '+' && c != '-' && c != '.' && c != ' ') return defaultValue;
			culture ??= CultureInfo.InvariantCulture;
			if (culture.Equals(CultureInfo.InvariantCulture) && value.IndexOf(',') >= 0) value = value.Replace(',', '.');
			return decimal.TryParse(value, NumberStyles.Float | NumberStyles.AllowThousands, culture, out decimal result) ? result : defaultValue;
		}

		[Pure]
		public static decimal? ToDecimal(string? value, IFormatProvider? culture = null)
		{
			if (string.IsNullOrEmpty(value)) return default;
			char c = value[0];
			if (!char.IsDigit(c) && c != '+' && c != '-' && c != '.' && c != ' ') return default(decimal?);
			culture ??= CultureInfo.InvariantCulture;
			if (culture.Equals(CultureInfo.InvariantCulture) && value.IndexOf(',') >= 0) value = value.Replace(',', '.');
			return decimal.TryParse(value, NumberStyles.Float | NumberStyles.AllowThousands, culture, out decimal result) ? result : default(decimal?);
		}

		/// <summary>Convertit une chaîne en DateTime</summary>
		/// <param name="value">Date à convertir</param>
		/// <param name="defaultValue">Valeur par défaut</param>
		/// <param name="culture"></param>
		/// <returns>Voir StringConverters.ParseDateTime()</returns>
		[Pure]
		public static DateTime ToDateTime(string value, DateTime defaultValue, CultureInfo? culture = null)
		{
			return ParseDateTime(value, defaultValue, culture);
		}

		/// <summary>Convertit une chaîne en DateTime</summary>
		/// <param name="value">Date à convertir</param>
		/// <param name="culture"></param>
		/// <returns>Voir StringConverters.ParseDateTime()</returns>
		[Pure]
		public static DateTime? ToDateTime(string? value, CultureInfo? culture = null)
		{
			if (string.IsNullOrEmpty(value)) return default;
			DateTime result = ParseDateTime(value, DateTime.MaxValue, culture);
			return result == DateTime.MaxValue ? default(DateTime?) : result;
		}

		[Pure]
		public static Instant? ToInstant(string? date, CultureInfo? culture = null)
		{
			if (string.IsNullOrEmpty(date)) return default;
			if (!TryParseInstant(date, culture, out Instant res, false)) return default(Instant?);
			return res;
		}

		[Pure]
		public static Instant ToInstant(string? date, Instant dflt, CultureInfo? culture = null)
		{
			if (string.IsNullOrEmpty(date)) return dflt;
			if (!TryParseInstant(date, culture, out Instant res, false)) return dflt;
			return res;
		}

		/// <summary>Convertit une chaîne de caractères en GUID</summary>
		/// <param name="value">Chaîne (ex: "123456-789")</param>
		/// <param name="defaultValue">Valeur par défaut si problème de conversion ou null</param>
		/// <returns>GUID correspondant</returns>
		[Pure]
		public static Guid ToGuid(string? value, Guid defaultValue)
		{
			if (string.IsNullOrEmpty(value)) return defaultValue;
			return Guid.TryParse(value, out Guid result) ? result : defaultValue;
		}

		[Pure]
		public static Guid? ToGuid(string? value)
		{
			if (string.IsNullOrEmpty(value)) return default;
			return Guid.TryParse(value, out Guid result) ? result : default(Guid?);
		}

		/// <summary>Convertit une chaîne de caractères en Enum</summary>
		/// <typeparam name="TEnum">Type de l'Enum</typeparam>
		/// <param name="value">Chaîne (ex: "Red", "2", ...)</param>
		/// <param name="defaultValue">Valeur par défaut si problème de conversion ou null</param>
		/// <returns>Valeur de l'enum correspondante</returns>
		/// <remarks>Accepte les valeurs sous forme textuelle ou numérique, case insensitive</remarks>
		[Pure]
		public static TEnum ToEnum<TEnum>(string? value, TEnum defaultValue)
			where TEnum : struct, Enum
		{
			if (string.IsNullOrEmpty(value)) return defaultValue;
			return Enum.TryParse(value, true, out TEnum result) ? result : defaultValue;
		}

		[Pure]
		public static TEnum? ToEnum<TEnum>(string? value)
			where TEnum : struct, Enum
		{
			if (string.IsNullOrEmpty(value)) return default(TEnum?);
			return Enum.TryParse(value, true, out TEnum result) ? result : default(TEnum?);
		}

		/// <summary>Maximum number of characters required to safely format any signed 8-bit integer in base 10</summary>
		/// <remarks><see cref="sbyte.MinValue"/> needs 4 characters in base 10 (including the negative sign)</remarks>
		public const int Base10MaxCapacityInt8 = 4;

		/// <summary>Maximum number of characters required to safely format any unsigned 8-bit integer in base 10</summary>
		/// <remarks><see cref="byte.MaxValue"/> needs 3 characters in base 10</remarks>
		public const int Base10MaxCapacityUInt8 = 3;

		/// <summary>Maximum number of characters required to safely format any signed 16-bit integer in base 10</summary>
		/// <remarks><see cref="short.MinValue"/> needs 6 characters in base 10 (including the negative sign)</remarks>
		public const int Base10MaxCapacityInt16 = 6;

		/// <summary>Maximum number of characters required to safely format any unsigned 16-bit integer in base 10</summary>
		/// <remarks><see cref="ushort.MaxValue"/> needs 5 characters in base 10</remarks>
		public const int Base10MaxCapacityUInt16 = 5;

		/// <summary>Maximum number of characters required to safely format any signed 32-bit integer in base 10</summary>
		/// <remarks><see cref="int.MinValue"/> needs 11 characters in base 10 (including the negative sign)</remarks>
		public const int Base10MaxCapacityInt32 = 11;

		/// <summary>Maximum number of characters required to safely format any unsigned 32-bit integer in base 10</summary>
		/// <remarks><see cref="uint.MaxValue"/> needs 10 characters in base 10</remarks>
		public const int Base10MaxCapacityUInt32 = 10;

		/// <summary>Maximum number of characters required to safely format any signed 64-bit integer in base 10</summary>
		/// <remarks><see cref="long.MinValue"/> needs 20 characters in base 10 (including the negative sign)</remarks>
		/// 
		public const int Base10MaxCapacityInt64 = 20;

		/// <summary>Maximum number of characters required to safely format any unsigned 64-bit integer in base 10</summary>
		/// <remarks><see cref="ulong.MaxValue"/> needs 20 characters in base 10</remarks>
		public const int Base10MaxCapacityUInt64 = 20;

		/// <summary>Maximum number of characters required to safely format any 32-bit IEEE floating point number in base 10</summary>
		/// <remarks>This the value used by the runtime in Number.TryFormatDouble.</remarks>
		public const int Base10MaxCapacitySingle = 32;

		/// <summary>Maximum number of characters required to safely format any 64-bit IEEE floating point number in base 10</summary>
		/// <remarks>This the value used by the runtime in Number.TryFormatDouble.</remarks>
		public const int Base10MaxCapacityDouble = 32;

		/// <summary>Maximum number of characters required to safely format any 128-bit decimal floating point number in base 10</summary>
		/// <remarks>This the value used by the runtime in Number.TryFormatDouble.</remarks>
		public const int Base10MaxCapacityDecimal = 32;

		/// <summary>Maximum number of characters required to safely format any 16-bit IEEE floating point number in base 10</summary>
		/// <remarks>This the value used by the runtime in Number.TryFormatDouble.</remarks>
		public const int Base10MaxCapacityHalf = 32;

		/// <summary>Maximum number of characters required to safely format any 128-bit using the standard format</summary>
		/// <remarks>"ffffffff-ffff-ffff-ffff-ffffffffffff" requires 36 characters</remarks>
		public const int Base16MaxCapacityGuid = 36;

		/// <summary>Maximum number of characters required to safely format any 128-bit using the standard format</summary>
		/// <remarks>"ffffffff-ffff-ffff-ffff-ffffffffffff" requires 36 characters</remarks>
		public const int Base16MaxCapacityUuid128 = 36;

		/// <summary>Maximum number of characters required to safely format any 64-bit using the standard format</summary>
		/// <remarks>"ffffffff-ffffffff" requires 17 characters</remarks>
		public const int Base16MaxCapacityUuid64 = 17;

#if NET8_0_OR_GREATER

		/// <summary>Maximum number of characters required to safely format any signed 64-bit integer in base 10</summary>
		/// <remarks><see cref="Int128.MinValue"/> needs 40 characters in base 10 (including the negative sign)</remarks>
		public const int Base10MaxCapacityInt128 = 40;

		/// <summary>Maximum number of characters required to safely format any unsigned 64-bit integer in base 10</summary>
		/// <remarks><see cref="UInt128.MaxValue"/> needs 39 characters in base 10</remarks>
		public const int Base10MaxCapacityUInt128 = 39;

#endif

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static int CountDigits(long value)
		{
			return value >= 0 ? CountDigits((ulong) value) : value != long.MinValue ? (1 + CountDigits((ulong)(-value))) : Base10MaxCapacityInt64;
		}


		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static int CountDigits(ulong value)
		{
			// Copied from System.Buffers.Text.FormattingHelpers.CountDigits(ulong) that is marked internal
			// Based on do_count_digits from https://github.com/fmtlib/fmt/blob/662adf4f33346ba9aba8b072194e319869ede54a/include/fmt/format.h#L1124

			if (value < 10) return 1;

			// Map the log2(value) to a power of 10.
			ReadOnlySpan<byte> log2ToPow10 =
			[
				1,  1,  1,  2,  2,  2,  3,  3,  3,  4,  4,  4,  4,  5,  5,  5,
				6,  6,  6,  7,  7,  7,  7,  8,  8,  8,  9,  9,  9,  10, 10, 10,
				10, 11, 11, 11, 12, 12, 12, 13, 13, 13, 13, 14, 14, 14, 15, 15,
				15, 16, 16, 16, 16, 17, 17, 17, 18, 18, 18, 19, 19, 19, 19, 20
			];

			uint index = Unsafe.Add(ref MemoryMarshal.GetReference(log2ToPow10), BitOperations.Log2(value));

			// Read the associated power of 10.
			ReadOnlySpan<ulong> powersOf10 =
			[
				0, // unused entry to avoid needing to subtract
				0, 10, 100,
				1_000, 10_000, 100_000,
				1_000_000, 10_000_000, 100_000_000,
				1_000_000_000, 10_000_000_000, 100_000_000_000,
				1_000_000_000_000, 10_000_000_000_000, 100_000_000_000_000,
				1_000_000_000_000_000, 10_000_000_000_000_000, 100_000_000_000_000_000,
				1_000_000_000_000_000_000, 10_000_000_000_000_000_000,
			];
			ulong powerOf10 = Unsafe.Add(ref MemoryMarshal.GetReference(powersOf10), index);

			// Return the number of digits based on the power of 10, shifted by 1 if it falls below the threshold.
			bool lessThan = value < powerOf10;
			return (int) (index - Unsafe.As<bool, byte>(ref lessThan)); // while arbitrary bools may be non-0/1, comparison operators are expected to return 0/1
		}

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static int CountDigits(short value) => CountDigits((int) value);

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static int CountDigits(ushort value) => CountDigits((uint) value);

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static int CountDigits(int value)
		{
			return value >= 0 ? CountDigits((uint) value) : value != int.MinValue ? (1 + CountDigits((uint) (-value))) : Base10MaxCapacityInt32;
		}

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static int CountDigits(uint value)
		{

			if (value < 10) return 1;
#if NET8_0_OR_GREATER
			// Copied from System.Buffers.Text.FormattingHelpers.CountDigits(ulong) that is marked internal
			// Algorithm based on https://lemire.me/blog/2021/06/03/computing-the-number-of-digits-of-an-integer-even-faster.
			ReadOnlySpan<long> table =
			[
				4294967296, 8589934582, 8589934582, 8589934582, 12884901788, 12884901788, 12884901788, 17179868184,
				17179868184, 17179868184, 21474826480, 21474826480, 21474826480, 21474826480, 25769703776, 25769703776,
				25769703776, 30063771072, 30063771072, 30063771072, 34349738368, 34349738368, 34349738368, 34349738368,
				38554705664, 38554705664, 38554705664, 41949672960, 41949672960, 41949672960, 42949672960, 42949672960,
			];

			long tableValue = Unsafe.Add(ref MemoryMarshal.GetReference(table), uint.Log2(value));
			return (int) ((value + tableValue) >> 32);
#else
			// slow path for .NET 6
			if (value < 100) return 2;
			if (value < 1000) return 3;
			if (value < 10000) return 4;
			if (value < 100000) return 5;
			if (value < 1000000) return 6;
			if (value < 10000000) return 7;
			if (value < 100000000) return 8;
			if (value < 1000000000) return 9;
			return 10;
#endif
		}

		#endregion

		#region Dates...

		/// <summary>Convertit une date en une chaîne de caractères au format "YYYYMMDDHHMMSS"</summary>
		/// <param name="date">Date à formater</param>
		/// <returns>Date formatée sur 14 caractères au format YYYYMMDDHHMMSS</returns>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static string ToDateTimeString(DateTime date)
		{
			//REVIEW: PERF: faire une version optimisée?
			return date.ToString("yyyyMMddHHmmss", CultureInfo.InvariantCulture);
		}

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static string ToDateTimeString(Instant date)
		{
			//REVIEW: PERF: faire une version optimisée?
			return date.ToString("yyyyMMddHHmmss", CultureInfo.InvariantCulture);
		}

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static string ToDateTimeString(ZonedDateTime date)
		{
			//REVIEW: PERF: faire une version optimisée?
			return date.ToString("yyyyMMddHHmmss", CultureInfo.InvariantCulture);
		}

		/// <summary>Convertit une date en une chaîne de caractères au format "AAAAMMJJ"</summary>
		/// <param name="date">Date à formater</param>
		/// <returns>Date formatée sur 8 caractères au format AAAAMMJJ</returns>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static string ToDateString(DateTime date)
		{
			//REVIEW: PERF: faire une version optimisée?
			return date.ToString("yyyyMMdd", CultureInfo.InvariantCulture);
		}

		/// <summary>Convertit un heure en une chaîne de caractères au format "HHMMSS"</summary>
		/// <param name="date">Date à formater</param>
		/// <returns>Heure formatée sur 6 caractères au format HHMMSS</returns>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static string ToTimeString(DateTime date)
		{
			//REVIEW: PERF: faire une version optimisée?
			return date.ToString("HHmmss", CultureInfo.InvariantCulture);
		}

		/// <summary>Convertit une date en une chaîne de caractères au format "yyyy-MM-dd HH:mm:ss"</summary>
		/// <param name="date">Date à convertir</param>
		/// <returns>Chaîne au format "yyyy-MM-dd HH:mm:ss"</returns>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static string FormatDateTime(DateTime date)
		{
			//REVIEW: PERF: faire une version optimisée?
			return date.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);
		}

		/// <summary>Convertit une date en une chaîne de caractères au format "yyyy-MM-dd"</summary>
		/// <param name="date">Date à convertir</param>
		/// <returns>Chaîne au format "yyyy-MM-dd"</returns>
		[Pure]
		public static string FormatDate(DateTime date)
		{
			//REVIEW: PERF: faire une version optimisée?
			return date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
		}

		/// <summary>Convertit une heure en une chaîne de caractères au format "hh:mm:ss"</summary>
		/// <param name="date">Heure à convertir</param>
		/// <returns>Chaîne au format "hh:mm:ss"</returns>
		[Pure]
		public static string FormatTime(DateTime date)
		{
			//REVIEW: PERF: faire une version optimisée?
			return date.ToString("HH:mm:ss", CultureInfo.InvariantCulture);
		}

		/// <summary>Convertit une chaîne de caractère au format "YYYY", "YYYYMM", "YYYYMMDD" ou "YYYYMMDDHHMMSS" en DateTime</summary>
		/// <param name="date">Chaîne de caractères à convertir</param>
		/// <returns>Objet DateTime correspondant, ou exception si incorrect</returns>
		/// <exception cref="System.ArgumentException">Si la date est incorrecte</exception>
		[Pure]
		public static DateTime ParseDateTime(string? date)
		{
			return ParseDateTime(date, null);
		}

		/// <summary>Convertit une chaîne de caractère au format "YYYY", "YYYYMM", "YYYYMMDD" ou "YYYYMMDDHHMMSS" en DateTime</summary>
		/// <param name="date">Chaîne de caractères à convertir</param>
		/// <param name="culture">Culture (pour le format attendu) ou null</param>
		/// <returns>Objet DateTime correspondant, ou exception si incorrect</returns>
		/// <exception cref="System.ArgumentException">Si la date est incorrecte</exception>
		[Pure]
		public static DateTime ParseDateTime(string? date, CultureInfo? culture)
		{
			if (!TryParseDateTime(date, culture, out DateTime result, true)) throw FailInvalidDateFormat();
			return result;
		}

		[Pure, MethodImpl(MethodImplOptions.NoInlining)]
		private static Exception FailInvalidDateFormat()
		{
			// ReSharper disable once NotResolvedInText
			return new ArgumentException("Invalid date format", "date");
		}

		/// <summary>Convertit une chaîne de caractère au format "YYYY", "YYYYMM", "YYYYMMDD" ou "YYYYMMDDHHMMSS" en DateTime</summary>
		/// <param name="date">Chaîne de caractères à convertir</param>
		/// <param name="dflt">Valeur par défaut</param>
		/// <returns>Objet DateTime correspondant, ou dflt si date est null ou vide</returns>
		[Pure]
		public static DateTime ParseDateTime(string? date, DateTime dflt)
		{
			if (string.IsNullOrEmpty(date)) return dflt;
			if (!TryParseDateTime(date, null, out DateTime result, false)) return dflt;
			return result;
		}

		/// <summary>Convertit une chaîne de caractère au format "YYYY", "YYYYMM", "YYYYMMDD" ou "YYYYMMDDHHMMSS" en DateTime</summary>
		/// <param name="date">Chaîne de caractères à convertir</param>
		/// <param name="dflt">Valeur par défaut</param>
		/// <param name="culture">Culture (pour le format attendu) ou null</param>
		/// <returns>Objet DateTime correspondant, ou dflt si date est null ou vide</returns>
		[Pure]
		public static DateTime ParseDateTime(string? date, DateTime dflt, CultureInfo? culture)
		{
			if (!TryParseDateTime(date, culture, out DateTime result, false)) return dflt;
			return result;
		}

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		private static int ParseDateSegment(ReadOnlySpan<char> source)
		{
			return int.TryParse(source, NumberStyles.Integer, CultureInfo.InvariantCulture, out int r) ? r : -1;
		}

		/// <summary>Essayes de convertir une chaîne de caractères au format "YYYY", "YYYYMM", "YYYYMMDD" ou "YYYYMMDDHHMMSS" en DateTime</summary>
		/// <param name="date">Chaîne de caractères à convertir</param>
		/// <param name="culture">Culture (pour le format attendu) ou null</param>
		/// <param name="result">Date convertie (ou DateTime.MinValue en cas de problème)</param>
		/// <param name="throwsFail">Si false, absorbe les exceptions éventuelles. Si true, laisse les s'échaper</param>
		/// <returns>True si la date est correcte, false dans les autres cas</returns>
		[Pure]
		public static bool TryParseDateTime(string? date, CultureInfo? culture, out DateTime result, bool throwsFail)
		{
			if (date == null)
			{
				if (throwsFail) throw new ArgumentNullException(nameof(date));
				result = DateTime.MinValue;
				return false;
			}
			return TryParseDateTime(date.AsSpan(), culture, out result, throwsFail);
		}

		/// <summary>Essayes de convertir une chaîne de caractères au format "YYYY", "YYYYMM", "YYYYMMDD" ou "YYYYMMDDHHMMSS" en DateTime</summary>
		/// <param name="date">Chaîne de caractères à convertir</param>
		/// <param name="culture">Culture (pour le format attendu) ou null</param>
		/// <param name="result">Date convertie (ou DateTime.MinValue en cas de problème)</param>
		/// <param name="throwsFail">Si false, absorbe les exceptions éventuelles. Si true, laisse les s'échaper</param>
		/// <returns>True si la date est correcte, false dans les autres cas</returns>
		[Pure]
		public static bool TryParseDateTime(ReadOnlySpan<char> date, CultureInfo? culture, out DateTime result, bool throwsFail)
		{
			result = DateTime.MinValue;

			if (date.Length < 4)
			{
				return !throwsFail ? false : throw new FormatException("Date must be at least 4 characters long");
			}

			try
			{
				if (char.IsDigit(date[0]))
				{ // commence par un chiffre, c'est peut etre un timestamp?
					switch (date.Length)
					{
						case 4:
						{ // YYYY -> YYYY/01/01 00:00:00.000
							int y = ParseDateSegment(date.Slice(0, 4));
							if (y < 1 || y > 9999) break;
							result = new DateTime(y, 1, 1);
							return true;
						}
						case 6:
						{ // YYYYMM -> YYYY/MM/01 00:00:00.000
							int y = ParseDateSegment(date.Slice(0, 4));
							if (y < 1 || y > 9999) break;
							int m = ParseDateSegment(date.Slice(4, 2));
							if (m < 1 || m > 12) break;
							result = new DateTime(y, m, 1);
							return true;
						}
						case 8:
						{ // YYYYMMDD -> YYYY/MM/DD 00:00:00.000
							int y = ParseDateSegment(date.Slice(0, 4));
							if (y < 1 || y > 9999) break;
							int m = ParseDateSegment(date.Slice(4, 2));
							if (m < 1 || m > 12) break;
							int d = ParseDateSegment(date.Slice(6, 2));
							if (d < 1 || d > 31) break;
							result = new DateTime(y, m, d);
							return true;
						}
						case 14:
						{ // YYYYMMDDHHMMSS -> YYYY/MM/DD HH:MM:SS.000
							int y = ParseDateSegment(date.Slice(0, 4));
							if (y < 1 || y > 9999) break;
							int m = ParseDateSegment(date.Slice(4, 2));
							if (m < 1 || m > 12) break;
							int d = ParseDateSegment(date.Slice(6, 2));
							if (d < 1 || d > 31) break;
							int h = ParseDateSegment(date.Slice(8, 2));
							if (h < 0 || h > 23) break;
							int n = ParseDateSegment(date.Slice(10, 2));
							if (n < 0 || n > 59) break;
							int s = ParseDateSegment(date.Slice(12, 2));
							if (s < 0 || s > 59) break;
							result = new DateTime(y, m, d, h, n, s);
							return true;
						}
						case 17:
						{ // YYYYMMDDHHMMSSFFF -> YYYY/MM/DD HH:MM:SS.FFF
							int y = ParseDateSegment(date.Slice(0, 4));
							if (y < 1 || y > 9999) break;
							int m = ParseDateSegment(date.Slice(4, 2));
							if (m < 1 || m > 12) break;
							int d = ParseDateSegment(date.Slice(6, 2));
							if (d < 1 || d > 31) break;
							int h = ParseDateSegment(date.Slice(8, 2));
							if (h < 0 || h > 23) break;
							int n = ParseDateSegment(date.Slice(10, 2));
							if (n < 0 || n > 59) break;
							int s = ParseDateSegment(date.Slice(12, 2));
							if (s < 0 || s > 59) break;
							int f = ParseDateSegment(date.Slice(14, 3));
							result = new DateTime(y, m, d, h, n, s, f);
							return true;
						}
					}
				}
				else if (char.IsLetter(date[0]))
				{ // on va tenter un ParseExact ("Vendredi, 37 Trumaire 1789 à 3 heures moins le quart")
					result = DateTime.ParseExact(date, [ "D", "F", "f" ], culture ?? CultureInfo.InvariantCulture, DateTimeStyles.None);
					return true;
				}

				// Je vais tenter le jackpot, mon cher Julien!
				result = DateTime.Parse(date, culture ?? CultureInfo.InvariantCulture);
				return true;
			}
			catch (FormatException)
			{ // Dommage! La cagnotte est remise à la fois prochaine...
				if (throwsFail) throw;
				return false;
			}
			catch (ArgumentOutOfRangeException)
			{ // Pb sur un DateTime avec des dates invalides (31 février, ...)
				if (throwsFail) throw;
				return false;
			}
		}

		/// <summary>Essayes de convertir une chaîne de caractères au format "YYYY", "YYYYMM", "YYYYMMDD" ou "YYYYMMDDHHMMSS" en DateTime</summary>
		/// <param name="date">Chaîne de caractères à convertir</param>
		/// <param name="culture">Culture (pour le format attendu) ou null</param>
		/// <param name="result">Date convertie (ou DateTime.MinValue en cas de problème)</param>
		/// <param name="throwsFail">Si false, absorbe les exceptions éventuelles. Si true, laisse les s'échapper</param>
		/// <returns>True si la date est correcte, false dans les autres cas</returns>
		[Pure]
		public static bool TryParseInstant(string? date, CultureInfo? culture, out Instant result, bool throwsFail)
		{

			if (date == null)
			{
				if (throwsFail) throw new ArgumentNullException(nameof(date));
				result = default(Instant);
				return false;
			}

			return TryParseInstant(date.AsSpan(), culture, out result, throwsFail);
		}

		/// <summary>Essayes de convertir une chaîne de caractères au format "YYYY", "YYYYMM", "YYYYMMDD" ou "YYYYMMDDHHMMSS" en DateTime</summary>
		/// <param name="date">Chaîne de caractères à convertir</param>
		/// <param name="culture">Culture (pour le format attendu) ou null</param>
		/// <param name="result">Date convertie (ou DateTime.MinValue en cas de problème)</param>
		/// <param name="throwsFail">Si false, absorbe les exceptions éventuelles. Si true, laisse les s'échapper</param>
		/// <returns>True si la date est correcte, false dans les autres cas</returns>
		[Pure]
		public static bool TryParseInstant(ReadOnlySpan<char> date, CultureInfo? culture, out Instant result, bool throwsFail)
		{
			result = default(Instant);

			if (date.Length < 4)
			{
				if (throwsFail) throw new FormatException("Date must be at least 4 characters long");
				return false;
			}
			if (!char.IsDigit(date[0]))
			{
				if (throwsFail) throw new FormatException("Date must contains only digits");
				return false;
			}
			try
			{
				switch (date.Length)
				{
					case 4:
					{ // YYYY -> YYYY/01/01 00:00:00.000
						int y = ParseDateSegment(date.Slice(0, 4));
						if (y < 1 || y > 9999) break;
						result = Instant.FromUtc(y, 1, 1, 0, 0);
						return true;
					}
					case 6:
					{ // YYYYMM -> YYYY/MM/01 00:00:00.000
						int y = ParseDateSegment(date.Slice(0, 4));
						if (y < 1 || y > 9999) break;
						int m = ParseDateSegment(date.Slice(4, 2));
						if (m < 1 || m > 12) break;
						result = Instant.FromUtc(y, m, 1, 0, 0);
						return true;
					}
					case 8:
					{ // YYYYMMDD -> YYYY/MM/DD 00:00:00.000
						int y = ParseDateSegment(date.Slice(0, 4));
						if (y < 1 || y > 9999) break;
						int m = ParseDateSegment(date.Slice(4, 2));
						if (m < 1 || m > 12) break;
						int d = ParseDateSegment(date.Slice(6, 2));
						if (d < 1 || d > 31) break;
						result = Instant.FromUtc(y, m, d, 0, 0);
						return true;
					}
					case 14:
					{ // YYYYMMDDHHMMSS -> YYYY/MM/DD HH:MM:SS.000
						int y = ParseDateSegment(date.Slice(0, 4));
						if (y < 1 || y > 9999) break;
						int m = ParseDateSegment(date.Slice(4, 2));
						if (m < 1 || m > 12) break;
						int d = ParseDateSegment(date.Slice(6, 2));
						if (d < 1 || d > 31) break;
						int h = ParseDateSegment(date.Slice(8, 2));
						if (h < 0 || h > 23) break;
						int n = ParseDateSegment(date.Slice(10, 2));
						if (n < 0 || n > 59) break;
						int s = ParseDateSegment(date.Slice(12, 2));
						if (s < 0 || s > 59) break;
						result = Instant.FromUtc(y, m, d, h, n, s);
						return true;
					}
					case 17:
					{ // YYYYMMDDHHMMSSFFF -> YYYY/MM/DD HH:MM:SS.FFF
						int y = ParseDateSegment(date.Slice(0, 4));
						if (y < 1 || y > 9999) break;
						int m = ParseDateSegment(date.Slice(4, 2));
						if (m < 1 || m > 12) break;
						int d = ParseDateSegment(date.Slice(6, 2));
						if (d < 1 || d > 31) break;
						int h = ParseDateSegment(date.Slice(8, 2));
						if (h < 0 || h > 23) break;
						int n = ParseDateSegment(date.Slice(10, 2));
						if (n < 0 || n > 59) break;
						int s = ParseDateSegment(date.Slice(12, 2));
						if (s < 0 || s > 59) break;
						int f = ParseDateSegment(date.Slice(14, 3));
						result = Instant.FromUtc(y, m, d, h, n, s) + Duration.FromMilliseconds(f);
						return true;
					}
				}
			}
			catch (FormatException)
			{ // Dommage! La cagnotte est remise à la fois prochaine...
				if (throwsFail) throw;
				return false;
			}
			catch (ArgumentOutOfRangeException)
			{ // Pb sur un DateTime avec des dates invalides (31 février, ...)
				if (throwsFail) throw;
				return false;
			}
			if (throwsFail) throw new FormatException("Date must contains only digits");
			return false;
		}

		/// <summary>Convertit une heure "human friendly" en DateTime: "11","11h","11h00","11:00" -> {11:00:00.000}</summary>
		/// <param name="time">Chaîne contenant l'heure à convertir</param>
		/// <returns>Object DateTime contenant l'heure. La partie "date" est fixée à aujourd'hui</returns>
		[Pure]
		public static DateTime ParseTime(string time)
		{
			Contract.NotNullOrEmpty(time);

			time = time.ToLowerInvariant();

			int hour;
			int minute = 0;
			int second = 0;

			int p = time.IndexOf('h');
			if (p > 0)
			{
				hour = short.Parse(time[..p], NumberStyles.Integer, CultureInfo.InvariantCulture);
				if (p + 1 >= time.Length)
				{
					minute = 0;
				}
				else
				{
					minute = short.Parse(time[(p + 1)..], NumberStyles.Integer, CultureInfo.InvariantCulture);
				}
			}
			else
			{
				p = time.IndexOf(':');
				if (p > 0)
				{
					hour = short.Parse(time[..p], NumberStyles.Integer, CultureInfo.InvariantCulture);
					if (p + 1 >= time.Length)
					{
						minute = 0;
					}
					else
					{
						minute = short.Parse(time[(p + 1)..], NumberStyles.Integer, CultureInfo.InvariantCulture);
					}
				}
				else
				{
					hour = short.Parse(time, NumberStyles.Integer, CultureInfo.InvariantCulture);
				}
			}
			var d = DateTime.Today;
			return new DateTime(d.Year, d.Month, d.Day, hour, minute, second, 0);
		}

		#endregion

		#region Log Helpers...

		/// <summary>Conversion rapide d'une l'heure courante</summary>
		/// <param name="time">Heure à convertir</param>
		/// <returns>"hh:mm:ss.fff"</returns>
		[Pure]
		public static string FastFormatTime(DateTime time)
		{
			unsafe
			{
				// on alloue notre buffer sur la stack
				char* buffer = stackalloc char[16];
				FastFormatTimeUnsafe(buffer, time);
				return new string(buffer, 0, 12);
			}
		}

		/// <summary>Conversion rapide d'une l'heure courante (<c>hh:mm:ss.fff</c>)</summary>
		/// <param name="time">Heure à convertir</param>
		/// <param name="buffer">Buffer a utiliser pour le formattage</param>
		/// <param name="result">Si la fonction retourne <see langword="true"/>, contient le literal <c>hh:mm:ss.fff</c> formatté</param>
		/// <returns>Retourne <see langword="true"/> si le buffer était assez grand, <see langword="false"/> s'il fait moins de 12 chars</returns>
		public static bool TryFastFormatTime(DateTime time, Span<char> buffer, out ReadOnlySpan<char> result)
		{
			if (buffer.Length < 12)
			{
				result = default;
				return false;
			}

			unsafe
			{
				fixed (char* ptr = buffer)
				{
					FastFormatShortTimeUnsafe(ptr, time);
				}
				result = buffer.Slice(0, 12);
				return true;
			}
		}

		/// <summary>Conversion rapide d'une l'heure courante</summary>
		/// <param name="ptr">Pointer vers un buffer d'au moins 12 caractères</param>
		/// <param name="time">Heure à convertir</param>
		/// <returns>"hh:mm:ss.fff"</returns>
		public static unsafe void FastFormatTimeUnsafe(char* ptr, DateTime time)
		{
			// cf: http://geekswithblogs.net/akraus1/archive/2006/04/23/76146.aspx
			// cf: http://blogs.extremeoptimization.com/jeffrey/archive/2006/04/26/13824.aspx

			long ticks = time.Ticks;
			
			// Calculate values by getting the ms values first and do then
			// shave off the hour minute and second values with multiplications
			// and bit shifts instead of simple but expensive divisions.

			int ms = (int) ((ticks / 10000) % 86400000); // Get daytime in ms which does fit into an int
			int hour = (int) (Math.BigMul(ms >> 7, 9773437) >> 38); // well ... it works
			ms -= 3600000 * hour;
			int minute = (int) ((Math.BigMul(ms >> 5, 2290650)) >> 32);
			ms -= 60000 * minute;
			int second = ((ms >> 3) * 67109) >> 23;
			ms -= 1000 * second;

			// Hour
			int temp = (hour * 13) >> 7;  // 13/128 is nearly the same as /10 for values up to 65
			*ptr++ = (char) (temp + '0');
			*ptr++ = (char) (hour - 10 * temp + '0'); // Do subtract to get remainder instead of doing % 10
			*ptr++ = ':';

			// Minute
			temp = (minute * 13) >> 7;   // 13/128 is nearly the same as /10 for values up to 65
			*ptr++ = (char) (temp + '0');
			*ptr++ = (char) (minute - 10 * temp + '0'); // Do subtract to get remainder instead of doing % 10
			*ptr++ = ':';

			// Second
			temp = (second * 13) >> 7; // 13/128 is nearly the same as /10 for values up to 65
			*ptr++ = (char) (temp + '0');
			*ptr++ = (char) (second - 10 * temp + '0');
			*ptr++ = '.';

			// Millisecond
			temp = (ms * 41) >> 12;   // 41/4096 is nearly the same as /100
			*ptr++ = (char) (temp + '0');

			ms -= 100 * temp;
			temp = (ms * 205) >> 11;  // 205/2048 is nearly the same as /10
			*ptr++ = (char) (temp + '0');

			ms -= 10 * temp;
			*ptr = (char) (ms + '0');
		}

		/// <summary>Conversion rapide d'une l'heure courante</summary>
		/// <param name="time">Heure à convertir</param>
		/// <returns>"hh:mm:ss"</returns>
		[Pure]
		public static string FastFormatShortTime(DateTime time)
		{
			unsafe
			{
				// on alloue notre buffer sur la stack
				char* buffer = stackalloc char[8];
				FastFormatShortTimeUnsafe(buffer, time);
				return new string(buffer, 0, 8);
			}
		}

		/// <summary>Conversion rapide d'une l'heure courante (<c>hh:mm:ss</c>)</summary>
		/// <param name="time">Heure à convertir</param>
		/// <param name="buffer">Buffer a utiliser pour le formattage</param>
		/// <param name="result">Si la fonction retourne <see langword="true"/>, contient le literal <c>hh:mm:ss</c> formatté</param>
		/// <returns>Retourne <see langword="true"/> si le buffer était assez grand, <see langword="false"/> s'il fait moins de 8 chars</returns>
		public static bool TryFastFormatShortTime(DateTime time, Span<char> buffer, out ReadOnlySpan<char> result)
		{
			if (buffer.Length < 8)
			{
				result = default;
				return false;
			}

			unsafe
			{
				fixed (char* ptr = buffer)
				{
					FastFormatShortTimeUnsafe(ptr, time);
				}
				result = buffer.Slice(0, 8);
				return true;
			}
		}

		/// <summary>Conversion rapide d'une l'heure courante</summary>
		/// <param name="ptr">Pointer vers un buffer d'au moins 8 caractères</param>
		/// <param name="time">Heure à convertir</param>
		/// <returns>"hh:mm:ss"</returns>
		public static unsafe void FastFormatShortTimeUnsafe(char* ptr, DateTime time)
		{
			// cf: http://geekswithblogs.net/akraus1/archive/2006/04/23/76146.aspx
			// cf: http://blogs.extremeoptimization.com/jeffrey/archive/2006/04/26/13824.aspx

			long ticks = time.Ticks;
			
			// Calculate values by getting the ms values first and do then
			// shave off the hour minute and second values with multiplications
			// and bit shifts instead of simple but expensive divisions.

			int ms = (int) ((ticks / 10000) % 86400000); // Get daytime in ms which does fit into an int
			int hour = (int) (Math.BigMul(ms >> 7, 9773437) >> 38); // well ... it works
			ms -= 3600000 * hour;
			int minute = (int) ((Math.BigMul(ms >> 5, 2290650)) >> 32);
			ms -= 60000 * minute;
			int second = ((ms >> 3) * 67109) >> 23;

			// Hour
			int temp = (hour * 13) >> 7;  // 13/128 is nearly the same as /10 for values up to 65
			*ptr++ = (char) (temp + '0');
			*ptr++ = (char) (hour - 10 * temp + '0'); // Do subtract to get remainder instead of doing % 10
			*ptr++ = ':';

			// Minute
			temp = (minute * 13) >> 7;   // 13/128 is nearly the same as /10 for values up to 65
			*ptr++ = (char) (temp + '0');
			*ptr++ = (char) (minute - 10 * temp + '0'); // Do subtract to get remainder instead of doing % 10
			*ptr++ = ':';

			// Second
			temp = (second * 13) >> 7; // 13/128 is nearly the same as /10 for values up to 65
			*ptr++ = (char) (temp + '0');
			*ptr = (char) (second - 10 * temp + '0');
		}

		/// <summary>Conversion rapide de la date courante au format international (YYYY-MM-DD)</summary>
		/// <param name="date">Date à convertir</param>
		/// <returns>"YYYY-MM-DD"</returns>
		[Pure]
		public static string FastFormatDate(DateTime date)
		{
			// ATTENTION: cette fonction ne peut formatter que des années entre 1000 et 2999 !
			unsafe
			{
				char* buffer = stackalloc char[12]; // on n'utilise que 10 chars
				FastFormatDateUnsafe(buffer, date);
				return new string(buffer, 0, 10);
			}
		}

		/// <summary>Conversion rapide de la date courante au format international (YYYY-MM-DD)</summary>
		/// <param name="date">Date à convertir</param>
		/// <param name="buffer">Buffer a utiliser pour le formattage</param>
		/// <param name="result">Si la fonction retourne <see langword="true"/>, contient le literal <c>YYYY-MM-DD</c> formatté</param>
		/// <returns>Retourne <see langword="true"/> si le buffer était assez grand, <see langword="false"/> s'il fait moins de 12 chars</returns>
		public static bool TryFastFormatDate(DateTime date, Span<char> buffer, out ReadOnlySpan<char> result)
		{
			if (buffer.Length < 12)
			{
				result = default;
				return false;
			}

			unsafe
			{
				fixed (char* ptr = buffer)
				{
					FastFormatDateUnsafe(ptr, date);
				}
				result = buffer.Slice(0, 12);
				return true;
			}
		}

		/// <summary>Conversion rapide de la date courante au format international (YYYY-MM-DD)</summary>
		/// <param name="ptr">Pointer vers un buffer d'au moins 10 caractères</param>
		/// <param name="date">Date à convertir</param>
		/// <returns>"YYYY-MM-DD"</returns>
		public static unsafe void FastFormatDateUnsafe(char* ptr, DateTime date)
		{
			int y = date.Year;
			int m = date.Month;
			int d = date.Day;

			#region YEAR
			// on va d'abord afficher le 1xxx ou le 2xxx (désolé si vous êtes en l'an 3000 !)
			if (y < 2000)
			{
				ptr[0] = '1';
				y -= 1000;
			}
			else
			{
				ptr[0] = '2';
				y -= 2000; // <-- Y3K BUG HERE
			}
			// ensuite pour les centaines, on utilise la même technique que pour formatter les millisecondes
			int temp = (y * 41) >> 12;   // 41/4096 is nearly the same as /100
			ptr[1] = (char) (temp + '0');

			y -= 100 * temp;
			temp = (y * 205) >> 11;  // 205/2048 is nearly the same as /10
			ptr[2] = (char) (temp + '0');

			y -= 10 * temp;
			ptr[3] = (char) (y + '0');
			ptr[4] = '-';
			#endregion

			#region MONTH
			temp = (m * 13) >> 7;  // 13/128 is nearly the same as /10 for values up to 65
			ptr[5] = (char) (temp + '0');
			ptr[6] = (char) (m - 10 * temp + '0'); // Do subtract to get remainder instead of doing % 10
			ptr[7] = '-';
			#endregion

			#region DAY
			temp = (d * 13) >> 7;   // 13/128 is nearly the same as /10 for values up to 65
			ptr[8] = (char) (temp + '0');
			ptr[9] = (char) (d - 10 * temp + '0'); // Do subtract to get remainder instead of doing % 10
			#endregion
		}

		/// <summary>Conversion rapide de la date courante au format court (MM-DD)</summary>
		/// <param name="date">Date à convertir</param>
		/// <returns>"MM-DD"</returns>
		[Pure]
		public static string FastFormatShortDate(DateTime date)
		{
			unsafe
			{
				char* buffer = stackalloc char[8]; // on n'utilise que 5 chars
				FastFormatShortDateUnsafe(buffer, date);
				return new string(buffer, 0, 5);
			}
		}

		/// <summary>Conversion rapide de la date courante au format court (MM-DD)</summary>
		/// <param name="date">Date à convertir</param>
		/// <param name="buffer">Buffer a utiliser pour le formattage</param>
		/// <param name="result">Si la fonction retourne <see langword="true"/>, contient le literal <c>MM-DD</c> formatté</param>
		/// <returns>Retourne <see langword="true"/> si le buffer était assez grand, <see langword="false"/> s'il fait moins de 5 chars</returns>
		public static bool TryFastFormatShortDate(DateTime date, Span<char> buffer, out ReadOnlySpan<char> result)
		{
			if (buffer.Length < 5)
			{
				result = default;
				return false;
			}

			unsafe
			{
				fixed (char* ptr = buffer)
				{
					FastFormatDateUnsafe(ptr, date);
				}
				result = buffer.Slice(0, 5);
				return true;
			}
		}

		/// <summary>Conversion rapide de la date courante au format court (MM-DD)</summary>
		/// <param name="ptr">Pointer vers un buffer d'au moins 5 caractères</param>
		/// <param name="date">Date à convertir</param>
		/// <returns>"MM-DD"</returns>
		public static unsafe void FastFormatShortDateUnsafe(char* ptr, DateTime date)
		{
			int m = date.Month;
			int d = date.Day;

			int temp = (m * 13) >> 7;  // 13/128 is nearly the same as /10 for values up to 65
			*ptr++ = (char) (temp + '0');
			*ptr++ = (char) (m - 10 * temp + '0'); // Do subtract to get remainder instead of doing % 10
			*ptr++ = '-';

			temp = (d * 13) >> 7;   // 13/128 is nearly the same as /10 for values up to 65
			*ptr++ = (char) (temp + '0');
			*ptr = (char) (d - 10 * temp + '0'); // Do subtract to get remainder instead of doing % 10
		}

		/// <summary>Formate une durée en chaîne compacte ("30s", "45min", "8h32m")</summary>
		/// <param name="duration">Durée à formater</param>
		/// <returns>Forme affichable de la durée (minutes arrondies au supérieur)</returns>
		[Pure]
		public static string FormatDuration(TimeSpan duration)
		{
			long d = (long) Math.Ceiling(duration.TotalSeconds);
			if (d == 0) return "0s"; //TODO: WMLiser KTL.FormatDuration!
			if (d <= 60) return d.ToString(CultureInfo.InvariantCulture) + "s";
			if (d < 3600) return "~" + ((long) Math.Round(duration.TotalMinutes + 0.2, MidpointRounding.AwayFromZero)).ToString(CultureInfo.InvariantCulture) + "min";
			if (d <= 86400) return ((long) Math.Floor(duration.TotalHours)).ToString(CultureInfo.InvariantCulture) + "h" + ((duration.Minutes >= 1) ? (((long) Math.Ceiling((double) duration.Minutes)).ToString("D2") + "m") : String.Empty);
			if (d < 259200) return "~" + ((long) Math.Round(duration.TotalHours, MidpointRounding.AwayFromZero)).ToString(CultureInfo.InvariantCulture) + "h";
			return "~" + ((long) Math.Floor(duration.TotalDays)).ToString(CultureInfo.InvariantCulture) + "d" + (duration.Hours > 0 ? duration.Hours + "h" : "");
		}

		#endregion

	}

}
