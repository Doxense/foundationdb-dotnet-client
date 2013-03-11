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
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace FoundationDb.Client
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

		private static byte[] GetValueResult(FutureHandle h)
		{
			bool present;
			byte[] value;
			int valueLength;
			var err = FdbNativeStub.FutureGetValue(h, out present, out value, out valueLength);
			Debug.WriteLine("fdb_future_get_value() => err=" + err + ", valueLength=" + valueLength);
			FdbCore.DieOnError(err);
			if (present)
			{
				if (value.Length != valueLength)
				{
					var tmp = new byte[valueLength];
					Array.Copy(value, 0, tmp, 0, valueLength);
					value = tmp;
				}
				return value;
			}
			return null;
		}

		public Task<byte[]> GetAsync(string key, bool snapshot = false, CancellationToken ct = default(CancellationToken))
		{
			ThrowIfDisposed();

			ct.ThrowIfCancellationRequested();

			FdbCore.EnsureNotOnNetworkThread();

			var keyBytes = GetKeyBytes(key);

			var future = FdbNativeStub.TransactionGet(m_handle, keyBytes, keyBytes.Length, snapshot);
			return FdbFuture.CreateTaskFromHandle(future, (h) => GetValueResult(h), ct);
		}

		public byte[] Get(string key, bool snapshot = false, CancellationToken ct = default(CancellationToken))
		{
			ThrowIfDisposed();

			ct.ThrowIfCancellationRequested();

			FdbCore.EnsureNotOnNetworkThread();

			var keyBytes = GetKeyBytes(key);

			var handle = FdbNativeStub.TransactionGet(m_handle, keyBytes, keyBytes.Length, snapshot);
			using (var future = FdbFuture.FromHandle(handle, (h) => GetValueResult(h), ct, willBlockForResult: true))
			{
				return future.GetResult();
			}
		}

		public Task<long> GetReadVersion(CancellationToken ct = default(CancellationToken))
		{
			ThrowIfDisposed();

			FdbCore.EnsureNotOnNetworkThread();

			var future = FdbNativeStub.TransactionGetReadVersion(m_handle);
			return FdbFuture.CreateTaskFromHandle(future,
				(h) =>
				{
					long version;
					var err = FdbNativeStub.FutureGetVersion(h, out version);
					Debug.WriteLine("fdb_future_get_version() => err=" + err + ", version=" + version);
					FdbCore.DieOnError(err);
					return version;
				},
				ct
			);
		}

		public void Set(string key, byte[] value, CancellationToken ct = default(CancellationToken))
		{
			ThrowIfDisposed();
			if (key == null) throw new ArgumentNullException("key");

			var keyBytes = GetKeyBytes(key);
			FdbNativeStub.TransactionSet(m_handle, keyBytes, keyBytes.Length, value, value.Length);
		}

		public void Set(string key, string value, CancellationToken ct = default(CancellationToken))
		{
			ThrowIfDisposed();
			if (key == null) throw new ArgumentNullException("key");
			if (value == null) throw new ArgumentNullException("value");

			FdbCore.EnsureNotOnNetworkThread();

			var keyBytes = GetKeyBytes(key);
			var valueBytes = Encoding.UTF8.GetBytes(value);
			FdbNativeStub.TransactionSet(m_handle, keyBytes, keyBytes.Length, valueBytes, valueBytes.Length);
		}

		public void Clear(string key, CancellationToken ct = default(CancellationToken))
		{
			ThrowIfDisposed();
			if (key == null) throw new ArgumentNullException("key");

			FdbCore.EnsureNotOnNetworkThread();

			var keyBytes = GetKeyBytes(key);
			FdbNativeStub.TransactionClear(m_handle, keyBytes, keyBytes.Length);
		}

		public Task CommitAsync(CancellationToken ct = default(CancellationToken))
		{
			ThrowIfDisposed();

			ct.ThrowIfCancellationRequested();

			FdbCore.EnsureNotOnNetworkThread();

			var future = FdbNativeStub.TransactionCommit(m_handle);
			return FdbFuture.CreateTaskFromHandle<object>(future, (h) => null, ct);
		}

		public void Commit(CancellationToken ct = default(CancellationToken))
		{
			ThrowIfDisposed();

			ct.ThrowIfCancellationRequested();

			FdbCore.EnsureNotOnNetworkThread();

			FutureHandle handle = null;
			try
			{
				// calls fdb_transaction_commit
				handle = FdbNativeStub.TransactionCommit(m_handle);
				using (var future = FdbFuture.FromHandle<object>(handle, (h) => null, ct, willBlockForResult: true))
				{
					future.Wait();
				}
			}
			catch (Exception)
			{
				if (handle != null) handle.Dispose();
			}
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
