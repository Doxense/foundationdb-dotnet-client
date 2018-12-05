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
	using System.Runtime.CompilerServices;
	using System.Threading;
	using System.Threading.Tasks;
	using Doxense.Diagnostics.Contracts;
	using FoundationDB.DependencyInjection;
	using FoundationDB.Layers.Directories;
	using JetBrains.Annotations;

	[PublicAPI]
	public static class FdbDatabaseProviderExtensions
	{

		/// <summary>Convert this database instance into a <see cref="IFdbDatabaseScopeProvider">provider</see></summary>
		[Pure, NotNull]
		public static IFdbDatabaseScopeProvider AsDatabaseProvider([NotNull] this IFdbDatabase db)
		{
			return Fdb.CreateRootScope(db);
		}

		/// <summary>Create a scope that will execute some initialization logic before the first transaction is allowed to run</summary>
		/// <param name="db">Parent provider</param>
		/// <param name="init">Handler that must run successfully once before allowing transactions on this scope</param>
		/// <param name="lifetime">Optional cancellation token that can be used to externally abort the new scope</param>
		[Pure, NotNull, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static IFdbDatabaseScopeProvider<TState> CreateRootScope<TState>(
			[NotNull] this IFdbDatabase db,
			[NotNull] Func<IFdbDatabase, CancellationToken, Task<(IFdbDatabase Db, TState state)>> init,
			CancellationToken lifetime = default
		)
		{
			return Fdb.CreateRootScope(db, init, lifetime);
		}

		/// <summary>Create a scope that will execute some initialization logic before the first transaction is allowed to run</summary>
		/// <param name="provider">Parent provider</param>
		/// <param name="init">Handler that must run successfully once before allowing transactions on this scope</param>
		/// <param name="lifetime">Optional cancellation token that can be used to externally abort the new scope</param>
		[Pure, NotNull, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static IFdbDatabaseScopeProvider CreateScope(
			[NotNull] this IFdbDatabaseScopeProvider provider,
			[NotNull] Func<IFdbDatabase, CancellationToken, Task> init,
			CancellationToken lifetime = default
		)
		{
			return Fdb.CreateScope(provider, init, lifetime);
		}

		/// <summary>Create a scope that will execute some initialization logic before the first transaction is allowed to run</summary>
		/// <param name="db">Parent database</param>
		/// <param name="init">Handler that must run successfully once before allowing transactions on this scope</param>
		/// <param name="lifetime">Optional cancellation token that can be used to externally abort the new scope</param>
		[Pure, NotNull, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static IFdbDatabaseScopeProvider CreateRootScope(
			[NotNull] this IFdbDatabase db,
			[NotNull] Func<IFdbDatabase, CancellationToken, Task> init,
			CancellationToken lifetime = default
		)
		{
			return Fdb.CreateRootScope(db, init, lifetime);
		}

		/// <summary>Create a scope that will provider a directory subspace to all transactions</summary>
		/// <param name="provider">Parent provider</param>
		/// <param name="path">Path of the directory subspace that will be open (and created if necessary) for all the transactions started from this scope.</param>
		/// <param name="lifetime">Optional cancellation token that can be used to externally abort the new scope</param>
		[Pure, NotNull]
		public static IFdbDatabaseScopeProvider<FdbDirectorySubspace> CreateDirectoryScope(
			[NotNull] this IFdbDatabaseScopeProvider provider,
			FdbDirectoryPath path,
			CancellationToken lifetime = default
		)
		{
			return provider.CreateScope<FdbDirectorySubspace>(async (db, cancel) =>
			{
				var folder = await db.Directory.CreateOrOpenAsync(path, cancel).ConfigureAwait(false);
				return (db, folder);
			}, lifetime);
		}

		/// <summary>Create a scope that will provider a directory subspace to all transactions</summary>
		/// <param name="db">Parent database</param>
		/// <param name="path">Path of the directory subspace that will be open (and created if necessary) for all the transactions started from this scope.</param>
		/// <param name="lifetime">Optional cancellation token that can be used to externally abort the new scope</param>
		[Pure, NotNull]
		public static IFdbDatabaseScopeProvider<FdbDirectorySubspace> CreateRootDirectoryScope(
			[NotNull] this IFdbDatabase db,
			FdbDirectoryPath path,
			CancellationToken lifetime = default
		)
		{
			return Fdb.CreateRootScope(db).CreateScope<FdbDirectorySubspace>(async (database, cancel) =>
			{
				var folder = await database.Directory.CreateOrOpenAsync(path, cancel).ConfigureAwait(false);
				return (database, folder);
			}, lifetime);
		}

		/// <summary>Wait for the scope to become ready.</summary>
		public static ValueTask EnsureIsReady([NotNull] this IFdbDatabaseScopeProvider provider, CancellationToken ct)
		{
			if (provider.IsAvailable)
			{
				return default;
			}
			return WaitForReadiness(provider, ct);
		}

		private static async ValueTask WaitForReadiness(IFdbDatabaseScopeProvider provider, CancellationToken ct)
		{
			_ = await provider.GetDatabase(ct).ConfigureAwait(false);
		}

		/// <summary>Runs a transactional lambda function inside a read-only transaction, which can be executed more than once if any retryable error occurs.</summary>
		/// <param name="provider">Provider of the database</param>
		/// <param name="handler">Asynchronous handler that will be retried until it succeeds, or a non-recoverable error occurs.</param>
		/// <param name="ct">Token used to cancel the operation</param>
		/// <remarks>
		/// Since the handler can run more than once, and that there is no guarantee that the transaction commits once it returns, you MAY NOT mutate any global state (counters, cache, global dictionary) inside this lambda!
		/// You must wait for the Task to complete successfully before updating the global state of the application.
		/// </remarks>
		public static async Task<TResult> ReadAsync<TResult>(
			[NotNull] this IFdbDatabaseScopeProvider provider,
			[NotNull, InstantHandle] Func<IFdbReadOnlyTransaction, Task<TResult>> handler,
			CancellationToken ct)
		{
			var db = await provider.GetDatabase(ct).ConfigureAwait(false);
			return await db.ReadAsync(handler, ct).ConfigureAwait(false);
		}

		/// <summary>Runs a transactional lambda function inside a read-only transaction, which can be executed more than once if any retryable error occurs.</summary>
		/// <param name="provider">Provider of the database</param>
		/// <param name="handler">Asynchronous handler that will be retried until it succeeds, or a non-recoverable error occurs.</param>
		/// <param name="ct">Token used to cancel the operation</param>
		/// <remarks>
		/// Since the handler can run more than once, and that there is no guarantee that the transaction commits once it returns, you MAY NOT mutate any global state (counters, cache, global dictionary) inside this lambda!
		/// You must wait for the Task to complete successfully before updating the global state of the application.
		/// </remarks>
		public static async Task<TResult> ReadAsync<TState, TResult>(
			[NotNull] this IFdbDatabaseScopeProvider<TState> provider,
			[NotNull, InstantHandle] Func<IFdbReadOnlyTransaction, TState, Task<TResult>> handler,
			CancellationToken ct)
		{
			var db = await provider.GetDatabase(ct).ConfigureAwait(false);
			return await db.ReadAsync(provider.GetState(), handler, ct).ConfigureAwait(false);
		}

		/// <summary>Run an idempotent transactional block that returns a value, inside a read-write transaction, which can be executed more than once if any retry-able error occurs.</summary>
		/// <param name="provider">Provider of the database</param>
		/// <param name="handler">Idempotent asynchronous lambda function that will be retried until the transaction commits, or a non-recoverable error occurs. The returned value of the last call will be the result of the operation.</param>
		/// <param name="ct">Token used to cancel the operation</param>
		/// <returns>Result of the lambda function if the transaction committed successfully.</returns>
		/// <remarks>
		/// You do not need to commit the transaction inside the handler, it will be done automatically.
		/// Since the <paramref name="handler"/> can run more than once, and that there is no guarantee that the transaction commits once it returns, you MAY NOT mutate any global state (counters, cache, global dictionary) inside this lambda!
		/// You must wait for the Task to complete successfully before updating the global state of the application.
		/// </remarks>
		public static async Task<TResult> ReadWriteAsync<TResult>(
			[NotNull] this IFdbDatabaseScopeProvider provider,
			[NotNull, InstantHandle] Func<IFdbTransaction, Task<TResult>> handler,
			CancellationToken ct)
		{
			var db = await provider.GetDatabase(ct).ConfigureAwait(false);
			return await db.ReadWriteAsync(handler, ct).ConfigureAwait(false);
		}

		/// <summary>Run an idempotent transactional block that returns a value, inside a read-write transaction, which can be executed more than once if any retry-able error occurs.</summary>
		/// <param name="provider">Provider of the database</param>
		/// <param name="handler">Idempotent asynchronous lambda function that will be retried until the transaction commits, or a non-recoverable error occurs. The returned value of the last call will be the result of the operation.</param>
		/// <param name="ct">Token used to cancel the operation</param>
		/// <returns>Result of the lambda function if the transaction committed successfully.</returns>
		/// <remarks>
		/// You do not need to commit the transaction inside the handler, it will be done automatically.
		/// Since the <paramref name="handler"/> can run more than once, and that there is no guarantee that the transaction commits once it returns, you MAY NOT mutate any global state (counters, cache, global dictionary) inside this lambda!
		/// You must wait for the Task to complete successfully before updating the global state of the application.
		/// </remarks>
		public static async Task<TResult> ReadWriteAsync<TState, TResult>(
			[NotNull] this IFdbDatabaseScopeProvider<TState> provider,
			[NotNull, InstantHandle] Func<IFdbTransaction, TState, Task<TResult>> handler,
			CancellationToken ct)
		{
			var db = await provider.GetDatabase(ct).ConfigureAwait(false);
			return await db.ReadWriteAsync(provider.GetState(), handler, ct).ConfigureAwait(false);
		}

		/// <summary>Run an idempotent transactional block that returns a value, inside a read-write transaction, which can be executed more than once if any retry-able error occurs.</summary>
		/// <param name="provider">Provider of the database</param>
		/// <param name="handler">Idempotent asynchronous lambda function that will be retried until the transaction commits, or a non-recoverable error occurs. The returned value of the last call will be the result of the operation.</param>
		/// <param name="ct">Token used to cancel the operation</param>
		/// <returns>Result of the lambda function if the transaction committed successfully.</returns>
		/// <remarks>
		/// You do not need to commit the transaction inside the handler, it will be done automatically.
		/// Since the <paramref name="handler"/> can run more than once, and that there is no guarantee that the transaction commits once it returns, you MAY NOT mutate any global state (counters, cache, global dictionary) inside this lambda!
		/// You must wait for the Task to complete successfully before updating the global state of the application.
		/// </remarks>
		public static async Task ReadWriteAsync(
			[NotNull] this IFdbDatabaseScopeProvider provider,
			[NotNull, InstantHandle] Func<IFdbTransaction, Task> handler,
			CancellationToken ct)
		{
			var db = await provider.GetDatabase(ct).ConfigureAwait(false);
			await db.ReadWriteAsync(handler, ct).ConfigureAwait(false);
		}

		/// <summary>Run an idempotent transactional block that returns a value, inside a read-write transaction, which can be executed more than once if any retry-able error occurs.</summary>
		/// <param name="provider">Provider of the database</param>
		/// <param name="handler">Idempotent asynchronous lambda function that will be retried until the transaction commits, or a non-recoverable error occurs. The returned value of the last call will be the result of the operation.</param>
		/// <param name="ct">Token used to cancel the operation</param>
		/// <returns>Result of the lambda function if the transaction committed successfully.</returns>
		/// <remarks>
		/// You do not need to commit the transaction inside the handler, it will be done automatically.
		/// Since the <paramref name="handler"/> can run more than once, and that there is no guarantee that the transaction commits once it returns, you MAY NOT mutate any global state (counters, cache, global dictionary) inside this lambda!
		/// You must wait for the Task to complete successfully before updating the global state of the application.
		/// </remarks>
		public static async Task ReadWriteAsync<TState>(
			[NotNull] this IFdbDatabaseScopeProvider<TState> provider,
			[NotNull, InstantHandle] Func<IFdbTransaction, TState, Task> handler,
			CancellationToken ct)
		{
			var db = await provider.GetDatabase(ct).ConfigureAwait(false);
			await db.ReadWriteAsync(provider.GetState(), handler, ct).ConfigureAwait(false);
		}

		/// <summary>Run an idempotent transaction block inside a write-only transaction, which can be executed more than once if any retry-able error occurs.</summary>
		/// <param name="provider">Provider of the database</param>
		/// <param name="handler">Idempotent handler that should only call write methods on the transaction, and may be retried until the transaction commits, or a non-recoverable error occurs.</param>
		/// <param name="ct">Token used to cancel the operation</param>
		/// <remarks>
		/// You do not need to commit the transaction inside the handler, it will be done automatically.
		/// Since the <paramref name="handler"/> can run more than once, and that there is no guarantee that the transaction commits once it returns, you MAY NOT mutate any global state (counters, cache, global dictionary) inside this lambda!
		/// You must wait for the Task to complete successfully before updating the global state of the application.
		/// </remarks>
		public static async Task WriteAsync(
			[NotNull] this IFdbDatabaseScopeProvider provider,
			[NotNull, InstantHandle] Action<IFdbTransaction> handler,
			CancellationToken ct)
		{
			var db = await provider.GetDatabase(ct).ConfigureAwait(false);
			await db.WriteAsync(handler, ct).ConfigureAwait(false);
		}

		/// <summary>Run an idempotent transaction block inside a write-only transaction, which can be executed more than once if any retry-able error occurs.</summary>
		/// <param name="provider">Provider of the database</param>
		/// <param name="handler">Idempotent handler that should only call write methods on the transaction, and may be retried until the transaction commits, or a non-recoverable error occurs.</param>
		/// <param name="ct">Token used to cancel the operation</param>
		/// <remarks>
		/// You do not need to commit the transaction inside the handler, it will be done automatically.
		/// Since the <paramref name="handler"/> can run more than once, and that there is no guarantee that the transaction commits once it returns, you MAY NOT mutate any global state (counters, cache, global dictionary) inside this lambda!
		/// You must wait for the Task to complete successfully before updating the global state of the application.
		/// </remarks>
		public static async Task WriteAsync<TState>(
			[NotNull] this IFdbDatabaseScopeProvider<TState> provider,
			[NotNull, InstantHandle] Action<IFdbTransaction, TState> handler,
			CancellationToken ct)
		{
			var db = await provider.GetDatabase(ct).ConfigureAwait(false);
			await db.WriteAsync(provider.GetState(), handler, ct).ConfigureAwait(false);
		}

	}
}
