#region Copyright Doxense 2017-2018
//
// All rights are reserved. Reproduction or transmission in whole or in part, in
// any form or by any means, electronic, mechanical or otherwise, is prohibited
// without the prior written consent of the copyright owner.
//
#endregion

namespace FoundationDB.DependencyInjection
{
	using System;
	using System.Threading;
	using System.Threading.Tasks;
	using Doxense.Diagnostics.Contracts;
	using FoundationDB.Client;
	using JetBrains.Annotations;

	/// <summary>Default implementation of a scope provider that uses a pre-initialized database instance</summary>
	/// <typeparam name="TState">Type of the state that will be passed to consumers of this type</typeparam>
	internal sealed class FdbDatabaseSingletonProvider<TState> : IFdbDatabaseScopeProvider<TState>
	{

		public FdbDatabaseSingletonProvider([NotNull] IFdbDatabase db, [CanBeNull] TState state, [NotNull] CancellationTokenSource lifetime)
		{
			Contract.Requires(db != null && lifetime != null);
			this.Db = db;
			this.State = state;
			this.Lifetime = lifetime;
		}

		public IFdbDatabase Db { get; private set; }

		public TState State { get; private set; }

		private CancellationTokenSource Lifetime { get; }

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
					this.Lifetime.Cancel();
					this.State = default;
					this.Db = null;
				}
			}
		}

		public IFdbDatabaseScopeProvider Parent => null;

		public bool IsAvailable => !Volatile.Read(ref m_disposed);

		public CancellationToken Cancellation => this.Lifetime.Token;

		public ValueTask<IFdbDatabase> GetDatabase(CancellationToken ct)
		{
			lock (this)
			{
				if (m_disposed) throw ThrowHelper.ObjectDisposedException(this);
				return new ValueTask<IFdbDatabase>(this.Db);
			}
		}

		public IFdbDatabaseScopeProvider<TNewState> CreateScope<TNewState>(Func<IFdbDatabase, CancellationToken, Task<(IFdbDatabase Db, TNewState State)>> start, CancellationToken lifetime = default)
		{
			if (m_disposed) throw ThrowHelper.ObjectDisposedException(this);
			return new FdbDatabaseScopeProvider<TNewState>(this, start, lifetime);
		}

	}

}
