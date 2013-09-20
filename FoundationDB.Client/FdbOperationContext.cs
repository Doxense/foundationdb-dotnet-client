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

	/// <summary>
	/// Represents the context of a retryable transactional function wich accept a read-only or read-write transaction.
	/// </summary>
	[DebuggerDisplay("Retries={Retries}, Committed={Committed}, Elapsed={Duration.Elapsed}")]
	public sealed class FdbOperationContext
	{
		/// <summary>The database used by the operation</summary>
		public FdbDatabase Db { get; private set; }

		/// <summary>The state of the operation (or null)</summary>
		public object State { get; set; }

		/// <summary>If true, attempts to commit read-only transactions anyway.</summary>
		public bool CommitReadOnlyTransactions { get; set; }

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

		/// <summary>If true, the transactino has been committed successfully</summary>
		public bool Committed { get; private set; }

		internal FdbOperationContext(FdbDatabase db, object state)
		{
			Contract.Requires(db != null);

			this.Db = db;
			this.State = state;
			this.Duration = new Stopwatch();
		}

#if REFACTORED

		/// <summary>Run the operation until it suceeds, timeouts, or fail with non-retryable error</summary>
		public Task Execute(Func<IFdbTransaction, Task> asyncAction, CancellationToken ct)
		{
			if (asyncAction == null) throw new ArgumentNullException("asyncAction");
			return ExecuteInternal(asyncAction, ct);
		}

		/// <summary>Run the operation until it suceeds, timeouts, or fail with non-retryable error</summary>
		public Task Execute(Func<IFdbTransaction, CancellationToken, Task> asyncAction, CancellationToken ct)
		{
			if (asyncAction == null) throw new ArgumentNullException("asyncAction");
			return ExecuteInternal(asyncAction, ct);
		}

		/// <summary>Run the operation until it suceeds, timeouts, or fail with non-retryable error</summary>
		public Task Execute(Func<IFdbTransaction, FdbOperationContext, Task> asyncAction, CancellationToken ct)
		{
			if (asyncAction == null) throw new ArgumentNullException("asyncAction");
			return ExecuteInternal(asyncAction, ct);
		}

		/// <summary>Run the operation until it suceeds, timeouts, or fail with non-retryable error</summary>
		public Task Execute(Func<IFdbReadTransaction, Task> asyncAction, CancellationToken ct)
		{
			if (asyncAction == null) throw new ArgumentNullException("asyncAction");
			return ExecuteInternal(asyncAction, ct);
		}

		/// <summary>Run the operation until it suceeds, timeouts, or fail with non-retryable error</summary>
		public Task Execute(Func<IFdbReadTransaction, CancellationToken, Task> asyncAction, CancellationToken ct)
		{
			if (asyncAction == null) throw new ArgumentNullException("asyncAction");
			return ExecuteInternal(asyncAction, ct);
		}

		/// <summary>Run the operation until it suceeds, timeouts, or fail with non-retryable error</summary>
		public Task Execute(Func<IFdbReadTransaction, FdbOperationContext, Task> asyncAction, CancellationToken ct)
		{
			if (asyncAction == null) throw new ArgumentNullException("asyncAction");
			return ExecuteInternal(asyncAction, ct);
		}

#endif

		internal async Task ExecuteInternal(Delegate handler, CancellationToken ct)
		{
			Contract.Requires(handler != null);

			try
			{
				this.Committed = false;
				this.Token = ct;
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
							else if (handler is Func<IFdbTransaction,CancellationToken,Task>)
							{
								await ((Func<IFdbTransaction, CancellationToken, Task>)handler)(trans, this.Token).ConfigureAwait(false);
							}
							else if (handler is Func<IFdbTransaction, FdbOperationContext, Task>)
							{
								await ((Func<IFdbTransaction, FdbOperationContext, Task>)handler)(trans, this).ConfigureAwait(false);
							}
							else if (handler is Action<IFdbTransaction, FdbOperationContext>)
							{
								((Action<IFdbTransaction, FdbOperationContext>)handler)(trans, this);
							}
							else if (handler is Func<IFdbReadTransaction, Task>)
							{
								readOnlyOperation = true;
								await ((Func<IFdbTransaction, Task>)handler)(trans).ConfigureAwait(false);
							}
							else if (handler is Func<IFdbReadTransaction, CancellationToken, Task>)
							{
								readOnlyOperation = true;
								await ((Func<IFdbTransaction, CancellationToken, Task>)handler)(trans, this.Token).ConfigureAwait(false);
							}
							else if (handler is Func<IFdbReadTransaction, FdbOperationContext, Task>)
							{
								readOnlyOperation = true;
								await ((Func<IFdbTransaction, FdbOperationContext, Task>)handler)(trans, this).ConfigureAwait(false);
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
								await trans.CommitAsync(this.Token).ConfigureAwait(false);
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
				this.Token = CancellationToken.None;
			}
		}

		#region Read-Only operations...

		/// <summary>Run a read-only operation until it suceeds, timeouts, or fail with non-retryable error</summary>
		public static Task RunReadAsync(FdbDatabase db, Func<IFdbReadTransaction, FdbOperationContext, Task> asyncAction, object state, CancellationToken ct = default(CancellationToken))
		{
			return new FdbOperationContext(db, state).ExecuteInternal(asyncAction, ct);
		}

		/// <summary>Run a read-only operation until it suceeds, timeouts, or fail with non-retryable error</summary>
		public static Task RunReadAsync(FdbDatabase db, Func<IFdbReadTransaction, CancellationToken, Task> asyncAction, object state, CancellationToken ct = default(CancellationToken))
		{
			return new FdbOperationContext(db, state).ExecuteInternal(asyncAction, ct);
		}

		/// <summary>Run a read-only operation until it suceeds, timeouts, or fail with non-retryable error</summary>
		public static Task RunReadAsync(FdbDatabase db, Func<IFdbReadTransaction, Task> asyncAction, object state, CancellationToken ct = default(CancellationToken))
		{
			return new FdbOperationContext(db, state).ExecuteInternal(asyncAction, ct);
		}

		/// <summary>Run a read-only operation until it suceeds, timeouts, or fail with non-retryable error</summary>
		public static async Task<R> RunReadWithResultAsync<R>(FdbDatabase db, Func<IFdbReadTransaction, FdbOperationContext, Task<R>> asyncAction, object state, CancellationToken ct = default(CancellationToken))
		{
			R result = default(R);
			Func<IFdbTransaction, FdbOperationContext, Task> handler = async (tr, _context) =>
			{
				result = await asyncAction(tr, _context).ConfigureAwait(false);
			};

			var context = new FdbOperationContext(db, state);
			await context.ExecuteInternal(handler, ct).ConfigureAwait(false);
			return result;
		}

		/// <summary>Run a read-only operation until it suceeds, timeouts, or fail with non-retryable error</summary>
		public static async Task<R> RunReadWithResultAsync<R>(FdbDatabase db, Func<IFdbReadTransaction, CancellationToken, Task<R>> asyncAction, object state, CancellationToken ct = default(CancellationToken))
		{
			R result = default(R);
			Func<IFdbTransaction, FdbOperationContext, Task> handler = async (tr, _context) =>
			{
				result = await asyncAction(tr, _context.Token).ConfigureAwait(false);
			};

			var context = new FdbOperationContext(db, state);
			await context.ExecuteInternal(handler, ct).ConfigureAwait(false);
			return result;
		}

		/// <summary>Run a read-only operation until it suceeds, timeouts, or fail with non-retryable error</summary>
		public static async Task<R> RunReadWithResultAsync<R>(FdbDatabase db, Func<IFdbReadTransaction, Task<R>> asyncAction, object state, CancellationToken ct = default(CancellationToken))
		{
			R result = default(R);
			Func<IFdbTransaction, FdbOperationContext, Task> handler = async (tr, _context) =>
			{
				result = await asyncAction(tr).ConfigureAwait(false);
			};

			var context = new FdbOperationContext(db, state);
			await context.ExecuteInternal(handler, ct).ConfigureAwait(false);
			return result;
		}

		#endregion

		#region Read/Write operations...

		/// <summary>Run a read/write operation until it suceeds, timeouts, or fail with non-retryable error</summary>
		public static Task RunWriteAsync(FdbDatabase db, Func<IFdbTransaction, FdbOperationContext, Task> asyncAction, object state, CancellationToken ct = default(CancellationToken))
		{
			return new FdbOperationContext(db, state).ExecuteInternal(asyncAction, ct);
		}

		/// <summary>Run a read/write operation until it suceeds, timeouts, or fail with non-retryable error</summary>
		public static Task RunWriteAsync(FdbDatabase db, Func<IFdbTransaction, CancellationToken, Task> asyncAction, object state, CancellationToken ct = default(CancellationToken))
		{
			return new FdbOperationContext(db, state).ExecuteInternal(asyncAction, ct);
		}

		/// <summary>Run a read/write operation until it suceeds, timeouts, or fail with non-retryable error</summary>
		public static Task RunWriteAsync(FdbDatabase db, Func<IFdbTransaction, Task> asyncAction, object state, CancellationToken ct = default(CancellationToken))
		{
			return new FdbOperationContext(db, state).ExecuteInternal(asyncAction, ct);
		}

		/// <summary>Run a write operation until it suceeds, timeouts, or fail with non-retryable error</summary>
		public static Task RunWriteAsync(FdbDatabase db, Action<IFdbTransaction, FdbOperationContext> action, object state, CancellationToken ct = default(CancellationToken))
		{
			return new FdbOperationContext(db, state).ExecuteInternal(action, ct);
		}

		/// <summary>Run a read/write operation until it suceeds, timeouts, or fail with non-retryable error</summary>
		public static async Task<R> RunWriteWithResultAsync<R>(FdbDatabase db, Func<IFdbTransaction, FdbOperationContext, Task<R>> asyncAction, object state, CancellationToken ct = default(CancellationToken))
		{
			R result = default(R);
			Func<IFdbTransaction, FdbOperationContext, Task> handler = async (tr, _context) =>
			{
				result = await asyncAction(tr, _context).ConfigureAwait(false);
			};

			var context = new FdbOperationContext(db, state);
			await context.ExecuteInternal(handler, ct).ConfigureAwait(false);
			return result;
		}

		/// <summary>Run a read/write operation until it suceeds, timeouts, or fail with non-retryable error</summary>
		public static async Task<R> RunWriteWithResultAsync<R>(FdbDatabase db, Func<IFdbTransaction, CancellationToken, Task<R>> asyncAction, object state, CancellationToken ct = default(CancellationToken))
		{
			R result = default(R);
			Func<IFdbTransaction, FdbOperationContext, Task> handler = async (tr, _context) =>
			{
				result = await asyncAction(tr, _context.Token).ConfigureAwait(false);
			};

			var context = new FdbOperationContext(db, state);
			await context.ExecuteInternal(handler, ct).ConfigureAwait(false);
			return result;
		}

		/// <summary>Run a read/write operation until it suceeds, timeouts, or fail with non-retryable error</summary>
		public static async Task<R> RunWriteWithResultAsync<R>(FdbDatabase db, Func<IFdbTransaction, Task<R>> asyncAction, object state, CancellationToken ct = default(CancellationToken))
		{
			R result = default(R);
			Func<IFdbTransaction, FdbOperationContext, Task> handler = async (tr, _context) =>
			{
				result = await asyncAction(tr).ConfigureAwait(false);
			};

			var context = new FdbOperationContext(db, state);
			await context.ExecuteInternal(handler, ct).ConfigureAwait(false);
			return result;
		}

		#endregion

	}

}
