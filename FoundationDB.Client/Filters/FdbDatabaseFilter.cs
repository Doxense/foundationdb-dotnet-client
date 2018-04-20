#region BSD Licence
/* Copyright (c) 2013-2015, Doxense SAS
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
	using System;
	using System.Collections.Generic;
	using System.Diagnostics;
	using System.Threading;
	using System.Threading.Tasks;
	using Doxense.Diagnostics.Contracts;
	using FoundationDB.Client;
	using JetBrains.Annotations;

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

		protected FdbDatabaseFilter([NotNull] IFdbDatabase database, bool forceReadOnly, bool ownsDatabase)
		{
			Contract.NotNull(database, nameof(database));

			m_database = database;
			m_readOnly = forceReadOnly || database.IsReadOnly;
			m_owner = ownsDatabase;
		}

		#endregion

		#region Public Properties...

		/// <summary>Database instance configured to read and write data from this partition</summary>
		protected IFdbDatabase Database
		{
			[NotNull]
			get { return m_database; }
		}

		[NotNull]
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
		public virtual IFdbCluster Cluster
		{
			//REVIEW: do we need a Cluster Filter ?
			[NotNull]
			get { return m_database.Cluster; }
		}

		/// <summary>Returns a cancellation token that is linked with the lifetime of this database instance</summary>
		public CancellationToken Cancellation
		{
			get { return m_database.Cancellation; }
		}

		/// <summary>Returns the global namespace used by this database instance</summary>
		public virtual IFdbDynamicSubspace GlobalSpace
		{
			[NotNull]
			get { return m_database.GlobalSpace; }
		}

		/// <summary>Directory partition of this database instance</summary>
		public virtual FdbDatabasePartition Directory
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
		public virtual bool IsReadOnly
		{
			get { return m_readOnly; }
		}

		Slice IFdbSubspace.Key
		{
			get { return this.GlobalSpace.Key; }
		}

		KeyRange IFdbSubspace.ToRange()
		{
			return this.GlobalSpace.ToRange();
		}

		KeyRange IFdbSubspace.ToRange(Slice suffix)
		{
			return this.GlobalSpace.ToRange(suffix);
		}

		KeyRange IFdbSubspace.ToRange<TKey>(TKey key)
		{
			return this.GlobalSpace.ToRange(key);
		}

		IFdbSubspace IFdbSubspace.this[Slice suffix]
		{
			get { return this.GlobalSpace[suffix]; }
		}

		IFdbSubspace IFdbSubspace.this[IFdbKey key]
		{
			get { return this.GlobalSpace[key]; }
		}

		public virtual FdbDynamicSubspacePartition Partition
		{
			get { return m_database.Partition; }
		}

		public virtual FdbDynamicSubspaceKeys Keys
		{
			get { return m_database.Keys; }
		}

		public virtual bool Contains(Slice key)
		{
			return m_database.Contains(key);
		}

		public virtual Slice BoundCheck(Slice key, bool allowSystemKeys)
		{
			return m_database.BoundCheck(key, allowSystemKeys);
		}

		public virtual Slice ConcatKey(Slice key)
		{
			return m_database.ConcatKey(key);
		}

		public virtual Slice ConcatKey<TKey>(TKey key)
			where TKey : IFdbKey
		{
			return m_database.ConcatKey<TKey>(key);
		}

		public virtual Slice[] ConcatKeys(IEnumerable<Slice> keys)
		{
			return m_database.ConcatKeys(keys);
		}

		public virtual Slice[] ConcatKeys<TKey>(IEnumerable<TKey> keys)
			where TKey : IFdbKey
		{
			return m_database.ConcatKeys<TKey>(keys);
		}

		public virtual Slice ExtractKey(Slice key, bool boundCheck = false)
		{
			return m_database.ExtractKey(key, boundCheck);
		}

		public virtual Slice[] ExtractKeys(IEnumerable<Slice> keys, bool boundCheck = false)
		{
			return m_database.ExtractKeys(keys, boundCheck);
		}

		public virtual SliceWriter GetWriter(int capacity = 0)
		{
			return m_database.GetWriter(capacity);
		}

		public virtual IDynamicKeyEncoder Encoder
		{
			get { return m_database.Encoder; }
		}

		#endregion

		#region Transactionals...

		public virtual IFdbTransaction BeginTransaction(FdbTransactionMode mode, CancellationToken cancellationToken = default(CancellationToken), FdbOperationContext context = null)
		{
			ThrowIfDisposed();

			// enforce read-only mode!
			if (m_readOnly) mode |= FdbTransactionMode.ReadOnly;

			if (context == null)
			{
				context = new FdbOperationContext(this, mode, cancellationToken);
			}

			return m_database.BeginTransaction(mode, cancellationToken, context);
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

		public int DefaultMaxRetryDelay
		{
			get { return m_database.DefaultMaxRetryDelay; }
			set
			{
				ThrowIfDisposed();
				m_database.DefaultMaxRetryDelay = value;
			}
		}

		#endregion

		#region IDisposable Members...

		protected void ThrowIfDisposed()
		{
			// this should be inlined by the caller
			if (m_disposed) ThrowFilterAlreadyDisposed(this);
		}

		[ContractAnnotation("=> halt")]
		private static void ThrowFilterAlreadyDisposed([NotNull] FdbDatabaseFilter filter)
		{
			throw new ObjectDisposedException(filter.GetType().Name);
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
