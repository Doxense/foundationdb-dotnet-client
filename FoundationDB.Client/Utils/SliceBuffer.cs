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

namespace FoundationDB.Client.Utils
{
	using System;
	using System.Collections.Generic;

	/// <summary>Buffer that can be used to efficiently store multiple slices into as few chunks as possible</summary>
	public sealed class SliceBuffer
	{
		private const int DefaultPageSize = 256;

		/// <summary>Default size of a new page</summary>
		private readonly int m_pageSize;
		/// <summary>Current buffer</summary>
		private byte[] m_current;
		/// <summary>Position of the next free slot in the current buffer</summary>
		private int m_pos;
		/// <summary>Number of bytes remaining in the current buffer</summary>
		private int m_remaining;
		/// <summary>If non null, list of previously used buffers</summary>
		private List<Slice> m_closed;

		public SliceBuffer()
			: this(0)
		{ }

		public SliceBuffer(int pageSize)
		{
			if (pageSize < 0) throw new ArgumentOutOfRangeException("pageSize", "Page size cannt be less than zero");
			if (pageSize == 0)
			{
				m_pageSize = DefaultPageSize;
			}
			else
			{
				m_pageSize = SliceWriter.Align(pageSize);
			}
		}

		/// <summary>Copy a slice into the buffer, and return a new identical slice</summary>
		/// <param name="data">Data to copy in the buffer</param>
		/// <returns>Equivalent slice that is backed by the buffer.</returns>
		public Slice Intern(Slice data)
		{
			return InternWithSuffix(data, Slice.Empty);
		}

		/// <summary>Copy a pair of keys into the buffer, and return a new identical pair</summary>
		/// <param name="range">Key range</param>
		/// <returns>Equivalent pair of keys, that are backed by the buffer.</returns>
		public FdbKeyRange InternRange(FdbKeyRange range)
		{
			//TODO: we could be smart and detect situations where 'begin' is included in 'end' (common when the end is just 'begin'+\0 of \ff),
			int p = range.Begin.Count;
			var tmp = InternWithSuffix(range.Begin, range.End);
			return new FdbKeyRange(tmp.Substring(0, p), tmp.Substring(p));
		}

		public FdbKeyRange InternRange(Slice key)
		{
			// Since the end key only adds \0 to the begin key, we can reuse the same bytes by making both overlap
			var tmp = InternWithSuffix(key, FdbKey.MinValue);
			return new FdbKeyRange(tmp[0, -1], tmp);
		}

		/// <summary>Copy a slice into the buffer, immediately followed by a suffix, and return a new slice that is the concatenation of the two.</summary>
		/// <param name="data">Data to copy in the buffer</param>
		/// <param name="suffix">Suffix to copy immediately after <paramref name="data"/>.</param>
		/// <returns>Slice that is the equivalent of <paramref name="data"/> plus <paramref name="suffix"/>, backed by the buffer.</returns>
		/// <remarks>When <paramref name="data"/> is empty, <paramref name="suffix"/> is returned without being copied to the buffer itself.</remarks>
		public Slice InternWithSuffix(Slice data, Slice suffix)
		{
			if (data.IsNullOrEmpty)
			{
				//TODO: consider memoizing suffix? In most case, it comes from a constant, and it would be a waste to copy it other and other again...
				return suffix.Count > 0 ? suffix : data.Memoize();
			}

			int n = data.Count + suffix.Count;
			if (n > m_remaining)
			{ // does not fit
				return InternFallback(data, suffix);
			}

			int pos = m_pos;
			int p = data.CopyToUnsafe(0, m_current, pos, data.Count);
			if (suffix.Count > 0) p = suffix.CopyToUnsafe(0, m_current, p, suffix.Count);
			m_pos = p;
			m_remaining -= n;
			return Slice.Create(m_current, p, n);

		}

		private void Keep(Slice chunk)
		{
			if (m_closed == null) m_closed = new List<Slice>();
			m_closed.Add(chunk);
		}

		private Slice InternFallback(Slice data, Slice suffix)
		{
			int n = data.Count + suffix.Count;
			if (n > (m_pageSize >> 1))
			{ // keys that are too large are best kept in their own chunks
				var copy = suffix.Count == 0 ? data.Memoize() : data.Concat(suffix);
				Keep(copy);
				return copy;
			}

			m_current = new byte[m_pageSize];
			data.CopyTo(m_current, 0);
			m_pos = n;
			m_remaining = m_pageSize - n;
			return Slice.Create(m_current, 0, n);
		}

	}


}
