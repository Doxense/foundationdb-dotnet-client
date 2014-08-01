using FoundationDB.Async;
using FoundationDB.Client;
using FoundationDB.Filters.Logging;
using FoundationDB.Layers.Directories;
using FoundationDB.Layers.Tuples;
using Mono.Options;
using Mono.Terminal;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace FdbShell
{

	public static class Program
	{
		private static IFdbDatabase Db;

		internal static bool LogEnabled = false;

		internal static string CurrentDirectoryPath = "/";

		private static StreamWriter GetLogFile(string name)
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

		private static IFdbDatabase GetLoggedDatabase(IFdbDatabase db, StreamWriter stream, bool autoFlush = false)
		{
			if (stream == null) return db;

			return new FdbLoggedDatabase(db, false, false, (tr) => { stream.WriteLine(tr.Log.GetTimingsReport(true)); if (autoFlush) stream.Flush(); });
		}

		public static void RunAsyncCommand(Func<IFdbDatabase, TextWriter, CancellationToken, Task> command)
		{
			TextWriter log = null;
			var db = Db;
			if (log == null) log = Console.Out;

			var cts = new CancellationTokenSource();
			//ConsoleCancelEventHandler emergencyBreak = (_, a) =>
			//{
			//	a.Cancel = true;
			//	Console.WriteLine();
			//	Console.WriteLine("[ABORTING]");
			//	cts.Cancel();
			//};
			//Console.CancelKeyPress += emergencyBreak;
			try
			{
				var t = command(db, log, cts.Token);
				t.GetAwaiter().GetResult();
			}
			catch (Exception e)
			{
				Console.WriteLine("EPIC FAIL!");
				Console.Error.WriteLine(e.ToString());
				cts.Cancel();
			}
			finally
			{
				//Console.CancelKeyPress -= emergencyBreak;
				cts.Dispose();
			}
		}

		public static Maybe<T> RunAsyncCommand<T>(Func<IFdbDatabase, TextWriter, CancellationToken, Task<T>> command)
		{
			TextWriter log = null;
			var db = Db;
			if (log == null) log = Console.Out;

			var cts = new CancellationTokenSource();
			try
			{
				var t = command(db, log, cts.Token);
				return Maybe.Return(t.GetAwaiter().GetResult());
			}
			catch (Exception e)
			{
				Console.Error.WriteLine(e.ToString());
				return Maybe.Error<T>(e);
			}
			finally
			{
				cts.Dispose();
			}
		}

		public static void Main(string[] args)
		{

			#region Options Parsing...

			string clusterFile = null;
			var dbName = "DB";
			var partition = new string[0];
			bool showHelp = false;
			int timeout = 30;
			int maxRetries =  10;

			var opts = new OptionSet()
			{
				{ 
					"c|connfile=",
					"The path of a file containing the connection string for the FoundationDB cluster.",
					v => clusterFile = v
				},
				{ 
					"p|partition=",
					"The name of the database partition to open.",
					v => partition = v.Trim().Split('/')
				},
				{
					"t|timeout=",
					"Default timeout (in seconds) for failed transactions.",
					(int v) => timeout = v
				},
				{
					"r|retries=",
					"Default max retry count for failed transactions.",
					(int v) => maxRetries = v
				},
				{
					"h|help",
					"Show this help and exit.",
					v => showHelp = v != null
				}
			};

			var extra = opts.Parse(args);

			if (showHelp)
			{
				//TODO!
				opts.WriteOptionDescriptions(Console.Out);
				return;
			}

			string startCommand = null;
			if (extra.Count > 0)
			{ // the remainder of the command line will be the first command to execute
				startCommand = String.Join(" ", extra);
			}

			#endregion

			var go = new CancellationTokenSource();
			bool stop = false;

			// Initialize FDB
			Fdb.Start();
			Db = null;
			try
			{
				Db = ChangeDatabase(clusterFile, dbName, partition, go.Token).GetAwaiter().GetResult();
				Db.DefaultTimeout = Math.Max(0, timeout) * 1000;
				Db.DefaultRetryLimit = Math.Max(0, maxRetries);

				Console.WriteLine("Using API v" + Fdb.ApiVersion + " (max " + Fdb.GetMaxApiVersion() + ")");
				Console.WriteLine("Cluster file: " + (clusterFile ?? "<default>"));
				Console.WriteLine();
				Console.WriteLine("FoundationDB Shell menu:");
				Console.WriteLine("\tdir\tShow the content of the current directory");
				Console.WriteLine("\ttree\tShow all the directories under the current directory");
				Console.WriteLine("\tsampling\tDisplay statistics on random shards from the database");
				Console.WriteLine("\tcoordinators\tShow the current coordinators for the cluster");
				Console.WriteLine("\tmem\tShow memory usage statistics");
				Console.WriteLine("\tgc\tTrigger garbage collection");
				Console.WriteLine("\tquit\tQuit");

				Console.WriteLine("Ready...");


				var le = new LineEditor(null);

				string[] cmds = new string[]
				{
					"exit",
					"quit",
					"pwd",
					"cd",
					"mkdir",
					"gc",
					"mem",
					"dir",
					"tree",
					"map",
					"show",
					"count",
					"sampling",
					"coordinators",
					"partition",
					"version",
					"help",
					"topology",
					"shards",
					"status",
					"wide",
				};

				le.AutoCompleteEvent = (txt, pos) =>
				{
					string[] res;
					int p = txt.IndexOf(' ');
					if (p > 0)
					{
						string cmd = txt.Substring(0, p);
						string arg = txt.Substring(p + 1);

						if (cmd == "cd")
						{ // handle completion for directories

							// txt: "cd foo" => prefix = "foo"
							// txt: "cd foobar/b" => prefix = "b"

							string path = CurrentDirectoryPath;
							string prefix = "";
							string search = arg;
							p = arg.LastIndexOf('/');
							if (p > 0)
							{
								path = Path.Combine(path, arg.Substring(0, p));
								search = arg.Substring(p + 1);
								prefix = arg.Substring(0, p + 1);
							}

							var subdirs = RunAsyncCommand((db, log, ct) => AutoCompleteDirectories(path, db, log, ct));
							if (!subdirs.HasValue || subdirs.Value == null) return new LineEditor.Completion(txt, null);

							res = subdirs.Value
								.Where(s => s.StartsWith(search, StringComparison.Ordinal))
								.Select(s => (cmd + " " + prefix + s).Substring(txt.Length))
								.ToArray();
							return new LineEditor.Completion(txt, res);
						}

						// unknown command
						return new LineEditor.Completion(txt, null);
					}

					// list of commands
					res = cmds
						.Where(cmd => cmd.StartsWith(txt, StringComparison.OrdinalIgnoreCase))
						.Select(cmd => cmd.Substring(txt.Length))
						.ToArray();
					return new LineEditor.Completion(txt, res);
				};
				le.TabAtStartCompletes = true;

				string prompt = null;
				Action<string> updatePrompt = (path) => { prompt = String.Format("fdb:{0}> ", path); };
				updatePrompt(CurrentDirectoryPath);

				while (!stop)
				{
					string s = startCommand != null ? startCommand : le.Edit(prompt, "");
					startCommand = null;

					if (s == null) break;

					var tokens = s.Trim().Split(new [] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
					string cmd = tokens.Length > 0 ? tokens[0] : String.Empty;
					string prm = tokens.Length > 1 ? tokens[1] : String.Empty;
					var extras = tokens.Length > 2 ? FdbTuple.CreateRange<string>(tokens.Skip(2)) : FdbTuple.Empty;

					var trimmedCommand = cmd.Trim().ToLowerInvariant();
					switch (trimmedCommand)
					{
						case "":
						{
							continue;
						}
						case "log":
						{
							LogCommand(prm, Console.Out);

							break;
						}

						case "version":
						{
							VersionCommand(prm, clusterFile, Console.Out);
							break;
						}

						case "tree":
						{
							var path = ParsePath(CombinePath(CurrentDirectoryPath, prm));
							RunAsyncCommand((db, log, ct) => BasicCommands.Tree(path, extras, db, log, ct));
							break;
						}
						case "map":
						{
							var path = ParsePath(CombinePath(CurrentDirectoryPath, prm));
							RunAsyncCommand((db, log, ct) => BasicCommands.Map(path, extras, db, log, ct));
							break;
						}

						case "dir":
						case "ls":
						{
							var path = ParsePath(CombinePath(CurrentDirectoryPath, prm));
							RunAsyncCommand((db, log, ct) => BasicCommands.Dir(path, extras, BasicCommands.DirectoryBrowseOptions.Default, db, log, ct));
							break;
						}
						case "ll":
						{
							var path = ParsePath(CombinePath(CurrentDirectoryPath, prm));
							RunAsyncCommand((db, log, ct) => BasicCommands.Dir(path, extras, BasicCommands.DirectoryBrowseOptions.ShowCount, db, log, ct));
							break;
						}

						case "count":
						{
							var path = ParsePath(CombinePath(CurrentDirectoryPath, prm));
							RunAsyncCommand((db, log, ct) => BasicCommands.Count(path, extras, db, log, ct));
							break;
						}

						case "show":
						{
							var path = ParsePath(CurrentDirectoryPath);
							RunAsyncCommand((db, log, ct) => BasicCommands.Show(path, extras, db, log, ct));
							break;
						}

						case "cd":
						case "pwd":
						{
							if (!string.IsNullOrEmpty(prm))
							{
								var newPath = CombinePath(CurrentDirectoryPath, prm);
								var res = RunAsyncCommand((db, log, ct) => BasicCommands.TryOpenCurrentDirectoryAsync(ParsePath(newPath), db, ct));
								if (res == null)
								{
									Console.WriteLine("# Directory {0} does not exist!", newPath);
								}
								else
								{
									CurrentDirectoryPath = newPath;
									Console.WriteLine("# Directory changed to {0}", CurrentDirectoryPath);
									updatePrompt(CurrentDirectoryPath);
								}
							}
							else
							{
								Console.WriteLine("# Current directory is {0}", CurrentDirectoryPath);
							}
							break;
						}
						case "mkdir":
						case "md":
						{
							// "mkdir DIRECTORYNAME"

							if (!string.IsNullOrEmpty(prm))
							{
								var path = ParsePath(CombinePath(CurrentDirectoryPath, prm));
								RunAsyncCommand((db, log, ct) => BasicCommands.Create(path, extras, db, log, ct));
							}
							break;
						}

						case "topology":
						{
							RunAsyncCommand((db, log, ct) => BasicCommands.Topology(null, extras, db, log, ct));
							break;
						}

						case "shards":
						{
							var path = ParsePath(CombinePath(CurrentDirectoryPath, prm));
							RunAsyncCommand((db, log, ct) => BasicCommands.Shards(path, extras, db, log, ct));
							break;
						}

						case "sampling":
						{
							var path = ParsePath(CombinePath(CurrentDirectoryPath, prm));
							RunAsyncCommand((db, log, ct) => BasicCommands.Sampling(path, extras, db, log, ct));
							break;
						}

						case "coordinators":
						{
							RunAsyncCommand((db, log, ct) => CoordinatorsCommand(db, log, ct));
							break;
						}

						case "partition":
						{
							if (string.IsNullOrEmpty(prm))
							{
								Console.WriteLine("# Current partition is {0}", String.Join("/", partition));
								//TODO: browse existing partitions ?
								break;
							}

							var newPartition = prm.Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
							IFdbDatabase newDb = null;
							try
							{
								newDb = ChangeDatabase(clusterFile, dbName, newPartition, go.Token).GetAwaiter().GetResult();
							}
							catch (Exception)
							{
								if (newDb != null) newDb.Dispose();
								newDb = null;
								throw;
							}
							finally
							{
								if (newDb != null)
								{
									if (Db != null) { Db.Dispose(); Db = null; }
									Db = newDb;
									partition = newPartition;
									Console.WriteLine("# Changed partition to {0}", partition);
								}
							}
							break;
						}

						case "q":
						case "x":
						case "quit":
						case "exit":
						case "bye":
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

						case "wide":
						{
							Console.WindowWidth = 160;
							break;
						}

						case "status":
						case "wtf":
						{
							var result = RunAsyncCommand((_, log, ct) => FdbCliCommands.RunFdbCliCommand("status details", null, clusterFile, log, ct));
							if (result.HasFailed) break;
							if (result.Value.ExitCode != 0)
							{
								Console.WriteLine("# fdbcli exited with code {0}", result.Value.ExitCode);
								Console.WriteLine("> StdErr:");
								Console.WriteLine(result.Value.StdErr);
								Console.WriteLine("> StdOut:");
							}
							Console.WriteLine(result.Value.StdOut);
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
			finally
			{
				go.Cancel();
				if (Db != null) Db.Dispose();
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

		private static async Task<string[]> AutoCompleteDirectories(string prm, IFdbDatabase db, TextWriter log, CancellationToken ct)
		{
			var path = ParsePath(prm);
			var parent = await BasicCommands.TryOpenCurrentDirectoryAsync(path, db, ct).ConfigureAwait(false);
			if (parent == null) return null;

			var names = await parent.ListAsync(db, ct).ConfigureAwait(false);
			return names.ToArray();
		}

		#endregion

		private static void LogCommand(string prm, TextWriter log)
		{
			switch (prm.ToLowerInvariant())
			{
				case "on":
				{
					LogEnabled = true;
					log.WriteLine("# Logging enabled");
					break;
				}
				case "off":
				{
					LogEnabled = false;
					log.WriteLine("# Logging disabled");
					break;
				}
				default:
				{
					log.WriteLine("# Logging is {0}", LogEnabled ? "ON" : "OFF");
					break;
				}
			} 
		}

		private static void VersionCommand(string prm, string clusterFile, TextWriter log)
		{
			log.WriteLine("Using .NET Binding v{0} with API level {1}", new System.Reflection.AssemblyName(typeof(Fdb).Assembly.FullName).Version, Fdb.ApiVersion);
			var res = RunAsyncCommand((db, _, ct) => FdbCliCommands.RunFdbCliCommand(null, "-h", clusterFile, log, ct));
			if (res.HasValue && res.Value.ExitCode == 0)
			{
				//HACK HACK HACK
				log.WriteLine("Found {0}", res.Value.StdOut.Split('\n')[0]);
			}
			else
			{
				log.WriteLine("# Failed to execute fdbcli :(");
			}
			log.WriteLine();
		}

		private static async Task CoordinatorsCommand(IFdbDatabase db, TextWriter log, CancellationToken ct)
		{
			var cf = await Fdb.System.GetCoordinatorsAsync(db, ct);
			log.WriteLine("Connnected to: " + cf.Description + " (" + cf.Id + ")");
			log.WriteLine("Found {0} coordinator(s):", cf.Coordinators.Length);
			foreach (var coordinator in cf.Coordinators)
			{
				var iphost = Dns.GetHostEntry(coordinator.Address);
				log.WriteLine("- " + coordinator.Address + ":" + coordinator.Port + " (" + iphost.HostName + ")");
			}
		}

		private static Task<IFdbDatabase> ChangeDatabase(string clusterFile, string dbName, string[] partition, CancellationToken ct)
		{
			if (partition == null || partition.Length == 0)
			{
				return Fdb.OpenAsync(clusterFile, dbName, ct);
			}
			else
			{
				return Fdb.Directory.OpenNamedPartitionAsync(clusterFile, dbName, partition, false, ct);
			}
		}

	}
}
