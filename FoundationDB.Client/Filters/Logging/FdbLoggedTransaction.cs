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

namespace FoundationDB.Client.Filters
{
	using FoundationDB.Async;
	using System;
	using System.Collections.Generic;
	using System.Threading.Tasks;

	/// <summary>Transaction filter that logs and measure all operations performed on the underlying transaction</summary>
	public sealed class FdbLoggedTransaction : FdbTransactionFilter
	{

		/// <summary>Log of all operations performed on this transaction</summary>
		public FdbTransactionLog Log { get; private set; }

		/// <summary>Handler that will be called when this transaction commits successfully</summary>
		public Action<FdbLoggedTransaction> OnCommitted { get; private set; }

		public FdbLoggedTransaction(IFdbTransaction trans, bool ownsTransaction, Action<FdbLoggedTransaction> onCommitted)
			: base(trans, false, ownsTransaction)
		{
			this.Log = new FdbTransactionLog(this);
			this.OnCommitted = onCommitted;
		}

		#region Data interning...

		private byte[] m_buffer = new byte[1024];
		private int m_offset;

		private Slice Grab(Slice slice)
		{
			if (slice.IsNullOrEmpty) return slice.IsNull ? Slice.Nil : Slice.Empty;

			//TODO: locking!

			int remaining = m_buffer.Length - m_offset;

			if (slice.Count > remaining)
			{ // not enough ?
				if (slice.Count >= 2048)
				{
					return slice.Memoize();
				}
				m_buffer = new byte[4096];
				m_offset = 0;
				remaining = m_buffer.Length;
			}

			int start = m_offset;
			slice.CopyTo(m_buffer, m_offset);
			m_offset += slice.Count;
			return new Slice(m_buffer, start, slice.Count);
		}

		private Slice[] Grab(Slice[] slices)
		{
			//TODO: locking!

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

		private FdbKeySelector Grab(FdbKeySelector selector)
		{
			return new FdbKeySelector(Grab(selector.Key), selector.OrEqual, selector.Offset);
		}

		private FdbKeySelector[] Grab(FdbKeySelector[] selectors)
		{
			var res = new FdbKeySelector[selectors.Length];
			for (int i = 0; i < selectors.Length; i++)
			{
				res[i] = Grab(selectors[i]);
			}
			return res;
		}

		#endregion

		#region Instrumentation...

		private void Execute<TCommand>(TCommand cmd, Action<IFdbTransaction, TCommand> action)
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

		private async Task ExecuteAsync<TCommand>(TCommand cmd, Func<IFdbTransaction, TCommand, Task> lambda)
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

		private async Task<R> ExecuteAsync<TCommand, R>(TCommand cmd, Func<IFdbTransaction, TCommand, Task<R>> lambda)
			where TCommand : FdbTransactionLog.Command<R>
		{
			ThrowIfDisposed();
			Exception error = null;
			this.Log.BeginOperation(cmd);
			try
			{
				R result = await lambda(m_transaction, cmd).ConfigureAwait(false);
				cmd.Result = Maybe.Return<R>(result);
				return result;
			}
			catch (Exception e)
			{
				error = e;
				cmd.Result = Maybe.Error<R>(e);
				throw;
			}
			finally
			{
				this.Log.EndOperation(cmd, error);
			}
		}

		public override void Cancel()
		{
			Execute(
				new FdbTransactionLog.CancelCommand(),
				(_tr, _cmd) => _tr.Cancel()
			);
		}

		public override void Reset()
		{
			Execute(
				new FdbTransactionLog.ResetCommand(),
				(_tr, _cmd) => _tr.Reset()
			);
		}

		public override async Task CommitAsync()
		{
			await ExecuteAsync(
				new FdbTransactionLog.CommitCommand(),
				(_tr, _cmd) => _tr.CommitAsync()
			);
			this.Log.Clock.Stop();
			if (this.OnCommitted != null)
			{
				this.OnCommitted(this);
			}
		}

		public override Task OnErrorAsync(FdbError code)
		{
			ThrowIfDisposed();
			return ExecuteAsync(
				new FdbTransactionLog.OnErrorCommand(code),
				(_tr, _cmd) => _tr.OnErrorAsync(_cmd.Code)
			);
		}

		public override void Set(Slice key, Slice value)
		{
			ThrowIfDisposed();
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
			ThrowIfDisposed();
			Execute(
				new FdbTransactionLog.ClearRangeCommand(Grab(beginKeyInclusive), Grab(endKeyExclusive)),
				(_tr, _cmd) => _tr.ClearRange(_cmd.Begin, _cmd.End)
			);
		}

		public override void Atomic(Slice key, Slice param, FdbMutationType mutation)
		{
			ThrowIfDisposed();
			Execute(
				new FdbTransactionLog.AtomicCommand(Grab(key), Grab(param), mutation),
				(_tr, _cmd) => _tr.Atomic(_cmd.Key, _cmd.Param, _cmd.Mutation)
			);
		}

		public override Task<Slice> GetAsync(Slice key)
		{
			return ExecuteAsync(
				new FdbTransactionLog.GetCommand(Grab(key)),
				(_tr, _cmd) => _tr.GetAsync(_cmd.Key)
			);
		}

		public override Task<Slice> GetKeyAsync(FdbKeySelector selector)
		{
			return ExecuteAsync(
				new FdbTransactionLog.GetKeyCommand(Grab(selector)),
				(_tr, _cmd) => _tr.GetKeyAsync(_cmd.Selector)
			);
		}

		public override Task<Slice[]> GetValuesAsync(Slice[] keys)
		{
			return ExecuteAsync(
				new FdbTransactionLog.GetValuesCommand(Grab(keys)),
				(_tr, _cmd) => _tr.GetValuesAsync(_cmd.Keys)
			);
		}

		public override Task<Slice[]> GetKeysAsync(FdbKeySelector[] selectors)
		{
			return ExecuteAsync(
				new FdbTransactionLog.GetKeysCommand(Grab(selectors)),
				(_tr, _cmd) => _tr.GetKeysAsync(_cmd.Selectors)
			);
		}

		public override Task<FdbRangeChunk> GetRangeAsync(FdbKeySelector beginInclusive, FdbKeySelector endExclusive, FdbRangeOptions options = null, int iteration = 0)
		{
			return ExecuteAsync(
				new FdbTransactionLog.GetRangeCommand(Grab(beginInclusive), Grab(endExclusive), options, iteration),
				(_tr, _cmd) => _tr.GetRangeAsync(_cmd.Begin, _cmd.End, _cmd.Options, _cmd.Iteration)
			);
		}

		public override FdbRangeQuery<KeyValuePair<Slice, Slice>> GetRange(FdbKeySelector beginInclusive, FdbKeySelector endExclusive, FdbRangeOptions options = null)
		{
			ThrowIfDisposed();
			var query = m_transaction.GetRange(beginInclusive, endExclusive, options);
			return query.UseTransaction(this);
		}

		public override void AddConflictRange(Slice beginKeyInclusive, Slice endKeyExclusive, FdbConflictRangeType type)
		{
			ThrowIfDisposed();
			Execute(
				new FdbTransactionLog.AddConflictRangeCommand(beginKeyInclusive, endKeyExclusive, type),
				(_tr, _cmd) => _tr.AddConflictRange(_cmd.Begin, _cmd.End, _cmd.Type)
			);
		}

		#endregion


	}

}
