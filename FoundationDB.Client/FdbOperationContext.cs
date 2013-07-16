#region BSD Licence
/* Copyright (c) 2013, Doxense SARL
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

namespace FoundationDB.Client
{
	using FoundationDB.Client.Utils;
	using System;
	using System.Diagnostics;
	using System.Threading;
	using System.Threading.Tasks;

	[DebuggerDisplay("Retries={Retries}, Committed={Committed}, Elapsed={Duration.Elapsed}")]
	public sealed class FdbOperationContext
	{
		/// <summary>The database used by the operation</summary>
		public FdbDatabase Db { get; private set; }

		/// <summary>If true, pass a Snapshot transaction on read operations</summary>
		public bool Snapshot { get; private set; }

		/// <summary>The state of the operation (or null)</summary>
		public object State { get; set; }

		/// <summary>Result of the operation (or null)</summary>
		public object Result { get; set; }

		/// <summary>Cancellation token associated with the operation</summary>
		public CancellationToken Token { get; private set; }

		/// <summary>If set to true, will abort and not commit the transaction. If false, will try to commit the transaction (and retry on failure)</summary>
		public bool Abort { get; set; }

		/// <summary>Current attempt number (0 for first, 1+ for retries)</summary>
		public int Retries { get; private set; }

		/// <summary>Date at wich the operation was first started</summary>
		public DateTime StartedUtc { get; private set; }

		/// <summary>Time spent since the start of the first attempt</summary>
		public Stopwatch Duration { get; private set; }

		public bool Committed { get; private set; }

		internal FdbOperationContext(FdbDatabase db, object state, bool snapshot)
		{
			Contract.Requires(db != null);

			this.Db = db;
			this.State = state;
			this.Snapshot = snapshot;
			this.Duration = new Stopwatch();
		}

		/// <summary>Run the operation until it suceeds, timeouts, or fail with non-retryable error</summary>
		public Task Execute(Func<IFdbTransaction, FdbOperationContext, Task> asyncAction, CancellationToken ct)
		{
			Contract.Requires(this.Snapshot == false); // not in snapshot mode!
			if (asyncAction == null) throw new ArgumentNullException("asyncAction");
			return ExecuteInternal(asyncAction, null, ct);
		}

		public Task Execute(Func<IFdbReadTransaction, FdbOperationContext, Task> asyncAction, CancellationToken ct)
		{
			if (asyncAction == null) throw new ArgumentNullException("asyncAction");
			return ExecuteInternal(null, asyncAction, ct);
		}

		internal async Task ExecuteInternal(Func<IFdbTransaction, FdbOperationContext, Task> asyncWritableAction, Func<IFdbReadTransaction, FdbOperationContext, Task> asyncReadableAction, CancellationToken ct)
		{
			Contract.Requires((asyncWritableAction != null) ^ (asyncReadableAction != null));

			try
			{
				this.Committed = false;
				this.Token = ct;
				this.Retries = 0;
				this.StartedUtc = DateTime.UtcNow;
				this.Duration.Start();

				while (!this.Committed && !this.Token.IsCancellationRequested)
				{
					using (var trans = this.Db.BeginTransaction())
					{
						FdbException e = null;
						try
						{
							// call the user provided lambda
							if (asyncWritableAction != null)
							{
								await asyncWritableAction(trans, this).ConfigureAwait(false);
							}
							else
							{
								await asyncReadableAction(this.Snapshot ? trans.Snapshot : trans, this).ConfigureAwait(false);
							}

							if (this.Abort)
							{
								break;
							}
							// commit the transaction
							await trans.CommitAsync(this.Token).ConfigureAwait(false);

							// we are done
							this.Committed = true;
						}
						catch (FdbException x)
						{
							x = e;
						}

						if (e != null)
						{
							await trans.OnErrorAsync(e.Code).ConfigureAwait(false);
						}

						if (this.Duration.Elapsed.TotalSeconds >= 1)
						{
							if (Logging.On) Logging.Info(String.Format("fdb WARNING: long transaction ({0} sec elapsed in transaction lambda function ({1} retries, {2})", this.Duration.Elapsed.TotalSeconds.ToString("N1"), this.Retries.ToString(), this.Committed ? "committed" : "not yet committed"));
						}

						this.Retries++;
					}
				}
				this.Token.ThrowIfCancellationRequested();

				if (this.Abort)
				{
					throw new OperationCanceledException(this.Token);
				}

			}
			finally
			{
				this.Duration.Stop();
				this.Token = CancellationToken.None;
			}
		}

		/// <summary>[EXPERIMENTAL] Retry an action in case of merge or temporary database failure</summary>
		/// <param name="db">Database that will be the source of the transactions</param>
		/// <param name="asyncAction">Async lambda to perform under a new transaction, that receives the transaction as the first parameter, and the context as the second parameter. It should throw an OperationCanceledException if it decides to not retry the action</param>
		/// <param name="state">Optional state that is passed to the lambda view the context</param>
		/// <param name="ct">Optionnal cancellation token, that will be passed to the async action as the third parameter</param>
		/// <returns>Task that completes when we have successfully completed the action, or fails if a non retryable error occurs</returns>
		public static Task RunWriteAsync(FdbDatabase db, Func<IFdbTransaction, FdbOperationContext, Task> asyncAction, object state, CancellationToken ct = default(CancellationToken))
		{
			// this is the equivalent of the "transactionnal" decorator in Python, and maybe also the "Run" method in Java
			//TODO: add 'maxAttempts' or 'maxDuration' optional parameters ?

			var context = new FdbOperationContext(db, state, false);
			return context.Execute(asyncAction, ct);
		}

		/// <summary>[EXPERIMENTAL] Retry an action in case of merge or temporary database failure</summary>
		/// <param name="db">Database that will be the source of the transactions</param>
		/// <param name="asyncAction">Async lambda to perform under a new transaction, that receives the transaction as the first parameter, and the context as the second parameter. It should throw an OperationCanceledException if it decides to not retry the action</param>
		/// <param name="state">Optional state that is passed to the lambda view the context</param>
		/// <param name="ct">Optionnal cancellation token, that will be passed to the async action as the third parameter</param>
		/// <returns>Task that completes when we have successfully completed the action, or fails if a non retryable error occurs</returns>
		public static Task RunReadAsync(FdbDatabase db, bool snapshot, Func<IFdbReadTransaction, FdbOperationContext, Task> asyncAction, object state, CancellationToken ct = default(CancellationToken))
		{
			// this is the equivalent of the "transactionnal" decorator in Python, and maybe also the "Run" method in Java
			//TODO: add 'maxAttempts' or 'maxDuration' optional parameters ?

			var context = new FdbOperationContext(db, state, snapshot);
			return context.Execute(asyncAction, ct);
		}

		/// <summary>[EXPERIMENTAL] Retry an action in case of merge or temporary database failure</summary>
		/// <typeparam name="TResult">Type of the result returned by the action</typeparam>
		/// <param name="db">Database that will be the source of the transactions</param>
		/// <param name="asyncAction">Async lambda to perform under a new transaction, that receives the transaction as the first parameter, and the context as the second parameter. It should throw an OperationCanceledException if it decides to not retry the action</param>
		/// <param name="state">Optional state that is passed to the lambda view the context</param>
		/// <param name="ct">Optionnal cancellation token, that will be passed to the async action as the third parameter</param>
		/// <returns>Task that returns a result when we have successfully completed the action, or fails if a non retryable error occurs</returns>
		public static async Task<R> RunWriteAsync<R>(FdbDatabase db, Func<IFdbTransaction, FdbOperationContext, Task> asyncAction, object state, CancellationToken ct = default(CancellationToken))
		{
			// this is the equivalent of the "transactionnal" decorator in Python, and maybe also the "Run" method in Java
			//TODO: add 'maxAttempts' or 'maxDuration' optional parameters ?

			var context = new FdbOperationContext(db, state, false);
			await context.Execute(asyncAction, ct).ConfigureAwait(false);
			return (R) context.Result;
		}

		/// <summary>[EXPERIMENTAL] Retry an action in case of merge or temporary database failure</summary>
		/// <typeparam name="TResult">Type of the result returned by the action</typeparam>
		/// <param name="db">Database that will be the source of the transactions</param>
		/// <param name="asyncAction">Async lambda to perform under a new transaction, that receives the transaction as the first parameter, and the context as the second parameter. It should throw an OperationCanceledException if it decides to not retry the action</param>
		/// <param name="state">Optional state that is passed to the lambda view the context</param>
		/// <param name="ct">Optionnal cancellation token, that will be passed to the async action as the third parameter</param>
		/// <returns>Task that returns a result when we have successfully completed the action, or fails if a non retryable error occurs</returns>
		public static async Task<R> RunReadAsync<R>(FdbDatabase db, bool snapshot, Func<IFdbReadTransaction, FdbOperationContext, Task> asyncAction, object state, CancellationToken ct = default(CancellationToken))
		{
			// this is the equivalent of the "transactionnal" decorator in Python, and maybe also the "Run" method in Java
			//TODO: add 'maxAttempts' or 'maxDuration' optional parameters ?

			var context = new FdbOperationContext(db, state, snapshot);
			await context.Execute(asyncAction, ct).ConfigureAwait(false);
			return (R)context.Result;
		}

	}

}
