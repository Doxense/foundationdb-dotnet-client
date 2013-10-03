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
	using System;
	using System.Collections.Generic;
	using System.Diagnostics;
	using System.Threading.Tasks;

	public sealed class LoggingTransactionFilter : FdbTransactionFilter
	{

		public TransactionLog Log { get; private set; }

		public LoggingTransactionFilter(IFdbTransaction trans, bool ownsTransaction)
			: base(trans, false, ownsTransaction)
		{
			this.Log = new TransactionLog(this);
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
			for(int i=0;i<selectors.Length;i++)
			{
				res[i] = Grab(selectors[i]);
			}
			return res;
		}

		#endregion

		#region Instrumentation...

		private void Execute<TCommand>(TCommand cmd, Action<IFdbTransaction, TCommand> action)
			where TCommand : Command
		{
			ThrowIfDisposed();
			Exception error = null;
			this.Log.Start(cmd);
			try
			{
				action(this, cmd);
			}
			catch (Exception e)
			{
				error = e;
				throw;
			}
			finally
			{
				this.Log.End(cmd, error);
			}
		}

		private async Task<R> ExecuteAsync<TCommand, R>(TCommand cmd, Func<IFdbTransaction, TCommand, Task<R>> lambda)
			where TCommand : Command
		{
			ThrowIfDisposed();
			Exception error = null;
			this.Log.Start(cmd);
			try
			{
				return await lambda(this, cmd).ConfigureAwait(false);
			}
			catch (Exception e)
			{
				error = e;
				throw;
			}
			finally
			{
				this.Log.End(cmd, error);
			}
		}

		public override void Cancel()
		{
			Execute(
				new CancelCommand(),
				(_tr, _cmd) => _tr.Cancel()
			);
		}

		public override void Reset()
		{
			Execute(
				new ResetCommand(),
				(_tr, _cmd) => _tr.Reset()
			);
		}

		public override Task CommitAsync()
		{
			return ExecuteAsync(
				new CommitCommand(),
				async (_tr, _cmd) => { await _tr.CommitAsync().ConfigureAwait(false); return default(object); }
			);
		}

		public override void Set(Slice key, Slice value)
		{
			ThrowIfDisposed();
			Execute(
				new SetCommand(Grab(key), Grab(value)),
				(_tr, _cmd) => _tr.Set(_cmd.Key, _cmd.Value)
			);
		}

		public override void Clear(Slice key)
		{
			ThrowIfDisposed();
			Execute(
				new ClearCommand(Grab(key)),
				(_tr, _cmd) => _tr.Clear(_cmd.Key)
			);
		}

		public override void ClearRange(Slice beginKeyInclusive, Slice endKeyExclusive)
		{
			ThrowIfDisposed();
			Execute(
				new ClearRangeCommand(Grab(beginKeyInclusive), Grab(endKeyExclusive)),
				(_tr, _cmd) => _tr.ClearRange(_cmd.Begin, _cmd.End)
			);
		}

		public override void Atomic(Slice key, Slice param, FdbMutationType mutation)
		{
			ThrowIfDisposed();
			Execute(
				new AtomicCommand(Grab(key), Grab(param), mutation),
				(_tr, _cmd) => _tr.Atomic(_cmd.Key, _cmd.Param, _cmd.Mutation)
			);
		}

		public override Task<Slice> GetAsync(Slice key)
		{
			return ExecuteAsync(
				new GetCommand(Grab(key)),
				(_tr, _cmd) => _tr.GetAsync(_cmd.Key)
			);
		}

		public override Task<Slice> GetKeyAsync(FdbKeySelector selector)
		{
			return ExecuteAsync(
				new GetKeyCommand(Grab(selector)),
				(_tr, _cmd) => _tr.GetKeyAsync(_cmd.Selector)
			);
		}

		public override Task<Slice[]> GetValuesAsync(Slice[] keys)
		{
			return ExecuteAsync(
				new GetValuesCommand(Grab(keys)),
				(_tr, _cmd) => _tr.GetValuesAsync(_cmd.Keys)
			);
		}

		public override Task<Slice[]> GetKeysAsync(FdbKeySelector[] selectors)
		{
			return ExecuteAsync(
				new GetKeysCommand(Grab(selectors)),
				(_tr, _cmd) => _tr.GetKeysAsync(_cmd.Selectors)
			);
		}

		public override Task<FdbRangeChunk> GetRangeAsync(FdbKeySelector beginInclusive, FdbKeySelector endExclusive, FdbRangeOptions options = null, int iteration = 0)
		{
			return ExecuteAsync(
				new GetRangeCommand(Grab(beginInclusive), Grab(endExclusive), options, iteration),
				(_tr, _cmd) => _tr.GetRangeAsync(_cmd.Begin, _cmd.End, _cmd.Options, _cmd.Iteration)
			);
		}

		#endregion

		public enum Operation
		{
			Set,
			Clear,
			ClearRange,
			Atomic,
			AddConflictRange,
			Get,
			GetKey,
			GetValues,
			GetKeys,
			GetRange,
			Watch,

			Commit,
			Cancel,
			Reset,
		}

		public enum Mode
		{
			Read,
			Write,
			Meta,
			Watch
		}

		public class TransactionLog
		{
			public TransactionLog(IFdbTransaction trans)
			{
				this.Id = trans.Id;
				this.Commands = new List<Command>();
				this.Clock = Stopwatch.StartNew();
			}

			public int Id { get; private set; }

			public List<Command> Commands { get; private set; }

			public Stopwatch Clock { get; private set; }

			public DateTimeOffset StartedUtc { get; private set; }

			public DateTimeOffset? CommittedUtc { get; private set; }

			public long ReadVersion { get; private set; }

			public long? CommittedVersion { get; private set; }

			public void Start(Command cmd)
			{
				cmd.StartOffset = this.Clock.ElapsedTicks;
				lock (this.Commands)
				{
					this.Commands.Add(cmd);
				}
			}

			public void End(Command cmd, Exception error = null)
			{
				cmd.EndOffset = this.Clock.ElapsedTicks;
				cmd.Error = error;
			}

		}

		public abstract class Command
		{
			protected Command()
			{

			}

			public virtual Mode Mode
			{
				get
				{
					switch(this.Op)
					{
						case Operation.Set:
						case Operation.Clear:
						case Operation.ClearRange:
						case Operation.Atomic:
							return LoggingTransactionFilter.Mode.Write;

						case Operation.Get:
						case Operation.GetKey:
						case Operation.GetValues:
						case Operation.GetKeys:
						case Operation.GetRange:
							return LoggingTransactionFilter.Mode.Read;

						case Operation.Reset:
						case Operation.Cancel:
						case Operation.AddConflictRange:
						case Operation.Commit:
							return LoggingTransactionFilter.Mode.Meta;

						case Operation.Watch:
							return LoggingTransactionFilter.Mode.Watch;

						default:
							throw new NotImplementedException("Fixme!");
					}
				}
			}
			public abstract Operation Op { get; }
			public long StartOffset { get; internal set; }
			public long EndOffset { get; internal set; }
			public Exception Error { get; internal set; }
		}

		public class SetCommand : Command
		{
			public Slice Key { get; private set; }
			public Slice Value { get; private set; }

			public override Operation Op { get { return Operation.Set; } }

			public SetCommand(Slice key, Slice value)
			{
				this.Key = key;
				this.Value = value;
			}
		}

		public class ClearCommand : Command
		{
			public Slice Key { get; private set; }

			public override Operation Op { get { return Operation.Clear; } }

			public ClearCommand(Slice key)
			{
				this.Key = key;
			}

		}

		public class ClearRangeCommand : Command
		{
			public Slice Begin { get; private set; }
			public Slice End { get; private set; }

			public override Operation Op { get { return Operation.ClearRange; } }

			public ClearRangeCommand(Slice begin, Slice end)
			{
				this.Begin = begin;
				this.End = end;
			}

		}

		public class AtomicCommand : Command
		{
			public Slice Key { get; private set; }
			public Slice Param { get; private set; }
			public FdbMutationType Mutation { get; private set; }

			public override Operation Op { get { return Operation.Atomic; } }

			public AtomicCommand(Slice key, Slice param, FdbMutationType mutation)
			{
				this.Key = key;
				this.Param = param;
				this.Mutation = mutation;
			}

		}

		public class GetCommand : Command
		{
			public Slice Key { get; private set; }

			public override Operation Op { get { return Operation.Get; } }

			public GetCommand(Slice key)
			{
				this.Key = key;
			}

		}

		public class GetKeyCommand : Command
		{
			public FdbKeySelector Selector { get; private set; }

			public override Operation Op { get { return Operation.GetKey; } }

			public GetKeyCommand(FdbKeySelector selector)
			{
				this.Selector = selector;
			}

		}

		public class GetValuesCommand : Command
		{
			public Slice[] Keys { get; private set; }

			public override Operation Op { get { return Operation.GetValues; } }

			public GetValuesCommand(Slice[] keys)
			{
				this.Keys = keys;
			}

		}

		public class GetKeysCommand : Command
		{
			public FdbKeySelector[] Selectors { get; private set; }

			public override Operation Op { get { return Operation.GetKeys; } }

			public GetKeysCommand(FdbKeySelector[] selectors)
			{
				this.Selectors = selectors;
			}

		}

		public class GetRangeCommand : Command
		{
			public FdbKeySelector Begin { get; private set; }
			public FdbKeySelector End { get; private set; }
			public FdbRangeOptions Options { get; private set; }
			public int Iteration { get; private set; }

			public override Operation Op { get { return Operation.Get; } }

			public GetRangeCommand(FdbKeySelector begin, FdbKeySelector end, FdbRangeOptions options, int iteration)
			{
				this.Begin = begin;
				this.End = end;
				this.Options = options;
				this.Iteration = iteration;
			}

		}

		public class CancelCommand : Command
		{
			public override Operation Op { get { return Operation.Cancel; } }
		}

		public class ResetCommand : Command
		{
			public override Operation Op { get { return Operation.Reset; } }
		}

		public class CommitCommand : Command
		{
			public override Operation Op { get { return Operation.Commit; } }
		}

	}

}
