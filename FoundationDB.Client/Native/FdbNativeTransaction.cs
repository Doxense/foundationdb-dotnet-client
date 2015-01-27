#region BSD Licence
/* Copyright (c) 2013-2014, Doxense SAS
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
// enable this to capture the stacktrace of the ctor, when troubleshooting leaked transaction handles
#undef CAPTURE_STACKTRACES

namespace FoundationDB.Client.Native
{
	using FoundationDB.Client.Core;
	using FoundationDB.Client.Utils;
	using FoundationDB.Layers.Tuples;
	using JetBrains.Annotations;
	using System;
	using System.Collections.Generic;
	using System.Diagnostics;
	using System.Threading;
	using System.Threading.Tasks;

	/// <summary>Wraps a native FDB_TRANSACTION handle</summary>
	[DebuggerDisplay("Handle={m_handle}, Size={m_payloadBytes}, Closed={m_handle.IsClosed}")]
	internal class FdbNativeTransaction : FdbFutureContext<TransactionHandle>, IFdbTransactionHandler
	{

		private readonly FdbNativeDatabase m_database;
		/// <summary>Estimated current size of the transaction</summary>
		private int m_payloadBytes;

		public FdbNativeTransaction([NotNull] FdbNativeDatabase db, [NotNull] TransactionHandle handle)
			: base(handle)
		{
			if (db == null) throw new ArgumentNullException("db");

			m_database = db;
		}

		#region Properties...

		public bool IsClosed { get { return m_handle.IsClosed; } }

		/// <summary>Database handler that owns this transaction</summary>
		public FdbNativeDatabase Database { get { return m_database; } }

		/// <summary>Estimated size of the transaction payload (in bytes)</summary>
		public int Size { get { return m_payloadBytes; } }

		public FdbIsolationLevel IsolationLevel
		{
			get
			{
				// FDB currently only supports Serializable transaction.
				return FdbIsolationLevel.Serializable;
			}
		}

		#endregion

		#region Options...

		public void SetOption(FdbTransactionOption option, Slice data)
		{
			Fdb.EnsureNotOnNetworkThread();

			unsafe
			{
				if (data.IsNull)
				{
					Fdb.DieOnError(FdbNative.TransactionSetOption(m_handle, option, null, 0));
				}
				else
				{
					fixed (byte* ptr = data.Array)
					{
						Fdb.DieOnError(FdbNative.TransactionSetOption(m_handle, option, ptr + data.Offset, data.Count));
					}
				}
			}
		}

		#endregion

		#region Reading...

		public Task<long> GetReadVersionAsync(CancellationToken cancellationToken)
		{
			return StartNewFuture(
				(handle, state) => FdbNative.TransactionGetReadVersion(handle),
				default(object),
				(future, state) =>
				{
					long version;
					var err = FdbNative.FutureGetVersion(future, out version);
#if DEBUG_TRANSACTIONS
					Debug.WriteLine("FdbTransaction[" + m_id + "].GetReadVersion() => err=" + err + ", version=" + version);
#endif
					Fdb.DieOnError(err);
					return version;
				},
				default(object),
				cancellationToken
			);
		}

		public void SetReadVersion(long version)
		{
			FdbNative.TransactionSetReadVersion(m_handle, version);
		}

		private static bool TryGetValueResult(IntPtr h, out Slice result)
		{
			bool present;
			var err = FdbNative.FutureGetValue(h, out present, out result);
#if DEBUG_TRANSACTIONS
			Debug.WriteLine("FdbTransaction[].TryGetValueResult() => err=" + err + ", present=" + present + ", valueLength=" + result.Count);
#endif
			Fdb.DieOnError(err);
			return present;
		}

		private static Slice GetValueResultBytes(IntPtr h)
		{
			Slice result;
			return !TryGetValueResult(h, out result) ? Slice.Nil : result;
		}

		public Task<Slice> GetAsync(Slice key, bool snapshot, CancellationToken cancellationToken)
		{
			return StartNewFuture(
				(handle, state) => FdbNative.TransactionGet(handle, state.Item1, state.Item2),
				FdbTuple.Create(key, snapshot),
				(future, state) => GetValueResultBytes(future),
				null,
				cancellationToken
			);
		}

		public Task<Slice[]> GetValuesAsync(Slice[] keys, bool snapshot, CancellationToken cancellationToken)
		{
			Contract.Requires(keys != null);

			if (keys.Length == 0) return Task.FromResult(Slice.EmptySliceArray);

			return StartNewFutures(
				keys.Length,
				(handle, state, futures) =>
				{
					var _keys = state.Item1;
					var _snapshot = state.Item2;
					for (int i = 0; i < _keys.Length; i++)
					{
						var h = FdbNative.TransactionGet(handle, _keys[i], _snapshot);
						if (h == IntPtr.Zero) throw new FdbException(FdbError.OperationFailed);
                        futures[i] = h;
					}
				},
				FdbTuple.Create(keys, snapshot),
				(future, state) => GetValueResultBytes(future),
				default(object), //TODO: buffer for the slices
				cancellationToken
			);
		}

		/// <summary>Extract a chunk of result from a completed Future</summary>
		/// <param name="h">Handle to the completed Future</param>
		/// <param name="more">Receives true if there are more result, or false if all results have been transmited</param>
		/// <returns>Array of key/value pairs, or an exception</returns>
		[NotNull]
		private static KeyValuePair<Slice, Slice>[] GetKeyValueArrayResult(IntPtr h, out bool more)
		{
			KeyValuePair<Slice, Slice>[] result;
			var err = FdbNative.FutureGetKeyValueArray(h, out result, out more);
			Fdb.DieOnError(err);
			//note: result can only be null if an error occured!
			Contract.Ensures(result != null);
			return result;
		}

		/// <summary>Asynchronously fetch a new page of results</summary>
		/// <returns>True if Chunk contains a new page of results. False if all results have been read.</returns>
		public Task<FdbRangeChunk> GetRangeAsync(FdbKeySelector begin, FdbKeySelector end, FdbRangeOptions options, int iteration, bool snapshot, CancellationToken cancellationToken)
		{
			Contract.Requires(options != null);

			bool reversed = options.Reverse ?? false;

			return StartNewFuture(
				(handle, _) => FdbNative.TransactionGetRange(handle, begin, end, options.Limit ?? 0, options.TargetBytes ?? 0, options.Mode ?? FdbStreamingMode.Iterator, iteration, snapshot, reversed),
				default(object), //TODO: pass options & co?
				(future, state) =>
				{
					// TODO: quietly return if disposed

					bool hasMore;
					var chunk = GetKeyValueArrayResult(future, out hasMore);

					return new FdbRangeChunk(hasMore, chunk, iteration, reversed);
				},
				default(object), //TODO: pass options & co?
				cancellationToken
			);
		}

		private static Slice GetKeyResult(IntPtr h)
		{
			Slice result;
			var err = FdbNative.FutureGetKey(h, out result);
#if DEBUG_TRANSACTIONS
			Debug.WriteLine("FdbTransaction[].GetKeyResult() => err=" + err + ", result=" + result.ToString());
#endif
			Fdb.DieOnError(err);
			return result;
		}

		public Task<Slice> GetKeyAsync(FdbKeySelector selector, bool snapshot, CancellationToken cancellationToken)
		{
			return StartNewFuture(
				(handle, state) => FdbNative.TransactionGetKey(handle, state.Item1, state.Item2),
				FdbTuple.Create(selector, snapshot),
				(future, state) => GetKeyResult(future),
				default(object),
				cancellationToken
			);
		}

		public Task<Slice[]> GetKeysAsync(FdbKeySelector[] selectors, bool snapshot, CancellationToken cancellationToken)
		{
			Contract.Requires(selectors != null);

			if (selectors.Length == 0) return Task.FromResult(Slice.EmptySliceArray);

			return StartNewFutures(
				selectors.Length,
				(handle, state, futures) =>
				{
					var _selectors = state.Item1;
					var _snapshot = state.Item2;
					for (int i = 0; i < _selectors.Length; i++)
					{
						var h = FdbNative.TransactionGetKey(handle, _selectors[i], _snapshot);
						if (h == IntPtr.Zero) throw new FdbException(FdbError.OperationFailed);
						futures[i] = h;
					}
				},
				FdbTuple.Create(selectors, snapshot),
				(future, state) => GetKeyResult(future),
				default(object), //TODO: buffer for the slices
				cancellationToken
			);
		}

		#endregion

		#region Writing...

		public void Set(Slice key, Slice value)
		{
			FdbNative.TransactionSet(m_handle, key, value);

			// There is a 28-byte overhead pet Set(..) in a transaction
			// cf http://community.foundationdb.com/questions/547/transaction-size-limit
			Interlocked.Add(ref m_payloadBytes, key.Count + value.Count + 28);
		}

		public void Atomic(Slice key, Slice param, FdbMutationType type)
		{
			FdbNative.TransactionAtomicOperation(m_handle, key, param, type);

			//TODO: what is the overhead for atomic operations?
			Interlocked.Add(ref m_payloadBytes, key.Count + param.Count);

		}

		public void Clear(Slice key)
		{
			FdbNative.TransactionClear(m_handle, key);
			// The key is converted to range [key, key.'\0'), and there is an overhead of 28-byte per operation
			Interlocked.Add(ref m_payloadBytes, (key.Count * 2) + 28 + 1);
		}

		public void ClearRange(Slice beginKeyInclusive, Slice endKeyExclusive)
		{
			FdbNative.TransactionClearRange(m_handle, beginKeyInclusive, endKeyExclusive);
			// There is an overhead of 28-byte per operation
			Interlocked.Add(ref m_payloadBytes, beginKeyInclusive.Count + endKeyExclusive.Count + 28);
		}

		public void AddConflictRange(Slice beginKeyInclusive, Slice endKeyExclusive, FdbConflictRangeType type)
		{
			FdbError err = FdbNative.TransactionAddConflictRange(m_handle, beginKeyInclusive, endKeyExclusive, type);
			Fdb.DieOnError(err);
		}

		[NotNull]
		private static string[] GetStringArrayResult(IntPtr h)
		{
			string[] result;
			var err = FdbNative.FutureGetStringArray(h, out result);
#if DEBUG_TRANSACTIONS
			Debug.WriteLine("FdbTransaction[].FutureGetStringArray() => err=" + err + ", results=" + (result == null ? "<null>" : result.Length.ToString()));
#endif
			Fdb.DieOnError(err);
			Contract.Ensures(result != null); // can only be null in case of an errror
			return result;
		}

		public Task<string[]> GetAddressesForKeyAsync(Slice key, CancellationToken cancellationToken)
		{
			return StartNewFuture(
				(handle, state) => FdbNative.TransactionGetAddressesForKey(handle, state),
				key,
				(future, state) => GetStringArrayResult(future),
				default(object),
				cancellationToken
			);
		}

		#endregion

		#region Watches...

		public FdbWatch Watch(Slice key, CancellationToken cancellationToken)
		{
			throw new NotImplementedException("FIXME: Future refactoring in progress! I owe you a beer (*) if I ever forget to remove this before committing! (*: if you come get it in person!)");
			//var future = FdbNative.TransactionWatch(m_handle, key);
			//return new FdbWatch(
			//	FdbFuture.FromHandle<Slice>(future, (h) => key, cancellationToken),
			//	key,
			//	Slice.Nil
			//);
		}

		#endregion

		#region State management...

		public long GetCommittedVersion()
		{
			long version;
			var err = FdbNative.TransactionGetCommittedVersion(m_handle, out version);
#if DEBUG_TRANSACTIONS
			Debug.WriteLine("FdbTransaction[" + m_id + "].GetCommittedVersion() => err=" + err + ", version=" + version);
#endif
			Fdb.DieOnError(err);
			return version;
		}

		/// <summary>
		/// Attempts to commit the sets and clears previously applied to the database snapshot represented by this transaction to the actual database. 
		/// The commit may or may not succeed – in particular, if a conflicting transaction previously committed, then the commit must fail in order to preserve transactional isolation. 
		/// If the commit does succeed, the transaction is durably committed to the database and all subsequently started transactions will observe its effects.
		/// </summary>
		/// <returns>Task that succeeds if the transaction was comitted successfully, or fails if the transaction failed to commit.</returns>
		/// <remarks>As with other client/server databases, in some failure scenarios a client may be unable to determine whether a transaction succeeded. In these cases, CommitAsync() will throw CommitUnknownResult error. The OnErrorAsync() function treats this error as retryable, so retry loops that don’t check for CommitUnknownResult could execute the transaction twice. In these cases, you must consider the idempotence of the transaction.</remarks>
		public Task CommitAsync(CancellationToken cancellationToken)
		{
			return StartNewFuture(
				(handle, state) => FdbNative.TransactionCommit(handle),
				default(object),
				(future, state) => state,
				default(object), //TODO:?
				cancellationToken
			);
		}

		public Task OnErrorAsync(FdbError code, CancellationToken cancellationToken)
		{
			return StartNewFuture(
				(handle, state) => FdbNative.TransactionOnError(handle, state),
				code,
				(h, state) =>
				{
					((FdbNativeTransaction)state).ResetInternal();
					return default(object);
				},
				this,
				cancellationToken
			);
		}

		public void Reset()
		{
			FdbNative.TransactionReset(m_handle);
			ResetInternal();
		}

		public void Cancel()
		{
			FdbNative.TransactionCancel(m_handle);
		}

		private void ResetInternal()
		{
			m_payloadBytes = 0;
		}

		#endregion

	}

}
