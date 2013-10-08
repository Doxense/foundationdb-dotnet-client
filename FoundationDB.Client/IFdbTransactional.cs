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
	using System;
	using System.Threading;
	using System.Threading.Tasks;

	/// <summary>Transactional context that can execute read and/or write transactions</summary>
	public interface IFdbTransactional : IFdbReadOnlyTransactional
	{
		/// <summary>Runs an idempotent transactional block inside a write-only transaction context, with optional retry logic.</summary>
		/// <param name="handler">Idempotent handler that will be retried until the transaction commits, or a non-recoverable error occurs.</param>
		/// <param name="cancellationToken">Token used to cancel the operation</param>
		Task WriteAsync(Action<IFdbTransaction> handler, CancellationToken cancellationToken = default(CancellationToken));

		/// <summary>Runs an idempotent transactional block inside a read-write transaction context, with optional retry logic.</summary>
		/// <param name="asyncHandler">Idempotent asynchronous handler that will be retried until the transaction commits, or a non-recoverable error occurs.</param>
		/// <param name="cancellationToken">Token used to cancel the operation</param>
		Task ReadWriteAsync(Func<IFdbTransaction, Task> asyncHandler, CancellationToken cancellationToken = default(CancellationToken));

		/// <summary>Runs an idempotent transactional block that returns a value, inside a read-write transaction context, with optional retry logic.</summary>
		/// <param name="asyncHandler">Idempotent asynchronous lambda function that will be retried until the transaction commits, or a non-recoverable error occurs. The returned value of the last call will be the result of the operation.</param>
		/// <param name="cancellationToken">Token used to cancel the operation</param>
		/// <returns>Result of the lambda function if the transaction committed sucessfully.</returns>
		Task<R> ReadWriteAsync<R>(Func<IFdbTransaction, Task<R>> asyncHandler, CancellationToken cancellationToken = default(CancellationToken));

		/// <summary>[EXPERIMENTAL] do not use yet!.</summary>
		Task WriteAsync(Action<IFdbTransaction> handler, Action<IFdbTransaction> onDone, CancellationToken cancellationToken = default(CancellationToken));

		/// <summary>[EXPERIMENTAL] do not use yet!.</summary>
		Task ReadWriteAsync(Func<IFdbTransaction, Task> asyncHandler, Action<IFdbTransaction> onDone, CancellationToken cancellationToken = default(CancellationToken));

		/// <summary>[EXPERIMENTAL] do not use yet!.</summary>
		Task<R> ReadWriteAsync<R>(Func<IFdbTransaction, Task<R>> asyncHandler, Action<IFdbTransaction> onDone, CancellationToken cancellationToken = default(CancellationToken));
	}

}
