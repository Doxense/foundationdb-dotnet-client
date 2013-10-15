using FoundationDB.Async;
using FoundationDB.Client;
using FoundationDB.Layers.Directories;
using FoundationDB.Layers.Tuples;
using FoundationDB.Samples.Tutorials;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace FoundationDB.Samples
{

	public interface IAsyncTest
	{
		string Name { get; }
		Task Run(FdbDatabasePartition db, CancellationToken ct);
	}

	public static class TestRunner
	{
		public static void RunAsyncTest(IAsyncTest test, FdbDatabasePartition db)
		{
			Console.WriteLine("Starting " + test.Name + " ...");

			var cts = new CancellationTokenSource();
			try
			{
				var t = test.Run(db, cts.Token);

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

		static void Main(string[] args)
		{
			bool stop = false;

			string clusterFile = null;
			string dbName = "DB";

			// Initialize FDB

			Fdb.Start();
			try
			{

				Db = Fdb.PartitionTable.OpenNamedPartitionAsync(clusterFile, dbName, FdbTuple.Create("Samples")).Result;
				using (Db)
				{

					Console.WriteLine("Using API v" + Fdb.GetMaxApiVersion());
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
						string s = Console.ReadLine();

						switch (s.Trim().ToLowerInvariant())
						{
							case "":
							{
								continue;
							}
							case "1":
							{ // Class Scheduling

								TestRunner.RunAsyncTest(new ClassScheduling(), Db);
								break;
							}
							case "2":
							{ // Directory Layer
								//TODO!
								Console.WriteLine("NOT IMPLEMENTED");
								break;
							}
							case "l":
							{ // LeastTest
								TestRunner.RunAsyncTest(new LeakTest(100, 100, 1000, TimeSpan.FromSeconds(30)), Db);
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
	}
}
