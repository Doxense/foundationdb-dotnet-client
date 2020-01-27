#region BSD License
/* Copyright (c) 2013-2020, Doxense SAS
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
	using JetBrains.Annotations;

	/// <summary>Defines a set of options for a transaction</summary>
	[PublicAPI]
	public enum FdbTransactionOption
	{
		/// <summary>None</summary>
		None = 0,

		/// <summary>The transaction, if not self-conflicting, may be committed a second time after commit succeeds, in the event of a fault</summary>
		/// <remarks>Parameter: Option takes no parameter</remarks>
		CausalWriteRisky = 10,

		/// <summary>The read version will be committed, and usually will be the latest committed, but might not be the latest committed in the event of a fault or partition</summary>
		/// <remarks>Parameter: Option takes no parameter</remarks>
		CausalReadRisky = 20,

		/// <summary>
		/// Parameter: Option takes no parameter
		/// </summary>
		CausalReadDisable = 21,

		//note: there is no entry for 22,

		/// <summary>Addresses returned by <see cref="IFdbReadOnlyTransaction.GetAddressesForKeyAsync"/> include the port when enabled. This will be enabled by default in api version 700, and this option will be deprecated.</summary>
		IncludePortInAddress = 23,

		/// <summary>The next write performed on this transaction will not generate a write conflict range.
		/// As a result, other transactions which read the key(s) being modified by the next write will not conflict with this transaction.
		/// Care needs to be taken when using this option on a transaction that is shared between multiple threads.
		/// When setting this option, write conflict ranges will be disabled on the next write operation, regardless of what thread it is on.</summary>
		/// <remarks>Parameter: Option takes no parameter</remarks>
		NextWriteNoWriteConflictRange = 30,

		/// <summary>Committing this transaction will bypass the normal load balancing across proxies and go directly to the specifically nominated 'first proxy'.</summary>
		CommitOnFirstProxy = 40, // hidden

		/// <summary>For internal use only.</summary>
		CheckWritesEnable = 50, // hidden

		/// <summary>Reads performed by a transaction will not see any prior mutations that occurred in that transaction, instead seeing the value which was in the database at the transaction's read version.
		/// This option may provide a small performance benefit for the client, but also disables a number of client-side optimizations which are beneficial for transactions which tend to read and write the same keys within a single transaction.
		/// Also note that with this option invoked any outstanding reads will return errors when transaction commit is called (rather than the normal behavior of commit waiting for outstanding reads to complete).</summary>
		/// <remarks>Parameter: Option takes no parameter</remarks>
		ReadYourWritesDisable = 51,

		/// <summary>Deprecated</summary>
		[Obsolete("This option is not available anymore")]
		ReadAheadDisable = 52,

		/// <summary>
		/// Parameter: Option takes no parameter
		/// </summary>
		DurabilityDataCenter = 110,

		/// <summary>
		/// Parameter: Option takes no parameter
		/// </summary>
		DurabilityRisky = 120,

		/// <summary>Deprecated</summary>
		[Obsolete("This option is not available anymore")]
		DevNullIsWebScale = 130,

		/// <summary>Specifies that this transaction should be treated as highest priority and that lower priority transactions should block behind this one.
		/// Use is discouraged outside of low-level tools</summary>
		/// <remarks>Parameter: Option takes no parameter</remarks>
		PrioritySystemImmediate = 200,

		/// <summary>Specifies that this transaction should be treated as low priority and that default priority transactions should be processed first.
		/// Useful for doing batch work simultaneously with latency-sensitive work</summary>
		/// <remarks>Parameter: Option takes no parameter</remarks>
		PriorityBatch = 201,

		/// <summary>This is a write-only transaction which sets the initial configuration.
		/// This option is designed for use by database system tools only.</summary>
		/// <remarks>Parameter: Option takes no parameter</remarks>
		InitializeNewDatabase = 300,

		/// <summary>Allows this transaction to read and modify system keys (those that start with the byte 0xFF)</summary>
		/// <remarks>Parameter: Option takes no parameter</remarks>
		AccessSystemKeys = 301,

		/// <summary>Allows this transaction to read system keys (those that start with the byte 0xFF)</summary>
		/// <remarks>Parameter: Option takes no parameter</remarks>
		ReadSystemKeys = 302,

		/// <summary>Enables dumping the set of mutations generated by a transaction, at commit time.</summary>
		DebugDump = 400, // hidden

		/// <summary>Sets a client provided identifier for the transaction that will be used for logging during retries.</summary>
		/// <remarks>Parameter: (String) Optional transaction name</remarks>
		DebugRetryLogging = 401,

		/// <summary>Deprecated</summary>
		/// <remarks>Parameter: (String) Optional transaction name</remarks>
		[Obsolete("This option is not available anymore")]
		TransactionLoggingEnable = 402,

		/// <summary>Sets a client provided identifier for the transaction that will be used in scenarios like tracing or profiling.
		/// Client trace logging or transaction profiling must be separately enabled.</summary>
		/// <remarks>Parameter: (String) String identifier to be used when tracing or profiling this transaction. The identifier must not exceed 100 characters.</remarks>
		DebugTransactionIdentifier = 403,

		/// <summary>Enables tracing for this transaction and logs results to the client trace logs. The DEBUG_TRANSACTION_IDENTIFIER option must be set before using this option, and client trace logging must be enabled and to get log output.</summary>
		LogTransaction = 404,

		/// <summary>Sets the maximum escaped length of key and value fields to be logged to the trace file via the LOG_TRANSACTION option, after which the field will be truncated. A negative value disables truncation.</summary>
		/// <remarks>Parameter: (Int32) Maximum length of escaped key and value fields.</remarks>
		TransactionLoggingMaxFieldLength = 405,

		/// <summary>Set a timeout in milliseconds which, when elapsed, will cause the transaction automatically to be cancelled.
		/// Valid parameter values are [0, int.MaxValue].
		/// If set to 0, will disable all timeouts.
		/// All pending and any future uses of the transaction will throw an exception.
		/// The transaction can be used again after it is reset.
		/// Like all transaction options, a timeout must be reset after a call to onError. This behavior allows the user to make the timeout dynamic.</summary>
		/// <remarks>Parameter: (Int32) value in milliseconds of timeout</remarks>
		Timeout = 500,

		/// <summary>Set a maximum number of retries after which additional calls to onError will throw the most recently seen error code.
		/// Valid parameter values are [-1, int.MaxValue]. If set to -1, will disable the retry limit.
		/// Parameter: (Int32) number of times to retry
		/// </summary>
		RetryLimit = 501,

		/// <summary>Set the maximum amount of backoff delay incurred in the call to onError if the error is retryable.
		/// Defaults to 1000 ms. Valid parameter values are [0, int.MaxValue].
		/// Like all transaction options, the maximum retry delay must be reset after a call to onError.
		/// If the maximum retry delay is less than the current retry delay of the transaction, then the current retry delay will be clamped to the maximum retry delay.
		/// Parameter: (Int32) value in milliseconds of maximum delay
		/// </summary>
		MaxRetryDelay = 502,

		/// <summary>Set the transaction size limit in bytes.
		/// The size is calculated by combining the sizes of all keys and values written or mutated, all key ranges cleared, and all read and write conflict ranges. (In other words, it includes the total size of all data included in the request to the cluster to commit the transaction.)
		/// Large transactions can cause performance problems on FoundationDB clusters, so setting this limit to a smaller value than the default can help prevent the client from accidentally degrading the cluster's performance.
		/// This value must be at least 32 and cannot be set to higher than 10,000,000, the default transaction size limit.</summary>
		/// <remarks>Parameter: (Int32) value in bytes</remarks>
		SizeLimit = 503,

		/// <summary>Snapshot read operations will see the results of writes done in the same transaction.</summary>
		SnapshotReadYourWriteEnable = 600,

		/// <summary>Snapshot read operations will not see the results of writes done in the same transaction.</summary>
		SnapshotReadYourWriteDisable = 601,

		/// <summary>The transaction can read and write to locked databases, and is responsible for checking that it took the lock.</summary>
		LockAware = 700,

		/// <summary>By default, operations that are performed on a transaction while it is being committed will not only fail themselves, but they will attempt to fail other in-flight operations (such as the commit) as well.
		/// This behavior is intended to help developers discover situations where operations could be unintentionally executed after the transaction has been reset.
		/// Setting this option removes that protection, causing only the offending operation to fail.</summary>
		UsedDuringCommitProtectionDisable = 701,

		/// <summary>The transaction can read from locked databases.</summary>
		ReadLockAware = 702,

		/// <summary>No other transactions will be applied before this transaction within the same commit version.</summary>
		FirstInBatch = 710,

		/// <summary>This option should only be used by tools which change the database configuration.</summary>
		UseProvisionalProxies = 711,

	}

}
