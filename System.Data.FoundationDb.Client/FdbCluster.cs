using System;
using System.Threading.Tasks;

namespace System.Data.FoundationDb.Client
{

	public class FdbCluster : IDisposable
	{

		private ClusterHandle m_handle;
		private bool m_disposed;

		internal FdbCluster(ClusterHandle handle)
		{
			m_handle = handle;
		}

		internal ClusterHandle Handle { get { return m_handle; } }

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

		public Task<FdbDatabase> OpenDatabaseAsync(string dbName)
		{
			ThrowIfDisposed();
			return FdbCore.CreateDatabaseAsync(this, dbName);
		}

	}

}
