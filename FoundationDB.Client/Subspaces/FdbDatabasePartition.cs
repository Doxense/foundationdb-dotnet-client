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
	using FoundationDB.Client.Filters;
	using FoundationDB.Client.Native;
	using FoundationDB.Client.Utils;
	using FoundationDB.Layers.Directories;
	using FoundationDB.Layers.Tuples;
	using System;
	using System.Collections.Generic;
	using System.Diagnostics;
	using System.Threading;
	using System.Threading.Tasks;

	/// <summary>Database instance that manages the content of a KeySpace partition</summary>
	[DebuggerDisplay("Database={Database.Name}, Contents={Directory.ContentsSubspace}, Nodes={Directory.NodeSubspace}")]
	public sealed class FdbDatabasePartition : FdbDatabaseFilter
	{
		/// <summary>Root directory layer</summary>
		private readonly FdbDirectoryLayer m_root;

		internal FdbDatabasePartition(IFdbDatabase database, FdbSubspace nodes, FdbSubspace contents, bool ownsDatabase)
			: base(database, false, ownsDatabase)
		{
			Contract.Requires(database != null);

			if (nodes == null) nodes = database.GlobalSpace[FdbKey.Directory];
			if (contents == null) contents = database.GlobalSpace;

			if (!database.GlobalSpace.Contains(nodes.Key)) throw new ArgumentException("Nodes subspace must be contained inside the database global namespace", "nodes");
			if (!database.GlobalSpace.Contains(contents.Key)) throw new ArgumentException("Contents subspace must be contained inside the database global namespace", "contents");

			m_root = new FdbDirectoryLayer(nodes, contents);
		}

		/// <summary>DirectoryLayer instance corresponding to the Root of this partition</summary>
		public FdbDirectoryLayer Root { get { return m_root; } }

		#region DirectoryLayer helpers...

		public Task<FdbDirectorySubspace> CreateOrOpenDirectoryAsync(IFdbTuple path, string layer = null, Slice prefix = default(Slice), CancellationToken cancellationToken = default(CancellationToken))
		{
			return this.Root.CreateOrOpenAsync(m_database, path, layer, prefix, cancellationToken);
		}

		public Task<FdbDirectorySubspace> CreateOrOpenDirectoryAsync(string[] path, string layer = null, Slice prefix = default(Slice), CancellationToken cancellationToken = default(CancellationToken))
		{
			return this.Root.CreateOrOpenAsync(m_database, path, layer, prefix, cancellationToken);
		}

		public Task<FdbDirectorySubspace> OpenDirectoryAsync(IFdbTuple path, string layer = null, CancellationToken cancellationToken = default(CancellationToken))
		{
			return this.Root.OpenAsync(m_database, path, layer, cancellationToken);
		}

		public Task<FdbDirectorySubspace> OpenDirectoryAsync(string[] path, string layer = null, CancellationToken cancellationToken = default(CancellationToken))
		{
			return this.Root.OpenAsync(m_database, path, layer, cancellationToken);
		}

		public Task<FdbDirectorySubspace> CreateDirectoryAsync(IFdbTuple path, string layer = null, Slice prefix = default(Slice), CancellationToken cancellationToken = default(CancellationToken))
		{
			return this.Root.CreateAsync(m_database, path, layer, prefix, cancellationToken);
		}

		public Task<FdbDirectorySubspace> CreateDirectoryAsync(string[] path, string layer = null, Slice prefix = default(Slice), CancellationToken cancellationToken = default(CancellationToken))
		{
			return this.Root.CreateAsync(m_database, path, layer, prefix, cancellationToken);
		}

		public Task<FdbDirectorySubspace> MoveDirectoryAsync(IFdbTuple oldPath, IFdbTuple newPath, CancellationToken cancellationToken = default(CancellationToken))
		{
			return this.Root.MoveAsync(m_database, oldPath, newPath, cancellationToken);
		}

		public Task<FdbDirectorySubspace> MoveDirectoryAsync(string[] oldPath, string[] newPath, CancellationToken cancellationToken = default(CancellationToken))
		{
			return this.Root.MoveAsync(m_database, oldPath, newPath, cancellationToken);
		}

		public Task RemoveDirectoryAsync(IFdbTuple path, CancellationToken cancellationToken = default(CancellationToken))
		{
			return this.Root.RemoveAsync(m_database, path, cancellationToken);
		}

		public Task<bool> TryRemoveDirectoryAsync(IFdbTuple path, CancellationToken cancellationToken = default(CancellationToken))
		{
			return this.Root.TryRemoveAsync(m_database, path, cancellationToken);
		}

		public Task RemoveDirectoryAsync(string[] path, CancellationToken cancellationToken = default(CancellationToken))
		{
			return this.Root.RemoveAsync(m_database, path, cancellationToken);
		}

		public Task<bool> TryRemoveDirectoryAsync(string[] path, CancellationToken cancellationToken = default(CancellationToken))
		{
			return this.Root.TryRemoveAsync(m_database, path, cancellationToken);
		}

		public Task<List<IFdbTuple>> ListDirectoryAsync(IFdbTuple path, CancellationToken cancellationToken = default(CancellationToken))
		{
			if (path == null) path = FdbTuple.Empty;
			return this.Root.ListAsync(m_database, path, cancellationToken);
		}

		public Task<List<IFdbTuple>> ListDirectoryAsync(string[] path, CancellationToken cancellationToken = default(CancellationToken))
		{
			return this.Root.ListAsync(m_database, path, cancellationToken);
		}

		#endregion

	}

}
