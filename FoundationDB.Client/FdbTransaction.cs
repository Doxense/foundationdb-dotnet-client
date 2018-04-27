#region BSD Licence
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

// enable this to help debug Transactions
//#define DEBUG_TRANSACTIONS

namespace FoundationDB.Client
{
	using System;
	using System.Collections.Generic;
	using System.Diagnostics;
	using System.Threading;
	using System.Threading.Tasks;
	using Doxense.Async;
	using Doxense.Diagnostics.Contracts;
	using FoundationDB.Client.Core;
	using FoundationDB.Client.Native;
	using JetBrains.Annotations;

	/// <summary>FounrationDB transaction handle.</summary>
	/// <remarks>An instance of this class can be used to read from and/or write to a snapshot of a FoundationDB database.</remarks>
	[DebuggerDisplay("Id={Id}, StillAlive={StillAlive}, Size={Size}")]
	public sealed partial class FdbTransaction : IFdbTransaction
	{

		#region Private Members...

		internal const int STATE_INIT = 0;
		internal const int STATE_READY = 1;
		internal const int STATE_COMMITTED = 2;
		internal const int STATE_CANCELED = 3;
		internal const int STATE_FAILED = 4;
		internal const int STATE_DISPOSED = -1;

		/// <summary>Current state of the transaction</summary>
		private int m_state;

		/// <summary>Owner database that created this instance</summary>
		private readonly FdbDatabase m_database;
		//REVIEW: this should be changed to "IFdbDatabase" if possible

		/// <summary>Context of the transaction when running inside a retry loop, or other custom scenario</summary>
		private readonly FdbOperationContext m_context;

		/// <summary>Unique internal id for this transaction (for debugging purpose)</summary>
		private readonly int m_id;

		/// <summary>True if the transaction has been opened in read-only mode</summary>
		private readonly bool m_readOnly;

		private readonly IFdbTransactionHandler m_handler;

		/// <summary>Timeout (in ms) of this transaction</summary>
		private int m_timeout;

		/// <summary>Retry Limit of this transaction</summary>
		private int m_retryLimit;

		/// <summary>Max Retry Delay (in ms) of this transaction</summary>
		private int m_maxRetryDelay;

		/// <summary>Cancelletation source specific to this instance.</summary>
		private readonly CancellationTokenSource m_cts;

		/// <summary>CancellationToken that should be used for all async operations executing inside this transaction</summary>
		private CancellationToken m_cancellation;

		/// <summary>Random token (but constant per transaction retry) used to generate incomplete VersionStamps</summary>
		private ulong m_versionStampToken;

		#endregion

		#region Constructors...

		internal FdbTransaction(FdbDatabase db, FdbOperationContext context, int id, IFdbTransactionHandler handler, FdbTransactionMode mode)
		{
			Contract.Requires(db != null && context != null && handler != null);
			Contract.Requires(context.Database != null);

			m_context = context;
			m_database = db;
			m_id = id;
			//REVIEW: the operation context may already have created its own CTS, maybe we can merge them ?
			m_cts = CancellationTokenSource.CreateLinkedTokenSource(context.Cancellation);
			m_cancellation = m_cts.Token;

			m_readOnly = (mode & FdbTransactionMode.ReadOnly) != 0;
			m_handler = handler;
		}

		#endregion

		#region Public Properties...

		/// <summary>Internal local identifier of the transaction</summary>
		/// <remarks>Should only used for logging/debugging purpose.</remarks>
		public int Id => m_id;

		/// <summary>Always returns false. Use the <see cref="FdbTransaction.Snapshot"/> property to get a different view of this transaction that will perform snapshot reads.</summary>
		public bool IsSnapshot => false;

		/// <summary>Returns the context of this transaction</summary>
		public FdbOperationContext Context
		{
			[NotNull]
			get { return m_context; }
		}

		/// <summary>Database instance that manages this transaction</summary>
		public FdbDatabase Database
		{
			[NotNull]
			get { return m_database; }
		}

		/// <summary>Returns the handler for this transaction</summary>
		internal IFdbTransactionHandler Handler
		{
			[NotNull]
			get { return m_handler; }
		}

		/// <summary>If true, the transaction is still pending (not committed or rolledback).</summary>
		internal bool StillAlive => this.State == STATE_READY;

		/// <summary>Estimated size of the transaction payload (in bytes)</summary>
		public int Size => m_handler.Size;

		/// <summary>Cancellation Token that is cancelled when the transaction is disposed</summary>
		public CancellationToken Cancellation => m_cancellation;

		/// <summary>Returns true if this transaction only supports read operations, or false if it supports both read and write operations</summary>
		public bool IsReadOnly => m_readOnly;

		/// <summary>Returns the isolation level of this transaction.</summary>
		public FdbIsolationLevel IsolationLevel => m_handler.IsolationLevel;

		#endregion

		#region Options..

		#region Properties...

		/// <summary>Timeout in milliseconds which, when elapsed, will cause the transaction automatically to be cancelled.
		/// Valid parameter values are [0, int.MaxValue].
		/// If set to 0, will disable all timeouts.
		/// All pending and any future uses of the transaction will throw an exception.
		/// The transaction can be used again after it is reset.</summary>
		public int Timeout
		{
			get => m_timeout;
			set
			{
				if (value < 0) throw new ArgumentOutOfRangeException(nameof(value), value, "Timeout value cannot be negative");
				SetOption(FdbTransactionOption.Timeout, value);
				m_timeout = value;
			}
		}

		/// <summary>Maximum number of retries after which additional calls to onError will throw the most recently seen error code.
		/// Valid parameter values are [-1, int.MaxValue].
		/// If set to -1, will disable the retry limit.
		/// </summary>
		public int RetryLimit
		{
			get => m_retryLimit;
			set
			{
				if (value < -1) throw new ArgumentOutOfRangeException(nameof(value), value, "Retry count cannot be negative");
				SetOption(FdbTransactionOption.RetryLimit, value);
				m_retryLimit = value;
			}
		}

		/// <summary>Maximum amount of backoff delay incurred in the call to onError if the error is retryable.
		/// Defaults to 1000 ms. Valid parameter values are [0, int.MaxValue].
		/// If the maximum retry delay is less than the current retry delay of the transaction, then the current retry delay will be clamped to the maximum retry delay.
		/// </summary>
		public int MaxRetryDelay
		{
			get => m_maxRetryDelay;
			set
			{
				if (value < 0) throw new ArgumentOutOfRangeException(nameof(value), value, "Max retry delay cannot be negative");
				SetOption(FdbTransactionOption.MaxRetryDelay, value);
				m_maxRetryDelay = value;
			}
		}

		#endregion

		/// <summary>Set an option on this transaction that does not take any parameter</summary>
		/// <param name="option">Option to set</param>
		public void SetOption(FdbTransactionOption option)
		{
			EnsureNotFailedOrDisposed();

			if (Logging.On && Logging.IsVerbose) Logging.Verbose(this, "SetOption", $"Setting transaction option {option.ToString()}");

			m_handler.SetOption(option, Slice.Nil);
		}

		/// <summary>Set an option on this transaction that takes a string value</summary>
		/// <param name="option">Option to set</param>
		/// <param name="value">Value of the parameter (can be null)</param>
		public void SetOption(FdbTransactionOption option, string value)
		{
			EnsureNotFailedOrDisposed();

			if (Logging.On && Logging.IsVerbose) Logging.Verbose(this, "SetOption", $"Setting transaction option {option.ToString()} to '{value ?? "<null>"}'");

			var data = FdbNative.ToNativeString(value, nullTerminated: true);
			m_handler.SetOption(option, data);
		}

		/// <summary>Set an option on this transaction that takes an integer value</summary>
		/// <param name="option">Option to set</param>
		/// <param name="value">Value of the parameter</param>
		public void SetOption(FdbTransactionOption option, long value)
		{
			EnsureNotFailedOrDisposed();

			if (Logging.On && Logging.IsVerbose) Logging.Verbose(this, "SetOption", $"Setting transaction option {option.ToString()} to {value}");

			// Spec says: "If the option is documented as taking an Int parameter, value must point to a signed 64-bit integer (little-endian), and value_length must be 8."
			var data = Slice.FromFixed64(value);

			m_handler.SetOption(option, data);
		}

		#endregion

		#region Versions...

		/// <summary>Returns this transaction snapshot read version.</summary>
		public Task<long> GetReadVersionAsync()
		{
			// can be called after the transaction has been committed
			EnsureCanRetry();

			return m_handler.GetReadVersionAsync(m_cancellation);
		}

		/// <summary>Retrieves the database version number at which a given transaction was committed.</summary>
		/// <returns>Version number, or -1 if this transaction was not committed (or did nothing)</returns>
		/// <remarks>The value return by this method is undefined if Commit has not been called</remarks>
		public long GetCommittedVersion()
		{
			//TODO: should we only allow calls if transaction is in state "COMMITTED" ?
			EnsureNotFailedOrDisposed();

			return m_handler.GetCommittedVersion();
		}

		/// <summary>
		/// Sets the snapshot read version used by a transaction. This is not needed in simple cases.
		/// </summary>
		public void SetReadVersion(long version)
		{
			EnsureCanRead();

			m_handler.SetReadVersion(version);
		}

		/// <summary>Returns the <see cref="VersionStamp"/> which was used by versionstamps operations in this transaction.</summary>
		/// <remarks>
		/// The Task will be ready only after the successful completion of a call to <see cref="CommitAsync"/> on this transaction.
		/// Read-only transactions do not modify the database when committed and will result in the Task completing with an error.
		/// Keep in mind that a transaction which reads keys and then sets them to their current values may be optimized to a read-only transaction.
		/// </remarks>
		public Task<VersionStamp> GetVersionStampAsync()
		{
			EnsureNotFailedOrDisposed();

			return m_handler.GetVersionStampAsync(m_cancellation);
		}

		private ulong GenerateNewVersionStampToken()
		{
			// We need to generate a 80-bits stamp, and also need to mark it as 'incomplete' by forcing the highest bit to 1.
			// Since this is supposed to be a version number with a ~1M tickrate per seconds, we will play it safe, and force the 8 highest bits to 1,
			// meaning that we only reduce the database potential lifetime but 1/256th, before getting into trouble.
			//
			// By doing some empirical testing, it also seems that the last 16 bits are a transction batch order which is usually a low number.
			// Again, we will force the 4 highest bit to 1 to reduce the change of collision with a complete version stamp.
			//
			// So the final token will look like:  'FF xx xx xx xx xx xx xx Fy yy', were 'x' is the random token, and 'y' will lowest 12 bits of the transaction retry count

			var rnd = new Random(); //TODO: singleton? (need locking!!)
			ulong x;
			unsafe
			{
				double r = rnd.NextDouble();
				x = *(ulong*) &r;
			}
			x |= 0xFF00000000000000UL;

			lock (this)
			{
				ulong token = m_versionStampToken;
				if (token == 0)
				{
					token = x;
					m_versionStampToken = x;
				}
				return token;
			}
		}

		/// <summary>Return a place-holder 80-bit VersionStamp, whose value is not yet known, but will be filled by the database at commit time.</summary>
		/// <returns>This value can used to generate temporary keys or value, for use with the <see cref="FdbMutationType.VersionStampedKey"/> or <see cref="FdbMutationType.VersionStampedValue"/> mutations</returns>
		/// <remarks>
		/// The generate placeholder will use a random value that is unique per transaction (and changes at reach retry).
		/// If the key contains the exact 80-bit byte signature of this token, the corresponding location will be tagged and replaced with the actual VersionStamp at commit time.
		/// If another part of the key contains (by random chance) the same exact byte sequence, then an error will be triggered, and hopefully the transaction will retry with another byte sequence.
		/// </remarks>
		[Pure]
		public VersionStamp CreateVersionStamp()
		{
			var token = m_versionStampToken;
			if (token == 0) token = GenerateNewVersionStampToken();
			return VersionStamp.Custom(token, (ushort) (m_context.Retries | 0xF000), incomplete: true);
		}

		/// <summary>Return a place-holder 96-bit VersionStamp with an attached user version, whose value is not yet known, but will be filled by the database at commit time.</summary>
		/// <returns>This value can used to generate temporary keys or value, for use with the <see cref="FdbMutationType.VersionStampedKey"/> or <see cref="FdbMutationType.VersionStampedValue"/> mutations</returns>
		/// <remarks>
		/// The generate placeholder will use a random value that is unique per transaction (and changes at reach retry).
		/// If the key contains the exact 80-bit byte signature of this token, the corresponding location will be tagged and replaced with the actual VersionStamp at commit time.
		/// If another part of the key contains (by random chance) the same exact byte sequence, then an error will be triggered, and hopefully the transaction will retry with another byte sequence.
		/// </remarks>
		public VersionStamp CreateVersionStamp(int userVersion)
		{
			var token = m_versionStampToken;
			if (token == 0) token = GenerateNewVersionStampToken();

			return VersionStamp.Custom(token, (ushort) (m_context.Retries | 0xF000), userVersion, incomplete: true);
		}

		#endregion

		#region Get...

		/// <summary>
		/// Reads a value from the database snapshot represented by transaction.
		/// </summary>
		/// <param name="key">Key to be looked up in the database</param>
		/// <returns>Task that will return the value of the key if it is found, Slice.Nil if the key does not exist, or an exception</returns>
		/// <exception cref="System.ArgumentException">If the <paramref name="key"/> is null</exception>
		/// <exception cref="System.OperationCanceledException">If the cancellation token is already triggered</exception>
		/// <exception cref="System.ObjectDisposedException">If the transaction has already been completed</exception>
		/// <exception cref="System.InvalidOperationException">If the operation method is called from the Network Thread</exception>
		public Task<Slice> GetAsync(Slice key)
		{
			EnsureCanRead();

			m_database.EnsureKeyIsValid(ref key);

#if DEBUG
			if (Logging.On && Logging.IsVerbose) Logging.Verbose(this, "GetAsync", $"Getting value for '{key.ToString()}'");
#endif

			return m_handler.GetAsync(key, snapshot: false, ct: m_cancellation);
		}

		#endregion

		#region GetValues...

		/// <summary>
		/// Reads several values from the database snapshot represented by the current transaction
		/// </summary>
		public Task<Slice[]> GetValuesAsync(Slice[] keys)
		{
			if (keys == null) throw new ArgumentNullException(nameof(keys));
			//TODO: should we make a copy of the key array ?

			EnsureCanRead();

			m_database.EnsureKeysAreValid(keys);

#if DEBUG
			if (Logging.On && Logging.IsVerbose) Logging.Verbose(this, "GetValuesAsync", $"Getting batch of {keys.Length} values ...");
#endif

			return m_handler.GetValuesAsync(keys, snapshot: false, ct: m_cancellation);
		}

		#endregion

		#region GetRangeAsync...

		/// <summary>
		/// Reads all key-value pairs in the database snapshot represented by transaction (potentially limited by limit, target_bytes, or mode)
		/// which have a key lexicographically greater than or equal to the key resolved by the begin key selector
		/// and lexicographically less than the key resolved by the end key selector.
		/// </summary>
		/// <param name="beginInclusive">key selector defining the beginning of the range</param>
		/// <param name="endExclusive">key selector defining the end of the range</param>
		/// <param name="options">Optionnal query options (Limit, TargetBytes, StreamingMode, Reverse, ...)</param>
		/// <param name="iteration">If streaming mode is FdbStreamingMode.Iterator, this parameter should start at 1 and be incremented by 1 for each successive call while reading this range. In all other cases it is ignored.</param>
		/// <returns></returns>
		public Task<FdbRangeChunk> GetRangeAsync(KeySelector beginInclusive, KeySelector endExclusive, FdbRangeOptions options = null, int iteration = 0)
		{
			EnsureCanRead();

			m_database.EnsureKeyIsValid(beginInclusive.Key);
			m_database.EnsureKeyIsValid(endExclusive.Key, endExclusive: true);

			options = FdbRangeOptions.EnsureDefaults(options, null, null, FdbStreamingMode.Iterator, false);
			options.EnsureLegalValues();

			// The iteration value is only needed when in iterator mode, but then it should start from 1
			if (iteration == 0) iteration = 1;

			return m_handler.GetRangeAsync(beginInclusive, endExclusive, options, iteration, snapshot: false, ct: m_cancellation);
		}

		#endregion

		#region GetRange...

		internal FdbRangeQuery<KeyValuePair<Slice, Slice>> GetRangeCore(KeySelector begin, KeySelector end, FdbRangeOptions options, bool snapshot)
		{
			this.Database.EnsureKeyIsValid(begin.Key);
			this.Database.EnsureKeyIsValid(end.Key, endExclusive: true);

			options = FdbRangeOptions.EnsureDefaults(options, null, null, FdbStreamingMode.Iterator, false);
			options.EnsureLegalValues();

#if DEBUG
			if (Logging.On && Logging.IsVerbose) Logging.Verbose(this, "GetRangeCore", $"Getting range '{begin.ToString()} <= x < {end.ToString()}'");
#endif

			return new FdbRangeQuery<KeyValuePair<Slice, Slice>>(this, begin, end, (kv) => kv, snapshot, options);
		}

		/// <summary>
		/// Create a new range query that will read all key-value pairs in the database snapshot represented by the transaction
		/// </summary>
		/// <param name="beginInclusive">key selector defining the beginning of the range</param>
		/// <param name="endExclusive">key selector defining the end of the range</param>
		/// <param name="options">Optionnal query options (Limit, TargetBytes, Mode, Reverse, ...)</param>
		/// <returns>Range query that, once executed, will return all the key-value pairs matching the providing selector pair</returns>
		public FdbRangeQuery<KeyValuePair<Slice, Slice>> GetRange(KeySelector beginInclusive, KeySelector endExclusive, FdbRangeOptions options = null)
		{
			EnsureCanRead();

			return GetRangeCore(beginInclusive, endExclusive, options, snapshot: false);
		}

		#endregion

		#region GetKey...

		/// <summary>Resolves a key selector against the keys in the database snapshot represented by transaction.</summary>
		/// <param name="selector">Key selector to resolve</param>
		/// <returns>Task that will return the key matching the selector, or an exception</returns>
		public async Task<Slice> GetKeyAsync(KeySelector selector)
		{
			EnsureCanRead();

			m_database.EnsureKeyIsValid(selector.Key);

#if DEBUG
			if (Logging.On && Logging.IsVerbose) Logging.Verbose(this, "GetKeyAsync", $"Getting key '{selector.ToString()}'");
#endif

			var key = await m_handler.GetKeyAsync(selector, snapshot: false, ct: m_cancellation).ConfigureAwait(false);

			// don't forget to truncate keys that would fall outside of the database's globalspace !
			return m_database.BoundCheck(key);
		}

		#endregion

		#region GetKeys..

		/// <summary>
		/// Resolves several key selectors against the keys in the database snapshot represented by the current transaction.
		/// </summary>
		/// <param name="selectors">Key selectors to resolve</param>
		/// <returns>Task that will return an array of keys matching the selectors, or an exception</returns>
		public Task<Slice[]> GetKeysAsync(KeySelector[] selectors)
		{
			EnsureCanRead();

			foreach (var selector in selectors)
			{
				m_database.EnsureKeyIsValid(selector.Key);
			}

#if DEBUG
			if (Logging.On && Logging.IsVerbose) Logging.Verbose(this, "GetKeysAsync", $"Getting batch of {selectors.Length} keys ...");
#endif

			return m_handler.GetKeysAsync(selectors, snapshot: false, ct: m_cancellation);
		}

		#endregion

		#region Set...

		/// <summary>
		/// Modify the database snapshot represented by transaction to change the given key to have the given value. If the given key was not previously present in the database it is inserted.
		/// The modification affects the actual database only if transaction is later committed with CommitAsync().
		/// </summary>
		/// <param name="key">Name of the key to be inserted into the database.</param>
		/// <param name="value">Value to be inserted into the database.</param>
		public void Set(Slice key, Slice value)
		{
			EnsureCanWrite();

			m_database.EnsureKeyIsValid(ref key);
			m_database.EnsureValueIsValid(ref value);

#if DEBUG
			if (Logging.On && Logging.IsVerbose) Logging.Verbose(this, "Set", $"Setting '{FdbKey.Dump(key)}' = {Slice.Dump(value)}");
#endif

			m_handler.Set(key, value);
		}

		#endregion

		#region Atomic Ops...

		/// <summary>Checks that this type of mutation is supported by the currently selected API level</summary>
		/// <param name="mutation">Mutation type</param>
		/// <param name="selectedApiVersion">Select API level (200, 300, ...)</param>
		/// <exception cref="FdbException">An error with code <see cref="FdbError.InvalidMutationType"/> if the type of mutation is not supported by this API level.</exception>
		private static void EnsureMutationTypeIsSupported(FdbMutationType mutation, int selectedApiVersion)
		{
			if (selectedApiVersion < 200)
			{ // mutations were not available at this time

				if (Fdb.GetMaxApiVersion() >= 200)
				{ // but the installed client could support it
					throw new FdbException(FdbError.InvalidMutationType, "Atomic mutations are only supported starting from API level 200. You need to select API level 200 or more at the start of your process.");
				}
				else
				{ // not supported by the local client
					throw new FdbException(FdbError.InvalidMutationType, "Atomic mutations are only supported starting from client version 2.x. You need to update the version of the client, and select API level 200 or more at the start of your process.");
				}
			}

			if (mutation == FdbMutationType.Add || mutation == FdbMutationType.BitAnd || mutation == FdbMutationType.BitOr || mutation == FdbMutationType.BitXor )
			{ // these mutations are available since v200
				return;
			}

			if (mutation == FdbMutationType.Max || mutation == FdbMutationType.Min)
			{ // these mutations are available since v300
				if (selectedApiVersion < 300)
				{
					if (Fdb.GetMaxApiVersion() >= 300)
					{
						throw new FdbException(FdbError.InvalidMutationType, "Atomic mutations Max and Min are only supported starting from API level 300. You need to select API level 300 or more at the start of your process.");
					}
					else
					{
						throw new FdbException(FdbError.InvalidMutationType, "Atomic mutations Max and Min are only supported starting from client version 3.x. You need to update the version of the client, and select API level 300 or more at the start of your process..");
					}
				}
				// ok!
				return;
			}

			if (mutation == FdbMutationType.VersionStampedKey || mutation == FdbMutationType.VersionStampedValue)
			{
				if (selectedApiVersion < 400)
				{
					if (Fdb.GetMaxApiVersion() >= 400)
					{
						throw new FdbException(FdbError.InvalidMutationType, "Atomic mutations for VersionStamps are only supported starting from API level 400. You need to select API level 400 or more at the start of your process.");
					}
					else
					{
						throw new FdbException(FdbError.InvalidMutationType, "Atomic mutations Max and Min are only supported starting from client version 4.x. You need to update the version of the client, and select API level 400 or more at the start of your process..");
					}
				}
				// ok!
				return;
			}

			// this could be a new mutation type, or an invalid value.
			throw new FdbException(FdbError.InvalidMutationType, "An invalid mutation type was issued. If you are attempting to call a new mutation type, you will need to update the version of this assembly, and select the latest API level.");
		}

		/// <summary>Modify the database snapshot represented by this transaction to perform the operation indicated by <paramref name="mutation"/> with operand <paramref name="param"/> to the value stored by the given key.</summary>
		/// <param name="key">Name of the key whose value is to be mutated.</param>
		/// <param name="param">Parameter with which the atomic operation will mutate the value associated with key_name.</param>
		/// <param name="mutation">Type of mutation that should be performed on the key</param>
		public void Atomic(Slice key, Slice param, FdbMutationType mutation)
		{
			//note: this method as many names in the various bindings:
			// - C API   : fdb_transaction_atomic_op(...)
			// - Java    : tr.Mutate(..)
			// - Node.js : tr.add(..), tr.max(..), ...

			EnsureCanWrite();

			m_database.EnsureKeyIsValid(ref key);
			m_database.EnsureValueIsValid(ref param);

			//The C API does not fail immediately if the mutation type is not valid, and only fails at commit time.
			EnsureMutationTypeIsSupported(mutation, Fdb.ApiVersion);

#if DEBUG
			if (Logging.On && Logging.IsVerbose) Logging.Verbose(this, "AtomicCore", $"Atomic {mutation.ToString()} on '{FdbKey.Dump(key)}' = {Slice.Dump(param)}");
#endif

			m_handler.Atomic(key, param, mutation);
		}

		#endregion

		#region Clear...

		/// <summary>
		/// Modify the database snapshot represented by transaction to remove the given key from the database. If the key was not previously present in the database, there is no effect.
		/// </summary>
		/// <param name="key">Name of the key to be removed from the database.</param>
		public void Clear(Slice key)
		{
			EnsureCanWrite();

			m_database.EnsureKeyIsValid(ref key);

#if DEBUG
			if (Logging.On && Logging.IsVerbose) Logging.Verbose(this, "Clear", $"Clearing '{FdbKey.Dump(key)}'");
#endif

			m_handler.Clear(key);
		}

		#endregion

		#region Clear Range...

		/// <summary>
		/// Modify the database snapshot represented by transaction to remove all keys (if any) which are lexicographically greater than or equal to the given begin key and lexicographically less than the given end_key.
		/// Sets and clears affect the actual database only if transaction is later committed with CommitAsync().
		/// </summary>
		/// <param name="beginKeyInclusive">Name of the key specifying the beginning of the range to clear.</param>
		/// <param name="endKeyExclusive">Name of the key specifying the end of the range to clear.</param>
		public void ClearRange(Slice beginKeyInclusive, Slice endKeyExclusive)
		{
			EnsureCanWrite();

			m_database.EnsureKeyIsValid(ref beginKeyInclusive);
			m_database.EnsureKeyIsValid(ref endKeyExclusive, endExclusive: true);

#if DEBUG
			if (Logging.On && Logging.IsVerbose) Logging.Verbose(this, "ClearRange", $"Clearing Range '{beginKeyInclusive.ToString()}' <= k < '{endKeyExclusive.ToString()}'");
#endif

			m_handler.ClearRange(beginKeyInclusive, endKeyExclusive);
		}

		#endregion

		#region Conflict Range...

		/// <summary>
		/// Adds a conflict range to a transaction without performing the associated read or write.
		/// </summary>
		/// <param name="beginKeyInclusive">Key specifying the beginning of the conflict range. The key is included</param>
		/// <param name="endKeyExclusive">Key specifying the end of the conflict range. The key is excluded</param>
		/// <param name="type">One of the FDBConflictRangeType values indicating what type of conflict range is being set.</param>
		public void AddConflictRange(Slice beginKeyInclusive, Slice endKeyExclusive, FdbConflictRangeType type)
		{
			EnsureCanWrite();

			m_database.EnsureKeyIsValid(ref beginKeyInclusive);
			m_database.EnsureKeyIsValid(ref endKeyExclusive, endExclusive: true);

#if DEBUG
			if (Logging.On && Logging.IsVerbose) Logging.Verbose(this, "AddConflictRange", String.Format("Adding {2} conflict range '{0}' <= k < '{1}'", beginKeyInclusive.ToString(), endKeyExclusive.ToString(), type.ToString()));
#endif

			m_handler.AddConflictRange(beginKeyInclusive, endKeyExclusive, type);
		}

		#endregion

		#region GetAddressesForKey...

		/// <summary>
		/// Returns a list of public network addresses as strings, one for each of the storage servers responsible for storing <paramref name="key"/> and its associated value
		/// </summary>
		/// <param name="key">Name of the key whose location is to be queried.</param>
		/// <returns>Task that will return an array of strings, or an exception</returns>
		public Task<string[]> GetAddressesForKeyAsync(Slice key)
		{
			EnsureCanRead();

			m_database.EnsureKeyIsValid(ref key);

#if DEBUG
			if (Logging.On && Logging.IsVerbose) Logging.Verbose(this, "GetAddressesForKeyAsync", $"Getting addresses for key '{FdbKey.Dump(key)}'");
#endif

			return m_handler.GetAddressesForKeyAsync(key, ct: m_cancellation);
		}

		#endregion

		#region Commit...

		/// <summary>
		/// Attempts to commit the sets and clears previously applied to the database snapshot represented by this transaction to the actual database. 
		/// The commit may or may not succeed – in particular, if a conflicting transaction previously committed, then the commit must fail in order to preserve transactional isolation. 
		/// If the commit does succeed, the transaction is durably committed to the database and all subsequently started transactions will observe its effects.
		/// </summary>
		/// <returns>Task that succeeds if the transaction was comitted successfully, or fails if the transaction failed to commit.</returns>
		/// <remarks>As with other client/server databases, in some failure scenarios a client may be unable to determine whether a transaction succeeded. In these cases, CommitAsync() will throw CommitUnknownResult error. The OnErrorAsync() function treats this error as retryable, so retry loops that don’t check for CommitUnknownResult could execute the transaction twice. In these cases, you must consider the idempotence of the transaction.</remarks>
		public async Task CommitAsync()
		{
			EnsureCanWrite();

			if (Logging.On) Logging.Verbose(this, "CommitAsync", "Committing transaction...");

			//TODO: need a STATE_COMMITTING ?
			try
			{
				await m_handler.CommitAsync(m_cancellation).ConfigureAwait(false);

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

		#region Watches...

		/// <summary>
		/// Watch a key for any change in the database.
		/// </summary>
		/// <param name="key">Key to watch</param>
		/// <param name="ct">CancellationToken used to abort the watch if the caller doesn't want to wait anymore. Note that you can manually cancel the watch by calling Cancel() on the returned FdbWatch instance</param>
		/// <returns>FdbWatch that can be awaited and will complete when the key has changed in the database, or cancellation occurs. You can call Cancel() at any time if you are not interested in watching the key anymore. You MUST always call Dispose() if the watch completes or is cancelled, to ensure that resources are released properly.</returns>
		/// <remarks>You can directly await an FdbWatch, or obtain a <see cref="Task{T}">Task&lt;Slice&gt;</see> by reading the <see cref="FdbWatch.Task"/> property</remarks>
		public FdbWatch Watch(Slice key, CancellationToken ct)
		{
			ct.ThrowIfCancellationRequested();
			EnsureCanRead();

			m_database.EnsureKeyIsValid(ref key);

			// keep a copy of the key
			// > don't keep a reference on a potentially large buffer while the watch is active, preventing it from being garbage collected
			// > allow the caller to reuse freely the slice underlying buffer, without changing the value that we will return when the task completes
			key = key.Memoize();

#if DEBUG
			if (Logging.On) Logging.Verbose(this, "WatchAsync", $"Watching key '{key.ToString()}'");
#endif

			// Note: the FDBFuture returned by 'fdb_transaction_watch()' outlives the transaction, and can only be cancelled with 'fdb_future_cancel()' or 'fdb_future_destroy()'
			// Since Task<T> does not expose any cancellation mechanism by itself (and we don't want to force the caller to create a CancellationTokenSource everytime),
			// we will return the FdbWatch that wraps the FdbFuture<Slice> directly, since it knows how to cancel itself.

			return m_handler.Watch(key, ct);
		}

		#endregion

		#region OnError...

		/// <summary>
		/// Implements the recommended retry and backoff behavior for a transaction.
		/// 
		/// This function knows which of the error codes generated by other query functions represent temporary error conditions and which represent application errors that should be handled by the application. 
		/// It also implements an exponential backoff strategy to avoid swamping the database cluster with excessive retries when there is a high level of conflict between transactions.
		/// </summary>
		/// <param name="code">FdbError code thrown by the previous command</param>
		/// <returns>Returns a task that completes if the operation can be safely retried, or that rethrows the original exception if the operation is not retryable.</returns>
		public async Task OnErrorAsync(FdbError code)
		{
			EnsureCanRetry();

			await m_handler.OnErrorAsync(code, ct: m_cancellation).ConfigureAwait(false);

			// If fdb_transaction_on_error succeeds, that means that the transaction has been reset and is usable again
			var state = this.State;
			if (state != STATE_DISPOSED) Interlocked.CompareExchange(ref m_state, STATE_READY, state);

			RestoreDefaultSettings();
		}

		#endregion

		#region Reset/Rollback/Cancel...

		private void RestoreDefaultSettings()
		{
			// resetting the state of a transaction automatically clears the RetryLimit and Timeout settings
			// => we need to set the again!

			m_timeout = 0;
			m_retryLimit = 0;
			m_maxRetryDelay = 0;

			if (m_database.DefaultRetryLimit > 0)
			{
				this.RetryLimit = m_database.DefaultRetryLimit;
			}
			if (m_database.DefaultMaxRetryDelay > 0)
			{
				this.MaxRetryDelay = m_database.DefaultMaxRetryDelay;
			}
			if (m_database.DefaultTimeout > 0)
			{
				this.Timeout = m_database.DefaultTimeout;
			}

			// if we have used a random token for versionstamps, we need to clear it (and generate a new one)
			// => this ensure that if the error was due to a collision between the token and another part of the key,
			//    a transaction retry will hopefully use a different token that does not collide.
			m_versionStampToken = 0;
		}

		/// <summary>Reset the transaction to its initial state.</summary>
		public void Reset()
		{
			EnsureCanRetry();

			if (Logging.On && Logging.IsVerbose) Logging.Verbose(this, "Reset", "Resetting transaction");

			m_handler.Reset();
			m_state = STATE_READY;

			RestoreDefaultSettings();

			if (Logging.On && Logging.IsVerbose) Logging.Verbose(this, "Reset", "Transaction has been reset");
		}

		/// <summary>Rollback this transaction, and dispose it. It should not be used after that.</summary>
		public void Cancel()
		{
			var state = Interlocked.CompareExchange(ref m_state, STATE_CANCELED, STATE_READY);
			if (state != STATE_READY)
			{
				switch(state)
				{
					case STATE_CANCELED: return; // already the case !

					case STATE_COMMITTED: throw new InvalidOperationException("Cannot cancel transaction that has already been committed");
					case STATE_FAILED: throw new InvalidOperationException("Cannot cancel transaction because it is in a failed state");
					case STATE_DISPOSED: throw new ObjectDisposedException("FdbTransaction", "Cannot cancel transaction because it already has been disposed");
					default: throw new InvalidOperationException($"Cannot cancel transaction because it is in unknown state {state}");
				}
			}

			if (Logging.On && Logging.IsVerbose) Logging.Verbose(this, "Cancel", "Canceling transaction...");

			m_handler.Cancel();

			if (Logging.On && Logging.IsVerbose) Logging.Verbose(this, "Cancel", "Transaction has been canceled");
		}

		#endregion

		#region IDisposable...

		/// <summary>Get/Sets the internal state of the exception</summary>
		internal int State
		{
			get => Volatile.Read(ref m_state);
			set
			{
				Contract.Requires(value >= STATE_DISPOSED && value <= STATE_FAILED, "Invalid state value");
				Volatile.Write(ref m_state, value);
			}
		}

		/// <summary>Throws if the transaction is not in a valid state (for reading/writing) and that we can proceed with a read operation</summary>
		public void EnsureCanRead()
		{
			// note: read operations are async, so they can NOT be called from the network without deadlocking the system !
			EnsureStilValid(allowFromNetworkThread: false, allowFailedState: false);
		}

		/// <summary>Throws if the transaction is not in a valid state (for writing) and that we can proceed with a write operation</summary>
		public void EnsureCanWrite()
		{
			if (m_readOnly) ThrowReadOnlyTransaction(this);
			// note: write operations are not async, and cannnot block, so it is (somewhat) safe to call them from the network thread itself.
			EnsureStilValid(allowFromNetworkThread: true, allowFailedState: false);
		}

		/// <summary>Throws if the transaction is not safely retryable</summary>
		public void EnsureCanRetry()
		{
			EnsureStilValid(allowFromNetworkThread: false, allowFailedState: true);
		}

		/// <summary>Throws if the transaction is not in a valid state (for reading/writing) and that we can proceed with a read or write operation</summary>
		/// <param name="allowFromNetworkThread">If true, this operation is allowed to run from a callback on the network thread and should NEVER block.</param>
		/// <param name="allowFailedState">If true, this operation can run even if the transaction is in a failed state.</param>
		/// <exception cref="System.ObjectDisposedException">If Dispose as already been called on the transaction</exception>
		/// <exception cref="System.InvalidOperationException">If CommitAsync() or Rollback() have already been called on the transaction, or if the database has been closed</exception>
		internal void EnsureStilValid(bool allowFromNetworkThread = false, bool allowFailedState = false)
		{
			// We must not be disposed
			if (allowFailedState ? this.State == STATE_DISPOSED : this.State != STATE_READY)
			{
				ThrowOnInvalidState(this);
			}

			// The cancellation token should not be signaled
			m_cancellation.ThrowIfCancellationRequested();

			// We cannot be called from the network thread (or else we will deadlock)
			if (!allowFromNetworkThread) Fdb.EnsureNotOnNetworkThread();

			// Ensure that the DB is still opened and that this transaction is still registered with it
			this.Database.EnsureTransactionIsValid(this);

			// we are ready to go !
		}

		/// <summary>Throws if the transaction is not in a valid state (for reading/writing)</summary>
		/// <exception cref="System.ObjectDisposedException">If Dispose as already been called on the transaction</exception>
		public void EnsureNotFailedOrDisposed()
		{
			switch (this.State)
			{
				case STATE_INIT:
				case STATE_READY:
				case STATE_COMMITTED:
				case STATE_CANCELED:
				{ // We are still valid
					// checks that the DB has not been disposed behind our back
					this.Database.EnsureTransactionIsValid(this);
					return;
				}

				default:
				{
					ThrowOnInvalidState(this);
					return;
				}
			}

		}

		[ContractAnnotation("=> halt")]
		internal static void ThrowOnInvalidState(FdbTransaction trans)
		{
			switch (trans.State)
			{
				case STATE_INIT: throw new InvalidOperationException("The transaction has not been initialized properly");
				case STATE_DISPOSED: throw new ObjectDisposedException("FdbTransaction", "This transaction has already been disposed and cannot be used anymore");
				case STATE_FAILED: throw new InvalidOperationException("The transaction is in a failed state and cannot be used anymore");
				case STATE_COMMITTED: throw new InvalidOperationException("The transaction has already been committed");
				case STATE_CANCELED: throw new FdbException(FdbError.TransactionCancelled, "The transaction has already been cancelled");
				default: throw new InvalidOperationException($"The transaction is unknown state {trans.State}");
			}
		}

		[ContractAnnotation("=> halt")]
		internal static void ThrowReadOnlyTransaction(FdbTransaction trans)
		{
			throw new InvalidOperationException("Cannot write to a read-only transaction");
		}

		/// <summary>
		/// Destroy the transaction and release all allocated resources, including all non-committed changes.
		/// </summary>
		/// <remarks>This instance will not be usable again and most methods will throw an ObjectDisposedException.</remarks>
		public void Dispose()
		{
			// note: we can be called by user code, or by the FdbDatabase when it is terminating with pending transactions
			if (Interlocked.Exchange(ref m_state, STATE_DISPOSED) != STATE_DISPOSED)
			{
				try
				{
					this.Database.UnregisterTransaction(this);
					m_cts.SafeCancelAndDispose();

					if (Logging.On) Logging.Verbose(this, "Dispose", $"Transaction #{m_id} has been disposed");
				}
				finally
				{
					// Dispose of the handle
					if (m_handler != null)
					{
						try { m_handler.Dispose(); }
						catch(Exception e)
						{
							if (Logging.On) Logging.Error(this, "Dispose", $"Transaction #{m_id} failed to dispose the transaction handler: [{e.GetType().Name}] {e.Message}");
						}
					}
					if (!m_context.Shared) m_context.Dispose();
					m_cts.Dispose();
				}
			}
		}

		#endregion

	}

}
