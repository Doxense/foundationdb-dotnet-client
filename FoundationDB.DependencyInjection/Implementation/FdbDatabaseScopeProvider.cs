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
	using System.Threading;
	using System.Threading.Tasks;
	using FoundationDB.Client;

	internal sealed class FdbDatabaseScopeProvider : IFdbDatabaseScopeProvider
	{

		public FdbDatabaseScopeProvider(IFdbDatabaseProvider provider, Func<IFdbDatabase, CancellationToken, Task> handler, CancellationTokenSource lifetime)
		{
			this.Provider = provider;
			this.Handler = handler;
			this.Lifetime = lifetime;
			this.DbTask = new Lazy<ValueTask<IFdbDatabase>>(this.InitAsync, LazyThreadSafetyMode.ExecutionAndPublication);
		}

		public IFdbDatabaseProvider Provider { get; }

		public Func<IFdbDatabase, CancellationToken, Task> Handler { get; }

		private readonly Lazy<ValueTask<IFdbDatabase>> DbTask;

		private CancellationTokenSource Lifetime { get; }

		private async ValueTask<IFdbDatabase> InitAsync()
		{
			var ct = this.Lifetime.Token;
			var db = await this.Provider.GetDatabase(ct);
			ct.ThrowIfCancellationRequested();
			await this.Handler(db, ct);
			return db;
		}

		public ValueTask<IFdbDatabase> GetDatabase(CancellationToken ct = default)
		{
			return this.DbTask.Value;
		}

		public void Dispose()
		{
			this.Lifetime?.Cancel();
		}
	}
}
