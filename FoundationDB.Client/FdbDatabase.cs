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

		private readonly FdbCluster m_cluster;
		private readonly DatabaseHandle m_handle;
		private readonly string m_name;
		private readonly bool m_ownsCluster;
		private readonly CancellationTokenSource m_cts = new CancellationTokenSource();
		private bool m_disposed;

		private static int s_transactionCounter;
		private readonly ConcurrentDictionary<int, FdbTransaction> m_transactions = new ConcurrentDictionary<int, FdbTransaction>();

		/// <summary>Contains the bounds of the allowed key space</summary>
		/// <remarks>Any key that is outside of the bound should be rejected</remarks>
		private FdbKeyRange m_allowedKeySpace;

		#endregion

		#region Constructors...

		internal FdbDatabase(FdbCluster cluster, DatabaseHandle handle, string name, bool ownsCluster)
		{
			m_cluster = cluster;
			m_handle = handle;
			m_name = name;
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

		/// <summary>Source of cancellation that is linked with the lifetime of this database</summary>
		/// <remarks>Will be cancelled when Dispose() is called</remarks>
		internal CancellationTokenSource CancellationSource { get { return m_cts; } }

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
			if (m_handle.IsInvalid) throw new InvalidOperationException("Cannot create a transaction on an invalid database");
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
						res = await asyncAction(trans, retries, ct);

						// commit the transaction
						await trans.CommitAsync(ct);

						// we are done
						committed = true;
					}
					catch (FdbException x)
					{
						x = e;
					}

					if (e != null)
					{
						await trans.OnErrorAsync(e.Code);
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
				await asyncAction(tr, retry);
				return null;
			}, ct);
		}

		public Task Attempt(Func<FdbTransaction, Task> asyncAction, CancellationToken ct = default(CancellationToken))
		{
			return Attempt<object>(async (tr, _, __) =>
			{
				await asyncAction(tr);
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
				throw new InvalidOperationException(String.Format("Failed to register transaction #{0} with this instance of database {1}", transaction.Id, this.Name));
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

		#region Key Management...

		/// <summary>Restrict access to only the keys contained inside the specified bounds</summary>
		/// <param name="beginInclusive">If non-null, only allow keys that are bigger than or equal to this key</param>
		/// <param name="endInclusive">If non-null, only allow keys that are less than or equal to this key</param>
		/// <remarks>This is "opt-in" security, and should not be relied on to ensure safety of the database. It should only be seen as a safety net to defend yourself from logical bugs in your code while dealing with multi-tenancy issues</remarks>
		public void RestrictKeySpace(Slice beginInclusive, Slice endInclusive)
		{
			// Ensure that end is not less then begin
			if (beginInclusive.HasValue && endInclusive.HasValue && beginInclusive.CompareTo(endInclusive) > 0)
			{
				throw new ArgumentException("The end key of the allowed key space cannot be less than the end key", "endInclusive");
			}

			m_allowedKeySpace = new FdbKeyRange(beginInclusive, endInclusive);
		}

		public void RestrictKeySpace(FdbKeyRange range)
		{
			// Ensure that end is not less then begin
			if (range.Begin.HasValue && range.End.HasValue && range.Begin.CompareTo(range.End) > 0)
			{
				throw new ArgumentException("The end key of the allowed key space cannot be less than the end key", "range");
			}

			m_allowedKeySpace = range;
		}

		public void RestrictKeySpace(Slice prefix)
		{
			m_allowedKeySpace = FdbKeyRange.FromPrefix(prefix);
		}

		public void RestrictKeySpace(IFdbTuple prefix)
		{
			m_allowedKeySpace = prefix != null ? prefix.ToRange(includePrefix: false) : FdbKeyRange.None;
		}

		/// <summary>Return the legal key space (if specified). Any key used for reading or writing outside of these bounds will be rejected at the binding layer</summary>
		public FdbKeyRange KeySpace { get { return m_allowedKeySpace; } }

		/// <summary>Checks that a key is inside the legal key space allowed on this database</summary>
		/// <param name="key">Key to verify</param>
		/// <exception cref="FdbException">If the key is outside of the allowed keyspace, throws an FdbException with code FdbError.KeyOutsideLegalRange</exception>
		internal void EnsureKeyIsValid(Slice key)
		{
			Fdb.EnsureKeyIsValid(key);

			var begin = this.KeySpace.Begin;
			var end = this.KeySpace.End;

			if (begin.HasValue && begin.CompareTo(key) > 0)	
			{
				throw new FdbException(FdbError.KeyOutsideLegalRange, String.Format("Key is outside the allowed key space for this database ({0} < {1})", key.ToString(), begin.ToString()));
			}
			if (end.HasValue && m_allowedKeySpace.End.CompareTo(key) < 0)
			{
				throw new FdbException(FdbError.KeyOutsideLegalRange, String.Format("Key is outside the allowed key space for this database ({0} > {1})", key.ToString(), end.ToString()));
			}
		}

		#endregion

		#region System Keys...

		/// <summary>Returns a string describing the list of the coordinators for the cluster</summary>
		public async Task<string> GetCoordinatorsAsync()
		{
			using (var tr = BeginTransaction())
			{
				tr.WithAccessToSystemKeys();

				var result = await tr.GetAsync(Slice.FromAscii("\xFF/coordinators"));
				return result.ToAscii();
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

				try
				{
					//note: will block until all the registered callbacks have finished executing
					m_cts.Cancel();
				}
				catch (ObjectDisposedException) { }
				catch (AggregateException e)
				{
					//TODO: what should we do with the exception ?
					Debug.WriteLine("Error while cancelling all pending operations: " + e.ToString());
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
