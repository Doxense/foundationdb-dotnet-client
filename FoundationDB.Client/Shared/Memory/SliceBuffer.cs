#region BSD License
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

#if !USE_SHARED_FRAMEWORK

namespace Doxense.Memory
{
	using System;
	using System.Collections.Generic;
	using System.Diagnostics;
	using Doxense.Diagnostics.Contracts;
	using JetBrains.Annotations;

	/// <summary>Buffer that can be used to efficiently store multiple slices into as few chunks as possible</summary>
	/// <remarks>
	/// This class is useful to centralize a lot of temporary slices whose lifetime is linked to a specific operation. Dropping the reference to the buffer will automatically reclaim all the slices that were stored with it.
	/// This class is not thread safe.
	/// </remarks>
	[DebuggerDisplay("Pos={m_pos}, Remaining={m_remaining}, PageSize={m_pageSize}, Size={Size}, Allocated={Allocated}")]
	public sealed class SliceBuffer
	{
		private const int DefaultPageSize = 256;
		private const int MaxPageSize = 64 * 1024; // 64KB (small enough to not go into the LOH)

		/// <summary>Default initial size of pages (doubled every time until it reached the max page size)</summary>
		private int m_pageSize;
		/// <summary>Current buffer</summary>
		private byte[] m_current;
		/// <summary>Position of the next free slot in the current buffer</summary>
		private int m_pos;
		/// <summary>Number of bytes remaining in the current buffer</summary>
		private int m_remaining;
		/// <summary>If non null, list of previously used buffers (excluding the current buffer)</summary>
		private List<Slice> m_chunks;
		/// <summary>Running total of the length of of all previously used buffers, excluding the size of the current buffer</summary>
		private int m_allocated;
		/// <summary>Running total of the number of bytes stored in the previously used buffers, excluding the size of the current buffer</summary>
		private int m_used;

		/// <summary>Create a new slice buffer with the default page size</summary>
		public SliceBuffer()
			: this(0)
		{ }

		/// <summary>Create a new slice buffer with the specified page size</summary>
		/// <param name="pageSize">Initial page size</param>
		public SliceBuffer(int pageSize)
		{
			if (pageSize < 0) throw new ArgumentOutOfRangeException(nameof(pageSize), "Page size cannt be less than zero");
			m_pageSize = pageSize == 0 ? DefaultPageSize : BitHelpers.AlignPowerOfTwo(pageSize, 16);
		}

		/// <summary>Gets the number of bytes used by all the slice allocated in this buffer</summary>
		public int Size => m_used + m_pos;

		/// <summary>Gets the total memory size allocated to store all the slices in this buffer</summary>
		public int Allocated => m_allocated + m_pos + m_remaining;

		/// <summary>Number of memory pages used by this buffer</summary>
		public int PageCount => m_chunks?.Count + 1 ?? 1;

		/// <summary>Return the list of all the pages used by this buffer</summary>
		/// <returns>Array of pages used by the buffer</returns>
		[NotNull]
		public Slice[] GetPages()
		{
			var pages = new Slice[this.PageCount];
			m_chunks?.CopyTo(pages);
			pages[pages.Length - 1] = new Slice(m_current, 0, m_pos);
			return pages;
		}

		/// <summary>Allocate an empty space in the buffer</summary>
		/// <param name="count">Number of bytes to allocate</param>
		/// <param name="aligned">If true, align the start of the slice with the default padding size.</param>
		/// <returns>Slice pointing to a space in the buffer</returns>
		/// <remarks>There is NO guarantees that the allocated slice will be pre-filled with zeroes.</remarks>
		public MutableSlice Allocate(int count, bool aligned = false)
		{
			if (count < 0) throw new ArgumentException("Cannot allocate less than zero bytes.", nameof(count));

			const int ALIGNMENT = 4;

			if (count == 0)
			{
				return MutableSlice.Empty;
			}

			int p = m_pos;
			int r = m_remaining;
			int extra = aligned ? (ALIGNMENT - (p & (ALIGNMENT - 1))) : 0;
			if (count + extra > r)
			{ // does not fit
				return AllocateFallback(count);
			}

			Contract.Assert(m_current != null && m_pos >= 0);
			m_pos = p + (count + extra);
			m_remaining = r - (count + extra);
			Contract.Ensures(m_remaining >= 0);
			//note: we rely on the fact that the buffer was pre-filled with zeroes
			return new MutableSlice(m_current, p + extra, count);
		}

		private MutableSlice AllocateFallback(int count)
		{
			// keys that are too large are best kept in their own chunks
			if (count > (m_pageSize >> 1))
			{
				var tmp = MutableSlice.Create(count);
				Keep(tmp);
				return tmp;
			}

			int pageSize = m_pageSize;

			// double the page size on each new allocation
			if (m_current != null)
			{
				if (m_pos > 0) Keep(new MutableSlice(m_current, 0, m_pos));
				pageSize <<= 1;
				if (pageSize > MaxPageSize) pageSize = MaxPageSize;
				m_pageSize = pageSize;
			}

			var buffer = new byte[pageSize];
			m_current = buffer;
			m_pos = count;
			m_remaining = pageSize - count;

			return new MutableSlice(buffer, 0, count);
		}

		/// <summary>Copy a slice into the buffer, with optional alignment, and return a new identical slice.</summary>
		/// <param name="data">Data to copy in the buffer</param>
		/// <param name="aligned">If true, align the index of first byte of the slice with a multiple of 8 bytes</param>
		/// <returns>Slice that is the equivalent of <paramref name="data"/>, backed by the buffer.</returns>
		public Slice Intern(Slice data, bool aligned = false)
		{
			if (data.Count == 0)
			{
				// transform into the corresponding Slice.Nil / Slice.Empty singleton
				return data.Memoize();
			}

			data.EnsureSliceIsValid();

			// allocate the slice
			var slice = Allocate(data.Count, aligned);
			data.CopyTo(slice.Span);
			return slice;
		}

		/// <summary>Copy a slice into the buffer, immediately followed by a suffix, and return a new slice that is the concatenation of the two.</summary>
		/// <param name="data">Data to copy in the buffer</param>
		/// <param name="suffix">Suffix to copy immediately after <paramref name="data"/>.</param>
		/// <param name="aligned">If true, align the index of first byte of the slice with a multiple of 8 bytes</param>
		/// <returns>Slice that is the equivalent of <paramref name="data"/> plus <paramref name="suffix"/>, backed by the buffer.</returns>
		/// <remarks>When <paramref name="data"/> is empty, <paramref name="suffix"/> is returned without being copied to the buffer itself.</remarks>
		internal Slice Intern(Slice data, Slice suffix, bool aligned = false)
		{
			if (data.Count == 0)
			{
				// note: we don't memoize the suffix, because in most case, it comes from a constant, and it would be a waste to copy it other and other again...
				return suffix.Count > 0 ? suffix : data.Array == null ? default : Slice.Empty;
			}

			data.EnsureSliceIsValid();
			suffix.EnsureSliceIsValid();

			var slice = Allocate(data.Count + suffix.Count, aligned);
			data.CopyTo(slice.Span);
			suffix.CopyTo(slice.Span.Slice(data.Count));
			return slice;
		}

		/// <summary>Adds a buffer to the list of allocated slices</summary>
		private void Keep(Slice chunk)
		{
			if (m_chunks == null) m_chunks = new List<Slice>();
			m_chunks.Add(chunk);
			m_allocated += chunk.Array.Length;
			m_used += chunk.Count;
		}

		/// <summary>Reset the buffer to its initial state</summary>
		/// <remarks>None of the existing buffers are kept, but the new page will keep the current page size (that may be larger than the initial size).
		/// Any previously allocated slice from this buffer will be untouched.</remarks>
		public void Reset()
		{
			Reset(keep: false);
		}

		/// <summary>Reset the buffer to its initial state, and reuse the current page.</summary>
		/// <remarks>This recycle the current page. The caller should ensure that nobody has a reference to a previously generated slice.</remarks>
		public void ResetUnsafe()
		{
			Reset(keep: true);
		}

		private void Reset(bool keep)
		{
			m_chunks = null;
			m_allocated = 0;
			m_pos = 0;
			m_used = 0;
			if (!keep) m_current = null;
		}

		/// <summary>Return a lock that will prevent the underlying byte arrays used by this buffer from moving around in memory during the next GC.</summary>
		/// <returns>Lock instance that MUST be disposed to release the GC lock.</returns>
		/// <remarks>Any data added to the buffer WHILE the buffer is pinned MAY NOT be pinned itself! For safety, caller should make sure to write everything to the buffer before pinning it</remarks>
		public Slice.Pinned Pin()
		{
			return new Slice.Pinned(this, m_current, m_chunks);
		}

	}

}

#endif
