#region Copyright (c) 2023-2024 SnowBank SAS, (c) 2005-2023 Doxense SAS
// All rights reserved.
// 
// Redistribution and use in source and binary forms, with or without
// modification, are permitted provided that the following conditions are met:
// 	* Redistributions of source code must retain the above copyright
// 	  notice, this list of conditions and the following disclaimer.
// 	* Redistributions in binary form must reproduce the above copyright
// 	  notice, this list of conditions and the following disclaimer in the
// 	  documentation and/or other materials provided with the distribution.
// 	* Neither the name of SnowBank nor the
// 	  names of its contributors may be used to endorse or promote products
// 	  derived from this software without specific prior written permission.
// 
// THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS "AS IS" AND
// ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED
// WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE
// DISCLAIMED. IN NO EVENT SHALL SNOWBANK SAS BE LIABLE FOR ANY
// DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES
// (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES;
// LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND
// ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT
// (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS
// SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
#endregion

namespace FoundationDB.Filters.Logging
{
	using System;
	using System.Collections.Concurrent;
	using System.Diagnostics;
	using System.Globalization;
	using System.Reflection;
	using System.Runtime.CompilerServices;
	using System.Text;
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

		/// <summary>Capture all the stack traces.</summary>
		/// <remarks>This is a shortcut for <see cref="RecordCreationStackTrace"/> | <see cref="RecordOperationStackTrace"/></remarks>
		WithStackTraces = RecordCreationStackTrace | RecordOperationStackTrace,
	}

	/// <summary>Container that logs all operations performed by a transaction</summary>
	public sealed partial class FdbTransactionLog
	{
		private int m_step;

		private int m_operations;
		private int m_readSize;
		private int m_writeSize;

		/// <summary>Create an empty log for a newly created transaction</summary>
		public FdbTransactionLog(FdbLoggingOptions options)
		{
			this.Options = options;
			this.Commands = new ConcurrentQueue<Command>();

			if (this.ShouldCaptureTransactionStackTrace)
			{
				this.CallSite = CaptureStackTrace(2);
			}
		}

		/// <summary>Id of the logged transaction</summary>
		public int Id { get; private set; }

		/// <summary>Logging options for this log</summary>
		public FdbLoggingOptions Options { get; private set; }

		/// <summary>True if the transaction is Read Only</summary>
		public bool IsReadOnly { get; private set; }

		/// <summary>StackTrace of the method that created this transaction</summary>
		/// <remarks>Only if the <see cref="FdbLoggingOptions.RecordCreationStackTrace"/> option is set</remarks>
		public StackTrace? CallSite { get; private set; }

		#region Interning...
		
		private byte[] m_buffer = new byte[1024];
		private int m_offset;
		private readonly object m_lock = new object();

		internal Slice Grab(Slice slice)
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

		internal Slice Grab(ReadOnlySpan<byte> slice)
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

		internal Slice[] Grab(Slice[]? slices)
		{
			if (slices == null || slices.Length == 0) return Array.Empty<Slice>();

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

		internal KeySelector Grab(in KeySelector selector)
		{
			return new KeySelector(
				Grab(selector.Key),
				selector.OrEqual,
				selector.Offset
			);
		}

		internal KeySelector[] Grab(KeySelector[]? selectors)
		{
			if (selectors == null || selectors.Length == 0) return Array.Empty<KeySelector>();

			var res = new KeySelector[selectors.Length];
			for (int i = 0; i < selectors.Length; i++)
			{
				res[i] = Grab(in selectors[i]);
			}
			return res;
		}

		#endregion

		internal void Execute<TCommand>(FdbTransaction tr, TCommand cmd, Action<FdbTransaction, TCommand> action)
			where TCommand : FdbTransactionLog.Command
		{
			Exception? error = null;
			BeginOperation(cmd);
			try
			{
				action(tr, cmd);
			}
			catch (Exception e)
			{
				error = e;
				throw;
			}
			finally
			{
				EndOperation(cmd, error);
			}
		}

		internal async Task ExecuteAsync<TCommand>(FdbTransaction tr, TCommand cmd, Func<FdbTransaction, TCommand, Task> lambda, Action<FdbTransaction, TCommand, FdbTransactionLog>? onSuccess = null)
			where TCommand : FdbTransactionLog.Command
		{
			Exception? error = null;
			BeginOperation(cmd);
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
				EndOperation(cmd, error);
				if (error == null) onSuccess?.Invoke(tr, cmd, this);
			}
		}

		internal async Task<TResult> ExecuteAsync<TCommand, TResult>(FdbTransaction tr, TCommand cmd, Func<FdbTransaction, TCommand, Task<TResult>> lambda)
			where TCommand : FdbTransactionLog.Command<TResult>
		{
			Exception? error = null;
			BeginOperation(cmd);
			try
			{
				TResult result = await lambda(tr, cmd).ConfigureAwait(false);
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
				EndOperation(cmd, error);
			}
		}

		internal StackTrace CaptureStackTrace(int numStackFramesToSkip)
		{
#if DEBUG
			const bool NEED_FILE_INFO = true;
#else
			const bool NEED_FILE_INFO = false;
#endif
			return new StackTrace(1 + numStackFramesToSkip, NEED_FILE_INFO);
		}

		/// <summary>Checks if we need to record the stacktrace of the creation of the transaction</summary>
		internal bool ShouldCaptureTransactionStackTrace
		{
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			get => (this.Options & FdbLoggingOptions.RecordCreationStackTrace) != 0;
		}

		internal bool ShouldCaptureOperationStackTrace
		{
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			get => (this.Options & FdbLoggingOptions.RecordOperationStackTrace) != 0;
		}

		/// <summary>Number of operations performed by the transaction</summary>
		public int Operations => m_operations;

		/// <summary>List of all commands processed by the transaction</summary>
		public ConcurrentQueue<Command> Commands { get; private set; }

		/// <summary>Timestamp of the start of transaction</summary>
		public long StartTimestamp { get; private set; }

		/// <summary>Timestamp of the end of transaction</summary>
		public long StopTimestamp { get; private set; }

		/// <summary>Timestamp (UTC) of the start of transaction</summary>
		public DateTimeOffset StartedUtc { get; internal set; }

		/// <summary>Timestamp (UTC) of the end of the transaction</summary>
		public DateTimeOffset? StoppedUtc { get; internal set; }

		/// <summary>Timestamp (UTC) of the last successful commit of the transaction</summary>
		public DateTimeOffset? CommittedUtc { get; internal set; }

		/// <summary>Committed version of the transaction (if a commit was successful)</summary>
		public long? CommittedVersion { get; internal set; }

		/// <summary>Internal step counter of the transaction</summary>
		/// <remarks>This counter is used to detect sequential vs parallel commands</remarks>
		public int Step => m_step;

		/// <summary>Read size of the last commit attempt</summary>
		/// <remarks>This value only account for read commands in the last attempt</remarks>
		public int ReadSize => m_readSize;

		/// <summary>Write size of the last commit attempt</summary>
		/// <remarks>This value only account for write commands in the last attempt</remarks>
		public int WriteSize => m_writeSize;

		/// <summary>Commit size of the last commit attempt</summary>
		/// <remarks>This value only account for write commands in the last attempt</remarks>
		public int CommitSize { get; internal set; }

		/// <summary>Total of the commit size of all attempts performed by this transaction</summary>
		/// <remarks>This value include the size of all previous retry attempts</remarks>
		public int TotalCommitSize { get; internal set; }

		/// <summary>If true, the transaction has completed (either Commit() completed successfully or Dispose was called)</summary>
		public bool Completed { get; private set; }

		/// <summary>Total number of attempts to commit this transaction</summary>
		/// <remarks>This value is increment on each call to Commit()</remarks>
		public int Attempts { get; internal set; }

		/// <summary>Receives the actual value of the VersionStamps generated by this transaction</summary>
		/// <remarks>This value will be non-null only if the last attempt used VersionStamped operations and committed successfully.</remarks>
		public VersionStamp? VersionStamp { get; internal set; }

		internal bool RequiresVersionStamp { get; set; }

		internal static long GetTimestamp() => Stopwatch.GetTimestamp();

		internal TimeSpan GetTimeOffset() => GetDuration(GetTimestamp() - this.StartTimestamp);

		internal static TimeSpan GetDuration(long elapsed) => TimeSpan.FromTicks((long)Math.Round(((double)elapsed / Stopwatch.Frequency) * TimeSpan.TicksPerSecond, MidpointRounding.AwayFromZero));

		/// <summary>Total duration of the transaction</summary>
		/// <remarks>If the transaction has not yet ended, returns the time elapsed since the start.</remarks>
		public TimeSpan TotalDuration => this.StopTimestamp == 0 ? GetTimeOffset() : GetDuration(this.StopTimestamp - this.StartTimestamp);

		/// <summary>Marks the start of the transaction</summary>
		internal void Start(IFdbTransaction trans)
		{
			Contract.Debug.Requires(trans != null);

			this.Id = trans.Id;
			this.IsReadOnly = trans.IsReadOnly;
			this.StartedUtc = DateTimeOffset.UtcNow; //TODO: use a configurable clock?
			this.StartTimestamp = GetTimestamp();
		}

		/// <summary>Marks the end of the transaction</summary>
		internal bool Stop(IFdbTransaction trans)
		{
			Contract.Debug.Requires(trans != null);

			//TODO: verify that the trans is the same one that was passed to Start(..)?
			if (this.Completed)
			{
				return false;
			}

			this.Completed = true;
			this.StopTimestamp = GetTimestamp();
			this.StoppedUtc = DateTimeOffset.UtcNow; //TODO: use a configurable clock?
			return true;
		}

		public void Annotate(string text)
		{
			AddOperation(new LogCommand(text), countAsOperation: false);
		}

		/// <summary>Adds a new already completed command to the log</summary>
		public void AddOperation(Command cmd, bool countAsOperation = true)
		{
			Contract.Debug.Requires(cmd != null);

			var ts = GetTimeOffset();
			int step = Volatile.Read(ref m_step);

			cmd.StartOffset = ts;
			cmd.Step = step;
			cmd.EndOffset = cmd.StartOffset;
			cmd.ThreadId = Environment.CurrentManagedThreadId;
			if (this.ShouldCaptureOperationStackTrace) cmd.CallSite = CaptureStackTrace(1);
			if (countAsOperation) Interlocked.Increment(ref m_operations);
			this.Commands.Enqueue(cmd);
		}

		/// <summary>Start tracking the execution of a new command</summary>
		public void BeginOperation(Command cmd)
		{
			Contract.Debug.Requires(cmd != null);

			var ts = GetTimeOffset();
			int step = Volatile.Read(ref m_step);

			cmd.StartOffset = ts;
			cmd.Step = step;
			cmd.ThreadId = Environment.CurrentManagedThreadId;
			if (this.ShouldCaptureOperationStackTrace) cmd.CallSite = CaptureStackTrace(2);
			if (cmd.ArgumentBytes.HasValue) Interlocked.Add(ref m_writeSize, cmd.ArgumentBytes.Value);
			Interlocked.Increment(ref m_operations);
			this.Commands.Enqueue(cmd);
		}

		/// <summary>Mark the end of the execution of a command</summary>
		public void EndOperation(Command cmd, Exception? error = null)
		{
			Contract.Debug.Requires(cmd != null);

			var ts = GetTimeOffset();
			var step = Interlocked.Increment(ref m_step);

			cmd.EndOffset = ts;
			cmd.EndStep = step;
			cmd.Error = error;
			if (cmd.ResultBytes.HasValue) Interlocked.Add(ref m_readSize, cmd.ResultBytes.Value);
		}

		/// <summary>Generate an ASCII report with all the commands that were executed by the transaction</summary>
		public string GetCommandsReport(bool detailed = false, KeyResolver? keyResolver = null)
		{
			keyResolver ??= KeyResolver.Default;

			var sb = new StringBuilder(1024);

			var cmds = this.Commands.ToArray();

			sb.Append(CultureInfo.InvariantCulture, $"Transaction #{this.Id} ({(this.IsReadOnly ? "read-only" : "read/write")}, {cmds.Length:N0} operations, started {this.StartedUtc.TimeOfDay}Z");
			if (this.StoppedUtc.HasValue)
			{
				sb.Append(CultureInfo.InvariantCulture, $", ended {this.StoppedUtc.Value.TimeOfDay}Z)");
			}
			else
			{
				sb.Append(", did not finish)");
			}

			sb.AppendLine();

			int reads = 0, writes = 0;
			for (int i = 0; i < cmds.Length; i++)
			{
				var cmd = cmds[i];
				if (detailed)
				{
					sb.Append(CultureInfo.InvariantCulture, $"{cmd.Step,3} - T+{cmd.StartOffset.TotalMilliseconds,7:##0.000} ({cmd.Duration.Ticks / 10.0,7:##,##0} µs) : {cmd.ToString(keyResolver)}");
				}
				else
				{
					sb.Append(CultureInfo.InvariantCulture, $"{cmd.Step,3} : {(cmd.Error != null ? "[FAILED] " : "")}{cmd.ToString(keyResolver)}");
				}
				sb.AppendLine();
				switch (cmd.Mode)
				{
					case Mode.Read: ++reads; break;
					case Mode.Write: ++writes; break;
				}
			}
			if (this.Completed)
			{
				sb.AppendLine(CultureInfo.InvariantCulture, $"Stats: {this.Operations:N0} operations, {reads:N0} reads ({this.ReadSize:N0} bytes), {writes:N0} writes ({this.CommitSize:N0} bytes), {this.TotalDuration.TotalMilliseconds:N2} ms");
			}
			sb.AppendLine();
			return sb.ToString();
		}

		/// <summary>Generate a full ASCII report with the detailed timeline of all the commands that were executed by the transaction</summary>
		public string GetTimingsReport(bool showCommands = false, KeyResolver? keyResolver = null)
		{
			keyResolver ??= KeyResolver.Default;

			var sb = new StringBuilder(1024);

			TimeSpan duration = this.TotalDuration;
			// ideal range is between 10 and 80 chars
			double scale = 0.0005d;
			int width;
			bool flag = false;
			int maxWidth = showCommands ? 80 : 160;
			while ((width = (int)(duration.TotalSeconds / scale)) > maxWidth)
			{
				if (flag) scale *= 5d; else scale *= 2d;
				flag = !flag;
			}

			var cmds = this.Commands.ToArray();

			// Header
			sb.Append(CultureInfo.InvariantCulture, $"Transaction #{this.Id} ({(this.IsReadOnly ? "read-only" : "read/write")}, {cmds.Length} operations, '#' = {(scale * 1000d):N1} ms, started {this.StartedUtc.TimeOfDay}Z [{this.StartedUtc.ToUnixTimeMilliseconds() / 1000.0:F3}]");
			if (this.StoppedUtc.HasValue)
			{
				sb.Append(CultureInfo.InvariantCulture, $", ended {this.StoppedUtc.Value.TimeOfDay}Z [{this.StoppedUtc.Value.ToUnixTimeMilliseconds() / 1000.0:F3}])");
			}
			else
			{
				sb.Append(", did not finish");
			}
			sb.AppendLine();

			if (cmds.Length > 0)
			{
				var bar = new string('─', width + 2);
				sb.AppendLine(CultureInfo.InvariantCulture, $"┌  oper. ┬{bar}┬──── start ──── end ── duration ──┬─ sent  recv ┐");

				// look for the timestamps of the first and last commands
				var first = TimeSpan.Zero;
				foreach (Command cmd in cmds)
				{
					if (cmd.Op == Operation.Log) continue;
					first = cmd.StartOffset;
					break;
				}
				for(int i = cmds.Length - 1; i >= 0; i--)
				{
					if (cmds[i].Op == Operation.Log) continue;
					var endOffset = cmds[i].EndOffset;
					if (endOffset.HasValue) duration = endOffset.Value;
					break;
				}
				duration -= first;

				int step = -1;
				bool previousWasOnError = false;
				int attempts = 1;
				int charsToSkip = 0;
				foreach (var cmd in cmds)
				{
					if (previousWasOnError)
					{
						sb.AppendLine(CultureInfo.InvariantCulture, $"├────────┼{bar}┼──────────────────────────────────┼─────────────┤ == Attempt #{(++attempts):N0} == {(this.StartedUtc + cmd.StartOffset).TimeOfDay}Z ({(this.StartedUtc + cmd.StartOffset).ToUnixTimeMilliseconds() / 1000.0:F3})");
					}

					long ticks = cmd.Duration.Ticks;
					string w = GetFancyGraph(width, (cmd.StartOffset - first).Ticks, ticks, duration.Ticks, charsToSkip);

					if (ticks > 0)
					{
						sb.Append(CultureInfo.InvariantCulture, $"│{(cmd.Step == step ? ":" : " ")}{cmd.Step,-3:##0}{(cmd.Error != null ? "!" : " ")}{cmd.ShortName,2}{(ticks >= TimeSpan.TicksPerMillisecond * 10 ? '*' : ticks >= TimeSpan.TicksPerMillisecond ? '°' : ' ')}│ {w} │ T+{cmd.StartOffset.TotalMilliseconds,7:##0.000} ~ {(cmd.EndOffset ?? TimeSpan.Zero).TotalMilliseconds,7:##0.000} ({ticks / 10.0,7:##,##0} µs) │ {cmd.ArgumentBytes,5} {cmd.ResultBytes,5} │ {(showCommands ? cmd.ToString(keyResolver) : string.Empty)}");
					}
					else
					{ // annotation
						sb.Append(CultureInfo.InvariantCulture, $"│{(cmd.Step == step ? ":" : " ")}{cmd.Step,-3:##0}{(cmd.Error != null ? "!" : " ")}{cmd.ShortName,2} │ {w} │ T+{cmd.StartOffset.TotalMilliseconds,7:##0.000}                        │     -     - │ {(showCommands ? cmd.ToString(keyResolver) : string.Empty)}");
					}

					if (showCommands && cmd.CallSite != null)
					{
						var f = GetFirstInterestingStackFrame(cmd.CallSite);
						if (f != null)
						{
							var m = f.GetMethod()!;
							string name = GetUserFriendlyMethodName(m);
							sb.Append(" // ").Append(name);
							var fn = f.GetFileName();
							if (fn != null) sb.Append(CultureInfo.InvariantCulture, $" at {fn}:{f.GetFileLineNumber()}");
						}
					}

					sb.AppendLine();

					previousWasOnError = cmd.Op == Operation.OnError;
					if (previousWasOnError)
					{
						charsToSkip = (int)Math.Floor(1.0d * width * (cmd.EndOffset ?? TimeSpan.Zero).Ticks / duration.Ticks);
					}

					step = cmd.Step;
				}

				sb.AppendLine(CultureInfo.InvariantCulture, $"└────────┴{bar}┴──────────────────────────────────┴─────────────┘");

				// Footer
				if (this.Completed)
				{
					sb.Append("> ");
					flag = false;
					if (this.ReadSize > 0)
					{
						sb.Append(CultureInfo.InvariantCulture, $"Read {this.ReadSize:N0} bytes");
						flag = true;
					}
					if (this.CommitSize > 0)
					{
						if (flag) sb.Append(" and ");
						sb.Append(CultureInfo.InvariantCulture, $"Committed {this.CommitSize:N0} bytes");
						if (this.VersionStamp != null) sb.Append(", used VersionStamp ").Append(this.VersionStamp.Value.ToString());
						flag = true;
					}
					if (!flag) sb.Append("Completed");
					sb.AppendLine(CultureInfo.InvariantCulture, $" in {this.TotalDuration.TotalMilliseconds:N3} ms and {attempts:N0} attempt(s)");
				}
			}
			else
			{ // empty transaction
				sb.AppendLine(CultureInfo.InvariantCulture, $"> Completed after {this.TotalDuration.TotalMilliseconds:N3} ms without performing any operation");
			}
			return sb.ToString();
		}

		private static StackFrame? GetFirstInterestingStackFrame(StackTrace? st)
		{
			if (st == null) return null;
			var self = typeof (Fdb).Module;
			for (int k = 0; k < st.FrameCount; k++)
			{
				var f = st.GetFrame(k);
				var m = f?.GetMethod();
				if (m == null) continue;

				var t = m.DeclaringType;
				if (t == null) continue;

				// discard any method in this assembly
				if (t.Module == self) continue;
				// discard any NETFX method (async state machines, threadpool, ...)
				if (t.Namespace!.StartsWith("System.", StringComparison.Ordinal)) continue;
				// discard any compiler generated state machine
				return f;
			}
			return null;
		}

		private static string GetUserFriendlyMethodName(MethodBase m)
		{
			Contract.Debug.Requires(m != null);
			var t = m.DeclaringType;
			Contract.Debug.Assert(t != null);

			if (m.Name == "MoveNext")
			{ // compiler generated state machine?

				// look for "OriginalType.<MethodName>d__123.MoveNext()", and replace it with "OriginalType.MethodName()"
				int p;
				if (t.Name.StartsWith("<", StringComparison.Ordinal)
				    && (p = t.Name.IndexOf('>')) > 0
					&& t.DeclaringType != null)
				{
					return t.DeclaringType.Name + "." + t.Name.Substring(1, p - 1) + "()";
				}
			}

			return t.Name + "." + m.Name + "()";
		}

		private static char GetFancyChar(int pos, int count, double start, double end, bool skip)
		{
			double cb = 1.0 * pos / count;
			double ce = 1.0 * (pos + 1) / count;

			if (cb >= end) return ' ';
			if (ce < start) return skip ? '°' : '_';

			double x = count * (Math.Min(ce, end) - Math.Max(cb, start));
			if (x < 0) x = 0;
			if (x > 1) x = 1;

			int p = (int)Math.Round(x * 10, MidpointRounding.AwayFromZero);
			return "`.:;+=xX$&#"[p];
		}

		private static string GetFancyGraph(int width, long offset, long duration, long total, int skip)
		{
			double begin = 1.0d * offset / total;
			double end = 1.0d * (offset + duration) / total;

			var tmp = new char[width];
			for (int i = 0; i < tmp.Length; i++)
			{
				tmp[i] = GetFancyChar(i, tmp.Length, begin, end, i < skip);
			}
			return new string(tmp);
		}

		/// <summary>List of all operation types supported by a transaction</summary>
		public enum Operation
		{
			Invalid = 0,

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
			CheckValue,
			Watch,

			GetReadVersion,
			Commit,
			Cancel,
			Reset,
			OnError,
			SetOption,
			GetVersionStamp,
			GetAddressesForKey,
			GetRangeSplitPoints,
			GetEstimatedRangeSizeBytes,
			GetApproximateSize,

			Log,
		}

		/// <summary>Categories of operations supported by a transaction</summary>
		public enum Mode
		{
			/// <summary>Invalid mode</summary>
			Invalid = 0,
			/// <summary>Operation that reads keys and/or values from the database</summary>
			Read,
			/// <summary>Operation that writes or clears keys from the database</summary>
			Write,
			/// <summary>Operation that changes the state or behavior of the transaction</summary>
			Meta,
			/// <summary>Operation that watch changes performed in the database, outside of the transaction</summary>
			Watch,
			/// <summary>Comments, annotations, debug output attached to the transaction</summary>
			Annotation
		}

	}

}
