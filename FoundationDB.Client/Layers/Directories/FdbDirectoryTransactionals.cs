#region BSD Licence
/* Copyright (c) 2013-2014, Doxense SAS
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
	using System;
	using System.Collections.Generic;
	using System.Threading;
	using System.Threading.Tasks;

	public static class FdbDirectoryTransactionals
	{

		#region CreateOrOpen...

		/// <summary>Opens the directory with the given <param name="path"/>.
		/// If the directory does not exist, it is created (creating parent directories if necessary).
		/// If layer is specified, it is checked against the layer of an existing directory or set as the layer of a new directory.
		/// </summary>
		public static Task<FdbDirectorySubspace> CreateOrOpenAsync(this IFdbDirectory directory, IFdbTransactional db, IEnumerable<string> path, CancellationToken cancellationToken)
		{
			if (directory == null) throw new ArgumentNullException("directory");
			if (db == null) throw new ArgumentNullException("db");
			if (path == null) throw new ArgumentNullException("path");
			return db.ReadWriteAsync((tr) => directory.CreateOrOpenAsync(tr, path, Slice.Nil), cancellationToken);
		}

		/// <summary>Opens the directory with the given <param name="path"/>.
		/// If the directory does not exist, it is created (creating parent directories if necessary).
		/// If layer is specified, it is checked against the layer of an existing directory or set as the layer of a new directory.
		/// </summary>
		public static Task<FdbDirectorySubspace> CreateOrOpenAsync(this IFdbDirectory directory, IFdbTransactional db, IEnumerable<string> path, Slice layer, CancellationToken cancellationToken)
		{
			if (directory == null) throw new ArgumentNullException("directory");
			if (db == null) throw new ArgumentNullException("db");
			if (path == null) throw new ArgumentNullException("path");
			return db.ReadWriteAsync((tr) => directory.CreateOrOpenAsync(tr, path, layer), cancellationToken);
		}

		/// <summary>Opens the directory with the given <param name="name"/>.
		/// If the directory does not exist, it is created (creating parent directories if necessary).
		/// If layer is specified, it is checked against the layer of an existing directory or set as the layer of a new directory.
		/// </summary>
		public static Task<FdbDirectorySubspace> CreateOrOpenAsync(this IFdbDirectory directory, IFdbTransactional db, string name, CancellationToken cancellationToken)
		{
			if (directory == null) throw new ArgumentNullException("directory");
			if (db == null) throw new ArgumentNullException("db");
			if (name == null) throw new ArgumentNullException("name");
			return db.ReadWriteAsync((tr) => directory.CreateOrOpenAsync(tr, new [] { name }, Slice.Nil), cancellationToken);
		}

		/// <summary>Opens the directory with the given <param name="name"/>.
		/// If the directory does not exist, it is created (creating parent directories if necessary).
		/// If layer is specified, it is checked against the layer of an existing directory or set as the layer of a new directory.
		/// </summary>
		public static Task<FdbDirectorySubspace> CreateOrOpenAsync(this IFdbDirectory directory, IFdbTransactional db, string name, Slice layer, CancellationToken cancellationToken)
		{
			if (directory == null) throw new ArgumentNullException("directory");
			if (db == null) throw new ArgumentNullException("db");
			if (name == null) throw new ArgumentNullException("name");
			return db.ReadWriteAsync((tr) => directory.CreateOrOpenAsync(tr, new[] { name }, layer), cancellationToken);
		}

		/// <summary>Opens the directory with the given <param name="name"/>.
		/// If the directory does not exist, it is created (creating parent directories if necessary).
		/// If layer is specified, it is checked against the layer of an existing directory or set as the layer of a new directory.
		/// </summary>
		public static Task<FdbDirectorySubspace> CreateOrOpenAsync(this IFdbDirectory directory, IFdbTransaction trans, string name, Slice layer = default(Slice))
		{
			if (directory == null) throw new ArgumentNullException("directory");
			if (trans == null) throw new ArgumentNullException("trans");
			if (name == null) throw new ArgumentNullException("name");
			return directory.CreateOrOpenAsync(trans, new[] { name }, layer);
		}

		#endregion

		#region Create / TryCreate...

		/// <summary>Creates a directory with the given <paramref name="path"/> (creating parent directories if necessary).
		/// An error is raised if the given directory already exists.
		/// </summary>
		public static Task<FdbDirectorySubspace> CreateAsync(this IFdbDirectory directory, IFdbTransactional db, IEnumerable<string> path, CancellationToken cancellationToken)
		{
			if (directory == null) throw new ArgumentNullException("directory");
			if (db == null) throw new ArgumentNullException("db");
			if (path == null) throw new ArgumentNullException("path");
			return db.ReadWriteAsync((tr) => directory.CreateAsync(tr, path, Slice.Nil), cancellationToken);
		}

		/// <summary>Creates a directory with the given <paramref name="path"/> (creating parent directories if necessary).
		/// An error is raised if the given directory already exists.
		/// If <paramref name="layer"/> is specified, it is recorded with the directory and will be checked by future calls to open.
		/// </summary>
		public static Task<FdbDirectorySubspace> CreateAsync(this IFdbDirectory directory, IFdbTransactional db, IEnumerable<string> path, Slice layer, CancellationToken cancellationToken)
		{
			if (directory == null) throw new ArgumentNullException("directory");
			if (db == null) throw new ArgumentNullException("db");
			if (path == null) throw new ArgumentNullException("path");
			return db.ReadWriteAsync((tr) => directory.CreateAsync(tr, path, layer), cancellationToken);
		}

		/// <summary>Creates a directory with the given <paramref name="name"/>.
		/// An error is raised if the given directory already exists.
		/// </summary>
		public static Task<FdbDirectorySubspace> CreateAsync(this IFdbDirectory directory, IFdbTransactional db, string name, CancellationToken cancellationToken)
		{
			if (directory == null) throw new ArgumentNullException("directory");
			if (db == null) throw new ArgumentNullException("db");
			if (name == null) throw new ArgumentNullException("name");
			return db.ReadWriteAsync((tr) => directory.CreateAsync(tr, new [] { name }, Slice.Nil), cancellationToken);
		}

		/// <summary>Creates a directory with the given <paramref name="name"/>.
		/// An error is raised if the given directory already exists.
		/// If <paramref name="layer"/> is specified, it is recorded with the directory and will be checked by future calls to open.
		/// </summary>
		public static Task<FdbDirectorySubspace> CreateAsync(this IFdbDirectory directory, IFdbTransactional db, string name, Slice layer, CancellationToken cancellationToken)
		{
			if (directory == null) throw new ArgumentNullException("directory");
			if (db == null) throw new ArgumentNullException("db");
			if (name == null) throw new ArgumentNullException("name");
			return db.ReadWriteAsync((tr) => directory.CreateAsync(tr, new[] { name }, layer), cancellationToken);
		}

		/// <summary>Creates a directory with the given <paramref name="name"/>.
		/// An error is raised if the given directory already exists.
		/// If <paramref name="layer"/> is specified, it is recorded with the directory and will be checked by future calls to open.
		/// </summary>
		public static Task<FdbDirectorySubspace> CreateAsync(this IFdbDirectory directory, IFdbTransaction trans, string name, Slice layer = default(Slice))
		{
			if (directory == null) throw new ArgumentNullException("directory");
			if (trans == null) throw new ArgumentNullException("trans");
			if (name == null) throw new ArgumentNullException("name");
			return directory.CreateAsync(trans, new[] { name }, layer);
		}

		/// <summary>Attempts to create a directory with the given <paramref name="path"/> (creating parent directories if necessary).</summary>
		public static Task<FdbDirectorySubspace> TryCreateAsync(this IFdbDirectory directory, IFdbTransactional db, IEnumerable<string> path, CancellationToken cancellationToken)
		{
			if (directory == null) throw new ArgumentNullException("directory");
			if (db == null) throw new ArgumentNullException("db");
			if (path == null) throw new ArgumentNullException("path");
			return db.ReadWriteAsync((tr) => directory.TryCreateAsync(tr, path, Slice.Nil), cancellationToken);
		}

		/// <summary>Attempts to create a directory with the given <paramref name="path"/> (creating parent directories if necessary).
		/// If <paramref name="layer"/> is specified, it is recorded with the directory and will be checked by future calls to open.
		/// </summary>
		public static Task<FdbDirectorySubspace> TryCreateAsync(this IFdbDirectory directory, IFdbTransactional db, IEnumerable<string> path, Slice layer, CancellationToken cancellationToken)
		{
			if (directory == null) throw new ArgumentNullException("directory");
			if (db == null) throw new ArgumentNullException("db");
			if (path == null) throw new ArgumentNullException("path");
			return db.ReadWriteAsync((tr) => directory.TryCreateAsync(tr, path, layer), cancellationToken);
		}

		/// <summary>Attempts to create a directory with the given <paramref name="name"/>.</summary>
		public static Task<FdbDirectorySubspace> TryCreateAsync(this IFdbDirectory directory, IFdbTransactional db, string name, CancellationToken cancellationToken)
		{
			if (directory == null) throw new ArgumentNullException("directory");
			if (db == null) throw new ArgumentNullException("db");
			if (name == null) throw new ArgumentNullException("name");
			return db.ReadWriteAsync((tr) => directory.TryCreateAsync(tr, new [] { name }, Slice.Nil), cancellationToken);
		}

		/// <summary>Attempts to create a directory with the given <paramref name="name"/>.
		/// If <paramref name="layer"/> is specified, it is recorded with the directory and will be checked by future calls to open.
		/// </summary>
		public static Task<FdbDirectorySubspace> TryCreateAsync(this IFdbDirectory directory, IFdbTransactional db, string name, Slice layer, CancellationToken cancellationToken)
		{
			if (directory == null) throw new ArgumentNullException("directory");
			if (db == null) throw new ArgumentNullException("db");
			if (name == null) throw new ArgumentNullException("name");
			return db.ReadWriteAsync((tr) => directory.TryCreateAsync(tr, new[] { name }, layer), cancellationToken);
		}

		/// <summary>Attempts to create a directory with the given <paramref name="name"/>.
		/// If <paramref name="layer"/> is specified, it is recorded with the directory and will be checked by future calls to open.
		/// </summary>
		public static Task<FdbDirectorySubspace> TryCreateAsync(this IFdbDirectory directory, IFdbTransaction trans, string name, Slice layer = default(Slice))
		{
			if (directory == null) throw new ArgumentNullException("directory");
			if (trans == null) throw new ArgumentNullException("trans");
			if (name == null) throw new ArgumentNullException("name");
			return directory.TryCreateAsync(trans, new[] { name }, layer);
		}

		#endregion

		#region Open / TryOpen...

		/// <summary>Opens the directory with the given <paramref name="path"/>.
		/// An error is raised if the directory does not exist.
		/// </summary>
		public static Task<FdbDirectorySubspace> OpenAsync(this IFdbDirectory directory, IFdbTransactional db, IEnumerable<string> path, CancellationToken cancellationToken)
		{
			if (directory == null) throw new ArgumentNullException("directory");
			if (db == null) throw new ArgumentNullException("db");
			if (path == null) throw new ArgumentNullException("path");
			return db.ReadAsync((tr) => directory.OpenAsync(tr, path, Slice.Nil), cancellationToken);
		}

		/// <summary>Opens the directory with the given <paramref name="path"/>.
		/// An error is raised if the directory does not exist, or if a layer is specified and a different layer was specified when the directory was created.
		/// </summary>
		public static Task<FdbDirectorySubspace> OpenAsync(this IFdbDirectory directory, IFdbTransactional db, IEnumerable<string> path, Slice layer, CancellationToken cancellationToken)
		{
			if (directory == null) throw new ArgumentNullException("directory");
			if (db == null) throw new ArgumentNullException("db");
			if (path == null) throw new ArgumentNullException("path");
			return db.ReadAsync((tr) => directory.OpenAsync(tr, path, layer), cancellationToken);
		}

		/// <summary>Opens the sub-directory with the given <paramref name="name"/>.
		/// An error is raised if the directory does not exist.
		/// </summary>
		public static Task<FdbDirectorySubspace> OpenAsync(this IFdbDirectory directory, IFdbTransactional db, string name, CancellationToken cancellationToken)
		{
			if (directory == null) throw new ArgumentNullException("directory");
			if (db == null) throw new ArgumentNullException("db");
			if (name == null) throw new ArgumentNullException("name");
			return db.ReadAsync((tr) => directory.OpenAsync(tr, new[] { name }, Slice.Nil), cancellationToken);
		}

		/// <summary>Opens the sub-directory with the given <paramref name="name"/>.
		/// An error is raised if the directory does not exist, or if a layer is specified and a different layer was specified when the directory was created.
		/// </summary>
		public static Task<FdbDirectorySubspace> OpenAsync(this IFdbDirectory directory, IFdbTransactional db, string name, Slice layer, CancellationToken cancellationToken)
		{
			if (directory == null) throw new ArgumentNullException("directory");
			if (db == null) throw new ArgumentNullException("db");
			if (name == null) throw new ArgumentNullException("name");
			return db.ReadAsync((tr) => directory.OpenAsync(tr, new[] { name }, layer), cancellationToken);
		}

		/// <summary>Opens the sub-directory with the given <paramref name="name"/>.
		/// An error is raised if the directory does not exist, or if a layer is specified and a different layer was specified when the directory was created.
		/// </summary>
		public static Task<FdbDirectorySubspace> OpenAsync(this IFdbDirectory directory, IFdbReadOnlyTransaction trans, string name, Slice layer = default(Slice))
		{
			if (directory == null) throw new ArgumentNullException("directory");
			if (trans == null) throw new ArgumentNullException("trans");
			if (name == null) throw new ArgumentNullException("name");
			return directory.OpenAsync(trans, new[] { name }, layer);
		}

		/// <summary>Attempts to open the directory with the given <paramref name="path"/>.</summary>
		public static Task<FdbDirectorySubspace> TryOpenAsync(this IFdbDirectory directory, IFdbReadOnlyTransactional db, IEnumerable<string> path, CancellationToken cancellationToken)
		{
			if (directory == null) throw new ArgumentNullException("directory");
			if (db == null) throw new ArgumentNullException("db");
			if (path == null) throw new ArgumentNullException("path");
			return db.ReadAsync((tr) => directory.TryOpenAsync(tr, path, Slice.Nil), cancellationToken);
		}

		/// <summary>Attempts to open the directory with the given <paramref name="path"/>.</summary>
		public static Task<FdbDirectorySubspace> TryOpenAsync(this IFdbDirectory directory, IFdbReadOnlyTransactional db, IEnumerable<string> path, Slice layer, CancellationToken cancellationToken)
		{
			if (directory == null) throw new ArgumentNullException("directory");
			if (db == null) throw new ArgumentNullException("db");
			if (path == null) throw new ArgumentNullException("path");
			return db.ReadAsync((tr) => directory.TryOpenAsync(tr, path, layer), cancellationToken);
		}

		/// <summary>Attempts to open the directory with the given <paramref name="name"/>.</summary>
		public static Task<FdbDirectorySubspace> TryOpenAsync(this IFdbDirectory directory, IFdbReadOnlyTransactional db, string name, CancellationToken cancellationToken)
		{
			if (directory == null) throw new ArgumentNullException("directory");
			if (db == null) throw new ArgumentNullException("db");
			if (name == null) throw new ArgumentNullException("name");
			return db.ReadAsync((tr) => directory.TryOpenAsync(tr, new[] { name }, Slice.Nil), cancellationToken);
		}

		/// <summary>Attempts to open the directory with the given <paramref name="name"/>.</summary>
		public static Task<FdbDirectorySubspace> TryOpenAsync(this IFdbDirectory directory, IFdbReadOnlyTransactional db, string name, Slice layer, CancellationToken cancellationToken)
		{
			if (directory == null) throw new ArgumentNullException("directory");
			if (db == null) throw new ArgumentNullException("db");
			if (name == null) throw new ArgumentNullException("name");
			return db.ReadAsync((tr) => directory.TryOpenAsync(tr, new[] { name }, layer), cancellationToken);
		}

		/// <summary>Attempts to open the directory with the given <paramref name="name"/>.</summary>
		public static Task<FdbDirectorySubspace> TryOpenAsync(this IFdbDirectory directory, IFdbReadOnlyTransaction trans, string name, CancellationToken cancellationToken)
		{
			if (directory == null) throw new ArgumentNullException("directory");
			if (trans == null) throw new ArgumentNullException("trans");
			if (name == null) throw new ArgumentNullException("name");
			return directory.TryOpenAsync(trans, new[] { name }, Slice.Nil);
		}

		/// <summary>Attempts to open the directory with the given <paramref name="name"/>.</summary>
		public static Task<FdbDirectorySubspace> TryOpenAsync(this IFdbDirectory directory, IFdbReadOnlyTransaction trans, string name, Slice layer, CancellationToken cancellationToken)
		{
			if (directory == null) throw new ArgumentNullException("directory");
			if (trans == null) throw new ArgumentNullException("trans");
			if (name == null) throw new ArgumentNullException("name");
			return directory.TryOpenAsync(trans, new[] { name }, layer);
		}

		#endregion

		#region Move / TryMove...

		/// <summary>Moves the directory found at <paramref name="oldPath"/> to <paramref name="newPath"/>.
		/// There is no effect on the physical prefix of the given directory, or on clients that already have the directory open.
		/// An error is raised if the old directory does not exist, a directory already exists at `new_path`, or the parent directory of `new_path` does not exist.
		/// </summary>
		public static Task<FdbDirectorySubspace> MoveAsync(this IFdbDirectory directory, IFdbTransactional db, IEnumerable<string> oldPath, IEnumerable<string> newPath, CancellationToken cancellationToken)
		{
			if (directory == null) throw new ArgumentNullException("directory");
			if (db == null) throw new ArgumentNullException("db");
			if (oldPath == null) throw new ArgumentNullException("oldPath");
			if (newPath == null) throw new ArgumentNullException("newPath");
			return db.ReadWriteAsync((tr) => directory.MoveAsync(tr, oldPath, newPath), cancellationToken);
		}

		/// <summary>Attempts to move the directory found at <paramref name="oldPath"/> to <paramref name="newPath"/>.
		/// There is no effect on the physical prefix of the given directory, or on clients that already have the directory open.
		/// </summary>
		public static Task<FdbDirectorySubspace> TryMoveAsync(this IFdbDirectory directory, IFdbTransactional db, IEnumerable<string> oldPath, IEnumerable<string> newPath, CancellationToken cancellationToken)
		{
			if (directory == null) throw new ArgumentNullException("directory");
			if (db == null) throw new ArgumentNullException("db");
			if (oldPath == null) throw new ArgumentNullException("oldPath");
			if (newPath == null) throw new ArgumentNullException("newPath");
			return db.ReadWriteAsync((tr) => directory.TryMoveAsync(tr, oldPath, newPath), cancellationToken);
		}

		#endregion

		#region MoveTo / TryMoveTo...

		/// <summary>Moves the current directory to <paramref name="newPath"/>.
		/// There is no effect on the physical prefix of the given directory, or on clients that already have the directory open.
		/// An error is raised if a directory already exists at `new_path`, or if the new path points to a child of the current directory.
		/// </summary>
		public static Task<FdbDirectorySubspace> MoveToAsync(this FdbDirectorySubspace subspace, IFdbTransactional db, IEnumerable<string> newPath, CancellationToken cancellationToken)
		{
			if (subspace == null) throw new ArgumentNullException("subspace");
			if (db == null) throw new ArgumentNullException("db");
			if (newPath == null) throw new ArgumentNullException("newPath");
			return db.ReadWriteAsync((tr) => subspace.MoveToAsync(tr, newPath), cancellationToken);
		}

		/// <summary>Attempts to move the current directory to <paramref name="newPath"/>.
		/// There is no effect on the physical prefix of the given directory, or on clients that already have the directory open.
		/// </summary>
		public static Task<FdbDirectorySubspace> TryMoveToAsync(this FdbDirectorySubspace subspace, IFdbTransactional db, IEnumerable<string> newPath, CancellationToken cancellationToken)
		{
			if (subspace == null) throw new ArgumentNullException("subspace");
			if (db == null) throw new ArgumentNullException("db");
			if (newPath == null) throw new ArgumentNullException("newPath");
			return db.ReadWriteAsync((tr) => subspace.TryMoveToAsync(tr, newPath), cancellationToken);
		}
	
		#endregion

		#region Remove / TryRemove...

		/// <summary>Removes the directory, its contents, and all subdirectories.
		/// Warning: Clients that have already opened the directory might still insert data into its contents after it is removed.
		/// </summary>
		public static Task RemoveAsync(this IFdbDirectory directory, IFdbTransactional db, IEnumerable<string> path, CancellationToken cancellationToken)
		{
			if (directory == null) throw new ArgumentNullException("directory");
			if (db == null) throw new ArgumentNullException("db");
			return db.ReadWriteAsync((tr) => directory.RemoveAsync(tr, path), cancellationToken);
		}

		/// <summary>Removes the directory, its contents, and all subdirectories.
		/// Warning: Clients that have already opened the directory might still insert data into its contents after it is removed.
		/// </summary>
		public static Task RemoveAsync(this IFdbDirectory directory, IFdbTransactional db, string name, CancellationToken cancellationToken)
		{
			if (directory == null) throw new ArgumentNullException("directory");
			if (db == null) throw new ArgumentNullException("db");
			if (name == null) throw new ArgumentNullException("name");
			return db.ReadWriteAsync((tr) => directory.RemoveAsync(tr, new [] { name }), cancellationToken);
		}

		/// <summary>Removes the directory, its contents, and all subdirectories.
		/// Warning: Clients that have already opened the directory might still insert data into its contents after it is removed.
		/// </summary>
		public static Task RemoveAsync(this IFdbDirectory directory, IFdbTransactional db, CancellationToken cancellationToken)
		{
			if (directory == null) throw new ArgumentNullException("directory");
			if (db == null) throw new ArgumentNullException("db");
			return db.ReadWriteAsync((tr) => directory.RemoveAsync(tr), cancellationToken);
		}

		/// <summary>Removes the directory, its contents, and all subdirectories.
		/// Warning: Clients that have already opened the directory might still insert data into its contents after it is removed.
		/// </summary>
		public static Task RemoveAsync(this IFdbDirectory directory, IFdbTransaction trans, string name)
		{
			if (directory == null) throw new ArgumentNullException("directory");
			if (trans == null) throw new ArgumentNullException("trans");
			if (name == null) throw new ArgumentNullException("name");
			return directory.RemoveAsync(trans, new[] { name });
		}

		/// <summary>Removes the directory, its contents, and all subdirectories.
		/// Warning: Clients that have already opened the directory might still insert data into its contents after it is removed.
		/// </summary>
		public static Task<bool> TryRemoveAsync(this IFdbDirectory directory, IFdbTransactional db, IEnumerable<string> path, CancellationToken cancellationToken)
		{
			if (directory == null) throw new ArgumentNullException("directory");
			if (db == null) throw new ArgumentNullException("db");
			return db.ReadWriteAsync((tr) => directory.TryRemoveAsync(tr, path), cancellationToken);
		}

		/// <summary>Removes the directory, its contents, and all subdirectories.
		/// Warning: Clients that have already opened the directory might still insert data into its contents after it is removed.
		/// </summary>
		public static Task<bool> TryRemoveAsync(this IFdbDirectory directory, IFdbTransactional db, string name, CancellationToken cancellationToken)
		{
			if (directory == null) throw new ArgumentNullException("directory");
			if (db == null) throw new ArgumentNullException("db");
			if (name == null) throw new ArgumentNullException("name");
			return db.ReadWriteAsync((tr) => directory.TryRemoveAsync(tr, new [] { name }), cancellationToken);
		}

		/// <summary>Removes the directory, its contents, and all subdirectories.
		/// Warning: Clients that have already opened the directory might still insert data into its contents after it is removed.
		/// </summary>
		public static Task<bool> TryRemoveAsync(this IFdbDirectory directory, IFdbTransaction trans, string name)
		{
			if (directory == null) throw new ArgumentNullException("directory");
			if (trans == null) throw new ArgumentNullException("trans");
			if (name == null) throw new ArgumentNullException("name");
			return directory.TryRemoveAsync(trans, new[] { name });
		}

		#endregion

		#region Exists...

		/// <summary>Checks if a directory already exists</summary>
		/// <returns>Returns true if the directory exists, otherwise false.</returns>
		public static Task<bool> ExistsAsync(this IFdbDirectory directory, IFdbReadOnlyTransactional db, IEnumerable<string> path, CancellationToken cancellationToken)
		{
			if (directory == null) throw new ArgumentNullException("directory");
			if (db == null) throw new ArgumentNullException("db");
			return db.ReadAsync((tr) => directory.ExistsAsync(tr, path), cancellationToken);
		}

		/// <summary>Checks if a directory already exists</summary>
		/// <returns>Returns true if the directory exists, otherwise false.</returns>
		public static Task<bool> ExistsAsync(this IFdbDirectory directory, IFdbReadOnlyTransactional db, string name, CancellationToken cancellationToken)
		{
			if (directory == null) throw new ArgumentNullException("directory");
			if (db == null) throw new ArgumentNullException("db");
			if (name == null) throw new ArgumentNullException("name");
			return db.ReadAsync((tr) => directory.ExistsAsync(tr, new[] { name }), cancellationToken);
		}

		/// <summary>Checks if a directory already exists</summary>
		/// <returns>Returns true if the directory exists, otherwise false.</returns>
		public static Task<bool> ExistsAsync(this IFdbDirectory directory, IFdbReadOnlyTransaction trans, string name)
		{
			if (directory == null) throw new ArgumentNullException("directory");
			if (trans == null) throw new ArgumentNullException("trans");
			if (name == null) throw new ArgumentNullException("name");
			return directory.ExistsAsync(trans, new[] { name });
		}

		/// <summary>Checks if this directory exists</summary>
		/// <returns>Returns true if the directory exists, otherwise false.</returns>
		public static Task<bool> ExistsAsync(this FdbDirectorySubspace subspace, IFdbReadOnlyTransactional db, CancellationToken cancellationToken)
		{
			if (subspace == null) throw new ArgumentNullException("subspace");
			if (db == null) throw new ArgumentNullException("db");
			return db.ReadAsync((tr) => subspace.ExistsAsync(tr), cancellationToken);
		}

		#endregion

		#region List / TryList...

		/// <summary>Returns the list of subdirectories of directory at <paramref name="path"/>.</summary>
		public static Task<List<string>> ListAsync(this IFdbDirectory directory, IFdbReadOnlyTransactional db, IEnumerable<string> path, CancellationToken cancellationToken)
		{
			if (directory == null) throw new ArgumentNullException("directory");
			if (db == null) throw new ArgumentNullException("db");
			if (path == null) throw new ArgumentNullException("path");
			return db.ReadAsync((tr) => directory.ListAsync(tr, path), cancellationToken);
		}

		/// <summary>Returns the list of subdirectories of the sub-directory with the given <paramref name="name"/>.</summary>
		public static Task<List<string>> ListAsync(this IFdbDirectory directory, IFdbReadOnlyTransactional db, string name, CancellationToken cancellationToken)
		{
			if (directory == null) throw new ArgumentNullException("directory");
			if (db == null) throw new ArgumentNullException("db");
			if (name == null) throw new ArgumentNullException("name");
			return db.ReadAsync((tr) => directory.ListAsync(tr, new [] { name }), cancellationToken);
		}

		/// <summary>Returns the list of subdirectories of the current directory.</summary>
		public static Task<List<string>> ListAsync(this IFdbDirectory directory, IFdbReadOnlyTransactional db, CancellationToken cancellationToken)
		{
			if (directory == null) throw new ArgumentNullException("directory");
			if (db == null) throw new ArgumentNullException("db");
			return db.ReadAsync((tr) => directory.ListAsync(tr), cancellationToken);
		}

		/// <summary>Returns the list of subdirectories of the current directory.</summary>
		public static Task<List<string>> ListAsync(this IFdbDirectory directory, IFdbReadOnlyTransaction trans)
		{
			if (directory == null) throw new ArgumentNullException("directory");
			if (trans == null) throw new ArgumentNullException("trans");
			return directory.ListAsync(trans);
		}

		/// <summary>Returns the list of subdirectories of the sub-directory with the given <paramref name="name"/>.</summary>
		public static Task<List<string>> ListAsync(this IFdbDirectory directory, IFdbReadOnlyTransaction trans, string name)
		{
			if (directory == null) throw new ArgumentNullException("directory");
			if (trans == null) throw new ArgumentNullException("trans");
			if (name == null) throw new ArgumentNullException("name");
			return directory.ListAsync(trans, new[] { name });
		}

		/// <summary>Returns the list of subdirectories of directory at <paramref name="path"/>, if it exists</summary>
		public static Task<List<string>> TryListAsync(this IFdbDirectory directory, IFdbReadOnlyTransactional db, IEnumerable<string> path, CancellationToken cancellationToken)
		{
			if (directory == null) throw new ArgumentNullException("directory");
			if (db == null) throw new ArgumentNullException("db");
			if (path == null) throw new ArgumentNullException("path");
			return db.ReadAsync((tr) => directory.TryListAsync(tr, path), cancellationToken);
		}

		/// <summary>Returns the list of subdirectories of the sub-directory with the given <paramref name="name"/>, if it exists</summary>
		public static Task<List<string>> TryListAsync(this IFdbDirectory directory, IFdbReadOnlyTransactional db, string name, CancellationToken cancellationToken)
		{
			if (directory == null) throw new ArgumentNullException("directory");
			if (db == null) throw new ArgumentNullException("db");
			if (name == null) throw new ArgumentNullException("name");
			return db.ReadAsync((tr) => directory.TryListAsync(tr, new [] { name }), cancellationToken);
		}

		/// <summary>Returns the list of subdirectories of the sub-directory with the given <paramref name="name"/>, if it exists</summary>
		public static Task<List<string>> TryListAsync(this IFdbDirectory directory, IFdbReadOnlyTransaction trans, string name)
		{
			if (directory == null) throw new ArgumentNullException("directory");
			if (trans == null) throw new ArgumentNullException("trans");
			if (name == null) throw new ArgumentNullException("name");
			return directory.TryListAsync(trans, new[] { name });
		}

		/// <summary>Returns the list of all the subdirectories of the current directory.</summary>
		public static Task<List<string>> ListAsync(this FdbDirectorySubspace subspace, IFdbReadOnlyTransactional db, CancellationToken cancellationToken)
		{
			if (subspace == null) throw new ArgumentNullException("subspace");
			if (db == null) throw new ArgumentNullException("db");
			return db.ReadAsync((tr) => subspace.ListAsync(tr), cancellationToken);
		}

		/// <summary>Returns the list of all the subdirectories of the current directory, it it exists.</summary>
		public static Task<List<string>> TryListAsync(this FdbDirectorySubspace subspace, IFdbReadOnlyTransactional db, CancellationToken cancellationToken)
		{
			if (subspace == null) throw new ArgumentNullException("subspace");
			if (db == null) throw new ArgumentNullException("db");
			return db.ReadAsync((tr) => subspace.TryListAsync(tr), cancellationToken);
		}

		#endregion

		#region Metadata

		/// <summary>Change the layer id of the directory at <param name="path"/></summary>
		public static Task<FdbDirectorySubspace> ChangeLayerAsync(this FdbDirectoryLayer directory, IFdbTransactional db, IEnumerable<string> path, Slice newLayer, CancellationToken cancellationToken)
		{
			if (directory == null) throw new ArgumentNullException("directory");
			if (db == null) throw new ArgumentNullException("db");
			if (path == null) throw new ArgumentNullException("path");
			return db.ReadWriteAsync((tr) => directory.ChangeLayerAsync(tr, path, newLayer), cancellationToken);
		}

		/// <summary>Change the layer id of this directory</summary>
		public static Task<FdbDirectorySubspace> ChangeLayerAsync(this FdbDirectorySubspace subspace, IFdbTransactional db, Slice newLayer, CancellationToken cancellationToken)
		{
			if (subspace == null) throw new ArgumentNullException("subspace");
			if (db == null) throw new ArgumentNullException("db");
			return db.ReadWriteAsync((tr) => subspace.ChangeLayerAsync(tr, newLayer), cancellationToken);
		}

		#endregion

	}

}
