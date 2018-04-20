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

namespace FoundationDB.Filters.Logging
{
	using FoundationDB.Async;
	using FoundationDB.Client;
	using JetBrains.Annotations;
	using System;
	using System.Collections.Generic;
	using System.Threading;
	using System.Threading.Tasks;

	[Flags]
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
	public sealed class FdbLoggedTransaction : FdbTransactionFilter
	{
		private Snapshotted m_snapshotted;

		/// <summary>Log of all operations performed on this transaction</summary>
		public FdbTransactionLog Log {[NotNull] get; private set; }

		/// <summary>Handler that will be called when this transaction commits successfully</summary>
		public Action<FdbLoggedTransaction> Committed { get; private set; }

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
				return new Slice(m_buffer, start, slice.Count);
			}
		}

		private Slice[] Grab(Slice[] slices)
		{
			if (slices == null || slices.Length == 0) return null;

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

		private KeySelector Grab(KeySelector selector)
		{
			return new KeySelector(Grab(selector.Key), selector.OrEqual, selector.Offset);
		}

		private KeySelector[] Grab(KeySelector[] selectors)
		{
			if (selectors == null || selectors.Length == 0) return null;

			var res = new KeySelector[selectors.Length];
			for (int i = 0; i < selectors.Length; i++)
			{
				res[i] = Grab(selectors[i]);
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

		private async Task ExecuteAsync<TCommand>([NotNull] TCommand cmd, [NotNull] Func<IFdbTransaction, TCommand, Task> lambda)
			where TCommand : FdbTransactionLog.Command
		{
			ThrowIfDisposed();
			Exception error = null;
			this.Log.BeginOperation(cmd);
			try
			{
				await lambda(m_transaction, cmd).ConfigureAwait(false);
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
#if NET_4_0
				cmd.Result = Maybe.Error<R>(e);
#else
				cmd.Result = Maybe.Error<TResult>(System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(e));
#endif
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
			Execute(
				new FdbTransactionLog.ResetCommand(),
				(tr, cmd) => tr.Reset()
			);
		}

		public override FdbWatch Watch(Slice key, CancellationToken cancellationToken)
		{
			var cmd = new FdbTransactionLog.WatchCommand(Grab(key));
			this.Log.AddOperation(cmd);
			return m_transaction.Watch(cmd.Key, cancellationToken);
		}

		#endregion

		#region Write...

		public override async Task CommitAsync()
		{
			this.Log.CommitSize = m_transaction.Size;
			this.Log.TotalCommitSize += m_transaction.Size;
			this.Log.Attempts++;

			await ExecuteAsync(
				new FdbTransactionLog.CommitCommand(),
				(_tr, _cmd) => _tr.CommitAsync()
			).ConfigureAwait(false);

			this.Log.CommittedUtc = DateTimeOffset.UtcNow;
			this.Log.CommittedVersion = m_transaction.GetCommittedVersion();
		}

		public override Task OnErrorAsync(FdbError code)
		{
			return ExecuteAsync(
				new FdbTransactionLog.OnErrorCommand(code),
				(_tr, _cmd) => _tr.OnErrorAsync(_cmd.Code)
			);
		}

		public override void Set(Slice key, Slice value)
		{
			Execute(
				new FdbTransactionLog.SetCommand(Grab(key), Grab(value)),
				(_tr, _cmd) => _tr.Set(_cmd.Key, _cmd.Value)
			);
		}

		public override void Clear(Slice key)
		{
			ThrowIfDisposed();
			Execute(
				new FdbTransactionLog.ClearCommand(Grab(key)),
				(_tr, _cmd) => _tr.Clear(_cmd.Key)
			);
		}

		public override void ClearRange(Slice beginKeyInclusive, Slice endKeyExclusive)
		{
			Execute(
				new FdbTransactionLog.ClearRangeCommand(Grab(beginKeyInclusive), Grab(endKeyExclusive)),
				(_tr, _cmd) => _tr.ClearRange(_cmd.Begin, _cmd.End)
			);
		}

		public override void Atomic(Slice key, Slice param, FdbMutationType mutation)
		{
			Execute(
				new FdbTransactionLog.AtomicCommand(Grab(key), Grab(param), mutation),
				(_tr, _cmd) => _tr.Atomic(_cmd.Key, _cmd.Param, _cmd.Mutation)
			);
		}

		public override void AddConflictRange(Slice beginKeyInclusive, Slice endKeyExclusive, FdbConflictRangeType type)
		{
			Execute(
				new FdbTransactionLog.AddConflictRangeCommand(beginKeyInclusive, endKeyExclusive, type),
				(_tr, _cmd) => _tr.AddConflictRange(_cmd.Begin, _cmd.End, _cmd.Type)
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

		public override Task<Slice> GetAsync(Slice key)
		{
			return ExecuteAsync(
				new FdbTransactionLog.GetCommand(Grab(key)),
				(tr, cmd) => tr.GetAsync(key)
			);
		}

		public override Task<Slice> GetKeyAsync(KeySelector selector)
		{
			return ExecuteAsync(
				new FdbTransactionLog.GetKeyCommand(Grab(selector)),
				(tr, cmd) => tr.GetKeyAsync(selector)
			);
		}

		public override Task<Slice[]> GetValuesAsync(Slice[] keys)
		{
			return ExecuteAsync(
				new FdbTransactionLog.GetValuesCommand(Grab(keys)),
				(tr, cmd) => tr.GetValuesAsync(keys)
			);
		}

		public override Task<Slice[]> GetKeysAsync(KeySelector[] selectors)
		{
			return ExecuteAsync(
				new FdbTransactionLog.GetKeysCommand(Grab(selectors)),
				(tr, cmd) => tr.GetKeysAsync(selectors)
			);
		}

		public override Task<FdbRangeChunk> GetRangeAsync(KeySelector beginInclusive, KeySelector endExclusive, FdbRangeOptions options = null, int iteration = 0)
		{
			return ExecuteAsync(
				new FdbTransactionLog.GetRangeCommand(Grab(beginInclusive), Grab(endExclusive), options, iteration),
				(tr, cmd) => tr.GetRangeAsync(beginInclusive, endExclusive, options, iteration)
			);
		}

		public override FdbRangeQuery<KeyValuePair<Slice, Slice>> GetRange(KeySelector beginInclusive, KeySelector endExclusive, FdbRangeOptions options = null)
		{
			ThrowIfDisposed();

			var query = m_transaction.GetRange(beginInclusive, endExclusive, options);
			// this method does not execute immediately, so we don't need to record any operation here, only when GetRangeAsync() is called (by ToListAsync() or any other LINQ operator)
			// we must override the transaction used by the query, so that we are notified when this happens
			return query.UseTransaction(this);
		}

		#endregion

		#region Snapshot...

		public override IFdbReadOnlyTransaction Snapshot
		{
			get
			{
				return m_snapshotted ?? (m_snapshotted = new Snapshotted(this, m_transaction.Snapshot));
			}
		}

		private sealed class Snapshotted : FdbReadOnlyTransactionFilter
		{
			private readonly FdbLoggedTransaction m_parent;

			public Snapshotted([NotNull] FdbLoggedTransaction parent, [NotNull] IFdbReadOnlyTransaction snapshot)
				: base(snapshot)
			{
				m_parent = parent;
			}

			private async Task<R> ExecuteAsync<TCommand, R>([NotNull] TCommand cmd, [NotNull] Func<IFdbReadOnlyTransaction, TCommand, Task<R>> lambda)
				where TCommand : FdbTransactionLog.Command<R>
			{
				m_parent.ThrowIfDisposed();
				Exception error = null;
				cmd.Snapshot = true;
				m_parent.Log.BeginOperation(cmd);
				try
				{
					R result = await lambda(m_transaction, cmd).ConfigureAwait(false);
					cmd.Result = Maybe.Return<R>(result);
					return result;
				}
				catch (Exception e)
				{
					error = e;
#if NET_4_0
					cmd.Result = Maybe.Error<R>(e);
#else
					cmd.Result = Maybe.Error<R>(System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(e));
#endif
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

			public override Task<Slice> GetAsync(Slice key)
			{
				return ExecuteAsync(
					new FdbTransactionLog.GetCommand(m_parent.Grab(key)),
					(tr, cmd) => tr.GetAsync(cmd.Key)
				);
			}

			public override Task<Slice> GetKeyAsync(KeySelector selector)
			{
				return ExecuteAsync(
					new FdbTransactionLog.GetKeyCommand(m_parent.Grab(selector)),
					(tr, cmd) => tr.GetKeyAsync(cmd.Selector)
				);
			}

			public override Task<Slice[]> GetValuesAsync(Slice[] keys)
			{
				return ExecuteAsync(
					new FdbTransactionLog.GetValuesCommand(m_parent.Grab(keys)),
					(tr, cmd) => tr.GetValuesAsync(cmd.Keys)
				);
			}

			public override Task<Slice[]> GetKeysAsync(KeySelector[] selectors)
			{
				return ExecuteAsync(
					new FdbTransactionLog.GetKeysCommand(m_parent.Grab(selectors)),
					(tr, cmd) => tr.GetKeysAsync(cmd.Selectors)
				);
			}

			public override Task<FdbRangeChunk> GetRangeAsync(KeySelector beginInclusive, KeySelector endExclusive, FdbRangeOptions options = null, int iteration = 0)
			{
				return ExecuteAsync(
					new FdbTransactionLog.GetRangeCommand(m_parent.Grab(beginInclusive), m_parent.Grab(endExclusive), options, iteration),
					(tr, cmd) => tr.GetRangeAsync(cmd.Begin, cmd.End, cmd.Options, cmd.Iteration)
				);
			}

			public override FdbRangeQuery<KeyValuePair<Slice, Slice>> GetRange(KeySelector beginInclusive, KeySelector endExclusive, FdbRangeOptions options = null)
			{
				m_parent.ThrowIfDisposed();
				var query = m_transaction.GetRange(beginInclusive, endExclusive, options);
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
