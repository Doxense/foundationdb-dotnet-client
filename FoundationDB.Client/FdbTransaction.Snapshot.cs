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

			public CancellationToken Token
			{
				get { return m_parent.Token; }
			}

			public bool IsSnapshot
			{
				get { return true; }
			}

			public IFdbReadOnlyTransaction Snapshot
			{
				get { return this; }
			}

			public void EnsureCanRead()
			{
				m_parent.EnsureCanRead();
			}

			public Task<long> GetReadVersionAsync()
			{
				return m_parent.GetReadVersionAsync();
			}

			public Task<Slice> GetAsync(Slice keyBytes)
			{
				EnsureCanRead();

				return m_parent.GetCoreAsync(keyBytes, snapshot: true);
			}

			public Task<Slice[]> GetValuesAsync(Slice[] keys)
			{
				if (keys == null) throw new ArgumentNullException("keys");

				EnsureCanRead();

				return m_parent.GetValuesCoreAsync(keys, snapshot: true);
			}

			public Task<Slice> GetKeyAsync(FdbKeySelector selector)
			{
				EnsureCanRead();

				return m_parent.GetKeyCoreAsync(selector, snapshot: true);
			}

			public Task<Slice[]> GetKeysAsync(FdbKeySelector[] selectors)
			{
				EnsureCanRead();

				return m_parent.GetKeysCoreAsync(selectors, snapshot:true);
			}

			public Task<FdbRangeChunk> GetRangeAsync(FdbKeySelectorPair range, FdbRangeOptions options, int iteration)
			{
				EnsureCanRead();

				return m_parent.GetRangeCoreAsync(range, options, iteration, snapshot: true);
			}

			public FdbRangeQuery<KeyValuePair<Slice, Slice>> GetRange(FdbKeySelectorPair range, FdbRangeOptions options)
			{
				EnsureCanRead();

				return m_parent.GetRangeCore(range, options, snapshot: true);
			}

			public Task<string[]> GetAddressesForKeyAsync(Slice key)
			{
				EnsureCanRead();
				return m_parent.GetAddressesForKeyCoreAsync(key);
			}

			public void Cancel()
			{
				m_parent.Cancel();
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

			void IDisposable.Dispose()
			{
				// NO-OP
			}
		}

	}

}
