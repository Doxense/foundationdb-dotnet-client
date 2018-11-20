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

	internal sealed class FdbDatabaseScopeProvider : IFdbDatabaseScopeProvider
	{

		public FdbDatabaseScopeProvider([NotNull] IFdbDatabaseScopeProvider parent, [NotNull] Func<IFdbDatabase, CancellationToken, Task<IFdbDatabase>> handler, [NotNull] CancellationTokenSource lifetime)
		{
			Contract.NotNull(parent, nameof(parent));
			Contract.NotNull(handler, nameof(handler));
			Contract.NotNull(lifetime, nameof(lifetime));
			this.Parent = parent;
			this.Handler = handler;
			this.LifeTime = lifetime;
			this.DbTask = new Lazy<Task<IFdbDatabase>>(this.InitAsync, LazyThreadSafetyMode.ExecutionAndPublication);
		}

		[NotNull]
		public IFdbDatabaseScopeProvider Parent { get; }

		public Func<IFdbDatabase, CancellationToken, Task<IFdbDatabase>> Handler { get; }

		private readonly Lazy<Task<IFdbDatabase>> DbTask;

		private IFdbDatabase Db { get; set; }

		private CancellationTokenSource LifeTime { get; }

		private async Task<IFdbDatabase> InitAsync()
		{
			var ct = this.LifeTime.Token;
			var db = await this.Parent.GetDatabase(ct).ConfigureAwait(false);
			ct.ThrowIfCancellationRequested();
			db = await this.Handler(db, ct).ConfigureAwait(false);
			this.Db = db;
			return db;
		}

		public IFdbDatabaseScopeProvider CreateScope(Func<IFdbDatabase, CancellationToken, Task<IFdbDatabase>> start)
		{
			return new FdbDatabaseScopeProvider(this, start, this.LifeTime);
		}

		public bool IsAvailable => this.DbTask.IsValueCreated && this.DbTask.Value.Status == TaskStatus.RanToCompletion;

		public ValueTask<IFdbDatabase> GetDatabase(CancellationToken ct = default)
		{
			//BUGBUG: what if the parent scope has been shut down?
			var db = this.Db;
			return db != null ? new ValueTask<IFdbDatabase>(db) : GetDatabaseSlow(ct);
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
			return await t.ConfigureAwait(false);
		}

		public void Dispose()
		{
			this.LifeTime?.Cancel();
		}
	}
}
