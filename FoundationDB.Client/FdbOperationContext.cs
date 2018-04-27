#region BSD Licence
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

namespace FoundationDB.Client
{
	using System;
	using System.Diagnostics;
	using System.Globalization;
	using System.Threading;
	using System.Threading.Tasks;
	using Doxense.Diagnostics.Contracts;
	using Doxense.Threading.Tasks;
	using JetBrains.Annotations;

	/// <summary>
	/// Represents the context of a retryable transactional function which accepts a read-only or read-write transaction.
	/// </summary>
	[DebuggerDisplay("Retries={Retries}, Committed={Committed}, Elapsed={Elapsed}")]
	public sealed class FdbOperationContext : IDisposable
	{
		//REVIEW: maybe we should find a way to reduce the size of this class? (it's already almost at 100 bytes !)

		/// <summary>The database used by the operation</summary>
		[NotNull]
		public IFdbDatabase Database { get; }

		/// <summary>Result of the operation (or null)</summary>
		public object Result { get; set; }
		//REVIEW: should we force using a "SetResult()/TrySetResult()" method for this ?

		/// <summary>Cancellation token associated with the operation</summary>
		public CancellationToken Cancellation { get; }

		/// <summary>If set to true, will abort and not commit the transaction. If false, will try to commit the transaction (and retry on failure)</summary>
		public bool Abort { get; set; }

		/// <summary>Current attempt number (0 for first, 1+ for retries)</summary>
		public int Retries { get; private set; }

		/// <summary>Date at wich the operation was first started</summary>
		public DateTime StartedUtc { get; private set; }

		/// <summary>Stopwatch that is started at the creation of the transaction, and stopped when it commits or gets disposed</summary>
		[NotNull]
		internal Stopwatch Clock { get; }

		/// <summary>Duration of all the previous attemps before the current one (starts at 0, and gets updated at each reset/retry)</summary>
		internal TimeSpan BaseDuration { get; private set; }

		/// <summary>Time elapsed since the start of the first attempt</summary>
		public TimeSpan ElapsedTotal => this.Clock.Elapsed;

		/// <summary>Time elapsed since the start of the current attempt</summary>
		/// <remarks>This value is reset to zero every time the transation fails and is retried.
		/// Note that this may not represent the actual lifetime of the transaction with the database itself, which starts at the first read operation.</remarks>
		public TimeSpan Elapsed => this.Clock.Elapsed.Subtract(this.BaseDuration);

		/// <summary>If true, the transaction has been committed successfully</summary>
		public bool Committed { get; private set; }

		/// <summary>If true, the lifetime of the context is handled by an external retry loop. If false, the context is linked to the lifetime of the transaction instance.</summary>
		internal bool Shared => (this.Mode & FdbTransactionMode.InsideRetryLoop) != 0;

		/// <summary>Mode of the transaction</summary>
		public FdbTransactionMode Mode { get; }

		/// <summary>Internal source of cancellation, able to abort any pending IO operations attached to this transaction</summary>
		[CanBeNull]
		internal CancellationTokenSource TokenSource { get; }

		/// <summary>Create a new retry loop operation context</summary>
		/// <param name="db">Database that will be used by the retry loop</param>
		/// <param name="mode">Operation mode of the retry loop</param>
		/// <param name="ct">Optional cancellation token that will abort the retry loop if triggered.</param>
		public FdbOperationContext([NotNull] IFdbDatabase db, FdbTransactionMode mode, CancellationToken ct)
		{
			if (db == null) throw new ArgumentNullException(nameof(db));

			this.Database = db;
			this.Mode = mode;
			this.Clock = new Stopwatch();
			// note: we don't start the clock yet, only when the context starts executing...

			// by default, we hook ourselves to the db's CancellationToken, but we may need to also
			// hook with a different, caller-provided, token and respond to cancellation from both sites.
			var token = db.Cancellation;
			if (ct.CanBeCanceled && !ct.Equals(token))
			{
				this.TokenSource = CancellationTokenSource.CreateLinkedTokenSource(token, ct);
				token = this.TokenSource.Token;
			}
			this.Cancellation = token;
		}

		/// <summary>Execute a retry loop on this context</summary>
		internal static async Task ExecuteInternal([NotNull] IFdbDatabase db, [NotNull] FdbOperationContext context, [NotNull] Delegate handler, Delegate onDone)
		{
			Contract.Requires(db != null && context != null && handler != null);
			Contract.Requires(context.Shared);

			if (context.Abort) throw new InvalidOperationException("Operation context has already been aborted or disposed");

			try
			{
				// make sure to reset everything (in case a context is reused multiple times)
				context.Committed = false;
				context.Retries = 0;
				context.BaseDuration = TimeSpan.Zero;
				context.StartedUtc = DateTime.UtcNow;
				context.Clock.Start();
				//note: we start the clock immediately, but the transaction's 5 seconde max lifetime is actually measured from the first read operation (Get, GetRange, GetReadVersion, etc...)
				// => algorithms that monitor the elapsed duration to rate limit themselves may think that the trans is older than it really is...
				// => we would need to plug into the transaction handler itself to be notified when exactly a read op starts...

				using (var trans = db.BeginTransaction(context.Mode, CancellationToken.None, context))
				{
					while (!context.Committed && !context.Cancellation.IsCancellationRequested)
					{
						FdbException e = null;
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
								await ((Func<IFdbReadOnlyTransaction, Task>)handler)(trans).ConfigureAwait(false);
							}
							else
							{
								throw new NotSupportedException(String.Format("Cannot execute handlers of type {0}", handler.GetType().Name));
							}

							if (context.Abort)
							{
								break;
							}

							if (!trans.IsReadOnly)
							{ // commit the transaction
								await trans.CommitAsync().ConfigureAwait(false);
							}

							// we are done
							context.Committed = true;

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
							//TODO: will be able to await in catch block in C# 6 !
							e = x;
						}

						if (e != null)
						{
							if (Logging.On && Logging.IsVerbose) Logging.Verbose(String.Format(CultureInfo.InvariantCulture, "fdb: transaction {0} failed with error code {1}", trans.Id, e.Code));
							await trans.OnErrorAsync(e.Code).ConfigureAwait(false);
							if (Logging.On && Logging.IsVerbose) Logging.Verbose(String.Format(CultureInfo.InvariantCulture, "fdb: transaction {0} can be safely retried", trans.Id));
						}

						// update the base time for the next attempt
						context.BaseDuration = context.ElapsedTotal;
						if (context.BaseDuration.TotalSeconds >= 10)
						{
							//REVIEW: this may not be a goot idea to spam the logs with long running transactions??
							if (Logging.On) Logging.Info(String.Format(CultureInfo.InvariantCulture, "fdb WARNING: long transaction ({0:N1} sec elapsed in transaction lambda function ({1} retries, {2})", context.BaseDuration.TotalSeconds, context.Retries, context.Committed ? "committed" : "not yet committed"));
						}

						context.Retries++;
					}
				}
				context.Cancellation.ThrowIfCancellationRequested();

				if (context.Abort)
				{
					throw new OperationCanceledException(context.Cancellation);
				}

			}
			finally
			{
				context.Clock.Stop();
				context.Dispose();
			}
		}

		public void Dispose()
		{
			this.Abort = true;
			this.TokenSource?.SafeCancelAndDispose();
		}

		#region Read-Only operations...

		/// <summary>Run a read-only operation until it suceeds, timeouts, or fail with non-retryable error</summary>
		public static Task RunReadAsync([NotNull] IFdbDatabase db, [NotNull] Func<IFdbReadOnlyTransaction, Task> asyncHandler, Action<IFdbReadOnlyTransaction> onDone, CancellationToken ct)
		{
			if (db == null) throw new ArgumentNullException(nameof(db));
			if (asyncHandler == null) throw new ArgumentNullException(nameof(asyncHandler));
			if (ct.IsCancellationRequested) return Task.FromCanceled(ct);

			var context = new FdbOperationContext(db, FdbTransactionMode.ReadOnly | FdbTransactionMode.InsideRetryLoop, ct);
			return ExecuteInternal(db, context, asyncHandler, onDone);
		}

		/// <summary>Run a read-only operation until it suceeds, timeouts, or fail with non-retryable error</summary>
		public static async Task<R> RunReadWithResultAsync<R>([NotNull] IFdbDatabase db, [NotNull] Func<IFdbReadOnlyTransaction, Task<R>> asyncHandler, Action<IFdbReadOnlyTransaction> onDone, CancellationToken ct)
		{
			if (db == null) throw new ArgumentNullException(nameof(db));
			if (asyncHandler == null) throw new ArgumentNullException(nameof(asyncHandler));
			ct.ThrowIfCancellationRequested();

			R result = default(R);
			Func<IFdbTransaction, Task> handler = async (tr) =>
			{
				result = await asyncHandler(tr).ConfigureAwait(false);
			};

			var context = new FdbOperationContext(db, FdbTransactionMode.ReadOnly | FdbTransactionMode.InsideRetryLoop, ct);
			await ExecuteInternal(db, context, handler, onDone).ConfigureAwait(false);
			return result;
		}

		#endregion

		#region Read/Write operations...

		/// <summary>Run a read/write operation until it suceeds, timeouts, or fail with non-retryable error</summary>
		public static Task RunWriteAsync([NotNull] IFdbDatabase db, [NotNull] Func<IFdbTransaction, Task> asyncHandler, Action<IFdbTransaction> onDone, CancellationToken ct)
		{
			if (db == null) throw new ArgumentNullException(nameof(db));
			if (asyncHandler == null) throw new ArgumentNullException(nameof(asyncHandler));
			if (ct.IsCancellationRequested) return Task.FromCanceled(ct);

			var context = new FdbOperationContext(db, FdbTransactionMode.Default | FdbTransactionMode.InsideRetryLoop, ct);
			return ExecuteInternal(db, context, asyncHandler, onDone);
		}

		/// <summary>Run a write operation until it suceeds, timeouts, or fail with non-retryable error</summary>
		public static Task RunWriteAsync([NotNull] IFdbDatabase db, [NotNull] Action<IFdbTransaction> handler, Action<IFdbTransaction> onDone, CancellationToken ct)
		{
			if (db == null) throw new ArgumentNullException(nameof(db));
			if (handler == null) throw new ArgumentNullException(nameof(handler));
			if (ct.IsCancellationRequested) return Task.FromCanceled(ct);

			var context = new FdbOperationContext(db, FdbTransactionMode.Default | FdbTransactionMode.InsideRetryLoop, ct);
			return ExecuteInternal(db, context, handler, onDone);
		}

		/// <summary>Run a read/write operation until it suceeds, timeouts, or fail with non-retryable error</summary>
		public static async Task<R> RunWriteWithResultAsync<R>([NotNull] IFdbDatabase db, [NotNull] Func<IFdbTransaction, Task<R>> asyncHandler, Action<IFdbTransaction> onDone, CancellationToken ct)
		{
			if (db == null) throw new ArgumentNullException(nameof(db));
			if (asyncHandler == null) throw new ArgumentNullException(nameof(asyncHandler));
			ct.ThrowIfCancellationRequested();

			Func<IFdbTransaction, Task> handler = async (tr) =>
			{
				tr.Context.Result = await asyncHandler(tr).ConfigureAwait(false);
			};

			var context = new FdbOperationContext(db, FdbTransactionMode.Default | FdbTransactionMode.InsideRetryLoop, ct);
			await ExecuteInternal(db, context, handler, onDone).ConfigureAwait(false);
			return (R)context.Result;
		}

		#endregion

	}

}
