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
	using FoundationDB.Client.Utils;
	using FoundationDB.Layers.Tuples;
	using System;
	using System.Collections.Generic;
	using System.Diagnostics;
	using System.Threading.Tasks;

	[DebuggerDisplay("Path={Path}, Prefix={Key}, Layer={Layer}")]
	public class FdbDirectorySubspace : FdbSubspace, IFdbDirectory
	{

		internal FdbDirectorySubspace(IFdbTuple location, Slice prefix, FdbDirectoryLayer directoryLayer, Slice layer)
			: base(prefix)
		{
			Contract.Requires(location != null && prefix != null && directoryLayer != null);

			this.Location = location;
			this.Path = location.ToArray<string>();
			this.DirectoryLayer = directoryLayer;
			this.Layer = layer;
		}

		internal IFdbTuple Location { get; private set;}

		/// <summary>Path of this directory</summary>
		public IReadOnlyList<string> Path { get; private set; }

		public FdbDirectoryLayer DirectoryLayer { get; private set; }

		/// <summary>Layer id of this directory</summary>
		public Slice Layer { get; private set; }

		/// <summary>Ensure that this directory was registered with the correct layer id</summary>
		/// <param name="layer">Expected layer id (if not empty)</param>
		/// <exception cref="System.InvalidOperationException">If the directory was registerd with a different layer id</exception>
		public void CheckLayer(Slice layer)
		{
			if (layer.IsPresent && layer != this.Layer)
			{
				throw new InvalidOperationException(String.Format("The directory {0} was created with incompatible layer {1} instead of expected {2}.", String.Join("/" , this.Path), this.Layer.ToAsciiOrHexaString(), layer.ToAsciiOrHexaString()));
			}
		}

		/// <summary>Change the layer id of this directory</summary>
		/// <param name="trans">Transaction to use for the operation</param>
		/// <param name="newLayer">New layer id of this directory</param>
		public async Task<FdbDirectorySubspace> ChangeLayerAsync(IFdbTransaction trans, Slice newLayer)
		{
			if (trans == null) throw new ArgumentNullException("trans");
			if (newLayer.IsNull) newLayer = Slice.Empty;

			// set the layer to the new value
			await this.DirectoryLayer.ChangeLayerInternalAsync(trans, this.Location, newLayer);
			// and return the new version of the subspace
			return new FdbDirectorySubspace(this.Location, this.Key, this.DirectoryLayer, newLayer);
		}

		#region CreateOrOpenAsync(...)

		/// <summary>Opens a subdirectory with the given <paramref name="name"/>.
		/// If the subdirectory does not exist, it is created (creating intermediate subdirectories if necessary).
		/// If prefix is specified, the subdirectory is created with the given physical prefix; otherwise a prefix is allocated automatically.
		/// If layer is specified, it is checked against the layer of an existing subdirectory or set as the layer of a new subdirectory.
		/// </summary>
		public Task<FdbDirectorySubspace> CreateOrOpenAsync(IFdbTransaction trans, string name, Slice layer = default(Slice), Slice prefix = default(Slice))
		{
			if (trans == null) throw new ArgumentNullException("trans");
			if (name == null) throw new ArgumentNullException("name");
			return this.DirectoryLayer.CreateOrOpenInternalAsync(trans, this.Location.Append(name), layer, prefix, allowCreate: true, allowOpen: true, throwOnError: true);
		}

		/// <summary>Opens a subdirectory with the given <paramref name="path"/>.
		/// If the subdirectory does not exist, it is created (creating intermediate subdirectories if necessary).
		/// If prefix is specified, the subdirectory is created with the given physical prefix; otherwise a prefix is allocated automatically.
		/// If layer is specified, it is checked against the layer of an existing subdirectory or set as the layer of a new subdirectory.
		/// </summary>
		public Task<FdbDirectorySubspace> CreateOrOpenAsync(IFdbTransaction trans, IEnumerable<string> subPath, Slice layer = default(Slice), Slice prefix = default(Slice))
		{
			if (trans == null) throw new ArgumentNullException("trans");
			if (subPath == null) throw new ArgumentNullException("subPath");
			return this.DirectoryLayer.CreateOrOpenInternalAsync(trans, this.Location.Concat(FdbTuple.CreateRange(subPath)), layer, prefix, allowCreate: true, allowOpen: true, throwOnError: true);
		}

		#endregion

		#region OpenAsync(...)

		/// <summary>Opens a subdirectory with the given <paramref name="name"/>.
		/// An exception is thrown if the subdirectory does not exist, or if a layer is specified and a different layer was specified when the subdirectory was created.
		/// </summary>
		/// <param name="trans">Transaction to use for the operation</param>
		/// <param name="subPath">Relative path of the subdirectory to open</param>
		public Task<FdbDirectorySubspace> OpenAsync(IFdbTransaction trans, string name, Slice layer = default(Slice))
		{
			if (trans == null) throw new ArgumentNullException("trans");
			if (name == null) throw new ArgumentNullException("name");
			return this.DirectoryLayer.CreateOrOpenInternalAsync(trans, this.Location.Append(name), layer, prefix: Slice.Nil, allowCreate: false, allowOpen: true, throwOnError: true);
		}

		/// <summary>Opens a subdirectory with the given <paramref name="path"/>.
		/// An exception is thrown if the subdirectory does not exist, or if a layer is specified and a different layer was specified when the subdirectory was created.
		/// </summary>
		/// <param name="trans">Transaction to use for the operation</param>
		/// <param name="subPath">Relative path of the subdirectory to open</param>
		public Task<FdbDirectorySubspace> OpenAsync(IFdbTransaction trans, IEnumerable<string> subPath, Slice layer = default(Slice))
		{
			if (trans == null) throw new ArgumentNullException("trans");
			if (subPath == null) throw new ArgumentNullException("subPath");
			return this.DirectoryLayer.CreateOrOpenInternalAsync(trans, this.Location.Concat(FdbTuple.CreateRange(subPath)), layer, prefix: Slice.Nil, allowCreate: false, allowOpen: true, throwOnError: true);
		}

		#endregion

		#region TryOpenAsync(...)

		/// <summary>Opens a subdirectory with the given <paramref name="name"/>, if it exists.
		/// An exception is thrown if the subdirectory if a layer is specified and a different layer was specified when the subdirectory was created.
		/// </summary>
		/// <param name="trans">Transaction to use for the operation</param>
		/// <param name="name">Name of the subdirectory to open</param>
		/// <returns>Returns the directory if it exists, or null if it was not found</returns>
		public Task<FdbDirectorySubspace> TryOpenAsync(IFdbTransaction trans, string name, Slice layer = default(Slice))
		{
			if (trans == null) throw new ArgumentNullException("trans");
			if (name == null) throw new ArgumentNullException("name");
			return this.DirectoryLayer.CreateOrOpenInternalAsync(trans, this.Location.Append(name), layer, prefix: Slice.Nil, allowCreate: false, allowOpen: true, throwOnError: false);
		}

		/// <summary>Opens a subdirectory with the given <paramref name="path"/>.
		/// An exception is thrown if the subdirectory if a layer is specified and a different layer was specified when the subdirectory was created.
		/// </summary>
		/// <param name="trans">Transaction to use for the operation</param>
		/// <param name="subPath">Relative path of the subdirectory to open</param>
		/// <returns>Returns the directory if it exists, or null if it was not found</returns>
		public Task<FdbDirectorySubspace> TryOpenAsync(IFdbTransaction trans, IEnumerable<string> subPath, Slice layer = default(Slice))
		{
			if (trans == null) throw new ArgumentNullException("trans");
			if (subPath == null) throw new ArgumentNullException("subPath");
			return this.DirectoryLayer.CreateOrOpenInternalAsync(trans, this.Location.Concat(FdbTuple.CreateRange(subPath)), layer, prefix: Slice.Nil, allowCreate: false, allowOpen: true, throwOnError: false);
		}

		#endregion

		#region CreateAsync(...)

		/// <summary>Creates a subdirectory with the given <paramref name="path"/> (creating intermediate subdirectories if necessary).
		/// An exception is thrown if the given subdirectory already exists.
		/// </summary>
		/// <param name="trans">Transaction to use for the operation</param>
		/// <param name="name">Name of the subdirectory to create</param>
		/// <param name="layer">If <paramref name="layer"/> is specified, it is recorded with the subdirectory and will be checked by future calls to open.</param>
		/// <param name="prefix">If <paramref name="prefix"/> is specified, the subdirectory is created with the given physical prefix; otherwise a prefix is allocated automatically.</param>
		public Task<FdbDirectorySubspace> CreateAsync(IFdbTransaction trans, string name, Slice layer = default(Slice), Slice prefix = default(Slice))
		{
			if (trans == null) throw new ArgumentNullException("trans");
			if (name == null) throw new ArgumentNullException("name");
			return this.DirectoryLayer.CreateOrOpenInternalAsync(trans, this.Location.Append(name), layer, prefix, allowCreate: true, allowOpen: false, throwOnError: true);
		}

		/// <summary>Creates a subdirectory with the given <paramref name="path"/> (creating intermediate subdirectories if necessary).
		/// An exception is thrown if the given subdirectory already exists.
		/// </summary>
		/// <param name="trans">Transaction to use for the operation</param>
		/// <param name="subPath">Relative path of the subdirectory to create</param>
		/// <param name="layer">If <paramref name="layer"/> is specified, it is recorded with the subdirectory and will be checked by future calls to open.</param>
		/// <param name="prefix">If <paramref name="prefix"/> is specified, the subdirectory is created with the given physical prefix; otherwise a prefix is allocated automatically.</param>
		public Task<FdbDirectorySubspace> CreateAsync(IFdbTransaction trans, IEnumerable<string> subPath, Slice layer = default(Slice), Slice prefix = default(Slice))
		{
			if (trans == null) throw new ArgumentNullException("trans");
			if (subPath == null) throw new ArgumentNullException("subPath");
			return this.DirectoryLayer.CreateOrOpenInternalAsync(trans, this.Location.Concat(FdbTuple.CreateRange(subPath)), layer, prefix, allowCreate: true, allowOpen: false, throwOnError: true);
		}

		#endregion

		#region TryCreateAsync(...)

		/// <summary>Creates a subdirectory with the given <paramref name="path"/> (creating intermediate subdirectories if necessary).
		/// An exception is thrown if the given subdirectory already exists.
		/// </summary>
		/// <param name="trans">Transaction to use for the operation</param>
		/// <param name="name">Relative path of the subdirectory to create</param>
		/// <param name="layer">If <paramref name="layer"/> is specified, it is recorded with the subdirectory and will be checked by future calls to open.</param>
		/// <param name="prefix">If <paramref name="prefix"/> is specified, the subdirectory is created with the given physical prefix; otherwise a prefix is allocated automatically.</param>
		public Task<FdbDirectorySubspace> TryCreateAsync(IFdbTransaction trans, string name, Slice layer = default(Slice), Slice prefix = default(Slice))
		{
			if (trans == null) throw new ArgumentNullException("trans");
			if (name == null) throw new ArgumentNullException("name");
			return this.DirectoryLayer.CreateOrOpenInternalAsync(trans, this.Location.Append(name), layer, prefix, allowCreate: true, allowOpen: false, throwOnError: false);
		}

		/// <summary>Creates a subdirectory with the given <paramref name="path"/> (creating intermediate subdirectories if necessary).
		/// An exception is thrown if the given subdirectory already exists.
		/// </summary>
		/// <param name="trans">Transaction to use for the operation</param>
		/// <param name="subPath">Relative path of the subdirectory to create</param>
		/// <param name="layer">If <paramref name="layer"/> is specified, it is recorded with the subdirectory and will be checked by future calls to open.</param>
		/// <param name="prefix">If <paramref name="prefix"/> is specified, the subdirectory is created with the given physical prefix; otherwise a prefix is allocated automatically.</param>
		public Task<FdbDirectorySubspace> TryCreateAsync(IFdbTransaction trans, IEnumerable<string> subPath, Slice layer = default(Slice), Slice prefix = default(Slice))
		{
			if (trans == null) throw new ArgumentNullException("trans");
			if (subPath == null) throw new ArgumentNullException("subPath");
			return this.DirectoryLayer.CreateOrOpenInternalAsync(trans, this.Location.Concat(FdbTuple.CreateRange(subPath)), layer, prefix, allowCreate: true, allowOpen: false, throwOnError: false);
		}

		#endregion

		#region MoveAsync(...)

		/// <summary>Moves the current directory to <paramref name="newPath"/>.
		/// There is no effect on the physical prefix of the given directory, or on clients that already have the directory open.
		/// An error is raised if a directory already exists at `new_path`, or if the new path points to a child of the current directory.
		/// </summary>
		/// <param name="trans">Transaction to use for the operation</param>
		/// <param name="newPath">Full path (from the root) where this directory will be moved</param>
		public Task<FdbDirectorySubspace> MoveAsync(IFdbTransaction trans, IEnumerable<string> newPath)
		{
			if (trans == null) throw new ArgumentNullException("trans");
			if (newPath == null) throw new ArgumentNullException("newPath");
			return this.DirectoryLayer.MoveInternalAsync(trans, this.Location, FdbTuple.CreateRange(newPath), throwOnError: true);
		}

		/// <summary>Moves the current directory to <paramref name="newPath"/>.
		/// There is no effect on the physical prefix of the given directory, or on clients that already have the directory open.
		/// An error is raised if a directory already exists at `new_path`, or if the new path points to a child of the current directory.
		/// </summary>
		/// <param name="trans">Transaction to use for the operation</param>
		/// <param name="newPath">Full path (from the root) where this directory will be moved</param>
		public Task<FdbDirectorySubspace> MoveAsync(IFdbTransaction trans, IEnumerable<string> oldPath, IEnumerable<string> newPath)
		{
			if (trans == null) throw new ArgumentNullException("trans");
			if (newPath == null) throw new ArgumentNullException("newPath");
			return this.DirectoryLayer.MoveInternalAsync(trans, this.Location.Concat(FdbTuple.CreateRange(oldPath)), this.Location.Concat(FdbTuple.CreateRange(newPath)), throwOnError: true);
		}

		#endregion

		#region TryMoveAsync(...)

		/// <summary>Attempts to move the current directory to <paramref name="newPath"/>.
		/// There is no effect on the physical prefix of the given directory, or on clients that already have the directory open.
		/// </summary>
		/// <param name="newPath">Full path (from the root) where this directory will be moved</param>
		public Task<FdbDirectorySubspace> TryMoveAsync(IFdbTransaction trans, IEnumerable<string> newPath)
		{
			if (trans == null) throw new ArgumentNullException("trans");
			if (newPath == null) throw new ArgumentNullException("newPath");
			return this.DirectoryLayer.MoveInternalAsync(trans, this.Location, FdbTuple.CreateRange(newPath), throwOnError: false);
		}

		/// <summary>Attempts to move the current directory to <paramref name="newPath"/>.
		/// There is no effect on the physical prefix of the given directory, or on clients that already have the directory open.
		/// </summary>
		/// <param name="newPath">Full path (from the root) where this directory will be moved</param>
		public Task<FdbDirectorySubspace> TryMoveAsync(IFdbTransaction trans, IEnumerable<string> oldPath, IEnumerable<string> newPath)
		{
			if (trans == null) throw new ArgumentNullException("trans");
			if (newPath == null) throw new ArgumentNullException("newPath");
			return this.DirectoryLayer.MoveInternalAsync(trans, this.Location.Concat(FdbTuple.CreateRange(oldPath)), this.Location.Concat(FdbTuple.CreateRange(oldPath)), throwOnError: false);
		}

		#endregion

		/// <summary>Removes the directory, its contents, and all subdirectories.
		/// Warning: Clients that have already opened the directory might still insert data into its contents after it is removed.
		/// </summary>
		/// <param name="trans">Transaction to use for the operation</param>
		public Task RemoveAsync(IFdbTransaction trans)
		{
			if (trans == null) throw new ArgumentNullException("trans");
			return this.DirectoryLayer.RemoveInternalAsync(trans, this.Location, throwIfMissing: true);
		}

		/// <summary>Removes the directory, its contents, and all subdirectories.
		/// Warning: Clients that have already opened the directory might still insert data into its contents after it is removed.
		/// </summary>
		/// <param name="trans">Transaction to use for the operation</param>
		public Task RemoveAsync(IFdbTransaction trans, IEnumerable<string> path)
		{
			if (trans == null) throw new ArgumentNullException("trans");
			if (path == null) throw new ArgumentNullException("path");
			return this.DirectoryLayer.RemoveInternalAsync(trans, this.Location.Concat(FdbTuple.CreateRange(path)), throwIfMissing: true);
		}

		/// <summary>Attempts to remove the directory, its contents, and all subdirectories.
		/// Warning: Clients that have already opened the directory might still insert data into its contents after it is removed.
		/// </summary>
		/// <param name="trans">Transaction to use for the operation</param>
		public Task<bool> TryRemoveAsync(IFdbTransaction trans)
		{
			if (trans == null) throw new ArgumentNullException("trans");
			return this.DirectoryLayer.RemoveInternalAsync(trans, this.Location, throwIfMissing: false);
		}

		/// <summary>Attempts to remove the directory, its contents, and all subdirectories.
		/// Warning: Clients that have already opened the directory might still insert data into its contents after it is removed.
		/// </summary>
		/// <param name="trans">Transaction to use for the operation</param>
		public Task<bool> TryRemoveAsync(IFdbTransaction trans, IEnumerable<string> path)
		{
			if (trans == null) throw new ArgumentNullException("trans");
			if (path == null) throw new ArgumentNullException("path");
			return this.DirectoryLayer.RemoveInternalAsync(trans, this.Location, throwIfMissing: false);
		}

		/// <summary>Checks if this directory exists</summary>
		/// <returns>Returns true if the directory exists, otherwise false.</returns>
		public Task<bool> ExistsAsync(IFdbReadOnlyTransaction trans)
		{
			if (trans == null) throw new ArgumentNullException("trans");
			return this.DirectoryLayer.ExistsInternalAsync(trans, this.Location);
		}

		/// <summary>Checks if a sub-directory exists</summary>
		/// <returns>Returns true if the directory exists, otherwise false.</returns>
		public Task<bool> ExistsAsync(IFdbReadOnlyTransaction trans, IEnumerable<string> path)
		{
			if (trans == null) throw new ArgumentNullException("trans");
			return this.DirectoryLayer.ExistsInternalAsync(trans, this.Location.Concat(FdbTuple.CreateRange(path)));
		}

		/// <summary>Returns the list of all the subdirectories of the current directory.</summary>
		public Task<List<string>> ListAsync(IFdbReadOnlyTransaction trans)
		{
			if (trans == null) throw new ArgumentNullException("trans");
			return this.DirectoryLayer.ListInternalAsync(trans, this.Location, throwIfMissing: true);
		}

		/// <summary>Returns the list of all the subdirectories of a sub-directory.</summary>
		public Task<List<string>> ListAsync(IFdbReadOnlyTransaction trans, IEnumerable<string> path)
		{
			if (trans == null) throw new ArgumentNullException("trans");
			if (path == null) throw new ArgumentNullException("path");
			return this.DirectoryLayer.ListInternalAsync(trans, this.Location.Concat(FdbTuple.CreateRange(path)), throwIfMissing: true);
		}

		/// <summary>Returns the list of all the subdirectories of a sub-directory, it it exists.</summary>
		public Task<List<string>> TryListAsync(IFdbReadOnlyTransaction trans)
		{
			if (trans == null) throw new ArgumentNullException("trans");
			return this.DirectoryLayer.ListInternalAsync(trans, this.Location, throwIfMissing: false);
		}

		/// <summary>Returns the list of all the subdirectories of the current directory, it it exists.</summary>
		public Task<List<string>> TryListAsync(IFdbReadOnlyTransaction trans, IEnumerable<string> path)
		{
			if (trans == null) throw new ArgumentNullException("trans");
			if (path == null) throw new ArgumentNullException("path");
			return this.DirectoryLayer.ListInternalAsync(trans, this.Location.Concat(FdbTuple.CreateRange(path)), throwIfMissing: false);
		}

		public override string ToString()
		{
			return String.Format("DirectorySubspace('/{0}', {1})", String.Join("/", this.Path), this.Key.ToAsciiOrHexaString());
		}

	}
}
