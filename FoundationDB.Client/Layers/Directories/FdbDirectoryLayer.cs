#region BSD License
/* Copyright (c) 2013-2020, Doxense SAS
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

namespace FoundationDB.Client
{
	using System;
	using System.Collections.Generic;
	using System.Diagnostics;
	using System.Linq;
	using System.Runtime.CompilerServices;
	using System.Threading;
	using System.Threading.Tasks;
	using JetBrains.Annotations;
	using Doxense.Collections.Tuples;
	using Doxense.Diagnostics.Contracts;
	using Doxense.Memory;
	using FoundationDB.Filters.Logging;
	using FoundationDB.Layers.Allocators;

	/// <summary>Provides a FdbDirectoryLayer class for managing directories in FoundationDB.
	/// Directories are a recommended approach for administering layers and applications. Directories work in conjunction with subspaces. Each layer or application should create or open at least one directory with which to manage its subspace(s).
	/// Directories are identified by paths (specified as tuples) analogous to the paths in a Unix-like file system. Each directory has an associated subspace that is used to store content. The layer uses a high-contention allocator to efficiently map each path to a short prefix for its corresponding subspace.
	/// <see cref="FdbDirectoryLayer"/> exposes methods to create, open, move, remove, or list directories. Creating or opening a directory returns the corresponding subspace.
	/// The <see cref="FdbDirectorySubspace"/> class represents subspaces that store the contents of a directory. An instance of <see cref="FdbDirectorySubspace"/> can be used for all the usual subspace operations. It can also be used to operate on the directory with which it was opened.
	/// </summary>
	[DebuggerDisplay("Content={Content}")]
	[PublicAPI]
	public class FdbDirectoryLayer : IFdbDirectory, IFdbLayer<FdbDirectoryLayer.State>
	{
		private const int SUBDIRS = 0;

		// Internal structure:
		// - All keys used by the Directory Layer are encoded using the Tuple Encoding
		// - The "Nodes" subspace is located under the 0xFE subspace relative to the Content subspace (by default)
		// - The 'version' and 'metadata' attributes of the partition are located under Nodes + Pack(Nodes, '...')
		// - All the metadata relative to directory with prefix X are located under Nodes + Pack(X, ...)
		//   - The layer of directory at prefix X is located at Key = Nodes + Pack(X, 'layer') = Empty or layer id
		//   - The sub-directories of directory at prefix X are located under Key = Nodes + Pack(X, 0, SUBDIR_NAME), Value = Prefix of subdirectory
		//   - For the root of the directory partition, X = the prefix of the nodes subspace

		internal static readonly Version LayerVersion = new Version(1, 1, 0);
		internal static readonly Slice LayerAttribute = Slice.FromStringAscii("layer");
		internal static readonly Slice HcaAttribute = Slice.FromStringAscii("hca");
		internal static readonly Slice VersionAttribute = Slice.FromStringAscii("version");
		internal static readonly Slice MetadataAttribute = Slice.FromStringAscii("metadata");

		/// <summary>Use this flag to make the Directory Layer start annotating the transactions with a descriptions of all operations.</summary>
		/// <remarks>
		/// This is only useful if you want to diagnose performance or read conflict issues.
		/// This will only work with logged transactions, obtained by applying the Logging Filter on a database instance
		/// </remarks>
		public static bool AnnotateTransactions { get; set; }

		/// <summary>Subspace where the content of each folder will be stored</summary>
		public DynamicKeySubspaceLocation Content { get; }

		/// <summary>Random generator used by the internal allocators</summary>
		internal Random AllocatorRng { get; }

		/// <summary>Most current cache context</summary>
		internal CacheContext? Cache;

		/// <summary>Name of root directory of this layer</summary>
		/// <remarks>Returns String.Empty for the root Directory Layer, or the name of the partition</remarks>
		public string Name => this.Path.Name;

		/// <summary>Formatted path of the root directory of this layer</summary>
		public string FullName => this.Path.ToString();

		/// <summary>Gets the path for the root node of this <code>FdbDirectoryLayer</code></summary>
		/// <remarks>Normally constructed <code>DirectoryLayer</code>s have an empty path, but <code>DirectoryLayer</code>s returned by <see cref="IFdbDirectory.DirectoryLayer"/> for <see cref="IFdbDirectory"/>s inside of a <see cref="FdbDirectoryPartition"/> could have non-empty paths.</remarks>
		public FdbPath Path => FdbPath.Root;

		FdbDirectorySubspaceLocation IFdbDirectory.Location => new FdbDirectorySubspaceLocation(this.Path);

		/// <summary>Returns the layer id for this <code>FdbDirectoryLayer</code>, which is always <see cref="string.Empty"/>.</summary>
		string IFdbDirectory.Layer => string.Empty;

		/// <summary>Self reference</summary>
		FdbDirectoryLayer IFdbDirectory.DirectoryLayer => this;

		/// <summary>Convert a relative path in this Directory Layer, into an absolute path from the root of partition of the database</summary>
		internal FdbPath PartitionSubPath(FdbPath path = default)
		{
			// If the DL is the root, the path is already absolute
			// If the DL is used by a partition, then the path of the partition will be prepended to the path
			return path.Count != 0 ? this.Path.Add(path) : this.Path;
		}

		void IFdbDirectory.CheckLayer(string? layer)
		{
			if (!string.IsNullOrEmpty(layer))
			{
				throw ThrowHelper.InvalidOperationException($"The directory layer {this.FullName} is not compatible with layer {layer}.");
			}
		}

		Task<FdbDirectorySubspace> IFdbDirectory.ChangeLayerAsync(IFdbTransaction trans, string newLayer)
		{
			throw ThrowHelper.NotSupportedException("You cannot change the layer of a Directory Layer.");
		}

		#region Constructors...

		/// <summary>Creates a new instance that will manages directories in FoundationDB.</summary>
		/// <param name="location">Location of the root of all the directories managed by this Directory Layer. Usually empty for the root partition of the database.</param>
		internal FdbDirectoryLayer(DynamicKeySubspaceLocation location)
		{
			Contract.Requires(location != null);

			this.Content = location;
			this.AllocatorRng = new Random();
		}

		/// <summary>Create an instance of a Directory Layer located under a specific subspace and path</summary>
		/// <param name="location">Location of the Directory Layer's content. The nodes will be stored under the &lt;FE&gt; subspace</param>
		public static FdbDirectoryLayer Create(ISubspaceLocation location)
		{
			Contract.NotNull(location, nameof(location));

			return new FdbDirectoryLayer(location.AsDynamic());
		}

		#endregion

		#region Public Methods...

		#region CreateOrOpen / Open / Create

		/// <summary>Opens the directory with the given path. If the directory does not exist, it is created (creating parent directories if necessary).</summary>
		/// <param name="trans">Transaction to use for the operation</param>
		/// <param name="path">Path of the directory to create or open</param>
		public async Task<FdbDirectorySubspace> CreateOrOpenAsync(IFdbTransaction trans, FdbPath path)
		{
			Contract.NotNull(trans, nameof(trans));

			var location = VerifyPath(path);

			var metadata = await Resolve(trans);
			return (await metadata.CreateOrOpenInternalAsync(null, trans, location, Slice.Nil, allowCreate: true, allowOpen: true, throwOnError: true))!;
		}

		/// <summary>Opens the directory with the given <paramref name="path"/>.
		/// An exception is thrown if the directory does not exist, or if a layer is specified and a different layer was specified when the directory was created.
		/// </summary>
		/// <param name="trans">Transaction to use for the operation</param>
		/// <param name="path">Path of the directory to open.</param>
		public async Task<FdbDirectorySubspace> OpenAsync(IFdbReadOnlyTransaction trans, FdbPath path)
		{
			Contract.NotNull(trans, nameof(trans));

			var location = VerifyPath(path);

			var metadata = await Resolve(trans);
			return (await metadata.CreateOrOpenInternalAsync(trans, null, location, prefix: Slice.Nil, allowCreate: false, allowOpen: true, throwOnError: true))!;
		}

		/// <summary>Creates a directory with the given <paramref name="path"/> (creating parent directories if necessary).
		/// An exception is thrown if the given directory already exists.
		/// </summary>
		/// <param name="trans">Transaction to use for the operation</param>
		/// <param name="path">Path of the directory to create</param>
		public async Task<FdbDirectorySubspace> CreateAsync(IFdbTransaction trans, FdbPath path)
		{
			Contract.NotNull(trans, nameof(trans));

			var location = VerifyPath(path);

			var metadata = await Resolve(trans);
			return (await metadata.CreateOrOpenInternalAsync(null, trans, location, prefix: Slice.Nil, allowCreate: true, allowOpen: false, throwOnError: true))!;
		}

		/// <summary>Attempts to open the directory with the given <paramref name="path"/>.</summary>
		/// <param name="trans">Transaction to use for the operation</param>
		/// <param name="path">Path of the directory to open.</param>
		/// <param name="layer">Optional layer id of the directory. If it is different than the layer specified when creating the directory, an exception will be thrown.</param>
		public async Task<FdbDirectorySubspace?> TryOpenAsync(IFdbReadOnlyTransaction trans, FdbPath path)
		{
			Contract.NotNull(trans, nameof(trans));

			var location = VerifyPath(path);

			var metadata = await Resolve(trans);
			return (await metadata.CreateOrOpenInternalAsync(trans, null, location, prefix: Slice.Nil, allowCreate: false, allowOpen: true, throwOnError: false))!;
		}

		public async ValueTask<FdbDirectorySubspace?> TryOpenCachedAsync(IFdbReadOnlyTransaction trans, FdbPath path)
		{
			Contract.NotNull(trans, nameof(trans));

			var metadata = await Resolve(trans);
			return await metadata.OpenCachedInternalAsync(trans, path, throwOnError: false);
		}

		public async ValueTask<FdbDirectorySubspace?[]> TryOpenCachedAsync(IFdbReadOnlyTransaction trans, IEnumerable<FdbPath> paths)
		{
			Contract.NotNull(trans, nameof(trans));

			var metadata = await Resolve(trans);
			return await metadata.OpenCachedInternalAsync(trans, (paths as FdbPath[]) ?? paths.ToArray(), throwOnError: false);
		}

		/// <summary>Attempts to create a directory with the given <paramref name="path"/> (creating parent directories if necessary).</summary>
		/// <param name="trans">Transaction to use for the operation</param>
		/// <param name="path">Path of the directory to create</param>
		public async Task<FdbDirectorySubspace?> TryCreateAsync(IFdbTransaction trans, FdbPath path)
		{
			Contract.NotNull(trans, nameof(trans));

			var location = VerifyPath(path);

			var metadata = await Resolve(trans);
			return await metadata.CreateOrOpenInternalAsync(null, trans, location, prefix: Slice.Nil, allowCreate: true, allowOpen: false, throwOnError: false);
		}

		/// <summary>Registers an existing prefix as a directory with the given <paramref name="path"/> (creating parent directories if necessary). This method is only indented for advanced use cases.</summary>
		/// <param name="trans">Transaction to use for the operation</param>
		/// <param name="path">Path of the directory to create</param>
		/// <param name="layer">If <paramref name="layer"/> is specified, it is recorded with the directory and will be checked by future calls to open.</param>
		/// <param name="prefix">The directory will be created with the given physical prefix; otherwise a prefix is allocated automatically.</param>
		public async Task<FdbDirectorySubspace> RegisterAsync(IFdbTransaction trans, FdbPath path, Slice prefix)
		{
			Contract.NotNull(trans, nameof(trans));

			var location = VerifyPath(path);

			var metadata = await Resolve(trans);
			return (await metadata.CreateOrOpenInternalAsync(null, trans, location, prefix: prefix, allowCreate: true, allowOpen: false, throwOnError: true))!;
		}

		/// <summary>Attempts to register an existing prefix as a directory with the given <paramref name="path"/> (creating parent directories if necessary). This method is only indented for advanced use cases.</summary>
		/// <param name="trans">Transaction to use for the operation</param>
		/// <param name="path">Path of the directory to create</param>
		/// <param name="layer">If <paramref name="layer"/> is specified, it is recorded with the directory and will be checked by future calls to open.</param>
		/// <param name="prefix">The directory will be created with the given physical prefix; otherwise a prefix is allocated automatically.</param>
		public async Task<FdbDirectorySubspace?> TryRegisterAsync(IFdbTransaction trans, FdbPath path, Slice prefix)
		{
			Contract.NotNull(trans, nameof(trans));

			var location = VerifyPath(path);

			var metadata = await Resolve(trans);
			return await metadata.CreateOrOpenInternalAsync(null, trans, location, prefix: prefix, allowCreate: true, allowOpen: false, throwOnError: false);
		}

		#endregion

		#region Move / TryMove

		/// <summary>Moves the directory found at <paramref name="oldPath"/> to <paramref name="newPath"/>.
		/// There is no effect on the physical prefix of the given directory, or on clients that already have the directory open.
		/// An error is raised if the old directory does not exist, a directory already exists at `new_path`, or the parent directory of `new_path` does not exist.
		/// </summary>
		/// <param name="trans">Transaction to use for the operation</param>
		/// <param name="oldPath">Path of the directory to move</param>
		/// <param name="newPath">New path of the directory</param>
		public async Task<FdbDirectorySubspace> MoveAsync(IFdbTransaction trans, FdbPath oldPath, FdbPath newPath)
		{
			Contract.NotNull(trans, nameof(trans));

			EnsureAbsolutePath(in oldPath);
			EnsureAbsolutePath(in newPath);

			var oldLocation = VerifyPath(oldPath, nameof(oldPath));
			var newLocation = VerifyPath(newPath, nameof(newPath));

			if (oldLocation.IsRoot) throw new InvalidOperationException("The root directory cannot be moved.");
			if (newLocation.IsRoot) throw new InvalidOperationException("The root directory cannot be replaced.");

			var metadata = await Resolve(trans);
			return (await metadata.MoveInternalAsync(trans, oldLocation, newLocation, throwOnError: true))!;
		}

		/// <summary>Attempts to move the directory found at <paramref name="oldPath"/> to <paramref name="newPath"/>.
		/// There is no effect on the physical prefix of the given directory, or on clients that already have the directory open.
		/// Returns null if the old directory does not exist, a directory already exists at `new_path`, or the parent directory of `new_path` does not exist.
		/// </summary>
		/// <param name="trans">Transaction to use for the operation</param>
		/// <param name="oldPath">Path of the directory to move</param>
		/// <param name="newPath">New path of the directory</param>
		public async Task<FdbDirectorySubspace?> TryMoveAsync(IFdbTransaction trans, FdbPath oldPath, FdbPath newPath)
		{
			Contract.NotNull(trans, nameof(trans));

			var oldLocation = VerifyPath(oldPath, nameof(oldPath));
			var newLocation = VerifyPath(newPath, nameof(newPath));

			if (oldLocation.IsRoot) throw new InvalidOperationException("The root directory cannot be moved.");
			if (newLocation.IsRoot) throw new InvalidOperationException("The root directory cannot be replaced.");

			var metadata = await Resolve(trans);
			return await metadata.MoveInternalAsync(trans, oldLocation, newLocation, throwOnError: false);
		}

		#endregion

		#region MoveTo / TryMoveTo

		Task<FdbDirectorySubspace> IFdbDirectory.MoveToAsync(IFdbTransaction trans, FdbPath newAbsolutePath)
		{
			throw new InvalidOperationException("The root directory cannot be moved.");
		}

		Task<FdbDirectorySubspace?> IFdbDirectory.TryMoveToAsync(IFdbTransaction trans, FdbPath newAbsolutePath)
		{
			throw new InvalidOperationException("The root directory cannot be moved.");
		}

		#endregion

		#region Remove / TryRemove

		/// <summary>Removes the directory, its contents, and all subdirectories.
		/// Warning: Clients that have already opened the directory might still insert data into its contents after it is removed.
		/// </summary>
		/// <param name="trans">Transaction to use for the operation</param>
		/// <param name="path">Path of the directory to remove (including any subdirectories)</param>
		public async Task RemoveAsync(IFdbTransaction trans, FdbPath path)
		{
			Contract.NotNull(trans, nameof(trans));

			var location = VerifyPath(path);
			if (location.IsRoot) throw new InvalidOperationException("Cannot remove a directory layer");

			var metadata = await Resolve(trans);
			await metadata.RemoveInternalAsync(trans, location, throwIfMissing: true);
		}

		/// <summary>Attempts to remove the directory, its contents, and all subdirectories.
		/// Warning: Clients that have already opened the directory might still insert data into its contents after it is removed.
		/// </summary>
		/// <param name="trans">Transaction to use for the operation</param>
		/// <param name="path">Path of the directory to remove (including any subdirectories)</param>
		public async Task<bool> TryRemoveAsync(IFdbTransaction trans, FdbPath path)
		{
			Contract.NotNull(trans, nameof(trans));

			var location = VerifyPath(path);
			if (location.IsRoot) return false; // cannot remove directory layer itself

			var metadata = await Resolve(trans);
			return await metadata.RemoveInternalAsync(trans, location, throwIfMissing: false);
		}

		#endregion

		#region Exists

		/// <summary>Checks if a directory already exists</summary>
		/// <param name="trans">Transaction to use for the operation</param>
		/// <param name="path">Path of the directory to remove (including any subdirectories)</param>
		/// <returns>Returns true if the directory exists, otherwise false.</returns>
		public async Task<bool> ExistsAsync(IFdbReadOnlyTransaction trans, FdbPath path)
		{
			Contract.NotNull(trans, nameof(trans));
			// no reason to disallow checking for the root directory (could be used to check if a directory layer is initialized?)

			var location = VerifyPath(path);
			if (location.Count == 0) return true;

			var metadata = await Resolve(trans);
			return await metadata.ExistsInternalAsync(trans, location);
		}

		#endregion

		#region List / TryList

		/// <summary>Returns the list of subdirectories of directory at <paramref name="path"/></summary>
		/// <param name="trans">Transaction to use for the operation</param>
		/// <param name="path">Path of the directory to list</param>
		public async Task<List<FdbPath>> ListAsync(IFdbReadOnlyTransaction trans, FdbPath path = default)
		{
			Contract.NotNull(trans, nameof(trans));

			var location = VerifyPath(path);

			var metadata = await Resolve(trans);
			return (await metadata.ListInternalAsync(trans, location, throwIfMissing: true))!;
		}

		/// <summary>Returns the list of subdirectories of directory at <paramref name="path"/>, if it exists.</summary>
		/// <param name="trans">Transaction to use for the operation</param>
		/// <param name="path">Path of the directory to list</param>
		public async Task<List<FdbPath>?> TryListAsync(IFdbReadOnlyTransaction trans, FdbPath path = default)
		{
			Contract.NotNull(trans, nameof(trans));

			var location = VerifyPath(path);

			var metadata = await Resolve(trans).ConfigureAwait(false);
			return await metadata.ListInternalAsync(trans, location, throwIfMissing: false).ConfigureAwait(false);
		}

		#endregion

		/// <summary>Change the layer id of the directory at <paramref name="path"/></summary>
		/// <param name="trans">Transaction to use for the operation</param>
		/// <param name="path">Path of the directory to change</param>
		/// <param name="newLayer">New layer id of the directory</param>
		public async Task<FdbDirectorySubspace> ChangeLayerAsync(IFdbTransaction trans, FdbPath path, string newLayer)
		{
			Contract.NotNull(trans, nameof(trans));
			Contract.NotNull(newLayer, nameof(newLayer));
			var location = VerifyPath(path);

			var metadata = await Resolve(trans);

			// Set the layer to the new value
			await metadata.ChangeLayerInternalAsync(trans, location, newLayer).ConfigureAwait(false);
			var newPath = path.WithLayer(newLayer);

			// And re-open the directory subspace
			return (await metadata.CreateOrOpenInternalAsync(null, trans, newPath, prefix: Slice.Nil, allowCreate: false, allowOpen: true, throwOnError: true).ConfigureAwait(false))!;
		}

		public override string ToString()
		{
			return $"DirectoryLayer({this.Content})";
		}

		#endregion

		#region Internal Helpers...

		[DebuggerDisplay("Path={Path}, Prefix={Prefix}, Layer={Layer}")]
		internal readonly struct Node
		{

			public Node(FdbPath path, Slice prefix, string? layer, PartitionDescriptor partition, PartitionDescriptor parentPartition, Slice prefixInParentPartition)
			{
				Contract.Requires(partition != null && parentPartition != null);
				this.Prefix = prefix;
				this.Path = path;
				this.Layer = layer;
				this.Partition = partition;
				this.ParentPartition = parentPartition;
				this.PrefixInParentPartition = prefixInParentPartition;
			}

			public readonly Slice Prefix;
			public readonly FdbPath Path;
			public readonly string? Layer;
			public readonly PartitionDescriptor Partition;
			public readonly PartitionDescriptor ParentPartition;
			public readonly Slice PrefixInParentPartition;

			public bool Exists => !this.Prefix.IsNull;

		}

		public ValueTask<State> Resolve(IFdbReadOnlyTransaction tr)
		{
			//note: we use the directory layer itself has the "token" key for the local data cache
			if (tr.Context.TryGetLocalData(this, out State? metadata))
			{
				return new ValueTask<State>(metadata);
			}
			return ResolveMetadata(tr);
		}

		private async ValueTask<State> ResolveMetadata(IFdbReadOnlyTransaction tr)
		{
			var rv = await tr.GetReadVersionAsync();
			var content = await this.Content.Resolve(tr);
			if (content == null) throw new InvalidOperationException("Directory Layer content subspace was not found");

			var partition = new PartitionDescriptor(this.Path, content, null);

			var metadata = new State(this, partition, this.AllocatorRng, rv);
			//TODO: locking?

			return tr.Context.GetOrCreateLocalData(this, metadata);
		}

		public sealed class State : ISubspaceContext
		{

			// We start at the state "neutral", and can either transition to "mutable" or "cached" state, but once there, the transaction is "locked" in only these types of operations
			// In state neutral, we can do anything, but then attempt at performing incompatible operations in this transaction should throw.
			// - "Neutral" operations can be performed in any state.
			// - "Mutating" operations can only be performed in the mutated or neutral state (and end up in the mutated state)
			// - "Cached" operations can only be performed in the cached or neutral state (and end up in the cached state)

			/// <summary>This transaction hasn't yet used a method that could mutate the tree</summary>
			internal const int STATUS_NEUTRAL = 0;

			/// <summary>This transaction has called an API that mutated the tree</summary>
			internal const int STATUS_MUTATED = 1;

			/// <summary>This transaction has called an API that used a cached tree </summary>
			internal const int STATUS_CACHED = 2;

			/// <summary>This transaction has been disposed</summary>
			internal const int STATUS_DEAD = 3;

			/// <summary>Current "state" of the transaction in regard to the Directory Layer</summary>
			/// <remarks>Once a transaction has called an API that mutates or use the cache, other kinds of operations are disallowed!</remarks>
			internal int Status;

			public FdbDirectoryLayer Layer { get; }

			internal PartitionDescriptor Partition { get; }

			public Random Allocator { get; }

			public long ReadVersion { get; }

			internal State(FdbDirectoryLayer layer, PartitionDescriptor partition, Random rng, long readVersion)
			{
				this.Layer = layer;
				this.Partition = partition;
				this.Allocator = rng;
				this.ReadVersion = readVersion;
			}

			private static void SetLayer(IFdbTransaction trans, PartitionDescriptor partition, Slice prefix, string layer)
			{
				Contract.Requires(layer != null);
				trans.Set(partition.Nodes.Encode(prefix, LayerAttribute), Slice.FromStringUtf8(layer));
			}

			private void UpdatePartitionMetadataVersion(IFdbTransaction trans, PartitionDescriptor partition, bool init = false)
			{
				// update the metadata version of this partition
				if (init && partition.Parent == null)
				{
					// IMPORTANT: The metadata key is NOT a read-version or version-stamp, it is a simple counter atomically incremented!
					// instead of starting the counter at 0, we use the current read-version of the database, in hope that it would be
					// greater than any previously used value if the entire DL was cleared and re-created, but from then on it is a simple
					// opaque monotonically incremeting number.
					trans.Set(partition.MetadataKey, Slice.FromInt64BE(this.ReadVersion));
				}
				else
				{
					trans.AtomicIncrement64(partition.MetadataKey);
					//note: creating multiple embedded partitions may increment the version of the root multiple times,
					// but this is not really an issue: we just need it to be different than any previously observed value.
				}

				// make sure that the transaction is safe for mutation, and update the global metadata version if required
				if (EnsureCanMutate())
				{
					trans.TouchMetadataVersionKey();
				}
			}

			/// <summary>Finds a node subspace, given its path, by walking the tree from the root.</summary>
			/// <returns>Node if it was found, or null</returns>
			private static async Task<Node> FindAsync(IFdbReadOnlyTransaction tr, PartitionDescriptor partition, FdbPath path)
			{
				Contract.Requires(tr != null);

				// look for the node by traversing from the root down. jumping over when crossing a partition...

				var current = partition.Nodes.GetPrefix();

				int i = 0;
				string layer = FdbDirectoryPartition.LayerId; // the root is by convention a "partition"
				PartitionDescriptor parent = partition;
				Slice prefixInParentPartition  = current;
				while (i < path.Count)
				{
					if (AnnotateTransactions) tr.Annotate("Looking for child {0} under node {1}...", path[i], FdbKey.Dump(current));

					// maybe use the node cache, if allowed
					var key = partition.Nodes.Encode(current, SUBDIRS, path[i].Name);
					current = await tr.GetAsync(key).ConfigureAwait(false);

					if (current.IsNull)
					{
						return new Node(path, Slice.Nil, null, partition, parent, Slice.Nil);
					}

					// get the layer id of this node
					layer = (await tr.GetAsync(partition.Nodes.Encode(current, LayerAttribute)).ConfigureAwait(false)).ToStringUtf8() ?? string.Empty;
					if (AnnotateTransactions) tr.Annotate("Found subfolder '{0}' at {1} ({2})", path[i].Name, FdbKey.Dump(current), layer);

					parent = partition;

					prefixInParentPartition = current;
					if (layer == FdbDirectoryPartition.LayerId)
					{ // jump to that partition's node subspace
						partition = partition.CreateChild(path.Substring(0, i + 1), current);
						current = partition.Nodes.GetPrefix();
					}

					++i;
				}

				return new Node(path, current, layer, partition, parent, prefixInParentPartition);
			}

			/// <summary>Open a subspace using the local cache</summary>
			internal async ValueTask<FdbDirectorySubspace?> OpenCachedInternalAsync(IFdbReadOnlyTransaction trans, FdbPath path, bool throwOnError)
			{
				Contract.Requires(trans != null);

				EnsureAbsolutePath(in path);

				var ctx = await GetContext(trans);
				Contract.Assert(ctx != null);

				if (EnsureCanCache(trans))
				{
					if (ctx.TryGetSubspace(trans, this, path, out var subspace))
					{
						return subspace;
					}
				}

				return await OpenCachedInternalSlow(ctx, trans, path, throwOnError);
			}

			private async Task<FdbDirectorySubspace?> OpenCachedInternalSlow(CacheContext context, IFdbReadOnlyTransaction readTrans, FdbPath path, bool throwOnError)
			{
				var existingNode = await FindAsync(readTrans, this.Partition, path).ConfigureAwait(false);

				// Path of the partition that contains the target directory (updated whenever we traverse partitions)

				FdbDirectorySubspace? subspace;
				if (existingNode.Exists)
				{
					var layer = path.LayerId;
					if (!string.IsNullOrEmpty(layer) && layer != existingNode.Layer)
					{
						throw new InvalidOperationException($"The directory {path} was created with incompatible layer '{layer}' instead of expected '{existingNode.Layer}'.");
					}
					subspace = ContentsOfNode(existingNode.Path, existingNode.Prefix, existingNode.Layer!, existingNode.Partition, existingNode.ParentPartition, context);
				}
				else
				{
					if (throwOnError) throw new InvalidOperationException($"The directory {path} does not exist.");
					subspace = null;
				}

				return context.AddSubspace(path, subspace)?.ChangeContext(this);
			}

			/// <summary>Open a subspace using the local cache</summary>
			internal async ValueTask<FdbDirectorySubspace?[]> OpenCachedInternalAsync(IFdbReadOnlyTransaction trans, ReadOnlyMemory<FdbPath> paths, bool throwOnError)
			{
				Contract.Requires(trans != null);

				var ctx = await GetContext(trans);
				Contract.Assert(ctx != null);

				if (EnsureCanCache(trans))
				{
					var results = new FdbDirectorySubspace?[paths.Length];
					List<(Task<FdbDirectorySubspace?> Task, int Index)>? tasks = null;
					for (int i = 0; i < paths.Length; i++)
					{
						var path = paths.Span[i];
						EnsureAbsolutePath(in path);
						if (ctx.TryGetSubspace(trans, this, path, out var subspace))
						{
							//TODO: check layer!
							results[i] = subspace;
						}
						else
						{
							tasks ??= new List<(Task<FdbDirectorySubspace?>, int)>();
							tasks.Add((OpenCachedInternalSlow(ctx, trans, path, throwOnError), i));
						}
					}

					if (tasks != null)
					{ // some of the directories were not in the cache!
						foreach (var (t, i) in tasks)
						{
							results[i] = await t;
						}
					}
					return results;
				}
				else
				{
					var tasks = new List<Task<FdbDirectorySubspace?>>(paths.Length);
					for (int i = 0; i < paths.Length; i++)
					{
						var path = paths.Span[i];
						tasks.Add(CreateOrOpenInternalAsync(trans, null, path, Slice.Nil, false, true, throwOnError));
					}

					return await Task.WhenAll(tasks);
				}
			}

			internal async Task<FdbDirectorySubspace?> CreateOrOpenInternalAsync(IFdbReadOnlyTransaction? readTrans, IFdbTransaction? trans, FdbPath path, Slice prefix, bool allowCreate, bool allowOpen, bool throwOnError)
			{
				Contract.Requires(readTrans != null || trans != null, "Need at least one transaction");
				Contract.Requires(readTrans == null || trans == null || object.ReferenceEquals(readTrans, trans), "The write transaction should be the same as the read transaction.");

				EnsureAbsolutePath(in path);

				if (path.IsRoot)
				{ // Root directory contains node metadata and so may not be opened.
					throw new InvalidOperationException("The root directory may not be opened.");
				}

				// to open an existing directory, we only need the read transaction
				// if none was specified, we can use the writable transaction
				readTrans ??= trans;
				if (readTrans == null) throw new ArgumentNullException(nameof(readTrans), "You must either specify either a read or write transaction!");

				await CheckReadVersionAsync(readTrans);

				if (prefix.HasValue && this.Layer.Path.Count > 0)
					throw new InvalidOperationException("Cannot specify a prefix in a partition.");

				string layer = path.LayerId;

				var existingNode = await FindAsync(readTrans, this.Partition, path).ConfigureAwait(false);

				if (existingNode.Exists)
				{
					if (!allowOpen)
					{
						if (throwOnError) throw new InvalidOperationException($"The directory {path} already exists.");
						return null;
					}

					if (!string.IsNullOrEmpty(layer) && layer != existingNode.Layer)
					{
						throw new InvalidOperationException($"The directory {path} was created with incompatible layer '{existingNode.Layer}' instead of expected '{layer}'.");
					}
					return ContentsOfNode(existingNode.Path, existingNode.Prefix, existingNode.Layer!, existingNode.Partition, existingNode.ParentPartition, null);
				}

				if (!allowCreate)
				{
					if (throwOnError) throw new InvalidOperationException($"The directory {path} does not exist.");
					return null;
				}

				// from there, we actually do need a writable transaction
				if (trans == null) throw new InvalidOperationException("A writable transaction is needed to create a new directory.");

				await CheckWriteVersionAsync(trans).ConfigureAwait(false);

				// we need to recursively create any missing parents
				Slice parentPrefix;
				var partition  = this.Partition;
				if (path.Count > 1)
				{
					var parentSubspace = await CreateOrOpenInternalAsync(readTrans, trans, path.GetParent(), Slice.Nil, true, true, true).ConfigureAwait(false);
					Contract.Assert(parentSubspace != null);
					//HACKHACK: idéalement, CreateOrOpenInternalAsync devrait retourner toutes les informations en une seule fois!
					var parentNode = await FindAsync(readTrans, this.Partition, path.GetParent());
					partition = parentNode.Partition;
					parentPrefix = parentNode.Prefix;
				}
				else
				{
					parentPrefix = partition.Nodes.GetPrefix();
				}

				if (prefix.IsNull)
				{ // automatically allocate a new prefix inside the ContentSubspace
					long id = await FdbHighContentionAllocator.AllocateAsync(trans, partition.Nodes.Partition.ByKey(partition.Nodes.GetPrefix(), HcaAttribute).AsTyped<int, long>(), this.Allocator).ConfigureAwait(false);
					prefix = partition.Content.Encode(id);

					// ensure that there is no data already present under this prefix
					if (AnnotateTransactions) trans.Annotate("Ensure that there is no data already present under prefix {0:K}", prefix);
					if (await trans.GetRange(KeyRange.StartsWith(prefix)).AnyAsync().ConfigureAwait(false))
					{
						throw new InvalidOperationException($"The database has keys stored at the prefix chosen by the automatic prefix allocator: {prefix:K}.");
					}

					// ensure that the prefix has not already been allocated
					if (AnnotateTransactions) trans.Annotate("Ensure that the prefix {0:K} has not already been allocated", prefix);
					if (!(await IsPrefixFree(trans.Snapshot, partition, prefix).ConfigureAwait(false)))
					{
						throw new InvalidOperationException("The directory layer has manually allocated prefixes that conflict with the automatic prefix allocator.");
					}
				}
				else
				{
					if (AnnotateTransactions) trans.Annotate("Ensure that the prefix {0:K} hasn't already been allocated", prefix);
					// ensure that the prefix has not already been allocated
					if (!(await IsPrefixFree(trans, partition, prefix).ConfigureAwait(false)))
					{
						throw new InvalidOperationException("The given prefix is already in use.");
					}
				}

				// initialize the metadata for this new directory

				if (AnnotateTransactions) trans.Annotate("Registering the new prefix {0:K} into the folder sub-tree", prefix);
				trans.Set(partition.Nodes.Encode(parentPrefix, SUBDIRS, path.Name), prefix);

				// initialize the new folder
				SetLayer(trans, partition, prefix, layer);
				UpdatePartitionMetadataVersion(trans, partition);

				if (layer == FdbDirectoryPartition.LayerId)
				{
					InitializePartition(trans, existingNode.Partition);
				}

				return ContentsOfNode(path, prefix, layer, existingNode.Partition, existingNode.ParentPartition ?? existingNode.Partition, null);
			}

			internal async Task<FdbDirectorySubspace?> MoveInternalAsync(IFdbTransaction trans, FdbPath oldPath, FdbPath newPath, bool throwOnError)
			{
				Contract.NotNull(trans, nameof(trans));

				EnsureAbsolutePath(in oldPath);
				EnsureAbsolutePath(in newPath);

				if (oldPath.IsRoot)
				{
					throw new InvalidOperationException("The root directory may not be moved.");
				}
				if (newPath.IsRoot)
				{
					throw new InvalidOperationException("The root directory cannot be overwritten.");
				}
				if (newPath.StartsWith(oldPath))
				{
					throw new InvalidOperationException($"The destination directory '{newPath}' cannot be a sub-directory of the source directory '{oldPath}'.");
				}

				await CheckWriteVersionAsync(trans).ConfigureAwait(false);

				var oldNode = await FindAsync(trans, this.Partition, oldPath).ConfigureAwait(false);
				if (!oldNode.Exists)
				{
					if (throwOnError) throw new InvalidOperationException($"The source directory '{oldPath}' does not exist.");
					return null;
				}
				var newNode = await FindAsync(trans, this.Partition, newPath).ConfigureAwait(false);

				if (newNode.Exists)
				{
					if (throwOnError) throw new InvalidOperationException($"The destination directory '{newPath}' already exists. Remove it first.");
					return null;
				}

				Slice parentPrefix;
				PartitionDescriptor parentPartition;
				if (!newPath.IsRoot)
				{
					var parentNode = await FindAsync(trans, this.Partition, newPath.GetParent()).ConfigureAwait(false);
					if (!parentNode.Exists)
					{
						if (throwOnError) throw new InvalidOperationException($"The parent of the destination directory '{newPath}' does not exist. Create it first.");
						return null;
					}

					parentPartition = parentNode.Partition;
					parentPrefix = (parentPartition == parentNode.ParentPartition) ? parentNode.Prefix : parentNode.Partition.Nodes.GetPrefix();
				}
				else
				{
					parentPartition = this.Partition;
					parentPrefix = parentPartition.Nodes.GetPrefix();
				}

				// we have already checked that old and new are under this partition path, but one of them (or both?) could be under a sub-partition..
				if (oldNode.Partition != null && !oldNode.ParentPartition.Path.Equals(parentPartition.Path))
				{
					throw new InvalidOperationException($"Cannot move '{oldNode.Path}' to '{newNode.Path}' between partitions ('{oldNode.ParentPartition.Path}' != '{parentPartition.Path}').");
				}

				if (AnnotateTransactions) trans.Annotate("Register the prefix {0} to its new location in the folder sub-tree", oldNode.Prefix);

				// make sure that the transaction is safe for mutation, and update the global metadata version if required
				if (EnsureCanMutate())
				{
					trans.TouchMetadataVersionKey();
				}

				trans.Set(parentPartition.Nodes.Encode(parentPrefix, SUBDIRS, newPath.Name), oldNode.PrefixInParentPartition);
				UpdatePartitionMetadataVersion(trans, parentPartition);

				await RemoveFromParent(trans, oldPath).ConfigureAwait(false);

				return ContentsOfNode(newPath, oldNode.Prefix, oldNode.Layer, newNode.Partition, newNode.ParentPartition, null);
			}

			internal async Task<bool> RemoveInternalAsync(IFdbTransaction trans, FdbPath path, bool throwIfMissing)
			{
				Contract.NotNull(trans, nameof(trans));

				EnsureAbsolutePath(in path);

				// We don't allow removing the root directory, because it would probably end up wiping out all the database.
				if (path.IsRoot) throw new InvalidOperationException("The root directory may not be removed.");

				await CheckWriteVersionAsync(trans).ConfigureAwait(false);

				var n = await FindAsync(trans, this.Partition, path).ConfigureAwait(false);
				if (!n.Exists)
				{
					if (throwIfMissing) throw new InvalidOperationException($"The directory '{path}' does not exist.");
					return false;
				}

				// make sure that the transaction is safe for mutation, and update the global metadata version if required
				if (EnsureCanMutate())
				{
					trans.TouchMetadataVersionKey();
				}

				// Delete the node subtree and all the data
				await RemoveRecursive(trans, n.Partition, n.Prefix).ConfigureAwait(false);
				// Remove the node from the tree
				await RemoveFromParent(trans, path).ConfigureAwait(false);

				return true;
			}

			internal async Task<List<FdbPath>?> ListInternalAsync(IFdbReadOnlyTransaction trans, FdbPath path, bool throwIfMissing)
			{
				Contract.NotNull(trans, nameof(trans));

				EnsureAbsolutePath(in path);

				await CheckReadVersionAsync(trans);

				var node = await FindAsync(trans, this.Partition, path).ConfigureAwait(false);

				if (!node.Exists)
				{
					if (throwIfMissing) throw new InvalidOperationException($"The directory '{path}' does not exist.");
					return null;
				}

				return (await SubdirNamesAndNodes(trans, node.Partition, node.Prefix, includeLayers: true).ConfigureAwait(false))
					.Select(kvp => node.Path[new FdbPathSegment(kvp.Name, kvp.LayerId)])
					.ToList()
					;
			}

			internal async Task<bool> ExistsInternalAsync(IFdbReadOnlyTransaction trans, FdbPath path)
			{
				Contract.Requires(trans != null);

				EnsureAbsolutePath(in path);

				await CheckReadVersionAsync(trans).ConfigureAwait(false);

				var node = await FindAsync(trans, this.Partition, path).ConfigureAwait(false);

				if (!node.Exists) return false;

				return true;
			}

			internal async Task ChangeLayerInternalAsync(IFdbTransaction trans, FdbPath path, string newLayer)
			{
				Contract.Requires(trans != null && newLayer != null);

				EnsureAbsolutePath(in path);

				await CheckWriteVersionAsync(trans).ConfigureAwait(false);

				var node = await FindAsync(trans, this.Partition, path).ConfigureAwait(false);

				if (!node.Exists)
				{
					throw new InvalidOperationException($"The directory '{path}' does not exist, or as already been removed.");
				}

				SetLayer(trans, node.Partition, node.Prefix, newLayer);
				UpdatePartitionMetadataVersion(trans, node.Partition);
			}

			private async Task<Slice> CheckReadVersionAsync(IFdbReadOnlyTransaction trans)
			{
				//TODO: we could defer the check using the cache context!

				var value = await trans.GetAsync(this.Partition.VersionKey).ConfigureAwait(false);
				if (!value.IsNullOrEmpty)
				{
					CheckVersion(value, false);
				}

				return value;
			}

			private async Task CheckWriteVersionAsync(IFdbTransaction trans)
			{
				var version = await CheckReadVersionAsync(trans);

				if (version.IsNullOrEmpty)
				{
					InitializePartition(trans, this.Partition);
				}
			}

			private void InitializePartition(IFdbTransaction trans, PartitionDescriptor partition)
			{
				// Set the version key
				trans.Set(partition.VersionKey, MakeVersionValue());
				UpdatePartitionMetadataVersion(trans, partition, init: true);
			}

			private static Slice MakeVersionValue()
			{
				var writer = new SliceWriter(3 * 4);
				writer.WriteFixed32((uint) LayerVersion.Major);
				writer.WriteFixed32((uint) LayerVersion.Minor);
				writer.WriteFixed32((uint) LayerVersion.Build);
				return writer.ToSlice();
			}

			private async Task<bool> NodeContainingKey(IFdbReadOnlyTransaction tr, PartitionDescriptor partition, Slice key)
			{
				// Right now this is only used for _is_prefix_free(), but if we add
				// parent pointers to directory nodes, it could also be used to find a
				// path based on a key.

				if (partition.Nodes.Contains(key))
				{ 
					return true;
				}

				var kvp = await tr
					.GetRange(
						partition.Nodes.ToRange().Begin,
						partition.Nodes.Encode(key) + FdbKey.MinValue
					)
					.LastOrDefaultAsync()
					.ConfigureAwait(false);

				if (kvp.Key.HasValue)
				{
					var prevPrefix = partition.Nodes.DecodeFirst<Slice>(kvp.Key);
					if (key.StartsWith(prevPrefix))
					{
						return true;
					}
				}

				return false;
			}

			/// <summary>Returns a new Directory Subspace given its node subspace, path and layer id</summary>
			private FdbDirectorySubspace ContentsOfNode(FdbPath path, Slice prefix, string layer, PartitionDescriptor partition, PartitionDescriptor parentPartition, ISubspaceContext? context)
			{
				Contract.Requires(partition != null && parentPartition != null);

				if (layer == FdbDirectoryPartition.LayerId)
				{
					var descriptor = new DirectoryDescriptor(this.Layer, path, partition.Content.GetPrefix(), FdbDirectoryPartition.LayerId, partition);
					return new FdbDirectoryPartition(descriptor, parentPartition, TuPack.Encoding.GetDynamicKeyEncoder(), context);
				}
				else
				{
					var descriptor = new DirectoryDescriptor(this.Layer, path, prefix, layer, partition);
					return new FdbDirectorySubspace(descriptor, TuPack.Encoding.GetDynamicKeyEncoder(), context);
				}
			}

			/// <summary>Returns the list of names and nodes of all children of the specified node</summary>
			private async Task<List<(string Name, string? LayerId, Slice Prefix)>> SubdirNamesAndNodes(IFdbReadOnlyTransaction tr, PartitionDescriptor partition, Slice prefix, bool includeLayers)
			{
				Contract.Requires(tr != null && partition != null);

				var sd = partition.Nodes.Partition.ByKey(prefix, SUBDIRS);

				var items = await tr
					.GetRange(sd.ToRange())
					.Select(kvp => (Name: sd.Decode<string>(kvp.Key) ?? string.Empty, Prefix: kvp.Value))
					.ToListAsync();

				// fetch the layers from the corresponding directories
				var layers = includeLayers ? await tr.GetValuesAsync(items.Select(item => partition.Nodes.Encode(item.Prefix, LayerAttribute))) : null;

				var res = new List<(string, string?, Slice)>(items.Count);
				for (int i = 0; i < items.Count; i++)
				{
					res.Add((items[i].Name, layers != null ? (layers[i].ToStringUtf8() ?? string.Empty) : null, items[i].Prefix));
				}

				return res;
			}

			/// <summary>Remove an existing node from its parents</summary>
			/// <returns>True if the parent node was found, otherwise false</returns>
			private async Task<bool> RemoveFromParent(IFdbTransaction tr, FdbPath path)
			{
				Contract.Requires(tr != null);

				var parent = await FindAsync(tr, this.Partition, path.GetParent()).ConfigureAwait(false);
				if (parent.Exists)
				{
					if (AnnotateTransactions) tr.Annotate("Removing path {0} from its parent folder at {1}", path, parent.Prefix);
					tr.Clear(GetSubDirKey(parent.Partition, parent.Prefix, path.Name));
					UpdatePartitionMetadataVersion(tr, parent.Partition);
					return true;
				}
				return false;
			}

			/// <summary>Recursively remove a node (including the content), all its children</summary>
			private async Task RemoveRecursive(IFdbTransaction tr, PartitionDescriptor partition, Slice prefix)
			{
				Contract.Requires(tr != null && partition != null);

				//note: we could use Task.WhenAll to remove the children, but there is a risk of task explosion if the subtree is very large...
				var children = await SubdirNamesAndNodes(tr, partition, prefix, includeLayers: false);
				await Task.WhenAll(children.Select(child => RemoveRecursive(tr, partition, child.Prefix))).ConfigureAwait(false);

				// remove ALL the contents
				if (AnnotateTransactions) tr.Annotate("Removing all content located under {0}", KeyRange.StartsWith(prefix));
				//TODO: REVIEW: we could get the prefix without calling ContentsOfNode here!
				tr.ClearRange(KeyRange.StartsWith(prefix));
				// and all the metadata for this folder
				if (AnnotateTransactions) tr.Annotate("Removing all metadata for folder under {0}", partition.Nodes.EncodeRange(prefix));
				tr.ClearRange(partition.Nodes.EncodeRange(prefix));
			}

			private async Task<bool> IsPrefixFree(IFdbReadOnlyTransaction tr, PartitionDescriptor partition, Slice prefix)
			{
				Contract.Requires(tr != null);

				// Returns true if the given prefix does not "intersect" any currently
				// allocated prefix (including the root node). This means that it neither
				// contains any other prefix nor is contained by any other prefix.

				if (prefix.IsNullOrEmpty) return false;

				if (await NodeContainingKey(tr, partition, prefix).ConfigureAwait(false))
				{
					return false;

				}

				return await tr
					.GetRange(
						partition.Nodes.Encode(prefix),
						partition.Nodes.Encode(FdbKey.Increment(prefix))
					)
					.NoneAsync()
					.ConfigureAwait(false);
			}

			private static Slice GetSubDirKey(PartitionDescriptor partition, Slice prefix, string segment)
			{
				Contract.Requires(partition != null);

				// for a path equal to ("foo","bar","baz") and index = -1, we need to generate (parent, SUBDIRS, "baz")
				// but since the last item of path can be of any type, we will use tuple splicing to copy the last item without changing its type
				return partition.Nodes.Encode(prefix, SUBDIRS, segment);
			}

			/// <summary>Ensure that this transaction can perform mutation operations</summary>
			/// <returns>True the first time a DL mutation is performed with this transaction, of false if it already did</returns>
			/// <exception cref="InvalidOperationException">If this transaction was already used for cached operations</exception>
			internal bool EnsureCanMutate()
			{
				while (true)
				{
					var state = Volatile.Read(ref this.Status);
					switch (state)
					{
						case STATUS_CACHED:
						{
							return true;
						}
						case STATUS_NEUTRAL:
						{ // first mutation?
							if (Interlocked.CompareExchange(ref this.Status, STATUS_MUTATED, STATUS_NEUTRAL) != STATUS_NEUTRAL)
							{ // another thread is racing with us! try again
								continue;
							}

							// yes, update the global metadata version!
							return true;
						}
						case STATUS_MUTATED:
						{ // already mutated
							return false;
						}
					}
				}
			}

			/// <summary>Ensure that this transaction can perform caching operations</summary>
			/// <returns>True the first time a DL cached operation is performed with this transaction, of false if it already did</returns>
			/// <exception cref="InvalidOperationException">If this transaction was already used for mutating operations</exception>
			internal bool EnsureCanCache(IFdbReadOnlyTransaction tr)
			{
				while (true)
				{
					var state = Volatile.Read(ref this.Status);
					switch (state)
					{
						case STATUS_MUTATED:
						{
							//throw new InvalidOperationException("Cannot perform a cache operation on the Directory Layer inside a transaction that has already mutated the tree.");
							return false;
						}
						case STATUS_NEUTRAL:
						{ // first cache?
							if (Interlocked.CompareExchange(ref this.Status, STATUS_CACHED, STATUS_NEUTRAL) != STATUS_NEUTRAL)
							{ // another thread is racing with us! try again
								continue;
							}
							if (tr is IFdbTransaction writable && !writable.IsReadOnly)
							{
								// the transaction is using a cached entry, so we must add a read conflict to protect against external changes!
								writable.AddReadConflictKey(this.Partition.MetadataKey);
							}
							return true;
						}
						case STATUS_CACHED:
						{ // already cached
							return true;
						}
					}
				}
			}

			public void Dispose()
			{
				if (Interlocked.Exchange(ref this.Status, STATUS_DEAD) != STATUS_DEAD)
				{
					this.Context = null;
				}
			}

			internal async ValueTask<CacheContext> GetContext(IFdbReadOnlyTransaction trans)
			{
				Contract.NotNull(trans, nameof(trans));

				// We will first check the value of the global \xff/metadataVersion key.
				// - If it hasn't changed, then we assume that the current context is still valid
				// - If it has changed, then we will have to read our local metadata version (stored in the partition)
				//  - If it hasn't changed, the current context is still valid, and can be updated to the last global metadata version
				//  - If it has changed, then we (for now) throw away the entire cache and start a new one.

				// Check the global metadata version key (should be fast, obtained along side the transaction read version)
				var gmv = await trans.GetMetadataVersionKeyAsync().ConfigureAwait(false);
				// We can observe "null" if the key has already been touched previously in the same transaction.
				// Since it is a versionstamp, we have no way to know its value until we commit, and this transaction will not be able cache anything
				// => we hope that the NEXT transaction will rebuild the cache for us.

				var context = Volatile.Read(ref this.Context) ?? this.Layer.Cache;
				if (context != null)
				{
					if (gmv != null && context.MetadataVersion == gmv.Value)
					{ // no change in the read version means that the context is unchanged
						if (AnnotateTransactions) trans.Annotate($"{this.Layer} cache context still valid (GMV hit)");
						return context;
					}

					if (AnnotateTransactions) trans.Annotate($"{this.Layer} cache context must be re-validated (GMV miss {gmv} != {context.MetadataVersion})");
					// context is not valid anymore!
				}

				//TODO: if this is the first call on this transaction, we must check the read layer version!

				// we need to check if the context is valid
				long? pmv = (await trans.GetAsync(this.Partition.MetadataKey)).ToInt64BE();

				if (context != null)
				{
					// if the pmv has a value, then no other mutating DL operation has run on this transaction
					// if it is null, then another transaction has mutated the DL, which is not supported
					if (pmv == null) throw new InvalidOperationException("Cannot use cached Directory Layer operations if the partition has already been mutated in the same transaction");

					if (context.PartitionVersion == pmv.Value)
					{ // no change in the partition read version means that the change was from someone else
						if (AnnotateTransactions) trans.Annotate($"{this.Layer} cache context still valid (PMV hit {pmv})");
						if (gmv != null) context.BumpMetadataVersion(gmv.Value);
						return context;
					}
					if (AnnotateTransactions) trans.Annotate($"{this.Layer} partition version has changed (PMV miss {pmv} != {context.PartitionVersion})");
				}

				// create a new context!
				context = new CacheContext(this.Layer, gmv, pmv);

				var global = this.Layer.Cache;
				if (gmv != null && pmv != null && (global == null || global.PartitionVersion < pmv))
				{ // make this the most recent context!
					this.Layer.Cache = context;
				}

				// attach that context to the transaction
				if (Interlocked.Exchange(ref this.Context, context) == null)
				{
					trans.Context.OnSuccess(this.OnTransactionStateChanged);
				}
				return context;
			}

			internal void OnTransactionStateChanged(FdbOperationContext ctx, FdbTransactionState state)
			{
				if (state != FdbTransactionState.Commit)
				{ // reset the context in the initial state
					Interlocked.Exchange(ref this.Status, STATUS_NEUTRAL);
					this.Context = null;
				}
				else
				{ // poison the context
					Dispose();
				}
			}

			internal CacheContext? Context;

			string ISubspaceContext.Name => this.Context?.DirectoryLayer.FullName ?? "<invalid>";

			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			void ISubspaceContext.EnsureIsValid()
			{
				if (Volatile.Read(ref this.Status) == STATUS_DEAD) throw CannotUseCacheContext();
			}

			[Pure, MethodImpl(MethodImplOptions.NoInlining)]
			private static Exception CannotUseCacheContext() => new InvalidOperationException("Directory Subspace context cannot be used outside its origination transaction.");

		}

		internal static void EnsureAbsolutePath(in FdbPath path)
		{
			if (!path.IsAbsolute)
			{
				throw new ArgumentException("The Directory Layer cannot process relative paths.", nameof(path));
			}
		}

		internal FdbPath VerifyPath(in FdbPath path, string? argName = null)
		{
			if (path.IsAbsolute)
			{
				if (!path.StartsWith(this.Path)) throw new ArgumentException("The specified path is outside the current Directory Layer", argName ?? nameof(path));
				return path;
			}
			return this.Path[path];
		}

		private static void CheckVersion(Slice value, bool writeAccess)
		{
			// the version is stored as 3 x 32-bit unsigned int, so (1, 0, 0) will be "<01><00><00><00> <00><00><00><00> <00><00><00><00>"
			var reader = new SliceReader(value);
			var major = reader.ReadFixed32();
			var minor = reader.ReadFixed32();
			var upgrade = reader.ReadFixed32();

			if (major > LayerVersion.Major) throw new InvalidOperationException($"Cannot load directory with version {major}.{minor}.{upgrade} using directory layer {FdbDirectoryLayer.LayerVersion}.");
			if (writeAccess && minor > LayerVersion.Minor) throw new InvalidOperationException($"Directory with version {major}.{minor}.{upgrade} is read-only when opened using directory layer {FdbDirectoryLayer.LayerVersion}.");
		}


		#endregion

		/// <summary>Represents the cached state used by the DirectoryLayer to map key prefixes to subspaces</summary>
		[DebuggerDisplay("Id={Id}, MV={MetadataVersion}, PV={PartitionVersion}")]
		internal sealed class CacheContext : ISubspaceContext
		{
			// Multiple transactions can link to the same cache context
			// The directory layer keeps the last N contexts in cache
			// It will be reclaimed by the GC once nobody points to it (no cached by the DL, and no more active transactions)

			public FdbDirectoryLayer DirectoryLayer { get; }

			/// <summary>Maximum global metadata version at which this state has been observed to be still valid</summary>
			public VersionStamp? MetadataVersion { get; private set; }
			// content of '\xff/metadataVersion' key

			/// <summary>Metadata version of the partition that produced this context</summary>
			//public VersionStamp? PartitionVersion { get; set; }
			public long? PartitionVersion { get; set; }
			// content of [PARTITION, "metadata"] key

			/// <summary>Version of the Directory Layer in the database</summary>
			public Slice LayerVersion { get; set; }
			// content of [PARTITION, "version"] key

			public Dictionary<FdbPath, FdbDirectorySubspace?> CachedSubspaces { get; } = new Dictionary<FdbPath, FdbDirectorySubspace?>();

			private ReaderWriterLockSlim Lock { get; } = new ReaderWriterLockSlim();

			private static int IdCounter;
			public readonly int Id = Interlocked.Increment(ref IdCounter);

			public CacheContext(FdbDirectoryLayer dl, VersionStamp? globalVersion, long? partitionVersion)
			{
				Contract.NotNull(dl, nameof(dl));

				this.DirectoryLayer = dl;
				this.MetadataVersion = globalVersion;
				this.PartitionVersion = partitionVersion;
			}

			public void BumpMetadataVersion(VersionStamp metadataVersion)
			{
				this.MetadataVersion = metadataVersion;
			}

			/// <summary>Lookup a subspace in the cache</summary>
			/// <param name="tr">Parent transaction</param>
			/// <param name="path">Absolute path to the subspace</param>
			/// <param name="subspace">If the method returns <c>true</c>, receives the cached subspace instance.</param>
			/// <returns>True if the subspace was found in the cache.</returns>
			public bool TryGetSubspace(IFdbReadOnlyTransaction tr, State state, FdbPath path, out FdbDirectorySubspace? subspace)
			{
				Contract.Requires(tr != null);
				subspace = null;

				FdbDirectorySubspace? candidate;

				this.Lock.EnterReadLock();
				try
				{
					if (!this.CachedSubspaces.TryGetValue(path, out candidate))
					{ // not in the cahce => we don't know
						if (AnnotateTransactions) tr.Annotate($"{this.DirectoryLayer} subspace MISS for {path}");
						return false;
					}
				}
				finally
				{
					this.Lock.ExitReadLock();
				}

				// if candidate == null, we know it DOES NOT exist (we checked previously, and noted its absence by inserting null in the cache)
				// if candidate != null, we know it DOES exist.

				if (AnnotateTransactions) tr.Annotate($"{this.DirectoryLayer} subspace HIT for {path}: {candidate?.ToString() ?? "<not_found>"}");

				// the subspace was created with another context, we must migrate it to the current transaction's context
				subspace = candidate?.ChangeContext(state);
				return true;
			}

			public FdbDirectorySubspace? AddSubspace(FdbPath path, FdbDirectorySubspace? subspace)
			{
				Contract.Requires(subspace == null || subspace.Descriptor.Path == path);
				//TODO: check !
				this.Lock.EnterWriteLock();
				try
				{
					this.CachedSubspaces[path] = subspace;
				}
				finally
				{
					this.Lock.ExitWriteLock();
				}
				return subspace;
			}

			public string Name => this.DirectoryLayer.FullName;

			public void EnsureIsValid()
			{
				//TODO?
			}

		}

		[DebuggerDisplay("Path={Path}, Prefix={Content}, Parent=({Parent})")]
		internal sealed class PartitionDescriptor
		{

			public FdbPath Path { get; }

			public PartitionDescriptor? Parent { get; }

			public IDynamicKeySubspace Content { get; }

			public IDynamicKeySubspace Nodes { get; }

			public Slice VersionKey { get; }

			public Slice MetadataKey { get; }

			public PartitionDescriptor(FdbPath path, IDynamicKeySubspace content, PartitionDescriptor? parent)
			{
				Contract.Requires(path.IsAbsolute && content != null);
				this.Path = path;
				this.Parent = parent;
				this.Content = content;
				this.Nodes = content.Partition[FdbKey.Directory];
				var rootNode = this.Nodes.Partition.ByKey(this.Nodes.GetPrefix());
				this.VersionKey = rootNode.Encode(VersionAttribute);
				this.MetadataKey = parent?.MetadataKey ?? rootNode.Encode(FdbDirectoryLayer.MetadataAttribute);
			}

			/// <summary>Return a child partition of the current partition</summary>
			public PartitionDescriptor CreateChild(FdbPath path, Slice prefix)
			{
				Contract.Requires(path.IsChildOf(this.Path), "Partition path is outside parent");
				return new PartitionDescriptor(path, KeySubspace.CreateDynamic(prefix), this);
			}

			public override string ToString() => $"PartitionDescriptor(Path={Path}, Prefix={Content.GetPrefix():K}, Parent=({Parent?.Path}, {Parent?.Content.GetPrefix():K}))";

		}

		[DebuggerDisplay("Path={Path}, Prefix={Prefix}, Layer={Layer}, Partition={Partition.Path}|{Partition.Content.GetPrefix()}")]
		internal sealed class DirectoryDescriptor
		{
			public DirectoryDescriptor(FdbDirectoryLayer directoryLayer, FdbPath path, Slice prefix, string layer, PartitionDescriptor partition)
			{
				Contract.Requires(directoryLayer != null && partition != null && path.StartsWith(partition.Path));

				this.DirectoryLayer = directoryLayer;
				this.Path = path;
				this.Prefix = prefix;
				this.Layer = layer ?? string.Empty;
				this.Partition = partition;

				Contract.Ensures(this.DirectoryLayer != null);
			}

			/// <summary>Absolute path of this directory, from the root directory</summary>
			public FdbPath Path { get; }

			public Slice Prefix { get; }

			public PartitionDescriptor Partition { get; }

			/// <summary>Instance of the DirectoryLayer that was used to create or open this directory</summary>
			public FdbDirectoryLayer DirectoryLayer { get; }

			/// <summary>Layer id of this directory</summary>
			public string Layer { get; }

			public override string ToString() => $"DirectoryDescriptor(Path={Path}, Prefix={Prefix:K}, Layer={Layer}, Partition=({Partition.Path}, {Partition.Content.GetPrefix():K}))";

		}
	}

}
