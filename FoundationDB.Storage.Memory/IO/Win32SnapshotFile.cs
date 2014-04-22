#region Copyright (c) 2013-2014, Doxense SAS. All rights reserved.
// See License.MD for license information
#endregion

namespace FoundationDB.Storage.Memory.IO
{
	using System;
	using System.Diagnostics.Contracts;
	using System.IO;
	using System.Threading;
	using System.Threading.Tasks;

	internal sealed class Win32SnapshotFile : IDisposable
	{
		private readonly string m_path;
		private readonly int m_pageSize;
		private FileStream m_fs;
		private byte[] m_scratch;

		public const int SECTOR_SIZE = 4096;

		public Win32SnapshotFile(string path)
			: this(path, SECTOR_SIZE)
		{ }

		public Win32SnapshotFile(string path, int pageSize)
		{
			if (string.IsNullOrEmpty(path)) throw new ArgumentNullException("path");
			if (pageSize < 512) throw new ArgumentException("Page size must be at least 512.", "pageSize");
			if (pageSize == 0) pageSize = SECTOR_SIZE;
			//TODO: check that pageSize is a power of two ??

			path = Path.GetFullPath(path);
			m_path = path;
			m_pageSize = pageSize;
			
			FileStream fs = null;
			try
			{
				fs = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read, pageSize, FileOptions.Asynchronous | FileOptions.WriteThrough | (FileOptions)0x20000000/* NO_BUFFERING */);
			}
			catch(Exception)
			{
				if (fs != null)
				{
					fs.Dispose();
					fs = null;
				}
				throw;
			}
			finally
			{
				m_fs = fs;
			}

			Contract.Ensures(m_fs != null && m_fs.IsAsync);
		}

		public long Length
		{
			get
			{
				var fs = m_fs;
				return fs != null ? fs.Length : 0;
			}
		}

		public void Seek(long position)
		{
			Contract.Requires(position >= 0);

			var pos = m_fs.Seek(position, SeekOrigin.Begin);
			if (pos != position) throw new IOException("Failed to seek to the desired position");
		}

		/// <summary>Read a certain number of bytes into a buffer</summary>
		/// <param name="buffer">Buffer where to store the data</param>
		/// <param name="offset">Offset in the buffer where the data will be written</param>
		/// <param name="count">Number of bytes to read</param>
		/// <param name="cancellationToken"></param>
		/// <returns>Number of bytes read. If it is less than <paramref name="count"/>, it means the file was truncated.</returns>
		/// <remarks>May execute more than one read operation if the first one did not return enough data (reading from a network stream or NFS share??)</remarks>
		public async Task<int> ReadExactlyAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
		{
			if (m_fs == null) throw new ObjectDisposedException(this.GetType().Name);

			int p = 0;
			int r = count;
			while (r > 0)
			{
				int n = await m_fs.ReadAsync(buffer, offset + p, r, cancellationToken).ConfigureAwait(false);
				if (n == 0) break;
				r -= n;
				p += n;
			}
			return p;
		}

		/// <summary>Write as many full pages to the file</summary>
		/// <param name="buffer">Buffer that contains the data to write</param>
		/// <param name="count">Number of bytes in the buffer (that may or may not be aligned to a page size)</param>
		/// <param name="cancellationToken">Optional cancellation token</param>
		/// <returns>Number of bytes written to the disk (always a multiple of 4K), or 0 if the buffer did not contain enough data.</returns>
		public async Task<int> WriteCompletePagesAsync(byte[] buffer, int count, CancellationToken cancellationToken)
		{
			if (m_fs == null) throw new ObjectDisposedException(this.GetType().Name);

			int complete = (count / m_pageSize) * m_pageSize;
			if (complete > 0)
			{
				await m_fs.WriteAsync(buffer, 0, complete, cancellationToken).ConfigureAwait(false);
			}

			return complete;
		}

		/// <summary>Flush the remaining of the buffer to the disk, and ensures that the content has been fsync'ed</summary>
		/// <param name="buffer">Buffer that may contains data (can be null if <paramref name="count"/> is equal to 0)</param>
		/// <param name="count">Number of bytes remaining in the buffer (or 0 if there is no more data to written)</param>
		/// <param name="cancellationToken"></param>
		/// <returns></returns>
		public async Task FlushAsync(byte[] buffer, int count, CancellationToken cancellationToken)
		{
			Contract.Assert(count == 0 || buffer != null);

			if (count > 0)
			{
				int complete = (count / m_pageSize) * m_pageSize;
				if (complete > 0)
				{
					await m_fs.WriteAsync(buffer, 0, complete, cancellationToken).ConfigureAwait(false);
					count -= complete;
				}
				if (count > 0)
				{ // we have to write full 4K sectors, so we'll need to copy the rest to a temp 4K buffer (padded with 0s)
					var tmp = m_scratch ?? (m_scratch = new byte[m_pageSize]);
					Buffer.BlockCopy(buffer, complete, tmp, 0, count);
					await m_fs.WriteAsync(tmp, 0, tmp.Length, cancellationToken).ConfigureAwait(false);
				}
			}
			//REVIEW: since we are using WRITE_THROUGH + NO_BUFFERING, the OS is *supposed* to write directly to the disk ... 
			// need to verify that this is actually the case!
			await m_fs.FlushAsync(cancellationToken);
		}

		public override string ToString()
		{
			return "Snapshot:" + m_path;
		}

		public void Dispose()
		{
			try
			{
				var fs = m_fs;
				if (fs != null) fs.Close();
			}
			finally
			{
				m_fs = null;
				m_scratch = null;
			}
		}
	}

}
