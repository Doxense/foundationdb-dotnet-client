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

		public Task<FdbDirectorySubspace> CreateOrOpenAsync(string name, Slice layer = default(Slice), CancellationToken cancellationToken = default(CancellationToken))
		{
			return m_database.ReadWriteAsync((tr) => m_directory.CreateOrOpenAsync(tr, name, layer, default(Slice)), cancellationToken);
		}

		public Task<FdbDirectorySubspace> CreateOrOpenAsync(IEnumerable<string> path, Slice layer = default(Slice), CancellationToken cancellationToken = default(CancellationToken))
		{
			return m_database.ReadWriteAsync((tr) => m_directory.CreateOrOpenAsync(tr, path, layer, default(Slice)), cancellationToken);
		}

		Task<FdbDirectorySubspace> IFdbDirectory.CreateOrOpenAsync(IFdbTransaction trans, string name, Slice layer, Slice prefix)
		{
			return m_directory.CreateOrOpenAsync(trans, name, layer, prefix);
		}

		Task<FdbDirectorySubspace> IFdbDirectory.CreateOrOpenAsync(IFdbTransaction trans, IEnumerable<string> subPath, Slice layer, Slice prefix)
		{
			return m_directory.CreateOrOpenAsync(trans, subPath, layer, prefix);
		}

		#endregion

		#region Open...

		public Task<FdbDirectorySubspace> OpenAsync(string name, Slice layer = default(Slice), CancellationToken cancellationToken = default(CancellationToken))
		{
			return m_database.ReadWriteAsync((tr) => m_directory.OpenAsync(tr, name, layer), cancellationToken);
		}

		public Task<FdbDirectorySubspace> OpenAsync(IEnumerable<string> path, Slice layer = default(Slice), CancellationToken cancellationToken = default(CancellationToken))
		{
			return m_database.ReadWriteAsync((tr) => m_directory.OpenAsync(tr, path, layer), cancellationToken);
		}

		Task<FdbDirectorySubspace> IFdbDirectory.OpenAsync(IFdbTransaction trans, string name, Slice layer)
		{
			return m_directory.OpenAsync(trans, name, layer);
		}

		Task<FdbDirectorySubspace> IFdbDirectory.OpenAsync(IFdbTransaction trans, IEnumerable<string> path, Slice layer)
		{
			return m_directory.OpenAsync(trans, path, layer);
		}

		#endregion

		#region TryOpen...

		public Task<FdbDirectorySubspace> TryOpenAsync(string name, Slice layer = default(Slice), CancellationToken cancellationToken = default(CancellationToken))
		{
			return m_database.ReadWriteAsync((tr) => m_directory.TryOpenAsync(tr, name, layer), cancellationToken);
		}

		public Task<FdbDirectorySubspace> TryOpenAsync(IEnumerable<string> path, Slice layer = default(Slice), CancellationToken cancellationToken = default(CancellationToken))
		{
			return m_database.ReadWriteAsync((tr) => m_directory.TryOpenAsync(tr, path, layer), cancellationToken);
		}

		Task<FdbDirectorySubspace> IFdbDirectory.TryOpenAsync(IFdbTransaction trans, string name, Slice layer)
		{
			return m_directory.TryOpenAsync(trans, name, layer);
		}

		Task<FdbDirectorySubspace> IFdbDirectory.TryOpenAsync(IFdbTransaction trans, IEnumerable<string> path, Slice layer)
		{
			return m_directory.TryOpenAsync(trans, path, layer);
		}

		#endregion

		#region Create...

		public Task<FdbDirectorySubspace> CreateAsync(string name, Slice layer = default(Slice), CancellationToken cancellationToken = default(CancellationToken))
		{
			return m_database.ReadWriteAsync((tr) => m_directory.CreateAsync(tr, name, layer, default(Slice)), cancellationToken);
		}

		public Task<FdbDirectorySubspace> CreateAsync(IEnumerable<string> path, Slice layer = default(Slice), CancellationToken cancellationToken = default(CancellationToken))
		{
			return m_database.ReadWriteAsync((tr) => m_directory.CreateAsync(tr, path, layer, default(Slice)), cancellationToken);
		}

		Task<FdbDirectorySubspace> IFdbDirectory.CreateAsync(IFdbTransaction trans, string name, Slice layer, Slice prefix)
		{
			return m_directory.CreateAsync(trans, name, layer, prefix);
		}

		Task<FdbDirectorySubspace> IFdbDirectory.CreateAsync(IFdbTransaction trans, IEnumerable<string> path, Slice layer, Slice prefix)
		{
			return m_directory.CreateAsync(trans, path, layer, prefix);
		}

		#endregion

		#region TryCreate...

		public Task<FdbDirectorySubspace> TryCreateAsync(string name, Slice layer = default(Slice), CancellationToken cancellationToken = default(CancellationToken))
		{
			return m_database.ReadWriteAsync((tr) => m_directory.TryCreateAsync(tr, name, layer, default(Slice)), cancellationToken);
		}

		public Task<FdbDirectorySubspace> TryCreateAsync(IEnumerable<string> path, Slice layer = default(Slice), CancellationToken cancellationToken = default(CancellationToken))
		{
			return m_database.ReadWriteAsync((tr) => m_directory.TryCreateAsync(tr, path, layer, default(Slice)), cancellationToken);
		}

		Task<FdbDirectorySubspace> IFdbDirectory.TryCreateAsync(IFdbTransaction trans, string name, Slice layer, Slice prefix)
		{
			return m_directory.TryCreateAsync(trans, name, layer, prefix);
		}

		Task<FdbDirectorySubspace> IFdbDirectory.TryCreateAsync(IFdbTransaction trans, IEnumerable<string> path, Slice layer, Slice prefix)
		{
			return m_directory.TryCreateAsync(trans, path, layer, prefix);
		}

		#endregion

		#region Move...

		public Task<FdbDirectorySubspace> MoveAsync(IEnumerable<string> oldPath, IEnumerable<string> newPath, CancellationToken cancellationToken = default(CancellationToken))
		{
			return m_database.ReadWriteAsync((tr) => m_directory.MoveAsync(tr, oldPath, newPath), cancellationToken);
		}

		Task<FdbDirectorySubspace> IFdbDirectory.MoveAsync(IFdbTransaction trans, IEnumerable<string> newPath)
		{
			throw new NotImplementedException();
		}

		Task<FdbDirectorySubspace> IFdbDirectory.MoveAsync(IFdbTransaction trans, IEnumerable<string> oldPath, IEnumerable<string> newPath)
		{
			throw new NotImplementedException();
		}

		#endregion

		#region TryMove...

		public Task<FdbDirectorySubspace> TryMoveAsync(IEnumerable<string> oldPath, IEnumerable<string> newPath, CancellationToken cancellationToken = default(CancellationToken))
		{
			return m_database.ReadWriteAsync((tr) => m_directory.TryMoveAsync(tr, oldPath, newPath), cancellationToken);
		}

		Task<FdbDirectorySubspace> IFdbDirectory.TryMoveAsync(IFdbTransaction trans, IEnumerable<string> newPath)
		{
			throw new NotImplementedException();
		}

		Task<FdbDirectorySubspace> IFdbDirectory.TryMoveAsync(IFdbTransaction trans, IEnumerable<string> oldPath, IEnumerable<string> newPath)
		{
			throw new NotImplementedException();
		}

		#endregion

		#region Remove...

		public Task RemoveAsync(string name, CancellationToken cancellationToken = default(CancellationToken))
		{
			return m_database.ReadWriteAsync((tr) => m_directory.RemoveAsync(tr, new string[] { name }), cancellationToken);
		}

		public Task RemoveAsync(IEnumerable<string> path, CancellationToken cancellationToken = default(CancellationToken))
		{
			return m_database.ReadWriteAsync((tr) => m_directory.RemoveAsync(tr, path), cancellationToken);
		}

		Task IFdbDirectory.RemoveAsync(IFdbTransaction trans)
		{
			throw new NotSupportedException();
		}

		Task IFdbDirectory.RemoveAsync(IFdbTransaction trans, IEnumerable<string> path)
		{
			return m_directory.RemoveAsync(trans, path);
		}

		#endregion

		#region TryRemove...

		public Task<bool> TryRemoveAsync(string name, CancellationToken cancellationToken = default(CancellationToken))
		{
			return m_database.ReadWriteAsync((tr) => m_directory.TryRemoveAsync(tr, new string[] { name }), cancellationToken);
		}

		public Task<bool> TryRemoveAsync(IEnumerable<string> path, CancellationToken cancellationToken = default(CancellationToken))
		{
			return m_database.ReadWriteAsync((tr) => m_directory.TryRemoveAsync(tr, path), cancellationToken);
		}

		Task<bool> IFdbDirectory.TryRemoveAsync(IFdbTransaction trans)
		{
			return m_directory.TryRemoveAsync(trans);
		}

		Task<bool> IFdbDirectory.TryRemoveAsync(IFdbTransaction trans, IEnumerable<string> path)
		{
			return m_directory.TryRemoveAsync(trans, path);
		}

		#endregion

		#region Exists...

		public Task<bool> ExistsAsync(string name, CancellationToken cancellationToken = default(CancellationToken))
		{
			return m_database.ReadWriteAsync((tr) => m_directory.ExistsAsync(tr, new string[] { name }), cancellationToken);
		}

		public Task<bool> ExistsAsync(IEnumerable<string> path, CancellationToken cancellationToken = default(CancellationToken))
		{
			return m_database.ReadWriteAsync((tr) => m_directory.ExistsAsync(tr, path), cancellationToken);
		}

		Task<bool> IFdbDirectory.ExistsAsync(IFdbReadOnlyTransaction trans)
		{
			return m_directory.ExistsAsync(trans);
		}

		Task<bool> IFdbDirectory.ExistsAsync(IFdbReadOnlyTransaction trans, IEnumerable<string> path)
		{
			return m_directory.ExistsAsync(trans, path);
		}

		#endregion

		#region List...

		public Task<List<string>> ListAsync(CancellationToken cancellationToken = default(CancellationToken))
		{
			return m_database.ReadWriteAsync((tr) => m_directory.ListAsync(tr), cancellationToken);
		}

		public Task<List<string>> ListAsync(IEnumerable<string> path, CancellationToken cancellationToken = default(CancellationToken))
		{
			return m_database.ReadWriteAsync((tr) => m_directory.ListAsync(tr, path), cancellationToken);
		}

		Task<List<string>> IFdbDirectory.ListAsync(IFdbReadOnlyTransaction trans)
		{
			return m_directory.ListAsync(trans);
		}

		Task<List<string>> IFdbDirectory.ListAsync(IFdbReadOnlyTransaction trans, IEnumerable<string> path)
		{
			return m_directory.ListAsync(trans, path);
		}

		#endregion

		#region TryList...

		public Task<List<string>> TryListAsync(CancellationToken cancellationToken = default(CancellationToken))
		{
			return m_database.ReadWriteAsync((tr) => m_directory.TryListAsync(tr), cancellationToken);
		}

		public Task<List<string>> TryListAsync(IEnumerable<string> path, CancellationToken cancellationToken = default(CancellationToken))
		{
			return m_database.ReadWriteAsync((tr) => m_directory.TryListAsync(tr, path), cancellationToken);
		}

		Task<List<string>> IFdbDirectory.TryListAsync(IFdbReadOnlyTransaction trans)
		{
			return m_directory.TryListAsync(trans);
		}

		Task<List<string>> IFdbDirectory.TryListAsync(IFdbReadOnlyTransaction trans, IEnumerable<string> path)
		{
			return m_directory.TryListAsync(trans, path);
		}

		#endregion
	}

}
