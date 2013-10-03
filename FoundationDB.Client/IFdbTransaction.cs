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

namespace FoundationDB.Client
{
	using System;
	using System.Threading;
	using System.Threading.Tasks;

	/// <summary>Transaction that allows read and write operations</summary>
	public interface IFdbTransaction : IFdbReadOnlyTransaction
	{
		/// <summary>Returns true if this transaction instance only allow read operations</summary>
		/// <remarks>Attempting to call a write method on a read-only transaction will immediately throw an exception</remarks>
		bool IsReadOnly { get; }

		/// <summary>
		/// Ensure thats the transaction is in a valid state for issuing write operations.
		/// </summary>
		/// <exception cref="System.ObjectDisposedException">If <see cref="IDisposable.Dispose">Dispose()</see> has already been called on the transaction</exception>
		/// <exception cref="System.InvalidOperationException">If the transaction as already been committed, or if the database connection has been closed</exception>
		/// <exception cref="System.OperationCanceledException">If the cancellation token has been cancelled</exception>
		void EnsureCanWrite();

		/// <summary>
		/// Estimated payload size of the transaction (in bytes)
		/// </summary>
		int Size { get; }

		/// <summary>
		/// Modify the database snapshot represented by transaction to change the given key to have the given value. If the given key was not previously present in the database it is inserted.
		/// The modification affects the actual database only if transaction is later committed with CommitAsync().
		/// </summary>
		/// <param name="key">Name of the key to be inserted into the database.</param>
		/// <param name="value">Value to be inserted into the database.</param>
		void Set(Slice key, Slice value);

		/// <summary>
		/// Modify the database snapshot represented by this transaction to perform the operation indicated by <paramref name="mutation"/> with operand <paramref name="param"/> to the value stored by the given key.
		/// </summary>
		/// <param name="key">Name of the key whose value is to be mutated.</param>
		/// <param name="param">Parameter with which the atomic operation will mutate the value associated with key_name.</param>
		/// <param name="mutation">Type of mutation that should be performed on the key</param>
		void Atomic(Slice key, Slice param, FdbMutationType mutation);

		/// <summary>
		/// Modify the database snapshot represented by this transaction to remove the given key from the database. If the key was not previously present in the database, there is no effect.
		/// </summary>
		/// <param name="key">Name of the key to be removed from the database.</param>
		void Clear(Slice key);

		/// <summary>
		/// Modify the database snapshot represented by this transaction to remove all keys (if any) which are lexicographically greater than or equal to the given begin key and lexicographically less than the given end_key.
		/// Sets and clears affect the actual database only if transaction is later committed with CommitAsync().
		/// </summary>
		/// <param name="beginKeyInclusive">Name of the key specifying the beginning of the range to clear.</param>
		/// <param name="endKeyExclusive">Name of the key specifying the end of the range to clear.</param>
		void ClearRange(Slice beginKeyInclusive, Slice endKeyExclusive);

		/// <summary>
		/// Adds a conflict range to a transaction without performing the associated read or write.
		/// </summary>
		/// <param name="range">Range of the keys specifying the conflict range. The end key is excluded</param>
		/// <param name="type">One of the FDBConflictRangeType values indicating what type of conflict range is being set.</param>
		void AddConflictRange(FdbKeyRange range, FdbConflictRangeType type);

		/// <summary>
		/// Reset transaction to its initial state.
		/// </summary>
		/// <remarks>This is similar to disposing the transaction and recreating a new one.  The only state that persists through a transaction reset is that which is related to the backoff logic used by OnErrorAsync()</remarks>
		void Reset();

		/// <summary>
		/// Attempts to commit the sets and clears previously applied to the database snapshot represented by this transaction to the actual database. 
		/// The commit may or may not succeed – in particular, if a conflicting transaction previously committed, then the commit must fail in order to preserve transactional isolation. 
		/// If the commit does succeed, the transaction is durably committed to the database and all subsequently started transactions will observe its effects.
		/// </summary>
		/// <returns>Task that succeeds if the transaction was comitted successfully, or fails if the transaction failed to commit.</returns>
		/// <remarks>As with other client/server databases, in some failure scenarios a client may be unable to determine whether a transaction succeeded. In these cases, CommitAsync() will throw CommitUnknownResult error. The OnErrorAsync() function treats this error as retryable, so retry loops that don’t check for CommitUnknownResult could execute the transaction twice. In these cases, you must consider the idempotence of the transaction.</remarks>
		Task CommitAsync();

		/// <summary>
		/// Retrieves the database version number at which a given transaction was committed.
		/// </summary>
		/// <returns></returns>
		/// <remarks>
		/// CommitAsync() must have been called on this transaction and the resulting task must have completed successfully before this function is callged, or the behavior is undefined.
		/// Read-only transactions do not modify the database when committed and will have a committed version of -1.
		/// Keep in mind that a transaction which reads keys and then sets them to their current values may be optimized to a read-only transaction.
		/// </remarks>
		long GetCommittedVersion();

		/// <summary>
		/// Sets the snapshot read version used by a transaction. This is not needed in simple cases.
		/// </summary>
		/// <param name="version">Read version to use in this transaction</param>
		/// <remarks>
		/// If the given version is too old, subsequent reads will fail with error_code_past_version; if it is too new, subsequent reads may be delayed indefinitely and/or fail with error_code_future_version.
		/// If any of Get*() methods have been called on this transaction already, the result is undefined.
		/// </remarks>
		void SetReadVersion(long version);

		/// <summary>
		/// Implements the recommended retry and backoff behavior for a transaction.
		/// 
		/// This function knows which of the error codes generated by other query functions represent temporary error conditions and which represent application errors that should be handled by the application. 
		/// It also implements an exponential backoff strategy to avoid swamping the database cluster with excessive retries when there is a high level of conflict between transactions.
		/// </summary>
		/// <param name="code">FdbError code thrown by the previous command</param>
		/// <returns>Returns a task that completes if the operation can be safely retried, or that rethrows the original exception if the operation is not retryable.</returns>
		Task OnErrorAsync(FdbError code);

		/// <summary>
		/// Watch a key for any change in the database.
		/// </summary>
		/// <param name="key">Key to watch</param>
		/// <param name="cancellationToken">CancellationToken used to abort the watch if the caller doesn't want to wait anymore. Note that you can manually cancel the watch by calling Cancel() on the returned FdbWatch instance</param>
		/// <returns>FdbWatch that can be awaited and will complete when the key has changed in the database, or cancellation occurs. You can call Cancel() at any time if you are not interested in watching the key anymore. You MUST always call Dispose() if the watch completes or is cancelled, to ensure that resources are released properly.</returns>
		/// <remarks>You can directly await an FdbWatch, or obtain a Task&lt;Slice&gt; by reading the <see cref="FdbWatch.Task"/> property</remarks>
		FdbWatch Watch(Slice key, CancellationToken cancellationToken);

	}

}
