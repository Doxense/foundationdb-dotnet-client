#region BSD License
/* Copyright (c) 2013-2020, Doxense SAS
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

namespace FoundationDB.Layers.Experimental.Indexing
{
	using System;
	using System.Collections.Generic;
	using System.Diagnostics;

	/// <summary>Represents a compressed vector of bits</summary>
	[DebuggerDisplay("{Count} words: {m_bounds.Lowest}..{m_bounds.Highest}")]
	public sealed class CompressedBitmap : IEnumerable<CompressedWord>
	{
		// A compressed bitmap is IMMUTABLE and ReadOnly

		/// <summary>Returns a new instance of an empty bitmap</summary>
		public static readonly CompressedBitmap Empty = new CompressedBitmap(MutableSlice.Empty, BitRange.Empty);

		private readonly Slice m_data;
		private readonly BitRange m_bounds;

		public CompressedBitmap(Slice data)
		{
			if (data.IsNull) throw new ArgumentNullException(nameof(data));
			if (data.Count > 0 && data.Count < 8) throw new ArgumentException("A compressed bitmap must either be empty, or at least 8 bytes long", nameof(data));
			if ((data.Count & 3) != 0) throw new ArgumentException("A compressed bitmap size must be a multiple of 4 bytes", nameof(data));

			if (data.Count == 0)
			{
				m_data = Slice.Empty;
				m_bounds = BitRange.Empty;
			}
			else
			{
				m_data = data;
				m_bounds = ComputeBounds(data);
			}
		}

		internal CompressedBitmap(MutableSlice data, BitRange bounds)
		{
			if (data.IsNull) throw new ArgumentNullException(nameof(data));

			if (data.Count == 0)
			{
				m_data = MutableSlice.Empty;
				m_bounds = BitRange.Empty;
			}
			else
			{
				if ((data.Count & 3) != 0) throw new ArgumentException("A compressed bitmap size must be a multiple of 4 bytes", nameof(data));
				if (data.Count < 4) throw new ArgumentException("A compressed bitmap must be at least 4 bytes long", nameof(data));
				m_data = data;
				m_bounds = bounds;
			}
		}

		/// <summary>Gets the compressed bitmap's data</summary>
		public Slice ToSlice() { return m_data; }

		public CompressedBitmapBuilder ToBuilder()
		{
			return new CompressedBitmapBuilder(this);
		}

		/// <summary>Gets the underlying buffer of the compressed bitmap</summary>
		/// <remarks>The content of the buffer MUST NOT be modified directly</remarks>
		internal Slice Data => m_data;

		/// <summary>Gets the bounds of the compressed bitmap</summary>
		public BitRange Bounds => m_bounds;

		/// <summary>Number of Data Words in the compressed bitmap</summary>
		public int Count => m_data.IsNullOrEmpty ? 0 : (m_data.Count >> 2) - 1;

		/// <summary>Number of bytes in the compressed bitmap</summary>
		public int ByteCount => m_data.Count;

		/// <summary>Test if the specified bit is set</summary>
		/// <param name="bitOffset">Offset of the bit to test</param>
		/// <returns>Returns true if the bit is set; otherwise, false</returns>
		/// <remarks>If bitOffset is outside the bitmap, false will be returned</remarks>
		public bool Test(int bitOffset)
		{
			if (bitOffset > m_bounds.Highest || bitOffset < m_bounds.Lowest)
			{ // out of bounds
				return false;
			}

			int p = 0;
			foreach(var word in this)
			{
				int n = p + word.FillCount * 31;
				if (n > bitOffset)
				{
					if (word.IsLiteral)
					{
						return (word.Literal & (1 << (bitOffset - p))) != 0;
					}
					else
					{
						return word.FillBit == 1;
					}
				}
			}
			return false;
		}

		/// <summary>Count the number of bits set to 1 in this bitmap</summary>
		public int CountBits()
		{
			int count = 0;
			foreach(var word in this)
			{
				count += word.CountBits();
			}
			return count;
		}

		/// <summary>Returns the bounds of the uncompressed bitmap index</summary>
		internal static BitRange ComputeBounds(Slice data, int words = -1)
		{
			int count = data.Count;
			if (count > 0 && count < 8) throw new ArgumentException("Bitmap buffer size is too small", nameof(data));
			if ((count & 3) != 0) throw new ArgumentException("Bitmap buffer size must be a multiple of 4 bytes", nameof(data));

			// if the bitmap is empty, return 0..0
			if (count == 0) return BitRange.Empty;

			// the highest bit is in the header
			int highest;
			if (words < 0)
			{ // the bitmap is complete so we can read the header
				highest = (int) data.ReadUInt32(0, 4);
				if (highest < 0 && highest != -1) throw new InvalidOperationException("Corrupted bitmap buffer (highest bit underflow)" + highest);
			}
			else
			{ // the bitmap is not finished, we need to find it ourselves
				highest = (words * 31) - 1;
				// check the last word if it is a literal
				int last = new CompressedWord(data.ReadUInt32(data.Count - 4, 4)).GetHighestBit();
				if (last >= 0) highest += last - 30;
			}

			// to compute the lowest bit, we need to look for initial fillers with 0-bit, and the check the first literal
			int lowest = 0;
			using(var iter = new CompressedBitmapWordIterator(data))
			{
				while (iter.MoveNext() && lowest >= 0)
				{
					var word = iter.Current;
					if (word.IsLiteral)
					{ // count the lower bits that are 0 for the final tally
						int first = word.GetLowestBit();
						if (first < 0)
						{ // all zeroes (not regular)
							lowest += 31;
							continue;
						}

						lowest += first;
						break;
					}

					if (word.FillBit == 1)
					{ // this is all 1s
						break;
					}

					// this is 0-bit FILLER
					lowest += 31 * word.FillCount;
				}
				if (lowest < 0) throw new InvalidOperationException("Corrupted bitmap buffer (lowest bit overflow)"+lowest);
			}

			//Console.WriteLine("Computed bounds are: {0}..{1}", lowest, highest);
			return new BitRange(lowest, highest);

		}

		/// <summary>Get some statistics on this bitmap</summary>
		/// <param name="bits">Receives the number of set bits in the bitmap</param>
		/// <param name="words">Receives the number of data words</param>
		/// <param name="literals">Receives the number of literal words (&lt;= <paramref name="words"/>)</param>
		/// <param name="fillers">Receives the number of filler words (&lt;= <paramref name="words"/>)</param>
		/// <param name="ratio">Receives ratio of the compressed size compared to the size of the equivalent uncompressed bitmap (using the highest set bit as a cutoff)</param>
		public void GetStatistics(out int bits, out int words, out int literals, out int fillers, out double ratio)
		{
			int n = 0, b = 0, l = 0, f = 0;
			foreach (var word in this)
			{
				++n;
				if (word.IsLiteral) l++; else f++;
				b += word.CountBits();
			}
			bits = b;
			words = n;
			literals = l;
			fillers = f;
			ratio = (32.0 * words) /  m_bounds.Highest;
		}

		public string Dump()
		{
			return WordAlignHybridEncoder.DumpCompressed(m_data).ToString();
		}

		public CompressedBitmapBitView GetView()
		{
			return new CompressedBitmapBitView(this);
		}

		#region IEnumerable<CompressedWord>...

		public CompressedBitmapWordIterator GetEnumerator()
		{
			return new CompressedBitmapWordIterator(m_data);
		}

		IEnumerator<CompressedWord> IEnumerable<CompressedWord>.GetEnumerator()
		{
			return new CompressedBitmapWordIterator(m_data);
		}

		System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
		{
			return new CompressedBitmapWordIterator(m_data);
		}

		#endregion

	}

}
