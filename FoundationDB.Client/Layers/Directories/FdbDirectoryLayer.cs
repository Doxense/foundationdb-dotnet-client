#region BSD Licence
/* Copyright (c) 2013, Doxense SARL
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
	using FoundationDB.Client;
	using FoundationDB.Layers.Tuples;
	using FoundationDB.Linq;
	using System;
	using System.Collections.Generic;
	using System.Diagnostics;
	using System.Threading;
	using System.Threading.Tasks;

	/// <summary>Provides a FdbDirectoryLayer class for managing directories in FoundationDB.
	/// Directories are a recommended approach for administering layers and applications. Directories work in conjunction with subspaces. Each layer or application should create or open at least one directory with which to manage its subspace(s).
	/// Directories are identified by paths (specified as tuples) analogous to the paths in a Unix-like file system. Each directory has an associated subspace that is used to store content. The layer uses a high-contention allocator to efficiently map each path to a short prefix for its corresponding subspace.
	/// <see cref="FdbDirectorLayer"/> exposes methods to create, open, move, remove, or list directories. Creating or opening a directory returns the corresponding subspace.
	/// The <see cref="FdbDirectorySubspace"/> class represents subspaces that store the contents of a directory. An instance of <see cref="FdbDirectorySubspace"/> can be used for all the usual subspace operations. It can also be used to operate on the directory with which it was opened.
	/// </summary>
	[DebuggerDisplay("Nodes={this.NodeSubspace}, Contents={this.ContentsSubspace}")]
	public class FdbDirectoryLayer
	{
		private const int SUBDIRS = 0;
		private static readonly Slice LayerSuffix = Slice.FromAscii("layer");
		private static readonly Slice HcaKey = Slice.FromAscii("hca");

		/// <summary>Subspace where the content of each folder will be stored</summary>
		public FdbSubspace ContentSubspace { get; private set; }

		/// <summary>Subspace where all the metadata nodes for each folder will be stored</summary>
		public FdbSubspace NodeSubspace { get; private set; }

		/// <summary>Root node of the directory</summary>
		internal FdbSubspace RootNode { get; private set; }

		/// <summary>Allocated used to generated prefix for new content</summary>
		internal FdbHighContentionAllocator Allocator { get; private set; }

		#region Constructors...

		/// <summary>
		/// Creates a new instance that will manages directories in FoudnationDB.
		/// </summary>
		/// <param name="nodeSubspace">Subspace where all the node metadata will be stored ('\xFE' by default)</param>
		/// <param name="contentSubspace">Subspace where all automatically allocated directories will be stored (empty by default)</param>
		public FdbDirectoryLayer(FdbSubspace nodeSubspace = null, FdbSubspace contentSubspace = null)
		{
			if (nodeSubspace == null) nodeSubspace = new FdbSubspace(FdbKey.Directory);
			if (contentSubspace == null) contentSubspace = FdbSubspace.Empty;

			// If specified, new automatically allocated prefixes will all fall within content_subspace
			this.ContentSubspace = contentSubspace;
			this.NodeSubspace = nodeSubspace;

			// The root node is the one whose contents are the node subspace
			this.RootNode = nodeSubspace.Partition(nodeSubspace.Key);
			this.Allocator = new FdbHighContentionAllocator(nodeSubspace.Partition(HcaKey));
		}

		#endregion

		#region Public Methods...

		/// <summary>
		/// Opens the directory with the given path. If the directory does not exist, it is created (creating parent directories if necessary).
		/// </summary>
		/// <param name="tr">Transaction to use for the operation</param>
		/// <param name="path">Path of the directory to create or open</param>
		/// <param name="layer">If layer is specified, it is checked against the layer of an existing directory or set as the layer of a new directory.</param>
		/// <param name="prefix">If a prefix is specified, the directory is created with the given physical prefix; otherwise a prefix is allocated automatically inside then Content subspace.</param>
		/// <param name="allowCreate">If the directory does not exist, it will be created if <paramref name="allowCreate"/> is true, or an exception will be thrown if it is false</param>
		/// <param name="allowOpen">If the directory already exists, it will be opened if <paramref name="allowOpened"/> is true, or an exception will be thrown if it is false</param>
		/// <param name="ct">Token used to abort this operation</param>
		public async Task<FdbDirectorySubspace> CreateOrOpenAsync(IFdbTransaction tr, IFdbTuple path, string layer = null, Slice prefix = default(Slice), bool allowCreate = true, bool allowOpen = true, CancellationToken ct = default(CancellationToken))
		{
			if (tr == null) throw new ArgumentNullException("tr");
			if (path == null) throw new ArgumentNullException("path");

			ct.ThrowIfCancellationRequested();

			if (path.Count == 0)
			{ // Root directory contains node metadata and so may not be opened.
				throw new ArgumentException("path", "The root directory may not be opened.");
			}

			var existingNode = await Find(tr, path, ct).ConfigureAwait(false);

			if (existingNode != null)
			{
				if (!allowOpen) throw new InvalidOperationException("The directory already exists.");

				var existingLayer = (await tr.GetAsync(existingNode.Partition(LayerSuffix).Key).ConfigureAwait(false)).ToUnicode();
				if (!string.IsNullOrEmpty(layer) && layer != existingLayer)
				{
					throw new InvalidOperationException("The directory exists but was created with an incompatible layer.");
				}

				return ContentsOfNode(existingNode, path, existingLayer);
			}

			if (!allowCreate) throw new InvalidOperationException("The directory does not exist.");

			if (prefix.IsNullOrEmpty)
			{ // automatically allocate a new prefix inside the ContentSubspace
				long id = await this.Allocator.AllocateAsync(tr).ConfigureAwait(false);
				prefix = this.ContentSubspace.Pack(id);
			}

			if (!(await IsPrefixFree(tr, prefix, ct).ConfigureAwait(false)))
			{
				throw new InvalidOperationException("The given prefix is already in use.");
			}

			// we need to recursively create any missing parents
			FdbSubspace parentNode;
			if (path.Count > 1)
			{
				var parentSubspace = await CreateOrOpenAsync(tr, path.Substring(0, path.Count - 1), ct: ct).ConfigureAwait(false);
				parentNode = NodeWithPrefix(parentSubspace.Key);
			}
			else
			{
				parentNode = this.RootNode;
			}
			if (parentNode == null) throw new InvalidOperationException("The parent directory doesn't exist.");

			// initialize the metadata for this new directory
			var node = NodeWithPrefix(prefix);
			tr.Set(GetSubDirKey(parentNode, path, -1), prefix);
			if (!string.IsNullOrEmpty(layer))
			{
				//note: we are using UTF-8 but layer authors should maybe refrain from using non-ASCII text ?
				tr.Set(node.Pack(LayerSuffix), Slice.FromString(layer));
			}

			return ContentsOfNode(node, path, layer);
		}

		/// <summary>
		/// Opens the directory with the given <paramref name="path"/>.
		/// An exception is thrown if the directory does not exist, or if a layer is specified and a different layer was specified when the directory was created.
		/// </summary>
		/// <param name="tr">Transaction to use for the operation</param>
		/// <param name="path">Path of the directory to open.</param>
		/// <param name="layer">Optional layer id of the directory. If it is different than the layer specified when creating the directory, an exception will be thrown.</param>
		/// <param name="ct">Token used to abort this operation</param>
		public Task<FdbDirectorySubspace> OpenAsync(IFdbTransaction tr, IFdbTuple path, string layer = null, CancellationToken ct = default(CancellationToken))
		{
			return CreateOrOpenAsync(tr, path, layer, prefix: Slice.Nil, allowCreate: false, allowOpen: true, ct: ct);
		}

		/// <summary>
		/// Creates a directory with the given <paramref name="path"/> (creating parent directories if necessary).
		/// An exception is thrown if the given directory already exists.
		/// </summary>
		/// <param name="tr">Transaction to use for the operation</param>
		/// <param name="path">Path of the directory to create</param>
		/// <param name="layer">If <paramref name="layer"/> is specified, it is recorded with the directory and will be checked by future calls to open.</param>
		/// <param name="prefix">If <paramref name="prefix"/> is specified, the directory is created with the given physical prefix; otherwise a prefix is allocated automatically.</param>
		/// <param name="ct">Token used to abort this operation</param>
		public Task<FdbDirectorySubspace> CreateAsync(IFdbTransaction tr, IFdbTuple path, string layer = null, Slice prefix = default(Slice), CancellationToken ct = default(CancellationToken))
		{
			return CreateOrOpenAsync(tr, path, layer, prefix: prefix, allowCreate: true, allowOpen: false, ct: ct);
		}

		/// <summary>
		/// Moves the directory found at <paramref name="oldPath"/> to <paramref name="newPath"/>.
		/// There is no effect on the physical prefix of the given directory, or on clients that already have the directory open.
        /// An error is raised if the old directory does not exist, a directory already exists at `new_path`, or the parent directory of `new_path` does not exist.
		/// </summary>
		/// <param name="tr">Transaction to use for the operation</param>
		/// <param name="oldPath">Path of the directory to move</param>
		/// <param name="newPath">New path of the directory</param>
		/// <param name="ct">Token used to abort this operation</param>
		public async Task<FdbDirectorySubspace> MoveAsync(IFdbTransaction tr, IFdbTuple oldPath, IFdbTuple newPath, CancellationToken ct = default(CancellationToken))
		{
			if (tr == null) throw new ArgumentNullException("tr");
			if (oldPath == null) throw new ArgumentNullException("oldPath");
			if (newPath == null) throw new ArgumentNullException("newPath");

			ct.ThrowIfCancellationRequested();

			//REVIEW: if oldPath == newPath, the exception message below will be misleading. Should we no-op or throw in this case ?
			if ((await Find(tr, newPath, ct).ConfigureAwait(false)) != null)
			{
				throw new InvalidOperationException("The destination directory already exists. Remove it first.");
			}

			var oldNode = await Find(tr, oldPath, ct).ConfigureAwait(false);
			if (oldNode == null)
			{
				throw new InvalidOperationException("The source directory dos not exist.");
			}

			var parentNode = await Find(tr, newPath.Substring(0, newPath.Count - 1), ct).ConfigureAwait(false);
			if (parentNode == null)
			{
				throw new InvalidOperationException("The parent of the destination directory does not exist. Create it first.");
			}

			tr.Set(GetSubDirKey(parentNode, newPath, -1), ContentsOfNode(oldNode, null, null).Key);
			await RemoveFromParent(tr, oldPath, ct).ConfigureAwait(false);

			var k = await tr.GetAsync(oldNode.Pack(LayerSuffix)).ConfigureAwait(false);
			return ContentsOfNode(oldNode, newPath, k.ToUnicode());
		}

		/// <summary>
		/// Removes the directory, its contents, and all subdirectories.
		/// Warning: Clients that have already opened the directory might still insert data into its contents after it is removed.
		/// </summary>
		/// <param name="tr">Transaction to use for the operation</param>
		/// <param name="path">Path of the directory to remove (including any subdirectories)</param>
		/// <param name="ct">Token used to abort this operation</param>
		public async Task<bool> RemoveAsync(IFdbTransaction tr, IFdbTuple path, CancellationToken ct = default(CancellationToken))
		{
			if (tr == null) throw new ArgumentNullException("tr");

			ct.ThrowIfCancellationRequested();

			var n = await Find(tr, path, ct).ConfigureAwait(false);
			if (n == null)
			{
				//throw new InvalidOperationException("The directory doesn't exist.");
				return false;
			}

			await RemoveRecursive(tr, n, ct).ConfigureAwait(false);
			await RemoveFromParent(tr, path, ct).ConfigureAwait(false);
			return true;
		}

		/// <summary>
		/// Returns the list of subdirectories of directory at <paramref name="path"/>
		/// </summary>
		/// <param name="tr">Transaction to use for the operation</param>
		/// <param name="path">Path of the directory to list</param>
		/// <param name="ct">Token used to abort this operation</param>
		public async Task<List<IFdbTuple>> ListAsync(IFdbReadTransaction tr, IFdbTuple path = null, CancellationToken ct = default(CancellationToken))
		{
			if (tr == null) throw new ArgumentNullException("tr");

			ct.ThrowIfCancellationRequested();

			path = path ?? FdbTuple.Empty;

			var node = await Find(tr, path, ct).ConfigureAwait(false);

			if (node == null)
			{
				//REVIEW: the python layer throws an execption in this case, but maybe we should return null ? There is currently no way to test if a folder exist because OpenAsync/ListAsync both throw if the folder does not exist...
				throw new InvalidOperationException("The given directory does not exist.");
			}

			return await SubdirNamesAndNodes(tr, node)
				.Select(kvp => kvp.Key)
				.ToListAsync()
				.ConfigureAwait(false);
		}

		#endregion

		#region Internal Helpers...

		private async Task<FdbSubspace> NodeContainingKey(IFdbReadTransaction tr, Slice key, CancellationToken ct)
		{
			// Right now this is only used for _is_prefix_free(), but if we add
			// parent pointers to directory nodes, it could also be used to find a
			// path based on a key.

			if (this.NodeSubspace.Contains(key))
				return this.RootNode;

			var kvp = await tr
				.GetRange(
					this.NodeSubspace.ToRange().Begin,
					this.NodeSubspace.Pack(FdbTuple.Pack(key)) + FdbKey.MinValue
				)
				.LastOrDefaultAsync(ct)
				.ConfigureAwait(false);

			var k = this.NodeSubspace.Unpack(kvp.Key);
			if (kvp.Key.HasValue && key.StartsWith(this.NodeSubspace.UnpackLast<Slice>(kvp.Key)))
			{
				return new FdbSubspace(k);
			}

			return null;
		}

		/// <summary>Returns the subspace to a node metadata, given its prefix</summary>
		private FdbSubspace NodeWithPrefix(Slice prefix)
		{
			if (prefix.IsNullOrEmpty) return null;
			return this.NodeSubspace.Partition(prefix);
		}

		/// <summary>Returns a new Directory Subspace given its node subspace, path and layer id</summary>
		private FdbDirectorySubspace ContentsOfNode(FdbSubspace node, IFdbTuple path, string layer)
		{
			var prefix = FdbTuple.Unpack(this.NodeSubspace.UnpackLast<Slice>(node.Key));
			return new FdbDirectorySubspace(path, prefix, this, layer);
		}

		/// <summary>Finds a node subspace, given its path, by walking the tree from the root</summary>
		/// <returns>Node if it was found, or null</returns>
		private async Task<FdbSubspace> Find(IFdbReadTransaction tr, IFdbTuple path, CancellationToken ct)
		{
			var n = this.RootNode;
			for (int i = 0; i < path.Count; i++)
			{
				n = NodeWithPrefix(await tr.GetAsync(GetSubDirKey(n, path, i)).ConfigureAwait(false));
				if (n == null) return null;
			}
			return n;
		}

		/// <summary>Returns the list of names and nodes of all children of the specified node</summary>
		private IFdbAsyncEnumerable<KeyValuePair<IFdbTuple, FdbSubspace>> SubdirNamesAndNodes(IFdbReadTransaction tr, FdbSubspace node)
		{
			var sd = node.Partition(SUBDIRS);
			return tr
				.GetRange(sd.ToRange())
				.Select(kvp => new KeyValuePair<IFdbTuple, FdbSubspace>(
					sd.Unpack(kvp.Key),
					NodeWithPrefix(kvp.Value)
				));
		}

		/// <summary>Remove an existing node from its parents</summary>
		/// <returns>True if the parent node was found, otherwise false</returns>
		private async Task<bool> RemoveFromParent(IFdbTransaction tr, IFdbTuple path, CancellationToken ct)
		{
			var parent = await Find(tr, path.Substring(0, path.Count - 1), ct).ConfigureAwait(false);
			if (parent != null)
			{
				tr.Clear(GetSubDirKey(parent, path, -1));
				return true;
			}
			return false;
		}

		/// <summary>Resursively remove a node and all its children</summary>
		/// <param name="tr"></param>
		/// <param name="node"></param>
		/// <param name="ct"></param>
		/// <returns></returns>
		private async Task RemoveRecursive(IFdbTransaction tr, FdbSubspace node, CancellationToken ct)
		{
			//note: we could use Task.WhenAll to remove the children, but there is a risk of task explosion if the subtree is very large...
			await SubdirNamesAndNodes(tr, node).ForEachAsync((kvp) => RemoveRecursive(tr, kvp.Value, ct)).ConfigureAwait(false);

			tr.ClearRange(FdbKeyRange.StartsWith(ContentsOfNode(node, FdbTuple.Empty, null).Key));
			tr.ClearRange(node.ToRange());
		}

		private async Task<bool> IsPrefixFree(IFdbReadTransaction tr, Slice prefix, CancellationToken ct)
		{
			// Returns true if the given prefix does not "intersect" any currently
			// allocated prefix (including the root node). This means that it neither
			// contains any other prefix nor is contained by any other prefix.

			if (prefix.IsNullOrEmpty) return false;
			if (await NodeContainingKey(tr, prefix, ct).ConfigureAwait(false) != null) return false;

			return await tr
				.GetRange(
					this.NodeSubspace.Pack(FdbTuple.Pack(prefix)),
					this.NodeSubspace.Pack(FdbTuple.Pack(FdbKey.Increment(prefix)))
				)
				.NoneAsync()
				.ConfigureAwait(false);
		}

		private static Slice GetSubDirKey(FdbSubspace parent, IFdbTuple path, int index)
		{
			// for a path equal to ("foo","bar","baz") and index = -1, we need to generate (parent, SUBDIRS, "baz")
			// but since the last item of path can be of any type, we will use tuple splicing to copy the last item without changing its type

			return parent.Create(SUBDIRS).Concat(path.Substring(index, 1)).ToSlice();
		}

		#endregion

	}

}
