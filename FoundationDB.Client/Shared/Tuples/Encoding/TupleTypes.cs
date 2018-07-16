#region BSD Licence
/* Copyright (c) 2013-2018, Doxense SAS
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

namespace Doxense.Collections.Tuples.Encoding
{
	using System;

	/// <summary>
	/// Constants for the various tuple value types
	/// </summary>
	internal static class TupleTypes
	{
		/// <summary>Null/Empty/Void</summary>
		internal const byte Nil = 0;

		/// <summary>ASCII String</summary>
		internal const byte Bytes = 0x1;

		/// <summary>UTF-8 String</summary>
		internal const byte Utf8 = 0x2;

		/// <summary>Nested tuple start [OBSOLETE]</summary>
		/// <remarks>Deprecated and should not be used anymore</remarks>
		internal const byte LegacyTupleStart = 0x3;

		/// <summary>Nested tuple end [OBSOLETE]</summary>
		/// <remarks>Deprecated and should not be used anymore</remarks>
		internal const byte LegacyTupleEnd = 0x4;

		/// <summary>Nested tuple</summary>
		internal const byte EmbeddedTuple = 0x5;

		/// <summary>Negative arbitrary-precision integer</summary>
		internal const byte NegativeBigInteger = 0x0B;

		internal const byte IntNeg8 = 0x0C;
		internal const byte IntNeg7 = 0x0D;
		internal const byte IntNeg6 = 0x0E;
		internal const byte IntNeg5 = 0x0F;
		internal const byte IntNeg4 = 0x10;
		internal const byte IntNeg3 = 0x11;
		internal const byte IntNeg2 = 0x12;
		internal const byte IntNeg1 = 0x13;
		/// <summary>Integer 0</summary>
		internal const byte IntZero = 0x14;
		internal const byte IntPos1 = 0x15;
		internal const byte IntPos2 = 0x16;
		internal const byte IntPos3 = 0x17;
		internal const byte IntPos4 = 0x18;
		internal const byte IntPos5 = 0x19;
		internal const byte IntPos6 = 0x1A;
		internal const byte IntPos7 = 0x1B;
		internal const byte IntPos8 = 0x1C;

		/// <summary>Positive arbitrary-precision integer</summary>
		internal const byte PositiveBigInteger = 0x1D;

		/// <summary>Single precision decimals (32-bit, Big-Endian) [DRAFT]</summary>
		internal const byte Single = 0x20;
		/// <summary>Double precision decimals (64-bit, Big-Endian) [DRAFT]</summary>
		internal const byte Double = 0x21;
		/// <summary>Triple precision decimals (80-bit, Big-Endian) [DRAFT]</summary>
		internal const byte Triple = 0x22; //note: javascript numbers
		/// <summary>Quadruple precision decimals (128-bit, Big-Endian) [DRAFT]</summary>
		internal const byte Decimal = 0x23;

		/// <summary>True Value [OBSOLETE]</summary>
		/// <remarks>Deprecated and should not be used anymore</remarks>
		internal const byte LegacyTrue = 0x25;

		/// <summary>False Value</summary>
		internal const byte False = 0x26;

		/// <summary>True Value</summary>
		internal const byte True = 0x27;

		/// <summary>RFC4122 UUID (128 bits)</summary>
		internal const byte Uuid128 = 0x30;
		/// <summary>UUID (64 bits)</summary>
		internal const byte Uuid64 = 0x31; //TODO: this is not official yet! may change!

		/// <summary>80-bit VersionStamp</summary>
		internal const byte VersionStamp80 = 0x32;

		/// <summary>96-bit VersionStamp</summary>
		internal const byte VersionStamp96 = 0x33;

		/// <summary>Standard prefix of the Directory Layer</summary>
		/// <remarks>This is not a part of the tuple encoding itself, but helps the tuple decoder pretty-print tuples that would otherwise be unparsable.</remarks>
		internal const byte Directory = 254;

		/// <summary>Standard prefix of the System keys, or frequent suffix with key ranges</summary>
		internal const byte Escape = 255;

		/// <summary>Return the type of a tuple segment, from its header</summary>
		public static TupleSegmentType DecodeSegmentType(Slice segment)
		{
			if (segment.Count == 0) return TupleSegmentType.Nil;

			int type = segment[0];
			switch(type)
			{
				case Nil: return TupleSegmentType.Nil;
				case Bytes: return TupleSegmentType.ByteString;
				case Utf8: return TupleSegmentType.UnicodeString;
				case LegacyTupleStart: return TupleSegmentType.Invalid; // not supported anymore
				case EmbeddedTuple: return TupleSegmentType.Tuple;
				case Single: return TupleSegmentType.Single;
				case Double: return TupleSegmentType.Double;
				case Triple: return TupleSegmentType.Triple;
				case Decimal: return TupleSegmentType.Decimal;
				case Uuid128: return TupleSegmentType.Uuid128;
				case Uuid64: return TupleSegmentType.Uuid64;
				case VersionStamp80: return TupleSegmentType.VersionStamp80;
				case VersionStamp96: return TupleSegmentType.VersionStamp96;
			}

			if (type <= IntPos8 & type >= IntNeg8)
			{
				return TupleSegmentType.Integer;
			}

			return TupleSegmentType.Invalid;
		}
	}

	/// <summary>Logical type of packed element of a tuple</summary>
	public enum TupleSegmentType
	{
		Invalid = -1,
		Nil = 0,
		ByteString = 1,
		UnicodeString = 2,
		Tuple = 3,
		Integer = 20,
		Single = 32,
		Double = 33,
		Triple = 34,
		Decimal = 35,
		Uuid128 = 48,
		Uuid64 = 49,
		VersionStamp80 = 0x32,
		VersionStamp96 = 0x33,
	}

}
