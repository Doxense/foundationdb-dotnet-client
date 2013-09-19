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

	public static class FdbDirectoryExtensions
	{

		[Obsolete]
		public static FdbDirectoryLayer OpenDirectory(this FdbDatabase db)
		{
			if (db == null) throw new ArgumentNullException("db");

			// returns a Directory that uses the namespace of the DB for the content, and stores the node under the "\xFE" prefix.
			return new FdbDirectoryLayer(db.GlobalSpace[FdbKey.Directory], db.GlobalSpace);
		}

		[Obsolete]
		public static FdbDirectoryLayer OpenDirectory(this FdbDatabase db, FdbSubspace global)
		{
			if (db == null) throw new ArgumentNullException("db");
			if (global == null) throw new ArgumentNullException("global");

			if (!db.GlobalSpace.Contains(global.Key)) throw new ArgumentOutOfRangeException("global", "The directory subspace must be contained in the global namespace of the database");

			return new FdbDirectoryLayer(global[FdbKey.Directory], global);
		}

		[Obsolete]
		public static FdbDirectoryLayer OpenDirectory(this FdbDatabase db, IFdbTuple nodePrefix, IFdbTuple contentPrefix)
		{
			if (db == null) throw new ArgumentNullException("db");
			if (nodePrefix == null) throw new ArgumentNullException("nodePrefix");
			if (contentPrefix == null) throw new ArgumentNullException("contentPrefix");

			// note: both tuples are relative to the db global namespace
			return new FdbDirectoryLayer(db.GlobalSpace.Partition(nodePrefix), db.GlobalSpace.Partition(contentPrefix));
		}

		/// <summary>
		/// Opens the directory with the given path.
		/// If the directory does not exist, it is created (creating parent directories if necessary).
		/// If prefix is specified, the directory is created with the given physical prefix; otherwise a prefix is allocated automatically.
		/// If layer is specified, it is checked against the layer of an existing directory or set as the layer of a new directory.
		/// </summary>
		public static Task<FdbDirectorySubspace> CreateOrOpenAsync(this FdbDirectoryLayer directory, FdbDatabase db, IFdbTuple path, string layer = null, Slice prefix = default(Slice), bool allowCreate = true, bool allowOpen = true, CancellationToken ct = default(CancellationToken))
		{
			return db.ChangeAsync((tr, _ctx) => directory.CreateOrOpenAsync(tr, path, layer, prefix, allowCreate, allowOpen, _ctx.Token), ct);
		}

		public static Task<FdbDirectorySubspace> CreateOrOpenAsync(this FdbDirectorySubspace subspace, FdbDatabase db, IFdbTuple path, string layer = null, Slice prefix = default(Slice), bool allowCreate = true, bool allowOpen = true, CancellationToken ct = default(CancellationToken))
		{
			return db.ChangeAsync((tr, _ctx) => subspace.CreateOrOpenAsync(tr, path, layer, prefix, allowCreate, allowOpen, _ctx.Token), ct);
		}

		/// <summary>
		/// Creates a directory with the given <paramref name="path"/> (creating parent directories if necessary).
		/// An error is raised if the given directory already exists.
		/// If <paramref name="prefix"/> is specified, the directory is created with the given physical prefix; otherwise a prefix is allocated automatically.
		/// If <paramref name="layer"/> is specified, it is recorded with the directory and will be checked by future calls to open.
		/// </summary>
		public static Task<FdbDirectorySubspace> CreateAsync(this FdbDirectoryLayer directory, FdbDatabase db, IFdbTuple path, string layer = null, Slice prefix = default(Slice), CancellationToken ct = default(CancellationToken))
		{
			return db.ChangeAsync((tr, _ctx) => directory.CreateAsync(tr, path, layer, prefix, _ctx.Token), ct);
		}

		public static Task<FdbDirectorySubspace> CreateAsync(this FdbDirectorySubspace subspace, FdbDatabase db, IFdbTuple path, string layer = null, Slice prefix = default(Slice), CancellationToken ct = default(CancellationToken))
		{
			return db.ChangeAsync((tr, _ctx) => subspace.CreateAsync(tr, path, layer, prefix, _ctx.Token), ct);
		}

		/// <summary>
		/// Opens the directory with the given <paramref name="path"/>.
		/// An error is raised if the directory does not exist, or if a layer is specified and a different layer was specified when the directory was created.
		/// </summary>
		public static Task<FdbDirectorySubspace> OpenAsync(this FdbDirectoryLayer directory, FdbDatabase db, IFdbTuple path, string layer = null, CancellationToken ct = default(CancellationToken))
		{
			// note: we will not write to the transaction
			return db.ChangeAsync((tr, _ctx) => directory.OpenAsync(tr, path, layer, _ctx.Token), ct);
		}

		public static Task<FdbDirectorySubspace> OpenAsync(this FdbDirectorySubspace subspace, FdbDatabase db, IFdbTuple path, string layer = null, CancellationToken ct = default(CancellationToken))
		{
			// note: we will not write to the transaction
			return db.ChangeAsync((tr, _ctx) => subspace.OpenAsync(tr, path, layer, _ctx.Token), ct);
		}

		/// <summary>
		/// Moves the directory found at <paramref name="oldPath"/> to <paramref name="newPath"/>.
		/// There is no effect on the physical prefix of the given directory, or on clients that already have the directory open.
		/// An error is raised if the old directory does not exist, a directory already exists at `new_path`, or the parent directory of `new_path` does not exist.
		/// </summary>
		public static Task<FdbDirectorySubspace> MoveAsync(this FdbDirectoryLayer directory, FdbDatabase db, IFdbTuple oldPath, IFdbTuple newPath, CancellationToken ct = default(CancellationToken))
		{
			return db.ChangeAsync((tr, _ctx) => directory.MoveAsync(tr, oldPath, newPath, _ctx.Token), ct);
		}

		public static Task<FdbDirectorySubspace> MoveAsync(this FdbDirectorySubspace subspace, FdbDatabase db, IFdbTuple newPath, CancellationToken ct = default(CancellationToken))
		{
			return db.ChangeAsync((tr, _ctx) => subspace.MoveAsync(tr, newPath, _ctx.Token), ct);
		}

		/// <summary>
		/// Removes the directory, its contents, and all subdirectories.
		/// Warning: Clients that have already opened the directory might still insert data into its contents after it is removed.
		/// </summary>
		public static Task<bool> RemoveAsync(this FdbDirectoryLayer directory, FdbDatabase db, IFdbTuple path, CancellationToken ct = default(CancellationToken))
		{
			return db.ChangeAsync((tr, _ctx) => directory.RemoveAsync(tr, path, _ctx.Token), ct);
		}

		public static Task RemoveAsync(this FdbDirectorySubspace subspace, FdbDatabase db, CancellationToken ct = default(CancellationToken))
		{
			return db.ChangeAsync((tr, _ctx) => subspace.RemoveAsync(tr, _ctx.Token), ct);
		}

		public static Task<List<IFdbTuple>> ListAsync(this FdbDirectoryLayer directory, FdbDatabase db, IFdbTuple path, CancellationToken ct = default(CancellationToken))
		{
			return db.ReadAsync((tr, _ctx) => directory.ListAsync(tr, path, _ctx.Token), ct);
		}

		public static Task<List<IFdbTuple>> ListAsync(this FdbDirectorySubspace subspace, FdbDatabase db, CancellationToken ct = default(CancellationToken))
		{
			return db.ReadAsync((tr, _ctx) => subspace.ListAsync(tr, _ctx.Token), ct);
		}

		// string paths

		// helper methods

		public static Task<FdbDirectorySubspace> CreateOrOpenAsync(this FdbDirectoryLayer directory, FdbDatabase db, params string[] path)
		{
			if (directory == null) throw new ArgumentNullException("directory");
			if (path == null) throw new ArgumentNullException("path");
			return directory.CreateOrOpenAsync(db, FdbTuple.CreateRange(path, 0, path.Length));
		}

		public static Task<FdbDirectorySubspace> OpenAsync(this FdbDirectoryLayer directory, FdbDatabase db, params string[] path)
		{
			if (directory == null) throw new ArgumentNullException("directory");
			if (path == null) throw new ArgumentNullException("path");
			return directory.OpenAsync(db, FdbTuple.CreateRange(path, 0, path.Length));
		}

		public static Task<FdbDirectorySubspace> CreateAsync(this FdbDirectoryLayer directory, FdbDatabase db, params string[] path)
		{
			if (directory == null) throw new ArgumentNullException("directory");
			if (path == null) throw new ArgumentNullException("path");
			return directory.OpenAsync(db, FdbTuple.CreateRange(path, 0, path.Length));
		}

		public static Task<bool> RemoveAsync(this FdbDirectoryLayer directory, FdbDatabase db, params string[] path)
		{
			if (directory == null) throw new ArgumentNullException("directory");
			if (path == null) throw new ArgumentNullException("path");
			return directory.RemoveAsync(db, FdbTuple.CreateRange(path, 0, path.Length));
		}

		//

		public static Task<FdbDirectorySubspace> CreateOrOpenAsync(this FdbRootDirectory root, params string[] path)
		{
			if (root == null) throw new ArgumentNullException("root");
			if (path == null) throw new ArgumentNullException("path");
			return root.CreateOrOpenAsync(FdbTuple.CreateRange(path, 0, path.Length));
		}

		public static Task<FdbDirectorySubspace> OpenAsync(this FdbRootDirectory root, params string[] path)
		{
			if (root == null) throw new ArgumentNullException("root");
			if (path == null) throw new ArgumentNullException("path");
			return root.OpenAsync(FdbTuple.CreateRange(path, 0, path.Length));
		}

		public static Task<FdbDirectorySubspace> CreateAsync(this FdbRootDirectory root, params string[] path)
		{
			if (root == null) throw new ArgumentNullException("root");
			if (path == null) throw new ArgumentNullException("path");
			return root.OpenAsync(FdbTuple.CreateRange(path, 0, path.Length));
		}

		public static Task<bool> RemoveAsync(this FdbRootDirectory root, params string[] path)
		{
			if (root == null) throw new ArgumentNullException("root");
			if (path == null) throw new ArgumentNullException("path");
			return root.RemoveAsync(FdbTuple.CreateRange(path, 0, path.Length));
		}

	}

}
