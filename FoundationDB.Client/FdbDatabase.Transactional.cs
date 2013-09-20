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
		/// Executes the provided async lambda function with a new read-only transaction
		/// </summary>
		public Task ReadAsync(Func<IFdbReadTransaction, Task> asyncAction, CancellationToken ct = default(CancellationToken))
		{
			return FdbOperationContext.RunReadAsync(
				db: this,
				asyncAction: asyncAction,
				ct: ct
			);
		}

		/// <summary>
		/// Executes the provided async lambda function with a new read-only transaction
		/// </summary>
		public Task<R> ReadAsync<R>(Func<IFdbReadTransaction, Task<R>> asyncAction, CancellationToken ct = default(CancellationToken))
		{
			return FdbOperationContext.RunReadWithResultAsync<R>(
				db: this,
				asyncAction: asyncAction,
				ct: ct
			);
		}

		#endregion

		#region Read/Write Transactionals...

		public Task WriteAsync(Action<IFdbTransaction> action, CancellationToken ct = default(CancellationToken))
		{
			return FdbOperationContext.RunWriteAsync(
				db: this,
				action: action,
				ct: ct
			);
		}

		public Task ReadWriteAsync(Func<IFdbTransaction, Task> asyncAction, CancellationToken ct = default(CancellationToken))
		{
			return FdbOperationContext.RunWriteAsync(
				db: this,
				asyncAction: asyncAction,
				ct: ct
			);
		}

		public Task<R> ReadWriteAsync<R>(Func<IFdbTransaction, Task<R>> asyncAction, CancellationToken ct = default(CancellationToken))
		{
			return FdbOperationContext.RunWriteWithResultAsync<R>(
				db: this,
				asyncAction: asyncAction,
				ct: ct
			);
		}

		#endregion

	}

}
