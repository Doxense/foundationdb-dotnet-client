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
	using System.IO;
	using System.Threading.Tasks;
	using Doxense.Diagnostics.Contracts;
	using JetBrains.Annotations;

	/// <summary>Stream that wraps a Slice for reading</summary>
	/// <remarks>This stream is optimized for blocking and async reads</remarks>
	public sealed class SliceStream : Stream
	{
		private Slice m_slice;
		private int m_position;
		private Task<int> m_lastTask;

		/// <summary>Creates a new stream that reads from an underlying slice</summary>
		public SliceStream(Slice slice)
		{
			m_slice = slice;
		}

		#region Seeking...

		/// <summary>Returns true if the underlying slice is not null</summary>
		public override bool CanSeek => m_slice.HasValue;

		/// <summary>Gets or sets the current position in the underlying slice</summary>
		public override long Position
		{
			get => m_position;
			set => Seek(value, SeekOrigin.Begin);
		}

		/// <summary>Getes the length of the underlying slice</summary>
		public override long Length => m_slice.Count;

		/// <summary>Seeks to a specific location in the underlying slice</summary>
		public override long Seek(long offset, SeekOrigin origin)
		{
			if (!m_slice.HasValue) StreamIsClosed();
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

		/// <summary>This methods is not supported</summary>
		public override void SetLength(long value)
		{
			throw new NotSupportedException();
		}

		#endregion

		#region Reading...

		/// <summary>Returns true unless the current position is after the end of the underlying slice</summary>
		public override bool CanRead => m_position < m_slice.Count;

		/// <summary>Reads from byte from the underyling slice and advances the position within the slice by one byte, or returns -1 if the end of the slice has been reached.</summary>
		public override int ReadByte()
		{
			Contract.Ensures(m_position >= 0 && m_position <= m_slice.Count);

			if (m_position < m_slice.Count)
			{
				return m_slice[m_position++];
			}
			return -1;
		}

		/// <summary>Reads a sequence of bytes from the underlying slice and advances the position within the slice by the number of bytes that are read.</summary>
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

		/// <summary>Asynchronously reads a sequence of bytes from the underlying slice and advances the position within the slice by the number of bytes read.</summary>
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

		/// <summary>Asynchronously reads the bytes from the underlying slice and writes them to another stream, using a specified buffer size and cancellation token.</summary>
		public override Task CopyToAsync(Stream destination, int bufferSize, System.Threading.CancellationToken ct)
		{
			Contract.Ensures(m_position >= 0 && m_position <= m_slice.Count);

			Contract.NotNull(destination, nameof(destination));
			if (!destination.CanWrite) throw new ArgumentException("The destination stream cannot be written to", nameof(destination));

			int remaining = m_slice.Count - m_position;
			if (remaining <= 0) return Task.CompletedTask;

			// simulate the read
			m_position += remaining;

			// we can write everyting in one go, so just call WriteAsync and return that
			return destination.WriteAsync(m_slice.Array, m_slice.Offset, remaining, ct);
		}

		#endregion

		#region Writing...

		/// <summary>Always return false</summary>
		public override bool CanWrite => false;

		public override void WriteByte(byte value)
		{
			throw new NotSupportedException();
		}

		/// <summary>This methods is not supported</summary>
		public override void Write(byte[] buffer, int offset, int count)
		{
			throw new NotSupportedException();
		}

		/// <summary>This methods is not supported</summary>
		public override Task WriteAsync(byte[] buffer, int offset, int count, System.Threading.CancellationToken ct)
		{
			return Task.FromException<object>(new NotSupportedException());
		}

		/// <summary>This methods does nothing.</summary>
		public override void Flush()
		{
			// Not supported, but don't throw here
		}

		/// <summary>This methods does nothing.</summary>
		public override Task FlushAsync(System.Threading.CancellationToken ct)
		{
			// Not supported, but don't throw here
			return Task.CompletedTask;
		}

		#endregion

		private static void ValidateBuffer(byte[] buffer, int offset, int count)
		{
			Contract.NotNull(buffer, nameof(buffer));
			if (count < 0) throw ThrowHelper.ArgumentOutOfRangeException(nameof(count), "Count cannot be less than zero.");
			if ((uint) offset > buffer.Length - count) throw ThrowHelper.ArgumentException(nameof(offset), "Buffer is too small.");
		}

		[ContractAnnotation("=> halt")]
		private static void StreamIsClosed()
		{
			throw ThrowHelper.ObjectDisposedException("The stream was already closed");
		}

		/// <summary>Closes the stream</summary>
		protected override void Dispose(bool disposing)
		{
			m_slice = default;
			m_position = 0;
			m_lastTask = null;
		}

	}

}

#endif
