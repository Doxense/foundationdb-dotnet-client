using FoundationDB.Client;
using FoundationDB.Client.Status;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace FdbTop
{
	public static class Program
	{

		static string ClusterPath = null;

		public static void Main(string[] args)
		{
			//TODO: move this to the main, and add a command line argument to on/off ?
			if (Console.LargestWindowWidth > 0 && Console.LargestWindowHeight > 0)
			{
				Console.WindowWidth = 160;
				Console.WindowHeight = 60;
			}

			if (args.Length > 0)
			{
				ClusterPath = args[0];
			}

			//note: always use the latest version available
			Fdb.UseApiVersion(Fdb.GetMaxSafeApiVersion());
			try
			{
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
				Fdb.Stop();
			}
		}

		private static readonly DateTime Epoch = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);

		private const int HistoryCapacity = 50;
		private static RingBuffer<HistoryMetric> History = new RingBuffer<HistoryMetric>(HistoryCapacity);

		private const int MAX_RW_WIDTH = 40;
		private const int MAX_WS_WIDTH = 20;

		private class HistoryMetric
		{
			public bool Available { get; set; }

			public TimeSpan LocalTime { get; set; }
			public long ReadVersion { get; set; }

			public long Timestamp { get; set; }

			public double ReadsHz { get; set; }
			public double WritesHz { get; set; }
			public double WrittenHz { get; set; }
			public double TransStarted { get; set; }
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
			return (int)Math.Ceiling(x);
		}

		private static int BarLog(double hz, double scaleLog10, int width)
		{
			if (hz == 0) return 0;

			var x = Math.Log10(hz) * width / scaleLog10;
			if (x < 0 || double.IsNaN(x)) x = 0;
			else if (x > width) x = width;
			return (int)Math.Ceiling(x);
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

		private static void RepaintTopBar()
		{
			Console.Clear();
			Console.ForegroundColor = ConsoleColor.Gray;
			WriteAt(1, 1, "Reads  : {0,8} Hz", "");
			WriteAt(1, 2, "Writes : {0,8} Hz", "");
			WriteAt(1, 3, "Written: {0,8} MB/s", "");

			WriteAt(25, 1, "Total K/V: {0,10} MB", "");
			WriteAt(25, 2, "Disk Used: {0,10} MB", "");

			WriteAt(52, 1, "Server Time : {0,19}", "");
			WriteAt(52, 2, "Client Time : {0,19}", "");
			WriteAt(52, 3, "Read Version: {0,10}", "");

			WriteAt(88, 1, "State: {0,10}", "");
			WriteAt(88, 2, "Data : {0,20}", "");
			WriteAt(88, 3, "Perf.: {0,20}", "");
		}

		private static void UpdateTopBar(FdbSystemStatus status, HistoryMetric current)
		{
			Console.ForegroundColor = ConsoleColor.White;
			WriteAt(1 + 9, 1, "{0,8:N0}", current.ReadsHz);
			WriteAt(1 + 9, 2, "{0,8:N0}", current.WritesHz);
			WriteAt(1 + 9, 3, "{0,8:N2}", MegaBytes(current.WrittenHz));

			WriteAt(25 + 11, 1, "{0,10:N1}", MegaBytes(status.Cluster.Data.TotalKVUsedBytes));
			WriteAt(25 + 11, 2, "{0,10:N1}", MegaBytes(status.Cluster.Data.TotalDiskUsedBytes));

			var serverTime = Epoch.AddSeconds(current.Timestamp);
			var clientTime = Epoch.AddSeconds(status.Client.Timestamp);
			WriteAt(52 + 14, 1, "{0,19}", serverTime);
			if (Math.Abs((serverTime - clientTime).TotalSeconds) >= 20) Console.ForegroundColor = ConsoleColor.Red;
			WriteAt(52 + 14, 2, "{0,19}", clientTime);
			Console.ForegroundColor = ConsoleColor.White;
			WriteAt(52 + 14, 3, "{0:N0}", current.ReadVersion);

			if (!status.Client.DatabaseAvailable)
			{
				Console.ForegroundColor = ConsoleColor.Red;
				WriteAt(88 + 7, 1, "UNAVAILABLE");
			}
			else
			{
				Console.ForegroundColor = ConsoleColor.Green;
				WriteAt(88 + 7, 1, "Available  ");
			}
			Console.ForegroundColor = ConsoleColor.White;
			WriteAt(88 + 7, 2, "{0,-40}", status.Cluster.Data.StateName);
			WriteAt(88 + 7, 3, "{0,-40}", status.Cluster.Qos.PerformanceLimitedBy.Name);
		}

		private static void ShowMetricsScreen(FdbSystemStatus status, HistoryMetric current, bool repaint)
		{
			if (repaint)
			{
				RepaintTopBar();
				WriteAt(  1, 5, "Elapsed");
				WriteAt( 12, 5, "   Reads (Hz)");
				WriteAt( 64, 5, "  Writes (Hz)");
				WriteAt(116, 5, "Disk Speed (MB/s)");
			}

			UpdateTopBar(status, current);

			double maxRead = GetMax(History, (m) => m.ReadsHz);
			double maxWrite = GetMax(History, (m) => m.WritesHz);
			double maxSpeed = GetMax(History, (m) => m.WrittenHz);
			double scaleRead = GetMaxScale(maxRead);
			double scaleWrite = GetMaxScale(maxWrite);
			double scaleSpeed = GetMaxScale(maxSpeed);
			//double scaleRatio = Math.Log10(Math.Max(scaleRead, scaleWrite));
			//double speedRatio = Math.Log10(scaleSpeed);

			Console.ForegroundColor = ConsoleColor.DarkGreen;
			WriteAt( 12 + 14, 5, "{0,35:N0}", maxRead);
			WriteAt( 64 + 14, 5, "{0,35:N0}", maxWrite);
			WriteAt(116 + 18, 5, "{0,13:N3}", MegaBytes(maxSpeed));

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
					bool isMaxRead = maxRead > 0 && metric.ReadsHz == maxRead;
					bool isMaxWrite = maxWrite > 0 && metric.WritesHz == maxWrite;
					bool isMaxSpeed = maxSpeed > 0 && metric.WrittenHz == maxSpeed;

					Console.ForegroundColor = isMaxRead ? ConsoleColor.Cyan : metric.ReadsHz >= 100 ? ConsoleColor.White : metric.ReadsHz >= 10 ? ConsoleColor.Gray : ConsoleColor.DarkGray;
					WriteAt(12, y, "{0,8:N0}", metric.ReadsHz);
					Console.ForegroundColor = isMaxWrite ? ConsoleColor.Cyan : metric.WritesHz >= 100 ? ConsoleColor.White : metric.WritesHz >= 10 ? ConsoleColor.Gray : ConsoleColor.DarkGray;
					WriteAt(64, y, "{0,8:N0}", metric.WritesHz);
					Console.ForegroundColor = isMaxSpeed ? ConsoleColor.Cyan : metric.WrittenHz >= 1048576 ? ConsoleColor.White : metric.WrittenHz >= 1024 ? ConsoleColor.Gray : ConsoleColor.DarkGray;
					WriteAt(116, y, "{0,10:N3}", MegaBytes(metric.WrittenHz));

					Console.ForegroundColor = ConsoleColor.Green;
					WriteAt(12 + 9, y, metric.ReadsHz == 0 ? "-" : new string(GetBarChar(metric.ReadsHz), Bar(metric.ReadsHz, scaleRead, MAX_RW_WIDTH)));
					WriteAt(64 + 9, y, metric.WritesHz == 0 ? "-" : new string(GetBarChar(metric.WritesHz), Bar(metric.WritesHz, scaleWrite, MAX_RW_WIDTH)));
					WriteAt(116 + 11, y, metric.WrittenHz == 0 ? "-" : new string(GetBarChar(metric.WrittenHz / 1000), Bar(metric.WrittenHz, scaleSpeed, MAX_WS_WIDTH)));
				}
				else
				{
					Console.ForegroundColor = ConsoleColor.DarkRed;
					WriteAt(12, y, "{0,8}", "x");
					WriteAt(64, y, "{0,8}", "x");
					WriteAt(116, y, "{0,8}", "x");
				}
				--y;
			}

			Console.ForegroundColor = ConsoleColor.Gray;
			var msgs = status.Cluster.Messages.Concat(status.Client.Messages).ToArray();
			for (int i = 0; i < 4; i++)
			{
				string msg = msgs.Length > i ? msgs[i].Name : "";
				WriteAt(118, i + 1, "{0,-40}", msg.Length < 50 ? msg : msg.Substring(0, 40));
			}

			Console.ForegroundColor = ConsoleColor.Gray;
		}

		private static void ShowTopologyScreen(FdbSystemStatus status, HistoryMetric current, bool repaint)
		{
			if (repaint)
			{
				RepaintTopBar();

				WriteAt(1 +   0, 5, "Address (port)");
				WriteAt(1 +  18, 5, "Network in / out (MB/s)");
				WriteAt(1 +  46, 5, "CPU (%core)");
				WriteAt(1 +  75, 5, "Memory Free / Total (GB)");
				WriteAt(1 + 116, 5, "HDD (MB/s)");
			}

			UpdateTopBar(status, current);

			int y = 8;
			foreach(var machine in status.Cluster.Machines.Values.OrderBy(x => x.Address, StringComparer.Ordinal))
			{
				var procs = status.Cluster.Processes.Values
					.Where(p => p.MachineId == machine.Id)
					.OrderBy(p => p.Address, StringComparer.Ordinal)
					.ToList();
				double totalHddBusy = Math.Min(procs.Sum(p => p.Disk.Busy), 1);

				Console.ForegroundColor = ConsoleColor.DarkGray;
				WriteAt(1, y,
					"{0,15} | {0,8} in {0,8} out | {0,6}% {0,20} | {0,5} / {0,5} GB {0,20} | {0,5}% {0,20}",
					""
				);

				Console.ForegroundColor = ConsoleColor.White;
				//"{0,-15} | net {2,8:N3} in {3,8:N3} out | cpu {4,5:N1}% | mem {5,5:N1} / {7,5:N1} GB {8,-20} | hdd {9,5:N1}% {10,-20}",
				WriteAt(1 +   0, y, machine.Address);
				WriteAt(1 +  18, y, "{0,8:N3}", MegaBytes(machine.Network.MegabitsReceived.Hz * 125000));
				WriteAt(1 +  30, y, "{0,8:N3}", MegaBytes(machine.Network.MegabitsSent.Hz * 125000));
				WriteAt(1 +  45, y, "{0,6:N1}", machine.Cpu.LogicalCoreUtilization * 100);
				WriteAt(1 +  76, y, "{0,5:N1}", GigaBytes(machine.Memory.CommittedBytes));
				WriteAt(1 +  84, y, "{0,5:N1}", GigaBytes(machine.Memory.TotalBytes));
				WriteAt(1 + 116, y, "{0,5:N1}", totalHddBusy * 100);

				Console.ForegroundColor = ConsoleColor.Green;
				WriteAt(1 +  53, y, "{0,-20}", new string('|', Bar(machine.Cpu.LogicalCoreUtilization, 1, 20))); //REVIEW: we don't know the total number of cores!!!
				WriteAt(1 +  93, y, "{0,-20}", new string('|', Bar(machine.Memory.CommittedBytes, machine.Memory.TotalBytes, 20)));
				WriteAt(1 + 123, y, "{0,-20}", new string('|', Bar(totalHddBusy, 1, 20)));

				++y;

				//TODO: use a set to map procs ot machines? Where(..) will be slow if there are a lot of machines x processes
				foreach (var proc in procs)
				{
					int p = proc.Address.IndexOf(':');
					string port = p >= 0 ? proc.Address.Substring(p + 1) : proc.Address;

					Console.ForegroundColor = ConsoleColor.DarkGray;
					WriteAt(1, y,
						"{0,7} | {0,5} | {0,8} in {0,8} out | {0,6}% {0,20} | {0,5} / {0,5} GB {0,20} | {0,5}% {0,20}",
						""
					);

					Console.ForegroundColor = ConsoleColor.Gray;
					WriteAt(1 +   0, y, "{0,7}", port);
					WriteAt(1 +  10, y, "{0,5}", proc.Version);
					WriteAt(1 +  18, y, "{0,8:N3}", MegaBytes(proc.Network.MegabitsReceived.Hz * 125000));
					WriteAt(1 +  30, y, "{0,8:N3}", MegaBytes(proc.Network.MegabitsSent.Hz * 125000));
					WriteAt(1 +  45, y, "{0,6:N1}", proc.Cpu.UsageCores * 100);
					WriteAt(1 +  76, y, "{0,5:N1}", GigaBytes(proc.Memory.UsedBytes));
					WriteAt(1 +  84, y, "{0,5:N1}", GigaBytes(proc.Memory.AvailableBytes));
					WriteAt(1 + 116, y, "{0,5:N1}", proc.Disk.Busy * 100);

					Console.ForegroundColor = ConsoleColor.DarkGreen;
					WriteAt(1 +  53, y, "{0,-20}", new string('|', Bar(proc.Cpu.UsageCores, 1, 20))); //REVIEW: we don't know the total number of cores!!!
					WriteAt(1 +  93, y, "{0,-20}", new string('|', Bar(proc.Memory.UsedBytes, machine.Memory.CommittedBytes, 20)));
					WriteAt(1 + 123, y, "{0,-20}", new string('|', Bar(proc.Disk.Busy, 1, 20)));

					++y;
				}
				++y;
			}

		}

		enum DisplayMode
		{
			Help = 0,
			Metrics,
			Topology
		}

		private static async Task MainAsync(string[] args, CancellationToken cancel)
		{
			Console.CursorVisible = false;
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
					db.DefaultTimeout = 5000;

					while (!exit && !cancel.IsCancellationRequested)
					{
						now = DateTime.UtcNow;

						if (Console.KeyAvailable)
						{
							var k = Console.ReadKey();
							switch (k.Key)
							{
								case ConsoleKey.Escape:
								case ConsoleKey.Q:
								{
									exit = true;
									break;
								}
								case ConsoleKey.S:
								{
									saveNext = true;
									break;
								}

								case ConsoleKey.F:
								{
									if (speed == FAST) speed = SLOW; else speed = FAST;
									break;
								}

								case ConsoleKey.T:
								{
									mode = DisplayMode.Topology;
									updated = repaint = true;
									break;
								}
								case ConsoleKey.M:
								{
									mode = DisplayMode.Metrics;
									updated = repaint = true;
									break;
								}

								case ConsoleKey.R:
								{
									lap = now;
									next = now;
									History.Clear();
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
								ReadsHz = status.Cluster.Workload.Operations.Reads.Hz,
								WritesHz = status.Cluster.Workload.Operations.Writes.Hz,
								WrittenHz = status.Cluster.Workload.Bytes.Written.Hz,
								TransStarted = status.Cluster.Workload.Transactions.Started.Hz
							};
							History.Enqueue(metric);
							updated = true;

							now = DateTime.UtcNow;
							while (next < now) next = next.AddSeconds(speed);
						}

						if (updated)
						{
							var metric = History.LastOrDefault();
							if (mode == DisplayMode.Metrics)
							{
								ShowMetricsScreen(status, metric, repaint);
							}
							else
							{
								ShowTopologyScreen(status, metric, repaint);
							}
							repaint = false;
							updated = false;
						}

						await Task.Delay(100);
					}

				}
			}
			finally
			{
				Console.CursorVisible = true;
				Console.ForegroundColor = ConsoleColor.Gray;
				Console.Clear();
			}
		}
	}
}
