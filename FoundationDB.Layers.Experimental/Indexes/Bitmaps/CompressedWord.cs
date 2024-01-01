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

namespace FoundationDB.Layers.Experimental.Indexing
{
	using System;
	using System.Diagnostics;
	using System.Globalization;
	using Doxense.Diagnostics.Contracts;

	/// <summary>Represent a 32-bit word in a Compressed Bitmap</summary>
	[DebuggerDisplay("Literal={IsLiteral}, {WordCount} x {WordValue}")]
	public readonly struct CompressedWord
	{
		internal const uint ALL_ZEROES = 0x0;
		internal const uint ALL_ONES = 0x7FFFFFFF;

		/// <summary>Return the raw 32-bit value of this word</summary>
		public readonly uint RawValue;

		/// <summary>Create a compressed word from a raw 32-bit value</summary>
		/// <param name="raw"></param>
		public CompressedWord(uint raw)
		{
			this.RawValue = raw;
		}

		/// <summary>Checks if this word is a literal</summary>
		/// <remarks>Literal words have their MSB unset (0)</remarks>
		public bool IsLiteral => (this.RawValue & WordAlignHybridEncoder.TYPE_MASK) == WordAlignHybridEncoder.BIT_TYPE_LITERAL;

		/// <summary>Value of the 31-bit uncompressed word</summary>
		/// <remarks>This word is repeated <see cref="WordCount"/> times in the in the uncompressed bitmap.</remarks>
		public uint WordValue
		{
			get
			{
				// if bit 30 is 0, then return 30 lower bits
				// if bit 29 is 0, then return 30 x 0 bit
				// if bit 29 is 1, then return 30 x 1 bit

				uint w;
				switch((w  = this.RawValue) >> WordAlignHybridEncoder.FILL_SHIFT)
				{
					default:
					{ // 0X
						return w & WordAlignHybridEncoder.LITERAL_MASK;
					}
					case 2:
					{ // 00
						return ALL_ZEROES;
					}
					case 3:
					{ // 10
						return ALL_ONES;
					}
				}
			}
		}

		/// <summary>Number of times the value <see cref="WordValue"/> is repeated in the uncompressed bitmap</summary>
		/// <remarks>This value is 1 for literal words, and <see cref="FillCount"/> for filler words</remarks>
		public int WordCount => this.IsLiteral ? 1 : this.FillCount;

		/// <summary>Value of a literal word</summary>
		/// <remarks>Only valid if <see cref="IsLiteral"/> is true</remarks>
		public int Literal => (int)(this.RawValue & WordAlignHybridEncoder.LITERAL_MASK);

		/// <summary>Value of the fill bit (either 0 or 1)</summary>
		/// <remarks>Only valid if <see cref="IsLiteral"/> is false</remarks>
		public int FillBit => (int)((this.RawValue & WordAlignHybridEncoder.FILL_MASK) >> WordAlignHybridEncoder.FILL_SHIFT);

		/// <summary>Number of 31-bit words that are filled by <see cref="FillBit"/></summary>
		/// <remarks>Only valid if <see cref="IsLiteral"/> is false</remarks>
		public int FillCount => 1 + (int)(this.RawValue & WordAlignHybridEncoder.LENGTH_MASK);

		/// <summary>Return the position of the lowest set bit, or -1</summary>
		/// <returns>Index from 0 to 30 of the lowest set bit, or -1 if the word is empty</returns>
		/// <remarks>Return 0 for 1-bit fillers, and -1 for 0-bit fillers</remarks>
		public int GetLowestBit()
		{
			uint w = this.WordValue;
			if (w != ALL_ZEROES)
			{
				//TODO: use a fast bit tricks algorithm here
				for (int i = 0; i <= 30; i--)
				{
					if ((w & (1 << i)) != 0) return i;
				}
			}
			return 0;
		}

		/// <summary>Return the position of the highest set bit, or -1</summary>
		/// <returns>Index from 0 to 30 of the highest set bit, or -1 if the word is empty</returns>
		/// <remarks>Return 31 for 1-bit fillers, and -1 for 0-bit fillers</remarks>
		public int GetHighestBit()
		{
			uint w = this.WordValue;
			if (w != ALL_ZEROES)
			{
				//TODO: use a fast bit tricks algorithm here
				for (int i = 30; i >= 0; i--)
				{
					if ((w & (1 << i)) != 0) return i;
				}
			}
			return -1;
		}

		/// <summary>Count the number of bits set to 1 in this word</summary>
		/// <returns>For literals, count the number of bits. For fillers, returns either 0, or 31 multiplied by <see cref="FillCount"/></returns>
		public int CountBits()
		{
			var r = this.RawValue;
			return IsLiteralWord(r)
				? BitCount(this.Literal)
				: (31 * GetFillBit(r) * GetFillCount(r));
		}

		#region Boilerplate code...

		public override string ToString()
		{
			return IsLiteral
				? String.Format(CultureInfo.InvariantCulture, "[31, 0x{0:X}]", this.Literal)
				: String.Format(CultureInfo.InvariantCulture, "[{0}, {1})]", this.FillCount * 31L, this.FillBit == 1 ? "set" : "clear");
		}

		public override int GetHashCode()
		{
			return (int)this.RawValue;
		}

		public override bool Equals(object? obj)
		{
			return obj is CompressedWord word && Equals(word);
		}

		public bool Equals(CompressedWord word)
		{
			return this.RawValue == word.RawValue;
		}

		public static implicit operator CompressedWord(uint value)
		{
			return new CompressedWord(value);
		}

		public static explicit operator uint(CompressedWord word)
		{
			return word.RawValue;
		}

		public static bool operator ==(CompressedWord a, CompressedWord b)
		{
			//REVIEW: should literal(0) == filler(0, x1) and literal(1<<31 - 1) == fillter(1, x1) ??
			return a.RawValue == b.RawValue;
		}

		public static bool operator !=(CompressedWord a, CompressedWord b)
		{
			return a.RawValue != b.RawValue;
		}

		#endregion

		#region Static Helpers...

		public static uint MakeHeader(int highest)
		{
			return (uint)(highest);
		}

		public static uint MakeLiteral(uint value)
		{
			Contract.Debug.Requires(value <= ALL_ONES);
			return WordAlignHybridEncoder.BIT_TYPE_LITERAL | (value & WordAlignHybridEncoder.LITERAL_MASK);
		}

		public static uint MakeFiller(bool set, int length)
		{
			Contract.Debug.Requires(length > 0 && length <= 0x40000000);
			return WordAlignHybridEncoder.BIT_TYPE_FILL
				| (set ? WordAlignHybridEncoder.BIT_FILL_ONE : WordAlignHybridEncoder.BIT_FILL_ZERO)
				| ((uint)(length - 1) & WordAlignHybridEncoder.LENGTH_MASK);
		}

		/// <summary>Make a filler word with all bits set to 0</summary>
		/// <param name="length">Number of 31-bits word repeated</param>
		public static uint MakeZeroes(int length)
		{
			Contract.Debug.Requires(length > 0 && length <= 0x40000000);
			return WordAlignHybridEncoder.BIT_TYPE_FILL | WordAlignHybridEncoder.BIT_FILL_ZERO | ((uint)(length - 1) & WordAlignHybridEncoder.LENGTH_MASK);
		}

		/// <summary>Make a filler word with all bits set to 1</summary>
		/// <param name="length">Number of 31-bits word repeated</param>
		public static uint MakeOnes(int length)
		{
			Contract.Debug.Requires(length > 0 && length <= 0x40000000);
			return WordAlignHybridEncoder.BIT_TYPE_FILL | WordAlignHybridEncoder.BIT_FILL_ONE | ((uint)(length - 1) & WordAlignHybridEncoder.LENGTH_MASK);
		}

		internal static bool IsLiteralWord(uint word)
		{
			return (word & WordAlignHybridEncoder.TYPE_MASK) == WordAlignHybridEncoder.BIT_TYPE_LITERAL;
		}

		internal static bool IsFillerWord(uint word)
		{
			return (word & WordAlignHybridEncoder.TYPE_MASK) == WordAlignHybridEncoder.BIT_TYPE_FILL;
		}

		internal static int GetFillBit(uint word)
		{
			return (int)((word & WordAlignHybridEncoder.FILL_MASK) >> WordAlignHybridEncoder.FILL_SHIFT);
		}

		internal static int GetFillCount(uint word)
		{
			return 1 + (int)(word & WordAlignHybridEncoder.LENGTH_MASK);
		}

		/// <summary>Count the number of set bits in a 32-bit word</summary>
		public static int BitCount(int value)
		{
			// cf https://graphics.stanford.edu/~seander/bithacks.html#CountBitsSet64
			value = value - ((value >> 1) & 0x55555555);
			value = (value & 0x33333333) + ((value >> 2) & 0x33333333);
			value = ((value + (value >> 4) & 0xF0F0F0F) * 0x1010101) >> (32 - 8);
			return value;
		}

		#endregion

	}

}
