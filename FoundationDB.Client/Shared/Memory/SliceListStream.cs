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

#if !USE_SHARED_FRAMEWORK

namespace Doxense.Memory
{
	using System;
	using System.Collections.Generic;
	using System.IO;
	using System.Linq;
	using System.Runtime.CompilerServices;
	using System.Threading.Tasks;
	using Doxense.Diagnostics.Contracts;
	using JetBrains.Annotations;

	//REVIEW: this is somewhat similar to the proposed new IBufferList (from System.IO.Pipeline) ?

	/// <summary>Merge multiple slices into a single stream</summary>
	public sealed class SliceListStream : Stream
	{
		private Slice[] m_slices;
		private long m_position;
		private long m_length;
		private int m_indexOfCurrentSlice;
		private int m_offsetInCurrentSlice;
		private Task<int> m_lastTask;

		internal SliceListStream([NotNull] Slice[] slices)
		{
			Contract.NotNull(slices, nameof(slices));
			Init(slices);
		}

		public SliceListStream([NotNull] IEnumerable<Slice> slices)
		{
			Contract.NotNull(slices, nameof(slices));
			Init(slices.ToArray());
		}

		private void Init([NotNull] Slice[] slices)
		{
			m_slices = slices;
			long total = 0;
			foreach (var slice in slices)
			{
				total += slice.Count;
			}
			m_length = total;
		}

		#region Seeking...

		public override bool CanSeek => m_slices != null;

		public override long Position
		{
			get => m_position;
			set => Seek(value, SeekOrigin.Begin);
		}

		public override long Length => m_length;

		public override long Seek(long offset, SeekOrigin origin)
		{
			if (m_slices == null) throw StreamIsClosed();
			if (offset > int.MaxValue) throw new ArgumentOutOfRangeException(nameof(offset));

			switch (origin)
			{
				case SeekOrigin.Begin:
				{
					break;
				}
				case SeekOrigin.Current:
				{
					offset += m_position;
					break;
				}
				case SeekOrigin.End:
				{
					offset = m_length - offset;
					break;
				}
				default:
				{
					throw new ArgumentException("origin");
				}
			}

			if (offset < 0) throw new IOException("Cannot seek before the beginning of the slice");

			// clip to the slice bounds
			offset = Math.Min(offset, m_length);

			// find the slice that contains the desired position
			long p = 0;
			int n = 0;
			foreach (var slice in m_slices)
			{
				if (p + slice.Count > offset)
				{
					m_indexOfCurrentSlice = n;
					m_offsetInCurrentSlice = (int)(offset - p);
					break;
				}
				p += slice.Count;
				++n;
			}

			m_position = (int)offset;

			Contract.Ensures(m_position >= 0 && m_position <= m_length);

			return offset;
		}

		public override void SetLength(long value)
		{
			throw new NotSupportedException();
		}

		#endregion

		#region Reading...

		public override bool CanRead => m_position < m_length;

		private bool AdvanceToNextSlice()
		{
			if (m_indexOfCurrentSlice >= m_slices.Length) return false;

			m_offsetInCurrentSlice = 0;
			++m_indexOfCurrentSlice;

			// skip empty slices
			while (m_indexOfCurrentSlice < m_slices.Length && m_slices[m_indexOfCurrentSlice].IsNullOrEmpty)
			{
				++m_indexOfCurrentSlice;
			}
			return m_indexOfCurrentSlice < m_slices.Length;
		}

		public override int ReadByte()
		{
			Contract.Ensures(m_position >= 0 && m_position <= m_length);

			if (m_position >= m_length || (m_offsetInCurrentSlice >= m_slices[m_indexOfCurrentSlice].Count && !AdvanceToNextSlice()))
			{ // EOF
				return -1;
			}

			int offset = m_offsetInCurrentSlice;
			int res = m_slices[m_indexOfCurrentSlice][offset];
			++m_position;
			m_offsetInCurrentSlice = offset + 1;

			return res;
		}

		public override int Read(byte[] buffer, int offset, int count)
		{
			ValidateBuffer(buffer, offset, count);

			if (m_slices == null) throw StreamIsClosed();

			Contract.Ensures(m_position >= 0 && m_position <= m_length);

			if (m_position >= m_length) return 0;

			int read = 0;

			while (count > 0)
			{
				if (m_offsetInCurrentSlice >= m_slices[m_indexOfCurrentSlice].Count && !AdvanceToNextSlice())
				{
					break;
				}

				var slice = m_slices[m_indexOfCurrentSlice];

				int remaining = Math.Min(slice.Count - m_offsetInCurrentSlice, count);
				if (remaining <= 0) return 0;

				int pos = m_offsetInCurrentSlice;
				int start = slice.Offset + pos;

				if (remaining <= 8)
				{ // too small, copy it ourselves
					int n = remaining;
					while (n-- > 0)
					{
						buffer[offset + n] = slice.Array[start + n];
					}
				}
				else
				{ // large enough
					Buffer.BlockCopy(slice.Array, start, buffer, offset, remaining);
				}

				m_offsetInCurrentSlice += remaining;
				m_position += remaining;
				offset += remaining;
				read += remaining;
				count -= remaining;
			}

			Contract.Ensures(m_position >= 0 && m_position <= m_length);
			return read;
		}

		public override Task<int> ReadAsync(byte[] buffer, int offset, int count, System.Threading.CancellationToken ct)
		{
			ValidateBuffer(buffer, offset, count);

			if (ct.IsCancellationRequested)
			{
				return Task.FromCanceled<int>(ct);
			}

			try
			{
				int n = Read(buffer, offset, count);

				var task = m_lastTask;
				return task != null && task.Result == n ? task : (m_lastTask = Task.FromResult(n));

			}
			catch (Exception e)
			{
				return Task.FromException<int>(e);
			}
		}

		#endregion

		#region Writing...

		public override bool CanWrite => false;

		public override void Write(byte[] buffer, int offset, int count)
		{
			throw new NotSupportedException();
		}

		public override Task WriteAsync(byte[] buffer, int offset, int count, System.Threading.CancellationToken ct)
		{
			return Task.FromException<object>(new NotSupportedException());
		}

		public override void Flush()
		{
			// Not supported, but don't throw here
		}

		public override Task FlushAsync(System.Threading.CancellationToken ct)
		{
			// Not supported, but don't throw here
			return Task.CompletedTask;
		}

		#endregion

		private static void ValidateBuffer(byte[] buffer, int offset, int count)
		{
			Contract.NotNull(buffer, nameof(buffer));
			if (count < 0) throw ThrowHelper.ArgumentOutOfRangeException(nameof(count), "Count cannot be less than zero");
			if (offset < 0) throw ThrowHelper.ArgumentOutOfRangeException(nameof(offset), "Offset cannot be less than zero");
			if (offset > buffer.Length - count) throw ThrowHelper.ArgumentException(nameof(offset), "Offset and count must fit inside the buffer");
		}

		[Pure, NotNull, MethodImpl(MethodImplOptions.NoInlining)]
		private static Exception StreamIsClosed()
		{
			return ThrowHelper.ObjectDisposedException("The stream was already closed");
		}

		protected override void Dispose(bool disposing)
		{
			m_slices = null;
			m_position = 0;
			m_length = 0;
			m_indexOfCurrentSlice = 0;
			m_offsetInCurrentSlice = 0;
			m_lastTask = null;
		}

	}

}

#endif
