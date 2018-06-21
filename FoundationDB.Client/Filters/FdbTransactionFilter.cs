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

namespace FoundationDB.Filters
{
	using FoundationDB.Client;
	using System;
	using System.Collections.Generic;
	using System.Threading;
	using System.Threading.Tasks;
	using Doxense.Diagnostics.Contracts;
	using JetBrains.Annotations;

	/// <summary>Base class for simple transaction filters</summary>
	public abstract class FdbTransactionFilter : IFdbTransaction
	{
		/// <summary>Inner database</summary>
		protected readonly IFdbTransaction m_transaction;
		/// <summary>If true, forces the underlying transaction to be read only</summary>
		protected readonly bool m_readOnly;
		/// <summary>If true, dispose the inner database when we get disposed</summary>
		protected readonly bool m_owner;
		/// <summary>If true, we have been disposed</summary>
		protected bool m_disposed;

		/// <summary>Base constructor for transaction filters</summary>
		/// <param name="trans">Underlying transaction that will be exposed as read-only</param>
		/// <param name="forceReadOnly">If true, force the transaction to be read-only. If false, use the read-only mode of the underlying transaction</param>
		/// <param name="ownsTransaction">If true, the underlying transaction will also be disposed when this instance is disposed</param>
		protected FdbTransactionFilter([NotNull] IFdbTransaction trans, bool forceReadOnly, bool ownsTransaction)
		{
			Contract.NotNull(trans, nameof(trans));

			m_transaction = trans;
			m_readOnly = forceReadOnly || trans.IsReadOnly;
			m_owner = ownsTransaction;
		}

		protected void ThrowIfDisposed()
		{
			// this should be inlined by the caller
			if (m_disposed) ThrowFilterAlreadyDisposed(this);
		}

		[ContractAnnotation("=> halt")]
		private static void ThrowFilterAlreadyDisposed([NotNull] FdbTransactionFilter filter)
		{
			throw new ObjectDisposedException(filter.GetType().Name);
		}

		public void Dispose()
		{
			Dispose(true);
			GC.SuppressFinalize(this);
		}

		protected virtual void Dispose(bool disposing)
		{
			if (!m_disposed)
			{
				m_disposed = true;
				if (disposing && m_owner)
				{
					m_transaction.Dispose();
				}
			}
		}

		/// <inheritdoc />
		public int Id => m_transaction.Id;

		/// <inheritdoc />
		public FdbOperationContext Context => m_transaction.Context;

		/// <inheritdoc />
		public bool IsSnapshot => m_transaction.IsSnapshot;

		/// <inheritdoc />
		public bool IsReadOnly => m_readOnly;

		/// <inheritdoc />
		public virtual IFdbReadOnlyTransaction Snapshot => m_transaction.Snapshot;
		//BUGBUG: we need a snapshot wrapper ?

		/// <inheritdoc />
		public CancellationToken Cancellation => m_transaction.Cancellation;

		/// <inheritdoc />
		public virtual void EnsureCanRead()
		{
			ThrowIfDisposed();
			m_transaction.EnsureCanRead();
		}

		/// <inheritdoc />
		public virtual Task<Slice> GetAsync(Slice key)
		{
			ThrowIfDisposed();
			return m_transaction.GetAsync(key);
		}

		/// <inheritdoc />
		public virtual Task<Slice[]> GetValuesAsync(Slice[] keys)
		{
			ThrowIfDisposed();
			return m_transaction.GetValuesAsync(keys);
		}

		/// <inheritdoc />
		public virtual Task<Slice> GetKeyAsync(KeySelector selector)
		{
			ThrowIfDisposed();
			return m_transaction.GetKeyAsync(selector);
		}

		/// <inheritdoc />
		public virtual Task<Slice[]> GetKeysAsync(KeySelector[] selectors)
		{
			ThrowIfDisposed();
			return m_transaction.GetKeysAsync(selectors);
		}

		/// <inheritdoc />
		public virtual Task<FdbRangeChunk> GetRangeAsync(KeySelector beginInclusive, KeySelector endExclusive, FdbRangeOptions options = null, int iteration = 0)
		{
			ThrowIfDisposed();
			return m_transaction.GetRangeAsync(beginInclusive, endExclusive, options, iteration);
		}

		/// <inheritdoc />
		public FdbRangeQuery<KeyValuePair<Slice, Slice>> GetRange(KeySelector beginInclusive, KeySelector endExclusive, FdbRangeOptions options = null)
		{
			return GetRange(beginInclusive, endExclusive, kv => kv, options);
		}

		/// <inheritdoc />
		public virtual FdbRangeQuery<TResult> GetRange<TResult>(KeySelector beginInclusive, KeySelector endExclusive, Func<KeyValuePair<Slice, Slice>, TResult> selector, FdbRangeOptions options = null)
		{
			ThrowIfDisposed();
			return m_transaction.GetRange<TResult>(beginInclusive, endExclusive, selector, options);
		}

		/// <inheritdoc />
		public virtual Task<string[]> GetAddressesForKeyAsync(Slice key)
		{
			ThrowIfDisposed();
			return m_transaction.GetAddressesForKeyAsync(key);
		}

		/// <inheritdoc />
		public virtual Task<long> GetReadVersionAsync()
		{
			ThrowIfDisposed();
			return m_transaction.GetReadVersionAsync();
		}

		/// <inheritdoc />
		public virtual void EnsureCanWrite()
		{
			ThrowIfDisposed();
			m_transaction.EnsureCanWrite();
		}

		/// <inheritdoc />
		public virtual int Size => m_transaction.Size;

		/// <inheritdoc />
		public virtual void Set(Slice key, Slice value)
		{
			ThrowIfDisposed();
			m_transaction.Set(key, value);
		}

		/// <inheritdoc />
		public virtual void Atomic(Slice key, Slice param, FdbMutationType mutation)
		{
			ThrowIfDisposed();
			m_transaction.Atomic(key, param, mutation);
		}

		/// <inheritdoc />
		public virtual void Clear(Slice key)
		{
			ThrowIfDisposed();
			m_transaction.Clear(key);
		}

		/// <inheritdoc />
		public virtual void ClearRange(Slice beginKeyInclusive, Slice endKeyExclusive)
		{
			ThrowIfDisposed();
			m_transaction.ClearRange(beginKeyInclusive, endKeyExclusive);
		}

		/// <inheritdoc />
		public virtual void AddConflictRange(Slice beginKeyInclusive, Slice endKeyExclusive, FdbConflictRangeType type)
		{
			ThrowIfDisposed();
			m_transaction.AddConflictRange(beginKeyInclusive, endKeyExclusive, type);
		}

		/// <inheritdoc />
		public virtual void Cancel()
		{
			ThrowIfDisposed();
			m_transaction.Cancel();
		}

		/// <inheritdoc />
		public virtual void Reset()
		{
			ThrowIfDisposed();
			m_transaction.Reset();
		}

		/// <inheritdoc />
		public virtual Task CommitAsync()
		{
			ThrowIfDisposed();
			return m_transaction.CommitAsync();
		}

		/// <inheritdoc />
		public virtual long GetCommittedVersion()
		{
			ThrowIfDisposed();
			return m_transaction.GetCommittedVersion();
		}

		/// <inheritdoc />
		public virtual Task<VersionStamp> GetVersionStampAsync()
		{
			ThrowIfDisposed();
			return m_transaction.GetVersionStampAsync();
		}

		/// <inheritdoc />
		public virtual VersionStamp CreateVersionStamp()
		{
			ThrowIfDisposed();
			return m_transaction.CreateVersionStamp();
		}

		/// <inheritdoc />
		public virtual VersionStamp CreateVersionStamp(int userVersion)
		{
			ThrowIfDisposed();
			return m_transaction.CreateVersionStamp(userVersion);
		}

		/// <inheritdoc />
		public virtual void SetReadVersion(long version)
		{
			ThrowIfDisposed();
			m_transaction.SetReadVersion(version);
		}

		/// <inheritdoc />
		public virtual Task OnErrorAsync(FdbError code)
		{
			ThrowIfDisposed();
			return m_transaction.OnErrorAsync(code);
		}

		/// <inheritdoc />
		public virtual FdbWatch Watch(Slice key, CancellationToken ct)
		{
			ThrowIfDisposed();
			return m_transaction.Watch(key, ct);
		}

		/// <inheritdoc />
		public virtual void SetOption(FdbTransactionOption option)
		{
			ThrowIfDisposed();
			m_transaction.SetOption(option);
		}

		/// <inheritdoc />
		public virtual void SetOption(FdbTransactionOption option, string value)
		{
			ThrowIfDisposed();
			m_transaction.SetOption(option, value);
		}

		/// <inheritdoc />
		public virtual void SetOption(FdbTransactionOption option, long value)
		{
			ThrowIfDisposed();
			m_transaction.SetOption(option, value);
		}

		/// <inheritdoc />
		public int Timeout
		{
			get { return m_transaction.Timeout; }
			set
			{
				ThrowIfDisposed();
				m_transaction.Timeout = value;
			}
		}

		/// <inheritdoc />
		public int RetryLimit
		{
			get { return m_transaction.RetryLimit; }
			set
			{
				ThrowIfDisposed();
				m_transaction.RetryLimit = value;
			}
		}

		/// <inheritdoc />
		public int MaxRetryDelay
		{
			get { return m_transaction.MaxRetryDelay; }
			set
			{
				ThrowIfDisposed();
				m_transaction.MaxRetryDelay = value;
			}
		}

	}

	public class FdbReadOnlyTransactionFilter : IFdbReadOnlyTransaction
	{
		/// <summary>Inner database</summary>
		protected readonly IFdbReadOnlyTransaction m_transaction;

		protected FdbReadOnlyTransactionFilter(IFdbReadOnlyTransaction transaction)
		{
			Contract.NotNull(transaction, nameof(transaction));
			m_transaction = transaction;
		}

		/// <inheritdoc />
		public int Id => m_transaction.Id;

		/// <inheritdoc />
		public FdbOperationContext Context => m_transaction.Context;

		/// <inheritdoc />
		public bool IsSnapshot => m_transaction.IsSnapshot;

		/// <inheritdoc />
		public IFdbReadOnlyTransaction Snapshot => this;

		/// <inheritdoc />
		public CancellationToken Cancellation => m_transaction.Cancellation;

		/// <inheritdoc />
		public void EnsureCanRead()
		{
			m_transaction.EnsureCanRead();
		}

		/// <inheritdoc />
		public virtual Task<Slice> GetAsync(Slice key)
		{
			return m_transaction.GetAsync(key);
		}

		/// <inheritdoc />
		public virtual Task<Slice[]> GetValuesAsync(Slice[] keys)
		{
			return m_transaction.GetValuesAsync(keys);
		}

		/// <inheritdoc />
		public virtual Task<Slice> GetKeyAsync(KeySelector selector)
		{
			return m_transaction.GetKeyAsync(selector);
		}

		/// <inheritdoc />
		public virtual Task<Slice[]> GetKeysAsync(KeySelector[] selectors)
		{
			return m_transaction.GetKeysAsync(selectors);
		}

		/// <inheritdoc />
		public virtual Task<FdbRangeChunk> GetRangeAsync(KeySelector beginInclusive, KeySelector endExclusive, FdbRangeOptions options = null, int iteration = 0)
		{
			return m_transaction.GetRangeAsync(beginInclusive, endExclusive, options, iteration);
		}

		/// <inheritdoc />
		public FdbRangeQuery<KeyValuePair<Slice, Slice>> GetRange(KeySelector beginInclusive, KeySelector endInclusive, FdbRangeOptions options = null)
		{
			return GetRange(beginInclusive, endInclusive, kv => kv, options);
		}

		/// <inheritdoc />
		public virtual FdbRangeQuery<TResult> GetRange<TResult>(KeySelector beginInclusive, KeySelector endInclusive, Func<KeyValuePair<Slice, Slice>, TResult> selector, FdbRangeOptions options = null)
		{
			return m_transaction.GetRange(beginInclusive, endInclusive, selector, options);
		}

		/// <inheritdoc />
		public virtual Task<string[]> GetAddressesForKeyAsync(Slice key)
		{
			return m_transaction.GetAddressesForKeyAsync(key);
		}

		/// <inheritdoc />
		public virtual Task<long> GetReadVersionAsync()
		{
			return m_transaction.GetReadVersionAsync();
		}

		/// <inheritdoc />
		public virtual void SetReadVersion(long version)
		{
			m_transaction.SetReadVersion(version);
		}

		/// <inheritdoc />
		public virtual void Cancel()
		{
			m_transaction.Cancel();
		}

		/// <inheritdoc />
		public virtual void Reset()
		{
			m_transaction.Reset();
		}

		/// <inheritdoc />
		public virtual Task OnErrorAsync(FdbError code)
		{
			return m_transaction.OnErrorAsync(code);
		}

		/// <inheritdoc />
		public virtual void SetOption(FdbTransactionOption option)
		{
			m_transaction.SetOption(option);
		}

		/// <inheritdoc />
		public virtual void SetOption(FdbTransactionOption option, string value)
		{
			m_transaction.SetOption(option, value);
		}

		/// <inheritdoc />
		public virtual void SetOption(FdbTransactionOption option, long value)
		{
			m_transaction.SetOption(option, value);
		}

		/// <inheritdoc />
		public virtual int Timeout
		{
			get { return m_transaction.Timeout; }
			set { m_transaction.Timeout = value; }
		}

		/// <inheritdoc />
		public virtual int RetryLimit
		{
			get { return m_transaction.RetryLimit; }
			set { m_transaction.RetryLimit = value; }
		}

		/// <inheritdoc />
		public virtual int MaxRetryDelay
		{
			get { return m_transaction.MaxRetryDelay; }
			set { m_transaction.MaxRetryDelay = value; }
		}

		public void Dispose()
		{
			Dispose(true);
			GC.SuppressFinalize(this);
		}

		protected virtual void Dispose(bool disposing)
		{
			//NOP?
		}
	}

}
