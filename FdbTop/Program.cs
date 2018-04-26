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

// ReSharper disable CompareOfFloatsByEqualityOperator
namespace FdbTop
{
	using System;
	using System.Diagnostics;
	using System.Globalization;
	using System.Linq;
	using System.Text;
	using System.Threading;
	using System.Threading.Tasks;
	using FoundationDB.Client;
	using FoundationDB.Client.Status;

	public static class Program
	{
		private static string ClusterPath;

		public static void Main(string[] args)
		{
			//TODO: move this to the main, and add a command line argument to on/off ?

			try
			{
				if (Console.LargestWindowWidth > 0 && Console.LargestWindowHeight > 0)
				{
					Console.WindowWidth = 160;
					Console.WindowHeight = 60;
				}
			}
			catch
			{
				Console.Error.WriteLine("This tool requires cannot run in a console smaller than 160 characters.");
				Console.Error.WriteLine("Either increase the screen resolution, or reduce the font size of your console.");
				Environment.ExitCode = -1;
				return;
			}

			string title = Console.Title;
			try
			{

				if (args.Length > 0)
				{
					ClusterPath = args[0];
				}

				Console.Title = "fdbtop";

				//note: always use the latest version available
				Fdb.UseApiVersion(Fdb.GetMaxSafeApiVersion());
				Fdb.Start();
				using (var go = new CancellationTokenSource())
				{
					MainAsync(args, go.Token).GetAwaiter().GetResult();
				}
			}
			catch(Exception e)
			{
				Trace.WriteLine(e.ToString());
				Console.Error.WriteLine("CRASHED! " + e);
				Environment.ExitCode = -1;
			}
			finally
			{
				Console.Title = title;
				Console.CursorVisible = true;
				Fdb.Stop();
			}
		}

		private static async Task MainAsync(string[] args, CancellationToken cancel)
		{
			Console.CursorVisible = false;
			Console.TreatControlCAsInput = true;
			try
			{
				DateTime now = DateTime.MinValue;
				DateTime lap = DateTime.MinValue;
				DateTime next = DateTime.MinValue;
				bool repaint = true;
				bool exit = false;
				bool saveNext = false;
				bool updated = false;
				const double FAST = 1;
				const double SLOW = 5;
				double speed = FAST;

				DisplayMode mode = DisplayMode.Metrics;

				using (var db = await Fdb.OpenAsync(ClusterPath, "DB", cancel))
				{
					db.DefaultTimeout = 10000;

					while (!exit && !cancel.IsCancellationRequested)
					{
						now = DateTime.UtcNow;

						if (Console.KeyAvailable)
						{
							var k = Console.ReadKey();
							switch (k.Key)
							{
								case ConsoleKey.Escape:
								{ // [ESC]
									exit = true;
									break;
								}

								case ConsoleKey.C:
								{
									if (k.Modifiers.HasFlag(ConsoleModifiers.Control))
									{ // CTRL-C
										exit = true;
									}
									break;
								}

								case ConsoleKey.F:
								{ // [F]ast (on/off)
									if (speed == FAST) speed = SLOW; else speed = FAST;
									break;
								}

								case ConsoleKey.H:
								case ConsoleKey.F1:
								{
									mode = DisplayMode.Help;
									updated = repaint = true;
									break;
								}

								case ConsoleKey.L:
								{ // [L]atency
									mode = DisplayMode.Latency;
									updated = repaint = true;
									break;
								}

								case ConsoleKey.M:
								{ // [M]etrics
									mode = DisplayMode.Metrics;
									updated = repaint = true;
									break;
								}

								case ConsoleKey.P:
								{ // [P]rocesses
									mode = DisplayMode.Processes;
									updated = repaint = true;
									break;
								}

								case ConsoleKey.Q:
								{ // [Q]uit
									exit = true;
									break;
								}

								case ConsoleKey.R:
								{ // [R]eset
									lap = now;
									next = now;
									History.Clear();
									updated = repaint = true;
									break;
								}

								case ConsoleKey.S:
								{ // [S]napshot
									saveNext = true;
									break;
								}

								case ConsoleKey.T:
								{ // [T]ransactions
									mode = DisplayMode.Transactions;
									updated = repaint = true;
									break;
								}

							}
						}

						var status = await Fdb.System.GetStatusAsync(db, cancel);

						if (saveNext)
						{
							System.IO.File.WriteAllText(@".\\status.json", status.RawText);
							saveNext = false;
						}

						if (lap == DateTime.MinValue)
						{
							lap = now;
							next = now.AddSeconds(speed);
						}

						if (now >= next)
						{
							var metric = new HistoryMetric
							{
								Available = status.ReadVersion > 0,

								LocalTime = now - lap,
								Timestamp = status.Cluster.ClusterControllerTimestamp,
								ReadVersion = status.ReadVersion,

								ReadsPerSecond = status.Cluster.Workload.Operations.Reads.Hz,
								WritesPerSecond = status.Cluster.Workload.Operations.Writes.Hz,
								WrittenBytesPerSecond = status.Cluster.Workload.Bytes.Written.Hz,

								TransStarted = status.Cluster.Workload.Transactions.Started.Hz,
								TransCommitted = status.Cluster.Workload.Transactions.Committed.Hz,
								TransConflicted = status.Cluster.Workload.Transactions.Conflicted.Hz,

								LatencyCommit = status.Cluster.Latency.CommitSeconds,
								LatencyRead = status.Cluster.Latency.ReadSeconds,
								LatencyStart = status.Cluster.Latency.TransactionStartSeconds,
							};
							History.Enqueue(metric);
							updated = true;

							now = DateTime.UtcNow;
							while (next < now) next = next.AddSeconds(speed);
						}

						if (updated)
						{
							var metric = History.LastOrDefault();
							switch (mode)
							{
								case DisplayMode.Metrics:
								{
									ShowMetricsScreen(status, metric, repaint);
									break;
								}
								case DisplayMode.Latency:
								{
									ShowLatencyScreen(status, metric, repaint);
									break;
								}
								case DisplayMode.Transactions:
								{
									ShowTransactionScreen(status, metric, repaint);
									break;
								}
								case DisplayMode.Processes:
								{
									ShowProcessesScreen(status, metric, repaint);
									break;
								}
								case DisplayMode.Help:
								{
									ShowHelpScreen(repaint);
									break;
								}
							}
							repaint = false;
							updated = false;
						}

						await Task.Delay(100, cancel);
					}

				}
			}
			finally
			{
				Console.CursorVisible = true;
				Console.ResetColor();
				Console.Clear();
			}
		}

		private static readonly DateTime Epoch = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);

		private const int HistoryCapacity = 50;
		private static readonly RingBuffer<HistoryMetric> History = new RingBuffer<HistoryMetric>(HistoryCapacity);

		static Program()
		{
			Program.ClusterPath = null;
		}

		private const int MAX_RW_WIDTH = 40;
		private const int MAX_WS_WIDTH = 20;

		private class HistoryMetric
		{
			public bool Available { get; set; }

			public TimeSpan LocalTime { get; set; }
			public long ReadVersion { get; set; }

			public long Timestamp { get; set; }

			public double ReadsPerSecond { get; set; }
			public double WritesPerSecond { get; set; }
			public double WrittenBytesPerSecond { get; set; }

			public double TransStarted { get; set; }
			public double TransCommitted { get; set; }
			public double TransConflicted { get; set; }

			public double LatencyCommit { get; set; }
			public double LatencyRead { get; set; }
			public double LatencyStart { get; set; }
		}

		private static char GetBarChar(double scale)
		{
			if (scale >= 1000000) return '@';
			if (scale >= 100000) return '#';
			if (scale >= 10000) return '|';
			return ':';
		}

		private static int Bar(double hz, double scale, int width)
		{
			if (hz == 0) return 0;

			var x = hz * width / scale;
			if (x < 0 || double.IsNaN(x)) x = 0;
			else if (x > width) x = width;
			return x == 0 ? 0 : x < 1 ? 1 : (int)Math.Round(x, MidpointRounding.AwayFromZero);
		}

		private static double GigaBytes(long x)
		{
			return x / 1073741824.0;
		}

		private static double MegaBytes(long x)
		{
			return x / 1048576.0;
		}

		private static double MegaBytes(double x)
		{
			return x / 1048576.0;
		}

		private static void WriteAt(int x, int y, string msg)
		{
			Console.SetCursorPosition(x, y);
			Console.Write(msg);
		}

		private static void WriteAt(int x, int y, string fmt, params object[] args)
		{
			Console.SetCursorPosition(x, y);
			Console.Write(String.Format(CultureInfo.InvariantCulture, fmt, args));
		}

		private static double GetMax(RingBuffer<HistoryMetric> metrics, Func<HistoryMetric, double> selector)
		{
			double max = double.NaN;
			foreach (var item in metrics)
			{
				double x = selector(item);
				if (double.IsNaN(max) || x > max) max = x;
			}
			return max;
		}

		private static double GetMaxScale(double max)
		{
			return Math.Pow(10, Math.Ceiling(Math.Log10(max)));
		}

		private static ConsoleColor FrenquencyColor(double hz)
		{
			return hz >= 100 ? ConsoleColor.White : hz >= 10 ? ConsoleColor.Gray : ConsoleColor.DarkGray;
		}

		private static ConsoleColor DiskSpeedColor(double bps)
		{
			return bps >= 1048576 ? ConsoleColor.White : bps >= 1024 ? ConsoleColor.Gray : ConsoleColor.DarkGray;
		}

		private static ConsoleColor LatencyColor(double x)
		{
			return x >= 1 ? ConsoleColor.Red
				: x >= 0.1 ? ConsoleColor.Yellow
				: x >= 0.01 ? ConsoleColor.White
				: ConsoleColor.Gray;
		}

		private const int TOP_COL0 = 1;
		private const int TOP_COL1 = TOP_COL0 + 24;
		private const int TOP_COL2 = TOP_COL1 + 26;
		private const int TOP_COL3 = TOP_COL2 + 36;
		private const int TOP_COL4 = TOP_COL3 + 22;

		private static void RepaintTopBar()
		{
			Console.Clear();
			Console.ForegroundColor = ConsoleColor.DarkGray;
			WriteAt(TOP_COL0, 1, "Reads  : {0,8} Hz", "");
			WriteAt(TOP_COL0, 2, "Writes : {0,8} Hz", "");
			WriteAt(TOP_COL0, 3, "Written: {0,8} MB/s", "");

			WriteAt(TOP_COL1, 1, "Total K/V: {0,10} MB", "");
			WriteAt(TOP_COL1, 2, "Disk Used: {0,10} MB", "");
			WriteAt(TOP_COL1, 3, "Shards: {0,5} x{0,6} MB", "");

			WriteAt(TOP_COL2, 1, "Server Time : {0,19}", "");
			WriteAt(TOP_COL2, 2, "Client Time : {0,19}", "");
			WriteAt(TOP_COL2, 3, "Read Version: {0,10}", "");

			WriteAt(TOP_COL3, 1, "Coordinat.: {0,10}", "");
			WriteAt(TOP_COL3, 2, "Storage   : {0,10}", "");
			WriteAt(TOP_COL3, 3, "Redundancy: {0,10}", "");

			WriteAt(TOP_COL4, 1, "State: {0,10}", "");
			WriteAt(TOP_COL4, 2, "Data : {0,20}", "");
			WriteAt(TOP_COL4, 3, "Perf.: {0,20}", "");
		}

		private static void UpdateTopBar(FdbSystemStatus status, HistoryMetric current)
		{
			Console.ForegroundColor = ConsoleColor.White;
			WriteAt(TOP_COL0 + 9, 1, "{0,8:N0}", current.ReadsPerSecond);
			WriteAt(TOP_COL0 + 9, 2, "{0,8:N0}", current.WritesPerSecond);
			WriteAt(TOP_COL0 + 9, 3, "{0,8:N2}", MegaBytes(current.WrittenBytesPerSecond));

			WriteAt(TOP_COL1 + 11, 1, "{0,10:N1}", MegaBytes(status.Cluster.Data.TotalKVUsedBytes));
			WriteAt(TOP_COL1 + 11, 2, "{0,10:N1}", MegaBytes(status.Cluster.Data.TotalDiskUsedBytes));
			WriteAt(TOP_COL1 + 8, 3, "{0,5:N0}", status.Cluster.Data.PartitionsCount);
			WriteAt(TOP_COL1 + 15, 3, "{0,6:N1}", MegaBytes(status.Cluster.Data.AveragePartitionSizeBytes));

			var serverTime = Epoch.AddSeconds(current.Timestamp);
			var clientTime = Epoch.AddSeconds(status.Client.Timestamp);
			WriteAt(TOP_COL2 + 14, 1, "{0,19}", serverTime.ToString("u"));
			if (Math.Abs((serverTime - clientTime).TotalSeconds) >= 20) Console.ForegroundColor = ConsoleColor.Red;
			WriteAt(TOP_COL2 + 14, 2, "{0,19}", clientTime.ToString("u"));
			Console.ForegroundColor = ConsoleColor.White;
			WriteAt(TOP_COL2 + 14, 3, "{0:N0}", current.ReadVersion);

			Console.ForegroundColor = ConsoleColor.White;
			WriteAt(TOP_COL3 + 12, 1, "{0,-10}", status.Cluster.Configuration.CoordinatorsCount);
			WriteAt(TOP_COL3 + 12, 2, "{0,-10}", status.Cluster.Configuration.StorageEngine);
			WriteAt(TOP_COL3 + 12, 3, "{0,-10}", status.Cluster.Configuration.RedundancyFactor);

			if (!status.Client.DatabaseAvailable)
			{
				Console.ForegroundColor = ConsoleColor.Red;
				WriteAt(TOP_COL4 + 7, 1, "UNAVAILABLE");
			}
			else
			{
				Console.ForegroundColor = ConsoleColor.Green;
				WriteAt(TOP_COL4 + 7, 1, "Available  ");
			}
			Console.ForegroundColor = ConsoleColor.White;
			WriteAt(TOP_COL4 + 7, 2, "{0,-40}", status.Cluster.Data.StateName);
			WriteAt(TOP_COL4 + 7, 3, "{0,-40}", status.Cluster.Qos.PerformanceLimitedBy.Name);

			//Console.ForegroundColor = ConsoleColor.Gray;
			//var msgs = status.Cluster.Messages.Concat(status.Client.Messages).ToArray();
			//for (int i = 0; i < 4; i++)
			//{
			//	string msg = msgs.Length > i ? msgs[i].Name : "";
			//	WriteAt(118, i + 1, "{0,-40}", msg.Length < 50 ? msg : msg.Substring(0, 40));
			//}

		}

		private static void ShowMetricsScreen(FdbSystemStatus status, HistoryMetric current, bool repaint)
		{
			const int COL0 = 1;
			const int COL1 = COL0 + 11;
			const int COL2 = COL1 + 12 + MAX_RW_WIDTH;
			const int COL3 = COL2 + 12 + MAX_RW_WIDTH;

			if (repaint)
			{
				Console.Title = "fdbtop - Metrics";
				RepaintTopBar();

				Console.ForegroundColor = ConsoleColor.DarkCyan;
				WriteAt(COL0, 5, "Elapsed");
				WriteAt(COL1, 5, "   Reads (Hz)");
				WriteAt(COL2, 5, "  Writes (Hz)");
				WriteAt(COL3, 5, "Disk Speed (MB/s)");
			}

			UpdateTopBar(status, current);

			double maxRead = GetMax(History, (m) => m.ReadsPerSecond);
			double maxWrite = GetMax(History, (m) => m.WritesPerSecond);
			double maxSpeed = GetMax(History, (m) => m.WrittenBytesPerSecond);
			double scaleRead = GetMaxScale(maxRead);
			double scaleWrite = GetMaxScale(maxWrite);
			double scaleSpeed = GetMaxScale(maxSpeed);

			Console.ForegroundColor = ConsoleColor.DarkGreen;
			WriteAt(COL1 + 14, 5, "{0,35:N0}", maxRead);
			WriteAt(COL2 + 14, 5, "{0,35:N0}", maxWrite);
			WriteAt(COL3 + 18, 5, "{0,13:N3}", MegaBytes(maxSpeed));

			int y = 7 + History.Count - 1;
			foreach (var metric in History)
			{
				Console.ForegroundColor = ConsoleColor.DarkGray;
				WriteAt(1, y,
					"{0} | {1,8} {1,40} | {1,8} {1,40} | {1,10} {1,20} |",
					TimeSpan.FromSeconds(Math.Round(metric.LocalTime.TotalSeconds, MidpointRounding.AwayFromZero)),
					""
				);

				if (metric.Available)
				{
					bool isMaxRead = maxRead > 0 && metric.ReadsPerSecond == maxRead;
					bool isMaxWrite = maxWrite > 0 && metric.WritesPerSecond == maxWrite;
					bool isMaxSpeed = maxSpeed > 0 && metric.WrittenBytesPerSecond == maxSpeed;

					Console.ForegroundColor = isMaxRead ? ConsoleColor.Cyan : FrenquencyColor(metric.ReadsPerSecond);
					WriteAt(COL1, y, "{0,8:N0}", metric.ReadsPerSecond);
					Console.ForegroundColor = isMaxWrite ? ConsoleColor.Cyan : FrenquencyColor(metric.WritesPerSecond);
					WriteAt(COL2, y, "{0,8:N0}", metric.WritesPerSecond);
					Console.ForegroundColor = isMaxSpeed ? ConsoleColor.Cyan : DiskSpeedColor(metric.WrittenBytesPerSecond);
					WriteAt(COL3, y, "{0,10:N3}", MegaBytes(metric.WrittenBytesPerSecond));

					Console.ForegroundColor = metric.ReadsPerSecond > 10 ? ConsoleColor.Green : ConsoleColor.DarkCyan;
					WriteAt(COL1 + 9, y, metric.ReadsPerSecond == 0 ? "-" : new string(GetBarChar(metric.ReadsPerSecond), Bar(metric.ReadsPerSecond, scaleRead, MAX_RW_WIDTH)));
					Console.ForegroundColor = metric.WritesPerSecond > 10 ? ConsoleColor.Green : ConsoleColor.DarkCyan;
					WriteAt(COL2 + 9, y, metric.WritesPerSecond == 0 ? "-" : new string(GetBarChar(metric.WritesPerSecond), Bar(metric.WritesPerSecond, scaleWrite, MAX_RW_WIDTH)));
					Console.ForegroundColor = metric.WrittenBytesPerSecond > 1000 ? ConsoleColor.Green : ConsoleColor.DarkCyan;
					WriteAt(COL3 + 11, y, metric.WrittenBytesPerSecond == 0 ? "-" : new string(GetBarChar(metric.WrittenBytesPerSecond / 1000), Bar(metric.WrittenBytesPerSecond, scaleSpeed, MAX_WS_WIDTH)));
				}
				else
				{
					Console.ForegroundColor = ConsoleColor.DarkRed;
					WriteAt(COL1, y, "{0,8}", "x");
					WriteAt(COL2, y, "{0,8}", "x");
					WriteAt(COL3, y, "{0,8}", "x");
				}
				--y;
			}

			Console.ForegroundColor = ConsoleColor.Gray;
		}

		private static void ShowLatencyScreen(FdbSystemStatus status, HistoryMetric current, bool repaint)
		{
			const int COL0 = 1;
			const int COL1 = COL0 + 11;
			const int COL2 = COL1 + 12 + MAX_RW_WIDTH;
			const int COL3 = COL2 + 12 + MAX_RW_WIDTH;

			if (repaint)
			{
				Console.Title = "fdbtop - Latency";
				RepaintTopBar();

				Console.ForegroundColor = ConsoleColor.DarkCyan;
				WriteAt(COL0, 5, "Elapsed");
				WriteAt(COL1, 5, "  Commit (ms)");
				WriteAt(COL2, 5, "    Read (ms)");
				WriteAt(COL3, 5, "   Start (ms)");
			}

			UpdateTopBar(status, current);

			double maxCommit = GetMax(History, (m) => m.LatencyCommit);
			double maxRead = GetMax(History, (m) => m.LatencyRead);
			double maxStart = GetMax(History, (m) => m.LatencyStart);
			double scaleCommit = GetMaxScale(maxCommit);
			double scaleRead = GetMaxScale(maxRead);
			double scaleStart = GetMaxScale(maxStart);

			Console.ForegroundColor = ConsoleColor.DarkGreen;
			WriteAt(COL1 + 14, 5, "{0,35:N3}", maxCommit * 1000);
			WriteAt(COL2 + 14, 5, "{0,35:N3}", maxRead * 1000);
			WriteAt(COL3 + 14, 5, "{0,18:N3}", maxStart * 1000);

			int y = 7 + History.Count - 1;
			foreach (var metric in History)
			{
				Console.ForegroundColor = ConsoleColor.DarkGray;
				WriteAt(1, y,
					"{0} | {1,8} {1,40} | {1,8} {1,40} | {1,10} {1,20} |",
					TimeSpan.FromSeconds(Math.Round(metric.LocalTime.TotalSeconds, MidpointRounding.AwayFromZero)),
					""
				);

				if (metric.Available)
				{
					bool isMaxRead = maxCommit > 0 && metric.LatencyCommit == maxCommit;
					bool isMaxWrite = maxRead > 0 && metric.LatencyRead == maxRead;
					bool isMaxSpeed = maxStart > 0 && metric.LatencyStart == maxStart;

					Console.ForegroundColor = isMaxRead ? ConsoleColor.Cyan : LatencyColor(metric.LatencyCommit);
					WriteAt(COL1, y, "{0,8:N3}", metric.LatencyCommit * 1000);
					Console.ForegroundColor = isMaxWrite ? ConsoleColor.Cyan : LatencyColor(metric.LatencyRead);
					WriteAt(COL2, y, "{0,8:N3}", metric.LatencyRead * 1000);
					Console.ForegroundColor = isMaxSpeed ? ConsoleColor.Cyan : LatencyColor(metric.LatencyStart);
					WriteAt(COL3, y, "{0,10:N3}", metric.LatencyStart * 1000);

					Console.ForegroundColor = ConsoleColor.Green;
					WriteAt(COL1 + 9, y, metric.LatencyCommit == 0 ? "-" : new string('|', Bar(metric.LatencyCommit, scaleCommit, MAX_RW_WIDTH)));
					WriteAt(COL2 + 9, y, metric.LatencyRead == 0 ? "-" : new string('|', Bar(metric.LatencyRead, scaleRead, MAX_RW_WIDTH)));
					WriteAt(COL3 + 11, y, metric.LatencyStart == 0 ? "-" : new string('|', Bar(metric.LatencyStart, scaleStart, MAX_WS_WIDTH)));
				}
				else
				{
					Console.ForegroundColor = ConsoleColor.DarkRed;
					WriteAt(COL1, y, "{0,8}", "x");
					WriteAt(COL2, y, "{0,8}", "x");
					WriteAt(COL3, y, "{0,8}", "x");
				}
				--y;
			}

			Console.ForegroundColor = ConsoleColor.Gray;
		}

		private static void ShowTransactionScreen(FdbSystemStatus status, HistoryMetric current, bool repaint)
		{
			const int COL0 = 1;
			const int COL1 = COL0 + 11;
			const int COL2 = COL1 + 12 + MAX_RW_WIDTH;
			const int COL3 = COL2 + 12 + MAX_RW_WIDTH;

			if (repaint)
			{
				Console.Title = "fdbtop - Transactions";
				RepaintTopBar();

				Console.ForegroundColor = ConsoleColor.DarkCyan;
				WriteAt(COL0, 5, "Elapsed");
				WriteAt(COL1, 5, "Started (tps)");
				WriteAt(COL2, 5, "Committed (tps)");
				WriteAt(COL3, 5, "Conflicted (tps)");
			}

			UpdateTopBar(status, current);

			double maxStarted = GetMax(History, (m) => m.TransStarted);
			double maxCommitted = GetMax(History, (m) => m.TransCommitted);
			double maxConflicted = GetMax(History, (m) => m.TransConflicted);
			double scaleStarted = GetMaxScale(maxStarted);
			double scaleComitted = GetMaxScale(maxCommitted);
			double scaleConflicted = GetMaxScale(maxConflicted);

			Console.ForegroundColor = ConsoleColor.DarkGreen;
			WriteAt(COL1 + 14, 5, "{0,35:N0}", maxStarted);
			WriteAt(COL2 + 16, 5, "{0,33:N0}", maxCommitted);
			WriteAt(COL3 + 16, 5, "{0,15:N0}", maxConflicted);

			int y = 7 + History.Count - 1;
			foreach (var metric in History)
			{
				Console.ForegroundColor = ConsoleColor.DarkGray;
				WriteAt(1, y,
					"{0} | {1,8} {1,40} | {1,8} {1,40} | {1,10} {1,20} |",
					TimeSpan.FromSeconds(Math.Round(metric.LocalTime.TotalSeconds, MidpointRounding.AwayFromZero)),
					""
				);

				if (metric.Available)
				{
					bool isMaxRead = maxStarted > 0 && metric.LatencyCommit == maxStarted;
					bool isMaxWrite = maxCommitted > 0 && metric.LatencyRead == maxCommitted;
					bool isMaxSpeed = maxConflicted > 0 && metric.LatencyStart == maxConflicted;

					Console.ForegroundColor = isMaxRead ? ConsoleColor.Cyan : FrenquencyColor(metric.TransStarted);
					WriteAt(COL1, y, "{0,8:N0}", metric.TransStarted);
					Console.ForegroundColor = isMaxWrite ? ConsoleColor.Cyan : FrenquencyColor(metric.TransCommitted);
					WriteAt(COL2, y, "{0,8:N0}", metric.TransCommitted);
					Console.ForegroundColor = isMaxSpeed ? ConsoleColor.Cyan : FrenquencyColor(metric.TransConflicted);
					WriteAt(COL3, y, "{0,8:N1}", metric.TransConflicted);

					Console.ForegroundColor = metric.TransStarted > 10 ? ConsoleColor.Green : ConsoleColor.DarkGreen;
					WriteAt(COL1 + 9, y, metric.TransStarted == 0 ? "-" : new string('|', Bar(metric.TransStarted, scaleStarted, MAX_RW_WIDTH)));
					Console.ForegroundColor = metric.TransCommitted > 10 ? ConsoleColor.Green : ConsoleColor.DarkGreen;
					WriteAt(COL2 + 9, y, metric.TransCommitted == 0 ? "-" : new string('|', Bar(metric.TransCommitted, scaleComitted, MAX_RW_WIDTH)));
					Console.ForegroundColor = metric.TransConflicted > 1000 ? ConsoleColor.Red : metric.TransConflicted > 10 ? ConsoleColor.Green : ConsoleColor.DarkGreen;
					WriteAt(COL3 + 9, y, metric.TransConflicted == 0 ? "-" : new string('|', Bar(metric.TransConflicted, scaleConflicted, MAX_WS_WIDTH)));
				}
				else
				{
					Console.ForegroundColor = ConsoleColor.DarkRed;
					WriteAt(COL1, y, "{0,8}", "x");
					WriteAt(COL2, y, "{0,8}", "x");
					WriteAt(COL3, y, "{0,8}", "x");
				}
				--y;
			}

			Console.ForegroundColor = ConsoleColor.Gray;
		}

		private struct RoleMap
		{
			private bool Master;
			private bool ClusterController;
			private bool Proxy;
			private bool Log;
			private bool Storage;
			private bool Resolver;
			private bool Other;

			public void Add(string role)
			{
				switch(role)
				{
					case "master": this.Master = true; break;
					case "cluster_controller": this.ClusterController = true; break;
					case "proxy": this.Proxy = true; break;
					case "log": this.Log = true; break;
					case "storage": this.Storage = true; break;
					case "resolver": this.Resolver = true; break;
					default: this.Other = true; break;
				}
			}

			public override string ToString()
			{
				var sb = new StringBuilder(11);
				sb.Append(this.Master ? "M " : "- ");
				sb.Append(this.ClusterController ? "C " : "- ");
				sb.Append(this.Proxy ? "P " : "- ");
				sb.Append(this.Log ? "L " : "- ");
				sb.Append(this.Storage ? "S " : "- ");
				sb.Append(this.Resolver ? "R" : "-");
				return sb.ToString();
			}
		}

		private static void ShowProcessesScreen(FdbSystemStatus status, HistoryMetric current, bool repaint)
		{
			const int BARSZ = 15;

			const int COL0 = 1;
			const int COL1 = COL0 + 18;
			const int COL2 = COL1 + 12;
			const int COL3 = COL2 + 15;
			const int COL4 = COL3 + 11 + BARSZ;
			const int COL5 = COL4 + 8;
			const int COL6 = COL5 + 12 + BARSZ;
			const int COL7 = COL6 + 10 + BARSZ;

			if (repaint)
			{
				Console.Title = "fdbtop - Processes";
				RepaintTopBar();

				Console.ForegroundColor = ConsoleColor.DarkCyan;
				WriteAt(COL0, 5, "Address (port)");
				WriteAt(COL1, 5, "Network in / out (MB/s)");
				WriteAt(COL3, 5, "CPU (%core)");
				WriteAt(COL4, 5, "Memory Free / Total (GB)");
				WriteAt(COL6, 5, "HDD (%busy)");
				WriteAt(COL7, 5, "Roles");

#if DEBUG_LAYOUT
				Console.ForegroundColor = ConsoleColor.DarkGray;
				WriteAt(COL0, 6, "0 - - - - - -");
				WriteAt(COL1, 6, "1 - - - - - -");
				WriteAt(COL2, 6, "2 - - - - - -");
				WriteAt(COL3, 6, "3 - - - - - -");
				WriteAt(COL4, 6, "4 - - - - - -");
				WriteAt(COL5, 6, "5 - - - - - -");
				WriteAt(COL6, 6, "6 - - - - - -");
				WriteAt(COL7, 6, "7 - - - - - -");
#endif
			}

			UpdateTopBar(status, current);

			if (status.Cluster.Machines.Count == 0)
			{
				//TODO display error message?
				return;
			}

			var maxVersion = status.Cluster.Processes.Values.Max(p => p.Version);

			int y = 7;
			foreach(var machine in status.Cluster.Machines.Values.OrderBy(x => x.Address, StringComparer.Ordinal))
			{
				var procs = status.Cluster.Processes.Values
					.Where(p => p.MachineId == machine.Id)
					.OrderBy(p => p.Address, StringComparer.Ordinal)
					.ToList();

				var map = new RoleMap();
				foreach(var proc in procs)
				{
					foreach(var role in proc.Roles)
					{
						map.Add(role.Role);
					}
				}

				Console.ForegroundColor = ConsoleColor.DarkGray;
				WriteAt(1, y,
					"{0,15} | {0,8} in {0,8} out | {0,6}% {0,15} | {0,5} / {0,5} GB {0,15} | {0,22} | {0,11} |",
					""
				);

				Console.ForegroundColor = ConsoleColor.White;
				//"{0,-15} | net {2,8:N3} in {3,8:N3} out | cpu {4,5:N1}% | mem {5,5:N1} / {7,5:N1} GB {8,-20} | hdd {9,5:N1}% {10,-20}",
				WriteAt(COL0, y, machine.Address);
				WriteAt(COL1, y, "{0,8:N3}", MegaBytes(machine.Network.MegabitsReceived.Hz * 125000));
				WriteAt(COL2, y, "{0,8:N3}", MegaBytes(machine.Network.MegabitsSent.Hz * 125000));
				WriteAt(COL3, y, "{0,6:N1}", machine.Cpu.LogicalCoreUtilization * 100);
				WriteAt(COL4, y, "{0,5:N1}", GigaBytes(machine.Memory.CommittedBytes));
				WriteAt(COL5, y, "{0,5:N1}", GigaBytes(machine.Memory.TotalBytes));
				//WriteAt(COL6, y, "{0,5:N1}", totalDiskBusy * 100);
				WriteAt(COL7, y, "{0,11}", map);

				Console.ForegroundColor = machine.Cpu.LogicalCoreUtilization >= 0.9 ? ConsoleColor.Red : ConsoleColor.Green;
				WriteAt(COL3 + 8, y, "{0,-15}", new string('|', Bar(machine.Cpu.LogicalCoreUtilization, 1, BARSZ))); // 1 = all the (logical) cores

				Console.ForegroundColor = machine.Memory.CommittedBytes >= 0.95 * machine.Memory.TotalBytes ? ConsoleColor.Red : ConsoleColor.Green;
				WriteAt(COL5 + 9, y, "{0,-15}", new string('|', Bar(machine.Memory.CommittedBytes, machine.Memory.TotalBytes, BARSZ)));

				//Console.ForegroundColor = totalDiskBusy >= 0.95 ? ConsoleColor.Red : ConsoleColor.Green;
				//WriteAt(COL6 + 7, y, "{0,-15}", new string('|', Bar(totalDiskBusy, 1, BARSZ)));

				++y;

				//TODO: use a set to map procs ot machines? Where(..) will be slow if there are a lot of machines x processes
				foreach (var proc in procs)
				{
					int p = proc.Address.IndexOf(':');
					string port = p >= 0 ? proc.Address.Substring(p + 1) : proc.Address;

					map = new RoleMap();
					foreach (var role in proc.Roles)
					{
						map.Add(role.Role);
					}
					Console.ForegroundColor = ConsoleColor.DarkGray;
					WriteAt(1, y,
						"{0,7} | {0,5} | {0,8} in {0,8} out | {0,6}% {0,15} | {0,5} / {0,5} GB {0,15} | {0,5}% {0,15} | {0,11} |",
						""
					);

					Console.ForegroundColor = proc.Version != maxVersion ? ConsoleColor.DarkCyan : ConsoleColor.Gray;
					WriteAt(1 +  10, y, "{0,5}", proc.Version);

					Console.ForegroundColor = proc.Excluded ? ConsoleColor.DarkRed : ConsoleColor.Gray;
					WriteAt(COL0, y, "{0,7}", port);
					Console.ForegroundColor = ConsoleColor.Gray;
					WriteAt(COL1, y, "{0,8:N3}", MegaBytes(proc.Network.MegabitsReceived.Hz * 125000));
					WriteAt(COL2, y, "{0,8:N3}", MegaBytes(proc.Network.MegabitsSent.Hz * 125000));
					WriteAt(COL3, y, "{0,6:N1}", proc.Cpu.UsageCores * 100);
					WriteAt(COL4, y, "{0,5:N1}", GigaBytes(proc.Memory.UsedBytes));
					WriteAt(COL5, y, "{0,5:N1}", GigaBytes(proc.Memory.AvailableBytes));
					WriteAt(COL6, y, "{0,5:N1}", Math.Min(proc.Disk.Busy * 100, 100)); // 1 == 1 core, but a process can go a little bit higher
					WriteAt(COL7, y, "{0,11}", map);

					Console.ForegroundColor = proc.Cpu.UsageCores >= 0.95 ? ConsoleColor.DarkRed : ConsoleColor.DarkGreen;
					WriteAt(COL3 + 8, y, "{0,-15}", new string('|', Bar(proc.Cpu.UsageCores, 1, BARSZ)));

					Console.ForegroundColor = proc.Memory.UsedBytes >= 0.95 * proc.Memory.AvailableBytes ? ConsoleColor.DarkRed : ConsoleColor.DarkGreen;
					WriteAt(COL5 + 9, y, "{0,-15}", new string('|', Bar(proc.Memory.UsedBytes, proc.Memory.AvailableBytes, BARSZ)));

					Console.ForegroundColor = proc.Disk.Busy >= 0.95 ? ConsoleColor.DarkRed : ConsoleColor.DarkGreen;
					WriteAt(COL6 + 7, y, "{0,-15}", new string('|', Bar(proc.Disk.Busy, 1, BARSZ)));

					++y;
				}
				++y;
			}

		}

		private static void ShowHelpScreen(bool repaint)
		{
			if (repaint)
			{
				Console.Title = "fdbtop - Topology";
				RepaintTopBar();
			}

			//TODO!
		}

		public enum DisplayMode
		{
			Help = 0,
			Processes,
			Metrics,
			Latency,
			Transactions,
		}

	}
}
