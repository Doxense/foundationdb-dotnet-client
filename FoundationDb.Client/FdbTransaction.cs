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
		/// <summary>Estimated size of written data (in bytes)</summary>
		private int m_payloadBytes;

		internal FdbTransaction(FdbDatabase database, TransactionHandle handle)
		{
			m_database = database;
			m_handle = handle;
		}

		public FdbDatabase Database { get { return m_database; } }

		internal TransactionHandle Handle { get { return m_handle; } }

		#region Options..

		/// <summary>Allows this transaction to read and modify system keys (those that start with the byte 0xFF)</summary>
		public void WithAccessToSystemKeys()
		{
			SetOption(FdbTransactionOption.AccessSystemKeys, null);
		}

		/// <summary>Specifies that this transaction should be treated as highest priority and that lower priority transactions should block behind this one. Use is discouraged outside of low-level tools</summary>
		public void WithPrioritySystemImmediate()
		{
			SetOption(FdbTransactionOption.PrioritySystemImmediate, null);
		}

		/// <summary>Specifies that this transaction should be treated as low priority and that default priority transactions should be processed first. Useful for doing batch work simultaneously with latency-sensitive work</summary>
		public void WithPriorityBatch()
		{
			SetOption(FdbTransactionOption.PriorityBatch, null);
		}

		/// <summary>Set a parameter-less option on this transaction</summary>
		/// <param name="option">Option to set</param>
		public void SetOption(FdbTransactionOption option)
		{
			SetOption(option, default(string));
		}

		/// <summary>Set an option on this transaction</summary>
		/// <param name="option">Option to set</param>
		/// <param name="value">Value of the parameter</param>
		public void SetOption(FdbTransactionOption option, string value)
		{
			ThrowIfDisposed();

			Fdb.EnsureNotOnNetworkThread();

			var data = FdbNative.ToNativeString(value, nullTerminated: true);
			unsafe
			{
				fixed (byte* ptr = data.Array)
				{
					FdbNative.TransactionSetOption(m_handle, option, ptr + data.Offset, data.Count);
				}
			}
		}

		#endregion

		#region Versions...

		/// <summary>Returns this transaction snapshot read version.</summary>
		public Task<long> GetReadVersionAsync(CancellationToken ct = default(CancellationToken))
		{
			ThrowIfDisposed();

			Fdb.EnsureNotOnNetworkThread();

			var future = FdbNative.TransactionGetReadVersion(m_handle);
			return FdbFuture.CreateTaskFromHandle(future,
				(h) =>
				{
					long version;
					var err = FdbNative.FutureGetVersion(h, out version);
					Debug.WriteLine("fdb_future_get_version() => err=" + err + ", version=" + version);
					Fdb.DieOnError(err);
					return version;
				},
				ct
			);
		}

		/// <summary>Retrieves the database version number at which a given transaction was committed.</summary>
		/// <returns>Version number, or -1 if this transaction was not committed (or did nothing)</returns>
		/// <remarks>The value return by this method is undefined if Commit has not been called</remarks>
		public long GetCommittedVersion()
		{
			ThrowIfDisposed();

			Fdb.EnsureNotOnNetworkThread();

			long version;
			var err = FdbNative.TransactionGetCommittedVersion(m_handle, out version);
			Debug.WriteLine("fdb_transaction_get_committed_version() => err=" + err + ", version=" + version);
			Fdb.DieOnError(err);
			return version;
		}

		public void SetReadVersion(long version)
		{
			ThrowIfDisposed();

			Fdb.EnsureNotOnNetworkThread();

			FdbNative.TransactionSetReadVersion(m_handle, version);
		}

		#endregion

		#region Get...

		private static bool TryGetValueResult(FutureHandle h, out ArraySegment<byte> result)
		{
			bool present;
			var err = FdbNative.FutureGetValue(h, out present, out result);
			Debug.WriteLine("fdb_future_get_value() => err=" + err + ", present=" + present + ", valueLength=" + result.Count);
			Fdb.DieOnError(err);
			return present;
		}

		private static byte[] GetValueResult(FutureHandle h)
		{
			ArraySegment<byte> result;
			if (!TryGetValueResult(h, out result))
			{
				return null;
			}
			return Fdb.GetBytes(result);
		}

		internal Task<byte[]> GetCoreAsync(ArraySegment<byte> key, bool snapshot, CancellationToken ct)
		{
			Fdb.EnsureKeyIsValid(key);

			var future = FdbNative.TransactionGet(m_handle, key, snapshot);
			return FdbFuture.CreateTaskFromHandle(future, (h) => GetValueResult(h), ct);
		}

		internal byte[] GetCore(ArraySegment<byte> key, bool snapshot, CancellationToken ct)
		{
			Fdb.EnsureKeyIsValid(key);

			var handle = FdbNative.TransactionGet(m_handle, key, snapshot);
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
			return GetAsync(Fdb.GetKeyBytes(key), snapshot, ct);
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
			return GetAsync(new ArraySegment<byte>(key), snapshot, ct);
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
		public Task<byte[]> GetAsync(ArraySegment<byte> key, bool snapshot = false, CancellationToken ct = default(CancellationToken))
		{
			ct.ThrowIfCancellationRequested();
			ThrowIfDisposed();
			Fdb.EnsureNotOnNetworkThread();

			return GetCoreAsync(key, snapshot, ct);
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
			return Get(Fdb.GetKeyBytes(key), snapshot, ct);
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
		public byte[] Get(byte[] key, bool snapshot = false, CancellationToken ct = default(CancellationToken))
		{
			return Get(new ArraySegment<byte>(key), snapshot, ct);
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
		public byte[] Get(ArraySegment<byte> key, bool snapshot = false, CancellationToken ct = default(CancellationToken))
		{
			ThrowIfDisposed();
			ct.ThrowIfCancellationRequested();
			Fdb.EnsureNotOnNetworkThread();

			return GetCore(key, snapshot, ct);
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

			Fdb.EnsureNotOnNetworkThread();

			var tasks = new List<Task<byte[]>>(keys.Length);
			for (int i = 0; i < keys.Length; i++)
			{
				//TODO: optimize to not have to allocate a scope
				tasks.Add(Task.Factory.StartNew((_state) => this.GetCoreAsync(Fdb.GetKeyBytes(keys[(int)_state]), snapshot, ct), i, ct).Unwrap());
			}

			var results = await Task.WhenAll(tasks);

			return results
				.Select((data, i) => new KeyValuePair<string, byte[]>(keys[i], data))
				.ToList();
		}

		#endregion

		#region GetRange...

		private static KeyValuePair<ArraySegment<byte>, ArraySegment<byte>>[] GetKeyValueArrayResult(FutureHandle h, out bool more)
		{
			KeyValuePair<ArraySegment<byte>, ArraySegment<byte>>[] result;
			var err = FdbNative.FutureGetKeyValueArray(h, out result, out more);
			Debug.WriteLine("fdb_future_get_keyvalue_array() => err=" + err + ", result=" + (result != null ? result.Length : -1) + ", more=" + more);
			Fdb.DieOnError(err);
			return result;
		}

		public class FdbSearchResults
		{
			/// <summary>Key selector describing the beginning of the range</summary>
			public FdbKeySelector Begin { get; internal set; }

			/// <summary>Key selector describing the end of the range</summary>
			public FdbKeySelector End { get; internal set; }

			public bool Reverse { get; internal set; }

			/// <summary>Iteration number of current page (in iterator mode), or 0</summary>
			public int Iteration { get; internal set; }

			/// <summary>Current page (may contain all records or only a segment)</summary>
			public KeyValuePair<ArraySegment<byte>, ArraySegment<byte>>[] Page { get; internal set; }

			/// <summary>If true, we have more records pending</summary>
			public bool HasMore { get; internal set; }

		}

		internal Task<FdbSearchResults> GetRangeCoreAsync(FdbKeySelector begin, FdbKeySelector end, int limit, int targetBytes, FDBStreamingMode mode, int iteration, bool snapshot, bool reverse, CancellationToken ct)
		{
			Fdb.EnsureKeyIsValid(begin.Key);
			Fdb.EnsureKeyIsValid(end.Key);

			var searchResults = new FdbSearchResults
			{
				Begin = begin,
				End = end,
				Reverse = reverse,
				Iteration = iteration,
			};

			var future = FdbNative.TransactionGetRange(m_handle, begin, end, limit, targetBytes, mode, iteration, snapshot, reverse);
			return FdbFuture.CreateTaskFromHandle(
				future,
				(h) =>
				{
					bool hasMore;
					searchResults.Page = GetKeyValueArrayResult(h, out hasMore);
					searchResults.HasMore = hasMore;
					return searchResults;
				},
				ct
			);
		}

		public Task<FdbSearchResults> GetRangeAsync(FdbKeySelector begin, FdbKeySelector end, int limit, int targetBytes, FDBStreamingMode mode, int iteration, bool snapshot, bool reverse, CancellationToken ct = default(CancellationToken))
		{
			ct.ThrowIfCancellationRequested();
			ThrowIfDisposed();
			Fdb.EnsureNotOnNetworkThread();

			return GetRangeCoreAsync(begin, end, limit, targetBytes, mode, iteration, snapshot, reverse, ct);
		}

		#endregion

		#region Set...

		internal void SetCore(ArraySegment<byte> key, ArraySegment<byte> value)
		{
			Fdb.EnsureKeyIsValid(key);
			Fdb.EnsureValueIsValid(value);

			FdbNative.TransactionSet(m_handle, key, value);
			Interlocked.Add(ref m_payloadBytes, key.Count + value.Count);
		}

		public void Set(ArraySegment<byte> keyBytes, ArraySegment<byte> valueBytes)
		{
			ThrowIfDisposed();
			Fdb.EnsureNotOnNetworkThread();

			SetCore(keyBytes, valueBytes);
		}

		public void Set(string key, byte[] value)
		{
			if (key == null) throw new ArgumentNullException("key");
			if (value == null) throw new ArgumentNullException("value");

			ThrowIfDisposed();
			Fdb.EnsureNotOnNetworkThread();

			SetCore(Fdb.GetKeyBytes(key), new ArraySegment<byte>(value));
		}

		public void Set(string key, string value)
		{
			if (key == null) throw new ArgumentNullException("key");
			if (value == null) throw new ArgumentNullException("value");

			ThrowIfDisposed();
			Fdb.EnsureNotOnNetworkThread();

			SetCore(Fdb.GetKeyBytes(key), Fdb.GetValueBytes(value));
		}

		public void Set(string key, long value)
		{
			if (key == null) throw new ArgumentNullException("key");

			ThrowIfDisposed();
			Fdb.EnsureNotOnNetworkThread();

			//HACKHACK: use something else! (endianness depends on plateform)
			var bytes = BitConverter.GetBytes(value);
			//TODO: only stores the needed number of bytes ?
			//or use ZigZag encoding ?
			SetCore(Fdb.GetKeyBytes(key), new ArraySegment<byte>(bytes));
		}

		#endregion

		#region Clear...

		internal void ClearCore(ArraySegment<byte> key)
		{
			Fdb.EnsureKeyIsValid(key);

			FdbNative.TransactionClear(m_handle, key);
			Interlocked.Add(ref m_payloadBytes, key.Count);
		}

		public void Clear(byte[] key)
		{
			if (key == null) throw new ArgumentNullException("key");

			ThrowIfDisposed();
			Fdb.EnsureNotOnNetworkThread();

			ClearCore(new ArraySegment<byte>(key));
		}

		public void Clear(string key)
		{
			if (key == null) throw new ArgumentNullException("key");

			ThrowIfDisposed();
			Fdb.EnsureNotOnNetworkThread();

			ClearCore(Fdb.GetKeyBytes(key));
		}

		#endregion

		#region Commit...

		public Task CommitAsync(CancellationToken ct = default(CancellationToken))
		{
			ThrowIfDisposed();

			ct.ThrowIfCancellationRequested();

			Fdb.EnsureNotOnNetworkThread();

			var future = FdbNative.TransactionCommit(m_handle);
			return FdbFuture.CreateTaskFromHandle<object>(future, (h) => null, ct);
		}

		public void Commit(CancellationToken ct = default(CancellationToken))
		{
			ThrowIfDisposed();

			ct.ThrowIfCancellationRequested();

			Fdb.EnsureNotOnNetworkThread();

			FutureHandle handle = null;
			try
			{
				// calls fdb_transaction_commit
				handle = FdbNative.TransactionCommit(m_handle);
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

		#region Reset/Rollback...

		/// <summary>Reset the transaction to its initial state.</summary>
		public void Reset()
		{
			ThrowIfDisposed();

			Fdb.EnsureNotOnNetworkThread();

			FdbNative.TransactionReset(m_handle);
		}

		/// <summary>Rollback this transaction, and dispose it. It should not be used after that.</summary>
		public void Rollback()
		{
			//TODO: refactor code between Rollback() and Dispose() ?
			this.Dispose();
		}

		#endregion

		private void ThrowIfDisposed()
		{
			if (m_disposed) throw new ObjectDisposedException(null);
			// also checks that the DB has not been disposed behind our back
			m_database.EnsureCheckTransactionIsValid(this);
		}

		public void Dispose()
		{
			if (!m_disposed)
			{
				m_disposed = true;
				m_handle.Dispose();
				m_database.UnregisterTransaction(this);
			}
		}
	}

}
