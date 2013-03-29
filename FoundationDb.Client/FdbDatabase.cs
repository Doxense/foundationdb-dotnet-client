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
	* Neither the name of the <organization> nor the
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

using Microsoft.Win32.SafeHandles;
using System;
using System.Collections.Generic;
using FoundationDb.Client.Native;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace FoundationDb.Client
{

	/// <summary>FoundationDB Database</summary>
	/// <remarks>Wraps an FDBDatabase* handle</remarks>
	public class FdbDatabase : IDisposable
	{

		private readonly FdbCluster m_cluster;
		private readonly DatabaseHandle m_handle;
		private readonly string m_name;
		private readonly bool m_ownsCluster;
		private bool m_disposed;

		//TODO: keep track of all pending transactions on this db that are still alive

		internal FdbDatabase(FdbCluster cluster, DatabaseHandle handle, string name, bool ownsCluster)
		{
			m_cluster = cluster;
			m_handle = handle;
			m_name = name;
			m_ownsCluster = ownsCluster;
		}

		public FdbCluster Cluster { get { return m_cluster; } }

		public string Name { get { return m_name; } }

		internal DatabaseHandle Handle { get { return m_handle; } }

		public FdbTransaction BeginTransaction()
		{
			if (m_handle.IsInvalid) throw new InvalidOperationException("Cannot create a transaction on an invalid database");

			TransactionHandle handle;
			var err = FdbNative.DatabaseCreateTransaction(m_handle, out handle);
			if (Fdb.Failed(err))
			{
				handle.Dispose();
				throw Fdb.MapToException(err);
			}

			//TODO: register this transation
			return new FdbTransaction(this, handle);
		}

		internal void EnsureCheckTransactionIsValid(FdbTransaction transaction)
		{
			ThrowIfDisposed();
			//TODO: enroll this transaction in a list of pending transactions ?
		}

		internal void RegisterTransaction(FdbTransaction transaction)
		{
			//TODO !
		}

		internal void UnregisterTransaction(FdbTransaction transaction)
		{
			//TODO !
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
				//TODO: kill all pending transactions on this db? 
				m_handle.Dispose();
				if (m_ownsCluster) m_cluster.Dispose();
			}
		}

	}

}
