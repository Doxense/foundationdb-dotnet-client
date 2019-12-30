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
ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT
(INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS
SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
 */
#endregion

namespace FoundationDB.Filters
{
	using System;
	using System.Diagnostics;
	using System.Runtime.CompilerServices;
	using System.Threading;
	using System.Threading.Tasks;
	using Doxense.Diagnostics.Contracts;
	using Doxense.Serialization.Encoders;
	using FoundationDB.Client;
	using FoundationDB.Layers.Directories;
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
		protected IFdbDirectory m_directory;

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
		[NotNull]
		protected IFdbDatabase Database => m_database;

		[NotNull]
		internal IFdbDatabase GetInnerDatabase()
		{
			return m_database;
		}

		/// <summary>Name of the database</summary>
		[Obsolete("This property is not supported anymore and will always return \"DB\".")]
		public string Name => m_database.Name;

		/// <summary>Path to the cluster file used to connect to the database</summary>
		public string ClusterFile => m_database.ClusterFile;

		/// <summary>Returns a cancellation token that is linked with the lifetime of this database instance</summary>
		public CancellationToken Cancellation => m_database.Cancellation;

		/// <summary>Returns the global namespace used by this database instance</summary>
		public virtual IDynamicKeySubspace GlobalSpace => m_database.GlobalSpace;

		/// <summary>Directory partition of this database instance</summary>
		public virtual IFdbDirectory Directory
		{
			get
			{
				if (m_directory == null || !object.ReferenceEquals(m_directory, m_database.Directory))
				{
					m_directory = m_database.Directory;
				}
				return m_directory;
			}
		}

		/// <summary>If true, this database instance will only allow starting read-only transactions.</summary>
		public virtual bool IsReadOnly => m_readOnly;

		Slice IKeySubspace.GetPrefix()
		{
			return this.GlobalSpace.GetPrefix();
		}

		KeyRange IKeySubspace.ToRange()
		{
			return this.GlobalSpace.ToRange();
		}

		public virtual DynamicPartition Partition => m_database.Partition;

		public virtual DynamicKeys Keys => m_database.Keys;

		public virtual bool Contains(Slice key)
		{
			return m_database.Contains(key);
		}

		public virtual bool Contains(ReadOnlySpan<byte> key)
		{
			return m_database.Contains(key);
		}

		public virtual Slice BoundCheck(Slice key, bool allowSystemKeys)
		{
			return m_database.BoundCheck(key, allowSystemKeys);
		}

		public virtual ReadOnlySpan<byte> BoundCheck(ReadOnlySpan<byte> key, bool allowSystemKeys)
		{
			return m_database.BoundCheck(key, allowSystemKeys);
		}

		public virtual Slice this[Slice key] => m_database[key];

		public virtual Slice ExtractKey(Slice key, bool boundCheck = false)
		{
			return m_database.ExtractKey(key, boundCheck);
		}

		public virtual IKeyEncoding Encoding => m_database.Encoding;

		#endregion

		#region Transactionals...

		public virtual ValueTask<IFdbTransaction> BeginTransactionAsync(FdbTransactionMode mode, CancellationToken ct = default, FdbOperationContext context = null)
		{
			ThrowIfDisposed();

			// enforce read-only mode!
			if (m_readOnly) mode |= FdbTransactionMode.ReadOnly;

			if (context == null)
			{
				context = new FdbOperationContext(this, mode, ct);
			}

			return m_database.BeginTransactionAsync(mode, ct, context);
		}

		#region ReadOnly...

		public Task<TResult> ReadAsync<TResult>(Func<IFdbReadOnlyTransaction, Task<TResult>> handler, CancellationToken ct)
		{
			ThrowIfDisposed();
			return FdbOperationContext.RunReadWithResultAsync<TResult>(this, handler, ct);
		}

		public Task<TResult> ReadAsync<TState, TResult>(TState state, Func<IFdbReadOnlyTransaction, TState, Task<TResult>> handler, CancellationToken ct)
		{
			ThrowIfDisposed();
			return FdbOperationContext.RunReadWithResultAsync<TResult>(this, (tr) => handler(tr, state), ct);
		}

		public Task<TResult> ReadAsync<TResult>(Func<IFdbReadOnlyTransaction, Task<TResult>> handler, Action<IFdbReadOnlyTransaction, TResult> success, CancellationToken ct)
		{
			ThrowIfDisposed();
			return FdbOperationContext.RunReadWithResultAsync<TResult>(this, handler, success, ct);
		}

		public Task<TResult> ReadAsync<TIntermediate, TResult>(Func<IFdbReadOnlyTransaction, Task<TIntermediate>> handler, Func<IFdbReadOnlyTransaction, TIntermediate, TResult> success, CancellationToken ct)
		{
			ThrowIfDisposed();
			return FdbOperationContext.RunReadWithResultAsync<TIntermediate, TResult>(this, handler, success, ct);
		}

		public Task<TResult> ReadAsync<TIntermediate, TResult>(Func<IFdbReadOnlyTransaction, Task<TIntermediate>> handler, Func<IFdbReadOnlyTransaction, TIntermediate, Task<TResult>> success, CancellationToken ct)
		{
			ThrowIfDisposed();
			return FdbOperationContext.RunReadWithResultAsync<TIntermediate, TResult>(this, handler, success, ct);
		}

		#endregion

		#region Write Only...

		public Task WriteAsync(Action<IFdbTransaction> handler, CancellationToken ct)
		{
			ThrowIfDisposed();
			return FdbOperationContext.RunWriteAsync(this, handler, ct);
		}

		public Task WriteAsync<TState>(TState state, Action<IFdbTransaction, TState> handler, CancellationToken ct)
		{
			ThrowIfDisposed();
			return FdbOperationContext.RunWriteAsync(this, (tr) => handler(tr, state), ct);
		}

		public Task WriteAsync(Action<IFdbTransaction> handler, Action<IFdbTransaction> success, CancellationToken ct)
		{
			ThrowIfDisposed();
			return FdbOperationContext.RunWriteAsync(this, handler, success, ct);
		}

		public Task WriteAsync(Action<IFdbTransaction> handler, Func<IFdbTransaction, Task> success, CancellationToken ct)
		{
			ThrowIfDisposed();
			return FdbOperationContext.RunWriteAsync(this, handler, success, ct);
		}

		public Task<TResult> WriteAsync<TResult>(Action<IFdbTransaction> handler, Func<IFdbTransaction, TResult> success, CancellationToken ct)
		{
			ThrowIfDisposed();
			return FdbOperationContext.RunWriteAsync<TResult>(this, handler, success, ct);
		}

		public Task WriteAsync(Func<IFdbTransaction, Task> handler, CancellationToken ct)
		{
			ThrowIfDisposed();
			return FdbOperationContext.RunWriteAsync(this, handler, ct);
		}

		public Task WriteAsync<TState>(TState state, Func<IFdbTransaction, TState, Task> handler, CancellationToken ct)
		{
			ThrowIfDisposed();
			return FdbOperationContext.RunWriteAsync(this, (tr) => handler(tr, state), ct);
		}

		public Task WriteAsync(Func<IFdbTransaction, Task> handler, Action<IFdbTransaction> success, CancellationToken ct)
		{
			ThrowIfDisposed();
			return FdbOperationContext.RunWriteAsync(this, handler, success, ct);
		}

		public Task WriteAsync(Func<IFdbTransaction, Task> handler, Func<IFdbTransaction, Task> success, CancellationToken ct)
		{
			ThrowIfDisposed();
			return FdbOperationContext.RunWriteAsync(this, handler, success, ct);
		}

		#endregion

		#region Read+Write...

		public Task ReadWriteAsync(Func<IFdbTransaction, Task> handler, CancellationToken ct)
		{
			ThrowIfDisposed();
			return FdbOperationContext.RunWriteAsync(this, handler, ct);
		}

		public Task ReadWriteAsync<TState>(TState state, Func<IFdbTransaction, TState, Task> handler, CancellationToken ct)
		{
			ThrowIfDisposed();
			return FdbOperationContext.RunWriteAsync(this, (tr) => handler(tr, state), ct);
		}

		public Task ReadWriteAsync(Func<IFdbTransaction, Task> handler, Action<IFdbTransaction> onDone, CancellationToken ct)
		{
			ThrowIfDisposed();
			return FdbOperationContext.RunWriteAsync(this, handler, onDone, ct);
		}

		public Task<TResult> ReadWriteAsync<TResult>(Func<IFdbTransaction, TResult> handler, CancellationToken ct)
		{
			ThrowIfDisposed();
			return FdbOperationContext.RunWriteWithResultAsync<TResult>(this, handler, ct);
		}

		public Task<TResult> ReadWriteAsync<TResult>(Func<IFdbTransaction, Task<TResult>> handler, CancellationToken ct)
		{
			ThrowIfDisposed();
			return FdbOperationContext.RunWriteWithResultAsync<TResult>(this, handler, ct);
		}

		public Task<TResult> ReadWriteAsync<TState, TResult>(TState state, Func<IFdbTransaction, TState, Task<TResult>> handler, CancellationToken ct)
		{
			ThrowIfDisposed();
			return FdbOperationContext.RunWriteWithResultAsync<TResult>(this, (tr) => handler(tr, state), ct);
		}

		public Task<TResult> ReadWriteAsync<TResult>(Func<IFdbTransaction, Task<TResult>> handler, Action<IFdbTransaction, TResult> onDone, CancellationToken ct)
		{
			ThrowIfDisposed();
			return FdbOperationContext.RunWriteWithResultAsync<TResult>(this, handler, onDone, ct);
		}

		public Task<TResult> ReadWriteAsync<TIntermediate, TResult>(Func<IFdbTransaction, Task<TIntermediate>> handler, Func<IFdbTransaction, TIntermediate, TResult> onDone, CancellationToken ct)
		{
			ThrowIfDisposed();
			return FdbOperationContext.RunWriteWithResultAsync<TIntermediate, TResult>(this, handler, onDone, ct);
		}

		public Task<TResult> ReadWriteAsync<TIntermediate, TResult>(Func<IFdbTransaction, Task<TIntermediate>> handler, Func<IFdbTransaction, TIntermediate, Task<TResult>> onDone, CancellationToken ct)
		{
			ThrowIfDisposed();
			return FdbOperationContext.RunWriteWithResultAsync<TIntermediate, TResult>(this, handler, onDone, ct);
		}

		#endregion

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
			get => m_database.DefaultTimeout;
			set
			{
				ThrowIfDisposed();
				m_database.DefaultTimeout = value;
			}
		}

		public int DefaultRetryLimit
		{
			get => m_database.DefaultRetryLimit;
			set
			{
				ThrowIfDisposed();
				m_database.DefaultRetryLimit = value;
			}
		}

		public int DefaultMaxRetryDelay
		{
			get => m_database.DefaultMaxRetryDelay;
			set
			{
				ThrowIfDisposed();
				m_database.DefaultMaxRetryDelay = value;
			}
		}

		#endregion

		#region IDisposable Members...

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		protected void ThrowIfDisposed()
		{
			if (m_disposed) throw ThrowFilterAlreadyDisposed(this);
		}

		[Pure, NotNull, MethodImpl(MethodImplOptions.NoInlining)]
		private Exception ThrowFilterAlreadyDisposed([NotNull] FdbDatabaseFilter filter)
		{
			return new ObjectDisposedException(filter.GetType().Name);
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

	}

}
