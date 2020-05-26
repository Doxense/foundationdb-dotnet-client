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

// enable this to help debug Transactions
//#define DEBUG_TRANSACTIONS

namespace FoundationDB.Client
{
	using System;
	using System.Collections.Generic;
	using System.Diagnostics;
	using System.Diagnostics.CodeAnalysis;
	using System.Runtime.CompilerServices;
	using System.Threading;
	using System.Threading.Tasks;
	using Doxense.Diagnostics.Contracts;
	using Doxense.Memory;
	using Doxense.Threading.Tasks;
	using FoundationDB.Client.Core;
	using FoundationDB.Client.Native;
	using FoundationDB.Filters.Logging;
	using JetBrains.Annotations;

	/// <summary>FoundationDB transaction handle.</summary>
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
		// => to bit flag so that we can have more options? ("write only", etc...)

		private readonly IFdbTransactionHandler m_handler;

		/// <summary>Timeout (in ms) of this transaction</summary>
		private int m_timeout;

		/// <summary>Retry Limit of this transaction</summary>
		private int m_retryLimit;

		/// <summary>Max Retry Delay (in ms) of this transaction</summary>
		private int m_maxRetryDelay;

		/// <summary>Cancellation source specific to this instance.</summary>
		private readonly CancellationTokenSource m_cts;

		/// <summary>CancellationToken that should be used for all async operations executing inside this transaction</summary>
		private CancellationToken m_cancellation;

		/// <summary>Random token (but constant per transaction retry) used to generate incomplete VersionStamps</summary>
		private ulong m_versionStampToken;

		/// <summary>Contains the log used by this transaction (or null if logging is disabled)</summary>
		private FdbTransactionLog? m_log;

		private Action<FdbTransactionLog>? m_logHandler;

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

		/// <inheritdoc />
		public int Id => m_id;

		/// <inheritdoc />
		public bool IsSnapshot => false;

		/// <inheritdoc />
		public FdbOperationContext Context => m_context;

		/// <summary>Database instance that manages this transaction</summary>
		public FdbDatabase Database => m_database;

		/// <summary>Returns the handler for this transaction</summary>
		internal IFdbTransactionHandler Handler => m_handler;

		/// <summary>If true, the transaction is still pending (not committed or rolled back).</summary>
		internal bool StillAlive => this.State == STATE_READY;

		/// <inheritdoc />
		public int Size => m_handler.Size;

		/// <inheritdoc />
		public CancellationToken Cancellation => m_cancellation;

		/// <inheritdoc />
		public bool IsReadOnly => m_readOnly;

		#endregion

		#region Options..

		#region Properties...

		/// <inheritdoc />
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

		/// <inheritdoc />
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

		/// <inheritdoc />
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

		/// <inheritdoc />
		public void SetOption(FdbTransactionOption option)
		{
			EnsureNotFailedOrDisposed();

			if (Logging.On && Logging.IsVerbose) Logging.Verbose(this, "SetOption", $"Setting transaction option {option.ToString()}");

			m_log?.Annotate($"SetOption({option})");
			m_handler.SetOption(option, default);
		}

		/// <inheritdoc />
		public void SetOption(FdbTransactionOption option, string value)
		{
			EnsureNotFailedOrDisposed();

			if (Logging.On && Logging.IsVerbose) Logging.Verbose(this, "SetOption", $"Setting transaction option {option.ToString()} to '{value ?? "<null>"}'");

			m_log?.Annotate($"SetOption({option}, \"{value}\")");
			var data = FdbNative.ToNativeString(value.AsSpan(), nullTerminated: false);
			m_handler.SetOption(option, data.Span);
		}

		/// <inheritdoc />
		public void SetOption(FdbTransactionOption option, ReadOnlySpan<char> value)
		{
			EnsureNotFailedOrDisposed();

			if (Logging.On && Logging.IsVerbose) Logging.Verbose(this, "SetOption", $"Setting transaction option {option.ToString()} to '{value.ToString() ?? "<null>"}'");

			m_log?.Annotate($"SetOption({option}, \"{value.ToString()}\")");
			var data = FdbNative.ToNativeString(value, nullTerminated: false);
			m_handler.SetOption(option, data.Span);
		}

		/// <inheritdoc />
		public void SetOption(FdbTransactionOption option, long value)
		{
			EnsureNotFailedOrDisposed();

			if (Logging.On && Logging.IsVerbose) Logging.Verbose(this, "SetOption", $"Setting transaction option {option.ToString()} to {value}");

			m_log?.Annotate($"SetOption({option}, {value})");

			// Spec says: "If the option is documented as taking an Int parameter, value must point to a signed 64-bit integer (little-endian), and value_length must be 8."
			Span<byte> tmp = stackalloc byte[8];
			UnsafeHelpers.WriteFixed64(tmp, (ulong) value);
			m_handler.SetOption(option, tmp);
		}

		#endregion

		#region Logging...

		/// <summary>Log of all operations performed on this transaction (if logging was enabled on the database or transaction)</summary>
		public FdbTransactionLog? Log => m_log;

		/// <summary>Return <c>true</c> if logging is enabled on this transaction</summary>
		/// <remarks>
		/// If logging is enabled, the transaction will track all the operations performed by this transaction until it completes.
		/// The log can be accessed via the <see cref="Log"/> property.
		/// Comments can be added via the <see cref="Annotate"/> method.
		/// </remarks>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool IsLogged() => m_log != null;

		/// <summary>Add a comment to the transaction log</summary>
		/// <param name="comment">Line of text that will be added to the log</param>
		/// <remarks>This method does nothing if logging is disabled. To prevent unnecessary allocations, you may check <see cref="IsLogged"/> first</remarks>
		/// <example><code>if (tr.IsLogged()) tr.Annonate($"Reticulated {splines.Count} splines");</code></example>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void Annotate(string comment)
		{
			m_log?.Annotate(comment);
		}

		/// <summary>If logging was previously enabled on this transaction, clear the log and stop logging any new operations</summary>
		/// <remarks>Any log handler attached to this transaction will not be called</remarks>
		public void StopLogging()
		{
			m_log = null;
		}

		internal void SetLogHandler(Action<FdbTransactionLog> handler, FdbLoggingOptions options)
		{
			if (m_log != null) throw new InvalidOperationException("There is already a log handler attached to this transaction.");
			m_logHandler = handler;
			m_log = new FdbTransactionLog(options);
			m_log.Start(this);
		}

		#endregion

		#region Versions...

		private Task<long>? CachedReadVersion;

		/// <inheritdoc />
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public Task<long> GetReadVersionAsync()
		{
			// can be called after the transaction has been committed
			EnsureCanRetry();
			return this.CachedReadVersion ?? GetReadVersionSlow();
		}

		/// <summary>Get the read version when it is not in cache</summary>
		[MethodImpl(MethodImplOptions.NoInlining)]
		private Task<long> GetReadVersionSlow()
		{
			lock (this)
			{
				return this.CachedReadVersion ??= FetchReadVersionInternal();
			}
		}

		[MethodImpl(MethodImplOptions.NoInlining)]
		private Task<long> FetchReadVersionInternal()
		{
			if (m_log != null)
			{
				return m_log.ExecuteAsync(
					this,
					new FdbTransactionLog.GetReadVersionCommand(),
					(tr, cmd) => tr.m_handler.GetReadVersionAsync(tr.m_cancellation)
				);
			}
			else
			{
				return m_handler.GetReadVersionAsync(m_cancellation);
			}
		}

		/// <inheritdoc />
		public long GetCommittedVersion()
		{
			//TODO: should we only allow calls if transaction is in state "COMMITTED" ?
			EnsureNotFailedOrDisposed();

			return m_handler.GetCommittedVersion();
		}

		/// <inheritdoc />
		public void SetReadVersion(long version)
		{
			EnsureCanRead();

			m_log?.Annotate($"Set read version to {version:N0}");
			m_handler.SetReadVersion(version);
		}

		/// <inheritdoc />
		public Task<VersionStamp?> GetMetadataVersionKeyAsync(Slice key = default)
		{
			return GetMetadataVersionKeyAsync(key.IsNull ? Fdb.System.MetadataVersionKey : key, snapshot: false);
		}

		/// <inheritdoc />
		public void TouchMetadataVersionKey(Slice key = default)
		{
			SetMetadataVersionKey(key.IsNull ? Fdb.System.MetadataVersionKey : key);
		}

		// all access to this should be made under lock!
		private Dictionary<Slice, (Task<VersionStamp?> Task, bool Snapshot)>? MetadataVersionKeysCache;

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private Dictionary<Slice, (Task<VersionStamp?> Task, bool Snapshot)> GetMetadataVersionKeysCache()
		{
			return this.MetadataVersionKeysCache ?? GetMetadataVersionKeysCacheSlow();
		}

		[MethodImpl(MethodImplOptions.NoInlining)]
		private Dictionary<Slice, (Task<VersionStamp?> Task, bool Snapshot)> GetMetadataVersionKeysCacheSlow()
		{
			lock(this)
			{
				return this.MetadataVersionKeysCache ??= new Dictionary<Slice, (Task<VersionStamp?>, bool)>(Slice.Comparer.Default);
			}
		}

		private static readonly Task<VersionStamp?> PoisonedMetadataVersion = Task.FromResult<VersionStamp?>(null);

		/// <summary>Fetch and parse the metadataVersion system key</summary>
		/// <param name="key">Key to read</param>
		/// <param name="snapshot">If false, add a read conflict range on the key.</param>
		internal Task<VersionStamp?> GetMetadataVersionKeyAsync(Slice key, bool snapshot)
		{
			if (key.IsNull) throw new ArgumentNullException(nameof(key));

			EnsureCanRetry();

			(Task<VersionStamp?> Task, bool Snapshot) t;

			// only add the range if we read in non-snapshot isolation for the first time
			bool mustAddConflictRange = false;

			// note: we have to lock because there could be another thread calling TouchMetadataVersionKey at the same time!
			// concurrent calls will be serialized internally by the network thread, which should prevent a concurrent write from causing us to throw error code 1036

			lock (this)
			{
				var cache = GetMetadataVersionKeysCache();
				if (!cache.TryGetValue(key, out t) || t.Task == null)
				{
					mustAddConflictRange = !snapshot;
					t = (ReadAndParseMetadataVersionSlow(key), snapshot);
					cache[key] = t;
				}
				else if (t.Snapshot)
				{ // previous reads were done in snapshot isolation

					if (!snapshot)
					{ // but this read is not, so we have to add the conflict range!
						mustAddConflictRange = true;
						t.Snapshot = false;
						cache[key] = t;
					}
				}
			}

			// for non-snapshot reads on writable transactions, we have to add the conflict range ourselves.
			if (mustAddConflictRange && !this.IsReadOnly)
			{
				//TODO: only on the first non-snapshot read!!!
				var range = KeyRange.FromKey(key);
				AddConflictRange(range.Begin.Span, range.End.Span, FdbConflictRangeType.Read);
			}
			return t.Task;
		}

		private async Task<VersionStamp?> ReadAndParseMetadataVersionSlow(Slice key)
		{
			Contract.Requires(!key.IsNull);

			Slice value;
			try
			{
				// this can fail if the value has been changed earlier in the transaction!
				value = await PerformGetOperation(key.Span, snapshot: true).ConfigureAwait(false);
			}
			catch (FdbException e)
			{
				if (e.Code == FdbError.AccessedUnreadable)
				{ // happens whenever the value has already been changed in the transaction!
					// Basically this means "we don't know yet", and the caller should decide on a case by case how to deal with it

					//note: the transaction will FAIL to commit! this only helps for transactions that decide to not commit given this result.
					lock (this)
					{
						// poison this key!
						GetMetadataVersionKeysCache()[key] = (FdbTransaction.PoisonedMetadataVersion, false);
					}

					return null;
				}
				throw;
			}

			// if the db is new, it is possible that the version is not yet present, we will return the empty stamp
			return value.IsNullOrEmpty ? VersionStamp.Complete(0, 0) : VersionStamp.Parse(value);
		}

		internal void SetMetadataVersionKey(Slice key)
		{
			//The C API does not fail immediately if the mutation type is not valid, and only fails at commit time.
			EnsureMutationTypeIsSupported(FdbMutationType.VersionStampedValue, Fdb.ApiVersion);

			// note: we have to lock because there could be another thread calling GetMetadataVersionKey at the same time!
			// concurrent calls will be serialized internally by the network thread, which should prevent a concurrent read from throwing error code 1036

			lock (this)
			{
				var cache = GetMetadataVersionKeysCache();
				if (cache.TryGetValue(key, out var t) && t.Task == PoisonedMetadataVersion)
				{ // already done!
					return;
				}

				// mark this key as dirty to prevent any future read
				cache[key] = (PoisonedMetadataVersion, false);

				// update the key with a new versionstamp
				PerformAtomicOperation(key.Span, Fdb.System.MetadataVersionValue.Span, FdbMutationType.VersionStampedValue);
			}
		}

		/// <inheritdoc />
		public Task<VersionStamp> GetVersionStampAsync()
		{
			EnsureNotFailedOrDisposed();
			if (!this.StillAlive)
			{ // we have already been committed or cancelled?
				ThrowOnInvalidState(this);
			}
			return m_handler.GetVersionStampAsync(m_cancellation);
		}

		private ulong GenerateNewVersionStampToken()
		{
			// We need to generate a 80-bits stamp, and also need to mark it as 'incomplete' by forcing the highest bit to 1.
			// Since this is supposed to be a version number with a ~1M tickrate per seconds, we will play it safe, and force the 8 highest bits to 1,
			// meaning that we only reduce the database potential lifetime but 1/256th, before getting into trouble.
			//
			// By doing some empirical testing, it also seems that the last 16 bits are a transaction batch order which is usually a low number.
			// Again, we will force the 4 highest bit to 1 to reduce the change of collision with a complete version stamp.
			//
			// So the final token will look like:  'FF xx xx xx xx xx xx xx Fy yy', were 'x' is the random token, and 'y' will lowest 12 bits of the transaction retry count

			ulong x;
			unsafe
			{
				// use a 128-bit guid as the source of entropy for our new token
				var rnd = Guid.NewGuid();
				ulong* p = (ulong*) &rnd;
				x = p[0] ^ p[1];
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

		/// <inheritdoc />
		[Pure]
		public VersionStamp CreateVersionStamp()
		{
			var token = m_versionStampToken;
			if (token == 0) token = GenerateNewVersionStampToken();
			return VersionStamp.Custom(token, (ushort) (m_context.Retries | 0xF000), incomplete: true);
		}

		/// <inheritdoc />
		public VersionStamp CreateVersionStamp(int userVersion)
		{
			var token = m_versionStampToken;
			if (token == 0) token = GenerateNewVersionStampToken();

			return VersionStamp.Custom(token, (ushort) (m_context.Retries | 0xF000), userVersion, incomplete: true);
		}

		/// <summary>Counter used to generated a unique unique versionstamps for this transaction.</summary>
		private int m_versionStampCounter;

		/// <inheritdoc />
		[Pure]
		public VersionStamp CreateUniqueVersionStamp()
		{
			int userVersion = Interlocked.Increment(ref m_versionStampCounter);
			if (userVersion > 0xFFF) throw new InvalidOperationException("Cannot generate more than 65535 unique VersionStamps per transaction!");
			return CreateVersionStamp(userVersion);
		}

		#endregion

		#region Get...

		/// <inheritdoc />
		public Task<Slice> GetAsync(ReadOnlySpan<byte> key)
		{
			EnsureCanRead();

			FdbKey.EnsureKeyIsValid(key);

#if DEBUG
			if (Logging.On && Logging.IsVerbose) Logging.Verbose(this, "GetAsync", $"Getting value for '{key.ToString()}'");
#endif

			return PerformGetOperation(key, snapshot: false);
		}

		private Task<Slice> PerformGetOperation(ReadOnlySpan<byte> key, bool snapshot)
		{
			if (m_log != null)
			{
				return m_log.ExecuteAsync(
					this,
					new FdbTransactionLog.GetCommand(m_log.Grab(key)) { Snapshot =  snapshot },
					(tr, cmd) => tr.m_handler.GetAsync(cmd.Key.Span, cmd.Snapshot, tr.m_cancellation)
				);
			}
			else
			{
				return m_handler.GetAsync(key, snapshot: snapshot, m_cancellation);
			}
		}

		/// <inheritdoc />
		public Task<(FdbValueCheckResult Result, Slice Actual)> CheckValueAsync(ReadOnlySpan<byte> key, Slice expected)
		{
			EnsureCanRead();

			FdbKey.EnsureKeyIsValid(key);

#if DEBUG
			if (Logging.On && Logging.IsVerbose) Logging.Verbose(this, "ValueCheckAsync", $"Checking the value for '{key.ToString()}'");
#endif

			return PerformValueCheckOperation(key, expected, snapshot: false);
		}

		private Task<(FdbValueCheckResult Result, Slice Actual)> PerformValueCheckOperation(ReadOnlySpan<byte> key, Slice expected, bool snapshot)
		{
			if (m_log != null)
			{
				return m_log.ExecuteAsync(
					this,
					new FdbTransactionLog.CheckValueCommand(m_log.Grab(key), m_log.Grab(expected)) { Snapshot =  snapshot },
					(tr, cmd) => tr.m_handler.CheckValueAsync(cmd.Key.Span, cmd.Expected, cmd.Snapshot, tr.m_cancellation)
				);
			}
			else
			{
				return m_handler.CheckValueAsync(key, expected, snapshot: snapshot, m_cancellation);
			}
		}

		#endregion

		#region GetValues...

		/// <inheritdoc />
		public Task<Slice[]> GetValuesAsync(Slice[] keys)
		{
			Contract.NotNull(keys, nameof(keys));
			//TODO: should we make a copy of the key array ?

			EnsureCanRead();

			FdbKey.EnsureKeysAreValid(keys);

#if DEBUG
			if (Logging.On && Logging.IsVerbose) Logging.Verbose(this, "GetValuesAsync", $"Getting batch of {keys.Length} values ...");
#endif

			return PerformGetValuesOperation(keys, snapshot: false);
		}

		private Task<Slice[]> PerformGetValuesOperation(Slice[] keys, bool snapshot)
		{
			if (m_log != null)
			{
				return m_log.ExecuteAsync(
					this,
					new FdbTransactionLog.GetValuesCommand(m_log.Grab(keys)) { Snapshot =  snapshot },
					(tr, cmd) => tr.m_handler.GetValuesAsync(cmd.Keys.AsSpan(), cmd.Snapshot, tr.m_cancellation)
				);
			}
			else
			{
				return m_handler.GetValuesAsync(keys, snapshot: snapshot, m_cancellation);
			}
		}

		#endregion

		#region GetRangeAsync...

		/// <inheritdoc />
		public Task<FdbRangeChunk> GetRangeAsync(
			KeySelector beginInclusive,
			KeySelector endExclusive,
			int limit = 0,
			bool reverse = false,
			int targetBytes = 0,
			FdbStreamingMode mode = FdbStreamingMode.Iterator,
			FdbReadMode read = FdbReadMode.Both,
			int iteration = 0)
		{
			EnsureCanRead();

			FdbKey.EnsureKeyIsValid(beginInclusive.Key);
			FdbKey.EnsureKeyIsValid(endExclusive.Key, endExclusive: true);

			FdbRangeOptions.EnsureLegalValues(limit, targetBytes, mode, read, iteration);

			// The iteration value is only needed when in iterator mode, but then it should start from 1
			if (iteration == 0) iteration = 1;

			return PerformGetRangeOperation(beginInclusive, endExclusive, snapshot: false, limit, reverse, targetBytes, mode, read, iteration);
		}

		private Task<FdbRangeChunk> PerformGetRangeOperation(
			KeySelector beginInclusive,
			KeySelector endExclusive,
			bool snapshot,
			int limit,
			bool reverse,
			int targetBytes,
			FdbStreamingMode mode,
			FdbReadMode read,
			int iteration)
		{
			if (m_log != null)
			{
				return m_log.ExecuteAsync(
					this,
					new FdbTransactionLog.GetRangeCommand(
						m_log.Grab(beginInclusive),
						m_log.Grab(endExclusive),
						new FdbRangeOptions(limit, reverse, targetBytes, mode, read),
						iteration
					)
					{
						Snapshot =  snapshot
					},
					(tr, cmd) => tr.m_handler.GetRangeAsync(cmd.Begin, cmd.End, cmd.Options.Limit.Value, cmd.Options.Reverse.Value, cmd.Options.TargetBytes.Value, cmd.Options.Mode.Value, cmd.Options.Read.Value, cmd.Iteration, cmd.Snapshot, tr.m_cancellation)
				);
			}
			else
			{
				return m_handler.GetRangeAsync(beginInclusive, endExclusive, limit, reverse, targetBytes, mode, read, iteration, snapshot, m_cancellation);
			}
		}

		#endregion

		#region GetRange...

		[Pure, LinqTunnel]
		internal FdbRangeQuery<TResult> GetRangeCore<TResult>(KeySelector begin, KeySelector end, FdbRangeOptions? options, bool snapshot, Func<KeyValuePair<Slice, Slice>, TResult> selector)
		{
			Contract.Requires(selector != null);

			EnsureCanRead();
			FdbKey.EnsureKeyIsValid(begin.Key);
			FdbKey.EnsureKeyIsValid(end.Key, endExclusive: true);

			options = FdbRangeOptions.EnsureDefaults(options, null, null, FdbStreamingMode.Iterator, FdbReadMode.Both, false);
			options.EnsureLegalValues();

#if DEBUG
			if (Logging.On && Logging.IsVerbose) Logging.Verbose(this, "GetRangeCore", $"Getting range '{begin.ToString()} <= x < {end.ToString()}'");
#endif

			return new FdbRangeQuery<TResult>(this, begin, end, selector, snapshot, options);
		}

		/// <inheritdoc />
		public FdbRangeQuery<KeyValuePair<Slice, Slice>> GetRange(KeySelector beginInclusive, KeySelector endExclusive, FdbRangeOptions? options = null)
		{
			return GetRangeCore(beginInclusive, endExclusive, options, snapshot: false, (kv) => kv);
		}

		/// <inheritdoc />
		public FdbRangeQuery<TResult> GetRange<TResult>(KeySelector beginInclusive, KeySelector endExclusive, Func<KeyValuePair<Slice, Slice>, TResult> selector, FdbRangeOptions? options = null)
		{
			return GetRangeCore(beginInclusive, endExclusive, options, snapshot: false, selector);
		}

		#endregion

		#region GetKey...

		/// <inheritdoc />
		public Task<Slice> GetKeyAsync(KeySelector selector)
		{
			EnsureCanRead();

			FdbKey.EnsureKeyIsValid(selector.Key);

#if DEBUG
			if (Logging.On && Logging.IsVerbose) Logging.Verbose(this, "GetKeyAsync", $"Getting key '{selector.ToString()}'");
#endif

			return PerformGetKeyOperation(selector, snapshot: false);
		}

		private Task<Slice> PerformGetKeyOperation(KeySelector selector, bool snapshot)
		{
			if (m_log != null)
			{
				return m_log.ExecuteAsync(
					this,
					new FdbTransactionLog.GetKeyCommand(m_log.Grab(selector)) { Snapshot =  snapshot },
					(tr, cmd) => tr.m_handler.GetKeyAsync(cmd.Selector, cmd.Snapshot, tr.m_cancellation)
				);
			}
			else
			{
				return m_handler.GetKeyAsync(selector, snapshot: snapshot, m_cancellation);
			}
		}

		#endregion

		#region GetKeys..

		/// <inheritdoc />
		public Task<Slice[]> GetKeysAsync(KeySelector[] selectors)
		{
			EnsureCanRead();

			foreach (var selector in selectors)
			{
				FdbKey.EnsureKeyIsValid(selector.Key);
			}

#if DEBUG
			if (Logging.On && Logging.IsVerbose) Logging.Verbose(this, "GetKeysAsync", $"Getting batch of {selectors.Length} keys ...");
#endif

			return PerformGetKeysOperation(selectors, snapshot: false);
		}


		private Task<Slice[]> PerformGetKeysOperation(KeySelector[] selectors, bool snapshot)
		{
			if (m_log != null)
			{
				return m_log.ExecuteAsync(
					this,
					new FdbTransactionLog.GetKeysCommand(m_log.Grab(selectors)) { Snapshot =  snapshot },
					(tr, cmd) => tr.m_handler.GetKeysAsync(cmd.Selectors, cmd.Snapshot, tr.m_cancellation)
				);
			}
			else
			{
				return m_handler.GetKeysAsync(selectors, snapshot: snapshot, m_cancellation);
			}
		}

		#endregion

		#region Set...

		/// <inheritdoc />
		public void Set(ReadOnlySpan<byte> key, ReadOnlySpan<byte> value)
		{
			EnsureCanWrite();

			FdbKey.EnsureKeyIsValid(key);
			FdbKey.EnsureValueIsValid(value);

#if DEBUG
			if (Logging.On && Logging.IsVerbose) Logging.Verbose(this, "Set", $"Setting '{FdbKey.Dump(key)}' = {Slice.Dump(value)}");
#endif
			PerformSetOperation(key, value);
		}

		private void PerformSetOperation(ReadOnlySpan<byte> key, ReadOnlySpan<byte> value)
		{
			if (m_log != null)
			{
				m_log.Execute(
					this,
					new FdbTransactionLog.SetCommand(m_log.Grab(key), m_log.Grab(value)),
					(tr, cmd) => tr.m_handler.Set(cmd.Key.Span, cmd.Value.Span)
				);
			}
			else
			{
				m_handler.Set(key, value);
			}
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
					throw new NotSupportedException("Atomic mutations are only supported starting from API level 200. You need to select API level 200 or more at the start of your process.");
				}
				else
				{ // not supported by the local client
					throw new NotSupportedException("Atomic mutations are only supported starting from client version 2.x. You need to update the version of the client, and select API level 200 or more at the start of your process.");
				}
			}

			switch (mutation)
			{

				case FdbMutationType.Add:
				case FdbMutationType.BitAnd:
				case FdbMutationType.BitOr:
				case FdbMutationType.BitXor:
				{ // these mutations are available since v200
					return;
				}

				case FdbMutationType.Max:
				case FdbMutationType.Min:
				{ // these mutations are available since v300
					if (selectedApiVersion < 300)
					{
						if (Fdb.GetMaxApiVersion() >= 300)
						{
							throw new NotSupportedException("Atomic mutations Max and Min are only supported starting from API level 300. You need to select API level 300 or more at the start of your process.");
						}
						else
						{
							throw new NotSupportedException("Atomic mutations Max and Min are only supported starting from client version 3.x. You need to update the version of the client, and select API level 300 or more at the start of your process..");
						}
					}

					// ok!
					return;
				}

				case FdbMutationType.VersionStampedKey:
				case FdbMutationType.VersionStampedValue:
				{
					if (selectedApiVersion < 400)
					{
						if (Fdb.GetMaxApiVersion() >= 400)
						{
							throw new NotSupportedException("Atomic mutations for VersionStamps are only supported starting from API level 400. You need to select API level 400 or more at the start of your process.");
						}
						else
						{
							throw new NotSupportedException("Atomic mutations Max and Min are only supported starting from client version 4.x. You need to update the version of the client, and select API level 400 or more at the start of your process..");
						}
					}

					// ok!
					return;
				}

				case FdbMutationType.AppendIfFits:
				{
					if (selectedApiVersion < 520)
					{
						if (Fdb.GetMaxApiVersion() >= 520)
						{
							throw new NotSupportedException("Atomic mutation AppendIfFits is only supported starting from API level 520. You need to select API level 520 or more at the start of your process.");
						}
						else
						{
							throw new NotSupportedException("Atomic mutation AppendIfFits is only supported starting from client version 5.2. You need to update the version of the client, and select API level 520 or more at the start of your process..");
						}
					}

					// ok!
					return;
				}

				case FdbMutationType.CompareAndClear:
				{
					if (selectedApiVersion < 610)
					{
						if (Fdb.GetMaxApiVersion() >= 610)
						{
							throw new NotSupportedException("Atomic mutation CompareAndClear is only supported starting from API level 610. You need to select API level 610 or more at the start of your process.");
						}
						else
						{
							throw new NotSupportedException("Atomic mutation CompareAndClear is only supported starting from client version 6.1. You need to update the version of the client, and select API level 610 or more at the start of your process..");
						}
					}

					// ok!
					return;
				}

				default:
				{
					// this could be a new mutation type, or an invalid value.
					throw new NotSupportedException($"An invalid mutation type '{mutation}' was issued. If you are attempting to call a new mutation type, you will need to update the version of this assembly, and select the latest API level.");
				}
			}
		}

		/// <inheritdoc />
		public void Atomic(ReadOnlySpan<byte> key, ReadOnlySpan<byte> param, FdbMutationType mutation)
		{
			//note: this method as many names in the various bindings:
			// - C API   : fdb_transaction_atomic_op(...)
			// - Java    : tr.Mutate(..)
			// - Node.js : tr.add(..), tr.max(..), ...

			EnsureCanWrite();

			FdbKey.EnsureKeyIsValid(key);
			FdbKey.EnsureValueIsValid(param);

			//The C API does not fail immediately if the mutation type is not valid, and only fails at commit time.
			EnsureMutationTypeIsSupported(mutation, Fdb.ApiVersion);

#if DEBUG
			if (Logging.On && Logging.IsVerbose) Logging.Verbose(this, "AtomicCore", $"Atomic {mutation.ToString()} on '{FdbKey.Dump(key)}' = {Slice.Dump(param)}");
#endif

			PerformAtomicOperation(key, param, mutation);
		}

		private void PerformAtomicOperation(ReadOnlySpan<byte> key, ReadOnlySpan<byte> param, FdbMutationType type)
		{
			if (m_log != null)
			{
				m_log.Execute(
					this,
					new FdbTransactionLog.AtomicCommand(m_log.Grab(key), m_log.Grab(param), type),
					(tr, cmd) => tr.m_handler.Atomic(cmd.Key.Span, cmd.Param.Span, cmd.Mutation)
				);
			}
			else
			{
				m_handler.Atomic(key, param, type);
			
			}
		}

		#endregion

		#region Clear...

		/// <inheritdoc />
		public void Clear(ReadOnlySpan<byte> key)
		{
			EnsureCanWrite();

			FdbKey.EnsureKeyIsValid(key);

#if DEBUG
			if (Logging.On && Logging.IsVerbose) Logging.Verbose(this, "Clear", $"Clearing '{FdbKey.Dump(key)}'");
#endif

			PerformClearOperation(key);
		}

		private void PerformClearOperation(ReadOnlySpan<byte> key)
		{
			if (m_log != null)
			{
				m_log.Execute(
					this,
					new FdbTransactionLog.ClearCommand(m_log.Grab(key)),
					(tr, cmd) => tr.m_handler.Clear(cmd.Key.Span)
				);
			}
			else
			{
				m_handler.Clear(key);
			}
		}

		#endregion

		#region Clear Range...

		/// <inheritdoc />
		public void ClearRange(ReadOnlySpan<byte> beginKeyInclusive, ReadOnlySpan<byte> endKeyExclusive)
		{
			EnsureCanWrite();

			FdbKey.EnsureKeyIsValid(beginKeyInclusive);
			FdbKey.EnsureKeyIsValid(endKeyExclusive, endExclusive: true);

#if DEBUG
			if (Logging.On && Logging.IsVerbose) Logging.Verbose(this, "ClearRange", $"Clearing Range '{beginKeyInclusive.ToString()}' <= k < '{endKeyExclusive.ToString()}'");
#endif

			PerformClearRangeOperation(beginKeyInclusive, endKeyExclusive);
		}

		private void PerformClearRangeOperation(ReadOnlySpan<byte> beginKeyInclusive, ReadOnlySpan<byte> endKeyExclusive)
		{
			if (m_log != null)
			{
				m_log.Execute(
					this,
					new FdbTransactionLog.ClearRangeCommand(m_log.Grab(beginKeyInclusive), m_log.Grab(endKeyExclusive)),
					(tr, cmd) => tr.m_handler.ClearRange(cmd.Begin.Span, cmd.End.Span)
				);
			}
			else
			{
				m_handler.ClearRange(beginKeyInclusive, endKeyExclusive);
			}
		}

		#endregion

		#region Conflict Range...

		/// <inheritdoc />
		public void AddConflictRange(ReadOnlySpan<byte> beginKeyInclusive, ReadOnlySpan<byte> endKeyExclusive, FdbConflictRangeType type)
		{
			EnsureCanWrite();

			FdbKey.EnsureKeyIsValid(beginKeyInclusive);
			FdbKey.EnsureKeyIsValid(endKeyExclusive, endExclusive: true);

#if DEBUG
			if (Logging.On && Logging.IsVerbose) Logging.Verbose(this, "AddConflictRange", String.Format("Adding {2} conflict range '{0}' <= k < '{1}'", beginKeyInclusive.ToString(), endKeyExclusive.ToString(), type.ToString()));
#endif

			PerformAddConflictRangeOperation(beginKeyInclusive, endKeyExclusive, type);
		}

		private void PerformAddConflictRangeOperation(ReadOnlySpan<byte> beginKeyInclusive, ReadOnlySpan<byte> endKeyExclusive, FdbConflictRangeType type)
		{
			if (m_log != null)
			{
				m_log.Execute(
					this,
					new FdbTransactionLog.AddConflictRangeCommand(m_log.Grab(beginKeyInclusive), m_log.Grab(endKeyExclusive), type),
					(tr, cmd) => tr.m_handler.AddConflictRange(cmd.Begin.Span, cmd.End.Span, cmd.Type)
				);
			}
			else
			{
				m_handler.AddConflictRange(beginKeyInclusive, endKeyExclusive, type);
			}
		}

		#endregion

		#region GetAddressesForKey...

		/// <inheritdoc />
		public Task<string[]> GetAddressesForKeyAsync(ReadOnlySpan<byte> key)
		{
			EnsureCanRead();

			FdbKey.EnsureKeyIsValid(key);

#if DEBUG
			if (Logging.On && Logging.IsVerbose) Logging.Verbose(this, "GetAddressesForKeyAsync", $"Getting addresses for key '{FdbKey.Dump(key)}'");
#endif

			return PerformGetAddressesForKeyOperation(key);
		}

		private Task<string[]> PerformGetAddressesForKeyOperation(ReadOnlySpan<byte> key)
		{
			if (m_log != null)
			{
				return m_log.ExecuteAsync(
					this,
					new FdbTransactionLog.GetAddressesForKeyCommand(m_log.Grab(key)),
					(tr, cmd) => tr.m_handler.GetAddressesForKeyAsync(cmd.Key.Span, tr.m_cancellation)
				);
			}
			else
			{
				return m_handler.GetAddressesForKeyAsync(key, m_cancellation);
			}
		}

		#endregion

		#region GetApproximateSize...

		/// <inheritdoc />
		public Task<long> GetApproximateSizeAsync()
		{
			EnsureCanWrite();

			return PerformGetApproximateSizeOperation();
		}

		private Task<long> PerformGetApproximateSizeOperation()
		{
			if (m_log != null)
			{
				return m_log.ExecuteAsync(
					this,
					new FdbTransactionLog.GetApproximateSizeCommand(),
					(tr, cmd) => tr.m_handler.GetApproximateSizeAsync(tr.m_cancellation)
				);
			}
			else
			{
				return m_handler.GetApproximateSizeAsync(m_cancellation);
			}
		}

		#endregion

		#region Commit...

		/// <inheritdoc />
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

		private Task PerformCommitOperation()
		{
			if (m_log != null)
			{
				int size = this.Size;
				m_log.CommitSize = size;
				m_log.TotalCommitSize += size;
				m_log.Attempts++;

				Task<VersionStamp>? tvs = m_log.RequiresVersionStamp ? m_handler.GetVersionStampAsync(m_cancellation) : null;

				return m_log.ExecuteAsync(
					this,
					new FdbTransactionLog.CommitCommand(),
					(tr, cmd) => tr.m_handler.CommitAsync(tr.m_cancellation),
					(tr, cmd, log) =>
					{
						log.CommittedUtc = DateTimeOffset.UtcNow;
						var cv = tr.GetCommittedVersion();
						log.CommittedVersion = cv;
						cmd.CommitVersion = cv;
						if (tvs != null) log.VersionStamp = tvs.GetAwaiter().GetResult();
					}
				);

			}
			else
			{
				return m_handler.CommitAsync(m_cancellation);
			}
		}

		#endregion

		#region Watches...

		/// <inheritdoc />
		[Pure]
		public FdbWatch Watch(ReadOnlySpan<byte> key, CancellationToken ct)
		{
			//note: the caller CANNOT use the transaction's own token, or else the watch would not survive after the commit, rendering it useless
			if (ct.CanBeCanceled && ct.Equals(m_cancellation))
			{
				throw new ArgumentException("You cannot use the transaction's own cancellation token, because the Watch will need to execute after the transaction has completed. You may use the same token that was used by the parent retry loop, or any other token.");
			}

			ct.ThrowIfCancellationRequested();
			EnsureCanWrite();

			FdbKey.EnsureKeyIsValid(key);

			// keep a copy of the key
			// > don't keep a reference on a potentially large buffer while the watch is active, preventing it from being garbage collected
			// > allow the caller to reuse freely the slice underlying buffer, without changing the value that we will return when the task completes
			var mkey = Slice.Copy(key);

#if DEBUG
			if (Logging.On) Logging.Verbose(this, "WatchAsync", $"Watching key '{mkey.ToString()}'");
#endif

			// Note: the FDBFuture returned by 'fdb_transaction_watch()' outlives the transaction, and can only be cancelled with 'fdb_future_cancel()' or 'fdb_future_destroy()'
			// Since Task<T> does not expose any cancellation mechanism by itself (and we don't want to force the caller to create a CancellationTokenSource every time),
			// we will return the FdbWatch that wraps the FdbFuture<Slice> directly, since it knows how to cancel itself.

			return PerformWatchOperation(mkey, ct);
		}

		private FdbWatch PerformWatchOperation(Slice key, CancellationToken ct)
		{
			m_log?.AddOperation(new FdbTransactionLog.WatchCommand(m_log.Grab(key)));
			return m_handler.Watch(key, ct);
		}

		#endregion

		#region OnError...

		/// <inheritdoc />
		public async Task OnErrorAsync(FdbError code)
		{
			EnsureCanRetry();

			await PerformOnErrorOperation(code).ConfigureAwait(false);

			// If fdb_transaction_on_error succeeds, that means that the transaction has been reset and is usable again
			var state = this.State;
			if (state != STATE_DISPOSED) Interlocked.CompareExchange(ref m_state, STATE_READY, state);

			RestoreDefaultSettings();
		}

		private Task PerformOnErrorOperation(FdbError code)
		{

			if (m_log != null)
			{
				m_log.RequiresVersionStamp = false;
				return m_log.ExecuteAsync(
					this,
					new FdbTransactionLog.OnErrorCommand(code),
					(tr, cmd) => tr.m_handler.OnErrorAsync(cmd.Code, tr.m_cancellation)
				);
			}
			else
			{
				return m_handler.OnErrorAsync(code, ct: m_cancellation);
			}
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

			// if we have used a random token for VersionStamps, we need to clear it (and generate a new one)
			// => this ensure that if the error was due to a collision between the token and another part of the key,
			//    a transaction retry will hopefully use a different token that does not collide.
			m_versionStampToken = 0;
			m_versionStampCounter = 0;

			// clear any cached local data!
			this.CachedReadVersion = null;
			this.MetadataVersionKeysCache = null;
			m_context.ClearAllLocalData();
		}

		/// <inheritdoc />
		public void Reset()
		{
			EnsureCanRetry();

			if (Logging.On && Logging.IsVerbose) Logging.Verbose(this, "Reset", "Resetting transaction");

			PerformResetOperation();

			m_state = STATE_READY;

			RestoreDefaultSettings();

			if (Logging.On && Logging.IsVerbose) Logging.Verbose(this, "Reset", "Transaction has been reset");
		}

		private void PerformResetOperation()
		{
			if (m_log != null)
			{
				m_log.RequiresVersionStamp = false;
				m_log.Execute(
					this,
					new FdbTransactionLog.ResetCommand(),
					(tr, cmd) => tr.m_handler.Reset()
				);
			}
			else
			{
				m_handler.Reset();
			}
		}

		/// <inheritdoc />
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

			PerformCancelOperation();

			if (Logging.On && Logging.IsVerbose) Logging.Verbose(this, "Cancel", "Transaction has been canceled");
		}

		private void PerformCancelOperation()
		{
			if (m_log != null)
			{
				m_log.Execute(
					this,
					new FdbTransactionLog.CancelCommand(),
					(tr, cmd) => tr.m_handler.Cancel()
				);
			}
			else
			{
				m_handler.Cancel();
			}
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
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void EnsureCanRead()
		{
			// note: read operations are async, so they can NOT be called from the network without deadlocking the system !
			EnsureStillValid(allowFromNetworkThread: false, allowFailedState: false);
		}

		/// <summary>Throws if the transaction is not in a valid state (for writing) and that we can proceed with a write operation</summary>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void EnsureCanWrite()
		{
			if (m_readOnly) throw ThrowReadOnlyTransaction(this);
			// note: write operations are not async, and cannnot block, so it is (somewhat) safe to call them from the network thread itself.
			EnsureStillValid(allowFromNetworkThread: true, allowFailedState: false);
		}

		/// <summary>Throws if the transaction is not safely retryable</summary>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void EnsureCanRetry()
		{
			EnsureStillValid(allowFromNetworkThread: false, allowFailedState: true);
		}

		/// <summary>Throws if the transaction is not in a valid state (for reading/writing) and that we can proceed with a read or write operation</summary>
		/// <param name="allowFromNetworkThread">If true, this operation is allowed to run from a callback on the network thread and should NEVER block.</param>
		/// <param name="allowFailedState">If true, this operation can run even if the transaction is in a failed state.</param>
		/// <exception cref="System.ObjectDisposedException">If Dispose as already been called on the transaction</exception>
		/// <exception cref="System.InvalidOperationException">If CommitAsync() or Rollback() have already been called on the transaction, or if the database has been closed</exception>
		internal void EnsureStillValid(bool allowFromNetworkThread = false, bool allowFailedState = false)
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

		[DoesNotReturn]
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

		[Pure, MethodImpl(MethodImplOptions.NoInlining)]
		internal static Exception ThrowReadOnlyTransaction(FdbTransaction trans)
		{
			return new InvalidOperationException("Cannot write to a read-only transaction");
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

					var context = m_context;
					context.ReleaseTransaction(this);
					if (!context.Shared)
					{
						context.Dispose();
					}
					m_cts.Dispose();

					if (m_log?.Stop(this) == true)
					{
						if (m_logHandler != null)
						{
							try
							{
								m_logHandler.Invoke(m_log);
							}
#if DEBUG
							catch(Exception e)
							{
								System.Diagnostics.Debug.WriteLine("Logged transaction handler failed: " + e.ToString());
							}
#else
							catch { }
#endif
						}
					}
				}
			}
		}

		#endregion

	}

}
