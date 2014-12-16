using FoundationDB.Client;
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
		public static void Main(string[] args)
		{
			//TODO: move this to the main, and add a command line argument to on/off ?
			if (Console.LargestWindowWidth > 0 && Console.LargestWindowHeight > 0)
			{
				Console.WindowWidth = 160;
				Console.WindowHeight = 60;
			}

			// Initialize FDB

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

		private static int BarLog(double hz, double scaleLog10, int width)
		{
			if (hz == 0) return 0;

			var x = Math.Log10(hz) * width / scaleLog10;
			if (x < 0 || double.IsNaN(x)) x = 0;
			else if (x > width) x = width;
			return (int)Math.Ceiling(x);
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
			return Math.Pow(10, Math.Ceiling(Math.Log10(max) * 1.1));
		}

		private static void UpdateScreen(Fdb.Status.SystemStatus status, HistoryMetric current, bool repaint)
		{
			if (repaint)
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

				WriteAt( 1, 6, "Elapsed");
				WriteAt(12, 6, "   Reads (Hz)");
				WriteAt(64, 6, "  Writes (Hz)");
				WriteAt(116, 6, "Disk Speed (MB/s)");
			}

			Console.ForegroundColor = ConsoleColor.White;
			WriteAt(1 + 9, 1, "{0,8:N0}", current.ReadsHz);
			WriteAt(1 + 9, 2, "{0,8:N0}", current.WritesHz);
			WriteAt(1 + 9, 3, "{0,8:N1}", MegaBytes(current.WrittenHz));

			WriteAt(25 + 11, 1, "{0,10:N1}", MegaBytes(status.Cluster.Data.TotalKVUsedBytes));
			WriteAt(25 + 11, 2, "{0,10:N1}", MegaBytes(status.Cluster.Data.TotalDiskUsedBytes));

			WriteAt(52 + 14, 1, "{0,19}", Epoch.AddSeconds(current.Timestamp));
			WriteAt(52 + 14, 2, "{0,19}", Epoch.AddSeconds(status.Client.Timestamp));
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

			double maxRead = GetMax(History, (m) => m.ReadsHz);
			double maxWrite = GetMax(History, (m) => m.WritesHz);
			double maxSpeed = GetMax(History, (m) => m.WrittenHz);
			double scaleRead = GetMaxScale(maxRead);
			double scaleWrite = GetMaxScale(maxWrite);
			double scaleSpeed = GetMaxScale(maxSpeed);
			double scaleRatio = Math.Log10(Math.Max(scaleRead, scaleWrite));
			double speedRatio = Math.Log10(scaleSpeed);

			Console.ForegroundColor = ConsoleColor.DarkGreen;
			WriteAt(12 + 14, 6, "{0,35:N0}", maxRead);
			WriteAt(64 + 14, 6, "{0,35:N0}", maxWrite);
			WriteAt(116 + 18, 6, "{0,11:N1}", MegaBytes(maxSpeed));

			int y = 7 + History.Count - 1;
			foreach (var metric in History)
			{
				Console.ForegroundColor = ConsoleColor.DarkGray;
				WriteAt(1, y,
					"{0} | {1,8} {2,40} | {3,8} {4,40} | {5,8} {6,20} |",
					TimeSpan.FromSeconds(Math.Round(metric.LocalTime.TotalSeconds, MidpointRounding.AwayFromZero)),
					null, //step.ReadsHz,
					null, //metric.ReadsHz == 0 ? "-" : new string('|', BarWidth(metric.ReadsHz)),
					null, //step.WritesHz,
					null, //metric.WritesHz == 0 ? "-" : new string('|', BarWidth(metric.WritesHz)),
					null, //MegaBytes(step.WrittenHz),
					null
				);

				Trace.WriteLine("Metric: " + metric.Timestamp + ", avl:" + metric.Available);

				if (metric.Available)
				{
					Console.ForegroundColor = maxRead > 0 && metric.ReadsHz == maxRead ? ConsoleColor.Green : ConsoleColor.White;
					WriteAt(12, y, "{0,8:N0}", metric.ReadsHz);

					Console.ForegroundColor = maxWrite > 0 && metric.WritesHz == maxWrite ? ConsoleColor.Green : ConsoleColor.White;
					WriteAt(64, y, "{0,8:N0}", metric.WritesHz);

					Console.ForegroundColor = maxSpeed > 0 && metric.WrittenHz == maxSpeed ? ConsoleColor.Green : ConsoleColor.White;
					WriteAt(116, y, "{0,8:N1}", MegaBytes(metric.WrittenHz));

					Console.ForegroundColor = ConsoleColor.Green;
					WriteAt(12 + 9, y, metric.ReadsHz == 0 ? "-" : new string('|', BarLog(metric.ReadsHz, scaleRatio, MAX_RW_WIDTH)));
					WriteAt(64 + 9, y, metric.WritesHz == 0 ? "-" : new string('|', BarLog(metric.WritesHz, scaleRatio, MAX_RW_WIDTH)));
					WriteAt(116 + 9, y, metric.WrittenHz == 0 ? "-" : new string('|', BarLog(metric.WrittenHz, speedRatio, MAX_WS_WIDTH)));
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
				WriteAt(108, i + 1, "{0,-50}", msg.Length < 50 ? msg : msg.Substring(0, 50));
			}

			Console.ForegroundColor = ConsoleColor.Gray;
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
				using (var db = await Fdb.OpenAsync(cancel))
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
								case ConsoleKey.R:
								{
									lap = now;
									next = now;
									History.Clear();
									repaint = true;
									break;
								}
							}
						}

						var status = await Fdb.Status.GetStatusAsync(db, cancel);

						if (lap == DateTime.MinValue)
						{
							lap = now;
							next = now.AddSeconds(1);
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

							UpdateScreen(status, metric, repaint);
							repaint = false;
							now = DateTime.UtcNow;
							while (next < now) next = next.AddSeconds(1);
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
