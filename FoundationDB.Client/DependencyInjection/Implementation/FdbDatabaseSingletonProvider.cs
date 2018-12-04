using System;

namespace FoundationDB.DependencyInjection
{
	using System.Threading;
	using System.Threading.Tasks;
	using Doxense.Diagnostics.Contracts;
	using FoundationDB.Client;
	using JetBrains.Annotations;

	/// <summary>Default implementation of a scope provider that uses a pre-initialized database instance</summary>
	/// <typeparam name="TState">Type of the state that will be passed to consumers of this type</typeparam>
	internal sealed class FdbDatabaseSingletonProvider<TState> : IFdbDatabaseScopeProvider<TState>
	{

		public FdbDatabaseSingletonProvider([NotNull] IFdbDatabase db, TState state)
		{
			this.Db = db;
			this.State = state;
		}

		public IFdbDatabase Db { get; private set; }

		public TState State { get; private set; }

		private bool m_disposed;

		public TState GetState()
		{
			return this.State;
		}

		public void Dispose()
		{
			lock (this)
			{
				if (!m_disposed)
				{
					m_disposed = true;
					this.State = default;
					this.Db = null;
				}
			}
		}

		public IFdbDatabaseScopeProvider Parent => null;

		public bool IsAvailable => !Volatile.Read(ref m_disposed);

		public ValueTask<IFdbDatabase> GetDatabase(CancellationToken ct)
		{
			lock (this)
			{
				if (m_disposed) throw ThrowHelper.ObjectDisposedException(this);
				return new ValueTask<IFdbDatabase>(this.Db);
			}
		}

		public IFdbDatabaseScopeProvider<TNewState> CreateScope<TNewState>(Func<IFdbDatabase, CancellationToken, Task<(IFdbDatabase Db, TNewState State)>> start)
		{
			return Fdb.CreateScope<TNewState>(this, start, this.Db.Cancellation);
		}

	}
}
