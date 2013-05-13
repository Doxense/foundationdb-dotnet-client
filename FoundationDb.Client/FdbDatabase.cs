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
using System.Collections.Concurrent;
using FoundationDb.Client.Native;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Diagnostics;

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
		private readonly CancellationTokenSource m_cts = new CancellationTokenSource();
		private bool m_disposed;

		private static int s_transactionCounter;
		private readonly ConcurrentDictionary<int, FdbTransaction> m_transactions = new ConcurrentDictionary<int, FdbTransaction>();

		//TODO: keep track of all pending transactions on this db that are still alive

		internal FdbDatabase(FdbCluster cluster, DatabaseHandle handle, string name, bool ownsCluster)
		{
			m_cluster = cluster;
			m_handle = handle;
			m_name = name;
			m_ownsCluster = ownsCluster;
		}

		public FdbCluster Cluster { get { return m_cluster; } }

		/// <summary>Name of the database</summary>
		public string Name { get { return m_name; } }

		/// <summary>Handle to the underlying FDB_DATABASE*</summary>
		internal DatabaseHandle Handle { get { return m_handle; } }

		/// <summary>Source or cancellation that is linked with the lifetime of this database</summary>
		/// <remarks>Will be cancelled when Dispose() is called</remarks>
		internal CancellationTokenSource CancellationSource { get { return m_cts; } }

		public FdbTransaction BeginTransaction()
		{
			if (m_handle.IsInvalid) throw new InvalidOperationException("Cannot create a transaction on an invalid database");

			ThrowIfDisposed();

			int id = Interlocked.Increment(ref s_transactionCounter);

			TransactionHandle handle;
			var err = FdbNative.DatabaseCreateTransaction(m_handle, out handle);
			if (Fdb.Failed(err))
			{
				handle.Dispose();
				throw Fdb.MapToException(err);
			}

			// ensure that if anything happens, either we return a valid Transaction, or we dispose it immediately
			FdbTransaction trans = null;
			try
			{
				trans = new FdbTransaction(this, id, handle);
				RegisterTransaction(trans);
				return trans;
			}
			catch (Exception)
			{
				if (trans != null)
				{
					trans.Dispose();
				}
				throw;
			}
		}

		/// <summary>Set a parameter-less option on this database</summary>
		/// <param name="option">Option to set</param>
		public void SetOption(FdbDatabaseOption option)
		{
			SetOption(option, default(string));
		}

		/// <summary>Set an option on this database</summary>
		/// <param name="option">Option to set</param>
		/// <param name="value">Value of the parameter</param>
		public void SetOption(FdbDatabaseOption option, string value)
		{
			ThrowIfDisposed();

			Fdb.EnsureNotOnNetworkThread();

			var data = FdbNative.ToNativeString(value, nullTerminated: true);
			unsafe
			{
				fixed (byte* ptr = data.Array)
				{
					FdbNative.DatabaseSetOption(m_handle, option, ptr + data.Offset, data.Count);
				}
			}
		}

		internal void EnsureCheckTransactionIsValid(FdbTransaction transaction)
		{
			ThrowIfDisposed();
			//TODO?
		}

		internal void RegisterTransaction(FdbTransaction transaction)
		{
			Debug.Assert(transaction != null);

			if (!m_transactions.TryAdd(transaction.Id, transaction))
			{
				throw new InvalidOperationException(String.Format("Failed to register transaction #{0} with this instance of database {1}", transaction.Id, this.Name));
			}
		}

		internal void UnregisterTransaction(FdbTransaction transaction)
		{
			Debug.Assert(transaction != null);

			//do nothing is already disposed
			if (m_disposed) return;

			// Unregister the transaction. We do not care if it has already been done
			FdbTransaction _;
			m_transactions.TryRemove(transaction.Id, out _);
			//TODO: compare removed value with the specified transaction to ensure it was the correct one?
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
				// mark this db has dead, but keep the handle alive until after all the callbacks have fired

				//TODO: kill all pending transactions on this db? 
				foreach (var trans in m_transactions.Values)
				{
					if (trans != null && trans.StillAlive)
					{
						trans.Rollback();
					}
				}
				m_transactions.Clear();

				try
				{
					//note: will block until all the registered callbacks have finished executing
					m_cts.Cancel();
				}
				catch (ObjectDisposedException) { }
				catch (AggregateException e)
				{
					//TODO: what should we do with the exception ?
					Debug.WriteLine("Error while cancelling all pending operations: " + e.ToString());
				}
				finally
				{
					m_handle.Dispose();
					if (m_ownsCluster) m_cluster.Dispose();
				}
			}
		}

	}

}
