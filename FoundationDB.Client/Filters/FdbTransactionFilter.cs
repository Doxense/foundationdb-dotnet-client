﻿#region BSD Licence
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

namespace FoundationDB.Filters
{
	using FoundationDB.Client;
	using System;
	using System.Collections.Generic;
	using System.Threading;
	using System.Threading.Tasks;

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
		protected FdbTransactionFilter(IFdbTransaction trans, bool forceReadOnly, bool ownsTransaction)
		{
			if (trans == null) throw new ArgumentNullException("trans");

			m_transaction = trans;
			m_readOnly = forceReadOnly || trans.IsReadOnly;
			m_owner = ownsTransaction;
		}

		protected void ThrowIfDisposed()
		{
			if (m_disposed) throw new ObjectDisposedException(this.GetType().Name);
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

		public int Id
		{
			get { return m_transaction.Id; }
		}

		public FdbOperationContext Context
		{
			get { return m_transaction.Context; }
		}

		public bool IsSnapshot
		{
			get { return m_transaction.IsSnapshot; }
		}

		public bool IsReadOnly
		{
			get { return m_readOnly; }
		}

		public virtual IFdbReadOnlyTransaction Snapshot
		{
			get
			{
				//BUGBUG: we need a snapshot wrapper ?
				return m_transaction.Snapshot;
			}
		}

		public virtual FdbIsolationLevel IsolationLevel
		{
			get
			{
				return m_transaction.IsolationLevel;
			}
		}

		public CancellationToken Cancellation
		{
			get { return m_transaction.Cancellation; }
		}

		public virtual void EnsureCanRead()
		{
			ThrowIfDisposed();
			m_transaction.EnsureCanRead();
		}

		public virtual Task<Slice> GetAsync(Slice key)
		{
			ThrowIfDisposed();
			return m_transaction.GetAsync(key);
		}

		public virtual Task<Slice[]> GetValuesAsync(Slice[] keys)
		{
			ThrowIfDisposed();
			return m_transaction.GetValuesAsync(keys);
		}

		public virtual Task<Slice> GetKeyAsync(FdbKeySelector selector)
		{
			ThrowIfDisposed();
			return m_transaction.GetKeyAsync(selector);
		}

		public virtual Task<Slice[]> GetKeysAsync(FdbKeySelector[] selectors)
		{
			ThrowIfDisposed();
			return m_transaction.GetKeysAsync(selectors);
		}

		public virtual Task<FdbRangeChunk> GetRangeAsync(FdbKeySelector beginInclusive, FdbKeySelector endExclusive, FdbRangeOptions options = null, int iteration = 0)
		{
			ThrowIfDisposed();
			return m_transaction.GetRangeAsync(beginInclusive, endExclusive, options, iteration);
		}

		public virtual FdbRangeQuery<KeyValuePair<Slice, Slice>> GetRange(FdbKeySelector beginInclusive, FdbKeySelector endExclusive, FdbRangeOptions options = null)
		{
			ThrowIfDisposed();
			return m_transaction.GetRange(beginInclusive, endExclusive, options);
		}

		public virtual Task<string[]> GetAddressesForKeyAsync(Slice key)
		{
			ThrowIfDisposed();
			return m_transaction.GetAddressesForKeyAsync(key);
		}

		public virtual Task<long> GetReadVersionAsync()
		{
			ThrowIfDisposed();
			return m_transaction.GetReadVersionAsync();
		}

		public virtual void EnsureCanWrite()
		{
			ThrowIfDisposed();
			m_transaction.EnsureCanWrite();
		}

		public virtual int Size
		{
			get { return m_transaction.Size; }
		}

		public virtual void Set(Slice key, Slice value)
		{
			ThrowIfDisposed();
			m_transaction.Set(key, value);
		}

		public virtual void Atomic(Slice key, Slice param, FdbMutationType mutation)
		{
			ThrowIfDisposed();
			m_transaction.Atomic(key, param, mutation);
		}

		public virtual void Clear(Slice key)
		{
			ThrowIfDisposed();
			m_transaction.Clear(key);
		}

		public virtual void ClearRange(Slice beginKeyInclusive, Slice endKeyExclusive)
		{
			ThrowIfDisposed();
			m_transaction.ClearRange(beginKeyInclusive, endKeyExclusive);
		}

		public virtual void AddConflictRange(Slice beginKeyInclusive, Slice endKeyExclusive, FdbConflictRangeType type)
		{
			ThrowIfDisposed();
			m_transaction.AddConflictRange(beginKeyInclusive, endKeyExclusive, type);
		}

		public virtual void Cancel()
		{
			ThrowIfDisposed();
			m_transaction.Cancel();
		}

		public virtual void Reset()
		{
			ThrowIfDisposed();
			m_transaction.Reset();
		}

		public virtual Task CommitAsync()
		{
			ThrowIfDisposed();
			return m_transaction.CommitAsync();
		}

		public virtual long GetCommittedVersion()
		{
			ThrowIfDisposed();
			return m_transaction.GetCommittedVersion();
		}

		public virtual void SetReadVersion(long version)
		{
			ThrowIfDisposed();
			m_transaction.SetReadVersion(version);
		}

		public virtual Task OnErrorAsync(FdbError code)
		{
			ThrowIfDisposed();
			return m_transaction.OnErrorAsync(code);
		}

		public virtual FdbWatch Watch(Slice key, CancellationToken cancellationToken)
		{
			ThrowIfDisposed();
			return m_transaction.Watch(key, cancellationToken);
		}

		public virtual void SetOption(FdbTransactionOption option)
		{
			ThrowIfDisposed();
			m_transaction.SetOption(option);
		}

		public virtual void SetOption(FdbTransactionOption option, string value)
		{
			ThrowIfDisposed();
			m_transaction.SetOption(option, value);
		}

		public virtual void SetOption(FdbTransactionOption option, long value)
		{
			ThrowIfDisposed();
			m_transaction.SetOption(option, value);
		}

		public int Timeout
		{
			get { return m_transaction.Timeout; }
			set
			{
				ThrowIfDisposed();
				m_transaction.Timeout = value;
			}
		}

		public int RetryLimit
		{
			get { return m_transaction.RetryLimit; }
			set
			{
				ThrowIfDisposed();
				m_transaction.RetryLimit = value;
			}
		}

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
			if (transaction == null) throw new ArgumentNullException("transaction");
			m_transaction = transaction;
		}

		public int Id
		{
			get { return m_transaction.Id; }
		}

		public FdbOperationContext Context
		{
			get { return m_transaction.Context; }
		}

		public bool IsSnapshot
		{
			get { return m_transaction.IsSnapshot; }
		}

		public IFdbReadOnlyTransaction Snapshot
		{
			get { return this; }
		}

		public FdbIsolationLevel IsolationLevel
		{
			get { return m_transaction.IsolationLevel; }
		}

		public CancellationToken Cancellation
		{
			get { return m_transaction.Cancellation; }
		}

		public void EnsureCanRead()
		{
			m_transaction.EnsureCanRead();
		}

		public virtual Task<Slice> GetAsync(Slice key)
		{
			return m_transaction.GetAsync(key);
		}

		public virtual Task<Slice[]> GetValuesAsync(Slice[] keys)
		{
			return m_transaction.GetValuesAsync(keys);
		}

		public virtual Task<Slice> GetKeyAsync(FdbKeySelector selector)
		{
			return m_transaction.GetKeyAsync(selector);
		}

		public virtual Task<Slice[]> GetKeysAsync(FdbKeySelector[] selectors)
		{
			return m_transaction.GetKeysAsync(selectors);
		}

		public virtual Task<FdbRangeChunk> GetRangeAsync(FdbKeySelector beginInclusive, FdbKeySelector endExclusive, FdbRangeOptions options = null, int iteration = 0)
		{
			return m_transaction.GetRangeAsync(beginInclusive, endExclusive, options, iteration);
		}

		public virtual FdbRangeQuery<KeyValuePair<Slice, Slice>> GetRange(FdbKeySelector beginInclusive, FdbKeySelector endInclusive, FdbRangeOptions options = null)
		{
			return m_transaction.GetRange(beginInclusive, endInclusive, options);
		}

		public virtual Task<string[]> GetAddressesForKeyAsync(Slice key)
		{
			return m_transaction.GetAddressesForKeyAsync(key);
		}

		public virtual Task<long> GetReadVersionAsync()
		{
			return m_transaction.GetReadVersionAsync();
		}

		public virtual void SetReadVersion(long version)
		{
			m_transaction.SetReadVersion(version);
		}

		public virtual void Cancel()
		{
			m_transaction.Cancel();
		}

		public virtual void Reset()
		{
			m_transaction.Reset();
		}

		public virtual Task OnErrorAsync(FdbError code)
		{
			return m_transaction.OnErrorAsync(code);
		}

		public virtual void SetOption(FdbTransactionOption option)
		{
			m_transaction.SetOption(option);
		}

		public virtual void SetOption(FdbTransactionOption option, string value)
		{
			m_transaction.SetOption(option, value);
		}

		public virtual void SetOption(FdbTransactionOption option, long value)
		{
			m_transaction.SetOption(option, value);
		}

		public virtual int Timeout
		{
			get { return m_transaction.Timeout; }
			set { m_transaction.Timeout = value; }
		}

		public virtual int RetryLimit
		{
			get { return m_transaction.RetryLimit; }
			set { m_transaction.RetryLimit = value; }
		}

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
