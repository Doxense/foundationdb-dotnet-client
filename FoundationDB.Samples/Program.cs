using FoundationDB.Async;
using FoundationDB.Client;
using FoundationDB.Filters.Logging;
using FoundationDB.Layers.Directories;
using FoundationDB.Layers.Tuples;
using FoundationDB.Samples.Benchmarks;
using FoundationDB.Samples.Tutorials;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace FoundationDB.Samples
{

	public interface IAsyncTest
	{
		string Name { get; }
		Task Run(FdbDatabasePartition db, TextWriter log, CancellationToken ct);
	}

	public static class TestRunner
	{
		public static void RunAsyncTest(IAsyncTest test, TextWriter log, FdbDatabasePartition db)
		{
			Console.WriteLine("Starting " + test.Name + " ...");

			if (log == null) log = Console.Out;

			var cts = new CancellationTokenSource();
			try
			{
				var t = test.Run(db, log, cts.Token);

				t.GetAwaiter().GetResult();
				Console.WriteLine("Completed " + test.Name + ".");
			}
			catch (Exception e)
			{
				Console.Error.WriteLine(e.ToString());
			}
			finally
			{
				cts.Dispose();
			}
		}

		public static async Task<TimeSpan> RunConcurrentWorkersAsync(int workers, 
			Func<int, CancellationToken, Task> handler, 
			CancellationToken ct)
		{
			await Task.Delay(1).ConfigureAwait(false);

			var signal = new AsyncCancelableMutex(ct);
			var tasks = Enumerable.Range(0, workers).Select(async (i) =>
			{
				await signal.Task;
				ct.ThrowIfCancellationRequested();
				await handler(i, ct);
			}).ToArray();

			var sw = Stopwatch.StartNew();
			signal.Set(async: true);
			await Task.WhenAll(tasks);
			sw.Stop();

			return sw.Elapsed;
		}

	}

	public static class PerfCounters
	{

		static PerfCounters()
		{
			var p = Process.GetCurrentProcess();
			ProcessName = p.ProcessName;
			ProcessId = p.Id;

			CategoryProcess = new PerformanceCounterCategory("Process");

			ProcessorTime = new PerformanceCounter("Process", "% Processor Time", ProcessName);
			UserTime = new PerformanceCounter("Process", "% User Time", ProcessName);

			PrivateBytes = new PerformanceCounter("Process", "Private Bytes", ProcessName);
			VirtualBytes = new PerformanceCounter("Process", "Virtual Bytes", ProcessName);
			VirtualBytesPeak = new PerformanceCounter("Process", "Virtual Bytes Peak", ProcessName);
			WorkingSet = new PerformanceCounter("Process", "Working Set", ProcessName);
			WorkingSetPeak = new PerformanceCounter("Process", "Working Set Peak", ProcessName);
			HandleCount = new PerformanceCounter("Process", "Handle Count", ProcessName);

			CategoryNetClrMemory = new PerformanceCounterCategory(".NET CLR Memory");
			ClrBytesInAllHeaps = new PerformanceCounter(".NET CLR Memory", "# Bytes in all Heaps", ProcessName);
			ClrTimeInGC = new PerformanceCounter(".NET CLR Memory", "% Time in GC", ProcessName);
			ClrGen0Collections = new PerformanceCounter(".NET CLR Memory", "# Gen 0 Collections", p.ProcessName, true);
			ClrGen1Collections = new PerformanceCounter(".NET CLR Memory", "# Gen 1 Collections", p.ProcessName, true);
			ClrGen2Collections = new PerformanceCounter(".NET CLR Memory", "# Gen 1 Collections", p.ProcessName, true);
		}

		public static readonly string ProcessName;
		public static readonly int ProcessId;

		public static readonly PerformanceCounterCategory CategoryProcess;
		public static readonly PerformanceCounter ProcessorTime;
		public static readonly PerformanceCounter UserTime;
		public static readonly PerformanceCounter PrivateBytes;
		public static readonly PerformanceCounter VirtualBytes;
		public static readonly PerformanceCounter VirtualBytesPeak;
		public static readonly PerformanceCounter WorkingSet;
		public static readonly PerformanceCounter WorkingSetPeak;
		public static readonly PerformanceCounter HandleCount;

		public static readonly PerformanceCounterCategory CategoryNetClrMemory;
		public static readonly PerformanceCounter ClrBytesInAllHeaps;
		public static readonly PerformanceCounter ClrTimeInGC;
		public static readonly PerformanceCounter ClrGen0Collections;
		public static readonly PerformanceCounter ClrGen1Collections;
		public static readonly PerformanceCounter ClrGen2Collections;

	}

	class Program
	{
		private static FdbDatabasePartition Db;

		private static string CurrentDirectoryPath = "/";

		static StreamWriter GetLogFile(string name)
		{
			long localTime = (DateTime.Now.Ticks / TimeSpan.TicksPerMillisecond) - 62135596800000;
			long utcTime = (DateTime.UtcNow.Ticks / TimeSpan.TicksPerMillisecond) - 62135596800000;

			string path = name + "_" + utcTime + ".log";
			var stream = System.IO.File.CreateText(path);

			stream.WriteLine("# File: " + name);
			stream.WriteLine("# Local Time: " + DateTime.Now.ToString("O") + " (" + localTime + " local) - Universal Time: " + DateTime.UtcNow.ToString("O") + " ( " + utcTime + " UTC)");
			stream.Flush();

			Console.WriteLine("> using log file " + path);

			return stream;
		}

		static FdbDatabasePartition GetLoggedDatabase(FdbDatabasePartition db, StreamWriter stream, bool autoFlush = false)
		{
			if (stream == null) return db;

			return new FdbDatabasePartition(
				new FdbLoggedDatabase(db, false, false, (tr) => { stream.WriteLine(tr.Log.GetTimingsReport(true)); if (autoFlush) stream.Flush(); }),
				db.Root
			);
		}

		static void Main(string[] args)
		{
			bool stop = false;

			string clusterFile = null;
			string dbName = "DB";

			// Initialize FDB

			string initial = null;
			if (args.Length > 0) initial = String.Join(" ", args);

			bool logEnabled = false;

			Fdb.Start();
			try
			{
				Db = Fdb.PartitionTable.OpenNamedPartitionAsync(clusterFile, dbName, FdbTuple.Create("Samples")).Result;
				using (Db)
				{
					Db.DefaultTimeout = 30 * 1000;
					Db.DefaultRetryLimit = 10;

					Console.WriteLine("Using API v" + Fdb.GetMaxApiVersion());
					Console.WriteLine("Cluster file: " + (clusterFile ?? "<default>"));
					var cf = Db.GetCoordinatorsAsync().GetAwaiter().GetResult();
					Console.WriteLine("Connnected to: " + cf.Description + " (" + cf.Id + ")");
					foreach (var coordinator in cf.Coordinators)
					{
						var iphost = Dns.GetHostEntry(coordinator.Address);
						Console.WriteLine("> " + coordinator.Address + ":" + coordinator.Port + " (" + iphost.HostName + ")");
					}
					Console.WriteLine();
					Console.WriteLine("FoundationDB Samples menu:");
					Console.WriteLine("\t1\tRun Class Schedudling sample");
					Console.WriteLine("\t2\tBrowser Directory Layer");
					Console.WriteLine("\tL\tRun Leak test");
					Console.WriteLine("\tgc\tTrigger garbage collection");
					Console.WriteLine("\tmem\tMemory usage statistics");
					Console.WriteLine("\tq\tQuit");

					Console.WriteLine("Ready...");

					while (!stop)
					{
						Console.Write("> ");
						string s = initial != null ? initial : Console.ReadLine();
						initial = null;

						var tokens = s.Trim().Split(" ".ToCharArray(), StringSplitOptions.RemoveEmptyEntries);
						string cmd = tokens.Length > 0 ? tokens[0] : String.Empty;
						string prm = tokens.Length > 1 ? tokens[1] : String.Empty;

						switch (cmd.Trim().ToLowerInvariant())
						{
							case "":
							{
								continue;
							}
							case "1":
							{ // Class Scheduling

								TestRunner.RunAsyncTest(new ClassScheduling(), null, Db);
								break;
							}
							case "2":
							{ // Directory Layer
								//TODO!
								Console.WriteLine("NOT IMPLEMENTED");
								break;
							}

							case "log":
							{
								switch(prm.ToLowerInvariant())
								{
									case "on":
									{
										logEnabled = true;
										Console.WriteLine("Logging is ON");
										break;
									}
									case "off":
									{
										logEnabled = false;
										Console.WriteLine("Logging is OFF");
										break;
									}
								}
								break;
							}

							case "dir":
							{
								prm = CombinePath(CurrentDirectoryPath, prm);
								BrowseDirectory(prm, null, Db).GetAwaiter().GetResult();
								break;
							}
							case "cd":
							case "pwd":
							{
								if (!string.IsNullOrEmpty(prm))
								{
									CurrentDirectoryPath = CombinePath(CurrentDirectoryPath, prm);
									Console.WriteLine("Directory changed to {0}", CurrentDirectoryPath);
								}
								else
								{
									Console.WriteLine("Current directory is {0}", CurrentDirectoryPath);
								}
								break;
							}
							case "mkdir":
							{
								if (!string.IsNullOrEmpty(prm))
								{
									prm = CombinePath(CurrentDirectoryPath, prm);
									string layer = null;
									if (tokens.Length > 2)
									{
										layer = tokens[2].Trim();
									}
									CreateDirectory(prm, layer, null, Db).GetAwaiter().GetResult();
								}
								break;
							}

							case "bench":
							{ // Benchs

								switch(prm.ToLowerInvariant())
								{
									case "read":
									{
										using (var stream = logEnabled ? GetLogFile("bench_readversion") : null)
										{
											TestRunner.RunAsyncTest(
												new BenchRunner(BenchRunner.BenchMode.GetReadVersion),
												stream,
												GetLoggedDatabase(Db, stream)
											);
										}
										break;
									}
									case "get":
									{
										using (var stream = logEnabled ? GetLogFile("bench_get") : null)
										{
											TestRunner.RunAsyncTest(
												new BenchRunner(BenchRunner.BenchMode.Get),
												stream,
												GetLoggedDatabase(Db, stream)
											);
										}
										break;
									}
									case "get10":
									{
										using (var stream = logEnabled ? GetLogFile("bench_get10") : null)
										{
											TestRunner.RunAsyncTest(
												new BenchRunner(BenchRunner.BenchMode.Get, 10),
												stream,
												GetLoggedDatabase(Db, stream)
											);
										}
										break;
									}
									case "set":
									{ // Bench Set
										using (var stream = logEnabled ? GetLogFile("bench_set") : null)
										{
											TestRunner.RunAsyncTest(
												new BenchRunner(BenchRunner.BenchMode.Set),
												stream,
												GetLoggedDatabase(Db, stream)
											);
										}
										break;
									}
									case "watch":
									{ // Bench Set
										using (var stream = logEnabled ? GetLogFile("bench_watch") : null)
										{
											TestRunner.RunAsyncTest(
												new BenchRunner(BenchRunner.BenchMode.Watch),
												stream,
												GetLoggedDatabase(Db, stream)
											);
										}
										break;
									}
								}

								break;
							}

							case "msg":
							{
								switch(prm.ToLowerInvariant())
								{
									case "producer":
									{ // Queue Producer
										using (var stream = logEnabled ? GetLogFile("producer_" + PerfCounters.ProcessId) : null)
										{
											//var dbp = Db;
											TestRunner.RunAsyncTest(
												new MessageQueueRunner(PerfCounters.ProcessName + "[" + PerfCounters.ProcessId + "]", MessageQueueRunner.AgentRole.Producer, TimeSpan.FromMilliseconds(50), TimeSpan.FromMilliseconds(200)), 
												stream,
												GetLoggedDatabase(Db, stream)
											);
										}
										break;
									}
									case "worker":
									{ // Queue Worker
										using (var stream = logEnabled ? GetLogFile("worker_" + PerfCounters.ProcessId) : null)
										{
											TestRunner.RunAsyncTest(
												new MessageQueueRunner(PerfCounters.ProcessName + "[" + PerfCounters.ProcessId + "]", MessageQueueRunner.AgentRole.Worker, TimeSpan.FromMilliseconds(10), TimeSpan.FromMilliseconds(10)),
												stream,
												GetLoggedDatabase(Db, stream)
											);
										}
										break;
									}
									case "clear":
									{ // Queue Clear
										TestRunner.RunAsyncTest(
											new MessageQueueRunner(PerfCounters.ProcessName + "[" + PerfCounters.ProcessId + "]", MessageQueueRunner.AgentRole.Clear, TimeSpan.Zero, TimeSpan.Zero),
											null,
											Db
										);
										break;
									}
									case "status":
									{ // Queue Status
										TestRunner.RunAsyncTest(
											new MessageQueueRunner(PerfCounters.ProcessName + "[" + PerfCounters.ProcessId + "]", MessageQueueRunner.AgentRole.Status, TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(10)), 
											null,
											Db
										);
										break;
									}
								}
								break;
							}

							case "l":
							{ // LeastTest
								TestRunner.RunAsyncTest(new LeakTest(100, 100, 1000, TimeSpan.FromSeconds(30)), null, Db);
								break;
							}

							case "q":
							case "x":
							case "quit":
							case "exit":
							{
								stop = true;
								break;
							}

							case "gc":
							{
								long before = GC.GetTotalMemory(false);
								Console.Write("Collecting garbage...");
								GC.Collect();
								GC.WaitForPendingFinalizers();
								GC.Collect();
								Console.WriteLine(" Done");
								long after = GC.GetTotalMemory(false);
								Console.WriteLine("- before = " + before.ToString("N0"));
								Console.WriteLine("- after  = " + after.ToString("N0"));
								Console.WriteLine("- delta  = " + (before - after).ToString("N0"));
								break;
							}

							case "mem":
							{
								Console.WriteLine("Memory usage:");
								Console.WriteLine("- Working Set  : " + PerfCounters.WorkingSet.NextValue().ToString("N0") + " (peak " + PerfCounters.WorkingSetPeak.NextValue().ToString("N0") + ")");
								Console.WriteLine("- Virtual Bytes: " + PerfCounters.VirtualBytes.NextValue().ToString("N0") + " (peak " + PerfCounters.VirtualBytesPeak.NextValue().ToString("N0") + ")");
								Console.WriteLine("- Private Bytes: " + PerfCounters.PrivateBytes.NextValue().ToString("N0"));
								Console.WriteLine("- Managed Mem  : " + GC.GetTotalMemory(false).ToString("N0"));
								Console.WriteLine("- BytesInAlHeap: " + PerfCounters.ClrBytesInAllHeaps.NextValue().ToString("N0"));
								break;
							}

							default:
							{
								Console.WriteLine("Unknown command");
								break;
							}
						}
					}
				}
			}
			finally
			{
				Fdb.Stop();
				Console.WriteLine("Bye");
			}
		}

		private static string CombinePath(string parent, string children)
		{
			if (string.IsNullOrEmpty(children) || children == ".") return parent;
			if (children.StartsWith("/")) return children;
			return System.IO.Path.GetFullPath(System.IO.Path.Combine(parent, children)).Replace("\\", "/").Substring(2);
		}

		private static IFdbTuple ParsePath(string path)
		{
			path = path.Replace("\\", "/").Trim();
			return FdbTuple.CreateRange<string>(path.Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries));
		}

		private static async Task CreateDirectory(string prm, string layer, TextWriter stream, FdbDatabasePartition db)
		{
			if (stream == null) stream = Console.Out;

			var path = ParsePath(prm);
			stream.WriteLine("# Creating directory {0} with layer '{1}'", prm, layer);

			var folder = await db.Root.TryOpenAsync(db, path);
			if (folder != null)
			{
				stream.WriteLine("- Directory already exists!", prm);
				return;
			}

			folder = await db.Root.TryCreateAsync(db, path, layer);
			stream.WriteLine("- Created under {0} [{1}]", FdbKey.Dump(folder.Key), folder.Key.ToHexaString());

			// look if there is already stuff under there
			var stuff = await db.ReadAsync((tr) => tr.GetRange(folder.ToRange()).FirstOrDefaultAsync());
			if (stuff.Key.IsPresent)
			{
				stream.WriteLine("CAUTION: There is already some data under {0} !");
				stream.WriteLine("  {0} = {1}", FdbKey.Dump(stuff.Key), stuff.Value.ToAsciiOrHexaString());
			}
		}

		private static async Task BrowseDirectory(string prm, TextWriter stream, FdbDatabasePartition db)
		{
			if (stream == null) stream = Console.Out;

			var path = ParsePath(prm);
			stream.WriteLine("# Listing {0}:", prm);

			var folders = await db.Root.ListAsync(db, path);
			if (folders.Count > 0)
			{
				foreach (var name in folders)
				{
					var subfolder = await db.Root.OpenAsync(db, path.Concat(name));
					stream.WriteLine("  {0,-12} {1,-12} {2}", FdbKey.Dump(subfolder.Key), string.IsNullOrEmpty(subfolder.Layer) ? "-" : ("<" + subfolder.Layer + ">"), name.Get<string>(0));
				}
				stream.WriteLine("- {0} sub-directories(s).", folders.Count);
			}
			else
			{
				stream.WriteLine("- No subfolders.");
			}

			if (prm != "/")
			{
				// look if there is something under there
				var folder = await db.Root.TryOpenAsync(db, path);
				if (folder != null)
				{
					stream.WriteLine("# Content of {0}:", FdbKey.Dump(folder.Key));
					var keys = await db.ReadAsync((tr) => tr.GetRange(folder.ToRange()).Take(21).ToListAsync());
					if (keys.Count > 0)
					{
						foreach(var key in keys.Take(20))
						{
							stream.WriteLine("  {0} = {1}", FdbKey.Dump(key.Key), key.Value.ToAsciiOrHexaString());
						}
						if (keys.Count == 21)
						{
							stream.WriteLine("  ...");
						}
					}
					else
					{
						stream.WriteLine("- no content found");
					}
				}
			}
		}
	}
}
