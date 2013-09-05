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
	using System.Threading.Tasks;

	public static class FdbDirectoryExtensions
	{

		public static FdbDirectoryLayer OpenDirectory(this FdbDatabase db)
		{
			if (db == null) throw new ArgumentNullException("db");
			// returns a Directory that uses the namespace of the DB for the content, and stores the node under the "\xFE" prefix.
			return new FdbDirectoryLayer(db.Partition(FdbTupleAlias.Directory), db.GlobalSpace);
		}

		public static FdbDirectoryLayer OpenDirectory(this FdbDatabase db, FdbSubspace global)
		{
			if (db == null) throw new ArgumentNullException("db");
			if (global == null) throw new ArgumentNullException("global");

			return new FdbDirectoryLayer(global.Partition(FdbTupleAlias.Directory), global);
		}

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
		public static Task<FdbDirectorySubspace> CreateOrOpenAsync(this FdbDirectoryLayer directory, FdbDatabase db, IFdbTuple path, string layer = null, Slice prefix = default(Slice))
		{
			return db.Attempt.ChangeAsync((tr) => directory.CreateOrOpenAsync(tr, path, layer, prefix));
		}

		public static Task<FdbDirectorySubspace> CreateOrOpenAsync(this FdbDirectorySubspace subspace, FdbDatabase db, IFdbTuple path, string layer = null, Slice prefix = default(Slice))
		{
			return db.Attempt.ChangeAsync((tr) => subspace.CreateOrOpenAsync(tr, path, layer, prefix));
		}

		/// <summary>
		/// Creates a directory with the given <paramref name="path"/> (creating parent directories if necessary).
		/// An error is raised if the given directory already exists.
		/// If <paramref name="prefix"/> is specified, the directory is created with the given physical prefix; otherwise a prefix is allocated automatically.
		/// If <paramref name="layer"/> is specified, it is recorded with the directory and will be checked by future calls to open.
		/// </summary>
		public static Task<FdbDirectorySubspace> CreateAsync(this FdbDirectoryLayer directory, FdbDatabase db, IFdbTuple path, string layer = null, Slice prefix = default(Slice))
		{
			return db.Attempt.ChangeAsync((tr) => directory.CreateAsync(tr, path, layer, prefix));
		}

		public static Task<FdbDirectorySubspace> CreateAsync(this FdbDirectorySubspace subspace, FdbDatabase db, IFdbTuple path, string layer = null, Slice prefix = default(Slice))
		{
			return db.Attempt.ChangeAsync((tr) => subspace.CreateAsync(tr, path, layer, prefix));
		}

		/// <summary>
		/// Opens the directory with the given <paramref name="path"/>.
		/// An error is raised if the directory does not exist, or if a layer is specified and a different layer was specified when the directory was created.
		/// </summary>
		public static Task<FdbDirectorySubspace> OpenAsync(this FdbDirectoryLayer directory, FdbDatabase db, IFdbTuple path, string layer = null)
		{
			// note: we will not write to the transaction
			return db.Attempt.ChangeAsync((tr) => directory.OpenAsync(tr, path, layer));
		}

		public static Task<FdbDirectorySubspace> OpenAsync(this FdbDirectorySubspace subspace, FdbDatabase db, IFdbTuple path, string layer = null)
		{
			// note: we will not write to the transaction
			return db.Attempt.ChangeAsync((tr) => subspace.OpenAsync(tr, path, layer));
		}

		/// <summary>
		/// Moves the directory found at <paramref name="oldPath"/> to <paramref name="newPath"/>.
		/// There is no effect on the physical prefix of the given directory, or on clients that already have the directory open.
		/// An error is raised if the old directory does not exist, a directory already exists at `new_path`, or the parent directory of `new_path` does not exist.
		/// </summary>
		public static Task<FdbDirectorySubspace> MoveAsync(this FdbDirectoryLayer directory, FdbDatabase db, IFdbTuple oldPath, IFdbTuple newPath)
		{
			return db.Attempt.ChangeAsync((tr) => directory.MoveAsync(tr, oldPath, newPath));
		}

		public static Task<FdbDirectorySubspace> MoveAsync(this FdbDirectorySubspace subspace, FdbDatabase db, IFdbTuple newPath)
		{
			return db.Attempt.ChangeAsync((tr) => subspace.MoveAsync(tr, newPath));
		}

		/// <summary>
		/// Removes the directory, its contents, and all subdirectories.
		/// Warning: Clients that have already opened the directory might still insert data into its contents after it is removed.
		/// </summary>
		public static Task RemoveAsync(this FdbDirectoryLayer directory, FdbDatabase db, IFdbTuple path)
		{
			return db.Attempt.ChangeAsync((tr) => directory.RemoveAsync(tr, path));
		}

		public static Task RemoveAsync(this FdbDirectorySubspace subspace, FdbDatabase db)
		{
			return db.Attempt.ChangeAsync((tr) => subspace.RemoveAsync(tr));
		}

		public static Task<List<IFdbTuple>> ListAsync(this FdbDirectoryLayer directory, FdbDatabase db, IFdbTuple path)
		{
			return db.Attempt.ReadAsync((tr) => directory.ListAsync(tr, path));
		}

		public static Task<List<IFdbTuple>> ListAsync(this FdbDirectorySubspace subspace, FdbDatabase db)
		{
			return db.Attempt.ReadAsync((tr) => subspace.ListAsync(tr));
		}
	}

}
