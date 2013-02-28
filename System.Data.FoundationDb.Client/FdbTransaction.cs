using Microsoft.Win32.SafeHandles;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace System.Data.FoundationDb.Client
{

	public class FdbTransaction : IDisposable
	{
		private FdbDatabase m_database;
		private TransactionHandle m_handle;
		private bool m_disposed;

		internal FdbTransaction(FdbDatabase database, TransactionHandle handle)
		{
			m_database = database;
			m_handle = handle;
		}

		public FdbDatabase Database { get { return m_database; } }

		internal TransactionHandle Handle { get { return m_handle; } }

		private byte[] GetKeyBytes(string key)
		{
			return Encoding.Default.GetBytes(key);
		}

		public void Set(string key, byte[] value)
		{
			ThrowIfDisposed();
			FdbNativeStub.TransactionSet(m_handle, GetKeyBytes(key), value);
		}

		public void Set(string key, string value)
		{
			ThrowIfDisposed();
			FdbNativeStub.TransactionSet(m_handle, GetKeyBytes(key), Encoding.UTF8.GetBytes(value));
		}

		public Task CommitAsync()
		{
			ThrowIfDisposed();
			var future = FdbNativeStub.TransactionCommit(m_handle);

			return FdbFuture<object>
				.FromHandle(future,
				(h) =>
				{
					var err = FdbNativeStub.FutureGetError(h.Handle);
					if (err != FdbError.Success)
					{
						throw FdbCore.MapToException(err);
					}
					return null;
				})
				.Task;
		}

		private void ThrowIfDisposed()
		{
			if (m_disposed) throw new ObjectDisposedException(null);
		}

		public void Dispose()
		{
			if (!m_disposed)
			{
				m_disposed = true;
				m_handle.Dispose();
			}
		}
	}

}
