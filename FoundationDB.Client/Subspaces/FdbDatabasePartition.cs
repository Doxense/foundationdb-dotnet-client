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
	using FoundationDB.Client.Native;
	using FoundationDB.Layers.Directories;
	using FoundationDB.Layers.Tuples;
	using System;
	using System.Collections.Generic;
	using System.Diagnostics;
	using System.Threading;
	using System.Threading.Tasks;

	/// <summary>Database instance that manages the content of a KeySpace partition</summary>
	[DebuggerDisplay("Database={Database.Name}, Contents={Directory.ContentsSubspace}, Nodes={Directory.NodeSubspace}")]
	public class FdbDatabasePartition : IFdbReadOnlyDatabase, IFdbDatabase
	{
		/// <summary>Inner database</summary>
		private readonly IFdbDatabase m_database;
		/// <summary>Root directory layer</summary>
		private readonly FdbDirectoryLayer m_root;
		/// <summary>If true, dispose the inner database when we get disposed</summary>
		private readonly bool m_ownsDatabase;
		/// <summary>If true, we have been disposed</summary>
		private bool m_disposed;

		internal FdbDatabasePartition(IFdbDatabase database, FdbSubspace nodes, FdbSubspace contents, bool ownsDatabase)
		{
			if (database == null) throw new ArgumentNullException("database");
			if (nodes == null) nodes = database.GlobalSpace[FdbKey.Directory];
			if (contents == null) contents = database.GlobalSpace;

			if (!database.GlobalSpace.Contains(nodes.Key)) throw new ArgumentException("Nodes subspace must be contained inside the database global namespace", "nodes");
			if (!database.GlobalSpace.Contains(contents.Key)) throw new ArgumentException("Contents subspace must be contained inside the database global namespace", "contents");

			m_database = database;
			m_root = new FdbDirectoryLayer(nodes, contents);
			m_ownsDatabase = ownsDatabase;
		}

		/// <summary>Database instance configured to read and write data from this partition</summary>
		internal IFdbDatabase Database { get { return m_database; } }

		/// <summary>DirectoryLayer instance corresponding to the Root of this partition</summary>
		public FdbDirectoryLayer Root { get { return m_root; } }

		#region DirectoryLayer helpers...

		public Task<FdbDirectorySubspace> CreateOrOpenDirectoryAsync(IFdbTuple path, string layer = null, Slice prefix = default(Slice), bool allowCreate = true, bool allowOpen = true, CancellationToken cancellationToken = default(CancellationToken))
		{
			return this.Root.CreateOrOpenAsync(this.Database, path, layer, prefix, allowCreate, allowOpen, cancellationToken);
		}

		public Task<FdbDirectorySubspace> OpenDirectoryAsync(IFdbTuple path, string layer = null, CancellationToken cancellationToken = default(CancellationToken))
		{
			return this.Root.OpenAsync(this.Database, path, layer, cancellationToken);
		}

		public Task<FdbDirectorySubspace> CreateDirectoryAsync(IFdbTuple path, string layer = null, Slice prefix = default(Slice), CancellationToken cancellationToken = default(CancellationToken))
		{
			return this.Root.CreateAsync(this.Database, path, layer, prefix, cancellationToken);
		}

		public Task<FdbDirectorySubspace> MoveDirectoryAsync(IFdbTuple oldPath, IFdbTuple newPath, CancellationToken cancellationToken = default(CancellationToken))
		{
			return this.Root.MoveAsync(this.Database, oldPath, newPath, cancellationToken);
		}

		public Task<bool> RemoveDirectoryAsync(IFdbTuple path, CancellationToken cancellationToken = default(CancellationToken))
		{
			return this.Root.RemoveAsync(this.Database, path, cancellationToken);
		}

		public Task<List<IFdbTuple>> ListDirectoryAsync(IFdbTuple path = null, CancellationToken cancellationToken = default(CancellationToken))
		{
			return this.Root.ListAsync(this.Database, path, cancellationToken);
		}

		#endregion

		public string Name
		{
			get { return m_database.Name; }
		}

		/// <summary>Returns a cancellation token that is linked with the lifetime of this database instance</summary>
		public CancellationToken Token
		{
			get { return m_database.Token; }
		}

		/// <summary>Returns the global namespace used by this database instance</summary>
		public FdbSubspace GlobalSpace
		{
			get { return m_database.GlobalSpace; }
		}

		public IFdbReadOnlyTransaction BeginReadOnlyTransaction(CancellationToken cancellationToken = default(CancellationToken))
		{
			ThrowIfDisposed();
			return m_database.BeginReadOnlyTransaction(cancellationToken);
		}

		public IFdbTransaction BeginTransaction(CancellationToken cancellationToken = default(CancellationToken))
		{
			ThrowIfDisposed();
			return m_database.BeginTransaction(cancellationToken);
		}

		public Task ReadAsync(Func<IFdbReadOnlyTransaction, Task> asyncHandler, CancellationToken cancellationToken = default(CancellationToken))
		{
			ThrowIfDisposed();
			return m_database.ReadAsync(asyncHandler, cancellationToken);
		}

		public Task ReadAsync(Func<IFdbReadOnlyTransaction, Task> asyncHandler, Action<IFdbReadOnlyTransaction> onDone, CancellationToken cancellationToken = default(CancellationToken))
		{
			ThrowIfDisposed();
			return m_database.ReadAsync(asyncHandler, onDone, cancellationToken);
		}

		public Task<R> ReadAsync<R>(Func<IFdbReadOnlyTransaction, Task<R>> asyncHandler, CancellationToken cancellationToken = default(CancellationToken))
		{
			return m_database.ReadAsync<R>(asyncHandler, cancellationToken);
		}

		public Task<R> ReadAsync<R>(Func<IFdbReadOnlyTransaction, Task<R>> asyncHandler, Action<IFdbReadOnlyTransaction> onDone, CancellationToken cancellationToken = default(CancellationToken))
		{
			ThrowIfDisposed();
			return m_database.ReadAsync<R>(asyncHandler, onDone, cancellationToken);
		}

		public Task WriteAsync(Action<IFdbTransaction> handler, CancellationToken cancellationToken = default(CancellationToken))
		{
			ThrowIfDisposed();
			return m_database.WriteAsync(handler, cancellationToken);
		}

		public Task WriteAsync(Action<IFdbTransaction> handler, Action<IFdbTransaction> onDone, CancellationToken cancellationToken = default(CancellationToken))
		{
			ThrowIfDisposed();
			return m_database.WriteAsync(handler, onDone, cancellationToken);
		}

		public Task ReadWriteAsync(Func<IFdbTransaction, Task> asyncHandler, CancellationToken cancellationToken = default(CancellationToken))
		{
			ThrowIfDisposed();
			return m_database.ReadWriteAsync(asyncHandler, cancellationToken);
		}

		public Task ReadWriteAsync(Func<IFdbTransaction, Task> asyncHandler, Action<IFdbTransaction> onDone, CancellationToken cancellationToken = default(CancellationToken))
		{
			ThrowIfDisposed();
			return m_database.ReadWriteAsync(asyncHandler, onDone, cancellationToken);
		}

		public Task<R> ReadWriteAsync<R>(Func<IFdbTransaction, Task<R>> asyncHandler, CancellationToken cancellationToken = default(CancellationToken))
		{
			ThrowIfDisposed();
			return m_database.ReadWriteAsync<R>(asyncHandler, cancellationToken);
		}

		public Task<R> ReadWriteAsync<R>(Func<IFdbTransaction, Task<R>> asyncHandler, Action<IFdbTransaction> onDone, CancellationToken cancellationToken = default(CancellationToken))
		{
			ThrowIfDisposed();
			return m_database.ReadWriteAsync<R>(asyncHandler, onDone, cancellationToken);
		}

		public void SetOption(FdbDatabaseOption option)
		{
			ThrowIfDisposed();
			m_database.SetOption(option);
		}

		public void SetOption(FdbDatabaseOption option, string value)
		{
			ThrowIfDisposed();
			m_database.SetOption(option, value);
		}

		public void SetOption(FdbDatabaseOption option, long value)
		{
			ThrowIfDisposed();
			m_database.SetOption(option, value);
		}

		public int DefaultTimeout
		{
			get { return m_database.DefaultTimeout; }
			set
			{
				ThrowIfDisposed();
				m_database.DefaultTimeout = value;
			}
		}

		public int DefaultRetryLimit
		{
			get { return m_database.DefaultRetryLimit; }
			set
			{
				ThrowIfDisposed();
				m_database.DefaultRetryLimit = value;
			}
		}

		private void ThrowIfDisposed()
		{
			if (m_disposed) throw new ObjectDisposedException(this.GetType().Name);
		}

		public void Dispose()
		{
			if (!m_disposed)
			{
				m_disposed = true;
				if (m_ownsDatabase)
				{
					m_database.Dispose();
				}
			}
		}

	}

}
