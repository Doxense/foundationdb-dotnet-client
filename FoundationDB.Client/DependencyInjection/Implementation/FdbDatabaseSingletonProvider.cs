#region Copyright Doxense 2017-2019
//
// All rights are reserved. Reproduction or transmission in whole or in part, in
// any form or by any means, electronic, mechanical or otherwise, is prohibited
// without the prior written consent of the copyright owner.
//
#endregion

namespace FoundationDB.DependencyInjection
{
	using System;
	using System.Runtime.CompilerServices;
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
			this.Lifetime = lifetime;
			Volatile.Write(ref this.InternalState, new Scope(db, state));
			Interlocked.MemoryBarrier();
		}

		private sealed class Scope
		{
			[NotNull]
			public readonly IFdbDatabase Db;

			[CanBeNull]
			public readonly TState State;

			public Scope([NotNull] IFdbDatabase db, [CanBeNull] TState state)
			{
				this.Db = db;
				this.State = state;
			}
		}

		[CanBeNull]
		private Scope InternalState;

		private CancellationTokenSource Lifetime { get; }

		private int m_disposed;

		public void Dispose()
		{
			if (Interlocked.Exchange(ref this.InternalState, null) != null)
			{
				this.Lifetime.Cancel();
			}
		}

		public IFdbDatabaseScopeProvider Parent => null;

		public bool IsAvailable => Volatile.Read(ref m_disposed) == 0;

		public CancellationToken Cancellation => this.Lifetime.Token;

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private Scope EnsureReady()
		{
			var scope = Volatile.Read(ref this.InternalState);
			if (scope == null) throw ThrowHelper.ObjectDisposedException(this);
			return scope;
		}

		public ValueTask<IFdbDatabase> GetDatabase(CancellationToken ct = default)
		{
			var scope = EnsureReady();
			return new ValueTask<IFdbDatabase>(scope.Db);
		}

		public ValueTask<TState> GetState(IFdbReadOnlyTransaction tr)
		{
			tr.Cancellation.ThrowIfCancellationRequested();
			var scope = EnsureReady();
			return new ValueTask<TState>(scope.State);
		}

		public ValueTask<(IFdbDatabase Database, TState State)> GetDatabaseAndState(CancellationToken ct = default)
		{
			var scope = EnsureReady();
			return new ValueTask<(IFdbDatabase, TState)>((scope.Db, scope.State));
		}

		public IFdbDatabaseScopeProvider<TNewState> CreateScope<TNewState>(Func<IFdbDatabase, CancellationToken, Task<(IFdbDatabase Db, TNewState State)>> start, CancellationToken lifetime = default)
		{
			_ = EnsureReady();
			return new FdbDatabaseScopeProvider<TNewState>(this, start, lifetime);
		}

	}

}
