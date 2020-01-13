#region BSD License
/* Copyright (c) 2013-2019, Doxense SAS
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

namespace FoundationDB.Filters.Logging
{
	using System;
	using System.Collections.Generic;
	using System.Threading;
	using System.Threading.Tasks;
	using Doxense;
	using Doxense.Diagnostics.Contracts;
	using FoundationDB.Client;
	using JetBrains.Annotations;

	[Flags]
	[PublicAPI]
	public enum FdbLoggingOptions
	{
		/// <summary>Default logging options</summary>
		Default = 0,

		/// <summary>Capture the stacktrace of the caller method that created the transaction</summary>
		RecordCreationStackTrace = 0x100,

		/// <summary>Capture the stacktrace of the caller method for each operation</summary>
		RecordOperationStackTrace = 0x200,

		/// <summary>Capture all the stacktraces.</summary>
		/// <remarks>This is a shortcurt for <see cref="RecordCreationStackTrace"/> | <see cref="RecordOperationStackTrace"/></remarks>
		WithStackTraces = RecordCreationStackTrace | RecordOperationStackTrace,
	}

	/// <summary>Transaction filter that logs and measure all operations performed on the underlying transaction</summary>
	[PublicAPI]
	public sealed class FdbLoggedTransaction : FdbTransactionFilter
	{
		private Snapshotted m_snapshotted;

		/// <summary>Log of all operations performed on this transaction</summary>
		public FdbTransactionLog Log {[NotNull] get; private set; }

		/// <summary>Handler that will be called when this transaction commits successfully</summary>
		public Action<FdbLoggedTransaction> Committed { get; private set; }

		/// <summary>If non-null, at least one VersionStamped operation in the last attempt</summary>
		private Task<VersionStamp> VersionStamp { get; set; }

		/// <summary>Wrap an existing transaction and log all operations performed</summary>
		public FdbLoggedTransaction(IFdbTransaction trans, bool ownsTransaction, Action<FdbLoggedTransaction> onCommitted, FdbLoggingOptions options)
			: base(trans, false, ownsTransaction)
		{
			this.Log = new FdbTransactionLog(options);
			this.Committed = onCommitted;
			this.Log.Start(this);
		}

		protected override void Dispose(bool disposing)
		{
			try
			{
				base.Dispose(disposing);
			}
			finally
			{
				if (!this.Log.Completed)
				{
					this.Log.Stop(this);
					OnCommitted();
				}
			}
		}

		#region Data interning...

		private byte[] m_buffer = new byte[1024];
		private int m_offset;
		private readonly object m_lock = new object();

		private Slice Grab(Slice slice)
		{
			if (slice.IsNullOrEmpty) return slice.IsNull ? Slice.Nil : Slice.Empty;

			lock (m_lock)
			{
				if (slice.Count > m_buffer.Length - m_offset)
				{ // not enough ?
					if (slice.Count >= 2048)
					{
						return slice.Memoize();
					}
					m_buffer = new byte[4096];
					m_offset = 0;
				}

				int start = m_offset;
				slice.CopyTo(m_buffer, m_offset);
				m_offset += slice.Count;
				return m_buffer.AsSlice(start, slice.Count);
			}
		}

		private Slice Grab(ReadOnlySpan<byte> slice)
		{
			if (slice.Length == 0) return Slice.Empty;

			lock (m_lock)
			{
				if (slice.Length > m_buffer.Length - m_offset)
				{ // not enough ?
					if (slice.Length >= 2048)
					{
						return slice.ToArray().AsSlice();
					}
					m_buffer = new byte[4096];
					m_offset = 0;
				}

				int start = m_offset;
				slice.CopyTo(m_buffer.AsSpan(m_offset));
				m_offset += slice.Length;
				return m_buffer.AsSlice(start, slice.Length);
			}
		}

		private Slice[] Grab(Slice[] slices)
		{
			if (slices == null) return null;
			if (slices.Length == 0) return Array.Empty<Slice>();

			lock (m_lock)
			{
				int total = 0;
				for (int i = 0; i < slices.Length; i++)
				{
					total += slices[i].Count;
				}

				if (total > m_buffer.Length - m_offset)
				{
					return FdbKey.Merge(Slice.Empty, slices);
				}

				var res = new Slice[slices.Length];
				for (int i = 0; i < slices.Length; i++)
				{
					res[i] = Grab(slices[i]);
				}
				return res;
			}
		}

		private KeySelector Grab(in KeySelector selector)
		{
			return new KeySelector(
				Grab(selector.Key),
				selector.OrEqual,
				selector.Offset
			);
		}

		private KeySelector[] Grab(KeySelector[] selectors)
		{
			if (selectors == null) return null;
			if (selectors.Length == 0) return Array.Empty<KeySelector>();

			var res = new KeySelector[selectors.Length];
			for (int i = 0; i < selectors.Length; i++)
			{
				res[i] = Grab(in selectors[i]);
			}
			return res;
		}

		#endregion

		#region Instrumentation...

		private void OnCommitted()
		{
			if (this.Committed != null)
			{
				try
				{
					this.Committed(this);
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

		private void Execute<TCommand>([NotNull] TCommand cmd, [NotNull] Action<IFdbTransaction, TCommand> action)
			where TCommand : FdbTransactionLog.Command
		{
			ThrowIfDisposed();
			Exception error = null;
			this.Log.BeginOperation(cmd);
			try
			{
				action(m_transaction, cmd);
			}
			catch (Exception e)
			{
				error = e;
				throw;
			}
			finally
			{
				this.Log.EndOperation(cmd, error);
			}
		}

		private async Task ExecuteAsync<TCommand>([NotNull] TCommand cmd, [NotNull] Func<IFdbTransaction, TCommand, Task> lambda, Action<FdbLoggedTransaction, IFdbTransaction> onSuccess = null)
			where TCommand : FdbTransactionLog.Command
		{
			ThrowIfDisposed();
			Exception error = null;
			var tr = m_transaction;
			this.Log.BeginOperation(cmd);
			try
			{
				await lambda(tr, cmd).ConfigureAwait(false);
			}
			catch (Exception e)
			{
				error = e;
				throw;
			}
			finally
			{
				this.Log.EndOperation(cmd, error);
				if (error == null) onSuccess?.Invoke(this, tr);
			}
		}

		private async Task<TResult> ExecuteAsync<TCommand, TResult>([NotNull] TCommand cmd, [NotNull] Func<IFdbTransaction, TCommand, Task<TResult>> lambda)
			where TCommand : FdbTransactionLog.Command<TResult>
		{
			ThrowIfDisposed();
			Exception error = null;
			this.Log.BeginOperation(cmd);
			try
			{
				TResult result = await lambda(m_transaction, cmd).ConfigureAwait(false);
				cmd.Result = Maybe.Return<TResult>(result);
				return result;
			}
			catch (Exception e)
			{
				error = e;
				cmd.Result = Maybe.Error<TResult>(System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(e));
				throw;
			}
			finally
			{
				this.Log.EndOperation(cmd, error);
			}
		}

		#endregion

		#region Meta...

		public override void Cancel()
		{
			Execute(
				new FdbTransactionLog.CancelCommand(),
				(tr, cmd) => tr.Cancel()
			);
		}

		public override void Reset()
		{
			this.VersionStamp = null;
			Execute(
				new FdbTransactionLog.ResetCommand(),
				(tr, cmd) => tr.Reset()
			);
		}

		public override FdbWatch Watch(ReadOnlySpan<byte> key, CancellationToken ct)
		{
			var cmd = new FdbTransactionLog.WatchCommand(Grab(key));
			this.Log.AddOperation(cmd);
			return m_transaction.Watch(cmd.Key, ct);
		}

		#endregion

		#region Write...

		public override Task CommitAsync()
		{
			int size = m_transaction.Size;
			this.Log.CommitSize = size;
			this.Log.TotalCommitSize += size;
			this.Log.Attempts++;

			var cmd = new FdbTransactionLog.CommitCommand();
			return ExecuteAsync(
				cmd,
				(tr, _) => tr.CommitAsync(),
				(self, tr) =>
				{
					self.Log.CommittedUtc = DateTimeOffset.UtcNow;
					var cv = tr.GetCommittedVersion();
					self.Log.CommittedVersion = cv;
					cmd.CommitVersion = cv;

					if (this.VersionStamp != null) self.Log.VersionStamp = this.VersionStamp.GetAwaiter().GetResult();
				}
			);
		}

		public override Task OnErrorAsync(FdbError code)
		{
			this.VersionStamp = null;
			return ExecuteAsync(
				new FdbTransactionLog.OnErrorCommand(code),
				(_tr, _cmd) => _tr.OnErrorAsync(_cmd.Code)
			);
		}

		public override Task<VersionStamp> GetVersionStampAsync()
		{
			return ExecuteAsync(
				new FdbTransactionLog.GetVersionStampCommand(),
				(tr, cmd) => tr.GetVersionStampAsync()
			);
		}

		public override void Set(ReadOnlySpan<byte> key, ReadOnlySpan<byte> value)
		{
			Execute(
				new FdbTransactionLog.SetCommand(Grab(key), Grab(value)),
				(_tr, _cmd) => _tr.Set(_cmd.Key, _cmd.Value)
			);
		}

		public override void Clear(ReadOnlySpan<byte> key)
		{
			ThrowIfDisposed();
			Execute(
				new FdbTransactionLog.ClearCommand(Grab(key)),
				(_tr, _cmd) => _tr.Clear(_cmd.Key)
			);
		}

		public override void ClearRange(ReadOnlySpan<byte> beginKeyInclusive, ReadOnlySpan<byte> endKeyExclusive)
		{
			Execute(
				new FdbTransactionLog.ClearRangeCommand(Grab(beginKeyInclusive), Grab(endKeyExclusive)),
				(_tr, _cmd) => _tr.ClearRange(_cmd.Begin, _cmd.End)
			);
		}

		public override void Atomic(ReadOnlySpan<byte> key, ReadOnlySpan<byte> param, FdbMutationType mutation)
		{
			if (mutation == FdbMutationType.VersionStampedKey || mutation == FdbMutationType.VersionStampedValue)
			{
				this.VersionStamp ??= m_transaction.GetVersionStampAsync();
			}
			Execute(
				new FdbTransactionLog.AtomicCommand(Grab(key), Grab(param), mutation),
				(_tr, _cmd) => _tr.Atomic(_cmd.Key, _cmd.Param, _cmd.Mutation)
			);
		}

		public override void AddConflictRange(ReadOnlySpan<byte> beginKeyInclusive, ReadOnlySpan<byte> endKeyExclusive, FdbConflictRangeType type)
		{
			Execute(
				new FdbTransactionLog.AddConflictRangeCommand(Grab(beginKeyInclusive), Grab(endKeyExclusive), type),
				(_tr, _cmd) => _tr.AddConflictRange(_cmd.Begin, _cmd.End, _cmd.Type)
			);
		}

		public override void TouchMetadataVersionKey(Slice key = default)
		{
			Execute(
				new FdbTransactionLog.TouchMetadataVersionKeyCommand(key.IsNull ? Fdb.System.MetadataVersionKey : Grab(key)),
				(tr, cmd) => tr.TouchMetadataVersionKey(cmd.Key)
			);
		}

		#endregion

		#region Read...

		public override Task<long> GetReadVersionAsync()
		{
			return ExecuteAsync(
				new FdbTransactionLog.GetReadVersionCommand(),
				(tr, cmd) => tr.GetReadVersionAsync()
			);
		}

		public override Task<VersionStamp?> GetMetadataVersionKeyAsync(Slice key = default)
		{
			return ExecuteAsync(
				new FdbTransactionLog.GetMetadataVersionCommand(key.IsNull ? Fdb.System.MetadataVersionKey : Grab(key)),
				(tr, cmd) => tr.GetMetadataVersionKeyAsync(cmd.Key)
			);
		}

		public override Task<Slice> GetAsync(ReadOnlySpan<byte> key)
		{
			return ExecuteAsync(
				new FdbTransactionLog.GetCommand(Grab(key)),
				(tr, cmd) => tr.GetAsync(cmd.Key)
			);
		}

		public override Task<Slice> GetKeyAsync(KeySelector selector)
		{
			return ExecuteAsync(
				new FdbTransactionLog.GetKeyCommand(Grab(in selector)),
				(tr, cmd) => tr.GetKeyAsync(selector)
			);
		}

		public override Task<Slice[]> GetValuesAsync(Slice[] keys)
		{
			Contract.Requires(keys != null);
			return ExecuteAsync(
				new FdbTransactionLog.GetValuesCommand(Grab(keys)),
				(tr, cmd) => tr.GetValuesAsync(keys)
			);
		}

		public override Task<Slice[]> GetKeysAsync(KeySelector[] selectors)
		{
			Contract.Requires(selectors != null);
			return ExecuteAsync(
				new FdbTransactionLog.GetKeysCommand(Grab(selectors)),
				(tr, cmd) => tr.GetKeysAsync(selectors)
			);
		}

		public override Task<FdbRangeChunk> GetRangeAsync(KeySelector beginInclusive, KeySelector endExclusive, FdbRangeOptions? options = null, int iteration = 0)
		{
			return ExecuteAsync(
				new FdbTransactionLog.GetRangeCommand(Grab(in beginInclusive), Grab(in endExclusive), options, iteration),
				(tr, cmd) => tr.GetRangeAsync(beginInclusive, endExclusive, options, iteration)
			);
		}

		public override FdbRangeQuery<TResult> GetRange<TResult>(KeySelector beginInclusive, KeySelector endExclusive, Func<KeyValuePair<Slice, Slice>, TResult> selector, FdbRangeOptions? options = null)
		{
			ThrowIfDisposed();

			var query = m_transaction.GetRange(beginInclusive, endExclusive, selector, options);
			// this method does not execute immediately, so we don't need to record any operation here, only when GetRangeAsync() is called (by ToListAsync() or any other LINQ operator)
			// we must override the transaction used by the query, so that we are notified when this happens
			return query.UseTransaction(this);
		}

		#endregion

		#region Snapshot...

		public override IFdbReadOnlyTransaction Snapshot => m_snapshotted ?? (m_snapshotted = new Snapshotted(this, m_transaction.Snapshot));

		private sealed class Snapshotted : FdbReadOnlyTransactionFilter
		{
			private readonly FdbLoggedTransaction m_parent;

			public Snapshotted([NotNull] FdbLoggedTransaction parent, [NotNull] IFdbReadOnlyTransaction snapshot)
				: base(snapshot)
			{
				m_parent = parent;
			}

			private async Task<TResult> ExecuteAsync<TCommand, TResult>([NotNull] TCommand cmd, [NotNull] Func<IFdbReadOnlyTransaction, TCommand, Task<TResult>> lambda)
				where TCommand : FdbTransactionLog.Command<TResult>
			{
				m_parent.ThrowIfDisposed();
				Exception error = null;
				cmd.Snapshot = true;
				m_parent.Log.BeginOperation(cmd);
				try
				{
					TResult result = await lambda(m_transaction, cmd).ConfigureAwait(false);
					cmd.Result = Maybe.Return<TResult>(result);
					return result;
				}
				catch (Exception e)
				{
					error = e;
					cmd.Result = Maybe.Error<TResult>(System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(e));
					throw;
				}
				finally
				{
					m_parent.Log.EndOperation(cmd, error);
				}
			}

			public override Task<long> GetReadVersionAsync()
			{
				return ExecuteAsync(
					new FdbTransactionLog.GetReadVersionCommand(),
					(tr, cmd) => tr.GetReadVersionAsync()
				);
			}

			public override Task<VersionStamp?> GetMetadataVersionKeyAsync(Slice key = default)
			{
				return ExecuteAsync(
					new FdbTransactionLog.GetMetadataVersionCommand(key.IsNull ? Fdb.System.MetadataVersionKey : m_parent.Grab(key)), 
					(tr, cmd) => tr.GetMetadataVersionKeyAsync(cmd.Key)
				);
			}

			public override Task<Slice> GetAsync(ReadOnlySpan<byte> key)
			{
				return ExecuteAsync(
					new FdbTransactionLog.GetCommand(m_parent.Grab(key)),
					(tr, cmd) => tr.GetAsync(cmd.Key)
				);
			}

			public override Task<Slice> GetKeyAsync(KeySelector selector)
			{
				return ExecuteAsync(
					new FdbTransactionLog.GetKeyCommand(m_parent.Grab(in selector)),
					(tr, cmd) => tr.GetKeyAsync(cmd.Selector)
				);
			}

			public override Task<Slice[]> GetValuesAsync(Slice[] keys)
			{
				Contract.Requires(keys != null);
				return ExecuteAsync(
					new FdbTransactionLog.GetValuesCommand(m_parent.Grab(keys)),
					(tr, cmd) => tr.GetValuesAsync(cmd.Keys)
				);
			}

			public override Task<Slice[]> GetKeysAsync(KeySelector[] selectors)
			{
				Contract.Requires(selectors != null);
				return ExecuteAsync(
					new FdbTransactionLog.GetKeysCommand(m_parent.Grab(selectors)),
					(tr, cmd) => tr.GetKeysAsync(cmd.Selectors)
				);
			}

			public override Task<FdbRangeChunk> GetRangeAsync(KeySelector beginInclusive, KeySelector endExclusive, FdbRangeOptions? options = null, int iteration = 0)
			{
				return ExecuteAsync(
					new FdbTransactionLog.GetRangeCommand(m_parent.Grab(in beginInclusive), m_parent.Grab(in endExclusive), options, iteration),
					(tr, cmd) => tr.GetRangeAsync(cmd.Begin, cmd.End, cmd.Options, cmd.Iteration)
				);
			}

			public override FdbRangeQuery<TResult> GetRange<TResult>(KeySelector beginInclusive, KeySelector endExclusive, Func<KeyValuePair<Slice, Slice>, TResult> selector, FdbRangeOptions? options = null)
			{
				m_parent.ThrowIfDisposed();
				var query = m_transaction.GetRange(beginInclusive, endExclusive, selector, options);
				return query.UseTransaction(this);
			}

		}
		#endregion

		#region Options...

		public override void SetOption(FdbTransactionOption option)
		{
			ThrowIfDisposed();
			this.Log.AddOperation(new FdbTransactionLog.SetOptionCommand(option), countAsOperation: false);
			m_transaction.SetOption(option);
		}

		public override void SetOption(FdbTransactionOption option, long value)
		{
			ThrowIfDisposed();
			this.Log.AddOperation(new FdbTransactionLog.SetOptionCommand(option, value), countAsOperation: false);
			m_transaction.SetOption(option, value);
		}

		public override void SetOption(FdbTransactionOption option, string value)
		{
			ThrowIfDisposed();
			this.Log.AddOperation(new FdbTransactionLog.SetOptionCommand(option, value), countAsOperation: false);
			m_transaction.SetOption(option, value);
		}

		#endregion

	}

}
