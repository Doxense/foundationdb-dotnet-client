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
			try
			{
				var t = command(db, log, cts.Token);
				if (t != null)
				{
					t.GetAwaiter().GetResult();
				}
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
					"sampling",
					"coordinators",
					"partition",
					"version",
					"help",
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
						case "log":
						{
							LogCommand(prm);

							break;
						}

						case "version":
						{
							Console.WriteLine("# API version : {0}", Fdb.ApiVersion);
							Console.WriteLine("# .NET Binding: {0}", new System.Reflection.AssemblyName(typeof(Fdb).Assembly.FullName).Version);
							break;
						}

						case "tree":
						{
							var path = CombinePath(CurrentDirectoryPath, prm);
							RunAsyncCommand((db, log, ct) => TreeDirectory(path, db, log, ct));
							break;
						}
						case "dir":
						{
							var path = CombinePath(CurrentDirectoryPath, prm);
							var options = DirectoryBrowseOptions.ShowCount;
							RunAsyncCommand((db, log, ct) => BrowseDirectory(path, options, db, log, ct));
							break;
						}
						case "cd":
						case "pwd":
						{
							if (!string.IsNullOrEmpty(prm))
							{
								CurrentDirectoryPath = CombinePath(CurrentDirectoryPath, prm);
								Console.WriteLine("# Directory changed to {0}", CurrentDirectoryPath);
								updatePrompt(CurrentDirectoryPath);
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

						case "sampling":
						{
							double ratio = 0.1;
							RunAsyncCommand((db, log, ct) => new SamplingCommand(ratio).Run(db, log, ct));
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

		public static async Task CreateDirectory(string prm, string layer, IFdbDatabase db, TextWriter stream, CancellationToken ct)
		{
			if (stream == null) stream = Console.Out;

			var path = ParsePath(prm);
			stream.WriteLine("# Creating directory {0} with layer '{1}'", prm, layer);

			var folder = await db.Directory.TryOpenAsync(path, cancellationToken: ct);
			if (folder != null)
			{
				stream.WriteLine("- Directory {0} already exists!", prm);
				return;
			}

			folder = await db.Directory.TryCreateAsync(path, Slice.FromString(layer), cancellationToken: ct);
			stream.WriteLine("- Created under {0} [{1}]", FdbKey.Dump(folder.Key), folder.Key.ToHexaString(' '));

			// look if there is already stuff under there
			var stuff = await db.ReadAsync((tr) => tr.GetRange(folder.ToRange()).FirstOrDefaultAsync(), cancellationToken: ct);
			if (stuff.Key.IsPresent)
			{
				stream.WriteLine("CAUTION: There is already some data under {0} !");
				stream.WriteLine("  {0} = {1}", FdbKey.Dump(stuff.Key), stuff.Value.ToAsciiOrHexaString());
			}
		}

		[Flags]
		public enum DirectoryBrowseOptions
		{
			Default = 0,
			ShowFirstKeys = 1,
			ShowCount = 2,
		}

		private static async Task<IFdbDirectory> TryOpenCurrentDirectoryAsync(string[] path, IFdbDatabase db, CancellationToken ct)
		{
			if (path != null && path.Length > 0)
			{
				return await db.Directory.TryOpenAsync(path, cancellationToken: ct);
			}
			else
			{
				return db.Directory;
			}
		}

		public static async Task BrowseDirectory(string prm, DirectoryBrowseOptions options, IFdbDatabase db, TextWriter stream, CancellationToken ct)
		{
			if (stream == null) stream = Console.Out;

			var path = ParsePath(prm);
			stream.WriteLine("# Listing {0}:", prm);

			var parent = await TryOpenCurrentDirectoryAsync(path, db, ct);
			if (parent == null)
			{
				stream.WriteLine("  Directory not found.");
				return;
			}

			var folders = await Fdb.Directory.BrowseAsync(db, parent, ct);
			if (folders != null && folders.Count > 0)
			{
				foreach (var kvp in folders)
				{
					var name = kvp.Key;
					var subfolder = kvp.Value;
					if (subfolder != null)
					{
						if ((options & DirectoryBrowseOptions.ShowCount) != 0)
						{
							long count = await Fdb.System.EstimateCountAsync(db, kvp.Value.ToRange(), ct);
							stream.WriteLine("  {0,-12} {1,-12} {3,9:N0} {2}", FdbKey.Dump(subfolder.Key), subfolder.Layer.IsNullOrEmpty ? "-" : ("<" + subfolder.Layer.ToUnicode() + ">"), name, count);
						}
						else
						{
							stream.WriteLine("  {0,-12} {1,-12} {2}", FdbKey.Dump(subfolder.Key), subfolder.Layer.IsNullOrEmpty ? "-" : ("<" + subfolder.Layer.ToUnicode() + ">"), name);
						}
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

			if ((options & DirectoryBrowseOptions.ShowFirstKeys) != 0 && prm != "/")
			{
				// look if there is something under there
				var folder = await db.Directory.TryOpenAsync(path, cancellationToken: ct);
				if (folder != null)
				{
					stream.WriteLine("# Content of {0} [{1}]", FdbKey.Dump(folder.Key), folder.Key.ToHexaString(' '));
					var keys = await db.ReadAsync((tr) => tr.GetRange(folder.ToRange()).Take(21).ToListAsync(), cancellationToken: ct);
					if (keys.Count > 0)
					{
						foreach (var key in keys.Take(20))
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

		public static async Task<string[]> AutoCompleteDirectories(string prm, IFdbDatabase db, TextWriter log, CancellationToken ct)
		{
			var path = ParsePath(prm);
			var parent = await TryOpenCurrentDirectoryAsync(path, db, ct).ConfigureAwait(false);
			if (parent == null) return null;

			var names = await parent.ListAsync(db, ct).ConfigureAwait(false);
			return names.ToArray();
		}

		public static async Task TreeDirectory(string prm, IFdbDatabase db, TextWriter stream, CancellationToken ct)
		{
			if (stream == null) stream = Console.Out;

			var path = ParsePath(prm);
			stream.WriteLine("# Tree of {0}:", prm);

			FdbDirectorySubspace root = null;
			if (prm != "/") root = await db.Directory.TryOpenAsync(path, cancellationToken: ct);

			await TreeDirectoryWalk(root, new List<bool>(), db, stream, ct);

			stream.WriteLine("# done");
		}

		private static async Task TreeDirectoryWalk(FdbDirectorySubspace folder, List<bool> last, IFdbDatabase db, TextWriter stream, CancellationToken ct)
		{
			ct.ThrowIfCancellationRequested();

			var sb = new StringBuilder(last.Count * 4);
			if (last.Count > 0)
			{
				for (int i = 0; i < last.Count - 1; i++) sb.Append(last[i] ? "    " : "|   ");
				sb.Append(last[last.Count - 1] ? "`-- " : "|-- ");
			}

			IFdbDirectory node;
			if (folder == null)
			{
				stream.WriteLine(sb.ToString() + "<root>");
				node = db.Directory;
			}
			else
			{
				stream.WriteLine(sb.ToString() + (folder.Layer.ToString() == "partition" ? ("<" + folder.Name + ">") : folder.Name) + (folder.Layer.IsNullOrEmpty ? String.Empty : (" [" + folder.Layer.ToString() + "]")));
				node = folder;
			}

			var children = await Fdb.Directory.BrowseAsync(db, node, ct);
			int n = children.Count;
			foreach (var child in children)
			{
				last.Add((n--) == 1);
				await TreeDirectoryWalk(child.Value, last, db, stream, ct);
				last.RemoveAt(last.Count - 1);
			}
		}

		#endregion

		private static void LogCommand(string prm)
		{
			switch (prm.ToLowerInvariant())
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
