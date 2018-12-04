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

namespace FoundationDB.DependencyInjection
{
	using System;
	using System.Runtime.CompilerServices;
	using System.Threading;
	using System.Threading.Tasks;
	using Doxense.Diagnostics.Contracts;
	using FoundationDB.Client;
	using JetBrains.Annotations;

	/// <summary>Default implementation of a child database scope provider</summary>
	/// <typeparam name="TState">Type of the State created by the init handler of this scope</typeparam>
	internal sealed class FdbDatabaseScopeProvider<TState> : IFdbDatabaseScopeProvider<TState>
	{

		public FdbDatabaseScopeProvider([NotNull] IFdbDatabaseScopeProvider parent, [NotNull] Func<IFdbDatabase, CancellationToken, Task<(IFdbDatabase, TState)>> handler, [NotNull] CancellationTokenSource lifetime)
		{
			Contract.Requires(parent != null && handler != null && lifetime != null);
			this.Parent = parent;
			this.Handler = handler;
			this.LifeTime = lifetime;
			this.DbTask = new Lazy<Task>(this.InitAsync, LazyThreadSafetyMode.ExecutionAndPublication);
		}

		[NotNull]
		public IFdbDatabaseScopeProvider Parent { get; }

		[NotNull]
		public Func<IFdbDatabase, CancellationToken, Task<(IFdbDatabase, TState)>> Handler { get; }

		private readonly Lazy<Task> DbTask;

		internal IFdbDatabase Db { get; private set; }

		internal TState State { get; private set; }

		internal Exception Error { get; private set; }

		private CancellationTokenSource LifeTime { get; }

		private async Task InitAsync()
		{
			var ct = this.LifeTime.Token;
			try
			{
				var db = await this.Parent.GetDatabase(ct).ConfigureAwait(false);
				ct.ThrowIfCancellationRequested();
				TState state;
				(db, state) = await this.Handler(db, ct).ConfigureAwait(false);
				Contract.Assert(db != null);
				this.State = state;
				this.Db = db;
			}
			catch (Exception e)
			{
				this.Error = e;
				this.Db = null;
				this.State = default;
				throw;
			}
		}

		public IFdbDatabaseScopeProvider<TNewState> CreateScope<TNewState>(Func<IFdbDatabase, CancellationToken, Task<(IFdbDatabase, TNewState)>> start)
		{
			Contract.NotNull(start, nameof(start));
			return new FdbDatabaseScopeProvider<TNewState>(this, start, this.LifeTime);
		}

		public bool IsAvailable => this.DbTask.IsValueCreated && this.DbTask.Value.Status == TaskStatus.RanToCompletion;

		public ValueTask<IFdbDatabase> GetDatabase(CancellationToken ct = default)
		{
			//BUGBUG: what if the parent scope has been shut down?
			var db = this.Db;
			return db != null ? new ValueTask<IFdbDatabase>(db) : GetDatabaseSlow(ct);
		}

		public TState GetState()
		{
			//REVIEW: do we need some sort of locking/volatile reads?
			if (this.Db == null) throw new InvalidOperationException("The state can only be accessed when the database becomes available.");
			return this.State;
		}

		[MethodImpl(MethodImplOptions.NoInlining)]
		private async ValueTask<IFdbDatabase> GetDatabaseSlow(CancellationToken ct)
		{
			var t = this.DbTask.Value;

			if (!t.IsCompleted && ct.CanBeCanceled)
			{
				//REVIEW: is there a faster way to do this? (we want to make sure not to leak tons of Task.Delay(...) that will linger on until the original ct is triggered
				using (var cts = CancellationTokenSource.CreateLinkedTokenSource(ct))
				{
					await Task.WhenAny(t, Task.Delay(Timeout.InfiniteTimeSpan, cts.Token)).ConfigureAwait(false);
					ct.ThrowIfCancellationRequested();
				}
			}
			await t.ConfigureAwait(false);
			return this.Db;
		}

		public void Dispose()
		{
			this.LifeTime?.Cancel();
		}
	}

}
