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
	using FoundationDB.Async;
	using FoundationDB.Client.Native;
	using FoundationDB.Client.Utils;
	using FoundationDB.Layers.Directories;
	using FoundationDB.Layers.Tuples;
	using System;
	using System.Collections.Concurrent;
	using System.Diagnostics;
	using System.Threading;
	using System.Threading.Tasks;

	/// <summary>FoundationDB Database</summary>
	/// <remarks>Wraps an FDBDatabase* handle</remarks>
	[DebuggerDisplay("Name={m_name}, GlobalSpace={m_globalSpace}")]
	public sealed class FdbDatabase : IFdbDatabase, IFdbTransactional, IDisposable
	{
		#region Private Fields...

		/// <summary>Parent cluster that owns the database.</summary>
		private readonly FdbCluster m_cluster;

		/// <summary>Handle that wraps the native FDB_DATABASE*</summary>
		private readonly DatabaseHandle m_handle;

		/// <summary>Name of the database (note: value it is the value that was passed to Connect(...) since we don't have any API to read the name from an FDB_DATABASE* handle)</summary>
		private readonly string m_name;

		/// <summary>If true, the cluster instance will be disposed at the same time as the current db instance.</summary>
		private readonly bool m_ownsCluster;

		/// <summary>If true, the database will only allow read-only transactions.</summary>
		private readonly bool m_readOnly;

		/// <summary>Global cancellation source that is cancelled when the current db instance gets disposed.</summary>
		private readonly CancellationTokenSource m_cts = new CancellationTokenSource();

		/// <summary>Set to true when the current db instance gets disposed.</summary>
		private volatile bool m_disposed;

		/// <summary>Global counters used to generate the transaction's local id (for debugging purpose)</summary>
		private static int s_transactionCounter;

		/// <summary>List of all "pending" transactions created from this database instance (and that have not yet been disposed)</summary>
		private readonly ConcurrentDictionary<int, FdbTransaction> m_transactions = new ConcurrentDictionary<int, FdbTransaction>();

		/// <summary>Global namespace used to prefix ALL keys and subspaces accessible by this database instance (default is empty)</summary>
		/// <remarks>This is readonly and is set when creating the database instance</remarks>
		private FdbSubspace m_globalSpace;
		/// <summary>Copy of the namespace, that is exposed to the outside.</summary>
		private FdbSubspace m_globalSpaceCopy;

		/// <summary>Default Timeout value for all transactions</summary>
		private int m_defaultTimeout;

		/// <summary>Default RetryLimit value for all transactions</summary>
		private int m_defaultRetryLimit;

#if REFACTORED
		/// <summary>Root directory used by this database instance (or null if none)</summary>
		private FdbDatabasePartition m_rootDirectory;
#endif

		#endregion

		#region Constructors...

		/// <summary>Create a new database instance</summary>
		/// <param name="cluster">Parent cluster</param>
		/// <param name="handle">Handle to the native FDB_DATABASE*</param>
		/// <param name="name">Name of the database</param>
		/// <param name="subspace">Root namespace of all keys accessible by this database instance</param>
		/// <param name="readOnly">If true, the database instance will only allow read-only transactions</param>
		/// <param name="ownsCluster">If true, the cluster instance lifetime is linked with the database instance</param>
		internal FdbDatabase(FdbCluster cluster, DatabaseHandle handle, string name, FdbSubspace subspace, bool readOnly, bool ownsCluster)
		{
			m_cluster = cluster;
			m_handle = handle;
			m_name = name;
			m_readOnly = readOnly;
			m_ownsCluster = ownsCluster;
			ChangeGlobalSpace(subspace);

#if REFACTORED
			// pre-initialize the directory to the default location (depending on the subspace)
			UseDefaultRootDirectory();
#endif
		}

		#endregion

		#region Public Properties...

		/// <summary>Cluster where the database is located</summary>
		public FdbCluster Cluster { get { return m_cluster; } }

		/// <summary>Name of the database</summary>
		public string Name { get { return m_name; } }

		/// <summary>Handle to the underlying FDB_DATABASE*</summary>
		internal DatabaseHandle Handle { get { return m_handle; } }

		/// <summary>Returns a cancellation token that is linked with the lifetime of this database instance</summary>
		/// <remarks>The token will be cancelled if the database instance is disposed</remarks>
		public CancellationToken Token { get { return m_cts.Token; } }

		public bool IsReadOnly { get { return m_readOnly; } }

		#endregion

		#region Transaction Management...

		/// <summary>Start a new transaction on this database</summary>
		/// <param name="cancellationToken">Optional cancellation token that can abort all pending async operations started by this transaction.</param>
		/// <returns>New transaction instance that can read from or write to the database.</returns>
		/// <remarks>You MUST call Dispose() on the transaction when you are done with it. You SHOULD wrap it in a 'using' statement to ensure that it is disposed in all cases.</remarks>
		/// <example>
		/// using(var tr = db.BeginTransaction(CancellationToken.None))
		/// {
		///		tr.Set(Slice.FromString("Hello"), Slice.FromString("World"));
		///		tr.Clear(Slice.FromString("OldValue"));
		///		await tr.CommitAsync();
		/// }</example>
		public IFdbTransaction BeginTransaction(FdbTransactionMode mode, CancellationToken cancellationToken = default(CancellationToken), FdbOperationContext context = null)
		{
			if (context == null) context = new FdbOperationContext(this, mode, cancellationToken);
			return CreateNewTransaction(context);
		}

		/// <summary>Start a new transaction on this database, with an optional context</summary>
		/// <param name="context">Optional context in which the transaction will run</param>
		/// <param name="readOnly">If true, only read operations are allowed, and all write attempts will throw.</param>
		internal FdbTransaction CreateNewTransaction(FdbOperationContext context)
		{
			Contract.Requires(context != null && context.Database != null);

			// force the transaction to be read-only, if the database itself is read-only
			var mode = context.Mode;
			if (m_readOnly) mode |= FdbTransactionMode.ReadOnly;

			ThrowIfDisposed();
			if (m_handle.IsInvalid) throw Fdb.Errors.CannotCreateTransactionOnInvalidDatabase();

			int id = Interlocked.Increment(ref s_transactionCounter);

			TransactionHandle handle;
			var err = FdbNative.DatabaseCreateTransaction(m_handle, out handle);
			if (Fdb.Failed(err))
			{
				handle.Dispose();
				throw Fdb.MapToException(err);
			}

			// ensure that if anything happens, either we return a valid Transaction, or we dispose it immediately
			FdbTransaction trans = null;
			try
			{
				trans = new FdbTransaction(this, context, id, handle, mode);
				RegisterTransaction(trans);
				// set default options..
				if (m_defaultTimeout != 0) trans.Timeout = m_defaultTimeout;
				if (m_defaultRetryLimit != 0) trans.RetryLimit = m_defaultRetryLimit;
				// flag as ready
				trans.State = FdbTransaction.STATE_READY;
				return trans;
			}
			catch (Exception)
			{
				if (trans != null)
				{
					trans.Dispose();
				}
				throw;
			}
		}

		internal void EnsureTransactionIsValid(FdbTransaction transaction)
		{
			if (m_disposed) ThrowIfDisposed();
			//TODO?
		}

		/// <summary>Add a new transaction to the list of tracked transactions</summary>
		internal void RegisterTransaction(FdbTransaction transaction)
		{
			Contract.Requires(transaction != null);

			if (!m_transactions.TryAdd(transaction.Id, transaction))
			{
				throw Fdb.Errors.FailedToRegisterTransactionOnDatabase(transaction, this);
			}
		}

		/// <summary>Remove a transaction from the list of tracked transactions</summary>
		/// <param name="transaction"></param>
		internal void UnregisterTransaction(FdbTransaction transaction)
		{
			Contract.Requires(transaction != null);

			//do nothing is already disposed
			if (m_disposed) return;

			// Unregister the transaction. We do not care if it has already been done
			FdbTransaction _;
			m_transactions.TryRemove(transaction.Id, out _);
			//TODO: compare removed value with the specified transaction to ensure it was the correct one?
		}

		#endregion

		#region Transactionals...

		#region IFdbReadOnlyTransactional methods...

		/// <summary>Runs a transactional lambda function against this database, inside a read-only transaction context, with retry logic.</summary>
		/// <param name="asyncHandler">Asynchronous lambda function that is passed a new read-only transaction on each retry.</param>
		/// <param name="cancellationToken">Optional cancellation token that will be passed to the transaction context, and that can also be used to abort the retry loop.</param>
		public Task ReadAsync(Func<IFdbReadOnlyTransaction, Task> asyncHandler, CancellationToken cancellationToken = default(CancellationToken))
		{
			return FdbOperationContext.RunReadAsync(this, asyncHandler, null, cancellationToken);
		}

		public Task ReadAsync(Func<IFdbReadOnlyTransaction, Task> asyncHandler, Action<IFdbReadOnlyTransaction> onDone, CancellationToken cancellationToken = default(CancellationToken))
		{
			return FdbOperationContext.RunReadAsync(this, asyncHandler, onDone, cancellationToken);
		}

		/// <summary>Runs a transactional lambda function against this database, inside a read-only transaction context, with retry logic.</summary>
		/// <param name="asyncHandler">Asynchronous lambda function that is passed a new read-only transaction on each retry. The result of the task will also be the result of the transactional.</param>
		/// <param name="cancellationToken">Optional cancellation token that will be passed to the transaction context, and that can also be used to abort the retry loop.</param>
		public Task<R> ReadAsync<R>(Func<IFdbReadOnlyTransaction, Task<R>> asyncHandler, CancellationToken cancellationToken = default(CancellationToken))
		{
			return FdbOperationContext.RunReadWithResultAsync<R>(this, asyncHandler, null, cancellationToken);
		}

		public Task<R> ReadAsync<R>(Func<IFdbReadOnlyTransaction, Task<R>> asyncHandler, Action<IFdbReadOnlyTransaction> onDone, CancellationToken cancellationToken = default(CancellationToken))
		{
			return FdbOperationContext.RunReadWithResultAsync<R>(this, asyncHandler, onDone, cancellationToken);
		}

		#endregion

		#region IFdbTransactional methods...

		/// <summary>Runs a transactional lambda function against this database, inside a write-only transaction context, with retry logic.</summary>
		/// <param name="handler">Lambda function that is passed a new read-write transaction on each retry. It should only call non-async methods, such as Set, Clear or any atomic operation.</param>
		/// <param name="cancellationToken">Optional cancellation token that will be passed to the transaction context, and that can also be used to abort the retry loop.</param>
		public Task WriteAsync(Action<IFdbTransaction> handler, CancellationToken cancellationToken = default(CancellationToken))
		{
			return FdbOperationContext.RunWriteAsync(this, handler, null, cancellationToken);
		}

		public Task WriteAsync(Action<IFdbTransaction> handler, Action<IFdbTransaction> onDone, CancellationToken cancellationToken = default(CancellationToken))
		{
			return FdbOperationContext.RunWriteAsync(this, handler, onDone, cancellationToken);
		}

		/// <summary>Runs a transactional lambda function against this database, inside a read-write transaction context, with retry logic.</summary>
		/// <param name="asyncHandler">Asynchronous lambda function that is passed a new read-write transaction on each retry.</param>
		/// <param name="cancellationToken">Optional cancellation token that will be passed to the transaction context, and that can also be used to abort the retry loop.</param>
		public Task ReadWriteAsync(Func<IFdbTransaction, Task> asyncHandler, CancellationToken cancellationToken = default(CancellationToken))
		{
			return FdbOperationContext.RunWriteAsync(this, asyncHandler, null, cancellationToken);
		}

		public Task ReadWriteAsync(Func<IFdbTransaction, Task> asyncHandler, Action<IFdbTransaction> onDone, CancellationToken cancellationToken = default(CancellationToken))
		{
			return FdbOperationContext.RunWriteAsync(this, asyncHandler, onDone, cancellationToken);
		}

		/// <summary>Runs a transactional lambda function against this database, inside a read-write transaction context, with retry logic.</summary>
		/// <param name="asyncHandler">Asynchronous lambda function that is passed a new read-write transaction on each retry. The result of the task will also be the result of the transactional.</param>
		/// <param name="cancellationToken">Optional cancellation token that will be passed to the transaction context, and that can also be used to abort the retry loop.</param>
		public Task<R> ReadWriteAsync<R>(Func<IFdbTransaction, Task<R>> asyncHandler, CancellationToken cancellationToken = default(CancellationToken))
		{
			return FdbOperationContext.RunWriteWithResultAsync<R>(this, asyncHandler, null, cancellationToken);
		}

		public Task<R> ReadWriteAsync<R>(Func<IFdbTransaction, Task<R>> asyncHandler, Action<IFdbTransaction> onDone, CancellationToken cancellationToken = default(CancellationToken))
		{
			return FdbOperationContext.RunWriteWithResultAsync<R>(this, asyncHandler, onDone, cancellationToken);
		}

		#endregion

		#endregion

		#region Database Options...

		/// <summary>Set a parameter-less option on this database</summary>
		/// <param name="option">Option to set</param>
		public void SetOption(FdbDatabaseOption option)
		{
			SetOption(option, default(string));
		}

		/// <summary>Set an option on this database that takes a string value</summary>
		/// <param name="option">Option to set</param>
		/// <param name="value">Value of the parameter (can be null)</param>
		public void SetOption(FdbDatabaseOption option, string value)
		{
			ThrowIfDisposed();

			Fdb.EnsureNotOnNetworkThread();
			//review: maybe we could allow running from the network thread?

			var data = FdbNative.ToNativeString(value, nullTerminated: true);
			unsafe
			{
				fixed (byte* ptr = data.Array)
				{
					Fdb.DieOnError(FdbNative.DatabaseSetOption(m_handle, option, ptr + data.Offset, data.Count));
				}
			}
		}

		/// <summary>Set an option on this database that takes an integer value</summary>
		/// <param name="option">Option to set</param>
		/// <param name="value">Value of the parameter</param>
		public void SetOption(FdbDatabaseOption option, long value)
		{
			ThrowIfDisposed();

			Fdb.EnsureNotOnNetworkThread();
			//review: maybe we could allow running from the network thread?

			unsafe
			{
				// Spec says: "If the option is documented as taking an Int parameter, value must point to a signed 64-bit integer (little-endian), and value_length must be 8."

				//TODO: what if we run on Big-Endian hardware ?
				Contract.Requires(BitConverter.IsLittleEndian, null, "Not supported on Big-Endian platforms");

				Fdb.DieOnError(FdbNative.DatabaseSetOption(m_handle, option, (byte*)(&value), 8));
			}
		}

		#endregion

#if REFACTORED

		#region Directory Layer...

		public FdbDatabasePartition Root { get { return m_rootDirectory; } }

		public void UseRootDirectory(FdbDirectoryLayer directoryLayer)
		{
			if (directoryLayer == null) throw new ArgumentNullException("directoryLayer");
			m_rootDirectory = new FdbDatabasePartition(this, directoryLayer);
		}

		public void UseRootDirectory(FdbSubspace nodes, FdbSubspace content)
		{
			if (nodes == null) throw new ArgumentNullException("nodes");
			if (content == null) throw new ArgumentNullException("content");

			// ensure that both subspaces are reachable
			if (!m_globalSpace.Contains(nodes.Key)) throw new ArgumentOutOfRangeException("nodes", "The Nodes subspace must be contained inside the Global Space of this database.");
			if (!m_globalSpace.Contains(content.Key)) throw new ArgumentOutOfRangeException("content", "The Content subspace must be contained inside the Global Space of this database.");

			var dl = new FdbDirectoryLayer(nodes, content);
			m_rootDirectory = new FdbDatabasePartition(this, dl);
		}

		public void UseDefaultRootDirectory()
		{
			var dl = new FdbDirectoryLayer(this.GlobalSpace[FdbKey.Directory], this.GlobalSpace);
			m_rootDirectory = new FdbDatabasePartition(this, dl);
		}

		#endregion

#endif

		#region Key Space Management...

		/// <summary>Change the current global namespace.</summary>
		/// <remarks>Do NOT call this, unless you know exactly what you are doing !</remarks>
		internal void ChangeGlobalSpace(FdbSubspace subspace)
		{
			subspace = subspace ?? FdbSubspace.Empty;
			m_globalSpace = subspace;
			m_globalSpaceCopy = subspace.Copy();
		}

		/// <summary>Returns the global namespace used by this database instance</summary>
		public FdbSubspace GlobalSpace
		{
			get
			{
				// return a copy of the subspace, to be sure that nobody can change the real globalspace and read elsewhere.
				return m_globalSpaceCopy;
			}
		}

		/// <summary>Test if a key is allowed to be used with this database instance</summary>
		/// <param name="key">Key to test</param>
		/// <returns>Returns true if the key is not null or empty, does not exceed the maximum key size, and is contained in the global key space of this database instance. Otherwise, returns false.</returns>
		public bool IsKeyValid(Slice key)
		{
			// key is legal if...
			return key.HasValue							// is not null (note: empty key is allowed)
				&& key.Count <= Fdb.MaxKeySize			// not too big
				&& m_globalSpace.Contains(key);			// not outside the namespace
		}

		/// <summary>Checks that a key is inside the global namespace of this database, and contained in the optional legal key space specified by the user</summary>
		/// <param name="key">Key to verify</param>
		/// <param name="endExclusive">If true, the key is allowed to be one past the maximum key allowed by the global namespace</param>
		/// <exception cref="FdbException">If the key is outside of the allowed keyspace, throws an FdbException with code FdbError.KeyOutsideLegalRange</exception>
		internal void EnsureKeyIsValid(Slice key, bool endExclusive = false)
		{
			var ex = ValidateKey(key, endExclusive);
			if (ex != null) throw ex;
		}

		/// <summary>Checks that a key is valid, and is inside the global key space of this database</summary>
		/// <param name="key">Key to verify</param>
		/// <param name="endExclusive">If true, the key is allowed to be one past the maximum key allowed by the global namespace</param>
		/// <returns>An exception if the key is outside of the allowed key space of this database</exception>
		internal Exception ValidateKey(Slice key, bool endExclusive = false)
		{
			// null or empty keys are not allowed
			if (key.IsNull)
			{
				return Fdb.Errors.KeyCannotBeNull();
			}

			// key cannot be larger than maximum allowed key size
			if (key.Count > Fdb.MaxKeySize)
			{
				return Fdb.Errors.KeyIsTooBig(key);
			}

			// special case for system keys
			if (IsSystemKey(key))
			{
				// note: it will fail later if the transaction does not have access to the system keys!
				return null;
			}

			// first, it MUST start with the root prefix of this database (if any)
			if (!m_globalSpace.Contains(key))
			{
				// special case: if endExclusive is true (we are validating the end key of a ClearRange),
				// and the key is EXACTLY equal to strinc(globalSpace.Prefix), we let is slide
				if (!endExclusive || !key.Equals(FdbKey.Increment(m_globalSpace.Key)))
				{
					return Fdb.Errors.InvalidKeyOutsideDatabaseNamespace(this, key);
				}
			}

			return null;
		}

		/// <summary>Returns true if the key is inside the system key space (starts with '\xFF')</summary>
		internal static bool IsSystemKey(Slice key)
		{
			return key.IsPresent && key.Array[key.Offset] == 0xFF;
		}

		/// <summary>Ensures that a serialized value is valid</summary>
		/// <remarks>Throws an exception if the value is null, or exceeds the maximum allowed size (Fdb.MaxValueSize)</exception>
		internal void EnsureValueIsValid(Slice value)
		{
			var ex = ValidateValue(value);
			if (ex != null) throw ex;
		}

		internal Exception ValidateValue(Slice value)
		{
			if (value.IsNull)
			{
				return Fdb.Errors.ValueCannotBeNull(value);
			}

			if (value.Count > Fdb.MaxValueSize)
			{
				return Fdb.Errors.ValueIsTooBig(value);
			}

			return null;
		}

		internal Slice BoundCheck(Slice value)
		{
			//REVIEW: should we always allow access to system keys ?
			return m_globalSpace.BoundCheck(value, allowSystemKeys: true);
		}

		#endregion

		#region Default Transaction Settings...

		/// <summary>Default Timeout value (in milliseconds) for all transactions created from this database instance.</summary>
		/// <remarks>Only effective for future transactions</remarks>
		public int DefaultTimeout
		{
			get { return m_defaultTimeout; }
			set
			{
				if (value < 0) throw new ArgumentOutOfRangeException("value", value, "Timeout value cannot be negative");
				m_defaultTimeout = value;
			}
		}

		/// <summary>Default Retry Limit value for all transactions created from this database instance.</summary>
		/// <remarks>Only effective for future transactions</remarks>
		public int DefaultRetryLimit
		{
			get { return m_defaultRetryLimit; }
			set
			{
				if (value < 0) throw new ArgumentOutOfRangeException("value", value, "RetryLimit value cannot be negative");
				m_defaultRetryLimit = value;
			}
		}

		#endregion

		#region IDisposable...

		private void ThrowIfDisposed()
		{
			if (m_disposed) throw new ObjectDisposedException(this.GetType().Name);
		}

		public void Dispose()
		{
			if (!m_disposed)
			{
				m_disposed = true;

				try
				{
					// mark this db has dead, but keep the handle alive until after all the callbacks have fired

					//TODO: kill all pending transactions on this db? 
					foreach (var trans in m_transactions.Values)
					{
						if (trans != null && trans.StillAlive)
						{
							trans.Cancel();
						}
					}
					m_transactions.Clear();

					//note: will block until all the registered callbacks have finished executing
					m_cts.SafeCancelAndDispose();
				}
				finally
				{
					m_handle.Dispose();
					if (m_ownsCluster) m_cluster.Dispose();
				}
			}
		}

		#endregion

	}

}
