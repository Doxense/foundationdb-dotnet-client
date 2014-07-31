using FoundationDB.Client;
using FoundationDB.Layers.Directories;
using FoundationDB.Layers.Tuples;
using System;
using System.Collections.Generic;
using System.Diagnostics;
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
				log.WriteLine("  No sub-directories.");
			}
		}

		/// <summary>Creates a new directory</summary>
		public static async Task Create(string[] path, IFdbTuple extras, IFdbDatabase db, TextWriter log, CancellationToken ct)
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
				Console.Write("\r# Found {0:N0} keys...", state.Item1);
			});

			long count = await Fdb.System.EstimateCountAsync(db, copy.ToRange(), progress, ct);
			Console.WriteLine("\r# Found {0:N0} keys in {1}", count, String.Join("/", folder.Path));
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
		public static async Task Tree(string[] path, IFdbTuple extras, IFdbDatabase db, TextWriter stream, CancellationToken ct)
		{
			if (stream == null) stream = Console.Out;

			stream.WriteLine("# Tree of {0}:", String.Join("/", path));

			FdbDirectorySubspace root = null;
			if (path.Length > 0) root = await db.Directory.TryOpenAsync(path, cancellationToken: ct);

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

		private static string FormatSize(long size)
		{
			if (size < 10000) return size.ToString("N0");
			double x = size / 1024.0;
			if (x < 800) return x.ToString("N1") + " kB";
			x /= 1024.0;
			if (x < 800) return x.ToString("N2") + " MB";
			x /= 1024.0;
			return x.ToString("N2") + " GB";
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
				span = FdbKeyRange.Create(FdbKey.MinValue, FdbKey.MaxValue);
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
			log.WriteLine("{0,9}{1,10}{2,10}{3,10} : K+V size distribution", "Count", "Keys", "Values", "Total");

			var rangeOptions = new FdbRangeOptions { Mode = FdbStreamingMode.WantAll };

			samples = samples.OrderBy(x => x.Begin).ToList();

			long total = 0;
			int workers = Math.Max(4, Environment.ProcessorCount);

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
						var hh = new RobustHistogram(RobustHistogram.TimeScale.Ticks);

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

									hh.Add(TimeSpan.FromTicks(kvp.Key.Count + kvp.Value.Count));
								}

								if (!data.HasMore) break;

								beginSelector = FdbKeySelector.FirstGreaterThan(data.Last.Key);
								++iter;
							}

							long totalSize = keySize + valueSize;
							Interlocked.Add(ref total, totalSize);

							lock (log)
							{
								log.WriteLine("{0,9}{1,10}{2,10}{3,10} : {4}", count.ToString("N0"), FormatSize(keySize), FormatSize(valueSize), FormatSize(totalSize), hh.GetDistribution(begin: 1, end: 10000, fold: 2));
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
