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
	using JetBrains.Annotations;
	using System;
	using System.Threading;
	using System.Threading.Tasks;

	/// <summary>Transactional context that can execute, inside a retry loop, idempotent actions using read and/or write transactions.</summary>
	public interface IFdbRetryable : IFdbReadOnlyRetryable
	{
		// note: see IFdbReadOnlyRetryable for comments about the differences between the .NET binding and other binding regarding the design of Transactionals

		/// <summary>Run an idempotent transaction block inside a write-only transaction, which can be executed more than once if any retryable error occurs.</summary>
		/// <param name="handler">Idempotent handler that should only call write methods on the transation, and may be retried until the transaction commits, or a non-recoverable error occurs.</param>
		/// <param name="cancellationToken">Token used to cancel the operation</param>
		/// <remarks>
		/// You do not need to commit the transaction inside the handler, it will be done automatically.
		/// Since the handler can run more than once, and that there is no guarantee that the transaction commits once it returns, you MAY NOT mutate any global state (counters, cache, global dictionary) inside this lambda!
		/// You must wait for the Task to complete successfully before updating the global state of the application.
		/// </remarks>
		Task WriteAsync([NotNull][InstantHandle]  Action<IFdbTransaction> handler, CancellationToken cancellationToken);

		/// <summary>Run an idempotent transactional block inside a write-only transaction, which can be executed more than once if any retryable error occurs.</summary>
		/// <param name="handler">Idempotent async handler that will be retried until the transaction commits, or a non-recoverable error occurs.</param>
		/// <param name="cancellationToken">Token used to cancel the operation</param>
		/// <remarks>
		/// You do not need to commit the transaction inside the handler, it will be done automatically.
		/// Since the handler can run more than once, and that there is no guarantee that the transaction commits once it returns, you MAY NOT mutate any global state (counters, cache, global dictionary) inside this lambda!
		/// You must wait for the Task to complete successfully before updating the global state of the application.
		/// </remarks>
		Task WriteAsync([NotNull][InstantHandle]  Func<IFdbTransaction, Task> handler, CancellationToken cancellationToken);

		/// <summary>Run an idempotent transactional block inside a read-write transaction, which can be executed more than once if any retryable error occurs.</summary>
		/// <param name="asyncHandler">Idempotent asynchronous handler that will be retried until the transaction commits, or a non-recoverable error occurs.</param>
		/// <param name="cancellationToken">Token used to cancel the operation</param>
		/// <remarks>
		/// You do not need to commit the transaction inside the handler, it will be done automatically.
		/// Since the handler can run more than once, and that there is no guarantee that the transaction commits once it returns, you MAY NOT mutate any global state (counters, cache, global dictionary) inside this lambda!
		/// You must wait for the Task to complete successfully before updating the global state of the application.
		/// </remarks>
		Task ReadWriteAsync([NotNull][InstantHandle]  Func<IFdbTransaction, Task> asyncHandler, CancellationToken cancellationToken);

		/// <summary>Run an idempotent transactional block that returns a value, inside a read-write transaction, which can be executed more than once if any retryable error occurs.</summary>
		/// <param name="asyncHandler">Idempotent asynchronous lambda function that will be retried until the transaction commits, or a non-recoverable error occurs. The returned value of the last call will be the result of the operation.</param>
		/// <param name="cancellationToken">Token used to cancel the operation</param>
		/// <returns>Result of the lambda function if the transaction committed sucessfully.</returns>
		/// <remarks>
		/// You do not need to commit the transaction inside the handler, it will be done automatically.
		/// Since the handler can run more than once, and that there is no guarantee that the transaction commits once it returns, you MAY NOT mutate any global state (counters, cache, global dictionary) inside this lambda!
		/// You must wait for the Task to complete successfully before updating the global state of the application.
		/// </remarks>
		Task<R> ReadWriteAsync<R>([NotNull][InstantHandle]  Func<IFdbTransaction, Task<R>> asyncHandler, CancellationToken cancellationToken);

		//REVIEW: should we keep these ?

		/// <summary>[EXPERIMENTAL] do not use yet!.</summary>
		Task WriteAsync([NotNull][InstantHandle]  Action<IFdbTransaction> handler, [NotNull][InstantHandle]  Action<IFdbTransaction> onDone, CancellationToken cancellationToken);

		/// <summary>[EXPERIMENTAL] do not use yet!.</summary>
		Task WriteAsync([NotNull][InstantHandle]  Func<IFdbTransaction, Task> handler, [NotNull][InstantHandle]  Action<IFdbTransaction> onDone, CancellationToken cancellationToken);

		/// <summary>[EXPERIMENTAL] do not use yet!.</summary>
		Task ReadWriteAsync([NotNull][InstantHandle]  Func<IFdbTransaction, Task> asyncHandler, [NotNull][InstantHandle]  Action<IFdbTransaction> onDone, CancellationToken cancellationToken);

		/// <summary>[EXPERIMENTAL] do not use yet!.</summary>
		Task<R> ReadWriteAsync<R>([NotNull][InstantHandle]  Func<IFdbTransaction, Task<R>> asyncHandler, [NotNull][InstantHandle]  Action<IFdbTransaction> onDone, CancellationToken cancellationToken);
	}

}
