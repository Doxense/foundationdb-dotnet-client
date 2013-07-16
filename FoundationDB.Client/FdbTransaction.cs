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
	* Neither the name of Doxense nor the
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

// enable this to help debug Transactions
#undef DEBUG_TRANSACTIONS

namespace FoundationDB.Client
{
	using FoundationDB.Async;
	using FoundationDB.Client.Native;
	using FoundationDB.Client.Utils;
	using System;
	using System.Collections.Generic;
	using System.Diagnostics;
	using System.Linq;
	using System.Threading;
	using System.Threading.Tasks;

	/// <summary>Wraps an FDB_TRANSACTION handle</summary>
	[DebuggerDisplay("Id={Id}, StillAlive={StillAlive}")]
	public class FdbTransaction : IFdbTransaction, IFdbReadTransaction, IDisposable
	{
		#region Private Members...

		internal const int STATE_INIT = 0;
		internal const int STATE_READY = 1;
		internal const int STATE_COMMITTED = 2;
		internal const int STATE_ROLLEDBACK = 3;
		internal const int STATE_FAILED = 4;
		internal const int STATE_DISPOSED = -1;

		/// <summary>Current state of the transaction</summary>
		private int m_state;

		/// <summary>Parent database for this transaction</summary>
		private readonly FdbDatabase m_database;

		/// <summary>Unique internal id for this transaction (for debugging purpose)</summary>
		private readonly int m_id;

		/// <summary>FDB_TRANSACTION* handle</summary>
		private readonly TransactionHandle m_handle;

		/// <summary>Estimated size of written data (in bytes)</summary>
		private int m_payloadBytes;
		//note: should be use a long instead?

		/// <summary>Cancelletation source specific to this instance.</summary>
		private readonly CancellationTokenSource m_cts;

		/// <summary>Snapshot version of this transaction (lazily allocated)</summary>
		private Snapshotted m_snapshotted;

		#endregion

		#region Constructors...

		internal FdbTransaction(FdbDatabase database, int id, TransactionHandle handle)
		{
			m_database = database;
			m_id = id;
			m_handle = handle;
			m_cts = CancellationTokenSource.CreateLinkedTokenSource(database.Token); //TODO: lazily allocated?
		}

		#endregion

		#region Public Members...

		/// <summary>Internal local identifier of the transaction</summary>
		/// <remarks>Should only used for logging/debugging purpose.</remarks>
		public int Id { get { return m_id; } }


		public bool IsSnapshot
		{
			get { return false; }
		}

		/// <summary>Database instance that manages this transaction</summary>
		public FdbDatabase Database { get { return m_database; } }

		/// <summary>Native FDB_TRANSACTION* handle</summary>
		internal TransactionHandle Handle { get { return m_handle; } }

		/// <summary>If true, the transaction is still pending (not committed or rolledback).</summary>
		internal bool StillAlive { get { return this.State == STATE_READY; } }

		/// <summary>Estimated size of the transaction payload (in bytes)</summary>
		public int Size { get { return m_payloadBytes; } }

		/// <summary>Cancellation Token that is canceled when the transaction is disposed</summary>
		public CancellationToken Token { get { return m_cts.Token; } }

		#endregion

		#region Options..

		/// <summary>Allows this transaction to read and modify system keys (those that start with the byte 0xFF)</summary>
		public void WithAccessToSystemKeys()
		{
			SetOption(FdbTransactionOption.AccessSystemKeys);
		}

		/// <summary>Specifies that this transaction should be treated as highest priority and that lower priority transactions should block behind this one. Use is discouraged outside of low-level tools</summary>
		public void WithPrioritySystemImmediate()
		{
			SetOption(FdbTransactionOption.PrioritySystemImmediate);
		}

		/// <summary>Specifies that this transaction should be treated as low priority and that default priority transactions should be processed first. Useful for doing batch work simultaneously with latency-sensitive work</summary>
		public void WithPriorityBatch()
		{
			SetOption(FdbTransactionOption.PriorityBatch);
		}

		/// <summary>Set an option on this transaction that does not take any parameter</summary>
		/// <param name="option">Option to set</param>
		public void SetOption(FdbTransactionOption option)
		{
			EnsureNotFailedOrDisposed();

			unsafe
			{
				Fdb.DieOnError(FdbNative.TransactionSetOption(m_handle, option, null, 0));
			}
		}

		/// <summary>Set an option on this transaction that takes a string value</summary>
		/// <param name="option">Option to set</param>
		/// <param name="value">Value of the parameter (can be null)</param>
		public void SetOption(FdbTransactionOption option, string value)
		{
			EnsureNotFailedOrDisposed();

			var data = FdbNative.ToNativeString(value, nullTerminated: true);
			unsafe
			{
				fixed (byte* ptr = data.Array)
				{
					Fdb.DieOnError(FdbNative.TransactionSetOption(m_handle, option, ptr + data.Offset, data.Count));
				}
			}
		}

		/// <summary>Set an option on this transaction that takes an integer value</summary>
		/// <param name="option">Option to set</param>
		/// <param name="value">Value of the parameter</param>
		public void SetOption(FdbTransactionOption option, long value)
		{
			EnsureNotFailedOrDisposed();

			unsafe
			{
				// Spec says: "If the option is documented as taking an Int parameter, value must point to a signed 64-bit integer (little-endian), and value_length must be 8."

				//TODO: what if we run on Big-Endian hardware ?
				Contract.Requires(BitConverter.IsLittleEndian, null, "Not supported on Big-Endian platforms");

				Fdb.DieOnError(FdbNative.TransactionSetOption(m_handle, option, (byte*)&value, 8));
			}
		}

		#endregion

		#region Versions...

		/// <summary>Returns this transaction snapshot read version.</summary>
		public Task<long> GetReadVersionAsync(CancellationToken ct = default(CancellationToken))
		{
			EnsureCanReadOrWrite(ct);
			//TODO: should we also allow being called after commit or rollback ?

			var future = FdbNative.TransactionGetReadVersion(m_handle);
			return FdbFuture.CreateTaskFromHandle(future,
				(h) =>
				{
					long version;
					var err = FdbNative.FutureGetVersion(h, out version);
#if DEBUG_TRANSACTIONS
					Debug.WriteLine("FdbTransaction[" + m_id + "].GetReadVersion() => err=" + err + ", version=" + version);
#endif
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
			//TODO: should we only allow calls if transaction is in state "COMMITTED" ?
			EnsureNotFailedOrDisposed();

			long version;
			var err = FdbNative.TransactionGetCommittedVersion(m_handle, out version);
#if DEBUG_TRANSACTIONS
			Debug.WriteLine("FdbTransaction[" + m_id + "].GetCommittedVersion() => err=" + err + ", version=" + version);
#endif
			Fdb.DieOnError(err);
			return version;
		}

		public void SetReadVersion(long version)
		{
			EnsureCanReadOrWrite();

			FdbNative.TransactionSetReadVersion(m_handle, version);
		}

		#endregion

		#region Get...

		private static bool TryGetValueResult(FutureHandle h, out Slice result)
		{
			Contract.Requires(h != null);

			bool present;
			var err = FdbNative.FutureGetValue(h, out present, out result);
#if DEBUG_TRANSACTIONS
			Debug.WriteLine("FdbTransaction[].TryGetValueResult() => err=" + err + ", present=" + present + ", valueLength=" + result.Count);
#endif
			Fdb.DieOnError(err);
			return present;
		}

		private static Slice GetValueResultBytes(FutureHandle h)
		{
			Contract.Requires(h != null);

			Slice result;
			if (!TryGetValueResult(h, out result))
			{
				return Slice.Nil;
			}
			return result;
		}

		internal Task<Slice> GetCoreAsync(Slice key, bool snapshot, CancellationToken ct)
		{
			this.Database.EnsureKeyIsValid(key);

#if DEBUG
			if (Logging.On && Logging.IsVerbose) Logging.Verbose(this, "GetRangeCore", String.Format("Getting value for '{0}'", key.ToString()));
#endif

			var future = FdbNative.TransactionGet(m_handle, key, snapshot);
			return FdbFuture.CreateTaskFromHandle(future, (h) => GetValueResultBytes(h), ct);
		}

		/// <summary>
		/// Reads a value from the database snapshot represented by transaction.
		/// </summary>
		/// <param name="keyBytes">Key to be looked up in the database</param>
		/// <param name="ct">CancellationToken used to cancel this operation (optionnal)</param>
		/// <returns>Task that will return the value of the key if it is found, Slice.Nil if the key does not exist, or an exception</returns>
		/// <exception cref="System.ArgumentException">If the key is null or empty</exception>
		/// <exception cref="System.OperationCanceledException">If the cancellation token is already triggered</exception>
		/// <exception cref="System.ObjectDisposedException">If the transaction has already been completed</exception>
		/// <exception cref="System.InvalidOperationException">If the operation method is called from the Network Thread</exception>
		public Task<Slice> GetAsync(Slice keyBytes, CancellationToken ct = default(CancellationToken))
		{
			EnsureCanReadOrWrite(ct);

			return GetCoreAsync(keyBytes, snapshot: false, ct: ct);
		}

		#endregion

		#region GetRangeAsync...

		/// <summary>Extract a chunk of result from a completed Future</summary>
		/// <param name="h">Handle to the completed Future</param>
		/// <param name="more">Receives true if there are more result, or false if all results have been transmited</param>
		/// <returns></returns>
		private static KeyValuePair<Slice, Slice>[] GetKeyValueArrayResult(FutureHandle h, out bool more)
		{
			KeyValuePair<Slice, Slice>[] result;
			var err = FdbNative.FutureGetKeyValueArray(h, out result, out more);
			Fdb.DieOnError(err);
			return result;
		}

		/// <summary>Asynchronously fetch a new page of results</summary>
		/// <param name="ct"></param>
		/// <returns>True if Chunk contains a new page of results. False if all results have been read.</returns>
		internal Task<FdbRangeChunk> GetRangeCoreAsync(FdbKeySelectorPair range, FdbRangeOptions options, int iteration, bool snapshot, CancellationToken ct)
		{
			this.Database.EnsureKeyIsValid(range.Start.Key);
			this.Database.EnsureKeyIsValid(range.Stop.Key);

			options = FdbRangeOptions.EnsureDefaults(options, 0, 0, FdbStreamingMode.WantAll, false);
			options.EnsureLegalValues();

			var future = FdbNative.TransactionGetRange(this.Handle, range.Start, range.Stop, options.Limit ?? 0, options.TargetBytes ?? 0, options.StreamingMode ?? FdbStreamingMode.WantAll, iteration, snapshot, options.Reverse ?? false);
			return FdbFuture.CreateTaskFromHandle(
				future,
				(h) =>
				{
					// TODO: quietly return if disposed

					bool hasMore;
					var chunk = GetKeyValueArrayResult(h, out hasMore);

					return new FdbRangeChunk(hasMore, chunk, iteration);
				},
				ct
			);
		}

		/// <summary>
		/// Reads all key-value pairs in the database snapshot represented by transaction (potentially limited by limit, target_bytes, or mode)
		/// which have a key lexicographically greater than or equal to the key resolved by the begin key selector
		/// and lexicographically less than the key resolved by the end key selector.
		/// </summary>
		/// <param name="range">Pair of key selectors defining the beginning and the end of the range</param>
		/// <param name="options">Optionnal query options (Limit, TargetBytes, StreamingMode, Reverse, ...)</param>
		/// <param name="iteration">If streaming mode is FdbStreamingMode.Iterator, this parameter should start at 1 and be incremented by 1 for each successive call while reading this range. In all other cases it is ignored.</param>
		/// <param name="ct">CancellationToken used to cancel this operation (optionnal)</param>
		/// <returns></returns>
		public Task<FdbRangeChunk> GetRangeAsync(FdbKeySelectorPair range, FdbRangeOptions options = null, int iteration = 0, CancellationToken ct = default(CancellationToken))
		{
			EnsureCanRead(ct);

			return GetRangeCoreAsync(range, options, iteration, snapshot: false, ct: ct);
		}

		public Task<FdbRangeChunk> GetRangeStartsWithAsync(Slice prefix, FdbRangeOptions options = null, int iteration = 0, CancellationToken ct = default(CancellationToken))
		{
			if (!prefix.HasValue) throw new ArgumentOutOfRangeException("prefix");

			EnsureCanRead(ct);

			return GetRangeCoreAsync(FdbKeySelectorPair.StartsWith(prefix), options, iteration, snapshot: false, ct: ct);
		}

		#endregion

		#region GetRange...

		internal FdbRangeQuery GetRangeCore(FdbKeySelectorPair range, FdbRangeOptions options, bool snapshot)
		{
			this.Database.EnsureKeyIsValid(range.Start.Key);
			this.Database.EnsureKeyIsValid(range.Stop.Key);

			options = FdbRangeOptions.EnsureDefaults(options, 0, 0, FdbStreamingMode.WantAll, false);
			options.EnsureLegalValues();

#if DEBUG
			if (Logging.On && Logging.IsVerbose) Logging.Verbose(this, "GetRangeCore", String.Format("Getting range '{0}'..'{1}'", range.Start.ToString(), range.Stop.ToString()));
#endif

			return new FdbRangeQuery(this, range, options, snapshot);
		}

		public FdbRangeQuery GetRange(FdbKeySelectorPair range, FdbRangeOptions options = null)
		{
			EnsureCanReadOrWrite();

			return GetRangeCore(range, options, snapshot: false);
		}

		public FdbRangeQuery GetRangeStartsWith(Slice prefix, FdbRangeOptions options = null)
		{
			if (!prefix.HasValue) throw new ArgumentOutOfRangeException("prefix");

			EnsureCanReadOrWrite();

			return GetRangeCore(FdbKeySelectorPair.StartsWith(prefix), options, snapshot: false);
		}

		#endregion

		#region GetKey...

		private static Slice GetKeyResult(FutureHandle h)
		{
			Contract.Requires(h != null);

			Slice result;
			var err = FdbNative.FutureGetKey(h, out result);
#if DEBUG_TRANSACTIONS
			Debug.WriteLine("FdbTransaction[].GetKeyResult() => err=" + err + ", result=" + FdbKey.Dump(result));
#endif
			Fdb.DieOnError(err);
			return result;
		}

		internal Task<Slice> GetKeyCoreAsync(FdbKeySelector selector, bool snapshot, CancellationToken ct)
		{
			this.Database.EnsureKeyIsValid(selector.Key);

#if DEBUG
			if (Logging.On && Logging.IsVerbose) Logging.Verbose(this, "GetKeyCoreAsync", String.Format("Getting key '{0}'", selector.ToString()));
#endif

			var future = FdbNative.TransactionGetKey(m_handle, selector, snapshot);
			return FdbFuture.CreateTaskFromHandle(
				future,
				(h) => GetKeyResult(h),
				ct
			);
		}

		/// <summary>Resolves a key selector against the keys in the database snapshot represented by transaction.</summary>
		/// <param name="selector">Key selector to resolve</param>
		/// <param name="ct">CancellationToken used to cancel this operation</param>
		/// <returns>Task that will return the key matching the selector, or an exception</returns>
		public Task<Slice> GetKeyAsync(FdbKeySelector selector, CancellationToken ct = default(CancellationToken))
		{
			EnsureCanReadOrWrite(ct);

			return GetKeyCoreAsync(selector, snapshot: false, ct: ct);
		}

		#endregion

		#region Set...

		internal void SetCore(Slice key, Slice value)
		{
			this.Database.EnsureKeyIsValid(key);
			this.Database.EnsureValueIsValid(value);

#if DEBUG
			if (Logging.On && Logging.IsVerbose) Logging.Verbose(this, "SetCore", String.Format("Setting '{0}' = {1}", key.ToString(), value.ToString()));
#endif

			FdbNative.TransactionSet(m_handle, key, value);
			Interlocked.Add(ref m_payloadBytes, key.Count + value.Count);
		}

		public void Set(Slice keyBytes, Slice valueBytes)
		{
			EnsureStilValid(allowFromNetworkThread: true);

			SetCore(keyBytes, valueBytes);
		}

		#endregion

		#region Clear...

		internal void ClearCore(Slice key)
		{
			this.Database.EnsureKeyIsValid(key);

#if DEBUG
			if (Logging.On && Logging.IsVerbose) Logging.Verbose(this, "ClearCore", String.Format("Clearing '{0}'", key.ToString()));
#endif

			FdbNative.TransactionClear(m_handle, key);
			Interlocked.Add(ref m_payloadBytes, key.Count);
		}

		public void Clear(Slice key)
		{
			EnsureStilValid(allowFromNetworkThread: true);

			ClearCore(key);
		}

		#endregion

		#region Clear Range...

		internal void ClearRangeCore(Slice beginKeyInclusive, Slice endKeyExclusive)
		{
			this.Database.EnsureKeyIsValid(beginKeyInclusive);
			this.Database.EnsureKeyIsValid(endKeyExclusive);

#if DEBUG
			if (Logging.On && Logging.IsVerbose) Logging.Verbose(this, "ClearRangeCore", String.Format("Clearing Range '{0}' <= k < '{1}'", beginKeyInclusive.ToString(), endKeyExclusive.ToString()));
#endif

			FdbNative.TransactionClearRange(m_handle, beginKeyInclusive, endKeyExclusive);
			//TODO: how to account for these ?
			//Interlocked.Add(ref m_payloadBytes, beginKey.Count);
			//Interlocked.Add(ref m_payloadBytes, endKey.Count);
		}

		/// <summary>
		/// Modify the database snapshot represented by transaction to remove all keys (if any) which are lexicographically greater than or equal to the given begin key and lexicographically less than the given end_key.
		/// Sets and clears affect the actual database only if transaction is later committed with fdb_transaction_commit().
		/// </summary>
		/// <param name="beginKeyInclusive"></param>
		/// <param name="endKeyExclusive"></param>
		public void ClearRange(Slice beginKeyInclusive, Slice endKeyExclusive)
		{
			EnsureStilValid(allowFromNetworkThread: true);

			ClearRangeCore(beginKeyInclusive, endKeyExclusive);
		}

		#endregion

		#region Batching...

		internal async Task<Slice[]> GetBatchValuesCoreAsync(Slice[] keys, bool snapshot, CancellationToken ct)
		{
			Contract.Requires(keys != null);

			//TODO: use a FdbAsyncTaskBuffer to throttle the number of concurrent reads !
			//TODO: add a FdbParallelQueryOptions argument to control the max concurrency

			var tasks = new List<Task<Slice>>(keys.Length);
			for (int i = 0; i < keys.Length; i++)
			{
				tasks.Add(GetCoreAsync(keys[i], snapshot, ct));
			}

			var results = await Task.WhenAll(tasks).ConfigureAwait(false);

			return results;
		}

		public Task<Slice[]> GetBatchValuesAsync(Slice[] keys, CancellationToken ct = default(CancellationToken))
		{
			if (keys == null) throw new ArgumentNullException("keys");

			EnsureCanRead(ct);

			return GetBatchValuesCoreAsync(keys, snapshot: false, ct: ct);
		}

		#endregion

		#region Commit...

		public async Task CommitAsync(CancellationToken ct = default(CancellationToken))
		{
			EnsureCanReadOrWrite(ct);

			if (Logging.On) Logging.Verbose(this, "CommitAsync", "Committing transaction...");

			//TODO: need a STATE_COMMITTING ?
			try
			{
				var future = FdbNative.TransactionCommit(m_handle);
				await FdbFuture.CreateTaskFromHandle<object>(future, (h) => null, ct).ConfigureAwait(false);

				if (Interlocked.CompareExchange(ref m_state, STATE_COMMITTED, STATE_READY) == STATE_READY)
				{
					if (Logging.On) Logging.Verbose(this, "CommitAsync", "Transaction has been committed");
				}
			}
			catch (Exception e)
			{
				if (Interlocked.CompareExchange(ref m_state, STATE_FAILED, STATE_READY) == STATE_READY)
				{
					if (Logging.On) Logging.Exception(this, "CommitAsync", e);
				}
				throw;
			}
		}

		#endregion

		#region OnError...

		public Task OnErrorAsync(FdbError code, CancellationToken ct = default(CancellationToken))
		{
			//note: should this be allowed from network thread ?
			EnsureCanReadOrWrite(ct);

			var future = FdbNative.TransactionOnError(m_handle, code);
			return FdbFuture.CreateTaskFromHandle<object>(future, (h) => null, ct);
		}

		#endregion

		#region Reset/Rollback...

		/// <summary>Reset the transaction to its initial state.</summary>
		public void Reset()
		{
			EnsureCanReadOrWrite();

			FdbNative.TransactionReset(m_handle);

			if (Logging.On) Logging.Verbose(this, "Reset", "Transaction has been reset");
		}

		/// <summary>Rollback this transaction, and dispose it. It should not be used after that.</summary>
		public void Rollback()
		{
			var state = Interlocked.CompareExchange(ref m_state, STATE_ROLLEDBACK, STATE_READY);
			if (state != STATE_READY)
			{
				switch(state)
				{
					case STATE_ROLLEDBACK: break; // already the case !

					case STATE_COMMITTED: throw new InvalidOperationException("Cannot rollback transaction that has already been committed");
					case STATE_FAILED: throw new InvalidOperationException("Cannot rollback transaction because it is in a failed state");
					case STATE_DISPOSED: throw new ObjectDisposedException("FdbTransaction", "Cannot rollback transaction because it already has been disposed");
					default: throw new InvalidOperationException(String.Format("Cannot rollback transaction because it is in unknown state {0}", state));
				}
			}

			if (Logging.On) Logging.Verbose(this, "Reset", "Rolling back transaction...");

			// Dispose of the handle
			if (!m_handle.IsClosed) m_handle.Dispose();

			if (Logging.On) Logging.Verbose(this, "Reset", "Transaction has been rolled back");
		}

		#endregion

		#region IDisposable...

		/// <summary>Get/Sets the internal state of the exception</summary>
		internal int State
		{
			get { return Volatile.Read(ref m_state); }
			set
			{
				Contract.Requires(value >= STATE_DISPOSED && value <= STATE_FAILED, null, "Invalid state value");
				Volatile.Write(ref m_state, value);
			}
		}

		/// <summary>Throws if the transaction is not a valid state (for reading/writing) and that we can proceed with a read operation</summary>
		public void EnsureCanRead(CancellationToken ct = default(CancellationToken))
		{
			EnsureStilValid(ct, allowFromNetworkThread: false);
		}

		/// <summary>Throws if the transaction is not a valid state (for reading/writing) and that we can proceed with a read or write operation</summary>
		public void EnsureCanReadOrWrite(CancellationToken ct = default(CancellationToken))
		{
			EnsureStilValid(ct, allowFromNetworkThread: false);
		}

		/// <summary>Throws if the transaction is not a valid state (for reading/writing) and that we can proceed with a read or write operation</summary>
		/// <param name="ct">Optionnal CancellationToken that should not be canceled</param>
		/// <exception cref="System.ObjectDisposedException">If Dispose as already been called on the transaction</exception>
		/// <exception cref="System.InvalidOperationException">If CommitAsync() or Rollback() have already been called on the transaction, or if the database has been closed</exception>
		internal void EnsureStilValid(CancellationToken ct = default(CancellationToken), bool allowFromNetworkThread = false)
		{
			// We must not be disposed
			if (this.State != STATE_READY)
			{
				ThrowOnInvalidState(this);
			}

			// The cancellation token should not be signaled
			if (ct.IsCancellationRequested) ct.ThrowIfCancellationRequested();

			// We cannot be called from the network thread (or else we will deadlock)
			if (!allowFromNetworkThread) Fdb.EnsureNotOnNetworkThread();

			// Ensure that the DB is still opened and that this transaction is still registered with it
			m_database.EnsureTransactionIsValid(this);

			// we are ready to go !
		}

		/// <summary>Throws if the transaction is not a valid state (for reading/writing)</summary>
		/// <exception cref="System.ObjectDisposedException">If Dispose as already been called on the transaction</exception>
		public void EnsureNotFailedOrDisposed()
		{
			switch (this.State)
			{
				case STATE_INIT:
				case STATE_READY:
				case STATE_COMMITTED:
				case STATE_ROLLEDBACK:
				{ // We are still valid
					// checks that the DB has not been disposed behind our back
					m_database.EnsureTransactionIsValid(this);
					return;
				}

				default:
				{
					ThrowOnInvalidState(this);
					return;
				}
			}

		}

		internal static void ThrowOnInvalidState(FdbTransaction trans)
		{
			switch (trans.State)
			{
				case STATE_INIT: throw new InvalidOperationException("The transaction has not been initialized properly");
				case STATE_DISPOSED: throw new ObjectDisposedException("FdbTransaction", "This transaction has already been disposed and cannot be used anymore");
				case STATE_FAILED: throw new InvalidOperationException("The transaction is in a failed state and cannot be used anymore");
				case STATE_COMMITTED: throw new InvalidOperationException("The transaction has already been committed");
				case STATE_ROLLEDBACK: throw new InvalidOperationException("The transaction has already been rolled back");
				default: throw new InvalidOperationException(String.Format("The transaction is unknown state {0}", trans.State));
			}
		}

		public void Dispose()
		{
			Dispose(true);
			GC.SuppressFinalize(this);
		}

		protected virtual void Dispose(bool disposing)
		{
			// note: we can be called by user code, or by the FdbDatabase when it is terminating with pending transactions
			if (Interlocked.Exchange(ref m_state, STATE_DISPOSED) != STATE_DISPOSED)
			{
				try
				{
					m_database.UnregisterTransaction(this);
					m_cts.SafeCancelAndDispose();

					if (Logging.On) Logging.Verbose(this, "Dispose", String.Format("Transaction #{0} has been disposed", m_id));
				}
				finally
				{
					// Dispose of the handle
					if (!m_handle.IsClosed) m_handle.Dispose();
					m_cts.Dispose();
				}
			}
		}

		#endregion

		#region Snapshot...

		/// <summary>Returns a version of this transaction that perform snapshotted operations</summary>
		public IFdbReadTransaction Snapshot
		{
			get
			{
				EnsureNotFailedOrDisposed();
				return m_snapshotted ?? (m_snapshotted = new Snapshotted(this));
			}
		}

		/// <summary>Wrapper on a transaction, that will use Snmapshot mode on all read operations</summary>
		private sealed class Snapshotted : IFdbReadTransaction
		{
			private readonly FdbTransaction m_parent;

			public Snapshotted(FdbTransaction parent)
			{
				if (parent == null) throw new ArgumentNullException("parent");
				m_parent = parent;
			}

			public int Id
			{
				get { return m_parent.Id; }
			}

			public int Size
			{
				get { return m_parent.Size; }
			}

			public CancellationToken Token
			{
				get { return m_parent.Token; }
			}

			public bool IsSnapshot
			{
				get { return true; }
			}

			public void EnsureCanRead(CancellationToken ct)
			{
				m_parent.EnsureCanRead(ct);
			}

			public void Reset()
			{
				m_parent.Reset();
			}

			public Task CommitAsync(CancellationToken ct)
			{
				return m_parent.CommitAsync(ct);
			}

			public Task<long> GetReadVersionAsync(CancellationToken ct)
			{
				return m_parent.GetReadVersionAsync(ct);
			}

			public void SetReadVersion(long version)
			{
				m_parent.SetReadVersion(version);
			}

			public long GetCommittedVersion()
			{
				return m_parent.GetCommittedVersion();
			}

			public Task<Slice> GetAsync(Slice keyBytes, CancellationToken ct)
			{
				EnsureCanRead(ct);

				return m_parent.GetCoreAsync(keyBytes, snapshot: true, ct: ct);
			}

			public Task<Slice[]> GetBatchValuesAsync(Slice[] keys, CancellationToken ct)
			{
				if (keys == null) throw new ArgumentNullException("keys");

				EnsureCanRead(ct);

				return m_parent.GetBatchValuesCoreAsync(keys, snapshot: true, ct: ct);
			}

			public Task<Slice> GetKeyAsync(FdbKeySelector selector, CancellationToken ct)
			{
				EnsureCanRead(ct);

				return m_parent.GetKeyCoreAsync(selector, snapshot: true, ct: ct);
			}

			public Task<FdbRangeChunk> GetRangeAsync(FdbKeySelectorPair range, FdbRangeOptions options, int iteration, CancellationToken ct)
			{
				EnsureCanRead(ct);

				return m_parent.GetRangeCoreAsync(range, options, iteration, snapshot: true, ct: ct);
			}

			public FdbRangeQuery GetRange(FdbKeySelector beginInclusive, FdbKeySelector endExclusive, FdbRangeOptions options)
			{
				EnsureCanRead(CancellationToken.None);

				return m_parent.GetRangeCore(FdbKeySelectorPair.Create(beginInclusive, endExclusive), options, snapshot: true);
			}

			public FdbRangeQuery GetRange(FdbKeySelectorPair range, FdbRangeOptions options)
			{
				EnsureCanRead(CancellationToken.None);

				return m_parent.GetRangeCore(range, options, snapshot: true);
			}

			public FdbRangeQuery GetRangeStartsWith(Slice prefix, FdbRangeOptions options)
			{
				if (!prefix.HasValue) throw new ArgumentOutOfRangeException("prefix");

				EnsureCanRead(CancellationToken.None);

				return m_parent.GetRangeCore(FdbKeySelectorPair.StartsWith(prefix), options, snapshot: true);
			}

			public Task OnErrorAsync(FdbError code, CancellationToken ct)
			{
				return m_parent.OnErrorAsync(code, ct);
			}
		}

		#endregion

	}

}
