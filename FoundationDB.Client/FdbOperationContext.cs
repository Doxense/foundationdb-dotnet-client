#region BSD License
/* Copyright (c) 2013-2020, Doxense SAS
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
	using System.Buffers;
	using System.Collections.Generic;
	using System.Diagnostics;
	using System.Diagnostics.CodeAnalysis;
	using System.Globalization;
	using System.Linq;
	using System.Runtime.CompilerServices;
	using System.Threading;
	using System.Threading.Tasks;
	using Doxense.Diagnostics.Contracts;
	using Doxense.Threading.Tasks;
	using FoundationDB.Filters.Logging;
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
		internal CancellationTokenSource? TokenSource { get; }

		/// <summary>Transaction instance currently being used by this context</summary>
		private IFdbTransaction? Transaction;
		//note: field accessed via interlocked operations!

		/// <summary>Create a new retry loop operation context</summary>
		/// <param name="db">Database that will be used by the retry loop</param>
		/// <param name="mode">Operation mode of the retry loop</param>
		/// <param name="ct">Optional cancellation token that will abort the retry loop if triggered.</param>
		public FdbOperationContext(IFdbDatabase db, FdbTransactionMode mode, CancellationToken ct)
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

		/// <summary>List of one or more state change callback</summary>
		/// <remarks>Either null, a single Delegate, or an array Delegate[]</remarks>
		private object? StateCallbacks; // interlocked!

		private void RegisterStateCallback(object callback)
		{
			Contract.Requires(callback is Delegate || callback is IHandleTransactionLifecycle);
			lock (this)
			{
				var previous = this.StateCallbacks;
				if (previous == null)
				{ // first handler for this context
					this.StateCallbacks = callback;
				}
				else if (previous is object[] arr)
				{ // one more 
					Array.Resize(ref arr, arr.Length + 1);
					arr[arr.Length - 1] = callback;
					this.StateCallbacks = arr;
				}
				else
				{ // second handler
					this.StateCallbacks = new [] { previous, callback };
				}
			}
		}

		private Task ExecuteHandlers(ref object handlers, FdbOperationContext ctx, FdbTransactionState state)
		{
			var cbk = Interlocked.Exchange(ref handlers, null);
			switch (cbk)
			{
				case null: return Task.CompletedTask;
				case object[] arr: return ExecuteMultipleHandlers(arr, ctx, state, this.Cancellation);
				default: return ExecuteSingleHandler(cbk, ctx, state, this.Cancellation);
			}
		}

		private static Task ExecuteSingleHandler(object del, FdbOperationContext ctx, FdbTransactionState arg, CancellationToken ct)
		{
			switch (del)
			{
				case IHandleTransactionLifecycle htl:
				{
					htl.OnTransactionStateChanged(arg);
					return Task.CompletedTask;
				}
				case Action<FdbOperationContext, FdbTransactionState> act:
				{
					act(ctx, arg);
					return Task.CompletedTask;
				}
				case Func<FdbOperationContext, FdbTransactionState, CancellationToken, Task> fct:
				{
					return fct(ctx, arg, ct);
				}
			}

			throw new NotSupportedException("Unexpected handler delegate type.");
		}

		private static async Task ExecuteMultipleHandlers(object[] arr, FdbOperationContext ctx, FdbTransactionState arg, CancellationToken ct)
		{
			foreach (object del in arr)
			{
				if (del != null) await ExecuteSingleHandler(del, ctx, arg, ct).ConfigureAwait(false);
			}
		}

		/// <summary>Register a callback that will only be called once the transaction has been successfully committed</summary>
		/// <remarks>NOTE: there are _no_ guarantees that the callback will fire at all, so this should only be used for cache updates or idempotent operations!</remarks>
		public void OnSuccess(Action<FdbOperationContext, FdbTransactionState> callback)
		{
			RegisterStateCallback(callback ?? throw new ArgumentNullException(nameof(callback)));
		}

		public void OnSuccess(Func<FdbOperationContext, FdbTransactionState, CancellationToken, Task> callback)
		{
			RegisterStateCallback(callback ?? throw new ArgumentNullException(nameof(callback)));
		}

		#endregion

		#region Local Data...

		// To help build a caching layer on top of transactions, it is necessary to be able to attach cached instance to each transaction
		// The intent is that a layer implementation will use to build a "per-transaction state" on the first call to the layer within this transaction,
		// and then reuse the same instance if the layer is called multiple times within the same transaction.

		// The convention is that each layer uses its own type has the "key" of the cached instances.
		// But since a layer can have multiple "instances" (ex: multiple indexes, multiple queues, ...) each instance is also coupled with a "token" key
		// If a layer can only have a single instance, it can use the example the empty string has its "token" key.
		// If not, the layer can use any type of key, if it can guarantee that it is unique. For exemple: for a record table "Users", the key can be the name
		// itself, IF IT IS NOT POSSIBLE TO HAVE A DIFFERENT "Users" TABLE IN THE APPLICATION! If there can be collisions, then the key should be prefixed by
		// some other "namespace" to make it globally unique.

		/// <summary>Stores contextual data that can be added and retrieved at any step of the transaction's processing</summary>
		/// <remarks>Access to this collection must be performed under lock, because a transaction can be used concurrently from multiple threads!</remarks>
		/// map[ typeof(TState) => map[ TToken => TState ] ]
		private Dictionary<Type, object>? LocalData { get; set; }

		[Pure, ContractAnnotation("createIfMissing:true => notnull"), MethodImpl(MethodImplOptions.AggressiveInlining)]
		private Dictionary<Type, object>? GetLocalDataContainer(bool createIfMissing)
		{
			return this.LocalData ?? GetLocalDataContainerSlow(createIfMissing);
		}

		[ContractAnnotation("createIfMissing:true => notnull"), MethodImpl(MethodImplOptions.NoInlining)]
		private Dictionary<Type, object>? GetLocalDataContainerSlow(bool createIfMissing)
		{
			var container = this.LocalData;
			if (container == null && createIfMissing)
			{
				container = new Dictionary<Type, object>();
				this.LocalData = container;
			}
			return container;
		}

		/// <summary>Clear any local data currently attached on this transaction</summary>
		internal void ClearAllLocalData()
		{
			lock (this)
			{
				var container = GetLocalDataContainer(false);
				if (container != null)
				{
					lock (container)
					{
						container.Clear();
					}
				}
			}
		}

		/// <summary>Set the value of a cached instance attached to the transaction</summary>
		/// <typeparam name="TState">Type of the instance</typeparam>
		/// <typeparam name="TToken">Type of the key used to distinguish multiple instance of the same "type"</typeparam>
		/// <param name="key">Value of the key to remove. If there can be only one instance per <typeparamref name="TState"/>, use a constant such as the <c>string.Empty</c></param>
		/// <param name="newState">New instance that must be attached to the transaction</param>
		/// <returns>If there was already a cached instance for this key, it will be discarded</returns>
		public void SetLocalData<TState, TToken>(TToken key, TState newState)
			where TState : class
		{
			Contract.NotNullAllowStructs(key, nameof(key));
			Contract.NotNull(newState, nameof(newState));
			lock (this)
			{
				var container = GetLocalDataContainer(true)!;
				if (!container.TryGetValue(typeof(TState), out var slot))
				{
					slot = new Dictionary<string, object>(StringComparer.Ordinal);
					container[typeof(TState)] = slot;
				}
				var items = (Dictionary<TToken, TState>) slot;
				items[key] = newState;
			}
		}

		/// <summary>Replace the value of a cached instance attached to the transaction, and return the previous one</summary>
		/// <typeparam name="TState">Type of the instance</typeparam>
		/// <typeparam name="TToken">Type of the key used to distinguish multiple instance of the same "type"</typeparam>
		/// <param name="key">Value of the key to remove. If there can be only one instance per <typeparamref name="TState"/>, use a constant such as the <c>string.Empty</c></param>
		/// <param name="newState">New instance that must be attached to the transaction</param>
		/// <returns>Previous cached instance, or null if none was found.</returns>
		[return: MaybeNull]
		public TState ReplaceLocalData<TState, TToken>(TToken key, TState newState)
			where TState : class
		{
			Contract.NotNullAllowStructs(key, nameof(key));
			Contract.NotNull(newState, nameof(newState));
			lock (this)
			{
				var container = GetLocalDataContainer(true)!;
				if (!container.TryGetValue(typeof(TState), out var slot))
				{
					var items =  new Dictionary<TToken, TState>
					{
						[key] = newState
					};
					container[typeof(TState)] = items;
					return default!;
				}
				else
				{
					var items = (Dictionary<TToken, TState>) slot;
					items.TryGetValue(key, out var previous);
					items[key] = newState;
					return previous;
				}
			}
		}

		/// <summary>Remove a cached instance previously attached to the transaction</summary>
		/// <typeparam name="TState">Type of the instance</typeparam>
		/// <typeparam name="TToken">Type of the key used to distinguish multiple instance of the same "type"</typeparam>
		/// <param name="key">Value of the key to remove. If there can be only one instance per <typeparamref name="TState"/>, use a constant such as the <c>string.Empty</c></param>
		/// <returns>Returns <c>true</c> if the value was found and removed; otherwise, false.</returns>
		public bool RemoveLocalData<TState, TToken>(TToken key) where TState : class
		{
			Contract.NotNullAllowStructs(key, nameof(key));
			lock (this)
			{
				var container = GetLocalDataContainer(false);
				if (container != null && container.TryGetValue(typeof(TState), out var slot))
				{
					var items = (Dictionary<TToken, TState>) slot;
					return items.Remove(key);
				}
				return false;
			}
		}

		/// <summary>Return the corresponding instance attached to the transaction, if it exists.</summary>
		/// <typeparam name="TState">Type of the instance</typeparam>
		/// <typeparam name="TToken">Type of the key used to distinguish multiple instance of the same "type"</typeparam>
		/// <param name="key">Value of the key. If there can be only one instance per <typeparamref name="TState"/>, use a constant such as the <c>string.Empty</c></param>
		/// <param name="state">Receive the value if it was found; otherwise, <c>default(<typeparamref name="TState"/>)</c></param>
		/// <returns>Returns <c>true</c> if the value was found; otherwise, <c>false</c>.</returns>
		[ContractAnnotation("=>false, state:null; =>true, state:notnull")]
		public bool TryGetLocalData<TState, TToken>(TToken key, [NotNullWhen(true)] out TState? state)
			where TState : class
		{
			Contract.NotNullAllowStructs(key, nameof(key));
			lock (this)
			{
				var container = GetLocalDataContainer(false);
				if (container != null && container.TryGetValue(typeof(TState), out var slot))
				{
					var items = (Dictionary<TToken, TState>) slot;
					if (items.TryGetValue(key, out var value))
					{
						state = (TState) value;
						return true;
					}
				}
				state = default!;
				return false;
			}
		}

		/// <summary>Return the corresponding instance attached to the transaction, or use the specified instance no value was found.</summary>
		/// <typeparam name="TState">Type of the instance</typeparam>
		/// <typeparam name="TToken">Type of the key used to distinguish multiple instance of the same "type"</typeparam>
		/// <param name="key">Value of the key. If there can be only one instance per <typeparamref name="TState"/>, use a constant such as the <c>string.Empty</c></param>
		/// <param name="newState">Instance that will be used if no value already exists in this transaction.</param>
		/// <returns>Either the existing value, or <paramref name="newState"/>.</returns>
		public TState GetOrCreateLocalData<TState, TToken>(TToken key, TState newState)
			where TState : class
		{
			Contract.NotNullAllowStructs(key, nameof(key));
			Contract.NotNull(newState, nameof(newState));
			lock (this)
			{
				var container = GetLocalDataContainer(true)!;
				TState result;
				if (container.TryGetValue(typeof(TState), out var slot))
				{
					var items = (Dictionary<TToken, TState>) slot;
					if (!items.TryGetValue(key, out result))
					{
						result = newState;
						items[key] = result;
					}
				}
				else
				{
					result = newState;
					slot = new Dictionary<TToken, TState>
					{
						[key] = result
					};
					container[typeof(TState)] = slot;
				}
				return result;
			}
		}

		/// <summary>Return the corresponding instance attached to the transaction, or invoke the specified factory if it was not already specified.</summary>
		/// <typeparam name="TState">Type of the instance</typeparam>
		/// <typeparam name="TToken">Type of the key used to distinguish multiple instance of the same "type"</typeparam>
		/// <param name="key">Value of the key. If there can be only one instance per <typeparamref name="TState"/>, use a constant such as the <c>string.Empty</c></param>
		/// <param name="factory">Handler called to generate a new <typeparamref name="TState"/> instance if there is no match.</param>
		/// <returns>Either the existing value, or the value returned by invoking <paramref name="factory"/>.</returns>
		//REVIEW: should we return a tuple (TState Data, bool Created) instead ?
		public TState GetOrCreateLocalData<TState, TToken>(TToken key, Func<TState> factory)
			where TState : class
		{
			Contract.NotNullAllowStructs(key, nameof(key));
			Contract.NotNull(factory, nameof(factory));
			lock (this)
			{
				var container = GetLocalDataContainer(true)!;
				TState result;
				if (container.TryGetValue(typeof(TState), out var slot))
				{
					var items = (Dictionary<TToken, TState>) slot;
					if (!items.TryGetValue(key, out result))
					{
						result = factory() ?? throw new InvalidOperationException("Constructed state cannot be null");
						items[key] = result;
					}
				}
				else
				{
					result = factory() ?? throw new InvalidOperationException("Constructed state cannot be null");
					slot = new Dictionary<TToken, TState>
					{
						[key] = result
					};
					container[typeof(TState)] = slot;
				}
				return result;
			}
		}

		#endregion

		#region Value Checkers...

		/// <summary>List of outstanding checks that must match in order for the transaction to commit successfully</summary>
		private List<(Slice Key, Slice ExpectedValue, string Tag, Task<Slice> ActualValue)>? ValueChecks { get; set; }

		/// <summary>If <c>true</c>, then a value check was unsuccessfull during the previous iteration of the retry loop. Defaults to <c>false</c> on the first attempt, and is reset to <c>false</c> if the previous attempt failed for an unrelated reason.</summary>
		/// <remarks>
		/// This can be used by layers wanting to implement deferred cache validation.
		/// The layer implementation can short-circuit any cache validation if this is <c>true</c>, and reload everything from the database.
		/// Please note that this flag is shared by all the layers that were touched by this transaction, so there can be false-positives (a check from layer A can invalidate the cache from layer B).
		/// </remarks>
		public bool HasAtLeastOneFailedValueCheck { get; private set; }

		/// <summary>Test if a value check with the specified tag has failed during the previous attempt</summary>
		/// <param name="tag">Tag that was passed to a call to <see cref="AddValueCheck"/> (or similar overloads) in the previous attempt of this context, that failed (meaning the expected value did not match with the database)</param>
		/// <returns>If <c>true</c> there is a very high probability that the cached key value has changed in the database. If <c>false</c> there is a low probabilty that the value has changed in the database.</returns>
		/// <remarks>
		/// The caller should use this as a hint that any previously cached data is highly likely to be stale, and should be discarded.
		/// Note that this method can fall victim to the ABA pattern, meaning that a subsquent read of the checked key could return the expected value (changed back to its original value by another transaction).
		/// To reduce the chances of ABA, checked keys should only be updated using atomic increment operations, or use versionstamps if possible.
		/// </remarks>
		public FdbValueCheckResult ValueCheckFailedInPreviousAttempt(string tag)
		{
			Dictionary<string, (FdbValueCheckResult Result, List<(FdbValueCheckResult, Slice, Slice, Slice)>)>? checks;
			lock (this)
			{
				checks = this.FailedValueCheckTags;
			}
			return checks != null && checks.TryGetValue(tag, out var check) ? check.Result : FdbValueCheckResult.Unknown;
		}

		/// <summary>Return the list of all value-checks performed in the previous transaction attempt</summary>
		/// <param name="tag">If not-null, only return the checks with the specified tag.</param>
		/// <param name="result">If not-null, only return the checks with the specified result</param>
		/// <returns>List of value-checks that match the specified filters.</returns>
		public List<(string Tag, FdbValueCheckResult Result, Slice Key, Slice Expected, Slice Actual)> ListValueChecksFromPreviousAttempt(string? tag = null, FdbValueCheckResult? result = null)
		{
			Dictionary<string, (FdbValueCheckResult Result, List<(FdbValueCheckResult Result, Slice Key, Slice Expected, Slice Actual)> Checks)>? checks;
			lock (this)
			{
				checks = this.FailedValueCheckTags;
			}

			var res = new List<(string Tag, FdbValueCheckResult Result, Slice Key, Slice Expected, Slice Actual)>();
			if (checks != null)
			{
				foreach (var kv in checks)
				{
					if (tag == null || tag == kv.Key)
					{
						foreach (var item in kv.Value.Checks)
						{
							if (result == null || item.Result == result)
							{
								res.Add((kv.Key, item.Result, item.Key, item.Expected, item.Actual));
							}
						}
					}
				}
			}
			return res;
		}

		/// <summary>Contains the tags of all the failed value checks from the previous attempt</summary>
		private Dictionary<string, (FdbValueCheckResult Result, List<(FdbValueCheckResult Result, Slice Key, Slice Expected, Slice Actual)>)>? FailedValueCheckTags { get; set; }

		/// <summary>Add a check on the value of the key, that will be resolved before the transaction is able to commit</summary>
		/// <param name="tag">Application-provided tag that can be used later to decide which layer failed the check.</param>
		/// <param name="key">Key to check</param>
		/// <param name="expectedValue">Expected value of the key. A value of <see cref="Slice.Nil"/> means the key is expected to NOT exist.</param>
		/// <remarks>
		/// If key does not have the expected value, the transaction will fail to commit, with an <see cref="FdbError.NotCommitted"/> error (simulating a conflict), which should trigger a retry,
		/// and the <see cref="HasAtLeastOneFailedValueCheck"/> property will then be set to <c>true</c>, for the next iteration of the retry-loop. A call to <see cref="ValueCheckFailedInPreviousAttempt"/>(<paramref name="tag"/>) will also return <c>true</c>.
		/// Any change to the value of the key _after_ this call, in the same transaction, will not be seen by this check. Only the value seen by this transaction at call time is considered.
		/// </remarks>
		public void AddValueCheck(string tag, Slice key, Slice expectedValue)
		{
			Contract.NotNullOrEmpty(tag, nameof(tag));

			var tr = this.Transaction;
			if (tr == null) throw new InvalidOperationException();
			var task = tr.GetAsync(key);

			lock (this)
			{
				(this.ValueChecks ??= new List<(Slice, Slice, string, Task<Slice>)>())
					.Add((key, expectedValue, tag, task));
			}
		}

		/// <summary>Add a check on the values of one or more keys, that will be resolved before the transaction is able to commit</summary>
		/// <param name="tag">Application-provided tag that can be used later to decide which layer failed the check.</param>
		/// <param name="items">List of keys to check, and their expected values. A value of <see cref="Slice.Nil"/> means the corresponding key is expected to NOT exist.</param>
		/// <remarks>
		/// If any of the keys does not have the expected value, the transaction will fail to commit, with an <see cref="FdbError.NotCommitted"/> error (simulating a conflict), which should trigger a retry,
		/// and the <see cref="HasAtLeastOneFailedValueCheck"/> property will then be set to <c>true</c>, for the next iteration of the retry-loop.
		/// Any change to the value of these keys _after_ this call, in the same transaction, will not be seen by this check. Only values seen by this transaction at call time are considered.
		/// </remarks>
		public void AddValueChecks(string tag, IEnumerable<KeyValuePair<Slice, Slice>> items)
		{
			Contract.NotNull(items, nameof(items));
			AddValueChecks(tag, (items as KeyValuePair<Slice, Slice>[] ?? items.ToArray()).AsSpan());
		}

		/// <summary>Add a check on the values of one or more keys, that will be resolved before the transaction is able to commit</summary>
		/// <param name="tag">Application-provided tag that can be used later to decide which layer failed the check.</param>
		/// <param name="items">List of keys to check, and their expected values. A value of <see cref="Slice.Nil"/> means the corresponding key is expected to NOT exist.</param>
		/// <remarks>
		/// If any of the keys does not have the expected value, the transaction will fail to commit, with an <see cref="FdbError.NotCommitted"/> error (simulating a conflict), which should trigger a retry,
		/// and the <see cref="HasAtLeastOneFailedValueCheck"/> property will then be set to <c>true</c>, for the next iteration of the retry-loop.
		/// Any change to the value of these keys _after_ this call, in the same transaction, will not be seen by this check. Only values seen by this transaction at call time are considered.
		/// </remarks>
		public void AddValueChecks(string tag, KeyValuePair<Slice, Slice>[] items)
		{
			Contract.NotNull(items, nameof(items));
			AddValueChecks(tag, items.AsSpan());
		}

		/// <summary>Add a check on the values of one or more keys, that will be resolved before the transaction is able to commit</summary>
		/// <param name="tag">Application-provided tag that can be used later to decide which layer failed the check.</param>
		/// <param name="items">List of keys to check, and their expected values. A value of <see cref="Slice.Nil"/> means the corresponding key is expected to NOT exist.</param>
		/// <remarks>
		/// If any of the keys does not have the expected value, the transaction will fail to commit, with an <see cref="FdbError.NotCommitted"/> error (simulating a conflict), which should trigger a retry,
		/// and the <see cref="HasAtLeastOneFailedValueCheck"/> property will then be set to <c>true</c>, for the next iteration of the retry-loop.
		/// Any change to the value of these keys _after_ this call, in the same transaction, will not be seen by this check. Only values seen by this transaction at call time are considered.
		/// </remarks>
		public void AddValueChecks(string tag, ReadOnlySpan<KeyValuePair<Slice, Slice>> items)
		{
			var tr = this.Transaction;
			if (tr == null) throw new InvalidOperationException("Transaction is not in a valid state.");

			// quick paths...
			if (items.Length == 0) return;
			if (items.Length == 1)
			{
				AddValueCheck(tag, items[0].Key, items[0].Value);
				return;
			}

			var taskBuffer = ArrayPool<Task<Slice>>.Shared.Rent(items.Length);
			try
			{
				for (int i = 0; i < items.Length; i++)
				{
					taskBuffer[i] = tr.GetAsync(items[i].Key);
				}

				lock (this)
				{
					var checks = this.ValueChecks ??= new List<(Slice, Slice, string, Task<Slice>)>();
					int capa = checked(checks.Count + items.Length);
					if (capa > checks.Capacity) checks.Capacity = capa;
					for (int i = 0; i < items.Length; i++)
					{
						checks.Add((items[i].Key, items[i].Value, tag, taskBuffer[i]));
					}
				}
			}
			finally
			{
				ArrayPool<Task<Slice>>.Shared.Return(taskBuffer, clearArray: true);
			}
		}

		private bool EnsureValueCheck(string tag, Slice key, Slice expectedValue, Slice actualResult)
		{
			var tags = (this.FailedValueCheckTags ??= new Dictionary<string, (FdbValueCheckResult Result, List<(FdbValueCheckResult, Slice, Slice, Slice)> Checks)>(StringComparer.Ordinal));

			if (!tags.TryGetValue(tag, out (FdbValueCheckResult Result, List<(FdbValueCheckResult Result, Slice Key, Slice Expected, Slice Actual)> Checks) previous))
			{
				previous.Result = FdbValueCheckResult.Success;
				previous.Checks = new List<(FdbValueCheckResult Result, Slice Key, Slice Expected, Slice Actual)>();
			}

			bool pass;
			if (!expectedValue.Equals(actualResult))
			{
				this.HasAtLeastOneFailedValueCheck = true;
				previous.Result = FdbValueCheckResult.Failed;
				previous.Checks.Add((FdbValueCheckResult.Failed, key, expectedValue, actualResult));
				if (this.Transaction?.IsLogged() == true) this.Transaction.Annotate($"Failed value-check '{tag}' for {FdbKey.Dump(key)}: expected {expectedValue:V}, actual {actualResult:V}");
				pass = false;
			}
			else
			{
				previous.Checks.Add((FdbValueCheckResult.Success, key, expectedValue, actualResult));
				pass = true;
			}

			tags[tag] = previous;

			return pass;
		}

		private ValueTask<bool> ValidateValueChecks(bool ignoreFailedTasks)
		{
			List<(Slice, Slice, string, Task<Slice>)>? checks;
			lock (this)
			{
				checks = this.ValueChecks;
				this.ValueChecks = null;
			}

			if (checks == null || checks.Count == 0) return new ValueTask<bool>(true);
			return ValidateValueChecksSlow(checks, ignoreFailedTasks);
		}

		private async ValueTask<bool> ValidateValueChecksSlow(List<(Slice Key, Slice ExpectedValue, string Tag, Task<Slice> ActualValue)> checks, bool ignoreFailedTasks)
		{
			//note: even if it looks like we are sequentially await all tasks, by the time the first one is complete,
			// the rest of the tasks will probably be already completed as well. Anyway, we need to inspect each individual task
			// to check for any failed tasks anyway, so we can't use Task.WhenAll(...) here

			bool pass = true;
			foreach (var check in checks)
			{
				Slice result;
				try
				{
					result = await check.ActualValue.ConfigureAwait(false);
				}
				catch (FdbException) when (ignoreFailedTasks)
				{
					continue;
				}
				pass &= EnsureValueCheck(check.Tag, check.Key, check.ExpectedValue, result);
			}

			return pass;
		}

		[MethodImpl(MethodImplOptions.NoInlining)]
		private static Exception FailValueCheck()
		{
			return new FdbException(FdbError.NotCommitted, "The value of the a key in the database did not match the expected value.");
		}

		#endregion

		/// <summary>Execute a retry loop on this context</summary>
		internal static async Task<TResult> ExecuteInternal<TState, TIntermediate, TResult>(FdbOperationContext context, [AllowNull] TState state, Delegate handler, Delegate? success)
		{
			Contract.Requires(context != null && handler != null && context.Shared);

			var db = context.Database;
			Contract.Requires(db != null);

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

				TResult result = default!;

				using (var trans = await db.BeginTransactionAsync(context.Mode, CancellationToken.None, context))
				{
					//note: trans may be different from context.Transaction if it has been filtered!
					Contract.Assert(context.Transaction != null);

					while (!context.Committed && !context.Cancellation.IsCancellationRequested)
					{
						bool hasResult = false;
						bool hasRunValueChecks = false;
						result = default!;

						try
						{
							TIntermediate intermediate;

							// call the user provided lambda
							switch (handler)
							{
								#region Read Only...

								#region With State...

								case Func<IFdbReadOnlyTransaction, TState, Task<TResult>> f:
								{
									intermediate = default!;
									result = await f(trans, state!).ConfigureAwait(false);
									hasResult = true;
									break;
								}
								case Func<IFdbReadOnlyTransaction, TState, ValueTask<TResult>> f:
								{
									intermediate = default!;
									result = await f(trans, state!).ConfigureAwait(false);
									hasResult = true;
									break;
								}
								case Func<IFdbReadOnlyTransaction, TState, TResult> f:
								{
									intermediate = default!;
									result = f(trans, state!);
									hasResult = true;
									break;
								}
								case Func<IFdbReadOnlyTransaction, TState, Task<TIntermediate>> f:
								{
									intermediate = await f(trans, state!).ConfigureAwait(false);
									break;
								}
								case Func<IFdbReadOnlyTransaction, TState, ValueTask<TIntermediate>> f:
								{
									intermediate = await f(trans, state!).ConfigureAwait(false);
									break;
								}
								case Func<IFdbReadOnlyTransaction, TState, TIntermediate> f:
								{
									intermediate = f(trans, state!);
									break;
								}
								case Func<IFdbReadOnlyTransaction, TState, Task> f:
								{
									intermediate = default!;
									await f(trans, state!).ConfigureAwait(false);
									break;
								}
								case Func<IFdbReadOnlyTransaction, TState, ValueTask> f:
								{
									intermediate = default!;
									await f(trans, state!).ConfigureAwait(false);
									break;
								}
								case Action<IFdbReadOnlyTransaction, TState> a:
								{
									intermediate = default!;
									a(trans, state!);
									break;
								}

								#endregion

								#region w/o State...

								case Func<IFdbReadOnlyTransaction, Task<TResult>> f:
								{
									intermediate = default!;
									result = await f(trans).ConfigureAwait(false);
									hasResult = true;
									break;
								}
								case Func<IFdbReadOnlyTransaction, ValueTask<TResult>> f:
								{
									intermediate = default!;
									result = await f(trans).ConfigureAwait(false);
									hasResult = true;
									break;
								}
								case Func<IFdbReadOnlyTransaction, TResult> f:
								{
									intermediate = default!;
									result = f(trans);
									hasResult = true;
									break;
								}
								case Func<IFdbReadOnlyTransaction, Task<TIntermediate>> f:
								{
									intermediate = await f(trans).ConfigureAwait(false);
									break;
								}
								case Func<IFdbReadOnlyTransaction, ValueTask<TIntermediate>> f:
								{
									intermediate = await f(trans).ConfigureAwait(false);
									break;
								}
								case Func<IFdbReadOnlyTransaction, TIntermediate> f:
								{
									intermediate = f(trans);
									break;
								}
								case Func<IFdbReadOnlyTransaction, Task> f:
								{
									intermediate = default!;
									await f(trans).ConfigureAwait(false);
									break;
								}
								case Func<IFdbReadOnlyTransaction, ValueTask> f:
								{
									intermediate = default!;
									await f(trans).ConfigureAwait(false);
									break;
								}
								case Action<IFdbReadOnlyTransaction> a:
								{
									intermediate = default!;
									a(trans);
									break;
								}

								#endregion

								#endregion

								#region Read/Write...

								#region w/o state...

								case Func<IFdbTransaction, Task<TResult>> f:
								{
									intermediate = default!;
									result = await f(trans).ConfigureAwait(false);
									hasResult = true;
									break;
								}
								case Func<IFdbTransaction, ValueTask<TResult>> f:
								{
									intermediate = default!;
									result = await f(trans).ConfigureAwait(false);
									hasResult = true;
									break;
								}
								case Func<IFdbTransaction, Task<TIntermediate>> f:
								{
									intermediate = await f(trans).ConfigureAwait(false);
									break;
								}
								case Func<IFdbTransaction, ValueTask<TIntermediate>> f:
								{
									intermediate = await f(trans).ConfigureAwait(false);
									break;
								}
								case Func<IFdbTransaction, Task> f:
								{
									intermediate = default!;
									await f(trans).ConfigureAwait(false);
									break;
								}
								case Func<IFdbTransaction, ValueTask> f:
								{
									intermediate = default!;
									await f(trans).ConfigureAwait(false);
									break;
								}
								case Func<IFdbTransaction, TIntermediate> f:
								{
									intermediate = f(trans);
									break;
								}
								case Func<IFdbTransaction, TResult> f:
								{
									intermediate = default!;
									result = f(trans);
									hasResult = true;
									break;
								}
								case Action<IFdbTransaction> a:
								{
									intermediate = default!;
									a(trans);
									break;
								}

								#endregion

								#region With state...

								case Func<IFdbTransaction, TState, Task<TIntermediate>> f:
								{
									intermediate = await f(trans, state!).ConfigureAwait(false);
									break;
								}
								case Func<IFdbTransaction, TState, ValueTask<TIntermediate>> f:
								{
									intermediate = await f(trans, state!).ConfigureAwait(false);
									break;
								}
								case Func<IFdbTransaction, TState, Task<TResult>> f:
								{
									intermediate = default!;
									result = await f(trans, state!).ConfigureAwait(false);
									hasResult = true;
									break;
								}
								case Func<IFdbTransaction, TState, ValueTask<TResult>> f:
								{
									intermediate = default!;
									result = await f(trans, state!).ConfigureAwait(false);
									hasResult = true;
									break;
								}
								case Func<IFdbTransaction, TState, Task> f:
								{
									intermediate = default!;
									await f(trans, state!).ConfigureAwait(false);
									break;
								}
								case Func<IFdbTransaction, TState, ValueTask> f:
								{
									intermediate = default!;
									await f(trans, state!).ConfigureAwait(false);
									break;
								}
								case Func<IFdbTransaction, TState, TIntermediate> f:
								{
									intermediate = f(trans, state!);
									break;
								}
								case Func<IFdbTransaction, TState, TResult> f:
								{
									intermediate = default!;
									result = f(trans, state!);
									hasResult = true;
									break;
								}
								case Action<IFdbTransaction, TState> a:
								{
									intermediate = default!;
									a(trans, state!);
									break;
								}

								#endregion

								#endregion

								default:
								{
									throw new NotSupportedException($"Cannot execute handlers of type {handler.GetType().Name}");
								}
							}

							if (context.Abort)
							{
								break;
							}

							// make sure any pending value checks are completed and match the expected value!
							context.FailedValueCheckTags?.Clear();
							context.HasAtLeastOneFailedValueCheck = false;
							hasRunValueChecks = true;
							if (!await context.ValidateValueChecks(ignoreFailedTasks: false))
							{
								context.HasAtLeastOneFailedValueCheck = true;
								throw FailValueCheck();
							}

							if (!trans.IsReadOnly)
							{ // commit the transaction
								await trans.CommitAsync().ConfigureAwait(false);
							}

							// we are done
							context.Committed = true;
							context.PreviousError = FdbError.Success;

							// execute any state callbacks, if there are any
							if (context.StateCallbacks != null)
							{
								await context.ExecuteHandlers(ref context.StateCallbacks, context, FdbTransactionState.Commit).ConfigureAwait(false);
							}

							// execute any final logic, if there is any
							if (success != null)
							{
								// if TIntermediate == TResult, the order of delegate resolution may be unstable
								// => we will copy the result in both fields!
								if (hasResult && typeof(TIntermediate) == typeof(TResult))
								{
									intermediate = (TIntermediate) (object?) result;
								}

								switch (success)
								{
									#region Read Only...

									case Func<IFdbReadOnlyTransaction, TIntermediate, Task<TResult>> f:
									{
										result = await f(trans, intermediate!);
										hasResult = true;
										break;
									}
									case Func<IFdbReadOnlyTransaction, TIntermediate, ValueTask<TResult>> f:
									{
										result = await f(trans, intermediate!);
										hasResult = true;
										break;
									}
									case Func<IFdbReadOnlyTransaction, TResult, Task> f:
									{
										if (!hasResult) throw new ArgumentException("Success handler requires the result to be computed by the loop handler.", nameof(success));
										await f(trans, result);
										break;
									}
									case Func<IFdbReadOnlyTransaction, TResult, ValueTask> f:
									{
										if (!hasResult) throw new ArgumentException("Success handler requires the result to be computed by the loop handler.", nameof(success));
										await f(trans, result);
										break;
									}
									case Func<IFdbReadOnlyTransaction, TIntermediate, TResult> f:
									{
										result = f(trans, intermediate!);
										hasResult = true;
										break;
									}
									case Func<IFdbReadOnlyTransaction, Task<TResult>> f:
									{
										result = await f(trans);
										hasResult = true;
										break;
									}
									case Func<IFdbReadOnlyTransaction, ValueTask<TResult>> f:
									{
										result = await f(trans);
										hasResult = true;
										break;
									}
									case Func<IFdbReadOnlyTransaction, Task> f:
									{
										await f(trans);
										result = default!;
										hasResult = true;
										break;
									}
									case Func<IFdbReadOnlyTransaction, ValueTask> f:
									{
										await f(trans);
										result = default!;
										hasResult = true;
										break;
									}
									case Func<IFdbReadOnlyTransaction, TResult> f:
									{
										result = f(trans);
										hasResult = true;
										break;
									}
									case Action<IFdbReadOnlyTransaction, TResult> a:
									{
										if (!hasResult) throw new ArgumentException("Success handler requires the result to be computed by the loop handler.", nameof(success));
										a(trans, result);
										break;
									}
									case Action<IFdbReadOnlyTransaction> a:
									{
										a(trans);
										break;
									}

									#endregion

									#region Read/Write...

									case Func<IFdbTransaction, TIntermediate, Task<TResult>> f:
									{
										result = await f(trans, intermediate!);
										hasResult = true;
										break;
									}
									case Func<IFdbTransaction, TIntermediate, ValueTask<TResult>> f:
									{
										result = await f(trans, intermediate!);
										hasResult = true;
										break;
									}
									case Func<IFdbTransaction, TResult, Task> f:
									{
										if (!hasResult) throw new ArgumentException("Success handler requires the result to be computed by the loop handler.", nameof(success));
										await f(trans, result);
										break;
									}
									case Func<IFdbTransaction, TResult, ValueTask> f:
									{
										if (!hasResult) throw new ArgumentException("Success handler requires the result to be computed by the loop handler.", nameof(success));
										await f(trans, result);
										break;
									}
									case Func<IFdbTransaction, TIntermediate, TResult> f:
									{
										result = f(trans, intermediate!);
										hasResult = true;
										break;
									}
									case Func<IFdbTransaction, Task<TResult>> f:
									{
										result = await f(trans);
										hasResult = true;
										break;
									}
									case Func<IFdbTransaction, ValueTask<TResult>> f:
									{
										result = await f(trans);
										hasResult = true;
										break;
									}
									case Func<IFdbTransaction, Task> f:
									{
										await f(trans);
										result = default!;
										hasResult = true;
										break;
									}
									case Func<IFdbTransaction, ValueTask> f:
									{
										await f(trans);
										result = default!;
										hasResult = true;
										break;
									}
									case Func<IFdbTransaction, TResult> f:
									{
										result = f(trans);
										hasResult = true;
										break;
									}
									case Action<IFdbTransaction, TResult> a:
									{
										if (!hasResult) throw new ArgumentException("Success handler requires the result to be computed by the loop handler.", nameof(success));
										a(trans, result);
										break;
									}
									case Action<IFdbTransaction> a:
									{
										a(trans);
										break;
									}

									#endregion

									default:
									{
										throw new NotSupportedException($"Cannot execute completion handler of type {handler.GetType().Name}");
									}
								}
							}

							if (!hasResult)
							{
								if (typeof(TResult) == typeof(TIntermediate))
								{
									result = (TResult) (object?) intermediate;
								}
								else
								{
									throw new ArgumentException($"Success handler is required to convert intermediate type {typeof(TIntermediate).Name} into result type {typeof(TResult).Name}.");
								
								}
							}

						}
						catch (FdbException e)
						{
							context.PreviousError = e.Code;

							// execute any state callbacks, if there are any
							if (context.StateCallbacks != null)
							{
								await context.ExecuteHandlers(ref context.StateCallbacks, context, FdbTransactionState.Faulted).ConfigureAwait(false);
							}

							if (Logging.On && Logging.IsVerbose) Logging.Verbose(string.Format(CultureInfo.InvariantCulture, "fdb: transaction {0} failed with error code {1}", trans.Id, e.Code));

							if (!hasRunValueChecks)
							{ // we need to resolve any check that would still have passed before the error

								context.FailedValueCheckTags?.Clear();
								context.HasAtLeastOneFailedValueCheck = false;
								if (!await context.ValidateValueChecks(ignoreFailedTasks: true))
								{
									context.HasAtLeastOneFailedValueCheck = true;
									// we don't override the original error, though
								}
							}

							bool shouldRethrow = false;
							try
							{
								await trans.OnErrorAsync(e.Code).ConfigureAwait(false);
							}
							catch (FdbException e2)
							{
								if (Logging.On && Logging.IsError) Logging.Error(string.Format(CultureInfo.InvariantCulture, "fdb: transaction {0} failed with un-retryable error code {1}", trans.Id, e.Code));
								// if the code is the same, we prefer re-throwing the original exception to keep the stacktrace intact!
								if (e2.Code != e.Code) throw;
								shouldRethrow = true;
							}

							// re-throw original exception because it is not retryable.
							if (shouldRethrow) throw;

							if (Logging.On && Logging.IsVerbose) Logging.Verbose(string.Format(CultureInfo.InvariantCulture, "fdb: transaction {0} can be safely retried", trans.Id));
						}
						catch (Exception e)
						{
							context.PreviousError = FdbError.UnknownError;
							if (context.Transaction.IsLogged()) context.Transaction.Annotate($"Handler failed with error: [{e.GetType().Name}] {e.Message}");

							// execute any state callbacks, if there are any
							if (context.StateCallbacks != null)
							{
								await context.ExecuteHandlers(ref context.StateCallbacks, context, FdbTransactionState.Faulted).ConfigureAwait(false);
							}

							// if we have pending value-checks, we have to run them !
							// => the exception thrown by the handler may be caused because of an invalid assumption on the value of a key,
							// and we HAVE to run the handler again, to give a change to the layer code to realize that it needs to update any cached data
							//REVIEW: TODO: how can we distinguish between errors that are caused by the bad assumption of a failed value check, and errors that are completely unrelated?

							bool shouldThrow = true;
							if (!hasRunValueChecks)
							{
								context.FailedValueCheckTags?.Clear();
								context.HasAtLeastOneFailedValueCheck = false;
								if (!await context.ValidateValueChecks(ignoreFailedTasks: false))
								{
									context.HasAtLeastOneFailedValueCheck = true;

									try
									{
										await trans.OnErrorAsync(FdbError.NotCommitted).ConfigureAwait(false);
									}
									catch (FdbException e2)
									{
										if (context.Transaction?.IsLogged() == true) context.Transaction.Annotate("Handler failure MAY be due to a failed value-check, but no more attempt is possible!");
										if (e2.Code != FdbError.NotCommitted) throw;
									}
									shouldThrow = false;
									// note: technically we are after the "OnError" so any new comment will be seen as part of the next attempt..
									if (context.Transaction?.IsLogged() == true) context.Transaction.Annotate($"Previous attempt failed because of the following failed value-check(s): {string.Join(", ", context.FailedValueCheckTags.Where(x => x.Value.Result == FdbValueCheckResult.Failed).Select(x => x.Key))}");
								}
							}

							if (shouldThrow) throw;
						}

						// update the base time for the next attempt
						context.BaseDuration = context.ElapsedTotal;
						context.Retries++;
					}
				}
				context.Cancellation.ThrowIfCancellationRequested();

				if (context.Abort)
				{
					// execute any state callbacks, if there are any
					if (context.StateCallbacks != null)
					{
						await context.ExecuteHandlers(ref context.StateCallbacks, context, FdbTransactionState.Aborted).ConfigureAwait(false);
					}

					throw new OperationCanceledException(context.Cancellation);
				}

				return result!;

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

		internal void AttachTransaction(IFdbTransaction trans)
		{
			if (Interlocked.CompareExchange(ref this.Transaction, trans, null) != null)
			{
				throw new InvalidOperationException("Cannot attach another transaction to this context because another transaction is still attache");
			}
		}

		internal void ReleaseTransaction(IFdbTransaction trans)
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

		public static Task<TResult> RunReadAsync<TState, TIntermediate, TResult>(IFdbDatabase db, TState state, Func<IFdbReadOnlyTransaction, TState, Task<TIntermediate>> handler, Func<TIntermediate, Task<TResult>> success, CancellationToken ct)
		{
			Contract.NotNull(db, nameof(db));
			Contract.NotNull(handler, nameof(handler));
			if (ct.IsCancellationRequested) return Task.FromCanceled<TResult>(ct);

			var context = new FdbOperationContext(db, FdbTransactionMode.ReadOnly | FdbTransactionMode.InsideRetryLoop, ct);
			return ExecuteInternal<TState, TIntermediate, TResult>(context, state, handler, success);
		}

#if REFACTORED

		/// <summary>Run a read-only operation until it succeeds, timeouts, or fails with a non retry-able error</summary>
		[Obsolete("Will be removed soon.")]
		public static Task RunReadAsync(IFdbDatabase db, Func<IFdbReadOnlyTransaction, Task> handler, CancellationToken ct)
		{
			Contract.NotNull(db, nameof(db));
			Contract.NotNull(handler, nameof(handler));
			if (ct.IsCancellationRequested) return Task.FromCanceled(ct);

			var context = new FdbOperationContext(db, FdbTransactionMode.ReadOnly | FdbTransactionMode.InsideRetryLoop, ct);
			return ExecuteInternal(context, handler, null);
		}

		/// <summary>Run a read-only operation until it succeeds, timeouts, or fails with a non retry-able error</summary>
		public static async Task<TResult> RunReadWithResultAsync<TResult>(IFdbDatabase db, Func<IFdbReadOnlyTransaction, Task<TResult>> handler, CancellationToken ct)
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
		public static async Task RunReadWithResultAsync(IFdbDatabase db, Func<IFdbReadOnlyTransaction, Task> handler, CancellationToken ct)
		{
			Contract.NotNull(db, nameof(db));
			Contract.NotNull(handler, nameof(handler));
			ct.ThrowIfCancellationRequested();

			var context = new FdbOperationContext(db, FdbTransactionMode.ReadOnly | FdbTransactionMode.InsideRetryLoop, ct);
			await ExecuteInternal(context, handler, null).ConfigureAwait(false);
		}

		/// <summary>Run a read-only operation until it succeeds, timeouts, or fails with a non retry-able error</summary>
		public static async Task<TResult> RunReadWithResultAsync<TResult>(IFdbDatabase db, Func<IFdbReadOnlyTransaction, ValueTask<TResult>> handler, CancellationToken ct)
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
		public static async Task<TResult> RunReadWithResultAsync<TResult>(IFdbDatabase db, Func<IFdbReadOnlyTransaction, Task<TResult>> handler, Action<IFdbReadOnlyTransaction, TResult> success, CancellationToken ct)
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
		public static async Task<TResult> RunReadWithResultAsync<TResult>(IFdbDatabase db, Func<IFdbReadOnlyTransaction, ValueTask<TResult>> handler, Action<IFdbReadOnlyTransaction, TResult> success, CancellationToken ct)
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
		public static async Task<TResult> RunReadWithResultAsync<TIntermediate, TResult>(IFdbDatabase db, Func<IFdbReadOnlyTransaction, Task<TIntermediate>> handler, Func<IFdbReadOnlyTransaction, TIntermediate, TResult> success, CancellationToken ct)
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
		public static async Task<TResult> RunReadWithResultAsync<TIntermediate, TResult>(IFdbDatabase db, Func<IFdbReadOnlyTransaction, Task<TIntermediate>> handler, Func<IFdbReadOnlyTransaction, TIntermediate, Task<TResult>> success, CancellationToken ct)
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

#endif

		#endregion

		#region Read/Write operations...

		public static Task<TResult> RunReadWriteAsync<TState, TIntermediate, TResult>(IFdbDatabase db, TState state, Func<IFdbTransaction, TState, TIntermediate> handler, Func<TIntermediate, Task<TResult>> success, CancellationToken ct)
		{
			Contract.NotNull(db, nameof(db));
			Contract.NotNull(handler, nameof(handler));
			if (ct.IsCancellationRequested) return Task.FromCanceled<TResult>(ct);

			var context = new FdbOperationContext(db, FdbTransactionMode.Default | FdbTransactionMode.InsideRetryLoop, ct);
			return ExecuteInternal<TState, TIntermediate, TResult>(context, state, handler, success);
		}

		public static Task<TResult> RunWriteAsync<TState, TIntermediate, TResult>(IFdbDatabase db, TState state, Func<IFdbTransaction, TState, Task<TIntermediate>> handler, Func<TState, TIntermediate, Task<TResult>> success, CancellationToken ct)
		{
			Contract.NotNull(db, nameof(db));
			Contract.NotNull(handler, nameof(handler));
			if (ct.IsCancellationRequested) return Task.FromCanceled<TResult>(ct);

			var context = new FdbOperationContext(db, FdbTransactionMode.Default | FdbTransactionMode.InsideRetryLoop, ct);
			return ExecuteInternal<TState, TIntermediate, TResult>(context, state, handler, success);
		}

#if REFACTORED

		/// <summary>Run a write operation until it succeeds, timeouts, or fails with a non retry-able error</summary>
		public static Task RunWriteAsync(IFdbDatabase db, Action<IFdbTransaction> handler, CancellationToken ct)
		{
			Contract.NotNull(db, nameof(db));
			Contract.NotNull(handler, nameof(handler));
			if (ct.IsCancellationRequested) return Task.FromCanceled(ct);

			var context = new FdbOperationContext(db, FdbTransactionMode.Default | FdbTransactionMode.InsideRetryLoop, ct);
			return ExecuteInternal(context, handler, null);
		}

		/// <summary>Run a read/write operation until it succeeds, timeouts, or fails a with non retry-able error</summary>
		public static Task RunWriteAsync(IFdbDatabase db, Func<IFdbTransaction, Task> handler, CancellationToken ct)
		{
			Contract.NotNull(db, nameof(db));
			Contract.NotNull(handler, nameof(handler));
			if (ct.IsCancellationRequested) return Task.FromCanceled(ct);

			var context = new FdbOperationContext(db, FdbTransactionMode.Default | FdbTransactionMode.InsideRetryLoop, ct);
			return ExecuteInternal(context, handler, null);
		}

		/// <summary>Run a write operation until it succeeds, timeouts, or fails with a non retry-able error</summary>
		public static Task RunWriteAsync(IFdbDatabase db, Action<IFdbTransaction> handler, Action<IFdbTransaction> success, CancellationToken ct)
		{
			Contract.NotNull(db, nameof(db));
			Contract.NotNull(handler, nameof(handler));
			Contract.NotNull(success, nameof(success));
			if (ct.IsCancellationRequested) return Task.FromCanceled(ct);

			var context = new FdbOperationContext(db, FdbTransactionMode.Default | FdbTransactionMode.InsideRetryLoop, ct);
			return ExecuteInternal(context, handler, success);
		}

		/// <summary>Run a write operation until it succeeds, timeouts, or fails with a non retry-able error</summary>
		public static Task RunWriteAsync(IFdbDatabase db, Action<IFdbTransaction> handler, Func<IFdbTransaction, Task> success, CancellationToken ct)
		{
			Contract.NotNull(db, nameof(db));
			Contract.NotNull(handler, nameof(handler));
			Contract.NotNull(success, nameof(success));
			if (ct.IsCancellationRequested) return Task.FromCanceled(ct);

			var context = new FdbOperationContext(db, FdbTransactionMode.Default | FdbTransactionMode.InsideRetryLoop, ct);
			return ExecuteInternal(context, handler, success);
		}

		/// <summary>Run a write operation until it succeeds, timeouts, or fails with a non retry-able error</summary>
		public static async Task<TResult> RunWriteAsync<TResult>(IFdbDatabase db, Action<IFdbTransaction> handler, Func<IFdbTransaction, TResult> success, CancellationToken ct)
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
		public static Task RunWriteAsync(IFdbDatabase db, Func<IFdbTransaction, Task> handler, Action<IFdbTransaction> success, CancellationToken ct)
		{
			Contract.NotNull(db, nameof(db));
			Contract.NotNull(handler, nameof(handler));
			Contract.NotNull(success, nameof(success));
			if (ct.IsCancellationRequested) return Task.FromCanceled(ct);

			var context = new FdbOperationContext(db, FdbTransactionMode.Default | FdbTransactionMode.InsideRetryLoop, ct);
			return ExecuteInternal(context, handler, success);
		}

		/// <summary>Run a read/write operation until it succeeds, timeouts, or fails with a non retry-able error</summary>
		public static Task RunWriteAsync(IFdbDatabase db, Func<IFdbTransaction, Task> handler, Func<IFdbTransaction, Task> success, CancellationToken ct)
		{
			Contract.NotNull(db, nameof(db));
			Contract.NotNull(handler, nameof(handler));
			Contract.NotNull(success, nameof(success));
			if (ct.IsCancellationRequested) return Task.FromCanceled(ct);

			var context = new FdbOperationContext(db, FdbTransactionMode.Default | FdbTransactionMode.InsideRetryLoop, ct);
			return ExecuteInternal(context, handler, success);
		}

		/// <summary>Run a read/write operation until it succeeds, timeouts, or fails with a non retry-able error</summary>
		public static async Task<TResult> RunWriteWithResultAsync<TResult>(IFdbDatabase db, Func<IFdbTransaction, TResult> handler, CancellationToken ct)
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
		public static async Task<TResult> RunWriteWithResultAsync<TResult>(IFdbDatabase db, Func<IFdbTransaction, Task<TResult>> handler, CancellationToken ct)
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
		public static async Task<TResult> RunWriteWithResultAsync<TResult>(IFdbDatabase db, Func<IFdbTransaction, Task<TResult>> handler, Action<IFdbTransaction, TResult> success, CancellationToken ct)
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
		public static async Task<TResult> RunWriteWithResultAsync<TResult>(IFdbDatabase db, Func<IFdbTransaction, Task> handler, Func<IFdbTransaction, Task<TResult>> success, CancellationToken ct)
		{
			Contract.NotNull(db, nameof(db));
			Contract.NotNull(handler, nameof(handler));
			Contract.NotNull(success, nameof(success));
			ct.ThrowIfCancellationRequested();

			TResult result = default!;
			async Task Complete(IFdbTransaction tr)
			{
				result = await success(tr);
			}

			var context = new FdbOperationContext(db, FdbTransactionMode.Default | FdbTransactionMode.InsideRetryLoop, ct);
			await ExecuteInternal(context, handler, (Func<IFdbTransaction, Task>) Complete).ConfigureAwait(false);
			return result;
		}

		/// <summary>Run a read/write operation until it succeeds, timeouts, or fails with a non retry-able error</summary>
		public static async Task<TResult> RunWriteWithResultAsync<TResult>(IFdbDatabase db, Func<IFdbTransaction, Task> handler, Func<IFdbTransaction, TResult> success, CancellationToken ct)
		{
			Contract.NotNull(db, nameof(db));
			Contract.NotNull(handler, nameof(handler));
			Contract.NotNull(success, nameof(success));
			ct.ThrowIfCancellationRequested();

			TResult result = default!;
			void Complete(IFdbTransaction tr)
			{
				result = success(tr);
			}

			var context = new FdbOperationContext(db, FdbTransactionMode.Default | FdbTransactionMode.InsideRetryLoop, ct);
			await ExecuteInternal(context, handler, (Action<IFdbTransaction>) Complete).ConfigureAwait(false);
			return result;
		}

		/// <summary>Run a read/write operation until it succeeds, timeouts, or fails with a non retry-able error</summary>
		public static async Task<TResult> RunWriteWithResultAsync<TIntermediate, TResult>(IFdbDatabase db, Func<IFdbTransaction, Task<TIntermediate>> handler, Func<IFdbTransaction, TIntermediate, TResult> success, CancellationToken ct)
		{
			Contract.NotNull(db, nameof(db));
			Contract.NotNull(handler, nameof(handler));
			Contract.NotNull(success, nameof(success));
			ct.ThrowIfCancellationRequested();

			TIntermediate tmp = default!;
			async Task Handler(IFdbTransaction tr)
			{
				tmp = await handler(tr).ConfigureAwait(false);
			}

			TResult result = default!;
			void Complete(IFdbTransaction tr)
			{
				result = success(tr, tmp);
			}

			var context = new FdbOperationContext(db, FdbTransactionMode.Default | FdbTransactionMode.InsideRetryLoop, ct);
			await ExecuteInternal(context, (Func<IFdbTransaction, Task>) Handler, (Action<IFdbTransaction>) Complete).ConfigureAwait(false);
			return result;
		}

		/// <summary>Run a read/write operation until it succeeds, timeouts, or fails with a non retry-able error</summary>
		public static async Task<TResult> RunWriteWithResultAsync<TIntermediate, TResult>(IFdbDatabase db, Func<IFdbTransaction, Task<TIntermediate>> handler, Func<IFdbTransaction, TIntermediate, Task<TResult>> success, CancellationToken ct)
		{
			Contract.NotNull(db, nameof(db));
			Contract.NotNull(handler, nameof(handler));
			Contract.NotNull(success, nameof(success));
			ct.ThrowIfCancellationRequested();

			TIntermediate tmp = default!;
			async Task Handler(IFdbTransaction tr)
			{
				tmp = await handler(tr).ConfigureAwait(false);
			}

			TResult result = default!;
			async Task Complete(IFdbTransaction tr)
			{
				result = await success(tr, tmp).ConfigureAwait(false);
			}

			var context = new FdbOperationContext(db, FdbTransactionMode.Default | FdbTransactionMode.InsideRetryLoop, ct);
			await ExecuteInternal(context, (Func<IFdbTransaction, Task>) Handler, (Func<IFdbTransaction, Task>) Complete).ConfigureAwait(false);
			return result;
		}

#endif

		#endregion

	}
 
	/// <summary>Marker interface for objects that need to respond to transaction state change (commit, failure, ...)</summary>
	public interface IHandleTransactionLifecycle
	{
		/// <summary>Called when the transaction state changes (reset, commit, rollback, ...)</summary>
		/// <param name="state">Reason for the state change</param>
		void OnTransactionStateChanged(FdbTransactionState state);

	}

	public enum FdbTransactionState
	{
		/// <summary>The last execution of the handler failed</summary>
		Faulted,
		/// <summary>The last execution of the handler successfully committed the transaction</summary>
		Commit,
		/// <summary>The last execution of the handler was aborted</summary>
		Aborted,
	}

	public enum FdbValueCheckResult
	{
		/// <summary>There was no value-check performed with this tag in the previous attempt.</summary>
		Unknown = 0,
		/// <summary>All value-checks performed with this tag passed in the previous attempt.</summary>
		Success = 1,
		/// <summary>At least one value-check performed with this tag failed in the previous attempt.</summary>
		Failed = 2,
	}


}
