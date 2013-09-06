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
	using FoundationDB.Layers.Tuples;
	using System;
	using System.Collections.Concurrent;
	using System.Diagnostics;
	using System.Threading;
	using System.Threading.Tasks;

	/// <summary>FoundationDB Database</summary>
	/// <remarks>Wraps an FDBDatabase* handle</remarks>
	[DebuggerDisplay("Name={m_name}, Namespace={m_namespace}")]
	public partial class FdbDatabase : IDisposable
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
	
		/// <summary>Global cancellation source that is canceled when the current db instance gets disposed.</summary>
		private readonly CancellationTokenSource m_cts = new CancellationTokenSource();

		/// <summary>Set to true when the current db instance gets disposed.</summary>
		private volatile bool m_disposed;

		/// <summary>Global counters used to generate the transaction's local id (for debugging purpose)</summary>
		private static int s_transactionCounter;

		/// <summary>List of all "pending" transactions created from this database instance (and that have not yet been disposed)</summary>
		private readonly ConcurrentDictionary<int, FdbTransaction> m_transactions = new ConcurrentDictionary<int, FdbTransaction>();

		/// <summary>Global namespace used to prefix ALL keys and subspaces accessible by this database instance (default is empty)</summary>
		/// <remarks>This is readonly and is set when creating the database instance</remarks>
		private readonly FdbSubspace m_globalSpace;
		/// <summary>Copy of the namespace, that is exposed to the outside.</summary>
		private readonly FdbSubspace m_globalSpaceCopy;

		/// <summary>Default Timeout value for all transactions</summary>
		private int m_defaultTimeout;

		/// <summary>Default RetryLimit value for all transactions</summary>
		private int m_defaultRetryLimit;

		#endregion

		#region Constructors...

		/// <summary>Create a new database instance</summary>
		/// <param name="cluster">Parent cluster</param>
		/// <param name="handle">Handle to the native FDB_DATABASE*</param>
		/// <param name="name">Name of the database</param>
		/// <param name="subspace">Root namespace of all keys accessible by this database instance</param>
		/// <param name="ownsCluster">If true, the cluster instance lifetime is linked with the database instance</param>
		internal FdbDatabase(FdbCluster cluster, DatabaseHandle handle, string name, FdbSubspace subspace, bool ownsCluster)
		{
			m_cluster = cluster;
			m_handle = handle;
			m_name = name;
			m_globalSpace = subspace ?? FdbSubspace.Empty;
			m_globalSpaceCopy = subspace.Copy();
			m_ownsCluster = ownsCluster;
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
		/// <remarks>The token will be canceled if the database instance is disposed</remarks>
		public CancellationToken Token { get { return m_cts.Token; } }

		#endregion

		#region Transaction Management...

		/// <summary>Start a new transaction on this database</summary>
		/// <returns>New transaction</returns>
		/// <remarks>You MUST call Dispose() on the transaction when you are done with it. You SHOULD wrap it in a 'using' statement to ensure that it is disposed in all cases.</remarks>
		/// <example>
		/// using(var tr = db.BeginTransaction())
		/// {
		///		tr.Set(Slice.FromString("Hello"), Slice.FromString("World"));
		///		await tr.CommitAsync();
		/// }</example>
		public FdbTransaction BeginTransaction()
		{
			if (m_handle.IsInvalid) throw Fdb.Errors.CannotCreateTransactionOnInvalidDatabase();
			ThrowIfDisposed();

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
				trans = new FdbTransaction(this, id, handle);
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
			ThrowIfDisposed();
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

		#region Attempt...

		private ReadWriteTransactional m_transactional;

		/// <summary>Retryable operations</summary>
		public ReadWriteTransactional Attempt
		{
			get { return m_transactional ?? (m_transactional = new ReadWriteTransactional(this)); }
		}

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

			unsafe
			{
				// Spec says: "If the option is documented as taking an Int parameter, value must point to a signed 64-bit integer (little-endian), and value_length must be 8."

				//TODO: what if we run on Big-Endian hardware ?
				Contract.Requires(BitConverter.IsLittleEndian, null, "Not supported on Big-Endian platforms");

				Fdb.DieOnError(FdbNative.DatabaseSetOption(m_handle, option, (byte*)(&value), 8));
			}
		}

		/// <summary>Set the size of the client location cache. Raising this value can boost performance in very large databases where clients access data in a near-random pattern. Defaults to 100000.</summary>
		/// <param name="size">Max location cache entries</param>
		public void SetLocationCacheSize(int size)
		{
			//REVIEW: we can't really change this to a Property, because we don't have a way to get the current value for the getter, and set only properties are weird...
			//TODO: cache this into a local variable ?
			SetOption(FdbDatabaseOption.LocationCacheSize, size);
		}

		/// <summary>Set the maximum number of watches allowed to be outstanding on a database connection. Increasing this number could result in increased resource usage. Reducing this number will not cancel any outstanding watches. Defaults to 10000 and cannot be larger than 1000000.</summary>
		/// <param name="count">Max outstanding watches</param>
		public void SetMaxWatches(int count)
		{
			//REVIEW: we can't really change this to a Property, because we don't have a way to get the current value for the getter, and set only properties are weird...
			//TODO: cache this into a local variable ?
			SetOption(FdbDatabaseOption.MaxWatches, count);
		}

		/// <summary>Specify the machine ID that was passed to fdbserver processes running on the same machine as this client, for better location-aware load balancing.</summary>
		/// <param name="hexId">Hexadecimal ID</param>
		public void SetMachineId(string hexId)
		{
			//REVIEW: we can't really change this to a Property, because we don't have a way to get the current value for the getter, and set only properties are weird...
			//TODO: cache this into a local variable ?
			SetOption(FdbDatabaseOption.MachineId, hexId);
		}

		/// <summary>Specify the datacenter ID that was passed to fdbserver processes running in the same datacenter as this client, for better location-aware load balancing.</summary>
		/// <param name="hexId">Hexadecimal ID</param>
		public void SetDataCenterId(string hexId)
		{
			//REVIEW: we can't really change this to a Property, because we don't have a way to get the current value for the getter, and set only properties are weird...
			//TODO: cache this into a local variable ?
			SetOption(FdbDatabaseOption.DataCenterId, hexId);
		}

		#endregion

		#region Key Space Management...

		/// <summary>Return the global namespace used by this database instance</summary>
		/// <remarks>Makes a copy of the subspace tuple, so you should not call this property a lot. Use any of the Partition(..) methods to create a subspace of the database</remarks>
		public FdbSubspace GlobalSpace
		{
			get
			{
				// return a copy of the subspace
				return m_globalSpaceCopy;
			}
		}

		/// <summary>Return a new partition of the current database</summary>
		/// <typeparam name="T">Type of the value used for the partition</typeparam>
		/// <param name="value">Prefix of the new partition</param>
		/// <returns>Subspace that is the concatenation of the database global namespace and the specified <paramref name="value"/></returns>
		public FdbSubspace Partition<T>(T value)
		{
			return m_globalSpace.Partition<T>(value);
		}

		/// <summary>Return a new partition of the current database</summary>
		/// <returns>Subspace that is the concatenation of the database global namespace and the specified values</returns>
		public FdbSubspace Partition<T1, T2>(T1 value1, T2 value2)
		{
			return m_globalSpace.Partition<T1, T2>(value1, value2);
		}

		/// <summary>Return a new partition of the current database</summary>
		/// <returns>Subspace that is the concatenation of the database global namespace and the specified values</returns>
		public FdbSubspace Partition<T1, T2, T3>(T1 value1, T2 value2, T3 value3)
		{
			return m_globalSpace.Partition<T1, T2, T3>(value1, value2, value3);
		}

		/// <summary>Return a new partition of the current database</summary>
		/// <returns>Subspace that is the concatenation of the database global namespace and the specified <paramref name="tuple"/></returns>
		public FdbSubspace Partition(IFdbTuple tuple)
		{
			return m_globalSpace.Partition(tuple);
		}

		public Slice Pack<T>(T key)
		{
			return m_globalSpace.Pack<T>(key);
		}

		public Slice Pack<T1, T2>(T1 key1, T2 key2)
		{
			return m_globalSpace.Pack<T1, T2>(key1, key2);
		}

		/// <summary>Unpack a key using the current namespace of the database</summary>
		/// <param name="key">Key that should fit inside the current namespace of the database</param>
		/// <returns></returns>
		public IFdbTuple Unpack(Slice key)
		{
			return m_globalSpace.Unpack(key);
		}

		/// <summary>Unpack a key using the current namespace of the database</summary>
		/// <param name="key">Key that should fit inside the current namespace of the database</param>
		/// <returns></returns>
		public T UnpackLast<T>(Slice key)
		{
			return m_globalSpace.UnpackLast<T>(key);
		}

		/// <summary>Add the global namespace prefix to a relative key</summary>
		/// <param name="keyRelative">Key that is relative to the global namespace</param>
		/// <returns>Key that starts with the global namespace prefix</returns>
		/// <example>
		/// // db with namespace prefix equal to"&lt;02&gt;Foo&lt;00&gt;"
		/// db.Concat('&lt;02&gt;Bar&lt;00&gt;') => '&lt;02&gt;Foo&lt;00&gt;&gt;&lt;02&gt;Bar&lt;00&gt;'
		/// db.Concat(Slice.Empty) => '&lt;02&gt;Foo&lt;00&gt;'
		/// db.Concat(Slice.Nil) => Slice.Nil
		/// </example>
		public Slice Concat(Slice keyRelative)
		{
			return m_globalSpace.Concat(keyRelative);
		}

		/// <summary>Remove the global namespace prefix of this database form the key, and return the rest of the bytes, or Slice.Nil is the key is outside the namespace</summary>
		/// <param name="keyAbsolute">Binary key that starts with the namespace prefix, followed by some bytes</param>
		/// <returns>Binary key that contain only the bytes after the namespace prefix</returns>
		/// <example>
		/// // db with namespace prefix equal to"&lt;02&gt;Foo&lt;00&gt;"
		/// db.Extract('&lt;02&gt;Foo&lt;00&gt;&lt;02&gt;Bar&lt;00&gt;') => '&gt;&lt;02&gt;Bar&lt;00&gt;'
		/// db.Extract('&lt;02&gt;Foo&lt;00&gt;') => Slice.Empty
		/// db.Extract('&lt;02&gt;TopSecret&lt;00&gt;&lt;02&gt;Password&lt;00&gt;') => Slice.Nil
		/// db.Extract(Slice.Nil) => Slice.Nil
		/// </example>
		public Slice Extract(Slice keyAbsolute)
		{
			return m_globalSpace.Extract(keyAbsolute);
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
		/// <exception cref="FdbException">If the key is outside of the allowed keyspace, throws an FdbException with code FdbError.KeyOutsideLegalRange</exception>
		internal void EnsureKeyIsValid(Slice key)
		{
			var ex = ValidateKey(key);
			if (ex != null) throw ex;
		}

		/// <summary>Checks that a key is valid, and is inside the global key space of this database</summary>
		/// <param name="key">Key to verify</param>
		/// <returns>An exception if the key is outside of the allowed key space of this database</exception>
		internal Exception ValidateKey(Slice key)
		{
			// null or empty keys are not allowed
			if (!key.HasValue)
			{
				return Fdb.Errors.KeyCannotBeNull(key);
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
				return Fdb.Errors.InvalidKeyOutsideDatabaseNamespace(this, key);
			}

			return null;
		}

		/// <summary>Returns true if the key is inside the system key space (starts with '\xFF')</summary>
		internal static bool IsSystemKey(Slice key)
		{
			return key.Count > 0 && key.Array[key.Offset] == 0xFF;
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
			if (!value.HasValue)
			{
				return Fdb.Errors.ValueCannotBeNull(value);
			}

			if (value.Count > Fdb.MaxValueSize)
			{
				return Fdb.Errors.ValueIsTooBig(value);
			}

			return null;
		}

		#endregion

		#region System Keys...

		/// <summary>Returns a string describing the list of the coordinators for the cluster</summary>
		public async Task<string> GetCoordinatorsAsync()
		{
			using (var tr = BeginTransaction().WithAccessToSystemKeys())
			{
				var result = await tr.GetAsync(Fdb.SystemKeys.Coordinators).ConfigureAwait(false);
				return result.ToAscii();
			}
		}

		/// <summary>Return the value of a configuration parameter (located under '\xFF/conf/')</summary>
		/// <param name="name">"storage_engine"</param>
		/// <returns>Value of '\xFF/conf/storage_engine'</returns>
		public async Task<Slice> GetConfigParameter(string name)
		{
			if (string.IsNullOrEmpty(name)) throw new ArgumentException("Configuration parameter name cannot be null or empty");

			using(var tr = BeginTransaction().WithAccessToSystemKeys())
			{
				return await tr.GetAsync(Fdb.SystemKeys.GetConfigKey(name)).ConfigureAwait(false);
			}
		}

		#endregion

		#region Transactional Methods..

		/// <summary>Clear the entire content of a subspace</summary>
		public async Task ClearRangeAsync(FdbSubspace subspace, CancellationToken ct = default(CancellationToken))
		{
			Contract.Requires(subspace != null);

			ct.ThrowIfCancellationRequested();

			using(var trans = this.BeginTransaction())
			{
				trans.ClearRange(subspace);
				await trans.CommitAsync(ct).ConfigureAwait(false);
			}
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
			if (m_disposed) throw new ObjectDisposedException(null);
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
