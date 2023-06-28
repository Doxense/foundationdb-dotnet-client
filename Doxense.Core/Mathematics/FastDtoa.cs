#region Copyright Doxense 2014-2022
//
// All rights are reserved. Reproduction or transmission in whole or in part, in
// any form or by any means, electronic, mechanical or otherwise, is prohibited
// without the prior written consent of the copyright owner.
//
#endregion

namespace Doxense.Mathematics
{
	using System;
	using System.Diagnostics;
	using System.Globalization;
	using System.Runtime.CompilerServices;
	using System.Runtime.InteropServices;
	using Doxense.Diagnostics.Contracts;
	using JetBrains.Annotations;

	/// <summary>
	/// This "Do It Yourself Floating Point" class implements a floating-point number
	/// with a uint64 significand and an int exponent. Normalized DiyFp numbers will
	/// have the most significant bit of the significand set.
	/// Multiplication and Subtraction do not normalize their results.
	/// DiyFp are not designed to contain special doubles (NaN and Infinity).
	/// </summary>
	[DebuggerDisplay("F={F}, E={E}")]
	public struct DiyFp
	{
		public const int SignificandSize = 64;

		public static DiyFp Zero => default(DiyFp);

		/// <summary>Significand (64-bit)</summary>
		public ulong F;

		/// <summary>Exponent</summary>
		public int E;

		public DiyFp(ulong significant, int exponent)
		{
			this.F = significant;
			this.E = exponent;
		}

		public void Subtract(DiyFp other)
		{
			Paranoid.Requires(this.E == other.E && this.F >= other.F, "The exponents of both numbers must be the same and the significand of this must be bigger than the significand of other.");
			this.F -= other.F;
		}

		public void Multiply(DiyFp other)
		{
			const ulong M32 = 0xFFFFFFFFul;

			ulong f;

			f = this.F;
			ulong a = f >> 32;
			ulong b = f & M32;
			f = other.F;
			ulong c = f >> 32;
			ulong d = f & M32;

			ulong ac = a * c;
			ulong bc = b * c;
			ulong ad = a * d;
			ulong bd = b * d;
			ulong tmp = (bd >> 32) + (ad & M32) + (bc & M32);
			// By adding 1U << 31 to tmp we round the final result.
			// Halfway cases will be round up.
			tmp += 1ul << 31;

			this.F = ac + (ad >> 32) + (bc >> 32) + (tmp >> 32);
			this.E += other.E + 64;
		}

		public void Normalize()
		{
			Normalize(this.F, this.E, out this.F, out this.E);
		}

		private static void Normalize(ulong significand, int exponent, out ulong outSignificand, out int outExponent)
		{
			Paranoid.Requires(significand != 0);

			// This method is mainly called for normalizing boundaries. In general
			// boundaries need to be shifted by 10 bits. We thus optimize for this
			// case.
			const ulong TEN_MS_BITS = 0xFFC0000000000000ul;
			const ulong UINT64_MSB = 0x8000000000000000ul;
			while ((significand & TEN_MS_BITS) == 0)
			{
				significand <<= 10;
				exponent -= 10;
			}
			while ((significand & UINT64_MSB) == 0)
			{
				significand <<= 1;
				exponent--;
			}
			outSignificand = significand;
			outExponent = exponent;
		}

		public static DiyFp Normalized(ulong significand, int exponent)
		{
			Normalize(significand, exponent, out significand, out exponent);
			return new DiyFp(significand, exponent);
		}

		public static DiyFp Normalized(ref DiyFp a)
		{
			var result = a;
			result.Normalize();
			return result;
		}

		public static DiyFp Minus(ref DiyFp a, ref DiyFp b)
		{
			var result = a;
			result.Subtract(b);
			return result;
		}

		/// <summary>Same as <see cref="Minus(ref DiyFp, ref DiyFp)"/> but only returns the significand</summary>
		public static ulong MinusSignificand(ref DiyFp a, ref DiyFp b)
		{
			Paranoid.Requires(a.E == b.E && a.F >= b.F, "The exponents of both numbers must be the same and the significand of a must be bigger than the significand of b.");
			return a.F - b.F;
		}

		public static DiyFp Times(ref DiyFp a, ref DiyFp b)
		{
			var result = a;
			result.Multiply(b);
			return result;
		}
	}

	[DebuggerDisplay("M={Significand}, E={Exponent}, S={Sign}")]
	[StructLayout(LayoutKind.Explicit)]
	public struct DiyDouble
	{
		public const ulong SignMask = 0x8000000000000000ul;
		public const ulong ExponentMask = 0x7FF0000000000000ul;
		public const ulong SignificandMask = 0x000FFFFFFFFFFFFFul;
		public const ulong HiddenBit = 0x0010000000000000ul;
		public const int PhysicalSignificandSize = 52; // Excludes the hidden bit.
		public const int SignificandSize = 53;

		internal const int ExponentOffset = 0x3FF; // = 1023
		internal const int ExponentBias = ExponentOffset + PhysicalSignificandSize; // = 1075
		internal const int DenormalExponent = -ExponentBias + 1; // = -1074
		internal const int MaxExponent = 0x7FF - ExponentBias; // = 972
		internal const ulong Infinity = 0x7FF0000000000000ul;
		internal const ulong NaN = 0x7FF8000000000000ul;

		//note: this only works on LE hosts!

		[FieldOffset(0)]
		public ulong Raw;
		[FieldOffset(0)]
		public double Value;

		public DiyDouble(double d)
			: this()
		{
			this.Value = d;
		}

		public DiyDouble(ulong r)
			: this()
		{
			this.Raw = r;
		}

		public static implicit operator DiyDouble(double v)
		{
			var d = default(DiyDouble);
			d.Value = v;
			return d;
		}

		public static implicit operator DiyDouble(ulong r)
		{
			var d = default(DiyDouble);
			d.Raw = r;
			return d;
		}

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static ulong ToRaw(double f)
		{
			var d = default(DiyDouble);
			d.Value = f;
			return d.Raw;
		}

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static double FromRaw(ulong raw)
		{
			var d = default(DiyDouble);
			d.Raw = raw;
			return d.Value;
		}


		/// <summary>Returns the next greater double.</summary>
		/// <remarks>Returns +infinity on input +infinity.</remarks>
		[Pure]
		public double NextDouble()
		{
			if (this.Raw == Infinity) return double.PositiveInfinity;
			if (this.Sign >= 0) return new DiyDouble(this.Raw + 1).Value;
			if (this.Significand == 0) return 0.0;
			return new DiyDouble(this.Raw - 1).Value;
		}

		/// <summary>Returns the next greater double.</summary>
		/// <remarks>Returns +infinity on input +infinity.</remarks>
		[Pure]
		public static double NextDouble(double value)
		{
			return new DiyDouble(value).NextDouble();
		}

		/// <summary>Returns the previous smaller double.</summary>
		/// <remarks>Returns -infinity on input -infinity.</remarks>
		[Pure]
		public double PreviousDouble()
		{
			if (this.Raw == (Infinity | SignMask)) return double.NegativeInfinity;
			if (this.Sign < 0) return new DiyDouble(this.Raw + 1).Value;
			if (this.Significand == 0) return -0.0;
			return new DiyDouble(this.Raw - 1).Value;
		}

		/// <summary>Returns the previous smaller double.</summary>
		/// <remarks>Returns -infinity on input -infinity.</remarks>
		[Pure]
		public static double PreviousDouble(double value)
		{
			return new DiyDouble(value).PreviousDouble();
		}

		[Pure]
		public DiyFp ToNormalized()
		{
			Paranoid.Requires(this.Value > 0d, "The value encoded by this Double must be strictly greater than 0.");

			ulong f = this.Significand;
			int e = this.Exponent;

			// The current double could be a denormal.
			while ((f & HiddenBit) == 0)
			{
				f <<= 1;
				e--;
			}

			// Do the final shifts in one go.
			f <<= DiyFp.SignificandSize - SignificandSize;
			e -= DiyFp.SignificandSize - SignificandSize;
			return new DiyFp(f, e);
		}

		public int Exponent
		{
			[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
			get
			{
				return this.IsDenormal
					? DenormalExponent
					: (int)((this.Raw & ExponentMask) >> PhysicalSignificandSize) - ExponentBias;
			}
		}

		public int Sign
		{
			[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
			get { return (this.Raw & SignMask) == 0 ? 1 : -1; }
		}

		public ulong Significand
		{
			[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
			get
			{
				ulong r = this.Raw;
				return (r & SignificandMask) | ((r & ExponentMask) == 0 ? 0 : HiddenBit);
			}
		}

		public bool IsDenormal
		{
			[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
			get
			{
				return (this.Raw & ExponentMask) == 0;
			}
		}

		public bool IsSpecial
		{
			[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
			get
			{
				// We consider denormals not to be special.
				// Hence only Infinity and NaN are special.
				return (this.Raw & ExponentMask) == ExponentMask;
			}
		}

		public bool IsNaN
		{
			[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
			get
			{
				ulong r = this.Raw;
				return (r & ExponentMask) == ExponentMask
					&& (r & SignificandMask) != 0;
			}
		}

		public bool IsInfinite
		{
			[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
			get
			{
				ulong r = this.Raw;
				return (r & ExponentMask) == ExponentMask
					&& (r & SignificandMask) == 0;
			}
		}

		public bool IsInteger
		{
			[Pure]
			get
			{
				// see http://stackoverflow.com/a/1944214

				ulong r = this.Raw;
				if (r == 0) return true; // 0 is a denormal, but is an integer

				int e = (int)((r & ExponentMask) >> PhysicalSignificandSize) - ExponentOffset;
				// - E == 1024: special number (nan, posinf, neginf, ...)
				// - E >= 52 : too large to hold a decimal value
				// - E < 0 : too small to hold a magnitude greater than 1.0
				return e >= 0 && e < (ExponentOffset + 1) && (e >= PhysicalSignificandSize || ((r << e) & SignificandMask) == 0);
			}
		}

		public bool IsNegative
		{
			[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
			get { return (this.Raw & SignMask) != 0; }
		}

		[Pure]
		public DiyFp UpperBoundary()
		{
			return new DiyFp(this.Significand * 2 + 1, this.Exponent - 1);
		}

		[Pure]
		public void NormalizedBoundaries(out DiyFp minus, out DiyFp plus)
		{
			Paranoid.Requires(this.Value > 0.0, "The value encoded by this Double must be greater than 0.");

			DiyFp v = ToDiyFp();

			DiyFp mPlus = DiyFp.Normalized((v.F << 1) + 1, v.E - 1);
			DiyFp mMinus;
			if (LowerBoundaryIsCloser())
			{
				mMinus = new DiyFp((v.F << 2) - 1, v.E - 2);
			}
			else
			{
				mMinus = new DiyFp((v.F << 1) - 1, v.E - 1);
			}
			mMinus.F <<= mMinus.E - mPlus.E;
			mMinus.E = mPlus.E;

			minus = mMinus;
			plus = mPlus;
		}

		[Pure]
		private bool LowerBoundaryIsCloser()
		{
			return (this.Raw & SignificandMask) == 0 && this.Exponent != DenormalExponent;
		}

		public DiyFp ToDiyFp()
		{
			Paranoid.Requires(this.Sign > 0, "The value encoded by this Double must be greater or equal to +0.0.");
			Paranoid.Requires(!this.IsSpecial, "It must not be special (infinity, or NaN).");

			return new DiyFp(this.Significand, this.Exponent);
		}

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public ulong ToUInt64()
		{
			return this.Raw;
		}

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public double ToDouble()
		{
			return this.Value;
		}

		public override string ToString()
		{
			if (this.IsSpecial)
			{
				return String.Format(
					CultureInfo.InvariantCulture,
					"DiyDouble[0x{0:X16}] = {1} [SPECIAL]",
					this.Raw,
					IsNaN ? "NaN" : this.IsInfinite ? (this.Sign > 0 ? "+Infinity" : "-Infinity") : "Special!"
				);
			}

			if (this.IsDenormal)
			{
				return String.Format(
					CultureInfo.InvariantCulture,
					"DiyDouble[0x{0:X16}] = {1}{2} [DENORMAL] = {3:R} {4}",
					this.Raw,
					this.Sign > 0 ? "+" : "-",
					Convert.ToString((long)this.Significand, 2),
					this.Value,
					this.IsInteger ? "(int)" : "(dec)"
				);
			}

			return String.Format(
				CultureInfo.InvariantCulture,
				"DiyDouble[0x{0:X16}] = {1}{2} x 2^{3} = {4:R} {5}",
				this.Raw,
				this.Sign > 0 ? "+" : "-",
				Convert.ToString((long)this.Significand, 2),
				this.Exponent,
				this.Value,
				this.IsInteger ? "(int)" : "(dec)"
			);
		}

	}

	[DebuggerDisplay("M={Significand}, E={Exponent}, S={Sign}")]
	[StructLayout(LayoutKind.Explicit)]
	public struct DiySingle
	{

		public const uint SignMask = 0x80000000u;
		public const uint ExponentMask = 0x7F800000u;
		public const uint SignificandMask = 0x007FFFFFu;
		public const uint HiddenBit = 0x00800000u;
		public const int PhysicalSignificandSize = 23; // Excludes the hidden bit.
		public const int SignificandSize = 24;

		internal const int ExponentOffset = 0x7F;
		internal const int ExponentBias = ExponentOffset + PhysicalSignificandSize;
		internal const int DenormalExponent = -ExponentBias + 1;
		internal const int MaxExponent = 0xFF - ExponentBias;
		internal const uint Infinity = 0x7F800000u;
		internal const uint NaN = 0x7FC00000u;

		//note: this only works on LE hosts!

		[FieldOffset(0)]
		public uint Raw;

		[FieldOffset(0)]
		public float Value;

		public DiySingle(float d)
			: this()
		{
			this.Value = d;
		}

		public DiySingle(uint r)
			: this()
		{
			this.Raw = r;
		}

		public static implicit operator DiySingle(float v)
		{
			var d = default(DiySingle);
			d.Value = v;
			return d;
		}

		public static implicit operator DiySingle(uint r)
		{
			var d = default(DiySingle);
			d.Raw = r;
			return d;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static uint ToRaw(float f)
		{
			var d = default(DiySingle);
			d.Value = f;
			return d.Raw;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static float FromRaw(uint raw)
		{
			var d = default(DiySingle);
			d.Raw = raw;
			return d.Value;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static uint GetRawExponent(uint raw)
		{
			return (raw & ExponentMask) >> PhysicalSignificandSize;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static uint GetRawSignificand(uint raw)
		{
			return raw & SignificandMask;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool GetRawSign(uint raw)
		{
			return (raw & SignMask) != 0;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static float Recombine(uint rawSignificand, uint rawExponent, bool sign)
		{
			var d = default(DiySingle);
			d.Raw = (rawSignificand & SignificandMask) | (rawExponent << PhysicalSignificandSize) | (sign ? SignMask : 0);
			return d.Value;
		}

		public int Exponent
		{
			[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
			get
			{
				return this.IsDenormal
					? DenormalExponent
					: (int)((this.Raw & ExponentMask) >> PhysicalSignificandSize) - ExponentBias;
			}
		}

		public int Sign
		{
			[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
			get { return (this.Raw & SignMask) == 0 ? 1 : -1; }
		}

		public uint Significand
		{
			[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
			get
			{
				uint r = this.Raw;
				return (r & SignificandMask) | ((r & ExponentMask) == 0 ? 0 : HiddenBit);
			}
		}

		public bool IsDenormal
		{
			[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
			get
			{
				return (this.Raw & ExponentMask) == 0;
			}
		}

		public bool IsSpecial
		{
			[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
			get
			{
				// We consider denormals not to be special.
				// Hence only Infinity and NaN are special.
				return (this.Raw & ExponentMask) == ExponentMask;
			}
		}

		public bool IsNaN
		{
			[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
			get
			{
				ulong r = this.Raw;
				return (r & ExponentMask) == ExponentMask
					&& (r & SignificandMask) != 0;
			}
		}

		public bool IsInfinite
		{
			[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
			get
			{
				ulong r = this.Raw;
				return (r & ExponentMask) == ExponentMask
					&& (r & SignificandMask) == 0;
			}
		}

		public bool IsInteger
		{
			[Pure]
			get
			{
				// see http://stackoverflow.com/a/1944214

				ulong r = this.Raw;
				if (r == 0) return true; // 0 is a denormal, but is an integer

				int e = (int)((r & ExponentMask) >> PhysicalSignificandSize) - ExponentOffset;
				// - E == 128: special number (nan, posinf, neginf, ...)
				// - E >= 23 : too large to hold a decimal value
				// - E < 0 : too small to hold a magnitude greater than 1.0
				return e >= 0 && e < (ExponentOffset + 1) && (e >= PhysicalSignificandSize || ((r << e) & SignificandMask) == 0);
			}
		}

		public bool IsNegative
		{
			[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
			get { return (this.Raw & SignMask) != 0; }
		}

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public ulong ToUInt32()
		{
			return this.Raw;
		}

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public double ToSingle()
		{
			return this.Value;
		}

		public override string ToString()
		{
			if (this.IsSpecial)
			{
				return String.Format(
					CultureInfo.InvariantCulture,
					"DiySingle[0x{0:X8}] = {1} [SPECIAL]",
					this.Raw,
					this.IsNaN ? "NaN" : this.IsInfinite ? (this.Sign > 0 ? "+Infinity" : "-Infinity") : "Special!"
				);
			}

			if (this.IsDenormal)
			{
				return String.Format(
					CultureInfo.InvariantCulture,
					"DiySingle[0x{0:X8}] = {1}{2} [DENORMAL] = {3:R} {4}",
					this.Raw,
					this.Sign > 0 ? "+" : "-",
					Convert.ToString((int)this.Significand, 2),
					this.Value,
					this.IsInteger ? "(int)" : "(dec)"
				);
			}

			return String.Format(
				CultureInfo.InvariantCulture,
				"DiySingle[0x{0:X8}] = {1}{2} x 2^{3} = {4:R} {5}",
				this.Raw,
				this.Sign > 0 ? "+" : "-",
				Convert.ToString((int)this.Significand, 2),
				this.Exponent,
				this.Value,
				this.IsInteger ? "(int)" : "(dec)"
			);
		}

	}

	public static class FastDtoa
	{
		// Portage .NET basé sur https://code.google.com/p/double-conversion/
		#region Original License (New BSD)
		// Copyright 2006-2011, the V8 project authors. All rights reserved.
		// Redistribution and use in source and binary forms, with or without
		// modification, are permitted provided that the following conditions are
		// met:

		// 	* Redistributions of source code must retain the above copyright
		// 	  notice, this list of conditions and the following disclaimer.
		// 	* Redistributions in binary form must reproduce the above
		// 	  copyright notice, this list of conditions and the following
		// 	  disclaimer in the documentation and/or other materials provided
		// 	  with the distribution.
		// 	* Neither the name of Google Inc. nor the names of its
		// 	  contributors may be used to endorse or promote products derived
		// 	  from this software without specific prior written permission.

		// THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS
		// "AS IS" AND ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT
		// LIMITED TO, THE IMPLIED WARRANTIES OF MERCHANTABILITY AND FITNESS FOR
		// A PARTICULAR PURPOSE ARE DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT
		// OWNER OR CONTRIBUTORS BE LIABLE FOR ANY DIRECT, INDIRECT, INCIDENTAL,
		// SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES (INCLUDING, BUT NOT
		// LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES; LOSS OF USE,
		// DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND ON ANY
		// THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT
		// (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE
		// OF THIS SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
		#endregion

		internal const int MinimalTargetExponent = -60;
		internal const int MaximalTargetExponent = -32;
		internal const int DecimalInShortestLow = -6;
		internal const int DecimalInShortestHigh = 21;
		internal const int Base10MaximalLength = 17;

		private static unsafe bool RoundWeed(char* buffer, int cursor, ulong distanceTooHighW, ulong unsafeInterval, ulong rest, ulong tenKappa, ulong unit)
		{
			Contract.Debug.Requires(buffer != null && cursor > 0);
			ulong smallDistance = distanceTooHighW - unit;
			ulong bigDistance = distanceTooHighW + unit;

			Paranoid.Requires(rest <= unsafeInterval);
			while (rest < smallDistance &&
				unsafeInterval - rest >= tenKappa &&
				(rest + tenKappa < smallDistance ||
				 smallDistance - rest >= rest + tenKappa - smallDistance))
			{
				if (cursor == 0) return false;
				buffer[cursor - 1]--;
				rest += tenKappa;
			}

			if (rest < bigDistance &&
				unsafeInterval - rest >= tenKappa &&
				(rest + tenKappa < bigDistance ||
				 bigDistance - rest > rest + tenKappa - bigDistance))
			{
				return false;
			}

			return (2 * unit <= rest) && (rest <= unsafeInterval - 4 * unit);
		}

		private static readonly uint[] SmallPowersOfTen = new uint[] { 0, 1, 10, 100, 1000, 10000, 100000, 1000000, 10000000, 100000000, 1000000000 };

		private static void BiggestPowerTen(uint number, int numberBits, out uint power, out int exponentPlusOne)
		{
			Paranoid.Requires(number < (1u << (numberBits + 1)));
			int exponentPlusOneGuess = ((numberBits + 1) * 1233) >> 12;
			exponentPlusOneGuess++;
			if (number < SmallPowersOfTen[exponentPlusOneGuess])
			{
				exponentPlusOneGuess--;
			}
			power = SmallPowersOfTen[exponentPlusOneGuess];
			exponentPlusOne = exponentPlusOneGuess;
		}

		private static unsafe bool DigitGen(ref DiyFp low, ref DiyFp w, ref DiyFp high, char* buffer, out int length, out int outKappa)
		{
			Contract.Debug.Requires(buffer != null);

			Paranoid.Requires(low.E == w.E && w.E == high.E);
			Paranoid.Requires(low.F + 1 <= high.F - 1);
			Paranoid.Requires(MinimalTargetExponent <= w.E && w.E <= MaximalTargetExponent);

			DiyFp tooLow = new DiyFp(low.F - 1, low.E);
			DiyFp tooHigh = new DiyFp(high.F + 1, high.E);

			ulong unsafeIntervalF = DiyFp.MinusSignificand(ref tooHigh, ref tooLow);

			DiyFp one = new DiyFp(1ul << -w.E, w.E); //TODO: constant => unpack dans deux variables oneE et oneF?
			uint integrals = (uint)(tooHigh.F >> -one.E);
			ulong fractionals = tooHigh.F & (one.F - 1);
			uint divisor;
			int divisorExponentPlusOne;
			BiggestPowerTen(integrals, DiyFp.SignificandSize - (-one.E), out divisor, out divisorExponentPlusOne);

			int kappa = divisorExponentPlusOne;
			char* ptr = buffer;

			while (kappa > 0)
			{
				uint digits = integrals / divisor;
				Paranoid.Assert(digits <= 9);
				*ptr++ = (char)('0' + digits);
				integrals %= divisor;
				--kappa;

				ulong rest = (((ulong)integrals) << -one.E) + fractionals;
				if (rest < unsafeIntervalF)
				{
					// Rounding down (by not emitting the remaining digits) yields a number that lies within the unsafe interval.
					length = (int)(ptr - buffer);
					outKappa = kappa;
					return RoundWeed(buffer, length, DiyFp.MinusSignificand(ref tooHigh, ref w), unsafeIntervalF, rest, ((ulong)divisor) << -one.E, 1);
				}

				divisor /= 10;
			}

			// The integrals have been generated. We are at the point of the decimal separator.
			// In the following loop we simply multiply the remaining digits by 10 and divide by one.
			// We just need to pay attention to multiply associated data (like the interval or 'unit'), too.
			// Note that the multiplication by 10 does not overflow, because w.e >= -60 and thus one.E >= -60.
			Paranoid.Assert(one.E >= -60);
			Paranoid.Assert(fractionals < one.F);
			Paranoid.Assert(0xFFFFFFFFFFFFFFFFul / 10 >= one.F);

			ulong unit = 1;
			while (true)
			{
				fractionals *= 10;
				unit *= 10;
				unsafeIntervalF *= 10;
				// Integer division by one.
				uint digit = (uint)(fractionals >> -one.E);
				Paranoid.Assert(digit <= 9);
				*ptr++ = (char)('0' + digit);
				fractionals &= one.F - 1; // Modulo by one
				kappa--;
				if (fractionals < unsafeIntervalF)
				{
					length = (int)(ptr - buffer);
					outKappa = kappa;
					return RoundWeed(buffer, length, DiyFp.MinusSignificand(ref tooHigh, ref w) * unit, unsafeIntervalF, fractionals, one.F, unit);
				}
			}
		}

		public static string FormatDouble(double v)
		{
			if (v == 0.0) return "0";
			unsafe
			{
				char* buffer = stackalloc char[32];
				int n = ToShortestIeeeNumber(v, buffer);
				return new string(buffer, 0, n);

			}
		}

		public static int FormatDouble(double v, char[] buffer, int offset)
		{
			Contract.NotNull(buffer);
			if (offset < 0) throw new ArgumentOutOfRangeException(nameof(offset));
			if (offset + 20 > buffer.Length) throw new ArgumentException("Buffer is too small", nameof(buffer));

			unsafe
			{
				fixed (char* ptr = &buffer[offset])
				{
					return ToShortestIeeeNumber(v, ptr);
				}
			}
		}

		private static unsafe int ToShortestIeeeNumber(double value, char* output)
		{
			Contract.Debug.Requires(output != null);

			if (((DiyDouble)value).IsSpecial)
			{ // "NaN", "Infinity", "-Infinity"
				return HandleSpecialValues(value, output);
			}

			int decimalPoint;
			bool sign;

			// temp buffer to hold the unformatted decimal digits
			//note: source does +1 for the \0 that we don't need
			const int DecimalRepCapacity = Base10MaximalLength + 1;
			char* chars = stackalloc char[DecimalRepCapacity];

			int decimalRepLength;
			if (!DoubleToAscii(value, chars, DecimalRepCapacity, out sign, out decimalRepLength, out decimalPoint))
			{ // fallback to BCL
				string s = value.ToString("R", NumberFormatInfo.InvariantInfo);
				foreach (char c in s)
				{
					*output++ = c;
				}
				return s.Length;
			}

			int length = 0;
			// ReSharper disable once CompareOfFloatsByEqualityOperator
			if (sign && value != 0.0)
			{
				*output++ = '-';
				length = 1;
			}

			int exponent = decimalPoint - 1;
			if (DecimalInShortestLow <= exponent && exponent < DecimalInShortestHigh)
			{
				length += CreateDecimalRepresentation(chars, decimalRepLength, decimalPoint, Math.Max(0, decimalRepLength - decimalPoint), output);
			}
			else
			{
				length += CreateExponentialRepresentation(chars, decimalRepLength, exponent, output);
			}
			return length;
		}

		private static unsafe bool DoubleToAscii(double v, char* buffer, int bufferLength, out bool sign, out int length, out int point)
		{
			Paranoid.Assert(!new DiyDouble(v).IsSpecial);
			Paranoid.Assert(buffer != null && bufferLength >= 20);

			if (v < 0.0)
			{
				sign = true;
				v = -v;
			}
			else
			{
				sign = false;
			}

			// ReSharper disable once CompareOfFloatsByEqualityOperator
			if (v == 0.0)
			{
				buffer[0] = '0';
				length = 1;
				point = 1;
				return true;
			}

			bool fastWorked = Grisu3(v, buffer, out length, out var decimalExponent);
			if (fastWorked)
			{
				point = length + decimalExponent;
				return true;
			}

			// if the fast dtoa didn't succeed, fallback to the BCL implementation
			length = 0;
			point = 0;
			return false;
		}

		//TODO: implémenter Grisu2 (moins rapide que Grisu3, mais pas de pb de rounding)
		// cf 'milo' => https://github.com/miloyip/dtoa-benchmark/blob/master/src/milo/dtoa_milo.h

		private static unsafe bool Grisu3(double v, char* buffer, out int length, out int decimalExponent)
		{
			Contract.Debug.Requires(buffer != null);

			var d = new DiyDouble(v);
			var w = d.ToNormalized();

			//SHORTEST
			d.NormalizedBoundaries(out var boundaryMinus, out var boundaryPlus);
			Paranoid.Assert(boundaryPlus.E == w.E);

			DiyFp tenMk; // Cached power of ten: 10^-k
			int mk;		 // -k
			int tenMkMinimalBinaryExponent = MinimalTargetExponent - (w.E + DiyFp.SignificandSize);
			int tenMkMaximalBinaryExponent = MaximalTargetExponent - (w.E + DiyFp.SignificandSize);
			GetCachedPowerForBinaryExponentRange(tenMkMinimalBinaryExponent, tenMkMaximalBinaryExponent, out tenMk, out mk);
			Paranoid.Assert((MinimalTargetExponent <= w.E + tenMk.E + DiyFp.SignificandSize) && (MaximalTargetExponent >= w.E + tenMk.E + DiyFp.SignificandSize));

			var scaledW = DiyFp.Times(ref w, ref tenMk);
			Paranoid.Assert(scaledW.E == boundaryPlus.E + tenMk.E + DiyFp.SignificandSize);

			var scaledBoundaryMinus = DiyFp.Times(ref boundaryMinus, ref tenMk);
			var scaledBoundaryPlus = DiyFp.Times(ref boundaryPlus, ref tenMk);

			bool res = DigitGen(ref scaledBoundaryMinus, ref scaledW, ref scaledBoundaryPlus, buffer, out length, out var kappa);
			decimalExponent = -mk + kappa;
			return res;
		}

		private static unsafe char* BuilderAddPadding(char* ptr, char c, int n)
		{
			Paranoid.Assert(ptr != null && n >= 0);
			while(n --> 0) *ptr++ = c;
			return ptr;
		}

		private static unsafe char* BuilderAddSubstring(char* ptr, char* src, int n)
		{
			Paranoid.Assert(ptr != null && src != null && n >= 0);
			while (n >= 2)
			{
				*((int*)ptr) = *((int*)src);
				ptr += 2;
				src += 2;
				n -= 2;
			}
			if (n > 0)
			{
				*ptr++ = *src;
			}
			return ptr;
		}

		private static unsafe int HandleSpecialValues(double value, char* output)
		{
			Paranoid.Assert(output != null);
			char* ptr = output;

			DiyDouble doubleInspect = value;
			if (doubleInspect.IsInfinite)
			{
				if (value < 0.0) *ptr++ = '-';
				ptr[0] = 'I';
				ptr[1] = 'n';
				ptr[2] = 'f';
				ptr[3] = 'i';
				ptr[4] = 'n';
				ptr[5] = 'i';
				ptr[6] = 't';
				ptr[7] = 'y';
				ptr += 8;
				return (int)(ptr - output);
			}
			if (doubleInspect.IsNaN)
			{
				ptr[0] = 'N';
				ptr[1] = 'a';
				ptr[2] = 'N';
				ptr += 3;
				return (int)(ptr - output);
			}
			return 0;
		}

		private static unsafe int CreateDecimalRepresentation(char* decimalDigits, int length, int decimalPoint, int digitsAfterPoint, char* output)
		{
			Paranoid.Assert(decimalDigits != null && length > 0 && output != null);

			char* ptr = output;

			// Create a representation that is padded with zeros if needed.
			if (decimalPoint <= 0)
			{ // "0.00000decimal_rep"
				*ptr++ = '0';
				if (digitsAfterPoint > 0)
				{
					*ptr++ = '.';
					ptr = BuilderAddPadding(ptr, '0', -decimalPoint);
					Paranoid.Assert(length <= digitsAfterPoint - (-decimalPoint));
					ptr = BuilderAddSubstring(ptr, decimalDigits, length);
					int remainingDigits = digitsAfterPoint - (-decimalPoint) - length;
					Paranoid.Assert(remainingDigits >= 0);
					ptr = BuilderAddPadding(ptr, '0', remainingDigits);
				}
			}
			else if (decimalPoint >= length)
			{ // "decimal_rep0000.00000" or "decimal_rep.0000"
				ptr = BuilderAddSubstring(ptr, decimalDigits, length);
				ptr = BuilderAddPadding(ptr, '0', decimalPoint - length);
				if (digitsAfterPoint > 0)
				{
					*ptr++ = '.';
					ptr = BuilderAddPadding(ptr, '0', digitsAfterPoint);
				}
			}
			else
			{ // "decima.l_rep000"
				Paranoid.Assert(digitsAfterPoint > 0);
				ptr = BuilderAddSubstring(ptr, decimalDigits, decimalPoint);
				*ptr++ = '.';
				Paranoid.Assert(length - decimalPoint <= digitsAfterPoint);
				ptr = BuilderAddSubstring(ptr, decimalDigits + decimalPoint, length - decimalPoint);
				int remainingDigits = digitsAfterPoint - (length - decimalPoint);
				ptr = BuilderAddPadding(ptr, '0', remainingDigits);
			}

			//TODO: source adds final '.0' depending on some flags

			return (int)(ptr - output);
		}

		private static unsafe int CreateExponentialRepresentation(char* decimalDigits, int length, int exponent, char* output)
		{
			Paranoid.Assert(decimalDigits != null && length > 0 && output != null);

			char* ptr = output;
			*ptr++ = *decimalDigits;
			if (length != 1)
			{
				*ptr++ = '.';
				ptr = BuilderAddSubstring(ptr, decimalDigits + 1, length - 1);
			}
			*ptr++ = 'E';
			if (exponent < 0)
			{
				*ptr++ = '-';
				exponent = -exponent;
			}
			else
			{ // note: la BCL met tjrs E+xxx
				*ptr++ = '+';
			}

			Paranoid.Assert(exponent < 1e4);
			if (exponent < 10)
			{
				*ptr++ = (char)('0' + exponent);
			}
			else if (exponent < 100)
			{
				ptr[0] = (char)('0' + (exponent / 10));
				ptr[1] = (char)('0' + (exponent % 10));
				ptr += 2;
			}
			else if (exponent < 1000)
			{
				ptr[0] = (char)('0' + (exponent / 100));
				ptr[1] = (char)('0' + ((exponent / 10) % 10));
				ptr[2] = (char)('0' + (exponent % 10));
				ptr += 3;
			}
			else
			{
				ptr[0] = (char)('0' + (exponent / 1000));
				ptr[1] = (char)('0' + ((exponent / 100) % 10));
				ptr[2] = (char)('0' + ((exponent / 10) % 10));
				ptr[3] = (char)('0' + (exponent % 10));
				ptr += 4;
			}
			return (int)(ptr - output);
		}

		private struct CachedPower
		{
			public readonly ulong Significand;
			public readonly short BinaryExponent;
			public readonly short DecimalExponent;

			public CachedPower(ulong significand, short binaryExponent, short decimalExponent)
			{
				this.Significand = significand;
				this.BinaryExponent = binaryExponent;
				this.DecimalExponent = decimalExponent;
			}
		}

		private static readonly CachedPower[] CachedPowers = new[]
		{
			new CachedPower(0xfa8fd5a0081c0288ul, -1220, -348),
			new CachedPower(0xbaaee17fa23ebf76ul, -1193, -340),
			new CachedPower(0x8b16fb203055ac76ul, -1166, -332),
			new CachedPower(0xcf42894a5dce35eaul, -1140, -324),
			new CachedPower(0x9a6bb0aa55653b2dul, -1113, -316),
			new CachedPower(0xe61acf033d1a45dful, -1087, -308),
			new CachedPower(0xab70fe17c79ac6caul, -1060, -300),
			new CachedPower(0xff77b1fcbebcdc4ful, -1034, -292),
			new CachedPower(0xbe5691ef416bd60cul, -1007, -284),
			new CachedPower(0x8dd01fad907ffc3cul, -980, -276),
			new CachedPower(0xd3515c2831559a83ul, -954, -268),
			new CachedPower(0x9d71ac8fada6c9b5ul, -927, -260),
			new CachedPower(0xea9c227723ee8bcbul, -901, -252),
			new CachedPower(0xaecc49914078536dul, -874, -244),
			new CachedPower(0x823c12795db6ce57ul, -847, -236),
			new CachedPower(0xc21094364dfb5637ul, -821, -228),
			new CachedPower(0x9096ea6f3848984ful, -794, -220),
			new CachedPower(0xd77485cb25823ac7ul, -768, -212),
			new CachedPower(0xa086cfcd97bf97f4ul, -741, -204),
			new CachedPower(0xef340a98172aace5ul, -715, -196),
			new CachedPower(0xb23867fb2a35b28eul, -688, -188),
			new CachedPower(0x84c8d4dfd2c63f3bul, -661, -180),
			new CachedPower(0xc5dd44271ad3cdbaul, -635, -172),
			new CachedPower(0x936b9fcebb25c996ul, -608, -164),
			new CachedPower(0xdbac6c247d62a584ul, -582, -156),
			new CachedPower(0xa3ab66580d5fdaf6ul, -555, -148),
			new CachedPower(0xf3e2f893dec3f126ul, -529, -140),
			new CachedPower(0xb5b5ada8aaff80b8ul, -502, -132),
			new CachedPower(0x87625f056c7c4a8bul, -475, -124),
			new CachedPower(0xc9bcff6034c13053ul, -449, -116),
			new CachedPower(0x964e858c91ba2655ul, -422, -108),
			new CachedPower(0xdff9772470297ebdul, -396, -100),
			new CachedPower(0xa6dfbd9fb8e5b88ful, -369, -92),
			new CachedPower(0xf8a95fcf88747d94ul, -343, -84),
			new CachedPower(0xb94470938fa89bcful, -316, -76),
			new CachedPower(0x8a08f0f8bf0f156bul, -289, -68),
			new CachedPower(0xcdb02555653131b6ul, -263, -60),
			new CachedPower(0x993fe2c6d07b7facul, -236, -52),
			new CachedPower(0xe45c10c42a2b3b06ul, -210, -44),
			new CachedPower(0xaa242499697392d3ul, -183, -36),
			new CachedPower(0xfd87b5f28300ca0eul, -157, -28),
			new CachedPower(0xbce5086492111aebul, -130, -20),
			new CachedPower(0x8cbccc096f5088ccul, -103, -12),
			new CachedPower(0xd1b71758e219652cul, -77, -4),
			new CachedPower(0x9c40000000000000ul, -50, 4),
			new CachedPower(0xe8d4a51000000000ul, -24, 12),
			new CachedPower(0xad78ebc5ac620000ul, 3, 20),
			new CachedPower(0x813f3978f8940984ul, 30, 28),
			new CachedPower(0xc097ce7bc90715b3ul, 56, 36),
			new CachedPower(0x8f7e32ce7bea5c70ul, 83, 44),
			new CachedPower(0xd5d238a4abe98068ul, 109, 52),
			new CachedPower(0x9f4f2726179a2245ul, 136, 60),
			new CachedPower(0xed63a231d4c4fb27ul, 162, 68),
			new CachedPower(0xb0de65388cc8ada8ul, 189, 76),
			new CachedPower(0x83c7088e1aab65dbul, 216, 84),
			new CachedPower(0xc45d1df942711d9aul, 242, 92),
			new CachedPower(0x924d692ca61be758ul, 269, 100),
			new CachedPower(0xda01ee641a708deaul, 295, 108),
			new CachedPower(0xa26da3999aef774aul, 322, 116),
			new CachedPower(0xf209787bb47d6b85ul, 348, 124),
			new CachedPower(0xb454e4a179dd1877ul, 375, 132),
			new CachedPower(0x865b86925b9bc5c2ul, 402, 140),
			new CachedPower(0xc83553c5c8965d3dul, 428, 148),
			new CachedPower(0x952ab45cfa97a0b3ul, 455, 156),
			new CachedPower(0xde469fbd99a05fe3ul, 481, 164),
			new CachedPower(0xa59bc234db398c25ul, 508, 172),
			new CachedPower(0xf6c69a72a3989f5cul, 534, 180),
			new CachedPower(0xb7dcbf5354e9beceul, 561, 188),
			new CachedPower(0x88fcf317f22241e2ul, 588, 196),
			new CachedPower(0xcc20ce9bd35c78a5ul, 614, 204),
			new CachedPower(0x98165af37b2153dful, 641, 212),
			new CachedPower(0xe2a0b5dc971f303aul, 667, 220),
			new CachedPower(0xa8d9d1535ce3b396ul, 694, 228),
			new CachedPower(0xfb9b7cd9a4a7443cul, 720, 236),
			new CachedPower(0xbb764c4ca7a44410ul, 747, 244),
			new CachedPower(0x8bab8eefb6409c1aul, 774, 252),
			new CachedPower(0xd01fef10a657842cul, 800, 260),
			new CachedPower(0x9b10a4e5e9913129ul, 827, 268),
			new CachedPower(0xe7109bfba19c0c9dul, 853, 276),
			new CachedPower(0xac2820d9623bf429ul, 880, 284),
			new CachedPower(0x80444b5e7aa7cf85ul, 907, 292),
			new CachedPower(0xbf21e44003acdd2dul, 933, 300),
			new CachedPower(0x8e679c2f5e44ff8ful, 960, 308),
			new CachedPower(0xd433179d9c8cb841ul, 986, 316),
			new CachedPower(0x9e19db92b4e31ba9ul, 1013, 324),
			new CachedPower(0xeb96bf6ebadf77d9ul, 1039, 332),
			new CachedPower(0xaf87023b9bf0ee6bul, 1066, 340),
		};
		private const int CachedPowersOffset = 348;
		private const double D_1_LOG2_10 = 0.30102999566398114;  //  1 / lg(10)
		private const int DecimalExponentDistance = 8;
		private const int MinDecimalExponent = -348;
		private const int MaxDecimalExponent = 340;

		private static void GetCachedPowerForBinaryExponentRange(int minExponent, int maxExponent, out DiyFp power, out int decimalExponent)
		{

			int kQ = DiyFp.SignificandSize;
			double k = Math.Ceiling((minExponent + kQ - 1) * D_1_LOG2_10);
			int foo = CachedPowersOffset;
			int index = (foo + (int)k - 1) / DecimalExponentDistance + 1;
			Paranoid.Assert(0 <= index && index < CachedPowers.Length);

			var cachedPower = CachedPowers[index];
			Paranoid.Assert(minExponent <= cachedPower.BinaryExponent);
			Paranoid.Assert(cachedPower.BinaryExponent <= maxExponent);
			decimalExponent = cachedPower.DecimalExponent;
			power = new DiyFp(cachedPower.Significand, cachedPower.BinaryExponent);
		}

	}

}
