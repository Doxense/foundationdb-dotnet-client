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

using Microsoft.Win32.SafeHandles;
using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using FoundationDB.Client.Native;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Diagnostics;
using FoundationDB.Layers.Tuples;
using FoundationDB.Client.Utils;

namespace FoundationDB.Client
{

	/// <summary>FoundationDB Database</summary>
	/// <remarks>Wraps an FDBDatabase* handle</remarks>
	public class FdbDatabase : IDisposable
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
		private readonly FdbSubspace m_namespace;
		/// <summary>Copy of the namespace, that is exposed to the outside.</summary>
		private readonly FdbSubspace m_namespaceCopy;

		/// <summary>Contains the bounds of the allowed key space. Any key that is outside of the bound should be rejected</summary>
		/// <remarks>This is modifiable, but should always be contained in the global namespace</remarks>
		private FdbKeyRange m_restrictedKeySpace;

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
			m_namespace = subspace != null ? new FdbSubspace(subspace) : FdbSubspace.Empty;
			m_namespaceCopy = new FdbSubspace(m_namespace);
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
		/// <remarks>The token will be cancelled if the database instance is disposed</remarks>
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

		/// <summary>[EXPERIMENTAL] Retry an action in case of merge or temporary database failure</summary>
		/// <typeparam name="TResult">Type of the result returned by the action</typeparam>
		/// <param name="asyncAction">Async action to perform under a new transaction, that receives the transaction as the first parameter, the number of retries (starts at 0) as the second parameter, and a cancellation token as the third parameter. It should throw an OperationCancelledException if it decides to not retry the action</param>
		/// <param name="ct">Optionnal cancellation token, that will be passed to the async action as the third parameter</param>
		/// <returns>Task that completes when we have successfully completed the action, or fails if a non retryable error occurs</returns>
		public async Task<TResult> Attempt<TResult>(Func<FdbTransaction, int, CancellationToken, Task<TResult>> asyncAction, CancellationToken ct = default(CancellationToken))
		{
			// this is the equivalent of the "transactionnal" decorator in Python, and maybe also the "Run" method in Java

			//TODO: add 'maxAttempts' or 'maxDuration' optional parameters ?

			var start = DateTime.UtcNow;

			int retries = 0;
			bool committed = false;
			TResult res = default(TResult);

			while (!committed && !ct.IsCancellationRequested)
			{
				using (var trans = BeginTransaction())
				{
					FdbException e = null;
					try
					{
						// call the user provided lambda
						res = await asyncAction(trans, retries, ct).ConfigureAwait(false);

						// commit the transaction
						await trans.CommitAsync(ct).ConfigureAwait(false);

						// we are done
						committed = true;
					}
					catch (FdbException x)
					{
						x = e;
					}

					if (e != null)
					{
						await trans.OnErrorAsync(e.Code).ConfigureAwait(false);
					}

					var now = DateTime.UtcNow;
					var elapsed = now - start;
					if (elapsed.TotalSeconds >= 1)
					{
						Debug.WriteLine("fdb WARNING: long transaction ({0} elapsed in transaction lambda function ({1} retries, {2})", elapsed, retries, committed ? "committed" : "not yet committed");
					}

					++retries;
				}
			}
			ct.ThrowIfCancellationRequested();

			return res;
		}

		public Task Attempt(Func<FdbTransaction, int, Task> asyncAction, CancellationToken ct = default(CancellationToken))
		{
			return Attempt<object>(async (tr, retry, _) =>
			{
				await asyncAction(tr, retry).ConfigureAwait(false);
				return null;
			}, ct);
		}

		public Task Attempt(Func<FdbTransaction, Task> asyncAction, CancellationToken ct = default(CancellationToken))
		{
			return Attempt<object>(async (tr, _, __) =>
			{
				await asyncAction(tr).ConfigureAwait(false);
				return null;
			}, ct);
		}

		public Task Attempt(Action<FdbTransaction, int> action, CancellationToken ct = default(CancellationToken))
		{
			return Attempt<bool>((tr, retry, _) =>
			{
				action(tr, retry);
				return Task.FromResult(true);
			}, ct);
		}

		public Task Attempt(Action<FdbTransaction> action, CancellationToken ct = default(CancellationToken))
		{
			return Attempt<bool>((tr, _, __) =>
			{
				action(tr);
				return Task.FromResult(true);
			}, ct);
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
		public void SetLocationCacheSize(long size)
		{
			//REVIEW: we can't really change this to a Property, because we don't have a way to get the current value for the getter, and set only properties are weird...
			SetOption(FdbDatabaseOption.LocationCacheSize, size);
		}

		#endregion

		#region Key Space Management...

		/// <summary>Return the global namespace used by this database instance</summary>
		/// <remarks>Makes a copy of the subspace tuple, so you should not call this property a lot. Use any of the Partition(..) methods to create a subspace of the database</remarks>
		public FdbSubspace Namespace
		{
			get
			{
				// return a copy of the subspace
				return m_namespaceCopy;
			}
		}

		/// <summary>Return a new partition of the current database</summary>
		/// <typeparam name="T">Type of the value used for the partition</typeparam>
		/// <param name="value">Prefix of the new partition</param>
		/// <returns>Subspace that is the concatenation of the database global namespace and the specified <paramref name="value"/></returns>
		public FdbSubspace Partition<T>(T value)
		{
			return m_namespace.Partition<T>(value);
		}

		/// <summary>Return a new partition of the current database</summary>
		/// <returns>Subspace that is the concatenation of the database global namespace and the specified values</returns>
		public FdbSubspace Partition<T1, T2>(T1 value1, T2 value2)
		{
			return m_namespace.Partition<T1, T2>(value1, value2);
		}

		/// <summary>Return a new partition of the current database</summary>
		/// <returns>Subspace that is the concatenation of the database global namespace and the specified values</returns>
		public FdbSubspace Partition<T1, T2, T3>(T1 value1, T2 value2, T3 value3)
		{
			return m_namespace.Partition<T1, T2, T3>(value1, value2, value3);
		}

		/// <summary>Return a new partition of the current database</summary>
		/// <returns>Subspace that is the concatenation of the database global namespace and the specified <paramref name="tuple"/></returns>
		public FdbSubspace Partition(IFdbTuple tuple)
		{
			return m_namespace.Partition(tuple);
		}

		public Slice Pack<T>(T key)
		{
			return m_namespace.Pack<T>(key);
		}

		public Slice Pack<T1, T2>(T1 key1, T2 key2)
		{
			return m_namespace.Pack<T1, T2>(key1, key2);
		}

		/// <summary>Unpack a key using the current namespace of the database</summary>
		/// <param name="key">Key that should fit inside the current namespace of the database</param>
		/// <returns></returns>
		public IFdbTuple Unpack(Slice key)
		{
			return m_namespace.Unpack(key);
		}

		/// <summary>Unpack a key using the current namespace of the database</summary>
		/// <param name="key">Key that should fit inside the current namespace of the database</param>
		/// <returns></returns>
		public T UnpackLast<T>(Slice key)
		{
			return m_namespace.UnpackLast<T>(key);
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
			return m_namespace.Concat(keyRelative);
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
			return m_namespace.Extract(keyAbsolute);
		}

		/// <summary>Restrict access to only the keys contained inside the specified range</summary>
		/// <param name="range"></param>
		/// <remarks>This is "opt-in" security, and should not be relied on to ensure safety of the database. It should only be seen as a safety net to defend yourself from logical bugs in your code while dealing with multi-tenancy issues</remarks>
		public void RestrictKeySpace(FdbKeyRange range)
		{
			var begin = range.Begin;
			var end = range.End;

			// Ensure that end is not less then begin
			if (begin.HasValue && end.HasValue && begin > end)
			{
				throw Fdb.Errors.EndKeyOfRangeCannotBeLessThanBeginKey(range);
			}

			// clip the bounds of the range with the global namespace
			var globalRange = m_namespace.ToRange();
			if (begin < globalRange.Begin) begin = globalRange.Begin;
			if (end > globalRange.End) end = globalRange.End;

			// copy the bounds so that nobody can change them behind our back
			m_restrictedKeySpace = new FdbKeyRange(begin.Memoize(), end.Memoize());
		}

		/// <summary>Restrict access to only the keys contained inside the specified bounds.</summary>
		/// <param name="beginInclusive">If non-null, only allow keys that are bigger than or equal to this key</param>
		/// <param name="endInclusive">If non-null, only allow keys that are less than or equal to this key</param>
		/// <remarks>
		/// The keys should fit inside the global namespace of the current db. If they don't, they will be clipped to fit inside the range.
		/// IMPORTANT: This is "opt-in" security, and should not be relied on to ensure safety of the database. It should only be seen as a safety net to defend yourself from logical bugs in your code while dealing with multi-tenancy issues
		/// </remarks>
		public void RestrictKeySpace(Slice beginInclusive, Slice endInclusive)
		{
			RestrictKeySpace(new FdbKeyRange(beginInclusive, endInclusive));
		}

		public void RestrictKeySpace(Slice prefix)
		{
			RestrictKeySpace(FdbKeyRange.FromPrefix(prefix));
		}

		public void RestrictKeySpace(IFdbTuple prefix)
		{
			RestrictKeySpace(prefix != null ? prefix.ToRange(includePrefix: false) : FdbKeyRange.None);
		}

		/// <summary>Returns the current key space</summary>
		/// <remarks>Makes a copy of the keys, so you should not call this property a lot</remarks>
		public FdbKeyRange KeySpace
		{
			get
			{
				// return a copy, in order not to expose the internal slice buffers
				return new FdbKeyRange(m_restrictedKeySpace.Begin.Memoize(), m_restrictedKeySpace.End.Memoize());
			}
		}

		/// <summary>Test if a key is allowed to be used with this database instance</summary>
		/// <param name="key">Key to test</param>
		/// <returns>Returns true if the key is not null or empty, does not exceed the maximum key size, is contained in the root namespace of this database instance, and is inside the bounds of the optionnal restricted key space. Otherwise, returns false.</returns>
		public bool IsKeyValid(Slice key)
		{
			// key is legal if...
			return !key.IsNullOrEmpty					// has some data in it
				&& key.Count <= Fdb.MaxKeySize			// not too big
				&& m_namespace.Contains(key)			// not outside the namespace
				&& m_restrictedKeySpace.Test(key, endIncluded: true) == 0; // not outside the restricted key space
		}

		/// <summary>Checks that a key is inside the global namespace of this database, and contained in the optional legal key space specified by the user</summary>
		/// <param name="key">Key to verify</param>
		/// <exception cref="FdbException">If the key is outside of the allowed keyspace, throws an FdbException with code FdbError.KeyOutsideLegalRange</exception>
		internal void EnsureKeyIsValid(Slice key)
		{
			var ex = ValidateKey(key);
			if (ex != null) throw ex;
		}

		/// <summary>Checks that a key is inside the global namespace of this database, and contained in the optional legal key space specified by the user</summary>
		/// <param name="key">Key to verify</param>
		/// <returns>An exception if the key is outside of the allowed keyspace of this database</exception>
		internal Exception ValidateKey(Slice key)
		{
			// null or empty keys are not allowed
			if (key.IsNullOrEmpty)
			{
				return Fdb.Errors.KeyCannotBeNullOrEmpty(key);
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
			if (!m_namespace.Contains(key))
			{
				return Fdb.Errors.InvalidKeyOutsideDatabaseNamespace(this, key);
			}

			// test if the key is inside the restrictied key space
			int x = m_restrictedKeySpace.Test(key, endIncluded: true); // returns -1/+1 if outside, 0 if inside
			if (x != 0)
			{
				return Fdb.Errors.InvalidKeyOutsideDatabaseRestrictedKeySpace(this, key, greaterThan: x > 0);
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
			using (var tr = BeginTransaction())
			{
				tr.WithAccessToSystemKeys();

				var result = await tr.GetAsync(Slice.FromAscii("\xFF/coordinators")).ConfigureAwait(false);
				return result.ToAscii();
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
							trans.Rollback();
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
