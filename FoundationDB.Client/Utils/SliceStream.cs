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

namespace FoundationDB.Client
{
	using FoundationDB.Async;
	using FoundationDB.Client.Utils;
	using System;
	using System.IO;
	using System.Threading.Tasks;

	/// <summary>Stream that wraps a Slice for reading</summary>
	/// <remarks>This stream is optimized for blocking and async reads</remarks>
	public sealed class SliceStream : Stream
	{
		private Slice m_slice;
		private int m_position;
		private Task<int> m_lastTask;

		public SliceStream(Slice slice)
		{
			m_slice = slice;
		}

		#region Seeking...

		public override bool CanSeek
		{
			get { return m_slice.HasValue; }
		}

		public override long Position
		{
			get
			{
				return m_position;
			}
			set
			{
				Seek(value, SeekOrigin.Begin);
			}
		}

		public override long Length
		{
			get { return m_slice.Count; }
		}

		public override long Seek(long offset, SeekOrigin origin)
		{
			if (!m_slice.HasValue) StreamIsClosed();
			if (offset > int.MaxValue) throw new ArgumentOutOfRangeException("offset");

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
						offset = m_slice.Count - offset;
						break;
					}
				default:
					{
						throw new ArgumentException("origin");
					}
			}

			if (offset < 0) throw new IOException("Cannot seek before the beginning of the slice");

			// clip to the slice bounds
			offset = Math.Min(offset, m_slice.Count);

			m_position = (int)offset;

			Contract.Ensures(m_position >= 0 && m_position <= m_slice.Count);

			return offset;

		}

		public override void SetLength(long value)
		{
			throw new NotSupportedException();
		}

		#endregion

		#region Reading...

		public override bool CanRead
		{
			get { return m_position < m_slice.Count; }
		}

		public override int ReadByte()
		{
			Contract.Ensures(m_position >= 0 && m_position <= m_slice.Count);

			if (m_position < m_slice.Count)
			{
				return m_slice[m_position++];
			}
			return -1;
		}

		public override int Read(byte[] buffer, int offset, int count)
		{
			ValidateBuffer(buffer, offset, count);

			if (!m_slice.HasValue) StreamIsClosed();

			Contract.Ensures(m_position >= 0 && m_position <= m_slice.Count);

			int remaining = Math.Min(m_slice.Count - m_position, count);
			if (remaining <= 0) return 0;

			int pos = m_position;
			int start = m_slice.Offset + pos;

			if (remaining <= 8)
			{ // too small, copy it ourselves
				int n = remaining;
				while (n-- > 0)
				{
					buffer[offset + n] = m_slice.Array[start + n];
				}
			}
			else
			{ // large enough
				Buffer.BlockCopy(m_slice.Array, start, buffer, offset, remaining);
			}

			m_position += remaining;
			Contract.Ensures(m_position >= 0 && m_position <= m_slice.Count);
			return remaining;
		}

#if !NET_4_0

		public override Task<int> ReadAsync(byte[] buffer, int offset, int count, System.Threading.CancellationToken cancellationToken)
		{
			ValidateBuffer(buffer, offset, count);

			if (cancellationToken.IsCancellationRequested)
			{
				return TaskHelpers.FromCancellation<int>(cancellationToken);
			}

			try
			{
				int n = Read(buffer, offset, count);

				var task = m_lastTask;
				return task != null && task.Result == n ? task : (m_lastTask = Task.FromResult(n));

			}
			catch (Exception e)
			{
				return TaskHelpers.FromException<int>(e);
			}
		}

		public override Task CopyToAsync(Stream destination, int bufferSize, System.Threading.CancellationToken cancellationToken)
		{
			Contract.Ensures(m_position >= 0 && m_position <= m_slice.Count);

			if (destination == null) throw new ArgumentNullException("destination");
			if (!destination.CanWrite) throw new ArgumentException("The destination stream cannot be written to", "destination");

			int remaining = m_slice.Count - m_position;
			if (remaining <= 0) return TaskHelpers.CompletedTask;

			// simulate the read
			m_position += remaining;

			// we can write everyting in one go, so just call WriteAsync and return that
			return destination.WriteAsync(m_slice.Array, m_slice.Offset, remaining);
		}

#endif

		#endregion

		#region Writing...

		public override bool CanWrite
		{
			get { return false; }
		}

		public override void Write(byte[] buffer, int offset, int count)
		{
			throw new NotSupportedException();
		}

#if !NET_4_0

		public override Task WriteAsync(byte[] buffer, int offset, int count, System.Threading.CancellationToken cancellationToken)
		{
			return TaskHelpers.FromException<object>(new NotSupportedException());
		}

#endif

		public override void Flush()
		{
			// Not supported, but don't throw here
		}

		public override Task FlushAsync(System.Threading.CancellationToken cancellationToken)
		{
			// Not supported, but don't throw here
			return TaskHelpers.CompletedTask;
		}

		#endregion

		private static void ValidateBuffer(byte[] buffer, int offset, int count)
		{
			if (buffer == null) throw new ArgumentNullException("buffer");
			if (count < 0) throw new ArgumentOutOfRangeException("count", "Count cannot be less than zero");
			if (offset < 0) throw new ArgumentOutOfRangeException("offset", "Offset cannot be less than zero");
			if (offset > buffer.Length - count) throw new ArgumentException("Offset and count must fit inside the buffer");
		}

		private static void StreamIsClosed()
		{
			throw new ObjectDisposedException(null, "The stream was already closed");
		}

		protected override void Dispose(bool disposing)
		{
			m_slice = Slice.Nil;
			m_position = 0;
			m_lastTask = null;
		}

	}

}
