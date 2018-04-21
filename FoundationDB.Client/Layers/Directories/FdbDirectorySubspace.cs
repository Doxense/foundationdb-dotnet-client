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
	using System.Threading.Tasks;
	using Doxense.Diagnostics.Contracts;
	using FoundationDB.Client;
	using FoundationDB.Layers.Tuples;
	using JetBrains.Annotations;

	/// <summary>A Directory Subspace represents the contents of a directory, but it also remembers the path with which it was opened and offers convenience methods to operate on the directory at that path.</summary>
	/// <remarks>An instance of DirectorySubspace can be used for all the usual subspace operations. It can also be used to operate on the directory with which it was opened.</remarks>
	[DebuggerDisplay("Path={this.FullName}, Prefix={InternalKey}, Layer={Layer}")]
	public class FdbDirectorySubspace : DynamicKeySubspace, IFdbDirectory
	{

		internal FdbDirectorySubspace(ITuple location, ITuple relativeLocation, Slice prefix, FdbDirectoryLayer directoryLayer, Slice layer, IDynamicKeyEncoder encoder)
			: base(prefix, encoder)
		{
			Contract.Requires(location != null && relativeLocation != null && prefix != null && directoryLayer != null);
			if (layer.IsNull) layer = Slice.Empty;

			this.DirectoryLayer = directoryLayer;
			this.Location = location;
			this.RelativeLocation = relativeLocation;
			this.Layer = layer;
			this.Path = location.ToArray<string>();

			Contract.Ensures(this.DirectoryLayer != null && this.Location != null && this.RelativeLocation != null && this.Path != null);
			Contract.Ensures(this.RelativeLocation.Count <= this.Location.Count && this.Location.EndsWith(this.RelativeLocation));
		}

		/// <summary>Absolute location of the directory</summary>
		protected ITuple Location { [NotNull] get; private set; }

		/// <summary>Location of the directory relative to its parent Directory Layer</summary>
		protected ITuple RelativeLocation { [NotNull] get; private set; }

		/// <summary>Absolute path of this directory</summary>
		public IReadOnlyList<string> Path { [NotNull] get; private set; }

		/// <summary>Name of the directory</summary>
		public string Name
		{
			[NotNull]
			get { return this.Path.Count == 0 ? String.Empty : this.Path[this.Path.Count - 1]; }
		}

		/// <summary>Formatted path of this directory</summary>
		public string FullName
		{
			[NotNull]
			get { return String.Join("/", this.Path); }
		}

		/// <summary>Instance of the DirectoryLayer that was used to create or open this directory</summary>
		public FdbDirectoryLayer DirectoryLayer { [NotNull] get; private set; }

		/// <summary>Layer id of this directory</summary>
		public Slice Layer { get; private set; }

		/// <summary>Return the DirectoryLayer instance that should be called for the given path</summary>
		/// <param name="relativeLocation">Location relative to this directory subspace</param>
		protected virtual FdbDirectoryLayer GetLayerForPath(ITuple relativeLocation)
		{
			// for regular directories, always returns its DL.
			return this.DirectoryLayer;
		}

		/// <summary>Convert a path relative to this directory, into a path relative to the root of the current partition</summary>
		/// <param name="location">Path relative from this directory</param>
		/// <returns>Path relative to the path of the current partition</returns>
		[NotNull]
		protected virtual ITuple ToRelativePath(ITuple location)
		{
			return location == null ? this.RelativeLocation : this.RelativeLocation.Concat(location);
		}

		/// <summary>Convert a path relative to this directory, into a path relative to the root of the current partition</summary>
		/// <param name="path">Path relative from this directory</param>
		/// <returns>Path relative to the path of the current partition</returns>
		[NotNull]
		protected ITuple ToRelativePath(IEnumerable<string> path)
		{
			return ToRelativePath(path == null ? null :  STuple.FromEnumerable<string>(path));
		}

		/// <summary>Ensure that this directory was registered with the correct layer id</summary>
		/// <param name="layer">Expected layer id (if not empty)</param>
		/// <exception cref="System.InvalidOperationException">If the directory was registerd with a different layer id</exception>
		public void CheckLayer(Slice layer)
		{
			if (layer.IsPresent && layer != this.Layer)
			{
				throw new InvalidOperationException(String.Format("The directory {0} was created with incompatible layer {1} instead of expected {2}.", this.FullName, this.Layer.ToAsciiOrHexaString(), layer.ToAsciiOrHexaString()));
			}
		}

		/// <summary>Change the layer id of this directory</summary>
		/// <param name="trans">Transaction to use for the operation</param>
		/// <param name="newLayer">New layer id of this directory</param>
		[ItemNotNull]
		public async Task<FdbDirectorySubspace> ChangeLayerAsync([NotNull] IFdbTransaction trans, Slice newLayer)
		{
			if (trans == null) throw new ArgumentNullException("trans");
			if (newLayer.IsNull) newLayer = Slice.Empty;

			if (this.RelativeLocation.Count == 0)
			{ // cannot change the layer of the root of a directory layer
				throw new InvalidOperationException("Cannot change the layer id of the root of a directory layer or partition.");
			}

			if (this.Layer == FdbDirectoryPartition.LayerId)
			{ // cannot change a partition back to a regular directory
				throw new InvalidOperationException("Cannot change the layer id of a directory partition.");
			}
			if (newLayer == FdbDirectoryPartition.LayerId)
			{ // cannot change a regular directory into a new partition
				//REVIEW: or maybe we can? This would only be possible if this directory does not contain any sub-directory
				throw new InvalidOperationException("Cannot transform a regular directory into a partition.");
			}

			// set the layer to the new value
			await this.DirectoryLayer.ChangeLayerInternalAsync(trans, this.RelativeLocation, newLayer).ConfigureAwait(false);
			// and return the new version of the subspace
			return new FdbDirectorySubspace(this.Location, this.RelativeLocation, this.InternalKey, this.DirectoryLayer, newLayer, TypeSystem.Default.GetDynamicEncoder());
		}

		/// <summary>Opens a subdirectory with the given <paramref name="path"/>.
		/// If the subdirectory does not exist, it is created (creating intermediate subdirectories if necessary).
		/// </summary>
		/// <param name="trans">Transaction to use for the operation</param>
		/// <param name="path">Relative path of the subdirectory to create or open</param>
		/// <param name="layer">If <paramref name="layer"/> is specified, it is checked against the layer of an existing subdirectory or set as the layer of a new subdirectory.</param>
		public Task<FdbDirectorySubspace> CreateOrOpenAsync([NotNull] IFdbTransaction trans, [NotNull] IEnumerable<string> path, Slice layer = default(Slice))
		{
			if (trans == null) throw new ArgumentNullException("trans");
			if (path == null) throw new ArgumentNullException("path");

			return this.DirectoryLayer.CreateOrOpenInternalAsync(null, trans, ToRelativePath(path), layer, Slice.Nil, allowCreate: true, allowOpen: true, throwOnError: true);
		}

		/// <summary>Opens a subdirectory with the given <paramref name="path"/>.
		/// An exception is thrown if the subdirectory does not exist, or if a layer is specified and a different layer was specified when the subdirectory was created.
		/// </summary>
		/// <param name="trans">Transaction to use for the operation</param>
		/// <param name="path">Relative path of the subdirectory to open</param>
		/// <param name="layer">If specified, the opened directory must have the same layer id.</param>
		public Task<FdbDirectorySubspace> OpenAsync([NotNull] IFdbReadOnlyTransaction trans, [NotNull] IEnumerable<string> path, Slice layer = default(Slice))
		{
			if (trans == null) throw new ArgumentNullException("trans");
			if (path == null) throw new ArgumentNullException("path");
			return this.DirectoryLayer.CreateOrOpenInternalAsync(trans, null, ToRelativePath(path), layer, prefix: Slice.Nil, allowCreate: false, allowOpen: true, throwOnError: true);
		}

		/// <summary>Opens a subdirectory with the given <paramref name="path"/>.
		/// An exception is thrown if the subdirectory if a layer is specified and a different layer was specified when the subdirectory was created.
		/// </summary>
		/// <param name="trans">Transaction to use for the operation</param>
		/// <param name="path">Relative path of the subdirectory to open</param>
		/// <param name="layer">If specified, the opened directory must have the same layer id.</param>
		/// <returns>Returns the directory if it exists, or null if it was not found</returns>
		public Task<FdbDirectorySubspace> TryOpenAsync([NotNull] IFdbReadOnlyTransaction trans, [NotNull] IEnumerable<string> path, Slice layer = default(Slice))
		{
			if (trans == null) throw new ArgumentNullException("trans");
			if (path == null) throw new ArgumentNullException("path");
			return this.DirectoryLayer.CreateOrOpenInternalAsync(trans, null, ToRelativePath(path), layer, prefix: Slice.Nil, allowCreate: false, allowOpen: true, throwOnError: false);
		}

		/// <summary>Creates a subdirectory with the given <paramref name="path"/> (creating intermediate subdirectories if necessary).
		/// An exception is thrown if the given subdirectory already exists.
		/// </summary>
		/// <param name="trans">Transaction to use for the operation</param>
		/// <param name="path">Relative path of the subdirectory to create</param>
		/// <param name="layer">If <paramref name="layer"/> is specified, it is recorded with the subdirectory and will be checked by future calls to open.</param>
		public Task<FdbDirectorySubspace> CreateAsync([NotNull] IFdbTransaction trans, [NotNull] IEnumerable<string> path, Slice layer = default(Slice))
		{
			if (trans == null) throw new ArgumentNullException("trans");
			if (path == null) throw new ArgumentNullException("path");
			return this.DirectoryLayer.CreateOrOpenInternalAsync(null, trans, ToRelativePath(path), layer, prefix: Slice.Nil, allowCreate: true, allowOpen: false, throwOnError: true);
		}

		/// <summary>Creates a subdirectory with the given <paramref name="path"/> (creating intermediate subdirectories if necessary).
		/// An exception is thrown if the given subdirectory already exists.
		/// </summary>
		/// <param name="trans">Transaction to use for the operation</param>
		/// <param name="path">Relative path of the subdirectory to create</param>
		/// <param name="layer">If <paramref name="layer"/> is specified, it is recorded with the subdirectory and will be checked by future calls to open.</param>
		public Task<FdbDirectorySubspace> TryCreateAsync([NotNull] IFdbTransaction trans, [NotNull] IEnumerable<string> path, Slice layer = default(Slice))
		{
			if (trans == null) throw new ArgumentNullException("trans");
			if (path == null) throw new ArgumentNullException("path");
			return this.DirectoryLayer.CreateOrOpenInternalAsync(null, trans, ToRelativePath(path), layer, prefix: Slice.Nil, allowCreate: true, allowOpen: false, throwOnError: false);
		}

		/// <summary>Registers an existing prefix as a directory with the given <paramref name="path"/> (creating parent directories if necessary). This method is only indented for advanced use cases.</summary>
		/// <param name="trans">Transaction to use for the operation</param>
		/// <param name="path">Path of the directory to create</param>
		/// <param name="layer">If <paramref name="layer"/> is specified, it is recorded with the directory and will be checked by future calls to open.</param>
		/// <param name="prefix">The directory will be created with the given physical prefix; otherwise a prefix is allocated automatically.</param>
		public Task<FdbDirectorySubspace> RegisterAsync([NotNull] IFdbTransaction trans, [NotNull] IEnumerable<string> path, Slice layer, Slice prefix)
		{
			if (trans == null) throw new ArgumentNullException("trans");
			if (path == null) throw new ArgumentNullException("path");
			return this.DirectoryLayer.CreateOrOpenInternalAsync(null, trans, ToRelativePath(path), layer, prefix: prefix, allowCreate: true, allowOpen: false, throwOnError: true);
		}

		/// <summary>Moves the current directory to <paramref name="newAbsolutePath"/>.
		/// There is no effect on the physical prefix of the given directory, or on clients that already have the directory open.
		/// An error is raised if a directory already exists at `new_path`, or if the new path points to a child of the current directory.
		/// </summary>
		/// <param name="trans">Transaction to use for the operation</param>
		/// <param name="newAbsolutePath">Full path (from the root) where this directory will be moved</param>
		public Task<FdbDirectorySubspace> MoveToAsync([NotNull] IFdbTransaction trans, [NotNull] IEnumerable<string> newAbsolutePath)
		{
			if (trans == null) throw new ArgumentNullException("trans");
			if (newAbsolutePath == null) throw new ArgumentNullException("newAbsolutePath");

			// if 'this' is a Directory Partition, we need to move it via the parent DL !
			var directoryLayer = GetLayerForPath(STuple.Empty);

			// verify that it is still inside the same partition
			var location = FdbDirectoryLayer.ParsePath(newAbsolutePath, "newAbsolutePath");
			if (!location.StartsWith(directoryLayer.Location)) throw new InvalidOperationException("Cannot move between partitions.");
			location = location.Substring(directoryLayer.Location.Count);

			return directoryLayer.MoveInternalAsync(trans, this.RelativeLocation, location, throwOnError: true);
		}

		/// <summary>Moves the specified subdirectory to <paramref name="newPath"/>.
		/// There is no effect on the physical prefix of the given directory, or on clients that already have the directory open.
		/// An error is raised if a directory already exists at `new_path`.
		/// </summary>
		/// <param name="trans">Transaction to use for the operation</param>
		/// <param name="oldPath">Relative path under this directory of the subdirectory to be moved</param>
		/// <param name="newPath">Relative path under this directory where the subdirectory will be moved to</param>
		/// <returns>Returns the directory at its new location if successful.</returns>
		Task<FdbDirectorySubspace> IFdbDirectory.MoveAsync(IFdbTransaction trans, IEnumerable<string> oldPath, IEnumerable<string> newPath)
		{
			return this.DirectoryLayer.MoveAsync(trans, this.ToRelativePath(oldPath).ToArray<string>(), this.ToRelativePath(newPath).ToArray<string>());
		}

		/// <summary>Attempts to move the current directory to <paramref name="newPath"/>.
		/// There is no effect on the physical prefix of the given directory, or on clients that already have the directory open.
		/// </summary>
		/// <param name="trans">Transaction to use for the operation</param>
		/// <param name="newPath">Full path (from the root) where this directory will be moved</param>
		public Task<FdbDirectorySubspace> TryMoveToAsync([NotNull] IFdbTransaction trans, [NotNull] IEnumerable<string> newPath)
		{
			if (trans == null) throw new ArgumentNullException("trans");
			if (newPath == null) throw new ArgumentNullException("newPath");

			// if 'this' is a Directory Partition, we need to move it via the parent DL !
			var directoryLayer = GetLayerForPath(STuple.Empty);

			var location = FdbDirectoryLayer.ParsePath(newPath, "newPath");
			if (!location.StartsWith(directoryLayer.Location)) throw new InvalidOperationException("Cannot move between partitions.");
			location = location.Substring(directoryLayer.Location.Count);

			return directoryLayer.MoveInternalAsync(trans, this.RelativeLocation, location, throwOnError: false);
		}

		/// <summary>Attempts to move the specified subdirectory to <paramref name="newPath"/>.
		/// There is no effect on the physical prefix of the given directory, or on clients that already have the directory open.
		/// An error is raised if a directory already exists at `new_path`.
		/// </summary>
		/// <param name="trans">Transaction to use for the operation</param>
		/// <param name="oldPath">Relative path under this directory of the subdirectory to be moved</param>
		/// <param name="newPath">Relative path under this directory where the subdirectory will be moved to</param>
		/// <returns>Returns the directory at its new location if successful. If the directory cannot be moved, then null is returned.</returns>
		Task<FdbDirectorySubspace> IFdbDirectory.TryMoveAsync(IFdbTransaction trans, IEnumerable<string> oldPath, IEnumerable<string> newPath)
		{
			return this.DirectoryLayer.TryMoveAsync(trans, this.ToRelativePath(oldPath).ToArray<string>(), this.ToRelativePath(newPath).ToArray<string>());
		}

		/// <summary>Removes the directory, its contents, and all subdirectories.
		/// Warning: Clients that have already opened the directory might still insert data into its contents after it is removed.
		/// </summary>
		/// <param name="trans">Transaction to use for the operation</param>
		public Task RemoveAsync([NotNull] IFdbTransaction trans)
		{
			if (trans == null) throw new ArgumentNullException("trans");

			// if 'this' is a Directory Partition, we need to remove it from the parent DL !
			var directoryLayer = GetLayerForPath(STuple.Empty);

			return directoryLayer.RemoveInternalAsync(trans, this.RelativeLocation, throwIfMissing: true);
		}

		/// <summary>Removes a sub-directory, its contents, and all subdirectories.
		/// Warning: Clients that have already opened the directory might still insert data into its contents after it is removed.
		/// </summary>
		/// <param name="trans">Transaction to use for the operation</param>
		/// <param name="path">Path of the sub-directory to remove (relative to this directory)</param>
		public Task RemoveAsync([NotNull] IFdbTransaction trans, IEnumerable<string> path)
		{
			if (trans == null) throw new ArgumentNullException("trans");

			// If path is empty, we are removing ourselves!
			var location = FdbDirectoryLayer.ParsePath(path, "path");
			if (location.Count == 0)
			{
				return RemoveAsync(trans);
			}

			return this.DirectoryLayer.RemoveInternalAsync(trans, ToRelativePath(location), throwIfMissing: true);
		}

		/// <summary>Attempts to remove the directory, its contents, and all subdirectories.
		/// Warning: Clients that have already opened the directory might still insert data into its contents after it is removed.
		/// </summary>
		/// <param name="trans">Transaction to use for the operation</param>
		public Task<bool> TryRemoveAsync([NotNull] IFdbTransaction trans)
		{
			if (trans == null) throw new ArgumentNullException("trans");

			// if 'this' is a Directory Partition, we need to remove it from the parent DL !
			var directoryLayer = GetLayerForPath(STuple.Empty);

			return directoryLayer.RemoveInternalAsync(trans, this.RelativeLocation, throwIfMissing: false);
		}

		/// <summary>Attempts to remove a sub-directory, its contents, and all subdirectories.
		/// Warning: Clients that have already opened the directory might still insert data into its contents after it is removed.
		/// </summary>
		/// <param name="trans">Transaction to use for the operation</param>
		/// <param name="path">Path of the sub-directory to remove (relative to this directory)</param>
		public Task<bool> TryRemoveAsync([NotNull] IFdbTransaction trans, IEnumerable<string> path)
		{
			if (trans == null) throw new ArgumentNullException("trans");

			// If path is empty, we are removing ourselves!
			var location = FdbDirectoryLayer.ParsePath(path, "path");
			if (location.Count == 0)
			{
				return TryRemoveAsync(trans);
			}

			return this.DirectoryLayer.RemoveInternalAsync(trans, ToRelativePath(location), throwIfMissing: false);
		}

		/// <summary>Checks if this directory exists</summary>
		/// <returns>Returns true if the directory exists, otherwise false.</returns>
		public Task<bool> ExistsAsync([NotNull] IFdbReadOnlyTransaction trans)
		{
			if (trans == null) throw new ArgumentNullException("trans");

			// if 'this' is a Directory Partition, we need to remove it from the parent DL !
			var directoryLayer = GetLayerForPath(STuple.Empty);

			return directoryLayer.ExistsInternalAsync(trans, this.RelativeLocation);
		}

		/// <summary>Checks if a sub-directory exists</summary>
		/// <returns>Returns true if the directory exists, otherwise false.</returns>
		public Task<bool> ExistsAsync([NotNull] IFdbReadOnlyTransaction trans, IEnumerable<string> path)
		{
			if (trans == null) throw new ArgumentNullException("trans");

			// If path is empty, we are checking ourselves!
			var location = FdbDirectoryLayer.ParsePath(path, "path");
			if (location.Count == 0)
			{
				return ExistsAsync(trans);
			}

			return this.DirectoryLayer.ExistsInternalAsync(trans, ToRelativePath(location));
		}

		/// <summary>Returns the list of all the subdirectories of the current directory.</summary>
		public Task<List<string>> ListAsync([NotNull] IFdbReadOnlyTransaction trans)
		{
			if (trans == null) throw new ArgumentNullException("trans");
			return this.DirectoryLayer.ListInternalAsync(trans, this.RelativeLocation, throwIfMissing: true);
		}

		/// <summary>Returns the list of all the subdirectories of a sub-directory.</summary>
		public Task<List<string>> ListAsync([NotNull] IFdbReadOnlyTransaction trans, IEnumerable<string> path)
		{
			if (trans == null) throw new ArgumentNullException("trans");
			return this.DirectoryLayer.ListInternalAsync(trans, ToRelativePath(path), throwIfMissing: true);
		}

		/// <summary>Returns the list of all the subdirectories of a sub-directory, it it exists.</summary>
		public Task<List<string>> TryListAsync([NotNull] IFdbReadOnlyTransaction trans)
		{
			if (trans == null) throw new ArgumentNullException("trans");
			return this.DirectoryLayer.ListInternalAsync(trans, this.RelativeLocation, throwIfMissing: false);
		}

		/// <summary>Returns the list of all the subdirectories of the current directory, it it exists.</summary>
		public Task<List<string>> TryListAsync([NotNull] IFdbReadOnlyTransaction trans, IEnumerable<string> path)
		{
			if (trans == null) throw new ArgumentNullException("trans");
			return this.DirectoryLayer.ListInternalAsync(trans, ToRelativePath(path), throwIfMissing: false);
		}

		public override string DumpKey(Slice key)
		{
			string str = base.DumpKey(key);
			return String.Format("[/{0}]:{1}", this.FullName, str);
		}

		/// <summary>Returns a user-friendly description of this directory</summary>
		public override string ToString()
		{
			if (this.Layer.IsNullOrEmpty)
			{
				return String.Format("DirectorySubspace(path={0}, prefix={1})", this.FullName, this.InternalKey.ToAsciiOrHexaString());
			}
			else
			{
				return String.Format("DirectorySubspace(path={0}, prefix={1}, layer={2})", this.FullName, this.InternalKey.ToAsciiOrHexaString(), this.Layer.ToAsciiOrHexaString());
			}
		}

		//note: Equals() and GetHashcode() are already implemented in FdbSubspace, and don't need to be overriden here

	}
}
