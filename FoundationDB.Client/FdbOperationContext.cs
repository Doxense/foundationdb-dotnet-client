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
	using FoundationDB.Async;
	using FoundationDB.Client.Utils;
	using System;
	using System.Diagnostics;
	using System.Threading;
	using System.Threading.Tasks;

	/// <summary>
	/// Represents the context of a retryable transactional function wich accept a read-only or read-write transaction.
	/// </summary>
	[DebuggerDisplay("Retries={Retries}, Committed={Committed}, Elapsed={Duration.Elapsed}")]
	public sealed class FdbOperationContext : IDisposable
	{
		/// <summary>The database used by the operation</summary>
		public FdbDatabase Db { get; private set; }

		/// <summary>If true, attempts to commit read-only transactions anyway.</summary>
		public bool CommitReadOnlyTransactions { get; set; }

		/// <summary>Result of the operation (or null)</summary>
		public object Result { get; set; }

		/// <summary>Cancellation token associated with the operation</summary>
		public CancellationToken Token { get; internal set; }

		/// <summary>If set to true, will abort and not commit the transaction. If false, will try to commit the transaction (and retry on failure)</summary>
		public bool Abort { get; set; }

		/// <summary>Current attempt number (0 for first, 1+ for retries)</summary>
		public int Retries { get; private set; }

		/// <summary>Date at wich the operation was first started</summary>
		public DateTime StartedUtc { get; private set; }

		/// <summary>Time spent since the start of the first attempt</summary>
		public Stopwatch Duration { get; private set; }

		/// <summary>If true, the transactino has been committed successfully</summary>
		public bool Committed { get; private set; }

		internal bool Shared { get; private set; }

		internal CancellationTokenSource TokenSource { get; private set; }

		internal FdbOperationContext(FdbDatabase db, CancellationToken ct)
		{
			Contract.Requires(db != null);

			this.Db = db;
			this.Duration = new Stopwatch();

			// by default, we hook ourselves on the db's CancellationToken
			var token = db.Token;
			if (ct.CanBeCanceled && ct != token)
			{
				this.TokenSource = CancellationTokenSource.CreateLinkedTokenSource(ct);
				token = this.TokenSource.Token;
			}
			this.Token = token;
		}

		internal async Task ExecuteInternal(Delegate handler)
		{
			Contract.Requires(handler != null);

			if (this.Abort) throw new InvalidOperationException("Operation context has already been aborted or disposed");

			try
			{
				this.Shared = true;
				this.Committed = false;
				this.Retries = 0;
				this.StartedUtc = DateTime.UtcNow;
				this.Duration.Start();

				using (var trans = this.Db.BeginTransaction())
				{
					while (!this.Committed && !this.Token.IsCancellationRequested)
					{
						FdbException e = null;
						bool readOnlyOperation = false;
						try
						{
							// call the user provided lambda
							if (handler is Func<IFdbTransaction, Task>)
							{
								await ((Func<IFdbTransaction, Task>)handler)(trans).ConfigureAwait(false);
							}
							else if (handler is Action<IFdbTransaction>)
							{
								((Action<IFdbTransaction>)handler)(trans);
							}
							else if (handler is Func<IFdbReadTransaction, Task>)
							{
								readOnlyOperation = true;
								await ((Func<IFdbTransaction, Task>)handler)(trans).ConfigureAwait(false);
							}
							else
							{
								throw new NotSupportedException(String.Format("Cannot execute delegates of type {0}", handler.GetType().Name));
							}

							if (this.Abort)
							{
								break;
							}

							if (!readOnlyOperation || this.CommitReadOnlyTransactions)
							{ // commit the transaction
								await trans.CommitAsync().ConfigureAwait(false);
							}

							// we are done
							this.Committed = true;
						}
						catch (FdbException x)
						{
							e = x;
						}

						if (e != null)
						{
							await trans.OnErrorAsync(e.Code).ConfigureAwait(false);
							if (Logging.On && Logging.IsVerbose) Logging.Verbose(String.Format("fdb: transaction {0} can be safely retried", trans.Id.ToString()));
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
				this.Dispose();
			}
		}

		public void Dispose()
		{
			this.Abort = true;
			if (this.TokenSource != null)
			{
				this.TokenSource.SafeCancelAndDispose();
			}
		}

		#region Read-Only operations...

		/// <summary>Run a read-only operation until it suceeds, timeouts, or fail with non-retryable error</summary>
		public static Task RunReadAsync(FdbDatabase db, Func<IFdbReadTransaction, Task> asyncAction, CancellationToken ct = default(CancellationToken))
		{
			return new FdbOperationContext(db, ct).ExecuteInternal(asyncAction);
		}

		/// <summary>Run a read-only operation until it suceeds, timeouts, or fail with non-retryable error</summary>
		public static async Task<R> RunReadWithResultAsync<R>(FdbDatabase db, Func<IFdbReadTransaction, Task<R>> asyncAction, CancellationToken ct = default(CancellationToken))
		{
			R result = default(R);
			Func<IFdbTransaction, Task> handler = async (tr) =>
			{
				result = await asyncAction(tr).ConfigureAwait(false);
			};

			var context = new FdbOperationContext(db, ct);
			await context.ExecuteInternal(handler).ConfigureAwait(false);
			return result;
		}

		#endregion

		#region Read/Write operations...

		/// <summary>Run a read/write operation until it suceeds, timeouts, or fail with non-retryable error</summary>
		public static Task RunWriteAsync(FdbDatabase db, Func<IFdbTransaction, Task> asyncAction, CancellationToken ct = default(CancellationToken))
		{
			return new FdbOperationContext(db, ct).ExecuteInternal(asyncAction);
		}

		/// <summary>Run a write operation until it suceeds, timeouts, or fail with non-retryable error</summary>
		public static Task RunWriteAsync(FdbDatabase db, Action<IFdbTransaction> action, CancellationToken ct = default(CancellationToken))
		{
			return new FdbOperationContext(db, ct).ExecuteInternal(action);
		}

		/// <summary>Run a read/write operation until it suceeds, timeouts, or fail with non-retryable error</summary>
		public static async Task<R> RunWriteWithResultAsync<R>(FdbDatabase db, Func<IFdbTransaction, Task<R>> asyncAction, CancellationToken ct = default(CancellationToken))
		{
			R result = default(R);
			Func<IFdbTransaction, Task> handler = async (tr) =>
			{
				result = await asyncAction(tr).ConfigureAwait(false);
			};

			var context = new FdbOperationContext(db, ct);
			await context.ExecuteInternal(handler).ConfigureAwait(false);
			return result;
		}

		#endregion

	}

}
