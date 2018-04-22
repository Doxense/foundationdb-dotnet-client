﻿#region BSD Licence
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

namespace FoundationDB.Layers.Experimental.Indexing
{
	using System;
	using System.Collections.Generic;
	using Doxense.Diagnostics.Contracts;
	using Doxense.Memory;

	/// <summary>Iterator that reads 32-bit compressed words from a compressed bitmap</summary>
	public struct CompressedBitmapWordIterator : IEnumerator<CompressedWord>
	{
		/// <summary>Source of compressed words</summary>
		private SliceReader m_reader;
		private uint m_current;

		internal CompressedBitmapWordIterator(Slice buffer)
		{
			Contract.Requires((buffer.Count & 3) == 0 && (buffer.Count == 0 || buffer.Count >= 8));
			if (buffer.Count == 0)
			{
				m_reader = new SliceReader();
			}
			else
			{ // skip the header
				m_reader = new SliceReader(buffer.Substring(4));
			}
			m_current = 0;
		}

		public bool MoveNext()
		{
			if (m_reader.Remaining < 4)
			{
				m_current = 0;
				return false;
			}
			m_current = m_reader.ReadFixed32();
			return true;
		}

		public CompressedWord Current => new CompressedWord(m_current);

		object System.Collections.IEnumerator.Current => this.Current;

		public void Reset()
		{
			m_reader.Position = 0;
			m_current = 0;
		}

		public void Dispose()
		{
			m_reader = default(SliceReader);
			m_current = 0;
		}

	}

}
