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

namespace Doxense.Memory
{
	using System;
	using System.Diagnostics.CodeAnalysis;
	using System.Globalization;

	/// <summary>Units for size of buffers or arrays of bytes (Kilo = 1_000 bytes, Kibi = 1_024 bytes, ...)</summary>
	public enum SizeUnit
	{
		// first range is base 10

		/// <summary>Bytes</summary>
		Byte = 0,

		/// <summary>Kilobyte, <c>kB</c>, 1E3 bytes: 1 kB = 1_000 bytes</summary>
		Kilo = 1,
		/// <summary>Megabyte, <c>MB</c>, 1E6 bytes: 1 MB = 10^6 = 1_000_000 bytes</summary>
		Mega = 2,
		/// <summary>Gigabyte, <c>GB</c>, 1E9 bytes: 1 GB = 10^9 = 1_000_000_000 bytes</summary>
		Giga = 3,
		/// <summary>Terabyte, <c>TB</c>, 1E12 bytes: 1 TB = 10^12 = 1_000_000_000_000 bytes</summary>
		Tera = 4,
		/// <summary>Petabyte, <c>PB</c>, 1E15 bytes: 1 PB = 10^15 = 1_000_000_000_000_000 bytes</summary>
		Peta = 5,
		/// <summary>Exabyte, <c>EB</c>, 1E18 bytes: 1 EB = 10^18 = 1_000_000_000_000_000_000 bytes</summary>
		Exa = 6,
		//...

		// second range is base 2

		/// <summary>Kibibyte, <c>KiB</c>, 2^10 bytes: 1 KiB = 1_024 bytes</summary>
		Kibi = 129,
		/// <summary>Mebibyte, <c>MiB</c>, 2^20 bytes: 1 MiB = 1_048_576 bytes</summary>
		Mebi = 130,
		/// <summary>Gibibyte, <c>GiB</c>, 2^30 bytes: 1 GiB = 1_073_741_824 bytes</summary>
		Gibi = 131,
		/// <summary>Tebibyte, <c>TiB</c>, 2^40 bytes: 1 TiB = 1_099_511_627_776 bytes</summary>
		Tebi = 132,
		/// <summary>Pebibyte, <c>PiB</c>, 2^50 bytes: 1 PiB =  1_125_899_906_842_624 bytes</summary>
		Pebi = 133,
		/// <summary>Exibyte, <c>EiB</c>, 2^60 bytes: 1 EiB =  1_152_921_504_606_846_976 bytes</summary>
		Exi = 134,
		//...

	}

	/// <summary>Represents the size (in bytes) of a buffer or range of data, with its associated "natural" unit (KB, KiB, GiB, ...)</summary>
	/// <example>A size of 8 GiB can be expressed as (8589934592, GiB) so that the value can be nicely formated as "8 GiB".</example>
	public readonly struct ByteSize : IEquatable<ByteSize>, IEquatable<long>, IEquatable<int>, IEquatable<ulong>, IEquatable<uint>
	{

		public static readonly ByteSize Zero = default;

		private ByteSize(ulong value, SizeUnit unit)
		{
			this.Value = value;
			this.Unit = unit;
		}

		/// <summary>The size (in bytes)</summary>
		public readonly ulong Value;

		/// <summary>The natural unit that was used to construct this size (GiB, KB, ..)</summary>
		public readonly SizeUnit Unit;

		/// <summary>Test if the size is an exact multiple of its base unit</summary>
		/// <returns>true if <c>value mod sizeof(unit) == 0</c></returns>
		/// <example>
		///	<para><c>new ByteSize(16384, SizeUnit.KiB).IsMultipleOfUnit() == true</c> (because 16384 mod 1024 = 0)</para>
		///	<para><c>new ByteSize(16384, SizeUnit.KB).IsMultipleOfUnit() == false</c> (because 16384 mod 1000 = 384 != 0)</para>
		/// </example>
		public bool IsMultipleOfUnit()
		{
			var s = GetUnitSize(this.Unit);
			return s == 1 || (this.Value % s) == 0;
		}

		public double ScaleToUnitDouble() => this.Value * GetUnitRatio(this.Unit);

		public double ScaleToUnitDouble(SizeUnit unit) => this.Value * GetUnitRatio(unit);

		public int ScaleToUnitInt32(MidpointRounding rounding = MidpointRounding.AwayFromZero) => checked((int) Math.Round(this.Value * GetUnitRatio(this.Unit), rounding));

		public int ScaleToUnitInt32(SizeUnit unit, MidpointRounding rounding = MidpointRounding.AwayFromZero) => checked((int) Math.Round(this.Value * GetUnitRatio(unit), rounding));

		public uint ScaleToUnitUInt32(MidpointRounding rounding = MidpointRounding.AwayFromZero) => checked((uint) Math.Round(this.Value * GetUnitRatio(this.Unit), rounding));

		public uint ScaleToUnitUInt32(SizeUnit unit, MidpointRounding rounding = MidpointRounding.AwayFromZero) => checked((uint) Math.Round(this.Value * GetUnitRatio(unit), rounding));

		public long ScaleToUnitInt64(MidpointRounding rounding = MidpointRounding.AwayFromZero) => checked((long) Math.Round(this.Value * GetUnitRatio(this.Unit), rounding));

		public long ScaleToUnitInt64(SizeUnit unit, MidpointRounding rounding = MidpointRounding.AwayFromZero) => checked((long) Math.Round(this.Value * GetUnitRatio(unit), rounding));

		public ulong ScaleToUnitUInt64(MidpointRounding rounding = MidpointRounding.AwayFromZero) => checked((ulong) Math.Round(this.Value * GetUnitRatio(this.Unit), rounding));

		public ulong ScaleToUnitUInt64(SizeUnit unit, MidpointRounding rounding = MidpointRounding.AwayFromZero) => checked((ulong) Math.Round(this.Value * GetUnitRatio(unit), rounding));

		/// <summary>Test if the size is an exact multiple of the specified unit</summary>
		/// <returns>true if <c>value mod sizeof(unit) == 0</c></returns>
		/// <example>
		///	<para><c>new ByteSize(16777216, SizeUnit.MiB).IsMultipleOfUnit(SizeOfUnit.MiB) == true</c> (because 16777216 mod 1048576 = 0)</para>
		///	<para><c>new ByteSize(16777216, SizeUnit.MiB).IsMultipleOfUnit(SizeOfUnit.KiB) == true</c> (because 16777216 mod 1024 = 0)</para>
		///	<para><c>new ByteSize(16777216, SizeUnit.MiB).IsMultipleOfUnit(SizeOfUnit.MB) == false</c> (because 16777216 mod 1000000 = 777216 != 0)</para>
		///	<para><c>new ByteSize(16000000, SizeUnit.MB).IsMultipleOfUnit(SizeOfUnit.KB) == true</c> (because 16000000 mod 1000 = 0)</para>
		/// </example>
		public bool IsMultipleOfUnit(SizeUnit unit)
		{
			var s = GetUnitSize(unit);
			return s == 1 || (this.Value % s) == 0;
		}

		#region Base 10

		/// <summary>Kilobyte (10^3 = 1,000 bytes)</summary>
		public const long KB = 1_000L;

		/// <summary>Megabyte (10^6 = 1,000,000 bytes)</summary>
		public const long MB = 1_000_000L;

		/// <summary>Gigabyte (10^9 = 1,000,000,000 bytes)</summary>
		public const long GB = 1_000_000_000L;

		/// <summary>Terabyte (10^12 = 1,000,000,000,000 bytes)</summary>
		public const long TB = 1_000_000_000_000L;

		/// <summary>Petabyte (10^15 = 1,000,000,000,000,000 bytes)</summary>
		public const long PB = 1_000_000_000_000_000L;

		/// <summary>Exabyte (10^19 = 1,000,000,000,000,000,000 bytes)</summary>
		public const long EB = 1_000_000_000_000_000_000L;

		//note: 1 zettabyte is larger than 2^64 so could not be represented by this type anyway!

		#endregion

		#region Base 2

		/// <summary>Kibibyte (2^10 = 1,024 bytes)</summary>
		public const long KiB = 1L << 10;

		/// <summary>Mebibyte (2^20 = 1,048,576 bytes)</summary>
		public const long MiB = 1L << 20;

		/// <summary>Gibibyte (2^30 = 1,073,741,824 bytes)</summary>
		public const long GiB = 1L << 30;

		/// <summary>Tebibyte (2^40 = 1,099,511,627,776 bytes)</summary>
		public const long TiB = 1L << 40;

		/// <summary>Pebibyte (2^50 = 1,125,899,906,842,624 bytes)</summary>
		public const long PiB = 1L << 50;

		/// <summary>Exibyte (2^60 = 1,152,921,504,606,846,976 bytes)</summary>
		public const long EiB = 1L << 60;

		//note: 1 zebibyte is larger than 2^64 so could not be represented by this type anyway!

		#endregion

		public override string ToString()
		{
			if (this.Unit == SizeUnit.Byte)
			{
				//TODO: try to "auto-detect" the natural unit if it is an exact multiple?)
				return this.Value.ToString(CultureInfo.InvariantCulture);
			}
			else
			{
				double x = this.Value * ByteSize.GetUnitRatio(this.Unit);
				return x.ToString("R", CultureInfo.InvariantCulture) + " " + GetUnitLiteral(this.Unit);
			}
		}

		public static ulong GetUnitSize(SizeUnit unit) => unit switch
		{
			SizeUnit.Byte => 1,
			SizeUnit.Kilo => KB,
			SizeUnit.Mega => MB,
			SizeUnit.Giga => GB,
			SizeUnit.Tera => TB,
			SizeUnit.Peta => PB,
			SizeUnit.Exa => EB,
			SizeUnit.Kibi => KiB,
			SizeUnit.Mebi => MiB,
			SizeUnit.Gibi => GiB,
			SizeUnit.Tebi => TiB,
			SizeUnit.Pebi => PiB,
			SizeUnit.Exi => EiB,
			_ => throw new ArgumentOutOfRangeException(nameof(unit), "Invalid size unit")
		};

		public static double GetUnitRatio(SizeUnit unit) => unit switch
		{
			SizeUnit.Byte => 1.0,
			SizeUnit.Kilo => 1.0 / KB,
			SizeUnit.Mega => 1.0 / MB,
			SizeUnit.Giga => 1.0 / GB,
			SizeUnit.Tera => 1.0 / TB,
			SizeUnit.Peta => 1.0 / PB,
			SizeUnit.Exa => 1.0 / EB,
			SizeUnit.Kibi => 1.0 / KiB,
			SizeUnit.Mebi => 1.0 / MiB,
			SizeUnit.Gibi => 1.0 / GiB,
			SizeUnit.Tebi => 1.0 / TiB,
			SizeUnit.Pebi => 1.0 / PiB,
			SizeUnit.Exi => 1.0 / EiB,
			_ => throw new ArgumentOutOfRangeException(nameof(unit), "Invalid size unit")
		};

		public static SizeUnit GetNaturalUnit(ulong value)
		{
			if (value < KB) return SizeUnit.Byte;

			if ((value & (KiB - 1)) == 0)
			{ // could be base 2
				if ((value & (MiB - 1)) != 0) return SizeUnit.Kibi;
				if ((value & (GiB - 1)) != 0) return SizeUnit.Mebi;
				if ((value & (TiB - 1)) != 0) return SizeUnit.Gibi;
				if ((value & (PiB - 1)) != 0) return SizeUnit.Tebi;
				if ((value & (EiB - 1)) != 0) return SizeUnit.Pebi;
				return SizeUnit.Exi;
			}

			if (value % KB == 0)
			{ // could be base 10

				if ((value % MB) != 0) return SizeUnit.Kilo;
				if ((value % GB) != 0) return SizeUnit.Mega;
				if ((value % TB) != 0) return SizeUnit.Giga;
				if ((value % PB) != 0) return SizeUnit.Tera;
				if ((value % EB) != 0) return SizeUnit.Peta;
				return SizeUnit.Exa;
			}

			return SizeUnit.Byte;
		}

		#region Equality, Comparison...

		public override int GetHashCode() => this.Value.GetHashCode();

		public override bool Equals([NotNullWhen(true)] object? obj)
		{
			if (obj is ByteSize bs) return Equals(bs);
			if (obj is long l) return l >= 0 && (ulong) l == this.Value;
			if (obj is int i) return i >= 0 && (ulong) i == this.Value;
			if (obj is ulong ul) return ul == this.Value;
			if (obj is uint ui) return ui == this.Value;
			return false;
		}

		public bool Equals(ByteSize other) => other.Value == this.Value;

		public bool Equals(int other) => other >= 0 && (ulong) other == this.Value;

		public bool Equals(uint other) => other == this.Value;

		public bool Equals(long other) => other >= 0 && (ulong) other == this.Value;

		public bool Equals(ulong other) => other == this.Value;

		public static bool operator ==(ByteSize left, ByteSize right) => left.Value == right.Value;

		public static bool operator ==(ByteSize left, ulong right) => left.Value == right;

		public static bool operator ==(ByteSize left, long right) => left.Value == (ulong) right && right >= 0;

		public static bool operator !=(ByteSize left, ByteSize right) => left.Value != right.Value;

		public static bool operator !=(ByteSize left, ulong right) => left.Value != right;

		public static bool operator !=(ByteSize left, long right) => left.Value != (ulong) right || right < 0;

		public static bool operator >(ByteSize left, ByteSize right) => left.Value > right.Value;

		public static bool operator >(ByteSize left, ulong right) => left.Value > right;

		public static bool operator >(ByteSize left, long right) => right < 0 || left.Value > (ulong) right;

		public static bool operator >=(ByteSize left, ByteSize right) => left.Value >= right.Value;

		public static bool operator >=(ByteSize left, ulong right) => left.Value >= right;

		public static bool operator >=(ByteSize left, long right) => right < 0 || left.Value >= (ulong) right;

		public static bool operator <(ByteSize left, ByteSize right) => left.Value < right.Value;

		public static bool operator <(ByteSize left, ulong right) => left.Value < right;

		public static bool operator <(ByteSize left, long right) => right >= 0 && left.Value < (ulong) right;

		public static bool operator <=(ByteSize left, ByteSize right) => left.Value <= right.Value;

		public static bool operator <=(ByteSize left, ulong right) => left.Value <= right;

		public static bool operator <=(ByteSize left, long right) => right >= 0 && left.Value <= (ulong) right;

		#endregion

		#region Conversion...

		public static implicit operator ByteSize(int bytes) => new ByteSize(checked((ulong) bytes), SizeUnit.Byte);

		public static implicit operator ByteSize(uint bytes) => new ByteSize((ulong) bytes, SizeUnit.Byte);

		public static implicit operator ByteSize(long bytes) => new ByteSize(checked((ulong) bytes), SizeUnit.Byte);

		public static implicit operator ByteSize(ulong bytes) => new ByteSize(bytes, SizeUnit.Byte);

		/// <summary>Size expressed in bytes</summary>
		public static ByteSize Bytes(ulong bytes) => new ByteSize(bytes, SizeUnit.Byte);

		/// <summary>Size expressed in bytes</summary>
		public static ByteSize Bytes(long bytes) => new ByteSize(checked((ulong) bytes), SizeUnit.Byte);

		#region Base 10...

		#region Kilo...

		/// <summary>Return the size, downscaled to Kilobytes (<c>KB</c>, 10^3 bytes)</summary>
		public double ToKiloBytes() => this.Value / (double) KB;

		/// <summary>Size expressed in Kilobytes (10^3 bytes)</summary>
		public static ByteSize KiloBytes(int kb) => new ByteSize(checked((ulong) kb * KB), SizeUnit.Kilo);

		/// <summary>Size expressed in Kilobytes (10^3 bytes)</summary>
		public static ByteSize KiloBytes(uint kb) => new ByteSize(checked((ulong) kb * KB), SizeUnit.Kilo);

		/// <summary>Size expressed in Kilobytes (10^3 bytes)</summary>
		public static ByteSize KiloBytes(long kb) => new ByteSize(checked((ulong) kb * KB), SizeUnit.Kilo);

		/// <summary>Size expressed in Kilobytes (10^3 bytes)</summary>
		public static ByteSize KiloBytes(ulong kb) => new ByteSize(checked(kb * KB), SizeUnit.Kilo);

		#endregion

		#region Mega...

		/// <summary>Return the size, downscaled to Megabytes (<c>MB</c>, 10^6 bytes)</summary>
		public double ToMegaBytes() => this.Value / (double) MB;

		/// <summary>Size expressed in Megabytes (10^6 bytes)</summary>
		public static ByteSize MegaBytes(int mb) => new ByteSize(checked((ulong) mb * MB), SizeUnit.Mega);

		/// <summary>Size expressed in Megabytes (10^6 bytes)</summary>
		public static ByteSize MegaBytes(uint mb) => new ByteSize(checked((ulong) mb * MB), SizeUnit.Mega);

		/// <summary>Size expressed in Megabytes (10^6 bytes)</summary>
		public static ByteSize MegaBytes(long mb) => new ByteSize(checked((ulong) mb * MB), SizeUnit.Mega);

		/// <summary>Size expressed in Megabytes (10^6 bytes)</summary>
		public static ByteSize MegaBytes(ulong mb) => new ByteSize(checked(mb * MB), SizeUnit.Mega);

		#endregion

		#region Giga...

		/// <summary>Return the size, downscaled to Gigabytes (<c>GB</c>, 10^9 bytes)</summary>
		public double ToGigaBytes() => this.Value / (double) GB;

		/// <summary>Size expressed in Gigabytes (10^9 bytes)</summary>
		public static ByteSize GigaBytes(int gb) => new ByteSize(checked((ulong) gb * GB), SizeUnit.Giga);

		/// <summary>Size expressed in Gigabytes (10^9 bytes)</summary>
		public static ByteSize GigaBytes(uint gb) => new ByteSize(checked((ulong) gb * GB), SizeUnit.Giga);

		/// <summary>Size expressed in Gigabytes (10^9 bytes)</summary>
		public static ByteSize GigaBytes(long gb) => new ByteSize(checked((ulong) gb * GB), SizeUnit.Giga);

		/// <summary>Size expressed in Gigabytes (10^9 bytes)</summary>
		public static ByteSize GigaBytes(ulong gb) => new ByteSize(checked(gb * GB), SizeUnit.Giga);

		#endregion

		#region Tera...

		/// <summary>Return the size, downscaled to Terabytes (<c>TB</c>, 10^12 bytes)</summary>
		public double ToTeraBytes() => this.Value / (double) TB;

		/// <summary>Size expressed in Terabytes (10^12 bytes)</summary>
		public static ByteSize TeraBytes(int tb) => new ByteSize(checked((ulong) tb * TB), SizeUnit.Tera);

		/// <summary>Size expressed in Terabytes (10^12 bytes)</summary>
		public static ByteSize TeraBytes(uint tb) => new ByteSize(checked((ulong) tb * TB), SizeUnit.Tera);

		#endregion

		#region Peta...

		/// <summary>Return the size, downscaled to Petabytes (<c>PB</c>, 10^15 bytes)</summary>
		public double ToPetaBytes() => this.Value / (double) PB;

		/// <summary>Size expressed in Petabytes (10^15 bytes)</summary>
		public static ByteSize PetaBytes(int pb) => new ByteSize(checked((ulong) pb * PB), SizeUnit.Peta);

		/// <summary>Size expressed in Petabytes (10^15 bytes)</summary>
		public static ByteSize PetaBytes(uint pb) => new ByteSize(checked((ulong) pb * PB), SizeUnit.Peta);

		#endregion

		#region Exa...

		/// <summary>Return the size, downscaled to Exabytes (<c>EB</c>, 10^19 bytes)</summary>
		public double ToExaBytes() => this.Value / (double) EB;

		/// <summary>Size expressed in Petabytes (10^15 bytes)</summary>
		public static ByteSize ExaBytes(int eb) => new ByteSize(checked((ulong) eb * EB), SizeUnit.Exa);

		/// <summary>Size expressed in Petabytes (10^15 bytes)</summary>
		public static ByteSize ExaBytes(uint eb) => new ByteSize(checked((ulong) eb * EB), SizeUnit.Exa);

		#endregion

		#endregion

		#region Base 2...

		#region Kibi...

		/// <summary>Return the size, downscaled to Kibibytes (<c>KiB</c>, 2^10 bytes)</summary>
		public double ToKibiBytes() => this.Value / (double) KiB;

		/// <summary>Size expressed in <b>Kibibytes</b> (2^10 or 1,024 bytes)</summary>
		public static ByteSize KibiBytes(int kib) => new ByteSize(checked((ulong) kib * KiB), SizeUnit.Kibi);

		/// <summary>Size expressed in <b>Kibibytes</b> (2^10 or 1,024 bytes)</summary>
		public static ByteSize KibiBytes(uint kib) => new ByteSize(checked((ulong) kib * KiB), SizeUnit.Kibi);

		/// <summary>Size expressed in <b>Kibibytes</b> (2^10 or 1,024 bytes)</summary>
		public static ByteSize KibiBytes(long kib) => new ByteSize(checked((ulong) kib * KiB), SizeUnit.Kibi);

		/// <summary>Size expressed in <b>Kibibytes</b> (2^10 or 1,024 bytes)</summary>
		public static ByteSize KibiBytes(ulong kib) => new ByteSize(checked(kib * KiB), SizeUnit.Kibi);

		#endregion

		#region Mebi...

		/// <summary>Return the size, downscaled to Mebibytes (<c>MiB</c>, 2^20 bytes)</summary>
		public double ToMebiBytes() => this.Value / (double) MiB;

		/// <summary>Size expressed in <b>Mebibytes</b> (2^20 or 1,048,576 bytes)</summary>
		public static ByteSize MebiBytes(int mib) => new ByteSize(checked((ulong) mib * MiB), SizeUnit.Mebi);

		/// <summary>Size expressed in <b>Mebibytes</b> (2^20 or 1,048,576 bytes)</summary>
		public static ByteSize MebiBytes(uint mib) => new ByteSize(checked((ulong) mib * MiB), SizeUnit.Mebi);

		/// <summary>Size expressed in <b>Mebibytes</b> (2^20 or 1,048,576 bytes)</summary>
		public static ByteSize MebiBytes(long mib) => new ByteSize(checked((ulong) mib * MiB), SizeUnit.Mebi);

		/// <summary>Size expressed in <b>Mebibytes</b> (2^20 or 1,048,576 bytes)</summary>
		public static ByteSize MebiBytes(ulong mib) => new ByteSize(checked(mib * MiB), SizeUnit.Mebi);

		#endregion

		#region Gibi...

		/// <summary>Return the size, downscaled to Gibibytes (<c>GiB</c>, 2^30 bytes)</summary>
		public double ToGibiBytes() => this.Value / (double) GiB;

		/// <summary>Size expressed in <b>Gibibytes</b> (2^30 or 1,073,741,824 bytes bytes)</summary>
		public static ByteSize GibiBytes(int gib) => new ByteSize(checked((ulong) gib * GiB), SizeUnit.Gibi);

		/// <summary>Size expressed in <b>Gibibytes</b> (2^30 or 1,073,741,824 bytes bytes)</summary>
		public static ByteSize GibiBytes(uint gib) => new ByteSize(checked((ulong) gib * GiB), SizeUnit.Gibi);

		/// <summary>Size expressed in <b>Gibibytes</b> (2^30 or 1,073,741,824 bytes bytes)</summary>
		public static ByteSize GibiBytes(long gib) => new ByteSize(checked((ulong) gib * GiB), SizeUnit.Gibi);

		/// <summary>Size expressed in <b>Gibibytes</b> (2^30 or 1,073,741,824 bytes bytes)</summary>
		public static ByteSize GibiBytes(ulong gib) => new ByteSize(checked(gib * GiB), SizeUnit.Gibi);

		#endregion

		#region Tebi...

		/// <summary>Return the size, downscaled to Tebibytes (<c>TiB</c>, 2^40 bytes)</summary>
		public double ToTebiBytes() => this.Value / (double) TiB;

		/// <summary>Size expressed in <b>Tebibytes</b> (2^40 or 1,099,511,627,776 bytes)</summary>
		public static ByteSize TebiBytes(int tib) => new ByteSize(checked((ulong) tib * TiB), SizeUnit.Tebi);

		/// <summary>Size expressed in <b>Tebibytes</b> (2^40 or 1,099,511,627,776 bytes)</summary>
		public static ByteSize TebiBytes(uint tib) => new ByteSize(checked((ulong) tib * TiB), SizeUnit.Tebi);

		#endregion

		#region Pebi...

		/// <summary>Return the size, downscaled to Pebibytes (<c>PiB</c>, 2^50 bytes)</summary>
		public double ToPebiBytes() => this.Value / (double) PiB;

		/// <summary>Size expressed in <b>Pebibytes</b> (2^50 or 1,125,899,906,842,624 bytes)</summary>
		public static ByteSize PebiBytes(int pib) => new ByteSize(checked((ulong) pib * PiB), SizeUnit.Pebi);

		/// <summary>Size expressed in <b>Pebibytes</b> (2^50 or 1,125,899,906,842,624 bytes)</summary>
		public static ByteSize PebiBytes(uint pib) => new ByteSize(checked((ulong) pib * PiB), SizeUnit.Pebi);

		#endregion

		#region Exi...

		/// <summary>Return the size, downscaled to Exibytes (<c>EiB</c>, 2^60 bytes)</summary>
		public double ToExiBytes() => this.Value / (double) EiB;

		/// <summary>Size expressed in <b>Exibytes</b> (2^60 or 1,152,921,504,606,846,976 bytes)</summary>
		public static ByteSize ExiBytes(int eib) => new ByteSize(checked((ulong) eib * EiB), SizeUnit.Exi);

		/// <summary>Size expressed in <b>Exibytes</b> (2^60 or 1,152,921,504,606,846,976 bytes)</summary>
		public static ByteSize ExiBytes(uint eib) => new ByteSize(checked((ulong) eib * EiB), SizeUnit.Exi);

		#endregion

		#endregion

		/// <summary>Return the value in a signed 32-bit integer</summary>
		/// <exception cref="OverflowException">If the value is larger than <c>int.MaxValue</c> (~2 GiB)</exception>
		public int ToInt32() => checked((int) this.Value);

		/// <summary>Return the value in a signed 32-bit integer</summary>
		/// <exception cref="OverflowException">If the value is larger than <c>uint.MaxValue</c> (~4 GiB)</exception>
		public uint ToUInt32() => checked((uint) this.Value);

		/// <exception cref="OverflowException">If the value is larger than <c>long.MaxValue</c> (~4 GiB)</exception>
		public long ToInt64() => checked((long) this.Value);

		public ulong ToUInt64() => this.Value;

		public static implicit operator int(ByteSize size) => checked((int) size.Value);

		public static implicit operator uint(ByteSize size) => checked((uint) size.Value);

		public static implicit operator long(ByteSize size) => checked((long) size.Value);

		public static implicit operator ulong(ByteSize size) => size.Value;

		#endregion

		#region Parsing...

		public static bool TryParse(ReadOnlySpan<char> literal, out ByteSize value)
		{
			if (literal.Length == 0) goto invalid;

			int offset;
			SizeUnit unit;

			// unit detection

			char last = literal[literal.Length - 1];

			if (char.IsDigit(last))
			{ // probably bytes
				offset = 0;
				unit = SizeUnit.Byte;
			}
			else if (last == 'B')
			{
				if (literal.Length > 3 && literal[literal.Length - 2] == 'i')
				{ // ends with "iB", could be KiB, MiB, ...

					char u = literal[literal.Length - 3];

					if (u == 'K')
					{ // "KiB" means 2^10 bytes
						unit = SizeUnit.Kibi;
					}
					else if (u == 'M')
					{ // "MiB" means 2^20 bytes
						unit = SizeUnit.Mebi;
					}
					else if (u == 'G')
					{ // "GiB" means 2^30 bytes
						unit = SizeUnit.Gibi;
					}
					else if (u == 'T')
					{ // "TiB" means 2^40 bytes
						unit = SizeUnit.Tebi;
					}
					else if (u == 'P')
					{ // "PiB" means 2^50 bytes
						unit = SizeUnit.Pebi;
					}
					else if (u == 'E')
					{ // "EiB" means 2^60 bytes
						unit = SizeUnit.Exi;
					}
					else
					{
						goto invalid;
					}
					offset = 3;
				}
				else if (literal.Length > 2)
				{ // ends with 'B' (but not 'iB'), could be "KB", "MB", "GB", ...
					var u = literal[literal.Length - 2];
					if (u == 'K')
					{ // "KB" means 10^3 bytes
						offset = 2;
						unit = SizeUnit.Kilo;
					}
					else if (u == 'M')
					{ // "MB" means 10^6 bytes
						offset = 2;
						unit = SizeUnit.Mega;
					}
					else if (u == 'G')
					{ // "GB" means 10^9 bytes
						offset = 2;
						unit = SizeUnit.Giga;
					}
					else if (u == 'T')
					{ // "TB" means 10^12 bytes
						offset = 2;
						unit = SizeUnit.Tera;
					}
					else if (u == 'P')
					{ // "PB" means 10^15 bytes
						offset = 2;
						unit = SizeUnit.Peta;
					}
					else if (u == 'E')
					{ // "EB" means 10^19 bytes
						offset = 2;
						unit = SizeUnit.Exa;
					}
					else
					{ // probably bytes
						offset = 1;
						unit = SizeUnit.Byte;
					}
				}
				else
				{
					goto invalid;
				}
			}
			else if (char.IsUpper(last))
			{
				if (last == 'K')
				{ // "K" means "KiB" or 2^10 bytes
					offset = 1;
					unit = SizeUnit.Kibi;
				}
				else if (last == 'M')
				{ // "M" means "MiB" or 2^20 bytes
					offset = 1;
					unit = SizeUnit.Mebi;
				}
				else if (last == 'G')
				{ // "G" means "GiB" or 2^30 bytes
					offset = 1;
					unit = SizeUnit.Gibi;
				}
				else if (last == 'T')
				{ // "T" means "TiB" or 2^40 bytes
					offset = 1;
					unit = SizeUnit.Tebi;
				}
				else if (last == 'P')
				{ // "P" means "PiB" or 2^50 bytes
					offset = 1;
					unit = SizeUnit.Tebi;
				}
				else if (last == 'E')
				{ // "P" means "EiB" or 2^60 bytes
					offset = 1;
					unit = SizeUnit.Exi;
				}
				else
				{
					goto invalid;
				}
			}
			else
			{
				goto invalid;
			}

#if NETFRAMEWORK || NETSTANDARD
			// must allocate :(
			literal = (offset > 0 ? literal.Slice(0, literal.Length - offset) : literal).Trim();
			if (!long.TryParse(literal.ToString(), NumberStyles.Integer, CultureInfo.InvariantCulture.NumberFormat, out var num))
			{
				goto invalid;
			}
#else
			literal = (offset > 0 ? literal[..^offset] : literal).Trim();
			if (!long.TryParse(literal, NumberStyles.Integer, CultureInfo.InvariantCulture.NumberFormat, out var num))
			{
				goto invalid;
			}
#endif

			try
			{
				value = new ByteSize(checked((ulong) num * GetUnitSize(unit)), unit);
				return true;
			}
			catch (OverflowException)
			{
				goto invalid;
			}

		invalid:
			value = default;
			return false;
		}

		public static string GetUnitLiteral(SizeUnit unit)
		{
			switch (unit)
			{
				case SizeUnit.Byte: return "b";
				case SizeUnit.Kilo: return "KB";
				case SizeUnit.Mega: return "MB";
				case SizeUnit.Giga: return "GB";
				case SizeUnit.Tera: return "TB";
				case SizeUnit.Peta: return "PB";
				case SizeUnit.Exa: return "EB";
				case SizeUnit.Kibi: return "KiB";
				case SizeUnit.Mebi: return "MiB";
				case SizeUnit.Gibi: return "GiB";
				case SizeUnit.Tebi: return "TiB";
				case SizeUnit.Pebi: return "PiB";
				case SizeUnit.Exi: return "EiB";
				default: throw new ArgumentOutOfRangeException(nameof(unit), "Unknown size unit");
			}
		}

		public static SizeUnit FromUnitLiteral(string literal)
		{
			switch (literal)
			{
				case "b": return SizeUnit.Byte;
				case "KB": return SizeUnit.Kilo;
				case "MB": return SizeUnit.Mega;
				case "GB": return SizeUnit.Giga;
				case "TB": return SizeUnit.Tera;
				case "PB": return SizeUnit.Peta;
				case "EB": return SizeUnit.Exa;
				case "KiB": return SizeUnit.Kibi;
				case "MiB": return SizeUnit.Mebi;
				case "GiB": return SizeUnit.Gibi;
				case "TiB": return SizeUnit.Tebi;
				case "PiB": return SizeUnit.Pebi;
				case "EiB": return SizeUnit.Exi;
				default: throw new ArgumentOutOfRangeException(nameof(literal), "Unknown size unit");
			}
		}


		#endregion

	}

}
