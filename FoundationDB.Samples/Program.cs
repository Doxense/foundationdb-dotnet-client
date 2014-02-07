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

	public class Program
	{
		private static FdbDatabasePartition Db;

		private static bool LogEnabled = false;
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
				db.Root,
				false
			);
		}

		public static void RunAsyncCommand(Func<FdbDatabasePartition, TextWriter, CancellationToken, Task> command)
		{
			TextWriter log = null;
			FdbDatabasePartition db = Db;
			if (log == null) log = Console.Out;

			var cts = new CancellationTokenSource();
			try
			{
				var t = command(db, log, cts.Token);
				t.GetAwaiter().GetResult();
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

		public static void RunAsyncTest(IAsyncTest test)
		{
			Console.WriteLine("# Running {0} ...");

			using (var log = LogEnabled ? GetLogFile(test.Name) : null)
			{
				var db = GetLoggedDatabase(Db, log);

				var cts = new CancellationTokenSource();
				try
				{
					var t = test.Run(db, log ?? Console.Out, cts.Token);

					t.GetAwaiter().GetResult();
					Console.WriteLine("# Completed {0}.", test.Name);
				}
				catch (Exception e)
				{
					Console.Error.WriteLine("# {0} FAILED: {1}", test.Name, e.ToString());
				}
				finally
				{
					cts.Dispose();
				}
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

		static void Main(string[] args)
		{
			bool stop = false;

			string clusterFile = null;
			var dbName = "DB";
			var partition = new [] { "Samples" };

			// Initialize FDB

			string initial = null;
			if (args.Length > 0) initial = String.Join(" ", args);

			Fdb.Start();
			try
			{
				if (partition == null || partition.Length == 0)
				{
					Db = Fdb.PartitionTable.OpenRootAsync(clusterFile, dbName).GetAwaiter().GetResult();
				}
				else
				{
					Db = Fdb.PartitionTable.OpenNamedPartitionAsync(clusterFile, dbName, partition).GetAwaiter().GetResult();
				}
				using (Db)
				{
					Db.DefaultTimeout = 30 * 1000;
					Db.DefaultRetryLimit = 10;

					Console.WriteLine("Using API v" + Fdb.ApiVersion + " (max " + Fdb.GetMaxApiVersion() + ")");
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
					Console.WriteLine("\tL\tRun Leak test");
					Console.WriteLine("\tdir\tBrowse directories");
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

								RunAsyncTest(new ClassScheduling());
								break;
							}

							case "log":
							{
								switch(prm.ToLowerInvariant())
								{
									case "on":
									{
										LogEnabled = true;
										Console.WriteLine("# Logging enabled");
										break;
									}
									case "off":
									{
										LogEnabled = false;
										Console.WriteLine("# Logging disabled");
										break;
									}
									default:
									{
										Console.WriteLine("# Logging is {0}", LogEnabled ? "ON" : "OFF");
										break;
									}
								}
								break;
							}

							case "tree":
							{
								prm = CombinePath(CurrentDirectoryPath, prm);
								RunAsyncCommand((db, log, ct) => TreeDirectory(prm, db, log, ct));
								break;
							}
							case "dir":
							{
								prm = CombinePath(CurrentDirectoryPath, prm);
								RunAsyncCommand((db, log, ct) => BrowseDirectory(prm, db, log, ct));
								break;
							}
							case "cd":
							case "pwd":
							{
								if (!string.IsNullOrEmpty(prm))
								{
									CurrentDirectoryPath = CombinePath(CurrentDirectoryPath, prm);
									Console.WriteLine("# Directory changed to {0}", CurrentDirectoryPath);
								}
								else
								{
									Console.WriteLine("# Current directory is {0}", CurrentDirectoryPath);
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
									RunAsyncCommand((db, log, ct) => CreateDirectory(prm, layer, db, log, ct));
								}
								break;
							}

							case "bench":
							{ // Benchs

								switch(prm.ToLowerInvariant())
								{
									case "read":
									{
										RunAsyncTest(new BenchRunner(BenchRunner.BenchMode.GetReadVersion));
										break;
									}
									case "get":
									{
										RunAsyncTest(new BenchRunner(BenchRunner.BenchMode.Get));
										break;
									}
									case "get10":
									{
										RunAsyncTest(new BenchRunner(BenchRunner.BenchMode.Get, 10));
										break;
									}
									case "set":
									{ // Bench Set
										RunAsyncTest(new BenchRunner(BenchRunner.BenchMode.Set));
										break;
									}
									case "watch":
									{ // Bench Set
										RunAsyncTest(new BenchRunner(BenchRunner.BenchMode.Watch));
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
										RunAsyncTest(new MessageQueueRunner(PerfCounters.ProcessName + "[" + PerfCounters.ProcessId + "]", MessageQueueRunner.AgentRole.Producer, TimeSpan.FromMilliseconds(50), TimeSpan.FromMilliseconds(200)));
										break;
									}
									case "worker":
									{ // Queue Worker
										RunAsyncTest(new MessageQueueRunner(PerfCounters.ProcessName + "[" + PerfCounters.ProcessId + "]", MessageQueueRunner.AgentRole.Worker, TimeSpan.FromMilliseconds(10), TimeSpan.FromMilliseconds(10)));
										break;
									}
									case "clear":
									{ // Queue Clear
										RunAsyncTest(new MessageQueueRunner(PerfCounters.ProcessName + "[" + PerfCounters.ProcessId + "]", MessageQueueRunner.AgentRole.Clear, TimeSpan.Zero, TimeSpan.Zero));
										break;
									}
									case "status":
									{ // Queue Status
										RunAsyncTest(new MessageQueueRunner(PerfCounters.ProcessName + "[" + PerfCounters.ProcessId + "]", MessageQueueRunner.AgentRole.Status, TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(10)));
										break;
									}
								}
								break;
							}

							case "leak":
							{ // LeastTest
								switch(prm.ToLowerInvariant())
								{
									case "fast": RunAsyncTest(new LeakTest(100, 100, 1000, TimeSpan.FromSeconds(0))); break;
									case "slow": RunAsyncTest(new LeakTest(100, 100, 1000, TimeSpan.FromSeconds(30))); break;
									default: RunAsyncTest(new LeakTest(100, 100, 1000, TimeSpan.FromSeconds(1))); break;
								}							
								break;
							}

							case "sampling":
							{ // SamplingTest
								RunAsyncTest(new SamplerTest(0.1));
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

		#region Directories...

		private static string CombinePath(string parent, string children)
		{
			if (string.IsNullOrEmpty(children) || children == ".") return parent;
			if (children.StartsWith("/")) return children;
			return System.IO.Path.GetFullPath(System.IO.Path.Combine(parent, children)).Replace("\\", "/").Substring(2);
		}

		private static string[] ParsePath(string path)
		{
			path = path.Replace("\\", "/").Trim();
			return path.Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
		}

		public static async Task CreateDirectory(string prm, string layer, FdbDatabasePartition db, TextWriter stream, CancellationToken ct)
		{
			if (stream == null) stream = Console.Out;

			var path = ParsePath(prm);
			stream.WriteLine("# Creating directory {0} with layer '{1}'", prm, layer);

			var folder = await db.Root.TryOpenAsync(db, path, cancellationToken: ct);
			if (folder != null)
			{
				stream.WriteLine("- Directory {0} already exists!", prm);
				return;
			}

			folder = await db.Root.TryCreateAsync(db, path, Slice.FromString(layer), cancellationToken: ct);
			stream.WriteLine("- Created under {0} [{1}]", FdbKey.Dump(folder.Key), folder.Key.ToHexaString(' '));

			// look if there is already stuff under there
			var stuff = await db.ReadAsync((tr) => tr.GetRange(folder.ToRange()).FirstOrDefaultAsync(), cancellationToken: ct);
			if (stuff.Key.IsPresent)
			{
				stream.WriteLine("CAUTION: There is already some data under {0} !");
				stream.WriteLine("  {0} = {1}", FdbKey.Dump(stuff.Key), stuff.Value.ToAsciiOrHexaString());
			}
		}

		public static async Task BrowseDirectory(string prm, FdbDatabasePartition db, TextWriter stream, CancellationToken ct)
		{
			if (stream == null) stream = Console.Out;

			var path = ParsePath(prm);
			stream.WriteLine("# Listing {0}:", prm);

			var folders = await db.Root.TryListAsync(db, path);
			if (folders == null)
			{
				stream.WriteLine("  Directory not found.");
			}
			else if (folders.Count > 0)
			{
				foreach (var name in folders)
				{
					var subfolder = await db.Root.TryOpenAsync(db, path.Concat(new [] { name }), cancellationToken: ct);
					if (subfolder != null)
					{
						stream.WriteLine("  {0,-12} {1,-12} {2}", FdbKey.Dump(subfolder.Key), subfolder.Layer.IsNullOrEmpty ? "-" : ("<" + subfolder.Layer.ToUnicode() + ">"), name);
					}
					else
					{
						stream.WriteLine("  WARNING: {0} seems to be missing!", name);
					}
				}
				stream.WriteLine("  {0} sub-directorie(s).", folders.Count);
			}
			else
			{
				stream.WriteLine("  No sub-directories.");
			}

			if (prm != "/")
			{
				// look if there is something under there
				var folder = await db.Root.TryOpenAsync(db, path, cancellationToken: ct);
				if (folder != null)
				{
					stream.WriteLine("# Content of {0} [{1}]", FdbKey.Dump(folder.Key), folder.Key.ToHexaString(' '));
					var keys = await db.ReadAsync((tr) => tr.GetRange(folder.ToRange()).Take(21).ToListAsync(), cancellationToken: ct);
					if (keys.Count > 0)
					{
						foreach(var key in keys.Take(20))
						{
							stream.WriteLine("  ...{0} = {1}", FdbKey.Dump(folder.Extract(key.Key)), key.Value.ToAsciiOrHexaString());
						}
						if (keys.Count == 21)
						{
							stream.WriteLine("  ... more");
						}
					}
					else
					{
						stream.WriteLine("  no content found");
					}
				}
			}
		}

		public static async Task TreeDirectory(string prm, FdbDatabasePartition db, TextWriter stream, CancellationToken ct)
		{
			if (stream == null) stream = Console.Out;

			var path = ParsePath(prm);
			stream.WriteLine("# Tree of {0}:", prm);

			await TreeDirectoryWalk(path, new List<bool>(), db, stream, ct);

			stream.WriteLine("# done");
		}

		private static async Task TreeDirectoryWalk(string[] path, List<bool> last, FdbDatabasePartition db, TextWriter stream, CancellationToken ct)
		{
			ct.ThrowIfCancellationRequested();

			var sb = new StringBuilder(last.Count * 4);
			if (last.Count > 0)
			{
				for (int i = 0; i < last.Count - 1; i++) sb.Append(last[i] ? "    " : "|   ");
				sb.Append(last[last.Count - 1] ? "`-- " : "|-- ");
			}
			stream.WriteLine(sb.ToString() + (path.Length == 0 ? "<root>" : path[path.Length - 1]));

			var children = await db.Root.TryListAsync(db, path);
			int n = children.Count;
			foreach(var child in children)
			{
				last.Add((n--) == 1);
				await TreeDirectoryWalk(path.Concat(new [] { child }).ToArray(), last, db, stream, ct);
				last.RemoveAt(last.Count - 1);
			}
		}

		#endregion

	}
}
