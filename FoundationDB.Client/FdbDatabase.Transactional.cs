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
	using System.Threading;
	using System.Threading.Tasks;

	/// <summary>FoundationDB Database</summary>
	/// <remarks>Wraps an FDBDatabase* handle</remarks>
	public partial class FdbDatabase
	{

		#region Readonly Transactionals...

		/// <summary>
		/// Runs a transactional lambda function against this database, inside a read-only transaction context, with retry logic.
		/// </summary>
		/// <param name="asyncHandler">Asynchronous lambda function that is passed a new read-only transaction on each retry.</param>
		/// <param name="ct">Optional cancellation token that will be passed to the transaction context, and that can also be used to abort the retry loop.</param>
		public Task ReadAsync(Func<IFdbReadOnlyTransaction, Task> asyncHandler, CancellationToken ct = default(CancellationToken))
		{
			return FdbOperationContext.RunReadAsync(
				db: this,
				asyncAction: asyncHandler,
				ct: ct
			);
		}

		/// <summary>
		/// Runs a transactional lambda function against this database, inside a read-only transaction context, with retry logic.
		/// </summary>
		/// <param name="asyncHandler">Asynchronous lambda function that is passed a new read-only transaction on each retry. The result of the task will also be the result of the transactional.</param>
		/// <param name="ct">Optional cancellation token that will be passed to the transaction context, and that can also be used to abort the retry loop.</param>
		public Task<R> ReadAsync<R>(Func<IFdbReadOnlyTransaction, Task<R>> asyncHandler, CancellationToken ct = default(CancellationToken))
		{
			return FdbOperationContext.RunReadWithResultAsync<R>(
				db: this,
				asyncAction: asyncHandler,
				ct: ct
			);
		}

		#endregion

		#region Read/Write Transactionals...

		/// <summary>
		/// Runs a transactional lambda function against this database, inside a write-only transaction context, with retry logic.
		/// </summary>
		/// <param name="handler">Lambda function that is passed a new read-write transaction on each retry. It should only call non-async methods, such as Set, Clear or any atomic operation.</param>
		/// <param name="ct">Optional cancellation token that will be passed to the transaction context, and that can also be used to abort the retry loop.</param>
		public Task WriteAsync(Action<IFdbTransaction> handler, CancellationToken ct = default(CancellationToken))
		{
			return FdbOperationContext.RunWriteAsync(
				db: this,
				action: handler,
				ct: ct
			);
		}

		/// <summary>
		/// Runs a transactional lambda function against this database, inside a read-write transaction context, with retry logic.
		/// </summary>
		/// <param name="asyncHandler">Asynchronous lambda function that is passed a new read-write transaction on each retry.</param>
		/// <param name="ct">Optional cancellation token that will be passed to the transaction context, and that can also be used to abort the retry loop.</param>
		public Task ReadWriteAsync(Func<IFdbTransaction, Task> asyncHandler, CancellationToken ct = default(CancellationToken))
		{
			return FdbOperationContext.RunWriteAsync(
				db: this,
				asyncAction: asyncHandler,
				ct: ct
			);
		}

		/// <summary>
		/// Runs a transactional lambda function against this database, inside a read-write transaction context, with retry logic.
		/// </summary>
		/// <param name="asyncHandler">Asynchronous lambda function that is passed a new read-write transaction on each retry. The result of the task will also be the result of the transactional.</param>
		/// <param name="ct">Optional cancellation token that will be passed to the transaction context, and that can also be used to abort the retry loop.</param>
		public Task<R> ReadWriteAsync<R>(Func<IFdbTransaction, Task<R>> asyncHandler, CancellationToken ct = default(CancellationToken))
		{
			return FdbOperationContext.RunWriteWithResultAsync<R>(
				db: this,
				asyncAction: asyncHandler,
				ct: ct
			);
		}

		#endregion

	}

}
