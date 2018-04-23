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
		internal const byte Bytes = 1;

		/// <summary>UTF-8 String</summary>
		internal const byte Utf8 = 2;

		/// <summary>Nested tuple [DRAFT]</summary>
		internal const byte TupleStart = 3;

		internal const byte IntNeg8 = 12;
		internal const byte IntNeg7 = 13;
		internal const byte IntNeg6 = 14;
		internal const byte IntNeg5 = 15;
		internal const byte IntNeg4 = 16;
		internal const byte IntNeg3 = 17;
		internal const byte IntNeg2 = 18;
		internal const byte IntNeg1 = 19;
		internal const byte IntZero = 20;
		internal const byte IntPos1 = 21;
		internal const byte IntPos2 = 22;
		internal const byte IntPos3 = 23;
		internal const byte IntPos4 = 24;
		internal const byte IntPos5 = 25;
		internal const byte IntPos6 = 26;
		internal const byte IntPos7 = 27;
		internal const byte IntPos8 = 28;

		/// <summary>Base value for integer types (20 +/- n)</summary>
		internal const int IntBase = 20;

		/// <summary>Single precision decimals (32-bit, Big-Endian) [DRAFT]</summary>
		internal const byte Single = 32;
		/// <summary>Double precision decimals (64-bit, Big-Endian) [DRAFT]</summary>
		internal const byte Double = 33;
		/// <summary>Triple precision decimals (80-bit, Big-Endian) [DRAFT]</summary>
		internal const byte Triple = 34; //note: javascript numbers
		/// <summary>Quadruple precision decimals (128-bit, Big-Endian) [DRAFT]</summary>
		internal const byte Decimal = 35;

		/// <summary>RFC4122 UUID (128 bits) [DRAFT]</summary>
		internal const byte Uuid128 = 48;
		/// <summary>UUID (64 bits) [DRAFT]</summary>
		internal const byte Uuid64 = 49; //TODO: this is not official yet! may change!

		/// <summary>Standard prefix of the Directory Layer</summary>
		/// <remarks>This is not a part of the tuple encoding itself, but helps the tuple decoder pretty-print tuples that would otherwise be unparsable.</remarks>
		internal const byte AliasDirectory = 254;

		/// <summary>Standard prefix of the System keys, or frequent suffix with key ranges</summary>
		/// <remarks>This is not a part of the tuple encoding itself, but helps the tuple decoder pretty-print End keys from ranges, that would otherwise be unparsable.</remarks>
		internal const byte AliasSystem = 255;

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
				case TupleStart: return TupleSegmentType.Tuple;
				case Single: return TupleSegmentType.Single;
				case Double: return TupleSegmentType.Double;
				case Triple: return TupleSegmentType.Triple;
				case Decimal: return TupleSegmentType.Decimal;
				case Uuid128: return TupleSegmentType.Uuid128;
				case Uuid64: return TupleSegmentType.Uuid64;
			}

			if (type <= IntPos8 && type >= IntNeg8)
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
	}

}
