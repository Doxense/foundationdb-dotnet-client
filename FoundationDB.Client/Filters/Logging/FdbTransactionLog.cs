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

namespace FoundationDB.Client.Filters.Logging
{
	using System;
	using System.Collections.Generic;
	using System.Diagnostics;
	using System.Text;
	using System.Threading;

	public sealed partial class FdbTransactionLog
	{
		private int m_step;

		public FdbTransactionLog(IFdbTransaction trans)
		{
			this.Commands = new List<Command>();
		}

		public int Id { get; private set; }

		public int Operations { get; private set; }

		public List<Command> Commands { get; private set; }

		public Stopwatch Clock { get; private set; }

		public DateTimeOffset StartedUtc { get; private set; }

		public DateTimeOffset? CommittedUtc { get; private set; }

		public long ReadVersion { get; private set; }

		public long? CommittedVersion { get; private set; }

		public int Step { get { return m_step; } }

		public int CommitSize { get; internal set; }

		public bool Completed { get; private set; }

		public void Start(IFdbTransaction trans)
		{
			this.Id = trans.Id;
			this.StartedUtc = DateTimeOffset.UtcNow;
			this.Clock = Stopwatch.StartNew();
		}

		public void Stop(IFdbTransaction trans)
		{
			if (!this.Completed)
			{
				this.Completed = true;
				this.Clock.Stop();
			}
		}

		public void AddOperation(Command cmd)
		{
			cmd.StartOffset = this.Clock.ElapsedTicks;
			cmd.Step = Volatile.Read(ref m_step);
			cmd.EndOffset = cmd.StartOffset;
			cmd.ThreadId = Thread.CurrentThread.ManagedThreadId;
			lock (this.Commands)
			{
				if (cmd.Op != Operation.Log) this.Operations++;
				this.Commands.Add(cmd);
			}
		}

		public void BeginOperation(Command cmd)
		{
			cmd.StartOffset = this.Clock.ElapsedTicks;
			cmd.Step = Volatile.Read(ref m_step);
			cmd.ThreadId = Thread.CurrentThread.ManagedThreadId;
			lock (this.Commands)
			{
				this.Operations++;
				this.Commands.Add(cmd);
			}
		}

		public void EndOperation(Command cmd, Exception error = null)
		{
			cmd.EndOffset = this.Clock.ElapsedTicks;
			cmd.Error = error;
			Interlocked.Increment(ref m_step);
		}

		public string GetCommandsReport()
		{
			var sb = new StringBuilder();
			sb.AppendLine("Transaction #" + this.Id.ToString() + " command log:");
			int reads = 0, writes = 0;
			for (int i = 0; i < this.Commands.Count; i++)
			{
				var cmd = this.Commands[i];
				sb.AppendFormat("{0,3}/{1,3} : {2}", i + 1, this.Commands.Count, cmd.ToString());
				sb.AppendLine();
				switch (cmd.Mode)
				{
					case FdbTransactionLog.Mode.Read: ++reads; break;
					case FdbTransactionLog.Mode.Write: ++writes; break;
				}
			}
			sb.AppendLine("Stats: " + this.Operations + " operations (" + reads + " reads, " + writes + " writes), " + this.CommitSize + " committed bytes");
			sb.AppendLine();
			return sb.ToString();
		}

		public string GetTimingsReport(bool showCommands = false)
		{
			long duration = this.Clock.ElapsedTicks;
			var sb = new StringBuilder();
			sb.Append("Transaction #" + this.Id.ToString() + " (" + this.Commands.Count + " operations, started " + this.StartedUtc.ToString("O"));
			if (this.CommittedUtc.HasValue) sb.AppendLine(", ended " + this.CommittedUtc.Value.ToString("O") + ")"); else sb.AppendLine(", did not finish");

			int step = -1;
			bool previousWasOnError = false;
			int attempts = 1;
			foreach (var cmd in this.Commands)
			{
				long ticks = cmd.Duration.Ticks;
				int width = (int)Math.Ceiling(2 * TimeSpan.FromTicks(duration).TotalMilliseconds);
				double r = 1.0d * ticks / duration;
				string w = GetFancyGraph(width, cmd.StartOffset, ticks, duration);

				if (previousWasOnError)
				{
					attempts++;
					sb.AppendLine("|----|-----|-" + new string('-', width) + "-|----------------------------------|-------------| Retrying (#" + (attempts++).ToString() + ")");
				}

				sb.AppendFormat(
					"| {0,2} |{6}{1,3:##0}{7}| {2} | T+{3,7:##0.000} ~ {4,7:##0.000} ({5,7:##,##0} µs) | {8,5} {9,5} | {10}",
					/* 0 */ cmd.ShortName,
					/* 1 */ cmd.Step,
					/* 2 */ w,
					/* 3 */ cmd.StartOffset / 10000.0,
					/* 4 */ (cmd.EndOffset ?? 0) / 10000.0,
					/* 5 */ ticks / 10.0,
					/* 6 */ cmd.Step == step ? ':' : ' ',
					/* 7 */ ticks >= 100000 ? '*' : ticks >= 10000 ? '°' : ' ',
					/* 8 */ cmd.ArgumentBytes,
					/* 9 */ cmd.ResultBytes,
					showCommands ? cmd.ToString() : ""
				);
				sb.AppendLine();

				previousWasOnError = cmd.Op == Operation.OnError;

				step = cmd.Step;
			}
			sb.AppendLine(" Committed " + this.CommitSize.ToString("N0") + " bytes in " + TimeSpan.FromTicks(duration).TotalMilliseconds.ToString("N3") + " ms and " + attempts.ToString() + " attempt(s)");
			return sb.ToString();
		}

		private static char GetFancyChar(int pos, int count, double start, double end)
		{
			double cb = 1.0 * pos / count;
			double ce = 1.0 * (pos + 1) / count;

			if (cb >= end) return ' ';
			if (ce < start) return '_';

			double x = count * (Math.Min(ce, end) - Math.Max(cb, start));
			if (x < 0) x = 0;
			if (x > 1) x = 1;

			int p = (int)Math.Round(x * 10);
			return "`.:;+=xX$&#"[p];
		}

		private static string GetFancyGraph(int width, long offset, long duration, long total)
		{
			double begin = 1.0d * offset / total;
			double end = 1.0d * (offset + duration) / total;

			var tmp = new char[width];
			for (int i = 0; i < tmp.Length; i++)
			{
				tmp[i] = GetFancyChar(i, tmp.Length, begin, end);
			}
			return new string(tmp);
		}

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

			GetReadVersion,
			Commit,
			Cancel,
			Reset,
			OnError,

			Log,
		}

		public enum Mode
		{
			Read,
			Write,
			Meta,
			Watch,
			Annotation
		}

	}

}
