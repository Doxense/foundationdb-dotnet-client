﻿#region BSD License
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

namespace FoundationDB.Client
{
	using System;
	using System.Collections.Generic;
	using System.Threading;
	using System.Threading.Tasks;
	using Doxense.Diagnostics.Contracts;
	using FoundationDB.Filters.Logging;

	/// <summary>Wraps an FDB_TRANSACTION handle</summary>
	public partial class FdbTransaction
	{

		/// <summary>Snapshot version of this transaction (lazily allocated)</summary>
		private Snapshotted? m_snapshotted;

		/// <summary>Returns a version of this transaction that perform snapshotted operations</summary>
		public IFdbReadOnlyTransaction Snapshot
		{
			get
			{
				EnsureNotFailedOrDisposed();
				//TODO: we need to check if the transaction handler supports Snapshot isolation level
				return m_snapshotted ??= new Snapshotted(this);
			}
		}

		/// <summary>Wrapper on a transaction, that will use Snapshot mode on all read operations</summary>
		private sealed class Snapshotted : IFdbReadOnlyTransaction
		{

			private readonly FdbTransaction m_parent;

			public Snapshotted(FdbTransaction parent)
			{
				Contract.NotNull(parent, nameof(parent));
				m_parent = parent;
			}

			public int Id => m_parent.Id;

			public FdbOperationContext Context => m_parent.Context;

			public CancellationToken Cancellation => m_parent.Cancellation;

			public bool IsSnapshot => true;

			public IFdbReadOnlyTransaction Snapshot => this;

			public void EnsureCanRead()
			{
				m_parent.EnsureCanRead();
			}

			public Task<long> GetReadVersionAsync()
			{
				return m_parent.GetReadVersionAsync();
			}

			public Task<VersionStamp?> GetMetadataVersionKeyAsync(Slice key = default)
			{
				return m_parent.GetMetadataVersionKeyAsync(key.IsNull ? Fdb.System.MetadataVersionKey : key, snapshot: true);
			}

			void IFdbReadOnlyTransaction.SetReadVersion(long version)
			{
				throw new NotSupportedException("You cannot set the read version on the Snapshot view of a transaction");
			}

			/// <inheritdoc />
			public Task<Slice> GetAsync(ReadOnlySpan<byte> key)
			{
				EnsureCanRead();

				FdbKey.EnsureKeyIsValid(key);

#if DEBUG
				if (Logging.On && Logging.IsVerbose) Logging.Verbose(this, "GetAsync", $"Getting value for '{key.ToString()}'");
#endif

				return m_parent.PerformGetOperation(key, snapshot: true);
			}

			public Task<Slice[]> GetValuesAsync(Slice[] keys)
			{
				Contract.NotNull(keys, nameof(keys));

				EnsureCanRead();

				FdbKey.EnsureKeysAreValid(keys);

#if DEBUG
				if (Logging.On && Logging.IsVerbose) Logging.Verbose(this, "GetValuesAsync", $"Getting batch of {keys.Length} values ...");
#endif

				return m_parent.PerformGetValuesOperation(keys, snapshot: true);
			}

			public Task<Slice> GetKeyAsync(KeySelector selector)
			{
				EnsureCanRead();

				FdbKey.EnsureKeyIsValid(selector.Key);

#if DEBUG
				if (Logging.On && Logging.IsVerbose) Logging.Verbose(this, "GetKeyAsync", $"Getting key '{selector.ToString()}'");
#endif

				return m_parent.PerformGetKeyOperation(selector, snapshot: true);
			}

			public Task<Slice[]> GetKeysAsync(KeySelector[] selectors)
			{
				EnsureCanRead();

				for(int i = 0; i < selectors.Length; i++)
				{
					FdbKey.EnsureKeyIsValid(selectors[i].Key);
				}

#if DEBUG
				if (Logging.On && Logging.IsVerbose) Logging.Verbose(this, "GetKeysCoreAsync", $"Getting batch of {selectors.Length} keys ...");
#endif

				return m_parent.PerformGetKeysOperation(selectors, snapshot: true);
			}

			public Task<FdbRangeChunk> GetRangeAsync(KeySelector beginInclusive, KeySelector endExclusive, FdbRangeOptions? options, int iteration)
			{
				int limit = options?.Limit ?? 0;
				bool reverse = options?.Reverse ?? false;
				int targetBytes = options?.TargetBytes ?? 0;
				var mode = options?.Mode ?? FdbStreamingMode.Iterator;
				var read = options?.Read ?? FdbReadMode.Both;

				return GetRangeAsync(beginInclusive, endExclusive, limit, reverse, targetBytes, mode, read, iteration);
			}

			/// <inheritdoc />
			public Task<FdbRangeChunk> GetRangeAsync(KeySelector beginInclusive,
				KeySelector endExclusive,
				int limit = 0,
				bool reverse = false,
				int targetBytes = 0,
				FdbStreamingMode mode = FdbStreamingMode.Exact,
				FdbReadMode read = FdbReadMode.Both,
				int iteration = 0)
			{
				EnsureCanRead();

				FdbKey.EnsureKeyIsValid(beginInclusive.Key);
				FdbKey.EnsureKeyIsValid(endExclusive.Key, endExclusive: true);

				FdbRangeOptions.EnsureLegalValues(limit, targetBytes, mode, read, iteration);

				// The iteration value is only needed when in iterator mode, but then it should start from 1
				if (iteration == 0) iteration = 1;

				return m_parent.PerformGetRangeOperation(beginInclusive, endExclusive, snapshot: true, limit, reverse, targetBytes, mode, read, iteration);
			}

			public FdbRangeQuery<KeyValuePair<Slice, Slice>> GetRange(KeySelector beginInclusive, KeySelector endExclusive, FdbRangeOptions? options = null)
			{
				return m_parent.GetRangeCore(beginInclusive, endExclusive, options, snapshot: true, kv => kv);
			}

			public FdbRangeQuery<TResult> GetRange<TResult>(KeySelector beginInclusive, KeySelector endExclusive, Func<KeyValuePair<Slice, Slice>, TResult> selector, FdbRangeOptions? options = null)
			{
				return m_parent.GetRangeCore(beginInclusive, endExclusive, options, snapshot: true, selector);
			}

			public Task<string[]> GetAddressesForKeyAsync(ReadOnlySpan<byte> key)
			{
				EnsureCanRead();
				return m_parent.PerformGetAddressesForKeyOperation(key);
			}

			/// <inheritdoc />
			public Task<(FdbValueCheckResult Result, Slice Actual)> CheckValueAsync(ReadOnlySpan<byte> key, Slice expected)
			{
				EnsureCanRead();

				FdbKey.EnsureKeyIsValid(key);

#if DEBUG
				if (Logging.On && Logging.IsVerbose) Logging.Verbose(this, "ValueCheckAsync", $"Checking the value for '{key.ToString()}'");
#endif

				return m_parent.PerformValueCheckOperation(key, expected, snapshot: true);
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

			public void SetOption(FdbTransactionOption option, ReadOnlySpan<char> value)
			{
				m_parent.SetOption(option, value);
			}

			public void SetOption(FdbTransactionOption option, long value)
			{
				m_parent.SetOption(option, value);
			}

			public int Timeout
			{
				get => m_parent.Timeout;
				set => throw new NotSupportedException("The timeout value cannot be changed via the Snapshot view of a transaction.");
			}

			public int RetryLimit
			{
				get => m_parent.RetryLimit;
				set => throw new NotSupportedException("The retry limit value cannot be changed via the Snapshot view of a transaction.");
			}

			public int MaxRetryDelay
			{
				get => m_parent.MaxRetryDelay;
				set => throw new NotSupportedException("The max retry delay value cannot be changed via the Snapshot view of a transaction.");
			}

			void IDisposable.Dispose()
			{
				// NO-OP
			}


			public FdbTransactionLog? Log => m_parent.Log;

			public bool IsLogged() => m_parent.IsLogged();

			public void StopLogging() => m_parent.StopLogging();

			public void Annotate(string comment) => m_parent.Annotate(comment);

		}

	}

}
