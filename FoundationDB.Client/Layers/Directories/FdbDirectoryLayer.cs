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

namespace FoundationDB.Layers.Directories
{
	using System;
	using System.Collections.Generic;
	using System.Diagnostics;
	using System.Linq;
	using System.Threading.Tasks;
	using JetBrains.Annotations;
	using Doxense.Collections.Tuples;
	using Doxense.Diagnostics.Contracts;
	using Doxense.Linq;
	using Doxense.Memory;
	using Doxense.Serialization.Encoders;
	using FoundationDB.Client;
	using FoundationDB.Filters.Logging;

	/// <summary>Provides a FdbDirectoryLayer class for managing directories in FoundationDB.
	/// Directories are a recommended approach for administering layers and applications. Directories work in conjunction with subspaces. Each layer or application should create or open at least one directory with which to manage its subspace(s).
	/// Directories are identified by paths (specified as tuples) analogous to the paths in a Unix-like file system. Each directory has an associated subspace that is used to store content. The layer uses a high-contention allocator to efficiently map each path to a short prefix for its corresponding subspace.
	/// <see cref="FdbDirectoryLayer"/> exposes methods to create, open, move, remove, or list directories. Creating or opening a directory returns the corresponding subspace.
	/// The <see cref="FdbDirectorySubspace"/> class represents subspaces that store the contents of a directory. An instance of <see cref="FdbDirectorySubspace"/> can be used for all the usual subspace operations. It can also be used to operate on the directory with which it was opened.
	/// </summary>
	[DebuggerDisplay("FullName={FullName}, Contents={ContentSubspace}, Nodes={NodeSubspace}")]
	public class FdbDirectoryLayer : IFdbDirectory
	{
		private const int SUBDIRS = 0;
		internal static readonly Version LayerVersion = new Version(1, 0, 0);
		internal static readonly Slice LayerSuffix = Slice.FromStringAscii("layer");
		internal static readonly Slice HcaKey = Slice.FromStringAscii("hca");
		internal static readonly Slice VersionKey = Slice.FromStringAscii("version");

		/// <summary>Use this flag to make the Directory Layer start annotating the transactions with a descriptions of all operations.</summary>
		/// <remarks>
		/// This is only usefull if you want to diagnose performance or read conflict issues.
		/// This will only work with logged transactions, obtained by applying the Logging Filter on a database instance
		/// </remarks>
		public static bool AnnotateTransactions { get; set; }

		/// <summary>Subspace where the content of each folder will be stored</summary>
		public IDynamicKeySubspace ContentSubspace { [NotNull] get; }

		/// <summary>Subspace where all the metadata nodes for each folder will be stored</summary>
		public IDynamicKeySubspace NodeSubspace { [NotNull] get; }

		/// <summary>Root node of the directory</summary>
		internal IDynamicKeySubspace RootNode { [NotNull] get; }

		/// <summary>Allocated used to generated prefix for new content</summary>
		internal FdbHighContentionAllocator Allocator { [NotNull] get; }

		/// <summary>Gets the path for the root node of this <code>FdbDirectoryLayer</code>.</summary>
		internal ITuple Location { [NotNull] get; }

		/// <summary>Name of root directory of this layer</summary>
		/// <remarks>Returns String.Empty for the root Directory Layer, or the name of the partition</remarks>
		public string Name
		{
			[NotNull]
			get { return this.Path.Count == 0 ? String.Empty : this.Path[this.Path.Count - 1]; }
		}

		/// <summary>Formatted path of the root directory of this layer</summary>
		public string FullName { [NotNull] get { return string.Join("/", this.Path); } }

		/// <summary>Gets the path for the root node of this <code>FdbDirectoryLayer</code></summary>
		/// <remarks>Normally constructed <code>DirectoryLayer</code>s have an empty path, but <code>DirectoryLayer</code>s returned by <see cref="IFdbDirectory.DirectoryLayer"/> for <see cref="IFdbDirectory"/>s inside of a <see cref="FdbDirectoryPartition"/> could have non-empty paths.</remarks>
		public IReadOnlyList<string> Path { [NotNull] get; private set; }

		/// <summary>Returns the layer id for this <code>FdbDirectoryLayer</code>, which is always Slice.Empty.</summary>
		Slice IFdbDirectory.Layer { get { return Slice.Empty; } }

		/// <summary>Self reference</summary>
		FdbDirectoryLayer IFdbDirectory.DirectoryLayer { get { return this; } }

		/// <summary>Convert a relative path in this Directory Layer, into an absolute path from the root of partition of the database</summary>
		[NotNull]
		internal ITuple PartitionSubPath(ITuple path = null)
		{
			// If the DL is the root, the path is already absolute
			// If the DL is used by a partition, then the path of the partition will be prepended to the path
			return path == null ? this.Location : this.Location.Concat(path);
		}

		void IFdbDirectory.CheckLayer(Slice layer)
		{
			if (layer.IsPresent)
			{
				throw new InvalidOperationException($"The directory layer {this.FullName} is not compatible with layer {layer:K}.");
			}
		}

		Task<FdbDirectorySubspace> IFdbDirectory.ChangeLayerAsync(IFdbTransaction trans, Slice newLayer)
		{
			throw new NotSupportedException("You cannot change the layer of an FdbDirectoryLayer.");
		}

		#region Constructors...

		/// <summary>
		/// Creates a new instance that will manages directories in FoudnationDB.
		/// </summary>
		/// <param name="nodeSubspace">Subspace where all the node metadata will be stored ('\xFE' by default)</param>
		/// <param name="contentSubspace">Subspace where all automatically allocated directories will be stored (empty by default)</param>
		/// <param name="location">Location of the root of all the directories managed by this Directory Layer. Ususally empty for the root partition of the database.</param>
		internal FdbDirectoryLayer(IDynamicKeySubspace nodeSubspace, IDynamicKeySubspace contentSubspace, ITuple location)
		{
			Contract.Requires(nodeSubspace != null && contentSubspace != null);

			// If specified, new automatically allocated prefixes will all fall within content_subspace
			this.ContentSubspace = contentSubspace;
			this.NodeSubspace = nodeSubspace;

			// The root node is the one whose contents are the node subspace
			this.RootNode = nodeSubspace.Partition.ByKey(nodeSubspace.GetPrefix());
			this.Allocator = new FdbHighContentionAllocator(this.RootNode.Partition.ByKey(HcaKey));
			if (location == null || location.Count == 0)
			{
				this.Location = STuple.Empty;
				this.Path = new string[0];
			}
			else
			{
				this.Location = location;
				this.Path = location.ToArray<string>();
			}
		}

		/// <summary>Create an instance of the default Directory Layer</summary>
		[NotNull]
		public static FdbDirectoryLayer Create()
		{
			return Create(Slice.Empty);
		}

		/// <summary>Create an instance of a Directory Layer located under a specific prefix and path</summary>
		/// <param name="prefix">Prefix for the content. The nodes will be stored under <paramref name="prefix"/> + &lt;FE&gt;</param>
		/// <param name="path">Optional path, if the Directory Layer is not located at the root of the database.</param>
		[NotNull]
		public static FdbDirectoryLayer Create(Slice prefix, IEnumerable<string> path = null)
		{
			var subspace = KeySubspace.FromKey(prefix).Using(TypeSystem.Tuples);
			var location = path != null ? ParsePath(path) : STuple.Empty;
			return new FdbDirectoryLayer(subspace.Partition[FdbKey.Directory], subspace, location);
		}

		/// <summary>Create an instance of a Directory Layer located under a specific subspace and path</summary>
		/// <param name="subspace">Subspace for the content. The nodes will be stored under <paramref name="subspace"/>.Key + &lt;FE&gt;</param>
		/// <param name="path">Optional path, if the Directory Layer is not located at the root of the database.</param>
		[NotNull]
		public static FdbDirectoryLayer Create(IKeySubspace subspace, IEnumerable<string> path = null)
		{
			if (subspace == null) throw new ArgumentNullException(nameof(subspace));

			var location = path != null ? ParsePath(path) : STuple.Empty;
			var space = subspace.Using(TypeSystem.Tuples);
			return new FdbDirectoryLayer(space.Partition[FdbKey.Directory], space, location);
		}

		/// <summary>Create an instance of a Directory Layer located under a specific subpsace and path</summary>
		/// <param name="nodeSubspace">Subspace for the nodes of the Directory Layer.</param>
		/// <param name="contentSubspace">Subspace for the content of the Directory Layer.</param>
		/// <param name="path">Optional path, if the Directory Layer is not located at the root of the database</param>
		[NotNull]
		public static FdbDirectoryLayer Create(IDynamicKeySubspace nodeSubspace, IDynamicKeySubspace contentSubspace, IEnumerable<string> path = null)
		{
			if (nodeSubspace == null) throw new ArgumentNullException(nameof(nodeSubspace));
			if (contentSubspace == null) throw new ArgumentNullException(nameof(contentSubspace));

			var location = path != null ? ParsePath(path) : STuple.Empty;
			//TODO: check that nodeSubspace != contentSubspace?
			return new FdbDirectoryLayer(nodeSubspace, contentSubspace, location);
		}

		#endregion

		#region Public Methods...

		#region CreateOrOpen / Open / Create

		/// <summary>Opens the directory with the given path. If the directory does not exist, it is created (creating parent directories if necessary).</summary>
		/// <param name="trans">Transaction to use for the operation</param>
		/// <param name="path">Path of the directory to create or open</param>
		/// <param name="layer">If layer is specified, it is checked against the layer of an existing directory or set as the layer of a new directory.</param>
		[ItemNotNull]
		public Task<FdbDirectorySubspace> CreateOrOpenAsync([NotNull] IFdbTransaction trans, [NotNull] IEnumerable<string> path, Slice layer = default(Slice))
		{
			if (trans == null) throw new ArgumentNullException(nameof(trans));
			if (path == null) throw new ArgumentNullException(nameof(path));

			return CreateOrOpenInternalAsync(null, trans, ParsePath(path), layer, Slice.Nil, allowCreate: true, allowOpen: true, throwOnError: true);
		}

		/// <summary>Opens the directory with the given <paramref name="path"/>.
		/// An exception is thrown if the directory does not exist, or if a layer is specified and a different layer was specified when the directory was created.
		/// </summary>
		/// <param name="trans">Transaction to use for the operation</param>
		/// <param name="path">Path of the directory to open.</param>
		/// <param name="layer">Optional layer id of the directory. If it is different than the layer specified when creating the directory, an exception will be thrown.</param>
		[ItemNotNull]
		public Task<FdbDirectorySubspace> OpenAsync([NotNull] IFdbReadOnlyTransaction trans, [NotNull] IEnumerable<string> path, Slice layer = default(Slice))
		{
			if (trans == null) throw new ArgumentNullException(nameof(trans));
			if (path == null) throw new ArgumentNullException(nameof(path));

			return CreateOrOpenInternalAsync(trans, null, ParsePath(path), layer, prefix: Slice.Nil, allowCreate: false, allowOpen: true, throwOnError: true);
		}

		/// <summary>Creates a directory with the given <paramref name="path"/> (creating parent directories if necessary).
		/// An exception is thrown if the given directory already exists.
		/// </summary>
		/// <param name="trans">Transaction to use for the operation</param>
		/// <param name="path">Path of the directory to create</param>
		/// <param name="layer">If <paramref name="layer"/> is specified, it is recorded with the directory and will be checked by future calls to open.</param>
		[ItemNotNull]
		public Task<FdbDirectorySubspace> CreateAsync([NotNull] IFdbTransaction trans, [NotNull] IEnumerable<string> path, Slice layer = default(Slice))
		{
			if (trans == null) throw new ArgumentNullException(nameof(trans));
			if (path == null) throw new ArgumentNullException(nameof(path));

			return CreateOrOpenInternalAsync(null, trans, ParsePath(path), layer, prefix: Slice.Nil, allowCreate: true, allowOpen: false, throwOnError: true);
		}

		/// <summary>Attempts to open the directory with the given <paramref name="path"/>.</summary>
		/// <param name="trans">Transaction to use for the operation</param>
		/// <param name="path">Path of the directory to open.</param>
		/// <param name="layer">Optional layer id of the directory. If it is different than the layer specified when creating the directory, an exception will be thrown.</param>
		[ItemCanBeNull]
		public Task<FdbDirectorySubspace> TryOpenAsync([NotNull] IFdbReadOnlyTransaction trans, [NotNull] IEnumerable<string> path, Slice layer = default(Slice))
		{
			if (trans == null) throw new ArgumentNullException(nameof(trans));
			if (path == null) throw new ArgumentNullException(nameof(path));

			return CreateOrOpenInternalAsync(trans, null, ParsePath(path), layer, prefix: Slice.Nil, allowCreate: false, allowOpen: true, throwOnError: false);
		}

		/// <summary>Attempts to create a directory with the given <paramref name="path"/> (creating parent directories if necessary).</summary>
		/// <param name="trans">Transaction to use for the operation</param>
		/// <param name="path">Path of the directory to create</param>
		/// <param name="layer">If <paramref name="layer"/> is specified, it is recorded with the directory and will be checked by future calls to open.</param>
		[ItemCanBeNull]
		public Task<FdbDirectorySubspace> TryCreateAsync([NotNull] IFdbTransaction trans, [NotNull] IEnumerable<string> path, Slice layer = default(Slice))
		{
			if (trans == null) throw new ArgumentNullException(nameof(trans));
			if (path == null) throw new ArgumentNullException(nameof(path));

			return CreateOrOpenInternalAsync(null, trans, ParsePath(path), layer, prefix: Slice.Nil, allowCreate: true, allowOpen: false, throwOnError: false);
		}

		/// <summary>Registers an existing prefix as a directory with the given <paramref name="path"/> (creating parent directories if necessary). This method is only indented for advanced use cases.</summary>
		/// <param name="trans">Transaction to use for the operation</param>
		/// <param name="path">Path of the directory to create</param>
		/// <param name="layer">If <paramref name="layer"/> is specified, it is recorded with the directory and will be checked by future calls to open.</param>
		/// <param name="prefix">The directory will be created with the given physical prefix; otherwise a prefix is allocated automatically.</param>
		[ItemNotNull]
		public Task<FdbDirectorySubspace> RegisterAsync([NotNull] IFdbTransaction trans, [NotNull] IEnumerable<string> path, Slice layer, Slice prefix)
		{
			if (trans == null) throw new ArgumentNullException(nameof(trans));
			if (path == null) throw new ArgumentNullException(nameof(path));

			return CreateOrOpenInternalAsync(null, trans, ParsePath(path), layer, prefix: prefix, allowCreate: true, allowOpen: false, throwOnError: true);
		}

		/// <summary>Attempts to register an existing prefix as a directory with the given <paramref name="path"/> (creating parent directories if necessary). This method is only indented for advanced use cases.</summary>
		/// <param name="trans">Transaction to use for the operation</param>
		/// <param name="path">Path of the directory to create</param>
		/// <param name="layer">If <paramref name="layer"/> is specified, it is recorded with the directory and will be checked by future calls to open.</param>
		/// <param name="prefix">The directory will be created with the given physical prefix; otherwise a prefix is allocated automatically.</param>
		[ItemCanBeNull]
		public Task<FdbDirectorySubspace> TryRegisterAsync([NotNull] IFdbTransaction trans, [NotNull] IEnumerable<string> path, Slice layer, Slice prefix)
		{
			if (trans == null) throw new ArgumentNullException(nameof(trans));
			if (path == null) throw new ArgumentNullException(nameof(path));

			return CreateOrOpenInternalAsync(null, trans, ParsePath(path), layer, prefix: prefix, allowCreate: true, allowOpen: false, throwOnError: false);
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
		[ItemNotNull]
		public Task<FdbDirectorySubspace> MoveAsync(IFdbTransaction trans, IEnumerable<string> oldPath, IEnumerable<string> newPath)
		{
			if (trans == null) throw new ArgumentNullException(nameof(trans));
			if (oldPath == null) throw new ArgumentNullException(nameof(oldPath));
			if (newPath == null) throw new ArgumentNullException(nameof(newPath));

			var oldLocation = STuple.FromEnumerable(oldPath);
			VerifyPath(oldLocation, "oldPath");
			var newLocation = STuple.FromEnumerable(newPath);
			VerifyPath(newLocation, "newPath");

			return MoveInternalAsync(trans, oldLocation, newLocation, throwOnError: true);
		}

		/// <summary>Attempts to move the directory found at <paramref name="oldPath"/> to <paramref name="newPath"/>.
		/// There is no effect on the physical prefix of the given directory, or on clients that already have the directory open.
		/// Returns null if the old directory does not exist, a directory already exists at `new_path`, or the parent directory of `new_path` does not exist.
		/// </summary>
		/// <param name="trans">Transaction to use for the operation</param>
		/// <param name="oldPath">Path of the directory to move</param>
		/// <param name="newPath">New path of the directory</param>
		[ItemCanBeNull]
		public Task<FdbDirectorySubspace> TryMoveAsync(IFdbTransaction trans, IEnumerable<string> oldPath, IEnumerable<string> newPath)
		{
			if (trans == null) throw new ArgumentNullException(nameof(trans));
			if (oldPath == null) throw new ArgumentNullException(nameof(oldPath));
			if (newPath == null) throw new ArgumentNullException(nameof(newPath));

			var oldLocation = STuple.FromEnumerable(oldPath);
			VerifyPath(oldLocation, "oldPath");
			var newLocation = STuple.FromEnumerable(newPath);
			VerifyPath(newLocation, "newPath");

			return MoveInternalAsync(trans, oldLocation, newLocation, throwOnError: false);
		}

		#endregion

		#region MoveTo / TryMoveTo

		Task<FdbDirectorySubspace> IFdbDirectory.MoveToAsync(IFdbTransaction trans, IEnumerable<string> newAbsolutePath)
		{
			throw new NotSupportedException("The root directory cannot be moved");
		}

		Task<FdbDirectorySubspace> IFdbDirectory.TryMoveToAsync(IFdbTransaction trans, IEnumerable<string> newAbsolutePath)
		{
			throw new NotSupportedException("The root directory cannot be moved");
		}

		#endregion

		#region Remove / TryRemove

		/// <summary>Removes the directory, its contents, and all subdirectories.
		/// Warning: Clients that have already opened the directory might still insert data into its contents after it is removed.
		/// </summary>
		/// <param name="trans">Transaction to use for the operation</param>
		/// <param name="path">Path of the directory to remove (including any subdirectories)</param>
		public Task RemoveAsync([NotNull] IFdbTransaction trans, [NotNull] IEnumerable<string> path)
		{
			if (trans == null) throw new ArgumentNullException(nameof(trans));
			if (path == null) throw new ArgumentNullException(nameof(path));

			return RemoveInternalAsync(trans, ParsePath(path), throwIfMissing: true);
		}

		/// <summary>Attempts to remove the directory, its contents, and all subdirectories.
		/// Warning: Clients that have already opened the directory might still insert data into its contents after it is removed.
		/// </summary>
		/// <param name="trans">Transaction to use for the operation</param>
		/// <param name="path">Path of the directory to remove (including any subdirectories)</param>
		public Task<bool> TryRemoveAsync([NotNull] IFdbTransaction trans, [NotNull] IEnumerable<string> path)
		{
			if (trans == null) throw new ArgumentNullException(nameof(trans));
			if (path == null) throw new ArgumentNullException(nameof(path));

			var location = ParsePath(path);
			if (location.Count == 0) throw new NotSupportedException("Cannot remove a directory layer");
			return RemoveInternalAsync(trans, location, throwIfMissing: false);
		}

		#endregion

		#region Exists

		/// <summary>Checks if a directory already exists</summary>
		/// <param name="trans">Transaction to use for the operation</param>
		/// <param name="path">Path of the directory to remove (including any subdirectories)</param>
		/// <returns>Returns true if the directory exists, otherwise false.</returns>
		public Task<bool> ExistsAsync([NotNull] IFdbReadOnlyTransaction trans, [NotNull] IEnumerable<string> path)
		{
			if (trans == null) throw new ArgumentNullException(nameof(trans));
			if (path == null) throw new ArgumentNullException(nameof(path));
			// no reason to disallow checking for the root directory (could be used to check if a directory layer is initialized?)

			var location = ParsePath(path);
			if (location.Count == 0) return Task.FromResult(true);

			return ExistsInternalAsync(trans, location);
		}

		#endregion

		#region List / TryList

		/// <summary>Returns the list of subdirectories of directory at <paramref name="path"/></summary>
		/// <param name="trans">Transaction to use for the operation</param>
		/// <param name="path">Path of the directory to list</param>
		[ItemNotNull]
		public Task<List<string>> ListAsync([NotNull] IFdbReadOnlyTransaction trans, [NotNull] IEnumerable<string> path)
		{
			if (trans == null) throw new ArgumentNullException(nameof(trans));

			return ListInternalAsync(trans, ParsePath(path), throwIfMissing: true);
		}

		/// <summary>Returns the list of subdirectories of the root directory</summary>
		/// <param name="trans">Transaction to use for the operation</param>
		[ItemNotNull]
		public Task<List<string>> ListAsync([NotNull] IFdbReadOnlyTransaction trans)
		{
			if (trans == null) throw new ArgumentNullException(nameof(trans));

			return ListInternalAsync(trans, STuple.Empty, throwIfMissing: true);
		}

		/// <summary>Returns the list of subdirectories of directory at <paramref name="path"/>, if it exists.</summary>
		/// <param name="trans">Transaction to use for the operation</param>
		/// <param name="path">Path of the directory to list</param>
		[ItemCanBeNull]
		public Task<List<string>> TryListAsync([NotNull] IFdbReadOnlyTransaction trans, IEnumerable<string> path)
		{
			if (trans == null) throw new ArgumentNullException(nameof(trans));

			return ListInternalAsync(trans, ParsePath(path), throwIfMissing: false);
		}

		[ItemCanBeNull]
		public Task<List<string>> TryListAsync([NotNull] IFdbReadOnlyTransaction trans)
		{
			if (trans == null) throw new ArgumentNullException(nameof(trans));
			return ListInternalAsync(trans, STuple.Empty, throwIfMissing: false);
		}

		#endregion

		/// <summary>Change the layer id of the directory at <paramref name="path"/></summary>
		/// <param name="trans">Transaction to use for the operation</param>
		/// <param name="path">Path of the directory to change</param>
		/// <param name="newLayer">New layer id of the directory</param>
		[ItemCanBeNull]
		public async Task<FdbDirectorySubspace> ChangeLayerAsync([NotNull] IFdbTransaction trans, [NotNull] IEnumerable<string> path, Slice newLayer)
		{
			if (trans == null) throw new ArgumentNullException(nameof(trans));
			if (path == null) throw new ArgumentNullException(nameof(path));

			var location = ParsePath(path);

			// Set the layer to the new value
			await ChangeLayerInternalAsync(trans, location, newLayer).ConfigureAwait(false);

			// And re-open the directory subspace
			return await CreateOrOpenInternalAsync(null, trans, location, newLayer, prefix: Slice.Nil, allowCreate: false, allowOpen: true, throwOnError: true).ConfigureAwait(false);
		}

		public override string ToString()
		{
			return $"DirectoryLayer(path={this.FullName}, contents={this.ContentSubspace.GetPrefix():K}, nodes={this.NodeSubspace.GetPrefix():K})";
		}

		#endregion

		#region Internal Helpers...

		private readonly struct Node
		{

			public Node(IDynamicKeySubspace subspace, ITuple path, ITuple targetPath, Slice layer)
			{
				this.Subspace = subspace;
				this.Path = path;
				this.TargetPath = targetPath;
				this.Layer = layer;
			}

			public readonly IDynamicKeySubspace Subspace;
			public readonly ITuple Path;
			public readonly ITuple TargetPath;
			public readonly Slice Layer;

			public bool Exists => this.Subspace != null;

			[NotNull]
			public ITuple PartitionSubPath => this.TargetPath.Substring(this.Path.Count);

			public bool IsInPartition(bool includeEmptySubPath)
			{
				return this.Exists && this.Layer == FdbDirectoryPartition.LayerId && (includeEmptySubPath || this.TargetPath.Count > this.Path.Count);
			}

		}

		private static void SetLayer([NotNull] IFdbTransaction trans, [NotNull] IDynamicKeySubspace subspace, Slice layer)
		{
			if (layer.IsNull) layer = Slice.Empty;
			trans.Set(subspace.Keys.Encode(LayerSuffix), layer);
		}

		[NotNull]
		internal static ITuple ParsePath(IEnumerable<string> path, string argName = null)
		{
			if (path == null) return STuple.Empty;

			var pathCopy = path.ToArray();
			foreach (var s in pathCopy)
			{
				if (s == null)
				{
					throw new ArgumentException("The path of a directory cannot contain null elements", argName ?? "path");
				}
			}
			return STuple.FromArray<string>(pathCopy);
		}

		[NotNull]
		internal static ITuple ParsePath([NotNull] string name, string argName = null)
		{
			if (name == null) throw new ArgumentNullException(argName ?? "name");

			return STuple.Create<string>(name);
		}

		[NotNull]
		internal static ITuple VerifyPath([NotNull] ITuple path, string argName = null)
		{
			// The path should not contain any null strings
			if (path == null) throw new ArgumentNullException(argName ?? "path");
			int count = path.Count;
			for (int i = 0; i < count; i++)
			{
				if (path.Get<string>(i) == null)
				{
					throw new ArgumentException("The path of a directory cannot contain null elements", argName ?? "path");
				}
			}
			return path;
		}

		[NotNull]
		internal IReadOnlyList<string> ToAbsolutePath([NotNull] ITuple path)
		{
			if (path.Count == 0) return this.Path;
			var converted = path.ToArray<string>();
			if (this.Path.Count == 0) return converted;
			return this.Path.Concat(converted).ToList();
		}

		/// <summary>Maps an absolute path to a relative path within this directory layer</summary>
		[NotNull]
		internal ITuple ToRelativePath([NotNull] ITuple path)
		{
			if (path == null) throw new ArgumentNullException(nameof(path));

			if (!path.StartsWith(this.Location)) throw new InvalidOperationException("The path cannot be outside of this partition");
			return path.Substring(this.Location.Count);
		}

		[ItemCanBeNull]
		internal async Task<FdbDirectorySubspace> CreateOrOpenInternalAsync(IFdbReadOnlyTransaction readTrans, IFdbTransaction trans, [NotNull] ITuple path, Slice layer, Slice prefix, bool allowCreate, bool allowOpen, bool throwOnError)
		{
			Contract.Requires(readTrans != null || trans != null, "Need at least one transaction");
			Contract.Requires(path != null, "Path must be specified");
			Contract.Requires(readTrans == null || trans == null || object.ReferenceEquals(readTrans, trans), "The write transaction should be the same as the read transaction");

			if (path.Count == 0)
			{ // Root directory contains node metadata and so may not be opened.
				throw new InvalidOperationException("The root directory may not be opened.");
			}

			// to open an existing directory, we only need the read transaction
			// if none was specified, we can use the writeable transaction
			if (readTrans == null) readTrans = trans;

			await CheckReadVersionAsync(readTrans).ConfigureAwait(false);

			if (prefix.HasValue && this.Path.Count > 0)
				throw new InvalidOperationException("Cannot specify a prefix in a partition.");

			var existingNode = await FindAsync(readTrans, path).ConfigureAwait(false);

			if (existingNode.Exists)
			{
				if (existingNode.IsInPartition(false))
				{
					var subpath = existingNode.PartitionSubPath;
					var dl = GetPartitionForNode(existingNode).DirectoryLayer;
					return await dl.CreateOrOpenInternalAsync(readTrans, trans, subpath, layer, prefix, allowCreate, allowOpen, throwOnError).ConfigureAwait(false);
				}

				if (!allowOpen)
				{
					if (throwOnError) throw new InvalidOperationException($"The directory {path} already exists.");
					return null;
				}

				if (layer.IsPresent && layer != existingNode.Layer)
				{
					throw new InvalidOperationException($"The directory {path} was created with incompatible layer {layer:P} instead of expected {existingNode.Layer:P}.");
				}
				return ContentsOfNode(existingNode.Subspace, path, existingNode.Layer);
			}

			if (!allowCreate)
			{
				if (throwOnError) throw new InvalidOperationException($"The directory {path} does not exist.");
				return null;
			}

			// from there, we actually do need a wrtieable transaction
			if (trans == null) throw new InvalidOperationException("A writeable transaction is needed to create a new directory");

			await CheckWriteVersionAsync(trans).ConfigureAwait(false);

			if (prefix.IsNull)
			{ // automatically allocate a new prefix inside the ContentSubspace
				long id = await this.Allocator.AllocateAsync(trans).ConfigureAwait(false);
				prefix = this.ContentSubspace.Keys.Encode(id);

				// ensure that there is no data already present under this prefix
				if (FdbDirectoryLayer.AnnotateTransactions) trans.Annotate("Ensure that there is no data already present under prefix {0}", prefix);
				if (await trans.GetRange(KeyRange.StartsWith(prefix)).AnyAsync().ConfigureAwait(false))
				{
					throw new InvalidOperationException($"The database has keys stored at the prefix chosen by the automatic prefix allocator: {prefix:K}");
				}

				// ensure that the prefix has not already been allocated
				if (FdbDirectoryLayer.AnnotateTransactions) trans.Annotate("Ensure that the prefix {0} has not already been allocated", prefix);
				if (!(await IsPrefixFree(trans.Snapshot, prefix).ConfigureAwait(false)))
				{
					throw new InvalidOperationException("The directory layer has manually allocated prefixes that conflict with the automatic prefix allocator.");
				}
			}
			else
			{
				if (FdbDirectoryLayer.AnnotateTransactions) trans.Annotate("Ensure that the prefix {0} hasn't already been allocated", prefix);
				// ensure that the prefix has not already been allocated
				if (!(await IsPrefixFree(trans, prefix).ConfigureAwait(false)))
				{
					throw new InvalidOperationException("The given prefix is already in use.");
				}
			}

			// we need to recursively create any missing parents
			IDynamicKeySubspace parentNode;
			if (path.Count > 1)
			{
				var parentSubspace = await CreateOrOpenInternalAsync(readTrans, trans, path.Substring(0, path.Count - 1), Slice.Nil, Slice.Nil, true, true, true).ConfigureAwait(false);
				parentNode = NodeWithPrefix(parentSubspace.GetPrefix());
			}
			else
			{
				parentNode = this.RootNode;
			}
			if (parentNode == null) throw new InvalidOperationException($"The parent directory of {path} doesn't exist.");

			// initialize the metadata for this new directory
			var node = NodeWithPrefix(prefix);
			if (FdbDirectoryLayer.AnnotateTransactions) trans.Annotate("Registering the new prefix {0} into the folder sub-tree", prefix);
			trans.Set(GetSubDirKey(parentNode, path.Get<string>(-1)), prefix);
			SetLayer(trans, node, layer);

			return ContentsOfNode(node, path, layer);
		}

		[ItemCanBeNull]
		internal async Task<FdbDirectorySubspace> MoveInternalAsync([NotNull] IFdbTransaction trans, [NotNull] ITuple oldPath, [NotNull] ITuple newPath, bool throwOnError)
		{
			Contract.Requires(trans != null && oldPath != null && newPath != null);

			if (oldPath.Count == 0)
			{
				throw new InvalidOperationException("The root directory may not be moved.");
			}
			if (newPath.Count == 0)
			{
				throw new InvalidOperationException("The root directory cannot be overwritten.");
			}
			if (newPath.StartsWith(oldPath))
			{
				throw new InvalidOperationException($"The destination directory({newPath}) cannot be a subdirectory of the source directory({oldPath}).");
			}

			await CheckWriteVersionAsync(trans).ConfigureAwait(false);

			var oldNode = await FindAsync(trans, oldPath).ConfigureAwait(false);
			if (!oldNode.Exists)
			{
				if (throwOnError) throw new InvalidOperationException($"The source directory '{oldPath}' does not exist.");
				return null;
			}

			var newNode = await FindAsync(trans, newPath).ConfigureAwait(false);

			// we have already checked that old and new are under this partition path, but one of them (or both?) could be under a sub-partition..
			if (oldNode.IsInPartition(false) || newNode.IsInPartition(false))
			{
				if (!oldNode.IsInPartition(false) || !newNode.IsInPartition(false) || !STuple.Equals(oldNode.Path, newNode.Path))
				{
					throw new InvalidOperationException("Cannot move between partitions.");
				}
				// both nodes are in the same sub-partition, delegate to it
				return await GetPartitionForNode(newNode).DirectoryLayer.MoveInternalAsync(trans, oldNode.PartitionSubPath, newNode.PartitionSubPath, throwOnError).ConfigureAwait(false);
			}

			if (newNode.Exists)
			{
				if (throwOnError) throw new InvalidOperationException($"The destination directory '{newPath}' already exists. Remove it first.");
				return null;
			}

			var parentNode = await FindAsync(trans, newPath.Substring(0, newPath.Count - 1)).ConfigureAwait(false);
			if (!parentNode.Exists)
			{
				if (throwOnError) throw new InvalidOperationException($"The parent of the destination directory '{newPath}' does not exist. Create it first.");
				return null;
			}

			if (FdbDirectoryLayer.AnnotateTransactions) trans.Annotate("Register the prefix {0} to its new location in the folder sub-tree", oldNode.Subspace.GetPrefix());
			trans.Set(GetSubDirKey(parentNode.Subspace, newPath.Get<string>(-1)), this.NodeSubspace.Keys.Decode<Slice>(oldNode.Subspace.GetPrefix()));
			await RemoveFromParent(trans, oldPath).ConfigureAwait(false);

			return ContentsOfNode(oldNode.Subspace, newPath, oldNode.Layer);
		}

		internal async Task<bool> RemoveInternalAsync([NotNull] IFdbTransaction trans, [NotNull] ITuple path, bool throwIfMissing)
		{
			Contract.Requires(trans != null && path != null);

			// We don't allow removing the root directory, because it would probably end up wiping out all the database.
			if (path.Count == 0) throw new InvalidOperationException("The root directory may not be removed.");

			await CheckWriteVersionAsync(trans).ConfigureAwait(false);

			var n = await FindAsync(trans, path).ConfigureAwait(false);
			if (!n.Exists)
			{
				if (throwIfMissing) throw new InvalidOperationException($"The directory '{path}' does not exist.");
				return false;
			}

			if (n.IsInPartition(includeEmptySubPath: false))
			{
				return await GetPartitionForNode(n).DirectoryLayer.RemoveInternalAsync(trans, n.PartitionSubPath, throwIfMissing).ConfigureAwait(false);
			}

			//TODO: partitions ?

			// Delete the node subtree and all the data
			await RemoveRecursive(trans, n.Subspace).ConfigureAwait(false);
			// Remove the node from the tree
			await RemoveFromParent(trans, path).ConfigureAwait(false);
			return true;
		}

		[ItemCanBeNull]
		internal async Task<List<string>> ListInternalAsync([NotNull] IFdbReadOnlyTransaction trans, [NotNull] ITuple path, bool throwIfMissing)
		{
			Contract.Requires(trans != null && path != null);

			await CheckReadVersionAsync(trans).ConfigureAwait(false);

			var node = await FindAsync(trans, path).ConfigureAwait(false);

			if (!node.Exists)
			{
				if (throwIfMissing) throw new InvalidOperationException($"The directory '{path}' does not exist.");
				return null;
			}

			if (node.IsInPartition(includeEmptySubPath: true))
			{
				return await GetPartitionForNode(node).DirectoryLayer.ListInternalAsync(trans, node.PartitionSubPath, throwIfMissing).ConfigureAwait(false);
			}

			return await SubdirNamesAndNodes(trans, node.Subspace)
				.Select(kvp => kvp.Key)
				.ToListAsync()
				.ConfigureAwait(false);
		}

		internal async Task<bool> ExistsInternalAsync([NotNull] IFdbReadOnlyTransaction trans, [NotNull] ITuple path)
		{
			Contract.Requires(trans != null && path != null);

			await CheckReadVersionAsync(trans).ConfigureAwait(false);

			var node = await FindAsync(trans, path).ConfigureAwait(false);

			if (!node.Exists) return false;

			if (node.IsInPartition(includeEmptySubPath: false))
			{
				return await GetPartitionForNode(node).DirectoryLayer.ExistsInternalAsync(trans, node.PartitionSubPath).ConfigureAwait(false);
			}

			return true;
		}

		internal async Task ChangeLayerInternalAsync([NotNull] IFdbTransaction trans, [NotNull] ITuple path, Slice newLayer)
		{
			Contract.Requires(trans != null && path != null);

			await CheckWriteVersionAsync(trans).ConfigureAwait(false);

			var node = await FindAsync(trans, path).ConfigureAwait(false);

			if (!node.Exists)
			{
				throw new InvalidOperationException($"The directory '{path}' does not exist, or as already been removed.");
			}

			if (node.IsInPartition(includeEmptySubPath: false))
			{
				await GetPartitionForNode(node).DirectoryLayer.ChangeLayerInternalAsync(trans, node.PartitionSubPath, newLayer).ConfigureAwait(false);
				return;
			}

			SetLayer(trans, node.Subspace, newLayer);
		}

		private async Task CheckReadVersionAsync([NotNull] IFdbReadOnlyTransaction trans)
		{
			Contract.Requires(trans != null);

			var value = await trans.GetAsync(this.RootNode.Keys.Encode(VersionKey)).ConfigureAwait(false);
			if (!value.IsNullOrEmpty)
			{
				CheckVersion(value, false);
			}
		}

		private async Task CheckWriteVersionAsync([NotNull] IFdbTransaction trans)
		{
			Contract.Requires(trans != null);

			var value = await trans.GetAsync(this.RootNode.Keys.Encode(VersionKey)).ConfigureAwait(false);
			if (value.IsNullOrEmpty)
			{
				InitializeDirectory(trans);
			}
			else
			{
				CheckVersion(value, true);
			}
		}

		private static void CheckVersion(Slice value, bool writeAccess)
		{
			// the version is stored as 3 x 32-bit unsigned ints, so (1, 0, 0) will be "<01><00><00><00> <00><00><00><00> <00><00><00><00>"
			var reader = new SliceReader(value);
			var major = reader.ReadFixed32();
			var minor = reader.ReadFixed32();
			var upgrade = reader.ReadFixed32();

			if (major > LayerVersion.Major) throw new InvalidOperationException($"Cannot load directory with version {major}.{minor}.{upgrade} using directory layer {FdbDirectoryLayer.LayerVersion}");
			if (writeAccess && minor > LayerVersion.Minor) throw new InvalidOperationException($"Directory with version {major}.{minor}.{upgrade} is read-only when opened using directory layer {FdbDirectoryLayer.LayerVersion}");
		}

		private void InitializeDirectory([NotNull] IFdbTransaction trans)
		{
			Contract.Requires(trans != null);

			// Set the version key
			var writer = new SliceWriter(3 * 4);
			writer.WriteFixed32((uint)LayerVersion.Major);
			writer.WriteFixed32((uint)LayerVersion.Minor);
			writer.WriteFixed32((uint)LayerVersion.Build);
			trans.Set(this.RootNode.Keys.Encode(VersionKey), writer.ToSlice());
		}

		[ItemCanBeNull]
		private async Task<IKeySubspace> NodeContainingKey([NotNull] IFdbReadOnlyTransaction tr, Slice key)
		{
			Contract.Requires(tr != null);

			// Right now this is only used for _is_prefix_free(), but if we add
			// parent pointers to directory nodes, it could also be used to find a
			// path based on a key.

			if (this.NodeSubspace.Contains(key))
				return this.RootNode;

			var kvp = await tr
				.GetRange(
					this.NodeSubspace.Keys.ToRange().Begin,
					this.NodeSubspace.Keys.Encode(key) + FdbKey.MinValue
				)
				.LastOrDefaultAsync()
				.ConfigureAwait(false);

			if (kvp.Key.HasValue)
			{
				var prevPrefix = this.NodeSubspace.Keys.DecodeFirst<Slice>(kvp.Key);
				if (key.StartsWith(prevPrefix))
				{
					return NodeWithPrefix(prevPrefix);
				}
			}

			return null;
		}

		/// <summary>Returns the subspace to a node metadata, given its prefix</summary>
		[CanBeNull]
		private IDynamicKeySubspace NodeWithPrefix(Slice prefix)
		{
			if (prefix.IsNullOrEmpty) return null;
			return this.NodeSubspace.Partition.ByKey(prefix);
		}

		/// <summary>Returns a new Directory Subspace given its node subspace, path and layer id</summary>
		[NotNull]
		private FdbDirectorySubspace ContentsOfNode([NotNull] IKeySubspace node, [NotNull] ITuple relativePath, Slice layer)
		{
			Contract.Requires(node != null);

			var path = this.Location.Concat(relativePath);
			var prefix = this.NodeSubspace.Keys.Decode<Slice>(node.GetPrefix());
			if (layer == FdbDirectoryPartition.LayerId)
			{
				return new FdbDirectoryPartition(path, relativePath, prefix, this);
			}
			else
			{
				return new FdbDirectorySubspace(path, relativePath, prefix, this, layer, TypeSystem.Default);
			}
		}

		[NotNull]
		private FdbDirectoryPartition GetPartitionForNode(Node node)
		{
			Contract.Requires(node.Subspace != null && node.Path != null && FdbDirectoryPartition.LayerId.Equals(node.Layer));
			return (FdbDirectoryPartition) ContentsOfNode(node.Subspace, node.Path, node.Layer);
		}

		/// <summary>Finds a node subspace, given its path, by walking the tree from the root.</summary>
		/// <returns>Node if it was found, or null</returns>
		private async Task<Node> FindAsync([NotNull] IFdbReadOnlyTransaction tr, [NotNull] ITuple path)
		{
			Contract.Requires(tr != null && path != null);

			// look for the node by traversing from the root down. Stop when crossing a partition...

			var n = this.RootNode;
			int i = 0;
			Slice layer = Slice.Nil;
			while (i < path.Count)
			{
				if (FdbDirectoryLayer.AnnotateTransactions) tr.Annotate("Looking for child {0} under node {1}...", path.Get<string>(i), n.GetPrefix());
				n = NodeWithPrefix(await tr.GetAsync(GetSubDirKey(n, path.Get<string>(i))).ConfigureAwait(false));
				if (n == null)
				{
					return new Node(null, path.Substring(0, i + 1), path, Slice.Empty);
				}

				if (FdbDirectoryLayer.AnnotateTransactions) tr.Annotate("Reading Layer value for subfolder {0} found at {1}", path, n.GetPrefix());
				layer = await tr.GetAsync(n.Keys.Encode(LayerSuffix)).ConfigureAwait(false);
				if (layer == FdbDirectoryPartition.LayerId)
				{ // stop when reaching a partition
					return new Node(n, path.Substring(0, i + 1), path, FdbDirectoryPartition.LayerId);
				}

				++i;
			}
			return new Node(n, path, path, layer);
		}

		/// <summary>Returns the list of names and nodes of all children of the specified node</summary>
		[NotNull]
		private IAsyncEnumerable<KeyValuePair<string, IDynamicKeySubspace>> SubdirNamesAndNodes([NotNull] IFdbReadOnlyTransaction tr, [NotNull] IDynamicKeySubspace node)
		{
			Contract.Requires(tr != null && node != null);

			var sd = node.Partition.ByKey(SUBDIRS);
			return tr
				.GetRange(sd.Keys.ToRange())
				.Select(kvp => new KeyValuePair<string, IDynamicKeySubspace>(
					sd.Keys.Decode<string>(kvp.Key),
					NodeWithPrefix(kvp.Value)
				));
		}

		/// <summary>Remove an existing node from its parents</summary>
		/// <returns>True if the parent node was found, otherwise false</returns>
		private async Task<bool> RemoveFromParent([NotNull] IFdbTransaction tr, [NotNull] ITuple path)
		{
			Contract.Requires(tr != null && path != null);

			var parent = await FindAsync(tr, path.Substring(0, path.Count - 1)).ConfigureAwait(false);
			if (parent.Exists)
			{
				if (FdbDirectoryLayer.AnnotateTransactions) tr.Annotate("Removing path {0} from its parent folder at {1}", path, parent.Subspace.GetPrefix());
				tr.Clear(GetSubDirKey(parent.Subspace, path.Get<string>(-1)));
				return true;
			}
			return false;
		}

		/// <summary>Resursively remove a node (including the content), all its children</summary>
		private async Task RemoveRecursive([NotNull] IFdbTransaction tr, [NotNull] IDynamicKeySubspace node)
		{
			Contract.Requires(tr != null && node != null);

			//note: we could use Task.WhenAll to remove the children, but there is a risk of task explosion if the subtree is very large...
			await SubdirNamesAndNodes(tr, node).ForEachAsync((kvp) => RemoveRecursive(tr, kvp.Value)).ConfigureAwait(false);

			// remove ALL the contents
			if (FdbDirectoryLayer.AnnotateTransactions) tr.Annotate("Removing all content located under {0}", node.GetPrefix());
			tr.ClearRange(ContentsOfNode(node, STuple.Empty, Slice.Empty).ToRange());
			// and all the metadata for this folder
			if (FdbDirectoryLayer.AnnotateTransactions) tr.Annotate("Removing all metadata for folder under {0}", node.GetPrefix());
			tr.ClearRange(node.Keys.ToRange());
		}

		private async Task<bool> IsPrefixFree([NotNull] IFdbReadOnlyTransaction tr, Slice prefix)
		{
			Contract.Requires(tr != null);

			// Returns true if the given prefix does not "intersect" any currently
			// allocated prefix (including the root node). This means that it neither
			// contains any other prefix nor is contained by any other prefix.

			if (prefix.IsNullOrEmpty) return false;
			if (await NodeContainingKey(tr, prefix).ConfigureAwait(false) != null) return false;

			return await tr
				.GetRange(
					this.NodeSubspace.Keys.Encode(prefix),
					this.NodeSubspace.Keys.Encode(FdbKey.Increment(prefix))
				)
				.NoneAsync()
				.ConfigureAwait(false);
		}

		private static Slice GetSubDirKey([NotNull] IDynamicKeySubspace parent, [NotNull] string path)
		{
			Contract.Requires(parent != null && path != null);

			// for a path equal to ("foo","bar","baz") and index = -1, we need to generate (parent, SUBDIRS, "baz")
			// but since the last item of path can be of any type, we will use tuple splicing to copy the last item without changing its type
			return parent.Keys.Encode(SUBDIRS, path);
		}

		#endregion

		#region Path Utils...

		/// <summary>Convert a tuple representing a path, into a string array</summary>
		/// <param name="path">Tuple that should only contain strings</param>
		/// <returns>Array of strings</returns>
		[NotNull]
		public static string[] ParsePath([NotNull] ITuple path)
		{
			if (path == null) throw new ArgumentNullException(nameof(path));
			var tmp = new string[path.Count];
			for (int i = 0; i < tmp.Length; i++)
			{
				tmp[i] = path.Get<string>(i);
			}
			return tmp;
		}

		[NotNull]
		public static string[] Combine([NotNull] IEnumerable<string> parent, string path)
		{
			if (parent == null) throw new ArgumentNullException(nameof(parent));
			return parent.Concat(new[] { path }).ToArray();
		}

		[NotNull]
		public static string[] Combine(IEnumerable<string> parent, params string[] paths)
		{
			if (parent == null) throw new ArgumentNullException(nameof(parent));
			if (paths == null) throw new ArgumentNullException(nameof(paths));
			return parent.Concat(paths).ToArray();
		}

		[NotNull]
		public static string[] Combine([NotNull] IEnumerable<string> parent, [NotNull] IEnumerable<string> paths)
		{
			if (parent == null) throw new ArgumentNullException(nameof(parent));
			if (paths == null) throw new ArgumentNullException(nameof(paths));
			return parent.Concat(paths).ToArray();
		}

		[NotNull]
		public static string[] Parse(string path)
		{
			if (string.IsNullOrEmpty(path)) return new string[0];

			var paths = new List<string>();
			var sb = new System.Text.StringBuilder();
			bool escaped = false;
			foreach(var c in path)
			{
				if (escaped)
				{
					escaped = false;
					sb.Append(c);
					continue;
				}

				switch (c)
				{
					case '\\':
					{
						escaped = true;
						continue;
					}
					case '/':
					{
						if (sb.Length == 0 && paths.Count == 0)
						{ // ignore the first '/'
							continue;
						}
						paths.Add(sb.ToString());
						sb.Clear();
						break;
					}
					default:
					{
						sb.Append(c);
						break;
					}
				}
			}
			if (sb.Length > 0)
			{
				paths.Add(sb.ToString());
			}
			return paths.ToArray();
		}

		[NotNull]
		public static string FormatPath([NotNull] IEnumerable<string> paths)
		{
			if (paths == null) throw new ArgumentNullException(nameof(paths));

			return String.Join("/", paths.Select(path =>
			{
				if (path.Contains('\\') || path.Contains('/'))
				{
					return path.Replace("\\", "\\\\").Replace("/", "\\/");
				}
				else
				{
					return path;
				}
			}));

		}

		#endregion

	}

}
