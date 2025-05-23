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

namespace FoundationDB.Samples
{
	using FoundationDB.Samples.Benchmarks;
	using FoundationDB.Samples.Tutorials;
	using SnowBank.Linq.Async;

	public interface IAsyncTest
	{
		string Name { get; }
		Task Run(IFdbDatabase db, TextWriter log, CancellationToken ct);
	}

	public class Program
	{

		private static IFdbDatabase? Db;

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

		static IFdbDatabase GetLoggedDatabase(IFdbDatabase db, StreamWriter? stream, bool autoFlush = false)
		{
			if (stream != null)
			{
				db.SetDefaultLogHandler(log =>
				{
					stream.WriteLine(log.GetTimingsReport(true));
					if (autoFlush)
					{
						stream.Flush();
					}
				});
			}
			return db;
		}

		public static void RunAsyncCommand(Func<IFdbDatabase, TextWriter, CancellationToken, Task> command)
		{
			TextWriter? log = null;
			var db = Db ?? throw new InvalidOperationException();
			log ??= Console.Out;

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
				var db = GetLoggedDatabase(Db ?? throw new InvalidOperationException(), log);

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

			string? clusterFile = null;
			var partition = FdbPath.Root;

			int pStart = 0;
			string? startCommand = null;
			while (pStart < args.Length)
			{
				if (args[pStart].StartsWith('-'))
				{
					switch (args[pStart][1..])
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
				startCommand = string.Join(" ", args, pStart, args.Length - pStart);
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
					Db.Options.DefaultTimeout = 30 * 1000;
					Db.Options.DefaultRetryLimit = 10;

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
						var s = startCommand ?? Console.ReadLine();
						if (s == null) break;
						startCommand = null;

						var tokens = s.Trim().Split(" ".ToCharArray(), StringSplitOptions.RemoveEmptyEntries);
						string cmd = tokens.Length > 0 ? tokens[0] : string.Empty;
						string prm = tokens.Length > 1 ? tokens[1] : string.Empty;

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
								Console.WriteLine($"- before = {before:N0}");
								Console.WriteLine($"- after  = {after:N0}");
								Console.WriteLine($"- delta  = {(before - after):N0}");
								break;
							}

							case "mem":
							{
								Console.WriteLine("Memory usage:");
								Console.WriteLine($"- Managed Mem  : {GC.GetTotalMemory(false):N0}");
								if (OperatingSystem.IsWindows())
								{
									Console.WriteLine($"- Working Set  : {PerfCounters.WorkingSet!.NextValue():N0} (peak {PerfCounters.WorkingSetPeak!.NextValue():N0})");
									Console.WriteLine($"- Virtual Bytes: {PerfCounters.VirtualBytes!.NextValue():N0} (peak {PerfCounters.VirtualBytesPeak!.NextValue():N0})");
									Console.WriteLine($"- Private Bytes: {PerfCounters.PrivateBytes!.NextValue():N0}");
									Console.WriteLine($"- BytesInAlHeap: {PerfCounters.ClrBytesInAllHeaps!.NextValue():N0}");
								}

								break;
							}

							default:
							{
								Console.WriteLine($"Unknown command : '{trimmedCommand}'");
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
