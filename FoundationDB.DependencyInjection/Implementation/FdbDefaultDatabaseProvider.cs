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
	using Microsoft.Extensions.Options;

	internal sealed class FdbDefaultDatabaseProvider : IFdbDatabaseProvider
	{

		private IFdbDatabase Db { get; set; }

		public bool IsAvailable { get; private set; }

		public FdbDatabaseProviderOptions Options { get; }

		private TaskCompletionSource<IFdbDatabase> InitTask;

		private ValueTask<IFdbDatabase> DbTask;

		private CancellationTokenSource LifeTime { get; set; }

		private Exception Error { get; set;}

		public FdbDefaultDatabaseProvider(IOptions<FdbDatabaseProviderOptions> optionsAccessor)
		{
			this.Options = optionsAccessor.Value;
			this.DbTask = new ValueTask<IFdbDatabase>(Task.FromException<IFdbDatabase>(new InvalidOperationException("The database has not been initialized.")));
		}

		public void Start(CancellationToken ct)
		{
			var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
			this.LifeTime = cts;
			_ = Task.Run(async () =>
			{
				var tcs = new TaskCompletionSource<IFdbDatabase>();
				this.InitTask = new TaskCompletionSource<IFdbDatabase>();
				this.DbTask = new ValueTask<IFdbDatabase>(tcs.Task);

				try
				{
					var db = await Fdb.OpenAsync(this.Options.ConnectionOptions, cts.Token).ConfigureAwait(false);
					SetDatabase(db, null);
				}
				catch (Exception e)
				{
					SetDatabase(null, e); //TODO: garder l'erreur!
				}

			}, cts.Token);
		}

		public void Stop()
		{
			this.LifeTime?.Cancel();
			Interlocked.Exchange(ref InitTask, null)?.TrySetCanceled();
			SetDatabase(null, null);
		}

		public void SetDatabase(IFdbDatabase db, Exception e)
		{
			this.Db = db;
			this.Error = e;
			var tcs = Interlocked.Exchange(ref InitTask, null);
			if (db == null)
			{
				if (e != null)
				{
					tcs?.TrySetException(e);
					this.DbTask = new ValueTask<IFdbDatabase>(Task.FromException<IFdbDatabase>(e));
				} 
				else
				{
					tcs?.TrySetCanceled();
					this.DbTask = new ValueTask<IFdbDatabase>(Task.FromException<IFdbDatabase>(new InvalidOperationException("There is not database available at this time.")));
				}
				this.IsAvailable = false;
			}
			else
			{
				tcs?.SetResult(db);
				DbTask = new ValueTask<IFdbDatabase>(db);
				this.IsAvailable = true;
			}
		}

		public void Dispose()
		{
			this.IsAvailable = false;
			this.Db?.Dispose();
			this.Error = new ObjectDisposedException("Database has shut down");
			this.DbTask = new ValueTask<IFdbDatabase>(Task.FromException<IFdbDatabase>(this.Error));
			Interlocked.Exchange(ref InitTask, null)?.TrySetCanceled();
		}

		public ValueTask<IFdbDatabase> GetDatabase(CancellationToken ct = default)
		{
			return this.DbTask;
		}

		public IFdbDatabaseScopeProvider CreateScope(Func<IFdbDatabase, CancellationToken, Task> start)
		{
			return new FdbDatabaseScopeProvider(this, start, this.LifeTime);
		}

	}
}
