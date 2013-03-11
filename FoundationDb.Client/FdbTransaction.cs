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

		#region Get...

		internal Task<byte[]> GetCoreAsync(ArraySegment<byte> key, bool snapshot, CancellationToken ct)
		{
			FdbCore.EnsureKeyIsValid(key);

			var future = FdbNativeStub.TransactionGet(m_handle, key, snapshot);
			return FdbFuture.CreateTaskFromHandle(future, (h) => GetValueResult(h), ct);
		}

		internal byte[] GetCore(ArraySegment<byte> key, bool snapshot, CancellationToken ct)
		{
			FdbCore.EnsureKeyIsValid(key);

			var handle = FdbNativeStub.TransactionGet(m_handle, key, snapshot);
			using (var future = FdbFuture.FromHandle(handle, (h) => GetValueResult(h), ct, willBlockForResult: true))
			{
				return future.GetResult();
			}
		}

		/// <summary>Returns the value of a particular key</summary>
		/// <param name="key">Key to retrieve (UTF-8)</param>
		/// <param name="snapshot"></param>
		/// <param name="ct">CancellationToken used to cancel this operation</param>
		/// <returns>Task that will return the value of the key if it is found, null if the key does not exist, or an exception</returns>
		/// <exception cref="System.ArgumentException">If the key is null or empty</exception>
		/// <exception cref="System.OperationCanceledException">If the cancellation token is already triggered</exception>
		/// <exception cref="System.ObjectDisposedException">If the transaction has already been completed</exception>
		/// <exception cref="System.InvalidOperationException">If the operation method is called from the Network Thread</exception>
		public Task<byte[]> GetAsync(string key, bool snapshot = false, CancellationToken ct = default(CancellationToken))
		{
			ct.ThrowIfCancellationRequested();
			ThrowIfDisposed();
			FdbCore.EnsureNotOnNetworkThread();

			return GetCoreAsync(FdbCore.GetKeyBytes(key), snapshot, ct);
		}

		/// <summary>Returns the value of a particular key</summary>
		/// <param name="key">Key to retrieve</param>
		/// <param name="snapshot"></param>
		/// <param name="ct">CancellationToken used to cancel this operation</param>
		/// <returns>Task that will return null if the value of the key if it is found, null if the key does not exist, or an exception</returns>
		/// <exception cref="System.ArgumentException">If the key is null or empty</exception>
		/// <exception cref="System.OperationCanceledException">If the cancellation token is already triggered</exception>
		/// <exception cref="System.ObjectDisposedException">If the transaction has already been completed</exception>
		/// <exception cref="System.InvalidOperationException">If the operation method is called from the Network Thread</exception>
		public Task<byte[]> GetAsync(byte[] key, bool snapshot = false, CancellationToken ct = default(CancellationToken))
		{
			ct.ThrowIfCancellationRequested();
			ThrowIfDisposed();
			FdbCore.EnsureNotOnNetworkThread();

			return GetCoreAsync(new ArraySegment<byte>(key), snapshot, ct);
		}

		/// <summary>Returns the value of a particular key</summary>
		/// <param name="key">Key to retrieve (UTF-8)</param>
		/// <param name="snapshot"></param>
		/// <param name="ct">CancellationToken used to cancel this operation</param>
		/// <returns>Returns the value of the key if it is found, or null if the key does not exist</returns>
		/// <exception cref="System.ArgumentException">If the key is null or empty</exception>
		/// <exception cref="System.OperationCanceledException">If the cancellation token is already triggered</exception>
		/// <exception cref="System.ObjectDisposedException">If the transaction has already been completed</exception>
		/// <exception cref="System.InvalidOperationException">If the operation method is called from the Network Thread</exception>
		public byte[] Get(string key, bool snapshot = false, CancellationToken ct = default(CancellationToken))
		{
			ThrowIfDisposed();
			ct.ThrowIfCancellationRequested();
			FdbCore.EnsureNotOnNetworkThread();

			return GetCore(FdbCore.GetKeyBytes(key), snapshot, ct);
		}

		public Task<List<KeyValuePair<string, byte[]>>> GetBatchAsync(IEnumerable<string> keys, bool snapshot = false, CancellationToken ct = default(CancellationToken))
		{
			ct.ThrowIfCancellationRequested();
			return GetBatchAsync(keys.ToArray(), snapshot, ct);
		}

		public async Task<List<KeyValuePair<string, byte[]>>> GetBatchAsync(string[] keys, bool snapshot = false, CancellationToken ct = default(CancellationToken))
		{
			ThrowIfDisposed();

			ct.ThrowIfCancellationRequested();

			FdbCore.EnsureNotOnNetworkThread();

			var tasks = new List<Task<byte[]>>(keys.Length);
			for (int i = 0; i < keys.Length; i++)
			{
				//TODO: optimize to not have to allocate a scope
				tasks.Add(Task.Factory.StartNew((_state) => this.GetCoreAsync(FdbCore.GetKeyBytes(keys[(int)_state]), snapshot, ct), i, ct).Unwrap());
			}

			var results = await Task.WhenAll(tasks);

			return results
				.Select((data, i) => new KeyValuePair<string, byte[]>(keys[i], data))
				.ToList();
		}

		#endregion

		#region Set...

		internal void SetCore(ArraySegment<byte> key, ArraySegment<byte> value)
		{
			FdbCore.EnsureKeyIsValid(key);
			FdbCore.EnsureValueIsValid(value);

			FdbNativeStub.TransactionSet(m_handle, key, value);
		}

		public void Set(string key, byte[] value)
		{
			if (key == null) throw new ArgumentNullException("key");
			if (value == null) throw new ArgumentNullException("value");

			ThrowIfDisposed();
			FdbCore.EnsureNotOnNetworkThread();

			SetCore(FdbCore.GetKeyBytes(key), new ArraySegment<byte>(value));
		}

		public void Set(string key, string value)
		{
			if (key == null) throw new ArgumentNullException("key");
			if (value == null) throw new ArgumentNullException("value");

			ThrowIfDisposed();
			FdbCore.EnsureNotOnNetworkThread();

			SetCore(FdbCore.GetKeyBytes(key), FdbCore.GetValueBytes(value));
		}

		#endregion

		#region Clear...

		internal void ClearCore(ArraySegment<byte> key)
		{
			FdbCore.EnsureKeyIsValid(key);

			FdbNativeStub.TransactionClear(m_handle, key);
		}

		public void Clear(byte[] key)
		{
			if (key == null) throw new ArgumentNullException("key");

			ThrowIfDisposed();
			FdbCore.EnsureNotOnNetworkThread();

			ClearCore(new ArraySegment<byte>(key));
		}

		public void Clear(string key)
		{
			if (key == null) throw new ArgumentNullException("key");

			ThrowIfDisposed();
			FdbCore.EnsureNotOnNetworkThread();

			ClearCore(FdbCore.GetKeyBytes(key));
		}

		#endregion

		#region Commit...

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

		#endregion

		#region Rollback...

		public void Rollback()
		{
			//TODO: refactor code between Rollback() and Dispose() ?
			this.Dispose();
		}

		#endregion

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
