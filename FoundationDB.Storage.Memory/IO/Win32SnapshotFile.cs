#region Copyright (c) 2013-2014, Doxense SAS. All rights reserved.
// See License.MD for license information
#endregion

namespace FoundationDB.Storage.Memory.API
{
	using System;
	using System.Diagnostics.Contracts;
	using System.IO;
	using System.Threading;
	using System.Threading.Tasks;

	internal sealed class Win32SnapshotFile : IDisposable
	{
		private readonly string m_path;
		private FileStream m_fs;

		public const int SECTOR_SIZE = 4096;

		public Win32SnapshotFile(string path)
		{
			if (string.IsNullOrEmpty(path)) throw new ArgumentNullException("path");

			path = Path.GetFullPath(path);
			m_path = path;
			
			FileStream fs = null;
			try
			{
				fs = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read, SECTOR_SIZE, FileOptions.Asynchronous | FileOptions.WriteThrough | (FileOptions)0x20000000/* NO_BUFFERING */);
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

		/// <summary>Write full sectors to the file</summary>
		/// <param name="buffer">Buffer that contains the data to write</param>
		/// <param name="count">Number of bytes in the buffer</param>
		/// <param name="cancellationToken">Optional cancellation token</param>
		/// <returns>Number of bytes written to the disk (always a multiple of 4K), or 0 if the buffer did not contain enough data</returns>
		public async Task<int> WriteAsync(byte[] buffer, int count, CancellationToken cancellationToken)
		{
			if (m_fs == null) throw new ObjectDisposedException(this.GetType().Name);

			int complete = (count / SECTOR_SIZE) * SECTOR_SIZE;
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
				int complete = (count / SECTOR_SIZE) * SECTOR_SIZE;
				if (complete > 0)
				{
					await m_fs.WriteAsync(buffer, 0, complete, cancellationToken).ConfigureAwait(false);
					count -= complete;
				}
				if (count > 0)
				{ // we have to write full 4K sectors, so we'll need to copy the rest to a temp 4K buffer (padded with 0s)
					byte[] tmp = new byte[SECTOR_SIZE];
					Buffer.BlockCopy(buffer, complete, tmp, 0, count);
					await m_fs.WriteAsync(tmp, 0, tmp.Length, cancellationToken).ConfigureAwait(false);
				}
			}
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
			}
		}
	}

}
