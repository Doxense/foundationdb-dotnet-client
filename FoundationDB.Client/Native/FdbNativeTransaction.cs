﻿#region Copyright (c) 2023-2024 SnowBank SAS, (c) 2005-2023 Doxense SAS
// All rights reserved.
// 
// Redistribution and use in source and binary forms, with or without
// modification, are permitted provided that the following conditions are met:
// 	* Redistributions of source code must retain the above copyright
// 	  notice, this list of conditions and the following disclaimer.
// 	* Redistributions in binary form must reproduce the above copyright
// 	  notice, this list of conditions and the following disclaimer in the
// 	  documentation and/or other materials provided with the distribution.
// 	* Neither the name of SnowBank nor the
// 	  names of its contributors may be used to endorse or promote products
// 	  derived from this software without specific prior written permission.
// 
// THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS "AS IS" AND
// ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED
// WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE
// DISCLAIMED. IN NO EVENT SHALL SNOWBANK SAS BE LIABLE FOR ANY
// DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES
// (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES;
// LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND
// ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT
// (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS
// SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
#endregion

// enable this to help debug Transactions
//#define DEBUG_TRANSACTIONS
// enable this to capture the stacktrace of the ctor, when troubleshooting leaked transaction handles
//#define CAPTURE_STACKTRACES

namespace FoundationDB.Client.Native
{
	using System.Buffers;
	using System.Diagnostics;
	using FoundationDB.Client.Core;

	/// <summary>Wraps a native FDB_TRANSACTION handle</summary>
	[DebuggerDisplay("Handle={m_handle}, Size={m_payloadBytes}, Closed={m_handle.IsClosed}")]
	internal class FdbNativeTransaction : IFdbTransactionHandler, IEquatable<FdbNativeTransaction>
	{
		/// <summary>FDB_DATABASE* handle</summary>
		private readonly FdbNativeDatabase m_database;

		/// <summary>FDB_TENANT* handle (optional)</summary>
		private readonly FdbNativeTenant? m_tenant;

		/// <summary>FDB_TRANSACTION* handle</summary>
		private readonly TransactionHandle m_handle;

		/// <summary>Estimated current size of the transaction mutations</summary>
		private int m_payloadBytes;
		//TODO: this is redundant with GetApproximateSize which does the exact bookkeeping (but is async!). Should we keep it? or get remove it?

		/// <summary>Estimed current size of the number of bytes read from the cluster</summary>
		/// <remarks>Includes the size of the both keys and values</remarks>
		private int m_readBytes;

#if CAPTURE_STACKTRACES
		private StackTrace m_stackTrace;
#endif

		public FdbNativeTransaction(FdbNativeDatabase db, FdbNativeTenant? tenant, TransactionHandle? handle)
		{
			Contract.NotNull(db);
			Contract.NotNull(handle);

			m_database = db;
			m_tenant = tenant;
			m_handle = handle;
#if CAPTURE_STACKTRACES
			m_stackTrace = new StackTrace();
#endif
		}

#if DEBUG
		// We add a destructor in DEBUG builds to help track leaks of transactions...
		~FdbNativeTransaction()
		{
#if CAPTURE_STACKTRACES
			Trace.WriteLine("A transaction handle (" + m_handle + ", " + m_payloadBytes + " bytes written) was leaked by " + m_stackTrace);
#endif
			// If you break here, that means that a native transaction handler was leaked by a FdbTransaction instance (or that the transaction instance was leaked)
			if (Debugger.IsAttached) Debugger.Break();
			Dispose(false);
		}
#endif

		#region Properties...

		public bool IsClosed => m_handle.IsClosed;

		/// <summary>Native FDB_TRANSACTION* handle</summary>
		public TransactionHandle Handle => m_handle;

		/// <summary>Database handler that owns this transaction</summary>
		public FdbNativeDatabase Database => m_database;

		/// <summary>Tenant handler that owns this transaction (optional)</summary>
		public FdbNativeTenant? Tenant => m_tenant;

		/// <summary>Estimated size of the transaction payload (in bytes)</summary>
		public int Size => m_payloadBytes;
		//TODO: this is redundant with GetApproximateSize which does the exact book-keeping (but is async!). Should we keep it? or get remove it?

		#endregion

		#region Options...

		/// <inheritdoc />
		public void SetOption(FdbTransactionOption option, ReadOnlySpan<byte> data)
		{
			Fdb.EnsureNotOnNetworkThread();

			unsafe
			{
				fixed (byte* ptr = data)
				{
					FdbNative.DieOnError(FdbNative.TransactionSetOption(m_handle, option, ptr, data.Length));
				}
			}
		}

		#endregion

		#region Reading...

		/// <inheritdoc />
		public Task<long> GetReadVersionAsync(CancellationToken ct)
		{
			var future = FdbNative.TransactionGetReadVersion(m_handle);
			return FdbFuture.CreateTaskFromHandle(future,
				(h) =>
				{
					// for 610 and below, we must use fdb_future_get_version
					// for 620 or above, we must use fdb_future_get_int64
					long version;
					var err = Fdb.BindingVersion < 620 
			          ? FdbNative.FutureGetVersion(h, out version)
			          : FdbNative.FutureGetInt64(h, out version);

#if DEBUG_TRANSACTIONS
					Debug.WriteLine("FdbTransaction[" + m_id + "].GetReadVersion() => err=" + err + ", version=" + version);
#endif
					FdbNative.DieOnError(err);
					return version;
				},
				ct
			);
		}

		public void SetReadVersion(long version)
		{
			FdbNative.TransactionSetReadVersion(m_handle, version);
		}

		private static bool TryPeekValueResultBytes(FutureHandle h, out ReadOnlySpan<byte> result)
		{
			Contract.Debug.Requires(h != null);
			var err = FdbNative.FutureGetValue(h, out bool present, out result);
#if DEBUG_TRANSACTIONS
			Debug.WriteLine("FdbTransaction[].TryPeekValueResultBytes() => err=" + err + ", present=" + present + ", valueLength=" + result.Count);
#endif
			FdbNative.DieOnError(err);

			return present;
		}

		private static Slice GetValueResultBytes(FutureHandle h)
		{
			Contract.Debug.Requires(h != null);

			var err = FdbNative.FutureGetValue(h, out bool present, out ReadOnlySpan<byte> result);
#if DEBUG_TRANSACTIONS
			Debug.WriteLine("FdbTransaction[].GetValueResultBytes() => err=" + err + ", present=" + present + ", valueLength=" + result.Count);
#endif
			FdbNative.DieOnError(err);

			return present ? Slice.Copy(result) : Slice.Nil;
		}

		private static TResult GetValueResultBytes<TState, TResult>(FutureHandle h, TState state, FdbValueDecoder<TState, TResult> decoder)
		{
			Contract.Debug.Requires(h != null);

			var err = FdbNative.FutureGetValue(h, out bool present, out var result);
#if DEBUG_TRANSACTIONS
			Debug.WriteLine("FdbTransaction[].GetValueResultBytes() => err=" + err + ", present=" + present + ", valueLength=" + result.Count);
#endif
			FdbNative.DieOnError(err);

			return decoder(state, result, present);
		}

		private static bool GetValueResultBytes(FutureHandle h, IBufferWriter<byte> writer)
		{
			Contract.Debug.Requires(h != null);
			Contract.Debug.Requires(writer != null);

			var err = FdbNative.FutureGetValue(h, out bool present, out ReadOnlySpan<byte> result);
#if DEBUG_TRANSACTIONS
			Debug.WriteLine("FdbTransaction[].TryGetValueResult() => err=" + err + ", present=" + present + ", valueLength=" + result.Count);
#endif
			FdbNative.DieOnError(err);

			if (present)
			{
				writer.Write(result);
			}

			return present;
		}

		public Task<Slice> GetAsync(ReadOnlySpan<byte> key, bool snapshot, CancellationToken ct)
		{
			return FdbFuture.CreateTaskFromHandle(
				FdbNative.TransactionGet(m_handle, key, snapshot),
				GetValueResultBytes,
				ct
			);
		}

		public Task<TResult> GetAsync<TState, TResult>(ReadOnlySpan<byte> key, bool snapshot, TState state, FdbValueDecoder<TState, TResult> decoder, CancellationToken ct)
		{
			return FdbFuture.CreateTaskFromHandle(
				FdbNative.TransactionGet(m_handle, key, snapshot),
				(h) => GetValueResultBytes(h, state, decoder),
				ct
			);
		}

		public Task<Slice[]> GetValuesAsync(ReadOnlySpan<Slice> keys, bool snapshot, CancellationToken ct)
		{
			if (ct.IsCancellationRequested) return Task.FromCanceled<Slice[]>(ct);

			if (keys.Length == 0)
			{
				return Task.FromResult(Array.Empty<Slice>());
			}

			//HACKHACK: as of now (730), there is no way to read multiple keys or values in a single API call
			// so we have to start one get request per key, and hide them all inside on "meta" future.
			// this is still has the same overhead for interop with the native library, but reduces the number of Task objects allocated by the CLR.

			var futures = new FutureHandle[keys.Length];
			try
			{
				//note: if one of the operation triggers an error, the array will be partially filled, but all previous futures will be canceled in the catch block below
				for (int i = 0; i < keys.Length; i++)
				{
					futures[i] = FdbNative.TransactionGet(m_handle, keys[i].Span, snapshot);
				}
			}
			catch
			{
				// we need to cancel any future created before the error
				foreach(var future in futures)
				{
					if (future == null!) break;
					future.Dispose();
				}
				throw;
			}

			return FdbFuture.CreateTaskFromHandleArray(futures, GetValueResultBytes, ct);
		}

		/// <summary>Extract a chunk of result from a completed Future</summary>
		/// <param name="h">Handle to the completed Future</param>
		/// <param name="more">Receives true if there are more result, or false if all results have been transmitted</param>
		/// <param name="first">Receives the first key in the page, or default if page is empty</param>
		/// <param name="last">Receives the last key in the page, or default if page is empty</param>
		/// <param name="dataBytes">Total size of keys and values</param>
		/// <returns>Array of key/value pairs, or an exception</returns>
		private static KeyValuePair<Slice, Slice>[] GetKeyValueArrayResult(FutureHandle h, out bool more, out Slice first, out Slice last, out int dataBytes)
		{
			var err = FdbNative.FutureGetKeyValueArray(h, out var result, out more, out dataBytes);
			FdbNative.DieOnError(err);
			//note: result can only be null if an error occured!
			Contract.Debug.Ensures(result != null);
			first = result.Length > 0 ? result[0].Key : default;
			last = result.Length > 0 ? result[^1].Key : default;
			return result;
		}

		/// <summary>Extract a chunk of result from a completed Future</summary>
		/// <param name="h">Handle to the completed Future</param>
		/// <param name="more">Receives true if there are more result, or false if all results have been transmitted</param>
		/// <param name="first">Receives the first key in the page, or default if page is empty</param>
		/// <param name="last">Receives the last key in the page, or default if page is empty</param>
		/// <param name="dataBytes">Total size of keys</param>
		/// <returns>Array of key/value pairs, or an exception</returns>
		private static KeyValuePair<Slice, Slice>[] GetKeyValueArrayResultKeysOnly(FutureHandle h, out bool more, out Slice first, out Slice last, out int dataBytes)
		{
			var err = FdbNative.FutureGetKeyValueArrayKeysOnly(h, out var result, out more, out dataBytes);
			FdbNative.DieOnError(err);
			//note: result can only be null if an error occured!
			Contract.Debug.Ensures(result != null);
			first = result.Length > 0 ? result[0].Key : default;
			last = result.Length > 0 ? result[^1].Key : default;
			return result;
		}

		/// <summary>Extract a chunk of result from a completed Future</summary>
		/// <param name="h">Handle to the completed Future</param>
		/// <param name="more">Receives true if there are more result, or false if all results have been transmitted</param>
		/// <param name="first">Receives the first key in the page, or default if page is empty</param>
		/// <param name="last">Receives the last key in the page, or default if page is empty</param>
		/// <param name="dataBytes">Total size of values</param>
		/// <returns>Array of key/value pairs, or an exception</returns>
		private static KeyValuePair<Slice, Slice>[] GetKeyValueArrayResultValuesOnly(FutureHandle h, out bool more, out Slice first, out Slice last, out int dataBytes)
		{
			var err = FdbNative.FutureGetKeyValueArrayValuesOnly(h, out var result, out more, out first, out last, out dataBytes);
			FdbNative.DieOnError(err);
			//note: result can only be null if an error occured!
			Contract.Debug.Ensures(result != null);
			return result;
		}

		/// <summary>Extract a list of keys from a completed Future</summary>
		/// <param name="h">Handle to the completed Future</param>
		private static Slice[] GetKeyArrayResult(FutureHandle h)
		{
			Contract.Debug.Requires(h != null);

			var err = FdbNative.FutureGetKeyArray(h, out var result);
#if DEBUG_TRANSACTIONS
			Debug.WriteLine("FdbTransaction[].FutureGetKeyArray() => err=" + err + ", results=" + (result == null ? "<null>" : result.Length.ToString()));
#endif
			FdbNative.DieOnError(err);
			Contract.Debug.Ensures(result != null); // can only be null in case of an error
			return result!;
		}
		/// <summary>Asynchronously fetch a new page of results</summary>
		/// <returns>True if Chunk contains a new page of results. False if all results have been read.</returns>
		public Task<FdbRangeChunk> GetRangeAsync(KeySelector beginInclusive, KeySelector endExclusive, int limit, bool reverse, int targetBytes, FdbStreamingMode mode, FdbReadMode read, int iteration, bool snapshot, CancellationToken ct)
		{
			var future = FdbNative.TransactionGetRange(m_handle, beginInclusive, endExclusive, limit, targetBytes, mode, iteration, snapshot, reverse);
			return FdbFuture.CreateTaskFromHandle(
				future,
				(h) =>
				{
					KeyValuePair<Slice, Slice>[] items;
					bool hasMore;
					Slice first, last;
					int dataBytes;
					switch (read)
					{
						case FdbReadMode.Both:
						{
							items = GetKeyValueArrayResult(h, out hasMore, out first, out last, out dataBytes);
							break;
						}
						case FdbReadMode.Keys:
						{
							items = GetKeyValueArrayResultKeysOnly(h, out hasMore, out first, out last, out dataBytes);
							break;
						}
						case FdbReadMode.Values:
						{
							items = GetKeyValueArrayResultValuesOnly(h, out hasMore, out first, out last, out dataBytes);
							break;
						}
						default:
						{
							throw new InvalidOperationException();
						}
					}
					return new FdbRangeChunk(items, hasMore, iteration, reverse, read, first, last, dataBytes);
				},
				ct
			);
		}

		/// <summary>Asynchronously fetch a new page of results</summary>
		/// <returns>True if Chunk contains a new page of results. False if all results have been read.</returns>
		public Task<FdbRangeChunk<TResult>> GetRangeAsync<TState, TResult>(KeySelector beginInclusive, KeySelector endExclusive, bool snapshot, TState state, FdbKeyValueDecoder<TState, TResult> decoder, int limit, bool reverse, int targetBytes, FdbStreamingMode mode, FdbReadMode read, int iteration, CancellationToken ct)
		{
			var future = FdbNative.TransactionGetRange(m_handle, beginInclusive, endExclusive, limit, targetBytes, mode, iteration, snapshot, reverse);
			return FdbFuture.CreateTaskFromHandle(
				future,
				(h) =>
				{
					// note: we don't have a way currently to do KeyOnly or ValueOnly, we always have both coming from the native client
					// but since we don't have to copy them into the managed heap, this is not really an issue

					var err = FdbNative.FutureGetKeyValueArray(h, state, decoder, out var items, out var hasMore, out var first, out var last, out var totalBytes);
					FdbNative.DieOnError(err);

					return new FdbRangeChunk<TResult>(items, hasMore, iteration, reverse, read, first, last, totalBytes);
				},
				ct
			);
		}

		private static Slice GetKeyResult(FutureHandle h)
		{
			Contract.Debug.Requires(h != null);

			var err = FdbNative.FutureGetKey(h, out var result);
#if DEBUG_TRANSACTIONS
			Debug.WriteLine("FdbTransaction[].GetKeyResult() => err=" + err + ", result=" + result.ToString());
#endif
			FdbNative.DieOnError(err);
			return Slice.Copy(result);
		}

		public Task<Slice> GetKeyAsync(KeySelector selector, bool snapshot, CancellationToken ct)
		{
			var future = FdbNative.TransactionGetKey(m_handle, selector, snapshot);
			return FdbFuture.CreateTaskFromHandle(
				future,
				GetKeyResult,
				ct
			);
		}

		public Task<Slice[]> GetKeysAsync(ReadOnlySpan<KeySelector> selectors, bool snapshot, CancellationToken ct)
		{
			if (ct.IsCancellationRequested) return Task.FromCanceled<Slice[]>(ct);

			if (selectors.Length == 0)
			{
				return Task.FromResult(Array.Empty<Slice>());
			}

			//HACKHACK: as of now (730), there is no way to read multiple keys or values in a single API call
			// so we have to start one get request per key, and hide them all inside on "meta" future.
			// this is still has the same overhead for interop with the native library, but reduces the number of Task objects allocated by the CLR.

			var futures = new FutureHandle[selectors.Length];
			try
			{
				//note: if one of the operation triggers an error, the array will be partially filled, but all previous futures will be canceled in the catch block below
				for (int i = 0; i < selectors.Length; i++)
				{
					futures[i] = FdbNative.TransactionGetKey(m_handle, selectors[i], snapshot);
				}
			}
			catch
			{
				// we need to cancel any future created before the error
				foreach (var future in futures)
				{
					if (future == null!) break;
					future.Dispose();
				}

				throw;
			}
			return FdbFuture.CreateTaskFromHandleArray(futures, GetKeyResult, ct);
		}

		public Task<(FdbValueCheckResult Result, Slice Actual)> CheckValueAsync(ReadOnlySpan<byte> key, Slice expected, bool snapshot, CancellationToken ct)
		{
			return FdbFuture.CreateTaskFromHandle(
				FdbNative.TransactionGet(m_handle, key, snapshot),
				(h) =>
				{
					if (TryPeekValueResultBytes(h, out var actual))
					{ // key exists
						return !expected.IsNull && expected.Span.SequenceEqual(actual) ? (FdbValueCheckResult.Success, expected) : (FdbValueCheckResult.Failed, Slice.Copy(actual));
					}
					else
					{ // key does not exist, pass only if expected is Nil
						return expected.IsNull ? (FdbValueCheckResult.Success, Slice.Nil) : (FdbValueCheckResult.Failed, Slice.Nil);
					}
				},
				ct
			);
		}

		#endregion

		#region Writing...

		/// <inheritdoc />
		public void Set(ReadOnlySpan<byte> key, ReadOnlySpan<byte> value)
		{
			FdbNative.TransactionSet(m_handle, key, value);

			// There is a 28-byte overhead pet Set(..) in a transaction
			// cf http://community.foundationdb.com/questions/547/transaction-size-limit
			Interlocked.Add(ref m_payloadBytes, key.Length + value.Length + 28);
		}

		/// <inheritdoc />
		public void Atomic(ReadOnlySpan<byte> key, ReadOnlySpan<byte> param, FdbMutationType type)
		{
			FdbNative.TransactionAtomicOperation(m_handle, key, param, type);

			//TODO: what is the overhead for atomic operations?
			Interlocked.Add(ref m_payloadBytes, key.Length + param.Length);

		}

		/// <inheritdoc />
		public void Clear(ReadOnlySpan<byte> key)
		{
			FdbNative.TransactionClear(m_handle, key);
			// The key is converted to range [key, key.'\0'), and there is an overhead of 28-byte per operation
			Interlocked.Add(ref m_payloadBytes, (key.Length * 2) + 28 + 1);
		}

		/// <inheritdoc />
		public void ClearRange(ReadOnlySpan<byte> beginKeyInclusive, ReadOnlySpan<byte> endKeyExclusive)
		{
			FdbNative.TransactionClearRange(m_handle, beginKeyInclusive, endKeyExclusive);
			// There is an overhead of 28-byte per operation
			Interlocked.Add(ref m_payloadBytes, beginKeyInclusive.Length + endKeyExclusive.Length + 28);
		}

		/// <inheritdoc />
		public void AddConflictRange(ReadOnlySpan<byte> beginKeyInclusive, ReadOnlySpan<byte> endKeyExclusive, FdbConflictRangeType type)
		{
			FdbError err = FdbNative.TransactionAddConflictRange(m_handle, beginKeyInclusive, endKeyExclusive, type);
			FdbNative.DieOnError(err);
		}

		private static string[] GetStringArrayResult(FutureHandle h)
		{
			Contract.Debug.Requires(h != null);

			var err = FdbNative.FutureGetStringArray(h, out var result);
#if DEBUG_TRANSACTIONS
			Debug.WriteLine("FdbTransaction[].FutureGetStringArray() => err=" + err + ", results=" + (result == null ? "<null>" : result.Length.ToString()));
#endif
			FdbNative.DieOnError(err);
			Contract.Debug.Ensures(result != null); // can only be null in case of an error
			return result!;
		}

		/// <inheritdoc />
		public Task<string[]> GetAddressesForKeyAsync(ReadOnlySpan<byte> key, CancellationToken ct)
		{
			var future = FdbNative.TransactionGetAddressesForKey(m_handle, key);
			return FdbFuture.CreateTaskFromHandle(
				future,
				(h) => GetStringArrayResult(h),
				ct
			);
		}

		/// <inheritdoc />
		public Task<Slice[]> GetRangeSplitPointsAsync(ReadOnlySpan<byte> beginKey, ReadOnlySpan<byte> endKey, long chunkSize, CancellationToken ct)
		{
			var future = FdbNative.TransactionGetRangeSplitPoints(m_handle, beginKey, endKey, chunkSize);
			return FdbFuture.CreateTaskFromHandle(
				future,
				(h) => GetKeyArrayResult(h),
				ct
			);
		}

		/// <inheritdoc />
		public Task<long> GetEstimatedRangeSizeBytesAsync(ReadOnlySpan<byte> beginKey, ReadOnlySpan<byte> endKey, CancellationToken ct)
		{
			var future = FdbNative.TransactionGetEstimatedRangeSizeBytes(m_handle, beginKey, endKey);
			return FdbFuture.CreateTaskFromHandle(
				future,
				(h) =>
				{
					var err = FdbNative.FutureGetInt64(h, out long size);
#if DEBUG_TRANSACTIONS
					Debug.WriteLine("FdbTransaction[" + m_id + "].GetEstimatedRangeSizeBytesAsync() => err=" + err + ", size=" + size);
#endif
					FdbNative.DieOnError(err);
					return size;
				},
				ct
			);
		}

		/// <inheritdoc />
		public Task<long> GetApproximateSizeAsync(CancellationToken ct)
		{
			// API was introduced in 6.2
			if (this.Database.GetApiVersion() < 620) throw new NotSupportedException($"The GetApproximateSize method is only available for version 6.2 or greater. Your application has selected API version {this.Database.GetApiVersion()} which is too low. You will need to select API version 620 or greater.");
			//TODO: for lesser version, maybe we could return our own estimation?

			var future = FdbNative.TransactionGetApproximateSize(m_handle);
			return FdbFuture.CreateTaskFromHandle(future,
				(h) =>
				{
					var err = FdbNative.FutureGetInt64(h, out long size);
#if DEBUG_TRANSACTIONS
					Debug.WriteLine("FdbTransaction[" + m_id + "].GetApproximateSize() => err=" + err + ", size=" + size);
#endif
					FdbNative.DieOnError(err);
					return size;
				},
				ct
			);
		}

		#endregion

		#region Watches...

		public FdbWatch Watch(Slice key, CancellationToken ct)
		{
			var future = FdbNative.TransactionWatch(m_handle, key.Span);
			return new FdbWatch(
				FdbFuture.FromHandle<Slice>(future, (h) => key, ct),
				key
			);
		}

		#endregion

		#region State management...

		public long GetCommittedVersion()
		{
			var err = FdbNative.TransactionGetCommittedVersion(m_handle, out long version);
#if DEBUG_TRANSACTIONS
			Debug.WriteLine("FdbTransaction[" + m_id + "].GetCommittedVersion() => err=" + err + ", version=" + version);
#endif
			FdbNative.DieOnError(err);
			return version;
		}

		public Task<VersionStamp> GetVersionStampAsync(CancellationToken ct)
		{
			var future = FdbNative.TransactionGetVersionStamp(m_handle);
			return FdbFuture.CreateTaskFromHandle<VersionStamp>(future, GetVersionStampResult, ct);
		}

		private static VersionStamp GetVersionStampResult(FutureHandle h)
		{
			Contract.Debug.Requires(h != null);
			var err = FdbNative.FutureGetVersionStamp(h, out VersionStamp stamp);
#if DEBUG_TRANSACTIONS
			Debug.WriteLine("FdbTransaction[" + m_id + "].FutureGetVersionStamp() => err=" + err + ", vs=" + stamp + ")");
#endif
			FdbNative.DieOnError(err);

			return stamp;
		}


		/// <summary>
		/// Attempts to commit the sets and clears previously applied to the database snapshot represented by this transaction to the actual database. 
		/// The commit may or may not succeed – in particular, if a conflicting transaction previously committed, then the commit must fail in order to preserve transactional isolation. 
		/// If the commit does succeed, the transaction is durably committed to the database and all subsequently started transactions will observe its effects.
		/// </summary>
		/// <returns>Task that succeeds if the transaction was committed successfully, or fails if the transaction failed to commit.</returns>
		/// <remarks>As with other client/server databases, in some failure scenarios a client may be unable to determine whether a transaction succeeded. In these cases, CommitAsync() will throw CommitUnknownResult error. The OnErrorAsync() function treats this error as retryable, so retry loops that don’t check for CommitUnknownResult could execute the transaction twice. In these cases, you must consider the idempotence of the transaction.</remarks>
		public Task CommitAsync(CancellationToken ct)
		{
			var future = FdbNative.TransactionCommit(m_handle);
			return FdbFuture.CreateTaskFromHandle<object?>(future, (h) => null, ct);
		}

		public Task OnErrorAsync(FdbError code, CancellationToken ct)
		{
			var future = FdbNative.TransactionOnError(m_handle, code);
			return FdbFuture.CreateTaskFromHandle<object?>(future, (h) => { ResetInternal(); return null; }, ct);
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

		#region IDisposable...

		public void Dispose()
		{
			Dispose(true);
			GC.SuppressFinalize(this);
		}

		private void Dispose(bool disposing)
		{
			if (disposing)
			{
				// Dispose of the handle
				if (!m_handle.IsClosed) m_handle.Dispose();
			}
		}

		#endregion

		#region IEquatable...

		public override string ToString() => $"FdbTransaction(0x{m_handle.Handle:x})";

		public bool Equals(FdbNativeTransaction? other) => ReferenceEquals(other, this) || (other != null && other.m_handle.Equals(m_handle));

		public override bool Equals(object? obj) => obj is FdbNativeTransaction tr && Equals(tr);

		public override int GetHashCode() => m_handle.GetHashCode();

		#endregion

	}

}
