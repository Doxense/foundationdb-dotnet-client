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

namespace FoundationDB.Filters
{
	using FoundationDB.Client;
	using System;
	using System.Diagnostics;
	using System.Threading;
	using System.Threading.Tasks;

	/// <summary>Base class for simple database filters</summary>
	[DebuggerDisplay("Database={m_database.Name}")]
	public abstract class FdbDatabaseFilter : IFdbDatabase
	{
		#region Private Members...

		/// <summary>Inner database</summary>
		protected readonly IFdbDatabase m_database;

		/// <summary>If true, forces the inner database to be read only</summary>
		protected readonly bool m_readOnly;

		/// <summary>If true, dispose the inner database when we get disposed</summary>
		protected readonly bool m_owner;

		/// <summary>If true, we have been disposed</summary>
		protected bool m_disposed;

		/// <summary>Wrapper for the inner db's Directory property</summary>
		protected FdbDatabasePartition m_directory;

		#endregion

		#region Constructors...

		protected FdbDatabaseFilter(IFdbDatabase database, bool forceReadOnly, bool ownsDatabase)
		{
			if (database == null) throw new ArgumentNullException("database");

			m_database = database;
			m_readOnly = forceReadOnly || database.IsReadOnly;
			m_owner = ownsDatabase;
		}

		#endregion

		#region Public Properties...

		/// <summary>Database instance configured to read and write data from this partition</summary>
		protected IFdbDatabase Database { get { return m_database; } }

		internal IFdbDatabase GetInnerDatabase()
		{
			return m_database;
		}

		/// <summary>Name of the database</summary>
		public string Name
		{
			get { return m_database.Name; }
		}

		/// <summary>Cluster of the database</summary>
		public IFdbCluster Cluster
		{
			//REVIEW: do we need a Cluster Filter ?
			get { return m_database.Cluster; }
		}

		/// <summary>Returns a cancellation token that is linked with the lifetime of this database instance</summary>
		public CancellationToken Cancellation
		{
			get { return m_database.Cancellation; }
		}

		/// <summary>Returns the global namespace used by this database instance</summary>
		public FdbSubspace GlobalSpace
		{
			get { return m_database.GlobalSpace; }
		}

		/// <summary>Directory partition of this database instance</summary>
		public FdbDatabasePartition Directory
		{
			get
			{
				if (m_directory == null || !object.ReferenceEquals(m_directory.Directory, m_database.Directory))
				{
					m_directory = new FdbDatabasePartition(this, m_database.Directory);
				}
				return m_directory;
			}
		}

		/// <summary>If true, this database instance will only allow starting read-only transactions.</summary>
		public bool IsReadOnly
		{
			get { return m_readOnly; }
		}

		#endregion

		#region Transactionals...

		public virtual IFdbTransaction BeginTransaction(FdbTransactionMode mode, CancellationToken cancellationToken = default(CancellationToken), FdbOperationContext context = null)
		{
			ThrowIfDisposed();

			// enfore read-only mode!
			if (m_readOnly) mode |= FdbTransactionMode.ReadOnly;

			if (context == null)
			{
				context = new FdbOperationContext(this, mode, cancellationToken);
			}

			return m_database.BeginTransaction(mode, cancellationToken, context);
		}

		public virtual bool Contains(Slice key)
		{
			return m_database.Contains(key);
		}

		public Task ReadAsync(Func<IFdbReadOnlyTransaction, Task> asyncHandler, CancellationToken cancellationToken)
		{
			ThrowIfDisposed();
			return FdbOperationContext.RunReadAsync(this, asyncHandler, null, cancellationToken);
		}

		public Task ReadAsync(Func<IFdbReadOnlyTransaction, Task> asyncHandler, Action<IFdbReadOnlyTransaction> onDone, CancellationToken cancellationToken)
		{
			ThrowIfDisposed();
			return FdbOperationContext.RunReadAsync(this, asyncHandler, onDone, cancellationToken);
		}

		public Task<R> ReadAsync<R>(Func<IFdbReadOnlyTransaction, Task<R>> asyncHandler, CancellationToken cancellationToken)
		{
			ThrowIfDisposed();
			return FdbOperationContext.RunReadWithResultAsync<R>(this, asyncHandler, null, cancellationToken);
		}

		public Task<R> ReadAsync<R>(Func<IFdbReadOnlyTransaction, Task<R>> asyncHandler, Action<IFdbReadOnlyTransaction> onDone, CancellationToken cancellationToken)
		{
			ThrowIfDisposed();
			return FdbOperationContext.RunReadWithResultAsync<R>(this, asyncHandler, onDone, cancellationToken);
		}

		public Task WriteAsync(Action<IFdbTransaction> handler, CancellationToken cancellationToken)
		{
			ThrowIfDisposed();
			return FdbOperationContext.RunWriteAsync(this, handler, null, cancellationToken);
		}

		public Task WriteAsync(Action<IFdbTransaction> handler, Action<IFdbTransaction> onDone, CancellationToken cancellationToken)
		{
			ThrowIfDisposed();
			return FdbOperationContext.RunWriteAsync(this, handler, onDone, cancellationToken);
		}

		public Task WriteAsync(Func<IFdbTransaction, Task> handler, CancellationToken cancellationToken)
		{
			ThrowIfDisposed();
			return FdbOperationContext.RunWriteAsync(this, handler, null, cancellationToken);
		}

		public Task WriteAsync(Func<IFdbTransaction, Task> handler, Action<IFdbTransaction> onDone, CancellationToken cancellationToken)
		{
			ThrowIfDisposed();
			return FdbOperationContext.RunWriteAsync(this, handler, onDone, cancellationToken);
		}

		public Task ReadWriteAsync(Func<IFdbTransaction, Task> asyncHandler, CancellationToken cancellationToken)
		{
			ThrowIfDisposed();
			return FdbOperationContext.RunWriteAsync(this, asyncHandler, null, cancellationToken);
		}

		public Task ReadWriteAsync(Func<IFdbTransaction, Task> asyncHandler, Action<IFdbTransaction> onDone, CancellationToken cancellationToken)
		{
			ThrowIfDisposed();
			return FdbOperationContext.RunWriteAsync(this, asyncHandler, onDone, cancellationToken);
		}

		public Task<R> ReadWriteAsync<R>(Func<IFdbTransaction, Task<R>> asyncHandler, CancellationToken cancellationToken)
		{
			ThrowIfDisposed();
			return FdbOperationContext.RunWriteWithResultAsync<R>(this, asyncHandler, null, cancellationToken);
		}

		public Task<R> ReadWriteAsync<R>(Func<IFdbTransaction, Task<R>> asyncHandler, Action<IFdbTransaction> onDone, CancellationToken cancellationToken)
		{
			ThrowIfDisposed();
			return FdbOperationContext.RunWriteWithResultAsync<R>(this, asyncHandler, onDone, cancellationToken);
		}

		#endregion

		#region Options...

		public virtual void SetOption(FdbDatabaseOption option)
		{
			ThrowIfDisposed();
			m_database.SetOption(option);
		}

		public virtual void SetOption(FdbDatabaseOption option, string value)
		{
			ThrowIfDisposed();
			m_database.SetOption(option, value);
		}

		public virtual void SetOption(FdbDatabaseOption option, long value)
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

		#endregion

		#region IDisposable Members...

		protected void ThrowIfDisposed()
		{
			if (m_disposed) throw new ObjectDisposedException(this.GetType().Name);
		}

		public void Dispose()
		{
			Dispose(true);
			GC.SuppressFinalize(this);
		}

		protected virtual void Dispose(bool disposing)
		{
			if (!m_disposed)
			{
				m_disposed = true;
				if (disposing && m_owner)
				{
					m_database.Dispose();
				}
			}
		}

		#endregion

		#region IFdbSubspace Members...

		Slice IFdbKey.ToFoundationDbKey()
		{
			return m_database.ToFoundationDbKey();
		}

		#endregion

	}

}
