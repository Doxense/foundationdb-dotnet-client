#region BSD License
/* Copyright (c) 2013-2018, Doxense SAS
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

namespace FoundationDB.Client.Core
{
	using JetBrains.Annotations;
	using System;
	using System.Threading;
	using System.Threading.Tasks;

	/// <summary>Basic API for FoundationDB transactions</summary>
	[PublicAPI]
	public interface IFdbTransactionHandler : IDisposable
	{
		/// <summary>Returns the estimated payload size of the transaction (including keys and values)</summary>
		int Size { get; }

		/// <summary>Checks if this transaction handler is closed</summary>
		bool IsClosed { get; }

		/// <summary>Set an option on this transaction</summary>
		/// <param name="option">Option to set</param>
		/// <param name="data">Parameter value (or Slice.Nil for parameterless options)</param>
		void SetOption(FdbTransactionOption option, Slice data);

		/// <summary>Returns this transaction snapshot read version.</summary>
		Task<long> GetReadVersionAsync(CancellationToken ct);

		/// <summary>Retrieves the database version number at which a given transaction was committed.</summary>
		/// <remarks>CommitAsync() must have been called on this transaction and the resulting task must have completed successfully before this function is callged, or the behavior is undefined.
		/// Read-only transactions do not modify the database when committed and will have a committed version of -1.
		/// Keep in mind that a transaction which reads keys and then sets them to their current values may be optimized to a read-only transaction.
		/// </remarks>
		long GetCommittedVersion();

		/// <summary>Returns the <see cref="VersionStamp"/> which was used by versionstamps operations in this transaction.</summary>
		Task<VersionStamp> GetVersionStampAsync(CancellationToken ct);

		/// <summary>Sets the snapshot read version used by a transaction. This is not needed in simple cases.</summary>
		/// <param name="version">Read version to use in this transaction</param>
		/// <remarks>
		/// If the given version is too old, subsequent reads will fail with error_code_past_version; if it is too new, subsequent reads may be delayed indefinitely and/or fail with error_code_future_version.
		/// If any of Get*() methods have been called on this transaction already, the result is undefined.
		/// </remarks>
		void SetReadVersion(long version);

		/// <summary>Reads a get from the database</summary>
		/// <param name="key">Key to read</param>
		/// <param name="snapshot">Set to true for snapshot reads</param>
		/// <param name="ct"></param>
		/// <returns></returns>
		Task<Slice> GetAsync(Slice key, bool snapshot, CancellationToken ct);

		/// <summary>Reads several values from the database snapshot represented by the current transaction</summary>
		/// <param name="keys">Keys to be looked up in the database</param>
		/// <param name="snapshot">Set to true for snapshot reads</param>
		/// <param name="ct">Token used to cancel the operation from the outside</param>
		/// <returns>Task that will return an array of values, or an exception. Each item in the array will contain the value of the key at the same index in <paramref name="keys"/>, or Slice.Nil if that key does not exist.</returns>
		[ItemNotNull]
		Task<Slice[]> GetValuesAsync([NotNull] Slice[] keys, bool snapshot, CancellationToken ct);

		/// <summary>Resolves a key selector against the keys in the database snapshot represented by the current transaction.</summary>
		/// <param name="selector">Key selector to resolve</param>
		/// <param name="snapshot">Set to true for snapshot reads</param>
		/// <param name="ct">Token used to cancel the operation from the outside</param>
		/// <returns>Task that will return the key matching the selector, or an exception</returns>
		Task<Slice> GetKeyAsync(KeySelector selector, bool snapshot, CancellationToken ct);

		/// <summary>Resolves several key selectors against the keys in the database snapshot represented by the current transaction.</summary>
		/// <param name="selectors">Key selectors to resolve</param>
		/// <param name="snapshot">Set to true for snapshot reads</param>
		/// <param name="ct">Token used to cancel the operation from the outside</param>
		/// <returns>Task that will return an array of keys matching the selectors, or an exception</returns>
		[ItemNotNull]
		Task<Slice[]> GetKeysAsync([NotNull] KeySelector[] selectors, bool snapshot, CancellationToken ct);

		/// <summary>Reads all key-value pairs in the database snapshot represented by transaction (potentially limited by Limit, TargetBytes, or Mode) which have a key lexicographically greater than or equal to the key resolved by the begin key selector and lexicographically less than the key resolved by the end key selector.</summary>
		/// <param name="beginInclusive">key selector defining the beginning of the range</param>
		/// <param name="endExclusive">key selector defining the end of the range</param>
		/// <param name="options">Optionnal query options (Limit, TargetBytes, Mode, Reverse, ...)</param>
		/// <param name="iteration">If streaming mode is FdbStreamingMode.Iterator, this parameter should start at 1 and be incremented by 1 for each successive call while reading this range. In all other cases it is ignored.</param>
		/// <param name="snapshot">Set to true for snapshot reads</param>
		/// <param name="ct">Token used to cancel the operation from the outside</param>
		/// <returns></returns>
		Task<FdbRangeChunk> GetRangeAsync(KeySelector beginInclusive, KeySelector endExclusive, [NotNull] FdbRangeOptions options, int iteration, bool snapshot, CancellationToken ct);

		/// <summary>Returns a list of public network addresses as strings, one for each of the storage servers responsible for storing <paramref name="key"/> and its associated value</summary>
		/// <param name="key">Name of the key whose location is to be queried.</param>
		/// <param name="ct">Token used to cancel the operation from the outside</param>
		/// <returns>Task that will return an array of strings, or an exception</returns>
		[ItemNotNull]
		Task<string[]> GetAddressesForKeyAsync(Slice key, CancellationToken ct);

		/// <summary>Modify the database snapshot represented by transaction to change the given key to have the given value. If the given key was not previously present in the database it is inserted.
		/// The modification affects the actual database only if transaction is later committed with CommitAsync().
		/// </summary>
		/// <param name="key">Name of the key to be inserted into the database.</param>
		/// <param name="value">Value to be inserted into the database.</param>
		void Set(Slice key, Slice value);

		/// <summary>Modify the database snapshot represented by this transaction to perform the operation indicated by <paramref name="mutation"/> with operand <paramref name="param"/> to the value stored by the given key.</summary>
		/// <param name="key">Name of the key whose value is to be mutated.</param>
		/// <param name="param">Parameter with which the atomic operation will mutate the value associated with key_name.</param>
		/// <param name="mutation">Type of mutation that should be performed on the key</param>
		void Atomic(Slice key, Slice param, FdbMutationType mutation);

		/// <summary>Modify the database snapshot represented by this transaction to remove the given key from the database. If the key was not previously present in the database, there is no effect.</summary>
		/// <param name="key">Name of the key to be removed from the database.</param>
		void Clear(Slice key);

		/// <summary>Modify the database snapshot represented by this transaction to remove all keys (if any) which are lexicographically greater than or equal to the given begin key and lexicographically less than the given end_key.
		/// Sets and clears affect the actual database only if transaction is later committed with CommitAsync().
		/// </summary>
		/// <param name="beginKeyInclusive">Name of the key specifying the beginning of the range to clear.</param>
		/// <param name="endKeyExclusive">Name of the key specifying the end of the range to clear.</param>
		void ClearRange(Slice beginKeyInclusive, Slice endKeyExclusive);

		/// <summary>Adds a conflict range to a transaction without performing the associated read or write.</summary>
		/// <param name="beginKeyInclusive">Key specifying the beginning of the conflict range. The key is included</param>
		/// <param name="endKeyExclusive">Key specifying the end of the conflict range. The key is excluded</param>
		/// <param name="type">One of the FDBConflictRangeType values indicating what type of conflict range is being set.</param>
		void AddConflictRange(Slice beginKeyInclusive, Slice endKeyExclusive, FdbConflictRangeType type);

		/// <summary>Watch a key for any change in the database.</summary>
		/// <param name="key">Key to watch</param>
		/// <param name="ct">CancellationToken used to abort the watch if the caller doesn't want to wait anymore. Note that you can manually cancel the watch by calling Cancel() on the returned FdbWatch instance</param>
		/// <returns>FdbWatch that can be awaited and will complete when the key has changed in the database, or cancellation occurs. You can call Cancel() at any time if you are not interested in watching the key anymore. You MUST always call Dispose() if the watch completes or is cancelled, to ensure that resources are released properly.</returns>
		/// <remarks>You can directly await an FdbWatch, or obtain a Task&lt;Slice&gt; by reading the <see cref="FdbWatch.Task"/> property.</remarks>
		[Pure, NotNull]
		FdbWatch Watch(Slice key, CancellationToken ct);

		/// <summary>Attempts to commit the sets and clears previously applied to the database snapshot represented by this transaction to the actual database. 
		/// The commit may or may not succeed – in particular, if a conflicting transaction previously committed, then the commit must fail in order to preserve transactional isolation. 
		/// If the commit does succeed, the transaction is durably committed to the database and all subsequently started transactions will observe its effects.
		/// </summary>
		/// <param name="ct">Token used to cancel the operation from the outside</param>
		/// <returns>Task that succeeds if the transaction was comitted successfully, or fails if the transaction failed to commit.</returns>
		/// <remarks>As with other client/server databases, in some failure scenarios a client may be unable to determine whether a transaction succeeded. In these cases, CommitAsync() will throw CommitUnknownResult error. The OnErrorAsync() function treats this error as retryable, so retry loops that don’t check for CommitUnknownResult could execute the transaction twice. In these cases, you must consider the idempotence of the transaction.</remarks>
		Task CommitAsync(CancellationToken ct);

		/// <summary>Implements the recommended retry and backoff behavior for a transaction.
		/// This function knows which of the error codes generated by other query functions represent temporary error conditions and which represent application errors that should be handled by the application. 
		/// It also implements an exponential backoff strategy to avoid swamping the database cluster with excessive retries when there is a high level of conflict between transactions.
		/// </summary>
		/// <param name="code">FdbError code thrown by the previous command</param>
		/// <param name="ct">Token used to cancel the operation from the outside</param>
		/// <returns>Returns a task that completes if the operation can be safely retried, or that rethrows the original exception if the operation is not retryable.</returns>
		Task OnErrorAsync(FdbError code, CancellationToken ct);

		/// <summary>Reset transaction to its initial state.</summary>
		/// <remarks>This is similar to disposing the transaction and recreating a new one.  The only state that persists through a transaction reset is that which is related to the backoff logic used by OnErrorAsync()</remarks>
		void Reset();

		/// <summary>Cancels the transaction. All pending or future uses of the transaction will return a TransactionCancelled error code. The transaction can be used again after it is reset.</summary>
		void Cancel();
	}

}
