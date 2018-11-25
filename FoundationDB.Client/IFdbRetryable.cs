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
	using JetBrains.Annotations;
	using System;
	using System.Threading;
	using System.Threading.Tasks;

	/// <summary>Transactional context that can execute, inside a retry loop, idempotent actions using read and/or write transactions.</summary>
	[PublicAPI]
	public interface IFdbRetryable : IFdbReadOnlyRetryable
	{
		// note: see IFdbReadOnlyRetryable for comments about the differences between the .NET binding and other binding regarding the design of Transactionals

		#region Write Only

		/// <summary>Run an idempotent transaction block inside a write-only transaction, which can be executed more than once if any retryable error occurs.</summary>
		/// <param name="handler">Idempotent handler that should only call write methods on the transaction, and may be retried until the transaction commits, or a non-recoverable error occurs.</param>
		/// <param name="ct">Token used to cancel the operation</param>
		/// <remarks>
		/// You do not need to commit the transaction inside the handler, it will be done automatically.
		/// Since the <paramref name="handler"/> can run more than once, and that there is no guarantee that the transaction commits once it returns, you MAY NOT mutate any global state (counters, cache, global dictionary) inside this lambda!
		/// You must wait for the Task to complete successfully before updating the global state of the application.
		/// </remarks>
		Task WriteAsync([NotNull, InstantHandle] Action<IFdbTransaction> handler, CancellationToken ct);

		/// <summary>Run an idempotent transaction block inside a write-only transaction, which can be executed more than once if any retryable error occurs.</summary>
		/// <param name="state">State that will be passed back to the <paramref name="handler"/></param>
		/// <param name="handler">Idempotent handler that should only call write methods on the transaction, and may be retried until the transaction commits, or a non-recoverable error occurs.</param>
		/// <param name="ct">Token used to cancel the operation</param>
		/// <remarks>
		/// You do not need to commit the transaction inside the handler, it will be done automatically.
		/// Since the <paramref name="handler"/> can run more than once, and that there is no guarantee that the transaction commits once it returns, you MAY NOT mutate any global state (counters, cache, global dictionary) inside this lambda!
		/// You must wait for the Task to complete successfully before updating the global state of the application.
		/// </remarks>
		Task WriteAsync<TState>(TState state, [NotNull, InstantHandle] Action<IFdbTransaction, TState> handler, CancellationToken ct);

		/// <summary>Run an idempotent transaction block inside a write-only transaction, which can be executed more than once if any retryable error occurs.</summary>
		/// <param name="handler">Idempotent handler that should only call write methods on the transaction, and may be retried until the transaction commits, or a non-recoverable error occurs.</param>
		/// <param name="success">Will be called at most once, and only if the transaction commits successfully. Any exception or crash that happens right after the commit may cause this callback not NOT be called, even if the transaction has committed!</param>
		/// <param name="ct">Token used to cancel the operation</param>
		/// <remarks>
		/// You do not need to commit the transaction inside the handler, it will be done automatically.
		/// Since the <paramref name="handler"/> can run more than once, and that there is no guarantee that the transaction commits once it returns, you MAY NOT mutate any global state (counters, cache, global dictionary) inside this lambda!
		/// You must wait for the Task to complete successfully before updating the global state of the application.
		/// </remarks>
		Task WriteAsync([NotNull, InstantHandle] Action<IFdbTransaction> handler, [NotNull, InstantHandle] Action<IFdbTransaction> success, CancellationToken ct);

		/// <summary>Run an idempotent transaction block inside a write-only transaction, which can be executed more than once if any retryable error occurs.</summary>
		/// <param name="handler">Idempotent handler that should only call write methods on the transaction, and may be retried until the transaction commits, or a non-recoverable error occurs.</param>
		/// <param name="success">Will be called at most once, and only if the transaction commits successfully. Any exception or crash that happens right after the commit may cause this callback not NOT be called, even if the transaction has committed!</param>
		/// <param name="ct">Token used to cancel the operation</param>
		/// <remarks>
		/// You do not need to commit the transaction inside the handler, it will be done automatically.
		/// Since the <paramref name="handler"/> can run more than once, and that there is no guarantee that the transaction commits once it returns, you MAY NOT mutate any global state (counters, cache, global dictionary) inside this lambda!
		/// You must wait for the Task to complete successfully before updating the global state of the application.
		/// </remarks>
		Task WriteAsync([NotNull, InstantHandle] Action<IFdbTransaction> handler, [NotNull, InstantHandle] Func<IFdbTransaction, Task> success, CancellationToken ct);

		/// <summary>Run an idempotent transaction block inside a write-only transaction, which can be executed more than once if any retryable error occurs.</summary>
		/// <param name="handler">Idempotent handler that should only call write methods on the transaction, and may be retried until the transaction commits, or a non-recoverable error occurs.</param>
		/// <param name="success">Will be called at most once, and only if the transaction commits successfully. Any exception or crash that happens right after the commit may cause this callback not NOT be called, even if the transaction has committed!</param>
		/// <param name="ct">Token used to cancel the operation</param>
		/// <remarks>
		/// You do not need to commit the transaction inside the handler, it will be done automatically.
		/// Since the <paramref name="handler"/> can run more than once, and that there is no guarantee that the transaction commits once it returns, you MAY NOT mutate any global state (counters, cache, global dictionary) inside this lambda!
		/// You must wait for the Task to complete successfully before updating the global state of the application.
		/// </remarks>
		Task<TResult> WriteAsync<TResult>([NotNull, InstantHandle] Action<IFdbTransaction> handler, [NotNull, InstantHandle] Func<IFdbTransaction, TResult> success, CancellationToken ct);

		/// <summary>Run an idempotent transactional block inside a write-only transaction, which can be executed more than once if any retryable error occurs.</summary>
		/// <param name="handler">Idempotent async handler that will be retried until the transaction commits, or a non-recoverable error occurs.</param>
		/// <param name="ct">Token used to cancel the operation</param>
		/// <remarks>
		/// You do not need to commit the transaction inside the handler, it will be done automatically.
		/// Since the handler can run more than once, and that there is no guarantee that the transaction commits once it returns, you MAY NOT mutate any global state (counters, cache, global dictionary) inside this lambda!
		/// You must wait for the Task to complete successfully before updating the global state of the application.
		/// </remarks>
		Task WriteAsync([NotNull, InstantHandle] Func<IFdbTransaction, Task> handler, CancellationToken ct);

		/// <summary>Run an idempotent transactional block inside a write-only transaction, which can be executed more than once if any retryable error occurs.</summary>
		/// <param name="state">State that will be passed back to the <paramref name="handler"/></param>
		/// <param name="handler">Idempotent async handler that will be retried until the transaction commits, or a non-recoverable error occurs.</param>
		/// <param name="ct">Token used to cancel the operation</param>
		/// <remarks>
		/// You do not need to commit the transaction inside the handler, it will be done automatically.
		/// Since the handler can run more than once, and that there is no guarantee that the transaction commits once it returns, you MAY NOT mutate any global state (counters, cache, global dictionary) inside this lambda!
		/// You must wait for the Task to complete successfully before updating the global state of the application.
		/// </remarks>
		Task WriteAsync<TState>(TState state, [NotNull, InstantHandle] Func<IFdbTransaction, TState, Task> handler, CancellationToken ct);

		/// <summary>Run an idempotent transactional block inside a write-only transaction, which can be executed more than once if any retryable error occurs.</summary>
		/// <param name="handler">Idempotent async handler that will be retried until the transaction commits, or a non-recoverable error occurs.</param>
		/// <param name="success">Will be called at most once, and only if the transaction commits successfully. Any exception or crash that happens right after the commit may cause this callback not NOT be called, even if the transaction has committed!</param>
		/// <param name="ct">Token used to cancel the operation</param>
		/// <remarks>
		/// You do not need to commit the transaction inside the handler, it will be done automatically.
		/// Since the <paramref name="handler"/> can run more than once, and that there is no guarantee that the transaction commits once it returns, you MAY NOT mutate any global state (counters, cache, global dictionary) inside this lambda!
		/// You must wait for the Task to complete successfully before updating the global state of the application.
		/// </remarks>
		Task WriteAsync([NotNull, InstantHandle] Func<IFdbTransaction, Task> handler, [NotNull, InstantHandle] Action<IFdbTransaction> success, CancellationToken ct);

		/// <summary>Run an idempotent transactional block inside a write-only transaction, which can be executed more than once if any retryable error occurs.</summary>
		/// <param name="handler">Idempotent async handler that will be retried until the transaction commits, or a non-recoverable error occurs.</param>
		/// <param name="success">Will be called at most once, and only if the transaction commits successfully. Any exception or crash that happens right after the commit may cause this callback not NOT be called, even if the transaction has committed!</param>
		/// <param name="ct">Token used to cancel the operation</param>
		/// <remarks>
		/// You do not need to commit the transaction inside the handler, it will be done automatically.
		/// Since the <paramref name="handler"/> can run more than once, and that there is no guarantee that the transaction commits once it returns, you MAY NOT mutate any global state (counters, cache, global dictionary) inside this lambda!
		/// You must wait for the Task to complete successfully before updating the global state of the application.
		/// </remarks>
		Task WriteAsync([NotNull, InstantHandle] Func<IFdbTransaction, Task> handler, [NotNull, InstantHandle] Func<IFdbTransaction, Task> success, CancellationToken ct);

		#endregion

		#region Read + Write

		/// <summary>Run an idempotent transactional block inside a read-write transaction, which can be executed more than once if any retryable error occurs.</summary>
		/// <param name="handler">Idempotent asynchronous handler that will be retried until the transaction commits, or a non-recoverable error occurs.</param>
		/// <param name="ct">Token used to cancel the operation</param>
		/// <remarks>
		/// You do not need to commit the transaction inside the handler, it will be done automatically.
		/// Since the <paramref name="handler"/> can run more than once, and that there is no guarantee that the transaction commits once it returns, you MAY NOT mutate any global state (counters, cache, global dictionary) inside this lambda!
		/// You must wait for the Task to complete successfully before updating the global state of the application.
		/// </remarks>
		Task ReadWriteAsync([NotNull, InstantHandle] Func<IFdbTransaction, Task> handler, CancellationToken ct);

		/// <summary>Run an idempotent transactional block inside a read-write transaction, which can be executed more than once if any retryable error occurs.</summary>
		/// <param name="state">State that will be passed back to the <paramref name="handler"/></param>
		/// <param name="handler">Idempotent asynchronous handler that will be retried until the transaction commits, or a non-recoverable error occurs.</param>
		/// <param name="ct">Token used to cancel the operation</param>
		/// <remarks>
		/// You do not need to commit the transaction inside the handler, it will be done automatically.
		/// Since the <paramref name="handler"/> can run more than once, and that there is no guarantee that the transaction commits once it returns, you MAY NOT mutate any global state (counters, cache, global dictionary) inside this lambda!
		/// You must wait for the Task to complete successfully before updating the global state of the application.
		/// </remarks>
		Task ReadWriteAsync<TState>(TState state, [NotNull, InstantHandle] Func<IFdbTransaction, TState, Task> handler, CancellationToken ct);

		/// <summary>Run an idempotent transactional block inside a read-write transaction, which can be executed more than once if any retryable error occurs.</summary>
		/// <param name="handler">Idempotent asynchronous handler that will be retried until the transaction commits, or a non-recoverable error occurs.</param>
		/// <param name="success">Will be called at most once, and only if the transaction commits successfully. Any exception or crash that happens right after the commit may cause this callback not NOT be called, even if the transaction has committed!</param>
		/// <param name="ct">Token used to cancel the operation</param>
		/// <remarks>
		/// You do not need to commit the transaction inside the handler, it will be done automatically.
		/// Since the <paramref name="handler"/> can run more than once, and that there is no guarantee that the transaction commits once it returns, you MAY NOT mutate any global state (counters, cache, global dictionary) inside this lambda!
		/// You must wait for the Task to complete successfully before updating the global state of the application.
		/// </remarks>
		Task ReadWriteAsync([NotNull, InstantHandle] Func<IFdbTransaction, Task> handler, [NotNull, InstantHandle] Action<IFdbTransaction> success, CancellationToken ct);

		/// <summary>Run an idempotent transactional block that returns a value, inside a read-write transaction, which can be executed more than once if any retryable error occurs.</summary>
		/// <param name="handler">Idempotent asynchronous lambda function that will be retried until the transaction commits, or a non-recoverable error occurs. The returned value of the last call will be the result of the operation.</param>
		/// <param name="ct">Token used to cancel the operation</param>
		/// <returns>Result of the lambda function if the transaction committed successfully.</returns>
		/// <remarks>
		/// You do not need to commit the transaction inside the handler, it will be done automatically.
		/// Since the <paramref name="handler"/> can run more than once, and that there is no guarantee that the transaction commits once it returns, you MAY NOT mutate any global state (counters, cache, global dictionary) inside this lambda!
		/// You must wait for the Task to complete successfully before updating the global state of the application.
		/// </remarks>
		Task<TResult> ReadWriteAsync<TResult>([NotNull, InstantHandle] Func<IFdbTransaction, TResult> handler, CancellationToken ct);

		/// <summary>Run an idempotent transactional block that returns a value, inside a read-write transaction, which can be executed more than once if any retryable error occurs.</summary>
		/// <param name="handler">Idempotent asynchronous lambda function that will be retried until the transaction commits, or a non-recoverable error occurs. The returned value of the last call will be the result of the operation.</param>
		/// <param name="ct">Token used to cancel the operation</param>
		/// <returns>Result of the lambda function if the transaction committed successfully.</returns>
		/// <remarks>
		/// You do not need to commit the transaction inside the handler, it will be done automatically.
		/// Since the <paramref name="handler"/> can run more than once, and that there is no guarantee that the transaction commits once it returns, you MAY NOT mutate any global state (counters, cache, global dictionary) inside this lambda!
		/// You must wait for the Task to complete successfully before updating the global state of the application.
		/// </remarks>
		Task<TResult> ReadWriteAsync<TResult>([NotNull, InstantHandle] Func<IFdbTransaction, Task<TResult>> handler, CancellationToken ct);

		/// <summary>Run an idempotent transactional block that returns a value, inside a read-write transaction, which can be executed more than once if any retryable error occurs.</summary>
		/// <param name="state">State that will be passed back to the <paramref name="handler"/></param>
		/// <param name="handler">Idempotent asynchronous lambda function that will be retried until the transaction commits, or a non-recoverable error occurs. The returned value of the last call will be the result of the operation.</param>
		/// <param name="ct">Token used to cancel the operation</param>
		/// <returns>Result of the lambda function if the transaction committed successfully.</returns>
		/// <remarks>
		/// You do not need to commit the transaction inside the handler, it will be done automatically.
		/// Since the <paramref name="handler"/> can run more than once, and that there is no guarantee that the transaction commits once it returns, you MAY NOT mutate any global state (counters, cache, global dictionary) inside this lambda!
		/// You must wait for the Task to complete successfully before updating the global state of the application.
		/// </remarks>
		Task<TResult> ReadWriteAsync<TState, TResult>(TState state, [NotNull, InstantHandle] Func<IFdbTransaction, TState, Task<TResult>> handler, CancellationToken ct);

		/// <summary>Run an idempotent transactional block that returns a value, inside a read-write transaction, which can be executed more than once if any retryable error occurs.</summary>
		/// <param name="handler">Idempotent asynchronous lambda function that will be retried until the transaction commits, or a non-recoverable error occurs. The returned value of the last call will be the result of the operation.</param>
		/// <param name="success">Will be called at most once with the result of <paramref name="handler"/>, and only if the transaction commits successfully. Any exception or crash that happens right after the commit may cause this callback not NOT be called, even if the transaction has committed!</param>
		/// <param name="ct">Token used to cancel the operation</param>
		/// <returns>Result of the lambda function if the transaction committed successfully.</returns>
		/// <remarks>
		/// You do not need to commit the transaction inside the handler, it will be done automatically.
		/// Since the <paramref name="handler"/> can run more than once, and that there is no guarantee that the transaction commits once it returns, you MAY NOT mutate any global state (counters, cache, global dictionary) inside this lambda!
		/// You must wait for the Task to complete successfully before updating the global state of the application.
		/// </remarks>
		Task<TResult> ReadWriteAsync<TResult>([NotNull, InstantHandle] Func<IFdbTransaction, Task<TResult>> handler, [NotNull, InstantHandle] Action<IFdbTransaction, TResult> success, CancellationToken ct);

		/// <summary>Run an idempotent transactional block that returns a value, inside a read-write transaction, which can be executed more than once if any retryable error occurs.</summary>
		/// <param name="handler">Idempotent asynchronous lambda function that will be retried until the transaction commits, or a non-recoverable error occurs. The returned value of the last call will be the result of the operation.</param>
		/// <param name="success">Will be called at most once with the result of <paramref name="handler"/>, and only if the transaction commits successfully. Any exception or crash that happens right after the commit may cause this callback not NOT be called, even if the transaction has committed!</param>
		/// <param name="ct">Token used to cancel the operation</param>
		/// <returns>Result of the <paramref name="success"/>lambda function if the transaction committed successfully.</returns>
		/// <remarks>
		/// You do not need to commit the transaction inside the handler, it will be done automatically.
		/// Since the <paramref name="handler"/> can run more than once, and that there is no guarantee that the transaction commits once it returns, you MAY NOT mutate any global state (counters, cache, global dictionary) inside this lambda!
		/// You must wait for the Task to complete successfully before updating the global state of the application.
		/// </remarks>
		Task<TResult> ReadWriteAsync<TIntermediate, TResult>([NotNull, InstantHandle] Func<IFdbTransaction, Task<TIntermediate>> handler, [NotNull, InstantHandle] Func<IFdbTransaction, TIntermediate, TResult> success, CancellationToken ct);

		/// <summary>Run an idempotent transactional block that returns a value, inside a read-write transaction, which can be executed more than once if any retryable error occurs.</summary>
		/// <param name="handler">Idempotent asynchronous lambda function that will be retried until the transaction commits, or a non-recoverable error occurs. The returned value of the last call will be the result of the operation.</param>
		/// <param name="success">Will be called at most once with the result of <paramref name="handler"/>, and only if the transaction commits successfully. Any exception or crash that happens right after the commit may cause this callback not NOT be called, even if the transaction has committed!</param>
		/// <param name="ct">Token used to cancel the operation</param>
		/// <returns>Result of the <paramref name="success"/>lambda function if the transaction committed successfully.</returns>
		/// <remarks>
		/// You do not need to commit the transaction inside the handler, it will be done automatically.
		/// Since the <paramref name="handler"/> can run more than once, and that there is no guarantee that the transaction commits once it returns, you MAY NOT mutate any global state (counters, cache, global dictionary) inside this lambda!
		/// You must wait for the Task to complete successfully before updating the global state of the application.
		/// </remarks>
		Task<TResult> ReadWriteAsync<TIntermediate, TResult>([NotNull, InstantHandle] Func<IFdbTransaction, Task<TIntermediate>> handler, [NotNull, InstantHandle] Func<IFdbTransaction, TIntermediate, Task<TResult>> success, CancellationToken ct);

		#endregion
	}

}
