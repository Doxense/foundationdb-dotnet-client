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
	using Microsoft.Extensions.Options;

	internal sealed class FdbDatabaseProvider : IFdbDatabaseProvider
	{

		private IFdbDatabase Db { get; set; }

		public bool IsAvailable { get; private set; }

		public FdbDatabaseProviderOptions Options { get; }

		private TaskCompletionSource<IFdbDatabase> InitTask;

		[NotNull]
		private Task<IFdbDatabase> DbTask;

		[NotNull]
		private CancellationTokenSource LifeTime { get; } = new CancellationTokenSource();

		private Exception Error { get; set;}

		[Pure, NotNull]
		public static IFdbDatabaseProvider CreateProvider([NotNull] FdbDatabaseProviderOptions options)
		{
			Contract.NotNull(options, nameof(options));
			return new FdbDatabaseProvider(Microsoft.Extensions.Options.Options.Create(options));
		}

		public FdbDatabaseProvider(IOptions<FdbDatabaseProviderOptions> optionsAccessor)
		{
			this.Options = optionsAccessor.Value;
			this.DbTask = Task.FromException<IFdbDatabase>(new InvalidOperationException("The database has not been initialized."));
		}

		public void Start()
		{
			var tcs = new TaskCompletionSource<IFdbDatabase>();
			this.DbTask = tcs.Task;
			this.InitTask = tcs;
			_ = Task.Run(async () =>
			{
				Fdb.Start(this.Options.ApiVersion);
				try
				{
					var db = await Fdb.OpenAsync(this.Options.ConnectionOptions, this.LifeTime.Token).ConfigureAwait(false);
					SetDatabase(db, null);
				}
				catch (Exception e)
				{
					SetDatabase(null, e); //TODO: garder l'erreur!
				}

			}, this.LifeTime.Token);
		}

		public void Stop()
		{
			this.LifeTime?.Cancel();
			Interlocked.Exchange(ref this.InitTask, null)?.TrySetCanceled();
			SetDatabase(null, null);
		}

		public void SetDatabase(IFdbDatabase db, Exception e)
		{
			this.Db = db;
			this.Error = e;
			var tcs = Interlocked.Exchange(ref this.InitTask, null);
			if (db == null)
			{
				if (e != null)
				{
					tcs?.TrySetException(e);
					this.DbTask = Task.FromException<IFdbDatabase>(e);
				} 
				else
				{
					tcs?.TrySetCanceled();
					this.DbTask = Task.FromException<IFdbDatabase>(new InvalidOperationException("There is no database available at this time."));
				}
				this.IsAvailable = false;
			}
			else
			{
				tcs?.SetResult(db);
				this.DbTask = Task.FromResult(db);
				this.IsAvailable = true;
			}
		}

		public void Dispose()
		{
			this.IsAvailable = false;
			this.Db?.Dispose();
			this.Error = new ObjectDisposedException("Database has been shut down.");
			this.DbTask = Task.FromException<IFdbDatabase>(this.Error);
			Interlocked.Exchange(ref this.InitTask, null)?.TrySetCanceled();
		}

		IFdbDatabaseScopeProvider IFdbDatabaseScopeProvider.Parent => null;

		public ValueTask<IFdbDatabase> GetDatabase(CancellationToken ct = default)
		{
			var db = this.Db;
			return db != null ? new ValueTask<IFdbDatabase>(db) : GetDatabaseRare(ct);
		}

		private async ValueTask<IFdbDatabase> GetDatabaseRare(CancellationToken ct)
		{
			// make sure we ARE started
			if (this.InitTask == null && this.Options.AutoStart)
			{
				lock (this)
				{
					if (this.InitTask == null)
					{
						Start();
					}
				}
			}

			var t = this.DbTask;
			if (!t.IsCompleted && ct.CanBeCanceled)
			{
				//REVIEW: is there a faster way to do this? (we want to make sure not to leak tons of Task.Delay(...) that will linger on until the original ct is triggered
				using (var cts = CancellationTokenSource.CreateLinkedTokenSource(ct))
				{
					await Task.WhenAny(t, Task.Delay(Timeout.InfiniteTimeSpan, cts.Token)).ConfigureAwait(false);
					ct.ThrowIfCancellationRequested();
				}
			}
			return await this.DbTask.ConfigureAwait(false);
		}

		public IFdbDatabaseScopeProvider CreateScope(Func<IFdbDatabase, CancellationToken, Task<IFdbDatabase>> start)
		{
			return new FdbDatabaseScopeProvider(this, start, this.LifeTime);
		}

	}
}
