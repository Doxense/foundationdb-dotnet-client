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

namespace FoundationDB.Client
{
	using System;
	using System.Collections.Generic;
	using System.Diagnostics;
	using System.Globalization;
	using System.Runtime.CompilerServices;
	using System.Threading;
	using System.Threading.Tasks;
	using Doxense.Diagnostics.Contracts;
	using Doxense.Threading.Tasks;
	using JetBrains.Annotations;

	/// <summary>
	/// Represents the context of a retry-able transactional function which accepts a read-only or read-write transaction.
	/// </summary>
	[DebuggerDisplay("Retries={Retries}, Committed={Committed}, Elapsed={Elapsed}")]
	[PublicAPI]
	public sealed class FdbOperationContext : IDisposable
	{
		//REVIEW: maybe we should find a way to reduce the size of this class? (it's already almost at 100 bytes !)

		/// <summary>The database used by the operation</summary>
		[NotNull]
		public IFdbDatabase Database { get; }

		/// <summary>Cancellation token associated with the operation</summary>
		public CancellationToken Cancellation { get; }

		/// <summary>If set to true, will abort and not commit the transaction. If false, will try to commit the transaction (and retry on failure)</summary>
		public bool Abort { get; set; }

		/// <summary>Current attempt number (0 for first, 1+ for retries)</summary>
		public int Retries { get; private set; }

		/// <summary>Error code of the previous attempt</summary>
		/// <remarks>Equal to <see cref="FdbError.Success"/> for the first attempt, or the error code generate by the previous failed attempt.</remarks>
		public FdbError PreviousError { get; private set; }

		/// <summary>Stopwatch that is started at the creation of the transaction, and stopped when it commits or gets disposed</summary>
		private ValueStopwatch Clock; //REVIEW: must be a field!

		/// <summary>Duration of all the previous attempts before the current one (starts at 0, and gets updated at each reset/retry)</summary>
		internal TimeSpan BaseDuration { get; private set; }

		/// <summary>Time elapsed since the start of the first attempt</summary>
		public TimeSpan ElapsedTotal => this.Clock.Elapsed;

		/// <summary>Time elapsed since the start of the current attempt</summary>
		/// <remarks>This value is reset to zero every time the transaction fails and is retried.
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

		/// <summary>Transaction instance currently being used by this context</summary>
		[CanBeNull]
		private IFdbTransaction Transaction;
		//note: field accessed via interlocked operations!

		/// <summary>Create a new retry loop operation context</summary>
		/// <param name="db">Database that will be used by the retry loop</param>
		/// <param name="mode">Operation mode of the retry loop</param>
		/// <param name="ct">Optional cancellation token that will abort the retry loop if triggered.</param>
		public FdbOperationContext([NotNull] IFdbDatabase db, FdbTransactionMode mode, CancellationToken ct)
		{
			Contract.NotNull(db, nameof(db));

			this.Database = db;
			this.Mode = mode;
			// note: we don't start the clock yet, only when the context starts executing...

			// by default, we hook ourselves to the database's CancellationToken, but we may need to also
			// hook with a different, caller-provided, token and respond to cancellation from both sites.
			var token = db.Cancellation;
			if (ct.CanBeCanceled && !ct.Equals(token))
			{
				this.TokenSource = CancellationTokenSource.CreateLinkedTokenSource(token, ct);
				token = this.TokenSource.Token;
			}
			this.Cancellation = token;
		}

		#region Sucess Handlers...

		/// <summary>List of one or more success handlers</summary>
		/// <remarks>Either null, a single Delegate, or an array Delegate[]</remarks>
		private object SuccessHandlers { get; set; }

		private void RegisterSuccessHandler(object handler)
		{
			Contract.Requires(handler is Delegate || handler is IHandleTransactionSuccess);
			lock (this)
			{
				var previous = this.SuccessHandlers;
				if (previous == null)
				{ // first handler for this context
					this.SuccessHandlers = handler;
				}
				else if (previous is object[] arr)
				{ // one more 
					Array.Resize(ref arr, arr.Length + 1);
					arr[arr.Length - 1] = handler;
					this.SuccessHandlers = arr;
				}
				else
				{ // second handler
					this.SuccessHandlers = new [] { previous, handler };
				}
			}
		}

		private Task ExecuteSuccessHandlers()
		{
			var handlers = this.SuccessHandlers;
			if (handlers == null) return Task.CompletedTask;

			return handlers is object[] arr
				? ExecuteMultipleHandlers(arr, this.Cancellation)
				: ExecuteSingleHandler(handlers, this.Cancellation);
		}

		private static Task ExecuteSingleHandler(object del, CancellationToken ct)
		{
			switch (del)
			{
				case IHandleTransactionSuccess hts:
				{
					hts.OnTransactionSuccessful();
					return Task.CompletedTask;
				}
				case IHandleTransactionFailure htf:
				{
					htf.OnTransactionFailed();
					return Task.CompletedTask;
				}
				case Action act:
				{
					act();
					return Task.CompletedTask;
				}
				case Func<CancellationToken, Task> fct:
				{
					return fct(ct);
				}
			}

			throw new NotSupportedException("Unexpected handler delegate type.");
		}

		private static async Task ExecuteMultipleHandlers(object[] arr, CancellationToken ct)
		{
			foreach (object del in arr)
			{
				if (del != null) await ExecuteSingleHandler(del, ct).ConfigureAwait(false);
			}
		}

		/// <summary>Register a callback that will only be called once the transaction has been successfully committed</summary>
		/// <remarks>NOTE: there are _no_ guarantees that the callback will fire at all, so this should only be used for cache updates or idempotent operations!</remarks>
		public void OnSuccess([NotNull] Action callback)
		{
			RegisterSuccessHandler(callback ?? throw new ArgumentNullException(nameof(callback)));
		}

		public void OnSuccess([NotNull] Func<CancellationToken, Task> callback)
		{
			RegisterSuccessHandler(callback ?? throw new ArgumentNullException(nameof(callback)));
		}

		#endregion

		#region Local Data...

		/// <summary>Stores contextual data that can be added and retrieved at any step of the transaction's processing</summary>
		/// <remarks>Access to this collection must be performed under lock, because a transaction can be used concurrently from multiple threads!</remarks>
		private Dictionary<Type, object> LocalData { get; set; }

		[Pure, CanBeNull, ContractAnnotation("createIfMissing:true => notnull"), MethodImpl(MethodImplOptions.AggressiveInlining)]
		private Dictionary<Type, object> GetLocalDataContainer(bool createIfMissing)
		{
			return this.LocalData ?? GetLocalDataContainerSlow(createIfMissing);
		}

		[CanBeNull, ContractAnnotation("createIfMissing:true => notnull"), MethodImpl(MethodImplOptions.NoInlining)]
		private Dictionary<Type, object> GetLocalDataContainerSlow(bool createIfMissing)
		{
			var container = this.LocalData;
			if (container == null && createIfMissing)
			{
				container = new Dictionary<Type, object>();
				this.LocalData = container;
			}
			return container;
		}

		public void SetLocalData<TState>([NotNull] TState newState) where TState : class
		{
			Contract.NotNull(newState, nameof(newState));
			lock (this)
			{
				GetLocalDataContainer(true)[typeof(TState)] = newState;
			}
		}

		public void RemoveLocalData<TState>() where TState : class
		{
			lock (this)
			{
				GetLocalDataContainer(false)?.Remove(typeof(TState));
			}
		}

		[ContractAnnotation("=>false, state:null; =>true, state:notnull")]
		public bool TryGetLocalData<TState>(out TState state) where TState : class
		{
			lock (this)
			{
				var container = GetLocalDataContainer(false);
				if (container != null && container.TryGetValue(typeof(TState), out var value))
				{
					state = (TState) value;
					return true;
				}
				state = default;
				return false;
			}
		}

		[NotNull]
		public TState GetOrCreateLocalData<TState>() where TState : class, new()
		{
			lock (this)
			{
				var container = GetLocalDataContainer(true);
				TState result;
				if (container.TryGetValue(typeof(TState), out var value))
				{
					result = (TState) value;
				}
				else
				{
					result = new TState();
					container[typeof(TState)] = result;
				}
				return result;
			}
		}


		[NotNull]
		public TState GetOrCreateLocalData<TState>(Func<TState> factory) where TState : class
		{
			lock (this)
			{
				var container = GetLocalDataContainer(true);
				TState result;
				if (container.TryGetValue(typeof(TState), out var value))
				{
					result = (TState) value;
				}
				else
				{
					result = factory() ?? throw new InvalidOperationException("Constructed state cannot be null");
					container[typeof(TState)] = result;
				}
				return result;
			}
		}

		#endregion

		/// <summary>Execute a retry loop on this context</summary>
		internal static async Task ExecuteInternal([NotNull] FdbOperationContext context, [NotNull] Delegate handler, Delegate success)
		{
			Contract.Requires(context != null && handler != null);
			Contract.Requires(context.Database != null && context.Shared);

			if (context.Abort) throw new InvalidOperationException("Operation context has already been aborted or disposed");

			try
			{
				// make sure to reset everything (in case a context is reused multiple times)
				context.Committed = false;
				context.Retries = 0;
				context.BaseDuration = TimeSpan.Zero;
				context.Clock = ValueStopwatch.StartNew();
				//note: we start the clock immediately, but the transaction's 5 seconds max lifetime is actually measured from the first read operation (Get, GetRange, GetReadVersion, etc...)
				// => algorithms that monitor the elapsed duration to rate limit themselves may think that the trans is older than it really is...
				// => we would need to plug into the transaction handler itself to be notified when exactly a read op starts...

				using (var trans = await context.Database.BeginTransactionAsync(context.Mode, CancellationToken.None, context))
				{
					//note: trans may be different from context.Transaction if it has been filtered!
					Contract.Assert(context.Transaction != null);

					while (!context.Committed && !context.Cancellation.IsCancellationRequested)
					{
						try
						{
							switch (handler)
							{
								// call the user provided lambda
								case Func<IFdbReadOnlyTransaction, Task> funcReadOnly:
								{
									await funcReadOnly(trans).ConfigureAwait(false);
									break;
								}
								case Func<IFdbTransaction, Task> funcWritable:
								{
									await funcWritable(trans).ConfigureAwait(false);
									break;
								}
								case Action<IFdbTransaction> action:
								{
									action(trans);
									break;
								}
								default:
								{
									throw new NotSupportedException($"Cannot execute handlers of type {handler.GetType().Name}");
								}
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
							context.PreviousError = FdbError.Success;

							// execute any success handlers if there are any
							if (context.SuccessHandlers != null)
							{
								await context.ExecuteSuccessHandlers().ConfigureAwait(false);
							}

							// execute any final logic, if there is any
							if (success != null)
							{
								switch (success)
								{
									case Action<IFdbReadOnlyTransaction> action1:
									{
										action1(trans);
										break;
									}
									case Action<IFdbTransaction> action2:
									{
										action2(trans);
										break;
									}
									case Func<IFdbReadOnlyTransaction, Task> func1:
									{
										await func1(trans).ConfigureAwait(false);
										break;
									}
									case Func<IFdbTransaction, Task> func2:
									{
										await func2(trans).ConfigureAwait(false);
										break;
									}
									default:
									{
										throw new NotSupportedException($"Cannot execute completion handler of type {handler.GetType().Name}");
									}
								}
							}
						}
						catch (FdbException e)
						{
							context.PreviousError = e.Code;

							// reset any handler
							context.SuccessHandlers = null;

							if (Logging.On && Logging.IsVerbose) Logging.Verbose(string.Format(CultureInfo.InvariantCulture, "fdb: transaction {0} failed with error code {1}", trans.Id, e.Code));
							await trans.OnErrorAsync(e.Code).ConfigureAwait(false);
							if (Logging.On && Logging.IsVerbose) Logging.Verbose(string.Format(CultureInfo.InvariantCulture, "fdb: transaction {0} can be safely retried", trans.Id));
						}

						// update the base time for the next attempt
						context.BaseDuration = context.ElapsedTotal;
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
				if (context.BaseDuration.TotalSeconds >= 10)
				{
					//REVIEW: this may not be a good idea to spam the logs with long running transactions??
					if (Logging.On) Logging.Info(string.Format(CultureInfo.InvariantCulture, "fdb WARNING: long transaction ({0:N1} sec elapsed in transaction lambda function ({1} retries, {2})", context.BaseDuration.TotalSeconds, context.Retries, context.Committed ? "committed" : "not committed"));
				}
				context.Dispose();
			}
		}

		internal void AttachTransaction([NotNull] IFdbTransaction trans)
		{
			if (Interlocked.CompareExchange(ref this.Transaction, trans, null) != null)
			{
				throw new InvalidOperationException("Cannot attach another transaction to this context because another transaction is still attache");
			}
		}

		internal void ReleaseTransaction([NotNull] IFdbTransaction trans)
		{
			// only if this is still the current one!
			Interlocked.CompareExchange(ref this.Transaction, null, trans);
		}

		public void Dispose()
		{
			this.Abort = true;
			this.TokenSource?.SafeCancelAndDispose();
		}

		#region Read-Only operations...

		/// <summary>Run a read-only operation until it succeeds, timeouts, or fails with a non retry-able error</summary>
		[Obsolete("Will be removed soon.")]
		public static Task RunReadAsync([NotNull] IFdbDatabase db, [NotNull] Func<IFdbReadOnlyTransaction, Task> handler, CancellationToken ct)
		{
			Contract.NotNull(db, nameof(db));
			Contract.NotNull(handler, nameof(handler));
			if (ct.IsCancellationRequested) return Task.FromCanceled(ct);

			var context = new FdbOperationContext(db, FdbTransactionMode.ReadOnly | FdbTransactionMode.InsideRetryLoop, ct);
			return ExecuteInternal(context, handler, null);
		}

		/// <summary>Run a read-only operation until it succeeds, timeouts, or fails with a non retry-able error</summary>
		public static async Task<TResult> RunReadWithResultAsync<TResult>([NotNull] IFdbDatabase db, [NotNull] Func<IFdbReadOnlyTransaction, Task<TResult>> handler, CancellationToken ct)
		{
			Contract.NotNull(db, nameof(db));
			Contract.NotNull(handler, nameof(handler));
			ct.ThrowIfCancellationRequested();

			TResult result = default;
			async Task Handler(IFdbTransaction tr)
			{
				result = await handler(tr).ConfigureAwait(false);
			}

			var context = new FdbOperationContext(db, FdbTransactionMode.ReadOnly | FdbTransactionMode.InsideRetryLoop, ct);
			await ExecuteInternal(context, (Func<IFdbTransaction, Task>) Handler, null).ConfigureAwait(false);
			return result;
		}

		/// <summary>Run a read-only operation until it succeeds, timeouts, or fails with a non retry-able error</summary>
		public static async Task<TResult> RunReadWithResultAsync<TResult>([NotNull] IFdbDatabase db, [NotNull] Func<IFdbReadOnlyTransaction, ValueTask<TResult>> handler, CancellationToken ct)
		{
			Contract.NotNull(db, nameof(db));
			Contract.NotNull(handler, nameof(handler));
			ct.ThrowIfCancellationRequested();

			TResult result = default;
			async Task Handler(IFdbTransaction tr)
			{
				result = await handler(tr).ConfigureAwait(false);
			}

			var context = new FdbOperationContext(db, FdbTransactionMode.ReadOnly | FdbTransactionMode.InsideRetryLoop, ct);
			await ExecuteInternal(context, (Func<IFdbTransaction, Task>) Handler, null).ConfigureAwait(false);
			return result;
		}

		/// <summary>Run a read-only operation until it succeeds, timeouts, or fails with a non retry-able error</summary>
		public static async Task<TResult> RunReadWithResultAsync<TResult>([NotNull] IFdbDatabase db, [NotNull] Func<IFdbReadOnlyTransaction, Task<TResult>> handler, [NotNull] Action<IFdbReadOnlyTransaction, TResult> success, CancellationToken ct)
		{
			Contract.NotNull(db, nameof(db));
			Contract.NotNull(handler, nameof(handler));
			ct.ThrowIfCancellationRequested();

			TResult result = default;
			async Task Handler(IFdbReadOnlyTransaction tr)
			{
				result = await handler(tr).ConfigureAwait(false);
			}

			void Complete(IFdbReadOnlyTransaction tr)
			{
				success(tr, result);
			}

			var context = new FdbOperationContext(db, FdbTransactionMode.ReadOnly | FdbTransactionMode.InsideRetryLoop, ct);
			await ExecuteInternal(context, (Func<IFdbTransaction, Task>) Handler, (Action<IFdbReadOnlyTransaction>) Complete).ConfigureAwait(false);
			return result;
		}

		/// <summary>Run a read-only operation until it succeeds, timeouts, or fails with a non retry-able error</summary>
		public static async Task<TResult> RunReadWithResultAsync<TResult>([NotNull] IFdbDatabase db, [NotNull] Func<IFdbReadOnlyTransaction, ValueTask<TResult>> handler, [NotNull] Action<IFdbReadOnlyTransaction, TResult> success, CancellationToken ct)
		{
			Contract.NotNull(db, nameof(db));
			Contract.NotNull(handler, nameof(handler));
			ct.ThrowIfCancellationRequested();

			TResult result = default;
			async Task Handler(IFdbReadOnlyTransaction tr)
			{
				result = await handler(tr).ConfigureAwait(false);
			}

			void Complete(IFdbReadOnlyTransaction tr)
			{
				success(tr, result);
			}

			var context = new FdbOperationContext(db, FdbTransactionMode.ReadOnly | FdbTransactionMode.InsideRetryLoop, ct);
			await ExecuteInternal(context, (Func<IFdbTransaction, Task>) Handler, (Action<IFdbReadOnlyTransaction>) Complete).ConfigureAwait(false);
			return result;
		}

		/// <summary>Run a read-only operation until it succeeds, timeouts, or fails with a non retry-able error</summary>
		public static async Task<TResult> RunReadWithResultAsync<TIntermediate, TResult>([NotNull] IFdbDatabase db, [NotNull] Func<IFdbReadOnlyTransaction, Task<TIntermediate>> handler, [NotNull] Func<IFdbReadOnlyTransaction, TIntermediate, TResult> success, CancellationToken ct)
		{
			Contract.NotNull(db, nameof(db));
			Contract.NotNull(handler, nameof(handler));
			ct.ThrowIfCancellationRequested();

			TIntermediate tmp= default;
			async Task Handler(IFdbReadOnlyTransaction tr)
			{
				tmp = await handler(tr).ConfigureAwait(false);
			}

			TResult result = default;
			void Complete(IFdbReadOnlyTransaction tr)
			{
				result = success(tr, tmp);
			}

			var context = new FdbOperationContext(db, FdbTransactionMode.ReadOnly | FdbTransactionMode.InsideRetryLoop, ct);
			await ExecuteInternal(context, (Func<IFdbTransaction, Task>) Handler, (Action<IFdbReadOnlyTransaction>) Complete).ConfigureAwait(false);
			return result;
		}

		/// <summary>Run a read-only operation until it succeeds, timeouts, or fails with a non retry-able error</summary>
		public static async Task<TResult> RunReadWithResultAsync<TIntermediate, TResult>([NotNull] IFdbDatabase db, [NotNull] Func<IFdbReadOnlyTransaction, Task<TIntermediate>> handler, [NotNull] Func<IFdbReadOnlyTransaction, TIntermediate, Task<TResult>> success, CancellationToken ct)
		{
			Contract.NotNull(db, nameof(db));
			Contract.NotNull(handler, nameof(handler));
			ct.ThrowIfCancellationRequested();

			TIntermediate tmp= default;
			async Task Handler(IFdbReadOnlyTransaction tr)
			{
				tmp = await handler(tr).ConfigureAwait(false);
			}

			TResult result = default;
			async Task Complete(IFdbReadOnlyTransaction tr)
			{
				result = await success(tr, tmp).ConfigureAwait(false);
			}

			var context = new FdbOperationContext(db, FdbTransactionMode.ReadOnly | FdbTransactionMode.InsideRetryLoop, ct);
			await ExecuteInternal(context, (Func<IFdbTransaction, Task>) Handler, (Func<IFdbReadOnlyTransaction, Task>) Complete).ConfigureAwait(false);
			return result;
		}

		#endregion

		#region Read/Write operations...

		/// <summary>Run a write operation until it succeeds, timeouts, or fails with a non retry-able error</summary>
		public static Task RunWriteAsync([NotNull] IFdbDatabase db, [NotNull] Action<IFdbTransaction> handler, CancellationToken ct)
		{
			Contract.NotNull(db, nameof(db));
			Contract.NotNull(handler, nameof(handler));
			if (ct.IsCancellationRequested) return Task.FromCanceled(ct);

			var context = new FdbOperationContext(db, FdbTransactionMode.Default | FdbTransactionMode.InsideRetryLoop, ct);
			return ExecuteInternal(context, handler, null);
		}

		/// <summary>Run a read/write operation until it succeeds, timeouts, or fails a with non retry-able error</summary>
		public static Task RunWriteAsync([NotNull] IFdbDatabase db, [NotNull] Func<IFdbTransaction, Task> handler, CancellationToken ct)
		{
			Contract.NotNull(db, nameof(db));
			Contract.NotNull(handler, nameof(handler));
			if (ct.IsCancellationRequested) return Task.FromCanceled(ct);

			var context = new FdbOperationContext(db, FdbTransactionMode.Default | FdbTransactionMode.InsideRetryLoop, ct);
			return ExecuteInternal(context, handler, null);
		}

		/// <summary>Run a write operation until it succeeds, timeouts, or fails with a non retry-able error</summary>
		public static Task RunWriteAsync([NotNull] IFdbDatabase db, [NotNull] Action<IFdbTransaction> handler, [NotNull] Action<IFdbTransaction> success, CancellationToken ct)
		{
			Contract.NotNull(db, nameof(db));
			Contract.NotNull(handler, nameof(handler));
			Contract.NotNull(success, nameof(success));
			if (ct.IsCancellationRequested) return Task.FromCanceled(ct);

			var context = new FdbOperationContext(db, FdbTransactionMode.Default | FdbTransactionMode.InsideRetryLoop, ct);
			return ExecuteInternal(context, handler, success);
		}

		/// <summary>Run a write operation until it succeeds, timeouts, or fails with a non retry-able error</summary>
		public static Task RunWriteAsync([NotNull] IFdbDatabase db, [NotNull] Action<IFdbTransaction> handler, [NotNull] Func<IFdbTransaction, Task> success, CancellationToken ct)
		{
			Contract.NotNull(db, nameof(db));
			Contract.NotNull(handler, nameof(handler));
			Contract.NotNull(success, nameof(success));
			if (ct.IsCancellationRequested) return Task.FromCanceled(ct);

			var context = new FdbOperationContext(db, FdbTransactionMode.Default | FdbTransactionMode.InsideRetryLoop, ct);
			return ExecuteInternal(context, handler, success);
		}

		/// <summary>Run a write operation until it succeeds, timeouts, or fails with a non retry-able error</summary>
		public static async Task<TResult> RunWriteAsync<TResult>([NotNull] IFdbDatabase db, [NotNull] Action<IFdbTransaction> handler, [NotNull] Func<IFdbTransaction, TResult> success, CancellationToken ct)
		{
			Contract.NotNull(db, nameof(db));
			Contract.NotNull(handler, nameof(handler));
			Contract.NotNull(success, nameof(success));
			ct.ThrowIfCancellationRequested();

			TResult result = default;
			void Complete(IFdbTransaction tr)
			{
				result = success(tr);
			}

			var context = new FdbOperationContext(db, FdbTransactionMode.Default | FdbTransactionMode.InsideRetryLoop, ct);
			await ExecuteInternal(context, handler, (Action<IFdbTransaction>) Complete).ConfigureAwait(false);
			return result;
		}

		/// <summary>Run a read/write operation until it succeeds, timeouts, or fails with a non retry-able error</summary>
		public static Task RunWriteAsync([NotNull] IFdbDatabase db, [NotNull] Func<IFdbTransaction, Task> handler, [NotNull] Action<IFdbTransaction> success, CancellationToken ct)
		{
			Contract.NotNull(db, nameof(db));
			Contract.NotNull(handler, nameof(handler));
			Contract.NotNull(success, nameof(success));
			if (ct.IsCancellationRequested) return Task.FromCanceled(ct);

			var context = new FdbOperationContext(db, FdbTransactionMode.Default | FdbTransactionMode.InsideRetryLoop, ct);
			return ExecuteInternal(context, handler, success);
		}

		/// <summary>Run a read/write operation until it succeeds, timeouts, or fails with a non retry-able error</summary>
		public static Task RunWriteAsync([NotNull] IFdbDatabase db, [NotNull] Func<IFdbTransaction, Task> handler, [NotNull] Func<IFdbTransaction, Task> success, CancellationToken ct)
		{
			Contract.NotNull(db, nameof(db));
			Contract.NotNull(handler, nameof(handler));
			Contract.NotNull(success, nameof(success));
			if (ct.IsCancellationRequested) return Task.FromCanceled(ct);

			var context = new FdbOperationContext(db, FdbTransactionMode.Default | FdbTransactionMode.InsideRetryLoop, ct);
			return ExecuteInternal(context, handler, success);
		}

		/// <summary>Run a read/write operation until it succeeds, timeouts, or fails with a non retry-able error</summary>
		public static async Task<TResult> RunWriteWithResultAsync<TResult>([NotNull] IFdbDatabase db, [NotNull] Func<IFdbTransaction, TResult> handler, CancellationToken ct)
		{
			Contract.NotNull(db, nameof(db));
			Contract.NotNull(handler, nameof(handler));
			ct.ThrowIfCancellationRequested();

			TResult result = default;
			void Handler(IFdbTransaction tr)
			{
				result = handler(tr);
			}

			var context = new FdbOperationContext(db, FdbTransactionMode.Default | FdbTransactionMode.InsideRetryLoop, ct);
			await ExecuteInternal(context, (Action<IFdbTransaction>) Handler, null).ConfigureAwait(false);
			return result;
		}

		/// <summary>Run a read/write operation until it succeeds, timeouts, or fails with a non retry-able error</summary>
		public static async Task<TResult> RunWriteWithResultAsync<TResult>([NotNull] IFdbDatabase db, [NotNull] Func<IFdbTransaction, Task<TResult>> handler, CancellationToken ct)
		{
			Contract.NotNull(db, nameof(db));
			Contract.NotNull(handler, nameof(handler));
			ct.ThrowIfCancellationRequested();

			TResult result = default;
			async Task Handler(IFdbTransaction tr)
			{
				result = await handler(tr).ConfigureAwait(false);
			}

			var context = new FdbOperationContext(db, FdbTransactionMode.Default | FdbTransactionMode.InsideRetryLoop, ct);
			await ExecuteInternal(context, (Func<IFdbTransaction, Task>) Handler, null).ConfigureAwait(false);
			return result;
		}

		/// <summary>Run a read/write operation until it succeeds, timeouts, or fails with a non retry-able error</summary>
		public static async Task<TResult> RunWriteWithResultAsync<TResult>([NotNull] IFdbDatabase db, [NotNull] Func<IFdbTransaction, Task<TResult>> handler, [NotNull] Action<IFdbTransaction, TResult> success, CancellationToken ct)
		{
			Contract.NotNull(db, nameof(db));
			Contract.NotNull(handler, nameof(handler));
			Contract.NotNull(success, nameof(success));
			ct.ThrowIfCancellationRequested();

			TResult result = default;
			async Task Handler(IFdbTransaction tr)
			{
				result = await handler(tr).ConfigureAwait(false);
			}

			void Complete(IFdbTransaction tr)
			{
				success(tr, result);
			}

			var context = new FdbOperationContext(db, FdbTransactionMode.Default | FdbTransactionMode.InsideRetryLoop, ct);
			await ExecuteInternal(context, (Func<IFdbTransaction, Task>) Handler, (Action<IFdbTransaction>) Complete).ConfigureAwait(false);
			return result;
		}

		/// <summary>Run a read/write operation until it succeeds, timeouts, or fails with a non retry-able error</summary>
		public static async Task<TResult> RunWriteWithResultAsync<TIntermediate, TResult>([NotNull] IFdbDatabase db, [NotNull] Func<IFdbTransaction, Task<TIntermediate>> handler, [NotNull] Func<IFdbTransaction, TIntermediate, TResult> success, CancellationToken ct)
		{
			Contract.NotNull(db, nameof(db));
			Contract.NotNull(handler, nameof(handler));
			Contract.NotNull(success, nameof(success));
			ct.ThrowIfCancellationRequested();

			TIntermediate tmp = default;
			async Task Handler(IFdbTransaction tr)
			{
				tmp = await handler(tr).ConfigureAwait(false);
			}

			TResult result = default;
			void Complete(IFdbTransaction tr)
			{
				result = success(tr, tmp);
			}

			var context = new FdbOperationContext(db, FdbTransactionMode.Default | FdbTransactionMode.InsideRetryLoop, ct);
			await ExecuteInternal(context, (Func<IFdbTransaction, Task>) Handler, (Action<IFdbTransaction>) Complete).ConfigureAwait(false);
			return result;
		}

		/// <summary>Run a read/write operation until it succeeds, timeouts, or fails with a non retry-able error</summary>
		public static async Task<TResult> RunWriteWithResultAsync<TIntermediate, TResult>([NotNull] IFdbDatabase db, [NotNull] Func<IFdbTransaction, Task<TIntermediate>> handler, [NotNull] Func<IFdbTransaction, TIntermediate, Task<TResult>> success, CancellationToken ct)
		{
			Contract.NotNull(db, nameof(db));
			Contract.NotNull(handler, nameof(handler));
			Contract.NotNull(success, nameof(success));
			ct.ThrowIfCancellationRequested();

			TIntermediate tmp = default;
			async Task Handler(IFdbTransaction tr)
			{
				tmp = await handler(tr).ConfigureAwait(false);
			}

			TResult result = default;
			async Task Complete(IFdbTransaction tr)
			{
				result = await success(tr, tmp).ConfigureAwait(false);
			}

			var context = new FdbOperationContext(db, FdbTransactionMode.Default | FdbTransactionMode.InsideRetryLoop, ct);
			await ExecuteInternal(context, (Func<IFdbTransaction, Task>) Handler, (Func<IFdbTransaction, Task>) Complete).ConfigureAwait(false);
			return result;
		}

		#endregion

	}
 
	/// <summary>Marker interface for objects that need to 'activate' only once a transaction commits successful.</summary>
	internal interface IHandleTransactionSuccess
	{
		//REVIEW: for now internal only, we'll see if we make this public!

		void OnTransactionSuccessful();
	}

	/// <summary>Marker interface for objects that need to 'self destruct' if a transaction fails to commit.</summary>
	internal interface IHandleTransactionFailure
	{
		//REVIEW: for now internal only, we'll see if we make this public!

		void OnTransactionFailed();
	}
}
