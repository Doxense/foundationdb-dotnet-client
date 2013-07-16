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

	/// <summary>Transaction that allows read operations</summary>
	public interface IFdbReadTransaction
	{
		/// <summary>
		/// Local id of the transaction
		/// </summary>
		/// <remarks>Should only be used for diagnosticts and logging</remarks>
		int Id { get; }

		/// <summary>
		/// Estimated payload size of the transaction (in bytes)
		/// </summary>
		int Size { get; }

		/// <summary>
		/// If true, the transaction is operating in Snapshot mode
		/// </summary>
		bool IsSnapshot { get; }

		/// <summary>Cancellation Token linked to the life time of the transaction</summary>
		/// <remarks>Will be triggered if the transaction is aborted or disposed</remarks>
		CancellationToken Token { get; }

		/// <summary>
		/// Ensure thats the transaction is in a valid state for issuing read operations.
		/// </summary>
		/// <param name="ct">CancellationToken used to cancel the operation (optionnal)</param>
		/// <exception cref="System.ObjectDisposedException">If Dispose as already been called on the transaction</exception>
		/// <exception cref="System.InvalidOperationException">If CommitAsync() or Rollback() have already been called on the transaction, or if the database has been closed</exception>
		/// <exception cref="System.OperationCanceledException">If the cancellation token has been cancelled</exception>
		void EnsureCanRead(CancellationToken ct = default(CancellationToken));

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
		Task<Slice> GetAsync(Slice keyBytes, CancellationToken ct = default(CancellationToken));

		Task<Slice[]> GetBatchValuesAsync(Slice[] keys, CancellationToken ct = default(CancellationToken));

		/// <summary>
		/// Resolves a key selector against the keys in the database snapshot represented by transaction.
		/// </summary>
		/// <param name="selector">Key selector to resolve</param>
		/// <param name="ct">CancellationToken used to cancel this operation (optionnal)</param>
		/// <returns>Task that will return the key matching the selector, or an exception</returns>
		Task<Slice> GetKeyAsync(FdbKeySelector selector, CancellationToken ct = default(CancellationToken));

		/// <summary>
		/// Reads all key-value pairs in the database snapshot represented by transaction (potentially limited by Limit, TargetBytes, or Mode)
		/// which have a key lexicographically greater than or equal to the key resolved by the begin key selector
		/// and lexicographically less than the key resolved by the end key selector.
		/// </summary>
		/// <param name="range">Pair of key selectors defining the beginning and the end of the range</param>
		/// <param name="options">Optionnal query options (Limit, TargetBytes, Mode, Reverse, ...)</param>
		/// <param name="iteration">If streaming mode is FdbStreamingMode.Iterator, this parameter should start at 1 and be incremented by 1 for each successive call while reading this range. In all other cases it is ignored.</param>
		/// <param name="ct">CancellationToken used to cancel this operation (optionnal)</param>
		/// <returns></returns>
		Task<FdbRangeChunk> GetRangeAsync(FdbKeySelectorPair range, FdbRangeOptions options = null, int iteration = 0, CancellationToken ct = default(CancellationToken));
		
		/// <summary>
		/// Create a new range query that will read all key-value pairs in the database snapshot represented by the transaction
		/// </summary>
		/// <param name="range">Pair of key selectors defining the beginning and the end of the range</param>
		/// <param name="options">Optionnal query options (Limit, TargetBytes, Mode, Reverse, ...)</param>
		/// <returns>Range query that, once executed, will return all the key-value pairs matching the providing selector pair</returns>
		FdbRangeQuery GetRange(FdbKeySelectorPair range, FdbRangeOptions options = null);

		/// <summary>Create a new range query that will read all key-value pairs that starts with a particular prefix in the database snapshot represented by the transaction</summary>
		/// <param name="prefix">Prefix of all keys that will match this query</param>
		/// <param name="options">Optionnal query options (Limit, TargetBytes, Mode, Reverse, ...)</param>
		/// <returns>Range query that, once executed, will return all the key-value pairs that have the specified prefix</returns>
		FdbRangeQuery GetRangeStartsWith(Slice prefix, FdbRangeOptions options = null);

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
		/// <param name="ct">CancellationToken used to cancel this operation (optionnal)</param>
		/// <returns>Task that succeeds if the transaction was comitted successfully, or fails if the transaction failed to commit.</returns>
		/// <remarks>As with other client/server databases, in some failure scenarios a client may be unable to determine whether a transaction succeeded. In these cases, CommitAsync() will throw CommitUnknownResult error. The OnErrorAsync() function treats this error as retryable, so retry loops that don’t check for CommitUnknownResult could execute the transaction twice. In these cases, you must consider the idempotence of the transaction.</remarks>
		Task CommitAsync(CancellationToken ct = default(CancellationToken));
		//TODO: should this be moved to IFdbTransaction instead ? Since readonly transaction don't do anything to the db, is there a point in exposing CommitAsync ? Caller would need an IFdbTransaction to change things anyway.

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
		/// ets the snapshot read version used by a transaction. This is not needed in simple cases.
		/// </summary>
		/// <param name="version">Read version to use in this transaction</param>
		/// <remarks>
		/// If the given version is too old, subsequent reads will fail with error_code_past_version; if it is too new, subsequent reads may be delayed indefinitely and/or fail with error_code_future_version.
		/// If any of Get*() methods have been called on this transaction already, the result is undefined.
		/// </remarks>
		void SetReadVersion(long version);

		/// <summary>
		/// Returns this transaction snapshot read version.
		/// </summary>
		Task<long> GetReadVersionAsync(CancellationToken ct = default(CancellationToken));

		/// <summary>
		/// Implements the recommended retry and backoff behavior for a transaction.
		/// 
		/// This function knows which of the error codes generated by other query functions represent temporary error conditions and which represent application errors that should be handled by the application. 
		/// It also implements an exponential backoff strategy to avoid swamping the database cluster with excessive retries when there is a high level of conflict between transactions.
		/// </summary>
		/// <param name="code">FdbError code thrown by the previous command</param>
		/// <param name="ct">CancellationToken used to cancel this operation (optionnal)</param>
		/// <returns>Returns a task that completes if the operation can be safely retried, or that rethrows the original exception if the operation is not retryable.</returns>
		Task OnErrorAsync(FdbError code, CancellationToken ct = default(CancellationToken));

	}

}
