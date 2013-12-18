#region Copyright (c) 2013-2014, Doxense SAS. All rights reserved.
// See License.MD for license information
#endregion

namespace FoundationDB.Storage.Memory.Utils
{
	using System;
	using System.Diagnostics.Contracts;
	using System.IO;
	using System.Runtime.InteropServices;
	using System.Text;
	using System.Threading;
	using System.Threading.Tasks;

	/// <summary>Stream that can read from a slice of unmanaged memory</summary>
	public unsafe sealed class UnmanagedSliceStream : Stream
	{
		private byte* m_begin;
		private uint m_pos;
		private readonly uint m_size;
		private Task<int> m_lastReadTask;

		internal UnmanagedSliceStream(USlice slice)
		{
			Contract.Requires(slice.Count == 0 || slice.Data != null);

			m_begin = slice.Data;
			m_size = slice.Count;
		}

		internal UnmanagedSliceStream(byte* data, uint size)
		{
			Contract.Requires(size == 0 || data != null);

			m_begin = data;
			m_size = size;
		}

		public override bool CanRead
		{
			get { return m_begin != null; }
		}

		public override bool CanSeek
		{
			get { return true; }
		}

		public override bool CanWrite
		{
			get { return false; }
		}

		public override void Flush()
		{
			//NO OP
		}

		public override Task FlushAsync(CancellationToken cancellationToken)
		{
			return TaskHelpers.CompletedTask;
		}

		public override long Length
		{
			get { return m_size; }
		}

		public override long Position
		{
			get
			{
				return m_pos;
			}
			set
			{
				Seek(value, SeekOrigin.Begin);
			}
		}

		public override int ReadByte()
		{
			if (m_begin == null) ThrowDisposed();
			uint pos = m_pos;
			if (pos < m_size)
			{
				int res = (int)m_begin[pos];
				m_pos = pos + 1;
				return res;
			}
			return -1;
		}

		public override int Read(byte[] buffer, int offset, int count)
		{
			if (m_begin == null) ThrowDisposed();

			if (buffer == null) throw new ArgumentNullException("buffer");
			if (offset < 0 || offset > buffer.Length) throw new ArgumentOutOfRangeException("offset");
			if (count < 0 || offset + count >= buffer.Length) throw new ArgumentOutOfRangeException("count");

			uint pos = m_pos;
			if (pos >= m_size) return 0; // EOF

			uint chunk;
			checked { chunk = (uint)Math.Max(m_size - pos, count); }

			if (chunk > 0)
			{
				fixed (byte* ptr = buffer)
				{
					UnmanagedHelpers.CopyUnsafe(ptr + offset, m_begin + pos, chunk);
				}
				m_pos = pos + chunk;
			}
			return (int)chunk;
		}

		public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
		{
			if (cancellationToken.IsCancellationRequested)
			{
				return TaskHelpers.FromCancellation<int>(cancellationToken);
			}
			try
			{
				int result = Read(buffer, offset, count);
				var t = m_lastReadTask;
				return t != null && t.Result == result ? t : (t = Task.FromResult(result));
			}
			catch (Exception e)
			{
				return TaskHelpers.FromException<int>(e);
			}
		}

		public override long Seek(long offset, SeekOrigin origin)
		{
			if (m_begin == null) ThrowDisposed();

			switch (origin)
			{
				case SeekOrigin.Begin:
				{
					if (offset < 0) throw new ArgumentOutOfRangeException("offset", "Offset cannot be less than zero");
					offset = offset >= m_size ? m_size : offset;
					Contract.Assert(offset >= 0);
					m_pos = (uint)offset;
					return m_pos;
				}
				case SeekOrigin.End:
				{
					if (offset < 0) throw new ArgumentOutOfRangeException("offset", "Offset cannot be less than zero");
					offset += m_size;
					offset = offset < 0 ? 0 : offset;
					Contract.Assert(offset >= 0);
					m_pos = (uint)offset;
					return m_pos;
				}
				case SeekOrigin.Current:
				{
					offset += m_pos;
					offset = offset < 0 ? 0 : offset >= m_size ? m_size : offset;
					Contract.Assert(offset >= 0);
					m_pos = (uint)offset;
					return m_pos;
				}
				default:
				{
					throw new ArgumentOutOfRangeException("origin");
				}
			}
		}

		public override void SetLength(long value)
		{
			throw new NotSupportedException("Cannot set the length of a read-only stream");
		}

		public override void Write(byte[] buffer, int offset, int count)
		{
			throw new NotSupportedException("Cannot write to a read-only stream");
		}

		public override Task WriteAsync(byte[] buffer, int offset, int count, System.Threading.CancellationToken cancellationToken)
		{
			return TaskHelpers.FromException<object>(new NotSupportedException("Cannot write to a read-only stream"));
		}

		public byte[] ToArray()
		{
			if (m_begin == null) ThrowDisposed();
			var tmp = new byte[m_size];
			if (tmp.Length > 0)
			{
				fixed (byte* ptr = tmp)
				{
					UnmanagedHelpers.CopyUnsafe(ptr, m_begin, (uint)m_size);
				}
			}
			return tmp;
		}

		public FoundationDB.Client.Slice ToSlice()
		{
			return FoundationDB.Client.Slice.Create(this.ToArray());
		}

		public USlice ToUSlice()
		{
			if (m_begin == null) ThrowDisposed();
			return new USlice(m_begin, m_size);
		}

		private void ThrowDisposed()
		{
			throw new ObjectDisposedException(this.GetType().Name);
		}

		protected override void Dispose(bool disposing)
		{
			m_begin = null;
			m_pos = m_size;
			m_lastReadTask = null;
		}
	}

}
