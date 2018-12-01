#region BSD License
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
N ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT
(INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS
SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
 */
#endregion

namespace FoundationDB.Layers.Directories
{
	using System;
	using System.Collections.Generic;
	using System.Threading;
	using System.Threading.Tasks;
	using Doxense.Diagnostics.Contracts;
	using FoundationDB.Client;
	using JetBrains.Annotations;

	/// <summary>Helper methods related to the Directory Layer</summary>
	[PublicAPI]
	public static class FdbDirectoryExtensions
	{

		#region CreateOrOpen...

		/// <summary>Opens the directory with the given <paramref name="path"/>.
		/// If the directory does not exist, it is created (creating parent directories if necessary).
		/// If layer is specified, it is checked against the layer of an existing directory or set as the layer of a new directory.
		/// </summary>
		public static Task<FdbDirectorySubspace> CreateOrOpenAsync([NotNull] this IFdbDirectory directory, [NotNull] IFdbRetryable db, FdbDirectoryPath path, CancellationToken ct)
		{
			Contract.NotNull(directory, nameof(directory));
			Contract.NotNull(db, nameof(db));

			return db.ReadWriteAsync((tr) => directory.CreateOrOpenAsync(tr, path, Slice.Nil), ct);
		}

		/// <summary>Opens the directory with the given <paramref name="path"/>.
		/// If the directory does not exist, it is created (creating parent directories if necessary).
		/// If layer is specified, it is checked against the layer of an existing directory or set as the layer of a new directory.
		/// </summary>
		public static Task<FdbDirectorySubspace> CreateOrOpenAsync([NotNull] this IFdbDirectory directory, [NotNull] IFdbRetryable db, FdbDirectoryPath path, Slice layer, CancellationToken ct)
		{
			Contract.NotNull(directory, nameof(directory));
			Contract.NotNull(db, nameof(db));

			return db.ReadWriteAsync((tr) => directory.CreateOrOpenAsync(tr, path, layer), ct);
		}

		/// <summary>Opens the directory with the given <paramref name="path"/>.
		/// If the directory does not exist, and if <paramref name="readOnly"/> is false, it is created. Otherwise, this method returns null.
		/// If the <paramref name="layer"/> is specified, it is checked against the layer of an existing directory or set as the layer of a new directory.
		/// </summary>
		/// <param name="directory">Parent directory</param>
		/// <param name="trans">Transaction used by the operation</param>
		/// <param name="path">Path to the directory to open or create</param>
		/// <param name="readOnly">If true, do not make any modifications to the database, and return null if the directory does not exist.</param>
		/// <param name="layer">Optional layer ID that is checked with the opened directory.</param>
		/// <returns></returns>
		public static Task<FdbDirectorySubspace> TryCreateOrOpenAsync([NotNull] this IFdbDirectory directory, [NotNull] IFdbTransaction trans, FdbDirectoryPath path, bool readOnly, Slice layer = default)
		{
			Contract.NotNull(directory, nameof(directory));
			Contract.NotNull(trans, nameof(trans));

			return readOnly ? directory.TryOpenAsync(trans, path, layer) : directory.CreateOrOpenAsync(trans, path, layer);
		}

		#endregion

		#region Create / TryCreate...

		/// <summary>Creates a directory with the given <paramref name="path"/> (creating parent directories if necessary).
		/// An error is raised if the given directory already exists.
		/// </summary>
		public static Task<FdbDirectorySubspace> CreateAsync([NotNull] this IFdbDirectory directory, [NotNull] IFdbRetryable db, FdbDirectoryPath path, CancellationToken ct)
		{
			Contract.NotNull(directory, nameof(directory));
			Contract.NotNull(db, nameof(db));

			return db.ReadWriteAsync((tr) => directory.CreateAsync(tr, path, Slice.Nil), ct);
		}

		/// <summary>Creates a directory with the given <paramref name="path"/> (creating parent directories if necessary).
		/// An error is raised if the given directory already exists.
		/// If <paramref name="layer"/> is specified, it is recorded with the directory and will be checked by future calls to open.
		/// </summary>
		public static Task<FdbDirectorySubspace> CreateAsync([NotNull] this IFdbDirectory directory, [NotNull] IFdbRetryable db, FdbDirectoryPath path, Slice layer, CancellationToken ct)
		{
			Contract.NotNull(directory, nameof(directory));
			Contract.NotNull(db, nameof(db));

			return db.ReadWriteAsync((tr) => directory.CreateAsync(tr, path, layer), ct);
		}

		/// <summary>Attempts to create a directory with the given <paramref name="path"/> (creating parent directories if necessary).</summary>
		public static Task<FdbDirectorySubspace> TryCreateAsync([NotNull] this IFdbDirectory directory, [NotNull] IFdbRetryable db, FdbDirectoryPath path, CancellationToken ct)
		{
			Contract.NotNull(directory, nameof(directory));
			Contract.NotNull(db, nameof(db));

			return db.ReadWriteAsync((tr) => directory.TryCreateAsync(tr, path, Slice.Nil), ct);
		}

		/// <summary>Attempts to create a directory with the given <paramref name="path"/> (creating parent directories if necessary).
		/// If <paramref name="layer"/> is specified, it is recorded with the directory and will be checked by future calls to open.
		/// </summary>
		public static Task<FdbDirectorySubspace> TryCreateAsync([NotNull] this IFdbDirectory directory, [NotNull] IFdbRetryable db, FdbDirectoryPath path, Slice layer, CancellationToken ct)
		{
			Contract.NotNull(directory, nameof(directory));
			Contract.NotNull(db, nameof(db));

			return db.ReadWriteAsync((tr) => directory.TryCreateAsync(tr, path, layer), ct);
		}

		#endregion

		#region Open / TryOpen...

		/// <summary>Opens the directory with the given <paramref name="path"/>.
		/// An error is raised if the directory does not exist.
		/// </summary>
		public static Task<FdbDirectorySubspace> OpenAsync([NotNull] this IFdbDirectory directory, [NotNull] IFdbRetryable db, FdbDirectoryPath path, CancellationToken ct)
		{
			Contract.NotNull(directory, nameof(directory));
			Contract.NotNull(db, nameof(db));

			return db.ReadAsync((tr) => directory.OpenAsync(tr, path, Slice.Nil), ct);
		}

		/// <summary>Opens the directory with the given <paramref name="path"/>.
		/// An error is raised if the directory does not exist, or if a layer is specified and a different layer was specified when the directory was created.
		/// </summary>
		public static Task<FdbDirectorySubspace> OpenAsync([NotNull] this IFdbDirectory directory, [NotNull] IFdbRetryable db, FdbDirectoryPath path, Slice layer, CancellationToken ct)
		{
			Contract.NotNull(directory, nameof(directory));
			Contract.NotNull(db, nameof(db));

			return db.ReadAsync((tr) => directory.OpenAsync(tr, path, layer), ct);
		}

		/// <summary>Attempts to open the directory with the given <paramref name="path"/>.</summary>
		public static Task<FdbDirectorySubspace> TryOpenAsync([NotNull] this IFdbDirectory directory, [NotNull] IFdbReadOnlyRetryable db, FdbDirectoryPath path, CancellationToken ct)
		{
			Contract.NotNull(directory, nameof(directory));
			Contract.NotNull(db, nameof(db));

			return db.ReadAsync((tr) => directory.TryOpenAsync(tr, path, Slice.Nil), ct);
		}

		/// <summary>Attempts to open the directory with the given <paramref name="path"/>.</summary>
		public static Task<FdbDirectorySubspace> TryOpenAsync([NotNull] this IFdbDirectory directory, [NotNull] IFdbReadOnlyRetryable db, FdbDirectoryPath path, Slice layer, CancellationToken ct)
		{
			Contract.NotNull(directory, nameof(directory));
			Contract.NotNull(db, nameof(db));

			return db.ReadAsync((tr) => directory.TryOpenAsync(tr, path, layer), ct);
		}

		#endregion

		#region Move / TryMove...

		/// <summary>Moves the directory found at <paramref name="oldPath"/> to <paramref name="newPath"/>.
		/// There is no effect on the physical prefix of the given directory, or on clients that already have the directory open.
		/// An error is raised if the old directory does not exist, a directory already exists at `new_path`, or the parent directory of `new_path` does not exist.
		/// </summary>
		public static Task<FdbDirectorySubspace> MoveAsync([NotNull] this IFdbDirectory directory, [NotNull] IFdbRetryable db, FdbDirectoryPath oldPath, FdbDirectoryPath newPath, CancellationToken ct)
		{
			Contract.NotNull(directory, nameof(directory));
			Contract.NotNull(db, nameof(db));

			return db.ReadWriteAsync((tr) => directory.MoveAsync(tr, oldPath, newPath), ct);
		}

		/// <summary>Attempts to move the directory found at <paramref name="oldPath"/> to <paramref name="newPath"/>.
		/// There is no effect on the physical prefix of the given directory, or on clients that already have the directory open.
		/// </summary>
		public static Task<FdbDirectorySubspace> TryMoveAsync([NotNull] this IFdbDirectory directory, [NotNull] IFdbRetryable db, FdbDirectoryPath oldPath, FdbDirectoryPath newPath, CancellationToken ct)
		{
			Contract.NotNull(directory, nameof(directory));
			Contract.NotNull(db, nameof(db));

			return db.ReadWriteAsync((tr) => directory.TryMoveAsync(tr, oldPath, newPath), ct);
		}

		#endregion

		#region MoveTo / TryMoveTo...

		/// <summary>Moves the current directory to <paramref name="newPath"/>.
		/// There is no effect on the physical prefix of the given directory, or on clients that already have the directory open.
		/// An error is raised if a directory already exists at `new_path`, or if the new path points to a child of the current directory.
		/// </summary>
		public static Task<FdbDirectorySubspace> MoveToAsync([NotNull] this FdbDirectorySubspace subspace, [NotNull] IFdbRetryable db, FdbDirectoryPath newPath, CancellationToken ct)
		{
			Contract.NotNull(subspace, nameof(subspace));
			Contract.NotNull(db, nameof(db));

			return db.ReadWriteAsync((tr) => subspace.MoveToAsync(tr, newPath), ct);
		}

		/// <summary>Attempts to move the current directory to <paramref name="newPath"/>.
		/// There is no effect on the physical prefix of the given directory, or on clients that already have the directory open.
		/// </summary>
		public static Task<FdbDirectorySubspace> TryMoveToAsync([NotNull] this FdbDirectorySubspace subspace, [NotNull] IFdbRetryable db, FdbDirectoryPath newPath, CancellationToken ct)
		{
			Contract.NotNull(subspace, nameof(subspace));
			Contract.NotNull(db, nameof(db));

			return db.ReadWriteAsync((tr) => subspace.TryMoveToAsync(tr, newPath), ct);
		}

		#endregion

		#region Remove / TryRemove...

		/// <summary>Removes the directory, its contents, and all subdirectories.
		/// Warning: Clients that have already opened the directory might still insert data into its contents after it is removed.
		/// </summary>
		public static Task RemoveAsync([NotNull] this IFdbDirectory directory, [NotNull] IFdbRetryable db, FdbDirectoryPath path, CancellationToken ct)
		{
			Contract.NotNull(directory, nameof(directory));
			Contract.NotNull(db, nameof(db));

			return db.ReadWriteAsync((tr) => directory.RemoveAsync(tr, path), ct);
		}

		/// <summary>Removes the directory, its contents, and all subdirectories.
		/// Warning: Clients that have already opened the directory might still insert data into its contents after it is removed.
		/// </summary>
		public static Task RemoveAsync([NotNull] this IFdbDirectory directory, [NotNull] IFdbRetryable db, CancellationToken ct)
		{
			Contract.NotNull(directory, nameof(directory));
			Contract.NotNull(db, nameof(db));

			return db.ReadWriteAsync((tr) => directory.RemoveAsync(tr), ct);
		}

		/// <summary>Removes the directory, its contents, and all subdirectories.
		/// Warning: Clients that have already opened the directory might still insert data into its contents after it is removed.
		/// </summary>
		public static Task<bool> TryRemoveAsync([NotNull] this IFdbDirectory directory, [NotNull] IFdbRetryable db, FdbDirectoryPath path, CancellationToken ct)
		{
			Contract.NotNull(directory, nameof(directory));
			Contract.NotNull(db, nameof(db));

			return db.ReadWriteAsync((tr) => directory.TryRemoveAsync(tr, path), ct);
		}

		/// <summary>Removes the directory, its contents, and all subdirectories.
		/// Warning: Clients that have already opened the directory might still insert data into its contents after it is removed.
		/// </summary>
		public static Task<bool> TryRemoveAsync([NotNull] this IFdbDirectory directory, [NotNull] IFdbRetryable db, [NotNull] string name, CancellationToken ct)
		{
			Contract.NotNull(directory, nameof(directory));
			Contract.NotNull(db, nameof(db));
			Contract.NotNull(name, nameof(name));

			return db.ReadWriteAsync((tr) => directory.TryRemoveAsync(tr, new [] { name }), ct);
		}

		/// <summary>Removes the directory, its contents, and all subdirectories.
		#endregion

		#region Exists...

		/// <summary>Checks if a directory already exists</summary>
		/// <returns>Returns true if the directory exists, otherwise false.</returns>
		public static Task<bool> ExistsAsync([NotNull] this IFdbDirectory directory, [NotNull] IFdbReadOnlyRetryable db, FdbDirectoryPath path, CancellationToken ct)
		{
			Contract.NotNull(directory, nameof(directory));
			Contract.NotNull(db, nameof(db));

			return db.ReadAsync((tr) => directory.ExistsAsync(tr, path), ct);
		}

		/// <summary>Checks if this directory exists</summary>
		/// <returns>Returns true if the directory exists, otherwise false.</returns>
		public static Task<bool> ExistsAsync([NotNull] this FdbDirectorySubspace subspace, [NotNull] IFdbReadOnlyRetryable db, CancellationToken ct)
		{
			Contract.NotNull(subspace, nameof(subspace));
			Contract.NotNull(db, nameof(db));

			return db.ReadAsync((tr) => subspace.ExistsAsync(tr), ct);
		}

		#endregion

		#region List / TryList...

		/// <summary>Returns the list of subdirectories of directory at <paramref name="path"/>.</summary>
		public static Task<List<string>> ListAsync([NotNull] this IFdbDirectory directory, [NotNull] IFdbReadOnlyRetryable db, FdbDirectoryPath path, CancellationToken ct)
		{
			Contract.NotNull(directory, nameof(directory));
			Contract.NotNull(db, nameof(db));

			return db.ReadAsync((tr) => directory.ListAsync(tr, path), ct);
		}

		/// <summary>Returns the list of subdirectories of the current directory.</summary>
		public static Task<List<string>> ListAsync([NotNull] this IFdbDirectory directory, [NotNull] IFdbReadOnlyRetryable db, CancellationToken ct)
		{
			Contract.NotNull(directory, nameof(directory));
			Contract.NotNull(db, nameof(db));
			return db.ReadAsync((tr) => directory.ListAsync(tr), ct);
		}

		/// <summary>Returns the list of subdirectories of directory at <paramref name="path"/>, if it exists</summary>
		public static Task<List<string>> TryListAsync([NotNull] this IFdbDirectory directory, [NotNull] IFdbReadOnlyRetryable db, FdbDirectoryPath path, CancellationToken ct)
		{
			Contract.NotNull(directory, nameof(directory));
			Contract.NotNull(db, nameof(db));

			return db.ReadAsync((tr) => directory.TryListAsync(tr, path), ct);
		}

		/// <summary>Returns the list of all the subdirectories of the current directory.</summary>
		public static Task<List<string>> ListAsync([NotNull] this FdbDirectorySubspace subspace, [NotNull] IFdbReadOnlyRetryable db, CancellationToken ct)
		{
			Contract.NotNull(subspace, nameof(subspace));
			Contract.NotNull(db, nameof(db));

			return db.ReadAsync((tr) => subspace.ListAsync(tr), ct);
		}

		/// <summary>Returns the list of all the subdirectories of the current directory, it it exists.</summary>
		public static Task<List<string>> TryListAsync([NotNull] this FdbDirectorySubspace subspace, [NotNull] IFdbReadOnlyRetryable db, CancellationToken ct)
		{
			Contract.NotNull(subspace, nameof(subspace));
			Contract.NotNull(db, nameof(db));

			return db.ReadAsync((tr) => subspace.TryListAsync(tr), ct);
		}

		#endregion

		#region Metadata

		/// <summary>Change the layer id of the <paramref name="directory"/> at <paramref name="path"/></summary>
		public static Task<FdbDirectorySubspace> ChangeLayerAsync([NotNull] this FdbDirectoryLayer directory, [NotNull] IFdbRetryable db, FdbDirectoryPath path, Slice newLayer, CancellationToken ct)
		{
			Contract.NotNull(directory, nameof(directory));
			Contract.NotNull(db, nameof(db));

			return db.ReadWriteAsync((tr) => directory.ChangeLayerAsync(tr, path, newLayer), ct);
		}

		/// <summary>Change the layer id of this directory</summary>
		public static Task<FdbDirectorySubspace> ChangeLayerAsync([NotNull] this FdbDirectorySubspace subspace, [NotNull] IFdbRetryable db, Slice newLayer, CancellationToken ct)
		{
			Contract.NotNull(subspace, nameof(subspace));
			Contract.NotNull(db, nameof(db));

			return db.ReadWriteAsync((tr) => subspace.ChangeLayerAsync(tr, newLayer), ct);
		}

		#endregion

	}

}
