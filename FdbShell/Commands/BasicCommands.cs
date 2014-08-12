using FoundationDB.Client;
using FoundationDB.Layers.Directories;
using FoundationDB.Layers.Tuples;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace FdbShell
{
	public static class BasicCommands
	{

		[Flags]
		public enum DirectoryBrowseOptions
		{
			Default = 0,
			ShowFirstKeys = 1,
			ShowCount = 2,
		}

		public static async Task<IFdbDirectory> TryOpenCurrentDirectoryAsync(string[] path, IFdbDatabase db, CancellationToken ct)
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

		public static async Task Dir(string[] path, IFdbTuple extras, DirectoryBrowseOptions options, IFdbDatabase db, TextWriter log, CancellationToken ct)
		{
			if (log == null) log = Console.Out;

			log.WriteLine("# Listing {0}:", String.Join("/", path));

			var parent = await TryOpenCurrentDirectoryAsync(path, db, ct);
			if (parent == null)
			{
				log.WriteLine("  Directory not found.");
				return;
			}

			if (parent.Layer.IsPresent)
			{
				log.WriteLine("# Layer: {0}", parent.Layer.ToAsciiOrHexaString());
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
							if (!(subfolder is FdbDirectoryPartition))
							{
								long count = await Fdb.System.EstimateCountAsync(db, subfolder.ToRange(), ct);
								log.WriteLine("  {0,-12} {1,-12} {3,9:N0} {2}", FdbKey.Dump(subfolder.Copy().Key), subfolder.Layer.IsNullOrEmpty ? "-" : ("<" + subfolder.Layer.ToUnicode() + ">"), name, count);
							}
							else
							{
								log.WriteLine("  {0,-12} {1,-12} {3,9:N0} {2}", FdbKey.Dump(subfolder.Copy().Key), subfolder.Layer.IsNullOrEmpty ? "-" : ("<" + subfolder.Layer.ToUnicode() + ">"), name, "-");
							}
						}
						else
						{
							log.WriteLine("  {0,-12} {1,-12} {2}", FdbKey.Dump(subfolder.Copy().Key), subfolder.Layer.IsNullOrEmpty ? "-" : ("<" + subfolder.Layer.ToUnicode() + ">"), name);
						}
					}
					else
					{
						log.WriteLine("  WARNING: {0} seems to be missing!", name);
					}
				}
				log.WriteLine("  {0} sub-directorie(s).", folders.Count);
			}
			else
			{
				//TODO: test if it contains data?
				log.WriteLine("  No sub-directories.");
			}
		}

		/// <summary>Creates a new directory</summary>
		public static async Task CreateDirectory(string[] path, IFdbTuple extras, IFdbDatabase db, TextWriter log, CancellationToken ct)
		{
			if (log == null) log = Console.Out;

			string layer = extras.Count > 0 ? extras.Get<string>(0) : null;

			log.WriteLine("# Creating directory {0} with layer '{1}'", String.Join("/", path), layer);

			var folder = await db.Directory.TryOpenAsync(path, cancellationToken: ct);
			if (folder != null)
			{
				log.WriteLine("- Directory {0} already exists!", string.Join("/", path));
				return;
			}

			folder = await db.Directory.TryCreateAsync(path, Slice.FromString(layer), cancellationToken: ct);
			log.WriteLine("- Created under {0} [{1}]", FdbKey.Dump(folder.Key), folder.Key.ToHexaString(' '));

			// look if there is already stuff under there
			var stuff = await db.ReadAsync((tr) => tr.GetRange(folder.ToRange()).FirstOrDefaultAsync(), cancellationToken: ct);
			if (stuff.Key.IsPresent)
			{
				log.WriteLine("CAUTION: There is already some data under {0} !");
				log.WriteLine("  {0} = {1}", FdbKey.Dump(stuff.Key), stuff.Value.ToAsciiOrHexaString());
			}
		}

		/// <summary>Remove a directory and all its data</summary>
		public static async Task RemoveDirectory(string[] path, IFdbTuple extras, IFdbDatabase db, TextWriter log, CancellationToken ct)
		{
			if (log == null) log = Console.Out;

			string layer = extras.Count > 0 ? extras.Get<string>(0) : null;

			var folder = await db.Directory.TryOpenAsync(path, cancellationToken: ct);
			if (folder == null)
			{
				log.WriteLine("# Directory {0} does not exist", string.Join("/", path));
				return;
			}

			// are there any subdirectories ?
			var subDirs = await folder.TryListAsync(db, ct);
			if (subDirs.Count > 0)
			{
				//TODO: "-r" flag ?
				log.WriteLine("# Cannot remove {0} because it still contains {1} sub-directorie(s)", string.Join("/", path), subDirs.Count);
			}

			//TODO: ask for confirmation?

			log.WriteLine("# Deleting directory {0} ...", String.Join("/", path));
			await folder.RemoveAsync(db, ct);
			log.WriteLine("# Gone!");
		}

		public static async Task ShowDirectoryLayer(string[] path, IFdbTuple extras, IFdbDatabase db, TextWriter log, CancellationToken ct)
		{
			var dir = await BasicCommands.TryOpenCurrentDirectoryAsync(path, db, ct);
			if (dir == null)
			{
				log.WriteLine("# Directory {0} does not exist anymore", String.Join("/", path));
			}
			else
			{
				if (dir.Layer == FdbDirectoryPartition.LayerId)
					log.WriteLine("# Directory {0} is a partition", String.Join("/", path));
				else if (dir.Layer.IsPresent)
					log.WriteLine("# Directory {0} has layer {1}", String.Join("/", path), dir.Layer.ToAsciiOrHexaString());
				else
					log.WriteLine("# Directory {0} does not have a layer defined", String.Join("/", path));
			}
		}

		public static async Task ChangeDirectoryLayer(string[] path, string layer, IFdbTuple extras, IFdbDatabase db, TextWriter log, CancellationToken ct)
		{
			var dir = await BasicCommands.TryOpenCurrentDirectoryAsync(path, db, ct);
			if (dir == null)
			{
				log.WriteLine("# Directory {0} does not exist anymore", String.Join("/", path));
			}
			else
			{
				dir = await db.ReadWriteAsync((tr) => dir.ChangeLayerAsync(tr, Slice.FromString(layer)), ct);
				log.WriteLine("# Directory {0} layer changed to {1}", String.Join("/", path), dir.Layer.ToAsciiOrHexaString());
			}
		}

		/// <summary>Counts the number of keys inside a directory</summary>
		public static async Task Count(string[] path, IFdbTuple extras, IFdbDatabase db, TextWriter log, CancellationToken ct)
		{
			// look if there is something under there
			var folder = (await TryOpenCurrentDirectoryAsync(path, db, ct)) as FdbDirectorySubspace;
			if (folder == null)
			{
				log.WriteLine("# Directory {0} does not exist", path);
				return;
			}

			var copy = folder.Copy();
			log.WriteLine("# Counting keys under {0} ...", FdbKey.Dump(copy.Key));

			var progress = new Progress<FdbTuple<long, Slice>>((state) =>
			{
				log.Write("\r# Found {0:N0} keys...", state.Item1);
			});

			long count = await Fdb.System.EstimateCountAsync(db, copy.ToRange(), progress, ct);
			log.WriteLine("\r# Found {0:N0} keys in {1}", count, String.Join("/", folder.Path));
		}

		/// <summary>Shows the first few keys of a directory</summary>
		public static async Task Show(string[] path, IFdbTuple extras, IFdbDatabase db, TextWriter log, CancellationToken ct)
		{
			int count = 20;
			if (extras.Count > 0)
			{
				int x = extras.Get<int>(0);
				if (x > 0) count = x;
			}

			// look if there is something under there
			var folder = await db.Directory.TryOpenAsync(path, cancellationToken: ct);
			if (folder != null)
			{
				log.WriteLine("# Content of {0} [{1}]", FdbKey.Dump(folder.Key), folder.Key.ToHexaString(' '));
				var keys = await db.ReadAsync((tr) => tr.GetRange(folder.ToRange()).Take(count + 1).ToListAsync(), cancellationToken: ct);
				if (keys.Count > 0)
				{
					foreach (var key in keys.Take(count))
					{
						log.WriteLine("...{0} = {1}", FdbKey.Dump(folder.Extract(key.Key)), key.Value.ToAsciiOrHexaString());
					}
					if (keys.Count == count + 1)
					{
						log.WriteLine("... more");
					}
				}
				else
				{
					log.WriteLine("  no content found");
				}
			}
		}

		/// <summary>Display a tree of a directory's children</summary>
		public static async Task Tree(string[] path, IFdbTuple extras, IFdbDatabase db, TextWriter log, CancellationToken ct)
		{
			if (log == null) log = Console.Out;

			log.WriteLine("# Tree of {0}:", String.Join("/", path));

			FdbDirectorySubspace root = null;
			if (path.Length > 0) root = await db.Directory.TryOpenAsync(path, cancellationToken: ct);

			await TreeDirectoryWalk(root, new List<bool>(), db, log, ct);

			log.WriteLine("# done");
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

		public static async Task Map(string[] path, IFdbTuple extras, IFdbDatabase db, TextWriter log, CancellationToken ct)
		{
			// we want to merge the map of shards, with the map of directories from the Directory Layer, and count for each directory how many shards intersect


			var folder = await TryOpenCurrentDirectoryAsync(path, db, ct);
			if (folder == null)
			{
				log.WriteLine("# Directory not found");
				return;
			}

			FdbKeyRange span = FdbKeyRange.All;
			FdbDirectoryLayer layer = db.Directory.DirectoryLayer;
			if (folder is FdbDirectorySubspace)
			{
				span = ((FdbDirectorySubspace)folder).Copy().ToRange();
				layer = ((FdbDirectorySubspace)folder).DirectoryLayer;
			}

			// note: this may break in future versions of the DL! Maybe we need a custom API to get a flat list of all directories in a DL that span a specific range ?

			var shards = await Fdb.System.GetChunksAsync(db, span, ct);
			int totalShards = shards.Count;
			log.WriteLine("Found {0} shard(s) in this partition", totalShards);

			log.WriteLine("Listing all directories...");
			var rootNode = layer.NodeSubspace.Partition(layer.NodeSubspace.Key);
			var subDirs = rootNode.Partition(0);

			var map = new Dictionary<string, int>(StringComparer.Ordinal);
			Action<string[], int> account = (p, c) =>
			{
				for (int i = 1; i <= p.Length; i++)
				{
					var s = "/" + String.Join("/", p, 0, i);
					int x;
					map[s] = map.TryGetValue(s, out x) ? (x + c) : c;
				}
			};

			var work = new Stack<IFdbDirectory>();
			work.Push(folder);

			var dirs = new List<IFdbDirectory>();
			int n = 0;
			while(work.Count > 0)
			{
				var cur = work.Pop();
				// skip sub partitions

				var names = await cur.ListAsync(db, ct);
				foreach(var name in names)
				{
					var sub = await cur.TryOpenAsync(db, name, ct);
					if (sub != null)
					{
						var p = String.Join("/", sub.Path);
						if (sub is FdbDirectoryPartition)
						{
							log.WriteLine("\r! Skipping partition {0}     ", sub.Name);
							n = 0;
							continue;
						}
						log.Write("\r/{0}{1}", p, p.Length > n ? String.Empty : new string(' ', n - p.Length));
						n = p.Length;
						work.Push(sub);
						dirs.Add(sub);
					}
				}
			}
			log.WriteLine("\r> Found {0} sub-directories", dirs.Count);

			log.WriteLine();
			log.WriteLine("Estimating size of each directory...");
			int foundShards = 0;
			n = 0;
			int max = 0;
			IFdbDirectory bigBad = null;
			foreach (var dir in dirs)
			{
				log.Write("\r> {0}{1}", dir.Name, dir.Name.Length > n ? String.Empty : new string(' ', n - dir.Name.Length));
				n = dir.Name.Length;

				var p = dir.Path.ToArray();
				var key = ((FdbSubspace)dir).Key;
				shards = await Fdb.System.GetChunksAsync(db, FdbKeyRange.StartsWith(key), ct);
				Console.WriteLine("/{0} under {1} with {2} shard(s)", string.Join("/", p), FdbKey.Dump(key), shards.Count);
				foundShards += shards.Count;
				account(p, shards.Count);
				if (shards.Count > max) { max = shards.Count; bigBad = dir; }
			}
			log.WriteLine("\rFound a total of {0} shard(s) in {1} folder(s)", foundShards, dirs.Count);
			log.WriteLine();

			log.WriteLine();
			log.WriteLine("Shards %Total     Path");
			foreach(var kvp in map.OrderBy(x => x.Key))
			{
				log.WriteLine("{0,6} {1,-10} {2}", kvp.Value, RobustHistogram.FormatHistoBar((double)kvp.Value / foundShards, 10), kvp.Key);
			}

			if (bigBad != null)
			{
				log.WriteLine();
				log.WriteLine("Biggest folder is /{0} with {1} shards ({2:N1}%)", String.Join("/", bigBad.Path), max, 100.0 * max / totalShards);
			}

			log.WriteLine();
		}

		private static string FormatSize(long size, CultureInfo ci = null)
		{
			ci = ci ?? CultureInfo.InvariantCulture;
			if (size < 2048) return size.ToString("N0", ci);
			double x = size / 1024.0;
			if (x < 800) return x.ToString("N1", ci) + " k";
			x /= 1024.0;
			if (x < 800) return x.ToString("N2", ci) + " M";
			x /= 1024.0;
			return x.ToString("N2", ci) + " G";
		}

		public static async Task Topology(string[] path, IFdbTuple extras, IFdbDatabase db, TextWriter log, CancellationToken ct)
		{
			// estimate the number of machines...
			Console.WriteLine("# Detecting cluster topology...");
			var servers = await db.QueryAsync(tr => tr
				.WithAccessToSystemKeys()
				.GetRange(FdbKeyRange.StartsWith(Fdb.System.ServerList))
				.Select(kvp => new
				{
					Node = kvp.Value.Substring(8, 16).ToHexaString(),
					Machine = kvp.Value.Substring(24, 16).ToHexaString(),
					DataCenter = kvp.Value.Substring(40, 16).ToHexaString()
				}),
				ct
			);

			var numNodes = servers.Select(s => s.Node).Distinct().Count();
			var numMachines = servers.Select(s => s.Machine).Distinct().Count();
			var numDCs = servers.Select(s => s.DataCenter).Distinct().Count();

			Console.WriteLine("Found " + numNodes + " process(es) on " + numMachines + " machine(s) in " + numDCs + " datacenter(s)");
			foreach(var dc in servers.GroupBy(x => x.DataCenter))
			{
				Console.WriteLine("> DataCenter {0} ({1})", dc.Key, dc.Count());
				foreach(var machine in dc.GroupBy(x => x.Machine))
				{
					Console.WriteLine("  > Machine {0} ({1})", machine.Key, machine.Count());
					foreach(var proc in machine)
					{
						Console.WriteLine("    > Process {0}", proc.Node);
					}
				}
			}
			Console.WriteLine();
		}

		public static async Task Shards(string[] path, IFdbTuple extras, IFdbDatabase db, TextWriter log, CancellationToken ct)
		{
			var ranges = await Fdb.System.GetChunksAsync(db, FdbKey.MinValue, FdbKey.MaxValue, ct);
			Console.WriteLine("Found {0} shards in the whole cluster", ranges.Count);

			// look if there is something under there
			var folder = (await TryOpenCurrentDirectoryAsync(path, db, ct)) as FdbDirectorySubspace;
			if (folder != null)
			{
				var r = FdbKeyRange.StartsWith(folder.Copy().Key);
				Console.WriteLine("Searching for shards that intersect with /{0} ...", String.Join("/", path));
				ranges = await Fdb.System.GetChunksAsync(db, r, ct);
				Console.WriteLine("Found {0} ranges intersecting {1}:", ranges.Count, r);
				var last = Slice.Empty;
				foreach (var range in ranges)
				{
					Console.Write("> " + FdbKey.Dump(range.Begin) + " ...");
					long count = await Fdb.System.EstimateCountAsync(db, range, ct);
					Console.WriteLine(" {0:N0}", count);
					last = range.End;
					//TODO: we can probably get more details on this shard looking in the system keyspace (where it is, how many replicas, ...)
				}
				Console.WriteLine("> ... " + FdbKey.Dump(last));
			}

			//Console.WriteLine("Found " + ranges.Count + " shards in the cluster");
			//TODO: shards that intersect the current directory
		}

		public static async Task Sampling(string[] path, IFdbTuple extras, IFdbDatabase db, TextWriter log, CancellationToken ct)
		{
			double ratio = 0.1d;
			bool auto = true;
			if (extras.Count > 0)
			{
				double x = extras.Get<double>(0);
				if (x > 0 && x <= 1) ratio = x;
				auto = false;
			}

			var folder = await TryOpenCurrentDirectoryAsync(path, db, ct);
			FdbKeyRange span;
			if (folder is FdbDirectorySubspace)
			{
				span = FdbKeyRange.StartsWith((folder as FdbDirectorySubspace).Copy());
				log.WriteLine("Reading list of shards for /{0} under {1} ...", String.Join("/", path), FdbKey.Dump(span.Begin));
			}
			else
			{
				log.WriteLine("Reading list of shards for the whole cluster ...");
				span = FdbKeyRange.All;
			}

			// dump keyServers
			var ranges = await Fdb.System.GetChunksAsync(db, span, ct);
			log.WriteLine("> Found {0:N0} shard(s)", ranges.Count);

			// take a sample
			var samples = new List<FdbKeyRange>();

			if (ranges.Count <= 32)
			{ // small enough to scan it all
				samples.AddRange(ranges);
				log.WriteLine("Sampling all {0:N0} shards ...", samples.Count);
			}
			else
			{ // need to take a random subset
				var rnd = new Random();
				int sz = Math.Max((int)Math.Ceiling(ratio * ranges.Count), 1);
				if (auto)
				{
					if (sz > 100) sz = 100; //SAFETY
					if (sz < 32) sz = Math.Max(sz, Math.Min(32, ranges.Count));
				}

				var population = new List<FdbKeyRange>(ranges);
				for (int i = 0; i < sz; i++)
				{
					int p = rnd.Next(population.Count);
					samples.Add(population[p]);
					population.RemoveAt(p);
				}
				log.WriteLine("Sampling " + samples.Count + " out of " + ranges.Count + " shards (" + (100.0 * samples.Count / ranges.Count).ToString("N1") + "%) ...");
			}

			log.WriteLine();
			const string FORMAT_STRING = "{0,9} ║{1,10}{6,6} {2,-29} ║{3,10}{7,7} {4,-37} ║{5,10}";
			const string SCALE_KEY = "....--------========########M";
			const string SCALE_VAL = "....--------========########@@@@@@@@M";
			log.WriteLine(FORMAT_STRING, "Count", "Keys", SCALE_KEY, "Values", SCALE_VAL, "Total", "med.", "med.");

			var rangeOptions = new FdbRangeOptions { Mode = FdbStreamingMode.WantAll };

			samples = samples.OrderBy(x => x.Begin).ToList();

			long total = 0;
			int workers = 8; // Math.Max(4, Environment.ProcessorCount);

			var sw = Stopwatch.StartNew();
			var tasks = new List<Task>();
			int n = samples.Count;
			while (samples.Count > 0)
			{
				while (tasks.Count < workers && samples.Count > 0)
				{
					var range = samples[0];
					samples.RemoveAt(0);
					tasks.Add(Task.Run(async () =>
					{
						var kk = new RobustHistogram(RobustHistogram.TimeScale.Ticks);
						var vv = new RobustHistogram(RobustHistogram.TimeScale.Ticks);

						#region Method 1: get_range everything...

						using (var tr = db.BeginTransaction(ct))
						{
							long keySize = 0;
							long valueSize = 0;
							long count = 0;

							int iter = 0;
							var beginSelector = FdbKeySelector.FirstGreaterOrEqual(range.Begin);
							var endSelector = FdbKeySelector.FirstGreaterOrEqual(range.End);
							while (true)
							{
								FdbRangeChunk data = default(FdbRangeChunk);
								FdbException error = null;
								try
								{
									data = await tr.Snapshot.GetRangeAsync(
										beginSelector,
										endSelector,
										rangeOptions,
										iter
									).ConfigureAwait(false);
								}
								catch (FdbException e)
								{
									error = e;
								}

								if (error != null)
								{
									await tr.OnErrorAsync(error.Code).ConfigureAwait(false);
									continue;
								}

								if (data.Count == 0) break;

								count += data.Count;
								foreach (var kvp in data.Chunk)
								{
									keySize += kvp.Key.Count;
									valueSize += kvp.Value.Count;

									kk.Add(TimeSpan.FromTicks(kvp.Key.Count));
									vv.Add(TimeSpan.FromTicks(kvp.Value.Count));
								}

								if (!data.HasMore) break;

								beginSelector = FdbKeySelector.FirstGreaterThan(data.Last.Key);
								++iter;
							}

							long totalSize = keySize + valueSize;
							Interlocked.Add(ref total, totalSize);

							lock (log)
							{
								log.WriteLine(FORMAT_STRING, count.ToString("N0"), FormatSize(keySize), kk.GetDistribution(begin: 1, end: 12000, fold: 2), FormatSize(valueSize), vv.GetDistribution(begin: 1, end: 120000, fold: 2), FormatSize(totalSize), FormatSize((int)Math.Ceiling(kk.Median)), FormatSize((int)Math.Ceiling(vv.Median)));
							}
						}
						#endregion

						#region Method 2: estimate the count using key selectors...

						//long counter = await Fdb.System.EstimateCountAsync(db, range, ct);
						//Console.WriteLine("COUNT = " + counter.ToString("N0"));

						#endregion
					}, ct));
				}

				var done = await Task.WhenAny(tasks);
				tasks.Remove(done);
			}

			await Task.WhenAll(tasks);
			sw.Stop();

			log.WriteLine();
			if (n != ranges.Count)
			{
				log.WriteLine("Sampled " + FormatSize(total) + " (" + total.ToString("N0") + " bytes) in " + sw.Elapsed.TotalSeconds.ToString("N1") + " sec");
				log.WriteLine("> Estimated total size is " + FormatSize(total * ranges.Count / n));
			}
			else
			{
				log.WriteLine("Found " + FormatSize(total) + " (" + total.ToString("N0") + " bytes) in " + sw.Elapsed.TotalSeconds.ToString("N1") + " sec");
				// compare to the whole cluster
				ranges = await Fdb.System.GetChunksAsync(db, FdbKey.MinValue, FdbKey.MaxValue, ct);
				log.WriteLine("> This directory contains ~{0:N2}% of all data", (100.0 * n / ranges.Count));
			}
			log.WriteLine();
		}

	}
}
