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

//#define USE_LOG_FILE

// ReSharper disable MethodHasAsyncOverload

namespace FdbShell
{
	using System.Collections.Generic;
	using System.CommandLine;
	using System.CommandLine.Invocation;
	using System.Diagnostics;
	using System.Globalization;
	using System.Linq;
	using System.Runtime.CompilerServices;
	using System.Text;
	using System.Threading;
	using System.Threading.Tasks;
	using FoundationDB.DependencyInjection;
	using Microsoft.Extensions.DependencyInjection;
	using Mono.Terminal;

	public static class Program
	{

		public static void Main(string[] args)
		{
			//TODO: move this to the main, and add a command line argument to on/off ?

			// Initialize FDB

			try
			{
				using (var go = new CancellationTokenSource())
				{
					MainAsync(args, go.Token).GetAwaiter().GetResult();
				}
			}
			catch (Exception e)
			{
				Console.Error.WriteLine("CRASH: " + e);
			}
			finally
			{
				Fdb.Stop();
			}
		}


		private static async Task MainAsync(string[] args, CancellationToken cancel)
		{
			#region Options Parsing...

			var cmd = new FdbShellCommand(cancel);

			try
			{
				await cmd.InvokeAsync(args);
			}
			catch (Exception e)
			{
				Console.Error.WriteLine("Crash: " + e);
			}

			#endregion
		}

		public class FdbShellCommand : RootCommand
		{

			public FdbShellCommand(CancellationToken cancel)
				: base("Hello, there!")
			{
				this.AddGlobalOption(ClusterFileOption);
				this.AddGlobalOption(ConnectionStringOption);
				this.AddGlobalOption(ApiVersionOption);
				this.AddGlobalOption(PartitionOption);
				this.AddGlobalOption(TimeoutOption);
				this.AddGlobalOption(RetriesOption);
				this.AddGlobalOption(AspireOption);
				this.AddOption(ExecOption);

				this.SetHandler(RunShell);

				this.Cancellation = cancel;
			}

			public CancellationToken Cancellation { get; }

			private static readonly System.CommandLine.Option<string?> ClusterFileOption = new (
				["--connfile", "-c", "-C"],
				"The path of a file containing the connection string for the FoundationDB cluster."
			);

			private static readonly System.CommandLine.Option<string?> ConnectionStringOption = new (
				["--connStr"],
				"The connection string for the FoundationDB cluster."
			);

			private static readonly System.CommandLine.Option<int?> ApiVersionOption = new(
				[ "--api" ],
				"The API version level that should be used."
			);

			private static readonly System.CommandLine.Option<string?> PartitionOption = new(
				[ "--partition", "-p" ],
				"The name of the database partition to open."
			);

			private static readonly System.CommandLine.Option<int?> TimeoutOption = new(
				[ "--timeout", "-t" ],
				getDefaultValue: () => 30,
				"Default timeout (in seconds) for failed transactions."
			);

			private static readonly System.CommandLine.Option<int?> RetriesOption = new(
				[ "--retries", "-r" ],
				getDefaultValue: () => 10,
				"Default max retry count for failed transactions."
			);

			private static readonly System.CommandLine.Option<string?> ExecOption = new(
				[ "--exec" ],
				"Execute this command, and exits immediately."
			);

			private static readonly Option<bool> AspireOption = new(
				[ "--aspire" ],
				"Use the local docker instance managed by .NET Aspire"
			);

			private async Task RunShell(InvocationContext context)
			{
				var clusterFile = context.ParseResult.GetValueForOption(ClusterFileOption);
				var connectionString = context.ParseResult.GetValueForOption(ConnectionStringOption);
				var apiVersion = context.ParseResult.GetValueForOption(ApiVersionOption);
				var partition = context.ParseResult.GetValueForOption(PartitionOption);
				var timeout = context.ParseResult.GetValueForOption(TimeoutOption) ?? 30;
				var maxRetries = context.ParseResult.GetValueForOption(RetriesOption) ?? 10;
				var execCommand = context.ParseResult.GetValueForOption(ExecOption);
				var aspire = context.ParseResult.GetValueForOption(AspireOption);

				if (aspire)
				{
					clusterFile = null;
					connectionString = "docker:docker@127.0.0.1:4550";
				}

				if (apiVersion == null)
				{
					apiVersion = !string.IsNullOrEmpty(connectionString) ? 720 : 620;
				}

				var rootPath = string.IsNullOrEmpty(partition) ? FdbPath.Root : FdbPath.Parse(partition);

				string? startCommand = null;
				if (!string.IsNullOrEmpty(execCommand))
				{
					startCommand = execCommand;
				}
				//else if (extra.Count > 0)
				//{ // the remainder of the command line will be the first command to execute
				//	startCommand = string.Join(" ", extra);
				//}

				var builder = new ServiceCollection();

				builder.AddFoundationDb(apiVersion.Value, options =>
				{
					options.ConnectionOptions.ClusterFile = clusterFile;
					options.ConnectionOptions.ConnectionString = connectionString;
					options.ConnectionOptions.Root = rootPath;

					options.ConnectionOptions.DefaultTimeout = TimeSpan.FromSeconds(Math.Max(0, timeout));
					options.ConnectionOptions.DefaultRetryLimit = Math.Max(0, maxRetries);
				});

				builder.AddSingleton<FdbShellRunner>();
				builder.AddSingleton<IFdbShellTerminal, FdbShellConsoleTerminal>();

				var services = builder.BuildServiceProvider();

				var terminal = services.GetRequiredService<IFdbShellTerminal>();

				if (terminal.IsInteractive)
				{
					terminal.SetWindowSize(160, 60);
				}

				var runner = services.GetRequiredService<FdbShellRunner>();

				var shellArgs = new FdbShellRunnerArguments()
				{
					StartCommand = startCommand,
					RunSingleCommand = execCommand != null,
				};

				await runner.RunAsync(shellArgs, this.Cancellation);
			}
		}


	}

	public interface IFdbShellTerminal
	{

		bool IsInteractive { get; }

		void StdOut(string? msg = null, ConsoleColor color = ConsoleColor.DarkGray, bool newLine = true);

		void StdOut(ref DefaultInterpolatedStringHandler msg, ConsoleColor color = ConsoleColor.DarkGray, bool newLine = true);

		void StdErr(string msg, ConsoleColor color = ConsoleColor.DarkGray);

		void StdErr(ref DefaultInterpolatedStringHandler msg, ConsoleColor color = ConsoleColor.DarkGray);

		void Beep();

		bool SetWindowSize(int? width, int? height);

	}

	public class FdbShellConsoleTerminal : IFdbShellTerminal
	{

		public bool IsInteractive => true;

		public bool DisableColors { get; set; }

		public bool IsSilent { get; set; }

		public void Beep()
		{
			if (!this.IsSilent)
			{
				Console.Beep();
			}
		}

		public void StdOut(string? msg = null, ConsoleColor color = ConsoleColor.DarkGray, bool newLine = true)
		{
			if (this.DisableColors)
			{
				if (newLine)
				{
					Console.WriteLine(msg);
				}
				else
				{
					Console.Write(msg);
				}
			}
			else
			{
				var prev = Console.ForegroundColor;
				if (prev != color) Console.ForegroundColor = color;

				if (newLine)
				{
					Console.WriteLine(msg);
				}
				else
				{
					Console.Write(msg);
				}

				if (prev != color) Console.ForegroundColor = prev;
			}
		}

		public void StdOut(ref DefaultInterpolatedStringHandler msg, ConsoleColor color = ConsoleColor.DarkGray, bool newLine = true)
			=> StdOut(string.Create(CultureInfo.InvariantCulture, ref msg), color, newLine);

		public void StdErr(string msg, ConsoleColor color = ConsoleColor.DarkRed)
		{
			if (this.DisableColors)
			{
				Console.Error.WriteLine(msg);
			}
			else
			{
				var prev = Console.ForegroundColor;
				if (prev != color)
				{
					Console.ForegroundColor = color;
				}

				Console.Error.WriteLine(msg);

				if (prev != color)
				{
					Console.ForegroundColor = prev;
				}
			}
		}

		public void StdErr(ref DefaultInterpolatedStringHandler msg, ConsoleColor color = ConsoleColor.DarkRed)
			=> StdErr(string.Create(CultureInfo.InvariantCulture, ref msg), color);

		public bool SetWindowSize(int? width, int? height)
		{
			if (width <= 0) return false;

			if (OperatingSystem.IsWindows())
			{
				try
				{
					if (width > 0)
					{
						Console.WindowWidth = width.Value;
					}

					if (height > 0)
					{
						Console.WindowHeight = height.Value;
					}
				}
				catch (Exception e)
				{
					StdErr($"Failed to change console width: {e.Message}");
					return false;
				}
			}

			return false;
		}

	}

	public static class FdbShellTerminalExtensions
	{

		public static void Comment(this IFdbShellTerminal terminal, string msg) => terminal.StdOut(msg, ConsoleColor.DarkGray);

		public static void Comment(this IFdbShellTerminal terminal, ref DefaultInterpolatedStringHandler msg) => terminal.StdOut(ref msg, ConsoleColor.DarkGray);

		public static void Error(this IFdbShellTerminal terminal, string msg) => terminal.StdErr(msg, ConsoleColor.Red);

		public static void Error(this IFdbShellTerminal terminal, ref DefaultInterpolatedStringHandler msg) => terminal.StdErr(ref msg, ConsoleColor.Red);

		public static void Success(this IFdbShellTerminal terminal, string msg) => terminal.StdOut(msg, ConsoleColor.Green);

		public static void Success(this IFdbShellTerminal terminal, ref DefaultInterpolatedStringHandler msg) => terminal.StdOut(ref msg, ConsoleColor.Green);

		public static void Progress(this IFdbShellTerminal terminal, string msg) => terminal.StdOut(msg, newLine: false);

		public static void Progress(this IFdbShellTerminal terminal, ref DefaultInterpolatedStringHandler msg) => terminal.StdOut(ref msg, newLine: false);

	}

	public sealed record FdbShellRunnerArguments
	{

		public FdbPath InitialPath { get; set; } = FdbPath.Root;

		public string? StartCommand { get; set; }

		public bool RunSingleCommand { get; set; }

	}

	public class FdbShellRunner
	{

		private IFdbDatabaseProvider Db { get; }

		public IFdbShellTerminal Terminal { get; }

		public FdbPath CurrentDirectoryPath { get; private set; }

		public string Description { get; set; } = "?";

		public FdbShellRunner(IFdbDatabaseProvider db, IFdbShellTerminal terminal)
		{
			this.Db = db;
			this.Terminal = terminal;
		}

		private void StdOut(string log, ConsoleColor color = ConsoleColor.DarkGray) => this.Terminal.StdOut(log, color);

		private void StdOut(ref DefaultInterpolatedStringHandler log, ConsoleColor color = ConsoleColor.DarkGray) => this.Terminal.StdErr(string.Create(CultureInfo.InvariantCulture, ref log), color);

		private void StdErr(string log, ConsoleColor color = ConsoleColor.DarkRed) => this.Terminal.StdErr(log, color);

		private void StdErr(ref DefaultInterpolatedStringHandler log, ConsoleColor color = ConsoleColor.DarkGray) => this.Terminal.StdErr(string.Create(CultureInfo.InvariantCulture, ref log), color);

		private static readonly Dictionary<string, string> KnownCommands = new (StringComparer.OrdinalIgnoreCase)
		{
			["cd"] = "Change the current directory",
			["clear"] = "Clear a key in the current directory",
			["clearrange"] = "Clear a range of keys in the current directory",
			["get"] = "Read the value of a key in the current directory",
			["coordinators"] = "Display the list of current coordinators",
			["count"] = "Count the number of keys in the current directory",
			["dir"] = "Display the list of sub-directories",
			["dump"] = "Dump the content of the current directory",
			["quit"] = "Stop the shell",
			["help"] = "Display this list",
			["layer"] = "Display of change the layer id of the current directory",
			["map"] = "Map the content of a directory and display a report",
			["mkdir"] = "Create a new sub-directory",
			["mv"] = "Move a directory to another location",
			["pwd"] = "Prints the current path",
			["ren"] = "Rename a directory",
			["rmdir"] = "Remove a directory",
			["sampling"] = "Perform a random sampling of the keys in a directory",
			["shards"] = "Display the shards that intersect a directory",
			["show"] = "List the keys and values in a directory",
			["status"] = "Display the status of cluster",
			["topology"] = "Display the topology of the cluster",
			["tree"] = "Show all the directories under the current directory",
			["version"] = "Display the version of the client and cluster",
			["wide"] = "Switch to wide screen (only on Windows)",
		};


		public async Task RunAsync(FdbShellRunnerArguments args, CancellationToken cancel)
		{
			StdOut("Connecting to cluster...");

			this.CurrentDirectoryPath = args.InitialPath;

			bool stop = false;
			try
			{
				StdOut($"Using API v{Fdb.ApiVersion} (max {Fdb.GetMaxApiVersion()})", ConsoleColor.Gray);
				if (!string.IsNullOrEmpty(this.Db.ProviderOptions.ConnectionOptions.ConnectionString))
				{
					StdOut($"Cluster file: {(this.Db.ProviderOptions.ConnectionOptions.ConnectionString)}", ConsoleColor.Gray);
				}
				else
				{
					StdOut($"Cluster file: {(this.Db.ProviderOptions.ConnectionOptions.ClusterFile ?? "<default>")}", ConsoleColor.Gray);
				}
				StdOut("");
				StdOut("FoundationDB Shell menu:");
				foreach (var cmd in (string[]) ["cd", "dir", "show", "tree", "help", "quit"])
				{
					StdOut($"  {cmd,-8} {FdbShellRunner.KnownCommands[cmd]}");
				}
				StdOut("");

				try
				{
					var cf = await Fdb.System.GetCoordinatorsAsync(this.Db, cancel);
					this.Description = cf.Description;
					StdOut("Ready...", ConsoleColor.DarkGreen);
				}
				catch (Exception e)
				{
					StdErr($"Failed to get coordinators state from cluster: {e.Message}", ConsoleColor.DarkRed);
					this.Description = "???";
				}
				
				StdOut("");

				var le = new LineEditor("FDBShell");

				le.AutoCompleteEvent = (txt, _) =>
				{
					string[] res;
					int p = txt.IndexOf(' ');
					if (p > 0)
					{
						string cmd = txt[..p];
						string arg = txt[(p + 1)..].Trim();

						if (cmd is "cd" or "rmdir")
						{ // handle completion for directories

							// txt: "cd foo" => prefix = "foo"
							// txt: "cd foobar/b" => prefix = "b"

							bool hasLeadingSlash = arg.EndsWith("/");
							var path = FdbPath.Parse(hasLeadingSlash ? (arg + "!") : arg);
							var parent = path.Count > 1 ? path.GetParent() : path.IsAbsolute ? FdbPath.Root : FdbPath.Empty;
							string search = hasLeadingSlash ? "" : path.Name;

							var subdirs = RunAsyncCommand((db, _, ct) => AutoCompleteDirectories(CombinePath(CurrentDirectoryPath, parent.ToString()), db, ct), cancel).GetAwaiter().GetResult();

							if (subdirs.GetValueOrDefault() == null)
							{
								return new LineEditor.Completion(txt, null);
							}

							res = subdirs.Value!
								.Where(s => s.StartsWith(search, StringComparison.Ordinal))
								.Select(s => (cmd + " " + parent[s])[txt.Length..])
								.ToArray();

							if (res.Length == 1 && res[0] == string.Empty)
							{ // someone was at "cd /Foo/Bar", pressed TAB again, and there is no other match
								// => we interpret it as "want to go in the sub-folder

								res = [ "/" ]; // add a "slash"
							}

							return new LineEditor.Completion(txt, res);
						}

						// unknown command
						return new LineEditor.Completion(txt, null);
					}

					// list of commands
					res = KnownCommands
					      .Where(cmd => cmd.Key.StartsWith(txt, StringComparison.OrdinalIgnoreCase))
					      .Select(cmd => cmd.Key.Substring(txt.Length))
					      .ToArray();
					return new LineEditor.Completion(txt, res);
				};
				le.TabAtStartCompletes = true;

				string? prompt;

				void UpdatePrompt(FdbPath path)
				{
					prompt = $"[fdb:{this.Description} {path}]# ";
				}
				le.PromptColor = ConsoleColor.Cyan;
				UpdatePrompt(CurrentDirectoryPath);

				List<string> history = [];
				string? nextCommand = args.StartCommand;

				while (!stop)
				{
					string s;
					if (nextCommand != null)
					{
						s = nextCommand;
						nextCommand = null;
					}
					else
					{
						s = le.Edit(prompt, "");
					}

					if (s == null) break;

					history.Add(s);

					//TODO: we need a tokenizer that recognizes binary keys, tuples, escaped paths, etc...
					var tokens = Tokenize(s);
					string cmd = (tokens.Count > 0 ? tokens.Get<string>(0) : null) ?? string.Empty;
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

							case "help":
							{
								ShowCommandHelp(this.Terminal);
								break;
							}

							case "version":
							{
								//TODO: get the version from the client or status json
								throw new NotImplementedException();
								//if (clusterFile != null)
								//{
								//	await VersionCommand(extras, clusterFile, Console.Out, cancel);
								//}
								//break;
							}

							case "tree":
							{
								string? prm = PopParam(ref extras);
								var path = CombinePath(CurrentDirectoryPath, prm);
								await RunAsyncCommand((db, log, ct) => BasicCommands.Tree(path, extras, db, log, ct), cancel);
								break;
							}
							case "map":
							{
								string? prm = PopParam(ref extras);
								var path = CombinePath(CurrentDirectoryPath, prm);
								await RunAsyncCommand((db, log, ct) => BasicCommands.Map(path, extras, db, log, ct), cancel);
								break;
							}

							case "dir":
							case "ls":
							{
								string? prm = PopParam(ref extras);
								var path = CombinePath(CurrentDirectoryPath, prm);
								await RunAsyncCommand((db, log, ct) => BasicCommands.Dir(path, extras, BasicCommands.DirectoryBrowseOptions.Default, db, log, ct), cancel);
								break;
							}
							case "ll":
							{
								string? prm = PopParam(ref extras);
								var path = CombinePath(CurrentDirectoryPath, prm);
								await RunAsyncCommand((db, log, ct) => BasicCommands.Dir(path, extras, BasicCommands.DirectoryBrowseOptions.ShowCount, db, log, ct), cancel);
								break;
							}

							case "count":
							{
								string? prm = PopParam(ref extras);
								var path = CombinePath(CurrentDirectoryPath, prm);
								await RunAsyncCommand((db, log, ct) => BasicCommands.Count(path, extras, db, log, ct), cancel);
								break;
							}

							case "show":
							case "top":
							{
								await RunAsyncCommand((db, log, ct) => BasicCommands.Show(CurrentDirectoryPath, extras, false, db, log, ct), cancel);
								break;
							}
							case "last":
							{
								await RunAsyncCommand((db, log, ct) => BasicCommands.Show(CurrentDirectoryPath, extras, true, db, log, ct), cancel);
								break;
							}

							case "dump":
							{
								string? output = PopParam(ref extras);
								if (string.IsNullOrEmpty(output))
								{
									StdErr("You must specify a target file path.", ConsoleColor.Red);
									break;
								}
								await RunAsyncCommand((db, log, ct) => BasicCommands.Dump(CurrentDirectoryPath, output, extras, db, log, ct), cancel);
								break;
							}

							case "cd":
							case "pwd":
							{
								string? prm = PopParam(ref extras);
								if (!string.IsNullOrEmpty(prm))
								{
									var newPath = CombinePath(CurrentDirectoryPath, prm);
									var res = await RunAsyncCommand(
										(db, _, ct) => db.ReadAsync(tr => BasicCommands.TryOpenCurrentDirectoryAsync(tr, newPath), ct),
										cancel
									);
									if (res.Failed)
									{
										StdErr($"# Failed to open Directory {newPath}: {res.Error!.Message}", ConsoleColor.Red);
										this.Terminal.Beep();
									}
									else if (res.Value == null)
									{
										StdOut($"# Directory {newPath} does not exist!", ConsoleColor.Red);
										this.Terminal.Beep();
									}
									else
									{
										CurrentDirectoryPath = newPath;
										UpdatePrompt(CurrentDirectoryPath);
									}
								}
								else
								{
									var res = await RunAsyncCommand(
										(db, _, ct) => db.ReadAsync(tr => BasicCommands.TryOpenCurrentDirectoryAsync(tr, CurrentDirectoryPath), ct),
										cancel
									);
									if (res.Failed)
									{
										StdErr($"# Failed to query Directory {this.CurrentDirectoryPath}: {res.Error!.Message}", ConsoleColor.Red);
									}
									else if (res.Value == null)
									{
										StdOut($"# Directory {this.CurrentDirectoryPath} does not exist anymore");
									}
								}

								break;
							}
							case "mkdir":
							case "md":
							{ // "mkdir DIRECTORYNAME"

								string? prm = PopParam(ref extras);
								if (!string.IsNullOrEmpty(prm))
								{
									var path = CombinePath(CurrentDirectoryPath, prm);
									await RunAsyncCommand((db, log, ct) => BasicCommands.CreateDirectory(path, extras, db, log, ct), cancel);
								}

								break;
							}
							case "rmdir":
							{ // "rmdir DIRECTORYNAME"
								string? prm = PopParam(ref extras);
								if (!string.IsNullOrEmpty(prm))
								{
									var path = CombinePath(CurrentDirectoryPath, prm);
									await RunAsyncCommand((db, log, ct) => BasicCommands.RemoveDirectory(path, extras, db, log, ct), cancel);
								}

								break;
							}

							case "mv":
							case "ren":
							{ // "mv SOURCE DESTINATION"

								string? prm = PopParam(ref extras);
								var srcPath = CombinePath(CurrentDirectoryPath, prm);
								var dstPath = CombinePath(CurrentDirectoryPath, extras.Get<string>(0));
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

								await RunAsyncCommand((db, log, ct) => BasicCommands.Get(CurrentDirectoryPath, extras, db, log, ct), cancel);
								break;
							}

							case "clear":
							{ // "clear KEY"

								if (extras.Count == 0)
								{
									StdErr("You must specify a key to clear.", ConsoleColor.Red);
									break;
								}

								await RunAsyncCommand((db, log, ct) => BasicCommands.Clear(CurrentDirectoryPath, extras, db, log, ct), cancel);
								break;
							}

							case "clearrange":
							{ // "clear *" or "clear FROM TO"

								if (extras.Count == 0)
								{
									StdErr("You must specify either '*', a prefix, or a key range.", ConsoleColor.Red);
									break;
								}

								await RunAsyncCommand((db, log, ct) => BasicCommands.ClearRange(CurrentDirectoryPath, extras, db, log, ct), cancel);
								break;
							}

							case "layer":
							{
								string? prm = PopParam(ref extras);
								if (string.IsNullOrEmpty(prm))
								{ // displays the layer id of the current folder
									await RunAsyncCommand((db, log, ct) => BasicCommands.ShowDirectoryLayer(CurrentDirectoryPath, extras, db, log, ct), cancel);

								}
								else
								{ // change the layer id of the current folder
									prm = prm.Trim();
									// double or single quotes can be used to escape the value
									if (prm.Length >= 2 && (prm.StartsWith("'") && prm.EndsWith("'")) || (prm.StartsWith("\"") && prm.EndsWith("\"")))
									{
										prm = prm.Substring(1, prm.Length - 2);
									}

									await RunAsyncCommand((db, log, ct) => BasicCommands.ChangeDirectoryLayer(CurrentDirectoryPath, prm, extras, db, log, ct), cancel);
								}

								break;
							}

							case "mkpart":
							{ // "mkpart PARTITIONNAME"

								string? prm = PopParam(ref extras);
								if (!string.IsNullOrEmpty(prm))
								{
									var path = CombinePath(CurrentDirectoryPath, prm);
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
								string? prm = PopParam(ref extras);
								var path = CombinePath(CurrentDirectoryPath, prm);
								await RunAsyncCommand((db, log, ct) => BasicCommands.Shards(path, extras, db, log, ct), cancel);
								break;
							}

							case "sampling":
							{
								string? prm = PopParam(ref extras);
								var path = CombinePath(CurrentDirectoryPath, prm);
								await RunAsyncCommand((db, log, ct) => BasicCommands.Sampling(path, extras, db, log, ct), cancel);
								break;
							}

							case "coordinators":
							{
								await RunAsyncCommand((db, log, ct) => CoordinatorsCommand(db, ct), cancel);
								break;
							}

							case "partition":
							{
								//TODO: how to re-implement this method?
								throw new NotSupportedException();

								//string? prm = PopParam(ref extras);
								//if (string.IsNullOrEmpty(prm))
								//{
								//	StdOut($"# Current partition is {partition}");
								//	//TODO: browse existing partitions ?
								//	break;
								//}

								//var newPartition = FdbPath.Parse(prm.Trim());
								//IFdbDatabase? newDb = null;
								//try
								//{
								//	var options = new FdbConnectionOptions
								//	{
								//		ClusterFile = clusterFile,
								//		Root = newPartition
								//	};
								//	newDb = await ChangeDatabase(options, cancel);
								//}
								//catch (Exception)
								//{
								//	newDb?.Dispose();
								//	newDb = null;
								//	throw;
								//}
								//finally
								//{
								//	if (newDb != null)
								//	{
								//		if (Db != null)
								//		{
								//			Db.Dispose();
								//			Db = null;
								//		}

								//		Db = newDb;
								//		partition = newPartition;
								//		StdOut($"# Changed partition to {partition}");
								//	}
								//}

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
								this.Terminal.Progress("Collecting garbage...");
								GC.Collect();
								GC.WaitForPendingFinalizers();
								GC.Collect();
								StdOut(" Done");
								long after = GC.GetTotalMemory(false);
								StdOut($"- before = {before:N0}");
								StdOut($"- after  = {after:N0}");
								StdOut($"- delta  = {(before - after):N0}");
								break;
							}

							case "mem":
							{
								//TODO: implement this in a more cross platform way!
								throw new NotSupportedException();
								//StdOut("Memory usage:");
								//StdOut("- Managed Mem  : " + GC.GetTotalMemory(false).ToString("N0"));
								//if (OperatingSystem.IsWindows())
								//{
								//	StdOut("- Working Set  : " + PerfCounters.WorkingSet!.NextValue().ToString("N0") + " (peak " + PerfCounters.WorkingSetPeak!.NextValue().ToString("N0") + ")");
								//	StdOut("- Virtual Bytes: " + PerfCounters.VirtualBytes!.NextValue().ToString("N0") + " (peak " + PerfCounters.VirtualBytesPeak!.NextValue().ToString("N0") + ")");
								//	StdOut("- Private Bytes: " + PerfCounters.PrivateBytes!.NextValue().ToString("N0"));
								//	StdOut("- BytesInAlHeap: " + PerfCounters.ClrBytesInAllHeaps!.NextValue().ToString("N0"));
								//}
								break;
							}

							case "wide":
							{
								this.Terminal.SetWindowSize(160, null);

								break;
							}

							case "status":
							case "wtf":
							{
								var clusterFile = this.Db.ProviderOptions.ConnectionOptions.ClusterFile;

								if (string.IsNullOrEmpty(this.Db.ProviderOptions.ConnectionOptions.ConnectionString))
								{
									//TODO: write the string to a temp file?
									throw new NotSupportedException("Needs a cluster file");
								}

								var result = await RunAsyncCommand((_, log, ct) => FdbCliCommands.RunFdbCliCommand("status details", null, clusterFile, log, ct), cancel);
								if (result.Failed) break;
								if (result.GetValueOrDefault()?.ExitCode != 0)
								{
									StdErr($"# fdbcli exited with code {result.Value?.ExitCode}", ConsoleColor.DarkRed);
									StdOut("> StdErr:", ConsoleColor.DarkGray);
									StdOut(result.Value?.StdErr ?? "");
									StdOut("> StdOut:", ConsoleColor.DarkGray);
								}

								StdOut(result.Value?.StdOut ?? "");
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
						StdErr($"Failed to execute command '{trimmedCommand}': {e.Message}", ConsoleColor.Red);
#if DEBUG
						StdErr(e.ToString(), ConsoleColor.DarkRed);
#endif
					}

					if (args.RunSingleCommand)
					{ // only run one command, and then exit
						break;
					}

				}
			}
			finally
			{
				StdOut("Bye");
				this.Db.Dispose();
			}
		}

		private void ShowCommandHelp(IFdbShellTerminal terminal)
		{
			terminal.StdOut("List of supported commands:");
			foreach (var (k, v) in FdbShellRunner.KnownCommands.OrderBy(kv => kv.Key))
			{
				terminal.StdOut($"  {k,-14} {v}");
			}
		}

		public async Task RunAsyncCommand(Func<IFdbDatabase, IFdbShellTerminal, CancellationToken, Task> command, CancellationToken cancel)
		{
			//var db = Db ?? throw new InvalidOperationException("DB not available");

			using(var cts = CancellationTokenSource.CreateLinkedTokenSource(cancel))
			{ 
				try
				{
					var db = await this.Db.GetDatabase(cancel);

					await command(db, this.Terminal, cts.Token).ConfigureAwait(false);
				}
				finally
				{
					await cts.CancelAsync();
				}
			}
		}

		public async Task<Doxense.Maybe<T>> RunAsyncCommand<T>(Func<IFdbDatabase, IFdbShellTerminal, CancellationToken, Task<T>> command, CancellationToken cancel)
		{
			using(var cts = CancellationTokenSource.CreateLinkedTokenSource(cancel))
			{
				try
				{
					var db = await this.Db.GetDatabase(cancel);

					return Doxense.Maybe.Return<T>(await command(db, this.Terminal, cts.Token).ConfigureAwait(false));
				}
				catch (Exception e)
				{
					return Doxense.Maybe.Error<T>(e);
				}
				finally
				{
					await cts.CancelAsync();
				}
			}
		}

		private static string? PopParam(ref IVarTuple extras)
		{
			if (extras.Count == 0)
			{
				return null;
			}

			string? prm = extras.Get<string>(0);
			extras = extras.Substring(1);
			return prm;
		}

		#region Directories...

		private static bool HasIndirection(FdbPath path)
		{
			foreach (var seg in path)
			{
				if (seg.Name == "." || seg.Name == "..") return true;
			}
			return false;
		}

		private static FdbPath RemoveIndirection(FdbPath path)
		{
			Debug.Assert(path.IsAbsolute, "Path must be absolute");

			var segments = new List<FdbPathSegment>(path.Count);
			foreach (var seg in path.Segments.Span)
			{
				if (seg.Name == ".") continue;
				if (seg.Name == "..")
				{
					if (segments.Count == 0)
					{
						throw new InvalidOperationException("The specified path is invalid.");
					}
					segments.RemoveAt(segments.Count - 1);
					continue;
				}
				segments.Add(seg);
			}
			return FdbPath.Absolute(segments);
		}

		private static FdbPath CombinePath(FdbPath parent, string? children)
		{
			if (string.IsNullOrEmpty(children) || children == ".") return parent;

			if (children == ".." && parent.IsAbsolute && parent.Count > 0)
			{
				return parent.GetParent();
			}

			var p = FdbPath.Parse(children);
			if (!p.IsAbsolute) p = parent[p];
			if (HasIndirection(p)) p = RemoveIndirection(p);
			return p;
		}

		private async Task<string[]?> AutoCompleteDirectories(FdbPath path, IFdbDatabase db, CancellationToken ct)
		{
			var parent = await db.ReadAsync(tr => BasicCommands.TryOpenCurrentDirectoryAsync(tr, path), ct);
			if (parent == null) return null;

			var paths = await db.ReadAsync(tr => parent.ListAsync(tr), ct);
			return paths.Select(p => p.Name).ToArray();
		}

		#endregion

		private async Task VersionCommand(IVarTuple _, string? clusterFile, IFdbShellTerminal terminal, CancellationToken cancel)
		{
			terminal.StdOut($"Using .NET Binding v{new System.Reflection.AssemblyName(typeof(Fdb).Assembly.FullName!).Version} with API level {Fdb.ApiVersion}");
			var res = await RunAsyncCommand((_, _, ct) => FdbCliCommands.RunFdbCliCommand(null, "-h", clusterFile, terminal, ct), cancel);
			if (res.GetValueOrDefault()?.ExitCode == 0)
			{
				//HACK HACK HACK
				terminal.StdOut($"Found {res.Value!.StdOut.Split('\n')[0]}");
			}
			else
			{
				terminal.StdOut("# Failed to execute fdbcli :(");
			}
			terminal.StdOut();
		}

		private async Task CoordinatorsCommand(IFdbDatabase db, CancellationToken ct)
		{
			var cf = await Fdb.System.GetCoordinatorsAsync(db, ct);
			this.Description = cf.Description;
			StdOut($"Connnected to: {cf.Description} ({cf.Id})", ConsoleColor.Gray);
			StdOut($"Found {cf.Coordinators.Length} coordinator(s):");
			foreach (var coordinator in cf.Coordinators)
			{
				//string hostName = null;
				//try
				//{
				//	var ipHost = Dns.GetHostEntry(coordinator.Address);
				//	hostName = " (" + ipHost.HostName + ")";
				//}
				//catch (Exception) { }

				StdOut($"  {coordinator.Address}:{coordinator.Port}", ConsoleColor.White);
			}
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
							STuple.Deformatter.ParseNext(exp, out var tok, out var tail);
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
