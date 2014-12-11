#region BSD Licence
/* Copyright (c) 2013-2014, Doxense SAS
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
	using FoundationDB.Client.Utils;
	using System;
	using System.Collections.Generic;
	using System.Diagnostics;
	using System.Threading;
	using System.Threading.Tasks;

	/// <summary>Wraps an FDB_TRANSACTION handle</summary>
	public partial class FdbTransaction
	{

		/// <summary>Snapshot version of this transaction (lazily allocated)</summary>
		private Snapshotted m_snapshotted;

		/// <summary>Returns a version of this transaction that perform snapshotted operations</summary>
		public IFdbReadOnlyTransaction Snapshot
		{
			get
			{
				EnsureNotFailedOrDisposed();
				//TODO: we need to check if the transaction handler supports Snapshot isolation level
				return m_snapshotted ?? (m_snapshotted = new Snapshotted(this));
			}
		}

		/// <summary>Wrapper on a transaction, that will use Snmapshot mode on all read operations</summary>
		private sealed class Snapshotted : IFdbReadOnlyTransaction, IDisposable
		{
			private readonly FdbTransaction m_parent;

			public Snapshotted(FdbTransaction parent)
			{
				if (parent == null) throw new ArgumentNullException("parent");
				m_parent = parent;
			}

			public int Id
			{
				get { return m_parent.Id; }
			}

			public FdbOperationContext Context
			{
				get { return m_parent.Context; }
			}

			public CancellationToken Cancellation
			{
				get { return m_parent.Cancellation; }
			}

			public bool IsSnapshot
			{
				get { return true; }
			}

			public IFdbReadOnlyTransaction Snapshot
			{
				get { return this; }
			}

			public FdbIsolationLevel IsolationLevel
			{
				//TODO: not all transaction handlers may support Snapshot isolation level??
				get { return FdbIsolationLevel.Snapshot; }
			}

			public void EnsureCanRead()
			{
				m_parent.EnsureCanRead();
			}

			public Task<long> GetReadVersionAsync()
			{
				return m_parent.GetReadVersionAsync();
			}

			void IFdbReadOnlyTransaction.SetReadVersion(long version)
			{
				throw new NotSupportedException("You cannot set the read version on the Snapshot view of a transaction");
			}

			public Task<Slice> GetAsync(Slice key)
			{
				EnsureCanRead();

				m_parent.m_database.EnsureKeyIsValid(ref key);

#if DEBUG
				if (Logging.On && Logging.IsVerbose) Logging.Verbose(this, "GetAsync", String.Format("Getting value for '{0}'", key.ToString()));
#endif

				return m_parent.m_handler.GetAsync(key, snapshot: true, cancellationToken: m_parent.m_cancellation);
			}

			public Task<Slice[]> GetValuesAsync(Slice[] keys)
			{
				if (keys == null) throw new ArgumentNullException("keys");

				EnsureCanRead();

				m_parent.m_database.EnsureKeysAreValid(keys);

#if DEBUG
				if (Logging.On && Logging.IsVerbose) Logging.Verbose(this, "GetValuesAsync", String.Format("Getting batch of {0} values ...", keys.Length));
#endif

				return m_parent.m_handler.GetValuesAsync(keys, snapshot: true, cancellationToken: m_parent.m_cancellation);
			}

			public async Task<Slice> GetKeyAsync(FdbKeySelector selector)
			{
				EnsureCanRead();

				m_parent.m_database.EnsureKeyIsValid(selector.Key);

#if DEBUG
				if (Logging.On && Logging.IsVerbose) Logging.Verbose(this, "GetKeyAsync", String.Format("Getting key '{0}'", selector.ToString()));
#endif

				var key = await m_parent.m_handler.GetKeyAsync(selector, snapshot: true, cancellationToken: m_parent.m_cancellation).ConfigureAwait(false);

				// don't forget to truncate keys that would fall outside of the database's globalspace !
				return m_parent.m_database.BoundCheck(key);

			}

			public Task<Slice[]> GetKeysAsync(FdbKeySelector[] selectors)
			{
				EnsureCanRead();

				foreach (var selector in selectors)
				{
					m_parent.m_database.EnsureKeyIsValid(selector.Key);
				}

#if DEBUG
				if (Logging.On && Logging.IsVerbose) Logging.Verbose(this, "GetKeysCoreAsync", String.Format("Getting batch of {0} keys ...", selectors.Length));
#endif

				return m_parent.m_handler.GetKeysAsync(selectors, snapshot: true, cancellationToken: m_parent.m_cancellation);
			}

			public Task<FdbRangeChunk> GetRangeAsync(FdbKeySelector beginInclusive, FdbKeySelector endExclusive, FdbRangeOptions options, int iteration)
			{
				EnsureCanRead();

				m_parent.m_database.EnsureKeyIsValid(beginInclusive.Key);
				m_parent.m_database.EnsureKeyIsValid(endExclusive.Key);

				options = FdbRangeOptions.EnsureDefaults(options, null, null, FdbStreamingMode.Iterator, false);
				options.EnsureLegalValues();

				// The iteration value is only needed when in iterator mode, but then it should start from 1
				if (iteration == 0) iteration = 1;

				return m_parent.m_handler.GetRangeAsync(beginInclusive, endExclusive, options, iteration, snapshot: true, cancellationToken: m_parent.m_cancellation);
			}

			public FdbRangeQuery<KeyValuePair<Slice, Slice>> GetRange(FdbKeySelector beginInclusive, FdbKeySelector endExclusive, FdbRangeOptions options)
			{
				EnsureCanRead();

				return m_parent.GetRangeCore(beginInclusive, endExclusive, options, snapshot: true);
			}

			public Task<string[]> GetAddressesForKeyAsync(Slice key)
			{
				EnsureCanRead();
				return m_parent.m_handler.GetAddressesForKeyAsync(key, cancellationToken: m_parent.m_cancellation);
			}

			void IFdbReadOnlyTransaction.Cancel()
			{
				throw new NotSupportedException("You cannot cancel the Snapshot view of a transaction.");
			}

			void IFdbReadOnlyTransaction.Reset()
			{
				throw new NotSupportedException("You cannot reset the Snapshot view of a transaction.");
			}

			Task IFdbReadOnlyTransaction.OnErrorAsync(FdbError code)
			{
				throw new NotSupportedException("You cannot retry on a Snapshot view of a transaction.");
			}

			public void SetOption(FdbTransactionOption option)
			{
				m_parent.SetOption(option);
			}

			public void SetOption(FdbTransactionOption option, string value)
			{
				m_parent.SetOption(option, value);
			}

			public void SetOption(FdbTransactionOption option, long value)
			{
				m_parent.SetOption(option, value);
			}

			public int Timeout
			{
				get { return m_parent.Timeout; }
				set { throw new NotSupportedException("The timeout value cannot be changed via the Snapshot view of a transaction."); }
			}

			public int RetryLimit
			{
				get { return m_parent.RetryLimit; }
				set { throw new NotSupportedException("The retry limit value cannot be changed via the Snapshot view of a transaction."); }
			}

			public int MaxRetryDelay
			{
				get { return m_parent.MaxRetryDelay; }
				set { throw new NotSupportedException("The max retry delay value cannot be changed via the Snapshot view of a transaction."); }
			}

			void IDisposable.Dispose()
			{
				// NO-OP
			}
		}

	}

}
