﻿//TODO: License for samples/tutorials ???

namespace FoundationDB.Samples
{
	using System;
	using System.Diagnostics;
	using System.IO;
	using System.Linq;
	using System.Threading;
	using System.Threading.Tasks;
	using Doxense.Async;
	using FoundationDB.Client;
	using FoundationDB.Filters.Logging;
	using FoundationDB.Samples.Benchmarks;
	using FoundationDB.Samples.Tutorials;

	public interface IAsyncTest
	{
		string Name { get; }
		Task Run(IFdbDatabase db, TextWriter log, CancellationToken ct);
	}

	public class Program
	{
		private static IFdbDatabase Db;

		private static bool LogEnabled;

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

		static IFdbDatabase GetLoggedDatabase(IFdbDatabase db, StreamWriter stream, bool autoFlush = false)
		{
			if (stream != null)
			{
				db.SetDefaultLogHandler(log => { stream.WriteLine(log.GetTimingsReport(true)); if (autoFlush) stream.Flush(); });
			}
			return db;
		}

		public static void RunAsyncCommand(Func<IFdbDatabase, TextWriter, CancellationToken, Task> command)
		{
			TextWriter log = null;
			var db = Db;
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

		public static void RunAsyncTest(IAsyncTest test, CancellationToken ct)
		{
			Console.WriteLine("# Running {0} ...");

			using (var log = LogEnabled ? GetLogFile(test.Name) : null)
			{
				var db = GetLoggedDatabase(Db, log);

				var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
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
			var partition = FdbPath.Root;

			int pStart = 0;
			string startCommand = null;
			while (pStart < args.Length)
			{
				if (args[pStart].StartsWith("-"))
				{
					switch (args[pStart].Substring(1))
					{
						case "C": case "c":
						{
							clusterFile = args[pStart + 1];
							pStart += 2;
							break;
						}
						case "P": case "p":
						{
							partition = FdbPath.Parse(args[pStart + 1].Trim());
							pStart += 2;
							break;
						}
						default:
						{
							Console.WriteLine($"Unknown option : '{args[pStart]}'");
							pStart++;
							break;
						}
					}
				}
				else
				{
					break;
				}
			}

			if (args.Length > 0 && pStart < args.Length)
			{ // the remainder of the command line will be the first command to execute
				startCommand = String.Join(" ", args, pStart, args.Length - pStart);
			}

			var go = new CancellationTokenSource();

			// Initialize FDB
			Fdb.Start(Fdb.GetDefaultApiVersion());
			try
			{
				var options = new FdbConnectionOptions
				{
					ClusterFile = clusterFile,
					Root = partition,
				};
				Db = Fdb.OpenAsync(options, go.Token).GetAwaiter().GetResult();

				using (Db)
				{
					Db.DefaultTimeout = 30 * 1000;
					Db.DefaultRetryLimit = 10;

					Console.WriteLine("Using API v" + Fdb.ApiVersion + " (max " + Fdb.GetMaxApiVersion() + ")");
					Console.WriteLine("Cluster file: " + (clusterFile ?? "<default>"));

					Console.WriteLine();
					Console.WriteLine("FoundationDB Samples menu:");
					Console.WriteLine("\t1\tRun Class Scheduling sample");
					Console.WriteLine("\tL\tRun Leak test");
					Console.WriteLine("\tbench\tRun synthetic benchmarks");
					Console.WriteLine("\tgc\tTrigger a .NET garbage collection");
					Console.WriteLine("\tmem\tDisplay memory usage statistics");
					Console.WriteLine("\tq\tQuit");

					Console.WriteLine("Ready...");

					while (!stop)
					{
						Console.Write("> ");
						string s = startCommand != null ? startCommand : Console.ReadLine();
						startCommand = null;

						var tokens = s.Trim().Split(" ".ToCharArray(), StringSplitOptions.RemoveEmptyEntries);
						string cmd = tokens.Length > 0 ? tokens[0] : String.Empty;
						string prm = tokens.Length > 1 ? tokens[1] : String.Empty;

						var trimmedCommand = cmd.Trim().ToLowerInvariant();
						switch (trimmedCommand)
						{
							case "":
							{
								continue;
							}
							case "1":
							{ // Class Scheduling

								RunAsyncTest(new ClassScheduling(), go.Token);
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

							case "bench":
							{ // Benchs

								switch(prm.ToLowerInvariant())
								{
									case "read":
									{
										RunAsyncTest(new BenchRunner(BenchRunner.BenchMode.GetReadVersion), go.Token);
										break;
									}
									case "get":
									{
										RunAsyncTest(new BenchRunner(BenchRunner.BenchMode.Get), go.Token);
										break;
									}
									case "get10":
									{
										RunAsyncTest(new BenchRunner(BenchRunner.BenchMode.Get, 10), go.Token);
										break;
									}
									case "set":
									{ // Bench Set
										RunAsyncTest(new BenchRunner(BenchRunner.BenchMode.Set), go.Token);
										break;
									}
									case "watch":
									{ // Bench Set
										RunAsyncTest(new BenchRunner(BenchRunner.BenchMode.Watch), go.Token);
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
										RunAsyncTest(new MessageQueueRunner(PerfCounters.ProcessName + "[" + PerfCounters.ProcessId + "]", MessageQueueRunner.AgentRole.Producer, TimeSpan.FromMilliseconds(50), TimeSpan.FromMilliseconds(200)), go.Token);
										break;
									}
									case "worker":
									{ // Queue Worker
										RunAsyncTest(new MessageQueueRunner(PerfCounters.ProcessName + "[" + PerfCounters.ProcessId + "]", MessageQueueRunner.AgentRole.Worker, TimeSpan.FromMilliseconds(10), TimeSpan.FromMilliseconds(10)), go.Token);
										break;
									}
									case "clear":
									{ // Queue Clear
										RunAsyncTest(new MessageQueueRunner(PerfCounters.ProcessName + "[" + PerfCounters.ProcessId + "]", MessageQueueRunner.AgentRole.Clear, TimeSpan.Zero, TimeSpan.Zero), go.Token);
										break;
									}
									case "status":
									{ // Queue Status
										RunAsyncTest(new MessageQueueRunner(PerfCounters.ProcessName + "[" + PerfCounters.ProcessId + "]", MessageQueueRunner.AgentRole.Status, TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(10)), go.Token);
										break;
									}
								}
								break;
							}

							case "leak":
							{ // LeastTest
								switch(prm.ToLowerInvariant())
								{
									case "fast": RunAsyncTest(new LeakTest(100, 100, 1000, TimeSpan.FromSeconds(0)), go.Token); break;
									case "slow": RunAsyncTest(new LeakTest(100, 100, 1000, TimeSpan.FromSeconds(30)), go.Token); break;
									default: RunAsyncTest(new LeakTest(100, 100, 1000, TimeSpan.FromSeconds(1)), go.Token); break;
								}							
								break;
							}

							case "sampling":
							{ // SamplingTest
								RunAsyncTest(new SamplerTest(0.1), go.Token);
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
								Console.WriteLine("- Managed Mem  : " + GC.GetTotalMemory(false).ToString("N0"));
#if !NETCOREAPP
								Console.WriteLine("- Working Set  : " + PerfCounters.WorkingSet.NextValue().ToString("N0") + " (peak " + PerfCounters.WorkingSetPeak.NextValue().ToString("N0") + ")");
								Console.WriteLine("- Virtual Bytes: " + PerfCounters.VirtualBytes.NextValue().ToString("N0") + " (peak " + PerfCounters.VirtualBytesPeak.NextValue().ToString("N0") + ")");
								Console.WriteLine("- Private Bytes: " + PerfCounters.PrivateBytes.NextValue().ToString("N0"));
								Console.WriteLine("- BytesInAlHeap: " + PerfCounters.ClrBytesInAllHeaps.NextValue().ToString("N0"));
#endif
								break;
							}

							default:
							{
								Console.WriteLine(string.Format("Unknown command : '{0}'", trimmedCommand));
								break;
							}
						}
					}
				}
			}
			finally
			{
				go.Cancel();
				Fdb.Stop();
				Console.WriteLine("Bye");
			}
		}

	}
}
