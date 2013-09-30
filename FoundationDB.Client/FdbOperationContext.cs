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
		public IFdbDatabase Db { get; private set; }

		/// <summary>If true, attempts to commit read-only transactions anyway.</summary>
		public bool CommitReadOnlyTransactions { get; internal set; }

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

		/// <summary>If true, the transaction has been committed successfully</summary>
		public bool Committed { get; private set; }

		/// <summary>If true, the lifetime of the context is handled by an external retry loop. If false, the context is linked to the lifetime of the transaction instance.</summary>
		internal bool Shared { get; private set; }

		/// <summary>Internal source of cancellation, able to abort any pending IO operations attached to this transaction</summary>
		internal CancellationTokenSource TokenSource { get; private set; }

		internal FdbOperationContext(IFdbDatabase db, CancellationToken cancellationToken)
		{
			Contract.Requires(db != null);

			this.Db = db;
			this.Duration = new Stopwatch();

			// by default, we hook ourselves on the db's CancellationToken
			var token = db.Token;
			if (cancellationToken.CanBeCanceled && cancellationToken != token)
			{
				this.TokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
				token = this.TokenSource.Token;
			}
			this.Token = token;
		}

		internal async Task ExecuteInternal(Delegate handler, Delegate onDone)
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

				//TODO: force calling BeginReadOnlyTransaction(...) for read-only operations ?
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
							else if (handler is Func<IFdbReadOnlyTransaction, Task>)
							{
								readOnlyOperation = true;
								await ((Func<IFdbReadOnlyTransaction, Task>)handler)(trans).ConfigureAwait(false);
							}
							else
							{
								throw new NotSupportedException(String.Format("Cannot execute handler of type {0}", handler.GetType().Name));
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

							if (onDone != null)
							{
								if (onDone is Action<IFdbReadOnlyTransaction>)
								{
									((Action<IFdbReadOnlyTransaction>)onDone)(trans);
								}
								else if (onDone is Action<IFdbTransaction>)
								{
									((Action<IFdbTransaction>)onDone)(trans);
								}
								else if (onDone is Func<IFdbReadOnlyTransaction, Task>)
								{
									await ((Func<IFdbReadOnlyTransaction, Task>)onDone)(trans).ConfigureAwait(false);
								}
								else if (onDone is Func<IFdbTransaction, Task>)
								{
									await ((Func<IFdbTransaction, Task>)onDone)(trans).ConfigureAwait(false);
								}
								else
								{
									throw new NotSupportedException(String.Format("Cannot execute completion handler of type {0}", handler.GetType().Name));
								}
							}
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
		public static Task RunReadAsync(FdbDatabase db, Func<IFdbReadOnlyTransaction, Task> asyncAction, Action<IFdbReadOnlyTransaction> onDone, CancellationToken cancellationToken)
		{
			return new FdbOperationContext(db, cancellationToken).ExecuteInternal(asyncAction, onDone);
		}

		/// <summary>Run a read-only operation until it suceeds, timeouts, or fail with non-retryable error</summary>
		public static async Task<R> RunReadWithResultAsync<R>(FdbDatabase db, Func<IFdbReadOnlyTransaction, Task<R>> asyncAction, Action<IFdbReadOnlyTransaction> onDone, CancellationToken cancellationToken)
		{
			R result = default(R);
			Func<IFdbTransaction, Task> handler = async (tr) =>
			{
				result = await asyncAction(tr).ConfigureAwait(false);
			};

			var context = new FdbOperationContext(db, cancellationToken);
			await context.ExecuteInternal(handler, onDone).ConfigureAwait(false);
			return result;
		}

		#endregion

		#region Read/Write operations...

		/// <summary>Run a read/write operation until it suceeds, timeouts, or fail with non-retryable error</summary>
		public static Task RunWriteAsync(FdbDatabase db, Func<IFdbTransaction, Task> asyncAction, Action<IFdbTransaction> onDone, CancellationToken cancellationToken)
		{
			return new FdbOperationContext(db, cancellationToken).ExecuteInternal(asyncAction, onDone);
		}

		/// <summary>Run a write operation until it suceeds, timeouts, or fail with non-retryable error</summary>
		public static Task RunWriteAsync(FdbDatabase db, Action<IFdbTransaction> action, Action<IFdbTransaction> onDone, CancellationToken cancellationToken)
		{
			return new FdbOperationContext(db, cancellationToken).ExecuteInternal(action, onDone);
		}

		/// <summary>Run a read/write operation until it suceeds, timeouts, or fail with non-retryable error</summary>
		public static async Task<R> RunWriteWithResultAsync<R>(FdbDatabase db, Func<IFdbTransaction, Task<R>> asyncAction, Action<IFdbTransaction> onDone, CancellationToken cancellationToken)
		{
			R result = default(R);
			Func<IFdbTransaction, Task> handler = async (tr) =>
			{
				result = await asyncAction(tr).ConfigureAwait(false);
			};

			var context = new FdbOperationContext(db, cancellationToken);
			await context.ExecuteInternal(handler, onDone).ConfigureAwait(false);
			return result;
		}

		#endregion

	}

}
