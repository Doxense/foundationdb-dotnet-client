#region BSD License
/* Copyright (c) 2013-2020, Doxense SAS
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
	using FoundationDB.Client;
	using JetBrains.Annotations;

	/// <summary>Base class for simple database filters</summary>
	[DebuggerDisplay("ClusterFile={m_database.ClusterFile}")]
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

		/// <summary>Wrapper for the inner db's Root property</summary>
		protected FdbDirectorySubspaceLocation? m_root;

		#endregion

		#region Constructors...

		protected FdbDatabaseFilter(IFdbDatabase database, bool forceReadOnly, bool ownsDatabase)
		{
			Contract.NotNull(database, nameof(database));

			m_database = database;
			m_readOnly = forceReadOnly || database.IsReadOnly;
			m_owner = ownsDatabase;
		}

		#endregion

		#region Public Properties...

		/// <summary>Database instance configured to read and write data from this partition</summary>
		protected IFdbDatabase Database => m_database;

		internal IFdbDatabase GetInnerDatabase()
		{
			return m_database;
		}

		/// <inheritdoc/>
		[Obsolete("This property is not supported anymore and will always return \"DB\".")]
		public string Name => m_database.Name;

		/// <inheritdoc/>
		public string? ClusterFile => m_database.ClusterFile;

		/// <inheritdoc/>
		public CancellationToken Cancellation => m_database.Cancellation;

		/// <inheritdoc/>
		public virtual FdbDirectorySubspaceLocation Root
		{
			get
			{
				if (m_root == null || !object.ReferenceEquals(m_root, m_database.Root))
				{
					m_root = m_database.Root;
				}
				return m_root;
			}
		}

		/// <inheritdoc/>
		public virtual FdbDirectoryLayer DirectoryLayer => this.Root.Directory;

		/// <inheritdoc/>
		public virtual bool IsReadOnly => m_readOnly;

		#endregion

		#region Transactionals...

		public virtual ValueTask<IFdbTransaction> BeginTransactionAsync(FdbTransactionMode mode, CancellationToken ct = default, FdbOperationContext? context = null)
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

		#region IFdbReadOnlyRetryable...

		private Task<TResult> ExecuteReadOnlyAsync<TState, TIntermediate, TResult>(TState state, Delegate handler, Delegate? success, CancellationToken ct)
		{
			Contract.NotNull(handler, nameof(handler));
			if (ct.IsCancellationRequested) return Task.FromCanceled<TResult>(ct);
			ThrowIfDisposed();

			var context = new FdbOperationContext(this, FdbTransactionMode.ReadOnly | FdbTransactionMode.InsideRetryLoop, ct);
			return FdbOperationContext.ExecuteInternal<TState, TIntermediate, TResult>(context, state, handler, success);
		}

		/// <inheritdoc/>
		public Task ReadAsync(Func<IFdbReadOnlyTransaction, Task> handler, CancellationToken ct)
		{
			return ExecuteReadOnlyAsync<object?, object?, object?>(null, handler, null, ct);
		}

		/// <inheritdoc/>
		public Task ReadAsync<TState>(TState state, Func<IFdbReadOnlyTransaction, TState, Task> handler, CancellationToken ct)
		{
			return ExecuteReadOnlyAsync<TState, object?, object?>(state, handler, null, ct);
		}

		/// <inheritdoc/>
		public Task<TResult> ReadAsync<TResult>(Func<IFdbReadOnlyTransaction, Task<TResult>> handler, CancellationToken ct)
		{
			return ExecuteReadOnlyAsync<object?, TResult, TResult>(null, handler, null, ct);
		}

		/// <inheritdoc/>
		public Task<TResult> ReadAsync<TState, TResult>(TState state, Func<IFdbReadOnlyTransaction, TState, Task<TResult>> handler, CancellationToken ct)
		{
			return ExecuteReadOnlyAsync<TState, TResult, TResult>(state, handler, null, ct);
		}

		/// <inheritdoc/>
		public Task<TResult> ReadAsync<TResult>(Func<IFdbReadOnlyTransaction, Task<TResult>> handler, Action<IFdbReadOnlyTransaction, TResult> success, CancellationToken ct)
		{
			return ExecuteReadOnlyAsync<object?, TResult, TResult>(null, handler, success, ct);
		}

		/// <inheritdoc/>
		public Task<TResult> ReadAsync<TIntermediate, TResult>(Func<IFdbReadOnlyTransaction, Task<TIntermediate>> handler, Func<IFdbReadOnlyTransaction, TIntermediate, TResult> success, CancellationToken ct)
		{
			return ExecuteReadOnlyAsync<object?, TIntermediate, TResult>(null, handler, success, ct);
		}

		/// <inheritdoc/>
		public Task<TResult> ReadAsync<TIntermediate, TResult>(Func<IFdbReadOnlyTransaction, Task<TIntermediate>> handler, Func<IFdbReadOnlyTransaction, TIntermediate, Task<TResult>> success, CancellationToken ct)
		{
			return ExecuteReadOnlyAsync<object?, TIntermediate, TResult>(null, handler, success, ct);
		}

		/// <inheritdoc/>
		public Task<TResult> ReadAsync<TState, TIntermediate, TResult>(TState state, Func<IFdbReadOnlyTransaction, TState, Task<TIntermediate>> handler, Func<IFdbReadOnlyTransaction, TIntermediate, Task<TResult>> success, CancellationToken ct)
		{
			return ExecuteReadOnlyAsync<TState, TIntermediate, TResult>(state, handler, success, ct);
		}

		#endregion

		#region IFdbRetryable...

		private Task<TResult> ExecuteReadWriteAsync<TState, TIntermediate, TResult>(TState state, Delegate handler, Delegate? success, CancellationToken ct)
		{
			Contract.NotNull(handler, nameof(handler));
			if (ct.IsCancellationRequested) return Task.FromCanceled<TResult>(ct);
			ThrowIfDisposed();
			if (m_readOnly) throw new InvalidOperationException("Cannot mutate a read-only database.");

			var context = new FdbOperationContext(this, FdbTransactionMode.Default | FdbTransactionMode.InsideRetryLoop, ct);
			return FdbOperationContext.ExecuteInternal<TState, TIntermediate, TResult>(context, state, handler, success);
		}

		/// <inheritdoc/>
		public Task WriteAsync(Action<IFdbTransaction> handler, CancellationToken ct)
		{
			return ExecuteReadWriteAsync<object?, object?, object?>(null, handler, null, ct);
		}

		/// <inheritdoc/>
		public Task WriteAsync<TState>(TState state, Action<IFdbTransaction, TState> handler, CancellationToken ct)
		{
			return ExecuteReadWriteAsync<TState, object?, object?>(state, handler, null, ct);
		}

		/// <inheritdoc/>
		public Task WriteAsync(Func<IFdbTransaction, Task> handler, CancellationToken ct)
		{
			return ExecuteReadWriteAsync<object?, object?, object?>(null, handler, null, ct);
		}

		/// <inheritdoc/>
		public Task WriteAsync<TState>(TState state, Func<IFdbTransaction, TState, Task> handler, CancellationToken ct)
		{
			return ExecuteReadWriteAsync<TState, object?, object?>(state, handler, null, ct);
		}

		/// <inheritdoc/>
		public Task WriteAsync(Action<IFdbTransaction> handler, Action<IFdbTransaction> success, CancellationToken ct)
		{
			return ExecuteReadWriteAsync<object?, object?, object?>(null, handler, success, ct);
		}

		/// <inheritdoc/>
		public Task WriteAsync(Action<IFdbTransaction> handler, Func<IFdbTransaction, Task> success, CancellationToken ct)
		{
			return ExecuteReadWriteAsync<object?, object?, object?>(null, handler, success, ct);
		}

		/// <inheritdoc/>
		public Task<TResult> ReadWriteAsync<TResult>(Action<IFdbTransaction> handler, Func<IFdbTransaction, TResult> success, CancellationToken ct)
		{
			return ExecuteReadWriteAsync<object?, object?, TResult>(null, handler, success, ct);
		}

		/// <inheritdoc/>
		public Task WriteAsync(Func<IFdbTransaction, Task> handler, Action<IFdbTransaction> success, CancellationToken ct)
		{
			return ExecuteReadWriteAsync<object?, object?, object?>(null, handler, success, ct);
		}

		/// <inheritdoc/>
		public Task WriteAsync(Func<IFdbTransaction, Task> handler, Func<IFdbTransaction, Task> success, CancellationToken ct)
		{
			return ExecuteReadWriteAsync<object?, object?, object?>(null, handler, success, ct);
		}

		/// <inheritdoc/>
		public Task<TResult> ReadWriteAsync<TResult>(Func<IFdbTransaction, Task<TResult>> handler, CancellationToken ct)
		{
			return ExecuteReadWriteAsync<object?, TResult, TResult>(null, handler, null, ct);
		}

		/// <inheritdoc/>
		public Task<TResult> ReadWriteAsync<TState, TResult>(TState state, Func<IFdbTransaction, TState, Task<TResult>> handler, CancellationToken ct)
		{
			return ExecuteReadWriteAsync<TState, TResult, TResult>(state, handler, null, ct);
		}

		/// <inheritdoc/>
		public Task<TResult> ReadWriteAsync<TResult>(Func<IFdbTransaction, Task<TResult>> handler, Action<IFdbTransaction, TResult> success, CancellationToken ct)
		{
			return ExecuteReadWriteAsync<object?, TResult, TResult>(null, handler, success, ct);
		}

		/// <inheritdoc/>
		public Task<TResult> ReadWriteAsync<TResult>(Func<IFdbTransaction, Task> handler, Func<IFdbTransaction, Task<TResult>> success, CancellationToken ct)
		{
			return ExecuteReadWriteAsync<object?, object?, TResult>(null, handler, success, ct);
		}

		/// <inheritdoc/>
		public Task<TResult> ReadWriteAsync<TResult>(Func<IFdbTransaction, Task> handler, Func<IFdbTransaction, TResult> success, CancellationToken ct)
		{
			return ExecuteReadWriteAsync<object?, object?, TResult>(null, handler, success, ct);
		}

		/// <inheritdoc/>
		public Task<TResult> ReadWriteAsync<TIntermediate, TResult>(Func<IFdbTransaction, Task<TIntermediate>> handler, Func<IFdbTransaction, TIntermediate, TResult> success, CancellationToken ct)
		{
			return ExecuteReadWriteAsync<object?, TIntermediate, TResult>(null, handler, success, ct);
		}

		/// <inheritdoc/>
		public Task<TResult> ReadWriteAsync<TIntermediate, TResult>(Func<IFdbTransaction, Task<TIntermediate>> handler, Func<IFdbTransaction, TIntermediate, Task<TResult>> success, CancellationToken ct)
		{
			return ExecuteReadWriteAsync<object?, TIntermediate, TResult>(null, handler, success, ct);
		}

		/// <inheritdoc/>
		public Task<TResult> ReadWriteAsync<TState, TIntermediate, TResult>(TState state, Func<IFdbTransaction, TState, Task<TIntermediate>> handler, Func<IFdbTransaction, TIntermediate, Task<TResult>> success, CancellationToken ct)
		{
			return ExecuteReadWriteAsync<TState, TIntermediate, TResult>(state, handler, success, ct);
		}

		#endregion

		#endregion

		#region Options...

		public virtual void SetOption(FdbDatabaseOption option)
		{
			ThrowIfDisposed();
			m_database.SetOption(option);
		}

		public virtual void SetOption(FdbDatabaseOption option, string? value)
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

		[Pure, MethodImpl(MethodImplOptions.NoInlining)]
		private Exception ThrowFilterAlreadyDisposed(FdbDatabaseFilter filter)
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
