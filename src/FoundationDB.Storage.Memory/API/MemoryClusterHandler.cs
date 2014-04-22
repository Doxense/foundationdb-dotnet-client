#region Copyright (c) 2013-2014, Doxense SAS. All rights reserved.
// See License.MD for license information
#endregion

namespace FoundationDB.Storage.Memory.API
{
	using FoundationDB.Client;
	using FoundationDB.Client.Core;
	using System;
	using System.Threading;
	using System.Threading.Tasks;

	internal class MemoryClusterHandler : IFdbClusterHandler, IDisposable
	{

		private bool m_disposed;

		public MemoryClusterHandler()
		{
			//TODO ?
		}

		public bool IsInvalid
		{
			get { return false; }
		}

		public bool IsClosed
		{
			get { return m_disposed; }
		}

		public void SetOption(FdbClusterOption option, Slice data)
		{
			throw new NotImplementedException();
		}

		public Task<IFdbDatabaseHandler> OpenDatabaseAsync(string databaseName, CancellationToken cancellationToken)
		{
			throw new NotImplementedException();
		}

		public void Dispose()
		{
			if (!m_disposed)
			{
				m_disposed = true;
				//TODO
			}

			GC.SuppressFinalize(this);
		}
	}

}
