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

		public static async Task RunAsyncCommand(Func<IFdbDatabase, TextWriter, CancellationToken, Task> command, CancellationToken cancel)
		{
			TextWriter log = null;
			var db = Db;
			if (log == null) log = Console.Out;

			using(var cts = CancellationTokenSource.CreateLinkedTokenSource(cancel))
			{ 
				try
				{
					await command(db, log, cts.Token).ConfigureAwait(false);
				}
				catch (Exception e)
				{
					Console.WriteLine("EPIC FAIL!");
					Console.Error.WriteLine(e.ToString());
					cts.Cancel();
				}
			}
		}

		public static async Task<Maybe<T>> RunAsyncCommand<T>(Func<IFdbDatabase, TextWriter, CancellationToken, Task<T>> command, CancellationToken cancel)
		{
			TextWriter log = null;
			var db = Db;
			if (log == null) log = Console.Out;

			using(var cts = CancellationTokenSource.CreateLinkedTokenSource(cancel))
			{
				try
				{
					return Maybe.Return<T>(await command(db, log, cts.Token).ConfigureAwait(false));
				}
				catch (Exception e)
				{
					Console.WriteLine("EPIC FAIL!");
					Console.Error.WriteLine(e.ToString());
					cts.Cancel();
					return Maybe.Error<T>(e);
				}
			}
		}

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
			finally
			{
				Fdb.Stop();
				Console.WriteLine("Bye");
			}
		}

		private static async Task MainAsync(string[] args, CancellationToken cancel)
		{
			#region Options Parsing...

			string clusterFile = null;
			var dbName = "DB";
			var partition = new string[0];
			bool showHelp = false;
			int timeout = 30;
			int maxRetries = 10;
			string execCommand = null;

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
					"exec=",
					"Execute this command, and exits immediately.",
					v => execCommand = v
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
			if (!string.IsNullOrEmpty(execCommand))
			{
				startCommand = execCommand;
			}
			else if (extra.Count > 0)
			{ // the remainder of the command line will be the first command to execute
				startCommand = String.Join(" ", extra);
			}

			#endregion

			bool stop = false;
			Db = null;
			try
			{
				Db = await ChangeDatabase(clusterFile, dbName, partition, cancel);
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


				var le = new LineEditor("FDBShell");

				string[] cmds = new string[]
				{
					"cd",
					"coordinators",
					"count",
					"dir",
					"exit",
					"gc",
					"help",
					"layer",
					"map",
					"mem",
					"mkdir",
					"mv",
					"partition",
					"pwd",
					"quit",
					"ren",
					"rmdir",
					"sampling",
					"shards",
					"show",
					"status",
					"topology",
					"tree",
					"version",
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

							var subdirs = RunAsyncCommand((db, log, ct) => AutoCompleteDirectories(path, db, log, ct), cancel).GetAwaiter().GetResult();
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
					var extras = tokens.Length > 2 ? FdbTuple.FromEnumerable<string>(tokens.Skip(2)) : FdbTuple.Empty;

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
							await VersionCommand(prm, clusterFile, Console.Out, cancel);
							break;
						}

						case "tree":
						{
							var path = ParsePath(CombinePath(CurrentDirectoryPath, prm));
							await RunAsyncCommand((db, log, ct) => BasicCommands.Tree(path, extras, db, log, ct), cancel);
							break;
						}
						case "map":
						{
							var path = ParsePath(CombinePath(CurrentDirectoryPath, prm));
							await RunAsyncCommand((db, log, ct) => BasicCommands.Map(path, extras, db, log, ct), cancel);
							break;
						}

						case "dir":
						case "ls":
						{
							var path = ParsePath(CombinePath(CurrentDirectoryPath, prm));
							await RunAsyncCommand((db, log, ct) => BasicCommands.Dir(path, extras, BasicCommands.DirectoryBrowseOptions.Default, db, log, ct), cancel);
							break;
						}
						case "ll":
						{
							var path = ParsePath(CombinePath(CurrentDirectoryPath, prm));
							await RunAsyncCommand((db, log, ct) => BasicCommands.Dir(path, extras, BasicCommands.DirectoryBrowseOptions.ShowCount, db, log, ct), cancel);
							break;
						}

						case "count":
						{
							var path = ParsePath(CombinePath(CurrentDirectoryPath, prm));
							await RunAsyncCommand((db, log, ct) => BasicCommands.Count(path, extras, db, log, ct), cancel);
							break;
						}

						case "show":
						case "top":
						{
							var path = ParsePath(CurrentDirectoryPath);
							await RunAsyncCommand((db, log, ct) => BasicCommands.Show(path, extras, false, db, log, ct), cancel);
							break;
						}
						case "last":
						{
							var path = ParsePath(CurrentDirectoryPath);
							await RunAsyncCommand((db, log, ct) => BasicCommands.Show(path, extras, true, db, log, ct), cancel);
							break;
						}

						case "cd":
						case "pwd":
						{
							if (!string.IsNullOrEmpty(prm))
							{
								var newPath = CombinePath(CurrentDirectoryPath, prm);
								var res = await RunAsyncCommand((db, log, ct) => BasicCommands.TryOpenCurrentDirectoryAsync(ParsePath(newPath), db, ct), cancel);
								if (res == null)
								{
									Console.WriteLine("# Directory {0} does not exist!", newPath);
									Console.Beep();
								}
								else
								{
									CurrentDirectoryPath = newPath;
									updatePrompt(CurrentDirectoryPath);
								}
							}
							else
							{
								var res = await RunAsyncCommand((db, log, ct) => BasicCommands.TryOpenCurrentDirectoryAsync(ParsePath(CurrentDirectoryPath), db, ct), cancel);
								if (res.GetValueOrDefault() == null)
								{
									Console.WriteLine("# Directory {0} does not exist anymore", CurrentDirectoryPath);
								}
								else
								{
									Console.WriteLine("# {0}", res);
								}
							}
							break;
						}
						case "mkdir":
						case "md":
						{ // "mkdir DIRECTORYNAME"

							if (!string.IsNullOrEmpty(prm))
							{
								var path = ParsePath(CombinePath(CurrentDirectoryPath, prm));
								await RunAsyncCommand((db, log, ct) => BasicCommands.CreateDirectory(path, extras, db, log, ct), cancel);
							}
							break;
						}
						case "rmdir":
						{ // "rmdir DIRECTORYNAME"
							if (!string.IsNullOrEmpty(prm))
							{
								var path = ParsePath(CombinePath(CurrentDirectoryPath, prm));
								await RunAsyncCommand((db, log, ct) => BasicCommands.RemoveDirectory(path, extras, db, log, ct), cancel);
							}
							break;
						}

						case "mv":
						case "ren":
						{ // "mv SOURCE DESTINATION"
							
							var srcPath = ParsePath(CombinePath(CurrentDirectoryPath, prm));
							var dstPath = ParsePath(CombinePath(CurrentDirectoryPath, extras.Get<string>(0)));
							await RunAsyncCommand((db, log, ct) => BasicCommands.MoveDirectory(srcPath, dstPath, extras.Substring(1), db, log, ct), cancel);

							break;
						}

						case "layer":
						{
							if (string.IsNullOrEmpty(prm))
							{ // displays the layer id of the current folder
								var path = ParsePath(CurrentDirectoryPath);
								await RunAsyncCommand((db, log, ct) => BasicCommands.ShowDirectoryLayer(path, extras, db, log, ct), cancel);

							}
							else
							{ // change the layer id of the current folder
								prm = prm.Trim();
								// double or single quotes can be used to escape the value
								if (prm.Length >= 2 && (prm.StartsWith("'") && prm.EndsWith("'")) || (prm.StartsWith("\"") && prm.EndsWith("\"")))
								{
									prm = prm.Substring(1, prm.Length - 2);
								}
								var path = ParsePath(CurrentDirectoryPath);
								await RunAsyncCommand((db, log, ct) => BasicCommands.ChangeDirectoryLayer(path, prm, extras, db, log, ct), cancel);
							}
							break;
						}

						case "mkpart":
						{ // "mkpart PARTITIONNAME"

							if (!string.IsNullOrEmpty(prm))
							{
								var path = ParsePath(CombinePath(CurrentDirectoryPath, prm));
								await RunAsyncCommand((db, log, ct) => BasicCommands.CreateDirectory(path, FdbTuple.Create(FdbDirectoryPartition.LayerId).Concat(extras), db, log, ct), cancel);
							}

							break;
						}

						case "topology":
						{
							await RunAsyncCommand((db, log, ct) => BasicCommands.Topology(null, extras, db, log, ct), cancel);
							break;
						}

						case "shards":
						{
							var path = ParsePath(CombinePath(CurrentDirectoryPath, prm));
							await RunAsyncCommand((db, log, ct) => BasicCommands.Shards(path, extras, db, log, ct), cancel);
							break;
						}

						case "sampling":
						{
							var path = ParsePath(CombinePath(CurrentDirectoryPath, prm));
							await RunAsyncCommand((db, log, ct) => BasicCommands.Sampling(path, extras, db, log, ct), cancel);
							break;
						}

						case "coordinators":
						{
							await RunAsyncCommand((db, log, ct) => CoordinatorsCommand(db, log, ct), cancel);
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
								newDb = await ChangeDatabase(clusterFile, dbName, newPartition, cancel);
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
							var result = await RunAsyncCommand((_, log, ct) => FdbCliCommands.RunFdbCliCommand("status details", null, clusterFile, log, ct), cancel);
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

					if (!string.IsNullOrEmpty(execCommand))
					{ // only run one command, and then exit
						break;
					}

				}
			}
			finally
			{
				if (Db != null) Db.Dispose();
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

		private static async Task VersionCommand(string prm, string clusterFile, TextWriter log, CancellationToken cancel)
		{
			log.WriteLine("Using .NET Binding v{0} with API level {1}", new System.Reflection.AssemblyName(typeof(Fdb).Assembly.FullName).Version, Fdb.ApiVersion);
			var res = await RunAsyncCommand((db, _, ct) => FdbCliCommands.RunFdbCliCommand(null, "-h", clusterFile, log, ct), cancel);
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
