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

namespace FoundationDB.Client
{
	using System;
	using System.Collections.Generic;
	using System.Diagnostics;
	using System.Threading;
	using System.Threading.Tasks;
	using FoundationDB.Layers.Directories;
	using JetBrains.Annotations;

	/// <summary>View of a database that is bound to a specific Partition</summary>
	[DebuggerDisplay("Database={Database.Name}, Partition={Directory.FullName}, Prefix={Database.GlobalSpace}")]
	public sealed class FdbDatabasePartition : IFdbDirectory
	{

		/// <summary>Wrap an existing database with a root directory</summary>
		public FdbDatabasePartition(IFdbDatabase database, IFdbDirectory directory)
		{
			this.Database = database ?? throw new ArgumentNullException(nameof(database));
			this.Directory = directory ?? throw new ArgumentNullException(nameof(directory));
		}

		/// <summary>Wrapped Directory instance</summary>
		[NotNull]
		public IFdbDirectory Directory { get; }

		/// <summary>Wrapped Database instance</summary>
		[NotNull]
		public IFdbDatabase Database { get; }

		/// <summary>Name of this <code>Partition</code>.</summary>
		/// <remarks>This returns the last part of the path</remarks>
		public string Name => this.Directory.Name;

		/// <summary>Formatted path of this <code>Partition</code></summary>
		/// <remarks>This returns the formatted path, using '/' as the separator</remarks>
		[NotNull]
		public string FullName => this.Directory.FullName;

		/// <summary>Gets the path represented by this <code>Partition</code>.</summary>
		/// <remarks>Returns an empty list for the root partition of the database, or a non empty list for a sub-partition</remarks>
		[NotNull]
		public IReadOnlyList<string> Path => this.Directory.Path;

		/// <summary>Get the DirectoryLayer that was used to create this partition.</summary>
		[NotNull]
		public FdbDirectoryLayer DirectoryLayer => this.Directory.DirectoryLayer;

		#region Layer...

		/// <summary>Returns "partition" (ASCII)</summary>
		/// <remarks>This should be equal to <see cref="FdbDirectoryPartition.LayerId"/></remarks>
		public Slice Layer => this.Directory.Layer;

		void IFdbDirectory.CheckLayer(Slice layer)
		{
			if (layer.IsPresent && layer != this.Layer)
			{
				throw new InvalidOperationException($"The directory {this.FullName} is a partition which is not compatible with layer {layer:P}.");
			}
		}

		Task<FdbDirectorySubspace> IFdbDirectory.ChangeLayerAsync(IFdbTransaction trans, Slice newLayer)
		{
			throw new NotSupportedException("You cannot change the layer of an FdbDirectoryPartition.");
		}

		#endregion

		#region CreateOrOpen...

		/// <summary>Opens a subdirectory with the given path.
		/// If the subdirectory does not exist, it is created (creating intermediate subdirectories if necessary).
		/// If layer is specified, it is checked against the layer of an existing subdirectory or set as the layer of a new subdirectory.
		/// </summary>
		public Task<FdbDirectorySubspace> CreateOrOpenAsync([NotNull] string name, CancellationToken ct)
		{
			return this.Database.ReadWriteAsync((tr) => this.Directory.CreateOrOpenAsync(tr, new [] { name }, Slice.Nil), ct);
		}

		/// <summary>Opens a subdirectory with the given path.
		/// If the subdirectory does not exist, it is created (creating intermediate subdirectories if necessary).
		/// If layer is specified, it is checked against the layer of an existing subdirectory or set as the layer of a new subdirectory.
		/// </summary>
		public Task<FdbDirectorySubspace> CreateOrOpenAsync([NotNull] string name, Slice layer, CancellationToken ct)
		{
			return this.Database.ReadWriteAsync((tr) => this.Directory.CreateOrOpenAsync(tr, new[] { name }, layer), ct);
		}

		/// <summary>Opens a subdirectory with the given path.
		/// If the subdirectory does not exist, it is created (creating intermediate subdirectories if necessary).
		/// If layer is specified, it is checked against the layer of an existing subdirectory or set as the layer of a new subdirectory.
		/// </summary>
		public Task<FdbDirectorySubspace> CreateOrOpenAsync([NotNull] IEnumerable<string> path, CancellationToken ct)
		{
			return this.Database.ReadWriteAsync((tr) => this.Directory.CreateOrOpenAsync(tr, path, Slice.Nil), ct);
		}

		/// <summary>Opens a subdirectory with the given path.
		/// If the subdirectory does not exist, it is created (creating intermediate subdirectories if necessary).
		/// If layer is specified, it is checked against the layer of an existing subdirectory or set as the layer of a new subdirectory.
		/// </summary>
		public Task<FdbDirectorySubspace> CreateOrOpenAsync([NotNull] IEnumerable<string> path, Slice layer, CancellationToken ct)
		{
			return this.Database.ReadWriteAsync((tr) => this.Directory.CreateOrOpenAsync(tr, path, layer), ct);
		}

		Task<FdbDirectorySubspace> IFdbDirectory.CreateOrOpenAsync(IFdbTransaction trans, IEnumerable<string> subPath, Slice layer)
		{
			return this.Directory.CreateOrOpenAsync(trans, subPath, layer);
		}

		#endregion

		#region Open...

		/// <summary>Opens a subdirectory with the given <paramref name="name"/>.
		/// An exception is thrown if the subdirectory does not exist, or if a layer is specified and a different layer was specified when the subdirectory was created.
		/// </summary>
		/// <param name="name">Name of the subdirectory to open</param>
		public Task<FdbDirectorySubspace> OpenAsync([NotNull] string name, CancellationToken ct)
		{
			return this.Database.ReadAsync((tr) => this.Directory.OpenAsync(tr, new [] { name }, Slice.Nil), ct);
		}

		/// <summary>Opens a subdirectory with the given <paramref name="name"/>.
		/// An exception is thrown if the subdirectory does not exist, or if a layer is specified and a different layer was specified when the subdirectory was created.
		/// </summary>
		/// <param name="name">Name of the subdirectory to open</param>
		/// <param name="layer">Expected layer id for the subdirectory (optional)</param>
		public Task<FdbDirectorySubspace> OpenAsync([NotNull] string name, Slice layer, CancellationToken ct)
		{
			return this.Database.ReadAsync((tr) => this.Directory.OpenAsync(tr, new[] { name }, layer), ct);
		}

		/// <summary>Opens a subdirectory with the given <paramref name="path"/>.
		/// An exception is thrown if the subdirectory does not exist, or if a layer is specified and a different layer was specified when the subdirectory was created.
		/// </summary>
		/// <param name="path">Relative path of the subdirectory to open</param>
		public Task<FdbDirectorySubspace> OpenAsync([NotNull] IEnumerable<string> path, CancellationToken ct)
		{
			return this.Database.ReadAsync((tr) => this.Directory.OpenAsync(tr, path, Slice.Nil), ct);
		}

		/// <summary>Opens a subdirectory with the given <paramref name="path"/>.
		/// An exception is thrown if the subdirectory does not exist, or if a layer is specified and a different layer was specified when the subdirectory was created.
		/// </summary>
		/// <param name="path">Relative path of the subdirectory to open</param>
		/// <param name="layer">Expected layer id for the subdirectory (optional)</param>
		public Task<FdbDirectorySubspace> OpenAsync([NotNull] IEnumerable<string> path, Slice layer, CancellationToken ct)
		{
			return this.Database.ReadAsync((tr) => this.Directory.OpenAsync(tr, path, layer), ct);
		}

		Task<FdbDirectorySubspace> IFdbDirectory.OpenAsync(IFdbReadOnlyTransaction trans, IEnumerable<string> path, Slice layer)
		{
			return this.Directory.OpenAsync(trans, path, layer);
		}

		#endregion

		#region TryOpen...

		/// <summary>Opens a subdirectory with the given <paramref name="name"/>.
		/// An exception is thrown if the subdirectory if a layer is specified and a different layer was specified when the subdirectory was created.
		/// </summary>
		/// <param name="name">Name of the subdirectory to open</param>
		/// <returns>Returns the directory if it exists, or null if it was not found</returns>
		public Task<FdbDirectorySubspace> TryOpenAsync([NotNull] string name, CancellationToken ct)
		{
			return this.Database.ReadAsync((tr) => this.Directory.TryOpenAsync(tr, new [] { name }, Slice.Nil), ct);
		}

		/// <summary>Opens a subdirectory with the given <paramref name="name"/>.
		/// An exception is thrown if the subdirectory if a layer is specified and a different layer was specified when the subdirectory was created.
		/// </summary>
		/// <param name="name">Name of the subdirectory to open</param>
		/// <param name="layer">Expected layer id for the subdirectory (optional)</param>
		/// <returns>Returns the directory if it exists, or null if it was not found</returns>
		public Task<FdbDirectorySubspace> TryOpenAsync([NotNull] string name, Slice layer, CancellationToken ct)
		{
			return this.Database.ReadAsync((tr) => this.Directory.TryOpenAsync(tr, new[] { name }, layer), ct);
		}

		/// <summary>Opens a subdirectory with the given <paramref name="path"/>.
		/// An exception is thrown if the subdirectory if a layer is specified and a different layer was specified when the subdirectory was created.
		/// </summary>
		/// <param name="path">Relative path of the subdirectory to open</param>
		/// <returns>Returns the directory if it exists, or null if it was not found</returns>
		public Task<FdbDirectorySubspace> TryOpenAsync([NotNull] IEnumerable<string> path, CancellationToken ct)
		{
			return this.Database.ReadAsync((tr) => this.Directory.TryOpenAsync(tr, path, Slice.Nil), ct);
		}

		/// <summary>Opens a subdirectory with the given <paramref name="path"/>.
		/// An exception is thrown if the subdirectory if a layer is specified and a different layer was specified when the subdirectory was created.
		/// </summary>
		/// <param name="path">Relative path of the subdirectory to open</param>
		/// <param name="layer">Expected layer id for the subdirectory (optional)</param>
		/// <returns>Returns the directory if it exists, or null if it was not found</returns>
		public Task<FdbDirectorySubspace> TryOpenAsync([NotNull] IEnumerable<string> path, Slice layer, CancellationToken ct)
		{
			return this.Database.ReadAsync((tr) => this.Directory.TryOpenAsync(tr, path, layer), ct);
		}

		Task<FdbDirectorySubspace> IFdbDirectory.TryOpenAsync(IFdbReadOnlyTransaction trans, IEnumerable<string> path, Slice layer)
		{
			return this.Directory.TryOpenAsync(trans, path, layer);
		}

		#endregion

		#region Create...

		public Task<FdbDirectorySubspace> CreateAsync([NotNull] string name, CancellationToken ct)
		{
			return this.Database.ReadWriteAsync((tr) => this.Directory.CreateAsync(tr, new[] { name }, Slice.Nil), ct);
		}

		public Task<FdbDirectorySubspace> CreateAsync([NotNull] string name, Slice layer, CancellationToken ct)
		{
			return this.Database.ReadWriteAsync((tr) => this.Directory.CreateAsync(tr, new [] { name }, layer), ct);
		}

		public Task<FdbDirectorySubspace> CreateAsync([NotNull] IEnumerable<string> path, CancellationToken ct)
		{
			return this.Database.ReadWriteAsync((tr) => this.Directory.CreateAsync(tr, path, Slice.Nil), ct);
		}

		public Task<FdbDirectorySubspace> CreateAsync([NotNull] IEnumerable<string> path, Slice layer, CancellationToken ct)
		{
			return this.Database.ReadWriteAsync((tr) => this.Directory.CreateAsync(tr, path, layer), ct);
		}

		Task<FdbDirectorySubspace> IFdbDirectory.CreateAsync(IFdbTransaction trans, IEnumerable<string> path, Slice layer)
		{
			return this.Directory.CreateAsync(trans, path, layer);
		}

		#endregion

		#region TryCreate...

		public Task<FdbDirectorySubspace> TryCreateAsync([NotNull] string name, CancellationToken ct)
		{
			return this.Database.ReadWriteAsync((tr) => this.Directory.TryCreateAsync(tr, new [] { name }, Slice.Nil), ct);
		}

		public Task<FdbDirectorySubspace> TryCreateAsync([NotNull] string name, Slice layer, CancellationToken ct)
		{
			return this.Database.ReadWriteAsync((tr) => this.Directory.TryCreateAsync(tr, new[] { name }, layer), ct);
		}

		public Task<FdbDirectorySubspace> TryCreateAsync([NotNull] IEnumerable<string> path, CancellationToken ct)
		{
			return this.Database.ReadWriteAsync((tr) => this.Directory.TryCreateAsync(tr, path, Slice.Nil), ct);
		}

		public Task<FdbDirectorySubspace> TryCreateAsync([NotNull] IEnumerable<string> path, Slice layer, CancellationToken ct)
		{
			return this.Database.ReadWriteAsync((tr) => this.Directory.TryCreateAsync(tr, path, layer), ct);
		}

		Task<FdbDirectorySubspace> IFdbDirectory.TryCreateAsync(IFdbTransaction trans, IEnumerable<string> path, Slice layer)
		{
			return this.Directory.TryCreateAsync(trans, path, layer);
		}

		#endregion

		#region Register...

		/// <summary>Registers an existing prefix as a directory with the given <paramref name="name"/> (creating parent directories if necessary). This method is only indented for advanced use cases.</summary>
		/// <param name="name">Name of the directory to create</param>
		/// <param name="layer">If <paramref name="layer"/> is specified, it is recorded with the directory and will be checked by future calls to open.</param>
		/// <param name="prefix">The directory will be created with the given physical prefix; otherwise a prefix is allocated automatically.</param>
		public Task<FdbDirectorySubspace> RegisterAsync([NotNull] string name, Slice layer, Slice prefix, CancellationToken ct)
		{
			return this.Database.ReadWriteAsync((tr) => this.Directory.RegisterAsync(tr, new[] { name }, layer, prefix), ct);
		}

		/// <summary>Registers an existing prefix as a directory with the given <paramref name="path"/> (creating parent directories if necessary). This method is only indented for advanced use cases.</summary>
		/// <param name="path">Path of the directory to create</param>
		/// <param name="layer">If <paramref name="layer"/> is specified, it is recorded with the directory and will be checked by future calls to open.</param>
		/// <param name="prefix">The directory will be created with the given physical prefix; otherwise a prefix is allocated automatically.</param>
		public Task<FdbDirectorySubspace> RegisterAsync([NotNull] IEnumerable<string> path, Slice layer, Slice prefix, CancellationToken ct)
		{
			return this.Database.ReadWriteAsync((tr) => this.Directory.RegisterAsync(tr, path, layer, prefix), ct);
		}

		Task<FdbDirectorySubspace> IFdbDirectory.RegisterAsync(IFdbTransaction trans, IEnumerable<string> path, Slice layer, Slice prefix)
		{
			return this.Directory.RegisterAsync(trans, path, layer, prefix);
		}

		#endregion

		#region Move...

		public Task<FdbDirectorySubspace> MoveAsync([NotNull] IEnumerable<string> oldPath, [NotNull] IEnumerable<string> newPath, CancellationToken ct)
		{
			return this.Database.ReadWriteAsync((tr) => this.Directory.MoveAsync(tr, oldPath, newPath), ct);
		}

		Task<FdbDirectorySubspace> IFdbDirectory.MoveAsync(IFdbTransaction trans, IEnumerable<string> oldPath, IEnumerable<string> newPath)
		{
			return this.Directory.MoveAsync(trans, oldPath, newPath);
		}

		#endregion

		#region TryMove...

		public Task<FdbDirectorySubspace> TryMoveAsync([NotNull] IEnumerable<string> oldPath, [NotNull] IEnumerable<string> newPath, CancellationToken ct)
		{
			return this.Database.ReadWriteAsync((tr) => this.Directory.TryMoveAsync(tr, oldPath, newPath), ct);
		}

		Task<FdbDirectorySubspace> IFdbDirectory.TryMoveAsync(IFdbTransaction trans, IEnumerable<string> oldPath, IEnumerable<string> newPath)
		{
			return this.Directory.TryMoveAsync(trans, oldPath, newPath);
		}

		#endregion

		#region MoveTo...

		public Task<FdbDirectorySubspace> MoveToAsync(IFdbTransaction trans, IEnumerable<string> newAbsolutePath)
		{
			throw new NotSupportedException("Database partitions cannot be moved");
		}

		#endregion

		#region TryMoveTo...

		public Task<FdbDirectorySubspace> TryMoveToAsync(IFdbTransaction trans, IEnumerable<string> newAbsolutePath)
		{
			throw new NotSupportedException("Database partitions cannot be moved");
		}

		#endregion

		#region Remove...

		public Task RemoveAsync([NotNull] string name, CancellationToken ct)
		{
			return this.Database.ReadWriteAsync((tr) => this.Directory.RemoveAsync(tr, new string[] { name }), ct);
		}

		public Task RemoveAsync(IEnumerable<string> path, CancellationToken ct)
		{
			return this.Database.ReadWriteAsync((tr) => this.Directory.RemoveAsync(tr, path), ct);
		}

		Task IFdbDirectory.RemoveAsync(IFdbTransaction trans, IEnumerable<string> path)
		{
			return this.Directory.RemoveAsync(trans, path);
		}

		#endregion

		#region TryRemove...

		public Task<bool> TryRemoveAsync([NotNull] string name, CancellationToken ct)
		{
			return this.Database.ReadWriteAsync((tr) => this.Directory.TryRemoveAsync(tr, new [] { name }), ct);
		}

		public Task<bool> TryRemoveAsync(IEnumerable<string> path, CancellationToken ct)
		{
			return this.Database.ReadWriteAsync((tr) => this.Directory.TryRemoveAsync(tr, path), ct);
		}

		Task<bool> IFdbDirectory.TryRemoveAsync(IFdbTransaction trans, IEnumerable<string> path)
		{
			return this.Directory.TryRemoveAsync(trans, path);
		}

		#endregion

		#region Exists...

		public Task<bool> ExistsAsync([NotNull] string name, CancellationToken ct)
		{
			return this.Database.ReadWriteAsync((tr) => this.Directory.ExistsAsync(tr, new [] { name }), ct);
		}

		public Task<bool> ExistsAsync(IEnumerable<string> path, CancellationToken ct)
		{
			return this.Database.ReadWriteAsync((tr) => this.Directory.ExistsAsync(tr, path), ct);
		}

		Task<bool> IFdbDirectory.ExistsAsync(IFdbReadOnlyTransaction trans, IEnumerable<string> path)
		{
			return this.Directory.ExistsAsync(trans, path);
		}

		#endregion

		#region List...

		/// <summary>Returns the list of all the top level directories of this database instance.</summary>
		public Task<List<string>> ListAsync(CancellationToken ct)
		{
			return this.Database.ReadWriteAsync((tr) => this.Directory.ListAsync(tr), ct);
		}

		/// <summary>Returns the list of all the top level directories of this database instance.</summary>
		public Task<List<string>> ListAsync([NotNull] string name, CancellationToken ct)
		{
			return this.Database.ReadWriteAsync((tr) => this.Directory.ListAsync(tr, new string[] { name }), ct);
		}

		/// <summary>Returns the list of all the top level directories of this database instance.</summary>
		public Task<List<string>> ListAsync(IEnumerable<string> path, CancellationToken ct)
		{
			return this.Database.ReadWriteAsync((tr) => this.Directory.ListAsync(tr, path), ct);
		}

		Task<List<string>> IFdbDirectory.ListAsync(IFdbReadOnlyTransaction trans, IEnumerable<string> path)
		{
			return this.Directory.ListAsync(trans, path);
		}

		#endregion

		#region TryList...

		/// <summary>Returns the list of all the top level directories of this database instance.</summary>
		public Task<List<string>> TryListAsync(CancellationToken ct)
		{
			//REVIEW: is it possible for this method to fail on a top-level db partition?
			// => it not, should be removed because it is a duplicate of ListAsync(..)
			return this.Database.ReadWriteAsync((tr) => this.Directory.TryListAsync(tr), ct);
		}

		/// <summary>Returns the list of all the top level directories of this database instance.</summary>
		public Task<List<string>> TryListAsync([NotNull] string name, CancellationToken ct)
		{
			//REVIEW: is it possible for this method to fail on a top-level db partition?
			// => it not, should be removed because it is a duplicate of ListAsync(..)
			return this.Database.ReadWriteAsync((tr) => this.Directory.TryListAsync(tr, new string[] { name }), ct);
		}

		/// <summary>Returns the list of all the top level directories of this database instance.</summary>
		public Task<List<string>> TryListAsync(IEnumerable<string> path, CancellationToken ct)
		{
			//REVIEW: is it possible for this method to fail on a top-level db partition?
			// => it not, should be removed because it is a duplicate of ListAsync(..)
			return this.Database.ReadWriteAsync((tr) => this.Directory.TryListAsync(tr, path), ct);
		}

		Task<List<string>> IFdbDirectory.TryListAsync(IFdbReadOnlyTransaction trans, IEnumerable<string> path)
		{
			return this.Directory.TryListAsync(trans, path);
		}

		#endregion

	}

}
