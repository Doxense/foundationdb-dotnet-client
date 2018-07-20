
namespace FdbShell
{
	using System;
	using System.Collections.Generic;
	using System.Diagnostics;
	using System.Globalization;
	using System.IO;
	using System.Linq;
	using System.Net;
	using System.Text;
	using System.Threading;
	using System.Threading.Tasks;
	using Doxense.Collections.Tuples;
	using FoundationDB.Client;
	using FoundationDB.Layers.Directories;

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
				return await db.Directory.TryOpenAsync(path, ct: ct);
			}
			else
			{
				return db.Directory;
			}
		}

		public static async Task Dir(string[] path, IVarTuple extras, DirectoryBrowseOptions options, IFdbDatabase db, TextWriter log, CancellationToken ct)
		{
			if (log == null) log = Console.Out;

			Program.Comment(log, $"# Listing /{string.Join("/", path)}:");

			var parent = await TryOpenCurrentDirectoryAsync(path, db, ct);
			if (parent == null)
			{
				Program.Error(log, "Directory not found.");
				return;
			}

			if (parent.Layer.IsPresent)
			{
				log.WriteLine($"# Layer: {parent.Layer:P}");
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
								long count = await Fdb.System.EstimateCountAsync(db, subfolder.Keys.ToRange(), ct);
								Program.StdOut(log, $"  {FdbKey.Dump(subfolder.Copy().GetPrefix()),-12} {(subfolder.Layer.IsNullOrEmpty ? "-" : ("<" + subfolder.Layer.ToUnicode() + ">")),-12} {count,9:N0} {name}", ConsoleColor.White);
							}
							else
							{
								Program.StdOut(log, $"  {FdbKey.Dump(subfolder.Copy().GetPrefix()),-12} {(subfolder.Layer.IsNullOrEmpty ? "-" : ("<" + subfolder.Layer.ToUnicode() + ">")),-12} {"-",9} {name}", ConsoleColor.White);
							}
						}
						else
						{
							Program.StdOut(log, $"  {FdbKey.Dump(subfolder.Copy().GetPrefix()),-12} {(subfolder.Layer.IsNullOrEmpty ? "-" : ("<" + subfolder.Layer.ToUnicode() + ">")),-12} {name}", ConsoleColor.White);
						}
					}
					else
					{
						Program.Error(log, $"  WARNING: {name} seems to be missing!");
					}
				}
				log.WriteLine($"  {folders.Count} sub-directorie(s).");
			}
			else
			{
				//TODO: test if it contains data?
				log.WriteLine("No sub-directories.");
			}

			//TODO: check if there is at least one key?
		}

		/// <summary>Creates a new directory</summary>
		public static async Task CreateDirectory(string[] path, IVarTuple extras, IFdbDatabase db, TextWriter log, CancellationToken ct)
		{
			if (log == null) log = Console.Out;

			string layer = extras.Count > 0 ? extras.Get<string>(0) : null;

			log.WriteLine($"# Creating directory {String.Join("/", path)} with layer '{layer}'");

			var folder = await db.Directory.TryOpenAsync(path, ct: ct);
			if (folder != null)
			{
				log.WriteLine($"- Directory {string.Join("/", path)} already exists!");
				return;
			}

			folder = await db.Directory.TryCreateAsync(path, Slice.FromString(layer), ct: ct);
			log.WriteLine($"- Created under {FdbKey.Dump(folder.GetPrefix())} [{folder.GetPrefix().ToHexaString(' ')}]");

			// look if there is already stuff under there
			var stuff = await db.ReadAsync((tr) => tr.GetRange(folder.Keys.ToRange()).FirstOrDefaultAsync(), ct: ct);
			if (stuff.Key.IsPresent)
			{
				Program.Error(log, $"CAUTION: There is already some data under {string.Join("/", path)} !");
				Program.Error(log, $"  {FdbKey.Dump(stuff.Key)} = {stuff.Value:V}");
			}
		}

		/// <summary>Remove a directory and all its data</summary>
		public static async Task RemoveDirectory(string[] path, IVarTuple extras, IFdbDatabase db, TextWriter log, CancellationToken ct)
		{
			if (log == null) log = Console.Out;

			string layer = extras.Count > 0 ? extras.Get<string>(0) : null;

			var folder = await db.Directory.TryOpenAsync(path, ct: ct);
			if (folder == null)
			{
				Program.Error(log, $"# Directory /{string.Join("/", path)} does not exist");
				return;
			}

			// are there any subdirectories ?
			var subDirs = await folder.TryListAsync(db, ct);
			if (subDirs != null && subDirs.Count > 0)
			{
				//TODO: "-r" flag ?
				Program.Error(log, $"# Cannot remove /{string.Join("/", path)} because it still contains {subDirs.Count:N0} sub-directorie(s)");
			}

			//TODO: ask for confirmation?

			await folder.RemoveAsync(db, ct);
			Program.Success(log, $"Deleted directory /{string.Join("/", path)}");
		}

		/// <summary>Move/Rename a directory</summary>
		public static async Task MoveDirectory(string[] srcPath, string[] dstPath, IVarTuple extras, IFdbDatabase db, TextWriter log, CancellationToken ct)
		{
			var folder = await db.Directory.TryOpenAsync(srcPath, ct: ct);
			if (folder == null)
			{
				Program.Error(log, $"# Source directory /{string.Join("/", srcPath)} does not exist!");
				return;
			}

			folder = await db.Directory.TryOpenAsync(dstPath, ct: ct);
			if (folder != null)
			{
				Program.Error(log, $"# Destination directory /{string.Join("/", dstPath)} already exists!");
				return;
			}

			await db.Directory.MoveAsync(srcPath, dstPath, ct);
			Program.Success(log, $"Moved /{string.Join("/", srcPath)} to {string.Join("/", dstPath)}");
		}

		public static async Task ShowDirectoryLayer(string[] path, IVarTuple extras, IFdbDatabase db, TextWriter log, CancellationToken ct)
		{
			var dir = await BasicCommands.TryOpenCurrentDirectoryAsync(path, db, ct);
			if (dir == null)
			{
				Program.Error(log, $"# Directory {String.Join("/", path)} does not exist anymore");
			}
			else
			{
				if (dir.Layer == FdbDirectoryPartition.LayerId)
					log.WriteLine($"# Directory {String.Join("/", path)} is a partition");
				else if (dir.Layer.IsPresent)
					log.WriteLine($"# Directory {String.Join("/", path)} has layer {dir.Layer:P}");
				else
					log.WriteLine($"# Directory {String.Join("/", path)} does not have a layer defined");
			}
		}

		public static async Task ChangeDirectoryLayer(string[] path, string layer, IVarTuple extras, IFdbDatabase db, TextWriter log, CancellationToken ct)
		{
			var dir = await BasicCommands.TryOpenCurrentDirectoryAsync(path, db, ct);
			if (dir == null)
			{
				Program.Error(log, $"# Directory {String.Join("/", path)} does not exist anymore");
			}
			else
			{
				dir = await db.ReadWriteAsync((tr) => dir.ChangeLayerAsync(tr, Slice.FromString(layer)), ct);
				Program.Success(log, $"# Directory {String.Join("/", path)} layer changed to {dir.Layer:P}");
			}
		}

		public static async Task Get(string[] path, IVarTuple extras, IFdbDatabase db, TextWriter log, CancellationToken ct)
		{

			if (path == null || path.Length == 0)
			{
				Program.Error(log, "Cannot read keys of the Root Partition.");
				return;
			}

			if (extras.Count == 0)
			{
				Program.Error(log, "You must specify a key of range of keys!");
				return;
			}

			var folder = await db.Directory.TryOpenAsync(path, ct: ct);
			if (folder == null)
			{
				Program.Error(log, "The directory does not exist anymore");
				return;
			}
			if (folder.Layer == FdbDirectoryPartition.LayerId)
			{
				Program.Error(log, "Cannot clear the content of a Directory Partition!");
				return;
			}

			object key = extras[0];
			Slice k = MakeKey(folder, key);

			Program.Comment(log, "# Reading key: " + k.ToString("K"));

			Slice v = await db.ReadWriteAsync(tr =>tr.GetAsync(k), ct);


			if (v.IsNull)
			{
				Program.StdOut(log, "# Key does not exist in the database.", ConsoleColor.Red);
				return;
			}
			if (v.IsEmpty)
			{
				Program.StdOut(log, "# Key exists but is empty.", ConsoleColor.Gray);
				return;
			}

			Program.StdOut(log, $"# Size: {v.Count:N0}", ConsoleColor.Gray);
			string format = extras.Count > 1 ? extras.Get<string>(1) : null;
			switch (format)
			{
				case "--text":
				case "--json":
				case "--utf8":
				{
					Program.StdOut(log, v.ToStringUtf8(), ConsoleColor.Gray);
					break;
				}
				case "--hex":
				case "--hexa":
				{
					Program.StdOut(log, v.ToHexaString(), ConsoleColor.White);
					break;
				}
				case "--dump":
				{
					var sb = new StringBuilder(v.Count * 3 + (v.Count >> 4) * 2 + 16);
					for (int i = 0; i < v.Count; i += 16)
					{
						sb.AppendLine(v.Substring(i, 16).ToHexaString(' '));
					}
					Program.StdOut(log, sb.ToString(), ConsoleColor.White);
					break;
				}
				case "--int":
				{
					if (v.Count <= 8)
					{
						long he = v.ToInt64BE();
						long le = v.ToInt64();
						Program.StdOut(log, $"BE: {he:X016} ({he:N0})", ConsoleColor.White);
						Program.StdOut(log, $"LE: {le:X016} ({le:N0})", ConsoleColor.White);
					}
					else
					{
						Program.StdOut(log, $"Value is too large ({v.Count} bytes)", ConsoleColor.DarkRed);
						Program.StdOut(log, v.ToHexaString(' '), ConsoleColor.Gray);
					}
					break;
				}
				case "--uint":
				{
					if (v.Count <= 8)
					{
						ulong he = v.ToUInt64BE();
						ulong le = v.ToUInt64();
						Program.StdOut(log, $"BE: {he:X016} ({he:N0})", ConsoleColor.White);
						Program.StdOut(log, $"LE: {le:X016} ({le:N0})", ConsoleColor.White);
					}
					else
					{
						Program.StdOut(log, $"Value is too large ({v.Count} bytes)", ConsoleColor.DarkRed);
						Program.StdOut(log, v.ToHexaString(' '), ConsoleColor.Gray);
					}
					break;
				}
				case "--tuple":
				{
					try
					{
						var t = TuPack.Unpack(v);
						Program.StdOut(log, t.ToString(), ConsoleColor.Gray);
					}
					catch (Exception e)
					{
						Program.Error(log, "Key value does not seem to be a valid Tuple: " + e.Message);
						Program.StdOut(log, v.ToHexaString(' '), ConsoleColor.Gray);
					}
					break;
				}
				default:
				{
					Program.StdOut(log, v.ToString("V"), ConsoleColor.White);
					break;
				}
			}
		}

		public static async Task Clear(string[] path, IVarTuple extras, IFdbDatabase db, TextWriter log, CancellationToken ct)
		{

			if (path == null || path.Length == 0)
			{
				Program.Error(log, "Cannot directory list the content of Root Partition.");
				return;
			}

			var folder = await db.Directory.TryOpenAsync(path, ct: ct);
			if (folder == null)
			{
				Program.Error(log, "The directory does not exist anymore");
				return;
			}
			if (folder.Layer == FdbDirectoryPartition.LayerId)
			{
				Program.Error(log, "Cannot clear the content of a Directory Partition!");
				return;
			}

			if (extras.Count == 0)
			{
				Program.Error(log, "You must specify a key of range of keys!");
				return;
			}

			object key = extras[0];
			Slice k = MakeKey(folder, key);

			bool empty = await db.ReadWriteAsync(async tr =>
			{
				var v = await tr.GetAsync(k);
				if (v.IsNullOrEmpty) return true;
				tr.Clear(k);
				return false;
			}, ct);

			if (empty)
			{
				Program.StdOut(log, "Key did not exist in the database.", ConsoleColor.Cyan);
			}
			else
			{
				Program.Success(log, $"Key {key} has been cleared from Directory {String.Join("/", path)}.");
			}
		}

		private static Slice MakeKey(IKeySubspace folder, object key)
		{
			if (key is IVarTuple t)
			{
				return folder[TuPack.Pack(t)];
			}
			else if (key is string s)
			{
				return folder[Slice.FromStringUtf8(s)];
			}
			else
			{
				throw new FormatException("Unsupported key type: " + key);
			}
		}

		public static async Task ClearRange(string[] path, IVarTuple extras, IFdbDatabase db, TextWriter log, CancellationToken ct)
		{

			if (path == null || path.Length == 0)
			{
				Program.Error(log, "Cannot directory list the content of Root Partition.");
				return;
			}

			var folder = await db.Directory.TryOpenAsync(path, ct: ct);
			if (folder == null)
			{
				Program.Error(log, "The directory does not exist anymore");
				return;
			}
			if (folder.Layer == FdbDirectoryPartition.LayerId)
			{
				Program.Error(log, "Cannot clear the content of a Directory Partition!");
				return;
			}

			if (extras.Count == 0)
			{
				Program.Error(log, "You must specify a key of range of keys!");
				return;
			}

			KeyRange range;
			if (extras.Get<string>(0) == "*")
			{ // clear all!
				range = folder.ToRange();
			}
			else
			{
				object from = extras[0];
				object to = extras.Count > 1 ? extras[1] : null;

				if (to == null)
				{
					range = KeyRange.StartsWith(MakeKey(folder, from));
				}
				else
				{
					range = new KeyRange(
						MakeKey(folder, from),
						MakeKey(folder, to)
					);
				}
			}

			Program.Comment(log, "Clearing range " + range.ToString());

			bool empty = await db.ReadWriteAsync(async tr =>
			{
				var any = await tr.GetRangeAsync(range, new FdbRangeOptions { Limit = 1 });
				if (any.Count == 0) return true;
				tr.ClearRange(folder.ToRange());
				return false;
			}, ct);
			if (empty)
			{
				Program.StdOut(log, $"Directory {String.Join("/", path)} was already empty.", ConsoleColor.Cyan);
			}
			else
			{
				Program.Success(log, $"Cleared all content of Directory {String.Join("/", path)}.");
			}
		}

		/// <summary>Counts the number of keys inside a directory</summary>
		public static async Task Count(string[] path, IVarTuple extras, IFdbDatabase db, TextWriter log, CancellationToken ct)
		{
			// look if there is something under there
			var folder = (await TryOpenCurrentDirectoryAsync(path, db, ct)) as FdbDirectorySubspace;
			if (folder == null)
			{
				Program.Error(log, $"# Directory {String.Join("/", path)} does not exist");
				return;
			}

			var copy = folder.Copy();
			log.WriteLine($"# Counting keys under {FdbKey.Dump(copy.GetPrefix())} ...");

			var progress = new Progress<(long Count, Slice Current)>((state) =>
			{
				log.Write($"\r# Found {state.Count:N0} keys...");
			});

			long count = await Fdb.System.EstimateCountAsync(db, copy.ToRange(), progress, ct);
			log.Write("\r");
			Program.Success(log, $"Found {count:N0} keys in {folder.FullName}");
		}

		/// <summary>Shows the first few keys of a directory</summary>
		public static async Task Show(string[] path, IVarTuple extras, bool reverse, IFdbDatabase db, TextWriter log, CancellationToken ct)
		{
			int count = 20;
			if (extras.Count > 0)
			{
				int x = extras.Get<int>(0);
				if (x > 0) count = x;
			}

			if (path == null || path.Length == 0)
			{
				Program.Error(log, "Cannot directory list the content of Root Partition.");
				return;
			}

			// look if there is something under there
			var folder = await db.Directory.TryOpenAsync(path, ct: ct);
			if (folder != null)
			{
				if (folder.Layer == FdbDirectoryPartition.LayerId)
				{
					Program.Error(log, "Cannot list the content of a Directory Partition!");
					return;
				}
				Program.Comment(log, $"# Content of {FdbKey.Dump(folder.GetPrefix())} [{folder.GetPrefix().ToHexaString(' ')}]");

				var keys = await db.QueryAsync((tr) =>
				{
					var query = tr.GetRange(folder.Keys.ToRange());
					return reverse
						? query.Reverse().Take(count)
						: query.Take(count + 1);
				}, ct: ct);
				if (keys.Count > 0)
				{
					if (reverse) keys.Reverse();
					foreach (var key in keys.Take(count))
					{
						log.WriteLine($"...{FdbKey.Dump(folder.ExtractKey(key.Key))} = {key.Value:V}");
					}
					if (!reverse && keys.Count == count + 1)
					{
						log.WriteLine("... more");
					}
				}
				else
				{
					log.WriteLine("Folder is empty");
				}
			}
		}

		public static async Task Dump(string[] path, string output, IVarTuple extras, IFdbDatabase db, TextWriter log, CancellationToken ct)
		{
			// look if there is something under there
			var folder = await db.Directory.TryOpenAsync(path, ct: ct);
			if (folder == null)
			{
				Program.Comment(log, $"# Directory {String.Join("/", path)} does not exist");
				return;
			}

			//TODO: if file already exists, ask confirmation before overriding?
			output = Path.GetFullPath(output);
			File.Delete(output);

			using (var fs = new FileStream(output, FileMode.Create, FileAccess.Write, FileShare.Read, 64 * 1024))
			using (var sw = new StreamWriter(fs, Encoding.UTF8, 32 * 1024))
			{
				Program.Comment(log, $"# Dumping content of {FdbKey.Dump(folder.GetPrefix())} [{folder.GetPrefix().ToHexaString(' ')}] to {output}");
				long bytes = 0;
				var kr = new[] { '|', '/', '-', '\\' };
				int p = 0;
				var count = await Fdb.Bulk.ExportAsync(
					db,
					folder.ToRange(),
					(batch, offset, _ct) =>
					{
						if (log == Console.Out) log.Write($"\r{kr[(p++) % kr.Length]} {bytes:N0} bytes");
						foreach (var kv in batch)
						{
							bytes += kv.Key.Count + kv.Value.Count;
							sw.WriteLine("{0} = {1:V}", FdbKey.Dump(folder.ExtractKey(kv.Key)), kv.Value);
						}
						ct.ThrowIfCancellationRequested();
						return sw.FlushAsync();
					},
					ct
				);
				if (log == Console.Out) log.Write("\r");
				if (count > 0)
				{
					Program.Success(log, $"Found {count:N0} keys ({bytes:N0} bytes)");
				}
				else
				{
					Program.StdOut(log, "Folder is empty.");
				}
			}
		}

		/// <summary>Display a tree of a directory's children</summary>
		public static async Task Tree(string[] path, IVarTuple extras, IFdbDatabase db, TextWriter log, CancellationToken ct)
		{
			log = log ?? Console.Out;

			Program.Comment(log, $"# Tree of {String.Join("/", path)}:");

			FdbDirectorySubspace root = null;
			if (path.Length > 0) root = await db.Directory.TryOpenAsync(path, ct: ct);

			await TreeDirectoryWalk(root, new List<bool>(), db, log, ct);

			Program.Comment(log, "# done");
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
				stream.WriteLine($"{sb}{(folder.Layer.ToString() == "partition" ? ("<" + folder.Name + ">") : folder.Name)}{(folder.Layer.IsNullOrEmpty ? string.Empty : (" [" + folder.Layer.ToString() + "]"))}");
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

		public static async Task Map(string[] path, IVarTuple extras, IFdbDatabase db, TextWriter log, CancellationToken ct)
		{
			// we want to merge the map of shards, with the map of directories from the Directory Layer, and count for each directory how many shards intersect
			bool progress = log == Console.Out;

			var folder = await TryOpenCurrentDirectoryAsync(path, db, ct);
			if (folder == null)
			{
				Program.Error(log, "# Directory not found");
				return;
			}

			Program.StdOut(log, "Listing all shards...");

			// note: this may break in future versions of the DL! Maybe we need a custom API to get a flat list of all directories in a DL that span a specific range ?
			var span = folder.DirectoryLayer.ContentSubspace.Keys.ToRange();
			var shards = await Fdb.System.GetChunksAsync(db, span, ct);
			int totalShards = shards.Count;
			Program.StdOut(log, $"> Found {totalShards} shard(s) in partition /{folder.DirectoryLayer.FullName}", ConsoleColor.Gray);

			Program.StdOut(log, "Listing all directories...");
			var map = new Dictionary<string, int>(StringComparer.Ordinal);

			void Account(string[] p, int c)
			{
				for (int i = 1; i <= p.Length; i++)
				{
					var s = "/" + string.Join("/", p, 0, i);
					map[s] = map.TryGetValue(s, out int x) ? (x + c) : c;
				}
			}

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
						var p = sub.FullName;
						if (sub is FdbDirectoryPartition)
						{
							if (progress) log.Write("\r");
							Program.StdOut(log ,$"! Skipping partition {sub.Name}     ", ConsoleColor.DarkRed);
							n = 0;
							continue;
						}
						if (progress) log.Write($"\r/{p}{(p.Length > n ? String.Empty : new string(' ', n - p.Length))}");
						n = p.Length;
						work.Push(sub);
						dirs.Add(sub);
					}
				}
			}
			if (progress) log.Write("\r" + new string(' ', n + 2) + "\r");
			Program.StdOut(log, $"> Found {dirs.Count} sub-directories", ConsoleColor.Gray);

			log.WriteLine();
			Program.StdOut(log, "Estimating size of each directory...");
			int foundShards = 0;
			n = 0;
			int max = 0;
			IFdbDirectory bigBad = null;
			foreach (var dir in dirs)
			{
				if (progress) log.Write($"\r> {dir.Name}{(dir.Name.Length > n ? String.Empty : new string(' ', n - dir.Name.Length))}");
				n = dir.Name.Length;

				var p = dir.Path.ToArray();
				var key = ((KeySubspace)dir).GetPrefix();

				// verify that the subspace has at least one key inside
				var bounds = await db.ReadAsync(async (tr) =>
				{
					var kvs = await Task.WhenAll(
						tr.GetRange(KeyRange.StartsWith(key)).FirstOrDefaultAsync(),
						tr.GetRange(KeyRange.StartsWith(key)).LastOrDefaultAsync()
					);
					return new { Min = kvs[0].Key, Max = kvs[1].Key };
				}, ct);

				if (bounds.Min.HasValue)
				{ // folder is not empty
					shards = await Fdb.System.GetChunksAsync(db, KeyRange.StartsWith(key), ct);
					//TODO: we still need to check if the first and last shard really intersect the subspace

					// we need to check if the shards actually contain data
					//Console.WriteLine("/{0} under {1} with {2} shard(s)", string.Join("/", p), FdbKey.Dump(key), shards.Count);
					foundShards += shards.Count;
					Account(p, shards.Count);
					if (shards.Count > max) { max = shards.Count; bigBad = dir; }
				}
				else
				{
					Account(p, 0);
				}
			}
			if (progress) log.Write("\r" + new string(' ', n + 2) + "\r");
			Program.StdOut(log, $"> Found a total of {foundShards:N0} shard(s) in {dirs.Count:N0} folder(s)", ConsoleColor.Gray);
			log.WriteLine();

			Program.StdOut(log ,"Shards %Total              Path");
			foreach(var kvp in map.OrderBy(x => x.Key))
			{
				Program.StdOut(log, $"{kvp.Value,6} {RobustHistogram.FormatHistoBar((double)kvp.Value / foundShards, 20),-20} {kvp.Key}", ConsoleColor.Gray);
			}
			log.WriteLine();

			if (bigBad != null)
			{
				Program.StdOut(log, $"Biggest folder is /{bigBad.FullName} with {max} shards ({100.0 * max / totalShards:N1}% total, {100.0 * max / foundShards:N1}% subtree)");
				log.WriteLine();
			}
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

		/// <summary>Find the DCs, machines and processes in the cluster</summary>
		public static async Task Topology(string[] path, IVarTuple extras, IFdbDatabase db, TextWriter log, CancellationToken ct)
		{
			var coords = await Fdb.System.GetCoordinatorsAsync(db, ct);
			log.WriteLine($"[Cluster] {coords.Id}");

			var servers = await db.QueryAsync(tr => tr
				.WithReadAccessToSystemKeys()
				.GetRange(KeyRange.StartsWith(Fdb.System.ServerList))
				.Select(kvp => new
				{
					// Offsets		Size	Type	Name		Description
					//    0			 2		Word	Version?	0100 (1.0 ?)
					//    2			 4		DWord	???			0x00 0x20 0xA2 0x00
					//    6			 2		Word	FDBMagic	0xDB 0x0F "FDB"
					//    8			16		Guid	NodeId		Unique Process ID
					//   24			16		Guid	Machine		"machine_id" field in foundationdb.conf (ends with 8x0 if manually specified)
					//   40			16		Guid	DataCenter	"datacenter_id" field in foundationdb.conf (ends with 8x0 if manually specified)
					//   56			 4		???		??			4 x 0
					//   60			12 x24	ARRAY[] ??			array of 12x the same 24-byte struct defined below

					// ...0			 4		DWord	IPAddress	01 00 00 7F => 127.0.0.1
					// ...4			 4		DWord	Port		94 11 00 00 -> 4500
					// ...8			 4		DWord	??			randomish, changes every reboot
					// ..12			 4		DWord	??			randomish, changes every reboot
					// ..16			 4		DWord	Size?		small L-E integer, usually between 0x20 and 0x40...
					// ..20			 4		DWord	??			randmoish, changes every reboot

					ProcessId = kvp.Value.Substring(8, 16).ToHexaString(),
					MachineId = kvp.Value.Substring(24, 16).ToHexaString(),
					DataCenterId = kvp.Value.Substring(40, 16).ToHexaString(),

					Parts = Enumerable.Range(0, 12).Select(i =>
					{
						int p = 60 + 24 * i;
						return new
						{
							Address = new IPAddress(kvp.Value.Substring(p, 4).GetBytes().Reverse().ToArray()),
							Port = kvp.Value.Substring(p + 4, 4).ToInt32(),
							Unknown1 = kvp.Value.Substring(p + 8, 4).ToInt32(),
							Unknown2 = kvp.Value.Substring(p + 12, 4).ToInt32(),
							Unknown3 = kvp.Value.Substring(p + 16, 4).ToInt32(),
							Unknown4 = kvp.Value.Substring(p + 20, 4).ToInt32(),
						};
					}).ToList(),
					Raw = kvp.Value,
				}),
				ct
			);

			var numNodes = servers.Select(s => s.ProcessId).Distinct().Count();
			var numMachines = servers.Select(s => s.MachineId).Distinct().Count();
			var numDCs = servers.Select(s => s.DataCenterId).Distinct().Count();

			var dcs = servers.GroupBy(x => x.DataCenterId).ToArray();
			for (int dcIndex = 0; dcIndex < dcs.Length;dcIndex++)
			{
				var dc = dcs[dcIndex];
				bool lastDc = dcIndex == dcs.Length - 1;

				string dcId = dc.Key.EndsWith("0000000000000000") ? dc.Key.Substring(0, 16) : dc.Key;
				log.WriteLine((lastDc ? "`- " : "|- ") + "[DataCenter] {0} (#{1})", dcId, dcIndex);

				var machines = dc.GroupBy(x => x.MachineId).ToArray();
				string dcPrefix = lastDc ? "   " : "|  ";
				for (int machineIndex = 0; machineIndex < machines.Length; machineIndex++)
				{
					var machine = machines[machineIndex];
					var lastMachine = machineIndex == machines.Length - 1;

					string machineId = machine.Key.EndsWith("0000000000000000") ? machine.Key.Substring(0, 16) : machine.Key;
					log.WriteLine(dcPrefix + (lastMachine ? "`- " : "|- ") + "[Machine] {0}, {1}", machine.First().Parts[0].Address, machineId);

					var procs = machine.ToArray();
					string machinePrefix = dcPrefix + (lastMachine ? "   " : "|  ");
					for (int procIndex = 0; procIndex < procs.Length; procIndex++)
					{
						var proc = procs[procIndex];
						bool lastProc = procIndex == procs.Length - 1;

						log.WriteLine(machinePrefix + (lastProc ? "`- " : "|- ") + "[Process] {0}:{1}, {2}", proc.Parts[0].Address, proc.Parts[0].Port, proc.ProcessId);
						//foreach (var part in proc.Parts)
						//{
						//	log.WriteLine(machinePrefix + "|  -> {0}, {1}, {2:X8}, {3:X8}, {4}, {5:X8}", part.Address, part.Port, part.Unknown1, part.Unknown2, part.Unknown3, part.Unknown4);
						//}
					}
				}
			}
			log.WriteLine();
			Program.StdOut(log, $"Found {numNodes:N0} process(es) on {numMachines:N0} machine(s) in {numDCs:N0} datacenter(s)", ConsoleColor.White);
			log.WriteLine();
		}

		public static async Task Shards(string[] path, IVarTuple extras, IFdbDatabase db, TextWriter log, CancellationToken ct)
		{
			var ranges = await Fdb.System.GetChunksAsync(db, FdbKey.MinValue, FdbKey.MaxValue, ct);
			Console.WriteLine($"Found {ranges.Count} shards in the whole cluster");

			// look if there is something under there
			if ((await TryOpenCurrentDirectoryAsync(path, db, ct)) is FdbDirectorySubspace folder)
			{
				var r = KeyRange.StartsWith(folder.Copy().GetPrefix());
				Console.WriteLine($"Searching for shards that intersect with /{String.Join("/", path)} ...");
				ranges = await Fdb.System.GetChunksAsync(db, r, ct);
				Console.WriteLine($"> Found {ranges.Count} ranges intersecting {r}:");
				var last = Slice.Empty;
				foreach (var range in ranges)
				{
					Console.Write($"> {FdbKey.Dump(range.Begin)} ...");
					long count = await Fdb.System.EstimateCountAsync(db, range, ct);
					Console.WriteLine($" {count:N0}");
					last = range.End;
					//TODO: we can probably get more details on this shard looking in the system keyspace (where it is, how many replicas, ...)
				}
				Console.WriteLine($"> ... {FdbKey.Dump(last)}");
			}

			//Console.WriteLine("Found " + ranges.Count + " shards in the cluster");
			//TODO: shards that intersect the current directory
		}

		public static async Task Sampling(string[] path, IVarTuple extras, IFdbDatabase db, TextWriter log, CancellationToken ct)
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
			KeyRange span;
			if (folder is FdbDirectorySubspace)
			{
				span = KeyRange.StartsWith((folder as FdbDirectorySubspace).Copy().GetPrefix());
				log.WriteLine($"Reading list of shards for /{String.Join("/", path)} under {FdbKey.Dump(span.Begin)} ...");
			}
			else
			{
				log.WriteLine("Reading list of shards for the whole cluster ...");
				span = KeyRange.All;
			}

			// dump keyServers
			var ranges = await Fdb.System.GetChunksAsync(db, span, ct);
			log.WriteLine($"> Found {ranges.Count:N0} shard(s)");

			// take a sample
			var samples = new List<KeyRange>();

			if (ranges.Count <= 32)
			{ // small enough to scan it all
				samples.AddRange(ranges);
				log.WriteLine($"Sampling all {samples.Count:N0} shards ...");
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

				var population = new List<KeyRange>(ranges);
				for (int i = 0; i < sz; i++)
				{
					int p = rnd.Next(population.Count);
					samples.Add(population[p]);
					population.RemoveAt(p);
				}
				log.WriteLine($"Sampling {samples.Count:N0} out of {ranges.Count:N0} shards ({(100.0 * samples.Count / ranges.Count):N1}%) ...");
			}

			log.WriteLine();
			const string FORMAT_STRING = "{0,9} ║{1,10}{6,6} {2,-29} ║{3,10}{7,7} {4,-37} ║{5,10}";
			const string SCALE_KEY = "....--------========########M";
			const string SCALE_VAL = "....--------========########@@@@@@@@M";
			log.WriteLine(FORMAT_STRING, "Count", "Keys", SCALE_KEY, "Values", SCALE_VAL, "Total", "med.", "med.");

			var rangeOptions = new FdbRangeOptions { Mode = FdbStreamingMode.WantAll };

			samples = samples.OrderBy(x => x.Begin).ToList();

			long globalSize = 0;
			long globalCount = 0;
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
							var beginSelector = KeySelector.FirstGreaterOrEqual(range.Begin);
							var endSelector = KeySelector.FirstGreaterOrEqual(range.End);
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
								foreach (var kvp in data)
								{
									keySize += kvp.Key.Count;
									valueSize += kvp.Value.Count;

									kk.Add(TimeSpan.FromTicks(kvp.Key.Count));
									vv.Add(TimeSpan.FromTicks(kvp.Value.Count));
								}

								if (!data.HasMore) break;

								beginSelector = KeySelector.FirstGreaterThan(data.Last);
								++iter;
							}

							long totalSize = keySize + valueSize;
							Interlocked.Add(ref globalSize, totalSize);
							Interlocked.Add(ref globalCount, count);

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
				log.WriteLine($"Sampled {FormatSize(globalSize)} ({globalSize:N0} bytes) and {globalCount:N0} keys in {sw.Elapsed.TotalSeconds:N1} sec");
				log.WriteLine($"> Estimated total size is {FormatSize(globalSize * ranges.Count / n)}");
			}
			else
			{
				log.WriteLine($"Found {FormatSize(globalSize)} ({globalSize:N0} bytes) and {globalCount:N0} keys in {sw.Elapsed.TotalSeconds:N1} sec");
				// compare to the whole cluster
				ranges = await Fdb.System.GetChunksAsync(db, FdbKey.MinValue, FdbKey.MaxValue, ct);
				log.WriteLine($"> This directory contains ~{(100.0 * n / ranges.Count):N2}% of all data");
			}
			log.WriteLine();
		}

	}
}
