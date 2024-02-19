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

// ReSharper disable MethodHasAsyncOverload
// ReSharper disable MethodSupportsCancellation

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

	public static class BasicCommands
	{

		[Flags]
		public enum DirectoryBrowseOptions
		{
			Default = 0,
			ShowFirstKeys = 1,
			ShowCount = 2,
		}

		public static async Task<IFdbDirectory?> TryOpenCurrentDirectoryAsync(IFdbReadOnlyTransaction tr, FdbPath path)
		{
			var location = new FdbDirectorySubspaceLocation(path);
			if (location.Path.Count != 0)
			{
				return await location.Resolve(tr);
			}
			else
			{
				return location;
			}
		}

		public static async Task Dir(FdbPath path, IVarTuple extras, DirectoryBrowseOptions options, IFdbDatabase db, TextWriter? log, CancellationToken ct)
		{
			log ??= Console.Out;

			Program.Comment(log, $"# Listing {path}:");

			await db.ReadAsync(async tr =>
			{
				var parent = await TryOpenCurrentDirectoryAsync(tr, path);
				if (parent == null)
				{
					Program.Error(log, "Directory not found.");
					return;
				}

				if (!string.IsNullOrEmpty(parent.Layer))
				{
					log.WriteLine($"# Layer: {parent.Layer}");
				}

				var folders = await Fdb.Directory.BrowseAsync(tr, parent);
				if (folders.Count > 0)
				{
					// to better align the names, we allow between 16 to 40 chars for the first column.
					// if there is a larger name, it will stick out!
					var maxLen = Math.Min(Math.Max(folders.Keys.Max(n => n.Length), 16), 40);

					foreach (var kvp in folders)
					{
						var name = kvp.Key;
						var subfolder = kvp.Value;
						if (subfolder != null!)
						{
							if ((options & DirectoryBrowseOptions.ShowCount) != 0)
							{
								if (subfolder is not FdbDirectoryPartition)
								{
									long count = await Fdb.System.EstimateCountAsync(db, subfolder.ToRange(), ct);
									Program.StdOut(log, $"  {name.PadRight(maxLen)} {FdbKey.Dump(subfolder.Copy().GetPrefix()),-12} {(string.IsNullOrEmpty(subfolder.Layer) ? "-" : ("[" + subfolder.Layer + "]")),-12} {count,9:N0}", ConsoleColor.White);
								}
								else
								{
									Program.StdOut(log, $"  {name.PadRight(maxLen)} {FdbKey.Dump(subfolder.Copy().GetPrefix()),-12} {(string.IsNullOrEmpty(subfolder.Layer) ? "-" : ("[" + subfolder.Layer + "]")),-12} {"-",9}", ConsoleColor.White);
								}
							}
							else
							{
								Program.StdOut(log, $"  {name.PadRight(maxLen)} {FdbKey.Dump(subfolder.Copy().GetPrefix()),-12} {(string.IsNullOrEmpty(subfolder.Layer) ? "-" : ("[" + subfolder.Layer + "]")),-12}", ConsoleColor.White);
							}
						}
						else
						{
							Program.Error(log, $"  WARNING: {name} seems to be missing!");
						}
					}

					log.WriteLine($"  {folders.Count} sub-directories.");
				}
				else
				{
					//TODO: test if it contains data?
					log.WriteLine("No sub-directories.");
				}

				//TODO: check if there is at least one key?
			}, ct);
		}

		/// <summary>Creates a new directory</summary>
		public static async Task CreateDirectory(FdbPath path, IVarTuple extras, IFdbDatabase db, TextWriter? log, CancellationToken ct)
		{
			log ??= Console.Out;

			string layer = (extras.Count > 0 ? extras.Get<string>(0)?.Trim() : null) ??string.Empty;

			if (path.LayerId != layer) path.WithLayer(layer);
			log.WriteLine($"# Creating directory {path}");

			var (prefix, created) = await db.ReadWriteAsync(async tr =>
			{
				var folder = await db.DirectoryLayer.TryOpenAsync(tr, path);
				if (folder != null)
				{
					return (folder.GetPrefix(), false);
				}

				folder = await db.DirectoryLayer.TryCreateAsync(tr, path);
				return (folder!.Copy().GetPrefix(), true);
			}, ct);

			if (!created)
			{
				log.WriteLine($"- Directory {path} already exists at {FdbKey.Dump(prefix)} [{prefix.ToHexaString(' ')}]");
				return;
			}
			log.WriteLine($"- Created under {FdbKey.Dump(prefix)} [{prefix.ToHexaString(' ')}]");

			// look if there is already stuff under there
			var stuff = await db.ReadAsync(async tr =>
			{
				var folder = await db.DirectoryLayer.TryOpenAsync(tr, path);
				return await tr.GetRange(folder!.ToRange()).FirstOrDefaultAsync();
			}, ct);

			if (stuff.Key.IsPresent)
			{
				Program.Error(log, $"CAUTION: There is already some data under {path} !");
				Program.Error(log, $"  {FdbKey.Dump(stuff.Key)} = {stuff.Value:V}");
			}
		}

		/// <summary>Remove a directory and all its data</summary>
		public static async Task RemoveDirectory(FdbPath path, IVarTuple extras, IFdbDatabase db, TextWriter? log, CancellationToken ct)
		{
			log ??= Console.Out;

			// "-r|--recursive" is used to allow removing an entire sub-tree
			string[] args = extras.ToArray<string>();
			bool recursive = args.Contains("-r", StringComparer.Ordinal) || args.Contains("--recursive", StringComparer.Ordinal);
			bool force = args.Contains("-f", StringComparer.Ordinal) || args.Contains("--force", StringComparer.Ordinal);

			var res = await db.ReadWriteAsync(async tr =>
			{
				var folder = await db.DirectoryLayer.TryOpenAsync(tr, path);
				if (folder == null)
				{
					Program.Error(log, $"# Directory {path} does not exist!");
					return false;
				}

				// are there any subdirectories ?
				if (!recursive)
				{
					var subDirs = await folder.TryListAsync(tr);
					if (subDirs != null && subDirs.Count > 0)
					{
						//TODO: "-r" flag ?
						Program.Error(log, $"# Cannot remove {path} because it still contains {subDirs.Count:N0} sub-directories.");
						Program.StdOut(log, "Use the -r|--recursive flag to override this warning.");
						return false;
					}
				}

				if (!force)
				{
					//TODO: ask for confirmation?
				}

				await folder.RemoveAsync(tr);
				return true;
			}, ct);
			if (res)
			{
				Program.Success(log, $"Deleted directory {path}");
			}
		}

		/// <summary>Move/Rename a directory</summary>
		public static async Task MoveDirectory(FdbPath source, FdbPath destination, IVarTuple extras, IFdbDatabase db, TextWriter log, CancellationToken ct)
		{
			await db.WriteAsync(async tr =>
			{
				var folder = await db.DirectoryLayer.TryOpenAsync(tr, source);
				if (folder == null)
				{
					Program.Error(log, $"# Source directory {source} does not exist!");
					return;
				}

				folder = await db.DirectoryLayer.TryOpenAsync(tr, destination);
				if (folder != null)
				{
					Program.Error(log, $"# Destination directory {destination} already exists!");
					return;
				}

				await db.DirectoryLayer.MoveAsync(tr, source, destination);
			}, ct);
			Program.Success(log, $"Moved {source} to {destination}");
		}

		public static async Task ShowDirectoryLayer(FdbPath path, IVarTuple extras, IFdbDatabase db, TextWriter log, CancellationToken ct)
		{
			var dir = await db.ReadAsync(tr => TryOpenCurrentDirectoryAsync(tr, path), ct);
			if (dir == null)
			{
				Program.Error(log, $"# Directory {path} does not exist anymore");
			}
			else
			{
				if (dir.Layer == FdbDirectoryPartition.LayerId)
					log.WriteLine($"# Directory {path} is a partition");
				else if (!string.IsNullOrEmpty(dir.Layer))
					log.WriteLine($"# Directory {path} has layer '{dir.Layer}'");
				else
					log.WriteLine($"# Directory {path} does not have a layer defined");
			}
		}

		public static Task ChangeDirectoryLayer(FdbPath path, string layer, IVarTuple extras, IFdbDatabase db, TextWriter log, CancellationToken ct)
		{
			return db.WriteAsync(async tr =>
			{
				var dir = await BasicCommands.TryOpenCurrentDirectoryAsync(tr, path);
				if (dir == null)
				{
					Program.Error(log, $"# Directory {path} does not exist anymore");
				}
				else
				{
					dir = await dir.ChangeLayerAsync(tr, layer);
					Program.Success(log, $"# Directory {path} layer changed to {dir.Layer}");
				}
			}, ct);
		}

		public static async Task Get(FdbPath path, IVarTuple extras, IFdbDatabase db, TextWriter log, CancellationToken ct)
		{

			if (path.IsRoot)
			{
				Program.Error(log, "Cannot read keys of the Root Partition.");
				return;
			}

			if (extras.Count == 0)
			{
				Program.Error(log, "You must specify a key of range of keys!");
				return;
			}

			(var v, bool pathNotFound) = await db.ReadAsync(async tr =>
			{

				var folder = await db.DirectoryLayer.TryOpenAsync(tr, path);
				if (folder == null)
				{
					Program.Error(log, "The directory does not exist anymore");
					return (default, false);
				}
				if (folder.Layer == FdbDirectoryPartition.LayerId)
				{
					Program.Error(log, "Cannot clear the content of a Directory Partition!");
					return (default, false);
				}

				object? key = extras[0];
				var k = MakeKey(folder, key);

				Program.Comment(log, "# Reading key: " + k.ToString("K"));

				return (await tr.GetAsync(k), true);
			}, ct);

			if (pathNotFound) return;

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
			string? format = extras.Count > 1 ? extras.Get<string>(1) : null;
			switch (format)
			{
				case "--text":
				case "--json":
				case "--utf8":
				{
					Program.StdOut(log, v.ToStringUtf8() ?? string.Empty, ConsoleColor.Gray);
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
						Program.StdOut(log, t.ToString() ?? string.Empty, ConsoleColor.Gray);
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

		public static async Task Clear(FdbPath path, IVarTuple extras, IFdbDatabase db, TextWriter log, CancellationToken ct)
		{
			if (path.Count == 0)
			{
				Program.Error(log, "Cannot directory list the content of Root Partition.");
				return;
			}

			if (extras.Count == 0)
			{
				Program.Error(log, "You must specify a key of range of keys!");
				return;
			}
			object? key = extras[0];

			var empty = await db.ReadWriteAsync(async tr =>
			{

				var folder = await db.DirectoryLayer.TryOpenAsync(tr, path);
				if (folder == null)
				{
					Program.Error(log, "The directory does not exist anymore");
					return default(bool?);
				}
				if (folder.Layer == FdbDirectoryPartition.LayerId)
				{
					Program.Error(log, "Cannot clear the content of a Directory Partition!");
					return default(bool?);
				}

				var k = MakeKey(folder, key);

				var v = await tr.GetAsync(k);
				if (v.IsNullOrEmpty)
				{
					return true;
				}

				tr.Clear(k);
				return false;
			}, ct);

			if (empty == null)
			{
				return;
			}

			if (empty.Value)
			{
				Program.StdOut(log, "Key did not exist in the database.", ConsoleColor.Cyan);
			}
			else
			{
				Program.Success(log, $"Key {key} has been cleared from Directory {path}.");
			}
		}

		private static Slice MakeKey(IKeySubspace folder, object? key) =>
			key switch
			{
				IVarTuple t => folder.Append(TuPack.Pack(t)),
				string s => folder.Append(Slice.FromStringUtf8(s)),
				_ => throw new FormatException("Unsupported key type: " + key)
			};

		public static async Task ClearRange(FdbPath path, IVarTuple extras, IFdbDatabase db, TextWriter log, CancellationToken ct)
		{
			if (path.Count == 0)
			{
				Program.Error(log, "Cannot directory list the content of Root Partition.");
				return;
			}

			bool? empty = await db.ReadWriteAsync(async tr =>
			{
				var folder = await db.DirectoryLayer.TryOpenAsync(tr, path);
				if (folder == null)
				{
					Program.Error(log, "The directory does not exist anymore");
					return default(bool?);
				}
				if (folder.Layer == FdbDirectoryPartition.LayerId)
				{
					Program.Error(log, "Cannot clear the content of a Directory Partition!");
					return default(bool?);
				}

				if (extras.Count == 0)
				{
					Program.Error(log, "You must specify a key of range of keys!");
					return default(bool?);
				}

				KeyRange range;
				if (extras[0] is "*")
				{ // clear all!
					range = folder.ToRange();
				}
				else
				{
					object? from = extras[0];
					object? to = extras.Count > 1 ? extras[1] : null;

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

				var any = await tr.GetRangeAsync(range, new FdbRangeOptions { Limit = 1 });
				if (any.Count == 0) return true;
				tr.ClearRange(folder.ToRange());
				return false;
			}, ct);

			if (empty == null) return;

			if (empty.Value)
			{
				Program.StdOut(log, $"Directory {path} was already empty.", ConsoleColor.Cyan);
			}
			else
			{
				Program.Success(log, $"Cleared all content of Directory {path}.");
			}
		}

		/// <summary>Counts the number of keys inside a directory</summary>
		public static async Task Count(FdbPath path, IVarTuple extras, IFdbDatabase db, TextWriter log, CancellationToken ct)
		{
			// look if there is something under there
			var folder = (await db.ReadWriteAsync(tr => TryOpenCurrentDirectoryAsync(tr, path), ct)) as FdbDirectorySubspace;
			if (folder == null)
			{
				Program.Error(log, $"# Directory {path} does not exist");
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
		public static async Task Show(FdbPath path, IVarTuple extras, bool reverse, IFdbDatabase db, TextWriter log, CancellationToken ct)
		{
			int count = 20;
			if (extras.Count > 0)
			{
				int x = extras.Get<int>(0);
				if (x > 0) count = x;
			}

			if (path.Count == 0)
			{
				Program.Error(log, "Cannot directory list the content of Root Partition.");
				return;
			}

			// look if there is something under there
			await db.ReadAsync(async tr =>
			{
				var folder = await db.DirectoryLayer.TryOpenAsync(tr, path);
				if (folder == null)
				{
					log.WriteLine("Folder does not exist");
					return;
				}

				if (folder.Layer == FdbDirectoryPartition.LayerId)
				{
					Program.Error(log, "Cannot list the content of a Directory Partition!");
					return;
				}
				Program.Comment(log, $"# Content of {FdbKey.Dump(folder.GetPrefix())} [{folder.GetPrefix().ToHexaString(' ')}]");

				var query = tr.GetRange(folder.ToRange());
				var keys = await (reverse
					? query.Reverse().Take(count)
					: query.Take(count + 1)
				).ToListAsync();

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
			}, ct);
		}

		public static async Task Dump(FdbPath path, string output, IVarTuple extras, IFdbDatabase db, TextWriter log, CancellationToken ct)
		{
			// look if there is something under there
			var folder = await db.ReadWriteAsync(async tr =>
			{
				var dir = await db.DirectoryLayer.TryOpenAsync(tr, path);
				return dir != null ? KeySubspace.CopyUnsafe(dir) : null;
			}, ct);

			if (folder == null)
			{
				Program.Comment(log, $"# Directory {path} does not exist");
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

				//BUGBUG: this is dangerous if the directory is moved/renamed/removed WHILE the bulk read is going!
				// => we should find a way to either detect the move, or fail the export if this happens!

				var count = await Fdb.Bulk.ExportAsync(
					db,
					folder.ToRange(),
					(batch, _, _) =>
					{
						if (log == Console.Out) log.Write($"\r{kr[(p++) % kr.Length]} {bytes:N0} bytes");
						foreach (var kv in batch)
						{
							bytes += kv.Key.Count + kv.Value.Count;
							sw.WriteLine("{0} = {1:V}", FdbKey.Dump(folder.ExtractKey(kv.Key)), kv.Value);
						}
						ct.ThrowIfCancellationRequested();
						return sw.FlushAsync(ct);
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
		public static async Task Tree(FdbPath path, IVarTuple extras, IFdbDatabase db, TextWriter? log, CancellationToken ct)
		{
			log ??= Console.Out;

			Program.Comment(log, $"# Tree of {path}:");

			FdbDirectorySubspace? root = null;
			if (path.Count != 0)
			{
				root = await db.ReadAsync(tr => db.DirectoryLayer.TryOpenAsync(tr, path), ct);
				if (root == null)
				{
					log.WriteLine("Folder not found.");
					return;
				}
			}

			await TreeDirectoryWalk(root?.Path ?? FdbPath.Root, new List<bool>(), db, log, ct);

			Program.Comment(log, "# done");
		}

		private static async Task TreeDirectoryWalk(FdbPath folder, List<bool> last, IFdbDatabase db, TextWriter stream, CancellationToken ct)
		{
			ct.ThrowIfCancellationRequested();

			var sb = new StringBuilder(last.Count * 4);
			if (last.Count > 0)
			{
				for (int i = 0; i < last.Count - 1; i++) sb.Append(last[i] ? "    " : "|   ");
				sb.Append(last[^1] ? "`-- " : "|-- ");
			}

			if (folder == FdbPath.Root)
			{
				sb.Append("<root>");
			}
			else
			{
				sb.Append($"{(folder.LayerId.ToString() == "partition" ? ("<" + folder.Name + ">") : folder.Name)}{(string.IsNullOrEmpty(folder.LayerId) ? string.Empty : (" [" + folder.LayerId + "]"))}");
			}
			stream.WriteLine(sb.ToString());

			var children = await db.ReadAsync(tr => db.DirectoryLayer.ListAsync(tr, folder), ct);
			int n = children.Count;
			foreach (var child in children)
			{
				last.Add((n--) == 1);
				await TreeDirectoryWalk(child, last, db, stream, ct);
				last.RemoveAt(last.Count - 1);
			}
		}

		public static async Task Map(FdbPath path, IVarTuple extras, IFdbDatabase db, TextWriter log, CancellationToken ct)
		{
			// we want to merge the map of shards, with the map of directories from the Directory Layer, and count for each directory how many shards intersect
			bool progress = log == Console.Out;

			var folder = await db.ReadAsync(tr => TryOpenCurrentDirectoryAsync(tr, path), ct);
			if (folder == null)
			{
				Program.Error(log, "# Directory not found");
				return;
			}

			Program.StdOut(log, "Listing all shards...");

			var dirLayer = path.Count > 0 ? folder.DirectoryLayer : db.DirectoryLayer;
			// note: this may break in future versions of the DL! Maybe we need a custom API to get a flat list of all directories in a DL that span a specific range ?
			var span = await db.ReadAsync(async tr => (await dirLayer.Content.Resolve(tr))!.ToRange(), ct);

			var shards = await Fdb.System.GetChunksAsync(db, span, ct);
			int totalShards = shards.Count;
			Program.StdOut(log, $"> Found {totalShards} shard(s) in partition {dirLayer.FullName}", ConsoleColor.Gray);

			Program.StdOut(log, "Listing all directories...");
			var map = new Dictionary<string, int>(StringComparer.Ordinal);

			void Account(FdbPath p, int c)
			{
				for (int i = 1; i <= p.Count; i++)
				{
					var s = FdbPath.Absolute(p.Segments.Slice(0, i)).ToString();
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

				var names = await db.ReadAsync(tr => cur.ListAsync(tr), ct);
				foreach(var name in names)
				{
					var sub = await db.ReadAsync(tr => cur.TryOpenAsync(tr, name), ct);
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
						if (progress) log.Write($"\r{p}{(p.Length > n ? string.Empty : new string(' ', n - p.Length))}");
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
			IFdbDirectory? bigBad = null;
			foreach (var dir in dirs)
			{
				if (progress) log.Write($"\r> {dir.Name}{(dir.Name.Length > n ? String.Empty : new string(' ', n - dir.Name.Length))}");
				n = dir.Name.Length;

				var p = dir.Path;
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
				Program.StdOut(log, $"Biggest folder is {bigBad.FullName} with {max} shards ({100.0 * max / totalShards:N1}% total, {100.0 * max / foundShards:N1}% subtree)");
				log.WriteLine();
			}
		}

		private static string FormatSize(long size, IFormatProvider? ci = null)
		{
			ci ??= CultureInfo.InvariantCulture;
			if (size < 2048) return size.ToString("N0", ci);
			double x = size / 1024.0;
			if (x < 800) return x.ToString("N1", ci) + " k";
			x /= 1024.0;
			if (x < 800) return x.ToString("N2", ci) + " M";
			x /= 1024.0;
			return x.ToString("N2", ci) + " G";
		}

		/// <summary>Find the DCs, machines and processes in the cluster</summary>
		public static async Task Topology(FdbDirectorySubspaceLocation? location, IVarTuple extras, IFdbDatabase db, TextWriter log, CancellationToken ct)
		{
			var coords = await Fdb.System.GetCoordinatorsAsync(db, ct);
			log.WriteLine($"[Cluster] {coords.Id}");

			var servers = await db.QueryAsync(tr => tr
				.WithOptions(options => options.WithReadAccessToSystemKeys())
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
							Address = new IPAddress(kvp.Value.Substring(p, 4).GetBytesOrEmpty().Reverse().ToArray()),
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

		public static async Task Shards(FdbPath path, IVarTuple extras, IFdbDatabase db, TextWriter log, CancellationToken ct)
		{
			var ranges = await Fdb.System.GetChunksAsync(db, FdbKey.MinValue, FdbKey.MaxValue, ct);
			Console.WriteLine($"Found {ranges.Count} shards in the whole cluster");

			// look if there is something under there
			if ((await db.ReadAsync(tr => TryOpenCurrentDirectoryAsync(tr, path), ct)) is FdbDirectorySubspace folder)
			{
				var r = KeyRange.StartsWith(folder.Copy().GetPrefix());
				Console.WriteLine($"Searching for shards that intersect with {path} ...");
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

		public static async Task Sampling(FdbPath path, IVarTuple extras, IFdbDatabase db, TextWriter log, CancellationToken ct)
		{
			double ratio = 0.1d;
			bool auto = true;
			if (extras.Count > 0)
			{
				double x = extras.Get<double>(0);
				if (x > 0 && x <= 1) ratio = x;
				auto = false;
			}

			var folder = await db.ReadAsync(tr => TryOpenCurrentDirectoryAsync(tr, path), ct);
			KeyRange span;
			if (folder is FdbDirectorySubspace subspace)
			{
				span = KeyRange.StartsWith(subspace.Copy().GetPrefix());
				log.WriteLine($"Reading list of shards for {path} under {FdbKey.Dump(span.Begin)} ...");
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
								FdbRangeChunk? data = null;
								FdbException? error = null;
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

								if (data == null || data.Count == 0) break;

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
