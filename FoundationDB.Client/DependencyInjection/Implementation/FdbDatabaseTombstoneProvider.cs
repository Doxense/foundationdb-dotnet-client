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

	internal sealed class FdbDatabaseTombstoneProvider<TState> : IFdbDatabaseScopeProvider<TState>
	{

		public FdbDatabaseTombstoneProvider([CanBeNull] IFdbDatabaseScopeProvider parent, [NotNull] Exception error, CancellationToken lifetime)
		{
			Contract.Requires(error != null);
			this.Parent = parent;
			this.Error = error;
			this.FailedTask = Task.FromException<IFdbDatabase>(error);
			this.Lifetime = parent != null ? CancellationTokenSource.CreateLinkedTokenSource(parent.Cancellation, lifetime) : CancellationTokenSource.CreateLinkedTokenSource(lifetime);
		}

		public IFdbDatabaseScopeProvider Parent { get; }

		public Exception Error { get; private set; }

		private Task<IFdbDatabase> FailedTask { get; set; }

		public TState GetState() => default;

		private CancellationTokenSource Lifetime { get; }

		public CancellationToken Cancellation => this.Lifetime.Token;

		private bool m_disposed;

		public void Dispose()
		{
			if (!m_disposed)
			{
				m_disposed = true;
				try
				{
					this.Lifetime.Cancel();
				}
				finally
				{
					this.Lifetime.Dispose();
					this.FailedTask = null;
					this.Error = null;
				}
			}
		}

		public ValueTask<IFdbDatabase> GetDatabase(CancellationToken ct)
		{
			return new ValueTask<IFdbDatabase>(
				ct.IsCancellationRequested ? Task.FromCanceled<IFdbDatabase>(ct)
				: m_disposed ? Task.FromException<IFdbDatabase>(ThrowHelper.ObjectDisposedException(this))
				: this.FailedTask
			);
		}

		public IFdbDatabaseScopeProvider<TNewState> CreateScope<TNewState>(Func<IFdbDatabase, CancellationToken, Task<(IFdbDatabase Db, TNewState State)>> start, CancellationToken lifetime = default)
		{
			// poison all child scopes with the same error
			return new FdbDatabaseTombstoneProvider<TNewState>(this, this.Error, lifetime);
		}

		public bool IsAvailable => false;

	}
}
