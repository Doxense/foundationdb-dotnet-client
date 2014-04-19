﻿#region BSD Licence
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

namespace FoundationDB.Client
{
	using FoundationDB.Client.Utils;
	using FoundationDB.Filters;
	using FoundationDB.Layers.Directories;
	using FoundationDB.Layers.Tuples;
	using System;
	using System.Collections.Generic;
	using System.Diagnostics;
	using System.Threading;
	using System.Threading.Tasks;

	/// <summary>Database instance that manages the content of a KeySpace partition</summary>
	[DebuggerDisplay("Database={Database.Name}, Contents={Directory.ContentsSubspace}, Nodes={Directory.NodeSubspace}")]
	public sealed class FdbDatabasePartition : IFdbDirectory
	{
		private readonly IFdbDatabase m_database;
		private readonly IFdbDirectory m_directory;

		public FdbDatabasePartition(IFdbDatabase database, IFdbDirectory directory)
		{
			if (database == null) throw new ArgumentNullException("database");
			if (directory == null) throw new ArgumentNullException("directory");

			m_database = database;
			m_directory = directory;
		}

		/// <summary>Wrapped Directory instance</summary>
		public IFdbDirectory Directory { get { return m_directory; } }

		/// <summary>Wrapped Directory instance</summary>
		public IFdbDatabase Database { get { return m_database; } }

		public string Name
		{
			get { return m_directory.Name; }
		}

		public IReadOnlyList<string> Path
		{
			get { return m_directory.Path; }
		}

		public Slice Layer
		{
			get { return m_directory.Layer; }
		}

		public FdbDirectoryLayer DirectoryLayer
		{
			get { return m_directory.DirectoryLayer; }
		}

		#region CreateOrOpen...

		public Task<FdbDirectorySubspace> CreateOrOpenAsync(string name, CancellationToken cancellationToken)
		{
			return m_database.ReadWriteAsync((tr) => m_directory.CreateOrOpenAsync(tr, new [] { name }, Slice.Nil), cancellationToken);
		}

		public Task<FdbDirectorySubspace> CreateOrOpenAsync(string name, Slice layer, CancellationToken cancellationToken)
		{
			return m_database.ReadWriteAsync((tr) => m_directory.CreateOrOpenAsync(tr, new[] { name }, layer), cancellationToken);
		}

		public Task<FdbDirectorySubspace> CreateOrOpenAsync(IEnumerable<string> path, CancellationToken cancellationToken)
		{
			return m_database.ReadWriteAsync((tr) => m_directory.CreateOrOpenAsync(tr, path, Slice.Nil), cancellationToken);
		}

		public Task<FdbDirectorySubspace> CreateOrOpenAsync(IEnumerable<string> path, Slice layer, CancellationToken cancellationToken)
		{
			return m_database.ReadWriteAsync((tr) => m_directory.CreateOrOpenAsync(tr, path, layer), cancellationToken);
		}

		Task<FdbDirectorySubspace> IFdbDirectory.CreateOrOpenAsync(IFdbTransaction trans, IEnumerable<string> subPath, Slice layer)
		{
			return m_directory.CreateOrOpenAsync(trans, subPath, layer);
		}

		#endregion

		#region Open...

		public Task<FdbDirectorySubspace> OpenAsync(string name, CancellationToken cancellationToken)
		{
			return m_database.ReadWriteAsync((tr) => m_directory.OpenAsync(tr, new [] { name }, Slice.Nil), cancellationToken);
		}

		public Task<FdbDirectorySubspace> OpenAsync(string name, Slice layer, CancellationToken cancellationToken)
		{
			return m_database.ReadWriteAsync((tr) => m_directory.OpenAsync(tr, new[] { name }, layer), cancellationToken);
		}

		public Task<FdbDirectorySubspace> OpenAsync(IEnumerable<string> path, CancellationToken cancellationToken)
		{
			return m_database.ReadWriteAsync((tr) => m_directory.OpenAsync(tr, path, Slice.Nil), cancellationToken);
		}

		public Task<FdbDirectorySubspace> OpenAsync(IEnumerable<string> path, Slice layer, CancellationToken cancellationToken)
		{
			return m_database.ReadWriteAsync((tr) => m_directory.OpenAsync(tr, path, layer), cancellationToken);
		}

		Task<FdbDirectorySubspace> IFdbDirectory.OpenAsync(IFdbTransaction trans, IEnumerable<string> path, Slice layer)
		{
			return m_directory.OpenAsync(trans, path, layer);
		}

		#endregion

		#region TryOpen...

		public Task<FdbDirectorySubspace> TryOpenAsync(string name, CancellationToken cancellationToken)
		{
			return m_database.ReadWriteAsync((tr) => m_directory.TryOpenAsync(tr, new [] { name }, Slice.Nil), cancellationToken);
		}

		public Task<FdbDirectorySubspace> TryOpenAsync(string name, Slice layer, CancellationToken cancellationToken)
		{
			return m_database.ReadWriteAsync((tr) => m_directory.TryOpenAsync(tr, new[] { name }, layer), cancellationToken);
		}

		public Task<FdbDirectorySubspace> TryOpenAsync(IEnumerable<string> path, CancellationToken cancellationToken)
		{
			return m_database.ReadWriteAsync((tr) => m_directory.TryOpenAsync(tr, path, Slice.Nil), cancellationToken);
		}

		public Task<FdbDirectorySubspace> TryOpenAsync(IEnumerable<string> path, Slice layer, CancellationToken cancellationToken)
		{
			return m_database.ReadWriteAsync((tr) => m_directory.TryOpenAsync(tr, path, layer), cancellationToken);
		}

		Task<FdbDirectorySubspace> IFdbDirectory.TryOpenAsync(IFdbTransaction trans, IEnumerable<string> path, Slice layer)
		{
			return m_directory.TryOpenAsync(trans, path, layer);
		}

		#endregion

		#region Create...

		public Task<FdbDirectorySubspace> CreateAsync(string name, CancellationToken cancellationToken)
		{
			return m_database.ReadWriteAsync((tr) => m_directory.CreateAsync(tr, new[] { name }, Slice.Nil), cancellationToken);
		}

		public Task<FdbDirectorySubspace> CreateAsync(string name, Slice layer, CancellationToken cancellationToken)
		{
			return m_database.ReadWriteAsync((tr) => m_directory.CreateAsync(tr, new [] { name }, layer), cancellationToken);
		}

		public Task<FdbDirectorySubspace> CreateAsync(IEnumerable<string> path, CancellationToken cancellationToken)
		{
			return m_database.ReadWriteAsync((tr) => m_directory.CreateAsync(tr, path, Slice.Nil), cancellationToken);
		}

		public Task<FdbDirectorySubspace> CreateAsync(IEnumerable<string> path, Slice layer, CancellationToken cancellationToken)
		{
			return m_database.ReadWriteAsync((tr) => m_directory.CreateAsync(tr, path, layer), cancellationToken);
		}

		Task<FdbDirectorySubspace> IFdbDirectory.CreateAsync(IFdbTransaction trans, IEnumerable<string> path, Slice layer)
		{
			return m_directory.CreateAsync(trans, path, layer);
		}

		#endregion

		#region TryCreate...

		public Task<FdbDirectorySubspace> TryCreateAsync(string name, CancellationToken cancellationToken)
		{
			return m_database.ReadWriteAsync((tr) => m_directory.TryCreateAsync(tr, new [] { name }, Slice.Nil), cancellationToken);
		}

		public Task<FdbDirectorySubspace> TryCreateAsync(string name, Slice layer, CancellationToken cancellationToken)
		{
			return m_database.ReadWriteAsync((tr) => m_directory.TryCreateAsync(tr, new[] { name }, layer), cancellationToken);
		}

		public Task<FdbDirectorySubspace> TryCreateAsync(IEnumerable<string> path, CancellationToken cancellationToken)
		{
			return m_database.ReadWriteAsync((tr) => m_directory.TryCreateAsync(tr, path, Slice.Nil), cancellationToken);
		}

		public Task<FdbDirectorySubspace> TryCreateAsync(IEnumerable<string> path, Slice layer, CancellationToken cancellationToken)
		{
			return m_database.ReadWriteAsync((tr) => m_directory.TryCreateAsync(tr, path, layer), cancellationToken);
		}

		Task<FdbDirectorySubspace> IFdbDirectory.TryCreateAsync(IFdbTransaction trans, IEnumerable<string> path, Slice layer)
		{
			return m_directory.TryCreateAsync(trans, path, layer);
		}

		#endregion

		#region Move...

		public Task<FdbDirectorySubspace> MoveAsync(IEnumerable<string> oldPath, IEnumerable<string> newPath, CancellationToken cancellationToken)
		{
			return m_database.ReadWriteAsync((tr) => m_directory.MoveAsync(tr, oldPath, newPath), cancellationToken);
		}

		Task<FdbDirectorySubspace> IFdbDirectory.MoveAsync(IFdbTransaction trans, IEnumerable<string> oldPath, IEnumerable<string> newPath)
		{
			return m_directory.MoveAsync(trans, oldPath, newPath);
		}

		#endregion

		#region TryMove...

		public Task<FdbDirectorySubspace> TryMoveAsync(IEnumerable<string> oldPath, IEnumerable<string> newPath, CancellationToken cancellationToken)
		{
			return m_database.ReadWriteAsync((tr) => m_directory.TryMoveAsync(tr, oldPath, newPath), cancellationToken);
		}

		Task<FdbDirectorySubspace> IFdbDirectory.TryMoveAsync(IFdbTransaction trans, IEnumerable<string> oldPath, IEnumerable<string> newPath)
		{
			return m_directory.TryMoveAsync(trans, oldPath, newPath);
		}

		#endregion

		#region Remove...

		public Task RemoveAsync(string name, CancellationToken cancellationToken)
		{
			return m_database.ReadWriteAsync((tr) => m_directory.RemoveAsync(tr, new string[] { name }), cancellationToken);
		}

		public Task RemoveAsync(IEnumerable<string> path, CancellationToken cancellationToken)
		{
			return m_database.ReadWriteAsync((tr) => m_directory.RemoveAsync(tr, path), cancellationToken);
		}

		Task IFdbDirectory.RemoveAsync(IFdbTransaction trans, IEnumerable<string> path)
		{
			return m_directory.RemoveAsync(trans, path);
		}

		#endregion

		#region TryRemove...

		public Task<bool> TryRemoveAsync(string name, CancellationToken cancellationToken)
		{
			return m_database.ReadWriteAsync((tr) => m_directory.TryRemoveAsync(tr, new string[] { name }), cancellationToken);
		}

		public Task<bool> TryRemoveAsync(IEnumerable<string> path, CancellationToken cancellationToken)
		{
			return m_database.ReadWriteAsync((tr) => m_directory.TryRemoveAsync(tr, path), cancellationToken);
		}

		Task<bool> IFdbDirectory.TryRemoveAsync(IFdbTransaction trans, IEnumerable<string> path)
		{
			return m_directory.TryRemoveAsync(trans, path);
		}

		#endregion

		#region Exists...

		public Task<bool> ExistsAsync(string name, CancellationToken cancellationToken)
		{
			return m_database.ReadWriteAsync((tr) => m_directory.ExistsAsync(tr, new string[] { name }), cancellationToken);
		}

		public Task<bool> ExistsAsync(IEnumerable<string> path, CancellationToken cancellationToken)
		{
			return m_database.ReadWriteAsync((tr) => m_directory.ExistsAsync(tr, path), cancellationToken);
		}

		Task<bool> IFdbDirectory.ExistsAsync(IFdbReadOnlyTransaction trans, IEnumerable<string> path)
		{
			return m_directory.ExistsAsync(trans, path);
		}

		#endregion

		#region List...

		/// <summary>Returns the list of all the top level directories of this database instance.</summary>
		public Task<List<string>> ListAsync(CancellationToken cancellationToken)
		{
			return m_database.ReadWriteAsync((tr) => m_directory.ListAsync(tr), cancellationToken);
		}

		/// <summary>Returns the list of all the top level directories of this database instance.</summary>
		public Task<List<string>> ListAsync(IEnumerable<string> path, CancellationToken cancellationToken)
		{
			return m_database.ReadWriteAsync((tr) => m_directory.ListAsync(tr, path), cancellationToken);
		}

		Task<List<string>> IFdbDirectory.ListAsync(IFdbReadOnlyTransaction trans, IEnumerable<string> path)
		{
			return m_directory.ListAsync(trans, path);
		}

		#endregion

		#region TryList...

		/// <summary>Returns the list of all the top level directories of this database instance.</summary>
		public Task<List<string>> TryListAsync(CancellationToken cancellationToken)
		{
			//REVIEW: is it possible for this method to fail on a top-level db partition?
			// => it not, should be removed because it is a duplicate of ListAsync(..)
			return m_database.ReadWriteAsync((tr) => m_directory.TryListAsync(tr), cancellationToken);
		}

		/// <summary>Returns the list of all the top level directories of this database instance.</summary>
		public Task<List<string>> TryListAsync(IEnumerable<string> path, CancellationToken cancellationToken)
		{
			//REVIEW: is it possible for this method to fail on a top-level db partition?
			// => it not, should be removed because it is a duplicate of ListAsync(..)
			return m_database.ReadWriteAsync((tr) => m_directory.TryListAsync(tr, path), cancellationToken);
		}

		Task<List<string>> IFdbDirectory.TryListAsync(IFdbReadOnlyTransaction trans, IEnumerable<string> path)
		{
			return m_directory.TryListAsync(trans, path);
		}

		#endregion
	}

}
