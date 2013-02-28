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

	public class FdbDatabase : IDisposable
	{

		private FdbCluster m_cluster;
		private DatabaseHandle m_handle;
		private string m_name;
		private bool m_disposed;

		internal FdbDatabase(FdbCluster cluster, DatabaseHandle handle, string name)
		{
			m_cluster = cluster;
			m_handle = handle;
			m_name = name;
		}

		public FdbCluster Cluster { get { return m_cluster; } }

		public string Name { get { return m_name; } }

		internal DatabaseHandle Handle { get { return m_handle; } }

		public FdbTransaction BeginTransaction()
		{
			return FdbCore.CreateTransaction(this);
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
