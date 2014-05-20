#region BSD Licence
/* Copyright (c) 2013-2014, Doxense SAS
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
	using JetBrains.Annotations;
	using System;
	using System.Diagnostics;
	using System.Globalization;
	using System.Threading;
	using System.Threading.Tasks;

	/// <summary>
	/// Represents the context of a retryable transactional function wich accept a read-only or read-write transaction.
	/// </summary>
	[DebuggerDisplay("Retries={Retries}, Committed={Committed}, Elapsed={Duration.Elapsed}")]
	public sealed class FdbOperationContext : IDisposable
	{
		/// <summary>The database used by the operation</summary>
		public IFdbDatabase Database { [NotNull] get; private set; }

		/// <summary>Result of the operation (or null)</summary>
		public object Result { get; set; }

		/// <summary>Cancellation token associated with the operation</summary>
		public CancellationToken Cancellation { get; internal set; }

		/// <summary>If set to true, will abort and not commit the transaction. If false, will try to commit the transaction (and retry on failure)</summary>
		public bool Abort { get; set; }

		/// <summary>Current attempt number (0 for first, 1+ for retries)</summary>
		public int Retries { get; private set; }

		/// <summary>Date at wich the operation was first started</summary>
		public DateTime StartedUtc { get; private set; }

		/// <summary>Time spent since the start of the first attempt</summary>
		public Stopwatch Duration { [NotNull] get; private set; }

		/// <summary>If true, the transaction has been committed successfully</summary>
		public bool Committed { get; private set; }

		/// <summary>If true, the lifetime of the context is handled by an external retry loop. If false, the context is linked to the lifetime of the transaction instance.</summary>
		internal bool Shared { get { return (this.Mode & FdbTransactionMode.InsideRetryLoop) != 0; } }

		/// <summary>Mode of the transaction</summary>
		public FdbTransactionMode Mode { get; private set; }

		/// <summary>Internal source of cancellation, able to abort any pending IO operations attached to this transaction</summary>
		internal CancellationTokenSource TokenSource { get; private set; }

		public FdbOperationContext([NotNull] IFdbDatabase db, FdbTransactionMode mode, CancellationToken cancellationToken)
		{
			if (db == null) throw new ArgumentNullException("db");

			this.Database = db;
			this.Mode = mode;
			this.Duration = new Stopwatch();

			// by default, we hook ourselves on the db's CancellationToken
			var token = db.Cancellation;
			if (cancellationToken.CanBeCanceled && cancellationToken != token)
			{
				this.TokenSource = CancellationTokenSource.CreateLinkedTokenSource(token, cancellationToken);
				token = this.TokenSource.Token;
			}
			this.Cancellation = token;
		}

		internal static async Task ExecuteInternal([NotNull] IFdbDatabase db, [NotNull] FdbOperationContext context, [NotNull] Delegate handler, Delegate onDone)
		{
			Contract.Requires(db != null && context != null && handler != null);
			Contract.Requires(context.Shared);

			if (context.Abort) throw new InvalidOperationException("Operation context has already been aborted or disposed");

			try
			{
				context.Committed = false;
				context.Retries = 0;
				context.StartedUtc = DateTime.UtcNow;
				context.Duration.Start();

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
							e = x;
						}

						if (e != null)
						{
							if (Logging.On && Logging.IsVerbose) Logging.Verbose(String.Format(CultureInfo.InvariantCulture, "fdb: transaction {0} failed with error code {1}", trans.Id, e.Code));
							await trans.OnErrorAsync(e.Code).ConfigureAwait(false);
							if (Logging.On && Logging.IsVerbose) Logging.Verbose(String.Format(CultureInfo.InvariantCulture, "fdb: transaction {0} can be safely retried", trans.Id));
						}

						if (context.Duration.Elapsed.TotalSeconds >= 1)
						{
							if (Logging.On) Logging.Info(String.Format(CultureInfo.InvariantCulture, "fdb WARNING: long transaction ({0:N1} sec elapsed in transaction lambda function ({1} retries, {2})", context.Duration.Elapsed.TotalSeconds, context.Retries, context.Committed ? "committed" : "not yet committed"));
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
				context.Duration.Stop();
				context.Dispose();
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
		public static Task RunReadAsync([NotNull] IFdbDatabase db, [NotNull] Func<IFdbReadOnlyTransaction, Task> asyncHandler, Action<IFdbReadOnlyTransaction> onDone, CancellationToken cancellationToken)
		{
			if (db == null) throw new ArgumentNullException("db");
			if (asyncHandler == null) throw new ArgumentNullException("asyncHandler");
			if (cancellationToken.IsCancellationRequested) return TaskHelpers.FromCancellation<object>(cancellationToken);

			var context = new FdbOperationContext(db, FdbTransactionMode.ReadOnly | FdbTransactionMode.InsideRetryLoop, cancellationToken);
			return ExecuteInternal(db, context, asyncHandler, onDone);
		}

		/// <summary>Run a read-only operation until it suceeds, timeouts, or fail with non-retryable error</summary>
		public static async Task<R> RunReadWithResultAsync<R>([NotNull] IFdbDatabase db, [NotNull] Func<IFdbReadOnlyTransaction, Task<R>> asyncHandler, Action<IFdbReadOnlyTransaction> onDone, CancellationToken cancellationToken)
		{
			if (db == null) throw new ArgumentNullException("db");
			if (asyncHandler == null) throw new ArgumentNullException("asyncHandler");
			cancellationToken.ThrowIfCancellationRequested();

			R result = default(R);
			Func<IFdbTransaction, Task> handler = async (tr) =>
			{
				result = await asyncHandler(tr).ConfigureAwait(false);
			};

			var context = new FdbOperationContext(db, FdbTransactionMode.ReadOnly | FdbTransactionMode.InsideRetryLoop, cancellationToken);
			await ExecuteInternal(db, context, handler, onDone).ConfigureAwait(false);
			return result;
		}

		#endregion

		#region Read/Write operations...

		/// <summary>Run a read/write operation until it suceeds, timeouts, or fail with non-retryable error</summary>
		public static Task RunWriteAsync([NotNull] IFdbDatabase db, [NotNull] Func<IFdbTransaction, Task> asyncHandler, Action<IFdbTransaction> onDone, CancellationToken cancellationToken)
		{
			if (db == null) throw new ArgumentNullException("db");
			if (asyncHandler == null) throw new ArgumentNullException("asyncHandler");
			if (cancellationToken.IsCancellationRequested) return TaskHelpers.FromCancellation<object>(cancellationToken);

			var context = new FdbOperationContext(db, FdbTransactionMode.Default | FdbTransactionMode.InsideRetryLoop, cancellationToken);
			return ExecuteInternal(db, context, asyncHandler, onDone);
		}

		/// <summary>Run a write operation until it suceeds, timeouts, or fail with non-retryable error</summary>
		public static Task RunWriteAsync([NotNull] IFdbDatabase db, [NotNull] Action<IFdbTransaction> handler, Action<IFdbTransaction> onDone, CancellationToken cancellationToken)
		{
			if (db == null) throw new ArgumentNullException("db");
			if (handler == null) throw new ArgumentNullException("handler");
			if (cancellationToken.IsCancellationRequested) return TaskHelpers.FromCancellation<object>(cancellationToken);

			var context = new FdbOperationContext(db, FdbTransactionMode.Default | FdbTransactionMode.InsideRetryLoop, cancellationToken);
			return ExecuteInternal(db, context, handler, onDone);
		}

		/// <summary>Run a read/write operation until it suceeds, timeouts, or fail with non-retryable error</summary>
		public static async Task<R> RunWriteWithResultAsync<R>([NotNull] IFdbDatabase db, [NotNull] Func<IFdbTransaction, Task<R>> asyncHandler, Action<IFdbTransaction> onDone, CancellationToken cancellationToken)
		{
			if (db == null) throw new ArgumentNullException("db");
			if (asyncHandler == null) throw new ArgumentNullException("asyncHandler");
			cancellationToken.ThrowIfCancellationRequested();

			R result = default(R);
			Func<IFdbTransaction, Task> handler = async (tr) =>
			{
				result = await asyncHandler(tr).ConfigureAwait(false);
			};

			var context = new FdbOperationContext(db, FdbTransactionMode.Default | FdbTransactionMode.InsideRetryLoop, cancellationToken);
			await ExecuteInternal(db, context, handler, onDone).ConfigureAwait(false);
			return result;
		}

		#endregion

	}

}
