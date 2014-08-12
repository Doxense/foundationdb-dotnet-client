#region BSD Licence
/* Copyright (c) 2013-2014, Doxense SAS
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
	using FoundationDB.Client;
	using FoundationDB.Client.Utils;
	using System;

	/// <summary>Writer that compresses a stream of bits into a <see cref="CompressedBitmap"/>, in memory</summary>
	public sealed class CompressedBitmapWriter
	{
		private const uint NO_VALUE = uint.MaxValue;
		private const int NO_BITS = int.MaxValue;

		#region Private Fields...

		/// <summary>Output buffer</summary>
		private SliceWriter m_writer;
		/// <summary>Offset of the header in the writer</summary>
		private int m_head;
		/// <summary>Last word written (NO_VALUE means none)</summary>
		private uint m_current = NO_VALUE;
		/// <summary>Number of repetitions of the current word</summary>
		private int m_counter;
		/// <summary>Number of input words written (excluding any trailing 0s)</summary>
		private int m_words;
		/// <summary>If true, the bitmap has been fully constructed. If false, new words can still be written.</summary>
		private bool m_packed;
		/// <summary>If true, the buffer is private. If false, it as been exposed to someone and cannot be modified anymore</summary>
		private bool m_ownsBuffer;
		/// <summary>Bounds of the constructed bitmap (only when packed)</summary>
		private BitRange m_bounds;

		#endregion

		#region Constructors...

		public CompressedBitmapWriter()
			: this(SliceWriter.Empty, true)
		{ }

		public CompressedBitmapWriter(SliceWriter writer, bool ownsBuffer)
		{
			m_writer = writer;
			m_head = writer.Position;
			m_ownsBuffer = ownsBuffer;
			Reset();
		}

		#endregion

		#region Public Properties...

		/// <summary>Number of words written</summary>
		public int Words
		{
			get { return m_words; }
		}

		/// <summary>Length of the Compressed Bitmap</summary>
		public int Length
		{
			get { return m_writer.Position; }
		}

		#endregion

		#region Public Methods...

		/// <summary>Add a single word to the output</summary>
		public void Write(uint word)
		{
			Contract.Requires(word <= CompressedWord.ALL_ONES);
			if (m_packed) ThrowAlreadyPacked();

			if (word == CompressedWord.ALL_ZEROES || word == CompressedWord.ALL_ONES)
			{ // all zero or all one
				WriteFiller(word, 1);
			}
			else
			{
				WriteLiteral(word, 1);
			}
		}

		/// <summary>Add a word repetition to the output </summary>
		/// <param name="word">Word to add</param>
		/// <param name="count">Number of times <paramref name="word"/> is repeated in the output</param>
		public void Write(uint word, int count)
		{
			// (NO_VALUE, 0) is used to flush the buffer by flushing the curent state
			Contract.Requires(word <= CompressedWord.ALL_ONES && count > 0);
			if (m_packed) ThrowAlreadyPacked();

			if (word == CompressedWord.ALL_ZEROES || word == CompressedWord.ALL_ONES)
			{
				WriteFiller(word, count);
			}
			else
			{
				WriteLiteral(word, count);
			}
		}

		private void WriteFiller(uint word, int count)
		{
			Contract.Requires(count > 0);

			uint previous = m_current;

			if (previous == word)
			{ // continuation of current run
				m_counter += count;
				return;
			}

			if (previous == NO_VALUE)
			{ // start of new run
				m_counter += count;
				m_current = word;
				return;
			}

			// switch from one type to the other
			m_words += m_counter;
			if (previous == CompressedWord.ALL_ZEROES)
			{
				m_writer.WriteFixed32(CompressedWord.MakeZeroes(m_counter));
			}
			else
			{
				m_writer.WriteFixed32(CompressedWord.MakeOnes(m_counter));
			}
			m_counter = count;
		}

		private void WriteLiteral(uint word, int count)
		{
			Contract.Requires(count > 0);

			uint previous = m_current;
			int counter = m_counter;

			// finish whatever was left open previously
			if (previous != NO_VALUE)
			{ // need to close previous filler
				Contract.Assert(counter > 0);
				if (previous == CompressedWord.ALL_ZEROES)
				{
					m_writer.WriteFixed32(CompressedWord.MakeZeroes(counter));
				}
				else if (previous == CompressedWord.ALL_ONES)
				{
					m_writer.WriteFixed32(CompressedWord.MakeOnes(counter));
				}
				else
				{
					Contract.Assert(counter == 1);
					m_writer.WriteFixed32(CompressedWord.MakeLiteral(previous));
				}
				m_words += counter;
			}

			// output the current literal
			int n = count;
			uint w = CompressedWord.MakeLiteral(word);
			while(n--> 0)
			{
				m_writer.WriteFixed32(w);
			}
			m_words += count;
			m_current = NO_VALUE;
			m_counter = 0;
		}

		/// <summary>Flush the curernt state of the writer</summary>
		public void Flush()
		{
			if (m_packed) ThrowAlreadyPacked();

			// either previous was a literal, or a run of zeroes or ones.
			Contract.Requires(m_counter == 0 ? (m_current == NO_VALUE) : (m_current == CompressedWord.ALL_ZEROES || m_current == CompressedWord.ALL_ONES));

			int counter = m_counter;
			if (counter > 0 && m_current == CompressedWord.ALL_ONES)
			{ // complete the last run only if it was all 1's
				m_writer.WriteFixed32(CompressedWord.MakeOnes(counter));
				m_words += counter;
			}
			m_counter = 0;
			m_current = NO_VALUE;
		}

		/// <summary>Flus the state and update the header</summary>
		/// <param name="padding">Padding bits that were added to the last written word</param>
		/// <returns>Slice contained the finished compressed bitmap</returns>
		public void Pack()
		{
			if (m_packed) ThrowAlreadyPacked();

			// flush any pending word
			Flush();

			if (m_words == 0)
			{ // empty!
				m_bounds = BitRange.Empty;
				// there will be no header
				m_writer.Position = m_head;
			}
			else
			{
				// we need to find the lowest and highest bits
				m_bounds = CompressedBitmap.ComputeBounds(m_writer.ToSlice(), m_words);

				// update the header
				int p;
				m_writer.Rewind(out p, m_head);
				//the last word is either a literal, or a 1-bit filler
				m_writer.WriteFixed32(CompressedWord.MakeHeader(m_bounds.Highest));
				m_writer.Position = p;
			}

			m_packed = true;
		}

		private static void ThrowAlreadyPacked()
		{
			throw new InvalidOperationException("The compressed bitmap has already been packed");
		}

		public Slice GetBuffer()
		{
			if (!m_packed)
			{
				Contract.Assert(m_ownsBuffer);
				Pack();
			}
			m_ownsBuffer = false;
			return m_writer.ToSlice();
		}

		public CompressedBitmap GetBitmap()
		{
			if (!m_packed)
			{
				Contract.Assert(m_ownsBuffer);
				Pack();
			}
			m_ownsBuffer = false;
			return new CompressedBitmap(m_writer.ToSlice(), m_bounds);
		}

		/// <summary>Clear the content of the buffer, and start from scratch</summary>
		/// <remarks>Keeps the allocated buffer space.</remarks>
		public void Reset()
		{
			if (m_ownsBuffer)
			{
				m_writer.Position = m_head;
			}
			else
			{ // buffer has been exposed and cannot be reused
				m_writer = new SliceWriter();
				m_head = 0;
				m_ownsBuffer = true;
			}
			m_writer.WriteFixed32(0xFFFFFFFF); // incomplete
			m_current = NO_VALUE;
			m_counter = 0;
			m_words = 0;
			m_packed = false;
		}

		#endregion

	}

}
