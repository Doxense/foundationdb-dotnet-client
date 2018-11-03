#region BSD Licence
/* Copyright (c) 2013-2018, Doxense SAS
All rights reserved.

Redistribution and use in source and binary forms, with or without
modification, are permitted provided that the following conditions are met:
	* Redistributions of source code must retain the above copyright
	  notice, this list of conditions and the following disclaimer.
	* Redistributions in binary form must reproduce the above copyright
	  notice, this list of conditions and the following disclaimer in the
	  documentation and/or other materials provided with the distribution.
	* Neither the name of Doxense nor the
	  names of its contributors may be used to endorse or promote products
	  derived from this software without specific prior written permission.

THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS "AS IS" AND
ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED
WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE
DISCLAIMED. IN NO EVENT SHALL <COPYRIGHT HOLDER> BE LIABLE FOR ANY
DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES
(INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES;
LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND
ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT
(INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS
SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
 */
#endregion

namespace FdbShell
{
	using System;
	using System.Collections.Generic;
	using System.IO;
	using System.Linq;
	using System.Net;
	using System.Text;
	using System.Threading;
	using System.Threading.Tasks;
	using Doxense;
	using Doxense.Collections.Tuples;
	using FoundationDB.Client;
	using FoundationDB.Layers.Directories;
	using Mono.Options;
	using Mono.Terminal;

	public static class Program
	{
		private static IFdbDatabase Db;

		internal static bool LogEnabled = false;

		internal static string CurrentDirectoryPath = "/";

		internal static string Description = "?";

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

		internal static void Comment(TextWriter output, string msg)
		{
			StdOut(output, msg, ConsoleColor.DarkGray);
		}

		internal static void Error(TextWriter output, string msg)
		{
			StdOut(output, msg, ConsoleColor.Red);
		}

		internal static void Success(TextWriter output, string msg)
		{
			StdOut(output, msg, ConsoleColor.Green);
		}

		internal static void StdOut(TextWriter output, string msg, ConsoleColor color = ConsoleColor.DarkGray)
		{
			if (output == Console.Out)
			{
				StdOut(msg, color);
			}
			else
			{
				output.WriteLine(msg);
			}
		}

		private static void StdOut(string msg, ConsoleColor color = ConsoleColor.DarkGray)
		{
			var prev = Console.ForegroundColor;
			if (prev != color) Console.ForegroundColor = color;
			Console.WriteLine(msg);
			if (prev != color) Console.ForegroundColor = prev;
		}

		private static void StdErr(string msg, ConsoleColor color = ConsoleColor.DarkRed)
		{
			var prev = Console.ForegroundColor;
			Console.ForegroundColor = color;
			Console.Error.WriteLine(msg);
			Console.ForegroundColor = prev;
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
				finally
				{
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
					return Maybe.Error<T>(e);
				}
				finally
				{
					cts.Cancel();
				}
			}
		}

		public static void Main(string[] args)
		{
			//TODO: move this to the main, and add a command line argument to on/off ?
			try
			{
				if (Console.LargestWindowWidth > 0 && Console.LargestWindowHeight > 0)
				{
					Console.WindowWidth = 160;
					Console.WindowHeight = 60;
				}
			}
			catch (Exception e)
			{
				// this sometimes fail on small screen sizes
			}

			// Initialize FDB

			try
			{
				Fdb.Start(Fdb.GetMaxSafeApiVersion(200, Fdb.GetDefaultApiVersion()));
				using (var go = new CancellationTokenSource())
				{
					MainAsync(args, go.Token).GetAwaiter().GetResult();
				}
			}
			finally
			{
				Fdb.Stop();
				StdOut("Bye");
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
					"c|C|connfile=",
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
				var cnxOptions = new FdbConnectionOptions
				{
					ClusterFile = clusterFile,
					DbName = dbName,
					PartitionPath = partition
				};
				Db = await ChangeDatabase(cnxOptions, cancel);
				Db.DefaultTimeout = Math.Max(0, timeout) * 1000;
				Db.DefaultRetryLimit = Math.Max(0, maxRetries);

				StdOut("Using API v" + Fdb.ApiVersion + " (max " + Fdb.GetMaxApiVersion() + ")", ConsoleColor.Gray);
				StdOut("Cluster file: " + (clusterFile ?? "<default>"), ConsoleColor.Gray);
				StdOut("");
				StdOut("FoundationDB Shell menu:");
				StdOut("\tcd\tChange the current directory");
				StdOut("\tdir\tList the sub-directories the current directory");
				StdOut("\tshow\tShow the content of the current directory");
				StdOut("\ttree\tShow all the directories under the current directory");
				StdOut("\tsampling\tDisplay statistics on random shards from the database");
				StdOut("\tcoordinators\tShow the current coordinators for the cluster");
				//StdOut("\thelp\tShow all the commands");
				StdOut("\tquit\tQuit");
				StdOut("");

				try
				{
					var cf = await Fdb.System.GetCoordinatorsAsync(Db, cancel);
					Description = cf.Description;
					StdOut("Ready...", ConsoleColor.DarkGreen);
				}
				catch (Exception e)
				{
					StdErr("Failed to get coordinators state from cluster: " + e.Message, ConsoleColor.DarkRed);
					Description = "???";
				}
				
				StdOut("");

				var le = new LineEditor("FDBShell");

				string[] cmds = new string[]
				{
					"cd",
					"clear",
					"coordinators",
					"count",
					"dir",
					"dump",
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

				void UpdatePrompt(string path)
				{
					prompt = $"[fdb:{Description} {path}]# ";
				}
				le.PromptColor = ConsoleColor.Cyan;
				UpdatePrompt(CurrentDirectoryPath);

				while (!stop)
				{
					string s;
					if (startCommand != null)
					{
						s = startCommand;
						startCommand = null;
					}
					else
					{
						s = startCommand ?? le.Edit(prompt, "");
					}

					if (s == null) break;

					//TODO: we need a tokenizer that recognizes binary keys, tuples, escaped paths, etc...
					var tokens = Tokenize(s);
					string cmd = tokens.Count > 0 ? tokens.Get<string>(0) : string.Empty;
					var extras = tokens.Count > 1 ? tokens.Substring(1) : STuple.Empty;

					var trimmedCommand = cmd.Trim().ToLowerInvariant();
					try
					{
						switch (trimmedCommand)
						{
							case "":
							{
								continue;
							}
							case "log":
							{
								string prm = PopParam(ref extras);
								LogCommand(prm, extras, Console.Out);
								break;
							}

							case "version":
							{
								await VersionCommand(extras, clusterFile, Console.Out, cancel);
								break;
							}

							case "tree":
							{
								string prm = PopParam(ref extras);
								var path = ParsePath(CombinePath(CurrentDirectoryPath, prm));
								await RunAsyncCommand((db, log, ct) => BasicCommands.Tree(path, extras, db, log, ct), cancel);
								break;
							}
							case "map":
							{
								string prm = PopParam(ref extras);
								var path = ParsePath(CombinePath(CurrentDirectoryPath, prm));
								await RunAsyncCommand((db, log, ct) => BasicCommands.Map(path, extras, db, log, ct), cancel);
								break;
							}

							case "dir":
							case "ls":
							{
								string prm = PopParam(ref extras);
								var path = ParsePath(CombinePath(CurrentDirectoryPath, prm));
								await RunAsyncCommand((db, log, ct) => BasicCommands.Dir(path, extras, BasicCommands.DirectoryBrowseOptions.Default, db, log, ct), cancel);
								break;
							}
							case "ll":
							{
								string prm = PopParam(ref extras);
								var path = ParsePath(CombinePath(CurrentDirectoryPath, prm));
								await RunAsyncCommand((db, log, ct) => BasicCommands.Dir(path, extras, BasicCommands.DirectoryBrowseOptions.ShowCount, db, log, ct), cancel);
								break;
							}

							case "count":
							{
								string prm = PopParam(ref extras);
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

							case "dump":
							{
								string output = PopParam(ref extras);
								if (string.IsNullOrEmpty(output))
								{
									StdErr("You must specify a target file path.", ConsoleColor.Red);
									break;
								}
								var path = ParsePath(CurrentDirectoryPath);
								await RunAsyncCommand((db, log, ct) => BasicCommands.Dump(path, output, extras, db, log, ct), cancel);
								break;
							}

							case "cd":
							case "pwd":
							{
								string prm = PopParam(ref extras);
								if (!string.IsNullOrEmpty(prm))
								{
									var newPath = CombinePath(CurrentDirectoryPath, prm);
									var res = await RunAsyncCommand((db, log, ct) => BasicCommands.TryOpenCurrentDirectoryAsync(ParsePath(newPath), db, ct), cancel);
									if (res.Failed)
									{
										StdErr($"# Failed to open Directory {newPath}: {res.Error.Message}", ConsoleColor.Red);
										Console.Beep();
									}
									else if (res.Value == null)
									{
										StdOut($"# Directory {newPath} does not exist!", ConsoleColor.Red);
										Console.Beep();
									}
									else
									{
										CurrentDirectoryPath = newPath;
										UpdatePrompt(CurrentDirectoryPath);
									}
								}
								else
								{
									var res = await RunAsyncCommand((db, log, ct) => BasicCommands.TryOpenCurrentDirectoryAsync(ParsePath(CurrentDirectoryPath), db, ct), cancel);
									if (res.Failed)
									{
										StdErr($"# Failed to query Directory {Program.CurrentDirectoryPath}: {res.Error.Message}", ConsoleColor.Red);
									}
									else if (res.Value == null)
									{
										StdOut($"# Directory {Program.CurrentDirectoryPath} does not exist anymore");
									}
								}

								break;
							}
							case "mkdir":
							case "md":
							{ // "mkdir DIRECTORYNAME"

								string prm = PopParam(ref extras);
								if (!string.IsNullOrEmpty(prm))
								{
									var path = ParsePath(CombinePath(CurrentDirectoryPath, prm));
									await RunAsyncCommand((db, log, ct) => BasicCommands.CreateDirectory(path, extras, db, log, ct), cancel);
								}

								break;
							}
							case "rmdir":
							{ // "rmdir DIRECTORYNAME"
								string prm = PopParam(ref extras);
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

								string prm = PopParam(ref extras);
								var srcPath = ParsePath(CombinePath(CurrentDirectoryPath, prm));
								var dstPath = ParsePath(CombinePath(CurrentDirectoryPath, extras.Get<string>(0)));
								await RunAsyncCommand((db, log, ct) => BasicCommands.MoveDirectory(srcPath, dstPath, extras.Substring(1), db, log, ct), cancel);

								break;
							}

							case "get":
							{ // "get KEY"

								if (extras.Count == 0)
								{
									StdErr("You must specify a key to read.", ConsoleColor.Red);
									break;
								}

								var path = ParsePath(CurrentDirectoryPath);
								await RunAsyncCommand((db, log, ct) => BasicCommands.Get(path, extras, db, log, ct), cancel);
								break;
							}

							case "clear":
							{ // "clear KEY"

								if (extras.Count == 0)
								{
									StdErr("You must specify a key to clear.", ConsoleColor.Red);
									break;
								}

								var path = ParsePath(CurrentDirectoryPath);
								await RunAsyncCommand((db, log, ct) => BasicCommands.Clear(path, extras, db, log, ct), cancel);
								break;
							}

							case "clearrange":
							{ // "clear *" or "clear FROM TO"

								if (extras.Count == 0)
								{
									StdErr("You must specify either '*', a prefix, or a key range.", ConsoleColor.Red);
									break;
								}

								var path = ParsePath(CurrentDirectoryPath);
								await RunAsyncCommand((db, log, ct) => BasicCommands.ClearRange(path, extras, db, log, ct), cancel);
								break;
							}

							case "layer":
							{
								string prm = PopParam(ref extras);
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

								string prm = PopParam(ref extras);
								if (!string.IsNullOrEmpty(prm))
								{
									var path = ParsePath(CombinePath(CurrentDirectoryPath, prm));
									await RunAsyncCommand((db, log, ct) => BasicCommands.CreateDirectory(path, STuple.Create(FdbDirectoryPartition.LayerId).Concat(extras), db, log, ct), cancel);
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
								string prm = PopParam(ref extras);
								var path = ParsePath(CombinePath(CurrentDirectoryPath, prm));
								await RunAsyncCommand((db, log, ct) => BasicCommands.Shards(path, extras, db, log, ct), cancel);
								break;
							}

							case "sampling":
							{
								string prm = PopParam(ref extras);
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
								string prm = PopParam(ref extras);
								if (string.IsNullOrEmpty(prm))
								{
									StdOut($"# Current partition is {String.Join("/", partition)}");
									//TODO: browse existing partitions ?
									break;
								}

								var newPartition = prm.Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
								IFdbDatabase newDb = null;
								try
								{
									var options = new FdbConnectionOptions
									{
										ClusterFile = clusterFile,
										DbName = dbName,
										PartitionPath = newPartition
									};
									newDb = await ChangeDatabase(options, cancel);
								}
								catch (Exception)
								{
									newDb?.Dispose();
									newDb = null;
									throw;
								}
								finally
								{
									if (newDb != null)
									{
										if (Db != null)
										{
											Db.Dispose();
											Db = null;
										}

										Db = newDb;
										partition = newPartition;
										StdOut($"# Changed partition to /{string.Join("/", partition)}");
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
								StdOut(" Done");
								long after = GC.GetTotalMemory(false);
								StdOut("- before = " + before.ToString("N0"));
								StdOut("- after  = " + after.ToString("N0"));
								StdOut("- delta  = " + (before - after).ToString("N0"));
								break;
							}

							case "mem":
							{
								StdOut("Memory usage:");
								StdOut("- Managed Mem  : " + GC.GetTotalMemory(false).ToString("N0"));
								//TODO: how do we get these values on Linux/Mac?
#if !NETCOREAPP
								StdOut("- Working Set  : " + PerfCounters.WorkingSet.NextValue().ToString("N0") + " (peak " + PerfCounters.WorkingSetPeak.NextValue().ToString("N0") + ")");
								StdOut("- Virtual Bytes: " + PerfCounters.VirtualBytes.NextValue().ToString("N0") + " (peak " + PerfCounters.VirtualBytesPeak.NextValue().ToString("N0") + ")");
								StdOut("- Private Bytes: " + PerfCounters.PrivateBytes.NextValue().ToString("N0"));
								StdOut("- BytesInAlHeap: " + PerfCounters.ClrBytesInAllHeaps.NextValue().ToString("N0"));
#endif
								break;
							}

							case "wide":
							{
								try
								{
									Console.WindowWidth = 160;
								}
								catch (Exception e)
								{
									StdErr("Failed to change console width: " + e.Message, ConsoleColor.DarkRed);
								}

								break;
							}

							case "status":
							case "wtf":
							{
								var result = await RunAsyncCommand((_, log, ct) => FdbCliCommands.RunFdbCliCommand("status details", null, clusterFile, log, ct), cancel);
								if (result.Failed) break;
								if (result.Value.ExitCode != 0)
								{
									StdErr($"# fdbcli exited with code {result.Value.ExitCode}", ConsoleColor.DarkRed);
									StdOut("> StdErr:", ConsoleColor.DarkGray);
									StdOut(result.Value.StdErr);
									StdOut("> StdOut:", ConsoleColor.DarkGray);
								}

								StdOut(result.Value.StdOut);
								break;
							}

							default:
							{
								StdErr($"Unknown command : '{trimmedCommand}'", ConsoleColor.Red);
								break;
							}
						}
					}
					catch (Exception e)
					{
						StdErr($"Failed to execute command '{trimmedCommand}': " + e.Message, ConsoleColor.Red);
#if DEBUG
						StdErr(e.ToString(), ConsoleColor.DarkRed);
#endif
					}

					if (!string.IsNullOrEmpty(execCommand))
					{ // only run one command, and then exit
						break;
					}

				}
			}
			finally
			{
				Program.Db?.Dispose();
			}
		}

		private static string PopParam(ref IVarTuple extras)
		{
			if (extras.Count == 0)
			{
				return null;
			}

			string prm = extras.Get<string>(0);
			extras = extras.Substring(1);
			return prm;
		}

		#region Directories...

		private static string CombinePath(string parent, string children)
		{
			if (string.IsNullOrEmpty(children) || children == ".") return parent;
			if (children.StartsWith("/", StringComparison.Ordinal)) return children;

			if (!parent.EndsWith("/", StringComparison.Ordinal)) parent += "/";
			if (children.EndsWith("/", StringComparison.Ordinal)) children = children.Substring(0, children.Length - 1);

			string[] tokens = (parent + children).Substring(1).Split('/');
			// we need to remove all the "." and ".." !
			var s = new List<string>();
			foreach(var tok in tokens)
			{
				if (tok == ".") continue;
				if (tok == "..")
				{
					if (s.Count == 0)
					{
						throw new InvalidOperationException("The specified path is invalid.");
					}
					s.RemoveAt(s.Count - 1);
					continue;
				}
				s.Add(tok);
			}
			return "/" + string.Join("/", s);
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

		private static void LogCommand(string prm, IVarTuple extras, TextWriter log)
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

		private static async Task VersionCommand(IVarTuple extras, string clusterFile, TextWriter log, CancellationToken cancel)
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
			Description = cf.Description;
			Program.StdOut(log, $"Connnected to: {cf.Description} ({cf.Id})", ConsoleColor.Gray);
			Program.StdOut(log, $"Found {cf.Coordinators.Length} coordinator(s):");
			foreach (var coordinator in cf.Coordinators)
			{
				//string hostName = null;
				//try
				//{
				//	var ipHost = Dns.GetHostEntry(coordinator.Address);
				//	hostName = " (" + ipHost.HostName + ")";
				//}
				//catch (Exception) { }

				Program.StdOut(log, $"  {coordinator.Address}:{coordinator.Port}", ConsoleColor.White);
			}
		}

		private static Task<IFdbDatabase> ChangeDatabase(FdbConnectionOptions options, CancellationToken ct)
		{
			options.DefaultTimeout = TimeSpan.FromSeconds(30);
			options.DefaultRetryLimit = 50;
			Program.StdOut("Connecting to cluster...", ConsoleColor.Gray);
			return Fdb.OpenAsync(options, ct);
		}

		private static IVarTuple Tokenize(string s)
		{

			bool hasToken = false;
			var tokens = STuple.Empty;
			int p = 0;


			for (int i = 0; i < s.Length; i++)
			{
				char c = s[i];
				switch (c)
				{
					case ' ':
					case '\t':
					{ // end of previous token, start of new token
						if (hasToken)
						{
							tokens = tokens.Append(s.Substring(p, i - p));
							hasToken = false;
						}
						break;
					}
					case '(':
					{ 
						if (!hasToken)
						{ // start of tuple
							string exp = s.Substring(i);
							STuple.Deformatter.ParseNext(exp, out var tok, out string tail);
							tokens = tokens.Append(tok);
							if (string.IsNullOrEmpty(tail))
							{
								i = s.Length;
							}
							else
							{
								i += exp.Length - tail.Length - 1;
							}
						}
						break;
					}
					case '\'':
					case '"':
					{
						if (!hasToken)
						{
							tokens = tokens.Append(ParseQuotedString(s, c, i, out int j));
							i = j;
						}
						break;
					}
					default:
					{
						if (!hasToken)
						{
							p = i;
							hasToken = true;
						}
						break;
					}
				}
			}
			if (hasToken)
			{
				tokens = tokens.Append(s.Substring(p));
			}

			return tokens;
		}

		private static string ParseQuotedString(string s, char quote, int start, out int next)
		{
			//TODO: read '.....'
			var sb = new StringBuilder();
			bool slash = false;
			for (int i = start + 1; i < s.Length; i++)
			{
				char c = s[i];
				switch (c)
				{
					case '\'':
					{
						if (slash)
						{
							slash = false;
							sb.Append('\'');
							break;
						}
						if (quote == '\'')
						{
							next = i + 1;
							return sb.ToString();
						}

						sb.Append('\'');
						break;
					}
					case '"':
					{
						if (slash)
						{
							slash = false;
							sb.Append('"');
							break;
						}
						if (quote == '"')
						{
							next = i + 1;
							return sb.ToString();
						}
						sb.Append('"');
						break;
					}
					case '\\':
					{
						if (slash)
						{
							slash = false;
							sb.Append('\\');
							break;
						}
						slash = true;
						break;
					}
					default:
					{
						if (slash)
						{
							slash = false;
							switch (c)
							{
								case 't': sb.Append('\t'); break;
								case 'r': sb.Append('\r'); break;
								case 'n': sb.Append('\n'); break;
								default: throw new FormatException("Invalid escape string literal");
							}
							break;
						}
						sb.Append(c);
						break;
					}
				}
			}
			throw new FormatException("Missing final quote at end of string literal");
		}

	}
}

