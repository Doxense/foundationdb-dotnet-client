using FoundationDB.Client;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace FdbBurner
{

	public class Program
	{
		static string ClusterPath = null;

		public static void Main(string[] args)
		{
			//TODO: move this to the main, and add a command line argument to on/off ?

			string title = Console.Title;
			try
			{

				if (args.Length > 0)
				{
					ClusterPath = args[0];
				}

				//note: always use the latest version available
				Fdb.UseApiVersion(Fdb.GetMaxSafeApiVersion());
				Fdb.Start();
				using (var go = new CancellationTokenSource())
				{
					MainAsync(args, go.Token).GetAwaiter().GetResult();
				}
			}
			catch (Exception e)
			{
				Trace.WriteLine(e.ToString());
				Console.Error.WriteLine("TOO HOT! " + e);
				Environment.ExitCode = -1;
			}
			finally
			{
				Console.Title = title;
				Console.CursorVisible = true;
				Fdb.Stop();
			}
		}

		public static long Keys;
		public static long Transactions;
		public static long Bytes;

		public static bool Randomized;

		private static async Task BurnerThread(IFdbDatabase db, CancellationToken ct)
		{

			var folder = await db.Directory.CreateOrOpenAsync(new[] { "Benchmarks", "Burner", "Sequential" }, ct);

			await db.WriteAsync((tr) => tr.ClearRange(folder), ct);

			const int N = 1000;
			const int KS = 32;
			const int VS = 3000;

			var rnd = new Random();
			Slice suffix = Slice.Random(rnd, KS);
			Slice value = Slice.Random(rnd, VS);

			long pos = 0;

			using (var tr = db.BeginTransaction(ct))
			{
				while (!ct.IsCancellationRequested)
				{
					FdbException error = null;
					try
					{
						tr.Reset();

						for(int i = 0; i < N; i++)
						{
							long x = Randomized
								? rnd.Next()
								: pos + i;

							tr.Set(folder.Tuples.EncodeKey(x, suffix), value);
							Interlocked.Increment(ref Keys);
						}
						pos += N;

						await tr.CommitAsync();
						Interlocked.Increment(ref Transactions);
						Interlocked.Add(ref Bytes, tr.Size);
					}
					catch (FdbException e)
					{
						error = e;
					}

					if (error != null && !ct.IsCancellationRequested)
					{
						await tr.OnErrorAsync(error.Code);
					}
				}
			}

		}

		public class Datum
		{
			public DateTime Date { get; set; }
			public long Keys { get; set; }
			public long Commits { get; set; }
			public long Bytes { get; set; }
			public long DiskWrites { get; set; }
		}

		public class Delta
		{
			public TimeSpan Delay { get; set; }
			public long Keys { get; set; }
			public long Commits { get; set; }
			public long Bytes { get; set; }
		}

		private static void WriteAt(int x, int y, string fmt, params object[] args)
		{
			Console.SetCursorPosition(x, y);
			Console.Write(String.Format(CultureInfo.InvariantCulture, fmt, args));
		}

		private static async Task MainAsync(string[] args, CancellationToken cancel)
		{
			Console.CursorVisible = false;
			Console.TreatControlCAsInput = true;
			try
			{
				CancellationTokenSource cts = null;
				Task burner = null;

				const int CAPACITY = 4 * 5;
				var history = new Queue<Datum>(CAPACITY);
				var heat = new Queue<Delta>(CAPACITY);

				using (var db = await Fdb.OpenAsync(ClusterPath, "DB", cancel))
				{
					db.DefaultTimeout = 10000;

					bool exit = false;
					bool hot = false;
					bool repaint = true;

					DateTime prevNow = DateTime.UtcNow;
					long prevKeys = 0;
					long prevTrans = 0;
					long prevBytes = 0;
					long prevDiskWrites = 0;

					var processName = Process.GetCurrentProcess().ProcessName;
					var perCpu = new PerformanceCounter("Process", "% Processor Time", processName);
					var perfDiskReads = new PerformanceCounter("PhysicalDisk", "Disk Read Bytes/sec", "0 C:");
					var perfDiskWrites = new PerformanceCounter("PhysicalDisk", "Disk Write Bytes/sec", "0 C:");

					const int COL0 = 1;
					const int COL1 = COL0 + 18;
					const int COL2 = COL1 + 18;
					const int COL3 = COL2 + 18;

					while (!exit && !cancel.IsCancellationRequested)
					{
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

								case ConsoleKey.R:
								{
									Randomized = !Randomized;
									repaint = true;
									break;
								}

								case ConsoleKey.Spacebar:
								{
									hot = !hot;
									repaint = true;
									if (hot)
									{
										cts = new CancellationTokenSource();
										burner = Task.Run(() => BurnerThread(db, cts.Token), cts.Token);
									}
									else
									{
										cts.Cancel();
										try { await burner; }
										catch (TaskCanceledException) { }
										cts.Dispose();
									}
									break;
								}

							}
						}

						if (repaint)
						{
							Console.Title = hot ? "FdbBurner - HOT HOT HOT" : "FdbBurner - ICY COLD";

							Console.BackgroundColor = hot ? ConsoleColor.DarkRed : ConsoleColor.DarkCyan;
							Console.Clear();
							Console.ForegroundColor = ConsoleColor.Gray;
							WriteAt(COL0, 3, "{0,-12}", "Transactions");
							WriteAt(COL1, 3, "{0,-12}", "Keys");
							WriteAt(COL2, 3, "{0,-12}", "Inserted Bytes");
							WriteAt(COL3, 3, "{0,-12}", "Disk Write Bytes");

							repaint = false;
						}

						await Task.Delay(250);

						long curKeys =  Volatile.Read(ref Keys);
						long curTrans = Volatile.Read(ref Transactions);
						long curBytes = Volatile.Read(ref Bytes);
						long curDiskWrites = (long)perfDiskWrites.NextValue();

						while (history.Count >= CAPACITY) history.Dequeue();
						while (heat.Count >= CAPACITY) heat.Dequeue();

						var now = DateTime.UtcNow;
						history.Enqueue(new Datum
						{
							Date = now,
							Keys = curKeys,
							Commits = curTrans,
							Bytes = curBytes,
							DiskWrites = curDiskWrites,
						});
						heat.Enqueue(new Delta
						{
							Delay = now - prevNow,
							Keys = curKeys - prevKeys,
							Commits = curTrans - prevTrans,
							Bytes = curBytes - prevBytes,
						});

						prevKeys = curKeys;
						prevTrans = curTrans;
						prevBytes = curBytes;
						prevDiskWrites = curDiskWrites;
						prevNow = now;

						Console.ForegroundColor = ConsoleColor.White;
						WriteAt(1, 1, "{0,-10}", Randomized ? "Random" : "Sequential");

						Console.ForegroundColor = ConsoleColor.White;
						WriteAt(COL0, 4, "{0,12:N0}", curTrans);
						WriteAt(COL1, 4, "{0,12:N0}", curKeys);
						WriteAt(COL2, 4, "{0,12:N3} MB", curBytes / 1048576.0);
						WriteAt(COL3, 4, "{0,12:N3} MB/s", curDiskWrites / 1048576.0);

						if (heat.Count > 1)
						{
							var old = history.Peek();
							var dur = (now - old.Date).TotalSeconds;
							double speed;

							speed = (curTrans - old.Commits) / dur;
							WriteAt(COL0, 6, "{0,12:N0}", speed);

							speed = (curKeys - old.Keys) / dur;
							WriteAt(COL1, 6, "{0,12:N0}", speed);

							speed = (curBytes - old.Bytes) / dur;
							WriteAt(COL2, 6, "{0,12:N3} MB/s", speed / 1048576.0);

							var speed2 = history.Average(d => d.DiskWrites);
							WriteAt(COL3, 6, "{0,12:N3} MB/s", speed2 / 1048576.0);

							var factor = speed > 0 ? speed2 / speed : 0;
							WriteAt(COL3, 7, "{0,12:N3} X", factor);
						}

						Console.SetCursorPosition(0, 0);
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

	}
}
